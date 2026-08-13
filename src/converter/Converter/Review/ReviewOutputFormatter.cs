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

            // *** PRINTED FOR EVERY FILE, INCLUDING PLANT ONES. *** The verdict and its derivation
            // are what make the classifier's behaviour checkable by a reader — and, more to the
            // point, what makes a classifier that silently STOPPED RUNNING visible: if this line
            // ever reads "no number was available" on a file that plainly has one, the derivation
            // broke. A scope that only announces itself when it exempts something is a scope whose
            // failure mode is invisible.
            if (file.Harness is { } verdict)
            {
                sb.Append("  SCOPE: ").Append(verdict.Class switch
                {
                    HarnessClass.Harness => "HARNESS-GENERATED",
                    HarnessClass.Plant => "plant content",
                    _ => "UNCLASSIFIED (treated as plant - every finding gates)",
                }).Append(" - ").Append(verdict.Basis).Append('\n');
            }

            sb.Append('\n');

            foreach (var status in file.RuleStatuses)
            {
                var statusText = status.Status switch
                {
                    RuleCheckStatus.Checked when status.FindingCount == 0 => "checked, clean",
                    RuleCheckStatus.Checked => $"checked, {status.FindingCount} finding(s)",
                    RuleCheckStatus.CheckedVacuous => "checked, vacuous (cannot fire against current IR capability)",
                    RuleCheckStatus.NotApplicable => $"not applicable ({status.Reason})",
                    // Deliberately not the word "skipped" alone, and deliberately shouted: this line
                    // and "checked, clean" were previously a reader's only difference between a rule
                    // that passed and a rule nobody ran.
                    RuleCheckStatus.Skipped => $"NOT CHECKED - no result, not a pass ({status.Reason})",
                    // Says what happened AND where the findings went. Never "clean" and never
                    // silent: the count that gates and the count that does not are both stated, and
                    // a zero here is written "0 gating" rather than "clean" — a rule whose findings
                    // were all moved has not come back clean, and must not read as though it had.
                    RuleCheckStatus.CheckedHarnessScope =>
                        $"checked, {status.FindingCount} gating finding(s) - {status.Reason}",
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

            if (file.HarnessScopedFindings.Count > 0)
            {
                sb.Append("  HARNESS-SCOPE (reported, NOT gating - ").Append(file.HarnessScopedFindings.Count)
                    .Append(" finding(s) on harness-generated content; doc 06's naming conventions govern content authored for the plant):\n");
                foreach (var finding in file.HarnessScopedFindings)
                {
                    var location = finding.NetworkNumber is int n ? $"network {n}" : "(block level)";
                    sb.Append("  [harness ").Append(finding.Severity).Append("] ").Append(finding.RuleId).Append(' ').Append(location).Append('\n');
                    sb.Append("    ").Append(finding.Description).Append('\n');
                }
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

        // *** ALWAYS PRINTED, INCLUDING THE ZERO — A COUNT OF ZERO IS A DIFFERENT FACT FROM AN
        // ABSENT SECTION. *** Same reasoning as UNCHECKED below: a section that appears only when it
        // has something to say cannot distinguish "nothing was exempted" from "the classifier never
        // ran". The object counts are stated separately from the finding count because a run that
        // exempted nothing while classifying two harness objects, and a run that classified none at
        // all, are not the same run.
        var harnessObjects = report.Files.Count(f => f.Harness?.Class == HarnessClass.Harness);
        var unclassifiedObjects = report.Files.Count(f => f.Harness?.Class == HarnessClass.Unclassified);
        var harnessFindings = report.Files.Sum(f => f.HarnessScopedFindings.Count);
        sb.Append("HARNESS-SCOPE: ").Append(harnessFindings).Append(" finding(s) reported and NOT gated, across ")
            .Append(harnessObjects).Append(" harness-generated object(s); ")
            .Append(unclassifiedObjects).Append(" object(s) could not be classified and gate as plant")
            .Append('\n');

        // Always printed, including the zero — an absent line would itself be ambiguous, and the
        // whole point of this section is that silence must not be readable as a pass (FI-44's
        // "empty is not clean", applied to the review's own report).
        var unchecked_ = ReviewOutcome.UncheckedRules(report);
        sb.Append("UNCHECKED: ").Append(unchecked_.Count).Append(" rule(s) not judged");
        if (unchecked_.Count > 0)
        {
            sb.Append(" - a zero finding count for these is NOT a pass (exit 2)");
            foreach (var entry in unchecked_)
            {
                sb.Append('\n').Append("  ").Append(entry.RuleId).Append(" in ").Append(entry.FilePath)
                    .Append(": ").Append(entry.Reason);
            }
        }

        sb.Append('\n');

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(ReviewReport report)
    {
        var uncheckedRules = ReviewOutcome.UncheckedRules(report);
        var payload = new
        {
            // Hoisted to the top level rather than left to be reassembled from ruleStatuses: a
            // consumer that has to compute "was anything left unjudged" itself is a consumer that
            // will not.
            uncheckedRuleCount = uncheckedRules.Count,
            uncheckedRules = uncheckedRules.Select(u => new
            {
                filePath = u.FilePath,
                ruleId = u.RuleId,
                reason = u.Reason,
            }),
            // Hoisted for the same reason as uncheckedRuleCount, and always emitted including the
            // zeros: a consumer must be able to tell "nothing was exempted" from "the classifier
            // produced no verdicts", and a field that is absent when empty cannot express that.
            harnessScopedFindingCount = report.Files.Sum(f => f.HarnessScopedFindings.Count),
            harnessObjectCount = report.Files.Count(f => f.Harness?.Class == HarnessClass.Harness),
            unclassifiedObjectCount = report.Files.Count(f => f.Harness?.Class == HarnessClass.Unclassified),
            files = report.Files.Select(f => new
            {
                filePath = f.FilePath,
                blockName = f.BlockName,
                fileError = f.FileError,
                harnessClass = (f.Harness?.Class ?? HarnessClass.Unclassified).ToString(),
                harnessBasis = f.Harness?.Basis,
                harnessScopedFindings = f.HarnessScopedFindings.Select(finding => new
                {
                    ruleId = finding.RuleId,
                    severity = finding.Severity.ToString(),
                    blockName = finding.BlockName,
                    networkNumber = finding.NetworkNumber,
                    description = finding.Description,
                    suggestedFix = finding.SuggestedFix,
                }),
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
