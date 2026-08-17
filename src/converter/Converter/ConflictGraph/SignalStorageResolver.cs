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

    /// <summary>
    /// 🔴 <b>EVERY PLACEMENT OF EVERY FB — an instance DB <i>or</i> a MULTI-INSTANCE, and the second
    /// half is what case B turned on.</b>
    ///
    /// <para>A multi-instance is an FB placed as a STATIC of another FB. It is a real instance with real
    /// per-instance state and it has no DB of its own, so an index built from instance DBs alone cannot
    /// see it. <see cref="ProjectUsageGraph"/> has resolved them to a fixpoint since FI-50 — <b>nested
    /// ones included</b>, which is why <c>iDB_Outer.Inner</c> is a key here — and this resolver simply
    /// had never asked. Same lesson <c>undriven-scan</c> learned in FI-50, one tool later.</para>
    /// </summary>
    private readonly Dictionary<string, List<string>> _placements;

    private SignalStorageResolver(
        IReadOnlyList<StorageGroup> groups, SubmissionSignalMap map, Dictionary<string, List<string>> placements)
    {
        _groups = groups;
        _map = map;
        _placements = placements;
    }

    public static SignalStorageResolver Over(ProjectUsageGraph graph, SubmissionSignalMap map)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var placements = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (placement, fb) in graph.InstanceToFb.Select(kv => (kv.Key, kv.Value))
                     .Concat(graph.MultiInstanceToFb.Select(kv => (kv.Key, kv.Value))))
        {
            if (!placements.TryGetValue(fb, out var list))
            {
                placements[fb] = list = new List<string>();
            }

            list.Add(placement);
        }

        foreach (var list in placements.Values)
        {
            list.Sort(StringComparer.Ordinal);
        }

        return new SignalStorageResolver(StorageGroups.Build(graph), map ?? SubmissionSignalMap.None, placements);
    }

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
            return FromLocations(
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
                return FromLocations(
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
    /// Turn the storage LOCATIONS a match found into one resolution.
    ///
    /// <para>*** THE TWO REASONS A NAME CAN REACH SEVERAL THINGS ARE OPPOSITES, AND CONFLATING THEM IS
    /// THE ALIASING DEFECT. *** Several SPELLINGS of one location must be pooled and their writers
    /// unioned; several DIFFERENT locations must be refused naming every candidate. Working in
    /// LOCATIONS rather than in groups keeps those apart <b>structurally</b>: a group is a reference at
    /// some level of qualification, and the locations it COVERS are the answer.</para>
    /// </summary>
    private SignalStorageResolution FromLocations(
        string signal, SignalJoinKind join, List<string> locations, string reason, string whenEmpty)
    {
        if (locations.Count == 0)
        {
            return new SignalStorageResolution(
                signal, join, SignalResolution.Unresolved, null, Array.Empty<string>(), whenEmpty);
        }

        if (locations.Count > 1)
        {
            return new SignalStorageResolution(
                signal, join, SignalResolution.Ambiguous, null,
                locations.OrderBy(p => p, StringComparer.Ordinal).ToList(),
                "this names MORE THAN ONE distinct storage location — most often an FB member reached through every PLACEMENT of that FB, because "
                + "nothing said WHICH placement. Refused rather than resolved to one of them: two placements of one FB are different memory, and "
                + "pooling them would invent a conflict between blocks that never share a location. *** NAME THE PLACEMENT AND THIS RESOLVES: *** "
                + "declare the fully-qualified instance path in `map.storage` (`<instance>.<member>`, or `<outerInstance>.<multiInstance>.<member>` "
                + "for an FB placed as a static of another FB).");
        }

        var location = locations[0];
        var matches = _groups.Where(g => Covers(g, location))
            .OrderByDescending(g => g.Writers.Count + g.Readers.Count)
            .ThenBy(g => g.Path, StringComparer.Ordinal)
            .ToList();

        if (matches.Count == 0)
        {
            return new SignalStorageResolution(
                signal, join, SignalResolution.Unresolved, null, Array.Empty<string>(), whenEmpty);
        }

        // *** THE WRITER SET IS THE UNION OF EVERY REFERENCE THAT REACHES THIS ONE LOCATION, AND
        // NOTHING ELSE. *** An FB writing its own `IO.Level`, its owner writing `Valve.IO.Level`, and a
        // caller writing `iDB_X.IO.Level` are three references to the SAME memory, and dropping any of
        // them under-reports the conflict this tool exists to find. What is NOT unioned is a sibling
        // placement: `iDB_A.IO.Level` and `iDB_B.IO.Level` are different memory and never pool — a
        // property of working in locations, rather than a rule bolted on afterwards.
        var writers = matches.SelectMany(m => m.WriterBlocks).Distinct(StringComparer.Ordinal)
            .OrderBy(b => b, StringComparer.Ordinal).ToList();

        // The reaching references are named on EVERY resolution, not only where several were pooled.
        // The reported location is a COMPUTED thing and the references are the CODE — without them a
        // reader cannot check the answer, which is the property this whole change is about.
        return new SignalStorageResolution(
            signal, join, SignalResolution.Resolved,
            new ResolvedStorage(location, writers),
            new[] { location },
            reason
            + $"; storage location {location}, reached by {matches.Count} reference(s) ("
            + string.Join(", ", matches.Select(m => m.Path).OrderBy(p => p, StringComparer.Ordinal)) + ")"
            + (matches.Count > 1 ? ", whose writers are unioned" : string.Empty));
    }

    /// <summary>
    /// 🔴 <b>THE LOCATIONS ONE REFERENCE COVERS — the whole model, in one method.</b>
    ///
    /// <para>A group is not a location; it is a REFERENCE written at some level of qualification, and
    /// what it covers depends on that level:</para>
    /// <list type="bullet">
    /// <item>A <b>global</b> reference (<c>DB_X.Member</c>, a PLC tag, <c>iDB_A.IO.Level</c>,
    /// <c>iDB_A.Valve.IO.Level</c>) is already fully qualified and covers exactly itself.</item>
    /// <item>A <b>block-local</b> reference — an FB addressing its own member as a bare path — covers
    /// <b>one location per PLACEMENT of that FB</b>. The FB's write executes once per placement and
    /// lands in each one's own memory.</item>
    /// <item>A block-local reference in an FB with <b>no placement at all</b> covers a single
    /// declaration-site location. Reporting nothing there is FI-44 in a new costume: a block written
    /// before its caller still has storage.</item>
    /// </list>
    ///
    /// <para>*** THIS IS A CONTAINMENT RELATION AND DELIBERATELY NOT AN EQUIVALENCE. *** It replaced a
    /// transitive closure over "these two spellings name one storage", which is what case A was:
    /// <c>FB_X|IO.Cmd</c> names the same storage as <c>iDB_A.IO.Cmd</c> and as <c>iDB_B.IO.Cmd</c>, but
    /// <c>iDB_A.IO.Cmd</c> is NOT <c>iDB_B.IO.Cmd</c> — <b>the relation is not transitive, and closing
    /// over it pooled five placements of one FB and then refused to guess between them.</b> The refusal
    /// was right and the pooling should never have happened: a declaration that names the placement has
    /// already answered the question.</para>
    /// </summary>
    private IEnumerable<string> LocationsOf(StorageGroup group) =>
        group.Owner is null
            ? new[] { group.Path }
            : LocationsOfMember(group.Owner, LocalPathOf(group));

    private IEnumerable<string> LocationsOfMember(string owner, string suffix) =>
        _placements.TryGetValue(owner, out var placements) && placements.Count > 0
            ? placements.Select(p => p + "." + suffix)
            : new[] { owner + "." + suffix };

    private bool Covers(StorageGroup group, string location) =>
        group.Owner is null
            ? string.Equals(group.Path, location, StringComparison.Ordinal)
            : LocationsOfMember(group.Owner, LocalPathOf(group))
                .Any(l => string.Equals(l, location, StringComparison.Ordinal));

    /// <summary>
    /// The DECLARED join, and it is exact.
    ///
    /// <para>A GLOBAL declaration <b>is</b> the location — including a fully-qualified instance path and
    /// a nested multi-instance path, which is exactly what case B declared and what nothing here used to
    /// look for. Whether anything in the corpus reaches it is then a question about the corpus, and an
    /// empty answer is the honest <i>"the join was stated and no storage is that location"</i>.</para>
    ///
    /// <para>An OWNER-QUALIFIED declaration names a member of a CLASS, so it names one location per
    /// placement — <b>and more than one placement is a genuine ambiguity the author can settle by naming
    /// the instance.</b> That is a refusal about the declaration, not a claim about the plant.</para>
    /// </summary>
    private List<string> MatchDeclared(DeclaredStorage declared)
    {
        var path = declared.Path.Trim();

        return declared.IsGlobal
            ? new List<string> { path }
            : LocationsOfMember(declared.Owner!.Trim(), path).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The WEAKER join: the cited name matched against the project's own references.
    ///
    /// <para>Two arms, and the first is the one that matters. <b>A name that is already a LOCATION —
    /// anything some reference covers — is taken as that location and nothing else</b>, which is how a
    /// fully-qualified instance path resolves even when the corpus only ever writes the member from
    /// inside its owning block. Only if that fails does the name-SHAPE arm run: exact reference path,
    /// exact path within its owner, or a dotted-suffix match.</para>
    ///
    /// <para>🔴 <b>THE SUFFIX ARM IS A NAME SHAPE AND THE CONTRACT FORBIDS RELYING ON IT.</b> It survives
    /// only for <c>--signals</c> and for a submission that declares no map at all, and every line that
    /// used it says so.</para>
    /// </summary>
    private List<string> MatchByName(string signal)
    {
        if (_groups.Any(g => Covers(g, signal)))
        {
            return new List<string> { signal };
        }

        return _groups
            .Where(g =>
                string.Equals(g.Path, signal, StringComparison.Ordinal)
                || (g.Owner is not null && string.Equals(LocalPathOf(g), signal, StringComparison.Ordinal))
                || g.Path.EndsWith("." + signal, StringComparison.Ordinal))
            .SelectMany(LocationsOf)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string LocalPathOf(StorageGroup group) => group.Path[(group.Owner!.Length + 1)..];
}
