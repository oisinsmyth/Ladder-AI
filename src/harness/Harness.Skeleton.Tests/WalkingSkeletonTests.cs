using Harness.Map;
using Harness.Skeleton;
using Harness.Wire;

namespace Harness.Skeleton.Tests;

/// <summary>
/// *** PHASE 2's EXIT CRITERION. ***
///
/// <para><b>"A deliberately introduced bug in the block under test must produce a RED result, and the
/// corrected block must produce a GREEN one."</b> Not "the harness runs" and not "a test passes" — a
/// test that cannot fail has proven nothing, and a green suite that never demonstrated it can go red is
/// the most expensive illusion available here.</para>
///
/// <para><b>WHAT IS AND IS NOT SHOWN HERE.</b> Every component in this loop is the real one — the map,
/// the copy layer's IR, the block under test's IR, the client sequence, the model — except the CPU,
/// which is a simulator that EXECUTES THE GENERATED IR TEXT. So the defect lives where it will live on
/// the device: one operator in one rung, which converts to a different SimaticML Part. <b>What remains
/// to be shown on the rig is that the same two builds compile, download, and produce the same two
/// results there</b> — nothing here is evidence about TIA or about a 1214C.</para>
/// </summary>
public class WalkingSkeletonTests
{
    // The discriminating vector. With step 5 and limit 10 the correct block stops AT the limit and the
    // defective one takes one step past it.
    private const int DiscriminatingStep = 5;
    private const int DiscriminatingLimit = 10;

    // The blind vector. With step 3 the count never lands on the limit, so BOTH builds ramp 0,3,6,9,12
    // and the defect is invisible. It is here because a demonstration that reddens everything proves
    // nothing about the harness either.
    private const int BlindStep = 3;
    private const int BlindLimit = 10;

    [Fact]
    public void GREEN_the_corrected_block_matches_the_model()
    {
        var rig = SkeletonRig.Build(TrivialBlockDefect.None);

        var (run, verdict) = rig.RunAndJudge(DiscriminatingStep, DiscriminatingLimit);

        Assert.Equal(SlotOutcome.Completed, run.Outcome);
        Assert.True(verdict.Held, verdict.Detail);
        Assert.Equal(10, verdict.ObservedCount);
    }

    [Fact]
    public void RED_the_same_loop_with_one_operator_changed_in_the_IR_does_not()
    {
        var rig = SkeletonRig.Build(TrivialBlockDefect.OffByOneAtTheLimit);

        var (run, verdict) = rig.RunAndJudge(DiscriminatingStep, DiscriminatingLimit);

        // The test still COMPLETES — the block does something, and it is confident about it. What fails
        // is the comparison with the model, which is the only place a wrong answer can be caught.
        Assert.Equal(SlotOutcome.Completed, run.Outcome);
        Assert.False(verdict.Held, "the defective block produced the model's answer, so this demonstration proves nothing.");
        Assert.Equal(15, verdict.ObservedCount);
        Assert.Equal(10, verdict.Predicted.Count);
        Assert.Contains("count is 15, the model predicts 10", verdict.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_two_builds_differ_by_exactly_one_operator_in_one_rung()
    {
        // If the two builds differed in more than this, the red above could be attributed to anything.
        var clean = TrivialBlock.Generate(3000, 901, TrivialBlockDefect.None).Single(o => o.Kind == HarnessObjectKind.Block).Ir;
        var buggy = TrivialBlock.Generate(3000, 901, TrivialBlockDefect.OffByOneAtTheLimit).Single(o => o.Kind == HarnessObjectKind.Block).Ir;

        var cleanLines = clean.Split('\n');
        var buggyLines = buggy.Split('\n');

        Assert.Equal(cleanLines.Length, buggyLines.Length);

        var differing = cleanLines.Zip(buggyLines).Where(p => p.First != p.Second).ToArray();
        Assert.Single(differing);
        Assert.Equal(differing[0].First.Replace("Demo_Count < Demo_Limit", "Demo_Count <= Demo_Limit"), differing[0].Second);
    }

    [Fact]
    public void The_defect_is_invisible_under_a_vector_that_never_lands_on_the_limit()
    {
        // The red above came from the ARTIFACT AND THE VECTOR TOGETHER. This is the other half of that
        // claim: the same defective build, a different vector, and the harness agrees with the model.
        var clean = SkeletonRig.Build(TrivialBlockDefect.None).RunAndJudge(BlindStep, BlindLimit);
        var buggy = SkeletonRig.Build(TrivialBlockDefect.OffByOneAtTheLimit).RunAndJudge(BlindStep, BlindLimit);

        Assert.True(clean.Verdict.Held, clean.Verdict.Detail);
        Assert.True(buggy.Verdict.Held, buggy.Verdict.Detail);
        Assert.Equal(12, buggy.Verdict.ObservedCount);
    }

    // ---------------------------------------------------------------------------------------------
    // The sequence the loop actually performed
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Inert_is_established_and_verified_before_the_start_bool_ever_rises()
    {
        var rig = SkeletonRig.Build();

        var (run, _) = rig.RunAndJudge(DiscriminatingStep, DiscriminatingLimit);

        Assert.True(run.Inert.Established, run.Inert.Detail);
        Assert.Equal(new ushort[] { 0, 0 }, run.Inert.FirstObservation);
        Assert.Equal(new ushort[] { 0, 0 }, run.Inert.SecondObservation);
        Assert.True(run.StartScan > run.Inert.ScanAtVerify);
    }

    [Fact]
    public void The_observation_happens_after_the_block_has_settled_and_that_is_the_sampling_floor_not_luck()
    {
        // §12a derivation 1: a poller's fastest SUSTAINED observation is 3.3 scans, and the block settles
        // in 2. So the first poll after T=0 necessarily sees a settled value. Stated as an assertion
        // because it is a premise of every result this loop produces — if a future change made the poll
        // faster than the block, results could be read mid-ramp and the failure would be silent.
        var rig = SkeletonRig.Build();

        var (run, _) = rig.RunAndJudge(DiscriminatingStep, DiscriminatingLimit);

        var predicted = TrivialBlockModel.Predict(DiscriminatingStep, DiscriminatingLimit);
        Assert.True(run.ElapsedScans > predicted.Scans,
            $"the first observation was {run.ElapsedScans} scan(s) after T=0 and the block needs {predicted.Scans}.");
    }

    [Fact]
    public void The_version_register_confirms_the_build_that_is_running()
    {
        var rig = SkeletonRig.Build();

        var report = VersionCheck.Confirm(rig.Client, rig.Stamp);

        Assert.True(report.Confirmed, report.Detail);
    }

    [Fact]
    public void A_client_holding_the_other_builds_stamp_refuses_the_mirror_entirely()
    {
        // The two builds are different downloads, so their stamps differ. A client carrying the wrong one
        // must not read the mirror at all: after a download its addresses are a guess (DB-6).
        var clean = SkeletonRig.Build(TrivialBlockDefect.None);
        var buggy = SkeletonRig.Build(TrivialBlockDefect.OffByOneAtTheLimit);

        Assert.NotEqual(clean.Stamp.Value, buggy.Stamp.Value);

        var mismatched = new MirrorClient(buggy.Map, buggy.Transport, clean.Stamp);
        Assert.Throws<WireException>(() => mismatched.ReadControl());
    }

    [Fact]
    public void A_CPU_that_stops_mid_wave_ends_the_run_rather_than_reporting_a_result()
    {
        // Stopped AFTER the version register has been published, which is the case that is hard: the
        // mirror still reads, the build stamp still confirms, and the only thing that gives it away is a
        // scan counter that does not move.
        var rig = SkeletonRig.Build();
        Assert.True(VersionCheck.Confirm(rig.Client, rig.Stamp).Confirmed);
        rig.Plc.Running = false;

        var run = SlotRun.Run(rig.Client, RuntimeCompression.Uncompressed, 0, SkeletonRig.RampVector(DiscriminatingStep, DiscriminatingLimit));

        Assert.Equal(SlotOutcome.NotInert, run.Outcome);
        Assert.Equal(InertOutcome.ScanCounterStalled, run.Inert.Outcome);
    }

    [Fact]
    public void A_CPU_that_never_started_reads_as_an_ABSENT_version_rather_than_a_wrong_answer()
    {
        var rig = SkeletonRig.Build();
        rig.Plc.Running = false;

        var report = VersionCheck.Confirm(rig.Client, rig.Stamp);

        Assert.Equal(VersionOutcome.Absent, report.Outcome);

        // And the guarded read refuses outright, so no part of the sequence can proceed on a mirror that
        // nothing is maintaining.
        Assert.Throws<WireException>(() => rig.Client.ReadControl());
    }

    [Fact]
    public void A_block_that_never_completes_TIMES_OUT_which_is_not_FAILED()
    {
        // X-B invented TIMED-OUT precisely so "the condition never occurred" would be distinguishable
        // from a real failure. Here the limit is far enough away that the backstop elapses first.
        var rig = SkeletonRig.Build();
        var vector = SkeletonRig.RampVector(1, 30000) with { Duration = new ScanBudget(1, 1) };

        var elapsed = 0L;
        var run = SlotRun.Run(rig.Client, RuntimeCompression.Uncompressed, 0, vector, () => elapsed += 500);

        Assert.Equal(SlotOutcome.TimedOut, run.Outcome);
        Assert.Contains("TIMED-OUT is not FAILED", run.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 0.1b over the objects this loop actually generated
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Every_object_this_rig_generates_passes_the_non_retentive_assertion()
    {
        var rig = SkeletonRig.Build();

        var verdict = RetentionCheck.Check(rig.HarnessObjects.Concat(rig.ProgramObjects), rig.Geometry);

        Assert.True(verdict.Passed, verdict.Summary());
        Assert.Equal(4, verdict.ObjectsExamined);
        Assert.Equal(13, verdict.AddressesExamined);
    }

    [Fact]
    public void The_program_under_tests_own_tags_are_subject_to_the_same_address_rule_as_the_mirror()
    {
        // The block under test consumes bit memory too, and retentive M starts at MB0 whoever wrote the
        // tag. Placed inside the window it is refused for exactly the reason the mirror would be.
        var inside = TrivialBlock.Generate(baseByte: 100, blockNumber: 901);

        var verdict = RetentionCheck.Check(inside, MirrorGeometry.ForCpu1214C(256, 4000));

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, f => f.Detail.Contains("retentive M window", StringComparison.Ordinal));
    }
}
