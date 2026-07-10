using Converter.Ir;

namespace Converter.SimaticMl;

/// <summary>
/// Reconstructs a FlgNetwork from IR + its sidecar — the inverse of GraphReducer. Each
/// assignment's own wires (operand wires, flow-between-contacts wires, coil operand wire) are
/// rebuilt independently in <see cref="BuildOneChain"/>. The rail connection is handled
/// separately and grouped by <see cref="CoilAssignmentSidecar.RailWireUId"/> across all
/// assignments in the network, since the source frequently uses one shared rail wire (many
/// endpoints) rather than one rail wire per chain — confirmed real, 2026-07-11.
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

            // First element of this chain (first contact, or the coil itself if none) is
            // powered from the rail — record its endpoint under the shared rail wire UId.
            var firstElementUId = assignmentSidecar.ContactUIds.Count > 0 ? assignmentSidecar.ContactUIds[0] : assignmentSidecar.CoilUId;
            if (!railEndpointsByWireUId.TryGetValue(assignmentSidecar.RailWireUId, out var endpoints))
            {
                endpoints = new List<WireEndpoint>();
                railEndpointsByWireUId[assignmentSidecar.RailWireUId] = endpoints;
            }

            endpoints.Add(new WireEndpoint(EndpointKind.NameCon, firstElementUId, "in"));
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
        var operandTags = ExtractOperandTagsInOrder(assignment.Condition);
        if (operandTags.Count != sidecar.ContactUIds.Count)
        {
            throw new IrFormatException(
                $"Network {networkNumber}: assignment for coil '{assignment.CoilTag}' has {operandTags.Count} contact " +
                $"operand(s) but the sidecar records {sidecar.ContactUIds.Count}.");
        }

        var expectedWireCount = (2 * sidecar.ContactUIds.Count) + 1;
        if (sidecar.WireUIds.Count != expectedWireCount)
        {
            throw new IrFormatException(
                $"Network {networkNumber}: assignment for coil '{assignment.CoilTag}' expects {expectedWireCount} " +
                $"non-rail wire UId(s) but the sidecar has {sidecar.WireUIds.Count}.");
        }

        for (var i = 0; i < sidecar.ContactUIds.Count; i++)
        {
            var contactUId = sidecar.ContactUIds[i];
            parts.Add(new PartNode(contactUId, "Contact"));

            var operandAccessUId = sidecar.ContactOperandAccessUIds[i];
            wires.Add(new WireNode(sidecar.WireUIds[2 * i], new[]
            {
                new WireEndpoint(EndpointKind.IdentCon, operandAccessUId, null),
                new WireEndpoint(EndpointKind.NameCon, contactUId, "operand"),
            }));

            var nextTarget = i + 1 < sidecar.ContactUIds.Count
                ? new WireEndpoint(EndpointKind.NameCon, sidecar.ContactUIds[i + 1], "in")
                : new WireEndpoint(EndpointKind.NameCon, sidecar.CoilUId, "in");
            wires.Add(new WireNode(sidecar.WireUIds[(2 * i) + 1], new[]
            {
                new WireEndpoint(EndpointKind.NameCon, contactUId, "out"),
                nextTarget,
            }));
        }

        parts.Add(new PartNode(sidecar.CoilUId, "Coil"));

        wires.Add(new WireNode(sidecar.WireUIds[^1], new[]
        {
            new WireEndpoint(EndpointKind.IdentCon, sidecar.CoilOperandAccessUId, null),
            new WireEndpoint(EndpointKind.NameCon, sidecar.CoilUId, "operand"),
        }));
    }

    private static List<string> ExtractOperandTagsInOrder(Expr expr) => expr switch
    {
        Expr.TagRef tagRef => new List<string> { tagRef.Path },
        Expr.And and => and.Operands.SelectMany(ExtractOperandTagsInOrder).ToList(),
        Expr.Or or => or.Operands.SelectMany(ExtractOperandTagsInOrder).ToList(),
        _ => throw new IrFormatException($"Unsupported expression node: {expr.GetType().Name}"),
    };
}
