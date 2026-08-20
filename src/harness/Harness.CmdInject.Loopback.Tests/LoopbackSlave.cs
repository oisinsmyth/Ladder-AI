using System.Net;
using System.Net.Sockets;
using NModbus;

namespace Harness.CmdInject.Loopback.Tests;

/// <summary>
/// A holding-register bank that <b>records the requests it was actually asked to serve.</b>
///
/// <para>This is the measuring instrument. Everything it records arrived through NModbus's own PDU
/// encode/decode over a socket, so a start address or a register value seen here is what went on the wire —
/// not what our formatter believed it would put there.</para>
/// </summary>
internal sealed class RecordedRegisters : IPointSource<ushort>
{
    private readonly Dictionary<int, ushort> _store = new();

    /// <summary>Every FC03 the server was asked to answer, in order.</summary>
    internal List<(int Start, int Count)> Reads { get; } = new();

    /// <summary>Every FC16 the server was asked to apply, in order, with the values as received.</summary>
    internal List<(int Start, ushort[] Values)> Writes { get; } = new();

    internal ushort this[int register]
    {
        get => _store.TryGetValue(register, out var value) ? value : (ushort)0;
        set => _store[register] = value;
    }

    public ushort[] ReadPoints(ushort startAddress, ushort numberOfPoints)
    {
        Reads.Add((startAddress, numberOfPoints));

        var answer = new ushort[numberOfPoints];
        for (var i = 0; i < numberOfPoints; i++)
            answer[i] = this[startAddress + i];
        return answer;
    }

    public void WritePoints(ushort startAddress, ushort[] points)
    {
        Writes.Add((startAddress, points.ToArray()));

        for (var i = 0; i < points.Length; i++)
            this[startAddress + i] = points[i];
    }
}

/// <summary>Discrete points nothing in this tool touches. Present because <see cref="ISlaveDataStore"/> requires all four.</summary>
internal sealed class UnusedDiscretes : IPointSource<bool>
{
    public bool[] ReadPoints(ushort startAddress, ushort numberOfPoints) => new bool[numberOfPoints];

    public void WritePoints(ushort startAddress, bool[] points) { }
}

/// <summary>The four point sources a Modbus slave exposes. Only the holding registers are real here.</summary>
internal sealed class LoopbackDataStore : ISlaveDataStore
{
    internal LoopbackDataStore(RecordedRegisters holding) => HoldingRegisters = holding;

    public IPointSource<bool> CoilDiscretes { get; } = new UnusedDiscretes();

    public IPointSource<bool> CoilInputs { get; } = new UnusedDiscretes();

    public IPointSource<ushort> HoldingRegisters { get; }

    public IPointSource<ushort> InputRegisters { get; } = new RecordedRegisters();
}

/// <summary>
/// A real Modbus TCP server, in this process, <b>bound to loopback and an ephemeral port.</b>
///
/// <para>Loopback so nothing is reachable off this machine; ephemeral so no fixed port can collide with a
/// real service — including the rig's own 503, which must never be what a test dials by accident.</para>
/// </summary>
internal sealed class LoopbackSlave : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serving;

    internal LoopbackSlave(byte unitId = 1)
    {
        Registers = new RecordedRegisters();

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        var factory = new ModbusFactory();
        var network = factory.CreateSlaveNetwork(_listener);
        network.AddSlave(factory.CreateSlave(unitId, new LoopbackDataStore(Registers)));

        _serving = network.ListenAsync(_stop.Token);
    }

    /// <summary>The port the listener was given. Read back, never assumed.</summary>
    internal int Port { get; }

    /// <summary>The register bank, and the record of every request served.</summary>
    internal RecordedRegisters Registers { get; }

    public void Dispose()
    {
        _stop.Cancel();

        try { _listener.Stop(); }
        catch (SocketException) { /* already down */ }

        try { _serving.Wait(TimeSpan.FromSeconds(2)); }
        catch (Exception) { /* a listener torn down mid-accept throws; that is the teardown, not a failure */ }

        _stop.Dispose();
    }
}
