using Converter.Ir;
using Converter.Preflight;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-13 (2026-07-16): `converter preflight` — static checks in front of the Portal round trip.
/// Each test builds a real on-disk "project dir" of .ir files plus target files, the same way the
/// command runs. True-positive fixtures per check (unresolved tag, missing callee, missing
/// INSTANCEOF) and true negatives proving resolution across locals, project files, and the batch.
/// </summary>
public class PreflightTests : IDisposable
{
    private readonly string _projectDir;
    private readonly List<string> _tempPaths = new();

    public PreflightTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), $"preflight-proj-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_projectDir, recursive: true);
        }
        catch (IOException)
        {
        }

        foreach (var path in _tempPaths)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
        }
    }

    private string WriteProjectFile(string fileName, string content)
    {
        var path = Path.Combine(_projectDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private string WriteBatchFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"preflight-batch-{Guid.NewGuid():N}.ir");
        File.WriteAllText(path, content);
        _tempPaths.Add(path);
        return path;
    }

    // Placeholder sidecars parse but do NOT convert (FlgNetBuilder correctly reports the
    // IR-vs-sidecar mismatch as a convert finding) — used only where convert findings are not
    // under test.
    private static string SerializeBlock(IrBlock block)
    {
        var sidecars = block.Networks
            .Select(n => new NetworkSidecar(n.Number, n.Number.ToString(), Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();
        return IrSerializer.SerializeBlock(block, sidecars);
    }

    // Real, consistent sidecars minted by the synthesizer — genuinely convertible content
    // (plain coil chains only, the synthesizer's own supported slice).
    private static string SerializeSynthesizable(IrBlock block) =>
        IrSerializer.SerializeBlock(block, SidecarSynthesizer.SynthesizeBlock(block));

    private static IrBlock CleanBlock(string coilConditionTag) => new(
        "0", "FC", "FC_Preflight", 40, "LAD", "Header.", new[]
        {
            new IrNetwork(1, "Wiring", new[]
            {
                new CoilAssignment("DB_Marks.Run", new Expr.And(new Expr[] { new Expr.TagRef(coilConditionTag), new Expr.TagRef("LocalFlag") })),
            }),
        },
        TempMembers: new[] { new DbMember("LocalFlag", "Bool", Retain: false, StartValue: null) });

    private void SeedProject()
    {
        WriteProjectFile("DB_Marks.ir", DbIrSerializer.Serialize(
            new DbSource("0", "DB_Marks", 20, InstanceOfName: null, Comment: "Marker DB.", Members: new[] { new DbMember("Run", "Bool", Retain: false, StartValue: null) })));
        WriteProjectFile("iDB_Callee.ir", DbIrSerializer.Serialize(
            new DbSource("0", "iDB_Callee", 21, InstanceOfName: "FB_Callee", Comment: "Instance.", Members: Array.Empty<DbMember>())));
        WriteProjectFile("FB_Callee.ir", SerializeBlock(new IrBlock(
            "0", "FB", "FB_Callee", 5, "LAD", "Callee.", new[]
            {
                new IrNetwork(1, "Body", new[] { new CoilAssignment("Done", new Expr.TagRef("Go")) }),
            },
            TempMembers: new[] { new DbMember("Done", "Bool", Retain: false, StartValue: null), new DbMember("Go", "Bool", Retain: false, StartValue: null) })));
        WriteProjectFile("Tags.ir", TagTableIrSerializer.Serialize(new PlcTagTableSource("0", "Tags", new[]
        {
            new PlcTagSource("1", "Start_PB", "Bool", "%I0.0", true, true, true, null),
        })));
    }

    [Fact]
    public void Run_CleanBatch_ResolvesLocalsAndProjectRoots_ZeroFindings()
    {
        SeedProject();
        var target = WriteBatchFile(SerializeSynthesizable(CleanBlock("Start_PB")));

        var report = PreflightRunner.Run(new[] { target }, _projectDir);

        var file = Assert.Single(report.Files);
        // Convention-clean, convertible, everything resolves: a fully clean pre-flight.
        Assert.Empty(file.Findings);
        Assert.Empty(report.IndexWarnings);
    }

    [Fact]
    public void Run_ResolvableCalleeAndInstance_NoCallOrTagFindings()
    {
        SeedProject();
        var block = new IrBlock("0", "FC", "FC_CallsReal", 44, "LAD", "Header.", new[]
        {
            new IrNetwork(1, "Call", Array.Empty<CoilAssignment>(),
                Calls: new[] { new CallStatement("FB_Callee", "iDB_Callee", new Expr.TagRef("Start_PB"), Array.Empty<CallArgument>()) }),
        });
        var target = WriteBatchFile(SerializeBlock(block));

        var report = PreflightRunner.Run(new[] { target }, _projectDir);

        var file = Assert.Single(report.Files);
        Assert.DoesNotContain(file.Findings, f => f.Check is "call" or "tag" or "parse");
    }

    [Fact]
    public void Run_UnresolvedTagRoot_IsAFinding_ReportedOncePerRoot()
    {
        SeedProject();
        var target = WriteBatchFile(SerializeSynthesizable(CleanBlock("Ghost_Tag")));

        var report = PreflightRunner.Run(new[] { target }, _projectDir);

        var file = Assert.Single(report.Files);
        var tagFinding = Assert.Single(file.Findings, f => f.Check == "tag");
        Assert.Contains("Ghost_Tag", tagFinding.Description);
    }

    [Fact]
    public void Run_MissingCallee_IsACallFinding()
    {
        SeedProject();
        var block = new IrBlock("0", "FC", "FC_CallsGhost", 41, "LAD", "Header.", new[]
        {
            new IrNetwork(1, "Call", Array.Empty<CoilAssignment>(),
                Calls: new[] { new CallStatement("FB_Ghost", "iDB_Callee", new Expr.TagRef("Start_PB"), Array.Empty<CallArgument>()) }),
        });
        var target = WriteBatchFile(SerializeBlock(block));

        var report = PreflightRunner.Run(new[] { target }, _projectDir);

        var file = Assert.Single(report.Files);
        var callFinding = Assert.Single(file.Findings, f => f.Check == "call");
        Assert.Contains("FB_Ghost", callFinding.Description);
    }

    [Fact]
    public void Run_InstanceDbWithMissingInstanceOf_IsAFinding()
    {
        SeedProject();
        var target = WriteBatchFile(DbIrSerializer.Serialize(
            new DbSource("0", "iDB_Orphan", 30, InstanceOfName: "FB_Nowhere", Comment: "Orphan.", Members: Array.Empty<DbMember>())));

        var report = PreflightRunner.Run(new[] { target }, _projectDir);

        var file = Assert.Single(report.Files);
        var finding = Assert.Single(file.Findings, f => f.Check == "instanceof");
        Assert.Contains("FB_Nowhere", finding.Description);
    }

    [Fact]
    public void Run_BatchResolvesItself_NewDbAndItsReferrerPreflightTogether()
    {
        SeedProject();
        var newDb = WriteBatchFile(DbIrSerializer.Serialize(
            new DbSource("0", "DB_Brand_New", 31, InstanceOfName: null, Comment: "New.", Members: new[] { new DbMember("Flag", "Bool", Retain: false, StartValue: null) })));
        var referrer = WriteBatchFile(SerializeSynthesizable(new IrBlock("0", "FC", "FC_UsesNewDb", 42, "LAD", "Header.", new[]
        {
            new IrNetwork(1, "Uses new DB", new[] { new CoilAssignment("DB_Brand_New.Flag", new Expr.TagRef("Start_PB")) }),
        })));

        var report = PreflightRunner.Run(new[] { newDb, referrer }, _projectDir);

        Assert.All(report.Files, f => Assert.DoesNotContain(f.Findings, x => x.Check == "tag"));
    }

    [Fact]
    public void Run_ReviewFindingsAreFoldedIn_PrefixedByRule()
    {
        SeedProject();
        // Unprefixed block name + no header comment: C-003 and C-201 both fire via the review pass.
        var block = new IrBlock("0", "FC", "Preflight", 43, "LAD", null, new[]
        {
            new IrNetwork(1, "Wiring", new[] { new CoilAssignment("DB_Marks.Run", new Expr.TagRef("Start_PB")) }),
        });
        var target = WriteBatchFile(SerializeSynthesizable(block));

        var report = PreflightRunner.Run(new[] { target }, _projectDir);

        var file = Assert.Single(report.Files);
        Assert.Contains(file.Findings, f => f.Check == "review:C-003");
    }

    [Fact]
    public void Run_UnparsableTargetFile_IsAParseFinding_NotAnAbort()
    {
        SeedProject();
        var bad = WriteBatchFile("not ir content");
        var good = WriteBatchFile(SerializeSynthesizable(CleanBlock("Start_PB")));

        var report = PreflightRunner.Run(new[] { bad, good }, _projectDir);

        Assert.Equal(2, report.Files.Count);
        Assert.Contains(report.Files[0].Findings, f => f.Check == "parse");
        Assert.DoesNotContain(report.Files[1].Findings, f => f.Check == "parse");
    }

    [Fact]
    public void FormatText_MarksCleanFilesAndCarriesTheNotTheCompileGateFooter()
    {
        SeedProject();
        var target = WriteBatchFile(SerializeSynthesizable(CleanBlock("Start_PB")));
        var report = PreflightRunner.Run(new[] { target }, _projectDir);

        var text = PreflightOutputFormatter.FormatText(report);

        Assert.Contains("NAME: FC_Preflight", text);
        Assert.Contains("CLEAN", text);
        Assert.Contains("PRE-FLIGHT ONLY", text);
        Assert.Contains("SUMMARY:", text);
    }

    // FI-60 (2026-08-08). Preflight's convert pass synthesized with NO callee registry, so a block
    // containing a WIRED CALL always reported "cannot synthesize the wired CALL ... or pass
    // --project <ir-dir>" — EVEN ON A RUN WHERE --project WAS PASSED. The advice in the message was
    // the one thing that could not help, because preflight already had the project and never used
    // it.
    //
    // It is a false positive, not a missed defect (`to-xml --project` converted the same files
    // cleanly and both import and compile passed), which makes it the worse kind: it fires for
    // anyone who adds a parameterised FC call, on a check whose entire value is that a finding
    // means something.
    [Fact]
    public void Run_BlockWithWiredCallToAProjectBlock_DoesNotReportAMissingCallee()
    {
        SeedProject();

        // The callee lives in the PROJECT, not in the batch — which is exactly the case the
        // --project flag exists to cover.
        WriteProjectFile("FC_Callee.ir", SerializeSynthesizable(new IrBlock(
            "0", "FC", "FC_Callee", 41, "LAD", "Callee.",
            new[] { new IrNetwork(1, "N", new[] { new CoilAssignment("DB_Marks.Run", new Expr.TagRef("Start_PB")) }) },
            InputMembers: new[] { new DbMember("Enable", "Bool", Retain: false, StartValue: null) })));

        // No stored sidecar, so preflight takes the SYNTHESIS path — which is where the false
        // positive lived.
        var caller = WriteBatchFile(IrSerializer.SerializeBlockReadable(new IrBlock(
            "0", "FC", "FC_Caller", 42, "LAD", "Caller.",
            new[]
            {
                new IrNetwork(1, "Call", Array.Empty<CoilAssignment>(), Calls: new[]
                {
                    new CallStatement("FC_Callee", null, new Expr.Literal("TRUE"),
                        new CallArgument[] { new CallArgument.InputArg("Enable", new Expr.TagRef("Start_PB")) }),
                }),
            })));

        var report = PreflightRunner.Run(new[] { caller }, _projectDir);

        Assert.DoesNotContain(
            Assert.Single(report.Files).Findings,
            f => f.Description.Contains("wired CALL", StringComparison.Ordinal));
    }
}
