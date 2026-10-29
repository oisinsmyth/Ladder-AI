using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// An FB call with an InOut parameter and a Bool Input fed by power flow (2026-10-29). Grounded on a
/// real export: an FB taking a PID_Compact instance as its `PID` InOut, and its `Running` input fed by
/// two series contacts. `CallWithInOutAndChainInput.xml` is that shape with invented names. The
/// InOut reads as `Ctrl &lt;=&gt; Tag`; the chain input reads as an ordinary `Active := A AND B`.
/// </summary>
public class CallInOutAndChainInputTests
{
    private const string Readable =
        "  CALL LoopControl(Loop1_DB, EN := TRUE, Active := Pump1.Running AND LoopEnable, Ctrl <=> Loop1Controller)\n";

    private static FlgNetwork LoadFixture() =>
        FlgNetParser.Parse(XElement.Load(Path.Combine("Fixtures", "CallWithInOutAndChainInput.xml")));

    private static (IrNetwork Network, NetworkSidecar Sidecar) Reduce()
    {
        var reduced = GraphReducer.Reduce(LoadFixture(), networkNumber: 1, title: "Loop", compileUnitUId: "3");
        return (reduced.Network, reduced.Sidecar);
    }

    private static CalleeInterfaceRegistry Registry(string activeType = "Bool") =>
        CalleeInterfaceRegistry.FromBlocks(new[]
        {
            new IrBlock("0", "FB", "LoopControl", 1, "LAD", null, System.Array.Empty<IrNetwork>(),
                InputMembers: new[] { new DbMember("Active", activeType, Retain: false, StartValue: null) },
                InOutMembers: new[] { new DbMember("Ctrl", "Loop_Type", Retain: false, StartValue: null) }),
        });

    [Fact]
    public void Parse_AcceptsAnInOutParameter()
    {
        var call = Assert.Single(LoadFixture().Parts, p => p.Name == "Call");
        Assert.Contains(call.CallParameters!, p => p.Name == "Ctrl" && p.Section == "InOut" && p.Type == "Loop_Type");
    }

    [Fact]
    public void Reduce_ReadsTheChainAsAnExpression_AndTheInOutAsATag()
    {
        var (network, sidecar) = Reduce();

        Assert.Equal(Readable, IrSerializer.SerializeNetworkOnly(network).Split('\n', 3)[1] + "\n");
        var arguments = Assert.Single(sidecar.Calls).Arguments;
        var chain = Assert.IsType<CallArgumentSidecar.ChainInputArgSidecar>(arguments[0]);
        Assert.Equal(2, chain.Steps.Count);
        Assert.Equal(30, chain.RailWireUId);
        var inOut = Assert.IsType<CallArgumentSidecar.InOutArgSidecar>(arguments[1]);
        Assert.Equal((23, 35), (inOut.AccessUId, inOut.WireUId));
    }

    [Fact]
    public void RoundTrip_ThroughTheSidecar_RebuildsTheSameTopology_InOutInConsumerOrder()
    {
        var (network, sidecar) = Reduce();

        var reparsed = FlgNetParser.Parse(FlgNetWriter.Write(FlgNetBuilder.Build(network, sidecar)));
        var original = LoadFixture();

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);
        var inOutWire = Assert.Single(reparsed.Wires, w => w.UId == 35);
        Assert.Equal(EndpointKind.IdentCon, inOutWire.Endpoints[0].Kind);
        Assert.Equal(("Ctrl", EndpointKind.NameCon), (inOutWire.Endpoints[1].PortName, inOutWire.Endpoints[1].Kind));
        var chainEnd = Assert.Single(reparsed.Wires, w => w.UId == 34);
        Assert.Contains(chainEnd.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 26 && e.PortName == "Active");
    }

    [Fact]
    public void FullBlock_ParseThenSerialize_IsByteIdentical()
    {
        var (network, sidecar) = Reduce();
        var text = IrSerializer.SerializeBlock(
            new IrBlock("0", "FC", "TestBlock", 1, "LAD", null, new[] { network }), new[] { sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);

        Assert.Equal(text, IrSerializer.SerializeBlock(parsedBlock, parsedSidecars));
    }

    [Fact]
    public void Synthesize_FromReadableAlone_BuildsAChainInputAndAnInOut()
    {
        var network = IrParser.ParseNetworkOnly($"NETWORK 1 \"Loop\"\n{Readable}");

        var sidecar = SidecarSynthesizer.Synthesize(network, new HashSet<string>(), Registry());

        var arguments = Assert.Single(sidecar.Calls).Arguments;
        Assert.Equal(2, Assert.IsType<CallArgumentSidecar.ChainInputArgSidecar>(arguments[0]).Steps.Count);
        Assert.Equal("Loop_Type", Assert.IsType<CallArgumentSidecar.InOutArgSidecar>(arguments[1]).Type);
        var reparsed = FlgNetParser.Parse(FlgNetWriter.Write(FlgNetBuilder.Build(network, sidecar)));
        Assert.Equal(LoadFixture().Parts.Count, reparsed.Parts.Count);
        Assert.Contains(Assert.Single(reparsed.Parts, p => p.Name == "Call").CallParameters!, p => p.Section == "InOut");
    }

    [Fact]
    public void Synthesize_InOutArgAgainstAnInputParam_HardErrors()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"Loop\"\n  CALL LoopControl(Loop1_DB, EN := TRUE, Active <=> SomeTag)\n");

        var ex = Assert.Throws<UnsupportedSynthesisConstructException>(
            () => SidecarSynthesizer.Synthesize(network, new HashSet<string>(), Registry()));
        Assert.Contains("declares it as Input", ex.Message);
    }

    [Fact]
    public void Synthesize_ExpressionIntoANonBoolInput_HardErrors()
    {
        var network = IrParser.ParseNetworkOnly($"NETWORK 1 \"Loop\"\n{Readable}");

        var ex = Assert.Throws<UnsupportedSynthesisConstructException>(
            () => SidecarSynthesizer.Synthesize(network, new HashSet<string>(), Registry(activeType: "Int")));
        Assert.Contains("only a Bool parameter", ex.Message);
    }

    [Fact]
    public void TagReferences_TreatAnInOutAsBothReadAndWritten()
    {
        var (network, _) = Reduce();

        var usages = TagReferences.AllDirectedUsages(network).Where(u => u.Path == "Loop1Controller").ToList();

        Assert.Contains(usages, u => u.Direction == TagDirection.Read);
        Assert.Contains(usages, u => u.Direction == TagDirection.Write);
    }
}
