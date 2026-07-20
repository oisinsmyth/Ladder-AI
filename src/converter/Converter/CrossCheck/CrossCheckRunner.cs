using System.Text.RegularExpressions;
using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.CrossCheck;

// Derives the four cross-block fact tables from the whole-project usage graph. Pure derivation over
// graph facts — no judgment, no severity, no verdict.
public static class CrossCheckRunner
{
    // Physical-IO tag roots (C-304): a symbolic IO name like DI3_SYS_… / DQ5_DIS_… or a raw %I/%Q operand.
    private static readonly Regex PhysicalIoRoot = new(@"^(DI|DQ|DO|AI|AQ|AO)\d+_", RegexOptions.Compiled);

    public static CrossCheckReport Run(string projectDir)
    {
        var graph = ProjectUsageGraph.Build(projectDir);

        var multiWriters = graph.Usages
            .Where(kv => kv.Value.Writers.Count >= 2)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new MultiWriterFact(kv.Key, kv.Value.Writers.Select(ToWriter).ToList()))
            .ToList();

        var deadMembers = new List<DeadMemberFact>();
        foreach (var path in graph.GlobalDbMemberPaths.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal))
        {
            graph.Usages.TryGetValue(path, out var usage);
            var writers = usage?.Writers ?? new List<ProjectUsageGraph.UsageSite>();
            var readers = usage?.Readers ?? new List<ProjectUsageGraph.UsageSite>();
            if (writers.Count == 0 || readers.Count == 0)
            {
                deadMembers.Add(new DeadMemberFact(
                    path,
                    writers.Select(ToWriter).ToList(),
                    readers.Select(r => new ReaderRef(r.Block, r.Network)).ToList()));
            }
        }

        var ioBoundary = graph.Flat
            .Where(f => IsPhysicalIo(f.Path))
            .Select(f => new IoBoundaryFact(f.Block, f.Path, f.Direction == TagDirection.Write ? "write" : "read"))
            .Distinct()
            .OrderBy(f => f.Block, StringComparer.Ordinal).ThenBy(f => f.Path, StringComparer.Ordinal)
            .ToList();

        var siblingRefs = BuildSiblingRefs(graph);

        return new CrossCheckReport(multiWriters, deadMembers, ioBoundary, siblingRefs, graph.Warnings);
    }

    private static List<SiblingRefFact> BuildSiblingRefs(ProjectUsageGraph graph)
    {
        var callsByBlock = graph.Calls
            .GroupBy(c => c.Block, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(c => c.CalledBlock).Distinct(StringComparer.Ordinal)
                .OrderBy(c => c, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);

        var idbByBlock = graph.Flat
            .Select(f => (f.Block, Root: RootOf(f.Path)))
            .Where(x => x.Root.StartsWith("iDB_", StringComparison.Ordinal))
            .GroupBy(x => x.Block, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Root).Distinct(StringComparer.Ordinal)
                .OrderBy(r => r, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);

        var blocks = callsByBlock.Keys.Union(idbByBlock.Keys, StringComparer.Ordinal)
            .OrderBy(b => b, StringComparer.Ordinal);

        var result = new List<SiblingRefFact>();
        foreach (var block in blocks)
        {
            var calls = callsByBlock.TryGetValue(block, out var c) ? c : new List<string>();
            var idbs = idbByBlock.TryGetValue(block, out var i) ? i : new List<string>();
            result.Add(new SiblingRefFact(block, calls, idbs));
        }

        return result;
    }

    private static WriterRef ToWriter(ProjectUsageGraph.UsageSite site) =>
        new(site.Block, site.Network, KindLabel(site.Kind));

    private static string KindLabel(CoilKind? kind) => kind switch
    {
        CoilKind.Set => "set",
        CoilKind.Reset => "reset",
        CoilKind.Assign => "assign",
        _ => "write",
    };

    private static bool IsPhysicalIo(string path) =>
        path.StartsWith("%I", StringComparison.Ordinal) ||
        path.StartsWith("%Q", StringComparison.Ordinal) ||
        PhysicalIoRoot.IsMatch(RootOf(path));

    private static string RootOf(string path) =>
        AccessNode.FromDottedPath(0, "GlobalVariable", path).ComponentPath[0];
}
