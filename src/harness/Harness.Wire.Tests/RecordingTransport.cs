using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>One transaction the client issued, in order.</summary>
public sealed record Transaction(bool IsWrite, int StartRegister, int Count, ushort[] Values);

/// <summary>
/// A register file with a transaction log — enough to exercise every decision above the wire and
/// nothing more.
///
/// <para>It is deliberately NOT a PLC: it runs no logic and only advances the scan counter because the
/// client's waits are keyed on it. The tests that need a program running behind the registers use the
/// skeleton's interpreter instead, which executes the generated IR.</para>
/// </summary>
public sealed class RecordingTransport : IRegisterTransport
{
    private readonly RegisterMap _map;
    private readonly ushort[] _registers;

    public RecordingTransport(RegisterMap map, BuildStamp? version = null)
    {
        _map = map;
        _registers = new ushort[map.TotalRegisters];

        if (version is { } stamp)
            SetVersion(stamp.Value);
    }

    public List<Transaction> Log { get; } = new();

    /// <summary>Scan-counter advance applied on every transaction. Zero models a stopped PLC.</summary>
    public long ScansPerTransaction { get; set; } = 4;

    /// <summary>Invoked after each transaction, so a test can move the register file the way a program would.</summary>
    public Action<RecordingTransport>? OnTransaction { get; set; }

    public long ScanCounter { get; private set; }

    public void SetVersion(uint value)
    {
        var words = RegisterWords.From32(value, RegisterWordOrder.HighWordFirst);
        _registers[_map.Version.Register] = words[0];
        _registers[_map.Version.Register + 1] = words[1];
    }

    public void SetResult(int slotIndex, int registerInSlot, ushort value) =>
        _registers[_map.Slots[slotIndex].Result.Register + registerInSlot] = value;

    public ushort GetResult(int slotIndex, int registerInSlot) =>
        _registers[_map.Slots[slotIndex].Result.Register + registerInSlot];

    public ushort GetVector(int slotIndex, int registerInSlot) =>
        _registers[_map.Slots[slotIndex].Vector.Register + registerInSlot];

    public ushort[] StartBoolRegisters =>
        _registers[_map.StartBools.Register..(_map.StartBools.Register + _map.StartBools.Length)];

    public ushort[] ReadHoldingRegisters(int startRegister, int count)
    {
        Advance();
        Log.Add(new Transaction(false, startRegister, count, Array.Empty<ushort>()));
        return _registers[startRegister..(startRegister + count)];
    }

    public void WriteHoldingRegisters(int startRegister, ushort[] values)
    {
        Advance();
        Log.Add(new Transaction(true, startRegister, values.Length, (ushort[])values.Clone()));
        Array.Copy(values, 0, _registers, startRegister, values.Length);
    }

    private void Advance()
    {
        ScanCounter += ScansPerTransaction;
        var words = RegisterWords.From32(unchecked((uint)ScanCounter), RegisterWordOrder.HighWordFirst);
        _registers[_map.ScanCounter.Register] = words[0];
        _registers[_map.ScanCounter.Register + 1] = words[1];
        OnTransaction?.Invoke(this);
    }

    public void Dispose() { }
}
