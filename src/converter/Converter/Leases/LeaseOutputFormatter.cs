using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Ladder.Converter.Leases;

/// <summary>
/// The "one record, two renderers" pattern the claims, review, preflight and signal-sweep outputs all
/// follow.
/// </summary>
public static class LeaseOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <param name="storeDirectory">
    /// <b>Echoed on every act, for the reason <c>claim</c> echoes its own store.</b> Two agents passing
    /// two different roots fork the registry, and no process can detect that from inside — each store is
    /// well-formed and legitimately empty. This is the line that lets a reader notice.
    /// </param>
    public static string FormatOutcomeText(LeaseOutcome outcome, string storeDirectory, string? caveat = null)
    {
        var sb = new StringBuilder();
        sb.Append(Headline(outcome.Result)).Append("  ").Append(outcome.Detail).Append('\n');
        sb.Append("  leases  ").Append(storeDirectory).Append('\n');

        if (outcome.Holder is not null)
        {
            var holder = outcome.Holder;
            sb.Append("  lease   ").Append(holder.Resource.ToString().ToLowerInvariant()).Append(':').Append(holder.Target).Append('\n');
            sb.Append("  holder  ").Append(holder.Holder).Append(" (pid ").Append(holder.ProcessId).Append(")\n");
            sb.Append("  expires ").Append(holder.ExpiresUtc.ToString("u")).Append('\n');
            if (holder.Purpose.Length > 0)
            {
                sb.Append("  purpose ").Append(holder.Purpose).Append('\n');
            }
        }

        if (caveat is not null)
        {
            sb.Append(caveat).Append('\n');
        }

        return sb.ToString();
    }

    public static string FormatOutcomeJson(LeaseOutcome outcome, string storeDirectory, string? caveat = null) =>
        JsonSerializer.Serialize(new
        {
            result = outcome.Result.ToString(),
            held = outcome.Held,
            detail = outcome.Detail,
            leases = storeDirectory,
            caveat,
            lease = outcome.Holder is null ? null : Describe(outcome.Holder),
        }, JsonOptions);

    /// <summary>
    /// <b>The denominator is printed even when it is zero.</b> "No leases held" and "I looked in the
    /// wrong directory" produce identical listings otherwise, and the second is the likelier mistake.
    /// </summary>
    public static string FormatReportText(IReadOnlyList<Lease> leases, string storeDirectory, DateTime nowUtc)
    {
        var sb = new StringBuilder();
        sb.Append("leases  store=").Append(storeDirectory).Append('\n');
        sb.Append("held ").Append(leases.Count).Append('\n');

        foreach (var lease in leases)
        {
            var state = lease.HasExpired(nowUtc) ? "EXPIRED" : "held   ";
            sb.Append("  ").Append(state).Append(' ')
                .Append(lease.Resource.ToString().ToLowerInvariant()).Append(':').Append(lease.Target)
                .Append("  by ").Append(lease.Holder)
                .Append(" (pid ").Append(lease.ProcessId).Append(')')
                .Append("  expires ").Append(lease.ExpiresUtc.ToString("u"));
            if (lease.Purpose.Length > 0)
            {
                sb.Append("  — ").Append(lease.Purpose);
            }

            sb.Append('\n');
        }

        if (leases.Count == 0)
        {
            sb.Append("  (none) — an empty store and a mistyped --leases path look identical from here; the store line above is the one to check.\n");
        }

        // An expired lease is REPORTED and not swept: the holder may still be alive, and `status` is a
        // read. Only an acquire may reclaim, and only against evidence the holder is gone.
        return sb.ToString();
    }

    public static string FormatReportJson(IReadOnlyList<Lease> leases, string storeDirectory, DateTime nowUtc) =>
        JsonSerializer.Serialize(new
        {
            leases = storeDirectory,
            held = leases.Count,
            entries = Project(leases, nowUtc),
        }, JsonOptions);

    private static string Headline(LeaseResult result) => result switch
    {
        LeaseResult.Acquired => "ACQUIRED ",
        LeaseResult.Released => "RELEASED ",
        LeaseResult.Reclaimed => "RECLAIMED",
        LeaseResult.HeldByAnother => "REFUSED  ",
        LeaseResult.HeldOutsideTheTool => "REFUSED  ",
        LeaseResult.EvidenceInconclusive => "REFUSED  ",
        _ => "UNUSABLE ",
    };

    private static object[] Project(IReadOnlyList<Lease> leases, DateTime nowUtc)
    {
        var rows = new object[leases.Count];
        for (var i = 0; i < leases.Count; i++)
        {
            rows[i] = Describe(leases[i], leases[i].HasExpired(nowUtc));
        }

        return rows;
    }

    private static object Describe(Lease lease, bool? expired = null) => new
    {
        resource = lease.Resource.ToString().ToLowerInvariant(),
        target = lease.Target,
        holder = lease.Holder,
        pid = lease.ProcessId,
        processStartUtc = lease.ProcessStartUtc.ToString("o"),
        acquiredUtc = lease.AcquiredUtc.ToString("o"),
        expiresUtc = lease.ExpiresUtc.ToString("o"),
        purpose = lease.Purpose,
        expired,
    };
}
