using System.Text.RegularExpressions;
using Converter.SimaticMl;

namespace Converter.Ir;

// A rung is one connected graph, and TIA draws each rung's parts contiguously. Readable IR can still be
// written with an independent statement placed between two statements of one rung: a statement inside a
// timer's {split}/{recv} group, or between a TON and the coil its Q leads (2026-10-29, a live run). No
// drawing keeps that order, so `to-xml` used to emit the in-between rung after the whole connected rung,
// silently changing the scan order (`ir/SPEC.md`, "Statement order within one network").
//
// The check builds the synthesized network, reads it back exactly as `to-ir` would, and compares statement
// order. It runs only for a network whose IR states an explicit order: canonical kind order states none,
// and its regrouping is the documented behaviour.
internal static class RungOrderCheck
{
    private static readonly Regex Markers = new(@"\{(split|recv) \d+\}", RegexOptions.Compiled);

    public static void Verify(IrNetwork network, NetworkSidecar sidecar)
    {
        if (network.StatementOrder is null)
        {
            return;
        }

        IReadOnlyList<string> drawn;
        try
        {
            // Through the writer and back: FlgNetWriter's rung grouping is where the order is decided.
            var built = FlgNetParser.Parse(FlgNetWriter.Write(FlgNetBuilder.Build(network, sidecar)));
            var reduced = GraphReducer.Reduce(built, network.Number, network.Title, sidecar.CompileUnitUId, sourceStatementOrder: true);
            drawn = StatementLines(reduced.Network);
        }
        catch (Exception ex) when (ex is IrFormatException or NonReducibleNetworkException or UnsupportedConstructException or SimaticMlFormatException)
        {
            return; // not this check's question: the build or the read-back reports its own problem
        }

        var written = StatementLines(network);
        var writtenKeys = written.Select(Key).ToList();
        var drawnKeys = drawn.Select(Key).ToList();

        // Only a pure permutation is a reorder. A read-back that also renormalises a statement's text
        // cannot be matched line for line, and this check makes no claim about it.
        if (writtenKeys.SequenceEqual(drawnKeys)
            || !writtenKeys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(drawnKeys.OrderBy(k => k, StringComparer.Ordinal)))
        {
            return;
        }

        var first = Enumerable.Range(0, writtenKeys.Count).First(i => writtenKeys[i] != drawnKeys[i]);
        var moved = written[first];
        var drawnFirst = written[writtenKeys.IndexOf(drawnKeys[first])];
        throw new UnsupportedSynthesisConstructException(
            $"Network {network.Number}: the statement order cannot be drawn. '{moved}' is written between statements " +
            $"of one rung, which TIA draws contiguously, so to-xml would place '{drawnFirst}' ahead of it and change " +
            "the scan order. Move the statement above or below that whole rung (a rung is every statement joined by " +
            "{split}/{recv} markers or fed by a same-network timer's Q), or put it in its own network.");
    }

    private static List<string> StatementLines(IrNetwork network) =>
        IrSerializer.SerializeNetworkOnly(network)
            .Split('\n')
            .Where(line => line.StartsWith("  ", StringComparison.Ordinal) && !line.TrimStart().StartsWith("COMMENT ", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .ToList();

    // Identity, not meaning: the read-back renumbers and drops redundant markers and parentheses.
    private static string Key(string line) =>
        new(Markers.Replace(line, string.Empty).Where(c => c is not ('(' or ')' or ' ')).ToArray());
}
