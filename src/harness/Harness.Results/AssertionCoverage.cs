namespace Harness.Results;

/// <summary>Whether a coverage figure exists at all — <b>and the zero value is unusable.</b></summary>
public enum CoverageState
{
    /// <summary>
    /// 🔴 <b>NOBODY COMPUTED IT, and that is the DEFAULT</b>, following <c>AssertionForm.Unstated</c> and
    /// <c>MapProvenance.Unstated</c>. A report that carried an unset coverage would print <c>0 of 0</c>
    /// and read as a measurement; this reads as the absence of one.
    /// </summary>
    NotComputed = 0,

    /// <summary>
    /// It was computed and there is <b>no denominator to compute it against</b> — no enumeration was
    /// supplied at all. <b>Not the same as <c>0 of 0</c>, and not the same as zero coverage.</b>
    /// </summary>
    NoDenominator,

    /// <summary>A denominator existed and the numerator was counted against it.</summary>
    Computed,
}

/// <summary>
/// One assertion that at least one vector cites, <b>with every vector that cites it</b>.
/// </summary>
/// <param name="Vectors">
/// 🔴 <b>THE LIST IS WHY THIS TYPE EXISTS.</b> Two vectors citing one assertion is two vectors and ONE
/// unit of coverage, and nothing in this system counted the numerator, so the fact was invisible: a lane's
/// vector count went 2 → 3 while its coverage did not move at all and no report said so.
/// </param>
public sealed record CitedAssertion(string AssertionId, IReadOnlyList<string> Vectors);

/// <summary>
/// The coverage of ONE subject's enumeration. <b>Per subject, and never summed with another.</b>
///
/// <para>Summed over two subjects the total is the coverage denominator of neither — the same rule
/// <see cref="AssertionEnumerationSet.DenominatorText"/> already enforces on the denominator alone, and a
/// fraction is the shape in which somebody would most want to break it.</para>
/// </summary>
/// <param name="Enumerator">
/// Who produced this denominator, <b>printed next to the fraction on purpose</b>. The enumeration is the
/// coverage denominator, so a denominator produced by the party whose work it measures makes the fraction
/// unfalsifiable. Gate 3d refuses that; this makes it visible in the number itself.
/// </param>
/// <param name="CitedNotEnumerated">
/// Citations that resolved into this subject and name an assertion it does not contain. <b>Never in the
/// numerator.</b> A citation the enumeration does not answer is an error in the VECTOR (gate 3 refuses
/// it), and counting it would let an author raise coverage by citing something that does not exist.
/// </param>
public sealed record SubjectCoverage(
    string Subject,
    string Enumerator,
    int Denominator,
    IReadOnlyList<CitedAssertion> Covered,
    IReadOnlyList<CitedAssertion> CitedNotEnumerated,
    int VectorsResolvedHere)
{
    /// <summary>Distinct assertions cited AND present in this enumeration.</summary>
    public int Numerator => Covered.Count;

    /// <summary>Assertions carried by more than one vector — the fact the vector count hides.</summary>
    public IReadOnlyList<CitedAssertion> MultiplyCited =>
        Covered.Where(c => c.Vectors.Count > 1).ToArray();

    /// <summary>Vectors spent beyond the first on an assertion already cited.</summary>
    public int RedundantVectors => Covered.Sum(c => c.Vectors.Count - 1);

    public string Fraction => $"{Numerator} of {Denominator}";
}

/// <summary>
/// 🔴 <b>COVERAGE AS A COMPUTED FACT: DISTINCT ASSERTIONS CITED, OVER THE ENUMERATION'S SIZE, PER
/// SUBJECT.</b>
///
/// <para><b>The measured problem.</b> Across five rig events over five days the set of assertions ever
/// asserted against a block was byte-identical, and nothing reported it — because <b>nothing counted the
/// numerator.</b> A lane's vector count rose while its coverage stood still, two of its vectors citing one
/// assertion, and every mechanical check stayed green: each vector was individually admissible, so there
/// was no gate to fail. What was missing was not a refusal, it was a NUMBER.</para>
///
/// <para><b>IT IS A REPORT AND DELIBERATELY NOT A GATE.</b> Low coverage is a true fact about a young
/// campaign, not an inadmissible submission, and a threshold here would be met by writing vectors against
/// whatever is cheapest to cite. What a report can do that a gate cannot is be <i>unfudgeable and
/// visible</i>: the numerator counts assertions and not vectors, and the denominator is somebody else's
/// document.</para>
///
/// <para>🔴 <b>AND THE AUTHOR CANNOT WIDEN EITHER HALF.</b> The denominator is
/// <see cref="AssertionEnumeration.Assertions"/> — spec-derived, produced by a third party, and named here
/// with its producer. The numerator counts only citations that RESOLVE into that enumeration and are
/// present in it, so a citation to something nobody enumerated raises nothing (it is listed under
/// <see cref="SubjectCoverage.CitedNotEnumerated"/> and refused by gate 3). Deleting assertions from the
/// enumeration would raise the fraction, and that is exactly why the enumerator is printed beside it.</para>
///
/// <para><b>What it cannot see</b> is stated on every rendering rather than left to be inferred — see
/// <see cref="Lines"/>. In short: it counts what was CITED at admission, not what PASSED; it cannot judge
/// whether the denominator itself is complete; and it cannot know which assertions have no implementing
/// logic to test at all.</para>
/// </summary>
public sealed record AssertionCoverage(
    CoverageState State,
    IReadOnlyList<SubjectCoverage> Subjects,
    int VectorsExamined,
    IReadOnlyList<string> VectorsCitingNothing,
    IReadOnlyList<string> VectorsUnresolved)
{
    /// <summary>The value a report carries before anything computed one. <b>Never a zero fraction.</b></summary>
    public static readonly AssertionCoverage NotComputed = new(
        CoverageState.NotComputed,
        Array.Empty<SubjectCoverage>(),
        0,
        Array.Empty<string>(),
        Array.Empty<string>());

    /// <summary>
    /// Count the distinct assertions this submission cites, against each enumeration it could cite into.
    ///
    /// <para><b>EVERY enumeration in the set gets a line, including the ones nothing cites.</b> A subject
    /// omitted because no vector reached it is a subject at zero coverage, and omitting it prints a
    /// submission that covers everything it looked at.</para>
    /// </summary>
    public static AssertionCoverage Compute(
        IReadOnlyList<SubmissionVector> vectors, AssertionEnumerationSet enumerations)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentNullException.ThrowIfNull(enumerations);

        var citingNothing = new List<string>();
        var unresolved = new List<string>();

        // assertion id -> vectors, per enumeration INDEX. Indexed rather than keyed on the enumeration
        // itself: AssertionEnumeration is a record, so two subjects that happened to be equal by value
        // would fold into one bucket and one of them would silently vanish from the report.
        var citations = enumerations.Enumerations
            .Select(_ => new SortedDictionary<string, List<string>>(StringComparer.Ordinal))
            .ToArray();

        foreach (var vector in vectors)
        {
            if (vector is null)
                continue;

            if (vector.Basis is null || string.IsNullOrWhiteSpace(vector.Basis.AssertionId))
            {
                // Gate 3 refuses this on its own account. It is recorded here because a vector that cites
                // nothing still costs a wave, and a coverage line that quietly dropped it would report a
                // smaller submission than the one that ran.
                citingNothing.Add(vector.Id);
                continue;
            }

            var resolution = enumerations.Resolve(vector.Basis);
            var index = IndexOf(enumerations, resolution.Enumeration);

            if (index < 0)
            {
                unresolved.Add(vector.Id);
                continue;
            }

            var bucket = citations[index];
            if (!bucket.TryGetValue(vector.Basis.AssertionId, out var citers))
                bucket[vector.Basis.AssertionId] = citers = new List<string>();

            citers.Add(vector.Id);
        }

        var subjects = enumerations.Enumerations
            .Select((enumeration, i) => new SubjectCoverage(
                AssertionEnumerationSet.DisplaySubject(enumeration.Subject),
                enumeration.Enumerator.IsRecorded ? enumeration.Enumerator.Value : "<unrecorded>",
                enumeration.Assertions.Count,
                citations[i].Where(e => enumeration.Assertions.Contains(e.Key))
                    .Select(e => new CitedAssertion(e.Key, e.Value)).ToArray(),
                citations[i].Where(e => !enumeration.Assertions.Contains(e.Key))
                    .Select(e => new CitedAssertion(e.Key, e.Value)).ToArray(),
                citations[i].Sum(e => e.Value.Count)))
            .ToArray();

        return new AssertionCoverage(
            enumerations.IsEmpty ? CoverageState.NoDenominator : CoverageState.Computed,
            subjects,
            vectors.Count,
            citingNothing,
            unresolved);
    }

    private static int IndexOf(AssertionEnumerationSet enumerations, AssertionEnumeration? enumeration)
    {
        if (enumeration is null)
            return -1;

        for (var i = 0; i < enumerations.Enumerations.Count; i++)
        {
            if (ReferenceEquals(enumerations.Enumerations[i], enumeration))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// The report, <b>on every run including the zero case</b>, with its denominator and its blind spots
    /// on every line it prints.
    ///
    /// <para>A figure that appeared only when there was something to celebrate would teach its reader that
    /// its absence means nothing was measured — the same lesson as F-6's collapse report and the loop's
    /// caveats, both of which print their nothing-to-say line.</para>
    /// </summary>
    public IReadOnlyList<string> Lines()
    {
        var lines = new List<string>
        {
            "ASSERTION COVERAGE — DISTINCT ASSERTIONS CITED over the enumeration's size, per subject. A REPORT, NOT A GATE.",
        };

        switch (State)
        {
            case CoverageState.NotComputed:
                lines.Add("  NOT COMPUTED. Nothing produced a coverage figure for this submission, so there is no number here — "
                          + "which is NOT the same as a coverage of zero, and must never be read as one.");
                return lines;

            case CoverageState.NoDenominator:
                lines.Add($"  NO DENOMINATOR. No assertion enumeration was supplied, so the {VectorsExamined} vector(s) here were counted against nothing. "
                          + "*** THIS IS NOT 0 OF 0 AND IT IS NOT 100%. *** Coverage is UNCOMPUTABLE without a spec-derived enumeration, and empty is not clean (FI-44).");
                break;

            default:
                foreach (var subject in Subjects)
                    lines.AddRange(SubjectLines(subject));

                break;
        }

        if (VectorsCitingNothing.Count > 0)
        {
            lines.Add($"  {VectorsCitingNothing.Count} of {VectorsExamined} vector(s) CITE NO ASSERTION and contribute to no numerator: "
                      + $"{string.Join(", ", VectorsCitingNothing)}. Gate 3 refuses them; they are counted here because a wave still costs what it costs.");
        }

        if (VectorsUnresolved.Count > 0)
        {
            lines.Add($"  {VectorsUnresolved.Count} of {VectorsExamined} vector(s) could not be placed in any one enumeration, so they raise NO subject's numerator: "
                      + $"{string.Join(", ", VectorsUnresolved)}. See gate 3j — an ambiguous citation is a refusal, not a pick, and it is not coverage either.");
        }

        lines.Add("  WHAT THIS NUMBER CANNOT SEE — stated every run, not only the bad ones:");
        lines.Add("    - It counts what was CITED at admission, NOT what passed. A cited assertion whose vector later fails, times out, goes unsettled or is never deployed still counts here.");
        // *** THE WORD "UNCLASSIFIED" IS DELIBERATELY NOT USED HERE. *** The enumerator's residual bucket
        // is spelled that way in its own skill, and this report shares an output stream with the gate
        // triage, whose whole claim is that no NOT CHECKED verdict in it is unclassified. Two unrelated
        // senses of one word in one report is how a reader concludes the wrong thing from a search.
        lines.Add("    - It cannot judge whether the DENOMINATOR is complete. The enumeration is a third party's decomposition of the spec, and the clauses it could not decompose are not visible from a submission.");
        lines.Add("    - It cannot know which assertions have NO IMPLEMENTING LOGIC to test. Those are unreachable rather than uncovered, and only the enumerator's gap report separates the two.");

        return lines;
    }

    private static IEnumerable<string> SubjectLines(SubjectCoverage subject)
    {
        var head = $"  {subject.Subject} [denominator by {subject.Enumerator}]: {subject.Fraction} assertion(s) cited, by {subject.VectorsResolvedHere} vector(s).";

        if (subject.Denominator == 0)
        {
            yield return head;
            yield return "      *** THE ENUMERATION IS EMPTY, so this is a denominator of nothing and NOT full coverage. *** "
                         + "A citation checked against an empty enumeration is checked against nothing (gate 3's EnumerationEmpty).";
            yield break;
        }

        yield return head;

        if (subject.Numerator == 0)
        {
            yield return $"      NOTHING IN THIS SUBJECT IS CITED. {subject.Denominator} assertion(s) stand unasserted, and no vector here would notice if any of them were wrong.";
        }

        foreach (var multiple in subject.MultiplyCited)
        {
            yield return $"      '{multiple.AssertionId}' is cited by {multiple.Vectors.Count} vectors ({string.Join(", ", multiple.Vectors)}) and counts ONCE. "
                         + "*** THE VECTOR COUNT IS NOT THE NUMERATOR. *** Adding the second one moved the vector count and moved coverage not at all.";
        }

        if (subject.RedundantVectors > 0)
        {
            yield return $"      {subject.VectorsResolvedHere} vector(s) resolved into this subject and bought {subject.Numerator} distinct assertion(s): "
                         + $"{subject.RedundantVectors} of them re-cite an assertion another vector already covers.";
        }

        foreach (var dangling in subject.CitedNotEnumerated)
        {
            yield return $"      '{dangling.AssertionId}' ({string.Join(", ", dangling.Vectors)}) resolves into this subject and the enumeration does NOT contain it, "
                         + "so it is NOT in the numerator. A citation the enumeration does not answer is an error in the vector, never an extension of the denominator.";
        }
    }
}
