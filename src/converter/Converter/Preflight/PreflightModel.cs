namespace Converter.Preflight;

// FI-13 (docs/16-future-ideas.md): a static filter in front of the Portal round trip — catches
// the *known, recurring* import/compile error classes (unresolved tag roots, missing callees,
// missing INSTANCEOF targets, unconvertible IR, review findings) in milliseconds instead of a
// Portal cycle. Explicitly NOT the compile gate (hard rule 4): passing pre-flight proves nothing
// about TIA acceptance; it only removes the failures we already know how to predict.

/// <summary>One pre-flight finding. Check is the short category: "parse", "convert", "tag",
/// "call", "instanceof", or "review:C-xxx" (review findings folded in, prefixed by rule).</summary>
public sealed record PreflightFinding(string Check, string Description);

public sealed record FilePreflight(string FilePath, string? Name, IReadOnlyList<PreflightFinding> Findings);

/// <summary>IndexWarnings: project-dir .ir files that could not be indexed (parse failure) — the
/// index is then incomplete, which can only cause false findings, never false passes… except that
/// an unindexed DB/tag table would fail its referrers. Surfaced so the operator knows.</summary>
public sealed record PreflightReport(IReadOnlyList<FilePreflight> Files, IReadOnlyList<string> IndexWarnings)
{
    public bool HasFindings => Files.Any(f => f.Findings.Count > 0);
}
