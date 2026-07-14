using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `MOVE_BLK_VARIANT` — block-move-with-array-indexing instruction (Phase 2 Tier 5, 2026-07-14).
/// Grounded against a real export sweep of `JOB9002`'s full block inventory (`FC MoveData`/
/// `FC VSDDataSequence`, 4 identical instances, fully wired — an earlier pass had wrongly
/// concluded only `en` was ever connected, from a `head_limit`-truncated grep; the full wire list,
/// read directly, shows otherwise). `Version="1.2"`, no `DisabledENO`, no other attributes or
/// children at all — the simplest possible Part-level shape. All its complexity lives in the
/// wiring: `SRC`/`COUNT`/`SRC_INDEX`/`DEST_INDEX` (plain tag-or-literal inputs) and `Ret_Val`/
/// `DEST` (two separate plain-tag outputs, mixed-case exactly as the real source has them) — the
/// first instruction this converter models with two destination writes instead of one.
/// </summary>
public class MoveBlkVariantTests
{
    private static FlgNetwork LoadFixture(string name) =>
        FlgNetParser.Parse(XElement.Load(Path.Combine("Fixtures", name)));

    [Fact]
    public void Parse_MoveBlkVariantFedByRail_ProducesMoveBlkVariantPart()
    {
        var network = LoadFixture("MoveBlkVariantFedByRail.xml");

        var part = Assert.Single(network.Parts, p => p.Name == "MOVE_BLK_VARIANT");
        Assert.Equal(27, part.UId);
        Assert.Equal("1.2", part.Version);
    }

    [Fact]
    public void Reduce_MoveBlkVariantFedByRail_EnIsRailAndAllOperandsResolve()
    {
        var network = LoadFixture("MoveBlkVariantFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Copy array segment", compileUnitUId: "62");

        var statement = Assert.Single(reduced.Network.MoveBlkVariants);
        var en = Assert.IsType<EnSource.Condition>(statement.En);
        Assert.Empty(Assert.IsType<Expr.And>(en.Value).Operands);
        Assert.Equal("SourceArray", Assert.IsType<Expr.TagRef>(statement.Src).Path);
        Assert.Equal("ElementCount", Assert.IsType<Expr.TagRef>(statement.Count).Path);
        Assert.Equal("SourceStartIndex", Assert.IsType<Expr.TagRef>(statement.SrcIndex).Path);
        Assert.Equal("DestStartIndex", Assert.IsType<Expr.TagRef>(statement.DestIndex).Path);
        Assert.Equal("MoveResult", statement.RetValTag);
        Assert.Equal("TargetArray", statement.DestTag);
    }

    [Fact]
    public void Reduce_MoveBlkVariantFedByRail_SidecarRecordsVersion()
    {
        var network = LoadFixture("MoveBlkVariantFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Copy array segment", compileUnitUId: "62");

        var sidecar = Assert.Single(reduced.Sidecar.MoveBlkVariants);
        Assert.Equal("1.2", sidecar.Version);
    }

    [Fact]
    public void RoundTrip_MoveBlkVariantFedByRail_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("MoveBlkVariantFedByRail.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Copy array segment", compileUnitUId: "62");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var part = Assert.Single(reparsed.Parts, p => p.Name == "MOVE_BLK_VARIANT");
        Assert.Equal("1.2", part.Version);

        var retValWire = Assert.Single(reparsed.Wires, w => w.UId == 33);
        Assert.Contains(retValWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 27 && e.PortName == "Ret_Val");

        var destWire = Assert.Single(reparsed.Wires, w => w.UId == 34);
        Assert.Contains(destWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 27 && e.PortName == "DEST");
    }

    [Fact]
    public void SerializeNetworkOnly_MoveBlkVariantFedByRail_ProducesReadableSyntax()
    {
        var network = LoadFixture("MoveBlkVariantFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Copy array segment", compileUnitUId: "62");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Contains(
            "  MOVE_BLK_VARIANT(EN := TRUE, SRC := SourceArray, COUNT := ElementCount, SRC_INDEX := SourceStartIndex, " +
            "DEST_INDEX := DestStartIndex, Ret_Val => MoveResult, DEST => TargetArray)\n", text);
    }

    [Fact]
    public void FullBlock_MoveBlkVariantFedByRail_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("MoveBlkVariantFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Copy array segment", compileUnitUId: "62");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_MoveBlkVariantWithDisabledEno_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="MOVE_BLK_VARIANT" Version="1.2" UId="1" DisabledENO="true" />
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(element));
        Assert.Contains("DisabledENO", ex.Message);
    }

    [Fact]
    public void Parse_MoveBlkVariantWithUnexpectedChild_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="MOVE_BLK_VARIANT" Version="1.2" UId="1">
                  <TemplateValue Name="Card" Type="Cardinality">1</TemplateValue>
                </Part>
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(element));
        Assert.Contains("child elements", ex.Message);
    }
}
