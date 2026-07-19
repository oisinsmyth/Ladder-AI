using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// MOVE (`Part Name="Move"`) — S1 item 10, 2026-07-11. Grounded against `FB MotorDOL`'s "HMI
/// Motor Status Telemetry" network: a cascade of `Contact -&gt; Move` taps writing a status code,
/// no Coil at all in that real network.
///
/// The key finding: a Move is not a boolean chain position (unlike Eq/Ge) and not a
/// self-contained production like TON — it's a side effect *tapped off* a chain position's own
/// output via genuine wire fan-out. The same wire that feeds the next chain position also feeds
/// the Move's `en`, so one wire can carry three endpoints (producer, Move.en tap,
/// next-position.in) instead of the usual two. This required two changes:
///   - `GraphReducer.TraceChain`'s fan-out check changed from "exactly one other endpoint, else
///     throw" to "find the single genuine producer endpoint (<c>OutPortFor</c>), ignore every
///     other endpoint" — a Move is never a producer, so its own tap is always ignored by a trace
///     passing through the same wire.
///   - `FlgNetBuilder` rebuilt around a shared, de-duplicating endpoint accumulator, because
///     multiple Moves' own `en` chains telescope through the same upstream Contacts a Coil's (or
///     another Move's) chain already walked — the reducer intentionally re-derives the same
///     `ContactStep` data once per production that traces through it (see
///     <see cref="Reduce_TelescopingChain_EachProductionCarriesItsOwnFullPrefix"/>), and only the
///     builder's de-duplication (not the reducer) prevents that from becoming duplicate XML.
/// </summary>
public class MoveTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_MoveFedByContact_ProducesMovePart()
    {
        var network = LoadFixture("MoveFedByContact.xml");

        var move = Assert.Single(network.Parts, p => p.Name == "Move");
        Assert.Equal(32, move.UId);
    }

    [Fact]
    public void Reduce_MoveFedByContact_ProducesMoveStatement()
    {
        var network = LoadFixture("MoveFedByContact.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Status tap", compileUnitUId: "3");

        Assert.Empty(reduced.Network.Assignments);
        Assert.Empty(reduced.Network.Timers);
        var move = Assert.Single(reduced.Network.Moves);
        Assert.Equal("RunPermit", Assert.IsType<Expr.TagRef>(move.En).Path);
        Assert.Equal("SourceValue", Assert.IsType<Expr.TagRef>(move.In).Path);
        Assert.Equal("Status.Code", move.DestTag);
    }

    [Fact]
    public void Reduce_MoveFedByContact_SidecarRecordsMoveFields()
    {
        var network = LoadFixture("MoveFedByContact.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Status tap", compileUnitUId: "3");

        var moveSidecar = Assert.Single(reduced.Sidecar.Moves);
        Assert.Equal(32, moveSidecar.MovePartUId);
        Assert.Equal(41, moveSidecar.RailWireUId);
        var contactStep = Assert.IsType<ChainStepSidecar.ContactStep>(Assert.Single(moveSidecar.Steps));
        Assert.Equal(31, contactStep.ContactUId);
        var inTag = Assert.IsType<OperandSidecar.TagOperand>(moveSidecar.In);
        Assert.Equal(22, inTag.AccessUId);
        Assert.Equal(23, moveSidecar.DestAccessUId);
        Assert.Equal(45, moveSidecar.DestWireUId);
    }

    [Fact]
    public void RoundTrip_MoveFedByContact_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("MoveFedByContact.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Status tap", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var move = Assert.Single(reparsed.Parts, p => p.Name == "Move");
        Assert.Equal(32, move.UId);

        var enWire = Assert.Single(reparsed.Wires, w => w.UId == 43);
        Assert.Equal(2, enWire.Endpoints.Count);
        Assert.Contains(enWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 31 && e.PortName == "out");
        Assert.Contains(enWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "en");

        var destWire = Assert.Single(reparsed.Wires, w => w.UId == 45);
        Assert.Contains(destWire.Endpoints, e => e.Kind == EndpointKind.IdentCon && e.UId == 23);
        Assert.Contains(destWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "out1");
    }

    [Fact]
    public void SerializeNetworkOnly_MoveFedByContact_ProducesMoveStatementSyntax()
    {
        var network = LoadFixture("MoveFedByContact.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Status tap", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 1 \"Status tap\"\n  MOVE(EN := RunPermit, IN := SourceValue) => Status.Code\n",
            text);
    }

    // The load-bearing fixture for this whole feature: Contact1 -> [Move10 tap, Contact2
    // continue] -> Contact2 -> [Move11 tap, Coil continue] -> Coil, matching the real
    // FB MotorDOL cascade shape (genericized). Two genuinely fanned-out wires (43, 45, each with
    // 3 endpoints), and Contact1 is shared by all three productions (Move10, Move11, Coil) while
    // Contact2 is shared by two (Move11, Coil).
    [Fact]
    public void Reduce_TelescopingChain_ProducesTwoMovesAndOneCoilAssignment()
    {
        var network = LoadFixture("MoveTelescopingChain.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 3, title: "Motor status telemetry", compileUnitUId: "5");

        Assert.Equal(2, reduced.Network.Moves.Count);
        var move10 = reduced.Network.Moves[0];
        Assert.Equal("StartCmd", Assert.IsType<Expr.TagRef>(move10.En).Path);
        Assert.Equal("1", Assert.IsType<Expr.Literal>(move10.In).Value);
        Assert.Equal("Status.Word1", move10.DestTag);

        var move11 = reduced.Network.Moves[1];
        var move11En = Assert.IsType<Expr.And>(move11.En);
        Assert.Equal("StartCmd", Assert.IsType<Expr.TagRef>(move11En.Operands[0]).Path);
        Assert.Equal("RunCmd", Assert.IsType<Expr.TagRef>(move11En.Operands[1]).Path);
        Assert.Equal("SourceValue", Assert.IsType<Expr.TagRef>(move11.In).Path);
        Assert.Equal("Status.Word2", move11.DestTag);

        var assignment = Assert.Single(reduced.Network.Assignments);
        Assert.Equal("Output.Run", assignment.CoilTag);
        var coilCondition = Assert.IsType<Expr.And>(assignment.Condition);
        Assert.Equal("StartCmd", Assert.IsType<Expr.TagRef>(coilCondition.Operands[0]).Path);
        Assert.Equal("RunCmd", Assert.IsType<Expr.TagRef>(coilCondition.Operands[1]).Path);
    }

    // Proves the reducer's documented, deliberate behavior (GraphReducer.cs's comment on the
    // Moves loop): each production that traces through a shared upstream Contact re-derives its
    // own full ContactStep for it — Move10 sees just Contact1; Move11 and the Coil both see
    // Contact1 THEN Contact2, identical UIds in both places. This is not a bug to fix; it's the
    // input the builder's de-duplication (proven separately below) is designed to handle.
    [Fact]
    public void Reduce_TelescopingChain_EachProductionCarriesItsOwnFullPrefix()
    {
        var network = LoadFixture("MoveTelescopingChain.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 3, title: "Motor status telemetry", compileUnitUId: "5");

        var move10Sidecar = reduced.Sidecar.Moves[0];
        Assert.Equal(33, move10Sidecar.MovePartUId);
        var move10Step = Assert.IsType<ChainStepSidecar.ContactStep>(Assert.Single(move10Sidecar.Steps));
        Assert.Equal(31, move10Step.ContactUId);
        Assert.Equal(41, move10Sidecar.RailWireUId);

        var move11Sidecar = reduced.Sidecar.Moves[1];
        Assert.Equal(34, move11Sidecar.MovePartUId);
        Assert.Equal(2, move11Sidecar.Steps.Count);
        Assert.Equal(31, Assert.IsType<ChainStepSidecar.ContactStep>(move11Sidecar.Steps[0]).ContactUId);
        Assert.Equal(32, Assert.IsType<ChainStepSidecar.ContactStep>(move11Sidecar.Steps[1]).ContactUId);
        Assert.Equal(41, move11Sidecar.RailWireUId);

        var coilSidecar = Assert.Single(reduced.Sidecar.Assignments);
        Assert.Equal(2, coilSidecar.Steps.Count);
        Assert.Equal(31, Assert.IsType<ChainStepSidecar.ContactStep>(coilSidecar.Steps[0]).ContactUId);
        Assert.Equal(32, Assert.IsType<ChainStepSidecar.ContactStep>(coilSidecar.Steps[1]).ContactUId);
        Assert.Equal(41, coilSidecar.RailWireUId);
    }

    // The other half of the load-bearing proof: rebuilding from the (deliberately duplicated,
    // per the test above) sidecar data must NOT emit duplicate <Part>/<Wire> elements, and the
    // two genuinely fanned-out wires must end up with all three of their real endpoints, not two.
    [Fact]
    public void RoundTrip_TelescopingChain_RebuildsWithoutDuplicationAndPreservesFanOut()
    {
        var original = LoadFixture("MoveTelescopingChain.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 3, title: "Motor status telemetry", compileUnitUId: "5");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        // No duplicate Parts: exactly 5 distinct UIds (31-35), matching the original, even though
        // Contact1 (31) was touched by three separate productions and Contact2 (32) by two.
        Assert.Equal(5, reparsed.Parts.Count);
        Assert.Equal(new[] { 31, 32, 33, 34, 35 }, reparsed.Parts.Select(p => p.UId).OrderBy(u => u));

        // No duplicate Wires: exactly 10 distinct UIds (41-50), matching the original.
        Assert.Equal(10, reparsed.Wires.Count);
        Assert.Equal(Enumerable.Range(41, 10), reparsed.Wires.Select(w => w.UId).OrderBy(u => u));

        // Wire 43 (Contact1.out) genuinely fans out to both Move10's tap and Contact2's own
        // continuation — three endpoints, not two.
        var wire43 = Assert.Single(reparsed.Wires, w => w.UId == 43);
        Assert.Equal(3, wire43.Endpoints.Count);
        Assert.Contains(wire43.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 31 && e.PortName == "out");
        Assert.Contains(wire43.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 33 && e.PortName == "en");
        Assert.Contains(wire43.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "in");

        // Wire 45 (Contact2.out) fans out the same way, to Move11's tap and the Coil.
        var wire45 = Assert.Single(reparsed.Wires, w => w.UId == 45);
        Assert.Equal(3, wire45.Endpoints.Count);
        Assert.Contains(wire45.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "out");
        Assert.Contains(wire45.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 34 && e.PortName == "en");
        Assert.Contains(wire45.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 35 && e.PortName == "in");

        // The shared rail wire (41) has exactly one rail-facing endpoint (Contact1, the only
        // actual chain start) even though all three productions independently contributed it.
        var wire41 = Assert.Single(reparsed.Wires, w => w.UId == 41);
        Assert.Equal(2, wire41.Endpoints.Count);
        Assert.Contains(wire41.Endpoints, e => e.Kind == EndpointKind.Powerrail);
        Assert.Contains(wire41.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 31 && e.PortName == "in");
    }

    [Fact]
    public void SerializeNetworkOnly_TelescopingChain_ProducesCoilThenBothMoveStatements()
    {
        var network = LoadFixture("MoveTelescopingChain.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 3, title: "Motor status telemetry", compileUnitUId: "5");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        // The telescoping cascade fans StartCmd (and StartCmd·RunCmd) out across the coil and moves, so
        // the network is marked SPLIT (2026-07-19).
        Assert.Equal(
            "NETWORK 3 \"Motor status telemetry\" SPLIT\n" +
            "  COIL Output.Run := StartCmd AND RunCmd\n" +
            "  MOVE(EN := StartCmd, IN := 1) => Status.Word1\n" +
            "  MOVE(EN := StartCmd AND RunCmd, IN := SourceValue) => Status.Word2\n",
            text);
    }

    [Fact]
    public void FullBlock_Move_ParseThenSerialize_IsByteIdentical()
    {
        var simpleNetwork = LoadFixture("MoveFedByContact.xml");
        var simpleReduced = GraphReducer.Reduce(simpleNetwork, networkNumber: 1, title: "Status tap", compileUnitUId: "3");
        var telescopingNetwork = LoadFixture("MoveTelescopingChain.xml");
        var telescopingReduced = GraphReducer.Reduce(telescopingNetwork, networkNumber: 3, title: "Motor status telemetry", compileUnitUId: "5");

        var block = new IrBlock(
            "0", "FB", "TestBlock", 1, "LAD", "A test block",
            new[] { simpleReduced.Network, telescopingReduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { simpleReduced.Sidecar, telescopingReduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_MoveMissingDisabledEno_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Move" UId="1">
                  <TemplateValue Name="Card" Type="Cardinality">1</TemplateValue>
                </Part>
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
    public void Parse_MoveUnconfirmedCardinality_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Move" UId="1" DisabledENO="true">
                  <TemplateValue Name="Card" Type="Cardinality">2</TemplateValue>
                </Part>
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(element));
        Assert.Contains("MOVE_BLK_VARIANT", ex.Message);
    }
}
