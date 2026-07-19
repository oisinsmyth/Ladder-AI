using Converter.Ir;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Derive-always (ADR-0005): a fully-synthesizable block serialises to a readable-only `.ir` (no
/// SIDECAR), which parses back and re-derives its sidecar — the round trip `to-ir` (readable-only) →
/// `to-xml` (synthesize) relies on. The CLI's verified omit/keep decision (`--no-sidecar`) is pinned by
/// NoSidecarEquivalenceTests + the parity harness + the live backstop; this checks the serializer contract.
/// </summary>
public class DeriveAlwaysTests
{
    private static IrBlock Block(params IrNetwork[] networks) =>
        new("0", "FC", "T", 1, "LAD", null, networks);

    [Fact]
    public void SerializeBlockReadable_OmitsSidecarSection()
    {
        var block = Block(IrParser.ParseNetworkOnly("NETWORK 1 \"N\"\n  COIL X := A AND NOT B\n"));

        var text = IrSerializer.SerializeBlockReadable(block);

        Assert.DoesNotContain("\nSIDECAR\n", text);
        Assert.Contains("COIL X := A AND NOT B", text);
    }

    [Fact]
    public void ReadableOnly_ParsesBackAndReDerives()
    {
        var block = Block(IrParser.ParseNetworkOnly("NETWORK 1 \"N\"\n  COIL X := A AND NOT B\n"));
        var text = IrSerializer.SerializeBlockReadable(block);

        // The canonical derive-always round trip: readable-only text → parse → synthesize a fresh sidecar.
        var reparsed = IrParser.ParseBlockWithoutSidecar(text);
        var sidecars = SidecarSynthesizer.SynthesizeBlock(reparsed);

        Assert.Equal(block.Networks.Count, sidecars.Count);
        Assert.Single(sidecars[0].Assignments);
    }
}
