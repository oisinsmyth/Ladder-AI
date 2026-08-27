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

        // 🔴 "Layer" IS A DECLARATION, NOT AN ITEM. It emits no screen object of its own - it names
        // a layer, gives it an index, and carries the visibility rule that PlanLayers copies onto
        // every member. It is listed here only so the unknown-type guard recognises it; EmitItem is
        // never called with one, and PlanLayers removes them from the member set.
        "Layer",
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

        // 🔴 A BUTTON THAT NAVIGATES TO ITS OWN SCREEN IS REFUSED. MEASURED 2026-08-18.
        //
        // Emitted, it imports clean and compiles clean, and TIA SILENTLY DISCARDS THE LINK - the
        // read-back carries an ActivateScreen with an EMPTY `Screen name` and the button does
        // nothing under the operator's finger. Every gate green, one dead control, and nothing
        // anywhere naming it. Found on the first real screen through this path: a header button
        // marking the CURRENT screen had been given that screen as its target.
        //
        // Refused rather than dropped-with-a-warning because there is no correct rendering of the
        // request: navigating to the screen you are already on is either a no-op or a mistake, and
        // the mistake is far likelier. A button that marks "you are here" is a Text, not a Button.
        var selfNav = ir.Items
            .Where(i => i.Type == "Button"
                        && !string.IsNullOrWhiteSpace(i.GoTo)
                        && string.Equals(i.GoTo!.Trim(), screenName.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(i => i.ElementId ?? i.Text ?? "(unnamed button)")
            .ToList();

        if (selfNav.Count > 0)
        {
            throw new SelfNavigationException(screenName, selfNav);
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

        // ------------------------------------------------------------------ data-hmi-set
        //
        // A STAGED OPERAND WRITE. Everything about this is narrow on purpose - see IrItem.SetTag.
        // The refusals below are what keep it from becoming a second, hand-rolled command path.
        var stagingProblems = new List<string>();
        foreach (var i in ir.Items.Where(x => !string.IsNullOrWhiteSpace(x.SetTag)))
        {
            var spec = i.SetTag!.Trim();
            var where = $"data-hmi-set=\"{spec}\"";

            if (i.Type != "Button")
            {
                stagingProblems.Add($"{where} on a {i.Type ?? "(untyped)"}: only a Button has a press "
                    + "to hang a write on.");
                continue;
            }

            // SEVERAL WRITES, SEMICOLON-SEPARATED, IN THE AUTHOR'S ORDER. Splitting here rather
            // than at the emitter keeps ONE parse: a segment that this loop refuses is a segment
            // WriteButtonEvents never sees.
            var writes = ParseStagedWrites(spec);
            if (writes.Count == 0)
            {
                stagingProblems.Add($"{where}: no write in it. Expected <Tag>=<value>, or several "
                    + "separated by ';'.");
                continue;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var segmentFault = false;

            foreach (var segment in writes)
            {
                var eq = segment.IndexOf('=');
                if (eq <= 0 || eq == segment.Length - 1)
                {
                    stagingProblems.Add($"{where}: segment \"{segment}\" is not <Tag>=<value>, where "
                        + "<value> is a number or @<other tag>.");
                    segmentFault = true;
                    continue;
                }

                var target = segment[..eq].Trim();
                var value = segment[(eq + 1)..].Trim();

                if (target.Length == 0 || value.Length == 0)
                {
                    stagingProblems.Add($"{where}: segment \"{segment}\" has an empty tag or value.");
                    segmentFault = true;
                    continue;
                }

                // TWO WRITES TO ONE TAG IN ONE PRESS is an ordering question with no good answer,
                // and the likeliest cause is a copy-paste in a generated seed list - which would
                // leave one of the intended targets unwritten while the list LOOKS complete.
                if (!seen.Add(target))
                {
                    stagingProblems.Add($"{where}: writes '{target}' more than once in a single "
                        + "press. Which write lands last is not a question an author should have to "
                        + "answer, and a repeated target usually means another one is missing.");
                    segmentFault = true;
                    continue;
                }

                // 🔴 THE ONE REFUSAL THAT IS NOT TIDINESS. _Seq is the handshake: writing it is
                // ISSUING a command, and issuing one from here would carry whatever code happens to
                // be standing - the exact wrong-order fault data-hmi-cmd was built so that nobody
                // could express. _Code is refused with it because a code staged here and bumped by a
                // later press is the same fault split across two screens, which is harder to see
                // rather than safer.
                if (target.EndsWith("_Seq", StringComparison.OrdinalIgnoreCase)
                    || target.EndsWith("_Code", StringComparison.OrdinalIgnoreCase))
                {
                    stagingProblems.Add($"{where}: refuses to write '{target}'. data-hmi-set STAGES "
                        + "AN OPERAND and can never issue a command. Bumping a sequence sends "
                        + "whatever code is standing, and staging a code for a later bump is the "
                        + "same fault spread over two presses. Use data-hmi-cmd with "
                        + "data-hmi-cmd-code, which cannot be ordered wrongly.");
                    segmentFault = true;
                    continue;
                }
            }

            if (segmentFault)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(i.Cmd))
            {
                stagingProblems.Add($"{where}: this button already declares data-hmi-cmd=\"{i.Cmd}\". "
                    + "A command carries its own operands (data-hmi-cmd-int1 etc.) in the proven order; "
                    + "two write paths on one press is an ordering question nobody should have to "
                    + "answer. Use one or the other.");
            }
        }

        if (stagingProblems.Count > 0)
        {
            throw new OperandStagingException(stagingProblems);
        }

        // ------------------------------------------------------------------ data-hmi-string
        var stringProblems = new List<string>();
        foreach (var i in ir.Items.Where(x => !string.IsNullOrWhiteSpace(x.StringLength)))
        {
            var raw = i.StringLength!.Trim();
            var where = $"data-hmi-string=\"{raw}\"";

            if (i.Type != "IOField")
            {
                stringProblems.Add($"{where} on a {i.Type ?? "(untyped)"}: only an IOField can show a "
                    + "string tag.");
                continue;
            }

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                || n < 1 || n > 255)
            {
                stringProblems.Add($"{where}: expected a character count between 1 and 255 - it is the "
                    + "PLC String's declared length, and it becomes both the format pattern and the "
                    + "FieldLength.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(i.Format))
            {
                stringProblems.Add($"{where}: this field also declares data-hmi-format=\"{i.Format}\". "
                    + "A string field and a numeric field carry DIFFERENT DataFormat values, so a field "
                    + "declaring both contradicts itself - and TIA does not reject a self-contradicting "
                    + "field, it crashes the Portal process. Declare one.");
            }
        }

        if (stringProblems.Count > 0)
        {
            throw new StringFieldException(stringProblems);
        }

        // ------------------------------------------------------------------ visibility animation
        //
        // AN ANIMATION WITH NO TRIGGER TAG IS REFUSED. It imports, and then it never fires - so the
        // object sits at whichever visibility TIA settles on and looks like an object nobody
        // animated. The failure family is the unbound IOField's: a control that reads as finished
        // and is not connected to anything.
        var animationProblems = ir.Items
            .Where(i => i.Visibility is not null && string.IsNullOrWhiteSpace(i.Visibility.Tag))
            .Select(i => $"{i.ElementId ?? i.Type ?? "(untyped)"}: a visibility animation naming no "
                       + "trigger tag. Without one the rule never evaluates and the object's "
                       + "visibility is whatever the panel defaults to.")
            .ToList();

        if (animationProblems.Count > 0)
        {
            throw new UnboundAnimationException(animationProblems);
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
            //
            // The BASE layer is index 0 with an empty name and holds everything that names no
            // layer, which is every screen authored before layers existed - so those emit exactly
            // as they did. Declared layers follow, in index order, each holding its own members.
            var layerPlans = PlanLayers(ir);
            var emitted = 0;

            w.WriteStartElement("Hmi.Screen.ScreenLayer");
            w.WriteAttributeString("ID", NextId());
            w.WriteAttributeString("CompositionName", "Layers");
            w.WriteStartElement("AttributeList");
            Attr(w, "Index", "0");
            w.WriteElementString("Name", string.Empty);
            Attr(w, "VisibleES", "true");
            w.WriteEndElement();
            w.WriteStartElement("ObjectList");

            foreach (var item in ir.Items.Where(i => i.Type is not null && i.Type != "Layer"
                                                     && string.IsNullOrWhiteSpace(i.Layer)
                                                     && !i.Geometryless))
            {
                EmitItem(item);
            }

            w.WriteEndElement(); // base layer ObjectList
            w.WriteEndElement(); // base ScreenLayer

            // Each declared layer, in index order. VisibleES is true so a person opening the
            // screen in TIA sees the dialog and can hide it while working behind it - that IS
            // the job a classic layer can do, and the only one.
            foreach (var plan in layerPlans)
            {
                w.WriteStartElement("Hmi.Screen.ScreenLayer");
                w.WriteAttributeString("ID", NextId());
                w.WriteAttributeString("CompositionName", "Layers");
                w.WriteStartElement("AttributeList");
                Attr(w, "Index", plan.Index.ToString(CultureInfo.InvariantCulture));
                Attr(w, "Name", plan.Name);
                Attr(w, "VisibleES", "true");
                w.WriteEndElement();
                w.WriteStartElement("ObjectList");

                foreach (var item in plan.Items.Where(i => !i.Geometryless))
                {
                    // The layer's rule, onto the member. See PlanLayers for why this is a copy
                    // rather than something the layer carries.
                    EmitItem(OnLayer(item, plan.Visibility));
                }

                w.WriteEndElement(); // layer ObjectList
                w.WriteEndElement(); // ScreenLayer
            }

            void EmitItem(IrItem item)
            {
                switch (item.Type)
                {
                    case "Rectangle":
                        WriteRectangle(w, NextId, Name(item, "Rectangle", emitted), item, Colour(item.BackColor, HouseGrey), Colour(item.BorderColor, "0, 0, 0"));
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
                        // ONLY AN INERT BUTTON IS A HAND-OFF NOW.
                        //
                        // Until 2026-08-18 EVERY button was one, because no event could be
                        // generated at all. That is no longer true: a button declaring a command or
                        // a navigation target gets a real, verified event and is finished. What
                        // survives - deliberately - is the rule that a button with NO declared
                        // action is still REPORTED rather than silently emitted dead. That rule was
                        // earned on a real job: nine command buttons across four screens, every one
                        // inert, and no generated file naming any of them.
                        //
                        // A button that is inert now is inert because nobody said what it does, not
                        // because the tool could not act on it - which makes the line MORE pointed,
                        // not less.
                        if (string.IsNullOrWhiteSpace(item.GoTo) && string.IsNullOrWhiteSpace(item.Cmd)
                            && string.IsNullOrWhiteSpace(item.SetTag))
                        {
                            handOff.Add($"{btnName} (\"{item.Text}\"): INERT - no data-hmi-cmd and no "
                                + "data-hmi-goto, so this button does nothing when pressed. Declare "
                                + "what it does, or remove it: an operator cannot tell a dead button "
                                + "from a working one.");
                        }
                        else if (!string.IsNullOrWhiteSpace(item.Cmd) && string.IsNullOrWhiteSpace(item.CmdCode))
                        {
                            handOff.Add($"{btnName} (\"{item.Text}\"): channel \"{item.Cmd}\" declared "
                                + "with NO data-hmi-cmd-code. The sequence would be bumped carrying "
                                + "whatever code was last written - which is a command, and the "
                                + "WRONG one. Give it a code.");
                        }

                        break;
                    }

                    case "Line":
                        WriteLine(w, NextId, Name(item, "Line", emitted), item);
                        emitted++;
                        break;

                    case "Circle":
                        WriteCircle(w, NextId, Name(item, "Circle", emitted), item);
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
                        WriteRectangle(w, NextId, boxName, item, "255, 255, 255", "176, 42, 30");
                        WriteTextField(w, NextId, boxName + "_Label", item with { Text = "ALARM VIEW GOES HERE - add manually" });
                        emitted += 2;
                        handOff.Add(
                            $"{boxName}: add an Alarm view at {item.Left:0},{item.Top:0} sized {item.Width:0}x{item.Height:0}, "
                            + "then DELETE the placeholder rectangle and its label.");
                        break;
                    }
                }
            }


            // Every layer closed itself above - the base one before the declared loop, and each
            // declared one inside it - so only the screen remains open here.
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

    /// <summary>
    /// <c>Hmi.Dynamic.VisibilityAnimation</c> - the object is <c>Visible</c> while the trigger tag
    /// sits inside <c>[RangeStart, RangeEnd]</c>, and the opposite outside it.
    ///
    /// STRUCTURE HARVESTED FROM A HAND-EDITED SCREEN THAT CAME BACK FROM TIA, not documentation:
    /// the four attributes are <c>Name</c>, <c>RangeEnd</c>, <c>RangeStart</c>, <c>Visible</c> (in
    /// that order - TIA writes its AttributeList alphabetically), and the trigger is a
    /// <c>Hmi.Dynamic.TagElementTrigger</c> in composition <c>VisibilityTag</c> whose LinkList
    /// carries the tag.
    ///
    /// 🔴 <c>Name</c> IS THE LITERAL STRING <c>VisibilityAnimation</c> ON EVERY SPECIMEN, ACROSS TWO
    /// UNRELATED PROJECTS - 20 in a screen set a person edited, 27 in a third-party export. It is
    /// the animation's KIND, not a user-chosen label, so it is written as a constant rather than
    /// taken from the IR. Inferred from that corpus rather than measured against TIA's schema.
    ///
    /// ⚠️ ORDER: animations come FIRST in an item's ObjectList, before Events and before Font.
    /// Measured across 26 real screens - all 20 specimens are the first child, and the 8 on buttons
    /// precede the Event. Emitting it after Font has not been tried, so "first" is what is known to
    /// work rather than what is known to be required.
    /// </summary>
    private static void WriteVisibility(XmlWriter w, Func<string> nextId, IrItem i)
    {
        if (i.Visibility is null)
        {
            return;
        }

        var v = i.Visibility;

        w.WriteStartElement("Hmi.Dynamic.VisibilityAnimation");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "Animations");
        w.WriteStartElement("AttributeList");
        Attr(w, "Name", "VisibilityAnimation");
        Attr(w, "RangeEnd", v.RangeEnd);
        Attr(w, "RangeStart", v.RangeStart);
        Attr(w, "Visible", v.Visible ? "true" : "false");
        w.WriteEndElement();

        w.WriteStartElement("ObjectList");
        w.WriteStartElement("Hmi.Dynamic.TagElementTrigger");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "VisibilityTag");
        w.WriteStartElement("LinkList");
        w.WriteStartElement("Tag");
        w.WriteAttributeString("TargetID", "@OpenLink");
        Attr(w, "Name", v.Tag);
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement(); // TagElementTrigger
        w.WriteEndElement(); // ObjectList
        w.WriteEndElement(); // VisibilityAnimation
    }

    /// <summary>
    /// The ObjectList a shape only has when something hangs off it.
    ///
    /// Rectangle, Circle and Line carry no font and no text, so they are written as a bare
    /// AttributeList - which is what a real export shows for an un-animated one. An animated Line in
    /// the third-party corpus DOES have an ObjectList holding nothing but the animation, so the
    /// element is opened only when there is something to put in it rather than always.
    /// </summary>
    private static void WriteShapeObjectList(XmlWriter w, Func<string> nextId, IrItem i)
    {
        if (i.Visibility is null)
        {
            return;
        }

        w.WriteStartElement("ObjectList");
        WriteVisibility(w, nextId, i);
        w.WriteEndElement();
    }

    private static void WriteRectangle(XmlWriter w, Func<string> nextId, string name, IrItem i, string back, string border)
    {
        w.WriteStartElement("Hmi.Screen.Rectangle");
        w.WriteAttributeString("ID", nextId());
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
        WriteShapeObjectList(w, nextId, i);
        w.WriteEndElement();
    }

    private static void WriteCircle(XmlWriter w, Func<string> nextId, string name, IrItem i)
    {
        var radius = (int)Math.Round(Math.Min(i.Width, i.Height) / 2);

        w.WriteStartElement("Hmi.Screen.Circle");
        w.WriteAttributeString("ID", nextId());
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
        WriteShapeObjectList(w, nextId, i);
        w.WriteEndElement();
    }

    private static void WriteLine(XmlWriter w, Func<string> nextId, string name, IrItem i)
    {
        w.WriteStartElement("Hmi.Screen.Line");
        w.WriteAttributeString("ID", nextId());
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
        WriteShapeObjectList(w, nextId, i);
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
        WriteVisibility(w, nextId, i);
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

        // ✅ EVENTS ARE EMITTED, AND THEY COME FIRST. RETRACTION, MEASURED 2026-08-18.
        //
        // This block used to say an event COULD NOT be created on a classic screen, citing a live
        //     'Create' is not supported by type 'Siemens.Engineering.Hmi.Event.EventComposition'
        // and made every button a hand-off item. THAT VERDICT WAS WRONG, and it was wrong for a
        // reason worth keeping: the failing probe differed from a real TIA export in THREE ways at
        // once, and the conclusion was drawn as though the only variable were "an event".
        //
        //   1. IT USED `KeyUp` ON A BUTTON. `KeyUp` belongs to `Hmi.Screen.SoftKey` - the physical
        //      bezel keys. A `Hmi.Screen.Button` has `Press` and `Release`. The harvest that
        //      produced `KeyUp` read the reference screen's SOFTKEYS and attributed their event to
        //      its buttons. That single mis-attribution is the whole finding: the importer
        //      POPULATES AN EXISTING event by name and cannot add one, so a name outside the item
        //      type's fixed set forces the `Create` that the composition refuses - which is exactly
        //      what the error said, read correctly.
        //   2. It put the event LAST in the ObjectList. TIA writes Events FIRST, before Font.
        //   3. It omitted `ActivateScreen`'s second parameter, `Object number`.
        //
        // Corrected on all three, a `Press`/`Release` event imports clean and reads back intact.
        //
        // The animation goes BEFORE the event: that is the order all 8 animated buttons in the
        // hand-edited corpus are written in.
        WriteVisibility(w, nextId, i);
        WriteButtonEvents(w, nextId, i);

        WriteFont(w, nextId, i);
        WriteText(w, nextId, string.Empty, "HelpText");
        WriteText(w, nextId, i.Text ?? string.Empty, "TextOff");
        WriteText(w, nextId, i.Text ?? string.Empty, "TextOn");

        w.WriteEndElement();

        w.WriteEndElement();
    }

    /// <summary>
    /// A declared layer and the items that belong to it.
    ///
    /// The base layer (index 0, no name) is not declared and always exists — it is what every
    /// screen built before layers existed produces, and it must keep producing exactly that.
    /// </summary>
    private sealed record LayerPlan(string Name, int Index, IrVisibility? Visibility, List<IrItem> Items);

    /// <summary>
    /// Work out the layers, validate them, and push each layer's visibility rule onto its members.
    ///
    /// 🔴 THE PUSH IS THE WHOLE FEATURE. A classic <c>ScreenLayer</c> cannot be hidden at runtime
    /// (measured: it carries only <c>Index</c>, <c>Name</c> and <c>VisibleES</c>, and
    /// <c>VisibleES</c> is the TIA editor's own show/hide), so the layer earns its place as the
    /// AUTHORING grouping and the runtime behaviour comes from a
    /// <c>Hmi.Dynamic.VisibilityAnimation</c> on every member.
    ///
    /// Declaring the rule once and copying it here is what makes a dialog whole. Author it per
    /// object and the failure mode is a single object left behind on the glass after the dialog
    /// closes — with nothing about the document, the checks or the render looking wrong.
    /// </summary>
    private static List<LayerPlan> PlanLayers(ScreenIr ir)
    {
        var declarations = ir.Items.Where(i => i.Type == "Layer").ToList();
        var members = ir.Items.Where(i => i.Type is not null && i.Type != "Layer").ToList();

        var plans = new List<LayerPlan>();
        var seenIndex = new Dictionary<int, string>();

        foreach (var d in declarations)
        {
            var name = (d.Layer ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                throw new LayerException("a data-hmi=\"Layer\" declaration carries no data-hmi-layer name. "
                                       + "A layer is referred to by name, so an unnamed one can hold nothing.");
            }

            if (!int.TryParse((d.LayerIndex ?? string.Empty).Trim(), NumberStyles.Integer,
                              CultureInfo.InvariantCulture, out var index))
            {
                throw new LayerException($"layer '{name}' declares data-hmi-layer-index "
                                       + $"'{d.LayerIndex}', which is not a whole number.");
            }

            // Index 0 is the base layer's and is not available: claiming it would silently merge a
            // dialog into the screen behind it, and the merge would look like a working screen.
            if (index <= 0)
            {
                throw new LayerException($"layer '{name}' asks for index {index}. Index 0 is the base "
                                       + "layer — everything not on a named layer — and cannot be claimed.");
            }

            if (seenIndex.TryGetValue(index, out var other))
            {
                throw new LayerException($"layers '{other}' and '{name}' both ask for index {index}. "
                                       + "Two layers at one index is a refusal rather than a merge: "
                                       + "which one a person sees in TIA would depend on import order.");
            }

            seenIndex[index] = name;

            // BOTH FORMS ARE NOW MEASURED, so both can be authored (2026-08-20). `Visible=false`
            // inside the range was the only shape ever put through Portal, so this emitter
            // hard-coded it and an author wanting "show while X" had to write the double negative.
            // A probe settled it: `Visible=true` inside the range imports and round-trips intact,
            // beside a control at false, with `compare` clean.
            var hasHide = !string.IsNullOrWhiteSpace(d.LayerHideWhen);
            var hasShow = !string.IsNullOrWhiteSpace(d.LayerShowWhen);

            // One or the other. Both is not a richer rule, it is two rules for one object - which is
            // the thing B1 proved the platform cannot hold: TIA keeps ONE animation per item and
            // discards the rest in silence.
            if (hasHide && hasShow)
            {
                throw new LayerException($"layer '{name}' declares BOTH data-hmi-layer-hide-when "
                                       + $"('{d.LayerHideWhen}') and data-hmi-layer-show-when "
                                       + $"('{d.LayerShowWhen}'). A layer carries one rule. TIA holds "
                                       + "exactly one visibility animation per object and drops any "
                                       + "second one WITHOUT reporting it, so the two would not both "
                                       + "take effect - one would simply vanish. State the intent in "
                                       + "whichever direction reads straight and use that one.");
            }

            IrVisibility? vis = null;
            if (hasHide || hasShow)
            {
                var visible = hasShow;
                var attr = visible ? "show" : "hide";
                var tag = (visible ? d.LayerShowWhen : d.LayerHideWhen)!.Trim();
                var raw = ((visible ? d.LayerShowRange : d.LayerHideRange) ?? string.Empty).Trim();

                var parts = raw.Split("..", StringSplitOptions.None);
                if (parts.Length != 2 || parts.Any(p => p.Trim().Length == 0))
                {
                    throw new LayerException($"layer '{name}' declares data-hmi-layer-{attr}-when "
                                           + $"'{tag}' but its {attr}-range is '{raw}'. "
                                           + "The range is written low..high, e.g. 0..0.");
                }

                vis = new IrVisibility
                {
                    Tag = tag,
                    RangeStart = parts[0].Trim(),
                    RangeEnd = parts[1].Trim(),
                    Visible = visible,
                };
            }

            var mine = members.Where(i => string.Equals(i.Layer, name, StringComparison.Ordinal)).ToList();

            // Empty is not clean. A declared layer with no members is a dialog that was authored
            // and then lost its contents, and it would emit as a valid, invisible nothing.
            if (mine.Count == 0)
            {
                throw new LayerException($"layer '{name}' is declared and no item names it. "
                                       + "An empty layer emits as a well-formed nothing, so it is "
                                       + "refused rather than written.");
            }

            plans.Add(new LayerPlan(name, index, vis, mine));
        }

        var declaredNames = plans.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var orphans = members
            .Where(i => !string.IsNullOrWhiteSpace(i.Layer) && !declaredNames.Contains(i.Layer!))
            .Select(i => $"{i.ElementId ?? i.Type} -> '{i.Layer}'")
            .ToList();

        if (orphans.Count > 0)
        {
            throw new LayerException($"{orphans.Count} item(s) name a layer that is not declared: "
                                   + string.Join(", ", orphans.Take(6))
                                   + ". A declaration carries the visibility rule, so an item on an "
                                   + "undeclared layer would be emitted with NO rule — permanently "
                                   + "on the glass, over whatever the dialog was meant to cover.");
        }

        // An item may not carry its own visibility AND sit on a layer that carries one: two rules
        // for one object, and the one that wins is an implementation detail nobody should learn.
        var doubled = plans.Where(p => p.Visibility is not null)
            .SelectMany(p => p.Items.Where(i => i.Visibility is not null)
                                    .Select(i => $"{i.ElementId ?? i.Type} on layer '{p.Name}'"))
            .ToList();

        if (doubled.Count > 0)
        {
            throw new LayerException($"{doubled.Count} item(s) carry their own visibility rule AND sit "
                                   + "on a layer that declares one: " + string.Join(", ", doubled.Take(6))
                                   + ". Put the rule in one place.");
        }

        plans.Sort((a, b) => a.Index.CompareTo(b.Index));
        return plans;
    }

    /// <summary>The layer's rule, applied to a member — or the member's own if the layer has none.</summary>
    private static IrItem OnLayer(IrItem item, IrVisibility? layerVisibility) =>
        layerVisibility is null ? item : item with { Visibility = layerVisibility };


    /// <summary>
    /// 🔴 THE VERIFIED FUNCTION VOCABULARY, AND THE ONLY NAMES THIS EMITTER MAY EVER WRITE.
    ///
    /// A function name outside a device's supported set does NOT produce a validation error. It
    /// KILLS THE PORTAL PROCESS, surfacing only as
    ///     'Access to a disposed object of type Siemens.Engineering.Project'
    /// which names nothing. Measured 2026-08-18, and pinned by a control: a deliberately nonsense
    /// name (<c>ZzDefinitelyNotAFunction</c>) crashes IDENTICALLY to a plausible-but-wrong one. So a
    /// wrong guess and pure gibberish are INDISTINGUISHABLE from the outside, and there is no
    /// feedback channel that would let a caller discover the right name by trying.
    ///
    /// That is why this is a whitelist and not a validation. It is the same shape, for the same
    /// reason, as the converter's <c>(name, version)</c> instruction registry: the tool supplies the
    /// name, the name cannot be checked cheaply, and being wrong is expensive. New entries are
    /// earned by HARVESTING A REAL EXPORT, never by reading documentation and never by guessing.
    ///
    /// EVERY NAME BELOW WAS HARVESTED FROM A REAL EXPORT OF A HAND-BUILT EVENT. The four this
    /// emitter actually uses were then round-tripped: imported, exported, and compared field by
    /// field. Note how badly guessing did before the harvest - <c>SetValue</c> and
    /// <c>IncreaseValue</c> are the obvious names, they are what the author reached for, and BOTH
    /// crash Portal. The real names are <c>SetTag</c> and <c>IncreaseTag</c>.
    /// </summary>
    private static readonly HashSet<string> VerifiedFunctions = new(StringComparer.Ordinal)
    {
        // Harvested + round-trip proven by this emitter.
        "ActivateScreen",   // (Screen name: link, Object number: Int32)
        "SetTag",           // (Tag: link, Value: Double)  <- NOT "SetValue", which crashes Portal
        "IncreaseTag",      // (Tag: link, Value: Double)  <- NOT "IncreaseValue", ditto
        "SetBit",           // (Tag: link)

        // Harvested from a real export, NOT yet round-tripped by this emitter. Safe to emit - the
        // name is what the crash is keyed on - but the parameter shapes are unproven here.
        "DecreaseTag",      // (Tag: link, Value: Double)
        "ResetBit",         // (Tag: link)
        "SetBitInTag",      // (Tag: link, Bit: Int32)   - addresses a bit WITHIN a word by number
        "ResetBitInTag",    // (Tag: link, Bit: Int32)
        "InvertBit",        // (Tag: link)
        "StopRuntime",      // (Mode: Int32)
    };

    /// <summary>
    /// Split a <c>data-hmi-set</c> specification into its individual <c>Tag=value</c> writes.
    ///
    /// One parser, used by the validation pass AND by the emission pass, so a segment that is
    /// refused is by construction a segment that is never written. Empty segments are dropped
    /// rather than refused - a trailing <c>;</c> in a generated list is a formatting artefact and
    /// not an author's intent - but a specification that yields NO segments is refused by the
    /// caller, because that one is an author who meant something.
    /// </summary>
    internal static List<string> ParseStagedWrites(string spec) =>
        spec.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToList();

    /// <summary>
    /// The events on a button, written FIRST in its ObjectList because that is where TIA writes them.
    ///
    /// Two shapes are generated, and both hang off <c>Release</c> rather than <c>Press</c>:
    /// a touch that lands on the wrong control can still be cancelled by sliding off before lifting,
    /// which is the behaviour an operator in gloves expects and the only one of the two that is
    /// recoverable. (<c>Press</c> is equally importable - this is a design choice, not a limit.)
    /// </summary>
    private static void WriteButtonEvents(XmlWriter w, Func<string> nextId, IrItem i)
    {
        var entries = new List<Action>();

        // A COMMAND: the code (and any operands) FIRST, then the sequence bump LAST.
        //
        // 🔴 THE ORDER IS THE HANDSHAKE, NOT A STYLE. The controller reads the code when the
        // sequence number CHANGES, so a sequence bumped before its code is written commits the
        // PREVIOUS command - a wrong action, from a correct-looking button, on a panel where every
        // gate is green. The author names a CHANNEL and never the two tags, so this order is not
        // something they can get wrong; it is not expressible.
        if (!string.IsNullOrWhiteSpace(i.Cmd))
        {
            var ch = i.Cmd!.Trim();
            if (!string.IsNullOrWhiteSpace(i.CmdCode))
            {
                entries.Add(() => WriteTagFunction(w, nextId, "SetTag", ch + "_Code", i.CmdCode!));
            }

            if (!string.IsNullOrWhiteSpace(i.CmdInt1))
            {
                entries.Add(() => WriteTagFunction(w, nextId, "SetTag", ch + "_Int1", i.CmdInt1!));
            }

            if (!string.IsNullOrWhiteSpace(i.CmdInt2))
            {
                entries.Add(() => WriteTagFunction(w, nextId, "SetTag", ch + "_Int2", i.CmdInt2!));
            }

            if (!string.IsNullOrWhiteSpace(i.CmdReal1))
            {
                entries.Add(() => WriteTagFunction(w, nextId, "SetTag", ch + "_Real1", i.CmdReal1!));
            }

            if (!string.IsNullOrWhiteSpace(i.CmdReal2))
            {
                entries.Add(() => WriteTagFunction(w, nextId, "SetTag", ch + "_Real2", i.CmdReal2!));
            }

            // The bump is UNCONDITIONAL and LAST. There is no path that writes a code without it.
            entries.Add(() => WriteTagFunction(w, nextId, "IncreaseTag", ch + "_Seq", "1"));
        }

        // STAGED OPERANDS. Writes with no sequence bump - so nothing is COMMANDED by this press.
        // Emitted before any navigation for the same reason a command is: the values must be
        // written while this screen is still the active one.
        //
        // IN THE AUTHOR'S ORDER, and that is deliberate even though nothing here depends on it: a
        // list whose emitted order differs from its written order is a list an author cannot read
        // back off the screen source, and the next thing hung off this attribute might care.
        if (!string.IsNullOrWhiteSpace(i.SetTag))
        {
            foreach (var segment in ParseStagedWrites(i.SetTag!.Trim()))
            {
                var eq = segment.IndexOf('=');
                var target = segment[..eq].Trim();
                var value = segment[(eq + 1)..].Trim();
                entries.Add(() => WriteTagFunction(w, nextId, "SetTag", target, value));
            }
        }

        // NAVIGATION. Emitted after any command on the same button so that a button which both acts
        // and navigates has sent its command before the screen changes.
        if (!string.IsNullOrWhiteSpace(i.GoTo))
        {
            entries.Add(() => WriteActivateScreen(w, nextId, i.GoTo!.Trim()));
        }

        if (entries.Count == 0)
        {
            return;
        }

        WriteEvent(w, nextId, "Release", entries);
    }

    /// <summary>One <c>Hmi.Event.Event</c> wrapping a function list, in TIA's own nesting.</summary>
    private static void WriteEvent(XmlWriter w, Func<string> nextId, string eventName, List<Action> entries)
    {
        w.WriteStartElement("Hmi.Event.Event");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "Events");
        w.WriteStartElement("AttributeList");
        Attr(w, "Name", eventName);
        w.WriteEndElement();

        w.WriteStartElement("ObjectList");
        w.WriteStartElement("Hmi.Event.FunctionListEventHandler");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "EventHandler");
        w.WriteStartElement("ObjectList");

        foreach (var entry in entries)
        {
            entry();
        }

        w.WriteEndElement(); // ObjectList
        w.WriteEndElement(); // FunctionListEventHandler
        w.WriteEndElement(); // ObjectList
        w.WriteEndElement(); // Event
    }

    private static void BeginFunction(XmlWriter w, Func<string> nextId, string function)
    {
        if (!VerifiedFunctions.Contains(function))
        {
            // Fail here, loudly, rather than emit a document that kills Portal with a message
            // naming nothing. See VerifiedFunctions for why this cannot be a runtime check.
            throw new InvalidOperationException(
                $"REFUSED: '{function}' is not in the verified system-function vocabulary. An "
                + "unrecognised function name CRASHES THE TIA PORTAL PROCESS rather than failing "
                + "validation, so it is never emitted on the strength of a guess. Harvest the name "
                + "from a real export of a hand-built event and add it to VerifiedFunctions.");
        }

        w.WriteStartElement("Hmi.Event.FunctionListEntry");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "FunctionListEntries");
        w.WriteStartElement("AttributeList");
        Attr(w, "Name", function);
        Attr(w, "Type", "SystemFunction");
        w.WriteEndElement();
        w.WriteStartElement("ObjectList");
    }

    private static void EndFunction(XmlWriter w)
    {
        w.WriteEndElement(); // ObjectList
        w.WriteEndElement(); // FunctionListEntry
    }

    /// <summary>A parameter carrying a tag or screen by NAME, as an <c>@OpenLink</c>.</summary>
    private static void LinkParam(XmlWriter w, Func<string> nextId, string paramName, string target)
    {
        w.WriteStartElement("Hmi.Event.FunctionListEntryParameter");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "Parameters");
        w.WriteStartElement("AttributeList");
        Attr(w, "Name", paramName);
        w.WriteEndElement();
        w.WriteStartElement("LinkList");
        w.WriteStartElement("Value");
        w.WriteAttributeString("TargetID", "@OpenLink");
        Attr(w, "Name", target);
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
    }

    /// <summary>
    /// A parameter carrying a literal, as a typed <c>Value</c> INSIDE the AttributeList.
    ///
    /// ⚠️ <paramref name="clrType"/> is the CLR type TIA itself writes for that parameter, and it is
    /// NOT the tag's type. Every numeric operand of a tag-writing function is
    /// <c>System.Double</c> - a Word tag, an Int tag and a Real tag all take Double - while
    /// <c>Object number</c> and <c>Bit</c> are <c>System.Int32</c>. Matching the parameter to the
    /// TAG's type instead is a guess that reads as reasonable and was measured wrong.
    /// </summary>
    private static void LiteralParam(XmlWriter w, Func<string> nextId, string paramName, string value, string clrType)
    {
        w.WriteStartElement("Hmi.Event.FunctionListEntryParameter");
        w.WriteAttributeString("ID", nextId());
        w.WriteAttributeString("CompositionName", "Parameters");
        w.WriteStartElement("AttributeList");
        Attr(w, "Name", paramName);
        w.WriteStartElement("Value");
        w.WriteAttributeString("Type", clrType);
        w.WriteString(value);
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
    }

    /// <summary>
    /// <c>SetTag</c> / <c>IncreaseTag</c> / <c>DecreaseTag</c>: (Tag link, value).
    ///
    /// The value is EITHER a literal OR another tag, and the author picks with a leading <c>@</c>:
    /// <c>data-hmi-cmd-int1="3"</c> writes the number 3, <c>data-hmi-cmd-int1="@Bay_A_StateID"</c>
    /// copies that tag's LIVE VALUE at the moment of the press.
    ///
    /// 🔴 THE TAG-VALUED FORM IS NOT COSMETIC — TWO COMMANDS ARE IMPOSSIBLE WITHOUT IT.
    /// The decoder refuses STEP ADVANCE unless its operand EQUALS the vessel's live state, and a
    /// recipe chooser must send the recipe's ID, which is editable data and unknowable when the
    /// screen is built. A literal cannot express either.
    ///
    /// Harvested and round-tripped 2026-08-18, not guessed: the shape is the SAME
    /// <c>@OpenLink</c> a Tag parameter uses, in place of the typed literal. It reads back intact.
    /// </summary>
    private static void WriteTagFunction(XmlWriter w, Func<string> nextId, string function, string tag, string value)
    {
        BeginFunction(w, nextId, function);
        LinkParam(w, nextId, "Tag", tag);
        if (value.StartsWith("@", StringComparison.Ordinal))
        {
            LinkParam(w, nextId, "Value", value.Substring(1).Trim());
        }
        else
        {
            LiteralParam(w, nextId, "Value", value, "System.Double");
        }

        EndFunction(w);
    }

    /// <summary>
    /// <c>ActivateScreen</c>: (Screen name link, Object number Int32).
    ///
    /// The parameter is <c>Screen name</c> - with the space and that capitalisation - and the target
    /// is a LINK, not an attribute value. <c>Object number</c> is not optional: omitting it was one
    /// of the three faults in the probe that produced the false "events cannot be created" verdict.
    /// </summary>
    private static void WriteActivateScreen(XmlWriter w, Func<string> nextId, string targetScreen)
    {
        BeginFunction(w, nextId, "ActivateScreen");
        LinkParam(w, nextId, "Screen name", targetScreen);
        LiteralParam(w, nextId, "Object number", "0", "System.Int32");
        EndFunction(w);
    }

    /// <summary>
    /// The field that shows a coded value as a WORD instead of a number.
    ///
    /// 🔴 This is the type whose absence made the first real screen unreadable: nine of nineteen
    /// fields on it were bare integers standing in for words - the state, the hold cause, the process
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
        WriteVisibility(w, nextId, i);
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

        // A STRING FIELD IS THE SAME OBJECT WITH DataFormat="String" AND A QUESTION-MARK PATTERN.
        //
        // 🔴 THE PLACEHOLDER IS '?', NOT '*', AND THE DIFFERENCE CRASHED PORTAL. MEASURED 2026-08-18.
        //
        // This was first written as an asterisk pattern - reconstructed from the WinCC classic
        // vocabulary, flagged UNPROVEN, and gated behind a two-object throwaway screen for exactly
        // that reason. The gate earned itself on its first use: the probe killed the Portal process,
        // and removing ONLY this field from it made the same document import clean.
        //
        // The correct character was then HARVESTED, not guessed a second time - a real classic
        // export in the corpus carries `DataFormat String`, `FieldLength 80` and eighty '?'. Every
        // other attribute the reconstruction chose was right; the placeholder was the whole of it.
        //
        // Note what this says about the failure mode. A self-contradicting field does not get
        // rejected with a message naming the attribute - it takes the process down, reporting only
        // `Access to a disposed object`. So the ONE unproven character could not have been found by
        // reading the error, and a screen set built on it would have failed as a set.
        var isString = !string.IsNullOrWhiteSpace(i.StringLength);
        var format = isString
            ? new string('?', int.Parse(i.StringLength!.Trim(), CultureInfo.InvariantCulture))
            : (string.IsNullOrWhiteSpace(i.Format) ? "9999" : i.Format!);

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
        Attr(w, "DataFormat", isString ? "String" : "Decimal");
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
        // left-aligned number in a fixed-width field does not. A STRING is left-aligned for the
        // opposite half of the same reason - words read from the left and have no decimal point to
        // line up - which is the rule SymbolicIOField already follows.
        Attr(w, "HorizontalAlignment", isString ? "Left" : "Right");
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
        WriteVisibility(w, nextId, i);
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

        // 🔴 THE CAPTION IS ESCAPED TWICE, AND MISSING THE INNER PASS IS A PORTAL-SIDE REJECTION.
        //
        // This payload is RICH TEXT carried inside an XML attribute, so it goes through two
        // encodings: the writer escapes it once for the XML, and TIA then parses what comes out as
        // markup. A caption containing '&' survives the first pass as a bare ampersand and reaches
        // TIA's HTML parser as an unterminated entity:
        //
        //     The argument 'text' (<body><p>CLEAN & MOTORS</p></body>) has an invalid format.
        //
        // Measured 2026-08-20 on a real screen title. Note where it did NOT fail: the document is
        // well-formed XML, `check` passes, the coherence gate passes and the render is correct -
        // every offline gate is green, because every offline gate reads the caption AFTER one
        // unescape. Only the import refuses it.
        //
        // So the caption is escaped for the INNER markup here, and the XmlWriter does the outer
        // pass. '<' and '>' get the same treatment: a caption is plain text and must never be able
        // to inject an element into the body.
        var escaped = text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        Attr(w, "Text", $"<body><p>{escaped}</p></body>");
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
/// A button whose navigation target is the screen it already sits on.
///
/// Its own class, and NOT folded into a generic failure, because the diagnosis is the whole value:
/// TIA accepts the document, drops the link, and reports nothing. Anyone meeting the dead button
/// later has no route back to the cause.
/// </summary>
public sealed class SelfNavigationException : Exception
{
    public SelfNavigationException(string screenName, IReadOnlyList<string> buttons)
        : base($"REFUSED: {buttons.Count} button(s) on '{screenName}' navigate to that same screen "
               + $"({string.Join(", ", buttons)}). TIA DISCARDS a self-referencing screen link at "
               + "import WITHOUT ERROR, leaving a button that passes every gate and does nothing "
               + "under the operator's finger. Mark the current screen with a Text, or give the "
               + "button a different target.")
    {
    }
}

/// <summary>
/// A layer that cannot be emitted as declared.
///
/// Its own type because every case it covers produces a document that IMPORTS AND COMPILES: an
/// orphaned item, a doubled rule, an empty layer and a claimed index are all well-formed. What
/// they are not is what the author meant, and none of them would show up in a check, a compile or
/// a render - only in front of an operator, once.
/// </summary>
public sealed class LayerException : Exception
{
    public LayerException(string message) : base("REFUSED: " + message)
    {
    }
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

/// <summary>
/// A <c>data-hmi-set</c> that is not a plain, single, non-handshake operand write.
///
/// Its own class because the important refusals are not typos: a write aimed at a <c>_Seq</c> or a
/// <c>_Code</c>, or sitting on a button that also carries a command, is an attempt - almost always
/// an innocent one - to assemble a command channel write out of parts. <see cref="IrItem.Cmd"/>'s
/// whole correctness argument is that the order of that write is not expressible wrongly, and a
/// second path that CAN express it wrongly would quietly retire that argument.
/// </summary>
public sealed class OperandStagingException : Exception
{
    public OperandStagingException(IReadOnlyList<string> problems)
        : base($"REFUSED: {problems.Count} staged tag write(s) this emitter will not produce:"
             + Environment.NewLine
             + string.Join(Environment.NewLine, problems.Select(x => "    " + x)))
    {
    }
}

/// <summary>
/// An IOField declared as a string display that contradicts itself or sits on the wrong item type.
/// Refused rather than emitted, because the family it belongs to - two attributes that must agree,
/// which TIA validates by dying rather than by rejecting - has already cost this project a Portal
/// session once over <c>FieldLength</c>.
/// </summary>
public sealed class StringFieldException : Exception
{
    public StringFieldException(IReadOnlyList<string> problems)
        : base($"REFUSED: {problems.Count} string field declaration(s) that cannot be emitted:"
             + Environment.NewLine
             + string.Join(Environment.NewLine, problems.Select(x => "    " + x)))
    {
    }
}

/// <summary>
/// A visibility animation with no trigger tag. Same family as <see cref="UnboundFieldException"/>:
/// the object imports, the rule never evaluates, and what the operator sees is whatever the panel
/// defaults to - which looks like a decision somebody made.
/// </summary>
public sealed class UnboundAnimationException : Exception
{
    public UnboundAnimationException(IReadOnlyList<string> problems)
        : base($"REFUSED: {problems.Count} visibility animation(s) with nothing to trigger them:"
             + Environment.NewLine
             + string.Join(Environment.NewLine, problems.Select(x => "    " + x)))
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
