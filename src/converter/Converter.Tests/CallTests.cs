using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FB/FC block calls (`&lt;Call&gt;`/`&lt;CallInfo&gt;`) — S1 item 14, 2026-07-12. Picked up
/// specifically to close the gap left by `Not` (S1 item 13): every one of `FC PlantAutoControl`'s 20
/// real networks pairs a `Not` with a `&lt;Call&gt;`, so `Not` alone could never reach the true
/// TIA `import -&gt; compile -&gt; re-export -&gt; Normalizer` gold standard. Grounded against a fresh
/// `PlantAutoControl` export before any code (scratch temp, deleted after use): a systematic sweep
/// found 19 of 20 real `&lt;Call&gt;` instances are entirely bare (`Instance` + a directly
/// rail-fed `en`, zero `&lt;Parameter&gt;` children at all — not present-but-empty, simply
/// absent) and exactly one (`TomraControlSystem`, `CompileUnit "35"`) has 10 wired parameters (8
/// Section="Input", 2 Section="Output", all Type="Word"). Both real shapes are covered here:
/// `CallBareFedByRail.xml` (the common case) and `CallWithParametersFedByRail.xml` (genericized
/// down from the real `TomraControlSystem` shape — 2 inputs + 1 output instead of 8+2, same structure).
///
/// Genuinely unlike every other Part built so far: `&lt;Call&gt;` isn't a `&lt;Part Name="Call"&gt;`
/// in the source at all — it's its own sibling element under `&lt;Parts&gt;`
/// (`&lt;Call UId="N"&gt;&lt;CallInfo Name="..." BlockType="FB"&gt;...&lt;/CallInfo&gt;&lt;/Call&gt;`),
/// adapted into an ordinary `PartNode(Name="Call")` by `FlgNetParser` so the rest of the pipeline
/// never needs a parallel type. Reduces as its own top-level production (like TON/Move/WAND, not
/// like `Not`, which is a chain-position discovered incidentally): `en` is reduced via the same
/// `TraceChain` fan-out mechanism as Move/WAND's own `en` (confirmed real: all 20 real instances
/// are directly rail-fed, reducing to the "wired directly to rail" TRUE sentinel). Instance
/// reuses the exact same `AccessNode` shape as TON's own `&lt;Instance&gt;` (confirmed identical).
/// Arguments are sparse and ordered exactly as the source's own `&lt;Parameter&gt;` children
/// appear — an Input resolves via the same tag-or-literal resolver used everywhere else; an
/// Output is a bare destination tag, same wire shape as Move's own `out1`, just named per the
/// source's own Parameter Name instead of a fixed port.
/// </summary>
public class CallTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_CallBareFedByRail_ProducesCallPartWithInstanceAndNoParameters()
    {
        var network = LoadFixture("CallBareFedByRail.xml");

        var call = Assert.Single(network.Parts, p => p.Name == "Call");
        Assert.Equal(25, call.UId);
        Assert.Equal("PumpControl", call.BlockName);
        Assert.Equal("FB", call.BlockType);
        Assert.NotNull(call.Instance);
        Assert.Equal("GlobalVariable", call.Instance!.Scope);
        Assert.Equal(new[] { "Pump1_DB" }, call.Instance.ComponentPath);
        Assert.Empty(call.CallParameters!);
    }

    [Fact]
    public void Reduce_CallBareFedByRail_ProducesCallStatementWithTrueEnAndNoArguments()
    {
        var network = LoadFixture("CallBareFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Pump control", compileUnitUId: "58");

        Assert.Single(reduced.Network.Assignments);
        var call = Assert.Single(reduced.Network.Calls);

        Assert.Equal("PumpControl", call.BlockName);
        Assert.Equal("Pump1_DB", call.InstancePath);
        var enCondition = Assert.IsType<Expr.And>(call.En);
        Assert.Empty(enCondition.Operands);
        Assert.Empty(call.Arguments);
    }

    [Fact]
    public void Reduce_CallBareFedByRail_SidecarRecordsInstanceAndSharedRail()
    {
        var network = LoadFixture("CallBareFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Pump control", compileUnitUId: "58");

        var sidecar = Assert.Single(reduced.Sidecar.Calls);
        Assert.Equal(25, sidecar.CallPartUId);
        Assert.Equal("PumpControl", sidecar.BlockName);
        Assert.Equal("FB", sidecar.BlockType);
        Assert.Empty(sidecar.Steps);
        // The Call's own "en" shares the same rail wire as the sibling Contact's chain — the same
        // fan-out/shared-rail mechanism proven for every other production, confirmed real.
        Assert.Equal(30, sidecar.RailWireUId);
        Assert.Equal(26, sidecar.InstanceUId);
        Assert.Equal("GlobalVariable", sidecar.InstanceScope);
        Assert.Equal(new[] { "Pump1_DB" }, sidecar.InstanceComponentPath);
        Assert.Empty(sidecar.Arguments);
    }

    [Fact]
    public void RoundTrip_CallBareFedByRail_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("CallBareFedByRail.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Pump control", compileUnitUId: "58");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var call = Assert.Single(reparsed.Parts, p => p.Name == "Call");
        Assert.Equal(25, call.UId);
        Assert.Equal("PumpControl", call.BlockName);
        Assert.Equal("FB", call.BlockType);

        var railWire = Assert.Single(reparsed.Wires, w => w.UId == 30);
        Assert.Equal(3, railWire.Endpoints.Count);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.Powerrail);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 23 && e.PortName == "in");
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 25 && e.PortName == "en");
    }

    [Fact]
    public void SerializeNetworkOnly_CallBareFedByRail_ProducesReadableCallSyntax()
    {
        var network = LoadFixture("CallBareFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Pump control", compileUnitUId: "58");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Contains("  CALL PumpControl(Pump1_DB, EN := TRUE)\n", text);
    }

    [Fact]
    public void FullBlock_CallBare_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("CallBareFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Pump control", compileUnitUId: "58");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_CallWithParametersFedByRail_ProducesCallPartWithParameters()
    {
        var network = LoadFixture("CallWithParametersFedByRail.xml");

        var call = Assert.Single(network.Parts, p => p.Name == "Call");
        Assert.Equal(3, call.CallParameters!.Count);
        Assert.Equal(new CallParameterNode("in0", "Input", "Word"), call.CallParameters[0]);
        Assert.Equal(new CallParameterNode("in1", "Input", "Word"), call.CallParameters[1]);
        Assert.Equal(new CallParameterNode("out0", "Output", "Word"), call.CallParameters[2]);
    }

    [Fact]
    public void Reduce_CallWithParametersFedByRail_ProducesOrderedArguments()
    {
        var network = LoadFixture("CallWithParametersFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Pump control with data", compileUnitUId: "35");

        var call = Assert.Single(reduced.Network.Calls);
        Assert.Equal(3, call.Arguments.Count);

        var in0 = Assert.IsType<CallArgument.InputArg>(call.Arguments[0]);
        Assert.Equal("in0", in0.ParamName);
        Assert.Equal("SensorWordA", Assert.IsType<Expr.TagRef>(in0.Value).Path);

        var in1 = Assert.IsType<CallArgument.InputArg>(call.Arguments[1]);
        Assert.Equal("in1", in1.ParamName);
        Assert.Equal("SensorWordB", Assert.IsType<Expr.TagRef>(in1.Value).Path);

        var out0 = Assert.IsType<CallArgument.OutputArg>(call.Arguments[2]);
        Assert.Equal("out0", out0.ParamName);
        Assert.Equal("ResultWord", out0.DestTag);
    }

    [Fact]
    public void Reduce_CallWithParametersFedByRail_SidecarRecordsArgumentTypesAndWires()
    {
        var network = LoadFixture("CallWithParametersFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Pump control with data", compileUnitUId: "35");

        var sidecar = Assert.Single(reduced.Sidecar.Calls);
        Assert.Equal(3, sidecar.Arguments.Count);

        var in0 = Assert.IsType<CallArgumentSidecar.InputArgSidecar>(sidecar.Arguments[0]);
        Assert.Equal("in0", in0.ParamName);
        Assert.Equal("Word", in0.Type);
        var in0Tag = Assert.IsType<OperandSidecar.TagOperand>(in0.Value);
        Assert.Equal(21, in0Tag.AccessUId);

        var out0 = Assert.IsType<CallArgumentSidecar.OutputArgSidecar>(sidecar.Arguments[2]);
        Assert.Equal("out0", out0.ParamName);
        Assert.Equal("Word", out0.Type);
        Assert.Equal(23, out0.DestAccessUId);
        Assert.Equal(33, out0.DestWireUId);
    }

    [Fact]
    public void RoundTrip_CallWithParametersFedByRail_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("CallWithParametersFedByRail.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Pump control with data", compileUnitUId: "35");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var call = Assert.Single(reparsed.Parts, p => p.Name == "Call");
        Assert.Equal(3, call.CallParameters!.Count);

        var outputWire = Assert.Single(reparsed.Wires, w => w.UId == 33);
        Assert.Contains(outputWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 24 && e.PortName == "out0");
        Assert.Contains(outputWire.Endpoints, e => e.Kind == EndpointKind.IdentCon && e.UId == 23);
    }

    [Fact]
    public void SerializeNetworkOnly_CallWithParametersFedByRail_ProducesReadableCallSyntax()
    {
        var network = LoadFixture("CallWithParametersFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Pump control with data", compileUnitUId: "35");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Contains(
            "  CALL PumpControl(Pump1_DB, EN := TRUE, in0 := SensorWordA, in1 := SensorWordB, out0 => ResultWord)\n",
            text);
    }

    [Fact]
    public void FullBlock_CallWithParameters_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("CallWithParametersFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Pump control with data", compileUnitUId: "35");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_CallMissingCallInfo_ThrowsSimaticMlFormatException()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Call UId="1" />
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<SimaticMlFormatException>(() => FlgNetParser.Parse(element));
        Assert.Contains("CallInfo", ex.Message);
    }

    [Fact]
    public void Parse_CallParameterUnsupportedSection_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Call UId="1">
                  <CallInfo Name="PumpControl" BlockType="FB">
                    <Instance Scope="GlobalVariable" UId="2">
                      <Component Name="Pump1_DB" />
                    </Instance>
                    <Parameter Name="io0" Section="InOut" Type="Word" />
                  </CallInfo>
                </Call>
                <Part Name="Coil" UId="3" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(element));
        Assert.Contains("Section", ex.Message);
    }
}
