using System.Text;
using System.Text.Json;

namespace Converter.Claims;

// Mirrors the Review/Preflight/SignalSweep "one record, two renderers" pattern.
public static class ClaimsOutputFormatter
{
    public static string FormatOutcomeText(ClaimOutcome outcome)
    {
        var sb = new StringBuilder();
        sb.Append(outcome.Ok ? "CLAIMED   " : "REFUSED   ").Append(outcome.Reason).Append('\n');

        if (outcome.Claim is not null)
        {
            sb.Append("  kind    ").Append(ClaimKinds.ToToken(outcome.Claim.Kind)).Append('\n');
            sb.Append("  value   ").Append(outcome.Claim.Value).Append('\n');
            sb.Append("  agent   ").Append(outcome.Claim.Agent).Append('\n');
        }

        if (outcome.Holder is not null && !outcome.Ok)
        {
            sb.Append("  holder  ").Append(outcome.Holder.Agent).Append('\n');
            if (outcome.Holder.Purpose is not null)
            {
                sb.Append("  purpose ").Append(outcome.Holder.Purpose).Append('\n');
            }
        }

        return sb.ToString();
    }

    public static string FormatOutcomeJson(ClaimOutcome outcome) => JsonSerializer.Serialize(new
    {
        result = outcome.Result.ToString(),
        ok = outcome.Ok,
        reason = outcome.Reason,
        claim = outcome.Claim is null ? null : Describe(outcome.Claim),
        holder = outcome.Holder is null ? null : Describe(outcome.Holder),
    }, JsonOptions);

    public static string FormatReportText(ClaimsReport report)
    {
        var sb = new StringBuilder();
        sb.Append("claims  project=").Append(report.ProjectDir)
            .Append("  store=").Append(report.ClaimsDir).Append('\n');
        sb.Append("held ").Append(report.Claims.Count)
            .Append("  conflicts ").Append(report.Conflicts.Count)
            .Append("  fulfilled ").Append(report.Fulfilled.Count)
            .Append("  stale ").Append(report.Stale.Count).Append('\n');

        if (report.CorpusEmpty)
        {
            sb.Append("\nCORPUS EMPTY — nothing was validated against the project (FI-44: empty is not clean)\n");
        }

        if (report.Claims.Count == 0)
        {
            sb.Append("\n(no claims held)\n");
        }
        else
        {
            sb.Append('\n');
            var width = report.Claims.Max(c => ClaimKinds.ToToken(c.Kind).Length);
            foreach (var claim in report.Claims)
            {
                sb.Append("  ").Append(ClaimKinds.ToToken(claim.Kind).PadRight(width))
                    .Append("  ").Append(claim.Value)
                    .Append("  [").Append(claim.Agent).Append(']');
                if (claim.Purpose is not null)
                {
                    sb.Append("  ").Append(claim.Purpose);
                }

                sb.Append('\n');
            }
        }

        AppendSection(sb, "CONFLICTS (gating)", report.Conflicts);
        AppendSection(sb, "fulfilled — resource now exists; release if the work is yours", report.Fulfilled);

        if (report.Stale.Count > 0)
        {
            sb.Append("\nstale (older than the threshold; reported, never auto-released)\n");
            foreach (var claim in report.Stale)
            {
                sb.Append("  ").Append(ClaimKinds.ToToken(claim.Kind)).Append(' ').Append(claim.Value)
                    .Append("  [").Append(claim.Agent).Append("]  since ")
                    .Append(claim.CreatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")).Append('\n');
            }
        }

        foreach (var warning in report.Warnings)
        {
            sb.Append("\nwarning: ").Append(warning).Append('\n');
        }

        return sb.ToString();
    }

    public static string FormatReportJson(ClaimsReport report) => JsonSerializer.Serialize(new
    {
        project = report.ProjectDir,
        store = report.ClaimsDir,
        corpusEmpty = report.CorpusEmpty,
        claims = report.Claims.Select(Describe),
        conflicts = report.Conflicts.Select(c => new { claim = Describe(c.Claim), detail = c.Detail }),
        fulfilled = report.Fulfilled.Select(c => new { claim = Describe(c.Claim), detail = c.Detail }),
        stale = report.Stale.Select(Describe),
        warnings = report.Warnings,
        hasFindings = report.HasFindings,
    }, JsonOptions);

    private static void AppendSection(StringBuilder sb, string title, IReadOnlyList<ClaimConflict> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        sb.Append('\n').Append(title).Append('\n');
        foreach (var item in items)
        {
            sb.Append("  ").Append(ClaimKinds.ToToken(item.Claim.Kind)).Append(' ').Append(item.Claim.Value)
                .Append("  [").Append(item.Claim.Agent).Append("]  ").Append(item.Detail).Append('\n');
        }
    }

    private static object Describe(Claim claim) => new
    {
        kind = ClaimKinds.ToToken(claim.Kind),
        value = claim.Value,
        agent = claim.Agent,
        purpose = claim.Purpose,
        created = claim.CreatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        project = claim.Project,
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
