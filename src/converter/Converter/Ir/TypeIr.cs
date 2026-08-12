using System.Text;
using Converter.SimaticMl;

namespace Converter.Ir;

/// <summary>
/// Text format for a PLC data type (UDT, `ir/SPEC.md` — extend "Tag tables, UDTs, DBs" once this
/// lands). Reuses <see cref="PlcTypeSource"/>/<see cref="DbMember"/> directly as the IR model —
/// no sidecar, no reduction step, same "the parsed SimaticML shape already is the readable form"
/// reasoning as <see cref="DbIrSerializer"/>/<see cref="DbIrParser"/>. Simpler than a DB's own IR
/// text: no `NUMBER`/`INSTANCEOF` lines — neither applies to a UDT (confirmed real, 2026-07-14:
/// `PlcType` has no `Number`, and a type is never an instance of anything).
///
/// ```
/// TYPE &lt;Name&gt;
///   ROOTID &lt;id&gt;
///   COMMENT "&lt;text&gt;"      # omitted if empty
///   MEMBERS
///     &lt;member&gt; : &lt;Datatype&gt;
///     &lt;member&gt; : &lt;Datatype&gt; = &lt;start value&gt;
///     &lt;member&gt; : &lt;Datatype&gt; SETPOINT   # present only when the source's own SetPoint
///                                          # BooleanAttribute is true
///     &lt;member&gt; : &lt;Datatype&gt; COMMENT "&lt;text&gt;"   # optional, always the last token —
///                                          # peeled before StartValue's " = " search, same rule
///                                          # as a DB/STATIC member line (2026-07-16)
///     &lt;member&gt; : Struct                  # anonymous inline struct — nested fields two spaces
///       &lt;nested member&gt; : &lt;Datatype&gt;     # deeper, same line grammar, arbitrary depth
/// ```
///
/// Member-line grammar is exactly <see cref="DbMemberLineFormat"/>'s own — same delimiters, same
/// fixed trailing-token order, same <c>SerializeMemberRecursive</c>/<c>ParseMemberRecursive</c>
/// indent recursion a DB's own MEMBERS section uses. Until 2026-07-16 this layer was flat while
/// the XML side (<see cref="Converter.SimaticMl.DbInterfaceMembers"/>'s ParseTypeMember/
/// WriteTypeMember) had recursed since 2026-07-14 — a real silent-loss gap, not a hard error as
/// this header used to claim: serialize dropped <see cref="DbMember.NestedMembers"/> entirely,
/// and parse mangled a nested line into a top-level member whose name began with the extra
/// indent. No `RETAIN`/`VERSION` line is ever *emitted* here (a real UDT member's XML carries no
/// `Remanence`/`Version` attribute at all, confirmed for `TypeDOL`); note the shared line grammar
/// would still *accept* a hand-authored ` RETAIN`, which the XML writer then has nowhere to put —
/// see `ir/SPEC.md`'s TYPE section for that documented edge.
/// </summary>
public static class TypeIrSerializer
{
    public static string Serialize(PlcTypeSource type)
    {
        var sb = new StringBuilder();
        sb.Append("TYPE ").Append(type.Name).Append('\n');
        sb.Append("  ROOTID ").Append(type.RootUId).Append('\n');

        if (!string.IsNullOrEmpty(type.Comment))
        {
            sb.Append("  COMMENT \"").Append(EscapeString(type.Comment)).Append("\"\n");
        }

        sb.Append("  MEMBERS\n");
        foreach (var member in type.Members)
        {
            DbMemberLineFormat.SerializeMemberRecursive(sb, "    ", member);
        }

        return sb.ToString();
    }

    private static string EscapeString(string value) => IrStringEscape.Escape(value);
}

public static class TypeIrParser
{
    public static PlcTypeSource ParseType(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var i = 0;

        var header = RequireLine(lines, ref i);
        if (!header.StartsWith("TYPE ", StringComparison.Ordinal))
        {
            throw new IrFormatException($"Expected 'TYPE <Name>', got: '{header}'");
        }

        var name = header["TYPE ".Length..];

        var rootUId = RequirePrefixedLine(lines, ref i, "  ROOTID ");

        string? comment = null;
        if (i < lines.Length && lines[i].StartsWith("  COMMENT \"", StringComparison.Ordinal))
        {
            comment = ParseQuotedString(lines[i]["  COMMENT ".Length..]);
            i++;
        }

        if (i >= lines.Length || lines[i] != "  MEMBERS")
        {
            throw new IrFormatException("Expected 'MEMBERS' section.");
        }

        i++;

        // Top-of-loop guard mirrors DbIrParser.ParseDb's MEMBERS loop exactly: only a 4-space
        // (not 6-space) line starts a new top-level member; ParseMemberRecursive consumes each
        // member's own deeper-indented nested lines itself.
        var members = new List<DbMember>();
        while (i < lines.Length && lines[i].StartsWith("    ", StringComparison.Ordinal) && !lines[i].StartsWith("      ", StringComparison.Ordinal))
        {
            members.Add(DbMemberLineFormat.ParseMemberRecursive(lines, ref i, "    "));
        }

        return new PlcTypeSource(rootUId, name, comment, members);
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

        return raw[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
    }
}
