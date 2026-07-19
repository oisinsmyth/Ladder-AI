using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// A standalone boolean inverter (`Part Name="Not"`) — S1 item 13, 2026-07-12. Found while
/// investigating whether `FC PlantAutoControl` round-trips (it doesn't yet — `Not` blocks its very
/// first network); confirmed real and grounded twice, independently, against two different real
/// instances in that same block (different networks, different UIds) before writing any code.
///
/// Genuinely different from a Contact's own `&lt;Negated Name="operand" /&gt;` (which negates a *tag
/// read*, not a chain position): `Not` is a standalone Part with a single `in`/`out` port pair
/// (same port names as `Contact`'s own), no operand/Access lookup at all — it inverts whatever
/// boolean value arrives on `in`. Architecturally simpler than MOVE/WAND: it's never its own
/// top-level production (no new `IrNetwork`/`NetworkSidecar` list) — purely a new chain-position
/// kind, discovered only when some other production's own `TraceChain` walk hits one. Resolved by
/// a fully self-contained, recursive `TraceChain` call on the Not's own `in` (exactly like an
/// OR-merge branch's own resolution — `ChainStepSidecar.NotStep` mirrors `OrBranch`'s nested
/// `(Steps, RailWireUId)` shape), wrapping the result in the already-existing `Expr.Not` node
/// (reused as-is — no new IR-text grammar needed, `NOT &lt;expr&gt;` already exists and already
/// renders/parses with correct precedence, from S1 items 7/11).
///
/// Both real instances grounded tap a shared wire via genuine fan-out (an upstream Part's output
/// feeds both a separately-continuing chain and the Not) — the same fan-out-tap mechanism already
/// proven for Move/OR-merge branches, here feeding back into a boolean chain instead of
/// terminating in a side-effect write. `NotFedByContact.xml` mirrors this real shape at a minimal
/// scale (genericized per `docs/13-data-boundary.md`).
/// </summary>
public class NotTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_NotFedByContact_ProducesBareNotPart()
    {
        var network = LoadFixture("NotFedByContact.xml");

        var not = Assert.Single(network.Parts, p => p.Name == "Not");
        Assert.Equal(34, not.UId);
        Assert.False(not.Negated);
        Assert.Null(not.Cardinality);
        Assert.Null(not.SrcType);
    }

    [Fact]
    public void Reduce_NotFedByContact_ProducesTwoIndependentCoilAssignments()
    {
        var network = LoadFixture("NotFedByContact.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Not tap", compileUnitUId: "3");

        Assert.Equal(2, reduced.Network.Assignments.Count);

        // Coil1's own chain never touches the Not tap at all — Contact31's output fans out, but
        // this chain only sees the "continuing" branch (Contact32), same producer-identification
        // mechanism already proven for Move.
        var coil1 = reduced.Network.Assignments[0];
        Assert.Equal("Output.RunA", coil1.CoilTag);
        var coil1Condition = Assert.IsType<Expr.And>(coil1.Condition);
        Assert.Equal("EnableCmd", Assert.IsType<Expr.TagRef>(coil1Condition.Operands[0]).Path);
        Assert.Equal("ModeA", Assert.IsType<Expr.TagRef>(coil1Condition.Operands[1]).Path);

        // Coil2's own chain goes through the Not tap: NOT(EnableCmd) AND FaultLatch.
        var coil2 = reduced.Network.Assignments[1];
        Assert.Equal("Output.RunB", coil2.CoilTag);
        var coil2Condition = Assert.IsType<Expr.And>(coil2.Condition);
        var notTerm = Assert.IsType<Expr.Not>(coil2Condition.Operands[0]);
        Assert.Equal("EnableCmd", Assert.IsType<Expr.TagRef>(notTerm.Operand).Path);
        Assert.Equal("FaultLatch", Assert.IsType<Expr.TagRef>(coil2Condition.Operands[1]).Path);
    }

    [Fact]
    public void Reduce_NotFedByContact_SidecarRecordsNestedChainAndSharedRail()
    {
        var network = LoadFixture("NotFedByContact.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Not tap", compileUnitUId: "3");

        var coil2Sidecar = reduced.Sidecar.Assignments[1];
        Assert.Equal(2, coil2Sidecar.Steps.Count);

        var notStep = Assert.IsType<ChainStepSidecar.NotStep>(coil2Sidecar.Steps[0]);
        Assert.Equal(34, notStep.NotPartUId);
        Assert.Equal(47, notStep.OutgoingWireUId);
        var nestedContact = Assert.IsType<ChainStepSidecar.ContactStep>(Assert.Single(notStep.Steps));
        Assert.Equal(31, nestedContact.ContactUId);
        // The Not's own upstream shares the same rail wire as Coil1's chain (Contact31's output
        // fans out to both) — confirmed real, same "rail wire is commonly shared" pattern this
        // project has tracked since S1 item 7.
        Assert.Equal(41, notStep.RailWireUId);

        var afterNot = Assert.IsType<ChainStepSidecar.ContactStep>(coil2Sidecar.Steps[1]);
        Assert.Equal(35, afterNot.ContactUId);
    }

    [Fact]
    public void RoundTrip_NotFedByContact_RebuildsWithoutDuplicationAndPreservesFanOut()
    {
        var original = LoadFixture("NotFedByContact.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Not tap", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var notPart = Assert.Single(reparsed.Parts, p => p.Name == "Not");
        Assert.Equal(34, notPart.UId);

        // Contact31 was touched by two separate productions (Coil1's own chain, and the Not
        // nested inside Coil2's chain) — must be emitted exactly once, and the fan-out wire must
        // end up with all 3 of its real endpoints (producer + both consumers), not 2.
        var fanOutWire = Assert.Single(reparsed.Wires, w => w.UId == 43);
        Assert.Equal(3, fanOutWire.Endpoints.Count);
        Assert.Contains(fanOutWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 31 && e.PortName == "out");
        Assert.Contains(fanOutWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "in");
        Assert.Contains(fanOutWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 34 && e.PortName == "in");

        var railWire = Assert.Single(reparsed.Wires, w => w.UId == 41);
        Assert.Equal(2, railWire.Endpoints.Count);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.Powerrail);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 31 && e.PortName == "in");
    }

    [Fact]
    public void SerializeNetworkOnly_NotFedByContact_ProducesReadableNotExpression()
    {
        var network = LoadFixture("NotFedByContact.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Not tap", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        // The fixture is a standalone Not *part* (invert-RLO), so it serialises as `NOT (EnableCmd)`
        // — distinct from a negated contact `NOT EnableCmd` (Gap H, 2026-07-18). It also fans one EnableCmd
        // contact out across the two coils — RunA's top-level contact and RunB's NOT input — so the reducer
        // marks it `{split 1}` (master) / `{recv 1}` (ADR-0006, 2026-07-19); the network keeps its SPLIT
        // flag alongside for now (phase 2). The recv marker rides inside the standalone Not: `NOT (EnableCmd{recv 1})`.
        Assert.Equal(
            "NETWORK 1 \"Not tap\" SPLIT\n" +
            "  COIL Output.RunA := EnableCmd{split 1} AND ModeA\n" +
            "  COIL Output.RunB := NOT (EnableCmd{recv 1}) AND FaultLatch\n",
            text);
    }

    [Fact]
    public void FullBlock_Not_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("NotFedByContact.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Not tap", compileUnitUId: "3");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }
}
