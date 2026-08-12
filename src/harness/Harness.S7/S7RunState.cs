namespace Harness.S7;

/// <summary>
/// What a run-state read established about the CPU — and deliberately no more than that.
///
/// <para><b>Why this exists.</b> Over Modbus, "the CPU went to STOP" and "the link dropped" are the
/// same observation: nothing answers. Classic S7comm can tell them apart, and TIA Openness has been
/// measured to expose no operating mode at all, so this read is the only source of that fact the
/// project has.</para>
///
/// <para><b>There is no <c>Stopped</c> member, and that is the design rather than a naming
/// preference.</b> The wire tells us whether the CPU answered RUN. It does not reliably tell us WHICH
/// non-running mode it is in — see <see cref="S7RunStateReading.Decode"/> for the measurement — so a
/// member called <c>Stopped</c> would assert a fact the read cannot establish, exactly the overclaim
/// the measurement warns against.</para>
/// </summary>
public enum S7RunState
{
    /// <summary>
    /// Nothing was established. The read did not complete, so this is a statement about the
    /// CONVERSATION and not about the CPU — it is never produced by a successful read.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The CPU answered RUN. Exact: this comes only from the one value Sharp7 passes through
    /// unmapped, so nothing unrecognised can arrive here.
    /// </summary>
    Running,

    /// <summary>
    /// The CPU answered, and the answer was not RUN. It is NOT "the CPU is stopped": STARTUP, HOLD, a
    /// mode nobody has seen and a byte Sharp7 did not recognise all land here.
    /// </summary>
    NotRunning,
}

/// <summary>
/// One run-state read: the decoded state plus the value it was decoded from.
/// </summary>
/// <param name="State">What was established.</param>
/// <param name="Sharp7Value">
/// The value Sharp7's <c>PlcGetStatus</c> produced, or <see cref="NotRead"/> when the read failed.
///
/// <para>Carried in the return shape rather than logged, because it is the only thing that
/// distinguishes the members of <see cref="S7RunState.NotRunning"/> from each other, and a diagnostic
/// that exists only in a log is unavailable to the code that has to explain a failure. Note what it
/// is NOT: <c>PlcGetStatus</c> has already collapsed the device's own byte to one of {0,4,8} before
/// this sees it, so an unfamiliar device byte arrives here as 4 and is not recoverable through this
/// API. Seeing the true byte would need a raw request that bypasses <c>PlcGetStatus</c>, which is not
/// built.</para>
/// </param>
public readonly record struct S7RunStateReading(S7RunState State, int Sharp7Value)
{
    /// <summary>
    /// The one value that means RUN — Sharp7's <c>S7Consts.S7CpuStatusRun</c>, restated here so that
    /// this file compiles and is tested without Sharp7 present (the adapter is excluded from the build
    /// when the DLL is absent; see the project file). Verified against the assembly on 2026-08-12.
    /// </summary>
    public const int RunValue = 8;

    /// <summary>No value was obtained. Outside the range <c>PlcGetStatus</c> can produce, so it cannot
    /// be mistaken for an answer.</summary>
    public const int NotRead = -1;

    /// <summary>A read that did not complete. The status returned alongside carries the reason.</summary>
    public static S7RunStateReading Unread { get; } = new(S7RunState.Unknown, NotRead);

    public bool Running => State == S7RunState.Running;

    /// <summary>
    /// Decode the value of a COMPLETED read. Running is keyed on <see cref="RunValue"/> and everything
    /// else is <see cref="S7RunState.NotRunning"/>.
    ///
    /// <para><b>Measured on the bench rig, 2026-08-12, in both CPU states.</b> In RUN the CPU answers
    /// PDU byte 0x08 and Sharp7 reports 8. In STOP it answers <b>PDU byte 0x03</b> — not one of
    /// Sharp7's three named constants — so it reaches this method as 4 through Sharp7's CATCH-ALL arm
    /// (<c>default: Status = S7CpuStatusStop</c>). <c>S7CpuStatusStop</c> is never actually returned by
    /// this device.</para>
    ///
    /// <para><b>So do not tighten this decoder</b> to accept only {0,4,8} and reject the rest, and do
    /// not tighten Sharp7's: on this rig either change reports a stopped CPU as not-stopped, which is
    /// the failure this read exists to prevent. Sharp7's catch-all is load-bearing.</para>
    ///
    /// <para>A value of 0 (Sharp7's <c>S7CpuStatusUnknown</c>) decodes to <c>NotRunning</c> and not to
    /// <see cref="S7RunState.Unknown"/>: the CPU answered, and what it answered was not RUN.
    /// <c>Unknown</c> is reserved for a read that produced no answer at all.</para>
    /// </summary>
    public static S7RunStateReading Decode(int sharp7Value) =>
        new(sharp7Value == RunValue ? S7RunState.Running : S7RunState.NotRunning, sharp7Value);

    public override string ToString() => State switch
    {
        S7RunState.Running => $"Running ({Sharp7Value})",
        S7RunState.NotRunning => $"NotRunning ({Sharp7Value}) — not RUN; which non-running mode is not knowable here",
        _ => "Unknown (not read)",
    };
}
