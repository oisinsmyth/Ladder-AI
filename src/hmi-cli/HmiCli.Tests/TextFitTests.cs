using HmiCli;
using Xunit;

namespace HmiCli.Tests;

/// <summary>
/// H-306 — a text box must be tall enough for the text in it.
///
/// Written after the owner looked at built screens and reported: "the textboxes have been sized too
/// small compared to the text they hold and has resulted in cut-off text at the bottom." It was
/// systematic — 36 of 62 items on one screen, 70 of 84 on another — and NOTHING caught it. The boxes
/// were a legal size, on the canvas, not overlapping, so every geometry rule passed while the glyphs
/// were clipped.
///
/// 🔴 The floors are MEASURED FROM A REAL TIA EXPORT, not derived from font metrics. Height ÷
/// FontSize over 66 text-bearing objects: TextField min 1.35 (23/17), IOField min 1.92, Button 2.67.
/// A real IOField is nearly twice its font size — it carries margins, a border and a focus rectangle
/// a plain label does not. The screens sat at 1.41, which is why the VALUES clipped worst.
/// </summary>
public class TextFitTests
{
    private static ScreenIr Ir(params IrItem[] items) => new()
    {
        Panel = "KTP700",
        CanvasWidth = 800,
        CanvasHeight = 480,
        Items = items.ToList(),
    };

    private static IrItem Text(double height, double font) => new()
    {
        Type = "Text", Left = 10, Top = 10, Width = 90, Height = height,
        Text = "WEIGHT", FontSizePx = font,
    };

    private static IrItem Field(double height, double font) => new()
    {
        Type = "IOField", Left = 200, Top = 10, Width = 88, Height = height,
        Bind = "Tag", FontSizePx = font,
    };

    private static IReadOnlyList<Finding> RunLint(ScreenIr ir)
    {
        Panels.TryResolve("KTP700", out var panel, out _);
        return Linter.Run(ir, panel).Findings;
    }

    private static bool Fires(ScreenIr ir) =>
        RunLint(ir).Any(f => f.RuleId == "H-306");

    // ---- the exact numbers the owner's screens used ------------------------------------------

    [Fact]
    public void The_label_size_that_shipped_is_caught()
    {
        // 18 px for a 14 px font = 1.29, below the corpus floor of 1.35.
        Assert.True(Fires(Ir(Text(18, 14))));
    }

    [Fact]
    public void The_field_size_that_shipped_is_caught()
    {
        // 24 px for a 17 px font = 1.41, against an IOField floor of 1.92 - needs 33.
        Assert.True(Fires(Ir(Field(24, 17))));
    }

    // ---- controls: a gate that fires on everything is not a gate ------------------------------

    [Fact]
    public void A_label_at_the_corpus_ratio_passes()
    {
        Assert.False(Fires(Ir(Text(20, 14))));
    }

    [Fact]
    public void A_field_at_the_corpus_ratio_passes()
    {
        Assert.False(Fires(Ir(Field(34, 17))));
    }

    /// <summary>
    /// The corpus's own tightest real TextField — 23 px at font 17 — must PASS. If the floor rejected
    /// a size TIA itself ships, the floor would be wrong rather than strict.
    /// </summary>
    [Fact]
    public void The_tightest_real_TextField_in_the_corpus_passes()
    {
        Assert.False(Fires(Ir(Text(23, 17))));
    }

    /// <summary>
    /// An IOField is held to a HIGHER floor than a label at the same font — that asymmetry is the
    /// finding, so it is pinned. 23 px is fine for a label and not for a field.
    /// </summary>
    [Fact]
    public void An_IOField_is_held_higher_than_a_label_at_the_same_font()
    {
        Assert.False(Fires(Ir(Text(23, 17))));
        Assert.True(Fires(Ir(Field(23, 17))));
    }

    /// <summary>
    /// Clipped text cannot be waved through. Same class as off-canvas: broken on any panel, to
    /// anyone's taste.
    /// </summary>
    [Fact]
    public void H306_is_a_correctness_rule_and_cannot_be_overridden()
    {
        Assert.Equal(RuleClass.Correctness, RuleClasses.Of("H-306"));
    }
}
