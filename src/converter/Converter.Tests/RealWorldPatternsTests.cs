using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Three real-world patterns confirmed against actual JOB9002 exports (2026-07-11), each a
/// correctness bug or gap found and fixed during the live proof: multiple independent
/// coil-per-network rungs sharing one rail wire, empty (`&lt;NetworkSource /&gt;`) networks,
/// and slice-access (bit-within-word) alarm addressing (06-lad-conventions.md C-501).
/// </summary>
public class RealWorldPatternsTests
{
    [Fact]
    public void Reduce_MultipleIndependentRungsSharingOneRailWire_ProducesOneAssignmentEach()
    {
        var element = XElement.Load(Path.Combine("Fixtures", "MultiAssignmentAndSlice.xml"));
        var network = FlgNetParser.Parse(element);

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Alarm summary", compileUnitUId: "5");

        Assert.Equal(2, reduced.Network.Assignments.Count);
        Assert.Equal("Input.AlarmCondition0", Assert.IsType<Expr.TagRef>(reduced.Network.Assignments[0].Condition).Path);
        Assert.Equal("Input.AlarmCondition1", Assert.IsType<Expr.TagRef>(reduced.Network.Assignments[1].Condition).Path);

        // Both chains' first contact was fed by the same 3-endpoint source wire (UId 40) —
        // must be recognized as one shared rail connection, not rejected as fan-out.
        Assert.Equal(40, reduced.Sidecar.Assignments[0].RailWireUId);
        Assert.Equal(40, reduced.Sidecar.Assignments[1].RailWireUId);
    }

    [Fact]
    public void Reduce_SliceAccessCoil_PreservesBitAddressInTagPath()
    {
        var element = XElement.Load(Path.Combine("Fixtures", "MultiAssignmentAndSlice.xml"));
        var network = FlgNetParser.Parse(element);

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Alarm summary", compileUnitUId: "5");

        Assert.Equal("Alarms.Summary0.%X0", reduced.Network.Assignments[0].CoilTag);
        Assert.Equal("Alarms.Summary0.%X1", reduced.Network.Assignments[1].CoilTag);
    }

    [Fact]
    public void RoundTrip_MultiAssignmentWithSlice_RebuildsIdenticalTopology()
    {
        var element = XElement.Load(Path.Combine("Fixtures", "MultiAssignmentAndSlice.xml"));
        var original = FlgNetParser.Parse(element);
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Alarm summary", compileUnitUId: "5");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.AccessNodes.Count, reparsed.AccessNodes.Count);
        Assert.Contains(reparsed.AccessNodes, a => a.DottedPath == "Alarms.Summary0.%X0" && a.UId == 22);
        Assert.Contains(reparsed.AccessNodes, a => a.DottedPath == "Alarms.Summary0.%X1" && a.UId == 24);

        // The shared rail wire must come back as ONE wire with both contacts as endpoints,
        // not two separate wires — otherwise re-export would produce a different topology
        // than TIA originally gave us.
        var railWire = Assert.Single(reparsed.Wires, w => w.UId == 40);
        Assert.Equal(3, railWire.Endpoints.Count);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.Powerrail);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 31 && e.PortName == "in");
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 33 && e.PortName == "in");
    }

    [Fact]
    public void Reduce_EmptyNetwork_ProducesZeroAssignments()
    {
        var empty = new FlgNetwork(Array.Empty<AccessNode>(), Array.Empty<PartNode>(), Array.Empty<WireNode>());

        var reduced = GraphReducer.Reduce(empty, networkNumber: 3, title: "", compileUnitUId: "D");

        Assert.True(reduced.Network.IsEmpty);
        Assert.Empty(reduced.Network.Assignments);
        Assert.Empty(reduced.Sidecar.Assignments);
    }

    [Fact]
    public void EmptyNetwork_SerializeThenParse_RoundTrips()
    {
        var emptyNetwork = new IrNetwork(3, "", Array.Empty<CoilAssignment>());

        var text = IrSerializer.SerializeNetworkOnly(emptyNetwork);
        var parsed = IrParser.ParseNetworkOnly(text);

        Assert.True(parsed.IsEmpty);
        Assert.Equal(3, parsed.Number);
        Assert.Contains("[empty]", text);
    }

    [Fact]
    public void EmptyNetwork_BuildsEmptyFlgNetwork()
    {
        var emptyNetwork = new IrNetwork(3, "", Array.Empty<CoilAssignment>());
        var emptySidecar = new NetworkSidecar(3, "D", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());

        var flgNetwork = FlgNetBuilder.Build(emptyNetwork, emptySidecar);

        Assert.Empty(flgNetwork.AccessNodes);
        Assert.Empty(flgNetwork.Parts);
        Assert.Empty(flgNetwork.Wires);
    }
}
