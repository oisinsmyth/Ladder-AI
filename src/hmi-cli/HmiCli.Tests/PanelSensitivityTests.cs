using HmiCli;
using Xunit;

namespace HmiCli.Tests;

/// <summary>
/// WHEN DOES THE PANEL CHOICE ACTUALLY CHANGE A FINDING?
///
/// <para>
/// The project this tooling was built against carries a 7" 800x480 panel whose tier was contested:
/// the artifacts said KTP700 <b>Basic</b> and the project file said a Comfort panel. Nothing in
/// <see cref="Panels"/> covers the Comfort family, so every millimetre threshold has been evaluated
/// against Basic geometry, and the open question was whether that mattered.
/// </para>
/// <para>
/// It is answerable rather than arguable, because the two are geometrically different KINDS of
/// panel and one of each is already in the table. A KTP700 Basic is 154.1 x 85.9 mm carrying
/// 800 x 480 — <b>non-square pixels</b>. A 7" Unified MTP700 is 152.0 x 91.0 for the same grid —
/// <b>square</b>. So MTP700 stands in for any square-pixel 7" panel, and the gap between the two
/// rows brackets the uncertainty.
/// </para>
/// <para>
/// 🔴 THE ANSWER IS A BAND, NOT A YES OR NO, AND THE BAND IS NARROW. On the vertical axis Basic
/// geometry is ~6% STRICTER (5.588 px/mm against 5.275), so it demands a taller control for the
/// same millimetre floor. The two therefore disagree only about controls whose height lands inside
/// that 6% — heights of 48, 49 and 50 px against the 9 mm floor. Below 48 both refuse; at 51 and
/// above both accept.
/// </para>
/// <para>
/// Measured consequence, recorded because it is what the question was really asking: across a full
/// production screen set checked under both rows, every finding was identical. Nothing was sized
/// into the band. That is a fact about those screens, not a licence — a control drawn 50 px tall
/// would separate them, and this test is here so that the next person can see the band rather than
/// re-run the corpus.
/// </para>
/// <para>
/// Note the axes disagree in OPPOSITE directions: horizontally Basic is ~1.4% LOOSER. The rule
/// takes the MINOR axis, so for the wide-and-short controls a panel is mostly made of, the strict
/// axis binds and Basic is the conservative choice. That is why checking against it is safe while
/// the tier is unsettled — but it is safe by accident of shape, not by construction.
/// </para>
/// </summary>
public class PanelSensitivityTests
{
    private const double TouchFloorMm = 9.0;

    private static ScreenIr WithButton(int heightPx) => new()
    {
        Panel = "KTP700",
        CanvasWidth = 800,
        CanvasHeight = 480,
        Items = new List<IrItem>
        {
            new()
            {
                Type = "Button", Left = 40, Top = 40, Width = 200, Height = heightPx,
                Interactive = true, ElementId = "b", FontSizePx = 17,
            },
        },
    };

    private static bool TooSmall(int heightPx, Panel panel) =>
        Linter.Run(WithButton(heightPx), panel).Findings.Any(x => x.RuleId == "H-401");

    /// <summary>The two rows really are different kinds of panel — if this fails, the rest is moot.</summary>
    [Fact]
    public void Basic_pixels_are_not_square_and_the_7in_Unified_pixels_are()
    {
        var basicSkew = Panels.Ktp700Basic.PxPerMmV / Panels.Ktp700Basic.PxPerMmH;
        var unifiedSkew = Panels.Mtp700.PxPerMmV / Panels.Mtp700.PxPerMmH;

        Assert.True(basicSkew > 1.07, $"KTP700 Basic pixels should be ~7.7% taller-dense, got {basicSkew:0.000}");
        Assert.True(Math.Abs(unifiedSkew - 1.0) < 0.01, $"MTP700 pixels should be square, got {unifiedSkew:0.000}");
    }

    /// <summary>Well clear of the floor: the panel choice cannot change the verdict.</summary>
    [Theory]
    [InlineData(68)]   // the height this project's buttons actually use
    [InlineData(60)]
    [InlineData(51)]   // the first height both accept
    public void Above_the_band_both_panels_accept(int heightPx)
    {
        Assert.False(TooSmall(heightPx, Panels.Ktp700Basic));
        Assert.False(TooSmall(heightPx, Panels.Mtp700));
    }

    /// <summary>Well below: both refuse, so again the choice cannot change the verdict.</summary>
    [Theory]
    [InlineData(47)]   // the last height both refuse
    [InlineData(40)]
    [InlineData(24)]
    public void Below_the_band_both_panels_refuse(int heightPx)
    {
        Assert.True(TooSmall(heightPx, Panels.Ktp700Basic));
        Assert.True(TooSmall(heightPx, Panels.Mtp700));
    }

    /// <summary>
    /// 🔴 THE POINT OF THE FILE. These three heights are the entire disagreement between a Basic
    /// and a square-pixel 7" panel under the 9 mm touch floor. A control drawn here is checked
    /// against a panel the project may not have.
    /// </summary>
    [Theory]
    [InlineData(48)]
    [InlineData(49)]
    [InlineData(50)]
    public void Inside_the_band_the_panels_disagree(int heightPx)
    {
        Assert.True(TooSmall(heightPx, Panels.Ktp700Basic),
            $"{heightPx} px is below 9 mm on Basic geometry and should be refused");
        Assert.False(TooSmall(heightPx, Panels.Mtp700),
            $"{heightPx} px clears 9 mm on square-pixel geometry and should be accepted");
    }

    /// <summary>
    /// The band is bounded on both sides — asserted directly so a future geometry edit that widens
    /// it cannot pass quietly. A wider band means more of a real screen sits in the region where
    /// the unsettled tier matters.
    /// </summary>
    [Fact]
    public void The_disagreement_band_is_exactly_three_pixels_tall()
    {
        var band = Enumerable.Range(1, 200)
            .Where(px => TooSmall(px, Panels.Ktp700Basic) != TooSmall(px, Panels.Mtp700))
            .ToArray();

        Assert.Equal(new[] { 48, 49, 50 }, band);

        // And it straddles the floor it is derived from, rather than sitting somewhere unrelated.
        Assert.InRange(Panels.Ktp700Basic.PxToMmV(band[0]), TouchFloorMm - 1.0, TouchFloorMm);
        Assert.InRange(Panels.Mtp700.PxToMmV(band[^1]), TouchFloorMm, TouchFloorMm + 1.0);
    }
}
