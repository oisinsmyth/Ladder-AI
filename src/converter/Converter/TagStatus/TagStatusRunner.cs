using Converter.Ir;
using Converter.Preflight;
using Converter.SimaticMl;

namespace Converter.TagStatus;

// Mechanical classification of a list of tag names against a project's exported IR (ir/<project>/),
// for the pipeline's anti-laundering rule (CLAUDE.md hard rule 3, docs/15 "Artifacts").
//
// Root resolution reuses ProjectIndex + AccessNode.FromDottedPath so it stays identical to
// `preflight`'s own (FI-24). MEMBER resolution walks the same cross-file DB/UDT index the C-118
// review rule depends on (TagTypeRegistry), through MemberPathResolver — one indexed corpus, so
// "does DB.member exist" can't drift from "what type is DB.member". Before member checking existed,
// `SomeDb.InventedMember` classified EXISTS on the strength of its root alone: the gate protecting
// hard rule 3 blessed invented members. Since FI-45 item 1 the walk also crosses ARRAY OF UDT
// members, which used to make every per-instance binding on a multi-vessel project read as invented.
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
            var (status, detail) = Classify(name, index, registry, rootsOnly);
            entries.Add(new TagStatusEntry(name, RootOf(name), status, detail));
        }

        return new TagStatusReport(entries, index.Warnings);
    }

    // FromDottedPath is total (regex-based; Split('.') always yields >=1 component) and is the
    // canonical root extractor shared with preflight/digest — a naive Split('.') would reintroduce
    // the Clock_0.5Hz literal-dot bug.
    private static string RootOf(string name) =>
        AccessNode.FromDottedPath(0, "GlobalVariable", name).ComponentPath[0];

    private static (TagStatusKind Status, string? Detail) Classify(
        string name, ProjectIndex index, TagTypeRegistry registry, bool rootsOnly)
    {
        // Whole-name first: catches bare tag-table tags that contain a literal dot (Clock_0.5Hz) and
        // bare DB names, neither of which has a member part to check.
        if (index.ResolvesAsTagRoot(name))
        {
            return (TagStatusKind.Exists, null);
        }

        var root = RootOf(name);
        if (!index.ResolvesAsTagRoot(root))
        {
            return (TagStatusKind.Proposed, null);
        }

        // Root resolves. Root-level classification stops here — what the Design stage wants, since it
        // designs *against* gaps rather than coding against them (gen-architecture section 9).
        if (rootsOnly)
        {
            return (TagStatusKind.Exists, null);
        }

        // The member walk (MemberPathResolver) is array-aware and diagnoses WHERE a path failed, so
        // an unresolvable path is no longer flattened to one root-level "are members enumerable?"
        // question — an unknown type three levels down now reports unchecked at that depth instead of
        // an unconditional MEMBER-NOT-FOUND for the whole path.
        var resolution = MemberPathResolver.Resolve(name, registry);
        return resolution.Outcome switch
        {
            MemberPathOutcome.Resolved => (TagStatusKind.Exists, null),
            MemberPathOutcome.MemberAbsent => (TagStatusKind.MemberNotFound, resolution.Detail),
            MemberPathOutcome.IndexOutOfRange => (TagStatusKind.IndexOutOfRange, resolution.Detail),
            _ => (TagStatusKind.MemberUnchecked, resolution.Detail),
        };
    }
}
