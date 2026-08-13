namespace Converter.Review;

/// <summary>
/// What a <see cref="ReviewReport"/> means for the process exit code — kept out of Program.cs so it
/// can be tested directly rather than only through the CLI.
///
/// The rule this exists to enforce (2026-08-13): *** A RULE THAT WAS NOT CHECKED MUST NOT EXIT 0. ***
/// `converter review` was run against a tag table and exited 0 while all 18 rules reported
/// "not applicable — TAGTABLE rule support not implemented in Phase 1". The file was unreviewed and
/// the exit code was indistinguishable from a clean review — the project's recurring failure class,
/// an absence of findings read as a positive result, live in the tooling.
///
/// A warning here would not have helped: a line in a report gets skimmed, and this one had been
/// printed on every tag-table review since Phase 1 without anyone acting on it. So
/// <see cref="RuleCheckStatus.Skipped"/> is a GATE. Exit 2 (Incomplete) takes precedence over exit 1
/// (findings), on the reasoning that the unjudged rule is the one that looks like a pass — a caller
/// testing `!= 0` is unaffected either way, and a caller that distinguishes gets the stronger
/// signal. `--allow-unchecked` is the named escape (the shape FI-71 established), never the default.
/// </summary>
public static class ReviewOutcome
{
    public const int Clean = 0;
    public const int Findings = 1;
    public const int Incomplete = 2;

    /// <summary>Every (file, rule) pair whose status says the rule was not judged.</summary>
    public static IReadOnlyList<UncheckedRule> UncheckedRules(ReviewReport report) =>
        report.Files
            .SelectMany(f => f.RuleStatuses
                .Where(s => s.Status == RuleCheckStatus.Skipped)
                .Select(s => new UncheckedRule(f.FilePath, s.RuleId, s.Reason)))
            .ToList();

    public static int ExitCode(ReviewReport report, bool allowUnchecked)
    {
        if (!allowUnchecked && UncheckedRules(report).Count > 0)
        {
            return Incomplete;
        }

        var hasErrorFindings = report.Files.Any(f => f.Findings.Any(finding => finding.Severity == FindingSeverity.Error));
        var hasFileErrors = report.Files.Any(f => f.FileError is not null);
        return hasErrorFindings || hasFileErrors ? Findings : Clean;
    }
}

public sealed record UncheckedRule(string FilePath, string RuleId, string? Reason);
