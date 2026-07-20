using Converter.Digest;
using Converter.Preflight;
using Converter.SimaticMl;

namespace Converter.TargetScan;

// Cross-joins the parsed register against a fresh ProjectIndex (exists/proposed classification — the
// same primitive tagstatus/preflight use, so classification never drifts) and the as-built corpus
// digest (the inline-implementation hint). Produces one ReqTarget per REQ with its mechanical
// disqualifiers pre-computed. No semantic judgment: it surfaces buckets, the human decides.
public static class TargetScanRunner
{
    public static TargetScanReport Run(string requirementsPath, string projectDir)
    {
        var doc = RequirementsParser.Parse(File.ReadAllText(requirementsPath));
        var index = ProjectIndex.Build(projectDir, Array.Empty<string>());

        var files = Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        // ignoreErrors: a single unparseable file must not sink the scan (surfaces as an index warning
        // via ProjectIndex; its tag roots just don't contribute to the hint).
        var digest = DigestBuilder.DigestFiles(files, ignoreErrors: true);

        // block name -> its tag roots, for the inline-implementation hint (only real blocks carry roots;
        // DBs/UDTs/tag-tables digest to empty TagRoots).
        var blockTagRoots = digest.Files
            .Where(f => f.FileError is null && f.Name is not null && f.TagRoots.Count > 0)
            .ToDictionary(f => f.Name!, f => (IReadOnlyList<string>)f.TagRoots, StringComparer.Ordinal);

        var targets = new List<ReqTarget>(doc.Requirements.Count);
        foreach (var req in doc.Requirements)
        {
            targets.Add(Evaluate(req, doc, index, blockTagRoots));
        }

        return new TargetScanReport(targets, index.Warnings);
    }

    private static ReqTarget Evaluate(
        ParsedReq req,
        RequirementsDocument doc,
        ProjectIndex index,
        IReadOnlyDictionary<string, IReadOnlyList<string>> blockTagRoots)
    {
        if (req.Withdrawn)
        {
            return new ReqTarget(
                req.Id, req.Title, req.ReqClass, TargetOutcome.Withdrawn,
                new[] { "withdrawn" }, Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<string>(), Array.Empty<string>());
        }

        var proposedTags = req.NamedTags.Where(t => !Exists(index, t)).ToList();

        var openQuestions = req.LinkedQuestions
            .Select(q => doc.Questions.TryGetValue(q, out var parsed) ? parsed : new ParsedQuestion(q, QuestionStatus.Unknown, "unknown"))
            .Where(q => q.Status != QuestionStatus.Resolved)
            .Select(q => $"{q.Id} ({q.RawStatus})")
            .ToList();

        var disqualifiers = new List<string>();
        if (string.Equals(req.ReqClass, "HMI", StringComparison.OrdinalIgnoreCase))
        {
            disqualifiers.Add("hmi-only");
        }

        if (string.Equals(req.ReqClass, "out-of-scope", StringComparison.OrdinalIgnoreCase))
        {
            disqualifiers.Add("out-of-scope");
        }

        if (proposedTags.Count > 0)
        {
            disqualifiers.Add("proposed-tag-blocked");
        }

        if (openQuestions.Count > 0)
        {
            disqualifiers.Add("q-open");
        }

        if (disqualifiers.Count > 0)
        {
            return new ReqTarget(
                req.Id, req.Title, req.ReqClass, TargetOutcome.Disqualified,
                disqualifiers, proposedTags, openQuestions, Array.Empty<string>(), Array.Empty<string>());
        }

        // Mechanically clean. Inline-implementation hint: which as-built blocks already reference this
        // REQ's exists-tags (by root). Soft/semantic — a separate bucket, never a mechanical verdict.
        var existsRoots = req.NamedTags
            .Where(t => Exists(index, t))
            .Select(RootComponent)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var implementedBlocks = new List<string>();
        var overlapRoots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (blockName, roots) in blockTagRoots.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var shared = roots.Where(existsRoots.Contains).ToList();
            if (shared.Count > 0)
            {
                implementedBlocks.Add(blockName);
                foreach (var r in shared)
                {
                    overlapRoots.Add(r);
                }
            }
        }

        var outcome = implementedBlocks.Count > 0 ? TargetOutcome.LikelyImplemented : TargetOutcome.Candidate;
        return new ReqTarget(
            req.Id, req.Title, req.ReqClass, outcome,
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            implementedBlocks, overlapRoots.OrderBy(r => r, StringComparer.Ordinal).ToList());
    }

    // Exists = resolves as any named entity in the export: tag root, DB, block, or UDT. Broader than
    // tagstatus's tag-root-only check because a register names blocks and UDTs too, and classifying
    // one of those as "proposed" would be a false disqualification. Root-level, like preflight —
    // member existence within an existing DB stays TIA's compile-time check.
    private static bool Exists(ProjectIndex index, string name)
    {
        var root = RootComponent(name);
        return index.ResolvesAsTagRoot(name) || index.ResolvesAsTagRoot(root) ||
               index.ResolvesAsBlock(name) || index.ResolvesAsType(name);
    }

    private static string RootComponent(string name) =>
        AccessNode.FromDottedPath(0, "GlobalVariable", name).ComponentPath[0];
}
