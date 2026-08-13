using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>One assertion of one clause, as the spec-side enumeration produced it.</summary>
    public sealed class EnumeratedAssertion
    {
        /// <param name="clauseId">The clause's stable ID from the requirements register.</param>
        /// <param name="text">The assertion, in one of the two canonical forms.</param>
        /// <param name="form">Which form it claims.</param>
        /// <param name="responseSignal">
        /// The observable signal the response names. It is what makes inflation route 5 partly
        /// mechanical: the citing vector's expectations must mention it.
        /// </param>
        /// <param name="declaredId">
        /// The ID as written in the artifact, or null to compute it. Supplying it is the normal case —
        /// it is what lets <see cref="AssertionEnumeration"/> catch a HAND-EDITED ID by rehashing.
        /// </param>
        /// <param name="supersedes">
        /// The ID this assertion replaces, when its text was edited. The link is what keeps STALE and
        /// MISSING apart for every prior citation.
        /// </param>
        public EnumeratedAssertion(
            string? clauseId,
            string? text,
            AssertionForm form,
            string? responseSignal,
            string? declaredId = null,
            string? supersedes = null)
        {
            ClauseId = (clauseId ?? string.Empty).Trim();
            Text = (text ?? string.Empty).Trim();
            Form = form;
            ResponseSignal = (responseSignal ?? string.Empty).Trim();
            DeclaredId = (declaredId ?? string.Empty).Trim();
            Supersedes = (supersedes ?? string.Empty).Trim();

            NormalisedText = AssertionId.Normalise(Text);
            ComputedId = ClauseId.Length == 0 || NormalisedText.Length == 0
                ? string.Empty
                : AssertionId.Compute(ClauseId, Text);
        }

        /// <summary>The owning clause.</summary>
        public string ClauseId { get; }

        /// <summary>The assertion text.</summary>
        public string Text { get; }

        /// <summary>Its normalised form, as hashed.</summary>
        public string NormalisedText { get; }

        /// <summary>The form it claims.</summary>
        public AssertionForm Form { get; }

        /// <summary>The observable signal named by the response.</summary>
        public string ResponseSignal { get; }

        /// <summary>The ID as written in the artifact; empty when the caller left it to be computed.</summary>
        public string DeclaredId { get; }

        /// <summary>The ID as recomputed from the text. The one that counts.</summary>
        public string ComputedId { get; }

        /// <summary>What this assertion replaced, when its text was edited.</summary>
        public string Supersedes { get; }

        /// <summary>The effective ID — always the computed one.</summary>
        public string Id => ComputedId;

        /// <inheritdoc />
        public override string ToString() => Id + " [" + Form + "] " + Text;
    }

    /// <summary>One clause and its assertions, plus the instances the clause applies to.</summary>
    public sealed class EnumeratedClause
    {
        /// <param name="clauseId">The stable clause ID.</param>
        /// <param name="assertions">Its assertions.</param>
        /// <param name="instances">
        /// The instances this clause applies to (R4). EMPTY means the clause is class-level and its
        /// assertions are one coverage unit each.
        /// </param>
        public EnumeratedClause(string? clauseId, IEnumerable<EnumeratedAssertion>? assertions, IEnumerable<string>? instances = null)
        {
            ClauseId = (clauseId ?? string.Empty).Trim();
            Assertions = (assertions ?? Enumerable.Empty<EnumeratedAssertion>()).Where(a => a != null).ToArray();
            Instances = (instances ?? Enumerable.Empty<string>())
                .Select(i => (i ?? string.Empty).Trim())
                .Where(i => i.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>The stable clause ID.</summary>
        public string ClauseId { get; }

        /// <summary>Its assertions.</summary>
        public IReadOnlyList<EnumeratedAssertion> Assertions { get; }

        /// <summary>The instances it applies to; empty for a class-level clause.</summary>
        public IReadOnlyList<string> Instances { get; }
    }

    /// <summary>One coverage unit — an assertion, qualified by instance (R4, §7/D35).</summary>
    public sealed class CoverageUnit : IEquatable<CoverageUnit>
    {
        internal CoverageUnit(string assertionId, string instance)
        {
            AssertionId = assertionId;
            Instance = instance;
        }

        /// <summary>The assertion.</summary>
        public string AssertionId { get; }

        /// <summary>The instance, or empty for a class-level unit.</summary>
        public string Instance { get; }

        /// <summary><c>REQ-014:3f9a1c@Feeder_02</c>, or the bare ID when class-level.</summary>
        public string Key => Instance.Length == 0 ? AssertionId : AssertionId + "@" + Instance;

        /// <inheritdoc />
        public bool Equals(CoverageUnit? other) =>
            other != null && string.Equals(Key, other.Key, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as CoverageUnit);

        /// <inheritdoc />
        public override int GetHashCode() => Key.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => Key;
    }

    /// <summary>Why an enumeration could not be used as a denominator.</summary>
    public enum EnumerationDefect
    {
        /// <summary>Not a defect.</summary>
        None = 0,

        /// <summary>The enumeration parsed zero assertions. Nothing examined — never a pass (FI-44).</summary>
        Empty = 1,

        /// <summary>A clause carries no assertions. An error in the decomposition, not a clause with nothing to say.</summary>
        ClauseWithNoAssertions = 2,

        /// <summary>A clause has no stable ID, so every assertion ID under it inherits the instability.</summary>
        ClauseNotStablyIdentified = 3,

        /// <summary>The ID written in the artifact does not recompute from the text — a hand-edited ID.</summary>
        IdDoesNotRecompute = 4,

        /// <summary>Two assertions in one clause normalise identically. A duplicate, reported as an error.</summary>
        DuplicateAssertion = 5,

        /// <summary>The text does not wear the canonical form it claims, or claims none.</summary>
        TextDoesNotMatchItsForm = 6,

        /// <summary>An assertion names no response signal, so no citation to it could ever be checked.</summary>
        NoResponseSignal = 7,

        /// <summary>
        /// The enumerator is the block's author or a vector's author. D6 is lost AT THE DENOMINATOR,
        /// which undoes the entire argument — §7's own open item 3.
        /// </summary>
        EnumeratorIsNotIndependent = 8,
    }

    /// <summary>One thing wrong with an enumeration.</summary>
    public sealed class EnumerationFinding
    {
        internal EnumerationFinding(EnumerationDefect defect, string subject, string detail)
        {
            Defect = defect;
            Subject = subject;
            Detail = detail;
        }

        /// <summary>Which defect.</summary>
        public EnumerationDefect Defect { get; }

        /// <summary>The clause or assertion it is about.</summary>
        public string Subject { get; }

        /// <summary>What is wrong.</summary>
        public string Detail { get; }

        /// <inheritdoc />
        public override string ToString() => Defect + " [" + Subject + "]: " + Detail;
    }

    /// <summary>
    /// THE DENOMINATOR. The spec-derived assertion enumeration, produced before any vector exists, by
    /// somebody who is neither the block's author nor a vector's author.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THE DENOMINATOR COMES FROM THE SPECIFICATION, NEVER FROM THE TEST SUITE. *** Any unit
    /// defined by what somebody wrote a vector for is self-referential: you cannot be missing an
    /// assertion nobody wrote, so coverage is always 100% and the gate is theatre. This type holds the
    /// spec side; <see cref="VectorCitation"/> holds the vector side; and nothing here can be extended
    /// by a citation — a vector naming an ID this does not contain is an ERROR IN THE VECTOR.
    /// </para>
    /// <para>
    /// *** THE INSTANCE SET IS SPEC-SIDE, AND THAT IS A READING THIS TYPE HAD TO TAKE. ***
    /// `assertion-enumeration.md` R4 says "instance qualification happens at citation time; the
    /// enumeration itself is per class, and instances multiply it". Read literally, the DENOMINATOR
    /// would then depend on what the citations mention — and an instance nobody wrote a vector for
    /// could not be missing, which is precisely the self-referential trap §7 forbids, arriving by a
    /// side door. So <see cref="EnumeratedClause.Instances"/> is declared by the ENUMERATOR from the
    /// clause text ("each of the three feeders"), and citations select from it rather than adding to
    /// it. Recorded as a reading, not a correction.
    /// </para>
    /// <para>
    /// EMPTY IS NOT CLEAN (FI-44). An enumeration that parses zero assertions is *nothing examined*
    /// and is a defect, never a pass. A clause with no assertions is an error in the decomposition.
    /// </para>
    /// </remarks>
    public sealed class AssertionEnumeration
    {
        /// <param name="clauses">The decomposed clauses.</param>
        /// <param name="enumeratorIdentity">
        /// Who produced it. Required: §7's open item 3 says that if the block's author performs the
        /// enumeration, D6's independence is lost at the denominator — so the identity has to be
        /// recorded before it can be checked.
        /// </param>
        /// <param name="registerSource">Which requirements register it was derived from.</param>
        public AssertionEnumeration(
            IEnumerable<EnumeratedClause>? clauses,
            string? enumeratorIdentity,
            string? registerSource)
        {
            Clauses = (clauses ?? Enumerable.Empty<EnumeratedClause>()).Where(c => c != null).ToArray();
            EnumeratorIdentity = (enumeratorIdentity ?? string.Empty).Trim();
            RegisterSource = (registerSource ?? string.Empty).Trim();
        }

        /// <summary>The decomposed clauses.</summary>
        public IReadOnlyList<EnumeratedClause> Clauses { get; }

        /// <summary>Who produced the enumeration.</summary>
        public string EnumeratorIdentity { get; }

        /// <summary>Which register it came from.</summary>
        public string RegisterSource { get; }

        /// <summary>Every assertion, across every clause.</summary>
        public IReadOnlyList<EnumeratedAssertion> Assertions =>
            Clauses.SelectMany(c => c.Assertions).ToArray();

        /// <summary>Assertions per clause, reported and never judged — no corpus norm exists yet.</summary>
        public IReadOnlyList<KeyValuePair<string, int>> AssertionsPerClause =>
            Clauses.Select(c => new KeyValuePair<string, int>(c.ClauseId, c.Assertions.Count)).ToArray();

        /// <summary>
        /// THE COVERAGE UNITS — the denominator proper. Every assertion multiplied by the instances its
        /// clause declares (R4), or the bare assertion when the clause is class-level.
        /// </summary>
        public IReadOnlyList<CoverageUnit> Units
        {
            get
            {
                var units = new List<CoverageUnit>();

                foreach (var clause in Clauses)
                {
                    foreach (var assertion in clause.Assertions.Where(a => a.Id.Length > 0))
                    {
                        if (clause.Instances.Count == 0)
                        {
                            units.Add(new CoverageUnit(assertion.Id, string.Empty));
                            continue;
                        }

                        foreach (var instance in clause.Instances)
                        {
                            units.Add(new CoverageUnit(assertion.Id, instance));
                        }
                    }
                }

                return units;
            }
        }

        /// <summary>Looks an assertion up by ID.</summary>
        public EnumeratedAssertion? Find(string? assertionId)
        {
            var id = (assertionId ?? string.Empty).Trim();
            return id.Length == 0
                ? null
                : Assertions.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));
        }

        /// <summary>
        /// TRUE when some assertion records <paramref name="assertionId"/> as the one it superseded —
        /// i.e. the citation is STALE (the text was reworded) rather than MISSING (it never existed).
        /// </summary>
        public EnumeratedAssertion? FindSuccessorOf(string? assertionId)
        {
            var id = (assertionId ?? string.Empty).Trim();
            return id.Length == 0
                ? null
                : Assertions.FirstOrDefault(a => string.Equals(a.Supersedes, id, StringComparison.Ordinal));
        }

        /// <summary>
        /// Validates the enumeration as a denominator. An enumeration with findings is NOT ENUMERABLE
        /// and the correct output is "not enumerable", never a percentage.
        /// </summary>
        /// <param name="blockAuthor">The block's author, for the independence check (D6).</param>
        /// <param name="vectorAuthors">Every vector author, for the same check.</param>
        public IReadOnlyList<EnumerationFinding> Validate(string? blockAuthor = null, IEnumerable<string>? vectorAuthors = null)
        {
            var findings = new List<EnumerationFinding>();

            if (Assertions.Count == 0)
            {
                findings.Add(new EnumerationFinding(
                    EnumerationDefect.Empty,
                    RegisterSource.Length == 0 ? "<no register named>" : RegisterSource,
                    "The enumeration carries no assertions. That is NOTHING EXAMINED, not a clean " +
                    "denominator — an empty enumeration would make every suite complete by construction " +
                    "(FI-44)."));
            }

            foreach (var clause in Clauses)
            {
                if (clause.ClauseId.Length == 0)
                {
                    findings.Add(new EnumerationFinding(
                        EnumerationDefect.ClauseNotStablyIdentified,
                        "<unnamed clause>",
                        "A clause carries no stable ID. Assertion IDs are (clause identity, content), so " +
                        "an unstable clause ID makes every assertion under it unstable too."));
                }

                if (clause.Assertions.Count == 0)
                {
                    findings.Add(new EnumerationFinding(
                        EnumerationDefect.ClauseWithNoAssertions,
                        clause.ClauseId.Length == 0 ? "<unnamed clause>" : clause.ClauseId,
                        "The clause decomposed to nothing. A clause with no assertions is an error in the " +
                        "decomposition, not a clause with nothing to say."));
                }

                foreach (var duplicate in clause.Assertions
                    .Where(a => a.NormalisedText.Length > 0)
                    .GroupBy(a => a.NormalisedText, StringComparer.Ordinal)
                    .Where(g => g.Count() > 1))
                {
                    findings.Add(new EnumerationFinding(
                        EnumerationDefect.DuplicateAssertion,
                        clause.ClauseId + " / " + duplicate.Key,
                        "Two assertions in this clause normalise identically. That is a duplicate — an " +
                        "error in the enumeration — and not an ID collision."));
                }

                foreach (var assertion in clause.Assertions)
                {
                    var subject = assertion.Id.Length > 0 ? assertion.Id : clause.ClauseId + " / " + assertion.Text;

                    if (assertion.DeclaredId.Length > 0 &&
                        !string.Equals(assertion.DeclaredId, assertion.ComputedId, StringComparison.Ordinal))
                    {
                        findings.Add(new EnumerationFinding(
                            EnumerationDefect.IdDoesNotRecompute,
                            subject,
                            "The artifact declares '" + assertion.DeclaredId + "' but the text hashes to '" +
                            assertion.ComputedId + "'. A hand-edited ID is exactly what rehashing exists " +
                            "to catch: it would let an edited assertion keep an old ID and inherit its " +
                            "citations without anybody re-reading them."));
                    }

                    if (!AssertionId.TextMatchesForm(assertion.Text, assertion.Form))
                    {
                        findings.Add(new EnumerationFinding(
                            EnumerationDefect.TextDoesNotMatchItsForm,
                            subject,
                            "The text does not wear the '" + assertion.Form + "' form it claims. The " +
                            "template is not style — anything that fits neither canonical form is a clause " +
                            "that has not been decomposed yet."));
                    }

                    if (assertion.ResponseSignal.Length == 0)
                    {
                        findings.Add(new EnumerationFinding(
                            EnumerationDefect.NoResponseSignal,
                            subject,
                            "The assertion names no response signal, so no citation to it could ever be " +
                            "checked against a vector's expectations — the one half of 'does this vector " +
                            "actually test what it cites' that is mechanical."));
                    }
                }
            }

            var authors = new List<string>();
            if (!string.IsNullOrWhiteSpace(blockAuthor))
            {
                authors.Add(blockAuthor!.Trim());
            }

            authors.AddRange((vectorAuthors ?? Enumerable.Empty<string>())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a!.Trim()));

            if (EnumeratorIdentity.Length == 0 && authors.Count > 0)
            {
                findings.Add(new EnumerationFinding(
                    EnumerationDefect.EnumeratorIsNotIndependent,
                    "<no enumerator recorded>",
                    "The enumeration records no author, so independence cannot be checked. An " +
                    "unrecorded identity is not an independent one."));
            }
            else if (authors.Any(a => string.Equals(a, EnumeratorIdentity, StringComparison.OrdinalIgnoreCase)))
            {
                findings.Add(new EnumerationFinding(
                    EnumerationDefect.EnumeratorIsNotIndependent,
                    EnumeratorIdentity,
                    "The enumerator is also the block's author or a vector's author. D6's independence " +
                    "is then lost AT THE DENOMINATOR: whoever decides what the code does would also " +
                    "decide what counts as the full set of things it must do, and coverage becomes " +
                    "unfalsifiable."));
            }

            return findings;
        }
    }
}
