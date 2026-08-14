using System.Globalization;

namespace Harness.Map;

/// <summary>
/// What an encoding does with a cited value that is <b>not</b> in its table but <b>is</b> an integer.
///
/// <para>There is no member meaning "guess". The three below are the three answers a coordinator's own
/// table can actually state, and an encoding that states none of them refuses.</para>
/// </summary>
public enum NumericFallback
{
    /// <summary>
    /// <b>The zero value, and it is the safe one.</b> A cited integer that the table does not name is
    /// refused by name. Right for a purely symbolic field: a numeric <c>Profile</c> would be a caller
    /// bypassing the decade table, and the table is the thing the prose states.
    /// </summary>
    Refuse = 0,

    /// <summary>The integer itself is written. Right for the member that carries the duration.</summary>
    Passthrough,

    /// <summary>
    /// A fixed integer is written, whatever the number was. Right for a member that carries the
    /// <i>classification</i> of the cited value rather than the value — <c>md:128</c>'s
    /// <i>"a number, e.g. 90000 → ResetMode 1"</i>.
    /// </summary>
    Literal,
}

/// <summary>One encoding attempt: the value, or the reason there is not one. Never both, never neither.</summary>
/// <param name="Encoded">True only when a value was derived from a stated rule.</param>
/// <param name="Value">The integer to write. Meaningless unless <paramref name="Encoded"/>.</param>
/// <param name="Refusal">
/// Null when it encoded. Otherwise a sentence naming the signal, the cited value and <b>every value the
/// table does name</b> — because the reader's next act is to decide whether the vector or the table is
/// wrong, and they cannot do that from one half.
/// </param>
/// <param name="Derivation">
/// How the value was reached, in words, <b>with the citation</b>. Carried on the success path as well as
/// the failure one: an encoding that only speaks when it refuses cannot be told from one that stopped
/// running.
/// </param>
public sealed record EncodedValue(bool Encoded, long Value, string? Refusal, string? Derivation);

/// <summary>
/// 🔴 <b>A SYMBOLIC VECTOR VALUE, TURNED INTO THE INTEGER THE IR MEMBER HOLDS — BY A TABLE THE
/// COORDINATOR SUPPLIES, NEVER BY THIS CODE.</b>
///
/// <para>*** MEASURED 2026-08-14, ON THE DELIVERABLE, AND IT WAS 81 FINDINGS. *** All 27 conformance
/// vectors write <c>HBA_Stim.Profile = "RAISE_UNINTERRUPTED"</c> and
/// <c>HBA_Stim.Precondition = "CLEARDOWN"</c>. The IR members are <c>Int</c>. With nowhere in the binding
/// schema to put an encoding there was no encoder, so those values reached the range check as TEXT and
/// were refused: <c>its value 'RAISE_UNINTERRUPTED' is not an integer</c>. The tables that resolve them
/// are stated in the coordinator's prose (<c>harness-binding.md:121</c>, <c>:123-130</c>,
/// <c>:132-143</c>) — so supplying them is a TRANSCRIPTION, and the schema simply had nowhere to put
/// one.</para>
///
/// <para><b>THE TABLE IS DATA AND THIS CLASS IS ONLY AN INTERPRETER.</b> Not one hopper value appears in
/// this file. That is the whole design constraint: an encoding compiled in here would be an authorship
/// that no reader of the authority document could see, and the coordinator could not change it without a
/// build.</para>
///
/// <para>🔴 <b><see cref="Source"/> IS REQUIRED AND AN ENCODING WITHOUT ONE REFUSES.</b> The brief for
/// this work is <i>"cite the md line for every value you encode, and if a value is not in the tables,
/// REFUSE it by name rather than inventing a number"</i>. A citation held as a convention is a citation
/// somebody drops; held as a required field it cannot be dropped without the encoding failing loudly.</para>
///
/// <para><b>WHY IT COVERS THE TWO-TAG CASE WITHOUT A SECOND MECHANISM.</b> <c>md:57</c> maps ONE set-B
/// field to TWO IR members, and the schema is one tag per entry. Two entries may now share a
/// <c>specName</c>, each with its own encoding over the same cited text — the <c>Int</c> mode getting
/// <see cref="NumericFallback.Literal"/> and the <c>Time</c> getting
/// <see cref="NumericFallback.Passthrough"/>. Nothing about "two tags" is special-cased anywhere; what
/// makes it work is that the join key is the SPEC NAME and an encoding is per ENTRY.</para>
/// </summary>
/// <param name="Values">
/// Cited text → integer written. Compared ORDINALLY and after trimming, never case-insensitively: a
/// decade table keyed on <c>RAISE_UNINTERRUPTED</c> that also answers <c>raise_uninterrupted</c> is one
/// that will one day answer something nobody wrote.
/// </param>
/// <param name="WhenNumeric">What to do with an integer the table does not name.</param>
/// <param name="NumericLiteral">
/// The fixed integer for <see cref="NumericFallback.Literal"/>. <b>Null is a refusal there</b>, and
/// stating it under any other fallback is also a refusal — a field that is read in one mode and ignored
/// in another reads as accepted in both.
/// </param>
/// <param name="Source">The prose line this table is a transcription of. Required.</param>
public sealed record ValueEncoding(
    IReadOnlyDictionary<string, long> Values,
    NumericFallback WhenNumeric,
    long? NumericLiteral,
    string? Source)
{
    /// <summary>
    /// Everything wrong with the encoding itself, or empty — checked before any value is put through it,
    /// so a malformed table is a refusal rather than a value nobody can trace.
    /// </summary>
    public IReadOnlyList<string> Refusals
    {
        get
        {
            var refusals = new List<string>();

            if (string.IsNullOrWhiteSpace(Source))
            {
                refusals.Add(
                    "the encoding states no `source`. *** THE CITATION IS REQUIRED, NOT DECORATIVE: *** an encoding is a "
                    + "TRANSCRIPTION of a table in the coordinator's prose, and one that cannot name the line it came from is "
                    + "indistinguishable from a number somebody invented. Name the line, e.g. `md:132-143`.");
            }

            if (Values.Count == 0 && WhenNumeric == NumericFallback.Refuse)
            {
                refusals.Add(
                    "the encoding names no values and refuses every number, so it cannot encode anything at all. An encoding "
                    + "that admits nothing is not a strict encoding, it is an absent one wearing a field's clothes.");
            }

            if (WhenNumeric == NumericFallback.Literal && NumericLiteral is null)
            {
                refusals.Add(
                    "`whenNumeric` is Literal and no `numericLiteral` is stated. Literal means 'a cited number becomes THIS "
                    + "fixed integer' — there is no default, because the whole point of the mode is that the number itself is "
                    + "discarded and something else stands for it.");
            }

            if (WhenNumeric != NumericFallback.Literal && NumericLiteral is not null)
            {
                refusals.Add(
                    $"`numericLiteral` is {NumericLiteral} but `whenNumeric` is {WhenNumeric}, which never reads it. A field "
                    + "that is read in one mode and silently ignored in another reads as ACCEPTED in both — so it is refused "
                    + "rather than dropped.");
            }

            return refusals;
        }
    }

    /// <summary>
    /// Encode one cited value.
    ///
    /// <para><b>The table is tried FIRST, before the number test.</b> That ordering is load-bearing:
    /// <c>md:127</c> names <c>-1</c> as a SENTINEL meaning <i>never reset</i>, not as a duration, so a
    /// table entry for <c>"-1"</c> has to win over "it parses as an integer". A cited value that is both
    /// named and numeric is named.</para>
    /// </summary>
    /// <param name="signal">The name the refusal should point at — the spec name a vector actually cited.</param>
    /// <param name="cited">The value exactly as the vector wrote it.</param>
    public EncodedValue Encode(string signal, string? cited)
    {
        var name = string.IsNullOrWhiteSpace(signal) ? "<unnamed signal>" : signal;
        var broken = Refusals;

        if (broken.Count > 0)
            return new EncodedValue(false, 0, $"the encoding declared for '{name}' cannot be used: {string.Join(" ", broken)}", null);

        var raw = (cited ?? string.Empty).Trim();

        if (raw.Length == 0)
        {
            return new EncodedValue(false, 0,
                $"'{name}' was given no value to encode. An absent value is not a zero one: in this table zero is a value with "
                + "a meaning of its own, so writing one on the author's behalf would be choosing a scenario for them.", null);
        }

        if (Values.TryGetValue(raw, out var mapped))
            return new EncodedValue(true, mapped, null, $"'{raw}' -> {mapped} by the table at {Source}");

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            switch (WhenNumeric)
            {
                case NumericFallback.Passthrough:
                    return new EncodedValue(true, number, null, $"'{raw}' is a number and is written as itself ({Source})");

                case NumericFallback.Literal:
                    return new EncodedValue(true, NumericLiteral!.Value, null,
                        $"'{raw}' is a number, which this member encodes as the fixed value {NumericLiteral} ({Source})");
            }
        }

        return new EncodedValue(false, 0,
            $"'{name}' was cited as '{raw}', and the encoding at {Source} does not name it. "
            + "*** THIS IS A REFUSAL AND NEVER AN INVENTED CODE: *** an integer chosen here would reach the controller as a "
            + "perfectly legal scenario that nobody asked for, and the run would report a result against it with no error "
            + "anywhere. "
            + (Values.Count == 0
                ? "The table names no values at all."
                : $"Named values: {string.Join(", ", Values.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(k => $"'{k}'"))}.")
            + " A cited number is "
            + WhenNumeric switch
            {
                NumericFallback.Passthrough => "written as itself, and this value is not a number.",
                NumericFallback.Literal => $"encoded as {NumericLiteral}, and this value is not a number.",
                _ => "REFUSED by this encoding too, so every admissible value is in the list above.",
            },
            null);
    }
}
