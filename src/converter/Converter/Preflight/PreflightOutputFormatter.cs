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

            if (file.Findings.Count == 0)
            {
                sb.Append("  CLEAN\n");
            }

            foreach (var finding in file.Findings)
            {
                sb.Append("  [").Append(finding.Check).Append("] ").Append(finding.Description).Append('\n');
            }

            sb.Append('\n');
        }

        foreach (var warning in report.IndexWarnings)
        {
            sb.Append("INDEX WARNING: ").Append(warning).Append('\n');
        }

        var totalFindings = report.Files.Sum(f => f.Findings.Count);
        sb.Append("SUMMARY: ").Append(report.Files.Count).Append(" file(s), ").Append(totalFindings).Append(" finding(s)\n");
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
                findings = f.Findings.Select(x => new { check = x.Check, description = x.Description }),
            }),
            indexWarnings = report.IndexWarnings,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
