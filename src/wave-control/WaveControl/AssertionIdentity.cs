using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ladder.Wave
{
    /// <summary>
    /// Which of the two canonical forms an assertion is written in. There are exactly two, and
    /// anything that fits neither is a clause that has not been decomposed yet.
    /// </summary>
    /// <remarks>
    /// *** THE ZERO VALUE IS <see cref="Unstated"/>, AND IT MATTERS MORE HERE THAN IT LOOKS. *** The
    /// vector declares a form of its own, and the enumeration records the form the assertion actually
    /// has. Comparing them is what closes the "cite a NEVER while declaring a When" route — so a
    /// dropped or defaulted form must land on a value that matches NEITHER enumerated form, rather
    /// than on the permissive one.
    /// </remarks>
    public enum AssertionForm
    {
        /// <summary>Nobody said. Matches no enumerated form, so a comparison against it always fails.</summary>
        Unstated = 0,

        /// <summary><c>WHEN &lt;trigger&gt; THEN &lt;observable response&gt; [WITHIN &lt;bound&gt; | FOR &lt;duration&gt;]</c>.</summary>
        When = 1,

        /// <summary><c>NEVER &lt;forbidden observable state&gt;</c> — interlocks and prohibitions, which have no natural trigger.</summary>
        Never = 2,
    }

    /// <summary>
    /// The assertion ID scheme: a stable clause ID plus a content hash, and NOTHING POSITIONAL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>REQ-014:3f9a1c</c> — the clause's stable ID from the requirements register, then the first
    /// six lowercase hex of <c>SHA-256(normalised assertion text)</c>.
    /// </para>
    /// <para>
    /// *** THE ID DEPENDS ONLY ON (CLAUSE IDENTITY, ASSERTION CONTENT). *** Inserting a clause above
    /// shifts nothing; inserting an assertion into a clause shifts none of its siblings. The scheme
    /// supersedes §7's sketch of <c>(clause hash, assertion index, assertion text hash)</c>, whose
    /// middle term is positional — insert an assertion at index 1 and every later index shifts, so a
    /// stored classification or a citation silently comes to name a different assertion.
    /// </para>
    /// <para>
    /// *** EDITING AN ASSERTION'S TEXT CHANGES ITS ID, DELIBERATELY. *** The old ID dangles and every
    /// prior citation to it is flagged STALE. A citation that silently survives a rewording of what it
    /// cites is the failure this avoids: the vector was written against the old words and nobody
    /// re-read it. <see cref="CoverageAnalyser"/> keeps STALE and MISSING as two different findings
    /// and refuses to collapse them.
    /// </para>
    /// </remarks>
    public static class AssertionId
    {
        /// <summary>How many hex characters of the digest the ID carries.</summary>
        public const int HashLength = 6;

        /// <summary>
        /// The normalisation, specified so two implementations agree: trim; collapse internal
        /// whitespace runs to a single space; strip ONE trailing '.' or ';'; preserve case and
        /// everything else.
        /// </summary>
        /// <remarks>
        /// *** CASE IS PRESERVED DELIBERATELY. *** Folding it risks merging two distinct signal names,
        /// and two assertions that differ only by the case of a tag are two assertions.
        /// </remarks>
        public static string Normalise(string? text)
        {
            var raw = (text ?? string.Empty).Trim();

            var sb = new StringBuilder(raw.Length);
            var inWhitespace = false;

            foreach (var c in raw)
            {
                if (char.IsWhiteSpace(c))
                {
                    inWhitespace = true;
                    continue;
                }

                if (inWhitespace && sb.Length > 0)
                {
                    sb.Append(' ');
                }

                inWhitespace = false;
                sb.Append(c);
            }

            var collapsed = sb.ToString();

            if (collapsed.Length > 0)
            {
                var last = collapsed[collapsed.Length - 1];
                if (last == '.' || last == ';')
                {
                    collapsed = collapsed.Substring(0, collapsed.Length - 1).TrimEnd();
                }
            }

            return collapsed;
        }

        /// <summary>Computes the full ID for an assertion of <paramref name="clauseId"/>.</summary>
        public static string Compute(string? clauseId, string? assertionText)
        {
            var clause = (clauseId ?? string.Empty).Trim();
            if (clause.Length == 0)
            {
                throw new ArgumentException(
                    "An assertion ID needs a stable clause ID. If the requirements register addresses " +
                    "clauses positionally ('§3.2, fourth paragraph') then pin stable clause IDs first — " +
                    "otherwise the assertion IDs inherit the instability they exist to avoid.",
                    nameof(clauseId));
            }

            return clause + ":" + HashOf(Normalise(assertionText));
        }

        /// <summary>The six-hex content hash on its own.</summary>
        public static string HashOf(string normalisedText)
        {
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(normalisedText));

                var sb = new StringBuilder(HashLength);
                for (var i = 0; i < digest.Length && sb.Length < HashLength; i++)
                {
                    sb.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return sb.ToString(0, HashLength);
            }
        }

        /// <summary>Splits an ID into its clause part and its hash part. False for anything malformed.</summary>
        public static bool TryParse(string? id, out string clauseId, out string hash)
        {
            clauseId = string.Empty;
            hash = string.Empty;

            var text = (id ?? string.Empty).Trim();
            var colon = text.LastIndexOf(':');
            if (colon <= 0 || colon == text.Length - 1)
            {
                return false;
            }

            var candidateHash = text.Substring(colon + 1);
            if (candidateHash.Length != HashLength)
            {
                return false;
            }

            foreach (var c in candidateHash)
            {
                var isLowerHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!isLowerHex)
                {
                    return false;
                }
            }

            clauseId = text.Substring(0, colon);
            hash = candidateHash;
            return true;
        }

        /// <summary>
        /// TRUE for the DISPLAY-ORDINAL form — <c>REQ-014.A2</c>. Listings show it because
        /// <c>REQ-014:3f9a1c</c> is unreadable aloud; *** a citation in this form is REJECTED,
        /// mechanically, by shape, precisely because it is the readable one and would otherwise be the
        /// one people type. ***
        /// </summary>
        public static bool IsDisplayOrdinalForm(string? citation)
        {
            var text = (citation ?? string.Empty).Trim();

            var dot = text.LastIndexOf('.');
            if (dot <= 0 || dot == text.Length - 1)
            {
                return false;
            }

            var tail = text.Substring(dot + 1);
            if (tail.Length < 2 || (tail[0] != 'A' && tail[0] != 'a'))
            {
                return false;
            }

            for (var i = 1; i < tail.Length; i++)
            {
                if (!char.IsDigit(tail[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks that the text actually wears the form it claims. The template is not style — it is
        /// what makes the decomposition rules mechanically visible, so a text that does not match its
        /// declared form is a clause that has not been decomposed.
        /// </summary>
        public static bool TextMatchesForm(string? text, AssertionForm form)
        {
            var normalised = Normalise(text);

            switch (form)
            {
                case AssertionForm.When:
                    return normalised.StartsWith("WHEN ", StringComparison.Ordinal) &&
                           normalised.IndexOf(" THEN ", StringComparison.Ordinal) > 0;

                case AssertionForm.Never:
                    return normalised.StartsWith("NEVER ", StringComparison.Ordinal) &&
                           normalised.IndexOf(" THEN ", StringComparison.Ordinal) < 0;

                default:
                    return false;
            }
        }
    }
}
