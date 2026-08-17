using DeviceGuard;
using Harness.Wire;

namespace Harness.MirrorView;

/// <summary>
/// How one poll cycle ended.
///
/// <para>🔴 <b>A SILENCE IS NOT A REFUSAL, AND NEITHER IS A GOVERNANCE DECISION.</b> These are six
/// distinct facts and the page prints which one it is. Collapsing them into "failed" is what lets a
/// device fence's refusal be read as a network problem — or worse, a network problem be read as
/// evidence about the device.</para>
/// </summary>
public enum PollOutcome
{
    /// <summary>The whole declared area was read.</summary>
    Ok,

    /// <summary>The DEVICE FENCE refused the target. <b>No socket was opened.</b></summary>
    RefusedByFence,

    /// <summary>The fence itself threw. Not a verdict about the device — nothing examined it.</summary>
    FenceFault,

    /// <summary>The fence allowed it and the session could not be opened.</summary>
    ConnectFailed,

    /// <summary>
    /// The server answered with a Modbus EXCEPTION RESPONSE — it received the request and refused it.
    /// A fact about the server's address space, and quite different from silence.
    /// </summary>
    RefusedByServer,

    /// <summary>No answer from the server at all — a timeout, a dropped socket, a short frame.</summary>
    TransportFailed,

    // ---- FOLLOW MODE. No socket exists on any of these paths. -------------------------------------
    //
    // 🔴 FIVE MORE WAYS TO BE WRONG, AND EACH ONE IS ITS OWN FACT. Following a wave's published feed
    // removes the second socket and introduces its own failure modes, and they are NOT the direct-mode
    // ones wearing different words: none of them is evidence about the device, and three of them are not
    // even evidence that anything is broken. Merging any pair would give a viewer one indistinguishable
    // failure to render, and the most likely rendering of an indistinguishable failure is a plausible page.

    /// <summary>
    /// <b>NO PUBLISHER HAS EVER WRITTEN TO THIS PATH.</b> No wave has run with <c>--publish</c> pointed
    /// here. Not a fault; not a stopped wave; not an empty mirror.
    /// </summary>
    FeedNeverPublished,

    /// <summary>
    /// <b>A PUBLISHER WROTE HERE AND THE FEED IS GONE.</b> The initialised marker survives and the feed
    /// does not, so a publish did not complete or the file was removed. Emphatically not the state above.
    /// </summary>
    FeedPublishInterrupted,

    /// <summary>The feed is present and could not be fully accounted for. A REFUSAL, never a short reading.</summary>
    FeedUnreadable,

    /// <summary>
    /// <b>THE FEED DESCRIBES A DIFFERENT MIRROR FROM THE ONE THIS VIEWER'S MAP DESCRIBES.</b> The registers
    /// would still line up and would mean different signals. This is the failure that renders perfectly.
    /// </summary>
    FeedDescribesADifferentMirror,

    /// <summary>
    /// The feed parsed, the identity agrees, and <b>it carries no read yet</b> — a publisher that has begun
    /// and whose first poll has not returned. There is nothing to show, which is not the same as zeros.
    /// </summary>
    FeedCarriesNoReading,
}

/// <summary>
/// Where a followed reading came from, and what the publisher said about itself.
///
/// <para>🔴 <b>THIS IS THE PROVENANCE THAT MAKES THE WHOLE CHANGE HONEST.</b> The viewer is not reading a
/// device; it is reading what another process read from a device. So the page states WHO published, WHAT
/// they said they were describing, WHETHER they expected to write again, and WHEN — and every register's
/// own reading carries the instant the harness's read returned. Without this the page would be an
/// unattributed table that looks exactly like a direct reading.</para>
/// </summary>
/// <param name="Origin">The feed file.</param>
/// <param name="PublisherId">Who wrote it.</param>
/// <param name="Status">RUNNING or ENDED, as the publisher last stated. The live/final distinction.</param>
/// <param name="PublishedUtc">When the publisher last wrote the document. NOT when any register was read.</param>
/// <param name="Sequence">Publishes since it began, so an advancing feed is visible as such.</param>
/// <param name="BuildStamp">The stamp the publisher's client expects, as a literal.</param>
/// <param name="MapHash">The publisher's own map hash.</param>
/// <param name="DeclaredRegisters">The width the publisher is addressing.</param>
/// <param name="WordOrder">The 32-bit order the publisher read under.</param>
/// <param name="Frames">Distinct reads in the feed.</param>
/// <param name="RegistersObserved">Registers an actual read covered.</param>
/// <param name="RegistersNotObserved">Registers in the declared area that no read covered. Shown as NOT READ, never as 0.</param>
/// <param name="OldestObservedUtc">The oldest read behind any displayed register — the page's worst case.</param>
public sealed record FeedProvenance(
    string Origin,
    string PublisherId,
    PublisherStatus Status,
    DateTimeOffset PublishedUtc,
    long Sequence,
    string BuildStamp,
    string MapHash,
    int DeclaredRegisters,
    RegisterWordOrder WordOrder,
    int Frames,
    int RegistersObserved,
    int RegistersNotObserved,
    DateTimeOffset? OldestObservedUtc);

/// <summary>
/// One poll, whatever became of it.
/// </summary>
/// <param name="At">When the attempt COMPLETED, from the injected clock. The age on screen counts from here.</param>
/// <param name="Outcome">Which of the six things happened.</param>
/// <param name="Detail">The reason, verbatim where one came from the fence or the wire.</param>
/// <param name="Registers">The whole declared area. Empty unless <see cref="PollOutcome.Ok"/>.</param>
/// <param name="ElapsedMs">Wall time for the transaction.</param>
/// <param name="RegisterObservedUtc">
/// 🔴 <b>PER-REGISTER PROVENANCE, AND A NULL IS "NOTHING READ THIS ONE".</b> Null for the whole array in
/// DIRECT mode, where one FC03 covers the whole area and every register shares <paramref name="At"/> by
/// construction. Populated in FOLLOW mode, where the picture is composed from the several reads a wave
/// actually makes and the registers therefore have DIFFERENT AGES. A consumer that ignored this would
/// print a register nobody read as a zero, and a register read forty seconds ago beside one read now.
/// </param>
/// <param name="Feed">Where a followed reading came from. Null in direct mode.</param>
public sealed record PollAttempt(
    DateTimeOffset At,
    PollOutcome Outcome,
    string Detail,
    ushort[] Registers,
    long ElapsedMs,
    DateTimeOffset?[]? RegisterObservedUtc = null,
    FeedProvenance? Feed = null)
{
    public bool Ok => Outcome == PollOutcome.Ok;
}

/// <summary>
/// The device fence, as a port, so its ordering relative to the socket is <b>observable</b>.
///
/// <para>A test supplies a fence that refuses everything and asserts the connection factory was never
/// invoked. That is a fact about a counter rather than a claim about the code — and it is the check
/// that survives somebody moving the fence below the connect, which an exit code would not.</para>
/// </summary>
public interface IDeviceFence
{
    /// <summary>May this address be contacted at all?</summary>
    GuardDecision Check(string address);
}

/// <summary>
/// The real fence: <c>DeviceAccessGuard</c> over the allowlist file, re-read on every poll.
///
/// <para>⚠️ <b><c>DeviceAccessGuard</c>, never <c>DeviceWriteGuard</c>.</b> The live allowlist entry
/// reads <c>"writeEligible": false</c>, which is correct and must not block a read. Consulting the write
/// guard here would refuse a device that is properly authorised for exactly what this tool does — an
/// over-firing gate, which decays into a warning and then into nothing.</para>
///
/// <para><b>The file is re-read every cycle on purpose.</b> Authorisation that was checked once at
/// startup is authorisation that cannot be withdrawn while the page is open, and a long-lived viewer is
/// exactly the thing somebody would want to revoke.</para>
/// </summary>
public sealed class AllowlistDeviceFence : IDeviceFence
{
    private readonly string? _allowlistPath;

    public AllowlistDeviceFence(string? allowlistPath) => _allowlistPath = allowlistPath;

    public GuardDecision Check(string address) =>
        new DeviceAccessGuard(AllowlistFile.Load(_allowlistPath)).Check(address);
}
