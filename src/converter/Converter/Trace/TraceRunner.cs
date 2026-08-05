using System.Globalization;
using Converter.CrossCheck;
using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Trace;

// Layer B of FI-25: given a binding file, walk each REQ's anchors over FI-22's reader/writer graph
// (`ProjectUsageGraph`, read-only) plus the project's DB start values, and emit per-hop facts +
// candidate verdicts. Hops: (1) output-path, (2) interface-chain, disarmed (v2, on 1/2 — every writer
// gated NOT AlwaysTrue), (4) number-constraint, (5) timing (v2 — the seconds member reaches the timer's
// PT via the ×1000 s→ms MUL/CONVERT chain; structural/name-correspondence — the MUL↔CONVERT sidecar-wire
// pairing is a documented refinement, not verified here).
public static class TraceRunner
{
    public static TraceReport Run(string bindingPath, string projectDir)
    {
        var bindingFile = BindingFile.Load(bindingPath);
        var graph = ProjectUsageGraph.Build(projectDir);
        var startValues = LoadStartValues(projectDir);
        var blocks = ParseBlocks(projectDir); // for the timing hop's statement walk (graph has no structure)

        var traces = new List<ReqTrace>(bindingFile.Bindings.Count);
        foreach (var binding in bindingFile.Bindings)
        {
            var hops = new List<HopResult>();

            if (!string.IsNullOrWhiteSpace(binding.OutTag))
            {
                hops.Add(WriteHop(HopKind.OutputPath, binding.OutTag!, graph,
                    Verdict.Unimplemented, "no writer — no output path"));
            }

            if (!string.IsNullOrWhiteSpace(binding.IfaceMember))
            {
                hops.Add(WriteHop(HopKind.InterfaceChain, binding.IfaceMember!, graph,
                    Verdict.BrokenChain, "written by nothing — broken chain (the in-cycle-lamp class)"));
            }

            if (binding.Number is { } number)
            {
                hops.Add(NumberHop(number, startValues));
            }

            if (binding.Timing is { } timing)
            {
                hops.Add(TimingHop(timing, blocks));
            }

            if (binding.Guard is { } guard)
            {
                hops.Add(GuardContainmentHop(guard, graph));
            }

            traces.Add(new ReqTrace(binding.Req, hops));
        }

        return new TraceReport(traces, graph.Warnings);
    }

    // The timing chain: seconds member → MUL(×1000) => <intermediate> → CONVERT => <ms member> → timer.PT.
    // Anchored on the bound timer (the "right timer" endpoint) and disambiguated by the bound seconds member
    // (which MUL feeds the chain), so it doesn't fall into the shared-scratch trap by tag alone. Honest
    // limitation: the MUL↔CONVERT link is an EN:=ENO wire recorded only in the sidecar — this checks the
    // pieces are present with the expected operands, NOT that the specific wire pairs them (a documented
    // sidecar-level refinement). Facts + a candidate verdict.
    private static HopResult TimingHop(TimingConstraint timing, IReadOnlyList<IrBlock> blocks)
    {
        // Find the timer and its owning block.
        foreach (var block in blocks)
        {
            foreach (var network in block.Networks)
            {
                foreach (var timer in network.Timers)
                {
                    if (timer.InstancePath != timing.Timer)
                    {
                        continue;
                    }

                    return TraceTimerChain(timing, block, timer, network.Number);
                }
            }
        }

        return new HopResult(HopKind.Timing, Verdict.Unimplemented,
            $"no timer named '{timing.Timer}' found — cannot trace the timing chain", Array.Empty<string>());
    }

    private static HopResult TraceTimerChain(TimingConstraint timing, IrBlock block, TimerBinding timer, int timerNetwork)
    {
        var loc = $"{block.Name} N{timerNetwork}";
        if (timer.Pt is not Expr.TagRef ptRef)
        {
            return new HopResult(HopKind.Timing, Verdict.Contradicted,
                $"'{timing.Timer}' PT is a literal/expression, not a converted ms member — cannot trace an s→ms chain",
                new[] { loc });
        }

        var msTag = ptRef.Path;

        // Primary linkage: the timer's UNIQUE PT ms-member name must correspond to the bound seconds member
        // (strip a trailing "MS"). This is the disambiguator the shared MUL/CONVERT scratch tag cannot
        // provide — the ms member is per-timer, so a name mismatch means this timer isn't fed by that member.
        if (!string.Equals(StripMsSuffix(Leaf(msTag)), Leaf(timing.SecondsMember), StringComparison.OrdinalIgnoreCase))
        {
            return new HopResult(HopKind.Timing, Verdict.Contradicted,
                $"'{timing.Timer}' PT is {msTag}, which does not correspond to seconds member {timing.SecondsMember} — " +
                "this timer's preset is not the ms form of that member",
                new[] { loc });
        }

        // Corroborate the ×1000 s→ms idiom for this member: a CONVERT writes msTag from an intermediate, and a
        // MUL writes that intermediate with <seconds member> × 1000. The intermediate is often a shared scratch
        // tag, so the specific MUL↔CONVERT wire pairing is NOT verified (a documented sidecar-level refinement).
        var convert = block.Networks.SelectMany(n => n.Converts).FirstOrDefault(c => c.DestTag == msTag);
        var hasMul = convert?.In is Expr.TagRef convertIn && block.Networks.SelectMany(n => n.Muls)
            .Any(m => m.DestTag == convertIn.Path
                && m.Inputs.Any(i => i is Expr.TagRef t && t.Path == timing.SecondsMember)
                && m.Inputs.Any(IsThousandLiteral));

        if (convert is null || !hasMul)
        {
            return new HopResult(HopKind.Timing, Verdict.Partial,
                $"'{timing.Timer}' PT ({msTag}) corresponds to {timing.SecondsMember} by name, but the ×1000 s→ms " +
                "MUL/CONVERT chain isn't fully present — the conversion may be missing or use a different idiom",
                new[] { loc });
        }

        return new HopResult(HopKind.Timing, Verdict.Ok,
            $"'{timing.Timer}' PT ({msTag}) is the ×1000 ms form of {timing.SecondsMember} — chain present " +
            "(MUL↔CONVERT sidecar-wire pairing not verified)",
            new[] { loc });
    }

    private static string Leaf(string path) => path.Contains('.') ? path[(path.LastIndexOf('.') + 1)..] : path;

    private static string StripMsSuffix(string leaf) =>
        leaf.EndsWith("MS", StringComparison.OrdinalIgnoreCase) ? leaf[..^2] : leaf;

    private static bool IsThousandLiteral(Expr e) =>
        e is Expr.Literal lit &&
        double.TryParse(lit.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) &&
        Math.Abs(v - 1000.0) < 1e-9;

    private static List<IrBlock> ParseBlocks(string projectDir)
    {
        var blocks = new List<IrBlock>();
        foreach (var path in Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var text = File.ReadAllText(path);
                if (!text.StartsWith("BLOCK ", StringComparison.Ordinal))
                {
                    continue;
                }

                blocks.Add(IrParser.HasSidecarSection(text) ? IrParser.ParseBlock(text).Block : IrParser.ParseBlockWithoutSidecar(text));
            }
            catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException
                                           or NonReducibleNetworkException or IrFormatException)
            {
                // Unparseable — skip; a timing binding naming a timer in it just won't resolve.
            }
        }

        return blocks;
    }

    // Hops 1 & 2 share the "is this path written anywhere?" shape — only the failure verdict differs.
    private static HopResult WriteHop(HopKind hop, string path, ProjectUsageGraph graph, Verdict deadVerdict, string deadDetail)
    {
        if (!graph.Usages.TryGetValue(path, out var usage) || usage.Writers.Count == 0)
        {
            return new HopResult(hop, deadVerdict, $"{path}: {deadDetail}", Array.Empty<string>());
        }

        var writers = usage.Writers
            .Select(w => $"{w.Block} N{w.Network}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        // FI-25 v2 disarmed hop: a write can never fire if its guard is a placeholder-false (NOT
        // AlwaysTrue). If EVERY writer of the path is disarmed, the path is built-but-switched-off →
        // Disarmed (which review-functional treats as NOT implemented). A mix (some armed) stays Ok, with
        // the disarmed count noted so the reviewer can still see it.
        var disarmedCount = usage.Writers.Count(w => DisarmAnalysis.IsProvablyFalse(w.Guard));
        if (disarmedCount == usage.Writers.Count)
        {
            return new HopResult(hop, Verdict.Disarmed,
                $"{path}: written by {writers.Count} site(s), all gated NOT AlwaysTrue — built but switched off", writers);
        }

        var okDetail = disarmedCount > 0
            ? $"{path}: written by {writers.Count} site(s) ({disarmedCount} disarmed)"
            : $"{path}: written by {writers.Count} site(s)";
        return new HopResult(hop, Verdict.Ok, okDetail, writers);
    }

    // Hop 6 (FI-36-min): every signal the spec lists as a condition on `coil` must appear in the guard of
    // each write to it. Pure set-difference over signal identity — it cannot be defeated by how anyone
    // *reads* an ambiguous requirement, which is the whole point: a dropped cascade-hold term shipped as a
    // REGRESSION because the coder and the reviewer resolved the same ambiguous source the same way
    // (`docs/evidence/PlantAutoControl-bench-autopsy.md`). Reported PER WRITING SITE, never unioned: a term
    // present in one network and absent in another is the multi-instance shape a union would hide.
    private static HopResult GuardContainmentHop(GuardConstraint constraint, ProjectUsageGraph graph)
    {
        var coil = constraint.Coil;

        if (!graph.Usages.TryGetValue(coil, out var usage) || usage.Writers.Count == 0)
        {
            // No writer at all is the output-path fact, not a containment fact — do not report every
            // required term as "missing" off the back of a path that simply isn't written.
            return new HopResult(HopKind.GuardContainment, Verdict.Unimplemented,
                $"{coil}: no writer — no output path (guard containment not assessable)", Array.Empty<string>());
        }

        var evidence = new List<string>();
        var anyMissing = false;

        foreach (var site in usage.Writers.OrderBy(w => w.Block, StringComparer.Ordinal).ThenBy(w => w.Network))
        {
            var present = new HashSet<string>(GuardTagPaths(site.Guard), StringComparer.Ordinal);
            var missing = constraint.MustContain
                .Where(term => !present.Contains(term))
                .ToList();

            var where = $"{site.Block} N{site.Network}";
            var disarmed = DisarmAnalysis.IsProvablyFalse(site.Guard) ? " [disarmed]" : string.Empty;

            if (missing.Count == 0)
            {
                evidence.Add($"{where}{disarmed}: all {constraint.MustContain.Count} required term(s) present");
                continue;
            }

            anyMissing = true;
            evidence.Add($"{where}{disarmed}: MISSING {string.Join(", ", missing)}");
        }

        return anyMissing
            ? new HopResult(HopKind.GuardContainment, Verdict.MissingTerm,
                $"{coil}: a spec-listed condition is absent from the guard of at least one writer", evidence)
            : new HopResult(HopKind.GuardContainment, Verdict.Ok,
                $"{coil}: every spec-listed condition present in all {usage.Writers.Count} writer guard(s)", evidence);
    }

    // Every tag path referenced anywhere in a guard expression. A null guard (a read, or an ENO-chained
    // write with no local condition) contributes nothing — an unconditional write contains no terms.
    private static IEnumerable<string> GuardTagPaths(Expr? guard)
    {
        if (guard is null)
        {
            yield break;
        }

        switch (guard)
        {
            case Expr.TagRef tag:
                yield return tag.Path;
                break;
            case Expr.Not not:
                foreach (var path in GuardTagPaths(not.Operand))
                {
                    yield return path;
                }

                break;
            case Expr.And and:
                foreach (var path in and.Operands.SelectMany(GuardTagPaths))
                {
                    yield return path;
                }

                break;
            case Expr.Or or:
                foreach (var path in or.Operands.SelectMany(GuardTagPaths))
                {
                    yield return path;
                }

                break;
            case Expr.Compare compare:
                foreach (var path in GuardTagPaths(compare.Left).Concat(GuardTagPaths(compare.Right)))
                {
                    yield return path;
                }

                break;
        }
    }

    private static HopResult NumberHop(NumberConstraint number, IReadOnlyDictionary<string, string?> startValues)
    {
        if (!startValues.TryGetValue(number.Member, out var startValue))
        {
            return new HopResult(HopKind.NumberConstraint, Verdict.Partial,
                $"{number.Member}: member not found in any DB (cannot verify {number.Expected})", Array.Empty<string>());
        }

        if (startValue is null)
        {
            return new HopResult(HopKind.NumberConstraint, Verdict.Partial,
                $"{number.Member}: no start value set — spec says {number.Expected} (cite the REQ's open question)",
                Array.Empty<string>());
        }

        var matches = ValuesMatch(startValue, number.Expected);
        return matches
            ? new HopResult(HopKind.NumberConstraint, Verdict.Ok,
                $"{number.Member}: start value {startValue} matches spec {number.Expected}", new[] { startValue })
            : new HopResult(HopKind.NumberConstraint, Verdict.Contradicted,
                $"{number.Member}: start value {startValue} != spec {number.Expected}", new[] { startValue });
    }

    // Numeric when both parse as numbers (so "10.0" matches "10"); else normalized-string. Start values
    // are uninterpreted Siemens literals (e.g. "10.0", "T#10S", "TRUE") — a non-numeric spec falls back
    // to trimmed case-insensitive equality.
    private static bool ValuesMatch(string startValue, string expected)
    {
        if (double.TryParse(startValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var a) &&
            double.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out var b))
        {
            return Math.Abs(a - b) < 1e-9;
        }

        return string.Equals(startValue.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    // Every DB member's full path -> its start value (null when unset). Same DB parse the usage graph
    // uses, but keeping the start value the graph discards. Covers global and instance DBs (a binding
    // may name either).
    private static Dictionary<string, string?> LoadStartValues(string projectDir)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly))
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
                if (!text.StartsWith("DB ", StringComparison.Ordinal))
                {
                    continue;
                }

                var db = DbIrParser.ParseDb(text);
                foreach (var member in db.Members ?? Array.Empty<DbMember>())
                {
                    CollectStartValues(db.Name, member, result);
                }
            }
            catch (Exception ex) when (ex is SimaticMlFormatException or IrFormatException)
            {
                // Unparseable DB — skip; its members just won't resolve (Partial for a binding naming them).
            }
        }

        return result;
    }

    private static void CollectStartValues(string prefix, DbMember member, Dictionary<string, string?> into)
    {
        var path = prefix + "." + member.Name;
        if (member.NestedMembers is { Count: > 0 } nested)
        {
            foreach (var child in nested)
            {
                CollectStartValues(path, child, into);
            }
        }
        else
        {
            into[path] = member.StartValue;
        }
    }
}
