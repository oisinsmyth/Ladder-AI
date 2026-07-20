using System.Text;
using System.Text.Json;

namespace Converter.ReuseScan;

// Mirrors Digest/TagStatus "one record, two renderers". Text for a human reading the reuse-first
// pass; JSON for a stage that consumes it.
public static class ReuseScanOutputFormatter
{
    public static string FormatText(ReuseScanReport report)
    {
        var sb = new StringBuilder();

        var tagPart = report.QueryTagRoots.Count > 0 ? string.Join(", ", report.QueryTagRoots) : "(none)";
        var kindPart = report.QueryKinds.Count > 0 ? string.Join(", ", report.QueryKinds) : "(none)";
        sb.Append("QUERY: tags=[").Append(tagPart).Append("] kinds=[").Append(kindPart).Append("]\n");

        foreach (var match in report.Matches)
        {
            sb.Append("MATCH: ").Append(match.BlockName).Append(" (").Append(match.Kind).Append(")  ")
                .Append(match.FilePath).Append('\n');

            if (match.MatchedTags.Count > 0)
            {
                sb.Append("  tags: ").Append(string.Join(", ", match.MatchedTags)).Append('\n');
            }

            foreach (var network in match.MatchedNetworks)
            {
                sb.Append("  network ").Append(network.Number);
                if (!string.IsNullOrEmpty(network.Title))
                {
                    sb.Append(" \"").Append(network.Title).Append('"');
                }

                sb.Append(" — ").Append(string.Join(", ", network.MatchedKinds)).Append('\n');
            }
        }

        foreach (var error in report.FileErrors)
        {
            sb.Append("FILE ERROR: ").Append(error).Append('\n');
        }

        sb.Append("SUMMARY: ").Append(report.Matches.Count).Append(" block(s) matched\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(ReuseScanReport report)
    {
        var payload = new
        {
            queryTagRoots = report.QueryTagRoots,
            queryKinds = report.QueryKinds,
            matches = report.Matches.Select(m => new
            {
                filePath = m.FilePath,
                blockName = m.BlockName,
                kind = m.Kind,
                matchedTags = m.MatchedTags,
                matchedNetworks = m.MatchedNetworks.Select(n => new
                {
                    number = n.Number,
                    title = n.Title,
                    matchedKinds = n.MatchedKinds,
                }),
            }),
            fileErrors = report.FileErrors,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
