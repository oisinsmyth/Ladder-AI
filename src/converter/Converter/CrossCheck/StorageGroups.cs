namespace Converter.CrossCheck;

/// <summary>
/// One storage location and everything that touches it.
///
/// <para><see cref="Path"/> is the DISPLAY form — <c>&lt;Owner&gt;.&lt;path&gt;</c> when block-local,
/// the path verbatim when global. <see cref="Owner"/> is null for a global path, so a consumer never
/// has to parse the display string to tell the two apart: *** AN EMITTED STRING IS NOT A SCHEMA. ***</para>
/// </summary>
public sealed record StorageGroup(
    string Path,
    string? Owner,
    IReadOnlyList<string> InstanceAliases,
    List<ProjectUsageGraph.UsageSite> Writers,
    List<ProjectUsageGraph.UsageSite> Readers)
{
    /// <summary>Distinct blocks that WRITE this storage — the set a cross-block conflict is decided on.</summary>
    public IReadOnlyList<string> WriterBlocks =>
        Writers.Select(w => w.Block).Distinct(StringComparer.Ordinal)
            .OrderBy(b => b, StringComparer.Ordinal).ToList();
}

/// <summary>
/// Re-keys the usage graph from each path's VERBATIM spelling onto its STORAGE IDENTITY.
///
/// <para>🔴 Extracted from <see cref="CrossCheckRunner"/> 2026-08-14 so the submission-scoped conflict
/// emitter derives its edges from *** THE SAME CORRECTED GROUPING *** rather than from a second walk
/// that could drift from it. That matters more than tidiness here: the aliasing defect this grouping
/// exists to fix manufactured false cross-block multi-writers, and those were on their way into a
/// submission's conflict graph. One producer, so a fix cannot land in one consumer and miss the other.</para>
///
/// <para>A block-local root cannot be the same storage as an identically-spelled root in another
/// block, so those are keyed under their owner; a global path (DB member, PLC tag, <c>iDB_…</c>,
/// physical address) is already unique and is left exactly as written.</para>
/// </summary>
public static class StorageGroups
{
    public static List<StorageGroup> Build(ProjectUsageGraph graph)
    {
        var groups = new Dictionary<string, StorageGroup>(StringComparer.Ordinal);

        void Add(string path, ProjectUsageGraph.UsageSite site, bool isWriter)
        {
            var owner = graph.OwnerOf(site.Block, path);
            var key = graph.QualifiedPath(site.Block, path);
            if (!groups.TryGetValue(key, out var group))
            {
                group = new StorageGroup(
                    owner is null ? path : owner + "." + path,
                    owner,
                    owner is null ? Array.Empty<string>() : graph.InstanceAliasesOf(owner, path),
                    new List<ProjectUsageGraph.UsageSite>(),
                    new List<ProjectUsageGraph.UsageSite>());
                groups[key] = group;
            }

            (isWriter ? group.Writers : group.Readers).Add(site);
        }

        foreach (var kv in graph.Usages)
        {
            foreach (var site in kv.Value.Writers)
            {
                Add(kv.Key, site, isWriter: true);
            }

            foreach (var site in kv.Value.Readers)
            {
                Add(kv.Key, site, isWriter: false);
            }
        }

        return groups.Values.ToList();
    }
}
