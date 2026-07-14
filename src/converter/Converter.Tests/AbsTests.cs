using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `Abs` — absolute-value box instruction (Phase 2 Tier 1, 2026-07-14). Grounded against a real
/// export sweep of `JOB9002`'s full block inventory (`FB VSDSim`'s own numeric-simulation ladder):
/// `&lt;Part Name="Abs" UId="N" DisabledENO="true"&gt;&lt;TemplateValue Name="SrcType"
/// Type="Type"&gt;Real&lt;/TemplateValue&gt;&lt;/Part&gt;` — structurally identical to `Swap`
/// (`en`-gated via `EnSource`, a single tag-or-literal `in`, one destination tag via `out`,
/// `DisabledENO="true"`, one `SrcType`). Modeled as its own `AbsStatement`/`AbsStatementSidecar`
/// for the same reason Swap didn't fold into ConvertStatement — each source Part Name keeps its
/// own IR construct.
/// </summary>
public class AbsTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_AbsFedByRail_ProducesAbsPart()
    {
        var network = LoadFixture("AbsFedByRail.xml");

        var abs = Assert.Single(network.Parts, p => p.Name == "Abs");
        Assert.Equal(25, abs.UId);
        Assert.Equal("Real", abs.SrcType);
        Assert.Null(abs.DestType);
    }

    [Fact]
    public void Reduce_AbsFedByRail_EnIsContactConditionAndValueResolves()
    {
        var network = LoadFixture("AbsFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Absolute value", compileUnitUId: "62");

        var abs = Assert.Single(reduced.Network.AbsStatements);
        var enCondition = Assert.IsType<EnSource.Condition>(abs.En);
        Assert.Equal("GateBit", Assert.IsType<Expr.TagRef>(enCondition.Value).Path);
        Assert.Equal("SignedValue", Assert.IsType<Expr.TagRef>(abs.In).Path);
        Assert.Equal("AbsValue", abs.DestTag);
    }

    [Fact]
    public void Reduce_AbsFedByRail_SidecarRecordsConditionEnSourceAndSrcType()
    {
        var network = LoadFixture("AbsFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Absolute value", compileUnitUId: "62");

        var sidecar = Assert.Single(reduced.Sidecar.AbsStatements);
        var enSidecar = Assert.IsType<EnSourceSidecar.ConditionSidecar>(sidecar.En);
        Assert.Equal(26, enSidecar.RailWireUId);
        Assert.Single(enSidecar.Steps);
        Assert.Equal("Real", sidecar.SrcType);
    }

    [Fact]
    public void RoundTrip_AbsFedByRail_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("AbsFedByRail.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Absolute value", compileUnitUId: "62");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var abs = Assert.Single(reparsed.Parts, p => p.Name == "Abs");
        Assert.Equal("Real", abs.SrcType);
        Assert.Null(abs.DestType);

        var enWire = Assert.Single(reparsed.Wires, w => w.UId == 28);
        Assert.Contains(enWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 24 && e.PortName == "out");
        Assert.Contains(enWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 25 && e.PortName == "en");
    }

    [Fact]
    public void SerializeNetworkOnly_AbsFedByRail_ProducesReadableAbsSyntax()
    {
        var network = LoadFixture("AbsFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Absolute value", compileUnitUId: "62");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Contains("  ABS(EN := GateBit, IN := SignedValue) => AbsValue\n", text);
    }

    [Fact]
    public void FullBlock_AbsFedByRail_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("AbsFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Absolute value", compileUnitUId: "62");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_AbsMissingSrcType_ThrowsSimaticMlFormatException()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Abs" UId="1" DisabledENO="true" />
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
    public void Parse_AbsMissingDisabledEno_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Abs" UId="1">
                  <TemplateValue Name="SrcType" Type="Type">Real</TemplateValue>
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
