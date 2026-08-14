using DeviceGuard;

namespace Harness.RigControl;

/// <summary>
/// Which gate a run-transition decision came out of. One member per gate, so a caller and a log can
/// say WHICH check refused rather than merely that something did.
/// </summary>
public enum RunGate
{
    /// <summary>Every gate passed. The only value that authorizes contacting a device.</summary>
    Allowed = 0,

    /// <summary>No target address was given.</summary>
    NoTarget,

    /// <summary>
    /// Neither <c>--allowlist</c> nor the environment variable named a file.
    ///
    /// <para>*** A REFUSAL, NOT A USAGE ERROR, AND THE DIFFERENCE IS THE WHOLE POINT. *** An absent
    /// fence must not read as an open one. There is deliberately no built-in default path — a default
    /// is a place a production device can quietly accumulate.</para>
    /// </summary>
    NoAllowlistConfigured,

    /// <summary>The allowlist could not be loaded, found, read or parsed. Fail closed.</summary>
    AllowlistUnusable,

    /// <summary>The target is not on the allowlist, or is on it with a kind other than <c>test-rig</c>.</summary>
    NotAnApprovedTestRig,

    /// <summary>
    /// The entry exists and is a test rig, but <c>writeEligible</c> is false.
    ///
    /// <para>*** THIS IS A HUMAN AUTHORISATION, NOT A CONFIGURATION VALUE. *** It is the field that
    /// says a person decided this device may be changed, on top of having decided it is a test rig.
    /// Nothing in this binary edits an allowlist, and no flag supplies this gate's answer.</para>
    /// </summary>
    NotWriteEligible,

    /// <summary>
    /// <c>outputsIsolated</c> is false.
    ///
    /// <para>Load-bearing for THIS operation specifically: putting a CPU into RUN is what makes its
    /// outputs live. Nothing in software can verify the assertion — it is about field wiring and
    /// interposing relays — which is why it is refused on rather than warned about.</para>
    /// </summary>
    OutputsNotIsolated,

    /// <summary>The isolation is asserted but nobody is named as having asserted it.</summary>
    IsolationUnattributed,

    /// <summary>
    /// The entry declares no order number, so there is nothing to check the answering CPU against.
    ///
    /// <para>Checked BEFORE the socket, because it is a property of the entry rather than of the
    /// device: an entry that can never be satisfied should say so without a connection.</para>
    /// </summary>
    NoDeclaredIdentity,
}

/// <summary>One fence verdict: whether it allowed, which gate decided, and why in words.</summary>
public sealed record RunTransitionDecision(
    bool Allowed,
    RunGate Gate,
    string Message,
    AllowlistEntry? MatchedEntry)
{
    public static RunTransitionDecision Refuse(RunGate gate, string message, AllowlistEntry? entry = null) =>
        new(false, gate, message, entry);

    public static RunTransitionDecision Allow(AllowlistEntry entry) =>
        new(true, RunGate.Allowed, $"'{entry.DisplayLabel}' is an approved, write-eligible, isolated test rig.", entry);
}

/// <summary>
/// The fence in front of a CPU RUN transition. A PURE decision function over an allowlist and a target
/// address: it opens nothing, reads nothing from a device, and cannot be reached by a socket.
///
/// <para><b>Why it reuses <c>src/device-guard/</c> rather than reimplementing it.</b> The read gate
/// (<see cref="DeviceAccessGuard"/>) already fails closed on a missing path, a missing file, an
/// unreadable file, a malformed document, a null entry, an unlisted address and an entry whose
/// <c>kind</c> is not exactly <c>test-rig</c> — each of those a measured failure with a test behind it.
/// A second implementation of that would be a second thing to get wrong, and the two would drift.</para>
///
/// <para><b>Why it does not use <see cref="DeviceWriteGuard"/>.</b> That guard is shaped around writing
/// an AREA: it demands a declared <see cref="WriteScope"/>, an area inside it, and a verified restore
/// point. A mode change writes no area, so those gates have no operands here — and its identity gate
/// takes an identity that has already been read, which would put a socket ahead of the fence's verdict.
/// The gates that DO apply are restated here in the same order and with the same fail-closed shape.
/// The restore-point gate has no analogue and its absence is stated rather than quietly dropped: the
/// inverse of this operation is a STOP, which this tool deliberately cannot perform (see
/// <see cref="IRunTransitionTransport"/>), so reversal is a person's job and the plan output says so.</para>
///
/// <para><b>There is no override.</b> No flag, no environment variable and no file supplies a gate's
/// answer or skips one. The allowlist is LOCATED by <c>--allowlist</c> or
/// <c>LADDER_DEVICE_ALLOWLIST</c> — which is not an override, it is the only way to name the file at
/// all, and with neither set every target is refused.</para>
/// </summary>
public static class RunTransitionFence
{
    /// <summary>
    /// Decide whether a RUN transition may be attempted against <paramref name="target"/>.
    /// </summary>
    /// <param name="target">The address the run would connect to. A routing hint, never an authorization.</param>
    /// <param name="allowlistPath">
    /// The resolved allowlist path, or null. Null is a REFUSAL — see
    /// <see cref="RunGate.NoAllowlistConfigured"/>.
    /// </param>
    public static RunTransitionDecision Check(string? target, string? allowlistPath)
    {
        if (string.IsNullOrWhiteSpace(target))
            return RunTransitionDecision.Refuse(RunGate.NoTarget, "no target device given.");

        if (string.IsNullOrWhiteSpace(allowlistPath))
        {
            return RunTransitionDecision.Refuse(RunGate.NoAllowlistConfigured,
                $"no allowlist configured. Pass --allowlist <path> or set {AllowlistPath.EnvVar}. " +
                "With neither, every target is refused and no socket is opened — an absent fence is " +
                "not an open one.");
        }

        var load = AllowlistFile.Load(allowlistPath);
        if (!load.Loaded)
        {
            return RunTransitionDecision.Refuse(RunGate.AllowlistUnusable,
                load.Message ?? "the allowlist could not be loaded.");
        }

        // Gate 1-2: listed, exact-matched, and marked test-rig. Delegated whole to the read guard.
        var read = new DeviceAccessGuard(load).Check(target);
        if (!read.Allowed)
        {
            return RunTransitionDecision.Refuse(RunGate.NotAnApprovedTestRig,
                $"{read.Reason}: {read.Message}");
        }

        var entry = read.MatchedEntry!;

        // Gate 3: the separate, deliberate grant. Being read-listed never implies writable, and being
        // a test rig never implies startable.
        if (!entry.WriteEligible)
        {
            return RunTransitionDecision.Refuse(RunGate.NotWriteEligible,
                $"'{target}' ({entry.DisplayLabel}) is an approved test rig for READING, but its " +
                "allowlist entry reads \"writeEligible\": false. Putting a CPU into RUN is a write to " +
                "the device. This field is a HUMAN AUTHORISATION and nothing here may edit it or " +
                "stand in for it: a person changes it in the allowlist, or the answer is no.", entry);
        }

        // Gate 4: the physical precondition (ADR-0009 fence item 3). The one gate no software bug can
        // cross, and the one that matters most for this operation — RUN is what makes outputs live.
        if (!entry.OutputsIsolated)
        {
            return RunTransitionDecision.Refuse(RunGate.OutputsNotIsolated,
                $"'{target}' ({entry.DisplayLabel}) is not asserted physically isolated. Putting this " +
                "CPU into RUN energises its outputs; a device that may be started must have its " +
                "outputs physically incapable of actuating — field wiring disconnected, or interposing " +
                "relays unpowered.", entry);
        }

        if (string.IsNullOrWhiteSpace(entry.IsolationAssertedBy))
        {
            return RunTransitionDecision.Refuse(RunGate.IsolationUnattributed,
                $"'{target}' ({entry.DisplayLabel}) claims physical isolation but names nobody as " +
                "having asserted it. An unattributed assertion is not an assertion.", entry);
        }

        // Gate 5: is there anything to check the answering CPU against? Asked here, before the socket,
        // because it is a fact about the ENTRY. The comparison itself happens after the connect.
        if (string.IsNullOrWhiteSpace(entry.OrderNumber))
        {
            return RunTransitionDecision.Refuse(RunGate.NoDeclaredIdentity,
                $"'{target}' ({entry.DisplayLabel}) declares no orderNumber, so nothing could verify " +
                "that the CPU answering this address is the one approved. The address is a routing " +
                "hint: on this network 10.10.10.10 is the standard PLC address at multiple sites and " +
                "which device answers depends on which tunnel is up.", entry);
        }

        return RunTransitionDecision.Allow(entry);
    }
}
