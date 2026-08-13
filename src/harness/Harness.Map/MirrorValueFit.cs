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
    public static MirrorFitResult Check(string signal, MirrorValueType type, string? text)
    {
        var name = string.IsNullOrWhiteSpace(signal) ? "<unnamed signal>" : signal;
        var raw = (text ?? string.Empty).Trim();

        var element = MirrorElements.For(type);
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

    /// <summary>Every value in a binding's inputs, checked. Empty when they all fit.</summary>
    public static IReadOnlyList<string> CheckAll(
        IReadOnlyList<MirroredSignal> signals,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var refusals = new List<string>();

        foreach (var signal in signals ?? Array.Empty<MirroredSignal>())
        {
            if (signal is null)
                continue;

            // A signal the vector says nothing about is NOT checked here: leaving an input undriven is a
            // legitimate choice, and it is the inert declaration's business rather than the width rule's.
            if (!values.TryGetValue(signal.Tag, out var text))
                continue;

            var result = Check(signal.Tag, signal.Type, text);
            if (!result.Fits)
                refusals.Add(result.Refusal!);
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
