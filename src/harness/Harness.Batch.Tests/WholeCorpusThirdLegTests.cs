using Harness.Batch;
using Harness.Device;
using Harness.Loop;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>THE THIRD LEG — the whole committed corpus against the whole project — AS A STEP, BECAUSE THIS
/// REPO HAS NO SCHEDULER AND INVENTING ONE WOULD BE THE WRONG MOVE.</b>
///
/// <para>Exactly one self-firing mechanism exists here (<c>hooks/pre-commit</c>); there is no CI and no
/// timed hook. The idiom is three-way — an xunit test, a pre-commit refusal, or a <c>BatchStep</c> for
/// anything needing Portal or the rig — and the batch already emits <c>ExportAll</c> + <c>DriftCheck</c>
/// under a Portal lease. <b>This is not a new capability: it is the same pair asking a different
/// question</b>, over a different population, whenever Portal is already held.</para>
///
/// <para><b>Different from the union pair in three ways, all deliberate.</b> <c>--complete</c> (the
/// exports dir is the whole picture, so an object with no <c>.ir</c> is a finding rather than a
/// silence); <c>--tagtables</c> on the export (without it <c>DefaultTagTable</c> and <c>HarnessMirror</c>
/// come back SKIPPED — and <c>HarnessMirror</c> is where the register map lives); and the whole committed
/// corpus as <c>--project</c> rather than the lane union.</para>
/// </summary>
public class WholeCorpusThirdLegTests
{
    private const string Committed = @"C:\repo\ir\test-project001";
    private const string WholeExport = @"C:\staging\whole-project-xml";
    private const string UnionExport = @"C:\staging\project-xml";

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
        CommittedCorpusDirectory: Committed,
        WholeCorpusExportDirectory: WholeExport);

    private static Lane LaneNamed(string name) =>
        new(name, name + "-binding.json", name + "-submission.json", new[] { name + @"\ir" });

    private static BatchPlanResult Batch(params string[] lanes) =>
        new(lanes.Length, lanes, Array.Empty<string>(), "{}", null, lanes.Select(n => n + @"\ir").ToArray());

    private static BatchRunPlan PlanFor(BatchRunOptions? options = null) =>
        BatchRunPlan.For(Batch("valve"), new[] { LaneNamed("valve") }, options ?? Options());

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

    private static BatchStep StepOf(BatchRunPlan plan, BatchStepKind kind) =>
        Assert.Single(plan.Steps.Where(s => s.Kind == kind));

    // =============================================================================================
    // 1 — THE QUESTION IT ASKS, AND HOW IT DIFFERS FROM THE UNION PAIR.
    // =============================================================================================

    /// <summary>
    /// <c>--complete</c> declares the exports dir the whole picture, so an object in the project that no
    /// <c>.ir</c> describes is a finding rather than a silence. The union pair deliberately omits it —
    /// the union is a subset of a ~119-object project — and both are in the plan at once.
    /// </summary>
    [Fact]
    public void The_whole_corpus_drift_check_asks_the_COMPLETE_question_over_the_COMMITTED_corpus()
    {
        var plan = PlanFor(Options() with { UnionIrDirectory = @"C:\staging\union-ir", ProjectExportDirectory = UnionExport });

        var whole = StepOf(plan, BatchStepKind.WholeCorpusDrift);
        var args = whole.Arguments.ToList();

        Assert.Contains("--complete", args);
        Assert.Equal(Committed, args[args.IndexOf("--project") + 1]);
        Assert.Equal(WholeExport, args[args.IndexOf("--exports") + 1]);

        // And the union pair is untouched by it: still no --complete, still the union directory.
        var union = StepOf(plan, BatchStepKind.DriftCheck);
        Assert.DoesNotContain("--complete", union.Arguments);
        Assert.Equal(@"C:\staging\union-ir", union.Arguments[union.Arguments.ToList().IndexOf("--project") + 1]);
    }

    /// <summary>
    /// 🔴 <b><c>--tagtables</c>, which today's third-leg run omitted.</b> Without it the export holds no
    /// tag tables, so <c>DefaultTagTable</c> and <c>HarnessMirror</c> pair with nothing and come back
    /// SKIPPED — and <c>HarnessMirror</c> is the object where the register map lives, the one most worth
    /// comparing against the controller.
    /// </summary>
    [Fact]
    public void Its_export_carries_tagtables_because_the_register_map_lives_in_one()
    {
        var export = StepOf(PlanFor(), BatchStepKind.WholeCorpusExport);

        Assert.Contains("export-all", export.Arguments);
        Assert.Contains("--tagtables", export.Arguments);
        Assert.Equal(WholeExport, export.Arguments[export.Arguments.ToList().IndexOf("--out") + 1]);
    }

    /// <summary>
    /// It runs where Portal is already held and where it costs no rig time: after the gates, before the
    /// deployment, and released with everything else. That is the closest thing to a schedule this repo
    /// has.
    /// </summary>
    [Fact]
    public void It_runs_inside_the_Portal_lease_and_before_the_deployment()
    {
        var kinds = PlanFor().Steps.Select(s => s.Kind).ToList();

        var export = kinds.IndexOf(BatchStepKind.WholeCorpusExport);
        var drift = kinds.IndexOf(BatchStepKind.WholeCorpusDrift);

        Assert.True(export > kinds.LastIndexOf(BatchStepKind.LeaseAcquire), "it must run after both gates.");
        Assert.True(drift == export + 1, "the comparison must follow its own export immediately.");
        Assert.True(drift < kinds.IndexOf(BatchStepKind.Deploy), "it must not sit between the deploy and the waves.");
        Assert.True(drift < kinds.IndexOf(BatchStepKind.LeaseRelease), "it must run while the Portal gate is still held.");
    }

    /// <summary>
    /// 🔴 <b>ITS OWN EXPORT DIRECTORY, AND THE COLLISION IS REFUSED.</b> The union export is taken
    /// WITHOUT <c>--tagtables</c> on purpose. Pointing <c>--complete</c> at a directory another step also
    /// writes into makes the denominator unstatable — which is the one thing a <c>--complete</c> run is
    /// for.
    /// </summary>
    [Fact]
    public void Sharing_one_export_directory_with_the_union_pair_is_REFUSED()
    {
        var plan = BatchRunPlan.For(Batch("valve"), new[] { LaneNamed("valve") },
            Options() with { UnionIrDirectory = @"C:\staging\union-ir", ProjectExportDirectory = WholeExport });

        Assert.False(plan.Planned);
        Assert.Empty(plan.Steps);
        Assert.Contains(plan.Refusals, r => r.Contains("--whole-corpus-exports", StringComparison.Ordinal));
    }

    // =============================================================================================
    // 2 — ABSENT IS NOT PASSED, AND ON A LIVE JOB IT IS ALWAYS ABSENT.
    // =============================================================================================

    /// <summary>
    /// ⚠️ <b>Scoped honestly.</b> A live job has no committed corpus, so this step cannot exist there —
    /// and a step that cannot exist must not be indistinguishable from one that passed. The plan says
    /// which of the two happened, in the step's own words.
    /// </summary>
    [Fact]
    public void With_no_committed_corpus_the_pair_is_absent_and_the_plan_says_WHY()
    {
        var plan = PlanFor(Options() with { CommittedCorpusDirectory = null });

        Assert.DoesNotContain(plan.Steps, s => s.Kind is BatchStepKind.WholeCorpusExport or BatchStepKind.WholeCorpusDrift);
        Assert.Contains(plan.Notices, n => n.Contains("WAS NOT COMPARED AGAINST THE WHOLE COMMITTED CORPUS", StringComparison.Ordinal));
        Assert.Contains(plan.Notices, n => n.Contains("live job", StringComparison.Ordinal));
    }

    /// <summary>The same when there is nowhere to put the whole-project export.</summary>
    [Fact]
    public void With_nowhere_to_export_the_pair_is_absent_and_the_plan_says_so()
    {
        var plan = PlanFor(Options() with { WholeCorpusExportDirectory = null });

        Assert.DoesNotContain(plan.Steps, s => s.Kind is BatchStepKind.WholeCorpusExport or BatchStepKind.WholeCorpusDrift);
        Assert.Contains(plan.Notices, n => n.Contains("WAS NOT COMPARED AGAINST THE WHOLE COMMITTED CORPUS", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>THE CONTROL.</b> Both tests above assert an absence, and a planner that never emitted the
    /// pair would pass both. Given a corpus and somewhere to export, it IS planned and nothing claims
    /// otherwise.
    /// </summary>
    [Fact]
    public void Given_a_corpus_and_an_export_directory_the_pair_IS_planned()
    {
        var plan = PlanFor();

        Assert.Contains(plan.Steps, s => s.Kind == BatchStepKind.WholeCorpusExport);
        Assert.Contains(plan.Steps, s => s.Kind == BatchStepKind.WholeCorpusDrift);
        Assert.DoesNotContain(plan.Notices, n => n.Contains("WAS NOT COMPARED AGAINST THE WHOLE COMMITTED CORPUS", StringComparison.Ordinal));
    }

    // =============================================================================================
    // 3 — WHAT ITS FINDINGS DO. THEY REPORT; THEY DO NOT SPEND THE RIG.
    // =============================================================================================

    /// <summary>
    /// 🔴 <b>A whole-corpus finding does NOT stop the batch, and is NOT recorded as Ok.</b>
    ///
    /// <para>The union pair's exit 1 gates, and must: an object a lane SUPPLIED not matching the project
    /// means the build stamp describes a program nobody is running. This pair's population is wider — it
    /// includes objects no lane deploys — so a difference there says nothing about this deployment's
    /// stamp. Gating on it would stop a correct rig run over a stale export of an unrelated block, and a
    /// gate that fires on things outside its scope is one people learn to switch off.</para>
    /// </summary>
    [Fact]
    public void A_whole_corpus_difference_is_reported_LOUDLY_and_still_lets_the_waves_run()
    {
        var runner = new ScriptedRunner((exe, args) =>
            exe.Contains("converter") && args.Contains("--complete") ? 1 : 0);

        var result = BatchRunner.Execute(PlanFor(), runner, Loads());

        var step = Assert.Single(result.Steps.Where(s => s.Step.Kind == BatchStepKind.WholeCorpusDrift));
        Assert.NotEqual(StepVerdict.Ok, step.Verdict);
        Assert.Equal(new[] { "valve" }, result.LanesRun);
        Assert.Equal(BatchRunOutcome.Ran, result.Outcome);
        Assert.Contains("WHOLE-PROJECT", result.Headline, StringComparison.Ordinal);
    }

    /// <summary>
    /// An export that could not be taken leaves nothing to compare, and the comparison that follows it
    /// would then be against an unstated denominator. Reported; still not a reason to stop a rig run.
    /// </summary>
    [Fact]
    public void An_export_that_failed_does_not_stop_the_batch_and_is_not_recorded_as_Ok()
    {
        var runner = new ScriptedRunner((exe, args) =>
            exe.Contains("openness-cli") && args.Contains("--tagtables") ? 12 : 0);

        var result = BatchRunner.Execute(PlanFor(), runner, Loads());

        var step = Assert.Single(result.Steps.Where(s => s.Step.Kind == BatchStepKind.WholeCorpusExport));
        Assert.Equal(StepVerdict.NotProven, step.Verdict);
        Assert.Equal(new[] { "valve" }, result.LanesRun);
    }

    /// <summary>
    /// 🔴 <b>THE CONTROL FOR THIS SECTION.</b> Both tests above would pass against a reader that marked
    /// this pair Ok whatever it returned. Exit 0 must be Ok, and it must be the only code that is.
    /// </summary>
    [Fact]
    public void A_clean_whole_corpus_comparison_is_the_only_reading_that_is_Ok()
    {
        var result = BatchRunner.Execute(PlanFor(), new ScriptedRunner((_, _) => 0), Loads());

        Assert.All(result.Steps.Where(s => s.Step.Kind is BatchStepKind.WholeCorpusExport or BatchStepKind.WholeCorpusDrift),
            s => Assert.Equal(StepVerdict.Ok, s.Verdict));
        Assert.DoesNotContain("WHOLE-PROJECT", result.Headline, StringComparison.Ordinal);
    }
}
