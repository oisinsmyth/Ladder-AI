using System.Text;
using Converter.SimaticMl;

namespace Converter.Ir;

/// <summary>
/// Shared text-line format for a `DbMember` (`ir/SPEC.md` "Tag tables, UDTs, DBs" member-line
/// grammar) — used identically by a DB's own `MEMBERS` section (<see cref="DbIrSerializer"/>/
/// <see cref="DbIrParser"/>) and a block's `INTERFACE` `STATIC`/`TEMP` sections (confirmed real,
/// 2026-07-11, S1 item 7 Phase B: same underlying XML shape, same IR line grammar, one member per
/// line: `&lt;name&gt; : &lt;Datatype&gt;[ VERSION &lt;v&gt;][ RETAIN][ SETPOINT][ = &lt;start
/// value&gt;]`, nested members one level of indentation deeper).
/// </summary>
internal static class DbMemberLineFormat
{
    public static void SerializeLine(StringBuilder sb, string indent, DbMember member)
    {
        sb.Append(indent).Append(member.Name).Append(" : ").Append(member.Datatype);
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

        if (member.StartValue is not null)
        {
            sb.Append(" = ").Append(member.StartValue);
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
        if (member.NestedMembers is not null)
        {
            var childIndent = indent + "  ";
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
        while (i < lines.Length && lines[i].StartsWith(childIndent, StringComparison.Ordinal))
        {
            nestedMembers.Add(ParseMemberRecursive(lines, ref i, childIndent));
        }

        return nestedMembers.Count > 0 ? member with { NestedMembers = nestedMembers } : member;
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

        string? startValue = null;
        var eqIndex = rest.IndexOf(" = ", StringComparison.Ordinal);
        if (eqIndex >= 0)
        {
            startValue = rest[(eqIndex + 3)..];
            rest = rest[..eqIndex];
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

        return new DbMember(name, rest, retain, startValue, version, setPoint);
    }
}
