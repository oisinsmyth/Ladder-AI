using Harness.S7;

namespace Harness.S7.Tests;

/// <summary>
/// An <see cref="IS7Client"/> with no socket: a byte array per DB, a scripted identity, and enough
/// levers to reproduce the failures that matter.
///
/// <para>What it deliberately models, because these are the behaviours the transport's correctness
/// depends on and none of them can be provoked against a healthy rig:</para>
/// <list type="bullet">
/// <item>the session dropping, so a reconnect happens and identity is re-read;</item>
/// <item>the device at the address CHANGING between connections — the incident this whole identity
/// mechanism exists for;</item>
/// <item>a read or a write failing at the protocol level;</item>
/// <item>an identity read failing while the connection itself is fine.</item>
/// </list>
/// </summary>
public sealed class FakeS7Client : IS7Client
{
    private readonly Dictionary<int, byte[]> _blocks = new();

    public bool Connected { get; private set; }

    // ---- scripted device state -------------------------------------------------------------

    /// <summary>What the CPU reports as its order code. Change it between connections to model a
    /// different physical device answering the same address.</summary>
    public string OrderCode { get; set; } = "6ES7 214-1AG40-0XB0";

    public S7CpuInfo CpuInfo { get; set; } = new("CPU 1214C DC/DC/DC", "S C-K1U399102021", "PLC_1");

    /// <summary>Non-zero makes the order-code read fail.</summary>
    public int OrderCodeFailureCode { get; set; }

    /// <summary>
    /// What <c>PlcGetStatus</c> reports. This is Sharp7's ALREADY-MAPPED value, not the device's byte:
    /// the rig answers 0x03 in STOP and Sharp7 hands that on as 4 (see
    /// <see cref="S7RunStateReading.Decode"/>), so scripting a stopped CPU means 4, and scripting a
    /// device value nobody has seen means whatever Sharp7 would have collapsed it to.
    /// </summary>
    public int RunStateValue { get; set; } = S7RunStateReading.RunValue;

    /// <summary>Non-zero makes the run-state read fail — the CPU could not be asked.</summary>
    public int RunStateFailureCode { get; set; }

    public int ReadFailureCode { get; set; }
    public int WriteFailureCode { get; set; }
    public int ConnectFailureCode { get; set; }

    // ---- observations ----------------------------------------------------------------------

    public int ConnectCount { get; private set; }
    public int OrderCodeReadCount { get; private set; }
    public int RunStateReadCount { get; private set; }
    public List<string> BitWrites { get; } = new();
    public List<string> BlockWrites { get; } = new();

    /// <summary>Model the session dropping without anyone calling Disconnect.</summary>
    public void DropConnection() => Connected = false;

    /// <summary>Seed a DB's contents.</summary>
    public FakeS7Client WithBlock(int dbNumber, params byte[] bytes)
    {
        _blocks[dbNumber] = bytes;
        return this;
    }

    public byte[] Block(int dbNumber) => _blocks.TryGetValue(dbNumber, out var b) ? b : Array.Empty<byte>();

    // ---- IS7Client -------------------------------------------------------------------------

    public S7Status Connect(string address, int rack, int slot, int connectTimeoutMs)
    {
        ConnectCount++;
        if (ConnectFailureCode != 0) return new S7Status(ConnectFailureCode, "connection refused (fake)");

        Connected = true;
        return S7Status.Success;
    }

    public void Disconnect() => Connected = false;

    public S7Status ReadOrderCode(out string orderCode)
    {
        OrderCodeReadCount++;

        if (OrderCodeFailureCode != 0)
        {
            orderCode = string.Empty;
            return new S7Status(OrderCodeFailureCode, "order code unavailable (fake)");
        }

        orderCode = OrderCode;
        return S7Status.Success;
    }

    public S7Status ReadCpuInfo(out S7CpuInfo info)
    {
        info = CpuInfo;
        return S7Status.Success;
    }

    public S7Status ReadRunState(out S7RunStateReading runState)
    {
        RunStateReadCount++;

        if (RunStateFailureCode != 0)
        {
            runState = S7RunStateReading.Unread;
            return new S7Status(RunStateFailureCode, "run state unavailable (fake)");
        }

        runState = S7RunStateReading.Decode(RunStateValue);
        return S7Status.Success;
    }

    public S7Status ReadDataBlock(int dbNumber, int startByte, byte[] buffer)
    {
        if (!Connected) return new S7Status(-2, "not connected (fake)");
        if (ReadFailureCode != 0) return new S7Status(ReadFailureCode, "read failed (fake)");

        if (!_blocks.TryGetValue(dbNumber, out var block) || startByte + buffer.Length > block.Length)
            return new S7Status(-3, $"DB{dbNumber} does not extend to byte {startByte + buffer.Length - 1} (fake)");

        Array.Copy(block, startByte, buffer, 0, buffer.Length);
        return S7Status.Success;
    }

    public S7Status WriteDataBlock(int dbNumber, int startByte, byte[] buffer)
    {
        if (!Connected) return new S7Status(-2, "not connected (fake)");
        if (WriteFailureCode != 0) return new S7Status(WriteFailureCode, "write failed (fake)");

        if (!_blocks.TryGetValue(dbNumber, out var block) || startByte + buffer.Length > block.Length)
            return new S7Status(-3, $"DB{dbNumber} does not extend to byte {startByte + buffer.Length - 1} (fake)");

        Array.Copy(buffer, 0, block, startByte, buffer.Length);
        BlockWrites.Add($"DB{dbNumber}.DBB{startByte} = {Convert.ToHexString(buffer)}");
        return S7Status.Success;
    }

    public S7Status WriteBit(int dbNumber, int byteOffset, int bitOffset, bool value)
    {
        if (!Connected) return new S7Status(-2, "not connected (fake)");
        if (WriteFailureCode != 0) return new S7Status(WriteFailureCode, "write failed (fake)");

        if (!_blocks.TryGetValue(dbNumber, out var block) || byteOffset >= block.Length)
            return new S7Status(-3, $"DB{dbNumber} does not extend to byte {byteOffset} (fake)");

        if (value) block[byteOffset] |= (byte)(1 << bitOffset);
        else block[byteOffset] &= (byte)~(1 << bitOffset);

        BitWrites.Add($"DB{dbNumber}.DBX{byteOffset}.{bitOffset} = {value}");
        return S7Status.Success;
    }
}
