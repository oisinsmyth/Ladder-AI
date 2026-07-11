using System.Xml.Linq;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

public class UnknownElementHardErrorTests
{
    // TON support landed 2026-07-11 (S1 item 8) — see TonTests.cs for its coverage, which now
    // uses this file's former WithTon.xml fixture as a positive round-trip case instead.

    [Fact]
    public void Parse_UnrecognizedAccessScope_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="LiteralConstant" UId="1">
                  <Symbol>
                    <Component Name="X" />
                  </Symbol>
                </Access>
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(element));
        Assert.Contains("LiteralConstant", ex.Message);
    }

    [Fact]
    public void Parse_TrulyUnrecognizedElement_ThrowsFormatException()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <SomethingWeirdAndUnknown UId="1" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        Assert.Throws<SimaticMlFormatException>(() => FlgNetParser.Parse(element));
    }
}
