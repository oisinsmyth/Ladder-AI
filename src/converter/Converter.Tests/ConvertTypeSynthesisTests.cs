using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// CONVERT type synthesis via the TagTypeRegistry (Gap B, 2026-07-18). A CONVERT's Src/DestType are
/// the operand tag types — sidecar-only, absent from the readable text. Synthesis now resolves them
/// from the registry, falling back to the original Real→DInt HMI-seconds idiom when a type is unknown
/// (no registry, or a literal input), so the change is strictly better than the old hardcode.
/// </summary>
public class ConvertTypeSynthesisTests
{
    private const string Network = "NETWORK 1 \"C\"\n  CONVERT(EN := TRUE, IN := RawSpeed) => ScaledSpeed\n";

    [Fact]
    public void Synthesize_Convert_UsesRegistryResolvedTypes()
    {
        var network = IrParser.ParseNetworkOnly(Network);
        var tagTypes = TagTypeRegistry.FromSources(
            System.Array.Empty<DbSource>(), System.Array.Empty<PlcTypeSource>(),
            new[]
            {
                new PlcTagSource("1", "RawSpeed", "Int", "%MW0", true, true, true, null),
                new PlcTagSource("2", "ScaledSpeed", "Real", "%MD4", true, true, true, null),
            });

        var sidecar = SidecarSynthesizer.Synthesize(
            network, new HashSet<string>(System.StringComparer.Ordinal), callees: null, tagTypes: tagTypes);

        var convert = Assert.Single(sidecar.Converts);
        Assert.Equal("Int", convert.SrcType);
        Assert.Equal("Real", convert.DestType);
    }

    [Fact]
    public void Synthesize_Convert_FallsBackToRealDIntWhenTypeUnknown()
    {
        var network = IrParser.ParseNetworkOnly(Network);

        // No registry supplied → the operands can't be typed → the historical HMI default holds.
        var sidecar = SidecarSynthesizer.Synthesize(network);

        var convert = Assert.Single(sidecar.Converts);
        Assert.Equal("Real", convert.SrcType);
        Assert.Equal("DInt", convert.DestType);
    }
}
