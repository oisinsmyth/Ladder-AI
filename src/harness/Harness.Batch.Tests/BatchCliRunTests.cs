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

    /// <summary>
    /// 🔴 Records WHAT was executed, not only how many times. <see cref="CountingRunner"/> is a bare
    /// <c>int++</c> over no argument, order or identity — deliberately, because it backs a negative
    /// control — and a test that needs to know a particular subprocess ran cannot be written on it.
    /// Answers by verb where the test needs a document back, keyed rather than sequenced so a change in
    /// call ORDER does not fail a test for the wrong reason.
    /// </summary>
    private sealed class RecordingRunner : IProcessRunner
    {
        private readonly Dictionary<string, string> _answers = new(StringComparer.Ordinal);

        internal List<(string Executable, IReadOnlyList<string> Arguments)> Calls { get; } = new();

        internal IEnumerable<string> Verbs => Calls.Select(c => c.Arguments.Count > 0 ? c.Arguments[0] : string.Empty);

        internal RecordingRunner Answering(string verb, string standardOutput)
        {
            _answers[verb] = standardOutput;
            return this;
        }

        public ProcessResult Run(string executable, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            Calls.Add((executable, arguments));

            var verb = arguments.Count > 0 ? arguments[0] : string.Empty;
            return new ProcessResult(true, false, 0,
                _answers.TryGetValue(verb, out var stdout) ? stdout : string.Empty, string.Empty, string.Empty);
        }
    }

    private (int Exit, string Output, int Calls) Invoke(params string[] extra)
    {
        var writer = new StringWriter();
        var runner = new CountingRunner();
        var exit = BatchCli.Run(RunArgs(extra), writer, File.ReadAllText, File.WriteAllText, runner,
            _ => new DeploymentOutcome(true, true, new HashSet<string>(), "loaded"));
        return (exit, writer.ToString(), runner.Calls);
    }

    private (int Exit, string Output) InvokeWith(IProcessRunner runner, params string[] extra)
    {
        var writer = new StringWriter();
        var exit = BatchCli.Run(RunArgs(extra), writer, File.ReadAllText, File.WriteAllText, runner,
            _ => new DeploymentOutcome(true, true, new HashSet<string>(), "loaded"));
        return (exit, writer.ToString());
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
    ///
    /// <para>⚠️ <b>EDITED 2026-08-23, TWICE, DELIBERATELY, AND NEITHER EDIT IS INCIDENTAL TO READ.</b></para>
    /// <para><b>(1) The invocation gained <c>--neighbours declared-only</c>: a CLI CONTRACT CHANGE.</b>
    /// <c>run --yes</c> now REFUSES until the neighbour decision is named, so this test's old argument
    /// list no longer describes a run that happens. It is filed as a contract change, not under "no
    /// existing test edited" — the flag is now part of what a deploying caller must type. The escape is
    /// the right value here because this test is about the SUBPROCESS COUNT and <c>derive</c> would add
    /// a subprocess of its own; the derive path has its own control below.</para>
    /// <para><b>(2) The count moved 9 → 10</b>, because <c>--converter</c> now DEFAULTS on <c>run</c> and
    /// the served width therefore derives on the default deployment path. This assertion is a bare
    /// integer over an <c>int++</c> that records no argument, order or identity — nothing here would
    /// notice if the ten calls were ten copies of <c>lease release</c>. Its stated purpose above is the
    /// negative control "a CLI that never executed anything would pass all of them", and <b>that claim
    /// survives at 10 exactly as it stood at 9.</b> The real sequence guard is elsewhere and is NOT in
    /// the blast radius: <c>BatchRunTests.With_everything_succeeding_every_step_runs_in_order</c> asserts
    /// 7 verbs each BY CONTENT over <c>BatchRunner.Execute</c>, and both derivations are direct
    /// <c>runner.Run</c> calls from <c>BatchCli</c> that never become plan steps.</para>
    /// </summary>
    [Fact]
    public void With_yes_and_a_gateway_the_runner_IS_called()
    {
        var (exit, _, calls) = Invoke("--yes", "--neighbours", "declared-only");

        Assert.Equal(BatchExit.Ok, exit);

        // served-area + 1 parity + 2 acquire + export-all + drift-check + generate + 1 wave + 2 release.
        // The deployment goes through the gateway, not the runner, so it is not in this count.
        //
        // The served-area call comes FIRST and is the one added on 2026-08-23: --converter defaults now,
        // so the width the map is allocated against is derived from the program on every deploying run
        // rather than only when somebody remembered a flag.
        //
        // The drift pair is here because --staging is set: the run stages the program union and compares
        // it against the project, since the build stamp claims the supplied program is what executes and
        // nothing used to verify that.
        //
        // The parity call is the reachability cross-check, and it runs before any gate. Both it and the
        // served-area derivation run only under --yes, deliberately: they would be useful in a dry run
        // too, but the dry run's contract is that it starts no process at all, and that invariant is
        // worth more than the convenience.
        Assert.Equal(10, calls);
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

    // =============================================================================================
    // A1 — THE SERVED WIDTH DERIVES ON THE DEFAULT DEPLOYMENT PATH.
    //
    // Until now `--converter` had to be NAMED for `run` to derive it, which meant the guard existed and
    // the path that reaches a controller did not take it. BatchCli.cs said so in its own words and
    // named the price of flipping it: "one line here plus the subprocess count in BatchCliRunTests".
    // =============================================================================================

    /// <summary>
    /// 🔴 <b>No <c>--converter</c> anywhere in the argument list, and the width is derived anyway.</b>
    ///
    /// <para>Asserted on the CALL, not on a count: the point is that <c>converter served-area</c> ran,
    /// and a count cannot tell one subprocess from another. The executable is asserted too, because the
    /// default is a NAME resolved on PATH and a default of <c>""</c> would satisfy a verb-only check.</para>
    /// </summary>
    [Fact]
    public void With_yes_the_served_width_is_DERIVED_even_though_no_converter_was_named()
    {
        var args = RunArgs("--yes", "--neighbours", "declared-only");
        Assert.DoesNotContain("--converter", args);

        var runner = new RecordingRunner();
        var writer = new StringWriter();

        var exit = BatchCli.Run(args, writer, File.ReadAllText, File.WriteAllText, runner,
            _ => new DeploymentOutcome(true, true, new HashSet<string>(), "loaded"));

        Assert.Equal(BatchExit.Ok, exit);
        Assert.Contains(runner.Calls, c => c.Executable == "converter" && c.Arguments[0] == "served-area");

        // And the report stops blaming a missing flag for a derivation that now happens by default.
        Assert.DoesNotContain("no --converter <exe> was named", writer.ToString());
    }

    /// <summary>
    /// The width derives on the DEPLOYING path only. A dry run still starts no process at all, so it
    /// still says the width was declared — the invariant `run` has always had, unchanged by the default.
    /// </summary>
    [Fact]
    public void The_default_converter_does_NOT_make_a_dry_run_start_a_process()
    {
        var runner = new RecordingRunner();
        var (exit, output) = InvokeWith(runner);

        Assert.Equal(BatchExit.Ok, exit);
        Assert.Empty(runner.Calls);
        Assert.Contains("DECLARED, not derived", output);
    }

    // =============================================================================================
    // A2 — THE NEIGHBOUR DECISION IS REQUIRED ON `run --yes`. NOT DEFAULTED — REQUIRED.
    //
    // A blanket default was rejected on evidence: NeighbourFact.Gates is a REFUSAL INPUT, so defaulting
    // `derive` on would turn two ordinary situations (a corpus with no MB_SERVER call; one unparseable
    // file anywhere in the union) into batch refusals. Requiring the choice refuses NOTHING that
    // succeeds today — it only stops the deployment path being decidable by omission.
    // =============================================================================================

    /// <summary>
    /// 🔴 <b>The refusal is a DECISION, not an obstacle: it names both options and what each costs.</b>
    /// A refusal naming only the flag would be answered with whichever value is shortest to type, which
    /// on this flag is the escape.
    /// </summary>
    [Fact]
    public void Run_yes_REFUSES_until_a_neighbour_decision_is_NAMED()
    {
        var runner = new RecordingRunner();
        var (exit, output) = InvokeWith(runner, "--yes");

        Assert.Equal(BatchExit.Unusable, exit);

        // A pure argument check, taken before anything costs anything.
        Assert.Empty(runner.Calls);

        Assert.Contains("--neighbours derive", output);
        Assert.Contains("--neighbours declared-only", output);
        Assert.Contains("NO GATE WAS TAKEN", output);
    }

    /// <summary>
    /// 🔴 <b>THE RUNNING ESCAPE TOTAL IS IN THE REFUSAL, because making the choice mandatory makes
    /// <c>declared-only</c> the routine opt-out — the exact "escape becomes routine" failure
    /// <see cref="NeighbourEscapeLog"/> exists to surface. A counter nobody reads is not a counter.</b>
    /// </summary>
    [Fact]
    public void The_refusal_prints_how_often_this_queue_has_ALREADY_taken_the_escape()
    {
        var (_, fresh) = InvokeWith(new RecordingRunner(), "--yes");
        Assert.Contains("taken 0 time(s)", fresh);

        NeighbourEscapeLog.Record(_queue, "run --yes");
        NeighbourEscapeLog.Record(_queue, "run --yes");

        var (_, after) = InvokeWith(new RecordingRunner(), "--yes");
        Assert.Contains("taken 2 time(s)", after);
    }

    /// <summary>
    /// The older, stronger promise wins the tie. <c>"--yes with nothing to deploy with starts NOTHING"</c>
    /// is refused for its own reason, not swallowed by a newer argument check — a run that cannot happen
    /// should not be made to answer a question about a run that cannot happen.
    /// </summary>
    [Fact]
    public void A_missing_deploy_gateway_is_still_reported_as_ITSELF_and_not_as_a_missing_neighbour_decision()
    {
        var writer = new StringWriter();

        var exit = BatchCli.Run(RunArgs("--yes"), writer, File.ReadAllText, File.WriteAllText,
            new RecordingRunner(), deploy: null);

        Assert.Equal(BatchExit.Unusable, exit);
        Assert.Contains("--deploy-config", writer.ToString());
        Assert.DoesNotContain("--neighbours declared-only", writer.ToString());
    }

    // ---------------------------------------------------------------------------------------------
    // 🔴 THE NEGATIVE CONTROLS. A required flag that ALSO changes behaviour when supplied is a
    // different change from the one intended, and only these tell the two apart. Both pass against the
    // binary BEFORE this change as well as after — that is what makes them controls rather than
    // evidence.
    // ---------------------------------------------------------------------------------------------

    /// <summary>With the escape named, the run deploys exactly as it did before the flag was required.</summary>
    [Fact]
    public void With_declared_only_named_run_yes_behaves_EXACTLY_as_it_did_before()
    {
        var runner = new RecordingRunner();
        var (exit, output) = InvokeWith(runner, "--yes", "--neighbours", "declared-only");

        Assert.Equal(BatchExit.Ok, exit);
        Assert.DoesNotContain("taken 0 time(s)", output);      // the refusal did not fire
        Assert.Contains("[Deploy]", output);
        Assert.Contains("ESCAPE `--neighbours declared-only` TAKEN", output);
        Assert.Contains("NEIGHBOURS: NOT DERIVED", output);
    }

    /// <summary>
    /// 🔴 <b>THE GUARD MUST NOT COST MORE KEYSTROKES THAN THE ESCAPE.</b>
    ///
    /// <para>A2 makes the <c>--neighbours</c> choice mandatory. If <c>derive</c> then also demanded a
    /// <c>--converter</c> that <c>declared-only</c> does not, the escape would be strictly cheaper to
    /// type than the guard — on precisely the choice <see cref="NeighbourEscapeLog"/> exists to watch.
    /// So <c>run</c> derives the neighbour list with the same defaulted converter it derives the width
    /// with.</para>
    ///
    /// <para>It is not the silent downgrade the argument check was written against: a defaulted
    /// <c>converter</c> that is not on PATH still produces a NotDerived, and NotDerived GATES. <c>plan</c>
    /// keeps the refusal, because <c>plan</c> defaults no converter at all.</para>
    /// </summary>
    [Fact]
    public void On_run_derive_needs_no_second_flag_so_the_escape_is_never_the_cheaper_answer()
    {
        var args = RunArgs("--yes", "--neighbours", "derive");
        Assert.DoesNotContain("--converter", args);

        var runner = new RecordingRunner()
            .Answering("served-area",
                """{ "derived": true, "baseByte": 1000, "registers": 576, "denominator": "served area: base 1000, 576 register(s)" }""")
            .Answering("neighbours",
                """{ "derived": true, "scanned": { "tagTables": 2, "blocks": 18 }, "neighbours": [], "denominator": "neighbours: 0 region(s) derived from 2 tag table(s) + 18 block(s)" }""");

        var writer = new StringWriter();
        var exit = BatchCli.Run(args, writer, File.ReadAllText, File.WriteAllText, runner,
            _ => new DeploymentOutcome(true, true, new HashSet<string>(), "loaded"));

        Assert.Equal(BatchExit.Ok, exit);
        Assert.Contains(runner.Calls, c => c.Executable == "converter" && c.Arguments[0] == "neighbours");
    }

    /// <summary>And with <c>derive</c> named, the derivation runs and the run proceeds — likewise unchanged.</summary>
    [Fact]
    public void With_derive_named_run_yes_behaves_EXACTLY_as_it_did_before()
    {
        var runner = new RecordingRunner()
            .Answering("served-area",
                """{ "derived": true, "baseByte": 1000, "registers": 576, "denominator": "served area: base 1000, 576 register(s)" }""")
            .Answering("neighbours",
                """{ "derived": true, "scanned": { "tagTables": 2, "blocks": 18 }, "neighbours": [], "denominator": "neighbours: 0 region(s) derived from 2 tag table(s) + 18 block(s)" }""");

        var (exit, output) = InvokeWith(runner, "--yes", "--converter", "converter.exe", "--neighbours", "derive");

        Assert.Equal(BatchExit.Ok, exit);
        Assert.DoesNotContain("taken 0 time(s)", output);      // the refusal did not fire
        Assert.Contains("neighbours", runner.Verbs);
        Assert.Contains("[Deploy]", output);
    }
}
