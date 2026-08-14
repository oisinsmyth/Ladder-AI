using NModbus;

namespace Harness.MirrorRead.Tests;

/// <summary>
/// A Modbus server that exists only in this process: it holds N registers, answers reads inside them
/// and returns an EXCEPTION RESPONSE outside them — which is what a real <c>MB_SERVER</c> does with an
/// address past <c>MB_HOLD_REG</c>.
///
/// <para>It can also be told to say NOTHING for named registers, because the difference between a
/// refusal and a silence is the whole reason the boundary probe is trustworthy, and a fixture that
/// could only produce refusals would leave the other branch untested.</para>
/// </summary>
internal sealed class ScriptedSource : IRegisterSource
{
    private readonly ushort[] _registers;
    private readonly HashSet<int> _silent;
    private readonly uint _scanPerRead;

    /// <param name="available">How many registers this server exposes — its MB_HOLD_REG width.</param>
    /// <param name="stamp">The build stamp published in registers 0..1, high-word-first.</param>
    /// <param name="scanPerRead">How much the scan counter (registers 2..3) advances per read.</param>
    /// <param name="silent">Start registers for which this server answers nothing at all.</param>
    internal ScriptedSource(int available, uint stamp = 0xF52ECEAD, uint scanPerRead = 7, params int[] silent)
    {
        _registers = new ushort[available];
        _scanPerRead = scanPerRead;
        _silent = new HashSet<int>(silent);

        if (available > 1)
        {
            _registers[0] = (ushort)(stamp >> 16);
            _registers[1] = (ushort)(stamp & 0xFFFF);
        }
    }

    internal int Reads { get; private set; }

    internal bool Disposed { get; private set; }

    /// <summary>Set one register's contents, so a test can put a value where it expects to read one.</summary>
    internal void Poke(int register, ushort value) => _registers[register] = value;

    public ushort[] Read(int startRegister, int count)
    {
        Reads++;

        if (_silent.Contains(startRegister))
            throw new TimeoutException($"scripted: the server said nothing about register {startRegister}.");

        if (startRegister < 0 || startRegister + count > _registers.Length)
        {
            throw new SlaveException(
                $"scripted: Function Code: 3, Exception Code: 2 - Illegal Data Address " +
                $"(asked for {count} at {startRegister}; this server holds 0..{_registers.Length - 1}).");
        }

        var answer = _registers[startRegister..(startRegister + count)];
        Advance();
        return answer;
    }

    /// <summary>The counter moves because the CPU is scanning, not because anybody read it — but a
    /// fixture has to move it somewhere, and per-read is the only clock a test has.</summary>
    private void Advance()
    {
        if (_scanPerRead == 0 || _registers.Length < 4) return;

        var scan = ((uint)_registers[2] << 16) | _registers[3];
        scan = unchecked(scan + _scanPerRead);
        _registers[2] = (ushort)(scan >> 16);
        _registers[3] = (ushort)(scan & 0xFFFF);
    }

    public void Dispose() => Disposed = true;
}

/// <summary>
/// The sentinel that makes the fence's ORDERING observable.
///
/// <para>A test asserting an exit code proves the run ended a particular way; it does not prove no
/// socket was opened, because a disconnected gate can produce the same code by accident. This counts
/// the one call that would open one.</para>
/// </summary>
internal sealed class RecordingFactory : IRegisterSourceFactory
{
    private readonly Func<IRegisterSource> _make;

    internal RecordingFactory(Func<IRegisterSource> make) => _make = make;

    internal int Opens { get; private set; }

    internal string? LastHost { get; private set; }

    internal int LastPort { get; private set; }

    public IRegisterSource Open(string host, int port, byte unitId)
    {
        Opens++;
        LastHost = host;
        LastPort = port;
        return _make();
    }
}

/// <summary>A factory that cannot open anything — for the connect-failure path.</summary>
internal sealed class FailingFactory : IRegisterSourceFactory
{
    internal int Opens { get; private set; }

    public IRegisterSource Open(string host, int port, byte unitId)
    {
        Opens++;
        throw new IOException("scripted: connection refused.");
    }
}
