using System.Net;

namespace DeviceGuard;

/// <summary>
/// The write fence (ADR-0009). Decides whether a write may proceed against a target device.
///
/// This exists SEPARATELY from <see cref="DeviceAccessGuard"/> on purpose. The read fence's worst
/// case is reading a log it should not have; the write fence's worst case is a controller changing
/// state. Same allowlist, categorically different consequence — so write authorization is its own
/// type, with its own gates, and there is no code path by which a read-only entry produces a write
/// allow. A read-listed device that nobody marked write-eligible is refused here, always.
///
/// Like the read guard, this does NO I/O. It is a pure decision function over facts it is handed:
/// the allowlist, the identity someone read off the device, the scope the run declared, and whether
/// a restore point exists. Keeping it pure is what makes every refusal path unit-testable without a
/// PLC, and it means the fence can be reasoned about without reading network code.
///
/// The gates, in order — each one fails closed:
///   1. a usable target and area were given
///   2. the READ gate passes (listed, exact-matched, marked test-rig, allowlist actually loaded)
///   3. the entry is separately marked write-eligible
///   4. its outputs are asserted physically incapable of actuating, by a named person
///   5. the device's OWN IDENTITY matches what the entry declares
///   6. the area is inside the scope this run declared, and inside any cap the entry sets
///   7. a verified restore point exists for the target
/// </summary>
public sealed class DeviceWriteGuard
{
    private readonly AllowlistFile.Result _allowlist;
    private readonly DeviceAccessGuard _readGuard;
    private readonly IRestorePointStore _restorePoints;

    public DeviceWriteGuard(AllowlistFile.Result allowlist, IRestorePointStore? restorePoints = null)
    {
        _allowlist = allowlist;
        _readGuard = new DeviceAccessGuard(allowlist);
        // Default to "nothing is restorable" rather than to "no check" — forgetting to wire a store
        // must not silently authorize writes.
        _restorePoints = restorePoints ?? NoRestorePoints.Instance;
    }

    public static DeviceWriteGuard FromPath(string? resolvedPath, IRestorePointStore? restorePoints = null) =>
        new(AllowlistFile.Load(resolvedPath), restorePoints);

    /// <summary>
    /// May this run write <paramref name="area"/> on <paramref name="target"/>?
    /// </summary>
    /// <param name="target">Address used to reach the device. A routing hint, never an authorization.</param>
    /// <param name="area">What is being written — a named DB, area or region from the declared scope.</param>
    /// <param name="observedIdentity">
    /// What the device reported about itself on THIS connection. Re-read it after every reconnect: a
    /// reconnect can land on a different device at the same address.
    /// </param>
    /// <param name="runScope">What this run declared it would write. Null means nothing was declared.</param>
    public WriteDecision Check(
        string? target,
        string? area,
        DeviceIdentity? observedIdentity,
        WriteScope? runScope)
    {
        if (string.IsNullOrWhiteSpace(target))
            return WriteDecision.Refuse(WriteRefusal.InvalidTarget, "no target device given.");

        if (string.IsNullOrWhiteSpace(area))
            return WriteDecision.Refuse(WriteRefusal.InvalidArea,
                "no target area given; a write must name what it is writing to.");

        // 2. The read gate first. If a device may not even be read, it certainly may not be written.
        var read = _readGuard.Check(target);
        if (!read.Allowed)
            return WriteDecision.RefuseFromRead(read);

        var entry = read.MatchedEntry!;

        // 3. Write eligibility is a separate, deliberate grant.
        if (!entry.WriteEligible)
            return WriteDecision.Refuse(WriteRefusal.NotWriteEligible,
                $"'{target}' ({entry.DisplayLabel}) is an approved test rig for READING but is not marked " +
                "write-eligible. Being read-listed never implies write-listed.", entry);

        // 4. The physical precondition. Mandatory, and the only fence here that a software bug
        //    cannot cross — so it is also the one worth refusing hardest on.
        if (!entry.OutputsIsolated)
            return WriteDecision.Refuse(WriteRefusal.OutputsNotIsolated,
                $"'{target}' ({entry.DisplayLabel}) is not asserted physically isolated. A write-listed " +
                "device must have its outputs physically incapable of actuating — field wiring " +
                "disconnected, or interposing relays unpowered.", entry);

        if (string.IsNullOrWhiteSpace(entry.IsolationAssertedBy))
            return WriteDecision.Refuse(WriteRefusal.IsolationAssertionUnattributed,
                $"'{target}' ({entry.DisplayLabel}) claims physical isolation but names nobody as having " +
                "asserted it. An unattributed assertion is not an assertion.", entry);

        // 5. Identity. The address got us here; it does not tell us what answered.
        var identity = DeviceIdentity.Compare(entry, observedIdentity);
        if (!identity.Matched)
        {
            var reason = !identity.WasDeclared ? WriteRefusal.NoDeclaredIdentity
                       : !identity.WasObserved ? WriteRefusal.IdentityNotVerified
                       : WriteRefusal.IdentityMismatch;

            return WriteDecision.Refuse(reason,
                $"identity check failed for '{target}' ({entry.DisplayLabel}): {identity.Problem}", entry);
        }

        // 6. Scope: what this run said it would touch, capped by what the entry allows at most.
        var scope = runScope ?? WriteScope.Nothing;

        if (scope.IsEmpty)
            return WriteDecision.Refuse(WriteRefusal.NoScopeDeclared,
                "this run declared no write scope, so it may write nothing. Declare the areas the test " +
                "will touch before writing any of them.", entry);

        if (!scope.Includes(area))
            return WriteDecision.Refuse(WriteRefusal.AreaOutsideRunScope,
                $"'{area}' is outside this run's declared scope '{scope.Purpose}' " +
                $"(declared: {scope.Describe()}).", entry);

        if (entry.WritableAreas is { Count: > 0 } &&
            !entry.WritableAreas.Any(a => string.Equals(a?.Trim(), area.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return WriteDecision.Refuse(WriteRefusal.AreaOutsideDeviceCap,
                $"'{area}' is within the run's scope but outside the cap the allowlist sets for " +
                $"'{target}' (permitted: {string.Join(", ", entry.WritableAreas.Select(a => $"'{a}'"))}).", entry);
        }

        // 7. Reversibility, per 10-non-goals.md #4(b).
        if (!_restorePoints.HasVerifiedRestorePoint(Normalize(target)))
            return WriteDecision.Refuse(WriteRefusal.NoRestorePoint,
                $"no verified restore point exists for '{target}'. Capture and verify one before writing; " +
                "if it cannot be captured, the write does not happen. Note that a restore point covering " +
                "only blocks is not sufficient — a download can reinitialise DB actual values and retain " +
                "data with them.", entry);

        return WriteDecision.Allow(entry, area, scope);
    }

    /// <summary>Same canonicalization the read guard uses, so both agree on what "same target" means.</summary>
    private static string Normalize(string address)
    {
        var trimmed = address.Trim();
        return IPAddress.TryParse(trimmed, out var ip) ? ip.ToString() : trimmed.ToLowerInvariant();
    }
}
