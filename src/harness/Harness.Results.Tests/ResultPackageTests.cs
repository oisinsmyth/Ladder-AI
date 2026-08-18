using Harness.Map;
using Harness.Results;
using Harness.Wire;

namespace Harness.Results.Tests;

/// <summary>
/// *** THE PROPERTY DB-8 EXISTS FOR: A RESULT MUST BE UNABLE TO LOOK CONCLUSIVE WHEN IT IS NOT. ***
///
/// <para>Most of these are negative. A package that only ever saw healthy runs would be one that could
/// not fail, and phase 5's exit criterion is not "the harness runs" — it is that <b>the package tells
/// the authoring agent something it could act on</b>, which a bare pass/fail cannot.</para>
/// </summary>
public class ResultPackageTests
{
    private static readonly BuildStamp Build = new(0xA93F2C71);

    private static readonly AssertionEnumeration Enumeration =
        AssertionEnumeration.Of(new[] { "REQ-14" }, new[] { "REQ-14.a", "REQ-14.b" });

    private static RegisterMap Map(int slots = 1, int result = 2) =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            Enumerable.Range(0, slots).Select(i => new SlotRequest($"S{i}", 2, result)).ToArray())).Require();

    private static VectorDeclaration Declaration(
        Basis? basis = null,
        FidelityDeclaration? fidelity = null,
        SettlingDeclaration? settling = null,
        IReadOnlyCollection<string>? behaviours = null,
        string vectorAuthor = "agent-b",
        string blockAuthor = "agent-a",
        bool observabilitySupported = true,
        // AMB-19. The default is a vector whose bound MATCHES the current table, because the default
        // fixture is a healthy submission. Null models "nobody asked", which is a caveat and not a pass.
        VectorBoundsCurrency? boundsCurrency = null,
        bool omitBoundsCurrency = false) =>
        new("V-1",
            basis ?? new Basis("REQ-14", "REQ-14.a"),
            fidelity ?? FidelityDeclaration.Of("M_Weigher", new[] { "fill-to-setpoint", "valve-close" },
                new[] { "in-flight-mass" }, validatedAgainstPlantData: true),
            settling ?? new SettlingDeclaration("count unchanged across 3 consecutive scans", new[] { "Demo_Count" }),
            behaviours ?? new[] { "fill-to-setpoint" },
            "Demo_Done",
            new AgentIdentity(vectorAuthor),
            new AgentIdentity(blockAuthor),
            observabilitySupported ? Supportable : null,
            omitBoundsCurrency ? null : boundsCurrency ?? CurrentBounds);

    /// <summary>A vector whose declared bound matches the enumeration's table — AMB-19's only passing state.</summary>
    /// <summary>
    /// These packages declare a bound, so the per-assertion relation is never consulted — it is only
    /// asked about the EMPTY claim. Passed explicitly rather than defaulted: the parameter is required so
    /// that a caller who has no relation says so, and says why.
    ///
    /// <para>Declared ABOVE its first use because static initialisers run in textual order — below it,
    /// every fixture here would be built against a null.</para>
    /// </summary>
    private static readonly AssertionBoundsExpectation NoRelation =
        AssertionBoundsExpectation.NotStated("these fixtures declare a bound, so the per-assertion relation is not consulted");

    private static readonly VectorBoundsCurrency CurrentBounds = BoundsCurrencyCheck.Evaluate(
        "V-1",
        new Dictionary<string, string>(StringComparer.Ordinal) { ["fill_setpoint"] = "500" },
        new Dictionary<string, string>(StringComparer.Ordinal) { ["fill_setpoint"] = "500" },
        NoRelation);

    private static readonly ObservabilityReport Supportable = ObservabilityCheck.Evaluate(
        new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 0) },
        AssertionForm.When,
        MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })), 9, 1, 1);

    private static SlotRunResult Run(SlotOutcome outcome = SlotOutcome.Completed) =>
        new(outcome, new ushort[] { 10, 1 }, new ScanCount(100), new ScanCount(140), 2, 8,
            new InertReport(InertOutcome.Established, new ScanCount(90), Array.Empty<ushort>(), Array.Empty<ushort>(), "established"),
            "stub",
            // These tests hand `assertions` to the builder directly, so they never route through the
            // observation path; one frame states the results honestly rather than claiming a series.
            // See ObservationSeries.OfSingleFrame for why a new observation test must NOT use this.
            ObservationSeries.OfSingleFrame(new ushort[] { 10, 1 }, new ScanCount(140)));

    private static ResultPackage Package(
        VectorDeclaration? declaration = null,
        AssertionEnumeration? enumeration = null,
        SlotOutcome outcome = SlotOutcome.Completed,
        StimulusEvidence? stimulus = null,
        SettlingState settling = SettlingState.Settled,
        IReadOnlyList<AssertionOutcome>? assertions = null,
        IReadOnlyList<int>? coRunners = null,
        int slotsCoveredByOneRead = 1) =>
        ResultPackageBuilder.Build(
            declaration ?? Declaration(),
            enumeration ?? Enumeration,
            Run(outcome),
            slotIndex: 0,
            waveIndex: 0,
            stimulus ?? new StimulusEvidence(true, true, 40, 8, ManifestPresence.Loaded,
                new VersionReport(VersionOutcome.Confirmed, Build.Value, Build.Value, 3, 1, "confirmed")),
            StimulusExpectation.AtLeastOneScanPerRoundTrip(8),
            settling,
            assertions ?? new[] { AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", "10") },
            coRunners ?? Array.Empty<int>(),
            Map(),
            Build,
            slotsCoveredByOneRead);

    // ---------------------------------------------------------------------------------------------
    // The happy path exists so the negatives mean something
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_live_admissible_run_whose_assertions_held_is_a_PASS()
    {
        var package = Package();

        Assert.Equal(ResultVerdict.Pass, package.Verdict);
        Assert.True(package.ConclusiveAboutTheBlock);
        Assert.Contains("not 'the block is correct'", package.WhatToDoNext, StringComparison.Ordinal);
    }

    [Fact]
    public void An_observed_disagreement_is_a_FAIL_and_points_at_the_SPECIFICATION()
    {
        var package = Package(assertions: new[] { AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", "15") });

        Assert.Equal(ResultVerdict.Fail, package.Verdict);
        Assert.Contains("NOT AGAINST THE VECTOR", package.WhatToDoNext, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // *** LIVENESS BEATS CONTENT. THIS IS THE PRECEDENCE THAT MAKES THE REST WORTH HAVING. ***
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(false, true, 40, StimulusOutcome.NeverRan)]
    [InlineData(true, false, 40, StimulusOutcome.CommandedButDidNotRun)]
    [InlineData(true, true, 0, StimulusOutcome.CounterFrozen)]
    [InlineData(true, true, 2, StimulusOutcome.CounterAdvancedTooLittle)]
    public void EVERY_ASSERTION_AGREEING_IS_STILL_STALE_WHEN_LIVENESS_IS_UNCONFIRMED(
        bool commanded, bool executed, long scanAdvance, StimulusOutcome expected)
    {
        // The registers say exactly what a pass says. That is the whole trap: "every register agrees" is
        // also what a mirror no write ever reached looks like, and a frozen mirror is perfectly
        // self-consistent. Content is judged LAST, and only after liveness.
        var package = Package(stimulus: new StimulusEvidence(commanded, executed, scanAdvance, 8,
            ManifestPresence.Loaded, new VersionReport(VersionOutcome.Confirmed, Build.Value, Build.Value, 3, 1, "ok")));

        Assert.Equal(ResultVerdict.Stale, package.Verdict);
        Assert.Equal(expected, package.Stimulus.Outcome);
        Assert.False(package.ConclusiveAboutTheBlock);
        Assert.Contains("THE EXPERIMENT NEVER RAN", package.WhatToDoNext, StringComparison.Ordinal);
    }

    [Fact]
    public void A_package_with_no_liveness_evidence_at_all_is_STALE_not_PASS()
    {
        var package = ResultPackageBuilder.Build(Declaration(), Enumeration, Run(), 0, 0,
            stimulus: null, expectation: null, SettlingState.Settled,
            new[] { AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", "10") },
            Array.Empty<int>(), Map(), Build, 1);

        Assert.Equal(ResultVerdict.Stale, package.Verdict);
        Assert.Contains(package.Stamp.Caveats, c => c.Contains("NO LIVENESS EVIDENCE", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------
    // The four not-conclusive verdicts, and each calls for a different action
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_run_that_timed_out_is_TIMED_OUT_and_says_the_condition_never_occurred()
    {
        var package = Package(outcome: SlotOutcome.TimedOut);

        Assert.Equal(ResultVerdict.TimedOut, package.Verdict);
        Assert.False(package.ConclusiveAboutTheBlock);
        Assert.Contains("NOT 'the wrong thing happened'", package.WhatToDoNext, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_whose_inert_was_never_established_is_STALE_not_a_failure()
    {
        var package = Package(outcome: SlotOutcome.NotInert);

        Assert.Equal(ResultVerdict.Stale, package.Verdict);
    }

    [Fact]
    public void A_value_that_never_settled_is_UNSETTLED_and_nothing_was_legitimately_read()
    {
        var package = Package(settling: SettlingState.NotSettled);

        Assert.Equal(ResultVerdict.Unsettled, package.Verdict);
        Assert.Contains("NOTHING WAS LEGITIMATELY READ", package.WhatToDoNext, StringComparison.Ordinal);
    }

    [Fact]
    public void Settling_that_was_never_ESTABLISHED_is_not_the_same_as_settling_that_HELD()
    {
        // The absent-field trap in miniature. "We did not check" and "it settled" are different facts and
        // only one of them licenses reading the value.
        Assert.Equal(ResultVerdict.Unsettled, Package(settling: SettlingState.NotEstablished).Verdict);
        Assert.Equal(ResultVerdict.Pass, Package(settling: SettlingState.Settled).Verdict);
    }

    [Fact]
    public void An_assertion_that_was_never_observed_is_not_counted_with_the_ones_that_held()
    {
        var package = Package(assertions: new[]
        {
            AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", "10"),
            AssertionOutcome.Compare("REQ-14.b", "Demo_Missing", "1", null),
        });

        Assert.Equal(ResultVerdict.Unsettled, package.Verdict);
        Assert.Contains(package.Assertions, a => a.State == AssertionState.NotObserved);
    }

    [Fact]
    public void A_package_whose_assertions_were_ALL_unobserved_is_NOT_OBSERVED_and_cannot_pass()
    {
        // 🔴 *** THIS ASSERTED `Refused` UNTIL 2026-08-17, AND THE PROPERTY IT WAS GUARDING — "cannot
        // pass" — IS KEPT. *** What changed is the name: `Refused` reads as "the vector was inadmissible",
        // i.e. the AUTHOR broke a rule, and its own WhatToDoNext text says exactly that while listing an
        // empty refusal set. Here the vector is admissible and the run happened; what failed is the
        // INSTRUMENT.
        var package = Package(assertions: new[] { AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", null) });

        Assert.Equal(ResultVerdict.NotObserved, package.Verdict);
        Assert.False(package.ConclusiveAboutTheBlock);

        // It must send the reader to the instrument, and must NOT send them to the durations.
        Assert.Contains("NOBODY LOOKED", package.WhatToDoNext, StringComparison.Ordinal);
        Assert.Contains("Demo_Count", package.WhatToDoNext, StringComparison.Ordinal);
    }

    [Fact]
    public void A_TIMED_OUT_RUN_THAT_OBSERVED_NOTHING_IS_NOT_OBSERVED_rather_than_TIMED_OUT()
    {
        // 🔴 *** THE MEASURED HEADLINE DEFECT, JOB9004'S FIRST LIVE WAVE, 2026-08-17. *** The wave ran, polled 7,912 times and
        // read the whole result band; every declared assertion came back `<never read>`; the package said
        // TIMEDOUT. *** TimedOut is a claim about the PLANT — "the condition never occurred" — made by a
        // package that did not observe the plant, *** and it sends its reader to check durations and
        // stimulus, which were both fine.
        var package = Package(
            outcome: SlotOutcome.TimedOut,
            assertions: new[] { AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", null) });

        Assert.Equal(ResultVerdict.NotObserved, package.Verdict);
        Assert.DoesNotContain("the condition never occurred", package.WhatToDoNext, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AN_UNSETTLED_RUN_THAT_OBSERVED_NOTHING_IS_NOT_OBSERVED_rather_than_UNSETTLED()
    {
        // The other half of the same live package: the second vector rendered UNSETTLED, which points at
        // the settling declaration. Nothing had got as far as needing to settle.
        var package = Package(
            settling: SettlingState.NotSettled,
            assertions: new[] { AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", null) });

        Assert.Equal(ResultVerdict.NotObserved, package.Verdict);
    }

    [Fact]
    public void BUT_A_RUN_THAT_OBSERVED_SOMETHING_KEEPS_ITS_OLD_VERDICT_because_a_gate_outside_its_scope_is_noise()
    {
        // *** THE UNAFFECTED CASES, ASSERTED AS DELIBERATELY AS THE MOVED ONES. *** A timeout that read its
        // registers is still a TIMEOUT — that verdict is correct and load-bearing — and a partial read is
        // still UNSETTLED. The new verdict fires only where NOTHING was read.
        Assert.Equal(ResultVerdict.TimedOut, Package(outcome: SlotOutcome.TimedOut).Verdict);

        Assert.Equal(ResultVerdict.Unsettled, Package(assertions: new[]
        {
            AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", "10"),
            AssertionOutcome.Compare("REQ-14.b", "Demo_Missing", "1", null),
        }).Verdict);
    }

    [Fact]
    public void AND_LIVENESS_STILL_OUTRANKS_IT_because_STALE_names_a_cause_where_NOT_OBSERVED_only_names_a_symptom()
    {
        // An experiment that never ran also observes nothing, and there `Stale` is the better answer: it
        // says WHY. Placing the new verdict above liveness would have replaced a diagnosis with a symptom
        // on every frozen-mirror run.
        var package = Package(
            stimulus: new StimulusEvidence(true, false, 0, 8, ManifestPresence.Loaded,
                new VersionReport(VersionOutcome.Confirmed, Build.Value, Build.Value, 3, 1, "confirmed")),
            assertions: new[] { AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", null) });

        Assert.Equal(ResultVerdict.Stale, package.Verdict);
    }

    [Fact]
    public void A_package_with_NO_assertions_is_refused_because_it_cannot_fail()
    {
        var package = Package(
            declaration: Declaration(behaviours: Array.Empty<string>()),
            assertions: Array.Empty<AssertionOutcome>());

        Assert.Equal(ResultVerdict.Refused, package.Verdict);
        Assert.Contains(package.Admissibility.Refusals, r => r.Reason == RefusalReason.NothingExamined);
    }

    // ---------------------------------------------------------------------------------------------
    // The validity stamp carries what has NOT been measured
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_result_read_out_of_a_MULTI_SLOT_group_carries_F1s_unmeasured_premise_as_a_caveat()
    {
        // The coordinator's instruction: F-1's premise is reasoned, not measured — that MB_SERVER serves a
        // 120-register six-slot read as coherently as a 20-register one-slot read. If a result's validity
        // depends on it, the stamp says so, and it does so PER RESULT because which results depend on it
        // is a property of how the run was performed.
        var grouped = Package(slotsCoveredByOneRead: 6);
        var single = Package(slotsCoveredByOneRead: 1);

        Assert.Contains(grouped.Stamp.Caveats, c => c.Contains("F-1's PREMISE IS UNMEASURED", StringComparison.Ordinal));
        Assert.DoesNotContain(single.Stamp.Caveats, c => c.Contains("F-1", StringComparison.Ordinal));
    }

    [Fact]
    public void An_unvalidated_model_is_a_caveat_on_every_result_derived_from_it()
    {
        var package = Package(declaration: Declaration(
            fidelity: FidelityDeclaration.Of("M_Rough", new[] { "fill-to-setpoint" }, validatedAgainstPlantData: false)));

        Assert.Contains(package.Stamp.Caveats, c => c.Contains("NOT BEEN VALIDATED AGAINST REAL PLANT DATA", StringComparison.Ordinal));

        // And a model that names no absences at all is itself worth saying, because every model omits
        // something and a declaration claiming otherwise has simply not been written.
        Assert.Contains(package.Stamp.Caveats, c => c.Contains("NO absences at all", StringComparison.Ordinal));
    }

    [Fact]
    public void A_run_with_no_manifest_records_that_the_question_was_not_answered()
    {
        var package = Package(stimulus: new StimulusEvidence(true, true, 40, 8, ManifestPresence.NotAvailable,
            new VersionReport(VersionOutcome.Confirmed, Build.Value, Build.Value, 3, 1, "ok")));

        Assert.Equal(ResultVerdict.Pass, package.Verdict);
        Assert.Contains(package.Stamp.Caveats, c => c.Contains("NO LOAD MANIFEST", StringComparison.Ordinal));
    }

    [Fact]
    public void The_stamp_carries_the_map_hash_so_a_result_cannot_outlive_the_map_it_was_read_through()
    {
        Assert.Equal(Map().MapHash, Package().Stamp.MapHash);
    }

    // ---------------------------------------------------------------------------------------------
    // The co-running slice, and what a green never licenses
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_package_carries_who_MEASURABLY_ran_alongside_it()
    {
        var package = Package(coRunners: new[] { 1, 3 });

        Assert.Equal(new[] { 1, 3 }, package.CoRunners);
        Assert.Contains("ran alongside slot(s) 1, 3", package.Summary(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_PASS_says_explicitly_that_it_does_not_mean_the_co_running_slice_was_benign()
    {
        Assert.Contains("only that nothing detected interference", Package().WhatToDoNext, StringComparison.Ordinal);
    }

    [Fact]
    public void The_summary_names_the_VERDICT_FIRST_so_it_cannot_be_skimmed_past()
    {
        Assert.StartsWith("STALE —", Package(stimulus: new StimulusEvidence(true, true, 0, 8, ManifestPresence.Loaded, null)).Summary(), StringComparison.Ordinal);
        Assert.StartsWith("PASS —", Package().Summary(), StringComparison.Ordinal);
    }

    [Fact]
    public void Only_PASS_and_FAIL_are_conclusive_about_the_block()
    {
        // Exposed as its own property so a caller aggregating results cannot reach for `!= Fail` and
        // count four different kinds of nothing as successes.
        Assert.True(Package().ConclusiveAboutTheBlock);
        Assert.True(Package(assertions: new[] { AssertionOutcome.Compare("REQ-14.a", "x", "10", "15") }).ConclusiveAboutTheBlock);

        Assert.False(Package(outcome: SlotOutcome.TimedOut).ConclusiveAboutTheBlock);
        Assert.False(Package(settling: SettlingState.NotSettled).ConclusiveAboutTheBlock);
        Assert.False(Package(stimulus: new StimulusEvidence(false, false, 0, 8, ManifestPresence.Loaded, null)).ConclusiveAboutTheBlock);
        Assert.False(Package(declaration: Declaration(vectorAuthor: "agent-a")).ConclusiveAboutTheBlock);
    }

    [Fact]
    public void Every_verdict_gives_a_DIFFERENT_instruction()
    {
        // The whole reason the states are separate. If two of them said the same thing, one of them would
        // be decoration.
        var instructions = new[]
        {
            Package().WhatToDoNext,
            Package(assertions: new[] { AssertionOutcome.Compare("REQ-14.a", "x", "10", "15") }).WhatToDoNext,
            Package(outcome: SlotOutcome.TimedOut).WhatToDoNext,
            Package(settling: SettlingState.NotSettled).WhatToDoNext,
            Package(stimulus: new StimulusEvidence(false, false, 0, 8, ManifestPresence.Loaded, null)).WhatToDoNext,
            Package(declaration: Declaration(vectorAuthor: "agent-a")).WhatToDoNext,

            // AMB-19's road to Stale is not the frozen-mirror road, and telling somebody the experiment
            // never ran when it was the PREMISE that expired sends them to the rig instead of the vector.
            Package(declaration: Declaration(boundsCurrency: Retuned)).WhatToDoNext,
        };

        Assert.Equal(7, instructions.Distinct(StringComparer.Ordinal).Count());
    }

    // ---------------------------------------------------------------------------------------------
    // AMB-19 — a bound that moved underneath a correct vector
    // ---------------------------------------------------------------------------------------------

    /// <summary>The table now says 900; the vector was written against 500 and nobody re-read it.</summary>
    private static readonly VectorBoundsCurrency Retuned = BoundsCurrencyCheck.Evaluate(
        "V-1",
        new Dictionary<string, string>(StringComparer.Ordinal) { ["fill_setpoint"] = "500" },
        new Dictionary<string, string>(StringComparer.Ordinal) { ["fill_setpoint"] = "900" },
        NoRelation);

    [Fact]
    public void A_RETUNED_BOUND_IS_STALE_AND_NOT_FAIL_even_when_every_assertion_HELD()
    {
        // The trap in full: the run is healthy, the stimulus confirmed, the value settled and every
        // assertion agreed. Under the old scheme that is an unqualified PASS against a number the
        // specification no longer states.
        var package = Package(declaration: Declaration(boundsCurrency: Retuned));

        Assert.Equal(ResultVerdict.Stale, package.Verdict);
        Assert.False(package.ConclusiveAboutTheBlock);
    }

    [Fact]
    public void A_RETUNED_BOUND_IS_STALE_AND_NOT_FAIL_even_when_an_assertion_DISAGREED()
    {
        // The direction that matters most. A disagreement observed against the wrong number is not
        // evidence about the block, and reporting Fail here is precisely the "blame the block for a
        // retune nobody told the vector about" defect.
        var package = Package(
            declaration: Declaration(boundsCurrency: Retuned),
            assertions: new[] { AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", "15") });

        Assert.Equal(ResultVerdict.Stale, package.Verdict);
        Assert.NotEqual(ResultVerdict.Fail, package.Verdict);
    }

    [Fact]
    public void A_RETUNED_BOUND_OUTRANKS_INADMISSIBILITY_so_the_verdict_is_STALE_and_not_REFUSED()
    {
        // Refused reads as "the author broke a rule". This author broke none — the number moved
        // underneath them — so bounds currency is asked before admissibility.
        var package = Package(declaration: Declaration(vectorAuthor: "agent-a", boundsCurrency: Retuned));

        Assert.Equal(ResultVerdict.Stale, package.Verdict);
    }

    [Fact]
    public void The_STALE_instruction_for_a_retune_says_DO_NOT_EDIT_THE_BLOCK_and_never_says_the_experiment_did_not_run()
    {
        var instruction = Package(declaration: Declaration(boundsCurrency: Retuned)).WhatToDoNext;

        Assert.Contains("DO NOT EDIT THE BLOCK", instruction, StringComparison.Ordinal);
        Assert.Contains("AMB-19", instruction, StringComparison.Ordinal);
        Assert.DoesNotContain("THE EXPERIMENT NEVER RAN", instruction, StringComparison.Ordinal);
    }

    [Fact]
    public void A_vector_that_DECLARED_NO_BOUND_does_not_change_the_verdict_but_is_CAVEATED()
    {
        // Unknown currency is not known staleness, so it must not masquerade as one. What it may not do
        // is vanish: the submission gate refuses it first, and if one ever reaches a package the gate
        // was bypassed and the result must still say so.
        var undeclared = BoundsCurrencyCheck.Evaluate("V-1", null,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["fill_setpoint"] = "500" },
            NoRelation);

        var package = Package(declaration: Declaration(boundsCurrency: undeclared));

        Assert.Equal(ResultVerdict.Pass, package.Verdict);
        Assert.Contains(package.Stamp.Caveats, c => c.Contains("BOUNDS CURRENCY WAS NOT ESTABLISHED", StringComparison.Ordinal));
    }

    [Fact]
    public void A_package_NOBODY_ASKED_about_bounds_carries_the_caveat_rather_than_silence()
    {
        var package = Package(declaration: Declaration(omitBoundsCurrency: true));

        Assert.Contains(package.Stamp.Caveats, c => c.Contains("NOTHING ASKED WHETHER THIS VECTOR STILL TESTS THE SPECIFIED BOUND", StringComparison.Ordinal));
    }
}
