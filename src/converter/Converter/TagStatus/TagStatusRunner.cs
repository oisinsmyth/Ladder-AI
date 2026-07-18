using Converter.Preflight;
using Converter.SimaticMl;

namespace Converter.TagStatus;

// Mechanical `exists`/`proposed` classification of a list of tag names against a project's exported
// IR (ir/<project>/), for the pipeline's anti-laundering rule (CLAUDE.md hard rule 3, docs/15
// "Artifacts"). Reuses ProjectIndex + AccessNode.FromDottedPath root extraction so classification is
// identical to `preflight`'s own — one source of truth, no second implementation to drift (FI-24).
public static class TagStatusRunner
{
    public static TagStatusReport Run(IReadOnlyList<string> names, string projectDir)
    {
        // Empty batch: classify against the existing export only (a proposed-tag list is checked
        // against what's already there, not against itself).
        var index = ProjectIndex.Build(projectDir, Array.Empty<string>());

        var entries = new List<TagStatusEntry>(names.Count);
        foreach (var name in names)
        {
            // FromDottedPath is total (regex-based; Split('.') always yields >=1 component, so
            // ComponentPath[0] is always present) and is the canonical root extractor shared with
            // preflight/digest — a naive Split('.') would reintroduce the Clock_0.5Hz literal-dot bug.
            var root = AccessNode.FromDottedPath(0, "GlobalVariable", name).ComponentPath[0];

            // Whole-name first catches bare tag-table tags that contain dots (Clock_0.5Hz) and DB
            // names; the root fallback catches DB.member / Block.member references.
            var exists = index.ResolvesAsTagRoot(name) || index.ResolvesAsTagRoot(root);
            entries.Add(new TagStatusEntry(name, root, exists));
        }

        return new TagStatusReport(entries, index.Warnings);
    }
}
