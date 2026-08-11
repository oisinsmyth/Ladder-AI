using Sharp7;

namespace Harness.S7;

/// <summary>
/// The real <see cref="IS7Client"/>: a thin marshalling layer over Sharp7.
///
/// <para><b>This is the only file in the project that cannot be unit-tested</b>, because the thing it
/// wraps opens a socket. Everything about it is therefore arranged so that a reader can verify it by
/// eye: no branching beyond an error check, no interpretation of results, no policy. Every decision
/// worth arguing about — which tag maps where, whether a write is allowed, what counts as an
/// identity, what a restore point must cover — lives above this line and has tests.</para>
///
/// <para><b>Sharp7's error convention.</b> Every call returns an <c>int</c>: zero is success and
/// anything else indexes an error table that only Sharp7 can read. So the return code is turned into
/// an <see cref="S7Status"/> here, carrying both the code and <c>ErrorText</c>'s prose, and no caller
/// ever needs Sharp7 to explain a failure.</para>
///
/// <para><b>Bit writes use a bit-length write, not read-modify-write</b> — see
/// <see cref="IS7Client.WriteBit"/> for why that distinction is not a micro-optimisation. When the
/// word length is Bit, S7 expects the start address in BITS rather than bytes, which is the one
/// non-obvious line in this file.</para>
/// </summary>
public sealed class Sharp7Client : IS7Client, IDisposable
{
    private readonly S7Client _client = new();

    public bool Connected => _client.Connected;

    public S7Status Connect(string address, int rack, int slot, int connectTimeoutMs)
    {
        _client.ConnTimeout = connectTimeoutMs;
        return Status(_client.ConnectTo(address, rack, slot));
    }

    public void Disconnect() => _client.Disconnect();

    public S7Status ReadOrderCode(out string orderCode)
    {
        var info = new S7Client.S7OrderCode();
        var rc = _client.GetOrderCode(ref info);

        orderCode = rc == 0 ? (info.Code ?? string.Empty).Trim() : string.Empty;
        return Status(rc);
    }

    public S7Status ReadCpuInfo(out S7CpuInfo info)
    {
        var raw = new S7Client.S7CpuInfo();
        var rc = _client.GetCpuInfo(ref raw);

        info = rc == 0
            ? new S7CpuInfo(
                (raw.ModuleTypeName ?? string.Empty).Trim(),
                (raw.SerialNumber ?? string.Empty).Trim(),
                (raw.ModuleName ?? string.Empty).Trim())
            : new S7CpuInfo();

        return Status(rc);
    }

    public S7Status ReadDataBlock(int dbNumber, int startByte, byte[] buffer) =>
        Status(_client.DBRead(dbNumber, startByte, buffer.Length, buffer));

    public S7Status WriteDataBlock(int dbNumber, int startByte, byte[] buffer) =>
        Status(_client.DBWrite(dbNumber, startByte, buffer.Length, buffer));

    public S7Status WriteBit(int dbNumber, int byteOffset, int bitOffset, bool value)
    {
        // With S7WordLength.Bit the "start" argument is a BIT address, not a byte address.
        var bitAddress = (byteOffset * 8) + bitOffset;
        var payload = new[] { value ? (byte)1 : (byte)0 };

        return Status(_client.WriteArea(S7Area.DB, dbNumber, bitAddress, 1, S7WordLength.Bit, payload));
    }

    private S7Status Status(int rc) =>
        rc == 0 ? S7Status.Success : new S7Status(rc, _client.ErrorText(rc));

    public void Dispose() => _client.Disconnect();
}
