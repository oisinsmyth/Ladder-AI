using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// The map/slot allocator, build-plan item 2.1.
///
/// Every refusal below is a design-time refusal, so every one of these tests runs with no device.
/// </summary>
public class MapAllocatorTests
{
    /// <summary>
    /// The rig's geometry with the mirror placed clear of a 256-byte retentive window.
    ///
    /// <para><c>declaredRegisters</c> defaults to the memory-derived maximum so the DECLARED ceiling is
    /// as permissive as the bit-memory one and these tests keep measuring what they were written to
    /// measure. The tests that exercise the declared ceiling pass a narrow width explicitly — which is
    /// the honest shape, since on the rig it is 576 and not 2,096.</para>
    /// </summary>
    private static MirrorGeometry Rig(int retentiveBytes = 256, int baseByte = 4000, int? declaredRegisters = null) =>
        MirrorGeometry.ForCpu1214C(
            retentiveBytes, baseByte,
            declaredRegisters ?? (MirrorGeometry.Cpu1214CBitMemoryBytes - baseByte) / 2);

    private static WaveSetRequest Wave(params SlotRequest[] slots) => new(Rig(), slots);

    // ---------------------------------------------------------------------------------------------
    // Layout
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Allocates_control_then_vectors_then_results()
    {
        var map = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", VectorRegisters: 4, ResultRegisters: 8),
            new SlotRequest("S1", VectorRegisters: 2, ResultRegisters: 3))).Require();

        Assert.Equal(new RegisterRange(0, 2), map.Version);
        Assert.Equal(new RegisterRange(2, 2), map.ScanCounter);
        Assert.Equal(new RegisterRange(4, 1), map.StartBools);
        Assert.Equal(new RegisterRange(5, 1), map.StartEcho);

        // Widest slot in the wave set sets both widths, for every slot (X-A: slots are fixed-size).
        Assert.Equal(4, map.VectorRegistersPerSlot);
        Assert.Equal(8, map.ResultRegistersPerSlot);

        Assert.Equal(new RegisterRange(6, 8), map.VectorBlock);
        Assert.Equal(new RegisterRange(14, 16), map.ResultBlock);
        Assert.Equal(30, map.TotalRegisters);
    }

    [Fact]
    public void The_control_region_is_one_read_covering_version_scan_counter_start_bools_and_the_echo()
    {
        // DB-6 wants the version register checked before EVERY transaction batch, and X-E wants the
        // co-running log built from executed start bools. Both are free only if they share the poll the
        // client was making anyway, which means one contiguous region.
        var map = MapAllocator.Allocate(Wave(new SlotRequest("S0", 4, 8))).Require();

        Assert.Equal(0, map.Control.Register);
        Assert.Equal(map.StartEcho.End, map.Control.End);
        Assert.Equal(
            map.Version.Length + map.ScanCounter.Length + map.StartBools.Length + map.StartEcho.Length,
            map.Control.Length);
    }

    [Fact]
    public void The_echo_is_as_wide_as_the_start_bools_so_every_commanded_slot_has_somewhere_to_answer()
    {
        foreach (var count in new[] { 1, 2, 16, 17, 33 })
        {
            var map = MapAllocator.Allocate(new WaveSetRequest(Rig(),
                Enumerable.Range(0, count).Select(i => new SlotRequest($"S{i}", 1, 1)).ToArray())).Require();

            Assert.Equal(map.StartBools.Length, map.StartEcho.Length);
            Assert.Equal(map.StartBools.End, map.StartEcho.Register);
        }
    }

    [Fact]
    public void Every_region_in_the_map_is_pairwise_disjoint()
    {
        // The FIRST way two slots could interfere has nothing to do with the program under test: it is a
        // map in which one slot's registers overlap another's, so agent A's write lands in agent B's
        // mirror. DB-6 calls that the one genuine leak in the design and says the protection can be BY
        // CONSTRUCTION. The layout is contiguous, so overlap is impossible as written — which is exactly
        // why the allocator checks rather than assumes, and why this walks a range of shapes.
        foreach (var (slots, vector, result) in new[] { (1, 1, 1), (2, 2, 2), (3, 7, 5), (17, 1, 25), (5, 123, 4) })
        {
            var map = MapAllocator.Allocate(new WaveSetRequest(Rig(),
                Enumerable.Range(0, slots).Select(i => new SlotRequest($"S{i}", vector, result)).ToArray())).Require();

            var windows = map.Regions
                .Concat(map.Slots.Select(s => ($"{s.SlotId}.v", s.Vector)))
                .Concat(map.Slots.Select(s => ($"{s.SlotId}.r", s.Result)))
                .ToArray();

            foreach (var register in Enumerable.Range(0, map.TotalRegisters))
            {
                // Each register belongs to exactly one named region, and to at most one slot window.
                Assert.Equal(1, map.Regions.Count(r => register >= r.Range.Register && register < r.Range.End));
                Assert.True(map.Slots.Count(s => register >= s.Vector.Register && register < s.Vector.End) <= 1);
                Assert.True(map.Slots.Count(s => register >= s.Result.Register && register < s.Result.End) <= 1);
            }

            Assert.NotEmpty(windows);
        }
    }

    [Fact]
    public void A_map_whose_regions_overlap_CANNOT_BE_CONSTRUCTED()
    {
        // The check that makes the one above more than a statement about arithmetic that happens to be
        // right. Two slots aliased onto one register is agent A's write landing in agent B's mirror, and
        // it would not show up as an error anywhere downstream — so the type refuses to hold one.
        var good = MapAllocator.Allocate(Wave(new SlotRequest("S0", 2, 2), new SlotRequest("S1", 2, 2))).Require();

        var error = Assert.Throws<ArgumentException>(() => Rebuild(good, startEcho: good.StartBools));
        Assert.Contains("aliases regions", error.Message, StringComparison.Ordinal);
        Assert.Contains("start bools", error.Message, StringComparison.Ordinal);

        // And per-slot windows, which is where a real aliasing bug would live.
        var aliased = good.Slots.Select(s => s with { Result = good.Slots[0].Result }).ToArray();
        var slotError = Assert.Throws<ArgumentException>(() => Rebuild(good, slots: aliased));
        Assert.Contains("share registers", slotError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_slot_window_outside_its_own_block_is_refused_too()
    {
        var good = MapAllocator.Allocate(Wave(new SlotRequest("S0", 2, 2))).Require();
        var stray = good.Slots.Select(s => s with { Vector = new RegisterRange(good.ResultBlock.Register, 2) }).ToArray();

        var error = Assert.Throws<ArgumentException>(() => Rebuild(good, slots: stray));
        Assert.Contains("is not inside the vector block", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Rebuild a map through its real constructor with one region replaced.
    ///
    /// <para>Deliberately NOT a <c>with</c> expression: a record's copy constructor does not re-run the
    /// validation, so <c>with</c> would produce the aliased map without complaint and this test would be
    /// asserting nothing.</para>
    /// </summary>
    private static RegisterMap Rebuild(RegisterMap map, RegisterRange? startEcho = null, IReadOnlyList<SlotAllocation>? slots = null) =>
        new(map.Geometry, map.Version, map.ScanCounter, map.StartBools, startEcho ?? map.StartEcho,
            map.VectorBlock, map.ResultBlock, map.VectorRegistersPerSlot, map.ResultRegistersPerSlot, slots ?? map.Slots);

    [Fact]
    public void Slot_addresses_are_base_plus_index_times_slot_size()
    {
        var map = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", 4, 8),
            new SlotRequest("S1", 2, 3),
            new SlotRequest("S2", 1, 1))).Require();

        for (var i = 0; i < map.Slots.Count; i++)
        {
            Assert.Equal(map.VectorBlock.Register + (i * map.VectorRegistersPerSlot), map.Slots[i].Vector.Register);
            Assert.Equal(map.ResultBlock.Register + (i * map.ResultRegistersPerSlot), map.Slots[i].Result.Register);
            Assert.Equal(map.VectorRegistersPerSlot, map.Slots[i].Vector.Length);
            Assert.Equal(map.ResultRegistersPerSlot, map.Slots[i].Result.Length);
        }
    }

    [Fact]
    public void Vectors_are_contiguous_so_the_wire_tensor_is_one_run()
    {
        // D26a: the wire carries modbusTensor(i) = the i-th vector of EVERY slot, concatenated.
        // Interleaving vector and result per slot would break that into one write per slot.
        var map = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", 10, 4),
            new SlotRequest("S1", 10, 4),
            new SlotRequest("S2", 10, 4))).Require();

        Assert.Equal(map.VectorBlock, map.WireTensor);
        Assert.Equal(30, map.WireTensor.Length);
        Assert.Equal(map.Slots[0].Vector.Register, map.WireTensor.Register);
        Assert.Equal(map.Slots[2].Vector.End, map.WireTensor.End);
    }

    [Fact]
    public void No_result_read_straddles_two_slots()
    {
        var map = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", 1, 7),
            new SlotRequest("S1", 1, 5),
            new SlotRequest("S2", 1, 7))).Require();

        for (var i = 0; i < map.Slots.Count; i++)
        {
            var read = map.ResultRead(i);
            Assert.True(read.Length <= ModbusLimits.MaxReadRegisters);

            foreach (var other in map.Slots.Where(s => s.Index != i))
            {
                var overlaps = read.Register < other.Result.End && other.Result.Register < read.End;
                Assert.False(overlaps, $"slot {i}'s read {read} overlaps slot {other.Index}'s region {other.Result}.");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The measured premise: registers are free on the wire, round trips cost. F-1 SPLIT THIS PAIR:
    // vector width is still free, RESULT width is not, because it decides how many slots share a read.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Widening_the_VECTOR_region_does_not_change_what_a_poll_costs()
    {
        var narrow = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", 1, 4),
            new SlotRequest("S1", 1, 4))).Require();

        var wide = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", 120, 4),
            new SlotRequest("S1", 120, 4))).Require();

        Assert.Equal(narrow.PollRoundTrips, wide.PollRoundTrips);
        Assert.Equal(narrow.CommitTransactions, wide.CommitTransactions);
    }

    [Fact]
    public void Widening_the_RESULT_region_DOES_change_it_and_before_F1_this_test_asserted_the_opposite()
    {
        // *** THE CLAIM F-1 FALSIFIES, INVERTED RATHER THAN DELETED. *** Until 2026-08-13 a read covered
        // one slot whatever its width, so no width could touch the poll cost. Now R = floor(125 / Wr):
        // padding a slot is paid in round trips, and one wide slot in a fixed-size wave set collapses R
        // for every slot in it. Whether admission should therefore group by slot size is F-6, and this
        // test deliberately does not presume an answer — it only records that the cost is real.
        var sized = MapAllocator.Allocate(Wave(
            Enumerable.Range(0, 6).Select(i => new SlotRequest($"S{i}", 4, 20)).ToArray())).Require();

        var padded = MapAllocator.Allocate(Wave(
            Enumerable.Range(0, 6).Select(i => new SlotRequest($"S{i}", 4, 123)).ToArray())).Require();

        Assert.Equal(6, sized.SlotsPerRead);
        Assert.Equal(1, padded.SlotsPerRead);

        Assert.Equal(2, sized.PollRoundTrips);        // control + ONE read covering all six slots
        Assert.Equal(7, padded.PollRoundTrips);       // control + one read per slot
    }

    [Fact]
    public void One_wide_slot_collapses_R_for_the_WHOLE_wave_set()
    {
        // Slots are fixed-size, sized to the largest member, so this is not a property of the wide slot —
        // it is a property of every slot beside it. F-6's whole subject, recorded and not decided.
        var homogeneous = MapAllocator.Allocate(Wave(
            Enumerable.Range(0, 6).Select(i => new SlotRequest($"S{i}", 4, 20)).ToArray())).Require();

        var withOneWideSlot = MapAllocator.Allocate(Wave(
            Enumerable.Range(0, 5).Select(i => new SlotRequest($"S{i}", 4, 20))
                .Append(new SlotRequest("S5", 4, 123)).ToArray())).Require();

        Assert.Equal(6, homogeneous.SlotsPerRead);
        Assert.Equal(1, withOneWideSlot.SlotsPerRead);
        Assert.Equal(123, withOneWideSlot.ResultRegistersPerSlot);
    }

    [Fact]
    public void Adding_a_slot_costs_a_round_trip_only_when_it_crosses_a_read_boundary()
    {
        // The read term is a STEP, not a slope. A slot added inside an existing group rides along free.
        var slots = (int n, int result) => MapAllocator.Allocate(Wave(
            Enumerable.Range(0, n).Select(i => new SlotRequest($"S{i}", 4, result)).ToArray())).Require();

        // R = 1: every slot is its own read, so every slot costs one.
        Assert.Equal(3, slots(2, 123).PollRoundTrips);
        Assert.Equal(4, slots(3, 123).PollRoundTrips);

        // R = 5: the fifth slot is free, the sixth is not.
        Assert.Equal(2, slots(4, 25).PollRoundTrips);
        Assert.Equal(2, slots(5, 25).PollRoundTrips);
        Assert.Equal(3, slots(6, 25).PollRoundTrips);
    }

    [Fact]
    public void READS_PER_POLL_CYCLE_IS_THE_OBSERVABILITY_FLOORS_ONLY_INPUT_and_it_is_NOT_the_slot_count()
    {
        // 🔴 *** THE PROPERTY THAT EXISTS BECAUSE A HARDCODED `1` STOOD IN FOR IT. *** The observability
        // floor is `reads x RTT_p99 / scan`. A slot is re-read once per CYCLE, so what sets the floor is
        // what a CYCLE costs — never how many slots exist. The gate derived it from the map; the code
        // building the delivered result package passed a literal one read, so on any wave needing more
        // than one read the package's floor was too small BY THAT FACTOR, in the PERMISSIVE direction.
        //
        // A one-read wave set is unaffected, which is why it went unseen — so this test is deliberately
        // written on wave sets where the two numbers DIFFER, because the fixtures where they agree cannot
        // tell a correct derivation from a literal.
        var slots = (int n, int result) => MapAllocator.Allocate(Wave(
            Enumerable.Range(0, n).Select(i => new SlotRequest($"S{i}", 4, result)).ToArray())).Require();

        // R = 5 at 25 registers: SIX slots ride in TWO reads. Slot count 6, reads 2 — the numbers a
        // reader "correcting" the floor's caller would have transposed.
        var sixInTwo = slots(6, 25);
        Assert.Equal(6, sixInTwo.Slots.Count);
        Assert.Equal(2, sixInTwo.ReadsPerPollCycle);

        // R = 1 at 123 registers: three slots, three reads — the case where they DO coincide, which is
        // exactly why coincidence is not evidence.
        Assert.Equal(3, slots(3, 123).ReadsPerPollCycle);

        // The control read is the +1, and it is the ONLY difference between the two properties. Stated
        // as an identity so a future edit cannot quietly move the control read into the floor's input.
        foreach (var map in new[] { sixInTwo, slots(3, 123), slots(1, 4) })
        {
            Assert.Equal(map.ReadPlan(Enumerable.Range(0, map.Slots.Count)).Count, map.ReadsPerPollCycle);
            Assert.Equal(1 + map.ReadsPerPollCycle, map.PollRoundTrips);
        }
    }

    [Fact]
    public void A_vector_wider_than_one_FC16_is_allocated_not_refused()
    {
        // The premise this guards against: capping vector width at the FC16 limit. X-A makes the data
        // phase non-atomic BY DESIGN — nothing is running while it is written — so spanning transactions
        // is legal, and narrowing the slot to avoid it would trade a free register for a real constraint.
        var map = MapAllocator.Allocate(Wave(new SlotRequest("S0", 200, 4))).Require();

        Assert.Equal(200, map.VectorRegistersPerSlot);
        Assert.Equal(2, map.VectorWriteTransactions);
        Assert.Equal(1, map.CommitTransactions);
    }

    // ---------------------------------------------------------------------------------------------
    // Start bools
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Sixteen_slots_share_one_start_register_and_the_seventeenth_takes_a_second()
    {
        var sixteen = MapAllocator.Allocate(Wave(
            Enumerable.Range(0, 16).Select(i => new SlotRequest($"S{i}", 1, 1)).ToArray())).Require();
        Assert.Equal(1, sixteen.StartBools.Length);
        Assert.Equal(15, sixteen.Slots[15].StartBitInRegister);

        var seventeen = MapAllocator.Allocate(Wave(
            Enumerable.Range(0, 17).Select(i => new SlotRequest($"S{i}", 1, 1)).ToArray())).Require();
        Assert.Equal(2, seventeen.StartBools.Length);
        Assert.Equal(seventeen.StartBools.Register + 1, seventeen.Slots[16].StartBoolRegister);
        Assert.Equal(0, seventeen.Slots[16].StartBitInRegister);
    }

    [Fact]
    public void Slot_zeros_start_bit_lands_in_the_low_byte_of_its_register()
    {
        // [I] Inferred from big-endianness on both sides, and the counter-intuitive half: a Modbus
        // register travels high byte first, so bit 0 of its VALUE is the SECOND M byte, not the first.
        // Nothing measured has distinguished this from its mirror image yet - see MirrorGeometry.BitAddressOf
        // for the one-write experiment that would.
        var map = MapAllocator.Allocate(Wave(
            Enumerable.Range(0, 16).Select(i => new SlotRequest($"S{i}", 1, 1)).ToArray())).Require();

        var geometry = map.Geometry;
        var word = geometry.ByteAddressOf(map.StartBools.Register);

        Assert.Equal($"%M{word + 1}.0", geometry.BitAddressOf(map.StartBools.Register, 0));
        Assert.Equal($"%M{word + 1}.7", geometry.BitAddressOf(map.StartBools.Register, 7));
        Assert.Equal($"%M{word}.0", geometry.BitAddressOf(map.StartBools.Register, 8));
        Assert.Equal($"%M{word}.7", geometry.BitAddressOf(map.StartBools.Register, 15));
    }

    // ---------------------------------------------------------------------------------------------
    // Refusals
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void An_empty_wave_set_is_refused_rather_than_mapped()
    {
        var result = MapAllocator.Allocate(Wave());

        Assert.False(result.Allocated);
        Assert.Contains(result.Refusals, r => r.Contains("no slots", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_result_region_larger_than_one_FC03_read_is_refused_at_derivation_time()
    {
        var result = MapAllocator.Allocate(Wave(new SlotRequest("S0", 1, ModbusLimits.MaxReadRegisters + 1)));

        Assert.False(result.Allocated);
        Assert.Contains(result.Refusals, r => r.Contains("FC03", StringComparison.Ordinal));
    }

    [Fact]
    public void A_result_region_of_exactly_one_FC03_read_is_allowed()
    {
        var map = MapAllocator.Allocate(Wave(new SlotRequest("S0", 1, ModbusLimits.MaxReadRegisters))).Require();
        Assert.Equal(ModbusLimits.MaxReadRegisters, map.ResultRegistersPerSlot);
    }

    [Fact]
    public void A_slot_publishing_nothing_is_refused()
    {
        var result = MapAllocator.Allocate(Wave(new SlotRequest("S0", 4, 0)));

        Assert.False(result.Allocated);
        Assert.Contains(result.Refusals, r => r.Contains("publishes nothing", StringComparison.Ordinal));
    }

    [Fact]
    public void A_slot_writing_nothing_is_allowed_because_pure_observation_vectors_are_real()
    {
        var map = MapAllocator.Allocate(Wave(new SlotRequest("S0", 0, 4))).Require();
        Assert.Equal(0, map.VectorRegistersPerSlot);
        Assert.Equal(0, map.VectorWriteTransactions);
    }

    [Fact]
    public void Duplicate_slot_ids_are_refused()
    {
        var result = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", 1, 1),
            new SlotRequest("S0", 1, 1)));

        Assert.False(result.Allocated);
        Assert.Contains(result.Refusals, r => r.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_map_that_overflows_bit_memory_is_refused_with_the_overflow_stated()
    {
        // 4000 bytes in, 2096 registers remain. 40 slots x 60 result registers alone is 2400.
        var slots = Enumerable.Range(0, 40).Select(i => new SlotRequest($"S{i}", 10, 60)).ToArray();
        var result = MapAllocator.Allocate(Wave(slots));

        Assert.False(result.Allocated);
        Assert.Contains(result.Refusals, r => r.Contains("bit memory", StringComparison.Ordinal) && r.Contains("over by", StringComparison.Ordinal));
    }

    [Fact]
    public void A_mirror_inside_the_retentive_window_is_refused()
    {
        var request = new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 512, baseByte: 256, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 256) / 2),
            new[] { new SlotRequest("S0", 1, 1) });

        var result = MapAllocator.Allocate(request);

        Assert.False(result.Allocated);
        Assert.Contains(result.Refusals, r => r.Contains("retentive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void An_odd_mirror_base_is_refused_because_every_register_would_straddle()
    {
        var request = new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 0, baseByte: 4001, declaredRegisters: 1),
            new[] { new SlotRequest("S0", 1, 1) });

        Assert.False(MapAllocator.Allocate(request).Allocated);
    }

    [Fact]
    public void A_refused_derivation_yields_no_map_and_a_map_yields_no_refusals()
    {
        var good = MapAllocator.Allocate(Wave(new SlotRequest("S0", 1, 1)));
        Assert.True(good.Allocated);
        Assert.Empty(good.Refusals);

        var bad = MapAllocator.Allocate(Wave());
        Assert.Null(bad.Map);
        Assert.NotEmpty(bad.Refusals);
        Assert.Throws<InvalidOperationException>(() => bad.Require());
    }

    // ---------------------------------------------------------------------------------------------
    // Excision: the map must NOT be re-derived
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Excising_a_slot_changes_neither_the_addresses_nor_the_map_hash()
    {
        var map = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", 4, 4),
            new SlotRequest("S1", 4, 4),
            new SlotRequest("S2", 4, 4))).Require();

        var after = map.WithSlotExcised("S1");

        Assert.Equal(map.MapHash, after.MapHash);
        Assert.True(after.Slot("S1")!.Excised);
        for (var i = 0; i < map.Slots.Count; i++)
        {
            Assert.Equal(map.Slots[i].Vector, after.Slots[i].Vector);
            Assert.Equal(map.Slots[i].Result, after.Slots[i].Result);
            Assert.Equal(map.Slots[i].StartBoolRegister, after.Slots[i].StartBoolRegister);
        }
    }

    [Fact]
    public void Re_deriving_without_the_excised_slot_DOES_change_the_hash_which_is_why_it_must_not_happen()
    {
        var full = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", 4, 4),
            new SlotRequest("S1", 4, 4),
            new SlotRequest("S2", 4, 4))).Require();

        var rederived = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", 4, 4),
            new SlotRequest("S2", 4, 4))).Require();

        Assert.NotEqual(full.MapHash, rederived.MapHash);
    }

    [Fact]
    public void The_map_hash_is_stable_across_derivations_of_the_same_wave_set()
    {
        var a = MapAllocator.Allocate(Wave(new SlotRequest("S0", 4, 4), new SlotRequest("S1", 2, 9))).Require();
        var b = MapAllocator.Allocate(Wave(new SlotRequest("S0", 4, 4), new SlotRequest("S1", 2, 9))).Require();

        Assert.Equal(a.MapHash, b.MapHash);
    }

    [Fact]
    public void Reordering_the_same_slots_is_a_different_map()
    {
        var a = MapAllocator.Allocate(Wave(new SlotRequest("S0", 4, 4), new SlotRequest("S1", 4, 4))).Require();
        var b = MapAllocator.Allocate(Wave(new SlotRequest("S1", 4, 4), new SlotRequest("S0", 4, 4))).Require();

        Assert.NotEqual(a.MapHash, b.MapHash);
    }

    [Fact]
    public void Excising_a_slot_the_map_does_not_hold_throws_rather_than_no_opping()
    {
        var map = MapAllocator.Allocate(Wave(new SlotRequest("S0", 1, 1))).Require();
        Assert.Throws<ArgumentException>(() => map.WithSlotExcised("S9"));
    }
}
