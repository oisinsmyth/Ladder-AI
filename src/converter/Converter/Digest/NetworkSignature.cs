using System.Security.Cryptography;
using System.Text;
using Converter.Ir;

namespace Converter.Digest;

// FI-23 (docs/16-future-ideas.md): a normalized structural signature per network. Where
// SummarizeStatements counts each statement kind (shape-blind: "coil:2, timer:1"), this captures
// the *shape* — statement kinds AND order, plus the canonicalized structure of every
// condition/operand Expr tree — then hashes it. N copy-pasted networks collapse to one signature;
// the one that drifted (an extra rung, a flipped compare, a changed constant) stands out. Serves
// the explain-plc-block skill's "describe the template once, verify EVERY instance" method, done
// by hand today.
//
// Same "derived fresh, orientation-only" posture as the rest of digest (docs/15 isolation model):
// a fingerprint is an explanation aid, never review input — no policy change here.
//
// Design choices baked into the canonical form:
//  * Tag-name-independent. Every Expr.TagRef, and every bare dest/coil/instance string, abstracts
//    to the fixed token TAG. Two networks that are structurally identical but wire different tags
//    produce the SAME signature — that's the point (find the template, not the wiring).
//  * Literal VALUES are KEPT (LIT:<value>), not abstracted. For copy-paste-drift detection the
//    more useful default is to surface a network that copied a template but changed a constant
//    (Step = 10 vs Step = 20 give DIFFERENT signatures) — exactly the drift worth catching. The
//    alternative (LIT with no value, collapsing step-copy networks) is the wrong default here.
//  * Deterministic / canonical. For semantically-unordered operand lists (And/Or), the
//    canonicalized child strings are sorted before emitting, so operand order can't change the
//    hash. Ordered nodes stay ordered: Compare (>=/<=/>/< aren't symmetric), and statement order
//    within the network (it's part of the shape — statements are emitted in list order per kind,
//    kinds in the same fixed order SummarizeStatements uses).
//  * CalcStatement.Equation is deliberately EXCLUDED from the signature: it's a free-text formula
//    that embeds operand names, so folding it in would break tag-independence (same reason
//    TagReferences excludes it). A Calc contributes its kind, en-source, and input structure only.
public static class NetworkSignature
{
    // The short hash shown per network. SHA-256 of the canonical string, first 6 bytes as 12 lower
    // hex chars — stable across processes (unlike string.GetHashCode), collision-negligible for the
    // handful of networks in one block. Mirrors Normalizer.Hash's own SHA-256 precedent.
    public static string Compute(IrNetwork network)
    {
        var canonical = Canonical(network);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(digest, 0, 6).ToLowerInvariant();
    }

    // Exposed for tests/debugging: the pre-hash canonical string the signature is derived from.
    internal static string Canonical(IrNetwork network)
    {
        var tokens = new List<string>();

        foreach (var c in network.Assignments)
        {
            tokens.Add($"coil{c.Kind}({Canon(c.Condition)})");
        }

        foreach (var t in network.Timers)
        {
            tokens.Add(t.Reset is null
                ? $"timer{t.Kind}({Canon(t.In)},{Canon(t.Pt)})"
                : $"timer{t.Kind}({Canon(t.In)},{Canon(t.Pt)},{Canon(t.Reset)})");
        }

        foreach (var m in network.Moves)
        {
            tokens.Add($"move({Canon(m.En)},{Canon(m.In)})");
        }

        foreach (var w in network.WordAnds)
        {
            tokens.Add($"wand({Canon(w.En)},{Inputs(w.Inputs)})");
        }

        foreach (var call in network.Calls)
        {
            tokens.Add($"call:{call.BlockName}({Canon(call.En)}|{CallArgs(call.Arguments)})");
        }

        foreach (var mul in network.Muls)
        {
            tokens.Add($"mul{mul.Kind}({Canon(mul.En)},{Inputs(mul.Inputs)})");
        }

        foreach (var cv in network.Converts)
        {
            tokens.Add($"convert({Canon(cv.En)},{Canon(cv.In)})");
        }

        foreach (var s in network.Swaps)
        {
            tokens.Add($"swap({Canon(s.En)},{Canon(s.In)})");
        }

        foreach (var a in network.AbsStatements)
        {
            tokens.Add($"abs({Canon(a.En)},{Canon(a.In)})");
        }

        foreach (var l in network.Limits)
        {
            tokens.Add($"limit({Canon(l.En)},{Canon(l.Min)},{Canon(l.In)},{Canon(l.Max)})");
        }

        foreach (var ts in network.TSubs)
        {
            tokens.Add($"tsub({Canon(ts.En)},{Canon(ts.In1)},{Canon(ts.In2)})");
        }

        foreach (var tc in network.TConvs)
        {
            tokens.Add($"tconv({Canon(tc.En)},{Canon(tc.In)})");
        }

        foreach (var calc in network.Calcs)
        {
            // Equation deliberately excluded (embeds operand names — see class comment).
            tokens.Add($"calc({Canon(calc.En)},{Inputs(calc.Inputs)})");
        }

        foreach (var mbv in network.MoveBlkVariants)
        {
            tokens.Add($"moveblk({Canon(mbv.En)},{Canon(mbv.Src)},{Canon(mbv.Count)},{Canon(mbv.SrcIndex)},{Canon(mbv.DestIndex)})");
        }

        foreach (var wait in network.Waits)
        {
            tokens.Add($"wait({Canon(wait.En)},{Canon(wait.Wt)})");
        }

        foreach (var fill in network.FillBlockIs)
        {
            tokens.Add($"fillblk({Canon(fill.En)},{Canon(fill.In)},{Canon(fill.Count)})");
        }

        foreach (var mm in network.ModbusMasters)
        {
            tokens.Add($"mbmaster({Canon(mm.En)},{Canon(mm.Req)},{Canon(mm.MbAddr)},{Canon(mm.Mode)},{Canon(mm.DataAddr)},{Canon(mm.DataLen)},{Canon(mm.DataPtr)})");
        }

        foreach (var mcl in network.ModbusCommLoads)
        {
            tokens.Add($"mbcommload({Canon(mcl.En)},{Canon(mcl.Req)},{Canon(mcl.Port)},{Canon(mcl.Baud)},{Canon(mcl.Parity)},{Canon(mcl.RespTo)},{Canon(mcl.MbDb)})");
        }

        // Statement order within the network is part of the shape; join with a fixed separator.
        return string.Join(";", tokens);
    }

    private static string Inputs(IReadOnlyList<Expr> inputs) => string.Join(",", inputs.Select(Canon));

    private static string CallArgs(IReadOnlyList<CallArgument> args) => string.Join(",", args.Select(arg => arg switch
    {
        CallArgument.InputArg input => Canon(input.Value),
        CallArgument.OutputArg => "out",
        _ => "?",
    }));

    // An en-source: an ordinary boolean condition tree, or the "preceding instruction's ENO"
    // chaining case — structurally distinct, so the token distinguishes them.
    private static string Canon(EnSource en) => en switch
    {
        EnSource.Condition c => Canon(c.Value),
        EnSource.PrecedingEno => "ENO",
        _ => "?",
    };

    // Canonical string for one Expr tree: node kinds only, tag names abstracted to TAG, literal
    // values kept. And/Or operands are sorted (order-independent); Compare stays ordered; a
    // standalone Not part is distinguished from a negated-contact Not (they're drawn differently
    // and are genuinely different shapes).
    private static string Canon(Expr expr) => expr switch
    {
        Expr.TagRef => "TAG",
        Expr.Literal lit => $"LIT:{lit.Value}",
        Expr.And and => $"and[{Sorted(and.Operands)}]",
        Expr.Or or => $"or[{Sorted(or.Operands)}]",
        Expr.Not { Standalone: true } not => $"nots({Canon(not.Operand)})",
        Expr.Not not => $"not({Canon(not.Operand)})",
        Expr.Compare cmp => $"cmp{cmp.Operator}({Canon(cmp.Left)},{Canon(cmp.Right)})",
        _ => "?",
    };

    private static string Sorted(IReadOnlyList<Expr> operands) =>
        string.Join(",", operands.Select(Canon).OrderBy(s => s, StringComparer.Ordinal));
}
