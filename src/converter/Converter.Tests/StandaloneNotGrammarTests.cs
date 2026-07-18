using Converter.Ir;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Gap H (2026-07-18): the readable grammar distinguishes a negated contact `NOT A` from a standalone
/// Not part `NOT (A)`. Both are real, logically identical LAD shapes that `to-ir` used to collapse to
/// `NOT A`; the parenthesised form now round-trips and synthesises as a NotStep, the bare form as a
/// negated contact.
/// </summary>
public class StandaloneNotGrammarTests
{
    [Fact]
    public void Parse_BareNot_IsNegatedContact()
    {
        var network = IrParser.ParseNetworkOnly("NETWORK 1 \"T\"\n  COIL X := NOT A AND B\n");
        var and = Assert.IsType<Expr.And>(Assert.Single(network.Assignments).Condition);
        var not = Assert.IsType<Expr.Not>(and.Operands[0]);
        Assert.False(not.Standalone);
    }

    [Fact]
    public void Parse_ParenthesisedNot_IsStandalone()
    {
        var network = IrParser.ParseNetworkOnly("NETWORK 1 \"T\"\n  COIL X := NOT (A) AND B\n");
        var and = Assert.IsType<Expr.And>(Assert.Single(network.Assignments).Condition);
        var not = Assert.IsType<Expr.Not>(and.Operands[0]);
        Assert.True(not.Standalone);
    }

    [Theory]
    [InlineData("  COIL X := NOT A AND B\n")]       // negated contact
    [InlineData("  COIL X := NOT (A) AND B\n")]     // standalone Not of a single tag
    [InlineData("  COIL X := NOT (A OR B) AND C\n")] // standalone Not of a compound
    public void RoundTrip_PreservesTheDistinction(string coilLine)
    {
        var text = "NETWORK 1 \"T\"\n" + coilLine;
        var reserialized = IrSerializer.SerializeNetworkOnly(IrParser.ParseNetworkOnly(text));
        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Synthesize_StandaloneNot_ProducesNotStep_BareNot_ProducesNegatedContact()
    {
        var standalone = SidecarSynthesizer.Synthesize(
            IrParser.ParseNetworkOnly("NETWORK 1 \"T\"\n  COIL X := NOT (A) AND B\n"));
        Assert.IsType<ChainStepSidecar.NotStep>(Assert.Single(standalone.Assignments).Steps[0]);

        var negated = SidecarSynthesizer.Synthesize(
            IrParser.ParseNetworkOnly("NETWORK 1 \"T\"\n  COIL X := NOT A AND B\n"));
        var contact = Assert.IsType<ChainStepSidecar.ContactStep>(Assert.Single(negated.Assignments).Steps[0]);
        Assert.True(contact.Negated);
    }
}
