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
    string? ApprovedDate = null,

    // ---- Write extension (ADR-0009). Every field here defaults to REFUSE. ----

    /// <summary>
    /// A SEPARATE grant from being listed. Read-listed never implies write-listed: someone has to
    /// consciously mark a device writable, on top of consciously marking it a test rig.
    /// </summary>
    bool WriteEligible = false,

    /// <summary>
    /// The mandatory physical precondition (ADR-0009 fence item 3): this device's outputs are
    /// physically incapable of actuating — field wiring disconnected, or interposing relays
    /// unpowered. It is a human assertion; nothing in software can verify it.
    /// </summary>
    bool OutputsIsolated = false,

    /// <summary>Who asserted the isolation. Required — an unattributed assertion is not one.</summary>
    string? IsolationAssertedBy = null,

    /// <summary>When it was asserted, for review. Free text, recorded not parsed.</summary>
    string? IsolationAssertedDate = null,

    // ---- Device identity (the fence the address cannot provide) ----
    //
    // An address does not identify a device. On this project's own network, 10.10.10.10 is the
    // standard PLC address across MULTIPLE deployment sites, and which physical controller answers
    // depends on which VPN tunnel happens to be up — a thing that changes mid-session. So the
    // allowlist keys on what the CPU says it is, and the address is only a routing hint.

    /// <summary>Expected CPU order number (e.g. the 6ES7... article number). Compared on connect.</summary>
    string? OrderNumber = null,

    /// <summary>Expected CPU serial number. The strongest identifier — unique per device.</summary>
    string? SerialNumber = null,

    /// <summary>
    /// Expected MAC, where the device is on a segment we can see. Useful as a secondary check, but
    /// it cannot be the primary one: across a routed tunnel the device's MAC is not visible at all.
    /// </summary>
    string? MacAddress = null,

    /// <summary>
    /// OPTIONAL outer bound on what a run may write on this device. Null means "this entry sets no
    /// cap" — the run's own declared scope stands alone. It does NOT mean "everything": a run must
    /// always declare what it will touch (see <see cref="WriteScope"/>).
    /// </summary>
    IReadOnlyList<string>? WritableAreas = null,

    // ---- Where a unit-level identifier can be READ FROM ----
    //
    // Declaring an expected identifier (above) and being able to READ one are different things, and
    // conflating them is how an entry ends up permanently unsatisfiable. The fields above say WHAT we
    // expect; the two below say HOW it can be obtained. An entry that declares a serial number but
    // configures no way to read one can never pass — see IdentitySourcePlan, which reports that as the
    // configuration error it is rather than letting it surface as a device mismatch.

    /// <summary>
    /// Where this device's program publishes a unique identifier, when the CPU itself will not report
    /// one. Null means no marker is available — which is a refusal for any entry declaring a serial
    /// number, not a licence to skip the check.
    /// </summary>
    MarkerLocation? Marker = null,

    /// <summary>
    /// Opt in to reading the serial from the CPU's own SZL 0x001C record.
    ///
    /// <para>Defaults to FALSE deliberately. It would be a better identifier than a marker DB — nothing
    /// in the user program can change it — but the CPU measured on 2026-08-11 REFUSED that request at
    /// every index, and enabling a source that always throws turns every connection into a failure.
    /// Turn it on to try it on a CPU that might answer; if it works, it may retire the marker DB.</para>
    /// </summary>
    bool UseCpuInfoSerial = false)
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
