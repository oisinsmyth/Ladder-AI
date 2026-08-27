using System.Xml.Linq;
using HmiCli;
using Xunit;

namespace HmiCli.Tests;

/// <summary>
/// Layers, and the visibility rule the emitter copies onto their members.
///
/// 🔴 THE FEATURE EXISTS BECAUSE A CLASSIC LAYER CANNOT BE HIDDEN AT RUNTIME. Measured across 26
/// real screens: a <c>ScreenLayer</c> carries <c>Index</c>, <c>Name</c> and <c>VisibleES</c> and
/// nothing else — no tag link, no animation — and <c>VisibleES</c> is the TIA editor's own
/// show/hide. So a popup cannot be "a layer shown by a condition", which is what the screen specs
/// describe.
///
/// The layer therefore does the AUTHORING job (a person can hide the dialog to work behind it) and
/// a <c>VisibilityAnimation</c> on every member does the RUNTIME job. Declaring the rule once, on
/// the group, is what makes a dialog whole: author it per object and one forgotten object stays on
/// the glass after the dialog closes, with nothing in the document, the checks or the render
/// looking wrong.
/// </summary>
public class LayerTests
{
    private static ScreenIr Ir(params IrItem[] items) => new()
    {
        Panel = "KTP700", CanvasWidth = 800, CanvasHeight = 480, Items = items.ToList(),
    };

    private static IrItem Decl(string name, string index, string? tag = null, string? range = null) => new()
    {
        Type = "Layer", Layer = name, LayerIndex = index, LayerHideWhen = tag, LayerHideRange = range,
        Geometryless = true, Leaf = true, FontSizePx = 17,
    };

    private static IrItem Box(string id, string? layer = null, IrVisibility? own = null) => new()
    {
        Type = "Rectangle", Left = 10, Top = 100, Width = 100, Height = 40,
        ElementId = id, Layer = layer, Visibility = own, FontSizePx = 17,
    };

    private static XDocument Emit(ScreenIr ir) => XDocument.Parse(Emitter.Emit(ir, "S", 1).Xml);

    private static List<XElement> Layers(XDocument d) =>
        d.Descendants().Where(x => x.Name.LocalName == "Hmi.Screen.ScreenLayer").ToList();

    [Fact]
    public void A_screen_with_no_declared_layer_emits_exactly_one_base_layer()
    {
        // The shape every screen built before layers existed produces, and must keep producing.
        var layers = Layers(Emit(Ir(Box("a"), Box("b"))));

        Assert.Single(layers);
        Assert.Equal("0", layers[0].Element("AttributeList")?.Element("Index")?.Value);
        Assert.Equal(string.Empty, layers[0].Element("AttributeList")?.Element("Name")?.Value);
    }

    [Fact]
    public void A_declared_layer_becomes_its_own_ScreenLayer_holding_only_its_members()
    {
        var layers = Layers(Emit(Ir(
            Box("chrome"),
            Decl("prompt", "1"),
            Box("dlg", "prompt"))));

        Assert.Equal(2, layers.Count);
        Assert.Equal("0", layers[0].Element("AttributeList")?.Element("Index")?.Value);
        Assert.Equal("1", layers[1].Element("AttributeList")?.Element("Index")?.Value);
        Assert.Equal("prompt", layers[1].Element("AttributeList")?.Element("Name")?.Value);

        string[] Names(XElement l) => l.Element("ObjectList")!.Elements()
            .Select(x => x.Element("AttributeList")!.Element("ObjectName")!.Value).ToArray();

        Assert.Equal(new[] { "chrome" }, Names(layers[0]));
        Assert.Equal(new[] { "dlg" }, Names(layers[1]));
    }

    /// <summary>
    /// The point of the whole feature: the rule is declared ONCE and reaches EVERY member.
    /// </summary>
    [Fact]
    public void The_layers_rule_is_copied_onto_every_member()
    {
        var doc = Emit(Ir(
            Box("chrome"),
            Decl("prompt", "1", "Bay_A_PromptID", "0..0"),
            Box("dlg-back", "prompt"),
            Box("dlg-text", "prompt")));

        var anims = doc.Descendants()
            .Where(x => x.Name.LocalName == "Hmi.Dynamic.VisibilityAnimation").ToList();

        Assert.Equal(2, anims.Count);
        foreach (var a in anims)
        {
            var al = a.Element("AttributeList")!;
            Assert.Equal("0", al.Element("RangeStart")?.Value);
            Assert.Equal("0", al.Element("RangeEnd")?.Value);

            // Visible=false INSIDE the range is the only form measured against Portal, and what
            // TIA itself writes. "Show while a prompt stands" is authored as "hide while it is 0".
            Assert.Equal("false", al.Element("Visible")?.Value);
            Assert.Equal("Bay_A_PromptID",
                a.Descendants().First(x => x.Name.LocalName == "Tag").Element("Name")?.Value);
        }

        // The base layer's item is untouched — a rule must not leak off its layer.
        var chrome = doc.Descendants().First(x =>
            x.Element("AttributeList")?.Element("ObjectName")?.Value == "chrome");
        Assert.Empty(chrome.Descendants().Where(x => x.Name.LocalName == "Hmi.Dynamic.VisibilityAnimation"));
    }

    [Fact]
    public void A_layer_with_no_rule_leaves_its_members_alone()
    {
        // A layer is legitimate as pure grouping — an author may want the TIA-side show/hide and
        // no runtime behaviour at all.
        var doc = Emit(Ir(Decl("notes", "1"), Box("n1", "notes")));

        Assert.Equal(2, Layers(doc).Count);
        Assert.Empty(doc.Descendants().Where(x => x.Name.LocalName == "Hmi.Dynamic.VisibilityAnimation"));
    }

    [Fact]
    public void An_item_naming_an_undeclared_layer_is_refused()
    {
        // Emitted, it would carry NO rule: permanently on the glass, over whatever the dialog was
        // meant to cover, and well-formed enough to pass every other gate.
        var ex = Assert.Throws<LayerException>(() => Emitter.Emit(Ir(Box("x", "typo")), "S", 1));
        Assert.Contains("typo", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_declared_layer_that_nothing_joins_is_refused()
    {
        var ex = Assert.Throws<LayerException>(() =>
            Emitter.Emit(Ir(Decl("prompt", "1"), Box("a")), "S", 1));
        Assert.Contains("no item names it", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_layer_claiming_index_zero_is_refused()
    {
        // Index 0 is the base layer's. Claiming it merges a dialog into the screen behind it, and
        // the merge looks like a working screen.
        var ex = Assert.Throws<LayerException>(() =>
            Emitter.Emit(Ir(Decl("prompt", "0"), Box("a", "prompt")), "S", 1));
        Assert.Contains("index 0", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Two_layers_at_one_index_are_refused()
    {
        var ex = Assert.Throws<LayerException>(() => Emitter.Emit(Ir(
            Decl("a", "1"), Box("x", "a"),
            Decl("b", "1"), Box("y", "b")), "S", 1));
        Assert.Contains("both ask for index 1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_item_with_its_own_rule_on_a_ruled_layer_is_refused()
    {
        // Two rules for one object, and which wins is an implementation detail nobody should have
        // to learn.
        var own = new IrVisibility { Tag = "Other", RangeStart = "1", RangeEnd = "2", Visible = false };
        var ex = Assert.Throws<LayerException>(() => Emitter.Emit(Ir(
            Decl("prompt", "1", "Bay_A_PromptID", "0..0"),
            Box("dlg", "prompt", own)), "S", 1));
        Assert.Contains("own visibility rule", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_malformed_hide_range_is_refused_rather_than_guessed()
    {
        var ex = Assert.Throws<LayerException>(() => Emitter.Emit(Ir(
            Decl("prompt", "1", "Bay_A_PromptID", "nonsense"),
            Box("dlg", "prompt")), "S", 1));
        Assert.Contains("low..high", ex.Message, StringComparison.Ordinal);
    }

    // ---- the SHOW form, measured 2026-08-20 -----------------------------------------------------

    private static IrItem ShowDecl(string name, string index, string? tag = null, string? range = null) => new()
    {
        Type = "Layer", Layer = name, LayerIndex = index, LayerShowWhen = tag, LayerShowRange = range,
        Geometryless = true, Leaf = true, FontSizePx = 17,
    };

    /// <summary>
    /// ✅ `Visible=true` INSIDE the range is now a proven form, so the emitter can author it.
    ///
    /// <para>
    /// Only the hide form had ever been through Portal, so this emitter hard-coded it and an author
    /// who meant "show this dialog while the state is 1000" had to write it as "hide it while the
    /// state is not 1000" — a double negative standing in for an unmeasured attribute. A probe
    /// settled it: the show form imports and round-trips intact, beside a control at false.
    /// </para>
    /// </summary>
    [Fact]
    public void The_show_form_emits_Visible_true_over_the_declared_range()
    {
        var doc = Emit(Ir(
            ShowDecl("memfault", "1", "Bay_A_StateID", "1000..1000"),
            Box("dlg", "memfault")));

        var anim = doc.Descendants().Single(x => x.Name.LocalName == "Hmi.Dynamic.VisibilityAnimation");
        var al = anim.Element("AttributeList")!;

        Assert.Equal("true", al.Element("Visible")?.Value);
        Assert.Equal("1000", al.Element("RangeStart")?.Value);
        Assert.Equal("1000", al.Element("RangeEnd")?.Value);
        Assert.Equal("Bay_A_StateID",
            anim.Descendants().First(x => x.Name.LocalName == "Tag").Element("Name")?.Value);
    }

    /// <summary>The hide form must keep behaving exactly as it did — this is the regression guard.</summary>
    [Fact]
    public void The_hide_form_is_unchanged_by_the_show_form_existing()
    {
        var doc = Emit(Ir(Decl("prompt", "1", "Bay_A_PromptID", "0..0"), Box("dlg", "prompt")));
        var al = doc.Descendants().Single(x => x.Name.LocalName == "Hmi.Dynamic.VisibilityAnimation")
                    .Element("AttributeList")!;

        Assert.Equal("false", al.Element("Visible")?.Value);
        Assert.Equal("0", al.Element("RangeStart")?.Value);
    }

    /// <summary>
    /// 🔴 Both rules on one layer is refused, and the reason is B1: TIA holds exactly ONE
    /// visibility animation per object and discards a second in silence. Two declared rules would
    /// not combine — one would simply vanish, with a green import and a green compile.
    /// </summary>
    [Fact]
    public void Declaring_BOTH_a_hide_and_a_show_rule_is_refused()
    {
        var both = new IrItem
        {
            Type = "Layer", Layer = "prompt", LayerIndex = "1",
            LayerHideWhen = "A", LayerHideRange = "0..0",
            LayerShowWhen = "B", LayerShowRange = "1..1",
            Geometryless = true, Leaf = true, FontSizePx = 17,
        };

        var ex = Assert.Throws<LayerException>(() => Emitter.Emit(Ir(both, Box("dlg", "prompt")), "S", 1));
        Assert.Contains("one rule", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_malformed_show_range_is_refused_and_names_the_show_attribute()
    {
        var ex = Assert.Throws<LayerException>(() => Emitter.Emit(Ir(
            ShowDecl("memfault", "1", "Bay_A_StateID", "nonsense"),
            Box("dlg", "memfault")), "S", 1));

        Assert.Contains("show-range", ex.Message, StringComparison.Ordinal);
        Assert.Contains("low..high", ex.Message, StringComparison.Ordinal);
    }

    // ---- the geometry rules and layers ----------------------------------------------------------

    private static IrItem Btn(string id, int left, int top, string? layer = null) => new()
    {
        Type = "Button", Left = left, Top = top, Width = 116, Height = 68,
        Interactive = true, ElementId = id, Layer = layer, FontSizePx = 17,
    };

    /// <summary>
    /// 🔴 THE REGRESSION THIS PAIR EXISTS TO CATCH IS THE SECOND TEST, NOT THE FIRST.
    ///
    /// Excluding cross-layer pairs is right — two controls on different layers are never on the
    /// glass together. Excluding too much would switch the rule off on the screens that have
    /// layers, which are exactly the crowded ones.
    /// </summary>
    [Fact]
    public void Interactives_on_DIFFERENT_layers_do_not_collide()
    {
        // A host button and a popup button in the same place: the popup covers the host, and the
        // emitter hides the popup whenever the dialog is down.
        var ir = Ir(Btn("host-abort", 100, 200),
                    Decl("prompt", "1", "Bay_A_PromptID", "0..0"),
                    Btn("dlg-answer", 100, 200, "prompt"));

        var findings = Linter.Run(ir, Panels.Ktp700Basic).Findings;

        Assert.DoesNotContain(findings, x => x.RuleId is "H-503" or "H-404");
    }

    /// <summary>
    /// 🔴 THE HOLE THE FIRST VERSION OF THIS EXEMPTION LEFT, AND IT IS THE ONE THAT MATTERS.
    ///
    /// <para>
    /// "Different layers" was treated as proof that two controls never coexist. It is not. A
    /// dialog's three answer buttons sit on three layers, keyed on three different tags, and are on
    /// the glass together constantly — so the rule silently stopped comparing exactly the controls a
    /// popup crowds most tightly, while still reporting a full denominator.
    /// </para>
    /// </summary>
    [Fact]
    public void Interactives_on_different_layers_keyed_on_DIFFERENT_tags_DO_collide()
    {
        var ir = Ir(Decl("ans1", "1", "Bay_A_PromptAnswerCode1", "0..0"), Btn("a", 100, 200, "ans1"),
                    Decl("ans2", "2", "Bay_A_PromptAnswerCode2", "0..0"), Btn("b", 110, 210, "ans2"));

        Assert.Contains(Linter.Run(ir, Panels.Ktp700Basic).Findings, x => x.RuleId == "H-503");
    }

    /// <summary>
    /// The case the exemption is FOR: a live control and its greyed stand-in, same tag, disjoint
    /// visible sets. Provably never together, so never compared.
    /// </summary>
    [Fact]
    public void A_live_control_and_its_greyed_stand_in_do_not_collide()
    {
        var ir = Ir(
            ShowDecl("blocked", "1", "Bay_A_PromptOffer", "1..99"), Btn("grey", 100, 200, "blocked"),
            Decl("ans1", "2", "Bay_A_PromptOffer", "0..100"), Btn("live", 100, 200, "ans1"));

        Assert.DoesNotContain(Linter.Run(ir, Panels.Ktp700Basic).Findings,
            x => x.RuleId is "H-503" or "H-404");
    }

    /// <summary>
    /// Same tag, but the ranges OVERLAP — so both can be visible and the pair must be compared.
    /// This is what stops the same-tag test becoming a new blanket exemption.
    /// </summary>
    [Fact]
    public void Same_tag_layers_whose_ranges_overlap_DO_collide()
    {
        var ir = Ir(
            Decl("frame", "1", "Bay_A_PromptID", "0..0"), Btn("f", 100, 200, "frame"),
            Decl("ans1", "2", "Bay_A_PromptID", "0..100"), Btn("a", 110, 210, "ans1"));

        Assert.Contains(Linter.Run(ir, Panels.Ktp700Basic).Findings, x => x.RuleId == "H-503");
    }

    [Fact]
    public void Interactives_on_the_SAME_layer_still_collide()
    {
        var ir = Ir(Decl("prompt", "1", "Bay_A_PromptID", "0..0"),
                    Btn("a", 100, 200, "prompt"),
                    Btn("b", 110, 210, "prompt"));

        var findings = Linter.Run(ir, Panels.Ktp700Basic).Findings;

        Assert.Contains(findings, x => x.RuleId == "H-503");
    }

    [Fact]
    public void Interactives_on_the_BASE_layer_still_collide()
    {
        // The overwhelmingly common case: no layers declared anywhere.
        var ir = Ir(Btn("a", 100, 200), Btn("b", 110, 210));

        Assert.Contains(Linter.Run(ir, Panels.Ktp700Basic).Findings, x => x.RuleId == "H-503");
    }
}
