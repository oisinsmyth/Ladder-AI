using System;

namespace Ladder.Converter.Leases;

/// <summary>
/// What a lease may be taken on. <b>Both are single-writer resources that nothing currently guards.</b>
///
/// <para><see cref="LeaseResource.Unstated"/> is the zero value and is unusable, so a resource nobody
/// named cannot be whichever happened to be first.</para>
/// </summary>
public enum LeaseResource
{
    /// <summary>The zero value, deliberately not a usable resource.</summary>
    Unstated = 0,

    /// <summary>
    /// One TIA Portal project. Two Openness sessions on one project is a genuine single-writer
    /// constraint on TIA's side, not a policy choice — and until now it was enforced by a text file
    /// whose own README calls it <i>"a cooperative convention… not a real lock"</i>.
    /// </summary>
    Portal,

    /// <summary>
    /// One bench rig, by address. <b>Its download is device-level and its <c>MB_SERVER</c> accepts ONE
    /// connection</b>, so two agents are as mutually exclusive here as on Portal — and the existing
    /// device guards are stateless per-call PERMISSION checks with no notion of who else is connected.
    /// </summary>
    Rig,
}

/// <summary>Why an acquire did or did not happen. Never a bool.</summary>
public enum LeaseResult
{
    /// <summary>The zero value, and unusable.</summary>
    Unstated = 0,

    /// <summary>The lease is now held by the caller.</summary>
    Acquired,

    /// <summary>Another holder has it and is alive. <b>The collision, prevented.</b></summary>
    HeldByAnother,

    /// <summary>
    /// The previous holder was dead AND its lease had expired, so it was reclaimed and the caller now
    /// holds it. Reported distinctly from <see cref="Acquired"/> because a reclaim means somebody's run
    /// died holding the gate, which is worth seeing rather than smoothing over.
    /// </summary>
    Reclaimed,

    /// <summary>
    /// 🔴 A human — or anything this tool did not launch — is holding the Portal project.
    ///
    /// <para><b>Refused by name, never queued.</b> FI-65's objection 11: the lease must DETECT a human
    /// rather than pretend to schedule one. There is no TTL on a person.</para>
    /// </summary>
    HeldOutsideTheTool,

    /// <summary>Malformed request — no resource, no holder, a non-positive TTL.</summary>
    Invalid,
}

/// <summary>
/// One lease. <b>Identity is holder + PID + the process's START TIME, not the PID alone</b> — PIDs are
/// reused, and a reclaim decision that trusted a bare PID would eventually evict a live holder because
/// an unrelated process inherited its number.
/// </summary>
/// <param name="ExpiresUtc">
/// When the lease lapses. <b>Expiry alone never permits a reclaim</b> — see
/// <see cref="LeaseStore"/>: the holder must also be provably gone.
/// </param>
public sealed record Lease(
    LeaseResource Resource,
    string Target,
    string Holder,
    int ProcessId,
    DateTime ProcessStartUtc,
    DateTime AcquiredUtc,
    DateTime ExpiresUtc,
    string Purpose)
{
    public bool HasExpired(DateTime nowUtc) => nowUtc >= ExpiresUtc;

    public override string ToString() =>
        $"{Resource.ToString().ToLowerInvariant()}:{Target} held by {Holder} (pid {ProcessId}, "
        + $"started {ProcessStartUtc:u}), acquired {AcquiredUtc:u}, expires {ExpiresUtc:u}"
        + (Purpose.Length == 0 ? "" : $" — {Purpose}");
}

/// <summary>The outcome of one acquire, with the holder that decided it.</summary>
/// <param name="Holder">
/// On success, the caller's own lease. On <see cref="LeaseResult.HeldByAnother"/>, <b>the lease that
/// won</b> — read back off disk rather than assumed, so the refusal can name who has it.
/// </param>
public sealed record LeaseOutcome(LeaseResult Result, Lease? Holder, string Detail)
{
    public bool Held => Result is LeaseResult.Acquired or LeaseResult.Reclaimed;
}
