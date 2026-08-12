using System.Text;
using Converter.SimaticMl;

namespace Converter.Ir;

/// <summary>
/// Text format for a PLC tag table (`ir/SPEC.md` — extend "Tag tables, UDTs, DBs" once this
/// lands). Reuses <see cref="PlcTagTableSource"/>/<see cref="PlcTagSource"/> directly as the IR
/// model — no sidecar, no reduction step, same "the parsed SimaticML shape already is the
/// readable form" reasoning as <see cref="DbIrSerializer"/>/<see cref="TypeIrSerializer"/>.
///
/// ```
/// TAGTABLE &lt;Name&gt;
///   ROOTID &lt;id&gt;
///   TAGS
///     &lt;tag&gt; &lt;id&gt; : &lt;DataTypeName&gt; @ &lt;LogicalAddress&gt; [ACCESSIBLE] [VISIBLE] [WRITABLE] [COMMENT "&lt;text&gt;"]
/// ```
///
/// `ACCESSIBLE`/`VISIBLE`/`WRITABLE` are shown only when true, same convention as
/// <see cref="DbMemberLineFormat"/>'s own `SETPOINT` — omitted (not assumed false) once a real
/// counter-example is seen; every one of the 10 real tags grounding this (`Default tag table`,
/// `station_2/JOB9002_PLC`) has all three true.
/// </summary>
public static class TagTableIrSerializer
{
    public static string Serialize(PlcTagTableSource tagTable)
    {
        var sb = new StringBuilder();
        sb.Append("TAGTABLE ").Append(tagTable.Name).Append('\n');
        sb.Append("  ROOTID ").Append(tagTable.RootUId).Append('\n');
        sb.Append("  TAGS\n");

        foreach (var tag in tagTable.Tags)
        {
            sb.Append("    ").Append(tag.Name).Append(' ').Append(tag.RootUId)
                .Append(" : ").Append(tag.DataTypeName)
                .Append(" @ ").Append(tag.LogicalAddress);

            if (tag.ExternalAccessible)
            {
                sb.Append(" ACCESSIBLE");
            }

            if (tag.ExternalVisible)
            {
                sb.Append(" VISIBLE");
            }

            if (tag.ExternalWritable)
            {
                sb.Append(" WRITABLE");
            }

            if (!string.IsNullOrEmpty(tag.Comment))
            {
                sb.Append(" COMMENT \"").Append(EscapeString(tag.Comment)).Append('"');
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static string EscapeString(string value) => IrStringEscape.Escape(value);
}

public static class TagTableIrParser
{
    public static PlcTagTableSource ParseTagTable(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var i = 0;

        var header = RequireLine(lines, ref i);
        if (!header.StartsWith("TAGTABLE ", StringComparison.Ordinal))
        {
            throw new IrFormatException($"Expected 'TAGTABLE <Name>', got: '{header}'");
        }

        var name = header["TAGTABLE ".Length..];
        var rootUId = RequirePrefixedLine(lines, ref i, "  ROOTID ");

        if (i >= lines.Length || lines[i] != "  TAGS")
        {
            throw new IrFormatException("Expected 'TAGS' section.");
        }

        i++;

        var tags = new List<PlcTagSource>();
        while (i < lines.Length && lines[i].StartsWith("    ", StringComparison.Ordinal))
        {
            tags.Add(ParseTagLine(lines[i]));
            i++;
        }

        return new PlcTagTableSource(rootUId, name, tags);
    }

    private static PlcTagSource ParseTagLine(string line)
    {
        var content = line["    ".Length..];

        var spaceIdx = content.IndexOf(' ');
        if (spaceIdx < 0)
        {
            throw new IrFormatException($"Malformed tag line: '{line}'");
        }

        var name = content[..spaceIdx];
        var rest = content[(spaceIdx + 1)..];

        var colonIdx = rest.IndexOf(" : ", StringComparison.Ordinal);
        if (colonIdx < 0)
        {
            throw new IrFormatException($"Malformed tag line (expected ' : '): '{line}'");
        }

        var rootUId = rest[..colonIdx];
        rest = rest[(colonIdx + " : ".Length)..];

        var atIdx = rest.IndexOf(" @ ", StringComparison.Ordinal);
        if (atIdx < 0)
        {
            throw new IrFormatException($"Malformed tag line (expected ' @ '): '{line}'");
        }

        var dataTypeName = rest[..atIdx];
        rest = rest[(atIdx + " @ ".Length)..];

        var tokens = TokenizeRemainder(rest, out var logicalAddress);

        var accessible = tokens.Remove("ACCESSIBLE");
        var visible = tokens.Remove("VISIBLE");
        var writable = tokens.Remove("WRITABLE");

        string? comment = null;
        var commentToken = tokens.FirstOrDefault(t => t.StartsWith("COMMENT \"", StringComparison.Ordinal));
        if (commentToken is not null)
        {
            comment = ParseQuotedString(commentToken["COMMENT ".Length..]);
        }

        return new PlcTagSource(rootUId, name, dataTypeName, logicalAddress, accessible, visible, writable, comment);
    }

    // The LogicalAddress itself has no internal spaces (confirmed real, e.g. "%IW64"), so the
    // first token is always it; everything after is trailing flags/COMMENT, but COMMENT's own
    // quoted text can contain spaces, so it's kept as one combined trailing token rather than
    // split on every space.
    private static List<string> TokenizeRemainder(string rest, out string logicalAddress)
    {
        var firstSpace = rest.IndexOf(' ');
        if (firstSpace < 0)
        {
            logicalAddress = rest;
            return new List<string>();
        }

        logicalAddress = rest[..firstSpace];
        var remainder = rest[(firstSpace + 1)..];

        var tokens = new List<string>();
        var commentIdx = remainder.IndexOf("COMMENT \"", StringComparison.Ordinal);
        if (commentIdx < 0)
        {
            tokens.AddRange(remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
        else
        {
            tokens.AddRange(remainder[..commentIdx].Split(' ', StringSplitOptions.RemoveEmptyEntries));
            tokens.Add(remainder[commentIdx..]);
        }

        return tokens;
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
