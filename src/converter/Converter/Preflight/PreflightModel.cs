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

/// <summary>
/// WHAT THE FINDINGS WERE RESOLVED AGAINST (2026-08-23). Until this record existed, preflight
/// printed a numerator with no denominator: `SUMMARY: 1 file(s), 2 finding(s)`, where `1 file(s)`
/// is the BATCH SIZE — the numerator's subject, not its index. The same file at the same instant
/// reported 2 findings against a 43-file export and 5 against a 15-file one, and neither 43 nor 15
/// appeared anywhere; the three extra findings were worded "does not resolve to any block in the
/// project or batch" and were indistinguishable from genuine unresolved calls. The inverse of this
/// repository's EMPTY IS NOT CLEAN principle: <b>narrow is not dirty</b>.
///
/// <para>*** THERE ARE FOUR CORPUS WALKS, AND EACH DECIDES A DIFFERENT FINDING CLASS, SO EACH
/// CARRIES ITS OWN COUNT. *** ProjectIndex (block names → `call`/`instanceof`; tag ∪ DB names →
/// `tag` root), TagTypeRegistry (DB/UDT bodies → member path, literal-fit), CalleeInterfaceRegistry
/// (wired-CALL synthesis) and HarnessScope (the exemption classifier). One number cannot honestly
/// cover them all: "does not resolve to any BLOCK" is falsifiable against
/// <see cref="BlockNameCount"/> and nothing else, and the member-path findings never touch
/// ProjectIndex at all — a file count printed under either is a category slip.</para>
///
/// <para><see cref="HarnessCorpusFileCount"/> is deduplicated and path-canonicalised by
/// <c>HarnessScope.Build</c>, so it can legitimately be smaller than
/// <see cref="ProjectFileCount"/> + <see cref="BatchFileCount"/> when a batch file already lives in
/// the project directory.</para>
/// </summary>
public sealed record PreflightCorpus(
    string ProjectDir,
    int ProjectFileCount,
    int BatchFileCount,
    int BlockNameCount,
    int TagRootNameCount,
    int TypeNameCount,
    int MemberBodyCount,
    int CalleeInterfaceCount,
    int HarnessCorpusFileCount)
{
    /// <summary>
    /// The `--project` directory exists (the CLI refuses a missing one) but held no .ir file, so
    /// every name was resolved against the batch alone. Not an error and not an exit code — a
    /// self-contained batch is a legitimate pre-flight — but a fact no verdict here may be read
    /// without.
    /// </summary>
    public bool ProjectContributedNothing => ProjectFileCount == 0;
}

/// <summary>IndexWarnings: project-dir .ir files that could not be indexed (parse failure) — the
/// index is then incomplete, which can only cause false findings, never false passes… except that
/// an unindexed DB/tag table would fail its referrers. Surfaced so the operator knows.
/// <para>Corpus: see <see cref="PreflightCorpus"/> — computed ONCE here, in the runner, and
/// rendered by both formatters. Deriving it in a formatter would be two derivations of the same
/// number, against this file's "one record, two renderers" contract.</para></summary>
public sealed record PreflightReport(
    IReadOnlyList<FilePreflight> Files, IReadOnlyList<string> IndexWarnings, PreflightCorpus Corpus)
{
    // Keyed on Gates, never on the raw count: a run whose only findings are the reported-but-exempt
    // harness ones is a CLEAN run, and counting them would mean a harness object could never pass
    // pre-flight no matter how the exemption was worded.
    public bool HasFindings => Files.Any(f => f.Findings.Any(x => x.Gates));
}
