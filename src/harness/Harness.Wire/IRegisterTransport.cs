namespace Harness.Wire;

/// <summary>
/// The client's only view of the mirror: read a run of holding registers, write a run of holding
/// registers. Nothing else.
///
/// <para><b>Why a port rather than NModbus's own interface.</b> Two reasons, and neither is
/// testability alone. First, it is where the FC03/FC16 register limits and the retry decision are
/// enforced, and those are decisions this project made from measurements rather than settings a
/// library chose. Second, everything above it — the write/commit/poll sequence, the inert verify, the
/// settling window — is then exercised with no socket, which is what makes phase 2 cheap to check.</para>
///
/// <para><b>Register addresses are ints, not ushorts.</b> A map that overflows the protocol's 16-bit
/// address space is a design-time refusal with a reason, not a silent wrap at the call site.</para>
/// </summary>
public interface IRegisterTransport : IDisposable
{
    /// <summary>One FC03. Must return exactly <paramref name="count"/> registers or throw.</summary>
    ushort[] ReadHoldingRegisters(int startRegister, int count);

    /// <summary>One FC16.</summary>
    void WriteHoldingRegisters(int startRegister, ushort[] values);
}

/// <summary>A transaction that could not be performed, or whose answer was not the shape it must be.</summary>
public class WireException : Exception
{
    public WireException(string message) : base(message) { }

    public WireException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// 🔴 <b>The link to the device went away mid-transaction.</b>
///
/// <para><b>Its own type because the wave has to be able to catch THIS and nothing else.</b> Before it
/// existed, a dropped connection surfaced as a raw <see cref="System.Net.Sockets.SocketException"/> from
/// deep inside the Modbus library, escaped <c>WaveRun</c>, escaped <c>LoopRun.Execute</c>, and reached
/// the top of <c>harness-run</c> — which never reached its <c>Write(result)</c> call. Every index that
/// had already completed was discarded with it, and the run produced NO result package at all.</para>
///
/// <para><b>The alternative — catching <see cref="Exception"/> around the wave — is worse than the
/// bug.</b> It would swallow every programming error in the observation path and report it as a lost
/// link, which is precisely the "a green that examined nothing" shape this project keeps meeting. So the
/// transport, which is the only layer that knows what a transport failure IS, names it.</para>
///
/// <para><b>A protocol refusal is NOT this.</b> An exception-2 answer from <c>MB_SERVER</c> is the
/// server ANSWERING — it is how the declared area's width is measured — and it must keep surfacing as
/// itself.</para>
/// </summary>
public sealed class WireLinkLostException : WireException
{
    public WireLinkLostException(string message, Exception inner) : base(message, inner) { }
}
