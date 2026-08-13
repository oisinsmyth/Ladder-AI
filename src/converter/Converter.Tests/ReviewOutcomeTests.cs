using Converter.Review;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 2026-08-13. The gate that stops "we did not check" from exiting 0. These tests build
/// ReviewReports directly rather than going through files, because the property under test is about
/// the STATUSES, not about any particular rule — a future rule that records itself Skipped for any
/// reason must gate without anyone remembering to wire it up.
///
/// Both directions are tested. A gate only ever exercised against input that should trip it has not
/// been tested either: the false-positive half (a clean report, a report whose only unjudged-looking
/// entries are genuinely NotApplicable) is what keeps exit 2 meaning something.
/// </summary>
public class ReviewOutcomeTests
{
    private static ReviewReport ReportWith(params RuleStatusEntry[] statuses) =>
        new(new[] { new FileReviewResult("x.ir", "FB_X", Array.Empty<Finding>(), statuses, null) });

    private static Finding ErrorFinding() =>
        new("C-201", FindingSeverity.Error, "FB_X", null, "no header comment", "add one");

    [Fact]
    public void ExitCode_NothingUnchecked_IsCleanZero()
    {
        var report = ReportWith(
            new RuleStatusEntry("C-001", RuleCheckStatus.Checked, 0, null),
            new RuleStatusEntry("C-301", RuleCheckStatus.NotApplicable, 0, "no networks"),
            new RuleStatusEntry("C-102", RuleCheckStatus.CheckedVacuous, 0, null));

        Assert.Empty(ReviewOutcome.UncheckedRules(report));
        Assert.Equal(ReviewOutcome.Clean, ReviewOutcome.ExitCode(report, allowUnchecked: false));
    }

    // NotApplicable and CheckedVacuous must NOT gate — they are real results. If they did, every
    // DB and every ordinary block would exit 2 and the signal would be worthless inside a week.
    [Theory]
    [InlineData(RuleCheckStatus.NotApplicable)]
    [InlineData(RuleCheckStatus.CheckedVacuous)]
    [InlineData(RuleCheckStatus.Checked)]
    public void ExitCode_NonSkippedStatuses_DoNotGate(RuleCheckStatus status)
    {
        var report = ReportWith(new RuleStatusEntry("C-118", status, 0, "some reason"));

        Assert.Empty(ReviewOutcome.UncheckedRules(report));
        Assert.NotEqual(ReviewOutcome.Incomplete, ReviewOutcome.ExitCode(report, allowUnchecked: false));
    }

    [Fact]
    public void ExitCode_SkippedRule_IsIncompleteTwo()
    {
        var report = ReportWith(
            new RuleStatusEntry("C-001", RuleCheckStatus.Checked, 0, null),
            new RuleStatusEntry("C-118", RuleCheckStatus.Skipped, 0, "not implemented for this content kind"));

        var entry = Assert.Single(ReviewOutcome.UncheckedRules(report));
        Assert.Equal("C-118", entry.RuleId);
        Assert.Equal("x.ir", entry.FilePath);
        Assert.Equal(ReviewOutcome.Incomplete, ReviewOutcome.ExitCode(report, allowUnchecked: false));
    }

    // The named escape (FI-71's shape): explicit, never the default, and it changes only the exit
    // code — the unchecked rules are still enumerated and still printed.
    [Fact]
    public void ExitCode_SkippedRule_AllowUncheckedFallsBackToTheFindingsVerdict()
    {
        var report = ReportWith(new RuleStatusEntry("C-118", RuleCheckStatus.Skipped, 0, "unimplemented"));

        Assert.Equal(ReviewOutcome.Clean, ReviewOutcome.ExitCode(report, allowUnchecked: true));
        Assert.Single(ReviewOutcome.UncheckedRules(report));
    }

    // Incomplete outranks Findings: an unjudged rule is the one that looks like a pass, so it is the
    // one the exit code should name. A caller testing `!= 0` is unaffected either way.
    [Fact]
    public void ExitCode_SkippedAndErrorFindings_ReportsIncomplete()
    {
        var report = new ReviewReport(new[]
        {
            new FileReviewResult("x.ir", "FB_X", new[] { ErrorFinding() },
                new[] { new RuleStatusEntry("C-118", RuleCheckStatus.Skipped, 0, "unimplemented") }, null),
        });

        Assert.Equal(ReviewOutcome.Incomplete, ReviewOutcome.ExitCode(report, allowUnchecked: false));
        Assert.Equal(ReviewOutcome.Findings, ReviewOutcome.ExitCode(report, allowUnchecked: true));
    }

    [Fact]
    public void ExitCode_ErrorFindingsOnly_IsOne()
    {
        var report = new ReviewReport(new[]
        {
            new FileReviewResult("x.ir", "FB_X", new[] { ErrorFinding() },
                new[] { new RuleStatusEntry("C-201", RuleCheckStatus.Checked, 1, null) }, null),
        });

        Assert.Equal(ReviewOutcome.Findings, ReviewOutcome.ExitCode(report, allowUnchecked: false));
    }

    // An Info finding must not gate — otherwise C-007's tolerated vendor defaults would fail every
    // real project's default tag table, and the exception would be worse than useless.
    [Fact]
    public void ExitCode_InfoFindingsOnly_IsClean()
    {
        var info = new Finding("C-005", FindingSeverity.Info, "Default tag table", null, "tolerated per C-007", "no action");
        var report = new ReviewReport(new[]
        {
            new FileReviewResult("x.ir", "Default tag table", new[] { info },
                new[] { new RuleStatusEntry("C-005", RuleCheckStatus.Checked, 1, null) }, null),
        });

        Assert.Equal(ReviewOutcome.Clean, ReviewOutcome.ExitCode(report, allowUnchecked: false));
    }

    [Fact]
    public void ExitCode_FileThatCouldNotBeParsed_IsOne()
    {
        var report = new ReviewReport(new[]
        {
            new FileReviewResult("x.ir", null, Array.Empty<Finding>(), Array.Empty<RuleStatusEntry>(), "IrFormatException: boom"),
        });

        Assert.Equal(ReviewOutcome.Findings, ReviewOutcome.ExitCode(report, allowUnchecked: false));
    }

    // The report must SAY so, in both renderings, and must say so even when the count is zero — an
    // absent line is itself ambiguous.
    [Fact]
    public void FormatTable_AlwaysCarriesTheUncheckedCount()
    {
        var clean = ReportWith(new RuleStatusEntry("C-001", RuleCheckStatus.Checked, 0, null));
        Assert.Contains("UNCHECKED: 0 rule(s) not judged", ReviewOutputFormatter.FormatTable(clean));

        var skipped = ReportWith(new RuleStatusEntry("C-118", RuleCheckStatus.Skipped, 0, "no --project index"));
        var text = ReviewOutputFormatter.FormatTable(skipped);
        Assert.Contains("UNCHECKED: 1 rule(s) not judged", text);
        Assert.Contains("NOT CHECKED - no result, not a pass", text);
        Assert.Contains("no --project index", text);
    }

    [Fact]
    public void FormatJson_HoistsTheUncheckedRulesToTheTopLevel()
    {
        var json = ReviewOutputFormatter.FormatJson(
            ReportWith(new RuleStatusEntry("C-118", RuleCheckStatus.Skipped, 0, "no --project index")));

        Assert.Contains("\"uncheckedRuleCount\": 1", json);
        Assert.Contains("\"ruleId\": \"C-118\"", json);
    }
}
