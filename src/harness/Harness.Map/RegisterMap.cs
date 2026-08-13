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
/// A run of CONSECUTIVE WHOLE SLOTS — the only unit in which results are read (X-A, F-1).
///
/// <para>There is no register in this type, and that is the point. X-A: "a split read must be
/// UNEXPRESSIBLE, not merely rejected… there is no parameter in which a partial slot could be named."
/// A caller wanting half a slot has nothing to type.</para>
/// </summary>
public readonly record struct SlotSpan(int FirstSlot, int SlotCount)
{
    /// <summary>One past the last slot in the run.</summary>
    public int End => FirstSlot + SlotCount;

    /// <summary>Slot ordinals this run covers.</summary>
    public IEnumerable<int> Slots => Enumerable.Range(FirstSlot, SlotCount);

    public override string ToString() => SlotCount == 1 ? $"slot {FirstSlot}" : $"slots {FirstSlot}..{End - 1}";
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
///                the start bools, ceil(N/16) registers, then the START ECHO, ceil(N/16) more
///   [ vectors  ] N x VectorRegistersPerSlot, all slots contiguous
///   [ results  ] N x ResultRegistersPerSlot, all slots contiguous
/// </code>
///
/// <para><b>0a. The start ECHO is what the program actually ran, and it is not the same fact as the
/// start bools</b> (X-E). The bools are what the client COMMANDED; the echo is published by the copy
/// layer from the block's own start condition — the far side of the coil — so it records what the
/// program saw. X-E asks for the co-running log to be built from executed start bools "NEVER FROM THE
/// PLAN", because a log generated from the planned slot set names slots that never executed, and §7a's
/// cause-4 attribution then points at a phantom. It costs one register per sixteen slots.</para>
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
/// <para><b>3. Results are per-slot regions and a read never SPLITS one.</b> Polling happens DURING a
/// test, so a read returning PART of a slot can catch that slot's publish half-done — a coherent-looking
/// result nobody ever published. Per-slot coherence is sufficient because slots are independent by
/// construction (D9), and hence the 125-register cap on <see cref="ResultRegistersPerSlot"/>, refused at
/// derivation time.</para>
///
/// <para><b>3a. AMENDED 2026-08-13 (F-1, adopted): a read may cover several WHOLE slots.</b> The rule
/// read "never straddles two slots" until then, and that forbade a superset of the actual hazard — the
/// forbidden thing was never "touching two slots", it was "touching half of one". A read covering three
/// entire slots is still ONE transaction, and each slot inside it is either wholly before or wholly
/// after any publish. The surplus was costing round trips, which are the scarce resource.
/// <see cref="SlotsPerRead"/> is what that buys.</para>
///
/// <para><b>3b. AND IT MOVED A COST RATHER THAN ONLY REMOVING ONE: padding a slot is no longer free.</b>
/// Widening a slot costs ~0.040 ms per register on the wire — negligible — but widening it far enough to
/// drop <see cref="SlotsPerRead"/> costs a WHOLE ROUND TRIP per read per slot-group, 78 ms typical and
/// 201 ms at the p99. Slots are fixed-size across a wave set, so <b>one wide slot collapses R for every
/// slot in the set</b>. Whether admission should therefore group by slot size is F-6, open with the
/// owner and deliberately not answered here: nothing in this type groups, reorders or sizes anything.</para>
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
    RegisterRange StartEcho,
    RegisterRange VectorBlock,
    RegisterRange ResultBlock,
    int VectorRegistersPerSlot,
    int ResultRegistersPerSlot,
    IReadOnlyList<SlotAllocation> Slots)
{
    /// <summary>
    /// <b>A map whose regions overlap cannot be constructed.</b>
    ///
    /// <para>This is an interference check, and it is the FIRST one phase 3 needs. Two slots aliased onto
    /// one register is agent A's write landing in agent B's mirror — DB-6 calls that the one genuine leak
    /// in the design and says the protection can be BY CONSTRUCTION. The layout the allocator produces is
    /// contiguous, so an overlap is impossible as written; this exists so that an arithmetic slip cannot
    /// produce a map that allocates cleanly, hashes stably, and silently aliases two agents.</para>
    ///
    /// <para>It THROWS rather than refusing, unlike everything in <c>MapAllocator</c>, and the difference
    /// is deliberate: a bad wave-set request is a caller's error and gets a refusal, while an overlapping
    /// map is a defect in the derivation itself and has no caller to report it to.</para>
    /// </summary>
    private readonly bool _regionsAreDisjoint =
        Validated(Version, ScanCounter, StartBools, StartEcho, VectorBlock, ResultBlock, Slots);

    /// <summary>Always true — the map cannot be constructed otherwise. Present so the check cannot be elided.</summary>
    public bool RegionsAreDisjoint => _regionsAreDisjoint;

    private static bool Validated(
        RegisterRange version,
        RegisterRange scanCounter,
        RegisterRange startBools,
        RegisterRange startEcho,
        RegisterRange vectorBlock,
        RegisterRange resultBlock,
        IReadOnlyList<SlotAllocation> slots)
    {
        var overlaps = RegionOverlaps(version, scanCounter, startBools, startEcho, vectorBlock, resultBlock, slots);

        if (overlaps.Count > 0)
        {
            throw new ArgumentException(
                "this register map aliases regions onto one another:" + Environment.NewLine + "  - "
                + string.Join(Environment.NewLine + "  - ", overlaps));
        }

        return true;
    }

    /// <summary>Every pair of same-level windows that share a register, plus any slot outside its block.</summary>
    public static IReadOnlyList<string> RegionOverlaps(
        RegisterRange version,
        RegisterRange scanCounter,
        RegisterRange startBools,
        RegisterRange startEcho,
        RegisterRange vectorBlock,
        RegisterRange resultBlock,
        IReadOnlyList<SlotAllocation> slots)
    {
        var refusals = new List<string>();

        Pairwise(new[]
        {
            ("version", version), ("scan counter", scanCounter), ("start bools", startBools),
            ("start echo", startEcho), ("vectors", vectorBlock), ("results", resultBlock),
        }, refusals);

        Pairwise((slots ?? Array.Empty<SlotAllocation>()).Select(s => ($"slot '{s.SlotId}' vector", s.Vector)).ToArray(), refusals);
        Pairwise((slots ?? Array.Empty<SlotAllocation>()).Select(s => ($"slot '{s.SlotId}' result", s.Result)).ToArray(), refusals);

        foreach (var slot in slots ?? Array.Empty<SlotAllocation>())
        {
            if (!Within(slot.Vector, vectorBlock))
                refusals.Add($"slot '{slot.SlotId}' vector {slot.Vector} is not inside the vector block {vectorBlock}.");

            if (!Within(slot.Result, resultBlock))
                refusals.Add($"slot '{slot.SlotId}' result {slot.Result} is not inside the result block {resultBlock}.");
        }

        return refusals;
    }

    private static void Pairwise(IReadOnlyList<(string Name, RegisterRange Range)> windows, List<string> refusals)
    {
        for (var i = 0; i < windows.Count; i++)
        {
            for (var j = i + 1; j < windows.Count; j++)
            {
                var (leftName, left) = windows[i];
                var (rightName, right) = windows[j];

                if (left.Length == 0 || right.Length == 0)
                    continue;

                if (left.Register < right.End && right.Register < left.End)
                {
                    refusals.Add($"{leftName} {left} and {rightName} {right} share registers. Two slots aliased onto one register is agent A's write landing in agent B's mirror — the one genuine leak this design has (DB-6), and it would not show up as an error anywhere downstream.");
                }
            }
        }
    }

    private static bool Within(RegisterRange inner, RegisterRange outer) =>
        inner.Length == 0 || (inner.Register >= outer.Register && inner.End <= outer.End);

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

    /// <summary>The whole control region — version, scan counter, start bools and start echo — in ONE FC03.</summary>
    public RegisterRange Control => new(Version.Register, StartEcho.End - Version.Register);

    /// <summary>
    /// Every named region, for the disjointness post-condition. Order is the layout's own.
    /// </summary>
    public IReadOnlyList<(string Name, RegisterRange Range)> Regions => new[]
    {
        ("version", Version),
        ("scan counter", ScanCounter),
        ("start bools", StartBools),
        ("start echo", StartEcho),
        ("vectors", VectorBlock),
        ("results", ResultBlock),
    };

    /// <summary>Total registers the map occupies, from register 0.</summary>
    public int TotalRegisters => ResultBlock.End;

    /// <summary>
    /// <b>How many WHOLE slots fit in one FC03 read</b> — <c>R = floor(125 / slot_size)</c> (X-A, F-1).
    ///
    /// <para>Always at least 1: the allocator refuses a result region wider than one FC03 read at
    /// derivation time, so the division can never be less than one for a map that exists.</para>
    ///
    /// <para><b>Slot width has entered the round-trip count for the first time — in the DENOMINATOR.</b>
    /// Registers still do not cost on the wire; what they now do is decide how many slots share a read.
    /// W is free per-register and expensive per-R-step.</para>
    /// </summary>
    public int SlotsPerRead => Math.Max(1, ModbusLimits.MaxReadRegisters / ResultRegistersPerSlot);

    /// <summary>
    /// Round trips one poll cycle costs: one FC03 for the control region, plus one per group of whole
    /// slots (<c>ceil(K / R)</c>).
    ///
    /// <para><b>This is the cost figure, and it is counted in round trips</b> because registers cost
    /// ~0.040 ms each — ~6% of a round trip at full width, against 100% for a second round trip. Two
    /// relationships are asserted by test, and under F-1 they are no longer the same relationship:</para>
    /// <list type="bullet">
    /// <item>widening the VECTOR region never changes this number — reads key on the result region;</item>
    /// <item>widening the RESULT region CAN raise it, by dropping <see cref="SlotsPerRead"/>. Before
    /// F-1 that was false, and the test that asserted the old blanket claim was inverted deliberately
    /// rather than deleted.</item>
    /// </list>
    /// </summary>
    public int PollRoundTrips => 1 + ReadPlan(Enumerable.Range(0, Slots.Count)).Count;

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

    /// <summary>Where the client reads one slot's results.</summary>
    public RegisterRange ResultRead(int index) => ResultRead(new SlotSpan(index, 1));

    /// <summary>
    /// <b>The register range a slot run occupies — DERIVED, never supplied.</b>
    ///
    /// <para>This is the whole of F-1's enforcement, and X-A is explicit that it is the SHAPE OF THE
    /// CALL rather than a constraint on its arguments: the caller names a first slot and a count of
    /// slots, and there is no register offset to pass. A caller wanting half a slot has nothing to type.
    /// <b>Do not add an overload taking a register range "for convenience"</b> — a validator over one is
    /// exactly what X-A forbids, because the unsafe call would still exist, still compile, and be one
    /// refactor from being reached.</para>
    ///
    /// <para>Throws rather than refuses, for the same reason the region-disjointness check does: a run
    /// that overruns the map or exceeds one FC03 is a defect in the caller's arithmetic, not a bad
    /// request from a user.</para>
    /// </summary>
    public RegisterRange ResultRead(SlotSpan run)
    {
        if (run.SlotCount < 1)
            throw new ArgumentOutOfRangeException(nameof(run), run.SlotCount, "a read of no slots is not a transaction. Empty is not clean.");

        if (run.FirstSlot < 0 || run.End > Slots.Count)
            throw new ArgumentOutOfRangeException(nameof(run), run.ToString(), $"this map holds {Slots.Count} slot(s); that run runs off the end of it.");

        if (run.SlotCount > SlotsPerRead)
        {
            throw new ArgumentOutOfRangeException(nameof(run), run.SlotCount,
                $"{run.SlotCount} slots of {ResultRegistersPerSlot} register(s) is {run.SlotCount * ResultRegistersPerSlot} registers, past the {ModbusLimits.MaxReadRegisters}-register FC03 limit. At this slot size a read covers at most {SlotsPerRead} whole slot(s).");
        }

        return new RegisterRange(Slots[run.FirstSlot].Result.Register, run.SlotCount * ResultRegistersPerSlot);
    }

    /// <summary>
    /// The fewest whole-slot reads that cover every slot named, each at most <see cref="SlotsPerRead"/>
    /// wide.
    ///
    /// <para>Groups by ADJACENCY, which is a property of the layout — never by size, never by
    /// reordering, and never by any admission policy. Whether wave-set admission should group by slot
    /// size is F-6, open with the owner; nothing here presumes an answer, and a slot that is already
    /// finished may ride along inside a group that was happening anyway, which costs nothing.</para>
    /// </summary>
    public IReadOnlyList<SlotSpan> ReadPlan(IEnumerable<int> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        var wanted = slots.Distinct().OrderBy(i => i).ToArray();

        foreach (var slot in wanted)
        {
            if (slot < 0 || slot >= Slots.Count)
                throw new ArgumentOutOfRangeException(nameof(slots), slot, $"this map holds {Slots.Count} slot(s).");
        }

        var plan = new List<SlotSpan>();
        var covered = 0;

        // Leftmost-greedy: start a run at the first uncovered slot and let it REACH as far as one FC03
        // allows. Optimal in transactions, which is what costs — for covering points with intervals that
        // must begin at a wanted point, taking the maximum reach each time is optimal.
        //
        // The run is then TRIMMED to the last slot it actually covers. Extending to the full reach would
        // cost the same one transaction and read registers nobody asked for, and registers are ~0.040 ms
        // each — negligible against a round trip and not zero. A slot BETWEEN two wanted ones still rides
        // along, because excluding it would cost a whole transaction to save a register.
        while (covered < wanted.Length)
        {
            var first = wanted[covered];
            var reach = first + Math.Min(SlotsPerRead, Slots.Count - first);
            var last = first;

            while (covered < wanted.Length && wanted[covered] < reach)
                last = wanted[covered++];

            plan.Add(new SlotSpan(first, last - first + 1));
        }

        return plan;
    }

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
            canonical.Append($"echo={StartEcho.Register}:{StartEcho.Length}\n");
            canonical.Append($"vec={VectorBlock.Register}:{VectorBlock.Length}/{VectorRegistersPerSlot}\n");
            canonical.Append($"res={ResultBlock.Register}:{ResultBlock.Length}/{ResultRegistersPerSlot}\n");
            foreach (var slot in Slots)
                canonical.Append($"slot={slot.Index}:{slot.SlotId}\n");

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
