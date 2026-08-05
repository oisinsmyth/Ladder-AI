using Converter.Ir;
using Converter.Preflight;
using Converter.SimaticMl;

namespace Converter.TagStatus;

// Mechanical classification of a list of tag names against a project's exported IR (ir/<project>/),
// for the pipeline's anti-laundering rule (CLAUDE.md hard rule 3, docs/15 "Artifacts").
//
// Root resolution reuses ProjectIndex + AccessNode.FromDottedPath so it stays identical to
// `preflight`'s own (FI-24). MEMBER resolution reuses TagTypeRegistry — the same cross-file DB/UDT
// index the C-118 review rule already depends on — so "does DB.member exist" has one implementation,
// not a second to drift. Before member checking existed, `SomeDb.InventedMember` classified EXISTS on
// the strength of its root alone: the gate protecting hard rule 3 blessed invented members.
public static class TagStatusRunner
{
    public static TagStatusReport Run(IReadOnlyList<string> names, string projectDir, bool rootsOnly = false)
    {
        var index = ProjectIndex.Build(projectDir, Array.Empty<string>());

        // Same file set ProjectIndex indexes, so the two views can never disagree about scope.
        var registry = rootsOnly
            ? TagTypeRegistry.Empty
            : TagTypeRegistry.FromFiles(
                Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly));

        var entries = new List<TagStatusEntry>(names.Count);
        foreach (var name in names)
        {
            entries.Add(new TagStatusEntry(name, RootOf(name), Classify(name, index, registry, rootsOnly)));
        }

        return new TagStatusReport(entries, index.Warnings);
    }

    // FromDottedPath is total (regex-based; Split('.') always yields >=1 component) and is the
    // canonical root extractor shared with preflight/digest — a naive Split('.') would reintroduce
    // the Clock_0.5Hz literal-dot bug.
    private static string RootOf(string name) =>
        AccessNode.FromDottedPath(0, "GlobalVariable", name).ComponentPath[0];

    private static TagStatusKind Classify(string name, ProjectIndex index, TagTypeRegistry registry, bool rootsOnly)
    {
        // Whole-name first: catches bare tag-table tags that contain a literal dot (Clock_0.5Hz) and
        // bare DB names, neither of which has a member part to check.
        if (index.ResolvesAsTagRoot(name))
        {
            return TagStatusKind.Exists;
        }

        var root = RootOf(name);
        if (!index.ResolvesAsTagRoot(root))
        {
            return TagStatusKind.Proposed;
        }

        // Root resolves. Root-level classification stops here — what the Design stage wants, since it
        // designs *against* gaps rather than coding against them (gen-architecture section 9).
        if (rootsOnly)
        {
            return TagStatusKind.Exists;
        }

        if (registry.Resolve(name) is not null)
        {
            return TagStatusKind.Exists;
        }

        return MembersAreEnumerable(root, registry)
            ? TagStatusKind.MemberNotFound
            : TagStatusKind.MemberUnchecked;
    }

    // Members are enumerable when the root is a DB whose member tree the export actually carries, or
    // a tag whose own datatype is a known UDT. Anything else — a tag typed by an unexported UDT, or
    // an instance-DB stub written by `create-instance-db` and not yet re-exported (no member tree at
    // all) — cannot be checked, and is reported as unchecked rather than failed. Treating an
    // unknowable namespace as "member absent" would manufacture false gaps, the opposite failure to
    // the one member checking exists to fix.
    private static bool MembersAreEnumerable(string root, TagTypeRegistry registry)
    {
        if (registry.TryGetDb(root, out var db))
        {
            return db.Members.Count > 0
                || (db.InputMembers?.Count ?? 0) > 0
                || (db.OutputMembers?.Count ?? 0) > 0
                || db.InOutMembers.Count > 0;
        }

        return registry.Resolve(root) is string rootType && registry.TryGetUdt(rootType, out _);
    }
}
