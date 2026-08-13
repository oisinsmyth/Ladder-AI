using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// The admissibility gates — basis, fidelity, settling, authorship, observability.
///
/// <para><b>Every gate fails closed and none has a relaxing flag</b>, per the contract's §10. The
/// precedent is phase 2's <c>AddressesExamined</c>: a rule that was correct while nothing established
/// that it had run.</para>
/// </summary>
public class AdmissibilityTests
{
    private static readonly AssertionEnumeration Enumeration =
        AssertionEnumeration.Of(new[] { "REQ-14" }, new[] { "REQ-14.a", "REQ-14.b" });

    private static Admissibility Check(
        Basis? basis = null,
        AssertionEnumeration? enumeration = null,
        FidelityDeclaration? fidelity = null,
        IReadOnlyCollection<string>? behaviours = null,
        SettlingDeclaration? settling = null,
        string completion = "Demo_Done",
        string vectorAuthor = "agent-b",
        string blockAuthor = "agent-a",
        bool observabilitySupported = true,
        bool omitFidelity = false,
        bool omitSettling = false,
        bool omitObservability = false) =>
        Admissibility.Check(
            basis ?? new Basis("REQ-14", "REQ-14.a"),
            enumeration ?? Enumeration,
            omitFidelity ? null : fidelity ?? FidelityDeclaration.Of("M", new[] { "fill" }, new[] { "lag" }, true),
            behaviours ?? new[] { "fill" },
            omitSettling ? null : settling ?? new SettlingDeclaration("unchanged across 3 scans", new[] { "Demo_Count" }),
            completion, new AgentIdentity(vectorAuthor), new AgentIdentity(blockAuthor),
            omitObservability ? null : observabilitySupported ? Supportable : Unsupportable);

    /// <summary>
    /// REAL computed reports, not fabricated verdicts. There is no way to construct an
    /// ObservabilityReport except by running the checker, which is the point of the change: the test
    /// cannot assert the answer any more than a caller can.
    /// </summary>
    private static readonly ObservabilityReport Supportable = ObservabilityCheck.Evaluate(
        new[] { new ObservabilityDeclaration("Sig", SignalNature.PersistentState, InstrumentationMode.Latched, 0) },
        AssertionForm.When,
        MirrorObservability.Of(("Sig", new[] { InstrumentationMode.Latched })), 9, 1, 1);

    private static readonly ObservabilityReport Unsupportable = ObservabilityCheck.Evaluate(
        new[] { new ObservabilityDeclaration("Sig", SignalNature.Transient, InstrumentationMode.Sampled, 40) },
        AssertionForm.When,
        MirrorObservability.Of(("Sig", new[] { InstrumentationMode.Sampled })), 9, 1, 1);

    [Fact]
    public void A_complete_declaration_is_admissible()
    {
        Assert.True(Check().Admissible);
    }

    // ---------------------------------------------------------------------------------------------
    // Basis — the clause AND the assertion, and why both
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_clause_with_no_assertion_is_refused_because_a_clause_alone_lets_a_misreading_be_reused()
    {
        var refusal = Assert.Single(Check(basis: new Basis("REQ-14", "")).Refusals);

        Assert.Equal(RefusalReason.BasisNotCited, refusal.Reason);
        Assert.Contains("correlated check", refusal.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_assertion_the_enumeration_does_not_contain_is_an_error_in_the_VECTOR()
    {
        // Section 7: a vector CITES into the spec-derived enumeration; it cannot extend it. An author who
        // WRITES an assertion rather than citing one has re-created the correlated check with extra steps.
        var refusal = Assert.Single(Check(basis: new Basis("REQ-14", "REQ-14.invented")).Refusals);

        Assert.Equal(RefusalReason.AssertionNotEnumerated, refusal.Reason);
        Assert.Contains("never an extension of the denominator", refusal.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_clause_that_resolves_to_nothing_written_is_refused()
    {
        Assert.Contains(Check(basis: new Basis("REQ-99", "REQ-14.a")).Refusals,
            r => r.Reason == RefusalReason.ClauseNotEnumerated);
    }

    [Fact]
    public void AN_EMPTY_ENUMERATION_IS_A_REFUSAL_NOT_A_PERMISSIVE_CHECK()
    {
        // FI-44 in its purest form: checking a citation against nothing admits everything while reading
        // exactly like a check that ran. Both halves are covered — no clauses and no assertions.
        Assert.Contains(Check(enumeration: AssertionEnumeration.Of(Array.Empty<string>(), Array.Empty<string>())).Refusals,
            r => r.Reason == RefusalReason.EnumerationEmpty);

        Assert.Contains(Check(enumeration: AssertionEnumeration.Of(new[] { "REQ-14" }, Array.Empty<string>())).Refusals,
            r => r.Reason == RefusalReason.EnumerationEmpty);

        Assert.Contains(Check(enumeration: AssertionEnumeration.Of(Array.Empty<string>(), new[] { "REQ-14.a" })).Refusals,
            r => r.Reason == RefusalReason.EnumerationEmpty);
    }

    // ---------------------------------------------------------------------------------------------
    // Fidelity (M4) — a set difference, per the instruction
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_vector_asserting_a_behaviour_the_model_does_not_claim_is_refused_and_BOTH_SETS_are_named()
    {
        var refusal = Assert.Single(Check(
            fidelity: FidelityDeclaration.Of("M_Ideal", new[] { "fill" }, new[] { "in-flight-mass" }, true),
            behaviours: new[] { "fill", "in-flight-mass" }).Refusals);

        Assert.Equal(RefusalReason.FidelityExceeded, refusal.Reason);
        Assert.Contains("in-flight-mass", refusal.Detail, StringComparison.Ordinal);
        Assert.Contains("M_Ideal", refusal.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_model_claiming_to_represent_NOTHING_is_unusable_rather_than_permissive()
    {
        Assert.Contains(Check(fidelity: FidelityDeclaration.Of("M_Empty", Array.Empty<string>())).Refusals,
            r => r.Reason == RefusalReason.FidelityUnusable);
    }

    [Fact]
    public void A_model_that_both_claims_and_disclaims_a_behaviour_says_nothing()
    {
        var refusal = Assert.Single(Check(
            fidelity: FidelityDeclaration.Of("M_Confused", new[] { "fill", "lag" }, new[] { "lag" }, true)).Refusals);

        Assert.Equal(RefusalReason.FidelityUnusable, refusal.Reason);
        Assert.Contains("both claims and disclaims", refusal.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_fidelity_declaration_is_refused_because_a_green_against_an_undeclared_model_cannot_be_read()
    {
        Assert.Contains(Check(omitFidelity: true).Refusals, r => r.Reason == RefusalReason.FidelityUnusable);
    }

    // ---------------------------------------------------------------------------------------------
    // Settling — a completion flag is NOT a settling signal
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_settling_condition_that_is_only_the_completion_flag_is_refused()
    {
        // Phase 2's finding, mechanised: the defective build raised Done at 10 and went on ramping to 15.
        var refusal = Assert.Single(Check(
            settling: new SettlingDeclaration("Demo_Done is high", new[] { "Demo_Done" })).Refusals);

        Assert.Equal(RefusalReason.SettlingIsTheCompletionFlag, refusal.Reason);
        Assert.Contains("ramping to 15", refusal.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_settling_condition_naming_the_completion_flag_AND_something_else_is_allowed()
    {
        // The rule is "not the completion flag ALONE". Naming it alongside a real settling signal is a
        // legitimate condition and refusing it would push authors toward vaguer declarations.
        Assert.True(Check(settling: new SettlingDeclaration(
            "Demo_Done high AND count unchanged for 3 scans", new[] { "Demo_Done", "Demo_Count" })).Admissible);
    }

    [Fact]
    public void No_settling_declaration_at_all_is_refused()
    {
        Assert.Contains(Check(omitSettling: true).Refusals, r => r.Reason == RefusalReason.SettlingNotDeclared);
    }

    // ---------------------------------------------------------------------------------------------
    // Authorship (D6) and observability
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void One_agent_writing_both_the_block_and_the_vector_is_REFUSED_not_warned()
    {
        var refusal = Assert.Single(Check(vectorAuthor: "agent-a", blockAuthor: "agent-a").Refusals);

        Assert.Equal(RefusalReason.AuthorshipCorrelated, refusal.Reason);
    }

    [Fact]
    public void UNRECORDED_authorship_is_refused_because_unknown_is_not_independent()
    {
        Assert.Contains(Check(vectorAuthor: "").Refusals, r => r.Reason == RefusalReason.AuthorshipCorrelated);
        Assert.Contains(Check(blockAuthor: "  ").Refusals, r => r.Reason == RefusalReason.AuthorshipCorrelated);
    }

    [Fact]
    public void An_unobservable_vector_is_refused_because_a_green_that_cannot_mean_anything_is_worse()
    {
        Assert.Contains(Check(observabilitySupported: false).Refusals, r => r.Reason == RefusalReason.Unobservable);
    }

    [Fact]
    public void AN_OBSERVABILITY_EVALUATION_THAT_NEVER_RAN_IS_REFUSED_NOT_PASSED()
    {
        // *** FOUND BY MUTATION, AND IT WAS A HOLE. *** The gate was made a computation, and the case
        // where the computation DID NOT HAPPEN had no test: neutering the null branch left the whole
        // suite green. A gate that did not run is not a gate that passed, and null is exactly how "did
        // not run" arrives here.
        var refusal = Assert.Single(Check(omitObservability: true).Refusals);

        Assert.Equal(RefusalReason.Unobservable, refusal.Reason);
        Assert.Contains("no observability evaluation was performed", refusal.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_refusing_observability_report_carries_EVERY_finding_into_the_refusals()
    {
        // Not just "unobservable" — the computed reason per signal, so the author is told which
        // expectation and why rather than that something somewhere could not be seen.
        var refusal = Assert.Single(Check(observabilitySupported: false).Refusals);

        Assert.Contains("ModeCannotAnswerThisNature", refusal.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Every failure is reported, not the first
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_submission_wrong_in_several_ways_is_told_ALL_of_them()
    {
        var refusals = Check(
            basis: new Basis("REQ-99", "REQ-14.invented"),
            fidelity: FidelityDeclaration.Of("M", new[] { "fill" }, validatedAgainstPlantData: false),
            behaviours: new[] { "overshoot" },
            vectorAuthor: "agent-a",
            omitSettling: true).Refusals;

        Assert.Contains(refusals, r => r.Reason == RefusalReason.ClauseNotEnumerated);
        Assert.Contains(refusals, r => r.Reason == RefusalReason.AssertionNotEnumerated);
        Assert.Contains(refusals, r => r.Reason == RefusalReason.FidelityExceeded);
        Assert.Contains(refusals, r => r.Reason == RefusalReason.SettlingNotDeclared);
        Assert.Contains(refusals, r => r.Reason == RefusalReason.AuthorshipCorrelated);
        Assert.True(refusals.Count >= 5, "one round trip to the author should carry every reason, not the first.");
    }

    [Fact]
    public void There_is_no_flag_that_relaxes_a_gate()
    {
        // Stated as a property of the signature. A gate with a skip flag is a gate that will be skipped
        // at 2 a.m., and the way to keep that true is for there to be nothing to pass.
        var parameters = typeof(Admissibility)
            .GetMethod(nameof(Admissibility.Check))!
            .GetParameters()
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain(parameters, p => p!.Contains("skip", StringComparison.OrdinalIgnoreCase)
                                            || p!.Contains("ignore", StringComparison.OrdinalIgnoreCase)
                                            || p!.Contains("force", StringComparison.OrdinalIgnoreCase)
                                            || p!.Contains("allow", StringComparison.OrdinalIgnoreCase));
    }
}
