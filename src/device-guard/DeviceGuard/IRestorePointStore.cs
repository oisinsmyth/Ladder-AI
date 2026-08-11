namespace DeviceGuard;

/// <summary>
/// Answers one question: has a verified restore point been captured for this target?
///
/// `10-non-goals.md` #4(b) requires that no AI writes to a device or a project without a verified
/// restore point captured first, and that if it cannot be captured, the write does not happen.
/// Enforcing that in the guard is what makes it structural rather than a discipline someone has to
/// remember — the same reasoning ADR-0007 records about gates you can bypass not being gates.
///
/// "Verified" is the store's word, not the guard's: the guard asks, it does not judge. An
/// implementation must only answer true when the restore point has been read back and confirmed,
/// never when it has merely been requested.
///
/// Note the hard case, recorded in #4(b) itself: a download can silently reinitialise DB actual
/// values and retentive data, so a restore point that captures only blocks is not a restore point.
/// </summary>
public interface IRestorePointStore
{
    /// <summary>
    /// True only if a verified, currently-valid restore point exists for this device.
    /// Implementations MUST fail closed — return false when unsure.
    /// </summary>
    bool HasVerifiedRestorePoint(string normalizedTarget);
}

/// <summary>
/// The default when nothing is wired up: nothing is ever restorable, so nothing is ever writable.
/// Present so that forgetting to supply a store cannot silently authorize writes.
/// </summary>
public sealed class NoRestorePoints : IRestorePointStore
{
    public static NoRestorePoints Instance { get; } = new();
    public bool HasVerifiedRestorePoint(string normalizedTarget) => false;
}
