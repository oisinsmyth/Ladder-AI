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
        // Backward-trace from the coil to the rail, one position at a time (rail-to-coil order
        // once reversed). Each position is either a single Contact (the chain continues further
        // upstream) or an OR-merge (Part Name="O") — confirmed real, 2026-07-10, always
        // rail-facing: every branch of every OR-merge seen resolves to exactly one Contact fed
        // directly by Powerrail, so an OR-merge always terminates the trace, the same as
        // Powerrail itself does for a plain chain.
        var accessEntries = new List<SidecarAccessEntry>();
        var steps = new List<ChainStepSidecar>();
        var stepExprs = new List<Expr>();
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

            visitedWireUIds.Add(wire.UId);
            var outgoingWireUId = wire.UId;

            if (upstreamPart.Name == "Contact")
            {
                var (tag, operandWireUId) = ResolveOperand(wiresByPort, accessByUId, upstreamPart.UId, networkNumber);
                visitedWireUIds.Add(operandWireUId);
                if (!accessEntries.Any(e => e.TagPath == tag.TagPath && e.UId == tag.UId))
                {
                    accessEntries.Add(tag);
                }

                Expr operandExpr = upstreamPart.Negated ? new Expr.Not(new Expr.TagRef(tag.TagPath)) : new Expr.TagRef(tag.TagPath);
                steps.Insert(0, new ChainStepSidecar.ContactStep(upstreamPart.UId, tag.UId, operandWireUId, upstreamPart.Negated, outgoingWireUId));
                stepExprs.Insert(0, operandExpr);
                currentInPort = (upstreamPart.UId, "in");
                continue;
            }

            if (upstreamPart.Name == "O")
            {
                var (orStep, orExpr, orRailWireUId) = ResolveOrMerge(
                    network, wiresByPort, accessByUId, upstreamPart, outgoingWireUId, networkNumber, visitedWireUIds, accessEntries);
                steps.Insert(0, orStep);
                stepExprs.Insert(0, orExpr);
                railWireUId = orRailWireUId;
                break;
            }

            throw new NonReducibleNetworkException(
                $"Network {networkNumber}: upstream of UId={currentInPort.Item1} is a '{upstreamPart.Name}', not a Contact — outside this slice.");
        }

        var (coilTag, coilOperandWireUId) = ResolveOperand(wiresByPort, accessByUId, coil.UId, networkNumber);
        visitedWireUIds.Add(coilOperandWireUId);
        if (!accessEntries.Any(e => e.TagPath == coilTag.TagPath && e.UId == coilTag.UId))
        {
            accessEntries.Add(coilTag);
        }

        Expr condition = stepExprs.Count switch
        {
            0 => new Expr.And(Array.Empty<Expr>()), // coil wired directly to the rail — always on
            1 => stepExprs[0],
            _ => new Expr.And(stepExprs),
        };

        var assignment = new CoilAssignment(coilTag.TagPath, condition);
        var sidecar = new CoilAssignmentSidecar(railWireUId, steps, coil.UId, coilTag.UId, coilOperandWireUId);

        return (assignment, sidecar, accessEntries);
    }

    // An OR-merge is always rail-facing (confirmed real, 2026-07-10): every branch resolves to
    // exactly one Contact fed directly by Powerrail. A branch that is itself a multi-element
    // chain, or shares its OR-merge with a differently-wired rail, is real-but-unconfirmed —
    // refused rather than guessed at.
    private static (ChainStepSidecar.OrStep OrStep, Expr Expr, int RailWireUId) ResolveOrMerge(
        FlgNetwork network,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        PartNode orPart,
        int outgoingWireUId,
        int networkNumber,
        HashSet<int> visitedWireUIds,
        List<SidecarAccessEntry> accessEntries)
    {
        if (orPart.Cardinality is not int cardinality || cardinality < 1)
        {
            throw new NonReducibleNetworkException($"Network {networkNumber}: OR-merge UId={orPart.UId} has no usable cardinality.");
        }

        var branches = new List<ChainStepSidecar.ContactStep>();
        var branchExprs = new List<Expr>();
        var railWireUId = -1;

        for (var k = 1; k <= cardinality; k++)
        {
            var inPort = (orPart.UId, $"in{k}");
            var branchWire = RequireWireAt(wiresByPort, inPort, networkNumber);
            var others = branchWire.Endpoints
                .Where(e => !(e.Kind == EndpointKind.NameCon && e.UId == orPart.UId && e.PortName == $"in{k}"))
                .ToList();

            if (others.Count != 1)
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: OR-merge UId={orPart.UId} branch {k} wire {branchWire.UId} has fan-out — not a single-contact branch.");
            }

            var other = others[0];
            if (other.Kind != EndpointKind.NameCon || other.PortName != "out")
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: OR-merge UId={orPart.UId} branch {k} is fed from an unsupported endpoint ({other.Kind}).");
            }

            var branchPart = network.Parts.FirstOrDefault(p => p.UId == other.UId)
                ?? throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: OR-merge UId={orPart.UId} branch {k} references unknown part UId={other.UId}.");

            if (branchPart.Name != "Contact")
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: OR-merge UId={orPart.UId} branch {k} is a '{branchPart.Name}', not a Contact — " +
                    "multi-element/non-contact OR branches are outside this slice.");
            }

            visitedWireUIds.Add(branchWire.UId);

            var branchInWire = RequireWireAt(wiresByPort, (branchPart.UId, "in"), networkNumber);
            if (!branchInWire.Endpoints.Any(e => e.Kind == EndpointKind.Powerrail))
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: OR-merge UId={orPart.UId} branch {k} contact UId={branchPart.UId} is not fed " +
                    "directly from Powerrail — multi-contact OR branches are outside this slice.");
            }

            if (railWireUId == -1)
            {
                railWireUId = branchInWire.UId;
            }
            else if (railWireUId != branchInWire.UId)
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: OR-merge UId={orPart.UId} branches are fed from different rail wires " +
                    $"({railWireUId} vs {branchInWire.UId}) — only a single shared rail wire has been observed.");
            }

            visitedWireUIds.Add(branchInWire.UId);

            var (tag, operandWireUId) = ResolveOperand(wiresByPort, accessByUId, branchPart.UId, networkNumber);
            visitedWireUIds.Add(operandWireUId);
            if (!accessEntries.Any(e => e.TagPath == tag.TagPath && e.UId == tag.UId))
            {
                accessEntries.Add(tag);
            }

            Expr branchExpr = branchPart.Negated ? new Expr.Not(new Expr.TagRef(tag.TagPath)) : new Expr.TagRef(tag.TagPath);
            branches.Add(new ChainStepSidecar.ContactStep(branchPart.UId, tag.UId, operandWireUId, branchPart.Negated, branchWire.UId));
            branchExprs.Add(branchExpr);
        }

        return (new ChainStepSidecar.OrStep(orPart.UId, branches, outgoingWireUId), new Expr.Or(branchExprs), railWireUId);
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
