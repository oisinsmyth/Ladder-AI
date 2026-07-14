using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `Calc` — free-expression box instruction (Phase 2 Tier 3, 2026-07-14). Grounded against a real
/// export sweep of `JOB9002`'s full block inventory (`FB VSDSim`'s own numeric-simulation ladder, 2
/// identical instances): `&lt;Part Name="Calc" UId="N" DisabledENO="true"&gt;&lt;Equation&gt;
/// IN1*(IN2/IN3)&lt;/Equation&gt;&lt;TemplateValue Name="Card" Type="Cardinality"&gt;3
/// &lt;/TemplateValue&gt;&lt;TemplateValue Name="SrcType" Type="Type"&gt;Real&lt;/TemplateValue&gt;
/// &lt;/Part&gt;` — Cardinality-driven inputs like Mul/Add (lowercase `in1`/`in2`/`in3`/`out`), but
/// the free-text Equation defines how they combine rather than the Part Name implying it. Carried
/// verbatim, never parsed as an expression — this converter has no Siemens-CALC-syntax parser.
/// </summary>
public class CalcTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_CalcFedByRail_ProducesCalcPart()
    {
        var network = LoadFixture("CalcFedByRail.xml");

        var calc = Assert.Single(network.Parts, p => p.Name == "Calc");
        Assert.Equal(25, calc.UId);
        Assert.Equal(3, calc.Cardinality);
        Assert.Equal("Real", calc.SrcType);
        Assert.Equal("IN1*(IN2/IN3)", calc.Equation);
    }

    [Fact]
    public void Reduce_CalcFedByRail_EnIsRailAndInputsResolve()
    {
        var network = LoadFixture("CalcFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Scale speed", compileUnitUId: "62");

        var calc = Assert.Single(reduced.Network.Calcs);
        var en = Assert.IsType<EnSource.Condition>(calc.En);
        Assert.Empty(Assert.IsType<Expr.And>(en.Value).Operands);
        Assert.Equal(3, calc.Inputs.Count);
        Assert.Equal("Speed", Assert.IsType<Expr.TagRef>(calc.Inputs[0]).Path);
        Assert.Equal("Ratio", Assert.IsType<Expr.TagRef>(calc.Inputs[1]).Path);
        Assert.Equal("Divisor", Assert.IsType<Expr.TagRef>(calc.Inputs[2]).Path);
        Assert.Equal("IN1*(IN2/IN3)", calc.Equation);
        Assert.Equal("ScaledSpeed", calc.DestTag);
    }

    [Fact]
    public void Reduce_CalcFedByRail_SidecarRecordsEquationAndSrcType()
    {
        var network = LoadFixture("CalcFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Scale speed", compileUnitUId: "62");

        var sidecar = Assert.Single(reduced.Sidecar.Calcs);
        Assert.Equal("IN1*(IN2/IN3)", sidecar.Equation);
        Assert.Equal("Real", sidecar.SrcType);
        Assert.Equal(3, sidecar.Inputs.Count);
    }

    [Fact]
    public void RoundTrip_CalcFedByRail_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("CalcFedByRail.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Scale speed", compileUnitUId: "62");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var calc = Assert.Single(reparsed.Parts, p => p.Name == "Calc");
        Assert.Equal(3, calc.Cardinality);
        Assert.Equal("Real", calc.SrcType);
        Assert.Equal("IN1*(IN2/IN3)", calc.Equation);

        var outWire = Assert.Single(reparsed.Wires, w => w.UId == 30);
        Assert.Contains(outWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 25 && e.PortName == "out");
    }

    [Fact]
    public void SerializeNetworkOnly_CalcFedByRail_ProducesReadableCalcSyntaxWithQuotedEquation()
    {
        var network = LoadFixture("CalcFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Scale speed", compileUnitUId: "62");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Contains(
            "  CALC(EN := TRUE, IN1 := Speed, IN2 := Ratio, IN3 := Divisor) => ScaledSpeed \"IN1*(IN2/IN3)\"\n", text);
    }

    [Fact]
    public void FullBlock_CalcFedByRail_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("CalcFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Scale speed", compileUnitUId: "62");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_CalcMissingEquation_ThrowsSimaticMlFormatException()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Calc" UId="1" DisabledENO="true">
                  <TemplateValue Name="Card" Type="Cardinality">3</TemplateValue>
                  <TemplateValue Name="SrcType" Type="Type">Real</TemplateValue>
                </Part>
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<SimaticMlFormatException>(() => FlgNetParser.Parse(element));
        Assert.Contains("Equation", ex.Message);
    }

    [Fact]
    public void Parse_CalcMissingDisabledEno_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Calc" UId="1">
                  <Equation>IN1*(IN2/IN3)</Equation>
                  <TemplateValue Name="Card" Type="Cardinality">3</TemplateValue>
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
