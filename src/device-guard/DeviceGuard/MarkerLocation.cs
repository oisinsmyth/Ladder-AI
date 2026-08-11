namespace DeviceGuard;

/// <summary>
/// Where a device's unit-level identifier can be read from, when the CPU will not report one itself.
///
/// <para><b>Why this is configuration and not a constant.</b> An S7-1200 reached over classic S7comm
/// will report its ORDER CODE — which every unit of that model reports identically — and, on the CPU
/// this project measured, nothing else: SZL 0x001C is refused, GetCpuInfo is refused, and MAC/DCP
/// cannot cross a routed tunnel. So a unit-level identifier has to be published BY THE PROGRAM, in a
/// data block the reader knows the location of. That location is a property of the program loaded on
/// that device, so it belongs beside the device's allowlist entry rather than compiled into the
/// reader.</para>
///
/// <para><b>This type performs no I/O and knows nothing about S7.</b> It is a declaration that is
/// handed to the harness, which builds the actual reader. DeviceGuard stays a pure decision layer.</para>
///
/// <para><b>The block must have OPTIMIZED BLOCK ACCESS TURNED OFF.</b> Classic S7comm cannot see an
/// optimized block at all — not as an error, it is simply invisible — and for an S7-1200 optimized is
/// the TIA default. Nothing here can check that; a wrongly-optimized block shows up as a failed read
/// at connect time, which is at least loud.</para>
/// </summary>
public sealed record MarkerLocation(
    /// <summary>The data block number, e.g. 100 for DB100. Absolute — classic S7comm has no symbol table.</summary>
    int? DbNumber = null,

    /// <summary>
    /// Byte offset of the marker WITHIN the block. Absolute, and NOT guessable: it depends on what
    /// members precede it. Read it off the compiled block rather than computing it by hand.
    /// </summary>
    int? ByteOffset = null,

    /// <summary>Declared character capacity of the marker — the <c>n</c> in <c>String[n]</c>.</summary>
    int? Length = null,

    /// <summary>
    /// How the characters are laid out: <see cref="S7String"/> (a TIA <c>String[n]</c>, two header
    /// bytes then the characters) or <see cref="FixedChars"/> (a bare <c>Array of Char</c>). Null
    /// means <see cref="S7String"/>, which is what a <c>String[n]</c> member produces.
    /// </summary>
    string? Encoding = null)
{
    /// <summary>A TIA <c>String[n]</c>: byte 0 declared maximum, byte 1 current length, then characters.</summary>
    public const string S7String = "S7String";

    /// <summary>A fixed-length <c>Array[0..n-1] of Char</c>, no length header.</summary>
    public const string FixedChars = "FixedChars";

    /// <summary>The encoding to use, defaulting to <see cref="S7String"/> when none was declared.</summary>
    public string EffectiveEncoding =>
        string.IsNullOrWhiteSpace(Encoding) ? S7String : Encoding!.Trim();

    /// <summary>
    /// Why this declaration cannot be used, or null when it is usable.
    ///
    /// <para>Fail-closed and complete: a HALF-FILLED marker block is refused rather than defaulted.
    /// Defaulting a missing DB number or offset would produce a reader pointed at the wrong bytes,
    /// and wrong bytes that happen to parse are the one outcome worse than no marker at all — they
    /// would be compared against the allowlist and could match.</para>
    /// </summary>
    public string? Problem()
    {
        if (DbNumber is null && ByteOffset is null && Length is null && string.IsNullOrWhiteSpace(Encoding))
            return "the marker block is present but completely empty; remove it, or give it a dbNumber, byteOffset and length";

        if (DbNumber is null)
            return "the marker declares no dbNumber, so there is no block to read it from";
        if (DbNumber <= 0)
            return $"the marker dbNumber must be positive, but is {DbNumber}";

        if (ByteOffset is null)
            return "the marker declares no byteOffset; it is not guessable, so read it off the compiled block";
        if (ByteOffset < 0)
            return $"the marker byteOffset must not be negative, but is {ByteOffset}";

        if (Length is null)
            return "the marker declares no length, so the reader would not know how many bytes to ask for";
        if (Length is <= 0 or > 254)
            return $"the marker length must be 1-254 (an S7 String holds at most 254), but is {Length}";

        var encoding = EffectiveEncoding;
        if (!string.Equals(encoding, S7String, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(encoding, FixedChars, StringComparison.OrdinalIgnoreCase))
        {
            return $"unknown marker encoding '{encoding}'; expected '{S7String}' or '{FixedChars}'";
        }

        return null;
    }

    /// <summary>True when this declaration can be turned into a reader.</summary>
    public bool IsUsable => Problem() is null;

    /// <summary>A short human description for messages.</summary>
    public string Describe() =>
        DbNumber is null || ByteOffset is null
            ? "<incomplete marker>"
            : $"DB{DbNumber}.DBB{ByteOffset} ({EffectiveEncoding}, {Length} chars)";
}
