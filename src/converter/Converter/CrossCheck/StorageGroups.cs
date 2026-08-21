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
    /// <summary>
    /// 🔴 <b>Writers of this storage that reach it through an <c>iDB_…</c> ALIAS, from OUTSIDE the
    /// owning block</b> (2026-08-21). Empty for a global path, and empty for a block-local path nobody
    /// addresses from outside.
    ///
    /// <para><b>Why this exists rather than pooling the keys.</b> The key spaces stay separate on
    /// purpose — see <c>ProjectUsageGraph.QualifiedPath</c>: an FB with TWO instance DBs has one
    /// internal write landing in both, so pooling would invent a conflict. But NOT pooling left the
    /// report making a claim its own data contradicted: <c>FB_ShredderSequencer.IO.Step</c> printed
    /// <i>"block-local — not a cross-block conflict; also addressable as
    /// iDB_ShredderSequencer.IO.Step"</i> while <c>OB100</c> wrote that very alias. Four rows in
    /// <c>ir/test-project001</c> carried the false label, every one of them with an outside writer.</para>
    ///
    /// <para>So: the aliases are still not merged, and the outside writers are now CARRIED, letting the
    /// consumer state the fact instead of asserting a verdict. Facts, not verdicts — the same rule the
    /// rest of this tool follows.</para>
    /// </summary>
    public List<ProjectUsageGraph.UsageSite> AliasWriters { get; } = new();

    /// <summary>Distinct blocks that WRITE this storage — the set a cross-block conflict is decided on.</summary>
    public IReadOnlyList<string> WriterBlocks =>
        Writers.Select(w => w.Block).Distinct(StringComparer.Ordinal)
            .OrderBy(b => b, StringComparer.Ordinal).ToList();

    /// <summary>Distinct blocks writing this storage through an alias from outside the owner.</summary>
    public IReadOnlyList<string> AliasWriterBlocks =>
        AliasWriters.Select(w => w.Block).Distinct(StringComparer.Ordinal)
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

        LinkAliasWriters(groups);

        return groups.Values.ToList();
    }

    /// <summary>
    /// Second pass, and it must be second: an alias group may not exist yet when the block-local group
    /// is created, so this can only be answered once every path has been placed.
    ///
    /// <para>A block-local group's <c>InstanceAliases</c> are GLOBAL paths, so each is its own key in
    /// this same dictionary — no re-derivation, no second walk that could drift. Writers already inside
    /// the owning block are excluded: those are the internal writes the group lists anyway, and
    /// counting them again would turn every ordinary FB into a self-conflict.</para>
    /// </summary>
    private static void LinkAliasWriters(Dictionary<string, StorageGroup> groups)
    {
        foreach (var group in groups.Values)
        {
            if (group.Owner is null || group.InstanceAliases.Count == 0)
            {
                continue;
            }

            foreach (var alias in group.InstanceAliases)
            {
                if (!groups.TryGetValue(alias, out var aliasGroup))
                {
                    continue;
                }

                group.AliasWriters.AddRange(
                    aliasGroup.Writers.Where(w => !string.Equals(w.Block, group.Owner, StringComparison.Ordinal)));
            }
        }
    }
}
