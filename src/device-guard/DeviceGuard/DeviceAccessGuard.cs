using System.Net;

namespace DeviceGuard;

/// <summary>
/// The safeguard. Given a loaded allowlist, decides whether a target device may be accessed.
///
/// This is the fence ADR-0008 requires to exist BEFORE any live-device read is built. It does one
/// thing: refuse any target that is not an explicitly asserted test rig. It says nothing about
/// read-vs-write (that is the fetch client's job — it must expose read-only verbs only) and nothing
/// about safety content (hard rule 2 — the fetch client must refuse safety-tagged content too). The
/// guard sees an address, not device contents.
///
/// Matching is EXACT. There is intentionally no prefix or subnet matching: a range match is exactly
/// how a whole production subnet gets authorized by accident from a single entry.
/// </summary>
public sealed class DeviceAccessGuard
{
    private readonly AllowlistFile.Result _allowlist;

    public DeviceAccessGuard(AllowlistFile.Result allowlist) => _allowlist = allowlist;

    /// <summary>Convenience: load from a resolved path and construct in one step.</summary>
    public static DeviceAccessGuard FromPath(string? resolvedPath) =>
        new(AllowlistFile.Load(resolvedPath));

    public GuardDecision Check(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return GuardDecision.Refuse(GuardReason.InvalidTarget, "no target device given.");

        // Fail closed on any load problem — an absent or unreadable allowlist grants nothing.
        if (!_allowlist.Loaded)
            return GuardDecision.Refuse(_allowlist.FailureReason,
                _allowlist.Message ?? "allowlist unavailable.");

        var normalizedTarget = Normalize(target);

        var addressed = _allowlist.Entries.Where(e => e.HasAddress).ToList();

        // 1. An explicit test-rig match is the only thing that allows.
        var rigMatch = addressed.FirstOrDefault(e => e.IsTestRig && Normalize(e.Address!) == normalizedTarget);
        if (rigMatch is not null)
            return GuardDecision.Allow(rigMatch);

        // 2. Listed, but not as a test rig — a clearer refusal than "not listed".
        var nonRigMatch = addressed.FirstOrDefault(e => !e.IsTestRig && Normalize(e.Address!) == normalizedTarget);
        if (nonRigMatch is not null)
        {
            return GuardDecision.Refuse(GuardReason.EntryNotTestRig,
                $"'{target}' is on the allowlist but its kind is '{nonRigMatch.Kind}', not '{AllowlistEntry.TestRigKind}'.");
        }

        // 3. No honored entries at all — say so distinctly (empty is not clean).
        if (!addressed.Any(e => e.IsTestRig))
        {
            return GuardDecision.Refuse(GuardReason.AllowlistEmpty,
                "the allowlist contains no approved test-rig entries; refusing all targets.");
        }

        // 4. Rigs exist, this address is not among them.
        return GuardDecision.Refuse(GuardReason.TargetNotListed,
            $"'{target}' is not on the test-rig allowlist.");
    }

    /// <summary>
    /// Canonicalize for comparison: parse IPs so trivially different spellings compare equal, and
    /// lower-case hostnames. Whitespace is always trimmed. Never widens a match — same-or-tighter.
    /// </summary>
    private static string Normalize(string address)
    {
        var trimmed = address.Trim();
        return IPAddress.TryParse(trimmed, out var ip)
            ? ip.ToString()
            : trimmed.ToLowerInvariant();
    }
}
