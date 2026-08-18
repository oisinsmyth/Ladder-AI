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
                     DriftStatus.Drifted, DriftStatus.PairingFailure, DriftStatus.ExportOnly,
                     DriftStatus.Error, DriftStatus.Skipped, DriftStatus.Match,
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
            .Append(Count(counts, DriftStatus.Error)).Append(" error, ")
            .Append(Count(counts, DriftStatus.PairingFailure)).Append(" pairing-failure\n");

        // THE DENOMINATOR, printed on every run (2026-08-14). Every other number in the summary is a
        // reason a comparison did NOT happen; this is the count that did. Without it "0 drifted" reads
        // identically whether ninety objects matched, the directory was empty, every .ir failed to
        // parse, or the path pointed one level above the files — all three of those measured, all three
        // exited 0.
        sb.Append("COMPARED: ").Append(report.ComparedCount).Append(" object(s) put through the Normalizer");
        if (report.ProjectDir is not null && report.ExportsDir is not null)
        {
            sb.Append("  (project=").Append(report.ProjectDir).Append("  exports=").Append(report.ExportsDir).Append(')');
        }

        sb.Append('\n');

        // WHOSE DOCUMENTS WERE ON THE OTHER SIDE. Printed on every run beside the denominator, because
        // COMPARED: 101 against 101 files this converter wrote itself is a tautology wearing a
        // denominator — the one shape of "examined nothing" that does not show up as a zero.
        if (report.ComparedCount > 0)
        {
            sb.Append("PROVENANCE: ").Append(report.TiaExportCount).Append(" of ").Append(report.ComparedCount)
                .Append(" export(s) compared carry TIA's <DocumentInfo>");
            if (report.NonTiaExportCount > 0)
            {
                sb.Append("; ").Append(report.NonTiaExportCount)
                    .Append(" do NOT and could be converter output (`to-xml` writes BESIDE ITS INPUT by default)");
            }

            sb.Append('\n');
        }

        if (report.ComparedNothingFromTia)
        {
            sb.Append("NO TIA EXPORT WAS COMPARED — this is not a pass. Every one of the ")
                .Append(report.ComparedCount).Append(" document(s) on the other side of these\n")
                .Append("       comparisons lacks TIA's <DocumentInfo>, so each could be this converter's own `to-xml`\n")
                .Append("       output. Comparing the converter against itself MATCHES by construction and says nothing\n")
                .Append("       about the project. Point --exports at a directory of real exports (openness-cli export-all).\n");
        }

        if (report.ExaminedNothing)
        {
            sb.Append("NOTHING COMPARED — this is not a pass. No object reached the Normalizer, so nothing is\n")
                .Append("       known about drift in either direction. Check that both paths hold the files themselves:\n")
                .Append("       each walk is top-level only, so a parent directory finds nothing and reports it clean.\n");
        }

        // What drift-check does NOT compare, said on every run rather than only when it bites.
        // `converter compare` prints a MEMORYLAYOUT line every time; this had no counterpart, so a MATCH
        // here read as a stronger claim than it is — and `compare` REFUSES (exit 2, NOT COMPARED) the
        // very pair this reports as a MATCH, because converter output declares no MemoryLayout while a
        // TIA export declares one. Measured on FB_PusherControl, 2026-08-14.
        if (report.ComparedCount > 0)
        {
            sb.Append("SCOPE: MemoryLayout is NOT compared — converter output never emits it, so the Normalizer\n")
                .Append("       holds neither side to the other's. `converter compare` on two TIA exports does check it.\n");
        }

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
        DriftStatus.PairingFailure => "PAIRING-FAILURE",
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
                fromTia = e.FromTia,
            }),
            // The denominator and the examined-nothing flag, so a --json consumer can tell "nothing
            // drifted" from "nothing was compared" without parsing the text.
            comparedCount = report.ComparedCount,
            examinedNothing = report.ExaminedNothing,
            // The provenance denominator: a consumer must be able to tell "nothing drifted" from
            // "nothing on the other side came from TIA" without parsing the text.
            tiaExportCount = report.TiaExportCount,
            nonTiaExportCount = report.NonTiaExportCount,
            comparedNothingFromTia = report.ComparedNothingFromTia,
            projectDir = report.ProjectDir,
            exportsDir = report.ExportsDir,
            hasDrift = report.HasDrift,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
