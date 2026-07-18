namespace Converter.Ir;

// A small, scoped helper enumerating every real tag reference in a network — built for S4's
// convention-review rules (C-005, C-301), which both need to inspect every name actually used,
// not just one instruction kind's own field. Deliberately NOT a general visitor/traversal
// abstraction: this codebase's own established, deliberate style is one hand-rolled loop per
// instruction kind (confirmed consistent across IrParser/IrSerializer/GraphReducer, and an
// explicit 2026-07-14 audit already looked at generalizing this exact "18 independent lists"
// shape and deferred it) — this is a single-purpose enumerator over that existing shape, not new
// infrastructure.
//
// Deliberately excludes: Expr.Literal values (not names — e.g. `16#89`, `T#100MS`),
// CallStatement.BlockName and CallArgument.InputArg.ParamName (identify a callee/parameter, not a
// tag), CalcStatement.Equation (a free-text formula, not a name).
public static class TagReferences
{
    public static IEnumerable<string> AllTagPaths(IrNetwork network)
    {
        foreach (var assignment in network.Assignments)
        {
            yield return assignment.CoilTag;
            foreach (var path in FromExpr(assignment.Condition))
            {
                yield return path;
            }
        }

        foreach (var timer in network.Timers)
        {
            yield return timer.InstancePath;
            foreach (var path in FromExpr(timer.In))
            {
                yield return path;
            }

            foreach (var path in FromExpr(timer.Pt))
            {
                yield return path;
            }

            if (timer.Reset is not null)
            {
                foreach (var path in FromExpr(timer.Reset))
                {
                    yield return path;
                }
            }
        }

        foreach (var move in network.Moves)
        {
            yield return move.DestTag;
            foreach (var path in FromExpr(move.En))
            {
                yield return path;
            }

            foreach (var path in FromExpr(move.In))
            {
                yield return path;
            }
        }

        foreach (var wand in network.WordAnds)
        {
            yield return wand.DestTag;
            foreach (var path in FromExpr(wand.En))
            {
                yield return path;
            }

            foreach (var input in wand.Inputs)
            {
                foreach (var path in FromExpr(input))
                {
                    yield return path;
                }
            }
        }

        foreach (var call in network.Calls)
        {
            if (call.InstancePath is not null)
            {
                yield return call.InstancePath;
            }

            foreach (var path in FromExpr(call.En))
            {
                yield return path;
            }

            foreach (var arg in call.Arguments)
            {
                switch (arg)
                {
                    case CallArgument.InputArg input:
                        foreach (var path in FromExpr(input.Value))
                        {
                            yield return path;
                        }

                        break;
                    case CallArgument.OutputArg output:
                        yield return output.DestTag;
                        break;
                }
            }
        }

        foreach (var mul in network.Muls)
        {
            yield return mul.DestTag;
            foreach (var path in FromEnSource(mul.En))
            {
                yield return path;
            }

            foreach (var input in mul.Inputs)
            {
                foreach (var path in FromExpr(input))
                {
                    yield return path;
                }
            }
        }

        foreach (var convert in network.Converts)
        {
            yield return convert.DestTag;
            foreach (var path in FromEnSource(convert.En))
            {
                yield return path;
            }

            foreach (var path in FromExpr(convert.In))
            {
                yield return path;
            }
        }

        foreach (var swap in network.Swaps)
        {
            yield return swap.DestTag;
            foreach (var path in FromEnSource(swap.En))
            {
                yield return path;
            }

            foreach (var path in FromExpr(swap.In))
            {
                yield return path;
            }
        }

        foreach (var abs in network.AbsStatements)
        {
            yield return abs.DestTag;
            foreach (var path in FromEnSource(abs.En))
            {
                yield return path;
            }

            foreach (var path in FromExpr(abs.In))
            {
                yield return path;
            }
        }

        foreach (var limit in network.Limits)
        {
            yield return limit.DestTag;
            foreach (var path in FromEnSource(limit.En))
            {
                yield return path;
            }

            foreach (var path in FromExpr(limit.Min))
            {
                yield return path;
            }

            foreach (var path in FromExpr(limit.In))
            {
                yield return path;
            }

            foreach (var path in FromExpr(limit.Max))
            {
                yield return path;
            }
        }

        foreach (var tsub in network.TSubs)
        {
            yield return tsub.DestTag;
            foreach (var path in FromEnSource(tsub.En))
            {
                yield return path;
            }

            foreach (var path in FromExpr(tsub.In1))
            {
                yield return path;
            }

            foreach (var path in FromExpr(tsub.In2))
            {
                yield return path;
            }
        }

        foreach (var tconv in network.TConvs)
        {
            yield return tconv.DestTag;
            foreach (var path in FromEnSource(tconv.En))
            {
                yield return path;
            }

            foreach (var path in FromExpr(tconv.In))
            {
                yield return path;
            }
        }

        foreach (var calc in network.Calcs)
        {
            yield return calc.DestTag;
            foreach (var path in FromEnSource(calc.En))
            {
                yield return path;
            }

            foreach (var input in calc.Inputs)
            {
                foreach (var path in FromExpr(input))
                {
                    yield return path;
                }
            }

            // calc.Equation is a free-text formula, not a name — deliberately excluded.
        }

        foreach (var mbv in network.MoveBlkVariants)
        {
            yield return mbv.RetValTag;
            yield return mbv.DestTag;
            foreach (var path in FromEnSource(mbv.En))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mbv.Src))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mbv.Count))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mbv.SrcIndex))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mbv.DestIndex))
            {
                yield return path;
            }
        }

        foreach (var wait in network.Waits)
        {
            foreach (var path in FromEnSource(wait.En))
            {
                yield return path;
            }

            foreach (var path in FromExpr(wait.Wt))
            {
                yield return path;
            }
        }

        foreach (var fill in network.FillBlockIs)
        {
            yield return fill.DestTag;
            foreach (var path in FromEnSource(fill.En))
            {
                yield return path;
            }

            foreach (var path in FromExpr(fill.In))
            {
                yield return path;
            }

            foreach (var path in FromExpr(fill.Count))
            {
                yield return path;
            }
        }

        foreach (var mm in network.ModbusMasters)
        {
            yield return mm.InstancePath;
            yield return mm.DoneTag;
            yield return mm.BusyTag;
            yield return mm.ErrorTag;
            yield return mm.StatusTag;
            foreach (var path in FromEnSource(mm.En))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mm.Req))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mm.MbAddr))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mm.Mode))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mm.DataAddr))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mm.DataLen))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mm.DataPtr))
            {
                yield return path;
            }
        }

        foreach (var mcl in network.ModbusCommLoads)
        {
            yield return mcl.InstancePath;
            yield return mcl.DoneTag;
            yield return mcl.ErrorTag;
            yield return mcl.StatusTag;
            foreach (var path in FromEnSource(mcl.En))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mcl.Req))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mcl.Port))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mcl.Baud))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mcl.Parity))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mcl.RespTo))
            {
                yield return path;
            }

            foreach (var path in FromExpr(mcl.MbDb))
            {
                yield return path;
            }
        }
    }

    // Companion to AllTagPaths that yields each Expr-valued field (conditions, EN-source conditions,
    // instruction inputs) as a whole Expr rather than flattening it to tag paths — so a rule that
    // needs expression *structure* (e.g. C-408's "is a .ET operand inside a Compare?") can walk it.
    // Mirrors AllTagPaths' per-statement-kind enumeration; deliberately skips the string dest/instance
    // fields (a coil tag / dest tag / instance path can't hold a Compare).
    public static IEnumerable<Expr> AllExpressions(IrNetwork network)
    {
        foreach (var assignment in network.Assignments)
        {
            yield return assignment.Condition;
        }

        foreach (var timer in network.Timers)
        {
            yield return timer.In;
            yield return timer.Pt;
            if (timer.Reset is not null)
            {
                yield return timer.Reset;
            }
        }

        foreach (var move in network.Moves)
        {
            yield return move.En;
            yield return move.In;
        }

        foreach (var wand in network.WordAnds)
        {
            yield return wand.En;
            foreach (var input in wand.Inputs)
            {
                yield return input;
            }
        }

        foreach (var call in network.Calls)
        {
            yield return call.En;
            foreach (var arg in call.Arguments)
            {
                if (arg is CallArgument.InputArg input)
                {
                    yield return input.Value;
                }
            }
        }

        foreach (var mul in network.Muls)
        {
            foreach (var e in ExprsOfEnSource(mul.En))
            {
                yield return e;
            }

            foreach (var input in mul.Inputs)
            {
                yield return input;
            }
        }

        foreach (var convert in network.Converts)
        {
            foreach (var e in ExprsOfEnSource(convert.En))
            {
                yield return e;
            }

            yield return convert.In;
        }

        foreach (var swap in network.Swaps)
        {
            foreach (var e in ExprsOfEnSource(swap.En))
            {
                yield return e;
            }

            yield return swap.In;
        }

        foreach (var abs in network.AbsStatements)
        {
            foreach (var e in ExprsOfEnSource(abs.En))
            {
                yield return e;
            }

            yield return abs.In;
        }

        foreach (var limit in network.Limits)
        {
            foreach (var e in ExprsOfEnSource(limit.En))
            {
                yield return e;
            }

            yield return limit.Min;
            yield return limit.In;
            yield return limit.Max;
        }

        foreach (var tsub in network.TSubs)
        {
            foreach (var e in ExprsOfEnSource(tsub.En))
            {
                yield return e;
            }

            yield return tsub.In1;
            yield return tsub.In2;
        }

        foreach (var tconv in network.TConvs)
        {
            foreach (var e in ExprsOfEnSource(tconv.En))
            {
                yield return e;
            }

            yield return tconv.In;
        }

        foreach (var calc in network.Calcs)
        {
            foreach (var e in ExprsOfEnSource(calc.En))
            {
                yield return e;
            }

            foreach (var input in calc.Inputs)
            {
                yield return input;
            }
        }

        foreach (var mbv in network.MoveBlkVariants)
        {
            foreach (var e in ExprsOfEnSource(mbv.En))
            {
                yield return e;
            }

            yield return mbv.Src;
            yield return mbv.Count;
            yield return mbv.SrcIndex;
            yield return mbv.DestIndex;
        }

        foreach (var wait in network.Waits)
        {
            foreach (var e in ExprsOfEnSource(wait.En))
            {
                yield return e;
            }

            yield return wait.Wt;
        }

        foreach (var fill in network.FillBlockIs)
        {
            foreach (var e in ExprsOfEnSource(fill.En))
            {
                yield return e;
            }

            yield return fill.In;
            yield return fill.Count;
        }

        foreach (var mm in network.ModbusMasters)
        {
            foreach (var e in ExprsOfEnSource(mm.En))
            {
                yield return e;
            }

            yield return mm.Req;
            yield return mm.MbAddr;
            yield return mm.Mode;
            yield return mm.DataAddr;
            yield return mm.DataLen;
            yield return mm.DataPtr;
        }

        foreach (var mcl in network.ModbusCommLoads)
        {
            foreach (var e in ExprsOfEnSource(mcl.En))
            {
                yield return e;
            }

            yield return mcl.Req;
            yield return mcl.Port;
            yield return mcl.Baud;
            yield return mcl.Parity;
            yield return mcl.RespTo;
            yield return mcl.MbDb;
        }
    }

    private static IEnumerable<Expr> ExprsOfEnSource(EnSource en) => en switch
    {
        EnSource.Condition condition => new[] { condition.Value },
        _ => Array.Empty<Expr>(),
    };

    private static IEnumerable<string> FromEnSource(EnSource en) => en switch
    {
        EnSource.Condition condition => FromExpr(condition.Value),
        EnSource.PrecedingEno => Array.Empty<string>(),
        _ => Array.Empty<string>(),
    };

    private static IEnumerable<string> FromExpr(Expr expr)
    {
        switch (expr)
        {
            case Expr.TagRef tagRef:
                yield return tagRef.Path;
                break;
            case Expr.And and:
                foreach (var operand in and.Operands)
                {
                    foreach (var path in FromExpr(operand))
                    {
                        yield return path;
                    }
                }

                break;
            case Expr.Or or:
                foreach (var operand in or.Operands)
                {
                    foreach (var path in FromExpr(operand))
                    {
                        yield return path;
                    }
                }

                break;
            case Expr.Not not:
                foreach (var path in FromExpr(not.Operand))
                {
                    yield return path;
                }

                break;
            case Expr.Compare compare:
                foreach (var path in FromExpr(compare.Left))
                {
                    yield return path;
                }

                foreach (var path in FromExpr(compare.Right))
                {
                    yield return path;
                }

                break;
            case Expr.Literal:
                // Not a name — deliberately excluded.
                break;
        }
    }
}
