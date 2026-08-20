using Harness.Map;
using Harness.Wire;

// Both Harness.Map and Harness.MirrorView define a MirrorTag; the one this tool resolves against is the
// map parser's, and the alias says so once rather than at every use.
using MirrorTag = Harness.MirrorView.MirrorTag;

namespace Harness.CmdInject;

/// <summary>
/// Waiting, as an injected dependency — <b>so a poll loop can be tested without the test taking as long
/// as the poll.</b> The production implementation is the only one that sleeps.
/// </summary>
public interface IInjectionClock
{
    /// <summary>Wait, or return early if cancelled. Never throws on cancellation — the caller decides.</summary>
    void Wait(int milliseconds, CancellationToken cancel);
}

/// <summary>The clock the composition root supplies.</summary>
public sealed class RealInjectionClock : IInjectionClock
{
    public void Wait(int milliseconds, CancellationToken cancel)
    {
        if (milliseconds <= 0) return;
        cancel.WaitHandle.WaitOne(milliseconds);
    }
}

/// <summary>What one session is aimed at, and how long it will wait to be told what happened.</summary>
/// <param name="Host">The address. Authorised by the fence before this record ever reaches a factory.</param>
/// <param name="Port">TCP port.</param>
/// <param name="UnitId">Modbus unit identifier.</param>
/// <param name="ExpectedStamp">
/// The build stamp the caller declares. <b>Compared after connect and before any write, and again on
/// every poll batch.</b> Not optional: the fence refuses before the socket when none is declared.
/// </param>
/// <param name="PollAttempts">How many acknowledgement reads before giving up. Giving up is an outcome, not a failure to have one.</param>
/// <param name="PollIntervalMs">Gap between poll reads.</param>
public sealed record SessionOptions(
    string Host,
    int Port,
    byte UnitId,
    BuildStamp ExpectedStamp,
    int PollAttempts,
    int PollIntervalMs);

/// <summary>Why a session could not be opened. <see cref="None"/> only when one was.</summary>
public enum SessionRefusal
{
    /// <summary>A session was opened.</summary>
    None = 0,

    /// <summary>The band cannot be put back in one write, so no session may be opened over it. Decided BEFORE the socket.</summary>
    BandNotRestorable,

    /// <summary>The socket could not be opened, or the server did not answer.</summary>
    ConnectFailed,

    /// <summary>The stamp/scan registers could not be read. Nothing was written.</summary>
    ControlUnreadable,

    /// <summary>
    /// 🔴 <b>THE DEVICE ANSWERING IS NOT RUNNING THE PROGRAM THIS BINDING DESCRIBES.</b> Nothing was
    /// written and nothing will be: every register this tool holds is derived from a map that describes
    /// some other program, so every address it would write to is a guess.
    /// </summary>
    StampMismatch,

    /// <summary>The command band could not be read, so there is no restore point. Nothing was written.</summary>
    RestorePointUnreadable,
}

/// <summary>A session, or the reason there is not one. Never both.</summary>
public sealed record SessionOpen(InjectionSession? Session, SessionRefusal Refusal, string Message)
{
    public static SessionOpen Opened(InjectionSession session, string message) =>
        new(session, SessionRefusal.None, message);

    public static SessionOpen Refused(SessionRefusal refusal, string message) =>
        new(null, refusal, message);
}

/// <summary>
/// How a poll loop ended. <b>The four acknowledgement outcomes are unchanged and still the only four</b>
/// (<see cref="AckOutcome"/>); these are the ways a loop stops, which is a different question — two of
/// them are facts about the DEVICE's identity and liveness rather than about the command.
/// </summary>
public enum WaitEnding
{
    /// <summary>The count advanced on our sequence. The command was processed.</summary>
    Acknowledged,

    /// <summary>The count advanced on someone else's sequence. Stop: another writer, or the device moved past us.</summary>
    Superseded,

    /// <summary>Every attempt was made and the count never advanced on our sequence. <b>Not a failure of the command — an absence of evidence about it.</b></summary>
    NotAcknowledged,

    /// <summary>🔴 The build stamp changed under the poll. A download landed; every address is now a guess.</summary>
    StampChanged,

    /// <summary>🔴 The scan counter went backwards. The CPU restarted. <b>No resend</b> — the command's fate is unknown and repeating it could double it.</summary>
    Restarted,

    /// <summary>Ctrl-C. The poll stopped where it was; the restore still runs.</summary>
    Cancelled,

    /// <summary>A poll read failed. Silence is not a verdict, so nothing is concluded from it.</summary>
    ReadFailed,
}

/// <summary>How one poll loop ended, with the last thing it actually saw.</summary>
/// <param name="Ending">The ending.</param>
/// <param name="Last">The last acknowledgement classification, when a reading was obtained at all.</param>
/// <param name="Attempts">How many reads were made — the denominator behind the ending.</param>
/// <param name="Message">The ending in words.</param>
public sealed record AckWait(WaitEnding Ending, AckClassification? Last, int Attempts, string Message);

/// <summary>
/// What the restore did, step by step, and whether the re-read confirmed it.
///
/// <para>🔴 <b>A RESTORE THAT CANNOT BE VERIFIED IS AN ERROR, NEVER A WARNING.</b> The whole claim of
/// reversibility rests on the band being back as it was found; an unverified restore is a band that may
/// be dirty, and a dirty command band is a plant surface holding values nobody chose.</para>
/// </summary>
public sealed record RestoreReport(bool Ok, IReadOnlyList<string> Steps, string? Failure)
{
    /// <summary>Nothing was written, so there is nothing to put back. Not a restore, and not a failure.</summary>
    public static RestoreReport NothingWritten { get; } =
        new(true, new[] { "nothing was written in this session, so the band was never dirtied and no write was made to put it back." }, null);
}

/// <summary>
/// One connected conversation with the device: <b>connect → confirm identity → capture the restore point
/// → act → poll → restore.</b>
///
/// <para>🔴 <b>ONE SOCKET, BECAUSE <c>MB_SERVER</c> ACCEPTS ONE CONNECTION.</b> Reading and writing share
/// this session. A writer that cannot read is not half a loop — it is a tool that can never tell you
/// whether anything happened. (Measured: a viewer attached alongside a wave produced
/// <c>0 of 22 vectors attempted</c>.)</para>
///
/// <para>🔴 <b>THE IDENTITY CHECK IS THE BUILD STAMP, AND IT IS MADE BEFORE ANY WRITE.</b> There is no
/// Modbus identity read, but the harness program publishes a 32-bit stamp at registers 0–1 and it is
/// already on the wire. An address is a routing hint — the same one is standard at multiple sites, and
/// which controller answers depends on which tunnel is up. If the stamp is not the declared one, the map
/// this tool is holding describes a different program and <b>every address it has is a guess</b>: the
/// session refuses, having written nothing. The comparison is repeated on every poll batch, because a
/// download can land between the first write and the acknowledgement.</para>
///
/// <para>🔴 <b>THE RESTORE POINT IS THE BAND AS THIS SESSION'S OWN OPENING READ FOUND IT</b> — not a
/// file, not a previous run's idea of it. It is captured before the first write and put back on every
/// exit path that wrote anything, including Ctrl-C, and the restore <b>re-reads to verify itself</b>.</para>
///
/// <para>⚠️ <b>THE ENABLE IS DROPPED FIRST AND IS LEFT DOWN — AND THAT IS NOT A FAITHFUL REVERSAL.</b>
/// Restoring a live command surface is the failure mode: the band image contains an OLD sequence value,
/// and a sequence <i>inequality</i> is exactly what makes the block execute. Writing that image back
/// while the enable is up would re-execute a stale command, so the enable is cleared first and the image
/// is written with the enable bit held clear. If the enable was up when the session opened, it is down
/// when the session ends, and the report says so rather than pretending the device was left untouched.
/// There is no ordering in which a one-write restore can both put an old sequence back and leave the
/// surface live without risking that execution.</para>
/// </summary>
public sealed class InjectionSession : IDisposable
{
    private readonly IInjectionTransport _transport;
    private readonly ResolvedBinding _binding;
    private readonly SessionOptions _options;
    private readonly IInjectionClock _clock;
    private readonly ushort[] _restorePoint;
    private readonly ScanCount _openingScan;

    private bool _wrote;
    private bool _restoreRun;
    private RestoreReport _restore = RestoreReport.NothingWritten;

    private InjectionSession(
        IInjectionTransport transport, ResolvedBinding binding, SessionOptions options,
        IInjectionClock clock, ushort[] restorePoint, ScanCount openingScan)
    {
        _transport = transport;
        _binding = binding;
        _options = options;
        _clock = clock;
        _restorePoint = restorePoint;
        _openingScan = openingScan;
    }

    /// <summary>The band exactly as the opening read found it. A copy — nothing can edit the restore point.</summary>
    public IReadOnlyList<ushort> RestorePoint => _restorePoint.ToArray();

    /// <summary>The scan counter at open, the baseline every restart check is made against.</summary>
    public ScanCount OpeningScan => _openingScan;

    /// <summary>
    /// Whether the master enable read as SET in the opening capture.
    ///
    /// <para>Read from the restore point rather than from a fresh read, on purpose: the whole session
    /// reasons about the band as it was found, and a second read could disagree with the image the restore
    /// will put back.</para>
    /// </summary>
    public bool EnableWasSet =>
        (_restorePoint[Offset(EnableTag.Register)] & (1 << EnableTag.BitInRegister)) != 0;

    /// <summary>Whether this session has written anything. Only a session that wrote has anything to put back.</summary>
    public bool HasWritten => _wrote;

    /// <summary>What the restore did. Meaningful once the session is disposed or <see cref="Restore"/> is called.</summary>
    public RestoreReport RestoreResult => _restore;

    // ---- opening ---------------------------------------------------------------------------------

    /// <summary>
    /// Can this binding's command band be put back in one write? <b>Asked before the socket</b>, because
    /// it is a property of the binding: a band that cannot be restored must never be connected to, let
    /// alone written.
    /// </summary>
    /// <returns>Null when the band is restorable; the reason when it is not.</returns>
    public static string? Unrestorable(ResolvedBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        var band = binding.CommandBand;

        if (band.RegisterCount > ModbusLimits.MaxWriteRegisters)
        {
            return $"the declared command band {band} is wider than the {ModbusLimits.MaxWriteRegisters}-register FC16 limit, so it " +
                   "cannot be put back in one write. A restore split across two writes can be interrupted between them, leaving the " +
                   "surface half-old — which is the one state the restore exists to make impossible. Narrow the band, or split the " +
                   "tool's reversibility model deliberately rather than by accident.";
        }

        if (band.RegisterCount > ModbusLimits.MaxReadRegisters)
        {
            return $"the declared command band {band} is wider than the {ModbusLimits.MaxReadRegisters}-register FC03 limit, so the " +
                   "restore point cannot be captured in one read.";
        }

        if (!binding.BandRoles.ContainsKey(InjectionRole.Enable))
        {
            return "the binding declares no enable role, so the restore has no register to drop before putting the band back. " +
                   "Restoring a live command surface re-executes a stale sequence; without an enable there is no way not to.";
        }

        return null;
    }

    /// <summary>
    /// Open a session: connect, confirm the stamp <b>before any write</b>, and capture the restore point.
    /// Every failure disposes the transport and returns a refusal — a half-open session is never handed back.
    /// </summary>
    public static SessionOpen Open(
        IInjectionTransportFactory factory, ResolvedBinding binding, SessionOptions options, IInjectionClock clock)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        if (Unrestorable(binding) is { } unrestorable)
            return SessionOpen.Refused(SessionRefusal.BandNotRestorable, unrestorable);

        IInjectionTransport transport;
        try
        {
            transport = factory.Open(options.Host, options.Port, options.UnitId);
        }
        catch (Exception ex)
        {
            return SessionOpen.Refused(SessionRefusal.ConnectFailed,
                $"could not open a session to {options.Host}:{options.Port} unit {options.UnitId} ({ex.GetType().Name}): {ex.Message}. " +
                "Nothing was written. A connection that was never made is not a device that refused.");
        }

        ushort[] control;
        try
        {
            control = transport.ReadRegisters(ControlRegisters.BuildStamp, ControlRegisters.Count);
        }
        catch (Exception ex)
        {
            transport.Dispose();
            return SessionOpen.Refused(SessionRefusal.ControlUnreadable,
                $"the build stamp and scan counter at registers 0..{ControlRegisters.Count - 1} could not be read " +
                $"({ex.GetType().Name}): {ex.Message}. Nothing was written — an identity that could not be read is not a matching one.");
        }

        if (control.Length < ControlRegisters.Count)
        {
            transport.Dispose();
            return SessionOpen.Refused(SessionRefusal.ControlUnreadable,
                $"the control read returned {control.Length} register(s) instead of {ControlRegisters.Count}. Empty is not clean: " +
                "a short read is a different answer, not a partial one. Nothing was written.");
        }

        var observedStamp = ControlRegisters.StampFrom(control);
        if (observedStamp != options.ExpectedStamp)
        {
            transport.Dispose();
            return SessionOpen.Refused(SessionRefusal.StampMismatch,
                $"the device at {options.Host}:{options.Port} publishes build stamp {observedStamp.Literal}; this run declared " +
                $"{options.ExpectedStamp.Literal}. That is not a version quibble: the register map this tool is holding was derived " +
                "for the declared build, so against a different one EVERY address it would write to is a guess. Nothing was written." +
                (RegisterWords.Swapped(observedStamp.Value) == options.ExpectedStamp.Value
                    ? " (The two halves are each other's swap, which is what a word-order error looks like — but a stamp that only " +
                      "matches when re-ordered is still not a match, and this is not the place to decide which is right.)"
                    : string.Empty));
        }

        var band = binding.CommandBand;
        ushort[] captured;
        try
        {
            captured = transport.ReadRegisters(band.FirstRegister, band.RegisterCount);
        }
        catch (Exception ex)
        {
            transport.Dispose();
            return SessionOpen.Refused(SessionRefusal.RestorePointUnreadable,
                $"the command band {band} could not be read ({ex.GetType().Name}): {ex.Message}. Without a restore point there is " +
                "nothing to put the band back to, so nothing is written.");
        }

        if (captured.Length != band.RegisterCount)
        {
            transport.Dispose();
            return SessionOpen.Refused(SessionRefusal.RestorePointUnreadable,
                $"the restore point read returned {captured.Length} register(s) for a band of {band.RegisterCount}. A short restore " +
                "point would put back registers it never read. Nothing was written.");
        }

        var session = new InjectionSession(
            transport, binding, options, clock, captured, ControlRegisters.ScanFrom(control));

        return SessionOpen.Opened(session,
            $"stamp {observedStamp.Literal} confirmed before any write; restore point captured over {band} " +
            $"({band.RegisterCount} register(s)); enable reads {(session.EnableWasSet ? "SET" : "CLEAR")}; " +
            $"scan counter at {session.OpeningScan}.");
    }

    // ---- acting ----------------------------------------------------------------------------------

    /// <summary>Read one register out of the observation side — the prior acknowledgement count, before the send.</summary>
    public ushort ReadAckCount(ResolvedChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        var tag = channel.ObservationRoles[InjectionRole.AckCount];
        var words = _transport.ReadRegisters(tag.Register, 1);

        if (words.Length != 1)
            throw new WireException($"the prior acknowledgement count at register {tag.Register} read back {words.Length} register(s).");

        return words[0];
    }

    /// <summary>
    /// Raise the master enable. <b>A write, and therefore something the restore will have to undo</b> —
    /// which it does by leaving the enable down.
    /// </summary>
    public void RaiseEnable()
    {
        var tag = EnableTag;
        var word = (ushort)(_restorePoint[Offset(tag.Register)] | (ushort)(1 << tag.BitInRegister));
        Write(new CommandTransaction(
            InjectionWriteTarget.Enable(_binding), new[] { word },
            $"raise the master enable — bit {tag.BitInRegister} of register {tag.Register}"));
    }

    /// <summary>Apply a built command: operands first, sequence alone second.</summary>
    public void Apply(CommandFrames frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        _wrote = true;
        InjectionDispatch.Apply(_transport, frames);
    }

    /// <summary>
    /// Poll for the acknowledgement — <b>re-checking identity and liveness on every batch</b>, and never
    /// resending. A command whose fate is unknown is reported as unknown; sending it again is how one
    /// command becomes two.
    /// </summary>
    public AckWait WaitForAcknowledgement(
        ResolvedChannel channel, ushort sentSequence, ushort priorCount, CancellationToken cancel, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(output);

        var ackTags = channel.ObservationRoles;
        var ackFirst = ackTags.Values.Min(t => t.Register);
        var ackLast = ackTags.Values.Max(t => t.LastRegister);
        var plan = PollPlan.For(ackFirst, ackLast);

        output.WriteLine($"  poll plan : {plan.Reads.Count} FC03 per batch — {plan.Basis}");

        AckClassification? last = null;

        for (var attempt = 1; attempt <= _options.PollAttempts; attempt++)
        {
            if (cancel.IsCancellationRequested)
            {
                return new AckWait(WaitEnding.Cancelled, last, attempt - 1,
                    "cancelled while polling. The command was written and its fate is unknown; it is NOT resent, and the band is put back.");
            }

            if (attempt > 1)
                _clock.Wait(_options.PollIntervalMs, cancel);

            Dictionary<int, ushort> registers;
            try
            {
                registers = ReadPlan(plan);
            }
            catch (Exception ex)
            {
                return new AckWait(WaitEnding.ReadFailed, last, attempt,
                    $"a poll read failed ({ex.GetType().Name}): {ex.Message}. Nothing is concluded from a silence — the command may " +
                    "or may not have been processed, and it is not resent.");
            }

            var control = new[]
            {
                registers[0], registers[1], registers[2], registers[3],
            };

            var stamp = ControlRegisters.StampFrom(control);
            if (stamp != _options.ExpectedStamp)
            {
                return new AckWait(WaitEnding.StampChanged, last, attempt,
                    $"the build stamp changed under the poll — it now reads {stamp.Literal}, not the declared {_options.ExpectedStamp.Literal}. " +
                    "A download landed. Every address this tool holds now describes a different program, so nothing further is written " +
                    "and nothing is concluded about the command.");
            }

            var scan = ControlRegisters.ScanFrom(control);
            if (!scan.IsPlausibleAdvanceFrom(_openingScan))
            {
                return new AckWait(WaitEnding.Restarted, last, attempt,
                    $"the scan counter reads {scan}, which is not a forward advance from {_openingScan} — the CPU restarted. " +
                    "The command is NOT resent: its fate before the restart is unknown, and repeating it is how one command becomes two.");
            }

            var observed = new AckObservation(
                registers[ackTags[InjectionRole.AckSeq].Register],
                registers[ackTags[InjectionRole.AckCount].Register],
                ResultText(registers[ackTags[InjectionRole.AckResult].Register]));

            last = AckModel.Classify(sentSequence, priorCount, observed);

            output.WriteLine($"    poll {attempt,3}: ackSeq={observed.AckSeq} ackCount={observed.AckCount} " +
                             $"result={observed.Result} -> {last.Outcome}");

            switch (last.Outcome)
            {
                case AckOutcome.Acknowledged:
                    return new AckWait(WaitEnding.Acknowledged, last, attempt,
                        $"the acknowledgement count advanced from {priorCount} to {observed.AckCount} on sequence {sentSequence}. " +
                        $"The result register reads {observed.Result}, reported verbatim and not used to reach this verdict.");

                case AckOutcome.Superseded:
                    return new AckWait(WaitEnding.Superseded, last, attempt,
                        $"the count advanced to {observed.AckCount} but on sequence {observed.AckSeq}, not ours ({sentSequence}). " +
                        "Another command was processed — a competing writer, or the device has already moved past ours. This is not " +
                        "our acknowledgement and it is not treated as one.");

                case AckOutcome.Pending:
                case AckOutcome.Incoherent:
                default:
                    continue;
            }
        }

        return new AckWait(WaitEnding.NotAcknowledged, last, _options.PollAttempts, Unacknowledged(last, sentSequence, priorCount));
    }

    private static string Unacknowledged(AckClassification? last, ushort sentSequence, ushort priorCount) => last switch
    {
        null => "no acknowledgement reading was obtained at all. Empty is not clean: this says nothing about the command.",

        // The refusal signature: our sequence echoed, the count never moved. The block SAW the command and did
        // not process it. Deliberately not a fifth ack outcome — the 2x2 already names this cell, and what makes
        // it a refusal rather than a torn read is that it persisted across every attempt.
        { Outcome: AckOutcome.Incoherent } =>
            $"the device echoed our sequence ({sentSequence}) on every attempt and its processed-count never moved from {priorCount}. " +
            $"The command was SEEN and not processed. The result register reads {last.Result} — reported verbatim, and NOT what this " +
            "verdict is keyed on. This is not a success with a caveat, and it is not a reason to send again.",

        _ =>
            $"the processed-count never moved from {priorCount} and sequence {sentSequence} was never echoed. The command may not have " +
            "been seen. 'The result register did not change' is NOT evidence that nothing happened — it is evidence of nothing at all, " +
            "because the block holds its result until the next command it processes.",
    };

    // ---- restoring -------------------------------------------------------------------------------

    /// <summary>
    /// Put the band back: <b>drop the enable, write the captured image with the enable held clear, re-read
    /// and verify.</b> Idempotent — the second call reports the first call's answer.
    /// </summary>
    public RestoreReport Restore()
    {
        if (_restoreRun) return _restore;
        _restoreRun = true;

        if (!_wrote)
        {
            _restore = RestoreReport.NothingWritten;
            return _restore;
        }

        var steps = new List<string>();
        var band = _binding.CommandBand;
        var enableTag = EnableTag;
        var enableOffset = Offset(enableTag.Register);
        var enableDown = (ushort)(_restorePoint[enableOffset] & ~(1 << enableTag.BitInRegister));

        try
        {
            // 1. THE ENABLE FIRST. Writing the captured image back while the surface is live re-executes a
            //    stale sequence, because the image carries the sequence value from before this session.
            Write(new CommandTransaction(
                InjectionWriteTarget.Enable(_binding), new[] { enableDown },
                $"drop the master enable — register {enableTag.Register}, bit {enableTag.BitInRegister}"));
            steps.Add($"enable dropped first (register {enableTag.Register} := 16#{enableDown:X4})");

            // 2. THE BAND, in one write, with the enable held clear so the restore cannot raise it again.
            var intended = _restorePoint.ToArray();
            intended[enableOffset] = enableDown;

            Write(new CommandTransaction(
                InjectionWriteTarget.CommandBand(_binding), intended,
                $"restore the command band {band} to the session's opening read"));
            steps.Add($"band {band} restored from the opening read ({intended.Length} register(s), one FC16)");

            // 3. VERIFY. A restore that cannot be verified is an error.
            var readBack = _transport.ReadRegisters(band.FirstRegister, band.RegisterCount);

            if (readBack.Length != intended.Length)
            {
                _restore = new RestoreReport(false, steps,
                    $"the verifying re-read returned {readBack.Length} register(s) for a band of {intended.Length}. The band may be " +
                    "dirty and this run cannot say it is not.");
                return _restore;
            }

            for (var i = 0; i < intended.Length; i++)
            {
                if (readBack[i] == intended[i]) continue;

                _restore = new RestoreReport(false, steps,
                    $"register {band.FirstRegister + i} reads 16#{readBack[i]:X4} after the restore; the restore point says " +
                    $"16#{intended[i]:X4}. The band is DIRTY — a command surface is holding a value nobody chose. This is an error, " +
                    "not a warning: fix it at the device before anything else is written.");
                return _restore;
            }

            steps.Add($"verified by re-read: all {intended.Length} register(s) match the restore point");

            if (EnableWasSet)
            {
                steps.Add("NOTE: the enable was SET when this session opened and is now CLEAR. That is deliberate — writing the " +
                          "captured image back under a live enable would re-execute the sequence the image carries — and it means the " +
                          "device was NOT left exactly as it was found.");
            }

            _restore = new RestoreReport(true, steps, null);
            return _restore;
        }
        catch (Exception ex)
        {
            _restore = new RestoreReport(false, steps,
                $"the restore failed ({ex.GetType().Name}): {ex.Message}. The command band may be dirty and this run cannot say it " +
                "is not. This is an error, not a warning.");
            return _restore;
        }
    }

    /// <summary>Restore, then close the socket. The restore runs here so no exit path can skip it.</summary>
    public void Dispose()
    {
        Restore();
        _transport.Dispose();
    }

    // ---- the small print -------------------------------------------------------------------------

    private MirrorTag EnableTag => _binding.BandRoles[InjectionRole.Enable];

    private int Offset(int register) => register - _binding.CommandBand.FirstRegister;

    private void Write(CommandTransaction transaction)
    {
        _wrote = true;
        InjectionDispatch.Apply(_transport, transaction);
    }

    private Dictionary<int, ushort> ReadPlan(PollPlan plan)
    {
        var registers = new Dictionary<int, ushort>();

        foreach (var span in plan.Reads)
        {
            var words = _transport.ReadRegisters(span.First, span.Count);

            if (words.Length != span.Count)
                throw new WireException($"a poll read of {span} returned {words.Length} register(s). A short read is a different answer, not a partial one.");

            for (var i = 0; i < words.Length; i++)
                registers[span.First + i] = words[i];
        }

        return registers;
    }

    /// <summary>
    /// The result register, rendered exactly as it arrived — the signed value and the raw word.
    ///
    /// <para>🔴 It travels as a <c>string</c> so a call site that tried to compare it to an integer would
    /// not compile. What the codes mean is job data that changes with the plant; a second copy of that
    /// meaning here would drift from the block and eventually refuse what the block would accept.</para>
    /// </summary>
    private static string ResultText(ushort raw) => $"{unchecked((short)raw)} (16#{raw:X4})";
}
