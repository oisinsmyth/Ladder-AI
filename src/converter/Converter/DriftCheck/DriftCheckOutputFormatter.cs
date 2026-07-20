using System.Text;
using System.Text.Json;

namespace Converter.DriftCheck;

// One record set, two renderers (Digest/TagStatus pattern). Text leads with the DRIFTED blocks (the
// actionable signal); JSON carries every entry for a consumer.
public static class DriftCheckOutputFormatter
{
    public static string FormatText(DriftCheckReport report)
    {
        var sb = new StringBuilder();

        foreach (var status in new[] { DriftStatus.Drifted, DriftStatus.Error, DriftStatus.Skipped, DriftStatus.Match })
        {
            foreach (var e in report.Entries.Where(e => e.Status == status))
            {
                sb.Append(Label(e.Status)).Append(": ").Append(e.Name);
                if (e.Detail is not null)
                {
                    sb.Append("  (").Append(e.Detail).Append(')');
                }

                sb.Append('\n');
            }
        }

        var counts = report.Entries.GroupBy(e => e.Status).ToDictionary(g => g.Key, g => g.Count());
        sb.Append("SUMMARY: ")
            .Append(Count(counts, DriftStatus.Drifted)).Append(" drifted, ")
            .Append(Count(counts, DriftStatus.Match)).Append(" match, ")
            .Append(Count(counts, DriftStatus.Skipped)).Append(" skipped, ")
            .Append(Count(counts, DriftStatus.Error)).Append(" error\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    private static string Label(DriftStatus s) => s switch
    {
        DriftStatus.Drifted => "DRIFTED",
        DriftStatus.Match => "MATCH",
        DriftStatus.Skipped => "SKIPPED",
        DriftStatus.Error => "ERROR",
        _ => s.ToString(),
    };

    private static int Count(IReadOnlyDictionary<DriftStatus, int> counts, DriftStatus s) =>
        counts.TryGetValue(s, out var n) ? n : 0;

    public static string FormatJson(DriftCheckReport report)
    {
        var payload = new
        {
            entries = report.Entries.Select(e => new
            {
                name = e.Name,
                irPath = e.IrPath,
                xmlPath = e.XmlPath,
                status = e.Status.ToString(),
                detail = e.Detail,
            }),
            hasDrift = report.HasDrift,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
