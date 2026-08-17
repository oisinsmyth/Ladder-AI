using HmiCli;
using Xunit;

namespace HmiCli.Tests;

/// <summary>
/// The coherence gate exists because of one measured incident: a Line whose Start/End points sat
/// outside its own bounding box did not get REJECTED by TIA's import - it CRASHED THE PORTAL
/// PROCESS, reporting only "Access to a disposed object of type 'Siemens.Engineering.Project'".
///
/// These tests are deliberately built around reproducing that exact document, because a gate that
/// has never been shown to fire is indistinguishable from no gate at all.
/// </summary>
public class CoherenceTests
{
    private const int W = 800;
    private const int H = 480;

    private static string Doc(string items) => $"""
<?xml version="1.0" encoding="utf-8"?>
<Document>
  <Engineering version="V20" />
  <Hmi.Screen.Screen ID="0">
    <AttributeList><Width>{W}</Width><Height>{H}</Height></AttributeList>
    <ObjectList>
      <Hmi.Screen.ScreenLayer ID="1" CompositionName="Layers">
        <AttributeList><Index>0</Index><VisibleES>true</VisibleES></AttributeList>
        <ObjectList>{items}</ObjectList>
      </Hmi.Screen.ScreenLayer>
    </ObjectList>
  </Hmi.Screen.Screen>
</Document>
""";

    private static string Rect(string name, int l, int t, int w, int h) => $"""
<Hmi.Screen.Rectangle ID="2" CompositionName="ScreenItems">
  <AttributeList><Height>{h}</Height><Left>{l}</Left><ObjectName>{name}</ObjectName>
  <Top>{t}</Top><Width>{w}</Width></AttributeList>
</Hmi.Screen.Rectangle>
""";

    private static string Line(string name, int l, int t, int w, int h, int sl, int st, int el, int et) => $"""
<Hmi.Screen.Line ID="3" CompositionName="ScreenItems">
  <AttributeList><EndLeft>{el}</EndLeft><EndTop>{et}</EndTop><Height>{h}</Height><Left>{l}</Left>
  <ObjectName>{name}</ObjectName><StartLeft>{sl}</StartLeft><StartTop>{st}</StartTop>
  <Top>{t}</Top><Width>{w}</Width></AttributeList>
</Hmi.Screen.Line>
""";

    // ---- the incident, reproduced ---------------------------------------------------------------

    /// <summary>
    /// THE PORTAL-CRASHING DOCUMENT. Left=40 Top=400 Width=400 Height=1 with endpoints expressed as
    /// OFFSETS (0,0)-(400,1) instead of absolute screen coordinates. This is the exact shape that
    /// killed the Portal process, and the gate must catch it without Portal being involved.
    /// </summary>
    [Fact]
    public void Line_WithRelativeEndpoints_IsCaught_BecauseTiaCrashesRatherThanRejecting()
    {
        var xml = Doc(Line("Line_11", l: 40, t: 400, w: 400, h: 1, sl: 0, st: 0, el: 400, et: 1));

        var findings = Coherence.Check(xml, W, H);

        var f = Assert.Single(findings);
        Assert.Equal("C-LINE", f.RuleId);
        Assert.Equal(Severity.Error, f.Severity);
        // The message has to carry the WHY: a future reader meeting this finding needs to know the
        // failure mode is a crash, or they will assume an import error and go looking for one.
        Assert.Contains("ABSOLUTE", f.Message);
        Assert.Contains("CRASHES THE PORTAL PROCESS", f.Message);
    }

    /// <summary>The same line written correctly must pass - otherwise the test above proves nothing.</summary>
    [Fact]
    public void Line_WithAbsoluteEndpoints_IsClean()
    {
        var xml = Doc(Line("Line_11", l: 40, t: 400, w: 400, h: 1, sl: 40, st: 400, el: 440, et: 401));

        Assert.Empty(Coherence.Check(xml, W, H));
    }

    /// <summary>Endpoint order must not matter: a line drawn right-to-left is still coherent.</summary>
    [Fact]
    public void Line_WithReversedEndpoints_IsClean()
    {
        var xml = Doc(Line("Line_r", l: 40, t: 400, w: 400, h: 1, sl: 440, st: 401, el: 40, et: 400));

        Assert.Empty(Coherence.Check(xml, W, H));
    }

    // ---- the other geometry traps ---------------------------------------------------------------

    [Fact]
    public void ItemExtendingBeyondTheScreen_IsCaught()
    {
        var xml = Doc(Rect("Box", l: 700, t: 10, w: 200, h: 20));   // 700+200 > 800

        var f = Assert.Single(Coherence.Check(xml, W, H));
        Assert.Equal("C-BOUNDS", f.RuleId);
    }

    [Fact]
    public void NegativeSize_IsCaught()
    {
        var xml = Doc(Rect("Box", l: 10, t: 10, w: -5, h: 20));

        Assert.Contains(Coherence.Check(xml, W, H), f => f.RuleId == "C-NEG");
    }

    [Fact]
    public void WellFormedItem_IsClean()
    {
        Assert.Empty(Coherence.Check(Doc(Rect("Box", 10, 10, 100, 50)), W, H));
    }

    // ---- empty is not clean ---------------------------------------------------------------------

    /// <summary>
    /// A document with no screen items validated nothing, and reporting that as clean is how a
    /// broken flatten reaches TIA looking like a working pipeline. This project has been bitten by
    /// the same shape on drift-check, compare, reuse-scan, undriven-scan and compile-all.
    /// </summary>
    [Fact]
    public void DocumentWithNoItems_IsReported_NotPassed()
    {
        var f = Assert.Single(Coherence.Check(Doc(string.Empty), W, H));
        Assert.Equal("C-EMPTY", f.RuleId);
    }

    [Fact]
    public void MalformedXml_IsReported_RatherThanThrowing()
    {
        var f = Assert.Single(Coherence.Check("<Document><unclosed>", W, H));
        Assert.Equal("C-XML", f.RuleId);
    }
}
