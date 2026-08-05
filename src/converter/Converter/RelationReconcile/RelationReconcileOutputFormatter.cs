using System.Text;
using System.Text.Json;

namespace Converter.RelationReconcile;

// Mirrors the Review/Preflight/Trace "one record, two renderers" pattern.
public static class RelationReconcileOutputFormatter
{
    private const int MaxListed = 20;

    public static string FormatText(RelationReconcileReport report)
    {
        var sb = new StringBuilder();
        sb.Append("relation-reconcile\n\n");

        sb.Append("legs\n");
        foreach (var leg in report.Legs)
        {
            sb.Append("  ").Append(leg.Kind.ToString().ToLowerInvariant().PadRight(10));
            sb.Append(leg.Present ? $"{leg.Count} relation(s)" : "ABSENT");
            sb.Append('\n');
        }

        var real = report.Differences.Where(d => d.MissingInTo.Count > 0).ToList();
        sb.Append('\n').Append("differences\n");
        if (real.Count == 0)
        {
            sb.Append("  none — every present leg carries the same (instance, relation) set\n");
        }
        else
        {
            foreach (var d in real)
            {
                sb.Append("  ").Append(d.From.ToString().ToLowerInvariant()).Append(" -> ")
                    .Append(d.To.ToString().ToLowerInvariant()).Append(": ")
                    .Append(d.MissingInTo.Count).Append(" missing in ")
                    .Append(d.To.ToString().ToLowerInvariant()).Append('\n');
                foreach (var key in d.MissingInTo.Take(MaxListed))
                {
                    sb.Append("      ").Append(key).Append('\n');
                }

                if (d.MissingInTo.Count > MaxListed)
                {
                    sb.Append("      … ").Append(d.MissingInTo.Count - MaxListed).Append(" more\n");
                }
            }
        }

        if (report.ProjectDir is not null)
        {
            sb.Append('\n').Append("citations (verified-cross-block rows)  — corpus: ")
                .Append(report.ProjectDir).Append(" (").Append(report.FilesScanned).Append(" file(s))\n");

            if (report.Citations.Count == 0)
            {
                sb.Append("  0 verified-cross-block rows to check\n");
            }

            foreach (var c in report.Citations)
            {
                sb.Append("  ").Append(c.Relation).Append(c.IsFinding ? "  FINDING" : "  ok").Append('\n');
                foreach (var t in c.Tokens)
                {
                    sb.Append("      `").Append(t.Token).Append("` -> ").Append(Describe(t)).Append('\n');
                }

                if (c.Tokens.Count == 0)
                {
                    sb.Append("      (no identifier-shaped token in the evidence cell)\n");
                }
            }
        }

        foreach (var warning in report.Warnings)
        {
            sb.Append("\nNOTE: ").Append(warning).Append('\n');
        }

        sb.Append("\nSUMMARY: ").Append(real.Sum(d => d.MissingInTo.Count)).Append(" difference(s), ")
            .Append(report.CitationFindings.Count).Append(" citation finding(s)\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    // Every absence claim carries its denominator: a partial export is normal here, so "no writer"
    // is a scope fact rather than a defect on its own.
    private static string Describe(CitationToken t) => t.Resolution switch
    {
        TokenResolution.WrittenMember => $"resolves to a member with {t.WriterCount} writer(s)",
        TokenResolution.DisarmedWriters => $"resolves; {t.WriterCount} writer(s), ALL disarmed (built but switched off)",
        TokenResolution.DeclarationOnly => "resolves to a DECLARED member with no writer in this export (partial exports are normal — a scope fact, not a defect)",
        _ => "does not resolve as a member/tag in this export",
    };

    public static string FormatJson(RelationReconcileReport report)
    {
        var payload = new
        {
            legs = report.Legs.Select(l => new
            {
                leg = l.Kind.ToString().ToLowerInvariant(),
                present = l.Present,
                count = l.Count,
            }),
            differences = report.Differences.Where(d => d.MissingInTo.Count > 0).Select(d => new
            {
                from = d.From.ToString().ToLowerInvariant(),
                to = d.To.ToString().ToLowerInvariant(),
                missingInTo = d.MissingInTo.Select(k => new { instance = k.Instance, id = k.Id }),
            }),
            citations = report.Citations.Select(c => new
            {
                instance = c.Relation.Instance,
                id = c.Relation.Id,
                isFinding = c.IsFinding,
                tokens = c.Tokens.Select(t => new
                {
                    token = t.Token,
                    resolution = t.Resolution.ToString().ToLowerInvariant(),
                    writerCount = t.WriterCount,
                }),
            }),
            project = report.ProjectDir,
            filesScanned = report.FilesScanned,
            hasFindings = report.HasFindings,
            warnings = report.Warnings,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
