using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `WAIT` — time-delay instruction (Phase 2 Tier 6, 2026-07-14). Grounded against a real export
/// sweep of `JOB9002`'s full block inventory (`FC VSDDataSequence`): `&lt;Part Name="WAIT"
/// Version="1.0" UId="N" /&gt;` — bare, no `DisabledENO`, no other attributes or children. Its own
/// two ports (confirmed by scoping the search to its own network, since the same UId is reused
/// across many other, unrelated networks in the same file — a general lesson for grounding, not
/// just this instruction): `en` (rail-fed in the one real instance) and `WT` (wait time, a literal
/// `Int` `1000` in the one real instance). **The first instruction this converter models with no
/// destination tag at all** — a pure side-effecting delay, not a value producer.
/// </summary>
public class WaitTests
{
    private static FlgNetwork LoadFixture(string name) =>
        FlgNetParser.Parse(XElement.Load(Path.Combine("Fixtures", name)));

    [Fact]
    public void Parse_WaitFedByRail_ProducesWaitPart()
    {
        var network = LoadFixture("WaitFedByRail.xml");

        var part = Assert.Single(network.Parts, p => p.Name == "WAIT");
        Assert.Equal(22, part.UId);
        Assert.Equal("1.0", part.Version);
    }

    [Fact]
    public void Reduce_WaitFedByRail_EnIsRailAndWtResolvesToLiteral()
    {
        var network = LoadFixture("WaitFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Comms step delay", compileUnitUId: "62");

        var statement = Assert.Single(reduced.Network.Waits);
        var en = Assert.IsType<EnSource.Condition>(statement.En);
        Assert.Empty(Assert.IsType<Expr.And>(en.Value).Operands);
        Assert.Equal("1000", Assert.IsType<Expr.Literal>(statement.Wt).Value);
    }

    [Fact]
    public void Reduce_WaitFedByRail_SidecarRecordsVersion()
    {
        var network = LoadFixture("WaitFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Comms step delay", compileUnitUId: "62");

        var sidecar = Assert.Single(reduced.Sidecar.Waits);
        Assert.Equal("1.0", sidecar.Version);
    }

    [Fact]
    public void RoundTrip_WaitFedByRail_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("WaitFedByRail.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Comms step delay", compileUnitUId: "62");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var part = Assert.Single(reparsed.Parts, p => p.Name == "WAIT");
        Assert.Equal("1.0", part.Version);

        var wtWire = Assert.Single(reparsed.Wires, w => w.UId == 24);
        Assert.Contains(wtWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 22 && e.PortName == "WT");
    }

    [Fact]
    public void SerializeNetworkOnly_WaitFedByRail_ProducesReadableSyntaxWithNoDestination()
    {
        var network = LoadFixture("WaitFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Comms step delay", compileUnitUId: "62");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Contains("  WAIT(EN := TRUE, WT := 1000)\n", text);
    }

    [Fact]
    public void FullBlock_WaitFedByRail_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("WaitFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Comms step delay", compileUnitUId: "62");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_WaitWithDisabledEno_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="WAIT" Version="1.0" UId="1" DisabledENO="true" />
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
