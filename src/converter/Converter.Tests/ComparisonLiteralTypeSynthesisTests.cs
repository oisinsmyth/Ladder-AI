using System;
using System.Collections.Generic;
using System.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// A comparison's LITERAL is typed by the comparison's own type, not by the literal's magnitude
/// (2026-08-12).
///
/// FI-55 (2026-08-08) fixed the `SrcType` half of this — a tag's declared type outranks a
/// literal's inferred one — and left the literal half. So with registers declared `UInt` (the
/// honest type for a Modbus holding register) the synthesizer emitted `SrcType="UInt"` compare
/// boxes fed by `Int`/`DInt` literals, which TIA rejects by the same door FI-55 came through:
/// *"the data type Int of the actual parameter does not match the data type UInt of the formal
/// parameter"*.
///
/// **Nothing had hit it because the committed corpus contains ZERO `UInt`/`Word` comparisons.**
/// `Word`, `USInt`, `UDInt` and `SInt` are equally unexercised, so these are parameterised over the
/// whole unsigned/narrow family rather than over the one type that happened to be found.
/// </summary>
public class ComparisonLiteralTypeSynthesisTests
{
    private static NetworkSidecar Synthesize(string network, params (string Name, string Type)[] tags)
    {
        var tagTypes = TagTypeRegistry.FromSources(
            Array.Empty<DbSource>(), Array.Empty<PlcTypeSource>(),
            tags.Select((t, i) => new PlcTagSource(i.ToString(), t.Name, t.Type, $"%MW{i * 2}", true, true, true, null)).ToArray());

        return SidecarSynthesizer.Synthesize(
            IrParser.ParseNetworkOnly(network), new HashSet<string>(StringComparer.Ordinal), callees: null, tagTypes: tagTypes);
    }

    /// <summary>
    /// The measured case: a `UInt` register compared to a small literal. Before the fix this
    /// emitted `<ConstantType>Int</ConstantType>` against `SrcType="UInt"`.
    /// </summary>
    [Fact]
    public void UIntRegisterComparedToLiteral_TypesTheLiteralUInt()
    {
        var sidecar = Synthesize("NETWORK 1 \"C\"\n  COIL Out := HoldReg = 1\n", ("HoldReg", "UInt"));

        var compare = Assert.IsType<ChainStepSidecar.CompareStep>(Assert.Single(sidecar.Assignments[0].Steps));
        Assert.Equal("UInt", compare.SrcType);
        Assert.Equal("UInt", Assert.Single(sidecar.ConstantUIds, c => c.Value == "1").ConstantType);
    }

    /// <summary>
    /// The general rule, not the UInt one: whatever the comparison's resolved type is, the literal
    /// carries it. `DInt 65535` against a `UInt` box was one of the nine measured mismatches, so a
    /// literal whose magnitude would infer WIDER than the tag is covered too.
    /// </summary>
    [Theory]
    [InlineData("UInt", "0")]
    [InlineData("UInt", "65535")]
    [InlineData("Word", "1")]
    [InlineData("USInt", "255")]
    [InlineData("SInt", "-1")]
    [InlineData("UDInt", "1")]
    [InlineData("LInt", "7")]
    public void ComparisonLiteral_AlwaysCarriesTheComparisonsOwnType(string tagType, string literal)
    {
        var sidecar = Synthesize($"NETWORK 1 \"C\"\n  COIL Out := Reg >= {literal}\n", ("Reg", tagType));

        var compare = Assert.IsType<ChainStepSidecar.CompareStep>(Assert.Single(sidecar.Assignments[0].Steps));
        Assert.Equal(tagType, compare.SrcType);
        Assert.Equal(tagType, Assert.Single(sidecar.ConstantUIds, c => c.Value == literal).ConstantType);
    }

    /// <summary>
    /// Both operand positions, not just the right-hand one — the fix is applied at the operand
    /// resolver, so a literal on the left is typed identically.
    /// </summary>
    [Fact]
    public void ComparisonLiteral_OnTheLeftHandSide_IsTypedToo()
    {
        var sidecar = Synthesize("NETWORK 1 \"C\"\n  COIL Out := 5 < Reg\n", ("Reg", "UInt"));

        Assert.Equal("UInt", Assert.Single(sidecar.ConstantUIds, c => c.Value == "5").ConstantType);
    }

    /// <summary>
    /// The unchanged paths, guarded: with no resolvable tag type the historical magnitude inference
    /// still decides both halves, and it still agrees with itself.
    /// </summary>
    [Theory]
    [InlineData("1", "Int")]
    [InlineData("4294967295", "UDInt")]
    public void NoResolvableTagType_MagnitudeStillDecidesBothHalvesConsistently(string literal, string expected)
    {
        var sidecar = Synthesize($"NETWORK 1 \"C\"\n  COIL Out := Unknown >= {literal}\n");

        var compare = Assert.IsType<ChainStepSidecar.CompareStep>(Assert.Single(sidecar.Assignments[0].Steps));
        Assert.Equal(expected, compare.SrcType);
        Assert.Equal(expected, Assert.Single(sidecar.ConstantUIds, c => c.Value == literal).ConstantType);
    }

    /// <summary>
    /// FI-54's carve-out must survive: a duration literal is a `TypedConstant` with NO
    /// `&lt;ConstantType&gt;` at all, whatever type the surrounding operation resolves to. TIA
    /// rejects the other form at import ("The value 'T#0MS' cannot be set for the parameter of the
    /// type 'Time'"), so an override that reached duration literals would have re-broken it.
    /// </summary>
    [Fact]
    public void DurationLiteralInAComparison_StillCarriesNoConstantType()
    {
        var sidecar = Synthesize("NETWORK 1 \"C\"\n  COIL Out := Elapsed >= T#5S\n", ("Elapsed", "Time"));

        Assert.Null(Assert.Single(sidecar.ConstantUIds, c => c.Value == "T#5S").ConstantType);
    }
}
