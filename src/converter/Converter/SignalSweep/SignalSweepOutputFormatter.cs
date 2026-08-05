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

        // Grouped by CONTAINER, not by "the bit before the first dot": a tag-table tag has no dot, so
        // the old split rendered every flat tag as its own one-row "DB" (FI-45 item 2). Column width is
        // computed rather than fixed, because a fixed 20 silently broke alignment on longer names.
        sb.Append('\n').Append("by container (DB / tag table)\n");
        var width = Math.Clamp(
            report.ByContainer.Count == 0 ? 20 : report.ByContainer.Max(d => d.Container.Length),
            20,
            48);
        foreach (var d in report.ByContainer)
        {
            sb.Append("  ").Append(Label(d.Kind).PadRight(10)).Append(d.Container.PadRight(width)).Append("  ")
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
                sb.Append("  ").Append(s.Path);

                // A tag's own name carries no container, so naming its table is what lets the reader find
                // (or write) the disposition heading that would account for it.
                if (s.Kind == SignalContainerKind.TagTable)
                {
                    sb.Append("   (tag table ").Append(s.Container).Append(')');
                }

                sb.Append('\n');
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
            sb.Append("(a PLC tag is qualified by its TAG TABLE exactly as a member is qualified by its DB —\n")
                .Append(" disposition it under a `### `<TagTableName>`` heading, spelled as the export spells it.)\n");
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
            byContainer = report.ByContainer.Select(d => new
            {
                container = d.Container,
                kind = Label(d.Kind),
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

    private static string Label(SignalContainerKind kind) =>
        kind == SignalContainerKind.TagTable ? "tag table" : "DB";
}
