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
    // Guard (FI-25 v2) is the write's guarding condition Expr (null for reads / ENO-chained writes), so a
    // consumer can tell an armed write from a disarmed one (e.g. `NOT AlwaysTrue`).
    public sealed record UsageSite(string Block, int Network, CoilKind? Kind, Expr? Guard = null);

    public sealed record PathUsage(List<UsageSite> Writers, List<UsageSite> Readers);

    private readonly Dictionary<string, PathUsage> _usages = new(StringComparer.Ordinal);
    private readonly List<string> _globalDbMemberPaths = new();
    private readonly List<(string Block, int Network, string Path, TagDirection Direction)> _flat = new();
    private readonly List<(string Block, string CalledBlock)> _calls = new();
    private readonly List<string> _warnings = new();

    // FI-22 follow-up: interface-UDT aliasing. An instance DB mirrors its FB's interface 1:1 and the
    // same member is addressed two ways — FB-internal bare (`IO.Step`, inside the FB's own logic) and
    // orchestrator-external (`iDB_X.IO.Step`, from callers). The verbatim-keyed `_usages`/
    // `GlobalDbMemberPaths` above are LEFT UNTOUCHED (FI-25's forward tracer reads them as-is); these
    // two parallel indexes carry the correlation the dead-wiring pass needs to collapse both forms.
    private readonly Dictionary<string, string> _instanceToFb = new(StringComparer.Ordinal);
    private readonly List<(string InstanceDb, string Suffix)> _instanceMemberPaths = new();

    public IReadOnlyDictionary<string, PathUsage> Usages => _usages;
    public IReadOnlyList<string> GlobalDbMemberPaths => _globalDbMemberPaths;
    public IReadOnlyList<(string Block, int Network, string Path, TagDirection Direction)> Flat => _flat;
    public IReadOnlyList<(string Block, string CalledBlock)> Calls => _calls;
    public IReadOnlyList<string> Warnings => _warnings;

    // iDB name -> the FB it instantiates (one-to-one; the FB->iDB direction is one-to-many).
    public IReadOnlyDictionary<string, string> InstanceToFb => _instanceToFb;

    // Every leaf interface member of every instance DB, as (iDB name, member suffix relative to the
    // iDB root) — e.g. ("iDB_ShredderSequencer", "IO.Step"), ("iDB_ShredderSequencer", "StopCmd").
    public IReadOnlyList<(string InstanceDb, string Suffix)> InstanceMemberPaths => _instanceMemberPaths;

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
                var site = new UsageSite(block.Name, network.Number, usage.SetResetKind, usage.Guard);
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

    // Global-DB members: their full path (DBName.member…) is how logic always addresses them, so the
    // reader/writer sets are unambiguous — inventoried into _globalDbMemberPaths as before.
    //
    // Instance-DB / interface-UDT members alias between an FB-internal bare name and an external
    // iDB-qualified path (FI-22 follow-up). We no longer skip them: capture the iDB->FB link and
    // inventory the iDB's interface-member leaves (as suffixes relative to the iDB root) into the
    // parallel indexes above, so CrossCheckRunner can correlate the two alias forms. The global path
    // (_usages / _globalDbMemberPaths) is untouched.
    private void AddDb(DbSource db)
    {
        if (db.InstanceOfName is not null)
        {
            _instanceToFb[db.Name] = db.InstanceOfName;
            foreach (var member in AllInterfaceMembers(db))
            {
                CollectInstanceLeafPaths(db.Name, suffixPrefix: string.Empty, member);
            }

            return;
        }

        foreach (var member in db.Members ?? Array.Empty<DbMember>())
        {
            CollectLeafPaths(db.Name, member);
        }
    }

    private static IEnumerable<DbMember> AllInterfaceMembers(DbSource db)
    {
        foreach (var m in db.Members ?? Array.Empty<DbMember>())
        {
            yield return m;
        }

        foreach (var m in db.InputMembers ?? Array.Empty<DbMember>())
        {
            yield return m;
        }

        foreach (var m in db.OutputMembers ?? Array.Empty<DbMember>())
        {
            yield return m;
        }

        foreach (var m in db.InOutMembers)
        {
            yield return m;
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

    // Same leaf-recursion as CollectLeafPaths, but records the member SUFFIX (path relative to the iDB
    // root) rather than a root-qualified path — that suffix is exactly the FB-internal alias form.
    private void CollectInstanceLeafPaths(string instanceDb, string suffixPrefix, DbMember member)
    {
        var suffix = suffixPrefix.Length == 0 ? member.Name : suffixPrefix + "." + member.Name;
        if (member.NestedMembers is { Count: > 0 } nested)
        {
            foreach (var child in nested)
            {
                CollectInstanceLeafPaths(instanceDb, suffix, child);
            }
        }
        else
        {
            _instanceMemberPaths.Add((instanceDb, suffix));
        }
    }

    // Collapses either alias form of an interface-UDT member to one canonical key `"<FB>|<suffix>"`.
    // A path rooted at a known instance-DB name (`iDB_X.IO.Step`) canonicalizes to `"<FB>|IO.Step"`;
    // the FB-internal bare form (`IO.Step`, referenced inside FB `<FB>`'s own logic) is the same key,
    // formed by the caller as `"<FB>|IO.Step"` since a bare path carries no root to key off. Returns
    // null when the root is not a known iDB (a global-DB / local / physical path).
    public string? CanonicalizeInstancePath(string path)
    {
        var dot = path.IndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        var root = path.Substring(0, dot);
        return _instanceToFb.TryGetValue(root, out var fb)
            ? fb + "|" + path.Substring(dot + 1)
            : null;
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
