using Converter.CrossCheck;

namespace Converter.ConflictGraph;

/// <summary>Where a cited signal came from. <b>It decides which join is authoritative for it.</b></summary>
public enum SignalOrigin
{
    /// <summary>
    /// Cited by a SUBMISSION — an <c>inputs</c> key or an <c>expectations[].signal</c>. These are the
    /// SPECIFICATION's names by design, so the submission's own <c>map.storage</c> is what joins them.
    /// </summary>
    Submission,

    /// <summary>
    /// Supplied by an operator through <c>--signals</c>. That file is a list of storage paths written by
    /// whoever is running the command; there is no document declaring a join for it, so the project-path
    /// match is all there is and saying otherwise would refuse every use of the flag.
    /// </summary>
    OperatorList,
}

/// <summary>One cited name and where it came from.</summary>
public sealed record CitedSignal(string Name, SignalOrigin Origin);

/// <summary>
/// WHICH JOIN CARRIED A NAME TO ITS STORAGE. <b>Reported per name, always</b> — a resolution that
/// cannot be explained is what made this defect invisible for as long as it was.
/// </summary>
public enum SignalJoinKind
{
    /// <summary>
    /// The submission's <c>map.storage</c> named the (owner, path) and it matched project storage.
    /// <b>The only join the contract endorses.</b>
    /// </summary>
    DeclaredStorage,

    /// <summary>
    /// The submission's <c>map.harnessOnly</c> claimed the signal occupies no PLC storage. <b>No edge is
    /// possible and that is a COMPUTED FACT</b>, not a gap — it does not count against the scope.
    /// </summary>
    DeclaredHarnessOnly,

    /// <summary>
    /// No join was declared anywhere in the document, so the cited name was matched against the
    /// project's own storage paths. <b>A weaker join, and it is reported as such on every line that uses
    /// it</b>: it includes a leaf-name match, which the contract forbids relying on.
    /// </summary>
    ProjectPathMatch,

    /// <summary>
    /// The document declares a join and this signal is in NEITHER <c>storage</c> nor <c>harnessOnly</c>.
    /// <b>Unresolved, and deliberately not fallen back on</b> — a map that names 69 of 70 signals is a
    /// map with one hole in it, and filling that hole by name shape is the aliasing the contract removed.
    /// </summary>
    NotDeclared,

    /// <summary>
    /// The declaration contradicts itself — in <c>storage</c> AND <c>harnessOnly</c>, or a
    /// <c>map</c> entry that could not be read at all. <b>Refused, naming it.</b>
    /// </summary>
    ContradictoryDeclaration,
}

/// <summary>One resolved storage location, reduced to what an edge needs.</summary>
/// <param name="Path">The display path, for the reader.</param>
/// <param name="WriterBlocks">
/// Distinct blocks that WRITE it — the set a cross-block conflict is decided on, UNIONED across every
/// spelling of the one storage. See <see cref="SignalStorageResolver"/> for why the union is bounded.
/// </param>
public sealed record ResolvedStorage(string Path, IReadOnlyList<string> WriterBlocks);

/// <summary>One cited name, the join that carried it, and where it landed.</summary>
public sealed record SignalStorageResolution(
    string Signal,
    SignalJoinKind Join,
    SignalResolution Resolution,
    ResolvedStorage? Storage,
    IReadOnlyList<string> Candidates,
    string Reason);

/// <summary>
/// 🔴 <b>THE ONE PLACE IN THIS ASSEMBLY WHERE A CITED SIGNAL NAME BECOMES A STORAGE LOCATION.</b>
///
/// <para><b>Why it is a type and not three helper methods.</b> The join between "the name a document
/// cites" and "the location the program uses" has failed <b>five times</b> in this codebase — a slot
/// id, a vector-target prefix, an observable vocabulary, a completion signal, and
/// <c>SlotBinding.ResultRegisterOf</c> matching on the tag while the runner passed the cited name. Each
/// was fixed at its own call site. The sixth is prevented by there being one site, and by
/// <c>JoinSiteWalkTests</c>, which fails when a NEW type reaches <see cref="StorageGroup"/> without
/// declaring itself.</para>
///
/// <para>⚠️ <b>AND A SHARED HELPER IS NECESSARY WITHOUT BEING SUFFICIENT.</b> The harness's own
/// <c>MirroredSignal.JoinKey</c> carries the doc comment <i>"one definition, used by every path that
/// joins the two documents"</i> — and the observe path was not one of them. A comment claiming
/// universality is not a check; the assembly walk is.</para>
///
/// <para><b>WHAT THIS DOES NOT COVER, stated where a reader of results meets it.</b> The walk is over
/// <see cref="StorageGroup"/>. Code that joins names to storage by reading
/// <see cref="ProjectUsageGraph.Usages"/> directly would not be caught, and neither would a join that
/// never leaves the harness assembly.</para>
/// </summary>
public sealed class SignalStorageResolver
{
    private readonly IReadOnlyList<StorageGroup> _groups;
    private readonly SubmissionSignalMap _map;

    private SignalStorageResolver(IReadOnlyList<StorageGroup> groups, SubmissionSignalMap map)
    {
        _groups = groups;
        _map = map;
    }

    public static SignalStorageResolver Over(ProjectUsageGraph graph, SubmissionSignalMap map) =>
        new(StorageGroups.Build(graph), map ?? SubmissionSignalMap.None);

    /// <summary>Every distinct storage the project holds — the denominator a caller may report.</summary>
    public int StorageCount => _groups.Count;

    public SignalStorageResolution Resolve(CitedSignal cited)
    {
        ArgumentNullException.ThrowIfNull(cited);
        var signal = cited.Name;

        // The operator's own list is not a document with a map, and a submission that declared no join
        // at all is the pre-2026-08-17 world this tool has always served. Both take the weaker join,
        // and both SAY which one they took.
        if (cited.Origin == SignalOrigin.OperatorList || !_map.Declared)
        {
            return FromComponents(
                signal,
                SignalJoinKind.ProjectPathMatch,
                MatchByName(signal),
                cited.Origin == SignalOrigin.OperatorList
                    ? "matched against the project's own storage paths — `--signals` is a list of storage paths, so there is no declared join to read"
                    : "matched against the project's own storage paths because the submission declares NO `map.storage` / `map.harnessOnly` at all (contract 2.7). "
                      + "*** THIS INCLUDES A LEAF-NAME MATCH, WHICH THE CONTRACT FORBIDS RELYING ON *** — declare the join and this becomes DeclaredStorage",
                whenEmpty:
                    "no storage path in the project matches this name, AND no join was declared for it. A submission signal is the SPECIFICATION's name by design (D8), "
                    + "not a storage path — declare it in `map.storage`, or declare it `harnessOnly`, which is a positive claim that it occupies no PLC storage.");
        }

        switch (_map.Resolve(signal))
        {
            case DeclaredJoin.Contradiction:
                return new SignalStorageResolution(
                    signal, SignalJoinKind.ContradictoryDeclaration, SignalResolution.Refused, null,
                    Array.Empty<string>(),
                    "declared in BOTH `map.storage` and `map.harnessOnly`. It cannot occupy storage and occupy none, and choosing which "
                    + "declaration to believe would be this tool deciding a question the author has answered twice.");

            case DeclaredJoin.HarnessOnly:
                return new SignalStorageResolution(
                    signal, SignalJoinKind.DeclaredHarnessOnly, SignalResolution.HarnessOnly, null,
                    Array.Empty<string>(),
                    "declared `harnessOnly`: a POSITIVE CLAIM that it occupies no PLC storage, so NO CONFLICT EDGE IS POSSIBLE for it. "
                    + "That is a computed fact rather than a gap, and it does not count against the scope.");

            case DeclaredJoin.InStorage:
                var declared = _map.StorageOf(signal)!;
                return FromComponents(
                    signal,
                    SignalJoinKind.DeclaredStorage,
                    MatchDeclared(declared),
                    $"resolved through the submission's declared join {declared}",
                    whenEmpty:
                        $"`map.storage` declares this signal at {declared} and NO storage in the project corpus is that location. "
                        + "The join WAS stated, so this is not an undeclared signal — either the declaration names a block/path the corpus does not contain, "
                        + "or the corpus is not the program the submission was written against. *** NOT falling back to a name match: that is the aliasing "
                        + "the declared join replaced. ***");

            default:
                // *** A SIGNAL DECLARED TWICE, DIFFERENTLY, IS A DEFECT IN THE DOCUMENT, NOT A GAP IN
                // COVERAGE — so it is Refused rather than Ambiguous, and `--allow-unresolved` cannot
                // reach it. The escape accepts names nobody looked at; it cannot accept a map that
                // answers "where does this live" twice.
                if (_map.AmbiguityOf(signal) is { } ambiguity)
                {
                    return new SignalStorageResolution(
                        signal, SignalJoinKind.ContradictoryDeclaration, SignalResolution.Refused, null,
                        ambiguity.Candidates.Select(c => c.ToString()).OrderBy(c => c, StringComparer.Ordinal).ToList(),
                        "declared MORE THAN ONCE in `map.storage`, with genuinely different storage ("
                        + string.Join(", ", ambiguity.Candidates.Select(c => c.ToString()).OrderBy(c => c, StringComparer.Ordinal))
                        + "). Refused rather than resolved to one of them: picking a candidate is the fiction, not the fix.");
                }

                return new SignalStorageResolution(
                    signal, SignalJoinKind.NotDeclared, SignalResolution.Unresolved, null, Array.Empty<string>(),
                    "the submission declares a `map` and this signal is in NEITHER `storage` nor `harnessOnly`, so nobody stated where it lives. "
                    + "*** NO NAME-SHAPE FALLBACK IS TRIED: *** a map with one hole in it is repaired by filling the hole, not by guessing at it. "
                    + "If it genuinely occupies no PLC storage, `harnessOnly` says so and turns this into a fact.");
        }
    }

    /// <summary>
    /// Turn the storage COMPONENTS a match found into one resolution.
    ///
    /// <para>*** THE TWO REASONS THERE CAN BE MORE THAN ONE GROUP ARE OPPOSITES, AND CONFLATING THEM IS
    /// THE ALIASING DEFECT. *** Several SPELLINGS of one location must be pooled and their writers
    /// unioned; several DIFFERENT locations must be refused naming every candidate. The components
    /// carry that distinction, which is why the match returns them rather than a flat list.</para>
    /// </summary>
    private static SignalStorageResolution FromComponents(
        string signal, SignalJoinKind join, List<List<StorageGroup>> components, string reason, string whenEmpty)
    {
        if (components.Count == 0)
        {
            return new SignalStorageResolution(
                signal, join, SignalResolution.Unresolved, null, Array.Empty<string>(), whenEmpty);
        }

        if (components.Count > 1)
        {
            return new SignalStorageResolution(
                signal, join, SignalResolution.Ambiguous, null,
                components.Select(c => c[0].Path).OrderBy(p => p, StringComparer.Ordinal).ToList(),
                "this matches MORE THAN ONE distinct storage location. Refused rather than resolved to one of them: picking a candidate is exactly "
                + "the aliasing that made cross-check invent multi-writers, and it would put the same fiction into the conflict graph. Qualify the "
                + "signal with its owning block or its full path — or declare it in `map.storage`, where owner and path are separate keys.");
        }

        var matches = components[0];

        // *** THE MULTI-INSTANCE CASE IS REFUSED, NOT POOLED. *** An FB with TWO instance DBs has an
        // internal write landing in BOTH, so unioning the writers of `iDB_A.x` and `iDB_B.x` would
        // manufacture a conflict between two blocks that touch genuinely different storage — the exact
        // fiction the corrected grouping exists to remove. With at most one instance spelling there is
        // nothing to choose between and the union is simply correct.
        var instanceSpellings = matches
            .Where(m => m.Owner is null)
            .Select(m => m.Path)
            .Where(p => matches.Any(other => other.InstanceAliases.Contains(p, StringComparer.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (instanceSpellings.Count > 1)
        {
            return new SignalStorageResolution(
                signal, join, SignalResolution.Ambiguous, null,
                instanceSpellings.OrderBy(p => p, StringComparer.Ordinal).ToList(),
                "one FB-internal member reached through MORE THAN ONE instance DB. Those are different storage, and pooling their writers would "
                + "invent a conflict between blocks that never share a location. Name the instance you mean.");
        }

        // Display prefers the most-referenced spelling, which is the one a reader will recognise; the
        // WRITER SET is the union, because dropping the other spelling's writers silently under-reports
        // the conflict this whole tool exists to find.
        var display = matches[0];
        var writers = matches.SelectMany(m => m.WriterBlocks).Distinct(StringComparer.Ordinal)
            .OrderBy(b => b, StringComparer.Ordinal).ToList();

        var aliasNote = matches.Count > 1
            ? $"; {matches.Count} spellings of one storage ({string.Join(", ", matches.Select(m => m.Path).OrderBy(p => p, StringComparer.Ordinal))}) were pooled, and their writers unioned"
            : string.Empty;

        return new SignalStorageResolution(
            signal, join, SignalResolution.Resolved,
            new ResolvedStorage(display.Path, writers),
            new[] { display.Path },
            reason + aliasNote);
    }

    /// <summary>
    /// The DECLARED join, and it is exact. An owner-qualified declaration matches on (owner, local
    /// path); a global one matches a group's own path or an <c>iDB.&lt;suffix&gt;</c> alias of it —
    /// <b>an identity read off the instance DB's own declared members, never a name shape.</b>
    /// </summary>
    private List<List<StorageGroup>> MatchDeclared(DeclaredStorage declared)
    {
        var path = declared.Path.Trim();

        if (!declared.IsGlobal)
        {
            var owner = declared.Owner!.Trim();
            return Collapse(_groups.Where(g =>
                g.Owner is not null
                && string.Equals(g.Owner, owner, StringComparison.Ordinal)
                && string.Equals(LocalPathOf(g), path, StringComparison.Ordinal)));
        }

        return Collapse(_groups.Where(g =>
            string.Equals(g.Path, path, StringComparison.Ordinal)
            || g.InstanceAliases.Contains(path, StringComparer.Ordinal)));
    }

    /// <summary>
    /// The WEAKER join: the cited name matched against the project's own paths. Exact display path,
    /// exact path within its owner, an <c>iDB.&lt;suffix&gt;</c> alias, or a dotted-suffix match.
    ///
    /// <para>🔴 <b>THE INSTANCE-ALIAS ARM IS THE SECOND MEASURED FAILURE MODE'S REPAIR.</b> A block that
    /// writes its own STATIC member writes it as a bare path, so the corpus holds
    /// <c>FB_X.Member</c> and nothing else — while the harness cites <c>iDB_X.Member</c>, the instance
    /// path. Before this arm existed those two never met, and a slot whose storage tags were supplied
    /// verbatim still resolved NONE of them.</para>
    /// </summary>
    private List<List<StorageGroup>> MatchByName(string signal) =>
        Collapse(_groups.Where(g =>
            string.Equals(g.Path, signal, StringComparison.Ordinal)
            || (g.Owner is not null && string.Equals(LocalPathOf(g), signal, StringComparison.Ordinal))
            || g.InstanceAliases.Contains(signal, StringComparer.Ordinal)
            || g.Path.EndsWith("." + signal, StringComparison.Ordinal)));

    private static string LocalPathOf(StorageGroup group) => group.Path[(group.Owner!.Length + 1)..];

    /// <summary>
    /// Take the groups a match SELECTED and return the whole storage each of them belongs to.
    ///
    /// <para>🔴 <b>THE EXPANSION IS NOT TIDINESS — WITHOUT IT THE WRITER SET IS SHORT.</b> An FB's own
    /// <c>IO.Level</c> and its caller's <c>iDB_X.IO.Level</c> are ONE location under two spellings, and
    /// they arrive as two groups because each is keyed on how it was written. A match that selects only
    /// one of them reports only that one's writers — so a member the FB writes internally and a caller
    /// also drives reads as SINGLE-WRITER, and the conflict this whole tool exists to find is
    /// invisible. Selecting is done by the match; deciding what counts as one storage is done here, in
    /// one place, for both joins.</para>
    ///
    /// <para>The relation is walked TRANSITIVELY. One hop is enough for the shape that exists today,
    /// and stopping there would make the answer depend on which spelling the caller happened to cite.</para>
    /// </summary>
    private List<List<StorageGroup>> Collapse(IEnumerable<StorageGroup> seeds)
    {
        var components = new List<List<StorageGroup>>();

        foreach (var seed in seeds)
        {
            if (components.Any(c => c.Any(m => ReferenceEquals(m, seed))))
            {
                continue;
            }

            var component = new List<StorageGroup>();
            var queue = new Queue<StorageGroup>();
            queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (component.Any(m => ReferenceEquals(m, current)))
                {
                    continue;
                }

                component.Add(current);
                foreach (var other in _groups.Where(g => SameStorage(g, current)))
                {
                    if (!component.Any(m => ReferenceEquals(m, other)))
                    {
                        queue.Enqueue(other);
                    }
                }
            }

            // A seed reached through an earlier seed's closure is the SAME storage, so the components
            // merge rather than becoming a spurious ambiguity.
            var overlapping = components.FirstOrDefault(c => c.Any(m => component.Any(n => ReferenceEquals(m, n))));
            if (overlapping is null)
            {
                components.Add(component);
            }
            else
            {
                overlapping.AddRange(component.Where(m => !overlapping.Any(n => ReferenceEquals(m, n))));
            }
        }

        // Each component is ONE storage under one or more spellings, most-referenced first (that is the
        // display path a reader will recognise). MORE THAN ONE component is genuinely different storage
        // matching one name, and the caller refuses it naming every candidate.
        return components
            .Select(c => c.OrderByDescending(m => m.Writers.Count + m.Readers.Count)
                .ThenBy(m => m.Path, StringComparer.Ordinal).ToList())
            .OrderBy(c => c[0].Path, StringComparer.Ordinal)
            .ToList();
    }

    private static bool SameStorage(StorageGroup a, StorageGroup b) =>
        a.InstanceAliases.Contains(b.Path, StringComparer.Ordinal)
        || b.InstanceAliases.Contains(a.Path, StringComparer.Ordinal);
}
