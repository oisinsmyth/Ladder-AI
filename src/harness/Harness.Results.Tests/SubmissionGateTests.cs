using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// The gate runner: which gates are CHECKED, which are JUDGEMENT, and which are NOT CHECKED — and
/// that the three are never collapsed into two.
/// </summary>
public class SubmissionGateTests
{
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
        SettlingDeclaration? settling = null,
        IReadOnlyList<BlacklistEntry>? blacklist = null) =>
        new(id, slot, 0, new AgentIdentity(author),
            basis ?? new Basis("REQ-014", "REQ-014:3f9a1c"),
            new Dictionary<string, string> { ["Demo_Step"] = "5" },
            startBool,
            expectations ?? new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 0) },
            settling ?? new SettlingDeclaration("count unchanged across 3 scans", new[] { "Demo_Count" }),
            maxDuration,
            blacklist ?? new[] { new BlacklistEntry("FC_Other", "shares the plant model instance") },
            comp,
            new[] { "ramp-to-limit" },
            "Demo_Done",
            kills);

    private static SubmissionReport Check(
        IReadOnlyList<SubmissionVector>? vectors = null,
        string blockAuthor = "agent-a",
        MirrorObservability? map = null,
        IReadOnlySet<string>? conflicts = null,
        double floor = 9,
        int runtimeCompression = 1,
        bool omitConflictGraph = false) =>
        SubmissionGate.Check(
            vectors ?? new[] { Vector() },
            AssertionEnumeration.Of(new[] { "REQ-014" }, new[] { "REQ-014:3f9a1c" }),
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true),
            new AgentIdentity(blockAuthor),
            map ?? MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })),
            floor, runtimeCompression,
            omitConflictGraph ? null : conflicts ?? new HashSet<string>());

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
        var blacklist = Assert.Single(report.NotChecked);
        Assert.StartsWith("8 blacklist", blacklist.Gate, StringComparison.Ordinal);
        Assert.Contains("a blacklist nobody checked", blacklist.Detail, StringComparison.Ordinal);
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
        Assert.Equal(GateStatus.Checked, Gate(Check(conflicts: new HashSet<string>()), "8 blacklist").Status);
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
}
