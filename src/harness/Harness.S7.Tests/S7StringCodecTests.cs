using System.Text;
using Harness.RigWrite;
using Harness.S7;

namespace Harness.S7.Tests;

/// <summary>
/// The S7 <c>String[n]</c> layout, both directions, as pure arithmetic on a byte buffer.
///
/// <para>These tests exist because the first governed write PRODUCES this layout where until now the
/// project only CONSUMED it, and because the marker DB's member offsets are computed rather than
/// measured. Laying the whole 104-byte block out in memory at the computed offsets and reading every
/// member back is the strongest check available without a device: it does not prove the device's block
/// looks like this, but it proves that IF it does, these offsets address it.</para>
/// </summary>
public class S7StringCodecTests
{
    // ---------------------------------------------------------------- shape

    [Fact]
    public void A_string_occupies_its_declared_length_plus_a_two_byte_header()
    {
        Assert.Equal(34, S7StringCodec.SizeOf(32));
        Assert.Equal(34, S7StringCodec.Encode("short", 32).Length);
        Assert.Equal(34, S7StringCodec.Encode(string.Empty, 32).Length);
    }

    [Fact]
    public void The_header_carries_the_declared_maximum_and_the_current_length()
    {
        var bytes = S7StringCodec.Encode("RIG01", 32);

        Assert.Equal(32, bytes[0]);
        Assert.Equal(5, bytes[1]);
    }

    [Fact]
    public void The_tail_past_the_current_length_is_zeroed_so_the_write_is_deterministic()
    {
        // A read-back comparison is byte-for-byte; an undefined tail would make it unverifiable.
        var bytes = S7StringCodec.Encode("AB", 8);

        Assert.All(bytes.Skip(2 + 2), b => Assert.Equal(0, b));
    }

    // ---------------------------------------------------------------- round trip

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("RIG-BENCH-01")]
    [InlineData("6ES7 214-1AG40-0XB0")]
    [InlineData("12345678901234567890123456789012")]   // exactly 32 — the boundary
    public void Encode_and_decode_round_trip(string text)
    {
        var bytes = S7StringCodec.Encode(text, 32);

        Assert.Equal(text, S7StringCodec.Decode(bytes, 32, "test"));
    }

    [Fact]
    public void The_whole_marker_block_round_trips_at_the_computed_offsets()
    {
        // The block as it is believed to exist: Int at 0, then three String[32] back to back. Every
        // member is written at its computed offset into one 104-byte buffer and read back from it, so
        // an offset that overlapped a neighbour would corrupt one of the three.
        var block = new byte[MarkerDbLayout.TotalBytes];

        block[MarkerDbLayout.FormatOffset] = 0;
        block[MarkerDbLayout.FormatOffset + 1] = 1;                  // Int 1, big-endian

        Place(block, MarkerDbLayout.RigMarkerOffset, "RIG-BENCH-01");
        Place(block, MarkerDbLayout.OrderNumberOffset, "6ES7 214-1AG40-0XB0");
        Place(block, MarkerDbLayout.SerialNumberOffset, string.Empty);

        Assert.Equal("RIG-BENCH-01", ReadAt(block, MarkerDbLayout.RigMarkerOffset));
        Assert.Equal("6ES7 214-1AG40-0XB0", ReadAt(block, MarkerDbLayout.OrderNumberOffset));
        Assert.Equal(string.Empty, ReadAt(block, MarkerDbLayout.SerialNumberOffset));
    }

    [Fact]
    public void Writing_the_reserved_member_disturbs_nothing_else_in_the_block()
    {
        // The property that makes the SerialNumber member the safe first write: its 34 bytes are
        // entirely its own. If the computed offsets were wrong by even one byte this would fail here
        // rather than on a device.
        var block = new byte[MarkerDbLayout.TotalBytes];
        Place(block, MarkerDbLayout.RigMarkerOffset, "RIG-BENCH-01");
        Place(block, MarkerDbLayout.OrderNumberOffset, "6ES7 214-1AG40-0XB0");

        var before = (byte[])block.Clone();

        Place(block, MarkerDbLayout.SerialNumberOffset, "RIGWRITE-PROBE");

        Assert.Equal(
            before.Take(MarkerDbLayout.SerialNumberOffset),
            block.Take(MarkerDbLayout.SerialNumberOffset));

        Assert.Equal("RIGWRITE-PROBE", ReadAt(block, MarkerDbLayout.SerialNumberOffset));
    }

    [Fact]
    public void The_computed_layout_agrees_with_the_block_size_tia_reports()
    {
        Assert.True(MarkerDbLayout.IsSelfConsistent);
        Assert.Equal(MarkerDbLayout.TotalBytes,
            2 + (3 * S7StringCodec.SizeOf(MarkerDbLayout.StringDeclaredMax)));
    }

    // ---------------------------------------------------------------- refusals

    [Fact]
    public void A_value_too_long_for_the_declared_string_is_refused_not_truncated()
    {
        var ex = Assert.Throws<S7ConfigurationException>(() => S7StringCodec.Encode(new string('x', 33), 32));
        Assert.Contains("will not fit", ex.Message);
    }

    [Fact]
    public void A_non_ascii_character_is_refused_rather_than_substituted()
    {
        // Encoding.ASCII turns this into '?', and the write would then fail its own read-back check
        // for a reason nobody would guess from the message.
        var ex = Assert.Throws<S7ConfigurationException>(() => S7StringCodec.Encode("Oisín", 32));
        Assert.Contains("non-ASCII", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void A_declared_length_outside_1_to_254_is_refused(int declaredMax)
    {
        Assert.Throws<S7ConfigurationException>(() => S7StringCodec.SizeOf(declaredMax));
    }

    [Fact]
    public void Bytes_that_are_not_a_string_are_refused_rather_than_read_as_a_value()
    {
        // The wrong-offset case: process data that happens to sit there would otherwise be turned into
        // a plausible identifier and compared against the allowlist.
        var ex = Assert.Throws<S7TransportException>(() =>
            S7StringCodec.Decode(new byte[] { 4, 200, 65, 66, 67, 68 }, 4, "DB38.DBB70"));

        Assert.Contains("not an S7 String", ex.Message);
        Assert.Contains("DB38.DBB70", ex.Message);
    }

    [Fact]
    public void A_read_too_short_to_hold_the_header_is_refused()
    {
        var ex = Assert.Throws<S7TransportException>(() => S7StringCodec.Decode(new byte[] { 32 }, 32, "DB38.DBB70"));
        Assert.Contains("not an S7 String", ex.Message);
    }

    // ---------------------------------------------------------------- helpers

    private static void Place(byte[] block, int offset, string text) =>
        S7StringCodec.Encode(text, MarkerDbLayout.StringDeclaredMax).CopyTo(block, offset);

    private static string ReadAt(byte[] block, int offset)
    {
        var slice = new byte[S7StringCodec.SizeOf(MarkerDbLayout.StringDeclaredMax)];
        Array.Copy(block, offset, slice, 0, slice.Length);
        return S7StringCodec.Decode(slice, MarkerDbLayout.StringDeclaredMax, $"DB38.DBB{offset}");
    }

    /// <summary>
    /// The codec and the marker reader must agree, because the reader is what the write fence's
    /// identity gate depends on. Encoding a marker here and reading it through
    /// <see cref="MarkerDbIdentitySource"/> is that agreement stated once.
    /// </summary>
    [Fact]
    public void A_marker_encoded_here_is_read_back_by_the_identity_source()
    {
        var block = new byte[MarkerDbLayout.TotalBytes];
        Place(block, MarkerDbLayout.RigMarkerOffset, "RIG-BENCH-01");

        var device = new FakeS7Client().WithBlock(MarkerDbLayout.DbNumber, block);
        device.Connect("192.0.2.11", 0, 1, 1000);

        var identity = new MarkerDbIdentitySource(
            MarkerDbLayout.DbNumber, MarkerDbLayout.RigMarkerOffset, MarkerDbLayout.StringDeclaredMax)
            .Read(device);

        Assert.Equal("RIG-BENCH-01", identity.SerialNumber);
    }

    [Fact]
    public void Ascii_is_the_encoding_on_the_wire()
    {
        // Pinned so a well-meaning switch to UTF-8 shows up here rather than as a two-byte character
        // silently shifting every subsequent byte of a fixed-width member.
        var bytes = S7StringCodec.Encode("AB", 4);
        Assert.Equal(Encoding.ASCII.GetBytes("AB"), bytes.Skip(2).Take(2));
    }
}
