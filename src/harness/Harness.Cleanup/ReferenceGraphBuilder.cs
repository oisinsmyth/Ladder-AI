using System.Text.Json;
using Harness.Results;

namespace Harness.Cleanup;

/// <summary>Raised when an input file exists but is not the document it was supposed to be.</summary>
public sealed class CleanupInputException(string message) : Exception(message);

/// <summary>
/// Turns <c>converter cross-check --project &lt;ir&gt; --json</c>, the corpus's own
/// <c>INSTANCEOF</c> lines and the admitted vector artifacts into the
/// <see cref="ReferenceGraph"/> DB-7 makes eligibility rest on.
///
/// <para><b>Every rule here is applied in the RETAINING direction except one, and that one is named.</b>
/// An edge this builder adds can only cause an object to be kept; an edge it fails to add is how
/// something still in use gets proposed for deletion. So a path it cannot classify contributes an edge
/// anyway, and the single subtraction — the self-edge — is reported per object rather than applied
/// quietly.</para>
/// </summary>
public static class ReferenceGraphBuilder
{
    /// <summary>One edge: who refers, and in what words.</summary>
    /// <param name="Referrer">The referring OBJECT's name. Kept as its own field, not parsed back out of
    /// <paramref name="Detail"/>, so the self-edge test is an identity comparison rather than a string
    /// search that would misfire on any name that is a substring of another.</param>
    public sealed record Edge(string Referrer, string Detail);

    /// <summary>The graph, plus how it was arrived at. The counts are the per-source denominator.</summary>
    /// <param name="SelfEdgesDropped">
    /// Objects whose referrer set contained THEMSELVES, by name. See <see cref="Build"/> for why this is
    /// the one subtraction, and why it is printed rather than applied silently.
    /// </param>
    public sealed record Result(
        ReferenceGraph Graph,
        IReadOnlyDictionary<string, IReadOnlyList<Edge>> Edges,
        int CallEdges,
        int InstanceDbRootEdges,
        int PathEdges,
        int InstanceOfEdges,
        int VectorEdges,
        IReadOnlyList<string> SelfEdgesDropped);

    /// <summary>
    /// An edge terminating on a name outside <paramref name="corpus"/> is dropped: a referrer of
    /// something that is not in this corpus is not evidence about anything in it.
    /// </summary>
    public static Result Build(
        string crossCheckJson,
        IReadOnlyCollection<IrObject> corpus,
        IReadOnlyList<(string Label, string Text)> vectorArtifacts)
    {
        var names = corpus.Select(o => o.Name).ToHashSet(StringComparer.Ordinal);
        var edges = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        int calls = 0, instanceRoots = 0, paths = 0, instanceOf = 0, vectors = 0;

        void Add(string target, string referrer, string detail)
        {
            if (!names.Contains(target)) return;
            if (!edges.TryGetValue(target, out var set))
                edges[target] = set = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!set.ContainsKey(referrer)) set[referrer] = detail;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(crossCheckJson);
        }
        catch (JsonException ex)
        {
            throw new CleanupInputException($"the cross-check file is not valid JSON: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("siblingRefs", out _))
            {
                // NOT tolerated. A file with no siblingRefs contributes no call edges, which would make
                // every called block read as orphaned — the sharpest form of "deleting an input produces
                // a WRONG conclusion, not a smaller one".
                throw new CleanupInputException(
                    "the cross-check file has no 'siblingRefs' key, so it is not `converter cross-check --json` output. "
                    + "Without the call graph EVERY called block reads as unreferenced, which is a confident false "
                    + "assurance about the one hazard this stage exists to avoid.");
            }

            foreach (var entry in Array(root, "siblingRefs"))
            {
                var block = Str(entry, "block");
                if (block is null) continue;

                foreach (var called in Array(entry, "calls"))
                    if (called.GetString() is { } target) { Add(target, block, $"called by {block}"); calls++; }

                // An instance DB named by a block is a reference to that DB even though it is not a CALL.
                foreach (var idb in Array(entry, "instanceDbRoots"))
                    if (idb.GetString() is { } target) { Add(target, block, $"used as an instance DB by {block}"); instanceRoots++; }
            }

            // The path-bearing fact sets. A path's ROOT is the object; its writers and readers are the
            // referrers. Readers count as much as writers — DB-7 asks whether anything REFERENCES this,
            // not whether anything drives it.
            foreach (var section in new[] { "multiWriters", "soleWriters", "deadMembers" })
            {
                foreach (var entry in Array(root, section))
                {
                    var path = Str(entry, "path");
                    if (path is null) continue;
                    var target = PathRoot(path);

                    foreach (var w in Array(entry, "writers"))
                        if (Str(w, "block") is { } b) { Add(target, b, $"{b} writes {path}"); paths++; }

                    if (entry.TryGetProperty("writer", out var single) && single.ValueKind == JsonValueKind.Object)
                        if (Str(single, "block") is { } b) { Add(target, b, $"{b} writes {path}"); paths++; }

                    foreach (var r in Array(entry, "readers"))
                        if (Str(r, "block") is { } b) { Add(target, b, $"{b} reads {path}"); paths++; }
                }
            }

            foreach (var entry in Array(root, "ioBoundary"))
            {
                var path = Str(entry, "path");
                var block = Str(entry, "block");
                if (path is null || block is null) continue;
                Add(PathRoot(path), block, $"{block} {Str(entry, "direction") ?? "touche"}s {path} at the IO boundary");
                paths++;
            }
        }

        // The corpus's own INSTANCEOF lines. cross-check does not emit this edge, and without it an FB
        // whose instance DB still exists reads as orphaned — proposing the deletion of an FB out from
        // under a live iDB. With it, cleanup CONVERGES ACROSS BATCHES: the iDB goes in one batch and the
        // FB becomes eligible in the next, which is also the only order TIA will accept.
        foreach (var o in corpus)
        {
            if (o.InstanceOf is null) continue;
            Add(o.InstanceOf, o.Name, $"instantiated by {o.Name}");
            instanceOf++;
        }

        // "Models no vector still references" and "blocks not in any admitted test" are DB-7's own words,
        // so the admitted vector artifacts are a reference source in their own right. The match is a
        // whole-word text search over the artifact: coarse, and coarse in the RETAINING direction — it
        // can invent a referrer but never miss one, so its errors keep objects alive.
        foreach (var (label, text) in vectorArtifacts)
        {
            foreach (var o in corpus)
            {
                if (!ContainsWholeWord(text, o.Name)) continue;
                Add(o.Name, $"test-artifact:{label}", $"named in admitted test artifact {label}");
                vectors++;
            }
        }

        // *** THE ONE SUBTRACTION, AND IT IS NOT MERELY CONVENIENT. *** An object that references only
        // ITSELF is referenced by nothing else, which is exactly DB-7's question. It arises constantly
        // and mechanically: an FB's STATIC members are paths rooted at the FB's own name, so every FB
        // with statics "references" itself. Left in, no object in any corpus could ever be eligible and
        // the tool would return a clean-looking zero forever. It is still the only removal-direction rule
        // here, so it is reported by name on every run.
        var selfDropped = new List<string>();
        foreach (var (name, set) in edges)
        {
            if (set.Remove(name)) selfDropped.Add(name);
        }

        var graph = new ReferenceGraph(edges.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlySet<string>)kv.Value.Values.OrderBy(v => v, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal));

        return new Result(
            graph,
            edges.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<Edge>)kv.Value
                    .OrderBy(e => e.Key, StringComparer.Ordinal)
                    .Select(e => new Edge(e.Key, e.Value)).ToArray(),
                StringComparer.Ordinal),
            calls, instanceRoots, paths, instanceOf, vectors,
            selfDropped.OrderBy(s => s, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// <c>DB_Alarms.ShredderAlarm0.%X0</c> and <c>DB_Input.Test[0].Reading</c> both root on the first
    /// segment. Subscripts are stripped for the reason <c>reachable-state</c> strips them: an element
    /// index would disconnect a reference that is plainly to the same object.
    /// </summary>
    public static string PathRoot(string path)
    {
        var cut = path.IndexOfAny(new[] { '.', '[' });
        return cut < 0 ? path : path[..cut];
    }

    private static bool ContainsWholeWord(string haystack, string needle)
    {
        if (needle.Length == 0) return false;
        var from = 0;
        while (from <= haystack.Length - needle.Length)
        {
            var at = haystack.IndexOf(needle, from, StringComparison.Ordinal);
            if (at < 0) return false;
            var beforeOk = at == 0 || !IsWordChar(haystack[at - 1]);
            var afterAt = at + needle.Length;
            var afterOk = afterAt >= haystack.Length || !IsWordChar(haystack[afterAt]);
            if (beforeOk && afterOk) return true;
            from = at + 1;
        }

        return false;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static IEnumerable<JsonElement> Array(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : Enumerable.Empty<JsonElement>();

    private static string? Str(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
