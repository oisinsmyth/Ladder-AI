using System.Net.Sockets;
using NModbus;

namespace Harness.Wire;

/// <summary>
/// The real wire: NModbus over TCP.
///
/// <para><b>A library, and phase 1 deliberately did not use one.</b> The spike hammered multi-register
/// writes with raw sockets because its question was whether <c>MB_SERVER</c> applies one request's
/// registers within a single scan — and a library that silently retried a request, or split one into
/// two, could have been mistaken for the PLC tearing. That experiment is answered (3,340 writes, no
/// tear observed at 16 registers), and phase 2 is not it.</para>
///
/// <para><b>The policy is applied in the constructor and the applied values are readable back</b>
/// (<see cref="Policy"/>), because the setting that matters — retries — has a non-zero DEFAULT in the
/// library and a wrong value here is invisible until it produces a second T=0.</para>
///
/// <para>This class owns a socket and nothing above it does. It is also the ONE class in this assembly
/// that cannot be exercised without a device, which is why it holds no logic beyond bounds checks and
/// the policy: everything with a decision in it lives above <see cref="IRegisterTransport"/>.</para>
/// </summary>
public sealed class NModbusTransport : IRegisterTransport
{
    private readonly IModbusMaster _master;
    private readonly TcpClient? _ownedClient;
    private readonly byte _unitId;

    /// <summary>Wrap an already-created master. The policy is applied to its transport here.</summary>
    public NModbusTransport(IModbusMaster master, byte unitId = 1, ModbusPolicy? policy = null, TcpClient? ownedClient = null)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _unitId = unitId;
        _ownedClient = ownedClient;
        Policy = policy ?? ModbusPolicy.Default;

        if (Policy.Refusals.Count > 0)
        {
            throw new WireException(
                "the Modbus policy is not usable:" + Environment.NewLine + "  - " + string.Join(Environment.NewLine + "  - ", Policy.Refusals));
        }

        _master.Transport.Retries = Policy.Retries;
        _master.Transport.ReadTimeout = Policy.ReadTimeoutMs;
        _master.Transport.WriteTimeout = Policy.WriteTimeoutMs;
        _master.Transport.WaitToRetryMilliseconds = Policy.WaitToRetryMs;
    }

    /// <summary>The policy that was applied — read back rather than assumed.</summary>
    public ModbusPolicy Policy { get; }

    /// <summary>Open a connection to a Modbus TCP server. The only place in the harness that does.</summary>
    public static NModbusTransport Connect(string host, int port = 502, byte unitId = 1, ModbusPolicy? policy = null)
    {
        var effective = policy ?? ModbusPolicy.Default;
        var client = new TcpClient
        {
            ReceiveTimeout = effective.ReadTimeoutMs,
            SendTimeout = effective.WriteTimeoutMs,
        };

        try
        {
            client.Connect(host, port);
            return new NModbusTransport(new ModbusFactory().CreateMaster(client), unitId, effective, client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public ushort[] ReadHoldingRegisters(int startRegister, int count)
    {
        Bounds(startRegister, count, Harness.Map.ModbusLimits.MaxReadRegisters, "FC03 read");

        var registers = _master.ReadHoldingRegisters(_unitId, (ushort)startRegister, (ushort)count);

        // Empty is not clean: a short answer is a different answer, not a partial success.
        if (registers is null || registers.Length != count)
        {
            throw new WireException(
                $"FC03 at register {startRegister} asked for {count} register(s) and received {registers?.Length.ToString() ?? "no"} — a short read is a different answer, not a partial one.");
        }

        return registers;
    }

    public void WriteHoldingRegisters(int startRegister, ushort[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Bounds(startRegister, values.Length, Harness.Map.ModbusLimits.MaxWriteRegisters, "FC16 write");

        _master.WriteMultipleRegisters(_unitId, (ushort)startRegister, values);
    }

    private static void Bounds(int startRegister, int count, int limit, string what)
    {
        if (startRegister < 0 || startRegister > ushort.MaxValue)
            throw new WireException($"{what} starts at register {startRegister}, which is outside the protocol's 16-bit address space.");

        if (count <= 0)
            throw new WireException($"{what} of {count} register(s) is not a transaction. An empty read is not a clean read.");

        if (count > limit)
            throw new WireException($"{what} of {count} register(s) exceeds the {limit}-register protocol limit. Splitting it here would hide a map that was derived wrong; the map refuses this at derivation time.");

        if (startRegister + count - 1 > ushort.MaxValue)
            throw new WireException($"{what} at register {startRegister} for {count} register(s) runs past the 16-bit address space.");
    }

    public void Dispose()
    {
        _master.Dispose();
        _ownedClient?.Dispose();
    }
}
