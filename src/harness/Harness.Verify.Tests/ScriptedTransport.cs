using Harness.Map;
using Harness.Wire;

namespace Harness.Verify.Tests;

/// <summary>
/// A mirror that answers FC03 out of an array. <b>No socket, no device, no NModbus.</b>
///
/// <para>It serves the WHOLE register file the map describes, and slices reads out of it, so a read at the
/// wrong address returns the wrong registers rather than throwing — which is the failure mode a real
/// device has, and the one a fixture that only answered the expected question could not reproduce.</para>
/// </summary>
internal sealed class ScriptedTransport : IRegisterTransport
{
    private readonly ushort[] _registers;

    internal ScriptedTransport(RegisterMap map, uint version, uint scanCounter = 1234,
        RegisterWordOrder order = RegisterWordOrder.HighWordFirst)
    {
        _registers = new ushort[map.TotalRegisters];

        var versionWords = RegisterWords.From32(version, order);
        _registers[map.Version.Register] = versionWords[0];
        _registers[map.Version.Register + 1] = versionWords[1];

        var scanWords = RegisterWords.From32(scanCounter, order);
        _registers[map.ScanCounter.Register] = scanWords[0];
        _registers[map.ScanCounter.Register + 1] = scanWords[1];
    }

    /// <summary>Every FC03 this transport served. The denominator: a run that read nothing must be visible as one.</summary>
    internal int Reads { get; private set; }

    /// <summary>
    /// Every FC16 this transport served. <b>Asserted zero by the behavioural tests</b>, which is a different
    /// and weaker claim than the IL walk's — this one says the run did not write, the walk says it cannot.
    /// </summary>
    internal int Writes { get; private set; }

    public ushort[] ReadHoldingRegisters(int startRegister, int count)
    {
        Reads++;

        if (startRegister < 0 || count < 1 || startRegister + count > _registers.Length)
            throw new WireException($"FC03 at {startRegister} for {count} register(s) is outside this scripted mirror's {_registers.Length}.");

        return _registers[startRegister..(startRegister + count)];
    }

    public void WriteHoldingRegisters(int startRegister, ushort[] values)
    {
        Writes++;
        values.CopyTo(_registers, startRegister);
    }

    public void Dispose() { }
}

/// <summary>
/// A connect factory that COUNTS. <i>Assert the observable consequence, not the exit code</i> — a
/// disconnected fence can still produce the right exit code by accident, and only the counter separates
/// "refused before the socket" from "refused after it".
/// </summary>
internal sealed class RecordingConnect
{
    private readonly Func<IRegisterTransport>? _open;

    internal RecordingConnect(Func<IRegisterTransport>? open = null) => _open = open;

    internal int Calls { get; private set; }

    internal Func<string, int, byte, IRegisterTransport> Factory => (_, _, _) =>
    {
        Calls++;
        return _open?.Invoke() ?? throw new IOException("this fixture has no device behind it.");
    };
}
