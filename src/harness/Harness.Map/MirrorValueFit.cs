using System.Globalization;

namespace Harness.Map;

/// <summary>Whether a value survives the mirror element it was asked to travel in, and if not, why.</summary>
/// <param name="Fits">True only when the value arrives at the PLC as the value that was written.</param>
/// <param name="Value">The parsed value, when it parsed at all.</param>
/// <param name="Refusal">
/// Null when it fits. Otherwise a sentence naming the signal, the value, the type and the range —
/// <b>never a number that was quietly changed.</b>
/// </param>
public sealed record MirrorFitResult(bool Fits, long Value, string? Refusal);

/// <summary>
/// 🔴 <b>A VALUE THAT DOES NOT FIT ITS MIRROR ELEMENT IS A REFUSAL BY NAME, NEVER A MODULO.</b>
///
/// <para><b>MEASURED, on the deliverable vector set: 81 duration values exceed 65 535 ms</b> — ten
/// distinct figures between 70 000 and 120 000. Through a single-register mapping <c>75 000 ms</c>
/// arrives as <c>9 464 ms</c>. <b>That is not an error and not a timeout — every scenario boundary fires
/// early and the run returns a plausible-looking FAIL against a block that did nothing wrong.</b> A
/// confident wrong answer is the worst thing this harness can produce, and truncation is the cheapest
/// way to produce one.</para>
///
/// <para>*** THE TWO 32-BIT HAZARDS ARE NOT SYMMETRIC, AND THAT IS WHY THIS EXISTS. *** A swapped WORD
/// ORDER announces itself: 75 s becomes about 7 days, the scenario never reaches its boundary and the
/// run TIMES OUT loudly. A truncated WIDTH passes quietly with the wrong number. <b>The width failure is
/// the dangerous half, so it is the one made loud</b> — and the order hazard, which is already loud, is
/// left to the rig's calibration step.</para>
///
/// <para><b>Where the range comes from:</b> <see cref="MirrorElement.Minimum"/>/<see cref="MirrorElement.Maximum"/>,
/// which are derived from the element's ADDRESS FORM. So a new element type inherits its own bound from
/// the same row that gives it its width and rung shape — there is no second table of limits to disagree
/// with the first.</para>
/// </summary>
public static class MirrorValueFit
{
    /// <summary>
    /// Parse and range-check one value against the element that will carry it.
    /// </summary>
    /// <param name="signal">The signal's name, so a refusal points at something the author can find.</param>
    /// <param name="type">The declared element type. An unsupported one refuses rather than defaulting.</param>
    /// <param name="text">The value as the vector wrote it.</param>
    public static MirrorFitResult Check(string signal, MirrorValueType type, string? text) =>
        Check(signal, type, text, encoding: null);

    /// <summary>
    /// 🔴 <b>Parse, ENCODE, and range-check one value — the one place a cited value becomes a number.</b>
    ///
    /// <para><b>The encoding runs FIRST and its output is then held to exactly the same width rule.</b>
    /// That ordering is the point: an encoding decides <i>what integer this member holds</i>, and the
    /// element decides <i>whether that integer survives the register</i>. Collapsing them would let a
    /// table smuggle a value past the width refusal, which is the one refusal measured to be load-bearing
    /// on this deliverable.</para>
    ///
    /// <para><b>A null encoding is not "encode as identity by a rule nobody wrote"</b> — it is the absence
    /// of an encoding, and the cited text is then required to BE the value. A symbolic value meeting that
    /// is refused with the ordinary "is not an integer" sentence, which is what happened to 54 of the
    /// deliverable's values before this existed.</para>
    /// </summary>
    public static MirrorFitResult Check(string signal, MirrorValueType type, string? text, ValueEncoding? encoding)
    {
        var name = string.IsNullOrWhiteSpace(signal) ? "<unnamed signal>" : signal;
        var raw = (text ?? string.Empty).Trim();

        var element = MirrorElements.For(type);

        if (encoding is not null && element is not null)
        {
            var encoded = encoding.Encode(name, raw);

            if (!encoded.Encoded)
                return new MirrorFitResult(false, 0, encoded.Refusal);

            // The ENCODED number, re-entered through the ordinary path so it meets the same width rule and
            // the same refusal sentence. Rendered back to text rather than short-circuited, because a
            // second range check written beside this one is how the two come to disagree.
            raw = encoded.Value.ToString(CultureInfo.InvariantCulture);
            text = raw;
        }
        if (element is null)
        {
            return new MirrorFitResult(false, 0,
                $"'{name}' has element type {type}, which this harness cannot mirror. Supported: {MirrorElements.Supported}. "
                + "A value cannot be range-checked against a type with no width.");
        }

        if (raw.Length == 0)
        {
            return new MirrorFitResult(false, 0,
                $"'{name}' was given no value. An absent value is not a zero one: zero is a value the block could "
                + "legitimately be driven with, so writing one on the author's behalf would be inventing the stimulus.");
        }

        if (element.Form == MirrorAddressForm.Bit)
        {
            if (bool.TryParse(raw, out var flag))
                return new MirrorFitResult(true, flag ? 1 : 0, null);

            // "1"/"0" are accepted because a wire-minded author writes them, and both are unambiguous.
            if (raw is "1" or "0")
                return new MirrorFitResult(true, raw == "1" ? 1 : 0, null);

            return new MirrorFitResult(false, 0,
                $"'{name}' is a {element.IrDataType} and its value '{raw}' is not a boolean. Expected true/false (or 1/0). "
                + "Anything else would have to be coerced, and a coerced boolean is one somebody has to guess the meaning of.");
        }

        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return new MirrorFitResult(false, 0,
                $"'{name}' is a {element.IrDataType} and its value '{raw}' is not an integer. It is not converted on a "
                + "best-effort basis: a value nobody can read is not a value that should reach a controller.");
        }

        if (element.Fits(value))
            return new MirrorFitResult(true, value, null);

        // *** THE SENTENCE THAT MATTERS. *** It names what the value WOULD have become, because the whole
        // hazard is that the wrapped number looks entirely reasonable — 9 464 is a plausible dwell.
        var wrapped = element.Form == MirrorAddressForm.Word
            ? $" Truncated to this element's width it would arrive as {unchecked((short)value)} — a plausible-looking number, which is exactly why this refuses rather than converting."
            : string.Empty;

        return new MirrorFitResult(false, value,
            $"'{name}' is declared {element.IrDataType} ({element.Registers} register(s), {element.Minimum}..{element.Maximum}) "
            + $"and its value {value} does not fit.{wrapped} *** THIS IS A REFUSAL AND NEVER A MODULO: *** a truncated value does not "
            + "error on the controller, it makes every boundary keyed on it fire early and returns a confident wrong answer against a "
            + $"block that may be perfectly correct. If the value is right, the signal needs a wider element — {NextWiderThan(element)}.");
    }

    /// <summary>
    /// 🔴 <b>THE KEY A VECTOR WRITES ITS INPUTS UNDER — AND UNTIL 2026-08-14 THIS JOINED ON THE WRONG
    /// ONE, SO THE WIDTH GATE EXAMINED NOTHING ON THE ONLY VECTOR SET IT WAS EVER WRITTEN FOR.</b>
    ///
    /// <para>A vector cites the SPECIFICATION's name; the binding's <c>Tag</c> is what the BLOCK calls the
    /// member. <see cref="CheckAll"/> looked values up by <c>Tag</c> alone. <b>Measured on the deliverable
    /// shape: an input keyed <c>HBA_Stim.P1Ms</c> against a target tagged
    /// <c>iDB_HopperBlockageStim.Stim.P1</c> produced ZERO refusals, while the identical value keyed by
    /// the tag produced one.</b> The doc comment above cites <i>81 duration values exceeding 65 535 ms in
    /// the deliverable vector set</i> as this class's reason to exist — and not one of them could be
    /// reached, because all 27 vectors key their inputs by spec name.</para>
    ///
    /// <para><b>Same defect, same cause, third place found:</b> the observability map keyed on the tag and
    /// resolved 1 of 17 signals; this keyed on the tag and resolved 0 of 10. <i>A green that examined
    /// nothing is indistinguishable from a green that examined everything.</i></para>
    ///
    /// <para><b>The stated name WINS EXCLUSIVELY — the tag is not also tried.</b> Falling back to the tag
    /// when a spec name IS stated would re-introduce the silent identity the <c>specName</c> field exists
    /// to remove. The tag is used only where the binding stated no spec name at all, which is a weaker
    /// join and is already reported as such on the observation side.</para>
    /// </summary>
    /// <remarks>
    /// 🔴 <b>MOVED ONTO <see cref="MirroredSignal.JoinKey"/> — this was the SECOND of two independent
    /// derivations of one rule, and the OTHER one was never fixed.</b> <c>LoopRun.ToWireVector</c> kept
    /// looking its inputs up by <c>Tag</c> long after this was corrected, so on the deliverable this class
    /// checked ten values that the writer then failed to write at all. One definition, used by both.
    /// </remarks>
    private static string JoinKey(MirroredSignal signal) => signal.JoinKey;

    /// <summary>
    /// Every value in a binding's inputs, checked — <b>and every input the binding consumes under no name
    /// at all.</b> Empty when they all fit and all land somewhere.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>AN INPUT NO TARGET CONSUMES IS A COMMANDED STIMULUS THAT WILL NEVER BE WRITTEN, AND IT
    /// USED TO BE DROPPED IN SILENCE.</b> The wave builder walks the binding's TARGETS and looks each one
    /// up in the vector's inputs; anything the vector supplies that no target claims is simply never
    /// visited. The run then proceeds with that register at zero — <b>and zero is a legal value for every
    /// element the mirror carries</b>, so the block is driven with a stimulus nobody asked for and the
    /// result is reported as though the requested one had been applied.</para>
    ///
    /// <para><b>Measured on the deliverable:</b> all 27 vectors supply <c>HBA_Stim.ResetAtMs</c>, and the
    /// coordinator's binding deliberately leaves it unbound (one set-B field maps to TWO IR members —
    /// <c>harness-binding.md:57</c>, recorded at <c>_unbound["HBA_Stim.ResetAtMs"]</c>). That absence is an
    /// honest record; what was NOT honest was the run silently proceeding without it, with
    /// <c>ResetMode</c> reading 0 — which the encoding table at <c>md:123-130</c> defines as <i>never
    /// reset</i>, a perfectly valid scenario that is not the one the vector asked for.</para>
    ///
    /// <para><b>It refuses rather than warns, and it cannot over-fire:</b> <c>CheckAll</c> is called with
    /// the vector's OWN slot's binding, so every input a vector carries is supposed to be consumed by that
    /// binding. There is no legitimate reading in which a vector commands a signal its own slot does not
    /// wire.</para>
    /// </remarks>
    public static IReadOnlyList<string> CheckAll(
        IReadOnlyList<MirroredSignal> signals,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var refusals = new List<string>();
        var consumed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var signal in signals ?? Array.Empty<MirroredSignal>())
        {
            if (signal is null)
                continue;

            var key = JoinKey(signal);
            consumed.Add(key);

            // A signal the vector says nothing about is NOT checked here: leaving an input undriven is a
            // legitimate choice, and it is the inert declaration's business rather than the width rule's.
            if (!values.TryGetValue(key, out var text))
                continue;

            var result = Check(key, signal.Type, text, signal.Encoding);
            if (!result.Fits)
                refusals.Add(result.Refusal!);
        }

        foreach (var orphan in values.Keys.Where(k => !consumed.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
        {
            refusals.Add(
                $"'{orphan}' is supplied as a vector input and NO BOUND TARGET CONSUMES IT, so it would never be written. "
                + "*** THAT IS A SILENT SUBSTITUTION, NOT A MISSING FEATURE: *** the register stays at zero, zero is a legal "
                + "value for every element the mirror carries, and the run reports a result as though the commanded stimulus "
                + "had been applied. Either the binding is short a vectorTarget for it, or the vector is citing a name the "
                + "coordinator did not bind. "
                + (consumed.Count == 0
                    ? "This binding wires NO vector targets at all."
                    : $"Bound target names: {string.Join(", ", consumed.OrderBy(k => k, StringComparer.Ordinal))}."));
        }

        return refusals;
    }

    /// <summary>The narrowest supported element that would hold more than this one, named for the refusal's remedy.</summary>
    private static string NextWiderThan(MirrorElement element)
    {
        var wider = MirrorElements.All
            .Where(e => e.Maximum > element.Maximum)
            .OrderBy(e => e.Maximum)
            .Select(e => e.Type.ToString())
            .ToArray();

        return wider.Length == 0
            ? "and there is no wider element in this harness, so the signal cannot be mirrored as it stands"
            : $"declare it {string.Join(" or ", wider)}";
    }
}
