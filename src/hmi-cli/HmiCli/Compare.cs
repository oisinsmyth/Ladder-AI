using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HmiCli;

/// <summary>
/// T7 - compare two classic-HMI SimaticML documents, normally what was EMITTED against what TIA
/// gave back on re-export.
/// </summary>
/// <remarks>
/// <para>
/// A green import and a clean compile say the document was ACCEPTED. Only reading it back and
/// comparing says it was accepted <b>as written</b>.
/// </para>
/// <para>
/// 🔴 <b>WIDENED 2026-08-17 after this tool was caught reporting a false green.</b> The first
/// version compared only type and the bounding rectangle. A dispatched agent negative-controlled it
/// by changing one character of a screen label and re-comparing: it reported
/// <c>IDENTICAL: 17 object(s), 0 differences</c>. Colours, enum values, <c>Flashing</c>,
/// <c>EdgeStyle</c>, fonts and every piece of label text were unexamined - while the
/// <c>IGNORED:</c> line named only <c>ID</c> and <c>DocumentInfo</c>, understating the scope so
/// badly that the output actively misled. That is the "print your real denominator" failure this
/// repo has closed five times elsewhere, reproduced here.
/// </para>
/// <para>
/// Now compares <b>every attribute stated on both sides</b>, plus text payloads and fonts, and
/// reports the field count rather than only the object count. The two absence directions are split
/// because they mean opposite things: an attribute the emitter stated and the read-back lost is a
/// <b>DROP</b> and gates; an attribute only the read-back has is TIA supplying a default, which is
/// reported and does not gate.
/// </para>
/// </remarks>
public static class ScreenCompare
{
    public sealed record Result(
        int Items,
        int FieldsComparedOnBothSides,
        IReadOnlyList<string> Changed,
        IReadOnlyList<string> Dropped,
        int DefaultedByTia)
    {
        public int Differences => Changed.Count + Dropped.Count;

        /// <summary>Zero fields compared is never a pass, however many objects were found.</summary>
        public bool NothingCompared => FieldsComparedOnBothSides == 0;
    }

    /// <summary>What is genuinely not compared. Kept short because the list is now genuinely short.</summary>
    public static readonly string[] Ignored =
    {
        "ID attributes (TIA reassigns them on import - objects pair by ObjectName)",
        "DocumentInfo (export timestamp, installed-product list)",
        "<p /> vs <p></p> in rich text (the same empty paragraph, written two ways)",
    };

    public static Result Compare(string leftXml, string rightXml)
    {
        var a = Parse(leftXml);
        var b = Parse(rightXml);

        var changed = new List<string>();
        var dropped = new List<string>();
        var both = 0;
        var defaulted = 0;

        foreach (var name in a.Keys.Union(b.Keys, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
        {
            var hasA = a.TryGetValue(name, out var fa);
            var hasB = b.TryGetValue(name, out var fb);

            if (!hasA)
            {
                changed.Add($"{name}: present only in the SECOND document");
                continue;
            }

            if (!hasB)
            {
                dropped.Add($"{name}: present only in the FIRST document");
                continue;
            }

            if (fa!.Type != fb!.Type)
            {
                changed.Add($"{name}: type {fa.Type} -> {fb.Type}");
            }

            foreach (var key in fa.Fields.Keys.Union(fb.Fields.Keys, StringComparer.Ordinal)
                         .OrderBy(x => x, StringComparer.Ordinal))
            {
                var inA = fa.Fields.TryGetValue(key, out var va);
                var inB = fb.Fields.TryGetValue(key, out var vb);

                if (inA && inB)
                {
                    both++;
                    if (!string.Equals(Normalise(va!), Normalise(vb!), StringComparison.Ordinal))
                    {
                        changed.Add($"{name}.{key}: '{va}' -> '{vb}'");
                    }
                }
                else if (inA)
                {
                    // Stated by the emitter and absent from the read-back: TIA discarded it.
                    dropped.Add($"{name}.{key}: '{va}' was DROPPED by TIA");
                }
                else
                {
                    defaulted++;
                }
            }
        }

        return new Result(a.Keys.Union(b.Keys, StringComparer.Ordinal).Count(), both, changed, dropped, defaulted);
    }

    private sealed record ItemFacts(string Type, Dictionary<string, string> Fields);

    private static Dictionary<string, ItemFacts> Parse(string xml)
    {
        var map = new Dictionary<string, ItemFacts>(StringComparer.Ordinal);
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return map;
        }

        foreach (var e in doc.Descendants()
                     .Where(x => x.Name.LocalName.StartsWith("Hmi.Screen.", StringComparison.Ordinal)
                                 && (string?)x.Attribute("CompositionName") == "ScreenItems"))
        {
            var al = e.Element("AttributeList");
            var name = al?.Element("ObjectName")?.Value;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var leaf in al!.Elements())
            {
                fields[leaf.Name.LocalName] = leaf.Value;
            }

            // Text payloads and fonts hang off the item's own ObjectList, keyed by the composition
            // they sit in - so a Button's TextOff and TextOn stay distinguishable.
            foreach (var multi in e.Descendants().Where(x => x.Name.LocalName is "MultilingualText"))
            {
                var comp = (string?)multi.Attribute("CompositionName") ?? "Text";
                foreach (var t in multi.Descendants().Where(x => x.Name.LocalName == "MultilingualTextItem"))
                {
                    var culture = t.Element("AttributeList")?.Element("Culture")?.Value ?? "?";
                    var text = t.Element("AttributeList")?.Element("Text");
                    // ReadInnerXml: TIA re-exports the payload as live nested XML where the emitter
                    // wrote it escaped. Both are the same content and must compare equal.
                    fields[$"{comp}[{culture}]"] = text is null ? string.Empty : InnerText(text);
                }
            }

            foreach (var font in e.Descendants().Where(x => x.Name.LocalName == "Hmi.Globalization.FontItem"))
            {
                var fal = font.Element("AttributeList");
                if (fal is null)
                {
                    continue;
                }

                var culture = fal.Element("Culture")?.Value ?? "?";
                foreach (var leaf in fal.Elements().Where(x => x.Name.LocalName != "Culture"))
                {
                    fields[$"Font[{culture}].{leaf.Name.LocalName}"] = leaf.Value;
                }
            }

            map[name] = new ItemFacts(e.Name.LocalName["Hmi.Screen.".Length..], fields);
        }

        return map;
    }

    /// <summary>
    /// The emitter writes the rich-text payload ESCAPED; TIA re-exports it as LIVE NESTED XML. Both
    /// are the same content and must compare equal.
    ///
    /// <c>DisableFormatting</c> is load-bearing: <c>XNode.ToString()</c> INDENTS nested elements by
    /// default, so the read-back side gained whitespace the escaped side never had and every text
    /// field compared as different. Widening this comparator without it turned one false green into
    /// 31 false positives on a single screen.
    /// </summary>
    private static string InnerText(XElement e) =>
        e.HasElements
            ? string.Concat(e.Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting)))
            : e.Value;

    /// <summary>The only normalisation: an empty paragraph written two ways is the same paragraph.</summary>
    private static string Normalise(string v) =>
        Regex.Replace(v, @"<(\w+)\s*/>", "<$1></$1>").Trim();
}
