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
public sealed class WireException : Exception
{
    public WireException(string message) : base(message) { }

    public WireException(string message, Exception inner) : base(message, inner) { }
}
