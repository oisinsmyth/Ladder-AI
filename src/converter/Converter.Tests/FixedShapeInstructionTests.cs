using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Fixed-shape registry instructions — `MB_COMM_LOAD` 2.1 and `MB_MASTER` 2.2 — plus the two
/// general defects a real block surfaced alongside them (2026-08-12).
///
/// <c>Fixtures/FixedShapeModbusTcpBlock.xml</c> IS the grounding export: a genuine TIA V20
/// `WithDefaults` export of a live S7-1200 (classic 1214C) Modbus TCP FC, element for element,
/// with four identifier lines replaced by invented ones per the data boundary — block name, DB
/// name, DB member name and the area pointer's DB number. Nothing structural was touched: the
/// document is 415 lines in both, and the structure is what is under test.
///
/// The three defects it grounds, each of which alone stopped the block from round-tripping:
///
/// 1. **The supported instruction spellings were the wrong ones.** `FlgNetParser` carried
///    `Modbus_Master`/`Modbus_Comm_Load`; TIA emits `MB_MASTER` Version="2.2" and `MB_COMM_LOAD`
///    Version="2.1" here. Both name families are now supported — see FixedShapeInstructions for
///    why the older pair is kept rather than replaced, and why VERSION participates in matching.
/// 2. **An `&lt;OpenCon&gt;` (unconnected port) could not be reduced.** This block leaves 8 of
///    MB_COMM_LOAD's 12 ports and all 4 of MB_MASTER's outputs unwired — normal, common LAD.
/// 3. **A constant value containing spaces was a ONE-WAY conversion.** `to-ir` emitted
///    `constant P#DB99.DBX0.0 BYTE 2 = 21 Any`; `to-xml` on the converter's own output then threw
///    `Malformed sidecar constant line`.
/// </summary>
public class FixedShapeInstructionTests
{
    private const string Fixture = "FixedShapeModbusTcpBlock.xml";

    private static XDocument LoadFixture(string name = Fixture) => XDocument.Load(Path.Combine("Fixtures", name));

    private static FlgNetwork NetworkOf(XDocument document, int index) =>
        BlockSourceParser.Parse(document).CompileUnits[index].Network;

    // The whole to-ir half of the pipeline, matching Program.ConvertToIr's own steps.
    private static (IrBlock Block, List<NetworkSidecar> Sidecars) ToIr(XDocument document)
    {
        var block = BlockSourceParser.Parse(document);
        var networks = new List<IrNetwork>();
        var sidecars = new List<NetworkSidecar>();
        foreach (var compileUnit in block.CompileUnits)
        {
            var reduced = GraphReducer.Reduce(compileUnit.Network, networks.Count + 1, compileUnit.Title ?? string.Empty, compileUnit.UId);
            networks.Add(reduced.Network with { Comment = compileUnit.Comment });
            sidecars.Add(reduced.Sidecar);
        }

        var irBlock = new IrBlock(
            block.RootUId, block.Kind, block.Name, block.Number, block.Language, block.Comment, networks,
            block.StaticMembers, block.TempMembers, block.Title, block.InputMembers, block.OutputMembers,
            block.InOutMembers, block.ConstantMembers, block.SecondaryType, block.MemoryLayout);
        return (irBlock, sidecars);
    }

    private static string ToIrText(XDocument document)
    {
        var (block, sidecars) = ToIr(document);
        return IrSerializer.SerializeBlock(block, sidecars);
    }

    // Serialize-then-parse, exactly as Program.SynthesizeReadableVerified does and for the same
    // reason: BuildBlockXml yields an in-memory tree whose node shape differs from a freshly-parsed
    // one, and XNode.DeepEquals inside the Normalizer is sensitive to that.
    private static XDocument ToXml(string irText)
    {
        var (block, sidecars) = IrParser.ParseBlock(irText);
        return XDocument.Parse(Program.BuildBlockXml(block, sidecars).ToString());
    }

    // ------------------------------------------------- defect 1: the real instruction spellings

    [Fact]
    public void Parse_MbCommLoadAtVersion2Point1_ProducesThePart()
    {
        var part = Assert.Single(NetworkOf(LoadFixture(), 1).Parts, p => p.Name == "MB_COMM_LOAD");

        Assert.Equal(22, part.UId);
        Assert.Equal("2.1", part.Version);
        Assert.Equal(new[] { "MB_COMM_LOAD_DB" }, part.Instance!.ComponentPath);
    }

    [Fact]
    public void Parse_MbMasterAtVersion2Point2_ProducesThePart()
    {
        var part = Assert.Single(NetworkOf(LoadFixture(), 2).Parts, p => p.Name == "MB_MASTER");

        Assert.Equal(27, part.UId);
        Assert.Equal("2.2", part.Version);
        Assert.Equal(new[] { "MB_MASTER_DB" }, part.Instance!.ComponentPath);
    }

    [Fact]
    public void Reduce_MbMaster_BindsEveryPortInTemplateOrder()
    {
        var reduced = GraphReducer.Reduce(NetworkOf(LoadFixture(), 2), 1, string.Empty, "D");

        var statement = Assert.Single(reduced.Network.FixedShapes);
        Assert.Equal("MB_MASTER", statement.Instruction);
        Assert.Equal("MB_MASTER_DB", statement.InstancePath);
        Assert.Equal(
            new[] { "REQ", "MB_ADDR", "MODE", "DATA_ADDR", "DATA_LEN", "DATA_PTR", "DONE", "BUSY", "ERROR", "STATUS" },
            statement.Arguments.Select(a => a.Port));
    }

    /// <summary>
    /// The older `Modbus_Master`/`Modbus_Comm_Load` family is KEPT, not replaced — a separate
    /// instruction family with its own live-import evidence (see FixedShapeInstructions). Adding
    /// the new names must not have cost the old ones.
    /// </summary>
    [Fact]
    public void Parse_LegacyModbusSpellings_StillSupported()
    {
        var legacy = FlgNetParser.Parse(XElement.Load(Path.Combine("Fixtures", "ModbusCommLoadFedByRail.xml")));

        var part = Assert.Single(legacy.Parts, p => p.Name == "Modbus_Comm_Load");
        Assert.Equal("5.0", part.Version);
    }

    // ------------------------------------------------------ defect 1: the version-matching gate

    /// <summary>
    /// The port list is NOT in the export — the converter supplies it per (name, version). So an
    /// unrecognized version is refused rather than templated with another version's ports, which
    /// would import cleanly and misbehave on the controller.
    /// </summary>
    [Fact]
    public void Parse_KnownInstructionAtUnknownVersion_IsRefused()
    {
        var document = LoadFixture();
        document.Descendants().First(e => e.Name.LocalName == "Part" && e.Attribute("Name")?.Value == "MB_MASTER")
            .SetAttributeValue("Version", "9.9");

        var error = Assert.Throws<UnsupportedConstructException>(() => NetworkOf(document, 2));

        Assert.Contains("Unsupported version '9.9'", error.Message);
        Assert.Contains("2.2", error.Message);
    }

    /// <summary>
    /// THE GUARD THAT MATTERS: widening the supported list must not have turned the fail-closed
    /// behaviour into fail-silent. An instruction the converter carries no template for is still a
    /// hard error, not a silent partial conversion.
    /// </summary>
    [Fact]
    public void Parse_UnknownInstruction_StillHardErrors()
    {
        var document = LoadFixture();
        document.Descendants().First(e => e.Name.LocalName == "Part" && e.Attribute("Name")?.Value == "MB_MASTER")
            .SetAttributeValue("Name", "MB_SERVER");

        var error = Assert.Throws<UnsupportedConstructException>(() => NetworkOf(document, 2));

        Assert.Contains("Unsupported instruction 'MB_SERVER'", error.Message);
    }

    /// <summary>
    /// The same guard one level up: a whole-block conversion of an unsupported instruction must
    /// fail, never produce a block with the instruction quietly missing.
    /// </summary>
    [Fact]
    public void ToIr_WithUnknownInstruction_ProducesNoPartialResult()
    {
        var document = LoadFixture();
        document.Descendants().First(e => e.Name.LocalName == "Part" && e.Attribute("Name")?.Value == "MB_COMM_LOAD")
            .SetAttributeValue("Name", "MB_SLAVE");

        Assert.Throws<UnsupportedConstructException>(() => ToIrText(document));
    }

    // ------------------------------------------------------------- defect 2: unconnected ports

    [Fact]
    public void Reduce_UnconnectedPorts_AreBoundAsOpenNotDropped()
    {
        var reduced = GraphReducer.Reduce(NetworkOf(LoadFixture(), 1), 1, string.Empty, "8");

        var statement = Assert.Single(reduced.Network.FixedShapes);
        Assert.Equal("MB_COMM_LOAD", statement.Instruction);

        // 8 inputs open, 3 outputs open, MB_DB the one genuinely wired port.
        Assert.Equal(8, statement.Arguments.Count(a => a.Binding is PortBinding.OpenInput));
        Assert.Equal(3, statement.Arguments.Count(a => a.Binding is PortBinding.OpenOutput));
        var mbDb = Assert.Single(statement.Arguments, a => a.Port == "MB_DB");
        Assert.Equal("MB_MASTER_DB", Assert.IsType<Expr.TagRef>(Assert.IsType<PortBinding.Value>(mbDb.Binding).Expr).Path);
    }

    [Fact]
    public void ReadableIr_ShowsAnUnconnectedPortRatherThanHidingIt()
    {
        var irText = ToIrText(LoadFixture());

        Assert.Contains("REQ := OPEN", irText);
        Assert.Contains("DONE => OPEN", irText);
        Assert.Contains("MB_DB := MB_MASTER_DB", irText);
    }

    /// <summary>
    /// The round-trip requirement stated in the brief: an `&lt;OpenCon&gt;` that converts to IR and
    /// back must still be an `&lt;OpenCon&gt;` — not a dropped port, not an invented dummy operand.
    /// </summary>
    [Fact]
    public void OpenCon_SurvivesTheRoundTrip_WithItsOwnUIdIntact()
    {
        var original = LoadFixture();
        var regenerated = ToXml(ToIrText(original));

        static List<(string Wire, string Open)> OpenConWires(XDocument document) =>
            document.Descendants().Where(e => e.Name.LocalName == "Wire")
                .SelectMany(w => w.Elements().Where(c => c.Name.LocalName == "OpenCon")
                    .Select(c => (Wire: w.Attribute("UId")!.Value, Open: c.Attribute("UId")!.Value)))
                .OrderBy(p => p.Wire, StringComparer.Ordinal).ToList();

        var before = OpenConWires(original);
        Assert.Equal(15, before.Count);
        Assert.Equal(before, OpenConWires(regenerated));
    }

    /// <summary>
    /// An open port names no tag — that is the point of modelling "unconnected" as its own state
    /// rather than as a placeholder operand. A dummy would have leaked into `tagstatus`/`preflight`
    /// as a proposed tag and gated a perfectly valid block.
    /// </summary>
    [Fact]
    public void OpenPorts_ContributeNoTagReferences()
    {
        var (block, _) = ToIr(LoadFixture());

        var paths = block.Networks.SelectMany(TagReferences.AllTagPaths).ToList();

        Assert.DoesNotContain("OPEN", paths);
    }

    /// <summary>
    /// `OPEN` is a bare-word sentinel (matching the `TRUE`/`ENO` precedent), so a tag genuinely
    /// named `OPEN` would be ambiguous. Checked, not assumed away.
    /// </summary>
    [Fact]
    public void TagLiterallyNamedOpen_OnAFixedShapePort_IsRefused()
    {
        var document = LoadFixture();
        document.Descendants().First(e => e.Name.LocalName == "Component" && e.Attribute("Name")?.Value == "MB_MASTER_DB")
            .SetAttributeValue("Name", "OPEN");

        var error = Assert.Throws<UnsupportedConstructException>(() => GraphReducer.Reduce(NetworkOf(document, 1), 1, string.Empty, "8"));

        Assert.Contains("reserved readable-IR token", error.Message);
    }

    // ----------------------------------------------- defect 3: a constant value containing spaces

    [Fact]
    public void SidecarConstantLine_WithSpacesInTheValue_ParsesBack()
    {
        var irText = ToIrText(LoadFixture());
        Assert.Contains("  constant P#DB99.DBX0.0 BYTE 2 = 21 Any\n", irText);

        var (_, sidecars) = IrParser.ParseBlock(irText);

        var constant = Assert.Single(sidecars[0].ConstantUIds, c => c.UId == 21);
        Assert.Equal("P#DB99.DBX0.0 BYTE 2", constant.Value);
        Assert.Equal("Any", constant.ConstantType);
    }

    /// <summary>
    /// Deliberately not an `Any`-only fix. Every constant type whose text can carry a space rides
    /// this one line; a narrow fix would only have moved the defect to the next type. The greedy
    /// value also survives a value containing the field separator itself.
    /// </summary>
    [Theory]
    [InlineData("P#DB99.DBX0.0 BYTE 2", "Any")]
    [InlineData("P#M100.0 BYTE 4", "Pointer")]
    [InlineData("'hello there'", "String")]
    [InlineData("DTL#2026-08-12-15:43:40.9", "DTL")]
    [InlineData("a = b", "String")]
    [InlineData("16#89", "Word")]
    [InlineData("-1", "Int")]
    public void SidecarConstantLine_RoundTripsEveryValueShape(string value, string constantType)
    {
        var irText = ToIrText(LoadFixture())
            .Replace("  constant P#DB99.DBX0.0 BYTE 2 = 21 Any\n", $"  constant {value} = 21 {constantType}\n", StringComparison.Ordinal);

        var (_, sidecars) = IrParser.ParseBlock(irText);

        var constant = Assert.Single(sidecars[0].ConstantUIds, c => c.UId == 21);
        Assert.Equal(value, constant.Value);
        Assert.Equal(constantType, constant.ConstantType);
    }

    /// <summary>
    /// Backward compatibility with every sidecar already in the repo: a space-free value parses
    /// exactly as it did before the regex widened.
    /// </summary>
    [Fact]
    public void SidecarConstantLine_SpaceFreeValue_ParsesUnchanged()
    {
        var irText = ToIrText(LoadFixture());

        var (_, sidecars) = IrParser.ParseBlock(irText);

        var constant = Assert.Single(sidecars[0].ConstantUIds, c => c.UId == 22);
        Assert.Equal("2", constant.Value);
        Assert.Equal("UDInt", constant.ConstantType);
    }

    /// <summary>
    /// An area pointer must read back as a LITERAL, not a tag reference. It converted to XML
    /// correctly either way (the sidecar says which), but as a TagRef it polluted every tool that
    /// walks tag references — `tagstatus` would have called `P#DB99.DBX0.0 BYTE 2` a proposed tag.
    /// </summary>
    [Fact]
    public void AreaPointer_ReadsBackAsALiteralNotATag()
    {
        var (block, _) = IrParser.ParseBlock(ToIrText(LoadFixture()));

        var moveBlk = Assert.Single(block.Networks[0].MoveBlkVariants);
        var src = Assert.IsType<Expr.Literal>(moveBlk.Src);
        Assert.Equal("P#DB99.DBX0.0 BYTE 2", src.Value);
        Assert.DoesNotContain("P#DB99.DBX0.0 BYTE 2", block.Networks.SelectMany(TagReferences.AllTagPaths));
    }

    // ------------------------------------------------------------------- the whole confirm loop

    /// <summary>
    /// The acceptance test the brief asks for, run offline: `to-ir` → `to-xml` → compare the
    /// regenerated XML against the real export it came from. Same judgement `converter compare`
    /// applies at the CLI.
    /// </summary>
    [Fact]
    public void WholeBlock_RoundTripsBackToTheOriginalExport()
    {
        var original = LoadFixture();

        var regenerated = ToXml(ToIrText(original));

        Assert.True(Normalizer.AreSemanticallyEquivalent(original, regenerated));
    }

    [Fact]
    public void WholeBlock_IrTextIsSelfStable()
    {
        var first = ToIrText(LoadFixture());

        var second = ToIrText(ToXml(first));

        Assert.Equal(first, second);
    }

    /// <summary>
    /// Conversion scope is not synthesis scope: this family converts in both directions with a
    /// real sidecar and hard-errors on the derive/--synthesize path, exactly like Modbus_*.
    /// </summary>
    [Fact]
    public void SidecarSynthesis_OfAFixedShapeInstruction_IsRefusedByName()
    {
        var (block, _) = ToIr(LoadFixture());

        var error = Assert.Throws<UnsupportedSynthesisConstructException>(
            () => SidecarSynthesizer.SynthesizeBlock(block, null, null));

        Assert.Contains("FixedShapes", error.Message);
    }

    // ---------------------------------------------------------------------------- the registry

    [Fact]
    public void Registry_CarriesPortDirectionsBecauseTheExportDoesNot()
    {
        var template = FixedShapeInstructions.Require("MB_COMM_LOAD", "2.1");

        Assert.Equal(PortDirection.Input, template.PortNamed("MB_DB")!.Direction);
        Assert.Equal(PortDirection.Output, template.PortNamed("STATUS")!.Direction);
        Assert.Null(template.PortNamed("BUSY"));
        Assert.Null(FixedShapeInstructions.Lookup("MB_COMM_LOAD", "2.2"));
    }

    /// <summary>
    /// The two name families are registered separately and neither aliases the other: their port
    /// lists coincide today and nothing guarantees they will at the next version of either.
    /// </summary>
    [Fact]
    public void Registry_DoesNotTreatTheLegacyNamesAsItsOwn()
    {
        Assert.False(FixedShapeInstructions.IsFixedShapePartName("Modbus_Master"));
        Assert.False(FixedShapeInstructions.IsFixedShapePartName("Modbus_Comm_Load"));
        Assert.True(FixedShapeInstructions.IsFixedShapePartName("MB_MASTER"));
    }
}
