using System.Text;
using System.Text.Json;

namespace Converter.SignalSet;

// Mirrors the Review/Preflight/Trace "one record, two renderers" pattern.
public static class SignalSetOutputFormatter
{
    public static string FormatText(SignalSetReport report)
    {
        var sb = new StringBuilder();

        // Denominator first, as every mechanical-floor command states it: a signal set is only ever
        // "everything this corpus declares", and a partial export makes an empty half a scope fact.
        sb.Append("signal-set  project=").Append(report.ProjectDir)
            .Append(" (").Append(report.FilesScanned).Append(" file(s) scanned)")
            .Append("  block=").Append(report.BlockName);

        if (report.Scope == SignalSetScope.UnknownBlock)
        {
            sb.Append(" (NOT FOUND in this corpus - nothing below was examined)");
        }

        sb.Append('\n');
        sb.Append("origin=").Append(report.OriginFilter)
            .Append("  type=").Append(report.TypeFilter ?? "any")
            .Append("  direction=").Append(report.DirectionFilter).Append('\n');

        // Said once, at the top, because it is the fact the whole document is read through: a harness
        // stimulates what the block READS and observes what it WRITES.
        sb.Append("direction is relative to ").Append(report.BlockName)
            .Append("; writers/readers are project-wide\n");

        Section(sb, report, "interface", e => e.Origin == SignalDeclaration.Interface);
        Section(sb, report, "referenced", e => e.Origin != SignalDeclaration.Interface);

        foreach (var warning in report.Warnings)
        {
            sb.Append("WARNING: ").Append(warning).Append('\n');
        }

        sb.Append("\nSIGNAL SET: ").Append(report.Entries.Count).Append(" signal(s) — ")
            .Append(report.InterfaceCount).Append(" declared on the interface, ")
            .Append(report.ReferencedCount).Append(" referenced\n");

        // A zero-row document reads exactly like a block with no signals, so the reason it is zero goes
        // in the text output rather than only on stderr - the text is what gets pasted into a hand-back.
        if (report.ExaminedNothing)
        {
            sb.Append("NOTHING EXAMINED - this is not a pass: ").Append(report.Scope switch
            {
                SignalSetScope.UnknownBlock => "no block of that name is in the corpus.",
                _ => "no signal survived the origin/type/direction filters.",
            }).Append('\n');
        }

        if (report.Partial)
        {
            sb.Append("PARTIAL - ").Append(report.Warnings.Count)
                .Append(" file(s) could not be read, so this set is not a complete statement of the "
                        + "block's signals.\n");
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    private static void Section(
        StringBuilder sb,
        SignalSetReport report,
        string heading,
        Func<SignalEntry, bool> predicate)
    {
        var rows = report.Entries.Where(predicate).ToList();
        sb.Append('\n').Append(heading).Append(" (").Append(rows.Count).Append(")\n");

        foreach (var e in rows)
        {
            sb.Append("  ").Append(e.Member.PadRight(34))
                .Append((e.Type ?? "(undeclared)").PadRight(14))
                .Append(e.Direction.ToString().ToLowerInvariant().PadRight(9))
                .Append(e.Origin.ToString().ToLowerInvariant());

            if (e.Retain)
            {
                sb.Append("  RETAIN");
            }

            if (e.StartValue is not null)
            {
                sb.Append("  start=").Append(e.StartValue);
            }

            sb.Append('\n');

            if (e.Writers.Count > 0)
            {
                sb.Append("      written ").Append(string.Join(", ", e.Writers)).Append('\n');
            }

            if (e.Readers.Count > 0)
            {
                sb.Append("      read ").Append(string.Join(", ", e.Readers)).Append('\n');
            }
        }
    }

    public static string FormatJson(SignalSetReport report)
    {
        var payload = new
        {
            project = report.ProjectDir,
            filesScanned = report.FilesScanned,
            block = report.BlockName,
            originFilter = report.OriginFilter,
            typeFilter = report.TypeFilter,
            directionFilter = report.DirectionFilter,
            signals = report.Entries.Select(e => new
            {
                member = e.Member,
                path = e.Path,
                type = e.Type,
                retain = e.Retain,
                startValue = e.StartValue,
                direction = e.Direction.ToString().ToLowerInvariant(),
                origin = e.Origin.ToString().ToLowerInvariant(),
                writers = e.Writers,
                readers = e.Readers,
            }),
            // The denominator a machine consumer needs to tell "no signals" from "nothing examined",
            // without reading the exit code — the gap candidate-scan's JSON carried until 2026-08-14.
            counts = new
            {
                total = report.Entries.Count,
                @interface = report.InterfaceCount,
                referenced = report.ReferencedCount,
            },
            scope = report.Scope.ToString(),
            examinedNothing = report.ExaminedNothing,
            partial = report.Partial,
            warnings = report.Warnings,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
