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
            // FI-53: pooled across array elements and members reached through them, not an exact
            // string match. An `Array[0..3] of "UDT_X"` member is one declared leaf but is only ever
            // referenced through an element (`…Silo[0].ZeroOffset`), so the exact lookup this used to
            // do reported live members dead.
            var (writers, readers) = graph.UsagesCovering(path);
            if (writers.Count == 0 || readers.Count == 0)
            {
                // Distinct because pooling across array elements makes repeats ordinary: three
                // members of one element read in one network are three usages and one reader.
                deadMembers.Add(new DeadMemberFact(
                    path,
                    writers.Select(ToWriter).Distinct().ToList(),
                    readers.Select(r => new ReaderRef(r.Block, r.Network)).Distinct().ToList()));
            }
        }

        deadMembers.AddRange(DeadInterfaceMembers(graph));

        var ioBoundary = graph.Flat
            .Where(f => IsPhysicalIo(f.Path))
            .Select(f => new IoBoundaryFact(f.Block, f.Path, f.Direction == TagDirection.Write ? "write" : "read"))
            .Distinct()
            .OrderBy(f => f.Block, StringComparer.Ordinal).ThenBy(f => f.Path, StringComparer.Ordinal)
            .ToList();

        var siblingRefs = BuildSiblingRefs(graph);

        return new CrossCheckReport(multiWriters, deadMembers, ioBoundary, siblingRefs, graph.Warnings);
    }

    // Interface-UDT dead members. Each FB interface member aliases between the FB-internal bare form
    // (`IO.Step`, referenced inside the FB's own block) and an external `iDB.IO.Step` per iDB of that
    // FB. The usage graph keys every alias verbatim, so each looks half-dead on its own; here we pool
    // writers/readers across ALL alias forms per canonical member and flag only the genuinely dead.
    private static IEnumerable<DeadMemberFact> DeadInterfaceMembers(ProjectUsageGraph graph)
    {
        // FB -> its instance DBs (one-to-many).
        var idbsByFb = graph.InstanceToFb
            .GroupBy(kv => kv.Value, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(kv => kv.Key).Distinct(StringComparer.Ordinal)
                    .OrderBy(n => n, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        // Canonical members: one (FB, suffix) per interface member, de-duplicated across the FB's iDBs.
        var canonicalMembers = graph.InstanceMemberPaths
            .Select(m => (Fb: graph.InstanceToFb[m.InstanceDb], m.Suffix))
            .Distinct()
            .OrderBy(m => m.Fb, StringComparer.Ordinal)
            .ThenBy(m => m.Suffix, StringComparer.Ordinal);

        foreach (var (fb, suffix) in canonicalMembers)
        {
            var writers = new List<ProjectUsageGraph.UsageSite>();
            var readers = new List<ProjectUsageGraph.UsageSite>();

            // FB-internal alias: the bare suffix, but only where the referencing block IS the FB
            // itself (a bare `IO.Step` in an unrelated block is a different tag, not this member).
            if (graph.Usages.TryGetValue(suffix, out var internalUsage))
            {
                writers.AddRange(internalUsage.Writers.Where(s => string.Equals(s.Block, fb, StringComparison.Ordinal)));
                readers.AddRange(internalUsage.Readers.Where(s => string.Equals(s.Block, fb, StringComparison.Ordinal)));
            }

            // External aliases: `iDB.suffix` for every iDB of this FB.
            var idbs = idbsByFb.TryGetValue(fb, out var list) ? list : Enumerable.Empty<string>();
            foreach (var idb in idbs)
            {
                if (graph.Usages.TryGetValue(idb + "." + suffix, out var externalUsage))
                {
                    writers.AddRange(externalUsage.Writers);
                    readers.AddRange(externalUsage.Readers);
                }
            }

            if (writers.Count == 0 || readers.Count == 0)
            {
                yield return new DeadMemberFact(
                    fb + "." + suffix,
                    writers.Select(ToWriter).ToList(),
                    readers.Select(r => new ReaderRef(r.Block, r.Network)).ToList(),
                    DeadMemberScope.InterfaceMember);
            }
        }
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
