using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.CrossCheck;

// FI-22: the whole-project reader/writer graph — the substrate the cross-check facts (and, later,
// FI-25's forward tracer) are computed over. Where ProjectIndex indexes names only, this walks every
// block's networks across the whole export (reusing the same *.ir prefix dispatch and the
// TagReferences.AllDirectedUsages per-network walk) and accumulates, per full dotted tag path, who
// writes it and who reads it — plus a global-DB member inventory for the dead-wiring pass. Derived
// fresh, never stored.
public sealed class ProjectUsageGraph
{
    // One occurrence of a tag being read or written, located to its block + network (+ Set/Reset kind).
    public sealed record UsageSite(string Block, int Network, CoilKind? Kind);

    public sealed record PathUsage(List<UsageSite> Writers, List<UsageSite> Readers);

    private readonly Dictionary<string, PathUsage> _usages = new(StringComparer.Ordinal);
    private readonly List<string> _globalDbMemberPaths = new();
    private readonly List<(string Block, int Network, string Path, TagDirection Direction)> _flat = new();
    private readonly List<(string Block, string CalledBlock)> _calls = new();
    private readonly List<string> _warnings = new();

    public IReadOnlyDictionary<string, PathUsage> Usages => _usages;
    public IReadOnlyList<string> GlobalDbMemberPaths => _globalDbMemberPaths;
    public IReadOnlyList<(string Block, int Network, string Path, TagDirection Direction)> Flat => _flat;
    public IReadOnlyList<(string Block, string CalledBlock)> Calls => _calls;
    public IReadOnlyList<string> Warnings => _warnings;

    public static ProjectUsageGraph Build(string projectDir)
    {
        var graph = new ProjectUsageGraph();
        foreach (var path in Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            graph.AddFile(path);
        }

        return graph;
    }

    private void AddFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path);

            if (text.StartsWith("DB ", StringComparison.Ordinal))
            {
                AddDb(DbIrParser.ParseDb(text));
                return;
            }

            if (text.StartsWith("TYPE ", StringComparison.Ordinal) || text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
            {
                return; // UDT members / tag-table entries are not walked for usage here
            }

            var block = IrParser.HasSidecarSection(text)
                ? IrParser.ParseBlock(text).Block
                : IrParser.ParseBlockWithoutSidecar(text);
            AddBlock(block);
        }
        catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException
                                       or NonReducibleNetworkException or IrFormatException)
        {
            _warnings.Add($"{path}: not indexed ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private void AddBlock(IrBlock block)
    {
        foreach (var network in block.Networks)
        {
            foreach (var call in network.Calls)
            {
                _calls.Add((block.Name, call.BlockName));
            }

            foreach (var usage in TagReferences.AllDirectedUsages(network))
            {
                var site = new UsageSite(block.Name, network.Number, usage.SetResetKind);
                var entry = GetOrAdd(usage.Path);
                if (usage.Direction == TagDirection.Write)
                {
                    entry.Writers.Add(site);
                }
                else
                {
                    entry.Readers.Add(site);
                }

                _flat.Add((block.Name, network.Number, usage.Path, usage.Direction));
            }
        }
    }

    // Global-DB members only: their full path (DBName.member…) is how logic always addresses them, so
    // the reader/writer sets are unambiguous. Instance-DB / interface-UDT members alias between an
    // FB-internal bare name and an external iDB-qualified path, which needs instance->FB correlation
    // (a documented follow-up); they are deliberately not inventoried for dead-wiring here.
    private void AddDb(DbSource db)
    {
        if (db.InstanceOfName is not null)
        {
            return; // instance DB — skip (aliasing, see note above)
        }

        foreach (var member in db.Members ?? Array.Empty<DbMember>())
        {
            CollectLeafPaths(db.Name, member);
        }
    }

    private void CollectLeafPaths(string prefix, DbMember member)
    {
        var path = prefix + "." + member.Name;
        if (member.NestedMembers is { Count: > 0 } nested)
        {
            foreach (var child in nested)
            {
                CollectLeafPaths(path, child);
            }
        }
        else
        {
            _globalDbMemberPaths.Add(path);
        }
    }

    private PathUsage GetOrAdd(string path)
    {
        if (!_usages.TryGetValue(path, out var entry))
        {
            entry = new PathUsage(new List<UsageSite>(), new List<UsageSite>());
            _usages[path] = entry;
        }

        return entry;
    }
}
