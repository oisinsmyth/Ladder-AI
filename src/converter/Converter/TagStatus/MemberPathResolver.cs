using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.TagStatus;

// How far a dotted member path got when walked against the export's DB/UDT member trees.
public enum MemberPathOutcome
{
    // Every component resolved to a declared member.
    Resolved,

    // A component named a member the enclosing type genuinely does not declare — the anti-laundering
    // case (an invented member on a real DB).
    MemberAbsent,

    // Every component resolved, but a written subscript falls outside the member's declared array
    // bounds. A real defect distinct from an invented member: the member exists, the element doesn't.
    IndexOutOfRange,

    // The walk ran out of type information (a member typed by a UDT the export doesn't carry, an
    // instance-DB stub with no member tree). Existence is genuinely unverified — never guessed.
    NotEnumerable,
}

// Outcome plus, where it helps a reader, what exactly was wrong ("Unit[7] outside Array[0..3]").
public sealed record MemberPathResolution(MemberPathOutcome Outcome, string? Detail = null);

// Array-aware member-path resolution for `tagstatus` (FI-45 item 1, 2026-08-05).
//
// TagTypeRegistry.Resolve answers "what TYPE is this path?"; this answers "does this path exist, and
// if not, what is wrong with it?" — a question with four answers, not two, and the reason it is a
// separate walk rather than a null-check on Resolve. It reads its facts from the same
// TagTypeRegistry the type resolver uses (TryGetDb/TryGetUdt/Resolve), so there is still exactly one
// indexed corpus of DBs/UDTs/tags; only the diagnosis is local.
//
// The defect it fixes: an ARRAY OF UDT is the ordinary way to express N identical vessels
// (`DB_Params.Vessel[0].MaxNet`). The previous resolver never stripped a subscript from an
// intermediate component and never continued into the element type, so on any such project EVERY
// per-instance binding classified MEMBER-NOT-FOUND — hard rule 3's anti-laundering gate crying wolf
// at correct code, which is how a gate gets ignored into uselessness.
public static class MemberPathResolver
{
    // Types whose member namespace is closed and known: nothing can hang off them, so a further
    // component is invented, not merely unverifiable. Anything NOT listed here (a UDT missing from
    // the export, IEC_TIMER, a system type) stays NotEnumerable — this list only ever makes the
    // verdict stricter, so a name missing from it costs honesty, never a false accusation.
    private static readonly HashSet<string> ElementaryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bool", "Byte", "Word", "DWord", "LWord", "SInt", "USInt", "Int", "UInt", "DInt", "UDInt",
        "LInt", "ULInt", "Real", "LReal", "Time", "LTime", "S5Time", "Date", "Time_Of_Day", "TOD",
        "LTime_Of_Day", "LTOD", "Char", "WChar", "String", "WString",
    };

    /// <summary>
    /// Walk <paramref name="dottedPath"/>'s member part against the export. The ROOT's own existence
    /// is the caller's business (ProjectIndex owns that, so tagstatus and preflight cannot disagree);
    /// a single-component path therefore resolves trivially here.
    /// </summary>
    public static MemberPathResolution Resolve(string dottedPath, TagTypeRegistry registry)
    {
        // Plain Split('.'), matching TagTypeRegistry.Resolve: a literal-dot tag name (Clock_0.5Hz) is
        // settled by the caller's whole-name-first check before it ever reaches a member walk.
        var components = dottedPath.Split('.');
        if (components.Length == 1)
        {
            return new MemberPathResolution(MemberPathOutcome.Resolved);
        }

        var root = StripSubscript(components[0]);
        var rest = components[1..];

        if (registry.TryGetDb(root, out var db))
        {
            var members = AllMembers(db);
            return members.Count == 0
                ? new MemberPathResolution(MemberPathOutcome.NotEnumerable)   // instance-DB stub: no member tree exported
                : Walk(members, rest, registry);
        }

        // A tag whose own declared type is a UDT — or an array of one.
        if (registry.Resolve(root) is string rootType)
        {
            var subscript = Subscript(components[0]);
            if (subscript is not null
                && CheckIndex(root, subscript, rootType) is MemberPathResolution outOfRange)
            {
                return outOfRange;
            }

            if (registry.TryGetUdt(ElementTypeOf(rootType), out var rootUdt))
            {
                return Walk(rootUdt.Members, rest, registry);
            }
        }

        return new MemberPathResolution(MemberPathOutcome.NotEnumerable);
    }

    private static MemberPathResolution Walk(
        IReadOnlyList<DbMember> members, string[] components, TagTypeRegistry registry)
    {
        var name = StripSubscript(components[0]);
        var member = members.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal));
        if (member is null)
        {
            return new MemberPathResolution(MemberPathOutcome.MemberAbsent, $"no member '{name}'");
        }

        if (Subscript(components[0]) is string subscript
            && CheckIndex(name, subscript, member.Datatype) is MemberPathResolution outOfRange)
        {
            return outOfRange;
        }

        if (components.Length == 1)
        {
            return new MemberPathResolution(MemberPathOutcome.Resolved);
        }

        var rest = components[1..];

        // A structured member inlines its sub-members one level deep (same precedence as
        // TagTypeRegistry's own descent).
        if (member.NestedMembers is { Count: > 0 } nested)
        {
            return Walk(nested, rest, registry);
        }

        // THE UNINDEXED-FORM DECISION (deliberate, FI-45 item 1): the element type is used whether or
        // not a subscript was written, so `DB.Vessel.MaxNet` resolves exactly like `DB.Vessel[0]
        // .MaxNet`. Unindexed is read as a TYPE-LEVEL question — "does every element of Vessel carry
        // MaxNet?" — which is how a spec, a requirements register or a binding table names a member
        // common to all instances, and the answer is a real yes. The alternative (MEMBER-NOT-FOUND)
        // would say "invented" about something that demonstrably exists, which is the precise false
        // accusation this fix exists to remove. It deliberately asserts nothing about WHICH element:
        // bounds are only ever checked against a subscript actually written.
        var elementType = ElementTypeOf(member.Datatype);
        if (registry.TryGetUdt(elementType, out var udt))
        {
            return Walk(udt.Members, rest, registry);
        }

        // A closed elementary type can carry nothing further, so the next component is invented.
        // Anything else is an unknown namespace, reported as unverified rather than as a gap.
        return ElementaryTypes.Contains(StripQuotes(elementType).Trim())
            ? new MemberPathResolution(MemberPathOutcome.MemberAbsent, $"'{name}' is {elementType}, which has no members")
            : new MemberPathResolution(MemberPathOutcome.NotEnumerable);
    }

    private static IReadOnlyList<DbMember> AllMembers(DbSource db) =>
        db.Members
            .Concat(db.InputMembers ?? Array.Empty<DbMember>())
            .Concat(db.OutputMembers ?? Array.Empty<DbMember>())
            .Concat(db.InOutMembers)
            .ToList();

    // Non-null only when the subscript is provably outside the declared bounds. Every unverifiable
    // shape — a symbolic or variable index (`[#i]`, `[IDX_W]`), `Array[*]`, a dimension-count
    // mismatch, a declared type that isn't an array at all — returns null and lets the walk continue.
    // Silence on the unverifiable is the point: a bounds check that guesses is a bounds check people
    // learn to ignore.
    private static MemberPathResolution? CheckIndex(string name, string subscript, string datatype)
    {
        var bounds = ArrayBounds(datatype);
        if (bounds is null)
        {
            return null;
        }

        var indexes = subscript.Split(',');
        if (indexes.Length != bounds.Count)
        {
            return null;
        }

        for (var i = 0; i < indexes.Length; i++)
        {
            if (!int.TryParse(indexes[i].Trim(), out var index))
            {
                continue;
            }

            var (lo, hi) = bounds[i];
            if (index < lo || index > hi)
            {
                return new MemberPathResolution(
                    MemberPathOutcome.IndexOutOfRange,
                    $"{name}[{subscript}] is outside {datatype.Trim()}");
            }
        }

        return null;
    }

    // "Array[0..3] of \"UDT_X\"" → [(0,3)]; "Array[0..3, 1..2] of Int" → [(0,3),(1,2)].
    // null when the datatype isn't an array or its bounds aren't integer literals (`Array[*]`).
    private static IReadOnlyList<(int Lo, int Hi)>? ArrayBounds(string datatype)
    {
        var trimmed = datatype.Trim();
        if (!trimmed.StartsWith("Array", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var open = trimmed.IndexOf('[');
        var close = trimmed.IndexOf(']');
        if (open < 0 || close < open)
        {
            return null;
        }

        var bounds = new List<(int, int)>();
        foreach (var dimension in trimmed[(open + 1)..close].Split(','))
        {
            var parts = dimension.Split("..", StringSplitOptions.None);
            if (parts.Length != 2
                || !int.TryParse(parts[0].Trim(), out var lo)
                || !int.TryParse(parts[1].Trim(), out var hi))
            {
                return null;
            }

            bounds.Add((lo, hi));
        }

        return bounds;
    }

    // "Array[0..3] of \"UDT_X\"" → UDT_X; a non-array type is its own element type.
    private static string ElementTypeOf(string datatype)
    {
        var trimmed = datatype.Trim();
        if (!trimmed.StartsWith("Array", StringComparison.OrdinalIgnoreCase))
        {
            return StripQuotes(trimmed);
        }

        var ofIdx = trimmed.IndexOf(" of ", StringComparison.OrdinalIgnoreCase);
        return ofIdx < 0 ? StripQuotes(trimmed) : StripQuotes(trimmed[(ofIdx + 4)..].Trim());
    }

    private static string? Subscript(string component)
    {
        var open = component.IndexOf('[');
        var close = component.LastIndexOf(']');
        return open < 0 || close < open ? null : component[(open + 1)..close];
    }

    private static string StripSubscript(string component)
    {
        var idx = component.IndexOf('[');
        return idx < 0 ? component : component[..idx];
    }

    private static string StripQuotes(string type) => type.Trim('"');
}
