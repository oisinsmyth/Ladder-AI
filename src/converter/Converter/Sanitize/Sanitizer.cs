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

            var sanitizedNetwork = SanitizeNetwork(unit.Network, map, missing);
            sanitizedUnits.Add(unit with { Comment = sanitizedComment, Network = sanitizedNetwork });
        }

        // Static/Temp (an FB's own instance-data/working-variable declarations, S1 item 7 Phase
        // B) are top-level, freely-named members exactly like a DB's own — same Tags-map
        // sanitization, same SanitizeMember helper. Unlike a structured member's *nested*
        // sub-fields (treated as structural, project owner's call — see
        // docs/13-data-boundary.md), these are chosen per-block by whoever wrote it and can carry
        // real meaning, so they aren't given the structural exemption.
        var sanitizedStaticMembers = block.StaticMembers?.Select(m => SanitizeMember(block.Name, m, map, missing)).ToList();
        var sanitizedTempMembers = block.TempMembers.Select(m => SanitizeMember(block.Name, m, map, missing)).ToList();

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
            CompileUnits = sanitizedUnits,
            StaticMembers = sanitizedStaticMembers,
            TempMembers = sanitizedTempMembers,
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
        // Datatype (Bool/Time/etc — never itself a quoted UDT reference, since double-nesting is
        // hard-errored) — they're the reusable type's own field names, not site-specific
        // identifying data, so they aren't renamed. Their StartValue *can* still carry
        // identifying string content (same real risk the top-level rule exists for), so that
        // alone is still sanitized.
        var sanitizedNestedMembers = member.NestedMembers?
            .Select(nested => nested with { StartValue = SanitizeStartValue(nested.StartValue, ownerName, $"{member.Name}.{nested.Name}", map, missing) })
            .ToList();

        return member with { Name = invented[(separatorIndex + 1)..], Datatype = sanitizedDatatype, StartValue = sanitizedStartValue, NestedMembers = sanitizedNestedMembers };
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
            .Select(access =>
            {
                var realPath = string.Join('.', access.ComponentPath);
                if (!map.Tags.TryGetValue(realPath, out var sanitizedPath))
                {
                    missing.Add($"Tags[\"{realPath}\"]");
                    return access;
                }

                return access with { ComponentPath = sanitizedPath.Split('.') };
            })
            .ToList();

        return network with { AccessNodes = sanitizedAccessNodes };
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
