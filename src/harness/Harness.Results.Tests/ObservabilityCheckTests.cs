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
        MirrorObservability? map = null, double floor = 9, int declared = 1, int runtime = 1,
        AssertionForm form = AssertionForm.When) =>
        ObservabilityCheck.Evaluate(
            new[] { new ObservabilityDeclaration("Sig", nature, mode, window) },
            form,
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
        Assert.Equal(new[] { InstrumentationMode.Stamped }, ObservabilityCheck.ModesThatCanAnswer(SignalNature.Coincidence, AssertionForm.When));

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
        Assert.Equal(3, ObservabilityCheck.ModesThatCanAnswer(SignalNature.PersistentState, AssertionForm.When).Count);

        // Latched and Stamped are exempt from the floor whatever the window; only Sampled has one to clear.
        Assert.True(Evaluate(SignalNature.PersistentState, InstrumentationMode.Latched, window: 0).Supported);
        Assert.True(Evaluate(SignalNature.PersistentState, InstrumentationMode.Stamped, window: 0).Supported);
        Assert.False(Evaluate(SignalNature.PersistentState, InstrumentationMode.Sampled, window: 0).Supported);
    }

    // ---------------------------------------------------------------------------------------------
    // *** F-3, RULED 2026-08-13: SAMPLED IS ADMISSIBLE FOR PersistentState ONLY, AND NEVER FOR A NEVER ***
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_NEVER_ASSERTION_CANNOT_BE_SAMPLED_AT_ANY_NATURE_INCLUDING_THE_ONE_SAMPLING_IS_GRANTED_FOR()
    {
        // *** THE FALSE GREEN THE RULING EXISTS FOR. *** "Never saw the forbidden state" is SATISFIED BY
        // NEVER HAVING LOOKED, and a poll gap produces exactly that - so an occurrence inside a gap is
        // missed entirely and READS AS A PASS. PersistentState is the nature sampling IS granted for,
        // which is why it is the interesting case: the grant is per (nature, form), not per nature.
        var finding = Evaluate(SignalNature.PersistentState, InstrumentationMode.Sampled, window: 100,
            map: Map(InstrumentationMode.Sampled), form: AssertionForm.Never).Findings[0];

        Assert.Equal(ObservabilityOutcome.SampledCannotAnswerANeverAssertion, finding.Outcome);
        Assert.Contains("MISSED ENTIRELY AND READS AS A PASS", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_refusal_names_all_three_consequences_that_decided_it()
    {
        var detail = Evaluate(SignalNature.PersistentState, InstrumentationMode.Sampled, window: 100,
            map: Map(InstrumentationMode.Sampled), form: AssertionForm.Never).Findings[0].Detail;

        Assert.Contains("~95 scans blind", detail, StringComparison.Ordinal);                    // rare and unreproducible
        Assert.Contains("result package cannot tell the two apart", detail, StringComparison.Ordinal);
        Assert.Contains("BECAUSE NOTHING HAPPENED", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_NEVER_assertion_IS_admissible_LATCHED_or_STAMPED()
    {
        // The ruling refuses a MODE, not the assertion form. A latch cannot fall in a gap, so the same
        // claim is answerable the moment it is latched - which is the whole point of refusing rather
        // than warning.
        Assert.True(Evaluate(SignalNature.PersistentState, InstrumentationMode.Latched, form: AssertionForm.Never).Supported);
        Assert.True(Evaluate(SignalNature.PersistentState, InstrumentationMode.Stamped, form: AssertionForm.Never).Supported);
    }

    [Fact]
    public void The_sufficiency_table_LOSES_Sampled_for_a_NEVER_at_every_nature()
    {
        foreach (var nature in Enum.GetValues<SignalNature>())
        {
            Assert.DoesNotContain(InstrumentationMode.Sampled, ObservabilityCheck.ModesThatCanAnswer(nature, AssertionForm.Never));
        }

        // And keeps it for the one nature the ruling grants it to, in the WHEN form.
        Assert.Contains(InstrumentationMode.Sampled, ObservabilityCheck.ModesThatCanAnswer(SignalNature.PersistentState, AssertionForm.When));
    }

    [Fact]
    public void SAMPLED_IS_ADMISSIBLE_FOR_PersistentState_ONLY_stated_as_the_whole_grant()
    {
        // The ruling in one assertion: across both forms and all three natures, the only cell that
        // admits Sampled is (PersistentState, When).
        var admitting = from nature in Enum.GetValues<SignalNature>()
                        from form in Enum.GetValues<AssertionForm>()
                        where ObservabilityCheck.ModesThatCanAnswer(nature, form).Contains(InstrumentationMode.Sampled)
                        select (nature, form);

        Assert.Equal(new[] { (SignalNature.PersistentState, AssertionForm.When) }, admitting);
    }

    [Fact]
    public void AN_UNSTATED_FORM_FAILS_CLOSED_BECAUSE_IT_MIGHT_BE_A_NEVER()
    {
        // The zero value. Treating silence as the permissive WHEN is exactly the silent default that made
        // F-3 enforceable only against what a vector chose to claim.
        var finding = Evaluate(SignalNature.PersistentState, InstrumentationMode.Sampled, window: 100,
            map: Map(InstrumentationMode.Sampled), form: AssertionForm.Unstated).Findings[0];

        Assert.Equal(ObservabilityOutcome.SampledCannotAnswerANeverAssertion, finding.Outcome);
        Assert.Contains("UNSTATED form, which is treated as a NEVER because it might be one", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_sufficiency_table_loses_Sampled_for_an_UNSTATED_form_at_every_nature()
    {
        foreach (var nature in Enum.GetValues<SignalNature>())
            Assert.DoesNotContain(InstrumentationMode.Sampled, ObservabilityCheck.ModesThatCanAnswer(nature, AssertionForm.Unstated));
    }

    [Fact]
    public void UNSTATED_IS_THE_ZERO_VALUE_so_a_defaulted_field_can_never_read_as_WHEN()
    {
        // The whole mechanism. If When were zero, every omitted field would arrive as the permissive form
        // and nothing downstream could tell it from a deliberate declaration.
        Assert.Equal(AssertionForm.Unstated, default(AssertionForm));
        Assert.NotEqual(AssertionForm.When, default(AssertionForm));
    }

    [Fact]
    public void The_NEVER_refusal_is_a_DIFFERENT_outcome_from_the_nature_refusal()
    {
        // The signal may be perfectly readable and it is the SHAPE OF THE CLAIM that sampling cannot
        // support, so collapsing the two would tell an author to change the wrong thing.
        Assert.Equal(ObservabilityOutcome.SampledCannotAnswerANeverAssertion,
            Evaluate(SignalNature.PersistentState, InstrumentationMode.Sampled, 100, Map(InstrumentationMode.Sampled), form: AssertionForm.Never).Findings[0].Outcome);

        Assert.Equal(ObservabilityOutcome.ModeCannotAnswerThisNature,
            Evaluate(SignalNature.Transient, InstrumentationMode.Sampled, 100, Map(InstrumentationMode.Sampled), form: AssertionForm.When).Findings[0].Outcome);
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

    // 🔴 *** `THE_MINIMAL_COPY_LAYER_PROVIDES_SAMPLED_AND_NOTHING_ELSE…` WAS DELETED HERE (2026-08-14)
    // ALONG WITH THE CONSTRUCTOR IT PINNED. *** `MirrorObservability.FromMinimalCopyLayer` keyed on the
    // BLOCK'S TAG and hard-coded `{ Sampled }`, and this test was its only caller in the whole repository -
    // so it asserted the behaviour of a path nothing in production took, while `FromBindings` (which every
    // production path uses) derives both the key and the modes. Leaving a test asserting the old behaviour
    // is how a deleted permissive path comes back.
    //
    // The property it actually exercised - a Sampled-only map refuses a Latched expectation - is the test
    // directly above, which builds that map with `Map(InstrumentationMode.Sampled)` and does not need a
    // second constructor to say it.

    [Fact]
    public void A_signal_absent_from_the_map_is_refused_and_says_the_declaration_must_predate_the_download()
    {
        var finding = ObservabilityCheck.Evaluate(
            new[] { new ObservabilityDeclaration("Elsewhere", SignalNature.PersistentState, InstrumentationMode.Sampled, 40) },
            AssertionForm.When, Map(InstrumentationMode.Sampled), 9, 1, 1).Findings[0];

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
        var report = ObservabilityCheck.Evaluate(Array.Empty<ObservabilityDeclaration>(), AssertionForm.When, Map(InstrumentationMode.Sampled), 9, 1, 1);

        Assert.False(report.Supported);
        Assert.Equal(ObservabilityOutcome.NothingToCheckAgainst, report.Findings[0].Outcome);
    }

    [Fact]
    public void AN_EMPTY_MAP_ADMITS_NOTHING_rather_than_everything()
    {
        var report = ObservabilityCheck.Evaluate(
            new[] { new ObservabilityDeclaration("Sig", SignalNature.PersistentState, InstrumentationMode.Latched, 0) },
            AssertionForm.When, new MirrorObservability(new Dictionary<string, IReadOnlySet<InstrumentationMode>>()), 9, 1, 1);

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
                AssertionForm.When, Map(InstrumentationMode.Sampled), floorScans: 0, 1, 1));
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

        Assert.Equal(new[] { "expectations", "form", "map", "floorScans", "declaredCompression", "runtimeCompression" }, parameters);
        Assert.DoesNotContain(parameters, p => p.Contains("support", StringComparison.OrdinalIgnoreCase)
                                            || p.Contains("observabilitySupported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void And_Admissibility_no_longer_takes_a_bool_for_it_either()
    {
        // Asserted of EVERY `Check` overload — see the note in AdmissibilityTests: a second one arrived on
        // 2026-08-18 and a bool creeping back into whichever one this did not resolve to would reopen the
        // hole this test closes.
        var overloads = typeof(Admissibility)
            .GetMethods()
            .Where(m => m.Name == nameof(Admissibility.Check))
            .ToArray();

        Assert.True(overloads.Length >= 2, $"expected both Check overloads; found {overloads.Length}.");

        foreach (var overload in overloads)
        {
            var observabilityParameter = overload.GetParameters().Single(p => p.Name == "observability");

            Assert.Equal(typeof(ObservabilityReport), observabilityParameter.ParameterType);
        }
    }
}
