using Converter.SimaticMl;

namespace Converter.Sanitize;

/// <summary>
/// Renames every identifying value in a parsed <see cref="BlockSource"/> via a
/// <see cref="SanitizationMap"/> — block name, block/network comments, tag paths. Structural
/// data (UIds, slice/array-index suffixes, wiring, part/wire shape) is untouched; only string
/// identity is replaced. Hard-errors (does not silently pass through) on any value found in the
/// source that the map doesn't cover — design philosophy #10, applied to sanitization: nothing
/// unmapped reaches the output by accident. Collects every missing mapping before throwing, once
/// per run, so a first attempt tells you everything you need to add rather than one item at a
/// time.
/// </summary>
public static class Sanitizer
{
    public static BlockSource Apply(BlockSource block, SanitizationMap map)
    {
        var missing = new List<string>();

        var sanitizedName = Require(
            map.Names.TryGetValue(block.Name, out var mappedName) ? mappedName : null,
            $"Names[\"{block.Name}\"]",
            missing);
        var sanitizedBlockComment = SanitizeComment(
            block.Comment,
            map.Comments.TryGetValue(block.Name, out var mappedBlockComment) ? mappedBlockComment : null,
            $"Comments[\"{block.Name}\"]",
            missing);

        // Block-level Title (S1 item 17, 2026-07-12) — confirmed real (FB MotorVSDSystem/AirStar, both
        // "VSD Motor") after being assumed always-empty during S1 item 16's own grounding. Same
        // sanitization treatment as the block's own Comment.
        var sanitizedBlockTitle = SanitizeComment(
            block.Title,
            map.Titles.TryGetValue(block.Name, out var mappedBlockTitle) ? mappedBlockTitle : null,
            $"Titles[\"{block.Name}\"]",
            missing);

        var sanitizedUnits = new List<CompileUnitSource>();
        for (var i = 0; i < block.CompileUnits.Count; i++)
        {
            var networkKey = $"{block.Name}#{i + 1}";
            var unit = block.CompileUnits[i];

            var sanitizedComment = SanitizeComment(
                unit.Comment,
                map.NetworkComments.TryGetValue(networkKey, out var mappedComment) ? mappedComment : null,
                $"NetworkComments[\"{networkKey}\"]",
                missing);

            // Title (S1 item 16, 2026-07-12) — genuinely identifying free text in real data
            // (e.g. equipment/process names), same sanitization treatment as Comment: hard-error
            // if a non-empty real Title isn't covered by the map, never pass it through verbatim.
            var sanitizedTitle = SanitizeComment(
                unit.Title,
                map.NetworkTitles.TryGetValue(networkKey, out var mappedTitle) ? mappedTitle : null,
                $"NetworkTitles[\"{networkKey}\"]",
                missing);

            var sanitizedNetwork = SanitizeNetwork(unit.Network, map, missing);
            sanitizedUnits.Add(unit with { Comment = sanitizedComment, Title = sanitizedTitle, Network = sanitizedNetwork });
        }

        // Static/Temp (an FB's own instance-data/working-variable declarations, S1 item 7 Phase
        // B) are top-level, freely-named members exactly like a DB's own — same Tags-map
        // sanitization, same SanitizeMember helper. Unlike a structured member's *nested*
        // sub-fields (treated as structural, project owner's call — see
        // docs/13-data-boundary.md), these are chosen per-block by whoever wrote it and can carry
        // real meaning, so they aren't given the structural exemption.
        var sanitizedStaticMembers = block.StaticMembers?.Select(m => SanitizeMember(block.Name, m, map, missing)).ToList();
        var sanitizedTempMembers = block.TempMembers.Select(m => SanitizeMember(block.Name, m, map, missing)).ToList();

        // Input/Output/InOut/Constant (S1 item 20, 2026-07-12) are the same freely-named,
        // owner-chosen category as Static/Temp — not given the structural exemption, same
        // reasoning and same SanitizeMember helper.
        var sanitizedInputMembers = block.InputMembers?.Select(m => SanitizeMember(block.Name, m, map, missing)).ToList();
        var sanitizedOutputMembers = block.OutputMembers?.Select(m => SanitizeMember(block.Name, m, map, missing)).ToList();
        var sanitizedInOutMembers = block.InOutMembers.Select(m => SanitizeMember(block.Name, m, map, missing)).ToList();
        var sanitizedConstantMembers = block.ConstantMembers?.Select(m => SanitizeMember(block.Name, m, map, missing)).ToList();

        if (missing.Count > 0)
        {
            throw new SanitizationMapException(
                $"Sanitization map is missing {missing.Count} entr{(missing.Count == 1 ? "y" : "ies")}:\n  " +
                string.Join("\n  ", missing));
        }

        return block with
        {
            Name = sanitizedName!,
            Comment = sanitizedBlockComment,
            Title = sanitizedBlockTitle,
            CompileUnits = sanitizedUnits,
            StaticMembers = sanitizedStaticMembers,
            TempMembers = sanitizedTempMembers,
            InputMembers = sanitizedInputMembers,
            OutputMembers = sanitizedOutputMembers,
            InOutMembers = sanitizedInOutMembers,
            ConstantMembers = sanitizedConstantMembers,
        };
    }

    /// <summary>
    /// DB analogue of <see cref="Apply"/>. A DB has no tag *references* of its own — instead its
    /// name and every member name are themselves the identifiers other blocks' tag references
    /// point at, so they're sanitized via the same <see cref="SanitizationMap.Tags"/> table
    /// ("&lt;RealDb&gt;.&lt;RealMember&gt;" -> "&lt;InventedDb&gt;.&lt;InventedMember&gt;") —
    /// one shared mapping, not a parallel one, so a block's tag reference and the DB's own
    /// declaration always agree.
    /// </summary>
    public static DbSource ApplyToDb(DbSource db, SanitizationMap map)
    {
        var missing = new List<string>();

        var sanitizedName = Require(
            map.Names.TryGetValue(db.Name, out var mappedName) ? mappedName : null,
            $"Names[\"{db.Name}\"]",
            missing);

        // InstanceOfName is an FB name — same identifying category as a DB/block name, so it
        // goes through the same map.Names table, not silently passed through. Null only for a
        // Global DB, in which case there's nothing to map.
        var sanitizedInstanceOfName = db.InstanceOfName is null
            ? null
            : Require(
                map.Names.TryGetValue(db.InstanceOfName, out var mappedInstanceOf) ? mappedInstanceOf : null,
                $"Names[\"{db.InstanceOfName}\"]",
                missing);

        var sanitizedComment = SanitizeComment(
            db.Comment,
            map.Comments.TryGetValue(db.Name, out var mappedComment) ? mappedComment : null,
            $"Comments[\"{db.Name}\"]",
            missing);

        var sanitizedMembers = db.Members.Select(member => SanitizeMember(db.Name, member, map, missing)).ToList();

        if (missing.Count > 0)
        {
            throw new SanitizationMapException(
                $"Sanitization map is missing {missing.Count} entr{(missing.Count == 1 ? "y" : "ies")}:\n  " +
                string.Join("\n  ", missing));
        }

        return db with { Name = sanitizedName!, InstanceOfName = sanitizedInstanceOfName, Comment = sanitizedComment, Members = sanitizedMembers };
    }

    /// <summary>
    /// PLC data type (UDT) analogue of <see cref="ApplyToDb"/> — same reasoning: a type has no
    /// tag references of its own, its name and member names are what other blocks' Datatype/tag
    /// references point at, sanitized via the same shared <see cref="SanitizationMap.Names"/>/
    /// <see cref="SanitizationMap.Tags"/> tables so a UDT's own declaration and a block's own
    /// `Datatype="&lt;Type&gt;"` reference (<see cref="SanitizeDatatype"/>) always agree.
    /// </summary>
    public static PlcTypeSource ApplyToType(PlcTypeSource type, SanitizationMap map)
    {
        var missing = new List<string>();

        var sanitizedName = Require(
            map.Names.TryGetValue(type.Name, out var mappedName) ? mappedName : null,
            $"Names[\"{type.Name}\"]",
            missing);

        var sanitizedComment = SanitizeComment(
            type.Comment,
            map.Comments.TryGetValue(type.Name, out var mappedComment) ? mappedComment : null,
            $"Comments[\"{type.Name}\"]",
            missing);

        var sanitizedMembers = type.Members.Select(member => SanitizeMember(type.Name, member, map, missing)).ToList();

        if (missing.Count > 0)
        {
            throw new SanitizationMapException(
                $"Sanitization map is missing {missing.Count} entr{(missing.Count == 1 ? "y" : "ies")}:\n  " +
                string.Join("\n  ", missing));
        }

        return type with { Name = sanitizedName!, Comment = sanitizedComment, Members = sanitizedMembers };
    }

    /// <summary>
    /// PLC tag table analogue of <see cref="ApplyToType"/>. A tag's own real "path" as referenced
    /// elsewhere is its bare name alone (confirmed real, 2026-07-14 — a tag reference is a single-
    /// component `Access`, never `TableName.TagName`), so each tag is looked up in
    /// <see cref="SanitizationMap.Tags"/> by its own bare name, not the "Owner.Member" dotted
    /// convention <see cref="SanitizeMember"/> uses for DB/UDT/block members.
    /// <c>DataTypeName</c>/<c>LogicalAddress</c> are structural (a built-in type name, a physical
    /// I/O address) and are never sanitized, same category as a UId.
    /// </summary>
    public static PlcTagTableSource ApplyToTagTable(PlcTagTableSource tagTable, SanitizationMap map)
    {
        var missing = new List<string>();

        var sanitizedName = Require(
            map.Names.TryGetValue(tagTable.Name, out var mappedName) ? mappedName : null,
            $"Names[\"{tagTable.Name}\"]",
            missing);

        var sanitizedTags = tagTable.Tags.Select(tag => SanitizeTag(tag, map, missing)).ToList();

        if (missing.Count > 0)
        {
            throw new SanitizationMapException(
                $"Sanitization map is missing {missing.Count} entr{(missing.Count == 1 ? "y" : "ies")}:\n  " +
                string.Join("\n  ", missing));
        }

        return tagTable with { Name = sanitizedName!, Tags = sanitizedTags };
    }

    private static PlcTagSource SanitizeTag(PlcTagSource tag, SanitizationMap map, List<string> missing)
    {
        var sanitizedName = Require(
            map.Tags.TryGetValue(tag.Name, out var mappedTagName) ? mappedTagName : null,
            $"Tags[\"{tag.Name}\"]",
            missing);

        var sanitizedComment = SanitizeComment(
            tag.Comment,
            map.Comments.TryGetValue(tag.Name, out var mappedComment) ? mappedComment : null,
            $"Comments[\"{tag.Name}\"]",
            missing);

        return tag with { Name = sanitizedName!, Comment = sanitizedComment };
    }

    // ownerName is a DB name for DbSource.Members, or a block (FC/FB) name for
    // BlockSource.StaticMembers/TempMembers — the same Tags-map convention
    // ("<Owner>.<Member>" -> "<InventedOwner>.<InventedMember>") covers both, one shared mapping.
    private static DbMember SanitizeMember(string ownerName, DbMember member, SanitizationMap map, List<string> missing)
    {
        var realPath = $"{ownerName}.{member.Name}";
        if (!map.Tags.TryGetValue(realPath, out var invented))
        {
            missing.Add($"Tags[\"{realPath}\"]");
            return member;
        }

        var separatorIndex = invented.IndexOf('.');
        if (separatorIndex < 0)
        {
            throw new SanitizationMapException($"Tags[\"{realPath}\"] = \"{invented}\" isn't a dotted \"Owner.Member\" path.");
        }

        var sanitizedStartValue = SanitizeStartValue(member.StartValue, ownerName, member.Name, map, missing);
        var sanitizedDatatype = SanitizeDatatype(member.Datatype, map, missing);

        // Nested members (a structured member's own sub-fields, e.g. a UDT's InHand/Running or a
        // timer's PT/ET/IN/Q) are treated as structural, same category as their own scalar
        // Datatype (Bool/Time/etc) — they're the reusable type's own field names, not
        // site-specific identifying data, so they aren't renamed. Their StartValue *can* still
        // carry identifying string content (same real risk the top-level rule exists for), so
        // that alone is still sanitized — recursively, since an anonymous `Struct` member can
        // nest arbitrarily deep (confirmed real 2026-07-14, `FB ShredderControlSystem`'s own
        // `ComsOutByte501`, three levels deep).
        var sanitizedNestedMembers = member.NestedMembers?
            .Select(nested => SanitizeNestedMember(ownerName, $"{member.Name}.{nested.Name}", nested, map, missing))
            .ToList();

        return member with { Name = invented[(separatorIndex + 1)..], Datatype = sanitizedDatatype, StartValue = sanitizedStartValue, NestedMembers = sanitizedNestedMembers };
    }

    private static DbMember SanitizeNestedMember(string ownerName, string nestedPath, DbMember nested, SanitizationMap map, List<string> missing)
    {
        var sanitizedStartValue = SanitizeStartValue(nested.StartValue, ownerName, nestedPath, map, missing);
        var sanitizedDeeperMembers = nested.NestedMembers?
            .Select(deeper => SanitizeNestedMember(ownerName, $"{nestedPath}.{deeper.Name}", deeper, map, missing))
            .ToList();

        return nested with { StartValue = sanitizedStartValue, NestedMembers = sanitizedDeeperMembers };
    }

    // Only string-typed StartValues (Siemens single-quote literal syntax, e.g. 'Some Text') can
    // carry identifying content — confirmed real, 2026-07-10 (a real DB's string start value
    // was a descriptive equipment name). Every other literal syntax (bool/numeric/hex/time) is
    // structural, not identifying, and passes through unmapped.
    private static string? SanitizeStartValue(string? startValue, string ownerName, string memberName, SanitizationMap map, List<string> missing)
    {
        if (startValue is null || !(startValue.StartsWith('\'') && startValue.EndsWith('\'')))
        {
            return startValue;
        }

        var key = $"{ownerName}.{memberName}";
        if (!map.StartValues.TryGetValue(key, out var invented))
        {
            missing.Add($"StartValues[\"{key}\"]");
            return startValue;
        }

        return invented;
    }

    // A quoted Datatype (e.g. `"TypeDOL"`) is a reference to a user-defined type — the same
    // identifying-name category as a DB/block/FB name (site convention C-302: UDTs are named,
    // reusable, per-project), confirmed real 2026-07-11 (S1 item 7 Phase B). Every other
    // Datatype string (Bool, Word, TON_TIME, Array[0..2] of Int, ...) is a built-in or
    // system-function-block type name, structural, and passes through verbatim.
    private static string SanitizeDatatype(string datatype, SanitizationMap map, List<string> missing)
    {
        if (datatype.Length < 2 || datatype[0] != '"' || datatype[^1] != '"')
        {
            return datatype;
        }

        var realTypeName = datatype[1..^1];
        if (!map.Names.TryGetValue(realTypeName, out var invented))
        {
            missing.Add($"Names[\"{realTypeName}\"]");
            return datatype;
        }

        return $"\"{invented}\"";
    }

    private static FlgNetwork SanitizeNetwork(FlgNetwork network, SanitizationMap map, List<string> missing)
    {
        var sanitizedAccessNodes = network.AccessNodes
            .Select(access => SanitizeAccessNode(access, map, missing))
            .ToList();

        // A Part's own Instance (a TON/TONR/TOF's timer instance, or a CALL's own FB instance) is
        // an AccessNode with exactly the same Scope+ComponentPath shape as an ordinary Access —
        // see PartNode's own doc comment — but it lives in network.Parts, not network.AccessNodes,
        // so it was never reached by the sanitization pass above. Real bug, found live 2026-07-14:
        // MotorDOL's own GeneralDelayTimer1/FaultTripTimer2 instance references stayed unsanitized in the network
        // body (visible as `<Instance><Component Name="GeneralDelayTimer1" /></Instance>`) even though the
        // exact same names were correctly renamed at their Interface-declaration site (a completely
        // separate code path, SanitizeMember) — an ordinary Access-based rename
        // (RisingEdgeFlags1 → RisingEdgeFlags) worked everywhere by contrast, since that one
        // never happens to be a Part's own Instance. Fixed by sanitizing every Part.Instance the
        // same way, reusing the same helper.
        var sanitizedParts = network.Parts
            .Select(part => part.Instance is null
                ? part
                : part with { Instance = SanitizeAccessNode(part.Instance, map, missing) })
            .ToList();

        return network with { AccessNodes = sanitizedAccessNodes, Parts = sanitizedParts };
    }

    private static AccessNode SanitizeAccessNode(AccessNode access, SanitizationMap map, List<string> missing)
    {
        var realPath = string.Join('.', access.ComponentPath);
        if (!map.Tags.TryGetValue(realPath, out var sanitizedPath))
        {
            missing.Add($"Tags[\"{realPath}\"]");
            return access;
        }

        return access with { ComponentPath = sanitizedPath.Split('.') };
    }

    /// <summary>
    /// A null/empty source comment needs no mapping (nothing to sanitize). A non-empty source
    /// comment requires a non-empty replacement — silently dropping real comment text would be
    /// data loss disguised as sanitization, not sanitization.
    /// </summary>
    private static string? SanitizeComment(string? sourceComment, string? mappedComment, string mapKeyDescription, List<string> missing)
    {
        if (string.IsNullOrEmpty(sourceComment))
        {
            return null;
        }

        if (string.IsNullOrEmpty(mappedComment))
        {
            missing.Add(mapKeyDescription);
            return sourceComment;
        }

        return mappedComment;
    }

    private static string? Require(string? mappedValue, string mapKeyDescription, List<string> missing)
    {
        if (string.IsNullOrEmpty(mappedValue))
        {
            missing.Add(mapKeyDescription);
            return null;
        }

        return mappedValue;
    }
}
