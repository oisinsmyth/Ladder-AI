using System.Text;
using Converter.SimaticMl;

namespace Converter.Ir;

/// <summary>
/// Shared text-line format for a `DbMember` (`ir/SPEC.md` "Tag tables, UDTs, DBs" member-line
/// grammar) — used identically by a DB's own `MEMBERS` section (<see cref="DbIrSerializer"/>/
/// <see cref="DbIrParser"/>) and a block's `INTERFACE` `STATIC`/`TEMP` sections (confirmed real,
/// 2026-07-11, S1 item 7 Phase B: same underlying XML shape, same IR line grammar, one member per
/// line: `&lt;name&gt; : &lt;Datatype&gt;[ VERSION &lt;v&gt;][ RETAIN][ SETPOINT][
/// EXTERNALACCESSIBLE=FALSE][ EXTERNALVISIBLE=FALSE][ EXTERNALWRITABLE=FALSE][ = &lt;start
/// value&gt;]`, nested members one level of indentation deeper).
/// </summary>
internal static partial class DbMemberLineFormat
{
    public static void SerializeLine(StringBuilder sb, string indent, DbMember member)
    {
        sb.Append(indent).Append(member.Name).Append(" : ").Append(member.Datatype);
        if (member.IsBareParameter)
        {
            sb.Append(" BAREPARAM");
        }

        // Informative/InformativeComment: confirmed real 2026-07-14, `OB1 Main`'s own system
        // parameters — see DbModel.cs's own doc comment. Only ever seen alongside IsBareParameter,
        // so no independent handling is needed for the non-bare case.
        if (member.Informative)
        {
            sb.Append(" INFORMATIVE \"").Append(EscapeString(member.InformativeComment ?? string.Empty)).Append('"');
        }

        if (member.Version is not null)
        {
            sb.Append(" VERSION ").Append(member.Version);
        }

        if (member.Retain)
        {
            sb.Append(" RETAIN");
        }

        if (member.SetPoint)
        {
            sb.Append(" SETPOINT");
        }

        // ExternalAccessible/Visible/Writable: default true (the overwhelming majority of
        // members), so only emitted when false. Deliberately diverges from TagTableIrSerializer's
        // own ACCESSIBLE/VISIBLE/WRITABLE (shown when *true*, same literal-value convention as
        // SETPOINT/RETAIN/BAREPARAM) — that choice was never actually tested against a real false
        // tag (all 10 grounding tags have all three true), whereas a real false *is* confirmed
        // here (`FB VSDSim`'s own `SpeedCalcArray`, 2026-07-14). Following the tag convention
        // literally would put this marker on nearly every member line in every already-committed
        // DB, for a case that's true in every member seen but one — see DbModel.cs's own DbMember
        // doc comment.
        if (!member.ExternalAccessible)
        {
            sb.Append(" EXTERNALACCESSIBLE=FALSE");
        }

        if (!member.ExternalVisible)
        {
            sb.Append(" EXTERNALVISIBLE=FALSE");
        }

        if (!member.ExternalWritable)
        {
            sb.Append(" EXTERNALWRITABLE=FALSE");
        }

        if (member.StartValue is not null)
        {
            sb.Append(" = ").Append(member.StartValue);
        }

        // Comment: an ordinary (non-bare) member's own "why" annotation — confirmed real
        // 2026-07-15 (see DbModel.cs's own DbMember.Comment doc comment). Appended as the
        // absolute last token, after StartValue, deliberately — StartValue's own extraction in
        // ParseLine below finds its leftmost " = " assuming nothing but the value itself follows;
        // free-text comment prose routinely contains "=" (e.g. "IO.Step = 10"), so Comment must be
        // peeled off the end *before* that search runs, not interleaved earlier in the line.
        if (member.Comment is not null)
        {
            sb.Append(" COMMENT \"").Append(EscapeString(member.Comment)).Append('"');
        }

        sb.Append('\n');
    }

    // Shared by DbIrSerializer (standalone DB files) and IrSerializer (a block's own STATIC
    // section) — both reuse this rather than duplicating it, after a real bug (2026-07-14, `FB
    // ShredderControlSystem`'s own `ComsOutByte501`, a Struct member nesting a further Struct-typed
    // field three levels deep) was found in each's own separate, still-two-level-only copy.
    // Recurses to arbitrary depth, each level two spaces deeper than its parent.
    public static void SerializeMemberRecursive(StringBuilder sb, string indent, DbMember member)
    {
        SerializeLine(sb, indent, member);
        var childIndent = indent + "  ";

        // Subelement lines FIRST, then nested members — mirroring the XML, where <Subelement>
        // children precede nested <Member> children. A subelement line is `[<path>] = <value>`; the
        // leading '[' is what tells it apart from a nested member line, and the discrimination is
        // total because a SIMATIC member name can never begin with one.
        foreach (var subelement in member.Subelements)
        {
            sb.Append(childIndent).Append('[').Append(subelement.Path).Append("] = ").Append(subelement.StartValue).Append('\n');
        }

        if (member.NestedMembers is not null)
        {
            foreach (var nested in member.NestedMembers)
            {
                SerializeMemberRecursive(sb, childIndent, nested);
            }
        }
    }

    // Shared by DbIrParser and IrParser — mirrors SerializeMemberRecursive's own indent-based
    // recursion exactly.
    public static DbMember ParseMemberRecursive(string[] lines, ref int i, string indent)
    {
        var member = ParseLine(lines[i], indent);
        i++;

        var childIndent = indent + "  ";
        var nestedMembers = new List<DbMember>();
        var subelements = new List<DbSubelement>();
        while (i < lines.Length && lines[i].StartsWith(childIndent, StringComparison.Ordinal))
        {
            // `[<path>] = <value>` is an ARRAY ELEMENT START VALUE, not a nested member. Accepted in
            // either order relative to nested member lines (the serializer emits subelements first)
            // since the leading '[' decides, not position.
            if (lines[i][childIndent.Length..].StartsWith('['))
            {
                subelements.Add(ParseSubelementLine(lines[i], childIndent));
                i++;
                continue;
            }

            nestedMembers.Add(ParseMemberRecursive(lines, ref i, childIndent));
        }

        if (nestedMembers.Count > 0)
        {
            member = member with { NestedMembers = nestedMembers };
        }

        return subelements.Count > 0 ? member with { Subelements = subelements } : member;
    }

    private static DbSubelement ParseSubelementLine(string line, string indent)
    {
        var content = line[indent.Length..];
        var close = content.IndexOf("] = ", StringComparison.Ordinal);
        if (close < 0)
        {
            throw new IrFormatException($"Expected an array start-value line '[<path>] = <value>', got: '{line}'");
        }

        var path = content[1..close];
        if (path.Length == 0)
        {
            throw new IrFormatException($"Array start-value line has an empty index path: '{line}'");
        }

        return new DbSubelement(path, content[(close + 4)..]);
    }

    public static DbMember ParseLine(string line, string indent)
    {
        var content = line[indent.Length..];
        var colonIndex = content.IndexOf(" : ", StringComparison.Ordinal);
        if (colonIndex < 0)
        {
            throw new IrFormatException($"Expected '<member> : <Datatype>', got: '{line}'");
        }

        var name = content[..colonIndex];
        var rest = content[(colonIndex + 3)..];

        // Comment is the last-appended token (SerializeLine, above) specifically so it can be
        // peeled off first, here, before StartValue's own leftmost-" = "-search runs — otherwise
        // an "=" inside free-text comment prose would be misread as the start-value separator.
        string? comment = null;
        var commentMatch = CommentSuffixRegex().Match(rest);
        if (commentMatch.Success)
        {
            comment = UnescapeString(commentMatch.Groups["text"].Value);
            rest = rest[..commentMatch.Index];
        }

        string? startValue = null;
        var eqIndex = rest.IndexOf(" = ", StringComparison.Ordinal);
        if (eqIndex >= 0)
        {
            startValue = rest[(eqIndex + 3)..];
            rest = rest[..eqIndex];
        }

        // ExternalAccessible/Visible/Writable: appended after SETPOINT in SerializeLine, so
        // peeled before it here (rightmost-first, same reverse-order discipline as every other
        // trailing marker in this format). Each is independently optional.
        var externalWritable = true;
        if (rest.EndsWith(" EXTERNALWRITABLE=FALSE", StringComparison.Ordinal))
        {
            externalWritable = false;
            rest = rest[..^" EXTERNALWRITABLE=FALSE".Length];
        }

        var externalVisible = true;
        if (rest.EndsWith(" EXTERNALVISIBLE=FALSE", StringComparison.Ordinal))
        {
            externalVisible = false;
            rest = rest[..^" EXTERNALVISIBLE=FALSE".Length];
        }

        var externalAccessible = true;
        if (rest.EndsWith(" EXTERNALACCESSIBLE=FALSE", StringComparison.Ordinal))
        {
            externalAccessible = false;
            rest = rest[..^" EXTERNALACCESSIBLE=FALSE".Length];
        }

        var setPoint = false;
        if (rest.EndsWith(" SETPOINT", StringComparison.Ordinal))
        {
            setPoint = true;
            rest = rest[..^" SETPOINT".Length];
        }

        var retain = false;
        if (rest.EndsWith(" RETAIN", StringComparison.Ordinal))
        {
            retain = true;
            rest = rest[..^" RETAIN".Length];
        }

        string? version = null;
        var versionIndex = rest.IndexOf(" VERSION ", StringComparison.Ordinal);
        if (versionIndex >= 0)
        {
            version = rest[(versionIndex + " VERSION ".Length)..];
            rest = rest[..versionIndex];
        }

        // IsBareParameter: confirmed real 2026-07-14 (`FC Scale`'s own Input/Output params, S1
        // item 20) — a genuinely minimal member shape (no Remanence attribute, no AttributeList at
        // all) distinct from an ordinary member that happens to have Retain/SetPoint both false.
        // Real bug, found live via the full export/convert/import/compile/re-export cycle run
        // against every block already in the scratch project (`AnalogScale`): this line format
        // never carried the flag at all, so a bare parameter silently reverted to the ordinary
        // shape crossing the to-ir/to-xml boundary, and TIA's own Import() then refused the
        // resulting (wrong) `Remanence` attribute outright.
        // Informative/InformativeComment: confirmed real 2026-07-14, `OB1 Main`'s own system
        // parameters — see DbModel.cs's own doc comment. Peeled before BAREPARAM (its own append
        // came after BAREPARAM's), matching the reverse-order peeling convention used throughout
        // this method.
        var informative = false;
        string? informativeComment = null;
        var informativeMatch = InformativeSuffixRegex().Match(rest);
        if (informativeMatch.Success)
        {
            informative = true;
            informativeComment = UnescapeString(informativeMatch.Groups["text"].Value);
            rest = rest[..informativeMatch.Index];
        }

        var isBareParameter = rest.EndsWith(" BAREPARAM", StringComparison.Ordinal);
        if (isBareParameter)
        {
            rest = rest[..^" BAREPARAM".Length];
        }

        return new DbMember(
            name, rest, retain, startValue, version, setPoint,
            NestedMembers: null, IsBareParameter: isBareParameter, Informative: informative, InformativeComment: informativeComment,
            ExternalAccessible: externalAccessible, ExternalVisible: externalVisible, ExternalWritable: externalWritable,
            Comment: comment);
    }

    [System.Text.RegularExpressions.GeneratedRegex(" INFORMATIVE \"(?<text>.*)\"$")]
    private static partial System.Text.RegularExpressions.Regex InformativeSuffixRegex();

    [System.Text.RegularExpressions.GeneratedRegex(" COMMENT \"(?<text>.*)\"$")]
    private static partial System.Text.RegularExpressions.Regex CommentSuffixRegex();

    private static string EscapeString(string value) => IrStringEscape.Escape(value);

    private static string UnescapeString(string value) => IrStringEscape.Unescape(value);
}
