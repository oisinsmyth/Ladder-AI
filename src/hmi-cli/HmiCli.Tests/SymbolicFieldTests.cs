using System.Xml.Linq;
using HmiCli;
using Xunit;

namespace HmiCli.Tests;

/// <summary>
/// SymbolicIOField — the field that shows a coded value as a WORD instead of a number.
///
/// 🔴 Built after the owner reviewed the first real screen and asked whether a non-technical operator
/// could read it. Nine of nineteen fields on it were bare integers standing in for words — the state,
/// the hold cause, the moisture stage — because the emitter had no type that could resolve them.
///
/// The structure is harvested from TWO real specimens, not documentation: the owner's own
/// hand-placed field on the JOB9004 screen (BIT mode — BitNumber/OnValue with TextOff/TextOn) and the
/// reference corpus's TEXT-LIST variant (a TextList link plus a tag). This emits the text-list form,
/// because a state number needs many values resolved to many words.
/// </summary>
public class SymbolicFieldTests
{
    private static ScreenIr Ir(params IrItem[] items) => new()
    {
        Panel = "KTP700",
        CanvasWidth = 800,
        CanvasHeight = 480,
        Items = items.ToList(),
    };

    private static IrItem Sym(string? bind, string? list, string? mode = null, double height = 34) => new()
    {
        Type = "SymbolicIOField",
        Left = 100, Top = 100, Width = 170, Height = height,
        Bind = bind, TextList = list, Mode = mode, FontSizePx = 17,
    };

    private static XDocument Emit(ScreenIr ir) => XDocument.Parse(Emitter.Emit(ir, "S", 1).Xml);

    private static XElement? Find(XDocument d, string localName) =>
        d.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);

    // ---- the two links, at their two DIFFERENT levels ------------------------------------------

    /// <summary>
    /// 🔴 The two links sit at DIFFERENT levels and swapping them yields a document that imports into
    /// nothing: the TEXT LIST hangs off the ITEM beside its AttributeList, while the TAG hangs off a
    /// Property inside the ObjectList. This test pins both.
    /// </summary>
    [Fact]
    public void Emits_a_text_list_link_on_the_item_and_a_tag_link_on_the_property()
    {
        var doc = Emit(Ir(Sym("Silo_W_StateID", "TL_SiloState")));

        var field = Find(doc, "Hmi.Screen.SymbolicIOField")!;

        var listLink = field.Elements().First(e => e.Name.LocalName == "LinkList")
                            .Elements().First(e => e.Name.LocalName == "TextList");
        Assert.Equal("@OpenLink", listLink.Attribute("TargetID")?.Value);
        Assert.Equal("TL_SiloState", listLink.Element("Name")?.Value);

        var property = Find(doc, "Hmi.Screen.Property")!;
        Assert.Equal("ProcessValue", property.Element("AttributeList")?.Element("Name")?.Value);
        Assert.Equal("Silo_W_StateID", Find(doc, "Tag")!.Element("Name")?.Value);
    }

    // ---- fail-closed on BOTH halves -------------------------------------------------------------

    /// <summary>
    /// Without a text list the field shows the NUMBER — which is precisely the unreadable state this
    /// type exists to remove, and it would look like a working field to everyone downstream.
    /// </summary>
    [Fact]
    public void A_field_with_no_text_list_is_refused()
    {
        Assert.Throws<UnboundFieldException>(() => Emitter.Emit(Ir(Sym("Tag", null)), "S", 1));
    }

    [Fact]
    public void A_field_with_no_tag_is_refused()
    {
        Assert.Throws<UnboundFieldException>(() => Emitter.Emit(Ir(Sym(null, "TL_X")), "S", 1));
    }

    /// <summary>Positive control — a gate that refused everything would pass both refusals above.</summary>
    [Fact]
    public void A_field_with_both_halves_is_emitted()
    {
        Assert.Equal(1, Emitter.Emit(Ir(Sym("Tag", "TL_X")), "S", 1).ItemCount);
    }

    // ---- house rules over the corpus ------------------------------------------------------------

    /// <summary>
    /// The corpus uses CornerRadius 3 / EdgeStyle Double, and the owner's hand-placed specimen came
    /// out Style3D — which is simply TIA's default and is what H-204 exists to catch.
    /// </summary>
    [Fact]
    public void House_form_rules_beat_the_corpus_defaults()
    {
        var attrs = Find(Emit(Ir(Sym("Tag", "TL_X"))), "Hmi.Screen.SymbolicIOField")!
            .Element("AttributeList")!;

        Assert.Equal("0", attrs.Element("CornerRadius")?.Value);
        Assert.Equal("Solid", attrs.Element("EdgeStyle")?.Value);
        Assert.Equal("false", attrs.Element("UseDesignColorSchema")?.Value);
    }

    /// <summary>
    /// A drop-down is an INPUT affordance. On a display field it invites a press that does nothing,
    /// so it is off unless the field is genuinely writable — and writability is opted into.
    /// </summary>
    [Fact]
    public void Output_mode_hides_the_drop_down_and_disables_the_field()
    {
        var attrs = Find(Emit(Ir(Sym("Tag", "TL_X"))), "Hmi.Screen.SymbolicIOField")!
            .Element("AttributeList")!;

        Assert.Equal("Output", attrs.Element("Mode")?.Value);
        Assert.Equal("false", attrs.Element("Enabled")?.Value);
        Assert.Equal("false", attrs.Element("ShowDropDownButton")?.Value);
    }

    [Fact]
    public void An_input_field_offers_the_drop_down()
    {
        var attrs = Find(Emit(Ir(Sym("Tag", "TL_X", "InOutput"))), "Hmi.Screen.SymbolicIOField")!
            .Element("AttributeList")!;

        Assert.Equal("true", attrs.Element("ShowDropDownButton")?.Value);
    }

    /// <summary>Words read from the left; only numbers line up on the right to align their points.</summary>
    [Fact]
    public void Text_is_left_aligned_unlike_a_numeric_field()
    {
        var attrs = Find(Emit(Ir(Sym("Tag", "TL_X"))), "Hmi.Screen.SymbolicIOField")!
            .Element("AttributeList")!;

        Assert.Equal("Left", attrs.Element("HorizontalAlignment")?.Value);
    }

    // ---- H-306 -----------------------------------------------------------------------------------

    /// <summary>
    /// The floor is 1.47 — the SMALLER of the two specimens (corpus 25/17). A floor that rejected a
    /// size TIA itself ships would be wrong rather than strict, so the corpus size must pass.
    /// </summary>
    [Fact]
    public void The_corpus_specimen_height_passes_and_a_shorter_one_does_not()
    {
        Panels.TryResolve("KTP700", out var panel, out _);

        var ok = Linter.Run(Ir(Sym("T", "L", null, 25)), panel).Findings.Any(f => f.RuleId == "H-306");
        var bad = Linter.Run(Ir(Sym("T", "L", null, 20)), panel).Findings.Any(f => f.RuleId == "H-306");

        Assert.False(ok);
        Assert.True(bad);
    }

    [Fact]
    public void SymbolicIOField_is_in_the_supported_set()
    {
        Assert.Contains("SymbolicIOField", Emitter.SupportedTypes);
    }

    [Fact]
    public void An_emitted_field_passes_the_coherence_gate()
    {
        Assert.Empty(Coherence.Check(Emitter.Emit(Ir(Sym("Tag", "TL_X")), "S", 1).Xml, 800, 480));
    }
}
