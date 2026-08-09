using System;
using System.Collections.Generic;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-55 (2026-08-08). A comparison's SrcType must come from the TAG's declared type, not from the
/// literal's inferred one. The literal's "type" is a guess from its digits; the tag's is a fact
/// from its declaration.
///
/// Letting them compete on one rank ladder meant an UNSIGNED tag lost to its own literal and the
/// comparison was emitted as the signed type, which TIA rejects outright at compile:
///     "The data type UInt of the actual parameter does not match the data type Int of the
///      formal parameter"
///
/// The mechanism was the rank table: it listed Int/DInt/UDInt/LInt/ULInt/Real and returned 0 for
/// everything else, so UInt, USInt, Byte, Word and SInt all ranked BELOW Int and any literal beat
/// them. Found live on a panel handshake — the first unsigned tag this corpus had ever compared to
/// a constant, which is why it had never bitten despite many DInt-vs-literal comparisons working
/// correctly all along.
///
/// Widening the table alone would have fixed those five and left the same trap for the next type
/// nobody listed, so the precedence rule is the fix and the wider table is the belt.
/// </summary>
public class UnsignedCompareSrcTypeTests
{
    private static TagTypeRegistry Registry(string declaredType) =>
        TagTypeRegistry.FromSources(
            Array.Empty<DbSource>(), Array.Empty<PlcTypeSource>(),
            new[]
            {
                new PlcTagSource("1", "Val", declaredType, "%MW0", true, true, true, null),
                new PlcTagSource("2", "Other", declaredType, "%MW2", true, true, true, null),
            });

    private static string SrcTypeOf(string declaredType, string expression)
    {
        var network = IrParser.ParseNetworkOnly($"NETWORK 1 \"N\"\n  COIL Out := {expression}\n");
        var sidecar = SidecarSynthesizer.Synthesize(
            network, new HashSet<string>(StringComparer.Ordinal), callees: null,
            tagTypes: Registry(declaredType));

        return Assert.IsType<ChainStepSidecar.CompareStep>(
            Assert.Single(sidecar.Assignments[0].Steps)).SrcType;
    }

    // The five types that ranked below Int and therefore lost to their own literal. Each is the
    // exact shape that failed the live compile.
    [Theory]
    [InlineData("UInt")]
    [InlineData("USInt")]
    [InlineData("Byte")]
    [InlineData("Word")]
    [InlineData("SInt")]
    public void UnsignedTagComparedToBareLiteral_KeepsItsOwnType(string declaredType)
    {
        Assert.Equal(declaredType, SrcTypeOf(declaredType, "Val <> 0"));
    }

    // Operand order must not decide it either — the tag wins from either side.
    [Fact]
    public void LiteralOnTheLeft_StillYieldsTheTagsType()
    {
        Assert.Equal("UInt", SrcTypeOf("UInt", "0 <> Val"));
    }

    // The cases that already worked must keep working. A DInt tag beat its literal before this
    // change because DInt happened to be listed above Int; it must still win now that it wins for
    // the right reason.
    [Theory]
    [InlineData("DInt")]
    [InlineData("Real")]
    [InlineData("Int")]
    public void SignedAndRealTagsComparedToLiteral_AreUnchanged(string declaredType)
    {
        Assert.Equal(declaredType, SrcTypeOf(declaredType, "Val > 0"));
    }

    // Tag-vs-tag is decided by the widened rank table, and the pre-existing relative order among
    // the types that were already listed is preserved.
    [Fact]
    public void TagVersusTag_TakesTheWiderType()
    {
        var tagTypes = TagTypeRegistry.FromSources(
            Array.Empty<DbSource>(), Array.Empty<PlcTypeSource>(),
            new[]
            {
                new PlcTagSource("1", "Small", "Int", "%MW0", true, true, true, null),
                new PlcTagSource("2", "Big", "DInt", "%MD4", true, true, true, null),
            });

        var network = IrParser.ParseNetworkOnly("NETWORK 1 \"N\"\n  COIL Out := Small <> Big\n");
        var sidecar = SidecarSynthesizer.Synthesize(
            network, new HashSet<string>(StringComparer.Ordinal), callees: null, tagTypes: tagTypes);

        Assert.Equal("DInt", Assert.IsType<ChainStepSidecar.CompareStep>(
            Assert.Single(sidecar.Assignments[0].Steps)).SrcType);
    }

    // Literal-vs-literal has no tag to take a type from, so magnitude inference still applies —
    // the fallback path is untouched.
    [Fact]
    public void LiteralVersusLiteral_StillFallsBackToMagnitudeInference()
    {
        var network = IrParser.ParseNetworkOnly("NETWORK 1 \"N\"\n  COIL Out := 1 <> 2\n");
        var sidecar = SidecarSynthesizer.Synthesize(network);

        Assert.Equal("Int", Assert.IsType<ChainStepSidecar.CompareStep>(
            Assert.Single(sidecar.Assignments[0].Steps)).SrcType);
    }
}
