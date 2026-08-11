using DeviceGuard;

namespace Harness.S7.Tests;

/// <summary>
/// The plan's job is to catch, at configuration time, an entry that could never pass — and to say so
/// as a configuration fault rather than letting it arrive at connect time dressed as a device fault.
///
/// <para>Every "unusable" case here was already refused before this type existed. What these tests pin
/// is WHICH problem is reported, because the wrong diagnosis on this particular fence leads somewhere
/// bad: the quickest way to make "device did not report a serial" go away is to delete the serial from
/// the allowlist entry, and that silently downgrades the check from "this exact box" to "a box of this
/// model" — on a network where the same address reaches different physical controllers depending on
/// which tunnel is up.</para>
/// </summary>
public class IdentitySourcePlanTests
{
    private const string Rig = "10.10.10.10";
    private const string Order = "6ES7 214-1AG40-0XB0";
    private const string Marker = "RIG-BENCH-01";

    /// <summary>
    /// A serial of the shape a CPU label carries — and which the CPU itself will not report over
    /// classic S7comm, which is the whole case these tests are about. Invented, like every other
    /// identifier in this suite.
    /// </summary>
    private const string UnreadableSerial = "S C-EXAMPLE00001";

    private static AllowlistEntry Entry(
        string? order = Order,
        string? serial = null,
        string? mac = null,
        MarkerLocation? marker = null,
        bool useCpuInfo = false) =>
        new(Rig,
            Label: "Bench rig",
            Kind: AllowlistEntry.TestRigKind,
            OrderNumber: order,
            SerialNumber: serial,
            MacAddress: mac,
            Marker: marker,
            UseCpuInfoSerial: useCpuInfo);

    private static MarkerLocation Good(string? encoding = null) =>
        new(DbNumber: 100, ByteOffset: 2, Length: 32, Encoding: encoding);

    [Fact]
    public void Order_code_is_always_read_even_when_nothing_is_declared()
    {
        // DeviceIdentityReader refuses to run with no sources, so the plan must never produce an empty
        // set. An entry declaring no identifiers is not the plan's problem to reject — DeviceIdentity
        // .Compare already refuses to treat "nothing declared" as write-usable.
        var plan = IdentitySourcePlan.ForEntry(Entry(order: null));

        Assert.True(plan.IsUsable);
        Assert.Single(plan.Sources);
        Assert.IsType<OrderCodeIdentitySource>(plan.Sources[0]);
    }

    [Fact]
    public void A_declared_serial_with_a_marker_to_read_it_from_is_usable()
    {
        var plan = IdentitySourcePlan.ForEntry(Entry(serial: Marker, marker: Good()));

        Assert.True(plan.IsUsable);
        Assert.Null(plan.Problem);
        Assert.Equal(2, plan.Sources.Count);
        Assert.Contains(plan.Sources, s => s is MarkerDbIdentitySource);
    }

    [Fact]
    public void A_declared_serial_with_no_way_to_read_one_is_a_configuration_fault()
    {
        // The live case as of 2026-08-11: the entry declares the serial printed on the CPU's label, and
        // the CPU refuses every request that would report it.
        var plan = IdentitySourcePlan.ForEntry(Entry(serial: UnreadableSerial));

        Assert.False(plan.IsUsable);
        Assert.Contains("no configured source can read one", plan.Problem);
    }

    [Fact]
    public void The_unreadable_serial_message_warns_against_deleting_the_serial()
    {
        // Pinned deliberately. Removing the serial makes the refusal disappear and leaves the fence
        // verifying an order code that every unit of the model reports identically.
        var plan = IdentitySourcePlan.ForEntry(Entry(serial: UnreadableSerial));

        Assert.Contains("DO NOT remove the serial number", plan.Problem);
        Assert.Contains("a model and not a device", plan.Problem);
    }

    [Fact]
    public void Cpu_info_opt_in_satisfies_a_declared_serial_without_a_marker()
    {
        var plan = IdentitySourcePlan.ForEntry(Entry(serial: UnreadableSerial, useCpuInfo: true));

        Assert.True(plan.IsUsable);
        Assert.Contains(plan.Sources, s => s is CpuInfoIdentitySource);
    }

    [Fact]
    public void Cpu_info_is_off_unless_asked_for()
    {
        // It throws on the CPU measured here, and a source that always throws fails every connection.
        var plan = IdentitySourcePlan.ForEntry(Entry(serial: Marker, marker: Good()));

        Assert.DoesNotContain(plan.Sources, s => s is CpuInfoIdentitySource);
    }

    [Fact]
    public void A_marker_with_nothing_to_check_it_against_is_refused()
    {
        // Read on every connection and compared against nothing — configured under the impression it
        // was being verified.
        var plan = IdentitySourcePlan.ForEntry(Entry(serial: null, marker: Good()));

        Assert.False(plan.IsUsable);
        Assert.Contains("never checked", plan.Problem);
    }

    [Theory]
    [InlineData(null, 2, 32, "no dbNumber")]
    [InlineData(100, null, 32, "no byteOffset")]
    [InlineData(100, 2, null, "no length")]
    [InlineData(0, 2, 32, "must be positive")]
    [InlineData(100, -1, 32, "must not be negative")]
    [InlineData(100, 2, 255, "must be 1-254")]
    public void A_malformed_marker_is_refused_rather_than_defaulted(
        int? db, int? offset, int? length, string expected)
    {
        // Defaulting any of these would point the reader at the wrong bytes, and wrong bytes that happen
        // to parse would be compared against the allowlist and could match.
        var plan = IdentitySourcePlan.ForEntry(Entry(
            serial: Marker,
            marker: new MarkerLocation(DbNumber: db, ByteOffset: offset, Length: length)));

        Assert.False(plan.IsUsable);
        Assert.Contains(expected, plan.Problem);
    }

    [Fact]
    public void An_unknown_marker_encoding_is_refused_not_guessed()
    {
        var plan = IdentitySourcePlan.ForEntry(Entry(
            serial: Marker, marker: Good(encoding: "utf8")));

        Assert.False(plan.IsUsable);
        Assert.Contains("unknown marker encoding", plan.Problem);
    }

    [Fact]
    public void A_marker_declared_as_fixed_chars_builds_a_fixed_chars_reader()
    {
        var plan = IdentitySourcePlan.ForEntry(Entry(
            serial: Marker, marker: Good(encoding: MarkerLocation.FixedChars)));

        Assert.True(plan.IsUsable);
        Assert.Contains(plan.Sources, s => s is MarkerDbIdentitySource);
    }

    [Fact]
    public void A_declared_mac_cannot_be_satisfied_across_a_routed_tunnel()
    {
        var plan = IdentitySourcePlan.ForEntry(Entry(
            serial: Marker, marker: Good(), mac: "00-1B-1B-2C-3D-4E"));

        Assert.False(plan.IsUsable);
        Assert.Contains("not visible across a routed tunnel", plan.Problem);
    }

    [Fact]
    public void A_malformed_marker_is_reported_before_the_missing_serial()
    {
        // Both faults are present. The marker is the one someone can act on; reporting the serial first
        // would send them to add a serial the broken marker still could not read.
        var plan = IdentitySourcePlan.ForEntry(Entry(
            serial: null, marker: new MarkerLocation(DbNumber: 100, Length: 32)));

        Assert.False(plan.IsUsable);
        Assert.Contains("no byteOffset", plan.Problem);
    }

    [Fact]
    public void Describe_names_the_sources_that_will_be_read()
    {
        var plan = IdentitySourcePlan.ForEntry(Entry(serial: Marker, marker: Good()));

        Assert.Contains("order code", plan.Describe());
        Assert.Contains("DB100", plan.Describe());
    }

    [Fact]
    public void A_null_entry_is_a_programming_error_not_an_empty_plan()
    {
        Assert.Throws<ArgumentNullException>(() => IdentitySourcePlan.ForEntry(null!));
    }
}
