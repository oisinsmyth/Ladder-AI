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

    private static readonly string[] AllRuleIds = { "C-001", "C-003", "C-005", "C-201", "C-301", "C-501", "C-406", "C-408", "C-102", "C-401", "C-404" };

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

    // An untitled single-slice alarm-word coil trips C-301 (absolute addressing) AND, because it
    // fails the alarm-word exception, C-501 - one check (CheckC301AbsoluteAddressing) emits both
    // (see ReviewRulesTests.CheckC301_SingleSliceBitUntitled_FlagsBothC301AndC501). The C-301
    // status count must reflect only the C-301 findings actually printed under it, not the
    // co-emitted C-501 one - the F-1 reporter bug (count read "2" while one C-301 finding printed).
    [Fact]
    public void ReviewFiles_C301CoEmitsC501_C301CountMatchesOnlyItsOwnPrintedFindings()
    {
        var block = new IrBlock("0", "FB", "FB_Alarms", 1, "LAD", null, new[]
        {
            new IrNetwork(1, string.Empty, new[] { new CoilAssignment("AlarmWord.%X3", new Expr.TagRef("Cond1")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var path = WriteTempIrFile(IrSerializer.SerializeBlock(block, new[] { sidecar }));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        var c301Printed = file.Findings.Count(f => f.RuleId == "C-301");
        var c501Printed = file.Findings.Count(f => f.RuleId == "C-501");
        Assert.Equal(1, c301Printed);
        Assert.Equal(1, c501Printed);

        var c301Status = file.RuleStatuses.Single(s => s.RuleId == "C-301");
        var c501Status = file.RuleStatuses.Single(s => s.RuleId == "C-501");
        Assert.Equal(c301Printed, c301Status.FindingCount); // was 2 before the F-1 fix (counted the C-501 too)
        Assert.Equal(c501Printed, c501Status.FindingCount);
    }

    // General invariant, the real future-proofing: a Checked status's FindingCount must always equal
    // the number of findings printed under that rule ID. Catches any check co-emitting another
    // rule's findings (only C-301 -> C-501 today) inflating the wrong count.
    [Fact]
    public void ReviewFiles_EveryCheckedStatusCountEqualsItsOwnPrintedFindings()
    {
        var block = new IrBlock("0", "FB", "FB_Alarms", 1, "LAD", null, new[]
        {
            new IrNetwork(1, string.Empty, new[] { new CoilAssignment("AlarmWord.%X3", new Expr.TagRef("Cond1")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var path = WriteTempIrFile(IrSerializer.SerializeBlock(block, new[] { sidecar }));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        foreach (var status in file.RuleStatuses.Where(s => s.Status == RuleCheckStatus.Checked))
        {
            var printed = file.Findings.Count(f => f.RuleId == status.RuleId);
            Assert.Equal(printed, status.FindingCount);
        }
    }

    // A committed block .ir can be sidecar-less (e.g. a hand-authored OB); review its logic anyway,
    // the same branch preflight uses. Before this, review threw "Expected a 'SIDECAR' section" and
    // the block was silently unreviewable.
    [Fact]
    public void ReviewFiles_SidecarLessBlock_ReviewsInsteadOfErroring()
    {
        var block = new IrBlock("0", "OB", "OB_Startup", 100, "LAD", "Startup reset.", new[]
        {
            new IrNetwork(1, "Reset a state bit", new[] { new CoilAssignment("SomeState", new Expr.TagRef("Trigger")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var full = IrSerializer.SerializeBlock(block, new[] { sidecar });

        var cut = full.IndexOf("\nSIDECAR", StringComparison.Ordinal);
        Assert.True(cut > 0, "fixture setup: serialized block should contain a SIDECAR section to strip");
        var sidecarLess = full[..cut] + "\n";
        Assert.False(IrParser.HasSidecarSection(sidecarLess)); // the fixture really is sidecar-less

        var path = WriteTempIrFile(sidecarLess);

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        Assert.Null(file.FileError);                 // was non-null ("COULD NOT REVIEW") before the fix
        Assert.Equal("OB_Startup", file.BlockName);
        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-408" && s.Status == RuleCheckStatus.Checked); // rules ran
    }

    // A UDT (TYPE file) is a member container: only C-001 (member naming) is checked, every other
    // Phase-1 rule is NotApplicable.
    [Fact]
    public void ReviewFiles_TypeKindFile_ChecksC001OnlyRestNotApplicable()
    {
        var udt = new PlcTypeSource("0", "UDT_Test", "A type.", new[]
        {
            new DbMember("Good", "Bool", Retain: false, StartValue: null),
            new DbMember("Bad_Name", "Bool", Retain: false, StartValue: null),
        });
        var path = WriteTempIrFile(TypeIrSerializer.Serialize(udt));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        Assert.Equal("UDT_Test", file.BlockName);
        Assert.Contains(file.Findings, f => f.RuleId == "C-001" && f.Description.Contains("Bad_Name"));
        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-001" && s.Status == RuleCheckStatus.Checked);
        foreach (var id in AllRuleIds.Where(id => id != "C-001"))
        {
            Assert.Contains(file.RuleStatuses, s => s.RuleId == id && s.Status == RuleCheckStatus.NotApplicable);
        }
    }

    // Physical-IO tags keep their underscores by design, so C-001 must not fire on tag-table
    // entries - the whole table is NotApplicable.
    [Fact]
    public void ReviewFiles_TagTable_C001NotApplicable_UnderscoredTagsNotFlagged()
    {
        var tags = new PlcTagTableSource("0", "Tags", new[]
        {
            new PlcTagSource("1", "DI3_SYS_Start", "Bool", "%I0.0", true, true, true, null),
        });
        var path = WriteTempIrFile(TagTableIrSerializer.Serialize(tags));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        Assert.DoesNotContain(file.Findings, f => f.RuleId == "C-001");
        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-001" && s.Status == RuleCheckStatus.NotApplicable);
    }
}
