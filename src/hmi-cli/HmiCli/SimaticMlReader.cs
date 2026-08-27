using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HmiCli;

/// <summary>
/// T8 TO-IR - classic-HMI SimaticML back to <see cref="ScreenIr"/>. The reverse of
/// <see cref="Emitter"/>, and the only route a screen a PERSON edited in TIA has back into our
/// source form.
/// </summary>
/// <remarks>
/// <para>
/// 🔴 <b>SIMATICML IS NEITHER A SUPERSET NOR A SUBSET OF THIS IR - IT IS AN OVERLAP, AND THE OUTPUT
/// IS THEREFORE A PARTIAL IR THAT SAYS SO.</b> SimaticML states dozens of attributes the IR has no
/// field for (every TIA-supplied default: <c>TabIndex</c>, <c>BackFillStyle</c>, the margins, the
/// limit colours). The IR carries things SimaticML never states at all: the CSS font stack, the box
/// shadow, the z tier, whether an element was a leaf, its declared zone, its accent role. Nothing
/// here invents a value to fill one of those - the field is left at its default and NAMED in
/// <see cref="ScreenIr.Unpopulatable"/>, so a consumer can tell "absent because the document did not
/// say" from "absent because it is genuinely off".
/// </para>
/// <para>
/// ⚠️ <b>ROUND-TRIP FIDELITY IS THE BAR, NOT COMPLETENESS.</b> The target is
/// <c>emit(to-ir(x)) == x</c> for everything the emitter can express - which is what makes a
/// structural diff between a hand-edited screen and our HTML possible. Anything outside that is a
/// NAMED REFUSAL in the report, never a silent skip: an unrecognised item type, an event whose
/// function list does not match a shape the emitter would produce, a property binding that is not
/// <c>ProcessValue</c>. Mislabelling one of those is worse than reporting it unmapped, because a
/// wrong label reads as knowledge.
/// </para>
/// <para>
/// Every structure read below was harvested from real documents: 26 screens a person edited in TIA
/// Portal, and one third-party export from an unrelated project. Where a mapping rests on inference
/// rather than on a specimen, the comment says so at the site.
/// </para>
/// </remarks>
public static class SimaticMlReader
{
    /// <summary>
    /// The SimaticML item element each IR type is read from. Deliberately the INVERSE of the
    /// emitter's own table rather than an independently written one - a reader whose vocabulary can
    /// drift from the writer's is a reader that silently stops recognising our own documents.
    ///
    /// <c>TextField</c> is the one rename: SimaticML's element is <c>Hmi.Screen.TextField</c> and
    /// the IR calls it <c>Text</c>.
    /// </summary>
    private static readonly Dictionary<string, string> ItemTypes = new(StringComparer.Ordinal)
    {
        ["Rectangle"] = "Rectangle",
        ["TextField"] = "Text",
        ["Button"] = "Button",
        ["Line"] = "Line",
        ["Circle"] = "Circle",
        ["IOField"] = "IOField",
        ["SymbolicIOField"] = "SymbolicIOField",
    };

    /// <summary>
    /// Structural elements that are not screen objects and must not be counted as one. A Screen and
    /// a ScreenLayer are the nesting; a Property is part of the item that owns it and is read there.
    /// </summary>
    private static readonly HashSet<string> Structural = new(StringComparer.Ordinal)
    {
        "Hmi.Screen.Screen", "Hmi.Screen.ScreenLayer", "Hmi.Screen.Property",
    };

    /// <summary>
    /// The IR fields this path can NEVER fill, with the reason each one is out of reach. Printed on
    /// every run and carried in the artifact, because the difference between "the document says this
    /// is off" and "the document cannot say" decides whether a downstream check means anything.
    /// </summary>
    public static readonly string[] CannotPopulate =
    {
        "panel - SimaticML states PIXELS only, and the KTP700 and KTP900 Basic share 800x480 while "
            + "differing ~28% physically (H-407). Declaring the wrong one is a quarter-scale sizing "
            + "error that passes every pixel check. Pass --panel to DECLARE it; it is never inferred.",
        "chromeVersion - there was no browser in this path.",
        "item.tag - the HTML element the item was authored from. SimaticML has no memory of it.",
        "item.zTier - CSS z-index. A classic screen has ONE layer and paints in document order.",
        "item.interactive - derived in the flattener from the element and data-hmi-mode. Reading "
            + "Enabled back would be a guess: a disabled Output IOField is not the same statement.",
        "item.leaf - whether the HTML element had children. Not a screen-object property at all.",
        "item.geometryless / item.ignored - authoring declarations. Anything in the document IS a "
            + "screen object, so neither can be true and neither can be recovered.",
        "item.overrideSpec - data-hmi-override. A suppression the author declared; not exported.",
        "item.zone / item.accentRole / item.accentFor - H-601/H-108/H-109 are DECLARED, never "
            + "inferred, and a checker that guessed them would be guessing about an operator screen.",
        "item.safetyCritical / item.alarmFlash - same: declared carve-outs, absent from SimaticML.",
        "item.textTruncated - a flattener condition, not a document one.",
        "item.backgroundImage / boxShadow / textShadow / borderRadius / animationName / "
            + "fontVariantNumeric - CSS the emitter never writes, so the document never carries it.",
        "item.fontStyleCss / textTransform / letterSpacing - the emitter REFUSES non-default values "
            + "for these (a classic FontItem cannot express them), so a document can only ever have "
            + "been produced from the defaults.",
        "item.fontFamilyCss / fontFamilyUsed - the emitter pins FontFamily to Tahoma and drops the "
            + "CSS stack, so the family in the document says nothing about what was authored.",
        "item.index - assigned in DOCUMENT ORDER here. In a flatten it is DOM order. The two agree "
            + "for a document this tool emitted and need not agree for one a person rearranged.",
        "type AlarmPlaceholder - the emitter expands one into a Rectangle plus a labelled TextField, "
            + "so it reads back as the two objects it became. The XML round-trips; the INTENT does not.",
    };

    /// <summary>
    /// The attributes the reader actually CONSUMES, per IR item type. Everything else an item's
    /// AttributeList states is part of the overlap - real in the document, with no IR field to hold
    /// it - and is COUNTED so the report can state how much of the document did not survive.
    ///
    /// ⚠️ This is a second statement of what <see cref="ReadItem"/> reads, so it can drift from it.
    /// It is kept because the alternative - instrumenting every read - would put bookkeeping through
    /// the middle of the mapping, and because drifting the wrong way is safe in one direction only:
    /// a name wrongly listed here UNDERSTATES the gap. A test asserts the two agree on the types
    /// this pipeline emits.
    /// </summary>
    private static readonly Dictionary<string, string[]> ConsumedAttributes = new(StringComparer.Ordinal)
    {
        ["Rectangle"] = new[] { "BackColor", "BorderColor", "Left", "Top", "Width", "Height", "ObjectName" },
        ["Text"] = new[] { "BackColor", "ForeColor", "Left", "Top", "Width", "Height", "ObjectName" },
        ["Button"] = new[] { "BackColor", "ForeColor", "BorderColor", "Left", "Top", "Width", "Height", "ObjectName" },
        ["Line"] = new[] { "Color", "Left", "Top", "Width", "Height", "ObjectName", "StartTop", "EndTop" },
        ["Circle"] = new[] { "BackColor", "BorderColor", "Left", "Top", "Width", "Height", "ObjectName" },
        ["IOField"] = new[]
        {
            "BackColor", "ForeColor", "BorderColor", "Left", "Top", "Width", "Height", "ObjectName",
            "Mode", "Unit", "FormatPattern", "DataFormat", "FieldLength",
        },
        ["SymbolicIOField"] = new[]
        {
            "BackColor", "ForeColor", "BorderColor", "Left", "Top", "Width", "Height", "ObjectName", "Mode",
        },
    };

    /// <summary>Exposed so a test can hold the table against what the emitter actually writes.</summary>
    public static IReadOnlyDictionary<string, string[]> ConsumedAttributeNames => ConsumedAttributes;

    /// <summary>One thing the reader met and did not put in the IR, with enough detail to act on.</summary>
    public sealed record Refusal(string Kind, string Where, string Detail);

    public sealed record Report(
        string SourcePath,
        string? ScreenName,
        int? ScreenNumber,
        int CanvasWidth,
        int CanvasHeight,
        int ObjectsFound,
        IReadOnlyDictionary<string, int> Recognised,
        IReadOnlyList<Refusal> NotRecognised,
        IReadOnlyList<Refusal> Unmapped,
        IReadOnlyList<string> Unpopulatable,
        int AttributesRead,
        IReadOnlyDictionary<string, IReadOnlyList<string>> AttributesOutsideTheIr)
    {
        /// <summary>Distinct (item type, attribute) pairs the IR has no field for. Keyed per TYPE
        /// deliberately: <c>BorderColor</c> is carried on a Button and dropped on a TextField, so a
        /// flat name list would report it as both, which is neither.</summary>
        public int AttributesOutsideCount => AttributesOutsideTheIr.Values.Sum(v => v.Count);

        public int RecognisedCount => Recognised.Values.Sum();

        /// <summary>Zero objects is never a pass - the same rule every other tool in this repo
        /// learned the hard way.</summary>
        public bool NothingRead => ObjectsFound == 0;

        /// <summary>Anything at all that did not reach the IR. Gates.</summary>
        public int SkippedCount => NotRecognised.Count + Unmapped.Count;
    }

    public sealed record ReadResult(ScreenIr Ir, Report Report);

    /// <summary>
    /// Read a classic-HMI SimaticML screen document.
    /// </summary>
    /// <param name="xml">The document.</param>
    /// <param name="sourcePath">Recorded in the IR's <c>source</c>; not read from disk here.</param>
    /// <param name="panel">
    /// The panel the caller DECLARES this screen is for, or null. Never inferred from the pixel
    /// dimensions - see <see cref="CannotPopulate"/>'s first entry.
    /// </param>
    public static ReadResult Read(string xml, string sourcePath, string? panel)
    {
        var doc = XDocument.Parse(xml);

        var notRecognised = new List<Refusal>();
        var unmapped = new List<Refusal>();
        var recognised = new Dictionary<string, int>(StringComparer.Ordinal);
        var items = new List<IrItem>();

        var screen = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Hmi.Screen.Screen");
        var screenAl = screen?.Element("AttributeList");
        var screenName = screenAl?.Element("Name")?.Value;
        var screenNumber = ParseInt(screenAl?.Element("Number")?.Value);
        var width = ParseInt(screenAl?.Element("Width")?.Value) ?? 0;
        var height = ParseInt(screenAl?.Element("Height")?.Value) ?? 0;

        var found = 0;
        var attributesSeen = 0;
        var outsideTheIr = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        // EVERY Hmi.Screen.* element is counted, not only the ones under a ScreenItems composition.
        //
        // 🔴 THIS IS THE DENOMINATOR AND THE OBVIOUS FILTER GETS IT WRONG. A `Hmi.Screen.SoftKey`
        // sits under composition "SoftKeys", directly on the screen rather than on the layer, so a
        // walk keyed on `CompositionName == "ScreenItems"` does not merely fail to read it - it
        // never SEES it, and the object vanishes from the count as well as from the IR. The
        // third-party export in the corpus has five of them. An absence that does not appear in the
        // denominator is precisely the failure this repo keeps closing.
        foreach (var e in doc.Descendants()
                     .Where(x => x.Name.LocalName.StartsWith("Hmi.Screen.", StringComparison.Ordinal))
                     .Where(x => !Structural.Contains(x.Name.LocalName)))
        {
            found++;
            var local = e.Name.LocalName["Hmi.Screen.".Length..];
            var name = e.Element("AttributeList")?.Element("ObjectName")?.Value ?? "(unnamed)";

            if (!ItemTypes.TryGetValue(local, out var irType))
            {
                // NAMED REFUSAL. A Group is called out separately because its consequence is
                // different in kind: its CHILDREN are ScreenItems and ARE read (they appear in this
                // same walk with absolute geometry), so what is lost is the GROUPING, not the
                // content. Saying "Group: unsupported" without that would read as "13 objects
                // dropped", which is false in the alarming direction.
                notRecognised.Add(local == "Group"
                    ? new Refusal("item-type", name,
                        $"Hmi.Screen.{local} - the IR has no grouping construct. Its "
                        + $"{e.Descendants().Count(x => (string?)x.Attribute("CompositionName") == "ScreenItems")} "
                        + "child object(s) ARE read, as loose items with their own absolute geometry; "
                        + "re-emitting them will not re-form the group.")
                    : new Refusal("item-type", name,
                        $"Hmi.Screen.{local}"
                        + (e.Attribute("CompositionName")?.Value is { } c && c != "ScreenItems"
                            ? $" (composition \"{c}\")"
                            : string.Empty)
                        + " - not an item type this pipeline can author or emit."));
                continue;
            }

            items.Add(ReadItem(e, doc, irType, name, items.Count, unmapped));
            recognised[irType] = recognised.GetValueOrDefault(irType) + 1;

            // THE ATTRIBUTE-LEVEL DENOMINATOR. An object being "recognised" says the IR has a type
            // for it, not that the IR has a field for everything it states - and on a real screen
            // that difference is hundreds of attributes. Counting them turns "partial" from an
            // adjective into a number.
            var consumed = ConsumedAttributes[irType];
            foreach (var leaf in e.Element("AttributeList")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                attributesSeen++;
                if (!consumed.Contains(leaf.Name.LocalName, StringComparer.Ordinal))
                {
                    if (!outsideTheIr.TryGetValue(irType, out var set))
                    {
                        set = new SortedSet<string>(StringComparer.Ordinal);
                        outsideTheIr[irType] = set;
                    }

                    set.Add(leaf.Name.LocalName);
                }
            }
        }

        var report = new Report(
            sourcePath, screenName, screenNumber, width, height, found,
            recognised, notRecognised, unmapped, CannotPopulate,
            attributesSeen,
            outsideTheIr.ToDictionary(
                k => k.Key,
                k => (IReadOnlyList<string>)k.Value.ToList(),
                StringComparer.Ordinal));

        var ir = new ScreenIr
        {
            // DECLARED or EMPTY. An empty panel makes the IR un-lintable downstream, which is the
            // correct outcome: LoadIr refuses it by name rather than sizing against a guess.
            Panel = panel ?? string.Empty,
            Family = "Classic",
            CanvasWidth = width,
            CanvasHeight = height,
            Source = sourcePath,
            SourceKind = "simaticml",
            Unpopulatable = CannotPopulate.ToList(),
            ChromeVersion = string.Empty,
            Items = items,
        };

        return new ReadResult(ir, report);
    }

    private static IrItem ReadItem(XElement e, XDocument doc, string irType, string name, int index,
        List<Refusal> unmapped)
    {
        var al = e.Element("AttributeList");

        var item = new IrItem
        {
            Index = index,
            Type = irType,
            // The ObjectName becomes the ElementId, which is what makes the round trip stable: the
            // emitter uses an item's id as its ObjectName, so a document read in and written out
            // keeps every name. Without this the emitter would fall back to POSITIONAL names and a
            // single inserted object would rename everything after it - the false-diff defect the
            // stable-name work already fixed once, re-entering through this door.
            ElementId = name == "(unnamed)" ? null : name,
            Left = Num(al, "Left"),
            Top = Num(al, "Top"),
            Width = Num(al, "Width"),
            Height = Num(al, "Height"),
        };

        // Colours come back as CSS rgb(), because that is the only form the emitter's own parser
        // reads. Round-tripping through the emitter's Colour() is what keeps the two honest.
        item = item with
        {
            // 🔴 A LINE'S COLOUR IS ITS `Color`, NOT ITS `BackColor`. The emitter writes the house
            // grey into a Line's BackColor unconditionally and puts the authored colour in `Color`,
            // so reading BackColor here would give every line the same wrong colour AND round-trip
            // cleanly, which is the worst combination.
            BackColor = Css(al, irType == "Line" ? "Color" : "BackColor"),
            ForeColor = Css(al, "ForeColor"),
            BorderColor = Css(al, "BorderColor"),
        };

        var font = e.Descendants().FirstOrDefault(x => x.Name.LocalName == "Hmi.Globalization.FontItem")
            ?.Element("AttributeList");
        if (font is not null)
        {
            item = item with
            {
                FontSizePx = ParseInt(font.Element("FontSize")?.Value) ?? 0,
                // FontStyle carries Regular or Bold and nothing between, so the weight comes back as
                // one of two numbers rather than as whatever CSS originally said. 700/400 are the
                // canonical pair on either side of the emitter's own >= 600 test.
                FontWeight = font.Element("FontStyle")?.Value == "Bold" ? 700 : 400,
            };
        }

        item = irType switch
        {
            "Text" => item with { Text = TextOf(e, "Text", unmapped, name) },
            "Button" => ReadButton(e, doc, item, name, unmapped),
            "Line" => item with { LineDirection = LineDirection(al) },
            "IOField" => ReadIOField(al, item),
            "SymbolicIOField" => item with
            {
                Mode = al?.Element("Mode")?.Value,
                TextList = ItemLink(e, doc, "TextList"),
            },
            _ => item,
        };

        item = item with { Visibility = ReadVisibility(e, doc, name, unmapped) };
        item = ReadProperties(e, doc, item, name, unmapped);

        ReportUnmappedAnimations(e, name, unmapped);

        return item;
    }

    private static IrItem ReadIOField(XElement? al, IrItem item)
    {
        var pattern = al?.Element("FormatPattern")?.Value ?? string.Empty;
        var isString = al?.Element("DataFormat")?.Value == "String";

        return item with
        {
            Mode = al?.Element("Mode")?.Value,
            Unit = al?.Element("Unit")?.Value,
            // The two are MUTUALLY EXCLUSIVE in the IR and the emitter refuses a field declaring
            // both - see StringFieldException. DataFormat is what decides which one this was, and
            // for a string field the length is the pattern's, which is the same invariant
            // FieldLength states. Reading the pattern rather than FieldLength keeps the derivation
            // in one place: Coherence already gates the two agreeing.
            StringLength = isString ? pattern.Length.ToString(CultureInfo.InvariantCulture) : null,
            Format = isString ? null : (pattern.Length == 0 ? null : pattern),
        };
    }

    /// <summary>
    /// Which diagonal the line runs along. <c>StartTop &gt; EndTop</c> is the "up" diagonal
    /// (bottom-left to top-right); anything else, including a horizontal line where the two are
    /// equal, is the default "down".
    /// </summary>
    private static string? LineDirection(XElement? al)
    {
        var st = ParseInt(al?.Element("StartTop")?.Value);
        var et = ParseInt(al?.Element("EndTop")?.Value);
        return st is not null && et is not null && st > et ? "up" : null;
    }

    // ---------------------------------------------------------------------------- properties

    /// <summary>
    /// A <c>Hmi.Screen.Property</c> is how a property is connected to a tag. The IR has ONE field
    /// for this (<c>bind</c>) and it means <c>ProcessValue</c>, so anything else is reported by
    /// name rather than folded into it.
    /// </summary>
    private static IrItem ReadProperties(XElement e, XDocument doc, IrItem item, string owner,
        List<Refusal> unmapped)
    {
        foreach (var prop in e.Descendants().Where(x => x.Name.LocalName == "Hmi.Screen.Property"))
        {
            var pname = prop.Element("AttributeList")?.Element("Name")?.Value ?? "(unnamed)";
            var dyn = prop.Descendants()
                .FirstOrDefault(x => x.Name.LocalName.StartsWith("Hmi.Dynamic.", StringComparison.Ordinal));

            if (dyn is null)
            {
                unmapped.Add(new Refusal("property", owner,
                    $"property \"{pname}\" carries no dynamization - nothing to read."));
                continue;
            }

            var kind = dyn.Name.LocalName["Hmi.Dynamic.".Length..];
            var tag = SimaticLinks.Single(dyn, doc);

            if (pname != "ProcessValue")
            {
                // Visible, Enabled, BackColor and the rest are all bindable this way, and the IR has
                // no field for any of them. Named, with the tag, so the reader of the report knows
                // exactly what was on the screen and is not there any more.
                unmapped.Add(new Refusal("property", owner,
                    $"property \"{pname}\" is bound via {kind} to \"{tag ?? "(no single link)"}\" - the "
                    + "IR's `bind` field means ProcessValue and nothing else, so this binding is NOT "
                    + "in the IR and would be LOST on a re-emit."));
                continue;
            }

            if (kind != "TagConnectionDynamic")
            {
                unmapped.Add(new Refusal("property", owner,
                    $"ProcessValue is driven by {kind}, not a plain TagConnectionDynamic. Only the "
                    + "plain tag connection maps to `bind`."));
                continue;
            }

            if (tag is null)
            {
                unmapped.Add(new Refusal("property", owner,
                    "ProcessValue's tag connection carries no single link target - it is either "
                    + "unset or wired to something this reader has not met."));
                continue;
            }

            item = item with { Bind = tag };
        }

        return item;
    }

    /// <summary>An <c>@OpenLink</c> hanging off the ITEM's own LinkList, beside its AttributeList -
    /// where a SymbolicIOField's TextList sits, one level up from where a tag binding goes.</summary>
    private static string? ItemLink(XElement e, XDocument doc, string linkName)
    {
        var link = e.Element("LinkList")?.Elements().FirstOrDefault(x => x.Name.LocalName == linkName);
        return link is null ? null : SimaticLinks.Single(link, doc);
    }

    // ---------------------------------------------------------------------------- animations

    private static IrVisibility? ReadVisibility(XElement e, XDocument doc, string owner,
        List<Refusal> unmapped)
    {
        // DIRECT children of the item's own ObjectList only. An item nested inside a Group would
        // otherwise inherit its parent's animation through a Descendants() walk, and a shape that
        // silently acquires somebody else's visibility rule is worse than one that has none.
        var anims = e.Element("ObjectList")?.Elements()
            .Where(x => x.Name.LocalName == "Hmi.Dynamic.VisibilityAnimation")
            .ToList() ?? new List<XElement>();

        if (anims.Count == 0)
        {
            return null;
        }

        if (anims.Count > 1)
        {
            unmapped.Add(new Refusal("animation", owner,
                $"{anims.Count} visibility animations on one object; the IR holds one. The first is "
                + "read and the rest are NOT in the IR."));
        }

        var a = anims[0];
        var al = a.Element("AttributeList");
        var trigger = a.Element("ObjectList")?.Elements()
            .FirstOrDefault(x => x.Name.LocalName == "Hmi.Dynamic.TagElementTrigger");
        var tag = trigger is null ? null : SimaticLinks.Single(trigger, doc);

        if (tag is null)
        {
            unmapped.Add(new Refusal("animation", owner,
                "visibility animation with no readable TagElementTrigger tag - it is not in the IR, "
                + "because an animation with no trigger is a rule that never fires and the emitter "
                + "refuses to write one."));
            return null;
        }

        return new IrVisibility
        {
            Tag = tag,
            RangeStart = al?.Element("RangeStart")?.Value ?? "0",
            RangeEnd = al?.Element("RangeEnd")?.Value ?? "0",
            Visible = al?.Element("Visible")?.Value == "true",
        };
    }

    /// <summary>
    /// Animations that are not visibility. <c>RangeAppearanceAnimation</c> (colour by value band)
    /// is the one the third-party corpus uses - ten of them - and there is no IR field for it.
    /// </summary>
    private static void ReportUnmappedAnimations(XElement e, string owner, List<Refusal> unmapped)
    {
        foreach (var a in e.Element("ObjectList")?.Elements()
                     .Where(x => (string?)x.Attribute("CompositionName") == "Animations"
                                 && x.Name.LocalName != "Hmi.Dynamic.VisibilityAnimation")
                 ?? Enumerable.Empty<XElement>())
        {
            unmapped.Add(new Refusal("animation", owner,
                $"{a.Name.LocalName} - the IR carries only VisibilityAnimation. This animation is "
                + "NOT in the IR and would be LOST on a re-emit."));
        }
    }

    // ---------------------------------------------------------------------------- events

    private sealed record Param(string Name, string Value, bool IsLink);

    private sealed record Fn(string Name, List<Param> Params)
    {
        public Param? P(string n) => Params.FirstOrDefault(x => x.Name == n);

        public override string ToString() =>
            $"{Name}({string.Join(", ", Params.Select(p => $"{p.Name}={(p.IsLink ? "@" : string.Empty)}{p.Value}"))})";
    }

    /// <summary>
    /// A button's behaviour, read back out of its ordered function list.
    ///
    /// 🔴 ORDER IS LOAD-BEARING AND IS WHAT THE DECODER KEYS ON. A command is
    /// <c>SetTag(&lt;channel&gt;_Code, code)</c> - and any operands - followed LAST by
    /// <c>IncreaseTag(&lt;channel&gt;_Seq, 1)</c>. The controller reads the code when the sequence
    /// number CHANGES, so the bump is the commit and it comes at the end. Recognising the shape
    /// therefore starts from the END of the list and works backwards.
    ///
    /// 🔴 WHERE THE SHAPE DOES NOT MATCH, THE LIST IS REPORTED VERBATIM AND NOTHING IS SET.
    /// A hand-built TIA event frequently will not fit the authoring attributes - it can call any of
    /// the system functions, in any order, on any event. Forcing one into <c>cmd</c> or
    /// <c>setTag</c> would produce an IR that emits a DIFFERENT program from the one on the panel,
    /// with every gate green. An unmapped event leaves the button inert in the IR, which the
    /// emitter then reports as a hand-off - visible, and true.
    /// </summary>
    private static IrItem ReadButton(XElement e, XDocument doc, IrItem item, string owner,
        List<Refusal> unmapped)
    {
        var events = e.Element("ObjectList")?.Elements()
            .Where(x => x.Name.LocalName == "Hmi.Event.Event")
            .ToList() ?? new List<XElement>();

        foreach (var ev in events)
        {
            var evName = ev.Element("AttributeList")?.Element("Name")?.Value ?? "(unnamed)";
            var fns = ev.Descendants()
                .Where(x => x.Name.LocalName == "Hmi.Event.FunctionListEntry")
                .Select(f => ReadFn(f, doc))
                .ToList();

            // The emitter writes exactly one event, "Release" - a touch that lands on the wrong
            // control can still be cancelled by sliding off before lifting. "Press", "KeyDown" and
            // the SoftKey events are all real and all outside what these authoring attributes can
            // express, so they are named rather than treated as a Release.
            if (evName != "Release")
            {
                unmapped.Add(new Refusal("event", owner,
                    $"\"{evName}\" event with {fns.Count} function(s): {Describe(fns)}. The IR can "
                    + "only express a Release, so this event is NOT in the IR."));
                continue;
            }

            var decoded = DecodeRelease(fns, out var reason);
            if (decoded is null)
            {
                unmapped.Add(new Refusal("event", owner,
                    $"Release: {reason} Function list, in order: {Describe(fns)}. Nothing was "
                    + "written to the IR for this event - a wrong label would read as knowledge."));
                continue;
            }

            item = decoded(item);
        }

        // 🔴 A BUTTON IS A TWO-STATE OBJECT AND THE IR HOLDS ONE CAPTION.
        //
        // TIA gives a Button a TextOff and a TextOn; the emitter writes the IR's single `text` into
        // BOTH, which is right for a momentary command button and cannot express a button whose two
        // states read differently. MEASURED on a hand-edited screen: two paging buttons came back
        // with TextOff "NEXT"/"PREV" and TextOn still "SELECT" - left over from the object they were
        // copied from. Reading TextOff and saying nothing would silently rewrite TextOn on the way
        // out, which is a change to the screen made by the tool rather than by anybody.
        var off = TextOf(e, "TextOff", unmapped, owner);
        var on = TextOf(e, "TextOn", unmapped, owner);

        if (on is not null && off != on)
        {
            unmapped.Add(new Refusal("text", owner,
                $"TextOff \"{off}\" and TextOn \"{on}\" differ. The IR holds ONE caption (TextOff is "
                + "taken), so a re-emit would write the off caption into both and the on caption "
                + "would be LOST."));
        }

        return item with { Text = off };
    }

    private static string Describe(List<Fn> fns) =>
        fns.Count == 0 ? "(empty)" : string.Join(" -> ", fns.Select(f => f.ToString()));

    /// <summary>
    /// The three shapes the emitter can produce on a Release, recognised from the end of the list
    /// backwards. Returns null with a reason when the list is anything else.
    /// </summary>
    private static Func<IrItem, IrItem>? DecodeRelease(List<Fn> fns, out string reason)
    {
        reason = string.Empty;
        var rest = new List<Fn>(fns);
        string? goTo = null;

        if (rest.Count == 0)
        {
            reason = "the function list is empty.";
            return null;
        }

        // NAVIGATION, always last: a button that both acts and navigates must send its command
        // while this screen is still the active one.
        if (rest[^1].Name == "ActivateScreen")
        {
            var nav = rest[^1];
            var target = nav.P("Screen name");
            var objectNumber = nav.P("Object number");

            if (target is null || !target.IsLink)
            {
                reason = "ActivateScreen's \"Screen name\" is not a link to a screen.";
                return null;
            }

            // The emitter always writes 0. A non-zero object number selects a control to focus on
            // arrival, which the IR cannot express - so it is a refusal, not a detail to drop.
            if (objectNumber is not null && objectNumber.Value.Trim() != "0")
            {
                reason = $"ActivateScreen carries \"Object number\" {objectNumber.Value}, which "
                       + "selects a control to focus on arrival; the IR can only express 0.";
                return null;
            }

            goTo = target.Value;
            rest.RemoveAt(rest.Count - 1);
        }

        if (rest.Count == 0)
        {
            return i => i with { GoTo = goTo };
        }

        // A COMMAND: the sequence bump is the commit and is always last of what remains.
        if (rest[^1].Name == "IncreaseTag")
        {
            var bump = rest[^1];
            var tag = bump.P("Tag");
            var by = bump.P("Value");

            if (tag is null || !tag.IsLink || !tag.Value.EndsWith("_Seq", StringComparison.Ordinal))
            {
                reason = "an IncreaseTag that does not bump a \"<channel>_Seq\" tag.";
                return null;
            }

            if (by is null || by.IsLink || !IsOne(by.Value))
            {
                reason = $"the sequence is increased by \"{by?.Value ?? "(nothing)"}\" rather than by 1.";
                return null;
            }

            var channel = tag.Value[..^"_Seq".Length];
            rest.RemoveAt(rest.Count - 1);

            string? code = null, int1 = null, int2 = null, real1 = null, real2 = null;
            foreach (var f in rest)
            {
                if (f.Name != "SetTag")
                {
                    reason = $"a \"{f.Name}\" between the code and the sequence bump; a command "
                           + "channel write is SetTag only.";
                    return null;
                }

                var t = f.P("Tag");
                var v = f.P("Value");
                if (t is null || !t.IsLink || v is null)
                {
                    reason = "a SetTag whose Tag is not a link or which carries no Value.";
                    return null;
                }

                if (!t.Value.StartsWith(channel + "_", StringComparison.Ordinal))
                {
                    reason = $"\"{t.Value}\" is written in the same press as channel \"{channel}\" but "
                           + "is not one of its tags. A command's operands all belong to one channel.";
                    return null;
                }

                var slot = t.Value[(channel.Length + 1)..];
                var written = v.IsLink ? "@" + v.Value : v.Value;
                switch (slot)
                {
                    case "Code": code = written; break;
                    case "Int1": int1 = written; break;
                    case "Int2": int2 = written; break;
                    case "Real1": real1 = written; break;
                    case "Real2": real2 = written; break;
                    default:
                        reason = $"\"{t.Value}\" is not a slot the IR has a field for "
                               + "(_Code, _Int1, _Int2, _Real1, _Real2).";
                        return null;
                }
            }

            return i => i with
            {
                Cmd = channel, CmdCode = code, CmdInt1 = int1, CmdInt2 = int2,
                CmdReal1 = real1, CmdReal2 = real2, GoTo = goTo,
            };
        }

        // A STAGED OPERAND: exactly one write, no sequence bump, so nothing is COMMANDED.
        if (rest.Count == 1 && rest[0].Name == "SetTag")
        {
            var t = rest[0].P("Tag");
            var v = rest[0].P("Value");

            if (t is null || !t.IsLink || v is null)
            {
                reason = "a lone SetTag whose Tag is not a link or which carries no Value.";
                return null;
            }

            // Refused for the same reason data-hmi-set refuses it: a code staged here and bumped by
            // a later press is a command assembled out of parts, in an order somebody chose.
            if (t.Value.EndsWith("_Seq", StringComparison.Ordinal)
                || t.Value.EndsWith("_Code", StringComparison.Ordinal))
            {
                reason = $"a lone write to \"{t.Value}\". data-hmi-set cannot name a _Seq or a _Code, "
                       + "so this cannot be expressed in the IR even though TIA accepts it.";
                return null;
            }

            var value = v.IsLink ? "@" + v.Value : v.Value;
            return i => i with { SetTag = $"{t.Value}={value}", GoTo = goTo };
        }

        reason = $"{rest.Count} function(s) that do not form a command (code then sequence bump) or "
               + "a single staged write.";
        return null;
    }

    /// <summary>
    /// 1, 1.0 and 1.00 are the same bump. TIA writes the parameter as a <c>System.Double</c>, so the
    /// spelling that comes back out of a round trip need not be the spelling that went in - and a
    /// decoder that compared the strings would report a perfectly ordinary command as unmapped.
    /// Inferred from the parameter's declared CLR type, not measured against a re-export.
    /// </summary>
    private static bool IsOne(string v) =>
        double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
        && Math.Abs(d - 1.0) < 1e-9;

    private static Fn ReadFn(XElement f, XDocument doc)
    {
        var name = f.Element("AttributeList")?.Element("Name")?.Value ?? "(unnamed)";
        var ps = new List<Param>();

        foreach (var p in f.Descendants().Where(x => x.Name.LocalName == "Hmi.Event.FunctionListEntryParameter"))
        {
            var al = p.Element("AttributeList");
            var pn = al?.Element("Name")?.Value ?? "(unnamed)";

            // 🔴 A PARAMETER IS EITHER A TYPED LITERAL IN THE AttributeList OR AN @OpenLink IN THE
            // LinkList, AND THE DIFFERENCE IS THE WHOLE MEANING. `Value=3` writes the number three;
            // `Value=@Bay_State` copies that tag's LIVE value at the press. Two commands in the
            // corpus are impossible without the second form. Read as one, a literal 3 and a tag
            // named "3" would be indistinguishable, so the IR marks the tag form with a leading @.
            var literal = al?.Elements().FirstOrDefault(x => x.Name.LocalName == "Value");
            if (literal is not null)
            {
                ps.Add(new Param(pn, literal.Value, false));
                continue;
            }

            var link = SimaticLinks.Single(p, doc);
            ps.Add(new Param(pn, link ?? string.Empty, link is not null));
        }

        return new Fn(name, ps);
    }

    // ---------------------------------------------------------------------------- text

    private static readonly Regex BodyWrapper =
        new(@"^\s*<body>\s*(?<inner>.*?)\s*</body>\s*$", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex Paragraph =
        new(@"<p\s*/>|<p[^>]*>(?<t>.*?)</p>", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// The text payload out of one of an item's MultilingualText compositions.
    ///
    /// The emitter writes <c>&lt;body&gt;&lt;p&gt;TEXT&lt;/p&gt;&lt;/body&gt;</c> ESCAPED; TIA
    /// re-exports the same content as LIVE NESTED XML. Both forms are handled, which is the same
    /// asymmetry ScreenCompare had to absorb.
    ///
    /// ⚠️ A payload carrying anything richer than one paragraph of plain text - several paragraphs,
    /// a span, an inline style - is REPORTED. The IR holds a flat string, so the markup is genuinely
    /// gone; the text itself is still taken, because losing the words as well would be worse.
    /// </summary>
    private static string? TextOf(XElement item, string composition, List<Refusal> unmapped, string owner)
    {
        var multi = item.Element("ObjectList")?.Elements()
            .FirstOrDefault(x => x.Name.LocalName == "MultilingualText"
                                 && (string?)x.Attribute("CompositionName") == composition);

        var textEl = multi?.Descendants()
            .FirstOrDefault(x => x.Name.LocalName == "MultilingualTextItem")
            ?.Element("AttributeList")?.Element("Text");

        if (textEl is null)
        {
            return null;
        }

        var raw = textEl.HasElements
            ? string.Concat(textEl.Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting)))
            : textEl.Value;

        var m = BodyWrapper.Match(raw);
        if (!m.Success)
        {
            unmapped.Add(new Refusal("text", owner,
                $"{composition} payload is not the expected <body>...</body> wrapper: \"{Clip(raw)}\". "
                + "Taken verbatim into the IR."));
            return raw;
        }

        var inner = m.Groups["inner"].Value;
        var paras = Paragraph.Matches(inner);

        if (paras.Count == 0)
        {
            unmapped.Add(new Refusal("text", owner,
                $"{composition} payload has no paragraph: \"{Clip(raw)}\". Taken verbatim."));
            return inner;
        }

        var parts = paras.Select(x => System.Net.WebUtility.HtmlDecode(x.Groups["t"].Value)).ToList();
        var stripped = string.Concat(paras.Select(x => x.Value));

        if (paras.Count > 1 || stripped.Trim() != inner.Trim())
        {
            unmapped.Add(new Refusal("text", owner,
                $"{composition} payload carries rich text the IR cannot hold ({paras.Count} "
                + $"paragraph(s), markup outside them): \"{Clip(raw)}\". The words are kept, joined "
                + "with a space; the MARKUP is NOT in the IR."));
        }

        if (parts.Any(p => p.Contains('<')))
        {
            unmapped.Add(new Refusal("text", owner,
                $"{composition} paragraph contains inline markup: \"{Clip(raw)}\". Kept as written; "
                + "it will be re-emitted escaped, not as markup."));
        }

        return string.Join(" ", parts).Trim();
    }

    private static string Clip(string s) =>
        s.Length <= 60 ? s : s[..60] + "...";

    // ---------------------------------------------------------------------------- primitives

    private static double Num(XElement? al, string name) =>
        double.TryParse(al?.Element(name)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;

    private static int? ParseInt(string? s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>SimaticML <c>"r, g, b"</c> to CSS <c>rgb(r, g, b)</c> - the only form the emitter's
    /// own colour parser reads, so a value read here survives being written straight back.</summary>
    private static string? Css(XElement? al, string name)
    {
        var v = al?.Element(name)?.Value;
        if (string.IsNullOrWhiteSpace(v))
        {
            return null;
        }

        var m = Regex.Match(v, @"^\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*$");
        return m.Success ? $"rgb({m.Groups[1].Value}, {m.Groups[2].Value}, {m.Groups[3].Value})" : null;
    }
}
