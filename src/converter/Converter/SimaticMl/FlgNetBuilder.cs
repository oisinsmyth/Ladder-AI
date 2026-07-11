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

        if (network.Timers.Count != sidecar.Timers.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.Timers.Count} timer(s) but the sidecar records {sidecar.Timers.Count}.");
        }

        var parts = new List<PartNode>();
        var wires = new List<WireNode>();
        var railEndpointsByWireUId = new Dictionary<int, List<WireEndpoint>>();

        for (var t = 0; t < network.Timers.Count; t++)
        {
            var timerSidecar = sidecar.Timers[t];
            BuildTimer(timerSidecar, parts, wires);

            // RailWireUId is null when the chain's first step is a TimerOutputStep — the IN is
            // fed directly by another TON's Q, never touches Powerrail, so there's nothing to
            // wire here at all (confirmed real, 2026-07-11, FC TimerSample's own pattern applied
            // to IN; not yet seen live but the same mechanism, so handled identically).
            if (timerSidecar.RailWireUId is int timerRailWireUId)
            {
                // Rail-facing endpoints: every first-step Contact/OR-branch/comparison uses its
                // own rail-facing port (RailFacingEndpoints already knows which — "in" for
                // Contact, "pre" for a comparison); only when there are no steps at all is the
                // rail wired straight to the TON itself, whose port is "IN" (uppercase).
                var railFacingEndpoints = timerSidecar.Steps.Count > 0
                    ? RailFacingEndpoints(timerSidecar.Steps[0])
                    : new[] { (UId: timerSidecar.TonPartUId, Port: "IN") };
                AddRailEndpoints(railEndpointsByWireUId, timerRailWireUId, railFacingEndpoints);
            }
        }

        for (var a = 0; a < network.Assignments.Count; a++)
        {
            var assignmentSidecar = sidecar.Assignments[a];
            BuildOneChain(network.Assignments[a], assignmentSidecar, network.Number, parts, wires);

            // RailWireUId is null when the chain's first step is a TimerOutputStep — the coil is
            // fed directly by a TON's Q, never touches Powerrail (confirmed real, 2026-07-11,
            // FC TimerSample).
            if (assignmentSidecar.RailWireUId is int coilRailWireUId)
            {
                // The rail-facing endpoints for this chain: the single first step's own
                // rail-facing port (RailFacingEndpoints — "in" for Contact, "pre" for a
                // comparison), every branch's contact if it's an OR-merge (all branches share
                // the rail, confirmed real 2026-07-10), or the coil itself if the chain has no
                // steps — a Coil's own port is "in" (lowercase) either way, unlike a TON's "IN".
                var railFacingEndpoints = assignmentSidecar.Steps.Count > 0
                    ? RailFacingEndpoints(assignmentSidecar.Steps[0])
                    : new[] { (UId: assignmentSidecar.CoilUId, Port: "in") };
                AddRailEndpoints(railEndpointsByWireUId, coilRailWireUId, railFacingEndpoints);
            }
        }

        foreach (var (railWireUId, endpoints) in railEndpointsByWireUId)
        {
            var allEndpoints = new List<WireEndpoint> { new(EndpointKind.Powerrail, null, null) };
            allEndpoints.AddRange(endpoints);
            wires.Add(new WireNode(railWireUId, allEndpoints));
        }

        // Scope is carried per-entry (not assumed) since 2026-07-11 — a plain tag Access can be
        // LocalVariable-scoped too (an FC/FB's own interface parameter, confirmed real grounding
        // TON's PT against FC ControlDelays), not only GlobalVariable.
        var accessNodes = sidecar.AccessUIds
            .Select(entry => AccessNode.FromDottedPath(entry.UId, entry.Scope, entry.TagPath))
            .ToList();
        var constants = sidecar.ConstantUIds
            .Select(entry => new ConstantAccessNode(entry.UId, entry.Value, entry.ConstantType))
            .ToList();

        return new FlgNetwork(accessNodes, parts, wires, constants);
    }

    private static void AddRailEndpoints(
        Dictionary<int, List<WireEndpoint>> railEndpointsByWireUId, int railWireUId, IEnumerable<(int UId, string Port)> railFacingEndpoints)
    {
        if (!railEndpointsByWireUId.TryGetValue(railWireUId, out var endpoints))
        {
            endpoints = new List<WireEndpoint>();
            railEndpointsByWireUId[railWireUId] = endpoints;
        }

        foreach (var (uid, port) in railFacingEndpoints)
        {
            endpoints.Add(new WireEndpoint(EndpointKind.NameCon, uid, port));
        }
    }

    // Builds a TON Part, its IN-chain (identical shape/mechanism to BuildOneChain's chain, just
    // terminating at "IN" instead of a coil's "in" — and, like a Coil's chain, may have a null
    // RailWireUId if fed directly by another TON's Q), its PT wire (tag or literal preset), and
    // its ET wire if the sidecar recorded one (OpenCon only).
    private static void BuildTimer(TimerBindingSidecar sidecar, List<PartNode> parts, List<WireNode> wires)
    {
        for (var i = 0; i < sidecar.Steps.Count; i++)
        {
            var nextTarget = i + 1 < sidecar.Steps.Count
                ? EntryTarget(sidecar.Steps[i + 1])
                : new WireEndpoint(EndpointKind.NameCon, sidecar.TonPartUId, "IN");

            BuildStep(sidecar.Steps[i], nextTarget, parts, wires);
        }

        var instance = new AccessNode(sidecar.InstanceUId, sidecar.InstanceScope, sidecar.InstanceComponentPath);
        parts.Add(new PartNode(sidecar.TonPartUId, "TON", TonVersion: sidecar.Version, TimeType: sidecar.TimeType, Instance: instance));

        wires.Add(BuildOperandWire(sidecar.Preset, sidecar.TonPartUId, "PT"));

        if (sidecar.Et is { } et)
        {
            wires.Add(new WireNode(et.WireUId, new[]
            {
                new WireEndpoint(EndpointKind.NameCon, sidecar.TonPartUId, "ET"),
                new WireEndpoint(EndpointKind.OpenCon, et.OpenConUId, null),
            }));
        }
    }

    // A tag-or-literal operand wire — used for a TON's PT and a comparison's in1/in2 alike (see
    // OperandSidecar's own doc comment for why this is shared rather than TON-specific).
    private static WireNode BuildOperandWire(OperandSidecar operand, int partUId, string port) => operand switch
    {
        OperandSidecar.TagOperand tagOperand => new WireNode(tagOperand.WireUId, new[]
        {
            new WireEndpoint(EndpointKind.IdentCon, tagOperand.AccessUId, null),
            new WireEndpoint(EndpointKind.NameCon, partUId, port),
        }),
        OperandSidecar.LiteralOperand literalOperand => new WireNode(literalOperand.WireUId, new[]
        {
            new WireEndpoint(EndpointKind.IdentCon, literalOperand.ConstantUId, null),
            new WireEndpoint(EndpointKind.NameCon, partUId, port),
        }),
        _ => throw new IrFormatException($"Unsupported operand kind: {operand.GetType().Name}"),
    };

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
                ? EntryTarget(sidecar.Steps[i + 1])
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

            case ChainStepSidecar.TimerOutputStep timerOutput:
                // No Part/Access to build — the TON Part itself is built once by BuildTimer;
                // this just rewires its already-emitted "Q" port to whatever's next. Confirmed
                // real, 2026-07-11, FC TimerSample.
                wires.Add(new WireNode(timerOutput.OutgoingWireUId, new[]
                {
                    new WireEndpoint(EndpointKind.NameCon, timerOutput.TonPartUId, timerOutput.Port),
                    outgoingTarget,
                }));
                break;

            case ChainStepSidecar.CompareStep compare:
                parts.Add(new PartNode(compare.ComparePartUId, compare.PartName, SrcType: compare.SrcType));
                wires.Add(BuildOperandWire(compare.Left, compare.ComparePartUId, "in1"));
                wires.Add(BuildOperandWire(compare.Right, compare.ComparePartUId, "in2"));
                wires.Add(new WireNode(compare.OutgoingWireUId, new[]
                {
                    new WireEndpoint(EndpointKind.NameCon, compare.ComparePartUId, "out"),
                    outgoingTarget,
                }));
                break;

            default:
                throw new IrFormatException($"Unsupported chain step: {step.GetType().Name}");
        }
    }

    // The port a following step wires its own "out" into — "in" for Contact/O, "pre" for a
    // comparison (genuinely different port name, not a typo — confirmed real, 2026-07-11,
    // FC ControlDelays). Only ever called with a step at index >= 1; an OrStep/TimerOutputStep is
    // always rail-facing/terminal (steps[0] only), so in practice this only ever sees
    // ContactStep/CompareStep — handled generally anyway since nothing about the shape rules out
    // a future position other than "first" for the others.
    private static WireEndpoint EntryTarget(ChainStepSidecar step) => step switch
    {
        ChainStepSidecar.ContactStep contact => new WireEndpoint(EndpointKind.NameCon, contact.ContactUId, "in"),
        ChainStepSidecar.OrStep orStep => new WireEndpoint(EndpointKind.NameCon, orStep.OrPartUId, "in"),
        ChainStepSidecar.TimerOutputStep timerOutput => new WireEndpoint(EndpointKind.NameCon, timerOutput.TonPartUId, "in"),
        ChainStepSidecar.CompareStep compare => new WireEndpoint(EndpointKind.NameCon, compare.ComparePartUId, "pre"),
        _ => throw new IrFormatException($"Unsupported chain step: {step.GetType().Name}"),
    };

    // The rail-facing endpoint(s) of a chain's first step: the contact's own "in", the
    // comparison's own "pre" (genuinely different port, not a typo), or every OR-merge branch's
    // contact "in" (all fed by the same shared rail wire, confirmed real 2026-07-10; a branch is
    // always a Contact, never itself a comparison — see OrMergeOfComparisons.xml/
    // ComparisonTests.cs for why that's deliberately still refused, not guessed at).
    private static IReadOnlyList<(int UId, string Port)> RailFacingEndpoints(ChainStepSidecar step) => step switch
    {
        ChainStepSidecar.ContactStep contact => new[] { (contact.ContactUId, "in") },
        ChainStepSidecar.OrStep orStep => orStep.Branches.Select(b => (b.ContactUId, "in")).ToArray(),
        ChainStepSidecar.CompareStep compare => new[] { (compare.ComparePartUId, "pre") },
        _ => throw new IrFormatException($"Unsupported chain step: {step.GetType().Name}"),
    };

    private static int CountExprLeaves(Expr expr) => expr switch
    {
        Expr.TagRef => 1,
        Expr.Literal => 1,
        Expr.Compare => 1,
        Expr.Not not => CountExprLeaves(not.Operand),
        Expr.And and => and.Operands.Sum(CountExprLeaves),
        Expr.Or or => or.Operands.Sum(CountExprLeaves),
        _ => throw new IrFormatException($"Unsupported expression node: {expr.GetType().Name}"),
    };

    private static int CountStepLeaves(ChainStepSidecar step) => step switch
    {
        ChainStepSidecar.ContactStep => 1,
        ChainStepSidecar.OrStep orStep => orStep.Branches.Count,
        ChainStepSidecar.TimerOutputStep => 1,
        ChainStepSidecar.CompareStep => 1,
        _ => throw new IrFormatException($"Unsupported chain step: {step.GetType().Name}"),
    };
}
