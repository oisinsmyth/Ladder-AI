using System.Globalization;
using System.Reflection;
using Harness.Results;
using Harness.Wire;

namespace Harness.Results.Tests;

/// <summary>
/// <b>Build-plan 6.6 — X-D's <c>comp_min</c> calculation and the ceilings it must clear.</b>
///
/// <para>The ceiling is the headline: <b>the timer floor is the RULED ABSOLUTE 500 ms (2026-08-18), so a
/// 2-second preset caps compression at 4.0x</b> — not the 4.3x the scan-derived floor gave, and not the
/// 10x X-D originally assumed. Every constant below is read from <c>TimeCompression</c> and
/// <c>WireTiming</c>, which transcribe the rulings and §12a — nothing is chosen in the tests either.</para>
///
/// <para>⚠️ <b>RULED IS NOT MEASURED.</b> <c>k = 5</c> was ratified by the project owner on 2026-08-18 and
/// has still never been measured; it no longer decides anything on its own, because the absolute floor is
/// four times larger and subsumes it. The scan period IS measured (24.931 ms under poll load). The tests
/// below pin the ARITHMETIC, which is exactly as sound as its inputs and no sounder.</para>
/// </summary>
public class TimeCompressionTests
{
    private static ObservabilityDeclaration Sampled(int window) =>
        new("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Sampled, window, "10");

    private static ObservabilityDeclaration Latched(int window) =>
        new("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, window, "10");

    private static CompressionRequest Request(
        double plantMs = 1_000,
        double budgetMs = 1_000,
        IReadOnlyList<ObservabilityDeclaration>? expectations = null,
        int declared = 1,
        int slotsPerPollCycle = 1,
        IReadOnlyList<TimerPreset>? presets = null,
        double? modelCompStable = 100,
        double? negligibleFraction = 0.01,
        int runtime = 1) =>
        new(plantMs, budgetMs, expectations ?? new[] { Sampled(20) }, declared, slotsPerPollCycle,
            presets ?? Array.Empty<TimerPreset>(), modelCompStable, negligibleFraction, runtime);

    private static double Floor => WireTiming.ObservabilityFloorScans(1);

    // ---------------------------------------------------------------------------------------------
    // THE MEASURED CEILING — the number this task exists to get right
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_TIMER_FLOOR_IS_THE_RULED_ABSOLUTE_500_ms_AND_A_TWO_SECOND_PRESET_CAPS_COMPRESSION_AT_4x()
    {
        // RULED BY THE PROJECT OWNER, 2026-08-18. The fraction rule asked what PROPORTION of a behaviour a
        // fixed timer may occupy; this asks how SHORT a timer may get before it stops behaving like one,
        // which is the question the hardware answers. This is the term X-D itself says OFTEN BINDS FIRST.
        Assert.Equal(500.0, TimeCompression.AbsoluteTimerFloorMs, 6);
        Assert.Equal(500.0, TimeCompression.EffectiveTimerFloorMs, 6);

        var plan = TimeCompression.Plan(
            Request(presets: new[] { new TimerPreset("Dwell", 2_000, PresetSource.Data) }),
            Floor);

        var timer = plan.Bounds.Single(b => b.Kind == CompressionBoundKind.Timer);

        // *** THE NUMBER THE RULING NAMES. ***
        Assert.Equal(4.0, timer.CompMax, 6);
        Assert.NotEqual(10.0, timer.CompMax, 1);

        // And it is stated in the detail, because a number without its provenance gets re-derived wrongly.
        Assert.Contains("ABSOLUTE 500 ms (ruled 2026-08-18)", timer.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_ABSOLUTE_FLOOR_SUBSUMES_THE_SCAN_DERIVED_ONE_and_the_max_is_taken_rather_than_assumed()
    {
        // The RULING is that 500 ms subsumes k x scan — true at the measured 24.931 ms scan with a factor
        // of four in hand, and FALSE the moment a program's scan period passes 100 ms. Both halves are
        // asserted: the subsumption TODAY, and that the code takes the maximum rather than hardcoding the
        // winner. Otherwise a future scan-period correction leaves a floor that has quietly stopped being
        // conservative — on a constant this repository has already had to raise once for being 7% low.
        Assert.Equal(5, TimeCompression.TimerScanMultiple);
        Assert.Equal(TimeCompression.TimerScanMultiple * WireTiming.ScanPeriodMs, TimeCompression.TimerFloorMs, 6);

        // 5 x 24.931 = 124.655, which is the owner's quoted 124.7 ms.
        Assert.Equal(124.655, TimeCompression.TimerFloorMs, 3);
        Assert.True(TimeCompression.AbsoluteTimerFloorMs > TimeCompression.TimerFloorMs,
            "the ruling rests on 500 ms being the larger of the two, and it must be checkable rather than asserted.");

        Assert.Equal(
            Math.Max(TimeCompression.AbsoluteTimerFloorMs, TimeCompression.TimerFloorMs),
            TimeCompression.EffectiveTimerFloorMs, 6);
    }

    [Fact]
    public void The_SAMPLED_poll_period_keys_on_RTT_p99_and_scales_with_tensor_width()
    {
        // BOUND-shaped: sizing a CEILING with the p90 permits a compression at which 10% of poll cycles no
        // longer resolve the behaviour, and the symptom is a green that was never sampled.
        Assert.Equal(201.0, TimeCompression.PollPeriodMs(1), 6);
        Assert.NotEqual(102.79, TimeCompression.PollPeriodMs(1), 2);
        Assert.Equal(402.0, TimeCompression.PollPeriodMs(2), 6);
    }

    // ---------------------------------------------------------------------------------------------
    // comp_min, and X-D's own worked verdict
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void comp_min_is_T_plant_over_T_budget()
    {
        Assert.Equal(240.0, TimeCompression.MinimumFor(4 * 60 * 60 * 1000, 60 * 1000), 6);
    }

    [Fact]
    public void A_four_hour_behaviour_in_a_sixty_second_budget_needs_240x_and_is_REFUSED_showing_BOTH_numbers()
    {
        var plan = TimeCompression.Plan(
            // Latched with a long window, so the ASSERTION ceiling is high and the TIMER is what binds —
            // which is X-D's own claim about which term usually does.
            Request(plantMs: 4 * 60 * 60 * 1000, budgetMs: 60 * 1000, expectations: new[] { Latched(1_000) },
                presets: new[] { new TimerPreset("Dwell", 2_000, PresetSource.Data) }),
            Floor);

        Assert.Equal(CompressionOutcome.Refused, plan.Outcome);
        Assert.Equal(240.0, plan.CompMin, 6);
        Assert.Equal(CompressionBoundKind.Timer, plan.BindingBound);

        Assert.Contains("240", plan.Render(), StringComparison.Ordinal);
        Assert.Contains("4", plan.Render(), StringComparison.Ordinal);
        Assert.Equal(4.0, plan.CompMax, 6);
        Assert.Contains("MODEL TASK", plan.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void comp_min_never_falls_below_one_because_dilation_is_not_a_thing_this_design_does()
    {
        Assert.Equal(1.0, TimeCompression.MinimumFor(500, 5_000), 6);
    }

    [Fact]
    public void A_budget_of_zero_is_REFUSED_rather_than_reporting_an_infinite_comp_min_as_a_number()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeCompression.MinimumFor(1000, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeCompression.MinimumFor(0, 1000));
    }

    // ---------------------------------------------------------------------------------------------
    // USE comp_min, NOT comp_max — carried by the SHAPE of the type
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_RECOMMENDED_factor_is_comp_min_and_NOTHING_recommends_comp_max()
    {
        var plan = TimeCompression.Plan(Request(plantMs: 2_000, budgetMs: 1_000), Floor);

        Assert.Equal(CompressionOutcome.Runnable, plan.Outcome);
        Assert.Equal(2.0, plan.CompMin, 6);
        Assert.Equal(plan.CompMin, plan.Recommended, 6);

        // The ceiling is genuinely higher here, so a Recommended that returned it would be visibly wrong
        // rather than coincidentally equal — the mistake the previous lanes' hardcoded-1 defect was made of.
        Assert.True(plan.CompMax > plan.CompMin);
        Assert.NotEqual(plan.CompMax, plan.Recommended, 3);

        // And no OTHER member offers the ceiling as something to use. "Run at the ceiling because the
        // ceiling permits it" is exactly what X-D's rule forbids, and a property called Best/Suggested
        // would reintroduce it without anyone editing this test.
        var suggestive = typeof(CompressionPlan)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.Contains("Recommend", StringComparison.OrdinalIgnoreCase)
                     || p.Name.Contains("Suggest", StringComparison.OrdinalIgnoreCase)
                     || p.Name.Contains("Best", StringComparison.OrdinalIgnoreCase)
                     || p.Name.Contains("Use", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(new[] { nameof(CompressionPlan.Recommended) }, suggestive);
    }

    // ---------------------------------------------------------------------------------------------
    // *** THE INTERACTION: the compression ceiling and the observability floor are ONE inequality ***
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_SAMPLED_CEILING_AND_THE_OBSERVABILITY_FLOOR_AGREE_AT_EVERY_FACTOR()
    {
        // A scan count with no comp attached silently crosses the floor under compression: 20 scans at
        // comp=1 is 2 scans at comp=10. The gate re-checks the window at the factor the wave will use, and
        // X-D computes the highest factor at which it still clears. *** THOSE ARE THE SAME INEQUALITY ***,
        // and if they could disagree, one of them would be admitting what the other refuses.
        var map = MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Sampled }));
        var window = new ScanBudget(40, 3);
        var ceiling = TimeCompression.SampledCeiling(window, Floor);

        for (var runtime = 1; runtime <= 30; runtime++)
        {
            var report = ObservabilityCheck.Evaluate(
                new[] { Sampled(window.Scans) }, AssertionForm.When, map, Floor,
                declaredCompression: window.DeclaredCompression, runtimeCompression: runtime);

            var crossedTheFloor = report.Findings.Single().Outcome == ObservabilityOutcome.WindowBelowFloorAtRuntimeCompression;

            Assert.Equal(runtime > ceiling, crossedTheFloor);
        }
    }

    [Fact]
    public void The_LATCHED_ceiling_is_the_window_in_PLANT_scans_because_the_event_must_still_occupy_one()
    {
        Assert.Equal(60.0, TimeCompression.LatchedCeiling(new ScanBudget(20, 3)), 6);

        // And it is HIGHER than the sampled one by exactly the floor, which is X-D's throughput lever and
        // F-3's argument for latching: 8.6x at the p99.
        Assert.Equal(
            TimeCompression.LatchedCeiling(new ScanBudget(20, 3)) / Floor,
            TimeCompression.SampledCeiling(new ScanBudget(20, 3), Floor), 6);
    }

    // ---------------------------------------------------------------------------------------------
    // Absent inputs: each treatment DECIDED, never defaulted
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void An_UNSTATED_preset_source_is_REFUSED_because_the_two_real_answers_push_OPPOSITE_WAYS()
    {
        var plan = TimeCompression.Plan(
            Request(plantMs: 2_000, budgetMs: 1_000,
                presets: new[] { new TimerPreset("Dwell", 500, PresetSource.Unstated) }),
            Floor);

        Assert.Equal(CompressionOutcome.NotComputable, plan.Outcome);
        Assert.Contains("OPPOSITE directions", plan.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_LITERAL_preset_NEEDS_NO_DECLARED_FRACTION_ANY_MORE_because_the_multiple_is_RULED()
    {
        // 🔴 THE INVERSION OF THE OLD TEST, DELIBERATELY. Until 2026-08-18 an unscaled literal with no
        // `negligibleFraction` reported NOT DECLARED and refused the whole plan — correctly, because the
        // specification names no value for that fraction, so the only alternatives were inventing one or
        // refusing. The ruling supplies the number (10x headroom), so the bound is now COMPUTABLE from the
        // presets alone and the input has stopped being required.
        var plan = TimeCompression.Plan(
            Request(plantMs: 2_000, budgetMs: 1_000, negligibleFraction: null,
                presets: new[] { new TimerPreset("Debounce", 5, PresetSource.Literal) }),
            Floor);

        var bound = plan.Bounds.Single(b => b.Kind == CompressionBoundKind.RatioDistortion);
        Assert.Equal(CompressionBoundState.Computed, bound.State);
        Assert.False(double.IsNaN(bound.CompMax));
        Assert.Contains("THE MULTIPLE IS RULED, NOT DECLARED", bound.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_DECLARED_negligible_fraction_IS_IGNORED_AND_THE_PLAN_SAYS_SO_rather_than_reading_as_though_it_governed()
    {
        // *** A STATED INPUT THAT QUIETLY GOVERNS NOTHING IS WORSE THAN AN ABSENT ONE. *** Submissions
        // written before the ruling still carry the field; they are not refused, and they are not allowed
        // to look as though the number they state is doing any work.
        var withFraction = TimeCompression.Plan(
            Request(negligibleFraction: 0.01, presets: new[] { new TimerPreset("Debounce", 5, PresetSource.Literal) }),
            Floor);

        var withoutFraction = TimeCompression.Plan(
            Request(negligibleFraction: null, presets: new[] { new TimerPreset("Debounce", 5, PresetSource.Literal) }),
            Floor);

        // The ARITHMETIC is identical — the declaration changes nothing.
        Assert.Equal(withoutFraction.CompMax, withFraction.CompMax, 6);
        Assert.Equal(withoutFraction.Outcome, withFraction.Outcome);

        // And the difference is SAID, on the plan that carries the superseded field — the FIELD is named,
        // the VALUE it declared is quoted back, and the supersession is stated. Only the plan carrying the
        // field says any of it.
        Assert.Contains("negligibleFraction = ", withFraction.Detail, StringComparison.Ordinal);
        Assert.Contains("AND IT WAS NOT USED", withFraction.Detail, StringComparison.Ordinal);
        Assert.Contains("replaced on 2026-08-18", withFraction.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("WAS NOT USED", withoutFraction.Detail, StringComparison.Ordinal);

        // ⚠️ THE DECLARED VALUE IS QUOTED IN THE AMBIENT CULTURE, so the percent form is rendered rather
        // than spelled out here. It is "1.00%" on this machine and "1.00 %" under the invariant culture,
        // and the difference is a space — pinning one spelling reddens this test on a machine with a
        // different locale, which is a failure that says nothing about the rule. What the rule requires is
        // that the plan quote back the number the submission declared, and that is what is checked.
        Assert.Contains(0.01.ToString("P2", CultureInfo.CurrentCulture), withFraction.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_literal_headroom_companion_keeps_the_COMPRESSED_behaviour_at_least_ten_times_the_literal()
    {
        // 🔴 THE RULED VALUE, PINNED AS A LITERAL — and it was not, until a mutation found it. Every other
        // assertion about this companion reads the constant to build its expectation, so all of them are
        // arithmetic checks that hold at ANY multiple: setting it to 5 left the whole suite green. The
        // absolute floor was pinned at 500 from the start and the companion was not, so half the ruling
        // was unguarded. A test that moves with the number it is checking is not checking the number.
        Assert.Equal(10.0, TimeCompression.LiteralHeadroomMultiple, 6);

        // 20 scans at comp 1 = 498.6 ms of plant behaviour; a 5 ms literal needs 10x headroom, so the
        // factor caps at 498.6 / (10 x 5). RULED 2026-08-18, replacing the caller-declared fraction.
        var plan = TimeCompression.Plan(
            Request(presets: new[] { new TimerPreset("Debounce", 5, PresetSource.Literal) }),
            Floor);

        var bound = plan.Bounds.Single(b => b.Kind == CompressionBoundKind.RatioDistortion);

        Assert.Equal(CompressionBoundState.Computed, bound.State);
        Assert.Equal(20 * WireTiming.ScanPeriodMs / (TimeCompression.LiteralHeadroomMultiple * 5), bound.CompMax, 6);
        Assert.Contains("CHANGED PROPORTION", bound.Detail, StringComparison.Ordinal);

        // The COMPANION is what it says it is: at the ceiling, the compressed behaviour is exactly 10x
        // the literal. Asserted from the other side so the multiple cannot drift out of the sentence.
        Assert.Equal(TimeCompression.LiteralHeadroomMultiple * 5,
            20 * WireTiming.ScanPeriodMs / bound.CompMax, 6);
    }

    [Fact]
    public void THE_LARGEST_PARTICIPATING_LITERAL_BINDS_and_a_totaliser_no_longer_needs_an_undeclarable_judgement()
    {
        // The case that motivated the replacement: several literals, and the ceiling is the one the
        // LARGEST imposes. Under the fraction rule a run-hour totaliser drove this to 0.0006x and somebody
        // had to decide, with nothing to decide from, which literals "participate". The participating set
        // is now simply the presets the submission supplies — and the arithmetic no longer needs a number
        // the specification never named.
        var plan = TimeCompression.Plan(
            Request(presets: new[]
            {
                new TimerPreset("Debounce", 5, PresetSource.Literal),
                new TimerPreset("Filter", 25, PresetSource.Literal),
            }),
            Floor);

        var binding = plan.Bounds.Where(b => b.Kind == CompressionBoundKind.RatioDistortion).MinBy(b => b.CompMax)!;

        Assert.Equal("Filter", binding.Subject);
        Assert.Equal(20 * WireTiming.ScanPeriodMs / (TimeCompression.LiteralHeadroomMultiple * 25), binding.CompMax, 6);
    }

    [Fact]
    public void An_absent_model_comp_stable_REFUSES_WHEN_COMPRESSING_and_is_merely_REPORTED_when_not()
    {
        // The established split, both branches: absent where the check needs it to proceed => refused;
        // absent where the artifact simply lacks it and it cannot bind => reported, not a pass in disguise.
        var compressing = TimeCompression.Plan(Request(plantMs: 5_000, budgetMs: 1_000, modelCompStable: null), Floor);
        Assert.Equal(CompressionOutcome.NotComputable, compressing.Outcome);
        Assert.Contains("An undeclared stability ceiling is not an infinite one", compressing.Detail, StringComparison.Ordinal);

        var notCompressing = TimeCompression.Plan(Request(plantMs: 1_000, budgetMs: 1_000, modelCompStable: null), Floor);
        Assert.Equal(CompressionOutcome.Runnable, notCompressing.Outcome);

        var bound = notCompressing.Bounds.Single(b => b.Kind == CompressionBoundKind.Model);
        Assert.Equal(CompressionBoundState.NotDeclared, bound.State);
        Assert.Contains("cannot bind", bound.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_NOT_DECLARED_bound_can_never_WIN_the_minimum_and_become_comp_max()
    {
        // NaN, not a large number: a bound nobody computed must not silently be the ceiling, and must not
        // silently fail to be it either. Here the model bound is absent and comp_min is 1, so the plan runs
        // — and comp_max is the SAMPLED assertion ceiling, never NaN and never the missing term.
        var plan = TimeCompression.Plan(Request(modelCompStable: null), Floor);

        Assert.Equal(CompressionOutcome.Runnable, plan.Outcome);
        Assert.Equal(CompressionBoundKind.Assertion, plan.BindingBound);
        Assert.False(double.IsNaN(plan.CompMax));
        Assert.Single(plan.NotDeclared);
    }

    [Fact]
    public void A_vector_that_declares_NO_OBSERVABILITY_does_not_thereby_become_freely_compressible()
    {
        var plan = TimeCompression.Plan(
            Request(plantMs: 2_000, budgetMs: 1_000, expectations: Array.Empty<ObservabilityDeclaration>()),
            Floor);

        Assert.Equal(CompressionOutcome.NotComputable, plan.Outcome);
        Assert.Contains("Empty is not clean", plan.Bounds.Single(b => b.Subject == "<no expectations>").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_SAMPLED_expectation_with_no_window_reports_an_uncomputable_ceiling_rather_than_none()
    {
        var plan = TimeCompression.Plan(Request(expectations: new[] { Sampled(0) }), Floor);

        var bound = plan.Bounds.Single(b => b.Kind == CompressionBoundKind.Assertion);
        Assert.Equal(CompressionBoundState.NotDeclared, bound.State);
        Assert.Contains("Undeclared is not exempt", bound.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_LATCHED_expectation_with_no_window_is_exempt_from_the_FLOOR_and_still_has_no_CEILING()
    {
        var plan = TimeCompression.Plan(Request(expectations: new[] { Latched(0) }), Floor);

        var bound = plan.Bounds.Single(b => b.Kind == CompressionBoundKind.Assertion);
        Assert.Equal(CompressionBoundState.NotDeclared, bound.State);
        Assert.Contains("an uncomputed ceiling is not an absent one", bound.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The zero values
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Every_enum_here_has_an_UNUSABLE_zero_value()
    {
        Assert.Equal(CompressionBoundKind.Unstated, default(CompressionBoundKind));
        Assert.Equal(CompressionBoundState.Unstated, default(CompressionBoundState));
        Assert.Equal(CompressionOutcome.Unstated, default(CompressionOutcome));
        Assert.Equal(PresetSource.Unstated, default(PresetSource));

        // And no plan ever RETURNS the zero outcome — it is only ever the uninitialised state.
        Assert.NotEqual(CompressionOutcome.Unstated, TimeCompression.Plan(Request(), Floor).Outcome);
    }
}
