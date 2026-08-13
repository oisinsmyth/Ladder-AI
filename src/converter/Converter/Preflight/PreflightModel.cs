namespace Converter.Preflight;

// FI-13 (docs/16-future-ideas.md): a static filter in front of the Portal round trip — catches
// the *known, recurring* import/compile error classes (unresolved tag roots, missing callees,
// missing INSTANCEOF targets, unconvertible IR, review findings) in milliseconds instead of a
// Portal cycle. Explicitly NOT the compile gate (hard rule 4): passing pre-flight proves nothing
// about TIA acceptance; it only removes the failures we already know how to predict.

/// <summary>One pre-flight finding. Check is the short category: "parse", "convert", "tag",
/// "call", "instanceof", "review:C-xxx" (review findings folded in, prefixed by rule), or
/// "review:harness-scope".
/// <para>
/// Gates (2026-08-13) says whether this finding decides the exit code. *** IT DEFAULTS TO TRUE, SO
/// A FINDING IS GATING UNLESS SOMEONE SAID OTHERWISE *** — a non-gating default would make every
/// future check silently advisory, which is this repository's "a warning is not a gate" defect
/// installed at the type level. The only non-gating findings today are the harness-scope ones: a
/// harness-generated object's C-001/C-201 findings are REPORTED so the exemption is visible, and a
/// reported exemption must not then fail the run it was granted for.
/// </para></summary>
public sealed record PreflightFinding(string Check, string Description, bool Gates = true);

public sealed record FilePreflight(string FilePath, string? Name, IReadOnlyList<PreflightFinding> Findings);

/// <summary>IndexWarnings: project-dir .ir files that could not be indexed (parse failure) — the
/// index is then incomplete, which can only cause false findings, never false passes… except that
/// an unindexed DB/tag table would fail its referrers. Surfaced so the operator knows.</summary>
public sealed record PreflightReport(IReadOnlyList<FilePreflight> Files, IReadOnlyList<string> IndexWarnings)
{
    // Keyed on Gates, never on the raw count: a run whose only findings are the reported-but-exempt
    // harness ones is a CLEAN run, and counting them would mean a harness object could never pass
    // pre-flight no matter how the exemption was worded.
    public bool HasFindings => Files.Any(f => f.Findings.Any(x => x.Gates));
}
