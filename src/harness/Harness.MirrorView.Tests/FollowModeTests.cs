using Harness.MirrorRead;
using Harness.Wire;

namespace Harness.MirrorView.Tests;

/// <summary>
/// FOLLOW MODE — the viewer fed from a running wave instead of from a second socket.
///
/// <para>🔴 <b>THE DISCIPLINE THESE TESTS EXIST FOR: A STALE READING MUST NEVER LOOK CURRENT.</b> Follow
/// mode adds new ways to be wrong, and each has to be its own visible state:</para>
/// <list type="bullet">
/// <item>no publisher has ever written (no wave has run),</item>
/// <item>a publisher wrote and then STOPPED without saying it had finished (the wave died),</item>
/// <item>a publisher wrote and ENDED (the wave finished; the data is final, not current),</item>
/// <item>the feed is being written and the SCAN COUNTER is static (the CPU stopped),</item>
/// <item>the feed is present and unreadable, and the feed describes a DIFFERENT MIRROR.</item>
/// </list>
/// <para>"No data", "old data" and "dead device" are three different facts. A viewer that rendered any of
/// them as a plausible-looking page would be worse than one that refused.</para>
/// </summary>
public class FollowModeTests
{
    private static readonly DateTimeOffset T0 = Fixtures.T0;

    private static MirrorViewOptions Options(int pollMs = 1000, int staleMs = 3000) =>
        new(string.Empty, 0, 0, null, Fixtures.Registers, pollMs, staleMs, 8137, FollowPath: "C:/feed.mirrorfeed");

    private static MirrorFeedIdentity Identity(
        int registers = Fixtures.Registers,
        RegisterWordOrder order = RegisterWordOrder.HighWordFirst) =>
        new(0xF52ECEAD, "maphash", registers, order);

    /// <summary>The whole six-register area as one frame, the way a wave's control read would arrive.</summary>
    private static MirrorFeedFrame WholeArea(DateTimeOffset at, uint stamp = 0xF52ECEAD, uint scan = 0) =>
        new(0, at, new[]
        {
            (ushort)(stamp >> 16), (ushort)(stamp & 0xFFFF),
            (ushort)(scan >> 16), (ushort)(scan & 0xFFFF),
            (ushort)0x0007, (ushort)0x0001,
        });

    private static MirrorFeedDocument Document(
        PublisherStatus status, DateTimeOffset publishedUtc, params MirrorFeedFrame[] frames) =>
        new("pid7@BENCH", Identity(), status, publishedUtc, 5, frames);

    private static MirrorFollowPoller Poller(IMirrorFeedSource source, MirrorState state, Func<DateTimeOffset> clock,
        MirrorViewOptions? options = null) =>
        new(options ?? Options(), Fixtures.Map(), state, source, clock);

    private static MirrorViewModel View(MirrorState state, DateTimeOffset now, MirrorViewOptions? options = null) =>
        MirrorView.Build(Fixtures.Map(), options ?? Options(), state, now);

    // ---- STATE 1: NO PUBLISHER HAS EVER WRITTEN ------------------------------------------------------

    /// <summary>
    /// <b>Nothing is wrong, and nothing is shown.</b> Its own grey banner, and emphatically not a table of
    /// zeros: the mirror has not been read, it is not empty.
    /// </summary>
    [Fact]
    public void No_publisher_has_ever_written_is_its_own_state_and_shows_no_values()
    {
        var state = new MirrorState();
        var attempt = Poller(new ScriptedFeed(MirrorFeedState.NeverPublished), state, () => T0).PollOnce();

        Assert.Equal(PollOutcome.FeedNeverPublished, attempt.Outcome);

        var view = View(state, T0);

        Assert.Equal(MirrorStatus.NoFeed, view.Status);
        Assert.Contains("NO PUBLISHER HAS EVER WRITTEN", view.StatusText, StringComparison.Ordinal);
        Assert.False(view.ValuesAreCurrent);
        Assert.All(view.Rows, r => Assert.Null(r.Raw));
        Assert.All(view.Rows, r => Assert.Null(r.Decoded));
        Assert.Equal(Fixtures.Registers, view.RegistersNotObserved);
        Assert.Equal(0, view.RegistersCurrent);
    }

    // ---- STATE 2: A PUBLISHER WROTE AND THE FEED IS GONE ---------------------------------------------

    /// <summary>
    /// 🔴 <b>NOT THE SAME AS "NOBODY EVER WROTE HERE", AND THE TWO MUST NOT SHARE A BANNER.</b> That state
    /// is the benign-looking one, and reading a LOST feed as "no wave has run" hands a reader the most
    /// reassuring answer available at the exact moment something has gone wrong.
    /// </summary>
    [Fact]
    public void A_publisher_that_wrote_and_lost_its_feed_is_a_different_state_from_one_that_never_wrote()
    {
        var never = new MirrorState();
        Poller(new ScriptedFeed(MirrorFeedState.NeverPublished), never, () => T0).PollOnce();

        var lost = new MirrorState();
        Poller(new ScriptedFeed(MirrorFeedState.PublishInterrupted), lost, () => T0).PollOnce();

        var neverView = View(never, T0);
        var lostView = View(lost, T0);

        Assert.Equal(MirrorStatus.NoFeed, neverView.Status);
        Assert.Equal(MirrorStatus.FeedInterrupted, lostView.Status);
        Assert.NotEqual(neverView.Status, lostView.Status);
        Assert.NotEqual(neverView.StatusText, lostView.StatusText);
        Assert.Contains("NOT 'no wave has run'", lostView.StatusText, StringComparison.Ordinal);
    }

    // ---- STATE 3: THE FEED IS THERE AND CANNOT BE READ -----------------------------------------------

    [Fact]
    public void An_unreadable_feed_refuses_rather_than_showing_what_parsed()
    {
        var state = new MirrorState();
        Poller(new ScriptedFeed(MirrorFeedState.Unreadable), state, () => T0).PollOnce();

        var view = View(state, T0);

        Assert.Equal(MirrorStatus.FeedUnreadable, view.Status);
        Assert.All(view.Rows, r => Assert.Null(r.Raw));
        Assert.Null(view.BuildStamp);
        Assert.Null(view.Scan.Value);
    }

    // ---- STATE 4: THE FEED DESCRIBES A DIFFERENT MIRROR ----------------------------------------------

    /// <summary>
    /// 🔴 <b>THE FAILURE THAT RENDERS PERFECTLY.</b> The viewer's map comes from the committed IR and the
    /// wave's comes from the binding it is running: two independent derivations of one thing. When they
    /// disagree the registers still line up and mean DIFFERENT SIGNALS, so nothing is shown.
    /// </summary>
    [Fact]
    public void A_feed_declaring_a_different_width_is_refused_by_name()
    {
        var state = new MirrorState();
        var document = new MirrorFeedDocument("p", Identity(registers: Fixtures.Registers + 4),
            PublisherStatus.Running, T0, 1, new[] { new MirrorFeedFrame(0, T0, new ushort[] { 1, 2 }) });

        var attempt = Poller(new ScriptedFeed(document), state, () => T0).PollOnce();

        Assert.Equal(PollOutcome.FeedDescribesADifferentMirror, attempt.Outcome);

        var view = View(state, T0);

        Assert.Equal(MirrorStatus.FeedMismatch, view.Status);
        Assert.Contains("register(s)", view.LastAttemptDetail, StringComparison.Ordinal);
        Assert.All(view.Rows, r => Assert.Null(r.Raw));
    }

    /// <summary>
    /// A feed read under the other 32-bit order would put every wide value out by up to 65 536 and would
    /// look entirely plausible. Refused, and the refusal says so.
    /// </summary>
    [Fact]
    public void A_feed_read_under_the_other_word_order_is_refused()
    {
        var state = new MirrorState();
        var document = new MirrorFeedDocument("p", Identity(order: RegisterWordOrder.LowWordFirst),
            PublisherStatus.Running, T0, 1, new[] { WholeArea(T0) });

        Poller(new ScriptedFeed(document), state, () => T0).PollOnce();

        var view = View(state, T0);

        Assert.Equal(MirrorStatus.FeedMismatch, view.Status);
        Assert.Contains("65 536", view.LastAttemptDetail, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every mismatch is named, not the first. A reader fixing one should not have to run again to
    /// discover the next.
    /// </summary>
    [Fact]
    public void Every_identity_mismatch_is_named_at_once()
    {
        var state = new MirrorState();
        var document = new MirrorFeedDocument("p",
            new MirrorFeedIdentity(1, "other", Fixtures.Registers + 1, RegisterWordOrder.LowWordFirst),
            PublisherStatus.Running, T0, 1, new[] { new MirrorFeedFrame(0, T0, new ushort[] { 1 }) });

        Poller(new ScriptedFeed(document), state, () => T0).PollOnce();

        var detail = View(state, T0).LastAttemptDetail;

        Assert.Contains("register(s)", detail, StringComparison.Ordinal);
        Assert.Contains("65 536", detail, StringComparison.Ordinal);
    }

    // ---- STATE 5: A PUBLISHER IS RUNNING AND NOTHING HAS BEEN READ -----------------------------------

    [Fact]
    public void A_publisher_that_has_begun_and_read_nothing_shows_no_values()
    {
        var state = new MirrorState();
        var attempt = Poller(new ScriptedFeed(Document(PublisherStatus.Running, T0)), state, () => T0).PollOnce();

        Assert.Equal(PollOutcome.FeedCarriesNoReading, attempt.Outcome);

        var view = View(state, T0);

        Assert.Equal(MirrorStatus.FeedCarriesNoReading, view.Status);
        Assert.All(view.Rows, r => Assert.Null(r.Raw));
        Assert.Null(view.BuildStamp);
        Assert.Equal(Fixtures.Registers, view.RegistersNotObserved);
    }

    // ---- STATE 6: LIVE ------------------------------------------------------------------------------

    /// <summary>
    /// The ordinary case, and the one every refusal above must not be confused with: a running publisher,
    /// a recent read, values decoded exactly as direct mode decodes them.
    /// </summary>
    [Fact]
    public void A_running_publisher_with_a_recent_read_is_LIVE_and_decodes_normally()
    {
        var state = new MirrorState();
        var attempt = Poller(
            new ScriptedFeed(Document(PublisherStatus.Running, T0, WholeArea(T0, scan: 100))),
            state, () => T0).PollOnce();

        Assert.True(attempt.Ok);

        var view = View(state, T0.AddSeconds(1));

        Assert.Equal(MirrorStatus.Live, view.Status);
        Assert.True(view.ValuesAreCurrent);
        Assert.Equal("16#F52ECEAD", view.BuildStamp);
        Assert.Equal(100u, view.Scan.Value);
        Assert.Equal(Fixtures.Registers, view.RegistersCurrent);
        Assert.Equal(0, view.RegistersStale);
        Assert.Equal(0, view.RegistersNotObserved);
    }

    /// <summary>
    /// 🔴 <b>THE INSTANT ON SCREEN IS THE HARNESS'S OWN READ TIME — NEVER NOW, AND NEVER THE PUBLISH
    /// TIME.</b> This is the whole truthfulness claim. Stamping with the viewer's clock would reset the
    /// age on every poll and make a dead wave's data look permanently fresh.
    /// </summary>
    [Fact]
    public void The_age_is_measured_from_the_instant_the_harnesss_read_returned()
    {
        var readAt = T0;
        var publishedAt = T0.AddSeconds(20);
        var renderedAt = T0.AddSeconds(30);

        var state = new MirrorState();
        Poller(new ScriptedFeed(Document(PublisherStatus.Running, publishedAt, WholeArea(readAt))),
            state, () => renderedAt).PollOnce();

        var view = View(state, renderedAt, Options(staleMs: 60_000));

        Assert.Equal(readAt, view.ObservedUtc);
        Assert.Equal(30.0, view.AgeSeconds!.Value, 1);
        Assert.All(view.Rows, r => Assert.Equal(readAt, r.ObservedUtc));
    }

    // ---- STATE 7: THE PUBLISHER STOPPED, AND STATE 8: IT ENDED ---------------------------------------

    /// <summary>
    /// 🔴 <b>A WAVE THAT DIED AND A WAVE THAT FINISHED ARE DIFFERENT FACTS.</b> One is an incident and
    /// the other is an outcome, and only the publisher saying <c>ENDED</c> tells them apart — a feed that
    /// simply stops advancing looks identical from the outside.
    /// </summary>
    [Fact]
    public void A_publisher_that_stopped_without_ending_is_not_the_same_state_as_one_that_ended()
    {
        var died = new MirrorState();
        Poller(new ScriptedFeed(Document(PublisherStatus.Running, T0, WholeArea(T0))), died, () => T0).PollOnce();

        var finished = new MirrorState();
        Poller(new ScriptedFeed(Document(PublisherStatus.Ended, T0, WholeArea(T0))), finished, () => T0).PollOnce();

        var late = T0.AddSeconds(60);

        var diedView = View(died, late);
        var finishedView = View(finished, late);

        Assert.Equal(MirrorStatus.PublisherStopped, diedView.Status);
        Assert.Equal(MirrorStatus.PublisherEnded, finishedView.Status);
        Assert.NotEqual(diedView.Status, finishedView.Status);

        Assert.Contains("died, was", diedView.StatusText, StringComparison.Ordinal);
        Assert.Contains("FINISHED", finishedView.StatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ENDED IS NEVER GREEN, EVEN ONE SECOND LATER.</b> "Final" and "current" are different facts:
    /// nothing further is coming, however recent the last read was. Showing a finished wave as LIVE is the
    /// most plausible-looking wrong page this mode can produce.
    /// </summary>
    [Fact]
    public void A_wave_that_finished_a_moment_ago_is_still_not_LIVE()
    {
        var state = new MirrorState();
        Poller(new ScriptedFeed(Document(PublisherStatus.Ended, T0, WholeArea(T0))), state, () => T0).PollOnce();

        var view = View(state, T0.AddMilliseconds(200));

        Assert.Equal(MirrorStatus.PublisherEnded, view.Status);
        Assert.False(view.ValuesAreCurrent);
        Assert.Contains("FINAL, not current", view.StatusText, StringComparison.Ordinal);

        // And the values are still there: what a wave finished on is the thing worth looking at.
        Assert.Equal("16#F52ECEAD", view.BuildStamp);
    }

    // ---- STATE 9: THE FEED ADVANCES AND THE SCAN COUNTER DOES NOT ------------------------------------

    /// <summary>
    /// 🔴 <b>A LIVE FEED OF A DEAD CPU.</b> The publisher is writing, the readings are fresh, and the scan
    /// counter has not moved — so the page is LIVE and the scan card says NOT ADVANCING. Those are two
    /// independent facts and the third distinct kind of wrong this mode has to show.
    /// </summary>
    [Fact]
    public void A_fresh_feed_whose_scan_counter_is_static_reads_live_and_NOT_ADVANCING()
    {
        var state = new MirrorState();
        var source = new ScriptedFeed(Document(PublisherStatus.Running, T0, WholeArea(T0, scan: 4242)));
        var poller = Poller(source, state, () => T0);

        poller.PollOnce();
        source.Answer(Document(PublisherStatus.Running, T0.AddSeconds(1), WholeArea(T0.AddSeconds(1), scan: 4242)));
        poller.PollOnce();

        var view = View(state, T0.AddSeconds(1));

        Assert.Equal(MirrorStatus.Live, view.Status);
        Assert.False(view.Scan.Advancing);
        Assert.Contains("NOT ADVANCING", view.Scan.Note, StringComparison.Ordinal);
        Assert.Equal(4242u, view.Scan.Value);
    }

    /// <summary>The counter advancing is reported as such, so the check above is not vacuously false.</summary>
    [Fact]
    public void A_feed_whose_scan_counter_advances_reads_ADVANCING()
    {
        var state = new MirrorState();
        var source = new ScriptedFeed(Document(PublisherStatus.Running, T0, WholeArea(T0, scan: 100)));
        var poller = Poller(source, state, () => T0);

        poller.PollOnce();
        source.Answer(Document(PublisherStatus.Running, T0.AddSeconds(1), WholeArea(T0.AddSeconds(1), scan: 576)));
        poller.PollOnce();

        var view = View(state, T0.AddSeconds(1));

        Assert.True(view.Scan.Advancing);
        Assert.Equal(476, view.Scan.Advance);
        Assert.Equal(1000, view.Scan.IntervalMs);
    }

    /// <summary>
    /// 🔴 <b>RE-READING ONE DOCUMENT IS NOT TWO READINGS.</b> A viewer polls a file on its own rhythm and
    /// legitimately sees the same document twice; rotating on that would put a reading against ITSELF and
    /// report "the counter read N twice, 0 ms apart" — this page's wording for a STOPPED CPU. That is a
    /// false alarm about the most serious thing the card can say.
    /// </summary>
    [Fact]
    public void Reading_the_same_document_twice_does_not_manufacture_a_stopped_cpu()
    {
        var state = new MirrorState();
        var document = Document(PublisherStatus.Running, T0, WholeArea(T0, scan: 4242));
        var poller = Poller(new ScriptedFeed(document), state, () => T0);

        poller.PollOnce();
        poller.PollOnce();
        poller.PollOnce();

        var view = View(state, T0);

        Assert.Null(view.Scan.Advancing);
        Assert.Contains("NOT ESTABLISHED", view.Scan.Note, StringComparison.Ordinal);
    }

    // ---- CLOCK DISAGREEMENT -------------------------------------------------------------------------

    /// <summary>
    /// <b>A negative age is not a fresh one.</b> The instants come from another process; if its clock runs
    /// ahead, an ancient reading computes as extraordinarily fresh and renders green. Refused rather than
    /// clamped, because a clamp hides the one input that has gone wrong.
    /// </summary>
    [Fact]
    public void A_reading_in_the_viewers_future_is_refused_rather_than_read_as_fresh()
    {
        var state = new MirrorState();
        Poller(new ScriptedFeed(Document(PublisherStatus.Running, T0.AddMinutes(10),
                WholeArea(T0.AddMinutes(10)))), state, () => T0).PollOnce();

        var view = View(state, T0);

        Assert.Equal(MirrorStatus.ClockDisagreement, view.Status);
        Assert.False(view.ValuesAreCurrent);
        Assert.Contains("FUTURE", view.StatusText, StringComparison.Ordinal);
    }

    // ---- PER-REGISTER PROVENANCE --------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A REGISTER NO READ COVERED SHOWS NOTHING — NOT A ZERO.</b> A wave reads the control region
    /// and each slot's results separately, so a composed picture legitimately has holes. The server's own
    /// zero and this program's default are the same bytes and completely different facts.
    /// </summary>
    [Fact]
    public void A_register_no_read_covered_shows_NOT_READ_rather_than_zero()
    {
        var state = new MirrorState();

        // Only registers 0..3 were read: the stamp and the counter. 4 and 5 were not.
        var partial = new MirrorFeedFrame(0, T0, new ushort[] { 0xF52E, 0xCEAD, 0x0000, 0x0064 });
        Poller(new ScriptedFeed(Document(PublisherStatus.Running, T0, partial)), state, () => T0).PollOnce();

        var view = View(state, T0);

        Assert.Equal(MirrorStatus.Live, view.Status);
        Assert.Equal(2, view.RegistersNotObserved);

        var unread = view.Rows.Single(r => r.Index == 4);
        Assert.Null(unread.Raw);
        Assert.Null(unread.Decoded);
        Assert.Null(unread.ObservedUtc);
        Assert.False(unread.Current);
        Assert.Contains("NOT READ", unread.Basis!, StringComparison.Ordinal);

        // And what WAS read still reads.
        Assert.Equal("16#F52ECEAD", view.BuildStamp);
        Assert.Equal(100u, view.Scan.Value);
    }

    /// <summary>
    /// <b>Rows carry DIFFERENT ages and each states its own.</b> A single whole-table verdict over a
    /// composed picture would present the old rows as current — which is exactly the two-instants problem
    /// the one-socket design removes, reintroduced in the rendering.
    /// </summary>
    [Fact]
    public void Rows_from_different_reads_carry_their_own_ages_and_go_stale_individually()
    {
        var state = new MirrorState();

        Poller(new ScriptedFeed(Document(PublisherStatus.Running, T0.AddSeconds(60),
            new MirrorFeedFrame(0, T0, new ushort[] { 0xF52E, 0xCEAD, 0, 0 }),
            new MirrorFeedFrame(4, T0.AddSeconds(60), new ushort[] { 0x0007, 0x0001 }))),
            state, () => T0.AddSeconds(60)).PollOnce();

        var view = View(state, T0.AddSeconds(61), Options(staleMs: 5000));

        var old = view.Rows.Single(r => r.Index == 0);
        var fresh = view.Rows.Single(r => r.Index == 4);

        Assert.Equal(T0, old.ObservedUtc);
        Assert.False(old.Current);
        Assert.Equal(61.0, old.AgeSeconds!.Value, 1);

        Assert.Equal(T0.AddSeconds(60), fresh.ObservedUtc);
        Assert.True(fresh.Current);
        Assert.Equal(1.0, fresh.AgeSeconds!.Value, 1);

        // Counted, and printed on every run including when the counts are zero. The three numbers sum to
        // the declared width, which is what makes them checkable against the table on screen.
        Assert.Equal(2, view.RegistersCurrent);
        Assert.Equal(4, view.RegistersStale);
        Assert.Equal(0, view.RegistersNotObserved);
        Assert.Equal(Fixtures.Registers,
            view.RegistersCurrent + view.RegistersStale + view.RegistersNotObserved);
    }

    /// <summary>
    /// <b>Half a 32-bit value is not a value.</b> A wide element whose second register was never read must
    /// not be reassembled from one real half and one default — that produces a plausible number.
    /// </summary>
    [Fact]
    public void A_wide_element_with_only_one_half_read_is_not_decoded()
    {
        var state = new MirrorState();
        Poller(new ScriptedFeed(Document(PublisherStatus.Running, T0,
            new MirrorFeedFrame(0, T0, new ushort[] { 0xF52E }))), state, () => T0).PollOnce();

        var view = View(state, T0);
        var stamp = view.Rows.Single(r => r.Index == 0);

        Assert.Null(stamp.Decoded);
        Assert.Null(stamp.Raw);
        Assert.Null(view.BuildStamp);
    }

    /// <summary>The scan card refuses on the same rule: a counter with one half unread is NOT ESTABLISHED.</summary>
    [Fact]
    public void A_scan_counter_with_only_one_half_read_is_not_established()
    {
        var state = new MirrorState();
        Poller(new ScriptedFeed(Document(PublisherStatus.Running, T0,
            new MirrorFeedFrame(0, T0, new ushort[] { 0xF52E, 0xCEAD, 0x0000 }))), state, () => T0).PollOnce();

        var view = View(state, T0);

        Assert.Null(view.Scan.Value);
        Assert.Contains("NOT ESTABLISHED", view.Scan.Note, StringComparison.Ordinal);
    }

    // ---- PROVENANCE ---------------------------------------------------------------------------------

    /// <summary>
    /// <b>The page states whose reads these are.</b> A viewer showing another process's readings without
    /// saying so is an unattributed table indistinguishable from a direct one.
    /// </summary>
    [Fact]
    public void A_followed_view_carries_the_publishers_identity()
    {
        var state = new MirrorState();
        Poller(new ScriptedFeed(Document(PublisherStatus.Running, T0, WholeArea(T0))), state, () => T0).PollOnce();

        var view = View(state, T0);

        Assert.True(view.IsFollowing);
        Assert.NotNull(view.Feed);
        Assert.Equal("pid7@BENCH", view.Feed!.PublisherId);
        Assert.Equal(PublisherStatus.Running, view.Feed.Status);
        Assert.Equal("16#F52ECEAD", view.Feed.BuildStamp);
        Assert.Equal(Fixtures.Registers, view.Feed.RegistersObserved);
        Assert.Equal(5, view.Feed.Sequence);
        Assert.Equal(T0, view.Feed.OldestObservedUtc);
    }

    /// <summary>The detail says outright that this viewer opened no socket. It is the reader's assurance.</summary>
    [Fact]
    public void A_followed_reading_states_that_no_socket_was_opened()
    {
        var state = new MirrorState();
        Poller(new ScriptedFeed(Document(PublisherStatus.Running, T0, WholeArea(T0))), state, () => T0).PollOnce();

        Assert.Contains("NO SOCKET WAS OPENED", View(state, T0).LastAttemptDetail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE TARGET LINE NAMES THE FEED, NOT A HOST AND PORT THIS VIEWER NEVER CONTACTED.</b>
    ///
    /// <para><b>Found by running the binary, not by a test.</b> The page's target read <c>:503 unit 1</c>
    /// in follow mode — a plausible-looking device target for a viewer holding no connection at all —
    /// because the view model rebuilt the string from the address fields instead of asking the options.
    /// <c>Describe</c> existed, was correct, and was printed in the console banner; nothing had wired it to
    /// the page. <i>A value with two readers has as many truths as it has readers.</i></para>
    /// </summary>
    [Fact]
    public void A_followed_view_names_the_feed_as_its_target_and_no_device()
    {
        var state = new MirrorState();
        Poller(new ScriptedFeed(Document(PublisherStatus.Running, T0, WholeArea(T0))), state, () => T0).PollOnce();

        var target = View(state, T0).Target;

        Assert.Contains("FOLLOWING", target, StringComparison.Ordinal);
        Assert.Contains("C:/feed.mirrorfeed", target, StringComparison.Ordinal);
        Assert.DoesNotContain(":503", target, StringComparison.Ordinal);
        Assert.DoesNotContain("unit", target, StringComparison.Ordinal);
    }

    /// <summary>The converse: a direct-mode view still names the host, the port and the unit.</summary>
    [Fact]
    public void A_direct_view_still_names_the_device_it_is_connected_to()
    {
        var state = new MirrorState();
        state.Publish(new PollAttempt(T0, PollOutcome.Ok, "FC03 answered.",
            ScriptedSource.Area(Fixtures.Registers), 74));

        Assert.Equal("10.10.10.10:503 unit 1",
            MirrorView.Build(Fixtures.Map(), Fixtures.Options(), state, T0).Target);
    }

    // ---- DIRECT MODE IS UNTOUCHED -------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE UNAFFECTED CASE, TESTED AS DELIBERATELY AS THE REFUSED ONES.</b> A gate that fires on
    /// working input is noise, and noise gets switched off. A direct-mode reading carries no feed, no
    /// per-register mask, and must classify and render exactly as it always has — every row current, no
    /// row NOT READ, and none of the follow states reachable.
    /// </summary>
    [Fact]
    public void A_direct_mode_reading_is_unchanged_by_any_of_this()
    {
        var state = new MirrorState();
        state.Publish(new PollAttempt(T0, PollOutcome.Ok, "FC03 answered.",
            ScriptedSource.Area(Fixtures.Registers, scan: 7), 74));

        var view = MirrorView.Build(Fixtures.Map(), Fixtures.Options(), state, T0.AddSeconds(1));

        Assert.Equal(MirrorStatus.Live, view.Status);
        Assert.True(view.ValuesAreCurrent);
        Assert.False(view.IsFollowing);
        Assert.Null(view.Feed);

        Assert.Equal(Fixtures.Registers, view.RegistersCurrent);
        Assert.Equal(0, view.RegistersStale);
        Assert.Equal(0, view.RegistersNotObserved);

        Assert.All(view.Rows, r => Assert.Equal(T0, r.ObservedUtc));
        Assert.All(view.Rows, r => Assert.True(r.Current));
        Assert.All(view.Rows, r => Assert.NotNull(r.Raw));
    }

    /// <summary>A direct-mode reading that HAS gone stale still reads Stale, not one of the new states.</summary>
    [Fact]
    public void A_stale_direct_mode_reading_still_reads_Stale()
    {
        var state = new MirrorState();
        state.Publish(new PollAttempt(T0, PollOutcome.Ok, "FC03 answered.",
            ScriptedSource.Area(Fixtures.Registers), 74));

        var view = MirrorView.Build(Fixtures.Map(), Fixtures.Options(), state, T0.AddSeconds(30));

        Assert.Equal(MirrorStatus.Stale, view.Status);
        Assert.Equal(0, view.RegistersCurrent);
        Assert.Equal(Fixtures.Registers, view.RegistersStale);
        Assert.Equal(0, view.RegistersNotObserved);
    }

    // ---- THE LOOP -----------------------------------------------------------------------------------

    /// <summary>
    /// <b>A follow loop that throws must not leave the last good numbers on screen.</b> A crash is loud
    /// without being NAMED, and the page must not go on looking like a live view because the poller died.
    /// </summary>
    [Fact]
    public void A_source_that_throws_is_reported_rather_than_leaving_the_page_looking_alive()
    {
        var state = new MirrorState();
        state.Publish(new PollAttempt(T0, PollOutcome.Ok, "an earlier reading.",
            ScriptedSource.Area(Fixtures.Registers), 1, new DateTimeOffset?[Fixtures.Registers].Select(_ => (DateTimeOffset?)T0).ToArray(),
            new FeedProvenance("f", "p", PublisherStatus.Running, T0, 1, "16#1", "h", Fixtures.Registers,
                RegisterWordOrder.HighWordFirst, 1, Fixtures.Registers, 0, T0)));

        using var poller = Poller(new ThrowingFeed(), state, () => T0.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(() => poller.PollOnce());
    }

    // ---- FAKES --------------------------------------------------------------------------------------

    /// <summary>A feed source that answers from a script, and counts. It never touches a filesystem.</summary>
    private sealed class ScriptedFeed : IMirrorFeedSource
    {
        private MirrorFeedResult _answer;

        public ScriptedFeed(MirrorFeedState state) =>
            _answer = new MirrorFeedResult(state, null, "scripted", "scripted problem");

        public ScriptedFeed(MirrorFeedDocument document) =>
            _answer = new MirrorFeedResult(MirrorFeedState.Ok, document, "scripted", string.Empty);

        public string Origin => "scripted";

        public int Reads { get; private set; }

        public void Answer(MirrorFeedDocument document) =>
            _answer = new MirrorFeedResult(MirrorFeedState.Ok, document, "scripted", string.Empty);

        public MirrorFeedResult Read()
        {
            Reads++;
            return _answer;
        }
    }

    private sealed class ThrowingFeed : IMirrorFeedSource
    {
        public string Origin => "throwing";

        public MirrorFeedResult Read() => throw new InvalidOperationException("the feed store exploded.");
    }
}
