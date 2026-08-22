using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// Build-plan item 2.4's client, and the properties that are not free: one transaction for the commit,
/// no addressable register outside the slot, and the version check riding on the poll.
/// </summary>
public class MirrorClientTests
{
    internal static readonly BuildStamp Stamp = new(0xA93F2C71);

    internal static RegisterMap Map(int vector = 2, int result = 2, params string[] extraSlots)
    {
        var slots = new List<SlotRequest> { new("S0", vector, result) };
        slots.AddRange(extraSlots.Select(id => new SlotRequest(id, vector, result)));

        return MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2), slots)).Require();
    }

    private static (MirrorClient Client, RecordingTransport Wire) Wired(RegisterMap? map = null)
    {
        map ??= Map();
        var wire = new RecordingTransport(map, Stamp);
        return (new MirrorClient(map, wire, Stamp), wire);
    }

    // ---------------------------------------------------------------------------------------------
    // The control region
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_control_read_is_one_transaction_carrying_version_scan_counter_and_start_bools()
    {
        var (client, wire) = Wired();

        var control = client.ReadControl();

        Assert.Single(wire.Log);
        Assert.Equal(Stamp.Value, control.Version);
        Assert.Equal(new ScanCount(unchecked((uint)wire.ScanCounter)), control.ScanCounter);
        Assert.False(control.StartRaised(0));
    }

    [Fact]
    public void A_version_the_client_did_not_expect_refuses_rather_than_reading_on()
    {
        // DB-6 rule 1: the layout version is checked before every transaction batch, not only at connect,
        // because a download can land mid-session. After one, every address this client holds is a guess.
        var (client, wire) = Wired();
        wire.SetVersion(0x11112222);

        var error = Assert.Throws<WireException>(() => client.ReadControl());

        Assert.Contains("11112222", error.Message, StringComparison.Ordinal);
        Assert.Contains("A93F2C71", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_client_holding_a_zero_expected_stamp_is_refused_at_construction()
    {
        var map = Map();
        Assert.Throws<WireException>(() => new MirrorClient(map, new RecordingTransport(map), default));
    }

    // ---------------------------------------------------------------------------------------------
    // Writing, committing, reading
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_vector_lands_in_the_slots_own_registers()
    {
        var (client, wire) = Wired();

        client.WriteVector(0, new ushort[] { 5, 10 });

        Assert.Equal(5, wire.GetVector(0, 0));
        Assert.Equal(10, wire.GetVector(0, 1));
    }

    [Fact]
    public void A_vector_wider_than_the_slot_is_refused_because_it_would_land_in_the_next_slots_region()
    {
        var (client, _) = Wired();

        var error = Assert.Throws<WireException>(() => client.WriteVector(0, new ushort[] { 1, 2, 3 }));

        Assert.Contains("next slot's region", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void There_is_no_way_to_address_a_register_outside_a_slot()
    {
        // DB-6 rule 3 asks for the write to be UNADDRESSABLE rather than rejected by a check that could
        // be skipped. This test states that as a property of the type: the only entry points take a slot
        // INDEX, and an index outside the map is refused.
        var (client, _) = Wired();

        Assert.Throws<WireException>(() => client.WriteVector(1, new ushort[] { 1 }));
        Assert.Throws<WireException>(() => client.ReadResults(1));
        Assert.Throws<WireException>(() => client.Commit(new[] { 1 }));

        Assert.DoesNotContain(typeof(MirrorClient).GetMethods(),
            m => m.Name.Contains("Register", StringComparison.Ordinal));
    }

    [Fact]
    public void The_commit_is_exactly_one_transaction_over_the_start_bool_registers()
    {
        // X-A: the data phase may span as many transactions as it likes because nothing is running while
        // it is written. The START is the commit, and a commit split across two writes is not a commit.
        //
        // SEVENTEEN slots, deliberately. At sixteen or fewer the start bools occupy ONE register, so a
        // commit written one register at a time is still one transaction and this test cannot fail —
        // measured: neutering the single Write() into a per-register loop left the whole suite green.
        var map = Map(extraSlots: Enumerable.Range(1, 16).Select(i => $"S{i}").ToArray());
        var wire = new RecordingTransport(map, Stamp);
        var client = new MirrorClient(map, wire, Stamp);

        Assert.Equal(2, map.StartBools.Length);

        client.Commit(new[] { 0, 2, 16 });

        var write = Assert.Single(wire.Log.Where(t => t.IsWrite));
        Assert.Equal(map.StartBools.Register, write.StartRegister);
        Assert.Equal(map.StartBools.Length, write.Count);
        Assert.Equal(new ushort[] { 0b101, 0b1 }, wire.StartBoolRegisters);
    }

    [Fact]
    public void A_slot_left_out_of_the_commit_simply_does_not_rise_which_is_D26as_null()
    {
        var map = Map(extraSlots: new[] { "S1" });
        var wire = new RecordingTransport(map, Stamp);
        var client = new MirrorClient(map, wire, Stamp);

        client.Commit(new[] { 1 });
        var control = client.ReadControl();

        Assert.False(control.StartRaised(0));
        Assert.True(control.StartRaised(1));
    }

    [Fact]
    public void A_result_read_is_one_transaction_and_never_straddles_two_slots()
    {
        var map = Map(extraSlots: new[] { "S1" });
        var wire = new RecordingTransport(map, Stamp);
        var client = new MirrorClient(map, wire, Stamp);
        wire.SetResult(1, 0, 42);

        var results = client.ReadResults(1);

        var read = Assert.Single(wire.Log.Where(t => !t.IsWrite));
        Assert.Equal(map.Slots[1].Result.Register, read.StartRegister);
        Assert.Equal(map.Slots[1].Result.Length, read.Count);
        Assert.Equal(42, results[0]);
    }

    [Fact]
    public void A_vector_wider_than_one_FC16_is_split_and_that_is_not_a_refusal()
    {
        // The data phase is non-atomic BY DESIGN: nothing is running while it is written, and the
        // one-transaction commit is what makes a torn data write a detectable non-event. Narrowing a slot
        // to fit FC16 would trade a free register for a constraint that buys nothing.
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2),
            new[] { new SlotRequest("S0", 200, 2) })).Require();

        var wire = new RecordingTransport(map, Stamp);
        var client = new MirrorClient(map, wire, Stamp);

        client.WriteVector(0, new ushort[200]);

        Assert.Equal(2, wire.Log.Count(t => t.IsWrite));
        Assert.All(wire.Log.Where(t => t.IsWrite), t => Assert.True(t.Count <= ModbusLimits.MaxWriteRegisters));
    }

    [Fact]
    public void Cost_is_counted_in_round_trips()
    {
        var (client, _) = Wired();

        client.ReadControl();
        client.WriteVector(0, new ushort[] { 1, 2 });
        client.ReadResults(0);

        Assert.Equal(3, client.RoundTrips);
    }
}
