using System.Globalization;
using System.Text;
using System.Xml;

namespace HmiCli;

/// <summary>
/// T5 EMIT - <see cref="ScreenIr"/> to classic-HMI SimaticML.
///
/// This is the half of the converter that makes the whole thing "HTML to HMI" rather than two
/// disconnected pieces: T1 resolves CSS into absolute integer geometry, and this turns that geometry
/// into the only document a classic screen can be built from.
/// </summary>
/// <remarks>
/// <para>
/// Structure was taken from a real TIA export rather than from documentation. Items do NOT sit
/// directly under the screen: the nesting is
/// <c>Screen -> ObjectList -> ScreenLayer(CompositionName="Layers") -> ObjectList -> item(CompositionName="ScreenItems")</c>.
/// Getting that wrong produces a document that imports into nothing.
/// </para>
/// <para>
/// 🔴 <b>This emitter may only claim to produce what SimaticML can represent.</b> An alarm view is
/// NOT representable - measured: a screen carrying one exports with no object for it at all, and
/// re-importing such a screen destroys the control. So an alarm view is emitted as a VISIBLE
/// PLACEHOLDER plus a hand-off item, never as a silent omission, and any item type this emitter does
/// not know is a hard error rather than a skip (ADR-0010's shape).
/// </para>
/// </remarks>
public static class Emitter
{
    /// <summary>Item types this emitter can faithfully produce. Anything else is refused by name.</summary>
    private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
    {
        "Rectangle", "Text", "Button", "Line", "Circle", "AlarmPlaceholder",
    };

    public static IReadOnlyCollection<string> SupportedTypes => Supported;

    public sealed record EmitResult(string Xml, int ItemCount, IReadOnlyList<string> HandOff);

    public static EmitResult Emit(ScreenIr ir, string screenName, int screenNumber)
    {
        var unknown = ir.Items
            .Where(i => i.Type is not null && !Supported.Contains(i.Type))
            .Select(i => i.Type!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (unknown.Count > 0)
        {
            // Named refusal, never a silent drop: an item quietly omitted here produces a screen that
            // imports clean, compiles clean, and is missing something nobody is told about.
            throw new UnsupportedItemTypeException(unknown, Supported);
        }

        // TEXT STYLING THE PANEL CANNOT EXPRESS IS A HARD ERROR, NOT A SILENT DROP.
        //
        // SimaticML's FontItem states exactly four things: Culture, FontFamily, FontSize, FontStyle.
        // There is no letter-spacing, no text-transform, no line-height. So CSS that relies on any
        // of them renders one way in the browser and another on the panel, with every check green -
        // the screen simply looks different from the thing that was reviewed.
        //
        // Found by diffing the complete font description out of each side's own document rather
        // than reasoning about it (the owner's method). Both were clean at the time - tracking
        // `normal`, transform `none` - which is exactly when to install the guard, while it costs
        // nothing and before something depends on it.
        //
        // text-transform is the sharper of the two: the BROWSER uppercases at render time while the
        // emitted payload keeps the authored case, so the panel would show mixed case where the
        // review saw capitals. That is a TEXT difference wearing a styling costume.
        var unrepresentable = new List<string>();
        foreach (var i in ir.Items.Where(x => x.Type is "Text" or "Button" && !string.IsNullOrWhiteSpace(x.Text)))
        {
            var label = string.IsNullOrWhiteSpace(i.Text) ? $"item {i.Index}" : $"\"{Trim(i.Text!)}\"";

            if (!string.IsNullOrWhiteSpace(i.LetterSpacing) && i.LetterSpacing != "normal" && i.LetterSpacing != "0px")
            {
                unrepresentable.Add($"{label}: letter-spacing '{i.LetterSpacing}' - the panel has no tracking control");
            }

            if (!string.IsNullOrWhiteSpace(i.TextTransform) && i.TextTransform != "none")
            {
                unrepresentable.Add($"{label}: text-transform '{i.TextTransform}' - the browser applies this at "
                                  + "render time, the panel would show the authored case instead. Write the text "
                                  + "in the case you want.");
            }

            if (!string.IsNullOrWhiteSpace(i.FontStyleCss) && i.FontStyleCss != "normal")
            {
                unrepresentable.Add($"{label}: font-style '{i.FontStyleCss}' - FontStyle carries Regular or Bold only");
            }
        }

        if (unrepresentable.Count > 0)
        {
            throw new UnrepresentableStylingException(unrepresentable);
        }

        // UNTYPED elements are a HARD ERROR, not a skip.
        //
        // The design rule is explicit mapping: an element carrying neither data-hmi nor
        // data-hmi-ignore has not been decided about, and deciding FOR it is exactly the silent
        // guess ADR-0010 forbids. Skipping them silently was a real defect: handed a plain HTML page
        // with no annotations at all, the emitter produced an EMPTY document and the failure
        // surfaced two layers later as "contains no screen items", which is a symptom rather than a
        // diagnosis. Naming them here turns a confusing empty result into an actionable list.
        var untyped = ir.Items
            .Where(i => i.Type is null && !i.Ignored && !i.Geometryless)
            .ToList();

        if (untyped.Count > 0)
        {
            throw new UntypedElementException(untyped);
        }

        var handOff = new List<string>();
        var id = 0;
        string NextId() => (++id).ToString("X", CultureInfo.InvariantCulture);

        // Utf8StringWriter, not StringBuilder: XmlWriter takes its declared encoding from the
        // WRITER, so a StringBuilder always yields <?xml encoding="utf-16"?> no matter what the
        // settings say. A real TIA export declares utf-8, and the declaration is part of the
        // contract - this is the kind of detail that fails at import with a message about something
        // else entirely.
        var itemCount = 0;
        var sb = new Utf8StringWriter();
        var settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", Encoding = Encoding.UTF8 };
        using (var w = XmlWriter.Create(sb, settings))
        {
            w.WriteStartDocument();
            w.WriteStartElement("Document");

            w.WriteStartElement("Engineering");
            w.WriteAttributeString("version", "V20");
            w.WriteEndElement();

            w.WriteStartElement("Hmi.Screen.Screen");
            w.WriteAttributeString("ID", "0");

            w.WriteStartElement("AttributeList");
            Attr(w, "ActiveLayer", "0");
            Attr(w, "BackColor", HouseGrey);
            Attr(w, "GridColor", "0, 0, 0");
            Attr(w, "Height", ir.CanvasHeight.ToString(CultureInfo.InvariantCulture));
            Attr(w, "Name", screenName);
            Attr(w, "Number", screenNumber.ToString(CultureInfo.InvariantCulture));
            Attr(w, "Visible", "true");
            Attr(w, "Width", ir.CanvasWidth.ToString(CultureInfo.InvariantCulture));
            w.WriteEndElement();

            w.WriteStartElement("ObjectList");

            // HelpText is present on EVERY screen in a real export, including a completely empty one,
            // and is emitted first to match that order. Suspected mandatory: omitting it did not
            // produce a rejection, it produced a PORTAL CRASH - the import killed the process twice
            // in a row, which is a far worse failure mode than an error message and is why this is
            // emitted unconditionally rather than only when there is help text to carry.
            WriteText(w, NextId, string.Empty, "HelpText");

            // The layer is not decoration: every screen item hangs off it, and a screen with content
            // but no layer is not a shape TIA produces.
            w.WriteStartElement("Hmi.Screen.ScreenLayer");
            w.WriteAttributeString("ID", NextId());
            w.WriteAttributeString("CompositionName", "Layers");
            w.WriteStartElement("AttributeList");
            Attr(w, "Index", "0");
            w.WriteElementString("Name", string.Empty);
            Attr(w, "VisibleES", "true");
            w.WriteEndElement();
            w.WriteStartElement("ObjectList");

            var emitted = 0;
            foreach (var item in ir.Items.Where(i => i.Type is not null && !i.Geometryless))
            {
                switch (item.Type)
                {
                    case "Rectangle":
                        WriteRectangle(w, NextId(), Name(item, "Rectangle", emitted), item, Colour(item.BackColor, HouseGrey), Colour(item.BorderColor, "0, 0, 0"));
                        emitted++;
                        break;

                    case "Text":
                        WriteTextField(w, NextId, Name(item, "Text", emitted), item);
                        emitted++;
                        break;

                    case "Button":
                        WriteButton(w, NextId, Name(item, "Button", emitted), item);
                        emitted++;
                        break;

                    case "Line":
                        WriteLine(w, NextId(), Name(item, "Line", emitted), item);
                        emitted++;
                        break;

                    case "Circle":
                        WriteCircle(w, NextId(), Name(item, "Circle", emitted), item);
                        emitted++;
                        break;

                    case "AlarmPlaceholder":
                    {
                        // The owner's decision (2026-08-17): a VISIBLE placeholder the engineer
                        // replaces by hand, because an alarm view cannot be authored at all on
                        // classic. Emitted as a bordered rectangle plus a label, so it is impossible
                        // to mistake for finished work.
                        var boxName = Name(item, "AlarmPlaceholder", emitted);
                        WriteRectangle(w, NextId(), boxName, item, "255, 255, 255", "176, 42, 30");
                        WriteTextField(w, NextId, boxName + "_Label", item with { Text = "ALARM VIEW GOES HERE - add manually" });
                        emitted += 2;
                        handOff.Add(
                            $"{boxName}: add an Alarm view at {item.Left:0},{item.Top:0} sized {item.Width:0}x{item.Height:0}, "
                            + "then DELETE the placeholder rectangle and its label.");
                        break;
                    }
                }
            }

            w.WriteEndElement(); // layer ObjectList
            w.WriteEndElement(); // ScreenLayer
            w.WriteEndElement(); // screen ObjectList
            w.WriteEndElement(); // Screen
            w.WriteEndElement(); // Document
            w.WriteEndDocument();

            itemCount = emitted;
        }

        // OUTSIDE the using, deliberately. XmlWriter buffers, and reading sb before the writer is
        // disposed yields a TRUNCATED document - which TIA rejects with "the following elements are
        // not closed", naming a line in the middle of the file. Measured: the first generated screen
        // failed exactly this way.
        return new EmitResult(sb.ToString(), itemCount, handOff);
    }

    private static string Trim(string t) => t.Length <= 24 ? t : t[..24] + "...";

    private const string HouseGrey = "182, 182, 182";

    // ENUM VALUES ARE HARVESTED FROM A REAL EXPORT, NEVER GUESSED.
    //
    // Every enum here was read out of a genuine TIA export rather than inferred from its name, after
    // three failed imports taught the lesson at roughly one Portal session each. The traps are not
    // guessable:
    //   * a Line's "no arrowhead" is "NoEnd", not "None"
    //   * LineEndShapeStyle's flat end is "Round", not "None"
    //   * VerticalAlignment is Top/Middle/Bottom - "Center" exists ONLY on the horizontal axis
    // An unknown enum value is a hard import failure that names the attribute and the line, which is
    // a good failure - but the export is a corpus of known-valid values, and reading it wholesale
    // beats being corrected one round trip at a time.
    //
    // DELIBERATE DEPARTURES from the observed values, both required by docs/17:
    //   * Button EdgeStyle: observed Style3D, emitted Solid       (H-204 forbids 3-D)
    //   * Button BackFillStyle: observed Transparent, emitted Solid (a command button must show its
    //     fill; Solid is observed on Rectangle and Circle, so the value is in the enum)

    private static string Name(IrItem item, string prefix, int n) =>
        string.IsNullOrWhiteSpace(item.Bind) ? $"{prefix}_{n + 1}" : $"{prefix}_{n + 1}";

    private static void Attr(XmlWriter w, string name, string value) => w.WriteElementString(name, value);

    /// <summary>CSS <c>rgb(r, g, b)</c> to SimaticML <c>"r, g, b"</c>. Falls back rather than guessing wildly.</summary>
    private static string Colour(string? css, string fallback)
    {
        var hsl = Hsl.Parse(css);
        if (hsl is null)
        {
            return fallback;
        }

        var m = System.Text.RegularExpressions.Regex.Match(css!, @"(\d+)\D+(\d+)\D+(\d+)");
        return m.Success ? $"{m.Groups[1].Value}, {m.Groups[2].Value}, {m.Groups[3].Value}" : fallback;
    }

    private static void Geometry(XmlWriter w, IrItem i)
    {
        Attr(w, "Height", ((int)Math.Round(i.Height)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "Left", ((int)Math.Round(i.Left)).ToString(CultureInfo.InvariantCulture));
    }

    private static void WriteRectangle(XmlWriter w, string id, string name, IrItem i, string back, string border)
    {
        w.WriteStartElement("Hmi.Screen.Rectangle");
        w.WriteAttributeString("ID", id);
        w.WriteAttributeString("CompositionName", "ScreenItems");
        w.WriteStartElement("AttributeList");
        Attr(w, "BackColor", back);
        Attr(w, "BackFillStyle", "Solid");
        Attr(w, "BorderColor", border);
        Attr(w, "BorderWidth", "1");
        Attr(w, "EdgeStyle", "Solid");
        Attr(w, "Flashing", "None");
        Geometry(w, i);
        Attr(w, "ObjectName", name);
        Attr(w, "RoundCornerHeight", "0");
        Attr(w, "RoundCornerWidth", "0");
        Attr(w, "TabIndex", "-1");
        Attr(w, "Top", ((int)Math.Round(i.Top)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "UseDesignColorSchema", "false");
        Attr(w, "Width", ((int)Math.Round(i.Width)).ToString(CultureInfo.InvariantCulture));
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void WriteCircle(XmlWriter w, string id, string name, IrItem i)
    {
        w.WriteStartElement("Hmi.Screen.Circle");
        w.WriteAttributeString("ID", id);
        w.WriteAttributeString("CompositionName", "ScreenItems");
        w.WriteStartElement("AttributeList");
        Attr(w, "BackColor", Colour(i.BackColor, HouseGrey));
        Attr(w, "BackFillStyle", "Solid");
        Attr(w, "BorderColor", Colour(i.BorderColor, "0, 0, 0"));
        Attr(w, "BorderWidth", "1");
        Attr(w, "EdgeStyle", "Solid");
        Attr(w, "Flashing", "None");
        Geometry(w, i);
        Attr(w, "ObjectName", name);
        Attr(w, "Radius", ((int)Math.Round(Math.Min(i.Width, i.Height) / 2)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "TabIndex", "-1");
        Attr(w, "Top", ((int)Math.Round(i.Top)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "UseDesignColorSchema", "false");
        Attr(w, "Width", ((int)Math.Round(i.Width)).ToString(CultureInfo.InvariantCulture));
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void WriteLine(XmlWriter w, string id, string name, IrItem i)
    {
        w.WriteStartElement("Hmi.Screen.Line");
        w.WriteAttributeString("ID", id);
        w.WriteAttributeString("CompositionName", "ScreenItems");
        w.WriteStartElement("AttributeList");
        Attr(w, "BackColor", HouseGrey);
        Attr(w, "Color", Colour(i.BackColor, "0, 0, 0"));
        // 🔴 START/END ARE ABSOLUTE SCREEN COORDINATES, NOT OFFSETS FROM Left/Top.
        // Emitting them relative (0,0 -> w,h) puts the endpoints outside the object's own bounding
        // box, and TIA does not reject that - it CRASHES THE PORTAL PROCESS on import, surfacing as
        // "Access to a disposed object of type 'Siemens.Engineering.Project'", which describes the
        // aftermath and not the cause. Isolated by bisection: Rectangle, TextField and Button all
        // imported cleanly and Line alone killed the process. Confirmed against the real export,
        // where every line satisfies StartLeft == Left and EndLeft == Left + Width.
        Attr(w, "EndLeft", ((int)Math.Round(i.Left + i.Width)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "EndStyle", "NoEnd");
        Attr(w, "EndTop", ((int)Math.Round(i.Top + i.Height)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "FillStyle", "Transparent");
        Attr(w, "Flashing", "None");
        Geometry(w, i);
        Attr(w, "LineEndShapeStyle", "Round");
        Attr(w, "LineWidth", "1");
        Attr(w, "ObjectName", name);
        Attr(w, "StartLeft", ((int)Math.Round(i.Left)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "StartStyle", "NoEnd");
        Attr(w, "StartTop", ((int)Math.Round(i.Top)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "Style", "Solid");
        Attr(w, "TabIndex", "-1");
        Attr(w, "Top", ((int)Math.Round(i.Top)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "UseDesignColorSchema", "false");
        Attr(w, "Width", ((int)Math.Round(i.Width)).ToString(CultureInfo.InvariantCulture));
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void WriteTextField(XmlWriter w, Func<string> nextId, string name, IrItem i)
    {
        w.WriteStartElement("Hmi.Screen.TextField");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "ScreenItems");
        w.WriteStartElement("AttributeList");
        Attr(w, "BackColor", Colour(i.BackColor, HouseGrey));
        Attr(w, "BackFillStyle", "Transparent");
        Attr(w, "BorderColor", "0, 0, 0");
        Attr(w, "BorderWidth", "0");
        Attr(w, "BottomMargin", "2");
        Attr(w, "CornerRadius", "0");
        Attr(w, "EdgeStyle", "Solid");
        Attr(w, "FitToLargest", "false");
        Attr(w, "Flashing", "None");
        Attr(w, "ForeColor", Colour(i.ForeColor, "0, 0, 0"));
        Geometry(w, i);
        Attr(w, "HorizontalAlignment", "Left");
        Attr(w, "LeftMargin", "2");
        Attr(w, "ObjectName", name);
        Attr(w, "RightMargin", "2");
        Attr(w, "TabIndex", "-1");
        Attr(w, "TextOrientation", "Horizontal");
        Attr(w, "Top", ((int)Math.Round(i.Top)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "TopMargin", "2");
        Attr(w, "UseDesignColorSchema", "false");
        Attr(w, "VerticalAlignment", "Top");
        Attr(w, "Width", ((int)Math.Round(i.Width)).ToString(CultureInfo.InvariantCulture));
        w.WriteEndElement();

        w.WriteStartElement("ObjectList");
        WriteFont(w, nextId, i);
        WriteText(w, nextId, i.Text ?? string.Empty, "Text");
        w.WriteEndElement();

        w.WriteEndElement();
    }

    private static void WriteButton(XmlWriter w, Func<string> nextId, string name, IrItem i)
    {
        w.WriteStartElement("Hmi.Screen.Button");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "ScreenItems");
        w.WriteStartElement("AttributeList");
        Attr(w, "BackColor", Colour(i.BackColor, "218, 218, 218"));
        Attr(w, "BackFillStyle", "Solid");
        Attr(w, "BorderColor", Colour(i.BorderColor, "105, 105, 105"));
        Attr(w, "BorderWidth", "1");
        // H-203/H-204: no radius, no 3-D. The panel default is Style3D, so this is a deliberate
        // override rather than an omission.
        Attr(w, "CornerRadius", "0");
        Attr(w, "CornerStyle", "Pointed");
        Attr(w, "EdgeStyle", "Solid");
        Attr(w, "Enabled", "true");
        Attr(w, "Flashing", "None");
        Attr(w, "ForeColor", Colour(i.ForeColor, "0, 0, 0"));
        Geometry(w, i);
        Attr(w, "HorizontalAlignment", "Center");
        Attr(w, "Mode", "Text");
        Attr(w, "ObjectName", name);
        Attr(w, "TabIndex", "-1");
        Attr(w, "TextOrientation", "Horizontal");
        Attr(w, "Top", ((int)Math.Round(i.Top)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "UseDesignColorSchema", "false");
        // "Middle", NOT "Center" - the vertical enum is Top/Middle/Bottom and does not contain
        // Center, even though the HORIZONTAL enum does. TIA refused the third generated screen on
        // exactly this, naming the attribute and the line.
        Attr(w, "VerticalAlignment", "Middle");
        Attr(w, "Width", ((int)Math.Round(i.Width)).ToString(CultureInfo.InvariantCulture));
        w.WriteEndElement();

        w.WriteStartElement("ObjectList");
        WriteFont(w, nextId, i);
        WriteText(w, nextId, string.Empty, "HelpText");
        WriteText(w, nextId, i.Text ?? string.Empty, "TextOff");
        WriteText(w, nextId, i.Text ?? string.Empty, "TextOn");
        w.WriteEndElement();

        w.WriteEndElement();
    }

    /// <summary>
    /// 🔴 FONT SIZE IS CARRIED FROM THE CSS, NOT HARD-CODED.
    ///
    /// This used to emit a fixed 12 for every text object, so every screen reached the panel with
    /// smaller type than it was authored with - reported by the owner comparing the two side by
    /// side, and by the agent that built the screens, independently.
    ///
    /// SimaticML's FontSize is in PIXELS, established from the corpus rather than assumed: a real
    /// TextField is Height 23 with FontSize 17, a ratio of 0.74, which is the signature of a
    /// pixel-sized font in a box sized to fit it. (Buttons in the same corpus sit at 0.15-0.38
    /// because a touch target is sized for the finger, not the text, so they say nothing about
    /// units.) So a CSS px value maps straight through with no conversion.
    ///
    /// The FAMILY is deliberately NOT carried. A panel has a small installed font set, and emitting
    /// whatever the browser resolved risks naming a face the panel does not have - which would fail
    /// somewhere far from here. Tahoma is what the corpus uses.
    /// </summary>
    private static void WriteFont(XmlWriter w, Func<string> nextId, IrItem item)
    {
        // 15 matches the smaller of the two sizes the real corpus uses; it is a floor for
        // legibility on a 7" panel, not an arbitrary default.
        var size = item.FontSizePx >= 1 ? (int)Math.Round(item.FontSizePx) : 15;
        var style = item.FontWeight >= 600 ? "Bold" : "Regular";

        w.WriteStartElement("Hmi.Globalization.MultiLingualFont");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "Font");
        w.WriteStartElement("ObjectList");
        w.WriteStartElement("Hmi.Globalization.FontItem");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "Items");
        w.WriteStartElement("AttributeList");
        Attr(w, "Culture", "en-US");
        Attr(w, "FontFamily", "Tahoma");
        Attr(w, "FontSize", size.ToString(CultureInfo.InvariantCulture));
        Attr(w, "FontStyle", style);
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
    }

    // Text is HTML-wrapped inside the element, exactly as TIA writes it. XmlWriter escapes the
    // angle brackets, which is the encoding a real export uses.
    // The composition name is TYPE-SPECIFIC and getting it wrong is a hard import failure, not a
    // cosmetic one: a TextField hangs its text off "Text", while a Button has NO "Text" composition
    // at all - it carries "TextOff" and "TextOn" (a button is a two-state object even when used as a
    // momentary command). TIA rejected the first generated screen with
    // "The 'Text' composition ... is not supported", naming the line, which is how this was found.
    private static void WriteText(XmlWriter w, Func<string> nextId, string text, string compositionName)
    {
        w.WriteStartElement("MultilingualText");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", compositionName);
        w.WriteStartElement("ObjectList");
        w.WriteStartElement("MultilingualTextItem");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "Items");
        w.WriteStartElement("AttributeList");
        Attr(w, "Culture", "en-US");
        Attr(w, "Text", $"<body><p>{text}</p></body>");
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
    }
}

/// <summary>
/// Elements carrying neither <c>data-hmi</c> nor <c>data-hmi-ignore</c>. Explicit mapping is the
/// rule: an unannotated element has not been decided about, and choosing a type for it would be a
/// silent guess about what appears on an operator screen.
/// </summary>
public sealed class UntypedElementException : Exception
{
    public UntypedElementException(IReadOnlyList<IrItem> untyped)
        : base(Build(untyped))
    {
    }

    private static string Build(IReadOnlyList<IrItem> untyped)
    {
        var sample = string.Join(Environment.NewLine, untyped.Take(8).Select(i =>
            $"    <{i.Tag.ToLowerInvariant()}> at {i.Left:0},{i.Top:0} {i.Width:0}x{i.Height:0}"
            + (string.IsNullOrWhiteSpace(i.Text) ? "" : $"  \"{Trim(i.Text!)}\"")));

        var more = untyped.Count > 8 ? $"{Environment.NewLine}    ... and {untyped.Count - 8} more" : string.Empty;

        return $"{untyped.Count} element(s) carry neither data-hmi nor data-hmi-ignore:"
             + Environment.NewLine + sample + more + Environment.NewLine
             + "Element mapping is EXPLICIT and never inferred - an unannotated element has not been "
             + "decided about, and guessing a type for it is a silent guess about what an operator sees. "
             + "Tag each with data-hmi=\"Rectangle|Text|Button|Line|Circle|AlarmPlaceholder\", or with "
             + "data-hmi-ignore if it is a layout wrapper that should not become a screen object.";
    }

    private static string Trim(string t) => t.Length <= 24 ? t : t[..24] + "...";
}

/// <summary>
/// CSS text styling with no SimaticML equivalent. Refused rather than dropped: the panel would
/// render differently from the browser the screen was reviewed in, with every gate green.
/// </summary>
public sealed class UnrepresentableStylingException : Exception
{
    public UnrepresentableStylingException(IReadOnlyList<string> problems)
        : base($"{problems.Count} text style(s) the panel cannot express:" + Environment.NewLine
             + string.Join(Environment.NewLine, problems.Select(x => "    " + x)) + Environment.NewLine
             + "A classic FontItem states only Culture, FontFamily, FontSize and FontStyle. Emitting "
             + "anyway would produce a screen that looks right in review and different on the panel.")
    {
    }
}

/// <summary>A StringWriter that reports UTF-8, so XmlWriter declares utf-8 rather than utf-16.</summary>
internal sealed class Utf8StringWriter : System.IO.StringWriter
{
    public override Encoding Encoding => Encoding.UTF8;
}

/// <summary>
/// An item type the emitter cannot faithfully produce. A hard error rather than a skip: a silently
/// dropped item yields a screen that imports clean, compiles clean, and is missing something nobody
/// was told about (ADR-0010 - an unsupported construct is a scope item, not a resting place).
/// </summary>
public sealed class UnsupportedItemTypeException : Exception
{
    public UnsupportedItemTypeException(IReadOnlyCollection<string> unknown, IReadOnlyCollection<string> supported)
        : base($"Cannot emit item type(s): {string.Join(", ", unknown)}. "
             + $"Supported: {string.Join(", ", supported.OrderBy(x => x, StringComparer.Ordinal))}. "
             + "This is a hard error, not a skip - emitting the screen without them would produce a "
             + "document that imports and compiles clean while missing content nobody was told about. "
             + "An alarm view specifically CANNOT be authored on classic HMI at all: use "
             + "data-hmi=\"AlarmPlaceholder\" to emit a visible placeholder plus a hand-off item.")
    {
    }
}
