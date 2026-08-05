using System.Text;
using System.Text.Json;

namespace Converter.CandidateScan;

// Mirrors the Review/Preflight/Trace "one record, two renderers" pattern.
public static class CandidateScanOutputFormatter
{
    public static string FormatText(CandidateScanReport report)
    {
        var sb = new StringBuilder();

        // The header states the DENOMINATOR: a candidate set is only ever "everything in scope in THIS
        // corpus", and a partial export makes an empty set a scope fact, not a finding.
        sb.Append("candidate-scan  project=").Append(report.ProjectDir)
            .Append(" (").Append(report.FilesScanned).Append(" file(s) scanned)")
            .Append("  fb=").Append(report.FbName);
        if (report.Instance is not null)
        {
            sb.Append("  instance=").Append(report.Instance);
        }

        sb.Append('\n');
        sb.Append("scope=").Append(report.Scopes.Count == 0 ? "(none)" : string.Join(",", report.Scopes))
            .Append("  type=").Append(report.TypeFilter ?? "any")
            .Append("  direction=").Append(report.Direction).Append('\n');

        sb.Append('\n').Append("IO candidates (").Append(report.IoCandidates.Count).Append(")\n");
        foreach (var c in report.IoCandidates)
        {
            sb.Append("  ").Append(c.Path).Append("  ").Append(c.Type);
            if (c.ReadBy.Count > 0)
            {
                sb.Append("  read by ").Append(string.Join(", ", c.ReadBy));
            }

            sb.Append('\n');
        }

        sb.Append('\n').Append("FB members (").Append(report.FbCandidates.Count).Append(")  — direction computed from the usage graph\n");
        foreach (var c in report.FbCandidates)
        {
            sb.Append("  ").Append(c.Member).Append("  ").Append(c.Type)
                .Append("  ").Append(c.Role.ToString().ToLowerInvariant());
            if (c.WrittenAt.Count > 0)
            {
                sb.Append("  written ").Append(string.Join(", ", c.WrittenAt));
            }

            sb.Append('\n');
        }

        sb.Append('\n').Append("family: ").Append(report.Family.SameTypedIoSignals)
            .Append(" same-typed IO signal(s) in scope; ").Append(report.Family.FbMembersOfMatchingDirection)
            .Append(" FB member(s) of matching direction\n");

        foreach (var warning in report.Warnings)
        {
            sb.Append("WARNING: ").Append(warning).Append('\n');
        }

        sb.Append("CANDIDATE SET SIZE: ").Append(report.Size);
        if (report.PhraseMatches.Count > 0)
        {
            // Advisory, and labelled as such — the exit code keys off the unfiltered size.
            sb.Append("   (phrase matches ").Append(report.PhraseMatches.Count)
                .Append(" of them — advisory, not a narrowing)");
        }

        sb.Append('\n');
        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(CandidateScanReport report)
    {
        var payload = new
        {
            project = report.ProjectDir,
            filesScanned = report.FilesScanned,
            fb = report.FbName,
            instance = report.Instance,
            scopes = report.Scopes,
            type = report.TypeFilter,
            direction = report.Direction,
            ioCandidates = report.IoCandidates.Select(c => new { path = c.Path, type = c.Type, readBy = c.ReadBy }),
            fbCandidates = report.FbCandidates.Select(c => new
            {
                member = c.Member,
                type = c.Type,
                role = c.Role.ToString().ToLowerInvariant(),
                writtenAt = c.WrittenAt,
            }),
            family = new
            {
                sameTypedIoSignals = report.Family.SameTypedIoSignals,
                fbMembersOfMatchingDirection = report.Family.FbMembersOfMatchingDirection,
            },
            phraseMatches = report.PhraseMatches,
            size = report.Size,
            hasChoice = report.HasChoice,
            warnings = report.Warnings,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
