using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// TON (`Part Name="TON"`) support — S1 item 8, 2026-07-11. Grounded against real exports:
/// `FB MotorDOL` (multi-instance, `Instance Scope="LocalVariable"`, PT fed by a `TypedConstant`
/// literal), `FC ControlDelays` (standalone instance, `Instance Scope="GlobalVariable"`, a
/// single Component naming its own instance DB directly, PT fed by a `LocalVariable` tag, Q read
/// back via an ordinary Access elsewhere), and `FC TimerSample` (purpose-built by the project
/// owner in the reference project: `Instance Scope="GlobalVariable"` with a *two*-component path
/// — `DB_Timers`, `SampleTimerN` — and Q wired *directly* into a plain Coil, closing the one gap
/// left after `MotorDOL`'s only example of that shape turned out to feed an out-of-scope
/// `RCoil`). Both instance scopes, and both single- and two-component `GlobalVariable` paths, are
/// modeled via the existing <see cref="AccessNode"/> rather than a new type — see Model.cs's
/// PartNode doc comment.
///
/// A TON's Q consumed directly (no ordinary Access) is <see cref="ChainStepSidecar.TimerOutputStep"/>
/// — always chain-terminal like an OR-merge, except it never touches Powerrail, so the owning
/// assignment/timer's `RailWireUId` comes back null.
/// </summary>
public class TonTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_WithTon_ProducesTonPartWithGlobalInstance()
    {
        var network = LoadFixture("WithTon.xml");

        var ton = Assert.Single(network.Parts, p => p.Name == "TON");
        Assert.Equal("1.0", ton.TonVersion);
        Assert.Equal("Time", ton.TimeType);
        Assert.NotNull(ton.Instance);
        Assert.Equal("GlobalVariable", ton.Instance!.Scope);
        Assert.Equal(new[] { "RunEnableDelay" }, ton.Instance.ComponentPath);
    }

    [Fact]
    public void Reduce_WithTon_GlobalInstanceTagPreset_ProducesTimerBinding()
    {
        var network = LoadFixture("WithTon.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Run enable delay", compileUnitUId: "3");

        var timer = Assert.Single(reduced.Network.Timers);
        Assert.Equal("RunEnableDelay", timer.InstancePath);
        Assert.Equal("Sensor1.Ok", Assert.IsType<Expr.TagRef>(timer.In).Path);
        Assert.Equal("Settings.RunDelay", Assert.IsType<Expr.TagRef>(timer.Pt).Path);
        Assert.Empty(reduced.Network.Assignments);
    }

    [Fact]
    public void Reduce_WithTon_SidecarRecordsInstanceAndEt()
    {
        var network = LoadFixture("WithTon.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Run enable delay", compileUnitUId: "3");

        var timerSidecar = Assert.Single(reduced.Sidecar.Timers);
        Assert.Equal(32, timerSidecar.TonPartUId);
        Assert.Equal("1.0", timerSidecar.Version);
        Assert.Equal("Time", timerSidecar.TimeType);
        Assert.Equal(33, timerSidecar.InstanceUId);
        Assert.Equal("GlobalVariable", timerSidecar.InstanceScope);
        Assert.Equal(new[] { "RunEnableDelay" }, timerSidecar.InstanceComponentPath);
        Assert.Equal(41, timerSidecar.RailWireUId);
        var tagPreset = Assert.IsType<OperandSidecar.TagOperand>(timerSidecar.Preset);
        Assert.Equal(44, tagPreset.WireUId);
        Assert.NotNull(timerSidecar.Et);
        Assert.Equal(45, timerSidecar.Et!.WireUId);
        Assert.Equal(46, timerSidecar.Et.OpenConUId);
    }

    [Fact]
    public void Reduce_WithTonLocalInstance_LiteralPreset_ProducesTimerBinding()
    {
        var network = LoadFixture("WithTonLocalInstance.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 2, title: "Local instance delay", compileUnitUId: "4");

        var timer = Assert.Single(reduced.Network.Timers);
        Assert.Equal("GeneralDelayTimer1", timer.InstancePath);
        Assert.Equal("IO.Run", Assert.IsType<Expr.TagRef>(timer.In).Path);
        Assert.Equal("T#100MS", Assert.IsType<Expr.Literal>(timer.Pt).Value);

        var timerSidecar = Assert.Single(reduced.Sidecar.Timers);
        Assert.Equal("LocalVariable", timerSidecar.InstanceScope);
        var literalPreset = Assert.IsType<OperandSidecar.LiteralOperand>(timerSidecar.Preset);
        Assert.Equal(22, literalPreset.ConstantUId);
    }

    [Fact]
    public void RoundTrip_WithTon_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("WithTon.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Run enable delay", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var tonPart = Assert.Single(reparsed.Parts, p => p.Name == "TON");
        Assert.Equal(32, tonPart.UId);
        Assert.Equal("1.0", tonPart.TonVersion);
        Assert.Equal("Time", tonPart.TimeType);
        Assert.Equal("GlobalVariable", tonPart.Instance!.Scope);
        Assert.Equal(33, tonPart.Instance.UId);
        Assert.Equal(new[] { "RunEnableDelay" }, tonPart.Instance.ComponentPath);

        var etWire = Assert.Single(reparsed.Wires, w => w.UId == 45);
        Assert.Contains(etWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "ET");
        Assert.Contains(etWire.Endpoints, e => e.Kind == EndpointKind.OpenCon && e.UId == 46);

        var ptWire = Assert.Single(reparsed.Wires, w => w.UId == 44);
        Assert.Contains(ptWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "PT");
    }

    [Fact]
    public void RoundTrip_WithTonLocalInstance_RebuildsLiteralPresetExactly()
    {
        var original = LoadFixture("WithTonLocalInstance.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 2, title: "Local instance delay", compileUnitUId: "4");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var constant = Assert.Single(reparsed.Constants);
        Assert.Equal(22, constant.UId);
        Assert.Equal("T#100MS", constant.Value);

        var tonPart = Assert.Single(reparsed.Parts, p => p.Name == "TON");
        Assert.Equal("LocalVariable", tonPart.Instance!.Scope);
    }

    // Grounded on FC ControlDelays' actual shape: a shared rail feeds both the TON's own IN
    // chain and a second, independent Contact that reads the TON's Q back via an ordinary
    // Access — the only real, live-provable way to consume a TON's output this phase.
    [Fact]
    public void Reduce_WithTonAndQReadBack_TimerAndCoilBothReduce()
    {
        var network = LoadFixture("WithTonAndQReadBack.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 3, title: "General enable delay", compileUnitUId: "5");

        var timer = Assert.Single(reduced.Network.Timers);
        Assert.Equal("GeneralEnableDelay", timer.InstancePath);
        Assert.Equal("T#100MS", Assert.IsType<Expr.Literal>(timer.Pt).Value);

        var assignment = Assert.Single(reduced.Network.Assignments);
        Assert.Equal("PlantControl.GeneralEnable", assignment.CoilTag);
        Assert.Equal("GeneralEnableDelay.Q", Assert.IsType<Expr.TagRef>(assignment.Condition).Path);
    }

    [Fact]
    public void RoundTrip_WithTonAndQReadBack_RebuildsSharedRailAndBothChains()
    {
        var original = LoadFixture("WithTonAndQReadBack.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 3, title: "General enable delay", compileUnitUId: "5");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var railWire = Assert.Single(reparsed.Wires, w => w.UId == 41);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.Powerrail);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 31 && e.PortName == "in");
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 34 && e.PortName == "in");

        Assert.Single(reparsed.Constants);
        Assert.Equal(3, reparsed.AccessNodes.Count);
    }

    [Fact]
    public void SerializeNetworkOnly_WithTon_ProducesReadableTonStatement()
    {
        var network = LoadFixture("WithTon.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Run enable delay", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 1 \"Run enable delay\"\n  TON(RunEnableDelay, IN := Sensor1.Ok, PT := Settings.RunDelay)\n",
            text);
    }

    [Fact]
    public void SerializeNetworkOnly_WithTonAndQReadBack_ProducesTonThenCoil()
    {
        var network = LoadFixture("WithTonAndQReadBack.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 3, title: "General enable delay", compileUnitUId: "5");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 3 \"General enable delay\"\n" +
            "  TON(GeneralEnableDelay, IN := StartCmd, PT := T#100MS)\n" +
            "  COIL PlantControl.GeneralEnable := GeneralEnableDelay.Q\n",
            text);
    }

    [Fact]
    public void FullBlock_Ton_ParseThenSerialize_IsByteIdentical()
    {
        var globalNetwork = LoadFixture("WithTon.xml");
        var globalReduced = GraphReducer.Reduce(globalNetwork, networkNumber: 1, title: "Run enable delay", compileUnitUId: "3");
        var localNetwork = LoadFixture("WithTonLocalInstance.xml");
        var localReduced = GraphReducer.Reduce(localNetwork, networkNumber: 2, title: "Local instance delay", compileUnitUId: "4");
        var qReadBackNetwork = LoadFixture("WithTonAndQReadBack.xml");
        var qReadBackReduced = GraphReducer.Reduce(qReadBackNetwork, networkNumber: 3, title: "General enable delay", compileUnitUId: "5");

        var block = new IrBlock(
            "0", "FC", "TestBlock", 1, "LAD", "A test block",
            new[] { globalReduced.Network, localReduced.Network, qReadBackReduced.Network });
        var text = IrSerializer.SerializeBlock(
            block, new[] { globalReduced.Sidecar, localReduced.Sidecar, qReadBackReduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    // A TON's Q wired *directly* into a downstream Coil (no ordinary Access in between) —
    // purpose-built by the project owner in the reference project (FC TimerSample) specifically
    // to close this gap out, after the only prior real example (FB MotorDOL) turned out to feed
    // an out-of-scope RCoil instead. TimerOutputStep is always a chain-terminal leaf, like
    // OrStep, except it never touches Powerrail — RailWireUId comes back null.
    [Fact]
    public void Reduce_TonQFeedsCoilDirectly_ProducesTimerOutputStepWithNullRail()
    {
        var network = LoadFixture("TonQFeedsCoilDirectly.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Direct Q wiring", compileUnitUId: "3");

        var assignment = Assert.Single(reduced.Network.Assignments);
        Assert.Equal("Output1", assignment.CoilTag);
        Assert.Equal("GeneralDelayTimer1.Q", Assert.IsType<Expr.TagRef>(assignment.Condition).Path);

        var assignmentSidecar = Assert.Single(reduced.Sidecar.Assignments);
        Assert.Null(assignmentSidecar.RailWireUId);
        var timerOutputStep = Assert.IsType<ChainStepSidecar.TimerOutputStep>(Assert.Single(assignmentSidecar.Steps));
        Assert.Equal(32, timerOutputStep.TonPartUId);
        Assert.Equal("Q", timerOutputStep.Port);
        Assert.Equal(45, timerOutputStep.OutgoingWireUId);

        Assert.Single(reduced.Network.Timers);
    }

    [Fact]
    public void RoundTrip_TonQFeedsCoilDirectly_RebuildsDirectWireNoPowerrail()
    {
        var original = LoadFixture("TonQFeedsCoilDirectly.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Direct Q wiring", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var qWire = Assert.Single(reparsed.Wires, w => w.UId == 45);
        Assert.Equal(2, qWire.Endpoints.Count);
        Assert.Contains(qWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "Q");
        Assert.Contains(qWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 34 && e.PortName == "in");
        Assert.DoesNotContain(reparsed.Wires, w => w.Endpoints.Any(e => e.Kind == EndpointKind.Powerrail) && w.UId == 45);
    }

    [Fact]
    public void SerializeNetworkOnly_TonQFeedsCoilDirectly_ProducesTonThenCoilReferencingIt()
    {
        var network = LoadFixture("TonQFeedsCoilDirectly.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Direct Q wiring", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 1 \"Direct Q wiring\"\n" +
            "  TON(GeneralDelayTimer1, IN := IO.Run, PT := RunDelayPreset)\n" +
            "  COIL Output1 := GeneralDelayTimer1.Q\n",
            text);
    }

    [Fact]
    public void FullBlock_TonQFeedsCoilDirectly_ParseThenSerialize_IsByteIdentical_WithNullRailInSidecar()
    {
        var network = LoadFixture("TonQFeedsCoilDirectly.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Direct Q wiring", compileUnitUId: "3");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        Assert.Contains("    rail = none\n", text);

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_TonUnsupportedInstanceScope_ThrowsUnsupportedConstruct()
    {
        var element = XElement.Load(Path.Combine("Fixtures", "TonUnsupportedInstanceScope.xml"));

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(element));

        Assert.Contains("LiteralConstant", ex.Message);
    }
}
