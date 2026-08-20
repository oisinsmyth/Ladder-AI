namespace Harness.CmdInject;

/// <summary>
/// How this session satisfies the block's arming gate: <b>how many heartbeat CHANGES to make, and how
/// each one is proved to have been seen.</b>
///
/// <para>🔴 <b>THE BLOCK'S GATE IS NOT THE TOOL'S GATE, AND THE TWO MUST NOT BE CONFUSED.</b>
/// <c>--arm</c> is OUR authorisation: it decides whether this binary may write at all. The heartbeat is
/// THE BLOCK's gate: it decides whether the thing on the other end will act on what we wrote. A run can
/// pass the first and fail the second, and until this type existed that is exactly what every run did —
/// the tool authorised itself to write and then had no way to satisfy the device, so the command came
/// back <c>NotAcknowledged</c> with no path to any other answer and no indication which end was at
/// fault.</para>
///
/// <para>🔴 <b>A CHANGE IS SOMETHING THE BLOCK SAMPLES, NOT SOMETHING WE WRITE.</b> The block reads the
/// heartbeat once per scan and compares it with what it read last time, so two writes that land inside
/// ONE scan are one change — or none, if the second put the first's value back. Wall-clock spacing makes
/// separation <i>probable</i>; it does not make it <i>observed</i>, and this tool does not ship probable.
/// So each stamp is followed by a wait for the free-running scan counter — which the session already
/// reads at the control registers, and already trusts — to ADVANCE. That turns arming from "two writes
/// about a round trip apart, which is surely enough" into a fact read off the device.</para>
///
/// <para>⚠️ <b>WHY <see cref="ScansBetweenStamps"/> DEFAULTS TO TWO AND NOT ONE.</b> One advance would be
/// enough if we knew where inside a scan the counter is incremented relative to where the heartbeat is
/// sampled — and nothing available to this tool establishes that. Two is the smallest number that is
/// sufficient whatever the answer turns out to be: a counter reading <c>A + 2</c> means a whole scan ran
/// strictly after the write was applied, so the sample cannot have been missed. It costs at most one
/// extra read, against a round trip that is already an order of magnitude longer than a scan. If the
/// ladder ever settles the phase question, the number to change is a parameter, not a constant.</para>
/// </summary>
/// <param name="Changes">
/// How many times the heartbeat register is CHANGED. Zero means this session stamps nothing — a
/// deliberate opt-out, not a default, and the run says so out loud.
/// </param>
/// <param name="ScansBetweenStamps">
/// How far the scan counter must be observed to advance after each stamp before the next one is made, and
/// before the command is written. Never zero: zero is the "we wrote twice and hoped" behaviour this
/// design exists to remove.
/// </param>
/// <param name="ScanWaitAttempts">
/// How many control reads may be spent waiting for that advance before the run gives up and refuses. A
/// counter that does not move is a CPU that is not scanning, and a stamp issued into that is a stamp
/// issued hopefully. <b>Counts the baseline read</b>, which is why one is not a legal value: a single
/// read can establish where the counter stands and can never show it moving.
/// </param>
/// <param name="ScanWaitIntervalMs">
/// Gap between those control reads. Zero by default because each attempt is itself a round trip, which is
/// already far longer than a scan — there is nothing to gain by adding a wait on top of it.
/// </param>
public sealed record HeartbeatPlan(
    int Changes,
    int ScansBetweenStamps = HeartbeatPlan.DefaultScansBetweenStamps,
    int ScanWaitAttempts = HeartbeatPlan.DefaultScanWaitAttempts,
    int ScanWaitIntervalMs = 0)
{
    /// <summary>
    /// The number of changes the protocol model requires. <b>The model's number, and it is a named
    /// constant so a correction is one edit in one place.</b>
    /// </summary>
    public const int ModelledChanges = 2;

    /// <summary>See the type remarks: two, because one is only enough if you know something we do not.</summary>
    public const int DefaultScansBetweenStamps = 2;

    /// <summary>Enough attempts that a running CPU cannot fail them, few enough that a stopped one is not waited on.</summary>
    public const int DefaultScanWaitAttempts = 20;

    /// <summary>What a run does unless told otherwise.</summary>
    public static HeartbeatPlan Default { get; } = new(ModelledChanges);

    /// <summary>Stamp nothing. A named value, so "we chose not to" reads differently from "we forgot".</summary>
    public static HeartbeatPlan None { get; } = new(0);

    /// <summary>How many times the heartbeat is changed. Never negative.</summary>
    public int Changes { get; } = Changes >= 0
        ? Changes
        : throw new ArgumentOutOfRangeException(nameof(Changes), Changes, "a negative number of heartbeat changes is not a plan.");

    /// <summary>Scans that must be observed to pass after each stamp. Never below one.</summary>
    public int ScansBetweenStamps { get; } = ScansBetweenStamps >= 1
        ? ScansBetweenStamps
        : throw new ArgumentOutOfRangeException(nameof(ScansBetweenStamps), ScansBetweenStamps,
            "stamps separated by zero observed scans are stamps separated by nothing — the block samples once per scan, so " +
            "two writes inside one scan are one change or none. There is no version of this that waits for nothing.");

    /// <summary>How many reads may be spent waiting for that advance, baseline included. Never below two.</summary>
    public int ScanWaitAttempts { get; } = ScanWaitAttempts >= 2
        ? ScanWaitAttempts
        : throw new ArgumentOutOfRangeException(nameof(ScanWaitAttempts), ScanWaitAttempts,
            "fewer than two control reads cannot observe an advance: the first read is the baseline, so a plan allowing one " +
            "read would refuse every time and would look like a stalled CPU. Zero would not look at the counter at all, which " +
            "is not a gate.");

    /// <summary>The gap between those reads in milliseconds. Never negative.</summary>
    public int ScanWaitIntervalMs { get; } = ScanWaitIntervalMs >= 0
        ? ScanWaitIntervalMs
        : throw new ArgumentOutOfRangeException(nameof(ScanWaitIntervalMs), ScanWaitIntervalMs, "a negative gap is not a wait.");

    /// <summary>True when this plan writes anything at all.</summary>
    public bool Stamps => Changes > 0;

    public override string ToString() =>
        Stamps
            ? $"{Changes} change(s), each followed by an observed advance of {ScansBetweenStamps} scan(s) " +
              $"(up to {ScanWaitAttempts} read(s), {ScanWaitIntervalMs} ms apart)"
            : "no stamping";
}

/// <summary>
/// The values a stamp run writes — <b>generated here so that no caller can supply one.</b>
///
/// <para>🔴 <b>THE BLOCK COUNTS CHANGES, NOT WRITES, AND TWO IDENTICAL WRITES ARM NOTHING.</b> That is
/// the whole reason this is a generator rather than a parameter: there is no method anywhere in this tool
/// that takes a heartbeat VALUE, so "the client wrote the same number twice" is not a bug that can be
/// introduced at a call site — it is a program that cannot be written. Each value is its predecessor plus
/// one, modulo the register, so adjacent values differ by construction and the first differs from
/// whatever the session's opening read found there.</para>
///
/// <para><b>Any inequality is a change</b> — larger, smaller or wrapped makes no difference, and nothing
/// here depends on the sequence being monotonic. Plus one is simply the cheapest way to be unequal.</para>
///
/// <para><b><see cref="RestingValue"/> is skipped.</b> Zero is what the register reads when nothing has
/// ever written it, so a "change" TO zero is the one change a liveness gate might legitimately decline to
/// count. Skipping it costs nothing and cannot make a value equal to its predecessor: the only way to
/// reach zero is from the top of the range, and the replacement is one.</para>
/// </summary>
public static class HeartbeatStamps
{
    /// <summary>The value a never-written register serves. Never used as a stamp.</summary>
    public const ushort RestingValue = 0;

    /// <summary>
    /// <paramref name="changes"/> successive values starting from what the register was FOUND holding.
    /// Each differs from the one before it; the first differs from <paramref name="found"/>.
    /// </summary>
    public static IReadOnlyList<ushort> From(ushort found, int changes)
    {
        if (changes < 0)
            throw new ArgumentOutOfRangeException(nameof(changes), changes, "a negative number of stamps is not a run.");

        var values = new ushort[changes];
        var current = found;

        for (var i = 0; i < changes; i++)
        {
            current = Next(current);
            values[i] = current;
        }

        return values;
    }

    private static ushort Next(ushort value)
    {
        var next = unchecked((ushort)(value + 1));
        return next == RestingValue ? unchecked((ushort)(next + 1)) : next;
    }
}

/// <summary>How a stamp run ended. Only two of these let the run go on to command.</summary>
public enum HeartbeatEnding
{
    /// <summary>Every change was made, and each was observed to be separated from the next by a scan advance.</summary>
    Stamped,

    /// <summary>The plan asked for no changes. Nothing was written and nothing is claimed.</summary>
    NotRequested,

    /// <summary>
    /// 🔴 The master enable read CLEAR at the device. <b>Nothing was stamped</b> — a heartbeat written
    /// while the enable is down lands in memory and never reaches the block, and from this end that is
    /// indistinguishable from a working write.
    /// </summary>
    EnableClear,

    /// <summary>🔴 The scan counter did not advance within the attempts allowed. The CPU is not scanning; a further stamp would be issued hopefully.</summary>
    ScanStalled,

    /// <summary>🔴 The scan counter went backwards under the arming. The CPU restarted; nothing about the stamps already made still holds.</summary>
    Restarted,

    /// <summary>🔴 The build stamp changed under the arming. A download landed and every address is now a guess.</summary>
    StampChanged,

    /// <summary>A read failed while waiting for the advance. A silence is not an advance, so nothing is concluded from it.</summary>
    ReadFailed,

    /// <summary>Ctrl-C during the arming. The restore still runs.</summary>
    Cancelled,
}

/// <summary>
/// What a stamp run did: the register, what it found there, every value it put there in order, and how
/// the separation between them was observed.
///
/// <para>The values are reported rather than summarised because "it stamped twice" and "it stamped twice
/// with two different values, each seen by a different scan" are different claims, and only the second
/// one arms anything.</para>
/// </summary>
/// <param name="Ending">How it ended.</param>
/// <param name="Register">The heartbeat register, from the resolved binding.</param>
/// <param name="Found">What the session's opening read found in that register.</param>
/// <param name="Values">Every value written, in order.</param>
/// <param name="ScanReads">How many control reads were spent observing the advances.</param>
/// <param name="Message">The run in words.</param>
public sealed record HeartbeatStampReport(
    HeartbeatEnding Ending,
    int Register,
    ushort Found,
    IReadOnlyList<ushort> Values,
    int ScanReads,
    string Message)
{
    /// <summary>Whether the run may go on to write a command. <b>Only <see cref="HeartbeatEnding.Stamped"/> and an explicit opt-out qualify.</b></summary>
    public bool MayCommand => Ending is HeartbeatEnding.Stamped or HeartbeatEnding.NotRequested;
}
