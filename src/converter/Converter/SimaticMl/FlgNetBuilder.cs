using Converter.Ir;

namespace Converter.SimaticMl;

/// <summary>
/// Reconstructs a FlgNetwork from IR + its sidecar — the inverse of GraphReducer. Each
/// assignment's own wires (operand wires, flow-between-contacts wires, coil operand wire) are
/// rebuilt independently in <see cref="BuildOneChain"/>. The rail connection is handled
/// separately and grouped by <see cref="CoilAssignmentSidecar.RailWireUId"/> across all
/// assignments in the network, since the source frequently uses one shared rail wire (many
/// endpoints) rather than one rail wire per chain — confirmed real, 2026-07-10.
/// </summary>
public static class FlgNetBuilder
{
    public static FlgNetwork Build(IrNetwork network, NetworkSidecar sidecar)
    {
        if (network.IsEmpty)
        {
            return new FlgNetwork(Array.Empty<AccessNode>(), Array.Empty<PartNode>(), Array.Empty<WireNode>());
        }

        if (network.Assignments.Count != sidecar.Assignments.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.Assignments.Count} coil assignment(s) but the sidecar " +
                $"records {sidecar.Assignments.Count}.");
        }

        var parts = new List<PartNode>();
        var wires = new List<WireNode>();
        var railEndpointsByWireUId = new Dictionary<int, List<WireEndpoint>>();

        for (var a = 0; a < network.Assignments.Count; a++)
        {
            var assignmentSidecar = sidecar.Assignments[a];
            BuildOneChain(network.Assignments[a], assignmentSidecar, network.Number, parts, wires);

            // The rail-facing UIds for this chain: the single first step's contact if it's a
            // plain Contact, every branch's contact if it's an OR-merge (all branches share the
            // rail, confirmed real 2026-07-10), or the coil itself if the chain has no steps.
            var railFacingUIds = assignmentSidecar.Steps.Count > 0
                ? RailFacingUIds(assignmentSidecar.Steps[0])
                : new[] { assignmentSidecar.CoilUId };

            if (!railEndpointsByWireUId.TryGetValue(assignmentSidecar.RailWireUId, out var endpoints))
            {
                endpoints = new List<WireEndpoint>();
                railEndpointsByWireUId[assignmentSidecar.RailWireUId] = endpoints;
            }

            foreach (var uid in railFacingUIds)
            {
                endpoints.Add(new WireEndpoint(EndpointKind.NameCon, uid, "in"));
            }
        }

        foreach (var (railWireUId, endpoints) in railEndpointsByWireUId)
        {
            var allEndpoints = new List<WireEndpoint> { new(EndpointKind.Powerrail, null, null) };
            allEndpoints.AddRange(endpoints);
            wires.Add(new WireNode(railWireUId, allEndpoints));
        }

        var accessNodes = sidecar.AccessUIds
            .Select(entry => AccessNode.FromDottedPath(entry.UId, "GlobalVariable", entry.TagPath))
            .ToList();

        return new FlgNetwork(accessNodes, parts, wires);
    }

    private static void BuildOneChain(
        CoilAssignment assignment,
        CoilAssignmentSidecar sidecar,
        int networkNumber,
        List<PartNode> parts,
        List<WireNode> wires)
    {
        var exprLeafCount = CountExprLeaves(assignment.Condition);
        var stepLeafCount = sidecar.Steps.Sum(CountStepLeaves);
        if (exprLeafCount != stepLeafCount)
        {
            throw new IrFormatException(
                $"Network {networkNumber}: assignment for coil '{assignment.CoilTag}' has {exprLeafCount} contact " +
                $"operand(s) but the sidecar records {stepLeafCount}.");
        }

        for (var i = 0; i < sidecar.Steps.Count; i++)
        {
            var nextTarget = i + 1 < sidecar.Steps.Count
                ? new WireEndpoint(EndpointKind.NameCon, EntryUId(sidecar.Steps[i + 1]), "in")
                : new WireEndpoint(EndpointKind.NameCon, sidecar.CoilUId, "in");

            BuildStep(sidecar.Steps[i], nextTarget, parts, wires);
        }

        parts.Add(new PartNode(sidecar.CoilUId, "Coil"));

        wires.Add(new WireNode(sidecar.CoilOperandWireUId, new[]
        {
            new WireEndpoint(EndpointKind.IdentCon, sidecar.CoilOperandAccessUId, null),
            new WireEndpoint(EndpointKind.NameCon, sidecar.CoilUId, "operand"),
        }));
    }

    // A single position rail-to-coil: either one Contact (chain continues to `outgoingTarget`),
    // or an OR-merge whose branches are wired to the shared rail elsewhere (Build's
    // railEndpointsByWireUId) and whose own "out" feeds `outgoingTarget`.
    private static void BuildStep(ChainStepSidecar step, WireEndpoint outgoingTarget, List<PartNode> parts, List<WireNode> wires)
    {
        switch (step)
        {
            case ChainStepSidecar.ContactStep contact:
                parts.Add(new PartNode(contact.ContactUId, "Contact", contact.Negated));
                wires.Add(new WireNode(contact.OperandWireUId, new[]
                {
                    new WireEndpoint(EndpointKind.IdentCon, contact.OperandAccessUId, null),
                    new WireEndpoint(EndpointKind.NameCon, contact.ContactUId, "operand"),
                }));
                wires.Add(new WireNode(contact.OutgoingWireUId, new[]
                {
                    new WireEndpoint(EndpointKind.NameCon, contact.ContactUId, "out"),
                    outgoingTarget,
                }));
                break;

            case ChainStepSidecar.OrStep orStep:
                parts.Add(new PartNode(orStep.OrPartUId, "O", Cardinality: orStep.Branches.Count));
                for (var b = 0; b < orStep.Branches.Count; b++)
                {
                    var branch = orStep.Branches[b];
                    parts.Add(new PartNode(branch.ContactUId, "Contact", branch.Negated));
                    wires.Add(new WireNode(branch.OperandWireUId, new[]
                    {
                        new WireEndpoint(EndpointKind.IdentCon, branch.OperandAccessUId, null),
                        new WireEndpoint(EndpointKind.NameCon, branch.ContactUId, "operand"),
                    }));
                    wires.Add(new WireNode(branch.OutgoingWireUId, new[]
                    {
                        new WireEndpoint(EndpointKind.NameCon, branch.ContactUId, "out"),
                        new WireEndpoint(EndpointKind.NameCon, orStep.OrPartUId, $"in{b + 1}"),
                    }));
                }

                wires.Add(new WireNode(orStep.OutgoingWireUId, new[]
                {
                    new WireEndpoint(EndpointKind.NameCon, orStep.OrPartUId, "out"),
                    outgoingTarget,
                }));
                break;

            default:
                throw new IrFormatException($"Unsupported chain step: {step.GetType().Name}");
        }
    }

    // The UId a following step wires its "in" to. Only ever called with a step at index >= 1;
    // an OrStep is always rail-facing (steps[0] only, confirmed real 2026-07-10), so in practice
    // this only ever sees ContactStep — handled generally anyway since nothing about the shape
    // rules it out for a future position other than "first".
    private static int EntryUId(ChainStepSidecar step) => step switch
    {
        ChainStepSidecar.ContactStep contact => contact.ContactUId,
        ChainStepSidecar.OrStep orStep => orStep.OrPartUId,
        _ => throw new IrFormatException($"Unsupported chain step: {step.GetType().Name}"),
    };

    // The rail-facing UId(s) of a chain's first step: the contact itself, or every OR-merge
    // branch's contact (all fed by the same shared rail wire, confirmed real 2026-07-10).
    private static IReadOnlyList<int> RailFacingUIds(ChainStepSidecar step) => step switch
    {
        ChainStepSidecar.ContactStep contact => new[] { contact.ContactUId },
        ChainStepSidecar.OrStep orStep => orStep.Branches.Select(b => b.ContactUId).ToArray(),
        _ => throw new IrFormatException($"Unsupported chain step: {step.GetType().Name}"),
    };

    private static int CountExprLeaves(Expr expr) => expr switch
    {
        Expr.TagRef => 1,
        Expr.Not not => CountExprLeaves(not.Operand),
        Expr.And and => and.Operands.Sum(CountExprLeaves),
        Expr.Or or => or.Operands.Sum(CountExprLeaves),
        _ => throw new IrFormatException($"Unsupported expression node: {expr.GetType().Name}"),
    };

    private static int CountStepLeaves(ChainStepSidecar step) => step switch
    {
        ChainStepSidecar.ContactStep => 1,
        ChainStepSidecar.OrStep orStep => orStep.Branches.Count,
        _ => throw new IrFormatException($"Unsupported chain step: {step.GetType().Name}"),
    };
}
