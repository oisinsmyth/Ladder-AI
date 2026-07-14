using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `LIMIT` — clamp/limiter box instruction (Phase 2 Tier 1, 2026-07-14). Grounded against a real
/// export sweep of `JOB9002`'s full block inventory (`FB VSDSim`'s own numeric-simulation ladder, 2
/// instances): `&lt;Part Name="LIMIT" Version="1.0" UId="N"&gt;&lt;TemplateValue
/// Name="value_type" Type="Type"&gt;Real&lt;/TemplateValue&gt;&lt;/Part&gt;` — genuinely
/// different arity from every other typed instruction so far: three fixed-named tag-or-literal
/// inputs (`MN`/`IN`/`MX` — minimum, value, maximum, all uppercase) and an uppercase `OUT`, not
/// the single lowercase `in`/`out` every other typed instruction (Convert/Swap/Abs) uses. No
/// `DisabledENO` in either real instance — checked for absence, not assumed.
/// </summary>
public class LimitTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_LimitFedByRail_ProducesLimitPart()
    {
        var network = LoadFixture("LimitFedByRail.xml");

        var limit = Assert.Single(network.Parts, p => p.Name == "LIMIT");
        Assert.Equal(27, limit.UId);
        Assert.Equal("1.0", limit.Version);
        Assert.Equal("Real", limit.SrcType);
    }

    [Fact]
    public void Reduce_LimitFedByRail_EnIsContactConditionAndAllThreeOperandsResolve()
    {
        var network = LoadFixture("LimitFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Clamp value", compileUnitUId: "62");

        var limit = Assert.Single(reduced.Network.Limits);
        var enCondition = Assert.IsType<EnSource.Condition>(limit.En);
        Assert.Equal("GateBit", Assert.IsType<Expr.TagRef>(enCondition.Value).Path);
        Assert.Equal("LowBound", Assert.IsType<Expr.TagRef>(limit.Min).Path);
        Assert.Equal("RawValue", Assert.IsType<Expr.TagRef>(limit.In).Path);
        Assert.Equal("HighBound", Assert.IsType<Expr.TagRef>(limit.Max).Path);
        Assert.Equal("ClampedValue", limit.DestTag);
    }

    [Fact]
    public void Reduce_LimitFedByRail_SidecarRecordsVersionAndValueType()
    {
        var network = LoadFixture("LimitFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Clamp value", compileUnitUId: "62");

        var sidecar = Assert.Single(reduced.Sidecar.Limits);
        Assert.Equal("1.0", sidecar.Version);
        Assert.Equal("Real", sidecar.ValueType);
        var enSidecar = Assert.IsType<EnSourceSidecar.ConditionSidecar>(sidecar.En);
        Assert.Equal(28, enSidecar.RailWireUId);
    }

    [Fact]
    public void RoundTrip_LimitFedByRail_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("LimitFedByRail.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Clamp value", compileUnitUId: "62");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var limit = Assert.Single(reparsed.Parts, p => p.Name == "LIMIT");
        Assert.Equal("1.0", limit.Version);
        Assert.Equal("Real", limit.SrcType);

        var outWire = Assert.Single(reparsed.Wires, w => w.UId == 34);
        Assert.Contains(outWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 27 && e.PortName == "OUT");
    }

    [Fact]
    public void SerializeNetworkOnly_LimitFedByRail_ProducesReadableLimitSyntax()
    {
        var network = LoadFixture("LimitFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Clamp value", compileUnitUId: "62");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Contains("  LIMIT(EN := GateBit, MN := LowBound, IN := RawValue, MX := HighBound) => ClampedValue\n", text);
    }

    [Fact]
    public void FullBlock_LimitFedByRail_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("LimitFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Clamp value", compileUnitUId: "62");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_LimitMissingValueType_ThrowsSimaticMlFormatException()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="LIMIT" Version="1.0" UId="1" DisabledENO="true" />
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<SimaticMlFormatException>(() => FlgNetParser.Parse(element));
        Assert.Contains("value_type", ex.Message);
    }

    [Fact]
    public void Parse_LimitMissingDisabledEno_ThrowsUnsupportedConstruct()
    {
        // TIA's own Import() validator rejects a LIMIT without DisabledENO="true" — "ENO cannot
        // be deactivated for the 'LIMIT' instruction" — confirmed live, 2026-07-14, correcting an
        // earlier reading of the real export that had missed this attribute.
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="LIMIT" Version="1.0" UId="1">
                  <TemplateValue Name="value_type" Type="Type">Real</TemplateValue>
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
