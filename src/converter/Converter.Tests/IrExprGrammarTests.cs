using Converter.Ir;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Standalone precedence-grammar tests for the IR text's boolean expressions — S1 item 11,
/// 2026-07-11/12. Once an OR-merge branch could be a compound expression (not just a single
/// tag), the grammar needed real operator precedence: `AND` binds tighter than `OR` (confirmed
/// with the project owner), with parentheses emitted only where precedence alone would misparse.
/// The old parser (`IrParser.ParseExpr`) was a naive `.Contains(" AND ")`/`.Contains(" OR ")`
/// substring split that never actually handled mixed AND+OR correctly — it just never got
/// exercised by anything more complex than a flat AND-chain or an OR of single leaves until now.
/// These tests exercise the grammar directly (constructing `Expr` trees by hand and round-
/// tripping through `IrSerializer`/`IrParser`), independent of `GraphReducer` — the reducer's own
/// OR-merge tests (`OrMergeAndNegationTests`/`ComparisonTests`) additionally prove real shapes
/// produce the right trees in the first place.
/// </summary>
public class IrExprGrammarTests
{
    private static string SerializeCondition(Expr condition)
    {
        var network = new IrNetwork(1, "Grammar test", new[] { new CoilAssignment("Output1", condition) });
        return IrSerializer.SerializeNetworkOnly(network);
    }

    private static Expr ParseCondition(string text)
    {
        var network = IrParser.ParseNetworkOnly(text);
        return Assert.Single(network.Assignments).Condition;
    }

    [Fact]
    public void Serialize_AndInsideOr_NeedsNoParens()
    {
        var condition = new Expr.Or(new Expr[] { new Expr.And(new Expr[] { new Expr.TagRef("A"), new Expr.TagRef("B") }), new Expr.TagRef("C") });

        var text = SerializeCondition(condition);

        Assert.Equal("NETWORK 1 \"Grammar test\"\n  COIL Output1 := A AND B OR C\n", text);
    }

    [Fact]
    public void Parse_AndInsideOr_RoundTripsToSameTree()
    {
        var original = new Expr.Or(new Expr[] { new Expr.And(new Expr[] { new Expr.TagRef("A"), new Expr.TagRef("B") }), new Expr.TagRef("C") });
        var text = SerializeCondition(original);

        var parsed = ParseCondition(text);

        var or = Assert.IsType<Expr.Or>(parsed);
        Assert.Equal(2, or.Operands.Count);
        var and = Assert.IsType<Expr.And>(or.Operands[0]);
        Assert.Equal("A", Assert.IsType<Expr.TagRef>(and.Operands[0]).Path);
        Assert.Equal("B", Assert.IsType<Expr.TagRef>(and.Operands[1]).Path);
        Assert.Equal("C", Assert.IsType<Expr.TagRef>(or.Operands[1]).Path);
    }

    [Fact]
    public void Serialize_OrInsideAnd_NeedsParens()
    {
        var condition = new Expr.And(new Expr[] { new Expr.Or(new Expr[] { new Expr.TagRef("A"), new Expr.TagRef("B") }), new Expr.TagRef("C") });

        var text = SerializeCondition(condition);

        Assert.Equal("NETWORK 1 \"Grammar test\"\n  COIL Output1 := (A OR B) AND C\n", text);
    }

    [Fact]
    public void Parse_OrInsideAnd_RoundTripsToSameTree()
    {
        var original = new Expr.And(new Expr[] { new Expr.Or(new Expr[] { new Expr.TagRef("A"), new Expr.TagRef("B") }), new Expr.TagRef("C") });
        var text = SerializeCondition(original);

        var parsed = ParseCondition(text);

        var and = Assert.IsType<Expr.And>(parsed);
        Assert.Equal(2, and.Operands.Count);
        var or = Assert.IsType<Expr.Or>(and.Operands[0]);
        Assert.Equal("A", Assert.IsType<Expr.TagRef>(or.Operands[0]).Path);
        Assert.Equal("B", Assert.IsType<Expr.TagRef>(or.Operands[1]).Path);
        Assert.Equal("C", Assert.IsType<Expr.TagRef>(and.Operands[1]).Path);
    }

    [Fact]
    public void Serialize_NotWrappingOr_NeedsParens()
    {
        var condition = new Expr.Not(new Expr.Or(new Expr[] { new Expr.TagRef("A"), new Expr.TagRef("B") }));

        var text = SerializeCondition(condition);

        Assert.Equal("NETWORK 1 \"Grammar test\"\n  COIL Output1 := NOT (A OR B)\n", text);
    }

    [Fact]
    public void Serialize_NotWrappingAnd_NeedsParens()
    {
        var condition = new Expr.Not(new Expr.And(new Expr[] { new Expr.TagRef("A"), new Expr.TagRef("B") }));

        var text = SerializeCondition(condition);

        Assert.Equal("NETWORK 1 \"Grammar test\"\n  COIL Output1 := NOT (A AND B)\n", text);
    }

    [Fact]
    public void Parse_NotWrappingAnd_RoundTripsToSameTree()
    {
        var original = new Expr.Not(new Expr.And(new Expr[] { new Expr.TagRef("A"), new Expr.TagRef("B") }));
        var text = SerializeCondition(original);

        var parsed = ParseCondition(text);

        var not = Assert.IsType<Expr.Not>(parsed);
        var and = Assert.IsType<Expr.And>(not.Operand);
        Assert.Equal("A", Assert.IsType<Expr.TagRef>(and.Operands[0]).Path);
        Assert.Equal("B", Assert.IsType<Expr.TagRef>(and.Operands[1]).Path);
    }

    [Fact]
    public void Serialize_DoubleNot_NeedsNoParens()
    {
        var condition = new Expr.Not(new Expr.Not(new Expr.TagRef("A")));

        var text = SerializeCondition(condition);

        Assert.Equal("NETWORK 1 \"Grammar test\"\n  COIL Output1 := NOT NOT A\n", text);
    }

    [Fact]
    public void Parse_DoubleNot_RoundTripsToSameTree()
    {
        var parsed = ParseCondition("NETWORK 1 \"Grammar test\"\n  COIL Output1 := NOT NOT A\n");

        var outerNot = Assert.IsType<Expr.Not>(parsed);
        var innerNot = Assert.IsType<Expr.Not>(outerNot.Operand);
        Assert.Equal("A", Assert.IsType<Expr.TagRef>(innerNot.Operand).Path);
    }

    [Fact]
    public void Parse_NotInsideAnd_BindsTighterThanAnd()
    {
        // "NOT A AND B" means "(NOT A) AND B", not "NOT (A AND B)" — NOT only ever consumes the
        // single unary/primary immediately following it, never extending across " AND "/" OR ".
        var parsed = ParseCondition("NETWORK 1 \"Grammar test\"\n  COIL Output1 := NOT A AND B\n");

        var and = Assert.IsType<Expr.And>(parsed);
        var not = Assert.IsType<Expr.Not>(and.Operands[0]);
        Assert.Equal("A", Assert.IsType<Expr.TagRef>(not.Operand).Path);
        Assert.Equal("B", Assert.IsType<Expr.TagRef>(and.Operands[1]).Path);
    }

    [Fact]
    public void Parse_ComparisonInsideAnd_ScansOnlyItsOwnSpan()
    {
        // The comparison-token scan must stop at the " AND " boundary, not consume past it.
        var parsed = ParseCondition("NETWORK 1 \"Grammar test\"\n  COIL Output1 := A = 1 AND B\n");

        var and = Assert.IsType<Expr.And>(parsed);
        var compare = Assert.IsType<Expr.Compare>(and.Operands[0]);
        Assert.Equal("=", compare.Operator);
        Assert.Equal("A", Assert.IsType<Expr.TagRef>(compare.Left).Path);
        Assert.Equal("1", Assert.IsType<Expr.Literal>(compare.Right).Value);
        Assert.Equal("B", Assert.IsType<Expr.TagRef>(and.Operands[1]).Path);
    }

    [Fact]
    public void Serialize_OrOfOr_FlattensWithNoParens()
    {
        var condition = new Expr.Or(new Expr[]
        {
            new Expr.Or(new Expr[] { new Expr.TagRef("A"), new Expr.TagRef("B") }),
            new Expr.TagRef("C"),
        });

        var text = SerializeCondition(condition);

        Assert.Equal("NETWORK 1 \"Grammar test\"\n  COIL Output1 := A OR B OR C\n", text);
    }

    [Fact]
    public void Parse_ThreeWayOr_ProducesFlatOrNotNested()
    {
        // Nested Or-in-Or serializes with no parens (associative, same precedence) — parsing it
        // back necessarily produces a flat 3-operand Or, not the original nested shape. This is
        // expected and semantically fine (the IR text, not the Expr tree, is the stability
        // contract — GraphReducer's own sidecar is what preserves exact source nesting).
        var parsed = ParseCondition("NETWORK 1 \"Grammar test\"\n  COIL Output1 := A OR B OR C\n");

        var or = Assert.IsType<Expr.Or>(parsed);
        Assert.Equal(3, or.Operands.Count);
        Assert.Equal("A", Assert.IsType<Expr.TagRef>(or.Operands[0]).Path);
        Assert.Equal("B", Assert.IsType<Expr.TagRef>(or.Operands[1]).Path);
        Assert.Equal("C", Assert.IsType<Expr.TagRef>(or.Operands[2]).Path);
    }

    [Fact]
    public void Parse_DeeplyNestedMixedPrecedenceWithParens_RoundTripsExactly()
    {
        // "A AND (B OR C) OR D" == Or(And(A, Or(B, C)), D) — proves parens correctly scope a
        // nested Or back out of an And, inside an outer Or, all in one expression.
        var original = new Expr.Or(new Expr[]
        {
            new Expr.And(new Expr[] { new Expr.TagRef("A"), new Expr.Or(new Expr[] { new Expr.TagRef("B"), new Expr.TagRef("C") }) }),
            new Expr.TagRef("D"),
        });
        var text = SerializeCondition(original);

        Assert.Equal("NETWORK 1 \"Grammar test\"\n  COIL Output1 := A AND (B OR C) OR D\n", text);

        var parsed = ParseCondition(text);

        var outerOr = Assert.IsType<Expr.Or>(parsed);
        Assert.Equal(2, outerOr.Operands.Count);
        var and = Assert.IsType<Expr.And>(outerOr.Operands[0]);
        Assert.Equal("A", Assert.IsType<Expr.TagRef>(and.Operands[0]).Path);
        var innerOr = Assert.IsType<Expr.Or>(and.Operands[1]);
        Assert.Equal("B", Assert.IsType<Expr.TagRef>(innerOr.Operands[0]).Path);
        Assert.Equal("C", Assert.IsType<Expr.TagRef>(innerOr.Operands[1]).Path);
        Assert.Equal("D", Assert.IsType<Expr.TagRef>(outerOr.Operands[1]).Path);
    }
}
