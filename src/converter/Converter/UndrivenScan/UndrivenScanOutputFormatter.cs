using System.Text;
using System.Text.Json;

namespace Converter.UndrivenScan;

// Mirrors the Review/Preflight/Trace "one record, two renderers" pattern.
public static class UndrivenScanOutputFormatter
{
    public static string FormatText(UndrivenScanReport report)
    {
        var sb = new StringBuilder();

        // Denominator first: an "undriven" claim is only ever relative to the files actually walked.
        sb.Append("undriven-scan  project=").Append(report.ProjectDir)
            .Append(" (").Append(report.FilesScanned).Append(" file(s) scanned)")
            .Append("  fb=").Append(report.FbName).Append('\n');
        if (report.Callers.Count > 0)
        {
            sb.Append("callers: ").Append(string.Join(", ", report.Callers)).Append('\n');
        }

        foreach (var group in report.Members.GroupBy(m => m.Instance).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            sb.Append('\n').Append(group.Key).Append('\n');
            foreach (var m in group.OrderBy(m => m.Member, StringComparer.Ordinal))
            {
                sb.Append("  ").Append(m.Member.PadRight(28)).Append(Label(m));
                if (m.Writers.Count > 0)
                {
                    sb.Append("  ").Append(string.Join(", ", m.Writers));
                }

                sb.Append('\n');

                if (m.NameJoinHints.Count > 0)
                {
                    // Explicitly labelled as a heuristic so nobody mistakes it for a fact.
                    sb.Append("      hint (name-token match, heuristic — not part of the finding): ")
                        .Append(string.Join(", ", m.NameJoinHints)).Append('\n');
                }
            }
        }

        foreach (var warning in report.Warnings)
        {
            sb.Append("WARNING: ").Append(warning).Append('\n');
        }

        var undriven = report.Members.Count(m => m.State == DriveState.Undriven);
        var disarmed = report.Members.Count(m => m.State == DriveState.Disarmed);
        var defaulted = report.Members.Count(m => m.State == DriveState.UndrivenDefault);
        var dead = report.Members.Count(m => m.State == DriveState.DeadInterfaceMember);

        sb.Append("\nSUMMARY: ").Append(report.Members.Count).Append(" member/instance pair(s), ")
            .Append(undriven).Append(" undriven, ")
            .Append(disarmed).Append(" disarmed, ")
            .Append(defaulted).Append(" defaulted, ")
            .Append(dead).Append(" dead-interface\n");

        // A zero-row summary reads exactly like a clean sweep, so the reason it is zero goes on its own
        // line rather than only on stderr - the text output is what gets pasted into a hand-back.
        if (report.ExaminedNothing)
        {
            sb.Append("NOTHING EXAMINED - this is not a pass: ").Append(report.Scope switch
            {
                ScanScope.UnknownBlock => "no block of that name is in the corpus.",
                ScanScope.NoInstances => "the block exists but nothing instantiates it.",
                ScanScope.NoInstancesMatchedFilter => "--instance matched none of the block's instances.",
                ScanScope.NoMembersInScope =>
                    "the block has instances, but every interface member is one the FB itself writes, so no "
                    + "caller-driven input was left to resolve.",
                _ => "the scan produced no member/instance rows.",
            }).Append('\n');
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(UndrivenScanReport report)
    {
        var payload = new
        {
            project = report.ProjectDir,
            filesScanned = report.FilesScanned,
            fb = report.FbName,
            callers = report.Callers,
            members = report.Members.Select(m => new
            {
                instance = m.Instance,
                member = m.Member,
                state = m.State.ToString().ToLowerInvariant(),
                startValue = m.StartValue,
                writers = m.Writers,
                nameJoinHints = m.NameJoinHints,
            }),
            hasFindings = report.HasFindings,
            // The JSON carried neither of these, so a consumer reading `members: []` + `hasFindings:
            // false` could not tell a clean scan from one that examined nothing - the same collapse the
            // exit code had.
            scope = report.Scope.ToString(),
            examinedNothing = report.ExaminedNothing,
            warnings = report.Warnings,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string Label(MemberDrive m) => m.State switch
    {
        DriveState.Driven => "driven",
        DriveState.Disarmed => "DISARMED",
        DriveState.UndrivenDefault => $"undriven (default {m.StartValue})",
        DriveState.DeadInterfaceMember => "DEAD-INTERFACE (no writer; the FB never reads it either)",
        _ => "UNDRIVEN",
    };
}
