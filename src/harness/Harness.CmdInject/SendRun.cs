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
/// <param name="Heartbeat">
/// How this session satisfies the block's arming gate — <b>which is not the same thing as <c>--arm</c></b>.
/// Null means <see cref="HeartbeatPlan.Default"/>: arming is done on every session, unconditionally,
/// because the tool cannot read whether the device needs it and a wrong guess is a command that cannot
/// succeed. See <see cref="HeartbeatPlan"/> for why the numbers are parameters and not constants.
/// </param>
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
    int PollIntervalMs = 250,
    HeartbeatPlan? Heartbeat = null)
{
    /// <summary>The heartbeat plan this run will use — the declared one, or the default.</summary>
    public HeartbeatPlan Arming => Heartbeat ?? HeartbeatPlan.Default;
}

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
/// stamp <i>before any write</i> → capture the restore point → <b>raise the enable</b> → <b>stamp the
/// heartbeat, each stamp separated by an observed scan advance</b> → read the prior acknowledgement count →
/// allocate a sequence → write → poll → <b>restore, on every exit path.</b></para>
///
/// <para>🔴 <b>THE ENABLE COMES BEFORE THE STAMPS AND THAT ORDER IS LOAD-BEARING, NOT TIDINESS.</b> The
/// heartbeat reaches the block only while the enable is up, so a stamp made first would land in memory,
/// count for nothing, and look from here exactly like one that worked. And the prior acknowledgement count
/// is read AFTER the arming, not before it: the count the verdict is measured against has to be the count
/// as it stands at the moment before our command, not before our arming writes.</para>
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
            return DryRun(options, resolution.Binding!, channel, request, ledger, output);

        return Armed(options, resolution.Binding!, channel, request, ledger, factory, clock ?? new RealInjectionClock(), cancel, output);
    }

    private static CmdInjectExit DryRun(
        SendOptions options, ResolvedBinding binding, ResolvedChannel channel, CommandRequest request,
        SequenceLedger ledger, TextWriter output)
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
        PrintArmingPlan(options.Arming, binding, output);
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
                output.WriteLine("  ignored — and an ignored command is indistinguishable from a rejected one at this end. The heartbeat");
                output.WriteLine("  reaches the block through the same gate, so arming cannot work either: a stamp made now would land in");
                output.WriteLine("  memory and count for nothing. Nothing was written. Pass --raise-enable to raise it inside this session");
                output.WriteLine("  (the restore drops it again on the way out).");
                return CmdInjectExit.EnableClear;
            }

            output.WriteLine("  --raise-enable given: raising the master enable inside this session, BEFORE any heartbeat stamp.");
            session.RaiseEnable();
        }

        // THE BLOCK'S ARMING GATE, satisfied here and nowhere else. Unconditional: nothing readable reports
        // whether the device needs it, so the alternative is a guess, and the redundant write is cheaper than
        // the wrong guess. See InjectionSession.StampHeartbeat.
        output.WriteLine();
        PrintArmingPlan(options.Arming, session.Binding, output);

        HeartbeatStampReport stamped;
        try
        {
            stamped = session.StampHeartbeat(options.Arming, cancel);
        }
        catch (Exception ex)
        {
            output.WriteLine($"== the heartbeat could not be stamped ({ex.GetType().Name}): {ex.Message} ==");
            return CmdInjectExit.Aborted;
        }

        output.WriteLine($"  arming  : {stamped.Ending} — {stamped.Message}");

        if (!stamped.MayCommand)
        {
            output.WriteLine("== REFUSED before the command: the block is not armed and this run cannot make it so ==");
            output.WriteLine("  No command was written. A command sent into an unarmed block comes back as NOT ACKNOWLEDGED, which is");
            output.WriteLine("  the same answer a wrong address gives — refusing here is what keeps those two apart.");
            return ForStampEnding(stamped.Ending);
        }

        output.WriteLine();

        ushort priorCount;
        try
        {
            // AFTER the arming, and before allocating a sequence. After, because the count the verdict is
            // measured against must be the count as it stands immediately before OUR command — arming writes
            // are writes, and a count read before them would be measuring across them. Before the allocation,
            // because a sequence picked without having read the device is one that could collide with what the
            // device has already acknowledged.
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
        {
            output.WriteLine($"  last reading: seq {(last.SeqMatches ? "matched" : "did not match")}, " +
                             $"count {(last.CountAdvanced ? "advanced" : "did not move")}, " +
                             $"code echo {last.AckCode ?? "(no role in this binding)"}, result {last.Result}");
            output.WriteLine("  The code echo and the result are REPORTED, never reasoned from: a refusal cascade publishes one value");
            output.WriteLine("  and the check written last wins, so a result code can mask every other reason a command was declined.");
        }
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

    /// <summary>
    /// Map an arming ending to an exit code. <b>Every one of these is a refusal BEFORE the command</b>, so
    /// none of them may look like an outcome the command had — an unarmed block that was never commanded is
    /// not a command that went unacknowledged.
    /// </summary>
    private static CmdInjectExit ForStampEnding(HeartbeatEnding ending) => ending switch
    {
        HeartbeatEnding.EnableClear => CmdInjectExit.EnableClear,
        HeartbeatEnding.StampChanged => CmdInjectExit.StampMismatch,
        HeartbeatEnding.ScanStalled => CmdInjectExit.Aborted,
        HeartbeatEnding.Restarted => CmdInjectExit.Aborted,
        HeartbeatEnding.ReadFailed => CmdInjectExit.Aborted,
        HeartbeatEnding.Cancelled => CmdInjectExit.Aborted,
        HeartbeatEnding.Stamped => CmdInjectExit.Ok,
        HeartbeatEnding.NotRequested => CmdInjectExit.Ok,
        _ => throw new ArgumentOutOfRangeException(nameof(ending), ending, "unmapped heartbeat ending."),
    };

    /// <summary>
    /// The arming plan, printed before it is carried out — and printed by the DRY RUN too, where it is the
    /// only description of the writes a real run would make before the command.
    /// </summary>
    private static void PrintArmingPlan(HeartbeatPlan plan, ResolvedBinding binding, TextWriter output)
    {
        output.WriteLine("== arming (the BLOCK's gate — not the same thing as --arm) ==");

        if (!binding.BandRoles.TryGetValue(InjectionRole.Heartbeat, out var tag))
        {
            output.WriteLine("  the binding declares no heartbeat role, so nothing can be stamped.");
            return;
        }

        if (!plan.Stamps)
        {
            output.WriteLine($"  heartbeat reg {tag.Register}: NOT stamped — zero changes were asked for. The block arms on");
            output.WriteLine("  heartbeat changes, so unless something else has stamped it the command will be seen and not processed.");
            return;
        }

        output.WriteLine($"  heartbeat reg {tag.Register}: {plan}");
        output.WriteLine("  each stamp is a CHANGE (values are generated from what the register is found holding, never supplied),");
        output.WriteLine("  and each is followed by a scan-counter advance READ BACK from the device — the block samples the");
        output.WriteLine("  heartbeat once per scan, so two writes inside one scan are one change or none.");
    }

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
