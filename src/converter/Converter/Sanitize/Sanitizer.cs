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

        if (missing.Count > 0)
        {
            throw new SanitizationMapException(
                $"Sanitization map is missing {missing.Count} entr{(missing.Count == 1 ? "y" : "ies")}:\n  " +
                string.Join("\n  ", missing));
        }

        return block with { Name = sanitizedName!, Comment = sanitizedBlockComment, CompileUnits = sanitizedUnits };
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

        return db with { Name = sanitizedName!, Comment = sanitizedComment, Members = sanitizedMembers };
    }

    private static DbMember SanitizeMember(string dbName, DbMember member, SanitizationMap map, List<string> missing)
    {
        var realPath = $"{dbName}.{member.Name}";
        if (!map.Tags.TryGetValue(realPath, out var invented))
        {
            missing.Add($"Tags[\"{realPath}\"]");
            return member;
        }

        var separatorIndex = invented.IndexOf('.');
        if (separatorIndex < 0)
        {
            throw new SanitizationMapException($"Tags[\"{realPath}\"] = \"{invented}\" isn't a dotted \"Db.Member\" path.");
        }

        var sanitizedStartValue = SanitizeStartValue(member.StartValue, dbName, member.Name, map, missing);

        return member with { Name = invented[(separatorIndex + 1)..], StartValue = sanitizedStartValue };
    }

    // Only string-typed StartValues (Siemens single-quote literal syntax, e.g. 'Some Text') can
    // carry identifying content — confirmed real, 2026-07-10 (a real DB's string start value
    // was a descriptive equipment name). Every other literal syntax (bool/numeric/hex/time) is
    // structural, not identifying, and passes through unmapped.
    private static string? SanitizeStartValue(string? startValue, string dbName, string memberName, SanitizationMap map, List<string> missing)
    {
        if (startValue is null || !(startValue.StartsWith('\'') && startValue.EndsWith('\'')))
        {
            return startValue;
        }

        var key = $"{dbName}.{memberName}";
        if (!map.StartValues.TryGetValue(key, out var invented))
        {
            missing.Add($"StartValues[\"{key}\"]");
            return startValue;
        }

        return invented;
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
