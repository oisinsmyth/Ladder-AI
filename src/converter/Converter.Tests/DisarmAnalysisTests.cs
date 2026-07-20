using Converter.Ir;
using Converter.Trace;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-25 v2 (2026-07-20): the disarm constant-folder. A write is disarmed when its guard is provably
/// constant-false via `NOT AlwaysTrue`. Bare `AlwaysTrue` and the rail sentinel are armed (true), never
/// flagged; an unknown gate is not provably false.
/// </summary>
public class DisarmAnalysisTests
{
    private static Expr Not(Expr e) => new Expr.Not(e);
    private static Expr Tag(string p) => new Expr.TagRef(p);

    [Fact]
    public void NotAlwaysTrue_Standalone_IsProvablyFalse() =>
        Assert.True(DisarmAnalysis.IsProvablyFalse(Not(Tag("AlwaysTrue"))));

    [Fact]
    public void NotAlwaysTrue_AsAndConjunct_IsProvablyFalse()
    {
        // The real corpus shape: IO.Step = 50 AND SpinUpTimer.Q AND NOT AlwaysTrue
        var guard = new Expr.And(new Expr[]
        {
            new Expr.Compare("=", Tag("IO.Step"), new Expr.Literal("50")),
            Tag("SpinUpTimer.Q"),
            Not(Tag("AlwaysTrue")),
        });
        Assert.True(DisarmAnalysis.IsProvablyFalse(guard));
    }

    [Fact]
    public void BareAlwaysTrue_IsArmed_NotProvablyFalse() =>
        Assert.False(DisarmAnalysis.IsProvablyFalse(Tag("AlwaysTrue")));

    [Fact]
    public void RailSentinel_EmptyAnd_IsArmed_NotProvablyFalse() =>
        Assert.False(DisarmAnalysis.IsProvablyFalse(new Expr.And(System.Array.Empty<Expr>())));

    [Fact]
    public void NormalGate_IsNotProvablyFalse()
    {
        Assert.False(DisarmAnalysis.IsProvablyFalse(Tag("SomeBit")));
        Assert.False(DisarmAnalysis.IsProvablyFalse(Not(Tag("SomeBit")))); // NOT unknown = unknown
        Assert.False(DisarmAnalysis.IsProvablyFalse(new Expr.And(new Expr[] { Tag("A"), Tag("B") })));
    }

    [Fact]
    public void NullGuard_IsNotProvablyFalse() =>
        Assert.False(DisarmAnalysis.IsProvablyFalse(null));

    [Fact]
    public void FalseLiteral_IsProvablyFalse() =>
        Assert.True(DisarmAnalysis.IsProvablyFalse(new Expr.Literal("FALSE")));
}
