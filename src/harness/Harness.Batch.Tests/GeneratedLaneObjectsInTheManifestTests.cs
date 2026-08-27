using Harness.Batch;
using Harness.Map;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b><c>LaneManifest</c> exists so <i>"how much of this lane is still hand-built"</i> is answerable —
/// and until the two lane generators had a production caller, the honest answer was ALL OF IT except the
/// copy layer.</b>
///
/// <para>This file drives <c>harness-batch manifest</c> over a binding that DECLARES its slot FC and its
/// stimulus head, and over one that declares neither. The two must be distinguishable in the emitted
/// document, in both directions: <b>a generated object must never be recorded as authored, and an authored
/// one must never be recorded as generated.</b> The second is the dangerous direction — it answers the
/// question this field exists for with a number nothing established.</para>
///
/// <para>Every head name here is INVENTED; see <c>Harness.Map.Tests.StimShellGeneratorTests</c>.</para>
/// </summary>
public sealed class GeneratedLaneObjectsInTheManifestTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lane-gen-manifest-" + Guid.NewGuid().ToString("N"));
    private readonly string _submission;
    private readonly string _ir;

    public GeneratedLaneObjectsInTheManifestTests()
    {
        Directory.CreateDirectory(_root);

        _submission = Path.Combine(_root, "sub.json");
        _ir = Path.Combine(_root, "ir");

        File.WriteAllText(_submission, "{}");

        Directory.CreateDirectory(_ir);
        File.WriteAllText(Path.Combine(_ir, "FB_WidgetUnderTest.ir"),
            "BLOCK FB FB_WidgetUnderTest\nNETWORK 1 \"drive\"\n  COIL Out := Go\n");
        File.WriteAllText(Path.Combine(_ir, "FB_WidgetStim.ir"),
            "BLOCK FB FB_WidgetStim\nNETWORK 1 \"index\"\n  COIL Running := Go\n");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private const string Preamble =
        "{ \"blockName\": \"FC_HarnessCopyLayer\", \"blockNumber\": 9001, \"tagTableName\": \"HarnessMirror\", "
        + "\"tagPrefix\": \"HX_\", \"baseByte\": 1000, \"retentiveBytes\": 256, \"declaredRegisters\": 576, "
        + "\"slots\": [{ \"slotId\": \"WIDGET\", \"startCondition\": \"Go\", "
        + "\"vectorTargets\": [{ \"tag\": \"In\", \"specName\": \"In\", \"type\": \"Int\" }], "
        + "\"resultSources\": [{ \"tag\": \"Out\", \"specName\": \"Out\", \"type\": \"Int\" }]";

    private const string Undeclared = Preamble + " }] }";

    /// <summary>
    /// 🔴 <b>THE WHOLE DECLARATION, AND ITS SIZE IS THE HEADLINE.</b> This section is what replaces two
    /// hand-authored artifacts: a slot FC, and the eighteen index-shell networks of a head.
    /// </summary>
    private const string Declared = Preamble + """
        , "generate": {
          "slotFcName": "FC_HarnessWidgetSlot",
          "slotFcNumber": 9010,
          "stimulusHead":   { "block": "FB_WidgetStim",      "instance": "iDB_WidgetStim",      "networkTitle": "Advance The Widget Stimulus Head" },
          "blockUnderTest": { "block": "FB_WidgetUnderTest", "instance": "iDB_WidgetUnderTest", "networkTitle": "Run The Widget Under Test" },
          "stimHead": {
            "headName": "FB_WidgetStim",
            "uutReset": "iDB_WidgetUnderTest.Cmd.FaultReset",
            "watchdog": "T#5M",
            "dwell": "T#200MS",
            "phases": [
              { "bit": "InHeadDisarm", "end": "HeadDisarmEnd", "duration": "DisarmDwell", "kind": "Disarm" },
              { "bit": "InHeadReset",  "end": "HeadResetEnd",  "duration": "ResetDwell",  "kind": "Reset" },
              { "bit": "InHeadVerify", "end": "HeadEnd",       "duration": "VerifyDwell", "kind": "Verify" },
              { "bit": "InScenario",   "end": "ScenEnd",       "duration": "Stim.EndAt",  "kind": "Scenario" },
              { "bit": "InTailDisarm", "end": "TailDisarmEnd", "duration": "DisarmDwell", "kind": "Disarm" },
              { "bit": "InTailReset",  "end": "TailResetEnd",  "duration": "ResetDwell",  "kind": "Reset" },
              { "bit": "InTailVerify", "end": "TailEnd",       "duration": "VerifyDwell", "kind": "Verify" }
            ],
            "causes": ["iDB_WidgetUnderTest.Status.JamLatched"],
            "cycleEdges": [],
            "outcomeBits": [
              "Stim.ArrivedDirty", "Stim.InertAtStart", "Stim.RecoverFailed",
              "Stim.InertAtEnd", "Stim.ScenarioDone"
            ]
          }
        } }] }
        """;

    private (int Exit, string Output, LaneManifest? Manifest) Produce(string bindingJson, string tag)
    {
        var binding = Path.Combine(_root, $"lane-{tag}.json");
        var emit = Path.Combine(_root, $"emit-{tag}");
        var outPath = Path.Combine(_root, $"manifest-{tag}.json");

        File.WriteAllText(binding, bindingJson);

        var writer = new StringWriter();
        var exit = BatchCli.Run(new[]
        {
            "manifest",
            "--lane", "widget",
            "--binding", binding,
            "--submission", _submission,
            "--program", _ir,
            "--emit", emit,
            "--out", outPath,
            "--block-under-test", "FB_WidgetUnderTest",
        }, writer, File.ReadAllText, File.WriteAllText, readBytes: File.ReadAllBytes);

        return (exit, writer.ToString(),
            File.Exists(outPath) ? LaneManifest.Read(outPath, File.ReadAllText) : null);
    }

    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_GENERATED_SLOT_FC_IS_RECORDED_AS_GENERATED_WITH_ITS_ROLE_and_the_file_exists()
    {
        var (exit, output, manifest) = Produce(Declared, "declared");

        Assert.Equal(BatchExit.Ok, exit);
        Assert.NotNull(manifest);

        var fc = Assert.Single(manifest!.Objects, o => o.Name == "FC_HarnessWidgetSlot");

        Assert.Equal(ObjectOrigin.Generated, fc.Origin);
        Assert.Equal(ObjectRole.SlotFc, fc.Role);
        Assert.Equal(HarnessObjectKind.Block, fc.Kind);
        Assert.True(File.Exists(fc.Path), fc.Path);

        Assert.Contains("(generated slot FC)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_GENERATED_SLOT_FC_CARRIES_A_RECORDED_HASH_because_it_IS_in_the_stamp()
    {
        // 🔴 THE COPY LAYER IS THE ONE OBJECT LEGITIMATELY CARRYING NO HASH — the stamp does not hash it,
        // because it embeds the stamp. A slot FC embeds nothing and executes, so a null hash here would
        // mean the manifest could not tell whether the deployed FC was still the one described.
        var manifest = Produce(Declared, "hash").Manifest!;

        var fc = Assert.Single(manifest.Objects, o => o.Name == "FC_HarnessWidgetSlot");
        var copyLayer = Assert.Single(manifest.Objects, o => o.Role == ObjectRole.CopyLayer && o.Kind == HarnessObjectKind.Block);

        Assert.NotNull(fc.Sha256);
        Assert.Null(copyLayer.Sha256);

        // And the manifest is TIED to the stamp: `Derive` refuses at birth if it is not.
        Assert.NotNull(manifest.Stamp);
    }

    [Fact]
    public void THE_STIMULUS_HEAD_ROLE_IS_DERIVED_FROM_THE_GENERATOR_rather_than_typed()
    {
        // §3.1 — a field that can be derived must never be typed. `SlotFcGenerator` is handed both the head
        // and the block under test and used to discard them; this is that derivation arriving, so
        // ObjectRole.StimulusHead stops being a value nothing ever sets.
        var manifest = Produce(Declared, "roles").Manifest!;

        var head = Assert.Single(manifest.Objects, o => o.Name == "FB_WidgetStim");
        Assert.Equal(ObjectRole.StimulusHead, head.Role);

        // ...and the origin of a block read off disk stays Unstated. The harness cannot know who wrote an
        // .ir file, and claiming Authored would be a guess in the other direction.
        Assert.Equal(ObjectOrigin.Unstated, head.Origin);

        Assert.Equal("FB_WidgetUnderTest", manifest.BlockUnderTest);
    }

    [Fact]
    public void THE_SHELL_FRAGMENT_IS_WRITTEN_AND_IS_DELIBERATELY_NOT_A_MANIFEST_OBJECT()
    {
        var (_, output, manifest) = Produce(Declared, "fragment");

        // It is NETWORKS, not a block: no header, no interface, no statics. Nothing can import it, so a
        // manifest naming it would put an unimportable file in the list the lane deploys from — and
        // `ProgramPaths` is exactly that list.
        Assert.DoesNotContain(manifest!.Objects, o => o.Path.Contains(StimShellFragment.Subdirectory, StringComparison.Ordinal));
        Assert.DoesNotContain(manifest.ProgramPaths, p => p.Contains(StimShellFragment.Subdirectory, StringComparison.Ordinal));

        var shellDir = Path.Combine(_root, "emit-fragment", StimShellFragment.Subdirectory);
        Assert.True(File.Exists(Path.Combine(shellDir, "FB_WidgetStim.networks.ir")));
        Assert.True(File.Exists(Path.Combine(shellDir, "FB_WidgetStim.requires.txt")));

        // It sits under a SUBDIRECTORY because `ManifestExpand` globs `*.ir` non-recursively — a fragment
        // beside the deployables would be loaded as a block.
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "emit-fragment"), "*.networks.ir"));

        Assert.Contains("A FRAGMENT, NOT A BLOCK", output, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_OBLIGATIONS_TRAVEL_WITH_THE_MANIFEST_because_a_generated_FC_nothing_calls_never_runs()
    {
        var manifest = Produce(Declared, "obligations").Manifest!;

        Assert.Contains(manifest.Obligations, o =>
            o.Contains("FC_HarnessWidgetSlot", StringComparison.Ordinal)
            && o.Contains("MUST be called from the cyclic OB", StringComparison.Ordinal));

        // The shell's own obligations too — chiefly the disarm lever and the scenario timeline, which the
        // generator states as OUTPUT rather than leaving in a README.
        Assert.Contains(manifest.Obligations, o => o.Contains("THE DISARM LEVER", StringComparison.Ordinal));

        // The head declares no counting edge, so the shell omits that network entirely and says so rather
        // than letting a vector assert on a bit that is never published.
        Assert.Contains(manifest.Obligations, o => o.Contains("NO COUNTING EDGES WERE DECLARED", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------
    // 🔴 THE NEGATIVE — a lane that declares nothing
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_LANE_THAT_DECLARES_NOTHING_STILL_PRODUCES_A_MANIFEST_and_claims_nothing_generated()
    {
        // *** THE CASE EVERY DEPLOYED LANE IS IN. *** It must keep working, and the ONE reading that must
        // never be available is an authored object counted as generated.
        var (exit, _, manifest) = Produce(Undeclared, "undeclared");

        Assert.Equal(BatchExit.Ok, exit);
        Assert.NotNull(manifest);

        Assert.DoesNotContain(manifest!.Objects, o => o.Name == "FC_HarnessWidgetSlot");

        // The only Generated rows are the copy layer's two — exactly what the harness generated before
        // this seam existed.
        Assert.All(manifest.Objects.Where(o => o.Origin == ObjectOrigin.Generated),
            o => Assert.Equal(ObjectRole.CopyLayer, o.Role));

        // And every object read off disk is Unstated, never Authored: the harness cannot tell who wrote an
        // .ir file, and saying so is the whole reason the third bucket exists.
        Assert.All(manifest.Objects.Where(o => o.Role != ObjectRole.CopyLayer),
            o => Assert.Equal(ObjectOrigin.Unstated, o.Origin));

        Assert.DoesNotContain(manifest.Objects, o => o.Role == ObjectRole.SlotFc);
        Assert.DoesNotContain(manifest.Objects, o => o.Role == ObjectRole.StimulusHead);
    }

    [Fact]
    public void ENQUEUE_COUNTS_THE_GENERATED_OBJECTS_and_the_two_lanes_report_different_numbers()
    {
        // The origin field's whole purpose, arriving at the surface a person reads. Before this, the
        // generated count was 2 on every lane that had ever existed.
        var declared = Produce(Declared, "enq-declared").Manifest!;
        var undeclared = Produce(Undeclared, "enq-undeclared").Manifest!;

        Assert.Equal(
            undeclared.Objects.Count(o => o.Origin == ObjectOrigin.Generated) + 1,
            declared.Objects.Count(o => o.Origin == ObjectOrigin.Generated));
    }
}
