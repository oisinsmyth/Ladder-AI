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
    IReadOnlyList<string> FileErrors)
{
    // Reuse-first alarm: a reuse candidate exists → non-zero exit, so `reuse-scan … && <build>` stops
    // the caller to look before building new logic. No matches → exit 0 (nothing to reuse, proceed).
    public bool HasMatches => Matches.Count > 0;
}
