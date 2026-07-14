namespace Converter.Review;

// Mirrors docs/06-lad-conventions.md's own three-tier severity scheme directly (error/warn/info)
// — that doc is the rule catalog this whole module checks against; no reason to invent a
// different taxonomy. Deliberately NOT OpennessCli.Model.CompileState (a 4-value overall-result
// enum including Success) — a per-finding severity is a different shape than an overall outcome.
public enum FindingSeverity
{
    Info,
    Warn,
    Error,
}

// One concrete violation. RuleId/Severity are always single-valued — a location that implicates
// two rules at once (e.g. an alarm-word slice-access exception failing both C-301 and C-501)
// emits two Findings sharing the same BlockName/NetworkNumber, not one Finding with a list-valued
// RuleId — keeps the error/warn tally in ReviewSummary correct without a second aggregation step.
// NetworkNumber is null for a block-level finding (e.g. C-003's own name-prefix check, or C-201's
// missing-header-comment check).
public sealed record Finding(
    string RuleId,
    FindingSeverity Severity,
    string BlockName,
    int? NetworkNumber,
    string Description,
    string SuggestedFix);

// Four states, not two — conflating any of these loses real information a reader would otherwise
// wrongly assume:
//   Checked        — the rule's check code ran against this file's actual content. FindingCount
//                     may be zero; that's a real, meaningful "nothing wrong here."
//   CheckedVacuous — the rule's check code ran, found nothing, but the checked-for construct is
//                     structurally incapable of appearing in any .ir file today (C-102/C-401/
//                     C-404 — the parser rejects jumps/counters/built-in edge instructions before
//                     a .ir file can even exist). A zero here proves nothing about this file
//                     specifically; reported separately from Checked so it's never read as "the
//                     tool verified this and found no problem."
//   NotApplicable  — this rule has nothing to inspect in this file's own content kind (e.g. a
//                     C-406 usage-form check against a DB-only file with no networks at all).
//   Skipped        — not implemented in this phase (Bucket B/C rules, or Bucket-A rules beyond
//                     Phase 1's 8). Reason is always populated.
// A rule is never silently absent from a report — every rule Phase 1 knows about gets an entry
// with one of these four statuses for every file reviewed.
public enum RuleCheckStatus
{
    Checked,
    CheckedVacuous,
    NotApplicable,
    Skipped,
}

public sealed record RuleStatusEntry(string RuleId, RuleCheckStatus Status, int FindingCount, string? Reason);

// One file's own review outcome. FileError is populated only when --ignore-errors let a
// parse/format failure be recorded instead of aborting the whole run (Program.cs's own
// SimaticMlFormatException/UnsupportedConstructException/NonReducibleNetworkException/
// IrFormatException catch set) — when it's set, Findings/RuleStatuses are both empty; this file
// was never reviewed, not reviewed-and-clean. Kept as its own explicit field rather than folding
// into RuleStatuses, since a parse failure isn't about any one rule, it's about not having
// reachable content to check any rule against at all.
public sealed record FileReviewResult(
    string FilePath,
    string? BlockName,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<RuleStatusEntry> RuleStatuses,
    string? FileError);

public sealed record ReviewReport(IReadOnlyList<FileReviewResult> Files);

// Thrown by ReviewRunner.ReviewFiles when ignoreErrors is false and a file fails to parse -
// carries the offending path in the message (the wrapped SimaticMlFormatException/
// UnsupportedConstructException/NonReducibleNetworkException/IrFormatException doesn't know its
// own source file). Kept narrow and purpose-specific, matching this codebase's existing exception
// style (SimaticMlFormatException, UnsupportedConstructException, IrFormatException,
// NonReducibleNetworkException), so Program.cs's catch can't accidentally swallow an unrelated bug
// the way catching a generic InvalidOperationException could.
public sealed class ReviewFileException : Exception
{
    public ReviewFileException(string message)
        : base(message)
    {
    }
}
