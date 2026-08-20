using Harness.CmdInject;
using Harness.Map;

namespace Harness.CmdInject.Tests;

/// <summary>
/// The arming path, in pieces: <b>the values, the plan's own refusals, and the session step that puts them
/// on the wire.</b>
///
/// <para>🔴 <b>WHAT THIS EXISTS TO STOP.</b> The tool could authorise itself to write and then had no way
/// to satisfy the device's own gate — <c>InjectionWriteTarget.Heartbeat</c> had zero callers, the role was
/// parsed, resolved, type-checked and printed, and nothing ever wrote it. On the rig that surfaces as a
/// command refused as unarmed, reported honestly as <c>NotAcknowledged</c>, with no path to any other
/// answer and no indication which end was at fault.</para>
///
/// <para>The two properties that make a stamp count for anything are checked separately, because they
/// fail separately: <b>a stamp must be a CHANGE</b> (two identical writes arm nothing) and <b>two stamps
/// must not share a scan</b> (the block samples once per scan, so writes it never sampled apart are one
/// change). Neither is visible in the tool's verdict, and both are what the verdict rests on.</para>
/// </summary>
public class HeartbeatStampingTests
{
    private static readonly BuildStamp Expected = new(0xABCD1234);
    private static readonly BuildStamp Other = new(0x0BAD0BAD);

    private static SessionOptions Options() => new("127.0.0.1", 503, 1, Expected, PollAttempts: 3, PollIntervalMs: 0);

    private static RecordingTransport Running(bool enable = true, uint scan = 500)
    {
        var transport = new RecordingTransport();
        transport.PublishStamp(Expected);
        transport.PublishScan(scan);

        if (enable)
        {
            var tag = Fixtures.DefaultResolved().BandRoles[InjectionRole.Enable];
            transport.Poke(tag.Register, (ushort)(1 << tag.BitInRegister));
        }

        return transport;
    }

    private static InjectionSession Open(RecordingTransport transport, IInjectionClock? clock = null) =>
        InjectionSession.Open(new RecordingFactory(transport), Fixtures.DefaultResolved(), Options(), clock ?? new InstantClock()).Session!;

    private static int HeartbeatRegister => Fixtures.DefaultResolved().BandRoles[InjectionRole.Heartbeat].Register;

    // ---- the values: a change, by construction ---------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(41)]
    [InlineData(ushort.MaxValue - 1)]
    [InlineData(ushort.MaxValue)]
    public void EverySuccessiveStamp_DiffersFromTheOneBeforeIt(int found)
    {
        var values = HeartbeatStamps.From((ushort)found, changes: 8);

        Assert.Equal(8, values.Count);
        Assert.NotEqual((ushort)found, values[0]);

        for (var i = 1; i < values.Count; i++)
            Assert.NotEqual(values[i - 1], values[i]);
    }

    [Fact]
    public void NoStampIsEverTheRestingValue()
    {
        // Zero is what a register reads when nothing has written it, so a change TO zero is the one change a
        // liveness gate might decline to count. Checked across the wrap, which is the only way to reach it.
        var values = HeartbeatStamps.From(ushort.MaxValue - 2, changes: 6);

        Assert.DoesNotContain(HeartbeatStamps.RestingValue, values);
        for (var i = 1; i < values.Count; i++)
            Assert.NotEqual(values[i - 1], values[i]);
    }

    [Fact]
    public void ANegativeNumberOfStamps_IsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => HeartbeatStamps.From(0, -1));

    // ---- the plan refuses what cannot work -------------------------------------------------------

    [Fact]
    public void APlanSeparatingStampsByNoScans_IsRefused()
    {
        // Zero observed scans is the "we wrote twice and hoped" behaviour this design removes. There is no
        // version of the plan that expresses it.
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeartbeatPlan(2, ScansBetweenStamps: 0));
    }

    [Fact]
    public void APlanAllowingOneControlRead_IsRefused()
    {
        // The first read is the baseline, so one read can never show an advance — a plan allowing one would
        // refuse every time and look exactly like a stalled CPU.
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeartbeatPlan(2, ScanWaitAttempts: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeartbeatPlan(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeartbeatPlan(2, ScanWaitIntervalMs: -1));
    }

    [Fact]
    public void TheDefaultPlan_IsTheModelledNumberOfChanges_AndSeparatesThem()
    {
        Assert.Equal(HeartbeatPlan.ModelledChanges, HeartbeatPlan.Default.Changes);
        Assert.True(HeartbeatPlan.Default.ScansBetweenStamps >= 1);
        Assert.True(HeartbeatPlan.Default.Stamps);
        Assert.False(HeartbeatPlan.None.Stamps);
    }

    // ---- the session step ------------------------------------------------------------------------

    [Fact]
    public void StampHeartbeat_WritesOneRegisterPerChange_EachADifferentValue()
    {
        var transport = Running();
        using var session = Open(transport);

        var report = session.StampHeartbeat(new HeartbeatPlan(3), CancellationToken.None);

        Assert.Equal(HeartbeatEnding.Stamped, report.Ending);
        Assert.True(report.MayCommand);
        Assert.Equal(3, report.Values.Count);

        var stamps = transport.Writes.Where(w => w.StartRegister == HeartbeatRegister).ToList();
        Assert.Equal(3, stamps.Count);
        Assert.All(stamps, stamp => Assert.Single(stamp.Values));
        Assert.Equal(3, stamps.Select(s => s.Values[0]).Distinct().Count());
        Assert.Equal(report.Values, stamps.Select(s => s.Values[0]).ToList());
    }

    [Fact]
    public void StampHeartbeat_ObservesAScanAdvanceAfterEveryStamp_IncludingTheLast()
    {
        // Including the last, because the last stamp has to be sampled before the command's sequence is
        // processed — otherwise the command arrives at a block that has counted one change fewer.
        var transport = Running();
        using var session = Open(transport);

        var report = session.StampHeartbeat(new HeartbeatPlan(2), CancellationToken.None);

        Assert.Equal(HeartbeatEnding.Stamped, report.Ending);

        // Each stamp is followed by at least two control reads: the baseline, then one that clears the margin.
        var events = transport.Events;
        var stampPositions = events
            .Select((e, index) => (Event: e, Index: index))
            .Where(x => x.Event.IsWrite && x.Event.StartRegister == HeartbeatRegister)
            .Select(x => x.Index)
            .ToList();

        Assert.Equal(2, stampPositions.Count);

        foreach (var position in stampPositions)
        {
            var controlReadsAfter = events
                .Skip(position + 1)
                .TakeWhile(e => !e.IsWrite)
                .Count(e => e is { IsWrite: false, StartRegister: ControlRegisters.BuildStamp, Count: ControlRegisters.Count });

            Assert.True(controlReadsAfter >= 2,
                $"a stamp at event {position} was followed by {controlReadsAfter} control read(s). One read is a baseline and " +
                "can never show an advance, so fewer than two means the separation was assumed rather than observed.");
        }

        Assert.True(report.ScanReads >= 4, $"only {report.ScanReads} control read(s) were spent observing the advances.");
    }

    [Fact]
    public void StampHeartbeat_RefusesWhenTheScanCounterWillNotAdvance_AndStampsNoFurther()
    {
        // A counter that does not move is a CPU that is not scanning: nothing is sampling the heartbeat, so
        // the next stamp would be issued hopefully. One stamp goes out, and then the run stops.
        var transport = Running();
        transport.ScanAdvancesOnRead = false;
        using var session = Open(transport);

        var report = session.StampHeartbeat(new HeartbeatPlan(2, ScanWaitAttempts: 4), CancellationToken.None);

        Assert.Equal(HeartbeatEnding.ScanStalled, report.Ending);
        Assert.False(report.MayCommand);
        Assert.Single(report.Values);
        Assert.Single(transport.Writes.Where(w => w.StartRegister == HeartbeatRegister));
        Assert.Equal(4, report.ScanReads);
    }

    [Fact]
    public void StampHeartbeat_WritesNothingWhenTheEnableReadsClear()
    {
        // The heartbeat reaches the block through the enable, so a stamp made now would land in memory and
        // count for nothing — which from this end looks exactly like a stamp that worked.
        var transport = Running(enable: false);
        using var session = Open(transport);

        var report = session.StampHeartbeat(HeartbeatPlan.Default, CancellationToken.None);

        Assert.Equal(HeartbeatEnding.EnableClear, report.Ending);
        Assert.False(report.MayCommand);
        Assert.Empty(report.Values);
        Assert.Empty(transport.Writes);
    }

    [Fact]
    public void StampHeartbeat_ReadsTheEnableFromTheDevice_NotFromItsOwnEarlierWrite()
    {
        // The enable is read back rather than remembered. A session that raised it and then believed itself
        // would stamp into a void whenever the raise did not take.
        var transport = Running(enable: false);
        var enableTag = Fixtures.DefaultResolved().BandRoles[InjectionRole.Enable];
        transport.IgnoreWritesTo.Add(enableTag.Register);

        using var session = Open(transport);
        session.RaiseEnable();

        var report = session.StampHeartbeat(HeartbeatPlan.Default, CancellationToken.None);

        Assert.Equal(HeartbeatEnding.EnableClear, report.Ending);
        Assert.DoesNotContain(transport.Writes, w => w.StartRegister == HeartbeatRegister);
    }

    [Fact]
    public void StampHeartbeat_StopsWhenTheBuildStampChangesUnderIt()
    {
        var transport = Running();
        using var session = Open(transport);

        transport.PublishStamp(Other); // a download landed between the session opening and the arming.

        var report = session.StampHeartbeat(HeartbeatPlan.Default, CancellationToken.None);

        Assert.Equal(HeartbeatEnding.StampChanged, report.Ending);
        Assert.False(report.MayCommand);
    }

    [Fact]
    public void StampHeartbeat_ConcludesNothingFromAFailedRead()
    {
        var transport = Running();
        using var session = Open(transport);

        transport.FailReads = (start, _) => start == ControlRegisters.BuildStamp ? new IOException("the connection dropped") : null;

        var report = session.StampHeartbeat(HeartbeatPlan.Default, CancellationToken.None);

        transport.FailReads = null;

        Assert.Equal(HeartbeatEnding.ReadFailed, report.Ending);
        Assert.False(report.MayCommand);
        Assert.Contains("silence", report.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StampHeartbeat_StopsOnCancellation()
    {
        var transport = Running();
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        using var session = Open(transport);

        var report = session.StampHeartbeat(HeartbeatPlan.Default, cancel.Token);

        Assert.Equal(HeartbeatEnding.Cancelled, report.Ending);
        Assert.False(report.MayCommand);
        Assert.Empty(transport.Writes);
    }

    [Fact]
    public void StampHeartbeat_WritesNothingWhenNoChangesAreAskedFor()
    {
        var transport = Running();
        using var session = Open(transport);

        var report = session.StampHeartbeat(HeartbeatPlan.None, CancellationToken.None);

        Assert.Equal(HeartbeatEnding.NotRequested, report.Ending);
        Assert.True(report.MayCommand, "opting out is a choice the run may make; it is not a refusal.");
        Assert.Empty(transport.Writes);

        // Not even the enable is read: an opt-out contacts nothing on the way past.
        Assert.DoesNotContain(transport.Events, e => e.StartRegister == Fixtures.DefaultResolved().BandRoles[InjectionRole.Enable].Register);
    }

    [Fact]
    public void StampHeartbeat_WaitsOnTheClockOnlyWhenAGapWasAskedFor()
    {
        var transport = Running();
        var clock = new InstantClock();
        using var session = Open(transport, clock);

        session.StampHeartbeat(new HeartbeatPlan(2), CancellationToken.None);
        Assert.Equal(0, clock.Waits);

        var second = new InstantClock();
        var other = Running();
        using var session2 = Open(other, second);
        session2.StampHeartbeat(new HeartbeatPlan(2, ScanWaitIntervalMs: 5), CancellationToken.None);
        Assert.True(second.Waits > 0, "a declared gap between control reads must actually reach the clock.");
    }

    [Fact]
    public void ASessionThatOnlyStamped_StillPutsTheBandBack()
    {
        // A stamp dirties the band exactly as a command does, so a run that armed and then refused still has
        // something to put back — and the heartbeat register goes back with everything else.
        var transport = Running();
        var session = Open(transport);

        session.StampHeartbeat(new HeartbeatPlan(2), CancellationToken.None);
        session.Dispose();

        Assert.True(session.RestoreResult.Ok, session.RestoreResult.Failure);
        Assert.Equal((ushort)0, transport.Peek(HeartbeatRegister));
    }
}
