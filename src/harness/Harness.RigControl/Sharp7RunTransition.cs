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
/// <para>🔴 <b>WHETHER THE RUN REQUEST WORKS ON THIS CPU IS NOT ESTABLISHED, AND THE ANALYSIS PREDICTS
/// IT DOES NOT.</b> <c>PlcHotStart</c> sends the classic S7comm PI service <c>P_PROGRAM</c>, function
/// <c>0x28</c>, inside a JOB pdu (<c>32 01</c>) — read out of Sharp7 1.1.82's own
/// <c>S7_HOT_START</c> telegram, 2026-08-14. Measured on this rig on 2026-08-12, in BOTH RUN and STOP:
/// job-class VARIABLE services (<c>DBRead</c>, <c>MBRead</c>) are refused CPU-wide with
/// <c>0x00040000</c> — a well-formed reply too short to be a read response, i.e. a negative
/// acknowledgement from the CPU — while USERDATA/SZL requests (<c>GetOrderCode</c>,
/// <c>PlcGetStatus</c>) are served throughout. *** THE RUN REQUEST SITS ON THE REFUSED SIDE OF THAT
/// SPLIT, WHICH HAS NOW DECIDED FOUR THINGS ON THIS DEVICE. *** It has never been sent, because the
/// fence refuses first. If it is ever sent and comes back refused, that is a RESULT and not a bug:
/// compare the elapsed time against the order-code round trip to tell a CPU refusal from a local
/// rejection, and record it.</para>
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
