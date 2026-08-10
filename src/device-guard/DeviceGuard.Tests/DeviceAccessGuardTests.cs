namespace DeviceGuard.Tests;

/// <summary>
/// The safeguard's whole point is that it fails closed. These tests pin every not-allowed path to a
/// refusal, and allow ONLY an exact test-rig match.
/// </summary>
public class DeviceAccessGuardTests
{
    private static AllowlistFile.Result Loaded(params AllowlistEntry[] entries) =>
        AllowlistFile.Result.Ok(entries, "test-allowlist.json");

    private static DeviceAccessGuard Guard(params AllowlistEntry[] entries) =>
        new(Loaded(entries));

    private static AllowlistEntry Rig(string address, string? label = null) =>
        new(address, label ?? "Bench rig", AllowlistEntry.TestRigKind);

    [Fact]
    public void Allows_exact_test_rig_match()
    {
        var d = Guard(Rig("10.10.10.15", "Bench HMI")).Check("10.10.10.15");

        Assert.True(d.Allowed);
        Assert.Equal(GuardReason.Allowed, d.Reason);
        Assert.Equal("10.10.10.15", d.MatchedEntry?.Address);
    }

    [Fact]
    public void Allows_ip_match_ignoring_surrounding_whitespace()
    {
        var d = Guard(Rig("10.10.10.15")).Check("  10.10.10.15 ");
        Assert.True(d.Allowed);
    }

    [Fact]
    public void Allows_hostname_case_insensitively()
    {
        var d = Guard(Rig("RIG-01")).Check("rig-01");
        Assert.True(d.Allowed);
    }

    [Fact]
    public void Refuses_address_not_listed()
    {
        var d = Guard(Rig("10.10.10.15")).Check("10.10.10.20");

        Assert.False(d.Allowed);
        Assert.Equal(GuardReason.TargetNotListed, d.Reason);
    }

    [Fact]
    public void Does_not_prefix_match_a_longer_address()
    {
        // The classic accident: "10.10.10.1" must NOT authorize "10.10.10.15" or vice versa.
        var guard = Guard(Rig("10.10.10.1"));

        Assert.False(guard.Check("10.10.10.15").Allowed);
        Assert.Equal(GuardReason.TargetNotListed, guard.Check("10.10.10.15").Reason);
    }

    [Fact]
    public void Refuses_listed_entry_that_is_not_a_test_rig()
    {
        var production = new AllowlistEntry("10.0.0.5", "Line 1 PLC", "production");
        var d = Guard(production, Rig("10.10.10.15")).Check("10.0.0.5");

        Assert.False(d.Allowed);
        Assert.Equal(GuardReason.EntryNotTestRig, d.Reason);
    }

    [Fact]
    public void Refuses_everything_when_no_test_rig_entries_exist()
    {
        var d = Guard(new AllowlistEntry("10.0.0.5", "Line 1 PLC", "production")).Check("10.0.0.5");

        // A listed non-rig address is reported as EntryNotTestRig (more specific)...
        Assert.Equal(GuardReason.EntryNotTestRig, d.Reason);
        // ...but an unlisted one, with no rigs at all, is AllowlistEmpty.
        var d2 = Guard(new AllowlistEntry("10.0.0.5", "Line 1 PLC", "production")).Check("10.0.0.99");
        Assert.False(d2.Allowed);
        Assert.Equal(GuardReason.AllowlistEmpty, d2.Reason);
    }

    [Fact]
    public void Refuses_blank_kind_entry()
    {
        // A blank kind is not "test-rig", so the address is listed but not honored. The guard reports
        // the more specific EntryNotTestRig (it IS listed, just not as a rig), never allows.
        var d = Guard(new AllowlistEntry("10.10.10.15", "unlabeled", Kind: null)).Check("10.10.10.15");
        Assert.False(d.Allowed);
        Assert.Equal(GuardReason.EntryNotTestRig, d.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Refuses_empty_target(string? target)
    {
        var d = Guard(Rig("10.10.10.15")).Check(target);
        Assert.False(d.Allowed);
        Assert.Equal(GuardReason.InvalidTarget, d.Reason);
    }

    [Fact]
    public void Fails_closed_when_allowlist_not_configured()
    {
        var guard = new DeviceAccessGuard(AllowlistFile.Load(null));
        var d = guard.Check("10.10.10.15");

        Assert.False(d.Allowed);
        Assert.Equal(GuardReason.NoAllowlistConfigured, d.Reason);
    }

    [Fact]
    public void Fails_closed_when_allowlist_file_missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "device-guard-nope-" + Guid.NewGuid() + ".json");
        var guard = DeviceAccessGuard.FromPath(missing);
        var d = guard.Check("10.10.10.15");

        Assert.False(d.Allowed);
        Assert.Equal(GuardReason.AllowlistFileMissing, d.Reason);
    }
}
