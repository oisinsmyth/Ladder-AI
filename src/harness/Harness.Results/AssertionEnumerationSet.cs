namespace Harness.Results;

/// <summary>What resolving one citation against the set produced.</summary>
public enum CitationResolutionState
{
    /// <summary>
    /// 🔴 <b>THE ZERO VALUE IS UNUSABLE, following <c>AssertionForm.Unstated</c>.</b> Nothing constructs
    /// it; a resolution that reads as this one was never performed, and a caller that keys on it fails
    /// closed rather than reading an unset field as a success.
    /// </summary>
    Unresolved = 0,

    /// <summary>Exactly one enumeration answers this citation, and it is carried on the resolution.</summary>
    Resolved,

    /// <summary>
    /// <b>MORE THAN ONE SUBJECT DECLARES THE CITED CLAUSE AND THE CITATION DOES NOT SAY WHICH.</b> The
    /// refusal this whole type exists for — see <see cref="AssertionEnumerationSet.Resolve"/>.
    /// </summary>
    Ambiguous,

    /// <summary>The citation names a subject no enumeration in the set declares.</summary>
    UnknownSubject,

    /// <summary>No enumeration in the set declares the cited clause. Only reachable with more than one enumeration.</summary>
    NotFound,

    /// <summary>The vector cites nothing. Gate 3's refusal, not this one's — reported so it is not counted twice.</summary>
    NotCited,

    /// <summary>The set is empty, so the citation was resolved against nothing. Empty is not clean (FI-44).</summary>
    NothingToResolveInto,
}

/// <summary>
/// One citation's resolution, with the reason attached.
/// </summary>
/// <param name="Enumeration">
/// The enumeration this citation resolved INTO. <b>Non-null exactly when <see cref="CitationResolutionState.Resolved"/></b>
/// — every other state is the absence of an answer, and a caller that reached for a fallback here would be
/// guessing at a denominator, which is the thing this type refuses to do.
/// </param>
/// <param name="Candidates">
/// The subjects that made this resolution what it is: the ambiguous candidates, the subjects that exist
/// where the declared one does not, or the subjects searched when the clause was found in none.
/// </param>
public sealed record CitationResolution(
    CitationResolutionState State,
    AssertionEnumeration? Enumeration,
    IReadOnlyList<string> Candidates,
    string Detail)
{
    public bool IsResolved => State == CitationResolutionState.Resolved && Enumeration is not null;

    /// <summary>
    /// True when this citation could not be placed AND that is this resolution's fault to report.
    /// <b><see cref="CitationResolutionState.NotCited"/> is excluded deliberately</b> — gate 3 already
    /// refuses a vector with no basis, and reporting it here as well would refuse one defect twice while
    /// making the subject gate look like it found something it did not.
    /// </summary>
    public bool IsFailure => State is CitationResolutionState.Ambiguous
        or CitationResolutionState.UnknownSubject
        or CitationResolutionState.NotFound
        or CitationResolutionState.NothingToResolveInto;

    internal static CitationResolution Of(CitationResolutionState state, AssertionEnumeration? enumeration,
        IEnumerable<string>? candidates, string detail) =>
        new(state, enumeration, (candidates ?? Array.Empty<string>()).ToArray(), detail);
}

/// <summary>
/// 🔴 <b>THE ENUMERATIONS A SUBMISSION CITES INTO — PLURAL, EACH WITH ITS OWN SUBJECT, AND A CITATION
/// RESOLVED TO ONE OF THEM RATHER THAN LOOKED UP ACROSS ALL OF THEM.</b>
///
/// <para><b>The measured problem, 2026-08-18.</b> A campaign gained a SECOND enumeration — one subject
/// per file, valve behaviour and vessel behaviour — and <b>four clause IDs appear in both</b>. Until this
/// type existed a submission could hold exactly one enumeration (<c>SubmissionDocument.Enumeration</c>,
/// singular) and a citation was two terms, <c>(clause, assertion)</c>, checked with two
/// <c>Contains</c> calls against two flat sets. The only way to submit a two-subject campaign was to MERGE
/// the files — after which <c>Clauses.Contains("SPEC-2.19")</c> is true for a citation from either
/// subject, the denominator gate 3 reports is the union of two denominators and therefore neither, and
/// the dangling-citation check passes having examined the wrong set.</para>
///
/// <para>🔴 <b>AND THE ASSERTION ID CANNOT BE THE TIE-BREAKER. MEASURED, NOT ASSUMED.</b> An ID is
/// <c>clause + ":" + first-six-hex(SHA-256(normalised text))</c> — <see cref="AssertionId.Compute"/>, in
/// which <b>no subject term appears</b>. Two subject files that share a clause AND share a normalised text
/// therefore mint the SAME ID, and the live vessel enumeration records that this has already happened:
/// <i>"THE TEXT IS BYTE-IDENTICAL, so the id is identical, so nothing dangles."</i> A resolver that fell
/// back on the hash where the clause was ambiguous would be right until the first shared sentence and then
/// silently wrong. <b>So matching is at CLAUSE level and the hash is never consulted to disambiguate.</b></para>
///
/// <para>🔴 <b>A SET OF ONE CANNOT BE AMBIGUOUS, AND THAT IS THE BACKWARD-COMPATIBILITY GUARANTEE.</b>
/// Every enumeration written before today is an unnamed single subject, and every citation written before
/// today is unqualified. <see cref="Resolve"/> answers <see cref="CitationResolutionState.Resolved"/> for a
/// one-enumeration set BEFORE it looks at clauses at all, so a single-enumeration submission takes exactly
/// the path it took yesterday, down to the refusal text. <b>An unqualified citation still means the file
/// that was there first</b> — it is not retargeted, because in a set of one there is nothing to retarget
/// it to. The four shared clauses become the only citations that must be qualified, and they become a
/// REFUSAL NAMING BOTH SUBJECTS rather than a pick.</para>
/// </summary>
public sealed record AssertionEnumerationSet(IReadOnlyList<AssertionEnumeration> Enumerations)
{
    /// <summary>The set nobody supplied. Every citation against it is <see cref="CitationResolutionState.NothingToResolveInto"/>.</summary>
    public static readonly AssertionEnumerationSet Empty = new(Array.Empty<AssertionEnumeration>());

    public static AssertionEnumerationSet Of(params AssertionEnumeration[] enumerations) =>
        new(enumerations ?? Array.Empty<AssertionEnumeration>());

    public static AssertionEnumerationSet Of(IEnumerable<AssertionEnumeration> enumerations) =>
        new((enumerations ?? Array.Empty<AssertionEnumeration>()).ToArray());

    /// <summary>
    /// 🔴 <b>ONE ENUMERATION *IS* A SET OF ONE, and saying so in the type system is what keeps every
    /// existing call site — the loop's, the CLI's, and every test in three suites — compiling and behaving
    /// identically.</b>
    ///
    /// <para>The alternative was an overload pair, which is two code paths that agree today. This project
    /// has measured what that costs twice: <c>GateInputs</c> exists because the loop and the CLI derived
    /// one input set two ways, and <c>AssertionId</c> is duplicated into <c>wave-control</c> and pinned by
    /// known-answer vectors for the same reason. <b>One path, and the singular case is a value of the
    /// plural type rather than a second implementation of it.</b></para>
    /// </summary>
    public static implicit operator AssertionEnumerationSet(AssertionEnumeration enumeration) => Of(enumeration);

    /// <summary><b>Nothing was supplied at all.</b> Distinct from a set holding an EMPTY enumeration, which is gate 3's <c>EnumerationEmpty</c>.</summary>
    public bool IsEmpty => Enumerations.Count == 0;

    /// <summary>True when this is the single-subject shape every submission had before 2026-08-18.</summary>
    public bool IsSingle => Enumerations.Count == 1;

    /// <summary>Every declared subject, in the order the document listed them. An unnamed subject reads as <c>&lt;unnamed&gt;</c>.</summary>
    public IReadOnlyList<string> Subjects =>
        Enumerations.Select(e => DisplaySubject(e.Subject)).ToArray();

    /// <summary>
    /// <b>Subjects declared more than once — malformed, and refused rather than resolved.</b>
    ///
    /// <para>Two enumerations under one subject name is two answers to one question: a citation naming it
    /// would resolve to whichever came first, which is the pick this type exists to prevent, wearing a
    /// qualified citation's clothes so nobody would look.</para>
    /// </summary>
    public IReadOnlyList<string> DuplicateSubjects =>
        Enumerations
            .GroupBy(e => Normalise(e.Subject), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => DisplaySubject(g.Key))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Resolve one citation to the ONE enumeration that answers it.
    ///
    /// <para><b>The rules, in the order they are applied:</b></para>
    /// <list type="number">
    /// <item>An empty set resolves nothing — <see cref="CitationResolutionState.NothingToResolveInto"/>.</item>
    /// <item>A citation with no clause is gate 3's refusal, not this one's — <see cref="CitationResolutionState.NotCited"/>.</item>
    /// <item><b>A set of ONE with an unqualified citation is RESOLVED, before any clause is examined.</b>
    /// There is nothing to resolve between, so yesterday's submission takes yesterday's path exactly.</item>
    /// <item>A citation that NAMES a subject resolves to that subject alone, or
    /// <see cref="CitationResolutionState.UnknownSubject"/>. <b>Whether the named subject then contains the
    /// clause is gate 3's question and not this one's</b> — keeping the two apart is what makes a dangling
    /// citation report as dangling instead of as a subject error.</item>
    /// <item>An unqualified citation matches on the CLAUSE set. One candidate resolves; <b>more than one is
    /// <see cref="CitationResolutionState.Ambiguous"/> and names every candidate</b>; none is
    /// <see cref="CitationResolutionState.NotFound"/>.</item>
    /// </list>
    /// </summary>
    public CitationResolution Resolve(Basis? basis)
    {
        if (IsEmpty)
        {
            return CitationResolution.Of(CitationResolutionState.NothingToResolveInto, null, null,
                "no assertion enumeration was supplied at all, so this citation was resolved against nothing. "
                + "An empty set answers every citation the same way while reading exactly like a resolution that ran.");
        }

        if (basis is null || string.IsNullOrWhiteSpace(basis.ClauseId))
        {
            return CitationResolution.Of(CitationResolutionState.NotCited, null, null,
                "the vector cites no clause, so there is nothing to resolve to a subject. Gate 3 refuses this on its own account.");
        }

        var declared = Normalise(basis.Subject);

        // 🔴 THE BACKWARD-COMPATIBILITY GUARANTEE, AND IT IS FIRST ON PURPOSE. A set of one has nothing to
        // resolve BETWEEN, so an unqualified citation cannot be ambiguous and must not be routed through the
        // clause scan below — which would answer NotFound for a dangling citation and change gate 3's text
        // for every submission written before subjects existed.
        if (declared.Length == 0 && IsSingle)
        {
            return CitationResolution.Of(CitationResolutionState.Resolved, Enumerations[0], Subjects,
                "the submission carries one enumeration, so there is nothing for an unqualified citation to be ambiguous between.");
        }

        if (declared.Length > 0)
        {
            var named = Enumerations
                .Where(e => string.Equals(Normalise(e.Subject), declared, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (named.Length == 0)
            {
                return CitationResolution.Of(CitationResolutionState.UnknownSubject, null, Subjects,
                    $"cites subject '{basis.Subject}', which no enumeration in this submission declares. "
                    + $"The subjects that exist are: {Join(Subjects)}. A citation into a subject nobody enumerated resolves to nothing written.");
            }

            if (named.Length > 1)
            {
                return CitationResolution.Of(CitationResolutionState.Ambiguous, null, named.Select(e => DisplaySubject(e.Subject)),
                    $"cites subject '{basis.Subject}', and {named.Length} enumerations in this submission declare that same subject. "
                    + "Two enumerations under one subject name is two answers to one question; the citation is qualified and still does not say which.");
            }

            return CitationResolution.Of(CitationResolutionState.Resolved, named[0], new[] { DisplaySubject(named[0].Subject) },
                $"resolved to subject '{DisplaySubject(named[0].Subject)}' because the citation names it.");
        }

        // Unqualified, in a set of more than one. Match on the CLAUSE — never on the assertion ID, which
        // carries no subject term and collides across files that share a clause and a sentence.
        var candidates = Enumerations.Where(e => e.Clauses.Contains(basis.ClauseId)).ToArray();

        if (candidates.Length == 0)
        {
            return CitationResolution.Of(CitationResolutionState.NotFound, null, Subjects,
                $"clause '{basis.ClauseId}' is declared by none of the {Enumerations.Count} enumerations in this submission "
                + $"({Join(Subjects)}), so the citation resolves to nothing written and no subject could be chosen to refuse it against.");
        }

        if (candidates.Length > 1)
        {
            var names = candidates.Select(e => DisplaySubject(e.Subject)).ToArray();

            return CitationResolution.Of(CitationResolutionState.Ambiguous, null, names,
                $"clause '{basis.ClauseId}' is declared by {candidates.Length} subjects — {Join(names)} — and this citation does not say which. "
                + "*** THIS IS A REFUSAL AND NOT A PICK. *** Each subject numbers its own assertions from A1, so the same clause names a "
                + "DIFFERENT assertion in each file; and the assertion ID cannot break the tie either, because an ID is "
                + "(clause identity, assertion content) with no subject term — two subjects sharing a clause and a sentence mint the same ID. "
                + $"Qualify the citation with the subject it means, e.g. {basis.ClauseId}[{names[0]}].");
        }

        return CitationResolution.Of(CitationResolutionState.Resolved, candidates[0], new[] { DisplaySubject(candidates[0].Subject) },
            $"resolved to subject '{DisplaySubject(candidates[0].Subject)}', the only one declaring clause '{basis.ClauseId}'.");
    }

    /// <summary>
    /// The per-subject denominators, as text. <b>Deliberately never a single total.</b>
    ///
    /// <para>Gate 3's pass line used to read <i>"an enumeration of N assertion(s)"</i>. Summed over two
    /// subjects that number is the coverage denominator of neither, and a reader taking a coverage figure
    /// from it would be quoting a denominator that does not exist. So the subjects are stated separately
    /// and the sum is never printed.</para>
    /// </summary>
    public string DenominatorText() =>
        IsEmpty
            ? "no enumeration"
            : IsSingle && Normalise(Enumerations[0].Subject).Length == 0
                ? $"an enumeration of {Enumerations[0].Assertions.Count} assertion(s)"
                : string.Join(", ", Enumerations.Select(e =>
                    $"{DisplaySubject(e.Subject)}: {e.Assertions.Count} assertion(s) across {e.Clauses.Count} clause(s)"));

    /// <summary>Clause IDs declared by more than one subject — the citations that must be qualified.</summary>
    public IReadOnlyList<string> SharedClauses() =>
        Enumerations
            .SelectMany(e => e.Clauses.Select(c => (Clause: c, Subject: Normalise(e.Subject))))
            .GroupBy(x => x.Clause, StringComparer.Ordinal)
            .Where(g => g.Select(x => x.Subject).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(g => g.Key)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray();

    internal static string Normalise(string? subject) => (subject ?? string.Empty).Trim();

    internal static string DisplaySubject(string? subject) =>
        Normalise(subject).Length == 0 ? "<unnamed>" : Normalise(subject);

    private static string Join(IEnumerable<string> values) => string.Join(", ", values);
}
