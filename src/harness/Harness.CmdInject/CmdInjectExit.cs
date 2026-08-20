namespace Harness.CmdInject;

/// <summary>Exit codes. Every one names a distinct fact; none of them means "something went wrong".</summary>
public enum CmdInjectExit
{
    /// <summary>Everything asked for succeeded.</summary>
    Ok = 0,

    /// <summary>The arguments do not describe a runnable request, OR no allowlist was configured. Nothing was contacted.</summary>
    Usage = 2,

    /// <summary>The map or the binding could not be loaded, or the binding did not resolve against the map. Nothing was contacted.</summary>
    MapRefused = 3,

    /// <summary>The allowlist could not be found, read or parsed. Fail closed.</summary>
    AllowlistUnusable = 4,

    /// <summary>The target is not on the allowlist, or is on it as something other than a test rig.</summary>
    NotApprovedTestRig = 5,

    /// <summary>The rig is approved for reading but not marked write-eligible. Today's live answer for the bench rig.</summary>
    NotWriteEligible = 6,

    /// <summary>The rig's outputs are not asserted physically isolated, or the assertion names nobody.</summary>
    NotIsolated = 7,

    /// <summary>No expected build stamp was declared, so nothing could confirm which CPU answers.</summary>
    NoExpectedBuildStamp = 8,

    /// <summary>The fence itself threw. A fault in the fence, not a verdict about the device.</summary>
    FenceFault = 9,

    /// <summary>
    /// <c>--arm</c> was absent, so the plan and the exact frames were printed and NOTHING was constructed
    /// or contacted. Not a failure — the deliberate resting state of every write verb.
    /// </summary>
    DryRunNotArmed = 10,

    /// <summary>A command's frames could not be built — an operand did not fit, or was missing. Nothing was contacted.</summary>
    FrameRefused = 11,

    /// <summary>
    /// <c>--arm</c> was given and the request was otherwise sound, but THIS build carries no transport — the
    /// socket adapter and session are phase 2. Nothing was contacted. Distinct from a refusal on purpose: the
    /// tool did not decide against the device, it simply cannot reach one yet.
    ///
    /// <para>The shipped build DOES carry a transport (<c>ModbusInjectionTransportFactory</c>), so this is
    /// no longer reachable from <c>Main</c>. It is kept because it is the honest answer whenever a caller
    /// supplies no factory, and because deleting it would renumber every code below it.</para>
    /// </summary>
    NoTransportInThisBuild = 12,

    /// <summary>The socket could not be opened, or the server did not answer. Nothing was written — a connection never made is not a device that refused.</summary>
    ConnectFailed = 13,

    /// <summary>
    /// The device's published build stamp is not the declared one, or it changed under the poll. Nothing
    /// further is written: the map this tool holds describes a different program, so every address is a guess.
    /// </summary>
    StampMismatch = 14,

    /// <summary>
    /// The command band cannot be captured or put back in one transaction, so no session was opened over
    /// it. A write that cannot be reversed is not attempted.
    /// </summary>
    NotRestorable = 15,

    /// <summary>
    /// The master enable read CLEAR at session open, so the block would not act on a command. Refused
    /// before any write. <c>--raise-enable</c> is the explicit consent to raise it inside the session.
    /// </summary>
    EnableClear = 16,

    /// <summary>
    /// The command was written and the acknowledgement count never advanced on our sequence. <b>An absence
    /// of evidence about the command, not a verdict on it</b> — and never a reason to send it again.
    /// </summary>
    NotAcknowledged = 17,

    /// <summary>
    /// The poll stopped on a fact about the DEVICE rather than about the command — a CPU restart, a failed
    /// read, or Ctrl-C. The command is not resent and the band is put back.
    /// </summary>
    Aborted = 18,

    /// <summary>
    /// 🔴 <b>THE LOUDEST CODE THIS TOOL HAS.</b> The restore did not complete or could not be verified, so
    /// the command band may be holding values nobody chose. It outranks every other outcome — a run that
    /// acknowledged perfectly and could not put the band back is a failed run.
    /// </summary>
    RestoreFailed = 19,
}

/// <summary>Maps a fence gate to the exit code that names it.</summary>
public static class CmdInjectExitMap
{
    public static CmdInjectExit ForGate(InjectionGate gate) => gate switch
    {
        InjectionGate.Allowed => CmdInjectExit.Ok,
        InjectionGate.NoTarget => CmdInjectExit.Usage,
        InjectionGate.NoAllowlistConfigured => CmdInjectExit.Usage,
        InjectionGate.AllowlistUnusable => CmdInjectExit.AllowlistUnusable,
        InjectionGate.NotAnApprovedTestRig => CmdInjectExit.NotApprovedTestRig,
        InjectionGate.NotWriteEligible => CmdInjectExit.NotWriteEligible,
        InjectionGate.OutputsNotIsolated => CmdInjectExit.NotIsolated,
        InjectionGate.IsolationUnattributed => CmdInjectExit.NotIsolated,
        InjectionGate.NoExpectedBuildStamp => CmdInjectExit.NoExpectedBuildStamp,
        _ => throw new ArgumentOutOfRangeException(nameof(gate), gate, "unmapped fence gate."),
    };
}
