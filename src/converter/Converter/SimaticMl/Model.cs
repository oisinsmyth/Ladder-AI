namespace Converter.SimaticMl;

// SliceAccessModifier carries a real, documented site construct: bit-within-word alarm
// addressing (06-lad-conventions.md C-501: "DB_Alarms.EStopAlarm0.%X3", a documented exception
// to C-301) — confirmed real, 2026-07-11, as `SliceAccessModifier="x15"` on the LAST <Component>
// of an Access's Symbol path. Always assumed to be on the last component only.
public sealed record AccessNode(int UId, string Scope, IReadOnlyList<string> ComponentPath, string? SliceAccessModifier = null)
{
    // ".%X15" notation matches the site's own documented convention exactly, so the IR reads
    // the same way an engineer already writes/reads it, not a converter-invented notation.
    public string DottedPath => SliceAccessModifier is null
        ? string.Join('.', ComponentPath)
        : $"{string.Join('.', ComponentPath)}.%{SliceAccessModifier.ToUpperInvariant()}";

    /// <summary>Inverse of <see cref="DottedPath"/> — used when rebuilding an AccessNode from an IR tag string.</summary>
    public static AccessNode FromDottedPath(int uid, string scope, string dottedPath)
    {
        var sliceMatch = System.Text.RegularExpressions.Regex.Match(dottedPath, @"^(?<path>.+)\.%(?<slice>[A-Za-z]\d+)$");
        return sliceMatch.Success
            ? new AccessNode(uid, scope, sliceMatch.Groups["path"].Value.Split('.'), sliceMatch.Groups["slice"].Value.ToLowerInvariant())
            : new AccessNode(uid, scope, dottedPath.Split('.'), null);
    }
}

public sealed record PartNode(int UId, string Name);

public enum EndpointKind
{
    Powerrail,
    IdentCon,
    NameCon,
    OpenCon,
}

public sealed record WireEndpoint(EndpointKind Kind, int? UId, string? PortName);

public sealed record WireNode(int UId, IReadOnlyList<WireEndpoint> Endpoints);

/// <summary>
/// One SimaticML network (a CompileUnit's FlgNet content): the Parts/Wires wiring graph,
/// per ADR-0001. AccessNodes and Parts both live inside the source's &lt;Parts&gt; element —
/// modeled separately here because they play different roles in the graph (data source vs.
/// instruction node).
/// </summary>
public sealed record FlgNetwork(
    IReadOnlyList<AccessNode> AccessNodes,
    IReadOnlyList<PartNode> Parts,
    IReadOnlyList<WireNode> Wires);

// CompileUnit "ID" is opaque — confirmed against a real export (2026-07-11) not to follow the
// same simple sequential-int scheme as FlgNet's own UIds (a real one came back as "D"). Treated
// as a string throughout, unlike Part/Wire/Access UId which are FlgNet-internal and stayed int.
public sealed record CompileUnitSource(string UId, string? Comment, FlgNetwork Network);

// RootUId is the block element's own "ID" attribute (e.g. `<SW.Blocks.FC ID="0">`), separate
// from its CompileUnits' own IDs — confirmed real and required, 2026-07-11: Import() rejects a
// block element with no ID ("Cannot find the required 'ID' attribute element"). Opaque, like
// CompileUnitSource.UId — not assumed to always be "0" just because that's what one real
// export showed.
public sealed record BlockSource(
    string RootUId,
    string Kind,
    string Name,
    int Number,
    string Language,
    string? Comment,
    IReadOnlyList<CompileUnitSource> CompileUnits);

public sealed class SimaticMlFormatException : Exception
{
    public SimaticMlFormatException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// A recognized-but-out-of-scope construct for this converter slice (e.g. a TON, an OR-merge,
/// a negated contact). Distinct from <see cref="SimaticMlFormatException"/>: the XML is
/// well-formed and understood, it's just not something this slice's converter can represent
/// yet (design philosophy #10 — hard error, not a silent partial result).
/// </summary>
public sealed class UnsupportedConstructException : Exception
{
    public UnsupportedConstructException(string message)
        : base(message)
    {
    }
}
