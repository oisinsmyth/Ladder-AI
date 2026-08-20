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
                else if (IsBehavioural(key))
                {
                    // 🔴 A BEHAVIOURAL KEY PRESENT ONLY IN THE SECOND DOCUMENT IS AN ADDITION, NOT
                    // A DEFAULT.
                    //
                    // Every second-only field used to be counted as DEFAULTED-BY-TIA, which is
                    // right for an ATTRIBUTE - TIA genuinely fills in BackColor, EdgeStyle and a
                    // hundred others the emitter never states. It is wrong for a dynamization:
                    // TIA does not invent a Property binding, an Event or an Animation.
                    //
                    // Measured: a screen came back with six objects newly hidden by a
                    // VisibilityAnimation and the comparator reported NOTHING, because every one of
                    // those keys was second-only and went into the defaulted bucket. The widened
                    // walk found the fields and the counting rule then threw them away again.
                    changed.Add($"{name}.{key}: ADDED '{vb}' (absent from the first document)");
                }
                else
                {
                    defaulted++;
                }
            }
        }

        return new Result(a.Keys.Union(b.Keys, StringComparer.Ordinal).Count(), both, changed, dropped, defaulted);
    }

    /// <summary>
    /// Is this field key part of what the screen DOES, rather than what it looks like?
    ///
    /// The distinction decides whether a one-sided field is an ADDITION or a TIA default, and it is
    /// keyed on the three compositions TIA never fabricates: a Property binding, an Event, and an
    /// Animation. Everything else - colours, margins, edge styles - TIA fills in freely, and
    /// treating those as additions would bury a real change under hundreds of them.
    /// </summary>
    private static bool IsBehavioural(string key) =>
        key.StartsWith("Property[", StringComparison.Ordinal)
        || key.StartsWith("Event[", StringComparison.Ordinal)
        || key.StartsWith("Animation[", StringComparison.Ordinal);

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

            // 🔴 THE BEHAVIOURAL HALF OF A SCREEN, WHICH THIS COMPARATOR DID NOT LOOK AT AT ALL.
            //
            // Until 2026-08-20 Parse read the AttributeList, the text payloads and the fonts - the
            // PICTURE - and nothing else. It never walked a Property, an Event or an Animation. So
            // it was blind to:
            //
            //   * WHICH TAG A FIELD DISPLAYS   (Hmi.Screen.Property -> dynamization -> Tag)
            //   * WHAT A BUTTON DOES           (Hmi.Event.Event -> the ordered function list)
            //   * WHEN AN OBJECT IS VISIBLE    (Hmi.Dynamic.*Animation -> its trigger tag)
            //
            // MEASURED, and this is why it is being fixed rather than merely widened: a real screen
            // came back from TIA with all EIGHT of its select buttons retargeted from one tag to
            // another - the change that decides which recipe the button starts - and compare
            // reported `4 changed, 3 dropped` WITHOUT NAMING ONE OF THEM. It saw two objects added
            // and a field nudged a pixel, and called the rest identical.
            //
            // That is this project's recurring failure in its worst form: the tool whose whole job
            // is "prove the round trip is faithful" was answering a question about geometry.
            //
            // Ordering is preserved deliberately. A function list's ORDER is the PLC command
            // handshake - code written before the sequence bumps - so a reordered list is a
            // different program, and a comparator that treated the list as a set would call it equal.
            foreach (var prop in e.Descendants().Where(x => x.Name.LocalName == "Hmi.Screen.Property"))
            {
                var pname = prop.Element("AttributeList")?.Element("Name")?.Value ?? "?";
                foreach (var dyn in prop.Descendants().Where(x => x.Name.LocalName.StartsWith("Hmi.Dynamic.", StringComparison.Ordinal)))
                {
                    fields[$"Property[{pname}].dynamic"] = dyn.Name.LocalName;
                    fields[$"Property[{pname}].tag"] = LinkTargets(dyn, doc);
                }
            }

            // 🔴 ANIMATIONS ARE KEYED BY NAME **AND POSITION**, BECAUSE THE NAME IS NOT UNIQUE.
            //
            // TIA names EVERY visibility animation "VisibilityAnimation" - all twenty of them in a
            // real export of one screen on this project. Keying on the name alone meant two
            // animations on one object collapsed into ONE dictionary entry, the second silently
            // overwriting the first, so a DROPPED animation compared IDENTICAL.
            //
            // That is not hypothetical. Measured 2026-08-20: a Classic screen item accepts exactly
            // one VisibilityAnimation and TIA DISCARDS a second on import - silently, with a green
            // import and a green compile. So the one case this comparator exists to catch is
            // precisely the case it could not see.
            //
            // The first occurrence keeps the bare name; later ones take a #n suffix. The per-name
            // COUNT is emitted as its own field, so a lost animation is a difference in its own
            // right and not merely an absent key - the same shape as the event list's .count.
            var animations = e.Descendants()
                .Where(x => x.Name.LocalName.StartsWith("Hmi.Dynamic.", StringComparison.Ordinal)
                            && (string?)x.Attribute("CompositionName") == "Animations")
                .ToList();

            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var anim in animations)
            {
                var rawName = anim.Element("AttributeList")?.Element("Name")?.Value ?? anim.Name.LocalName;
                seen.TryGetValue(rawName, out var already);
                seen[rawName] = already + 1;

                var aname = already == 0 ? rawName : $"{rawName}#{already + 1}";

                foreach (var leaf in anim.Element("AttributeList")?.Elements() ?? Enumerable.Empty<XElement>())
                {
                    fields[$"Animation[{aname}].{leaf.Name.LocalName}"] = leaf.Value;
                }

                fields[$"Animation[{aname}].trigger"] = LinkTargets(anim, doc);
            }

            foreach (var pair in seen)
            {
                fields[$"Animation[{pair.Key}].count"] = pair.Value.ToString(CultureInfo.InvariantCulture);
            }

            foreach (var ev in e.Descendants().Where(x => x.Name.LocalName == "Hmi.Event.Event"))
            {
                var evname = ev.Element("AttributeList")?.Element("Name")?.Value ?? "?";
                var n = 0;
                foreach (var fn in ev.Descendants().Where(x => x.Name.LocalName == "Hmi.Event.FunctionListEntry"))
                {
                    var fal = fn.Element("AttributeList");
                    var fnName = fal?.Element("Name")?.Value ?? "?";
                    var ps = new List<string>();
                    foreach (var pr in fn.Descendants().Where(x => x.Name.LocalName == "Hmi.Event.FunctionListEntryParameter"))
                    {
                        var pal = pr.Element("AttributeList");
                        var pn = pal?.Element("Name")?.Value ?? "?";

                        // A parameter is EITHER a typed literal in the AttributeList OR an
                        // @OpenLink in the LinkList. The '@' marks which, so a literal 5 and a tag
                        // named "5" can never compare equal.
                        var lit = pal?.Elements().FirstOrDefault(x => x.Name.LocalName == "Value");
                        ps.Add(lit is not null ? $"{pn}={lit.Value}" : $"{pn}=@{LinkTargets(pr, doc)}");
                    }

                    fields[$"Event[{evname}].{n}"] = $"{fnName}({string.Join(", ", ps)})";
                    n++;
                }

                // The COUNT is stated separately so a function REMOVED from the end of a list is a
                // change, not a silently absent key that pairs with nothing.
                fields[$"Event[{evname}].count"] = n.ToString(CultureInfo.InvariantCulture);
            }

            map[name] = new ItemFacts(e.Name.LocalName["Hmi.Screen.".Length..], fields);
        }

        return map;
    }

    /// <summary>
    /// Every link target under <paramref name="e"/>, in document order, as one comparable string.
    ///
    /// Moved to <see cref="SimaticLinks"/> on 2026-08-20 when <c>to-ir</c> needed the same reading.
    /// Two readers of the same document that each know a different set of link forms is how one of
    /// them ends up silently reporting a wired link as empty - which had already happened once here,
    /// with the <c>TargetID="#8A9"</c> form. One implementation, both callers.
    /// </summary>
    private static string LinkTargets(XElement e, XDocument doc) => SimaticLinks.Joined(e, doc);

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
