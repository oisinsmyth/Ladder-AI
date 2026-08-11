using Harness.S7;

namespace Harness.RigWrite;

/// <summary>
/// Where each member of the rig marker DB sits, in bytes from the start of the block.
///
/// <para><b>These offsets are COMPUTED, not measured, and that distinction is the whole reason this
/// type carries a comment instead of just four integers.</b> A SimaticML DB export states member
/// names, types, order and start values — and no offsets at all. So the numbers below are derived from
/// the S7 layout rules for a STANDARD (non-optimized) block: an <c>Int</c> occupies 2 bytes, a
/// <c>String[n]</c> occupies <c>n + 2</c> (see <see cref="S7StringCodec"/>), and members follow one
/// another in declaration order.</para>
///
/// <para><b>What corroborates them.</b> The four members computed this way occupy exactly
/// <see cref="TotalBytes"/>, which is the block size TIA reports — an arithmetic identity that a
/// different member size would break (checked by <see cref="IsSelfConsistent"/>). That is evidence
/// about SIZES; it is not evidence about ORDER, since any permutation of three equal-length strings
/// sums the same.</para>
///
/// <para><b>What would settle them, and costs nothing extra.</b> Reading the whole block —
/// <see cref="TotalBytes"/> bytes from byte 0 — and checking that a two-byte String header
/// <c>(32, len)</c> appears at 2, 36 and 70, with the expected text after each. If the layout is as
/// computed, all three land; if it is not, at least one header reads as something that is not a
/// plausible <c>(declared, current)</c> pair. That read is step 1 of the first-write sequence for
/// exactly this reason: it confirms the offsets BEFORE any write uses one.</para>
/// </summary>
public static class MarkerDbLayout
{
    /// <summary>The block number, as imported and compiled into the project.</summary>
    public const int DbNumber = 38;

    /// <summary>The block's symbolic name — and the area name the write fence is scoped on.</summary>
    public const string AreaName = "DB_RigMarker";

    /// <summary>Declared capacity of each <c>String[n]</c> member.</summary>
    public const int StringDeclaredMax = 32;

    /// <summary><c>Format : Int</c> — a version stamp for readers of this block. Value 1.</summary>
    public const int FormatOffset = 0;

    /// <summary><c>RigMarker : String[32]</c> — the unit identifier the write fence verifies against.</summary>
    public const int RigMarkerOffset = 2;

    /// <summary><c>OrderNumber : String[32]</c> — the CPU article number, published by the program
    /// because the CPU also reports it, which makes it a cross-check on the offsets.</summary>
    public const int OrderNumberOffset = 36;

    /// <summary><c>SerialNumber : String[32]</c> — declared, allocated, empty, and read by nothing.</summary>
    public const int SerialNumberOffset = 70;

    /// <summary>The block size TIA reports for the standard-layout form.</summary>
    public const int TotalBytes = 104;

    /// <summary>Bytes one <c>String[32]</c> member occupies, header included.</summary>
    public static int StringSize => S7StringCodec.SizeOf(StringDeclaredMax);

    /// <summary>
    /// Do the computed offsets and the reported block size agree? They must, or one of the two
    /// statements about this block is wrong and neither should be used to address it.
    /// </summary>
    public static bool IsSelfConsistent =>
        RigMarkerOffset == FormatOffset + 2
        && OrderNumberOffset == RigMarkerOffset + StringSize
        && SerialNumberOffset == OrderNumberOffset + StringSize
        && TotalBytes == SerialNumberOffset + StringSize;

    /// <summary>The whole block as one restorable region, for the read that confirms the layout.</summary>
    public static RestoreRegion WholeBlock => new(AreaName, DbNumber, 0, TotalBytes);

    /// <summary>The reserved <c>SerialNumber</c> member as one region — the first write's target.</summary>
    public static RestoreRegion SerialNumberRegion => new(AreaName, DbNumber, SerialNumberOffset, StringSize);

    public static string Describe() =>
        $"DB{DbNumber} {AreaName} ({TotalBytes} bytes, STANDARD access): " +
        $"Format Int @DBW{FormatOffset}; " +
        $"RigMarker String[{StringDeclaredMax}] @DBB{RigMarkerOffset}; " +
        $"OrderNumber String[{StringDeclaredMax}] @DBB{OrderNumberOffset}; " +
        $"SerialNumber String[{StringDeclaredMax}] @DBB{SerialNumberOffset}. " +
        "Offsets COMPUTED from the S7 layout rules, not read off the device.";
}
