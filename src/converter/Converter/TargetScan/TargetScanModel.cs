namespace Converter.TargetScan;

// FI-30 (docs/16-future-ideas.md): the S6 new-block target gap-hunter. Cross-joins requirements.md
// REQs against a FRESH tag-status check (anti-laundering — never trusts the register's own marks) and
// the as-built corpus, pre-computing each REQ's MECHANICAL disqualifiers so a human confirms a short
// filtered list instead of hand-surveying every REQ. The "already implemented inline" signal is soft
// and semantic, so it is a SEPARATE, clearly-heuristic bucket (LikelyImplemented), never folded into
// the mechanical disqualifiers — the boundary the project's mechanization discipline requires.

// Where a REQ lands after the join.
public enum TargetOutcome
{
    // Mechanically clean AND no as-built block already references its exists-tags — a genuine
    // new-block-only candidate the human then confirms.
    Candidate,

    // Mechanically clean, but its exists-tags already appear in an as-built block: likely already
    // implemented. Heuristic, needs a human read / reuse-scan — NOT a mechanical verdict.
    LikelyImplemented,

    // A mechanical disqualifier fired (hmi-only / out-of-scope / proposed-tag-blocked / q-open).
    Disqualified,

    // The register marks the REQ withdrawn — carried for completeness, never a target.
    Withdrawn,
}

public sealed record ReqTarget(
    string ReqId,
    string Title,
    string ReqClass,
    TargetOutcome Outcome,
    IReadOnlyList<string> Disqualifiers,       // "hmi-only" | "out-of-scope" | "proposed-tag-blocked" | "q-open" | "withdrawn"
    IReadOnlyList<string> ProposedTags,        // the specific names that classified PROPOSED (root-level)
    IReadOnlyList<string> OpenQuestions,       // "Q-05 (Still open)" — linked questions not fully resolved
    IReadOnlyList<string> ImplementedInBlocks, // as-built blocks whose tag roots overlap the REQ's exists-tags
    IReadOnlyList<string> ImplementedViaRoots);// the overlapping roots, so the human judges the hint's strength

public sealed record TargetScanReport(
    IReadOnlyList<ReqTarget> Requirements,
    IReadOnlyList<string> IndexWarnings)
{
    public IReadOnlyList<ReqTarget> Candidates =>
        Requirements.Where(r => r.Outcome == TargetOutcome.Candidate).ToList();

    // No clean candidate → non-zero exit: the fast "no clean new-block target here" signal that the
    // ~55m manual survey used to reach by hand.
    public bool HasCandidates => Requirements.Any(r => r.Outcome == TargetOutcome.Candidate);
}
