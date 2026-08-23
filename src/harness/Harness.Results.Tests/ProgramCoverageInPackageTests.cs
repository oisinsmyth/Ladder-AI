using System.Text.Json;
using Harness.Map;
using Harness.Results;
using Harness.Wire;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>THE STAMP'S COVERAGE TRAVELS IN THE RESULT PACKAGE, BESIDE THE MANIFEST IT QUALIFIES.</b>
///
/// <para>*** MEASURED, <c>docs/18-project-workbench.md:821-829</c>. *** The last wave's stamp was derived
/// over <b>8 objects</b> and the parameter DB was not one of them, so <i>"compressing them changes the
/// controller without changing the stamp"</i>. <b>Two result packages describing materially different
/// programs would carry the same stamp, and the verifying gateway would not notice.</b> The package is the
/// artifact meant to OUTLIVE the run, so the count has to be IN it — a number printed to a terminal is gone
/// with the terminal, which is the same lesson that put the manifest here in the first place.</para>
///
/// <para><b>Absent is not empty, here as everywhere in this project.</b> A package with no
/// <c>Program</c> is one whose derivation recorded nothing; it is NOT a package whose stamp covered
/// everything, and the artifact must not let a consumer read the second out of the first.</para>
/// </summary>
public class ProgramCoverageInPackageTests
{
    private static readonly CopyLayerNaming Naming =
        new(BlockName: "FC_TheCopyLayer", BlockNumber: 900, TagTableName: "TheMirrorTable");

    private static readonly BuildStamp Build = new(0xA93F2C71);

    private static readonly AssertionEnumeration Enumeration =
        AssertionEnumeration.Of(new[] { "REQ-14" }, new[] { "REQ-14.a", "REQ-14.b" });

    private static RegisterMap Map() =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2),
            new[] { new SlotRequest("S0", 2, 2) })).Require();

    private static readonly ObservabilityReport Supportable = ObservabilityCheck.Evaluate(
        new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 0) },
        AssertionForm.When,
        MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })), 9, 1, 1);

    private static SlotRunResult Run() =>
        new(SlotOutcome.Completed, new ushort[] { 10, 1 }, new ScanCount(100), new ScanCount(140), 2, 8,
            new InertReport(InertOutcome.Established, new ScanCount(90), Array.Empty<ushort>(), Array.Empty<ushort>(), "established"),
            "stub",
            ObservationSeries.OfSingleFrame(new ushort[] { 10, 1 }, new ScanCount(140)));

    private static SlotBinding Binding() => new(
        "S0",
        MirroredSignal.Ints("DB_Unit.Setpoint"),
        "DB_Unit.StartCmd",
        MirroredSignal.Ints("DB_Unit.Actual"));

    private static RegisterMap StampMap() =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2),
            new[] { new SlotRequest("S0", 1, 1) })).Require();

    private static readonly string[] NineStaged =
    {
        "FB_Subject", "DB_Subject_iDB", "FC_SlotFc", "FB_StimHead", "DB_StimHead_iDB",
        "FB_Support", "DB_Support_iDB", "OB_Main", "DB_Params",
    };

    /// <summary>A real derivation, so the manifest and its coverage cannot disagree with each other.</summary>
    private static ProgramManifest Derived(StagedCorpus? corpus, params string[] program)
    {
        BuildStamp.Derive(
            StampMap(), new[] { Binding() }, Naming,
            program.Select(n => new HarnessObject(n, HarnessObjectKind.Block, $"BLOCK {n}\n")),
            corpus,
            out _, out var manifest);

        return manifest;
    }

    private static ResultPackage Package(ProgramManifest? program) =>
        ResultPackageBuilder.Build(
            new VectorDeclaration("V-1",
                new Basis("REQ-14", "REQ-14.a"),
                FidelityDeclaration.Of("M_Weigher", new[] { "fill-to-setpoint" }, new[] { "in-flight-mass" },
                    validatedAgainstPlantData: true, declaredBy: "agent-m"),
                new SettlingDeclaration("count unchanged across 3 consecutive scans", new[] { "Demo_Count" }),
                new[] { "fill-to-setpoint" },
                "Demo_Done",
                new AgentIdentity("agent-b"),
                new AgentIdentity("agent-a"),
                Supportable,
                BoundsCurrencyCheck.Evaluate("V-1",
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["fill_setpoint"] = "500" },
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["fill_setpoint"] = "500" },
                    AssertionBoundsExpectation.NotStated("this fixture declares a bound"))),
            Enumeration,
            Run(),
            slotIndex: 3,
            waveIndex: 7,
            new StimulusEvidence(true, true, 40, 8, ManifestPresence.Loaded,
                new VersionReport(VersionOutcome.Confirmed, Build.Value, Build.Value, 3, 1, "confirmed")),
            StimulusExpectation.AtLeastOneScanPerRoundTrip(8),
            new SettlingReport(SettlingState.Settled, "stated by a test fixture."),
            new[] { AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", "10") },
            new[] { 1, 4 },
            Map(),
            Build,
            slotsCoveredByOneRead: 1,
            program);

    private static JsonElement RoundTrip(ResultPackage package)
    {
        var text = ResultPackageJson.Of(package).ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    // ---------------------------------------------------------------------------------------------
    // the pair travels
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_PACKAGE_CARRIES_THE_MANIFEST_AND_ITS_COVERAGE_TOGETHER()
    {
        var package = Package(Derived(StagedCorpus.FromNames("lane 'vessel'", NineStaged), NineStaged));

        Assert.NotNull(package.Program);
        Assert.Equal(9, package.Program!.Objects.Count);
        Assert.Equal(9, package.Program!.Coverage!.Hashed);
        Assert.Equal(9, package.Program!.Coverage!.CorpusSize);
        Assert.True(package.Program!.Coverage!.Complete);
    }

    [Fact]
    public void AND_THE_SHORT_CASE_REACHES_THE_ARTIFACT_WITH_THE_MISSING_OBJECT_NAMED()
    {
        // 🔴 *** THE MEASURED DEFECT, END TO END THROUGH A REAL SERIALISE AND PARSE. *** A consumer reads
        // the FILE, never a rendering: anything the renderer drops is gone before a scraper sees it.
        var json = RoundTrip(Package(Derived(
            StagedCorpus.FromNames("lane 'vessel'", NineStaged),
            NineStaged.Where(n => n != "DB_Params").ToArray())));

        var program = json.GetProperty("programUnderTest");
        Assert.True(program.GetProperty("recorded").GetBoolean());

        var coverage = program.GetProperty("coverage");
        Assert.Equal(8, coverage.GetProperty("hashed").GetInt32());
        Assert.Equal(9, coverage.GetProperty("stagedCorpusSize").GetInt32());
        Assert.False(coverage.GetProperty("complete").GetBoolean());

        var gaps = coverage.GetProperty("presentAndNotHashed").EnumerateArray().Select(g => g.GetString()!).ToArray();
        Assert.Contains(gaps, g => g.Contains("DB_Params", StringComparison.Ordinal));

        Assert.Contains("8 of 9 object(s) in the staged corpus", coverage.GetProperty("line").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_LINE_IN_THE_ARTIFACT_CARRIES_THE_DEVICE_RESIDUAL()
    {
        // 🔴 The residual is not a comment in the source — it is IN the artifact, in the same sentence as
        // the number, because a reader of the file is the person most likely to mistake this for coverage
        // of the program.
        var json = RoundTrip(Package(Derived(StagedCorpus.FromNames("lane 'vessel'", NineStaged), NineStaged)));

        Assert.Contains(
            StampCoverage.DeviceResidual,
            json.GetProperty("programUnderTest").GetProperty("coverage").GetProperty("line").GetString()!,
            StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // the absences, each its own state
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void NO_MANIFEST_RECORDED_IS_SAID_NOT_INFERRED_FROM_AN_ABSENT_KEY()
    {
        // An absent key reads downstream as "there was none". This one says which kind of none it is.
        var json = RoundTrip(Package(program: null));

        var program = json.GetProperty("programUnderTest");
        Assert.False(program.GetProperty("recorded").GetBoolean());
        Assert.Equal(JsonValueKind.Null, program.GetProperty("coverage").ValueKind);
    }

    [Fact]
    public void HASHED_NOTHING_AND_HASHED_ZERO_OF_NINE_DO_NOT_RENDER_THE_SAME()
    {
        // 🔴 Three different facts, three different renderings: nobody recorded a manifest; a manifest that
        // hashed nothing with no denominator to judge it against; and NINE staged objects of which the
        // stamp covered NONE. Only the last is an accusation, and collapsing them would bury it.
        var notRecorded = RoundTrip(Package(program: null)).GetProperty("programUnderTest");
        var nothing = RoundTrip(Package(Derived(corpus: null))).GetProperty("programUnderTest");
        var zeroOfNine = RoundTrip(Package(Derived(StagedCorpus.FromNames("lane 'vessel'", NineStaged))))
            .GetProperty("programUnderTest");

        Assert.False(notRecorded.GetProperty("recorded").GetBoolean());

        Assert.True(nothing.GetProperty("recorded").GetBoolean());
        Assert.True(nothing.GetProperty("hashedNothing").GetBoolean());
        Assert.Equal(JsonValueKind.Null, nothing.GetProperty("coverage").GetProperty("stagedCorpusSize").ValueKind);
        Assert.Contains("NO STAGED CORPUS WAS SUPPLIED",
            nothing.GetProperty("coverage").GetProperty("line").GetString()!, StringComparison.Ordinal);

        Assert.True(zeroOfNine.GetProperty("recorded").GetBoolean());
        Assert.True(zeroOfNine.GetProperty("hashedNothing").GetBoolean());
        Assert.Equal(9, zeroOfNine.GetProperty("coverage").GetProperty("stagedCorpusSize").GetInt32());
        Assert.Equal(9, zeroOfNine.GetProperty("coverage").GetProperty("presentAndNotHashed").GetArrayLength());
        Assert.Contains("0 of 9 object(s) in the staged corpus",
            zeroOfNine.GetProperty("coverage").GetProperty("line").GetString()!, StringComparison.Ordinal);

        Assert.NotEqual(
            nothing.GetProperty("coverage").GetProperty("line").GetString(),
            zeroOfNine.GetProperty("coverage").GetProperty("line").GetString());
    }

    // ---------------------------------------------------------------------------------------------
    // a gap is a caveat on the result, because that is what a caveat IS
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_STAGED_OBJECT_THE_STAMP_DID_NOT_COVER_IS_A_CAVEAT_ON_THE_RESULT()
    {
        // The validity stamp's whole claim is "a program hashing to this was executing". An object the
        // stamp does not cover can change under it without moving it, so the claim is weaker than it
        // reads — and a caveat somebody has to go and look up is a caveat nobody reads.
        var package = Package(Derived(
            StagedCorpus.FromNames("lane 'vessel'", NineStaged),
            NineStaged.Where(n => n != "DB_Params").ToArray()));

        var caveat = package.Stamp.Caveats.Single(c => c.Contains("DB_Params", StringComparison.Ordinal));
        Assert.Contains("8 of 9", caveat, StringComparison.Ordinal);
    }

    [Fact]
    public void NO_CORPUS_IS_ALSO_A_CAVEAT_BECAUSE_AN_UNMEASURED_DENOMINATOR_IS_NOT_A_CLEAN_ONE()
    {
        var package = Package(Derived(corpus: null, "FB_Subject"));

        Assert.Contains(package.Stamp.Caveats, c => c.Contains("NO STAGED CORPUS", StringComparison.Ordinal));
    }

    [Fact]
    public void AND_A_COMPLETE_COVERAGE_ADDS_NO_CAVEAT()
    {
        // A gate that fires outside its scope is noise, and noise gets switched off.
        var package = Package(Derived(StagedCorpus.FromNames("lane 'vessel'", NineStaged), NineStaged));

        Assert.DoesNotContain(package.Stamp.Caveats, c => c.Contains("staged corpus", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_PACKAGE_WITH_NO_MANIFEST_KEEPS_EXACTLY_THE_CAVEATS_IT_ALWAYS_HAD()
    {
        // 🔴 The absent case adds NOTHING. Every package built before this existed passes `null`, and a new
        // always-on caveat would say the same sentence on every result ever produced — which is how a
        // caveat list stops being read at all. The absence is visible as `recorded: false` in the artifact.
        Assert.DoesNotContain(Package(program: null).Stamp.Caveats,
            c => c.Contains("staged corpus", StringComparison.OrdinalIgnoreCase));
    }
}
