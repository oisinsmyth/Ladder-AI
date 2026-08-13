using System;
using System.Collections.Generic;
using System.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// *** A LITERAL IS TYPED BY THE PORT IT IS WRITTEN INTO, NEVER BY ITS OWN MAGNITUDE *** (2026-08-13).
///
/// Second instance of one class. `4090b6f` fixed *"a comparison's literal is typed by magnitude, not
/// by the compare's own type"*; this is the same bug one site along, so the fix is the general rule
/// rather than a second narrow patch — `ResolveOperand`'s port type is now a REQUIRED parameter, so a
/// site cannot omit it, and the magnitude fallback REFUSES where it cannot honestly infer.
///
/// The measured defect: the converter emitted `&lt;ConstantType&gt;Int&lt;/ConstantType&gt;` for EVERY
/// base-prefixed literal regardless of magnitude or destination, so a 32-bit build stamp was 16 bits
/// **by construction** and every real stamp failed to import. Two independent causes, both closed here:
///
///   1. `long.TryParse("16#A93F2C71")` fails, so a base-prefixed literal never reached the magnitude
///      test at all — it fell straight through to the `Int` default. Widening the parse would NOT
///      have fixed it: 2839872113 would then be typed `UDInt` into a `DWord` port, which TIA rejects
///      by the same door. A bit string's WIDTH IS A DECLARATION CHOICE its digits cannot express, so
///      the honest fallback is a refusal.
///   2. Six operand sites never passed the port type at all — including the CALL-argument site, where
///      the callee's own `param.Type` was resolved ON THE VERY NEXT LINE and used for the sidecar's
///      Type while the literal beside it was typed by magnitude.
///
/// *** WHY 1069 EXISTING TESTS WERE ALL GREEN ON THIS: *** the phase-2 lane's proof was that its IR
/// survives `to-xml` → `to-ir --no-sidecar` byte-identically, and it did — BECAUSE THE WRONG TYPE
/// ROUND-TRIPS FAITHFULLY. A round-trip check is blind to any error the round trip preserves. Every
/// assertion below is therefore against the EMITTED TYPE, never against a round trip.
/// </summary>
public class LiteralDestinationTypeSynthesisTests
{
    private const string Stamp32 = "16#A93F2C71";  // the worked example: 2,839,872,113 — 32 bits

    private static TagTypeRegistry Tags(params (string Name, string Type)[] tags) =>
        TagTypeRegistry.FromSources(
            Array.Empty<DbSource>(), Array.Empty<PlcTypeSource>(),
            tags.Select((t, i) => new PlcTagSource(i.ToString(), t.Name, t.Type, $"%MD{i * 4}", true, true, true, null)).ToArray());

    private static NetworkSidecar Synth(string body, TagTypeRegistry? tagTypes = null, CalleeInterfaceRegistry? callees = null) =>
        SidecarSynthesizer.Synthesize(
            IrParser.ParseNetworkOnly($"NETWORK 1 \"T\"\n  {body}\n"),
            new HashSet<string>(StringComparer.Ordinal),
            callees,
            tagTypes ?? TagTypeRegistry.FromSources(Array.Empty<DbSource>(), Array.Empty<PlcTypeSource>(), Array.Empty<PlcTagSource>()));

    private static string? TypeOf(NetworkSidecar sidecar, string literal) =>
        Assert.Single(sidecar.ConstantUIds, c => c.Value == literal).ConstantType;

    private static CalleeInterfaceRegistry Callee(string name, params (string Name, string Type)[] inputs) =>
        CalleeInterfaceRegistry.FromBlocks(new[]
        {
            new IrBlock("0", "FB", name, 1, "LAD", null, Array.Empty<IrNetwork>(),
                InputMembers: inputs.Select(p => new DbMember(p.Name, p.Type, Retain: false, StartValue: null)).ToArray()),
        });

    // ---- THE MEASURED DEFECT: a CALL argument, where the type was already resolved one line up ----

    // RED BEFORE THE FIX: emitted <ConstantType>Int</ConstantType>. 32 bits into a 16-bit type — the
    // import failure that blocks the version register.
    [Fact]
    public void CallArgument_ThirtyTwoBitStampIntoADWordParam_TypesItDWord()
    {
        var sidecar = Synth(
            $"CALL FB_Reg(iDB_Reg, EN := TRUE, Stamp := {Stamp32})",
            callees: Callee("FB_Reg", ("Stamp", "DWord")));

        Assert.Equal("DWord", TypeOf(sidecar, Stamp32));
    }

    // The general rule, not a DWord special case: whatever the callee declares, the literal carries.
    [Theory]
    [InlineData("DWord", Stamp32)]
    [InlineData("UDInt", "3000000000")]
    [InlineData("Word", "16#89")]
    [InlineData("Byte", "16#7F")]
    [InlineData("UInt", "65535")]
    [InlineData("SInt", "-1")]
    public void CallArgument_LiteralAlwaysCarriesTheCalleesDeclaredParamType(string paramType, string literal)
    {
        var sidecar = Synth(
            $"CALL FB_Reg(iDB_Reg, EN := TRUE, P := {literal})",
            callees: Callee("FB_Reg", ("P", paramType)));

        Assert.Equal(paramType, TypeOf(sidecar, literal));
    }

    // *** NOT A BLANKET WIDENING. *** An Int-range literal into an Int port is untouched — this is the
    // half that would go green on a fix that simply widened everything, so it is asserted explicitly.
    [Theory]
    [InlineData("Int", "5")]
    [InlineData("Int", "0")]
    [InlineData("Int", "32767")]
    [InlineData("Int", "-32768")]
    public void CallArgument_InRangeLiteralIntoAnIntParam_IsUnchanged(string paramType, string literal)
    {
        var sidecar = Synth(
            $"CALL FB_Reg(iDB_Reg, EN := TRUE, P := {literal})",
            callees: Callee("FB_Reg", ("P", paramType)));

        Assert.Equal("Int", TypeOf(sidecar, literal));
    }

    // ---- MOVE: the destination's declared type, including through a base-prefixed literal ----

    [Theory]
    [InlineData("DWord", Stamp32)]
    [InlineData("Word", "16#89")]
    [InlineData("UDInt", "0")]
    public void MoveLiteral_CarriesTheDestinationTagsType(string destType, string literal)
    {
        var sidecar = Synth($"MOVE(EN := TRUE, IN := {literal}) => Dest", Tags(("Dest", destType)));

        Assert.Equal(destType, TypeOf(sidecar, literal));
    }

    // The Step/counter literals this corpus is full of must not move.
    [Theory]
    [InlineData("0")]
    [InlineData("10")]
    [InlineData("30")]
    public void MoveLiteral_IntoAnIntDestination_StaysInt(string literal)
    {
        Assert.Equal("Int", TypeOf(Synth($"MOVE(EN := TRUE, IN := {literal}) => Step", Tags(("Step", "Int"))), literal));
    }

    // ---- THE FALLBACK REFUSES RATHER THAN GUESSING ----

    // RED BEFORE THE FIX in the worst possible way: it SUCCEEDED, emitting Int. A silent wrong answer
    // is what shipped; a hard error is the honest one.
    [Theory]
    [InlineData("16#A93F2C71")]
    [InlineData("16#89")]
    [InlineData("2#1011")]
    [InlineData("8#77")]
    public void BasePrefixedLiteral_WithNoResolvablePortType_IsARefusalNotAGuess(string literal)
    {
        var ex = Assert.Throws<UnsupportedSynthesisConstructException>(
            () => Synth($"MOVE(EN := TRUE, IN := {literal}) => UnknownDest"));

        Assert.Contains(literal, ex.Message);
        Assert.Contains("bit-string", ex.Message);
    }

    // ...but only base-prefixed literals lose magnitude inference. A plain decimal still infers, so
    // the historical fallback is intact and this is not a general tightening of unresolvable ports.
    [Theory]
    [InlineData("1", "Int")]
    [InlineData("32767", "Int")]
    [InlineData("40000", "DInt")]
    [InlineData("4294967295", "UDInt")]
    [InlineData("1.5", "Real")]
    public void DecimalLiteral_WithNoResolvablePortType_StillInfersByMagnitude(string literal, string expected)
    {
        Assert.Equal(expected, TypeOf(Synth($"MOVE(EN := TRUE, IN := {literal}) => UnknownDest"), literal));
    }

    // ---- The carve-outs that a "type everything from the port" rule could have re-broken ----

    // FI-54: a duration literal is a TypedConstant with NO <ConstantType> at all, whatever the port
    // says. TIA rejects the other form at import outright, so a port type reaching duration literals
    // would have re-broken a fix that cost a live round trip to find.
    [Fact]
    public void DurationLiteral_IntoATimePort_StillCarriesNoConstantType()
    {
        Assert.Null(TypeOf(Synth("MOVE(EN := TRUE, IN := T#5S) => Dwell", Tags(("Dwell", "Time"))), "T#5S"));
    }

    [Fact]
    public void DurationLiteral_AsATimerPreset_StillCarriesNoConstantType()
    {
        Assert.Null(TypeOf(Synth("TON(DwellTimer, IN := Go, PT := T#100MS)"), "T#100MS"));
    }

    // A tag operand is unaffected by the port type — it mints an Access, never a Constant.
    [Fact]
    public void TagOperand_IsNotAffectedByThePortType()
    {
        var sidecar = Synth("MOVE(EN := TRUE, IN := Source) => Dest", Tags(("Dest", "DWord"), ("Source", "DWord")));

        Assert.Empty(sidecar.ConstantUIds);
    }

    // ---- CONVERT: the fifth site, whose SrcType was computed ten lines below the operand ----

    [Fact]
    public void ConvertLiteralInput_CarriesTheConvertsOwnSrcType()
    {
        // No tag IN, so SrcType falls back to the grounded Real idiom — and the literal must agree
        // with it rather than being typed Int by its digits.
        Assert.Equal("Real", TypeOf(Synth("CONVERT(EN := TRUE, IN := 5) => Dest", Tags(("Dest", "DInt"))), "5"));
    }
}
