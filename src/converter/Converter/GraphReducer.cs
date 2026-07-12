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
        var constantsByUId = network.Constants.ToDictionary(c => c.UId);
        var wiresByPort = BuildPortIndex(network.Wires);

        var tonParts = network.Parts.Where(p => p.Name == "TON").ToList();
        // SCoil/RCoil (S1 item 15) are structurally identical to Coil — same "in"/"operand"
        // ports, never a producer — confirmed real, 2026-07-12, FC PlantAutoControl (two independent
        // instances of each). ReduceOneChain resolves all three via the exact same code, tagging
        // the result with CoilAssignment.Kind (derived from the Part Name below).
        var coils = network.Parts.Where(p => p.Name is "Coil" or "SCoil" or "RCoil").ToList();
        var moveParts = network.Parts.Where(p => p.Name == "Move").ToList();
        var wordAndParts = network.Parts.Where(p => p.Name == "And").ToList();
        var callParts = network.Parts.Where(p => p.Name == "Call").ToList();
        var mulParts = network.Parts.Where(p => p.Name == "Mul").ToList();
        var convertParts = network.Parts.Where(p => p.Name == "Convert").ToList();
        if (coils.Count == 0 && tonParts.Count == 0 && moveParts.Count == 0 && wordAndParts.Count == 0
            && callParts.Count == 0 && mulParts.Count == 0 && convertParts.Count == 0)
        {
            throw new NonReducibleNetworkException($"Network {networkNumber}: no Coil/SCoil/RCoil, TON, Move, And, Call, Mul, or Convert found.");
        }

        var assignments = new List<CoilAssignment>();
        var assignmentSidecars = new List<CoilAssignmentSidecar>();
        var timerBindings = new List<TimerBinding>();
        var timerSidecars = new List<TimerBindingSidecar>();
        var moveStatements = new List<MoveStatement>();
        var moveSidecars = new List<MoveStatementSidecar>();
        var wordAndStatements = new List<WordAndStatement>();
        var wordAndSidecars = new List<WordAndStatementSidecar>();
        var callStatements = new List<CallStatement>();
        var callSidecars = new List<CallStatementSidecar>();
        var mulStatements = new List<MulStatement>();
        var mulSidecars = new List<MulStatementSidecar>();
        var convertStatements = new List<ConvertStatement>();
        var convertSidecars = new List<ConvertStatementSidecar>();
        var allAccessEntries = new List<SidecarAccessEntry>();
        var allConstantEntries = new List<SidecarConstantEntry>();
        var visitedWireUIds = new HashSet<int>();

        // Timers are reduced first (order doesn't affect correctness — a Coil's own chain can
        // only reference a TON's Q via an ordinary Access elsewhere, see the note on
        // ChainStepSidecar, never via direct wire-graph traversal into this pass — but doing
        // timers first keeps their statements first in emitted IR, reads naturally).
        foreach (var ton in tonParts)
        {
            var (binding, sidecar, accessEntries, constantEntries) =
                ReduceTimer(network, ton, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            timerBindings.Add(binding);
            timerSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        foreach (var coil in coils)
        {
            var (assignment, sidecar, accessEntries, constantEntries) =
                ReduceOneChain(network, coil, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            assignments.Add(assignment);
            assignmentSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        // Moves are reduced last (order doesn't affect correctness, same reasoning as Timers-
        // before-Coils above — a Move's own `en`/`in` never depend on another Move's result via
        // wire-graph traversal). Telescoping Move chains (FB MotorDOL's cascade: each Move's `en`
        // trace re-walks the same upstream Contacts a prior Move's trace already walked) produce
        // duplicate ContactStep entries across sidecars by design — that's the reducer correctly
        // reporting each Move's own full condition, not a bug; FlgNetBuilder (not this reducer) is
        // where the resulting duplicate Part/Wire UIds get deduplicated on rebuild.
        foreach (var move in moveParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceMove(network, move, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            moveStatements.Add(statement);
            moveSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        // Word-ANDs are reduced last, same reasoning as Moves above (own `en`/`inK` never depend
        // on another production via wire-graph traversal; telescoping shared prefixes, if any,
        // are handled by the same dedup FlgNetBuilder already does for Move).
        foreach (var wordAnd in wordAndParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceWordAnd(network, wordAnd, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            wordAndStatements.Add(statement);
            wordAndSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        // Calls are reduced last, same reasoning as Moves/WordAnds above (own `en`/arguments
        // never depend on another production via wire-graph traversal; a shared upstream prefix,
        // if any, dedupes the same way FlgNetBuilder already handles for Move/WAND).
        foreach (var call in callParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceCall(network, call, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            callStatements.Add(statement);
            callSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        // Muls/Converts are reduced last, same reasoning as every other production above (own
        // `en`/inputs never depend on another production's own *reduction* completing first —
        // ResolveEnSource inspects the raw network.Parts/wiring directly for the ENO-chain shape,
        // not any already-reduced Model output — so Mul-before-Convert vs. Convert-before-Mul
        // ordering doesn't affect correctness, only which list a given statement ends up in).
        foreach (var mul in mulParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceMul(network, mul, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            mulStatements.Add(statement);
            mulSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        foreach (var convert in convertParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceConvert(network, convert, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            convertStatements.Add(statement);
            convertSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        if (visitedWireUIds.Count != network.Wires.Count)
        {
            throw new NonReducibleNetworkException(
                $"Network {networkNumber}: {network.Wires.Count - visitedWireUIds.Count} wire(s) unaccounted for after " +
                "reduction — unexpected topology, refusing to silently drop structure.");
        }

        var irNetwork = new IrNetwork(
            networkNumber, title, assignments, timerBindings, moveStatements, wordAndStatements, callStatements, null, mulStatements, convertStatements);
        var networkSidecar = new NetworkSidecar(
            networkNumber, compileUnitUId, allAccessEntries, assignmentSidecars, allConstantEntries, timerSidecars, moveSidecars, wordAndSidecars,
            callSidecars, mulSidecars, convertSidecars);
        return new ReducedNetwork(irNetwork, networkSidecar);
    }

    private static (CoilAssignment Assignment, CoilAssignmentSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceOneChain(
        FlgNetwork network,
        PartNode coil,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();
        var (condition, steps, railWireUId) = TraceChain(
            network, (coil.UId, "in"), wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (coilTag, coilOperandWireUId) = ResolveOperand(wiresByPort, accessByUId, coil.UId, networkNumber);
        visitedWireUIds.Add(coilOperandWireUId);
        AddAccessEntry(accessEntries, coilTag);

        var kind = CoilKindFor(coil.Name, networkNumber, coil.UId);
        var assignment = new CoilAssignment(coilTag.TagPath, condition, kind);
        var sidecar = new CoilAssignmentSidecar(railWireUId, steps, coil.UId, coilTag.UId, coilOperandWireUId);

        return (assignment, sidecar, accessEntries, constantEntries);
    }

    // Coil/SCoil/RCoil (S1 item 15) map 1:1 to CoilKind — no other Part Name has ever mapped to
    // one of these three kinds, so this is a straight lookup, not a guess.
    private static CoilKind CoilKindFor(string partName, int networkNumber, int uid) => partName switch
    {
        "Coil" => CoilKind.Assign,
        "SCoil" => CoilKind.Set,
        "RCoil" => CoilKind.Reset,
        _ => throw new NonReducibleNetworkException($"Network {networkNumber}: UId={uid} has unexpected Part Name '{partName}' for a coil-kind assignment."),
    };

    // A TON's IN is reduced exactly like a Coil's condition — same backward trace, terminating
    // at the TON's own "IN" port instead of a Coil's "in" (and, like a Coil's chain, may itself
    // terminate at another TON's Q instead of the rail — see TraceChain). PT is fed by an
    // IdentCon directly (no chain — it's a single operand, tag or literal), unlike Coil's own
    // "operand" pattern only in that there's no intervening Contact-chain concept for it. `Q`
    // itself is deliberately *not* validated here — whether/how it's consumed (an ordinary
    // Access elsewhere, confirmed real FC ControlDelays; or a direct wire into another chain,
    // confirmed real FC TimerSample) is entirely the consuming chain's concern via TraceChain;
    // this reduction only owns IN/PT/ET.
    private static (TimerBinding Binding, TimerBindingSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceTimer(
        FlgNetwork network,
        PartNode ton,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (inExpr, inSteps, inRailWireUId) = TraceChain(
            network, (ton.UId, "IN"), wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (ptExpr, presetSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, ton.UId, "PT", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var et = ResolveOptionalOutputPort(wiresByPort, ton.UId, "ET", networkNumber, visitedWireUIds);

        var instance = ton.Instance
            ?? throw new NonReducibleNetworkException($"Network {networkNumber}: TON UId={ton.UId} has no Instance reference.");
        var instancePath = string.Join('.', instance.ComponentPath);

        var binding = new TimerBinding(instancePath, inExpr, ptExpr);
        var sidecar = new TimerBindingSidecar(
            ton.UId,
            ton.TonVersion ?? throw new NonReducibleNetworkException($"Network {networkNumber}: TON UId={ton.UId} has no Version."),
            ton.TimeType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: TON UId={ton.UId} has no time_type."),
            instance.UId,
            instance.Scope,
            instance.ComponentPath,
            inRailWireUId,
            inSteps,
            presetSidecar,
            et);

        return (binding, sidecar, accessEntries, constantEntries);
    }

    // A Move's `en` is reduced exactly like a Coil's condition/TON's IN — same backward trace via
    // TraceChain, terminating at the Move's own "en" port (the tap wire shared with the chain's
    // real continuation — see TraceChain's producer-identification comment for the fan-out
    // shape). `in` is a single tag-or-literal operand (ResolveTagOrLiteralOperand, same resolver
    // as TON's PT / a comparison's operands). `out1` writes to a plain tag (ResolveOperand — same
    // IdentCon-fed wire shape as an ordinary Contact/Coil operand, just port "out1" instead of
    // "operand", and a write instead of a read).
    private static (MoveStatement Statement, MoveStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceMove(
        FlgNetwork network,
        PartNode move,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (enExpr, enSteps, enRailWireUId) = TraceChain(
            network, (move.UId, "en"), wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (inExpr, inSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, move.UId, "in", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (destTag, destWireUId) = ResolveOperand(wiresByPort, accessByUId, move.UId, networkNumber, "out1");
        visitedWireUIds.Add(destWireUId);
        AddAccessEntry(accessEntries, destTag);

        var statement = new MoveStatement(enExpr, inExpr, destTag.TagPath);
        var sidecar = new MoveStatementSidecar(move.UId, enRailWireUId, enSteps, inSidecar, destTag.UId, destWireUId);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // A bitwise-And's `en` is reduced exactly like a Move's — same TraceChain fan-out tap
    // mechanism (confirmed real, 2026-07-12, FB VSDUpdateComs: the And's `en` shares the same
    // rail wire as sibling Contacts elsewhere in the network, the same tap shape Move already
    // proved). Inputs are N tag-or-literal operands (`in1`..`inCard`, ResolveTagOrLiteralOperand
    // per port — same resolver as TON's PT/a comparison's operands/Move's own `in`), looped over
    // the Part's own Cardinality exactly like ResolveOrMerge already loops over an OR-merge's
    // branches. `out` writes to a plain tag (ResolveOperand, port "out" — confirmed real; not
    // "out1" like Move's own write port).
    private static (WordAndStatement Statement, WordAndStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceWordAnd(
        FlgNetwork network,
        PartNode wordAnd,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (enExpr, enSteps, enRailWireUId) = TraceChain(
            network, (wordAnd.UId, "en"), wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        if (wordAnd.Cardinality is not int cardinality || cardinality < 1)
        {
            throw new NonReducibleNetworkException($"Network {networkNumber}: And UId={wordAnd.UId} has no usable cardinality.");
        }

        var inputExprs = new List<Expr>();
        var inputSidecars = new List<OperandSidecar>();
        for (var k = 1; k <= cardinality; k++)
        {
            var (inputExpr, inputSidecar) = ResolveTagOrLiteralOperand(
                wiresByPort, accessByUId, constantsByUId, wordAnd.UId, $"in{k}", networkNumber, visitedWireUIds, accessEntries, constantEntries);
            inputExprs.Add(inputExpr);
            inputSidecars.Add(inputSidecar);
        }

        var (destTag, destWireUId) = ResolveOperand(wiresByPort, accessByUId, wordAnd.UId, networkNumber, "out");
        visitedWireUIds.Add(destWireUId);
        AddAccessEntry(accessEntries, destTag);

        var statement = new WordAndStatement(enExpr, inputExprs, destTag.TagPath);
        var sidecar = new WordAndStatementSidecar(
            wordAnd.UId,
            enRailWireUId,
            enSteps,
            inputSidecars,
            wordAnd.SrcType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: And UId={wordAnd.UId} has no SrcType."),
            destTag.UId,
            destWireUId);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // A Call's `en` is reduced exactly like Move/WAND's own — same TraceChain fan-out tap
    // mechanism (confirmed real, 2026-07-12, FC PlantAutoControl: all 20 real en's are directly
    // rail-fed, reducing to the existing "wired directly to rail" TRUE sentinel — same as WAND's
    // own live-verified case; a Contact-gated en on a Call specifically remains unconfirmed).
    // Instance is required (confirmed real: every Call carries one). Arguments are resolved per
    // the source's own sparse, ordered <Parameter> list (confirmed real: only wired parameters
    // appear at all, in source declaration order) — an Input parameter resolves via
    // ResolveTagOrLiteralOperand (same resolver as everywhere else); an Output parameter is a
    // bare destination tag (ResolveOperand — same IdentCon-fed wire shape as Move's own out1/
    // WAND's dest, just named per-parameter via the source's own Parameter Name instead of a
    // fixed port name).
    private static (CallStatement Statement, CallStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceCall(
        FlgNetwork network,
        PartNode call,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (enExpr, enSteps, enRailWireUId) = TraceChain(
            network, (call.UId, "en"), wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var instance = call.Instance
            ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Call UId={call.UId} has no Instance reference.");
        var instancePath = string.Join('.', instance.ComponentPath);

        var arguments = new List<CallArgument>();
        var argumentSidecars = new List<CallArgumentSidecar>();
        foreach (var parameter in call.CallParameters ?? Array.Empty<CallParameterNode>())
        {
            if (parameter.Section == "Input")
            {
                var (valueExpr, valueSidecar) = ResolveTagOrLiteralOperand(
                    wiresByPort, accessByUId, constantsByUId, call.UId, parameter.Name, networkNumber, visitedWireUIds, accessEntries, constantEntries);
                arguments.Add(new CallArgument.InputArg(parameter.Name, valueExpr));
                argumentSidecars.Add(new CallArgumentSidecar.InputArgSidecar(parameter.Name, parameter.Type, valueSidecar));
            }
            else
            {
                var (destTag, destWireUId) = ResolveOperand(wiresByPort, accessByUId, call.UId, networkNumber, parameter.Name);
                visitedWireUIds.Add(destWireUId);
                AddAccessEntry(accessEntries, destTag);
                arguments.Add(new CallArgument.OutputArg(parameter.Name, destTag.TagPath));
                argumentSidecars.Add(new CallArgumentSidecar.OutputArgSidecar(parameter.Name, parameter.Type, destTag.UId, destWireUId));
            }
        }

        var blockName = call.BlockName ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Call UId={call.UId} has no BlockName.");
        var blockType = call.BlockType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Call UId={call.UId} has no BlockType.");

        var statement = new CallStatement(blockName, instancePath, enExpr, arguments);
        var sidecar = new CallStatementSidecar(
            call.UId,
            blockName,
            blockType,
            enRailWireUId,
            enSteps,
            instance.UId,
            instance.Scope,
            instance.ComponentPath,
            argumentSidecars);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // Resolves an en-gated production's own `en` source — either an ordinary boolean condition
    // (TraceChain, same mechanism as every other production) or, confirmed real 2026-07-12 (S1
    // item 18, Mul->Convert pairs in FB MotorDOL/EquipmentControlSystem), the immediately preceding
    // Mul/Convert's own `eno` output wired directly in. Checked first, by inspecting the `en`
    // wire's own producer shape, rather than assumed either way — a wire whose only other
    // endpoint is a Mul/Convert Part's own "eno" port is the ENO-chained case; everything else
    // (Powerrail, a Contact chain, ...) falls through to the ordinary TraceChain path unchanged.
    // See EnSource's own doc comment for why this isn't folded into TraceChain's own
    // Expr-producing recursion — there's no tag to represent "the preceding instruction's own
    // success" as a boolean value.
    private static (EnSource En, EnSourceSidecar Sidecar) ResolveEnSource(
        FlgNetwork network,
        int partUId,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds,
        List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries)
    {
        var wire = RequireWireAt(wiresByPort, (partUId, "en"), networkNumber);
        var others = wire.Endpoints
            .Where(e => !(e.Kind == EndpointKind.NameCon && e.UId == partUId && e.PortName == "en"))
            .ToList();

        if (others.Count == 1
            && others[0].Kind == EndpointKind.NameCon
            && others[0].PortName == "eno"
            && network.Parts.FirstOrDefault(p => p.UId == others[0].UId!.Value) is { } precedingPart
            && precedingPart.Name is "Mul" or "Convert")
        {
            visitedWireUIds.Add(wire.UId);
            return (new EnSource.PrecedingEno(), new EnSourceSidecar.PrecedingEnoSidecar(precedingPart.UId, wire.UId));
        }

        var (expr, steps, railWireUId) = TraceChain(
            network, (partUId, "en"), wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);
        return (new EnSource.Condition(expr), new EnSourceSidecar.ConditionSidecar(railWireUId, steps));
    }

    // A Mul's `en` is resolved via ResolveEnSource (ordinary condition or ENO-chained — see its
    // own doc comment). Inputs are Cardinality-driven tag-or-literal operands (`in1`..`inCard`,
    // ResolveTagOrLiteralOperand per port — same resolver as everywhere else, same loop shape as
    // ResolveOrMerge/ReduceWordAnd's own Cardinality loop). `out` writes to a plain tag
    // (ResolveOperand, port "out" — confirmed real, same as WAND's own write port).
    private static (MulStatement Statement, MulStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceMul(
        FlgNetwork network,
        PartNode mul,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (en, enSidecar) = ResolveEnSource(
            network, mul.UId, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        if (mul.Cardinality is not int cardinality || cardinality < 1)
        {
            throw new NonReducibleNetworkException($"Network {networkNumber}: Mul UId={mul.UId} has no usable cardinality.");
        }

        var inputExprs = new List<Expr>();
        var inputSidecars = new List<OperandSidecar>();
        for (var k = 1; k <= cardinality; k++)
        {
            var (inputExpr, inputSidecar) = ResolveTagOrLiteralOperand(
                wiresByPort, accessByUId, constantsByUId, mul.UId, $"in{k}", networkNumber, visitedWireUIds, accessEntries, constantEntries);
            inputExprs.Add(inputExpr);
            inputSidecars.Add(inputSidecar);
        }

        var (destTag, destWireUId) = ResolveOperand(wiresByPort, accessByUId, mul.UId, networkNumber, "out");
        visitedWireUIds.Add(destWireUId);
        AddAccessEntry(accessEntries, destTag);

        var statement = new MulStatement(en, inputExprs, destTag.TagPath);
        var sidecar = new MulStatementSidecar(mul.UId, enSidecar, inputSidecars, destTag.UId, destWireUId);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // A Convert's `en` is resolved via ResolveEnSource (ordinary condition, confirmed real
    // standalone in FB ShredderControlSystem; or ENO-chained after a Mul, confirmed real in
    // MotorDOL/EquipmentControlSystem). `in` is a single tag-or-literal operand (ResolveTagOrLiteralOperand).
    // `out` writes to a plain tag (ResolveOperand, same shape as Move's own out1).
    private static (ConvertStatement Statement, ConvertStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceConvert(
        FlgNetwork network,
        PartNode convert,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (en, enSidecar) = ResolveEnSource(
            network, convert.UId, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (inExpr, inSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, convert.UId, "in", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (destTag, destWireUId) = ResolveOperand(wiresByPort, accessByUId, convert.UId, networkNumber, "out");
        visitedWireUIds.Add(destWireUId);
        AddAccessEntry(accessEntries, destTag);

        var statement = new ConvertStatement(en, inExpr, destTag.TagPath);
        var sidecar = new ConvertStatementSidecar(
            convert.UId,
            enSidecar,
            inSidecar,
            convert.SrcType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Convert UId={convert.UId} has no SrcType."),
            convert.DestType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Convert UId={convert.UId} has no DestType."),
            destTag.UId,
            destWireUId);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // Resolves a single IdentCon-fed operand (no chain, unlike a boolean chain position) that's
    // either an ordinary tag (AccessNode) or a literal constant (ConstantAccessNode) — used for
    // a TON's `PT` (confirmed real 2026-07-11, FB MotorDOL / FC ControlDelays) and, since the
    // same date, a comparison's `in1`/`in2` (FC ControlDelays) — one shared resolver rather than
    // duplicating the tag-vs-constant branch per caller, parameterized by which port to resolve.
    private static (Expr Expr, OperandSidecar Sidecar) ResolveTagOrLiteralOperand(
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int partUId,
        string port,
        int networkNumber,
        HashSet<int> visitedWireUIds,
        List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries)
    {
        var wire = RequireWireAt(wiresByPort, (partUId, port), networkNumber);
        var identCon = wire.Endpoints.FirstOrDefault(e => e.Kind == EndpointKind.IdentCon)
            ?? throw new NonReducibleNetworkException(
                $"Network {networkNumber}: {port} wire {wire.UId} for UId={partUId} has no IdentCon source.");

        visitedWireUIds.Add(wire.UId);

        if (accessByUId.TryGetValue(identCon.UId!.Value, out var access))
        {
            AddAccessEntry(accessEntries, new SidecarAccessEntry(access.DottedPath, access.UId, access.Scope));
            return (new Expr.TagRef(access.DottedPath), new OperandSidecar.TagOperand(access.UId, wire.UId));
        }

        if (constantsByUId.TryGetValue(identCon.UId!.Value, out var constant))
        {
            AddConstantEntry(constantEntries, new SidecarConstantEntry(constant.Value, constant.UId, constant.ConstantType));
            return (new Expr.Literal(constant.Value), new OperandSidecar.LiteralOperand(constant.UId, wire.UId));
        }

        throw new NonReducibleNetworkException(
            $"Network {networkNumber}: {port} wire {wire.UId} for UId={partUId} references unknown Access/Constant UId={identCon.UId}.");
    }

    private static void AddConstantEntry(List<SidecarConstantEntry> entries, SidecarConstantEntry entry)
    {
        if (!entries.Any(e => e.UId == entry.UId))
        {
            entries.Add(entry);
        }
    }

    // Each part kind's own "out"-equivalent port name — the port TraceChain looks for when
    // identifying a wire's producer. Contact/O/Eq/Ge/Not use "out"; a TON's only confirmed real
    // upstream leaf is "Q" ("ET" has no live example as a consumed leaf, refused like any other
    // unrecognized shape). Move/And never appear here — neither is ever a producer for a boolean
    // chain, only a consumer (their own "en" tap) — see TraceChain's producer-identification
    // comment.
    private static string? OutPortFor(string partName) => partName switch
    {
        "Contact" or "O" or "Eq" or "Ge" or "Not" => "out",
        "TON" => "Q",
        _ => null,
    };

    // IR-text infix operator per Part Name — only Eq/Ge confirmed real (ir/SPEC.md's readable-form
    // table already sketches the full IEC family, but Ne/Le/Gt/Lt Part Names are unconfirmed, so
    // only these two are reachable — SupportedComparisonPartNames gates this at parse time).
    private static string ComparisonOperator(string partName) => partName switch
    {
        "Eq" => "=",
        "Ge" => ">=",
        _ => throw new UnsupportedConstructException($"Unsupported comparison Part Name '{partName}'."),
    };

    // ET is an optional output port — confirmed real, 2026-07-11: entirely absent from <Wires>,
    // or wired to OpenCon (FB MotorDOL). A wire to any other endpoint is refused — no live
    // example of a *used* ET exists yet (unlike Q, which TraceChain now handles as a genuine
    // chain leaf when wired directly to a consumer — see ChainStepSidecar.TimerOutputStep).
    private static OpenConnectionSidecar? ResolveOptionalOutputPort(
        Dictionary<(int, string), WireNode> wiresByPort,
        int tonUId,
        string port,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        if (!wiresByPort.TryGetValue((tonUId, port), out var wire))
        {
            return null;
        }

        var others = wire.Endpoints
            .Where(e => !(e.Kind == EndpointKind.NameCon && e.UId == tonUId && e.PortName == port))
            .ToList();

        if (others.Count != 1 || others[0].Kind != EndpointKind.OpenCon)
        {
            throw new UnsupportedConstructException(
                $"Network {networkNumber}: TON UId={tonUId}'s '{port}' port is wired to a consumer — only an unconnected " +
                $"(absent or OpenCon) '{port}' is supported this phase; reading a TON's output back only works via an " +
                "ordinary Access elsewhere.");
        }

        visitedWireUIds.Add(wire.UId);
        return new OpenConnectionSidecar(wire.UId, others[0].UId!.Value);
    }

    private static void AddAccessEntry(List<SidecarAccessEntry> entries, SidecarAccessEntry entry)
    {
        if (!entries.Any(e => e.TagPath == entry.TagPath && e.UId == entry.UId))
        {
            entries.Add(entry);
        }
    }

    // Shared backward trace from a starting port (a Coil's "in", or a TON's "IN") to the rail —
    // one position at a time, rail-to-coil order once reversed. Each position is a single
    // Contact or comparison (Eq/Ge — confirmed real, 2026-07-11, FC ControlDelays; both are
    // pass-through positions, chain continues further upstream), an OR-merge (Part Name="O"), or
    // an already-defined TON's Q output — the last confirmed real, 2026-07-11, `FC TimerSample`
    // (purpose-built by the project owner to close this gap: Q wired directly into a plain
    // Coil). OR-merge and TON-via-Q are both always rail-facing/terminal — nothing further
    // upstream to trace within *this* chain. When a TON's Q terminates the chain, the chain
    // never touches Powerrail at all, so RailWireUId comes back null (see TimerOutputStep's doc
    // comment). Used identically by ReduceOneChain and ReduceTimer: a TON's IN is reduced
    // exactly the same way a Coil's condition is, just terminating at a different port name.
    private static (Expr Condition, List<ChainStepSidecar> Steps, int? RailWireUId) TraceChain(
        FlgNetwork network,
        (int UId, string Port) startPort,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds,
        List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries)
    {
        var steps = new List<ChainStepSidecar>();
        var stepExprs = new List<Expr>();
        var currentInPort = startPort;
        int? railWireUId = null;

        while (true)
        {
            var wire = RequireWireAt(wiresByPort, (currentInPort.UId, currentInPort.Port), networkNumber);

            if (wire.Endpoints.Any(e => e.Kind == EndpointKind.Powerrail))
            {
                // Reached the rail. Other endpoints on this same wire (if any) belong to
                // sibling chains' first elements — not this chain's concern, not fan-out.
                visitedWireUIds.Add(wire.UId);
                railWireUId = wire.UId;
                break;
            }

            var others = wire.Endpoints
                .Where(e => !(e.Kind == EndpointKind.NameCon && e.UId == currentInPort.UId && e.PortName == currentInPort.Port))
                .ToList();

            // A wire feeding this chain position has exactly one producer — Powerrail (handled
            // above) or the single upstream Part whose "out"-equivalent port matches. Every other
            // endpoint on the wire is a consumer we don't care about here: most commonly a Move's
            // "en" tap sharing the wire with the chain's real continuation (confirmed real,
            // FB MotorDOL — one wire, three endpoints: producer, Move.en tap, next-position.in).
            // Those taps belong to their own Move production, not this trace, so they're simply
            // ignored rather than treated as fan-out that breaks series-purity.
            var producerCandidates = others
                .Where(e => e.Kind == EndpointKind.NameCon)
                .Select(e => (Endpoint: e, Part: network.Parts.FirstOrDefault(p => p.UId == e.UId)))
                .Where(x => x.Part is not null && x.Endpoint.PortName == OutPortFor(x.Part.Name))
                .ToList();

            if (producerCandidates.Count != 1)
            {
                throw new NonReducibleNetworkException(
                    $"Network {networkNumber}: wire {wire.UId} feeding ({currentInPort.UId}, {currentInPort.Port}) has " +
                    $"{producerCandidates.Count} candidate producer endpoint(s) (expected exactly 1) among {others.Count} " +
                    "other endpoint(s) — not a supported chain shape.");
            }

            var other = producerCandidates[0].Endpoint;
            var upstreamPart = producerCandidates[0].Part!;

            visitedWireUIds.Add(wire.UId);
            var outgoingWireUId = wire.UId;

            if (upstreamPart.Name == "TON")
            {
                var instancePath = string.Join('.', upstreamPart.Instance!.ComponentPath);
                steps.Insert(0, new ChainStepSidecar.TimerOutputStep(upstreamPart.UId, "Q", outgoingWireUId));
                stepExprs.Insert(0, new Expr.TagRef($"{instancePath}.Q"));
                break;
            }

            if (upstreamPart.Name == "Contact")
            {
                var (tag, operandWireUId) = ResolveOperand(wiresByPort, accessByUId, upstreamPart.UId, networkNumber);
                visitedWireUIds.Add(operandWireUId);
                AddAccessEntry(accessEntries, tag);

                Expr operandExpr = upstreamPart.Negated ? new Expr.Not(new Expr.TagRef(tag.TagPath)) : new Expr.TagRef(tag.TagPath);
                steps.Insert(0, new ChainStepSidecar.ContactStep(upstreamPart.UId, tag.UId, operandWireUId, upstreamPart.Negated, outgoingWireUId));
                stepExprs.Insert(0, operandExpr);
                currentInPort = (upstreamPart.UId, "in");
                continue;
            }

            if (upstreamPart.Name == "Eq" || upstreamPart.Name == "Ge")
            {
                var (leftExpr, leftOperand) = ResolveTagOrLiteralOperand(
                    wiresByPort, accessByUId, constantsByUId, upstreamPart.UId, "in1", networkNumber, visitedWireUIds, accessEntries, constantEntries);
                var (rightExpr, rightOperand) = ResolveTagOrLiteralOperand(
                    wiresByPort, accessByUId, constantsByUId, upstreamPart.UId, "in2", networkNumber, visitedWireUIds, accessEntries, constantEntries);

                var compareStep = new ChainStepSidecar.CompareStep(
                    upstreamPart.UId,
                    upstreamPart.Name,
                    upstreamPart.SrcType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: comparison UId={upstreamPart.UId} has no SrcType."),
                    leftOperand,
                    rightOperand,
                    outgoingWireUId);
                steps.Insert(0, compareStep);
                stepExprs.Insert(0, new Expr.Compare(ComparisonOperator(upstreamPart.Name), leftExpr, rightExpr));
                // Eq/Ge's own rail-facing/continuation port is "pre" — genuinely different from
                // a Contact's "in", not a typo (confirmed real, 2026-07-11, FC ControlDelays).
                currentInPort = (upstreamPart.UId, "pre");
                continue;
            }

            if (upstreamPart.Name == "O")
            {
                // An OR-merge's own branches may diverge (one rail-fed, another terminating at a
                // TON's Q, ...), so there's no longer a single shared rail wire to bubble up here
                // — the outer chain's own RailWireUId stays null, exactly like a TimerOutputStep
                // above (each OrBranch carries its own, see ResolveOrMerge).
                var (orStep, orExpr) = ResolveOrMerge(
                    network, wiresByPort, accessByUId, constantsByUId, upstreamPart, outgoingWireUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);
                steps.Insert(0, orStep);
                stepExprs.Insert(0, orExpr);
                break;
            }

            if (upstreamPart.Name == "Not")
            {
                // A Not must wrap only its own upstream in Expr.Not, not the whole rest of this
                // chain — resolved via a fully self-contained, recursive TraceChain call on the
                // Not's own "in" (exactly like an OR-merge branch's own resolution), not by
                // continuing the current loop. The recursive call already resolves everything
                // upstream of the Not, so — like an OrStep — nothing further to trace here.
                var (notExpr, notSteps, notRailWireUId) = TraceChain(
                    network, (upstreamPart.UId, "in"), wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);
                steps.Insert(0, new ChainStepSidecar.NotStep(upstreamPart.UId, notSteps, notRailWireUId, outgoingWireUId));
                stepExprs.Insert(0, new Expr.Not(notExpr));
                break;
            }

            throw new NonReducibleNetworkException(
                $"Network {networkNumber}: upstream of ({currentInPort.UId}, {currentInPort.Port}) is a '{upstreamPart.Name}', not a Contact — outside this slice.");
        }

        Expr condition = stepExprs.Count switch
        {
            0 => new Expr.And(Array.Empty<Expr>()), // wired directly to the rail — always on
            1 => stepExprs[0],
            _ => new Expr.And(stepExprs),
        };

        return (condition, steps, railWireUId);
    }

    // An OR-merge's branches are ordinary chains (S1 item 11, 2026-07-11): each `inK` port is
    // resolved via the exact same TraceChain mechanism used for a Coil's `in`, a TON's `IN`, or a
    // Move's `en` — recursion, not a bespoke branch walker. This replaces the original
    // single-Contact-only, rail-required special case (S1 item 7) with the general mechanism,
    // confirmed real 2026-07-11 against `FC ControlDelays` (`O(41)`'s branches are comparisons,
    // each itself fed by a further OR-merge rather than Powerrail) and `FB MotorDOL` (`O(45)`'s
    // branches are Contacts fed by a shared upstream Contact's own fan-out, not directly
    // rail-fed — the same telescoping-shared-prefix shape MOVE's FlgNetBuilder dedup already
    // handles for free, since it's the identical wire/UId-sharing mechanism, just reached from a
    // branch instead of a Move tap). Whatever's genuinely upstream of a branch — Contact,
    // comparison, nested OR-merge, even a TON's Q — is handled by TraceChain's own existing
    // per-part-kind dispatch; nothing here special-cases any of it.
    private static (ChainStepSidecar.OrStep OrStep, Expr Expr) ResolveOrMerge(
        FlgNetwork network,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        PartNode orPart,
        int outgoingWireUId,
        int networkNumber,
        HashSet<int> visitedWireUIds,
        List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries)
    {
        if (orPart.Cardinality is not int cardinality || cardinality < 1)
        {
            throw new NonReducibleNetworkException($"Network {networkNumber}: OR-merge UId={orPart.UId} has no usable cardinality.");
        }

        var branches = new List<OrBranch>();
        var branchExprs = new List<Expr>();

        for (var k = 1; k <= cardinality; k++)
        {
            var (branchExpr, branchSteps, branchRailWireUId) = TraceChain(
                network, (orPart.UId, $"in{k}"), wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

            branches.Add(new OrBranch(branchSteps, branchRailWireUId));
            branchExprs.Add(branchExpr);
        }

        return (new ChainStepSidecar.OrStep(orPart.UId, branches, outgoingWireUId), new Expr.Or(branchExprs));
    }

    // Port defaults to "operand" (every Contact/Coil operand) — Move's write-side target uses
    // "out1" instead (see ReduceMove); the wire shape is otherwise identical, an IdentCon-fed
    // Access, only the read/write direction differs semantically, not structurally.
    private static (SidecarAccessEntry Tag, int WireUId) ResolveOperand(
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        int partUId,
        int networkNumber,
        string port = "operand")
    {
        var wire = RequireWireAt(wiresByPort, (partUId, port), networkNumber);
        var identCon = wire.Endpoints.FirstOrDefault(e => e.Kind == EndpointKind.IdentCon)
            ?? throw new NonReducibleNetworkException(
                $"Network {networkNumber}: {port} wire {wire.UId} for UId={partUId} has no IdentCon source.");

        var access = accessByUId.TryGetValue(identCon.UId!.Value, out var found)
            ? found
            : throw new NonReducibleNetworkException(
                $"Network {networkNumber}: operand wire {wire.UId} references unknown Access UId={identCon.UId}.");

        return (new SidecarAccessEntry(access.DottedPath, access.UId, access.Scope), wire.UId);
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
