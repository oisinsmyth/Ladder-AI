using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// OR-merge (`Part Name="O"`) and negated-contact (`&lt;Negated Name="operand" /&gt;`) support —
/// confirmed real, 2026-07-10, against station_1's `PerimeterSafetyAlarms`/`GeneralAlarms` (S1 item 7,
/// docs/notes/stage-gates.md). Both real examples had the OR-merge rail-facing (every branch a
/// single Contact fed directly by Powerrail); a multi-contact branch and a nested OR-merge were
/// initially refused as unconfirmed shapes, until S1 item 11 (2026-07-11/12) generalized every
/// branch to an ordinary chain (reusing `GraphReducer.TraceChain` recursively, confirmed real
/// against `FC ControlDelays`'/`FB MotorDOL`'s own shapes) — the two tests that used to assert a
/// hard error here now assert the correct positive reduction instead, same fixtures.
/// </summary>
public class OrMergeAndNegationTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Reduce_ThreeWayOrMerge_ProducesOrExpression()
    {
        var network = LoadFixture("OrMergeCoil.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Any fault", compileUnitUId: "3");

        var assignment = Assert.Single(reduced.Network.Assignments);
        Assert.Equal("AnyFault", assignment.CoilTag);
        var or = Assert.IsType<Expr.Or>(assignment.Condition);
        Assert.Equal(3, or.Operands.Count);
        Assert.Equal("Sensor1.Fault", Assert.IsType<Expr.TagRef>(or.Operands[0]).Path);
        Assert.Equal("Sensor2.Fault", Assert.IsType<Expr.TagRef>(or.Operands[1]).Path);
        Assert.Equal("Sensor3.Fault", Assert.IsType<Expr.TagRef>(or.Operands[2]).Path);
    }

    [Fact]
    public void Reduce_ThreeWayOrMerge_SidecarRecordsSharedRailAndBranches()
    {
        var network = LoadFixture("OrMergeCoil.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Any fault", compileUnitUId: "3");

        var assignment = Assert.Single(reduced.Sidecar.Assignments);
        // The outer chain's own RailWireUId is null whenever it terminates at an OrStep — each
        // branch now owns its own rail wiring internally (S1 item 11), mirroring the existing
        // TimerOutputStep precedent (a chain terminating at a TON's Q is also always null here).
        Assert.Null(assignment.RailWireUId);
        var orStep = Assert.IsType<ChainStepSidecar.OrStep>(Assert.Single(assignment.Steps));
        Assert.Equal(54, orStep.OrPartUId);
        Assert.Equal(77, orStep.OutgoingWireUId);
        Assert.Equal(3, orStep.Branches.Count);
        Assert.Equal(
            new[] { 51, 52, 53 },
            orStep.Branches.Select(b => Assert.IsType<ChainStepSidecar.ContactStep>(Assert.Single(b.Steps)).ContactUId));
        Assert.All(orStep.Branches, b => Assert.False(Assert.IsType<ChainStepSidecar.ContactStep>(Assert.Single(b.Steps)).Negated));
        // Every branch shares the same rail wire (confirmed real, 2026-07-10) — proven per-branch
        // now that each branch carries its own RailWireUId (S1 item 11), not just assumed.
        Assert.All(orStep.Branches, b => Assert.Equal(70, b.RailWireUId));
    }

    [Fact]
    public void RoundTrip_OrMergeCoil_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("OrMergeCoil.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Any fault", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.AccessNodes.Count, reparsed.AccessNodes.Count);
        var orPart = Assert.Single(reparsed.Parts, p => p.Name == "O");
        Assert.Equal(3, orPart.Cardinality);

        var railWire = Assert.Single(reparsed.Wires, w => w.UId == 70);
        Assert.Equal(4, railWire.Endpoints.Count);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.Powerrail);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 51 && e.PortName == "in");
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 52 && e.PortName == "in");
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 53 && e.PortName == "in");
    }

    [Fact]
    public void SerializeNetworkOnly_OrMerge_ProducesReadableOrExpression()
    {
        var network = LoadFixture("OrMergeCoil.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Any fault", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 1 \"Any fault\"\n  COIL AnyFault := Sensor1.Fault OR Sensor2.Fault OR Sensor3.Fault\n",
            text);
    }

    [Fact]
    public void Reduce_NegatedContactInChain_ProducesNotExpression()
    {
        var network = LoadFixture("NegatedContactChain.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Test NOT", compileUnitUId: "3");

        var assignment = Assert.Single(reduced.Network.Assignments);
        var and = Assert.IsType<Expr.And>(assignment.Condition);
        Assert.Equal(2, and.Operands.Count);
        Assert.Equal("Sensor1.Ok", Assert.IsType<Expr.TagRef>(and.Operands[0]).Path);
        var not = Assert.IsType<Expr.Not>(and.Operands[1]);
        Assert.Equal("Sensor2.Fault", Assert.IsType<Expr.TagRef>(not.Operand).Path);
    }

    [Fact]
    public void SerializeNetworkOnly_NegatedContact_ProducesReadableNotExpression()
    {
        var network = LoadFixture("NegatedContactChain.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Test NOT", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal("NETWORK 1 \"Test NOT\"\n  COIL Output1 := Sensor1.Ok AND NOT Sensor2.Fault\n", text);
    }

    [Fact]
    public void RoundTrip_NegatedContact_RebuildsNegatedElement()
    {
        var original = LoadFixture("NegatedContactChain.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Test NOT", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var negatedContact = Assert.Single(reparsed.Parts, p => p.UId == 32);
        Assert.True(negatedContact.Negated);
        var plainContact = Assert.Single(reparsed.Parts, p => p.UId == 31);
        Assert.False(plainContact.Negated);
    }

    [Fact]
    public void FullBlock_OrMergeAndNegation_ParseThenSerialize_IsByteIdentical()
    {
        var orNetwork = LoadFixture("OrMergeCoil.xml");
        var orReduced = GraphReducer.Reduce(orNetwork, networkNumber: 1, title: "Any fault", compileUnitUId: "3");
        var notNetwork = LoadFixture("NegatedContactChain.xml");
        var notReduced = GraphReducer.Reduce(notNetwork, networkNumber: 2, title: "Test NOT", compileUnitUId: "4");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { orReduced.Network, notReduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { orReduced.Sidecar, notReduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    // A multi-contact branch (Contact50->Contact51, an ordinary 2-step chain) mixed with a plain
    // single-contact branch (Contact52) — mirrors the real FB MotorDOL shape (O(45)'s branches
    // fed by a non-rail-fed shared Contact) at a smaller scale. Confirms an OR-branch is resolved
    // as an ordinary chain (S1 item 11): the branch's own AND-chain nests correctly inside the
    // overall OR, with standard AND-binds-tighter precedence needing no parens in the IR text.
    [Fact]
    public void Reduce_OrMergeWithMultiContactBranch_ProducesAndWithinOr()
    {
        var network = LoadFixture("OrMergeMultiContactBranch.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Multi-contact branch", compileUnitUId: "3");

        var assignment = Assert.Single(reduced.Network.Assignments);
        Assert.Equal("AnyFault", assignment.CoilTag);
        var or = Assert.IsType<Expr.Or>(assignment.Condition);
        Assert.Equal(2, or.Operands.Count);
        var and = Assert.IsType<Expr.And>(or.Operands[0]);
        Assert.Equal("Sensor0.Fault", Assert.IsType<Expr.TagRef>(and.Operands[0]).Path);
        Assert.Equal("Sensor1.Fault", Assert.IsType<Expr.TagRef>(and.Operands[1]).Path);
        Assert.Equal("Sensor2.Fault", Assert.IsType<Expr.TagRef>(or.Operands[1]).Path);

        var orStep = Assert.IsType<ChainStepSidecar.OrStep>(Assert.Single(reduced.Sidecar.Assignments[0].Steps));
        Assert.Equal(2, orStep.Branches[0].Steps.Count);
        Assert.Equal(70, orStep.Branches[0].RailWireUId);
        Assert.Equal(70, orStep.Branches[1].RailWireUId);
    }

    [Fact]
    public void SerializeNetworkOnly_OrMergeWithMultiContactBranch_UsesPrecedenceWithoutParens()
    {
        var network = LoadFixture("OrMergeMultiContactBranch.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Multi-contact branch", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 1 \"Multi-contact branch\"\n  COIL AnyFault := Sensor0.Fault AND Sensor1.Fault OR Sensor2.Fault\n",
            text);
    }

    [Fact]
    public void RoundTrip_OrMergeWithMultiContactBranch_RebuildsWithoutDuplicationAndSharedRail()
    {
        var original = LoadFixture("OrMergeMultiContactBranch.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Multi-contact branch", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var railWire = Assert.Single(reparsed.Wires, w => w.UId == 70);
        Assert.Equal(3, railWire.Endpoints.Count);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.Powerrail);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 50 && e.PortName == "in");
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 52 && e.PortName == "in");
    }

    // O(53) (Contact51/Contact52) feeds O(57).in1 directly, with Contact56 as O(57).in2 — nested
    // OR-merge, real per S1 item 11's design (falls out of resolving branches via TraceChain
    // recursively) though not independently confirmed live in isolation, noted honestly as such.
    [Fact]
    public void Reduce_NestedOrMerge_ProducesNestedOrExpression()
    {
        var network = LoadFixture("NestedOrMerge.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Nested OR", compileUnitUId: "3");

        var assignment = Assert.Single(reduced.Network.Assignments);
        var outerOr = Assert.IsType<Expr.Or>(assignment.Condition);
        Assert.Equal(2, outerOr.Operands.Count);
        var innerOr = Assert.IsType<Expr.Or>(outerOr.Operands[0]);
        Assert.Equal("Sensor1.Fault", Assert.IsType<Expr.TagRef>(innerOr.Operands[0]).Path);
        Assert.Equal("Sensor2.Fault", Assert.IsType<Expr.TagRef>(innerOr.Operands[1]).Path);
        Assert.Equal("Sensor3.Fault", Assert.IsType<Expr.TagRef>(outerOr.Operands[1]).Path);

        var outerOrStep = Assert.IsType<ChainStepSidecar.OrStep>(Assert.Single(reduced.Sidecar.Assignments[0].Steps));
        Assert.Equal(57, outerOrStep.OrPartUId);
        var nestedBranch = outerOrStep.Branches[0];
        var innerOrStep = Assert.IsType<ChainStepSidecar.OrStep>(Assert.Single(nestedBranch.Steps));
        Assert.Equal(53, innerOrStep.OrPartUId);
        // The branch's own chain terminates at O(53), not the rail — mirrors TimerOutputStep's
        // own null-RailWireUId precedent (S1 item 11).
        Assert.Null(nestedBranch.RailWireUId);
    }

    [Fact]
    public void SerializeNetworkOnly_NestedOrMerge_FlattensToThreeWayOr()
    {
        var network = LoadFixture("NestedOrMerge.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Nested OR", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        // Or-in-Or needs no parens (associative, same precedence) — reads identically to a flat
        // 3-way OR-merge, even though the underlying sidecar/Part structure is genuinely nested.
        Assert.Equal(
            "NETWORK 1 \"Nested OR\"\n  COIL AnyFault := Sensor1.Fault OR Sensor2.Fault OR Sensor3.Fault\n",
            text);
    }

    [Fact]
    public void RoundTrip_NestedOrMerge_RebuildsBothOrPartsAndSharedRail()
    {
        var original = LoadFixture("NestedOrMerge.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Nested OR", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);
        Assert.Equal(2, reparsed.Parts.Count(p => p.Name == "O"));

        var railWire = Assert.Single(reparsed.Wires, w => w.UId == 70);
        Assert.Equal(4, railWire.Endpoints.Count);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.Powerrail);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 51 && e.PortName == "in");
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 52 && e.PortName == "in");
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 56 && e.PortName == "in");
    }

    // The load-bearing case none of the other fixtures cover: a single upstream Contact's
    // outgoing wire genuinely fans out to become the shared prefix of TWO different OR-merge
    // branches (Contact31 -> [Contact32.in, Contact33.in] via one wire with 3 endpoints) —
    // mirrors the real FB MotorDOL shape (O(45)'s branches fed by Contact41's own fan-out) at a
    // minimal scale, without needing a Move tap to exercise it. Proves OR-branch recursion and
    // the MOVE-era dedup/fan-out mechanism compose correctly together, not just each in
    // isolation.
    [Fact]
    public void Reduce_OrMergeSharedPrefixBranches_ProducesOrOfTwoAndChains()
    {
        var network = LoadFixture("OrMergeSharedPrefixBranches.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Shared prefix", compileUnitUId: "3");

        var assignment = Assert.Single(reduced.Network.Assignments);
        Assert.Equal("Output.Selected", assignment.CoilTag);
        var or = Assert.IsType<Expr.Or>(assignment.Condition);
        Assert.Equal(2, or.Operands.Count);
        var branch1 = Assert.IsType<Expr.And>(or.Operands[0]);
        Assert.Equal("EnableCmd", Assert.IsType<Expr.TagRef>(branch1.Operands[0]).Path);
        Assert.Equal("ModeA", Assert.IsType<Expr.TagRef>(branch1.Operands[1]).Path);
        var branch2 = Assert.IsType<Expr.And>(or.Operands[1]);
        Assert.Equal("EnableCmd", Assert.IsType<Expr.TagRef>(branch2.Operands[0]).Path);
        Assert.Equal("ModeB", Assert.IsType<Expr.TagRef>(branch2.Operands[1]).Path);
    }

    [Fact]
    public void Reduce_OrMergeSharedPrefixBranches_BothBranchesShareContact31AndRailWire()
    {
        var network = LoadFixture("OrMergeSharedPrefixBranches.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Shared prefix", compileUnitUId: "3");

        var orStep = Assert.IsType<ChainStepSidecar.OrStep>(Assert.Single(reduced.Sidecar.Assignments[0].Steps));
        Assert.Equal(2, orStep.Branches.Count);

        var branch1Steps = orStep.Branches[0].Steps;
        Assert.Equal(2, branch1Steps.Count);
        Assert.Equal(31, Assert.IsType<ChainStepSidecar.ContactStep>(branch1Steps[0]).ContactUId);
        Assert.Equal(32, Assert.IsType<ChainStepSidecar.ContactStep>(branch1Steps[1]).ContactUId);
        Assert.Equal(41, orStep.Branches[0].RailWireUId);

        var branch2Steps = orStep.Branches[1].Steps;
        Assert.Equal(2, branch2Steps.Count);
        Assert.Equal(31, Assert.IsType<ChainStepSidecar.ContactStep>(branch2Steps[0]).ContactUId);
        Assert.Equal(33, Assert.IsType<ChainStepSidecar.ContactStep>(branch2Steps[1]).ContactUId);
        Assert.Equal(41, orStep.Branches[1].RailWireUId);
    }

    [Fact]
    public void SerializeNetworkOnly_OrMergeSharedPrefixBranches_UsesPrecedenceWithoutParens()
    {
        var network = LoadFixture("OrMergeSharedPrefixBranches.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Shared prefix", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        // One EnableCmd contact fans out to both OR-branches of the single coil (an intra-statement split),
        // so the reducer marks it `{split 1}`/`{recv 1}` (ADR-0006). Note: no ` SPLIT` header — the legacy
        // per-network flag only catches *cross*-statement fan-out (DetectSplit), which is exactly the gap
        // the per-node markers close; the flag is removed entirely in phase 3.
        Assert.Equal(
            "NETWORK 1 \"Shared prefix\"\n  COIL Output.Selected := EnableCmd{split 1} AND ModeA OR EnableCmd{recv 1} AND ModeB\n",
            text);
    }

    // The other half of the load-bearing proof: rebuilding from the (deliberately duplicated,
    // per the test above — Contact31 appears in both branches' own Steps) sidecar data must NOT
    // emit duplicate <Part>/<Wire> elements, and the genuinely fanned-out wire must end up with
    // all 3 of its real endpoints, not 2.
    [Fact]
    public void RoundTrip_OrMergeSharedPrefixBranches_RebuildsWithoutDuplicationAndPreservesFanOut()
    {
        var original = LoadFixture("OrMergeSharedPrefixBranches.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Shared prefix", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(5, reparsed.Parts.Count);
        Assert.Equal(new[] { 31, 32, 33, 34, 35 }, reparsed.Parts.Select(p => p.UId).OrderBy(u => u));

        Assert.Equal(9, reparsed.Wires.Count);
        Assert.Equal(Enumerable.Range(41, 9), reparsed.Wires.Select(w => w.UId).OrderBy(u => u));

        var fanOutWire = Assert.Single(reparsed.Wires, w => w.UId == 43);
        Assert.Equal(3, fanOutWire.Endpoints.Count);
        Assert.Contains(fanOutWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 31 && e.PortName == "out");
        Assert.Contains(fanOutWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "in");
        Assert.Contains(fanOutWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 33 && e.PortName == "in");

        var railWire = Assert.Single(reparsed.Wires, w => w.UId == 41);
        Assert.Equal(2, railWire.Endpoints.Count);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.Powerrail);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 31 && e.PortName == "in");
    }
}
