using Converter.SimaticMl;

namespace Converter.Ir;

// Maps a dotted tag/member path → its datatype, sourced from the batch's (and --project's)
// DB/UDT/tag-table .ir files. The tag/member-type sibling of CalleeInterfaceRegistry: it lets
// `to-xml --synthesize` mint the type-carrying box/compare parts the readable IR deliberately omits
// (ADR-0001) — a CONVERT's Src/DestType, a WAND/ABS/SWAP's SrcType, a comparison's SrcType — by
// looking the operand's type up here instead of guessing from magnitude. Mirrors what the read side
// gets from the source XML's own type attributes.
//
// Resolution walks the same DB/UDT member models ProjectIndex already parses (but keeps the types
// ProjectIndex discards): a dotted path's root resolves as a tag-table tag or a DB, and each further
// component descends through a structured member's inlined NestedMembers or, failing that, the UDT
// its Datatype names. Unknown paths return null — the caller decides whether that's a hard error
// (a type it must emit) rather than this silently guessing.
public sealed class TagTypeRegistry
{
    private readonly IReadOnlyDictionary<string, string> _tagTypes;
    private readonly IReadOnlyDictionary<string, DbSource> _dbs;
    private readonly IReadOnlyDictionary<string, PlcTypeSource> _udts;
    private readonly IReadOnlyList<DbMember> _localMembers;

    private TagTypeRegistry(
        IReadOnlyDictionary<string, string> tagTypes,
        IReadOnlyDictionary<string, DbSource> dbs,
        IReadOnlyDictionary<string, PlcTypeSource> udts,
        IReadOnlyList<DbMember> localMembers)
    {
        _tagTypes = tagTypes;
        _dbs = dbs;
        _udts = udts;
        _localMembers = localMembers;
    }

    public static readonly TagTypeRegistry Empty = new(
        new Dictionary<string, string>(StringComparer.Ordinal),
        new Dictionary<string, DbSource>(StringComparer.Ordinal),
        new Dictionary<string, PlcTypeSource>(StringComparer.Ordinal),
        Array.Empty<DbMember>());

    // A copy that also resolves the enclosing block's OWN interface members (a bare `SignedValue`, a
    // `IO.Ready` struct field) — these carry types in the block being synthesized, not in a separate
    // DB, so SynthesizeBlock layers them on top of the project-wide sources. Local members take
    // priority: they're the innermost namespace, exactly the LocalVariable scope ScopeFor assigns.
    public TagTypeRegistry WithLocalMembers(IEnumerable<DbMember> localMembers) =>
        new(_tagTypes, _dbs, _udts, localMembers.ToList());

    public static TagTypeRegistry FromSources(
        IEnumerable<DbSource> dbs, IEnumerable<PlcTypeSource> udts, IEnumerable<PlcTagSource> tags)
    {
        var dbMap = new Dictionary<string, DbSource>(StringComparer.Ordinal);
        foreach (var db in dbs)
        {
            dbMap[db.Name] = db;
        }

        var udtMap = new Dictionary<string, PlcTypeSource>(StringComparer.Ordinal);
        foreach (var udt in udts)
        {
            udtMap[udt.Name] = udt;
        }

        var tagMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            tagMap[tag.Name] = tag.DataTypeName;
        }

        return new TagTypeRegistry(tagMap, dbMap, udtMap, Array.Empty<DbMember>());
    }

    // Builds from a set of .ir files, same DB/TYPE/TAGTABLE prefix dispatch as ProjectIndex. A file
    // that doesn't parse (or isn't a DB/UDT/tag-table) is skipped — the registry is a best-effort
    // type source, and an unresolvable operand simply returns null, not an error here.
    public static TagTypeRegistry FromFiles(IEnumerable<string> paths)
    {
        var dbs = new List<DbSource>();
        var udts = new List<PlcTypeSource>();
        var tags = new List<PlcTagSource>();

        foreach (var path in paths)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException)
            {
                continue;
            }

            try
            {
                if (text.StartsWith("DB ", StringComparison.Ordinal))
                {
                    dbs.Add(DbIrParser.ParseDb(text));
                }
                else if (text.StartsWith("TYPE ", StringComparison.Ordinal))
                {
                    udts.Add(TypeIrParser.ParseType(text));
                }
                else if (text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
                {
                    tags.AddRange(TagTableIrParser.ParseTagTable(text).Tags);
                }
            }
            catch (Exception ex) when (ex is IrFormatException or SimaticMlFormatException)
            {
                // Best-effort: an unparseable data file just doesn't contribute types.
            }
        }

        return FromSources(dbs, udts, tags);
    }

    /// <summary>
    /// The datatype of the tag/member named by a dotted path (e.g. "StatusWord",
    /// "DB_Timers.SampleTimer0.Q", "SpeedArray[3]"), or null if it can't be resolved from the known
    /// DBs/UDTs/tags. A subscripted final component resolves to the array's element type.
    /// </summary>
    public string? Resolve(string dottedPath)
    {
        var components = dottedPath.Split('.');
        var root = StripSubscript(components[0]);

        // The enclosing block's own interface members are the innermost, highest-priority namespace.
        if (_localMembers.Count > 0 && ResolveInMembers(_localMembers, components) is string localType)
        {
            return localType;
        }

        if (components.Length == 1)
        {
            if (_tagTypes.TryGetValue(root, out var tagType))
            {
                return HasSubscript(components[0]) ? ArrayElementType(tagType) ?? tagType : tagType;
            }

            return null;
        }

        var rest = components[1..];

        if (_dbs.TryGetValue(root, out var db))
        {
            return ResolveInMembers(AllMembers(db), rest);
        }

        // A tag whose own type is a UDT, then further members into that UDT.
        if (_tagTypes.TryGetValue(root, out var rootTagType)
            && _udts.TryGetValue(StripQuotes(rootTagType), out var rootUdt))
        {
            return ResolveInMembers(rootUdt.Members, rest);
        }

        return null;
    }

    private static IEnumerable<DbMember> AllMembers(DbSource db) =>
        db.Members
            .Concat(db.InputMembers ?? Array.Empty<DbMember>())
            .Concat(db.OutputMembers ?? Array.Empty<DbMember>())
            .Concat(db.InOutMembers);

    private string? ResolveInMembers(IEnumerable<DbMember> members, string[] components)
    {
        var name = StripSubscript(components[0]);
        var member = members.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal));
        if (member is null)
        {
            return null;
        }

        if (components.Length == 1)
        {
            return HasSubscript(components[0]) ? ArrayElementType(member.Datatype) ?? member.Datatype : member.Datatype;
        }

        var rest = components[1..];

        // A structured member inlines its own sub-members one level deep — descend into them first.
        if (member.NestedMembers is { Count: > 0 } nested)
        {
            return ResolveInMembers(nested, rest);
        }

        // Otherwise the member's own type may name a UDT whose members we can descend into.
        if (_udts.TryGetValue(StripQuotes(member.Datatype), out var udt))
        {
            return ResolveInMembers(udt.Members, rest);
        }

        return null;
    }

    private static bool HasSubscript(string component) => component.Contains('[');

    private static string StripSubscript(string component)
    {
        var idx = component.IndexOf('[');
        return idx < 0 ? component : component[..idx];
    }

    private static string StripQuotes(string type) => type.Trim('"');

    // "Array[0..14] of Bool" → "Bool". Case-insensitive on the "of" separator; null if the datatype
    // isn't an array shape (so the caller can fall back to the datatype itself).
    private static string? ArrayElementType(string datatype)
    {
        var trimmed = datatype.Trim();
        if (!trimmed.StartsWith("Array", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var ofIdx = trimmed.IndexOf(" of ", StringComparison.OrdinalIgnoreCase);
        return ofIdx < 0 ? null : trimmed[(ofIdx + 4)..].Trim();
    }
}
