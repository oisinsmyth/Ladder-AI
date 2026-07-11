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
/// single Contact fed directly by Powerrail) — the two hard-error tests here confirm this
/// converter slice refuses rather than guesses at the two unconfirmed shapes a step further out:
/// a multi-contact branch, and an OR-merge nested inside another OR-merge.
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
        Assert.Equal(70, assignment.RailWireUId);
        var orStep = Assert.IsType<ChainStepSidecar.OrStep>(Assert.Single(assignment.Steps));
        Assert.Equal(54, orStep.OrPartUId);
        Assert.Equal(77, orStep.OutgoingWireUId);
        Assert.Equal(3, orStep.Branches.Count);
        Assert.Equal(new[] { 51, 52, 53 }, orStep.Branches.Select(b => b.ContactUId));
        Assert.All(orStep.Branches, b => Assert.False(b.Negated));
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

    [Fact]
    public void Reduce_OrMergeWithMultiContactBranch_ThrowsNonReducible()
    {
        var network = LoadFixture("OrMergeMultiContactBranch.xml");

        var ex = Assert.Throws<NonReducibleNetworkException>(
            () => GraphReducer.Reduce(network, networkNumber: 1, title: "Bad branch", compileUnitUId: "3"));

        Assert.Contains("multi-contact OR branches are outside this slice", ex.Message);
    }

    [Fact]
    public void Reduce_NestedOrMerge_ThrowsNonReducible()
    {
        var network = LoadFixture("NestedOrMerge.xml");

        var ex = Assert.Throws<NonReducibleNetworkException>(
            () => GraphReducer.Reduce(network, networkNumber: 1, title: "Nested OR", compileUnitUId: "3"));

        Assert.Contains("multi-element/non-contact OR branches are outside this slice", ex.Message);
    }
}
