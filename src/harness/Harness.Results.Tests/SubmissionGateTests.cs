using Harness.Map;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// The gate runner: which gates are CHECKED, which are JUDGEMENT, and which are NOT CHECKED — and
/// that the three are never collapsed into two.
/// </summary>
public class SubmissionGateTests
{
    /// <summary>
    /// A REAL assertion ID, COMPUTED rather than invented.
    ///
    /// <para>It used to be a hand-written six-hex literal — the method document's illustrative example, which is
    /// not the hash of anything. Gate 3g recomputes every ID from its own normalised text, so an invented
    /// hex string cannot be made to pass: finding a text that hashes to a chosen six hex digits is a
    /// preimage problem. That is the gate working, and the fixture is what had to change.</para>
    /// </summary>
    private const string ClauseId = "REQ-014";

    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";

    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    private static readonly IReadOnlyDictionary<string, string> Texts =
        new Dictionary<string, string>(StringComparer.Ordinal) { [AssertionIdValue] = AssertionText };

    /// <summary>AMB-14: every signal a citation of this assertion depends on. The default vector observes it.</summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Observations =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [AssertionIdValue] = new HashSet<string>(StringComparer.Ordinal) { "Demo_Count" },
        };

    private static SubmissionVector Vector(
        string id = "V-1",
        string slot = "S0",
        string author = "agent-b",
        Basis? basis = null,
        string startBool = "Demo_Start",
        int maxDuration = 20,
        int comp = 1,
        string? kills = "a ramp that overshoots by one step",
        IReadOnlyList<ObservabilityDeclaration>? expectations = null,
        AssertionForm form = AssertionForm.When,
        SettlingDeclaration? settling = null,
        IReadOnlyList<BlacklistEntry>? blacklist = null,
        // AMB-19: the default vector DECLARES the bound it was written against, because the default
        // fixture is supposed to be a COMPLETE submission. Pass an empty dictionary to model the vector
        // that says nothing — that is the hole, and it has its own tests below.
        IReadOnlyDictionary<string, string>? boundsUsed = null) =>
        new(id, slot, 0, new AgentIdentity(author),
            basis ?? new Basis("REQ-014", AssertionIdValue),
            new Dictionary<string, string> { ["Demo_Step"] = "5" },
            startBool,
            expectations ?? new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 0, "10") },
            form,
            settling ?? new SettlingDeclaration("count unchanged across 3 scans", new[] { "Demo_Count" }),
            maxDuration,
            CompletionValue: 1,
            blacklist ?? new[] { new BlacklistEntry("FC_Other", "shares the plant model instance") },
            comp,
            new[] { "ramp-to-limit" },
            "Demo_Done",
            kills,
            boundsUsed ?? SpecifiedBounds);

    /// <summary>The enumeration's bounds table for these fixtures — AMB-19's right-hand side.</summary>
    private static readonly IReadOnlyDictionary<string, string> SpecifiedBounds =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["ramp_limit"] = "10", ["dwell"] = "T#5S" };

    private static SubmissionReport Check(
        IReadOnlyList<SubmissionVector>? vectors = null,
        string blockAuthor = "agent-a",
        MirrorObservability? map = null,
        ConflictGraph? conflicts = null,
        double floor = 9,
        int runtimeCompression = 1,
        bool omitConflictGraph = false,
        AssertionEnumeration? enumeration = null,
        BlockCompressionInputs? compressionInputs = null) =>
        SubmissionGate.Check(
            vectors ?? new[] { Vector() },
            enumeration ?? AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { AssertionIdValue },
                new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When }, "agent-c", Texts, Observations, SpecifiedBounds),
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true),
            new AgentIdentity(blockAuthor),
            // The DEFAULT fixture stands for a COMPLETE submission, which means the map came from the
            // coordinator's bindings. A caller-supplied map keeps its own provenance, because the whole
            // point of the gate-5 tests below is that a self-declared map is adjudicated differently.
            map ?? MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched }))
                with { Provenance = MapProvenance.Bindings },
            floor, runtimeCompression,
            omitConflictGraph ? null : conflicts ?? ConflictGraph.Empty,
            compressionInputs,

            // Contract 4.5, and TRUE of these fixtures rather than convenient: the mirror is %MW bit
            // memory, nothing here generates a data block, and the reachable set really is empty.
            new DeploymentDeclaration("fixture-import", Array.Empty<S7ObjectDeclaration>()),
            TagMapReach.Of(Array.Empty<S7Reach>()));

    /// <summary>
    /// One vector whose expectation declares a WINDOW, so X-D's assertion ceiling is computable for it.
    /// The default helper declares none — legal for a latch, which is exempt from the observability floor —
    /// and X-D then reports the assertion ceiling as NOT DECLARED rather than as unbounded.
    /// </summary>
    private static SubmissionVector[] Windowed =>
        new[] { Vector(expectations: new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 40, "10") }) };

    private static GateResult Gate(SubmissionReport report, string startsWith) =>
        report.Gates.Single(g => g.Gate.StartsWith(startsWith, StringComparison.Ordinal));

    // ---------------------------------------------------------------------------------------------
    // The verdict vocabulary
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_complete_submission_is_ADMISSIBLE_SUBJECT_TO_JUDGEMENT_and_never_plain_ADMISSIBLE()
    {
        var report = Check();

        Assert.Equal(SubmissionVerdict.AdmissibleSubjectToJudgement, report.Verdict);
        Assert.Equal(3, report.Judgements.Count);
        Assert.DoesNotContain(Enum.GetNames<SubmissionVerdict>(), n => n == "Admissible");
    }

    [Fact]
    public void A_submission_with_NO_VECTORS_is_NOTHING_EXAMINED_not_admissible()
    {
        var report = Check(Array.Empty<SubmissionVector>());

        Assert.Equal(SubmissionVerdict.NothingExamined, report.Verdict);
        Assert.Equal(0, report.VectorsExamined);
    }

    [Fact]
    public void A_gate_that_COULD_NOT_RUN_makes_the_submission_NOT_ADMISSIBLE()
    {
        // NOT CHECKED fails closed and there is no flag that relaxes it. This is the whole difference
        // between three outcomes and two.
        var report = Check(omitConflictGraph: true);

        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);

        // An absent graph now costs TWO gates, and they are different questions: the blacklist could not be
        // compared against anything, and X-G's multi-writer report would have been empty for a reason that
        // has nothing to do with multi-writers.
        var blacklist = Gate(report, "8 blacklist");
        Assert.Equal(GateStatus.NotChecked, blacklist.Status);
        Assert.Contains("a blacklist nobody checked", blacklist.Detail, StringComparison.Ordinal);

        var provenance = Gate(report, "8c multi-writer");
        Assert.Equal(GateStatus.NotChecked, provenance.Status);
        Assert.Contains("the same empty report", provenance.Detail, StringComparison.Ordinal);

        Assert.Equal(2, report.NotChecked.Count);
    }

    [Fact]
    public void A_JUDGEMENT_gate_is_never_reported_as_CHECKED()
    {
        var report = Check();

        Assert.All(report.Judgements, g => Assert.Equal("none, ever", g.Verifier == "density, reported not gated" ? "none, ever" : g.Verifier));
        Assert.All(report.Judgements, g => Assert.Equal(GateStatus.Judgement, g.Status));
    }

    // ---------------------------------------------------------------------------------------------
    // Gate 1 — schema
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_vector_with_no_MaxDuration_is_refused_because_it_can_be_neither_packed_nor_bounded()
    {
        var report = Check(new[] { Vector(maxDuration: 0) });

        Assert.False(Gate(report, "1 schema").Passed);
        Assert.Contains("per-test timeout", Gate(report, "1 schema").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_vector_with_no_Kills_is_refused_and_the_contract_discrepancy_is_RAISED_not_resolved()
    {
        var report = Check(new[] { Vector(kills: null) });

        var detail = Gate(report, "1 schema").Detail;
        Assert.Contains("only measures uptime", detail, StringComparison.Ordinal);
        Assert.Contains("raised as a discrepancy, not resolved", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_vector_with_no_compression_factor_is_refused()
    {
        Assert.False(Gate(Check(new[] { Vector(comp: 0) }), "1 schema").Passed);
    }

    // ---------------------------------------------------------------------------------------------
    // Gate 2 — authorship, and the keystroke that used to defeat it
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_same_agent_writing_both_is_refused()
    {
        Assert.False(Gate(Check(new[] { Vector(author: "agent-a") }), "2 authorship").Passed);
    }

    [Fact]
    public void A_CASE_OR_WHITESPACE_VARIANT_NO_LONGER_DEFEATS_D6()
    {
        // An ordinal comparison called "Agent-A " independent of "agent-a" — a gate passed by typing a
        // different string. The comparison is now normalised, which closes the trivial variants and
        // closes nothing deeper: what MAKES two agents different is still undefined.
        Assert.False(Gate(Check(new[] { Vector(author: "Agent-A ") }), "2 authorship").Passed);
        Assert.False(Gate(Check(new[] { Vector(author: "  AGENT-A") }), "2 authorship").Passed);
        Assert.True(Gate(Check(new[] { Vector(author: "agent-b") }), "2 authorship").Passed);
    }

    [Fact]
    public void An_unrecorded_author_on_either_side_is_refused()
    {
        Assert.False(Gate(Check(new[] { Vector(author: "") }), "2 authorship").Passed);
        Assert.False(Gate(Check(blockAuthor: "   "), "2 authorship").Passed);
    }

    // ---------------------------------------------------------------------------------------------
    // Gates 3d/3e - the enumeration's authority. Two caveats that became CHECKS when the enumeration
    // gained a producer emitting per-assertion forms and recording who wrote it.
    // ---------------------------------------------------------------------------------------------

    // ---------------------------------------------------------------------------------------------
    // Gate 3h — AMB-14: every signal a citation depends on, not just one
    // ---------------------------------------------------------------------------------------------

    private static AssertionEnumeration Relational(params string[] signals) =>
        AssertionEnumeration.Of(new[] { ClauseId }, new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When },
            "agent-c", Texts,
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [AssertionIdValue] = new HashSet<string>(signals, StringComparer.Ordinal),
            },
            SpecifiedBounds);

    /// <summary>
    /// *** AMB-14's CASE. *** A simultaneity claim names two signals. A vector observing ONE of them, and
    /// never the other, tests neither the second nor the RELATION — and the old single-string check passed
    /// it, reporting green on a claim it had not examined.
    /// </summary>
    [Fact]
    public void A_RELATIONAL_ASSERTION_OBSERVED_ON_ONE_OF_ITS_TWO_SIGNALS_IS_REFUSED()
    {
        var report = Check(enumeration: Relational("Demo_Count", "Demo_Inhibit"));

        var gate = Gate(report, "3h required observations");
        Assert.False(gate.Passed);
        Assert.Contains("Demo_Inhibit", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("AMB-14's CASE", gate.Detail, StringComparison.Ordinal);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);
    }

    [Fact]
    public void And_the_SAME_vector_observing_BOTH_signals_is_admitted()
    {
        var both = new[]
        {
            Vector(expectations: new[]
            {
                new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 0, "10"),
                new ObservabilityDeclaration("Demo_Inhibit", SignalNature.PersistentState, InstrumentationMode.Latched, 0, "1"),
            }),
        };

        var report = Check(both,
            map: MirrorObservability.Of(
                ("Demo_Count", new[] { InstrumentationMode.Latched }),
                ("Demo_Inhibit", new[] { InstrumentationMode.Latched })) with { Provenance = MapProvenance.Bindings },
            enumeration: Relational("Demo_Count", "Demo_Inhibit"));

        Assert.True(Gate(report, "3h required observations").Passed);
        Assert.Equal(SubmissionVerdict.AdmissibleSubjectToJudgement, report.Verdict);
    }

    [Fact]
    public void AN_ENUMERATION_DECLARING_NO_REQUIRED_OBSERVATIONS_IS_NOT_CHECKED_RATHER_THAN_PASSING()
    {
        var silent = AssertionEnumeration.Of(new[] { ClauseId }, new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When }, "agent-c", Texts);

        var gate = Gate(Check(enumeration: silent), "3h required observations");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains("RELATIONAL assertion is the case that matters", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_PARTIALLY_DECLARED_ENUMERATION_IS_NOT_A_PERMISSIVE_ONE()
    {
        // Observations declared, but for a different assertion than the one cited.
        var elsewhere = AssertionEnumeration.Of(new[] { ClauseId }, new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When }, "agent-c", Texts,
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["REQ-014:000000"] = new HashSet<string>(StringComparer.Ordinal) { "Demo_Count" },
            });

        var gate = Gate(Check(enumeration: elsewhere), "3h required observations");

        Assert.False(gate.Passed);
        Assert.Contains("checked against nothing", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_EMPTY_REQUIRED_OBSERVATION_SET_IS_REFUSED_BECAUSE_EMPTY_IS_NOT_CLEAN()
    {
        var gate = Gate(Check(enumeration: Relational()), "3h required observations");

        Assert.False(gate.Passed);
        Assert.Contains("EMPTY set", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_ENUMERATION_CARRYING_NO_FORMS_LEAVES_F3_ENFORCED_AGAINST_WHAT_THE_VECTOR_CLAIMS()
    {
        // The flat projection. Reporting this gate as passed would be exactly the failure the gate table
        // exists to prevent: the hole is as open as it was, and NOT CHECKED says so.
        var flat = AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { AssertionIdValue }, null, "agent-c", Texts, Observations);
        var report = Check(enumeration: flat);

        var gate = Gate(report, "3e assertion form authority");
        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains("cite a NEVER, declare WHEN, take the permissive path", gate.Detail, StringComparison.Ordinal);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);
    }

    [Fact]
    public void A_VECTOR_DECLARING_A_FORM_THE_ENUMERATION_CONTRADICTS_IS_REFUSED()
    {
        // *** F-3's HOLE, CLOSED WHERE THE DATA SUPPORTS IT. *** Cite an assertion the enumeration says
        // is a NEVER, declare it a WHEN, and the permissive path is no longer available.
        var enumeration = AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.Never }, "agent-c", Texts, Observations);

        var report = Check(new[] { Vector(form: AssertionForm.When) }, enumeration: enumeration);

        Assert.False(Gate(report, "3e assertion form authority").Passed);
        Assert.Contains("F-3's refusal being walked around", Gate(report, "3e").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_VECTOR_THAT_DECLARES_NO_FORM_IS_REFUSED_AND_IT_IS_ITS_OWN_CASE()
    {
        // *** NOT-DECLARED, TESTED SEPARATELY FROM MIS-DECLARED. *** A dropped form fails the same
        // comparison as a wrong one, deliberately: the form decides whether a SAMPLED observation is
        // admissible, so a field that defaulted to WHEN handed every author who omitted it the
        // permissive path.
        var report = Check(new[] { Vector(form: AssertionForm.Unstated) });

        Assert.False(Gate(report, "3e assertion form authority").Passed);
        Assert.Contains("declares no assertion form", Gate(report, "3e").Detail, StringComparison.Ordinal);
        Assert.Contains("A DROPPED FORM FAILS THE SAME COMPARISON AS A WRONG ONE", Gate(report, "3e").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_ENUMERATION_THAT_LISTS_AN_ASSERTION_WITH_NO_FORM_IS_REFUSED_TOO()
    {
        // A blank on the enumeration's side is not a WHEN either - it is a decomposition that has not
        // been finished, and F-3 cannot be enforced against it.
        var blank = AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.Unstated }, "agent-c", Texts, Observations);

        var report = Check(enumeration: blank);

        Assert.False(Gate(report, "3e assertion form authority").Passed);
        Assert.Contains("has not been finished", Gate(report, "3e").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void And_the_OBSERVABILITY_gate_then_uses_the_ENUMERATIONS_form_not_the_vectors()
    {
        // Refusing the mismatch is not enough on its own: the observability verdict has to be right too,
        // or a sampled NEVER would be reported admissible in the same run that refused the mismatch.
        var enumeration = AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.Never }, "agent-c", Texts, Observations);

        var report = Check(
            new[] { Vector(form: AssertionForm.When, expectations: new[]
                { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Sampled, 40, "10") }) },
            map: MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Sampled })) with { Provenance = MapProvenance.Bindings },
            enumeration: enumeration);

        Assert.Contains("SampledCannotAnswerANeverAssertion", Gate(report, "5 observability").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_UNRECORDED_ENUMERATOR_IS_NOT_CHECKED_BECAUSE_UNKNOWN_IS_NOT_INDEPENDENT()
    {
        var anonymous = AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When }, normalisedTexts: Texts, requiredObservations: Observations);

        var gate = Gate(Check(enumeration: anonymous), "3d enumerator independence");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains("lost AT THE DENOMINATOR", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_BLOCKS_AUTHOR_ENUMERATING_ITS_OWN_ASSERTIONS_IS_REFUSED()
    {
        // If the block's author decomposed the requirement, the denominator is the block author's own
        // reading and a vector citing into it is agreeing with the block by construction.
        var byBlockAuthor = AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When }, "agent-a", Texts, Observations);

        Assert.False(Gate(Check(enumeration: byBlockAuthor), "3d enumerator independence").Passed);
    }

    [Fact]
    public void SO_IS_THE_VECTORS_AUTHOR_ENUMERATING_THEM()
    {
        // Normalised, like D6 itself: a case-and-whitespace variant is not a different agent.
        var byVectorAuthor = AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When }, "AGENT-B ", Texts, Observations);

        var gate = Gate(Check(enumeration: byVectorAuthor), "3d enumerator independence");

        Assert.False(gate.Passed);
        Assert.Contains("THIRD party to both authors", gate.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Gate 5 — observability, now a computation
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_observability_gate_reports_the_COMPUTED_reason_not_a_caller_supplied_bool()
    {
        var report = Check(new[]
        {
            Vector(expectations: new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.Transient, InstrumentationMode.Sampled, 40) }),
        });

        var gate = Gate(report, "5 observability");
        Assert.False(gate.Passed);
        Assert.Equal(nameof(ObservabilityCheck), gate.Verifier);
        Assert.Contains("ModeCannotAnswerThisNature", gate.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Gate 7 — start bool
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Two_different_start_bools_in_one_slot_are_refused()
    {
        var report = Check(new[] { Vector("V-1"), Vector("V-2", startBool: "Other_Start") });

        Assert.False(Gate(report, "7 start bool").Passed);
        Assert.Contains("Exactly one per slot", Gate(report, "7 start bool").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_start_bool_given_as_a_BIT_POSITION_is_refused()
    {
        // The contract's one live "do not do that" on the write side: the bit order within the register
        // is [I], and the simulator and BitAddressOf agree FROM THE SAME PREMISE.
        var report = Check(new[] { Vector(startBool: "%M4009.0") });

        Assert.False(Gate(report, "7 start bool").Passed);
        Assert.Contains("bit POSITION, not a name", Gate(report, "7 start bool").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_later_scan_rule_is_reported_as_RUN_TIME_and_not_claimed_here()
    {
        Assert.Contains("enforced at run time", Gate(Check(), "7 start bool").Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Gate 8 — blacklist
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_blacklist_entry_with_no_reason_is_refused()
    {
        var report = Check(new[] { Vector(blacklist: new[] { new BlacklistEntry("FC_Other", "") }) });

        Assert.False(Gate(report, "8 blacklist").Passed);
        Assert.Contains("BECAUSE IT STILL WORKS", Gate(report, "8 blacklist").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ADD_ONLY_IS_A_PROPERTY_OF_THE_TYPE_and_there_is_nothing_to_type_to_remove_an_exclusion()
    {
        // The same shape as the split read that cannot be named: BlacklistEntry carries no negation, no
        // "allow" and no override, so an agent cannot express a removal at all.
        var properties = typeof(BlacklistEntry).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(new[] { "Block", "Reason" }, properties);
    }

    [Fact]
    public void An_EMPTY_conflict_graph_is_a_different_statement_from_an_ABSENT_one()
    {
        // "The graph ran and found no conflicts" is a result. "No graph was supplied" is not.
        Assert.Equal(GateStatus.Checked, Gate(Check(conflicts: ConflictGraph.Empty), "8 blacklist").Status);
        Assert.Equal(GateStatus.NotChecked, Gate(Check(omitConflictGraph: true), "8 blacklist").Status);
    }

    // ---------------------------------------------------------------------------------------------
    // Gate 9 — the separable half of liveness
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_vector_whose_liveness_could_NEVER_be_established_is_refused_BEFORE_a_wave_is_spent()
    {
        // The liveness CHECK is post-run and cannot be brought forward. Whether liveness could be
        // established AT ALL can be, and a vector that would return an unreadable result is refused now
        // rather than after a wave.
        var report = Check(new[] { Vector(startBool: " ") });

        Assert.False(Gate(report, "9 liveness preconditions").Passed);
        Assert.Contains("unreadable rather than failing", Gate(report, "9 liveness preconditions").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_post_run_stimulus_check_is_named_as_post_run_and_not_claimed_at_submission()
    {
        Assert.Contains("is not a submission-time gate", Gate(Check(), "9 liveness preconditions").Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Gate 8c — X-G, multi-writer provenance
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_multi_writer_on_a_DELIVERABLE_signal_is_REPORTED_by_name_on_a_passing_submission()
    {
        var graph = new ConflictGraph(new[]
        {
            new ConflictEdge("FC_PumpA", "FC_PumpB", ConflictProvenance.MultiWriter, "Pump_Run", SignalClass.Deliverable),
        });

        var report = Check(conflicts: graph);
        var gate = Gate(report, "8c multi-writer");

        // It does not refuse: the defect is in the DELIVERABLE, not in these vectors. But a passing gate
        // that said nothing would be the packer repairing a shipping defect silently, which is X-G's whole
        // complaint.
        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
        Assert.Contains("Pump_Run", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("REPORTED, NOT REFUSED", gate.Detail, StringComparison.Ordinal);
        Assert.Equal(SubmissionVerdict.AdmissibleSubjectToJudgement, report.Verdict);
    }

    [Fact]
    public void A_CONFLICT_LIST_WITH_NO_PROVENANCE_MAKES_THE_SUBMISSION_NOT_ADMISSIBLE()
    {
        // The pre-X-G input shape. An empty multi-writer report over it means nothing, and reporting it as
        // a clean bill is exactly the silence X-G was raised about.
        var report = Check(conflicts: ConflictGraph.WithoutProvenance(new[] { "FC_Other" }));

        Assert.Equal(GateStatus.NotChecked, Gate(report, "8c multi-writer").Status);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);

        // And the BLACKLIST gate still ran — the packing half of the graph is unaffected.
        Assert.Equal(GateStatus.Checked, Gate(report, "8 blacklist").Status);
    }

    [Fact]
    public void A_clean_provenanced_graph_reports_ZERO_FINDINGS_rather_than_saying_nothing()
    {
        Assert.Contains("0 multi-writer edges on deliverable signals",
            Gate(Check(), "8c multi-writer").Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Gate 10 — X-D, and the interaction with X-B
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_WAVE_RUNNING_ABOVE_A_VECTORS_ASSERTION_CEILING_IS_REFUSED()
    {
        var sampled = new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Sampled, 20, "10") };
        var map = MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Sampled }));

        // 20 scans at comp=1 against a floor of 9 gives a ceiling of ~2.2x. At comp=1 it runs; at comp=3
        // the window is 6.7 scans and the assertion is simply never sampled.
        Assert.True(Gate(Check(new[] { Vector(expectations: sampled) }, map: map, runtimeCompression: 1), "10a time compression").Passed);

        var refused = Gate(Check(new[] { Vector(expectations: sampled) }, map: map, runtimeCompression: 3), "10a time compression");

        Assert.False(refused.Passed);
        Assert.Contains("USE comp_min, NOT comp_max", refused.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_LATCHED_CEILING_IS_CHECKED_HERE_AND_GATE_5_STRUCTURALLY_CANNOT_SEE_IT()
    {
        // Gate 5 exempts LATCHED from the observability floor, correctly — a latch cannot fall in a poll
        // gap. X-D does not exempt it from the SCAN-PERIOD term: an event compressed below one scan of real
        // time does not happen long enough to be latched either. So this is a hole gate 5 cannot close.
        var latched = new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 4, "10") };
        var report = Check(new[] { Vector(expectations: latched) }, runtimeCompression: 9);

        Assert.True(Gate(report, "5 observability").Passed);
        Assert.False(Gate(report, "10a time compression").Passed);
    }

    [Fact]
    public void The_THREE_CEILINGS_A_SUBMISSION_CANNOT_CARRY_ARE_A_REAL_PASS_AT_COMP_ONE_AND_NOT_CHECKED_ABOVE_IT()
    {
        // Both branches, because the interesting one is the pass: at comp=1 a DATA preset is unscaled, an
        // unscaled LITERAL keeps its proportion and the model is not being asked to run at a factor, so
        // none of the three CAN bind. That is computed from the submission rather than assumed.
        var uncompressed = Gate(Check(runtimeCompression: 1), "10b time compression");
        Assert.Equal(GateStatus.Checked, uncompressed.Status);
        Assert.True(uncompressed.Passed);
        Assert.Contains("4.3x", uncompressed.Detail, StringComparison.Ordinal);

        var compressed = Gate(Check(runtimeCompression: 2), "10b time compression");
        Assert.Equal(GateStatus.NotChecked, compressed.Status);
        Assert.Contains("OFTEN BINDS FIRST", compressed.Detail, StringComparison.Ordinal);
        Assert.Contains("An unknown ceiling is not a high one", compressed.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void SUPPLYING_THE_BLOCK_LEVEL_CEILINGS_TURNS_10b_INTO_A_REAL_CHECK_and_the_TIMER_can_then_REFUSE()
    {
        // The remedy the NOT CHECKED text names has to exist, or the gate is a dead end wearing the costume
        // of a build list. Supplied, the plan runs — and a 500 ms DATA preset caps compression at 4.3x, so
        // a wave at 6x is refused by the term X-D says binds first.
        static BlockCompressionInputs Inputs(double plantMs) =>
            new(plantMs, 1_000, new[] { new TimerPreset("Dwell", 500, PresetSource.Data) }, 100, 0.01);

        var ok = Gate(Check(Windowed, runtimeCompression: 2, compressionInputs: Inputs(2_000)), "10b time compression");
        Assert.Equal(GateStatus.Checked, ok.Status);
        Assert.True(ok.Passed);
        Assert.Contains("RUNNABLE", ok.Detail, StringComparison.Ordinal);

        var tooFast = Gate(Check(Windowed, runtimeCompression: 6, compressionInputs: Inputs(6_000)), "10b time compression");
        Assert.Equal(GateStatus.Checked, tooFast.Status);
        Assert.False(tooFast.Passed);
        Assert.Contains("Timer", tooFast.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void RUNNING_ABOVE_comp_min_BUT_UNDER_THE_CEILING_IS_ADMITTED_AND_SAID_OUT_LOUD()
    {
        // X-D: never run at the ceiling merely because the ceiling permits it. Compression is a fidelity
        // risk, so the margin between comp_min and comp_max is margin, not headroom to spend.
        var gate = Gate(
            Check(Windowed, runtimeCompression: 3,
                compressionInputs: new BlockCompressionInputs(1_100, 1_000, new[] { new TimerPreset("Dwell", 500, PresetSource.Data) }, 100, 0.01)),
            "10b time compression");

        Assert.True(gate.Passed);
        Assert.Contains("ABOVE comp_min", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("compression is a fidelity risk", gate.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The schema gate's predicate — a field that existed and nothing checked
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AN_EXPECTATION_WITH_NO_EXPECTED_VALUE_IS_REFUSED_AT_THE_SCHEMA_GATE()
    {
        // Contract section 2 lists a predicate and ObservabilityDeclaration carried it with nothing
        // checking it. Left alone it does not become an error downstream — it becomes a spurious
        // DISAGREEMENT against the placeholder string, so the author is told the block is wrong when what
        // is wrong is that nobody said what right looks like.
        var noPredicate = new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 0) };

        var gate = Gate(Check(new[] { Vector(expectations: noPredicate) }), "1 schema");

        Assert.False(gate.Passed);
        Assert.Contains("declares no expected value", gate.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 3i — bounds currency (AMB-19). The channel made entirely out of correct decisions.
    // ---------------------------------------------------------------------------------------------

    private static AssertionEnumeration EnumerationWithBounds(IReadOnlyDictionary<string, string>? bounds) =>
        AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When }, "agent-c", Texts, Observations, bounds);

    [Fact]
    public void A_vector_written_against_the_CURRENT_bounds_passes_gate_3i_and_the_pass_NAMES_the_values()
    {
        var gate = Gate(Check(), "3i bounds currency");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);

        // A pass that does not say WHICH numbers it compared is not distinguishable from one that
        // compared nothing, which is the whole complaint this gate answers.
        Assert.Contains("ramp_limit = 10", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("dwell = T#5S", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_RETUNED_bound_refuses_the_submission_although_NOT_ONE_ASSERTION_ID_MOVED()
    {
        // The retune, exactly as AMB-19 describes it: the table now says T#9S, the vector still says
        // T#5S, and the enumeration is otherwise IDENTICAL — same clause, same assertion ID, same
        // normalised text. Every other gate stays green, which is the point.
        var retuned = EnumerationWithBounds(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ramp_limit"] = "10",
            ["dwell"] = "T#9S",
        });

        var report = Check(enumeration: retuned);

        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);

        // Nothing else noticed. Not decoration — it is the measurement that says the hole was real:
        // the citation still resolves, the ID still recomputes, the form still agrees.
        Assert.True(Gate(report, "3 basis").Passed);
        Assert.True(Gate(report, "3g assertion IDs recompute").Passed);
        Assert.True(Gate(report, "3e assertion form authority").Passed);

        var gate = Gate(report, "3i bounds currency");
        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("dwell", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("T#5S", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("T#9S", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_retune_is_reported_as_STALE_and_the_refusal_SAYS_NOT_TO_EDIT_THE_BLOCK()
    {
        // The distinction the brief turns on. A Fail would send an agent to edit correct logic — the
        // same defect as the missing-predicate case one gate up — so the wording is load-bearing and is
        // asserted rather than left to whoever next edits the string.
        var retuned = EnumerationWithBounds(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ramp_limit"] = "10",
            ["dwell"] = "T#9S",
        });

        var gate = Gate(Check(enumeration: retuned), "3i bounds currency");

        Assert.Contains("STALE, NOT FAILED", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("Do NOT edit the block", gate.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("the block disagreed", gate.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_vector_that_states_NO_BOUND_is_NOT_CHECKED_and_never_a_pass()
    {
        // *** THIS IS AMB-19 ITSELF, NOT A FORMALITY. *** A vector recording no number cannot be found
        // stale by anything, so it is the one that survives a retune in silence.
        var silent = Vector(boundsUsed: new Dictionary<string, string>(StringComparer.Ordinal));

        var report = Check(new[] { silent });
        var gate = Gate(report, "3i bounds currency");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);
        Assert.Contains("CANNOT BE FOUND STALE BY ANYTHING", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_ABSENT_bounds_table_is_NOT_CHECKED_rather_than_agreement()
    {
        var report = Check(enumeration: EnumerationWithBounds(null));
        var gate = Gate(report, "3i bounds currency");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("AN ABSENT TABLE IS NOT AN AGREEING ONE", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_UNDECLARED_vector_keeps_its_NOT_CHECKED_status_even_when_another_vector_is_STALE()
    {
        // The two facts must not be collapsed. "We compared and refused" would hide "and these others we
        // could not compare at all", and the second is the build list.
        var retuned = EnumerationWithBounds(new Dictionary<string, string>(StringComparer.Ordinal) { ["dwell"] = "T#9S" });

        var stale = Vector(id: "V-stale", boundsUsed: new Dictionary<string, string>(StringComparer.Ordinal) { ["dwell"] = "T#5S" });
        var silent = Vector(id: "V-silent", boundsUsed: new Dictionary<string, string>(StringComparer.Ordinal));

        var gate = Gate(Check(new[] { stale, silent }, enumeration: retuned), "3i bounds currency");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains("V-silent", gate.Detail, StringComparison.Ordinal);

        // And the stale one is still NAMED — downgrading the status must not lose the finding.
        Assert.Contains("V-stale", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("STALE, NOT FAILED", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_bound_the_table_does_not_contain_is_UNKNOWN_rather_than_stale()
    {
        var gate = Gate(
            Check(new[] { Vector(boundsUsed: new Dictionary<string, string>(StringComparer.Ordinal) { ["settle_time"] = "T#1S" }) }),
            "3i bounds currency");

        Assert.False(gate.Passed);
        Assert.Contains("does not contain", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("never to the block", gate.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Gate 5's AUTHORITY — the standalone tool was weaker than the loop, in the place it decides
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_SELF_DECLARED_OBSERVABILITY_MAP_MAKES_GATE_5_NOT_CHECKED_never_a_pass()
    {
        // 🔴 The map says what the copy layer PROVIDES, and this gate compares a vector's demands against
        // it. A map out of the submission is the vector author vouching for the artifact the gate exists
        // to check them against — and because this gate is consulted FIRST, being quietly permissive here
        // is worse than not running at all.
        var selfDeclared = MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched }))
            with { Provenance = MapProvenance.SelfDeclared };

        var report = Check(map: selfDeclared);
        var gate = Gate(report, "5 observability");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);
        Assert.Contains("CANNOT BE THE DECIDING VOICE", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_UNATTRIBUTED_map_is_NOT_CHECKED_too_because_the_default_must_fail_closed()
    {
        // Unstated is the enum's zero value deliberately: a field that defaulted to the trustworthy answer
        // would hand every caller who omitted it the permissive path.
        var unattributed = MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched }));

        Assert.Equal(MapProvenance.Unstated, unattributed.Provenance);

        var gate = Gate(Check(map: unattributed), "5 observability");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains("indistinguishable from one the vector author wrote", gate.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Gate 11 — "there is no S7 transport" was previously inexpressible
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void NO_S7_TRANSPORT_IS_A_POSITIVE_CLAIM_and_is_CHECKED_rather_than_skipped()
    {
        // Gate 11 rightly refused a bare `s7Objects: []` and asked for a tag map. But "the map reaches no
        // DB" and "there is no map" are different claims, and only the first was sayable — so a
        // Modbus-only deployment had to invent a tag map it does not use or sit permanently NOT CHECKED.
        var report = SubmissionGate.Check(
            new[] { Vector() },
            AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { AssertionIdValue },
                new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When }, "agent-c", Texts, Observations, SpecifiedBounds),
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true),
            new AgentIdentity("agent-a"),
            MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })) with { Provenance = MapProvenance.Bindings },
            9, 1, ConflictGraph.Empty, null,
            new DeploymentDeclaration("fixture-import", Array.Empty<S7ObjectDeclaration>(), NoS7Transport: true),
            null);

        var gate = Gate(report, "11 memory layout");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
        Assert.Contains("POSITIVE CLAIM, NOT A SKIP", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void And_declaring_NO_S7_TRANSPORT_beside_an_s7Object_is_a_CONTRADICTION_and_is_refused()
    {
        var report = SubmissionGate.Check(
            new[] { Vector() },
            AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { AssertionIdValue },
                new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When }, "agent-c", Texts, Observations, SpecifiedBounds),
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true),
            new AgentIdentity("agent-a"),
            MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })) with { Provenance = MapProvenance.Bindings },
            9, 1, ConflictGraph.Empty, null,
            new DeploymentDeclaration("fixture-import",
                new[] { new S7ObjectDeclaration("DB_X", 100, "DB_X", DeclaredLayout.Standard, "fixture-import") },
                NoS7Transport: true),
            TagMapReach.Of(Array.Empty<S7Reach>()));

        var gate = Gate(report, "11 memory layout");

        Assert.False(gate.Passed);
        Assert.Contains("Those contradict", gate.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // THE SPEC NAME -> BLOCK TAG JOIN. Measured: the harness assumed they were the same name.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_DID_NOT_RUN_TEST_a_signal_whose_TWO_NAMES_COINCIDE_still_resolves_when_STATED()
    {
        // 🔴 *** A FENCE THAT REFUSES EVERYTHING PASSES EVERY TEST THAT ONLY CHECKS REFUSALS. *** The
        // whole change is about not matching by accident, so the case that MUST keep working is the one
        // where the accident would have succeeded: the two names are the same, and the binding SAYS so.
        var map = MirrorObservability.FromBindings(new[]
        {
            new MirroredSignal("Demo_Count", MirrorValueType.Int, SpecName: "Demo_Count", LatchedBy: "FB_DemoLatch"),
        });

        var gate = Gate(Check(map: map), "5 observability");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
        Assert.Empty(map.TagsWithNoSpecName);
    }

    [Fact]
    public void A_RENAMED_SIGNAL_RESOLVES_UNDER_THE_SPEC_NAME_and_NOT_under_the_block_tag()
    {
        // The measured case: the block calls it one thing, the specification another, and a vector cites
        // the specification. 16 of 17 real signals were in this position and every mechanical path missed
        // them.
        var map = MirrorObservability.FromBindings(new[]
        {
            new MirroredSignal("HBA_Inhibit", MirrorValueType.Bool, SpecName: "Demo_Count", LatchedBy: "FB_DemoLatch"),
        });

        Assert.True(map.ProvidedFor.ContainsKey("Demo_Count"));
        Assert.False(map.ProvidedFor.ContainsKey("HBA_Inhibit"));

        Assert.True(Gate(Check(map: map), "5 observability").Passed);
    }

    [Fact]
    public void AN_UNSTATED_SPEC_NAME_IS_NOT_CHECKED_AND_IS_NAMED_never_matched_by_accident()
    {
        // *** ABSENT DOES NOT MEAN "THE SAME AS THE TAG". *** That silent identity is the assumption being
        // removed, so re-introducing it as a default would remove nothing.
        var map = MirrorObservability.FromBindings(new[]
        {
            new MirroredSignal("Demo_Count", MirrorValueType.Int),
        });

        Assert.Equal(new[] { "Demo_Count" }, map.TagsWithNoSpecName);
        Assert.Empty(map.ProvidedFor);

        var report = Check(map: map);
        var gate = Gate(report, "5 observability");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);
        Assert.Contains("Demo_Count", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("ABSENT DOES NOT MEAN", gate.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The mode is DERIVED, and a latch is admitted only on PROVENANCE
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SAMPLED_IS_DERIVED_and_a_signal_claiming_no_latch_gets_SAMPLED_ALONE()
    {
        // The copy layer emits a result-register MOVE or COIL and NO per-signal latch — named absences in
        // its own documentation. So Sampled is computed from what the generator does; nothing takes a
        // caller's word for it. Thirteen of the seventeen refusals in the live run were exactly this, and
        // they were CORRECT.
        var map = MirrorObservability.FromBindings(new[]
        {
            new MirroredSignal("Alarm", MirrorValueType.Bool, SpecName: "Alarm"),
        });

        Assert.Equal(new[] { InstrumentationMode.Sampled }, map.For("Alarm").ToArray());
        Assert.Empty(map.LatchProvenance);
    }

    [Fact]
    public void A_LATCH_IS_ADMITTED_ONLY_WHEN_THE_BINDING_NAMES_THE_BLOCK_THAT_DOES_IT()
    {
        // *** FOUR OF SEVENTEEN REFUSALS WERE FALSE: those signals genuinely ARE latched on the device, by
        // a hand-authored block the generator did not emit, and the schema could not say so. *** The fix
        // is not `latched: true` — a caller assertion is forgotten exactly when it matters — but a BLOCK
        // NAME, which is provenance and is checkable against the deployed object set.
        var map = MirrorObservability.FromBindings(new[]
        {
            new MirroredSignal("HBA_Violation_1", MirrorValueType.Bool, SpecName: "Violation1", LatchedBy: "FB_HarnessViolationLatch"),
        });

        Assert.Contains(InstrumentationMode.Latched, map.For("Violation1"));
        Assert.Contains(InstrumentationMode.Sampled, map.For("Violation1"));
        Assert.Equal("FB_HarnessViolationLatch", map.LatchProvenance["Violation1"]);
    }

    [Fact]
    public void And_the_GATE_PRINTS_the_latch_provenance_so_the_claim_can_be_CHECKED_rather_than_taken()
    {
        var map = MirrorObservability.FromBindings(new[]
        {
            new MirroredSignal("Demo_Count", MirrorValueType.Int, SpecName: "Demo_Count", LatchedBy: "FB_HarnessViolationLatch"),
        });

        var gate = Gate(Check(map: map), "5 observability");

        Assert.True(gate.Passed);
        Assert.Contains("LATCHES ARE NOT FROM THE COPY LAYER", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("Demo_Count latched by FB_HarnessViolationLatch", gate.Detail, StringComparison.Ordinal);

        // It says outright that it took the NAME and not the FACT — the gate cannot see the deployment.
        Assert.Contains("this gate takes the name, not the fact", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_IT_DOES_NOT_MANUFACTURE_A_LATCH_FOR_THE_THIRTEEN_THAT_HAVE_NONE()
    {
        // *** WHAT THIS DOES NOT FIX, AS A TEST RATHER THAN A SENTENCE. *** Giving the translation a home
        // does not make the copy layer emit latches. A Latched expectation on a signal no block latches is
        // still refused, and that refusal was one of the thirteen CORRECT ones.
        var map = MirrorObservability.FromBindings(new[]
        {
            new MirroredSignal("Demo_Count", MirrorValueType.Int, SpecName: "Demo_Count"),
        });

        var latchedExpectation = new[]
        {
            new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 0, "10"),
        };

        var gate = Gate(Check(new[] { Vector(expectations: latchedExpectation) }, map: map), "5 observability");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("MapDoesNotProvideIt", gate.Detail, StringComparison.Ordinal);
    }
}
