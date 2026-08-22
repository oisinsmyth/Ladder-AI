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
    // The guard — for a clock that is not free-running
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// R000 gated at rest, R001 the phase clock (excluded), <b>R002 the arm indicator</b> (excluded — it
    /// changes when the block arms, so it has no resting value either).
    /// </summary>
    /// <summary>Three result registers, because the guard needs one of its own beside the clock.</summary>
    private static (MirrorClient Client, RecordingTransport Wire) WiredWide()
    {
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2),
            new[] { new SlotRequest("S0", 2, 3) })).Require();

        var wire = new RecordingTransport(map, Stamp);
        return (new MirrorClient(map, wire, Stamp), wire);
    }

    private static InertDeclaration GuardedDeclaration(PhaseCondition phase) => new(
        new Dictionary<int, ushort> { [0] = 0 },
        1,
        new Dictionary<int, string>
        {
            [1] = "the phase clock: running by design",
            [2] = "the arm indicator: changes with the block's own state",
        },
        null,
        phase);

    /// <summary>
    /// 🔴 <b>THE FALSE POSITIVE THE GUARD EXISTS FOR, and it was found on a real block rather than
    /// imagined.</b> An arm-gated timer reads <b>0 while disarmed</b> — and 0 is exactly what "the window
    /// just restarted" looks like. Without a guard, <c>Below</c> is satisfied on the very first poll and
    /// the wave commits believing a fresh window has begun when no window is running at all.
    /// </summary>
    [Fact]
    public void A_DISARMED_clock_reading_zero_does_NOT_satisfy_Below_once_a_guard_is_declared()
    {
        var (client, wire) = WiredWide();

        // Disarmed forever: clock pinned at 0, arm indicator low.
        wire.OnTransaction = t =>
        {
            t.SetResult(0, 0, 0);
            t.SetResult(0, 1, 0);   // the clock — reads 0 because it is NOT RUNNING
            t.SetResult(0, 2, 0);   // the arm — low
        };

        var guarded = InertPhase.Establish(client, 0, Vector, GuardedDeclaration(
            new PhaseCondition(1, "Window_ET", PhaseTrigger.Below, 50, GuardRegister: 2, GuardSignal: "Window_Armed")), maxPolls: 5);

        Assert.Equal(InertOutcome.PhaseNotReached, guarded.Outcome);

        // And it says WHICH problem it is: a window that never armed, not one that failed to wrap. Those
        // send a reader to completely different places.
        Assert.Contains("ITS GUARD IS LOW", guarded.Detail, StringComparison.Ordinal);

        // *** THE CONTROL. *** The same clock, the same instant, with no guard declared: satisfied
        // immediately and confidently wrong. This is the behaviour the guard replaces.
        var unguarded = InertPhase.Establish(client, 0, Vector, GuardedDeclaration(
            new PhaseCondition(1, "Window_ET", PhaseTrigger.Below, 50)), maxPolls: 5);

        Assert.True(unguarded.Established, unguarded.Detail);
        Assert.Contains("PHASE ESTABLISHED", unguarded.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// The worse half of the same defect: the arm→disarm edge takes the clock from mid-ramp back to 0,
    /// which is a DECREASE. Without the guard that reads as a wrap; with it the sample is discarded, and
    /// discarded includes the remembered previous value so no later comparison can straddle the disarm.
    /// </summary>
    [Fact]
    public void The_DISARM_edge_is_not_mistaken_for_a_wrap()
    {
        var (client, wire) = WiredWide();

        // Ramps while armed, then disarms: 300, 400, 500, then arm low and clock 0, and stays there.
        var step = 0;
        wire.OnTransaction = t =>
        {
            step++;
            var armed = step <= 3;
            t.SetResult(0, 0, 0);
            t.SetResult(0, 1, armed ? (ushort)(200 + step * 100) : (ushort)0);
            t.SetResult(0, 2, armed ? (ushort)1 : (ushort)0);
        };

        var report = InertPhase.Establish(client, 0, Vector, GuardedDeclaration(
            new PhaseCondition(1, "Window_ET", PhaseTrigger.Decreases, GuardRegister: 2, GuardSignal: "Window_Armed")), maxPolls: 6);

        // The only "decrease" available was the disarm, and it must not count.
        Assert.Equal(InertOutcome.PhaseNotReached, report.Outcome);
    }

    [Fact]
    public void With_the_guard_SET_the_phase_is_established_normally()
    {
        var (client, wire) = WiredWide();

        var value = (ushort)0;
        wire.OnTransaction = t =>
        {
            t.SetResult(0, 0, 0);
            t.SetResult(0, 1, value);
            t.SetResult(0, 2, 1);          // armed throughout
            value = (ushort)((value + 1) % 1000);
        };

        var report = InertPhase.Establish(client, 0, Vector, GuardedDeclaration(
            new PhaseCondition(1, "Window_ET", PhaseTrigger.Below, 50, GuardRegister: 2, GuardSignal: "Window_Armed")));

        Assert.True(report.Established, report.Detail);
        Assert.Contains("while Window_Armed", report.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>A <c>Time</c> IS TWO REGISTERS, HIGH-WORD-FIRST, AND WATCHING ONE OF THEM IS SILENTLY
    /// ALWAYS-TRUE.</b>
    ///
    /// <para><c>ResultRegisterOf</c> returns a signal's FIRST register, which for a Time is the HIGH word
    /// — and any elapsed value under 65.536 s leaves it at <b>0 permanently</b> while every millisecond
    /// lands in the second. So a one-register <c>Below</c> on a window clock reports a freshly-restarted
    /// window on the first poll of every wave, including waves where no window is running at all. The
    /// same high-word blindness is already recorded against a mirrored timer elsewhere in this system.</para>
    /// </summary>
    [Fact]
    public void A_TIME_phase_reads_BOTH_registers_because_the_high_word_alone_is_always_zero()
    {
        // 30 000 ms mid-window: high word 0, low word 30000.
        var registers = new ushort[] { 0, 0, 30_000 };

        var wide = new PhaseCondition(1, "Window_ET", PhaseTrigger.Below, 2_000, Width: 2);
        Assert.Equal(30_000u, wide.ValueIn(registers));
        Assert.False(wide.IsMet(wide.ValueIn(registers), hasPrevious: false, previous: 0));

        // *** THE CONTROL, AND IT IS THE BUG. *** The same clock read as ONE register sees the high word,
        // which is 0, and declares the window freshly restarted.
        var narrow = new PhaseCondition(1, "Window_ET", PhaseTrigger.Below, 2_000);
        Assert.Equal(0u, narrow.ValueIn(registers));
        Assert.True(narrow.IsMet(narrow.ValueIn(registers), hasPrevious: false, previous: 0));

        // And a genuinely fresh window IS caught at full width — so the fix did not simply disable the
        // trigger, which is the other way a check like this goes quiet.
        Assert.True(wide.IsMet(wide.ValueIn(new ushort[] { 0, 0, 900 }), hasPrevious: false, previous: 0));

        // A width the map cannot produce is refused rather than truncated.
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhaseCondition(1, "X", PhaseTrigger.Decreases, Width: 3));
    }

    [Fact]
    public void A_TIME_phase_that_would_run_off_the_end_of_the_band_is_refused_on_its_SECOND_register()
    {
        var (client, wire) = Wired();   // two result registers
        WindowClock(wire, start: 0, step: 100, period: 1000);

        // R001 is the last register, so a two-register signal starting there does not fit — and the first
        // register alone would have looked perfectly addressable.
        var report = InertPhase.Establish(client, 0, Vector, Declaration(
            new PhaseCondition(1, "Window_ET", PhaseTrigger.Decreases, Width: 2)));

        Assert.Equal(InertOutcome.PhaseNotReached, report.Outcome);
        Assert.Contains("occupies 2 register(s)", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_clock_cannot_be_its_own_guard()
    {
        var binding = Binding("Window_ET", PhaseTrigger.Below, 50) with { PhaseGuardSignal = "Window_ET" };
        var plan = InertRestPlan.For(binding, RegisterWordOrder.HighWordFirst);

        // The guard exists because the clock reads 0 in BOTH the "just restarted" and the "not running"
        // cases; comparing it against itself cannot separate them.
        Assert.Contains(plan.Refusals, r => r.Contains("cannot be its own arm indicator", StringComparison.Ordinal));
    }

    [Fact]
    public void A_guard_signal_the_slot_does_not_publish_is_refused_by_name()
    {
        var binding = Binding("Window_ET", PhaseTrigger.Below, 50) with { PhaseGuardSignal = "Not_Published" };
        var plan = InertRestPlan.For(binding, RegisterWordOrder.HighWordFirst);

        Assert.Contains(plan.Refusals, r => r.Contains("phase guard on 'Not_Published'", StringComparison.Ordinal));
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
