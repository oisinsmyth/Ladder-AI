using System;
using System.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Exponent literals (2026-10-29). TIA exports an out-of-Real-range sentinel such as
/// `MOVE(IN := 1.0E+64) => &lt;Real&gt;` with ConstantType LReal. The IR parser's numeric-literal
/// shape had no exponent, so `1.0E+64` became the tag path `"1"."0E+64"` and the re-derived block
/// failed to compile ("Tag "1"."0E+64" not defined"). Asserted against the EMITTED type, not a round
/// trip, for the reason LiteralDestinationTypeSynthesisTests gives.
/// </summary>
public class ExponentLiteralTests
{
    private static TagTypeRegistry Tags(params (string Name, string Type)[] tags) =>
        TagTypeRegistry.FromSources(
            Array.Empty<DbSource>(), Array.Empty<PlcTypeSource>(),
            tags.Select((t, i) => new PlcTagSource(i.ToString(), t.Name, t.Type, $"%MD{i * 4}", true, true, true, null)).ToArray());

    private static NetworkSidecar Synth(string body, TagTypeRegistry tagTypes) =>
        SidecarSynthesizer.Synthesize(
            IrParser.ParseNetworkOnly($"NETWORK 1 \"T\"\n  {body}\n"),
            new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal),
            null,
            tagTypes);

    private static string? TypeOf(NetworkSidecar sidecar, string literal) =>
        Assert.Single(sidecar.ConstantUIds, c => c.Value == literal).ConstantType;

    // RED BEFORE THE FIX: the literal was not a literal at all, so no constant entry existed.
    [Fact]
    public void BeyondRealRange_IntoARealPort_IsAnLRealConstant()
    {
        var sidecar = Synth("MOVE(EN := TRUE, IN := 1.0E+64) => MaxT", Tags(("MaxT", "Real")));

        Assert.Equal("LReal", TypeOf(sidecar, "1.0E+64"));
    }

    // Within Real range the port's type still decides, exactly as for a plain decimal.
    [Theory]
    [InlineData("1.5E+3")]
    [InlineData("2.0e-5")]
    [InlineData("-3.0E+38")]
    public void InRange_ExponentLiteral_TakesThePortType(string literal)
    {
        var sidecar = Synth($"MOVE(EN := TRUE, IN := {literal}) => Dest", Tags(("Dest", "Real")));

        Assert.Equal("Real", TypeOf(sidecar, literal));
    }

    // The fallback with no resolvable port: a float literal is Real, or LReal past Real's range.
    [Theory]
    [InlineData("1.5E+3", "Real")]
    [InlineData("1.0E+64", "LReal")]
    public void UnknownPort_TypesAFloatLiteralByRange(string literal, string expected)
    {
        var sidecar = Synth($"MOVE(EN := TRUE, IN := {literal}) => UnknownDest", Tags());

        Assert.Equal(expected, TypeOf(sidecar, literal));
    }
}
