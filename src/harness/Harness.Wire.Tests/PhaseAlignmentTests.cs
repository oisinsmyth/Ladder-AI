using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// W2 — holding the inert phase until the block's own clock is at a known point.
///
/// <para>🔴 <b>What this exists to remove, measured.</b> Where a block's movement windows are TUMBLING
/// and the arm instant depends on how the previous index left the plant, a vector that cannot know the
/// phase has to place its stimulus late enough to land inside a live window under BOTH the earliest and
/// latest arm. On the one wave that has run end to end, <b>half of the dominant index was that hedge
/// rather than behaviour under test</b> — its author wrote as much into the vector.</para>
///
/// <para><b>It makes the test stronger, not merely faster.</b> The failure it removes is a step landing
/// outside a live window, which yields a verdict that never becomes conclusive — <i>"which reads as a
/// clean pass having tested nothing."</i></para>
/// </summary>
public class PhaseAlignmentTests
{
    private static readonly BuildStamp Stamp = MirrorClientTests.Stamp;
    private static readonly ushort[] Vector = { 5, 10 };

    /// <summary>
    /// R000 is gated at rest; <b>R001 is EXCLUDED and carries the phase</b>, because a phase signal is a
    /// running clock and check two refuses a gated register that moves. That contradiction is itself
    /// refused at build time — see <see cref="A_phase_signal_that_is_also_GATED_is_refused"/>.
    /// </summary>
    private static InertDeclaration Declaration(PhaseCondition? phase, int quiescenceScans = 1) => new(
        new Dictionary<int, ushort> { [0] = 0 },
        quiescenceScans,
        new Dictionary<int, string> { [1] = "the phase clock: running by design, so it has no resting value" },
        null,
        phase);

    private static (MirrorClient Client, RecordingTransport Wire) Wired()
    {
        var map = MirrorClientTests.Map();
        var wire = new RecordingTransport(map, Stamp);
        return (new MirrorClient(map, wire, Stamp), wire);
    }

    /// <summary>
    /// A window clock on R001 that climbs by <paramref name="step"/> per READ and wraps at
    /// <paramref name="period"/> — a tumbling window, sampled through the mirror exactly as the real one
    /// would be.
    /// </summary>
    private static void WindowClock(RecordingTransport wire, ushort start, ushort step, ushort period)
    {
        var value = start;
        wire.OnTransaction = t =>
        {
            t.SetResult(0, 0, 0);              // gated, at rest
            t.SetResult(0, 1, value);          // the phase clock
            value = (ushort)((value + step) % period);
        };
    }

    // ---------------------------------------------------------------------------------------------
    // The wait itself
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_commit_waits_until_the_window_clock_WRAPS_and_says_where_it_landed()
    {
        var (client, wire) = Wired();

        // Starts high and near the wrap, so Decreases cannot be satisfied for several polls.
        WindowClock(wire, start: 700, step: 100, period: 1000);

        var report = InertPhase.Establish(client, 0, Vector, Declaration(
            new PhaseCondition(1, "Window_ET", PhaseTrigger.Decreases)));

        Assert.True(report.Established, report.Detail);
        Assert.Contains("PHASE ESTABLISHED", report.Detail, StringComparison.Ordinal);
        Assert.Contains("Window_ET", report.Detail, StringComparison.Ordinal);
        Assert.Contains("decreases", report.Detail, StringComparison.Ordinal);

        // *** IT ACTUALLY WAITED. *** Decreases needs a previous sample to have decreased from, so it can
        // never be satisfied on the first poll — an implementation that returned true immediately would
        // still print every string above, and only this assertion would catch it.
        Assert.DoesNotContain("after 1 poll round(s)", report.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE, AND IT IS THE POINT OF THE WHOLE CHANGE.</b> The same clock, the same instant,
    /// with no phase declared: inert establishes immediately and the commit happens wherever the window
    /// happened to be. That is the state every wave has run in until now.
    /// </summary>
    [Fact]
    public void WITHOUT_a_phase_condition_the_commit_happens_at_WHATEVER_the_clock_reads()
    {
        var (client, wire) = Wired();
        WindowClock(wire, start: 700, step: 100, period: 1000);

        var report = InertPhase.Establish(client, 0, Vector, Declaration(phase: null));

        Assert.True(report.Established, report.Detail);
        Assert.DoesNotContain("PHASE ESTABLISHED", report.Detail, StringComparison.Ordinal);

        // And the cost of not declaring one is stated on the PASSING run — a choice that is invisible in
        // the artifact is one nobody revisits when a wave takes six minutes.
        Assert.Contains("NO PHASE CONDITION DECLARED", report.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>Below</c> beats <c>Decreases</c> on cost for a monotonic clock: it is satisfied on the FIRST
    /// poll when the wave is already well placed, where <c>Decreases</c> must always wait for the next
    /// wrap — up to a full window period.
    /// </summary>
    [Fact]
    public void BELOW_is_satisfied_immediately_when_the_window_has_only_just_started()
    {
        var (client, wire) = Wired();

        // A slow clock, so it is still early in its window by the time the inert checks have run — this
        // fixture's clock advances on EVERY transaction, and the inert phase makes several before the
        // phase wait begins.
        WindowClock(wire, start: 0, step: 1, period: 1000);

        var report = InertPhase.Establish(client, 0, Vector, Declaration(
            new PhaseCondition(1, "Window_ET", PhaseTrigger.Below, 50)));

        Assert.True(report.Established, report.Detail);
        Assert.Contains("PHASE ESTABLISHED", report.Detail, StringComparison.Ordinal);
        Assert.Contains("after 1 poll round(s)", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_phase_that_never_occurs_is_PhaseNotReached_and_the_wave_does_not_start()
    {
        var (client, wire) = Wired();

        // A clock that never wraps and never drops below the threshold.
        wire.OnTransaction = t =>
        {
            t.SetResult(0, 0, 0);
            t.SetResult(0, 1, 900);
        };

        var report = InertPhase.Establish(client, 0, Vector, Declaration(
            new PhaseCondition(1, "Window_ET", PhaseTrigger.Below, 50)), maxPolls: 6);

        // NOT Established, and NOT ScanCounterStalled: the counter is advancing perfectly well while the
        // watched quantity simply never reaches its phase, and reporting a stall sends a reader to the
        // controller for something that is not wrong with it.
        Assert.Equal(InertOutcome.PhaseNotReached, report.Outcome);
        Assert.False(report.Established);
        Assert.Contains("did not occur within the poll budget", report.Detail, StringComparison.Ordinal);
        Assert.Contains("last read 900", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_phase_register_outside_the_slot_band_is_a_DECLARATION_error_and_says_so()
    {
        var (client, wire) = Wired();
        WindowClock(wire, start: 0, step: 100, period: 1000);

        var report = InertPhase.Establish(client, 0, Vector, Declaration(
            new PhaseCondition(99, "Window_ET", PhaseTrigger.Decreases)));

        Assert.Equal(InertOutcome.PhaseNotReached, report.Outcome);

        // The distinction that matters: this is a declaration that does not fit the map, NOT a block that
        // never reached its phase. Left to the budget it would read as the latter.
        Assert.Contains("outside the band", report.Detail, StringComparison.Ordinal);
        Assert.Contains("not a block that never reached its phase", report.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The declaration itself
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Decreases_refuses_a_threshold_and_the_comparisons_require_one()
    {
        // A threshold on Decreases would be silently ignored, which is the failure mode this whole file
        // is about: a declaration that reads as doing something and does nothing.
        Assert.Throws<ArgumentException>(() => new PhaseCondition(1, "X", PhaseTrigger.Decreases, 50));
        Assert.Throws<ArgumentException>(() => new PhaseCondition(1, "X", PhaseTrigger.Below));
        Assert.Throws<ArgumentException>(() => new PhaseCondition(1, "X", PhaseTrigger.AtOrAbove));

        // The zero value is unusable, so a trigger nobody stated cannot be whichever happened to be first.
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhaseCondition(1, "X", PhaseTrigger.Unstated));
    }

    [Fact]
    public void Decreases_can_never_be_met_on_the_first_sample_because_one_sample_witnesses_no_transition()
    {
        var condition = new PhaseCondition(1, "X", PhaseTrigger.Decreases);

        Assert.False(condition.IsMet(value: 5, hasPrevious: false, previous: 0));
        Assert.True(condition.IsMet(value: 5, hasPrevious: true, previous: 900));
        Assert.False(condition.IsMet(value: 900, hasPrevious: true, previous: 5));
    }

    // ---------------------------------------------------------------------------------------------
    // Build-time refusals — every one of these would otherwise surface as "the block never reached its
    // phase", which points at the wrong thing entirely
    // ---------------------------------------------------------------------------------------------

    private static SlotBinding Binding(string? signal, PhaseTrigger trigger, ushort? threshold = null, bool gatePhase = false) =>
        new("S0",
            MirroredSignal.Ints("Step"),
            "Start",
            new[]
            {
                new MirroredSignal("Count", MirrorValueType.Int, Rest: InertRest.At("0", "idle")),

                // The phase clock: EXCLUDED in the well-formed case because it runs by design, GATED in
                // the case that must be refused.
                gatePhase
                    ? new MirroredSignal("Window_ET", MirrorValueType.Int, Rest: InertRest.At("0", "gated on purpose, to be refused"))
                    : new MirroredSignal("Window_ET", MirrorValueType.Int, Rest: InertRest.Excluded("the phase clock runs by design")),
            })
        {
            PhaseSignal = signal,
            PhaseTrigger = trigger,
            PhaseThreshold = threshold,
        };

    [Fact]
    public void A_phase_signal_the_slot_does_not_publish_is_refused_by_name()
    {
        var plan = InertRestPlan.For(Binding("Not_A_Signal", PhaseTrigger.Decreases), RegisterWordOrder.HighWordFirst);

        Assert.Contains(plan.Refusals, r => r.Contains("resolves to no result register", StringComparison.Ordinal));
    }

    [Fact]
    public void A_signal_without_a_trigger_and_a_trigger_without_a_signal_are_both_refused()
    {
        var noTrigger = InertRestPlan.For(Binding("Window_ET", PhaseTrigger.Unstated), RegisterWordOrder.HighWordFirst);
        Assert.Contains(noTrigger.Refusals, r => r.Contains("no phaseTrigger", StringComparison.Ordinal));

        // There is deliberately no default trigger: Decreases costs up to a whole window period and Below
        // can cost nothing, so choosing for the author changes what the wave costs as well as what it proves.
        var noSignal = InertRestPlan.For(Binding(null, PhaseTrigger.Decreases), RegisterWordOrder.HighWordFirst);
        Assert.Contains(noSignal.Refusals, r => r.Contains("no phaseSignal", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>The contradiction that would otherwise blame the block.</b> A phase signal has to be moving
    /// during the inert phase or there is no phase to observe — and check two refuses a gated register
    /// that moves between its observations. Left in place, inert never establishes and reports
    /// <c>NotQuiescent</c> about a signal the coordinator asked to watch precisely because it never rests.
    /// </summary>
    [Fact]
    public void A_phase_signal_that_is_also_GATED_is_refused()
    {
        var plan = InertRestPlan.For(Binding("Window_ET", PhaseTrigger.Decreases, gatePhase: true), RegisterWordOrder.HighWordFirst);

        Assert.Contains(plan.Refusals, r => r.Contains("THESE CONTRADICT EACH OTHER", StringComparison.Ordinal));
    }

    [Fact]
    public void A_well_formed_declaration_yields_a_condition_on_the_right_register()
    {
        var plan = InertRestPlan.For(Binding("Window_ET", PhaseTrigger.Below, 50), RegisterWordOrder.HighWordFirst);

        Assert.Empty(plan.Refusals);

        var phase = plan.Require().Phase;
        Assert.NotNull(phase);
        Assert.Equal("Window_ET", phase!.Signal);
        Assert.Equal(PhaseTrigger.Below, phase.Trigger);
        Assert.Equal((ushort)50, phase.Threshold);
    }
}
