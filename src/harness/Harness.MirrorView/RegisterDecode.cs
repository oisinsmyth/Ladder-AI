using System.Globalization;
using Harness.Wire;

namespace Harness.MirrorView;

/// <summary>One decoded value, and the raw words it was decoded FROM. Never one without the other.</summary>
/// <param name="Text">The value a reader is meant to act on.</param>
/// <param name="Basis">
/// How it was reached, in words — the transform, not a restatement of the answer. A decode that hides
/// its raw bytes hides a wrong assumption, and a decode that hides its RULE hides one just as well.
/// </param>
public sealed record DecodedValue(string Text, string Basis);

/// <summary>
/// Turning holding registers into values, with the raw hex always beside the result.
///
/// <para>🔴 <b>32-BIT VALUES ARE HIGH-WORD-FIRST.</b> Measured on this rig off a build stamp with
/// distinguishable halves (2026-08-13), re-confirmed over both transports (2026-08-14). The transform
/// itself is <c>Harness.Wire.RegisterWords</c> — the same one the harness proper uses — rather than a
/// second shift-and-or written here, so a view and a harness cannot disagree about word order while
/// both looking right.</para>
///
/// <para><b>A <c>Time</c> spans two registers and inherits that order.</b> It is a 32-bit count of
/// milliseconds.</para>
/// </summary>
public static class RegisterDecode
{
    /// <summary>The word order this project has measured. Named, so the page can print it.</summary>
    public const RegisterWordOrder Order = RegisterWordOrder.HighWordFirst;

    /// <summary>One register as <c>16#XXXX</c>.</summary>
    public static string Hex(ushort word) => "16#" + word.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>A run of registers as space-separated <c>XXXX</c> words, in address order.</summary>
    public static string HexWords(IReadOnlyList<ushort> words) =>
        string.Join(" ", words.Select(w => w.ToString("X4", CultureInfo.InvariantCulture)));

    /// <summary>
    /// Decode one tag from the registers it occupies.
    ///
    /// <para>Returns null when this tool has no decode for the type. <b>That is reported as "no decode"
    /// beside the raw words, never as a guess</b> — the mirror generator may grow a type before this
    /// view does, and a plausible number against an unknown encoding is worse than an admitted gap.</para>
    /// </summary>
    public static DecodedValue? Decode(MirrorTag tag, IReadOnlyList<ushort> registers)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(registers);

        if (registers.Count != tag.RegisterCount) return null;

        switch (tag.TypeName)
        {
            case "Bool":
            {
                var set = (registers[0] & (1 << tag.BitInRegister)) != 0;
                return new DecodedValue(
                    set ? "TRUE" : "FALSE",
                    $"bit {tag.BitInRegister} of {Hex(registers[0])}. The bit POSITION inside the register " +
                    "is inferred (Harness.Map.MirrorGeometry.BitAddressOf, marked [I] — not measured); the " +
                    "register itself is not in doubt. The whole word is printed so the inference is visible.");
            }

            case "Int":
                return new DecodedValue(
                    ((short)registers[0]).ToString(CultureInfo.InvariantCulture),
                    $"signed 16-bit from {Hex(registers[0])}.");

            case "UInt":
                return new DecodedValue(
                    registers[0].ToString(CultureInfo.InvariantCulture),
                    $"unsigned 16-bit from {Hex(registers[0])}.");

            case "Word":
                return new DecodedValue(
                    Hex(registers[0]),
                    "a bit pattern, printed as itself.");

            case "DWord":
            {
                var raw = RegisterWords.To32(registers[0], registers[1], Order);
                return new DecodedValue(
                    "16#" + raw.ToString("X8", CultureInfo.InvariantCulture),
                    $"{Hex(registers[0])} then {Hex(registers[1])}, HIGH-WORD-FIRST (measured on this rig).");
            }

            case "UDInt":
            {
                var raw = RegisterWords.To32(registers[0], registers[1], Order);
                return new DecodedValue(
                    raw.ToString(CultureInfo.InvariantCulture),
                    $"unsigned 32-bit, {Hex(registers[0])} then {Hex(registers[1])}, HIGH-WORD-FIRST.");
            }

            case "DInt":
            {
                var raw = RegisterWords.To32(registers[0], registers[1], Order);
                return new DecodedValue(
                    unchecked((int)raw).ToString(CultureInfo.InvariantCulture),
                    $"signed 32-bit, {Hex(registers[0])} then {Hex(registers[1])}, HIGH-WORD-FIRST. " +
                    $"Unsigned it reads {raw} — which is how a free-running counter must be read across its wrap.");
            }

            case "Time":
            {
                var raw = RegisterWords.To32(registers[0], registers[1], Order);
                var ms = unchecked((int)raw);
                return new DecodedValue(
                    FormatIecTime(ms),
                    $"32-bit milliseconds ({ms}), {Hex(registers[0])} then {Hex(registers[1])}, " +
                    "HIGH-WORD-FIRST — a Time spans two registers and inherits the order.");
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// An S7 <c>Time</c> printed the way TIA prints it, so it can be compared against a block's own
    /// preset without arithmetic in the reader's head. Negative values keep their sign — <c>Time</c> is
    /// signed and a negative one is a real (and interesting) reading, not something to clamp away.
    /// </summary>
    public static string FormatIecTime(int milliseconds)
    {
        if (milliseconds == 0) return "T#0ms";

        var negative = milliseconds < 0;

        // int.MinValue has no positive counterpart; do the decomposition in long.
        var remaining = Math.Abs((long)milliseconds);

        var days = remaining / 86_400_000; remaining %= 86_400_000;
        var hours = remaining / 3_600_000; remaining %= 3_600_000;
        var minutes = remaining / 60_000; remaining %= 60_000;
        var seconds = remaining / 1000;
        var ms = remaining % 1000;

        var text = "T#";
        if (negative) text = "T#-";
        if (days > 0) text += $"{days}d";
        if (hours > 0) text += $"{hours}h";
        if (minutes > 0) text += $"{minutes}m";
        if (seconds > 0) text += $"{seconds}s";
        if (ms > 0) text += $"{ms}ms";
        return text;
    }
}
