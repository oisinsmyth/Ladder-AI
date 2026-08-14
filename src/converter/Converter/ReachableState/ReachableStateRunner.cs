using System.Security.Cryptography;
using System.Text;
using Converter.CrossCheck;

namespace Converter.ReachableState;

/// <summary>
/// Computes D9's reachable-state closure per block, from the IR alone.
///
/// <para><b>WHAT "REACHABLE STATE" IS TAKEN TO MEAN, AND WHY.</b> Every storage location a block
/// TOUCHES — read or written — unioned over the transitive closure of the blocks it CALLs. Reads
/// count, and that is not a conservative widening: two slots cannot share a signal one of them drives
/// and the other observes, whichever way round. `SlotConflictDerivation.OverlappingReachableState`
/// then makes the edge by set intersection, so the question this answers is exactly *"could these two
/// tests see each other?"*, never *"would they collide on a write?"*.</para>
///
/// <para>*** THE DIRECTION OF ERROR IS CHOSEN DELIBERATELY AND IT IS NOT SYMMETRIC. *** An
/// over-large closure separates two slots that could have run together — concurrency lost, nothing
/// unsafe. An under-large one produces the positive claim <i>"the slots are disjoint on every computed
/// relation"</i> about a hazard it cannot see, and puts two agents on one FB instance. Where the two
/// are traded against each other here, the larger closure wins.</para>
///
/// <para><b>WHAT THIS DOES NOT DO.</b> It does not close UPWARD through callers. A block's closure is
/// what IT can reach, not what the program can reach through it — closing upward from any leaf of a
/// plant program reaches OB1 and therefore everything, which would make every pair of slots conflict
/// and the whole relation useless. Coupling that exists only in a common caller is the author's
/// blacklist to state (§2.5 / D22, add-only).</para>
/// </summary>
public static class ReachableStateRunner
{
    /// <summary>
    /// Computes the closure for every block in <paramref name="projectDir"/>, or for
    /// <paramref name="blocks"/> alone when that is non-empty.
    /// </summary>
    /// <param name="nowUtc">Injected so the provenance string is testable. Defaults to now.</param>
    public static ReachableStateReport Run(
        string projectDir,
        IReadOnlyList<string> blocks,
        DateTimeOffset? nowUtc = null)
    {
        var graph = ProjectUsageGraph.Build(projectDir);
        var files = Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var corpusHash = CorpusHash(files);
        var stamp = (nowUtc ?? DateTimeOffset.UtcNow).UtcDateTime
            .ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        var provenance = $"converter reachable-state @ {corpusHash}, {stamp}";
        var corpus = $"{files.Count} .ir file(s); {graph.BlockNames.Count} block(s); {graph.Warnings.Count} unparseable";

        // *** THE PARTIAL-CORPUS REFUSAL. *** A file that did not parse can hold a reference that
        // makes two closures intersect, so a closure computed over the rest cannot honestly claim
        // disjointness. `reachableState: []` — and any set at all — is a POSITIVE CLAIM downstream;
        // withholding leaves the slot with no provenance, which admission refuses. Same shape and
        // same reasoning as ConflictGraphRunner's, deliberately.
        if (graph.Warnings.Count > 0)
        {
            return new ReachableStateReport(
                Computed: false,
                NotComputedReason:
                    $"{graph.Warnings.Count} project file(s) could not be indexed, so the reference graph is PARTIAL. "
                    + "An unread file can hold the reference that makes two closures intersect, and a closure computed "
                    + "over the rest would be the positive claim that two slots are disjoint. Withheld: an under-large "
                    + "closure fails toward putting two agents on one instance (FI-44: empty is not clean).",
                Array.Empty<BlockReachableState>(),
                graph.Warnings,
                corpus,
                corpusHash,
                Provenance: string.Empty);
        }

        // FI-44. A named block that is not here is UNJUDGEABLE, never clean — computing an empty
        // closure for it would report the most independent slot in the set.
        var missing = blocks
            .Where(b => !graph.BlockNames.Contains(b, StringComparer.Ordinal))
            .OrderBy(b => b, StringComparer.Ordinal)
            .ToList();
        if (missing.Count > 0)
        {
            return new ReachableStateReport(
                Computed: false,
                NotComputedReason:
                    $"--block named {missing.Count} block(s) the corpus does not contain: {string.Join(", ", missing)}. "
                    + "A block that is not here has no closure to compute, and emitting an empty one would report it as "
                    + "the most independent slot in the set (FI-44).",
                Array.Empty<BlockReachableState>(),
                graph.Warnings,
                corpus,
                corpusHash,
                Provenance: string.Empty);
        }

        // Nothing at all to examine is not a clean project — it is an empty or wrongly-aimed directory.
        if (graph.BlockNames.Count == 0)
        {
            return new ReachableStateReport(
                Computed: false,
                NotComputedReason:
                    $"the corpus contains no blocks at all ({files.Count} .ir file(s) read). Nothing was examined, which "
                    + "is not the same as nothing being reachable.",
                Array.Empty<BlockReachableState>(),
                graph.Warnings,
                corpus,
                corpusHash,
                Provenance: string.Empty);
        }

        var own = OwnTouches(graph, out var aliasesByBlock);
        var callees = graph.Calls
            .GroupBy(c => c.Block, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(c => c.CalledBlock).Distinct(StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        var wanted = blocks.Count > 0
            ? blocks.Distinct(StringComparer.Ordinal).OrderBy(b => b, StringComparer.Ordinal).ToList()
            : graph.BlockNames.OrderBy(b => b, StringComparer.Ordinal).ToList();

        var results = new List<BlockReachableState>();
        foreach (var block in wanted)
        {
            var closure = CallClosureOf(block, callees);
            var unresolved = closure
                .Where(b => !graph.BlockNames.Contains(b, StringComparer.Ordinal))
                .OrderBy(b => b, StringComparer.Ordinal)
                .ToList();

            var reached = closure.OrderBy(b => b, StringComparer.Ordinal).ToList();

            if (unresolved.Count > 0)
            {
                results.Add(new BlockReachableState(
                    block,
                    Computed: false,
                    NotComputedReason:
                        $"the call closure reaches {unresolved.Count} block(s) the corpus does not contain "
                        + $"({string.Join(", ", unresolved)}), and what they touch is unknown. The closure is therefore "
                        + "INCOMPLETE, and an incomplete closure fails toward FALSE DISJOINTNESS — it would claim two "
                        + "slots cannot see each other through storage it never looked at.",
                    Array.Empty<string>(),
                    reached,
                    unresolved,
                    Array.Empty<string>(),
                    Provenance: string.Empty));
                continue;
            }

            var state = new SortedSet<string>(StringComparer.Ordinal);
            var aliases = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var member in closure)
            {
                if (own.TryGetValue(member, out var touches))
                {
                    state.UnionWith(touches);
                }

                if (aliasesByBlock.TryGetValue(member, out var applied))
                {
                    aliases.UnionWith(applied);
                }
            }

            results.Add(new BlockReachableState(
                block,
                Computed: true,
                NotComputedReason: string.Empty,
                state.ToList(),
                reached,
                Array.Empty<string>(),
                aliases.ToList(),
                provenance));
        }

        return new ReachableStateReport(
            Computed: true,
            NotComputedReason: string.Empty,
            results,
            graph.Warnings,
            corpus,
            corpusHash,
            provenance);
    }

    /// <summary>
    /// Every storage key each block touches ITSELF, before the call closure is applied.
    /// </summary>
    private static Dictionary<string, SortedSet<string>> OwnTouches(
        ProjectUsageGraph graph,
        out Dictionary<string, SortedSet<string>> aliasesByBlock)
    {
        var own = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        aliasesByBlock = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        // A block with no usages at all still gets an entry, so its closure is a computed EMPTY set
        // rather than a missing one.
        foreach (var name in graph.BlockNames)
        {
            own[name] = new SortedSet<string>(StringComparer.Ordinal);
            aliasesByBlock[name] = new SortedSet<string>(StringComparer.Ordinal);
        }

        foreach (var (block, _, path, _) in graph.Flat)
        {
            if (!own.TryGetValue(block, out var set))
            {
                set = own[block] = new SortedSet<string>(StringComparer.Ordinal);
                aliasesByBlock[block] = new SortedSet<string>(StringComparer.Ordinal);
            }

            var keys = CanonicalKeys(graph, block, path, out var note);
            foreach (var key in keys)
            {
                set.Add(key);
            }

            if (note is not null)
            {
                aliasesByBlock[block].Add(note);
            }
        }

        return own;
    }

    /// <summary>
    /// The STORAGE-IDENTITY key(s) a reference resolves to. Usually one; a bare instance-DB root
    /// expands to that instance's declared members.
    ///
    /// <para>🔴 <b>RULE 1: CANONICALISE INSTANCE ALIASES BEFORE INTERSECTING.</b> An FB addresses its
    /// own interface member with no root (<c>IO.Step</c>) and a caller addresses the same storage as
    /// <c>iDB_X.IO.Step</c>. Left alone those are two different strings, so a slot testing the FB and
    /// a slot testing its caller would read as DISJOINT while driving one location.</para>
    ///
    /// <para>⚠️ <b>THIS POOLS WHERE <c>ProjectUsageGraph.QualifiedPath</c> DELIBERATELY DOES NOT, AND
    /// THE REASON IS THAT THE ERROR POINTS THE OTHER WAY HERE.</b> That method refuses to pool an
    /// FB-internal member with its `iDB.` form because an FB with TWO instance DBs has one internal
    /// write landing in BOTH, and pooling INVENTS a multi-writer — a false accusation against correct
    /// work. In this relation the same pooling can only ADD an overlap, i.e. SEPARATE two slots that
    /// might have run together: concurrency lost, nothing unsafe. *Computed disjointness is the FLOOR*
    /// (D22), so erring toward more of it is the direction the design asks for. Every rewrite applied
    /// is reported in <c>AliasCanonicalisations</c> rather than done silently.</para>
    /// </summary>
    internal static IReadOnlyList<string> CanonicalKeys(
        ProjectUsageGraph graph, string block, string path, out string? aliasNote)
    {
        aliasNote = null;

        // Array subscripts are stripped: `DB_Input.Test[3]` and `DB_Input.Test[5]` are one declared
        // member of one type, and two tests driving different elements of one injection array are not
        // independent of each other. This is also the granularity the D9 finding was recorded at by
        // hand — "one `DB_Input.Test[]`".
        var stripped = ProjectUsageGraph.StripSubscripts(path);

        // A root the block DECLARES cannot be the same storage as an identically-spelled root in
        // another block. Checked first: it is the stronger claim, and it wins over a name that merely
        // happens to match an instance DB.
        if (graph.OwnerOf(block, stripped) is { } owner)
        {
            return new[] { owner + "|" + stripped };
        }

        if (graph.CanonicalizeInstancePath(stripped) is { } canonical)
        {
            aliasNote = stripped + " -> " + canonical;
            return new[] { canonical };
        }

        // A BARE instance-DB root — the instance argument of a CALL. It names the WHOLE instance, so
        // it expands to that instance's declared members rather than staying an opaque token that
        // would fail to intersect `<FB>|<member>` from either side. The CALL edge usually carries the
        // same information; this closes the case where it does not.
        if (graph.InstanceToFb.TryGetValue(stripped, out var fb))
        {
            var members = graph.InstanceMemberPaths
                .Where(p => string.Equals(p.InstanceDb, stripped, StringComparison.Ordinal))
                .Select(p => fb + "|" + p.Suffix)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(m => m, StringComparer.Ordinal)
                .ToList();

            if (members.Count > 0)
            {
                aliasNote = stripped + " -> " + members.Count + " member(s) of " + fb;
                return members;
            }

            // An instance with no declared members: fall through to the verbatim root rather than
            // return nothing. An expansion that silently yields the empty set is a reference DROPPED.
        }

        return new[] { stripped };
    }

    /// <summary>
    /// The block itself plus everything reachable from it through CALL, transitively. Cycle-safe by
    /// the visited set.
    /// </summary>
    internal static IReadOnlyCollection<string> CallClosureOf(
        string block, IReadOnlyDictionary<string, IReadOnlyList<string>> callees)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { block };
        var queue = new Queue<string>();
        queue.Enqueue(block);

        while (queue.Count > 0)
        {
            if (!callees.TryGetValue(queue.Dequeue(), out var next))
            {
                continue;
            }

            foreach (var callee in next.Where(c => seen.Add(c)))
            {
                queue.Enqueue(callee);
            }
        }

        return seen;
    }

    /// <summary>
    /// A digest over the corpus — every .ir file's name and content, in a fixed order. Carried in the
    /// provenance so a closure computed against a DIFFERENT corpus is a distinguishable fact from one
    /// nobody computed, rather than both being "an empty set with a string beside it".
    /// </summary>
    internal static string CorpusHash(IReadOnlyList<string> files)
    {
        using var sha = SHA256.Create();
        var builder = new StringBuilder();

        foreach (var file in files)
        {
            builder.Append(Path.GetFileName(file)).Append('\n');
            try
            {
                builder.Append(Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(file)))).Append('\n');
            }
            catch (IOException)
            {
                builder.Append("UNREADABLE\n");
            }
        }

        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())))[..16];
    }
}
