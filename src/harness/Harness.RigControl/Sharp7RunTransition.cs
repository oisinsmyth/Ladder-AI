using Harness.S7;
using Sharp7;

namespace Harness.RigControl;

/// <summary>
/// The real transport: a thin marshalling layer over Sharp7, and the only file in this project that
/// opens a socket.
///
/// <para><b>It is built exactly like <see cref="Sharp7Client"/> and for the same reason</b> — no
/// branching beyond an error check, no interpretation, no policy — because it cannot be unit-tested and
/// therefore has to be verifiable by eye. Every decision worth arguing about is above this line and has
/// tests.</para>
///
/// <para>🔴 <b>WHETHER THE RUN REQUEST WORKS ON THIS CPU IS NOT ESTABLISHED, IN EITHER DIRECTION.</b>
/// <c>PlcHotStart</c> sends the classic S7comm PI service <c>P_PROGRAM</c>, function <c>0x28</c>,
/// inside a JOB pdu (<c>32 01</c>) — read out of Sharp7 1.1.82's own <c>S7_HOT_START</c> telegram,
/// 2026-08-14. It has NEVER BEEN SENT, because the fence refuses first, and nothing here predicts
/// what it would do.</para>
///
/// <para>⚠️ <b>THIS PARAGRAPH PREVIOUSLY PREDICTED REFUSAL, AND THE PREDICTION WAS BUILT ON A STALE
/// RECORD. RETRACTED 2026-08-14.</b> It argued that job-class VARIABLE services were refused CPU-wide
/// with <c>0x00040000</c> — true when measured on 2026-08-12 — so the run request, being job-class,
/// sat on the refused side of that split. *** THAT FACT HAS SINCE CHANGED: PUT/GET WAS ENABLED ON THIS
/// CPU. *** Re-measured read-only on 2026-08-14, twice by two parties: <c>MBRead(0,1)</c> SUCCEEDS at
/// a full round trip (79 ms), and <c>DBRead(38,...)</c> fails with <c>0x00C00000</c> — <i>item</i> not
/// available, a BLOCK-SPECIFIC code (DB38 is optimized or absent), not the CPU-wide negative
/// acknowledgement. Job-class variable access is SERVED here today, so the argument for expecting a
/// refusal is gone.</para>
///
/// <para><b>But do not read that as "expected to work" — it moves this to a different KIND of unknown,
/// not to a positive.</b> Three things stand between "PUT/GET is on" and "this telegram will be
/// answered", and none of them has been tested: PUT/GET governs the VARIABLE services (Read/Write Var,
/// <c>0x04</c>/<c>0x05</c>), and <c>0x28</c> is a different function that happens to share the job
/// class; whether an S7-1200 implements the classic <c>P_PROGRAM</c> PI service AT ALL is unknown here
/// (TIA's own mode control for a 1200 does not go over classic S7comm); and a CPU may gate mode
/// control on its protection level independently of PUT/GET. *** AN UNTESTED TELEGRAM IS UNTESTED. ***</para>
///
/// <para>If it is ever sent and comes back refused, that is a RESULT and not a bug: compare the
/// elapsed time against the order-code round trip (~80–110 ms measured) to tell a CPU refusal from a
/// local rejection, and record it.</para>
///
/// <para><b>What is NOT reachable from here, deliberately:</b> <c>PlcStop</c>, <c>PlcColdStart</c>,
/// <c>DBWrite</c>, <c>MBWrite</c>, <c>WriteArea</c>, <c>Download</c>, <c>Delete</c>,
/// <c>PlcCopyRamToRom</c>, <c>SetPlcDateTime</c>. Sharp7 offers all of them on the same object this
/// class holds, so their absence is a property of the COMPILED ASSEMBLY rather than of this comment —
/// and <c>RigControlStructureTests</c> walks the IL to check it, with a denominator and a live positive
/// control.</para>
/// </summary>
public sealed class Sharp7RunTransition : IRunTransitionTransport
{
    private readonly S7Client _client = new();

    public bool Connected => _client.Connected;

    public S7Status Connect(string address, int rack, int slot, int connectTimeoutMs)
    {
        _client.ConnTimeout = connectTimeoutMs;
        return Status(_client.ConnectTo(address, rack, slot));
    }

    public void Disconnect() => _client.Disconnect();

    public S7Status ReadOrderCode(out string orderCode)
    {
        var info = new S7Client.S7OrderCode();
        var rc = _client.GetOrderCode(ref info);

        orderCode = rc == 0 ? (info.Code ?? string.Empty).Trim() : string.Empty;
        return Status(rc);
    }

    /// <summary>
    /// <c>PlcGetStatus</c>, decoded by <see cref="S7RunStateReading.Decode"/> — reused rather than
    /// re-derived, because that decoder carries a measured fact (this CPU answers <c>0x03</c> for STOP,
    /// absorbed by Sharp7's load-bearing catch-all) and a regression guard that a fresh copy would not.
    /// </summary>
    public S7Status ReadRunState(out S7RunStateReading runState)
    {
        var value = S7RunStateReading.NotRead;
        var rc = _client.PlcGetStatus(ref value);

        // A failed read is Unknown, never NotRunning: "the CPU did not answer RUN" and "the CPU could
        // not be asked" are different facts, and this tool's verdict turns on the difference.
        runState = rc == 0 ? S7RunStateReading.Decode(value) : S7RunStateReading.Unread;
        return Status(rc);
    }

    /// <summary>
    /// <c>PlcHotStart</c> — a warm restart, which resumes with retentive data intact.
    ///
    /// <para><b>Hot, not cold, and that is a choice.</b> <c>PlcColdStart</c> reinitialises retentive
    /// data. The purpose here is to return a CPU that <c>download-probe --disruptive</c> stopped to the
    /// state the vectors need, not to alter what is in it — and a cold start would silently discard
    /// data a run may depend on. <c>PlcColdStart</c> is not referenced anywhere in this assembly.</para>
    /// </summary>
    public S7Status RequestRun() => Status(_client.PlcHotStart());

    private S7Status Status(int rc) =>
        rc == 0 ? S7Status.Success : new S7Status(rc, _client.ErrorText(rc));

    public void Dispose() => _client.Disconnect();
}
