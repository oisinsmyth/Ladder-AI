namespace Harness.CmdInject;

/// <summary>
/// This tool's ONLY way to put registers on the wire.
///
/// <para>🔴 <b>THE INTERFACE EXISTS SO THE WRITE HAS A SINGLE NAMED CHOKEPOINT.</b> Exactly one method in
/// the shipped assembly names <see cref="WriteRegisters"/> — <c>InjectionDispatch.ApplyEach</c> — and a
/// test walks the compiled IL and asserts that count is one. Every write this tool can perform, command,
/// enable and restore alike, goes through it, which is what makes the split write and the band containment
/// properties of the ASSEMBLY rather than of every future call site.</para>
///
/// <para><b>It is an interface, and not merely a class, so that "nothing was opened" is observable.</b>
/// The run logic never constructs a transport; it is handed a factory. A test supplies a recording double
/// and asserts <c>opens == 0</c> on every refusal path, which is a fact about a counter rather than an
/// exit code a disconnected gate could also produce. The one production implementation is
/// <see cref="ModbusInjectionTransport"/>.</para>
/// </summary>
public interface IInjectionTransport : IDisposable
{
    /// <summary>One Modbus write of a contiguous register span. The single write verb this tool has.</summary>
    void WriteRegisters(int startRegister, IReadOnlyList<ushort> values);

    /// <summary>One FC03 read, for the acknowledgement poll and the stamp check. Reading is not the hazard; writing is.</summary>
    ushort[] ReadRegisters(int startRegister, int count);
}

/// <summary>
/// The one place a session would be opened.
///
/// <para>An interface so the fence's ordering is <b>observable</b>: a test supplies a factory that records
/// every call, and "the fence refused and no socket was opened" becomes a fact about a counter rather than
/// an exit code a disconnected gate could also produce. The production implementation is
/// <see cref="ModbusInjectionTransportFactory"/>, named in exactly one place —
/// <c>Program.Main</c> — and nowhere below it.</para>
/// </summary>
public interface IInjectionTransportFactory
{
    /// <summary>Open a session. Called only after the fence has ALLOWED the target and arming is present.</summary>
    IInjectionTransport Open(string host, int port, byte unitId);
}
