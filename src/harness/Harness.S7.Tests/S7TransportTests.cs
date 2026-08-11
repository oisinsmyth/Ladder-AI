using DeviceGuard;
using Harness.S7;

namespace Harness.S7.Tests;

/// <summary>
/// These tests are about the ways a transport lies.
///
/// A transport that reports a capability it does not have turns a vector the runner would have
/// refused into a green one. A transport that swallows the fence's refusal turns "the write was
/// forbidden" into "the write happened". A transport that identifies its device once and then
/// silently reconnects turns the identity gate into decoration. Each of those produces a run that
/// looks fine, which is why each of them has a test here.
/// </summary>
public class S7TransportTests
{
    private const string RigAddress = "192.0.2.11";
    private const string RigOrderCode = "6ES7 214-1AG40-0XB0";

    // ---------------------------------------------------------------- fixtures

    private static AllowlistEntry RigEntry(
        bool writeEligible = true,
        bool outputsIsolated = true,
        string? isolationAssertedBy = "an engineer",
        string? orderNumber = RigOrderCode,
        string? serialNumber = null,
        IReadOnlyList<string>? writableAreas = null) =>
        new(Address: RigAddress,
            Label: "Bench PLC",
            Kind: "test-rig",
            ApprovedBy: "an engineer",
            ApprovedDate: "2026-08-11",
            WriteEligible: writeEligible,
            OutputsIsolated: outputsIsolated,
            IsolationAssertedBy: isolationAssertedBy,
            OrderNumber: orderNumber,
            SerialNumber: serialNumber,
            WritableAreas: writableAreas);

    private static AllowlistFile.Result Allowlist(params AllowlistEntry[] entries) =>
        AllowlistFile.Result.Ok(entries, "(in-memory)");

    /// <summary>DB10 is the interface the harness writes; DB11 exists to be out of scope.</summary>
    private static S7TagMap Tags() => new(new[]
    {
        new S7Tag("Cmd_Start", "DB_Interface", 10, 0, S7DataType.Bool, 0),
        new S7Tag("Cmd_Stop", "DB_Interface", 10, 0, S7DataType.Bool, 1),
        new S7Tag("Setpoint", "DB_Interface", 10, 2, S7DataType.Real),
        new S7Tag("StateID", "DB_Interface", 10, 6, S7DataType.Int),
        new S7Tag("ScanCount", "DB_Interface", 10, 8, S7DataType.UDInt),
        new S7Tag("Other_Bit", "DB_Elsewhere", 11, 0, S7DataType.Bool, 0),
    });

    private static FakeS7Client Device() =>
        new FakeS7Client().WithBlock(10, new byte[16]).WithBlock(11, new byte[4]);

    private static IDeviceIdentitySource[] OrderCodeOnly() => new IDeviceIdentitySource[] { new OrderCodeIdentitySource() };

    private static WriteScope Scope(params string[] areas) =>
        WriteScope.For("conformance run", areas.Length == 0 ? new[] { "DB_Interface" } : areas);

    /// <summary>Gate 7 satisfied, so the other six gates are what the test is actually about.</summary>
    private sealed class AlwaysRestorable : IRestorePointStore
    {
        public bool HasVerifiedRestorePoint(string normalizedTarget) => true;
    }

    private static S7Transport Writable(
        FakeS7Client device,
        AllowlistFile.Result? allowlist = null,
        WriteScope? scope = null,
        IRestorePointStore? restorePoints = null,
        S7ConnectionSettings? settings = null) =>
        S7Transport.Writable(
            device,
            settings ?? new S7ConnectionSettings(RigAddress),
            Tags(),
            allowlist ?? Allowlist(RigEntry()),
            restorePoints ?? new AlwaysRestorable(),
            scope ?? Scope(),
            OrderCodeOnly());

    private static S7Transport ReadOnly(FakeS7Client device, AllowlistFile.Result? allowlist = null) =>
        S7Transport.ReadOnly(
            device,
            new S7ConnectionSettings(RigAddress),
            Tags(),
            allowlist ?? Allowlist(RigEntry(writeEligible: false)),
            OrderCodeOnly());

    // ---------------------------------------------------------------- capabilities

    [Fact]
    public void A_writable_transport_reports_write_and_nothing_else()
    {
        var t = Writable(Device());

        Assert.Equal(TransportCapabilities.Write, t.Capabilities);
    }

    [Fact]
    public void Scan_counter_event_stamps_and_latching_are_all_absent_today()
    {
        // The three instrumentation capabilities are properties of the PROGRAM. No program under test
        // provides any of them as of 2026-08-11, and claiming one would convert a vector the runner
        // refuses to evaluate into a vector it evaluates against data that cannot carry the answer.
        var t = Writable(Device());

        Assert.False(t.Capabilities.HasFlag(TransportCapabilities.ScanCounter));
        Assert.False(t.Capabilities.HasFlag(TransportCapabilities.EventScanStamps));
        Assert.False(t.Capabilities.HasFlag(TransportCapabilities.LatchedTransients));
    }

    [Fact]
    public void A_read_only_transport_reports_no_capabilities_at_all()
    {
        Assert.Equal(TransportCapabilities.None, ReadOnly(Device()).Capabilities);
    }

    [Fact]
    public void The_scan_counter_capability_appears_only_when_a_scan_counter_tag_exists()
    {
        var t = Writable(Device(), settings: new S7ConnectionSettings(RigAddress, ScanCounterTag: "ScanCount"));

        Assert.True(t.Capabilities.HasFlag(TransportCapabilities.ScanCounter));
    }

    [Fact]
    public void Naming_a_scan_counter_tag_that_is_not_in_the_map_is_refused_at_construction()
    {
        var ex = Assert.Throws<S7ConfigurationException>(() =>
            Writable(Device(), settings: new S7ConnectionSettings(RigAddress, ScanCounterTag: "NoSuchTag")));

        Assert.Contains("not in the tag map", ex.Message);
    }

    [Fact]
    public void Reading_the_scan_counter_without_one_throws_rather_than_guessing()
    {
        var t = Writable(Device());

        var ex = Assert.Throws<NotSupportedException>(() => t.ReadScanCounter());
        Assert.Contains("wall-clock delay is not a scan count", ex.Message);
    }

    [Fact]
    public void The_scan_counter_is_monotonic_across_a_wrap()
    {
        var device = Device();
        var t = Writable(device, settings: new S7ConnectionSettings(RigAddress, ScanCounterTag: "ScanCount"));

        // 0xFFFFFFFE, then wrapped to 3.
        WriteUDInt(device.Block(10), 8, 0xFFFFFFFE);
        var before = t.ReadScanCounter();

        WriteUDInt(device.Block(10), 8, 3);
        var after = t.ReadScanCounter();

        Assert.Equal(0xFFFFFFFEL, before);

        // FFFFFFFE -> FFFFFFFF -> 0 -> 1 -> 2 -> 3 is five scans, and that is what the runner's
        // `now - start` must yield. Without wrap absorption it would be about -4.3 billion, and the
        // wait would spin to its poll limit and error — not a false pass, but a baffling one.
        Assert.Equal(5, after - before);
    }

    // ---------------------------------------------------------------- identity

    [Fact]
    public void Identity_is_read_on_connect()
    {
        var device = Device();
        var t = Writable(device);

        t.Connect();

        Assert.Equal(1, t.IdentityReadCount);
        Assert.Equal(RigOrderCode, t.ObservedIdentity!.OrderNumber);
    }

    [Fact]
    public void A_device_whose_identity_does_not_match_the_allowlist_is_refused()
    {
        var device = Device();
        device.OrderCode = "6ES7 511-1AK02-0AB0";   // a different CPU answered

        var ex = Assert.Throws<S7WriteRefusedException>(() => Writable(device).Connect());

        Assert.Equal(WriteRefusal.IdentityMismatch, ex.Reason);
        Assert.Contains("order number mismatch", ex.Message);
    }

    [Fact]
    public void An_identifier_the_device_did_not_report_is_a_mismatch_not_a_pass()
    {
        // The entry declares a serial; only the order code is readable, so the serial comes back
        // absent. DeviceIdentity.Compare treats that as a mismatch, and it must reach the caller.
        var t = Writable(Device(), Allowlist(RigEntry(serialNumber: "S C-K1U399102021")));

        var ex = Assert.Throws<S7WriteRefusedException>(() => t.Connect());

        Assert.Equal(WriteRefusal.IdentityMismatch, ex.Reason);
        Assert.Contains("did not report one", ex.Message);
    }

    [Fact]
    public void A_reconnect_re_reads_identity()
    {
        var device = Device();
        var t = Writable(device);

        t.Connect();
        Assert.Equal(1, t.IdentityReadCount);

        device.DropConnection();
        t.Read("StateID");              // triggers the reconnect

        Assert.Equal(2, device.ConnectCount);
        Assert.Equal(2, t.IdentityReadCount);
        Assert.Equal(2, device.OrderCodeReadCount);
    }

    [Fact]
    public void A_reconnect_that_lands_on_a_different_device_refuses_the_write()
    {
        // The reason the whole mechanism exists: a remote tunnel injecting the same /24 at a lower
        // metric silently changes which box answers, mid-session, with no signal in the routing table.
        // A transport that reconnected and carried on would write plant commands into a stranger.
        var device = Device();
        var t = Writable(device);

        t.Connect();
        t.Write("DB_Interface", "Cmd_Start", "true");
        Assert.Single(device.BitWrites);

        device.DropConnection();
        device.OrderCode = "6ES7 511-1AK02-0AB0";

        var ex = Assert.Throws<S7WriteRefusedException>(() => t.Write("DB_Interface", "Cmd_Stop", "true"));

        Assert.Equal(WriteRefusal.IdentityMismatch, ex.Reason);
        Assert.Single(device.BitWrites);   // nothing further reached the device
    }

    [Fact]
    public void A_read_only_session_still_honours_a_declared_identity()
    {
        var device = Device();
        device.OrderCode = "6ES7 511-1AK02-0AB0";

        var ex = Assert.Throws<S7TransportException>(() => ReadOnly(device).Connect());

        Assert.Contains("is not the one the allowlist declares", ex.Message);
    }

    [Fact]
    public void A_read_only_entry_declaring_no_identity_may_still_be_read()
    {
        // ADR-0008's read fence authorizes on the allowlist, not on identity. Only writes need more.
        var device = Device();
        WriteInt(device.Block(10), 6, 110);

        var t = ReadOnly(device, Allowlist(RigEntry(writeEligible: false, orderNumber: null)));

        Assert.Equal("110", t.Read("StateID"));
    }

    [Fact]
    public void A_failed_identity_read_drops_the_session_rather_than_using_it()
    {
        var device = Device();
        device.OrderCodeFailureCode = 5;

        var t = Writable(device);

        Assert.Throws<S7TransportException>(() => t.Connect());
        Assert.False(device.Connected);
        Assert.Null(t.ObservedIdentity);
    }

    [Fact]
    public void A_transport_with_no_identity_sources_is_refused_at_construction()
    {
        var ex = Assert.Throws<S7ConfigurationException>(() => S7Transport.ReadOnly(
            Device(), new S7ConnectionSettings(RigAddress), Tags(), Allowlist(RigEntry()),
            Array.Empty<IDeviceIdentitySource>()));

        Assert.Contains("no identity sources", ex.Message);
    }

    // ---------------------------------------------------------------- the fence

    [Fact]
    public void A_target_that_is_not_on_the_allowlist_never_reaches_the_wire()
    {
        var device = Device();
        var t = S7Transport.ReadOnly(device, new S7ConnectionSettings("198.51.100.7"), Tags(),
            Allowlist(RigEntry()), OrderCodeOnly());

        var ex = Assert.Throws<S7AccessRefusedException>(() => t.Connect());

        Assert.False(ex.Decision.Allowed);
        Assert.Equal(0, device.ConnectCount);
    }

    [Fact]
    public void A_rig_that_is_read_listed_but_not_write_eligible_is_refused_with_that_reason()
    {
        var ex = Assert.Throws<S7WriteRefusedException>(() =>
            Writable(Device(), Allowlist(RigEntry(writeEligible: false))).Connect());

        Assert.Equal(WriteRefusal.NotWriteEligible, ex.Reason);
        Assert.Contains("Being read-listed never implies write-listed", ex.Message);
    }

    [Fact]
    public void An_unisolated_rig_is_refused_with_that_reason()
    {
        var ex = Assert.Throws<S7WriteRefusedException>(() =>
            Writable(Device(), Allowlist(RigEntry(outputsIsolated: false))).Connect());

        Assert.Equal(WriteRefusal.OutputsNotIsolated, ex.Reason);
    }

    [Fact]
    public void Without_a_restore_point_no_write_is_authorized()
    {
        var ex = Assert.Throws<S7WriteRefusedException>(() =>
            Writable(Device(), restorePoints: NoRestorePoints.Instance).Connect());

        Assert.Equal(WriteRefusal.NoRestorePoint, ex.Reason);
        Assert.Contains("reinitialise DB actual values", ex.Message);
    }

    [Fact]
    public void The_guards_refusal_is_surfaced_verbatim_rather_than_swallowed()
    {
        // The fence's own words reach the caller. A transport that caught this and returned quietly
        // would make "the write was forbidden" indistinguishable from "the write happened".
        var device = Device();
        var t = Writable(device);
        t.Connect();

        var ex = Assert.Throws<S7WriteRefusedException>(() => t.Write("DB_Elsewhere", "Other_Bit", "true"));

        Assert.Equal(WriteRefusal.AreaOutsideRunScope, ex.Reason);
        Assert.StartsWith("REFUSED:", ex.Message);
        Assert.Contains("'DB_Elsewhere' is outside this run's declared scope", ex.Message);
        Assert.Empty(device.BitWrites);
    }

    [Fact]
    public void An_area_outside_the_allowlists_own_cap_is_refused()
    {
        var t = Writable(
            Device(),
            Allowlist(RigEntry(writableAreas: new[] { "DB_SomethingElse" })),
            Scope("DB_Interface"));

        var ex = Assert.Throws<S7WriteRefusedException>(() => t.Connect());

        Assert.Equal(WriteRefusal.AreaOutsideDeviceCap, ex.Reason);
    }

    [Fact]
    public void A_writable_transport_with_no_declared_scope_is_refused_at_construction()
    {
        var ex = Assert.Throws<S7ConfigurationException>(() =>
            Writable(Device(), scope: WriteScope.Nothing));

        Assert.Contains("no declared write scope", ex.Message);
    }

    [Fact]
    public void A_read_only_transport_refuses_to_write_at_all()
    {
        var t = ReadOnly(Device());

        var ex = Assert.Throws<NotSupportedException>(() => t.Write("DB_Interface", "Cmd_Start", "true"));
        Assert.Contains("read-only", ex.Message);
    }

    [Fact]
    public void Writing_a_tag_under_an_area_it_does_not_belong_to_is_refused()
    {
        // The fence authorizes an AREA. If the caller names one area and the bytes land in another,
        // the fence answered a question about the wrong region of the device.
        var device = Device();
        var t = Writable(device, scope: Scope("DB_Interface", "DB_Elsewhere"));
        t.Connect();

        var ex = Assert.Throws<S7TransportException>(() => t.Write("DB_Interface", "Other_Bit", "true"));

        Assert.Contains("the tag map puts it in 'DB_Elsewhere'", ex.Message);
        Assert.Empty(device.BitWrites);
    }

    [Fact]
    public void The_fence_is_consulted_on_every_write_not_only_at_connect()
    {
        // Connect preflights the declared scope; that must not be mistaken for a session-wide grant.
        var device = Device();
        var t = Writable(device);
        t.Connect();

        t.Write("DB_Interface", "Cmd_Start", "true");
        Assert.Throws<S7WriteRefusedException>(() => t.Write("DB_Elsewhere", "Other_Bit", "true"));
    }

    // ---------------------------------------------------------------- reads and writes

    [Fact]
    public void Reads_decode_big_endian_values()
    {
        var device = Device();
        var block = device.Block(10);
        block[0] = 0b0000_0010;                 // Cmd_Stop set, Cmd_Start clear
        WriteReal(block, 2, 512.25f);
        WriteInt(block, 6, -3);

        var t = Writable(device);

        Assert.Equal("False", t.Read("Cmd_Start"));
        Assert.Equal("True", t.Read("Cmd_Stop"));
        Assert.Equal("512.25", t.Read("Setpoint"));
        Assert.Equal("-3", t.Read("StateID"));
    }

    [Fact]
    public void A_bool_write_uses_a_bit_write_and_leaves_its_neighbours_alone()
    {
        // Read-modify-write of the enclosing byte would silently revert any other bit the PLC changed
        // in between — an intermittent, timing-dependent fault inside a conformance suite.
        var device = Device();
        device.Block(10)[0] = 0b0000_0010;

        var t = Writable(device);
        t.Write("DB_Interface", "Cmd_Start", "true");

        Assert.Equal(new[] { "DB10.DBX0.0 = True" }, device.BitWrites);
        Assert.Empty(device.BlockWrites);
        Assert.Equal((byte)0b0000_0011, device.Block(10)[0]);
    }

    [Fact]
    public void A_real_write_lands_big_endian()
    {
        var device = Device();
        var t = Writable(device);

        t.Write("DB_Interface", "Setpoint", "512.25");

        Assert.Equal("512.25", t.Read("Setpoint"));
        Assert.Equal((byte)0x44, device.Block(10)[2]);   // big-endian: high byte first
    }

    [Fact]
    public void An_unknown_tag_is_a_hard_error_rather_than_an_empty_read()
    {
        // FakeTransport returns "" for an unknown tag, which is right for authoring. Against a device
        // the same leniency would let a typo'd tag compare "" against "" and pass.
        var t = Writable(Device());

        var ex = Assert.Throws<S7ConfigurationException>(() => t.Read("Setpiont"));
        Assert.Contains("compare equal to an empty expectation and pass", ex.Message);
    }

    [Fact]
    public void A_protocol_level_read_failure_is_reported_with_the_likely_cause()
    {
        var device = Device();
        device.ReadFailureCode = 10;
        var t = Writable(device);
        t.Connect();

        var ex = Assert.Throws<S7TransportException>(() => t.Read("StateID"));

        Assert.Contains("Optimized block access", ex.Message);
        Assert.Contains("PUT/GET", ex.Message);
    }

    // ---------------------------------------------------------------- through the runner

    [Fact]
    public void A_refused_write_reaches_the_run_report_as_errored_carrying_the_reason()
    {
        // The end-to-end property the whole design rests on: the runner knows nothing about the
        // fence, so a refusal has to survive as an exception and land in the report. An Errored
        // vector is explicitly not a pass and spoils the run's green.
        var t = Writable(Device());

        var vector = new TestVector("V-01", "stimulate an out-of-scope area", "Rev 1 §1",
            Steps: new[]
            {
                new VectorStep(Stimulus: new[] { new TagWrite("DB_Elsewhere", "Other_Bit", "true") }),
            });

        var report = new VectorRunner(t, TargetEnvironment.Both).RunAll(new[] { vector });

        Assert.False(report.IsGreen);
        Assert.Equal(1, report.Errored);
        Assert.Contains("outside this run's declared scope", report.Results[0].Message);
    }

    [Fact]
    public void A_read_only_transport_makes_a_stimulus_vector_unobservable_not_passing()
    {
        var t = ReadOnly(Device());

        var vector = new TestVector("V-02", "needs to write", "Rev 1 §2",
            Steps: new[]
            {
                new VectorStep(Stimulus: new[] { new TagWrite("DB_Interface", "Cmd_Start", "true") }),
            });

        var report = new VectorRunner(t, TargetEnvironment.Both).RunAll(new[] { vector });

        Assert.Equal(VectorOutcome.NotObservable, report.Results[0].Outcome);
        Assert.False(report.IsGreen);
    }

    // ---------------------------------------------------------------- helpers

    private static void WriteInt(byte[] block, int offset, short value)
    {
        block[offset] = (byte)(value >> 8);
        block[offset + 1] = (byte)value;
    }

    private static void WriteUDInt(byte[] block, int offset, uint value)
    {
        block[offset] = (byte)(value >> 24);
        block[offset + 1] = (byte)(value >> 16);
        block[offset + 2] = (byte)(value >> 8);
        block[offset + 3] = (byte)value;
    }

    private static void WriteReal(byte[] block, int offset, float value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        Array.Copy(bytes, 0, block, offset, 4);
    }
}
