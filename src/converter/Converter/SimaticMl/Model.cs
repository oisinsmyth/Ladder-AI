namespace Converter.SimaticMl;

// SliceAccessModifier carries a real, documented site construct: bit-within-word alarm
// addressing (06-lad-conventions.md C-501: "DB_Alarms.EStopAlarm0.%X3", a documented exception
// to C-301) — confirmed real, 2026-07-11, as `SliceAccessModifier="x15"` on the LAST <Component>
// of an Access's Symbol path. Always assumed to be on the last component only.
//
// ArrayIndex carries a second real construct found the same day: a literal-constant array
// subscript on the final Component (`CommsProcessData.Node_Error[n]`) — confirmed from an
// untouched sibling block after a round-trip on the affected block silently collapsed three
// distinct array elements (Node_Error[1], [2], [3]) into one indistinguishable tag path, a real
// data-loss bug caught by the project owner reviewing the result in TIA, not by a test. XML
// shape: `<Component Name="Node_Error" AccessModifier="Array"><Access Scope="LiteralConstant">
// <Constant><ConstantType>DInt</ConstantType><ConstantValue>n</ConstantValue></Constant>
// </Access></Component>`. Seen only on the last component, same as SliceAccessModifier, and
// never confirmed together with one on the same Component — but nothing here assumes they're
// mutually exclusive.
public sealed record AccessNode(
    int UId,
    string Scope,
    IReadOnlyList<string> ComponentPath,
    string? SliceAccessModifier = null,
    int? ArrayIndex = null)
{
    // "[n]" and ".%X15" notation composed together — array index before slice, matching the one
    // real case observed of a not-yet-seen combination; the site's own ".%X15" convention is
    // preserved exactly, "[n]" is the natural/obvious choice for array subscript, not otherwise
    // used by the IR.
    public string DottedPath
    {
        get
        {
            var path = string.Join('.', ComponentPath);
            if (ArrayIndex is not null)
            {
                path += $"[{ArrayIndex}]";
            }

            if (SliceAccessModifier is not null)
            {
                path += $".%{SliceAccessModifier.ToUpperInvariant()}";
            }

            return path;
        }
    }

    /// <summary>Inverse of <see cref="DottedPath"/> — used when rebuilding an AccessNode from an IR tag string.</summary>
    public static AccessNode FromDottedPath(int uid, string scope, string dottedPath)
    {
        var sliceMatch = System.Text.RegularExpressions.Regex.Match(dottedPath, @"^(?<rest>.+)\.%(?<slice>[A-Za-z]\d+)$");
        var slice = sliceMatch.Success ? sliceMatch.Groups["slice"].Value.ToLowerInvariant() : null;
        var rest = sliceMatch.Success ? sliceMatch.Groups["rest"].Value : dottedPath;

        var arrayMatch = System.Text.RegularExpressions.Regex.Match(rest, @"^(?<path>.+)\[(?<index>\d+)\]$");
        var arrayIndex = arrayMatch.Success ? int.Parse(arrayMatch.Groups["index"].Value) : (int?)null;
        var path = arrayMatch.Success ? arrayMatch.Groups["path"].Value : rest;

        return new AccessNode(uid, scope, path.Split('.'), slice, arrayIndex);
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
