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

        var registers = LinkGuarded(
            () => _master.ReadHoldingRegisters(_unitId, (ushort)startRegister, (ushort)count),
            $"FC03 at register {startRegister} for {count} register(s)");

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

        LinkGuarded<object?>(
            () =>
            {
                _master.WriteMultipleRegisters(_unitId, (ushort)startRegister, values);
                return null;
            },
            $"FC16 at register {startRegister} for {values.Length} register(s)");
    }

    /// <summary>
    /// 🔴 <b>Turn a lost link into <see cref="WireLinkLostException"/>, and leave everything else alone.</b>
    ///
    /// <para>This is the ONLY layer that can tell the difference. Above it, a
    /// <see cref="SocketException"/> and a bug in the poll loop are both just exceptions, and a wave
    /// that caught both would report programming errors as network weather.</para>
    ///
    /// <para><b><see cref="SlaveException"/> is deliberately NOT caught.</b> A Modbus exception response
    /// is the server ANSWERING: exception 2 on the first register past the declared area is how
    /// <c>MB_SERVER</c>'s width is measured from the far side. Rewriting that as a lost link would
    /// destroy a measurement and invent a fault.</para>
    /// </summary>
    private static T LinkGuarded<T>(Func<T> transaction, string what)
    {
        try
        {
            return transaction();
        }
        catch (SlaveException)
        {
            // The server answered. Not our business.
            throw;
        }
        catch (Exception error) when (
            error is SocketException
                or IOException
                or TimeoutException
                or ObjectDisposedException
                or InvalidOperationException)
        {
            // InvalidOperationException is in the list because NModbus raises it for a master whose
            // stream has already been torn down — the second transaction after a drop, rather than the
            // first. Excluding it would let the failure that FOLLOWS the one we handled escape raw.
            throw new WireLinkLostException(
                $"{what} could not be completed: the link to the device is gone ({error.GetType().Name}: {error.Message}). "
                + "Everything observed before this point is kept and reported; nothing after it was attempted.",
                error);
        }
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
