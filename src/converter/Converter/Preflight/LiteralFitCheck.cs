using Converter.Ir;

namespace Converter.Preflight;

/// <summary>
/// *** A LITERAL THAT CANNOT FIT ITS DESTINATION *** — pre-flight check `literal-fit`, added
/// 2026-08-13 alongside the fix that types a literal from the port it feeds rather than from its own
/// magnitude.
///
/// WHY IT LIVES IN PRE-FLIGHT AND NOT IN `converter review`: the review tool checks the C-nnn
/// conventions of `docs/06-lad-conventions.md`, and **doc 06 has no rule about a literal fitting its
/// destination's declared type** — so no review rule failed to catch the build-stamp defect; there
/// was never one to fail. Inventing a C-nnn here would be writing a site convention from the tooling
/// side, which is not this component's call. Pre-flight's stated job, on the other hand, is exactly
/// this: *catch the known, recurring import/compile error classes in milliseconds instead of a
/// Portal cycle*. A literal too wide for the member it is written into is that, precisely — measured:
/// `MOVE(EN := TRUE, IN := 70000) => &lt;an Int member&gt;` was reported CLEAN by pre-flight, converted
/// without complaint, and is rejected by TIA.
///
/// SCOPE IS DELIBERATELY THE UNAMBIGUOUS HALF, so a finding here always means something:
///   - a PLAIN DECIMAL literal must lie inside the destination type's declared min..max;
///   - a BASE-PREFIXED literal must fit the destination type's BIT WIDTH — `16#A93F2C71` needs 32
///     bits and an `Int` has 16, which is wrong under any reading. Deliberately NOT a signed-range
///     test: whether TIA reinterprets `16#FFFF` as a two's-complement `Int` is a question this
///     project has no grounded export to answer, and a check that guesses at it would produce the
///     kind of finding people learn to ignore.
/// Non-integer destinations (Real/Time/Bool/String/UDT) are skipped outright — not "checked and
/// clean", simply not this check's subject.
/// </summary>
public static class LiteralFitCheck
{
    // Declared range and bit width per elementary integer type. A type absent from this table is not
    // checked at all — silence here means "no opinion", never "fits".
    private static readonly Dictionary<string, (long Min, ulong Max, int Bits)> IntegerTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SInt"] = (-128, 127, 8),
            ["USInt"] = (0, 255, 8),
            ["Byte"] = (0, 255, 8),
            ["Int"] = (-32768, 32767, 16),
            ["UInt"] = (0, 65535, 16),
            ["Word"] = (0, 65535, 16),
            ["DInt"] = (-2147483648, 2147483647, 32),
            ["UDInt"] = (0, 4294967295, 32),
            ["DWord"] = (0, 4294967295, 32),
            ["LInt"] = (long.MinValue, long.MaxValue, 64),
            ["ULInt"] = (0, ulong.MaxValue, 64),
            ["LWord"] = (0, ulong.MaxValue, 64),
        };

    public static IEnumerable<PreflightFinding> Check(
        IrBlock block, TagTypeRegistry tagTypes, CalleeInterfaceRegistry callees)
    {
        foreach (var network in block.Networks)
        {
            // A MOVE's destination member type — the site the version-register stamp is written at.
            foreach (var move in network.Moves)
            {
                var finding = Fit(network.Number, move.In, tagTypes.Resolve(move.DestTag), $"MOVE into '{move.DestTag}'");
                if (finding is not null)
                {
                    yield return finding;
                }
            }

            // A CALL's declared parameter type — the site the defect was actually measured at.
            foreach (var call in network.Calls)
            {
                if (!callees.TryGetBlock(call.BlockName, out var parms))
                {
                    continue; // an unresolved callee is already a `call` finding; not this check's business
                }

                foreach (var argument in call.Arguments.OfType<CallArgument.InputArg>())
                {
                    if (!parms.TryGetValue(argument.ParamName, out var param))
                    {
                        continue; // an unknown parameter is the synthesizer's hard error, not a fit question
                    }

                    var finding = Fit(
                        network.Number, argument.Value, param.Type,
                        $"CALL {call.BlockName}({argument.ParamName} := ...)");
                    if (finding is not null)
                    {
                        yield return finding;
                    }
                }
            }
        }
    }

    private static PreflightFinding? Fit(int networkNumber, Expr operand, string? destinationType, string where)
    {
        if (operand is not Expr.Literal literal
            || destinationType is null
            || !IntegerTypes.TryGetValue(destinationType.Trim('"'), out var range))
        {
            return null;
        }

        if (TryParseBasePrefixed(literal.Value, out var magnitude))
        {
            var bitsNeeded = BitsNeeded(magnitude);
            return bitsNeeded <= range.Bits
                ? null
                : new PreflightFinding(
                    "literal-fit",
                    $"network {networkNumber}: {where} — the literal '{literal.Value}' needs {bitsNeeded} bits and "
                    + $"'{destinationType}' holds {range.Bits}. TIA rejects this at import; a wider destination type "
                    + "or a narrower value is the fix.");
        }

        if (long.TryParse(literal.Value, out var signed))
        {
            var fits = signed >= range.Min && (signed < 0 || (ulong)signed <= range.Max);
            return fits
                ? null
                : new PreflightFinding(
                    "literal-fit",
                    $"network {networkNumber}: {where} — the literal '{literal.Value}' is outside '{destinationType}'"
                    + $"'s range ({range.Min} to {range.Max}). TIA rejects this at import.");
        }

        if (ulong.TryParse(literal.Value, out var unsigned))
        {
            return unsigned <= range.Max
                ? null
                : new PreflightFinding(
                    "literal-fit",
                    $"network {networkNumber}: {where} — the literal '{literal.Value}' is outside '{destinationType}'"
                    + $"'s range ({range.Min} to {range.Max}). TIA rejects this at import.");
        }

        // Not a decidable numeric literal (a duration, TRUE/FALSE, a Real against an integer member).
        // No opinion — and no finding, rather than a guess dressed as one.
        return null;
    }

    // Siemens' `<base>#<digits>` notation, bases 2/8/16 (the ones IrParser's own shape-based literal
    // detection accepts). An unparseable body is not this check's problem — the converter refuses it.
    private static bool TryParseBasePrefixed(string value, out ulong magnitude)
    {
        magnitude = 0;
        var hash = value.IndexOf('#');
        if (hash <= 0 || hash == value.Length - 1 || !int.TryParse(value[..hash], out var numberBase))
        {
            return false;
        }

        if (numberBase is not (2 or 8 or 16))
        {
            return false;
        }

        try
        {
            magnitude = Convert.ToUInt64(value[(hash + 1)..].Replace("_", string.Empty), numberBase);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            return false;
        }
    }

    private static int BitsNeeded(ulong magnitude)
    {
        var bits = 1;
        while (magnitude > 1)
        {
            magnitude >>= 1;
            bits++;
        }

        return bits;
    }
}
