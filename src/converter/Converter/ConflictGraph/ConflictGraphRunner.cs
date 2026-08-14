using System.Text.Json;
using Converter.CrossCheck;
using Converter.Ir;
using Converter.Review;

namespace Converter.ConflictGraph;

/// <summary>
/// Emits the SUBMISSION-SCOPED conflict graph that `Harness.Results.SubmissionGate` gates 8 and 8c
/// actually consume — <c>conflictEdges</c>, in the exact shape `ConflictEdgeDocument` deserializes.
///
/// <para><b>WHY THIS IS NOT `cross-check` WITH A FILTER.</b> `cross-check` emits whole-project
/// <c>multiWriters</c> / <c>deadMembers</c> / <c>ioBoundary</c> / <c>soleWriters</c>: FACT TABLES
/// keyed on a storage path. The gate consumes EDGES between BLOCKS, carrying a provenance and a
/// signal class it has no other source for. Those are not the same shape, and an earlier instruction
/// to bridge them was withdrawn as wrong — the lane that received it correctly supplied nothing
/// rather than reshaping one into the other.</para>
///
/// <para>*** THE ONLY PROVENANCE THIS TOOL EMITS IS <see cref="EdgeProvenance.MultiWriter"/>, AND
/// THAT IS A DELIBERATE REFUSAL, NOT AN OMISSION. *** `CallGraph` is derivable here — the usage graph
/// records every CALL — but a call edge is about no signal, so it could only carry
/// <see cref="EdgeSignalClass.Unstated"/>; and the consumer's `ProvenanceComplete` is ALL-or-nothing,
/// so *** ONE SUCH EDGE WOULD TURN GATE 8c TO NOT CHECKED FOR THE ENTIRE SUBMISSION. *** Emitting a
/// helpful-looking extra edge would silently disable the multi-writer report it was added beside.
/// Call-graph coupling belongs to the author's blacklist, which is add-only for exactly this reason.</para>
///
/// <para><b>Every edge is derived from the CORRECTED storage grouping</b> (<see cref="StorageGroups"/>),
/// so the false cross-block multi-writers that `cross-check` used to manufacture from unqualified
/// FB-internal paths are structurally incapable of becoming edges: a block-local storage has all its
/// writers in one block, and one block is not a conflict.</para>
/// </summary>
public static class ConflictGraphRunner
{
    public static ConflictGraphReport Run(string projectDir, IReadOnlyList<string> signals, bool allowUnresolved)
    {
        var graph = ProjectUsageGraph.Build(projectDir);
        var groups = StorageGroups.Build(graph);
        var harness = HarnessScope.Build(Directory.EnumerateFiles(projectDir, "*.ir"));
        var corpus = $"{harness.CorpusFileCount} project file(s); {harness.UnreadableCorpusFiles.Count} unparseable";

        // Built HERE and threaded through, never held in static state: a cache someone forgets to
        // prime degrades every edge to Unstated, which fails closed but fails SILENTLY — and a guard
        // that can be disconnected without anything going red is the failure mode this repo keeps
        // finding. An explicit parameter cannot be forgotten.
        var blockClasses = BlockClasses(projectDir);

        // *** THE PARTIAL-CORPUS REFUSAL, AND IT IS THE ONE THAT MATTERS MOST HERE. *** A file that
        // did not parse can hold the second writer that makes a signal a conflict, so a graph built
        // over it cannot honestly say "no conflicts". `conflictEdges: []` is a POSITIVE CLAIM at the
        // gate; withholding the key leaves gate 8 NOT CHECKED, which is the correct weaker statement.
        if (graph.Warnings.Count > 0)
        {
            return new ConflictGraphReport(
                Computed: false,
                NotComputedReason:
                    $"{graph.Warnings.Count} project file(s) could not be indexed, so the writer graph is PARTIAL and an unread file could hold the second writer that makes a signal a conflict. "
                    + "Emitting `conflictEdges: []` here would be the positive claim that the graph ran and found nothing. The key is withheld, which leaves gates 8/8c NOT CHECKED — the weaker and true statement.",
                Array.Empty<ConflictEdgeFact>(),
                Array.Empty<SignalResolutionFact>(),
                graph.Warnings,
                corpus);
        }

        if (signals.Count == 0)
        {
            return new ConflictGraphReport(
                Computed: false,
                NotComputedReason:
                    "no submission signals were supplied, so nothing was scoped and nothing was examined. An empty scope produces an empty edge list for a reason that has nothing to do with conflicts (FI-44: empty is not clean).",
                Array.Empty<ConflictEdgeFact>(),
                Array.Empty<SignalResolutionFact>(),
                graph.Warnings,
                corpus);
        }

        var resolutions = new List<SignalResolutionFact>();
        var edges = new List<ConflictEdgeFact>();

        foreach (var signal in signals.Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal))
        {
            var matches = Resolve(groups, signal);

            if (matches.Count == 0)
            {
                resolutions.Add(new SignalResolutionFact(
                    signal,
                    SignalResolution.Unresolved,
                    Array.Empty<string>(),
                    "no storage path in the project matches this name, so no conflict could be looked for. A submission signal is a harness-side logical name and the join to PLC storage is not stated in the submission — this may be a mirror-only signal, or the name may be wrong."));
                continue;
            }

            if (matches.Count > 1)
            {
                resolutions.Add(new SignalResolutionFact(
                    signal,
                    SignalResolution.Ambiguous,
                    matches.Select(m => m.Path).OrderBy(p => p, StringComparer.Ordinal).ToList(),
                    "this name matches MORE THAN ONE distinct storage location. Refused rather than resolved to one of them: picking a candidate is exactly the aliasing that made cross-check invent multi-writers, and it would put the same fiction into the conflict graph. Qualify the signal with its owning block or its full path."));
                continue;
            }

            var group = matches[0];
            resolutions.Add(new SignalResolutionFact(
                signal,
                SignalResolution.Resolved,
                new[] { group.Path },
                group.Owner is null
                    ? "resolved to a global storage path"
                    : $"resolved to a member of {group.Owner}"));

            var blocks = group.WriterBlocks;
            if (blocks.Count < 2)
            {
                continue; // one writer, or one block writing several times — not a cross-block conflict
            }

            var signalClass = ClassifyWriters(blocks, blockClasses);
            for (var i = 0; i < blocks.Count; i++)
            {
                for (var j = i + 1; j < blocks.Count; j++)
                {
                    edges.Add(new ConflictEdgeFact(
                        blocks[i],
                        blocks[j],
                        EdgeProvenance.MultiWriter,
                        signal,
                        signalClass,
                        group.Path,
                        $"both blocks write {group.Path}; {DescribeClass(signalClass, blocks)}"));
                }
            }
        }

        // Unresolved and ambiguous signals are NOT a detail. A submission whose signals mostly failed
        // to resolve gets an edge list that is empty because nothing was looked at, and at the gate
        // that reads exactly like a clean program. So the emission is withheld unless the caller says
        // outright that it accepts the gap — the named-escape shape, never the default.
        var unjudged = resolutions.Count(r => r.Resolution != SignalResolution.Resolved);
        if (unjudged > 0 && !allowUnresolved)
        {
            return new ConflictGraphReport(
                Computed: false,
                NotComputedReason:
                    $"{unjudged} of {resolutions.Count} submission signal(s) could not be resolved to exactly one storage path "
                    + $"({resolutions.Count(r => r.Resolution == SignalResolution.Unresolved)} unresolved, {resolutions.Count(r => r.Resolution == SignalResolution.Ambiguous)} ambiguous). "
                    + "An edge list computed over a scope that was mostly not looked at is empty for a reason that has nothing to do with conflicts, and at the gate that is indistinguishable from a clean program. "
                    + "Fix the signal names, or pass --allow-unresolved to emit the edges for the signals that DID resolve and accept the gap deliberately.",
                edges,
                resolutions,
                graph.Warnings,
                corpus);
        }

        return new ConflictGraphReport(
            Computed: true,
            NotComputedReason: string.Empty,
            edges.OrderBy(e => e.Signal, StringComparer.Ordinal)
                .ThenBy(e => e.BlockA, StringComparer.Ordinal)
                .ThenBy(e => e.BlockB, StringComparer.Ordinal).ToList(),
            resolutions,
            graph.Warnings,
            corpus);
    }

    // Instance aliases of ONE storage are collapsed before ambiguity is declared: an FB's own
    // `IO.HopperBlockedAlarm` and the caller's `iDB_X.IO.HopperBlockedAlarm` are two display paths for
    // one location, and calling that "ambiguous" would refuse a signal that is perfectly determined.
    // This is the join StorageGroups deliberately REPORTS rather than pools, earning its keep.
    private static List<StorageGroup> Resolve(IReadOnlyList<StorageGroup> groups, string signal)
    {
        var matches = groups.Where(g => Matches(g, signal)).ToList();
        if (matches.Count <= 1)
        {
            return matches;
        }

        var collapsed = new List<StorageGroup>();
        foreach (var candidate in matches.OrderByDescending(m => m.Writers.Count + m.Readers.Count))
        {
            if (collapsed.Any(kept => SameStorage(kept, candidate)))
            {
                continue;
            }

            collapsed.Add(candidate);
        }

        return collapsed;
    }

    private static bool SameStorage(StorageGroup a, StorageGroup b) =>
        a.InstanceAliases.Contains(b.Path, StringComparer.Ordinal)
        || b.InstanceAliases.Contains(a.Path, StringComparer.Ordinal);

    // Exact display path, exact unqualified path, or a dotted-suffix match. A suffix match is what
    // lets a submission's logical `HopperBlockedAlarm` find `FB_X.IO.HopperBlockedAlarm`; it is only
    // ever allowed to succeed when it is UNIQUE, which is enforced by the caller.
    private static bool Matches(StorageGroup group, string signal) =>
        string.Equals(group.Path, signal, StringComparison.Ordinal)
        || (group.Owner is not null && string.Equals(group.Path[(group.Owner.Length + 1)..], signal, StringComparison.Ordinal))
        || group.Path.EndsWith("." + signal, StringComparison.Ordinal);

    // *** THE SIGNAL CLASS COMES FROM THE SAME DERIVED HARNESS CLASSIFICATION AS `converter review`'s
    // HARNESS SCOPE — the reserved 9000-9999 block band, read off each writing block's own number.
    // There is no flag and no declaration. *** And its Unclassified state maps to `Unstated`, which
    // the consumer treats as NOT CHECKED: the refusal is carried across rather than resolved into a
    // guess.
    private static EdgeSignalClass ClassifyWriters(
        IReadOnlyList<string> blocks, IReadOnlyDictionary<string, HarnessClass> blockClasses)
    {
        var classes = blocks
            .Select(b => blockClasses.TryGetValue(b, out var c) ? c : HarnessClass.Unclassified)
            .ToList();

        // Any PLANT writer means deliverable code writes this signal, so a multi-writer on it SHIPS.
        if (classes.Any(c => c == HarnessClass.Plant))
        {
            return EdgeSignalClass.Deliverable;
        }

        if (classes.All(c => c == HarnessClass.Harness))
        {
            return EdgeSignalClass.HarnessInstrumentation;
        }

        return EdgeSignalClass.Unstated;
    }

    /// <summary>
    /// Each block's harness classification, read off its OWN number through the reserved band — the
    /// same derivation `converter review`'s harness scope uses, with no flag and no declaration
    /// anywhere. A block that cannot be parsed or classified is absent from this map, which reads back
    /// as <see cref="HarnessClass.Unclassified"/> and becomes <see cref="EdgeSignalClass.Unstated"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, HarnessClass> BlockClasses(string projectDir)
    {
        var classes = new Dictionary<string, HarnessClass>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(projectDir, "*.ir"))
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException)
            {
                continue;
            }

            if (text.StartsWith("DB ", StringComparison.Ordinal)
                || text.StartsWith("TYPE ", StringComparison.Ordinal)
                || text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var block = IrParser.HasSidecarSection(text)
                    ? IrParser.ParseBlock(text).Block
                    : IrParser.ParseBlockWithoutSidecar(text);
                classes[block.Name] = HarnessScope.ClassifyBlock(block).Class;
            }
            catch (Exception ex) when (ex is IrFormatException or SimaticMl.SimaticMlFormatException
                                           or SimaticMl.UnsupportedConstructException or NonReducibleNetworkException)
            {
                // Left unclassified — which becomes Unstated, which fails the gate closed.
            }
        }

        return classes;
    }

    private static string DescribeClass(EdgeSignalClass cls, IReadOnlyList<string> blocks) => cls switch
    {
        EdgeSignalClass.Deliverable => "at least one writer is a plant block, so this multi-writer SHIPS",
        EdgeSignalClass.HarnessInstrumentation => "every writer is a harness-generated block (reserved band 9000-9999), so this conflict is an artefact of testing",
        _ => $"the writers ({string.Join(", ", blocks)}) could not all be classified as plant or harness, so the class is Unstated and the consumer will report NOT CHECKED rather than a clean bill",
    };

    /// <summary>
    /// Read a submission document's signal set: every <c>inputs</c> key and every
    /// <c>expectations[].signal</c>, across all vectors. Deliberately tolerant — this reads ANOTHER
    /// LANE'S evolving schema, so it takes only the two fields it needs and ignores everything else
    /// rather than binding to a type that will move.
    /// </summary>
    public static IReadOnlyList<string> SignalsFromSubmission(string path)
    {
        var signals = new List<string>();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        if (!doc.RootElement.TryGetProperty("vectors", out var vectors) || vectors.ValueKind != JsonValueKind.Array)
        {
            return signals;
        }

        foreach (var vector in vectors.EnumerateArray())
        {
            if (vector.TryGetProperty("inputs", out var inputs) && inputs.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in inputs.EnumerateObject())
                {
                    signals.Add(property.Name);
                }
            }

            if (!vector.TryGetProperty("expectations", out var expectations) || expectations.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var expectation in expectations.EnumerateArray())
            {
                if (expectation.TryGetProperty("signal", out var signal) && signal.ValueKind == JsonValueKind.String)
                {
                    signals.Add(signal.GetString()!);
                }
            }
        }

        return signals;
    }

    /// <summary>One signal name per line; blank lines and `#` comments ignored.</summary>
    public static IReadOnlyList<string> SignalsFromList(string path) =>
        File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();
}
