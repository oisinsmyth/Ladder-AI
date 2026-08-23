using Harness.Batch;
using Harness.Loop;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>THE GENERATE STEP RUNS FOR <c>lanes[0]</c> ALONE, AND IT WAS THE ONLY STEP THAT PRINTED
/// ASSERTION COVERAGE.</b>
///
/// <para><b>The measured shape.</b> <c>BatchRunPlan</c> passes <c>--generate-only</c> for the generator
/// lane only — correctly, because the copy layer is merged and generating it N times would be N
/// conflicting emissions — while the Wave step is per-lane without it. There is no <c>harness gate</c>
/// step in the plan at all. So a two-lane batch printed the fraction once, for lane 0, to scrollback, and
/// never computed it for lane 1.</para>
///
/// <para>🔴 <b>THE FIX IS ON THE RUN PATH, NOT IN THIS PLAN, AND THAT IS AN ARGUMENT RATHER THAN A
/// PREFERENCE.</b> The Wave step is already per-lane and its <c>harness-run</c> now emits the figure to
/// its own console and its own <c>--out</c> artifact, so every lane gets it from the gate the wave
/// ACTUALLY ran. A separate <c>harness gate</c> step would be a SECOND computation of one measurement:
/// a second rendering to read, a second place for the enumeration and binding arguments to diverge from
/// what the wave gated on, a new <c>HarnessGateExe</c> option every caller must supply, and a figure that
/// could disagree with the submission that was admitted. None of that buys a number the wave does not
/// already produce.</para>
///
/// <para><b>What the run path cannot cover is stated as a notice instead.</b> A batch that dies before
/// its Wave steps — at Deploy, say — has printed coverage for the generator lane only, and the other
/// lanes' figures were never computed. That silence looks exactly like a pass, which is what
/// <see cref="BatchRunPlan.Notices"/> exists for.</para>
/// </summary>
public class AssertionCoverageAcrossLanesTests
{
    private const string Notice = "ASSERTION COVERAGE FOR EVERY LANE BUT THE GENERATOR LANE IS COMPUTED ONLY AT ITS WAVE STEP";

    private static BatchRunOptions Options() => new(
        ConverterExe: @"C:\bin\converter.exe",
        OpennessCliExe: @"C:\bin\openness-cli.exe",
        HarnessRunExe: @"C:\bin\harness-run.exe",
        LeasesDirectory: @"C:\ProgramData\Ladder-AI\leases",
        PortalProject: @"C:\projects\Rig\Rig.ap20",
        RigAddress: "10.10.10.10",
        Holder: "agent-a",
        HolderPid: 4242,
        StagingDirectory: @"C:\staging",
        MergedBindingPath: @"C:\staging\merged-binding.json",
        PortalEvidencePath: @"C:\staging\portal-status.json",
        DeviceAllowlistPath: @"C:\tools\download-probe.allowlist");

    private static Lane LaneNamed(string name) =>
        new(name, name + "-binding.json", name + "-submission.json", new[] { name + @"\ir" });

    private static BatchRunPlan PlanFor(params string[] lanes)
    {
        var batch = new BatchPlanResult(
            lanes.Length, lanes, Array.Empty<string>(), "{}", null,
            lanes.Select(n => n + @"\ir").ToArray());

        return BatchRunPlan.For(batch, lanes.Select(LaneNamed).ToArray(), Options());
    }

    /// <summary>
    /// <b>The fixture trap, closed first.</b> Everything below is meaningless if the plan does not
    /// actually have one Generate step and a Wave step per lane.
    /// </summary>
    [Fact]
    public void The_plan_generates_once_and_waves_per_lane()
    {
        var plan = PlanFor("vessel", "hopper");

        Assert.Single(plan.Steps.Where(s => s.Kind == BatchStepKind.Generate));
        Assert.Equal(2, plan.Steps.Count(s => s.Kind == BatchStepKind.Wave));
        Assert.Equal("vessel", Assert.Single(plan.Steps.Where(s => s.Kind == BatchStepKind.Generate)).Lane);
    }

    /// <summary>
    /// 🔴 <b>NO SECOND COMPUTATION OF ONE MEASUREMENT.</b> If a <c>harness gate</c> step is ever added it
    /// must be argued on its own terms, not slipped in — two renderings of one figure in one output stream
    /// is the defect this whole change is about, one level up.
    /// </summary>
    [Fact]
    public void AND_THE_PLAN_ADDS_NO_SECOND_GATE_STEP_TO_RECOMPUTE_IT()
    {
        var plan = PlanFor("vessel", "hopper");

        Assert.DoesNotContain(plan.Steps, s =>
            s.Arguments.Contains("check") || s.Executable.Contains("harness-gate", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 🔴 <b>THE RESIDUAL SILENCE ANNOUNCES ITSELF.</b> A batch that never reaches its waves has coverage
    /// for the generator lane and nothing for the rest — and an absent figure reads like a measured one.
    /// </summary>
    [Fact]
    public void A_MULTI_LANE_BATCH_SAYS_WHERE_THE_OTHER_LANES_FIGURES_COME_FROM()
    {
        Assert.Contains(PlanFor("vessel", "hopper").Notices, n => n.Contains(Notice, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>The control, and it is the reason this is conditional.</b> With one lane the Generate step IS
    /// that lane, so there is no gap and no notice — <i>"a caveat that fires on every package is a caveat
    /// nobody reads"</i> is this file's own rule, and a notice on every plan would earn exactly that.
    /// </summary>
    [Fact]
    public void AND_A_SINGLE_LANE_BATCH_RAISES_NO_SUCH_NOTICE()
    {
        Assert.DoesNotContain(PlanFor("vessel").Notices, n => n.Contains(Notice, StringComparison.Ordinal));
    }
}
