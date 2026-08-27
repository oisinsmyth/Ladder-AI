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
// ResolvedType is the declared type the walk ENDED on, populated only on Resolved. It exists so a
// trailing C-501 bit slice (".%X3") can be bounds-checked against the thing it slices without a second
// walk - and so a caller can tell "resolved, and I know its type" from "resolved, type unknown".
public sealed record MemberPathResolution(
    MemberPathOutcome Outcome, string? Detail = null, string? ResolvedType = null);

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
        // Bracket-aware, matching TagTypeRegistry.Resolve: a literal-dot tag name (Clock_0.5Hz) is
        // settled by the caller's whole-name-first check before it ever reaches a member walk, and a
        // SYMBOLIC array subscript is itself a dotted path that must stay inside its own component.
        // Was a plain Split('.') and reported "no member 'Sequence'" for
        // `DB_Config.Profile[iDB_Unit_A.Cycle.ChosenIndex]` — hard rule 3's
        // anti-laundering gate accusing wiring tagstatus itself confirms exists (live run,
        // 2026-08-27; the same edit with a literal `[1]` subscript resolved clean).
        var components = TagPath.Split(dottedPath);

        // A trailing ".%X3" is a SLICE, not a member: C-501 alarm-bit addressing, carried on the access
        // as SliceAccessModifier and re-appended by AccessNode.DottedPath. Walked as a component it
        // reported MEMBER-NOT-FOUND for EVERY alarm bit in the corpus - hard rule 3's anti-laundering
        // gate accusing the one construct doc 06 documents an exception for. Found 2026-08-14 by the
        // corpus sweep that validated preflight's member resolution; the defect was tagstatus's, and
        // predates it. Split the slice off first, resolve what it slices, then bounds-check the bit.
        if (SliceOf(components) is string slice)
        {
            return CheckSlice(Resolve(string.Join('.', components[..^1]), registry), slice, dottedPath);
        }

        if (components.Length == 1)
        {
            return new MemberPathResolution(MemberPathOutcome.Resolved, ResolvedType: registry.Resolve(dottedPath));
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

    /// <summary>
    /// The same walk, rooted at a BLOCK-LOCAL declaration instead of a project DB or tag (2026-08-14).
    /// <para>
    /// `preflight` resolved tag references to their ROOT only, so a block reading
    /// <c>IO.ThisMemberDoesNotExist</c> — where <c>IO</c> is the block's own UDT-typed STATIC, which is
    /// this project's house style (C-132) — reported CLEAN. The local root resolves, and nothing looked
    /// further. The registry cannot answer for a local because a local is not in it; the block's own
    /// declaration is, and it carries the type.
    /// </para>
    /// <para>
    /// <paramref name="rootDeclaration"/> is passed to <c>Walk</c> as the single candidate member, so
    /// subscript bounds, inline nested members, array-of-UDT descent and the elementary-type stop all
    /// behave exactly as they do for a global root — one walk, not a second implementation that can
    /// drift from it.
    /// </para>
    /// </summary>
    public static MemberPathResolution ResolveUnderLocal(
        string dottedPath, DbMember rootDeclaration, TagTypeRegistry registry)
    {
        var components = TagPath.Split(dottedPath);
        if (SliceOf(components) is string slice)
        {
            return CheckSlice(
                ResolveUnderLocal(string.Join('.', components[..^1]), rootDeclaration, registry),
                slice, dottedPath);
        }

        return components.Length == 1
            ? new MemberPathResolution(MemberPathOutcome.Resolved, ResolvedType: rootDeclaration.Datatype)
            : Walk(new[] { rootDeclaration }, components, registry);
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
            return new MemberPathResolution(MemberPathOutcome.Resolved, ResolvedType: member.Datatype);
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

    // Bit widths of the types a ".%Xn" slice can legally address. Anything ABSENT - Real, a UDT, a type
    // the export does not carry - is neither range-checked nor accused: the slice is accepted and the
    // base walk's own verdict stands. Like ElementaryTypes above, this table only ever makes the
    // verdict stricter, so a name missing from it costs a check, never a false accusation.
    private static readonly Dictionary<string, int> BitWidths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bool"] = 1,
        ["Byte"] = 8, ["SInt"] = 8, ["USInt"] = 8, ["Char"] = 8,
        ["Word"] = 16, ["Int"] = 16, ["UInt"] = 16, ["WChar"] = 16,
        ["DWord"] = 32, ["DInt"] = 32, ["UDInt"] = 32,
        ["LWord"] = 64, ["LInt"] = 64, ["ULInt"] = 64,
    };

    // The final component when it is an access-level slice suffix rather than a member name. Only a
    // LAST component qualifies: SliceAccessModifier was observed on the last <Component> and nowhere
    // else, and FlgNetParser rejects it mid-path outright, so anything mid-path is not this.
    private static string? SliceOf(string[] components) =>
        components.Length > 1 && components[^1].StartsWith('%') ? components[^1] : null;

    // A slice is only ever judged once the thing it slices has RESOLVED. If the base walk could not
    // resolve, its verdict IS the answer: accusing the slice as well would report one defect twice, in
    // two vocabularies, and send the reader after the wrong half.
    private static MemberPathResolution CheckSlice(
        MemberPathResolution baseResolution, string slice, string dottedPath)
    {
        if (baseResolution.Outcome != MemberPathOutcome.Resolved)
        {
            return baseResolution;
        }

        // Only "%X<n>" is range-checked. %B/%W/%D slices exist in TIA and appear in no committed .ir,
        // so they are accepted UNCHECKED rather than judged against a rule this project has never seen
        // exercised - an unverifiable slice is accepted, never accused.
        if (slice.Length < 3
            || !(slice[1] is 'X' or 'x')
            || !int.TryParse(slice[2..], out var bit)
            || baseResolution.ResolvedType is not string type
            || !BitWidths.TryGetValue(StripQuotes(ElementTypeOf(type)).Trim(), out var width))
        {
            return new MemberPathResolution(MemberPathOutcome.Resolved);
        }

        return bit >= 0 && bit < width
            ? new MemberPathResolution(MemberPathOutcome.Resolved)
            : new MemberPathResolution(
                MemberPathOutcome.IndexOutOfRange,
                $"{dottedPath} addresses bit {bit} of a {type.Trim()}, which has {width}");
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

    // Component-level subscript handling is TagPath's — one mechanism, so a symbolic index is
    // stripped exactly where a literal one is (2026-08-27).
    private static string? Subscript(string component) => TagPath.SubscriptOf(component);

    private static string StripSubscript(string component) => TagPath.StripComponentSubscript(component);

    private static string StripQuotes(string type) => type.Trim('"');
}
