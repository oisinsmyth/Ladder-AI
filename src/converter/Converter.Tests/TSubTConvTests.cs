using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `T_SUB`/`T_CONV` — time-arithmetic subtraction and type-conversion box instructions (Phase 2
/// Tier 2, 2026-07-14). Grounded against a real export sweep of `JOB9002`'s full block inventory
/// (`FB VibratorCycle`'s own time-tracking ladder): a real `T_SUB -&gt; T_CONV -&gt; Convert` ENO
/// chain. `T_SUB` is binary (`IN1`/`IN2` -&gt; `OUT`, all uppercase, unlike Sub's lowercase) with
/// two independently-typed operands (`date_type`/`time_type`, both `"Time"` in the one real
/// instance). `T_CONV` is structurally identical to Convert (single `IN` -&gt; `OUT`, both
/// uppercase) with lowercase `src_type`/`dest_type` TemplateValue names. Neither carries
/// `DisabledENO` at all. Both confirmed real as ENO-chain participants — `T_SUB`'s own `eno`
/// feeds `T_CONV`'s `en` directly, extending `ResolveEnSource`'s existing Mul/Convert/Sub/Div
/// allowlist.
///
/// The fixture's own two references to the same conceptual tag (`StartTime`, read by both
/// `T_SUB.IN1` and `T_CONV.IN`) deliberately use two separate `&lt;Access&gt;` elements with
/// different UIds rather than one `IdentCon` reused across two `Wire`s — confirmed live,
/// 2026-07-14: TIA's own Import() validator rejects the latter ("The connection ... is used
/// multiple times at the cables"), and the real source itself (`VibratorCycleTimer.PT`/`.ET`,
/// `Remainder`, `UDintTime`, `time` — this same VibratorCycle network) always declares a fresh
/// Access UId per reference, never dedupes by tag name. Worth remembering when hand-building any
/// future fixture that reads the same tag from two wire positions.
/// </summary>
public class TSubTConvTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_TSubThenTConvViaEno_ProducesBothParts()
    {
        var network = LoadFixture("TSubThenTConvViaEno.xml");

        var tSub = Assert.Single(network.Parts, p => p.Name == "T_SUB");
        Assert.Equal("1.2", tSub.Version);
        Assert.Equal("Time", tSub.SrcType);
        Assert.Equal("Time", tSub.TimeType);

        var tConv = Assert.Single(network.Parts, p => p.Name == "T_CONV");
        Assert.Equal("1.2", tConv.Version);
        Assert.Equal("Time", tConv.SrcType);
        Assert.Equal("UDInt", tConv.DestType);
    }

    [Fact]
    public void Reduce_TSubThenTConvViaEno_TSubEnIsRailAndTConvEnIsPrecedingEno()
    {
        var network = LoadFixture("TSubThenTConvViaEno.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Elapsed time", compileUnitUId: "62");

        var tSub = Assert.Single(reduced.Network.TSubs);
        var tSubEn = Assert.IsType<EnSource.Condition>(tSub.En);
        Assert.Empty(Assert.IsType<Expr.And>(tSubEn.Value).Operands);
        Assert.Equal("StartTime", Assert.IsType<Expr.TagRef>(tSub.In1).Path);
        Assert.Equal("EndTime", Assert.IsType<Expr.TagRef>(tSub.In2).Path);
        Assert.Equal("ElapsedTime", tSub.DestTag);

        var tConv = Assert.Single(reduced.Network.TConvs);
        Assert.IsType<EnSource.PrecedingEno>(tConv.En);
        Assert.Equal("StartTime", Assert.IsType<Expr.TagRef>(tConv.In).Path);
        Assert.Equal("ElapsedMillis", tConv.DestTag);
    }

    [Fact]
    public void Reduce_TSubThenTConvViaEno_SidecarsRecordVersionAndTypes()
    {
        var network = LoadFixture("TSubThenTConvViaEno.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Elapsed time", compileUnitUId: "62");

        var tSubSidecar = Assert.Single(reduced.Sidecar.TSubs);
        Assert.Equal("1.2", tSubSidecar.Version);
        Assert.Equal("Time", tSubSidecar.DateType);
        Assert.Equal("Time", tSubSidecar.TimeType);

        var tConvSidecar = Assert.Single(reduced.Sidecar.TConvs);
        Assert.Equal("1.2", tConvSidecar.Version);
        Assert.Equal("Time", tConvSidecar.SrcType);
        Assert.Equal("UDInt", tConvSidecar.DestType);
        var tConvEnSidecar = Assert.IsType<EnSourceSidecar.PrecedingEnoSidecar>(tConvSidecar.En);
        Assert.Equal(25, tConvEnSidecar.PrecedingPartUId);
    }

    [Fact]
    public void RoundTrip_TSubThenTConvViaEno_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("TSubThenTConvViaEno.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Elapsed time", compileUnitUId: "62");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var tSub = Assert.Single(reparsed.Parts, p => p.Name == "T_SUB");
        Assert.Equal("1.2", tSub.Version);

        var tConv = Assert.Single(reparsed.Parts, p => p.Name == "T_CONV");
        Assert.Equal("1.2", tConv.Version);

        var enoWire = Assert.Single(reparsed.Wires, w => w.UId == 30);
        Assert.Contains(enoWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 25 && e.PortName == "eno");
        Assert.Contains(enoWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 26 && e.PortName == "en");
    }

    [Fact]
    public void SerializeNetworkOnly_TSubThenTConvViaEno_ProducesReadableSyntax()
    {
        var network = LoadFixture("TSubThenTConvViaEno.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Elapsed time", compileUnitUId: "62");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Contains("  T_SUB(EN := TRUE, IN1 := StartTime, IN2 := EndTime) => ElapsedTime\n", text);
        Assert.Contains("  T_CONV(EN := ENO, IN := StartTime) => ElapsedMillis\n", text);
    }

    [Fact]
    public void FullBlock_TSubThenTConvViaEno_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("TSubThenTConvViaEno.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Elapsed time", compileUnitUId: "62");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_TSubMissingTimeType_ThrowsSimaticMlFormatException()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="T_SUB" Version="1.2" UId="1">
                  <TemplateValue Name="date_type" Type="Type">Time</TemplateValue>
                </Part>
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<SimaticMlFormatException>(() => FlgNetParser.Parse(element));
        Assert.Contains("time_type", ex.Message);
    }

    [Fact]
    public void Parse_TConvWithDisabledEno_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="T_CONV" Version="1.2" UId="1" DisabledENO="true">
                  <TemplateValue Name="src_type" Type="Type">Time</TemplateValue>
                  <TemplateValue Name="dest_type" Type="Type">UDInt</TemplateValue>
                </Part>
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(element));
        Assert.Contains("DisabledENO", ex.Message);
    }
}
