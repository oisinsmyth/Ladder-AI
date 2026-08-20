using System.Xml.Linq;

namespace HmiCli;

/// <summary>
/// Reads the two forms a SimaticML link takes. Shared by <see cref="ScreenCompare"/> and
/// <see cref="SimaticMlReader"/> so that neither can learn about a form the other does not know.
/// </summary>
/// <remarks>
/// 🔴 <b>TWO LINK FORMS EXIST AND READING ONLY THE FIRST REPORTS THE SECOND AS EMPTY.</b>
/// <list type="bullet">
/// <item><c>TargetID="@OpenLink"</c> carries a <c>&lt;Name&gt;</c> child and means "resolve this by
/// name at import". Everything this project emits uses it.</item>
/// <item><c>TargetID="#8A9"</c> is an INTRA-DOCUMENT reference to another object's <c>ID</c> - the
/// form a multiplexed tag's index and entries use. A reader that only knows the first shape reports
/// a wired multiplex index as UNSET, which is how one first read here.</item>
/// </list>
/// An ID is resolved to that object's own name, so the result survives TIA reassigning IDs at
/// import - which it does, unprompted. An ID that resolves to nothing comes back as the raw
/// reference rather than as blank: a dangling link is a finding, not an absence.
/// </remarks>
internal static class SimaticLinks
{
    /// <summary>Every link target under <paramref name="e"/>, in document order.</summary>
    public static List<string> Targets(XElement e, XDocument doc)
    {
        var outs = new List<string>();

        // DescendantsAndSelf, not Descendants. A SymbolicIOField's TextList link IS the element
        // handed in - the item's LinkList holds it directly, one level up from where a tag binding
        // sits inside a Property - so a self-excluding walk found nothing and reported the field as
        // having no text list, which is indistinguishable from a field that genuinely has none.
        // Caught by the round-trip test: the re-emit refused the field for being unbound.
        foreach (var link in e.DescendantsAndSelf().Where(x => x.Attribute("TargetID") is not null))
        {
            var byName = link.Element(link.Name.Namespace + "Name")
                         ?? link.Elements().FirstOrDefault(x => x.Name.LocalName == "Name");
            if (byName is not null)
            {
                outs.Add(byName.Value);
                continue;
            }

            var target = ((string?)link.Attribute("TargetID") ?? string.Empty).TrimStart('#');
            var resolved = doc.Descendants().FirstOrDefault(x => (string?)x.Attribute("ID") == target);
            var rname = resolved?.Element("AttributeList")?.Element("Name")?.Value
                        ?? resolved?.Element("AttributeList")?.Element("ObjectName")?.Value;
            outs.Add(rname ?? "#unresolved:" + target);
        }

        return outs;
    }

    /// <summary>The same targets as one comparable string. Empty when there are none.</summary>
    public static string Joined(XElement e, XDocument doc)
    {
        var t = Targets(e, doc);
        return t.Count == 0 ? string.Empty : string.Join(", ", t);
    }

    /// <summary>
    /// The single link target under <paramref name="e"/>, or null when there is not exactly one.
    ///
    /// "Not exactly one" is deliberately a null rather than a first-of-many: a parameter or a
    /// trigger carrying two links is a shape this tool has never met, and picking one of them would
    /// be the silent guess the caller is trying to avoid making.
    /// </summary>
    public static string? Single(XElement e, XDocument doc)
    {
        var t = Targets(e, doc);
        return t.Count == 1 ? t[0] : null;
    }
}
