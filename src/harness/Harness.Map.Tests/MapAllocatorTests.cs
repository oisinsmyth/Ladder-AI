using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// The map/slot allocator, build-plan item 2.1.
///
/// Every refusal below is a design-time refusal, so every one of these tests runs with no device.
/// </summary>
public class MapAllocatorTests
{
    /// <summary>The rig's geometry with the mirror placed clear of a 256-byte retentive window.</summary>
    private static MirrorGeometry Rig(int retentiveBytes = 256, int baseByte = 4000) =>
        MirrorGeometry.ForCpu1214C(retentiveBytes, baseByte);

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

        // Widest slot in the wave set sets both widths, for every slot (X-A: slots are fixed-size).
        Assert.Equal(4, map.VectorRegistersPerSlot);
        Assert.Equal(8, map.ResultRegistersPerSlot);

        Assert.Equal(new RegisterRange(5, 8), map.VectorBlock);
        Assert.Equal(new RegisterRange(13, 16), map.ResultBlock);
        Assert.Equal(29, map.TotalRegisters);
    }

    [Fact]
    public void The_control_region_is_one_read_covering_version_scan_counter_and_start_bools()
    {
        // DB-6 wants the version register checked before EVERY transaction batch. It is free to check
        // only if it shares the poll the client was making anyway, which means one contiguous region.
        var map = MapAllocator.Allocate(Wave(new SlotRequest("S0", 4, 8))).Require();

        Assert.Equal(0, map.Control.Register);
        Assert.Equal(map.StartBools.End, map.Control.End);
        Assert.Equal(map.Version.Length + map.ScanCounter.Length + map.StartBools.Length, map.Control.Length);
    }

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
    // The measured premise: registers are free, round trips cost. This pair IS the premise.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Widening_every_slot_does_not_change_what_a_poll_costs()
    {
        var narrow = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", 1, 1),
            new SlotRequest("S1", 1, 1))).Require();

        var wide = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", 120, 125),
            new SlotRequest("S1", 120, 125))).Require();

        Assert.Equal(narrow.PollRoundTrips, wide.PollRoundTrips);
        Assert.Equal(narrow.CommitTransactions, wide.CommitTransactions);
    }

    [Fact]
    public void Adding_a_slot_costs_exactly_one_more_round_trip_per_poll()
    {
        var two = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", 4, 4),
            new SlotRequest("S1", 4, 4))).Require();

        var three = MapAllocator.Allocate(Wave(
            new SlotRequest("S0", 4, 4),
            new SlotRequest("S1", 4, 4),
            new SlotRequest("S2", 4, 4))).Require();

        Assert.Equal(3, two.PollRoundTrips);          // one control read plus one per slot
        Assert.Equal(two.PollRoundTrips + 1, three.PollRoundTrips);
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
            MirrorGeometry.ForCpu1214C(retentiveBytes: 512, baseByte: 256),
            new[] { new SlotRequest("S0", 1, 1) });

        var result = MapAllocator.Allocate(request);

        Assert.False(result.Allocated);
        Assert.Contains(result.Refusals, r => r.Contains("retentive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void An_odd_mirror_base_is_refused_because_every_register_would_straddle()
    {
        var request = new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 0, baseByte: 4001),
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
