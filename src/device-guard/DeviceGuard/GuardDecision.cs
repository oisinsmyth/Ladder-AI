namespace DeviceGuard;

/// <summary>Why the guard allowed or refused a target.</summary>
public enum GuardReason
{
    /// <summary>Target is on the allowlist as a test rig.</summary>
    Allowed,

    /// <summary>Neither --allowlist nor the environment variable was set — no allowlist to check against.</summary>
    NoAllowlistConfigured,

    /// <summary>An allowlist path was given but the file does not exist.</summary>
    AllowlistFileMissing,

    /// <summary>The allowlist file exists but could not be read or parsed.</summary>
    AllowlistUnreadable,

    /// <summary>The allowlist loaded but holds no honored (test-rig) entries.</summary>
    AllowlistEmpty,

    /// <summary>The target is not on the allowlist at all.</summary>
    TargetNotListed,

    /// <summary>The target is listed, but its entry is not marked as a test rig.</summary>
    EntryNotTestRig,

    /// <summary>No usable target address was given.</summary>
    InvalidTarget,
}

/// <summary>
/// The result of asking the guard whether a device may be accessed. Fail-closed by construction:
/// every path that is not an explicit test-rig match yields <see cref="Allowed"/> == false.
/// </summary>
public sealed record GuardDecision(
    bool Allowed,
    GuardReason Reason,
    string Message,
    AllowlistEntry? MatchedEntry = null)
{
    public static GuardDecision Allow(AllowlistEntry entry) =>
        new(true, GuardReason.Allowed,
            $"ALLOWED: '{entry.Address}' is an approved test rig ({entry.DisplayLabel}).", entry);

    public static GuardDecision Refuse(GuardReason reason, string message) =>
        new(false, reason, "REFUSED: " + message);
}
