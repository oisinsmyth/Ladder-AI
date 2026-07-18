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
}
