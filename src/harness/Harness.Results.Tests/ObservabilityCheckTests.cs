using Harness.Results;
using Harness.Wire;

namespace Harness.Results.Tests;

/// <summary>
/// Gate 5 — <b>the contract's own "one with teeth" — as a computation.</b>
///
/// <para>It was <c>bool observabilitySupported</c>, a caller-supplied parameter: the gate the contract
/// leans on hardest was an ARGUMENT. Every test below varies an input the caller cannot assert — the
/// signal's nature, what the map provides, the window, the compression — and none of them can hand the
/// checker its answer.</para>
/// </summary>
public class ObservabilityCheckTests
{
    private static MirrorObservability Map(params InstrumentationMode[] provided) =>
        MirrorObservability.Of(("Sig", provided));

    private static ObservabilityReport Evaluate(
        SignalNature nature, InstrumentationMode mode, int window = 0,
        MirrorObservability? map = null, double floor = 9, int declared = 1, int runtime = 1) =>
        ObservabilityCheck.Evaluate(
            new[] { new ObservabilityDeclaration("Sig", nature, mode, window) },
            map ?? Map(InstrumentationMode.Latched, InstrumentationMode.Sampled, InstrumentationMode.Stamped),
            floor, declared, runtime);

    // ---------------------------------------------------------------------------------------------
    // THE TWO AXES, AND THE RELATION BETWEEN THEM
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_same_scan_COINCIDENCE_can_only_be_answered_by_a_STAMP()
    {
        // "A same-scan coincidence is unobservable by sampling AT ALL." A latch says it happened; it
        // never says two things coincided.
        Assert.Equal(new[] { InstrumentationMode.Stamped }, ObservabilityCheck.ModesThatCanAnswer(SignalNature.Coincidence));

        Assert.Equal(ObservabilityOutcome.ModeCannotAnswerThisNature, Evaluate(SignalNature.Coincidence, InstrumentationMode.Sampled, 100).Findings[0].Outcome);
        Assert.Equal(ObservabilityOutcome.ModeCannotAnswerThisNature, Evaluate(SignalNature.Coincidence, InstrumentationMode.Latched).Findings[0].Outcome);
        Assert.True(Evaluate(SignalNature.Coincidence, InstrumentationMode.Stamped).Supported);
    }

    [Fact]
    public void A_TRANSIENT_cannot_be_SAMPLED_at_any_window_however_large()
    {
        // "A one-scan event is unobservable at ANY polling rate" — structurally invisible, and no
        // protocol choice changes it. A window of ten thousand scans does not help, which is the point:
        // this refusal is about physics, not provisioning.
        foreach (var window in new[] { 1, 100, 10_000 })
        {
            var finding = Evaluate(SignalNature.Transient, InstrumentationMode.Sampled, window).Findings[0];
            Assert.Equal(ObservabilityOutcome.ModeCannotAnswerThisNature, finding.Outcome);
            Assert.Contains("at ANY polling rate", finding.Detail, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_TRANSIENT_is_answered_by_a_LATCH_and_by_a_STAMP()
    {
        Assert.True(Evaluate(SignalNature.Transient, InstrumentationMode.Latched).Supported);
        Assert.True(Evaluate(SignalNature.Transient, InstrumentationMode.Stamped).Supported);
    }

    [Fact]
    public void A_PERSISTENT_state_is_the_only_nature_the_window_check_does_any_work_for()
    {
        Assert.Equal(3, ObservabilityCheck.ModesThatCanAnswer(SignalNature.PersistentState).Count);

        // Latched and Stamped are exempt from the floor whatever the window; only Sampled has one to clear.
        Assert.True(Evaluate(SignalNature.PersistentState, InstrumentationMode.Latched, window: 0).Supported);
        Assert.True(Evaluate(SignalNature.PersistentState, InstrumentationMode.Stamped, window: 0).Supported);
        Assert.False(Evaluate(SignalNature.PersistentState, InstrumentationMode.Sampled, window: 0).Supported);
    }

    // ---------------------------------------------------------------------------------------------
    // WHAT THE MAP ACTUALLY PROVIDES — the caller cannot assert this either
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_mode_the_MAP_does_not_provide_is_refused_however_suitable_it_would_be()
    {
        var finding = Evaluate(SignalNature.Transient, InstrumentationMode.Latched, map: Map(InstrumentationMode.Sampled)).Findings[0];

        Assert.Equal(ObservabilityOutcome.MapDoesNotProvideIt, finding.Outcome);
        Assert.Contains("regenerating the copy layer", finding.Detail, StringComparison.Ordinal);
        Assert.Contains("open with the owner", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_MINIMAL_COPY_LAYER_PROVIDES_SAMPLED_AND_NOTHING_ELSE_so_a_transient_vector_is_refused_for_a_TRUE_reason()
    {
        // Phase 2's generator emits result-register MOVEs and no per-signal latch or scan-stamp — listed
        // in its own doc comment as deliberate absences. So this refusal is the gate WORKING, computed
        // from what the copy layer actually generates, rather than somebody passing a false.
        var minimal = MirrorObservability.FromMinimalCopyLayer(new[] { "Sig" });

        Assert.Equal(new[] { InstrumentationMode.Sampled }, minimal.For("Sig"));
        Assert.Equal(ObservabilityOutcome.MapDoesNotProvideIt,
            Evaluate(SignalNature.Transient, InstrumentationMode.Latched, map: minimal).Findings[0].Outcome);
    }

    [Fact]
    public void A_signal_absent_from_the_map_is_refused_and_says_the_declaration_must_predate_the_download()
    {
        var finding = ObservabilityCheck.Evaluate(
            new[] { new ObservabilityDeclaration("Elsewhere", SignalNature.PersistentState, InstrumentationMode.Sampled, 40) },
            Map(InstrumentationMode.Sampled), 9, 1, 1).Findings[0];

        Assert.Equal(ObservabilityOutcome.SignalNotInMap, finding.Outcome);
        Assert.Contains("BEFORE the download", finding.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // THE FLOOR, AND THE COMPRESSION RE-CHECK
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_sampled_window_below_the_floor_is_refused_and_names_the_worse_outcome()
    {
        var finding = Evaluate(SignalNature.PersistentState, InstrumentationMode.Sampled, window: 5, floor: 9).Findings[0];

        Assert.Equal(ObservabilityOutcome.WindowBelowFloor, finding.Outcome);
        Assert.Contains("AT it you observe something plausible", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_window_SOUND_AT_AUTHORING_TIME_and_VOID_AT_RUN_TIME_is_caught_by_the_compression_re_check()
    {
        // Contract §4.4: a behaviour occupying 20 scans at comp=1 occupies 2 at comp=10 — crossing the
        // floor with nobody editing the vector. Without this, the observability check is sound when it is
        // written and silently meaningless when it runs.
        Assert.True(Evaluate(SignalNature.PersistentState, InstrumentationMode.Sampled, window: 20, floor: 9, declared: 1, runtime: 1).Supported);

        var finding = Evaluate(SignalNature.PersistentState, InstrumentationMode.Sampled, window: 20, floor: 9, declared: 1, runtime: 10).Findings[0];
        Assert.Equal(ObservabilityOutcome.WindowBelowFloorAtRuntimeCompression, finding.Outcome);
        Assert.Contains("nobody edited the vector", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_sampled_expectation_with_no_declared_window_is_refused_because_undeclared_is_not_exempt()
    {
        Assert.Equal(ObservabilityOutcome.WindowNotDeclared,
            Evaluate(SignalNature.PersistentState, InstrumentationMode.Sampled, window: 0).Findings[0].Outcome);
    }

    [Fact]
    public void The_floor_is_READ_from_12a_and_scales_with_the_tensor_width()
    {
        // Not carried in ObservabilityCheck at all — it is an argument, computed by WireTiming. A wave
        // set wide enough to double the poll period doubles what a sampled assertion must survive.
        var narrow = WireTiming.ObservabilityFloorScans(1);
        var wide = WireTiming.ObservabilityFloorScans(4);

        Assert.True(Evaluate(SignalNature.PersistentState, InstrumentationMode.Sampled, window: 20, floor: narrow).Supported);
        Assert.False(Evaluate(SignalNature.PersistentState, InstrumentationMode.Sampled, window: 20, floor: wide).Supported);
    }

    // ---------------------------------------------------------------------------------------------
    // EMPTY IS NOT CLEAN, ON BOTH SIDES
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_vector_declaring_NO_observability_is_not_observable_by_default()
    {
        var report = ObservabilityCheck.Evaluate(Array.Empty<ObservabilityDeclaration>(), Map(InstrumentationMode.Sampled), 9, 1, 1);

        Assert.False(report.Supported);
        Assert.Equal(ObservabilityOutcome.NothingToCheckAgainst, report.Findings[0].Outcome);
    }

    [Fact]
    public void AN_EMPTY_MAP_ADMITS_NOTHING_rather_than_everything()
    {
        var report = ObservabilityCheck.Evaluate(
            new[] { new ObservabilityDeclaration("Sig", SignalNature.PersistentState, InstrumentationMode.Latched, 0) },
            new MirrorObservability(new Dictionary<string, IReadOnlySet<InstrumentationMode>>()), 9, 1, 1);

        Assert.False(report.Supported);
        Assert.Equal(ObservabilityOutcome.NothingToCheckAgainst, report.Findings[0].Outcome);
        Assert.Contains("admits everything while reading exactly like a check that ran", report.Findings[0].Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_floor_of_zero_scans_is_refused_because_it_would_admit_a_one_scan_event()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObservabilityCheck.Evaluate(
                new[] { new ObservabilityDeclaration("Sig", SignalNature.PersistentState, InstrumentationMode.Sampled, 1) },
                Map(InstrumentationMode.Sampled), floorScans: 0, 1, 1));
    }

    [Fact]
    public void THERE_IS_NO_WAY_TO_HAND_THE_CHECKER_ITS_ANSWER()
    {
        // The property the whole change exists for. ObservabilityReport has no public constructor that
        // takes a verdict, and Evaluate's parameters are all evidence: the declarations, the map, the
        // floor, the two compression factors. None of them is "supported".
        var parameters = typeof(ObservabilityCheck)
            .GetMethod(nameof(ObservabilityCheck.Evaluate))!
            .GetParameters()
            .Select(p => p.Name!)
            .ToArray();

        Assert.Equal(new[] { "expectations", "map", "floorScans", "declaredCompression", "runtimeCompression" }, parameters);
        Assert.DoesNotContain(parameters, p => p.Contains("support", StringComparison.OrdinalIgnoreCase)
                                            || p.Contains("observabilitySupported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void And_Admissibility_no_longer_takes_a_bool_for_it_either()
    {
        var observabilityParameter = typeof(Admissibility)
            .GetMethod(nameof(Admissibility.Check))!
            .GetParameters()
            .Single(p => p.Name == "observability");

        Assert.Equal(typeof(ObservabilityReport), observabilityParameter.ParameterType);
    }
}
