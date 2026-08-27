using System.Text;
using Converter.SimaticMl;

namespace Converter.Ir;

public static class IrSerializer
{
    public static string SerializeBlock(IrBlock block, IReadOnlyList<NetworkSidecar> sidecars)
    {
        var sb = new StringBuilder();
        AppendReadable(sb, block);

        sb.Append("\nSIDECAR\n");
        foreach (var sidecar in sidecars)
        {
            SerializeSidecarNetwork(sb, sidecar);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Readable-only form — no SIDECAR section. The derive-always canonical form (ADR-0005) for a
    /// fully-synthesizable block: its sidecar is re-derived by `to-xml` on demand rather than stored,
    /// so editing a network can't leave a stale sidecar behind (no D-6). A block that synthesis can't
    /// yet reproduce keeps its stored sidecar via <see cref="SerializeBlock"/>.
    /// </summary>
    public static string SerializeBlockReadable(IrBlock block)
    {
        var sb = new StringBuilder();
        AppendReadable(sb, block);
        return sb.ToString();
    }

    private static void AppendReadable(StringBuilder sb, IrBlock block)
    {
        sb.Append("BLOCK ").Append(block.Kind).Append(' ').Append(block.Name).Append('\n');
        sb.Append("ROOTID ").Append(block.RootUId).Append('\n');
        sb.Append("NUMBER ").Append(block.Number).Append('\n');
        sb.Append("LANGUAGE ").Append(block.Language).Append('\n');
        if (!string.IsNullOrEmpty(block.SecondaryType))
        {
            sb.Append("SECONDARYTYPE ").Append(block.SecondaryType).Append('\n');
        }

        // MEMORYLAYOUT (2026-08-12) — written only when the model carries one, so a block whose
        // IR predates this stays byte-identical and keeps emitting no <MemoryLayout> element.
        if (block.MemoryLayout is not null)
        {
            sb.Append("MEMORYLAYOUT ").Append(block.MemoryLayout).Append('\n');
        }

        if (!string.IsNullOrEmpty(block.Title))
        {
            sb.Append("TITLE \"").Append(EscapeString(block.Title)).Append("\"\n");
        }

        if (!string.IsNullOrEmpty(block.Comment))
        {
            sb.Append("COMMENT \"").Append(EscapeString(block.Comment)).Append("\"\n");
        }

        if (block.StaticMembers is not null || block.TempMembers.Count > 0 || block.InputMembers is not null
            || block.OutputMembers is not null || block.InOutMembers.Count > 0 || block.ConstantMembers is not null)
        {
            sb.Append('\n');
            SerializeInterface(sb, block);
        }

        foreach (var network in block.Networks)
        {
            sb.Append('\n');
            SerializeNetwork(sb, network);
        }
    }

    /// <summary>Network-only form, for unit tests that operate at network granularity (no BLOCK wrapper).</summary>
    public static string SerializeNetworkOnly(IrNetwork network)
    {
        var sb = new StringBuilder();
        SerializeNetwork(sb, network);
        return sb.ToString();
    }

    private static void SerializeNetwork(StringBuilder sb, IrNetwork network)
    {
        sb.Append("NETWORK ").Append(network.Number).Append(" \"").Append(EscapeString(network.Title)).Append('"');
        sb.Append(network.IsEmpty ? " [empty]\n" : "\n");

        // Comment (S1 item 16) — a network-level COMMENT line, mirroring the block-level one,
        // shown regardless of [empty] (same reasoning as Title itself, which already carries
        // through an empty network — Comment/Title both live on a source ObjectList sibling of
        // NetworkSource, independent of whether NetworkSource itself has content).
        if (!string.IsNullOrEmpty(network.Comment))
        {
            sb.Append("  COMMENT \"").Append(EscapeString(network.Comment)).Append("\"\n");
        }

        if (network.IsEmpty)
        {
            return;
        }

        foreach (var timer in network.Timers)
        {
            sb.Append("  ").Append(TimerKeywordFor(timer.Kind)).Append('(').Append(timer.InstancePath)
              .Append(", IN := ").Append(SerializeChain(timer.In))
              .Append(", PT := ").Append(SerializeExpr(timer.Pt));

            if (timer.Reset is { } reset)
            {
                sb.Append(", R := ").Append(SerializeExpr(reset));
            }

            sb.Append(")\n");
        }

        foreach (var assignment in network.Assignments)
        {
            sb.Append("  ").Append(CoilKeywordFor(assignment.Kind)).Append(' ').Append(assignment.CoilTag)
              .Append(" := ").Append(SerializeChain(assignment.Condition)).Append('\n');
        }

        foreach (var move in network.Moves)
        {
            sb.Append("  MOVE(EN := ").Append(SerializeChain(move.En))
              .Append(", IN := ").Append(SerializeExpr(move.In))
              .Append(") => ").Append(move.DestTag).Append('\n');
        }

        foreach (var wordAnd in network.WordAnds)
        {
            sb.Append("  WAND(EN := ").Append(SerializeChain(wordAnd.En));
            for (var k = 0; k < wordAnd.Inputs.Count; k++)
            {
                sb.Append(", IN").Append(k + 1).Append(" := ").Append(SerializeExpr(wordAnd.Inputs[k]));
            }

            sb.Append(") => ").Append(wordAnd.DestTag).Append('\n');
        }

        foreach (var call in network.Calls)
        {
            // InstancePath is omitted entirely when absent — confirmed real, 2026-07-12 (S1 item
            // 24, FB MotorVSDSystem's own call to the stateless FC "Scale"): an FC call has no instance
            // at all, unlike every FB call, which always does. `EN := ` is a reserved prefix no
            // real instance path could ever collide with, so the parser disambiguates on it.
            sb.Append("  CALL ").Append(call.BlockName).Append('(');
            if (call.InstancePath is not null)
            {
                sb.Append(call.InstancePath).Append(", ");
            }

            sb.Append("EN := ").Append(SerializeChain(call.En));

            foreach (var argument in call.Arguments)
            {
                switch (argument)
                {
                    case CallArgument.InputArg input:
                        sb.Append(", ").Append(input.ParamName).Append(" := ").Append(SerializeExpr(input.Value));
                        break;
                    case CallArgument.OutputArg output:
                        sb.Append(", ").Append(output.ParamName).Append(" => ").Append(output.DestTag);
                        break;
                    default:
                        throw new IrFormatException($"Unsupported call argument kind: {argument.GetType().Name}");
                }
            }

            sb.Append(")\n");
        }

        foreach (var mul in network.Muls)
        {
            sb.Append("  ").Append(MulKeywordFor(mul.Kind)).Append("(EN := ").Append(SerializeEnSource(mul.En));
            for (var k = 0; k < mul.Inputs.Count; k++)
            {
                sb.Append(", IN").Append(k + 1).Append(" := ").Append(SerializeExpr(mul.Inputs[k]));
            }

            sb.Append(") => ").Append(mul.DestTag).Append('\n');
        }

        foreach (var convert in network.Converts)
        {
            sb.Append("  CONVERT(EN := ").Append(SerializeEnSource(convert.En))
              .Append(", IN := ").Append(SerializeExpr(convert.In))
              .Append(") => ").Append(convert.DestTag).Append('\n');
        }

        foreach (var swap in network.Swaps)
        {
            sb.Append("  SWAP(EN := ").Append(SerializeEnSource(swap.En))
              .Append(", IN := ").Append(SerializeExpr(swap.In))
              .Append(") => ").Append(swap.DestTag).Append('\n');
        }

        foreach (var abs in network.AbsStatements)
        {
            sb.Append("  ABS(EN := ").Append(SerializeEnSource(abs.En))
              .Append(", IN := ").Append(SerializeExpr(abs.In))
              .Append(") => ").Append(abs.DestTag).Append('\n');
        }

        foreach (var limit in network.Limits)
        {
            sb.Append("  LIMIT(EN := ").Append(SerializeEnSource(limit.En))
              .Append(", MN := ").Append(SerializeExpr(limit.Min))
              .Append(", IN := ").Append(SerializeExpr(limit.In))
              .Append(", MX := ").Append(SerializeExpr(limit.Max))
              .Append(") => ").Append(limit.DestTag).Append('\n');
        }

        foreach (var tSub in network.TSubs)
        {
            sb.Append("  T_SUB(EN := ").Append(SerializeEnSource(tSub.En))
              .Append(", IN1 := ").Append(SerializeExpr(tSub.In1))
              .Append(", IN2 := ").Append(SerializeExpr(tSub.In2))
              .Append(") => ").Append(tSub.DestTag).Append('\n');
        }

        foreach (var tConv in network.TConvs)
        {
            sb.Append("  T_CONV(EN := ").Append(SerializeEnSource(tConv.En))
              .Append(", IN := ").Append(SerializeExpr(tConv.In))
              .Append(") => ").Append(tConv.DestTag).Append('\n');
        }

        // Equation trails the destination tag as a quoted string (escaped the same way TITLE/
        // COMMENT already are) — kept outside the comma-separated argument list entirely so the
        // equation text (which may itself contain arithmetic operators, though never a comma in
        // any real instance seen) can never collide with the top-level-comma-split the argument
        // list itself relies on.
        foreach (var calc in network.Calcs)
        {
            sb.Append("  CALC(EN := ").Append(SerializeEnSource(calc.En));
            for (var k = 0; k < calc.Inputs.Count; k++)
            {
                sb.Append(", IN").Append(k + 1).Append(" := ").Append(SerializeExpr(calc.Inputs[k]));
            }

            sb.Append(") => ").Append(calc.DestTag).Append(" \"").Append(EscapeString(calc.Equation)).Append("\"\n");
        }

        // No trailing "=> dest" — MOVE_BLK_VARIANT has two named outputs, not one, so both are
        // ordinary arguments inside the parens (":=" for inputs, "=>" for outputs), same mixing
        // convention CALL's own argument list already established.
        foreach (var moveBlkVariant in network.MoveBlkVariants)
        {
            sb.Append("  MOVE_BLK_VARIANT(EN := ").Append(SerializeEnSource(moveBlkVariant.En))
              .Append(", SRC := ").Append(SerializeExpr(moveBlkVariant.Src))
              .Append(", COUNT := ").Append(SerializeExpr(moveBlkVariant.Count))
              .Append(", SRC_INDEX := ").Append(SerializeExpr(moveBlkVariant.SrcIndex))
              .Append(", DEST_INDEX := ").Append(SerializeExpr(moveBlkVariant.DestIndex))
              .Append(", Ret_Val => ").Append(moveBlkVariant.RetValTag)
              .Append(", DEST => ").Append(moveBlkVariant.DestTag)
              .Append(")\n");
        }

        // No trailing "=> dest" — WAIT is a pure side-effecting delay, no destination at all.
        foreach (var wait in network.Waits)
        {
            sb.Append("  WAIT(EN := ").Append(SerializeEnSource(wait.En))
              .Append(", WT := ").Append(SerializeExpr(wait.Wt))
              .Append(")\n");
        }

        foreach (var fillBlockI in network.FillBlockIs)
        {
            sb.Append("  FILLBLOCKI(EN := ").Append(SerializeEnSource(fillBlockI.En))
              .Append(", IN := ").Append(SerializeExpr(fillBlockI.In))
              .Append(", COUNT := ").Append(SerializeExpr(fillBlockI.Count))
              .Append(") => ").Append(fillBlockI.DestTag).Append('\n');
        }

        // Instance path is the first positional argument (no label, same convention as TON/CALL's
        // own). No trailing "=> dest" — four named outputs, not one, mixed in with the inputs
        // inside the parens (":=" for inputs, "=>" for outputs), same convention MOVE_BLK_VARIANT
        // already established. FlowCtrl/RtsOnDly/RtsOffDly (Modbus_Comm_Load only) are
        // sidecar-only, never shown here — see ModbusCommLoadStatementSidecar's own doc comment.
        foreach (var modbusMaster in network.ModbusMasters)
        {
            sb.Append("  MODBUS_MASTER(").Append(modbusMaster.InstancePath)
              .Append(", EN := ").Append(SerializeEnSource(modbusMaster.En))
              .Append(", REQ := ").Append(SerializeExpr(modbusMaster.Req))
              .Append(", MB_ADDR := ").Append(SerializeExpr(modbusMaster.MbAddr))
              .Append(", MODE := ").Append(SerializeExpr(modbusMaster.Mode))
              .Append(", DATA_ADDR := ").Append(SerializeExpr(modbusMaster.DataAddr))
              .Append(", DATA_LEN := ").Append(SerializeExpr(modbusMaster.DataLen))
              .Append(", DATA_PTR := ").Append(SerializeExpr(modbusMaster.DataPtr))
              .Append(", DONE => ").Append(modbusMaster.DoneTag)
              .Append(", BUSY => ").Append(modbusMaster.BusyTag)
              .Append(", ERROR => ").Append(modbusMaster.ErrorTag)
              .Append(", STATUS => ").Append(modbusMaster.StatusTag)
              .Append(")\n");
        }

        foreach (var modbusCommLoad in network.ModbusCommLoads)
        {
            sb.Append("  MODBUS_COMM_LOAD(").Append(modbusCommLoad.InstancePath)
              .Append(", EN := ").Append(SerializeEnSource(modbusCommLoad.En))
              .Append(", REQ := ").Append(SerializeExpr(modbusCommLoad.Req))
              .Append(", PORT := ").Append(SerializeExpr(modbusCommLoad.Port))
              .Append(", BAUD := ").Append(SerializeExpr(modbusCommLoad.Baud))
              .Append(", PARITY := ").Append(SerializeExpr(modbusCommLoad.Parity))
              .Append(", RESP_TO := ").Append(SerializeExpr(modbusCommLoad.RespTo))
              .Append(", MB_DB := ").Append(SerializeExpr(modbusCommLoad.MbDb))
              .Append(", DONE => ").Append(modbusCommLoad.DoneTag)
              .Append(", ERROR => ").Append(modbusCommLoad.ErrorTag)
              .Append(", STATUS => ").Append(modbusCommLoad.StatusTag)
              .Append(")\n");
        }

        // Registry-driven fixed-shape instructions (MB_COMM_LOAD/MB_MASTER as of 2026-08-12).
        // Same layout as MODBUS_MASTER above — instance path first, then EN, then the template's
        // own ports in order — except that the port list is data, not code, and a deliberately
        // unconnected port is SHOWN as `OPEN` rather than hidden in the sidecar. Showing it is the
        // point: a port an AI cannot see is a port an AI cannot write back.
        foreach (var fixedShape in network.FixedShapes)
        {
            sb.Append("  ").Append(fixedShape.Instruction).Append('(').Append(fixedShape.InstancePath)
              .Append(", EN := ").Append(SerializeEnSource(fixedShape.En));
            foreach (var argument in fixedShape.Arguments)
            {
                sb.Append(", ").Append(argument.Port);
                switch (argument.Binding)
                {
                    case PortBinding.Value value:
                        sb.Append(" := ").Append(SerializeExpr(value.Expr));
                        break;
                    case PortBinding.Dest dest:
                        sb.Append(" => ").Append(dest.Tag);
                        break;
                    case PortBinding.OpenInput:
                        sb.Append(" := OPEN");
                        break;
                    case PortBinding.OpenOutput:
                        sb.Append(" => OPEN");
                        break;
                    default:
                        throw new IrFormatException($"Unsupported PortBinding kind: {argument.Binding.GetType().Name}");
                }
            }

            sb.Append(")\n");
        }
    }

    // The EN slot's own value — either an ordinary boolean expression (including the existing
    // "TRUE" sentinel) or, confirmed real 2026-07-12 (S1 item 18), the reserved word "ENO",
    // meaning "gated by the immediately preceding statement's own ENO" (see EnSource's own doc
    // comment for why this isn't a generic Expr).
    private static string SerializeEnSource(EnSource en) => en switch
    {
        EnSource.Condition condition => SerializeExpr(condition.Value),
        EnSource.PrecedingEno => "ENO",
        _ => throw new IrFormatException($"Unsupported EnSource kind: {en.GetType().Name}"),
    };

    // Only emitted when there's real content — matches every FC seen (all six member lists
    // null/empty), where the whole INTERFACE section is omitted per the "absence means default"
    // convention used throughout this format. INPUT/OUTPUT/STATIC/CONSTANT are each their own
    // optional subsection (null distinguishes "no such section in source at all" from "present
    // but empty" — confirmed a real distinction for Static since S1 item 7 Phase B, and now for
    // Input/Output/Constant too, S1 item 20); INOUT/TEMP always show when non-empty (no real
    // example of either being entirely absent has been seen). Section order matches the real
    // source's own (Input, Output, InOut, Static, Temp, Constant — BlockSourceWriter.WriteInterface).
    // Member-line grammar (VERSION/RETAIN/SETPOINT/nested indentation) is identical to a DB's own
    // MEMBERS section — DbMemberLineFormat is shared, confirmed real 2026-07-11 (S1 item 7 Phase
    // B); Input/Output/InOut/Constant members never carry nested content in any grounded example
    // (S1 item 20), so they reuse the same flat per-member line with no nested-member loop.
    private static void SerializeInterface(StringBuilder sb, IrBlock block)
    {
        sb.Append("INTERFACE\n");

        SerializeOptionalMemberSection(sb, "INPUT", block.InputMembers);
        SerializeOptionalMemberSection(sb, "OUTPUT", block.OutputMembers);
        SerializeMemberSection(sb, "INOUT", block.InOutMembers);

        if (block.StaticMembers is not null)
        {
            sb.Append("  STATIC\n");
            foreach (var member in block.StaticMembers)
            {
                DbMemberLineFormat.SerializeMemberRecursive(sb, "    ", member);
            }
        }

        SerializeMemberSection(sb, "TEMP", block.TempMembers);
        SerializeOptionalMemberSection(sb, "CONSTANT", block.ConstantMembers);
    }

    // Null vs. present-but-empty is a real, must-preserve distinction (same as StaticMembers'
    // own null check above) — a section header with zero member lines still gets emitted when
    // the source had the section present-but-empty (e.g. TomraControlSystem's own empty Constant
    // section), distinct from the section being entirely absent from the source (null).
    private static void SerializeOptionalMemberSection(StringBuilder sb, string keyword, IReadOnlyList<DbMember>? members)
    {
        if (members is null)
        {
            return;
        }

        sb.Append("  ").Append(keyword).Append('\n');
        foreach (var member in members)
        {
            DbMemberLineFormat.SerializeLine(sb, "    ", member);
        }
    }

    // InOut/Temp are never null (no real example of either section being entirely absent has
    // been seen) — an empty list is the only "nothing to say" case, so the header itself is
    // simply omitted when empty, unlike the nullable sections above.
    private static void SerializeMemberSection(StringBuilder sb, string keyword, IReadOnlyList<DbMember> members)
    {
        if (members.Count == 0)
        {
            return;
        }

        sb.Append("  ").Append(keyword).Append('\n');
        foreach (var member in members)
        {
            DbMemberLineFormat.SerializeLine(sb, "    ", member);
        }
    }

    // AND binds tighter than OR (standard precedence, confirmed with the project owner,
    // 2026-07-11, S1 item 11 — needed once an OR-merge branch can be a compound expression, not
    // just a single tag): "A AND B OR C" already reads unambiguously as "(A AND B) OR C" with no
    // parens — the exact shape a shared-prefix OR-merge branch produces (`FB MotorDOL`'s
    // `O(45)`). Parens are only emitted where precedence alone would misparse: an `Or` appearing
    // as an `And`'s own operand, or as a `Not`'s own operand (both bind looser than their parent
    // there). Every other nesting needs none — either the parent is already looser (And-in-Or),
    // or the child is a single self-contained token (Compare, TagRef, Literal) or already the
    // tightest binder (Not-in-anything).
    // A boolean chain (a coil/move/timer condition, an OR-branch) whose top-level element may itself carry
    // a fan-out marker (ADR-0006) — e.g. a whole condition that is one shared contact (N12). Routes through
    // SerializeOperand so that marker lands correctly even when the whole condition is a single element.
    private static string SerializeChain(Expr expr) => SerializeOperand(expr, precedenceParens: false);

    // The pure logic text of a node — no fan-out marker. And/Or operands go through SerializeOperand so a
    // marked operand's `{split N}`/`{recv N}` suffix lands OUTSIDE any precedence parentheses.
    private static string SerializeExpr(Expr expr) => expr switch
    {
        Expr.TagRef tagRef => tagRef.Path,
        Expr.Literal literal => literal.Value,
        // A standalone Not (invert-RLO) is parenthesised even around a single tag — `NOT (A)` — to
        // distinguish it from a negated contact `NOT A` (Gap H); a compound operand is parenthesised
        // regardless. The standalone form's inner is a chain (SerializeChain) so a fan-out marker on an
        // inner element (`NOT (EnableCmd{recv 1})`) is emitted; a negated contact wraps a bare tag whose
        // marker rides on the Not element itself (SerializeOperand), so its inner needs no marker pass.
        Expr.Not { Standalone: true } not => $"NOT ({SerializeChain(not.Operand)})",
        Expr.Not not => $"NOT {Parenthesize(not.Operand, not.Operand is Expr.And or Expr.Or)}",
        Expr.And { Operands.Count: 0 } => "TRUE",
        Expr.And and => string.Join(" AND ", and.Operands.Select(op => SerializeOperand(op, op is Expr.Or))),
        Expr.Or { Operands.Count: 0 } => "TRUE",
        Expr.Or or => string.Join(" OR ", or.Operands.Select(op => SerializeOperand(op, precedenceParens: false))),
        Expr.Compare compare => $"{SerializeExpr(compare.Left)} {compare.Operator} {SerializeExpr(compare.Right)}",
        _ => throw new IrFormatException($"Unsupported expression node: {expr.GetType().Name}"),
    };

    // A chain operand: precedence parens (if the caller needs them) OR marker-forced parens (a marked
    // comparison/OR must be parenthesised so its suffix binds the whole element, not the RHS/last branch),
    // then the `{split N}`/`{recv N}` suffix — always outside the parentheses. A marked TagRef/Not needs no
    // parens; a marked And never occurs (the And *is* the chain, not a chain element).
    private static string SerializeOperand(Expr expr, bool precedenceParens)
    {
        var markerParens = expr.Fanout is not null && expr is Expr.Compare or Expr.Or;
        var body = precedenceParens || markerParens ? $"({SerializeExpr(expr)})" : SerializeExpr(expr);
        return expr.Fanout is { } marker ? body + MarkerText(marker) : body;
    }

    private static string MarkerText(FanoutMarker marker) =>
        marker.Kind == FanoutMarkerKind.Split ? $"{{split {marker.Label}}}" : $"{{recv {marker.Label}}}";

    private static string Parenthesize(Expr expr, bool needsParens) =>
        needsParens ? $"({SerializeExpr(expr)})" : SerializeExpr(expr);

    // SCOIL/RCOIL (S1 item 15) mirror their own source Part Names ("SCoil"/"RCoil"), same
    // convention as COIL/TON/MOVE/CALL — WAND is the one deliberate exception, for a naming
    // collision that doesn't apply here.
    private static string CoilKeywordFor(CoilKind kind) => kind switch
    {
        CoilKind.Assign => "COIL",
        CoilKind.Set => "SCOIL",
        CoilKind.Reset => "RCOIL",
        _ => throw new IrFormatException($"Unsupported coil kind: {kind}"),
    };

    // TONR (S1 item 19)/TOF (S1 item 23) mirror their own source Part Names, same convention as
    // TON/COIL/MOVE/CALL.
    private static string TimerKeywordFor(TimerKind kind) => kind switch
    {
        TimerKind.Ton => "TON",
        TimerKind.Tonr => "TONR",
        TimerKind.Tof => "TOF",
        _ => throw new IrFormatException($"Unsupported timer kind: {kind}"),
    };

    // ADD (S1 item 19) mirrors its own source Part Name, same convention as MUL/CONVERT.
    // SUB/DIV (2026-07-14, FC Scale) extend the same convention.
    private static string MulKeywordFor(MulKind kind) => kind switch
    {
        MulKind.Multiply => "MUL",
        MulKind.Add => "ADD",
        MulKind.Subtract => "SUB",
        MulKind.Divide => "DIV",
        _ => throw new IrFormatException($"Unsupported Mul kind: {kind}"),
    };

    private static void SerializeSidecarNetwork(StringBuilder sb, NetworkSidecar sidecar)
    {
        sb.Append("NETWORK ").Append(sidecar.NetworkNumber).Append('\n');
        sb.Append("  compileunit = ").Append(sidecar.CompileUnitUId).Append('\n');
        foreach (var access in sidecar.AccessUIds)
        {
            sb.Append("  access ").Append(access.TagPath).Append(" = ").Append(access.UId).Append(' ').Append(access.Scope);

            // Variable-array-subscript scopes, as optional trailing ` idx<position>=<scope>` tokens.
            // Appended rather than inserted so that an access without one — which is nearly all of
            // them — serializes to exactly the line it always did, and every sidecar written before
            // this existed still parses. Emitted in ascending position order so the output is stable.
            foreach (var indexScope in access.IndexScopes.OrderBy(kv => kv.Key))
            {
                sb.Append(" idx").Append(indexScope.Key).Append('=').Append(indexScope.Value);
            }

            sb.Append('\n');
        }

        foreach (var constant in sidecar.ConstantUIds)
        {
            sb.Append("  constant ").Append(constant.Value).Append(" = ").Append(constant.UId)
              .Append(' ').Append(constant.ConstantType ?? "none").Append('\n');
        }

        for (var t = 0; t < sidecar.Timers.Count; t++)
        {
            var timer = sidecar.Timers[t];
            sb.Append("  timer ").Append(t).Append('\n');
            sb.Append("    tonpartuid = ").Append(timer.TonPartUId).Append('\n');
            sb.Append("    kind = ").Append(TimerSidecarKind(timer.Kind)).Append('\n');
            sb.Append("    version = ").Append(timer.Version).Append('\n');
            sb.Append("    timetype = ").Append(timer.TimeType).Append('\n');
            sb.Append("    instanceuid = ").Append(timer.InstanceUId).Append('\n');
            sb.Append("    instancescope = ").Append(timer.InstanceScope).Append('\n');
            sb.Append("    instancepath = ").Append(string.Join('.', timer.InstanceComponentPath)).Append('\n');
            sb.Append("    rail = ").Append(SerializeRail(timer.RailWireUId)).Append('\n');

            for (var s = 0; s < timer.Steps.Count; s++)
            {
                SerializeStep(sb, "    ", $"step {s}", timer.Steps[s]);
            }

            SerializeOperand(sb, "    ", "preset", timer.Preset);

            if (timer.Et is { } et)
            {
                sb.Append("    et = ").Append(et.WireUId).Append(' ').Append(et.OpenConUId).Append('\n');
            }

            if (timer.Reset is { } reset)
            {
                SerializeOperand(sb, "    ", "reset", reset);
            }
        }

        for (var a = 0; a < sidecar.Assignments.Count; a++)
        {
            var assignment = sidecar.Assignments[a];
            sb.Append("  assignment ").Append(a).Append('\n');
            sb.Append("    rail = ").Append(SerializeRail(assignment.RailWireUId)).Append('\n');

            for (var s = 0; s < assignment.Steps.Count; s++)
            {
                SerializeStep(sb, "    ", $"step {s}", assignment.Steps[s]);
            }

            sb.Append("    coil = ").Append(assignment.CoilUId).Append('\n');
            sb.Append("    coil operand = ").Append(assignment.CoilOperandAccessUId).Append('\n');
            sb.Append("    coil operandwire = ").Append(assignment.CoilOperandWireUId).Append('\n');
        }

        for (var m = 0; m < sidecar.Moves.Count; m++)
        {
            var move = sidecar.Moves[m];
            sb.Append("  move ").Append(m).Append('\n');
            sb.Append("    moveuid = ").Append(move.MovePartUId).Append('\n');
            sb.Append("    rail = ").Append(SerializeRail(move.RailWireUId)).Append('\n');

            for (var s = 0; s < move.Steps.Count; s++)
            {
                SerializeStep(sb, "    ", $"step {s}", move.Steps[s]);
            }

            SerializeOperand(sb, "    ", "in", move.In);

            sb.Append("    dest = ").Append(move.DestAccessUId).Append('\n');
            sb.Append("    destwire = ").Append(move.DestWireUId).Append('\n');
        }

        for (var d = 0; d < sidecar.WordAnds.Count; d++)
        {
            var wordAnd = sidecar.WordAnds[d];
            sb.Append("  wand ").Append(d).Append('\n');
            sb.Append("    anduid = ").Append(wordAnd.AndPartUId).Append('\n');
            sb.Append("    rail = ").Append(SerializeRail(wordAnd.RailWireUId)).Append('\n');

            for (var s = 0; s < wordAnd.Steps.Count; s++)
            {
                SerializeStep(sb, "    ", $"step {s}", wordAnd.Steps[s]);
            }

            for (var k = 0; k < wordAnd.Inputs.Count; k++)
            {
                SerializeOperand(sb, "    ", $"input {k}", wordAnd.Inputs[k]);
            }

            sb.Append("    srctype = ").Append(wordAnd.SrcType).Append('\n');
            sb.Append("    dest = ").Append(wordAnd.DestAccessUId).Append('\n');
            sb.Append("    destwire = ").Append(wordAnd.DestWireUId).Append('\n');
        }

        for (var c = 0; c < sidecar.Calls.Count; c++)
        {
            var call = sidecar.Calls[c];
            sb.Append("  call ").Append(c).Append('\n');
            sb.Append("    calluid = ").Append(call.CallPartUId).Append('\n');
            sb.Append("    blockname = ").Append(call.BlockName).Append('\n');
            sb.Append("    blocktype = ").Append(call.BlockType).Append('\n');
            sb.Append("    rail = ").Append(SerializeRail(call.RailWireUId)).Append('\n');

            for (var s = 0; s < call.Steps.Count; s++)
            {
                SerializeStep(sb, "    ", $"step {s}", call.Steps[s]);
            }

            // Omitted entirely when absent — confirmed real, 2026-07-12 (S1 item 24) — same
            // "absent lines mean default/no-instance" convention as the timer sidecar's own
            // optional `reset` lines (S1 item 19).
            if (call.InstanceUId is not null)
            {
                sb.Append("    instanceuid = ").Append(call.InstanceUId).Append('\n');
                sb.Append("    instancescope = ").Append(call.InstanceScope).Append('\n');
                sb.Append("    instancepath = ").Append(string.Join('.', call.InstanceComponentPath!)).Append('\n');
            }

            for (var a = 0; a < call.Arguments.Count; a++)
            {
                SerializeCallArgument(sb, "    ", a, call.Arguments[a]);
            }
        }

        for (var m2 = 0; m2 < sidecar.Muls.Count; m2++)
        {
            var mul = sidecar.Muls[m2];
            sb.Append("  mul ").Append(m2).Append('\n');
            sb.Append("    muluid = ").Append(mul.MulPartUId).Append('\n');
            sb.Append("    kind = ").Append(MulSidecarKind(mul.Kind)).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", mul.En);

            for (var k = 0; k < mul.Inputs.Count; k++)
            {
                SerializeOperand(sb, "    ", $"input {k}", mul.Inputs[k]);
            }

            // Confirmed real, 2026-07-12 (S1 item 20 live verification, FB AirStar) — omitted
            // when null (AutomaticTyped, the original shape), same "absent line means default"
            // convention as the timer sidecar's own optional `et` line.
            if (mul.SrcType is not null)
            {
                sb.Append("    srctype = ").Append(mul.SrcType).Append('\n');
            }

            sb.Append("    dest = ").Append(mul.DestAccessUId).Append('\n');
            sb.Append("    destwire = ").Append(mul.DestWireUId).Append('\n');
        }

        for (var c2 = 0; c2 < sidecar.Converts.Count; c2++)
        {
            var convert = sidecar.Converts[c2];
            sb.Append("  convert ").Append(c2).Append('\n');
            sb.Append("    convertuid = ").Append(convert.ConvertPartUId).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", convert.En);

            SerializeOperand(sb, "    ", "in", convert.In);

            sb.Append("    srctype = ").Append(convert.SrcType).Append('\n');
            sb.Append("    desttype = ").Append(convert.DestType).Append('\n');
            sb.Append("    dest = ").Append(convert.DestAccessUId).Append('\n');
            sb.Append("    destwire = ").Append(convert.DestWireUId).Append('\n');
        }

        for (var s = 0; s < sidecar.Swaps.Count; s++)
        {
            var swap = sidecar.Swaps[s];
            sb.Append("  swap ").Append(s).Append('\n');
            sb.Append("    swapuid = ").Append(swap.SwapPartUId).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", swap.En);

            SerializeOperand(sb, "    ", "in", swap.In);

            sb.Append("    srctype = ").Append(swap.SrcType).Append('\n');
            sb.Append("    dest = ").Append(swap.DestAccessUId).Append('\n');
            sb.Append("    destwire = ").Append(swap.DestWireUId).Append('\n');
        }

        for (var ab = 0; ab < sidecar.AbsStatements.Count; ab++)
        {
            var abs = sidecar.AbsStatements[ab];
            sb.Append("  abs ").Append(ab).Append('\n');
            sb.Append("    absuid = ").Append(abs.AbsPartUId).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", abs.En);

            SerializeOperand(sb, "    ", "in", abs.In);

            sb.Append("    srctype = ").Append(abs.SrcType).Append('\n');
            sb.Append("    dest = ").Append(abs.DestAccessUId).Append('\n');
            sb.Append("    destwire = ").Append(abs.DestWireUId).Append('\n');
        }

        for (var lm = 0; lm < sidecar.Limits.Count; lm++)
        {
            var limit = sidecar.Limits[lm];
            sb.Append("  limit ").Append(lm).Append('\n');
            sb.Append("    limituid = ").Append(limit.LimitPartUId).Append('\n');
            sb.Append("    version = ").Append(limit.Version).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", limit.En);

            SerializeOperand(sb, "    ", "mn", limit.Min);
            SerializeOperand(sb, "    ", "in", limit.In);
            SerializeOperand(sb, "    ", "mx", limit.Max);

            sb.Append("    valuetype = ").Append(limit.ValueType).Append('\n');
            sb.Append("    dest = ").Append(limit.DestAccessUId).Append('\n');
            sb.Append("    destwire = ").Append(limit.DestWireUId).Append('\n');
        }

        for (var ts = 0; ts < sidecar.TSubs.Count; ts++)
        {
            var tSub = sidecar.TSubs[ts];
            sb.Append("  tsub ").Append(ts).Append('\n');
            sb.Append("    tsubuid = ").Append(tSub.TSubPartUId).Append('\n');
            sb.Append("    version = ").Append(tSub.Version).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", tSub.En);

            SerializeOperand(sb, "    ", "in1", tSub.In1);
            SerializeOperand(sb, "    ", "in2", tSub.In2);

            sb.Append("    datetype = ").Append(tSub.DateType).Append('\n');
            sb.Append("    timetype = ").Append(tSub.TimeType).Append('\n');
            sb.Append("    dest = ").Append(tSub.DestAccessUId).Append('\n');
            sb.Append("    destwire = ").Append(tSub.DestWireUId).Append('\n');
        }

        for (var tc = 0; tc < sidecar.TConvs.Count; tc++)
        {
            var tConv = sidecar.TConvs[tc];
            sb.Append("  tconv ").Append(tc).Append('\n');
            sb.Append("    tconvuid = ").Append(tConv.TConvPartUId).Append('\n');
            sb.Append("    version = ").Append(tConv.Version).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", tConv.En);

            SerializeOperand(sb, "    ", "in", tConv.In);

            sb.Append("    srctype = ").Append(tConv.SrcType).Append('\n');
            sb.Append("    desttype = ").Append(tConv.DestType).Append('\n');
            sb.Append("    dest = ").Append(tConv.DestAccessUId).Append('\n');
            sb.Append("    destwire = ").Append(tConv.DestWireUId).Append('\n');
        }

        for (var cc = 0; cc < sidecar.Calcs.Count; cc++)
        {
            var calc = sidecar.Calcs[cc];
            sb.Append("  calc ").Append(cc).Append('\n');
            sb.Append("    calcuid = ").Append(calc.CalcPartUId).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", calc.En);

            for (var k = 0; k < calc.Inputs.Count; k++)
            {
                SerializeOperand(sb, "    ", $"input {k}", calc.Inputs[k]);
            }

            sb.Append("    equation = \"").Append(EscapeString(calc.Equation)).Append("\"\n");
            sb.Append("    srctype = ").Append(calc.SrcType).Append('\n');
            sb.Append("    dest = ").Append(calc.DestAccessUId).Append('\n');
            sb.Append("    destwire = ").Append(calc.DestWireUId).Append('\n');
        }

        for (var mb = 0; mb < sidecar.MoveBlkVariants.Count; mb++)
        {
            var moveBlkVariant = sidecar.MoveBlkVariants[mb];
            sb.Append("  moveblkvariant ").Append(mb).Append('\n');
            sb.Append("    moveblkvariantuid = ").Append(moveBlkVariant.MoveBlkVariantPartUId).Append('\n');
            sb.Append("    version = ").Append(moveBlkVariant.Version).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", moveBlkVariant.En);

            SerializeOperand(sb, "    ", "src", moveBlkVariant.Src);
            SerializeOperand(sb, "    ", "count", moveBlkVariant.Count);
            SerializeOperand(sb, "    ", "srcindex", moveBlkVariant.SrcIndex);
            SerializeOperand(sb, "    ", "destindex", moveBlkVariant.DestIndex);

            sb.Append("    retval = ").Append(moveBlkVariant.RetValAccessUId).Append('\n');
            sb.Append("    retvalwire = ").Append(moveBlkVariant.RetValWireUId).Append('\n');
            sb.Append("    dest = ").Append(moveBlkVariant.DestAccessUId).Append('\n');
            sb.Append("    destwire = ").Append(moveBlkVariant.DestWireUId).Append('\n');
        }

        for (var w = 0; w < sidecar.Waits.Count; w++)
        {
            var wait = sidecar.Waits[w];
            sb.Append("  wait ").Append(w).Append('\n');
            sb.Append("    waituid = ").Append(wait.WaitPartUId).Append('\n');
            sb.Append("    version = ").Append(wait.Version).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", wait.En);

            SerializeOperand(sb, "    ", "wt", wait.Wt);
        }

        for (var fb = 0; fb < sidecar.FillBlockIs.Count; fb++)
        {
            var fillBlockI = sidecar.FillBlockIs[fb];
            sb.Append("  fillblocki ").Append(fb).Append('\n');
            sb.Append("    fillblockiuid = ").Append(fillBlockI.FillBlockIPartUId).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", fillBlockI.En);

            SerializeOperand(sb, "    ", "in", fillBlockI.In);
            SerializeOperand(sb, "    ", "count", fillBlockI.Count);

            sb.Append("    dest = ").Append(fillBlockI.DestAccessUId).Append('\n');
            sb.Append("    destwire = ").Append(fillBlockI.DestWireUId).Append('\n');
        }

        for (var mm = 0; mm < sidecar.ModbusMasters.Count; mm++)
        {
            var modbusMaster = sidecar.ModbusMasters[mm];
            sb.Append("  modbusmaster ").Append(mm).Append('\n');
            sb.Append("    modbusmasteruid = ").Append(modbusMaster.ModbusMasterPartUId).Append('\n');
            sb.Append("    version = ").Append(modbusMaster.Version).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", modbusMaster.En);

            sb.Append("    instanceuid = ").Append(modbusMaster.InstanceUId).Append('\n');
            sb.Append("    instancescope = ").Append(modbusMaster.InstanceScope).Append('\n');
            sb.Append("    instancepath = ").Append(string.Join('.', modbusMaster.InstanceComponentPath)).Append('\n');

            sb.Append("    reqrail = ").Append(SerializeRail(modbusMaster.ReqRailWireUId)).Append('\n');
            for (var s = 0; s < modbusMaster.ReqSteps.Count; s++)
            {
                SerializeStep(sb, "    ", $"reqstep {s}", modbusMaster.ReqSteps[s]);
            }

            SerializeOperand(sb, "    ", "mbaddr", modbusMaster.MbAddr);
            SerializeOperand(sb, "    ", "mode", modbusMaster.Mode);
            SerializeOperand(sb, "    ", "dataaddr", modbusMaster.DataAddr);
            SerializeOperand(sb, "    ", "datalen", modbusMaster.DataLen);
            SerializeOperand(sb, "    ", "dataptr", modbusMaster.DataPtr);

            sb.Append("    done = ").Append(modbusMaster.DoneAccessUId).Append('\n');
            sb.Append("    donewire = ").Append(modbusMaster.DoneWireUId).Append('\n');
            sb.Append("    busy = ").Append(modbusMaster.BusyAccessUId).Append('\n');
            sb.Append("    busywire = ").Append(modbusMaster.BusyWireUId).Append('\n');
            sb.Append("    error = ").Append(modbusMaster.ErrorAccessUId).Append('\n');
            sb.Append("    errorwire = ").Append(modbusMaster.ErrorWireUId).Append('\n');
            sb.Append("    status = ").Append(modbusMaster.StatusAccessUId).Append('\n');
            sb.Append("    statuswire = ").Append(modbusMaster.StatusWireUId).Append('\n');
        }

        for (var mc = 0; mc < sidecar.ModbusCommLoads.Count; mc++)
        {
            var modbusCommLoad = sidecar.ModbusCommLoads[mc];
            sb.Append("  modbuscommload ").Append(mc).Append('\n');
            sb.Append("    modbuscommloaduid = ").Append(modbusCommLoad.ModbusCommLoadPartUId).Append('\n');
            sb.Append("    version = ").Append(modbusCommLoad.Version).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", modbusCommLoad.En);

            sb.Append("    instanceuid = ").Append(modbusCommLoad.InstanceUId).Append('\n');
            sb.Append("    instancescope = ").Append(modbusCommLoad.InstanceScope).Append('\n');
            sb.Append("    instancepath = ").Append(string.Join('.', modbusCommLoad.InstanceComponentPath)).Append('\n');

            SerializeOperand(sb, "    ", "req", modbusCommLoad.Req);
            SerializeOperand(sb, "    ", "port", modbusCommLoad.Port);
            SerializeOperand(sb, "    ", "baud", modbusCommLoad.Baud);
            SerializeOperand(sb, "    ", "parity", modbusCommLoad.Parity);

            if (modbusCommLoad.FlowCtrl is { } flowCtrl)
            {
                sb.Append("    flowctrl = ").Append(flowCtrl.WireUId).Append(' ').Append(flowCtrl.OpenConUId).Append('\n');
            }

            if (modbusCommLoad.RtsOnDly is { } rtsOnDly)
            {
                sb.Append("    rtsondly = ").Append(rtsOnDly.WireUId).Append(' ').Append(rtsOnDly.OpenConUId).Append('\n');
            }

            if (modbusCommLoad.RtsOffDly is { } rtsOffDly)
            {
                sb.Append("    rtsoffdly = ").Append(rtsOffDly.WireUId).Append(' ').Append(rtsOffDly.OpenConUId).Append('\n');
            }

            SerializeOperand(sb, "    ", "respto", modbusCommLoad.RespTo);
            SerializeOperand(sb, "    ", "mbdb", modbusCommLoad.MbDb);

            sb.Append("    done = ").Append(modbusCommLoad.DoneAccessUId).Append('\n');
            sb.Append("    donewire = ").Append(modbusCommLoad.DoneWireUId).Append('\n');
            sb.Append("    error = ").Append(modbusCommLoad.ErrorAccessUId).Append('\n');
            sb.Append("    errorwire = ").Append(modbusCommLoad.ErrorWireUId).Append('\n');
            sb.Append("    status = ").Append(modbusCommLoad.StatusAccessUId).Append('\n');
            sb.Append("    statuswire = ").Append(modbusCommLoad.StatusWireUId).Append('\n');
        }

        // A fixed-shape instruction's ports are DATA, so its sidecar carries one uniform `port`
        // line per bound port rather than a hand-written field per port name. Kinds mirror
        // PortBindingSidecar exactly: `tag`/`literal` carry (AccessUId, WireUId); `open` carries
        // (WireUId, OpenConUId) — an OpenCon's own UId is distinct from its wire's and neither may
        // be fabricated on rebuild.
        for (var fs = 0; fs < sidecar.FixedShapes.Count; fs++)
        {
            var fixedShape = sidecar.FixedShapes[fs];
            sb.Append("  fixedshape ").Append(fs).Append('\n');
            sb.Append("    fixedshapeuid = ").Append(fixedShape.PartUId).Append('\n');
            sb.Append("    instruction = ").Append(fixedShape.Instruction).Append('\n');
            sb.Append("    version = ").Append(fixedShape.Version).Append('\n');
            SerializeEnSourceSidecar(sb, "    ", fixedShape.En);

            sb.Append("    instanceuid = ").Append(fixedShape.InstanceUId).Append('\n');
            sb.Append("    instancescope = ").Append(fixedShape.InstanceScope).Append('\n');
            sb.Append("    instancepath = ").Append(string.Join('.', fixedShape.InstanceComponentPath)).Append('\n');

            foreach (var argument in fixedShape.Arguments)
            {
                sb.Append("    port ").Append(argument.Port).Append(' ');
                switch (argument.Binding)
                {
                    case PortBindingSidecar.Tag tag:
                        sb.Append("tag = ").Append(tag.AccessUId).Append(' ').Append(tag.WireUId);
                        break;
                    case PortBindingSidecar.Literal literal:
                        sb.Append("literal = ").Append(literal.ConstantUId).Append(' ').Append(literal.WireUId);
                        break;
                    case PortBindingSidecar.Open open:
                        sb.Append("open = ").Append(open.WireUId).Append(' ').Append(open.OpenConUId);
                        break;
                    default:
                        throw new IrFormatException($"Unsupported PortBindingSidecar kind: {argument.Binding.GetType().Name}");
                }

                sb.Append('\n');
            }
        }
    }

    // EnSourceSidecar's own text — "en = condition" followed by the same rail/steps shape every
    // other production's own en/IN chain already uses, or "en = eno <precedingUid> <wireUid>"
    // for the ENO-chained case (confirmed real, 2026-07-12, S1 item 18) — no steps/rail at all,
    // just the two UIds needed for exact regeneration.
    private static void SerializeEnSourceSidecar(StringBuilder sb, string indent, EnSourceSidecar en)
    {
        switch (en)
        {
            case EnSourceSidecar.ConditionSidecar condition:
                sb.Append(indent).Append("en = condition\n");
                sb.Append(indent).Append("  rail = ").Append(SerializeRail(condition.RailWireUId)).Append('\n');
                for (var s = 0; s < condition.Steps.Count; s++)
                {
                    SerializeStep(sb, indent + "  ", $"step {s}", condition.Steps[s]);
                }

                break;
            case EnSourceSidecar.PrecedingEnoSidecar precedingEno:
                sb.Append(indent).Append("en = eno ").Append(precedingEno.PrecedingPartUId).Append(' ').Append(precedingEno.WireUId).Append('\n');
                break;
            default:
                throw new IrFormatException($"Unsupported EnSource sidecar kind: {en.GetType().Name}");
        }
    }

    // A Call argument's sidecar text — "argument <n> input <name> <type>" followed by the same
    // tag-or-literal operand line SerializeOperand already produces (a wired Input parameter is
    // identical in shape to a TON's PT/a comparison's operand); "argument <n> output <name>
    // <type>" followed by the same dest/destwire pair Move's own out1 uses, just under this
    // argument's own header instead of the production's own top-level "dest"/"destwire" lines.
    private static void SerializeCallArgument(StringBuilder sb, string indent, int index, CallArgumentSidecar argument)
    {
        switch (argument)
        {
            case CallArgumentSidecar.InputArgSidecar input:
                sb.Append(indent).Append("argument ").Append(index).Append(" input ").Append(input.ParamName).Append(' ').Append(input.Type).Append('\n');
                SerializeOperand(sb, indent + "  ", "value", input.Value);
                break;
            case CallArgumentSidecar.OutputArgSidecar output:
                sb.Append(indent).Append("argument ").Append(index).Append(" output ").Append(output.ParamName).Append(' ').Append(output.Type).Append('\n');
                sb.Append(indent).Append("  dest = ").Append(output.DestAccessUId).Append('\n');
                sb.Append(indent).Append("  destwire = ").Append(output.DestWireUId).Append('\n');
                break;
            default:
                throw new IrFormatException($"Unsupported call argument kind: {argument.GetType().Name}");
        }
    }

    private static void SerializeStep(StringBuilder sb, string indent, string label, ChainStepSidecar step)
    {
        switch (step)
        {
            case ChainStepSidecar.ContactStep contact:
                sb.Append(indent).Append(label).Append(" contact\n");
                sb.Append(indent).Append("  uid = ").Append(contact.ContactUId).Append('\n');
                sb.Append(indent).Append("  operand = ").Append(contact.OperandAccessUId).Append('\n');
                sb.Append(indent).Append("  operandwire = ").Append(contact.OperandWireUId).Append('\n');
                sb.Append(indent).Append("  negated = ").Append(contact.Negated ? "true" : "false").Append('\n');
                sb.Append(indent).Append("  out = ").Append(contact.OutgoingWireUId).Append('\n');
                break;
            case ChainStepSidecar.OrStep orStep:
                sb.Append(indent).Append(label).Append(" or\n");
                sb.Append(indent).Append("  uid = ").Append(orStep.OrPartUId).Append('\n');
                for (var b = 0; b < orStep.Branches.Count; b++)
                {
                    SerializeOrBranch(sb, indent + "  ", $"branch {b}", orStep.Branches[b]);
                }

                sb.Append(indent).Append("  out = ").Append(orStep.OutgoingWireUId).Append('\n');
                break;
            case ChainStepSidecar.TimerOutputStep timerOutput:
                sb.Append(indent).Append(label).Append(" timeroutput\n");
                sb.Append(indent).Append("  tonpartuid = ").Append(timerOutput.TonPartUId).Append('\n');
                sb.Append(indent).Append("  port = ").Append(timerOutput.Port).Append('\n');
                sb.Append(indent).Append("  out = ").Append(timerOutput.OutgoingWireUId).Append('\n');
                break;
            case ChainStepSidecar.CompareStep compare:
                sb.Append(indent).Append(label).Append(" compare\n");
                sb.Append(indent).Append("  partname = ").Append(compare.PartName).Append('\n');
                sb.Append(indent).Append("  uid = ").Append(compare.ComparePartUId).Append('\n');
                sb.Append(indent).Append("  srctype = ").Append(compare.SrcType).Append('\n');
                SerializeOperand(sb, indent + "  ", "left", compare.Left);
                SerializeOperand(sb, indent + "  ", "right", compare.Right);
                sb.Append(indent).Append("  out = ").Append(compare.OutgoingWireUId).Append('\n');
                break;
            case ChainStepSidecar.NotStep notStep:
                sb.Append(indent).Append(label).Append(" not\n");
                sb.Append(indent).Append("  uid = ").Append(notStep.NotPartUId).Append('\n');
                sb.Append(indent).Append("  rail = ").Append(SerializeRail(notStep.RailWireUId)).Append('\n');
                for (var s = 0; s < notStep.Steps.Count; s++)
                {
                    SerializeStep(sb, indent + "  ", $"step {s}", notStep.Steps[s]);
                }

                sb.Append(indent).Append("  out = ").Append(notStep.OutgoingWireUId).Append('\n');
                break;
            default:
                throw new IrFormatException($"Unsupported chain step: {step.GetType().Name}");
        }
    }

    // An OR-merge branch is an ordinary mini-chain (S1 item 11) — same rail/steps shape every
    // other production (timer/assignment/move) already carries in its own top-level sidecar
    // block, just nested here under the OR-merge's own step instead.
    private static void SerializeOrBranch(StringBuilder sb, string indent, string label, OrBranch branch)
    {
        sb.Append(indent).Append(label).Append('\n');
        sb.Append(indent).Append("  rail = ").Append(SerializeRail(branch.RailWireUId)).Append('\n');
        for (var s = 0; s < branch.Steps.Count; s++)
        {
            SerializeStep(sb, indent + "  ", $"step {s}", branch.Steps[s]);
        }
    }

    // Shared tag-or-literal operand serialization — used by a TON's PT and a comparison's
    // left/right operand alike (see OperandSidecar's own doc comment).
    private static void SerializeOperand(StringBuilder sb, string indent, string label, OperandSidecar operand)
    {
        switch (operand)
        {
            case OperandSidecar.TagOperand tagOperand:
                sb.Append(indent).Append(label).Append(" tag = ").Append(tagOperand.AccessUId).Append(' ').Append(tagOperand.WireUId).Append('\n');
                break;
            case OperandSidecar.LiteralOperand literalOperand:
                sb.Append(indent).Append(label).Append(" literal = ").Append(literalOperand.ConstantUId).Append(' ').Append(literalOperand.WireUId).Append('\n');
                break;
            default:
                throw new IrFormatException($"Unsupported operand kind: {operand.GetType().Name}");
        }
    }

    // A chain that terminates via a TimerOutputStep never touches Powerrail — "none" records
    // that explicitly rather than a sentinel int, so a reader (or the parser) never mistakes it
    // for a real, if unusual, wire UId. Confirmed necessary real, 2026-07-11, FC TimerSample.
    private static string SerializeRail(int? railWireUId) => railWireUId?.ToString() ?? "none";

    // Sidecar-only lowercase Kind tokens (distinct from the readable-form TIMER/MUL keywords
    // above) — S1 item 19, mirrors the plain-text style every other sidecar field already uses
    // (e.g. "condition"/"eno" in SerializeEnSourceSidecar).
    private static string TimerSidecarKind(TimerKind kind) => kind switch
    {
        TimerKind.Ton => "ton",
        TimerKind.Tonr => "tonr",
        TimerKind.Tof => "tof",
        _ => throw new IrFormatException($"Unsupported timer kind: {kind}"),
    };

    private static string MulSidecarKind(MulKind kind) => kind switch
    {
        MulKind.Multiply => "mul",
        MulKind.Add => "add",
        MulKind.Subtract => "sub",
        MulKind.Divide => "div",
        _ => throw new IrFormatException($"Unsupported Mul kind: {kind}"),
    };

    // A newline in a quoted value USED TO BE A HARD ERROR here, on correct reasoning — the whole
    // `.ir` document is split on '\n' before any quoted-string parsing runs (IrParser.cs), so a raw
    // newline would desync reparsing rather than round-trip. What was missing was the other half:
    // the format defined no escape sequence, so the guard was a permanent refusal instead of a
    // guard. A real TIA V20 export settles that a multi-line comment is real (an S7-1200 Modbus TCP
    // FB with a three-line network comment), and refusing the whole block over its documentation
    // makes that block unmodifiable — the very thing the "no IR the AI cannot change" ruling
    // forbids. IrStringEscape now defines \n/\r and the parser reverses them; see its doc comment.
    private static string EscapeString(string value) => IrStringEscape.Escape(value);
}
