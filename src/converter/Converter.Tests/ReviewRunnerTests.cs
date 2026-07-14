using Converter.Ir;
using Converter.Review;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// S4 Phase 1 (2026-07-15): ReviewRunner's own orchestration layer - BLOCK vs. DB dispatch, the
/// never-silently-absent rule-status invariant, and the --ignore-errors abort-vs-record branch
/// (the mid-session ask that added it: a batch review shouldn't abort entirely just because one
/// file fails to parse). Rules.cs's own check logic is covered directly in ReviewRulesTests.cs;
/// these tests are about the file-dispatch/status-assembly layer on top, which genuinely needs
/// real files on disk (ReviewRunner.ReviewFiles reads by path, unlike the in-memory Rules.* API).
/// </summary>
public class ReviewRunnerTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string WriteTempIrFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"review-test-{Guid.NewGuid():N}.ir");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private static readonly string[] AllRuleIds = { "C-003", "C-005", "C-201", "C-301", "C-501", "C-406", "C-102", "C-401", "C-404" };

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Best-effort cleanup only - not the point of the test.
            }
        }
    }

    [Fact]
    public void ReviewFiles_BlockKindFile_DispatchesToReviewBlockAndReportsEveryRule()
    {
        // Deliberately unprefixed name/no header comment so C-003/C-201 both have something real
        // to find - proves findings actually flow end-to-end through the dispatch/assembly layer,
        // not just that Rules.cs itself works in isolation.
        var block = new IrBlock("0", "FB", "MotorDOL", 1, "LAD", null, new[]
        {
            new IrNetwork(1, "Motor start/stop", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var path = WriteTempIrFile(IrSerializer.SerializeBlock(block, new[] { sidecar }));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        Assert.Equal("MotorDOL", file.BlockName);
        Assert.Null(file.FileError);
        Assert.Contains(file.Findings, f => f.RuleId == "C-003");
        Assert.Contains(file.Findings, f => f.RuleId == "C-201");

        foreach (var ruleId in AllRuleIds)
        {
            Assert.Contains(file.RuleStatuses, s => s.RuleId == ruleId);
        }

        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-102" && s.Status == RuleCheckStatus.CheckedVacuous);
        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-401" && s.Status == RuleCheckStatus.CheckedVacuous);
        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-404" && s.Status == RuleCheckStatus.CheckedVacuous);
    }

    [Fact]
    public void ReviewFiles_DbKindFile_DispatchesToReviewDbWithNetworkRulesNotApplicable()
    {
        var db = new DbSource("0", "AlarmData", 1, InstanceOfName: null, Comment: "Holds alarm state.", Members: Array.Empty<DbMember>());
        var path = WriteTempIrFile(DbIrSerializer.Serialize(db));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        Assert.Equal("AlarmData", file.BlockName);
        Assert.Null(file.FileError);
        Assert.Contains(file.Findings, f => f.RuleId == "C-003"); // "AlarmData" lacks the DB_ prefix.

        foreach (var ruleId in AllRuleIds)
        {
            Assert.Contains(file.RuleStatuses, s => s.RuleId == ruleId);
        }

        foreach (var networkOnlyRule in new[] { "C-301", "C-501", "C-102", "C-401", "C-404" })
        {
            Assert.Contains(file.RuleStatuses, s => s.RuleId == networkOnlyRule && s.Status == RuleCheckStatus.NotApplicable);
        }
    }

    [Fact]
    public void ReviewFiles_MalformedFile_IgnoreErrorsFalse_ThrowsWithPathInMessage()
    {
        var path = WriteTempIrFile("BLOCK GARBAGE\n");

        var ex = Assert.Throws<ReviewFileException>(() => ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false));

        Assert.Contains(path, ex.Message);
    }

    [Fact]
    public void ReviewFiles_MalformedFile_IgnoreErrorsTrue_RecordsFileErrorAndContinuesBatch()
    {
        var badPath = WriteTempIrFile("BLOCK GARBAGE\n");

        var block = new IrBlock("0", "FB", "FB_Good", 1, "LAD", "A header comment.", new[]
        {
            new IrNetwork(1, "Motor start/stop", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var goodPath = WriteTempIrFile(IrSerializer.SerializeBlock(block, new[] { sidecar }));

        var report = ReviewRunner.ReviewFiles(new[] { badPath, goodPath }, ignoreErrors: true);

        Assert.Equal(2, report.Files.Count);

        var badFile = report.Files.Single(f => f.FilePath == badPath);
        Assert.NotNull(badFile.FileError);
        Assert.Empty(badFile.Findings);
        Assert.Empty(badFile.RuleStatuses);

        var goodFile = report.Files.Single(f => f.FilePath == goodPath);
        Assert.Null(goodFile.FileError);
        Assert.Equal("FB_Good", goodFile.BlockName);
    }
}
