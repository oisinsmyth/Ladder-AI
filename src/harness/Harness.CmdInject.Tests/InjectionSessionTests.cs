using Harness.CmdInject;
using Harness.Map;

namespace Harness.CmdInject.Tests;

/// <summary>
/// The session's two gates: <b>identity before any write</b>, and <b>reversibility on every exit path</b>.
///
/// <para>Both are asserted against a recording port rather than an exit code, because both claims are about
/// what reached the wire and in what order. "Nothing was written" is a fact about a list; "the enable was
/// dropped before the band went back" is a fact about that list's order; "the restore verified itself" is a
/// fact about a READ appearing after the last write. None of the three is visible in a return value.</para>
/// </summary>
public class InjectionSessionTests
{
    private static readonly BuildStamp Expected = new(0xABCD1234);
    private static readonly BuildStamp Other = new(0x0BAD0BAD);

    private static SessionOptions Options(int pollAttempts = 3) =>
        new("127.0.0.1", 503, 1, Expected, pollAttempts, PollIntervalMs: 0);

    /// <summary>A transport seeded the way a running harness program presents itself.</summary>
    private static RecordingTransport Seeded(BuildStamp stamp, bool enable = true, uint scan = 500)
    {
        var transport = new RecordingTransport();
        transport.PublishStamp(stamp);
        transport.PublishScan(scan);

        if (enable)
        {
            var tag = Fixtures.DefaultResolved().BandRoles[InjectionRole.Enable];
            transport.Poke(tag.Register, (ushort)(1 << tag.BitInRegister));
        }

        return transport;
    }

    // ---- identity ---------------------------------------------------------------------------------

    [Fact]
    public void Open_ConfirmsTheDeclaredStamp_AndReportsIt()
    {
        var factory = new RecordingFactory(Seeded(Expected));

        var open = InjectionSession.Open(factory, Fixtures.DefaultResolved(), Options(), new InstantClock());

        Assert.NotNull(open.Session);
        Assert.Equal(SessionRefusal.None, open.Refusal);
        Assert.Contains(Expected.Literal, open.Message, StringComparison.Ordinal);
        open.Session!.Dispose();
    }

    [Fact]
    public void Open_RefusesAStampMismatch_HavingWrittenNothing()
    {
        var transport = Seeded(Other);
        var factory = new RecordingFactory(transport);

        var open = InjectionSession.Open(factory, Fixtures.DefaultResolved(), Options(), new InstantClock());

        Assert.Null(open.Session);
        Assert.Equal(SessionRefusal.StampMismatch, open.Refusal);

        // THE CLAIM: not one write reached the wire, and the socket was closed.
        Assert.Empty(transport.Writes);
        Assert.True(transport.Disposed, "a refused session must not leave a socket open.");
    }

    [Fact]
    public void Open_ReadsTheStampBeforeItReadsAnythingElse()
    {
        var transport = Seeded(Expected);
        var factory = new RecordingFactory(transport);

        var session = InjectionSession.Open(factory, Fixtures.DefaultResolved(), Options(), new InstantClock()).Session!;

        // The first transaction of the session is the control read at register 0 — the identity question is
        // asked before the restore point is captured, and opening the session writes nothing at all.
        Assert.Equal(new RecordedEvent(IsWrite: false, ControlRegisters.BuildStamp, ControlRegisters.Count), transport.Events[0]);
        Assert.DoesNotContain(transport.Events, e => e.IsWrite);
        Assert.Equal(2, transport.Events.Count); // exactly two reads: the control block, then the band.
        session.Dispose();
    }

    [Fact]
    public void Open_NamesAWordOrderSwapWithoutTreatingItAsAMatch()
    {
        // A stamp whose halves are each other's swap is what a word-order error looks like. It is still not a
        // match, and the session still refuses — but the message says so, because "the stamp is wrong" and
        // "the stamp is right and read backwards" send a person to different places.
        var swapped = new BuildStamp((Expected.Value >> 16) | (Expected.Value << 16));
        var factory = new RecordingFactory(Seeded(swapped));

        var open = InjectionSession.Open(factory, Fixtures.DefaultResolved(), Options(), new InstantClock());

        Assert.Equal(SessionRefusal.StampMismatch, open.Refusal);
        Assert.Contains("swap", open.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- reversibility ----------------------------------------------------------------------------

    [Fact]
    public void Open_RefusesABandItCouldNotPutBackInOneWrite_WithoutOpeningASocket()
    {
        var binding = Fixtures.DefaultResolved() with
        {
            CommandBand = new BandDeclaration(100, ModbusLimits.MaxWriteRegisters + 1),
        };

        var factory = new RecordingFactory(Seeded(Expected));

        var open = InjectionSession.Open(factory, binding, Options(), new InstantClock());

        Assert.Equal(SessionRefusal.BandNotRestorable, open.Refusal);
        Assert.Equal(0, factory.Opens);
    }

    [Fact]
    public void Open_CapturesTheBandAsItFoundIt()
    {
        var transport = Seeded(Expected);
        transport.Poke(150, 0x1234);
        var factory = new RecordingFactory(transport);

        using var session = InjectionSession.Open(factory, Fixtures.DefaultResolved(), Options(), new InstantClock()).Session!;

        Assert.Equal(Fixtures.CommandBand.RegisterCount, session.RestorePoint.Count);
        Assert.Equal((ushort)0x1234, session.RestorePoint[150 - Fixtures.CommandBand.FirstRegister]);
    }

    [Fact]
    public void Restore_DropsTheEnableFirst_ThenTheBand_ThenVerifiesByReading()
    {
        var transport = Seeded(Expected);
        var factory = new RecordingFactory(transport);
        var binding = Fixtures.DefaultResolved();
        var enableTag = binding.BandRoles[InjectionRole.Enable];

        var session = InjectionSession.Open(factory, binding, Options(), new InstantClock()).Session!;
        session.Apply(CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(), sequence: 1).Frames!);

        var before = transport.Events.Count;
        var restore = session.Restore();

        Assert.True(restore.Ok, restore.Failure);

        var steps = transport.Events.Skip(before).ToList();

        // 1. the enable, ALONE. Restoring a live command surface writes an OLD sequence back under a live
        //    enable, and a sequence inequality is exactly what makes the block execute.
        Assert.Equal(new RecordedEvent(IsWrite: true, enableTag.Register, 1), steps[0]);

        // 2. the whole band, one write.
        Assert.Equal(new RecordedEvent(IsWrite: true, Fixtures.CommandBand.FirstRegister, Fixtures.CommandBand.RegisterCount), steps[1]);

        // 3. a READ over the same span — the restore verifying itself.
        Assert.Equal(new RecordedEvent(IsWrite: false, Fixtures.CommandBand.FirstRegister, Fixtures.CommandBand.RegisterCount), steps[2]);

        session.Dispose();
    }

    [Fact]
    public void Restore_LeavesTheEnableDown_AndSaysSoWhenItWasUp()
    {
        var transport = Seeded(Expected, enable: true);
        var factory = new RecordingFactory(transport);
        var binding = Fixtures.DefaultResolved();
        var enableTag = binding.BandRoles[InjectionRole.Enable];

        var session = InjectionSession.Open(factory, binding, Options(), new InstantClock()).Session!;
        Assert.True(session.EnableWasSet);

        session.Apply(CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(), 1).Frames!);
        var restore = session.Restore();

        Assert.True(restore.Ok, restore.Failure);
        Assert.Equal(0, transport.Peek(enableTag.Register) & (1 << enableTag.BitInRegister));

        // NOT a faithful reversal, and the report says so rather than implying the device was left untouched.
        Assert.Contains(restore.Steps, s => s.Contains("NOT left exactly as it was found", StringComparison.Ordinal));
        session.Dispose();
    }

    [Fact]
    public void Restore_FailsWhenTheReReadDisagrees_AndCallsItAnError()
    {
        var transport = Seeded(Expected);
        transport.Poke(150, 0x1111);
        var factory = new RecordingFactory(transport);

        var session = InjectionSession.Open(factory, Fixtures.DefaultResolved(), Options(), new InstantClock()).Session!;
        session.Apply(CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(), 1).Frames!);

        // Register 150 will not take the restored value: the write returns without error and the re-read
        // disagrees. That is exactly what an unverifiable restore looks like from this end.
        transport.IgnoreWritesTo.Add(150);
        transport.Poke(150, 0x9999);

        var restore = session.Restore();

        Assert.False(restore.Ok);
        Assert.Contains("DIRTY", restore.Failure!, StringComparison.Ordinal);
        Assert.Contains("error, not a warning", restore.Failure!, StringComparison.Ordinal);
        session.Dispose();
    }

    [Fact]
    public void Restore_WritesNothingWhenTheSessionWroteNothing()
    {
        var transport = Seeded(Expected);
        var factory = new RecordingFactory(transport);

        var session = InjectionSession.Open(factory, Fixtures.DefaultResolved(), Options(), new InstantClock()).Session!;
        session.Dispose();

        // A session that never dirtied the band has nothing to put back, and writing anyway would be a change
        // the tool made for no reason.
        Assert.Empty(transport.Writes);
        Assert.True(session.RestoreResult.Ok);
    }

    [Fact]
    public void Dispose_RunsTheRestore_SoNoExitPathCanSkipIt()
    {
        var transport = Seeded(Expected);
        var factory = new RecordingFactory(transport);

        var session = InjectionSession.Open(factory, Fixtures.DefaultResolved(), Options(), new InstantClock()).Session!;
        session.Apply(CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(), 1).Frames!);

        // No explicit Restore() — this is the Ctrl-C / thrown-exception shape.
        session.Dispose();

        Assert.True(session.RestoreResult.Ok, session.RestoreResult.Failure);
        Assert.Contains(session.RestoreResult.Steps, s => s.Contains("verified by re-read", StringComparison.Ordinal));
        Assert.True(transport.Disposed);
    }

    [Fact]
    public void Restore_IsIdempotent()
    {
        var transport = Seeded(Expected);
        var factory = new RecordingFactory(transport);

        var session = InjectionSession.Open(factory, Fixtures.DefaultResolved(), Options(), new InstantClock()).Session!;
        session.Apply(CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(), 1).Frames!);

        var first = session.Restore();
        var writesAfterFirst = transport.Writes.Count;
        var second = session.Restore();
        session.Dispose();

        Assert.Same(first, second);
        Assert.Equal(writesAfterFirst, transport.Writes.Count);
    }

    // ---- polling ----------------------------------------------------------------------------------

    [Fact]
    public void WaitForAcknowledgement_GivesUpWithoutEverResending()
    {
        var transport = Seeded(Expected);
        var factory = new RecordingFactory(transport);

        var session = InjectionSession.Open(factory, Fixtures.DefaultResolved(), Options(pollAttempts: 5), new InstantClock()).Session!;
        session.Apply(CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(), 9).Frames!);
        var writesAfterTheCommand = transport.Writes.Count;

        var wait = session.WaitForAcknowledgement(Fixtures.DefaultChannel(), 9, priorCount: 0, CancellationToken.None, new StringWriter());

        Assert.Equal(WaitEnding.NotAcknowledged, wait.Ending);
        Assert.Equal(5, wait.Attempts);
        Assert.Equal(writesAfterTheCommand, transport.Writes.Count);
        session.Dispose();
    }

    [Fact]
    public void WaitForAcknowledgement_StopsOnCancellation_AndSaysTheCommandIsNotResent()
    {
        var transport = Seeded(Expected);
        var factory = new RecordingFactory(transport);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        var session = InjectionSession.Open(factory, Fixtures.DefaultResolved(), Options(pollAttempts: 50), new InstantClock()).Session!;
        session.Apply(CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(), 4).Frames!);
        var writes = transport.Writes.Count;

        var wait = session.WaitForAcknowledgement(Fixtures.DefaultChannel(), 4, 0, cancel.Token, new StringWriter());

        Assert.Equal(WaitEnding.Cancelled, wait.Ending);
        Assert.Equal(writes, transport.Writes.Count);

        // And the restore still happens — an interrupt is an exit path, not an escape from one.
        session.Dispose();
        Assert.True(session.RestoreResult.Ok, session.RestoreResult.Failure);
    }

    [Fact]
    public void WaitForAcknowledgement_StopsWhenTheStampChangesUnderIt()
    {
        var transport = Seeded(Expected);
        var factory = new RecordingFactory(transport);

        var session = InjectionSession.Open(factory, Fixtures.DefaultResolved(), Options(pollAttempts: 3), new InstantClock()).Session!;
        session.Apply(CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(), 2).Frames!);

        transport.PublishStamp(Other); // a download landed between the write and the acknowledgement.

        var wait = session.WaitForAcknowledgement(Fixtures.DefaultChannel(), 2, 0, CancellationToken.None, new StringWriter());

        Assert.Equal(WaitEnding.StampChanged, wait.Ending);
        Assert.Equal(1, wait.Attempts);
        session.Dispose();
    }

    [Fact]
    public void WaitForAcknowledgement_ConcludesNothingFromAFailedRead()
    {
        var transport = Seeded(Expected);
        var factory = new RecordingFactory(transport);

        var session = InjectionSession.Open(factory, Fixtures.DefaultResolved(), Options(pollAttempts: 3), new InstantClock()).Session!;
        session.Apply(CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(), 3).Frames!);

        transport.FailReads = (_, _) => new IOException("the connection dropped");

        var wait = session.WaitForAcknowledgement(Fixtures.DefaultChannel(), 3, 0, CancellationToken.None, new StringWriter());

        Assert.Equal(WaitEnding.ReadFailed, wait.Ending);
        Assert.Contains("Nothing is concluded from a silence", wait.Message, StringComparison.Ordinal);

        transport.FailReads = null;
        session.Dispose();
    }

    private static CommandRequest Request() => new(Fixtures.ChannelName, new Dictionary<InjectionRole, string>
    {
        [InjectionRole.Code] = "5",
        [InjectionRole.Int1] = "-3",
        [InjectionRole.Int2] = "1000",
        [InjectionRole.Real1] = "1.5",
        [InjectionRole.Real2] = "-2.5",
    });
}
