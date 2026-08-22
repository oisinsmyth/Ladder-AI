using Harness.Batch;
using Harness.Device;
using Harness.Loop;
using Harness.Map;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>WHAT THESE TESTS PROVE, AND WHAT THEY DO NOT.</b>
///
/// <para>They substitute the process runner. So they prove the ORDERING — gates before Portal, abort
/// before the download, teardown whatever happened, which failures stop the batch and which stop only a
/// lane — and they prove the argument vectors are the ones a rig session would type. <b>They do not
/// prove any of it runs.</b></para>
///
/// <para>That distinction is not pedantry here. <c>Harness.Device</c> carries the same seam and its own
/// caveat says no step of it has ever been executed against Portal or a controller, and this repository
/// has twice had a component's tests mistaken for evidence it worked. Stated at the top of the file so a
/// reader meets it before the green.</para>
/// </summary>
public class BatchRunTests
{
    private const string Merged = @"C:\staging\merged-binding.json";

    private static BatchRunOptions Options() => new(
        ConverterExe: @"C:\bin\converter.exe",
        HarnessRunExe: @"C:\bin\harness-run.exe",
        LeasesDirectory: @"C:\ProgramData\Ladder-AI\leases",
        PortalProject: @"C:\projects\Rig\Rig.ap20",
        RigAddress: "10.10.10.10",
        Holder: "agent-a",
        HolderPid: 4242,
        StagingDirectory: @"C:\staging",
        MergedBindingPath: Merged,
        PortalEvidencePath: @"C:\staging\portal-status.json");

    private static Lane LaneNamed(string name) =>
        new(name, name + "-binding.json", name + "-submission.json", new[] { name + @"\ir" });

    /// <summary>A batch that planned, without going near BatchPlanner's own inputs.</summary>
    private static BatchPlanResult Batch(params string[] laneNames) =>
        new(laneNames.Length, laneNames, Array.Empty<string>(), "{}", null, laneNames.Select(n => n + @"\ir").ToArray());

    private static BatchRunPlan PlanFor(params string[] laneNames) =>
        BatchRunPlan.For(Batch(laneNames), laneNames.Select(LaneNamed).ToArray(), Options());

    /// <summary>Records every invocation and answers each with a scripted exit code.</summary>
    private sealed class ScriptedRunner : IProcessRunner
    {
        private readonly Func<string, IReadOnlyList<string>, int> _exitCode;

        internal ScriptedRunner(Func<string, IReadOnlyList<string>, int> exitCode) => _exitCode = exitCode;

        internal List<(string Exe, IReadOnlyList<string> Args)> Calls { get; } = new();

        public ProcessResult Run(string executable, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            Calls.Add((executable, arguments));
            return new ProcessResult(true, false, _exitCode(executable, arguments), string.Empty, string.Empty, string.Empty);
        }
    }

    private static ScriptedRunner AllOk() => new((_, _) => 0);

    private static Func<DeploymentOutcome> Deploys(bool loaded) =>
        () => new DeploymentOutcome(true, loaded, new HashSet<string>(), loaded ? "loaded" : "the download did not transfer");

    // ---------------------------------------------------------------------------------------------
    // The plan.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>Portal is leased FIRST.</b> Taking the rig first would mean holding it while discovering a
    /// person is in the project — locking another agent out of the rig for a run that was never going to
    /// happen.
    /// </summary>
    [Fact]
    public void The_PORTAL_gate_is_taken_before_the_rig()
    {
        var plan = PlanFor("valve", "vessel");

        var acquires = plan.Steps.Where(s => s.Kind == BatchStepKind.LeaseAcquire).ToList();
        Assert.Equal(2, acquires.Count);
        Assert.Contains(acquires[0].Arguments, a => a.StartsWith("portal:", StringComparison.Ordinal));
        Assert.Contains("rig:10.10.10.10", acquires[1].Arguments);
    }

    /// <summary>Both gates come before anything that writes, and both are released after everything.</summary>
    [Fact]
    public void Nothing_touches_Portal_before_both_gates_and_both_are_released_last()
    {
        var plan = PlanFor("valve");

        var kinds = plan.Steps.Select(s => s.Kind).ToList();
        Assert.Equal(BatchStepKind.LeaseAcquire, kinds[0]);
        Assert.Equal(BatchStepKind.LeaseAcquire, kinds[1]);
        Assert.Equal(BatchStepKind.LeaseRelease, kinds[^1]);
        Assert.Equal(BatchStepKind.LeaseRelease, kinds[^2]);

        Assert.True(kinds.IndexOf(BatchStepKind.Generate) > 1);
        Assert.True(kinds.IndexOf(BatchStepKind.Deploy) > kinds.IndexOf(BatchStepKind.Generate));
    }

    /// <summary>
    /// Only the Portal lease carries evidence. Nothing can detect a rig in use — <c>MB_SERVER</c>'s one
    /// connection is discovered by failure — so passing it there would imply an observation nobody made.
    /// </summary>
    [Fact]
    public void Only_the_PORTAL_acquire_passes_evidence()
    {
        var plan = PlanFor("valve");
        var acquires = plan.Steps.Where(s => s.Kind == BatchStepKind.LeaseAcquire).ToList();

        Assert.Contains("--portal-evidence", acquires[0].Arguments);
        Assert.DoesNotContain("--portal-evidence", acquires[1].Arguments);
    }

    /// <summary>
    /// Every lane's wave runs against the MERGED binding, never its own. That is what makes it one
    /// deployment — a lane pointed at its own binding would expect a map that is not on the device.
    /// </summary>
    [Fact]
    public void Every_lane_runs_against_the_MERGED_binding_and_its_OWN_submission()
    {
        var plan = PlanFor("valve", "vessel");
        var waves = plan.Steps.Where(s => s.Kind == BatchStepKind.Wave).ToList();

        Assert.Equal(2, waves.Count);
        Assert.All(waves, w =>
        {
            var args = w.Arguments.ToList();
            Assert.Equal(Merged, args[args.IndexOf("--binding") + 1]);
            Assert.Equal(w.Lane + "-submission.json", args[args.IndexOf("--submission") + 1]);
        });
    }

    /// <summary>
    /// 🔴 <b>EVERY step gets the UNION of the lanes' programs — generation AND every wave.</b>
    ///
    /// <para>The build stamp is computed over map + bindings + naming + PROGRAM UNDER TEST, and it means
    /// "what is executing". A batch deploys every lane's blocks, so the deployed stamp covers all of
    /// them. A lane verifying against only its OWN program computes a different stamp, the version check
    /// fails, and the package comes back <c>Stale</c> — whose text says the download "never reached it",
    /// sending a reader to re-download a device that is already correct.</para>
    ///
    /// <para>Being batched must not change a lane's verdict. This is what that costs, and it is not
    /// visible from any single lane's run.</para>
    /// </summary>
    [Fact]
    public void Generation_AND_every_wave_get_the_UNION_of_the_lanes_programs()
    {
        var plan = PlanFor("valve", "vessel");

        var expected = new[] { @"valve\ir", @"vessel\ir" };

        foreach (var step in plan.Steps.Where(s => s.Kind is BatchStepKind.Generate or BatchStepKind.Wave))
        {
            var programs = step.Arguments
                .Select((a, i) => (a, i))
                .Where(x => x.a == "--program")
                .Select(x => step.Arguments[x.i + 1])
                .ToArray();

            Assert.Equal(expected, programs);
        }
    }

    /// <summary>
    /// Each path takes its own <c>--program</c>. <c>harness-run</c>'s parser consumes tokens until the
    /// next flag, so two paths under one flag would work until one contained a space — which every real
    /// project path on this machine does.
    /// </summary>
    [Fact]
    public void Each_program_path_gets_its_own_flag()
    {
        var plan = PlanFor("valve", "vessel");
        var wave = plan.Steps.First(s => s.Kind == BatchStepKind.Wave);

        Assert.Equal(2, wave.Arguments.Count(a => a == "--program"));
    }

    [Fact]
    public void A_plan_missing_a_gate_argument_is_refused_before_any_step_exists()
    {
        var plan = BatchRunPlan.For(Batch("valve"), new[] { LaneNamed("valve") }, Options() with { Holder = "" });

        Assert.False(plan.Planned);
        Assert.Empty(plan.Steps);
        Assert.Contains(plan.Refusals, r => r.Contains("--holder"));
    }

    /// <summary>
    /// The pid is required and must not be this process. A lease held by a pid that exits immediately
    /// makes every later reclaim decision fall back to the TTL alone while still looking evidence-based.
    /// </summary>
    [Fact]
    public void A_plan_with_no_holder_pid_is_refused()
    {
        var plan = BatchRunPlan.For(Batch("valve"), new[] { LaneNamed("valve") }, Options() with { HolderPid = 0 });

        Assert.False(plan.Planned);
        Assert.Contains(plan.Refusals, r => r.Contains("--holder-pid"));
    }

    [Fact]
    public void A_run_over_NO_lanes_is_refused_rather_than_taking_the_gate_for_nothing()
    {
        var plan = BatchRunPlan.For(Batch(), Array.Empty<Lane>(), Options());

        Assert.False(plan.Planned);
        Assert.Contains(plan.Refusals, r => r.Contains("NOTHING TO RUN"));
    }

    // ---------------------------------------------------------------------------------------------
    // Execution.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_clean_run_runs_every_lane_and_hands_both_gates_back()
    {
        var runner = AllOk();

        var result = BatchRunner.Execute(PlanFor("valve", "vessel"), runner, Deploys(loaded: true));

        Assert.Equal(BatchRunOutcome.Ran, result.Outcome);
        Assert.Equal(new[] { "valve", "vessel" }, result.LanesRun);
        Assert.Empty(result.LanesNotRun);
        Assert.True(result.LeasesReleased);
        Assert.Contains("2 of 2 lane(s) were run", result.Headline);
    }

    /// <summary>
    /// 🔴 <b>A refused Portal gate stops everything, and nothing is deployed.</b> This is the case the
    /// gate exists for: a person has the project open.
    /// </summary>
    [Fact]
    public void A_REFUSED_gate_deploys_nothing_and_runs_no_lane()
    {
        var deployed = false;
        var runner = new ScriptedRunner((_, args) => args.Contains("portal:" + Options().PortalProject) ? 1 : 0);

        var result = BatchRunner.Execute(PlanFor("valve", "vessel"), runner,
            () => { deployed = true; return new DeploymentOutcome(true, true, new HashSet<string>(), ""); });

        Assert.Equal(BatchRunOutcome.GateRefused, result.Outcome);
        Assert.False(deployed);
        Assert.Empty(result.LanesRun);
        Assert.Equal(2, result.LanesNotRun.Count);
        Assert.Contains("0 of 2 lane(s) were run", result.Headline);
    }

    /// <summary>
    /// And the gate it DID take is still handed back. A Portal lease left held because the rig lease was
    /// refused would block every other agent until its TTL.
    /// </summary>
    [Fact]
    public void A_gate_taken_before_a_refusal_is_still_RELEASED()
    {
        var runner = new ScriptedRunner((_, args) => args.Contains("rig:10.10.10.10") && args.Contains("acquire") ? 1 : 0);

        var result = BatchRunner.Execute(PlanFor("valve"), runner, Deploys(loaded: true));

        Assert.Equal(BatchRunOutcome.GateRefused, result.Outcome);
        Assert.True(result.LeasesReleased);

        // Exactly the one that was taken, and not the one that was refused.
        var releases = runner.Calls.Where(c => c.Args.Contains("release")).ToList();
        Assert.Single(releases);
        Assert.Contains("portal:" + Options().PortalProject, releases[0].Args);
    }

    /// <summary>A deployment that did not load runs no lane — there is nothing on the device to test.</summary>
    [Fact]
    public void A_deployment_that_did_not_load_runs_NO_lane()
    {
        var result = BatchRunner.Execute(PlanFor("valve", "vessel"), AllOk(), Deploys(loaded: false));

        Assert.Equal(BatchRunOutcome.NotDeployed, result.Outcome);
        Assert.Empty(result.LanesRun);
        Assert.Contains("NO LANE WAS RUN", result.Headline);
        Assert.True(result.LeasesReleased);
    }

    /// <summary>
    /// 🔴 <b>A lane whose wave fails does NOT stop the others.</b> The lanes are independent experiments
    /// sharing a deployment; abandoning the rest would throw away the evidence they were queued to
    /// produce. A lane that failed has still RUN.
    /// </summary>
    [Fact]
    public void ONE_lane_failing_does_not_stop_the_others()
    {
        var runner = new ScriptedRunner((_, args) => args.Contains("valve-submission.json") && args.Contains("--verify") ? 1 : 0);

        var result = BatchRunner.Execute(PlanFor("valve", "vessel"), runner, Deploys(loaded: true));

        Assert.Equal(BatchRunOutcome.Ran, result.Outcome);
        Assert.Equal(new[] { "valve", "vessel" }, result.LanesRun);
        Assert.Empty(result.LanesNotRun);
    }

    /// <summary>
    /// <b>RAN IS NOT PASSED.</b> This component attempts waves; it does not read verdicts. A reader who
    /// took "THE BATCH RAN" for "every lane passed" would be believing something nothing here checked.
    /// </summary>
    [Fact]
    public void The_headline_says_outright_that_a_wave_which_ran_is_not_a_wave_which_passed()
    {
        var result = BatchRunner.Execute(PlanFor("valve"), AllOk(), Deploys(loaded: true));

        Assert.Contains("a wave that ran is not a wave that passed", result.Headline);
    }

    /// <summary>
    /// A release that did not happen is REPORTED, loudly. The failure mode of a silent one is another
    /// agent blocked on a gate nobody is holding any more.
    /// </summary>
    [Fact]
    public void A_release_that_FAILS_is_reported_on_the_headline()
    {
        var runner = new ScriptedRunner((_, args) => args.Contains("release") ? 1 : 0);

        var result = BatchRunner.Execute(PlanFor("valve"), runner, Deploys(loaded: true));

        Assert.False(result.LeasesReleased);
        Assert.Contains("A GATE WAS NOT RELEASED", result.Headline);
    }

    /// <summary>
    /// A timed-out step is never Ok — a Portal command that expired may still be holding the project,
    /// and the run cannot tell from here which happened.
    /// </summary>
    [Fact]
    public void A_TIMED_OUT_step_is_NotProven_rather_than_a_pass()
    {
        var runner = new TimeoutRunner();

        var result = BatchRunner.Execute(PlanFor("valve"), runner, Deploys(loaded: true));

        Assert.Equal(BatchRunOutcome.GateRefused, result.Outcome);
        Assert.Contains(result.Steps, s => s.Verdict == StepVerdict.NotProven && s.Reason.Contains("timed out"));
    }

    private sealed class TimeoutRunner : IProcessRunner
    {
        public ProcessResult Run(string executable, IReadOnlyList<string> arguments, TimeSpan timeout) =>
            new(true, true, 0, string.Empty, string.Empty, "expired");
    }

    /// <summary>
    /// 🔴 <b>The negative control for the whole file.</b> Every test above asserts that something stops.
    /// A runner that refused everything would satisfy nearly all of them, so this pins the other
    /// direction: with every step succeeding, both gates are taken, the deployment runs, both lanes run,
    /// and both gates come back — in that order, with no step skipped.
    /// </summary>
    [Fact]
    public void With_everything_succeeding_every_step_runs_in_order()
    {
        var runner = AllOk();

        BatchRunner.Execute(PlanFor("valve", "vessel"), runner, Deploys(loaded: true));

        var verbs = runner.Calls.Select(c => string.Join(" ", c.Args)).ToList();

        // 2 acquire + 1 generate + 2 waves + 2 release. The deployment is not in this list: it is
        // delegated to the gateway rather than run as a process from here.
        Assert.Equal(7, verbs.Count);
        Assert.Contains("acquire", verbs[0]);
        Assert.Contains("acquire", verbs[1]);
        Assert.Contains("--generate-only", verbs[2]);
        Assert.Contains("valve-submission.json", verbs[3]);
        Assert.Contains("vessel-submission.json", verbs[4]);

        // Released in the reverse order they were taken: rig first, then Portal.
        Assert.Contains("release", verbs[5]);
        Assert.Contains("rig:10.10.10.10", verbs[5]);
        Assert.Contains("release", verbs[6]);
        Assert.Contains("portal:", verbs[6]);
    }
}
