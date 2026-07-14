using System.Text;
using System.Text.Json;

namespace Converter.Review;

// Mirrors OpennessCli.Cli.OutputFormatter's own "one record, two renderers" pattern - already
// this project's house style for exactly this shape of output.
public static class ReviewOutputFormatter
{
    public static string FormatTable(ReviewReport report)
    {
        var sb = new StringBuilder();

        foreach (var file in report.Files)
        {
            sb.Append("FILE: ").Append(file.FilePath).Append('\n');

            if (file.FileError is not null)
            {
                sb.Append("  COULD NOT REVIEW: ").Append(file.FileError).Append('\n');
                sb.Append('\n');
                continue;
            }

            sb.Append("  BLOCK: ").Append(file.BlockName).Append('\n');
            sb.Append('\n');

            foreach (var status in file.RuleStatuses)
            {
                var statusText = status.Status switch
                {
                    RuleCheckStatus.Checked when status.FindingCount == 0 => "checked, clean",
                    RuleCheckStatus.Checked => $"checked, {status.FindingCount} finding(s)",
                    RuleCheckStatus.CheckedVacuous => "checked, vacuous (cannot fire against current IR capability)",
                    RuleCheckStatus.NotApplicable => $"not applicable ({status.Reason})",
                    RuleCheckStatus.Skipped => $"skipped ({status.Reason})",
                    _ => status.Status.ToString(),
                };
                sb.Append("  ").Append(status.RuleId).Append(": ").Append(statusText).Append('\n');
            }

            sb.Append('\n');

            foreach (var finding in file.Findings)
            {
                var location = finding.NetworkNumber is int n ? $"network {n}" : "(block level)";
                sb.Append("  [").Append(finding.Severity).Append("] ").Append(finding.RuleId).Append(' ').Append(location).Append('\n');
                sb.Append("    ").Append(finding.Description).Append('\n');
                sb.Append("    fix: ").Append(finding.SuggestedFix).Append('\n');
            }

            sb.Append('\n');
        }

        var totalFindings = report.Files.Sum(f => f.Findings.Count);
        var errorCount = report.Files.Sum(f => f.Findings.Count(x => x.Severity == FindingSeverity.Error));
        var warnCount = report.Files.Sum(f => f.Findings.Count(x => x.Severity == FindingSeverity.Warn));
        var couldNotReview = report.Files.Count(f => f.FileError is not null);
        sb.Append("SUMMARY: ").Append(report.Files.Count).Append(" file(s), ")
            .Append(totalFindings).Append(" finding(s) (").Append(errorCount).Append(" error, ").Append(warnCount).Append(" warn)");
        if (couldNotReview > 0)
        {
            sb.Append(", ").Append(couldNotReview).Append(" file(s) could not be reviewed");
        }

        sb.Append('\n');

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(ReviewReport report)
    {
        var payload = new
        {
            files = report.Files.Select(f => new
            {
                filePath = f.FilePath,
                blockName = f.BlockName,
                fileError = f.FileError,
                ruleStatuses = f.RuleStatuses.Select(s => new
                {
                    ruleId = s.RuleId,
                    status = s.Status.ToString(),
                    findingCount = s.FindingCount,
                    reason = s.Reason,
                }),
                findings = f.Findings.Select(finding => new
                {
                    ruleId = finding.RuleId,
                    severity = finding.Severity.ToString(),
                    blockName = finding.BlockName,
                    networkNumber = finding.NetworkNumber,
                    description = finding.Description,
                    suggestedFix = finding.SuggestedFix,
                }),
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
