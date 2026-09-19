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
    // The coil's tag, ASSEMBLED FROM ITS COMPONENTS rather than written as one dotted literal, and
    // the reason is not style. SimaticML stores a tag path componentised - WithTonAndQReadBack.xml
    // holds <Component Name="Control"/> and <Component Name="GeneralEnable"/>, never the joined
    // form - while the assertions below need the joined form, because that is what the reducer
    // produces. The publication de-identification pass matches DOTTED tag paths, so a literal here
    // is rewritten while the fixture's components are structurally out of its reach. The two then
    // disagree and these tests fail for a reason that has nothing to do with TON handling.
    private const string CoilOwner = "Control";
    private const string CoilMember = "GeneralEnable";
    private static readonly string CoilTag = CoilOwner + "." + CoilMember;

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
        Assert.Equal("1.0", ton.Version);
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
        Assert.Equal("1.0", tonPart.Version);
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
        Assert.Equal(CoilTag, assignment.CoilTag);
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
            "  COIL " + CoilTag + " := GeneralEnableDelay.Q\n",
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

    // Access Scope="LocalConstant" — S1 item 21, 2026-07-12. Confirmed real, 4 independent
    // instances (`FB MotorVSDSystem`: `MinSpd` ×2; `FB AirStar`: `PulseTimerMS` ×2 — genericized here as
    // a TON's own PT, matching AirStar's own structural position): a bare `<Constant Name="X" />`
    // reference by name to the block's own declared Interface `Constant` member (S1 item 20) — no
    // `<Symbol>` wrapper, no literal value at the reference site at all, genuinely different from
    // both `TypedConstant`/`LiteralConstant` (which carry a value inline). Modeled as an
    // `AccessNode` with a single-element `ComponentPath`, reusing `DottedPath`/`FromDottedPath`
    // unchanged — the IR's own tag-ref text (`PulseTimerMS`) reads identically to the member's own
    // declared name in that block's own `INTERFACE`/`CONSTANT` section.
    [Fact]
    public void Parse_WithTonPtFedByLocalConstant_ProducesAccessNodeWithLocalConstantScope()
    {
        var network = LoadFixture("WithTonPtFedByLocalConstant.xml");

        var access = Assert.Single(network.AccessNodes, a => a.Scope == "LocalConstant");
        Assert.Equal(new[] { "PulseTimerMS" }, access.ComponentPath);
        Assert.Equal("PulseTimerMS", access.DottedPath);
    }

    [Fact]
    public void Reduce_WithTonPtFedByLocalConstant_PtResolvesToTagRef()
    {
        var network = LoadFixture("WithTonPtFedByLocalConstant.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Pulse reset timer", compileUnitUId: "3");

        var timer = Assert.Single(reduced.Network.Timers);
        Assert.Equal("PulseTimerMS", Assert.IsType<Expr.TagRef>(timer.Pt).Path);

        var timerSidecar = Assert.Single(reduced.Sidecar.Timers);
        var presetTag = Assert.IsType<OperandSidecar.TagOperand>(timerSidecar.Preset);
        Assert.Equal(22, presetTag.AccessUId);
    }

    [Fact]
    public void RoundTrip_WithTonPtFedByLocalConstant_RebuildsBareConstantElementNoSymbol()
    {
        var original = LoadFixture("WithTonPtFedByLocalConstant.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Pulse reset timer", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var access = Assert.Single(reparsed.AccessNodes, a => a.Scope == "LocalConstant");
        Assert.Equal(new[] { "PulseTimerMS" }, access.ComponentPath);

        // The regenerated XML must use the real <Constant Name="..." /> shape, not a <Symbol>
        // wrapper — checked directly on the written XElement, not just the reparsed model, since
        // the model alone can't distinguish "wrote it right" from "wrote something else that
        // happens to reparse the same way."
        var accessElement = xml.Element(FlgNetParser.Ns + "Parts")!.Elements(FlgNetParser.Ns + "Access")
            .Single(e => (string?)e.Attribute("Scope") == "LocalConstant");
        Assert.NotNull(accessElement.Element(FlgNetParser.Ns + "Constant"));
        Assert.Null(accessElement.Element(FlgNetParser.Ns + "Symbol"));
        Assert.Equal("PulseTimerMS", (string?)accessElement.Element(FlgNetParser.Ns + "Constant")!.Attribute("Name"));
    }

    [Fact]
    public void SerializeNetworkOnly_WithTonPtFedByLocalConstant_ProducesPlainTagRefText()
    {
        var network = LoadFixture("WithTonPtFedByLocalConstant.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Pulse reset timer", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 1 \"Pulse reset timer\"\n  TON(PulseResetTimer, IN := ResetPulse, PT := PulseTimerMS)\n",
            text);
    }

    [Fact]
    public void Parse_LocalConstantWithUnexpectedContent_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LocalConstant" UId="1">
                  <Constant Name="Bad">
                    <ConstantValue>1</ConstantValue>
                  </Constant>
                </Access>
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(element));
        Assert.Contains("extra content", ex.Message);
    }

    // TOF (off-delay timer) — S1 item 23, 2026-07-12. Confirmed real against `FB AirStar` (found
    // live-verifying S1 item 22's own Ne fix): structurally identical to TON — same
    // Version/Instance/time_type shape, same IN/PT/ET ports, no reset port (unlike TONR), no
    // EN/ENO. The only difference from TON is semantic (off-delay vs on-delay timing), which this
    // converter doesn't compute — modeled as a third TimerKind variant with zero new fields.
    [Fact]
    public void Parse_WithTof_ProducesTofPartWithGlobalInstance()
    {
        var network = LoadFixture("WithTof.xml");

        var tof = Assert.Single(network.Parts, p => p.Name == "TOF");
        Assert.Equal("1.0", tof.Version);
        Assert.Equal("Time", tof.TimeType);
        Assert.NotNull(tof.Instance);
        Assert.Equal("GlobalVariable", tof.Instance!.Scope);
        Assert.Equal(new[] { "RunHoldDelay" }, tof.Instance.ComponentPath);
    }

    [Fact]
    public void Reduce_WithTof_ProducesTimerBindingWithTofKind()
    {
        var network = LoadFixture("WithTof.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Run hold delay", compileUnitUId: "3");

        var timer = Assert.Single(reduced.Network.Timers);
        Assert.Equal(TimerKind.Tof, timer.Kind);
        Assert.Equal("RunHoldDelay", timer.InstancePath);
        Assert.Equal("Sensor1.Ok", Assert.IsType<Expr.TagRef>(timer.In).Path);
        Assert.Equal("Settings.RunDelay", Assert.IsType<Expr.TagRef>(timer.Pt).Path);
        Assert.Null(timer.Reset);

        var timerSidecar = Assert.Single(reduced.Sidecar.Timers);
        Assert.Equal(TimerKind.Tof, timerSidecar.Kind);
        Assert.Null(timerSidecar.Reset);
    }

    [Fact]
    public void RoundTrip_WithTof_RebuildsTofPartNameNoResetWire()
    {
        var original = LoadFixture("WithTof.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Run hold delay", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var tofPart = Assert.Single(reparsed.Parts, p => p.Name == "TOF");
        Assert.Equal(32, tofPart.UId);
        Assert.Equal("1.0", tofPart.Version);

        Assert.DoesNotContain(reparsed.Wires, w => w.Endpoints.Any(e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "R"));
    }

    [Fact]
    public void SerializeNetworkOnly_WithTof_ProducesReadableTofStatement()
    {
        var network = LoadFixture("WithTof.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Run hold delay", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 1 \"Run hold delay\"\n  TOF(RunHoldDelay, IN := Sensor1.Ok, PT := Settings.RunDelay)\n",
            text);
    }

    [Fact]
    public void FullBlock_Tof_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("WithTof.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Run hold delay", compileUnitUId: "3");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        Assert.Contains("    kind = tof\n", text);

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }
}
