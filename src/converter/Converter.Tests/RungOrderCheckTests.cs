using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// A rung is one connected graph and TIA draws it contiguously, so an independent statement written
/// between two statements of one rung cannot keep its place. Until 2026-10-29 `to-xml` silently drew it
/// after the whole rung: inside a timer's {split}/{recv} group, or between a TON and the coil its Q
/// leads. Explicit-order IR is now refused with the statements named. Canonical kind order states no
/// order (ir/SPEC.md) and keeps regrouping, but a drawable canonical network is drawn in its text order.
/// </summary>
public class RungOrderCheckTests
{
    private const string Header = "BLOCK FC Probe\nROOTID 0\nNUMBER 900\nLANGUAGE LAD\n\n";

    private static IrBlock Block(string networks) => IrParser.ParseBlockWithoutSidecar(Header + networks);

    // The statement lines a to-xml -> to-ir round trip reads back, in order.
    private static List<string> RoundTrip(IrBlock block, CalleeInterfaceRegistry? callees = null)
    {
        var network = block.Networks[0];
        var sidecar = SidecarSynthesizer.SynthesizeBlock(block, callees)[0];
        var reparsed = FlgNetParser.Parse(FlgNetWriter.Write(FlgNetBuilder.Build(network, sidecar)));
        var reduced = GraphReducer.Reduce(reparsed, network.Number, network.Title, sidecar.CompileUnitUId, sourceStatementOrder: true);
        return IrSerializer.SerializeNetworkOnly(reduced.Network).Split('\n')
            .Where(l => l.StartsWith("  ", StringComparison.Ordinal)).Select(l => l.Trim()).ToList();
    }

    [Fact]
    public void ExplicitOrder_StatementInsideATimerSplitGroup_IsRefused_NamingIt()
    {
        var block = Block(
            "NETWORK 1 \"N\"\n" +
            "  COIL Lamp.First := Plant.Healthy\n" +
            "  TON(Timers.StartDelay, IN := ((Plant.Mode >= 1){split 2} AND (Plant.Mode < 3){split 3}){split 1} AND Feeder.Ready, PT := T#5S)\n" +
            "  COIL Lamp.Other := Plant.Ok\n" +
            "  COIL Valve.Shutdown := NOT ((Plant.Mode >= 1 AND (Plant.Mode < 3){recv 3}){recv 1}) AND Plant.Mode = -1\n");

        var ex = Assert.Throws<UnsupportedSynthesisConstructException>(() => SidecarSynthesizer.SynthesizeBlock(block));

        Assert.Contains("statement order cannot be drawn", ex.Message);
        Assert.Contains("'COIL Lamp.Other := Plant.Ok'", ex.Message);
        Assert.Contains("COIL Valve.Shutdown", ex.Message);
    }

    [Fact]
    public void ExplicitOrder_StatementBetweenATimerAndTheCoilItsQLeads_IsRefused()
    {
        var block = Block(
            "NETWORK 1 \"N\"\n" +
            "  COIL Lamp.First := Plant.Healthy\n" +
            "  TON(Timers.Delay, IN := Plant.Run, PT := T#5S)\n" +
            "  COIL Lamp.Between := Plant.Ok\n" +
            "  COIL Valve.Start := Timers.Delay.Q AND Plant.Enable\n");

        var ex = Assert.Throws<UnsupportedSynthesisConstructException>(() => SidecarSynthesizer.SynthesizeBlock(block));
        Assert.Contains("'COIL Lamp.Between := Plant.Ok'", ex.Message);
    }

    [Fact]
    public void ExplicitOrder_ThatCanBeDrawn_IsKeptExactly()
    {
        var block = Block(
            "NETWORK 1 \"N\"\n" +
            "  COIL Lamp.First := Plant.Healthy\n" +
            "  TON(Timers.Delay, IN := Plant.Run, PT := T#5S)\n" +
            "  COIL Valve.Start := Timers.Delay.Q AND Plant.Enable\n" +
            "  COIL Lamp.After := Plant.Ok\n");

        Assert.Equal(
            new[]
            {
                "COIL Lamp.First := Plant.Healthy",
                "TON(Timers.Delay, IN := Plant.Run, PT := T#5S)",
                "COIL Valve.Start := Timers.Delay.Q AND Plant.Enable",
                "COIL Lamp.After := Plant.Ok",
            },
            RoundTrip(block));
    }

    [Fact]
    public void CanonicalOrder_StatesNoOrder_SoAnUndrawableOneIsStillRegrouped()
    {
        // TON, COIL, RCOIL-from-Q is canonical kind order: legacy grouped IR, whose real order lives in
        // its sidecar. Refusing it would make every such network uneditable.
        var block = Block(
            "NETWORK 1 \"N\"\n" +
            "  TON(Timers.Delay, IN := Plant.Run, PT := T#5S)\n" +
            "  COIL Lamp.Between := Plant.Ok\n" +
            "  RCOIL Valve.Start := Timers.Delay.Q\n");

        var drawn = RoundTrip(block);

        Assert.Equal("COIL Lamp.Between := Plant.Ok", drawn[^1]);
    }

    [Fact]
    public void CanonicalOrder_CallBeforeTheBoxReadingItsOutput_IsDrawnCallFirst()
    {
        // Until 2026-10-29 kind-ordered synthesis drew CALL last, so this ADD ran before the CALL that
        // writes its input, a scan-order change in a block read straight from a real export.
        var callees = CalleeInterfaceRegistry.FromBlocks(new[]
        {
            new IrBlock("0", "FC", "ScaleIt", 1, "LAD", null, System.Array.Empty<IrNetwork>(),
                InputMembers: new[] { new DbMember("Raw", "Real", Retain: false, StartValue: null) },
                OutputMembers: new[] { new DbMember("Result", "Real", Retain: false, StartValue: null) }),
        });
        var block = Block(
            "NETWORK 1 \"N\"\n" +
            "  CALL ScaleIt(EN := TRUE, Raw := Sensor.Raw, Result => Work.Scaled)\n" +
            "  ADD(EN := TRUE, IN1 := Work.Scaled, IN2 := Sensor.Offset) => Sensor.Value\n");

        var drawn = RoundTrip(block, callees);

        Assert.StartsWith("CALL ScaleIt(", drawn[0]);
        Assert.StartsWith("ADD(", drawn[1]);
    }
}
