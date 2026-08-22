using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ladder.Converter.Leases;

/// <summary>
/// 🔴 <b>A REAL LOCK ON PORTAL AND THE RIG — the thing a 3,365-line hand-edited text file has been
/// standing in for.</b>
///
/// <para>That file's own README says what it is: <i>"a cooperative convention enforced by agents
/// reading and editing a shared file, not a real lock — it only works if every agent actually follows
/// it."</i> It has been raced: two claims both read the same generic holder name, indistinguishable,
/// and the collision was <i>"caught by chance when re-reading git log."</i></para>
///
/// <para><b>THE PRIMITIVE IS BORROWED FROM <c>ClaimStore</c> AND THE SEMANTICS DELIBERATELY ARE NOT.</b>
/// A claim is held until released and <i>"stale claims are reported, never auto-released"</i> — correct
/// for a block number, and fatal for a gate, because one crashed agent would wedge the rig forever. So a
/// lease EXPIRES. Keeping the two mechanisms apart is the point: a later tidy-up that gave claims a TTL
/// would start silently dropping reservations.</para>
///
/// <para><b>Write-temp-then-move, for a reason that was measured rather than reasoned.</b> Creating the
/// slot file directly looked equivalent and was not — the winner holds it open while writing, so a loser
/// that immediately read it hit a sharing violation and could not say who had beaten it. The move is the
/// atomic step, and it fails rather than clobbering.</para>
/// </summary>
public sealed class LeaseStore
{
    private const string Extension = ".lease";

    private readonly string _root;
    private readonly Func<DateTime> _nowUtc;
    private readonly Func<int, DateTime?> _processStartUtc;

    /// <param name="processStartUtc">
    /// 🔴 <b>The liveness oracle, and it returns a START TIME rather than a bool.</b> Given a pid, the
    /// start time of the process now running under it, or null if there is none.
    ///
    /// <para><b>PIDs are reused.</b> A reclaim that trusted a bare "is pid alive" would eventually evict
    /// a live holder because an unrelated process had inherited its number — rare, silent, and it would
    /// hand two agents the same gate. Comparing the start time as well makes the identity actually
    /// unique. Injectable so the race can be tested without spawning processes.</para>
    /// </param>
    public LeaseStore(string root, Func<DateTime>? nowUtc = null, Func<int, DateTime?>? processStartUtc = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _nowUtc = nowUtc ?? (() => DateTime.UtcNow);
        _processStartUtc = processStartUtc ?? LiveProcessStart;
    }

    public string Root => _root;

    /// <summary>The lease currently in the slot, or null. The file on disk is always the authority.</summary>
    public Lease? Read(LeaseResource resource, string target) =>
        ReadWithRetry(PathFor(resource, target));

    /// <summary>Every lease in the store, for <c>status</c>.</summary>
    public Lease[] All() =>
        !Directory.Exists(_root)
            ? Array.Empty<Lease>()
            : Directory.EnumerateFiles(_root, "*" + Extension)
                .Select(ReadWithRetry)
                .Where(l => l is not null)
                .Select(l => l!)
                .OrderBy(l => l.Resource).ThenBy(l => l.Target, StringComparer.Ordinal)
                .ToArray();

    /// <summary>
    /// Take the lease, or report who has it.
    ///
    /// <para><b>Reclaim is evidence-based and needs BOTH halves: the holder provably gone AND the lease
    /// expired.</b> A live holder past its TTL is REPORTED, never evicted — a long download is not a
    /// dead one, and evicting it would put two writers on the gate, which is the exact failure this
    /// exists to prevent.</para>
    ///
    /// <para><b>Why delete-then-acquire is safe here and would not be in general.</b> The race a
    /// compare-and-swap would protect against is a holder renewing between our read and our delete — and
    /// a holder we have just proven DEAD cannot renew. That leaves only other reclaimers, and they
    /// resolve on the atomic move below exactly as fresh acquirers do: one wins.</para>
    /// </summary>
    public LeaseOutcome TryAcquire(
        LeaseResource resource, string target, string holder, int processId, TimeSpan ttl, string? purpose)
    {
        if (resource == LeaseResource.Unstated)
            return new LeaseOutcome(LeaseResult.Invalid, null, "no resource was named. `Unstated` is deliberately not a usable value.");

        if (string.IsNullOrWhiteSpace(target))
            return new LeaseOutcome(LeaseResult.Invalid, null, "no target was named — a lease on 'a Portal' or 'a rig' locks nothing.");

        if (string.IsNullOrWhiteSpace(holder))
        {
            return new LeaseOutcome(LeaseResult.Invalid, null,
                "no holder was named. *** A GENERIC HOLDER DEFEATS THE WHOLE POINT: *** the recorded race on the text file this "
                + "replaces happened because two entries both read the same non-specific name and were indistinguishable.");
        }

        if (ttl <= TimeSpan.Zero)
            return new LeaseOutcome(LeaseResult.Invalid, null, $"a TTL of {ttl} would be expired before it was written.");

        Directory.CreateDirectory(_root);

        var now = _nowUtc();
        var start = _processStartUtc(processId)
            ?? throw new InvalidOperationException(
                $"process {processId} is not running, so it cannot be recorded as a lease holder. Pass the acquiring process's own pid.");

        var mine = new Lease(resource, target, holder.Trim(), processId, start, now, now + ttl, (purpose ?? string.Empty).Trim());
        var path = PathFor(resource, target);

        var first = TryPlace(mine, path);
        if (first.Result != LeaseResult.HeldByAnother)
            return first;

        var existing = first.Holder!;

        // ---- is the holder actually gone? ------------------------------------------------------
        var holderStart = _processStartUtc(existing.ProcessId);
        var holderAlive = holderStart is { } s && s == existing.ProcessStartUtc;

        if (holderAlive)
        {
            return new LeaseOutcome(LeaseResult.HeldByAnother, existing,
                existing.HasExpired(now)
                    ? $"{existing} — PAST ITS TTL BUT STILL ALIVE, so it is reported and NOT reclaimed. A long download is not a dead one."
                    : existing.ToString());
        }

        if (!existing.HasExpired(now))
        {
            return new LeaseOutcome(LeaseResult.HeldByAnother, existing,
                $"{existing} — its process is gone but the lease has not expired yet. Waiting for the TTL rather than reclaiming: "
                + "a holder that died mid-write may still have left the resource in a state its successor needs to know about, "
                + $"and the TTL is the declared window for noticing. Reclaimable from {existing.ExpiresUtc:u}.");
        }

        // Dead AND expired. Nobody can renew it, so the delete has no racing writer; a second
        // reclaimer meets us on the move below.
        TryDelete(path);

        var second = TryPlace(mine, path);

        return second.Result switch
        {
            LeaseResult.Acquired => new LeaseOutcome(LeaseResult.Reclaimed, mine,
                $"reclaimed from {existing.Holder} (pid {existing.ProcessId}), whose process is gone and whose lease expired "
                + $"{existing.ExpiresUtc:u}. *** SOMEBODY'S RUN DIED HOLDING THE GATE *** — that is worth knowing, which is why "
                + "this is not reported as an ordinary acquire."),
            _ => second,
        };
    }

    /// <summary>
    /// Release the lease. <b>Only the holder may</b> — releasing somebody else's is how two writers end
    /// up on one resource, and a mistyped holder name is the likeliest way it would happen.
    /// </summary>
    public LeaseOutcome Release(LeaseResource resource, string target, string holder)
    {
        var path = PathFor(resource, target);
        var existing = ReadWithRetry(path);

        if (existing is null)
            return new LeaseOutcome(LeaseResult.Invalid, null, $"no lease on {resource.ToString().ToLowerInvariant()}:{target} to release.");

        if (!string.Equals(existing.Holder, holder?.Trim(), StringComparison.Ordinal))
        {
            return new LeaseOutcome(LeaseResult.HeldByAnother, existing,
                $"'{holder}' does not hold this lease — {existing} does. Refusing to release another holder's lease.");
        }

        TryDelete(path);
        return new LeaseOutcome(LeaseResult.Acquired, existing, $"released {resource.ToString().ToLowerInvariant()}:{target}.");
    }

    // -------------------------------------------------------------------------------------------

    private LeaseOutcome TryPlace(Lease lease, string path)
    {
        var temp = Path.Combine(_root, $".{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temp, Serialize(lease), new UTF8Encoding(false));

            try
            {
                File.Move(temp, path, overwrite: false);
                return new LeaseOutcome(LeaseResult.Acquired, lease, lease.ToString());
            }
            catch (IOException)
            {
                var existing = ReadWithRetry(path);

                if (existing is null)
                {
                    // The slot exists and will not parse. Refusing to overwrite something this tool
                    // cannot identify — the same rule ClaimStore applies, and for the same reason.
                    return new LeaseOutcome(LeaseResult.Invalid, null,
                        $"lease slot '{path}' exists but could not be read. Refusing to overwrite a lease this tool cannot identify; inspect it by hand.");
                }

                return new LeaseOutcome(LeaseResult.HeldByAnother, existing, existing.ToString());
            }
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Another process got there. Not ours to force.
        }
    }

    private string PathFor(LeaseResource resource, string target) =>
        Path.Combine(_root, $"{resource.ToString().ToLowerInvariant()}-{Sanitize(target)}-{ShortHash(target)}{Extension}");

    private static string Sanitize(string value)
    {
        var clean = new string(value.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
        return clean.Length <= 40 ? clean : clean.Substring(0, 40);
    }

    private static string ShortHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).Substring(0, 8).ToLowerInvariant();

    private static Lease? ReadWithRetry(string path)
    {
        // A writer is only ever holding a TEMP file open, so a read here should not contend — but the
        // retry costs nothing and the alternative is an unreadable-slot refusal on a transient.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return File.Exists(path) ? Deserialize(File.ReadAllText(path)) : null;
            }
            catch (IOException)
            {
                System.Threading.Thread.Sleep(20);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        return null;
    }

    internal static string Serialize(Lease lease) =>
        string.Join("\n", new[]
        {
            $"resource {lease.Resource}",
            $"target {lease.Target}",
            $"holder {lease.Holder}",
            $"pid {lease.ProcessId.ToString(CultureInfo.InvariantCulture)}",
            $"processStart {lease.ProcessStartUtc.ToString("o", CultureInfo.InvariantCulture)}",
            $"acquired {lease.AcquiredUtc.ToString("o", CultureInfo.InvariantCulture)}",
            $"expires {lease.ExpiresUtc.ToString("o", CultureInfo.InvariantCulture)}",
            $"purpose {lease.Purpose}",
        }) + "\n";

    internal static Lease? Deserialize(string text)
    {
        var fields = text.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0)
            .Select(l => l.Split(new[] { ' ' }, 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1], StringComparer.Ordinal);

        if (!fields.TryGetValue("resource", out var resourceText) || !Enum.TryParse<LeaseResource>(resourceText, out var resource))
            return null;

        if (!fields.TryGetValue("pid", out var pidText) || !int.TryParse(pidText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
            return null;

        if (!TryTime(fields, "processStart", out var processStart)
            || !TryTime(fields, "acquired", out var acquired)
            || !TryTime(fields, "expires", out var expires))
        {
            return null;
        }

        return new Lease(
            resource,
            fields.TryGetValue("target", out var t) ? t : string.Empty,
            fields.TryGetValue("holder", out var h) ? h : string.Empty,
            pid, processStart, acquired, expires,
            fields.TryGetValue("purpose", out var p) ? p : string.Empty);

        static bool TryTime(System.Collections.Generic.Dictionary<string, string> f, string key, out DateTime value)
        {
            value = default;
            return f.TryGetValue(key, out var text)
                && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value);
        }
    }

    private static DateTime? LiveProcessStart(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return process.StartTime.ToUniversalTime();
        }
        catch (ArgumentException)
        {
            return null;   // no such process
        }
        catch (InvalidOperationException)
        {
            return null;   // exited between the lookup and the read
        }
    }
}
