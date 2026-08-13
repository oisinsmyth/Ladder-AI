using Harness.Map;
using Harness.Skeleton;
using Harness.Wire;

namespace Harness.Skeleton.Tests;

/// <summary>
/// *** PHASE 3's EXIT CRITERION. ***
///
/// <para><b>"Two slots produce results indistinguishable from their solo runs, AND a deliberately
/// coupled pair is caught rather than silently tolerated."</b> The plan names the way this gate is
/// usually failed — proving non-interference on a pair too simple to interfere — so both halves are
/// here, and the green half is worth nothing without the red one.</para>
/// </summary>
public class TwoSlotTests
{
    private const int RampStep = 5;
    private const int RampLimit = 10;
    private const int PeakLevel = 7;
    private const int PeakTrip = 6;

    private static IReadOnlyList<SlotTensor> Pair(int rampVectors = 1, int peakVectors = 1) => new[]
    {
        new SlotTensor(SkeletonRig.RampSlot,
            Enumerable.Range(0, rampVectors).Select(_ => SkeletonRig.RampVector(RampStep, RampLimit)).ToArray()),
        new SlotTensor(SkeletonRig.PeakSlot,
            Enumerable.Range(0, peakVectors).Select(_ => SkeletonRig.PeakVector(PeakLevel, PeakTrip)).ToArray()),
    };

    // ---------------------------------------------------------------------------------------------
    // 3.1 — a second slot, a different block, running concurrently
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Both_blocks_run_in_one_wave_and_both_match_their_own_models()
    {
        var rig = SkeletonRig.BuildPair();

        var wave = rig.RunWave(Pair());

        var ramp = wave.For(SkeletonRig.RampSlot).Results.Single();
        var peak = wave.For(SkeletonRig.PeakSlot).Results.Single();

        Assert.Equal(SlotOutcome.Completed, ramp.Outcome);
        Assert.Equal(SlotOutcome.Completed, peak.Outcome);

        Assert.True(TrivialBlockModel.Judge(RampStep, RampLimit, ramp.Results).Held);
        Assert.True(PeakBlockModel.Judge(PeakLevel, PeakTrip, peak.Results).Held);
    }

    [Fact]
    public void The_two_slots_are_addressed_separately_and_publish_different_things()
    {
        // Two instances of ONE block would make an aliasing bug invisible: every register would hold the
        // same value at the same time. The blocks are different shapes precisely so this can be asserted.
        var rig = SkeletonRig.BuildPair();

        var wave = rig.RunWave(Pair());

        Assert.NotEqual(
            wave.For(SkeletonRig.RampSlot).Results.Single().Results,
            wave.For(SkeletonRig.PeakSlot).Results.Single().Results);
    }

    // ---------------------------------------------------------------------------------------------
    // 3.2 — non-interference, and the DETECTION of interference
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void GREEN_a_disjoint_pair_produces_results_indistinguishable_from_its_solo_runs()
    {
        var rig = SkeletonRig.BuildPair(PeakBlockCoupling.None);

        var report = NonInterference.Compare(Pair(2, 2), tensors => rig.RunWave(tensors));

        Assert.True(report.Indistinguishable, report.Summary());
        Assert.Equal(4, report.Comparisons);
    }

    [Fact]
    public void RED_a_pair_coupled_by_ONE_extra_rung_is_caught()
    {
        // *** HOW THE PAIR IS MADE TO GENUINELY INTERFERE. *** The peak block gains ONE network that
        // writes into the ramp block's accumulator — overlapping reachable state (D9), the first edge
        // DB-13's conflict graph is defined by, and the multi-writer fact cross-check reports as C-308.
        //
        // The rung is gated on the peak block's OWN start condition. That is what makes this a test of
        // CONCURRENCY rather than of a broken block: every block is called every scan (D37), so a
        // coupling that fired whenever the block executed would corrupt the ramp's SOLO run too — and a
        // differential cannot see a fault that is present in both of its arms.
        var rig = SkeletonRig.BuildPair(PeakBlockCoupling.WritesTheRampsAccumulator);

        var report = NonInterference.Compare(Pair(2, 2), tensors => rig.RunWave(tensors));

        Assert.False(report.Indistinguishable, report.Summary());

        // The RAMP is the slot that moved, and the peak block's own results are unaffected — which is
        // D21's story exactly: the victim's test fails, the victim's code is innocent, and without the
        // co-running log the victim would hunt a phantom.
        Assert.All(report.Findings, f => Assert.Equal(SkeletonRig.RampSlot, f.SlotIndex));

        // Alone the ramp stops at its limit of 10; alongside the peak block it is carried to 12 by the
        // one extra rung, and it still reports DONE — a plausible wrong answer, not an error.
        Assert.Equal(2, report.Findings.Count);
        Assert.All(report.Findings, f =>
        {
            Assert.Equal(new ushort[] { 10, 1 }, f.Solo);
            Assert.Equal(new ushort[] { 12, 1 }, f.Concurrent);
        });
    }

    [Fact]
    public void And_the_coupled_pair_is_CORRECT_when_each_slot_runs_alone()
    {
        // Without this, the red above could just as well mean "the coupled build is broken outright".
        // It is not: alone, both blocks agree with their models. The fault exists only when both run.
        var rig = SkeletonRig.BuildPair(PeakBlockCoupling.WritesTheRampsAccumulator);

        var rampSolo = rig.RunWave(new[] { Pair()[0] }).For(SkeletonRig.RampSlot).Results.Single();
        var peakSolo = rig.RunWave(new[] { Pair()[1] }).For(SkeletonRig.PeakSlot).Results.Single();

        Assert.True(TrivialBlockModel.Judge(RampStep, RampLimit, rampSolo.Results).Held);
        Assert.True(PeakBlockModel.Judge(PeakLevel, PeakTrip, peakSolo.Results).Held);
    }

    [Fact]
    public void The_coupled_and_disjoint_builds_differ_by_exactly_one_rung()
    {
        var disjoint = PeakBlock.Generate(3100, 902, PeakBlockCoupling.None)
            .Single(o => o.Kind == HarnessObjectKind.Block).Ir;
        var coupled = PeakBlock.Generate(3100, 902, PeakBlockCoupling.WritesTheRampsAccumulator)
            .Single(o => o.Kind == HarnessObjectKind.Block).Ir;

        var extra = coupled.Split('\n').Except(disjoint.Split('\n')).Where(l => l.Trim().Length > 0).ToArray();

        Assert.Contains(extra, l => l.Contains("=> Demo_Count", StringComparison.Ordinal));
        Assert.Contains(extra, l => l.Contains("Add the level into the ramp block's accumulator", StringComparison.Ordinal));
    }

    [Fact]
    public void The_model_alone_would_ALSO_have_caught_it_which_is_the_second_independent_check()
    {
        // The differential compares two runs of the same system; the model compares one run against the
        // specification. They are independent, and on this defect they agree — which is worth asserting,
        // because a coupling that only the differential could see would be one the model was blind to.
        var rig = SkeletonRig.BuildPair(PeakBlockCoupling.WritesTheRampsAccumulator);

        var wave = rig.RunWave(Pair());
        var ramp = wave.For(SkeletonRig.RampSlot).Results.Single();

        Assert.False(TrivialBlockModel.Judge(RampStep, RampLimit, ramp.Results).Held);
        Assert.True(PeakBlockModel.Judge(PeakLevel, PeakTrip, wave.For(SkeletonRig.PeakSlot).Results.Single().Results).Held);
    }

    // ---------------------------------------------------------------------------------------------
    // 3.3 — the co-running log, from EXECUTED start bools
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_log_records_what_RAN_and_it_agrees_with_what_was_commanded()
    {
        var rig = SkeletonRig.BuildPair();

        var wave = rig.RunWave(Pair());

        Assert.True(wave.Log.Agrees);
        var index = Assert.Single(wave.Log.Indices);
        Assert.Equal(new[] { 0, 1 }, index.Executed);
        Assert.Equal(new[] { 1 }, index.CoRunnersOf(SkeletonRig.RampSlot));
    }

    [Fact]
    public void The_echo_is_LATCHED_so_a_slot_that_finished_between_two_polls_still_reports_as_having_run()
    {
        // A poll gap is 8.6 scans at the p99 and the peak block settles in ONE. A level echo would read
        // low at both observations and the log would say the slot never ran — which is the false evidence
        // X-E exists to kill, pointing the wrong way.
        var rig = SkeletonRig.BuildPair(scansPerTransaction: 40);

        var wave = rig.RunWave(Pair());

        Assert.True(wave.Log.Agrees, string.Join("; ", wave.Log.Discrepancies));
        Assert.Contains(SkeletonRig.PeakSlot, wave.Log.Indices.Single().Executed);
    }

    [Fact]
    public void A_slot_that_is_null_at_an_index_does_not_appear_in_that_indexs_executed_set()
    {
        var rig = SkeletonRig.BuildPair();

        var wave = rig.RunWave(Pair(rampVectors: 2, peakVectors: 1));

        Assert.Equal(new[] { 0, 1 }, wave.Log.Indices[0].Executed);
        Assert.Equal(new[] { 0 }, wave.Log.Indices[1].Executed);
        Assert.Empty(wave.Log.Indices[1].CoRunnersOf(SkeletonRig.RampSlot));
    }

    // ---------------------------------------------------------------------------------------------
    // 3.4 — unequal tensor lengths, null, inert
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_wave_runs_to_the_MAX_tensor_length_and_the_short_slot_goes_quiet()
    {
        var rig = SkeletonRig.BuildPair();

        var wave = rig.RunWave(Pair(rampVectors: 3, peakVectors: 1));

        Assert.Equal(3, wave.Length);
        Assert.Equal(3, wave.For(SkeletonRig.RampSlot).Results.Count);
        Assert.Single(wave.For(SkeletonRig.PeakSlot).Results);
    }

    [Fact]
    public void A_null_slot_is_held_inert_and_its_results_stay_at_the_reset_state()
    {
        // D26a rule 2 needs NO new encoding: the slot's start bool is simply not raised, D33's inert
        // holds, and the values are don't-care. Asserted on the device side rather than assumed: after the
        // wave, the peak block's own outputs read as its reset state, not as a stale result.
        var rig = SkeletonRig.BuildPair();

        rig.RunWave(Pair(rampVectors: 3, peakVectors: 1));

        Assert.Equal(new ushort[] { 0, 0 }, rig.Client.ReadResults(SkeletonRig.PeakSlot));
    }

    // ---------------------------------------------------------------------------------------------
    // 3.5 — per-slot completion and exit
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_slots_results_are_distributed_when_ITS_tensor_ends_not_when_the_wave_does()
    {
        // D26a rule 3: feedback latency is bounded by a slot's OWN tensor length. A caller that only read
        // the returned WaveResult would have re-batched the feedback to wave end, which is the latency
        // the rule exists to remove — so the callback is what this asserts.
        var rig = SkeletonRig.BuildPair();
        var distributedAt = new List<(int Slot, int Index)>();

        var wave = rig.RunWave(Pair(rampVectors: 3, peakVectors: 1),
            d => distributedAt.Add((d.SlotIndex, d.CompletedAtIndex)));

        Assert.Equal(new[] { (SkeletonRig.PeakSlot, 0), (SkeletonRig.RampSlot, 2) }, distributedAt);
        Assert.Equal(0, wave.For(SkeletonRig.PeakSlot).CompletedAtIndex);
        Assert.Equal(2, wave.For(SkeletonRig.RampSlot).CompletedAtIndex);
    }

    [Fact]
    public void A_slots_distribution_carries_only_the_indices_it_actually_ran_at()
    {
        var rig = SkeletonRig.BuildPair();

        var wave = rig.RunWave(Pair(rampVectors: 3, peakVectors: 1));

        Assert.Equal(new[] { 0 }, wave.For(SkeletonRig.PeakSlot).CoRunning.Select(c => c.WaveIndex));
        Assert.Equal(new[] { 0, 1, 2 }, wave.For(SkeletonRig.RampSlot).CoRunning.Select(c => c.WaveIndex));
        Assert.Equal(new[] { SkeletonRig.PeakSlot }, wave.For(SkeletonRig.RampSlot).CoRunning[0].CoRunners);
        Assert.Empty(wave.For(SkeletonRig.RampSlot).CoRunning[2].CoRunners);
    }

    // ---------------------------------------------------------------------------------------------
    // 0.1b over the two-slot object set
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Every_object_the_two_slot_rig_generates_passes_the_non_retentive_assertion()
    {
        var rig = SkeletonRig.BuildPair();

        var verdict = RetentionCheck.Check(rig.HarnessObjects.Concat(rig.ProgramObjects), rig.Geometry);

        Assert.True(verdict.Passed, verdict.Summary());
        Assert.Equal(6, verdict.ObjectsExamined);
    }
}
