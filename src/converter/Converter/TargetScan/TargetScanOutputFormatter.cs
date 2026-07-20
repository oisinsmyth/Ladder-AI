using System.Text;
using System.Text.Json;

namespace Converter.TargetScan;

// One record set, two renderers. Text groups REQs by outcome (candidates first — the whole point is a
// short list to confirm); JSON carries the full per-REQ detail for a stage that consumes it.
public static class TargetScanOutputFormatter
{
    public static string FormatText(TargetScanReport report)
    {
        var sb = new StringBuilder();

        var candidates = report.Requirements.Where(r => r.Outcome == TargetOutcome.Candidate).ToList();
        var likely = report.Requirements.Where(r => r.Outcome == TargetOutcome.LikelyImplemented).ToList();
        var disqualified = report.Requirements.Where(r => r.Outcome == TargetOutcome.Disqualified).ToList();
        var withdrawn = report.Requirements.Where(r => r.Outcome == TargetOutcome.Withdrawn).ToList();

        sb.Append("REQUIREMENTS: ").Append(report.Requirements.Count).Append(" parsed | CANDIDATES: ")
            .Append(candidates.Count).Append('\n');

        if (candidates.Count > 0)
        {
            sb.Append("\nCANDIDATE (mechanically clean, no as-built block references its tags):\n");
            foreach (var r in candidates)
            {
                sb.Append("  ").Append(r.ReqId).Append("  [").Append(r.ReqClass).Append("]  ").Append(r.Title).Append('\n');
            }
        }

        if (likely.Count > 0)
        {
            sb.Append("\nLIKELY-IMPLEMENTED (heuristic — mechanically clean but tags already in a block; confirm with reuse-scan / a read):\n");
            foreach (var r in likely)
            {
                sb.Append("  ").Append(r.ReqId).Append("  [").Append(r.ReqClass).Append("]  ").Append(r.Title).Append('\n');
                sb.Append("    implemented-in: ").Append(string.Join(", ", r.ImplementedInBlocks))
                    .Append("  (shared roots: ").Append(string.Join(", ", r.ImplementedViaRoots)).Append(")\n");
            }
        }

        if (disqualified.Count > 0)
        {
            sb.Append("\nDISQUALIFIED (mechanical):\n");
            foreach (var r in disqualified)
            {
                sb.Append("  ").Append(r.ReqId).Append("  [").Append(r.ReqClass).Append("]  ").Append(r.Title).Append('\n');
                sb.Append("    reasons: ").Append(FormatReasons(r)).Append('\n');
            }
        }

        if (withdrawn.Count > 0)
        {
            sb.Append("\nWITHDRAWN:\n");
            foreach (var r in withdrawn)
            {
                sb.Append("  ").Append(r.ReqId).Append("  ").Append(r.Title).Append('\n');
            }
        }

        foreach (var warning in report.IndexWarnings)
        {
            sb.Append("\nINDEX WARNING: ").Append(warning);
        }

        sb.Append("\nSUMMARY: ").Append(candidates.Count).Append(" candidate(s), ").Append(likely.Count)
            .Append(" likely-implemented, ").Append(disqualified.Count).Append(" disqualified, ")
            .Append(withdrawn.Count).Append(" withdrawn\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    private static string FormatReasons(ReqTarget r)
    {
        var parts = new List<string>();
        foreach (var d in r.Disqualifiers)
        {
            parts.Add(d switch
            {
                "proposed-tag-blocked" => $"proposed-tag-blocked [{string.Join(", ", r.ProposedTags)}]",
                "q-open" => $"q-open [{string.Join(", ", r.OpenQuestions)}]",
                _ => d,
            });
        }

        return string.Join(", ", parts);
    }

    public static string FormatJson(TargetScanReport report)
    {
        var payload = new
        {
            requirements = report.Requirements.Select(r => new
            {
                reqId = r.ReqId,
                title = r.Title,
                reqClass = r.ReqClass,
                outcome = r.Outcome.ToString(),
                disqualifiers = r.Disqualifiers,
                proposedTags = r.ProposedTags,
                openQuestions = r.OpenQuestions,
                implementedInBlocks = r.ImplementedInBlocks,
                implementedViaRoots = r.ImplementedViaRoots,
            }),
            indexWarnings = report.IndexWarnings,
            candidateCount = report.Candidates.Count,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
