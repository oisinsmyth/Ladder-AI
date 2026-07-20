using System.Globalization;
using Converter.CrossCheck;
using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Trace;

// Layer B of FI-25: given a binding file, walk each REQ's anchors over FI-22's reader/writer graph
// (`ProjectUsageGraph`, read-only) plus the project's DB start values, and emit per-hop facts +
// candidate verdicts. v1 hops: (1) output-path, (2) interface-chain, (4) number-constraint. The
// "disarmed" (write-condition) and timing (s→ms) hops are deferred — they need the graph to carry
// write conditions / net-new dataflow analysis (see docs/16 FI-25).
public static class TraceRunner
{
    public static TraceReport Run(string bindingPath, string projectDir)
    {
        var bindingFile = BindingFile.Load(bindingPath);
        var graph = ProjectUsageGraph.Build(projectDir);
        var startValues = LoadStartValues(projectDir);

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

            traces.Add(new ReqTrace(binding.Req, hops));
        }

        return new TraceReport(traces, graph.Warnings);
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
