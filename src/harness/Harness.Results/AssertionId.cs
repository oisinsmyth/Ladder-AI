using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Harness.Results;

/// <summary>
/// The assertion ID scheme (`assertion-enumeration.md` §3.1–§3.3): a stable clause ID plus a content
/// hash, and <b>nothing positional</b>.
///
/// <para><c>REQ-014:3f9a1c</c> — the clause's stable ID from the requirements register, then the first
/// six lowercase hex of <c>SHA-256(normalised assertion text)</c>.</para>
///
/// <para><b>THE ID DEPENDS ONLY ON (CLAUSE IDENTITY, ASSERTION CONTENT).</b> Inserting a clause above
/// shifts nothing; inserting an assertion into a clause shifts none of its siblings. This supersedes
/// §7's sketch of <c>(clause hash, assertion index, assertion text hash)</c>, whose middle term is
/// positional — insert an assertion at index 1 and every later index shifts, so a stored classification
/// or a citation silently comes to name a different assertion.</para>
///
/// <para>*** EDITING AN ASSERTION'S TEXT CHANGES ITS ID, DELIBERATELY. *** The old ID dangles, every
/// prior citation to it is stale, and the enumeration records a <c>supersedes:</c> link. A citation that
/// silently survives a rewording of what it cites is the failure this exists to avoid — the vector was
/// written against the old words and nobody re-read it.</para>
///
/// <para><b>Duplicated implementation, recorded rather than hidden:</b> <c>Ladder.Wave</c>'s
/// <c>AssertionId</c> in <c>src/wave-control/</c> implements the same scheme. That lane is held by
/// somebody else, so this is not consolidated here. <see cref="Harness.Results"/>'s copy is pinned by
/// KNOWN-ANSWER VECTORS (<c>AssertionIdTests</c>) computed independently of both, so the two can be
/// checked against each other rather than merely assumed to agree.</para>
/// </summary>
public static class AssertionId
{
    /// <summary>How many hex characters of the digest the ID carries.</summary>
    public const int HashLength = 6;

    /// <summary>
    /// The normalisation, specified so two implementations agree (§3.1): trim; collapse internal
    /// whitespace runs to a single space; strip ONE trailing <c>.</c> or <c>;</c>; <b>preserve case and
    /// everything else</b>.
    /// </summary>
    /// <remarks>
    /// *** CASE IS PRESERVED DELIBERATELY. *** Folding it risks merging two distinct signal names, and
    /// two assertions differing only by the case of a tag are two assertions.
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
                sb.Append(' ');

            inWhitespace = false;
            sb.Append(c);
        }

        var collapsed = sb.ToString();

        // ONE trailing '.' or ';', never a run: "…threshold.." keeps one dot, because two is a typo the
        // enumerator can see and a silent strip of both would make two visibly different texts collide.
        if (collapsed.Length > 0 && (collapsed[^1] == '.' || collapsed[^1] == ';'))
            collapsed = collapsed[..^1].TrimEnd();

        return collapsed;
    }

    /// <summary>The full ID for an assertion of <paramref name="clauseId"/>.</summary>
    public static string Compute(string? clauseId, string? assertionText)
    {
        var clause = (clauseId ?? string.Empty).Trim();
        if (clause.Length == 0)
        {
            throw new ArgumentException(
                "an assertion ID needs a stable clause ID. If the requirements register addresses clauses positionally "
                + "(\"§3.2, fourth paragraph\") then pin stable clause IDs first — otherwise the assertion IDs inherit "
                + "the instability they exist to avoid.",
                nameof(clauseId));
        }

        var normalised = Normalise(assertionText);
        if (normalised.Length == 0)
        {
            throw new ArgumentException(
                $"clause '{clause}' has an assertion with no text. An ID computed over nothing is the same ID for every "
                + "empty assertion, so it would collide silently rather than fail.",
                nameof(assertionText));
        }

        return clause + ":" + HashOf(normalised);
    }

    /// <summary>The six-hex content hash of ALREADY-NORMALISED text.</summary>
    public static string HashOf(string normalisedText)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalisedText ?? string.Empty));

        var sb = new StringBuilder(HashLength);
        for (var i = 0; i < digest.Length && sb.Length < HashLength; i++)
            sb.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));

        return sb.ToString(0, HashLength);
    }

    /// <summary>Splits an ID into clause and hash. False for anything malformed.</summary>
    public static bool TryParse(string? id, out string clauseId, out string hash)
    {
        clauseId = string.Empty;
        hash = string.Empty;

        var text = (id ?? string.Empty).Trim();
        var colon = text.LastIndexOf(':');
        if (colon <= 0 || colon == text.Length - 1)
            return false;

        var candidate = text[(colon + 1)..];
        if (candidate.Length != HashLength)
            return false;

        foreach (var c in candidate)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                return false;
        }

        clauseId = text[..colon];
        hash = candidate;
        return true;
    }

    /// <summary>
    /// TRUE for the DISPLAY-ORDINAL form — <c>REQ-014.A2</c>.
    ///
    /// <para>Listings show it because <c>REQ-014:3f9a1c</c> is unreadable aloud. *** A CITATION IN THIS
    /// FORM IS REJECTED, MECHANICALLY, BY SHAPE *** (§3.3) — precisely because it is the readable one and
    /// would otherwise be the one people type. The ordinal is positional, so a citation in it silently
    /// comes to name a different assertion the moment one is inserted above.</para>
    /// </summary>
    public static bool IsDisplayOrdinalForm(string? citation)
    {
        var text = (citation ?? string.Empty).Trim();

        var dot = text.LastIndexOf('.');
        if (dot <= 0 || dot == text.Length - 1)
            return false;

        var tail = text[(dot + 1)..];
        if (tail.Length < 2 || (tail[0] != 'A' && tail[0] != 'a'))
            return false;

        for (var i = 1; i < tail.Length; i++)
        {
            if (!char.IsDigit(tail[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the text actually wears the form it claims. The template is not style — it is what makes
    /// the decomposition rules mechanically visible, so a text that does not match its declared form is a
    /// clause that has not been decomposed.
    /// </summary>
    public static bool TextMatchesForm(string? text, AssertionForm form)
    {
        var normalised = Normalise(text);

        return form switch
        {
            AssertionForm.When => normalised.StartsWith("WHEN ", StringComparison.Ordinal)
                                  && normalised.Contains(" THEN ", StringComparison.Ordinal),
            AssertionForm.Never => normalised.StartsWith("NEVER ", StringComparison.Ordinal)
                                   && !normalised.Contains(" THEN ", StringComparison.Ordinal),
            _ => false,
        };
    }
}
