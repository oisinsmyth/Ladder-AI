using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// THE NUMERATOR SIDE — one vector's `Basis` citation, plus the expectation signals it declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THIS IS THE ONLY SIDE THE TEST AUTHOR CONTROLS, AND IT IS THE NUMERATOR. *** Coverage is a
    /// ratio of two spec-side counts (enumerated, classified) against one vector-side count (cited).
    /// The denominator is produced before any vector exists, by somebody else. Every inflation attack
    /// in `assertion-enumeration.md` §5 is therefore an attack on the DENOMINATOR — which is why they
    /// all have to route through a second party, and why that structural fact is worth more than any
    /// individual check.
    /// </para>
    /// <para>
    /// A citation SELECTS from the enumeration. It cannot extend it: a vector naming an ID the
    /// enumeration does not contain is an error in the vector, never a new assertion.
    /// </para>
    /// </remarks>
    public sealed class VectorCitation
    {
        /// <param name="vectorId">The vector.</param>
        /// <param name="author">Its author (D6 — must differ from the block's author).</param>
        /// <param name="clauseId">The clause cited.</param>
        /// <param name="assertionId">The assertion cited, in ID form. The ordinal form is rejected.</param>
        /// <param name="instance">Which instance this vector is an index of, or empty for class-level.</param>
        /// <param name="declaredForm">
        /// The form the VECTOR declares. Compared against the enumerated form — see
        /// <see cref="CoverageDefect.DeclaredFormDisagreesWithTheEnumeration"/>, which is the gap the
        /// enumeration's own producer could not close because the gate's enumeration was flat strings.
        /// </param>
        /// <param name="expectationSignals">The tags this vector's `Expectations` name.</param>
        public VectorCitation(
            string? vectorId,
            string? author,
            string? clauseId,
            string? assertionId,
            string? instance = null,
            AssertionForm declaredForm = AssertionForm.Unstated,
            IEnumerable<string>? expectationSignals = null)
        {
            VectorId = (vectorId ?? string.Empty).Trim();
            Author = (author ?? string.Empty).Trim();
            ClauseId = (clauseId ?? string.Empty).Trim();
            AssertionIdCited = (assertionId ?? string.Empty).Trim();
            Instance = (instance ?? string.Empty).Trim();
            DeclaredForm = declaredForm;
            ExpectationSignals = (expectationSignals ?? Enumerable.Empty<string>())
                .Select(s => (s ?? string.Empty).Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>The vector.</summary>
        public string VectorId { get; }

        /// <summary>Its author.</summary>
        public string Author { get; }

        /// <summary>The clause cited.</summary>
        public string ClauseId { get; }

        /// <summary>The assertion ID cited.</summary>
        public string AssertionIdCited { get; }

        /// <summary>The instance, or empty.</summary>
        public string Instance { get; }

        /// <summary>The form the vector declares.</summary>
        public AssertionForm DeclaredForm { get; }

        /// <summary>The tags the vector's expectations name.</summary>
        public IReadOnlyList<string> ExpectationSignals { get; }

        /// <summary>The coverage unit this citation claims.</summary>
        public string UnitKey =>
            Instance.Length == 0 ? AssertionIdCited : AssertionIdCited + "@" + Instance;

        /// <inheritdoc />
        public override string ToString() => VectorId + " cites " + UnitKey;
    }
}
