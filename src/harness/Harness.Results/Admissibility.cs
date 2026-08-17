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
/// <param name="Forms">
/// The canonical FORM of each assertion, where the enumeration carries it. <b>This is what gives F-3 an
/// authority</b>: the form decides whether a SAMPLED observation is admissible, and while it was
/// declared by the VECTOR the refusal was enforced against what a vector CLAIMED. An enumeration that
/// carries forms lets the gate compare the two and refuse the mismatch. <b>Empty is the flat projection
/// the gate has always read</b> — legal, and the gate then says the check could not be made rather than
/// that it passed.
/// </param>
/// <param name="Enumerator">
/// Who performed the decomposition. Recorded so independence can be CHECKED rather than assumed: the
/// enumeration is the coverage denominator, and if the block's author produced it, D6's independence is
/// lost at the denominator - which undoes most of what citing into it buys.
/// </param>
/// <param name="NormalisedTexts">
/// Assertion ID to the <c>normalised_text</c> its ID was computed from (§3.4).
///
/// <para><b>THIS IS WHAT KEEPS THE STAMPER UNTRUSTED.</b> The enumerator cannot compute a SHA-256 — it is
/// denied <c>Bash</c> deliberately — so a separate stamping step writes the IDs in. That step needs no
/// independence from the block or vector author <i>only because the gate recomputes every ID from this
/// text and refuses a mismatch</i>. Carrying the texts is not decoration: <b>if this is empty the
/// recomputation cannot run, and the stamper silently becomes an authority it was never designed to
/// be.</b> The gate therefore reports NOT CHECKED rather than passing.</para>
/// </param>
/// <param name="RequiredObservations">
/// Assertion ID to <b>every signal a citation of it depends on</b> — the enumeration's
/// <c>response_signal:</c> AND its <c>also_requires_observation_of:</c>, merged into one set.
///
/// <para><b>AMB-14, and it is a SCHEMA gap rather than a spec one.</b> The mechanical check was "the
/// response signal appears in <c>Expectations</c>", and a response signal was a SINGLE STRING. <b>A
/// simultaneity claim has two signals and both must be observed for the citation to mean anything</b> —
/// so a vector could cite a relational assertion, expect on one output alone, never observe the other,
/// and the check would pass while nothing about the RELATION was tested.</para>
///
/// <para><b>It is a set here and not a second scalar</b> because the next relational assertion may name
/// three. And it is carried in a FIELD rather than fixed by editing the assertion's text: no hashed
/// sentence in this format contains a signal name or a numeric bound, which is what has made four
/// re-issues cost zero re-hashes when an output was renamed. Naming both signals in the text to close
/// this would give that property away to fix something that lives in a field.</para>
/// </param>
/// <param name="Bounds">
/// The enumeration's <c>bounds:</c> table — the SPECIFIED values that clauses refer to BY NAME rather
/// than by number.
///
/// <para><b>AMB-19, and it is the exact price of the property described just above.</b> Keeping numbers
/// out of the hashed text is what has made four re-issues cost zero re-hashes; the same choice means
/// <b>retuning this table changes what many assertions are TRUE OF while moving no assertion ID at
/// all</b>. A vector written against the old value then goes on passing — nothing dangles, nothing
/// reads Stale — because staleness keys on assertion IDs and not on bound values.</para>
///
/// <para><b>So the table is carried here to be COMPARED against what a vector says it used</b>
/// (<see cref="BoundsCurrencyCheck"/>). Absent means the comparison was made against nothing, which the
/// gate reports as NOT CHECKED and never as agreement.</para>
/// </param>
/// <param name="AssertionBounds">
/// Assertion ID to <b>which of the bounds above that assertion actually depends on</b> — the relation
/// that makes <c>boundsUsed: {}</c> a CHECKABLE claim.
///
/// <para><b>Without it a vector citing a genuinely unbounded assertion had no honest exit.</b> An
/// assertion about a gating condition, an ordering or a state names no time and no threshold, so its
/// vector truthfully records no bound — and gate 3i refused it, leaving <i>inventing a bound</i> as the
/// only way through. That is the fabrication the gate exists to prevent, so the gate was inverted for
/// exactly the vectors that were being most honest.</para>
///
/// <para>🔴 <b>ABSENT IS NOT EMPTY, at both levels.</b> A null relation is "nobody stated it" and the
/// empty claim then reads NOT CHECKED. An empty SET for one assertion is the positive statement "this
/// assertion depends on no bound" — the same shape as <c>reachable-state</c>'s <c>reachableState: []</c>
/// against an omitted key. <b>And it is the ENUMERATION that states it</b>, not the vector: a claim
/// verified against its own author is not verified.</para>
/// </param>
public sealed record AssertionEnumeration(
    IReadOnlySet<string> Clauses,
    IReadOnlySet<string> Assertions,
    IReadOnlyDictionary<string, AssertionForm> Forms,
    AgentIdentity Enumerator,
    IReadOnlyDictionary<string, string>? NormalisedTexts = null,
    IReadOnlyDictionary<string, IReadOnlySet<string>>? RequiredObservations = null,
    IReadOnlyDictionary<string, string>? Bounds = null,
    IReadOnlyDictionary<string, IReadOnlySet<string>>? AssertionBounds = null)
{
    public bool IsEmpty => Assertions.Count == 0 || Clauses.Count == 0;

    /// <summary>True when the enumeration is the flat projection and carries no forms at all.</summary>
    public bool CarriesNoForms => Forms.Count == 0;

    /// <summary>True when nothing in this projection can have its ID recomputed.</summary>
    public bool CarriesNoNormalisedText => NormalisedTexts is null || NormalisedTexts.Count == 0;

    /// <summary>The enumeration's own form for an assertion, or null when it does not say.</summary>
    public AssertionForm? FormOf(string assertionId) =>
        Forms.TryGetValue(assertionId, out var form) ? form : null;

    /// <summary>The text an ID was computed from, or null when the projection does not carry it.</summary>
    public string? NormalisedTextOf(string assertionId) =>
        NormalisedTexts is not null && NormalisedTexts.TryGetValue(assertionId, out var text) ? text : null;

    /// <summary>True when no assertion declares the signals a citation of it depends on.</summary>
    public bool CarriesNoRequiredObservations => RequiredObservations is null || RequiredObservations.Count == 0;

    /// <summary>Every signal a citation of this assertion must observe, or null when the projection does not say.</summary>
    public IReadOnlySet<string>? RequiredObservationsOf(string assertionId) =>
        RequiredObservations is not null && RequiredObservations.TryGetValue(assertionId, out var signals) ? signals : null;

    /// <summary>
    /// True when no bounds table was supplied, so AMB-19's comparison has nothing to compare against.
    /// <b>Not the same as a table that agrees with every vector.</b>
    /// </summary>
    public bool CarriesNoBounds => Bounds is null || Bounds.Count == 0;

    /// <summary>
    /// True when nothing states which bounds each assertion depends on, so a vector's positive
    /// <c>boundsUsed: {}</c> claim <b>cannot be verified by this enumeration</b>. Not the same as an
    /// enumeration stating that every assertion is unbounded.
    /// </summary>
    public bool CarriesNoAssertionBounds => AssertionBounds is null || AssertionBounds.Count == 0;

    /// <summary>
    /// The bounds this assertion depends on, or null when the enumeration does not say. <b>An EMPTY set
    /// is an answer</b> — "this assertion depends on none" — and null is the absence of one.
    /// </summary>
    public IReadOnlySet<string>? BoundsOf(string assertionId) =>
        AssertionBounds is not null && AssertionBounds.TryGetValue(assertionId, out var bounds) ? bounds : null;

    /// <summary>
    /// <b>What gate 3i should compare a vector's empty bounds claim against</b> — assembled here rather
    /// than in the gate so the "why is there nothing" reasons are written once, next to the data that
    /// decides them, and reach the report intact.
    /// </summary>
    public AssertionBoundsExpectation BoundsExpectationFor(string? citedAssertionId)
    {
        if (string.IsNullOrWhiteSpace(citedAssertionId))
        {
            return AssertionBoundsExpectation.NotStated(
                "this vector cites no assertion, so there is nothing whose bounds could be looked up. A vector with no basis is refused by gate 3 in any case.");
        }

        if (CarriesNoAssertionBounds)
        {
            return AssertionBoundsExpectation.NotStated(
                $"the enumeration states no per-assertion bounds relation at all, so nothing says whether '{citedAssertionId}' depends on a bound");
        }

        var bounds = BoundsOf(citedAssertionId);

        return bounds is null
            ? AssertionBoundsExpectation.NotStated(
                $"the enumeration states a per-assertion bounds relation but says nothing about '{citedAssertionId}' — an assertion missing from the relation is an ABSENCE, never an assertion with no bounds")
            : AssertionBoundsExpectation.Stated(citedAssertionId, bounds);
    }

    public static AssertionEnumeration Of(
        IEnumerable<string> clauses,
        IEnumerable<string> assertions,
        IReadOnlyDictionary<string, AssertionForm>? forms = null,
        string enumerator = "",
        IReadOnlyDictionary<string, string>? normalisedTexts = null,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? requiredObservations = null,
        IReadOnlyDictionary<string, string>? bounds = null,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? assertionBounds = null) =>
        new(clauses.ToHashSet(StringComparer.Ordinal),
            assertions.ToHashSet(StringComparer.Ordinal),
            forms ?? new Dictionary<string, AssertionForm>(StringComparer.Ordinal),
            new AgentIdentity(enumerator),
            normalisedTexts,
            requiredObservations,
            bounds,
            assertionBounds);
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
/// <param name="DeclaredBy">
/// Who declared the model's <c>Represents</c> set.
///
/// <para>🔴 <b>THE FIDELITY LIST IS WHAT LICENSES A VECTOR'S ASSERTED BEHAVIOURS (M4), SO A LIST
/// SUPPLIED BY THE VECTOR'S OWN AUTHOR LICENSES ITSELF.</b> Measured 2026-08-14: a model BLOCK existed
/// and a model DECLARATION did not — the model id occurred <b>only inside the vector file</b> — so the
/// gate compared each vector's asserted behaviours against a set the same party had written.</para>
///
/// <para><b>Third instance of this family</b>, after the enumeration (3d) and the observability map
/// (gate 5). Unrecorded is NOT CHECKED, never a pass: unknown is not independent, and the alternative
/// admits every submission that simply says less.</para>
/// </param>
public sealed record FidelityDeclaration(
    string ModelId,
    IReadOnlySet<string> Represents,
    IReadOnlySet<string> DoesNotRepresent,
    bool ValidatedAgainstPlantData,
    AgentIdentity DeclaredBy = default)
{
    public static FidelityDeclaration Of(string modelId, IEnumerable<string> represents,
        IEnumerable<string>? doesNotRepresent = null, bool validatedAgainstPlantData = false,
        string declaredBy = "") =>
        new(modelId, represents.ToHashSet(StringComparer.Ordinal),
            (doesNotRepresent ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal),
            validatedAgainstPlantData,
            new AgentIdentity(declaredBy));
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
/// <param name="UnchangedForScans">
/// The one settling form this harness can EVALUATE: the observed value must be unchanged across this
/// many consecutive scans. <b>Zero means the declared condition is prose the runner cannot check</b>,
/// and the result then carries <c>SettlingState.NotEstablished</c> — which is deliberately not the same
/// as settled. Whether the declared condition really implies finality is judgement either way.
/// </param>
public sealed record SettlingDeclaration(string Condition, IReadOnlyList<string> Signals, int UnchangedForScans = 0);

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
