using Converter.Ir;

namespace Converter.SimaticMl;

/// <summary>
/// Reconstructs a FlgNetwork from IR + its sidecar — the inverse of GraphReducer. Every wire is
/// built through one shared endpoint accumulator (<see cref="AddEndpoint"/>), keyed by wire UId,
/// rather than each chain-building call emitting its own WireNode directly. Two things this
/// unification exists for, both confirmed real, 2026-07-11 (FB MotorDOL):
///   - A Contact/OR/comparison's own private wires (operand wire, outgoing wire) are frequently
///     referenced by MORE THAN ONE production — a Move's `en` chain telescopes through the same
///     upstream Contacts a Coil's own chain (or another Move's own chain) already walked, so the
///     same step data (same UIds) is emitted once per production that traces through it. Each
///     Part is added at most once (<see cref="AddPart"/>); each wire's endpoint list is built up
///     across however many productions touch it, de-duplicating identical endpoints so private
///     (non-fanned-out) wires still end up with exactly the two endpoints they've always had.
///   - Genuine fan-out: a Move's `en` tap shares its actual source wire with the chain's real
///     continuation (one wire, three endpoints — producer, Move.en, next-position.in). Neither
///     production alone knows the wire's complete endpoint set; only accumulating across both
///     (this Move's BuildMove call and the chain's own BuildOneChain/BuildTimer/BuildMove call)
///     produces the correct topology. The Rail wire already needed this same accumulation (one
///     wire, many chains' first elements, confirmed real 2026-07-10) — this generalizes that
///     mechanism to every wire, rather than keeping Rail as a special case.
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

        if (network.Moves.Count != sidecar.Moves.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.Moves.Count} move(s) but the sidecar records {sidecar.Moves.Count}.");
        }

        if (network.WordAnds.Count != sidecar.WordAnds.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.WordAnds.Count} And(s) but the sidecar records {sidecar.WordAnds.Count}.");
        }

        if (network.Calls.Count != sidecar.Calls.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.Calls.Count} Call(s) but the sidecar records {sidecar.Calls.Count}.");
        }

        var parts = new List<PartNode>();
        var emittedPartUIds = new HashSet<int>();
        var wireEndpointsByUId = new Dictionary<int, List<WireEndpoint>>();

        for (var t = 0; t < network.Timers.Count; t++)
        {
            var timerSidecar = sidecar.Timers[t];
            BuildTimer(timerSidecar, parts, emittedPartUIds, wireEndpointsByUId);

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
                AddRailEndpoints(wireEndpointsByUId, timerRailWireUId, railFacingEndpoints);
            }
        }

        for (var a = 0; a < network.Assignments.Count; a++)
        {
            var assignmentSidecar = sidecar.Assignments[a];
            BuildOneChain(network.Assignments[a], assignmentSidecar, network.Number, parts, emittedPartUIds, wireEndpointsByUId);

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
                AddRailEndpoints(wireEndpointsByUId, coilRailWireUId, railFacingEndpoints);
            }
        }

        for (var m = 0; m < network.Moves.Count; m++)
        {
            var moveSidecar = sidecar.Moves[m];
            BuildMove(moveSidecar, parts, emittedPartUIds, wireEndpointsByUId);

            // RailWireUId is null when `en`'s first step is a TimerOutputStep, same reasoning as
            // Timer/Coil above (not yet seen live, same mechanism, handled identically).
            if (moveSidecar.RailWireUId is int moveRailWireUId)
            {
                var railFacingEndpoints = moveSidecar.Steps.Count > 0
                    ? RailFacingEndpoints(moveSidecar.Steps[0])
                    : new[] { (UId: moveSidecar.MovePartUId, Port: "en") };
                AddRailEndpoints(wireEndpointsByUId, moveRailWireUId, railFacingEndpoints);
            }
        }

        for (var d = 0; d < network.WordAnds.Count; d++)
        {
            var wordAndSidecar = sidecar.WordAnds[d];
            BuildWordAnd(wordAndSidecar, parts, emittedPartUIds, wireEndpointsByUId);

            // RailWireUId is null when `en`'s first step is a TimerOutputStep, same reasoning as
            // Timer/Coil/Move above (not yet seen live, same mechanism, handled identically).
            if (wordAndSidecar.RailWireUId is int wordAndRailWireUId)
            {
                var railFacingEndpoints = wordAndSidecar.Steps.Count > 0
                    ? RailFacingEndpoints(wordAndSidecar.Steps[0])
                    : new[] { (UId: wordAndSidecar.AndPartUId, Port: "en") };
                AddRailEndpoints(wireEndpointsByUId, wordAndRailWireUId, railFacingEndpoints);
            }
        }

        for (var c = 0; c < network.Calls.Count; c++)
        {
            var callSidecar = sidecar.Calls[c];
            BuildCall(callSidecar, parts, emittedPartUIds, wireEndpointsByUId);

            // RailWireUId is null when `en`'s first step is a TimerOutputStep, same reasoning as
            // Timer/Coil/Move/WAND above (not yet seen live, same mechanism, handled identically).
            if (callSidecar.RailWireUId is int callRailWireUId)
            {
                var railFacingEndpoints = callSidecar.Steps.Count > 0
                    ? RailFacingEndpoints(callSidecar.Steps[0])
                    : new[] { (UId: callSidecar.CallPartUId, Port: "en") };
                AddRailEndpoints(wireEndpointsByUId, callRailWireUId, railFacingEndpoints);
            }
        }

        var wires = wireEndpointsByUId.Select(kv => new WireNode(kv.Key, kv.Value)).ToList();

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

    private static void AddPart(List<PartNode> parts, HashSet<int> emittedPartUIds, PartNode part)
    {
        if (emittedPartUIds.Add(part.UId))
        {
            parts.Add(part);
        }
    }

    private static void AddEndpoint(Dictionary<int, List<WireEndpoint>> wireEndpointsByUId, int wireUId, WireEndpoint endpoint)
    {
        if (!wireEndpointsByUId.TryGetValue(wireUId, out var endpoints))
        {
            endpoints = new List<WireEndpoint>();
            wireEndpointsByUId[wireUId] = endpoints;
        }

        if (!endpoints.Any(e => e.Kind == endpoint.Kind && e.UId == endpoint.UId && e.PortName == endpoint.PortName))
        {
            endpoints.Add(endpoint);
        }
    }

    private static void AddRailEndpoints(
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId, int railWireUId, IEnumerable<(int UId, string Port)> railFacingEndpoints)
    {
        AddEndpoint(wireEndpointsByUId, railWireUId, new WireEndpoint(EndpointKind.Powerrail, null, null));
        foreach (var (uid, port) in railFacingEndpoints)
        {
            AddEndpoint(wireEndpointsByUId, railWireUId, new WireEndpoint(EndpointKind.NameCon, uid, port));
        }
    }

    // Builds a TON Part, its IN-chain (identical shape/mechanism to BuildOneChain's chain, just
    // terminating at "IN" instead of a coil's "in" — and, like a Coil's chain, may have a null
    // RailWireUId if fed directly by another TON's Q), its PT wire (tag or literal preset), and
    // its ET wire if the sidecar recorded one (OpenCon only).
    private static void BuildTimer(
        TimerBindingSidecar sidecar, List<PartNode> parts, HashSet<int> emittedPartUIds, Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        for (var i = 0; i < sidecar.Steps.Count; i++)
        {
            var nextTarget = i + 1 < sidecar.Steps.Count
                ? EntryTarget(sidecar.Steps[i + 1])
                : new WireEndpoint(EndpointKind.NameCon, sidecar.TonPartUId, "IN");

            BuildStep(sidecar.Steps[i], nextTarget, parts, emittedPartUIds, wireEndpointsByUId);
        }

        var instance = new AccessNode(sidecar.InstanceUId, sidecar.InstanceScope, sidecar.InstanceComponentPath);
        AddPart(parts, emittedPartUIds, new PartNode(sidecar.TonPartUId, "TON", TonVersion: sidecar.Version, TimeType: sidecar.TimeType, Instance: instance));

        AddOperandWire(wireEndpointsByUId, sidecar.Preset, sidecar.TonPartUId, "PT");

        if (sidecar.Et is { } et)
        {
            AddEndpoint(wireEndpointsByUId, et.WireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.TonPartUId, "ET"));
            AddEndpoint(wireEndpointsByUId, et.WireUId, new WireEndpoint(EndpointKind.OpenCon, et.OpenConUId, null));
        }
    }

    // Builds a Move Part, its `en`-chain (identical mechanism to BuildOneChain/BuildTimer's own
    // chain, terminating at the Move's own "en" port instead of a Coil's "in"/TON's "IN" — may
    // have a null RailWireUId if `en` is fed directly by another TON's Q, same as any other
    // chain), its `in` wire (tag or literal source, same AddOperandWire as a TON's PT), and its
    // `out1` wire (the write target — same IdentCon-fed wire shape as an ordinary Contact/Coil
    // operand, just port "out1" and the opposite read/write direction).
    private static void BuildMove(
        MoveStatementSidecar sidecar, List<PartNode> parts, HashSet<int> emittedPartUIds, Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        for (var i = 0; i < sidecar.Steps.Count; i++)
        {
            var nextTarget = i + 1 < sidecar.Steps.Count
                ? EntryTarget(sidecar.Steps[i + 1])
                : new WireEndpoint(EndpointKind.NameCon, sidecar.MovePartUId, "en");

            BuildStep(sidecar.Steps[i], nextTarget, parts, emittedPartUIds, wireEndpointsByUId);
        }

        AddPart(parts, emittedPartUIds, new PartNode(sidecar.MovePartUId, "Move"));

        AddOperandWire(wireEndpointsByUId, sidecar.In, sidecar.MovePartUId, "in");

        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DestAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.MovePartUId, "out1"));
    }

    // Builds a bitwise-And Part, its `en`-chain (identical mechanism to BuildMove's own — may
    // have a null RailWireUId if `en` is fed directly by another TON's Q, same as any other
    // chain), its N input wires (`in1`..`inK`, `AddOperandWire` per input — positional, mirrors
    // how the sidecar's own `Inputs` list is ordered), and its `out` wire (the write target —
    // same IdentCon-fed wire shape as Move's own `out1`, just port "out" instead — confirmed
    // real, 2026-07-12, FB VSDUpdateComs).
    private static void BuildWordAnd(
        WordAndStatementSidecar sidecar, List<PartNode> parts, HashSet<int> emittedPartUIds, Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        for (var i = 0; i < sidecar.Steps.Count; i++)
        {
            var nextTarget = i + 1 < sidecar.Steps.Count
                ? EntryTarget(sidecar.Steps[i + 1])
                : new WireEndpoint(EndpointKind.NameCon, sidecar.AndPartUId, "en");

            BuildStep(sidecar.Steps[i], nextTarget, parts, emittedPartUIds, wireEndpointsByUId);
        }

        AddPart(parts, emittedPartUIds, new PartNode(sidecar.AndPartUId, "And", Cardinality: sidecar.Inputs.Count, SrcType: sidecar.SrcType));

        for (var k = 0; k < sidecar.Inputs.Count; k++)
        {
            AddOperandWire(wireEndpointsByUId, sidecar.Inputs[k], sidecar.AndPartUId, $"in{k + 1}");
        }

        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DestAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.AndPartUId, "out"));
    }

    // Builds a Call Part (as its own sibling <Call>/<CallInfo> element, not a <Part Name="Call">
    // — see PartNode's own doc comment), its `en`-chain (identical mechanism to BuildMove/
    // BuildWordAnd's own — may have a null RailWireUId if `en` is fed directly by another TON's
    // Q, same as any other chain), and its sparse, ordered argument list: each Input argument via
    // AddOperandWire (same tag-or-literal wire shape as a TON's PT/a comparison's operand), each
    // Output argument via the same IdentCon-fed wire shape as Move's own out1 — just named per
    // the source's own Parameter Name (confirmed real, 2026-07-12, FC PlantAutoControl) instead of a
    // fixed port.
    private static void BuildCall(
        CallStatementSidecar sidecar, List<PartNode> parts, HashSet<int> emittedPartUIds, Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        for (var i = 0; i < sidecar.Steps.Count; i++)
        {
            var nextTarget = i + 1 < sidecar.Steps.Count
                ? EntryTarget(sidecar.Steps[i + 1])
                : new WireEndpoint(EndpointKind.NameCon, sidecar.CallPartUId, "en");

            BuildStep(sidecar.Steps[i], nextTarget, parts, emittedPartUIds, wireEndpointsByUId);
        }

        var instance = new AccessNode(sidecar.InstanceUId, sidecar.InstanceScope, sidecar.InstanceComponentPath);
        var callParameters = sidecar.Arguments.Select(argument => argument switch
        {
            CallArgumentSidecar.InputArgSidecar input => new CallParameterNode(input.ParamName, "Input", input.Type),
            CallArgumentSidecar.OutputArgSidecar output => new CallParameterNode(output.ParamName, "Output", output.Type),
            _ => throw new IrFormatException($"Unsupported call argument kind: {argument.GetType().Name}"),
        }).ToList();

        AddPart(parts, emittedPartUIds, new PartNode(
            sidecar.CallPartUId, "Call", Instance: instance, BlockName: sidecar.BlockName, BlockType: sidecar.BlockType, CallParameters: callParameters));

        foreach (var argument in sidecar.Arguments)
        {
            switch (argument)
            {
                case CallArgumentSidecar.InputArgSidecar input:
                    AddOperandWire(wireEndpointsByUId, input.Value, sidecar.CallPartUId, input.ParamName);
                    break;

                case CallArgumentSidecar.OutputArgSidecar output:
                    AddEndpoint(wireEndpointsByUId, output.DestWireUId, new WireEndpoint(EndpointKind.IdentCon, output.DestAccessUId, null));
                    AddEndpoint(wireEndpointsByUId, output.DestWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.CallPartUId, output.ParamName));
                    break;

                default:
                    throw new IrFormatException($"Unsupported call argument kind: {argument.GetType().Name}");
            }
        }
    }

    // A tag-or-literal operand wire — used for a TON's PT, a comparison's in1/in2, and a Move's
    // `in` alike (see OperandSidecar's own doc comment for why this is shared rather than
    // TON-specific).
    private static void AddOperandWire(
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId, OperandSidecar operand, int partUId, string port)
    {
        switch (operand)
        {
            case OperandSidecar.TagOperand tagOperand:
                AddEndpoint(wireEndpointsByUId, tagOperand.WireUId, new WireEndpoint(EndpointKind.IdentCon, tagOperand.AccessUId, null));
                AddEndpoint(wireEndpointsByUId, tagOperand.WireUId, new WireEndpoint(EndpointKind.NameCon, partUId, port));
                break;

            case OperandSidecar.LiteralOperand literalOperand:
                AddEndpoint(wireEndpointsByUId, literalOperand.WireUId, new WireEndpoint(EndpointKind.IdentCon, literalOperand.ConstantUId, null));
                AddEndpoint(wireEndpointsByUId, literalOperand.WireUId, new WireEndpoint(EndpointKind.NameCon, partUId, port));
                break;

            default:
                throw new IrFormatException($"Unsupported operand kind: {operand.GetType().Name}");
        }
    }

    private static void BuildOneChain(
        CoilAssignment assignment,
        CoilAssignmentSidecar sidecar,
        int networkNumber,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
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

            BuildStep(sidecar.Steps[i], nextTarget, parts, emittedPartUIds, wireEndpointsByUId);
        }

        AddPart(parts, emittedPartUIds, new PartNode(sidecar.CoilUId, CoilPartNameFor(assignment.Kind)));

        AddEndpoint(wireEndpointsByUId, sidecar.CoilOperandWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.CoilOperandAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.CoilOperandWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.CoilUId, "operand"));
    }

    // The inverse of GraphReducer.CoilKindFor — derived from the model's own CoilAssignment.Kind
    // (BuildOneChain already takes the model alongside its sidecar, for the leaf-count check
    // above) rather than a redundant sidecar field (see CoilAssignmentSidecar's own doc comment).
    private static string CoilPartNameFor(CoilKind kind) => kind switch
    {
        CoilKind.Assign => "Coil",
        CoilKind.Set => "SCoil",
        CoilKind.Reset => "RCoil",
        _ => throw new IrFormatException($"Unsupported coil kind: {kind}"),
    };

    // A single position rail-to-coil: either one Contact (chain continues to `outgoingTarget`),
    // or an OR-merge whose branches are wired to the shared rail elsewhere (Build's own
    // AddRailEndpoints calls) and whose own "out" feeds `outgoingTarget`.
    private static void BuildStep(
        ChainStepSidecar step,
        WireEndpoint outgoingTarget,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        switch (step)
        {
            case ChainStepSidecar.ContactStep contact:
                AddPart(parts, emittedPartUIds, new PartNode(contact.ContactUId, "Contact", contact.Negated));
                AddEndpoint(wireEndpointsByUId, contact.OperandWireUId, new WireEndpoint(EndpointKind.IdentCon, contact.OperandAccessUId, null));
                AddEndpoint(wireEndpointsByUId, contact.OperandWireUId, new WireEndpoint(EndpointKind.NameCon, contact.ContactUId, "operand"));
                AddEndpoint(wireEndpointsByUId, contact.OutgoingWireUId, new WireEndpoint(EndpointKind.NameCon, contact.ContactUId, "out"));
                AddEndpoint(wireEndpointsByUId, contact.OutgoingWireUId, outgoingTarget);
                break;

            case ChainStepSidecar.OrStep orStep:
                AddPart(parts, emittedPartUIds, new PartNode(orStep.OrPartUId, "O", Cardinality: orStep.Branches.Count));
                for (var b = 0; b < orStep.Branches.Count; b++)
                {
                    var branch = orStep.Branches[b];
                    var branchEntryTarget = new WireEndpoint(EndpointKind.NameCon, orStep.OrPartUId, $"in{b + 1}");

                    // A branch is an ordinary chain (S1 item 11) — build it the same way any
                    // top-level chain builds its own steps, just terminating at this branch's
                    // own "inK" port instead of a Coil's "in"/a TON's "IN". Recursing into
                    // BuildStep means a branch that's itself a nested OR-merge, a comparison, or
                    // a multi-Contact chain all just work, no special-casing needed here.
                    for (var i = 0; i < branch.Steps.Count; i++)
                    {
                        var nextTarget = i + 1 < branch.Steps.Count
                            ? EntryTarget(branch.Steps[i + 1])
                            : branchEntryTarget;

                        BuildStep(branch.Steps[i], nextTarget, parts, emittedPartUIds, wireEndpointsByUId);
                    }

                    // A branch's own rail wire (the common case — every branch fed directly by
                    // Powerrail, confirmed real 2026-07-10) is nullable for the same reason a
                    // top-level chain's is: a branch whose own chain terminates at a TON's Q
                    // never touches Powerrail. Two branches genuinely sharing one rail wire (or
                    // sharing it with an unrelated chain elsewhere in the network) need no
                    // special handling — same wire UId, deduplicated by AddEndpoint like any
                    // other shared wire.
                    if (branch.RailWireUId is int branchRailWireUId)
                    {
                        var railFacingEndpoints = branch.Steps.Count > 0
                            ? RailFacingEndpoints(branch.Steps[0])
                            : new[] { (UId: orStep.OrPartUId, Port: $"in{b + 1}") };
                        AddRailEndpoints(wireEndpointsByUId, branchRailWireUId, railFacingEndpoints);
                    }
                }

                AddEndpoint(wireEndpointsByUId, orStep.OutgoingWireUId, new WireEndpoint(EndpointKind.NameCon, orStep.OrPartUId, "out"));
                AddEndpoint(wireEndpointsByUId, orStep.OutgoingWireUId, outgoingTarget);
                break;

            case ChainStepSidecar.TimerOutputStep timerOutput:
                // No Part/Access to build — the TON Part itself is built once by BuildTimer;
                // this just rewires its already-emitted "Q" port to whatever's next. Confirmed
                // real, 2026-07-11, FC TimerSample.
                AddEndpoint(wireEndpointsByUId, timerOutput.OutgoingWireUId, new WireEndpoint(EndpointKind.NameCon, timerOutput.TonPartUId, timerOutput.Port));
                AddEndpoint(wireEndpointsByUId, timerOutput.OutgoingWireUId, outgoingTarget);
                break;

            case ChainStepSidecar.CompareStep compare:
                AddPart(parts, emittedPartUIds, new PartNode(compare.ComparePartUId, compare.PartName, SrcType: compare.SrcType));
                AddOperandWire(wireEndpointsByUId, compare.Left, compare.ComparePartUId, "in1");
                AddOperandWire(wireEndpointsByUId, compare.Right, compare.ComparePartUId, "in2");
                AddEndpoint(wireEndpointsByUId, compare.OutgoingWireUId, new WireEndpoint(EndpointKind.NameCon, compare.ComparePartUId, "out"));
                AddEndpoint(wireEndpointsByUId, compare.OutgoingWireUId, outgoingTarget);
                break;

            case ChainStepSidecar.NotStep notStep:
                var notEntryTarget = new WireEndpoint(EndpointKind.NameCon, notStep.NotPartUId, "in");

                // A Not's own upstream is an ordinary nested chain (S1 item 13) — same recursive
                // build as an OR-merge branch's own steps, just a single chain instead of several.
                for (var i = 0; i < notStep.Steps.Count; i++)
                {
                    var nextTarget = i + 1 < notStep.Steps.Count
                        ? EntryTarget(notStep.Steps[i + 1])
                        : notEntryTarget;

                    BuildStep(notStep.Steps[i], nextTarget, parts, emittedPartUIds, wireEndpointsByUId);
                }

                if (notStep.RailWireUId is int notRailWireUId)
                {
                    var railFacingEndpoints = notStep.Steps.Count > 0
                        ? RailFacingEndpoints(notStep.Steps[0])
                        : new[] { (UId: notStep.NotPartUId, Port: "in") };
                    AddRailEndpoints(wireEndpointsByUId, notRailWireUId, railFacingEndpoints);
                }

                AddPart(parts, emittedPartUIds, new PartNode(notStep.NotPartUId, "Not"));
                AddEndpoint(wireEndpointsByUId, notStep.OutgoingWireUId, new WireEndpoint(EndpointKind.NameCon, notStep.NotPartUId, "out"));
                AddEndpoint(wireEndpointsByUId, notStep.OutgoingWireUId, outgoingTarget);
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
        ChainStepSidecar.NotStep notStep => new WireEndpoint(EndpointKind.NameCon, notStep.NotPartUId, "in"),
        _ => throw new IrFormatException($"Unsupported chain step: {step.GetType().Name}"),
    };

    // The rail-facing endpoint(s) of a chain's first step: the contact's own "in", or the
    // comparison's own "pre" (genuinely different port, not a typo). No OrStep case — like
    // TimerOutputStep, an OrStep-terminated chain's own outer RailWireUId is always null (S1
    // item 11: each branch owns its own rail wiring internally, wired directly in BuildStep's own
    // OrStep case, not bubbled up through here) — this is never actually invoked with one.
    private static IReadOnlyList<(int UId, string Port)> RailFacingEndpoints(ChainStepSidecar step) => step switch
    {
        ChainStepSidecar.ContactStep contact => new[] { (contact.ContactUId, "in") },
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
        ChainStepSidecar.OrStep orStep => orStep.Branches.Sum(b => b.Steps.Sum(CountStepLeaves)),
        ChainStepSidecar.TimerOutputStep => 1,
        ChainStepSidecar.CompareStep => 1,
        ChainStepSidecar.NotStep notStep => notStep.Steps.Sum(CountStepLeaves),
        _ => throw new IrFormatException($"Unsupported chain step: {step.GetType().Name}"),
    };
}
