namespace Harness.Results;

/// <summary>
/// Where an expectation came from: the requirement clause AND the assertion within it.
///
/// <para><b>Both, and the second is the load-bearing one.</b> A vector that cannot cite its clause is
/// not admissible (D6); <b>a vector that cites only a clause is admissible and nearly worthless.</b>
/// The reason is the autopsy this pipeline was rebuilt around — an AI reviewer reading the same
/// register as the AI coder is a CORRELATED check, and both failed together on an ambiguous <c>/</c>.
/// Using a different agent does not decorrelate them if both read the same sentence and are free to
/// read it the same way.</para>
///
/// <para>The clause says where the requirement came from. <b>The assertion says what would be observed
/// if the block were correct</b> — and that is the decorrelating half, because two different readings
/// of one clause produce two VISIBLY DIFFERENT assertions instead of two green results.</para>
/// </summary>
public sealed record Basis(string ClauseId, string AssertionId);

/// <summary>
/// The spec-derived assertion enumeration a <see cref="Basis"/> must cite INTO.
///
/// <para><b>Spec-derived, never vector-derived, and §7 calls the invariant absolute.</b> Any unit
/// defined by what somebody wrote a vector for is self-referential — you cannot be missing an assertion
/// nobody wrote — so coverage is always 100% and the gate is theatre. A vector CITES into this; it
/// cannot extend it, and a citation the enumeration does not contain is an error in the VECTOR.</para>
///
/// <para><b>An EMPTY enumeration is a refusal, not a permissive one.</b> Checking a citation against
/// nothing admits everything while reading exactly like a check that ran (FI-44).</para>
/// </summary>
public sealed record AssertionEnumeration(IReadOnlySet<string> Clauses, IReadOnlySet<string> Assertions)
{
    public bool IsEmpty => Assertions.Count == 0 || Clauses.Count == 0;

    public static AssertionEnumeration Of(IEnumerable<string> clauses, IEnumerable<string> assertions) =>
        new(clauses.ToHashSet(StringComparer.Ordinal), assertions.ToHashSet(StringComparer.Ordinal));
}

/// <summary>What a model claims to represent, and what it explicitly does not (M3).</summary>
/// <param name="Represents">Behaviours the model claims. A vector may assert only these (M4).</param>
/// <param name="DoesNotRepresent">
/// Named absences — noise, lag, overshoot, in-flight mass, saturation, quantisation. <b>Recorded rather
/// than left silent</b>, so a green is never read without its caveats.
/// </param>
/// <param name="ValidatedAgainstPlantData">
/// M5: where no real plant data exists, that ABSENCE is part of the declaration. It is a fact about the
/// result's strength, not a footnote.
/// </param>
public sealed record FidelityDeclaration(
    string ModelId,
    IReadOnlySet<string> Represents,
    IReadOnlySet<string> DoesNotRepresent,
    bool ValidatedAgainstPlantData)
{
    public static FidelityDeclaration Of(string modelId, IEnumerable<string> represents,
        IEnumerable<string>? doesNotRepresent = null, bool validatedAgainstPlantData = false) =>
        new(modelId, represents.ToHashSet(StringComparer.Ordinal),
            (doesNotRepresent ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal),
            validatedAgainstPlantData);
}

/// <summary>
/// When a value counts as FINAL — and it is not the block's completion flag.
///
/// <para><b>Phase 2's finding, and the sharpest thing in the contract.</b> The deliberately defective
/// build raised <c>Done</c> at 10 and then went on ramping to 15, so a faster-than-floor poll reads
/// mid-ramp and returns a CONFIDENTLY WRONG verdict — not a missed observation, an observed one that is
/// wrong. The block's own opinion of its progress is a claim under test, not evidence about the
/// observation.</para>
/// </summary>
/// <param name="Condition">Prose: what makes the value final.</param>
/// <param name="Signals">The signals the condition names. Checked against the completion signal.</param>
public sealed record SettlingDeclaration(string Condition, IReadOnlyList<string> Signals);

/// <summary>Why a vector was inadmissible. Each is a different thing for the author to do.</summary>
public enum RefusalReason
{
    /// <summary>The clause or the assertion was not cited at all.</summary>
    BasisNotCited,

    /// <summary>The clause is not in the spec-derived enumeration, so it resolves to nothing written.</summary>
    ClauseNotEnumerated,

    /// <summary>The assertion is not in the spec-derived enumeration. An error in the VECTOR, never an extension of the denominator.</summary>
    AssertionNotEnumerated,

    /// <summary>The enumeration itself was empty, so the citation was checked against nothing.</summary>
    EnumerationEmpty,

    /// <summary>The vector asserts a behaviour the model does not claim to represent (M4).</summary>
    FidelityExceeded,

    /// <summary>The model claims to represent nothing, or claims and disclaims the same behaviour.</summary>
    FidelityUnusable,

    /// <summary>The vector's author is the block's author (D6) — the correlated check this pipeline exists to prevent.</summary>
    AuthorshipCorrelated,

    /// <summary>The settling condition is the completion flag re-cited, which is not a settling condition.</summary>
    SettlingIsTheCompletionFlag,

    /// <summary>No settling condition was declared at all.</summary>
    SettlingNotDeclared,

    /// <summary>The transport cannot support the declared observability, so a green could not mean anything.</summary>
    Unobservable,

    /// <summary>There was nothing to judge: no assertions, or no evidence. Empty is not clean.</summary>
    NothingExamined,
}

/// <summary>
/// The admissibility gates, run before a result is allowed to mean anything.
///
/// <para><b>Every gate fails closed and none has a relaxing flag.</b> The precedent is phase 2's
/// <c>AddressesExamined</c>: a rule that was correct while nothing established that it had run. A gate
/// with a skip flag is a gate that will be skipped at 2 a.m.</para>
/// </summary>
public sealed record Admissibility(IReadOnlyList<(RefusalReason Reason, string Detail)> Refusals)
{
    public bool Admissible => Refusals.Count == 0;

    /// <summary>Run every gate. Order is the contract's own (§10), and every failure is reported, not the first.</summary>
    public static Admissibility Check(
        Basis? basis,
        AssertionEnumeration enumeration,
        FidelityDeclaration? fidelity,
        IReadOnlyCollection<string> assertedBehaviours,
        SettlingDeclaration? settling,
        string completionSignal,
        AgentIdentity vectorAuthor,
        AgentIdentity blockAuthor,
        ObservabilityReport? observability)
    {
        ArgumentNullException.ThrowIfNull(enumeration);
        ArgumentNullException.ThrowIfNull(assertedBehaviours);

        var refusals = new List<(RefusalReason, string)>();

        // --- basis ---------------------------------------------------------------------------------
        if (basis is null || string.IsNullOrWhiteSpace(basis.ClauseId) || string.IsNullOrWhiteSpace(basis.AssertionId))
        {
            refusals.Add((RefusalReason.BasisNotCited,
                "a vector must cite BOTH a requirement clause and an assertion within it. A clause alone lets an ambiguous reading be silently reused, which is the correlated check this pipeline exists to prevent."));
        }
        else if (enumeration.IsEmpty)
        {
            refusals.Add((RefusalReason.EnumerationEmpty,
                $"the spec-derived enumeration holds {enumeration.Clauses.Count} clause(s) and {enumeration.Assertions.Count} assertion(s), so the citation was checked against nothing. An empty denominator admits everything while reading exactly like a check that ran."));
        }
        else
        {
            if (!enumeration.Clauses.Contains(basis.ClauseId))
                refusals.Add((RefusalReason.ClauseNotEnumerated, $"clause '{basis.ClauseId}' resolves to nothing written."));

            if (!enumeration.Assertions.Contains(basis.AssertionId))
            {
                refusals.Add((RefusalReason.AssertionNotEnumerated,
                    $"assertion '{basis.AssertionId}' is not in the spec-derived enumeration. That is an error in the VECTOR, never an extension of the denominator (section 7): an author who WRITES an assertion rather than CITING one has re-created the correlated check with extra steps."));
            }
        }

        // --- fidelity (M4), as a set difference ----------------------------------------------------
        if (fidelity is null)
        {
            refusals.Add((RefusalReason.FidelityUnusable,
                "no model fidelity declaration. A green against an undeclared model cannot be read at all: M3 has the declaration travel with every result so it is never read without its caveats."));
        }
        else if (fidelity.Represents.Count == 0)
        {
            refusals.Add((RefusalReason.FidelityUnusable,
                $"model '{fidelity.ModelId}' claims to represent nothing, so every assertion exceeds it and none can be judged."));
        }
        else
        {
            var contradictory = fidelity.Represents.Intersect(fidelity.DoesNotRepresent, StringComparer.Ordinal).ToArray();
            if (contradictory.Length > 0)
            {
                refusals.Add((RefusalReason.FidelityUnusable,
                    $"model '{fidelity.ModelId}' both claims and disclaims: {string.Join(", ", contradictory)}. A declaration that says two things says nothing."));
            }

            var beyond = assertedBehaviours.Where(b => !fidelity.Represents.Contains(b)).ToArray();
            if (beyond.Length > 0)
            {
                refusals.Add((RefusalReason.FidelityExceeded,
                    $"this vector asserts {string.Join(", ", beyond)}, which model '{fidelity.ModelId}' does not claim to represent. M4 refuses rather than returning a result whose meaning depends on a behaviour nobody modelled."));
            }
        }

        if (assertedBehaviours.Count == 0)
        {
            refusals.Add((RefusalReason.NothingExamined,
                "this vector asserts nothing. A vector with no assertion cannot fail, so its pass says nothing at all."));
        }

        // --- settling ------------------------------------------------------------------------------
        if (settling is null || string.IsNullOrWhiteSpace(settling.Condition))
        {
            refusals.Add((RefusalReason.SettlingNotDeclared,
                "no settling condition. Without one, an observation taken while the value is still moving is compared and believed — a confidently wrong verdict rather than a missed one."));
        }
        else if (settling.Signals.Count > 0
                 && settling.Signals.All(s => string.Equals(s, completionSignal, StringComparison.Ordinal)))
        {
            refusals.Add((RefusalReason.SettlingIsTheCompletionFlag,
                $"the settling condition names only '{completionSignal}', which is the block's own completion flag. A completion flag is NOT a settling signal: phase 2's defective build raised Done at 10 and went on ramping to 15."));
        }

        // --- authorship (D6) -----------------------------------------------------------------------
        if (!vectorAuthor.IsRecorded || !blockAuthor.IsRecorded)
        {
            refusals.Add((RefusalReason.AuthorshipCorrelated,
                "authorship is not recorded on both sides, so D6's independence cannot be established. Unknown is not independent."));
        }
        else if (vectorAuthor.SameAs(blockAuthor))
        {
            refusals.Add((RefusalReason.AuthorshipCorrelated,
                $"'{vectorAuthor}' wrote both the block and the vector. That is a correlated check, and it is a refusal rather than a warning. (The comparison is NORMALISED - trimmed and case-folded - which is strictly tighter than the ordinal one it replaces, and closes the variants a keystroke would otherwise defeat.)"));
        }

        // --- observability -------------------------------------------------------------------------
        // *** IT IS A COMPUTATION AND NO LONGER A PARAMETER. *** This used to take `bool
        // observabilitySupported`, which made the gate the contract leans on hardest an ARGUMENT: a
        // caller could assert the answer. An ObservabilityReport can only be produced by
        // ObservabilityCheck.Evaluate against the map and the floor, so passing one is passing evidence
        // rather than a verdict; passing NULL is "the check did not run", which fails closed.
        if (observability is null)
        {
            refusals.Add((RefusalReason.Unobservable,
                "no observability evaluation was performed, so nothing established that this vector's declared observability can be supported. A gate that did not run is not a gate that passed."));
        }
        else if (!observability.Supported)
        {
            foreach (var finding in observability.Refusals)
            {
                refusals.Add((RefusalReason.Unobservable,
                    $"{finding.Signal}: {finding.Outcome} - {finding.Detail} A green that cannot mean anything is worse than a refusal."));
            }
        }

        return new Admissibility(refusals);
    }
}
