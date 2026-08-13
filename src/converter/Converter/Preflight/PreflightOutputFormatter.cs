using System.Text;
using System.Text.Json;

namespace Converter.Preflight;

// Mirrors ReviewOutputFormatter/DigestOutputFormatter's "one record, two renderers" pattern.
public static class PreflightOutputFormatter
{
    public static string FormatText(PreflightReport report)
    {
        var sb = new StringBuilder();

        foreach (var file in report.Files)
        {
            sb.Append("FILE: ").Append(file.FilePath).Append('\n');
            if (file.Name is not null)
            {
                sb.Append("  NAME: ").Append(file.Name).Append('\n');
            }

            // "CLEAN" is about the GATING findings — a file whose only entries are the reported
            // harness-scope ones has passed, and must say so while still printing them.
            if (!file.Findings.Any(x => x.Gates))
            {
                sb.Append("  CLEAN\n");
            }

            foreach (var finding in file.Findings)
            {
                sb.Append(finding.Gates ? "  [" : "  [INFO, does not gate] [").Append(finding.Check).Append("] ").Append(finding.Description).Append('\n');
            }

            sb.Append('\n');
        }

        foreach (var warning in report.IndexWarnings)
        {
            sb.Append("INDEX WARNING: ").Append(warning).Append('\n');
        }

        var totalFindings = report.Files.Sum(f => f.Findings.Count(x => x.Gates));
        var nonGating = report.Files.Sum(f => f.Findings.Count(x => !x.Gates));
        sb.Append("SUMMARY: ").Append(report.Files.Count).Append(" file(s), ").Append(totalFindings).Append(" finding(s)");
        // Always appended, including the zero: an absent clause cannot distinguish "nothing was
        // exempted" from "the harness scope never ran".
        sb.Append(", ").Append(nonGating).Append(" reported but not gating (harness-scope)\n");
        sb.Append("PRE-FLIGHT ONLY: a static filter in front of the compile gate — import + block-level compile still decides (hard rule 4).\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(PreflightReport report)
    {
        var payload = new
        {
            files = report.Files.Select(f => new
            {
                filePath = f.FilePath,
                name = f.Name,
                findings = f.Findings.Select(x => new { check = x.Check, description = x.Description, gates = x.Gates }),
            }),
            indexWarnings = report.IndexWarnings,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
