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
