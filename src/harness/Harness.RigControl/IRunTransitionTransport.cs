using Harness.S7;

namespace Harness.RigControl;

/// <summary>
/// Everything <c>rig-control</c> is allowed to do to a device — and, far more importantly, everything
/// it is not.
///
/// <para><b>Why this is not <see cref="IS7Client"/>, and must never become it.</b>
/// <see cref="IS7Client"/> is deliberately a REDUCTION of Sharp7's surface: it carries no
/// <c>PlcStop</c>, no <c>PlcHotStart</c>, no <c>Download</c>, so the conformance harness cannot change
/// a CPU's mode however a caller behaves. That property is load-bearing and this project should not
/// lose it to add one verb. So the RUN transition lives on a SEPARATE interface, in a SEPARATE
/// assembly, behind a SEPARATE fence — and <see cref="IS7Client"/> is left exactly as it was.</para>
///
/// <para><b>Why it does not simply extend <see cref="IS7Client"/> either.</b> That would have been
/// convenient (identity reads, run-state reads and the tag map all come free) and it would have handed
/// this binary <c>WriteDataBlock</c> and <c>WriteBit</c> as a side effect. A tool that may start a CPU
/// should not also be able to write its memory: the two grants have different consequences and there
/// is no reason for one to imply the other. Everything needed is restated here, and nothing else is.</para>
///
/// <para><b>There is no stop.</b> Not omitted pending work — REFUSED. The owner asked for the RUN
/// transition because <c>download-probe --disruptive</c> leaves the CPU stopped and the conformance
/// vectors cannot run against a stopped CPU. Nothing asked for a stop, and a capability built because
/// it is symmetrical is a capability nobody weighed. *** THE CONSEQUENCE IS REAL AND IS PRINTED ON
/// EVERY RUN: this tool can start a CPU and cannot undo it. *** Whoever runs it must be able to stop
/// the CPU by other means — in practice, TIA Portal.</para>
/// </summary>
public interface IRunTransitionTransport : IDisposable
{
    /// <summary>Whether the transport currently believes it holds a session.</summary>
    bool Connected { get; }

    S7Status Connect(string address, int rack, int slot, int connectTimeoutMs);

    /// <summary>Idempotent — disconnecting an already-disconnected transport is not an error.</summary>
    void Disconnect();

    /// <summary>
    /// The CPU's article/order number, e.g. <c>6ES7 214-1AG40-0XB0</c>. Read over SZL, which is the one
    /// request class measured to be served by this rig in both RUN and STOP.
    ///
    /// <para>This is here because an ADDRESS IS NOT AN IDENTITY. On this project's network
    /// <c>10.10.10.10</c> is the standard PLC address at multiple deployment sites and which physical
    /// controller answers depends on which tunnel is up — observed changing mid-session. Starting the
    /// wrong CPU is the incident this read exists to prevent.</para>
    /// </summary>
    S7Status ReadOrderCode(out string orderCode);

    /// <summary>
    /// Whether the CPU is running. READ-ONLY: asks for a status, cannot set one.
    ///
    /// <para>Used twice per run and for two different purposes: BEFORE, so a CPU that is already
    /// running is left alone rather than asked to start; and AFTER, as the read-back that decides
    /// whether the request actually did anything. <see cref="S7RunStateReading"/> and its decoder are
    /// reused from <c>Harness.S7</c> rather than re-derived — that decoder carries a measured fact
    /// (this CPU answers <c>0x03</c> for STOP, which Sharp7's catch-all absorbs) and a regression guard
    /// that a fresh copy would not.</para>
    /// </summary>
    S7Status ReadRunState(out S7RunStateReading runState);

    /// <summary>
    /// Ask the CPU to go to RUN. THE ONE WRITE THIS BINARY CAN PERFORM.
    ///
    /// <para>The status says whether the CPU was asked and what it answered. It says NOTHING about
    /// whether the CPU is now running — that is <see cref="ReadRunState"/>'s question, asked
    /// afterwards, and the two are kept apart deliberately: a request that is acknowledged and does
    /// nothing is precisely the failure mode a read-back exists to catch.</para>
    /// </summary>
    S7Status RequestRun();
}
