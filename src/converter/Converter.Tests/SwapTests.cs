using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `Swap` — byte-swap box instruction (S1 item 25, 2026-07-12). Picked up per the project owner's
/// own explicit choice, to ground and close the last remaining gap in `PlantAutoControl`'s 8 dependency
/// FBs: `FB TomraControlSystem` hard-errors on `Part Name="Swap"`, unsupported.
///
/// Grounded against real `TomraControlSystem` before any code (2 independent instances, identical
/// shape): `&lt;Part Name="Swap" UId="N" DisabledENO="true"&gt;&lt;TemplateValue Name="SrcType"
/// Type="Type"&gt;Word&lt;/TemplateValue&gt;&lt;/Part&gt;` — structurally identical to `Convert`
/// (`en`-gated via `EnSource`, a single tag-or-literal `in`, one destination tag via `out`,
/// `DisabledENO="true"`) minus `DestType` — a byte-swap doesn't change the value's type, so
/// there's nothing to declare a destination type for. Modeled as its own `SwapStatement`/
/// `SwapStatementSidecar` rather than folding into `ConvertStatement` with a nullable `DestType`
/// — project owner's own explicit call, keeping each source Part Name mapped to its own IR
/// construct (same precedent as `MulKind`/`TimerKind` staying separate variants).
/// </summary>
public class SwapTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_SwapFedByRail_ProducesSwapPart()
    {
        var network = LoadFixture("SwapFedByRail.xml");

        var swap = Assert.Single(network.Parts, p => p.Name == "Swap");
        Assert.Equal(25, swap.UId);
        Assert.Equal("Word", swap.SrcType);
        Assert.Null(swap.DestType);
    }

    [Fact]
    public void Reduce_SwapFedByRail_EnIsContactConditionAndValueResolves()
    {
        var network = LoadFixture("SwapFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Byte swap", compileUnitUId: "62");

        var swap = Assert.Single(reduced.Network.Swaps);
        var enCondition = Assert.IsType<EnSource.Condition>(swap.En);
        Assert.Equal("GateBit", Assert.IsType<Expr.TagRef>(enCondition.Value).Path);
        Assert.Equal("ControlWord", Assert.IsType<Expr.TagRef>(swap.In).Path);
        Assert.Equal("SwappedWord", swap.DestTag);
    }

    [Fact]
    public void Reduce_SwapFedByRail_SidecarRecordsConditionEnSourceAndSrcType()
    {
        var network = LoadFixture("SwapFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Byte swap", compileUnitUId: "62");

        var sidecar = Assert.Single(reduced.Sidecar.Swaps);
        var enSidecar = Assert.IsType<EnSourceSidecar.ConditionSidecar>(sidecar.En);
        Assert.Equal(26, enSidecar.RailWireUId);
        Assert.Single(enSidecar.Steps);
        Assert.Equal("Word", sidecar.SrcType);
    }

    [Fact]
    public void RoundTrip_SwapFedByRail_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("SwapFedByRail.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Byte swap", compileUnitUId: "62");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var swap = Assert.Single(reparsed.Parts, p => p.Name == "Swap");
        Assert.Equal("Word", swap.SrcType);
        Assert.Null(swap.DestType);

        var enWire = Assert.Single(reparsed.Wires, w => w.UId == 28);
        Assert.Contains(enWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 24 && e.PortName == "out");
        Assert.Contains(enWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 25 && e.PortName == "en");
    }

    [Fact]
    public void SerializeNetworkOnly_SwapFedByRail_ProducesReadableSwapSyntax()
    {
        var network = LoadFixture("SwapFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Byte swap", compileUnitUId: "62");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Contains("  SWAP(EN := GateBit, IN := ControlWord) => SwappedWord\n", text);
    }

    [Fact]
    public void FullBlock_SwapFedByRail_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("SwapFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Byte swap", compileUnitUId: "62");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_SwapMissingSrcType_ThrowsSimaticMlFormatException()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Swap" UId="1" DisabledENO="true" />
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<SimaticMlFormatException>(() => FlgNetParser.Parse(element));
        Assert.Contains("SrcType", ex.Message);
    }

    [Fact]
    public void Parse_SwapMissingDisabledEno_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Swap" UId="1">
                  <TemplateValue Name="SrcType" Type="Type">Word</TemplateValue>
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
