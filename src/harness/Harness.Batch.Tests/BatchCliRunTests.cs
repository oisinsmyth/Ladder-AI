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

        new LaneQueue(_queue).Enqueue(new Lane("valve", binding, submission, new[] { Path.Combine(_root, "ir") }));
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
}
