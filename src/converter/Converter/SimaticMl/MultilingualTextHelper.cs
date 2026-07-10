using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Shared `MultilingualText`/`MultilingualTextItem` reading — the same shape (Comment, and a
/// separate, always-empty-so-far Title) appears on both code blocks and DBs. Extracted once both
/// <see cref="BlockSourceParser"/> and <see cref="DbSourceParser"/> needed it, rather than
/// duplicated.
/// </summary>
internal static class MultilingualTextHelper
{
    /// <summary>Reads a MultilingualText[CompositionName=<paramref name="compositionName"/>]'s en-US Text, or null if empty/absent.</summary>
    public static string? ReadMultilingualText(XElement objectList, string compositionName)
    {
        var textElement = objectList.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "MultilingualText" && (string?)e.Attribute("CompositionName") == compositionName);

        if (textElement is null)
        {
            return null;
        }

        var items = textElement
            .Descendants()
            .Where(e => e.Name.LocalName == "MultilingualTextItem")
            .Select(item =>
            {
                var attrs = item.Element("AttributeList");
                var culture = attrs?.Elements().FirstOrDefault(e => e.Name.LocalName == "Culture")?.Value;
                var textValue = attrs?.Elements().FirstOrDefault(e => e.Name.LocalName == "Text")?.Value;
                return (Culture: culture, Text: textValue);
            })
            .ToList();

        var nonEnglish = items.FirstOrDefault(item => item.Culture is not null and not "en-US");
        if (nonEnglish.Culture is not null)
        {
            throw new UnsupportedConstructException(
                $"{compositionName} culture '{nonEnglish.Culture}' found — this converter only supports en-US (site convention C-006, English only).");
        }

        var text = items.FirstOrDefault(item => item.Culture == "en-US").Text;
        return string.IsNullOrEmpty(text) ? null : text;
    }

    // A separate "Title" MultilingualText (distinct from "Comment") exists at both block/DB and
    // network level in real exports — confirmed real, 2026-07-10, always empty on every block/DB
    // seen so far. Not currently modeled/round-tripped, so a non-empty one would be silently
    // dropped — hard error instead (design philosophy #10) rather than let real content vanish
    // quietly. Safe to proceed when empty, which is the only case observed.
    public static void RequireEmptyTitle(XElement objectList, string context)
    {
        var title = ReadMultilingualText(objectList, "Title");
        if (!string.IsNullOrEmpty(title))
        {
            throw new UnsupportedConstructException(
                $"{context} has a non-empty Title (\"{title}\") — this converter doesn't model Title text yet, only Comment.");
        }
    }
}
