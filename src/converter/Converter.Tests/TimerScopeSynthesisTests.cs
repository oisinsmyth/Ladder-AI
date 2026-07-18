using Converter.Ir;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Timer instance-scope synthesis (Gap G, 2026-07-18). A TON's instance scope is inferred from its
/// instance path the same way an operand's is: a timer whose instance lives in a global DB is
/// GlobalVariable; a multi-instance FB timer (a local STATIC member) is LocalVariable. Previously
/// hardcoded LocalVariable — surfaced by the synthesis-parity harness (TimerSample: a real
/// single-instance global-DB TON exports GlobalVariable).
/// </summary>
public class TimerScopeSynthesisTests
{
    [Fact]
    public void Synthesize_GlobalDbTimerInstance_ScopesGlobalVariable()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"T\"\n  TON(DB_Timers.SampleTimer0, IN := Run, PT := T#100MS)\n");

        // No local names → the instance root (DB_Timers) is a global DB.
        var sidecar = SidecarSynthesizer.Synthesize(network);

        var timer = Assert.Single(sidecar.Timers);
        Assert.Equal("GlobalVariable", timer.InstanceScope);
    }

    [Fact]
    public void Synthesize_LocalStaticTimerInstance_ScopesLocalVariable()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"T\"\n  TON(RunTimer, IN := Run, PT := T#100MS)\n");

        // RunTimer is a block-local STATIC member (a multi-instance FB timer).
        var localNames = new HashSet<string>(StringComparer.Ordinal) { "RunTimer" };
        var sidecar = SidecarSynthesizer.Synthesize(network, localNames);

        var timer = Assert.Single(sidecar.Timers);
        Assert.Equal("LocalVariable", timer.InstanceScope);
    }

    [Fact]
    public void Synthesize_Tonr_CarriesKindAndResetOperand()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"T\"\n  TONR(FBTimers.RunTimeTimer, IN := Run, PT := T#1S, R := ResetReq)\n");

        var timer = Assert.Single(SidecarSynthesizer.Synthesize(network).Timers);
        Assert.Equal(TimerKind.Tonr, timer.Kind);
        Assert.NotNull(timer.Reset);
    }

    [Fact]
    public void Synthesize_Tof_CarriesKindAndNoReset()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"T\"\n  TOF(FBTimers.HoldTimer, IN := Hold, PT := T#1S)\n");

        var timer = Assert.Single(SidecarSynthesizer.Synthesize(network).Timers);
        Assert.Equal(TimerKind.Tof, timer.Kind);
        Assert.Null(timer.Reset);
    }

    [Fact]
    public void Synthesize_CoilFedBySameNetworkTimerQ_IsTimerOutputStep()
    {
        // Gap G2: `COIL Out := <sameNetworkTimer>.Q` wires directly from the TON's Q port.
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"T\"\n" +
            "  TON(DB_Timers.T0, IN := Run, PT := T#1S)\n" +
            "  COIL Out := DB_Timers.T0.Q\n");

        var sidecar = SidecarSynthesizer.Synthesize(network);
        var assignment = Assert.Single(sidecar.Assignments);

        Assert.Null(assignment.RailWireUId); // fed by the timer's Q, not the rail
        var step = Assert.IsType<ChainStepSidecar.TimerOutputStep>(Assert.Single(assignment.Steps));
        Assert.Equal("Q", step.Port);
        Assert.Equal(Assert.Single(sidecar.Timers).TonPartUId, step.TonPartUId);
    }

    [Fact]
    public void Synthesize_CoilFedByOtherNetworkTimerQ_IsOrdinaryContact()
    {
        // A `.Q` with no matching same-network timer (cross-network read) stays an ordinary contact.
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"T\"\n  COIL Out := DB_Timers.Elsewhere.Q\n");

        var assignment = Assert.Single(SidecarSynthesizer.Synthesize(network).Assignments);

        Assert.NotNull(assignment.RailWireUId);
        Assert.IsType<ChainStepSidecar.ContactStep>(Assert.Single(assignment.Steps));
    }

    [Fact]
    public void Synthesize_CoilFedBySameNetworkLocalTimerQ_IsOrdinaryContact()
    {
        // Only a GLOBAL-instance timer's same-network Q wires directly. A LOCAL (FB-instance) timer's
        // Q is an ordinary LocalVariable Access even in the same network (FB_ShredderSequencer N11).
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"T\"\n  TON(RunTimer, IN := Enable, PT := T#1S)\n  COIL Out := RunTimer.Q\n");
        var block = new Converter.SimaticMl.DbMember("RunTimer", "TON_TIME", Retain: false, StartValue: null);
        var irBlock = new IrBlock("0", "FB", "T", 1, "LAD", null, new[] { network }, StaticMembers: new[] { block });

        var sidecar = SidecarSynthesizer.SynthesizeBlock(irBlock).Single();
        var assignment = Assert.Single(sidecar.Assignments);

        Assert.NotNull(assignment.RailWireUId); // rail-fed contact, not a timer-output direct wire
        Assert.IsType<ChainStepSidecar.ContactStep>(Assert.Single(assignment.Steps));
    }
}
