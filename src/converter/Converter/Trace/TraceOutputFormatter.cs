using System.Text;
using System.Text.Json;

namespace Converter.Trace;

// Facts the review-functional reviewer embeds verbatim, then confirms semantically. One record set,
// two renderers.
public static class TraceOutputFormatter
{
    public static string FormatText(TraceReport report)
    {
        var sb = new StringBuilder();

        foreach (var req in report.Requirements)
        {
            sb.Append(req.Req).Append('\n');
            foreach (var hop in req.Hops)
            {
                sb.Append("  [").Append(hop.Verdict.ToString().ToUpperInvariant()).Append("] ")
                    .Append(HopLabel(hop.Hop)).Append(": ").Append(hop.Detail);
                if (hop.Evidence.Count > 0)
                {
                    sb.Append("  {").Append(string.Join("; ", hop.Evidence)).Append('}');
                }

                sb.Append('\n');
            }
        }

        foreach (var warning in report.Warnings)
        {
            sb.Append("INDEX WARNING: ").Append(warning).Append('\n');
        }

        var hopCount = report.Requirements.Sum(r => r.Hops.Count);
        var candidateCount = report.Requirements.Sum(r => r.Hops.Count(h => h.Verdict != Verdict.Ok));
        sb.Append("SUMMARY: ").Append(report.Requirements.Count).Append(" REQ(s), ")
            .Append(hopCount).Append(" hop(s), ").Append(candidateCount).Append(" non-ok candidate(s)\n");
        sb.Append("NOTE: candidate verdicts are facts for the reviewer to confirm — not an adjudicated pass.\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    private static string HopLabel(HopKind hop) => hop switch
    {
        HopKind.OutputPath => "output-path",
        HopKind.InterfaceChain => "interface-chain",
        HopKind.NumberConstraint => "number-constraint",
        _ => hop.ToString(),
    };

    public static string FormatJson(TraceReport report)
    {
        var payload = new
        {
            requirements = report.Requirements.Select(r => new
            {
                req = r.Req,
                hops = r.Hops.Select(h => new
                {
                    hop = h.Hop.ToString(),
                    verdict = h.Verdict.ToString(),
                    detail = h.Detail,
                    evidence = h.Evidence,
                }),
            }),
            warnings = report.Warnings,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
