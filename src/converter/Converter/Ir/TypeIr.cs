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
/// ```
///
/// Member-line grammar is exactly <see cref="DbMemberLineFormat"/>'s own — same delimiters, same
/// fixed trailing-token order. No `RETAIN`/`VERSION` lines or nested members are ever emitted
/// here: a UDT member is never Retain-flagged and never structured (confirmed real for `TypeDOL`;
/// a nested/structured UDT member would hard-error at parse time before reaching this far — see
/// <see cref="PlcTypeSourceParser"/>).
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
            DbMemberLineFormat.SerializeLine(sb, "    ", member);
        }

        return sb.ToString();
    }

    private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
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

        var members = new List<DbMember>();
        while (i < lines.Length && lines[i].StartsWith("    ", StringComparison.Ordinal))
        {
            members.Add(DbMemberLineFormat.ParseLine(lines[i], "    "));
            i++;
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
