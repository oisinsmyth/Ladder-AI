namespace Harness.MirrorRead;

/// <summary>
/// This binary's ONLY view of the mirror: read a run of holding registers. There is no write verb,
/// and there is deliberately no way to obtain the underlying transport back out.
///
/// <para><b>Why a narrower port than <c>Harness.Wire.IRegisterTransport</c>.</b> That interface
/// carries <c>WriteHoldingRegisters</c> beside the read, because the harness proper must write
/// vectors and raise start bools. This tool must not, and a handle that carries the capability is a
/// capability one line away. Narrowing it here means the write verb is not merely unused in this
/// binary — it is <b>not on any type this binary's own code holds</b>.</para>
///
/// <para><b>What this does NOT claim.</b> The adapter that implements this
/// (<see cref="ModbusRegisterSource"/>) constructs a <c>Harness.Wire.NModbusTransport</c>, and that
/// class does have a write. The narrowing removes the write from every call site above the adapter;
/// it does not delete it from the referenced assembly, and pretending otherwise would be exactly the
/// kind of declaration this project has learned not to trust. The claim that is actually checked is
/// the structural one — <i>no method in this assembly names a write member</i> — and it is checked by
/// a walk over the compiled assembly with a denominator and a live positive control, not by this
/// comment. See <c>MirrorReadStructureTests</c>.</para>
/// </summary>
public interface IRegisterSource : IDisposable
{
    /// <summary>One FC03. Returns exactly <paramref name="count"/> registers or throws.</summary>
    ushort[] Read(int startRegister, int count);
}

/// <summary>
/// The one place a connection is opened.
///
/// <para>It is an interface so that the fence's ordering is <b>observable</b> rather than asserted:
/// a test supplies a factory that records every call, and "the fence refused and no socket was
/// opened" becomes a fact about a sentinel instead of a claim about the source. That matters because
/// the failure this guards against — the fence being deleted, or moved below the connect — leaves an
/// exit code that a disconnected gate can still produce by accident.</para>
/// </summary>
public interface IRegisterSourceFactory
{
    /// <summary>Open a session. Called only after the fence has ALLOWED the target.</summary>
    IRegisterSource Open(string host, int port, byte unitId);
}
