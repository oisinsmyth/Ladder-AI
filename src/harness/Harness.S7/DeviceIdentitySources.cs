using System.Text;
using DeviceGuard;

namespace Harness.S7;

/// <summary>
/// One way of asking a device who it is.
///
/// <para><b>Why this is a list and not a method.</b> Today the only identifier readable off the rig
/// over S7comm is the order code, which identifies a MODEL — every 6ES7 214-1AG40-0XB0 on earth
/// reports the same string. That is enough to catch "the tunnel moved and now a different model
/// answers", and not enough to catch "a different unit of the same model answers". A rig marker DB
/// carrying a unique identifier is being added to close that, and there may be a third source after
/// it (SZL 0x001C exposes a serial number field, whether or not this CPU fills it in). So identity is
/// composed from independent sources rather than read by one method, and adding the marker source is
/// a line in the composition root rather than a change to the transport.</para>
///
/// <para>A source that cannot read THROWS. It must never return an empty identity, because an
/// identity missing a field the allowlist declares is treated by
/// <see cref="DeviceIdentity.Compare"/> as a mismatch — correct, but it would report the wrong cause,
/// blaming the device for what was a failed read.</para>
/// </summary>
public interface IDeviceIdentitySource
{
    /// <summary>Short name, used in failure messages so the failing source is obvious.</summary>
    string Name { get; }

    /// <summary>Read this source's contribution. Throws <see cref="S7TransportException"/> on failure.</summary>
    DeviceIdentity Read(IS7Client client);
}

/// <summary>
/// The CPU's order/article number. Available on every S7-1200 over classic S7comm, and proven against
/// the rig.
///
/// <para><b>What it is worth.</b> It distinguishes models, not units. On its own it satisfies the
/// write fence only in the weak sense that the allowlist entry can declare an order number and have
/// it verified; two identical CPUs at the same address remain indistinguishable. Say so plainly
/// rather than let the presence of an identity check imply more than it delivers.</para>
/// </summary>
public sealed class OrderCodeIdentitySource : IDeviceIdentitySource
{
    public string Name => "order code";

    public DeviceIdentity Read(IS7Client client)
    {
        var status = client.ReadOrderCode(out var code);
        if (!status.Ok)
            throw new S7TransportException($"could not read the CPU order code: {status}.");

        if (string.IsNullOrWhiteSpace(code))
            throw new S7TransportException(
                "the CPU reported an empty order code. An empty identifier is treated as a failed " +
                "read, not as a device with no order code.");

        return new DeviceIdentity(OrderNumber: code.Trim());
    }
}

/// <summary>How the marker string is laid out in the DB.</summary>
public enum MarkerEncoding
{
    /// <summary>A TIA <c>String[n]</c>: byte 0 is the declared maximum, byte 1 the current length,
    /// then the characters.</summary>
    S7String,

    /// <summary>A fixed-length <c>Array[0..n-1] of Char</c> — no header, trailing NULs or spaces trimmed.</summary>
    FixedChars,
}

/// <summary>
/// A unique identifier the rig itself carries, read out of a marker DB.
///
/// <para><b>What problem it solves.</b> The order code identifies a model; this identifies a unit. It
/// is what makes the write fence's identity gate mean "this exact box" rather than "a box of this
/// type", and therefore what makes an allowlist entry safe to leave in place while tunnels come and
/// go.</para>
///
/// <para><b>Provisional.</b> As of 2026-08-11 the marker DB is being created in parallel with this
/// code and its DB number, offset and encoding are NOT settled, which is why all three are
/// constructor arguments with no defaults. It maps to <see cref="DeviceIdentity.SerialNumber"/>
/// because that is the allowlist field meaning "unique to this unit"; if the marker ever needs its
/// own field, that is a change in DeviceGuard, not here.</para>
/// </summary>
public sealed class MarkerDbIdentitySource : IDeviceIdentitySource
{
    private readonly int _dbNumber;
    private readonly int _byteOffset;
    private readonly int _maxLength;
    private readonly MarkerEncoding _encoding;

    public MarkerDbIdentitySource(int dbNumber, int byteOffset, int maxLength,
        MarkerEncoding encoding = MarkerEncoding.S7String)
    {
        if (dbNumber <= 0) throw new S7ConfigurationException("marker DB number must be positive.");
        if (byteOffset < 0) throw new S7ConfigurationException("marker byte offset must not be negative.");
        if (maxLength is <= 0 or > 254)
            throw new S7ConfigurationException("marker length must be 1–254 (an S7 String holds at most 254).");

        _dbNumber = dbNumber;
        _byteOffset = byteOffset;
        _maxLength = maxLength;
        _encoding = encoding;
    }

    public string Name => $"marker DB{_dbNumber}.{_byteOffset}";

    public DeviceIdentity Read(IS7Client client)
    {
        var headerBytes = _encoding == MarkerEncoding.S7String ? 2 : 0;
        var buffer = new byte[headerBytes + _maxLength];

        var status = client.ReadDataBlock(_dbNumber, _byteOffset, buffer);
        if (!status.Ok)
            throw new S7TransportException(
                $"could not read the rig marker at DB{_dbNumber}.DBB{_byteOffset}: {status}. " +
                "Check the DB exists, has 'Optimized block access' turned OFF, and that PUT/GET " +
                "communication is permitted in the CPU's protection settings.");

        var text = _encoding == MarkerEncoding.S7String
            ? ReadS7String(buffer)
            : Encoding.ASCII.GetString(buffer).TrimEnd('\0', ' ');

        if (string.IsNullOrWhiteSpace(text))
            throw new S7TransportException(
                $"the rig marker at DB{_dbNumber}.DBB{_byteOffset} is empty. An empty marker is a " +
                "failed identification, not an unidentified device — refusing rather than letting the " +
                "identity check pass on a blank.");

        return new DeviceIdentity(SerialNumber: text.Trim());
    }

    private string ReadS7String(byte[] buffer)
    {
        // Byte 0 is the DECLARED maximum and byte 1 the current length. A current length larger than
        // either the declared maximum or what we actually read means the bytes are not a String at
        // all — almost always a wrong offset. Reading on would produce a plausible-looking identifier
        // out of unrelated process data, which would then be compared against the allowlist, so this
        // has to be a hard failure.
        int declaredMax = buffer[0];
        int current = buffer[1];

        if (current > declaredMax || current > _maxLength)
            throw new S7TransportException(
                $"the bytes at DB{_dbNumber}.DBB{_byteOffset} are not an S7 String: the length header " +
                $"reads max={declaredMax}, current={current} against a {_maxLength}-character read. " +
                "Check the marker's offset and declared length.");

        return Encoding.ASCII.GetString(buffer, 2, current);
    }
}

/// <summary>
/// The CPU's own serial number, from SZL 0x001C.
///
/// <para><b>Not wired in by default, on purpose.</b> Sharp7 exposes this (<c>GetCpuInfo</c>) and it
/// would be a better unit-level identifier than a marker DB, because nothing in the user program can
/// change it. But it is UNVERIFIED on the target CPU as of 2026-08-11 — S7-1200 firmware is known to
/// answer some SZL requests with empty fields — and this project's own brief records the order code
/// as the only identifier currently readable. So it exists, ready to try, and the composition root
/// has to opt in. If it works, it may retire the marker DB entirely.</para>
/// </summary>
public sealed class CpuInfoIdentitySource : IDeviceIdentitySource
{
    public string Name => "CPU info (SZL 0x001C)";

    public DeviceIdentity Read(IS7Client client)
    {
        var status = client.ReadCpuInfo(out var info);
        if (!status.Ok)
            throw new S7TransportException($"could not read CPU info: {status}.");

        if (string.IsNullOrWhiteSpace(info.SerialNumber))
            throw new S7TransportException(
                "the CPU answered the SZL 0x001C request but left the serial number blank. Treated as " +
                "a failed read: a blank serial must not be compared against the allowlist.");

        return new DeviceIdentity(SerialNumber: info.SerialNumber.Trim());
    }
}

/// <summary>
/// Combines the sources into the single <see cref="DeviceIdentity"/> the write fence compares against.
/// </summary>
public static class DeviceIdentityReader
{
    /// <summary>
    /// Read every source and merge. Fails closed three ways, each of which would otherwise produce an
    /// identity that looks verified and is not:
    ///
    /// <list type="bullet">
    /// <item>no sources at all — a configuration error, not an empty identity;</item>
    /// <item>any source failing — a partial identity would still be compared, and would be reported as
    /// the device being wrong rather than as the read having failed;</item>
    /// <item>two sources disagreeing on the same field — that is either a wrong marker offset or two
    /// devices in one conversation, and neither is something to average over.</item>
    /// </list>
    /// </summary>
    public static DeviceIdentity Read(IS7Client client, IReadOnlyList<IDeviceIdentitySource> sources)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (sources is null || sources.Count == 0)
            throw new S7ConfigurationException(
                "no device identity sources are configured, so nothing would be read and the device " +
                "could not be identified. Configure at least an OrderCodeIdentitySource.");

        string? order = null, serial = null, mac = null;

        foreach (var source in sources)
        {
            DeviceIdentity part;
            try
            {
                part = source.Read(client);
            }
            catch (S7TransportException ex)
            {
                throw new S7TransportException(
                    $"device identity could not be established: source '{source.Name}' failed. " +
                    $"{ex.Message}", ex);
            }

            order = Merge(source.Name, "order number", order, part.OrderNumber);
            serial = Merge(source.Name, "serial number", serial, part.SerialNumber);
            mac = Merge(source.Name, "MAC", mac, part.MacAddress);
        }

        var identity = new DeviceIdentity(order, serial, mac);

        if (!identity.HasAnyIdentifier)
            throw new S7TransportException(
                "every identity source reported success but none produced an identifier. Refusing to " +
                "treat that as an identified device.");

        return identity;
    }

    private static string? Merge(string sourceName, string field, string? sofar, string? incoming)
    {
        if (string.IsNullOrWhiteSpace(incoming)) return sofar;
        if (string.IsNullOrWhiteSpace(sofar)) return incoming.Trim();

        if (!string.Equals(sofar.Trim(), incoming.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new S7TransportException(
                $"identity sources contradict each other on the {field}: '{sofar.Trim()}' already read, " +
                $"but '{sourceName}' reports '{incoming.Trim()}'. Refusing — one of the two is reading " +
                "the wrong thing, and guessing which would be guessing about device identity.");

        return sofar;
    }
}
