using Harness.Batch;
using Harness.Device;
using Harness.Loop;
using Harness.Map;

namespace Harness.Batch.Tests;

/// <summary>
/// The <c>run</c> verb's own gate: <b><c>--yes</c>, and without it nothing is contacted.</b>
///
/// <para>Every test here asserts on the RUNNER as well as the exit code, because an exit code proves
/// the command ended a particular way and says nothing about whether a lease was taken on the way. That
/// is the same reason <c>download-probe</c>'s fence is tested with a sentinel rather than by its exit
/// code alone.</para>
/// </summary>
public sealed class BatchCliRunTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "batch-cli-" + Guid.NewGuid().ToString("N"));
    private readonly string _queue;

    public BatchCliRunTests()
    {
        Directory.CreateDirectory(_root);
        _queue = Path.Combine(_root, "queue");

        var binding = Path.Combine(_root, "lane.json");
        var submission = Path.Combine(_root, "sub.json");
        File.WriteAllText(binding,
            "{ \"blockName\": \"FC_HarnessCopyLayer\", \"blockNumber\": 9001, \"tagTableName\": \"HarnessMirror\", "
            + "\"tagPrefix\": \"HX_\", \"baseByte\": 1000, \"retentiveBytes\": 256, \"declaredRegisters\": 576, "
            + "\"slots\": [{ \"slotId\": \"S0\", \"startCondition\": \"Go\", "
            + "\"vectorTargets\": [{ \"tag\": \"In\", \"specName\": \"In\", \"type\": \"Int\" }], "
            + "\"resultSources\": [{ \"tag\": \"Out\", \"specName\": \"Out\", \"type\": \"Int\" }] }] }");
        File.WriteAllText(submission, "{}");

        // 🔴 A REAL program directory. This used to name a path that did not exist, and the planner
        // happily planned over it: the union was empty, so the basename-collision check could not fire
        // and reachability had nothing to walk. The batch is now refused when a program path contributes
        // nothing — the build stamp is computed over this set and claims it is what executes.
        var ir = Path.Combine(_root, "ir");
        Directory.CreateDirectory(ir);
        File.WriteAllText(Path.Combine(ir, "FC_LaneFixture.ir"), "BLOCK FC FC_LaneFixture\nEND_BLOCK\n");

        new LaneQueue(_queue).Enqueue(new Lane("valve", binding, submission, new[] { ir }));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private sealed class CountingRunner : IProcessRunner
    {
        internal int Calls { get; private set; }

        public ProcessResult Run(string executable, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            Calls++;
            return new ProcessResult(true, false, 0, string.Empty, string.Empty, string.Empty);
        }
    }

    private string[] RunArgs(params string[] extra) => new[]
    {
        "run",
        "--queue", _queue,
        "--merged", Path.Combine(_root, "merged.json"),
        "--staging", _root,
        "--leases", Path.Combine(_root, "leases"),
        "--holder", "agent-a",
        "--holder-pid", "4242",
        "--portal-project", @"C:\projects\Rig\Rig.ap20",
        "--portal-evidence", Path.Combine(_root, "portal.json"),
        "--rig", "10.10.10.10",
        "--settle-seconds", "0",
    }.Concat(extra).ToArray();

    /// <summary>Drop a flag and its value by NAME, so neighbouring arguments are never disturbed.</summary>
    private static string[] Without(string[] args, string flag)
    {
        var index = Array.IndexOf(args, flag);
        Assert.True(index >= 0, $"{flag} is not in the argument list, so removing it proves nothing.");

        return args.Take(index).Concat(args.Skip(index + 2)).ToArray();
    }

    private (int Exit, string Output, int Calls) Invoke(params string[] extra)
    {
        var writer = new StringWriter();
        var runner = new CountingRunner();
        var exit = BatchCli.Run(RunArgs(extra), writer, File.ReadAllText, File.WriteAllText, runner,
            _ => new DeploymentOutcome(true, true, new HashSet<string>(), "loaded"));
        return (exit, writer.ToString(), runner.Calls);
    }

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>The dry run starts NO process.</b> Not "starts no Portal process" — none at all, including
    /// the lease acquire, which is the first thing that would block another agent.
    /// </summary>
    [Fact]
    public void Without_yes_NOTHING_is_executed()
    {
        var (exit, output, calls) = Invoke();

        Assert.Equal(BatchExit.Ok, exit);
        Assert.Equal(0, calls);
        Assert.Contains("NO GATE WAS TAKEN", output);
        Assert.Contains("PORTAL WAS NOT CONTACTED", output);
    }

    /// <summary>And it still prints the whole ordered sequence, which is the point of having it.</summary>
    [Fact]
    public void The_dry_run_prints_every_command_in_order()
    {
        var (_, output, _) = Invoke();

        Assert.Contains("[LeaseAcquire]", output);
        Assert.Contains("[Generate]", output);
        Assert.Contains("[Deploy]", output);
        Assert.Contains("[Wave]", output);
        Assert.Contains("[LeaseRelease]", output);

        Assert.True(output.IndexOf("[LeaseAcquire]", StringComparison.Ordinal) < output.IndexOf("[Deploy]", StringComparison.Ordinal));
        Assert.True(output.IndexOf("[Wave]", StringComparison.Ordinal) < output.LastIndexOf("[LeaseRelease]", StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>--yes</c> with nothing to deploy with is refused BEFORE the gates, not at the deploy step.
    /// Stopping there would have taken and handed back two gates to discover a missing argument.
    /// </summary>
    [Fact]
    public void Yes_without_a_deploy_gateway_is_refused_before_ANY_gate_is_taken()
    {
        var writer = new StringWriter();
        var runner = new CountingRunner();

        var exit = BatchCli.Run(RunArgs("--yes"), writer, File.ReadAllText, File.WriteAllText, runner, deploy: null);

        Assert.Equal(BatchExit.Unusable, exit);
        Assert.Equal(0, runner.Calls);
        Assert.Contains("NO GATE WAS TAKEN", writer.ToString());
    }

    /// <summary>
    /// An empty queue exits 3 and starts nothing. A run over no lanes would take the gate, deploy
    /// nothing and report a clean pass.
    /// </summary>
    [Fact]
    public void An_empty_queue_exits_NothingBatched_and_starts_no_process()
    {
        var writer = new StringWriter();
        var runner = new CountingRunner();

        var args = RunArgs().ToArray();
        args[2] = Path.Combine(_root, "empty-queue");

        var exit = BatchCli.Run(args, writer, File.ReadAllText, File.WriteAllText, runner,
            _ => new DeploymentOutcome(true, true, new HashSet<string>(), "loaded"));

        Assert.Equal(BatchExit.NothingBatched, exit);
        Assert.Equal(0, runner.Calls);
        Assert.Contains("NOTHING WAS RUN", writer.ToString());
    }

    /// <summary>
    /// 🔴 <b>The negative control.</b> Every test above asserts nothing ran; a CLI that never executed
    /// anything would pass all of them. With <c>--yes</c> and a gateway, the runner IS called.
    /// </summary>
    [Fact]
    public void With_yes_and_a_gateway_the_runner_IS_called()
    {
        var (exit, _, calls) = Invoke("--yes");

        Assert.Equal(BatchExit.Ok, exit);

        // 2 acquire + export-all + drift-check + generate + 1 wave + 2 release. The deployment goes
        // through the gateway, not the runner, so it is not in this count.
        //
        // The drift pair is here because --staging is set: the run stages the program union and compares
        // it against the project, since the build stamp claims the supplied program is what executes and
        // nothing used to verify that.
        Assert.Equal(8, calls);
    }

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE DRIFT CHECK CANNOT DISAPPEAR QUIETLY.</b>
    ///
    /// <para>The export/drift pair is only planned when there is somewhere to stage the union, and without
    /// <c>--staging</c> there is not. Until now that produced NO LINE AT ALL — the steps were simply absent
    /// from the plan, while the comment at their construction site claimed <i>"the run SAYS so rather than
    /// passing quietly"</i>. It did not. The most important gate this batch has could be dropped by
    /// omitting one flag, and the report was indistinguishable from a run that had passed it.</para>
    ///
    /// <para>Asserted on the REPORT, not the exit code: an exit code cannot tell "compared and matched"
    /// from "never compared".</para>
    /// </summary>
    [Fact]
    public void Without_staging_the_run_SAYS_the_program_was_not_compared_against_the_project()
    {
        var writer = new StringWriter();
        var runner = new CountingRunner();

        // Every argument except --staging and its value. Dropped BY NAME, not by index: an index slice
        // silently ate the neighbouring --merged value when this was first written, which left the test
        // passing for the wrong reason.
        var args = Without(RunArgs(), "--staging");
        Assert.DoesNotContain("--staging", args);
        Assert.Contains("--merged", args);

        BatchCli.Run(args, writer, File.ReadAllText, File.WriteAllText, runner,
            _ => new DeploymentOutcome(true, true, new HashSet<string>(), "loaded"));

        var output = writer.ToString();
        Assert.Contains("NOT STAGED", output);
        Assert.Contains("WAS NOT COMPARED AGAINST THE PROJECT", output);
        Assert.DoesNotContain("[DriftCheck]", output);
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL.</b> With <c>--staging</c> the comparison IS planned, and the union line
    /// carries its denominator — how many files, out of how many paths asked for. "12 staged" cannot be
    /// told from "12 of 15", and only the second says whether the comparison covers what was requested.
    /// </summary>
    [Fact]
    public void With_staging_the_comparison_is_planned_and_the_union_line_states_its_denominator()
    {
        var (_, output, _) = Invoke();

        Assert.Contains("[DriftCheck]", output);
        Assert.Contains("requested path(s)", output);
        Assert.DoesNotContain("NOT STAGED", output);
    }
}
