using System;
using System.Linq;
using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

// ADR-0010: anything the AI must change lives in the READABLE IR. A stored sidecar used to be the only
// thing FlgNetBuilder read — the readable statements were consulted for their COUNTS and nothing else —
// so an operand edited in the readable text of a sidecar'd block was silently written back as the
// ORIGINAL operand: to-xml exit 0, compare EQUIVALENT to the unedited block, every edit gone. These tests
// pin the three edit kinds that were lost (a box's destination, a comparator input, a contact operand),
// and that an unedited sidecar still wins byte for byte.
public class SidecarReadableAuthorityTests
{
    private static readonly TagTypeRegistry Types = TagTypeRegistry.FromSources(
        Array.Empty<DbSource>(), Array.Empty<PlcTypeSource>(),
        new[]
        {
            new PlcTagSource("1", "Span", "Real", "%MD0", true, true, true, null),
            new PlcTagSource("2", "Offset", "Real", "%MD4", true, true, true, null),
            new PlcTagSource("3", "LimitHigh", "Real", "%MD8", true, true, true, null),
            new PlcTagSource("4", "LimitSpare", "Real", "%MD12", true, true, true, null),
            new PlcTagSource("5", "EnableA", "Bool", "%M20.0", true, true, true, null),
            new PlcTagSource("6", "EnableB", "Bool", "%M20.1", true, true, true, null),
            new PlcTagSource("7", "Reading", "Real", "%MD24", true, true, true, null),
            new PlcTagSource("8", "LimitLow", "Real", "%MD28", true, true, true, null),
            new PlcTagSource("9", "Healthy", "Bool", "%M32.0", true, true, true, null),
        });

    // The fixture with its DemoData./DemoIn./DemoOut. prefixes dropped, so every operand is a plain PLC tag
    // the registry above types.
    private static string BlockIrText()
    {
        var xml = System.IO.File.ReadAllText(System.IO.Path.Combine("Fixtures", "SidecarEditTarget.xml"))
            .Replace("<Component Name=\"DemoData\" />", string.Empty)
            .Replace("<Component Name=\"DemoIn\" />", string.Empty)
            .Replace("<Component Name=\"DemoOut\" />", string.Empty);
        var reduced = GraphReducer.Reduce(FlgNetParser.Parse(XElement.Parse(xml)), networkNumber: 1, title: "Limits", compileUnitUId: "3");
        var block = new IrBlock("0", "FC", "DemoBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        return IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });
    }

    // Edits the readable half only; the SIDECAR is left exactly as to-ir wrote it.
    private static string EditReadable(string text, string from, string to)
    {
        var split = text.IndexOf("\nSIDECAR\n", StringComparison.Ordinal);
        var readable = text[..split];
        Assert.Contains(from, readable);
        return readable.Replace(from, to) + text[split..];
    }

    private static string ReadableOf(XDocument xml)
    {
        var flgNet = xml.Descendants().Single(e => e.Name.LocalName == "FlgNet");
        var reduced = GraphReducer.Reduce(FlgNetParser.Parse(flgNet), networkNumber: 1, title: "Limits", compileUnitUId: "3");
        return IrSerializer.SerializeNetworkOnly(reduced.Network);
    }

    private static XDocument ToXml(string text) => Program.BuildXmlFromIrText(text, synthesize: false, callees: null, tagTypes: Types);

    [Fact]
    public void TheFixtureReadsAsExpected()
    {
        var text = BlockIrText();

        Assert.Contains("  SUB(EN := TRUE, IN1 := Span, IN2 := Offset) => LimitHigh\n", text);
        Assert.Contains("  COIL Healthy := EnableA AND Reading > LimitLow\n", text);
    }

    [Theory]
    [InlineData("=> LimitHigh", "=> LimitSpare")]                            // a box's destination
    [InlineData("Reading > LimitLow", "Reading > LimitHigh")]                 // a comparator input
    [InlineData("COIL Healthy := EnableA", "COIL Healthy := EnableB")]        // a contact operand
    public void AReadableOperandEdit_ReachesTheXml(string from, string to)
    {
        var edited = EditReadable(BlockIrText(), from, to);

        var written = ReadableOf(ToXml(edited));

        Assert.Contains(to, written);
        Assert.DoesNotContain(from, written);
    }

    [Fact]
    public void AllThreeEditsTogether_ReachTheXml()
    {
        var edited = EditReadable(BlockIrText(), "=> LimitHigh", "=> LimitSpare");
        edited = EditReadable(edited, "Reading > LimitLow", "Reading > LimitHigh");
        edited = EditReadable(edited, "COIL Healthy := EnableA", "COIL Healthy := EnableB");

        Assert.Equal(
            "NETWORK 1 \"Limits\"\n" +
            "  SUB(EN := TRUE, IN1 := Span, IN2 := Offset) => LimitSpare\n" +
            "  COIL Healthy := EnableB AND Reading > LimitHigh\n",
            ReadableOf(ToXml(edited)));
    }

    // The unedited path is untouched: the stored sidecar is used as-is, source UIds and all.
    [Fact]
    public void AnUneditedSidecar_StillWinsByteForByte()
    {
        var text = BlockIrText();
        var (block, sidecars) = IrParser.ParseBlock(text);

        Assert.Equal(
            Program.BuildBlockXml(block, sidecars).ToString(),
            ToXml(text).ToString());
    }

    // Rung order is readable IR too: moving a statement in an interleaved network is an edit, and it wins.
    [Fact]
    public void AReorderedStatement_ReachesTheXml()
    {
        var reduced = GraphReducer.Reduce(
            FlgNetParser.Parse(XElement.Load(System.IO.Path.Combine("Fixtures", "RungOrderTimerBetweenCoils.xml"))),
            networkNumber: 1, title: "Rungs", compileUnitUId: "3");
        var block = new IrBlock("0", "FC", "DemoBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });
        var edited = EditReadable(text, "  COIL DemoOut.Z := DemoIn.C\n", string.Empty);
        edited = EditReadable(edited, "NETWORK 1 \"Rungs\"\n", "NETWORK 1 \"Rungs\"\n  COIL DemoOut.Z := DemoIn.C\n");

        var xml = Program.BuildXmlFromIrText(edited, false, null, null);

        var flgNet = xml.Descendants().Single(e => e.Name.LocalName == "FlgNet");
        var written = IrSerializer.SerializeNetworkOnly(
            GraphReducer.Reduce(FlgNetParser.Parse(flgNet), 1, "Rungs", "3").Network);
        Assert.StartsWith("NETWORK 1 \"Rungs\"\n  COIL DemoOut.Z := DemoIn.C\n  COIL DemoOut.X := DemoIn.A\n", written);
    }

    // The one case where the sidecar's order stands: readable IR in plain kind order states no rung order.
    // That is the layout every to-ir wrote before statement order was expressible, so every older
    // sidecar'd file is written in it; its stored order is kept, byte for byte (ir/SPEC.md, "Sidecar").
    [Fact]
    public void KindGroupedReadableIr_KeepsTheSidecarsRungOrder()
    {
        var reduced = GraphReducer.Reduce(
            FlgNetParser.Parse(XElement.Load(System.IO.Path.Combine("Fixtures", "RungOrderTimerBetweenCoils.xml"))),
            networkNumber: 1, title: "Rungs", compileUnitUId: "3");
        var block = new IrBlock("0", "FC", "DemoBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });
        var grouped = EditReadable(text, "  TON(DemoTimer, IN := DemoIn.B, PT := T#2S)\n", string.Empty);
        grouped = EditReadable(grouped, "NETWORK 1 \"Rungs\"\n", "NETWORK 1 \"Rungs\"\n  TON(DemoTimer, IN := DemoIn.B, PT := T#2S)\n");
        var (parsed, sidecars) = IrParser.ParseBlock(text);

        Assert.Equal(
            Program.BuildBlockXml(parsed, sidecars).ToString(),
            Program.BuildXmlFromIrText(grouped, false, null, null).ToString());
    }

    // When the edited network cannot be re-derived from its readable text (here: a LIMIT, which synthesis
    // does not support), to-xml refuses and names the network — it never falls back to the stale sidecar.
    [Fact]
    public void AnEditThatCannotBeRederived_IsRefused_NeverSilentlyDropped()
    {
        var reduced = GraphReducer.Reduce(
            FlgNetParser.Parse(XElement.Load(System.IO.Path.Combine("Fixtures", "LimitFedByRail.xml"))),
            networkNumber: 4, title: "Clamp value", compileUnitUId: "62");
        var block = new IrBlock("0", "FC", "DemoBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });
        var edited = EditReadable(text, "=> ClampedValue", "=> OtherValue");

        var ex = Assert.Throws<IrFormatException>(() => Program.BuildXmlFromIrText(edited, false, null, null));

        Assert.Contains("Network 4", ex.Message);
        Assert.Contains("OtherValue", ex.Message);
    }
}
