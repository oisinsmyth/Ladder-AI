using DeviceGuard;
using Harness.RigWrite;
using Harness.S7;

namespace Harness.S7.Tests;

/// <summary>
/// Planning the first governed write, with no device in the room.
///
/// <para><b>Gate ORDER is asserted as hard as gate outcomes.</b> Every one of these refusals is a
/// correct "no", so a test that only checked <c>Allowed == false</c> would pass on the wrong
/// diagnosis — and the wrong diagnosis is what sends somebody to fix the wrong thing. The live entry
/// refuses at NotWriteEligible; if it ever reported NoRestorePoint instead, the reader would go
/// capture a restore point for a device that was never write-listed.</para>
/// </summary>
public class RigWritePlannerTests : IDisposable
{
    private const string Rig = "10.10.10.10";
    private const string Order = "6ES7 214-1AG40-0XB0";

    /// <summary>The marker value the rig's own program publishes — the entry's serial number.</summary>
    private const string MarkerValue = "RIG-BENCH-01";

    /// <summary>A CPU-label serial: declared by the live entry, and unreadable from this CPU.</summary>
    private const string UnreadableSerial = "S C-EXAMPLE00001";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ladder-rw-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // ---------------------------------------------------------------- fixtures

    /// <summary>
    /// The allowlist entry as it stands on this machine today: write-eligible FALSE, and declaring the
    /// serial printed on the CPU's label, which this CPU will not report.
    /// </summary>
    private static AllowlistEntry LiveShaped() => new(
        Rig,
        Label: "Bench rig",
        Kind: AllowlistEntry.TestRigKind,
        ApprovedBy: "Owner",
        ApprovedDate: "2026-08-11",
        WriteEligible: false,
        OutputsIsolated: true,
        IsolationAssertedBy: "Owner",
        IsolationAssertedDate: "2026-08-11",
        OrderNumber: Order,
        SerialNumber: UnreadableSerial);

    /// <summary>The proposed entry: a marker block, and the MARKER VALUE as the serial number.</summary>
    private static AllowlistEntry Proposed(bool writeEligible = false) => LiveShaped() with
    {
        WriteEligible = writeEligible,
        SerialNumber = MarkerValue,
        Marker = new MarkerLocation(
            MarkerDbLayout.DbNumber, MarkerDbLayout.RigMarkerOffset, MarkerDbLayout.StringDeclaredMax),
    };

    private static AllowlistFile.Result Loaded(params AllowlistEntry[] entries) =>
        AllowlistFile.Result.Ok(entries, "test-allowlist.json");

    private static RigWriteRequest Request() =>
        RigWriteRequest.MarkerSerialProbe(Rig, "RIGWRITE-PROBE");

    private sealed class HasRestorePoint : IRestorePointStore
    {
        public bool HasVerifiedRestorePoint(string normalizedTarget) => true;
    }

    private sealed class MemoryRegions : IRegionAccess
    {
        private readonly byte[] _block = new byte[MarkerDbLayout.TotalBytes];

        public byte[] Read(RestoreRegion region)
        {
            var slice = new byte[region.Size];
            Array.Copy(_block, region.StartByte, slice, 0, region.Size);
            return slice;
        }

        public void Write(RestoreRegion region, byte[] data) =>
            Array.Copy(data, 0, _block, region.StartByte, data.Length);
    }

    // ---------------------------------------------------------------- the live state

    [Fact]
    public void The_live_entry_refuses_at_the_write_eligibility_gate_and_not_a_later_one()
    {
        var plan = RigWritePlanner.Plan(Request(), Loaded(LiveShaped()), new HasRestorePoint());

        Assert.False(plan.OfflineClear);
        Assert.Equal(WriteRefusal.NotWriteEligible, plan.BlockingGate);

        // Explicitly NOT any of the gates that come after it. Each of these would send a reader to do
        // something real and useless.
        Assert.NotEqual(WriteRefusal.NoDeclaredIdentity, plan.BlockingGate);
        Assert.NotEqual(WriteRefusal.IdentityNotVerified, plan.BlockingGate);
        Assert.NotEqual(WriteRefusal.NoScopeDeclared, plan.BlockingGate);
        Assert.NotEqual(WriteRefusal.NoRestorePoint, plan.BlockingGate);
    }

    [Fact]
    public void The_live_entry_also_reports_that_its_declared_serial_can_never_be_read()
    {
        // Two independent faults, and the plan is a checklist rather than a fence: it reports both,
        // where the fence stops at its first. Fixing only the writeEligible flag would leave this one
        // to surface at connect time dressed as a device fault.
        var plan = RigWritePlanner.Plan(Request(), Loaded(LiveShaped()), new HasRestorePoint());

        Assert.False(plan.IdentityPlan!.IsUsable);
        Assert.Contains("no configured source can read one", plan.IdentityPlan.Problem);
        Assert.Equal(2, plan.Blockers.Count);
    }

    [Fact]
    public void The_proposed_entry_can_read_every_identifier_it_declares()
    {
        var plan = RigWritePlanner.Plan(Request(), Loaded(Proposed()), new HasRestorePoint());

        Assert.True(plan.IdentityPlan!.IsUsable);
        Assert.Contains("order code", plan.IdentityPlan.Describe());
        Assert.Contains($"DB{MarkerDbLayout.DbNumber}", plan.IdentityPlan.Describe());

        // Still refused, because writeEligible is still false. That is the one line an owner changes.
        Assert.Equal(WriteRefusal.NotWriteEligible, plan.BlockingGate);
    }

    [Fact]
    public void Arming_the_entry_clears_every_offline_gate()
    {
        var plan = RigWritePlanner.Plan(Request(), Loaded(Proposed(writeEligible: true)), new HasRestorePoint());

        Assert.True(plan.Decision!.Allowed);
        Assert.True(plan.OfflineClear);

        // But not "would proceed": this build cannot arm, and the identity gate was assumed.
        Assert.False(plan.WouldProceed);
        Assert.True(plan.IdentityAssumed);
    }

    // ---------------------------------------------------------------- identity

    [Fact]
    public void An_identity_mismatch_refuses()
    {
        // The scenario the whole mechanism exists for: the right address, the right model, a different
        // physical controller answering because a tunnel moved.
        var plan = RigWritePlanner.Plan(
            Request(), Loaded(Proposed(writeEligible: true)), new HasRestorePoint(),
            observedIdentity: new DeviceIdentity(Order, "SOME-OTHER-RIG"));

        Assert.Equal(WriteRefusal.IdentityMismatch, plan.BlockingGate);
        Assert.False(plan.IdentityAssumed);
    }

    [Fact]
    public void An_identity_that_could_not_be_read_refuses_rather_than_passing()
    {
        var plan = RigWritePlanner.Plan(
            Request(), Loaded(Proposed(writeEligible: true)), new HasRestorePoint(),
            observedIdentity: new DeviceIdentity(Order));   // order code only, no marker read

        Assert.Equal(WriteRefusal.IdentityMismatch, plan.BlockingGate);
    }

    [Fact]
    public void A_dry_run_marks_the_identity_gate_assumed_rather_than_verified()
    {
        var plan = RigWritePlanner.Plan(Request(), Loaded(Proposed(writeEligible: true)), new HasRestorePoint());

        Assert.True(plan.IdentityAssumed);
        Assert.Contains("GATE 5 ASSUMED", plan.Steps.Single(s => s.Number == 6).Detail);
    }

    // ---------------------------------------------------------------- restore point

    [Fact]
    public void A_missing_restore_point_refuses()
    {
        var plan = RigWritePlanner.Plan(
            Request(), Loaded(Proposed(writeEligible: true)), NoRestorePoints.Instance);

        Assert.Equal(WriteRefusal.NoRestorePoint, plan.BlockingGate);
    }

    [Fact]
    public void A_missing_restore_point_is_reported_as_an_action_needing_a_device_read()
    {
        var store = new FileRestorePointStore(_dir, new[] { MarkerDbLayout.AreaName });
        var plan = RigWritePlanner.Plan(Request(), Loaded(Proposed(writeEligible: true)), store, null, store);

        var step = plan.Steps.Single(s => s.Number == 5);
        Assert.Equal(StepStatus.Deferred, step.Status);
        Assert.Contains("DEVICE READ", step.Detail);
    }

    [Fact]
    public void A_restore_point_that_passes_the_fence_but_misses_the_bytes_is_a_blocker_of_its_own()
    {
        // The hole the fence cannot see: it compares AREA NAMES, so a two-byte capture labelled
        // 'DB_RigMarker' satisfies gate 7 for a thirty-four-byte write into that area. The restore
        // would then put back two of the thirty-four bytes and report success.
        var store = new FileRestorePointStore(_dir, new[] { MarkerDbLayout.AreaName });
        store.Capture(Rig,
            new[] { new RestoreRegion(MarkerDbLayout.AreaName, MarkerDbLayout.DbNumber, 0, 2) },
            "test", new MemoryRegions());

        Assert.True(store.HasVerifiedRestorePoint(Rig));   // the fence is satisfied

        var plan = RigWritePlanner.Plan(Request(), Loaded(Proposed(writeEligible: true)), store, null, store);

        Assert.True(plan.Decision!.Allowed);               // and the fence allows
        Assert.False(plan.OfflineClear);                   // but the plan does not
        Assert.Equal(StepStatus.Blocked, plan.Steps.Single(s => s.Number == 5).Status);
        Assert.Contains(plan.Blockers, b => b.Contains("does not cover"));
    }

    [Fact]
    public void A_restore_point_covering_the_written_bytes_is_ready()
    {
        var store = new FileRestorePointStore(_dir, new[] { MarkerDbLayout.AreaName });
        store.Capture(Rig, new[] { MarkerDbLayout.SerialNumberRegion }, "test", new MemoryRegions());

        var plan = RigWritePlanner.Plan(Request(), Loaded(Proposed(writeEligible: true)), store, null, store);

        Assert.Equal(StepStatus.Ready, plan.Steps.Single(s => s.Number == 5).Status);
        Assert.True(plan.OfflineClear);
    }

    [Fact]
    public void A_whole_block_capture_covers_the_member_written_inside_it()
    {
        var store = new FileRestorePointStore(_dir, new[] { MarkerDbLayout.AreaName });
        store.Capture(Rig, new[] { MarkerDbLayout.WholeBlock }, "test", new MemoryRegions());

        var plan = RigWritePlanner.Plan(Request(), Loaded(Proposed(writeEligible: true)), store, null, store);

        Assert.Equal(StepStatus.Ready, plan.Steps.Single(s => s.Number == 5).Status);
    }

    // ---------------------------------------------------------------- the allowlist itself

    [Fact]
    public void An_unlisted_target_is_refused_before_anything_else_is_considered()
    {
        var plan = RigWritePlanner.Plan(
            RigWriteRequest.MarkerSerialProbe("10.10.10.99", "RIGWRITE-PROBE"),
            Loaded(Proposed(writeEligible: true)), new HasRestorePoint());

        Assert.Equal(WriteRefusal.ReadGateRefused, plan.BlockingGate);
        Assert.Equal(StepStatus.Blocked, plan.Steps.Single(s => s.Number == 1).Status);
        Assert.Null(plan.IdentityPlan);
    }

    [Fact]
    public void An_absent_allowlist_grants_nothing()
    {
        var none = AllowlistFile.Load(null);
        var plan = RigWritePlanner.Plan(Request(), none, new HasRestorePoint());

        Assert.False(plan.OfflineClear);
        Assert.Equal(WriteRefusal.ReadGateRefused, plan.BlockingGate);
    }

    // ---------------------------------------------------------------- the sequence itself

    [Fact]
    public void The_write_and_its_verification_are_never_reached_in_this_build()
    {
        var plan = RigWritePlanner.Plan(Request(), Loaded(Proposed(writeEligible: true)), new HasRestorePoint());

        foreach (var number in new[] { 7, 8, 9 })
            Assert.Equal(StepStatus.NotReached, plan.Steps.Single(s => s.Number == number).Status);

        Assert.False(Arming.CompiledIn);
    }

    [Fact]
    public void The_device_steps_are_deferred_not_passed()
    {
        // A deferred step is an open question. Reporting it as Ready would let a clean-looking plan
        // stand in for a connection and an identity read that never happened.
        var plan = RigWritePlanner.Plan(Request(), Loaded(Proposed(writeEligible: true)), new HasRestorePoint());

        Assert.Equal(StepStatus.Deferred, plan.Steps.Single(s => s.Number == 3).Status);
        Assert.Equal(StepStatus.Deferred, plan.Steps.Single(s => s.Number == 4).Status);
    }

    [Fact]
    public void The_planned_write_is_the_reserved_member_and_nothing_larger()
    {
        var request = Request();

        Assert.Equal(MarkerDbLayout.DbNumber, request.DbNumber);
        Assert.Equal(MarkerDbLayout.SerialNumberOffset, request.ByteOffset);
        Assert.Equal(S7StringCodec.SizeOf(MarkerDbLayout.StringDeclaredMax), request.Size);
        Assert.Equal(MarkerDbLayout.SerialNumberRegion, request.Region);

        // Ends inside the block, and starts after the last member the identity check reads.
        Assert.True(request.ByteOffset + request.Size <= MarkerDbLayout.TotalBytes);
        Assert.True(request.ByteOffset >= MarkerDbLayout.OrderNumberOffset + S7StringCodec.SizeOf(32));
    }
}
