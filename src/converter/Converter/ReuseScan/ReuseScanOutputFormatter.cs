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

        // THE DENOMINATOR. "0 block(s) matched" over 43 files and over 0 files are different facts, and
        // this report printed neither of them until 2026-08-14 — so a wrong --project read exactly like
        // a thorough scan, and exit 0 here LICENSES "nothing to reuse, write a new block".
        sb.Append("SUMMARY: ").Append(report.Matches.Count).Append(" block(s) matched of ")
            .Append(report.FilesScanned).Append(" file(s) scanned\n");

        if (report.Absent.Count > 0)
        {
            // Named whether or not it gates: a partly-absent query is a real answer with a caveat, and
            // a wholly-absent one is not an answer at all.
            sb.Append("ABSENT: ").Append(string.Join(", ", report.Absent))
                .Append(" — queried tag root(s) that appear in NO block of this corpus. ")
                .Append("`--kind` is validated against a known set; `--tag` is only checkable against the corpus itself.\n");
        }

        if (report.AskedAboutNothing)
        {
            sb.Append("NOTHING TO ANSWER FROM — every queried tag root is absent from this corpus, so ")
                .Append("\"0 matched\" is not evidence there is nothing to reuse.\n");
        }

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
            filesScanned = report.FilesScanned,
            absentTagRoots = report.Absent,
            askedAboutNothing = report.AskedAboutNothing,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
