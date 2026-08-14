using System.Text;

namespace Harness.Map;

/// <summary>
/// How one slot's id was turned into the fragment that appears inside a mirror tag name.
/// </summary>
/// <param name="SlotId">The slot's IDENTITY — the string a vector's <c>slot</c> field must equal.</param>
/// <param name="Token">The identifier fragment used in <c>HX_&lt;token&gt;_Start</c> and friends.</param>
/// <param name="Transliterated">
/// True when the two differ. <b>Reported on every slot either way</b> — a derivation that only speaks
/// when it changed something is indistinguishable from one that has stopped running.
/// </param>
public sealed record SlotTokenDerivation(string SlotId, string Token, bool Transliterated)
{
    public override string ToString() => Transliterated ? $"{SlotId} -> {Token}" : $"{SlotId} (unchanged)";
}

/// <summary>
/// 🔴 <b>A SLOT ID IS TWO DIFFERENT THINGS WITH TWO DIFFERENT ALPHABETS, AND TREATING THEM AS ONE MADE
/// THE DELIVERABLE'S OWN BINDING UNGENERATABLE.</b>
///
/// <para>The slot id is (a) the CROSS-REFERENCE KEY a submission vector's <c>slot</c> field must equal —
/// chosen by whoever wrote the vectors, and in the hopper set that is <c>SLOT-HBA-RAISE</c> — and (b) a
/// FRAGMENT OF A PLC TAG NAME, <c>HX_&lt;slot&gt;_Start</c>, which must be a plain identifier because a
/// hyphen is not legal in one. <b>Those alphabets are not the same and neither party can change the
/// other's.</b></para>
///
/// <para><b>Measured 2026-08-14, and it is not a six-slot problem — it is a ONE-slot problem.</b> The
/// committed coordinator binding declares the single slot <c>SLOT-HBA-ALL</c>, and
/// <c>harness-run --generate-only</c> against it returns
/// <c>slot id 'SLOT-HBA-ALL' is not a plain identifier</c> and emits nothing. Every hyphenated slot id in
/// the deliverable was in that state, so the copy layer for those 27 vectors could not be generated at
/// all, at any slot count.</para>
///
/// <para><b>The derivation is mechanical, total and REPORTED, and a collision is REFUSED.</b> It never
/// touches the binding document: the slot keeps its id, the vectors keep their cross-reference, and only
/// the generated tag name — which this generator already invents in full — is transliterated. Two
/// different ids that would land on one token are refused BY NAME rather than sharing a register band,
/// because that would be two slots aliased onto one mirror region, which is the one genuine leak this
/// design has (DB-6).</para>
///
/// <para>⚠️ <b>Do not "simplify" this into a silent <c>Replace('-', '_')</c> at the call site.</b> The
/// value here is not the substitution — it is that the substitution is <i>counted, printed and
/// collision-checked</i>. A narrowing nobody can see becomes a place to hide.</para>
/// </summary>
public static class SlotTagToken
{
    /// <summary>True when <paramref name="text"/> may appear in a PLC tag name unchanged.</summary>
    public static bool IsPlainIdentifier(string? text) =>
        !string.IsNullOrEmpty(text)
        && (char.IsAsciiLetter(text[0]) || text[0] == '_')
        && text.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    /// <summary>
    /// True when a slot id carries at least one character that survives into an identifier.
    ///
    /// <para>An id of punctuation alone (<c>"---"</c>) would transliterate to underscores and name
    /// nothing, and two such ids would collide with each other. <b>That is a refusal, not a token.</b></para>
    /// </summary>
    public static bool CanDerive(string? slotId) =>
        !string.IsNullOrWhiteSpace(slotId) && slotId.Any(char.IsAsciiLetterOrDigit);

    /// <summary>
    /// The identifier fragment for a slot id. <b>Only callable once <see cref="CanDerive"/> holds</b> —
    /// the generator refuses before it gets here, so this throws rather than inventing a name.
    /// </summary>
    public static string For(string slotId)
    {
        if (!CanDerive(slotId))
        {
            throw new ArgumentException(
                $"slot id '{slotId}' carries no letter or digit, so there is no identifier to derive from it. The generator's "
                + "refusal should have stopped this before rendering; deriving a name of underscores here would give two such "
                + "slots the same tag names and alias their mirror regions.", nameof(slotId));
        }

        var token = new StringBuilder(slotId.Length + 1);

        if (char.IsAsciiDigit(slotId[0]))
            token.Append('_');

        foreach (var c in slotId)
            token.Append(char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_');

        return token.ToString();
    }

    /// <summary>The derivation for one slot, whether or not anything changed.</summary>
    public static SlotTokenDerivation Derive(string slotId)
    {
        var token = For(slotId);
        return new SlotTokenDerivation(slotId, token, !string.Equals(token, slotId, StringComparison.Ordinal));
    }

    /// <summary>
    /// A one-line summary of a whole wave set's derivations — <b>printed on every run, including the run
    /// where nothing was transliterated.</b>
    /// </summary>
    public static string Describe(IReadOnlyList<SlotTokenDerivation> derivations)
    {
        var changed = derivations.Where(d => d.Transliterated).ToArray();

        return changed.Length == 0
            ? $"slot tag tokens: {derivations.Count} slot id(s), 0 transliterated (every id is already a plain identifier)"
            : $"slot tag tokens: {derivations.Count} slot id(s), {changed.Length} transliterated for tag names — "
              + string.Join("; ", changed.Select(d => d.ToString()));
    }
}
