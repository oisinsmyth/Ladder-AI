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
    /// 🔴 MEASURED AGAINST A REAL PROJECT, 2026-08-17: an event CANNOT be imported.
    /// <c>'Create' is not supported by type 'Siemens.Engineering.Hmi.Event.EventComposition'.</c>
    ///
    /// So the emitter must NOT write one - emitting it fails the whole import and takes the working
    /// half of the screen with it. This test pins that, because the structure is still in the file
    /// (correct, harvested, and what a future create-capable route would need) and the temptation to
    /// re-enable it will recur.
    /// </summary>
    [Fact]
    public void A_navigation_button_emits_NO_event_because_events_cannot_be_imported()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "HOME", GoTo = "Plant Overview", FontSizePx = 17,
        };

        Assert.Null(Find(Emit(Ir(button)), "Hmi.Event.Event"));
    }

    /// <summary>
    /// The navigation is not silently dropped - it becomes a hand-off item naming the button, the
    /// event and the target. A silent omission would leave a screen whose buttons do nothing and
    /// nobody told to wire them.
    /// </summary>
    [Fact]
    public void A_navigation_button_produces_a_hand_off_item_naming_its_target()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "HOME", GoTo = "Plant Overview", FontSizePx = 17,
        };

        var handOff = Emitter.Emit(Ir(button), "S", 1).HandOff;

        Assert.Single(handOff);
        Assert.Contains("Plant Overview", handOff[0], StringComparison.Ordinal);
        Assert.Contains("ActivateScreen", handOff[0], StringComparison.Ordinal);
    }

    /// <summary>Positive control: an ordinary button raises no hand-off.</summary>
    [Fact]
    public void A_button_with_no_goto_produces_no_hand_off()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "START", FontSizePx = 17,
        };

        Assert.Empty(Emitter.Emit(Ir(button), "S", 1).HandOff);
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
