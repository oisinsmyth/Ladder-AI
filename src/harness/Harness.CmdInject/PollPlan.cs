using Harness.Map;

namespace Harness.CmdInject;

/// <summary>One contiguous FC03 — a first register and a count.</summary>
public sealed record RegisterSpan(int First, int Count)
{
    /// <summary>Last register the span covers.</summary>
    public int Last => First + Count - 1;

    /// <summary>True when the span covers <paramref name="register"/>.</summary>
    public bool Covers(int register) => register >= First && register <= Last;

    public override string ToString() => $"[{First}..{Last}] ({Count} register(s))";
}

/// <summary>
/// How one acknowledgement poll is read off the wire — <b>and whether re-checking the build stamp on
/// this batch costs anything.</b>
///
/// <para>🔺 <b>A CORRECTION TO THE PLAN, MADE WHERE IT MEETS THE MAP.</b> The plan says the identity
/// re-check is free because "it shares an FC03 the poll was making anyway". That is true exactly when the
/// acknowledgement registers lie within the protocol's FC03 register limit of register
/// zero — the stamp and the scan counter sit at registers 0–3, so one read from zero can reach both. It
/// is <b>not</b> true of a mirror whose observation band starts beyond that, and the mirror this tool was
/// written against is one of them. So the plan is computed rather than assumed, and the run says which
/// case it is in: one read (the re-check is genuinely free) or two (it costs a round trip, and the report
/// says so). Neither case ever skips the re-check — a free check and a cheap one are both worth more than
/// a write against a device that has been re-downloaded underneath you.</para>
/// </summary>
public sealed record PollPlan(IReadOnlyList<RegisterSpan> Reads, bool ControlSharesTheAckRead, string Basis)
{
    /// <summary>
    /// Plan the reads for one poll of an acknowledgement span.
    /// </summary>
    /// <param name="ackFirst">First acknowledgement register.</param>
    /// <param name="ackLast">Last acknowledgement register.</param>
    public static PollPlan For(int ackFirst, int ackLast)
    {
        if (ackFirst < 0)
            throw new ArgumentOutOfRangeException(nameof(ackFirst), ackFirst, "an acknowledgement span cannot start below register zero.");

        if (ackLast < ackFirst)
            throw new ArgumentOutOfRangeException(nameof(ackLast), ackLast, "an acknowledgement span cannot end before it begins.");

        var wholeCount = ackLast + 1;
        if (wholeCount <= ModbusLimits.MaxReadRegisters)
        {
            return new PollPlan(
                new[] { new RegisterSpan(0, wholeCount) },
                ControlSharesTheAckRead: true,
                $"one FC03 covers registers 0..{ackLast}, so the stamp and scan-counter re-check ride on the poll's own read and cost nothing.");
        }

        return new PollPlan(
            new[] { new RegisterSpan(0, ControlRegisters.Count), new RegisterSpan(ackFirst, ackLast - ackFirst + 1) },
            ControlSharesTheAckRead: false,
            $"registers 0..{ackLast} span {wholeCount} registers, past the {ModbusLimits.MaxReadRegisters}-register FC03 limit, so the " +
            "stamp and scan-counter re-check is a SECOND read. It is still made: identity is worth a round trip.");
    }
}
