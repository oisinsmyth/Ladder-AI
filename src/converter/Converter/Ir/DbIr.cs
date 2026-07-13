using System.Text;
using Converter.SimaticMl;

namespace Converter.Ir;

/// <summary>
/// Text format for a DB (`ir/SPEC.md` "Tag tables, UDTs, DBs" — revised 2026-07-10 against real
/// exports; the original sketch had DB kind as a field and retention as per-DB, both wrong).
/// Reuses <see cref="DbSource"/>/<see cref="DbMember"/> directly as the IR model — no sidecar,
/// no reduction step: a DB has no wiring graph, so the parsed SimaticML shape already *is* the
/// readable form.
///
/// ```
/// DB &lt;Name&gt;
///   ROOTID &lt;id&gt;
///   NUMBER &lt;n&gt;
///   INSTANCEOF &lt;FBName&gt;              # only present for Instance DBs
///   COMMENT "&lt;text&gt;"                       # omitted if empty
///   MEMBERS
///     &lt;member&gt; : &lt;Datatype&gt;
///     &lt;member&gt; : &lt;Datatype&gt; RETAIN
///     &lt;member&gt; : &lt;Datatype&gt; = &lt;start value&gt;
///     &lt;member&gt; : &lt;Datatype&gt; RETAIN = &lt;start value&gt;
///     &lt;member&gt; : &lt;Datatype&gt; VERSION &lt;v&gt;   # structured members only (2026-07-11, Phase B)
///       &lt;nested member&gt; : &lt;Datatype&gt;         # one level deeper — structured members only
///       &lt;nested member&gt; : &lt;Datatype&gt; = &lt;start value&gt;
///     &lt;member&gt; : &lt;Datatype&gt; SETPOINT      # present only when the source's own SetPoint
///                                              # BooleanAttribute is true — verbatim, not a
///                                              # per-kind default (2026-07-11, Phase B: a real
///                                              # structured member disproved "always true")
/// ```
///
/// `Datatype` is written verbatim (e.g. `Array[0..14] of Bool`) — the " : "/" RETAIN"/"
/// SETPOINT"/" = "/" VERSION " delimiters are fixed strings the parser anchors on rather than
/// splitting on whitespace, so a multi-word datatype never needs escaping. A member line's
/// trailing tokens always appear in this fixed order when present: `VERSION`, then `RETAIN`, then
/// `SETPOINT`, then `= <start>` — parsed by stripping from the end (`= <start>` first, then
/// ` SETPOINT`, then ` RETAIN`, then ` VERSION <v>`), symmetric with how the serializer appends
/// them.
/// </summary>
public static class DbIrSerializer
{
    public static string Serialize(DbSource db)
    {
        var sb = new StringBuilder();
        sb.Append("DB ").Append(db.Name).Append('\n');
        sb.Append("  ROOTID ").Append(db.RootUId).Append('\n');
        sb.Append("  NUMBER ").Append(db.Number).Append('\n');
        if (db.InstanceOfName is not null)
        {
            sb.Append("  INSTANCEOF ").Append(db.InstanceOfName).Append('\n');
        }

        if (!string.IsNullOrEmpty(db.Comment))
        {
            sb.Append("  COMMENT \"").Append(EscapeString(db.Comment)).Append("\"\n");
        }

        sb.Append("  MEMBERS\n");
        foreach (var member in db.Members)
        {
            DbMemberLineFormat.SerializeMemberRecursive(sb, "    ", member);
        }

        return sb.ToString();
    }

    private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

public static class DbIrParser
{
    public static DbSource ParseDb(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var i = 0;

        var header = RequireLine(lines, ref i);
        if (!header.StartsWith("DB ", StringComparison.Ordinal))
        {
            throw new IrFormatException($"Expected 'DB <Name>', got: '{header}'");
        }

        var name = header["DB ".Length..];

        var rootUId = RequirePrefixedLine(lines, ref i, "  ROOTID ");
        var number = int.Parse(RequirePrefixedLine(lines, ref i, "  NUMBER "));

        string? instanceOfName = null;
        if (i < lines.Length && lines[i].StartsWith("  INSTANCEOF ", StringComparison.Ordinal))
        {
            instanceOfName = lines[i]["  INSTANCEOF ".Length..];
            i++;
        }

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
        while (i < lines.Length && lines[i].StartsWith("    ", StringComparison.Ordinal) && !lines[i].StartsWith("      ", StringComparison.Ordinal))
        {
            members.Add(DbMemberLineFormat.ParseMemberRecursive(lines, ref i, "    "));
        }

        return new DbSource(rootUId, name, number, instanceOfName, comment, members);
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
