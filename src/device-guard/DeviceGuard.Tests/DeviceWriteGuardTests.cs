namespace DeviceGuard.Tests;

/// <summary>
/// The write fence fails closed on seven independent gates. These tests pin every refusal path, and
/// allow ONLY when all seven pass together. The ordering matters as much as the outcomes: a device
/// that fails an earlier gate must never be reported as failing a later one, or a diagnosis sends
/// someone to fix the wrong thing.
/// </summary>
public class DeviceWriteGuardTests
{
    private const string Rig = "10.10.10.10";
    private const string Order = "6ES7214-1AG40-0XB0";
    private const string Serial = "S C-J1V284512023";

    private static AllowlistFile.Result Loaded(params AllowlistEntry[] entries) =>
        AllowlistFile.Result.Ok(entries, "test-allowlist.json");

    /// <summary>An entry with every gate satisfied — the baseline each test then breaks one way.</summary>
    private static AllowlistEntry FullyEligible(
        string address = Rig,
        bool writeEligible = true,
        bool isolated = true,
        string? isolationBy = "Test Engineer",
        string? order = Order,
        string? serial = Serial,
        IReadOnlyList<string>? cap = null) =>
        new(address,
            Label: "Bench rig",
            Kind: AllowlistEntry.TestRigKind,
            WriteEligible: writeEligible,
            OutputsIsolated: isolated,
            IsolationAssertedBy: isolationBy,
            IsolationAssertedDate: "2026-08-11",
            OrderNumber: order,
            SerialNumber: serial,
            WritableAreas: cap);

    private static DeviceIdentity Observed(string? order = Order, string? serial = Serial, string? mac = null) =>
        new(order, serial, mac);

    private static WriteScope Scope(params string[] areas) =>
        WriteScope.For("conformance run", areas.Length == 0 ? new[] { "DB_GaugeInterface" } : areas);

    private sealed class HasRestorePoint : IRestorePointStore
    {
        public bool HasVerifiedRestorePoint(string normalizedTarget) => true;
    }

    private static DeviceWriteGuard Guard(AllowlistEntry entry, IRestorePointStore? store = null) =>
        new(Loaded(entry), store ?? new HasRestorePoint());

    // ---------------------------------------------------------------- the allow path

    [Fact]
    public void Allows_only_when_every_gate_passes()
    {
        var d = Guard(FullyEligible())
            .Check(Rig, "DB_GaugeInterface", Observed(), Scope("DB_GaugeInterface"));

        Assert.True(d.Allowed);
        Assert.Equal(WriteRefusal.Allowed, d.Reason);
        Assert.Equal(Rig, d.MatchedEntry?.Address);
    }

    // ---------------------------------------------------------------- basic arguments

    [Fact]
    public void Refuses_when_no_target_given()
    {
        var d = Guard(FullyEligible()).Check(" ", "DB_GaugeInterface", Observed(), Scope());
        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.InvalidTarget, d.Reason);
    }

    [Fact]
    public void Refuses_when_no_area_given()
    {
        var d = Guard(FullyEligible()).Check(Rig, "  ", Observed(), Scope());
        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.InvalidArea, d.Reason);
    }

    // ---------------------------------------------------------------- gate 2: the read fence

    [Fact]
    public void Refuses_when_the_read_gate_refuses_and_preserves_its_reason()
    {
        // Not a test rig at all — the read guard must stop it before any write gate is consulted.
        var notARig = new AllowlistEntry(Rig, "Production PLC", Kind: "production", WriteEligible: true,
            OutputsIsolated: true, IsolationAssertedBy: "someone", OrderNumber: Order, SerialNumber: Serial);

        var d = Guard(notARig).Check(Rig, "DB_GaugeInterface", Observed(), Scope());

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.ReadGateRefused, d.Reason);
        Assert.Equal(GuardReason.EntryNotTestRig, d.ReadDecision?.Reason);
    }

    [Fact]
    public void Refuses_an_unlisted_target_even_with_perfect_identity()
    {
        var d = Guard(FullyEligible())
            .Check("10.10.10.99", "DB_GaugeInterface", Observed(), Scope());

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.ReadGateRefused, d.Reason);
    }

    // ---------------------------------------------------------------- gate 3: separate write grant

    [Fact]
    public void Read_listed_never_implies_write_listed()
    {
        var d = Guard(FullyEligible(writeEligible: false))
            .Check(Rig, "DB_GaugeInterface", Observed(), Scope());

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.NotWriteEligible, d.Reason);
    }

    // ---------------------------------------------------------------- gate 4: physical isolation

    [Fact]
    public void Refuses_when_outputs_are_not_asserted_isolated()
    {
        var d = Guard(FullyEligible(isolated: false))
            .Check(Rig, "DB_GaugeInterface", Observed(), Scope());

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.OutputsNotIsolated, d.Reason);
    }

    [Fact]
    public void Refuses_an_unattributed_isolation_assertion()
    {
        var d = Guard(FullyEligible(isolationBy: null))
            .Check(Rig, "DB_GaugeInterface", Observed(), Scope());

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.IsolationAssertionUnattributed, d.Reason);
    }

    // ---------------------------------------------------------------- gate 5: identity

    [Fact]
    public void Refuses_when_the_entry_declares_no_identity_to_verify_against()
    {
        var d = Guard(FullyEligible(order: null, serial: null))
            .Check(Rig, "DB_GaugeInterface", Observed(), Scope());

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.NoDeclaredIdentity, d.Reason);
    }

    [Fact]
    public void Refuses_when_no_identity_was_read_from_the_device()
    {
        var d = Guard(FullyEligible())
            .Check(Rig, "DB_GaugeInterface", observedIdentity: null, runScope: Scope());

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.IdentityNotVerified, d.Reason);
    }

    /// <summary>
    /// The scenario this whole mechanism exists for: the right address, the right order number —
    /// a different physical controller. Observed live on 2026-08-11, where a VPN tunnel injected
    /// 10.10.10.0/24 at metric 0 and shadowed a local segment carrying the same range.
    /// </summary>
    [Fact]
    public void Refuses_a_different_device_answering_at_the_expected_address()
    {
        var d = Guard(FullyEligible())
            .Check(Rig, "DB_GaugeInterface", Observed(serial: "S C-DIFFERENT00001"), Scope());

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.IdentityMismatch, d.Reason);
        Assert.Contains("serial number mismatch", d.Message);
    }

    [Fact]
    public void Refuses_when_a_declared_identifier_was_not_reported_rather_than_treating_it_as_a_pass()
    {
        // Entry declares a serial; device reports only an order number. Silence is not agreement.
        var d = Guard(FullyEligible())
            .Check(Rig, "DB_GaugeInterface", Observed(serial: null), Scope());

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.IdentityMismatch, d.Reason);
    }

    [Fact]
    public void Matches_mac_regardless_of_separator_and_case()
    {
        var entry = FullyEligible(order: null, serial: null) with { MacAddress = "00-1B-1B-2C-04-0F" };
        var d = Guard(entry).Check(Rig, "DB_GaugeInterface", Observed(null, null, "001b:1b2c:040f"), Scope());

        Assert.True(d.Allowed);
    }

    // ---------------------------------------------------------------- gate 6: scope

    [Fact]
    public void Refuses_when_the_run_declared_no_scope()
    {
        var d = Guard(FullyEligible())
            .Check(Rig, "DB_GaugeInterface", Observed(), runScope: null);

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.NoScopeDeclared, d.Reason);
    }

    [Fact]
    public void Refuses_an_area_outside_what_this_run_declared()
    {
        var d = Guard(FullyEligible())
            .Check(Rig, "DB_PanelCmd", Observed(), Scope("DB_GaugeInterface"));

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.AreaOutsideRunScope, d.Reason);
    }

    [Fact]
    public void Refuses_an_area_the_run_declared_but_the_device_entry_caps_out()
    {
        var d = Guard(FullyEligible(cap: new[] { "DB_GaugeInterface" }))
            .Check(Rig, "DB_PanelCmd", Observed(), Scope("DB_GaugeInterface", "DB_PanelCmd"));

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.AreaOutsideDeviceCap, d.Reason);
    }

    [Fact]
    public void An_absent_device_cap_means_no_cap_not_no_permission()
    {
        var d = Guard(FullyEligible(cap: null))
            .Check(Rig, "DB_PanelCmd", Observed(), Scope("DB_PanelCmd"));

        Assert.True(d.Allowed);
    }

    // ---------------------------------------------------------------- gate 7: reversibility

    [Fact]
    public void Refuses_when_no_verified_restore_point_exists()
    {
        var d = new DeviceWriteGuard(Loaded(FullyEligible()), NoRestorePoints.Instance)
            .Check(Rig, "DB_GaugeInterface", Observed(), Scope());

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.NoRestorePoint, d.Reason);
    }

    [Fact]
    public void Omitting_the_restore_point_store_refuses_rather_than_skipping_the_check()
    {
        // Forgetting to wire a store must not silently authorize writes.
        var d = new DeviceWriteGuard(Loaded(FullyEligible()))
            .Check(Rig, "DB_GaugeInterface", Observed(), Scope());

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.NoRestorePoint, d.Reason);
    }

    // ---------------------------------------------------------------- empty is not clean

    [Fact]
    public void An_empty_allowlist_grants_no_writes()
    {
        var d = new DeviceWriteGuard(Loaded(), new HasRestorePoint())
            .Check(Rig, "DB_GaugeInterface", Observed(), Scope());

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.ReadGateRefused, d.Reason);
    }

    [Fact]
    public void An_unreadable_allowlist_grants_no_writes()
    {
        var broken = AllowlistFile.Result.Fail(GuardReason.AllowlistUnreadable, "corrupt json", "test-allowlist.json");
        var d = new DeviceWriteGuard(broken, new HasRestorePoint())
            .Check(Rig, "DB_GaugeInterface", Observed(), Scope());

        Assert.False(d.Allowed);
        Assert.Equal(WriteRefusal.ReadGateRefused, d.Reason);
    }
}
