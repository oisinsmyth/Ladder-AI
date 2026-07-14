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

        // SecondaryType (S1, 2026-07-14) — required by Openness for an OB specifically
        // (e.g. "ProgramCycle" for OB1 Main); never present on FC/FB. Parsed right after
        // LANGUAGE, matching IrSerializer's own emission order.
        string? secondaryType = null;
        if (i < lines.Length && lines[i].StartsWith("SECONDARYTYPE ", StringComparison.Ordinal))
        {
            secondaryType = lines[i]["SECONDARYTYPE ".Length..];
            i++;
        }

        // Block-level Title (S1 item 17, 2026-07-12) — mirrors Comment's own optional-line
        // handling, parsed first to match IrSerializer's own TITLE-then-COMMENT ordering.
        string? title = null;
        if (i < lines.Length && lines[i].StartsWith("TITLE \"", StringComparison.Ordinal))
        {
            title = ParseQuotedString(lines[i]["TITLE ".Length..]);
            i++;
        }

        string? comment = null;
        if (i < lines.Length && lines[i].StartsWith("COMMENT \"", StringComparison.Ordinal))
        {
            comment = ParseQuotedString(lines[i]["COMMENT ".Length..]);
            i++;
        }

        var (staticMembers, tempMembers, inputMembers, outputMembers, inOutMembers, constantMembers) = ParseInterface(lines, ref i);

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

        return (
            new IrBlock(
                rootUId, kind, name, number, language, comment, networks, staticMembers, tempMembers, title,
                inputMembers, outputMembers, inOutMembers, constantMembers, secondaryType),
            sidecars);
    }

    // Optional — only present when the source had real Interface content (Static/Temp since S1
    // item 7 Phase B, 2026-07-11; Input/Output/InOut/Constant since S1 item 20, 2026-07-12). A
    // single blank-line separator precedes "INTERFACE", same convention as the one preceding
    // each NETWORK — skipped here rather than left for the caller, since this is the one place
    // that separator has a specific fixed follower to check for. Section order matches
    // IrSerializer.SerializeInterface's own (Input, Output, InOut, Static, Temp, Constant).
    private static (
        IReadOnlyList<DbMember>? StaticMembers,
        IReadOnlyList<DbMember> TempMembers,
        IReadOnlyList<DbMember>? InputMembers,
        IReadOnlyList<DbMember>? OutputMembers,
        IReadOnlyList<DbMember> InOutMembers,
        IReadOnlyList<DbMember>? ConstantMembers) ParseInterface(string[] lines, ref int i)
    {
        var lookahead = i;
        if (lookahead < lines.Length && string.IsNullOrWhiteSpace(lines[lookahead]))
        {
            lookahead++;
        }

        if (lookahead >= lines.Length || lines[lookahead] != "INTERFACE")
        {
            return (null, Array.Empty<DbMember>(), null, null, Array.Empty<DbMember>(), null);
        }

        i = lookahead + 1;

        var inputMembers = ParseOptionalMemberSection(lines, ref i, "  INPUT");
        var outputMembers = ParseOptionalMemberSection(lines, ref i, "  OUTPUT");
        var inOutMembers = ParseOptionalMemberSection(lines, ref i, "  INOUT") ?? Array.Empty<DbMember>();

        IReadOnlyList<DbMember>? staticMembers = null;
        if (i < lines.Length && lines[i] == "  STATIC")
        {
            i++;
            var parsed = new List<DbMember>();
            while (i < lines.Length && lines[i].StartsWith("    ", StringComparison.Ordinal) && !lines[i].StartsWith("      ", StringComparison.Ordinal))
            {
                parsed.Add(DbMemberLineFormat.ParseMemberRecursive(lines, ref i, "    "));
            }

            staticMembers = parsed;
        }

        var tempMembers = ParseOptionalMemberSection(lines, ref i, "  TEMP") ?? Array.Empty<DbMember>();
        var constantMembers = ParseOptionalMemberSection(lines, ref i, "  CONSTANT");

        return (staticMembers, tempMembers, inputMembers, outputMembers, inOutMembers, constantMembers);
    }

    // Shared flat member-section parser for Input/Output/InOut/Constant — none of these ever
    // carries nested content in any grounded example (S1 item 20), unlike Static, so no
    // nested-member look-ahead is needed here (contrast the Static-specific loop above). Returns
    // null when the section header itself is absent, preserving the null-vs-present-but-empty
    // distinction IrSerializer.SerializeOptionalMemberSection relies on.
    private static IReadOnlyList<DbMember>? ParseOptionalMemberSection(string[] lines, ref int i, string header)
    {
        if (i >= lines.Length || lines[i] != header)
        {
            return null;
        }

        i++;
        var parsed = new List<DbMember>();
        while (i < lines.Length && lines[i].StartsWith("    ", StringComparison.Ordinal))
        {
            parsed.Add(DbMemberLineFormat.ParseLine(lines[i], "    "));
            i++;
        }

        return parsed;
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

        // Comment (S1 item 16) — an optional network-level COMMENT line, shown regardless of
        // [empty] (mirrors IrSerializer.SerializeNetwork's own placement).
        string? comment = null;
        if (i < lines.Length && lines[i].StartsWith("  COMMENT \"", StringComparison.Ordinal))
        {
            comment = ParseQuotedString(lines[i]["  COMMENT ".Length..]);
            i++;
        }

        if (headerMatch.Groups["empty"].Success)
        {
            return new IrNetwork(number, title, Array.Empty<CoilAssignment>(), Comment: comment);
        }

        // Timers are always emitted before coil assignments (IrSerializer) — parsed in the same
        // order for self-stability. TON/TONR/TOF (S1 items 19/23) share one loop, distinguished
        // by keyword — TONR's own 4th argument (R) is optional in the grammar's own arity check
        // but always present in practice (GraphReducer.ReduceTimer requires it whenever Kind is
        // Tonr), split on top-level commas like WAND/CALL/MUL's own variable-arity argument
        // lists (safe for the same reason: no Expr ever renders a literal comma). TOF shares
        // TON's own 3-argument arity exactly (no reset port, confirmed real).
        var timers = new List<TimerBinding>();
        while (i < lines.Length && (lines[i].StartsWith("  TON(", StringComparison.Ordinal)
            || lines[i].StartsWith("  TONR(", StringComparison.Ordinal) || lines[i].StartsWith("  TOF(", StringComparison.Ordinal)))
        {
            var tonMatch = TonLineRegex().Match(lines[i]);
            if (!tonMatch.Success)
            {
                throw new IrFormatException(
                    $"Expected '  TON(<path>, IN := <expr>, PT := <expr>)', '  TONR(<path>, IN := <expr>, PT := <expr>, R := <expr>)', " +
                    $"or '  TOF(<path>, IN := <expr>, PT := <expr>)', got: '{lines[i]}'");
            }

            var timerKind = tonMatch.Groups["kind"].Value switch
            {
                "TONR" => TimerKind.Tonr,
                "TOF" => TimerKind.Tof,
                _ => TimerKind.Ton,
            };
            var timerArgs = tonMatch.Groups["args"].Value.Split(", ", StringSplitOptions.None);
            if (timerArgs.Length is not (3 or 4) || !timerArgs[1].StartsWith("IN := ", StringComparison.Ordinal) || !timerArgs[2].StartsWith("PT := ", StringComparison.Ordinal))
            {
                throw new IrFormatException($"Expected '<path>, IN := <expr>, PT := <expr>[, R := <expr>]' inside TON/TONR/TOF(...), got: '{lines[i]}'");
            }

            var timerPath = timerArgs[0];
            var inExpr = ParseExpr(timerArgs[1]["IN := ".Length..]);
            var ptExpr = ParseExprTerm(timerArgs[2]["PT := ".Length..]);

            Expr? resetExpr = null;
            if (timerArgs.Length == 4)
            {
                if (timerKind != TimerKind.Tonr)
                {
                    throw new IrFormatException($"Only TONR takes a 4th (R) argument — '{lines[i]}' isn't TONR.");
                }

                if (!timerArgs[3].StartsWith("R := ", StringComparison.Ordinal))
                {
                    throw new IrFormatException($"Expected ', R := <expr>' as TONR's 4th argument, got: '{lines[i]}'");
                }

                resetExpr = ParseExprTerm(timerArgs[3]["R := ".Length..]);
            }

            timers.Add(new TimerBinding(timerPath, inExpr, ptExpr, timerKind, resetExpr));
            i++;
        }

        // COIL/SCOIL/RCOIL (S1 item 15) are parsed in one interleaved loop, in whatever order
        // they appear (matching how a real network naturally mixes assign/set/reset rungs) —
        // not three separate sections.
        var assignments = new List<CoilAssignment>();
        while (i < lines.Length && (lines[i].StartsWith("  COIL ", StringComparison.Ordinal)
            || lines[i].StartsWith("  SCOIL ", StringComparison.Ordinal)
            || lines[i].StartsWith("  RCOIL ", StringComparison.Ordinal)))
        {
            var coilMatch = CoilLineRegex().Match(lines[i]);
            if (!coilMatch.Success)
            {
                throw new IrFormatException($"Expected '  COIL|SCOIL|RCOIL <tag> := <expr>', got: '{lines[i]}'");
            }

            var kind = coilMatch.Groups["kind"].Value switch
            {
                "COIL" => CoilKind.Assign,
                "SCOIL" => CoilKind.Set,
                "RCOIL" => CoilKind.Reset,
                _ => throw new IrFormatException($"Unexpected coil keyword in '{lines[i]}'"),
            };

            assignments.Add(new CoilAssignment(coilMatch.Groups["tag"].Value, ParseExpr(coilMatch.Groups["expr"].Value), kind));
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

        // WordAnds are always emitted last (IrSerializer, after Timers/Coils/Moves) — parsed in
        // the same order for self-stability. Variable input count (Cardinality-driven, S1 item
        // 12), so the argument list is split on top-level commas rather than matched by a single
        // fixed-arity regex the way TON/MOVE's own fixed argument shapes are — safe because no
        // Expr ever renders a literal comma (AND/OR/NOT/parens/comparisons never use one).
        var wordAnds = new List<WordAndStatement>();
        while (i < lines.Length && lines[i].StartsWith("  WAND(", StringComparison.Ordinal))
        {
            var wandMatch = WordAndLineRegex().Match(lines[i]);
            if (!wandMatch.Success)
            {
                throw new IrFormatException($"Expected '  WAND(EN := <expr>, IN1 := <expr>, ...) => <dest>', got: '{lines[i]}'");
            }

            var args = wandMatch.Groups["args"].Value.Split(", ", StringSplitOptions.None);
            if (args.Length < 2 || !args[0].StartsWith("EN := ", StringComparison.Ordinal))
            {
                throw new IrFormatException($"Expected 'EN := <expr>' as WAND's first argument, got: '{lines[i]}'");
            }

            var wandEnExpr = ParseExpr(args[0]["EN := ".Length..]);

            var inputs = new List<Expr>();
            for (var k = 1; k < args.Length; k++)
            {
                var prefix = $"IN{k} := ";
                if (!args[k].StartsWith(prefix, StringComparison.Ordinal))
                {
                    throw new IrFormatException($"Expected '{prefix}<expr>' as WAND argument {k + 1}, got: '{args[k]}' in '{lines[i]}'");
                }

                inputs.Add(ParseExprTerm(args[k][prefix.Length..]));
            }

            wordAnds.Add(new WordAndStatement(wandEnExpr, inputs, wandMatch.Groups["dest"].Value));
            i++;
        }

        // Calls are always emitted last (IrSerializer, after Timers/Coils/Moves/WordAnds) —
        // parsed in the same order for self-stability. Instance is an optional leading positional
        // argument (no label, same convention as TON's own instance path) — omitted entirely when
        // the call has no instance (confirmed real, 2026-07-12, S1 item 24: an FC call, unlike
        // every FB call, carries no instance). EN is always present, even when trivially TRUE
        // (confirmed real, 2026-07-12, FC PlantAutoControl: all 20 real en's are directly rail-fed).
        // Disambiguated by checking whether the first argument itself starts with "EN := " — a
        // reserved prefix no real instance path could ever collide with (instance paths are bare
        // dotted tag-shaped text). The remaining arguments are a sparse, ordered mix of
        // "<Param> := <expr>" (Input) and "<Param> => <tag>" (Output) — split on top-level commas
        // same as WAND's own variable argument list (safe for the same reason: no Expr ever
        // renders a literal comma).
        var calls = new List<CallStatement>();
        while (i < lines.Length && lines[i].StartsWith("  CALL ", StringComparison.Ordinal))
        {
            var callMatch = CallLineRegex().Match(lines[i]);
            if (!callMatch.Success)
            {
                throw new IrFormatException($"Expected '  CALL <BlockName>([<instance>, ]EN := <expr>, ...)', got: '{lines[i]}'");
            }

            var blockName = callMatch.Groups["blockname"].Value;
            var args = callMatch.Groups["args"].Value.Split(", ", StringSplitOptions.None);

            string? instancePath;
            Expr callEnExpr;
            int enArgIndex;
            if (args.Length >= 1 && args[0].StartsWith("EN := ", StringComparison.Ordinal))
            {
                instancePath = null;
                callEnExpr = ParseExpr(args[0]["EN := ".Length..]);
                enArgIndex = 0;
            }
            else if (args.Length >= 2 && args[1].StartsWith("EN := ", StringComparison.Ordinal))
            {
                instancePath = args[0];
                callEnExpr = ParseExpr(args[1]["EN := ".Length..]);
                enArgIndex = 1;
            }
            else
            {
                throw new IrFormatException($"Expected '[<instance>, ]EN := <expr>' as CALL's leading argument(s), got: '{lines[i]}'");
            }

            var arguments = new List<CallArgument>();
            for (var k = enArgIndex + 1; k < args.Length; k++)
            {
                var inputSep = args[k].IndexOf(" := ", StringComparison.Ordinal);
                var outputSep = args[k].IndexOf(" => ", StringComparison.Ordinal);
                if (inputSep >= 0 && (outputSep < 0 || inputSep < outputSep))
                {
                    var paramName = args[k][..inputSep];
                    arguments.Add(new CallArgument.InputArg(paramName, ParseExprTerm(args[k][(inputSep + 4)..])));
                }
                else if (outputSep >= 0)
                {
                    var paramName = args[k][..outputSep];
                    arguments.Add(new CallArgument.OutputArg(paramName, args[k][(outputSep + 4)..]));
                }
                else
                {
                    throw new IrFormatException($"Expected '<param> := <expr>' or '<param> => <tag>' as CALL argument {k + 1}, got: '{args[k]}' in '{lines[i]}'");
                }
            }

            calls.Add(new CallStatement(blockName, instancePath, callEnExpr, arguments));
            i++;
        }

        // Muls are always emitted after Calls (IrSerializer) — parsed in the same order for
        // self-stability. MUL/ADD (S1 item 19) share one loop, distinguished by keyword — same
        // XML shape, different Part Name, mirroring TON/TONR's own treatment above. EN's own
        // value is either an ordinary expression or the reserved word "ENO" (confirmed real,
        // 2026-07-12, S1 item 18 — see EnSource's own doc comment), parsed via ParseEnSource
        // rather than ParseExpr directly.
        var muls = new List<MulStatement>();
        while (i < lines.Length && (lines[i].StartsWith("  MUL(", StringComparison.Ordinal) || lines[i].StartsWith("  ADD(", StringComparison.Ordinal)
            || lines[i].StartsWith("  SUB(", StringComparison.Ordinal) || lines[i].StartsWith("  DIV(", StringComparison.Ordinal)))
        {
            var mulMatch = MulLineRegex().Match(lines[i]);
            if (!mulMatch.Success)
            {
                throw new IrFormatException($"Expected '  MUL(EN := <expr-or-ENO>, IN1 := <expr>, ...) => <dest>' or 'ADD(...)'/'SUB(...)'/'DIV(...)', got: '{lines[i]}'");
            }

            var mulKind = mulMatch.Groups["kind"].Value switch
            {
                "ADD" => MulKind.Add,
                "SUB" => MulKind.Subtract,
                "DIV" => MulKind.Divide,
                _ => MulKind.Multiply,
            };
            var args = mulMatch.Groups["args"].Value.Split(", ", StringSplitOptions.None);
            if (args.Length < 2 || !args[0].StartsWith("EN := ", StringComparison.Ordinal))
            {
                throw new IrFormatException($"Expected 'EN := <expr-or-ENO>' as MUL/ADD/SUB/DIV's first argument, got: '{lines[i]}'");
            }

            var mulEn = ParseEnSource(args[0]["EN := ".Length..]);

            var mulInputs = new List<Expr>();
            for (var k = 1; k < args.Length; k++)
            {
                var prefix = $"IN{k} := ";
                if (!args[k].StartsWith(prefix, StringComparison.Ordinal))
                {
                    throw new IrFormatException($"Expected '{prefix}<expr>' as MUL/ADD/SUB/DIV argument {k + 1}, got: '{args[k]}' in '{lines[i]}'");
                }

                mulInputs.Add(ParseExprTerm(args[k][prefix.Length..]));
            }

            muls.Add(new MulStatement(mulEn, mulInputs, mulMatch.Groups["dest"].Value, mulKind));
            i++;
        }

        // Converts are always emitted after Muls (IrSerializer) — fixed arity (EN, IN), same
        // regex-based shape as MOVE's own line, not the split-on-commas approach MUL/WAND/CALL
        // need for their own variable-arity argument lists.
        var converts = new List<ConvertStatement>();
        while (i < lines.Length && lines[i].StartsWith("  CONVERT(", StringComparison.Ordinal))
        {
            var convertMatch = ConvertLineRegex().Match(lines[i]);
            if (!convertMatch.Success)
            {
                throw new IrFormatException($"Expected '  CONVERT(EN := <expr-or-ENO>, IN := <expr>) => <dest>', got: '{lines[i]}'");
            }

            var convertEn = ParseEnSource(convertMatch.Groups["en"].Value);
            var convertIn = ParseExprTerm(convertMatch.Groups["in"].Value);
            converts.Add(new ConvertStatement(convertEn, convertIn, convertMatch.Groups["dest"].Value));
            i++;
        }

        // Swaps are always emitted after Converts (IrSerializer) — same fixed-arity (EN, IN)
        // regex-based shape as CONVERT's own line, minus a DestType group.
        var swaps = new List<SwapStatement>();
        while (i < lines.Length && lines[i].StartsWith("  SWAP(", StringComparison.Ordinal))
        {
            var swapMatch = SwapLineRegex().Match(lines[i]);
            if (!swapMatch.Success)
            {
                throw new IrFormatException($"Expected '  SWAP(EN := <expr-or-ENO>, IN := <expr>) => <dest>', got: '{lines[i]}'");
            }

            var swapEn = ParseEnSource(swapMatch.Groups["en"].Value);
            var swapIn = ParseExprTerm(swapMatch.Groups["in"].Value);
            swaps.Add(new SwapStatement(swapEn, swapIn, swapMatch.Groups["dest"].Value));
            i++;
        }

        // Abs statements are always emitted after Swaps (IrSerializer) — same fixed-arity (EN, IN)
        // shape as SWAP's own line.
        var absStatements = new List<AbsStatement>();
        while (i < lines.Length && lines[i].StartsWith("  ABS(", StringComparison.Ordinal))
        {
            var absMatch = AbsLineRegex().Match(lines[i]);
            if (!absMatch.Success)
            {
                throw new IrFormatException($"Expected '  ABS(EN := <expr-or-ENO>, IN := <expr>) => <dest>', got: '{lines[i]}'");
            }

            var absEn = ParseEnSource(absMatch.Groups["en"].Value);
            var absIn = ParseExprTerm(absMatch.Groups["in"].Value);
            absStatements.Add(new AbsStatement(absEn, absIn, absMatch.Groups["dest"].Value));
            i++;
        }

        // Limits are always emitted after Abs statements (IrSerializer). Three fixed-named
        // inputs (MN/IN/MX), same top-level-comma-split discipline as WAND/CALL/MUL's own
        // variable-arity argument lists — safe for the same reason (no Expr ever renders a
        // literal comma).
        var limits = new List<LimitStatement>();
        while (i < lines.Length && lines[i].StartsWith("  LIMIT(", StringComparison.Ordinal))
        {
            var limitMatch = LimitLineRegex().Match(lines[i]);
            if (!limitMatch.Success)
            {
                throw new IrFormatException($"Expected '  LIMIT(EN := <expr-or-ENO>, MN := <expr>, IN := <expr>, MX := <expr>) => <dest>', got: '{lines[i]}'");
            }

            var limitArgs = limitMatch.Groups["args"].Value.Split(", ", StringSplitOptions.None);
            if (limitArgs.Length != 4 || !limitArgs[0].StartsWith("EN := ", StringComparison.Ordinal)
                || !limitArgs[1].StartsWith("MN := ", StringComparison.Ordinal)
                || !limitArgs[2].StartsWith("IN := ", StringComparison.Ordinal)
                || !limitArgs[3].StartsWith("MX := ", StringComparison.Ordinal))
            {
                throw new IrFormatException($"Expected 'EN := <expr>, MN := <expr>, IN := <expr>, MX := <expr>' inside LIMIT(...), got: '{lines[i]}'");
            }

            var limitEn = ParseEnSource(limitArgs[0]["EN := ".Length..]);
            var limitMin = ParseExprTerm(limitArgs[1]["MN := ".Length..]);
            var limitIn = ParseExprTerm(limitArgs[2]["IN := ".Length..]);
            var limitMax = ParseExprTerm(limitArgs[3]["MX := ".Length..]);
            limits.Add(new LimitStatement(limitEn, limitMin, limitIn, limitMax, limitMatch.Groups["dest"].Value));
            i++;
        }

        // T_SUBs are always emitted after Limits (IrSerializer). Two fixed-named inputs (IN1/IN2),
        // same top-level-comma-split discipline as LIMIT's own.
        var tSubs = new List<TSubStatement>();
        while (i < lines.Length && lines[i].StartsWith("  T_SUB(", StringComparison.Ordinal))
        {
            var tSubMatch = TSubLineRegex().Match(lines[i]);
            if (!tSubMatch.Success)
            {
                throw new IrFormatException($"Expected '  T_SUB(EN := <expr-or-ENO>, IN1 := <expr>, IN2 := <expr>) => <dest>', got: '{lines[i]}'");
            }

            var tSubArgs = tSubMatch.Groups["args"].Value.Split(", ", StringSplitOptions.None);
            if (tSubArgs.Length != 3 || !tSubArgs[0].StartsWith("EN := ", StringComparison.Ordinal)
                || !tSubArgs[1].StartsWith("IN1 := ", StringComparison.Ordinal)
                || !tSubArgs[2].StartsWith("IN2 := ", StringComparison.Ordinal))
            {
                throw new IrFormatException($"Expected 'EN := <expr>, IN1 := <expr>, IN2 := <expr>' inside T_SUB(...), got: '{lines[i]}'");
            }

            var tSubEn = ParseEnSource(tSubArgs[0]["EN := ".Length..]);
            var tSubIn1 = ParseExprTerm(tSubArgs[1]["IN1 := ".Length..]);
            var tSubIn2 = ParseExprTerm(tSubArgs[2]["IN2 := ".Length..]);
            tSubs.Add(new TSubStatement(tSubEn, tSubIn1, tSubIn2, tSubMatch.Groups["dest"].Value));
            i++;
        }

        // T_CONVs are always emitted after T_SUBs (IrSerializer) — same fixed-arity (EN, IN)
        // shape as CONVERT/SWAP/ABS's own line.
        var tConvs = new List<TConvStatement>();
        while (i < lines.Length && lines[i].StartsWith("  T_CONV(", StringComparison.Ordinal))
        {
            var tConvMatch = TConvLineRegex().Match(lines[i]);
            if (!tConvMatch.Success)
            {
                throw new IrFormatException($"Expected '  T_CONV(EN := <expr-or-ENO>, IN := <expr>) => <dest>', got: '{lines[i]}'");
            }

            var tConvEn = ParseEnSource(tConvMatch.Groups["en"].Value);
            var tConvIn = ParseExprTerm(tConvMatch.Groups["in"].Value);
            tConvs.Add(new TConvStatement(tConvEn, tConvIn, tConvMatch.Groups["dest"].Value));
            i++;
        }

        // Calcs are always emitted after T_CONVs (IrSerializer). Cardinality-driven inputs (same
        // top-level-comma-split discipline as MUL/WAND), plus a trailing quoted Equation string
        // kept outside the argument-list parens entirely (see IrSerializer's own comment).
        var calcs = new List<CalcStatement>();
        while (i < lines.Length && lines[i].StartsWith("  CALC(", StringComparison.Ordinal))
        {
            var calcMatch = CalcLineRegex().Match(lines[i]);
            if (!calcMatch.Success)
            {
                throw new IrFormatException($"Expected '  CALC(EN := <expr-or-ENO>, IN1 := <expr>, ...) => <dest> \"<equation>\"', got: '{lines[i]}'");
            }

            var calcArgs = calcMatch.Groups["args"].Value.Split(", ", StringSplitOptions.None);
            if (calcArgs.Length < 2 || !calcArgs[0].StartsWith("EN := ", StringComparison.Ordinal))
            {
                throw new IrFormatException($"Expected 'EN := <expr>' as CALC's first argument, got: '{lines[i]}'");
            }

            var calcEn = ParseEnSource(calcArgs[0]["EN := ".Length..]);

            var calcInputs = new List<Expr>();
            for (var k = 1; k < calcArgs.Length; k++)
            {
                var prefix = $"IN{k} := ";
                if (!calcArgs[k].StartsWith(prefix, StringComparison.Ordinal))
                {
                    throw new IrFormatException($"Expected '{prefix}<expr>' as CALC argument {k + 1}, got: '{calcArgs[k]}' in '{lines[i]}'");
                }

                calcInputs.Add(ParseExprTerm(calcArgs[k][prefix.Length..]));
            }

            var calcEquation = ParseQuotedString(calcMatch.Groups["equation"].Value);
            calcs.Add(new CalcStatement(calcEn, calcInputs, calcEquation, calcMatch.Groups["dest"].Value));
            i++;
        }

        // MOVE_BLK_VARIANTs are always emitted after Calcs (IrSerializer). No trailing "=> dest"
        // — two named outputs, not one, so everything (inputs and outputs alike) is inside one
        // top-level-comma-split argument list, disambiguated per-argument by ":=" vs "=>", same
        // mixing convention CALL's own argument list already established.
        var moveBlkVariants = new List<MoveBlkVariantStatement>();
        while (i < lines.Length && lines[i].StartsWith("  MOVE_BLK_VARIANT(", StringComparison.Ordinal))
        {
            var moveBlkVariantMatch = MoveBlkVariantLineRegex().Match(lines[i]);
            if (!moveBlkVariantMatch.Success)
            {
                throw new IrFormatException(
                    $"Expected '  MOVE_BLK_VARIANT(EN := <expr-or-ENO>, SRC := <expr>, COUNT := <expr>, SRC_INDEX := <expr>, " +
                    $"DEST_INDEX := <expr>, Ret_Val => <tag>, DEST => <tag>)', got: '{lines[i]}'");
            }

            var moveBlkVariantArgs = moveBlkVariantMatch.Groups["args"].Value.Split(", ", StringSplitOptions.None);
            if (moveBlkVariantArgs.Length != 7
                || !moveBlkVariantArgs[0].StartsWith("EN := ", StringComparison.Ordinal)
                || !moveBlkVariantArgs[1].StartsWith("SRC := ", StringComparison.Ordinal)
                || !moveBlkVariantArgs[2].StartsWith("COUNT := ", StringComparison.Ordinal)
                || !moveBlkVariantArgs[3].StartsWith("SRC_INDEX := ", StringComparison.Ordinal)
                || !moveBlkVariantArgs[4].StartsWith("DEST_INDEX := ", StringComparison.Ordinal)
                || !moveBlkVariantArgs[5].StartsWith("Ret_Val => ", StringComparison.Ordinal)
                || !moveBlkVariantArgs[6].StartsWith("DEST => ", StringComparison.Ordinal))
            {
                throw new IrFormatException(
                    "Expected 'EN := <expr>, SRC := <expr>, COUNT := <expr>, SRC_INDEX := <expr>, DEST_INDEX := <expr>, " +
                    $"Ret_Val => <tag>, DEST => <tag>' inside MOVE_BLK_VARIANT(...), got: '{lines[i]}'");
            }

            var moveBlkVariantEn = ParseEnSource(moveBlkVariantArgs[0]["EN := ".Length..]);
            var moveBlkVariantSrc = ParseExprTerm(moveBlkVariantArgs[1]["SRC := ".Length..]);
            var moveBlkVariantCount = ParseExprTerm(moveBlkVariantArgs[2]["COUNT := ".Length..]);
            var moveBlkVariantSrcIndex = ParseExprTerm(moveBlkVariantArgs[3]["SRC_INDEX := ".Length..]);
            var moveBlkVariantDestIndex = ParseExprTerm(moveBlkVariantArgs[4]["DEST_INDEX := ".Length..]);
            var moveBlkVariantRetVal = moveBlkVariantArgs[5]["Ret_Val => ".Length..];
            var moveBlkVariantDest = moveBlkVariantArgs[6]["DEST => ".Length..];
            moveBlkVariants.Add(new MoveBlkVariantStatement(
                moveBlkVariantEn, moveBlkVariantSrc, moveBlkVariantCount, moveBlkVariantSrcIndex, moveBlkVariantDestIndex,
                moveBlkVariantRetVal, moveBlkVariantDest));
            i++;
        }

        // WAITs are always emitted after MOVE_BLK_VARIANTs (IrSerializer). No trailing "=> dest"
        // — a pure side-effecting delay, no destination at all.
        var waits = new List<WaitStatement>();
        while (i < lines.Length && lines[i].StartsWith("  WAIT(", StringComparison.Ordinal))
        {
            var waitMatch = WaitLineRegex().Match(lines[i]);
            if (!waitMatch.Success)
            {
                throw new IrFormatException($"Expected '  WAIT(EN := <expr-or-ENO>, WT := <expr>)', got: '{lines[i]}'");
            }

            var waitEn = ParseEnSource(waitMatch.Groups["en"].Value);
            var waitWt = ParseExprTerm(waitMatch.Groups["wt"].Value);
            waits.Add(new WaitStatement(waitEn, waitWt));
            i++;
        }

        // FillBlockIs are always emitted after WAITs (IrSerializer). Fixed arity (EN, IN, COUNT),
        // same regex-based shape as MOVE's own line.
        var fillBlockIs = new List<FillBlockIStatement>();
        while (i < lines.Length && lines[i].StartsWith("  FILLBLOCKI(", StringComparison.Ordinal))
        {
            var fillBlockIMatch = FillBlockILineRegex().Match(lines[i]);
            if (!fillBlockIMatch.Success)
            {
                throw new IrFormatException($"Expected '  FILLBLOCKI(EN := <expr-or-ENO>, IN := <expr>, COUNT := <expr>) => <dest>', got: '{lines[i]}'");
            }

            var fillBlockIEn = ParseEnSource(fillBlockIMatch.Groups["en"].Value);
            var fillBlockIIn = ParseExprTerm(fillBlockIMatch.Groups["in"].Value);
            var fillBlockICount = ParseExprTerm(fillBlockIMatch.Groups["count"].Value);
            fillBlockIs.Add(new FillBlockIStatement(fillBlockIEn, fillBlockIIn, fillBlockICount, fillBlockIMatch.Groups["dest"].Value));
            i++;
        }

        // ModbusMasters are always emitted after FillBlockIs (IrSerializer). Instance path is the
        // first positional argument (no label, same convention as TON/CALL's own). No trailing
        // "=> dest" — four named outputs, disambiguated per-argument by ":=" vs "=>", same mixing
        // convention MOVE_BLK_VARIANT already established.
        var modbusMasters = new List<ModbusMasterStatement>();
        while (i < lines.Length && lines[i].StartsWith("  MODBUS_MASTER(", StringComparison.Ordinal))
        {
            var modbusMasterMatch = ModbusMasterLineRegex().Match(lines[i]);
            if (!modbusMasterMatch.Success)
            {
                throw new IrFormatException(
                    $"Expected '  MODBUS_MASTER(<path>, EN := <expr-or-ENO>, REQ := <expr>, MB_ADDR := <expr>, MODE := <expr>, " +
                    $"DATA_ADDR := <expr>, DATA_LEN := <expr>, DATA_PTR := <expr>, DONE => <tag>, BUSY => <tag>, ERROR => <tag>, " +
                    $"STATUS => <tag>)', got: '{lines[i]}'");
            }

            var modbusMasterArgs = modbusMasterMatch.Groups["args"].Value.Split(", ", StringSplitOptions.None);
            if (modbusMasterArgs.Length != 12
                || !modbusMasterArgs[1].StartsWith("EN := ", StringComparison.Ordinal)
                || !modbusMasterArgs[2].StartsWith("REQ := ", StringComparison.Ordinal)
                || !modbusMasterArgs[3].StartsWith("MB_ADDR := ", StringComparison.Ordinal)
                || !modbusMasterArgs[4].StartsWith("MODE := ", StringComparison.Ordinal)
                || !modbusMasterArgs[5].StartsWith("DATA_ADDR := ", StringComparison.Ordinal)
                || !modbusMasterArgs[6].StartsWith("DATA_LEN := ", StringComparison.Ordinal)
                || !modbusMasterArgs[7].StartsWith("DATA_PTR := ", StringComparison.Ordinal)
                || !modbusMasterArgs[8].StartsWith("DONE => ", StringComparison.Ordinal)
                || !modbusMasterArgs[9].StartsWith("BUSY => ", StringComparison.Ordinal)
                || !modbusMasterArgs[10].StartsWith("ERROR => ", StringComparison.Ordinal)
                || !modbusMasterArgs[11].StartsWith("STATUS => ", StringComparison.Ordinal))
            {
                throw new IrFormatException(
                    "Expected '<path>, EN := <expr>, REQ := <expr>, MB_ADDR := <expr>, MODE := <expr>, DATA_ADDR := <expr>, " +
                    $"DATA_LEN := <expr>, DATA_PTR := <expr>, DONE => <tag>, BUSY => <tag>, ERROR => <tag>, STATUS => <tag>' " +
                    $"inside MODBUS_MASTER(...), got: '{lines[i]}'");
            }

            var modbusMasterPath = modbusMasterArgs[0];
            var modbusMasterEn = ParseEnSource(modbusMasterArgs[1]["EN := ".Length..]);
            var modbusMasterReq = ParseExpr(modbusMasterArgs[2]["REQ := ".Length..]);
            var modbusMasterMbAddr = ParseExprTerm(modbusMasterArgs[3]["MB_ADDR := ".Length..]);
            var modbusMasterMode = ParseExprTerm(modbusMasterArgs[4]["MODE := ".Length..]);
            var modbusMasterDataAddr = ParseExprTerm(modbusMasterArgs[5]["DATA_ADDR := ".Length..]);
            var modbusMasterDataLen = ParseExprTerm(modbusMasterArgs[6]["DATA_LEN := ".Length..]);
            var modbusMasterDataPtr = ParseExprTerm(modbusMasterArgs[7]["DATA_PTR := ".Length..]);
            var modbusMasterDone = modbusMasterArgs[8]["DONE => ".Length..];
            var modbusMasterBusy = modbusMasterArgs[9]["BUSY => ".Length..];
            var modbusMasterError = modbusMasterArgs[10]["ERROR => ".Length..];
            var modbusMasterStatus = modbusMasterArgs[11]["STATUS => ".Length..];
            modbusMasters.Add(new ModbusMasterStatement(
                modbusMasterEn, modbusMasterPath, modbusMasterReq, modbusMasterMbAddr, modbusMasterMode, modbusMasterDataAddr,
                modbusMasterDataLen, modbusMasterDataPtr, modbusMasterDone, modbusMasterBusy, modbusMasterError, modbusMasterStatus));
            i++;
        }

        // ModbusCommLoads are always emitted after ModbusMasters (IrSerializer). Same shape as
        // MODBUS_MASTER's own, minus BUSY (three outputs, not four).
        var modbusCommLoads = new List<ModbusCommLoadStatement>();
        while (i < lines.Length && lines[i].StartsWith("  MODBUS_COMM_LOAD(", StringComparison.Ordinal))
        {
            var modbusCommLoadMatch = ModbusCommLoadLineRegex().Match(lines[i]);
            if (!modbusCommLoadMatch.Success)
            {
                throw new IrFormatException(
                    $"Expected '  MODBUS_COMM_LOAD(<path>, EN := <expr-or-ENO>, REQ := <expr>, PORT := <expr>, BAUD := <expr>, " +
                    $"PARITY := <expr>, RESP_TO := <expr>, MB_DB := <expr>, DONE => <tag>, ERROR => <tag>, STATUS => <tag>)', " +
                    $"got: '{lines[i]}'");
            }

            var modbusCommLoadArgs = modbusCommLoadMatch.Groups["args"].Value.Split(", ", StringSplitOptions.None);
            if (modbusCommLoadArgs.Length != 11
                || !modbusCommLoadArgs[1].StartsWith("EN := ", StringComparison.Ordinal)
                || !modbusCommLoadArgs[2].StartsWith("REQ := ", StringComparison.Ordinal)
                || !modbusCommLoadArgs[3].StartsWith("PORT := ", StringComparison.Ordinal)
                || !modbusCommLoadArgs[4].StartsWith("BAUD := ", StringComparison.Ordinal)
                || !modbusCommLoadArgs[5].StartsWith("PARITY := ", StringComparison.Ordinal)
                || !modbusCommLoadArgs[6].StartsWith("RESP_TO := ", StringComparison.Ordinal)
                || !modbusCommLoadArgs[7].StartsWith("MB_DB := ", StringComparison.Ordinal)
                || !modbusCommLoadArgs[8].StartsWith("DONE => ", StringComparison.Ordinal)
                || !modbusCommLoadArgs[9].StartsWith("ERROR => ", StringComparison.Ordinal)
                || !modbusCommLoadArgs[10].StartsWith("STATUS => ", StringComparison.Ordinal))
            {
                throw new IrFormatException(
                    "Expected '<path>, EN := <expr>, REQ := <expr>, PORT := <expr>, BAUD := <expr>, PARITY := <expr>, " +
                    $"RESP_TO := <expr>, MB_DB := <expr>, DONE => <tag>, ERROR => <tag>, STATUS => <tag>' inside " +
                    $"MODBUS_COMM_LOAD(...), got: '{lines[i]}'");
            }

            var modbusCommLoadPath = modbusCommLoadArgs[0];
            var modbusCommLoadEn = ParseEnSource(modbusCommLoadArgs[1]["EN := ".Length..]);
            var modbusCommLoadReq = ParseExprTerm(modbusCommLoadArgs[2]["REQ := ".Length..]);
            var modbusCommLoadPort = ParseExprTerm(modbusCommLoadArgs[3]["PORT := ".Length..]);
            var modbusCommLoadBaud = ParseExprTerm(modbusCommLoadArgs[4]["BAUD := ".Length..]);
            var modbusCommLoadParity = ParseExprTerm(modbusCommLoadArgs[5]["PARITY := ".Length..]);
            var modbusCommLoadRespTo = ParseExprTerm(modbusCommLoadArgs[6]["RESP_TO := ".Length..]);
            var modbusCommLoadMbDb = ParseExprTerm(modbusCommLoadArgs[7]["MB_DB := ".Length..]);
            var modbusCommLoadDone = modbusCommLoadArgs[8]["DONE => ".Length..];
            var modbusCommLoadError = modbusCommLoadArgs[9]["ERROR => ".Length..];
            var modbusCommLoadStatus = modbusCommLoadArgs[10]["STATUS => ".Length..];
            modbusCommLoads.Add(new ModbusCommLoadStatement(
                modbusCommLoadEn, modbusCommLoadPath, modbusCommLoadReq, modbusCommLoadPort, modbusCommLoadBaud, modbusCommLoadParity,
                modbusCommLoadRespTo, modbusCommLoadMbDb, modbusCommLoadDone, modbusCommLoadError, modbusCommLoadStatus));
            i++;
        }

        if (assignments.Count == 0 && timers.Count == 0 && moves.Count == 0 && wordAnds.Count == 0
            && calls.Count == 0 && muls.Count == 0 && converts.Count == 0 && swaps.Count == 0
            && absStatements.Count == 0 && limits.Count == 0 && tSubs.Count == 0 && tConvs.Count == 0
            && calcs.Count == 0 && moveBlkVariants.Count == 0 && waits.Count == 0 && fillBlockIs.Count == 0
            && modbusMasters.Count == 0 && modbusCommLoads.Count == 0)
        {
            throw new IrFormatException($"Network {number} has no COIL/TON/TONR/MOVE/WAND/CALL/MUL/ADD/CONVERT/SWAP/ABS/LIMIT/T_SUB/T_CONV/CALC/MOVE_BLK_VARIANT/WAIT/FILLBLOCKI/MODBUS_MASTER/MODBUS_COMM_LOAD statements and isn't marked [empty].");
        }

        return new IrNetwork(
            number, title, assignments, timers, moves, wordAnds, calls, comment, muls, converts, swaps, absStatements, limits, tSubs, tConvs, calcs,
            moveBlkVariants, waits, fillBlockIs, modbusMasters, modbusCommLoads);
    }

    // The inverse of IrSerializer.SerializeEnSource — "ENO" is the reserved sentinel for the
    // ENO-chained case (confirmed real, 2026-07-12, S1 item 18); anything else is an ordinary
    // expression, parsed via ParseExpr (not ParseExprTerm) since the ordinary case can itself be
    // a compound AND/OR condition, same as every other production's own EN field.
    private static EnSource ParseEnSource(string text)
    {
        text = text.Trim();
        return text == "ENO" ? new EnSource.PrecedingEno() : new EnSource.Condition(ParseExpr(text));
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
    // "=" / ">=" / "<" / "<>" / ">" / "<=" (Eq/Ge/Lt/Ne/Gt/Le) are all confirmed real and emitted
    // by this converter — the full IEC comparison family, none left unconfirmed.
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

    // A literal (e.g. "T#100MS", "16#89", or a bare integer like "1"/"-1") is recognized by shape
    // rather than by consulting the sidecar, so the IR text alone stays unambiguous to a reader.
    // "T#" is the existing time-literal convention (confirmed real, 2026-07-11, FB MotorDOL's TON
    // PT); a bare (optionally negative) integer is a comparison operand (FC ControlDelays); "16#"
    // is Siemens' own hex-literal notation (confirmed real, 2026-07-12, FB VSDUpdateComs's
    // bitwise-And input, `16#89`) — a base-N numeric literal is recognized by the presence of "#"
    // generally (only "16#" grounded so far; other bases like "2#"/"8#" are the same IEC 61131-3
    // family but unconfirmed in this codebase, so not specifically claimed, just not excluded by
    // this shape check either). Safe to recognize any of these by shape since a real tag path is
    // never purely numeric and never contains "#" (06-lad-conventions.md C-005: starts with a
    // letter).
    private static Expr ParseLeaf(string text)
    {
        text = text.Trim();
        if (text.StartsWith("T#", StringComparison.Ordinal) || IntegerLiteralRegex().IsMatch(text) || NumericBaseLiteralRegex().IsMatch(text))
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

        var wordAnds = new List<WordAndStatementSidecar>();
        while (i < lines.Length && WordAndHeaderRegex().IsMatch(lines[i]))
        {
            wordAnds.Add(ParseWordAndSidecar(lines, ref i, number));
        }

        var calls = new List<CallStatementSidecar>();
        while (i < lines.Length && CallHeaderRegex().IsMatch(lines[i]))
        {
            calls.Add(ParseCallSidecar(lines, ref i, number));
        }

        var muls = new List<MulStatementSidecar>();
        while (i < lines.Length && MulHeaderRegex().IsMatch(lines[i]))
        {
            muls.Add(ParseMulSidecar(lines, ref i, number));
        }

        var converts = new List<ConvertStatementSidecar>();
        while (i < lines.Length && ConvertHeaderRegex().IsMatch(lines[i]))
        {
            converts.Add(ParseConvertSidecar(lines, ref i, number));
        }

        var swaps = new List<SwapStatementSidecar>();
        while (i < lines.Length && SwapHeaderRegex().IsMatch(lines[i]))
        {
            swaps.Add(ParseSwapSidecar(lines, ref i, number));
        }

        var absStatements = new List<AbsStatementSidecar>();
        while (i < lines.Length && AbsHeaderRegex().IsMatch(lines[i]))
        {
            absStatements.Add(ParseAbsSidecar(lines, ref i, number));
        }

        var limits = new List<LimitStatementSidecar>();
        while (i < lines.Length && LimitHeaderRegex().IsMatch(lines[i]))
        {
            limits.Add(ParseLimitSidecar(lines, ref i, number));
        }

        var tSubs = new List<TSubStatementSidecar>();
        while (i < lines.Length && TSubHeaderRegex().IsMatch(lines[i]))
        {
            tSubs.Add(ParseTSubSidecar(lines, ref i, number));
        }

        var tConvs = new List<TConvStatementSidecar>();
        while (i < lines.Length && TConvHeaderRegex().IsMatch(lines[i]))
        {
            tConvs.Add(ParseTConvSidecar(lines, ref i, number));
        }

        var calcs = new List<CalcStatementSidecar>();
        while (i < lines.Length && CalcHeaderRegex().IsMatch(lines[i]))
        {
            calcs.Add(ParseCalcSidecar(lines, ref i, number));
        }

        var moveBlkVariants = new List<MoveBlkVariantStatementSidecar>();
        while (i < lines.Length && MoveBlkVariantHeaderRegex().IsMatch(lines[i]))
        {
            moveBlkVariants.Add(ParseMoveBlkVariantSidecar(lines, ref i, number));
        }

        var waits = new List<WaitStatementSidecar>();
        while (i < lines.Length && WaitHeaderRegex().IsMatch(lines[i]))
        {
            waits.Add(ParseWaitSidecar(lines, ref i, number));
        }

        var fillBlockIs = new List<FillBlockIStatementSidecar>();
        while (i < lines.Length && FillBlockIHeaderRegex().IsMatch(lines[i]))
        {
            fillBlockIs.Add(ParseFillBlockISidecar(lines, ref i, number));
        }

        var modbusMasters = new List<ModbusMasterStatementSidecar>();
        while (i < lines.Length && ModbusMasterHeaderRegex().IsMatch(lines[i]))
        {
            modbusMasters.Add(ParseModbusMasterSidecar(lines, ref i, number));
        }

        var modbusCommLoads = new List<ModbusCommLoadStatementSidecar>();
        while (i < lines.Length && ModbusCommLoadHeaderRegex().IsMatch(lines[i]))
        {
            modbusCommLoads.Add(ParseModbusCommLoadSidecar(lines, ref i, number));
        }

        return new NetworkSidecar(
            number, compileUnitUId, accessEntries, assignments, constantEntries, timers, moves, wordAnds, calls, muls, converts, swaps,
            absStatements, limits, tSubs, tConvs, calcs, moveBlkVariants, waits, fillBlockIs, modbusMasters, modbusCommLoads);
    }

    // The inverse of IrSerializer.SerializeEnSourceSidecar — "en = condition" followed by the
    // same rail/steps shape every other production's own en/IN chain already uses, or
    // "en = eno <precedingUid> <wireUid>" for the ENO-chained case (S1 item 18).
    private static EnSourceSidecar ParseEnSourceSidecar(string[] lines, ref int i, string indent)
    {
        var line = RequireLine(lines, ref i);
        if (line == indent + "en = condition")
        {
            var railWireUId = ParseRail(RequirePrefixedLine(lines, ref i, indent + "  rail = "));
            var steps = new List<ChainStepSidecar>();
            var s = 0;
            while (i < lines.Length && IsStepHeader(lines[i], indent + "  ", $"step {s}"))
            {
                steps.Add(ParseStep(lines, ref i, indent + "  ", $"step {s}"));
                s++;
            }

            return new EnSourceSidecar.ConditionSidecar(railWireUId, steps);
        }

        var enoPrefix = indent + "en = eno ";
        if (line.StartsWith(enoPrefix, StringComparison.Ordinal))
        {
            var parts = line[enoPrefix.Length..].Split(' ');
            return new EnSourceSidecar.PrecedingEnoSidecar(int.Parse(parts[0]), int.Parse(parts[1]));
        }

        throw new IrFormatException($"Expected '{indent}en = condition' or '{indent}en = eno <uid> <wire>', got: '{line}'");
    }

    // A Mul's own sidecar shape mirrors ParseWordAndSidecar's closely — same positional input
    // operands (`input 0`, `input 1`, ... — Cardinality-driven) — except `en` is an EnSourceSidecar
    // (S1 item 18), not a flat rail/steps pair, and there's no `srctype` (Mul is untyped in the
    // source, see PartNode's own AutomaticSrcType field).
    private static MulStatementSidecar ParseMulSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  mul <n>" header — index itself isn't needed, position in the list is enough.

        var mulPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    muluid = "));
        var mulKindText = RequirePrefixedLine(lines, ref i, "    kind = ").Trim();
        var mulKind = mulKindText switch
        {
            "mul" => MulKind.Multiply,
            "add" => MulKind.Add,
            "sub" => MulKind.Subtract,
            "div" => MulKind.Divide,
            _ => throw new IrFormatException($"Unexpected Mul kind '{mulKindText}' in SIDECAR for network {networkNumber}."),
        };
        var en = ParseEnSourceSidecar(lines, ref i, "    ");

        var inputs = new List<OperandSidecar>();
        var k = 0;
        while (i < lines.Length && (lines[i].StartsWith($"    input {k} tag = ", StringComparison.Ordinal) || lines[i].StartsWith($"    input {k} literal = ", StringComparison.Ordinal)))
        {
            inputs.Add(ParseOperand(lines, ref i, "    ", $"input {k}"));
            k++;
        }

        // Optional — confirmed real, 2026-07-12 (S1 item 20 live verification, FB AirStar): a
        // Mul/Add's own explicit SrcType shape, alongside the original AutomaticTyped shape
        // (absent line).
        string? srcType = null;
        if (i < lines.Length && lines[i].StartsWith("    srctype = ", StringComparison.Ordinal))
        {
            srcType = lines[i]["    srctype = ".Length..];
            i++;
        }

        var destAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    dest = "));
        var destWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    destwire = "));

        return new MulStatementSidecar(mulPartUId, en, inputs, destAccessUId, destWireUId, mulKind, srcType);
    }

    // A Convert's own sidecar shape mirrors ParseMoveSidecar's rail/steps mechanism, except `en`
    // is an EnSourceSidecar (S1 item 18), plus `desttype` alongside the existing `srctype`
    // (Convert is typed *between* two types, unlike every other typed instruction's single type).
    private static ConvertStatementSidecar ParseConvertSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  convert <n>" header — index itself isn't needed, position in the list is enough.

        var convertPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    convertuid = "));
        var en = ParseEnSourceSidecar(lines, ref i, "    ");

        var inOperand = ParseOperand(lines, ref i, "    ", "in");

        var srcType = RequirePrefixedLine(lines, ref i, "    srctype = ");
        var destType = RequirePrefixedLine(lines, ref i, "    desttype = ");

        var destAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    dest = "));
        var destWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    destwire = "));

        return new ConvertStatementSidecar(convertPartUId, en, inOperand, srcType, destType, destAccessUId, destWireUId);
    }

    // A Swap's own sidecar shape mirrors ParseConvertSidecar exactly, minus `desttype` — a
    // byte-swap has only one type.
    private static SwapStatementSidecar ParseSwapSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  swap <n>" header — index itself isn't needed, position in the list is enough.

        var swapPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    swapuid = "));
        var en = ParseEnSourceSidecar(lines, ref i, "    ");

        var inOperand = ParseOperand(lines, ref i, "    ", "in");

        var srcType = RequirePrefixedLine(lines, ref i, "    srctype = ");

        var destAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    dest = "));
        var destWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    destwire = "));

        return new SwapStatementSidecar(swapPartUId, en, inOperand, srcType, destAccessUId, destWireUId);
    }

    // An Abs's own sidecar shape mirrors ParseSwapSidecar exactly (same shape, different source
    // Part Name).
    private static AbsStatementSidecar ParseAbsSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  abs <n>" header — index itself isn't needed, position in the list is enough.

        var absPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    absuid = "));
        var en = ParseEnSourceSidecar(lines, ref i, "    ");

        var inOperand = ParseOperand(lines, ref i, "    ", "in");

        var srcType = RequirePrefixedLine(lines, ref i, "    srctype = ");

        var destAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    dest = "));
        var destWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    destwire = "));

        return new AbsStatementSidecar(absPartUId, en, inOperand, srcType, destAccessUId, destWireUId);
    }

    // A LIMIT's own sidecar shape adds a Version line (same role as a timer's own, see
    // TimerBindingSidecar) and three named operands (mn/in/mx) instead of Convert/Swap/Abs's
    // single "in" — see LimitStatementSidecar's own doc comment.
    private static LimitStatementSidecar ParseLimitSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  limit <n>" header — index itself isn't needed, position in the list is enough.

        var limitPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    limituid = "));
        var version = RequirePrefixedLine(lines, ref i, "    version = ");
        var en = ParseEnSourceSidecar(lines, ref i, "    ");

        var minOperand = ParseOperand(lines, ref i, "    ", "mn");
        var inOperand = ParseOperand(lines, ref i, "    ", "in");
        var maxOperand = ParseOperand(lines, ref i, "    ", "mx");

        var valueType = RequirePrefixedLine(lines, ref i, "    valuetype = ");

        var destAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    dest = "));
        var destWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    destwire = "));

        return new LimitStatementSidecar(limitPartUId, version, en, minOperand, inOperand, maxOperand, valueType, destAccessUId, destWireUId);
    }

    // A T_SUB's own sidecar shape adds a Version line (same role as LIMIT's own) and two named
    // operands (in1/in2, mirroring Sub's own binary shape) plus datetype/timetype instead of a
    // single srctype.
    private static TSubStatementSidecar ParseTSubSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  tsub <n>" header — index itself isn't needed, position in the list is enough.

        var tSubPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    tsubuid = "));
        var version = RequirePrefixedLine(lines, ref i, "    version = ");
        var en = ParseEnSourceSidecar(lines, ref i, "    ");

        var in1Operand = ParseOperand(lines, ref i, "    ", "in1");
        var in2Operand = ParseOperand(lines, ref i, "    ", "in2");

        var dateType = RequirePrefixedLine(lines, ref i, "    datetype = ");
        var timeType = RequirePrefixedLine(lines, ref i, "    timetype = ");

        var destAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    dest = "));
        var destWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    destwire = "));

        return new TSubStatementSidecar(tSubPartUId, version, en, in1Operand, in2Operand, dateType, timeType, destAccessUId, destWireUId);
    }

    // A T_CONV's own sidecar shape mirrors ParseConvertSidecar exactly, plus a Version line
    // (which ordinary Convert never carries).
    private static TConvStatementSidecar ParseTConvSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  tconv <n>" header — index itself isn't needed, position in the list is enough.

        var tConvPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    tconvuid = "));
        var version = RequirePrefixedLine(lines, ref i, "    version = ");
        var en = ParseEnSourceSidecar(lines, ref i, "    ");

        var inOperand = ParseOperand(lines, ref i, "    ", "in");

        var srcType = RequirePrefixedLine(lines, ref i, "    srctype = ");
        var destType = RequirePrefixedLine(lines, ref i, "    desttype = ");

        var destAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    dest = "));
        var destWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    destwire = "));

        return new TConvStatementSidecar(tConvPartUId, version, en, inOperand, srcType, destType, destAccessUId, destWireUId);
    }

    // A Calc's own sidecar shape mirrors ParseMulSidecar's Cardinality-driven input-operand loop,
    // plus a quoted `equation` line (same ParseQuotedString every TITLE/COMMENT line already
    // uses) instead of Mul's own optional srctype-or-AutomaticTyped choice — Calc's SrcType is
    // always an explicit TemplateValue, never AutomaticTyped (see ParseCalcFixedShape's own doc
    // comment), so it's a plain required line here, not optional.
    private static CalcStatementSidecar ParseCalcSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  calc <n>" header — index itself isn't needed, position in the list is enough.

        var calcPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    calcuid = "));
        var en = ParseEnSourceSidecar(lines, ref i, "    ");

        var inputs = new List<OperandSidecar>();
        var k = 0;
        while (i < lines.Length && (lines[i].StartsWith($"    input {k} tag = ", StringComparison.Ordinal) || lines[i].StartsWith($"    input {k} literal = ", StringComparison.Ordinal)))
        {
            inputs.Add(ParseOperand(lines, ref i, "    ", $"input {k}"));
            k++;
        }

        var equation = ParseQuotedString(RequirePrefixedLine(lines, ref i, "    equation = "));
        var srcType = RequirePrefixedLine(lines, ref i, "    srctype = ");

        var destAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    dest = "));
        var destWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    destwire = "));

        return new CalcStatementSidecar(calcPartUId, en, inputs, equation, srcType, destAccessUId, destWireUId);
    }

    // A MOVE_BLK_VARIANT's own sidecar shape has four named operands (src/count/srcindex/
    // destindex) and two destination pairs (retval/retvalwire, dest/destwire) instead of the
    // usual one — see MoveBlkVariantStatementSidecar's own doc comment.
    private static MoveBlkVariantStatementSidecar ParseMoveBlkVariantSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  moveblkvariant <n>" header — index itself isn't needed, position in the list is enough.

        var moveBlkVariantPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    moveblkvariantuid = "));
        var version = RequirePrefixedLine(lines, ref i, "    version = ");
        var en = ParseEnSourceSidecar(lines, ref i, "    ");

        var srcOperand = ParseOperand(lines, ref i, "    ", "src");
        var countOperand = ParseOperand(lines, ref i, "    ", "count");
        var srcIndexOperand = ParseOperand(lines, ref i, "    ", "srcindex");
        var destIndexOperand = ParseOperand(lines, ref i, "    ", "destindex");

        var retValAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    retval = "));
        var retValWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    retvalwire = "));
        var destAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    dest = "));
        var destWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    destwire = "));

        return new MoveBlkVariantStatementSidecar(
            moveBlkVariantPartUId, version, en, srcOperand, countOperand, srcIndexOperand, destIndexOperand,
            retValAccessUId, retValWireUId, destAccessUId, destWireUId);
    }

    // A WAIT's own sidecar shape has a Version line (same role as LIMIT/T_SUB's own) and one
    // named operand (wt) — no destination lines at all, see WaitStatementSidecar's own doc
    // comment.
    private static WaitStatementSidecar ParseWaitSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  wait <n>" header — index itself isn't needed, position in the list is enough.

        var waitPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    waituid = "));
        var version = RequirePrefixedLine(lines, ref i, "    version = ");
        var en = ParseEnSourceSidecar(lines, ref i, "    ");

        var wtOperand = ParseOperand(lines, ref i, "    ", "wt");

        return new WaitStatementSidecar(waitPartUId, version, en, wtOperand);
    }

    // A FillBlockI's own sidecar shape mirrors ParseMoveSidecar's rail/steps mechanism, plus a
    // second named operand (count) — no Version (FillBlockI never carries one, unlike WAIT).
    private static FillBlockIStatementSidecar ParseFillBlockISidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  fillblocki <n>" header — index itself isn't needed, position in the list is enough.

        var fillBlockIPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    fillblockiuid = "));
        var en = ParseEnSourceSidecar(lines, ref i, "    ");

        var inOperand = ParseOperand(lines, ref i, "    ", "in");
        var countOperand = ParseOperand(lines, ref i, "    ", "count");

        var destAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    dest = "));
        var destWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    destwire = "));

        return new FillBlockIStatementSidecar(fillBlockIPartUId, en, inOperand, countOperand, destAccessUId, destWireUId);
    }

    // A Modbus_Master's own sidecar shape adds Instance fields (mirrors ParseTimerSidecar's own)
    // and a rail+steps chain for Req (mirrors a Coil's own IN-chain shape — "reqstep" instead of
    // "step" to disambiguate the label, same reasoning EnSourceSidecar's own nested "step" labels
    // already establish at a different indent level), then five ordinary operands and four
    // separate destination pairs (Done/Busy/Error/Status).
    private static ModbusMasterStatementSidecar ParseModbusMasterSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  modbusmaster <n>" header — index itself isn't needed, position in the list is enough.

        var modbusMasterPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    modbusmasteruid = "));
        var version = RequirePrefixedLine(lines, ref i, "    version = ");
        var en = ParseEnSourceSidecar(lines, ref i, "    ");

        var instanceUId = int.Parse(RequirePrefixedLine(lines, ref i, "    instanceuid = "));
        var instanceScope = RequirePrefixedLine(lines, ref i, "    instancescope = ");
        var instancePath = RequirePrefixedLine(lines, ref i, "    instancepath = ").Split('.');

        var reqRailWireUId = ParseRail(RequirePrefixedLine(lines, ref i, "    reqrail = "));
        var reqSteps = new List<ChainStepSidecar>();
        var rs = 0;
        while (i < lines.Length && IsStepHeader(lines[i], "    ", $"reqstep {rs}"))
        {
            reqSteps.Add(ParseStep(lines, ref i, "    ", $"reqstep {rs}"));
            rs++;
        }

        var mbAddrOperand = ParseOperand(lines, ref i, "    ", "mbaddr");
        var modeOperand = ParseOperand(lines, ref i, "    ", "mode");
        var dataAddrOperand = ParseOperand(lines, ref i, "    ", "dataaddr");
        var dataLenOperand = ParseOperand(lines, ref i, "    ", "datalen");
        var dataPtrOperand = ParseOperand(lines, ref i, "    ", "dataptr");

        var doneAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    done = "));
        var doneWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    donewire = "));
        var busyAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    busy = "));
        var busyWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    busywire = "));
        var errorAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    error = "));
        var errorWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    errorwire = "));
        var statusAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    status = "));
        var statusWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    statuswire = "));

        return new ModbusMasterStatementSidecar(
            modbusMasterPartUId, version, en, instanceUId, instanceScope, instancePath, reqRailWireUId, reqSteps,
            mbAddrOperand, modeOperand, dataAddrOperand, dataLenOperand, dataPtrOperand,
            doneAccessUId, doneWireUId, busyAccessUId, busyWireUId, errorAccessUId, errorWireUId, statusAccessUId, statusWireUId);
    }

    // A Modbus_Comm_Load's own sidecar shape adds Instance fields (mirrors ParseModbusMasterSidecar's
    // own), four ordinary operands, three optional OpenConnectionSidecar lines (mirrors
    // ParseTimerSidecar's own `et` line — absent-by-omission, same convention), two more ordinary
    // operands, and three destination pairs (Done/Error/Status, no Busy).
    private static ModbusCommLoadStatementSidecar ParseModbusCommLoadSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  modbuscommload <n>" header — index itself isn't needed, position in the list is enough.

        var modbusCommLoadPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    modbuscommloaduid = "));
        var version = RequirePrefixedLine(lines, ref i, "    version = ");
        var en = ParseEnSourceSidecar(lines, ref i, "    ");

        var instanceUId = int.Parse(RequirePrefixedLine(lines, ref i, "    instanceuid = "));
        var instanceScope = RequirePrefixedLine(lines, ref i, "    instancescope = ");
        var instancePath = RequirePrefixedLine(lines, ref i, "    instancepath = ").Split('.');

        var reqOperand = ParseOperand(lines, ref i, "    ", "req");
        var portOperand = ParseOperand(lines, ref i, "    ", "port");
        var baudOperand = ParseOperand(lines, ref i, "    ", "baud");
        var parityOperand = ParseOperand(lines, ref i, "    ", "parity");

        OpenConnectionSidecar? flowCtrl = null;
        if (i < lines.Length && lines[i].StartsWith("    flowctrl = ", StringComparison.Ordinal))
        {
            var parts = lines[i]["    flowctrl = ".Length..].Split(' ');
            flowCtrl = new OpenConnectionSidecar(int.Parse(parts[0]), int.Parse(parts[1]));
            i++;
        }

        OpenConnectionSidecar? rtsOnDly = null;
        if (i < lines.Length && lines[i].StartsWith("    rtsondly = ", StringComparison.Ordinal))
        {
            var parts = lines[i]["    rtsondly = ".Length..].Split(' ');
            rtsOnDly = new OpenConnectionSidecar(int.Parse(parts[0]), int.Parse(parts[1]));
            i++;
        }

        OpenConnectionSidecar? rtsOffDly = null;
        if (i < lines.Length && lines[i].StartsWith("    rtsoffdly = ", StringComparison.Ordinal))
        {
            var parts = lines[i]["    rtsoffdly = ".Length..].Split(' ');
            rtsOffDly = new OpenConnectionSidecar(int.Parse(parts[0]), int.Parse(parts[1]));
            i++;
        }

        var respToOperand = ParseOperand(lines, ref i, "    ", "respto");
        var mbDbOperand = ParseOperand(lines, ref i, "    ", "mbdb");

        var doneAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    done = "));
        var doneWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    donewire = "));
        var errorAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    error = "));
        var errorWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    errorwire = "));
        var statusAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    status = "));
        var statusWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    statuswire = "));

        return new ModbusCommLoadStatementSidecar(
            modbusCommLoadPartUId, version, en, instanceUId, instanceScope, instancePath,
            reqOperand, portOperand, baudOperand, parityOperand, flowCtrl, rtsOnDly, rtsOffDly, respToOperand, mbDbOperand,
            doneAccessUId, doneWireUId, errorAccessUId, errorWireUId, statusAccessUId, statusWireUId);
    }

    // A Call's own sidecar shape mirrors ParseMoveSidecar's rail/steps mechanism, plus
    // BlockName/BlockType and the same Instance fields ParseTimerSidecar carries, plus a sparse,
    // ordered argument list (ParseCallArgument per entry — mirrors ParseWordAndSidecar's own
    // "read positional entries until the header no longer matches" loop, just keyed by index
    // rather than fixed "input K" naming since a Call argument carries its own Name already).
    private static CallStatementSidecar ParseCallSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  call <n>" header — index itself isn't needed, position in the list is enough.

        var callPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    calluid = "));
        var blockName = RequirePrefixedLine(lines, ref i, "    blockname = ");
        var blockType = RequirePrefixedLine(lines, ref i, "    blocktype = ");
        var railWireUId = ParseRail(RequirePrefixedLine(lines, ref i, "    rail = "));

        var steps = new List<ChainStepSidecar>();
        var s = 0;
        while (i < lines.Length && IsStepHeader(lines[i], "    ", $"step {s}"))
        {
            steps.Add(ParseStep(lines, ref i, "    ", $"step {s}"));
            s++;
        }

        // Omitted entirely when absent — confirmed real, 2026-07-12 (S1 item 24: an FC call has
        // no instance) — same "absent lines mean no-instance" convention as the timer sidecar's
        // own optional `reset` lines (S1 item 19) below.
        int? instanceUId = null;
        string? instanceScope = null;
        IReadOnlyList<string>? instancePath = null;
        if (i < lines.Length && lines[i].StartsWith("    instanceuid = ", StringComparison.Ordinal))
        {
            instanceUId = int.Parse(RequirePrefixedLine(lines, ref i, "    instanceuid = "));
            instanceScope = RequirePrefixedLine(lines, ref i, "    instancescope = ");
            instancePath = RequirePrefixedLine(lines, ref i, "    instancepath = ").Split('.');
        }

        var arguments = new List<CallArgumentSidecar>();
        var a = 0;
        while (i < lines.Length && (lines[i].StartsWith($"    argument {a} input ", StringComparison.Ordinal) || lines[i].StartsWith($"    argument {a} output ", StringComparison.Ordinal)))
        {
            arguments.Add(ParseCallArgument(lines, ref i, a));
            a++;
        }

        return new CallStatementSidecar(callPartUId, blockName, blockType, railWireUId, steps, instanceUId, instanceScope, instancePath, arguments);
    }

    // The inverse of IrSerializer.SerializeCallArgument — "argument <n> input <name> <type>"
    // followed by the same tag-or-literal operand line ParseOperand already handles, or
    // "argument <n> output <name> <type>" followed by the same dest/destwire pair Move's own
    // out1 uses. Header built from the exact expected index (same style as ParseWordAndSidecar's
    // own "input K" lines) rather than a general regex, since a Call argument's own ParamName can
    // itself contain characters a generic \S+ capture would need to be careful about.
    private static CallArgumentSidecar ParseCallArgument(string[] lines, ref int i, int index)
    {
        var line = RequireLine(lines, ref i);
        var inputPrefix = $"    argument {index} input ";
        var outputPrefix = $"    argument {index} output ";

        string kind;
        string rest;
        if (line.StartsWith(inputPrefix, StringComparison.Ordinal))
        {
            kind = "input";
            rest = line[inputPrefix.Length..];
        }
        else if (line.StartsWith(outputPrefix, StringComparison.Ordinal))
        {
            kind = "output";
            rest = line[outputPrefix.Length..];
        }
        else
        {
            throw new IrFormatException($"Expected '    argument {index} input|output <name> <type>', got: '{line}'");
        }

        var spaceIndex = rest.LastIndexOf(' ');
        if (spaceIndex < 0)
        {
            throw new IrFormatException($"Expected '<name> <type>' in call argument header, got: '{rest}'");
        }

        var paramName = rest[..spaceIndex];
        var type = rest[(spaceIndex + 1)..];

        if (kind == "input")
        {
            var value = ParseOperand(lines, ref i, "      ", "value");
            return new CallArgumentSidecar.InputArgSidecar(paramName, type, value);
        }

        var destAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "      dest = "));
        var destWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "      destwire = "));
        return new CallArgumentSidecar.OutputArgSidecar(paramName, type, destAccessUId, destWireUId);
    }

    // A bitwise-And's own sidecar shape mirrors ParseMoveSidecar's closely — same rail/steps
    // mechanism, N positional input operands (`input 0`, `input 1`, ... — Cardinality-driven, S1
    // item 12) instead of Move's single `in`, and `srctype` (a comparison's own SrcType is
    // carried the same way, on its ChainStepSidecar.CompareStep instead — here it lives on the
    // production itself since there's exactly one per And, not one per operand).
    private static WordAndStatementSidecar ParseWordAndSidecar(string[] lines, ref int i, int networkNumber)
    {
        i++; // "  wand <n>" header — index itself isn't needed, position in the list is enough.

        var andPartUId = int.Parse(RequirePrefixedLine(lines, ref i, "    anduid = "));
        var railWireUId = ParseRail(RequirePrefixedLine(lines, ref i, "    rail = "));

        var steps = new List<ChainStepSidecar>();
        var s = 0;
        while (i < lines.Length && IsStepHeader(lines[i], "    ", $"step {s}"))
        {
            steps.Add(ParseStep(lines, ref i, "    ", $"step {s}"));
            s++;
        }

        var inputs = new List<OperandSidecar>();
        var k = 0;
        while (i < lines.Length && (lines[i].StartsWith($"    input {k} tag = ", StringComparison.Ordinal) || lines[i].StartsWith($"    input {k} literal = ", StringComparison.Ordinal)))
        {
            inputs.Add(ParseOperand(lines, ref i, "    ", $"input {k}"));
            k++;
        }

        var srcType = RequirePrefixedLine(lines, ref i, "    srctype = ");

        var destAccessUId = int.Parse(RequirePrefixedLine(lines, ref i, "    dest = "));
        var destWireUId = int.Parse(RequirePrefixedLine(lines, ref i, "    destwire = "));

        return new WordAndStatementSidecar(andPartUId, railWireUId, steps, inputs, srcType, destAccessUId, destWireUId);
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
        var timerKindText = RequirePrefixedLine(lines, ref i, "    kind = ").Trim();
        var timerKind = timerKindText switch
        {
            "ton" => TimerKind.Ton,
            "tonr" => TimerKind.Tonr,
            "tof" => TimerKind.Tof,
            _ => throw new IrFormatException($"Unexpected timer kind '{timerKindText}' in SIDECAR for network {networkNumber}."),
        };
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

        // TONR's own R (reset) — confirmed real, 2026-07-12, S1 item 19 — same tag-or-literal
        // operand shape as preset, emitted only when Kind is Tonr.
        OperandSidecar? reset = null;
        if (i < lines.Length && (lines[i].StartsWith("    reset tag = ", StringComparison.Ordinal) || lines[i].StartsWith("    reset literal = ", StringComparison.Ordinal)))
        {
            reset = ParseOperand(lines, ref i, "    ", "reset");
        }

        return new TimerBindingSidecar(
            tonPartUId, version, timeType, instanceUId, instanceScope, instancePath, railWireUId, steps, preset, et, timerKind, reset);
    }

    private static bool IsStepHeader(string line, string indent, string label) =>
        line == indent + label + " contact" || line == indent + label + " or"
        || line == indent + label + " timeroutput" || line == indent + label + " compare"
        || line == indent + label + " not";

    private static ChainStepSidecar ParseStep(string[] lines, ref int i, string indent, string label)
    {
        var header = RequireLine(lines, ref i);
        var contactHeader = indent + label + " contact";
        var orHeader = indent + label + " or";
        var timerOutputHeader = indent + label + " timeroutput";
        var compareHeader = indent + label + " compare";
        var notHeader = indent + label + " not";
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

        if (header == notHeader)
        {
            return ParseNotStepBody(lines, ref i, indent + "  ");
        }

        throw new IrFormatException(
            $"Expected '{contactHeader}', '{orHeader}', '{timerOutputHeader}', '{compareHeader}', or '{notHeader}', got: '{header}'");
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

    // The inverse of IrSerializer.SerializeStep's NotStep case — a Not's own upstream is an
    // ordinary nested chain (S1 item 13), same rail/steps shape as ParseOrStepBody's own branch
    // parsing, just a single chain instead of several.
    private static ChainStepSidecar.NotStep ParseNotStepBody(string[] lines, ref int i, string indent)
    {
        var uid = int.Parse(RequirePrefixedLine(lines, ref i, indent + "uid = "));
        var railWireUId = ParseRail(RequirePrefixedLine(lines, ref i, indent + "rail = "));

        var steps = new List<ChainStepSidecar>();
        var s = 0;
        while (i < lines.Length && IsStepHeader(lines[i], indent, $"step {s}"))
        {
            steps.Add(ParseStep(lines, ref i, indent, $"step {s}"));
            s++;
        }

        var outWire = int.Parse(RequirePrefixedLine(lines, ref i, indent + "out = "));
        return new ChainStepSidecar.NotStep(uid, steps, railWireUId, outWire);
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

    [GeneratedRegex(@"^  (?<kind>COIL|SCOIL|RCOIL) (?<tag>\S+) := (?<expr>.+)$")]
    private static partial Regex CoilLineRegex();

    // Variable arity (3 args for TON/TOF, 4 for TONR — S1 items 19/23), same
    // split-on-top-level-commas discipline as WAND/CALL/MUL's own variable-arity argument lists —
    // only the outer "TON|TONR|TOF(...)" shape is matched here.
    [GeneratedRegex(@"^  (?<kind>TONR|TOF|TON)\((?<args>.+)\)$")]
    private static partial Regex TonLineRegex();

    [GeneratedRegex(@"^  MOVE\(EN := (?<en>.+), IN := (?<in>.+)\) => (?<dest>\S+)$")]
    private static partial Regex MoveLineRegex();

    // Variable input count (Cardinality-driven, S1 item 12) — only the outer "WAND(...) => dest"
    // shape is matched here; the argument list itself is split on top-level commas separately
    // (ParseNetwork), not captured per-argument by this regex the way TON/MOVE's fixed arity is.
    [GeneratedRegex(@"^  WAND\((?<args>.+)\) => (?<dest>\S+)$")]
    private static partial Regex WordAndLineRegex();

    // Variable argument count and a mixed ":="/"=>" shape (Input/Output parameters interleaved,
    // S1 item 14) — only the outer "CALL BlockName(...)" shape is matched here; the argument
    // list itself is split on top-level commas separately (ParseNetwork), same discipline as
    // WAND's own variable-arity argument list above.
    [GeneratedRegex(@"^  CALL (?<blockname>\S+)\((?<args>.+)\)$")]
    private static partial Regex CallLineRegex();

    // Variable input count (Cardinality-driven, S1 item 18), same discipline as WAND's own
    // variable-arity argument list — the argument list itself is split on top-level commas
    // separately (ParseNetwork). MUL/ADD (S1 item 19) share this regex, distinguished by keyword;
    // SUB/DIV (2026-07-14, FC Scale) extend the same regex — always exactly 2 args in practice
    // (no Cardinality element in the source), but parsed the same variable-arity way regardless.
    [GeneratedRegex(@"^  (?<kind>MUL|ADD|SUB|DIV)\((?<args>.+)\) => (?<dest>\S+)$")]
    private static partial Regex MulLineRegex();

    // Fixed arity (EN, IN) — same regex-based shape as MOVE's own line.
    [GeneratedRegex(@"^  CONVERT\(EN := (?<en>.+), IN := (?<in>.+)\) => (?<dest>\S+)$")]
    private static partial Regex ConvertLineRegex();

    // Fixed arity (EN, IN) — same regex-based shape as CONVERT's own line, minus DestType.
    [GeneratedRegex(@"^  SWAP\(EN := (?<en>.+), IN := (?<in>.+)\) => (?<dest>\S+)$")]
    private static partial Regex SwapLineRegex();

    // Fixed arity (EN, IN) — same regex-based shape as SWAP's own line (Phase 2 Tier 1).
    [GeneratedRegex(@"^  ABS\(EN := (?<en>.+), IN := (?<in>.+)\) => (?<dest>\S+)$")]
    private static partial Regex AbsLineRegex();

    // Three fixed-named inputs (MN/IN/MX), same outer-shape-only-matched, split-on-top-level-
    // commas discipline as TON/WAND/MUL/CALL's own variable-arity argument lists (Phase 2 Tier 1).
    [GeneratedRegex(@"^  LIMIT\((?<args>.+)\) => (?<dest>\S+)$")]
    private static partial Regex LimitLineRegex();

    // Two fixed-named inputs (IN1/IN2), same outer-shape-only-matched discipline as LIMIT's own
    // (Phase 2 Tier 2).
    [GeneratedRegex(@"^  T_SUB\((?<args>.+)\) => (?<dest>\S+)$")]
    private static partial Regex TSubLineRegex();

    // Fixed arity (EN, IN) — same regex-based shape as CONVERT/SWAP/ABS's own line (Phase 2 Tier 2).
    [GeneratedRegex(@"^  T_CONV\(EN := (?<en>.+), IN := (?<in>.+)\) => (?<dest>\S+)$")]
    private static partial Regex TConvLineRegex();

    // Variable input count (Cardinality-driven, Phase 2 Tier 3), same discipline as WAND/MUL's own
    // variable-arity argument list, plus a trailing quoted Equation string after the destination
    // tag — kept outside the argument-list parens (see IrSerializer's own comment on why).
    [GeneratedRegex(@"^  CALC\((?<args>.+)\) => (?<dest>\S+) (?<equation>"".*"")$")]
    private static partial Regex CalcLineRegex();

    // No trailing "=> dest" — two named outputs, not one, both inside the parens (Phase 2 Tier 5).
    [GeneratedRegex(@"^  MOVE_BLK_VARIANT\((?<args>.+)\)$")]
    private static partial Regex MoveBlkVariantLineRegex();

    // No trailing "=> dest" — WAIT has no destination at all (Phase 2 Tier 6).
    [GeneratedRegex(@"^  WAIT\(EN := (?<en>.+), WT := (?<wt>.+)\)$")]
    private static partial Regex WaitLineRegex();

    // Fixed arity (EN, IN, COUNT) — same regex-based shape as MOVE's own line (Phase 2 Tier 6).
    [GeneratedRegex(@"^  FILLBLOCKI\(EN := (?<en>.+), IN := (?<in>.+), COUNT := (?<count>.+)\) => (?<dest>\S+)$")]
    private static partial Regex FillBlockILineRegex();

    // No trailing "=> dest" — four named outputs, not one, all inside the parens (Phase 2 Tier 4).
    [GeneratedRegex(@"^  MODBUS_MASTER\((?<args>.+)\)$")]
    private static partial Regex ModbusMasterLineRegex();

    // Same shape as MODBUS_MASTER's own, minus BUSY (three outputs, not four).
    [GeneratedRegex(@"^  MODBUS_COMM_LOAD\((?<args>.+)\)$")]
    private static partial Regex ModbusCommLoadLineRegex();

    [GeneratedRegex(@"^NETWORK (?<number>\d+)$")]
    private static partial Regex SidecarNetworkLineRegex();

    [GeneratedRegex(@"^  access (?<path>\S+) = (?<uid>\d+) (?<scope>\S+)$")]
    private static partial Regex SidecarAccessLineRegex();

    [GeneratedRegex(@"^  constant (?<value>\S+) = (?<uid>\d+) (?<type>\S+)$")]
    private static partial Regex SidecarConstantLineRegex();

    [GeneratedRegex(@"^-?\d+$")]
    private static partial Regex IntegerLiteralRegex();

    // Siemens' own <base>#<value> numeric-literal notation (e.g. "16#89") — only the "16#" (hex)
    // form is confirmed real (2026-07-12, FB VSDUpdateComs); matched generically by base-number
    // shape rather than hardcoded to "16" specifically, since nothing about the shape ties it to
    // one particular base.
    [GeneratedRegex(@"^-?\d+#[0-9A-Za-z]+$")]
    private static partial Regex NumericBaseLiteralRegex();

    [GeneratedRegex(@"^  timer (?<index>\d+)$")]
    private static partial Regex TimerHeaderRegex();

    [GeneratedRegex(@"^  assignment (?<index>\d+)$")]
    private static partial Regex AssignmentHeaderRegex();

    [GeneratedRegex(@"^  move (?<index>\d+)$")]
    private static partial Regex MoveHeaderRegex();

    [GeneratedRegex(@"^  wand (?<index>\d+)$")]
    private static partial Regex WordAndHeaderRegex();

    [GeneratedRegex(@"^  call (?<index>\d+)$")]
    private static partial Regex CallHeaderRegex();

    [GeneratedRegex(@"^  mul (?<index>\d+)$")]
    private static partial Regex MulHeaderRegex();

    [GeneratedRegex(@"^  convert (?<index>\d+)$")]
    private static partial Regex ConvertHeaderRegex();

    [GeneratedRegex(@"^  swap (?<index>\d+)$")]
    private static partial Regex SwapHeaderRegex();

    [GeneratedRegex(@"^  abs (?<index>\d+)$")]
    private static partial Regex AbsHeaderRegex();

    [GeneratedRegex(@"^  limit (?<index>\d+)$")]
    private static partial Regex LimitHeaderRegex();

    [GeneratedRegex(@"^  tsub (?<index>\d+)$")]
    private static partial Regex TSubHeaderRegex();

    [GeneratedRegex(@"^  tconv (?<index>\d+)$")]
    private static partial Regex TConvHeaderRegex();

    [GeneratedRegex(@"^  calc (?<index>\d+)$")]
    private static partial Regex CalcHeaderRegex();

    [GeneratedRegex(@"^  moveblkvariant (?<index>\d+)$")]
    private static partial Regex MoveBlkVariantHeaderRegex();

    [GeneratedRegex(@"^  wait (?<index>\d+)$")]
    private static partial Regex WaitHeaderRegex();

    [GeneratedRegex(@"^  fillblocki (?<index>\d+)$")]
    private static partial Regex FillBlockIHeaderRegex();

    [GeneratedRegex(@"^  modbusmaster (?<index>\d+)$")]
    private static partial Regex ModbusMasterHeaderRegex();

    [GeneratedRegex(@"^  modbuscommload (?<index>\d+)$")]
    private static partial Regex ModbusCommLoadHeaderRegex();
}
