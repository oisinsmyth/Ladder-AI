using Converter.SimaticMl;

namespace Converter.Ir;

/// <summary>
/// Sidecar synthesis (2026-07-15, extended 2026-07-15): produces a fresh, valid
/// <see cref="NetworkSidecar"/> directly from an <see cref="IrNetwork"/>'s own logical model — no
/// real donor export to read wire/part/access UIds from.
///
/// Scope, v2: plain COIL/SCOIL/RCOIL AND/OR/NOT/Compare chains (<see cref="Expr.TagRef"/>/
/// <see cref="Expr.And"/>/<see cref="Expr.Or"/>/<see cref="Expr.Not"/>/<see cref="Expr.Compare"/>/
/// <see cref="CoilAssignment"/>), TON (<see cref="TimerBinding"/>, TON only — TOF/TONR hard-error,
/// matching site convention C-406 as well as being genuinely unimplemented here), MOVE
/// (<see cref="MoveStatement"/>), MUL/ADD (<see cref="MulStatement"/>, Multiply/Add only —
/// Subtract/Divide hard-error, unimplemented), CONVERT (<see cref="ConvertStatement"/>, scoped to
/// the real Real-seconds-to-DInt-milliseconds HMI idiom — see <see cref="BuildConvertSidecar"/>),
/// and zero-argument FB CALLs (<see cref="CallStatement"/> — a wired-argument call hard-errors,
/// unimplemented; every equipment FB this project has built or reused so far exposes its interface
/// through a caller-visible STATIC struct instead of Input/Output parameters, per C-115/C-118, so a
/// zero-argument CALL covers every real need seen). Still out of scope, still a deliberate, named
/// future follow-on: WAND, SWAP, ABS, LIMIT, T_SUB, T_CONV, CALC, MOVE_BLK_VARIANT, WAIT,
/// FILLBLOCKI, MODBUS_MASTER/MODBUS_COMM_LOAD — this hard-errors on all of them rather than
/// guessing.
///
/// Scope, current (2026-07-20 — supersedes the v2 snapshot above): the subset has since grown as the
/// parity harness closed gaps (docs/notes/converter-synthesis-gaps.md). Now also covers TONR/TOF (Gap C),
/// SUB/DIV, ABS/SWAP/WAND/CALC/T_SUB/T_CONV/MOVE_BLK_VARIANT, registry-typed CONVERT and comparisons
/// (incl. Real tag-vs-tag, via <see cref="TagTypeRegistry"/> — Gaps B/E), and wired-argument CALLs (types
/// from the callee interface). Genuinely still hard-errored: LIMIT, WAIT, FILLBLOCKI, MODBUS_*, and InOut
/// CALL params. Parts are serialized in wire-graph flow order, not ascending UId (Gap I, FlgNetWriter).
///
/// UId values are minted from a single, per-<see cref="Synthesize"/>-call monotonic counter —
/// never threaded across networks (UId numbering is scoped per network, confirmed real,
/// docs/notes/stage-gates.md's "UId numbering restarts per network" finding). This is safe
/// because TIA reassigns Wire/Access/Part UId on every Import()/Compile()/Export() cycle
/// regardless of what's written — confirmed independently three times (Wire, Access, Part) in
/// docs/notes/stage-gates.md: "TIA relocates/renumbers freely, only the topology matters." The
/// one invariant that *does* matter and that minting order must get right on its own (since
/// nothing here is copying it off a real document): FlgNetBuilder.cs sorts the final Parts list
/// by ascending UId and trusts that order to already be TIA's own required signal-flow order (a
/// quoted real Import() validator rule — "the elements must be sorted according to the current
/// flow"). Every Build* method mints a compound/dependent Part's own UId only *after* recursing
/// into whatever feeds it (branches, a NOT's nested chain, an EN/IN chain, a preceding Mul in an
/// ENO chain) — so ascending UId already matches signal-flow order by construction. Access/
/// Constant UIds are a separate concern: FlgNetwork carries them in their own list, entirely
/// outside the Parts-list flow-order constraint (confirmed by construction — FlgNetBuilder never
/// sorts AccessUIds/ConstantUIds, and the original v1 synthesizer's own interleaved
/// contact-then-access minting already passed live TIA verification), so they're minted in
/// whatever order is locally convenient, never re-derived or re-sorted.
/// </summary>
public static class SidecarSynthesizer
{
    private const string GlobalVariableScope = "GlobalVariable";
    private const string LocalVariableScope = "LocalVariable";

    private static readonly IReadOnlySet<string> NoLocalNames = new HashSet<string>();

    // SynthesizeBlock computes the block's own local-name set once (its own declared Static/Temp/
    // Input/Output/InOut member names — Constant excluded, a compile-time-substituted mechanism,
    // not a scoped variable) and passes it to every network — see Synthesize's own doc comment for
    // why this matters. ConstantMembers not included: never referenced this way in this codebase.
    public static IReadOnlyList<NetworkSidecar> SynthesizeBlock(
        IrBlock block, CalleeInterfaceRegistry? callees = null, TagTypeRegistry? tagTypes = null)
    {
        var localNames = ComputeLocalNames(block);
        // The block's own interface members type its local operands (a bare TEMP `SignedValue`, a
        // STATIC struct field) — layer them onto the project-wide registry so typed boxes resolve.
        var effectiveTypes = (tagTypes ?? TagTypeRegistry.Empty).WithLocalMembers(InterfaceMembers(block));
        return block.Networks.Select(network => Synthesize(network, localNames, callees, effectiveTypes)).ToList();
    }

    private static IEnumerable<DbMember> InterfaceMembers(IrBlock block) =>
        (block.StaticMembers ?? Array.Empty<DbMember>())
            .Concat(block.TempMembers)
            .Concat(block.InputMembers ?? Array.Empty<DbMember>())
            .Concat(block.OutputMembers ?? Array.Empty<DbMember>())
            .Concat(block.InOutMembers);

    private static IReadOnlySet<string> ComputeLocalNames(IrBlock block)
    {
        var names = new HashSet<string>();
        foreach (var member in (block.StaticMembers ?? Array.Empty<DbMember>())
            .Concat(block.TempMembers)
            .Concat(block.InputMembers ?? Array.Empty<DbMember>())
            .Concat(block.OutputMembers ?? Array.Empty<DbMember>())
            .Concat(block.InOutMembers))
        {
            names.Add(member.Name);
        }

        return names;
    }

    // A tag reference is LocalVariable-scoped when its own first dotted-path component names a
    // member the ENCLOSING block itself declares (Static/Temp/Input/Output/InOut) — GlobalVariable
    // otherwise (a different DB's own member, a physical/PLC tag, ...). Confirmed real and
    // exhaustive, MotorStarter.ir's own sidecar (grounded 2026-07-15 specifically to get this
    // right, not guessed): *every* access in that whole real fixture is LocalVariable — its own
    // "IO" struct, every plain Static bool (PreStartMemory, RisingEdgeFlags[n], ...), its TEMP
    // "Time" scratch var, computed *MS members, and even multi-instance timers' own .Q reads
    // (ShutdownTimer.Q, FaultTripTimer.Q, ...) — because that whole real FB never references
    // anything outside its own declared interface; every "external" fact flows in through its own
    // STATIC IO struct instead (C-115/C-118's own caller-visible-interface-UDT convention), wired
    // by the calling code, not read directly. v1's own hardcoded GlobalVariableScope default is
    // still correct unchanged for v1's own real use case (FC1 Inputs/FC2 Outputs, which reference
    // a *different* block's own DB — Input./Output. are DB1/DB2's own names, not FC1/FC2's own
    // declared members) — this is a refinement, not a reversal: a block with no Static/Temp/etc.
    // members of its own (NoLocalNames, the 1-arg Synthesize overload every pre-existing test and
    // v1 caller uses) reduces to the exact old always-GlobalVariable behavior. Found live,
    // 2026-07-15: a naive always-GlobalVariable synthesis of FB_PusherControl produced exactly this
    // shape of failure — 174 "tag not defined" errors covering literally every reference in the
    // block, including its own just-declared Step — because TIA was correctly looking for a
    // project-wide global tag matching each name and finding none (the "#" prefix a human would
    // type in the TIA editor for an internal reference is exactly this LocalVariable scope,
    // encoded in the XML rather than in the text — not a text-level convention this IR needs to
    // show, since the readable form never carries Scope at all, sidecar-only like everything else
    // UId/wiring-related).
    private static string ScopeFor(string tagPath, IReadOnlySet<string> localNames)
    {
        var firstComponent = tagPath.Split('.')[0];

        // Strip a trailing array subscript before the local-name lookup: the member behind
        // `RisingEdgeFlags[3]` is `RisingEdgeFlags` (Gap D, 2026-07-19). Without this an array-indexed
        // local member mis-scopes to GlobalVariable, so TIA rejects it as an undefined global tag — the
        // divergence found in MotorStarter / the test-project001 FBs.
        var bracket = firstComponent.IndexOf('[');
        if (bracket >= 0)
        {
            firstComponent = firstComponent[..bracket];
        }

        return localNames.Contains(firstComponent) ? LocalVariableScope : GlobalVariableScope;
    }

    // Convenience overload for a single network with no enclosing block context — every existing
    // test and v1 caller uses this, unaffected by the v2 local-scope refinement (NoLocalNames
    // reduces ScopeFor to the original always-GlobalVariable behavior exactly).
    public static NetworkSidecar Synthesize(IrNetwork network) => Synthesize(network, NoLocalNames);

    public static NetworkSidecar Synthesize(
        IrNetwork network, IReadOnlySet<string> localNames,
        CalleeInterfaceRegistry? callees = null, TagTypeRegistry? tagTypes = null)
    {
        RequireInScope(network);

        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();
        var nextUid = 1;
        var railWireUId = nextUid++;

        // Timers are built before the assignments/chains that read them: a coil fed directly by a
        // same-network timer's Q wires straight from the TON's Q port (a TimerOutputStep, Gap G2),
        // which needs that TON's part UId already minted. Map each *eligible* timer's instance path
        // to its UId — eligible = a GLOBAL-instance timer. A same-network GLOBAL-instance timer's Q is
        // a direct wire (TimerOutputStep, TimerSample); a LOCAL/FB-instance timer's Q is an ordinary
        // LocalVariable Access even in the same network (FB_ShredderSequencer N11) — so a local timer
        // is left out of the map and its Q read falls through to the normal contact/Access path.
        var timers = new List<TimerBindingSidecar>();
        var timerPartUIdByInstancePath = new Dictionary<string, int>(StringComparer.Ordinal);
        // Same-network LOCAL-instance timers, kept in a separate map used only for latch (Set/Reset)
        // coils. A plain (Assign) coil wires directly from a same-network timer's Q only for a
        // GLOBAL-instance timer (the first map); a Set/Reset coil wires directly for a LOCAL-instance
        // one too. Evidence (docs/notes/converter-synthesis-gaps.md, 2026-07-19): every whole-condition
        // R/SCoil-from-timer-Q in the real corpus is a direct wire (MotorStarter N3, FB_MotorFwdRevSystem
        // N1/N4 — 4/4), while every plain-Coil LOCAL-instance timer-Q is an ordinary Access
        // (FB_ShredderSequencer N11, FB_PusherControl N5, MotorStarter N11/N12). Coil type is the signal.
        var latchTimerPartUIdByInstancePath = new Dictionary<string, int>(StringComparer.Ordinal);

        // Fan-out registry (ADR-0006, phase 3): label N → shared node N's reused prefix step-list. A
        // `{split N}` element registers node N; a later `{recv N}` (in this or any following boolean chain)
        // splices node N's steps by reference, so FlgNetBuilder fans out the shared wire. Built up as the
        // boolean chains are synthesised in serialization order (timers → coils → moves → wands → calls), so
        // every split is registered before its receiver — replacing the old per-network SPLIT prefix
        // heuristic with the explicit markers `to-ir` derived.
        var fanoutRegistry = new Dictionary<int, IReadOnlyList<ChainStepSidecar>>();

        // Resolved tag types (from the project export / block interface) — used to type a literal operand to
        // its destination/operation type, and a tag-vs-tag comparison to its operands' type, rather than by
        // magnitude alone. Available from the timer loop on, since a timer IN can carry a comparison too.
        var types = tagTypes ?? TagTypeRegistry.Empty;

        foreach (var timer in network.Timers)
        {
            var timerSidecar = BuildTimerSidecar(timer, railWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, types);
            timers.Add(timerSidecar);
            if (timerSidecar.InstanceScope == GlobalVariableScope)
            {
                timerPartUIdByInstancePath[timer.InstancePath] = timerSidecar.TonPartUId;
            }
            else
            {
                latchTimerPartUIdByInstancePath[timer.InstancePath] = timerSidecar.TonPartUId;
            }
        }

        var assignments = new List<CoilAssignmentSidecar>();
        foreach (var assignment in network.Assignments)
        {
            assignments.Add(BuildAssignment(
                assignment, railWireUId, timerPartUIdByInstancePath, latchTimerPartUIdByInstancePath,
                ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, types));
        }

        var moves = new List<MoveStatementSidecar>();
        foreach (var move in network.Moves)
        {
            moves.Add(BuildMoveSidecar(move, railWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, types));
        }

        // Mul/Convert are synthesized as index-paired siblings, not two independent batches — see
        // BuildConvertSidecar's own doc comment for why a batched "all Muls, then all Converts"
        // order would misattribute which Mul an "EN := ENO"-chained Convert belongs to once a
        // network has more than one such pair (the real, proven MotorStarter "HMI Times" shape
        // this whole idiom is grounded on has three, interleaved).
        var muls = new List<MulStatementSidecar>();
        var converts = new List<ConvertStatementSidecar>();
        var pairCount = Math.Max(network.Muls.Count, network.Converts.Count);
        for (var i = 0; i < pairCount; i++)
        {
            int? mulPartUIdForEno = null;
            if (i < network.Muls.Count)
            {
                // A Mul/Add/Sub/Div with `EN := ENO` chains from the immediately-preceding box in
                // the same network — Mul→Convert (existing) or Mul→Mul (Sub then Div, SignalConditioning).
                var precedingMulUId = muls.Count > 0 ? muls[^1].MulPartUId : (int?)null;
                var mulSidecar = BuildMulSidecar(network.Muls[i], precedingMulUId, railWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, types);
                muls.Add(mulSidecar);
                mulPartUIdForEno = mulSidecar.MulPartUId;
            }

            if (i < network.Converts.Count)
            {
                converts.Add(BuildConvertSidecar(
                    network.Converts[i], mulPartUIdForEno, railWireUId, ref nextUid, accessEntries, constantEntries,
                    localNames, tagTypes ?? TagTypeRegistry.Empty, fanoutRegistry));
            }
        }

        var abs = new List<AbsStatementSidecar>();
        foreach (var absStatement in network.AbsStatements)
        {
            abs.Add(BuildAbsSidecar(absStatement, railWireUId, ref nextUid, accessEntries, constantEntries, localNames, types, fanoutRegistry));
        }

        var swaps = new List<SwapStatementSidecar>();
        foreach (var swap in network.Swaps)
        {
            swaps.Add(BuildSwapSidecar(swap, railWireUId, ref nextUid, accessEntries, constantEntries, localNames, types, fanoutRegistry));
        }

        var wordAnds = new List<WordAndStatementSidecar>();
        foreach (var wordAnd in network.WordAnds)
        {
            wordAnds.Add(BuildWordAndSidecar(wordAnd, railWireUId, ref nextUid, accessEntries, constantEntries, localNames, types, fanoutRegistry));
        }

        var calcs = new List<CalcStatementSidecar>();
        foreach (var calc in network.Calcs)
        {
            calcs.Add(BuildCalcSidecar(calc, railWireUId, ref nextUid, accessEntries, constantEntries, localNames, types, fanoutRegistry));
        }

        // T_SUB → T_CONV ENO chaining, index-paired exactly like Mul → Convert (N3: a T_CONV whose
        // `EN := ENO` chains from the T_SUB immediately before it).
        var tsubs = new List<TSubStatementSidecar>();
        var tconvs = new List<TConvStatementSidecar>();
        var timePairCount = Math.Max(network.TSubs.Count, network.TConvs.Count);
        for (var i = 0; i < timePairCount; i++)
        {
            int? tsubPartUIdForEno = null;
            if (i < network.TSubs.Count)
            {
                var tsubSidecar = BuildTSubSidecar(network.TSubs[i], railWireUId, ref nextUid, accessEntries, constantEntries, localNames, types, fanoutRegistry);
                tsubs.Add(tsubSidecar);
                tsubPartUIdForEno = tsubSidecar.TSubPartUId;
            }

            if (i < network.TConvs.Count)
            {
                tconvs.Add(BuildTConvSidecar(
                    network.TConvs[i], tsubPartUIdForEno, railWireUId, ref nextUid, accessEntries, constantEntries, localNames, types, fanoutRegistry));
            }
        }

        var moveBlkVariants = new List<MoveBlkVariantStatementSidecar>();
        foreach (var moveBlkVariant in network.MoveBlkVariants)
        {
            moveBlkVariants.Add(BuildMoveBlkVariantSidecar(moveBlkVariant, railWireUId, ref nextUid, accessEntries, constantEntries, localNames));
        }

        var calls = new List<CallStatementSidecar>();
        foreach (var call in network.Calls)
        {
            calls.Add(BuildCallSidecar(call, railWireUId, ref nextUid, accessEntries, constantEntries, localNames, callees ?? CalleeInterfaceRegistry.Empty, fanoutRegistry, types));
        }

        return new NetworkSidecar(
            network.Number,
            network.Number.ToString(),
            accessEntries,
            assignments,
            ConstantUIds: constantEntries,
            Timers: timers,
            Moves: moves,
            Calls: calls,
            Muls: muls,
            Converts: converts,
            Swaps: swaps,
            AbsStatements: abs,
            WordAnds: wordAnds,
            Calcs: calcs,
            TSubs: tsubs,
            TConvs: tconvs,
            MoveBlkVariants: moveBlkVariants);
    }

    // Every non-Assignments/Timers/Moves/Muls/Converts/Calls production list on IrNetwork is out
    // of scope for v2 — named individually in the error so a caller knows exactly what's missing,
    // not just "unsupported". Per-item scope narrowing within an in-scope list (TOF/TONR,
    // Subtract/Divide, wired-argument Call) is checked at the point of use, not here, so the error
    // names the *specific* unsupported shape rather than rejecting the whole network.
    private static void RequireInScope(IrNetwork network)
    {
        var populated = new List<string>();
        if (network.Limits.Count > 0)
        {
            populated.Add("Limits");
        }

        if (network.Waits.Count > 0)
        {
            populated.Add("Waits");
        }

        if (network.FillBlockIs.Count > 0)
        {
            populated.Add("FillBlockIs");
        }

        if (network.ModbusMasters.Count > 0)
        {
            populated.Add("ModbusMasters");
        }

        if (network.ModbusCommLoads.Count > 0)
        {
            populated.Add("ModbusCommLoads");
        }

        // Fixed-shape registry instructions (MB_COMM_LOAD/MB_MASTER) sit with the Modbus family for
        // the same reason: synthesizing one means minting an <Instance> for a system-FB instance DB
        // and an <OpenCon> UId per unconnected port, neither of which has been proven against a
        // live import. Conversion scope is not synthesis scope (src/converter/README.md).
        if (network.FixedShapes.Count > 0)
        {
            populated.Add("FixedShapes");
        }

        if (populated.Count > 0)
        {
            throw new UnsupportedSynthesisConstructException(
                $"Network {network.Number}: sidecar synthesis does not support: {string.Join(", ", populated)}.");
        }
    }

    // One coil assignment. The common case is an ordinary rail-to-coil chain; the exception (Gap G2)
    // is a coil fed *directly* by a same-network timer's Q — that wires straight from the TON's Q port
    // (a TimerOutputStep, no rail, no Access), exactly as TIA exports it. A cross-network `.Q` read has
    // no matching same-network timer here, so it falls through to the ordinary chain (an Access) —
    // which is precisely how the real export renders it (TimerSample N3).
    private static CoilAssignmentSidecar BuildAssignment(
        CoilAssignment assignment, int sharedRailWireUId, IReadOnlyDictionary<string, int> timerPartUIdByInstancePath,
        IReadOnlyDictionary<string, int> latchTimerPartUIdByInstancePath,
        ref int nextUid, List<SidecarAccessEntry> accessEntries, List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry, TagTypeRegistry tagTypes)
    {
        IReadOnlyList<ChainStepSidecar> steps;
        int? chainRail;
        // A whole-condition timer-Q coil feed wires directly from the TON's Q port. A plain (Assign)
        // coil qualifies only for a GLOBAL-instance timer; a Set/Reset (latch) coil qualifies for a
        // same-network LOCAL-instance timer too — see the timer-loop comment above for the corpus
        // evidence that coil type, not scope alone, is the signal.
        var directTimer = assignment.Condition is Expr.TagRef tag
            ? TrySplitTimerOutput(tag.Path, timerPartUIdByInstancePath)
              ?? (assignment.Kind != CoilKind.Assign
                  ? TrySplitTimerOutput(tag.Path, latchTimerPartUIdByInstancePath)
                  : null)
            : null;
        if (directTimer is (int tonPartUId, string port))
        {
            chainRail = null; // fed by the timer's Q, never the rail

            // A fanned-out timer Q (`RCOIL a := Timer.Q{split N}` / `SCOIL b := Timer.Q{recv N}`) must share ONE
            // outgoing wire from the TON's Q across all its coils — the real export fans the Q wire out, not one
            // wire per coil (FB_MotorFwdRevSystem N1). The direct-wire path is separate from BuildChain, so it
            // honours the fan-out registry itself: {split} registers its TimerOutputStep, {recv} reuses it.
            var marker = (assignment.Condition as Expr.TagRef)?.Fanout;
            if (marker is { Kind: FanoutMarkerKind.Recv, Label: var recvLabel } && fanoutRegistry.TryGetValue(recvLabel, out var reused))
            {
                steps = reused;
            }
            else
            {
                var outgoingWireUId = nextUid++;
                steps = new ChainStepSidecar[] { new ChainStepSidecar.TimerOutputStep(tonPartUId, port, outgoingWireUId) };
                if (marker is { Kind: FanoutMarkerKind.Split, Label: var splitLabel })
                {
                    fanoutRegistry[splitLabel] = steps;
                }
            }
        }
        else
        {
            (chainRail, steps) = BuildChain(assignment.Condition, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
        }

        var coilUId = nextUid++;
        var coilOperandAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(assignment.CoilTag, coilOperandAccessUId, ScopeFor(assignment.CoilTag, localNames)));
        var coilOperandWireUId = nextUid++;

        return new CoilAssignmentSidecar(chainRail, steps, coilUId, coilOperandAccessUId, coilOperandWireUId);
    }

    // A `<instancePath>.Q` read of a timer built in this same network → (its TON part UId, "Q"), else
    // null. Only the whole-condition case is handled (a coil reads exactly one timer's Q, TimerSample's
    // shape); a same-network Q *mid-chain* would need a TimerOutputStep inside BuildChain — not seen in
    // the corpus (N3's multi-term reads are all cross-network), left for when a real case appears.
    private static (int TonPartUId, string Port)? TrySplitTimerOutput(
        string path, IReadOnlyDictionary<string, int> timerPartUIdByInstancePath)
    {
        const string suffix = ".Q";
        if (path.EndsWith(suffix, StringComparison.Ordinal)
            && timerPartUIdByInstancePath.TryGetValue(path[..^suffix.Length], out var tonPartUId))
        {
            return (tonPartUId, "Q");
        }

        return null;
    }

    // Rail-to-coil chain for one condition. Mirrors GraphReducer.TraceChain's own confirmed
    // grammar in reverse: Steps ::= (ContactStep)* | (OrStep|NotStep) (ContactStep)* — an Or/Not
    // can only ever occupy Steps[0]; everything after it must be a plain (possibly negated) leaf.
    // Not an artificial restriction — the same physical fact about series ladder chains
    // GraphReducer already discovered (only one branch point possible per series run, and it's
    // inherently rail-most). Expr.And's own Operands are rail-to-coil ordered for the same reason.
    // A bare Expr.Compare is a third leaf-like shape (v2): unlike a TagRef, it resolves its own two
    // operands rather than a single Access, but like a TagRef it may appear at any chain position,
    // not just Steps[0] (confirmed real, FC ControlDelays: a comparison behaves like a Contact, not
    // an OR-merge/TON).
    private static (int? RailWireUId, List<ChainStepSidecar> Steps) BuildChain(
        Expr expr, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry, TagTypeRegistry tagTypes)
    {
        var operands = FlattenAnd(expr);
        var steps = new List<ChainStepSidecar>();

        // Fan-out (ADR-0006): a `{recv N}` operand means the chain from its start up to and including it IS
        // shared node N — splice node N's registered prefix steps (reused BY REFERENCE, so FlgNetBuilder
        // dedups the Parts and fans out the shared wire), and skip building the absorbed prefix operands
        // before it; only the operands after it are built. The boundary rule (docs/adr/adr-0006) guarantees
        // at most one recv per chain, with every split after it — so find the recv, splice, then build on.
        var startBuild = 0;
        for (var i = 0; i < operands.Count; i++)
        {
            if (operands[i].Fanout is { Kind: FanoutMarkerKind.Recv, Label: var recvLabel })
            {
                if (!fanoutRegistry.TryGetValue(recvLabel, out var reused))
                {
                    throw new UnsupportedSynthesisConstructException(
                        $"'{{recv {recvLabel}}}' has no matching '{{split {recvLabel}}}' registered earlier in " +
                        "this network — a split master must be serialised before its receiver (ADR-0006).");
                }

                steps.AddRange(reused);
                startBuild = i + 1;
                break;
            }
        }

        for (var idx = startBuild; idx < operands.Count; idx++)
        {
            var operand = operands[idx];
            steps.Add(BuildStep(operand, idx, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes));

            // Register node N as the prefix up to and including this element, for a later `{recv N}` to
            // reuse (a snapshot copy of the current step references — cascading: node 3 = node 2's steps + this).
            if (operand.Fanout is { Kind: FanoutMarkerKind.Split, Label: var splitLabel })
            {
                fanoutRegistry[splitLabel] = steps.ToList();
            }
        }

        // The rail-facing status follows operands[0] (the reused prefix's first element when a recv spliced
        // it) — a leaf/compare chain is rail-fed, an OR/standalone-Not first element bubbles a null rail.
        var railWired = operands.Count == 0 || !IsCompound(operands[0]);
        return (railWired ? sharedRailWireUId : null, steps);
    }

    private static ChainStepSidecar BuildStep(
        Expr operand, int idx, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry, TagTypeRegistry tagTypes)
    {
        if (idx == 0 && IsCompound(operand))
        {
            return BuildCompoundStep(operand, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
        }

        if (operand is Expr.Compare compare)
        {
            return BuildCompareStep(compare, ref nextUid, accessEntries, constantEntries, localNames, tagTypes);
        }

        if (IsLeaf(operand))
        {
            return BuildLeafContactStep(operand, ref nextUid, accessEntries, localNames);
        }

        if (IsCompound(operand))
        {
            throw new UnsupportedSynthesisConstructException(
                "Only the first operand of an AND chain may be a compound (OR, or NOT-of-non-leaf) " +
                "expression — this matches every real series-ladder-chain shape this converter has " +
                "ever seen (a branch point can only occur at the rail-most position of a chain). " +
                $"Found a compound operand at position {idx}.");
        }

        throw new UnsupportedSynthesisConstructException(
            $"Sidecar synthesis does not support '{operand.GetType().Name}' expression nodes " +
            "yet (only TagRef/And/Or/Not/Compare are supported).");
    }

    // Expr.And can arrive left-nested from parenthesized text (e.g. "(A AND B) AND C") even
    // though it's logically flat — flatten defensively rather than assume the parser always
    // hands back a flat list. Or is deliberately NOT flattened: nesting there is structurally
    // meaningful (a nested Or means a nested "O" Part). An empty And (zero operands) is the
    // reserved "EN := TRUE"/"wired directly to rail" sentinel (confirmed real, WAND's own
    // live-verified case) — FlattenAnd already returns an empty list for it with no special
    // casing needed (SelectMany over zero operands is itself empty), and the caller's own
    // `operands.Count == 0` check is what turns that into a direct-rail wire.
    private static List<Expr> FlattenAnd(Expr expr) => expr switch
    {
        Expr.And and => and.Operands.SelectMany(FlattenAnd).ToList(),
        _ => new List<Expr> { expr },
    };

    // A standalone Not (Standalone=true, incl. every Not of a compound) is a NotStep; a negated
    // contact (Standalone=false, always a bare tag) is a leaf (Gap H).
    private static bool IsCompound(Expr expr) =>
        expr is Expr.Or || expr is Expr.Not { Standalone: true };

    private static bool IsLeaf(Expr expr) =>
        expr is Expr.TagRef || (expr is Expr.Not { Standalone: false, Operand: Expr.TagRef });

    private static ChainStepSidecar BuildCompoundStep(
        Expr expr, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry, TagTypeRegistry tagTypes)
    {
        switch (expr)
        {
            case Expr.Or or:
                // Build every branch (and everything upstream of it) before minting the O Part's
                // own UId — see this class's own doc comment for why minting order must produce
                // ascending-UId-equals-signal-flow-order on its own. Branches carry the fanout registry so a
                // marker inside a branch (an intra-statement split, MotorStarter N1) is honoured.
                var branches = new List<OrBranch>();
                foreach (var operand in or.Operands)
                {
                    var (branchRail, branchSteps) = BuildChain(operand, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
                    branches.Add(new OrBranch(branchSteps, branchRail));
                }

                var orPartUId = nextUid++;
                var orOutgoingWireUId = nextUid++;
                return new ChainStepSidecar.OrStep(orPartUId, branches, orOutgoingWireUId);

            case Expr.Not not:
                var (notRail, notSteps) = BuildChain(not.Operand, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
                var notPartUId = nextUid++;
                var notOutgoingWireUId = nextUid++;
                return new ChainStepSidecar.NotStep(notPartUId, notSteps, notRail, notOutgoingWireUId);

            default:
                throw new UnsupportedSynthesisConstructException(
                    $"BuildCompoundStep called with a non-compound expression: {expr.GetType().Name}.");
        }
    }

    private static ChainStepSidecar.ContactStep BuildLeafContactStep(
        Expr expr, ref int nextUid, List<SidecarAccessEntry> accessEntries, IReadOnlySet<string> localNames)
    {
        var (tagPath, negated) = expr switch
        {
            Expr.TagRef tagRef => (tagRef.Path, false),
            Expr.Not { Standalone: false, Operand: Expr.TagRef tagRef } => (tagRef.Path, true),
            _ => throw new UnsupportedSynthesisConstructException(
                $"BuildLeafContactStep called with a non-leaf expression: {expr.GetType().Name}."),
        };

        var contactUId = nextUid++;
        var accessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(tagPath, accessUId, ScopeFor(tagPath, localNames)));
        var operandWireUId = nextUid++;
        var outgoingWireUId = nextUid++;

        return new ChainStepSidecar.ContactStep(contactUId, accessUId, operandWireUId, negated, outgoingWireUId);
    }

    // A comparison as an ordinary chain position (v2) — confirmed real, FC ControlDelays: behaves
    // like a Contact (a pass-through position, own rail-facing/continuation port is "pre"), not
    // like an OrStep/TimerOutputStep terminal. SrcType is inferred from a literal operand's
    // magnitude (InferCompareSrcType): a Step/count comparison against a small Int stays "Int"
    // (C-118's Step tag, C-401's counters — unchanged), but a comparison against a literal that
    // cannot be an Int (e.g. a UDInt rollover constant like 4294967295) is typed from that literal,
    // so it synthesizes instead of failing at import on an Int overflow — found 2026-07-18 during
    // the gen-block-new validation of FB_FilterUnitSystem (REQ-023). Without a symbol table this is
    // a magnitude heuristic, not full type inference: a tag-vs-tag comparison (no literal) still
    // defaults to "Int", so a wide tag-vs-tag comparison remains a genuinely separate, unimplemented
    // case, not silently guessed at.
    private static ChainStepSidecar.CompareStep BuildCompareStep(
        Expr.Compare compare, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames, TagTypeRegistry tagTypes)
    {
        // 🔴 THE COMPARISON'S OWN TYPE TYPES ITS LITERALS — fixed 2026-08-12.
        //
        // SrcType is resolved FIRST and then handed to both operands as their `portType`, so
        // a literal's `<ConstantType>` is the comparison's type rather than a guess from the
        // literal's own digits. FI-55 fixed the SrcType half of exactly this bug and left the
        // literal half: with registers declared `UInt` — the honest type for a Modbus holding
        // register — the synthesizer emitted NINE literals as `Int`/`DInt` against `SrcType="UInt"`
        // compare boxes (`Int 1` x6, `Int 0`, `DInt 65535` x2), which TIA rejects by the same door
        // FI-55 came through: "the data type Int of the actual parameter does not match the data
        // type UInt of the formal parameter".
        //
        // Real TIA types them consistently — `simatic-ml/test-project001/FB_MotorFwdRevSystem.xml`
        // carries `<ConstantType>UDInt</ConstantType><ConstantValue>4294967295</ConstantValue>`
        // against a UDInt compare.
        //
        // NOT a UInt fix. Nothing had hit this because the committed corpus contains ZERO
        // `UInt`/`Word` comparisons — and `Word`, `USInt`, `UDInt` and `SInt` are all equally
        // unexercised, so the rule is "the operation's type wins", the same rule MUL/ADD/CALC's own
        // operands (portType: srcType) have always followed. Duration literals keep
        // their FI-54 carve-out inside ResolveOperand: they are TypedConstants with no
        // <ConstantType> at all, whatever type the surrounding operation has.
        var srcType = InferCompareSrcType(compare.Left, compare.Right, tagTypes);
        var left = ResolveOperand(compare.Left, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: srcType);
        var right = ResolveOperand(compare.Right, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: srcType);
        var comparePartUId = nextUid++;
        var outgoingWireUId = nextUid++;

        return new ChainStepSidecar.CompareStep(comparePartUId, ComparePartNameFor(compare.Operator), srcType, left, right, outgoingWireUId);
    }

    // The comparison's SrcType must match its operand type. Each operand contributes a type: a literal by
    // magnitude, a tag by its registry-resolved type. The widest present wins (Real outranks the integer
    // widths). A tag-vs-tag comparison whose tags resolve now types from them (closing the wide-tag gap); one
    // with no resolvable type still falls back to "Int" (unchanged).
    // FI-55 (2026-08-08). A TAG'S DECLARED TYPE OUTRANKS A LITERAL'S INFERRED ONE, ALWAYS.
    //
    // The literal's "type" is a guess from its digits; the tag's is a fact from its declaration.
    // Letting them compete on one ladder meant an unsigned tag LOST to its own literal and the
    // comparison was emitted as the signed type, which TIA rejects outright:
    //     "The data type UInt of the actual parameter does not match the data type Int of the
    //      formal parameter"
    // Found live on `IO.UnloadHoldSeq <> IO.UnloadHoldAck`-adjacent logic — the first unsigned
    // tag this corpus had ever compared to a constant.
    //
    // The old rank table was the mechanism: it listed Int/DInt/UDInt/LInt/ULInt/Real and returned
    // 0 for everything else, so UInt, USInt, Byte, Word and SInt all ranked BELOW Int and any
    // literal beat them. Widening the table alone would have fixed these five and left the same
    // trap for the next type nobody listed — so the precedence rule is the fix and the wider table
    // is the belt.
    private static string InferCompareSrcType(Expr left, Expr right, TagTypeRegistry tagTypes)
    {
        var fromTags = new[] { TagOperandType(left, tagTypes), TagOperandType(right, tagTypes) }
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList();

        if (fromTags.Count > 0)
        {
            return fromTags.OrderByDescending(TypeRank).First();
        }

        // Literal-vs-literal, or tags whose types don't resolve: fall back to magnitude inference
        // exactly as before.
        var fromLiterals = new[] { LiteralOperandType(left), LiteralOperandType(right) }
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList();

        return fromLiterals.Count == 0 ? "Int" : fromLiterals.OrderByDescending(TypeRank).First();
    }

    private static string? TagOperandType(Expr expr, TagTypeRegistry tagTypes) =>
        expr is Expr.TagRef tag ? tagTypes.Resolve(tag.Path) : null;

    private static string? LiteralOperandType(Expr expr) =>
        expr is Expr.Literal literal ? InferLiteralConstantType(literal.Value) : null;

    // Ordered by width, then by the pre-existing relative order among the types that were already
    // listed (Int < DInt < UDInt < LInt < ULInt < Real is preserved exactly, just shifted) so this
    // widening cannot change any comparison that already worked. Unknown still ranks 0 — but with
    // the tag-wins rule above, a type landing here no longer loses to a literal.
    private static int TypeRank(string type) => type switch
    {
        "SInt" or "USInt" or "Byte" => 1,
        "Int" or "UInt" or "Word" => 2,
        "DInt" or "DWord" => 3,
        "UDInt" => 4,
        "LInt" or "LWord" => 5,
        "ULInt" => 6,
        "Real" => 7,
        "LReal" => 8,
        _ => 0,
    };

    private static string ComparePartNameFor(string infixOperator) => infixOperator switch
    {
        "=" => "Eq",
        "<>" => "Ne",
        ">=" => "Ge",
        "<=" => "Le",
        ">" => "Gt",
        "<" => "Lt",
        _ => throw new UnsupportedSynthesisConstructException($"Unsupported comparison operator: '{infixOperator}'."),
    };

    // A tag-or-literal operand — shared by TON's PT, a comparison's Left/Right, MOVE's IN, and
    // MUL/ADD's Inputs (mirrors OperandSidecar's own "used by TON's PT and a comparison's
    // in1/in2" doc comment, generalized further here to every other tag-or-literal port this
    // synthesizer now covers). typedConstant selects which of the two real literal shapes to mint:
    // true for a TypedConstant (TON's PT, e.g. T#100MS, no ConstantType child — none of this
    // build's own timers actually use a literal PT, every one is HMI-tunable via a computed *MS
    // tag, but the shape is implemented for completeness rather than narrowed to "tag only"); false
    // for a LiteralConstant (every other case), whose ConstantType is inferred from the literal's
    // own text shape (a decimal point means Real; otherwise the narrowest integer type that holds
    // it — Int for in-range values, widening to DInt/UDInt/... for larger ones, InferLiteralConstantType)
    // — correct for Step numbers, counter increments, the x1000.0 HMI-seconds scale factor, and a
    // UDInt rollover constant alike; still a magnitude heuristic, not symbol-table type inference.
    //
    // 🔴 `portType` IS REQUIRED — NO DEFAULT, DELIBERATELY (2026-08-13). It is the declared type of
    // the PORT this operand feeds, and it is what types a literal. It used to be
    // `constantTypeOverride = null`, and an optional parameter is an invitation: SIX call sites
    // never passed it, including the CALL-argument site where the callee's own parameter type was
    // already resolved ON THE VERY NEXT LINE. Removing the default is the fix — a new operand site
    // now cannot silently fall back to magnitude, it must state its port type or pass null and say
    // why. Fixing the six sites alone would have left the seventh to be written next year.
    private static OperandSidecar ResolveOperand(
        Expr expr, bool typedConstant, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames, string? portType)
    {
        switch (expr)
        {
            case Expr.TagRef tagRef:
                var accessUId = nextUid++;
                accessEntries.Add(new SidecarAccessEntry(tagRef.Path, accessUId, ScopeFor(tagRef.Path, localNames)));
                var tagWireUId = nextUid++;
                return new OperandSidecar.TagOperand(accessUId, tagWireUId);

            case Expr.Literal literal:
                var constantUId = nextUid++;
                // portType carries the operation's own type where magnitude can't infer it
                // (a WAND mask `16#89` is a Word, not the Int its digits suggest) — from the caller's
                // resolved SrcType.
                // FI-54 (2026-08-08). A DURATION LITERAL IS ALWAYS A TypedConstant, whatever
                // position it appears in — never a LiteralConstant carrying ConstantType="Time".
                //
                // TIA REJECTS THE OTHER FORM OUTRIGHT, at import, not at compile:
                //   "The value 'T#0MS' cannot be set for the parameter of the type 'Time'"
                // and the whole block import fails. Found live, importing a `MOVE(IN := T#0MS)`
                // into a Time member — the first duration literal this corpus had ever placed
                // anywhere but a TON's PT.
                //
                // Why the old rule produced it: `typedConstant` is passed true only at a TON's PT,
                // so everywhere else the type came from `portType` (the operation's own
                // resolved type). For a MOVE into a Time member that override is "Time", which is
                // the one value that must never be written. The rule was keyed on POSITION when the
                // thing that actually decides is the LITERAL'S OWN KIND.
                //
                // The accepted shape is confirmed against a real TIA export
                // (`simatic-ml/reference/TimerSample.xml`): Scope="TypedConstant", a bare
                // <ConstantValue>T#100MS</ConstantValue>, and NO <ConstantType> child at all.
                //
                // 🔴 THE PORT'S DECLARED TYPE TYPES THE LITERAL; MAGNITUDE IS ONLY THE FALLBACK, AND
                // IT REFUSES RATHER THAN GUESSES (2026-08-13). See InferLiteralConstantType: a
                // base-prefixed literal has NO magnitude-derivable type, because a bit string's
                // width is a declaration choice its digits cannot express. `16#A93F2C71` used to be
                // emitted as `Int` — 32 bits into a 16-bit type, so every real 32-bit build stamp
                // failed to import, by construction.
                var constantType = typedConstant || IsDurationLiteral(literal.Value)
                    ? null
                    : (portType ?? InferLiteralConstantType(literal.Value) ?? throw new UnsupportedSynthesisConstructException(
                        $"Cannot type the literal '{literal.Value}': it is a base-prefixed (bit-string) literal, whose "
                        + "width is a declaration choice its digits cannot express, and the port it feeds has no "
                        + "resolvable declared type. Declare the destination/parameter type (pass `--project <ir-dir>` "
                        + "so it resolves), or write the value in decimal if it really is a plain number. Typing it by "
                        + "magnitude is what emitted every hex literal as `Int`."));
                constantEntries.Add(new SidecarConstantEntry(literal.Value, constantUId, constantType));
                var literalWireUId = nextUid++;
                return new OperandSidecar.LiteralOperand(constantUId, literalWireUId);

            default:
                throw new UnsupportedSynthesisConstructException(
                    $"Operand must be a tag reference or literal, found '{expr.GetType().Name}'.");
        }
    }

    // FI-54. IEC duration literals: `T#`/`TIME#` and the LTime forms, case-insensitive. Matched on
    // the prefix rather than by parsing the duration, because the question here is only "is this a
    // duration literal" — a malformed one is still a duration literal and still must not be given a
    // ConstantType, and TIA is the right thing to reject it rather than this.
    private static bool IsDurationLiteral(string value)
    {
        var v = value.TrimStart();
        return v.StartsWith("T#", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("LT#", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("TIME#", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("LTIME#", StringComparison.OrdinalIgnoreCase);
    }

    // The FALLBACK, used only when the port has no declared type. Returns null for a literal whose
    // type genuinely cannot be inferred from its own text, so the caller refuses instead of guessing.
    //
    // A BASE-PREFIXED LITERAL HAS NO MAGNITUDE-DERIVABLE TYPE. `16#89` is a `Word` in this corpus and
    // `16#7F` could be `Byte`, `Word` or `DWord` — a bit string's WIDTH IS A DECLARATION CHOICE, and
    // its digits cannot express it. The old code did not merely guess badly here, it never even
    // reached the magnitude test: `long.TryParse("16#A93F2C71")` fails, so EVERY base-prefixed
    // literal fell through to the `Int` default regardless of its value. A 32-bit build stamp was
    // therefore 16 bits by construction and could not import. Widening the parse would not fix it —
    // 2839872113 would then be typed `UDInt` into a `DWord` port, which TIA rejects by the same door.
    //
    // Note the `.` test below is deliberately reached only for non-base-prefixed values: `16#1.5`
    // is not a Real, and a base prefix now short-circuits before that test can misread one.
    private static string? InferLiteralConstantType(string value) =>
        IsBasePrefixedLiteral(value) ? null
        : value.Contains('.') ? "Real"
        : InferIntegerLiteralType(value);

    // Siemens' `<base>#<value>` notation, the same shape IrParser.ParseLeaf matches generically by
    // base-number shape rather than hardcoding base 16 (S1 item 12) — so `2#1011` and `8#77` are
    // covered, not just hex.
    private static bool IsBasePrefixedLiteral(string value)
    {
        var hash = value.IndexOf('#');
        if (hash <= 0 || hash == value.Length - 1)
        {
            return false;
        }

        // A duration literal is also `<something>#<value>`; it never reaches here (IsDurationLiteral
        // short-circuits in ResolveOperand) but the digits-only test keeps this predicate honest on
        // its own terms — `T#100MS`'s prefix is not a number.
        return value[..hash].All(char.IsAsciiDigit);
    }

    // Narrowest standard integer type that holds a non-decimal literal, defaulting to Int for
    // in-range values so existing Step/counter literals are unchanged; widens to DInt/UDInt/LInt/
    // ULInt only when the value genuinely exceeds Int (the UDInt-rollover case, 2026-07-18). A
    // base-prefixed or otherwise non-plain-decimal literal that won't parse falls back to Int
    // (unchanged behavior — those don't reach comparison synthesis today).
    private static string InferIntegerLiteralType(string value)
    {
        if (long.TryParse(value, out var signed))
        {
            if (signed >= short.MinValue && signed <= short.MaxValue) return "Int";
            if (signed >= int.MinValue && signed <= int.MaxValue) return "DInt";
            if (signed >= 0 && signed <= uint.MaxValue) return "UDInt";
            return "LInt";
        }

        return ulong.TryParse(value, out _) ? "ULInt" : "Int";
    }

    // A TON/TONR/TOF (TOF/TONR added 2026-07-18, TimingAndCalls — the read side already renders each
    // kind, so synthesis just builds the right sidecar). IN is an ordinary chain (same BuildChain
    // mechanism as a Coil's condition). A TONR additionally carries a reset (R) operand, built below.
    // Et is always a fresh OpenCon (wired, not read) — corrected from an initial "no
    // wire at all" attempt: that's a real, confirmed shape too, but only for a *standalone*
    // (GlobalVariable-scoped) timer (FC ControlDelays); this build's own timers are all multi-
    // instance (C-407, LocalVariable-scoped, matching MotorStarter's own real precedent, which
    // *always* wires ET to OpenCon) — live TIA import genuinely rejected the unconnected-ET shape
    // here ("The connection with the name 'ET' is not connected to the object with the UID"),
    // found 2026-07-15 building FB_PusherControl. Reset is built from timer.Reset (present for a
    // TONR, null for TON/TOF). Every reference to this timer's own
    // Q elsewhere in the network is an ordinary tag reference (e.g. "PusherEndTravelTimer.Q"),
    // resolved by the ordinary ResolveOperand/BuildLeafContactStep machinery like any other tag —
    // not a ChainStepSidecar.TimerOutputStep (that shape is for a Q wired *directly* into a
    // downstream Part with no ordinary Access in between, a real but different shape this build
    // never needs), so no inline "ensure this timer's own Part already exists" bookkeeping is
    // needed the way FlgNetBuilder's own read-side EnsureTimerBuilt requires — confirmed safe
    // because Access/Constant entries (which is what an ordinary Q-read mints) sit outside the
    // Parts-list flow-order constraint entirely (this class's own doc comment).
    private static TimerBindingSidecar BuildTimerSidecar(
        TimerBinding timer, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry, TagTypeRegistry tagTypes)
    {
        // TON/TONR/TOF all supported (TOF/TONR added 2026-07-18, TimingAndCalls). The read side and
        // FlgNetBuilder already render each kind; synthesis just builds the right sidecar — the same
        // shape for all three, plus the reset (R) operand a TONR carries and TON/TOF don't.
        var (chainRail, steps) = BuildChain(timer.In, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
        var tonPartUId = nextUid++;
        // portType null and irrelevant: `typedConstant: true` means a PT literal is a TypedConstant
        // with no <ConstantType> child at all, whatever type the port has (FI-54).
        var preset = ResolveOperand(timer.Pt, typedConstant: true, ref nextUid, accessEntries, constantEntries, localNames, portType: null);

        // Not added to accessEntries: FlgNetBuilder.BuildTimer embeds InstanceUId/Scope/Path
        // directly into the TON Part's own <Instance> sub-element (a real, live-verified finding —
        // adding a *second*, separate top-level Access entry for the same UId produced a genuine
        // TIA Import() rejection: "There are at least two definitions for UIds").
        var instanceUId = nextUid++;
        var instanceComponentPath = timer.InstancePath.Split('.');

        var etWireUId = nextUid++;
        var etOpenConUId = nextUid++;

        // The reset (R) input — a TONR carries one, TON/TOF don't. Minted after ET so the reset wire
        // sits last in flow order, matching the real export; its Access (like every operand Access)
        // is outside the Parts flow-order constraint, so late minting is fine.
        var reset = timer.Reset is null
            ? null
            // A TONR's R port is Bool; a literal there is `TRUE`/`FALSE`, which no numeric type
            // describes, so the port type is stated as null rather than invented.
            : ResolveOperand(timer.Reset, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: null);

        return new TimerBindingSidecar(
            tonPartUId,
            Version: "1.0",
            TimeType: "Time",
            instanceUId,
            // The instance scope follows the instance path the same way an operand's does: a timer
            // whose instance lives in a *global* DB (e.g. `DB_Timers.SampleTimer0`) is
            // GlobalVariable; a multi-instance FB timer (a local STATIC member) is LocalVariable.
            // Previously hardcoded LocalVariable — surfaced by the parity harness (TimerSample: a
            // real single-instance global-DB TON exports GlobalVariable, Gap G).
            ScopeFor(timer.InstancePath, localNames),
            instanceComponentPath,
            chainRail,
            steps,
            preset,
            Et: new OpenConnectionSidecar(etWireUId, etOpenConUId),
            timer.Kind,
            reset);
    }

    // A MOVE — En is a plain Expr (ordinary chain, not EnSource; confirmed real, MoveStatement's
    // own shape), unlike Mul/Convert below. In is a tag-or-literal operand (same resolver as a
    // TON's PT). DestTag is always a bare tag write (confirmed real: out1 always wires straight to
    // an ordinary Access, never a literal or expression).
    private static MoveStatementSidecar BuildMoveSidecar(
        MoveStatement move, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry, TagTypeRegistry tagTypes)
    {
        var (chainRail, steps) = BuildChain(move.En, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
        var movePartUId = nextUid++;
        // A literal `IN` is typed to the destination tag's type (a MOVE moves a value of the dest's type), not
        // by magnitude — so `IN := 0 => IO.HrsRun` (UDInt) carries ConstantType UDInt, matching the real
        // export. A tag `IN` ignores the override; an unresolvable dest falls back to magnitude.
        var inOperand = ResolveOperand(
            move.In, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames,
            portType: tagTypes.Resolve(move.DestTag));

        var destAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(move.DestTag, destAccessUId, ScopeFor(move.DestTag, localNames)));
        var destWireUId = nextUid++;

        return new MoveStatementSidecar(movePartUId, chainRail, steps, inOperand, destAccessUId, destWireUId);
    }

    // An EnSource — either an ordinary condition chain, or (v2) the reserved "EN := ENO" chained
    // form (confirmed real, MotorStarter's own "HMI Times" network: a Mul's eno wired straight into
    // the immediately-following Convert's own en). precedingEnoPartUId is supplied by the caller
    // (BuildConvertSidecar, index-paired with its own Mul — see that method's doc comment for why);
    // null means "no preceding Mul in this network", which is a hard error if the IR text actually
    // asked for ENO chaining.
    private static (int? RailWireUId, EnSourceSidecar Sidecar) BuildEnSourceSidecar(
        EnSource en, int? precedingEnoPartUId, int sharedRailWireUId, ref int nextUid,
        List<SidecarAccessEntry> accessEntries, List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>>? fanoutRegistry = null, TagTypeRegistry? tagTypes = null)
    {
        switch (en)
        {
            case EnSource.Condition condition:
                // A synthesizable box's EN chain (Mul/Add/Sub/Div, Convert, Swap, Abs, Calc, T_Sub, T_Conv) is
                // now marked for fan-out (ADR-0006 phase 4), so it shares the network's registry — a `{recv N}`
                // in a box EN reuses a node a coil/move mastered (MotorStarter N12: the increment ADD reuses
                // the `Q AND NOT RisingEdgeFlags` prefix the reset MOVE registered). A caller that never carries
                // a marked box EN (MOVE_BLK_VARIANT) omits it → a throwaway registry that is never consulted.
                var (chainRail, steps) = BuildChain(
                    condition.Value, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames,
                    fanoutRegistry ?? new Dictionary<int, IReadOnlyList<ChainStepSidecar>>(), tagTypes ?? TagTypeRegistry.Empty);
                return (chainRail, new EnSourceSidecar.ConditionSidecar(chainRail, steps));

            case EnSource.PrecedingEno:
                if (precedingEnoPartUId is null)
                {
                    throw new UnsupportedSynthesisConstructException(
                        "'EN := ENO' has no preceding Mul/Add in this network to chain from — sidecar " +
                        "synthesis pairs the Nth ENO-chained Convert with the Nth Mul/Add, in authored " +
                        "order (write one MUL/ADD immediately before each ENO-chained CONVERT).");
                }

                var enoWireUId = nextUid++;
                return (null, new EnSourceSidecar.PrecedingEnoSidecar(precedingEnoPartUId.Value, enoWireUId));

            default:
                throw new UnsupportedSynthesisConstructException($"Unsupported EnSource kind: {en.GetType().Name}.");
        }
    }

    // A MUL/ADD/SUB/DIV box (all four MulKinds — Subtract/Divide added 2026-07-18, SignalConditioning).
    // SrcType is always null (AutomaticTyped): TIA infers it from the operands, which is correct for
    // every grounded case (Real from the x1000.0 scale multiply, Int from a counter increment or the
    // Sub/Div here). precedingEnoPartUId chains an `EN := ENO` box from the box immediately before it
    // in the same network — a preceding Mul→Mul (Sub then Div) or the caller-paired Mul→Convert.
    private static MulStatementSidecar BuildMulSidecar(
        MulStatement mul, int? precedingEnoPartUId, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry, TagTypeRegistry tagTypes)
    {
        var (_, enSidecar) = BuildEnSourceSidecar(mul.En, precedingEnoPartUId, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
        var mulPartUId = nextUid++;
        // A literal input is typed to the operation type (the box's tag operand's type), not by magnitude — so
        // an increment `ADD(IN1 := 1, IN2 := IO.HrsRun)` types the `1` UDInt to match `IO.HrsRun`, as the real
        // export does. Falls back to magnitude when no input is a resolvable tag (all-literal box).
        var operandType = TryInputsType(tagTypes, mul.Inputs);
        var inputs = new List<OperandSidecar>();
        foreach (var input in mul.Inputs)
        {
            inputs.Add(ResolveOperand(
                input, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames,
                portType: operandType));
        }

        var destAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(mul.DestTag, destAccessUId, ScopeFor(mul.DestTag, localNames)));
        var destWireUId = nextUid++;

        return new MulStatementSidecar(mulPartUId, enSidecar, inputs, destAccessUId, destWireUId, mul.Kind, SrcType: null);
    }

    // A CONVERT — scoped (v2) to the real Real-seconds-to-DInt-milliseconds HMI idiom this whole
    // project's DB_Settings convention (C-307) is built on (confirmed real, MotorStarter's own
    // "HMI Times" network: Real seconds x1000.0 -> DInt milliseconds, feeding a TON's own PT).
    // SrcType/DestType are sidecar-only information with no field on the parsed ConvertStatement
    // model at all (there is no symbol table here to derive them from for a synthesized network,
    // unlike real data, where they come from the source XML's own type declarations) — hardcoded
    // "Real"/"DInt" rather than threaded through as a parameter, since every Convert this build
    // authors is this one idiom. A differently-typed Convert is a genuinely separate, unimplemented
    // case, not silently guessed at — extend this (and thread real types through) if one is ever
    // needed, rather than generalizing speculatively now.
    private static ConvertStatementSidecar BuildConvertSidecar(
        ConvertStatement convert, int? precedingEnoPartUId, int sharedRailWireUId, ref int nextUid,
        List<SidecarAccessEntry> accessEntries, List<SidecarConstantEntry> constantEntries,
        IReadOnlySet<string> localNames, TagTypeRegistry tagTypes,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry)
    {
        var (_, enSidecar) = BuildEnSourceSidecar(convert.En, precedingEnoPartUId, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
        var convertPartUId = nextUid++;

        // Src/DestType are the operand tag types (sidecar-only, not in the readable text). Resolve
        // them from the tag-type registry; fall back to the Real→DInt HMI-seconds→ms idiom this build
        // was originally grounded on when a type is unknown (no --project types, or a literal input) —
        // strictly better than the old hardcode, never worse. A tag-typed IN/dest now types correctly.
        //
        // Hoisted above the operand (2026-08-13) so `srcType` — the CONVERT's own input port type —
        // can type a literal IN. It was computed ten lines below the operand and never handed to it,
        // so `CONVERT(IN := 16#FF)` emitted `ConstantType Int` against `SrcType Real`.
        var srcType = (convert.In is Expr.TagRef inTag ? tagTypes.Resolve(inTag.Path) : null) ?? "Real";
        var destType = tagTypes.Resolve(convert.DestTag) ?? "DInt";

        var inOperand = ResolveOperand(convert.In, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: srcType);

        var destAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(convert.DestTag, destAccessUId, ScopeFor(convert.DestTag, localNames)));
        var destWireUId = nextUid++;

        return new ConvertStatementSidecar(convertPartUId, enSidecar, inOperand, srcType, destType, destAccessUId, destWireUId);
    }

    // An ABS box — en-gated (EnSource), one tag input, one dest write. SrcType is the input operand's
    // type (the output has the same type; no DestType), resolved from the tag-type registry — no safe
    // default exists (Real vs Int vs DInt all occur), so an unresolvable operand is a clear hard error.
    private static AbsStatementSidecar BuildAbsSidecar(
        AbsStatement abs, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames, TagTypeRegistry tagTypes,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry)
    {
        var (_, enSidecar) = BuildEnSourceSidecar(abs.En, precedingEnoPartUId: null, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
        var absPartUId = nextUid++;
        // Type first, then the operand it types (2026-08-13). RequireOperandType already refuses a
        // non-tag input, so no ABS literal reaches synthesis today and this changes no output — it
        // stops the site being an omission that would bite the moment that guard is relaxed.
        var srcType = RequireOperandType(tagTypes, abs.In, "ABS");
        var inOperand = ResolveOperand(abs.In, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: srcType);

        var destAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(abs.DestTag, destAccessUId, ScopeFor(abs.DestTag, localNames)));
        var destWireUId = nextUid++;

        return new AbsStatementSidecar(absPartUId, enSidecar, inOperand, srcType, destAccessUId, destWireUId);
    }

    // A SWAP box — structurally identical to ABS (both real instances are `Word`); SrcType resolved
    // from the input operand's type the same way.
    private static SwapStatementSidecar BuildSwapSidecar(
        SwapStatement swap, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames, TagTypeRegistry tagTypes,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry)
    {
        var (_, enSidecar) = BuildEnSourceSidecar(swap.En, precedingEnoPartUId: null, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
        var swapPartUId = nextUid++;
        // Type first, then the operand — same reasoning as ABS above.
        var srcType = RequireOperandType(tagTypes, swap.In, "SWAP");
        var inOperand = ResolveOperand(swap.In, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: srcType);

        var destAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(swap.DestTag, destAccessUId, ScopeFor(swap.DestTag, localNames)));
        var destWireUId = nextUid++;

        return new SwapStatementSidecar(swapPartUId, enSidecar, inOperand, srcType, destAccessUId, destWireUId);
    }

    // A box whose SrcType is its input operand's type (ABS/SWAP) needs that type resolved — there's
    // no safe default, so an unresolvable operand is a hard error, not a silent guess.
    private static string RequireOperandType(TagTypeRegistry tagTypes, Expr operand, string instruction)
    {
        if (operand is Expr.TagRef tag && tagTypes.Resolve(tag.Path) is string type)
        {
            return type;
        }

        var what = operand is Expr.TagRef t ? $"operand '{t.Path}'" : "a non-tag operand";
        throw new UnsupportedSynthesisConstructException(
            $"{instruction} synthesis needs its input {what}'s type, which couldn't be resolved — ensure it's " +
            "declared in the block interface or a --project DB/UDT/tag-table.");
    }

    // The operation type of a multi-input box (WAND/Calc) — the type of its first tag operand (its
    // literal inputs share it). Hard-error if no input is a resolvable tag, same discipline as
    // RequireOperandType (no safe default).
    private static string RequireInputsType(TagTypeRegistry tagTypes, IReadOnlyList<Expr> inputs, string instruction)
    {
        foreach (var input in inputs)
        {
            if (input is Expr.TagRef tag && tagTypes.Resolve(tag.Path) is string type)
            {
                return type;
            }
        }

        throw new UnsupportedSynthesisConstructException(
            $"{instruction} synthesis needs its operation type from a tag input, none of which resolved — ensure " +
            "at least one input tag is declared in the block interface or a --project DB/UDT/tag-table.");
    }

    // Soft variant of RequireInputsType: the first resolvable tag input's type, or null (no resolvable tag —
    // an all-literal box). Used only to *type a literal input* to the operation type; a miss falls back to
    // magnitude, so unlike ABS/SWAP/WAND — which genuinely need the type — this never hard-errors.
    private static string? TryInputsType(TagTypeRegistry tagTypes, IReadOnlyList<Expr> inputs)
    {
        foreach (var input in inputs)
        {
            if (input is Expr.TagRef tag && tagTypes.Resolve(tag.Path) is string type)
            {
                return type;
            }
        }

        return null;
    }

    // A WAND (bitwise word-AND box). En is a plain Expr chain (like Move); Inputs are tag-or-literal;
    // SrcType is the operation's word type, from the first tag input — and its literal inputs (a mask
    // like `16#89`) take that same type, not the Int their digits suggest (Gap F).
    private static WordAndStatementSidecar BuildWordAndSidecar(
        WordAndStatement wordAnd, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames, TagTypeRegistry tagTypes,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry)
    {
        var srcType = RequireInputsType(tagTypes, wordAnd.Inputs, "WAND");
        var (chainRail, steps) = BuildChain(wordAnd.En, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
        var andPartUId = nextUid++;

        var inputs = new List<OperandSidecar>();
        foreach (var input in wordAnd.Inputs)
        {
            inputs.Add(ResolveOperand(input, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: srcType));
        }

        var destAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(wordAnd.DestTag, destAccessUId, ScopeFor(wordAnd.DestTag, localNames)));
        var destWireUId = nextUid++;

        return new WordAndStatementSidecar(andPartUId, chainRail, steps, inputs, srcType, destAccessUId, destWireUId);
    }

    // A CALC (free-expression box). Like a MUL but the input combination is the Equation string
    // (carried verbatim, shown in the readable text); SrcType is the operation type from the first tag.
    private static CalcStatementSidecar BuildCalcSidecar(
        CalcStatement calc, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames, TagTypeRegistry tagTypes,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry)
    {
        var srcType = RequireInputsType(tagTypes, calc.Inputs, "CALC");
        var (_, enSidecar) = BuildEnSourceSidecar(calc.En, precedingEnoPartUId: null, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
        var calcPartUId = nextUid++;

        var inputs = new List<OperandSidecar>();
        foreach (var input in calc.Inputs)
        {
            inputs.Add(ResolveOperand(input, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: srcType));
        }

        var destAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(calc.DestTag, destAccessUId, ScopeFor(calc.DestTag, localNames)));
        var destWireUId = nextUid++;

        return new CalcStatementSidecar(calcPartUId, enSidecar, inputs, calc.Equation, srcType, destAccessUId, destWireUId);
    }

    // A T_SUB (time subtraction box). Version 1.2; two tag-or-literal inputs; DateType from In1, TimeType
    // from In2 (both Time in the one grounded Time−Time case).
    private static TSubStatementSidecar BuildTSubSidecar(
        TSubStatement tsub, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames, TagTypeRegistry tagTypes,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry)
    {
        var (_, enSidecar) = BuildEnSourceSidecar(tsub.En, precedingEnoPartUId: null, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
        var tsubPartUId = nextUid++;
        // Types first, then the operands they type — same reasoning as ABS above.
        var dateType = RequireOperandType(tagTypes, tsub.In1, "T_SUB");
        var timeType = RequireOperandType(tagTypes, tsub.In2, "T_SUB");
        var in1 = ResolveOperand(tsub.In1, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: dateType);
        var in2 = ResolveOperand(tsub.In2, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: timeType);

        var destAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(tsub.DestTag, destAccessUId, ScopeFor(tsub.DestTag, localNames)));
        var destWireUId = nextUid++;

        return new TSubStatementSidecar(tsubPartUId, "1.2", enSidecar, in1, in2, dateType, timeType, destAccessUId, destWireUId);
    }

    // A T_CONV (time type-conversion box). Convert-shaped plus Version 1.2; SrcType from the IN tag,
    // DestType from the dest tag; `EN := ENO` chains from the paired T_SUB (precedingEnoPartUId).
    private static TConvStatementSidecar BuildTConvSidecar(
        TConvStatement tconv, int? precedingEnoPartUId, int sharedRailWireUId, ref int nextUid,
        List<SidecarAccessEntry> accessEntries, List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames, TagTypeRegistry tagTypes,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry)
    {
        var (_, enSidecar) = BuildEnSourceSidecar(tconv.En, precedingEnoPartUId, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
        var tconvPartUId = nextUid++;
        // Types first, then the operand — same reasoning as ABS above.
        var srcType = RequireOperandType(tagTypes, tconv.In, "T_CONV");
        var destType = tagTypes.Resolve(tconv.DestTag) ?? throw new UnsupportedSynthesisConstructException(
            $"T_CONV synthesis needs its dest '{tconv.DestTag}' type, which couldn't be resolved.");
        var inOperand = ResolveOperand(tconv.In, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: srcType);

        var destAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(tconv.DestTag, destAccessUId, ScopeFor(tconv.DestTag, localNames)));
        var destWireUId = nextUid++;

        return new TConvStatementSidecar(tconvPartUId, "1.2", enSidecar, inOperand, srcType, destType, destAccessUId, destWireUId);
    }

    // A MOVE_BLK_VARIANT (block move with array indexing). Version 1.2; four fixed-named tag-or-literal
    // inputs; the first production with TWO output tags (Ret_Val + Dest), both plain-tag writes.
    private static MoveBlkVariantStatementSidecar BuildMoveBlkVariantSidecar(
        MoveBlkVariantStatement move, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        var (_, enSidecar) = BuildEnSourceSidecar(move.En, precedingEnoPartUId: null, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
        var partUId = nextUid++;
        // portType null, stated rather than defaulted (2026-08-13) — and this is the one operand site
        // the "type from the port" rule genuinely cannot cover. SRC is a `Variant` (it has no scalar
        // type at all), and COUNT/SRC_INDEX/DEST_INDEX carry TIA's own fixed port types, which live
        // in no registry this synthesizer can read and for which this project has no grounded export
        // to read them off. So the magnitude fallback applies here — correct for the plain decimal
        // counts and indices that are the only literals ever seen in these ports, and now a hard
        // error rather than a silent `Int` if a base-prefixed literal ever appears in one.
        var src = ResolveOperand(move.Src, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: null);
        var count = ResolveOperand(move.Count, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: null);
        var srcIndex = ResolveOperand(move.SrcIndex, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: null);
        var destIndex = ResolveOperand(move.DestIndex, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: null);

        var retValAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(move.RetValTag, retValAccessUId, ScopeFor(move.RetValTag, localNames)));
        var retValWireUId = nextUid++;

        var destAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(move.DestTag, destAccessUId, ScopeFor(move.DestTag, localNames)));
        var destWireUId = nextUid++;

        return new MoveBlkVariantStatementSidecar(
            partUId, "1.2", enSidecar, src, count, srcIndex, destIndex, retValAccessUId, retValWireUId, destAccessUId, destWireUId);
    }

    // An FB/FC CALL. Two argument shapes are supported:
    //   - Zero-argument (en-gated) — the site's STATIC-struct convention (C-115/C-118): the callee
    //     exposes its interface through a caller-visible STATIC struct wired by ordinary Coil/Move
    //     statements to the instance's dotted path before the CALL. Covers every equipment FB this
    //     project reuses (MotorStarter/MotorDOL, MotorFwdRevSystem, FB_PusherControl/sequencer).
    //   - Wired Input/Output arguments — for a callee exposing formal INPUT/OUTPUT parameters (a
    //     reusable, C-304/C-127-clean FB). The argument Type strings come from the callee's own .ir
    //     interface via CalleeInterfaceRegistry (ADR-0001: the callee .ir is the source of truth for
    //     its interface — the readable CALL deliberately omits types), mirroring what the read side
    //     records from a source <Parameter Type=…> element (GraphReducer.ReduceCall). See
    //     BuildCallArguments. InOut params are not yet supported — they resolve as "unknown parameter"
    //     and hard-error rather than being silently mistyped.
    // Instance is a standalone instance DB referenced by name (GlobalVariable scope, single-component
    // path — confirmed real, FC ControlDelays' own standalone-timer precedent), not a multi-instance
    // nested in the caller's own Static section (TON's own LocalVariable-scoped shape, see
    // BuildTimerSidecar). BlockType is inferred "FB" when an instance is present, "FC" otherwise — a
    // safe default matching the established correlation (ir/SPEC.md notes FB=>Instance/FC=>no-Instance
    // isn't proven universal).
    private static CallStatementSidecar BuildCallSidecar(
        CallStatement call, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames, CalleeInterfaceRegistry callees,
        Dictionary<int, IReadOnlyList<ChainStepSidecar>> fanoutRegistry, TagTypeRegistry tagTypes)
    {
        var (chainRail, steps) = BuildChain(call.En, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames, fanoutRegistry, tagTypes);
        var callPartUId = nextUid++;

        // Not added to accessEntries, same reason as BuildTimerSidecar's own instance reference
        // (see its doc comment): FlgNetBuilder.BuildCall embeds InstanceUId/Scope/Path directly
        // into the Call's own <Instance> sub-element.
        int? instanceUId = null;
        string? instanceScope = null;
        IReadOnlyList<string>? instanceComponentPath = null;
        if (call.InstancePath is { } instancePath)
        {
            instanceUId = nextUid++;

            // MULTI-INSTANCE: a call whose instance is a STATIC of this block is a LocalVariable, not a
            // global instance DB. This was hardcoded to GlobalVariableScope with a comment saying
            // multi-instance was out of scope, so TIA resolved `#ValveWater` as a global DB name and
            // reported "Missing instance DB" on a block that had imported cleanly. BuildTimerSidecar has
            // always done this correctly (its own ScopeFor call below), so the two paths were asymmetric
            // for no reason other than that no multi-instance FB call had ever been written.
            instanceScope = ScopeFor(instancePath, localNames);
            instanceComponentPath = instancePath.Split('.');
        }

        var arguments = BuildCallArguments(call, callees, ref nextUid, accessEntries, constantEntries, localNames);

        return new CallStatementSidecar(
            callPartUId,
            call.BlockName,
            BlockType: call.InstancePath is not null ? "FB" : "FC",
            chainRail,
            steps,
            instanceUId,
            instanceScope,
            instanceComponentPath,
            Arguments: arguments);
    }

    // Mints the wired Input/Output argument sidecars for a CALL, taking each argument's Type from the
    // callee's interface (CalleeInterfaceRegistry). Input value operands reuse ResolveOperand (the same
    // resolver the read side's Input path uses); Output destinations mint an ordinary dest Access + wire
    // exactly like MOVE's out1. Emitted in written order — TIA matches <Parameter> by Name, so order is
    // not semantically load-bearing. Every failure is an explicit hard-error naming the callee +
    // parameter, never a silent guess (hard rule 7).
    private static IReadOnlyList<CallArgumentSidecar> BuildCallArguments(
        CallStatement call, CalleeInterfaceRegistry callees, ref int nextUid,
        List<SidecarAccessEntry> accessEntries, List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        if (call.Arguments.Count == 0)
        {
            return Array.Empty<CallArgumentSidecar>();
        }

        if (!callees.TryGetBlock(call.BlockName, out var parms))
        {
            throw new UnsupportedSynthesisConstructException(
                $"Network: cannot synthesize the wired CALL to '{call.BlockName}' — the callee's interface is not " +
                "available. Include the callee's .ir in the same `to-xml --synthesize` batch (or pass " +
                "`--project <ir-dir>`) so its parameter types can be resolved (ADR-0001: the callee .ir is the " +
                "source of truth for its interface).");
        }

        var arguments = new List<CallArgumentSidecar>(call.Arguments.Count);
        foreach (var argument in call.Arguments)
        {
            switch (argument)
            {
                case CallArgument.InputArg input:
                {
                    var param = RequireParam(call.BlockName, input.ParamName, "Input", parms);
                    // 🔴 `param.Type` — the callee's own declared parameter type — is what types a
                    // literal argument (2026-08-13). It was resolved here and used on the next line
                    // for the sidecar's Type while the literal itself was typed by magnitude, so
                    // `CALL FB_Reg(Stamp := 16#A93F2C71)` against `Stamp : DWord` emitted
                    // `<ConstantType>Int</ConstantType>` and the block could not be imported. The
                    // type was never missing; it was simply not passed one line up.
                    var value = ResolveOperand(input.Value, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames, portType: param.Type);
                    arguments.Add(new CallArgumentSidecar.InputArgSidecar(input.ParamName, param.Type, value));
                    break;
                }

                case CallArgument.OutputArg output:
                {
                    var param = RequireParam(call.BlockName, output.ParamName, "Output", parms);
                    var destAccessUId = nextUid++;
                    accessEntries.Add(new SidecarAccessEntry(output.DestTag, destAccessUId, ScopeFor(output.DestTag, localNames)));
                    var destWireUId = nextUid++;
                    arguments.Add(new CallArgumentSidecar.OutputArgSidecar(output.ParamName, param.Type, destAccessUId, destWireUId));
                    break;
                }

                default:
                    throw new UnsupportedSynthesisConstructException($"Unsupported call argument kind: {argument.GetType().Name}");
            }
        }

        return arguments;
    }

    // Resolves a CALL parameter against the callee interface: it must exist and its section must match
    // the argument shape (an Input `:=` arg must be an Input param, an Output `=>` arg an Output param).
    private static CalleeInterfaceRegistry.Param RequireParam(
        string blockName, string paramName, string expectedSection,
        IReadOnlyDictionary<string, CalleeInterfaceRegistry.Param> parms)
    {
        if (!parms.TryGetValue(paramName, out var param))
        {
            throw new UnsupportedSynthesisConstructException(
                $"Network: CALL to '{blockName}' names parameter '{paramName}', which is not an Input or Output " +
                "parameter of the callee (InOut is not supported in synthesis).");
        }

        if (param.Section != expectedSection)
        {
            throw new UnsupportedSynthesisConstructException(
                $"Network: CALL to '{blockName}' passes '{paramName}' as {expectedSection}, but the callee " +
                $"declares it as {param.Section}.");
        }

        return param;
    }
}

public sealed class UnsupportedSynthesisConstructException : Exception
{
    public UnsupportedSynthesisConstructException(string message)
        : base(message)
    {
    }
}
