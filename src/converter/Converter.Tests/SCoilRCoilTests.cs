using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Set/reset coils (`Part Name="SCoil"`/`"RCoil"`) — S1 item 15, 2026-07-12. Picked up per the
/// project owner's own explicit sequencing after `CALL` (S1 item 14): 3 of each real in
/// `FC PlantAutoControl`, also seen alongside TON in `FB MotorDOL`'s own earlier grounding. Grounded
/// against two independent real instances of each (different `CompileUnit`s) before any code —
/// found completely bare (`&lt;Part Name="SCoil" UId="N" /&gt;`, no attributes/children beyond
/// `Name`/`UId`), with the exact same `in`/`operand` port shape as a plain `Coil` and never a
/// producer (no `out` port). Structurally identical to `Coil` in every respect — the only
/// difference is semantic (`SCoil` only ever sets the target true when the condition is true,
/// `RCoil` only ever clears it; both leave the target unchanged when the condition is false,
/// unlike `Coil`'s own direct assignment) — a distinction inherent in the keyword itself, not
/// something the IR computes. `GraphReducer`/`FlgNetBuilder` reuse `ReduceOneChain`/
/// `BuildOneChain` verbatim for all three, tagging the result with `CoilAssignment.Kind`.
/// Readable-form keywords `SCOIL`/`RCOIL` mirror their own source Part Names, same convention as
/// `COIL`/`TON`/`MOVE`/`CALL` (`WAND` is the one deliberate exception, for a naming collision
/// that doesn't apply here).
/// </summary>
public class SCoilRCoilTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_CoilSetResetFedByRail_ProducesCoilSCoilRCoilParts()
    {
        var network = LoadFixture("CoilSetResetFedByRail.xml");

        var coil = Assert.Single(network.Parts, p => p.UId == 31);
        Assert.Equal("Coil", coil.Name);
        var sCoil = Assert.Single(network.Parts, p => p.UId == 33);
        Assert.Equal("SCoil", sCoil.Name);
        var rCoil = Assert.Single(network.Parts, p => p.UId == 35);
        Assert.Equal("RCoil", rCoil.Name);
    }

    [Fact]
    public void Reduce_CoilSetResetFedByRail_ProducesThreeAssignmentsWithCorrectKinds()
    {
        var network = LoadFixture("CoilSetResetFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Motor latch", compileUnitUId: "8");

        Assert.Equal(3, reduced.Network.Assignments.Count);

        var coil = reduced.Network.Assignments[0];
        Assert.Equal(CoilKind.Assign, coil.Kind);
        Assert.Equal("Output.Run", coil.CoilTag);
        Assert.Equal("StartCmd", Assert.IsType<Expr.TagRef>(coil.Condition).Path);

        var sCoil = reduced.Network.Assignments[1];
        Assert.Equal(CoilKind.Set, sCoil.Kind);
        Assert.Equal("Motor1.Latched", sCoil.CoilTag);
        Assert.Equal("SetCmd", Assert.IsType<Expr.TagRef>(sCoil.Condition).Path);

        var rCoil = reduced.Network.Assignments[2];
        Assert.Equal(CoilKind.Reset, rCoil.Kind);
        Assert.Equal("Motor1.Latched", rCoil.CoilTag);
        Assert.Equal("ResetCmd", Assert.IsType<Expr.TagRef>(rCoil.Condition).Path);
    }

    [Fact]
    public void Reduce_CoilSetResetFedByRail_SidecarHasNoKindField()
    {
        var network = LoadFixture("CoilSetResetFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Motor latch", compileUnitUId: "8");

        // The sidecar carries no Kind/PartName of its own (see CoilAssignmentSidecar's own doc
        // comment) — FlgNetBuilder derives the exact source Part Name from the model's own
        // CoilAssignment.Kind instead. Just confirm the shared rail/UId plumbing is otherwise
        // identical to a plain Coil's own sidecar shape.
        Assert.Equal(3, reduced.Sidecar.Assignments.Count);
        Assert.All(reduced.Sidecar.Assignments, a => Assert.Equal(40, a.RailWireUId));
        Assert.Equal(33, reduced.Sidecar.Assignments[1].CoilUId);
        Assert.Equal(35, reduced.Sidecar.Assignments[2].CoilUId);
    }

    [Fact]
    public void RoundTrip_CoilSetResetFedByRail_RebuildsIdenticalPartNames()
    {
        var original = LoadFixture("CoilSetResetFedByRail.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Motor latch", compileUnitUId: "8");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        Assert.Equal("Coil", Assert.Single(reparsed.Parts, p => p.UId == 31).Name);
        Assert.Equal("SCoil", Assert.Single(reparsed.Parts, p => p.UId == 33).Name);
        Assert.Equal("RCoil", Assert.Single(reparsed.Parts, p => p.UId == 35).Name);

        var railWire = Assert.Single(reparsed.Wires, w => w.UId == 40);
        Assert.Equal(4, railWire.Endpoints.Count);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "in");
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 34 && e.PortName == "in");
    }

    [Fact]
    public void SerializeNetworkOnly_CoilSetResetFedByRail_ProducesReadableKeywords()
    {
        var network = LoadFixture("CoilSetResetFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Motor latch", compileUnitUId: "8");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 1 \"Motor latch\"\n" +
            "  COIL Output.Run := StartCmd\n" +
            "  SCOIL Motor1.Latched := SetCmd\n" +
            "  RCOIL Motor1.Latched := ResetCmd\n",
            text);
    }

    [Fact]
    public void FullBlock_CoilSetReset_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("CoilSetResetFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Motor latch", compileUnitUId: "8");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", null, new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }
}
