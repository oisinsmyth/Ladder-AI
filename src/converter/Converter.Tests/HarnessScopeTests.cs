using Converter.Ir;
using Converter.Review;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 2026-08-13. A generated test-harness copy layer drew 20+ C-001 findings and a C-201 on every
/// `converter review` run. The owner ruled that doc 06's naming conventions govern PLC program
/// content authored FOR THE PLANT, and a harness-generated object is neither plant nor
/// hand-authored — so those findings were correct against the letter of the rule and wrong about
/// their subject.
///
/// <para>*** BOTH FAILURE DIRECTIONS ARE LIVE HERE, AND THIS FILE TESTS BOTH. ***</para>
/// <list type="number">
/// <item>Twenty standing findings a run is how a reviewer learns to skim — an over-firing gate
/// decays into a warning.</item>
/// <item>A silent exemption and a correct pass are indistinguishable — and a classifier that
/// classified EVERYTHING as harness would make every other test in this file pass while checking
/// nothing. *** THE SECOND IS THE DANGEROUS ONE ***, so the plant-side assertions below are not
/// courtesy coverage: they are the tests that can fail when the classifier is wrong.</item>
/// </list>
///
/// <para>The question asked throughout is "COULD ANY INPUT REACH THIS LINE?", not "is this line
/// called?" — so every derivation is exercised with an input that must NOT trip it as well as one
/// that must.</para>
/// </summary>
public class HarnessScopeTests : IDisposable
{
    private readonly List<string> _temp = new();
    private readonly string _dir;

    public HarnessScopeTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"harness-scope-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        foreach (var path in _temp)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
        }

        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        _temp.Add(path);
        return path;
    }

    // A copy-layer-shaped FC: writes three underscore-bearing tags, carries no header comment. Both
    // C-001 (the tags, via the table) and C-201 (this block) therefore have real subjects.
    private static IrBlock CopyLayer(string name, int number, string kind = "FC") =>
        new("0", kind, name, number, "LAD", null, new[]
        {
            new IrNetwork(1, "Copy in", new[]
            {
                new CoilAssignment("HX_Slot_Start", new Expr.TagRef("HX_Slot_Cmd")),
                new CoilAssignment("HX_Slot_Ran", new Expr.TagRef("HX_Slot_Echo")),
            }),
        });

    private static string Serialize(IrBlock block) =>
        IrSerializer.SerializeBlock(block, new[]
        {
            new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()),
        });

    private static PlcTagTableSource MirrorTable(string tableName) =>
        new("0", tableName, new[]
        {
            new PlcTagSource("1", "HX_Slot_Start", "Bool", "%M1009.0", true, true, true, null),
            new PlcTagSource("2", "HX_Slot_Cmd", "Bool", "%M1010.0", true, true, true, null),
            new PlcTagSource("3", "HX_Slot_Ran", "Bool", "%M1011.0", true, true, true, null),
            new PlcTagSource("4", "HX_Slot_Echo", "Bool", "%M1012.0", true, true, true, null),
        });

    // ---- The number band, both directions -------------------------------------------------------

    [Theory]
    [InlineData("FC", 9000)]
    [InlineData("FC", 9001)]
    [InlineData("FB", 9999)]
    public void ClassifyBlock_InsideReservedBand_IsHarness(string kind, int number)
    {
        var verdict = HarnessScope.ClassifyBlock(CopyLayer("FC_X", number, kind));
        Assert.Equal(HarnessClass.Harness, verdict.Class);
        Assert.Contains("9000-9999", verdict.Basis);
    }

    // *** THE DID-NOT-RUN / RAN-TOO-FAR TEST FOR THE BAND. *** Without it, a classifier that
    // returned Harness for everything would satisfy every other case in this file.
    [Theory]
    [InlineData("FC", 1)]
    [InlineData("FC", 8999)]
    [InlineData("FB", 10000)]
    [InlineData("FB", 900)]
    public void ClassifyBlock_OutsideReservedBand_IsPlant(string kind, int number)
    {
        var verdict = HarnessScope.ClassifyBlock(CopyLayer("FC_X", number, kind));
        Assert.Equal(HarnessClass.Plant, verdict.Class);
    }

    // The carve-out. An OB's number is FIXED BY ITS EVENT CLASS, so the band cannot classify one in
    // EITHER direction: OB80 is a harness object the band would call plant, and an OB numbered in the
    // 9000s is not thereby harness. Unclassified is the honest answer and it gates.
    [Theory]
    [InlineData(1)]
    [InlineData(80)]
    [InlineData(9001)]
    public void ClassifyBlock_Ob_IsUnclassifiedWhateverItsNumber(int number)
    {
        var verdict = HarnessScope.ClassifyBlock(CopyLayer("Main", number, "OB"));
        Assert.Equal(HarnessClass.Unclassified, verdict.Class);
        Assert.Contains("event class", verdict.Basis);
    }

    [Fact]
    public void ClassifyType_AlwaysUnclassified_BecauseAUdtHasNoDerivableProperty()
    {
        var verdict = HarnessScope.ClassifyType(new PlcTypeSource("0", "UDT_Thing", null, Array.Empty<DbMember>()));
        Assert.Equal(HarnessClass.Unclassified, verdict.Class);
    }

    // ---- The tag table: the hard case ------------------------------------------------------------

    // *** THE LAUNDERING TEST, AND IT IS THE POINT OF THE WHOLE DESIGN. ***
    // A tag table has no number, so the only table-level property is its NAME — and a name is what
    // anything can be renamed into. Here a PLANT tag table is given the most harness-looking name
    // available AND `HX_`-prefixed tags, and its tags are referenced by a plant FC. Every finding
    // must still gate. If this test ever goes green while reporting an exemption, the classifier has
    // started reading names.
    [Fact]
    public void PlantTagTableRenamedToLookLikeHarness_StillGates()
    {
        var plantCaller = Write("plant.ir", Serialize(CopyLayer("FC_PlantCopy", 42)));
        var table = Write("tags.ir", PlcTagTableIrWriterForTest(MirrorTable("HarnessMirror")));

        var scope = HarnessScope.Build(new[] { plantCaller, table });
        var report = ReviewRunner.ReviewFiles(new[] { table }, ignoreErrors: false, null, scope);

        var file = Assert.Single(report.Files);
        Assert.Equal(HarnessClass.Plant, file.Harness!.Class);
        Assert.Empty(file.HarnessScopedFindings);
        Assert.NotEmpty(file.Findings.Where(f => f.RuleId == "C-001"));
        Assert.Equal(ReviewOutcome.Findings, ReviewOutcome.ExitCode(report, allowUnchecked: false));
    }

    // The positive case: identical table, identical tag names — the ONLY difference is that the
    // block referencing them is numbered inside the reserved band.
    [Fact]
    public void SameTableReferencedOnlyByAHarnessBlock_IsHarnessScoped()
    {
        var harnessCaller = Write("harness.ir", Serialize(CopyLayer("FC_HarnessCopy", 9001)));
        var table = Write("tags.ir", PlcTagTableIrWriterForTest(MirrorTable("HarnessMirror")));

        var scope = HarnessScope.Build(new[] { harnessCaller, table });
        var report = ReviewRunner.ReviewFiles(new[] { table }, ignoreErrors: false, null, scope);

        var file = Assert.Single(report.Files);
        Assert.Equal(HarnessClass.Harness, file.Harness!.Class);
        Assert.NotEmpty(file.HarnessScopedFindings);
        Assert.All(file.HarnessScopedFindings, f => Assert.Equal("C-001", f.RuleId));
        Assert.Empty(file.Findings.Where(f => f.RuleId == "C-001"));
        Assert.Equal(ReviewOutcome.Clean, ReviewOutcome.ExitCode(report, allowUnchecked: false));
    }

    // *** ONE PLANT REFERRER IS ENOUGH. *** The dangerous direction: a tag read by both a harness
    // block and a plant block is PLANT. Without this, a mirror that a plant rung happened to consume
    // would be exempted, and the exemption would be invisible.
    [Fact]
    public void TagReferencedByBothHarnessAndPlant_IsPlant()
    {
        var harnessCaller = Write("harness.ir", Serialize(CopyLayer("FC_HarnessCopy", 9001)));
        var plantCaller = Write("plant.ir", Serialize(CopyLayer("FC_PlantCopy", 42)));
        var table = Write("tags.ir", PlcTagTableIrWriterForTest(MirrorTable("HarnessMirror")));

        var scope = HarnessScope.Build(new[] { harnessCaller, plantCaller, table });
        var report = ReviewRunner.ReviewFiles(new[] { table }, ignoreErrors: false, null, scope);

        var file = Assert.Single(report.Files);
        Assert.Empty(Assert.Single(report.Files).HarnessScopedFindings);
        Assert.NotEmpty(file.Findings.Where(f => f.RuleId == "C-001"));
    }

    // No corpus at all — the DEFAULT. A caller that forgets to build a scope must get the full
    // pre-2026-08-13 finding set, never a bypass.
    [Fact]
    public void TagTableReviewedWithNoScope_IsUnclassifiedAndGates()
    {
        var table = Write("tags.ir", PlcTagTableIrWriterForTest(MirrorTable("HarnessMirror")));

        var report = ReviewRunner.ReviewFiles(new[] { table }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        Assert.Equal(HarnessClass.Unclassified, file.Harness!.Class);
        Assert.Empty(file.HarnessScopedFindings);
        Assert.NotEmpty(file.Findings.Where(f => f.RuleId == "C-001"));
    }

    // A tag nobody references is not thereby harness — the "empty is not clean" shape, applied to the
    // referrer relation.
    [Fact]
    public void TagWithNoReferrerAtAll_IsUnclassified()
    {
        var harnessCaller = Write("harness.ir", Serialize(CopyLayer("FC_HarnessCopy", 9001)));
        var scope = HarnessScope.Build(new[] { harnessCaller });

        var verdict = scope.ClassifyTag("HX_NobodyReadsThis");
        Assert.Equal(HarnessClass.Unclassified, verdict.Class);
        Assert.Contains("referenced by NO block", verdict.Basis);
    }

    // *** THE PARTIAL-CORPUS FENCE. *** Every KNOWN referrer is a harness block, but one corpus file
    // could not be parsed — and an unread file could hold the plant reference that changes the
    // answer. Refuse rather than mis-classify.
    [Fact]
    public void UnparseableCorpusFile_BlocksAnyHarnessClassificationOfTags()
    {
        var harnessCaller = Write("harness.ir", Serialize(CopyLayer("FC_HarnessCopy", 9001)));
        var broken = Write("broken.ir", "BLOCK FC FC_Broken\nthis is not IR\n");

        var scope = HarnessScope.Build(new[] { harnessCaller, broken });

        Assert.NotEmpty(scope.UnreadableCorpusFiles);
        var verdict = scope.ClassifyTag("HX_Slot_Start");
        Assert.Equal(HarnessClass.Unclassified, verdict.Class);
        Assert.Contains("could not be parsed", verdict.Basis);
    }

    // Same corpus minus the broken file: the fence is the only thing that changed the answer, so the
    // test above is measuring the fence and not some unrelated refusal.
    [Fact]
    public void SameCorpusWithoutTheUnparseableFile_ClassifiesHarness()
    {
        var harnessCaller = Write("harness.ir", Serialize(CopyLayer("FC_HarnessCopy", 9001)));

        var scope = HarnessScope.Build(new[] { harnessCaller });

        Assert.Empty(scope.UnreadableCorpusFiles);
        Assert.Equal(HarnessClass.Harness, scope.ClassifyTag("HX_Slot_Start").Class);
    }

    // ---- Scope: C-001 and C-201 only -------------------------------------------------------------

    // *** C-103 STAYS A FINDING ON A HARNESS BLOCK. *** It is behaviour, not naming, and it was
    // recorded rather than silenced. A classifier that exempted "everything on a harness object"
    // fails here.
    [Fact]
    public void HarnessBlock_NonNamingRuleStillGates()
    {
        var block = new IrBlock("0", "FC", "FC_HarnessCopy", 9001, "LAD", null, new[]
        {
            new IrNetwork(1, "Set with no reset", new[]
            {
                new CoilAssignment("HX_Slot_Ran", new Expr.TagRef("HX_Slot_Echo"), CoilKind.Set),
            }),
        });
        var path = Write("harness-set.ir", Serialize(block));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        Assert.Equal(HarnessClass.Harness, file.Harness!.Class);
        Assert.Contains(file.Findings, f => f.RuleId == "C-103");
        Assert.DoesNotContain(file.HarnessScopedFindings, f => f.RuleId == "C-103");
    }

    [Fact]
    public void HarnessBlock_C201HeaderCommentIsReportedNotGated()
    {
        var path = Write("harness.ir", Serialize(CopyLayer("FC_HarnessCopy", 9001)));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        Assert.Contains(file.HarnessScopedFindings, f => f.RuleId == "C-201");
        Assert.DoesNotContain(file.Findings, f => f.RuleId == "C-201");
    }

    // *** THE DID-NOT-RUN TEST FOR THE WHOLE PATH. *** The SAME block, the SAME C-201 defect, one
    // number changed. Without this, a scope that classified everything as harness would leave every
    // other assertion in this file green.
    [Fact]
    public void PlantBlock_SameDefects_StillDrawsItsC201FindingThroughTheSamePath()
    {
        var path = Write("plant.ir", Serialize(CopyLayer("FC_PlantCopy", 42)));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        Assert.Equal(HarnessClass.Plant, file.Harness!.Class);
        Assert.Contains(file.Findings, f => f.RuleId == "C-201");
        Assert.Empty(file.HarnessScopedFindings);
        Assert.Equal(ReviewOutcome.Findings, ReviewOutcome.ExitCode(report, allowUnchecked: false));
    }

    // A harness block's own interface member must not vouch for an identically-named plant tag.
    [Fact]
    public void BlockOwnInterfaceMemberNames_AreNotCountedAsTagReferences()
    {
        var block = new IrBlock("0", "FC", "FC_HarnessCopy", 9001, "LAD", null, new[]
        {
            new IrNetwork(1, "Local only", new[] { new CoilAssignment("Out_Flag", new Expr.TagRef("In_Flag")) }),
        }, InputMembers: new[] { new DbMember("In_Flag", "Bool", false, null) }, OutputMembers: new[] { new DbMember("Out_Flag", "Bool", false, null) });
        var path = Write("harness-iface.ir", Serialize(block));

        var scope = HarnessScope.Build(new[] { path });

        Assert.Equal(HarnessClass.Unclassified, scope.ClassifyTag("In_Flag").Class);
        Assert.Equal(HarnessClass.Unclassified, scope.ClassifyTag("Out_Flag").Class);
    }

    // ---- The report must SAY SO -------------------------------------------------------------------

    // A count of zero is a different fact from an absent section: the line is present on a run with
    // no harness content at all.
    [Fact]
    public void Report_AlwaysStatesTheHarnessScopeCount_EvenAtZero()
    {
        var path = Write("plant.ir", Serialize(CopyLayer("FC_PlantCopy", 42)));

        var text = ReviewOutputFormatter.FormatTable(ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false));

        Assert.Contains("HARNESS-SCOPE: 0 finding(s) reported and NOT gated", text);
    }

    // The verdict and its derivation are printed for EVERY file, plant ones included — which is what
    // would make a classifier that silently stopped running visible.
    [Fact]
    public void Report_PrintsTheScopeVerdictAndItsBasisForPlantContentToo()
    {
        var path = Write("plant.ir", Serialize(CopyLayer("FC_PlantCopy", 42)));

        var text = ReviewOutputFormatter.FormatTable(ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false));

        Assert.Contains("SCOPE: plant content", text);
        Assert.Contains("outside the reserved harness band 9000-9999", text);
    }

    [Fact]
    public void Report_HarnessFindingsArePrintedNotDropped()
    {
        var path = Write("harness.ir", Serialize(CopyLayer("FC_HarnessCopy", 9001)));

        var text = ReviewOutputFormatter.FormatTable(ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false));

        Assert.Contains("SCOPE: HARNESS-GENERATED", text);
        Assert.Contains("HARNESS-SCOPE (reported, NOT gating", text);
        Assert.Contains("has no header comment", text);
    }

    // Serializing a tag table to its .ir text — the review runner reads by path, and there is no
    // public writer helper elsewhere in the test project.
    private static string PlcTagTableIrWriterForTest(PlcTagTableSource table) =>
        TagTableIrSerializer.Serialize(table);
}
