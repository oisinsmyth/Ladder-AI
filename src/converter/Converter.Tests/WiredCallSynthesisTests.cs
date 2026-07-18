using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Wired-argument CALL synthesis (2026-07-18): `to-xml --synthesize` can now mint a CALL with
/// Input/Output arguments, taking each argument's Type from the callee's interface via
/// CalleeInterfaceRegistry (ADR-0001: the callee .ir is the source of truth). Found needed by the
/// genval2 (ShredderControlSystem) blind validation, whose reusable formal-parameter FB couldn't be wired.
/// The zero-argument CALL path (the STATIC-struct site convention) is unchanged and covered by
/// SidecarSynthesizerTests.
/// </summary>
public class WiredCallSynthesisTests
{
    private static IrBlock Callee(string name, (string Name, string Type)[] inputs, (string Name, string Type)[] outputs) =>
        new("0", "FB", name, 1, "LAD", null, System.Array.Empty<IrNetwork>(),
            InputMembers: inputs.Select(p => new DbMember(p.Name, p.Type, Retain: false, StartValue: null)).ToArray(),
            OutputMembers: outputs.Select(p => new DbMember(p.Name, p.Type, Retain: false, StartValue: null)).ToArray());

    private static (string Name, string Type)[] None => System.Array.Empty<(string, string)>();

    private static (IrNetwork Network, NetworkSidecar Sidecar) Synth(string callLine, CalleeInterfaceRegistry registry)
    {
        var network = IrParser.ParseNetworkOnly($"NETWORK 1 \"Test\"\n  {callLine}\n");
        return (network, SidecarSynthesizer.Synthesize(network, new HashSet<string>(), registry));
    }

    [Fact]
    public void WiredCall_InputAndOutput_TakeTypesFromCalleeInterface_AndFeedTheBuilder()
    {
        var registry = CalleeInterfaceRegistry.FromBlocks(new[]
        {
            Callee("FB_Callee", inputs: new[] { ("InVal", "Word"), ("Enable", "Bool") }, outputs: new[] { ("OutVal", "Int") }),
        });

        var (network, sidecar) = Synth(
            "CALL FB_Callee(iDB_Callee, EN := TRUE, InVal := SourceWord, Enable := StartBit, OutVal => ResultInt)", registry);

        var call = Assert.Single(sidecar.Calls);
        Assert.Equal(3, call.Arguments.Count);
        var inputs = call.Arguments.OfType<CallArgumentSidecar.InputArgSidecar>().ToDictionary(a => a.ParamName);
        var outputs = call.Arguments.OfType<CallArgumentSidecar.OutputArgSidecar>().ToDictionary(a => a.ParamName);
        Assert.Equal("Word", inputs["InVal"].Type);
        Assert.Equal("Bool", inputs["Enable"].Type);
        Assert.Equal("Int", outputs["OutVal"].Type);

        // Same write path PlantAutoControl's real wired calls round-trip through — proves the sidecar builds.
        Assert.NotEmpty(FlgNetBuilder.Build(network, sidecar).Parts);
    }

    [Fact]
    public void WiredCall_UdtTypedParam_NormalizesToBareTypeName()
    {
        // A UDT-typed member may carry its type quoted in the IR grammar; the emitted <Parameter Type=…>
        // must be the bare name (the read side records the bare name).
        var registry = CalleeInterfaceRegistry.FromBlocks(new[]
        {
            Callee("FB_Callee", inputs: new[] { ("InImage", "\"UDT_ShredderInImage\"") }, outputs: None),
        });

        var (_, sidecar) = Synth("CALL FB_Callee(iDB_Callee, EN := TRUE, InImage := DB_Inputs)", registry);

        var input = Assert.IsType<CallArgumentSidecar.InputArgSidecar>(Assert.Single(sidecar.Calls.Single().Arguments));
        Assert.Equal("UDT_ShredderInImage", input.Type);
    }

    [Fact]
    public void WiredCall_UnknownCallee_HardErrors()
    {
        var ex = Assert.Throws<UnsupportedSynthesisConstructException>(() =>
            Synth("CALL FB_Missing(iDB_X, EN := TRUE, P := A)", CalleeInterfaceRegistry.Empty));
        Assert.Contains("FB_Missing", ex.Message);
        Assert.Contains("interface is not available", ex.Message);
    }

    [Fact]
    public void WiredCall_UnknownParam_HardErrors()
    {
        var registry = CalleeInterfaceRegistry.FromBlocks(new[]
        {
            Callee("FB_Callee", inputs: new[] { ("Known", "Bool") }, outputs: None),
        });

        var ex = Assert.Throws<UnsupportedSynthesisConstructException>(() =>
            Synth("CALL FB_Callee(iDB_Callee, EN := TRUE, NotAParam := A)", registry));
        Assert.Contains("NotAParam", ex.Message);
    }

    [Fact]
    public void WiredCall_SectionMismatch_HardErrors()
    {
        // Passing an Output param as an input (:=) — the callee declares OutVal as Output.
        var registry = CalleeInterfaceRegistry.FromBlocks(new[]
        {
            Callee("FB_Callee", inputs: None, outputs: new[] { ("OutVal", "Int") }),
        });

        var ex = Assert.Throws<UnsupportedSynthesisConstructException>(() =>
            Synth("CALL FB_Callee(iDB_Callee, EN := TRUE, OutVal := A)", registry));
        Assert.Contains("Output", ex.Message);
    }

    [Fact]
    public void ZeroArgCall_StillWorks_WithoutRegistry()
    {
        // Regression: the STATIC-struct zero-arg CALL path is unchanged; a null/empty registry is fine.
        var network = IrParser.ParseNetworkOnly("NETWORK 1 \"Test\"\n  CALL FB_Callee(iDB_Callee, EN := TRUE)\n");
        var sidecar = SidecarSynthesizer.Synthesize(network, new HashSet<string>());
        Assert.Empty(sidecar.Calls.Single().Arguments);
    }
}
