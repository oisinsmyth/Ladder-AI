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

        // TON/TONR/TOF (S1 items 8/19/23) are reduced via the exact same code, tagging the result
        // with TimerBinding.Kind (derived from the Part Name below) — identical Version/Instance/
        // time_type shape for all three; TONR adds one extra `R` port; TOF is structurally
        // identical to TON (no extra port), confirmed real 2026-07-12.
        var tonParts = network.Parts.Where(p => p.Name is "TON" or "TONR" or "TOF").ToList();
        // SCoil/RCoil (S1 item 15) are structurally identical to Coil — same "in"/"operand"
        // ports, never a producer — confirmed real, 2026-07-12, FC PlantAutoControl (two independent
        // instances of each). ReduceOneChain resolves all three via the exact same code, tagging
        // the result with CoilAssignment.Kind (derived from the Part Name below).
        var coils = network.Parts.Where(p => p.Name is "Coil" or "SCoil" or "RCoil").ToList();
        var moveParts = network.Parts.Where(p => p.Name == "Move").ToList();
        var wordAndParts = network.Parts.Where(p => p.Name == "And").ToList();
        var callParts = network.Parts.Where(p => p.Name == "Call").ToList();
        // Mul/Add (S1 items 18/19) share the exact same shape and reduction — tagged with
        // MulStatement.Kind (derived from the Part Name below). Sub/Div (2026-07-14, FC Scale)
        // reuse the identical reduction too — always-binary, no Cardinality element (see
        // ReduceMulOrAdd's own cardinality-defaulting comment).
        var mulParts = network.Parts.Where(p => p.Name is "Mul" or "Add" or "Sub" or "Div").ToList();
        var convertParts = network.Parts.Where(p => p.Name == "Convert").ToList();
        // Swap (S1 item 25) is structurally identical to Convert minus DestType — confirmed real,
        // 2026-07-12, FB TomraControlSystem.
        var swapParts = network.Parts.Where(p => p.Name == "Swap").ToList();
        // Abs (Phase 2 Tier 1, 2026-07-14, FB VSDSim) shares Swap's exact shape (en-gated, single
        // SrcType, one in/out) — a different source Part Name, same reduction.
        var absParts = network.Parts.Where(p => p.Name == "Abs").ToList();
        // LIMIT (Phase 2 Tier 1, 2026-07-14, FB VSDSim) is its own shape — three fixed-named
        // inputs (MN/IN/MX), see LimitStatement's own doc comment.
        var limitParts = network.Parts.Where(p => p.Name == "LIMIT").ToList();
        // T_SUB/T_CONV (Phase 2 Tier 2, 2026-07-14, FB VibratorCycle) — time-arithmetic variants
        // of Sub/Convert, see TSubStatement/TConvStatement's own doc comments.
        var tSubParts = network.Parts.Where(p => p.Name == "T_SUB").ToList();
        var tConvParts = network.Parts.Where(p => p.Name == "T_CONV").ToList();
        // Calc (Phase 2 Tier 3, 2026-07-14, FB VSDSim) — Cardinality-driven inputs like Mul/Add,
        // see CalcStatement's own doc comment.
        var calcParts = network.Parts.Where(p => p.Name == "Calc").ToList();
        // MOVE_BLK_VARIANT (Phase 2 Tier 5, 2026-07-14, FC MoveData/VSDDataSequence) — see
        // MoveBlkVariantStatement's own doc comment.
        var moveBlkVariantParts = network.Parts.Where(p => p.Name == "MOVE_BLK_VARIANT").ToList();
        // WAIT/FillBlockI (Phase 2 Tier 6, 2026-07-14, FC VSDDataSequence/ModbusComs) — see
        // WaitStatement/FillBlockIStatement's own doc comments.
        var waitParts = network.Parts.Where(p => p.Name == "WAIT").ToList();
        var fillBlockIParts = network.Parts.Where(p => p.Name == "FillBlockI").ToList();
        // Modbus_Master/Modbus_Comm_Load (Phase 2 Tier 4, 2026-07-14, FC ModbusComs) — see
        // ModbusMasterStatement/ModbusCommLoadStatement's own doc comments.
        var modbusMasterParts = network.Parts.Where(p => p.Name == "Modbus_Master").ToList();
        var modbusCommLoadParts = network.Parts.Where(p => p.Name == "Modbus_Comm_Load").ToList();
        if (coils.Count == 0 && tonParts.Count == 0 && moveParts.Count == 0 && wordAndParts.Count == 0
            && callParts.Count == 0 && mulParts.Count == 0 && convertParts.Count == 0 && swapParts.Count == 0
            && absParts.Count == 0 && limitParts.Count == 0 && tSubParts.Count == 0 && tConvParts.Count == 0
            && calcParts.Count == 0 && moveBlkVariantParts.Count == 0 && waitParts.Count == 0 && fillBlockIParts.Count == 0
            && modbusMasterParts.Count == 0 && modbusCommLoadParts.Count == 0)
        {
            throw new NonReducibleNetworkException($"Network {networkNumber}: no Coil/SCoil/RCoil, TON/TONR/TOF, Move, And, Call, Mul/Add, Convert, Swap, Abs, LIMIT, T_SUB, T_CONV, Calc, MOVE_BLK_VARIANT, WAIT, FillBlockI, Modbus_Master, or Modbus_Comm_Load found.");
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
        var swapStatements = new List<SwapStatement>();
        var swapSidecars = new List<SwapStatementSidecar>();
        var absStatements = new List<AbsStatement>();
        var absSidecars = new List<AbsStatementSidecar>();
        var limitStatements = new List<LimitStatement>();
        var limitSidecars = new List<LimitStatementSidecar>();
        var tSubStatements = new List<TSubStatement>();
        var tSubSidecars = new List<TSubStatementSidecar>();
        var tConvStatements = new List<TConvStatement>();
        var tConvSidecars = new List<TConvStatementSidecar>();
        var calcStatements = new List<CalcStatement>();
        var calcSidecars = new List<CalcStatementSidecar>();
        var moveBlkVariantStatements = new List<MoveBlkVariantStatement>();
        var moveBlkVariantSidecars = new List<MoveBlkVariantStatementSidecar>();
        var waitStatements = new List<WaitStatement>();
        var waitSidecars = new List<WaitStatementSidecar>();
        var fillBlockIStatements = new List<FillBlockIStatement>();
        var fillBlockISidecars = new List<FillBlockIStatementSidecar>();
        var modbusMasterStatements = new List<ModbusMasterStatement>();
        var modbusMasterSidecars = new List<ModbusMasterStatementSidecar>();
        var modbusCommLoadStatements = new List<ModbusCommLoadStatement>();
        var modbusCommLoadSidecars = new List<ModbusCommLoadStatementSidecar>();
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

        foreach (var swap in swapParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceSwap(network, swap, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            swapStatements.Add(statement);
            swapSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        // Abs/LIMIT are reduced last, same reasoning as every other production above (own
        // en/inputs never depend on another production's own reduction completing first).
        foreach (var abs in absParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceAbs(network, abs, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            absStatements.Add(statement);
            absSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        foreach (var limit in limitParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceLimit(network, limit, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            limitStatements.Add(statement);
            limitSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        // T_SUB/T_CONV are reduced last, same reasoning as every other production above. T_SUB
        // must reduce before T_CONV for no correctness reason (ResolveEnSource inspects the raw
        // network directly, not already-reduced Model output, same as Mul-before-Convert) — kept
        // in this order purely to match IrSerializer's own emission order.
        foreach (var tSub in tSubParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceTSub(network, tSub, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            tSubStatements.Add(statement);
            tSubSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        foreach (var tConv in tConvParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceTConv(network, tConv, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            tConvStatements.Add(statement);
            tConvSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        // Calc is reduced last, same reasoning as every other production above.
        foreach (var calc in calcParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceCalc(network, calc, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            calcStatements.Add(statement);
            calcSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        // MOVE_BLK_VARIANT is reduced last, same reasoning as every other production above.
        foreach (var moveBlkVariant in moveBlkVariantParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceMoveBlkVariant(network, moveBlkVariant, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            moveBlkVariantStatements.Add(statement);
            moveBlkVariantSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        // WAIT/FillBlockI are reduced last, same reasoning as every other production above.
        foreach (var wait in waitParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceWait(network, wait, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            waitStatements.Add(statement);
            waitSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        foreach (var fillBlockI in fillBlockIParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceFillBlockI(network, fillBlockI, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            fillBlockIStatements.Add(statement);
            fillBlockISidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        // Modbus_Master/Modbus_Comm_Load are reduced last, same reasoning as every other
        // production above.
        foreach (var modbusMaster in modbusMasterParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceModbusMaster(network, modbusMaster, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            modbusMasterStatements.Add(statement);
            modbusMasterSidecars.Add(sidecar);
            foreach (var entry in accessEntries)
            {
                AddAccessEntry(allAccessEntries, entry);
            }

            foreach (var entry in constantEntries)
            {
                AddConstantEntry(allConstantEntries, entry);
            }
        }

        foreach (var modbusCommLoad in modbusCommLoadParts)
        {
            var (statement, sidecar, accessEntries, constantEntries) =
                ReduceModbusCommLoad(network, modbusCommLoad, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds);
            modbusCommLoadStatements.Add(statement);
            modbusCommLoadSidecars.Add(sidecar);
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
            networkNumber, title, assignments, timerBindings, moveStatements, wordAndStatements, callStatements, null, mulStatements, convertStatements,
            swapStatements, absStatements, limitStatements, tSubStatements, tConvStatements, calcStatements, moveBlkVariantStatements, waitStatements,
            fillBlockIStatements, modbusMasterStatements, modbusCommLoadStatements);
        var networkSidecar = new NetworkSidecar(
            networkNumber, compileUnitUId, allAccessEntries, assignmentSidecars, allConstantEntries, timerSidecars, moveSidecars, wordAndSidecars,
            callSidecars, mulSidecars, convertSidecars, swapSidecars, absSidecars, limitSidecars, tSubSidecars, tConvSidecars, calcSidecars,
            moveBlkVariantSidecars, waitSidecars, fillBlockISidecars, modbusMasterSidecars, modbusCommLoadSidecars);
        // ADR-0006 phase 2: derive per-node fan-out markers ({split N}/{recv N}) from shared part UIds and
        // attach them to the readable Expr trees. Fan-out is recorded per node here — the old per-network
        // SPLIT flag + synthesis heuristic are gone (ADR-0006 phase 3).
        irNetwork = ApplyFanoutMarkers(irNetwork, networkSidecar);
        return new ReducedNetwork(irNetwork, networkSidecar);
    }

    // ADR-0006 phase 2: derive per-node fan-out markers from the reduced network's shared part UIds and
    // attach them to the readable Expr trees. The sidecar's ChainStep tree is isomorphic to the Expr tree
    // (TraceChain builds both in lockstep), so every Expr element has a known part UId; a UId at >= 2 chain
    // positions network-wide is a physical fan-out. Marks the boundary of each shared leading run per the
    // "boundary-marking algorithm" (docs/adr/adr-0006): the deepest received node gets {recv} (absorbing the
    // shallower prefix), each newly-mastered node gets {split}. Labels are network-scoped ordinals assigned
    // at first master, walking the boolean chains in serialization order so a {split} always precedes its {recv}.
    private static IrNetwork ApplyFanoutMarkers(IrNetwork net, NetworkSidecar sidecar)
    {
        var timers = net.Timers ?? Array.Empty<TimerBinding>();
        var moves = net.Moves ?? Array.Empty<MoveStatement>();
        var wordAnds = net.WordAnds ?? Array.Empty<WordAndStatement>();
        var calls = net.Calls ?? Array.Empty<CallStatement>();

        // Boolean chains in serialization order (must match IrSerializer: timers, coils, moves, wands, calls),
        // so a shared part is first seen — and mastered ({split}) — at the position it is serialized first.
        var chains = new List<(Expr Expr, IReadOnlyList<ChainStepSidecar> Steps)>();
        for (var i = 0; i < timers.Count; i++) chains.Add((timers[i].In, sidecar.Timers[i].Steps));
        for (var i = 0; i < net.Assignments.Count; i++) chains.Add((net.Assignments[i].Condition, sidecar.Assignments[i].Steps));
        for (var i = 0; i < moves.Count; i++) chains.Add((moves[i].En, sidecar.Moves[i].Steps));
        for (var i = 0; i < wordAnds.Count; i++) chains.Add((wordAnds[i].En, sidecar.WordAnds[i].Steps));
        for (var i = 0; i < calls.Count; i++) chains.Add((calls[i].En, sidecar.Calls[i].Steps));

        var counts = new Dictionary<int, int>();
        foreach (var (_, steps) in chains)
        {
            CountPartUIds(steps, counts);
        }

        if (!counts.Values.Any(c => c >= 2))
        {
            return net; // no fan-out anywhere — nothing to mark
        }

        var ctx = new FanoutMarkContext(counts);
        var marked = chains.Select(c => MarkChain(c.Expr, c.Steps, ctx)).ToList();

        // Reduce always passes non-null (possibly empty) lists, so rebuild each in place. A statement whose
        // chain had no fan-out gets an identical Expr back (no marker), so this is a no-op for those.
        var idx = 0;
        return net with
        {
            Timers = timers.Select(t => t with { In = marked[idx++] }).ToList(),
            Assignments = net.Assignments.Select(a => a with { Condition = marked[idx++] }).ToList(),
            Moves = moves.Select(m => m with { En = marked[idx++] }).ToList(),
            WordAnds = wordAnds.Select(w => w with { En = marked[idx++] }).ToList(),
            Calls = calls.Select(c => c with { En = marked[idx++] }).ToList(),
        };
    }

    private sealed class FanoutMarkContext
    {
        public FanoutMarkContext(IReadOnlyDictionary<int, int> counts) => Counts = counts;

        public IReadOnlyDictionary<int, int> Counts { get; }

        public Dictionary<int, int> LabelOf { get; } = new();

        public int NextLabel { get; set; } = 1;

        public bool Shared(int uid) => Counts.TryGetValue(uid, out var c) && c >= 2;
    }

    private static int? StepPartUId(ChainStepSidecar step) => step switch
    {
        ChainStepSidecar.ContactStep c => c.ContactUId,
        ChainStepSidecar.CompareStep cmp => cmp.ComparePartUId,
        ChainStepSidecar.OrStep or => or.OrPartUId,
        ChainStepSidecar.NotStep not => not.NotPartUId,
        ChainStepSidecar.TimerOutputStep t => t.TonPartUId,
        _ => null,
    };

    // Occurrence count of each part UId across a chain, recursing into OR-branches and NOT inner chains — so
    // a contact shared between a top-level position and one nested in an OR/NOT is counted at both (the
    // cross-depth case, N13). >= 2 occurrences ⇒ fan-out.
    private static void CountPartUIds(IReadOnlyList<ChainStepSidecar> steps, Dictionary<int, int> counts)
    {
        foreach (var step in steps)
        {
            if (StepPartUId(step) is int u)
            {
                counts[u] = counts.GetValueOrDefault(u) + 1;
            }

            switch (step)
            {
                case ChainStepSidecar.OrStep or:
                    foreach (var branch in or.Branches)
                    {
                        CountPartUIds(branch.Steps, counts);
                    }

                    break;
                case ChainStepSidecar.NotStep not:
                    CountPartUIds(not.Steps, counts);
                    break;
            }
        }
    }

    // Mark one chain whose Expr operands align 1:1 with `steps` (rail-to-coil order). Applies the boundary
    // rule to the leading run of shared elements, then recurses into compound elements (OR-branches, a
    // standalone NOT's inner chain) as their own chains.
    private static Expr MarkChain(Expr expr, IReadOnlyList<ChainStepSidecar> steps, FanoutMarkContext ctx)
    {
        var isAnd = expr is Expr.And;
        var operands = expr is Expr.And and ? and.Operands.ToList() : new List<Expr> { expr };
        if (operands.Count != steps.Count || operands.Count == 0)
        {
            return expr; // isomorphism broken (never guess) or rail-fed TRUE — nothing to mark
        }

        var uids = steps.Select(StepPartUId).ToList();

        // Leading run of shared elements (a part has one input, so shared elements are always a prefix).
        var k = 0;
        while (k < uids.Count && uids[k] is int u && ctx.Shared(u))
        {
            k++;
        }

        // m = first index in the leading run this chain masters (UId not yet labelled); else k.
        var m = 0;
        while (m < k && ctx.LabelOf.ContainsKey(uids[m]!.Value))
        {
            m++;
        }

        var markers = new FanoutMarker?[operands.Count];
        if (m > 0)
        {
            // The deepest received node — absorbs the shallower prefix (indices 0..m-2 stay unmarked).
            markers[m - 1] = new FanoutMarker(FanoutMarkerKind.Recv, ctx.LabelOf[uids[m - 1]!.Value]);
        }

        for (var i = m; i < k; i++)
        {
            var label = ctx.NextLabel++;
            ctx.LabelOf[uids[i]!.Value] = label;
            markers[i] = new FanoutMarker(FanoutMarkerKind.Split, label);
        }

        var newOperands = new List<Expr>(operands.Count);
        for (var i = 0; i < operands.Count; i++)
        {
            // Always recurse into compound elements, even a marked one: a shared compound's branches may hold
            // a node shared *outside* it (a cross-statement nested OR-merge cascade — FB_MotorFwdRevSystem's
            // telemetry, where an inner `(A OR B)` is shared between a top-level MOVE and the branches of a
            // later MOVE's outer OR), which must still be marked so synthesis reuses it rather than rebuilding.
            // When the branch-internal node is only reused *via* the enclosing compound (MotorStarter N4), the
            // extra `{recv}` inside is harmless — synthesis splices the whole compound and never re-processes
            // its interior.
            var op = RecurseCompound(operands[i], steps[i], ctx);
            if (markers[i] is { } marker)
            {
                op = op with { Fanout = marker };
            }

            newOperands.Add(op);
        }

        return isAnd ? new Expr.And(newOperands) : newOperands[0];
    }

    private static Expr RecurseCompound(Expr op, ChainStepSidecar step, FanoutMarkContext ctx)
    {
        switch (step)
        {
            case ChainStepSidecar.OrStep orStep when op is Expr.Or orExpr && orExpr.Operands.Count == orStep.Branches.Count:
                var branches = new List<Expr>(orExpr.Operands.Count);
                for (var j = 0; j < orExpr.Operands.Count; j++)
                {
                    branches.Add(MarkChain(orExpr.Operands[j], orStep.Branches[j].Steps, ctx));
                }

                return orExpr with { Operands = branches };
            case ChainStepSidecar.NotStep notStep when op is Expr.Not notExpr:
                return notExpr with { Operand = MarkChain(notExpr.Operand, notStep.Steps, ctx) };
            default:
                return op; // leaf (contact / negated contact / comparison / timer-Q) — no sub-chain
        }
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

    // A TON/TONR's IN is reduced exactly like a Coil's condition — same backward trace,
    // terminating at the TON/TONR's own "IN" port instead of a Coil's "in" (and, like a Coil's
    // chain, may itself terminate at another TON's Q instead of the rail — see TraceChain). PT is
    // fed by an IdentCon directly (no chain — it's a single operand, tag or literal), unlike
    // Coil's own "operand" pattern only in that there's no intervening Contact-chain concept for
    // it. `Q` itself is deliberately *not* validated here — whether/how it's consumed (an
    // ordinary Access elsewhere, confirmed real FC ControlDelays; or a direct wire into another
    // chain, confirmed real FC TimerSample) is entirely the consuming chain's concern via
    // TraceChain; this reduction only owns IN/PT/ET/R. TONR's own `R` (reset) — confirmed real,
    // 2026-07-12, S1 item 19 — is resolved the exact same way PT is (ResolveTagOrLiteralOperand,
    // no chain), only when Kind is Tonr.
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

        var et = ResolveOptionalOpenPort(wiresByPort, ton.Name, ton.UId, "ET", networkNumber, visitedWireUIds);

        var instance = ton.Instance
            ?? throw new NonReducibleNetworkException($"Network {networkNumber}: TON UId={ton.UId} has no Instance reference.");
        var instancePath = string.Join('.', instance.ComponentPath);

        var kind = TimerKindFor(ton.Name, networkNumber, ton.UId);

        Expr? resetExpr = null;
        OperandSidecar? resetSidecar = null;
        if (kind == TimerKind.Tonr)
        {
            (resetExpr, resetSidecar) = ResolveTagOrLiteralOperand(
                wiresByPort, accessByUId, constantsByUId, ton.UId, "R", networkNumber, visitedWireUIds, accessEntries, constantEntries);
        }

        var binding = new TimerBinding(instancePath, inExpr, ptExpr, kind, resetExpr);
        var sidecar = new TimerBindingSidecar(
            ton.UId,
            ton.Version ?? throw new NonReducibleNetworkException($"Network {networkNumber}: TON UId={ton.UId} has no Version."),
            ton.TimeType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: TON UId={ton.UId} has no time_type."),
            instance.UId,
            instance.Scope,
            instance.ComponentPath,
            inRailWireUId,
            inSteps,
            presetSidecar,
            et,
            kind,
            resetSidecar);

        return (binding, sidecar, accessEntries, constantEntries);
    }

    // TON/TONR/TOF (S1 items 8/19/23) map 1:1 to TimerKind — no other Part Name has ever mapped
    // to one of these three kinds, so this is a straight lookup, not a guess (mirrors
    // CoilKindFor's own pattern exactly).
    private static TimerKind TimerKindFor(string partName, int networkNumber, int uid) => partName switch
    {
        "TON" => TimerKind.Ton,
        "TONR" => TimerKind.Tonr,
        "TOF" => TimerKind.Tof,
        _ => throw new NonReducibleNetworkException($"Network {networkNumber}: UId={uid} has unexpected Part Name '{partName}' for a timer binding."),
    };

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
    // Instance is optional — confirmed real, 2026-07-12 (S1 item 24, FB MotorVSDSystem's own call to the
    // stateless FC "Scale"): an FC call carries no Instance at all, unlike every FB call grounded
    // so far (S1 item 14), which always does. Arguments are resolved per the source's own sparse,
    // ordered <Parameter> list (confirmed real: only wired parameters appear at all, in source
    // declaration order) — an Input parameter resolves via ResolveTagOrLiteralOperand (same
    // resolver as everywhere else); an Output parameter is a bare destination tag (ResolveOperand
    // — same IdentCon-fed wire shape as Move's own out1/WAND's dest, just named per-parameter via
    // the source's own Parameter Name instead of a fixed port name).
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

        var instance = call.Instance;
        var instancePath = instance is not null ? string.Join('.', instance.ComponentPath) : null;

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
            instance?.UId,
            instance?.Scope,
            instance?.ComponentPath,
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
            // "Mul"/"Convert" confirmed real 2026-07-12 (S1 items 18/19); "Sub"/"Div" confirmed
            // real 2026-07-14 (`FC Scale`, grounding `FB MotorVSDSystem`'s own dependency closure —
            // Sub->Sub and Sub->Div chains both directly observed). "Add" as a *producer* (as
            // opposed to consumer, already supported via Mul->Add) hasn't been directly grounded
            // yet — not added here, same "don't guess even when the family suggests it" discipline
            // as Le/Gt's own history. "T_SUB"/"T_CONV" confirmed real 2026-07-14 (Phase 2 Tier 2,
            // `FB VibratorCycle`: a T_SUB->T_CONV->Convert chain, T_SUB and T_CONV each producing
            // the next link's `en` via `eno` directly, same mechanism).
            && precedingPart.Name is "Mul" or "Convert" or "Sub" or "Div" or "T_SUB" or "T_CONV")
        {
            visitedWireUIds.Add(wire.UId);
            return (new EnSource.PrecedingEno(), new EnSourceSidecar.PrecedingEnoSidecar(precedingPart.UId, wire.UId));
        }

        var (expr, steps, railWireUId) = TraceChain(
            network, (partUId, "en"), wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);
        return (new EnSource.Condition(expr), new EnSourceSidecar.ConditionSidecar(railWireUId, steps));
    }

    // A Mul/Add's `en` is resolved via ResolveEnSource (ordinary condition or ENO-chained — see
    // its own doc comment; an Add's own `en` fed by a comparison's `out` is just the ordinary
    // Condition case, confirmed real 2026-07-12, S1 item 19, FB MotorDOL/FilterUnitSystem — `Lt`'s own
    // `out` feeds the following `Add`'s `en`). Inputs are Cardinality-driven tag-or-literal
    // operands (`in1`..`inCard`, ResolveTagOrLiteralOperand per port — same resolver as
    // everywhere else, same loop shape as ResolveOrMerge/ReduceWordAnd's own Cardinality loop).
    // `out` writes to a plain tag (ResolveOperand, port "out" — confirmed real, same as WAND's own
    // write port).
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

        // Mul/Add always carry a real Cardinality (enforced at parse time); Sub/Div never do
        // (always binary, `in1`/`in2`) — default to 2 only when genuinely absent, same
        // "absence means default" convention used throughout this project.
        var cardinality = mul.Cardinality ?? 2;
        if (cardinality < 1)
        {
            throw new NonReducibleNetworkException($"Network {networkNumber}: Mul/Add/Sub/Div UId={mul.UId} has no usable cardinality.");
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

        if (mul.AutomaticSrcType == (mul.SrcType is not null))
        {
            throw new NonReducibleNetworkException(
                $"Network {networkNumber}: Mul/Add UId={mul.UId} must have exactly one of AutomaticTyped SrcType or an explicit SrcType TemplateValue.");
        }

        var kind = MulKindFor(mul.Name, networkNumber, mul.UId);
        var statement = new MulStatement(en, inputExprs, destTag.TagPath, kind);
        var sidecar = new MulStatementSidecar(mul.UId, enSidecar, inputSidecars, destTag.UId, destWireUId, kind, mul.SrcType);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // Mul/Add (S1 item 19) map 1:1 to MulKind — no other Part Name has ever mapped to one of
    // these two kinds, so this is a straight lookup, not a guess (mirrors CoilKindFor's own
    // pattern exactly). Sub/Div (2026-07-14, FC Scale) extend the same lookup.
    private static MulKind MulKindFor(string partName, int networkNumber, int uid) => partName switch
    {
        "Mul" => MulKind.Multiply,
        "Add" => MulKind.Add,
        "Sub" => MulKind.Subtract,
        "Div" => MulKind.Divide,
        _ => throw new NonReducibleNetworkException($"Network {networkNumber}: UId={uid} has unexpected Part Name '{partName}' for a Mul/Add/Sub/Div statement."),
    };

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

    // A Swap's `en`/`in`/`out` resolve exactly like Convert's own (ResolveEnSource/
    // ResolveTagOrLiteralOperand/ResolveOperand) — the only difference from ReduceConvert is no
    // DestType to carry, confirmed real, 2026-07-12, FB TomraControlSystem (2 instances, both
    // independently rail-fed via a Contact chain, neither ENO-chained).
    private static (SwapStatement Statement, SwapStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceSwap(
        FlgNetwork network,
        PartNode swap,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (en, enSidecar) = ResolveEnSource(
            network, swap.UId, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (inExpr, inSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, swap.UId, "in", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (destTag, destWireUId) = ResolveOperand(wiresByPort, accessByUId, swap.UId, networkNumber, "out");
        visitedWireUIds.Add(destWireUId);
        AddAccessEntry(accessEntries, destTag);

        var statement = new SwapStatement(en, inExpr, destTag.TagPath);
        var sidecar = new SwapStatementSidecar(
            swap.UId,
            enSidecar,
            inSidecar,
            swap.SrcType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Swap UId={swap.UId} has no SrcType."),
            destTag.UId,
            destWireUId);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // An Abs's `en`/`in`/`out` resolve exactly like Swap's own — identical shape, different
    // source Part Name (Phase 2 Tier 1, 2026-07-14, FB VSDSim).
    private static (AbsStatement Statement, AbsStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceAbs(
        FlgNetwork network,
        PartNode abs,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (en, enSidecar) = ResolveEnSource(
            network, abs.UId, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (inExpr, inSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, abs.UId, "in", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (destTag, destWireUId) = ResolveOperand(wiresByPort, accessByUId, abs.UId, networkNumber, "out");
        visitedWireUIds.Add(destWireUId);
        AddAccessEntry(accessEntries, destTag);

        var statement = new AbsStatement(en, inExpr, destTag.TagPath);
        var sidecar = new AbsStatementSidecar(
            abs.UId,
            enSidecar,
            inSidecar,
            abs.SrcType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Abs UId={abs.UId} has no SrcType."),
            destTag.UId,
            destWireUId);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // A LIMIT's `en` resolves via ResolveEnSource (a plain condition in both real instances,
    // never ENO-chained). Its three inputs (`MN`/`IN`/`MX`) each resolve exactly like a TON's own
    // `PT` (ResolveTagOrLiteralOperand, one call per named port — fixed-named, not
    // Cardinality-driven, so no loop the way Mul/WAND's own variable input lists need). `out`
    // writes to a plain tag, same as Convert/Swap/Abs's own.
    private static (LimitStatement Statement, LimitStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceLimit(
        FlgNetwork network,
        PartNode limit,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (en, enSidecar) = ResolveEnSource(
            network, limit.UId, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (minExpr, minSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, limit.UId, "MN", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (inExpr, inSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, limit.UId, "IN", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (maxExpr, maxSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, limit.UId, "MX", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (destTag, destWireUId) = ResolveOperand(wiresByPort, accessByUId, limit.UId, networkNumber, "OUT");
        visitedWireUIds.Add(destWireUId);
        AddAccessEntry(accessEntries, destTag);

        var statement = new LimitStatement(en, minExpr, inExpr, maxExpr, destTag.TagPath);
        var sidecar = new LimitStatementSidecar(
            limit.UId,
            limit.Version ?? throw new NonReducibleNetworkException($"Network {networkNumber}: LIMIT UId={limit.UId} has no Version."),
            enSidecar,
            minSidecar,
            inSidecar,
            maxSidecar,
            limit.SrcType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: LIMIT UId={limit.UId} has no value_type."),
            destTag.UId,
            destWireUId);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // A T_SUB's `en` resolves via ResolveEnSource (directly rail-fed in the one real instance,
    // never Contact-gated — but ResolveEnSource handles either case the same way it does for
    // every other production). Its two inputs (`IN1`/`IN2`, uppercase) each resolve like Sub's own
    // `in1`/`in2` (ResolveTagOrLiteralOperand). `OUT` (uppercase) writes to a plain tag.
    private static (TSubStatement Statement, TSubStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceTSub(
        FlgNetwork network,
        PartNode tSub,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (en, enSidecar) = ResolveEnSource(
            network, tSub.UId, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (in1Expr, in1Sidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, tSub.UId, "IN1", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (in2Expr, in2Sidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, tSub.UId, "IN2", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (destTag, destWireUId) = ResolveOperand(wiresByPort, accessByUId, tSub.UId, networkNumber, "OUT");
        visitedWireUIds.Add(destWireUId);
        AddAccessEntry(accessEntries, destTag);

        var statement = new TSubStatement(en, in1Expr, in2Expr, destTag.TagPath);
        var sidecar = new TSubStatementSidecar(
            tSub.UId,
            tSub.Version ?? throw new NonReducibleNetworkException($"Network {networkNumber}: T_SUB UId={tSub.UId} has no Version."),
            enSidecar,
            in1Sidecar,
            in2Sidecar,
            tSub.SrcType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: T_SUB UId={tSub.UId} has no date_type."),
            tSub.TimeType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: T_SUB UId={tSub.UId} has no time_type."),
            destTag.UId,
            destWireUId);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // A T_CONV's `en`/`in`/`out` resolve exactly like Convert's own, using the uppercase `IN`/
    // `OUT` port names confirmed real for T_CONV (unlike ordinary Convert's lowercase).
    private static (TConvStatement Statement, TConvStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceTConv(
        FlgNetwork network,
        PartNode tConv,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (en, enSidecar) = ResolveEnSource(
            network, tConv.UId, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (inExpr, inSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, tConv.UId, "IN", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (destTag, destWireUId) = ResolveOperand(wiresByPort, accessByUId, tConv.UId, networkNumber, "OUT");
        visitedWireUIds.Add(destWireUId);
        AddAccessEntry(accessEntries, destTag);

        var statement = new TConvStatement(en, inExpr, destTag.TagPath);
        var sidecar = new TConvStatementSidecar(
            tConv.UId,
            tConv.Version ?? throw new NonReducibleNetworkException($"Network {networkNumber}: T_CONV UId={tConv.UId} has no Version."),
            enSidecar,
            inSidecar,
            tConv.SrcType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: T_CONV UId={tConv.UId} has no src_type."),
            tConv.DestType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: T_CONV UId={tConv.UId} has no dest_type."),
            destTag.UId,
            destWireUId);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // A Calc's `en` resolves via ResolveEnSource (directly rail-fed in both real instances).
    // Inputs are Cardinality-driven tag-or-literal operands (`in1`..`inCard`), identical loop
    // shape to ReduceMul's own. `out` writes to a plain tag. Equation is carried verbatim from
    // the PartNode (never parsed/interpreted — see PartNode.Equation's own doc comment).
    private static (CalcStatement Statement, CalcStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceCalc(
        FlgNetwork network,
        PartNode calc,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (en, enSidecar) = ResolveEnSource(
            network, calc.UId, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var cardinality = calc.Cardinality
            ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Calc UId={calc.UId} has no usable cardinality.");
        if (cardinality < 1)
        {
            throw new NonReducibleNetworkException($"Network {networkNumber}: Calc UId={calc.UId} has no usable cardinality.");
        }

        var inputExprs = new List<Expr>();
        var inputSidecars = new List<OperandSidecar>();
        for (var k = 1; k <= cardinality; k++)
        {
            var (inputExpr, inputSidecar) = ResolveTagOrLiteralOperand(
                wiresByPort, accessByUId, constantsByUId, calc.UId, $"in{k}", networkNumber, visitedWireUIds, accessEntries, constantEntries);
            inputExprs.Add(inputExpr);
            inputSidecars.Add(inputSidecar);
        }

        var (destTag, destWireUId) = ResolveOperand(wiresByPort, accessByUId, calc.UId, networkNumber, "out");
        visitedWireUIds.Add(destWireUId);
        AddAccessEntry(accessEntries, destTag);

        var equation = calc.Equation ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Calc UId={calc.UId} has no Equation.");
        var statement = new CalcStatement(en, inputExprs, equation, destTag.TagPath);
        var sidecar = new CalcStatementSidecar(
            calc.UId,
            enSidecar,
            inputSidecars,
            equation,
            calc.SrcType ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Calc UId={calc.UId} has no SrcType."),
            destTag.UId,
            destWireUId);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // A MOVE_BLK_VARIANT's `en` resolves via ResolveEnSource (confirmed real either rail-fed or
    // an ordinary TraceChain condition — never ENO-chained in any real instance). Its four inputs
    // (`SRC`/`COUNT`/`SRC_INDEX`/`DEST_INDEX`) each resolve like a TON's own `PT`
    // (ResolveTagOrLiteralOperand, one call per named port). Its two outputs (`Ret_Val`/`DEST`,
    // mixed-case exactly as the real source has them) each write to a plain tag (ResolveOperand,
    // same shape as Move's own `out1`) — the first production this converter reduces with two
    // separate destination writes instead of one.
    private static (MoveBlkVariantStatement Statement, MoveBlkVariantStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceMoveBlkVariant(
        FlgNetwork network,
        PartNode moveBlkVariant,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (en, enSidecar) = ResolveEnSource(
            network, moveBlkVariant.UId, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (srcExpr, srcSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, moveBlkVariant.UId, "SRC", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (countExpr, countSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, moveBlkVariant.UId, "COUNT", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (srcIndexExpr, srcIndexSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, moveBlkVariant.UId, "SRC_INDEX", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (destIndexExpr, destIndexSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, moveBlkVariant.UId, "DEST_INDEX", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (retValTag, retValWireUId) = ResolveOperand(wiresByPort, accessByUId, moveBlkVariant.UId, networkNumber, "Ret_Val");
        visitedWireUIds.Add(retValWireUId);
        AddAccessEntry(accessEntries, retValTag);

        var (destTag, destWireUId) = ResolveOperand(wiresByPort, accessByUId, moveBlkVariant.UId, networkNumber, "DEST");
        visitedWireUIds.Add(destWireUId);
        AddAccessEntry(accessEntries, destTag);

        var statement = new MoveBlkVariantStatement(en, srcExpr, countExpr, srcIndexExpr, destIndexExpr, retValTag.TagPath, destTag.TagPath);
        var sidecar = new MoveBlkVariantStatementSidecar(
            moveBlkVariant.UId,
            moveBlkVariant.Version ?? throw new NonReducibleNetworkException($"Network {networkNumber}: MOVE_BLK_VARIANT UId={moveBlkVariant.UId} has no Version."),
            enSidecar,
            srcSidecar,
            countSidecar,
            srcIndexSidecar,
            destIndexSidecar,
            retValTag.UId,
            retValWireUId,
            destTag.UId,
            destWireUId);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // A WAIT's `en` resolves via ResolveEnSource. Its one input (`WT`) resolves like a TON's own
    // `PT` (ResolveTagOrLiteralOperand). No destination at all — see WaitStatement's own doc
    // comment.
    private static (WaitStatement Statement, WaitStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceWait(
        FlgNetwork network,
        PartNode wait,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (en, enSidecar) = ResolveEnSource(
            network, wait.UId, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (wtExpr, wtSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, wait.UId, "WT", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var statement = new WaitStatement(en, wtExpr);
        var sidecar = new WaitStatementSidecar(
            wait.UId,
            wait.Version ?? throw new NonReducibleNetworkException($"Network {networkNumber}: WAIT UId={wait.UId} has no Version."),
            enSidecar,
            wtSidecar);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // A FillBlockI's `en` resolves via ResolveEnSource. Its two inputs (`in`/`count`) each
    // resolve like Move's own `in` (ResolveTagOrLiteralOperand). `out` writes to a plain tag,
    // same shape as Move's own `out1`.
    private static (FillBlockIStatement Statement, FillBlockIStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceFillBlockI(
        FlgNetwork network,
        PartNode fillBlockI,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (en, enSidecar) = ResolveEnSource(
            network, fillBlockI.UId, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (inExpr, inSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, fillBlockI.UId, "in", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (countExpr, countSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, fillBlockI.UId, "count", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (destTag, destWireUId) = ResolveOperand(wiresByPort, accessByUId, fillBlockI.UId, networkNumber, "out");
        visitedWireUIds.Add(destWireUId);
        AddAccessEntry(accessEntries, destTag);

        var statement = new FillBlockIStatement(en, inExpr, countExpr, destTag.TagPath);
        var sidecar = new FillBlockIStatementSidecar(fillBlockI.UId, enSidecar, inSidecar, countSidecar, destTag.UId, destWireUId);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // A Modbus_Master's `en` resolves via ResolveEnSource. `REQ` is genuinely different from
    // every other operand this converter has resolved so far: confirmed real fed by a full
    // Contact chain's own `out` (not a plain IdentCon-fed tag), so it needs `TraceChain` — the
    // same mechanism a Coil's own condition uses — rather than `ResolveTagOrLiteralOperand`.
    // `MB_ADDR`/`MODE`/`DATA_ADDR`/`DATA_LEN`/`DATA_PTR` are ordinary tag-or-literal inputs.
    // `DONE`/`BUSY`/`ERROR`/`STATUS` are four separate plain-tag writes (see
    // ModbusMasterStatement's own doc comment).
    private static (ModbusMasterStatement Statement, ModbusMasterStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceModbusMaster(
        FlgNetwork network,
        PartNode modbusMaster,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (en, enSidecar) = ResolveEnSource(
            network, modbusMaster.UId, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (reqExpr, reqSteps, reqRailWireUId) = TraceChain(
            network, (modbusMaster.UId, "REQ"), wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (mbAddrExpr, mbAddrSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, modbusMaster.UId, "MB_ADDR", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (modeExpr, modeSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, modbusMaster.UId, "MODE", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (dataAddrExpr, dataAddrSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, modbusMaster.UId, "DATA_ADDR", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (dataLenExpr, dataLenSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, modbusMaster.UId, "DATA_LEN", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (dataPtrExpr, dataPtrSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, modbusMaster.UId, "DATA_PTR", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (doneTag, doneWireUId) = ResolveOperand(wiresByPort, accessByUId, modbusMaster.UId, networkNumber, "DONE");
        visitedWireUIds.Add(doneWireUId);
        AddAccessEntry(accessEntries, doneTag);

        var (busyTag, busyWireUId) = ResolveOperand(wiresByPort, accessByUId, modbusMaster.UId, networkNumber, "BUSY");
        visitedWireUIds.Add(busyWireUId);
        AddAccessEntry(accessEntries, busyTag);

        var (errorTag, errorWireUId) = ResolveOperand(wiresByPort, accessByUId, modbusMaster.UId, networkNumber, "ERROR");
        visitedWireUIds.Add(errorWireUId);
        AddAccessEntry(accessEntries, errorTag);

        var (statusTag, statusWireUId) = ResolveOperand(wiresByPort, accessByUId, modbusMaster.UId, networkNumber, "STATUS");
        visitedWireUIds.Add(statusWireUId);
        AddAccessEntry(accessEntries, statusTag);

        var instance = modbusMaster.Instance
            ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Modbus_Master UId={modbusMaster.UId} has no Instance reference.");
        var instancePath = string.Join('.', instance.ComponentPath);

        var statement = new ModbusMasterStatement(
            en, instancePath, reqExpr, mbAddrExpr, modeExpr, dataAddrExpr, dataLenExpr, dataPtrExpr,
            doneTag.TagPath, busyTag.TagPath, errorTag.TagPath, statusTag.TagPath);
        var sidecar = new ModbusMasterStatementSidecar(
            modbusMaster.UId,
            modbusMaster.Version ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Modbus_Master UId={modbusMaster.UId} has no Version."),
            enSidecar,
            instance.UId,
            instance.Scope,
            instance.ComponentPath,
            reqRailWireUId,
            reqSteps,
            mbAddrSidecar,
            modeSidecar,
            dataAddrSidecar,
            dataLenSidecar,
            dataPtrSidecar,
            doneTag.UId,
            doneWireUId,
            busyTag.UId,
            busyWireUId,
            errorTag.UId,
            errorWireUId,
            statusTag.UId,
            statusWireUId);

        return (statement, sidecar, accessEntries, constantEntries);
    }

    // A Modbus_Comm_Load's `en` resolves via ResolveEnSource. `REQ`/`PORT`/`BAUD`/`PARITY`/
    // `RESP_TO`/`MB_DB` are ordinary tag-or-literal inputs (unlike Modbus_Master's own `REQ`, this
    // one is a plain IdentCon-fed tag in every real instance seen). `FLOW_CTRL`/`RTS_ON_DLY`/
    // `RTS_OFF_DLY` are real ports always left wired to OpenCon (`ResolveOptionalOpenPort`, same
    // mechanism as TON's own `ET`). `DONE`/`ERROR`/`STATUS` are three separate plain-tag writes.
    private static (ModbusCommLoadStatement Statement, ModbusCommLoadStatementSidecar Sidecar, List<SidecarAccessEntry> AccessEntries, List<SidecarConstantEntry> ConstantEntries) ReduceModbusCommLoad(
        FlgNetwork network,
        PartNode modbusCommLoad,
        Dictionary<(int, string), WireNode> wiresByPort,
        Dictionary<int, AccessNode> accessByUId,
        Dictionary<int, ConstantAccessNode> constantsByUId,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();

        var (en, enSidecar) = ResolveEnSource(
            network, modbusCommLoad.UId, wiresByPort, accessByUId, constantsByUId, networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (reqExpr, reqSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, modbusCommLoad.UId, "REQ", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (portExpr, portSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, modbusCommLoad.UId, "PORT", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (baudExpr, baudSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, modbusCommLoad.UId, "BAUD", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (parityExpr, paritySidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, modbusCommLoad.UId, "PARITY", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var flowCtrl = ResolveOptionalOpenPort(wiresByPort, "Modbus_Comm_Load", modbusCommLoad.UId, "FLOW_CTRL", networkNumber, visitedWireUIds);
        var rtsOnDly = ResolveOptionalOpenPort(wiresByPort, "Modbus_Comm_Load", modbusCommLoad.UId, "RTS_ON_DLY", networkNumber, visitedWireUIds);
        var rtsOffDly = ResolveOptionalOpenPort(wiresByPort, "Modbus_Comm_Load", modbusCommLoad.UId, "RTS_OFF_DLY", networkNumber, visitedWireUIds);

        var (respToExpr, respToSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, modbusCommLoad.UId, "RESP_TO", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (mbDbExpr, mbDbSidecar) = ResolveTagOrLiteralOperand(
            wiresByPort, accessByUId, constantsByUId, modbusCommLoad.UId, "MB_DB", networkNumber, visitedWireUIds, accessEntries, constantEntries);

        var (doneTag, doneWireUId) = ResolveOperand(wiresByPort, accessByUId, modbusCommLoad.UId, networkNumber, "DONE");
        visitedWireUIds.Add(doneWireUId);
        AddAccessEntry(accessEntries, doneTag);

        var (errorTag, errorWireUId) = ResolveOperand(wiresByPort, accessByUId, modbusCommLoad.UId, networkNumber, "ERROR");
        visitedWireUIds.Add(errorWireUId);
        AddAccessEntry(accessEntries, errorTag);

        var (statusTag, statusWireUId) = ResolveOperand(wiresByPort, accessByUId, modbusCommLoad.UId, networkNumber, "STATUS");
        visitedWireUIds.Add(statusWireUId);
        AddAccessEntry(accessEntries, statusTag);

        var instance = modbusCommLoad.Instance
            ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Modbus_Comm_Load UId={modbusCommLoad.UId} has no Instance reference.");
        var instancePath = string.Join('.', instance.ComponentPath);

        var statement = new ModbusCommLoadStatement(
            en, instancePath, reqExpr, portExpr, baudExpr, parityExpr, respToExpr, mbDbExpr,
            doneTag.TagPath, errorTag.TagPath, statusTag.TagPath);
        var sidecar = new ModbusCommLoadStatementSidecar(
            modbusCommLoad.UId,
            modbusCommLoad.Version ?? throw new NonReducibleNetworkException($"Network {networkNumber}: Modbus_Comm_Load UId={modbusCommLoad.UId} has no Version."),
            enSidecar,
            instance.UId,
            instance.Scope,
            instance.ComponentPath,
            reqSidecar,
            portSidecar,
            baudSidecar,
            paritySidecar,
            flowCtrl,
            rtsOnDly,
            rtsOffDly,
            respToSidecar,
            mbDbSidecar,
            doneTag.UId,
            doneWireUId,
            errorTag.UId,
            errorWireUId,
            statusTag.UId,
            statusWireUId);

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
    // identifying a wire's producer. Contact/O/Eq/Ge/Lt/Ne/Not use "out"; a TON/TONR/TOF's only
    // confirmed real upstream leaf is "Q" ("ET" has no live example as a consumed leaf, refused
    // like any other unrecognized shape). Move/And/Mul/Add/Convert never appear here — none is
    // ever a producer for a boolean chain, only a consumer (their own "en" tap, or — for
    // Mul/Convert — a separate ENO-chain mechanism, see ResolveEnSource) — see TraceChain's
    // producer-identification comment.
    private static string? OutPortFor(string partName) => partName switch
    {
        "Contact" or "O" or "Eq" or "Ge" or "Lt" or "Ne" or "Gt" or "Le" or "Not" => "out",
        "TON" or "TONR" or "TOF" => "Q",
        _ => null,
    };

    // IR-text infix operator per Part Name — Eq/Ge confirmed real 2026-07-11, Lt confirmed real
    // 2026-07-12 (S1 item 19, FB MotorDOL/FilterUnitSystem), Ne confirmed real 2026-07-12 (S1 item 22,
    // FB AirStar — identical shape to Eq/Ge/Lt), Gt/Le both confirmed real 2026-07-14 (FC Scale),
    // completing the full IEC comparison family — SupportedComparisonPartNames gates this at
    // parse time.
    private static string ComparisonOperator(string partName) => partName switch
    {
        "Eq" => "=",
        "Ge" => ">=",
        "Lt" => "<",
        "Ne" => "<>",
        "Gt" => ">",
        "Le" => "<=",
        _ => throw new UnsupportedConstructException($"Unsupported comparison Part Name '{partName}'."),
    };

    // A port that's either entirely absent from <Wires> or wired to OpenCon — never a genuine
    // consumer/producer connection. Confirmed real, 2026-07-11, for TON's own optional ET (`FB
    // MotorDOL`), and again, 2026-07-14 (Phase 2 Tier 4), for Modbus_Comm_Load's own
    // FlowCtrl/RtsOnDly/RtsOffDly (`FC ModbusComs`, always OpenCon in every real instance seen —
    // never entirely absent, unlike ET) — generalized into one shared helper (contextLabel/partUId
    // replacing the original TON-specific naming) once the second real instruction confirmed the
    // shape isn't TON-ET-specific, same "don't stack special cases" reasoning as `Version`'s own
    // generalization. A wire to any other endpoint is refused — no live example of a genuinely
    // *used* optional port exists yet (unlike a TON's Q, which TraceChain handles as a real chain
    // leaf when wired to an actual consumer — see ChainStepSidecar.TimerOutputStep).
    private static OpenConnectionSidecar? ResolveOptionalOpenPort(
        Dictionary<(int, string), WireNode> wiresByPort,
        string contextLabel,
        int partUId,
        string port,
        int networkNumber,
        HashSet<int> visitedWireUIds)
    {
        if (!wiresByPort.TryGetValue((partUId, port), out var wire))
        {
            return null;
        }

        var others = wire.Endpoints
            .Where(e => !(e.Kind == EndpointKind.NameCon && e.UId == partUId && e.PortName == port))
            .ToList();

        if (others.Count != 1 || others[0].Kind != EndpointKind.OpenCon)
        {
            throw new UnsupportedConstructException(
                $"Network {networkNumber}: {contextLabel} UId={partUId}'s '{port}' port is wired to a consumer — only an " +
                $"unconnected (absent or OpenCon) '{port}' is supported this phase; reading its value back only works via " +
                "an ordinary Access elsewhere.");
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

            if (upstreamPart.Name is "TON" or "TONR" or "TOF")
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

            if (upstreamPart.Name is "Eq" or "Ge" or "Lt" or "Ne" or "Gt" or "Le")
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
                // A standalone Not part (invert-RLO), distinct from a negated contact (line ~1686) —
                // Standalone=true so `to-ir` emits `NOT (X)` and synthesis rebuilds a NotStep, not a
                // negated contact (Gap H).
                stepExprs.Insert(0, new Expr.Not(notExpr, Standalone: true));
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
