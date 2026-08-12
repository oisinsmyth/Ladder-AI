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

    /// <summary>
    /// The PDU size the CPU agreed to during connect, in bytes — 0 if nothing was negotiated.
    ///
    /// <para>Read-only, and not on <see cref="IS7Client"/>: it describes the SESSION rather than the
    /// device, so nothing above the transport should branch on it. It is exposed because it is the one
    /// fact that tells a device-side refusal apart from Sharp7 computing a chunk size from a PDU it
    /// never got — both of which surface as a failed read on a connection that reported success.</para>
    /// </summary>
    public int PduSizeNegotiated => _client.PduSizeNegotiated;

    /// <summary>The PDU size asked for, for comparison with what was granted.</summary>
    public int PduSizeRequested => _client.PduSizeRequested;

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

    /// <summary>
    /// The CPU's run state, via <c>PlcGetStatus</c> — a status request, never a mode change.
    ///
    /// <para><b>Measured on the bench rig 2026-08-12 in both states:</b> RUN answers PDU byte 0x08 and
    /// arrives here as 8; STOP answers <b>PDU byte 0x03</b>, which is none of Sharp7's three named
    /// constants and so arrives as 4 through its CATCH-ALL arm. That catch-all is load-bearing — the
    /// decoder must not be tightened to {0,4,8}, or a stopped CPU reads as not-stopped on this rig.
    /// The reasoning lives with the decoder, in <see cref="S7RunStateReading.Decode"/>.</para>
    /// </summary>
    public S7Status ReadRunState(out S7RunStateReading runState)
    {
        // Sharp7 leaves the ref parameter alone when the request fails, so it starts at the "nothing
        // was read" sentinel rather than at a value that could be mistaken for an answer.
        var value = S7RunStateReading.NotRead;
        var rc = _client.PlcGetStatus(ref value);

        // A failed read is Unknown, never NotRunning. "The CPU did not answer RUN" and "the CPU could
        // not be asked" are different facts, and telling them apart is the whole reason this read
        // exists — flattening the second into the first would rebuild the ambiguity it removes.
        runState = rc == 0 ? S7RunStateReading.Decode(value) : S7RunStateReading.Unread;
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
