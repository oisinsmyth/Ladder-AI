namespace Converter.Ir;

/// <summary>
/// Siemens' <b>area-pointer</b> notation — <c>P#M1000.0 WORD 37</c>, <c>P#DB99.DBX0.0 BYTE 2</c> —
/// split into its parts and nothing more.
///
/// <para><b>Syntax only, policy nowhere.</b> This type answers "what does that text say", and every
/// caller answers "is that acceptable here" for itself, because the two callers legitimately
/// disagree. <c>served-area</c> accepts <c>WORD</c> at bit 0 in marker memory and refuses everything
/// else, since a served width guessed WIDE allocates a map that overflows the real Modbus window.
/// <c>neighbours</c> accepts every unit it can convert to a byte span, since a claim it refuses to
/// measure is a claim it silently drops from an occupancy list. Folding either policy in here would
/// give one of them the other's answer.</para>
///
/// <para><b>Extracted 2026-08-23 (workbench Y1)</b> from <c>ServedAreaRunner.TryParsePointer</c>,
/// which landed hours earlier as the first reader of this syntax. A second hand-rolled reader of the
/// same notation is how two checks come to disagree about what a program says.</para>
/// </summary>
/// <param name="Text">Verbatim, exactly as the IR carries it. Everything else is derived from it.</param>
/// <param name="AddressText">The address between <c>P#</c> and the unit — <c>M1000.0</c>, <c>DB99.DBX0.0</c>.</param>
/// <param name="Unit">The unit token verbatim and un-judged: <c>WORD</c>, <c>BYTE</c>, or something nobody has seen.</param>
/// <param name="Count">How many <see cref="Unit"/>s. Always positive — a non-positive count does not parse.</param>
/// <param name="Area">
/// <c>"M"</c> when <see cref="AddressText"/> is a <c>&lt;byte&gt;.&lt;bit&gt;</c> marker address, and
/// <b>null for every other address space</b>. Null is not an error here: a pointer into a data block
/// is a perfectly good pointer, it is simply not in marker memory, and only the caller knows whether
/// that matters.
/// </param>
public sealed record AreaPointer(string Text, string AddressText, string Unit, int Count, string? Area, int BaseByte, int Bit)
{
    /// <summary>True when the address is marker memory and its byte/bit therefore mean something.</summary>
    public bool IsMarker => Area == "M";

    /// <summary>
    /// The width of one <see cref="Unit"/> in BITS, or null for a unit nobody has grounded.
    ///
    /// <para><b>Null is the honest answer, not zero.</b> A unit this table does not carry is a span
    /// that cannot be measured, and every caller must decide that out loud rather than treating an
    /// unmeasured pointer as a zero-length one — which would drop it from an occupancy list while
    /// looking like a clean read.</para>
    /// </summary>
    public int? UnitBits => Unit.ToUpperInvariant() switch
    {
        "BOOL" => 1,
        "BYTE" or "CHAR" or "SINT" or "USINT" => 8,
        "WORD" or "INT" or "UINT" or "DATE" or "S5TIME" => 16,
        "DWORD" or "DINT" or "UDINT" or "REAL" or "TIME" or "TIME_OF_DAY" or "TOD" => 32,
        "LWORD" or "LINT" or "ULINT" or "LREAL" or "LTIME" => 64,
        _ => null,
    };

    /// <summary>
    /// How many BYTES the pointer occupies from <see cref="BaseByte"/>, or null when
    /// <see cref="UnitBits"/> is. Rounded OUTWARD: a bit-counted pointer that ends mid-byte still
    /// occupies that byte, and rounding the other way would understate an occupancy — the direction
    /// that lets two owners share a byte without anything noticing.
    /// </summary>
    public int? ByteLength => UnitBits is int bits ? ((Bit + (bits * Count)) + 7) / 8 : null;
}

/// <summary>The one reader of <see cref="AreaPointer"/> syntax. See that type for why policy lives elsewhere.</summary>
public static class AreaPointerParser
{
    public const string Prefix = "P#";

    /// <summary>
    /// Parses <c>P#&lt;address&gt; &lt;UNIT&gt; &lt;count&gt;</c>.
    ///
    /// <para><paramref name="why"/> covers only the failures both callers share — text that is not a
    /// pointer at all, a shape that is not three parts, and a count that is not a positive integer.
    /// An address outside marker memory PARSES, with <see cref="AreaPointer.Area"/> null; a unit
    /// nobody recognises PARSES, with <see cref="AreaPointer.UnitBits"/> null. Both are questions for
    /// the caller.</para>
    /// </summary>
    public static bool TryParse(string text, out AreaPointer pointer, out string why)
    {
        ArgumentNullException.ThrowIfNull(text);

        pointer = new AreaPointer(text, string.Empty, string.Empty, 0, null, 0, 0);

        if (!text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            why = "it is not an area pointer at all.";
            return false;
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            why = $"it does not have the three parts (<address> <unit> <count>) an area pointer has; it has {parts.Length}.";
            return false;
        }

        if (!int.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count)
            || count <= 0)
        {
            why = $"`{parts[2]}` is not a positive count.";
            return false;
        }

        var address = parts[0][Prefix.Length..];
        TryParseMarkerAddress(address, out var area, out var baseByte, out var bit);

        pointer = new AreaPointer(text, address, parts[1], count, area, baseByte, bit);
        why = string.Empty;
        return true;
    }

    /// <summary>
    /// <c>M1000.0</c> → marker memory, byte 1000, bit 0. Anything else leaves <paramref name="area"/>
    /// null; the caller decides whether that is a refusal or simply a different address space.
    /// </summary>
    private static void TryParseMarkerAddress(string address, out string? area, out int baseByte, out int bit)
    {
        area = null;
        baseByte = 0;
        bit = 0;

        if (address.Length < 3 || address[0] != 'M')
        {
            return;
        }

        var dot = address.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0
            || !int.TryParse(address[1..dot], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out baseByte)
            || !int.TryParse(address[(dot + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out bit))
        {
            baseByte = 0;
            bit = 0;
            return;
        }

        area = "M";
    }
}
