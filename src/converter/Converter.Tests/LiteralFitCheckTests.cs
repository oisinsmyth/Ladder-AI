using System;
using System.Collections.Generic;
using System.Linq;
using Converter.Ir;
using Converter.Preflight;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `literal-fit` (2026-08-13) — the pre-flight check for a literal too wide for the member or
/// parameter it is written into.
///
/// It exists because the answer to *"which `converter review` rule should have caught the build-stamp
/// defect?"* is **none** — `docs/06-lad-conventions.md` has no rule about a literal fitting its
/// destination type, so no review rule failed; there was never one to fail. Pre-flight's own stated
/// job is the known, recurring import/compile error classes, and this is one: measured, a
/// `MOVE(EN := TRUE, IN := 70000)` into an `Int` member was reported CLEAN by pre-flight and is
/// rejected by TIA.
///
/// Every case below is asserted BOTH ways — the check that only ever fires is as useless as the one
/// that never does, and the "stays quiet" half is what keeps a finding here worth reading.
/// </summary>
public class LiteralFitCheckTests
{
    private static IrBlock Block(string body) =>
        IrParser.ParseBlockWithoutSidecar(
            "BLOCK FC FC_T\nROOTID 0\nNUMBER 1\nLANGUAGE LAD\nTITLE \"T\"\nCOMMENT \"C\"\n\n"
            + "INTERFACE\n  INPUT\n  OUTPUT\n  TEMP\n  CONSTANT\n\n"
            + $"NETWORK 1 \"N\"\n  {body}\n");

    private static TagTypeRegistry Tags(params (string Name, string Type)[] tags) =>
        TagTypeRegistry.FromSources(
            Array.Empty<DbSource>(), Array.Empty<PlcTypeSource>(),
            tags.Select((t, i) => new PlcTagSource(i.ToString(), t.Name, t.Type, $"%MD{i * 4}", true, true, true, null)).ToArray());

    private static CalleeInterfaceRegistry Callee(string name, params (string Name, string Type)[] inputs) =>
        CalleeInterfaceRegistry.FromBlocks(new[]
        {
            new IrBlock("0", "FB", name, 1, "LAD", null, Array.Empty<IrNetwork>(),
                InputMembers: inputs.Select(p => new DbMember(p.Name, p.Type, Retain: false, StartValue: null)).ToArray()),
        });

    private static IReadOnlyList<PreflightFinding> Check(
        string body, TagTypeRegistry? tags = null, CalleeInterfaceRegistry? callees = null) =>
        LiteralFitCheck.Check(
            Block(body),
            tags ?? Tags(),
            callees ?? CalleeInterfaceRegistry.Empty).ToList();

    // ---- fires ----

    [Theory]
    [InlineData("70000", "Int")]
    [InlineData("-40000", "Int")]
    [InlineData("300", "Byte")]
    [InlineData("-1", "UInt")]
    [InlineData("5000000000", "DInt")]
    public void DecimalLiteralOutsideTheDestinationRange_IsAFinding(string literal, string destType)
    {
        var finding = Assert.Single(Check($"MOVE(EN := TRUE, IN := {literal}) => Dest", Tags(("Dest", destType))));

        Assert.Equal("literal-fit", finding.Check);
        Assert.Contains(literal, finding.Description);
        Assert.Contains(destType, finding.Description);
    }

    // The worked example: a 32-bit stamp written into a 16-bit member. Wrong under any reading of
    // signedness, which is exactly why the base-prefixed test is a WIDTH test.
    [Theory]
    [InlineData("16#A93F2C71", "Int")]
    [InlineData("16#A93F2C71", "Word")]
    [InlineData("16#1FF", "Byte")]
    [InlineData("2#100000000", "Byte")]
    public void BasePrefixedLiteralWiderThanTheDestination_IsAFinding(string literal, string destType)
    {
        var finding = Assert.Single(Check($"MOVE(EN := TRUE, IN := {literal}) => Dest", Tags(("Dest", destType))));

        Assert.Equal("literal-fit", finding.Check);
        Assert.Contains("bits", finding.Description);
    }

    // The site the defect was actually measured at.
    [Fact]
    public void CallArgumentWiderThanTheCalleesParam_IsAFinding()
    {
        var finding = Assert.Single(Check(
            "CALL FB_Reg(iDB_Reg, EN := TRUE, Stamp := 16#A93F2C71)",
            callees: Callee("FB_Reg", ("Stamp", "Int"))));

        Assert.Equal("literal-fit", finding.Check);
        Assert.Contains("FB_Reg", finding.Description);
        Assert.Contains("Stamp", finding.Description);
    }

    // ---- stays quiet ----

    // The whole point of the phase-2 register: a 32-bit stamp into a 32-bit destination is CORRECT
    // and must not be flagged. A check that rejected this would block the thing it exists to protect.
    [Theory]
    [InlineData("16#A93F2C71", "DWord")]
    [InlineData("16#A93F2C71", "UDInt")]
    [InlineData("16#89", "Word")]
    [InlineData("16#7F", "Byte")]
    public void BasePrefixedLiteralThatFits_IsNotAFinding(string literal, string destType)
    {
        Assert.Empty(Check($"MOVE(EN := TRUE, IN := {literal}) => Dest", Tags(("Dest", destType))));
    }

    [Theory]
    [InlineData("0", "Int")]
    [InlineData("32767", "Int")]
    [InlineData("-32768", "Int")]
    [InlineData("65535", "UInt")]
    [InlineData("4294967295", "UDInt")]
    public void DecimalLiteralAtTheBoundary_IsNotAFinding(string literal, string destType)
    {
        Assert.Empty(Check($"MOVE(EN := TRUE, IN := {literal}) => Dest", Tags(("Dest", destType))));
    }

    // No opinion is not a finding — and equally, not a claim of fitness. A destination this check
    // cannot decide about is skipped outright rather than guessed at.
    [Theory]
    [InlineData("MOVE(EN := TRUE, IN := T#5S) => Dest", "Time")]
    [InlineData("MOVE(EN := TRUE, IN := 1.5) => Dest", "Real")]
    [InlineData("MOVE(EN := TRUE, IN := 70000) => Dest", "Real")]
    public void UndecidableDestinationOrLiteral_IsNotAFinding(string body, string destType)
    {
        Assert.Empty(Check(body, Tags(("Dest", destType))));
    }

    [Fact]
    public void UnresolvableDestination_IsNotAFinding()
    {
        // An unresolved tag root is already a `tag` finding; this check must not double-report it,
        // and must not pretend to have judged it either.
        Assert.Empty(Check("MOVE(EN := TRUE, IN := 70000) => Unknown"));
    }

    [Fact]
    public void TagOperand_IsNotAFinding()
    {
        Assert.Empty(Check("MOVE(EN := TRUE, IN := Source) => Dest", Tags(("Dest", "Int"), ("Source", "Int"))));
    }

    [Fact]
    public void UnresolvableCallee_IsNotAFinding()
    {
        // Already reported as a `call` finding by the runner; silence here is deliberate.
        Assert.Empty(Check("CALL FB_Missing(iDB_X, EN := TRUE, P := 70000)"));
    }
}
