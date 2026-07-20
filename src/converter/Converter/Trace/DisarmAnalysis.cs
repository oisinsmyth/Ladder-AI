using Converter.Ir;

namespace Converter.Trace;

// FI-25 v2: is a write's guard a placeholder-disarm (provably constant-false), so the write can never
// fire? The site convention is `NOT AlwaysTrue` — a negated reference to the S7 always-on system bit
// `AlwaysTrue` (%M1.2) — standing alone as the whole guard or ANDed as one conjunct. Detected by a
// three-valued constant-fold with `AlwaysTrue ⇒ true` and empty-`And`/`Or` ⇒ true (the rail sentinel);
// a bare `AlwaysTrue` is armed (true), never flagged. Pure/testable — no graph, no IO.
public static class DisarmAnalysis
{
    // The S7 always-on memory bit. `NOT AlwaysTrue` is the disarm idiom; bare `AlwaysTrue` is armed.
    private const string AlwaysTrueTag = "AlwaysTrue";

    private enum Tri
    {
        True,
        False,
        Unknown,
    }

    // Disarmed ⇔ the guard folds to a constant False. Null guard (a read, or an ENO-chained write with no
    // local condition) is not disarmable — treated as not-provably-false.
    public static bool IsProvablyFalse(Expr? guard) => guard is not null && Fold(guard) == Tri.False;

    private static Tri Fold(Expr expr) => expr switch
    {
        // Empty And/Or is the "wired to rail — always on" sentinel (renders "TRUE").
        Expr.And and => and.Operands.Count == 0 ? Tri.True : FoldAnd(and.Operands),
        Expr.Or or => or.Operands.Count == 0 ? Tri.True : FoldOr(or.Operands),
        Expr.Not not => Negate(Fold(not.Operand)),
        Expr.TagRef tag => IsAlwaysTrue(tag.Path) ? Tri.True : Tri.Unknown,
        Expr.Literal lit => FoldLiteral(lit.Value),
        Expr.Compare => Tri.Unknown, // a comparison's truth needs runtime values
        _ => Tri.Unknown,
    };

    private static Tri FoldAnd(IReadOnlyList<Expr> operands)
    {
        var result = Tri.True;
        foreach (var operand in operands)
        {
            var t = Fold(operand);
            if (t == Tri.False)
            {
                return Tri.False; // any false operand ⇒ the whole AND is false (the NOT AlwaysTrue conjunct)
            }

            if (t == Tri.Unknown)
            {
                result = Tri.Unknown;
            }
        }

        return result; // True only if every operand folded True
    }

    private static Tri FoldOr(IReadOnlyList<Expr> operands)
    {
        var result = Tri.False;
        foreach (var operand in operands)
        {
            var t = Fold(operand);
            if (t == Tri.True)
            {
                return Tri.True;
            }

            if (t == Tri.Unknown)
            {
                result = Tri.Unknown;
            }
        }

        return result; // False only if every operand folded False
    }

    private static Tri Negate(Tri t) => t switch
    {
        Tri.True => Tri.False,
        Tri.False => Tri.True,
        _ => Tri.Unknown,
    };

    // Bare `AlwaysTrue`, or a qualified `<root>.AlwaysTrue`, is the always-on bit.
    private static bool IsAlwaysTrue(string path) =>
        path == AlwaysTrueTag || path.EndsWith("." + AlwaysTrueTag, StringComparison.Ordinal);

    private static Tri FoldLiteral(string value) => value.Trim().ToUpperInvariant() switch
    {
        "TRUE" or "1" => Tri.True,
        "FALSE" or "0" => Tri.False,
        _ => Tri.Unknown,
    };
}
