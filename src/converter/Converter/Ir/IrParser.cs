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

        if (assignments.Count == 0)
        {
            throw new IrFormatException($"Network {number} has no COIL assignments and isn't marked [empty].");
        }

        return new IrNetwork(number, title, assignments);
    }

    private static Expr ParseExpr(string text)
    {
        text = text.Trim();
        if (text == "TRUE")
        {
            return new Expr.And(Array.Empty<Expr>());
        }

        if (text.Contains(" AND "))
        {
            var operands = text.Split(" AND ", StringSplitOptions.TrimEntries).Select(ParseExprTerm).ToList();
            return new Expr.And(operands);
        }

        if (text.Contains(" OR "))
        {
            var operands = text.Split(" OR ", StringSplitOptions.TrimEntries).Select(ParseExprTerm).ToList();
            return new Expr.Or(operands);
        }

        return ParseExprTerm(text);
    }

    private static Expr ParseExprTerm(string text)
    {
        text = text.Trim();
        if (text.StartsWith("NOT ", StringComparison.Ordinal))
        {
            return new Expr.Not(ParseExprTerm(text["NOT ".Length..]));
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

            accessEntries.Add(new SidecarAccessEntry(match.Groups["path"].Value, int.Parse(match.Groups["uid"].Value)));
            i++;
        }

        var assignments = new List<CoilAssignmentSidecar>();
        while (i < lines.Length && AssignmentHeaderRegex().IsMatch(lines[i]))
        {
            i++;

            if (i >= lines.Length || !lines[i].StartsWith("    rail = ", StringComparison.Ordinal))
            {
                throw new IrFormatException($"Expected '    rail = <uid>' in SIDECAR for network {number}.");
            }

            var railWireUId = int.Parse(lines[i]["    rail = ".Length..].Trim());
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

        return new NetworkSidecar(number, compileUnitUId, accessEntries, assignments);
    }

    private static bool IsStepHeader(string line, string indent, string label) =>
        line == indent + label + " contact" || line == indent + label + " or";

    private static ChainStepSidecar ParseStep(string[] lines, ref int i, string indent, string label)
    {
        var header = RequireLine(lines, ref i);
        var contactHeader = indent + label + " contact";
        var orHeader = indent + label + " or";
        if (header == contactHeader)
        {
            return ParseContactStepBody(lines, ref i, indent + "  ");
        }

        if (header == orHeader)
        {
            return ParseOrStepBody(lines, ref i, indent + "  ");
        }

        throw new IrFormatException($"Expected '{contactHeader}' or '{orHeader}', got: '{header}'");
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

        var branches = new List<ChainStepSidecar.ContactStep>();
        var b = 0;
        while (i < lines.Length && IsStepHeader(lines[i], indent, $"branch {b}"))
        {
            var branch = ParseStep(lines, ref i, indent, $"branch {b}");
            if (branch is not ChainStepSidecar.ContactStep contactBranch)
            {
                throw new IrFormatException("OR-merge branch must be a single contact.");
            }

            branches.Add(contactBranch);
            b++;
        }

        if (branches.Count == 0)
        {
            throw new IrFormatException("OR-merge step has no branches.");
        }

        var outWire = int.Parse(RequirePrefixedLine(lines, ref i, indent + "out = "));
        return new ChainStepSidecar.OrStep(uid, branches, outWire);
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

    [GeneratedRegex(@"^NETWORK (?<number>\d+)$")]
    private static partial Regex SidecarNetworkLineRegex();

    [GeneratedRegex(@"^  access (?<path>\S+) = (?<uid>\d+)$")]
    private static partial Regex SidecarAccessLineRegex();

    [GeneratedRegex(@"^  assignment (?<index>\d+)$")]
    private static partial Regex AssignmentHeaderRegex();
}
