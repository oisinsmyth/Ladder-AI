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

        if (network.Muls.Count != sidecar.Muls.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.Muls.Count} Mul(s) but the sidecar records {sidecar.Muls.Count}.");
        }

        if (network.Converts.Count != sidecar.Converts.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.Converts.Count} Convert(s) but the sidecar records {sidecar.Converts.Count}.");
        }

        if (network.Swaps.Count != sidecar.Swaps.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.Swaps.Count} Swap(s) but the sidecar records {sidecar.Swaps.Count}.");
        }

        if (network.AbsStatements.Count != sidecar.AbsStatements.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.AbsStatements.Count} Abs(es) but the sidecar records {sidecar.AbsStatements.Count}.");
        }

        if (network.Limits.Count != sidecar.Limits.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.Limits.Count} Limit(s) but the sidecar records {sidecar.Limits.Count}.");
        }

        if (network.TSubs.Count != sidecar.TSubs.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.TSubs.Count} T_SUB(s) but the sidecar records {sidecar.TSubs.Count}.");
        }

        if (network.TConvs.Count != sidecar.TConvs.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.TConvs.Count} T_CONV(s) but the sidecar records {sidecar.TConvs.Count}.");
        }

        if (network.Calcs.Count != sidecar.Calcs.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.Calcs.Count} Calc(s) but the sidecar records {sidecar.Calcs.Count}.");
        }

        if (network.MoveBlkVariants.Count != sidecar.MoveBlkVariants.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.MoveBlkVariants.Count} MOVE_BLK_VARIANT(s) but the sidecar records {sidecar.MoveBlkVariants.Count}.");
        }

        if (network.Waits.Count != sidecar.Waits.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.Waits.Count} WAIT(s) but the sidecar records {sidecar.Waits.Count}.");
        }

        if (network.FillBlockIs.Count != sidecar.FillBlockIs.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.FillBlockIs.Count} FillBlockI(s) but the sidecar records {sidecar.FillBlockIs.Count}.");
        }

        if (network.ModbusMasters.Count != sidecar.ModbusMasters.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.ModbusMasters.Count} Modbus_Master(s) but the sidecar records {sidecar.ModbusMasters.Count}.");
        }

        if (network.ModbusCommLoads.Count != sidecar.ModbusCommLoads.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.ModbusCommLoads.Count} Modbus_Comm_Load(s) but the sidecar records {sidecar.ModbusCommLoads.Count}.");
        }

        if (network.FixedShapes.Count != sidecar.FixedShapes.Count)
        {
            throw new IrFormatException(
                $"Network {network.Number}: IR has {network.FixedShapes.Count} fixed-shape instruction(s) but the sidecar records {sidecar.FixedShapes.Count}.");
        }

        var parts = new List<PartNode>();
        var emittedPartUIds = new HashSet<int>();
        var wireEndpointsByUId = new Dictionary<int, List<WireEndpoint>>();

        // Timers are no longer built in their own up-front phase — confirmed real, 2026-07-14
        // (FB MotorDOL, S1 item 26's live verification): TIA's own Import() validator rejects a
        // network where an unrelated production's own Parts sit physically between a TON and the
        // production that reads its Q, even though every dependency is still declared before its
        // dependent ("The elements must be sorted according to the current flow"). A Timer whose
        // Q/ET is read via a TimerOutputStep is now built inline, contiguous with whichever
        // production's chain first reaches it (EnsureTimerBuilt, called from BuildStep) — matching
        // the real TIA-original export's own ordering exactly (grounded directly against a fresh
        // export, not guessed). A Timer never referenced via TimerOutputStep (read only via an
        // ordinary Access elsewhere, e.g. FC ControlDelays' own Q read) has nothing to be
        // contiguous with, so it still falls through to the catch-all pass at the end, unchanged.
        var timersByTonPartUId = sidecar.Timers.ToDictionary(t => t.TonPartUId);

        for (var a = 0; a < network.Assignments.Count; a++)
        {
            var assignmentSidecar = sidecar.Assignments[a];
            BuildOneChain(network.Assignments[a], assignmentSidecar, network.Number, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

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
            BuildMove(moveSidecar, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

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
            BuildWordAnd(wordAndSidecar, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

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
            BuildCall(callSidecar, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

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

        for (var m = 0; m < network.Muls.Count; m++)
        {
            BuildMul(sidecar.Muls[m], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        for (var c2 = 0; c2 < network.Converts.Count; c2++)
        {
            BuildConvert(sidecar.Converts[c2], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        for (var s = 0; s < network.Swaps.Count; s++)
        {
            BuildSwap(sidecar.Swaps[s], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        for (var ab = 0; ab < network.AbsStatements.Count; ab++)
        {
            BuildAbs(sidecar.AbsStatements[ab], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        for (var lm = 0; lm < network.Limits.Count; lm++)
        {
            BuildLimit(sidecar.Limits[lm], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        for (var ts = 0; ts < network.TSubs.Count; ts++)
        {
            BuildTSub(sidecar.TSubs[ts], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        for (var tc = 0; tc < network.TConvs.Count; tc++)
        {
            BuildTConv(sidecar.TConvs[tc], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        for (var cc = 0; cc < network.Calcs.Count; cc++)
        {
            BuildCalc(sidecar.Calcs[cc], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        for (var mb = 0; mb < network.MoveBlkVariants.Count; mb++)
        {
            BuildMoveBlkVariant(sidecar.MoveBlkVariants[mb], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        for (var w = 0; w < network.Waits.Count; w++)
        {
            BuildWait(sidecar.Waits[w], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        for (var fb = 0; fb < network.FillBlockIs.Count; fb++)
        {
            BuildFillBlockI(sidecar.FillBlockIs[fb], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        for (var mm = 0; mm < network.ModbusMasters.Count; mm++)
        {
            BuildModbusMaster(sidecar.ModbusMasters[mm], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        for (var fs = 0; fs < network.FixedShapes.Count; fs++)
        {
            BuildFixedShape(sidecar.FixedShapes[fs], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        for (var mc = 0; mc < network.ModbusCommLoads.Count; mc++)
        {
            BuildModbusCommLoad(sidecar.ModbusCommLoads[mc], timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        // Catch-all: any Timer never reached via a TimerOutputStep above (read only via an
        // ordinary Access elsewhere, e.g. FC ControlDelays' own Q read) still needs to be built —
        // EnsureTimerBuilt no-ops for anything already built inline above.
        foreach (var timerSidecar in sidecar.Timers)
        {
            EnsureTimerBuilt(timerSidecar.TonPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        // Final sort by UId — the real, general fix for a whole class of "wrong document order"
        // bugs, found live 2026-07-14 via the full export/convert/import/compile/re-export cycle
        // run against every block already in the scratch project (not an isolated unit test).
        // Each production kind above (Assignments, Moves, WordAnds, Calls, Muls, Converts, Swaps)
        // is built in its own dedicated loop, so any interleaving *between* kinds — or even
        // between independent chains of the *same* kind — that existed in the real source gets
        // lost (`FB MotorStarter`'s own three interleaved `Mul->Convert` pairs came out grouped by
        // kind; `FB AirStarSystem`'s own four independent Coil-assignment chains, laid out with
        // their contacts spatially interleaved in the real ladder diagram, came out grouped by
        // chain instead). TIA's own Import() validator rejects both ("The elements must be sorted
        // according to the current flow"). Confirmed real: a Part's own UId is assigned by TIA in
        // true document order in every example grounded so far (this is also why `EnsureTimerBuilt`
        // above already treats it as a trustworthy position proxy) — so re-sorting the fully-built
        // list by UId reconstructs the real order regardless of which loop built each Part,
        // correctly handling every interleaving shape at once rather than special-casing each
        // pair of kinds as its own gap is found.
        parts.Sort((a, b) => a.UId.CompareTo(b.UId));

        // Same document-order fix as the Parts sort above, applied to each wire's own endpoint
        // list — real bug, found live 2026-07-14 (`FB AirStarSystem`): a rail wire shared by more
        // than two consumers needs its own endpoints listed in real document order too, not just
        // its Parts — TIA's own Import() validator rejects a mismatch here separately ("The parts
        // in the parts list and the connections in the power rail ... with more than two I/Os must
        // be located in the same sequence"). Endpoints were appended in whichever order each
        // production's own dedicated loop happened to reach the shared wire, not real document
        // order. A null UId (Powerrail itself, the wire's own producer) sorts first — the natural
        // "source before consumers" position, matching every real rail wire seen.
        var wires = wireEndpointsByUId
            .Select(kv => new WireNode(kv.Key, kv.Value.OrderBy(e => e.UId ?? int.MinValue).ToList()))
            .ToList();

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

    // Builds a TON/TONR Part, its IN-chain (identical shape/mechanism to BuildOneChain's chain,
    // just terminating at "IN" instead of a coil's "in" — and, like a Coil's chain, may have a
    // null RailWireUId if fed directly by another TON's Q), its PT wire (tag or literal preset),
    // its ET wire if the sidecar recorded one (OpenCon only), and — for TONR — its R (reset) wire
    // (tag or literal, same AddOperandWire as PT — confirmed real, 2026-07-12, S1 item 19).
    private static void BuildTimer(
        TimerBindingSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        for (var i = 0; i < sidecar.Steps.Count; i++)
        {
            var nextTarget = i + 1 < sidecar.Steps.Count
                ? EntryTarget(sidecar.Steps[i + 1])
                : new WireEndpoint(EndpointKind.NameCon, sidecar.TonPartUId, "IN");

            BuildStep(sidecar.Steps[i], nextTarget, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        var instance = new AccessNode(sidecar.InstanceUId, sidecar.InstanceScope, sidecar.InstanceComponentPath);
        AddPart(parts, emittedPartUIds, new PartNode(
            sidecar.TonPartUId, TimerPartNameFor(sidecar.Kind), Version: sidecar.Version, TimeType: sidecar.TimeType, Instance: instance));

        AddOperandWire(wireEndpointsByUId, sidecar.Preset, sidecar.TonPartUId, "PT");

        if (sidecar.Et is { } et)
        {
            AddEndpoint(wireEndpointsByUId, et.WireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.TonPartUId, "ET"));
            AddEndpoint(wireEndpointsByUId, et.WireUId, new WireEndpoint(EndpointKind.OpenCon, et.OpenConUId, null));
        }

        if (sidecar.Reset is { } reset)
        {
            AddOperandWire(wireEndpointsByUId, reset, sidecar.TonPartUId, "R");
        }
    }

    // Builds a Timer's own Part (and its upstream IN-chain) the first time anything needs it —
    // either inline, from BuildStep's own TimerOutputStep case (the common case: contiguous with
    // whichever production's chain first reads its Q/ET), or from Build()'s own catch-all pass at
    // the end (a Timer never referenced via TimerOutputStep, e.g. FC ControlDelays' own Q read via
    // an ordinary Access elsewhere, has nothing to be contiguous with). No-ops if already built —
    // confirmed real, 2026-07-14 (FB MotorDOL, S1 item 26's live verification): building every
    // Timer in its own global phase ahead of everything else put an unrelated production's Parts
    // between a TON and the production reading its Q, which TIA's own Import() validator rejected
    // ("The elements must be sorted according to the current flow") even though every dependency
    // was still technically declared before its dependent.
    private static void EnsureTimerBuilt(
        int tonPartUId,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        if (emittedPartUIds.Contains(tonPartUId))
        {
            return;
        }

        if (!timersByTonPartUId.TryGetValue(tonPartUId, out var timerSidecar))
        {
            throw new IrFormatException($"No timer sidecar found for TON Part UId={tonPartUId}.");
        }

        BuildTimer(timerSidecar, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        // RailWireUId is null when the chain's first step is a TimerOutputStep — the IN is fed
        // directly by another TON's Q, never touches Powerrail (confirmed real, 2026-07-11,
        // FC TimerSample's own pattern applied to IN).
        if (timerSidecar.RailWireUId is int timerRailWireUId)
        {
            var railFacingEndpoints = timerSidecar.Steps.Count > 0
                ? RailFacingEndpoints(timerSidecar.Steps[0])
                : new[] { (UId: timerSidecar.TonPartUId, Port: "IN") };
            AddRailEndpoints(wireEndpointsByUId, timerRailWireUId, railFacingEndpoints);
        }
    }

    // The inverse of GraphReducer.TimerKindFor — TimerBindingSidecar carries its own Kind
    // (BuildTimer works entirely off the sidecar, never cross-referencing the model), mirroring
    // CoilPartNameFor's own precedent.
    private static string TimerPartNameFor(TimerKind kind) => kind switch
    {
        TimerKind.Ton => "TON",
        TimerKind.Tonr => "TONR",
        TimerKind.Tof => "TOF",
        _ => throw new IrFormatException($"Unsupported timer kind: {kind}"),
    };

    // Builds a Move Part, its `en`-chain (identical mechanism to BuildOneChain/BuildTimer's own
    // chain, terminating at the Move's own "en" port instead of a Coil's "in"/TON's "IN" — may
    // have a null RailWireUId if `en` is fed directly by another TON's Q, same as any other
    // chain), its `in` wire (tag or literal source, same AddOperandWire as a TON's PT), and its
    // `out1` wire (the write target — same IdentCon-fed wire shape as an ordinary Contact/Coil
    // operand, just port "out1" and the opposite read/write direction).
    private static void BuildMove(
        MoveStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        for (var i = 0; i < sidecar.Steps.Count; i++)
        {
            var nextTarget = i + 1 < sidecar.Steps.Count
                ? EntryTarget(sidecar.Steps[i + 1])
                : new WireEndpoint(EndpointKind.NameCon, sidecar.MovePartUId, "en");

            BuildStep(sidecar.Steps[i], nextTarget, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
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
        WordAndStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        for (var i = 0; i < sidecar.Steps.Count; i++)
        {
            var nextTarget = i + 1 < sidecar.Steps.Count
                ? EntryTarget(sidecar.Steps[i + 1])
                : new WireEndpoint(EndpointKind.NameCon, sidecar.AndPartUId, "en");

            BuildStep(sidecar.Steps[i], nextTarget, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
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
        CallStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        for (var i = 0; i < sidecar.Steps.Count; i++)
        {
            var nextTarget = i + 1 < sidecar.Steps.Count
                ? EntryTarget(sidecar.Steps[i + 1])
                : new WireEndpoint(EndpointKind.NameCon, sidecar.CallPartUId, "en");

            BuildStep(sidecar.Steps[i], nextTarget, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        // Instance is optional — confirmed real, 2026-07-12 (S1 item 24): an FC call carries no
        // Instance at all, unlike every FB call, which always does.
        var instance = sidecar.InstanceUId is int instanceUId
            ? new AccessNode(instanceUId, sidecar.InstanceScope!, sidecar.InstanceComponentPath!)
            : null;
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

    // Builds an en-gated production's own `en` wiring — either the ordinary chain mechanism
    // (steps + rail, identical to every other production's own en/IN chain) or, confirmed real
    // 2026-07-12 (S1 item 18), a direct wire from the immediately preceding Mul/Convert's own
    // `eno` port. See EnSourceSidecar's own doc comment.
    private static void BuildEnSource(
        EnSourceSidecar en,
        int partUId,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        switch (en)
        {
            case EnSourceSidecar.ConditionSidecar condition:
                for (var i = 0; i < condition.Steps.Count; i++)
                {
                    var nextTarget = i + 1 < condition.Steps.Count
                        ? EntryTarget(condition.Steps[i + 1])
                        : new WireEndpoint(EndpointKind.NameCon, partUId, "en");

                    BuildStep(condition.Steps[i], nextTarget, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
                }

                if (condition.RailWireUId is int railWireUId)
                {
                    var railFacingEndpoints = condition.Steps.Count > 0
                        ? RailFacingEndpoints(condition.Steps[0])
                        : new[] { (UId: partUId, Port: "en") };
                    AddRailEndpoints(wireEndpointsByUId, railWireUId, railFacingEndpoints);
                }

                break;

            case EnSourceSidecar.PrecedingEnoSidecar precedingEno:
                AddEndpoint(wireEndpointsByUId, precedingEno.WireUId, new WireEndpoint(EndpointKind.NameCon, precedingEno.PrecedingPartUId, "eno"));
                AddEndpoint(wireEndpointsByUId, precedingEno.WireUId, new WireEndpoint(EndpointKind.NameCon, partUId, "en"));
                break;

            default:
                throw new IrFormatException($"Unsupported EnSource sidecar kind: {en.GetType().Name}");
        }
    }

    // Builds a Mul/Add Part, its `en` wiring (BuildEnSource), its N input wires (`in1`..`inK`,
    // `AddOperandWire` per input — positional, mirrors WAND's own Inputs list), and its `out`
    // wire (same IdentCon-fed wire shape as WAND's own dest). Its own type regenerates whichever
    // real shape the sidecar recorded — `sidecar.SrcType is null` means `<AutomaticTyped
    // Name="SrcType" />` (the original shape, confirmed real S1 item 19); non-null means an
    // ordinary `<TemplateValue Name="SrcType">` (confirmed real, 2026-07-12, S1 item 20 live
    // verification, `FB AirStar` — a genuine second real shape, not assumed universal either way).
    private static void BuildMul(
        MulStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        BuildEnSource(sidecar.En, sidecar.MulPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        // Mul/Add always regenerate a real Card element (confirmed real, every instance seen);
        // Sub/Div never do (2026-07-14, FC Scale — always binary, no Cardinality element in the
        // source) — Cardinality is left null on the PartNode for those two kinds specifically so
        // FlgNetWriter's own `if (part.Cardinality is not null)` correctly omits it.
        var cardinalityElement = RequiresCardinalityElement(sidecar.Kind) ? sidecar.Inputs.Count : (int?)null;
        AddPart(parts, emittedPartUIds, new PartNode(
            sidecar.MulPartUId, MulPartNameFor(sidecar.Kind), Cardinality: cardinalityElement,
            AutomaticSrcType: sidecar.SrcType is null, SrcType: sidecar.SrcType));

        for (var k = 0; k < sidecar.Inputs.Count; k++)
        {
            AddOperandWire(wireEndpointsByUId, sidecar.Inputs[k], sidecar.MulPartUId, $"in{k + 1}");
        }

        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DestAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.MulPartUId, "out"));
    }

    // The inverse of GraphReducer.MulKindFor — MulStatementSidecar carries its own Kind (BuildMul
    // works entirely off the sidecar, never cross-referencing the model), mirroring
    // CoilPartNameFor's/TimerPartNameFor's own precedent. Sub/Div (2026-07-14, FC Scale) extend
    // the same lookup.
    private static string MulPartNameFor(MulKind kind) => kind switch
    {
        MulKind.Multiply => "Mul",
        MulKind.Add => "Add",
        MulKind.Subtract => "Sub",
        MulKind.Divide => "Div",
        _ => throw new IrFormatException($"Unsupported Mul kind: {kind}"),
    };

    // Mul/Add carry a real Card element (confirmed real, S1 items 18/19); Sub/Div (2026-07-14,
    // FC Scale) never do — always binary, no real example has shown one.
    private static bool RequiresCardinalityElement(MulKind kind) => kind is MulKind.Multiply or MulKind.Add;

    // Builds a Convert Part, its `en` wiring (BuildEnSource), its `in` wire (tag or literal
    // source, same AddOperandWire as a TON's PT), and its `out` wire (same IdentCon-fed wire
    // shape as Move's own out1).
    private static void BuildConvert(
        ConvertStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        BuildEnSource(sidecar.En, sidecar.ConvertPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        AddPart(parts, emittedPartUIds, new PartNode(sidecar.ConvertPartUId, "Convert", SrcType: sidecar.SrcType, DestType: sidecar.DestType));

        AddOperandWire(wireEndpointsByUId, sidecar.In, sidecar.ConvertPartUId, "in");

        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DestAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.ConvertPartUId, "out"));
    }

    // Builds a Swap Part, its `en` wiring (BuildEnSource), its `in` wire (tag or literal source,
    // same AddOperandWire as a TON's PT), and its `out` wire (same IdentCon-fed wire shape as
    // Convert's own `out`). Mirrors BuildConvert exactly, minus DestType — a Swap Part carries
    // only SrcType.
    private static void BuildSwap(
        SwapStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        BuildEnSource(sidecar.En, sidecar.SwapPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        AddPart(parts, emittedPartUIds, new PartNode(sidecar.SwapPartUId, "Swap", SrcType: sidecar.SrcType));

        AddOperandWire(wireEndpointsByUId, sidecar.In, sidecar.SwapPartUId, "in");

        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DestAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.SwapPartUId, "out"));
    }

    // Builds an Abs Part — mirrors BuildSwap exactly (same shape, different source Part Name,
    // Phase 2 Tier 1).
    private static void BuildAbs(
        AbsStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        BuildEnSource(sidecar.En, sidecar.AbsPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        AddPart(parts, emittedPartUIds, new PartNode(sidecar.AbsPartUId, "Abs", SrcType: sidecar.SrcType));

        AddOperandWire(wireEndpointsByUId, sidecar.In, sidecar.AbsPartUId, "in");

        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DestAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.AbsPartUId, "out"));
    }

    // Builds a LIMIT Part, its `en` wiring, its three named-port inputs (`MN`/`IN`/`MX` — same
    // AddOperandWire as Convert/Swap/Abs's own `in`, just three calls instead of one, uppercase
    // port names matching the real source exactly — see LimitStatementSidecar's own doc comment),
    // and its `OUT` wire (uppercase, unlike every other typed instruction's lowercase `out`).
    // Version is carried on the PartNode itself (same field TON uses), not a TemplateValue.
    private static void BuildLimit(
        LimitStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        BuildEnSource(sidecar.En, sidecar.LimitPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        AddPart(parts, emittedPartUIds, new PartNode(sidecar.LimitPartUId, "LIMIT", Version: sidecar.Version, SrcType: sidecar.ValueType));

        AddOperandWire(wireEndpointsByUId, sidecar.Min, sidecar.LimitPartUId, "MN");
        AddOperandWire(wireEndpointsByUId, sidecar.In, sidecar.LimitPartUId, "IN");
        AddOperandWire(wireEndpointsByUId, sidecar.Max, sidecar.LimitPartUId, "MX");

        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DestAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.LimitPartUId, "OUT"));
    }

    // Builds a T_SUB Part, its `en` wiring, its two named-port inputs (`IN1`/`IN2`, uppercase —
    // same AddOperandWire as Sub's own `in1`/`in2`), and its `OUT` wire (uppercase). Version and
    // DateType/TimeType are carried on the PartNode itself.
    private static void BuildTSub(
        TSubStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        BuildEnSource(sidecar.En, sidecar.TSubPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        AddPart(parts, emittedPartUIds, new PartNode(
            sidecar.TSubPartUId, "T_SUB", Version: sidecar.Version, SrcType: sidecar.DateType, TimeType: sidecar.TimeType));

        AddOperandWire(wireEndpointsByUId, sidecar.In1, sidecar.TSubPartUId, "IN1");
        AddOperandWire(wireEndpointsByUId, sidecar.In2, sidecar.TSubPartUId, "IN2");

        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DestAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.TSubPartUId, "OUT"));
    }

    // Builds a T_CONV Part — mirrors BuildConvert exactly, plus Version, using the uppercase
    // `IN`/`OUT` port names confirmed real for T_CONV.
    private static void BuildTConv(
        TConvStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        BuildEnSource(sidecar.En, sidecar.TConvPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        AddPart(parts, emittedPartUIds, new PartNode(
            sidecar.TConvPartUId, "T_CONV", Version: sidecar.Version, SrcType: sidecar.SrcType, DestType: sidecar.DestType));

        AddOperandWire(wireEndpointsByUId, sidecar.In, sidecar.TConvPartUId, "IN");

        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DestAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.TConvPartUId, "OUT"));
    }

    // Builds a Calc Part — mirrors BuildMul's own Cardinality-driven input loop (lowercase
    // `in1`..`inN`/`out`, matching Calc's own confirmed-real port names), plus the Equation
    // string carried straight onto the PartNode (never parsed). Calc's SrcType is always an
    // explicit TemplateValue (AutomaticSrcType always false), unlike Mul/Add's own either/or.
    private static void BuildCalc(
        CalcStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        BuildEnSource(sidecar.En, sidecar.CalcPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        AddPart(parts, emittedPartUIds, new PartNode(
            sidecar.CalcPartUId, "Calc", Cardinality: sidecar.Inputs.Count, SrcType: sidecar.SrcType, Equation: sidecar.Equation));

        for (var k = 0; k < sidecar.Inputs.Count; k++)
        {
            AddOperandWire(wireEndpointsByUId, sidecar.Inputs[k], sidecar.CalcPartUId, $"in{k + 1}");
        }

        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DestAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.CalcPartUId, "out"));
    }

    // Builds a MOVE_BLK_VARIANT Part, its `en` wiring, its four named-port inputs (`SRC`/`COUNT`/
    // `SRC_INDEX`/`DEST_INDEX`, uppercase), and its two named-port outputs (`Ret_Val`/`DEST`,
    // mixed-case exactly as the real source has them) — the first production this converter
    // builds with two separate destination writes instead of one.
    private static void BuildMoveBlkVariant(
        MoveBlkVariantStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        BuildEnSource(sidecar.En, sidecar.MoveBlkVariantPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        AddPart(parts, emittedPartUIds, new PartNode(sidecar.MoveBlkVariantPartUId, "MOVE_BLK_VARIANT", Version: sidecar.Version));

        AddOperandWire(wireEndpointsByUId, sidecar.Src, sidecar.MoveBlkVariantPartUId, "SRC");
        AddOperandWire(wireEndpointsByUId, sidecar.Count, sidecar.MoveBlkVariantPartUId, "COUNT");
        AddOperandWire(wireEndpointsByUId, sidecar.SrcIndex, sidecar.MoveBlkVariantPartUId, "SRC_INDEX");
        AddOperandWire(wireEndpointsByUId, sidecar.DestIndex, sidecar.MoveBlkVariantPartUId, "DEST_INDEX");

        AddEndpoint(wireEndpointsByUId, sidecar.RetValWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.RetValAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.RetValWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.MoveBlkVariantPartUId, "Ret_Val"));

        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DestAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.MoveBlkVariantPartUId, "DEST"));
    }

    // Builds a WAIT Part, its `en` wiring, and its one named-port input (`WT`, uppercase) — no
    // destination wiring at all, see WaitStatementSidecar's own doc comment.
    private static void BuildWait(
        WaitStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        BuildEnSource(sidecar.En, sidecar.WaitPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        AddPart(parts, emittedPartUIds, new PartNode(sidecar.WaitPartUId, "WAIT", Version: sidecar.Version));

        AddOperandWire(wireEndpointsByUId, sidecar.Wt, sidecar.WaitPartUId, "WT");
    }

    // Builds a FillBlockI Part, its `en` wiring, its two named-port inputs (`in`/`count`,
    // lowercase — matching the real source exactly, unlike MOVE_BLK_VARIANT's own uppercase), and
    // its `out` wire (same IdentCon-fed shape as Move's own `out1`).
    private static void BuildFillBlockI(
        FillBlockIStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        BuildEnSource(sidecar.En, sidecar.FillBlockIPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        AddPart(parts, emittedPartUIds, new PartNode(sidecar.FillBlockIPartUId, "FillBlockI"));

        AddOperandWire(wireEndpointsByUId, sidecar.In, sidecar.FillBlockIPartUId, "in");
        AddOperandWire(wireEndpointsByUId, sidecar.Count, sidecar.FillBlockIPartUId, "count");

        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DestAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DestWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.FillBlockIPartUId, "out"));
    }

    // Builds a Modbus_Master Part, its `en` wiring, its Instance (same shape TON/Call already
    // use), its chain-fed `REQ` (BuildChainIntoPort — the one operand here resolved as a full
    // boolean chain, not a plain tag), its five ordinary named-port inputs, and its four
    // named-port outputs (Done/Busy/Error/Status — the second production this converter builds
    // with more than one destination write, after MOVE_BLK_VARIANT).
    private static void BuildModbusMaster(
        ModbusMasterStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        BuildEnSource(sidecar.En, sidecar.ModbusMasterPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        var instance = new AccessNode(sidecar.InstanceUId, sidecar.InstanceScope, sidecar.InstanceComponentPath);
        AddPart(parts, emittedPartUIds, new PartNode(sidecar.ModbusMasterPartUId, "Modbus_Master", Version: sidecar.Version, Instance: instance));

        BuildChainIntoPort(
            sidecar.ReqRailWireUId, sidecar.ReqSteps, sidecar.ModbusMasterPartUId, "REQ",
            timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        AddOperandWire(wireEndpointsByUId, sidecar.MbAddr, sidecar.ModbusMasterPartUId, "MB_ADDR");
        AddOperandWire(wireEndpointsByUId, sidecar.Mode, sidecar.ModbusMasterPartUId, "MODE");
        AddOperandWire(wireEndpointsByUId, sidecar.DataAddr, sidecar.ModbusMasterPartUId, "DATA_ADDR");
        AddOperandWire(wireEndpointsByUId, sidecar.DataLen, sidecar.ModbusMasterPartUId, "DATA_LEN");
        AddOperandWire(wireEndpointsByUId, sidecar.DataPtr, sidecar.ModbusMasterPartUId, "DATA_PTR");

        AddEndpoint(wireEndpointsByUId, sidecar.DoneWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DoneAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DoneWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.ModbusMasterPartUId, "DONE"));

        AddEndpoint(wireEndpointsByUId, sidecar.BusyWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.BusyAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.BusyWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.ModbusMasterPartUId, "BUSY"));

        AddEndpoint(wireEndpointsByUId, sidecar.ErrorWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.ErrorAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.ErrorWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.ModbusMasterPartUId, "ERROR"));

        AddEndpoint(wireEndpointsByUId, sidecar.StatusWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.StatusAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.StatusWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.ModbusMasterPartUId, "STATUS"));
    }

    // Builds a Modbus_Comm_Load Part, its `en` wiring, its Instance, four ordinary named-port
    // inputs, its three deliberately-unconnected ports (FlowCtrl/RtsOnDly/RtsOffDly — same
    // OpenCon-wiring shape TON's own ET already established), two more ordinary inputs, and its
    // three named-port outputs (Done/Error/Status).
    private static void BuildModbusCommLoad(
        ModbusCommLoadStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        BuildEnSource(sidecar.En, sidecar.ModbusCommLoadPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        var instance = new AccessNode(sidecar.InstanceUId, sidecar.InstanceScope, sidecar.InstanceComponentPath);
        AddPart(parts, emittedPartUIds, new PartNode(sidecar.ModbusCommLoadPartUId, "Modbus_Comm_Load", Version: sidecar.Version, Instance: instance));

        AddOperandWire(wireEndpointsByUId, sidecar.Req, sidecar.ModbusCommLoadPartUId, "REQ");
        AddOperandWire(wireEndpointsByUId, sidecar.Port, sidecar.ModbusCommLoadPartUId, "PORT");
        AddOperandWire(wireEndpointsByUId, sidecar.Baud, sidecar.ModbusCommLoadPartUId, "BAUD");
        AddOperandWire(wireEndpointsByUId, sidecar.Parity, sidecar.ModbusCommLoadPartUId, "PARITY");

        if (sidecar.FlowCtrl is { } flowCtrl)
        {
            AddEndpoint(wireEndpointsByUId, flowCtrl.WireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.ModbusCommLoadPartUId, "FLOW_CTRL"));
            AddEndpoint(wireEndpointsByUId, flowCtrl.WireUId, new WireEndpoint(EndpointKind.OpenCon, flowCtrl.OpenConUId, null));
        }

        if (sidecar.RtsOnDly is { } rtsOnDly)
        {
            AddEndpoint(wireEndpointsByUId, rtsOnDly.WireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.ModbusCommLoadPartUId, "RTS_ON_DLY"));
            AddEndpoint(wireEndpointsByUId, rtsOnDly.WireUId, new WireEndpoint(EndpointKind.OpenCon, rtsOnDly.OpenConUId, null));
        }

        if (sidecar.RtsOffDly is { } rtsOffDly)
        {
            AddEndpoint(wireEndpointsByUId, rtsOffDly.WireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.ModbusCommLoadPartUId, "RTS_OFF_DLY"));
            AddEndpoint(wireEndpointsByUId, rtsOffDly.WireUId, new WireEndpoint(EndpointKind.OpenCon, rtsOffDly.OpenConUId, null));
        }

        AddOperandWire(wireEndpointsByUId, sidecar.RespTo, sidecar.ModbusCommLoadPartUId, "RESP_TO");
        AddOperandWire(wireEndpointsByUId, sidecar.MbDb, sidecar.ModbusCommLoadPartUId, "MB_DB");

        AddEndpoint(wireEndpointsByUId, sidecar.DoneWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.DoneAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.DoneWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.ModbusCommLoadPartUId, "DONE"));

        AddEndpoint(wireEndpointsByUId, sidecar.ErrorWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.ErrorAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.ErrorWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.ModbusCommLoadPartUId, "ERROR"));

        AddEndpoint(wireEndpointsByUId, sidecar.StatusWireUId, new WireEndpoint(EndpointKind.IdentCon, sidecar.StatusAccessUId, null));
        AddEndpoint(wireEndpointsByUId, sidecar.StatusWireUId, new WireEndpoint(EndpointKind.NameCon, sidecar.ModbusCommLoadPartUId, "STATUS"));
    }

    // Builds a registry-driven fixed-shape instruction Part, its `en` wiring, its Instance (same
    // shape TON/Call/Modbus_* already use), and one wire per bound port.
    //
    // The registry is consulted here for one thing the sidecar deliberately does not carry: each
    // port's DIRECTION, which decides wire endpoint ORDER. An input wires
    // IdentCon/OpenCon → NameCon; an output wires NameCon → IdentCon/OpenCon. That order is not
    // semantically load-bearing for `Normalizer` (it sorts a wire's endpoints), but it is what a
    // real TIA export writes, and regenerating a document that differs from TIA's own shape for no
    // reason is how avoidable import surprises get manufactured.
    private static void BuildFixedShape(
        FixedShapeStatementSidecar sidecar,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        var template = FixedShapeInstructions.Require(sidecar.Instruction, sidecar.Version);

        BuildEnSource(sidecar.En, sidecar.PartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);

        var instance = new AccessNode(sidecar.InstanceUId, sidecar.InstanceScope, sidecar.InstanceComponentPath);
        AddPart(parts, emittedPartUIds, new PartNode(sidecar.PartUId, sidecar.Instruction, Version: sidecar.Version, Instance: instance));

        foreach (var argument in sidecar.Arguments)
        {
            var port = template.PortNamed(argument.Port)
                ?? throw new IrFormatException(
                    $"{sidecar.Instruction} {sidecar.Version} has no port named '{argument.Port}' — the sidecar and the " +
                    "converter's port template disagree.");

            var isOutput = port.Direction == PortDirection.Output;
            var (wireUId, otherEndpoint) = argument.Binding switch
            {
                PortBindingSidecar.Tag tag => (tag.WireUId, new WireEndpoint(EndpointKind.IdentCon, tag.AccessUId, null)),
                PortBindingSidecar.Literal literal => (literal.WireUId, new WireEndpoint(EndpointKind.IdentCon, literal.ConstantUId, null)),
                PortBindingSidecar.Open open => (open.WireUId, new WireEndpoint(EndpointKind.OpenCon, open.OpenConUId, null)),
                _ => throw new IrFormatException($"Unsupported PortBindingSidecar kind: {argument.Binding.GetType().Name}"),
            };

            var portEndpoint = new WireEndpoint(EndpointKind.NameCon, sidecar.PartUId, argument.Port);
            if (isOutput)
            {
                AddEndpoint(wireEndpointsByUId, wireUId, portEndpoint);
                AddEndpoint(wireEndpointsByUId, wireUId, otherEndpoint);
            }
            else
            {
                AddEndpoint(wireEndpointsByUId, wireUId, otherEndpoint);
                AddEndpoint(wireEndpointsByUId, wireUId, portEndpoint);
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

    // Builds a full boolean-chain into an arbitrary named port — the same steps-into-next-target,
    // rail-fallback shape BuildEnSource's own ConditionSidecar case and BuildOneChain's own
    // coil-chain already use, just generalized over the target port name rather than hardcoded to
    // "en"/"in". Introduced for Modbus_Master's own Req (Phase 2 Tier 4, 2026-07-14, confirmed
    // real fed by a full Contact chain, not a plain tag) — a new, standalone helper rather than
    // refactoring the three already-proven, live-verified call sites above, to avoid regression
    // risk on code no part of this task needed to touch.
    private static void BuildChainIntoPort(
        int? railWireUId,
        IReadOnlyList<ChainStepSidecar> steps,
        int partUId,
        string port,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
        List<PartNode> parts,
        HashSet<int> emittedPartUIds,
        Dictionary<int, List<WireEndpoint>> wireEndpointsByUId)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var nextTarget = i + 1 < steps.Count
                ? EntryTarget(steps[i + 1])
                : new WireEndpoint(EndpointKind.NameCon, partUId, port);

            BuildStep(steps[i], nextTarget, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
        }

        if (railWireUId is int rail)
        {
            var railFacingEndpoints = steps.Count > 0
                ? RailFacingEndpoints(steps[0])
                : new[] { (UId: partUId, Port: port) };
            AddRailEndpoints(wireEndpointsByUId, rail, railFacingEndpoints);
        }
    }

    private static void BuildOneChain(
        CoilAssignment assignment,
        CoilAssignmentSidecar sidecar,
        int networkNumber,
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
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

            BuildStep(sidecar.Steps[i], nextTarget, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
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
        IReadOnlyDictionary<int, TimerBindingSidecar> timersByTonPartUId,
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
                // The O Part itself is added AFTER its branches are built (mirrors NotStep's own
                // children-then-self order below) — not before. TIA's own Import() validator
                // requires Parts in signal-flow order (upstream before downstream); adding the O
                // Part first put a downstream element ahead of the upstream Contacts/nested-ORs
                // that feed it, which round-tripped fine locally (nothing here compares Part
                // order) but was rejected on real import: "The elements must be sorted according
                // to the current flow" — found live, 2026-07-14, FB MotorDOL's own nested
                // O(44)/O(41) shape (S1 item 26's live verification).
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

                        BuildStep(branch.Steps[i], nextTarget, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
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

                AddPart(parts, emittedPartUIds, new PartNode(orStep.OrPartUId, "O", Cardinality: orStep.Branches.Count));
                AddEndpoint(wireEndpointsByUId, orStep.OutgoingWireUId, new WireEndpoint(EndpointKind.NameCon, orStep.OrPartUId, "out"));
                AddEndpoint(wireEndpointsByUId, orStep.OutgoingWireUId, outgoingTarget);
                break;

            case ChainStepSidecar.TimerOutputStep timerOutput:
                // Builds the referenced TON's own Part (and its upstream IN-chain) inline, right
                // here, the first time any production's chain actually reads its Q/ET — no-ops if
                // already built by an earlier production or the catch-all pass. See
                // EnsureTimerBuilt's own doc comment for why this replaced the old "build every
                // Timer in its own global phase first" design.
                EnsureTimerBuilt(timerOutput.TonPartUId, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
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

                    BuildStep(notStep.Steps[i], nextTarget, timersByTonPartUId, parts, emittedPartUIds, wireEndpointsByUId);
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
