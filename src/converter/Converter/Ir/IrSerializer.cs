using System.Text;
using Converter.SimaticMl;

namespace Converter.Ir;

public static class IrSerializer
{
    public static string SerializeBlock(IrBlock block, IReadOnlyList<NetworkSidecar> sidecars)
    {
        var sb = new StringBuilder();
        sb.Append("BLOCK ").Append(block.Kind).Append(' ').Append(block.Name).Append('\n');
        sb.Append("ROOTID ").Append(block.RootUId).Append('\n');
        sb.Append("NUMBER ").Append(block.Number).Append('\n');
        sb.Append("LANGUAGE ").Append(block.Language).Append('\n');
        if (!string.IsNullOrEmpty(block.Comment))
        {
            sb.Append("COMMENT \"").Append(EscapeString(block.Comment)).Append("\"\n");
        }

        if (block.StaticMembers is not null || block.TempMembers.Count > 0)
        {
            sb.Append('\n');
            SerializeInterface(sb, block.StaticMembers, block.TempMembers);
        }

        foreach (var network in block.Networks)
        {
            sb.Append('\n');
            SerializeNetwork(sb, network);
        }

        sb.Append("\nSIDECAR\n");
        foreach (var sidecar in sidecars)
        {
            SerializeSidecarNetwork(sb, sidecar);
        }

        return sb.ToString();
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
        if (network.IsEmpty)
        {
            sb.Append(" [empty]\n");
            return;
        }

        sb.Append('\n');
        foreach (var timer in network.Timers)
        {
            sb.Append("  TON(").Append(timer.InstancePath)
              .Append(", IN := ").Append(SerializeExpr(timer.In))
              .Append(", PT := ").Append(SerializeExpr(timer.Pt))
              .Append(")\n");
        }

        foreach (var assignment in network.Assignments)
        {
            sb.Append("  COIL ").Append(assignment.CoilTag).Append(" := ").Append(SerializeExpr(assignment.Condition)).Append('\n');
        }

        foreach (var move in network.Moves)
        {
            sb.Append("  MOVE(EN := ").Append(SerializeExpr(move.En))
              .Append(", IN := ").Append(SerializeExpr(move.In))
              .Append(") => ").Append(move.DestTag).Append('\n');
        }

        foreach (var wordAnd in network.WordAnds)
        {
            sb.Append("  WAND(EN := ").Append(SerializeExpr(wordAnd.En));
            for (var k = 0; k < wordAnd.Inputs.Count; k++)
            {
                sb.Append(", IN").Append(k + 1).Append(" := ").Append(SerializeExpr(wordAnd.Inputs[k]));
            }

            sb.Append(") => ").Append(wordAnd.DestTag).Append('\n');
        }

        foreach (var call in network.Calls)
        {
            sb.Append("  CALL ").Append(call.BlockName).Append('(').Append(call.InstancePath)
              .Append(", EN := ").Append(SerializeExpr(call.En));

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
    }

    // Only emitted when there's real content — matches every FC seen (StaticMembers null,
    // TempMembers empty), where the whole INTERFACE section is omitted per the "absence means
    // default" convention used throughout this format. STATIC is its own optional subsection
    // (null distinguishes "no Static section in source at all" — an FC — from "an FB with an
    // empty one"); TEMP always shows when non-empty. Member-line grammar (VERSION/RETAIN/
    // SETPOINT/nested indentation) is identical to a DB's own MEMBERS section — DbMemberLineFormat
    // is shared between the two, confirmed real 2026-07-11 (S1 item 7 Phase B).
    private static void SerializeInterface(StringBuilder sb, IReadOnlyList<DbMember>? staticMembers, IReadOnlyList<DbMember> tempMembers)
    {
        sb.Append("INTERFACE\n");
        if (staticMembers is not null)
        {
            sb.Append("  STATIC\n");
            foreach (var member in staticMembers)
            {
                DbMemberLineFormat.SerializeLine(sb, "    ", member);
                if (member.NestedMembers is not null)
                {
                    foreach (var nested in member.NestedMembers)
                    {
                        DbMemberLineFormat.SerializeLine(sb, "      ", nested);
                    }
                }
            }
        }

        if (tempMembers.Count > 0)
        {
            sb.Append("  TEMP\n");
            foreach (var member in tempMembers)
            {
                DbMemberLineFormat.SerializeLine(sb, "    ", member);
            }
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
    private static string SerializeExpr(Expr expr) => expr switch
    {
        Expr.TagRef tagRef => tagRef.Path,
        Expr.Literal literal => literal.Value,
        Expr.Not not => $"NOT {Parenthesize(not.Operand, not.Operand is Expr.And or Expr.Or)}",
        Expr.And { Operands.Count: 0 } => "TRUE",
        Expr.And and => string.Join(" AND ", and.Operands.Select(op => Parenthesize(op, op is Expr.Or))),
        Expr.Or { Operands.Count: 0 } => "TRUE",
        Expr.Or or => string.Join(" OR ", or.Operands.Select(SerializeExpr)),
        Expr.Compare compare => $"{SerializeExpr(compare.Left)} {compare.Operator} {SerializeExpr(compare.Right)}",
        _ => throw new IrFormatException($"Unsupported expression node: {expr.GetType().Name}"),
    };

    private static string Parenthesize(Expr expr, bool needsParens) =>
        needsParens ? $"({SerializeExpr(expr)})" : SerializeExpr(expr);

    private static void SerializeSidecarNetwork(StringBuilder sb, NetworkSidecar sidecar)
    {
        sb.Append("NETWORK ").Append(sidecar.NetworkNumber).Append('\n');
        sb.Append("  compileunit = ").Append(sidecar.CompileUnitUId).Append('\n');
        foreach (var access in sidecar.AccessUIds)
        {
            sb.Append("  access ").Append(access.TagPath).Append(" = ").Append(access.UId).Append(' ').Append(access.Scope).Append('\n');
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

            sb.Append("    instanceuid = ").Append(call.InstanceUId).Append('\n');
            sb.Append("    instancescope = ").Append(call.InstanceScope).Append('\n');
            sb.Append("    instancepath = ").Append(string.Join('.', call.InstanceComponentPath)).Append('\n');

            for (var a = 0; a < call.Arguments.Count; a++)
            {
                SerializeCallArgument(sb, "    ", a, call.Arguments[a]);
            }
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

    private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
