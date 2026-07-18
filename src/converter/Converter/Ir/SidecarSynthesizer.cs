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
        // which needs that TON's part UId already minted. Map each timer's instance path to its UId.
        var timers = new List<TimerBindingSidecar>();
        var timerPartUIdByInstancePath = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var timer in network.Timers)
        {
            var timerSidecar = BuildTimerSidecar(timer, railWireUId, ref nextUid, accessEntries, constantEntries, localNames);
            timers.Add(timerSidecar);
            timerPartUIdByInstancePath[timer.InstancePath] = timerSidecar.TonPartUId;
        }

        var assignments = new List<CoilAssignmentSidecar>();
        foreach (var assignment in network.Assignments)
        {
            assignments.Add(BuildAssignment(
                assignment, railWireUId, timerPartUIdByInstancePath, ref nextUid, accessEntries, constantEntries, localNames));
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
                // A Mul/Add/Sub/Div with `EN := ENO` chains from the immediately-preceding box in
                // the same network — Mul→Convert (existing) or Mul→Mul (Sub then Div, SignalConditioning).
                var precedingMulUId = muls.Count > 0 ? muls[^1].MulPartUId : (int?)null;
                var mulSidecar = BuildMulSidecar(network.Muls[i], precedingMulUId, railWireUId, ref nextUid, accessEntries, constantEntries, localNames);
                muls.Add(mulSidecar);
                mulPartUIdForEno = mulSidecar.MulPartUId;
            }

            if (i < network.Converts.Count)
            {
                converts.Add(BuildConvertSidecar(
                    network.Converts[i], mulPartUIdForEno, railWireUId, ref nextUid, accessEntries, constantEntries,
                    localNames, tagTypes ?? TagTypeRegistry.Empty));
            }
        }

        var types = tagTypes ?? TagTypeRegistry.Empty;

        var abs = new List<AbsStatementSidecar>();
        foreach (var absStatement in network.AbsStatements)
        {
            abs.Add(BuildAbsSidecar(absStatement, railWireUId, ref nextUid, accessEntries, constantEntries, localNames, types));
        }

        var swaps = new List<SwapStatementSidecar>();
        foreach (var swap in network.Swaps)
        {
            swaps.Add(BuildSwapSidecar(swap, railWireUId, ref nextUid, accessEntries, constantEntries, localNames, types));
        }

        var calls = new List<CallStatementSidecar>();
        foreach (var call in network.Calls)
        {
            calls.Add(BuildCallSidecar(call, railWireUId, ref nextUid, accessEntries, constantEntries, localNames, callees ?? CalleeInterfaceRegistry.Empty));
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
            AbsStatements: abs);
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

    // One coil assignment. The common case is an ordinary rail-to-coil chain; the exception (Gap G2)
    // is a coil fed *directly* by a same-network timer's Q — that wires straight from the TON's Q port
    // (a TimerOutputStep, no rail, no Access), exactly as TIA exports it. A cross-network `.Q` read has
    // no matching same-network timer here, so it falls through to the ordinary chain (an Access) —
    // which is precisely how the real export renders it (TimerSample N3).
    private static CoilAssignmentSidecar BuildAssignment(
        CoilAssignment assignment, int sharedRailWireUId, IReadOnlyDictionary<string, int> timerPartUIdByInstancePath,
        ref int nextUid, List<SidecarAccessEntry> accessEntries, List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        IReadOnlyList<ChainStepSidecar> steps;
        int? chainRail;
        if (assignment.Condition is Expr.TagRef tag
            && TrySplitTimerOutput(tag.Path, timerPartUIdByInstancePath) is (int tonPartUId, string port))
        {
            var outgoingWireUId = nextUid++;
            steps = new ChainStepSidecar[] { new ChainStepSidecar.TimerOutputStep(tonPartUId, port, outgoingWireUId) };
            chainRail = null; // fed by the timer's Q, never the rail
        }
        else
        {
            (chainRail, steps) = BuildChain(assignment.Condition, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
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
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        var left = ResolveOperand(compare.Left, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames);
        var right = ResolveOperand(compare.Right, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames);
        var comparePartUId = nextUid++;
        var outgoingWireUId = nextUid++;

        var srcType = InferCompareSrcType(compare.Left, compare.Right);
        return new ChainStepSidecar.CompareStep(comparePartUId, ComparePartNameFor(compare.Operator), srcType, left, right, outgoingWireUId);
    }

    // The comparison's SrcType must match its operand type. The only type signal available without a
    // symbol table is a literal operand's magnitude, so a comparison involving a literal is typed
    // from that literal (the widest, if both operands are literals); a tag-vs-tag comparison has no
    // signal and defaults to "Int". Real outranks the integer widths — a comparison against a Real
    // literal is a Real comparison.
    private static string InferCompareSrcType(Expr left, Expr right)
    {
        var present = new[] { LiteralTypeOrNull(left), LiteralTypeOrNull(right) }
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList();
        return present.Count == 0 ? "Int" : present.OrderByDescending(TypeRank).First();
    }

    private static string? LiteralTypeOrNull(Expr expr) =>
        expr is Expr.Literal literal ? InferLiteralConstantType(literal.Value) : null;

    private static int TypeRank(string type) => type switch
    {
        "Int" => 1,
        "DInt" => 2,
        "UDInt" => 3,
        "LInt" => 4,
        "ULInt" => 5,
        "Real" => 6,
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

    private static string InferLiteralConstantType(string value) =>
        value.Contains('.') ? "Real" : InferIntegerLiteralType(value);

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
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        // TON/TONR/TOF all supported (TOF/TONR added 2026-07-18, TimingAndCalls). The read side and
        // FlgNetBuilder already render each kind; synthesis just builds the right sidecar — the same
        // shape for all three, plus the reset (R) operand a TONR carries and TON/TOF don't.
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

        // The reset (R) input — a TONR carries one, TON/TOF don't. Minted after ET so the reset wire
        // sits last in flow order, matching the real export; its Access (like every operand Access)
        // is outside the Parts flow-order constraint, so late minting is fine.
        var reset = timer.Reset is null
            ? null
            : ResolveOperand(timer.Reset, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames);

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

    // A MUL/ADD/SUB/DIV box (all four MulKinds — Subtract/Divide added 2026-07-18, SignalConditioning).
    // SrcType is always null (AutomaticTyped): TIA infers it from the operands, which is correct for
    // every grounded case (Real from the x1000.0 scale multiply, Int from a counter increment or the
    // Sub/Div here). precedingEnoPartUId chains an `EN := ENO` box from the box immediately before it
    // in the same network — a preceding Mul→Mul (Sub then Div) or the caller-paired Mul→Convert.
    private static MulStatementSidecar BuildMulSidecar(
        MulStatement mul, int? precedingEnoPartUId, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames)
    {
        var (_, enSidecar) = BuildEnSourceSidecar(mul.En, precedingEnoPartUId, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
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
        List<SidecarAccessEntry> accessEntries, List<SidecarConstantEntry> constantEntries,
        IReadOnlySet<string> localNames, TagTypeRegistry tagTypes)
    {
        var (_, enSidecar) = BuildEnSourceSidecar(convert.En, precedingEnoPartUId, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
        var convertPartUId = nextUid++;
        var inOperand = ResolveOperand(convert.In, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames);

        var destAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(convert.DestTag, destAccessUId, ScopeFor(convert.DestTag, localNames)));
        var destWireUId = nextUid++;

        // Src/DestType are the operand tag types (sidecar-only, not in the readable text). Resolve
        // them from the tag-type registry; fall back to the Real→DInt HMI-seconds→ms idiom this build
        // was originally grounded on when a type is unknown (no --project types, or a literal input) —
        // strictly better than the old hardcode, never worse. A tag-typed IN/dest now types correctly.
        var srcType = (convert.In is Expr.TagRef inTag ? tagTypes.Resolve(inTag.Path) : null) ?? "Real";
        var destType = tagTypes.Resolve(convert.DestTag) ?? "DInt";

        return new ConvertStatementSidecar(convertPartUId, enSidecar, inOperand, srcType, destType, destAccessUId, destWireUId);
    }

    // An ABS box — en-gated (EnSource), one tag input, one dest write. SrcType is the input operand's
    // type (the output has the same type; no DestType), resolved from the tag-type registry — no safe
    // default exists (Real vs Int vs DInt all occur), so an unresolvable operand is a clear hard error.
    private static AbsStatementSidecar BuildAbsSidecar(
        AbsStatement abs, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames, TagTypeRegistry tagTypes)
    {
        var (_, enSidecar) = BuildEnSourceSidecar(abs.En, precedingEnoPartUId: null, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
        var absPartUId = nextUid++;
        var inOperand = ResolveOperand(abs.In, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames);
        var srcType = RequireOperandType(tagTypes, abs.In, "ABS");

        var destAccessUId = nextUid++;
        accessEntries.Add(new SidecarAccessEntry(abs.DestTag, destAccessUId, ScopeFor(abs.DestTag, localNames)));
        var destWireUId = nextUid++;

        return new AbsStatementSidecar(absPartUId, enSidecar, inOperand, srcType, destAccessUId, destWireUId);
    }

    // A SWAP box — structurally identical to ABS (both real instances are `Word`); SrcType resolved
    // from the input operand's type the same way.
    private static SwapStatementSidecar BuildSwapSidecar(
        SwapStatement swap, int sharedRailWireUId, ref int nextUid, List<SidecarAccessEntry> accessEntries,
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames, TagTypeRegistry tagTypes)
    {
        var (_, enSidecar) = BuildEnSourceSidecar(swap.En, precedingEnoPartUId: null, sharedRailWireUId, ref nextUid, accessEntries, constantEntries, localNames);
        var swapPartUId = nextUid++;
        var inOperand = ResolveOperand(swap.In, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames);
        var srcType = RequireOperandType(tagTypes, swap.In, "SWAP");

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
        List<SidecarConstantEntry> constantEntries, IReadOnlySet<string> localNames, CalleeInterfaceRegistry callees)
    {
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
                    var value = ResolveOperand(input.Value, typedConstant: false, ref nextUid, accessEntries, constantEntries, localNames);
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
