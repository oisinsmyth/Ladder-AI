using Converter.Ir;
using Converter.SimaticMl;

namespace Converter;

/// <summary>
/// Reduces a Contact/Coil FlgNetwork to the readable IR form (ADR-0001): one or more
/// independent series chains (Powerrail -> Contact -> Contact -> ... -> Coil), bundled in one
/// network. Two real-world shapes confirmed against actual exports (2026-07-10), both handled
/// explicitly rather than guessed at:
///   - Multiple independent chains sharing one network, with no wiring between them (a
///     16-independent-rung alarm-bit network).
///   - The rail connection itself is commonly one wire with many endpoints — Powerrail plus
///     every chain's first element — not one wire per chain. This is not fan-out that breaks
///     series-purity; it's just a shared source, so it's tracked separately from each chain's
///     own private wires (see <see cref="CoilAssignmentSidecar"/>).
///   - An empty network (source `&lt;NetworkSource /&gt;`, no content at all) reduces to zero assignments.
///
/// Deliberately narrow otherwise: real OR/parallel-branch detection *within* a single chain is
/// real complexity (ADR-0001's "Consequences" section) that this slice's fixtures don't
/// exercise. Anything that isn't a clean series chain throws
/// <see cref="NonReducibleNetworkException"/> rather than guessing — the seam for overall-plan
/// item 7's explicit-form fallback, not built yet since nothing here exercises it.
/// </summary>
public static class GraphReducer
{
    public static ReducedNetwork Reduce(FlgNetwork network, int networkNumber, string title, string compileUnitUId)
    {
        if (network.AccessNodes.Count == 0 && network.Parts.Count == 0 && network.Wires.Count == 0)
        {
            var emptyNetwork = new IrNetwork(networkNumber, title, Array.Empty<CoilAssignment>());
            var emptySidecar = new NetworkSidecar(networkNumber, compileUnitUId, Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
            return new ReducedNetwork(emptyNetwork, emptySidecar);
        }

        var accessByUId = network.AccessNodes.ToDictionary(a => a.UId);
        var wiresByPort = BuildPortIndex(network.Wires);

        var coils = network.Parts.Where(p => p.Name == "Coil").ToList();
        if (coils.Count == 0)
        {
            throw new NonReducibleNetworkException($"Network {networkNumber}: no Coil found.");
        }

        var assignments = new List<CoilAssignment>();
        var assignmentSidecars = new List<CoilAssignmentSidecar>();
        var allAccessEntries = new List<SidecarAccessEntry>();
        var visitedWireUIds = new HashSet<int>();

        foreach (var coil in coils)
        {
            var (assignment, sidecar, accessEntries) = ReduceOneChain(network, coil, wiresByPort, accessByUId, networkNumber, visitedWireUIds);
            assignments.Add(assignment);
            assignmentSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                if (!allAccessEntries.Any(e => e.TagPath == entry.TagPath && e.UId == entry.UId))
                {
                    allAccessEntries.Add(entry);
                }
            }
        }

        if (visitedWireUIds.Count != network.Wires.Count)
        {
            throw new NonReducibleNetworkException(
                $"Network {networkNumber}: {network.Wires.Count - visitedWireUIds.Count} wire(s) unaccounted for after " +
                "reduction — unexpected topology, refusing to silently drop structure.");
        }

        var irNetwork = new IrNetwork(networkNumber, title, assignments);
        var networkSidecar = new NetworkSidecar(networkNumber, compileUnitUId, allAccessEntries, assignmentSidecars);
        return new ReducedNetwork(irNetwork, networkSidecar);
    }

    private static (CoilAssignment Assignment, CoilAssignmentSidecar Sidecar, List<SidecarAccessEntry> AccessEntries) ReduceOneChain(
        FlgNetwork network,
        PartNode coil,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        // Backward-trace from the coil to the rail, collecting contacts (rail-to-coil order)
        // and the "flow" wire feeding each element — except the very first one, which touches
        // Powerrail and may be shared with sibling chains (tracked as railWireUId instead).
        var contactChain = new List<PartNode>();
        var flowWireUIds = new List<int>(); // one per element after the first: contact[1..].in and coil.in
        var currentInPort = (coil.UId, "in");
        var railWireUId = -1;

        while (true)
        {
            var wire = RequireWireAt(wiresByPort, currentInPort, networkNumber);

            if (wire.Endpoints.Any(e => e.Kind == EndpointKind.Powerrail))
            {
                // Reached the rail. Other endpoints on this same wire (if any) belong to
                // sibling chains' first elements — not this chain's concern, not fan-out.
                visitedWireUIds.Add(wire.UId);
                railWireUId = wire.UId;
                break;
            }

            var others = wire.Endpoints
                .Where(e => !(e.Kind == EndpointKind.NameCon && e.UId == currentInPort.Item1 && e.PortName == currentInPort.Item2))
                .ToList();

            if (others.Count != 1)
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: wire {wire.UId} has fan-out ({wire.Endpoints.Count} endpoints) — not a pure series chain.");
            }

            var other = others[0];
            if (other.Kind != EndpointKind.NameCon || other.PortName != "out")
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: wire {wire.UId} feeds {currentInPort} from an unsupported endpoint ({other.Kind}).");
            }

            var upstreamPart = network.Parts.FirstOrDefault(p => p.UId == other.UId)
                ?? throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: wire {wire.UId} references unknown part UId={other.UId}.");

            if (upstreamPart.Name != "Contact")
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: upstream of UId={currentInPort.Item1} is a '{upstreamPart.Name}', not a Contact — outside this slice.");
            }

            visitedWireUIds.Add(wire.UId);
            flowWireUIds.Insert(0, wire.UId);
            contactChain.Insert(0, upstreamPart);
            currentInPort = (upstreamPart.UId, "in");
        }

        var accessEntries = new List<SidecarAccessEntry>();
        var operandTags = new List<string>();
        var operandWireUIds = new List<int>();
        var contactOperandAccessUIds = new List<int>();

        foreach (var contact in contactChain)
        {
            var (tag, wireUId) = ResolveOperand(wiresByPort, accessByUId, contact.UId, networkNumber);
            visitedWireUIds.Add(wireUId);
            operandWireUIds.Add(wireUId);
            operandTags.Add(tag.TagPath);
            contactOperandAccessUIds.Add(tag.UId);
            if (!accessEntries.Any(e => e.TagPath == tag.TagPath && e.UId == tag.UId))
            {
                accessEntries.Add(tag);
            }
        }

        var (coilTag, coilOperandWireUId) = ResolveOperand(wiresByPort, accessByUId, coil.UId, networkNumber);
        visitedWireUIds.Add(coilOperandWireUId);
        if (!accessEntries.Any(e => e.TagPath == coilTag.TagPath && e.UId == coilTag.UId))
        {
            accessEntries.Add(coilTag);
        }

        Expr condition = operandTags.Count switch
        {
            0 => new Expr.And(Array.Empty<Expr>()), // coil wired directly to the rail — always on
            1 => new Expr.TagRef(operandTags[0]),
            _ => new Expr.And(operandTags.Select(t => (Expr)new Expr.TagRef(t)).ToList()),
        };

        // Interleave to match FlgNetBuilder's regeneration order: operand_0, flow_to_1,
        // operand_1, flow_to_2, ..., operand_last, flow_toCoil, coilOperand. flowWireUIds has
        // one entry per contact after the first, plus the final flow into the coil — i.e.
        // exactly contactChain.Count entries (flow-into-contact[1..] is Count-1, plus
        // flow-into-coil is 1 more).
        var wireUIds = new List<int>();
        for (var i = 0; i < contactChain.Count; i++)
        {
            wireUIds.Add(operandWireUIds[i]);
            wireUIds.Add(flowWireUIds[i]);
        }

        wireUIds.Add(coilOperandWireUId);

        var assignment = new CoilAssignment(coilTag.TagPath, condition);
        var sidecar = new CoilAssignmentSidecar(
            railWireUId,
            contactChain.Select(c => c.UId).ToList(),
            contactOperandAccessUIds,
            coil.UId,
            coilTag.UId,
            wireUIds);

        return (assignment, sidecar, accessEntries);
    }

    private static (SidecarAccessEntry Tag, int WireUId) ResolveOperand(
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        int partUId,
        int networkNumber)
    {
        var wire = RequireWireAt(wiresByPort, (partUId, "operand"), networkNumber);
        var identCon = wire.Endpoints.FirstOrDefault(e => e.Kind == EndpointKind.IdentCon)
            ?? throw new NonReducibleNetworkException(
                $"Network {networkNumber}: operand wire {wire.UId} for UId={partUId} has no IdentCon source.");

        var access = accessByUId.TryGetValue(identCon.UId!.Value, out var found)
            ? found
            : throw new NonReducibleNetworkException(
                $"Network {networkNumber}: operand wire {wire.UId} references unknown Access UId={identCon.UId}.");

        return (new SidecarAccessEntry(access.DottedPath, access.UId), wire.UId);
    }

    private static Dictionary<(int, string), WireNode> BuildPortIndex(IReadOnlyList<WireNode> wires)
    {
        var index = new Dictionary<(int, string), WireNode>();
        foreach (var wire in wires)
        {
            foreach (var endpoint in wire.Endpoints)
            {
                if (endpoint.Kind == EndpointKind.NameCon)
                {
                    index[(endpoint.UId!.Value, endpoint.PortName!)] = wire;
                }
            }
        }

        return index;
    }

    private static WireNode RequireWireAt(Dictionary<(int, string), WireNode> index, (int, string) port, int networkNumber) =>
        index.TryGetValue(port, out var wire)
            ? wire
            : throw new NonReducibleNetworkException($"Network {networkNumber}: no wire found feeding port {port}.");
}

public sealed class NonReducibleNetworkException : Exception
{
    public NonReducibleNetworkException(string message)
        : base(message)
    {
    }
}
