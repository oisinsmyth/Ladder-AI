using Harness.CmdInject;
using Harness.Map;

namespace Harness.CmdInject.Tests;

/// <summary>
/// L3 — the client driven against <see cref="FakePlc"/>, on the cases where <b>the device tells the truth
/// and the truth is misleading.</b>
///
/// <para>🔴 <b>SAY IT HERE AS WELL AS IN THE MODEL: THIS IS EVIDENCE ABOUT THE CLIENT AND NEVER ABOUT THE
/// PLC.</b> The model and the client were written from one document, by one hand. A green run here says
/// only: <i>given a device that behaves as the document says, this client draws the right conclusion.</i>
/// It says nothing whatever about whether the device behaves that way. This repository has a recorded
/// instance of two components failing together because they read the same register — that is the shape of
/// check this is, and naming it is the only defence available offline. <b>The loop closes on the rig and
/// nowhere else; until then no result here may be written up as "verified".</b></para>
///
/// <para>🔴 <b>AND ONE THING THIS FILE USED TO GET WRONG, RECORDED BECAUSE IT IS THE FAILURE THIS PROJECT
/// EXISTS TO AVOID.</b> Every test here used to arm the model by calling a helper on it directly — the
/// suite supplying the exact capability the client was missing. Ninety-nine tests passed over a client
/// that could not arm anything, the tool's own arming factory had zero callers, and the first place it
/// could have failed was the rig, where it would have shown up as a command refused with no path to any
/// other answer. <b>Every command below is armed BY THE CLIENT, inside its own session.</b> If the client
/// stops being able to arm, these tests stop passing — which is the only property that made them worth
/// having.</para>
/// </summary>
public class FakePlcProtocolTests
{
    private static readonly BuildStamp Stamp = new(0x5150C0DE);

    private static SessionOptions Options(int attempts = 6) =>
        new("127.0.0.1", 503, 1, Stamp, attempts, PollIntervalMs: 0);

    private sealed record Rig(FakePlc Plc, RecordingFactory Factory, ResolvedBinding Binding, ResolvedChannel Channel);

    private static Rig NewRig(bool enabled, bool laggingResult = false)
    {
        var binding = Fixtures.DefaultResolved();
        var channel = binding.Channel(Fixtures.ChannelName)!;
        var plc = new FakePlc(binding, channel, Stamp) { ResultPublicationLagsOneRead = laggingResult };

        plc.SetEnable(enabled);

        return new Rig(plc, new RecordingFactory(plc), binding, channel);
    }

    /// <summary>
    /// One command, start to finish, <b>with the arming done by the client under test</b>.
    ///
    /// <para><paramref name="arming"/> is the whole variable this suite turns: the default plan arms, and
    /// a plan that stamps once or not at all is how the "the client cannot arm" cases are expressed —
    /// through the tool, never behind its back.</para>
    /// </summary>
    private static (WaitEnding Ending, AckClassification? Last, ushort Sequence, HeartbeatStampReport Stamped) Send(
        Rig rig, SequenceLedger ledger, HeartbeatPlan? arming = null, int attempts = 6)
    {
        var plan = arming ?? HeartbeatPlan.Default;
        var session = InjectionSession.Open(rig.Factory, rig.Binding, Options(attempts), new InstantClock()).Session!;

        var stamped = session.StampHeartbeat(plan, CancellationToken.None);

        var prior = session.ReadAckCount(rig.Channel);
        var sequence = ledger.Allocate();

        session.Apply(CommandFrameBuilder.Build(rig.Channel, Request(), sequence).Frames!);
        var wait = session.WaitForAcknowledgement(rig.Channel, sequence, prior, CancellationToken.None, new StringWriter());

        session.Dispose();
        return (wait.Ending, wait.Last, sequence, stamped);
    }

    // ---- the ordinary case, so the misleading ones have something to be measured against ----------

    [Fact]
    public void ACommandTheClientArmedItself_IsAcknowledgedOnce()
    {
        var rig = NewRig(enabled: true);
        Assert.False(rig.Plc.Armed, "the model must start UNARMED, or this test proves nothing about the client.");

        var result = Send(rig, new SequenceLedger());

        Assert.Equal(HeartbeatEnding.Stamped, result.Stamped.Ending);
        Assert.True(rig.Plc.Armed, "the client's own stamps are what armed the model.");
        Assert.Equal(WaitEnding.Acknowledged, result.Ending);
        Assert.Equal(new[] { result.Sequence }, rig.Plc.Processed);
        Assert.Single(rig.Plc.Processed);
    }

    [Fact]
    public void TheStampsTheClientMakes_AreChanges_AndAreSeparatedByScans()
    {
        // The two properties that make a stamp count for anything, measured on what reached the device:
        // every value differs from the one before it, and each was sampled in a scan of its own. Neither is
        // visible in the verdict, and both are what the verdict rests on.
        var rig = NewRig(enabled: true);

        Send(rig, new SequenceLedger());

        var register = rig.Binding.BandRoles[InjectionRole.Heartbeat].Register;
        var stamps = rig.Plc.Writes
            .Where(w => w.StartRegister == register && w.Values.Count == 1)
            .Select(w => w.Values[0])
            .Take(HeartbeatPlan.ModelledChanges)
            .ToList();

        Assert.Equal(HeartbeatPlan.ModelledChanges, stamps.Count);
        Assert.Equal(stamps.Count, stamps.Distinct().Count());
        Assert.DoesNotContain(HeartbeatStamps.RestingValue, stamps);

        // The model counts CHANGES, and it counted one per stamp — so no two stamps shared a scan.
        Assert.Equal(HeartbeatPlan.ModelledChanges, rig.Plc.HeartbeatChangesSeen);
    }

    // ---- 🔴 THE MUTATION CASES: a client that cannot arm must not be able to pass ------------------

    [Fact]
    public void AClientThatStampsTheHeartbeatOnlyOnce_ArmsNothingAndIsRefused()
    {
        // One change is not arming. This is the first of the two mutations that must fail: if a client that
        // stamps once can still get a command acknowledged here, this suite is not testing arming at all.
        var rig = NewRig(enabled: true);

        var result = Send(rig, new SequenceLedger(), new HeartbeatPlan(Changes: 1));

        Assert.Equal(HeartbeatEnding.Stamped, result.Stamped.Ending);
        Assert.Single(result.Stamped.Values);
        Assert.Equal(1, rig.Plc.HeartbeatChangesSeen);
        Assert.False(rig.Plc.Armed);

        Assert.Equal(WaitEnding.NotAcknowledged, result.Ending);
        Assert.Equal(AckOutcome.Incoherent, result.Last!.Outcome);
        Assert.Empty(rig.Plc.Processed);
    }

    [Fact]
    public void TheSameHeartbeatValueWrittenTwice_IsOneChangeAndArmsNothing()
    {
        // The second mutation, and the reason the client generates its own values: a WRITE is not a CHANGE.
        // This one has to be performed behind the client's back, because the client cannot express it —
        // there is no method anywhere in the tool that takes a heartbeat value.
        var rig = NewRig(enabled: true);

        rig.Plc.PokeHeartbeatBypassingTheClient(7);
        rig.Plc.Scan();
        rig.Plc.PokeHeartbeatBypassingTheClient(7);
        rig.Plc.Scan();

        Assert.Equal(1, rig.Plc.HeartbeatChangesSeen);
        Assert.False(rig.Plc.Armed);

        // The positive control beside it: two DIFFERENT values, each with a scan of its own, do arm.
        var control = NewRig(enabled: true);
        control.Plc.ArmBypassingTheClient();
        Assert.True(control.Plc.Armed);
    }

    [Fact]
    public void TwoStampsInsideOneScan_AreOneChange()
    {
        // Why the client waits for an observed scan advance rather than trusting wall-clock spacing: the
        // block compares against what it read LAST SCAN, so two writes it never got to sample separately
        // are one change however far apart the clock says they were.
        var rig = NewRig(enabled: true);

        rig.Plc.PokeHeartbeatBypassingTheClient(11);
        rig.Plc.PokeHeartbeatBypassingTheClient(12);
        rig.Plc.Scan();

        Assert.Equal(1, rig.Plc.HeartbeatChangesSeen);
        Assert.False(rig.Plc.Armed);
    }

    [Fact]
    public void AHeartbeatStampedWithTheEnableClear_CountsForNothing_AndTheClientRefusesToMakeOne()
    {
        // The heartbeat reaches the block only through the enable, so a stamp made with the enable down
        // lands in memory and is never sampled. The client does not make one: it reads the enable back from
        // the device first, and a clear one is a refusal that wrote nothing.
        var rig = NewRig(enabled: false);
        var session = InjectionSession.Open(rig.Factory, rig.Binding, Options(), new InstantClock()).Session!;

        var stamped = session.StampHeartbeat(HeartbeatPlan.Default, CancellationToken.None);
        session.Dispose();

        Assert.Equal(HeartbeatEnding.EnableClear, stamped.Ending);
        Assert.False(stamped.MayCommand);
        Assert.Empty(stamped.Values);
        Assert.Empty(rig.Plc.Writes);

        // And the control: a stamp made anyway, behind the client, really does count for nothing.
        rig.Plc.PokeHeartbeatBypassingTheClient(3);
        rig.Plc.Scan();
        Assert.Equal(0, rig.Plc.HeartbeatChangesSeen);
    }

    // ---- a refusal is not a success, and not a reason to try again --------------------------------

    [Fact]
    public void ARefusedCommand_IsNotReportedAsSuccess_AndIsNeverResent()
    {
        // Unarmed by the client's own choice: the block sees the command, echoes the sequence, and does NOT
        // move its count.
        var rig = NewRig(enabled: true);

        var result = Send(rig, new SequenceLedger(), HeartbeatPlan.None);

        Assert.Equal(HeartbeatEnding.NotRequested, result.Stamped.Ending);
        Assert.NotEqual(WaitEnding.Acknowledged, result.Ending);
        Assert.Equal(WaitEnding.NotAcknowledged, result.Ending);

        // The device saw exactly ONE command and refused it. No resend — a client that retried an unknown
        // outcome is how one command becomes two.
        Assert.Single(rig.Plc.Refused);
        Assert.Empty(rig.Plc.Processed);

        var sequenceWrites = rig.Plc.Writes.Count(w => w.StartRegister == rig.Channel.SequenceRegister && w.Values.Count == 1);
        Assert.Equal(1, sequenceWrites);
    }

    [Fact]
    public void ARefusedCommand_IsDistinguishedFromOneThatWasNeverSeen()
    {
        // 🔴 "The result register did not change" must NEVER be read as "the command was not seen". The block
        // holds its result until the next command it processes, so an unchanged result is evidence of nothing.
        // What separates the two cases is the ECHOED SEQUENCE: a refusal echoes it, an unseen command does not.
        var rig = NewRig(enabled: true);

        var result = Send(rig, new SequenceLedger(), HeartbeatPlan.None);

        Assert.NotNull(result.Last);
        Assert.True(result.Last!.SeqMatches, "the device echoed the sequence, so the command WAS seen.");
        Assert.False(result.Last.CountAdvanced);
        Assert.Equal(AckOutcome.Incoherent, result.Last.Outcome);

        // And this cell is NOT read as 'pending / not yet seen'.
        Assert.NotEqual(AckOutcome.Pending, result.Last.Outcome);
    }

    // ---- 🔴 THE CASE THE WHOLE DESIGN EXISTS FOR --------------------------------------------------

    [Fact]
    public void ASuccessWhoseResultRegisterStillReadsTheOldRefusal_IsStillReportedAsSuccess()
    {
        // A refusal, then a success — and the success's result register still momentarily publishes the
        // REFUSAL's value. Every fact on the wire is true; read the result and you conclude the opposite of
        // what happened. The verdict is keyed on the COUNT, so the client gets it right.
        var rig = NewRig(enabled: true, laggingResult: true);
        var ledger = new SequenceLedger();

        var refused = Send(rig, ledger, HeartbeatPlan.None);
        Assert.Equal(WaitEnding.NotAcknowledged, refused.Ending);
        Assert.Equal(FakePlc.RefusalResult, ResultRegister(rig));

        // Now send again, this time letting the client arm. The count moves; the result register lags by one
        // read. (The enable is re-raised because the first session's restore dropped it — see
        // InjectionSessionTests.Restore_LeavesTheEnableDown_AndSaysSoWhenItWasUp. The enable cannot survive
        // a session, and a test that assumed it could would be testing a tool that does not exist.)
        rig.Plc.SetEnable(true);
        var accepted = Send(rig, ledger);

        Assert.Equal(WaitEnding.Acknowledged, accepted.Ending);
        Assert.True(accepted.Last!.CountAdvanced);
        Assert.True(accepted.Last.SeqMatches);

        // The result the client reported is the STALE one — carried through verbatim, and demonstrably not
        // what the verdict rested on. That is the whole claim: the result is reported, never branched on.
        Assert.Contains(unchecked((short)FakePlc.RefusalResult).ToString(), accepted.Last.Result, StringComparison.Ordinal);
        Assert.Single(rig.Plc.Processed);
    }

    [Fact]
    public void TheEchoedCommandCode_IsReadAndReported()
    {
        // The fourth acknowledgement member. It is what says WHICH command a held result belongs to, and it
        // is carried exactly as the result is: read, printed, and never allowed to decide anything.
        var rig = NewRig(enabled: true);

        var result = Send(rig, new SequenceLedger());

        Assert.Equal(WaitEnding.Acknowledged, result.Ending);
        Assert.NotNull(result.Last!.AckCode);
        Assert.Contains("5", result.Last.AckCode!, StringComparison.Ordinal);
    }

    // ---- a restart under the poll -----------------------------------------------------------------

    [Fact]
    public void ARestartMidPoll_AbortsWithoutResending()
    {
        var rig = NewRig(enabled: true);
        var session = InjectionSession.Open(rig.Factory, rig.Binding, Options(attempts: 10), new InstantClock()).Session!;
        var prior = session.ReadAckCount(rig.Channel);
        var sequence = new SequenceLedger().Allocate();

        // Send against a channel the model will not acknowledge, so the poll is still running when the CPU
        // restarts: the enable is dropped first, which is what a restart looks like to this model.
        rig.Plc.SetEnable(false);
        session.Apply(CommandFrameBuilder.Build(rig.Channel, Request(), sequence).Frames!);
        var writesBeforeTheRestart = rig.Plc.Writes.Count;

        rig.Plc.Restart();

        var wait = session.WaitForAcknowledgement(rig.Channel, sequence, prior, CancellationToken.None, new StringWriter());

        Assert.Equal(WaitEnding.Restarted, wait.Ending);
        Assert.Contains("NOT resent", wait.Message, StringComparison.Ordinal);

        // Not one further write went out during the poll.
        Assert.Equal(writesBeforeTheRestart, rig.Plc.Writes.Count);

        session.Dispose();
    }

    [Fact]
    public void ARestartUnderTheArming_StopsTheStampingAndSaysWhy()
    {
        // The same failure one step earlier. A restart clears the block's arming, so stamps already made are
        // worth nothing — and stamping on regardless would be a client arming a device it has just been told
        // it knows nothing about.
        var rig = NewRig(enabled: true);
        var session = InjectionSession.Open(rig.Factory, rig.Binding, Options(), new InstantClock()).Session!;

        rig.Plc.Restart();
        rig.Plc.SetEnable(true); // a restart drops the enable; put it back so the refusal is about the SCAN.

        var stamped = session.StampHeartbeat(HeartbeatPlan.Default, CancellationToken.None);
        session.Dispose();

        Assert.Equal(HeartbeatEnding.Restarted, stamped.Ending);
        Assert.False(stamped.MayCommand);
        Assert.Contains("restarted", stamped.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- the restore's ordering, measured against a model that acts on what it is written ---------

    [Fact]
    public void TheRestore_DoesNotReExecuteTheStaleSequenceItPutsBack()
    {
        // The restore writes the band as it was FOUND, and that image carries an OLD sequence. A sequence
        // inequality is exactly what makes this block execute, so restoring under a live enable would run a
        // stale command. Dropping the enable first is what stops it — and here that is measured rather than
        // argued, because this model really does act on what it is written.
        var rig = NewRig(enabled: true);

        // The SUBJECT of this test is a surface an earlier session left armed and dirty, so the arming is set
        // up directly. Nothing here is a claim about the client's own ability to arm — that is elsewhere.
        rig.Plc.ArmBypassingTheClient();
        rig.Plc.WriteRegisters(rig.Channel.SequenceRegister, new ushort[] { 40 });
        Assert.Single(rig.Plc.Processed);

        var session = InjectionSession.Open(rig.Factory, rig.Binding, Options(attempts: 1), new InstantClock()).Session!;
        session.Apply(CommandFrameBuilder.Build(rig.Channel, Request(), 41).Frames!);
        Assert.Equal(2, rig.Plc.Processed.Count);

        var restore = session.Restore();
        session.Dispose();

        Assert.True(restore.Ok, restore.Failure);

        // The band went back — sequence 40 was written into the sequence register again — and NOTHING further
        // was processed, because the enable was already down when it landed.
        Assert.Equal((ushort)40, LastValueWrittenTo(rig, rig.Channel.SequenceRegister));
        Assert.Equal(2, rig.Plc.Processed.Count);
    }

    [Fact]
    public void TheControl_ShowsAStaleSequenceWouldHaveReExecutedUnderALiveEnable()
    {
        // The negative control for the test above. Without it, "nothing was re-executed" could just mean the
        // model never re-executes anything — a green that examined nothing.
        var rig = NewRig(enabled: true);
        rig.Plc.ArmBypassingTheClient();

        rig.Plc.WriteRegisters(rig.Channel.SequenceRegister, new ushort[] { 40 });
        rig.Plc.WriteRegisters(rig.Channel.SequenceRegister, new ushort[] { 41 });
        Assert.Equal(2, rig.Plc.Processed.Count);

        // The stale image written back with the enable STILL UP — the failure mode, performed deliberately.
        rig.Plc.WriteRegisters(rig.Channel.SequenceRegister, new ushort[] { 40 });

        Assert.Equal(3, rig.Plc.Processed.Count);
        Assert.Equal((ushort)40, rig.Plc.Processed[^1]);
    }

    private static ushort ResultRegister(Rig rig)
    {
        var tag = rig.Channel.ObservationRoles[InjectionRole.AckResult];
        return rig.Plc.ReadRegisters(tag.Register, 1)[0];
    }

    private static ushort LastValueWrittenTo(Rig rig, int register) =>
        rig.Plc.Writes.Where(w => w.Covers(register)).Select(w => w.Values[register - w.StartRegister]).Last();

    private static CommandRequest Request() => new(Fixtures.ChannelName, new Dictionary<InjectionRole, string>
    {
        [InjectionRole.Code] = "5",
        [InjectionRole.Int1] = "-3",
        [InjectionRole.Int2] = "1000",
        [InjectionRole.Real1] = "1.5",
        [InjectionRole.Real2] = "-2.5",
    });
}
