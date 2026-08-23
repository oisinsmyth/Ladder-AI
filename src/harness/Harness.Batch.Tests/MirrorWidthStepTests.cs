using Harness.Batch;
using Harness.Device;
using Harness.Loop;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>THE DERIVED WIDTH, ASKED OF THE CONTROLLER — the wiring, and what it is allowed to conclude.</b>
///
/// <para><c>served-area</c> reads the Modbus width off the program corpus and prints, on every run, that
/// it cannot see whether the block it read is the block on the controller. The comparison it is missing
/// already exists whole: <c>harness-mirror-read --declared-registers</c> is exactly "is the area on the
/// device as wide as this claim", and its exit codes are the verdict. What did not exist was a caller
/// feeding it the DERIVED number instead of a hand-typed one.</para>
///
/// <para><b>What these tests prove and what they do not.</b> They substitute the process runner, so they
/// prove the argument vector, the placement, the absence conditions and how each exit code is READ. They
/// do not prove <c>harness-mirror-read</c> answers that way against a rig — that claim belongs to
/// <c>Harness.MirrorRead.Tests</c> and, ultimately, to a rig session.</para>
/// </summary>
public class MirrorWidthStepTests
{
    private const string MirrorReadExe = @"C:\bin\harness-mirror-read.exe";

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
        DeviceAllowlistPath: @"C:\tools\download-probe.allowlist",
        MirrorReadExe: MirrorReadExe);

    private static Lane LaneNamed(string name) =>
        new(name, name + "-binding.json", name + "-submission.json", new[] { name + @"\ir" });

    /// <summary>A planned batch whose served area WAS derived, at the given width.</summary>
    private static BatchPlanResult Derived(int registers, params string[] lanes) =>
        new(lanes.Length, lanes, Array.Empty<string>(), "{}", null, lanes.Select(n => n + @"\ir").ToArray(),
            ServedArea: new ServedAreaFact(true, 1000, registers,
                $"derived from MB_SERVER: base %MB1000, {registers} register(s)", Array.Empty<string>()));

    /// <summary>The same batch with the derivation ABSENT — the state every batch was in before Y2.</summary>
    private static BatchPlanResult NotDerived(params string[] lanes) =>
        new(lanes.Length, lanes, Array.Empty<string>(), "{}", null, lanes.Select(n => n + @"\ir").ToArray(),
            ServedArea: ServedAreaFact.NotAsked);

    private static BatchRunPlan PlanFor(BatchPlanResult batch, BatchRunOptions? options = null) =>
        BatchRunPlan.For(batch, batch.LanesBatched.Select(LaneNamed).ToArray(), options ?? Options());

    private static string Value(BatchStep step, string flag)
    {
        var index = step.Arguments.ToList().IndexOf(flag);
        Assert.True(index >= 0, $"the step carries no {flag}: {step.CommandLineText}");
        return step.Arguments[index + 1];
    }

    /// <summary>Answers each invocation with a scripted exit code, and records what was asked.</summary>
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

    private static Func<DeploymentOutcome> Loads() =>
        () => new DeploymentOutcome(true, true, new HashSet<string>(), "loaded");

    /// <summary>A runner where only harness-mirror-read answers with the given code.</summary>
    private static ScriptedRunner MirrorExits(int code) =>
        new((exe, _) => exe == MirrorReadExe ? code : 0);

    // =============================================================================================
    // 1 — WHERE IT SITS, AND WHY THERE AND NOWHERE ELSE.
    // =============================================================================================

    /// <summary>
    /// 🔴 <b><c>MB_SERVER</c> ACCEPTS ONE CONNECTION PER INSTANCE.</b> With a second client attached a
    /// conformance wave once reported 0 of 22 vectors attempted — so this step is strictly sequential
    /// with the waves, and after the deployment because the width it is asking about is the one that was
    /// just downloaded. Between <c>Deploy</c> and the first <c>Wave</c> is the only correct slot.
    /// </summary>
    [Fact]
    public void The_width_step_sits_AFTER_the_deployment_and_BEFORE_every_wave()
    {
        var kinds = PlanFor(Derived(576, "valve", "vessel")).Steps.Select(s => s.Kind).ToList();

        var width = kinds.IndexOf(BatchStepKind.MirrorWidth);
        Assert.True(width > kinds.IndexOf(BatchStepKind.Deploy), "the width step must follow the deployment.");
        Assert.True(width < kinds.IndexOf(BatchStepKind.Wave), "the width step must precede the FIRST wave.");
        Assert.Equal(1, kinds.Count(k => k == BatchStepKind.MirrorWidth));
    }

    /// <summary>
    /// 🔴 <b>THE DERIVED VALUE, NEVER THE AUTHORED ONE.</b> The authored <c>declaredRegisters</c> is the
    /// field the derivation exists to check; feeding it back in would make the run compare a number
    /// against itself and pass.
    /// </summary>
    [Fact]
    public void It_carries_the_DERIVED_width_as_the_claim_under_test()
    {
        var step = Assert.Single(PlanFor(Derived(576, "valve")).Steps.Where(s => s.Kind == BatchStepKind.MirrorWidth));

        Assert.Equal(MirrorReadExe, step.Executable);
        Assert.Equal("576", Value(step, "--declared-registers"));
        Assert.Equal("10.10.10.10", Value(step, "--address"));
    }

    /// <summary>
    /// The rig's own allowlist and the calibrated transport's port and unit — no second configuration.
    /// <i>"A second client with its own settings would be an uncalibrated one."</i>
    /// </summary>
    [Fact]
    public void It_reuses_the_rig_allowlist_and_the_calibrated_port_and_unit()
    {
        var step = Assert.Single(PlanFor(Derived(37, "valve")).Steps.Where(s => s.Kind == BatchStepKind.MirrorWidth));

        Assert.Equal(@"C:\tools\download-probe.allowlist", Value(step, "--allowlist"));
        Assert.Equal("503", Value(step, "--port"));
        Assert.Equal("1", Value(step, "--unit"));
    }

    /// <summary>
    /// 🔴 <b>A download stops the CPU, so this step meets a just-restarted scan counter.</b> Exit 8 is
    /// reachable and is NOT a width verdict. The waves already solved this with inert-retry — ask again
    /// rather than sleep a guessed duration — and the step reuses those same two numbers rather than
    /// inventing a second policy for the same transient.
    /// </summary>
    [Fact]
    public void It_reuses_the_waves_OWN_retry_numbers_for_the_just_restarted_CPU()
    {
        var options = Options() with { InertRetries = 12, InertRetryIntervalSeconds = 5 };
        var step = Assert.Single(PlanFor(Derived(37, "valve"), options).Steps.Where(s => s.Kind == BatchStepKind.MirrorWidth));

        Assert.Equal("12", Value(step, "--scan-retry"));
        Assert.Equal("5000", Value(step, "--scan-retry-interval-ms"));
    }

    /// <summary>
    /// The verdict is written to a file, not only to a terminal. The width reaches a reader as prose
    /// today and only the exit code is structured — a run whose evidence is scrollback is a run nobody
    /// can cite afterwards.
    /// </summary>
    [Fact]
    public void It_writes_its_report_into_the_staging_directory()
    {
        var step = Assert.Single(PlanFor(Derived(37, "valve")).Steps.Where(s => s.Kind == BatchStepKind.MirrorWidth));

        Assert.Equal(Path.Combine(@"C:\staging", "mirror-width.json"), Value(step, "--out"));
    }

    // =============================================================================================
    // 2 — THE NEGATIVE CONTROLS: IT IS ABSENT, AND ITS ABSENCE IS NOT A PASS.
    // =============================================================================================

    /// <summary>
    /// 🔴 <b>No derivation, no comparison — and the plan SAYS so.</b> Feeding the AUTHORED width to the
    /// device would be the tool agreeing with the binding, which is the check the whole item exists to
    /// stop being.
    /// </summary>
    [Fact]
    public void With_NO_derived_width_the_step_is_absent_and_the_plan_says_the_device_was_not_asked()
    {
        var plan = PlanFor(NotDerived("valve"));

        Assert.DoesNotContain(plan.Steps, s => s.Kind == BatchStepKind.MirrorWidth);
        Assert.Contains(plan.Notices, n => n.Contains("NOT COMPARED AGAINST THE DEVICE", StringComparison.Ordinal));
    }

    /// <summary>The same for a batch with no binary to run: absent, and never silent.</summary>
    [Fact]
    public void With_no_mirror_read_binary_the_step_is_absent_and_the_plan_says_so()
    {
        var plan = PlanFor(Derived(576, "valve"), Options() with { MirrorReadExe = null });

        Assert.DoesNotContain(plan.Steps, s => s.Kind == BatchStepKind.MirrorWidth);
        Assert.Contains(plan.Notices, n => n.Contains("NOT COMPARED AGAINST THE DEVICE", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>THE CONTROL FOR BOTH.</b> Every test above asserts an absence; a planner that emitted this
    /// step under no condition at all would pass them. With a derivation and a binary it IS planned.
    /// </summary>
    [Fact]
    public void With_a_derivation_and_a_binary_the_step_IS_planned_and_no_notice_claims_otherwise()
    {
        var plan = PlanFor(Derived(576, "valve"));

        Assert.Contains(plan.Steps, s => s.Kind == BatchStepKind.MirrorWidth);
        Assert.DoesNotContain(plan.Notices, n => n.Contains("NOT COMPARED AGAINST THE DEVICE", StringComparison.Ordinal));
    }

    // =============================================================================================
    // 3 — HOW EACH EXIT CODE IS READ. THE GATING DECISION, ASSERTED.
    // =============================================================================================

    /// <summary>
    /// 🔴 <b>EXIT 6, NARROWER THAN DERIVED, STOPS THE BATCH — AND NO WAVE RUNS.</b>
    ///
    /// <para>The map was allocated against the derived width. If the server serves fewer registers than
    /// the map spans, every slot above the real edge is invisible to the harness client — and a wave run
    /// then reports vector failures against the LOGIC when the cause is the window. A confident wrong
    /// answer about the plant is the one outcome this project spends everything to avoid, and unlike a
    /// lane failure this is a SHARED precondition: it makes every later step meaningless.</para>
    /// </summary>
    [Fact]
    public void A_device_NARROWER_than_derived_STOPS_the_batch_and_no_wave_is_attempted()
    {
        var runner = MirrorExits(6);

        var result = BatchRunner.Execute(PlanFor(Derived(576, "valve", "vessel")), runner, Loads());

        Assert.DoesNotContain(runner.Calls, c => c.Args.Contains("--verify"));
        Assert.Empty(result.LanesRun);
        Assert.Equal(2, result.LanesNotRun.Count);
        Assert.Contains(result.Steps, s => s.Step.Kind == BatchStepKind.MirrorWidth && s.Verdict == StepVerdict.Failed);
        Assert.Contains("NARROWER", result.Headline, StringComparison.Ordinal);

        // And the gates still come back: a lease left held by a stopped run blocks every other agent.
        Assert.True(result.LeasesReleased);
    }

    /// <summary>
    /// <b>EXIT 7, WIDER THAN DERIVED, DOES NOT STOP THE BATCH — and is NOT recorded as a pass.</b>
    ///
    /// <para>Every register the harness reads is inside the derived area, so a server exposing MORE than
    /// derived makes no vector unreadable and invalidates no wave. What it does mean is that the
    /// occupancy analysis was done over a smaller area than the device exposes — a real finding about the
    /// DERIVATION, not about these readings. Spending three lanes of rig time on it would be paying the
    /// wrong bill.</para>
    /// </summary>
    [Fact]
    public void A_device_WIDER_than_derived_lets_the_waves_run_but_is_NOT_recorded_as_Ok()
    {
        var result = BatchRunner.Execute(PlanFor(Derived(576, "valve", "vessel")), MirrorExits(7), Loads());

        Assert.Equal(new[] { "valve", "vessel" }, result.LanesRun);

        var step = Assert.Single(result.Steps.Where(s => s.Step.Kind == BatchStepKind.MirrorWidth));
        Assert.NotEqual(StepVerdict.Ok, step.Verdict);
        Assert.Contains("WIDER", result.Headline, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>EXIT 4 — THE MEASUREMENT THAT DID NOT HAPPEN.</b> It is the state that looks most like a
    /// pass, so it is the one that must never read as one: not <c>Ok</c>, and on the headline where a
    /// reader meets it, not buried in a step list.
    /// </summary>
    [Fact]
    public void A_measurement_that_did_NOT_happen_is_never_Ok_and_reaches_the_headline()
    {
        var result = BatchRunner.Execute(PlanFor(Derived(576, "valve")), MirrorExits(4), Loads());

        var step = Assert.Single(result.Steps.Where(s => s.Step.Kind == BatchStepKind.MirrorWidth));
        Assert.Equal(StepVerdict.NotProven, step.Verdict);
        Assert.Contains("MEASURED NOTHING", result.Headline, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>EXIT 8 IS NOT A WIDTH VERDICT.</b> A download stops the CPU; the step already asks again
    /// (<c>--scan-retry</c>), and a counter still not rising after that is a fact about liveness which
    /// the wave's own inert phase and version check are better placed to judge. Recorded, not silent,
    /// and not read as a width finding either way.
    /// </summary>
    [Fact]
    public void A_scan_counter_that_did_not_rise_is_recorded_as_NOT_a_width_verdict()
    {
        var result = BatchRunner.Execute(PlanFor(Derived(576, "valve")), MirrorExits(8), Loads());

        var step = Assert.Single(result.Steps.Where(s => s.Step.Kind == BatchStepKind.MirrorWidth));
        Assert.Equal(StepVerdict.NotProven, step.Verdict);
        Assert.Contains("NOT a width verdict", step.Reason, StringComparison.Ordinal);
        Assert.Equal(new[] { "valve" }, result.LanesRun);
    }

    /// <summary>
    /// 🔴 <b>THE CONTROL FOR THE WHOLE SECTION.</b> Every test above asserts something is refused or
    /// caveated; a reader that failed everything would satisfy most of them. Exit 0 — the device is
    /// exactly as wide as the program says — runs every lane and says so plainly.
    /// </summary>
    [Fact]
    public void A_device_EXACTLY_as_wide_as_derived_passes_and_every_lane_runs()
    {
        var result = BatchRunner.Execute(PlanFor(Derived(576, "valve", "vessel")), MirrorExits(0), Loads());

        var step = Assert.Single(result.Steps.Where(s => s.Step.Kind == BatchStepKind.MirrorWidth));
        Assert.Equal(StepVerdict.Ok, step.Verdict);
        Assert.Equal(new[] { "valve", "vessel" }, result.LanesRun);
        Assert.Equal(BatchRunOutcome.Ran, result.Outcome);
    }

    /// <summary>
    /// And it is one client, taken and handed back before the first wave opens its own. The exclusivity
    /// is <c>MB_SERVER</c>'s, not ours: two connections and the wave measures nothing.
    /// </summary>
    [Fact]
    public void The_width_step_is_executed_before_the_first_wave_reaches_the_device()
    {
        var order = new List<string>();
        var runner = new ScriptedRunner((exe, args) =>
        {
            if (exe == MirrorReadExe) order.Add("width");
            if (args.Contains("--verify")) order.Add("wave");
            return 0;
        });

        BatchRunner.Execute(PlanFor(Derived(576, "valve", "vessel")), runner, Loads());

        Assert.Equal(new[] { "width", "wave", "wave" }, order);
    }
}
