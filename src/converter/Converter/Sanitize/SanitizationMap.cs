using System.Text.Json;

namespace Converter.Sanitize;

/// <summary>
/// Hand-authored, never committed (docs/13-data-boundary.md): maps every real, identifying
/// value in a source block or DB to an invented replacement. Loaded from a plain JSON file kept
/// outside the repo's tracked content. Covers multiple blocks/DBs in one file (a whole
/// reference-project seeding pass at a time) so tag references in one block and the DB/member
/// declarations they point at share exactly one mapping — revised 2026-07-10 from an
/// earlier single-block-only shape once DB support made that necessary.
/// </summary>
public sealed class SanitizationMap
{
    /// <summary>Keyed by real block or DB name -> invented name.</summary>
    public Dictionary<string, string> Names { get; set; } = new();

    /// <summary>Keyed by real block or DB name -> invented comment text. Only needs an entry when the source comment is non-empty.</summary>
    public Dictionary<string, string> Comments { get; set; } = new();

    /// <summary>Keyed by "&lt;real block name&gt;#&lt;network number&gt;" -> invented network comment text.</summary>
    public Dictionary<string, string> NetworkComments { get; set; } = new();

    /// <summary>
    /// Keyed by the real dotted component path with no slice/array suffix (e.g.
    /// "CommsProcessData.Node_Error", not "CommsProcessData.Node_Error[1]" or "...​.%X0") — the
    /// suffix is structural (array index, bit slice) and is preserved as-is on the sanitized
    /// tag, never mapped. Value is the invented dotted replacement path, same shape. Doubles as
    /// the source of truth for a DB's own member renaming — "&lt;RealDb&gt;.&lt;RealMember&gt;"
    /// -> "&lt;InventedDb&gt;.&lt;InventedMember&gt;" is exactly a tag-path entry.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    /// <summary>
    /// Keyed by "&lt;real DB name&gt;.&lt;real member name&gt;" -> invented string literal
    /// (quotes included, e.g. "'Generic Conveyor Unit'"). Only needed for string-typed
    /// <c>StartValue</c>s (Siemens single-quote syntax) — every other literal syntax (bool, numeric,
    /// hex, time) is structural, not identifying, and is preserved as-is without a mapping entry.
    /// </summary>
    public Dictionary<string, string> StartValues { get; set; } = new();

    public static SanitizationMap Load(string path)
    {
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<SanitizationMap>(json, options)
            ?? throw new SanitizationMapException($"'{path}' did not deserialize to a sanitization map.");
    }
}

public sealed class SanitizationMapException : Exception
{
    public SanitizationMapException(string message)
        : base(message)
    {
    }
}
