namespace DeviceGuard;

/// <summary>
/// One approved device in the test-rig allowlist.
///
/// Only entries whose <see cref="Kind"/> is exactly "test-rig" are honored by the guard —
/// an entry with any other kind (or none) is ignored. That is deliberate: a device cannot be
/// authorized just by adding its address; someone has to consciously assert "this is a test rig".
/// </summary>
public sealed record AllowlistEntry(
    string? Address,
    string? Label = null,
    string? Kind = null,
    string? Note = null,
    string? ApprovedBy = null,
    string? ApprovedDate = null)
{
    /// <summary>The one kind value that authorizes access. Anything else is refused.</summary>
    public const string TestRigKind = "test-rig";

    /// <summary>True only when this entry is explicitly marked as a test rig.</summary>
    public bool IsTestRig =>
        string.Equals(Kind?.Trim(), TestRigKind, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the entry actually names a device (guards against blank rows).</summary>
    public bool HasAddress => !string.IsNullOrWhiteSpace(Address);

    /// <summary>A human label for messages, falling back to the address.</summary>
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(Label) ? (Address ?? "<no address>") : Label;
}
