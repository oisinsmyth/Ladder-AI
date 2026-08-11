using System.Net;

namespace DeviceGuard;

/// <summary>
/// How a target address is canonicalized before anything compares two of them.
///
/// <para>This was a private method copied into both <see cref="DeviceAccessGuard"/> and
/// <see cref="DeviceWriteGuard"/> — with the write guard's copy carrying a comment saying it must
/// agree with the read guard's. Two copies that must agree are one copy that will eventually not, so
/// there is now one.</para>
///
/// <para>It is public because callers outside the guards need the same answer: a restore-point store
/// keyed on "the target" has to key on exactly the string the guard will ask it about, and computing
/// that independently is the same drift by another route.</para>
///
/// <para>The rule only ever TIGHTENS a match, never widens one — IPs are parsed so that trivially
/// different spellings of the same address compare equal, hostnames are lower-cased, whitespace is
/// trimmed. Nothing here turns one address into a range.</para>
/// </summary>
public static class DeviceAddress
{
    public static string Normalize(string address)
    {
        var trimmed = (address ?? string.Empty).Trim();
        return IPAddress.TryParse(trimmed, out var ip)
            ? ip.ToString()
            : trimmed.ToLowerInvariant();
    }
}
