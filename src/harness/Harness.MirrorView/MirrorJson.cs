using System.Text.Json;
using System.Text.Json.Serialization;

namespace Harness.MirrorView;

/// <summary>
/// The view model as JSON — the page's only data source, and a perfectly good thing to curl.
///
/// <para>The shape is the view model's own, camel-cased, with enums as names rather than numbers. That
/// is deliberate: a second hand-written projection is a second place for the page and the API to
/// disagree about what was measured, and the disagreement would be invisible because each half would
/// look right.</para>
/// </summary>
public static class MirrorJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(MirrorViewModel model) => JsonSerializer.Serialize(model, Options);
}
