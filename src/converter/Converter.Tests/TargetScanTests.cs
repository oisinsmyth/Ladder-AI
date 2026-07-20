using System.Text.Json;
using Converter.Ir;
using Converter.SimaticMl;
using Converter.TargetScan;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-30 (2026-07-20): `converter target-scan` — the S6 new-block target gap-hunter. Cross-joins a
/// requirements.md register against a fresh tag-status classification and the as-built corpus,
/// bucketing each REQ (Candidate / LikelyImplemented / Disqualified / Withdrawn). Builds a real
/// on-disk corpus + register the way the command runs.
/// </summary>
public class TargetScanTests : IDisposable
{
    private readonly string _projectDir;
    private readonly string _requirementsPath;

    public TargetScanTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), $"targetscan-proj-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_projectDir);

        // Corpus: a tag table with an existing-and-referenced tag (DI3_Start) and an existing-but-unused
        // tag (SpareTag), plus one block that references DI3_Start (so it's "implemented-in") and has a
        // timer. SpareTag is in the export but in no block.
        File.WriteAllText(Path.Combine(_projectDir, "Tags.ir"), TagTableIrSerializer.Serialize(
            new PlcTagTableSource("0", "Tags", new[]
            {
                new PlcTagSource("1", "DI3_Start", "Bool", "%I0.0", true, true, true, null),
                new PlcTagSource("2", "SpareTag", "Bool", "%I0.1", true, true, true, null),
            })));

        WriteBlock("FB_Main.ir", new IrBlock(
            "0", "FB", "FB_Main", 1, "LAD", null, new[]
            {
                new IrNetwork(1, "Start", new[] { new CoilAssignment("Motor", new Expr.TagRef("DI3_Start")) }),
                new IrNetwork(2, "Delay", Array.Empty<CoilAssignment>(),
                    Timers: new[] { new TimerBinding("T1", new Expr.TagRef("Motor"), new Expr.TagRef("DB_Settings.Delay")) }),
            }));

        _requirementsPath = Path.Combine(_projectDir, "requirements.md");
        File.WriteAllText(_requirementsPath, Register);
    }

    private void WriteBlock(string fileName, IrBlock block)
    {
        var sidecars = block.Networks
            .Select(n => new NetworkSidecar(n.Number, n.Number.ToString(), Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();
        File.WriteAllText(Path.Combine(_projectDir, fileName), IrSerializer.SerializeBlock(block, sidecars));
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
    }

    // Uses em-dash headers and the register's real bullet shape, exercising every outcome bucket.
    private const string Register = """
# Test register

## Requirements

### REQ-001 — Start command
- **Text:** Start on `DI3_Start`.
- **Class:** control
- **Notes:** `DI3_Start` exists and is wired.

### REQ-002 — Spare feature
- **Text:** A new feature over `SpareTag`.
- **Class:** control
- **Notes:** `SpareTag` exists but no block uses it.

### REQ-003 — Needs a new tag
- **Text:** Needs `MadeUpTag`.
- **Class:** control
- **Notes:** **proposed** — `MadeUpTag` is not in the export.

### REQ-004 — HMI indication
- **Text:** Show a lamp.
- **Class:** HMI
- **Notes:** display only.

### REQ-005 — Hardwired E-stop
- **Text:** E-stop is hardwired.
- **Class:** out-of-scope
- **Notes:** no PLC logic by design.

### REQ-006 — Blocked by an open question
- **Text:** Behaviour depends on the answer to Q-01.
- **Class:** control
- **Notes:** pending Q-01.

### REQ-007 — Old idea
- **Text:** A withdrawn feature.
- **Class:** control
- **Status:** withdrawn (2026-01-01, superseded).
- **Notes:** gone.

### REQ-008 — Resolved-question feature
- **Text:** A new feature over `SpareTag`, previously Q-02.
- **Class:** control
- **Notes:** Q-02 is resolved; go ahead.

## Open questions

- **Q-01 — First question. Still open** — no owner answer yet.
- **Q-02 — Second question. RESOLVED (owner ruling, 2026-01-01).** answered.
""";

    private static ReqTarget Req(TargetScanReport report, string id) =>
        report.Requirements.Single(r => r.ReqId == id);

    [Fact]
    public void Run_BucketsEveryReqByOutcome()
    {
        var report = TargetScanRunner.Run(_requirementsPath, _projectDir);

        Assert.Equal(8, report.Requirements.Count);
        Assert.Equal(TargetOutcome.LikelyImplemented, Req(report, "REQ-001").Outcome);
        Assert.Equal(TargetOutcome.Candidate, Req(report, "REQ-002").Outcome);
        Assert.Equal(TargetOutcome.Disqualified, Req(report, "REQ-003").Outcome);
        Assert.Equal(TargetOutcome.Disqualified, Req(report, "REQ-004").Outcome);
        Assert.Equal(TargetOutcome.Disqualified, Req(report, "REQ-005").Outcome);
        Assert.Equal(TargetOutcome.Disqualified, Req(report, "REQ-006").Outcome);
        Assert.Equal(TargetOutcome.Withdrawn, Req(report, "REQ-007").Outcome);
        Assert.Equal(TargetOutcome.Candidate, Req(report, "REQ-008").Outcome);

        Assert.Equal(2, report.Candidates.Count);
        Assert.True(report.HasCandidates);
    }

    [Fact]
    public void Run_DisqualifierReasonsAreSpecific()
    {
        var report = TargetScanRunner.Run(_requirementsPath, _projectDir);

        Assert.Contains("proposed-tag-blocked", Req(report, "REQ-003").Disqualifiers);
        Assert.Contains("MadeUpTag", Req(report, "REQ-003").ProposedTags);

        Assert.Contains("hmi-only", Req(report, "REQ-004").Disqualifiers);
        Assert.Contains("out-of-scope", Req(report, "REQ-005").Disqualifiers);

        Assert.Contains("q-open", Req(report, "REQ-006").Disqualifiers);
        Assert.Contains(Req(report, "REQ-006").OpenQuestions, q => q.Contains("Q-01") && q.Contains("Still open"));
    }

    [Fact]
    public void Run_InlineHint_NamesTheImplementingBlock()
    {
        var report = TargetScanRunner.Run(_requirementsPath, _projectDir);

        var req001 = Req(report, "REQ-001");
        Assert.Contains("FB_Main", req001.ImplementedInBlocks);
        Assert.Contains("DI3_Start", req001.ImplementedViaRoots);
    }

    [Fact]
    public void Run_ResolvedLinkedQuestion_DoesNotDisqualify()
    {
        var report = TargetScanRunner.Run(_requirementsPath, _projectDir);

        // REQ-008 links Q-02 (RESOLVED) and names an existing-but-unused tag → a clean candidate.
        Assert.Equal(TargetOutcome.Candidate, Req(report, "REQ-008").Outcome);
        Assert.Empty(Req(report, "REQ-008").Disqualifiers);
    }

    [Fact]
    public void FormatText_And_Json_RenderBuckets()
    {
        var report = TargetScanRunner.Run(_requirementsPath, _projectDir);

        var text = TargetScanOutputFormatter.FormatText(report);
        Assert.Contains("CANDIDATES: 2", text);
        Assert.Contains("REQ-002", text);
        Assert.Contains("proposed-tag-blocked [MadeUpTag]", text);

        var json = TargetScanOutputFormatter.FormatJson(report);
        using var doc = JsonDocument.Parse(json); // asserts valid JSON
        Assert.Equal(8, doc.RootElement.GetProperty("requirements").GetArrayLength());
        Assert.Equal(2, doc.RootElement.GetProperty("candidateCount").GetInt32());
    }
}
