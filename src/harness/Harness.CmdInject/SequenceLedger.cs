namespace Harness.CmdInject;

/// <summary>How the client's allocated sequence stands against the sequence the device last acknowledged.</summary>
public enum SequenceStanding
{
    /// <summary>The device has acknowledged exactly the client's latest sequence. Nothing is in flight.</summary>
    InSync,

    /// <summary>
    /// The client has allocated ahead of the device — the ordinary state right after a write, before the
    /// acknowledgement lands. Keep polling.
    /// </summary>
    ClientAhead,

    /// <summary>
    /// 🔴 <b>THE DEVICE'S ACKNOWLEDGED SEQUENCE IS AHEAD OF ANYTHING THIS CLIENT ALLOCATED.</b> Someone
    /// else wrote, or the CPU restarted, or the address is not the device the ledger has been talking to.
    /// Not a state to poll through — a state to stop on.
    /// </summary>
    DeviceAhead,
}

/// <summary>
/// One channel's sequence allocator, and <b>the only thing that decides the next sequence number.</b>
///
/// <para>The sequence is what a command is recognised by: the block executes when a NEW sequence appears.
/// So the rule is exact — every allocation is different from the last, none is ever zero (zero is the
/// "no command" resting value, and a command carrying it would be invisible), and the counter wraps
/// 65535 → 1 rather than 65535 → 0. Sixteen bits is the register's width, not a choice, so a full cycle
/// eventually revisits an old value; within a cycle no value repeats.</para>
///
/// <para><b>Reconciliation is modular, in the tick-comparison idiom</b> (the same one
/// <c>Harness.Wire.ScanCount</c> uses): a forward distance in the near half means the device is genuinely
/// ahead; in the far half it means the device is a little behind, i.e. the client is ahead. Absorbing a
/// large forward jump as "the device caught up" would manufacture progress, which is the one direction
/// this tooling never fails in.</para>
/// </summary>
public sealed class SequenceLedger
{
    // 0 means "nothing allocated yet". It is never a value Allocate() returns.
    private ushort _last;

    /// <summary>The most recently allocated sequence, or null before the first allocation.</summary>
    public ushort? LastAllocated => _last == 0 ? null : _last;

    /// <summary>
    /// Allocate the next sequence: one past the last, skipping zero, wrapping at the 16-bit edge.
    /// </summary>
    public ushort Allocate()
    {
        _last = NextAfter(_last);
        return _last;
    }

    /// <summary>The value <see cref="Allocate"/> would return next, without consuming it. Pure — for tests and dry runs.</summary>
    public ushort Peek() => NextAfter(_last);

    private static ushort NextAfter(ushort prior)
    {
        // Semantics-preserving rewrite: same mapping, expressed as an explicit wrap at the edge.
        return prior == ushort.MaxValue ? (ushort)1 : (ushort)(prior + 1);
    }

    /// <summary>
    /// Where the device's acknowledged sequence stands relative to the client's latest allocation.
    /// </summary>
    /// <param name="deviceAckSeq">The sequence the device reports in its ack register.</param>
    public SequenceStanding Reconcile(ushort deviceAckSeq)
    {
        var forward = (ushort)(deviceAckSeq - _last);

        if (forward == 0)
            return SequenceStanding.InSync;

        // Near half forward: the device is ahead of us. Far half forward: it is behind us (we are ahead).
        return forward <= HalfSpan ? SequenceStanding.DeviceAhead : SequenceStanding.ClientAhead;
    }

    private const ushort HalfSpan = ushort.MaxValue / 2; // 32767
}
