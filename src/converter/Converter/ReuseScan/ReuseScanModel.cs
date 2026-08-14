namespace Converter.ReuseScan;

// FI-29 (docs/16-future-ideas.md): a digest-backed corpus query — "which blocks reference tag T /
// implement a timeout/fault (a statement kind) on T" — for the reuse-first carving pass
// (gen-architecture / gen-spec-analysis). Surfaces CANDIDATE blocks a human/AI must then check for
// semantic duplication; it never rules "already exists" (matches digest's orientation-only policy,
// docs/15 isolation model). One record set, two renderers, like Digest/TagStatus.

// One network within a matched block, and which queried statement kind(s) it contains.
public sealed record ReuseNetworkMatch(int Number, string Title, IReadOnlyList<string> MatchedKinds);

// One block that matched the query: the tag roots that matched (block-level — digest aggregates tag
// roots per block, not per network) and the networks that matched a --kind (empty when no --kind was
// queried).
public sealed record ReuseBlockMatch(
    string FilePath,
    string BlockName,
    string Kind,
    IReadOnlyList<string> MatchedTags,
    IReadOnlyList<ReuseNetworkMatch> MatchedNetworks);

public sealed record ReuseScanReport(
    IReadOnlyList<string> QueryTagRoots,
    IReadOnlyList<string> QueryKinds,
    IReadOnlyList<ReuseBlockMatch> Matches,
    IReadOnlyList<string> FileErrors,
    // The denominator, and which of the queried roots the corpus actually knows about. Neither
    // existed before 2026-08-14 — the report printed the query and the matches and nothing else, so a
    // wrong --project or a typo'd tag produced "SUMMARY: 0 block(s) matched" and exit 0, identical to
    // a thorough scan that genuinely found no reuse candidate.
    int FilesScanned = 0,
    IReadOnlyList<string>? AbsentTagRoots = null)
{
    // Reuse-first alarm: a reuse candidate exists → non-zero exit, so `reuse-scan … && <build>` stops
    // the caller to look before building new logic. No matches → exit 0 (nothing to reuse, proceed).
    public bool HasMatches => Matches.Count > 0;

    public IReadOnlyList<string> Absent => AbsentTagRoots ?? Array.Empty<string>();

    // 🔴 EMPTY IS NOT CLEAN. Exit 0 here LICENSES "nothing to reuse, write a new block" — so a query
    // whose tag roots are in NO block of the corpus must not produce it. Note the asymmetry the tool
    // already had and this mirrors: `--kind` IS validated against a known set and refuses an unknown
    // value outright, while `--tag` was validated against nothing at all.
    //
    // Deliberately keyed on ALL roots being absent, not any. A Design-stage query legitimately mixes
    // existing tags with proposed ones (gen-architecture designs AGAINST gaps), and refusing that
    // would be a gate firing outside its scope — which is noise, and noise gets switched off. All
    // absent means the question landed on nothing, which no reading ends in "proceed".
    public bool AskedAboutNothing =>
        QueryTagRoots.Count > 0 && Absent.Count == QueryTagRoots.Count;
}
