namespace DeviceGuard;

/// <summary>Why a write was allowed or refused. Every value except <see cref="Allowed"/> is a refusal.</summary>
public enum WriteRefusal
{
    /// <summary>All gates passed: listed test rig, write-eligible, physically isolated,
    /// identity verified, area in scope, restore point captured.</summary>
    Allowed,

    /// <summary>No usable target address was given.</summary>
    InvalidTarget,

    /// <summary>No area was named. A write must say what it is writing to.</summary>
    InvalidArea,

    /// <summary>The read-level gate refused first — not listed, not a test rig, no allowlist, etc.
    /// The inner <see cref="WriteDecision.ReadDecision"/> carries the specific reason.</summary>
    ReadGateRefused,

    /// <summary>Listed and a test rig, but not marked write-eligible. Read-listed never implies
    /// write-listed.</summary>
    NotWriteEligible,

    /// <summary>Write-eligible, but its outputs are not asserted physically incapable of actuating.
    /// Mandatory — ADR-0009 fence item 3.</summary>
    OutputsNotIsolated,

    /// <summary>Isolation is claimed but nobody is named as having asserted it. An unattributed
    /// assertion is not an assertion.</summary>
    IsolationAssertionUnattributed,

    /// <summary>The entry declares no order number, serial or MAC, so there is nothing to verify the
    /// device against.</summary>
    NoDeclaredIdentity,

    /// <summary>No identity was read from the device before the write.</summary>
    IdentityNotVerified,

    /// <summary>The device is not the device the allowlist expected. The dangerous one.</summary>
    IdentityMismatch,

    /// <summary>The run declared no write scope, so it may write nothing.</summary>
    NoScopeDeclared,

    /// <summary>The area is outside what this run declared it would write.</summary>
    AreaOutsideRunScope,

    /// <summary>The area is within the run's scope but outside the cap the allowlist entry sets.</summary>
    AreaOutsideDeviceCap,

    /// <summary>No verified restore point exists for this target — `10-non-goals.md` #4(b).</summary>
    NoRestorePoint,
}

/// <summary>
/// The result of asking whether a write may proceed. Fail-closed by construction: every path that is
/// not an explicit all-gates-passed yields <see cref="Allowed"/> == false.
/// </summary>
public sealed record WriteDecision(
    bool Allowed,
    WriteRefusal Reason,
    string Message,
    AllowlistEntry? MatchedEntry = null,
    GuardDecision? ReadDecision = null)
{
    public static WriteDecision Allow(AllowlistEntry entry, string area, WriteScope scope) =>
        new(true, WriteRefusal.Allowed,
            $"ALLOWED: write to '{area}' on '{entry.Address}' ({entry.DisplayLabel}) — identity verified, " +
            $"outputs asserted isolated by {entry.IsolationAssertedBy}, within run scope '{scope.Purpose}', " +
            "restore point present.",
            entry);

    public static WriteDecision Refuse(WriteRefusal reason, string message, AllowlistEntry? entry = null) =>
        new(false, reason, "REFUSED: " + message, entry);

    public static WriteDecision RefuseFromRead(GuardDecision read) =>
        new(false, WriteRefusal.ReadGateRefused,
            "REFUSED: " + read.Message.Replace("REFUSED: ", string.Empty), read.MatchedEntry, read);
}
