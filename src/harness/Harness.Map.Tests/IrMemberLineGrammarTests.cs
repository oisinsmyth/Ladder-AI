using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE DUPLICATED GRAMMAR, PINNED AGAINST LINES COPIED VERBATIM OUT OF THE COMMITTED CORPUS.</b>
///
/// <para><c>Harness.Map</c> reads the member-line grammar whose authority is
/// <c>src/converter/Converter/Ir/DbMemberLineFormat.cs</c>, and it reads it with its own code because the
/// harness takes no converter project reference (see <c>Directory.Build.props</c>, ruling C2 — permitted,
/// but a build-layout decision with a cost, and not this lane's to take).</para>
///
/// <para><b>A duplicated parser's one unacceptable failure mode is projecting a line WRONG.</b> So every
/// input below is a real line lifted out of <c>ir/test-project001/</c>, and the two halves this file pins
/// are the two the peel order exists for: <b>a COMMENT containing an <c>=</c></b>, which the converter's own
/// comment says must be peeled before the start value or it is misread as one, and <b>a member with no
/// <c> : </c> at all</b>, which must throw rather than be skipped.</para>
/// </summary>
public class IrMemberLineGrammarTests
{
    private static string Render(string line)
    {
        var member = IrMemberLineReader.Parse(line);
        return member.Render(member.StartValue);
    }

    /// <summary>Markers and nesting indentation survive verbatim; nothing splits on whitespace.</summary>
    [Theory]
    [InlineData("    Running : Bool")]
    [InlineData("    PreBoundaryDone : Bool RETAIN")]
    [InlineData("    RunTimer : TON_TIME VERSION 1.0 SETPOINT")]
    [InlineData("    IO : \"UDT_HopperBlockageIO\" RETAIN SETPOINT")]
    [InlineData("      ADDR : Array[1..4] of Byte")]
    [InlineData("    Initial_Call : Bool BAREPARAM INFORMATIVE \"Initial call of this OB\"")]
    public void A_MEMBER_LINE_ROUND_TRIPS_UNCHANGED(string line) => Assert.Equal(line, Render(line));

    /// <summary>A start value is separated out and re-emitted in the same place.</summary>
    [Theory]
    [InlineData("      LocalPort : UInt = 503", "503")]
    [InlineData("      ID : CONN_OUC = 16#0010", "16#0010")]
    [InlineData("    BlockedTimeThreshold : Time = T#60S", "T#60S")]
    public void A_START_VALUE_IS_READ_AND_RE_EMITTED(string line, string expected)
    {
        Assert.Equal(expected, IrMemberLineReader.Parse(line).StartValue);
        Assert.Equal(line, Render(line));
    }

    /// <summary>
    /// 🔴 <b>THE PEEL ORDER, AND THE REASON IT EXISTS: comment prose routinely contains an <c>=</c>.</b>
    /// This line is from <c>ir/test-project001/FB_HopperBlockageStim.ir</c>'s own <c>Remanence</c>-style
    /// prose family — peel the start value first and the member acquires a start value of
    /// <c>"True, if remanent data are available"</c>.
    /// </summary>
    [Fact]
    public void A_COMMENT_CONTAINING_AN_EQUALS_IS_NOT_READ_AS_A_START_VALUE()
    {
        var member = IrMemberLineReader.Parse(
            "    Remanence : Bool COMMENT \"=True, if remanent data are available\"");

        Assert.Null(member.StartValue);
        Assert.Equal("Bool", member.Body);
        Assert.Equal("    Remanence : Bool", member.Render(null));
    }

    /// <summary>And with both present, both are found — the comment peeled first, then the start value.</summary>
    [Fact]
    public void A_START_VALUE_AND_A_COMMENT_CONTAINING_AN_EQUALS_ARE_BOTH_READ()
    {
        var member = IrMemberLineReader.Parse(
            "    Threshold : Time = T#60S COMMENT \"Commissioning default; the spec writes it as t = 60 s.\"");

        Assert.Equal("T#60S", member.StartValue);
        Assert.Equal("Time", member.Body);
    }

    /// <summary>
    /// 🔴 <b>A LINE THE GRAMMAR DOES NOT RECOGNISE THROWS RATHER THAN BEING SKIPPED, and that is what keeps
    /// a duplicated parser safe.</b> A member silently dropped from an instance DB is a member the block
    /// writes into storage it does not own.
    /// </summary>
    [Fact]
    public void A_LINE_WITH_NO_TYPE_SEPARATOR_IS_REFUSED()
    {
        var error = Assert.Throws<ArgumentException>(() => IrMemberLineReader.Parse("    Running Bool"));
        Assert.Contains("refuses rather than guessing", error.Message, StringComparison.Ordinal);
    }

    /// <summary>An array element start value is a subelement line, told apart by its leading <c>[</c>.</summary>
    [Fact]
    public void AN_ARRAY_ELEMENT_START_VALUE_IS_READ_AS_A_SUBELEMENT()
    {
        Assert.True(IrMemberLineReader.IsSubelement("          [1] = 16#00"));
        Assert.False(IrMemberLineReader.IsSubelement("    Running : Bool"));

        var (path, value) = IrMemberLineReader.ParseSubelement("          [1] = 16#00");
        Assert.Equal("1", path);
        Assert.Equal("16#00", value);
    }
}
