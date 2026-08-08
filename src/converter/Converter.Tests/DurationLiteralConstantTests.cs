using System.Linq;
using Converter.Ir;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-54 (2026-08-08). A DURATION LITERAL IS ALWAYS A TypedConstant — never a LiteralConstant
/// carrying <c>ConstantType="Time"</c> — wherever it appears.
///
/// TIA rejects the other form outright, at IMPORT rather than at compile, and the whole block
/// import fails:
///     "The value 'T#0MS' cannot be set for the parameter of the type 'Time'"
///
/// Found live, importing a `MOVE(IN := T#0MS)` into a Time member — the first duration literal
/// this corpus had ever placed anywhere but a TON's PT. The old rule keyed the decision on
/// POSITION (`typedConstant` is passed true only at a TON's PT), so everywhere else the type came
/// from the operation's own resolved type — which for a MOVE into a Time member is exactly the one
/// value that must never be written. The thing that actually decides is the LITERAL'S OWN KIND.
///
/// The accepted shape is confirmed against a real TIA export (simatic-ml/reference/TimerSample.xml):
/// Scope="TypedConstant", a bare &lt;ConstantValue&gt;, and no &lt;ConstantType&gt; child at all.
/// </summary>
public class DurationLiteralConstantTests
{
    // The exact shape that failed the live import: a duration literal as a MOVE input, which is
    // not a TON PT and therefore took the LiteralConstant path.
    [Fact]
    public void DurationLiteral_AsMoveInput_IsTypedConstant_NotLiteralConstantWithTimeType()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"N\"\n  MOVE(EN := Clear, IN := T#0MS) => IO.Elapsed\n");

        var constant = Assert.Single(SidecarSynthesizer.Synthesize(network).ConstantUIds!);

        Assert.Equal("T#0MS", constant.Value);
        Assert.Null(constant.ConstantType); // null ConstantType is what makes the writer emit TypedConstant
    }

    // The case that always worked must keep working — a TON's PT is the shape the original rule
    // was built around.
    [Fact]
    public void DurationLiteral_AsTimerPreset_StaysTypedConstant()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"N\"\n  TON(DwellTimer, IN := Run, PT := T#100MS)\n");

        var constant = Assert.Single(SidecarSynthesizer.Synthesize(network).ConstantUIds!);

        Assert.Equal("T#100MS", constant.Value);
        Assert.Null(constant.ConstantType);
    }

    // The guard that matters: fixing the duration case must not strip ConstantType from every
    // other literal. A plain number in the same position still carries its inferred type.
    [Fact]
    public void NonDurationLiteral_InTheSamePosition_StillCarriesItsConstantType()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"N\"\n  MOVE(EN := Clear, IN := 0) => IO.Count\n");

        var constant = Assert.Single(SidecarSynthesizer.Synthesize(network).ConstantUIds!);

        Assert.Equal("0", constant.Value);
        Assert.NotNull(constant.ConstantType);
    }

    // Durations of any magnitude, in the spelling the IR parser actually recognises.
    [Theory]
    [InlineData("T#0MS")]
    [InlineData("T#5S")]
    [InlineData("T#100MS")]
    [InlineData("T#2M")]
    [InlineData("T#40H")]
    public void DurationLiteralsOfAnyMagnitude_AreTypedConstant(string literal)
    {
        var network = IrParser.ParseNetworkOnly(
            $"NETWORK 1 \"N\"\n  MOVE(EN := Clear, IN := {literal}) => IO.Elapsed\n");

        Assert.Null(Assert.Single(SidecarSynthesizer.Synthesize(network).ConstantUIds!).ConstantType);
    }

    // SCOPE NOTE, and a separate latent defect found while testing this one (2026-08-08).
    //
    // `IsDurationLiteral` deliberately also accepts `t#` lower case and the `LT#`/`TIME#`/`LTIME#`
    // forms — but THE IR PARSER DOES NOT PRODUCE THEM AS LITERALS. It recognises only `T#`
    // upper case; every other spelling is parsed as a TAG REFERENCE and emitted as
    // `<Component Name="t#5s" />`, a tag by that name which does not exist.
    //
    // That is a real defect and it is NOT this one. It is also LOUD rather than silent — TIA
    // rejects an undefined tag at import, the same way it rejected the ConstantType form — so it
    // announces itself rather than corrupting anything. It is left alone deliberately: this corpus
    // writes `T#` upper case throughout, and widening literal recognition risks reclassifying a
    // legitimately-named tag, which would be a silent failure traded for a loud one.
    //
    // The defensive spellings in `IsDurationLiteral` cost nothing and mean the fix is already
    // correct if the parser is ever widened.
    [Fact]
    public void LowerCaseDurationSpelling_IsNotYetParsedAsALiteral_DocumentedLimitation()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"N\"\n  MOVE(EN := Clear, IN := t#5s) => IO.Elapsed\n");

        // No constant at all — it became a tag reference. Asserted so that widening the parser
        // trips this test and forces the decision to be made deliberately rather than noticed.
        Assert.Empty(SidecarSynthesizer.Synthesize(network).ConstantUIds ?? []);
    }
}
