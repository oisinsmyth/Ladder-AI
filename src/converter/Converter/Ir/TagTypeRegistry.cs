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

    // FB name → its interface members, so an instance DB's INSTANCEOF can be followed into the
    // block that defines its member tree. See the fallback in Resolve for why this is needed.
    private readonly IReadOnlyDictionary<string, IReadOnlyList<DbMember>> _fbInterfaces;

    private TagTypeRegistry(
        IReadOnlyDictionary<string, string> tagTypes,
        IReadOnlyDictionary<string, DbSource> dbs,
        IReadOnlyDictionary<string, PlcTypeSource> udts,
        IReadOnlyList<DbMember> localMembers,
        IReadOnlyDictionary<string, IReadOnlyList<DbMember>>? fbInterfaces = null)
    {
        _tagTypes = tagTypes;
        _dbs = dbs;
        _udts = udts;
        _localMembers = localMembers;
        _fbInterfaces = fbInterfaces
            ?? new Dictionary<string, IReadOnlyList<DbMember>>(StringComparer.Ordinal);
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
        new(_tagTypes, _dbs, _udts, localMembers.ToList(), _fbInterfaces);

    // Resolve a UDT body by name (quotes on the name are tolerated, e.g. a member's raw
    // Datatype string `"UDT_Foo"`). Public so cross-file rules — C-118's interface-UDT Step
    // resolution (FI-09) — can descend into the referenced type, the first review rule needing a
    // second file. Returns false (udt null) for an unknown name.
    public bool TryGetUdt(string name, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out PlcTypeSource udt) =>
        _udts.TryGetValue(StripQuotes(name), out udt);

    // Whether a name is a DB the registry knows (quotes tolerated). Lets C-118 flag a Step that
    // lives in a Controls/Settings DB rather than the interface UDT.
    public bool IsKnownDb(string name) => _dbs.ContainsKey(StripQuotes(name));

    /// <summary>
    /// How many DB and UDT BODIES this registry can descend into — the denominator of a
    /// "member path '…' does not resolve" finding (2026-08-23).
    ///
    /// <para>Deliberately separate from any ProjectIndex count. Member-path resolution walks THIS
    /// registry via <c>MemberPathResolver</c> and never touches <c>ProjectIndex</c>, so a
    /// block-name or file count printed under a member-path finding would be a category slip: the
    /// two walks read the same directory but index different things, and a DB present by NAME in
    /// ProjectIndex can be a body-less stub here (see <see cref="TryGetDb"/>).</para>
    /// </summary>
    public int IndexedBodyCount => _dbs.Count + _udts.Count;

    // The DB body behind a name (quotes tolerated), so a caller can tell "this DB has no such
    // member" from "this DB's members aren't in the export at all" — an instance-DB stub created by
    // `create-instance-db` and not yet re-exported carries no member tree, and treating that as
    // "member absent" would manufacture a false gap. Sibling of TryGetUdt.
    public bool TryGetDb(string name, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out DbSource db) =>
        _dbs.TryGetValue(StripQuotes(name), out db);

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
        var fbInterfaces = new Dictionary<string, IReadOnlyList<DbMember>>(StringComparer.Ordinal);

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
                else if (text.StartsWith("BLOCK FB ", StringComparison.Ordinal))
                {
                    // An FB is indexed for its INTERFACE only — it is the definition of every
                    // instance DB's member tree, and nothing else here reads its networks.
                    var fb = IrParser.ParseBlockWithoutSidecar(text);
                    fbInterfaces[fb.Name] = (fb.StaticMembers ?? Array.Empty<DbMember>())
                        .Concat(fb.InputMembers ?? Array.Empty<DbMember>())
                        .Concat(fb.OutputMembers ?? Array.Empty<DbMember>())
                        .Concat(fb.InOutMembers)
                        .ToList();
                }
            }
            catch (Exception ex) when (ex is IrFormatException or SimaticMlFormatException)
            {
                // Best-effort: an unparseable data file just doesn't contribute types.
            }
        }

        var flat = FromSources(dbs, udts, tags);
        return new TagTypeRegistry(
            flat._tagTypes, flat._dbs, flat._udts, Array.Empty<DbMember>(), fbInterfaces);
    }

    /// <summary>
    /// The datatype of the tag/member named by a dotted path (e.g. "StatusWord",
    /// "DB_Timers.SampleTimer0.Q", "SpeedArray[3]"), or null if it can't be resolved from the known
    /// DBs/UDTs/tags. A subscripted final component resolves to the array's element type.
    /// </summary>
    public string? Resolve(string dottedPath)
    {
        // Bracket-aware: a SYMBOLIC subscript is itself a dotted path (`Recipe[iDB.Seq.Slot]`), so a
        // plain Split('.') cut a valid path into components that resolve to nothing and this returned
        // null — which MemberPathResolver then reads as "type unknown". 2026-08-27, same defect class
        // as the tagstatus/review false positives; see TagPath.
        var components = TagPath.Split(dottedPath);
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
            if (ResolveInMembers(AllMembers(db), rest) is string dbType)
            {
                return dbType;
            }

            // An INSTANCE DB's member tree is its FB's interface, and the FB is the source of truth
            // for it (2026-08-24). Measured on a real import: `iDB_X.Bay.SettledElapsed >=
            // iDB_X.Settings.DwellTimeout` is Time >= Time, both operands failed to resolve
            // here, and InferCompareSrcType's no-tags-no-literals fallback emitted `SrcType Int`.
            // TIA rejected the block — 12 compile errors — while the IDENTICAL comparison written
            // against the same members from inside the FB emitted the right type, because there the
            // local-member namespace answered it.
            //
            // Falling back to the FB rather than requiring the iDB to carry members is deliberate:
            // a scaffolded instance DB legitimately has an EMPTY member section (TIA populates it
            // on import), so requiring the tree here would make correctness depend on whether
            // somebody had re-exported the project yet.
            if (db.InstanceOfName is { Length: > 0 } fbName
                && _fbInterfaces.TryGetValue(StripQuotes(fbName), out var fbMembers))
            {
                return ResolveInMembers(fbMembers, rest);
            }

            return null;
        }

        // A tag whose own type is a UDT, then further members into that UDT.
        if (_tagTypes.TryGetValue(root, out var rootTagType)
            && _udts.TryGetValue(StripQuotes(ArrayElementType(rootTagType) ?? rootTagType), out var rootUdt))
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

        // Otherwise the member's own type may name a UDT whose members we can descend into — via its
        // ELEMENT type when the member is an array of UDT (`Array[0..3] of "UDT_Vessel"`), the
        // ordinary way to express N identical vessels. Descending on the element type covers the
        // indexed path (`Vessel[0].MaxNet`) and the unindexed, type-level one (`Vessel.MaxNet`)
        // alike; before FI-45 neither resolved, which left every per-instance operand on such a
        // project typeless here and an invented member over in `tagstatus`.
        if (_udts.TryGetValue(StripQuotes(ArrayElementType(member.Datatype) ?? member.Datatype), out var udt))
        {
            return ResolveInMembers(udt.Members, rest);
        }

        return null;
    }

    private static bool HasSubscript(string component) => component.Contains('[');

    // Component-level subscript handling is TagPath's — one mechanism (2026-08-27).
    private static string StripSubscript(string component) => TagPath.StripComponentSubscript(component);

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
