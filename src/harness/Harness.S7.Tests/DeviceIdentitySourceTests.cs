using System.Text;
using DeviceGuard;
using Harness.S7;

namespace Harness.S7.Tests;

/// <summary>
/// Identity reading, and specifically every way it can fail QUIETLY. A source that returns an empty
/// identity instead of throwing produces a refusal blamed on the device rather than on the read, and
/// two sources that disagree produce an identity that is nobody's.
/// </summary>
public class DeviceIdentitySourceTests
{
    /// <summary>A fake that has already been connected — every source reads over a live session.</summary>
    private static FakeS7Client Device() => Connected(new FakeS7Client());

    private static FakeS7Client Connected(FakeS7Client client)
    {
        client.Connect("192.0.2.11", 0, 1, 1000);
        return client;
    }

    // ---------------------------------------------------------------- order code

    [Fact]
    public void The_order_code_becomes_the_order_number()
    {
        var identity = new OrderCodeIdentitySource().Read(Device());

        Assert.Equal("6ES7 214-1AG40-0XB0", identity.OrderNumber);
        Assert.Null(identity.SerialNumber);
    }

    [Fact]
    public void An_empty_order_code_is_a_failed_read_not_a_device_without_one()
    {
        var device = Device();
        device.OrderCode = "   ";

        var ex = Assert.Throws<S7TransportException>(() => new OrderCodeIdentitySource().Read(device));
        Assert.Contains("treated as a failed read", ex.Message);
    }

    [Fact]
    public void A_protocol_failure_reading_the_order_code_throws()
    {
        var device = Device();
        device.OrderCodeFailureCode = 7;

        Assert.Throws<S7TransportException>(() => new OrderCodeIdentitySource().Read(device));
    }

    // ---------------------------------------------------------------- marker DB

    private static FakeS7Client WithMarker(string text, int db = 100, int offset = 0, int declaredMax = 32)
    {
        var payload = new byte[2 + declaredMax];
        payload[0] = (byte)declaredMax;
        payload[1] = (byte)text.Length;
        Encoding.ASCII.GetBytes(text).CopyTo(payload, 2);

        var block = new byte[offset + payload.Length];
        payload.CopyTo(block, offset);

        return Connected(new FakeS7Client().WithBlock(db, block));
    }

    [Fact]
    public void A_marker_string_becomes_the_serial_number()
    {
        var identity = new MarkerDbIdentitySource(100, 0, 32).Read(WithMarker("RIG-BENCH-01"));

        Assert.Equal("RIG-BENCH-01", identity.SerialNumber);
        Assert.Null(identity.OrderNumber);
    }

    [Fact]
    public void A_fixed_char_marker_is_trimmed_of_its_padding()
    {
        var block = new byte[16];
        Encoding.ASCII.GetBytes("RIG-02").CopyTo(block, 0);

        var identity = new MarkerDbIdentitySource(100, 0, 16, MarkerEncoding.FixedChars)
            .Read(Connected(new FakeS7Client().WithBlock(100, block)));

        Assert.Equal("RIG-02", identity.SerialNumber);
    }

    [Fact]
    public void Bytes_that_are_not_an_s7_string_are_refused_rather_than_read_as_an_identifier()
    {
        // The commonest cause is a wrong offset: process data happens to sit there, and reading on
        // would manufacture a plausible identifier out of it and then compare it to the allowlist.
        var device = Connected(new FakeS7Client().WithBlock(100, new byte[] { 4, 200, 65, 66, 67, 68, 69, 70 }));

        var ex = Assert.Throws<S7TransportException>(() => new MarkerDbIdentitySource(100, 0, 6).Read(device));
        Assert.Contains("not an S7 String", ex.Message);
    }

    [Fact]
    public void An_empty_marker_is_a_failed_identification()
    {
        var ex = Assert.Throws<S7TransportException>(() => new MarkerDbIdentitySource(100, 0, 32).Read(WithMarker("")));
        Assert.Contains("failed identification", ex.Message);
    }

    [Fact]
    public void An_unreadable_marker_db_names_the_two_settings_that_usually_cause_it()
    {
        var ex = Assert.Throws<S7TransportException>(() =>
            new MarkerDbIdentitySource(999, 0, 32).Read(Device()));

        Assert.Contains("Optimized block access", ex.Message);
        Assert.Contains("PUT/GET", ex.Message);
    }

    // ---------------------------------------------------------------- CPU info

    [Fact]
    public void Cpu_info_yields_a_serial_when_the_cpu_fills_it_in()
    {
        Assert.Equal("S C-K1U399102021", new CpuInfoIdentitySource().Read(Device()).SerialNumber);
    }

    [Fact]
    public void A_blank_cpu_serial_is_a_failed_read()
    {
        var device = Device();
        device.CpuInfo = new S7CpuInfo("CPU 1214C DC/DC/DC", "", "PLC_1");

        Assert.Throws<S7TransportException>(() => new CpuInfoIdentitySource().Read(device));
    }

    // ---------------------------------------------------------------- composition

    [Fact]
    public void Sources_compose_into_one_identity()
    {
        var device = WithMarker("RIG-BENCH-01");

        var identity = DeviceIdentityReader.Read(device, new IDeviceIdentitySource[]
        {
            new OrderCodeIdentitySource(),
            new MarkerDbIdentitySource(100, 0, 32),
        });

        Assert.Equal("6ES7 214-1AG40-0XB0", identity.OrderNumber);
        Assert.Equal("RIG-BENCH-01", identity.SerialNumber);
    }

    [Fact]
    public void Adding_a_marker_source_needs_no_change_to_anything_else()
    {
        // The property the brief asks for, stated as a test: an identity built from one source and an
        // identity built from two differ only in what they carry.
        var device = WithMarker("RIG-BENCH-01");

        var withoutMarker = DeviceIdentityReader.Read(device, new IDeviceIdentitySource[] { new OrderCodeIdentitySource() });
        var withMarker = DeviceIdentityReader.Read(device, new IDeviceIdentitySource[]
        {
            new OrderCodeIdentitySource(), new MarkerDbIdentitySource(100, 0, 32),
        });

        Assert.Equal(withoutMarker.OrderNumber, withMarker.OrderNumber);
        Assert.Null(withoutMarker.SerialNumber);
        Assert.NotNull(withMarker.SerialNumber);
    }

    [Fact]
    public void One_failing_source_fails_the_whole_read_and_names_itself()
    {
        var device = WithMarker("RIG-BENCH-01");
        device.OrderCodeFailureCode = 3;

        var ex = Assert.Throws<S7TransportException>(() => DeviceIdentityReader.Read(device,
            new IDeviceIdentitySource[] { new OrderCodeIdentitySource(), new MarkerDbIdentitySource(100, 0, 32) }));

        Assert.Contains("source 'order code' failed", ex.Message);
    }

    [Fact]
    public void Sources_that_contradict_each_other_are_refused()
    {
        var device = WithMarker("RIG-BENCH-01");
        device.CpuInfo = new S7CpuInfo(SerialNumber: "SOMETHING-ELSE");

        var ex = Assert.Throws<S7TransportException>(() => DeviceIdentityReader.Read(device,
            new IDeviceIdentitySource[] { new MarkerDbIdentitySource(100, 0, 32), new CpuInfoIdentitySource() }));

        Assert.Contains("contradict each other", ex.Message);
    }

    [Fact]
    public void No_sources_at_all_is_a_configuration_error_not_an_empty_identity()
    {
        Assert.Throws<S7ConfigurationException>(() =>
            DeviceIdentityReader.Read(Device(), Array.Empty<IDeviceIdentitySource>()));
    }

    [Fact]
    public void An_identity_read_this_way_satisfies_the_guards_own_comparison()
    {
        // The seam that matters: what the sources produce is exactly what DeviceWriteGuard compares.
        var device = WithMarker("RIG-BENCH-01");
        var identity = DeviceIdentityReader.Read(device, new IDeviceIdentitySource[]
        {
            new OrderCodeIdentitySource(), new MarkerDbIdentitySource(100, 0, 32),
        });

        var entry = new AllowlistEntry(
            Address: "192.0.2.11", Kind: "test-rig",
            OrderNumber: "6ES7 214-1AG40-0XB0", SerialNumber: "RIG-BENCH-01");

        Assert.True(DeviceIdentity.Compare(entry, identity).Matched);
    }
}
