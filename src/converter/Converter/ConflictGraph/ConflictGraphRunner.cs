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
/// <para><b>Every edge is derived from the CORRECTED storage grouping</b> (<c>StorageGroups</c>),
/// so the false cross-block multi-writers that `cross-check` used to manufacture from unqualified
/// FB-internal paths are structurally incapable of becoming edges: a block-local storage has all its
/// writers in one block, and one block is not a conflict.</para>
///
/// <para>🔴 <b>AND IT READS THE DECLARED JOIN — WHICH IT DID NOT UNTIL 2026-08-17.</b> A submission
/// speaks the SPECIFICATION's vocabulary by design (D8: agents cite tag names, never registers), and
/// this runner fed those names straight into a resolver expecting STORAGE PATHS. <b>On a real
/// submission that was 70 of 70 unresolved — and the field carrying the join, <c>map.storage</c>, was
/// already in the document and read by nobody.</b> The whole name→storage step now lives in
/// <see cref="SignalStorageResolver"/>, which is the single site the fifth instance of this seam
/// argued for, and every resolution reports WHICH JOIN carried it.</para>
/// </summary>
public static class ConflictGraphRunner
{
    /// <summary>
    /// The pre-2026-08-17 entry point: bare names, no declared join. Kept because <c>--signals</c> is
    /// exactly this — an operator's list of storage paths — and because the CONVERSE has to stay
    /// testable: <b>a signal set that already resolved must still resolve.</b>
    /// </summary>
    public static ConflictGraphReport Run(string projectDir, IReadOnlyList<string> signals, bool allowUnresolved) =>
        Run(
            projectDir,
            signals.Select(s => new CitedSignal(s, SignalOrigin.OperatorList)).ToList(),
            SubmissionSignalMap.None,
            allowUnresolved);

    public static ConflictGraphReport Run(
        string projectDir,
        IReadOnlyList<CitedSignal> signals,
        SubmissionSignalMap map,
        bool allowUnresolved)
    {
        var graph = ProjectUsageGraph.Build(projectDir);
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

        var resolver = SignalStorageResolver.Over(graph, map);
        var resolutions = new List<SignalResolutionFact>();
        var edges = new List<ConflictEdgeFact>();

        foreach (var cited in signals
                     .DistinctBy(s => (s.Name, s.Origin))
                     .OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            // *** THE ONE JOIN. *** Every name-to-storage step in this assembly goes through here; see
            // SignalStorageResolver for why it is a type, and JoinSiteWalkTests for what stops a sixth
            // call site being written beside it.
            var resolved = resolver.Resolve(cited);
            resolutions.Add(new SignalResolutionFact(
                resolved.Signal, resolved.Resolution, resolved.Candidates, resolved.Reason, resolved.Join));

            if (resolved.Storage is not { } storage)
            {
                continue;
            }

            var blocks = storage.WriterBlocks;
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
                        cited.Name,
                        signalClass,
                        storage.Path,
                        $"both blocks write {storage.Path}; {DescribeClass(signalClass, blocks)}"));
                }
            }
        }

        // *** A CONTRADICTORY DECLARATION IS NOT A GAP, SO THE NAMED ESCAPE DOES NOT COVER IT. ***
        // `--allow-unresolved` means "accept that some names were not looked at". A signal declared in
        // both `storage` and `harnessOnly` — or a `map` entry that could not be read at all — is a
        // document answering one question twice, and choosing a half would be this tool deciding it.
        var refused = resolutions.Where(r => r.Resolution == SignalResolution.Refused).ToList();
        if (refused.Count > 0 || map.Rejections.Count > 0)
        {
            return new ConflictGraphReport(
                Computed: false,
                NotComputedReason:
                    $"the submission's `map` cannot be read as ONE statement: {refused.Count} signal(s) declared contradictorily "
                    + $"({string.Join(" | ", refused.Select(r => $"'{r.Signal}': {r.Reason}"))}), {map.Rejections.Count} malformed entr(y/ies). "
                    + (map.Rejections.Count > 0 ? string.Join(" | ", map.Rejections) + " " : string.Empty)
                    + "*** --allow-unresolved DOES NOT COVER THIS: *** it accepts names nobody looked at, not a declaration that contradicts itself.",
                edges,
                resolutions,
                graph.Warnings,
                corpus);
        }

        // Unresolved and ambiguous signals are NOT a detail. A submission whose signals mostly failed
        // to resolve gets an edge list that is empty because nothing was looked at, and at the gate
        // that reads exactly like a clean program. So the emission is withheld unless the caller says
        // outright that it accepts the gap — the named-escape shape, never the default.
        //
        // `harnessOnly` is deliberately NOT counted here: it is a positive claim that no edge is
        // possible, which is a computed fact rather than a gap. Counting it would refuse a submission
        // whose author did exactly what the contract asks.
        var unjudged = resolutions.Count(r => r.Resolution is not (SignalResolution.Resolved or SignalResolution.HarnessOnly));
        if (unjudged > 0 && !allowUnresolved)
        {
            var notDeclared = resolutions.Count(r => r.Join == SignalJoinKind.NotDeclared);
            return new ConflictGraphReport(
                Computed: false,
                NotComputedReason:
                    $"{unjudged} of {resolutions.Count} submission signal(s) could not be resolved to exactly one storage path "
                    + $"({resolutions.Count(r => r.Resolution == SignalResolution.Unresolved)} unresolved, {resolutions.Count(r => r.Resolution == SignalResolution.Ambiguous)} ambiguous"
                    + $"; {notDeclared} of them stated no join at all). "
                    + "An edge list computed over a scope that was mostly not looked at is empty for a reason that has nothing to do with conflicts, and at the gate that is indistinguishable from a clean program. "
                    + (notDeclared > 0
                        ? "*** A SUBMISSION SIGNAL IS THE SPECIFICATION'S NAME, NOT A STORAGE PATH (contract 2.8). *** Declare each one in `map.storage` as { owner?, path }, or in `map.harnessOnly` if it occupies no PLC storage — the second is a positive claim and turns a NOT CHECKED into a fact. "
                        : string.Empty)
                    + "Or pass --allow-unresolved to emit the edges for the signals that DID resolve and accept the gap deliberately.",
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
