using System.Security.Cryptography;
using System.Text;

namespace Harness.Map;

/// <summary>A contiguous run of holding registers: where it starts and how many.</summary>
public readonly record struct RegisterRange(int Register, int Length)
{
    /// <summary>One past the last register in the range.</summary>
    public int End => Register + Length;

    public override string ToString() => Length == 1 ? $"[{Register}]" : $"[{Register}..{End - 1}]";
}

/// <summary>
/// One slot's place in the map: its ordinal, its vector window, its result window and its start bit.
///
/// <para><see cref="Excised"/> marks a slot that was allocated and will never run — D32's excision
/// path. It is deliberately NOT part of the map's identity: an excised slot keeps its address and the
/// map keeps its hash, so excision costs a copy-layer regeneration and leaves every client mirror
/// valid. Re-deriving the map instead would churn the hash, invalidate the clients and buy nothing.</para>
/// </summary>
public sealed record SlotAllocation(
    string SlotId,
    int Index,
    RegisterRange Vector,
    RegisterRange Result,
    int StartBoolRegister,
    int StartBitInRegister,
    bool Excised = false);

/// <summary>
/// The derived register map for one wave set — the frozen answer to "where does everything live".
///
/// <para><b>Layout, and each of the three decisions below is forced by something measured:</b></para>
/// <code>
///   [ control  ] version (2 registers, a 32-bit build stamp), scan counter (2 registers, a DInt),
///                then the start bools, ceil(N/16) registers
///   [ vectors  ] N x VectorRegistersPerSlot, all slots contiguous
///   [ results  ] N x ResultRegistersPerSlot, all slots contiguous
/// </code>
///
/// <para><b>0. The version register is FIRST, and that is not cosmetic</b> (§9, DB-6). DB-6 requires
/// the layout version to be checked "before EVERY transaction batch, not only at connect — a download
/// can land mid-session". Placing it at register 0, inside the control region, means the check rides on
/// the poll that was happening anyway: one FC03 returns the build stamp AND the scan counter AND the
/// start-bool echo. Anywhere else it would be a round trip of its own, and round trips are the only
/// thing measured to cost.</para>
///
/// <para><b>1. Slots are fixed-size</b> (X-A): every slot gets the widest slot's width, so the client's
/// bounds check is arithmetic rather than a lookup, and an excised slot leaves a hole rather than
/// shifting its neighbours.</para>
///
/// <para><b>2. Vectors are grouped, not interleaved with results.</b> The wire unit is
/// <c>modbusTensor(i)</c> — the i-th vector of EVERY slot, concatenated (D26a) — so grouping makes the
/// whole tensor one contiguous run written in the fewest possible FC16s. Interleaving vector and result
/// per slot would force one write transaction per slot, and round trips are the thing that costs.</para>
///
/// <para><b>3. Results are per-slot regions and a read never straddles two.</b> Polling happens DURING
/// a test, so a read spanning transactions can catch a publish half-done; per-slot coherence is
/// sufficient because slots are independent by construction (D9). Hence one FC03 per slot, and hence
/// the 125-register cap on <see cref="ResultRegistersPerSlot"/> — refused at derivation time.</para>
///
/// <para><b>What is NOT here, on purpose:</b> no packing of two values into one register, no
/// multi-slot-per-read, no claims, no coverage, no deferred queue, no executed-start-bool echo. Every
/// one of those is width and none of it is proven yet.</para>
/// </summary>
public sealed record RegisterMap(
    MirrorGeometry Geometry,
    RegisterRange Version,
    RegisterRange ScanCounter,
    RegisterRange StartBools,
    RegisterRange VectorBlock,
    RegisterRange ResultBlock,
    int VectorRegistersPerSlot,
    int ResultRegistersPerSlot,
    IReadOnlyList<SlotAllocation> Slots)
{
    /// <summary>Registers the scan counter occupies. A DInt, so two — and it will wrap; stamps are differences from T=0.</summary>
    public const int ScanCounterRegisters = 2;

    /// <summary>
    /// Registers the version register occupies. TWO, because it is a <c>%MD</c> and not a <c>%MW</c>.
    ///
    /// <para>§9 corrected this by audit: the mechanism is <c>MOVE 16#A93F2C71 -&gt; MD_ProgramVersion</c>,
    /// eight hex digits, 32 bits. A 16-bit truncation of a build hash would also collide far too easily
    /// to serve as an identity, which is the job it exists for.</para>
    /// </summary>
    public const int VersionRegisters = 2;

    /// <summary>Slots whose start bools fit in one holding register.</summary>
    public const int SlotsPerStartRegister = 16;

    /// <summary>The whole control region — version, scan counter and start bools — in ONE FC03.</summary>
    public RegisterRange Control => new(Version.Register, StartBools.End - Version.Register);

    /// <summary>Total registers the map occupies, from register 0.</summary>
    public int TotalRegisters => ResultBlock.End;

    /// <summary>
    /// Round trips one poll cycle costs: one FC03 for the control region plus one per slot.
    ///
    /// <para><b>This is the cost figure, and it is counted in round trips because registers were
    /// measured free</b> (1, 4, 8 and 16 registers all cost the same within noise, phase 1.2). Widening
    /// every slot in the wave set does not change this number; adding one slot increases it by exactly
    /// one. That relationship is asserted by test, because it is the premise the whole allocator is
    /// built on and the spec's D29 originally assumed the opposite.</para>
    /// </summary>
    public int PollRoundTrips => 1 + Slots.Count;

    /// <summary>FC16 transactions the vector phase costs. May exceed one — see <see cref="VectorWrite"/>.</summary>
    public int VectorWriteTransactions => ModbusLimits.WriteTransactions(VectorBlock.Length);

    /// <summary>
    /// FC16 transactions the commit costs. Always exactly one, and the allocator refuses any wave set
    /// where it would not be.
    ///
    /// <para>This is what makes a torn vector write harmless (X-A): the data phase may span as many
    /// transactions as it likes because nothing is running while it is written, and the START is the
    /// commit. A tear in the data phase leaves the start bools unraised, so nothing runs and it is a
    /// detectable non-event rather than a plausible wrong answer.</para>
    /// </summary>
    public int CommitTransactions => 1;

    /// <summary>Where the client reads slot <paramref name="index"/>'s results — one FC03, never straddling.</summary>
    public RegisterRange ResultRead(int index) => Slots[index].Result;

    /// <summary>
    /// Where the client writes slot <paramref name="index"/>'s vector. Not necessarily one transaction,
    /// and deliberately not capped to one: X-A makes the data phase non-atomic BY DESIGN, so narrowing a
    /// slot to fit FC16 would trade a free register for a constraint that buys nothing.
    /// </summary>
    public RegisterRange VectorWrite(int index) => Slots[index].Vector;

    /// <summary>The whole wire tensor for one wave index — every slot's vector, contiguous (D26a).</summary>
    public RegisterRange WireTensor => VectorBlock;

    /// <summary>Slot lookup by id, or null.</summary>
    public SlotAllocation? Slot(string slotId) =>
        Slots.FirstOrDefault(s => string.Equals(s.SlotId, slotId, StringComparison.Ordinal));

    /// <summary>
    /// The same map with one slot marked excised — allocated, addressed, and null at every index.
    ///
    /// <para>Its start bool simply never rises, so D33's inert holds and the values are don't-care.
    /// <see cref="MapHash"/> is unchanged by construction, which is the property DB-6's client-side
    /// protection keys on.</para>
    /// </summary>
    public RegisterMap WithSlotExcised(string slotId)
    {
        if (Slot(slotId) is null)
            throw new ArgumentException($"no slot '{slotId}' in this map.", nameof(slotId));

        return this with
        {
            Slots = Slots.Select(s => s.SlotId == slotId ? s with { Excised = true } : s).ToArray(),
        };
    }

    /// <summary>
    /// A stable hash of the ALLOCATION — geometry, region bases and widths, and the slot ids in
    /// ordinal order. Excision is not an input, so an excised map hashes identically to the map it
    /// came from.
    /// </summary>
    public string MapHash
    {
        get
        {
            var canonical = new StringBuilder();
            canonical.Append("harness-map/1\n");
            canonical.Append($"mem={Geometry.TotalBytes} retain={Geometry.RetentiveBytes} base={Geometry.BaseByte}\n");
            canonical.Append($"ver={Version.Register}:{Version.Length}\n");
            canonical.Append($"scan={ScanCounter.Register}:{ScanCounter.Length}\n");
            canonical.Append($"start={StartBools.Register}:{StartBools.Length}\n");
            canonical.Append($"vec={VectorBlock.Register}:{VectorBlock.Length}/{VectorRegistersPerSlot}\n");
            canonical.Append($"res={ResultBlock.Register}:{ResultBlock.Length}/{ResultRegistersPerSlot}\n");
            foreach (var slot in Slots)
                canonical.Append($"slot={slot.Index}:{slot.SlotId}\n");

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
