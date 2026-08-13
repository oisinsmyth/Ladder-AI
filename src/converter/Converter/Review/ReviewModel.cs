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
//                     C-406 usage-form check against a DB-only file with no networks at all). The
//                     Reason must say WHY for this specific rule — a blanket per-file phrase (the
//                     old "TAGTABLE rule support not implemented in Phase 1", stamped on all 18)
//                     is how "we did not check" got filed under "nothing to check".
//   Skipped        — *** THE RULE HAD A SUBJECT HERE AND WAS NOT JUDGED. *** Not implemented for
//                     this content kind, or an input it needs (a --project index) was not supplied.
//                     A zero finding count here proves NOTHING. Reason is always populated, and
//                     `converter review` FAILS CLOSED on it — exit 2, see ReviewOutcome. This is
//                     deliberately a gate and not a warning: the tag-table version of this status
//                     was printed on every tag-table review from Phase 1 until 2026-08-13, in a run
//                     that exited 0, and it was read as a clean review every time.
//   CheckedHarnessScope
//                  — *** THE RULE RAN, IT PRODUCED FINDINGS, AND THOSE FINDINGS DO NOT GATE ***
//                     because this file's content is HARNESS-GENERATED (HarnessScope) and doc 06's
//                     naming conventions govern content authored for the plant. Only C-001 and C-201
//                     can ever carry this status — a harness object is exempt from a NAMING
//                     convention, never from behaving correctly, so C-103 and everything else gates
//                     exactly as before. The findings are still PRINTED, under their own labelled
//                     bucket, with the derivation that classified the object: a suppression nobody
//                     can see is one step from a suppression that hides, and the whole reason this
//                     status is not simply "skip the rule".
// A rule is never silently absent from a report — every rule Phase 1 knows about gets an entry
// with one of these five statuses for every file reviewed.
public enum RuleCheckStatus
{
    Checked,
    CheckedVacuous,
    NotApplicable,
    Skipped,
    CheckedHarnessScope,
}

public sealed record RuleStatusEntry(string RuleId, RuleCheckStatus Status, int FindingCount, string? Reason);

// One file's own review outcome. FileError is populated only when --ignore-errors let a
// parse/format failure be recorded instead of aborting the whole run (Program.cs's own
// SimaticMlFormatException/UnsupportedConstructException/NonReducibleNetworkException/
// IrFormatException catch set) — when it's set, Findings/RuleStatuses are both empty; this file
// was never reviewed, not reviewed-and-clean. Kept as its own explicit field rather than folding
// into RuleStatuses, since a parse failure isn't about any one rule, it's about not having
// reachable content to check any rule against at all.
// Harness/HarnessScopedFindings (2026-08-13) are additive and default to the conservative reading:
// a result constructed without them is UNCLASSIFIED with nothing exempted, which is exactly the
// pre-change behaviour. *** THE DEFAULT MUST NEVER BE "harness". *** HarnessScopedFindings are
// findings that WERE produced and are reported without gating; they are deliberately a separate
// list rather than a flag on Finding, so that every existing consumer of Findings (ReviewOutcome's
// exit code, PreflightRunner, the formatters) keeps its meaning of "this gates" without being
// edited to filter — an omitted filter is how an exemption becomes silent.
public sealed record FileReviewResult(
    string FilePath,
    string? BlockName,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<RuleStatusEntry> RuleStatuses,
    string? FileError,
    HarnessVerdict? Harness = null,
    IReadOnlyList<Finding>? HarnessScopedFindings = null)
{
    public IReadOnlyList<Finding> HarnessScopedFindings { get; init; } =
        HarnessScopedFindings ?? Array.Empty<Finding>();
}

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
