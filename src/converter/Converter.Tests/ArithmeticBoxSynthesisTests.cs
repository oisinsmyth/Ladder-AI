using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Box-family synthesis (2026-07-18, SignalConditioning parity): SUB/DIV kinds, Mul-to-Mul ENO
/// chaining, and registry-typed ABS/SWAP. Integration is proven end-to-end by the parity harness
/// (SignalConditioning flips green); these check the pieces directly.
/// </summary>
public class ArithmeticBoxSynthesisTests
{
    [Fact]
    public void Synthesize_SubThenDiv_ProducesKindsAndMulToMulEnoChain()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"A\"\n" +
            "  SUB(EN := TRUE, IN1 := A, IN2 := B) => D\n" +
            "  DIV(EN := ENO, IN1 := E, IN2 := F) => G\n");

        var sidecar = SidecarSynthesizer.Synthesize(network);

        Assert.Equal(2, sidecar.Muls.Count);
        Assert.Equal(MulKind.Subtract, sidecar.Muls[0].Kind);
        Assert.Equal(MulKind.Divide, sidecar.Muls[1].Kind);

        // DIV's `EN := ENO` chains from the SUB immediately before it.
        var eno = Assert.IsType<EnSourceSidecar.PrecedingEnoSidecar>(sidecar.Muls[1].En);
        Assert.Equal(sidecar.Muls[0].MulPartUId, eno.PrecedingPartUId);
    }

    [Fact]
    public void Synthesize_AbsAndSwap_TakeSrcTypeFromRegistry()
    {
        // Statement kind-order (IrSerializer): SWAP is emitted before ABS.
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"A\"\n" +
            "  SWAP(EN := Gate, IN := CtrlWord) => SwapOut\n" +
            "  ABS(EN := Gate, IN := SignedValue) => AbsOut\n");

        var tagTypes = TagTypeRegistry.FromSources(
            System.Array.Empty<DbSource>(), System.Array.Empty<PlcTypeSource>(),
            new[]
            {
                new PlcTagSource("1", "SignedValue", "Real", "%MD0", true, true, true, null),
                new PlcTagSource("2", "CtrlWord", "Word", "%MW4", true, true, true, null),
            });

        var sidecar = SidecarSynthesizer.Synthesize(
            network, new HashSet<string>(System.StringComparer.Ordinal), callees: null, tagTypes: tagTypes);

        Assert.Equal("Real", Assert.Single(sidecar.AbsStatements).SrcType);
        Assert.Equal("Word", Assert.Single(sidecar.Swaps).SrcType);
    }

    [Fact]
    public void Synthesize_Abs_UnresolvableOperandType_HardErrors()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"A\"\n  ABS(EN := Gate, IN := Mystery) => AbsOut\n");

        // No registry → Mystery's type can't be resolved → a clear hard error, not a silent guess.
        Assert.Throws<UnsupportedSynthesisConstructException>(() => SidecarSynthesizer.Synthesize(network));
    }
}
