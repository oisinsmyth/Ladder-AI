using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>What is wrong with a citation, or with the coverage picture as a whole.</summary>
    public enum CoverageDefect
    {
        /// <summary>Not a defect.</summary>
        None = 0,

        /// <summary>
        /// *** THE RESIDUAL IS NON-ZERO. *** Assertions the classification never mentioned. Not a
        /// missing tick — a failed gate. Nobody wrote "unclassified"; it is what remained.
        /// </summary>
        UnclassifiedResidual = 1,

        /// <summary>
        /// The citation names an ID the enumeration does not contain and NOTHING SUPERSEDED IT. It
        /// never existed: a typo, an invention, or a citation into a different register.
        /// </summary>
        CitationIsMissing = 2,

        /// <summary>
        /// *** THE CITED ASSERTION WAS REWORDED. *** Some assertion records this ID as the one it
        /// superseded, so the citation is STALE, not missing. The vector was written against the OLD
        /// words and nobody re-read it — which is exactly why editing text changes the ID. Kept
        /// separate from <see cref="CitationIsMissing"/>: they call for opposite fixes, and collapsing
        /// them would send an author hunting a typo that is really a re-read.
        /// </summary>
        CitationIsStale = 3,

        /// <summary>The citation used the display-ordinal form (<c>REQ-014.A2</c>), which is rejected by shape.</summary>
        CitationUsesTheDisplayOrdinalForm = 4,

        /// <summary>
        /// The cited assertion's response signal appears nowhere in the vector's expectations. It does
        /// not prove the vector fails to test the assertion; it catches a citation that could not
        /// possibly be testing it.
        /// </summary>
        ResponseSignalAbsentFromExpectations = 5,

        /// <summary>
        /// *** THE VECTOR DECLARES A FORM THE ENUMERATION DISAGREES WITH *** — citing a NEVER while
        /// declaring a When, which takes the permissive path. Closed here because the enumeration now
        /// records the form; it could not be closed while the gate's enumeration was flat strings.
        /// </summary>
        DeclaredFormDisagreesWithTheEnumeration = 6,

        /// <summary>The citation names an instance the clause does not declare.</summary>
        InstanceNotDeclaredByTheClause = 7,

        /// <summary>A unit is bucketed COVERED but no vector cites it.</summary>
        CoveredWithoutACitation = 8,

        /// <summary>A unit is cited by a vector but bucketed as something other than COVERED.</summary>
        CitedButNotBucketedCovered = 9,

        /// <summary>The vector's author is the block's author (D6).</summary>
        VectorAuthorIsTheBlockAuthor = 10,

        /// <summary>
        /// The decomposition produced a different assertion count than the previous enumeration. §7
        /// requires this as its OWN EVENT — never absorbed as a classification change.
        /// </summary>
        RedecompositionChangedTheCount = 11,
    }

    /// <summary>One coverage finding.</summary>
    public sealed class CoverageFinding
    {
        internal CoverageFinding(CoverageDefect defect, string subject, string detail)
        {
            Defect = defect;
            Subject = subject;
            Detail = detail;
        }

        /// <summary>Which defect.</summary>
        public CoverageDefect Defect { get; }

        /// <summary>The unit, vector or clause it is about.</summary>
        public string Subject { get; }

        /// <summary>What is wrong.</summary>
        public string Detail { get; }

        /// <inheritdoc />
        public override string ToString() => Defect + " [" + Subject + "]: " + Detail;
    }

    /// <summary>Whether the wave may claim its requirements verified.</summary>
    /// <remarks>
    /// *** THE GATE STOPS THE CLAIM, NOT THE RUN (§7). *** Blocking a wave for a bookkeeping reason
    /// spends the scarce thing (rig time) to enforce the cheap thing. A wave with incomplete
    /// classification still produces useful results — it simply may not claim "requirements verified".
    /// That decouples honesty from throughput.
    /// </remarks>
    public enum ClaimVerdict
    {
        /// <summary>
        /// Nothing was evaluated. The zero value, so a dropped or defaulted verdict never licenses a
        /// claim.
        /// </summary>
        NotEvaluated = 0,

        /// <summary>
        /// *** NOT ENUMERABLE. *** The denominator itself is unusable — no stable clause IDs, an empty
        /// enumeration, a clause not decomposed. §6's rule: the correct output is "not enumerable" and
        /// NOT a number.
        /// </summary>
        NotEnumerable = 1,

        /// <summary>The residual is non-zero, or something else failed. Results stand; the claim does not.</summary>
        MayNotClaimRequirementsVerified = 2,

        /// <summary>Every assertion was considered. Not every assertion has a test — that was never the gate.</summary>
        MayClaimRequirementsVerified = 3,
    }

    /// <summary>
    /// The coverage figure. *** A BARE PERCENTAGE IS NOT AN AVAILABLE OUTPUT, STRUCTURALLY. ***
    /// </summary>
    /// <remarks>
    /// <para>
    /// §5 row 6 makes this the one inflation route that closes cleanly, and only if the closure is
    /// structural rather than a convention. So this type exposes NO numeric coverage property: no
    /// `Percent`, no `Ratio`, no `Fraction`. The percentage exists only inside <see cref="Render"/>,
    /// which always emits all four bucket counts, the residual, and the oldest deferral's age
    /// alongside it. There is nothing to quote on its own — a caller wanting the number has to take
    /// the sentence that carries its caveats.
    /// </para>
    /// <para>
    /// A test pins this by reflection: no public member of this type may return a number and be named
    /// for a percentage or a ratio.
    /// </para>
    /// </remarks>
    public sealed class CoverageFigure
    {
        internal CoverageFigure(
            int covered,
            int outOfScope,
            int deferred,
            int untestableOnRig,
            int unclassified,
            TimeSpan? oldestDeferral)
        {
            Covered = covered;
            OutOfScope = outOfScope;
            Deferred = deferred;
            UntestableOnRig = untestableOnRig;
            Unclassified = unclassified;
            OldestDeferral = oldestDeferral;
        }

        /// <summary>COVERED count.</summary>
        public int Covered { get; }

        /// <summary>OUT-OF-SCOPE count.</summary>
        public int OutOfScope { get; }

        /// <summary>DEFERRED count.</summary>
        public int Deferred { get; }

        /// <summary>UNTESTABLE-ON-RIG count.</summary>
        public int UntestableOnRig { get; }

        /// <summary>The computed residual — never written by anybody.</summary>
        public int Unclassified { get; }

        /// <summary>The oldest deferral's age, or null when there are none.</summary>
        public TimeSpan? OldestDeferral { get; }

        /// <summary>The denominator.</summary>
        public int Enumerated => Covered + OutOfScope + Deferred + UntestableOnRig + Unclassified;

        /// <summary>
        /// The ONLY way to render a coverage figure. Always carries all four bucket counts, the
        /// residual and the oldest deferral's age — a bare percentage is not obtainable from this type.
        /// </summary>
        public string Render()
        {
            var deferralAge = OldestDeferral.HasValue
                ? ((int)Math.Floor(OldestDeferral.Value.TotalDays)).ToString(CultureInfo.InvariantCulture) + "d"
                : "none";

            var proportion = Enumerated == 0
                ? "n/a"
                : (100.0 * Covered / Enumerated).ToString("0.0", CultureInfo.InvariantCulture) + "%";

            return "COVERAGE " + proportion + " of " + Enumerated + " assertion(s) — " +
                   "COVERED " + Covered + ", OUT-OF-SCOPE " + OutOfScope + ", DEFERRED " + Deferred +
                   " (oldest " + deferralAge + "), UNTESTABLE-ON-RIG " + UntestableOnRig +
                   ", UNCLASSIFIED " + Unclassified;
        }

        /// <inheritdoc />
        public override string ToString() => Render();
    }

    /// <summary>The whole coverage picture for one wave.</summary>
    public sealed class CoverageReport
    {
        internal CoverageReport(
            ClaimVerdict verdict,
            CoverageFigure figure,
            IReadOnlyList<string> unclassifiedUnits,
            IReadOnlyList<CoverageFinding> findings,
            IReadOnlyList<EnumerationFinding> enumerationFindings,
            IReadOnlyList<AssignmentFinding> assignmentFindings,
            IReadOnlyList<KeyValuePair<string, int>> assertionsPerClause)
        {
            Verdict = verdict;
            Figure = figure;
            UnclassifiedUnits = unclassifiedUnits;
            Findings = findings;
            EnumerationFindings = enumerationFindings;
            AssignmentFindings = assignmentFindings;
            AssertionsPerClause = assertionsPerClause;
        }

        /// <summary>Whether the wave may claim its requirements verified.</summary>
        public ClaimVerdict Verdict { get; }

        /// <summary>The counts. Renderable only as a whole.</summary>
        public CoverageFigure Figure { get; }

        /// <summary>*** THE RESIDUAL, NAMED. *** The units nobody classified.</summary>
        public IReadOnlyList<string> UnclassifiedUnits { get; }

        /// <summary>Citation and coverage findings.</summary>
        public IReadOnlyList<CoverageFinding> Findings { get; }

        /// <summary>What was wrong with the denominator.</summary>
        public IReadOnlyList<EnumerationFinding> EnumerationFindings { get; }

        /// <summary>What was wrong with the classification.</summary>
        public IReadOnlyList<AssignmentFinding> AssignmentFindings { get; }

        /// <summary>Assertions per clause — reported, never judged. No corpus norm exists yet.</summary>
        public IReadOnlyList<KeyValuePair<string, int>> AssertionsPerClause { get; }

        /// <summary>TRUE only for <see cref="ClaimVerdict.MayClaimRequirementsVerified"/>.</summary>
        public bool MayClaim => Verdict == ClaimVerdict.MayClaimRequirementsVerified;

        /// <summary>The report, in full. The figure never appears without its context.</summary>
        public string Describe()
        {
            var lines = new List<string>();

            lines.Add(Verdict == ClaimVerdict.NotEnumerable
                ? "NOT ENUMERABLE — the denominator is unusable, so there is no coverage figure to print. " +
                  "A coverage figure resting on an unenumerable denominator must say so rather than print " +
                  "a percentage."
                : Figure.Render());

            lines.Add("CLAIM: " + (MayClaim
                ? "may claim requirements verified — every assertion was CONSIDERED (not every assertion has a test; that was never the gate)."
                : "MAY NOT claim requirements verified. The gate stops the CLAIM, not the run — these results still stand."));

            if (UnclassifiedUnits.Count > 0)
            {
                lines.Add("UNCLASSIFIED (computed, never written) — " + UnclassifiedUnits.Count + ":");
                lines.AddRange(UnclassifiedUnits.Select(u => "  - " + u));
            }

            lines.AddRange(EnumerationFindings.Select(f => "  ENUM " + f));
            lines.AddRange(AssignmentFindings.Select(f => "  CLASS " + f));
            lines.AddRange(Findings.Select(f => "  COVER " + f));

            if (AssertionsPerClause.Count > 0)
            {
                lines.Add("assertions per clause: " +
                          string.Join(", ", AssertionsPerClause.Select(p => p.Key + "=" + p.Value).ToArray()) +
                          " (reported, not judged — no corpus norm exists yet)");
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        /// <inheritdoc />
        public override string ToString() => Describe();
    }

    /// <summary>
    /// §7 / 6.3 — the four buckets, the residual, and the gate that is `UNCLASSIFIED = 0`.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THE RESIDUAL IS A SET DIFFERENCE AND NOBODY CAN WRITE IT. *** That is the whole of why the
    /// zero is enforceable rather than aspirational:
    /// </para>
    /// <code>
    /// UNCLASSIFIED = enumerated − (COVERED ∪ OUT-OF-SCOPE ∪ DEFERRED ∪ UNTESTABLE-ON-RIG)
    /// </code>
    /// <para>
    /// Forgetting an assertion does not produce a missing tick; it produces a non-zero residual and a
    /// failed gate. The enumeration is the denominator, the classification is a SEPARATE artifact, and
    /// the residual is computed across them — the same shape as phase 2's `AddressesExamined`, which
    /// became a separate denominator because a rule that was correct and never established that it had
    /// run is not a check.
    /// </para>
    /// <para>
    /// WHAT THIS DOES NOT CHECK, and it is judgement rather than an omission: whether the decomposition
    /// is faithful and at the right grain; whether a bucket assignment is honest (§4.3 puts it with a
    /// disinterested party, which does not make it mechanical); whether a vector genuinely exercises
    /// the assertion it cites — only the signal-mention half is mechanical; and whether an assertion is
    /// a correct reading of the clause at all. *** THE GATE PROVES SOMEONE LOOKED, NEVER THAT THEY
    /// LOOKED WELL. ***
    /// </para>
    /// </remarks>
    public static class CoverageAnalyser
    {
        /// <summary>Compute the coverage picture.</summary>
        /// <param name="enumeration">The spec-derived denominator.</param>
        /// <param name="classification">The separate bucket artifact.</param>
        /// <param name="citations">The vectors' `Basis` citations.</param>
        /// <param name="now">For deferral ages.</param>
        /// <param name="blockAuthor">The block's author, for D6 and §4.3.</param>
        /// <param name="previousAssertionCount">
        /// The assertion count of the previous enumeration, or null on a first run. A different count
        /// is reported as its OWN event (§7) and is never absorbed as a classification change.
        /// </param>
        public static CoverageReport Analyse(
            AssertionEnumeration enumeration,
            CoverageClassification classification,
            IEnumerable<VectorCitation>? citations,
            DateTimeOffset now,
            string? blockAuthor = null,
            int? previousAssertionCount = null)
        {
            if (enumeration == null)
            {
                throw new ArgumentNullException(nameof(enumeration));
            }

            if (classification == null)
            {
                throw new ArgumentNullException(nameof(classification));
            }

            var cited = (citations ?? Enumerable.Empty<VectorCitation>()).Where(c => c != null).ToArray();
            var vectorAuthors = cited.Select(c => c.Author).Where(a => a.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            var enumerationFindings = enumeration.Validate(blockAuthor, vectorAuthors);
            var assignmentFindings = classification.Validate(enumeration, blockAuthor, vectorAuthors);
            var findings = new List<CoverageFinding>();

            // --- THE CITATION CHECKS. ---------------------------------------------------------------
            foreach (var citation in cited)
            {
                CheckCitation(enumeration, citation, blockAuthor, findings);
            }

            // --- THE RESIDUAL. A SET DIFFERENCE, COMPUTED, NEVER WRITTEN. ---------------------------
            var units = enumeration.Units;
            var classified = classification.ClassifiedUnitKeys;

            var unclassified = units
                .Select(u => u.Key)
                .Where(k => !classified.Contains(k))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();

            if (unclassified.Length > 0)
            {
                findings.Add(new CoverageFinding(
                    CoverageDefect.UnclassifiedResidual,
                    unclassified.Length + " unit(s)",
                    "Enumerated but in no bucket. NOBODY WROTE 'unclassified' — it is what remained after " +
                    "differencing the enumeration against the classification, which is why forgetting an " +
                    "assertion fails the gate instead of producing a missing tick."));
            }

            // --- COVERED versus CITED, both directions. ---------------------------------------------
            var citedKeys = new HashSet<string>(cited.Select(c => c.UnitKey), StringComparer.Ordinal);
            var knownUnits = new HashSet<string>(units.Select(u => u.Key), StringComparer.Ordinal);

            foreach (var assignment in classification.Assignments
                .Where(a => a.Bucket == CoverageBucket.Covered && a.UnitKey.Length > 0 && knownUnits.Contains(a.UnitKey)))
            {
                if (!citedKeys.Contains(assignment.UnitKey))
                {
                    findings.Add(new CoverageFinding(
                        CoverageDefect.CoveredWithoutACitation,
                        assignment.UnitKey,
                        "Bucketed COVERED, but no vector cites it. COVERED means 'a vector exists and " +
                        "cites this assertion'; asserting it without one is the classification claiming " +
                        "the numerator's own fact."));
                }
            }

            foreach (var key in citedKeys.Where(knownUnits.Contains))
            {
                var assigned = classification.Assignments
                    .Where(a => string.Equals(a.UnitKey, key, StringComparison.Ordinal))
                    .Select(a => a.Bucket)
                    .Distinct()
                    .ToArray();

                if (assigned.Length > 0 && !assigned.Contains(CoverageBucket.Covered))
                {
                    findings.Add(new CoverageFinding(
                        CoverageDefect.CitedButNotBucketedCovered,
                        key,
                        "A vector cites it, but it is bucketed " + string.Join("/", assigned.Select(b => b.ToString()).ToArray()) +
                        ". A cited assertion sitting in an escape-hatch bucket is the laundering route " +
                        "arriving from the other direction."));
                }
            }

            // --- RE-DECOMPOSITION IS ITS OWN EVENT. -------------------------------------------------
            if (previousAssertionCount.HasValue && previousAssertionCount.Value != enumeration.Assertions.Count)
            {
                findings.Add(new CoverageFinding(
                    CoverageDefect.RedecompositionChangedTheCount,
                    previousAssertionCount.Value + " -> " + enumeration.Assertions.Count,
                    "The decomposition yields a different assertion count than last time. §7 requires this " +
                    "as its OWN event: a clause previously read as one assertion that now decomposes into " +
                    "two produces a NEW, UNCLASSIFIED assertion, and it is explicitly not absorbed as a " +
                    "classification change."));
            }

            // --- THE FIGURE. ------------------------------------------------------------------------
            var validAssignments = classification.Assignments
                .Where(a => a.UnitKey.Length > 0 && knownUnits.Contains(a.UnitKey))
                .GroupBy(a => a.UnitKey, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToArray();

            var oldest = classification.Assignments
                .Select(a => a.Age(now))
                .Where(a => a.HasValue)
                .Select(a => a!.Value)
                .DefaultIfEmpty()
                .Max();

            var figure = new CoverageFigure(
                validAssignments.Count(a => a.Bucket == CoverageBucket.Covered),
                validAssignments.Count(a => a.Bucket == CoverageBucket.OutOfScope),
                validAssignments.Count(a => a.Bucket == CoverageBucket.Deferred),
                validAssignments.Count(a => a.Bucket == CoverageBucket.UntestableOnRig),
                unclassified.Length,
                oldest == default(TimeSpan) ? (TimeSpan?)null : oldest);

            // --- THE VERDICT. -----------------------------------------------------------------------
            var verdict = enumerationFindings.Count > 0
                ? ClaimVerdict.NotEnumerable
                : (unclassified.Length == 0 && assignmentFindings.Count == 0 && findings.Count == 0
                    ? ClaimVerdict.MayClaimRequirementsVerified
                    : ClaimVerdict.MayNotClaimRequirementsVerified);

            return new CoverageReport(
                verdict,
                figure,
                unclassified,
                findings,
                enumerationFindings,
                assignmentFindings,
                enumeration.AssertionsPerClause);
        }

        private static void CheckCitation(
            AssertionEnumeration enumeration,
            VectorCitation citation,
            string? blockAuthor,
            List<CoverageFinding> findings)
        {
            var subject = citation.VectorId.Length == 0 ? "<unnamed vector>" : citation.VectorId;

            if (!string.IsNullOrWhiteSpace(blockAuthor) &&
                string.Equals(blockAuthor!.Trim(), citation.Author, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new CoverageFinding(
                    CoverageDefect.VectorAuthorIsTheBlockAuthor,
                    subject,
                    "The vector's author is the block's author. An agent authoring both a block and its " +
                    "tests encodes its misreading into both, and the wave returns green having verified " +
                    "only self-consistency (D6)."));
            }

            if (AssertionId.IsDisplayOrdinalForm(citation.AssertionIdCited))
            {
                findings.Add(new CoverageFinding(
                    CoverageDefect.CitationUsesTheDisplayOrdinalForm,
                    subject + " -> " + citation.AssertionIdCited,
                    "The display ordinal is display only, and is rejected by shape precisely because it " +
                    "is the readable one and would otherwise be the one people type. It is positional, so " +
                    "inserting an assertion would silently re-point every citation using it."));
                return;
            }

            var assertion = enumeration.Find(citation.AssertionIdCited);

            if (assertion == null)
            {
                // *** STALE AND MISSING ARE TWO STATES AND MUST NOT COLLAPSE. *** An ID nothing
                // superseded never existed. An ID something records as superseded was REWORDED, and the
                // vector was written against the old words.
                var successor = enumeration.FindSuccessorOf(citation.AssertionIdCited);

                findings.Add(successor == null
                    ? new CoverageFinding(
                        CoverageDefect.CitationIsMissing,
                        subject + " -> " + citation.AssertionIdCited,
                        "The enumeration contains no such assertion and nothing records it as superseded, " +
                        "so it never existed. A citation is an error in the VECTOR, never an extension of " +
                        "the denominator.")
                    : new CoverageFinding(
                        CoverageDefect.CitationIsStale,
                        subject + " -> " + citation.AssertionIdCited,
                        "STALE, not missing: '" + successor.Id + "' records this ID as the one it " +
                        "superseded, so the assertion was REWORDED. The vector was written against the " +
                        "old words and has to be re-read against the new ones — which is the entire " +
                        "reason editing text changes the ID."));
                return;
            }

            if (citation.DeclaredForm != assertion.Form)
            {
                findings.Add(new CoverageFinding(
                    CoverageDefect.DeclaredFormDisagreesWithTheEnumeration,
                    subject + " -> " + assertion.Id,
                    "The vector declares '" + citation.DeclaredForm + "' and the enumeration says '" +
                    assertion.Form + "'. Citing a NEVER while declaring a When takes the permissive path, " +
                    "and nothing else in the pipeline compares the two — the vector declares its own form."));
            }

            if (assertion.ResponseSignal.Length > 0 &&
                !citation.ExpectationSignals.Any(s => string.Equals(s, assertion.ResponseSignal, StringComparison.Ordinal)))
            {
                findings.Add(new CoverageFinding(
                    CoverageDefect.ResponseSignalAbsentFromExpectations,
                    subject + " -> " + assertion.Id,
                    "The assertion's response names '" + assertion.ResponseSignal + "', which appears " +
                    "nowhere in this vector's expectations. Mentioning a signal is not testing it, but a " +
                    "vector that never mentions it could not possibly be testing the assertion it cites."));
            }

            if (citation.Instance.Length > 0)
            {
                var clause = enumeration.Clauses.FirstOrDefault(c =>
                    c.Assertions.Any(a => string.Equals(a.Id, assertion.Id, StringComparison.Ordinal)));

                if (clause != null && !clause.Instances.Contains(citation.Instance, StringComparer.Ordinal))
                {
                    findings.Add(new CoverageFinding(
                        CoverageDefect.InstanceNotDeclaredByTheClause,
                        subject + " -> " + citation.UnitKey,
                        "The clause declares instance(s) " +
                        (clause.Instances.Count == 0 ? "<none — it is class-level>" : string.Join(", ", clause.Instances.ToArray())) +
                        " and this citation names '" + citation.Instance + "'. The instance set is " +
                        "spec-side: if a citation could add one, an instance nobody wrote a vector for " +
                        "could not be missing, which is the self-referential trap by a side door."));
                }
            }
        }
    }
}
