using Harness.Wire;

namespace Harness.CmdInject;

/// <summary>
/// 🔴 <b>THE ONE PLACE IN THIS TOOL THAT OPENS A SOCKET — AND IT ADDS NO WIRE CODE OF ITS OWN.</b>
///
/// <para><c>Harness.Wire.NModbusTransport</c> already owns the socket, the FC03/FC16 bounds checks and
/// the calibrated <see cref="ModbusPolicy"/>. Re-implementing any of that here would produce a second,
/// uncalibrated client whose timeouts and retry behaviour nobody had measured — and one of those
/// settings is load-bearing and is <b>not</b> the library default: <c>Retries = 0</c>. NModbus defaults
/// it to three, and a retry after a timeout may duplicate a request whose original still landed. On a
/// command injection that is a <i>second command executed</i>, indistinguishable at this end from the
/// first having been lost. The policy is passed explicitly rather than left to the transport's own
/// default, and <see cref="Policy"/> reads back what was actually applied.</para>
///
/// <para><b>This class holds no decisions.</b> It is the one class here that cannot be exercised without
/// a device, so everything with a judgement in it lives above <see cref="IInjectionTransport"/> and is
/// tested against a double. What is checked structurally, not declared: exactly one method in this
/// assembly names a write member of the wire stack (<see cref="WriteRegisters"/>, below), and exactly
/// one names <see cref="NModbusTransport.Connect"/> (<see cref="Open"/>, below).</para>
/// </summary>
public sealed class ModbusInjectionTransport : IInjectionTransport
{
    private readonly IRegisterTransport _transport;

    private ModbusInjectionTransport(IRegisterTransport transport, ModbusPolicy policy)
    {
        _transport = transport;
        Policy = policy;
    }

    /// <summary>The policy that was applied to the session, read back rather than assumed.</summary>
    public ModbusPolicy Policy { get; }

    /// <summary>
    /// Open a Modbus TCP session. <b>The only construction of <see cref="NModbusTransport"/> in this
    /// binary</b>, so there is exactly one place the retry policy could be got wrong.
    /// </summary>
    public static ModbusInjectionTransport Open(string host, int port, byte unitId)
    {
        var policy = ModbusPolicy.Default;
        var wire = NModbusTransport.Connect(host, port, unitId, policy);
        return new ModbusInjectionTransport(wire, wire.Policy);
    }

    /// <summary>One FC16. The single write this tool has, reached only through <see cref="InjectionDispatch"/>.</summary>
    public void WriteRegisters(int startRegister, IReadOnlyList<ushort> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _transport.WriteHoldingRegisters(startRegister, values.ToArray());
    }

    /// <summary>One FC03 — the stamp check, the restore point, and every acknowledgement poll.</summary>
    public ushort[] ReadRegisters(int startRegister, int count) =>
        _transport.ReadHoldingRegisters(startRegister, count);

    public void Dispose() => _transport.Dispose();
}

/// <summary>
/// The production factory — <b>the composition root's only route to a socket.</b>
///
/// <para>It exists as a separate type for the reason <c>Harness.MirrorRead</c>'s does: the run logic
/// takes an <see cref="IInjectionTransportFactory"/> and never constructs one, so a test can supply a
/// recording double and assert <c>opens == 0</c> on every refusal path. "The fence held" is then a fact
/// about a counter rather than an exit code a disconnected gate could also produce.</para>
/// </summary>
public sealed class ModbusInjectionTransportFactory : IInjectionTransportFactory
{
    public IInjectionTransport Open(string host, int port, byte unitId) =>
        ModbusInjectionTransport.Open(host, port, unitId);
}
