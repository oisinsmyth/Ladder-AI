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

    // A separate "Title" MultilingualText (distinct from "Comment") exists at block/DB and
    // network level in real exports. Network-level Title (S1 item 16) and block-level Title (S1
    // item 17, 2026-07-12 — confirmed real on two of FC PlantAutoControl's own dependency FBs,
    // `MotorVSDSystem`/`AirStar`, both titled "VSD Motor") are now modeled, read directly via
    // ReadMultilingualText (BlockSourceParser.ParseCompileUnit / BlockSourceParser.Parse
    // respectively). DB-level Title remains unconfirmed real (always empty on every DB seen so
    // far) — still guarded here so a non-empty one hard-errors (design philosophy #10) rather
    // than silently vanishing, until a real one is actually grounded.
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
