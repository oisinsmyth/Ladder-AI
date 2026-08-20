using Harness.Map;

namespace Harness.CmdInject;

/// <summary>What one <c>send</c> is: which command, against which channel, armed or not, and against what.</summary>
/// <param name="TagTablePath">The mirror tag table, from the job folder.</param>
/// <param name="AreaPointerPath">The block carrying <c>MB_HOLD_REG</c>, from the job folder.</param>
/// <param name="BindingPath">The role → tag binding, from the job folder.</param>
/// <param name="ChannelName">Which channel the command is aimed at.</param>
/// <param name="Operands">Role → value, as text. Parsed, range-checked and encoded by the frame builder.</param>
/// <param name="Armed">Whether <c>--arm</c> was present. Read before the verb is parsed.</param>
/// <param name="Target">The address. A routing hint, never an authorisation.</param>
/// <param name="AllowlistPath">Where the device allowlist is. There is no default path.</param>
/// <param name="ExpectedBuildStamp">The stamp the caller declares. Absent is a refusal, before the socket.</param>
/// <param name="Port">TCP port.</param>
/// <param name="UnitId">Modbus unit identifier.</param>
/// <param name="RaiseEnable">
/// Raise the master enable inside this session. <b>Needed in practice, and here is why:</b> the restore
/// runs on every exit path and leaves the enable DOWN, so no invocation can leave it up for the next one.
/// A command sent while the enable is clear is a command the block ignores, so a session that must send
/// one has to raise it itself. Off by default — raising a plant's master enable is not something to do
/// because a flag was omitted.
/// </param>
/// <param name="PollAttempts">How many acknowledgement reads before giving up. Giving up is an outcome.</param>
/// <param name="PollIntervalMs">Gap between poll reads.</param>
public sealed record SendOptions(
    string TagTablePath,
    string AreaPointerPath,
    string BindingPath,
    string ChannelName,
    IReadOnlyDictionary<InjectionRole, string> Operands,
    bool Armed,
    string? Target,
    string? AllowlistPath,
    uint? ExpectedBuildStamp,
    int Port,
    byte UnitId,
    bool RaiseEnable = false,
    int PollAttempts = 20,
    int PollIntervalMs = 250);

/// <summary>
/// The <c>send</c> verb.
///
/// <para>🔴 <b>WITHOUT <c>--arm</c> IT PRINTS THE PLAN AND THE EXACT FRAMES, CONSTRUCTS NO TRANSPORT, AND
/// EXITS 10.</b> A dry run reads nothing from the device — it prints <c>current: NOT READ</c> rather than
/// inventing a before-picture, because a dry run that opened a socket "just to read" would turn the arming
/// gate into a decoration. The sequence it shows is what the ledger WOULD allocate, peeked, never consumed.</para>
///
/// <para><b>With <c>--arm</c> it runs the fence first.</b> The fence is pure and above every line that
/// could open a session; the socket is opened only through the injected <see cref="IInjectionTransportFactory"/>,
/// so a test that supplies a recording factory can assert <c>opens == 0</c> on every refusal — the fence
/// holding as an observable fact, not an exit code a disconnected gate could also produce.</para>
///
/// <para><b>Past the fence, the order is fixed and every step of it is a gate:</b> open → confirm the build
/// stamp <i>before any write</i> → capture the restore point → check the enable → read the prior
/// acknowledgement count → allocate a sequence → write → poll → <b>restore, on every exit path.</b></para>
/// </summary>
public static class SendRun
{
    public static CmdInjectExit Execute(
        SendOptions options,
        SequenceLedger ledger,
        IInjectionTransportFactory? factory,
        TextWriter output,
        IInjectionClock? clock = null,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(output);

        output.WriteLine("harness-cmd-inject send — a command against ONE channel.");
        output.WriteLine("  Without --arm: the plan and the exact frames are printed, nothing is constructed, exit 10.");
        output.WriteLine();

        var resolution = Resolution.Load(options.TagTablePath, options.AreaPointerPath, options.BindingPath);
        if (!resolution.Ok)
        {
            output.WriteLine("== NOT RESOLVED ==");
            foreach (var refusal in resolution.Refusals)
                output.WriteLine($"  - {refusal}");
            output.WriteLine("  Nothing was contacted.");
            return CmdInjectExit.MapRefused;
        }

        var channel = resolution.Binding!.Channel(options.ChannelName);
        if (channel is null)
        {
            output.WriteLine("== usage ==");
            output.WriteLine($"  the binding has no channel named '{options.ChannelName}'. Known channels: " +
                             $"{string.Join(", ", resolution.Binding!.Channels.Select(c => c.Name))}.");
            output.WriteLine("  Nothing was contacted.");
            return CmdInjectExit.Usage;
        }

        var request = new CommandRequest(options.ChannelName, options.Operands);

        if (!options.Armed)
            return DryRun(channel, request, ledger, output);

        return Armed(options, resolution.Binding!, channel, request, ledger, factory, clock ?? new RealInjectionClock(), cancel, output);
    }

    private static CmdInjectExit DryRun(ResolvedChannel channel, CommandRequest request, SequenceLedger ledger, TextWriter output)
    {
        // Peek, never Allocate: a dry run must not consume a sequence, and it has not read the device to
        // know where the device's own counter stands.
        var wouldBeSequence = ledger.Peek();
        var build = CommandFrameBuilder.Build(channel, request, wouldBeSequence);

        if (!build.Ok)
        {
            output.WriteLine("== frames NOT built ==");
            foreach (var refusal in build.Refusals)
                output.WriteLine($"  - {refusal}");
            output.WriteLine("  Nothing was contacted.");
            return CmdInjectExit.FrameRefused;
        }

        output.WriteLine("== DRY RUN (no --arm) ==");
        output.WriteLine("  current: NOT READ — this invocation contacted nothing.");
        output.WriteLine($"  sequence that WOULD be allocated: {wouldBeSequence} (peeked, not consumed)");
        output.WriteLine();
        PrintFrames(build.Frames!, output);
        output.WriteLine();
        output.WriteLine("  --arm was not given, so no transport was constructed and nothing was written. Exit 10.");
        return CmdInjectExit.DryRunNotArmed;
    }

    private static CmdInjectExit Armed(
        SendOptions options, ResolvedBinding binding, ResolvedChannel channel, CommandRequest request,
        SequenceLedger ledger, IInjectionTransportFactory? factory, IInjectionClock clock,
        CancellationToken cancel, TextWriter output)
    {
        output.WriteLine("== --arm given: the fence is consulted BEFORE anything could open ==");

        InjectionDecision decision;
        try
        {
            decision = InjectionFence.Check(options.Target, options.AllowlistPath, options.ExpectedBuildStamp);
        }
        catch (Exception ex)
        {
            output.WriteLine($"  FenceFault ({ex.GetType().Name}): {ex.Message}");
            output.WriteLine("  REFUSED — no connection attempted. This is a FAULT IN THE FENCE, not a verdict about the device.");
            return CmdInjectExit.FenceFault;
        }

        output.WriteLine($"  gate    : {decision.Gate}");
        output.WriteLine($"  verdict : {decision.Message}");
        if (!decision.Allowed)
        {
            output.WriteLine("  REFUSED — no connection attempted.");
            return CmdInjectExitMap.ForGate(decision.Gate);
        }

        // The fence allowed. The ONLY route to a socket is the factory; a caller that supplies none gets an
        // honest "this build cannot reach a device" rather than a refusal it did not make.
        if (factory is null)
        {
            output.WriteLine("  ALLOWED — but no transport factory was supplied, so nothing was contacted. Exit 12.");
            return CmdInjectExit.NoTransportInThisBuild;
        }

        var sessionOptions = new SessionOptions(
            options.Target!, options.Port, options.UnitId,
            new BuildStamp(options.ExpectedBuildStamp!.Value),
            options.PollAttempts, options.PollIntervalMs);

        output.WriteLine($"  ALLOWED — opening a session to {options.Target}:{options.Port} unit {options.UnitId}.");
        output.WriteLine($"  expecting build stamp {sessionOptions.ExpectedStamp.Literal}, confirmed before any write.");

        var open = InjectionSession.Open(factory, binding, sessionOptions, clock);
        if (open.Session is null)
        {
            output.WriteLine($"== SESSION NOT OPENED: {open.Refusal} ==");
            output.WriteLine($"  {open.Message}");
            return ForRefusal(open.Refusal);
        }

        using var session = open.Session;
        output.WriteLine($"  session : {open.Message}");
        output.WriteLine();

        var exit = Act(options, channel, request, ledger, session, cancel, output);

        // The restore is called explicitly so its report is printed in order; Dispose calls it again and gets
        // the same answer. Every exit path above this line has already gone through it too — that is what
        // `using` is for here, and a refusal that wrote nothing writes nothing to put back.
        var restore = session.Restore();
        PrintRestore(restore, output);

        if (!restore.Ok)
        {
            output.WriteLine("  EXIT 19 — the restore outranks the command's own outcome. A run that could not put the band back is a failed run.");
            return CmdInjectExit.RestoreFailed;
        }

        return exit;
    }

    private static CmdInjectExit Act(
        SendOptions options, ResolvedChannel channel, CommandRequest request, SequenceLedger ledger,
        InjectionSession session, CancellationToken cancel, TextWriter output)
    {
        if (!session.EnableWasSet)
        {
            if (!options.RaiseEnable)
            {
                output.WriteLine("== REFUSED: the master enable reads CLEAR ==");
                output.WriteLine("  The block acts on a command only while the enable is set, so this command would be written and");
                output.WriteLine("  ignored — and an ignored command is indistinguishable from a rejected one at this end. Nothing was");
                output.WriteLine("  written. Pass --raise-enable to raise it inside this session (the restore drops it again on the way out).");
                return CmdInjectExit.EnableClear;
            }

            output.WriteLine("  --raise-enable given: raising the master enable inside this session.");
            session.RaiseEnable();
        }

        ushort priorCount;
        try
        {
            // BEFORE allocating a sequence: a sequence picked without having read the device is one that could
            // collide with what the device has already acknowledged, and the acknowledgement verdict is keyed
            // on this count moving.
            priorCount = session.ReadAckCount(channel);
        }
        catch (Exception ex)
        {
            output.WriteLine($"== the prior acknowledgement count could not be read ({ex.GetType().Name}): {ex.Message} ==");
            output.WriteLine("  Nothing was written: without a before-count there is nothing to observe a move against.");
            return CmdInjectExit.Aborted;
        }

        var sequence = ledger.Allocate();
        var build = CommandFrameBuilder.Build(channel, request, sequence);
        if (!build.Ok)
        {
            output.WriteLine("== frames NOT built ==");
            foreach (var refusal in build.Refusals)
                output.WriteLine($"  - {refusal}");
            output.WriteLine("  The session was opened but no command was written.");
            return CmdInjectExit.FrameRefused;
        }

        output.WriteLine($"  prior acknowledgement count: {priorCount}");
        output.WriteLine();
        PrintFrames(build.Frames!, output);
        output.WriteLine();

        session.Apply(build.Frames!);
        output.WriteLine($"  applied: operands first, then sequence {sequence} alone.");
        output.WriteLine();

        output.WriteLine("== polling for the acknowledgement — keyed on the COUNT, never on the result ==");
        var wait = session.WaitForAcknowledgement(channel, sequence, priorCount, cancel, output);

        output.WriteLine();
        output.WriteLine($"== {wait.Ending} after {wait.Attempts} poll(s) ==");
        output.WriteLine($"  {wait.Message}");
        if (wait.Last is { } last)
            output.WriteLine($"  last reading: seq {(last.SeqMatches ? "matched" : "did not match")}, count {(last.CountAdvanced ? "advanced" : "did not move")}, result {last.Result}");
        output.WriteLine("  The command is NOT resent under any ending. This tool sends once and reports what it saw.");
        output.WriteLine();

        return wait.Ending switch
        {
            WaitEnding.Acknowledged => CmdInjectExit.Ok,
            WaitEnding.Superseded => CmdInjectExit.NotAcknowledged,
            WaitEnding.NotAcknowledged => CmdInjectExit.NotAcknowledged,
            WaitEnding.StampChanged => CmdInjectExit.StampMismatch,
            WaitEnding.Restarted => CmdInjectExit.Aborted,
            WaitEnding.Cancelled => CmdInjectExit.Aborted,
            WaitEnding.ReadFailed => CmdInjectExit.Aborted,
            _ => CmdInjectExit.Aborted,
        };
    }

    private static CmdInjectExit ForRefusal(SessionRefusal refusal) => refusal switch
    {
        SessionRefusal.BandNotRestorable => CmdInjectExit.NotRestorable,
        SessionRefusal.RestorePointUnreadable => CmdInjectExit.NotRestorable,
        SessionRefusal.ControlUnreadable => CmdInjectExit.NotRestorable,
        SessionRefusal.ConnectFailed => CmdInjectExit.ConnectFailed,
        SessionRefusal.StampMismatch => CmdInjectExit.StampMismatch,
        SessionRefusal.None => CmdInjectExit.Ok,
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "unmapped session refusal."),
    };

    private static void PrintRestore(RestoreReport restore, TextWriter output)
    {
        output.WriteLine(restore.Ok ? "== restore: DONE and VERIFIED ==" : "== restore: FAILED ==");
        foreach (var step in restore.Steps)
            output.WriteLine($"  - {step}");

        if (restore.Failure is not null)
            output.WriteLine($"  🔴 {restore.Failure}");
    }

    /// <summary>Print both transactions, register by register and byte by byte (Modbus is big-endian per register).</summary>
    private static void PrintFrames(CommandFrames frames, TextWriter output)
    {
        output.WriteLine("== frames (exact bytes) ==");
        PrintTransaction(frames.Operands, output);
        PrintTransaction(frames.SequenceWrite, output);

        output.WriteLine();
        output.WriteLine("  ordering  : operands FIRST, sequence ALONE second — a tear cannot manufacture a command.");
        output.WriteLine($"  disjoint  : the sequence write covers only register {frames.SequenceWrite.Target.Register}, " +
                         $"the operand write covers {frames.Operands.Target.Register}..{frames.Operands.Target.LastRegister}; no write covers both.");
    }

    private static void PrintTransaction(CommandTransaction transaction, TextWriter output)
    {
        output.WriteLine($"  {transaction.Purpose}");
        output.WriteLine($"    target: {transaction.Target}  ({transaction.Values.Count} register(s))");
        for (var i = 0; i < transaction.Values.Count; i++)
        {
            var value = transaction.Values[i];
            var high = (byte)(value >> 8);
            var low = (byte)(value & 0xFF);
            output.WriteLine($"    reg {transaction.Target.Register + i,4} = 0x{value:X4}   bytes {high:X2} {low:X2}");
        }
    }
}
