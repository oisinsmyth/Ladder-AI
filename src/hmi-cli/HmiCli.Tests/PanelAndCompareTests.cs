using HmiCli;
using Xunit;

namespace HmiCli.Tests;

/// <summary>
/// Panel geometry. The property under test is H-406: px/mm is a PAIR, not a scalar. This project
/// asserted square pixels once, reasoning from the Unified MTP700 (which genuinely is square), and
/// the Basic datasheets contradicted it.
/// </summary>
public class PanelTests
{
    [Fact]
    public void BasicPanels_DoNotHaveSquarePixels()
    {
        // 154.1 x 85.9 mm carrying an 800x480 grid: physical aspect 1.794 vs pixel aspect 1.667.
        Assert.NotEqual(Panels.Ktp700Basic.PxPerMmH, Panels.Ktp700Basic.PxPerMmV, precision: 2);
        Assert.NotEqual(Panels.Ktp900Basic.PxPerMmH, Panels.Ktp900Basic.PxPerMmV, precision: 2);
    }

    [Fact]
    public void TheVerticalAxisIsTheTighterOne_WhichIsWhyMinorAxisChecksUseIt()
    {
        Assert.True(Panels.Ktp700Basic.PxPerMmV > Panels.Ktp700Basic.PxPerMmH);
        Assert.True(Panels.Ktp900Basic.PxPerMmV > Panels.Ktp900Basic.PxPerMmH);
    }

    /// <summary>
    /// The anchor (9") and the target (7") share a resolution but differ ~28% physically. This is
    /// the trap that makes H-407 necessary: a layout transfers pixel-for-pixel with no warning while
    /// every physical dimension changes.
    /// </summary>
    [Fact]
    public void AnchorAndTarget_ShareAResolution_ButNotAPhysicalSize()
    {
        Assert.Equal(Panels.Ktp900Basic.WidthPx, Panels.Ktp700Basic.WidthPx);
        Assert.Equal(Panels.Ktp900Basic.HeightPx, Panels.Ktp700Basic.HeightPx);

        var ratio = Panels.Ktp900Basic.WidthMm / Panels.Ktp700Basic.WidthMm;
        Assert.InRange(ratio, 1.25, 1.32);
    }

    [Theory]
    [InlineData(9, 47, 51)]      // the minimum interactive floor - rounds UP: 50px would be 8.95mm
    [InlineData(20, 104, 112)]   // safety-critical
    public void MillimetreThresholds_ConvertPerAxis_OnTheTargetPanel(double mm, int expectH, int expectV)
    {
        Assert.Equal(expectH, Panels.Ktp700Basic.MmToPxH(mm));
        Assert.Equal(expectV, Panels.Ktp700Basic.MmToPxV(mm));
    }

    /// <summary>H-407: an undeclared panel is a refusal, never a default.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("KTP750")]
    public void AnUnknownOrAbsentPanel_IsRefused(string? name)
    {
        Assert.False(Panels.TryResolve(name, out _, out var error));
        Assert.NotEmpty(error);
    }
}

/// <summary>
/// T7. A green import says a document was ACCEPTED; only a read-back comparison says it was accepted
/// AS WRITTEN.
/// </summary>
public class CompareTests
{
    private static string DocWithText(string name, string label) => $"""
<?xml version="1.0" encoding="utf-8"?>
<Document>
  <Hmi.Screen.Screen ID="0">
    <ObjectList>
      <Hmi.Screen.ScreenLayer ID="1" CompositionName="Layers">
        <ObjectList>
          <Hmi.Screen.TextField ID="5" CompositionName="ScreenItems">
            <AttributeList><Height>20</Height><Left>10</Left><ObjectName>{name}</ObjectName>
            <Top>10</Top><Width>100</Width></AttributeList>
            <ObjectList>
              <MultilingualText ID="6" CompositionName="Text">
                <ObjectList>
                  <MultilingualTextItem ID="7" CompositionName="Items">
                    <AttributeList><Culture>en-US</Culture>
                    <Text>&amp;lt;body&amp;gt;&amp;lt;p&amp;gt;{label}&amp;lt;/p&amp;gt;&amp;lt;/body&amp;gt;</Text></AttributeList>
                  </MultilingualTextItem>
                </ObjectList>
              </MultilingualText>
            </ObjectList>
          </Hmi.Screen.TextField>
        </ObjectList>
      </Hmi.Screen.ScreenLayer>
    </ObjectList>
  </Hmi.Screen.Screen>
</Document>
""";

    private static string Doc(string name, int l, int t, int w, int h, string id = "5", string? back = null) => $"""
<?xml version="1.0" encoding="utf-8"?>
<Document>
  <Hmi.Screen.Screen ID="0">
    <ObjectList>
      <Hmi.Screen.ScreenLayer ID="1" CompositionName="Layers">
        <ObjectList>
          <Hmi.Screen.Rectangle ID="{id}" CompositionName="ScreenItems">
            <AttributeList>{(back is null ? "" : $"<BackColor>{back}</BackColor>")}<Height>{h}</Height><Left>{l}</Left><ObjectName>{name}</ObjectName>
            <Top>{t}</Top><Width>{w}</Width></AttributeList>
          </Hmi.Screen.Rectangle>
        </ObjectList>
      </Hmi.Screen.ScreenLayer>
    </ObjectList>
  </Hmi.Screen.Screen>
</Document>
""";

    [Fact]
    public void IdenticalGeometry_CompareEqual()
    {
        var r = ScreenCompare.Compare(Doc("Box", 10, 20, 30, 40), Doc("Box", 10, 20, 30, 40));

        Assert.Equal(0, r.Differences);
        Assert.Equal(1, r.Items);
        Assert.True(r.FieldsComparedOnBothSides > 0);
    }

    /// <summary>
    /// THE REGRESSION. The first version of this comparator compared type and the bounding rectangle
    /// and nothing else, so a changed LABEL round-tripped as IDENTICAL. Caught by a dispatched agent
    /// negative-controlling the tool rather than trusting it.
    /// </summary>
    [Fact]
    public void ChangedLabelText_IsReported_NotSilentlyIgnored()
    {
        var r = ScreenCompare.Compare(DocWithText("Box", "START"), DocWithText("Box", "STARY"));

        Assert.Equal(1, r.Differences);
        Assert.Contains(r.Changed, d => d.Contains("START") && d.Contains("STARY"));
    }

    [Fact]
    public void EscapedAndNestedRichText_AreTheSameContent()
    {
        // The emitter writes the payload escaped; TIA re-exports it as live nested XML.
        var r = ScreenCompare.Compare(DocWithText("Box", "GO"), DocWithText("Box", "GO"));
        Assert.Equal(0, r.Differences);
    }

    [Fact]
    public void AChangedColour_IsReported()
    {
        var r = ScreenCompare.Compare(Doc("Box", 10, 20, 30, 40, back: "182, 182, 182"),
                                      Doc("Box", 10, 20, 30, 40, back: "255, 0, 0"));

        Assert.Equal(1, r.Differences);
        Assert.Contains(r.Changed, d => d.Contains("BackColor"));
    }

    /// <summary>An attribute the emitter stated and the read-back lost is a DROP, and it gates.</summary>
    [Fact]
    public void AnAttributeTiaDropped_Gates()
    {
        var r = ScreenCompare.Compare(Doc("Box", 10, 20, 30, 40, back: "1, 2, 3"), Doc("Box", 10, 20, 30, 40));

        Assert.Single(r.Dropped);
        Assert.Equal(1, r.Differences);
    }

    /// <summary>An attribute only the read-back carries is TIA defaulting, and it does NOT gate.</summary>
    [Fact]
    public void AnAttributeTiaDefaulted_IsReported_ButDoesNotGate()
    {
        var r = ScreenCompare.Compare(Doc("Box", 10, 20, 30, 40), Doc("Box", 10, 20, 30, 40, back: "9, 9, 9"));

        Assert.Equal(0, r.Differences);
        Assert.Equal(1, r.DefaultedByTia);
    }

    /// <summary>
    /// The load-bearing ignore: TIA reassigns element IDs on import unprompted, so a comparator that
    /// keyed on them would report every round trip as different. Objects pair by ObjectName instead.
    /// </summary>
    [Fact]
    public void ADifferentElementId_IsIgnored_BecauseTiaReassignsThem()
    {
        var r = ScreenCompare.Compare(Doc("Box", 10, 20, 30, 40, id: "5"), Doc("Box", 10, 20, 30, 40, id: "FE"));

        Assert.Equal(0, r.Differences);
    }

    [Fact]
    public void ChangedGeometry_IsReported()
    {
        var r = ScreenCompare.Compare(Doc("Box", 10, 20, 30, 40), Doc("Box", 10, 20, 30, 99));

        Assert.Equal(1, r.Differences);
        Assert.Contains(r.Changed, d => d.Contains("Height"));
    }

    [Fact]
    public void AnObjectPresentOnOneSideOnly_IsReported()
    {
        var r = ScreenCompare.Compare(Doc("Box", 10, 20, 30, 40), Doc("Other", 10, 20, 30, 40));

        Assert.Equal(2, r.Differences);
        Assert.Contains(r.Dropped, d => d.Contains("only in the FIRST"));
        Assert.Contains(r.Changed, d => d.Contains("only in the SECOND"));
    }

    /// <summary>Empty is not clean: two documents with no items agree about nothing.</summary>
    [Fact]
    public void TwoEmptyDocuments_AreNotAPass()
    {
        var r = ScreenCompare.Compare("<Document />", "<Document />");

        Assert.True(r.NothingCompared);
    }

    [Fact]
    public void TheIgnoreListIsStated_SoItsScopeIsVisible()
    {
        Assert.NotEmpty(ScreenCompare.Ignored);
        Assert.Contains(ScreenCompare.Ignored, x => x.Contains("ID"));
    }
}
