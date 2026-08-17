using System.Text;
using System.Text.Json;

namespace Converter.ConflictGraph;

/// <summary>
/// One record set, two renderers — the house style. The JSON half is a SUBMISSION FRAGMENT: its
/// <c>conflictEdges</c> key is the exact shape `Harness.Gate.ConflictEdgeDocument` deserializes, so a
/// caller merges it into the submission document rather than translating it.
/// </summary>
public static class ConflictGraphOutputFormatter
{
    public static string FormatJson(ConflictGraphReport report)
    {
        // *** THE ONE DECISION THIS WHOLE FILE EXISTS FOR. ***
        //
        // The consumer reads a MISSING `conflictEdges` key as NOT CHECKED and gates, and reads
        // `conflictEdges: []` as THE POSITIVE CLAIM THAT THE GRAPH RAN AND FOUND NOTHING. Two authors
        // have already refused to write that claim unearned. So when the graph did not run, the key
        // is OMITTED — not written as an empty array, and not written as null, because a null would
        // let a lenient deserializer round it to the empty list and restore the false claim one layer
        // down. `notComputed` carries the reason in its place.
        //
        // `computedConflicts` is NEVER emitted, and that is not an omission either: a bare block name
        // there becomes an edge with `Unstated` provenance, and the consumer's `ProvenanceComplete` is
        // ALL-or-nothing — so emitting the weaker field alongside the stronger one would turn gate 8c
        // to NOT CHECKED for the whole submission. `BlocksForPacking` is derived from the edges, so
        // gate 8 is fully served by `conflictEdges` alone.
        object payload = report.Computed
            ? new
            {
                conflictEdges = report.Edges.Select(e => new
                {
                    blockA = e.BlockA,
                    blockB = e.BlockB,
                    provenance = e.Provenance.ToString(),
                    signal = e.Signal,
                    @class = e.Class.ToString(),
                }),
                derivation = Derivation(report),
            }
            : new
            {
                notComputed = report.NotComputedReason,
                derivation = Derivation(report),
            };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    // Ours, ignored by the consumer's deserializer. It carries what a reader needs to check the edge
    // list instead of trusting it — including the signals that were NOT judged, which is the fact an
    // empty edge list would otherwise hide.
    private static object Derivation(ConflictGraphReport report) => new
    {
        computed = report.Computed,
        corpus = report.CorpusDescription,
        signalsExamined = report.Signals.Count,
        signalsResolved = report.Signals.Count(s => s.Resolution == SignalResolution.Resolved),
        signalsUnresolved = report.Unresolved.Count,
        signalsAmbiguous = report.Ambiguous.Count,
        signalsHarnessOnly = report.HarnessOnly.Count,
        signalsRefused = report.RefusedSignals.Count,
        // WHICH JOIN CARRIED EACH NAME, counted. A tool that reports only THAT a name resolved cannot
        // be asked whether it resolved for the right reason — which is how `map.storage` sat unread.
        joinsUsed = report.Signals.GroupBy(s => s.Join.ToString())
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count()),
        edgesWithoutRecordedProvenance = report.WithoutRecordedProvenance.Count,
        // Only MultiWriter is ever emitted; see ConflictGraphRunner for why a CallGraph edge would
        // silently disable gate 8c rather than add to it.
        provenanceKindsEmitted = report.Edges.Select(e => e.Provenance.ToString()).Distinct().OrderBy(p => p),
        signals = report.Signals.Select(s => new
        {
            signal = s.Signal,
            resolution = s.Resolution.ToString(),
            join = s.Join.ToString(),
            candidates = s.Candidates,
            reason = s.Reason,
        }),
        edges = report.Edges.Select(e => new { storagePath = e.StoragePath, derivation = e.Derivation }),
        warnings = report.Warnings,
    };

    public static string FormatText(ConflictGraphReport report)
    {
        var sb = new StringBuilder();

        if (!report.Computed)
        {
            sb.Append("NOT COMPUTED — no `conflictEdges` key is emitted, so gates 8/8c report NOT CHECKED.\n");
            sb.Append("  ").Append(report.NotComputedReason).Append('\n');
        }

        sb.Append("== CONFLICT EDGES (submission-scoped; provenance MultiWriter only) ==\n");
        foreach (var e in report.Edges)
        {
            sb.Append("  ").Append(e.BlockA).Append(" <-> ").Append(e.BlockB)
                .Append(" on '").Append(e.Signal).Append("' (").Append(e.Provenance).Append(", ").Append(e.Class).Append(")\n")
                .Append("     ").Append(e.Derivation).Append('\n');
        }

        // *** THE JOIN IS PRINTED ON EVERY LINE, RESOLVED OR NOT. *** A resolution that cannot be
        // explained is exactly what let this tool report 70 of 70 unresolved while the field that
        // joins them sat unread in the same document. `[DeclaredStorage]` and `[ProjectPathMatch]` are
        // different claims about the same green.
        sb.Append("== SIGNAL RESOLUTION (every supplied signal, including the ones that produced nothing) ==\n");
        foreach (var s in report.Signals)
        {
            sb.Append("  ").Append(s.Signal).Append(": ").Append(s.Resolution.ToString().ToUpperInvariant())
                .Append(" [join: ").Append(s.Join).Append(']');
            if (s.Candidates.Count > 0)
            {
                sb.Append(" -> ").Append(string.Join(", ", s.Candidates));
            }

            // *** THE REASON IS PRINTED ON EVERY LINE, INCLUDING THE RESOLVED ONES. *** It used to be
            // suppressed for anything that resolved — which hid the two facts a reader most needs about
            // a green: WHICH join carried it, and whether several spellings of one storage were pooled
            // to get there. A resolution nobody can check is the shape this whole defect had.
            sb.Append('\n').Append("     ").Append(s.Reason).Append('\n');
        }

        foreach (var w in report.Warnings)
        {
            sb.Append("INDEX WARNING: ").Append(w).Append('\n');
        }

        // Always printed, zeros included: an edge list that is empty because nothing was examined and
        // one that is empty because nothing conflicts must never look alike.
        sb.Append("SUMMARY: ").Append(report.Edges.Count).Append(" edge(s) over ")
            .Append(report.Signals.Count(s => s.Resolution == SignalResolution.Resolved)).Append('/')
            .Append(report.Signals.Count).Append(" resolved signal(s); ")
            .Append(report.Unresolved.Count).Append(" unresolved, ")
            .Append(report.Ambiguous.Count).Append(" ambiguous, ")
            .Append(report.HarnessOnly.Count).Append(" declared harnessOnly (no edge possible - a computed fact), ")
            .Append(report.RefusedSignals.Count).Append(" refused, ")
            .Append(report.WithoutRecordedProvenance.Count).Append(" edge(s) with unstated provenance/class\n");

        // The join breakdown, always printed. `signalsResolved` alone says a name found storage; only
        // this says whether the DECLARED join or the weaker name match carried it there.
        sb.Append("JOINS: ").Append(string.Join(", ", report.Signals
            .GroupBy(s => s.Join)
            .OrderBy(g => g.Key.ToString(), StringComparer.Ordinal)
            .Select(g => $"{g.Key}={g.Count()}"))).Append('\n');
        sb.Append("CORPUS: ").Append(report.CorpusDescription).Append('\n');
        sb.Append(report.Computed
            ? "EMITTED: `conflictEdges` is present, so an empty list is the earned claim that the graph ran and found nothing.\n"
            : "WITHHELD: `conflictEdges` is ABSENT, which is the weaker and true statement — the graph did not run over a complete scope.\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }
}
