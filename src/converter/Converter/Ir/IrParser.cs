using System.Text.RegularExpressions;
using Converter.SimaticMl;

namespace Converter.Ir;

public static partial class IrParser
{
    public static (IrBlock Block, IReadOnlyList<NetworkSidecar> Sidecars) ParseBlock(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var i = 0;

        var blockLine = RequireLine(lines, ref i);
        var blockMatch = BlockLineRegex().Match(blockLine);
        if (!blockMatch.Success)
        {
            throw new IrFormatException($"Expected 'BLOCK <Kind> <Name>', got: '{blockLine}'");
        }

        var kind = blockMatch.Groups["kind"].Value;
        var name = blockMatch.Groups["name"].Value;

        var rootUId = RequirePrefixedLine(lines, ref i, "ROOTID ");
        var number = int.Parse(RequirePrefixedLine(lines, ref i, "NUMBER "));
        var language = RequirePrefixedLine(lines, ref i, "LANGUAGE ");

        string? comment = null;
        if (i < lines.Length && lines[i].StartsWith("COMMENT \"", StringComparison.Ordinal))
        {
            comment = ParseQuotedString(lines[i]["COMMENT ".Length..]);
            i++;
        }

        var (staticMembers, tempMembers) = ParseInterface(lines, ref i);

        var networks = new List<IrNetwork>();
        while (i < lines.Length && lines[i] != "SIDECAR")
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                i++;
                continue;
            }

            networks.Add(ParseNetwork(lines, ref i));
        }

        if (i >= lines.Length || lines[i] != "SIDECAR")
        {
            throw new IrFormatException("Expected a 'SIDECAR' section after the last network.");
        }

        i++;

        var sidecars = new List<NetworkSidecar>();
        while (i < lines.Length)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                i++;
                continue;
            }

            sidecars.Add(ParseSidecarNetwork(lines, ref i));
        }

        return (new IrBlock(rootUId, kind, name, number, language, comment, networks, staticMembers, tempMembers), sidecars);
    }

    // Optional — only present when the source had real Static/Temp content (S1 item 7 Phase B,
    // 2026-07-11). A single blank-line separator precedes "INTERFACE", same convention as the
    // one preceding each NETWORK — skipped here rather than left for the caller, since this is
    // the one place that separator has a specific fixed follower to check for.
    private static (IReadOnlyList<DbMember>? StaticMembers, IReadOnlyList<DbMember> TempMembers) ParseInterface(string[] lines, ref int i)
    {
        var lookahead = i;
        if (lookahead < lines.Length && string.IsNullOrWhiteSpace(lines[lookahead]))
        {
            lookahead++;
        }

        if (lookahead >= lines.Length || lines[lookahead] != "INTERFACE")
        {
            return (null, Array.Empty<DbMember>());
        }

        i = lookahead + 1;

        IReadOnlyList<DbMember>? staticMembers = null;
        if (i < lines.Length && lines[i] == "  STATIC")
        {
            i++;
            var parsed = new List<DbMember>();
            while (i < lines.Length && lines[i].StartsWith("    ", StringComparison.Ordinal) && !lines[i].StartsWith("      ", StringComparison.Ordinal))
            {
                var member = DbMemberLineFormat.ParseLine(lines[i], "    ");
                i++;

                var nestedMembers = new List<DbMember>();
                while (i < lines.Length && lines[i].StartsWith("      ", StringComparison.Ordinal))
                {
                    nestedMembers.Add(DbMemberLineFormat.ParseLine(lines[i], "      "));
                    i++;
                }

                parsed.Add(nestedMembers.Count > 0 ? member with { NestedMembers = nestedMembers } : member);
            }

            staticMembers = parsed;
        }

        var tempMembers = new List<DbMember>();
        if (i < lines.Length && lines[i] == "  TEMP")
        {
            i++;
            while (i < lines.Length && lines[i].StartsWith("    ", StringComparison.Ordinal))
            {
                tempMembers.Add(DbMemberLineFormat.ParseLine(lines[i], "    "));
                i++;
            }
        }

        return (staticMembers, tempMembers);
    }

    public static IrNetwork ParseNetworkOnly(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var i = 0;
        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
        {
            i++;
        }

        return ParseNetwork(lines, ref i);
    }

    private static IrNetwork ParseNetwork(string[] lines, ref int i)
    {
        var headerMatch = NetworkLineRegex().Match(RequireLine(lines, ref i));
        if (!headerMatch.Success)
        {
            throw new IrFormatException($"Expected 'NETWORK <n> \"<title>\"' at line {i}.");
        }

        var number = int.Parse(headerMatch.Groups["number"].Value);
        var title = UnescapeString(headerMatch.Groups["title"].Value);

        if (headerMatch.Groups["empty"].Success)
        {
            return new IrNetwork(number, title, Array.Empty<CoilAssignment>());
        }

        // Timers are always emitted before coil assignments (IrSerializer) — parsed in the same
        // order for self-stability.
        var timers = new List<TimerBinding>();
        while (i < lines.Length && lines[i].StartsWith("  TON(", StringComparison.Ordinal))
        {
            var tonMatch = TonLineRegex().Match(lines[i]);
            if (!tonMatch.Success)
            {
                throw new IrFormatException($"Expected '  TON(<path>, IN := <expr>, PT := <expr>)', got: '{lines[i]}'");
            }

            var inExpr = ParseExpr(tonMatch.Groups["in"].Value);
            var ptExpr = ParseExprTerm(tonMatch.Groups["pt"].Value);
            timers.Add(new TimerBinding(tonMatch.Groups["path"].Value, inExpr, ptExpr));
            i++;
        }

        var assignments = new List<CoilAssignment>();
        while (i < lines.Length && lines[i].StartsWith("  COIL ", StringComparison.Ordinal))
        {
            var coilMatch = CoilLineRegex().Match(lines[i]);
            if (!coilMatch.Success)
            {
                throw new IrFormatException($"Expected '  COIL <tag> := <expr>', got: '{lines[i]}'");
            }

            assignments.Add(new CoilAssignment(coilMatch.Groups["tag"].Value, ParseExpr(coilMatch.Groups["expr"].Value)));
            i++;
        }

        // Moves are always emitted last (IrSerializer, after Timers/Coils) — parsed in the same
        // order for self-stability.
        var moves = new List<MoveStatement>();
        while (i < lines.Length && lines[i].StartsWith("  MOVE(", StringComparison.Ordinal))
        {
            var moveMatch = MoveLineRegex().Match(lines[i]);
            if (!moveMatch.Success)
            {
                throw new IrFormatException($"Expected '  MOVE(EN := <expr>, IN := <expr>) => <dest>', got: '{lines[i]}'");
            }

            var enExpr = ParseExpr(moveMatch.Groups["en"].Value);
            var inExpr = ParseExprTerm(moveMatch.Groups["in"].Value);
            moves.Add(new MoveStatement(enExpr, inExpr, moveMatch.Groups["dest"].Value));
            i++;
        }

        if (assignments.Count == 0 && timers.Count == 0 && moves.Count == 0)
        {
            throw new IrFormatException($"Network {number} has no COIL/TON/MOVE statements and isn't marked [empty].");
        }

        return new IrNetwork(number, title, assignments, timers, moves);
    }

    // Top-level entry point: OR is the loosest binder. "TRUE" is only meaningful here (the
    // "wired directly to rail — always on" sentinel for an empty AND/OR — ir/SPEC.md), never as
    // a general primary nested inside a compound expression.
    private static Expr ParseExpr(string text)
    {
        text = text.Trim();
        if (text == "TRUE")
        {
            return new Expr.And(Array.Empty<Expr>());
        }

        var pos = 0;
        var expr = ParseOrExpr(text, ref pos);
        if (pos != text.Length)
        {
            throw new IrFormatException($"Unexpected trailing content in expression '{text}' at position {pos}.");
        }

        return expr;
    }

    // A single term — no AND/OR at this level — used for contexts that are never a boolean
    // chain (a TON's PT, a Move's IN): still NOT/comparison/parens-aware for consistency, since
    // nothing about those contexts rules it out, even though no real source has needed it yet.
    private static Expr ParseExprTerm(string text)
    {
        text = text.Trim();
        var pos = 0;
        var expr = ParseUnaryExpr(text, ref pos);
        if (pos != text.Length)
        {
            throw new IrFormatException($"Unexpected trailing content in expression '{text}' at position {pos}.");
        }

        return expr;
    }

    // AND binds tighter than OR (standard precedence, confirmed with the project owner,
    // 2026-07-11, S1 item 11 — mirrors IrSerializer.SerializeExpr's own precedence note). Real
    // recursive descent, not the old naive substring split: that approach never correctly
    // handled mixed AND+OR (it always matched " AND " first regardless of an OR's lower
    // precedence) — it just never got exercised by anything more complex than a single flat
    // AND-chain or OR-of-single-leaves until OR-merge branches became compound expressions.
    private static Expr ParseOrExpr(string text, ref int pos)
    {
        var operands = new List<Expr> { ParseAndExpr(text, ref pos) };
        while (TryConsumeToken(text, ref pos, " OR "))
        {
            operands.Add(ParseAndExpr(text, ref pos));
        }

        return operands.Count == 1 ? operands[0] : new Expr.Or(operands);
    }

    private static Expr ParseAndExpr(string text, ref int pos)
    {
        var operands = new List<Expr> { ParseUnaryExpr(text, ref pos) };
        while (TryConsumeToken(text, ref pos, " AND "))
        {
            operands.Add(ParseUnaryExpr(text, ref pos));
        }

        return operands.Count == 1 ? operands[0] : new Expr.And(operands);
    }

    // NOT binds tighter than AND/OR — "NOT A AND B" is "(NOT A) AND B", not "NOT (A AND B)"
    // (matches IrSerializer.SerializeExpr's own parenthesization rule for Not-wrapping-And/Or).
    private static Expr ParseUnaryExpr(string text, ref int pos)
    {
        if (TryConsumeToken(text, ref pos, "NOT "))
        {
            return new Expr.Not(ParseUnaryExpr(text, ref pos));
        }

        return ParsePrimaryExpr(text, ref pos);
    }

    // A parenthesized group (recurses to the top of the grammar) or a comparison-or-leaf.
    private static Expr ParsePrimaryExpr(string text, ref int pos)
    {
        if (pos < text.Length && text[pos] == '(')
        {
            pos++;
            var inner = ParseOrExpr(text, ref pos);
            if (pos >= text.Length || text[pos] != ')')
            {
                throw new IrFormatException($"Expected ')' in expression '{text}' at position {pos}.");
            }

            pos++;
            return inner;
        }

        return ParseComparisonOrLeaf(text, ref pos);
    }

    // Longer operators first so e.g. ">=" is never mistaken for a "=" search hitting inside it —
    // in practice the exact character sequences never actually overlap (see ComparisonTokens'
    // own note), but ordering longest-first is the safer, more obviously-correct habit anyway.
    // Only "=" and ">=" (Eq/Ge) are ever emitted by this converter today; the rest are recognized
    // here because they're already part of the documented IR grammar (`ir/SPEC.md`'s
    // readable-form table), not because the converter builds networks that use them — Ne/Le/Gt/Lt
    // remain hard errors at the SimaticML level (FlgNetParser's SupportedComparisonPartNames).
    private static readonly (string Operator, string Token)[] ComparisonTokens =
    {
        (">=", " >= "), ("<=", " <= "), ("<>", " <> "),
        ("=", " = "), (">", " > "), ("<", " < "),
    };

    // Consumes everything up to (but not including) the next top-level " AND "/" OR "/")" — a
    // primary never contains a literal '(' or ')' itself (tag paths/literals don't), so a single
    // forward scan is enough, no paren-depth tracking needed at this level (ParsePrimaryExpr
    // already branched off actual parenthesized groups before reaching here). The comparison
    // scan then runs on that raw span, exactly like the pre-S1-item-11 ParseExprTerm did.
    private static Expr ParseComparisonOrLeaf(string text, ref int pos)
    {
        var start = pos;
        while (pos < text.Length && text[pos] != ')' && !MatchesAt(text, pos, " AND ") && !MatchesAt(text, pos, " OR "))
        {
            pos++;
        }

        var raw = text[start..pos].Trim();
        if (raw.Length == 0)
        {
            throw new IrFormatException($"Expected an expression in '{text}' at position {start}.");
        }

        foreach (var (op, token) in ComparisonTokens)
        {
            var index = raw.IndexOf(token, StringComparison.Ordinal);
            if (index >= 0)
            {
                return new Expr.Compare(op, ParseLeaf(raw[..index]), ParseLeaf(raw[(index + token.Length)..]));
            }
        }

        return ParseLeaf(raw);
    }

    private static bool MatchesAt(string text, int pos, string token) =>
        pos + token.Length <= text.Length && string.CompareOrdinal(text, pos, token, 0, token.Length) == 0;

    private static bool TryConsumeToken(string text, ref int pos, string token)
    {
        if (!MatchesAt(text, pos, token))
        {
            return false;
        }

        pos += token.Length;
        return true;
    }

    // A literal (e.g. "T#100MS" or a bare integer like "1"/"-1") is recognized by shape rather
    // than by consulting the sidecar, so the IR text alone stays unambiguous to a reader. "T#"
    // is the existing time-literal convention (confirmed real, 2026-07-11, FB MotorDOL's TON PT);
    // a bare (optionally negative) integer is a comparison operand (FC ControlDelays) — safe to
    // recognize this way since a real tag path is never purely numeric (06-lad-conventions.md
    // C-005: starts with a letter).
    private static Expr ParseLeaf(string text)
    {
        text = text.Trim();
        if (text.StartsWith("T#", StringComparison.Ordinal) || IntegerLiteralRegex().IsMatch(text))
        {
            return new Expr.Literal(text);
        }

        return new Expr.TagRef(text);
    }

    private static NetworkSidecar ParseSidecarNetwork(string[] lines, ref int i)
    {
        var headerMatch = SidecarNetworkLineRegex().Match(RequireLine(lines, ref i));
        if (!headerMatch.Success)
        {
            throw new IrFormatException($"Expected 'NETWORK <n>' in SIDECAR at line {i}.");
        }

        var number = int.Parse(headerMatch.Groups["number"].Value);

        if (i >= lines.Length || !lines[i].StartsWith("  compileunit = ", StringComparison.Ordinal))
        {
            throw new IrFormatException($"Expected '  compileunit = <uid>' in SIDECAR for network {number}.");
        }

        var compileUnitUId = lines[i]["  compileunit = ".Length..].Trim();
        i++;

        var accessEntries = new List<SidecarAccessEntry>();
        while (i < lines.Length && lines[i].StartsWith("  access ", StringComparison.Ordinal))
        {
            var match = SidecarAccessLineRegex().Match(lines[i]);
            if (!match.Success)
            {
                throw new IrFormatException($"Malformed sidecar access line: '{lines[i]}'");
            }

            accessEntries.Add(new SidecarAccessEntry(match.Groups["path"].Value, int.Parse(match.Groups["uid"].Value), match.Groups["scope"].Value));
            i++;
        }

        var constantEntries = new List<SidecarConstantEntry>();
        while (i < lines.Length && lines[i].StartsWith("  constant ", StringComparison.Ordinal))
        {
            var match = SidecarConstantLineRegex().Match(lines[i]);
            if (!match.Success)
            {
                throw new IrFormatException($"Malformed sidecar constant line: '{lines[i]}'");
            }

            var constantType = match.Groups["type"].Value == "none" ? null : match.Groups["type"].Value;
            constantEntries.Add(new SidecarConstantEntry(match.Groups["value"].Value, int.Parse(match.Groups["uid"].Value), constantType));
            i++;
        }

        var timers = new List<TimerBindingSidecar>();
        while (i < lines.Length && TimerHeaderRegex().IsMatch(lines[i]))
        {
            timers.Add(ParseTimerSidecar(lines, ref i, number));
        }

        var assignments = new List<CoilAssignmentSidecar>();
        while (i < lines.Length && AssignmentHeaderRegex().IsMatch(lines[i]))
        {
            i++;

            if (i >= lines.Length || !lines[i].StartsWith("    rail = ", StringComparison.Ordinal))
            {
                throw new IrFormatException($"Expected '    rail = <uid|none>' in SIDECAR for network {number}.");
            }

            var railWireUId = ParseRail(lines[i]["    rail = ".Length..].Trim());
            i++;

            var steps = new List<ChainStepSidecar>();
            var s = 0;
            while (i < lines.Length && IsStepHeader(lines[i], "    ", $"step {s}"))
            {
                steps.Add(ParseStep(lines, ref i, "    ", $"step {s}"));
                s++;
            }

            if (i >= lines.Length || !lines[i].StartsWith("    coil = ", StringComparison.Ordinal))
            {
                throw new IrFormatException($"Expected '    coil = <uid>' in SIDECAR for network {number}.");
            }

            var coilUId = int.Parse(lines[i]["    coil = ".Length..].Trim());
            i++;

            if (i >= lines.Length || !lines[i].StartsWith("    coil operand = ", StringComparison.Ordinal))
            {
                throw new IrFormatException($"Expected '    coil operand = <uid>' in SIDECAR for network {number}.");
            }

            var coilOperandAccessUId = int.Parse(lines[i]["    coil operand = ".Length..].Trim());
            i++;

            if (i >= lines.Length || !lines[i].StartsWith("    coil operandwire = ", StringComparison.Ordinal))
            {
                throw new IrFormatException($"Expected '    coil operandwire = <uid>' in SIDECAR for network {number}.");
            }

            var coilOperandWireUId = int.Parse(lines[i]["    coil operandwire = ".Length..].Trim());
            i++;

            assignments.Add(new CoilAssignmentSidecar(railWireUId, steps, coilUId, coilOperandAccessUId, coilOperandWireUId));
        }

        var moves = new List<MoveStatementSidecar>();
        while (i < lines.Length && MoveHeaderRegex().IsMatch(lines[i]))
        {
            moves.Add(ParseMoveSidecar(lines, ref i, number));
        }

        return new NetworkSidecar(number, compileUnitUId, accessEntries, assignments, constantEntries, timers, moves);
    }

    // A Move's own sidecar shape mirrors ParseTimerSidecar's exactly — same rail/steps mechanism,
    // just terminating at `in`/`dest` (out1) instead of `preset`/(no destination).
    private static MoveStatementSidecar ParseMoveSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  move <n>" header — index itself isn't needed, position in the list is enough.

        var movePartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    moveuid = "));
        var railWireUId = ParseRail(RequirePrefixedLine(lines, ref i, "    rail = "));

        var steps = new List<ChainStepSidecar>();
        var s = 0;
        while (i < lines.Length && IsStepHeader(lines[i], "    ", $"step {s}"))
        {
            steps.Add(ParseStep(lines, ref i, "    ", $"step {s}"));
            s++;
        }

        var inOperand = ParseOperand(lines, ref i, "    ", "in");

        var destAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    dest = "));
        var destWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    destwire = "));

        return new MoveStatementSidecar(movePartUId, railWireUId, steps, inOperand, destAccessUId, destWireUId);
    }

    private static TimerBindingSidecar ParseTimerSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  timer <n>" header — index itself isn't needed, position in the list is enough.

        var tonPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    tonpartuid = "));
        var version = RequirePrefixedLine(lines, ref i, "    version = ");
        var timeType = RequirePrefixedLine(lines, ref i, "    timetype = ");
        var instanceUId = int.Parse(RequirePrefixedLine(lines, ref i, "    instanceuid = "));
        var instanceScope = RequirePrefixedLine(lines, ref i, "    instancescope = ");
        var instancePath = RequirePrefixedLine(lines, ref i, "    instancepath = ").Split('.');
        var railWireUId = ParseRail(RequirePrefixedLine(lines, ref i, "    rail = "));

        var steps = new List<ChainStepSidecar>();
        var s = 0;
        while (i < lines.Length && IsStepHeader(lines[i], "    ", $"step {s}"))
        {
            steps.Add(ParseStep(lines, ref i, "    ", $"step {s}"));
            s++;
        }

        if (i >= lines.Length)
        {
            throw new IrFormatException($"Expected a 'preset' line in SIDECAR for network {networkNumber}'s timer.");
        }

        var preset = ParseOperand(lines, ref i, "    ", "preset");

        OpenConnectionSidecar? et = null;
        if (i < lines.Length && lines[i].StartsWith("    et = ", StringComparison.Ordinal))
        {
            var parts = lines[i]["    et = ".Length..].Split(' ');
            et = new OpenConnectionSidecar(int.Parse(parts[0]), int.Parse(parts[1]));
            i++;
        }

        return new TimerBindingSidecar(
            tonPartUId, version, timeType, instanceUId, instanceScope, instancePath, railWireUId, steps, preset, et);
    }

    private static bool IsStepHeader(string line, string indent, string label) =>
        line == indent + label + " contact" || line == indent + label + " or"
        || line == indent + label + " timeroutput" || line == indent + label + " compare";

    private static ChainStepSidecar ParseStep(string[] lines, ref int i, string indent, string label)
    {
        var header = RequireLine(lines, ref i);
        var contactHeader = indent + label + " contact";
        var orHeader = indent + label + " or";
        var timerOutputHeader = indent + label + " timeroutput";
        var compareHeader = indent + label + " compare";
        if (header == contactHeader)
        {
            return ParseContactStepBody(lines, ref i, indent + "  ");
        }

        if (header == orHeader)
        {
            return ParseOrStepBody(lines, ref i, indent + "  ");
        }

        if (header == timerOutputHeader)
        {
            return ParseTimerOutputStepBody(lines, ref i, indent + "  ");
        }

        if (header == compareHeader)
        {
            return ParseCompareStepBody(lines, ref i, indent + "  ");
        }

        throw new IrFormatException($"Expected '{contactHeader}', '{orHeader}', '{timerOutputHeader}', or '{compareHeader}', got: '{header}'");
    }

    private static ChainStepSidecar.TimerOutputStep ParseTimerOutputStepBody(string[] lines, ref int i, string indent)
    {
        var tonPartUId = int.Parse(RequirePrefixedLine(lines, ref i, indent + "tonpartuid = "));
        var port = RequirePrefixedLine(lines, ref i, indent + "port = ");
        var outWire = int.Parse(RequirePrefixedLine(lines, ref i, indent + "out = "));
        return new ChainStepSidecar.TimerOutputStep(tonPartUId, port, outWire);
    }

    private static ChainStepSidecar.CompareStep ParseCompareStepBody(string[] lines, ref int i, string indent)
    {
        var partName = RequirePrefixedLine(lines, ref i, indent + "partname = ");
        var uid = int.Parse(RequirePrefixedLine(lines, ref i, indent + "uid = "));
        var srcType = RequirePrefixedLine(lines, ref i, indent + "srctype = ");
        var left = ParseOperand(lines, ref i, indent, "left");
        var right = ParseOperand(lines, ref i, indent, "right");
        var outWire = int.Parse(RequirePrefixedLine(lines, ref i, indent + "out = "));
        return new ChainStepSidecar.CompareStep(uid, partName, srcType, left, right, outWire);
    }

    // Shared tag-or-literal operand parsing — the inverse of IrSerializer.SerializeOperand, used
    // by a TON's PT and a comparison's left/right operand alike.
    private static OperandSidecar ParseOperand(string[] lines, ref int i, string indent, string label)
    {
        var tagPrefix = indent + label + " tag = ";
        var literalPrefix = indent + label + " literal = ";
        if (i < lines.Length && lines[i].StartsWith(tagPrefix, StringComparison.Ordinal))
        {
            var parts = lines[i][tagPrefix.Length..].Split(' ');
            i++;
            return new OperandSidecar.TagOperand(int.Parse(parts[0]), int.Parse(parts[1]));
        }

        if (i < lines.Length && lines[i].StartsWith(literalPrefix, StringComparison.Ordinal))
        {
            var parts = lines[i][literalPrefix.Length..].Split(' ');
            i++;
            return new OperandSidecar.LiteralOperand(int.Parse(parts[0]), int.Parse(parts[1]));
        }

        throw new IrFormatException(
            $"Expected '{tagPrefix}<uid> <wire>' or '{literalPrefix}<uid> <wire>', got: '{(i < lines.Length ? lines[i] : "<end of input>")}'");
    }

    private static int? ParseRail(string text)
    {
        text = text.Trim();
        return text == "none" ? null : int.Parse(text);
    }

    private static ChainStepSidecar.ContactStep ParseContactStepBody(string[] lines, ref int i, string indent)
    {
        var uid = int.Parse(RequirePrefixedLine(lines, ref i, indent + "uid = "));
        var operand = int.Parse(RequirePrefixedLine(lines, ref i, indent + "operand = "));
        var operandWire = int.Parse(RequirePrefixedLine(lines, ref i, indent + "operandwire = "));
        var negated = RequirePrefixedLine(lines, ref i, indent + "negated = ").Trim() == "true";
        var outWire = int.Parse(RequirePrefixedLine(lines, ref i, indent + "out = "));
        return new ChainStepSidecar.ContactStep(uid, operand, operandWire, negated, outWire);
    }

    private static ChainStepSidecar.OrStep ParseOrStepBody(string[] lines, ref int i, string indent)
    {
        var uid = int.Parse(RequirePrefixedLine(lines, ref i, indent + "uid = "));

        var branches = new List<OrBranch>();
        var b = 0;
        while (i < lines.Length && lines[i] == indent + $"branch {b}")
        {
            branches.Add(ParseOrBranch(lines, ref i, indent, $"branch {b}"));
            b++;
        }

        if (branches.Count == 0)
        {
            throw new IrFormatException("OR-merge step has no branches.");
        }

        var outWire = int.Parse(RequirePrefixedLine(lines, ref i, indent + "out = "));
        return new ChainStepSidecar.OrStep(uid, branches, outWire);
    }

    // The inverse of IrSerializer.SerializeOrBranch — a branch is an ordinary mini-chain (S1
    // item 11), same rail/steps shape as ParseTimerSidecar/ParseMoveSidecar's own top-level
    // pattern, just nested here instead of at the sidecar network's own top level.
    private static OrBranch ParseOrBranch(string[] lines, ref int i, string indent, string label)
    {
        var header = RequireLine(lines, ref i);
        if (header != indent + label)
        {
            throw new IrFormatException($"Expected '{indent}{label}', got: '{header}'");
        }

        var railWireUId = ParseRail(RequirePrefixedLine(lines, ref i, indent + "  rail = "));

        var steps = new List<ChainStepSidecar>();
        var s = 0;
        while (i < lines.Length && IsStepHeader(lines[i], indent + "  ", $"step {s}"))
        {
            steps.Add(ParseStep(lines, ref i, indent + "  ", $"step {s}"));
            s++;
        }

        return new OrBranch(steps, railWireUId);
    }

    private static string RequireLine(string[] lines, ref int i)
    {
        if (i >= lines.Length)
        {
            throw new IrFormatException("Unexpected end of IR text.");
        }

        return lines[i++];
    }

    private static string RequirePrefixedLine(string[] lines, ref int i, string prefix)
    {
        var line = RequireLine(lines, ref i);
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new IrFormatException($"Expected line starting with '{prefix}', got: '{line}'");
        }

        return line[prefix.Length..];
    }

    private static string ParseQuotedString(string raw)
    {
        raw = raw.Trim();
        if (raw.Length < 2 || raw[0] != '"' || raw[^1] != '"')
        {
            throw new IrFormatException($"Expected a quoted string, got: '{raw}'");
        }

        return UnescapeString(raw[1..^1]);
    }

    private static string UnescapeString(string value) => value.Replace("\\\"", "\"").Replace("\\\\", "\\");

    [GeneratedRegex(@"^BLOCK (?<kind>\S+) (?<name>\S+)$")]
    private static partial Regex BlockLineRegex();

    [GeneratedRegex("^NETWORK (?<number>\\d+) \"(?<title>(?:[^\"\\\\]|\\\\.)*)\"(?<empty> \\[empty\\])?$")]
    private static partial Regex NetworkLineRegex();

    [GeneratedRegex(@"^  COIL (?<tag>\S+) := (?<expr>.+)$")]
    private static partial Regex CoilLineRegex();

    [GeneratedRegex(@"^  TON\((?<path>[^,]+), IN := (?<in>.+), PT := (?<pt>.+)\)$")]
    private static partial Regex TonLineRegex();

    [GeneratedRegex(@"^  MOVE\(EN := (?<en>.+), IN := (?<in>.+)\) => (?<dest>\S+)$")]
    private static partial Regex MoveLineRegex();

    [GeneratedRegex(@"^NETWORK (?<number>\d+)$")]
    private static partial Regex SidecarNetworkLineRegex();

    [GeneratedRegex(@"^  access (?<path>\S+) = (?<uid>\d+) (?<scope>\S+)$")]
    private static partial Regex SidecarAccessLineRegex();

    [GeneratedRegex(@"^  constant (?<value>\S+) = (?<uid>\d+) (?<type>\S+)$")]
    private static partial Regex SidecarConstantLineRegex();

    [GeneratedRegex(@"^-?\d+$")]
    private static partial Regex IntegerLiteralRegex();

    [GeneratedRegex(@"^  timer (?<index>\d+)$")]
    private static partial Regex TimerHeaderRegex();

    [GeneratedRegex(@"^  assignment (?<index>\d+)$")]
    private static partial Regex AssignmentHeaderRegex();

    [GeneratedRegex(@"^  move (?<index>\d+)$")]
    private static partial Regex MoveHeaderRegex();
}
