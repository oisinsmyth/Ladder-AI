using System.Xml.Linq;
using HmiCli;
using Xunit;

namespace HmiCli.Tests;

/// <summary>
/// The emitter learned to BIND on 2026-08-17. Before that it read <c>data-hmi-bind</c> into the IR
/// and discarded it in a ternary whose two branches were identical, so every screen this tool had
/// ever produced was a static picture — and no results document said so.
///
/// These tests exist because that defect was invisible to every gate the project had: the documents
/// imported, compiled and round-tripped clean, and the screens were dead. So each test here asserts
/// against the EMITTED DOCUMENT rather than against an exit code.
///
/// 🔴 Every structure asserted below was HARVESTED FROM A REAL TIA CLASSIC EXPORT, never guessed —
/// the element names, the composition names, the <c>KeyUp</c> event name and the <c>Screen name</c>
/// parameter spelling. A plausible guess here (<c>Click</c> being the obvious one) produces a
/// document that passes every gate and does nothing under the operator's finger.
/// </summary>
public class BindingTests
{
    private static IrItem Field(string? bind, string? mode = null) => new()
    {
        Type = "IOField",
        Left = 100, Top = 100, Width = 96, Height = 51,
        Bind = bind,
        Mode = mode,
        FontSizePx = 17,
    };

    private static ScreenIr Ir(params IrItem[] items) => new()
    {
        Panel = "KTP700",
        CanvasWidth = 800,
        CanvasHeight = 480,
        Items = items.ToList(),
    };

    private static XDocument Emit(ScreenIr ir) =>
        XDocument.Parse(Emitter.Emit(ir, "TestScreen", 1).Xml);

    private static XElement? Find(XDocument d, string localName) =>
        d.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);

    // ---- the binding itself -------------------------------------------------------------------

    [Fact]
    public void IOField_emits_a_tag_connection_naming_the_tag()
    {
        var doc = Emit(Ir(Field("SiloLevel")));

        var property = Find(doc, "Hmi.Screen.Property");
        Assert.NotNull(property);
        Assert.Equal("Properties", property!.Attribute("CompositionName")?.Value);
        Assert.Equal("ProcessValue", property.Element("AttributeList")?.Element("Name")?.Value);

        var dynamic = Find(doc, "Hmi.Dynamic.TagConnectionDynamic");
        Assert.NotNull(dynamic);

        var tag = Find(doc, "Tag");
        Assert.NotNull(tag);
        Assert.Equal("@OpenLink", tag!.Attribute("TargetID")?.Value);
        Assert.Equal("SiloLevel", tag.Element("Name")?.Value);
    }

    /// <summary>
    /// The regression guard for the original defect. Not "does a Tag element exist" but "does the
    /// name the AUTHOR wrote reach the document" — the old code produced a perfectly well-formed
    /// screen while dropping exactly this.
    /// </summary>
    [Fact]
    public void The_authored_tag_name_survives_to_the_document()
    {
        var xml = Emitter.Emit(Ir(Field("DB_HmiSilo_Level_0")), "S", 1).Xml;
        Assert.Contains("DB_HmiSilo_Level_0", xml, StringComparison.Ordinal);
    }

    // ---- fail-closed --------------------------------------------------------------------------

    [Fact]
    public void An_unbound_IOField_is_refused_rather_than_written()
    {
        var ex = Assert.Throws<UnboundFieldException>(() => Emitter.Emit(Ir(Field(null)), "S", 1));
        Assert.Contains("data-hmi-bind", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_whitespace_tag_counts_as_unbound()
    {
        Assert.Throws<UnboundFieldException>(() => Emitter.Emit(Ir(Field("   ")), "S", 1));
    }

    /// <summary>
    /// POSITIVE CONTROL for the two refusals above. Without it, a gate that refused EVERYTHING would
    /// pass both of them — which is the shape of failure this repo keeps finding in its own checks.
    /// </summary>
    [Fact]
    public void A_bound_IOField_is_not_refused()
    {
        var result = Emitter.Emit(Ir(Field("Level")), "S", 1);
        Assert.Equal(1, result.ItemCount);
    }

    // ---- mode ---------------------------------------------------------------------------------

    /// <summary>
    /// Writability is opted into, never inherited. A display field that silently became writable
    /// would hand an operator a control nobody decided to give them.
    /// </summary>
    [Fact]
    public void Mode_defaults_to_Output_and_the_field_is_not_enabled()
    {
        var doc = Emit(Ir(Field("Level")));
        var attrs = Find(doc, "Hmi.Screen.IOField")!.Element("AttributeList")!;

        Assert.Equal("Output", attrs.Element("Mode")?.Value);
        Assert.Equal("false", attrs.Element("Enabled")?.Value);
    }

    [Fact]
    public void An_explicit_Input_mode_is_carried_and_enables_the_field()
    {
        var doc = Emit(Ir(Field("Setpoint", "Input")));
        var attrs = Find(doc, "Hmi.Screen.IOField")!.Element("AttributeList")!;

        Assert.Equal("Input", attrs.Element("Mode")?.Value);
        Assert.Equal("true", attrs.Element("Enabled")?.Value);
    }

    // ---- navigation ---------------------------------------------------------------------------

    /// <summary>
    /// ✅ RETRACTION, 2026-08-18. This test previously asserted that a navigation button emits
    /// NO event, on the strength of a live
    ///     'Create' is not supported by type 'Siemens.Engineering.Hmi.Event.EventComposition'.
    ///
    /// That refusal was real and the conclusion drawn from it was wrong. The failing document
    /// differed from a real TIA export in THREE ways at once - it used <c>KeyUp</c> (a
    /// <c>SoftKey</c> event, not a <c>Button</c> one), it put the event LAST in the ObjectList where
    /// TIA puts it FIRST, and it omitted <c>ActivateScreen</c>'s <c>Object number</c> parameter.
    /// Corrected on all three, the import returns exit 0 and the event reads back intact.
    ///
    /// The test is INVERTED rather than deleted: the old assertion is the one a future reader is
    /// most likely to reintroduce from the surrounding comments, and this is where they will look.
    /// </summary>
    [Fact]
    public void A_navigation_button_emits_an_ActivateScreen_event_on_Release()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "HOME", GoTo = "Plant Overview", FontSizePx = 17,
        };

        var ev = Find(Emit(Ir(button)), "Hmi.Event.Event");

        Assert.NotNull(ev);
        Assert.Equal("Release", ev!.Element("AttributeList")?.Element("Name")?.Value);

        var fn = ev.Descendants("Hmi.Event.FunctionListEntry").Single();
        Assert.Equal("ActivateScreen", fn.Element("AttributeList")?.Element("Name")?.Value);

        var ps = fn.Descendants("Hmi.Event.FunctionListEntryParameter").ToList();

        // The target is a LINK, not an attribute value, and the parameter name carries a space.
        var screen = ps.Single(x => x.Element("AttributeList")?.Element("Name")?.Value == "Screen name");
        Assert.Equal("Plant Overview", screen.Descendants("Value").Single().Element("Name")?.Value);

        // Object number is NOT optional - omitting it was one of the three original faults.
        var objNo = ps.Single(x => x.Element("AttributeList")?.Element("Name")?.Value == "Object number");
        Assert.Equal("System.Int32", objNo.Element("AttributeList")?.Element("Value")?.Attribute("Type")?.Value);
    }

    /// <summary>
    /// The event goes FIRST in the button's ObjectList, before Font. Pinned separately from the
    /// event's content because document ORDER was one of the three faults that produced the false
    /// "events cannot be created" verdict, and it is the one that leaves no trace in a diff of
    /// element names.
    /// </summary>
    [Fact]
    public void A_button_event_is_written_before_the_font()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "HOME", GoTo = "Plant Overview", FontSizePx = 17,
        };

        var btn = Find(Emit(Ir(button)), "Hmi.Screen.Button");
        var children = btn!.Element("ObjectList")!.Elements().Select(x => x.Name.LocalName).ToList();

        Assert.Equal("Hmi.Event.Event", children[0]);
        Assert.Contains("Hmi.Globalization.MultiLingualFont", children);
        Assert.True(children.IndexOf("Hmi.Event.Event")
                    < children.IndexOf("Hmi.Globalization.MultiLingualFont"));
    }

    /// <summary>
    /// A navigating button is FINISHED, so it raises no hand-off. Until 2026-08-18 every button
    /// raised one, because no event could be generated at all.
    /// </summary>
    [Fact]
    public void A_navigation_button_is_not_a_hand_off_item()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "HOME", GoTo = "Plant Overview", FontSizePx = 17,
        };

        Assert.Empty(Emitter.Emit(Ir(button), "S", 1).HandOff);
    }

    /// <summary>
    /// 🔴 A COMMAND button writes its CODE FIRST and bumps the SEQUENCE LAST.
    ///
    /// The order IS the handshake: the controller reads the code when the sequence changes, so a
    /// sequence bumped before its code commits the PREVIOUS command - a wrong action from a
    /// correct-looking button, with every gate green. This test is the only thing standing between
    /// that and a plausible refactor of the emit order.
    /// </summary>
    [Fact]
    public void A_command_button_writes_the_code_before_it_bumps_the_sequence()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "START", Cmd = "Cmd_Unit", CmdCode = "12", CmdInt1 = "3", FontSizePx = 17,
        };

        var ev = Find(Emit(Ir(button)), "Hmi.Event.Event");
        var fns = ev!.Descendants("Hmi.Event.FunctionListEntry")
            .Select(x => (fn: x.Element("AttributeList")!.Element("Name")!.Value,
                          tag: x.Descendants("LinkList").Single().Descendants("Value").Single()
                                .Element("Name")!.Value))
            .ToList();

        Assert.Equal(
            new[] { ("SetTag", "Cmd_Unit_Code"), ("SetTag", "Cmd_Unit_Int1"), ("IncreaseTag", "Cmd_Unit_Seq") },
            fns);
    }

    /// <summary>
    /// Every numeric operand is <c>System.Double</c> - a Word, an Int and a Real tag all take it.
    /// Matching the parameter to the TAG's type instead is a guess that reads as obviously right,
    /// and it was measured wrong.
    /// </summary>
    [Fact]
    public void A_command_operand_is_typed_Double_whatever_the_tag_is()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "START", Cmd = "Cmd_Unit", CmdCode = "12", FontSizePx = 17,
        };

        var ev = Find(Emit(Ir(button)), "Hmi.Event.Event");
        var setTag = ev!.Descendants("Hmi.Event.FunctionListEntry")
            .Single(x => x.Element("AttributeList")?.Element("Name")?.Value == "SetTag");
        var value = setTag.Descendants("Hmi.Event.FunctionListEntryParameter")
            .Single(x => x.Element("AttributeList")?.Element("Name")?.Value == "Value")
            .Element("AttributeList")!.Element("Value")!;

        Assert.Equal("System.Double", value.Attribute("Type")?.Value);
        Assert.Equal("12", value.Value);
    }

    /// <summary>
    /// An operand prefixed <c>@</c> copies another TAG's live value instead of writing a literal.
    ///
    /// Two commands on this plant are impossible without it: STEP ADVANCE is refused unless its
    /// operand equals the vessel's live state, and a recipe chooser must send an ID that is
    /// editable data and unknowable when the screen is built.
    /// </summary>
    [Fact]
    public void A_command_operand_prefixed_with_at_copies_a_tag_instead_of_a_literal()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "STEP", Cmd = "Cmd_Unit", CmdCode = "25", CmdInt1 = "@Unit_StateID",
            FontSizePx = 17,
        };

        var ev = Find(Emit(Ir(button)), "Hmi.Event.Event");
        var setInt1 = ev!.Descendants("Hmi.Event.FunctionListEntry")
            .Single(x => x.Descendants("Name").Any(n => n.Value == "Cmd_Unit_Int1"));
        var value = setInt1.Descendants("Hmi.Event.FunctionListEntryParameter")
            .Single(x => x.Element("AttributeList")?.Element("Name")?.Value == "Value");

        // A LINK, not a typed literal - and the '@' must not survive into the tag name.
        Assert.Equal("Unit_StateID", value.Descendants("Value").Single().Element("Name")?.Value);
        Assert.Null(value.Element("AttributeList")!.Element("Value"));

        // The code is still a literal, and the sequence bump is still last.
        var fns = ev.Descendants("Hmi.Event.FunctionListEntry")
            .Select(x => x.Element("AttributeList")!.Element("Name")!.Value).ToList();
        Assert.Equal("IncreaseTag", fns[^1]);
    }

    /// <summary>
    /// 🔴 A button navigating to its OWN screen is REFUSED.
    ///
    /// Emitted, it imports clean and compiles clean, and TIA silently discards the link - leaving an
    /// ActivateScreen with an empty target and a button that does nothing under the operator's
    /// finger. Measured on the first real screen through the corrected event path.
    /// </summary>
    [Fact]
    public void A_button_navigating_to_its_own_screen_is_refused()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "USER", GoTo = "S", ElementId = "btn_self", FontSizePx = 17,
        };

        var ex = Assert.Throws<SelfNavigationException>(() => Emitter.Emit(Ir(button), "S", 1));
        Assert.Contains("btn_self", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 A COMMAND button — one with no navigation target — must ALSO raise a hand-off, and this
    /// test exists because it did not. Found on a real job: nine command buttons across four screens
    /// emitted completely inert, with no generated file naming any of them.
    ///
    /// A button with no declared target is not LESS incomplete than a navigating one. It is MORE:
    /// no event can be created for either, and for this one nobody even knows what it was meant to
    /// do.
    /// </summary>
    [Fact]
    public void A_command_button_with_no_goto_still_raises_a_hand_off()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "START", FontSizePx = 17,
        };

        var handOff = Emitter.Emit(Ir(button), "S", 1).HandOff;

        Assert.Single(handOff);
        Assert.Contains("INERT", handOff[0], StringComparison.Ordinal);
        Assert.Contains("START", handOff[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// Control: a non-button item raises nothing, so the rule above is about BUTTONS and not a
    /// hand-off line for everything on the screen.
    /// </summary>
    [Fact]
    public void A_plain_field_raises_no_hand_off()
    {
        Assert.Empty(Emitter.Emit(Ir(Field("Level")), "S", 1).HandOff);
    }

    [Fact]
    public void A_navigation_button_naming_no_screen_is_refused()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "HOME", GoTo = "", FontSizePx = 17,
        };

        Assert.Throws<UnboundFieldException>(() => Emitter.Emit(Ir(button), "S", 1));
    }

    // ---- the vocabulary -----------------------------------------------------------------------

    /// <summary>
    /// 🔴 THE THIRD MEMBER OF THE "TWO ATTRIBUTES THAT MUST AGREE" CRASH FAMILY, after Line's
    /// endpoints and Circle's radius. <c>FieldLength</c> is the FormatPattern's LENGTH, not its digit
    /// count — corpus: pattern <c>99999.999</c>, FieldLength <c>9</c>. Emitting the digit count (8)
    /// made them disagree for every pattern containing a decimal point and CRASHED PORTAL on import.
    /// </summary>
    [Theory]
    [InlineData("99999.999", "9")]
    [InlineData("9999.9", "6")]
    [InlineData("9999", "4")]
    [InlineData("999999", "6")]
    public void FieldLength_is_the_pattern_length_not_its_digit_count(string pattern, string expected)
    {
        var item = Field("T") with { Format = pattern };
        var attrs = Find(Emit(Ir(item)), "Hmi.Screen.IOField")!.Element("AttributeList")!;

        Assert.Equal(pattern, attrs.Element("FormatPattern")?.Value);
        Assert.Equal(expected, attrs.Element("FieldLength")?.Value);
    }

    /// <summary>
    /// The regression guard stated as the invariant rather than as four cases: whatever the pattern,
    /// the two attributes agree. A future format feature that breaks the relationship fails here
    /// instead of at a Portal session.
    /// </summary>
    [Theory]
    [InlineData("99999.999")]
    [InlineData("9999.9")]
    [InlineData("9.999")]
    public void FieldLength_and_FormatPattern_never_disagree(string pattern)
    {
        var attrs = Find(Emit(Ir(Field("T") with { Format = pattern })), "Hmi.Screen.IOField")!
            .Element("AttributeList")!;

        Assert.Equal(
            attrs.Element("FormatPattern")!.Value.Length.ToString(),
            attrs.Element("FieldLength")!.Value);
    }

    [Fact]
    public void IOField_is_in_the_supported_set()
    {
        Assert.Contains("IOField", Emitter.SupportedTypes);
    }

    /// <summary>
    /// The document must still be COHERENT — the gate that catches the two measured Portal-crash
    /// classes runs over whatever the emitter produces, so a new item type is a new crash class
    /// until this passes.
    /// </summary>
    [Fact]
    public void An_emitted_IOField_passes_the_coherence_gate()
    {
        var xml = Emitter.Emit(Ir(Field("Level")), "S", 1).Xml;
        var findings = Coherence.Check(xml, 800, 480);
        Assert.Empty(findings);
    }
}
