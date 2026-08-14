using Harness.MirrorRead;

namespace Harness.MirrorView.Tests;

/// <summary>
/// 🔴 <b>THE DESIGN CONSTRAINT THAT MATTERS MOST: A STALE VALUE MUST NEVER LOOK CURRENT.</b>
///
/// <para>Both directions are asserted as deliberately as each other. A staleness rule that fires on a
/// healthy poll is noise, and noise gets switched off — after which the readings it was right about go
/// through unchecked.</para>
/// </summary>
public class StalenessTests
{
    private static MirrorViewModel Build(MirrorState state, DateTimeOffset now, int staleMs = 3000) =>
        MirrorView.Build(Fixtures.Map(), Fixtures.Options(staleMs: staleMs), state, now);

    private static MirrorPoller Poller(MirrorState state, IRegisterSource source, TestClock clock) =>
        new(Fixtures.Options(), state, new AllowingFence(), new RecordingFactory(() => source), clock.Read);

    private static ScriptedSource Healthy(uint scan = 100) =>
        new() { Standing = ScriptedSource.Area(Fixtures.Registers, scan: scan) };

    // ---- the two directions -------------------------------------------------------------------------

    /// <summary>*** A HEALTHY POLL MUST NOT BE CALLED STALE. *** The anti-over-fire half.</summary>
    [Fact]
    public void AFreshReading_IsLive()
    {
        var clock = new TestClock(Fixtures.T0);
        var state = new MirrorState();
        using var poller = Poller(state, Healthy(), clock);

        poller.PollOnce();
        clock.Advance(TimeSpan.FromSeconds(1));

        var model = Build(state, clock.Now);
        Assert.Equal(MirrorStatus.Live, model.Status);
        Assert.True(model.ValuesAreCurrent);
        Assert.Equal(1.0, model.AgeSeconds!.Value, 3);
    }

    /// <summary>
    /// *** A POLLER THAT STOPS DELIVERING MUST TAKE THE PAGE STALE, WITHOUT ANY POLL FAILING. *** This
    /// is the frozen-clock case: the last attempt still says "Ok", and the reading behind it is old.
    /// Keying green on the last outcome alone would leave a dead loop looking healthy forever.
    /// </summary>
    [Fact]
    public void AReadingOlderThanTheWindow_GoesStale_EvenThoughNothingFailed()
    {
        var clock = new TestClock(Fixtures.T0);
        var state = new MirrorState();
        using var poller = Poller(state, Healthy(), clock);

        poller.PollOnce();
        clock.Advance(TimeSpan.FromSeconds(30));

        var model = Build(state, clock.Now);

        Assert.Equal(MirrorStatus.Stale, model.Status);
        Assert.False(model.ValuesAreCurrent);
        Assert.Equal("Ok", model.LastAttemptOutcome);
        Assert.Contains("not now", model.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The boundary, pinned both sides, so "stale after N" means N and not N-ish.</summary>
    [Theory]
    [InlineData(2.9, MirrorStatus.Live)]
    [InlineData(3.1, MirrorStatus.Stale)]
    public void TheWindowIsWhereItSays(double afterSeconds, MirrorStatus expected)
    {
        var clock = new TestClock(Fixtures.T0);
        var state = new MirrorState();
        using var poller = Poller(state, Healthy(), clock);

        poller.PollOnce();
        clock.Advance(TimeSpan.FromSeconds(afterSeconds));

        Assert.Equal(expected, Build(state, clock.Now).Status);
    }

    // ---- a failed poll after a good one -------------------------------------------------------------

    /// <summary>
    /// *** THE HEADLINE CASE. *** The previous numbers are still on screen — they are the last thing
    /// known, and hiding them would answer a narrower question than the one on the page — but the status
    /// is red, <c>ValuesAreCurrent</c> is false, and the age is the age of the READING, not of the
    /// attempt. The page greys and strikes the value columns on exactly that flag.
    /// </summary>
    [Fact]
    public void AFailedPollAfterAGoodOne_KeepsTheValues_ButNotTheClaimThatTheyAreCurrent()
    {
        var clock = new TestClock(Fixtures.T0);
        var state = new MirrorState();

        // One session that answers once and then stops answering — which is what a rig going away
        // actually looks like. Two sources would not exercise it: a succeeding session is REUSED, so the
        // second one would never be opened.
        var source = new ScriptedSource()
            .Then(() => ScriptedSource.Area(Fixtures.Registers, scan: 4242))
            .Then(() => throw new TimeoutException("no answer within 3000 ms."));

        using var poller = new MirrorPoller(
            Fixtures.Options(), state, new AllowingFence(), new RecordingFactory(() => source), clock.Read);

        poller.PollOnce();
        clock.Advance(TimeSpan.FromSeconds(1));
        poller.PollOnce();

        var model = Build(state, clock.Now);

        Assert.Equal(MirrorStatus.Failing, model.Status);
        Assert.False(model.ValuesAreCurrent);
        Assert.Equal(1.0, model.AgeSeconds!.Value, 3);
        Assert.Equal((uint)4242, model.Scan.Value);
        Assert.Contains("EARLIER poll", model.StatusText, StringComparison.Ordinal);
        Assert.Contains("16#F52ECEAD", model.BuildStamp!, StringComparison.Ordinal);
    }

    /// <summary>Nothing read yet is its own state. There is no reading to be stale, and none to show.</summary>
    [Fact]
    public void BeforeAnyPoll_TheStateIsNeverRead_AndNothingClaimsToBeAValue()
    {
        var model = Build(new MirrorState(), Fixtures.T0);

        Assert.Equal(MirrorStatus.NeverRead, model.Status);
        Assert.Null(model.AgeSeconds);
        Assert.Null(model.ObservedUtc);
        Assert.Null(model.BuildStamp);
        Assert.All(model.Rows, row => Assert.Null(row.Raw));
        Assert.All(model.Rows, row => Assert.Null(row.Decoded));
    }

    // ---- the scan counter ---------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>ONE READING CANNOT SAY A COUNTER IS ADVANCING.</b> Answering it anyway is exactly how a
    /// value left in memory by a past download comes to look like a running program — which is the
    /// distinction this panel exists for.
    /// </summary>
    [Fact]
    public void OneReading_LeavesAdvancingUnestablished()
    {
        var clock = new TestClock(Fixtures.T0);
        var state = new MirrorState();
        using var poller = Poller(state, Healthy(scan: 7), clock);

        poller.PollOnce();
        var model = Build(state, clock.Now);

        Assert.Equal((uint)7, model.Scan.Value);
        Assert.Null(model.Scan.Advancing);
        Assert.Contains("NOT ESTABLISHED", model.Scan.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void ARisingCounter_IsReportedAsAdvancing_WithItsRate()
    {
        var clock = new TestClock(Fixtures.T0);
        var state = new MirrorState();
        var source = Healthy(scan: 1000);
        using var poller = Poller(state, source, clock);

        poller.PollOnce();
        clock.Advance(TimeSpan.FromSeconds(1));
        source.Standing = ScriptedSource.Area(Fixtures.Registers, scan: 1500);
        poller.PollOnce();

        var model = Build(state, clock.Now);

        Assert.True(model.Scan.Advancing);
        Assert.Equal(500, model.Scan.Advance);
        Assert.Equal(1000, model.Scan.IntervalMs);
        Assert.Contains("2.00 ms per scan", model.Scan.Note, StringComparison.Ordinal);
    }

    /// <summary>A counter that does not move is the fact the build stamp cannot give you. Say it plainly.</summary>
    [Fact]
    public void AStaticCounter_IsReportedAsNotAdvancing()
    {
        var clock = new TestClock(Fixtures.T0);
        var state = new MirrorState();
        using var poller = Poller(state, Healthy(scan: 55), clock);

        poller.PollOnce();
        clock.Advance(TimeSpan.FromSeconds(1));
        poller.PollOnce();

        var model = Build(state, clock.Now);

        Assert.False(model.Scan.Advancing);
        Assert.Contains("NOT ADVANCING", model.Scan.Note, StringComparison.Ordinal);
    }

    /// <summary>
    /// The counter is read UNSIGNED and differenced modularly, so a wrap is a small forward advance and
    /// not a nine-figure negative. Pinned here because the same defect has already been shipped once on
    /// the live path (<c>Harness.Wire.ScanCount</c>'s own remarks).
    /// </summary>
    [Fact]
    public void TheCounterWraps_WithoutTurningAOneScanAdvanceIntoAHugeNegative()
    {
        var clock = new TestClock(Fixtures.T0);
        var state = new MirrorState();
        var source = Healthy(scan: 0xFFFFFFFFu);
        using var poller = Poller(state, source, clock);

        poller.PollOnce();
        clock.Advance(TimeSpan.FromSeconds(1));
        source.Standing = ScriptedSource.Area(Fixtures.Registers, scan: 0u);
        poller.PollOnce();

        var model = Build(state, clock.Now);

        Assert.True(model.Scan.Advancing);
        Assert.Equal(1, model.Scan.Advance);
    }

    /// <summary>
    /// An implausibly large modular advance is a counter that went BACKWARDS — a restart, or a different
    /// program. Absorbing it as an advance would MANUFACTURE liveness, which is the one direction this
    /// page must never fail in.
    /// </summary>
    [Fact]
    public void ACounterThatWentBackwards_IsNotReportedAsAdvancing()
    {
        var clock = new TestClock(Fixtures.T0);
        var state = new MirrorState();
        // Backwards by 1e9, so the MODULAR advance is 3.29e9 — past the half-span ceiling, which is what
        // separates "wrapped forward by a little" from "restarted". A jump back past 2^31 would read as a
        // small forward advance and is, correctly, indistinguishable.
        var source = Healthy(scan: 3_000_000_000u);
        using var poller = Poller(state, source, clock);

        poller.PollOnce();
        clock.Advance(TimeSpan.FromSeconds(1));
        source.Standing = ScriptedSource.Area(Fixtures.Registers, scan: 2_000_000_000u);
        poller.PollOnce();

        var model = Build(state, clock.Now);

        Assert.False(model.Scan.Advancing);
        Assert.Contains("WENT BACKWARDS", model.Scan.Note, StringComparison.Ordinal);
    }

    // ---- the rows ------------------------------------------------------------------------------------

    /// <summary>Every declared register gets a row, whether or not the map names it.</summary>
    [Fact]
    public void TheTableHasOneRowPerDeclaredRegister()
    {
        var model = Build(new MirrorState(), Fixtures.T0);

        Assert.Equal(Fixtures.Registers, model.Rows.Count);
        Assert.Equal(Enumerable.Range(0, Fixtures.Registers), model.Rows.Select(r => r.Index));
    }

    /// <summary>
    /// A 32-bit element occupies two rows: the first carries the decode, the second says which half of
    /// which value it is. Both carry their own raw word — a decode that hides its bytes hides a wrong
    /// assumption, and the second register of a pair is exactly where one would hide.
    /// </summary>
    [Fact]
    public void ATwoRegisterElement_OccupiesTwoRows_AndTheSecondSaysWhatItIs()
    {
        var clock = new TestClock(Fixtures.T0);
        var state = new MirrorState();
        using var poller = Poller(state, Healthy(), clock);
        poller.PollOnce();

        var model = Build(state, clock.Now);

        Assert.Equal("value", model.Rows[0].Role);
        Assert.Equal("16#F52ECEAD", model.Rows[0].Decoded);
        Assert.Equal("16#F52E", model.Rows[0].Raw);

        Assert.Equal("continuation", model.Rows[1].Role);
        Assert.Equal("16#CEAD", model.Rows[1].Raw);
        Assert.Null(model.Rows[1].Decoded);
        Assert.Contains("LOW half", model.Rows[1].Basis!, StringComparison.Ordinal);
    }

    /// <summary>Every value row states the transform that produced it, not merely the answer.</summary>
    [Fact]
    public void EveryDecodedRow_CarriesItsBasis()
    {
        var clock = new TestClock(Fixtures.T0);
        var state = new MirrorState();
        using var poller = Poller(state, Healthy(), clock);
        poller.PollOnce();

        var model = Build(state, clock.Now);

        Assert.All(model.Rows.Where(r => r.Decoded is not null), r => Assert.False(string.IsNullOrWhiteSpace(r.Basis)));
    }

    /// <summary>The IR comment reaches the screen — it is the only "meaning" column there is.</summary>
    [Fact]
    public void TheCommentColumnComesFromTheArtifact()
    {
        var model = Build(new MirrorState(), Fixtures.T0);

        Assert.Equal("Build stamp of the downloaded IR set.", model.Rows[0].Comment);
        Assert.Equal("Free-running scan counter.", model.Rows[2].Comment);
    }

    // ---- the JSON ------------------------------------------------------------------------------------

    /// <summary>The API and the page cannot disagree, because the page has no other source.</summary>
    [Fact]
    public void TheJsonCarriesTheStatusAndTheAge()
    {
        var clock = new TestClock(Fixtures.T0);
        var state = new MirrorState();
        using var poller = Poller(state, Healthy(), clock);
        poller.PollOnce();
        clock.Advance(TimeSpan.FromSeconds(30));

        var json = MirrorJson.Serialize(Build(state, clock.Now));

        Assert.Contains("\"status\": \"Stale\"", json, StringComparison.Ordinal);
        Assert.Contains("\"valuesAreCurrent\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"ageSeconds\": 30", json, StringComparison.Ordinal);
    }
}
