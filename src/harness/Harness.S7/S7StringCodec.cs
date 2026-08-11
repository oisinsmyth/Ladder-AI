using System.Text;

namespace Harness.S7;

/// <summary>
/// The TIA <c>String[n]</c>, as bytes: byte 0 is the DECLARED maximum, byte 1 the CURRENT length,
/// and then <c>n</c> character bytes — so a <c>String[32]</c> occupies 34 bytes, always, whatever it
/// currently holds.
///
/// <para><b>Why this is its own type.</b> It started as a private method inside
/// <see cref="MarkerDbIdentitySource"/>, which only ever needed to READ. The first governed write
/// targets a <c>String[32]</c>, so the same layout now has to be produced as well as consumed, and two
/// independent statements of one wire format is the class of drift this project keeps finding
/// (<see cref="DeviceGuard.DeviceAddress"/> carries the same note). One codec, used by both
/// directions, means a round-trip test is a test of the thing that actually runs.</para>
///
/// <para><b>It is pure arithmetic on a byte array.</b> No device, no session, no S7 client — which is
/// what lets the offsets of a real DB be exercised in a unit test at the exact byte positions the
/// device will use.</para>
///
/// <para><b>ASCII only, and refused rather than mangled.</b> An S7 String is one byte per character.
/// <see cref="Encoding.ASCII"/> silently substitutes <c>?</c> for anything outside it, so a name with
/// an accent in it would write bytes that decode to something else — a value that fails its own
/// read-back verification for a reason nobody would guess. It is refused at encode time instead.</para>
/// </summary>
public static class S7StringCodec
{
    /// <summary>The declared-maximum byte and the current-length byte that precede the characters.</summary>
    public const int HeaderBytes = 2;

    /// <summary>An S7 String holds at most 254 characters.</summary>
    public const int MaxDeclaredLength = 254;

    /// <summary>Bytes a <c>String[declaredMax]</c> occupies in a DB — the header plus every declared
    /// character position, occupied or not.</summary>
    public static int SizeOf(int declaredMax)
    {
        if (declaredMax is <= 0 or > MaxDeclaredLength)
            throw new S7ConfigurationException(
                $"an S7 String's declared length must be 1-{MaxDeclaredLength}, but is {declaredMax}.");

        return HeaderBytes + declaredMax;
    }

    /// <summary>
    /// Produce the complete bytes of a <c>String[declaredMax]</c> — header plus characters plus
    /// padding — ready to be written as one contiguous region.
    ///
    /// <para><b>The unused tail is zeroed, deliberately.</b> TIA does not define what the bytes past
    /// the current length hold, and a device will typically still be carrying whatever was there
    /// before. Writing a full, deterministic <c>SizeOf(declaredMax)</c>-byte region is what makes the
    /// write verifiable by comparing the read-back bytes with the bytes sent: a partial write, leaving
    /// an unknown tail, could only ever be verified by decoding, which is the weaker check of the
    /// two.</para>
    /// </summary>
    public static byte[] Encode(string? text, int declaredMax)
    {
        var size = SizeOf(declaredMax);
        var value = text ?? string.Empty;

        if (value.Length > declaredMax)
            throw new S7ConfigurationException(
                $"'{value}' is {value.Length} characters and will not fit a String[{declaredMax}]. " +
                "Refused rather than truncated — a silently shortened identifier still compares as a " +
                "value, and would be compared against the allowlist.");

        foreach (var c in value)
        {
            if (c > 0x7F)
                throw new S7ConfigurationException(
                    $"'{value}' contains the non-ASCII character '{c}', which an S7 String cannot hold. " +
                    "Encoding it would substitute '?' and the write would fail its own read-back check.");
        }

        var buffer = new byte[size];
        buffer[0] = (byte)declaredMax;
        buffer[1] = (byte)value.Length;
        Encoding.ASCII.GetBytes(value).CopyTo(buffer, HeaderBytes);
        return buffer;
    }

    /// <summary>
    /// Read a <c>String[n]</c> out of bytes that begin at the string's own first byte.
    ///
    /// <para><b>A malformed header is a hard error, not a best effort.</b> A current length larger than
    /// the declared maximum, or larger than what was actually read, means these bytes are not a String
    /// — nearly always a wrong offset. Reading on would manufacture a plausible-looking identifier out
    /// of unrelated process data, and that identifier would then be compared against the allowlist and
    /// could match.</para>
    /// </summary>
    /// <param name="buffer">Exactly the string's bytes, starting at its declared-maximum byte.</param>
    /// <param name="expectedMax">The <c>n</c> the caller believes it declared, for the sanity check.</param>
    /// <param name="where">Where these bytes came from, e.g. <c>DB38.DBB70</c>, for the message.</param>
    public static string Decode(byte[] buffer, int expectedMax, string where)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (buffer.Length < HeaderBytes)
            throw new S7TransportException(
                $"the bytes at {where} are not an S7 String: {buffer.Length} byte(s) were read, and the " +
                $"two-byte length header alone needs {HeaderBytes}.");

        int declaredMax = buffer[0];
        int current = buffer[1];

        if (current > declaredMax || current > expectedMax || current > buffer.Length - HeaderBytes)
            throw new S7TransportException(
                $"the bytes at {where} are not an S7 String: the length header reads max={declaredMax}, " +
                $"current={current} against a {expectedMax}-character read. Check the offset and the " +
                "declared length.");

        return Encoding.ASCII.GetString(buffer, HeaderBytes, current);
    }
}
