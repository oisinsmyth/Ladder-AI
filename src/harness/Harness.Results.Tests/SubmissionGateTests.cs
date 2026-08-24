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

    /// <summary>
    /// <b><c>internal</c> so the gate-0c tests can build their typed cases from the SAME vector this
    /// suite's own control uses.</b> A second hand-rolled vector is a second thing that can stop being
    /// admissible for reasons unrelated to what the test is about.
    /// </summary>
    /// <summary>
    /// Gate 1b's two halves for these fixtures: which input carries the scenario's end, and its value.
    ///
    /// <para>400 ms is 17 scans at the measured 24.931 ms period, so the fixture's 20-scan
    /// <c>maxDuration</c> sits ABOVE the scenario's own end (1b's floor) and far below
    /// <c>ceil(17 x 1.5) + 500</c> (1b's ceiling). Both bounds are cleared with room, so a fixture change
    /// that trips this gate is a real change and not a fixture running along an edge.</para>
    /// </summary>
    internal const string ScenarioEndInput = "Demo_EndAt";

    /// <inheritdoc cref="ScenarioEndInput"/>
    internal const string ScenarioEndMs = "400";

    internal static SubmissionVector Vector(
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
        IReadOnlyDictionary<string, string>? boundsUsed = null,
        string? scenarioEndMs = null) =>
        new(id, slot, 0, new AgentIdentity(author),
            basis ?? new Basis("REQ-014", AssertionIdValue),
            new Dictionary<string, string> { ["Demo_Step"] = "5", [ScenarioEndInput] = scenarioEndMs ?? ScenarioEndMs },
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
    /// <summary>
    /// The standard enumeration these fixtures cite into, for the gate-0c tests.
    ///
    /// <para>Exposed as a method rather than by making four separate statics internal: the IDs must
    /// RECOMPUTE from their own text (gate 3g), so the parts have to travel together or the caller
    /// assembles a set that fails for a reason that has nothing to do with derivation.</para>
    /// </summary>
    internal static AssertionEnumerationSet EnumerationForDerivationTests() =>
        AssertionEnumeration.Of(new[] { ClauseId }, new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When },
            "agent-c", Texts, Observations, SpecifiedBounds);

    private static readonly IReadOnlyDictionary<string, string> SpecifiedBounds =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["ramp_limit"] = "10", ["dwell"] = "T#5S" };

    /// <summary>
    /// The 2.7 join for these fixtures, complete — <b>the DID-NOT-RUN control.</b>
    ///
    /// <para>A join that refuses everything passes every test that only checks refusals. So the default
    /// fixture DECLARES a full join and gates 8 / 8c must actually RUN; the refusing states have their
    /// own tests below.</para>
    /// </summary>
    private static readonly SignalStorageMap Joined = SignalStorageMap.Of(new[]
    {
        ("Demo_Count", new SignalStorage("DemoUnit", "Demo_Count")),
        ("Demo_Inhibit", new SignalStorage("DemoUnit", "Demo_Inhibit")),
    });

    /// <summary>
    /// 🔴 <b>THE COORDINATOR — a FOURTH party, and the fixture is deliberately four-way independent.</b>
    ///
    /// <para><c>agent-a</c> wrote the block, <c>agent-b</c> the vectors, <c>agent-c</c> the enumeration,
    /// <c>agent-m</c> the model declaration, and this one the binding the observability map is derived
    /// from. Gate 5c compares the last against the first two, so a fixture that left the map unattributed
    /// would report NOT CHECKED in every test below and none of them would be measuring the map.</para>
    /// </summary>
    internal const string Coordinator = "agent-k";

    /// <summary>
    /// A map derived the way production derives it, attributed to <see cref="Coordinator"/>.
    ///
    /// <para><c>FromBindings</c> takes the author as a REQUIRED argument, so the alternative here is
    /// repeating the same identity at every call site — and one site quietly written <c>default</c> would
    /// turn gate 5c off for that test while it went on asserting about gate 5.</para>
    /// </summary>
    private static MirrorObservability Bound(params MirroredSignal[] signals) =>
        MirrorObservability.FromBindings(signals, new AgentIdentity(Coordinator));

    private static SubmissionReport Check(
        IReadOnlyList<SubmissionVector>? vectors = null,
        string blockAuthor = "agent-a",
        MirrorObservability? map = null,
        ConflictGraph? conflicts = null,
        double floor = 9,
        int runtimeCompression = 1,
        bool omitConflictGraph = false,
        AssertionEnumeration? enumeration = null,
        BlockCompressionInputs? compressionInputs = null,
        SignalStorageMap? storage = null,
        string? scenarioEndInput = ScenarioEndInput,
        int? maxIndexScans = null,
        // The two third-party identities, overridable so the cross-form tests can move ONE string at a
        // time. Everything else in this fixture is a role label, so a single instance-form override is
        // exactly the mixed tree the guard exists for.
        string fidelityDeclaredBy = "agent-m",
        string enumeratedBy = "agent-c") =>
        SubmissionGate.Check(
            vectors ?? new[] { Vector() },
            enumeration ?? AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { AssertionIdValue },
                new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When }, enumeratedBy, Texts, Observations, SpecifiedBounds),
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true, declaredBy: fidelityDeclaredBy),
            new AgentIdentity(blockAuthor),
            // The DEFAULT fixture stands for a COMPLETE submission, which means the map came from the
            // coordinator's bindings. A caller-supplied map keeps its own provenance, because the whole
            // point of the gate-5 tests below is that a self-declared map is adjudicated differently.
            // ...and attributed to the coordinator, for gate 5c, for the same reason: an unattributed map
            // is NOT CHECKED, and a default fixture that could not clear 5c would make every verdict
            // assertion below a test of the fixture rather than of the gate under examination.
            map ?? MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched }))
                with { Provenance = MapProvenance.Bindings, MapAuthor = new AgentIdentity(Coordinator) },
            floor, runtimeCompression,
            omitConflictGraph ? null : conflicts ?? ConflictGraph.Empty,
            compressionInputs,

            // Contract 4.5, and TRUE of these fixtures rather than convenient: the mirror is %MW bit
            // memory, nothing here generates a data block, and the reachable set really is empty.
            new DeploymentDeclaration("fixture-import", Array.Empty<S7ObjectDeclaration>()),
            TagMapReach.Of(Array.Empty<S7Reach>()),

            // 2.7's join, complete. Gates 8/8c are statements about STORAGE and are NOT CHECKED without it.
            storage ?? Joined,
            conflictEdgesExplicitlyNull: false,

            // These fixtures build typed objects rather than parsing a document, so the unknown-field set
            // is a COMPUTED empty rather than an unasked question. The same is true of gate 0c: with no
            // document there is no authored field to attribute, and the claim is made explicitly.
            unknownFields: Array.Empty<string>(),
            derivation: DerivationEvidence.NoDocument,

            // Gate 1b: the default fixture stands for a COMPLETE submission, so it names where its
            // scenario ends. Omit it and 1b is NOT CHECKED — which is the point of the gate, and has its
            // own test rather than being the default everything else runs under.
            scenarioEndInput: scenarioEndInput,
            maxIndexScans: maxIndexScans);

    // ---------------------------------------------------------------------------------------------
    // Gate 1b — the backstop is bounded by the scenario the vector itself declares (2026-08-22)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>The measured case that produced this gate.</b> One real submission declared 6,168 / 8,568 /
    /// 44,328 scans against 2,007 / 2,793 / 14,468 actually used — a flat ~3.07x hedge that nothing
    /// checked, worth ~24.7 minutes of wall clock if the slot never completes.
    /// </summary>
    [Fact]
    public void A_backstop_far_above_the_vector_OWN_scenario_is_REFUSED_naming_the_ratio_and_the_wasted_seconds()
    {
        // 358 s of scenario is 14,360 scans; the declared 44,328 is 3.09x it.
        var report = Check(new[] { Vector(maxDuration: 44_328, scenarioEndMs: "358000") });
        var gate = Gate(report, "1b backstop bound");

        Assert.False(gate.Passed);
        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.Contains("3.09x", gate.Detail, StringComparison.Ordinal);

        // The cost has to be in the message: a ratio is an abstraction, and the reason anyone cares is
        // the wall clock a wedged index burns before it says TIMED-OUT.
        Assert.Contains(" s of wall clock", gate.Detail, StringComparison.Ordinal);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);
    }

    /// <summary>
    /// The other direction, and the more dangerous one: a backstop the scenario cannot finish inside is a
    /// spurious TIMED-OUT <b>by construction</b> on a perfectly healthy test.
    /// </summary>
    [Fact]
    public void A_backstop_BELOW_the_scenario_own_end_is_REFUSED_because_it_fires_on_a_HEALTHY_test()
    {
        // 400 ms is 17 scans. Ten is not enough for the scenario to reach its own end.
        var gate = Gate(Check(new[] { Vector(maxDuration: 10) }), "1b backstop bound");

        Assert.False(gate.Passed);
        Assert.Contains("fires on a HEALTHY test", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_right_sized_backstop_passes_and_the_gate_states_its_denominator()
    {
        var gate = Gate(Check(), "1b backstop bound");

        Assert.True(gate.Passed);

        // Every other number this gate prints is a reason a vector was NOT bounded. This one is the
        // denominator, and it is printed on every run — the same shape as drift-check's COMPARED line.
        Assert.Contains("BOUNDED: 1 of 1 vector(s)", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void WITHOUT_a_scenarioEndInput_the_gate_is_NOT_CHECKED_and_that_makes_the_submission_INADMISSIBLE()
    {
        var report = Check(scenarioEndInput: null);
        var gate = Gate(report, "1b backstop bound");

        // NOT CHECKED, never a vacuous pass: nothing said where the scenario ends, so nothing bounded the
        // backstop, and an unbounded backstop is the whole cost this gate exists to stop.
        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Equal(NotCheckedReason.AwaitingAnArtifactThatCouldExist, gate.Reason);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);
    }

    [Fact]
    public void A_vector_that_does_not_carry_the_named_input_is_REFUSED_rather_than_skipped()
    {
        // The gate is told where to look and the vector does not have it. Skipping this one and reporting
        // the rest as bounded would be the "empty is not clean" failure at the level of a single vector.
        var gate = Gate(Check(scenarioEndInput: "Demo_NoSuchInput"), "1b backstop bound");

        Assert.False(gate.Passed);
        Assert.Contains("BOUNDED: 0 of 1 vector(s)", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("bounded by nothing", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The bound for a vector with no scenario clock at all.</b> A ramp-to-limit test finishes when a
    /// count reaches a limit and has no end TIME, so <c>scenarioEndInput</c> has nothing to point at — and
    /// without the flat ceiling there is no bound available to that shape whatsoever.
    /// </summary>
    [Fact]
    public void WITH_NO_SCENARIO_CLOCK_the_flat_ceiling_bounds_the_backstop_and_the_gate_says_it_is_the_WEAKER_claim()
    {
        var report = Check(new[] { Vector(maxDuration: 150) }, scenarioEndInput: null, maxIndexScans: 200);
        var gate = Gate(report, "1b backstop bound");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);

        // *** THE TWO BOUNDS MUST NOT READ ALIKE. *** "Bounded by a flat ceiling" is materially weaker
        // than "checked against the scenario this vector itself describes", and a submission where every
        // vector took the weaker route must not look like one where every vector took the stronger.
        Assert.Contains("by the flat ceiling of 200 scan(s) ONLY", gate.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("checked against", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_backstop_above_the_flat_ceiling_is_REFUSED_even_with_no_scenario_clock()
    {
        var gate = Gate(Check(new[] { Vector(maxDuration: 5_000) }, scenarioEndInput: null, maxIndexScans: 200), "1b backstop bound");

        Assert.False(gate.Passed);
        Assert.Contains("against the declared ceiling of 200", gate.Detail, StringComparison.Ordinal);
        Assert.Contains(" s of wall clock", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_ceiling_applies_even_WITH_a_scenario_clock_because_it_is_the_outer_bound_not_a_fallback()
    {
        // The scenario says 400 ms = 17 scans, so the per-scenario bound is ceil(17 x 1.5) + 500 = 526 and
        // a MaxDuration of 300 clears it comfortably. The flat ceiling of 100 does NOT, and it still
        // fires: the ceiling is an OUTER bound that applies to every vector, not a fallback used only
        // where the scenario clock is missing. Whichever bound is tighter is the one that refuses.
        var gate = Gate(Check(new[] { Vector(maxDuration: 300) }, maxIndexScans: 100), "1b backstop bound");

        Assert.False(gate.Passed);
        Assert.Contains("against the declared ceiling of 100", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_scenario_end_that_is_not_a_positive_number_of_milliseconds_is_REFUSED()
    {
        var gate = Gate(Check(new[] { Vector(scenarioEndMs: "T#6M") }), "1b backstop bound");

        Assert.False(gate.Passed);
        Assert.Contains("not a positive whole number of milliseconds", gate.Detail, StringComparison.Ordinal);
    }

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
                ("Demo_Inhibit", new[] { InstrumentationMode.Latched }))
                with { Provenance = MapProvenance.Bindings, MapAuthor = new AgentIdentity(Coordinator) },
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
        Assert.Contains("4.0x", uncompressed.Detail, StringComparison.Ordinal);

        var compressed = Gate(Check(runtimeCompression: 2), "10b time compression");
        Assert.Equal(GateStatus.NotChecked, compressed.Status);
        Assert.Contains("OFTEN BINDS FIRST", compressed.Detail, StringComparison.Ordinal);
        Assert.Contains("An unknown ceiling is not a high one", compressed.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void SUPPLYING_THE_BLOCK_LEVEL_CEILINGS_TURNS_10b_INTO_A_REAL_CHECK_and_the_TIMER_can_then_REFUSE()
    {
        // The remedy the NOT CHECKED text names has to exist, or the gate is a dead end wearing the costume
        // of a build list. Supplied, the plan runs — and under the RULED ABSOLUTE 500 ms floor (2026-08-18)
        // a 2-second DATA preset caps compression at 4.0x, so a wave at 6x is refused by the term X-D says
        // binds first. The preset was 500 ms here while the floor was the scan-derived 116.7 ms; the
        // headline ratio is deliberately the same, so what this test pins is the ARITHMETIC and not a
        // coincidence of two numbers.
        static BlockCompressionInputs Inputs(double plantMs) =>
            new(plantMs, 1_000, new[] { new TimerPreset("Dwell", 2_000, PresetSource.Data) }, 100, null);

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
                compressionInputs: new BlockCompressionInputs(1_100, 1_000, new[] { new TimerPreset("Dwell", 2_000, PresetSource.Data) }, 100, null)),
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

    private static AssertionEnumeration EnumerationWithBounds(
        IReadOnlyDictionary<string, string>? bounds,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? assertionBounds = null) =>
        AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When }, "agent-c", Texts, Observations, bounds,
            assertionBounds);

    /// <summary>
    /// The enumeration stating which of its bounds the default fixture's assertion depends on. <b>An
    /// empty set is the positive statement "none"</b>, which is what makes a vector's <c>boundsUsed: {}</c>
    /// checkable rather than merely accepted.
    /// </summary>
    private static AssertionEnumeration EnumerationSayingAssertionDependsOn(params string[] bounds) =>
        EnumerationWithBounds(SpecifiedBounds,
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [AssertionIdValue] = bounds.ToHashSet(StringComparer.Ordinal),
            });

    /// <summary>The vector that says NOTHING AT ALL about bounds — distinct from one claiming none.</summary>
    private static SubmissionVector SilentOnBounds(string id = "V-1") => Vector(id: id) with { BoundsUsed = null };

    /// <summary>The vector that POSITIVELY CLAIMS it was written against no bound.</summary>
    private static SubmissionVector ClaimsNoBounds(string id = "V-1") =>
        Vector(id: id, boundsUsed: new Dictionary<string, string>(StringComparer.Ordinal));

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
    public void A_vector_that_says_NOTHING_AT_ALL_about_bounds_is_NOT_CHECKED_and_never_a_pass()
    {
        // *** THIS IS AMB-19 ITSELF, NOT A FORMALITY. *** A vector recording no number cannot be found
        // stale by anything, so it is the one that survives a retune in silence. SILENCE MUST STAY A
        // REFUSAL — the empty-claim fix below deliberately does not touch this path.
        var report = Check(new[] { SilentOnBounds() });
        var gate = Gate(report, "3i bounds currency");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);
        Assert.Contains("CANNOT BE FOUND STALE BY ANYTHING", gate.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // `boundsUsed: {}` — THE POSITIVE CLAIM. Measured 2026-08-17 on a real submission: two vectors cited
    // assertions that genuinely carry no bound, recorded that truthfully, and were refused — so the only
    // way to clear the refusal was to INVENT a bound, which is the fabrication this gate exists to
    // prevent. A gate satisfiable only by making something up is inverted.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_vector_claiming_NO_BOUND_that_the_enumeration_CONFIRMS_passes_gate_3i()
    {
        var report = Check(new[] { ClaimsNoBounds() }, enumeration: EnumerationSayingAssertionDependsOn());
        var gate = Gate(report, "3i bounds currency");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);

        // The pass states its denominator and its authority. With no vector declaring a value there is
        // nothing to compare, and a summary claiming "every one matches the current table ()" would be an
        // invariance claim over an empty remainder.
        Assert.Contains("NO VECTOR HERE DECLARED A VALUE", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("VERIFIED against the enumeration, not taken from the vector", gate.Detail, StringComparison.Ordinal);
        Assert.Contains(AssertionIdValue, gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_vector_claiming_NO_BOUND_the_enumeration_CANNOT_CONFIRM_is_NOT_CHECKED_and_the_repair_is_the_ENUMERATION()
    {
        // The default enumeration carries a bounds TABLE and no per-assertion relation, so the claim
        // cannot be verified. It must not pass — and it must not tell the author to invent a number.
        var report = Check(new[] { ClaimsNoBounds() });
        var gate = Gate(report, "3i bounds currency");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);

        Assert.Contains("THE REPAIR IS TO THE ENUMERATION, NOT TO THESE VECTORS", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("assertionBounds", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("DO NOT invent a bound", gate.Detail, StringComparison.Ordinal);

        // And it is NOT reported as the silence case: the two claims have two repairs, and naming the
        // wrong one sends the author to the wrong document.
        Assert.DoesNotContain("say NOTHING AT ALL about bounds", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_vector_claiming_NO_BOUND_where_the_enumeration_NAMES_ONE_is_REFUSED_and_it_is_the_serious_case()
    {
        // *** THE CHECK THAT MAKES THIS A FIX AND NOT A HOLE. *** The enumeration says the cited
        // assertion depends on `dwell`; the vector asserts that no bound applies to it. That is a vector
        // written against a bound nobody looked at, and it is a refusal rather than a note.
        var report = Check(new[] { ClaimsNoBounds() }, enumeration: EnumerationSayingAssertionDependsOn("dwell"));
        var gate = Gate(report, "3i bounds currency");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);

        Assert.Contains("dwell", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("T#5S", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("THE BLOCK IS NOT ACCUSED OF ANYTHING HERE", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_claim_of_NO_BOUND_against_an_enumeration_with_NO_TABLE_stays_NOT_CHECKED()
    {
        // Empty is not clean: the route by which this fix could become a hole is "declare nothing on both
        // sides and sail through". An enumeration with no bounds table is not one whose assertions have
        // no bounds.
        var gate = Gate(
            Check(new[] { ClaimsNoBounds() }, enumeration: EnumerationWithBounds(null)),
            "3i bounds currency");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("AN ABSENT TABLE IS NOT AN AGREEING ONE", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_OVER_FIRE_CONVERSE_a_submission_whose_bounds_are_DECLARED_AND_CURRENT_is_untouched_by_the_empty_claim_fix()
    {
        // *** A GATE THAT FIRES OUTSIDE ITS SCOPE IS NOISE, AND NOISE GETS SWITCHED OFF. *** The fix adds
        // a consultation of a relation that the ordinary submission does not carry — so the ordinary
        // submission must be unaffected by its absence, and pass on exactly the evidence it did before.
        var report = Check();
        var gate = Gate(report, "3i bounds currency");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
        Assert.Equal(SubmissionVerdict.AdmissibleSubjectToJudgement, report.Verdict);

        // Still compares, still names both numbers. The empty-claim wording must not appear at all: a
        // pass that talks about claims nobody made is a pass nobody can read.
        Assert.Contains("ramp_limit = 10", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("dwell = T#5S", gate.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("NO VECTOR HERE DECLARED A VALUE", gate.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("positively state", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_RETUNE_IS_STILL_CAUGHT_when_a_SIBLING_vector_legitimately_cites_no_bound()
    {
        // The mixed submission, which is the shape a real one takes: one vector against a bound that
        // moved, one vector on an assertion that genuinely has none. The honest sibling must not launder
        // the stale one — the refusal must survive, and it must still name the numbers.
        var retuned = EnumerationWithBounds(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["ramp_limit"] = "10", ["dwell"] = "T#9S" },
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [AssertionIdValue] = new HashSet<string>(StringComparer.Ordinal),
            });

        var report = Check(new[] { Vector(id: "V-stale"), ClaimsNoBounds("V-unbounded") }, enumeration: retuned);
        var gate = Gate(report, "3i bounds currency");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);
        Assert.Contains("V-stale", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("T#9S", gate.Detail, StringComparison.Ordinal);
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
        var silent = SilentOnBounds("V-silent");

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
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true, declaredBy: "agent-m"),
            new AgentIdentity("agent-a"),
            MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })) with { Provenance = MapProvenance.Bindings },
            9, 1, ConflictGraph.Empty, null,
            new DeploymentDeclaration("fixture-import", Array.Empty<S7ObjectDeclaration>(), NoS7Transport: true),
            null,
            Joined, false, Array.Empty<string>(), derivation: DerivationEvidence.NoDocument);

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
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true, declaredBy: "agent-m"),
            new AgentIdentity("agent-a"),
            MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })) with { Provenance = MapProvenance.Bindings },
            9, 1, ConflictGraph.Empty, null,
            new DeploymentDeclaration("fixture-import",
                new[] { new S7ObjectDeclaration("DB_X", 100, "DB_X", DeclaredLayout.Standard, "fixture-import") },
                NoS7Transport: true),
            TagMapReach.Of(Array.Empty<S7Reach>()),
            Joined, false, Array.Empty<string>(), derivation: DerivationEvidence.NoDocument);

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
        var map = Bound(new[]
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
        var map = Bound(new[]
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
        var map = Bound(new[]
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
        var map = Bound(new[]
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
        var map = Bound(new[]
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
        var map = Bound(new[]
        {
            new MirroredSignal("Demo_Count", MirrorValueType.Int, SpecName: "Demo_Count", LatchedBy: "FB_HarnessViolationLatch"),
        });

        var gate = Gate(Check(map: map), "5 observability");

        Assert.True(gate.Passed);
        Assert.Contains("LATCH CLAIMS ADMITTED ON PROVENANCE", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("Demo_Count latched by FB_HarnessViolationLatch", gate.Detail, StringComparison.Ordinal);

        // It says outright that it took the NAME and not the FACT — the gate cannot see the deployment.
        Assert.Contains("THIS GATE TAKES THE NAME, NOT THE FACT", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_ADMISSION_IS_ON_THE_CLAIM_NOT_THE_OUTCOME_so_a_REFUSING_report_still_carries_it()
    {
        // 🔴 *** THE DEFECT THIS PINS. *** The admission used to render only when problems.Count == 0, so a
        // report that refused for ANY other reason admitted latch claims on an unverified caller-supplied
        // block name and NOTHING SAID SO. Measured: four claims went through exactly that way.
        //
        // *** AN ADMISSION THAT APPEARS ONLY WHEN EVERYTHING PASSED IS MISSING FROM EVERY REPORT ANYONE
        // READS CLOSELY *** - a refusing report is precisely the one that gets read.
        var map = Bound(new[]
        {
            new MirroredSignal("Demo_Count", MirrorValueType.Int, SpecName: "Demo_Count", LatchedBy: "FB_HarnessViolationLatch"),
        });

        // A second expectation on a signal the map does not carry, so the gate REFUSES for an unrelated
        // reason - which is the situation the defect hid in.
        var mixed = new[]
        {
            new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 0, "10"),
            new ObservabilityDeclaration("Demo_Absent", SignalNature.PersistentState, InstrumentationMode.Latched, 0, "1"),
        };

        var gate = Gate(Check(new[] { Vector(expectations: mixed) }, map: map), "5 observability");

        Assert.False(gate.Passed);
        Assert.Contains("Demo_Absent", gate.Detail, StringComparison.Ordinal);

        // AND THE ADMISSION IS STILL THERE.
        Assert.Contains("LATCH CLAIMS ADMITTED ON PROVENANCE", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("THIS GATE TAKES THE NAME, NOT THE FACT", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void And_a_report_with_NO_latch_claim_says_SO_rather_than_saying_nothing()
    {
        // An empty string here would be indistinguishable from the defect it replaces, so the no-claim
        // case is stated. That is also the answer to "could any input reach a branch that omits it?" - the
        // admission is appended unconditionally by the only caller and has no empty arm.
        var gate = Gate(Check(), "5 observability");

        Assert.Contains("No signal claims a latch", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_IT_DOES_NOT_MANUFACTURE_A_LATCH_FOR_THE_THIRTEEN_THAT_HAVE_NONE()
    {
        // *** WHAT THIS DOES NOT FIX, AS A TEST RATHER THAN A SENTENCE. *** Giving the translation a home
        // does not make the copy layer emit latches. A Latched expectation on a signal no block latches is
        // still refused, and that refusal was one of the thirteen CORRECT ones.
        var map = Bound(new[]
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

    // ---------------------------------------------------------------------------------------------
    // Gate 5c — the map's AUTHOR. Gate 5 fences the VECTOR author out structurally, by provenance, and
    // says nothing whatever about the BLOCK's author: a coordinator binding written by the party who
    // wrote the block decides both what the block does and what can be seen of it. Same three outcomes
    // as 3d and 4b, the two gates this is the fourth instance of.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A bindings-derived map, attributed to whoever is named — the only knob these tests turn.</summary>
    private static MirrorObservability MapDeclaredBy(string author) =>
        MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched }))
            with { Provenance = MapProvenance.Bindings, MapAuthor = new AgentIdentity(author) };

    [Fact]
    public void AN_UNRECORDED_MAP_AUTHOR_IS_NOT_CHECKED_BECAUSE_UNKNOWN_IS_NOT_INDEPENDENT()
    {
        var report = Check(map: MapDeclaredBy(string.Empty));
        var gate = Gate(report, "5c map authority");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Equal(NotCheckedReason.AwaitingAnArtifactThatCouldExist, gate.Reason);
        Assert.Contains("UNKNOWN IS NOT INDEPENDENT", gate.Detail, StringComparison.Ordinal);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);

        // 🔴 *** AND IT IS A SECOND FACT, NOT THE SAME SILENCE TWICE. *** The map IS derived from the
        // coordinator's bindings, so gate 5 ran and returned a verdict on this very submission — which is
        // the whole gap: a bindings-derived map is trusted on WHERE it came from and never on WHO wrote it.
        Assert.Equal(GateStatus.Checked, Gate(report, "5 observability").Status);
        Assert.True(Gate(report, "5 observability").Passed);
    }

    [Fact]
    public void THE_BLOCKS_AUTHOR_DECLARING_THE_OBSERVABILITY_MAP_IS_REFUSED()
    {
        // M-19's first limb. Every expectation any vector may state is admitted or refused against this
        // map, so the party under test would be bounding what it can be caught doing.
        var gate = Gate(Check(map: MapDeclaredBy("agent-a")), "5c map authority");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("THE PARTY UNDER TEST DECIDED WHAT CAN BE SEEN OF IT", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void SO_IS_A_VECTORS_AUTHOR_DECLARING_IT_and_the_provenance_test_cannot_see_that_at_all()
    {
        // Normalised, like D6 itself: a case-and-whitespace variant is not a different agent. And the map
        // is `Bindings`-derived here, so gate 5 passes it — this is the route its provenance test misses.
        var gate = Gate(Check(map: MapDeclaredBy("AGENT-B ")), "5c map authority");

        Assert.False(gate.Passed);
        Assert.Contains("V-1", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("provenance test cannot see this", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_CONTROL_a_THIRD_PARTY_MAP_PASSES_and_the_verdict_STATES_ITS_DENOMINATOR()
    {
        // 🔴 A gate that refuses everything passes every test that only checks refusals. This is the
        // other direction — and it asserts the DENOMINATOR the pass was reached over, because
        // `problems.Count == 0` is true of nothing found and of nothing looked at alike.
        var gate = Gate(Check(), "5c map authority");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
        Assert.Contains($"declared by '{Coordinator}'", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("compared against the block's author (agent-a) and 1 recorded vector author(s)", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void WITH_NOBODY_TO_COMPARE_AGAINST_IT_IS_NOT_CHECKED_rather_than_green_over_an_empty_set()
    {
        // *** THE DENOMINATOR, AS A BRANCH RATHER THAN AS A SENTENCE. *** Gate 2 refuses an unrecorded
        // block author independently, and this does not lean on that: a check whose correctness rests on
        // another gate's is a check that silently stops working when the other one moves.
        var report = Check(new[] { Vector(author: "   ") }, blockAuthor: "   ");
        var gate = Gate(report, "5c map authority");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains("NOTHING FOUND AND NOTHING LOOKED AT ARE NOT THE SAME RESULT", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_SELF_DECLARED_MAP_IS_NOT_CHECKED_HERE_TOO_and_says_it_is_waiting_on_the_SAME_artifact()
    {
        // The two silences have different repairs, and a reader who has just been told gate 5 could not
        // run needs to know whether this is the same fact or a second one. Here it is the same one.
        var selfDeclared = MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched }))
            with { Provenance = MapProvenance.SelfDeclared };

        var gate = Gate(Check(map: selfDeclared), "5c map authority");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains("gate 5 already refuses to be the deciding voice", gate.Detail, StringComparison.Ordinal);

        // *** THE OTHER DIRECTION OF THE MERGE SENTENCE BELOW. *** This silence has a different repair —
        // supply a coordinator binding — and telling this reader their lanes named different coordinators
        // would send them hunting a batch that does not exist. A message that appears everywhere explains
        // nothing anywhere.
        Assert.DoesNotContain("harness-batch plan", gate.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("owner-questions.md D2", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The symptom an agent actually meets is <i>"5c says NOT CHECKED and I supplied a binding"</i>, and
    /// the merged-batch cause is the one with no code left behind to explain itself — the plural
    /// <c>declaredBy</c> is RULED NOT BUILT (owner, 2026-08-24). So the refusal names it, names it as a
    /// DECISION, and points at where the decision is recorded. Without this the trail from the symptom to
    /// the ruling is archaeology through <c>BatchPlanner</c>.
    /// </summary>
    [Fact]
    public void THE_MERGED_BATCH_CAUSE_IS_NAMED_IN_THE_BINDINGS_SILENCE_as_a_RULED_omission_not_an_oversight()
    {
        var gate = Gate(Check(map: MapDeclaredBy(string.Empty)), "5c map authority");

        Assert.Equal(GateStatus.NotChecked, gate.Status);

        // The cause, and BOTH of its two forms — a reader told only about disagreement will not think to
        // look for the lane that simply said nothing.
        Assert.Contains("harness-batch plan", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("DIFFERENT coordinators OR when any single lane is silent", gate.Detail, StringComparison.Ordinal);

        // Ruled, not overlooked — and the record is cited by path, because "we decided not to" with no
        // pointer reads exactly like "nobody got to it".
        Assert.Contains("RULED NOT BUILT", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("docs/notes/owner-questions.md D2", gate.Detail, StringComparison.Ordinal);

        // And it names the surface that says WHICH of the two happened, so the reader is sent to the plan
        // rather than to the source.
        Assert.Contains("`authority` line", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_IT_IS_ABSENT_FROM_A_MAP_WHOSE_AUTHOR_IS_RECORDED_because_nothing_was_dropped_there()
    {
        // The control. A gate that prints the merge story on every submission has told nobody anything:
        // the sentence has to be a fact about THIS submission or it is decoration.
        var gate = Gate(Check(), "5c map authority");

        Assert.True(gate.Passed);
        Assert.DoesNotContain("RULED NOT BUILT", gate.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("harness-batch plan", gate.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 2.7 — the SUBMISSION-side join: WHERE a signal lives, not only how it is watched
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_DID_NOT_RUN_TEST_a_fully_joined_submission_lets_gates_8_and_8c_ACTUALLY_RUN()
    {
        // A join that refuses everything passes every test that only checks refusals, and that shape has
        // been caught here repeatedly. This is the control.
        var report = Check();

        Assert.Equal(GateStatus.Checked, Gate(report, "8s signal storage join").Status);
        Assert.True(Gate(report, "8s signal storage join").Passed);
        Assert.Equal(GateStatus.Checked, Gate(report, "8 blacklist").Status);
        Assert.Equal(GateStatus.Checked, Gate(report, "8c multi-writer").Status);
    }

    [Fact]
    public void WITH_NO_JOIN_AT_ALL_gates_8_and_8c_are_NOT_CHECKED_because_a_graph_is_about_STORAGE()
    {
        // *** MEASURED: 1 OF 17 SIGNALS RESOLVED ON A LIVE SUBMISSION, AND THAT ONE ONLY BECAUSE ITS SPEC
        // NAME AND BLOCK TAG HAPPEN TO BE THE SAME STRING. *** providedFor says HOW a signal is watched
        // and never WHERE it is.
        var report = Check(storage: SignalStorageMap.None);

        Assert.Equal(GateStatus.NotChecked, Gate(report, "8s signal storage join").Status);
        Assert.Equal(GateStatus.NotChecked, Gate(report, "8 blacklist").Status);
        Assert.Equal(GateStatus.NotChecked, Gate(report, "8c multi-writer").Status);
        Assert.Contains("says HOW a signal is watched and never WHERE it is", Gate(report, "8s signal storage join").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void HARNESS_ONLY_IS_A_POSITIVE_CLAIM_and_turns_a_NOT_CHECKED_into_a_COMPUTED_FACT()
    {
        // *** THE THIRD STATE IS THE ONE THAT MAKES THE OTHER TWO MEAN ANYTHING. *** The converter's own
        // unresolved reason is "this may be a mirror-only signal, or the name may be wrong" - two entirely
        // different repairs behind one silence. Without this state a legitimately mirror-only signal is
        // indistinguishable from a typo for ever.
        var mirrorOnly = SignalStorageMap.Of(Array.Empty<(string, SignalStorage)>(), new[] { "Demo_Count" });

        var report = Check(storage: mirrorOnly);
        var gate = Gate(report, "8s signal storage join");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
        Assert.Contains("POSITIVE CLAIM AND NOT AN OMISSION", gate.Detail, StringComparison.Ordinal);
        Assert.Equal(StorageJoin.HarnessOnly, mirrorOnly.Resolve("Demo_Count"));
    }

    [Fact]
    public void A_SIGNAL_IN_BOTH_IS_A_CONTRADICTION_AND_IS_REFUSED_naming_it()
    {
        var both = SignalStorageMap.Of(
            new[] { ("Demo_Count", new SignalStorage("DemoUnit", "Demo_Count")) },
            new[] { "Demo_Count" });

        Assert.Equal(StorageJoin.Contradiction, both.Resolve("Demo_Count"));

        var gate = Gate(Check(storage: both), "8s signal storage join");

        // CHECKED-and-failed, not NOT CHECKED: this was COMPARED and found wrong, which is a different
        // repair from a join nobody made.
        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("Demo_Count", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("cannot both occupy storage and occupy none", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_AMBIGUOUS_JOIN_IS_REFUSED_NAMING_EVERY_CANDIDATE_never_resolved_to_one()
    {
        // Picking a candidate is the aliasing that manufactured fictional multi-writers - two of the four
        // cross-block findings this project has ever recorded - not the fix.
        var ambiguous = SignalStorageMap.Of(new[]
        {
            ("Demo_Count", new SignalStorage("UDT_A", "IO.Step")),
            ("Demo_Count", new SignalStorage("UDT_B", "IO.Step")),
        });

        var ambiguity = Assert.Single(ambiguous.Ambiguities);
        Assert.Equal(2, ambiguity.Candidates.Count);
        Assert.Empty(ambiguous.Storage);

        var gate = Gate(Check(storage: ambiguous), "8s signal storage join");

        Assert.False(gate.Passed);
        Assert.Contains("UDT_A", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("UDT_B", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("GENUINELY DIFFERENT STORAGE", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void INSTANCE_ALIASES_OF_ONE_STORAGE_COLLAPSE_before_ambiguity_is_declared()
    {
        // Otherwise every signal declared twice identically would read as ambiguous, and the refusal would
        // stop meaning "genuinely different storage".
        var same = SignalStorageMap.Of(new[]
        {
            ("Demo_Count", new SignalStorage("DemoUnit", "Demo_Count")),
            ("Demo_Count", new SignalStorage("DemoUnit", "Demo_Count")),
        });

        Assert.Empty(same.Ambiguities);
        Assert.Equal(StorageJoin.InStorage, same.Resolve("Demo_Count"));
    }

    [Fact]
    public void OWNER_AND_PATH_ARE_TWO_KEYS_so_a_dotted_string_cannot_collide_with_a_qualified_one()
    {
        // "an emitted string is not a schema": joining them would make (owner A, path B.C) and
        // (owner null, path A.B.C) the same identity, which is exactly the parse this shape removes.
        var qualified = new SignalStorage("A", "B.C");
        var global = new SignalStorage(null, "A.B.C");

        Assert.NotEqual(qualified.Identity, global.Identity);
        Assert.True(global.IsGlobal);
        Assert.False(qualified.IsGlobal);
    }

    [Fact]
    public void NOTHING_RESOLVES_A_SIGNAL_BY_THE_SHAPE_OF_ITS_NAME()
    {
        // 🔴 The forbidden shortcut: find the storage whose PATH ENDS WITH the signal's name. It was built,
        // examined and declined - two of the four cross-block multi-writer findings this project has ever
        // recorded were fiction produced exactly that way. The declared join REPLACES name matching.
        var map = SignalStorageMap.Of(new[]
        {
            ("Demo_Count", new SignalStorage("DemoUnit", "Some.Path.Demo_Count")),
        });

        // The declared KEY resolves; the leaf of the PATH does not, however suggestive it looks.
        Assert.Equal(StorageJoin.InStorage, map.Resolve("Demo_Count"));
        Assert.Equal(StorageJoin.NotStated, map.Resolve("Some.Path.Demo_Count"));
        Assert.Equal(StorageJoin.NotStated, map.Resolve("Path.Demo_Count"));
    }

    // =============================================================================================
    // CROSS-FORM IDENTITY — the vacuous green, at every D6 gate (2026-08-24)
    //
    // 🔴 The defect: this repo carries TWO identity vocabularies at once. Role labels — `lad-coder`,
    // `vector-author-b-5.2`, `model-fidelity-declarer-1` — in every committed submission, and instance
    // labels `<session-id>/<agent-type>` from the claims convention. Exact string equality can never
    // make those collide, so a gate handed one of each reported "different parties" HAVING COMPARED
    // NOTHING. Each test below is paired with a control that must still return a real verdict: a guard
    // that silences the working comparisons has replaced one wrong answer with another.
    // =============================================================================================

    /// <summary>A session-id-shaped instance label, the form `converter claim --agent` already takes.</summary>
    private const string InstanceLabel = "session_015D8Gn9KXogXP6UxXZzHeFj/lad-coder";

    /// <summary>A SECOND instance label — same session, different agent type. Comparable with the first.</summary>
    private const string OtherInstanceLabel = "session_015D8Gn9KXogXP6UxXZzHeFj/assertion-enumerator";

    [Fact]
    public void GATE_5c_AN_INSTANCE_FORM_MAP_DECLARER_AGAINST_A_ROLE_FORM_BLOCK_AUTHOR_IS_NOT_CHECKED()
    {
        // 🔴 *** THE CASE THE WHOLE GUARD EXISTS FOR, AND IT WAS A GREEN. *** The binding comes out of a
        // session running the claims convention; the block author is `lad-coder`, as every committed
        // submission's is. Before this guard the gate compared the two literals, found them unequal and
        // PASSED — reporting that the party who decided what can be seen of the block is somebody else,
        // on the strength of a slash.
        var report = Check(map: MapDeclaredBy(InstanceLabel));
        var gate = Gate(report, "5c map authority");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Equal(NotCheckedReason.AwaitingAnArtifactThatCouldExist, gate.Reason);
        Assert.False(gate.Passed);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);

        // The text has to tell a reader WHICH is which, that it is a ruling and not a bug, and what the
        // repair is. Asserted rather than trusted, because that is the whole value of a NOT CHECKED.
        Assert.Contains("an INSTANCE label (<session-id>/<agent-type>)", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("a ROLE label", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("This is a RULING and not a bug", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("committed artifacts are NOT retrofitted", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("test-environment-contract.md", gate.Detail, StringComparison.Ordinal);

        // *** THE DENOMINATOR. *** 2 recorded authors here: the block's, and V-1's.
        Assert.Contains("2 of 2 recorded author(s)", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void GATE_5c_THE_CONTROL_TWO_INSTANCE_LABELS_STILL_GET_A_REAL_VERDICT()
    {
        // Both sides in the SAME vocabulary, and different parties: this must still PASS. A guard that
        // sent every instance label to NOT CHECKED would make the convention useless on arrival.
        var vectors = new[] { Vector(author: OtherInstanceLabel) };
        var gate = Gate(Check(vectors, blockAuthor: InstanceLabel, map: MapDeclaredBy(Coordinator + "/coordinator")), "5c map authority");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
    }

    [Fact]
    public void GATE_5c_THE_OTHER_CONTROL_TWO_MATCHING_INSTANCE_LABELS_ARE_STILL_REFUSED()
    {
        // Same vocabulary, SAME party — the collision the gate exists to catch, now expressed in the new
        // form. NOT CHECKED must not have swallowed the refusal path.
        var vectors = new[] { Vector(author: OtherInstanceLabel) };
        var gate = Gate(Check(vectors, blockAuthor: InstanceLabel, map: MapDeclaredBy(InstanceLabel)), "5c map authority");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("THE PARTY UNDER TEST DECIDED WHAT CAN BE SEEN OF IT", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void GATE_2_A_ROLE_BLOCK_AUTHOR_AGAINST_INSTANCE_VECTOR_AUTHORS_IS_NOT_CHECKED_naming_the_denominator()
    {
        // One of the two vectors is in the other vocabulary. That is enough: a verdict on the comparable
        // half, silent about the rest, is the partially-attributed shape 3d already refuses.
        var vectors = new[] { Vector(), Vector(id: "V-2", author: InstanceLabel) };
        var gate = Gate(Check(vectors), "2 authorship");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Equal(NotCheckedReason.AwaitingAnArtifactThatCouldExist, gate.Reason);
        Assert.Contains("1 of 2 recorded vector author(s) (V-2)", gate.Detail, StringComparison.Ordinal);

        // V-1 was comparable and is NOT named as a problem — the message says which vector is in the
        // other vocabulary, so the reader is not sent to re-stamp a file that was already correct.
        Assert.DoesNotContain("V-1", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void GATE_2_THE_CONTROL_two_ROLE_labels_that_DIFFER_still_PASS_and_two_that_MATCH_are_still_REFUSED()
    {
        // 🔴 Both directions in one test, because the pair is the assertion: the guard must not silence a
        // comparison that was working, in EITHER of its two outcomes.
        var differ = Gate(Check(), "2 authorship");
        Assert.Equal(GateStatus.Checked, differ.Status);
        Assert.True(differ.Passed);

        var collide = Gate(Check(new[] { Vector(author: "agent-a") }), "2 authorship");
        Assert.Equal(GateStatus.Checked, collide.Status);
        Assert.False(collide.Passed);
        Assert.Contains("wrote both the block and the vector", collide.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void GATE_4b_AN_INSTANCE_FORM_FIDELITY_DECLARER_AGAINST_ROLE_AUTHORS_IS_NOT_CHECKED()
    {
        var gate = Gate(Check(fidelityDeclaredBy: InstanceLabel), "4b fidelity authority");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Equal(NotCheckedReason.AwaitingAnArtifactThatCouldExist, gate.Reason);
        Assert.Contains("model 'M_Ramp's declarer", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("2 of 2 recorded author(s)", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void GATE_4b_THE_CONTROL_a_ROLE_declarer_still_PASSES_and_a_COLLIDING_one_is_still_REFUSED()
    {
        var third = Gate(Check(), "4b fidelity authority");
        Assert.Equal(GateStatus.Checked, third.Status);
        Assert.True(third.Passed);

        var collide = Gate(Check(fidelityDeclaredBy: "agent-a"), "4b fidelity authority");
        Assert.Equal(GateStatus.Checked, collide.Status);
        Assert.False(collide.Passed);
        Assert.Contains("both wrote the block and declared what model", collide.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void GATE_3d_AN_INSTANCE_FORM_ENUMERATOR_AGAINST_ROLE_AUTHORS_IS_NOT_CHECKED()
    {
        var gate = Gate(Check(enumeratedBy: InstanceLabel), "3d enumerator independence");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Equal(NotCheckedReason.AwaitingAnArtifactThatCouldExist, gate.Reason);
        Assert.Contains("the enumeration's enumerator", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("2 of 2 recorded author(s)", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void GATE_3d_THE_CONTROL_a_ROLE_enumerator_still_PASSES_and_a_COLLIDING_one_is_still_REFUSED()
    {
        var third = Gate(Check(), "3d enumerator independence");
        Assert.Equal(GateStatus.Checked, third.Status);
        Assert.True(third.Passed);

        var collide = Gate(Check(enumeratedBy: "agent-a"), "3d enumerator independence");
        Assert.Equal(GateStatus.Checked, collide.Status);
        Assert.False(collide.Passed);
        Assert.Contains("both wrote the block and enumerated its assertions", collide.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_INDETERMINATE_LABEL_IS_NOT_CHECKED_TOO_and_it_says_NEITHER_vocabulary()
    {
        // A doubled separator fits neither vocabulary. It is not rounded into the nearer one — "both
        // unclassifiable" is not evidence that two strings are the same kind of thing.
        var gate = Gate(Check(map: MapDeclaredBy("session//lad-coder")), "5c map authority");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains("in NEITHER vocabulary", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void EVERY_CROSS_FORM_NOT_CHECKED_IS_CLOSABLE_OFFLINE_and_none_is_a_design_property()
    {
        // The claim `NotCheckedReason` makes about itself: no NOT CHECKED in this system is a property of
        // the design. Each of these closes the moment both sides are produced under the convention.
        var gates = new[]
        {
            Gate(Check(map: MapDeclaredBy(InstanceLabel)), "5c map authority"),
            Gate(Check(new[] { Vector(author: InstanceLabel) }), "2 authorship"),
            Gate(Check(fidelityDeclaredBy: InstanceLabel), "4b fidelity authority"),
            Gate(Check(enumeratedBy: InstanceLabel), "3d enumerator independence"),
        };

        Assert.Equal(4, gates.Length);
        Assert.All(gates, g => Assert.True(g.IsClosableOffline, $"{g.Gate} is not closable offline"));
        Assert.All(gates, g => Assert.Contains("Unknown is not independent.", g.Detail, StringComparison.Ordinal));
    }
}
