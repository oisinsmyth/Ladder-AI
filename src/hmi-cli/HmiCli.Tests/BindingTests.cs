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
    /// 🔴 <c>KeyUp</c>, not <c>Click</c>. Harvested from a real export where all four navigation
    /// buttons use it. This assertion is the whole reason the test exists.
    /// </summary>
    [Fact]
    public void A_navigation_button_emits_ActivateScreen_on_KeyUp()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "HOME", GoTo = "Plant Overview", FontSizePx = 17,
        };

        var doc = Emit(Ir(button));

        var ev = Find(doc, "Hmi.Event.Event");
        Assert.NotNull(ev);
        Assert.Equal("KeyUp", ev!.Element("AttributeList")?.Element("Name")?.Value);

        var entry = Find(doc, "Hmi.Event.FunctionListEntry")!.Element("AttributeList")!;
        Assert.Equal("ActivateScreen", entry.Element("Name")?.Value);
        Assert.Equal("SystemFunction", entry.Element("Type")?.Value);

        // The parameter name carries a space and that capitalisation, from the export.
        var param = Find(doc, "Hmi.Event.FunctionListEntryParameter")!;
        Assert.Equal("Screen name", param.Element("AttributeList")?.Element("Name")?.Value);
        Assert.Equal("Plant Overview", param.Element("LinkList")?.Element("Value")?.Element("Name")?.Value);
    }

    /// <summary>Positive control: an ordinary button must NOT acquire an event.</summary>
    [Fact]
    public void A_button_with_no_goto_emits_no_event()
    {
        var button = new IrItem
        {
            Type = "Button", Left = 0, Top = 400, Width = 120, Height = 68,
            Text = "START", FontSizePx = 17,
        };

        Assert.Null(Find(Emit(Ir(button)), "Hmi.Event.Event"));
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
