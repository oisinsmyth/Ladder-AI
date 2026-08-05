using System.Text;
using System.Text.Json;

namespace Converter.SignalSweep;

// Mirrors the Review/Preflight/Trace "one record, two renderers" pattern.
public static class SignalSweepOutputFormatter
{
    private const int MaxListed = 40;

    public static string FormatText(SignalSweepReport report)
    {
        var sb = new StringBuilder();
        sb.Append("signal-sweep  project=").Append(report.ProjectDir)
            .Append(" (").Append(report.FilesScanned).Append(" file(s) scanned)\n\n");

        sb.Append("swept ").Append(report.Swept)
            .Append("  claimed ").Append(report.Claimed)
            .Append("  disposed ").Append(report.Disposed)
            .Append("  unaccounted ").Append(report.Unaccounted.Count).Append('\n');

        if (!report.DispositionTableRead)
        {
            sb.Append("(no --unclaimed disposition artifact given; 'disposed' is 0 by construction)\n");
        }

        sb.Append('\n').Append("by DB\n");
        foreach (var d in report.ByDb)
        {
            sb.Append("  ").Append(d.Db.PadRight(20))
                .Append("swept ").Append(d.Swept.ToString().PadLeft(4))
                .Append("   claimed ").Append(d.Claimed.ToString().PadLeft(4))
                .Append("   disposed ").Append(d.Disposed.ToString().PadLeft(4))
                .Append("   unaccounted ").Append(d.Unaccounted.ToString().PadLeft(4))
                .Append('\n');
        }

        var unaccounted = report.Unaccounted;
        if (unaccounted.Count > 0)
        {
            sb.Append('\n').Append("unaccounted — in no spec, and in no disposition table\n");
            foreach (var s in unaccounted.Take(MaxListed))
            {
                sb.Append("  ").Append(s.Path).Append('\n');
            }

            if (unaccounted.Count > MaxListed)
            {
                sb.Append("  … ").Append(unaccounted.Count - MaxListed).Append(" more\n");
            }

            // The tool computes coverage; it never rules on whether a gap matters.
            sb.Append("(coverage fact, not a verdict: whether an unaccounted signal implies control is the engineer's call)\n");
            sb.Append("(a signal dispositioned only in PROSE — e.g. a row reading \"E-stop members | safety\" —\n")
                .Append(" reads as unaccounted here, because prose is not machine-checkable. Backticking the\n")
                .Append(" member names in the disposition table is the fix, and is the point: an approximate\n")
                .Append(" disposition cannot support a completeness claim.)\n");
        }

        foreach (var warning in report.Warnings)
        {
            sb.Append("WARNING: ").Append(warning).Append('\n');
        }

        sb.Append("\nSUMMARY: ").Append(report.Swept).Append(" swept, ")
            .Append(report.Claimed).Append(" claimed, ")
            .Append(report.Disposed).Append(" disposed, ")
            .Append(unaccounted.Count).Append(" unaccounted\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(SignalSweepReport report)
    {
        var payload = new
        {
            project = report.ProjectDir,
            filesScanned = report.FilesScanned,
            swept = report.Swept,
            claimed = report.Claimed,
            disposed = report.Disposed,
            dispositionTableRead = report.DispositionTableRead,
            byDb = report.ByDb.Select(d => new
            {
                db = d.Db,
                swept = d.Swept,
                claimed = d.Claimed,
                disposed = d.Disposed,
                unaccounted = d.Unaccounted,
            }),
            unaccounted = report.Unaccounted.Select(s => s.Path),
            hasFindings = report.HasFindings,
            warnings = report.Warnings,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
