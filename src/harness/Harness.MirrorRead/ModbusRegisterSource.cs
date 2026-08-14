using Harness.Wire;

namespace Harness.MirrorRead;

/// <summary>
/// The real wire, exposed read-only.
///
/// <para><b>No new transport code.</b> <c>Harness.Wire.NModbusTransport</c> already owns the socket,
/// the FC03 bounds checks and <c>ModbusPolicy</c> — including the one setting that is load-bearing and
/// is not the library default (<c>Retries = 0</c>). Re-implementing any of that here would produce a
/// second, uncalibrated client whose timeouts and retry behaviour nobody had measured. This class adds
/// exactly one thing: a handle that has no write on it.</para>
///
/// <para>The transport is held as <see cref="IRegisterTransport"/> and never handed out. Nothing above
/// this file can reach the write, because nothing above this file is given anything that has one.</para>
/// </summary>
public sealed class ModbusRegisterSource : IRegisterSource
{
    private readonly IRegisterTransport _transport;

    private ModbusRegisterSource(IRegisterTransport transport) => _transport = transport;

    /// <summary>Open a Modbus TCP session and expose only its read half.</summary>
    public static IRegisterSource Open(string host, int port, byte unitId) =>
        new ModbusRegisterSource(NModbusTransport.Connect(host, port, unitId));

    public ushort[] Read(int startRegister, int count) =>
        _transport.ReadHoldingRegisters(startRegister, count);

    public void Dispose() => _transport.Dispose();
}

/// <summary>The production factory. The only implementation that opens a socket.</summary>
public sealed class ModbusRegisterSourceFactory : IRegisterSourceFactory
{
    public IRegisterSource Open(string host, int port, byte unitId) =>
        ModbusRegisterSource.Open(host, port, unitId);
}
