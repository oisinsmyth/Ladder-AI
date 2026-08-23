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
/// <param name="DeclaredRegisters">
/// 🔴 <b>The width <c>MB_HOLD_REG</c> actually declares — the ONLY registers a Modbus client can reach.</b>
///
/// <para><b>This is a second and much smaller ceiling than <see cref="AvailableRegisters"/>, and until
/// 2026-08-22 nothing compared a map against it.</b> Bit memory allows 3,596 registers at base
/// <c>%M1000</c>; the area pointer on the rig declares <b>576</b>. One lane uses ~165, so the gap has
/// never bitten — a batch of four would overflow it, and the symptom would not be a refusal at
/// derivation time. It would be reads REFUSED BY THE SERVER partway through the band, i.e. a wave that
/// looks like a device fault.</para>
///
/// <para><b>Required, never defaulted.</b> The same rule <c>--declared-registers</c> already enforces
/// in <c>harness-mirror-read</c>: <i>"defaulting it would let a run conclude against a width nobody
/// stated."</i> A default here would be a number this code invented, silently governing whether a map
/// fits.</para>
/// </param>
public sealed record MirrorGeometry(int TotalBytes, int RetentiveBytes, int BaseByte, int DeclaredRegisters)
{
    /// <summary>
    /// 🔴 <b>Parts of the declared area that belong to SOMETHING ELSE IN THE DEPLOYED PROGRAM, and that
    /// the mirror may therefore not enter.</b> Empty by default, and the default is the honest one — see
    /// below, because it is the opposite of <see cref="DeclaredRegisters"/>'s rule and the difference is
    /// the point.
    ///
    /// <para><b>What it is for</b> (measured 2026-08-23, live on the rig): a generated mirror and a
    /// hand-authored virtual panel both claimed registers 256–323 of one <c>%M</c> area, and 53 panel
    /// tags collided bit for bit — the copy layer overwriting the panel's master enable every scan from
    /// an unrelated fault flag. Nothing refused, because nothing in this component had ever been told
    /// that anything else lived in the area. Full account on <see cref="ReservedRegion"/>.</para>
    ///
    /// <para><b>Why this one DOES default, when <see cref="DeclaredRegisters"/> deliberately does not.</b>
    /// A default width would be <i>a number this code invented</i>, silently governing whether a map fits
    /// — the code would be asserting a measurement nobody took. An empty reservation set asserts nothing
    /// at all. It is the ABSENCE of a claim, not the claim "the area is otherwise empty", and it leaves
    /// the allocator behaving exactly as it did before this existed. <b>Which is also the honest
    /// statement of this guard's reach: it can only ever see neighbours somebody wrote down.</b> An
    /// undeclared neighbour is as invisible as the panel was, and no arrangement of this code can change
    /// that — the fix for an undeclared neighbour is to declare it, never to strengthen the check.</para>
    ///
    /// <para><b>Not a <see cref="RegisterMap.MapHash"/> input, unlike the declared width.</b> The width
    /// changed what the wire could reach, so a client and a controller could disagree about the window
    /// and still match stamps. A reservation changes NOTHING about the layout, the deployed bytes or the
    /// wire — it can only decide whether a map is allowed to exist. Two maps differing only in
    /// reservations are the same map, and hashing them apart would move every existing stamp for no fact
    /// on the device.</para>
    ///
    /// <para>Set through <see cref="Reserving(ReservedRegion[])"/> or
    /// <see cref="ReservingBytes(int, int, string)"/> rather than by hand, so the region list is always
    /// appended to and never quietly replaced.</para>
    /// </summary>
    public IReadOnlyList<ReservedRegion> ReservedRegions { get; init; } = Array.Empty<ReservedRegion>();

    /// <summary>Bit memory on the classic 1214C: 8192 bytes / 4096 words [R, System Manual V4.4 Table A-48].</summary>
    public const int Cpu1214CBitMemoryBytes = 8192;

    /// <summary>The 1214C geometry, with the three program-dependent numbers supplied by the caller.</summary>
    public static MirrorGeometry ForCpu1214C(int retentiveBytes, int baseByte, int declaredRegisters) =>
        new(Cpu1214CBitMemoryBytes, retentiveBytes, baseByte, declaredRegisters);

    /// <summary>
    /// The same geometry with <paramref name="regions"/> added to what the mirror may not enter.
    ///
    /// <para><b>An empty argument list THROWS.</b> <c>Reserving()</c> — or <c>Reserving(list.ToArray())</c>
    /// over a list that turned out to be empty — reads at the call site as "the neighbours are declared"
    /// and delivers no protection whatsoever. That is precisely the shape this whole type exists to
    /// prevent, so it is a caller defect and gets the same treatment <see cref="RegisterMap"/> gives an
    /// impossible read: it throws, because there is no user to report it to. A caller with genuinely
    /// nothing to declare does not call this.</para>
    /// </summary>
    public MirrorGeometry Reserving(params ReservedRegion[] regions)
    {
        ArgumentNullException.ThrowIfNull(regions);

        if (regions.Length == 0)
        {
            throw new ArgumentException(
                "Reserving() was called with no regions. An empty reservation reads at the call site as 'the neighbours are declared' and protects nothing — which is the defect this guard exists for, one level up. Declare the regions, or do not call this at all: a geometry with no reservations is a legitimate state and behaves exactly as it always has.",
                nameof(regions));
        }

        return this with { ReservedRegions = ReservedRegions.Concat(regions).ToArray() };
    }

    /// <summary>
    /// The same geometry with the registers covering a <c>%M</c> BYTE span reserved — the conversion
    /// DERIVED rather than typed.
    ///
    /// <para>A neighbour in the deployed program is known by its <c>%M</c> addresses; a reservation is
    /// numbered in holding registers from <see cref="BaseByte"/>. Doing that arithmetic by hand is the
    /// likeliest way to declare a band that is real, off by some registers, and therefore protects the
    /// wrong ones — a guard that looks present and is not.</para>
    ///
    /// <para><b>The span is rounded OUTWARD to whole registers</b>, because the mirror writes whole
    /// <c>%MW</c> words: a byte the mirror would share with the neighbour costs the neighbour's whole
    /// register. Rounding inward would hand back exactly the byte that gets overwritten.</para>
    ///
    /// <para>A span lying below <see cref="BaseByte"/> derives a negative register and is refused by the
    /// region itself, naming the units — it is not silently clamped to zero.</para>
    /// </summary>
    /// <param name="firstByte"><c>%M</c> byte address the neighbour starts at.</param>
    /// <param name="byteLength">How many bytes it covers.</param>
    /// <param name="owner">Who holds it, in the words a reader would search for.</param>
    public MirrorGeometry ReservingBytes(int firstByte, int byteLength, string owner)
    {
        var firstRegister = FloorDiv(firstByte - BaseByte, 2);

        // A non-positive byte length must survive as a non-positive register length, so the region's own
        // "a reservation of nothing protects nothing" refusal fires instead of being rounded up into one
        // register that looks like a real band.
        var length = byteLength <= 0
            ? byteLength
            : CeilDiv(firstByte + byteLength - BaseByte, 2) - firstRegister;

        return Reserving(new ReservedRegion(firstRegister, length, owner));
    }

    private static int FloorDiv(int value, int divisor) =>
        value >= 0 ? value / divisor : -(((-value) + divisor - 1) / divisor);

    private static int CeilDiv(int value, int divisor) =>
        value >= 0 ? (value + divisor - 1) / divisor : -((-value) / divisor);

    /// <summary>
    /// 🔴 <b>Every place a mirror region lands in somebody else's registers — the check that was missing
    /// on 2026-08-23.</b>
    ///
    /// <para>Takes the mirror's OWN named regions (<see cref="RegisterMap.Regions"/>) so that the answer
    /// covers exactly what the map declares itself to occupy, and a region added to the layout later is
    /// covered here without anyone remembering to come back. Every refusal names <b>both sides and the
    /// overlapping registers</b>, because the failure mode was not that the two did not fit — it was
    /// that nobody knew the two shared an area at all.</para>
    ///
    /// <para><b>Fails closed, twice.</b> A malformed reservation stops the comparison rather than being
    /// dropped from it, and reservations checked against no usable mirror region at all is a refusal
    /// carrying its own denominator. In both cases every reservation would otherwise report satisfied,
    /// which is the FI-44 pass over nothing.</para>
    ///
    /// <para><b>What it cannot see:</b> anything in the area that nobody declared. A reservation list is
    /// somebody's knowledge of the neighbourhood, so this check is exactly as complete as that knowledge
    /// and cannot be made more so from inside this component. It also says nothing about registers past
    /// <see cref="DeclaredRegisters"/> — a band out there is kept clear by the declared-area ceiling in
    /// <see cref="MapAllocator"/>, which is a live check and not this one.</para>
    /// </summary>
    public IReadOnlyList<string> Intrusions(IReadOnlyList<(string Name, RegisterRange Range)> mirrorRegions)
    {
        ArgumentNullException.ThrowIfNull(mirrorRegions);

        if (ReservedRegions.Count == 0)
            return Array.Empty<string>();

        var malformed = ReservedRegions.SelectMany(r => r.Refusals).ToArray();
        if (malformed.Length > 0)
        {
            return malformed
                .Select(m => $"the mirror was NOT checked against the reserved regions, because one of them cannot be read: {m}")
                .ToArray();
        }

        // The denominator. A zero-length mirror region occupies nothing and can collide with nothing, so
        // it is not one of the things examined — and if NOTHING was examined, every reservation reports
        // satisfied and the run reads exactly like a clean one.
        var examined = mirrorRegions.Where(r => r.Range.Length > 0).ToArray();
        if (examined.Length == 0)
        {
            return new[]
            {
                $"{ReservedRegions.Count} reserved region(s) were declared and the mirror presented {mirrorRegions.Count} region(s), NONE of which occupies a register. Nothing was compared, so nothing was cleared — a reservation check over an empty mirror is not a pass (FI-44).",
            };
        }

        var refusals = new List<string>();

        foreach (var reserved in ReservedRegions)
        {
            foreach (var (name, range) in examined)
            {
                if (!reserved.Overlaps(range))
                    continue;

                refusals.Add(
                    $"the mirror's {name} region {range} enters reserved registers {reserved.OverlapWith(range)}, which belong to {reserved.Describe()}. "
                    + "Both live in the same declared Modbus area and NOTHING ELSE IN THIS TOOLCHAIN COMPARES THEM: the mirror is bounded against the declared width and proved disjoint from ITSELF, so a mirror grown into a neighbour's band imports, compiles, drift-checks and downloads green while the copy layer overwrites that neighbour's values every scan. "
                    + "Shrink the wave set, or move the mirror off this area — do not widen the mirror through the reservation.");
            }
        }

        return refusals;
    }

    /// <summary>
    /// Holding registers addressable from <see cref="BaseByte"/> to the top of bit memory.
    ///
    /// <para>⚠️ <b>This is the MEMORY ceiling, not the reachable one.</b> A register inside it but beyond
    /// <see cref="DeclaredRegisters"/> exists in <c>%M</c> and cannot be read over Modbus at all. Both
    /// are checked; this one is almost never the binding constraint.</para>
    /// </summary>
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

            if (DeclaredRegisters <= 0)
                refusals.Add($"the declared Modbus area is {DeclaredRegisters} register(s). An area nothing can be read from is not a mirror — this is the width MB_HOLD_REG states in the comms block's area pointer (P#M<base>.0 WORD n).");

            // The declared area cannot extend past the memory it is a window onto. A pointer that does is
            // a program-side mistake, and it would show up as reads that succeed on the wire and address
            // memory the CPU does not have.
            if (DeclaredRegisters > 0 && TotalBytes > 0 && BaseByte >= 0 && BaseByte + (2 * DeclaredRegisters) > TotalBytes)
                refusals.Add($"the declared Modbus area of {DeclaredRegisters} register(s) from %M{BaseByte} runs to %M{BaseByte + (2 * DeclaredRegisters) - 1}, past the CPU's {TotalBytes} bytes of bit memory. Either the area pointer or the base is wrong.");

            // A reservation that cannot be read is refused here rather than dropped from the comparison
            // later: a caller who declared a neighbour and got no protection is the defect, not the cure.
            foreach (var reserved in ReservedRegions)
                refusals.AddRange(reserved.Refusals);

            // Two owners cannot both hold one register. This is the same defect the mirror check exists
            // for, one level up — and left unrefused it means the picture of the area being checked
            // against is one its own author does not agree with.
            for (var i = 0; i < ReservedRegions.Count; i++)
            {
                for (var j = i + 1; j < ReservedRegions.Count; j++)
                {
                    var left = ReservedRegions[i];
                    var right = ReservedRegions[j];

                    if (left.Overlaps(right.Range))
                        refusals.Add($"reserved regions {left.Describe()} and {right.Describe()} both claim registers {left.OverlapWith(right.Range)}. Two owners cannot hold the same registers, so one of these declarations is wrong — and the mirror would be checked against an area map its own author disagrees with.");
                }
            }

            return refusals;
        }
    }

    /// <summary>True when a byte address lies at or above the retentive window — the 0.1b test itself.</summary>
    public bool IsNonRetentiveAddress(int byteAddress) => byteAddress >= RetentiveBytes;
}
