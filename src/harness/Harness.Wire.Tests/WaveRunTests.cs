using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// Build-plan items 3.3, 3.4 and 3.5 at the wire level — the co-running log, null slots, and per-slot
/// exit — driven by a register file rather than a program, so every refusal is reachable.
/// </summary>
public class WaveRunTests
{
    private static readonly BuildStamp Stamp = MirrorClientTests.Stamp;

    private static (MirrorClient Client, RecordingTransport Wire, RegisterMap Map) Wired(int slots = 2, int resultWidth = 2)
    {
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            Enumerable.Range(0, slots).Select(i => new SlotRequest($"S{i}", 2, resultWidth)).ToArray())).Require();

        var wire = new RecordingTransport(map, Stamp);

        // The "program": once a slot's start bool is raised, its completion register reads 1.
        wire.OnTransaction = t =>
        {
            for (var i = 0; i < slots; i++)
            {
                var register = i / RegisterMap.SlotsPerStartRegister;
                var mask = (ushort)(1 << (i % RegisterMap.SlotsPerStartRegister));
                var running = (t.StartBoolRegisters[register] & mask) != 0;

                t.SetResult(i, 0, running ? (ushort)(10 + i) : (ushort)0);
                t.SetResult(i, 1, running ? (ushort)1 : (ushort)0);
            }
        };

        return (new MirrorClient(map, wire, Stamp), wire, map);
    }

    private static WireVector Vector() => new(
        Values: new ushort[] { 1, 2 },
        Inert: new InertDeclaration(new Dictionary<int, ushort> { [0] = 0, [1] = 0 }),
        CompletionRegister: 1,
        CompletionValue: 1,
        DeclaredScans: 1);

    private static SlotTensor Tensor(int slot, int length) =>
        new(slot, Enumerable.Range(0, length).Select(_ => Vector()).ToArray());

    // ---------------------------------------------------------------------------------------------
    // 3.4 — unequal tensor lengths
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Wave_length_is_the_MAX_tensor_length_and_nothing_else_sets_it()
    {
        var (client, _, _) = Wired();

        var wave = WaveRun.Run(client, new[] { Tensor(0, 4), Tensor(1, 2) });

        Assert.Equal(4, wave.Length);
        Assert.Equal(4, wave.For(0).Results.Count);
        Assert.Equal(2, wave.For(1).Results.Count);
    }

    [Fact]
    public void A_null_slots_start_bool_is_simply_not_raised_which_is_the_whole_of_D26a_rule_2()
    {
        var (client, wire, map) = Wired();

        WaveRun.Run(client, new[] { Tensor(0, 2), Tensor(1, 1) });

        // At the last index only slot 0 was commanded. No separate encoding for the null slot exists.
        var commits = wire.Log.Where(t => t.IsWrite && t.StartRegister == map.StartBools.Register).ToArray();
        Assert.Equal((ushort)0b1, commits[^1].Values[0]);
    }

    // ---------------------------------------------------------------------------------------------
    // 3.5 — per-slot completion and exit
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_slot_is_distributed_the_moment_its_own_tensor_ends()
    {
        var (client, _, _) = Wired();
        var order = new List<int>();

        WaveRun.Run(client, new[] { Tensor(0, 3), Tensor(1, 1) }, onSlotComplete: d => order.Add(d.SlotIndex));

        Assert.Equal(new[] { 1, 0 }, order);
    }

    [Fact]
    public void A_completed_slot_is_never_the_REASON_for_a_read_though_it_may_ride_along_in_one()
    {
        // Restated under F-1. Before it, one slot was one read and "not polled again" was a statement
        // about transactions. Now the transaction is the unit: a finished slot inside a group that was
        // happening anyway costs nothing, and excluding it would cost a round trip to save registers —
        // which is the trade the whole design refuses. What must still hold is that a group is never
        // issued for a set of slots that are ALL finished.
        var (client, wire, map) = Wired(3);

        WaveRun.Run(client, new[] { Tensor(0, 3), Tensor(1, 1), Tensor(2, 1) });

        var reads = wire.Log
            .Where(t => !t.IsWrite && t.StartRegister >= map.ResultBlock.Register && t.StartRegister < map.ResultBlock.End)
            .Select(t => (First: (t.StartRegister - map.ResultBlock.Register) / map.ResultRegistersPerSlot,
                          Count: t.Count / map.ResultRegistersPerSlot))
            .ToArray();

        // Index 0 runs all three slots; indices 1 and 2 run slot 0 alone. So the later reads must start
        // at slot 0 and must not exist for a group containing only slots 1 and 2.
        Assert.DoesNotContain(reads, r => r.First > 0);
        Assert.Contains(reads, r => r.Count == 3);
        Assert.Contains(reads, r => r.Count == 1);
    }

    // ---------------------------------------------------------------------------------------------
    // F-1, end to end: counted in transactions that actually happened, not in arithmetic
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Six_slots_sized_to_their_contents_cost_far_fewer_round_trips_than_six_padded_ones()
    {
        // The same wave, the same slot count, the same vectors — only the RESULT width differs, and with
        // it R. This is the win measured at the wire rather than derived: reads happen three times per
        // index (two inert observations and one poll round), so R = 6 does in three what R = 1 does in
        // eighteen. Padding to the FC03 limit "because width is free" is now the expensive mistake.
        var sized = Wired(slots: 6, resultWidth: 20);
        var padded = Wired(slots: 6, resultWidth: 123);

        var tensors = Enumerable.Range(0, 6).Select(i => Tensor(i, 1)).ToArray();

        var sizedWave = WaveRun.Run(sized.Client, tensors);
        var paddedWave = WaveRun.Run(padded.Client, tensors);

        Assert.Equal(6, sized.Map.SlotsPerRead);
        Assert.Equal(1, padded.Map.SlotsPerRead);
        Assert.True(paddedWave.RoundTrips > sizedWave.RoundTrips + 10,
            $"padded {paddedWave.RoundTrips} round trips against sized {sizedWave.RoundTrips} — F-1 bought nothing.");
    }

    [Fact]
    public void The_WRITE_count_is_identical_between_the_two_because_F1_is_a_READ_side_change()
    {
        // Stated as a test because the tempting next step is to batch writes the same way, and that is
        // exactly where A1's coherence guarantee lives — measured at 16 registers per FC16 with one
        // request never split. F-1 says nothing about writes at all.
        var sized = Wired(slots: 6, resultWidth: 20);
        var padded = Wired(slots: 6, resultWidth: 123);
        var tensors = Enumerable.Range(0, 6).Select(i => Tensor(i, 1)).ToArray();

        WaveRun.Run(sized.Client, tensors);
        WaveRun.Run(padded.Client, tensors);

        Assert.Equal(
            sized.Wire.Log.Count(t => t.IsWrite),
            padded.Wire.Log.Count(t => t.IsWrite));
    }

    [Fact]
    public void A_group_read_is_trimmed_to_the_last_slot_it_covers()
    {
        // Extending to the full reach would cost the same one transaction and read registers nobody
        // asked for. Registers are ~0.040 ms each — negligible against a round trip and not zero — so
        // the run stops at the last wanted slot. A slot BETWEEN two wanted ones still rides along,
        // because excluding it would cost a whole transaction to save a register.
        var (_, _, map) = Wired(slots: 10, resultWidth: 12);   // R = 10

        Assert.Equal(new[] { new SlotSpan(0, 1) }, map.ReadPlan(new[] { 0 }));
        Assert.Equal(new[] { new SlotSpan(2, 4) }, map.ReadPlan(new[] { 2, 5 }));
        Assert.Equal(new[] { new SlotSpan(0, 10) }, map.ReadPlan(Enumerable.Range(0, 10)));
    }

    // ---------------------------------------------------------------------------------------------
    // 3.3 — the co-running log
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_log_is_built_from_the_echo_and_agrees_when_everything_ran()
    {
        var (client, _, _) = Wired();

        var wave = WaveRun.Run(client, new[] { Tensor(0, 2), Tensor(1, 2) });

        Assert.True(wave.Log.Agrees);
        Assert.Equal(2, wave.Log.Indices.Count);
        Assert.All(wave.Log.Indices, i => Assert.Equal(new[] { 0, 1 }, i.Executed));
    }

    [Fact]
    public void A_slot_that_was_commanded_and_never_ran_is_a_discrepancy_not_a_silent_pass()
    {
        // This is also the guard against the one inference left in the map. If the %M byte carrying a
        // register's bit is wrong, the commanded block never sees its start condition — and it presents
        // exactly here, named, rather than as a test that timed out for no stated reason.
        var (client, wire, _) = Wired();
        wire.SuppressEchoFor.Add(1);

        var wave = WaveRun.Run(client, new[] { Tensor(0, 1), Tensor(1, 1) });

        Assert.False(wave.Log.Agrees);
        Assert.Equal(CoRunningOutcome.CommandedButDidNotRun, wave.Log.Indices[0].Outcome);
        Assert.Contains(wave.Log.Discrepancies, d => d.Contains("were commanded and never ran", StringComparison.Ordinal));
    }

    [Fact]
    public void A_slot_that_ran_without_being_commanded_is_a_discrepancy_too()
    {
        var (client, wire, _) = Wired();
        wire.ForceEchoFor.Add(1);

        var wave = WaveRun.Run(client, new[] { Tensor(0, 1) });

        Assert.False(wave.Log.Agrees);
        Assert.Equal(CoRunningOutcome.RanButWasNotCommanded, wave.Log.Indices[0].Outcome);
        Assert.Contains(wave.Log.Discrepancies, d => d.Contains("without being commanded", StringComparison.Ordinal));
    }

    [Fact]
    public void The_slice_a_slot_carries_names_who_MEASURABLY_ran_alongside_it()
    {
        var (client, wire, _) = Wired(3);
        wire.SuppressEchoFor.Add(2);

        var wave = WaveRun.Run(client, new[] { Tensor(0, 1), Tensor(1, 1), Tensor(2, 1) });

        // Slot 2 was PLANNED alongside slots 0 and 1 and did not run. A log built from the plan would
        // name it; this one does not, which is the whole of X-E.
        Assert.Equal(new[] { 1 }, wave.For(0).CoRunning.Single().CoRunners);
    }

    [Fact]
    public void The_echo_latch_is_cleared_at_inert_so_a_stale_bit_is_not_read_as_this_indexs_evidence()
    {
        // The echo is a LATCH — it is never cleared by the program, only by the client, and D33 puts that
        // release inside the inert phase so it COMPLETES before the first scan of the test. Without it,
        // slot 1 having run at index 0 would still read as having run at index 1, and the co-running log
        // would name a slot that was null — the same false evidence as building it from the plan, one
        // index later.
        var (client, _, _) = Wired();

        var wave = WaveRun.Run(client, new[] { Tensor(0, 2), Tensor(1, 1) });

        Assert.Equal(new[] { 0, 1 }, wave.Log.Indices[0].Executed);
        Assert.Equal(new[] { 0 }, wave.Log.Indices[1].Executed);
        Assert.True(wave.Log.Agrees, string.Join("; ", wave.Log.Discrepancies));
    }

    [Fact]
    public void An_empty_log_does_not_agree()
    {
        Assert.False(new CoRunningLog().Agrees);
    }

    // ---------------------------------------------------------------------------------------------
    // Refusals
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_wave_over_no_slots_is_refused()
    {
        var (client, _, _) = Wired();

        Assert.Throws<ArgumentException>(() => WaveRun.Run(client, Array.Empty<SlotTensor>()));
    }

    [Fact]
    public void A_slot_submitting_an_empty_tensor_is_refused_rather_than_treated_as_null_everywhere()
    {
        // "Nothing to run" and "null at some index" are different claims. A slot with no vectors at all
        // should not be in the wave set, and silently accepting it would allocate a region nothing uses.
        var (client, _, _) = Wired();

        Assert.Throws<ArgumentException>(() =>
            WaveRun.Run(client, new[] { Tensor(0, 1), new SlotTensor(1, Array.Empty<WireVector>()) }));
    }

    [Fact]
    public void Two_tensors_naming_one_slot_are_refused()
    {
        var (client, _, _) = Wired();

        Assert.Throws<ArgumentException>(() => WaveRun.Run(client, new[] { Tensor(0, 1), Tensor(0, 1) }));
    }

    [Fact]
    public void An_inert_phase_with_no_active_slot_is_refused()
    {
        var (client, _, _) = Wired();

        Assert.Throws<ArgumentException>(() => InertPhase.Establish(client, Array.Empty<SlotInert>()));
    }

    [Fact]
    public void An_inert_phase_that_cannot_be_established_stops_the_wave_and_still_distributes()
    {
        // D34 alternates inert/test unconditionally, but an inert phase that cannot be ESTABLISHED is
        // O6's residual — a wave-BLOCKING condition — and continuing would run every later index from a
        // state nobody verified. Silence would read as "no results yet", so the slots are still reported.
        var (client, wire, _) = Wired();
        wire.ScansPerTransaction = 0;

        var wave = WaveRun.Run(client, new[] { Tensor(0, 3), Tensor(1, 3) });

        Assert.Equal(2, wave.Distributions.Count);
        Assert.All(wave.Distributions, d => Assert.Equal(SlotOutcome.NotInert, d.Results.Single().Outcome));
    }
}
