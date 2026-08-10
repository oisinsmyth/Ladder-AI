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

        foreach (var status in new[]
                 {
                     DriftStatus.Drifted, DriftStatus.ExportOnly, DriftStatus.Error, DriftStatus.Skipped, DriftStatus.Match,
                 })
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
            .Append(Count(counts, DriftStatus.ExportOnly)).Append(" export-only, ")
            .Append(Count(counts, DriftStatus.Error)).Append(" error\n");

        // FI-70. Whether an absence is a finding depends entirely on what filled the exports directory,
        // and the tool cannot know that — so it says which question it answered rather than letting a
        // green summary imply the stronger one.
        var absences = Count(counts, DriftStatus.Skipped) + Count(counts, DriftStatus.ExportOnly);
        if (report.Complete)
        {
            sb.Append("SCOPE: --complete — the exports dir was taken as the WHOLE picture, so the ")
                .Append(absences).Append(" absence(s) above are findings, not ordinary states.\n");
        }
        else if (absences > 0)
        {
            sb.Append("SCOPE: comparison only. ").Append(absences)
                .Append(" file(s) had no counterpart and were NOT judged — pass --complete when the exports\n")
                .Append("       dir is a full dump (e.g. straight from the controller) and an absence should fail.\n");
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    private static string Label(DriftStatus s) => s switch
    {
        DriftStatus.Drifted => "DRIFTED",
        DriftStatus.Match => "MATCH",
        DriftStatus.Skipped => "SKIPPED",
        DriftStatus.ExportOnly => "EXPORT-ONLY",
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
