using Converter.Digest;
using Converter.SimaticMl;

namespace Converter.ReuseScan;

// Builds the reuse-scan report by running the existing DigestBuilder over the whole export and
// filtering its FileDigests — deliberately no new IR traversal (the digest already extracts the tag
// roots and per-network statement-kind summaries this query needs). Root extraction is the shared
// AccessNode.FromDottedPath primitive, so a literal-dot tag (Clock_0.5Hz) is never mis-split.
public static class ReuseScanRunner
{
    // The statement-kind labels DigestBuilder.SummarizeStatements emits (the "coil:2, timer:1" tokens).
    // A --kind must be one of these; validated at the CLI so a typo is caught, not silently unmatched.
    public static readonly IReadOnlyList<string> KnownKinds = new[]
    {
        "coil", "timer", "move", "wand", "call", "arith", "convert", "swap", "abs", "limit",
        "tsub", "tconv", "calc", "moveblk", "wait", "fillblk", "mb-master", "mb-commload",
    };

    public static ReuseScanReport Run(string projectDir, IReadOnlyList<string> tags, IReadOnlyList<string> kinds)
    {
        var tagRoots = tags
            .Select(RootComponent)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();
        var queryKinds = kinds.Distinct(StringComparer.Ordinal).ToList();

        var files = Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        // ignoreErrors: a single unparseable corpus file must not sink the whole scan — it surfaces as
        // a FileError line instead, exactly like digest's own --ignore-errors contract.
        var digest = DigestBuilder.DigestFiles(files, ignoreErrors: true);

        var matches = new List<ReuseBlockMatch>();
        var fileErrors = new List<string>();

        foreach (var file in digest.Files)
        {
            if (file.FileError is not null)
            {
                fileErrors.Add($"{file.FilePath}: {file.FileError}");
                continue;
            }

            var matchedTags = tagRoots
                .Where(r => file.TagRoots.Contains(r, StringComparer.Ordinal))
                .ToList();

            var matchedNetworks = new List<ReuseNetworkMatch>();
            foreach (var network in file.Networks)
            {
                var present = ParseKinds(network.Statements);
                var matchedKinds = queryKinds.Where(present.Contains).ToList();
                if (matchedKinds.Count > 0)
                {
                    matchedNetworks.Add(new ReuseNetworkMatch(network.Number, network.Title, matchedKinds));
                }
            }

            // A block is a candidate only if it satisfies EVERY queried filter group: all --tag roots
            // present (block-level) AND at least one network carrying a --kind. An empty group is
            // vacuously satisfied, so `--tag T` alone matches on tags and `--kind timer` alone on kinds.
            var tagGroupOk = tagRoots.Count == 0 || matchedTags.Count > 0;
            var kindGroupOk = queryKinds.Count == 0 || matchedNetworks.Count > 0;
            if (tagGroupOk && kindGroupOk && (matchedTags.Count > 0 || matchedNetworks.Count > 0))
            {
                matches.Add(new ReuseBlockMatch(
                    file.FilePath, file.Name ?? "?", file.Kind, matchedTags, matchedNetworks));
            }
        }

        return new ReuseScanReport(tagRoots, queryKinds, matches, fileErrors);
    }

    // Splits a digest statement summary ("coil:2, timer:1" or "-") into the set of kind labels present.
    private static HashSet<string> ParseKinds(string summary)
    {
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        if (summary == "-")
        {
            return kinds;
        }

        foreach (var token in summary.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = token.IndexOf(':');
            kinds.Add(colon >= 0 ? token[..colon] : token);
        }

        return kinds;
    }

    private static string RootComponent(string tagPath) =>
        AccessNode.FromDottedPath(0, "GlobalVariable", tagPath).ComponentPath[0];
}
