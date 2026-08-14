namespace Harness.Map;

/// <summary>
/// Where the <c>%MW</c> mirror sits in bit memory, and what that leaves to allocate.
///
/// <para><b>Why bit memory and not a DB</b> (spec §6): an IR-authored DB imports with no
/// <c>MemoryLayout</c> element at all, TIA applies the S7-1200 default of Optimized, and
/// <c>MB_HOLD_REG</c> then refuses it with <c>16#818C</c> — while import, compile and
/// <c>drift-check</c> all stay green. A DB-backed mirror is one re-import away from being invisible on
/// the wire with nothing in the toolchain able to see it happen. <c>%MW</c> cannot suffer that.</para>
///
/// <para><b>Capacity, measured 2026-08-12:</b> the 1214C has <b>8192 bytes / 4096 words</b> of bit
/// memory — double the figure the spec originally carried — and bit memory is SEPARATE from work
/// memory, so the mirror costs zero work budget. (1211C and 1212C have 4096 bytes; 1214C/1215C/1217C
/// and all G2 models have 8192. This project's rig is the classic 1214C, <c>6ES7 214-1AG40-0XB0</c>.)</para>
///
/// <para><b>The placement rule, and it is the whole of item 0.1b's mirror half.</b> Retentive bit
/// memory ALWAYS starts at MB0 and runs contiguously upward, and retentive <c>M</c> DOES count against
/// retain memory — the tightest budget on the rig by a factor of twelve. A <c>%M</c> tag carries no
/// per-tag retain flag of its own (a PLC tag table has no Remanence concept at all — see
/// <c>ir/SPEC.md</c>'s TAGTABLE grammar), so <b>for the mirror, "non-retentive" is not an attribute
/// that can be asserted on the object — it is a property of the ADDRESS.</b> Placing the mirror above
/// the retentive window is therefore not a precaution, it is the entire mechanism, and
/// <see cref="RetentionCheck"/> re-checks every generated tag against it rather than trusting that the
/// allocator got it right.</para>
///
/// <para><b><see cref="RetentiveBytes"/> has no default, deliberately.</b> It is a property of the
/// program being tested, not of the CPU, and D36's discipline is that a deferred measurement must never
/// acquire "a default that could be mistaken for a measurement". A caller that does not know the
/// program's retentive <c>M</c> extent must go and read it.</para>
/// </summary>
/// <param name="TotalBytes">Bit-memory size of the CPU, in bytes.</param>
/// <param name="RetentiveBytes">
/// Bytes of <c>M</c> configured retentive, counted from MB0. Zero is a legitimate value and means the
/// program declares no retentive bit memory — but it must be stated, never assumed.
/// </param>
/// <param name="BaseByte">Byte address of Modbus holding register 0 — the <c>MB_HOLD_REG</c> pointer.</param>
public sealed record MirrorGeometry(int TotalBytes, int RetentiveBytes, int BaseByte)
{
    /// <summary>Bit memory on the classic 1214C: 8192 bytes / 4096 words [R, System Manual V4.4 Table A-48].</summary>
    public const int Cpu1214CBitMemoryBytes = 8192;

    /// <summary>The 1214C geometry, with the two program-dependent numbers supplied by the caller.</summary>
    public static MirrorGeometry ForCpu1214C(int retentiveBytes, int baseByte) =>
        new(Cpu1214CBitMemoryBytes, retentiveBytes, baseByte);

    /// <summary>Holding registers addressable from <see cref="BaseByte"/> to the top of bit memory.</summary>
    public int AvailableRegisters => Refusals.Count > 0 ? 0 : (TotalBytes - BaseByte) / 2;

    /// <summary>Byte address of the <c>%MW</c> word carrying holding register <paramref name="register"/>.</summary>
    /// <remarks>
    /// <c>MB_HOLD_REG = P#M&lt;BaseByte&gt;.0 WORD n</c> makes register r the word at
    /// <c>BaseByte + 2r</c> — confirmed by the phase-1 spike's own address windows (register 0 read as
    /// <c>%MW1000</c> and register 200 as <c>%MW1400</c> against a base of 1000).
    /// </remarks>
    public int ByteAddressOf(int register) => BaseByte + (2 * register);

    /// <summary>IR/TIA address of the word carrying holding register <paramref name="register"/>.</summary>
    public string WordAddressOf(int register) => $"%MW{ByteAddressOf(register)}";

    /// <summary>IR/TIA address of the double word starting at holding register <paramref name="register"/>.</summary>
    public string DoubleWordAddressOf(int register) => $"%MD{ByteAddressOf(register)}";

    /// <summary>
    /// IR/TIA address of one BIT inside a holding register, given the bit's position in the register's
    /// 16-bit value (0 = least significant).
    ///
    /// <para><b>[I] — INFERRED, NOT MEASURED, and this is the one place in this file that is.</b> A
    /// Modbus register travels the wire big-endian (high byte first) and an S7 <c>%MW</c> is big-endian
    /// too, so the wire's high byte lands in <c>M[byte]</c> and its low byte in <c>M[byte+1]</c>.
    /// Bit 0 of the register value therefore lands in the SECOND byte, not the first — which is the
    /// counter-intuitive half and the reason this is a method rather than an open-coded expression.
    /// The phase-1 spike wrote and compared whole words, so it exercised word order and NOT bit order
    /// within a word.</para>
    ///
    /// <para>🔴 <b>THE MARKER STAYS AFTER A PARTIAL READING ON 2026-08-14, AND THE REASON IS THE MARKER'S
    /// OWN.</b> The bit was read on the device that day and came back <b>FALSE</b>. That RULES OUT the
    /// byte-swapped mapping; it does <b>not</b> CONFIRM this one. A false reading is equally consistent
    /// with <i>"the mapping is wrong and the bit we happened to land on is also false"</i> — so what was
    /// eliminated is one alternative, not the doubt. <b>Downgrading <c>[I]</c> on that would be replacing
    /// a weak inference with a slightly-less-weak one, which is not progress</b>, and this marker exists
    /// precisely because the simulator and this method agree FROM THE SAME PREMISE and their agreement is
    /// therefore worth nothing.</para>
    ///
    /// <para><b>The experiment that settles it is unchanged and still costs one write:</b> put
    /// <c>16#0001</c> in the start-bool register and read back which <c>%M</c> bit ROSE. A bit going
    /// TRUE where nothing else could have put it is positive evidence; a bit reading false is not. If it
    /// is <c>M[byte+1].0</c> this is right; if it is <c>M[byte].0</c> the two bytes swap here and nowhere
    /// else.</para>
    ///
    /// <para><b>Contrast with the 32-bit word order, which WAS discharged</b> (<c>RegisterWordOrder</c>):
    /// there the readings were of patterns with DISTINGUISHABLE HALVES, so each observation could only
    /// have come out one way under one hypothesis. That is what a calibration looks like, and it is what
    /// this one still lacks.</para>
    /// </summary>
    public string BitAddressOf(int register, int bitInRegister)
    {
        if (bitInRegister is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(bitInRegister), bitInRegister, "a holding register has 16 bits.");

        var word = ByteAddressOf(register);
        return bitInRegister < 8
            ? $"%M{word + 1}.{bitInRegister}"
            : $"%M{word}.{bitInRegister - 8}";
    }

    /// <summary>
    /// Everything wrong with this geometry, or empty. Checked before any allocation is attempted, so a
    /// bad placement is a refusal rather than a map nobody can trust.
    /// </summary>
    public IReadOnlyList<string> Refusals
    {
        get
        {
            var refusals = new List<string>();

            if (TotalBytes <= 0 || TotalBytes % 2 != 0)
                refusals.Add($"bit-memory size must be a positive even number of bytes; got {TotalBytes}.");

            if (RetentiveBytes < 0)
                refusals.Add($"retentive M extent cannot be negative; got {RetentiveBytes}.");

            if (BaseByte < 0)
                refusals.Add($"mirror base cannot be negative; got {BaseByte}.");
            else if (BaseByte % 2 != 0)
                refusals.Add($"mirror base must be word-aligned (even); got %M{BaseByte}. Holding register r is the word at base + 2r, so an odd base straddles every register.");

            if (BaseByte >= 0 && RetentiveBytes >= 0 && BaseByte < RetentiveBytes)
                refusals.Add($"mirror base %M{BaseByte} is INSIDE the retentive M window (MB0..MB{RetentiveBytes - 1}). Retentive M starts at MB0 and runs contiguously upward, so a mirror placed low silently consumes retain memory — the one hard memory restriction (build plan 0.1b). Place it above MB{RetentiveBytes - 1}.");

            if (TotalBytes > 0 && BaseByte >= 0 && BaseByte >= TotalBytes)
                refusals.Add($"mirror base %M{BaseByte} is beyond the CPU's {TotalBytes} bytes of bit memory.");

            return refusals;
        }
    }

    /// <summary>True when a byte address lies at or above the retentive window — the 0.1b test itself.</summary>
    public bool IsNonRetentiveAddress(int byteAddress) => byteAddress >= RetentiveBytes;
}
