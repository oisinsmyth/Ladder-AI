using System.Globalization;

namespace Harness.S7;

/// <summary>
/// Converts between the string form the harness speaks and the big-endian bytes an S7 CPU speaks.
///
/// <para><b>Why strings.</b> <see cref="Harness.ITransport"/> reads and writes strings and leaves
/// comparison to the runner, which is what lets the vector format stay declarative text. The cost is
/// that formatting decisions matter: a Real rendered with the host's decimal separator would compare
/// unequal on a machine with a comma locale and equal on this one, so every conversion here is
/// <see cref="CultureInfo.InvariantCulture"/>, without exception.</para>
///
/// <para><b>Why range checks are hard errors.</b> Writing 70000 into an Int could truncate to 4464 and
/// carry on. A vector that stimulates a value the tag cannot hold is a broken vector, and the only
/// useful response is to say so — a silently truncated stimulus produces a failure at the far end of
/// the plant model, hours from its cause.</para>
/// </summary>
public static class S7Values
{
    // ---------------------------------------------------------------- decode (device -> string)

    /// <summary>
    /// Render the bytes of one tag. <paramref name="buffer"/> holds exactly the tag's bytes, starting
    /// at the tag's own offset.
    /// </summary>
    public static string Decode(S7Tag tag, byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(buffer);

        var need = tag.Type.SizeInBytes();
        if (buffer.Length < need)
            throw new S7TransportException(
                $"decoding '{tag.Name}' needs {need} byte(s) but only {buffer.Length} were read.");

        return tag.Type switch
        {
            // "True"/"False" rather than "1"/"0": the runner's default comparison is a
            // case-insensitive string equality, so a vector may write true/TRUE/True and still match,
            // whereas "1" would not. Encode() accepts both spellings on the way in.
            S7DataType.Bool => ((buffer[0] >> tag.BitOffset) & 1) == 1 ? "True" : "False",

            S7DataType.Byte => buffer[0].ToString(CultureInfo.InvariantCulture),
            S7DataType.Word => ReadUInt16(buffer).ToString(CultureInfo.InvariantCulture),
            S7DataType.Int => ReadInt16(buffer).ToString(CultureInfo.InvariantCulture),
            S7DataType.DWord => ReadUInt32(buffer).ToString(CultureInfo.InvariantCulture),
            S7DataType.UDInt => ReadUInt32(buffer).ToString(CultureInfo.InvariantCulture),
            S7DataType.DInt => ReadInt32(buffer).ToString(CultureInfo.InvariantCulture),

            // "R" is the shortest form that round-trips exactly. Anything shorter would make a
            // tolerance-free comparison of a value we just wrote fail against itself.
            S7DataType.Real => ReadSingle(buffer).ToString("R", CultureInfo.InvariantCulture),

            _ => throw new S7TransportException($"tag '{tag.Name}': unsupported type {tag.Type}."),
        };
    }

    /// <summary>Decode a tag as an integer, for the scan counter. Unsigned widths never come back negative.</summary>
    public static long DecodeAsInteger(S7Tag tag, byte[] buffer) => tag.Type switch
    {
        S7DataType.Byte => buffer[0],
        S7DataType.Word => ReadUInt16(buffer),
        S7DataType.Int => ReadInt16(buffer),
        S7DataType.DWord or S7DataType.UDInt => ReadUInt32(buffer),
        S7DataType.DInt => ReadInt32(buffer),
        _ => throw new S7TransportException(
            $"tag '{tag.Name}' is {tag.Type}, which is not an integer; a scan counter must be an " +
            "integer tag (DInt or UDInt in practice)."),
    };

    // ---------------------------------------------------------------- encode (string -> device)

    /// <summary>Interpret a Bool stimulus. Accepts the spellings a hand-written vector actually uses.</summary>
    public static bool DecodeBool(S7Tag tag, string? value)
    {
        var v = (value ?? string.Empty).Trim();

        return v.ToLowerInvariant() switch
        {
            "true" or "1" or "on" => true,
            "false" or "0" or "off" => false,
            _ => throw new S7TransportException(
                $"cannot write '{value}' to Bool tag '{tag.Name}'. Use true/false (or 1/0, on/off)."),
        };
    }

    /// <summary>
    /// Produce the bytes for a non-Bool tag. Bools do not come through here: they are written a bit at
    /// a time so that the transport never read-modify-writes a live byte (see
    /// <see cref="IS7Client.WriteBit"/>).
    /// </summary>
    public static byte[] Encode(S7Tag tag, string? value)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (tag.Type == S7DataType.Bool)
            throw new InvalidOperationException(
                $"Bool tag '{tag.Name}' must be written with a bit write, not a byte encode.");

        var raw = (value ?? string.Empty).Trim();

        if (tag.Type == S7DataType.Real)
        {
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                throw new S7TransportException(
                    $"cannot write '{value}' to Real tag '{tag.Name}': not a number. Note that the " +
                    "decimal separator is always '.', whatever the machine's locale.");

            if (float.IsNaN(f) || float.IsInfinity(f))
                throw new S7TransportException(
                    $"refusing to write {raw} to Real tag '{tag.Name}': NaN and infinity are not " +
                    "values a plant model should be stimulated with, and an S7 Real carrying one " +
                    "propagates into every arithmetic result downstream of it.");

            return WriteBigEndian(BitConverter.GetBytes(f));
        }

        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            throw new S7TransportException(
                $"cannot write '{value}' to {tag.Type} tag '{tag.Name}': not a whole number.");

        var (min, max) = Range(tag.Type);
        if (n < min || n > max)
            throw new S7TransportException(
                $"cannot write {n} to {tag.Type} tag '{tag.Name}': out of range ({min}..{max}). " +
                "Refused rather than truncated — a silently wrapped stimulus fails somewhere else.");

        return tag.Type switch
        {
            S7DataType.Byte => new[] { (byte)n },
            S7DataType.Word => WriteBigEndian(BitConverter.GetBytes((ushort)n)),
            S7DataType.Int => WriteBigEndian(BitConverter.GetBytes((short)n)),
            S7DataType.DWord or S7DataType.UDInt => WriteBigEndian(BitConverter.GetBytes((uint)n)),
            S7DataType.DInt => WriteBigEndian(BitConverter.GetBytes((int)n)),
            _ => throw new S7TransportException($"tag '{tag.Name}': unsupported type {tag.Type}."),
        };
    }

    private static (long Min, long Max) Range(S7DataType t) => t switch
    {
        S7DataType.Byte => (byte.MinValue, byte.MaxValue),
        S7DataType.Word => (ushort.MinValue, ushort.MaxValue),
        S7DataType.Int => (short.MinValue, short.MaxValue),
        S7DataType.DWord or S7DataType.UDInt => (uint.MinValue, uint.MaxValue),
        S7DataType.DInt => (int.MinValue, int.MaxValue),
        _ => throw new S7TransportException($"no integer range for {t}."),
    };

    // ---------------------------------------------------------------- byte order
    //
    // S7 is big-endian on the wire and every x86/ARM host this runs on is little-endian, so a
    // conversion is always required. BitConverter.IsLittleEndian is checked rather than assumed
    // because assuming it is exactly the kind of thing that is true until it is not.

    private static byte[] WriteBigEndian(byte[] hostOrder)
    {
        if (BitConverter.IsLittleEndian) Array.Reverse(hostOrder);
        return hostOrder;
    }

    private static byte[] ToHostOrder(byte[] bigEndian, int count)
    {
        var slice = new byte[count];
        Array.Copy(bigEndian, slice, count);
        if (BitConverter.IsLittleEndian) Array.Reverse(slice);
        return slice;
    }

    private static ushort ReadUInt16(byte[] b) => BitConverter.ToUInt16(ToHostOrder(b, 2), 0);
    private static short ReadInt16(byte[] b) => BitConverter.ToInt16(ToHostOrder(b, 2), 0);
    private static uint ReadUInt32(byte[] b) => BitConverter.ToUInt32(ToHostOrder(b, 4), 0);
    private static int ReadInt32(byte[] b) => BitConverter.ToInt32(ToHostOrder(b, 4), 0);
    private static float ReadSingle(byte[] b) => BitConverter.ToSingle(ToHostOrder(b, 4), 0);
}
