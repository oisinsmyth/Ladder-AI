using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `FillBlockI` — block-fill-with-integer instruction (Phase 2 Tier 6, 2026-07-14). Grounded
/// against a real export sweep of `JOB9002`'s full block inventory (`FC ModbusComs`):
/// `&lt;Part Name="FillBlockI" UId="N" DisabledENO="true" /&gt;` — bare, no `Version`, no other
/// content. Structurally close to `Move` (`en`-gated, one destination tag via `out`) plus one
/// extra tag-or-literal input, `count` (how many elements to fill) — the fill value itself is
/// `in`. Ports are lowercase (`en`/`in`/`count`/`out`), unlike `MOVE_BLK_VARIANT`'s own uppercase.
///
/// The real `out` destination (`CommsProcessData.NodeFaultCount[3]`) is array-indexed, not a
/// plain scalar tag — confirmed live, 2026-07-14: TIA's own compiler rejected a live-verification
/// attempt against a plain scalar destination ("Elements of a structure or of an ARRAY can only
/// be filled if all the elements have the same elementary data type"), which is what surfaced
/// this. No converter code changed for it — the existing general array-index `AccessNode` support
/// (`ArrayIndex`/`DottedPath`, confirmed real since the very first array-subscript grounding) was
/// already sufficient; only the fixture needed the array-indexed shape to be a faithful
/// reproduction of the real one.
/// </summary>
public class FillBlockITests
{
    private static FlgNetwork LoadFixture(string name) =>
        FlgNetParser.Parse(XElement.Load(Path.Combine("Fixtures", name)));

    [Fact]
    public void Parse_FillBlockIFedByRail_ProducesFillBlockIPart()
    {
        var network = LoadFixture("FillBlockIFedByRail.xml");

        var part = Assert.Single(network.Parts, p => p.Name == "FillBlockI");
        Assert.Equal(26, part.UId);
        Assert.Null(part.Version);
    }

    [Fact]
    public void Reduce_FillBlockIFedByRail_EnIsContactConditionAndOperandsResolve()
    {
        var network = LoadFixture("FillBlockIFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Fill status block", compileUnitUId: "62");

        var statement = Assert.Single(reduced.Network.FillBlockIs);
        var en = Assert.IsType<EnSource.Condition>(statement.En);
        Assert.Equal("GateBit", Assert.IsType<Expr.TagRef>(en.Value).Path);
        Assert.Equal("FillValue", Assert.IsType<Expr.TagRef>(statement.In).Path);
        Assert.Equal("FillCount", Assert.IsType<Expr.TagRef>(statement.Count).Path);
        Assert.Equal("TargetBlock[0]", statement.DestTag);
    }

    [Fact]
    public void RoundTrip_FillBlockIFedByRail_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("FillBlockIFedByRail.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Fill status block", compileUnitUId: "62");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var part = Assert.Single(reparsed.Parts, p => p.Name == "FillBlockI");
        Assert.Null(part.Version);

        var enWire = Assert.Single(reparsed.Wires, w => w.UId == 29);
        Assert.Contains(enWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 25 && e.PortName == "out");
        Assert.Contains(enWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 26 && e.PortName == "en");
    }

    [Fact]
    public void SerializeNetworkOnly_FillBlockIFedByRail_ProducesReadableSyntax()
    {
        var network = LoadFixture("FillBlockIFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Fill status block", compileUnitUId: "62");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Contains("  FILLBLOCKI(EN := GateBit, IN := FillValue, COUNT := FillCount) => TargetBlock[0]\n", text);
    }

    [Fact]
    public void FullBlock_FillBlockIFedByRail_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("FillBlockIFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Fill status block", compileUnitUId: "62");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_FillBlockIMissingDisabledEno_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="FillBlockI" UId="1" />
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(element));
        Assert.Contains("DisabledENO", ex.Message);
    }
}
