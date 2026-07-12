using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// TONR (`Part Name="TONR"`, retentive on-delay timer), plus `Lt` (a third comparison operator)
/// and `Add` (arithmetic) — S1 item 19, 2026-07-12. All three real gaps were confirmed grounding
/// `FB MotorDOL`/`FilterUnitSystem` (two independent instances, byte-identical network shape) while
/// scoping `TONR` support: `TONR` alone would not have fully round-tripped either FB, so `Add`/
/// `Lt` were bundled into this item after an explicit project-owner check-in.
///
/// `TONR`'s own shape is identical to `TON`'s (`Version`/`Instance`/`time_type`, no `EN`/`ENO`)
/// plus one genuine new port — `R` (reset), fed directly by a plain tag `IdentCon` in both
/// grounded instances, no chain (same shape as `PT`). Modeled via a new `TimerKind` enum
/// (`Ton`/`Tonr`), mirroring the `CoilKind` (`Assign`/`Set`/`Reset`) precedent exactly.
///
/// `Add`'s own XML shape is identical to `Mul`'s (`DisabledENO="true"`/`Card="2"`/
/// `AutomaticTyped SrcType`) — modeled via a new `MulKind` enum (`Multiply`/`Add`). Its own `en`
/// in the grounded network is fed by a comparison's (`Lt`'s) `out` — an ordinary `TraceChain`
/// `Condition`, confirmed **not** ENO-chained (verified directly against the real wiring before
/// writing any code) — so `ResolveEnSource`'s `Mul`/`Convert` ENO-chain check was deliberately
/// left untouched for `Add`.
///
/// `Lt` needed no new model shape at all — `ChainStepSidecar.CompareStep` already carries
/// `PartName` generically, so it's purely a parser/reducer/writer dictionary extension
/// (`SupportedComparisonPartNames`, `ComparisonOperator`, `OutPortFor`, `TraceChain`'s upstream
/// dispatch).
/// </summary>
public class TonrTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_WithTonr_ProducesTonrPartWithLocalInstance()
    {
        var network = LoadFixture("WithTonr.xml");

        var tonr = Assert.Single(network.Parts, p => p.Name == "TONR");
        Assert.Equal("1.0", tonr.TonVersion);
        Assert.Equal("Time", tonr.TimeType);
        Assert.NotNull(tonr.Instance);
        Assert.Equal("LocalVariable", tonr.Instance!.Scope);
        Assert.Equal(new[] { "RunTimeTimer" }, tonr.Instance.ComponentPath);
    }

    [Fact]
    public void Reduce_WithTonr_ProducesTimerBindingWithResetAndTonrKind()
    {
        var network = LoadFixture("WithTonr.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Run time timer", compileUnitUId: "3");

        var timer = Assert.Single(reduced.Network.Timers);
        Assert.Equal(TimerKind.Tonr, timer.Kind);
        Assert.Equal("RunTimeTimer", timer.InstancePath);
        Assert.Equal("RunEnable", Assert.IsType<Expr.TagRef>(timer.In).Path);
        Assert.Equal("PresetDuration", Assert.IsType<Expr.TagRef>(timer.Pt).Path);
        Assert.NotNull(timer.Reset);
        Assert.Equal("ResetRequest", Assert.IsType<Expr.TagRef>(timer.Reset!).Path);
        Assert.Empty(reduced.Network.Assignments);
    }

    [Fact]
    public void Reduce_WithTonr_SidecarRecordsResetOperandAndKind()
    {
        var network = LoadFixture("WithTonr.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Run time timer", compileUnitUId: "3");

        var timerSidecar = Assert.Single(reduced.Sidecar.Timers);
        Assert.Equal(TimerKind.Tonr, timerSidecar.Kind);
        Assert.NotNull(timerSidecar.Reset);
        var resetTag = Assert.IsType<OperandSidecar.TagOperand>(timerSidecar.Reset!);
        Assert.Equal(44, resetTag.WireUId);
    }

    [Fact]
    public void RoundTrip_WithTonr_RebuildsResetWireAndTonrPartName()
    {
        var original = LoadFixture("WithTonr.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Run time timer", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var tonrPart = Assert.Single(reparsed.Parts, p => p.Name == "TONR");
        Assert.Equal(32, tonrPart.UId);
        Assert.Equal("1.0", tonrPart.TonVersion);

        var resetWire = Assert.Single(reparsed.Wires, w => w.UId == 44);
        Assert.Contains(resetWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "R");

        var etWire = Assert.Single(reparsed.Wires, w => w.UId == 46);
        Assert.Contains(etWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "ET");
    }

    [Fact]
    public void SerializeNetworkOnly_WithTonr_ProducesReadableTonrStatementWithResetArgument()
    {
        var network = LoadFixture("WithTonr.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Run time timer", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 1 \"Run time timer\"\n  TONR(RunTimeTimer, IN := RunEnable, PT := PresetDuration, R := ResetRequest)\n",
            text);
    }

    [Fact]
    public void FullBlock_Tonr_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("WithTonr.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Run time timer", compileUnitUId: "3");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        Assert.Contains("    kind = tonr\n", text);
        Assert.Contains("    reset tag = ", text);

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    // Confirms a plain TON (Kind = Ton, no Reset) still round-trips unchanged after TONR's own
    // fields were added — the "kind = ton" sidecar line is new, but the readable form stays
    // TON(...) with no trailing R argument, exactly as before this item.
    [Fact]
    public void FullBlock_TonAlongsideTonr_BothKindsRoundTripDistinctly()
    {
        var tonNetwork = LoadFixture("WithTon.xml");
        var tonReduced = GraphReducer.Reduce(tonNetwork, networkNumber: 1, title: "Run enable delay", compileUnitUId: "3");
        var tonrNetwork = LoadFixture("WithTonr.xml");
        var tonrReduced = GraphReducer.Reduce(tonrNetwork, networkNumber: 2, title: "Run time timer", compileUnitUId: "4");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { tonReduced.Network, tonrReduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { tonReduced.Sidecar, tonrReduced.Sidecar });

        Assert.Contains("  TON(RunEnableDelay, IN := Sensor1.Ok, PT := Settings.RunDelay)\n", text);
        Assert.Contains("  TONR(RunTimeTimer, IN := RunEnable, PT := PresetDuration, R := ResetRequest)\n", text);
        Assert.Contains("    kind = ton\n", text);
        Assert.Contains("    kind = tonr\n", text);

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
        Assert.Equal(TimerKind.Ton, parsedBlock.Networks[0].Timers[0].Kind);
        Assert.Equal(TimerKind.Tonr, parsedBlock.Networks[1].Timers[0].Kind);
    }

    [Fact]
    public void Parse_LtFeedsCoil_ProducesLtPartWithSrcType()
    {
        var network = LoadFixture("LtFeedsCoil.xml");

        var lt = Assert.Single(network.Parts, p => p.Name == "Lt");
        Assert.Equal("Int", lt.SrcType);
    }

    [Fact]
    public void Reduce_LtFeedsCoil_ProducesLessThanCompareExpr()
    {
        var network = LoadFixture("LtFeedsCoil.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Level check", compileUnitUId: "3");

        var assignment = Assert.Single(reduced.Network.Assignments);
        var and = Assert.IsType<Expr.And>(assignment.Condition);
        Assert.Equal("Enable", Assert.IsType<Expr.TagRef>(and.Operands[0]).Path);
        var compare = Assert.IsType<Expr.Compare>(and.Operands[1]);
        Assert.Equal("<", compare.Operator);
        Assert.Equal("Level", Assert.IsType<Expr.TagRef>(compare.Left).Path);
        Assert.Equal("10", Assert.IsType<Expr.Literal>(compare.Right).Value);
    }

    [Fact]
    public void RoundTrip_LtFeedsCoil_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("LtFeedsCoil.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Level check", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var ltPart = Assert.Single(reparsed.Parts, p => p.Name == "Lt");
        Assert.Equal("Int", ltPart.SrcType);
    }

    [Fact]
    public void SerializeNetworkOnly_LtFeedsCoil_ProducesLessThanOperator()
    {
        var network = LoadFixture("LtFeedsCoil.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Level check", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal("NETWORK 1 \"Level check\"\n  COIL Output2 := Enable AND Level < 10\n", text);
    }

    [Fact]
    public void Parse_AddFedByComparison_ProducesAddPartWithAutomaticTyped()
    {
        var network = LoadFixture("AddFedByComparison.xml");

        var add = Assert.Single(network.Parts, p => p.Name == "Add");
        Assert.Equal(2, add.Cardinality);
        Assert.True(add.AutomaticSrcType);
    }

    // The real, confirmed finding this item's own scope check hinged on: Add's `en` here is fed
    // by Lt's own `out` — an ordinary boolean condition, not the Mul/Convert ENO-chain shape.
    [Fact]
    public void Reduce_AddFedByComparison_EnIsOrdinaryConditionNotEnoChained()
    {
        var network = LoadFixture("AddFedByComparison.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Conditional add", compileUnitUId: "3");

        var add = Assert.Single(reduced.Network.Muls);
        Assert.Equal(MulKind.Add, add.Kind);
        var condition = Assert.IsType<EnSource.Condition>(add.En);
        var and = Assert.IsType<Expr.And>(condition.Value);
        Assert.Equal("Enable", Assert.IsType<Expr.TagRef>(and.Operands[0]).Path);
        var compare = Assert.IsType<Expr.Compare>(and.Operands[1]);
        Assert.Equal("<", compare.Operator);

        Assert.Equal("AddendA", Assert.IsType<Expr.TagRef>(add.Inputs[0]).Path);
        Assert.Equal("AddendB", Assert.IsType<Expr.TagRef>(add.Inputs[1]).Path);
        Assert.Equal("Sum", add.DestTag);

        var addSidecar = Assert.Single(reduced.Sidecar.Muls);
        Assert.IsType<EnSourceSidecar.ConditionSidecar>(addSidecar.En);
    }

    [Fact]
    public void RoundTrip_AddFedByComparison_RebuildsAddPartName()
    {
        var original = LoadFixture("AddFedByComparison.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Conditional add", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var addPart = Assert.Single(reparsed.Parts, p => p.Name == "Add");
        Assert.Equal(29, addPart.UId);
        Assert.True(addPart.AutomaticSrcType);
    }

    [Fact]
    public void SerializeNetworkOnly_AddFedByComparison_ProducesAddKeyword()
    {
        var network = LoadFixture("AddFedByComparison.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Conditional add", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 1 \"Conditional add\"\n  ADD(EN := Enable AND Level < 10, IN1 := AddendA, IN2 := AddendB) => Sum\n",
            text);
    }

    [Fact]
    public void FullBlock_AddFedByComparison_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("AddFedByComparison.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Conditional add", compileUnitUId: "3");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        Assert.Contains("    kind = add\n", text);

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }
}
