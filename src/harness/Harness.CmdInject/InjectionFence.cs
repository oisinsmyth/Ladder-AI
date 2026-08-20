using DeviceGuard;

namespace Harness.CmdInject;

/// <summary>
/// Which gate an injection decision came out of. One member per gate, so a caller and a log can say
/// WHICH check decided rather than merely that something did.
/// </summary>
public enum InjectionGate
{
    /// <summary>Every gate passed. The only value that authorizes opening a socket to write.</summary>
    Allowed = 0,

    /// <summary>No target address was given.</summary>
    NoTarget,

    /// <summary>
    /// Neither <c>--allowlist</c> nor the environment variable named a file.
    ///
    /// <para>🔴 <b>A USAGE ERROR, NOT A REFUSAL — AND THE DIFFERENCE IS DELIBERATE.</b> Unlike the read
    /// fence, which treats an absent allowlist as a governance refusal, this reads it as a SETUP mistake:
    /// nobody has yet said which devices are approved, which is a thing to fix, not a decision the tool
    /// made about this device. It fails just as closed — nothing is contacted — but it must not read to a
    /// caller as "that device is not approved".</para>
    /// </summary>
    NoAllowlistConfigured,

    /// <summary>The allowlist could not be found, read or parsed. Fail closed.</summary>
    AllowlistUnusable,

    /// <summary>The target is not on the allowlist, or is on it with a kind other than <c>test-rig</c>.</summary>
    NotAnApprovedTestRig,

    /// <summary>
    /// The entry exists and is a test rig, but <c>writeEligible</c> is false.
    ///
    /// <para>🔴 <b>TODAY'S LIVE ANSWER, AND THE MOST COMMON ONE THIS TOOL WILL GET.</b> A rig may be
    /// approved for reading and not for writing; injection is a write. This field is a HUMAN
    /// authorisation and nothing here edits it or stands in for it — a person edits the allowlist, or the
    /// answer is no. No <c>--arm</c> supplies this gate's answer: arming authorises injection in the
    /// abstract, it does not decide that THIS device may be written.</para>
    /// </summary>
    NotWriteEligible,

    /// <summary>
    /// <c>outputsIsolated</c> is false.
    ///
    /// <para>The one gate no software bug can cross. Injecting a command drives the plant's outputs; a
    /// device that may be written must have its outputs physically incapable of actuating.</para>
    /// </summary>
    OutputsNotIsolated,

    /// <summary>The isolation is asserted but nobody is named as having asserted it.</summary>
    IsolationUnattributed,

    /// <summary>
    /// No expected build stamp was declared, so nothing could confirm the CPU answering this address is
    /// the program this binding was written for.
    ///
    /// <para>🔴 <b>THE IDENTITY GATE, MOVED TO THE ONLY IDENTITY THIS LINK CARRIES.</b> There is no Modbus
    /// identity read, but the harness program publishes a build stamp at registers 0–1 and it is already
    /// on the wire. The stamp is declared here, BEFORE the socket, and compared after connect and before
    /// any write (phase 2). Declaring none is refused now rather than discovered later, because an address
    /// is a routing hint — 10.10.10.10 is the standard PLC address at multiple sites, and which controller
    /// answers depends on which tunnel is up.</para>
    /// </summary>
    NoExpectedBuildStamp,
}

/// <summary>One fence verdict: whether it allowed, which gate decided, and why in words.</summary>
public sealed record InjectionDecision(
    bool Allowed,
    InjectionGate Gate,
    string Message,
    AllowlistEntry? MatchedEntry)
{
    public static InjectionDecision Refuse(InjectionGate gate, string message, AllowlistEntry? entry = null) =>
        new(false, gate, message, entry);

    public static InjectionDecision Allow(AllowlistEntry entry, uint expectedStamp) =>
        new(true, InjectionGate.Allowed,
            $"'{entry.DisplayLabel}' is an approved, write-eligible, isolated test rig; expecting build stamp " +
            $"16#{expectedStamp:X8} to be confirmed on the wire before any write.", entry);
}

/// <summary>
/// The fence in front of an injection write. A PURE decision function over an allowlist, a target
/// address and an expected build stamp: it opens nothing, reads nothing from a device, and cannot be
/// reached by a socket.
///
/// <para><b>Why it reuses <c>src/device-guard/</c> rather than reimplementing it.</b> The read guard
/// (<see cref="DeviceAccessGuard"/>) already fails closed on a missing path, a missing file, an
/// unreadable file, a malformed document, a null entry, an unlisted address and an entry whose
/// <c>kind</c> is not exactly <c>test-rig</c> — each a measured failure with a test behind it. The
/// listed/kind/exact-match gates are delegated to it WHOLE; a second implementation would be a second
/// thing to get wrong and the two would drift.</para>
///
/// <para><b>Why it does not use <c>DeviceWriteGuard</c>.</b> That guard is shaped around writing an
/// AREA — a declared scope, an area inside it, a verified restore point — and its identity gate takes an
/// identity that has already been read, which would put a socket ahead of the fence's verdict. The gates
/// that DO apply to injection are restated here in the same order and with the same fail-closed shape.
/// The build-stamp gate is this fence's identity analogue: declared here, confirmed on the wire later.</para>
///
/// <para><b>There is no override.</b> No flag, no environment variable and no file supplies a gate's
/// answer or skips one.</para>
/// </summary>
public static class InjectionFence
{
    /// <summary>
    /// Decide whether an injection write may be attempted against <paramref name="target"/>.
    /// </summary>
    /// <param name="target">The address the write would connect to. A routing hint, never an authorization.</param>
    /// <param name="allowlistPath">The resolved allowlist path, or null. Null is a USAGE outcome — see <see cref="InjectionGate.NoAllowlistConfigured"/>.</param>
    /// <param name="expectedBuildStamp">The build stamp the caller declares it expects, or null. Null is a refusal.</param>
    public static InjectionDecision Check(string? target, string? allowlistPath, uint? expectedBuildStamp)
    {
        if (string.IsNullOrWhiteSpace(target))
            return InjectionDecision.Refuse(InjectionGate.NoTarget, "no target device given.");

        if (string.IsNullOrWhiteSpace(allowlistPath))
        {
            return InjectionDecision.Refuse(InjectionGate.NoAllowlistConfigured,
                $"no allowlist configured. Pass --allowlist <path> or set {AllowlistPath.EnvVar}. This is a SETUP " +
                "error, not a decision about this device — nothing is contacted, and an absent fence is not an open one.");
        }

        var load = AllowlistFile.Load(allowlistPath);
        if (!load.Loaded)
        {
            return InjectionDecision.Refuse(InjectionGate.AllowlistUnusable,
                load.Message ?? "the allowlist could not be loaded.");
        }

        // Gates 1-2: listed, exact-matched, and marked test-rig. Delegated whole to the read guard.
        var read = new DeviceAccessGuard(load).Check(target);
        if (!read.Allowed)
        {
            return InjectionDecision.Refuse(InjectionGate.NotAnApprovedTestRig,
                $"{read.Reason}: {read.Message}");
        }

        var entry = read.MatchedEntry!;

        // Gate 3: the separate, deliberate write grant.
        if (entry.WriteEligible is false)
        {
            return InjectionDecision.Refuse(InjectionGate.NotWriteEligible,
                $"'{target}' ({entry.DisplayLabel}) is an approved test rig for READING, but its allowlist entry reads " +
                "\"writeEligible\": false. Injecting a command is a write. This field is a HUMAN AUTHORISATION and nothing " +
                "here may edit it or stand in for it: a person changes it in the allowlist, or the answer is no.", entry);
        }

        // Gate 4: the physical precondition (ADR-0009 fence item 3).
        if (!entry.OutputsIsolated)
        {
            return InjectionDecision.Refuse(InjectionGate.OutputsNotIsolated,
                $"'{target}' ({entry.DisplayLabel}) is not asserted physically isolated. An injected command drives the " +
                "plant's outputs; a device that may be written must have its outputs physically incapable of actuating — " +
                "field wiring disconnected, or interposing relays unpowered.", entry);
        }

        if (string.IsNullOrWhiteSpace(entry.IsolationAssertedBy))
        {
            return InjectionDecision.Refuse(InjectionGate.IsolationUnattributed,
                $"'{target}' ({entry.DisplayLabel}) claims physical isolation but names nobody as having asserted it. " +
                "An unattributed assertion is not an assertion.", entry);
        }

        // Gate 5: is there an identity to confirm the answering CPU against? Asked here, before the socket,
        // because whether one was DECLARED is a fact about the inputs. The comparison happens after connect.
        if (expectedBuildStamp is not { } stamp)
        {
            return InjectionDecision.Refuse(InjectionGate.NoExpectedBuildStamp,
                $"'{target}' ({entry.DisplayLabel}) is authorised, but no expected build stamp was declared, so nothing " +
                "could confirm the CPU answering this address is running the program this binding was written for. The " +
                "address is only a routing hint. Declare the stamp the harness publishes at registers 0-1.", entry);
        }

        return InjectionDecision.Allow(entry, stamp);
    }
}
