using System.Text;
using System.Text.Json;

namespace Converter.TagStatus;

// Mirrors ReviewOutputFormatter/PreflightOutputFormatter's "one record, two renderers" pattern.
public static class TagStatusOutputFormatter
{
    public static string FormatText(TagStatusReport report)
    {
        var sb = new StringBuilder();

        foreach (var e in report.Entries)
        {
            sb.Append(e.Name).Append(" -> ").Append(e.Exists ? "EXISTS" : "PROPOSED");
            if (e.Root != e.Name)
            {
                sb.Append(" (root: ").Append(e.Root).Append(')');
            }

            sb.Append('\n');
        }

        foreach (var warning in report.IndexWarnings)
        {
            sb.Append("INDEX WARNING: ").Append(warning).Append('\n');
        }

        var proposed = report.Entries.Count(e => !e.Exists);
        sb.Append("SUMMARY: ").Append(report.Entries.Count).Append(" name(s), ").Append(proposed).Append(" proposed\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(TagStatusReport report)
    {
        var payload = new
        {
            names = report.Entries.Select(e => new { name = e.Name, root = e.Root, exists = e.Exists }),
            indexWarnings = report.IndexWarnings,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
