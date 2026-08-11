using DeviceGuard;
using Harness.S7;

namespace Harness.RigWrite;

/// <summary>
/// Turns a <see cref="RigWriteRequest"/> into a <see cref="RigWritePlan"/> — the whole governed-write
/// sequence, decided as far as it can be without contacting anything.
///
/// <para><b>It performs no I/O against a device and takes no client.</b> Not a promise in a comment: an
/// <see cref="IS7Client"/> is not a parameter of any method here, so there is nothing to call. The only
/// I/O is reading the allowlist and the restore-point manifest, both files on this PC.</para>
///
/// <para><b>It does not reimplement the fence.</b> Gate order is decided by
/// <see cref="DeviceWriteGuard"/> and nowhere else. A planner that walked the gates itself would be a
/// second statement of the same rules, and the day the two disagree is the day a person is told the
/// wrong thing about why a write was refused — the failure mode the whole seven-gate ordering exists
/// to prevent. So the fence is asked once, and its verdict is reported verbatim.</para>
/// </summary>
public static class RigWritePlanner
{
    /// <param name="observedIdentity">
    /// What the device actually reported, when something has read it. Null means a dry run: the
    /// identity the allowlist DECLARES stands in, the gate is marked assumed, and every other gate is
    /// still decided for real. This is the parameter an armed build would fill in after connecting —
    /// the planner is the same either way, which is what keeps the dry run's answer honest about the
    /// armed one.
    /// </param>
    public static RigWritePlan Plan(
        RigWriteRequest request,
        AllowlistFile.Result allowlist,
        IRestorePointStore restorePoints,
        DeviceIdentity? observedIdentity = null,
        FileRestorePointStore? inspectableStore = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(allowlist);
        ArgumentNullException.ThrowIfNull(restorePoints);

        var steps = new List<PlanStep>();
        var blockers = new List<string>();
        var normalizedTarget = DeviceAddress.Normalize(request.Target);

        // ---- 1. The allowlist entry ---------------------------------------------------------
        var read = new DeviceAccessGuard(allowlist).Check(request.Target);
        var entry = read.MatchedEntry;

        steps.Add(new PlanStep(1, $"Resolve '{request.Target}' in the allowlist",
            read.Allowed ? StepStatus.Ready : StepStatus.Blocked,
            read.Allowed
                ? $"matched '{entry!.DisplayLabel}', kind '{entry.Kind}', approved by " +
                  $"{entry.ApprovedBy ?? "<nobody named>"} on {entry.ApprovedDate ?? "<no date>"}."
                : read.Message));

        if (!read.Allowed) blockers.Add(read.Message);

        // ---- 2. Identity sources ------------------------------------------------------------
        IdentitySourcePlan? identityPlan = entry is null ? null : IdentitySourcePlan.ForEntry(entry);

        steps.Add(new PlanStep(2, "Build the identity sources this entry needs",
            identityPlan is null ? StepStatus.NotReached
            : identityPlan.IsUsable ? StepStatus.Ready : StepStatus.Blocked,
            identityPlan is null
                ? "no allowlist entry, so there is nothing to configure sources for."
                : identityPlan.IsUsable
                    ? $"would read: {identityPlan.Describe()}."
                    : identityPlan.Problem!));

        if (identityPlan is { IsUsable: false }) blockers.Add(identityPlan.Problem!);

        // ---- 3 & 4. The device half ---------------------------------------------------------
        var deviceStepsReachable = read.Allowed && identityPlan is { IsUsable: true };
        var deviceStatus = deviceStepsReachable ? StepStatus.Deferred : StepStatus.NotReached;

        steps.Add(new PlanStep(3, $"Connect to {request.Target} (ISO-on-TCP, rack 0 slot 1)",
            deviceStatus,
            "NOT PERFORMED — this build opens no socket. A successful connect proves reachability and " +
            "nothing else: PUT/GET permission and block accessibility are not tested until the first " +
            "data read."));

        steps.Add(new PlanStep(4, "Read the device's identity, on this session",
            deviceStatus,
            identityPlan is { IsUsable: true }
                ? $"NOT PERFORMED — would read {identityPlan.Describe()} and compare against the entry."
                : "not reached."));

        // ---- 5. The restore point -----------------------------------------------------------
        var (restoreStatus, restoreDetail, restoreBlocker) =
            DescribeRestorePoint(request, normalizedTarget, restorePoints, inspectableStore, deviceStepsReachable);

        steps.Add(new PlanStep(5, $"Capture and verify a restore point covering {request.Region.Describe()}",
            restoreStatus, restoreDetail));

        if (restoreBlocker is not null) blockers.Add(restoreBlocker);

        // ---- 6. The fence -------------------------------------------------------------------
        var assumed = observedIdentity is null;
        var identityForGuard = observedIdentity ?? Assumed(entry);
        var scope = WriteScope.For(request.Purpose, request.Area);

        var decision = new DeviceWriteGuard(allowlist, restorePoints)
            .Check(request.Target, request.Area, identityForGuard, scope);

        steps.Add(new PlanStep(6, "Ask the write fence (DeviceWriteGuard, seven gates)",
            decision.Allowed ? StepStatus.Ready : StepStatus.Blocked,
            decision.Message +
            (assumed
                ? "\n     GATE 5 ASSUMED: the device was not read, so the identity the entry declares " +
                  "stood in for the one it would report. This is the only assumption in the plan."
                : "\n     Gate 5 decided against a real reading: " + identityForGuard?.Describe() + ".")));

        if (!decision.Allowed) blockers.Add($"fence gate {decision.Reason}: {decision.Message}");

        // ---- 7, 8, 9. Never reached in this build -------------------------------------------
        const string notArmed = "NOT RUN — " + Arming.WhyNot;

        steps.Add(new PlanStep(7, $"WRITE {request.Size} byte(s) to DB{request.DbNumber}.DBB{request.ByteOffset}",
            StepStatus.NotReached, notArmed));

        steps.Add(new PlanStep(8, "Verify by reading the same bytes back",
            StepStatus.NotReached,
            "NOT RUN — would re-read the region and compare BYTE FOR BYTE against what was sent, then " +
            "decode it and compare against the intended value. Two checks, because a byte comparison " +
            "catches a partial write and a decode catches a right-bytes-wrong-place write."));

        steps.Add(new PlanStep(9, "Restore the captured bytes and confirm by re-reading",
            StepStatus.NotReached,
            "NOT RUN — the run is designed to end where it started: FileRestorePointStore.Restore puts " +
            "the captured region back and confirms it by re-reading, so the first governed write is " +
            "also the first governed restore and the device is left byte-identical."));

        return new RigWritePlan(request, allowlist.ResolvedPath, entry, identityPlan, decision,
            assumed, steps, blockers);
    }

    /// <summary>The identity a dry run stands in for the real one: exactly what the entry declares.</summary>
    private static DeviceIdentity? Assumed(AllowlistEntry? entry) =>
        entry is null ? null : new DeviceIdentity(entry.OrderNumber, entry.SerialNumber, entry.MacAddress);

    /// <summary>
    /// The restore-point step, which asks a question the fence cannot.
    ///
    /// <para>The fence asks "does a verified restore point exist for this target?" and
    /// <see cref="FileRestorePointStore"/> answers it by comparing AREA NAMES, because an area is the
    /// vocabulary the fence is scoped on. So a two-byte capture labelled with the right area satisfies
    /// the fence for a thirty-four-byte write into that area, and the restore would put back two of
    /// the thirty-four bytes and report success. Here the write's exact extent IS known, so the
    /// stronger question gets asked — and a restore point that passes the fence while not covering the
    /// bytes is reported as a blocker in its own right.</para>
    /// </summary>
    private static (StepStatus Status, string Detail, string? Blocker) DescribeRestorePoint(
        RigWriteRequest request,
        string normalizedTarget,
        IRestorePointStore restorePoints,
        FileRestorePointStore? inspectable,
        bool reachable)
    {
        var exists = restorePoints.HasVerifiedRestorePoint(normalizedTarget);

        if (!exists)
        {
            var why = inspectable?.LastRefusal;
            return (reachable ? StepStatus.Deferred : StepStatus.NotReached,
                "NOT PERFORMED — no verified restore point is on disk for this target" +
                (why is null ? "." : $": {why}") +
                "\n     Capturing one is a DEVICE READ of the same region about to be written, which " +
                "this build does not do. FileRestorePointStore.Capture already implements the capture, " +
                "the read-back off disk and the hash verification; what it has never had is a device.",
                null);
        }

        if (inspectable is not null && inspectable.TryLoad(normalizedTarget, out var manifest) && manifest is not null)
        {
            if (!manifest.Covers(request.DbNumber, request.ByteOffset, request.Size))
            {
                var held = string.Join(", ", manifest.Regions.Select(r =>
                    $"DB{r.DbNumber}.DBB{r.StartByte}+{r.Size}"));

                return (StepStatus.Blocked,
                    $"a verified restore point exists AND SATISFIES THE FENCE, but it does not hold " +
                    $"DB{request.DbNumber}.DBB{request.ByteOffset}+{request.Size}. It holds: {held}. " +
                    "The fence compares area names, not byte ranges — so this one would pass gate 7 and " +
                    "then restore only part of what the write changed.",
                    $"the restore point on disk does not cover DB{request.DbNumber}." +
                    $"DBB{request.ByteOffset}+{request.Size}, though it passes the fence's area check.");
            }

            return (StepStatus.Ready,
                $"a verified restore point exists and holds DB{request.DbNumber}." +
                $"DBB{request.ByteOffset}+{request.Size} — captured {manifest.CapturedUtc} by " +
                $"{manifest.CapturedBy}, scope {manifest.Scope}.", null);
        }

        return (StepStatus.Ready,
            "a verified restore point exists for this target. Its BYTE coverage was not checked — the " +
            "store in use cannot be inspected, only asked yes/no.", null);
    }
}
