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
/// </summary>
public class FakePlcProtocolTests
{
    private static readonly BuildStamp Stamp = new(0x5150C0DE);

    private static SessionOptions Options(int attempts = 6) =>
        new("127.0.0.1", 503, 1, Stamp, attempts, PollIntervalMs: 0);

    private sealed record Rig(FakePlc Plc, RecordingFactory Factory, ResolvedBinding Binding, ResolvedChannel Channel);

    private static Rig NewRig(bool armed, bool enabled, bool laggingResult = false)
    {
        var binding = Fixtures.DefaultResolved();
        var channel = binding.Channel(Fixtures.ChannelName)!;
        var plc = new FakePlc(binding, channel, Stamp) { ResultPublicationLagsOneRead = laggingResult };

        if (armed) plc.ArmByHeartbeat();
        plc.SetEnable(enabled);

        return new Rig(plc, new RecordingFactory(plc), binding, channel);
    }

    private static (WaitEnding Ending, AckClassification? Last, ushort Sequence) Send(
        Rig rig, SequenceLedger ledger, int attempts = 6)
    {
        var session = InjectionSession.Open(rig.Factory, rig.Binding, Options(attempts), new InstantClock()).Session!;
        var prior = session.ReadAckCount(rig.Channel);
        var sequence = ledger.Allocate();

        session.Apply(CommandFrameBuilder.Build(rig.Channel, Request(), sequence).Frames!);
        var wait = session.WaitForAcknowledgement(rig.Channel, sequence, prior, CancellationToken.None, new StringWriter());

        session.Dispose();
        return (wait.Ending, wait.Last, sequence);
    }

    // ---- the ordinary case, so the misleading ones have something to be measured against ----------

    [Fact]
    public void AnArmedEnabledCommand_IsAcknowledgedOnce()
    {
        var rig = NewRig(armed: true, enabled: true);

        var result = Send(rig, new SequenceLedger());

        Assert.Equal(WaitEnding.Acknowledged, result.Ending);
        Assert.Equal(new[] { result.Sequence }, rig.Plc.Processed);
        Assert.Single(rig.Plc.Processed);
    }

    // ---- a refusal is not a success, and not a reason to try again --------------------------------

    [Fact]
    public void ARefusedCommand_IsNotReportedAsSuccess_AndIsNeverResent()
    {
        // Unarmed: the block sees the command, echoes the sequence, and does NOT move its count.
        var rig = NewRig(armed: false, enabled: true);

        var result = Send(rig, new SequenceLedger());

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
        var rig = NewRig(armed: false, enabled: true);

        var result = Send(rig, new SequenceLedger());

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
        var rig = NewRig(armed: false, enabled: true, laggingResult: true);
        var ledger = new SequenceLedger();

        var refused = Send(rig, ledger);
        Assert.Equal(WaitEnding.NotAcknowledged, refused.Ending);
        Assert.Equal(FakePlc.RefusalResult, ResultRegister(rig));

        // Now arm it and send again. The count moves; the result register lags by one read.
        // (The enable is re-raised because the first session's restore dropped it — see
        // InjectionSessionTests.Restore_LeavesTheEnableDown_AndSaysSoWhenItWasUp. The enable cannot survive
        // a session, and a test that assumed it could would be testing a tool that does not exist.)
        rig.Plc.ArmByHeartbeat();
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

    // ---- unarmed, before the heartbeat has moved twice --------------------------------------------

    [Fact]
    public void ACommandSentBeforeArming_IsRefusedAndTheClientSaysSo()
    {
        var binding = Fixtures.DefaultResolved();
        var channel = binding.Channel(Fixtures.ChannelName)!;
        var plc = new FakePlc(binding, channel, Stamp);
        plc.SetEnable(true);

        // ONE heartbeat change is not arming: the model counts CHANGES, and it wants two.
        plc.WriteRegisters(binding.BandRoles[InjectionRole.Heartbeat].Register, new ushort[] { 1 });
        Assert.False(plc.Armed);

        var rig = new Rig(plc, new RecordingFactory(plc), binding, channel);
        var result = Send(rig, new SequenceLedger());

        Assert.Equal(WaitEnding.NotAcknowledged, result.Ending);
        Assert.Equal(AckOutcome.Incoherent, result.Last!.Outcome);
        Assert.Empty(plc.Processed);
    }

    // ---- a restart under the poll -----------------------------------------------------------------

    [Fact]
    public void ARestartMidPoll_AbortsWithoutResending()
    {
        var rig = NewRig(armed: true, enabled: true);
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

    // ---- the restore's ordering, measured against a model that acts on what it is written ---------

    [Fact]
    public void TheRestore_DoesNotReExecuteTheStaleSequenceItPutsBack()
    {
        // The restore writes the band as it was FOUND, and that image carries an OLD sequence. A sequence
        // inequality is exactly what makes this block execute, so restoring under a live enable would run a
        // stale command. Dropping the enable first is what stops it — and here that is measured rather than
        // argued, because this model really does act on what it is written.
        var rig = NewRig(armed: true, enabled: true);

        // A previous session left a non-zero sequence behind; the restore point will carry it.
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
        var rig = NewRig(armed: true, enabled: true);

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
