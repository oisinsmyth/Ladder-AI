using System.Globalization;
using Harness.S7;

namespace Harness.S7.Tests;

/// <summary>
/// Byte-order and formatting. Both are the kind of thing that works on the machine it was written on.
/// </summary>
public class S7ValuesTests
{
    private static S7Tag Tag(S7DataType type, int bit = 0) => new("T", "DB_A", 1, 0, type, bit);

    [Theory]
    [InlineData(S7DataType.Int, "-3")]
    [InlineData(S7DataType.Int, "32767")]
    [InlineData(S7DataType.DInt, "-2147483648")]
    [InlineData(S7DataType.UDInt, "4294967295")]
    [InlineData(S7DataType.Word, "65535")]
    [InlineData(S7DataType.Byte, "255")]
    [InlineData(S7DataType.Real, "512.25")]
    [InlineData(S7DataType.Real, "-0.001")]
    public void Encode_and_decode_round_trip(S7DataType type, string value)
    {
        var tag = Tag(type);
        Assert.Equal(value, S7Values.Decode(tag, S7Values.Encode(tag, value)));
    }

    [Fact]
    public void Integers_are_written_big_endian()
    {
        Assert.Equal(new byte[] { 0x01, 0x02 }, S7Values.Encode(Tag(S7DataType.Int), "258"));
        Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x02 }, S7Values.Encode(Tag(S7DataType.DInt), "258"));
    }

    [Fact]
    public void A_real_is_written_big_endian()
    {
        // 1.0f is 0x3F800000.
        Assert.Equal(new byte[] { 0x3F, 0x80, 0x00, 0x00 }, S7Values.Encode(Tag(S7DataType.Real), "1.0"));
    }

    [Fact]
    public void A_value_that_does_not_fit_is_refused_rather_than_truncated()
    {
        var ex = Assert.Throws<S7TransportException>(() => S7Values.Encode(Tag(S7DataType.Int), "70000"));

        Assert.Contains("out of range", ex.Message);
        Assert.Contains("Refused rather than truncated", ex.Message);
    }

    [Fact]
    public void A_negative_value_in_an_unsigned_tag_is_refused()
    {
        Assert.Throws<S7TransportException>(() => S7Values.Encode(Tag(S7DataType.UDInt), "-1"));
    }

    [Fact]
    public void NaN_and_infinity_are_refused()
    {
        Assert.Throws<S7TransportException>(() => S7Values.Encode(Tag(S7DataType.Real), "NaN"));
        Assert.Throws<S7TransportException>(() => S7Values.Encode(Tag(S7DataType.Real), "Infinity"));
    }

    [Fact]
    public void Formatting_does_not_depend_on_the_machines_locale()
    {
        // On a comma-decimal locale, a Real rendered with the current culture would come back "512,25"
        // and compare unequal to the "512.25" a vector was written with — on that machine only.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var tag = Tag(S7DataType.Real);
            Assert.Equal("512.25", S7Values.Decode(tag, S7Values.Encode(tag, "512.25")));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("on", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData(" off ", false)]
    public void Bool_stimulus_accepts_the_spellings_a_hand_written_vector_uses(string text, bool expected)
    {
        Assert.Equal(expected, S7Values.DecodeBool(Tag(S7DataType.Bool), text));
    }

    [Fact]
    public void A_bool_stimulus_that_is_neither_is_refused()
    {
        Assert.Throws<S7TransportException>(() => S7Values.DecodeBool(Tag(S7DataType.Bool), "yes"));
    }

    [Fact]
    public void A_bool_decodes_from_its_own_bit()
    {
        var buffer = new byte[] { 0b0100_0000 };

        Assert.Equal("True", S7Values.Decode(Tag(S7DataType.Bool, 6), buffer));
        Assert.Equal("False", S7Values.Decode(Tag(S7DataType.Bool, 5), buffer));
    }

    [Fact]
    public void A_bool_never_goes_through_the_byte_encoder()
    {
        // Bools are written a bit at a time so the transport never read-modify-writes a live byte.
        Assert.Throws<InvalidOperationException>(() => S7Values.Encode(Tag(S7DataType.Bool), "true"));
    }

    [Fact]
    public void A_short_read_is_refused_rather_than_decoded()
    {
        var ex = Assert.Throws<S7TransportException>(() => S7Values.Decode(Tag(S7DataType.Real), new byte[2]));
        Assert.Contains("needs 4 byte(s)", ex.Message);
    }

    [Fact]
    public void A_real_is_not_an_integer_and_cannot_be_a_scan_counter()
    {
        var ex = Assert.Throws<S7TransportException>(() =>
            S7Values.DecodeAsInteger(Tag(S7DataType.Real), new byte[4]));

        Assert.Contains("scan counter must be an integer", ex.Message);
    }

    [Fact]
    public void A_full_scale_udint_does_not_come_back_negative()
    {
        Assert.Equal(4294967295L,
            S7Values.DecodeAsInteger(Tag(S7DataType.UDInt), new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }));
    }
}
