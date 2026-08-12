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
///   MEMORYLAYOUT Standard|Optimized          # only present when the source declares one
///   COMMENT "&lt;text&gt;"                       # omitted if empty
///   INPUT                                    # only present when the source has one (2026-07-13)
///     &lt;member&gt; : &lt;Datatype&gt;
///   OUTPUT                                   # only present when the source has one
///     &lt;member&gt; : &lt;Datatype&gt;
///   INOUT                                    # only shown when non-empty (never seen populated)
///     &lt;member&gt; : &lt;Datatype&gt;
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
///     &lt;member&gt; : &lt;Datatype&gt; EXTERNALACCESSIBLE=FALSE   # present only when the source's own
///     &lt;member&gt; : &lt;Datatype&gt; EXTERNALVISIBLE=FALSE     # ExternalAccessible/Visible/Writable
///     &lt;member&gt; : &lt;Datatype&gt; EXTERNALWRITABLE=FALSE     # BooleanAttribute is false — all
///                                              # three default true (2026-07-14: `FB VSDSim`'s
///                                              # own `SpeedCalcArray` disproved "always true" for
///                                              # ExternalAccessible specifically)
/// ```
///
/// `Datatype` is written verbatim (e.g. `Array[0..14] of Bool`) — the " : "/" RETAIN"/"
/// SETPOINT"/" = "/" VERSION " delimiters are fixed strings the parser anchors on rather than
/// splitting on whitespace, so a multi-word datatype never needs escaping. A member line's
/// trailing tokens always appear in this fixed order when present: `VERSION`, then `RETAIN`, then
/// `SETPOINT`, then `EXTERNALACCESSIBLE=FALSE`, then `EXTERNALVISIBLE=FALSE`, then
/// `EXTERNALWRITABLE=FALSE`, then `= <start>` — parsed by stripping from the end (`= <start>`
/// first, then each `EXTERNAL*=FALSE` marker rightmost-first, then ` SETPOINT`, then ` RETAIN`,
/// then ` VERSION <v>`), symmetric with how the serializer appends them.
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

        // MEMORYLAYOUT (2026-08-12) — written only when the model carries one, so every `.ir`
        // predating this stays byte-identical and keeps emitting no <MemoryLayout> element.
        if (db.MemoryLayout is not null)
        {
            sb.Append("  MEMORYLAYOUT ").Append(db.MemoryLayout).Append('\n');
        }

        if (!string.IsNullOrEmpty(db.Comment))
        {
            sb.Append("  COMMENT \"").Append(EscapeString(db.Comment)).Append("\"\n");
        }

        SerializeOptionalMemberSection(sb, "INPUT", db.InputMembers);
        SerializeOptionalMemberSection(sb, "OUTPUT", db.OutputMembers);
        SerializeMemberSection(sb, "INOUT", db.InOutMembers);

        sb.Append("  MEMBERS\n");
        foreach (var member in db.Members)
        {
            DbMemberLineFormat.SerializeMemberRecursive(sb, "    ", member);
        }

        return sb.ToString();
    }

    private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // Null vs. present-but-empty is a real, must-preserve distinction (mirrors IrSerializer's own
    // InputMembers/OutputMembers handling for BlockSource) — a section header with zero member
    // lines still gets emitted when the source had the section present-but-empty, distinct from
    // the section being entirely absent (null).
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

    // InOut is never null (no real example of it being entirely absent has been seen) — an empty
    // list is the only "nothing to say" case, so the header itself is simply omitted when empty.
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

        // Parsed between INSTANCEOF and COMMENT, matching DbIrSerializer's own emission order.
        // Optional: absence is the pre-2026-08-12 shape and means "no opinion", not a default.
        string? memoryLayout = null;
        if (i < lines.Length && lines[i].StartsWith("  MEMORYLAYOUT ", StringComparison.Ordinal))
        {
            memoryLayout = lines[i]["  MEMORYLAYOUT ".Length..];
            if (!BlockMemoryLayout.IsKnownValue(memoryLayout))
            {
                throw new IrFormatException(
                    $"MEMORYLAYOUT '{memoryLayout}' is not a known memory layout — expected " +
                    $"'{BlockMemoryLayout.Standard}' or '{BlockMemoryLayout.Optimized}'.");
            }

            i++;
        }

        string? comment = null;
        if (i < lines.Length && lines[i].StartsWith("  COMMENT \"", StringComparison.Ordinal))
        {
            comment = ParseQuotedString(lines[i]["  COMMENT ".Length..]);
            i++;
        }

        var inputMembers = ParseOptionalMemberSection(lines, ref i, "  INPUT");
        var outputMembers = ParseOptionalMemberSection(lines, ref i, "  OUTPUT");
        var inOutMembers = ParseOptionalMemberSection(lines, ref i, "  INOUT") ?? Array.Empty<DbMember>();

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

        return new DbSource(rootUId, name, number, instanceOfName, comment, members, inputMembers, outputMembers, inOutMembers, memoryLayout);
    }

    // Shared flat member-section parser for Input/Output/InOut — mirrors IrParser's own private
    // helper of the same name/behavior for BlockSource (not reusable directly — different
    // partial class). None of these ever carries nested content in any grounded example (S1 item
    // 20), so no nested-member look-ahead is needed here (contrast the MEMBERS loop above, which
    // does support nesting for Static). Returns null when the section header itself is absent,
    // preserving the null-vs-present-but-empty distinction DbIrSerializer relies on.
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
