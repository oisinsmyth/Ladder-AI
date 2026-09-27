using System.Linq;
using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Two box shapes grounded on a live-run export (2026-09-27; fixtures invented):
/// <list type="bullet">
/// <item><c>Round</c> — Convert's exact shape (en/in/out, DisabledENO, SrcType + DestType), ENO-chained after
/// a MUL. A kind of CONVERT: <c>ROUND(EN := &lt;expr-or-ENO&gt;, IN := &lt;expr&gt;) =&gt; &lt;dest&gt;</c>.</item>
/// <item>A MOVE cascade: each MOVE's <c>en</c> wired from the previous MOVE's <c>eno</c> —
/// <c>MOVE(EN := ENO, IN := ...) =&gt; ...</c>, the reserved word every other box already uses.</item>
/// </list>
/// </summary>
public class RoundAndMoveEnoTests
{
    // Two bare <FlgNet>s, compared through the Normalizer's single-document canonical form (UIds replaced
    // by content keys, order-insensitive where TIA's order is not meaningful).
    private static bool SameNetwork(XDocument a, XDocument b) =>
        Normalizer.Strip(a.Root!).ToString() == Normalizer.Strip(b.Root!).ToString();

    private const string Ns = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5";

    private const string RoundXml = """
        <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
          <Parts>
            <Access Scope="GlobalVariable" UId="21"><Symbol><Component Name="DemoData" /><Component Name="Seconds" /></Symbol></Access>
            <Access Scope="LiteralConstant" UId="22"><Constant><ConstantType>Real</ConstantType><ConstantValue>1000.0</ConstantValue></Constant></Access>
            <Access Scope="GlobalVariable" UId="23"><Symbol><Component Name="DemoData" /><Component Name="Scratch" /></Symbol></Access>
            <Access Scope="GlobalVariable" UId="24"><Symbol><Component Name="DemoData" /><Component Name="Scratch" /></Symbol></Access>
            <Access Scope="GlobalVariable" UId="25"><Symbol><Component Name="DemoData" /><Component Name="Millis" /></Symbol></Access>
            <Part Name="Mul" UId="31" DisabledENO="true">
              <TemplateValue Name="Card" Type="Cardinality">2</TemplateValue>
              <AutomaticTyped Name="SrcType" />
            </Part>
            <Part Name="Round" UId="32" DisabledENO="true">
              <TemplateValue Name="SrcType" Type="Type">Real</TemplateValue>
              <TemplateValue Name="DestType" Type="Type">DInt</TemplateValue>
            </Part>
          </Parts>
          <Wires>
            <Wire UId="41"><Powerrail /><NameCon UId="31" Name="en" /></Wire>
            <Wire UId="42"><IdentCon UId="21" /><NameCon UId="31" Name="in1" /></Wire>
            <Wire UId="43"><IdentCon UId="22" /><NameCon UId="31" Name="in2" /></Wire>
            <Wire UId="44"><NameCon UId="31" Name="eno" /><NameCon UId="32" Name="en" /></Wire>
            <Wire UId="45"><NameCon UId="31" Name="out" /><IdentCon UId="23" /></Wire>
            <Wire UId="46"><IdentCon UId="24" /><NameCon UId="32" Name="in" /></Wire>
            <Wire UId="47"><NameCon UId="32" Name="out" /><IdentCon UId="25" /></Wire>
          </Wires>
        </FlgNet>
        """;

    private const string MoveCascadeXml = """
        <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
          <Parts>
            <Access Scope="GlobalVariable" UId="21"><Symbol><Component Name="DemoIn" /><Component Name="A" /></Symbol></Access>
            <Access Scope="GlobalVariable" UId="22"><Symbol><Component Name="DemoOut" /><Component Name="A" /></Symbol></Access>
            <Access Scope="GlobalVariable" UId="23"><Symbol><Component Name="DemoIn" /><Component Name="B" /></Symbol></Access>
            <Access Scope="GlobalVariable" UId="24"><Symbol><Component Name="DemoOut" /><Component Name="B" /></Symbol></Access>
            <Part Name="Move" UId="31" DisabledENO="true"><TemplateValue Name="Card" Type="Cardinality">1</TemplateValue></Part>
            <Part Name="Move" UId="32" DisabledENO="true"><TemplateValue Name="Card" Type="Cardinality">1</TemplateValue></Part>
          </Parts>
          <Wires>
            <Wire UId="41"><Powerrail /><NameCon UId="31" Name="en" /></Wire>
            <Wire UId="42"><IdentCon UId="21" /><NameCon UId="31" Name="in" /></Wire>
            <Wire UId="43"><NameCon UId="31" Name="eno" /><NameCon UId="32" Name="en" /></Wire>
            <Wire UId="44"><NameCon UId="31" Name="out1" /><IdentCon UId="22" /></Wire>
            <Wire UId="45"><IdentCon UId="23" /><NameCon UId="32" Name="in" /></Wire>
            <Wire UId="46"><NameCon UId="32" Name="out1" /><IdentCon UId="24" /></Wire>
          </Wires>
        </FlgNet>
        """;

    private static ReducedNetwork Reduce(string xml) =>
        GraphReducer.Reduce(FlgNetParser.Parse(XElement.Parse(xml)), networkNumber: 1, title: "T", compileUnitUId: "3");

    private static void AssertRoundTrips(string xml, string expectedReadable)
    {
        var reduced = Reduce(xml);
        Assert.Equal(expectedReadable, IrSerializer.SerializeNetworkOnly(reduced.Network));

        var block = new IrBlock("0", "FC", "DemoBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });
        var (parsed, sidecars) = IrParser.ParseBlock(text);
        Assert.Equal(text, IrSerializer.SerializeBlock(parsed, sidecars));

        var source = new XDocument(XElement.Parse(xml));
        Assert.True(SameNetwork(source, new XDocument(FlgNetWriter.Write(FlgNetBuilder.Build(parsed.Networks[0], sidecars[0])))));
        var synthesized = SidecarSynthesizer.Synthesize(parsed.Networks[0]);
        Assert.True(SameNetwork(source, new XDocument(FlgNetWriter.Write(FlgNetBuilder.Build(parsed.Networks[0], synthesized)))));
    }

    [Fact]
    public void Round_ReadsAndWritesBack_BothPaths()
    {
        AssertRoundTrips(RoundXml,
            "NETWORK 1 \"T\"\n" +
            "  MUL(EN := TRUE, IN1 := DemoData.Seconds, IN2 := 1000.0) => DemoData.Scratch\n" +
            "  ROUND(EN := ENO, IN := DemoData.Scratch) => DemoData.Millis\n");
    }

    [Fact]
    public void Round_KeepsItsPartNameAndTypes()
    {
        var reduced = Reduce(RoundXml);
        var flgNet = FlgNetWriter.Write(FlgNetBuilder.Build(reduced.Network, reduced.Sidecar));

        var round = flgNet.Descendants(XName.Get("Part", Ns)).Single(p => p.Attribute("Name")!.Value == "Round");
        Assert.Equal(new[] { "Real", "DInt" }, round.Elements().Select(e => e.Value));
    }

    [Fact]
    public void MoveCascade_ReadsAndWritesBack_BothPaths()
    {
        AssertRoundTrips(MoveCascadeXml,
            "NETWORK 1 \"T\"\n" +
            "  MOVE(EN := TRUE, IN := DemoIn.A) => DemoOut.A\n" +
            "  MOVE(EN := ENO, IN := DemoIn.B) => DemoOut.B\n");
    }

    [Fact]
    public void MoveEno_WithNoPrecedingMove_IsRefusedBySynthesis()
    {
        var network = IrParser.ParseNetworkOnly("NETWORK 1 \"T\"\n  MOVE(EN := ENO, IN := A) => B\n");

        Assert.Throws<UnsupportedSynthesisConstructException>(() => SidecarSynthesizer.Synthesize(network));
    }
}
