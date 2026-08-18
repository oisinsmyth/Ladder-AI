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
        "Rectangle", "Text", "Button", "Line", "Circle", "AlarmPlaceholder", "IOField", "SymbolicIOField",
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

        // AN IOField WITHOUT A TAG IS REFUSED, NOT WRITTEN UNBOUND.
        //
        // An unbound IOField imports clean, compiles clean, and renders 0 or #### on the panel -
        // indistinguishable from a working field whose value happens to be zero. That is this
        // project's standing failure mode (a green that examined nothing) in its most operational
        // form: the number an operator reads off the screen is not connected to the plant.
        //
        // The same reasoning is why the check lives HERE and not in the checker: emit is the last
        // point at which the document does not yet exist. Refusing costs one line of output;
        // discovering it at commissioning costs a site visit.
        var unbound = ir.Items
            .Where(i => i.Type == "IOField" && string.IsNullOrWhiteSpace(i.Bind))
            // A SymbolicIOField needs BOTH halves. With no tag it shows nothing; with no text list it
            // shows the NUMBER - which is exactly the unreadable state this type exists to remove, and
            // it would look like a working field to everyone downstream.
            .Concat(ir.Items.Where(i => i.Type == "SymbolicIOField"
                                        && (string.IsNullOrWhiteSpace(i.Bind)
                                            || string.IsNullOrWhiteSpace(i.TextList))))
            .ToList();

        if (unbound.Count > 0)
        {
            throw new UnboundFieldException(unbound);
        }

        // A DUPLICATE ObjectName IS REFUSED HERE RATHER THAN AT TIA.
        //
        // Now that an author-supplied `id` becomes the ObjectName, two elements sharing an id produce
        // two objects sharing a name. TIA rejects that at IMPORT - which costs a Portal session to
        // discover, and the message names the document rather than the element. Cheaper to say it now.
        var dupeIds = ir.Items
            .Where(i => i.Type is not null && !i.Geometryless && !string.IsNullOrWhiteSpace(i.ElementId))
            .GroupBy(i => i.ElementId!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToList();

        if (dupeIds.Count > 0)
        {
            throw new UnrepresentableStylingException(dupeIds
                .Select(g => $"id=\"{g.Key}\" is on {g.Count()} elements - an id becomes the object's "
                           + "name on the panel and TIA refuses duplicates at import")
                .ToList());
        }

        // TRUNCATED TEXT IS REFUSED, NOT SHORTENED. See ScreenIr.TextTruncated - the flattener used
        // to cut at 80 characters silently, and a caption that renders while missing its second half
        // is the worst available outcome.
        var truncated = ir.Items.Where(i => i.TextTruncated).ToList();
        if (truncated.Count > 0)
        {
            throw new UnrepresentableStylingException(truncated
                .Select(i => $"\"{Trim(i.Text ?? string.Empty)}\": text is too long to carry intact - "
                           + "split it across elements rather than letting it be cut")
                .ToList());
        }

        // A NAVIGATION BUTTON THAT NAMES NO SCREEN is the same class of defect: it looks like a
        // button, it presses, and nothing happens. Only flagged when the author declared an intent
        // to navigate (an empty data-hmi-goto), never for an ordinary command button.
        var emptyGoto = ir.Items
            .Where(i => i.Type == "Button" && i.GoTo is not null && string.IsNullOrWhiteSpace(i.GoTo))
            .ToList();

        if (emptyGoto.Count > 0)
        {
            throw new UnboundFieldException(emptyGoto);
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
                    {
                        var btnName = Name(item, "Button", emitted);
                        WriteButton(w, NextId, btnName, item);
                        emitted++;
                        // EVERY BUTTON GETS A HAND-OFF LINE, not only the navigating ones.
                        //
                        // This used to fire only when data-hmi-goto was set, which meant a COMMAND
                        // button - START, ABORT, ACKNOWLEDGE - was emitted completely inert with
                        // NOTHING ANYWHERE NAMING IT. Found on a real job: nine command buttons
                        // across four screens, every one of them dead, and no generated file
                        // mentioned any of them.
                        //
                        // That is the silent omission this emitter exists to refuse. No button can
                        // carry an action on classic (no event can be created at all), so a button
                        // WITHOUT a declared target is not less incomplete than one with - it is
                        // MORE, because nobody even knows what it was meant to do.
                        handOff.Add(string.IsNullOrWhiteSpace(item.GoTo)
                            ? $"{btnName} (\"{item.Text}\"): INERT - this button has no action and no "
                              + "event can be generated for it. Define what it does and build the "
                              + "function list by hand, including any enable condition."
                            : $"{btnName} (\"{item.Text}\"): add an ActivateScreen event on KeyUp targeting "
                              + $"\"{item.GoTo}\". THE PANEL CANNOT BE NAVIGATED UNTIL THIS IS DONE BY HAND.");

                        break;
                    }

                    case "Line":
                        WriteLine(w, NextId(), Name(item, "Line", emitted), item);
                        emitted++;
                        break;

                    case "Circle":
                        WriteCircle(w, NextId(), Name(item, "Circle", emitted), item);
                        emitted++;
                        break;

                    case "IOField":
                        WriteIOField(w, NextId, Name(item, "IOField", emitted), item);
                        emitted++;
                        break;

                    case "SymbolicIOField":
                        WriteSymbolicIOField(w, NextId, Name(item, "SymbolicIOField", emitted), item);
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

    // 🔴 AN OBJECT NAME IS AN IDENTITY, AND A POSITIONAL ONE IS NOT STABLE.
    //
    // Names were purely positional - `Button_53` meant "the 53rd item emitted". So inserting ONE
    // element near the top of the HTML renumbered every object after it, and a `compare` between two
    // versions of the same screen then reported a wall of differences that were pure noise.
    //
    // Measured 2026-08-17: comparing a screen against a re-emitted version of itself produced 32
    // CHANGED and 2 DROPPED lines, of which the real count was ZERO - every one was a name shifting
    // by one. I read that as "TIA renumbers objects on a hand edit" and reported it as a finding. It
    // was our own emitter, and a lane comparing against the correct baseline showed 1 changed over
    // 1758 fields. A misleading identity produced a false conclusion about the PLATFORM.
    //
    // So: an element carrying an `id` gets that as its ObjectName, which survives anything happening
    // above it. Without one the positional name remains - it is fine for decoration, and requiring an
    // id on every rectangle would be noise. **Put an id on anything you intend to diff.**
    private static string Name(IrItem item, string prefix, int n) =>
        string.IsNullOrWhiteSpace(item.ElementId) ? $"{prefix}_{n + 1}" : item.ElementId!;

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
        var radius = (int)Math.Round(Math.Min(i.Width, i.Height) / 2);
        var side = radius * 2;

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
        // Radius and the bounding box must agree or the object is incoherent, so the box is
        // SQUARED to the radius rather than left as authored. An ellipse is not a thing this type
        // can express, and emitting one crashes Portal rather than being refused.
        Attr(w, "Radius", radius.ToString(CultureInfo.InvariantCulture));
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
        // A bounding box has TWO diagonals. "down" runs top-left to bottom-right; "up" runs
        // bottom-left to top-right. Without the choice, opposed pairs - the two sides of a cone,
        // the two slopes of a roof - simply cannot be drawn.
        var up = string.Equals(i.LineDirection, "up", StringComparison.OrdinalIgnoreCase);
        var startY = up ? i.Top + i.Height : i.Top;
        var endY = up ? i.Top : i.Top + i.Height;

        Attr(w, "EndLeft", ((int)Math.Round(i.Left + i.Width)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "EndStyle", "NoEnd");
        Attr(w, "EndTop", ((int)Math.Round(endY)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "FillStyle", "Transparent");
        Attr(w, "Flashing", "None");
        Geometry(w, i);
        Attr(w, "LineEndShapeStyle", "Round");
        Attr(w, "LineWidth", "1");
        Attr(w, "ObjectName", name);
        Attr(w, "StartLeft", ((int)Math.Round(i.Left)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "StartStyle", "NoEnd");
        Attr(w, "StartTop", ((int)Math.Round(startY)).ToString(CultureInfo.InvariantCulture));
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

        // 🔴 THE EVENT IS DELIBERATELY NOT WRITTEN. MEASURED 2026-08-17, AGAINST A REAL PROJECT:
        //
        //     'Create' is not supported by type 'Siemens.Engineering.Hmi.Event.EventComposition'.
        //
        // Openness will NOT create an event on a classic screen through an import, and the refusal is
        // structural rather than a validation failure. Note the asymmetry, which is the trap: TIA
        // EXPORTS events perfectly well - this emitter's event structure was harvested from a real
        // export carrying four of them - so a round trip reads as though it should work.
        //
        // This is the ALARM VIEW's shape a second time: representable in the document, not creatable
        // through the API. So it gets the alarm view's treatment - the button is emitted, and the
        // event becomes a HAND-OFF ITEM the engineer completes in TIA. Emitting it anyway would fail
        // the whole import and take the working half of the screen down with it.
        //
        // WriteNavigationEvent is KEPT, not deleted: it is the correct structure, it is proven
        // against the corpus, and it is what a future create-capable route would emit. Deleting it
        // would discard harvested knowledge that cost a Portal session to obtain.

        w.WriteEndElement();

        w.WriteEndElement();
    }

    /// <summary>
    /// A button that changes screen: <c>ActivateScreen</c> on <c>KeyUp</c>.
    ///
    /// 🔴 THE EVENT IS <c>KeyUp</c>, NOT <c>Click</c> - harvested from a real Classic export where all
    /// four navigation buttons use it. Guessing <c>Click</c> here would produce a document that
    /// imports and compiles cleanly and does nothing when pressed, which is the worst available
    /// failure: every gate green, and the screen dead under the operator's finger.
    ///
    /// The parameter is named <c>Screen name</c> - with the space, and with that capitalisation - and
    /// carries the target as a <c>Value</c> LINK rather than as an attribute value.
    /// </summary>
    private static void WriteNavigationEvent(XmlWriter w, Func<string> nextId, string targetScreen)
    {
        w.WriteStartElement("Hmi.Event.Event");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "Events");
        w.WriteStartElement("AttributeList");
        Attr(w, "Name", "KeyUp");
        w.WriteEndElement();

        w.WriteStartElement("ObjectList");
        w.WriteStartElement("Hmi.Event.FunctionListEventHandler");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "EventHandler");
        w.WriteStartElement("ObjectList");

        w.WriteStartElement("Hmi.Event.FunctionListEntry");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "FunctionListEntries");
        w.WriteStartElement("AttributeList");
        Attr(w, "Name", "ActivateScreen");
        Attr(w, "Type", "SystemFunction");
        w.WriteEndElement();

        w.WriteStartElement("ObjectList");
        w.WriteStartElement("Hmi.Event.FunctionListEntryParameter");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "Parameters");
        w.WriteStartElement("AttributeList");
        Attr(w, "Name", "Screen name");
        w.WriteEndElement();
        w.WriteStartElement("LinkList");
        w.WriteStartElement("Value");
        w.WriteAttributeString("TargetID", "@OpenLink");
        Attr(w, "Name", targetScreen);
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement(); // Parameter
        w.WriteEndElement(); // ObjectList
        w.WriteEndElement(); // FunctionListEntry

        w.WriteEndElement(); // ObjectList
        w.WriteEndElement(); // FunctionListEventHandler
        w.WriteEndElement(); // ObjectList
        w.WriteEndElement(); // Event
    }

    /// <summary>
    /// The field that shows a coded value as a WORD instead of a number.
    ///
    /// 🔴 This is the type whose absence made the first real screen unreadable: nine of nineteen
    /// fields on it were bare integers standing in for words - the state, the hold cause, the moisture
    /// stage - because the emitter had nothing that could resolve them. The owner's verdict on seeing
    /// it was that a non-technical operator could not read the screen, and they were right.
    ///
    /// STRUCTURE HARVESTED FROM TWO REAL SPECIMENS, not documentation. There are two distinct modes
    /// and only one of them is useful here:
    ///   * BIT mode      - BitNumber + OnValue with TextOff/TextOn. Two states, no list. This is what
    ///                     TIA creates by default when you drop one on a screen.
    ///   * TEXT-LIST mode - a LinkList naming a TextList, plus a tag on the ProcessValue property.
    ///                     Many values to many words, which is what a state number needs.
    /// This emits TEXT-LIST mode; the bit variant is reachable with a two-entry list and is not worth
    /// a second code path.
    ///
    /// ⚠️ The text list itself is a PROJECT object the engineer creates - this only NAMES one. A
    /// screen naming a list that does not exist still imports; the hand-off carries the list and its
    /// entries so the naming is not left implicit.
    /// </summary>
    private static void WriteSymbolicIOField(XmlWriter w, Func<string> nextId, string name, IrItem i)
    {
        var mode = string.IsNullOrWhiteSpace(i.Mode) ? "Output" : i.Mode!;

        w.WriteStartElement("Hmi.Screen.SymbolicIOField");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "ScreenItems");
        w.WriteStartElement("AttributeList");
        Attr(w, "AboveUpperLimitColor", "237, 88, 97");
        Attr(w, "BackColor", Colour(i.BackColor, "255, 255, 255"));
        Attr(w, "BackFillStyle", "Solid");
        Attr(w, "BelowLowerLimitColor", "241, 161, 44");
        Attr(w, "BitNumber", "0");
        Attr(w, "BorderColor", Colour(i.BorderColor, "105, 105, 105"));
        Attr(w, "BorderWidth", "1");
        Attr(w, "BottomMargin", "2");
        // H-203/H-204: the corpus uses CornerRadius 3 and EdgeStyle Double, and the owner's own
        // hand-placed specimen came out Style3D - that is simply TIA's default, and it is what H-204
        // exists to catch. Flat and square here, as on Button and IOField.
        Attr(w, "CornerRadius", "0");
        Attr(w, "CountVisibleItems", "3");
        Attr(w, "EdgeStyle", "Solid");
        Attr(w, "Enabled", mode == "Output" ? "false" : "true");
        Attr(w, "EvenRowBackColor", "230, 230, 232");
        Attr(w, "FitToLargest", "false");
        Attr(w, "Flashing", "None");
        Attr(w, "FlashingOnLimitViolation", "false");
        Attr(w, "ForeColor", Colour(i.ForeColor, "0, 0, 0"));
        Geometry(w, i);
        // Left-aligned, unlike an IOField: this holds a WORD, and words read from the left. Numbers
        // line up on the right so their decimal points align; text has no such point.
        Attr(w, "HorizontalAlignment", "Left");
        Attr(w, "LeftMargin", "3");
        Attr(w, "Mode", mode);
        Attr(w, "ObjectName", name);
        Attr(w, "OnValue", "1");
        Attr(w, "RightMargin", "2");
        Attr(w, "SelectBackColor", "0, 0, 0");
        Attr(w, "SelectForeColor", "255, 255, 255");
        // A drop-down is an INPUT affordance. On a display field it invites a press that does
        // nothing, so both are off unless the field is genuinely writable.
        Attr(w, "ShowDropDownButton", mode == "Output" ? "false" : "true");
        Attr(w, "ShowDropDownList", mode == "Output" ? "false" : "true");
        Attr(w, "TabIndex", "-1");
        Attr(w, "TextOrientation", "Horizontal");
        Attr(w, "Top", ((int)Math.Round(i.Top)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "TopMargin", "2");
        Attr(w, "UseDesignColorSchema", "false");
        Attr(w, "VerticalAlignment", "Middle");
        Attr(w, "Width", ((int)Math.Round(i.Width)).ToString(CultureInfo.InvariantCulture));
        w.WriteEndElement();

        // The TEXT LIST link sits on the ITEM, beside its AttributeList - not inside a Property, which
        // is where the TAG goes. Two different link levels on one object, and swapping them yields a
        // document that imports into nothing.
        w.WriteStartElement("LinkList");
        w.WriteStartElement("TextList");
        w.WriteAttributeString("TargetID", "@OpenLink");
        Attr(w, "Name", i.TextList!);
        w.WriteEndElement();
        w.WriteEndElement();

        w.WriteStartElement("ObjectList");
        WriteFont(w, nextId, i);
        WriteText(w, nextId, string.Empty, "HelpText");
        WriteTagBinding(w, nextId, "ProcessValue", i.Bind!);
        w.WriteEndElement();

        w.WriteEndElement();
    }

    /// <summary>
    /// Connects one of an item's PROPERTIES to a PLC tag.
    ///
    /// Harvested whole from a real Classic export. The nesting is
    /// <c>Hmi.Screen.Property(Name=&lt;property&gt;) -> Hmi.Dynamic.TagConnectionDynamic -> LinkList -> Tag</c>,
    /// and it is GENERAL: the same shape binds <c>ProcessValue</c> on an IOField, and would bind
    /// <c>Visible</c> on any item - which is the mechanism a popup layer's visibility condition needs.
    ///
    /// ⚠️ The tag NAME here is an HMI tag name, not a PLC symbol path. The HMI tag is what carries the
    /// connection to the PLC; binding an item straight to <c>"DB".Member</c> is not what the corpus
    /// does. So a screen is only as bound as the HMI tag table behind it - which is why nothing here
    /// invents one.
    /// </summary>
    private static void WriteTagBinding(XmlWriter w, Func<string> nextId, string property, string tag)
    {
        w.WriteStartElement("Hmi.Screen.Property");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "Properties");
        w.WriteStartElement("AttributeList");
        Attr(w, "Name", property);
        w.WriteEndElement();

        w.WriteStartElement("ObjectList");
        w.WriteStartElement("Hmi.Dynamic.TagConnectionDynamic");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "Dynamic");
        w.WriteStartElement("AttributeList");
        Attr(w, "Indirect", "false");
        w.WriteEndElement();
        w.WriteStartElement("LinkList");
        w.WriteStartElement("Tag");
        w.WriteAttributeString("TargetID", "@OpenLink");
        Attr(w, "Name", tag);
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement(); // TagConnectionDynamic
        w.WriteEndElement(); // ObjectList
        w.WriteEndElement(); // Property
    }

    /// <summary>
    /// The field that puts a live plant value on a screen - and the reason the emitter's previous
    /// vocabulary could not build a working HMI at all.
    ///
    /// There are 23 of these on the five-screen reference corpus, against zero in anything this tool
    /// had produced before 2026-08-17.
    ///
    /// MODE DEFAULTS TO <c>Output</c>. An <c>Input</c> or <c>InOutput</c> field writes to the PLC, so
    /// a display field that silently became writable would hand an operator a control nobody decided
    /// to give them. Writability is declared (<c>data-hmi-mode</c>), never inherited.
    ///
    /// An IOField with no <c>data-hmi-bind</c> is REFUSED at emit rather than written unbound: an
    /// unbound field renders as <c>0</c> or <c>####</c> on the panel and looks exactly like a working
    /// one that happens to read zero. That is the failure this project keeps naming - a green that
    /// examined nothing - so it fails closed.
    /// </summary>
    private static void WriteIOField(XmlWriter w, Func<string> nextId, string name, IrItem i)
    {
        var mode = string.IsNullOrWhiteSpace(i.Mode) ? "Output" : i.Mode!;
        var format = string.IsNullOrWhiteSpace(i.Format) ? "9999" : i.Format!;

        // 🔴 FieldLength IS THE WHOLE PATTERN'S LENGTH, NOT ITS DIGIT COUNT - AND GETTING IT WRONG
        // CRASHES PORTAL.
        //
        // Measured: the corpus has FormatPattern "99999.999" with FieldLength 9. That is the string's
        // LENGTH (9), not the number of digits in it (8). Emitting the digit count made the two
        // attributes disagree for any pattern containing a decimal point, and the import killed the
        // Portal process with "Access to a disposed object of type 'Siemens.Engineering.Project'" -
        // the aftermath, never the cause.
        //
        // Isolated by bisection against a control that PASSED: a probe field with pattern "9999" -
        // where length and digit count are both 4, so the bug could not express itself - imported
        // clean, while the same field with "9999.9" did not. A control that cannot fail is worth
        // exactly nothing, and this one nearly was one by accident.
        //
        // Third member of the same family: Circle's Radius vs its bounding box, Line's endpoints vs
        // its own box, and now this. TWO ATTRIBUTES THAT MUST AGREE, where TIA validates neither and
        // dies instead of refusing.
        var fieldLength = format.Length.ToString(CultureInfo.InvariantCulture);

        w.WriteStartElement("Hmi.Screen.IOField");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "ScreenItems");
        w.WriteStartElement("AttributeList");
        // The two limit colours are the panel's own out-of-range indication. Kept at the corpus
        // values: they are an ALARM channel, and H-105 rations exactly this.
        Attr(w, "AboveUpperLimitColor", "237, 88, 97");
        Attr(w, "BackColor", Colour(i.BackColor, "255, 255, 255"));
        Attr(w, "BackFillStyle", "Solid");
        Attr(w, "BelowLowerLimitColor", "241, 161, 44");
        Attr(w, "BorderColor", Colour(i.BorderColor, "105, 105, 105"));
        Attr(w, "BorderWidth", "1");
        Attr(w, "BottomMargin", "2");
        // H-203: no radius. The corpus uses 3; the house rule wins, as it does on Button.
        Attr(w, "CornerRadius", "0");
        Attr(w, "DataFormat", "Decimal");
        // H-204: the corpus uses Double (a 3-D bevel); Solid is the flat equivalent.
        Attr(w, "EdgeStyle", "Solid");
        Attr(w, "Enabled", mode == "Output" ? "false" : "true");
        Attr(w, "FieldLength", fieldLength);
        Attr(w, "FitToLargest", "false");
        Attr(w, "Flashing", "None");
        Attr(w, "ForeColor", Colour(i.ForeColor, "0, 0, 0"));
        Attr(w, "FormatPattern", format);
        Geometry(w, i);
        Attr(w, "HiddenInput", "false");
        // Right-aligned: H-302 wants process values to line up on the decimal point, and a
        // left-aligned number in a fixed-width field does not.
        Attr(w, "HorizontalAlignment", "Right");
        Attr(w, "LeftMargin", "3");
        Attr(w, "Mode", mode);
        Attr(w, "ObjectName", name);
        Attr(w, "RightMargin", "2");
        Attr(w, "ShiftDecimalPoint", "0");
        Attr(w, "ShowLeadingZeros", "false");
        Attr(w, "TabIndex", "-1");
        Attr(w, "TextOrientation", "Horizontal");
        Attr(w, "Top", ((int)Math.Round(i.Top)).ToString(CultureInfo.InvariantCulture));
        Attr(w, "TopMargin", "2");
        Attr(w, "Unit", i.Unit ?? string.Empty);
        Attr(w, "UseDesignColorSchema", "false");
        Attr(w, "UseTwoHandOperation", "false");
        Attr(w, "VerticalAlignment", "Middle");
        Attr(w, "Width", ((int)Math.Round(i.Width)).ToString(CultureInfo.InvariantCulture));
        w.WriteEndElement();

        w.WriteStartElement("ObjectList");
        WriteFont(w, nextId, i);
        WriteText(w, nextId, string.Empty, "HelpText");
        WriteTagBinding(w, nextId, "ProcessValue", i.Bind!);
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
             + "Tag each with data-hmi=\"Rectangle|Text|Button|Line|Circle|IOField|AlarmPlaceholder\", or with "
             + "data-hmi-ignore if it is a layout wrapper that should not become a screen object.";
    }

    private static string Trim(string t) => t.Length <= 24 ? t : t[..24] + "...";
}

/// <summary>
/// An item that would reach the panel LOOKING connected and BEING disconnected.
///
/// Separate from <see cref="UntypedElementException"/> because the failure is the opposite shape: an
/// untyped element is a question the author has not answered, whereas an unbound IOField is an answer
/// that is silently wrong. It renders 0 or #### and reads as a working field showing zero.
/// </summary>
public sealed class UnboundFieldException : Exception
{
    public UnboundFieldException(IReadOnlyList<IrItem> items)
        : base(Build(items))
    {
    }

    private static string Build(IReadOnlyList<IrItem> items)
    {
        var sample = string.Join(Environment.NewLine, items.Take(8).Select(i =>
            $"    {i.Type} at {i.Left:0},{i.Top:0} {i.Width:0}x{i.Height:0}"
            + (string.IsNullOrWhiteSpace(i.Text) ? "" : $"  \"{i.Text}\"")));

        var more = items.Count > 8 ? $"{Environment.NewLine}    ... and {items.Count - 8} more" : string.Empty;

        return $"{items.Count} item(s) declare a connection and name nothing to connect to:"
             + Environment.NewLine + sample + more + Environment.NewLine
             + "An IOField needs data-hmi-bind=\"<hmi tag>\"; a navigation button needs a non-empty "
             + "data-hmi-goto=\"<screen name>\". These are REFUSED rather than emitted unbound, because "
             + "an unbound field imports clean, compiles clean, and displays a number that is not "
             + "coming from the plant - which nobody can tell apart from a working one by looking.";
    }
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
