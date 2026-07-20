using System.Xml.Linq;
using Converter.SimaticMl;

namespace Converter.Preflight;

// FI-27 (docs/16-future-ideas.md): validates that a synthesized network's instruction <Part>/<Call>
// elements are emitted in the DFS-from-rail wire-graph flow order TIA's import requires. This class
// was import-only discoverable before — the Normalizer sorts <Parts> before comparing, so every
// offline equivalence oracle masks raw Part order (7694fdf fixed the writer; this guards against a
// regression on any real corpus block, in the inner loop, before a Portal round trip).
//
// Pure and parameterized (takes the emitted UId sequence) so it is independently testable: the writer
// always applies the rule, so a real mismatch can't be produced through the normal serialize path —
// this seam lets a test inject a deliberately-wrong order and assert the finding fires.
public static class FlowOrderCheck
{
    // The instruction UId sequence actually emitted, read from a serialized <FlgNet>: the <Part> and
    // <Call> children of <Parts> in document order (the <Access> data-leaf children are excluded — they
    // are emitted first, UId-sorted, and are not subject to the flow-order rule).
    public static IReadOnlyList<int> ReadEmittedPartUIds(XElement flgNet)
    {
        var parts = flgNet.Elements().FirstOrDefault(e => e.Name.LocalName == "Parts");
        if (parts is null)
        {
            return Array.Empty<int>();
        }

        return parts.Elements()
            .Where(e => e.Name.LocalName is "Part" or "Call")
            .Select(e => int.Parse(e.Attribute("UId")!.Value))
            .ToList();
    }

    // Compares the emitted instruction order against the single source of truth for the rule
    // (FlgNetWriter.FlowOrderedPartUIds). Returns a finding on divergence, else null.
    public static PreflightFinding? Validate(int networkNumber, FlgNetwork network, IReadOnlyList<int> emittedPartUIds)
    {
        var expected = FlgNetWriter.FlowOrderedPartUIds(network);
        if (emittedPartUIds.SequenceEqual(expected))
        {
            return null;
        }

        return new PreflightFinding(
            "flow-order",
            $"network {networkNumber}: instruction <Parts> are not in wire-graph flow order — TIA import " +
            "would reject (\"the elements must be sorted according to the current flow\"). Expected UId order [" +
            string.Join(", ", expected) + "], got [" + string.Join(", ", emittedPartUIds) + "].");
    }
}
