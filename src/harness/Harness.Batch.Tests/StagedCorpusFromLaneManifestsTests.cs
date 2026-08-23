using Harness.Batch;
using Harness.Map;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>THE DENOMINATOR REACHES THE RUN — the half of Phase 6 Y3 that was deliberately left open.</b>
///
/// <para><see cref="StagedCorpus"/> and <c>StampCoverage</c> landed able to report <i>hashed n of m</i>
/// and NOTHING SUPPLIED A CORPUS, so every real run printed <c>NO STAGED CORPUS WAS SUPPLIED</c> — the
/// honest state, and not a closed item. These tests are the wiring: a lane manifest is the source, the
/// projection is the caller's job, and the corpus reaches <c>harness-run</c> on the command line the same
/// way the program union already does.</para>
///
/// <para><b>The layering is asserted by construction rather than by comment.</b> <c>Harness.Map</c> holds
/// zero project references and <c>Harness.Batch</c> depends on IT, so the arrow cannot be reversed:
/// <see cref="StagedCorpus"/> never sees <see cref="LaneManifest"/>, and this assembly does the
/// projection.</para>
///
/// <para>🔴 <b>AND THE HONEST CASE STAYS REACHABLE.</b> A lane enqueued with a bare <c>--program</c> list
/// emits NO corpus rows, so the batch passes no denominator and the run says so. Making the no-corpus
/// sentence unreachable while wiring the happy one would convert a loud absence into a silent assumption,
/// which is the exact failure this whole item is about.</para>
/// </summary>
public sealed class StagedCorpusFromLaneManifestsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "staged-corpus-" + Guid.NewGuid().ToString("N"));

    public StagedCorpusFromLaneManifestsTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>A lane manifest for <paramref name="lane"/> naming <paramref name="objects"/>.</summary>
    private string Manifest(string lane, params string[] objects)
    {
        var manifest = new LaneManifest(
            lane,
            objects.Select(o => new ManifestObject(o, Path.Combine(_root, lane + "-ir"), ObjectOrigin.Generated)).ToArray(),
            Array.Empty<string>());

        return Write(lane + "-manifest.json", manifest.ToJson());
    }

    private (int Exit, string Output) Enqueue(string queue, string lane, params string[] extra)
    {
        var writer = new StringWriter();
        var args = new List<string>
        {
            "enqueue", "--queue", queue,
            "--lane", lane,
            "--binding", Write(lane + "-binding.json", "{}"),
            "--submission", Write(lane + "-submission.json", "{}"),
        };
        args.AddRange(extra);

        return (BatchCli.Run(args.ToArray(), writer, File.ReadAllText, (p, c) => File.WriteAllText(p, c)), writer.ToString());
    }

    // ---------------------------------------------------------------------------------------------
    // 1. The projection: a manifest becomes corpus rows, and the LANE names the document to go and fix.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>Enqueueing with <c>--manifest</c> records what the lane STAGED, not only where its IR is.</b>
    /// <c>ProgramPaths</c> is the numerator's input; the object NAMES are the denominator, and they were
    /// read and thrown away.
    /// </summary>
    [Fact]
    public void ENQUEUE_RECORDS_THE_MANIFESTS_STAGED_OBJECTS_on_the_lane()
    {
        var queue = Path.Combine(_root, "q1");
        var (exit, _) = Enqueue(queue, "vessel", "--manifest", Manifest("vessel", "FB_Subject", "DB_Subject_iDB", "DB_Params"));

        Assert.Equal(BatchExit.Ok, exit);

        var lane = Assert.Single(new LaneQueue(queue).All());
        Assert.Equal(new[] { "FB_Subject", "DB_Subject_iDB", "DB_Params" }, lane.Staged);
    }

    /// <summary>
    /// A lane enqueued from a bare <c>--program</c> list stages nothing it can NAME. <b>Empty, never
    /// invented</b> — the paths are files, and a file is not an object name TIA matches an import on.
    /// </summary>
    [Fact]
    public void A_LANE_WITH_NO_MANIFEST_RECORDS_NO_STAGED_OBJECTS()
    {
        var queue = Path.Combine(_root, "q2");
        var (exit, _) = Enqueue(queue, "valve", "--program", Path.Combine(_root, "valve-ir"));

        Assert.Equal(BatchExit.Ok, exit);
        Assert.Empty(Assert.Single(new LaneQueue(queue).All()).Staged);
    }

    /// <summary>
    /// 🔴 <b>An object two lanes both stage keeps BOTH sources.</b> One lane's copy silently winning would
    /// make a gap report name the wrong document to go and fix — which is the whole reason
    /// <see cref="StagedCorpus.Union"/> merges sources rather than overwriting them.
    /// </summary>
    [Fact]
    public void THE_UNION_KEEPS_BOTH_LANES_AS_THE_SOURCE_of_a_shared_object()
    {
        var lanes = new[]
        {
            new Lane("valve", "b", "s", new[] { "ir" }, StagedObjects: new[] { "FB_Shared", "FB_Valve" }),
            new Lane("vessel", "b", "s", new[] { "ir" }, StagedObjects: new[] { "FB_Shared", "FB_Vessel" }),
        };

        var fact = LaneCorpus.Of(lanes);
        var corpus = fact.Corpus!;

        Assert.True(fact.Stated);
        Assert.Equal(3, corpus.Count);

        var shared = corpus.Entries.Single(e => e.Name == "FB_Shared");
        Assert.Contains("lane 'valve'", shared.Source, StringComparison.Ordinal);
        Assert.Contains("lane 'vessel'", shared.Source, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>NULL, not an empty corpus.</b> An empty one would make every run read <i>"hashed n of 0"</i>
    /// over an empty gap list — the shape of a check that examined nothing — and <see cref="StagedCorpus"/>
    /// refuses it at construction for exactly that reason.
    /// </summary>
    [Fact]
    public void NO_LANE_STAGING_ANYTHING_IS_NULL_never_an_empty_corpus()
    {
        Assert.False(LaneCorpus.Of(new[] { new Lane("valve", "b", "s", new[] { "ir" }) }).Stated);
        Assert.False(LaneCorpus.Of(Array.Empty<Lane>()).Stated);
    }

    /// <summary>
    /// 🔴 <b>A PARTIAL DENOMINATOR IS REFUSED, AND THIS IS THE SUBTLE ONE.</b>
    ///
    /// <para>The lanes share ONE deployment. A union over only the lanes that happen to declare a manifest
    /// would publish a number SHORTER than what gets downloaded — <b>which is the defect this whole
    /// mechanism measures</b>, reproduced one level up: <i>"6 of 9"</i> that reads complete for a batch
    /// staging more than nine. So the batch says NO DENOMINATOR, and NAMES the lanes that owe a manifest
    /// rather than leaving a reader to work out why the number is missing.</para>
    /// </summary>
    [Fact]
    public void A_BATCH_WHERE_ONLY_SOME_LANES_HAVE_A_MANIFEST_PUBLISHES_NO_DENOMINATOR()
    {
        var fact = LaneCorpus.Of(new[]
        {
            new Lane("vessel", "b", "s", new[] { "ir" }, StagedObjects: new[] { "FB_Vessel" }),
            new Lane("valve", "b", "s", new[] { "ir" }),
        });

        Assert.False(fact.Stated);
        Assert.Contains("NO DENOMINATOR", fact.Detail, StringComparison.Ordinal);

        // The lane to go and fix, by name. A refusal that does not say which document is missing is a
        // refusal nobody can close.
        Assert.Contains("valve", fact.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("vessel)", fact.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 2. The wiring: the corpus reaches every harness-run the batch plans.
    // ---------------------------------------------------------------------------------------------

    private static BatchRunOptions Options() => new(
        ConverterExe: @"C:\bin\converter.exe",
        OpennessCliExe: @"C:\bin\openness-cli.exe",
        HarnessRunExe: @"C:\bin\harness-run.exe",
        LeasesDirectory: @"C:\leases",
        PortalProject: @"C:\projects\Rig\Rig.ap20",
        RigAddress: "10.10.10.10",
        Holder: "agent-a",
        HolderPid: 4242,
        StagingDirectory: @"C:\staging",
        MergedBindingPath: @"C:\staging\merged.json",
        PortalEvidencePath: @"C:\staging\portal.json");

    private static BatchRunPlan PlanFor(params Lane[] lanes)
    {
        var names = lanes.Select(l => l.Name).ToArray();
        var batch = new BatchPlanResult(lanes.Length, names, Array.Empty<string>(), "{}", null,
            lanes.SelectMany(l => l.ProgramPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

        return BatchRunPlan.For(batch, lanes, Options());
    }

    /// <summary>
    /// 🔴 <b>THE ACCEPTANCE SHAPE: every <c>harness-run</c> the batch plans carries the denominator.</b>
    ///
    /// <para>Both the GENERATE step and every WAVE step, and for the same reason the program UNION goes to
    /// both: a step that measured its coverage against a different denominator would report a different
    /// gap from the deployment it belongs to.</para>
    /// </summary>
    [Fact]
    public void EVERY_HARNESS_RUN_STEP_CARRIES_THE_STAGED_CORPUS()
    {
        var plan = PlanFor(
            new Lane("valve", "b", "s", new[] { @"valve\ir" }, StagedObjects: new[] { "FB_Valve", "DB_Params" }),
            new Lane("vessel", "b", "s", new[] { @"vessel\ir" }, StagedObjects: new[] { "FB_Vessel" }));

        var runs = plan.Steps
            .Where(s => s.Kind is BatchStepKind.Generate or BatchStepKind.Wave)
            .ToArray();

        // One generate, two waves. A test that asserted over an empty set would pass for the wrong reason.
        Assert.Equal(3, runs.Length);

        foreach (var step in runs)
        {
            var staged = Staged(step);

            Assert.Equal(3, staged.Count);
            Assert.Contains("FB_Valve=lane 'valve'", staged);
            Assert.Contains("DB_Params=lane 'valve'", staged);
            Assert.Contains("FB_Vessel=lane 'vessel'", staged);
        }
    }

    /// <summary>
    /// 🔴 <b>THE HONEST CASE, PRESERVED.</b> No lane staged anything nameable, so NOTHING is passed and
    /// the run says <c>NO STAGED CORPUS WAS SUPPLIED</c>. An invented denominator here would be the silent
    /// assumption this item exists to prevent.
    /// </summary>
    [Fact]
    public void A_BATCH_WHOSE_LANES_HAVE_NO_MANIFEST_PASSES_NO_DENOMINATOR()
    {
        var plan = PlanFor(new Lane("valve", "b", "s", new[] { @"valve\ir" }));

        foreach (var step in plan.Steps.Where(s => s.Kind is BatchStepKind.Generate or BatchStepKind.Wave))
            Assert.DoesNotContain("--staged", step.Arguments);
    }

    /// <summary>And the partial case reaches the command line as the same nothing, for the same reason.</summary>
    [Fact]
    public void A_PARTIALLY_MANIFESTED_BATCH_PASSES_NO_DENOMINATOR_EITHER()
    {
        var plan = PlanFor(
            new Lane("vessel", "b", "s", new[] { @"vessel\ir" }, StagedObjects: new[] { "FB_Vessel" }),
            new Lane("valve", "b", "s", new[] { @"valve\ir" }));

        foreach (var step in plan.Steps.Where(s => s.Kind is BatchStepKind.Generate or BatchStepKind.Wave))
            Assert.DoesNotContain("--staged", step.Arguments);
    }

    /// <summary>
    /// The values are one token each, so a source label containing a space survives the argument vector
    /// intact. <c>lane 'vessel'</c> has one, and a row whose source split into two tokens would name half
    /// a document.
    /// </summary>
    [Fact]
    public void A_SOURCE_LABEL_WITH_A_SPACE_IS_ONE_ARGUMENT()
    {
        var plan = PlanFor(new Lane("vessel", "b", "s", new[] { "ir" }, StagedObjects: new[] { "FB_Vessel" }));
        var step = plan.Steps.First(s => s.Kind == BatchStepKind.Wave);

        Assert.Equal(new[] { "FB_Vessel=lane 'vessel'" }, Staged(step));
    }

    private static IReadOnlyList<string> Staged(BatchStep step)
    {
        var values = new List<string>();

        for (var i = 0; i < step.Arguments.Count - 1; i++)
        {
            if (string.Equals(step.Arguments[i], "--staged", StringComparison.Ordinal))
                values.Add(step.Arguments[i + 1]);
        }

        return values;
    }
}
