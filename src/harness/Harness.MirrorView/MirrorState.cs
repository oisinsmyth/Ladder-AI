namespace Harness.MirrorView;

/// <summary>
/// What the page is entitled to say about how current its numbers are.
///
/// <para>🔴 <b>THE WHOLE REASON THIS ENUM EXISTS: A STALE VALUE MUST NEVER LOOK CURRENT.</b> There is
/// no member meaning "probably fine". Every state below is either "these numbers were read just now" or
/// a named reason they were not.</para>
/// </summary>
public enum MirrorStatus
{
    /// <summary>The last poll succeeded and its reading is inside the freshness window. Only this is green.</summary>
    Live,

    /// <summary>
    /// The polls are still succeeding as far as anything here knows, but the newest reading is older
    /// than the freshness window — so the poller is not delivering and the numbers on screen are not now.
    /// </summary>
    Stale,

    /// <summary>The last attempt did not produce a reading. Values, if any, are from an EARLIER poll.</summary>
    Failing,

    /// <summary>The device fence refused the target. No socket was opened. Not a network problem.</summary>
    Refused,

    /// <summary>The fence itself faulted. Nothing examined the target at all.</summary>
    FenceFault,

    /// <summary>No poll has ever succeeded. There is nothing on screen to be stale.</summary>
    NeverRead,

    // ---- FOLLOW MODE ------------------------------------------------------------------------------
    //
    // 🔴 *** "NO DATA", "OLD DATA" AND "DEAD DEVICE" ARE THREE DIFFERENT FACTS *** — and following a
    // wave's feed splits the first of those into three again. Every member below renders as its own
    // banner with its own words. NONE of them may be shown as a plausible page of numbers.

    /// <summary>
    /// <b>NO PUBLISHER HAS EVER WRITTEN TO THIS PATH.</b> No wave has run with <c>--publish</c> here.
    /// There is nothing to be stale and nothing is wrong.
    /// </summary>
    NoFeed,

    /// <summary>
    /// <b>A PUBLISHER WROTE HERE AND THE FEED IS NOT THERE NOW.</b> A publish did not complete, or the
    /// file was deleted. Never collapsed into <see cref="NoFeed"/>: that is the benign-looking state, and
    /// reading a lost feed as "nothing ever ran" is the mistake that costs the most.
    /// </summary>
    FeedInterrupted,

    /// <summary>The feed is present and could not be fully accounted for. Nothing from it is displayed.</summary>
    FeedUnreadable,

    /// <summary>
    /// <b>THE FEED AND THIS VIEWER'S MAP DESCRIBE DIFFERENT MIRRORS.</b> The registers would line up and
    /// mean different signals, so this refuses instead of rendering.
    /// </summary>
    FeedMismatch,

    /// <summary>A publisher is running and no read has landed yet. Nothing to show, which is not zeros.</summary>
    FeedCarriesNoReading,

    /// <summary>
    /// <b>THE PUBLISHER FINISHED.</b> The wave ended and wrote so. The values are FINAL, which is a
    /// different fact from CURRENT — nothing further is coming, however recent the last read was.
    /// </summary>
    PublisherEnded,

    /// <summary>
    /// <b>THE PUBLISHER WAS RUNNING AND HAS STOPPED WRITING.</b> It never said it had finished, so the
    /// wave died, was killed, or is wedged. Distinct from <see cref="PublisherEnded"/> because one is an
    /// outcome and the other is an incident.
    /// </summary>
    PublisherStopped,

    /// <summary>
    /// <b>THE FEED'S TIMESTAMPS ARE IN THIS VIEWER'S FUTURE.</b> Ages computed against them would be
    /// negative, and a negative age reads as extraordinarily fresh — so an ancient reading behind a clock
    /// that is minutes ahead would render green. Refused rather than clamped.
    /// </summary>
    ClockDisagreement,
}

/// <summary>
/// The newest readings, and the newest ATTEMPT, held separately — because they are different facts and
/// the page must show both.
///
/// <para>Thread-safe by a single lock: the poller writes from a background task and the HTTP handler
/// reads from request threads. A torn read here would put half of one poll's registers beside half of
/// another's, which is precisely the "picture of no single moment" this design exists to avoid.</para>
/// </summary>
public sealed class MirrorState
{
    private readonly object _gate = new();

    private PollAttempt? _lastAttempt;
    private PollAttempt? _lastSuccess;
    private PollAttempt? _previousSuccess;

    /// <summary>Record one poll.</summary>
    public void Publish(PollAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        lock (_gate)
        {
            _lastAttempt = attempt;

            if (!attempt.Ok) return;

            // 🔴 *** RE-READING ONE READING IS NOT TWO READINGS. *** In FOLLOW mode the viewer polls a
            // file and the wave publishes on its own rhythm, so the viewer legitimately sees the same
            // document twice — and rotating on that would put a reading against ITSELF, producing
            // "the counter read N twice, 0 ms apart", which is this page's wording for a STOPPED CPU. The
            // key is the observed instant, which is the harness's own read time and is identical only
            // when it IS the same read. Direct mode is untouched: two polls there always carry two
            // distinct instants from the viewer's own clock.
            if (_lastSuccess is not null && _lastSuccess.At == attempt.At)
            {
                _lastSuccess = attempt;
                return;
            }

            // Two successes are kept, and only successes: the scan counter's advance is a difference
            // between two READINGS, and a failed poll carries no counter to difference against.
            _previousSuccess = _lastSuccess;
            _lastSuccess = attempt;
        }
    }

    /// <summary>An immutable copy of everything the view needs, taken under the lock.</summary>
    public (PollAttempt? LastAttempt, PollAttempt? LastSuccess, PollAttempt? PreviousSuccess) Snapshot()
    {
        lock (_gate)
        {
            return (_lastAttempt, _lastSuccess, _previousSuccess);
        }
    }
}
