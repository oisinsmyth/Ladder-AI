using DeviceGuard;

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
}

/// <summary>
/// One poll, whatever became of it.
/// </summary>
/// <param name="At">When the attempt COMPLETED, from the injected clock. The age on screen counts from here.</param>
/// <param name="Outcome">Which of the six things happened.</param>
/// <param name="Detail">The reason, verbatim where one came from the fence or the wire.</param>
/// <param name="Registers">The whole declared area. Empty unless <see cref="PollOutcome.Ok"/>.</param>
/// <param name="ElapsedMs">Wall time for the transaction.</param>
public sealed record PollAttempt(
    DateTimeOffset At,
    PollOutcome Outcome,
    string Detail,
    ushort[] Registers,
    long ElapsedMs)
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
