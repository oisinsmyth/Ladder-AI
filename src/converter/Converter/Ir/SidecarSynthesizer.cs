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
    public static IReadOnlyList<NetworkSidecar> SynthesizeBlock(IrBlock block)
    {
        var localNames = ComputeLocalNames(block);
        return block.Networks.Select(network => Synthesize(network, localNames)).ToList();
    }

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
        return localNames.Contains(firstComponent) ? LocalVariableScope : GlobalVariableScope;
    }

    // Convenience overload for a single network with no enclosing block context — every existing
    // test and v1 caller uses this, unaffected by the v2 local-scope refinement (NoLocalNames
    // reduces ScopeFor to the original always-GlobalVariable behavior exactly).
    public static NetworkSidecar Synthesize(IrNetwork network) => Synthesize(network, NoLocalNames);

    public static NetworkSidecar Synthesize(IrNetwork network, IReadOnlySet<string> localNames)
    {
        RequireInScope(network);

        var accessEntries = new List<SidecarAccessEntry>();
        var constantEntries = new List<SidecarConstantEntry>();
        var nextUid = 1;
        var railWireUId = nextUid++;

        var assignments = new List<CoilAssignmentSidecar>();
        foreach (var assignment in network.Assignments)
        {
            var (chainRail, steps) = BuildChain(assignment.Condition, railWireUId, ref nextUid, accessEntries, constantEntries, localNames);

            var coilUId = nextUid++;
            var coilOperandAccessUId = nextUid++;
            accessEntries.Add(new SidecarAccessEntry(assignment.CoilTag, coilOperandAccessUId, ScopeFor(assignment.CoilTag, localNames)));
            var coilOperandWireUId = nextUid++;

            assignments.Add(new CoilAssignmentSidecar(chainRail, steps, coilUId, coilOperandAccessUId, coilOperandWireUId));
        }

        var timers = new List<TimerBindingSidecar>();
        foreach (var timer in network.Timers)
        {
            timers.Add(BuildTimerSidecar(timer, railWireUId, ref nextUid, accessEntries, constantEntries, localNames));
        }

        var moves = new List<MoveStatementSidecar>();
        foreach (var move in network.Moves)
        {
            moves.Add(BuildMoveSidecar(move, railWireUId, ref nextUid, accessEntries, constantEntries, localNames));
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
                var mulSidecar = BuildMulSidecar(network.Muls[i], railWireUId, ref nextUid, accessEntries, constantEntries, localNames);
                muls.Add(mulSidecar);
                mulPartUIdForEno = mulSidecar.MulPartUId;
            }

            if (i < network.Converts.Count)
            {
                converts.Add(BuildConvertSidecar(
                    network.Converts[i], mulPartUIdForEno, railWireUId, ref nextUid, accessEntries, constantEntries, localNames));
            }
        }

        var calls = new List<CallStatementSidecar>();
        foreach (var call in network.Calls)
        {
            calls.Add(BuildCallSidecar(call, railWireUId, ref nextUid, accessEntries, constantEntries, localNames));
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
            Converts: converts);
    }

    // Every non-Assignments/Timers/Moves/Muls/Converts/Calls production list on IrNetwork is out
    // of scope for v2 — named individually in the error so a caller knows exactly what's missing,
    // not just "unsupported". Per-item scope narrowing within an in-scope list (TOF/TONR,
    // Subtract/Divide, wired-argument Call) is checked at the point of use, not here, so the error
    // names the *specific* unsupported shape rather than rejecting the whole network.
    private static void RequireInScope(IrNetwork network)
    {
        var populated = new List<string>();
        if (network.WordAnds.Count > 0)
        {
            populated.Add("WordAnds");
        }

        if (network.Swaps.Count > 0)
        {
            populated.Add("Swaps");
        }

        if (network.AbsStatements.Count > 0)
        {
            populated.Add("AbsStatements");
        }

        if (network.Limits.Count > 0)
        {
            populated.Add("Limits");
        }

        if (network.TSubs.Count > 0)
        {
            populated.Add("TSubs");
        }

        if (network.TConvs.Count > 0)
        {
            populated.Add("TConvs");
        }

        if (network.Calcs.Count > 0)
        {
            populated.Add("Calcs");
        }

        if (network.MoveBlkVariants.Count > 0)
        {
            populated.Add("MoveBlkVariants");
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

        if (populated.Count > 0)
        {
            throw new UnsupportedSynthesisConstructException(
                $"Network {network.Number}: sidecar synthesis does not support: {string.Join(", ", populated)}.");
        }
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
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        var operands = FlattenAnd(expr);
        var steps = new List<ChainStepSidecar>();

        for (var idx = 0; idx < operands.Count; idx++)
        {
            var operand = operands[idx];
            if (idx == 0 && IsCompound(operand))
            {
                steps.Add(BuildCompoundStep(operand, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames));
            }
            else if (operand is Expr.Compare compare)
            {
                steps.Add(BuildCompareStep(compare, ref nextUid, accessEntries, constantEntries, localNames));
            }
            else if (IsLeaf(operand))
            {
                steps.Add(BuildLeafContactStep(operand, ref nextUid, accessEntries, localNames));
            }
            else if (IsCompound(operand))
            {
                throw new UnsupportedSynthesisConstructException(
                    "Only the first operand of an AND chain may be a compound (OR, or NOT-of-non-leaf) " +
                    "expression — this matches every real series-ladder-chain shape this converter has " +
                    "ever seen (a branch point can only occur at the rail-most position of a chain). " +
                    $"Found a compound operand at position {idx}.");
            }
            else
            {
                throw new UnsupportedSynthesisConstructException(
                    $"Sidecar synthesis does not support '{operand.GetType().Name}' expression nodes " +
                    "yet (only TagRef/And/Or/Not/Compare are supported).");
            }
        }

        var railWired = operands.Count == 0 || !IsCompound(operands[0]);
        return (railWired ? sharedRailWireUId : null, steps);
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

    private static bool IsCompound(Expr expr) =>
        expr is Expr.Or || (expr is Expr.Not not && not.Operand is not Expr.TagRef);

    private static bool IsLeaf(Expr expr) =>
        expr is Expr.TagRef || (expr is Expr.Not { Operand: Expr.TagRef });

    private static ChainStepSidecar BuildCompoundStep(
        Expr expr, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        switch (expr)
        {
            case Expr.Or or:
                // Build every branch (and everything upstream of it) before minting the O Part's
                // own UId — see this class's own doc comment for why minting order must produce
                // ascending-UId-equals-signal-flow-order on its own.
                var branches = new List<OrBranch>();
                foreach (var operand in or.Operands)
                {
                    var (branchRail, branchSteps) = BuildChain(operand, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
                    branches.Add(new OrBranch(branchSteps, branchRail));
                }

                var orPartUId = nextUid++;
                var orOutgoingWireUId = nextUid++;
                return new ChainStepSidecar.OrStep(orPartUId, branches, orOutgoingWireUId);

            case Expr.Not not:
                var (notRail, notSteps) = BuildChain(not.Operand, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
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
            Expr.Not { Operand: Expr.TagRef tagRef } => (tagRef.Path, true),
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
    // like an OrStep/TimerOutputStep terminal. SrcType is hardcoded "Int" — every comparison this
    // build needs is a Step/count comparison against a plain Int (C-118's Step tag, C-401's
    // counters); there is no symbol table here to infer a real type from, so a Real (or other)
    // comparison is a genuinely separate, unimplemented case, not silently guessed at.
    private static ChainStepSidecar.CompareStep BuildCompareStep(
        Expr.Compare compare, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        var left = ResolveOperand(compare.Left, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames);
        var right = ResolveOperand(compare.Right, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames);
        var comparePartUId = nextUid++;
        var outgoingWireUId = nextUid++;

        return new ChainStepSidecar.CompareStep(comparePartUId, ComparePartNameFor(compare.Operator), "Int", left, right, outgoingWireUId);
    }

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
    // own text shape (a decimal point means Real, otherwise Int) — correct for every literal this
    // build actually writes (Step numbers, counter increments, the x1000.0 HMI-seconds scale
    // factor), not a general numeric-type inference.
    private static OperandSidecar ResolveOperand(
        Expr expr, bool typedConstant, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
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
                var constantType = typedConstant ? null : InferLiteralConstantType(literal.Value);
                constantEntries.Add(new SidecarConstantEntry(literal.Value, constantUId, constantType));
                var literalWireUId = nextUid++;
                return new OperandSidecar.LiteralOperand(constantUId, literalWireUId);

            default:
                throw new UnsupportedSynthesisConstructException(
                    $"Operand must be a tag reference or literal, found '{expr.GetType().Name}'.");
        }
    }

    private static string InferLiteralConstantType(string value) => value.Contains('.') ? "Real" : "Int";

    // A TON — confirmed always TimerKind.Ton by construction (TOF/TONR hard-error below, matching
    // site convention C-406's own "TON is the only timer instruction used" as well as being
    // genuinely unimplemented here). IN is an ordinary chain (same BuildChain mechanism as a Coil's
    // condition). Et is always a fresh OpenCon (wired, not read) — corrected from an initial "no
    // wire at all" attempt: that's a real, confirmed shape too, but only for a *standalone*
    // (GlobalVariable-scoped) timer (FC ControlDelays); this build's own timers are all multi-
    // instance (C-407, LocalVariable-scoped, matching MotorStarter's own real precedent, which
    // *always* wires ET to OpenCon) — live TIA import genuinely rejected the unconnected-ET shape
    // here ("The connection with the name 'ET' is not connected to the object with the UID"),
    // found 2026-07-15 building FB_PusherControl. Reset is always
    // null — TONR-only, unreachable once Kind is validated Ton. Every reference to this timer's own
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
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        if (timer.Kind != TimerKind.Ton)
        {
            throw new UnsupportedSynthesisConstructException(
                $"Network: TON is the only timer instruction sidecar synthesis supports (site convention " +
                $"C-406 — TOF/TONR are also unimplemented here regardless) — found '{timer.Kind}'.");
        }

        var (chainRail, steps) = BuildChain(timer.In, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
        var tonPartUId = nextUid++;
        var preset = ResolveOperand(timer.Pt, typedConstant: true, ref nextUid, accessEntries, constantEntries, localNames);

        // Not added to accessEntries: FlgNetBuilder.BuildTimer embeds InstanceUId/Scope/Path
        // directly into the TON Part's own <Instance> sub-element (a real, live-verified finding —
        // adding a *second*, separate top-level Access entry for the same UId produced a genuine
        // TIA Import() rejection: "There are at least two definitions for UIds").
        var instanceUId = nextUid++;
        var instanceComponentPath = timer.InstancePath.Split('.');

        var etWireUId = nextUid++;
        var etOpenConUId = nextUid++;

        return new TimerBindingSidecar(
            tonPartUId,
            Version: "1.0",
            TimeType: "Time",
            instanceUId,
            LocalVariableScope,
            instanceComponentPath,
            chainRail,
            steps,
            preset,
            Et: new OpenConnectionSidecar(etWireUId, etOpenConUId),
            TimerKind.Ton,
            Reset: null);
    }

    // A MOVE — En is a plain Expr (ordinary chain, not EnSource; confirmed real, MoveStatement's
    // own shape), unlike Mul/Convert below. In is a tag-or-literal operand (same resolver as a
    // TON's PT). DestTag is always a bare tag write (confirmed real: out1 always wires straight to
    // an ordinary Access, never a literal or expression).
    private static MoveStatementSidecar BuildMoveSidecar(
        MoveStatement move, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        var (chainRail, steps) = BuildChain(move.En, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
        var movePartUId = nextUid++;
        var inOperand = ResolveOperand(move.In, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames);

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
        List<SidecarAccessEntry> accessEntries, List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        switch (en)
        {
            case EnSource.Condition condition:
                var (chainRail, steps) = BuildChain(condition.Value, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
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

    // A MUL/ADD — Subtract/Divide hard-error (unimplemented; this build only ever multiplies an
    // HMI seconds value by 1000.0 or increments a plain Int counter, both Multiply/Add). SrcType is
    // always null (AutomaticTyped) — the original, more common real shape (MotorDOL/EquipmentControlSystem),
    // and correct for both of this build's own uses (TIA infers Real from the x1000.0 scale
    // multiply, Int from the counter increment).
    private static MulStatementSidecar BuildMulSidecar(
        MulStatement mul, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        if (mul.Kind is not (MulKind.Multiply or MulKind.Add))
        {
            throw new UnsupportedSynthesisConstructException(
                $"Network: sidecar synthesis only supports MUL/ADD (Subtract/Divide are unimplemented here) — found '{mul.Kind}'.");
        }

        var (_, enSidecar) = BuildEnSourceSidecar(mul.En, precedingEnoPartUId: null, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
        var mulPartUId = nextUid++;
        var inputs = new List<OperandSidecar>();
        foreach (var input in mul.Inputs)
        {
            inputs.Add(ResolveOperand(input, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames));
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
        List<SidecarAccessEntry> accessEntries, List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        var (_, enSidecar) = BuildEnSourceSidecar(convert.En, precedingEnoPartUId, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
        var convertPartUId = nextUid++;
        var inOperand = ResolveOperand(convert.In, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames);

        var destAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(convert.DestTag, destAccessUId, ScopeFor(convert.DestTag, localNames)));
        var destWireUId = nextUid++;

        return new ConvertStatementSidecar(convertPartUId, enSidecar, inOperand, "Real", "DInt", destAccessUId, destWireUId);
    }

    // An FB CALL — scoped (v2) to zero wired arguments: a Call with any Input/Output argument hard-
    // errors, unimplemented (there is no grounded, live-verified shape here to mint CallArgumentSidecar
    // Type strings from without a real donor). Every equipment FB this project has built or reused
    // (MotorStarter/MotorDOL, MotorFwdRevSystem, and this build's own new FB_PusherControl/sequencer)
    // exposes its interface through a caller-visible STATIC struct (C-115/C-118) instead of Input/
    // Output parameters, wired by ordinary Coil/Move statements to the instance's own dotted path
    // before the CALL — so a zero-argument, en-gated CALL covers every real need this build has.
    // Instance is a standalone instance DB referenced by name (GlobalVariable scope, single-component
    // path — confirmed real, FC ControlDelays' own standalone-timer precedent: "a single Component
    // naming its own instance DB directly"), not a multi-instance nested in the caller's own Static
    // section (that's TON's own LocalVariable-scoped shape, genuinely different, see BuildTimerSidecar) —
    // matching how PlantAutoControl's own 20 real FB calls each reference their own dedicated instance DB.
    // BlockType is inferred "FB" when an instance is present, "FC" otherwise — a safe default for
    // this build specifically (every call it authors either has one or doesn't, matching the
    // established correlation), not a claimed general rule (ir/SPEC.md is explicit that FB=>Instance/
    // FC=>no-Instance isn't proven universal).
    private static CallStatementSidecar BuildCallSidecar(
        CallStatement call, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        if (call.Arguments.Count > 0)
        {
            throw new UnsupportedSynthesisConstructException(
                $"Network: sidecar synthesis only supports zero-argument CALLs (found {call.Arguments.Count} " +
                $"wired argument(s) on a call to '{call.BlockName}') — every equipment FB this build calls " +
                "exposes its interface through a STATIC struct instead, wired by ordinary Coil/Move " +
                "statements before the CALL, not through Input/Output parameters.");
        }

        var (chainRail, steps) = BuildChain(call.En, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
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
            instanceScope = GlobalVariableScope;
            instanceComponentPath = instancePath.Split('.');
        }

        return new CallStatementSidecar(
            callPartUId,
            call.BlockName,
            BlockType: call.InstancePath is not null ? "FB" : "FC",
            chainRail,
            steps,
            instanceUId,
            instanceScope,
            instanceComponentPath,
            Arguments: Array.Empty<CallArgumentSidecar>());
    }
}

public sealed class UnsupportedSynthesisConstructException : Exception
{
    public UnsupportedSynthesisConstructException(string message)
        : base(message)
    {
    }
}
