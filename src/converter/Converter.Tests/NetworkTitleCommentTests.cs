using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Network `Title` (S1 item 16, 2026-07-12) — picked up after `SCoil`/`RCoil` surfaced a real,
/// systematic gap while attempting a whole-block round-trip of `FC PlantAutoControl`: every one of
/// its 20 real networks carries a non-empty `Title` (a separate `MultilingualText
/// [CompositionName="Title"]`), which this converter previously hard-errored on.
///
/// A real design correction, not just a new feature: the IR's own `NETWORK &lt;n&gt; "&lt;title&gt;"`
/// line was, before this item, actually sourced from the network's `Comment` field — a mismatch
/// between the format's own vocabulary (`ir/SPEC.md`'s original sketch already called it
/// "title") and what was actually built, unnoticed until real data (`PlantAutoControl`) finally had
/// non-trivial content in both fields at once (Title populated, Comment empty — the opposite of
/// what every network grounded earlier this session showed, since Comment was always empty
/// everywhere). Confirmed with the project owner before changing: the `NETWORK` line's own
/// quoted label now carries `Title` (matching its own name and how engineers actually use it in
/// the TIA LAD editor); a new, separate, optional `COMMENT "..."` line (mirroring the existing
/// block-level `COMMENT` line) carries the rarely-used `Comment` field instead.
/// </summary>
public class NetworkTitleCommentTests
{
    private static XDocument LoadFixture(string name) => XDocument.Load(Path.Combine("Fixtures", name));

    [Fact]
    public void Parse_NetworkWithTitleAndComment_ReadsBothSeparately()
    {
        var block = BlockSourceParser.Parse(LoadFixture("SanitizeSource.xml"));

        var compileUnit = block.CompileUnits[0];
        Assert.Equal("Real network comment.", compileUnit.Comment);
        Assert.Equal("Real network title mentioning RealSite.", compileUnit.Title);
    }

    [Fact]
    public void SerializeNetworkOnly_TitleAndComment_TitleOnHeaderCommentOnOwnLine()
    {
        var network = new IrNetwork(
            1,
            "Incline Feed Conveyor Auto Control",
            new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) },
            Comment: "A rarely-used network comment.");

        var text = IrSerializer.SerializeNetworkOnly(network);

        Assert.Equal(
            "NETWORK 1 \"Incline Feed Conveyor Auto Control\"\n" +
            "  COMMENT \"A rarely-used network comment.\"\n" +
            "  COIL Output1 := Sensor1\n",
            text);
    }

    [Fact]
    public void SerializeNetworkOnly_EmptyNetworkWithComment_CommentStillShown()
    {
        // Comment/Title both live on a source ObjectList sibling of NetworkSource, independent
        // of whether NetworkSource itself has content — confirmed real for Title already (an
        // [empty] network's own Title survives, see IrSerializer.SerializeNetwork); Comment gets
        // the same treatment for the same reason, not yet grounded on a real empty+commented
        // network specifically but handled identically on principle.
        var network = new IrNetwork(2, "Placeholder", Array.Empty<CoilAssignment>(), Comment: "Still here.");

        var text = IrSerializer.SerializeNetworkOnly(network);

        Assert.Equal(
            "NETWORK 2 \"Placeholder\" [empty]\n" +
            "  COMMENT \"Still here.\"\n",
            text);
    }

    [Fact]
    public void ParseNetworkOnly_TitleAndComment_RoundTripsBothFields()
    {
        var text =
            "NETWORK 1 \"Incline Feed Conveyor Auto Control\"\n" +
            "  COMMENT \"A rarely-used network comment.\"\n" +
            "  COIL Output1 := Sensor1\n";

        var network = IrParser.ParseNetworkOnly(text);

        Assert.Equal("Incline Feed Conveyor Auto Control", network.Title);
        Assert.Equal("A rarely-used network comment.", network.Comment);

        var reserialized = IrSerializer.SerializeNetworkOnly(network);
        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void ParseNetworkOnly_NoCommentLine_CommentIsNull()
    {
        var text = "NETWORK 1 \"A title, no comment\"\n  COIL Output1 := Sensor1\n";

        var network = IrParser.ParseNetworkOnly(text);

        Assert.Null(network.Comment);
    }

    [Fact]
    public void FullPipeline_BlockSourceThroughIrAndBack_PreservesTitleAndCommentSeparately()
    {
        var original = BlockSourceParser.Parse(LoadFixture("SanitizeSource.xml"));
        var compileUnit = original.CompileUnits[0];

        // Mirrors Program.cs's own ConvertToIr wiring exactly: Title drives the reduce-time
        // title, Comment is threaded through afterward via `with`.
        var reduced = GraphReducer.Reduce(compileUnit.Network, 1, compileUnit.Title ?? string.Empty, compileUnit.UId);
        var network = reduced.Network with { Comment = compileUnit.Comment };

        var irBlock = new IrBlock(original.RootUId, original.Kind, original.Name, original.Number, original.Language, original.Comment, new[] { network });
        var irText = IrSerializer.SerializeBlock(irBlock, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(irText);
        Assert.Equal("Real network title mentioning RealSite.", parsedBlock.Networks[0].Title);
        Assert.Equal("Real network comment.", parsedBlock.Networks[0].Comment);

        // And mirrors Program.cs's own ConvertToXml wiring: rebuild the FlgNetwork, then write
        // the whole block back out, confirming Title/Comment land in the correct, separate
        // MultilingualText elements on the way back to SimaticML.
        var rebuiltNetwork = FlgNetBuilder.Build(parsedBlock.Networks[0], parsedSidecars[0]);
        var rebuiltTitle = string.IsNullOrEmpty(parsedBlock.Networks[0].Title) ? null : parsedBlock.Networks[0].Title;
        var blockSource = new BlockSource(parsedBlock.RootUId, parsedBlock.Kind, parsedBlock.Name, parsedBlock.Number, parsedBlock.Language, parsedBlock.Comment, Array.Empty<CompileUnitSource>());
        var xml = BlockSourceWriter.Write(
            blockSource,
            new[] { rebuiltNetwork },
            new[] { parsedSidecars[0].CompileUnitUId },
            new[] { rebuiltTitle },
            new[] { parsedBlock.Networks[0].Comment });

        var reparsed = BlockSourceParser.Parse(xml);
        Assert.Equal("Real network title mentioning RealSite.", reparsed.CompileUnits[0].Title);
        Assert.Equal("Real network comment.", reparsed.CompileUnits[0].Comment);
    }

    [Fact]
    public void Parse_BlockLevelNonEmptyTitle_ReadsItInsteadOfThrowing()
    {
        // Block-level Title was originally assumed always-empty (S1 item 16 only grounded
        // network-level Title as real) — confirmed real after all, 2026-07-12 (S1 item 17, two
        // of FC PlantAutoControl's own dependency FBs, MotorVSDSystem/AirStar, both titled "VSD Motor").
        // This repurposes what was originally a hard-error test into a positive one, same
        // pattern already used for TON's direct-Q-wiring and OR-merge's nested-branch cases.
        var xml = XDocument.Parse("""
            <Document>
              <Engineering version="V20" />
              <SW.Blocks.FC ID="0">
                <AttributeList>
                  <Name>SomeBlock</Name>
                  <Namespace />
                  <Number>1</Number>
                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
                </AttributeList>
                <ObjectList>
                  <MultilingualText ID="1" CompositionName="Title">
                    <ObjectList>
                      <MultilingualTextItem ID="2" CompositionName="Items">
                        <AttributeList>
                          <Culture>en-US</Culture>
                          <Text>A block-level title</Text>
                        </AttributeList>
                      </MultilingualTextItem>
                    </ObjectList>
                  </MultilingualText>
                  <SW.Blocks.CompileUnit ID="3" CompositionName="CompileUnits">
                    <AttributeList>
                      <NetworkSource />
                      <ProgrammingLanguage>LAD</ProgrammingLanguage>
                    </AttributeList>
                  </SW.Blocks.CompileUnit>
                </ObjectList>
              </SW.Blocks.FC>
            </Document>
            """);

        var block = BlockSourceParser.Parse(xml);

        Assert.Equal("A block-level title", block.Title);
    }

    [Fact]
    public void SerializeBlock_WithTitle_ProducesTitleThenCommentLines()
    {
        var block = new IrBlock("0", "FB", "VSDMotor", 5, "LAD", "A block comment.", new[]
        {
            new IrNetwork(1, "Network one", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) }),
        }, Title: "VSD Motor");

        var text = IrSerializer.SerializeBlock(block, new[]
        {
            new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()),
        });

        Assert.StartsWith(
            "BLOCK FB VSDMotor\nROOTID 0\nNUMBER 5\nLANGUAGE LAD\nTITLE \"VSD Motor\"\nCOMMENT \"A block comment.\"\n",
            text);
    }

    [Fact]
    public void ParseBlock_WithTitleAndComment_RoundTripsBothFields()
    {
        var block = new IrBlock("0", "FB", "VSDMotor", 5, "LAD", "A block comment.", new[]
        {
            new IrNetwork(1, "Network one", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) }),
        }, Title: "VSD Motor");
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var text = IrSerializer.SerializeBlock(block, new[] { sidecar });

        var (parsedBlock, _) = IrParser.ParseBlock(text);

        Assert.Equal("VSD Motor", parsedBlock.Title);
        Assert.Equal("A block comment.", parsedBlock.Comment);

        var reserialized = IrSerializer.SerializeBlock(parsedBlock, new[] { sidecar });
        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void RoundTrip_BlockLevelTitle_SurvivesParseWriteParse()
    {
        var original = new BlockSource("0", "FB", "VSDMotor", 5, "LAD", null, Array.Empty<CompileUnitSource>(), Title: "VSD Motor");
        var emptyNetwork = new FlgNetwork(Array.Empty<AccessNode>(), Array.Empty<PartNode>(), Array.Empty<WireNode>());

        var xml = BlockSourceWriter.Write(original, new[] { emptyNetwork }, new[] { "3" }, new string?[] { null }, new string?[] { null });
        var reparsed = BlockSourceParser.Parse(xml);

        Assert.Equal("VSD Motor", reparsed.Title);
    }

    // The scenario S3 (comment generation) actually needs, and which none of the tests above
    // cover: every existing test is pure parse, fresh construction, or an unchanged round-trip.
    // These start from a parsed *existing* value and change it — proving the write path handles
    // an edit, not just preservation.

    [Fact]
    public void SerializeNetworkOnly_TitleChangedFromExisting_NewTitleWinsOldTitleGone()
    {
        var originalText = "NETWORK 1 \"Old Title\"\n  COIL Output1 := Sensor1\n";
        var parsed = IrParser.ParseNetworkOnly(originalText);
        Assert.Equal("Old Title", parsed.Title);

        var edited = parsed with { Title = "New Title" };
        var newText = IrSerializer.SerializeNetworkOnly(edited);

        Assert.Contains("\"New Title\"", newText);
        Assert.DoesNotContain("Old Title", newText);

        var reparsed = IrParser.ParseNetworkOnly(newText);
        Assert.Equal("New Title", reparsed.Title);
    }

    [Fact]
    public void SerializeBlock_TitleChangedFromExisting_NewTitleWinsOldTitleGone()
    {
        var block = new IrBlock("0", "FB", "VSDMotor", 5, "LAD", null, new[]
        {
            new IrNetwork(1, "Network one", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) }),
        }, Title: "Old Block Title");
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var originalText = IrSerializer.SerializeBlock(block, new[] { sidecar });
        var (parsed, sidecars) = IrParser.ParseBlock(originalText);
        Assert.Equal("Old Block Title", parsed.Title);

        var edited = parsed with { Title = "New Block Title" };
        var newText = IrSerializer.SerializeBlock(edited, sidecars);

        Assert.Contains("TITLE \"New Block Title\"", newText);
        Assert.DoesNotContain("Old Block Title", newText);

        var (reparsed, _) = IrParser.ParseBlock(newText);
        Assert.Equal("New Block Title", reparsed.Title);
    }

    // EMBEDDED NEWLINES — inverted 2026-08-12, deliberately. These two tests asserted a HARD ERROR,
    // on reasoning that was half right: the whole .ir document is split on '\n' before any
    // quoted-string parsing runs, so a RAW newline would corrupt the format. What was missing was
    // the other half — no escape sequence existed, so the guard was a permanent refusal rather than
    // a guard, and a real TIA V20 export settled the question by containing one: an S7-1200 Modbus
    // TCP FB whose first network's comment is three lines of engineer's notes. `to-ir` refused the
    // WHOLE BLOCK over its documentation, which under the "no IR the AI cannot change" ruling makes
    // that block permanently unmodifiable for a reason that is presentation, not logic.
    //
    // The invariant the old tests were protecting is unchanged and is now asserted directly: the
    // serialized form stays ONE LINE, and the value survives the round trip intact.

    [Fact]
    public void SerializeNetworkOnly_TitleContainsNewline_EscapesAndStaysOneLine()
    {
        var network = new IrNetwork(1, "Multi\nLine Title", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) });

        var text = IrSerializer.SerializeNetworkOnly(network);

        Assert.Contains("NETWORK 1 \"Multi\\nLine Title\"", text);
        // The point of the escape: the header is still a single physical line.
        Assert.Equal("NETWORK 1 \"Multi\\nLine Title\"", text.Split('\n')[0]);
        Assert.Equal("Multi\nLine Title", IrParser.ParseNetworkOnly(text).Title);
    }

    [Fact]
    public void SerializeBlock_CommentContainsCarriageReturn_RoundTripsThroughTheEscape()
    {
        var block = new IrBlock("0", "FB", "VSDMotor", 5, "LAD", "Two\r\nLine\rComment", new[]
        {
            new IrNetwork(1, "Network one", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());

        var text = IrSerializer.SerializeBlock(block, new[] { sidecar });

        Assert.Contains("COMMENT \"Two\\r\\nLine\\rComment\"", text);
        var (reparsed, _) = IrParser.ParseBlock(text);
        Assert.Equal("Two\r\nLine\rComment", reparsed.Comment);
    }

    // The escape must not have become lossy in the other direction either: a backslash that is
    // genuinely part of the text still survives, and an escape sequence the format does not define
    // is passed through verbatim rather than silently rewritten (the behaviour of the naive Replace
    // pair this superseded — existing .ir comment text must keep reading the same way).
    [Fact]
    public void QuotedString_BackslashesAndUndefinedEscapes_RoundTripUnchanged()
    {
        var block = new IrBlock("0", "FB", "VSDMotor", 5, "LAD", @"path C:\temp, tab-ish \t, quote """, new[]
        {
            new IrNetwork(1, "Network one", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());

        var (reparsed, _) = IrParser.ParseBlock(IrSerializer.SerializeBlock(block, new[] { sidecar }));

        Assert.Equal(@"path C:\temp, tab-ish \t, quote """, reparsed.Comment);
    }
}
