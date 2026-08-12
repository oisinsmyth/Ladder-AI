using System;

namespace Ladder.Wave
{
    /// <summary>
    /// What the marker file says. Three states, and only one of them means "start clean".
    /// </summary>
    /// <remarks>
    /// *** <see cref="NoWaveInProgress"/> IS DELIBERATELY NOT THE ZERO VALUE. *** The zero value is
    /// <see cref="WaveInProgressDetailsUnreadable"/>, so a defaulted field, a zeroed struct or a
    /// value that got lost on the way somewhere lands on "a wave was in flight" — the direction X-C
    /// requires and the direction FI-44 states as a rule: empty is not clean. The only state a caller
    /// must never arrive at by accident is the one that says the rig is fresh.
    /// </remarks>
    public enum WaveMarkerState
    {
        /// <summary>
        /// A marker is present but its contents could not be read — torn write, truncation, a format
        /// this build does not know, or a file that would not open. A WAVE WAS IN PROGRESS. What was
        /// in it is unknown, which is worse than knowing, not better.
        /// </summary>
        WaveInProgressDetailsUnreadable = 0,

        /// <summary>A marker is present and readable. A wave was in progress; <see cref="WaveStatus.Marker"/> says which.</summary>
        WaveInProgress = 1,

        /// <summary>No marker. The coordinator is starting clean.</summary>
        NoWaveInProgress = 2,
    }

    /// <summary>
    /// The answer to the one question a restarting coordinator asks: was a wave in flight when I
    /// stopped existing?
    /// </summary>
    /// <remarks>
    /// *** FINDING A MARKER IS NOT AN ERROR AND THIS TYPE IS SHAPED SO IT CANNOT READ AS ONE. ***
    /// <see cref="WaveMarkerStore.Read"/> returns one of these; it does not throw, does not report a
    /// failure, and names the affirmative case (<see cref="WaveWasInProgress"/>) rather than the
    /// negative one. A marker on startup is the EXPECTED signal after a coordinator death — the
    /// mechanism working, not the mechanism complaining.
    /// </remarks>
    public sealed class WaveStatus
    {
        private WaveStatus(string path, WaveMarkerState state, WaveMarker? marker, string? problem, CoordinatorIdentity current)
        {
            MarkerPath = path;
            State = state;
            Marker = marker;
            Problem = problem;
            CurrentCoordinator = current;
        }

        /// <summary>Where the marker was looked for.</summary>
        public string MarkerPath { get; }

        /// <summary>Which of the three states was found.</summary>
        public WaveMarkerState State { get; }

        /// <summary>
        /// What was in flight, when it could be read. Null in both other states — and null does NOT
        /// mean no wave; check <see cref="WaveWasInProgress"/> for that.
        /// </summary>
        public WaveMarker? Marker { get; }

        /// <summary>Why the marker could not be read, when it could not. Null otherwise.</summary>
        public string? Problem { get; }

        /// <summary>The identity this read compared the marker against.</summary>
        public CoordinatorIdentity CurrentCoordinator { get; }

        /// <summary>
        /// TRUE IF A WAVE WAS IN FLIGHT — including when the marker is unreadable. The single
        /// question X-C item 2 exists to answer.
        /// </summary>
        public bool WaveWasInProgress => State != WaveMarkerState.NoWaveInProgress;

        /// <summary>
        /// True when the marker was written by THIS process. False for an unreadable marker, because
        /// a marker we cannot read is not one we can claim.
        /// </summary>
        public bool WrittenByThisProcess =>
            Marker != null && Marker.Coordinator.Equals(CurrentCoordinator);

        /// <summary>
        /// STALE — a marker left by something that is not this process. On startup every found marker
        /// is stale, and that is exactly the signal: it means the coordinator that wrote it did not
        /// live to clear it. An unreadable marker counts as stale.
        /// </summary>
        public bool IsStale => WaveWasInProgress && !WrittenByThisProcess;

        /// <summary>How long ago the wave started, when that is known.</summary>
        public TimeSpan? Age(DateTimeOffset now) => Marker?.Age(now);

        /// <summary>A line for the startup log, in the affirmative.</summary>
        public string Describe()
        {
            switch (State)
            {
                case WaveMarkerState.WaveInProgress:
                    return "A WAVE WAS IN PROGRESS: " + Marker +
                           (IsStale
                               ? ". The coordinator that wrote it is not this process, so it did not live to clear it. " +
                                 "Every test and result in that wave is INVALID: discard, never re-read, and force inert " +
                                 "before anything else."
                               : ". Written by this process.");

                case WaveMarkerState.WaveInProgressDetailsUnreadable:
                    return "A WAVE WAS IN PROGRESS AND ITS DETAILS ARE UNREADABLE (" + Problem +
                           "). The marker exists, so a wave was begun; what was in it is unknown. Treat the whole " +
                           "rig state as invalid, force inert, and do not read any result from it.";

                default:
                    return "No wave was in progress. Starting clean. (Still force inert on connect — X-C item 1: assume nothing.)";
            }
        }

        /// <inheritdoc />
        public override string ToString() => Describe();

        internal static WaveStatus None(string path, CoordinatorIdentity current) =>
            new WaveStatus(path, WaveMarkerState.NoWaveInProgress, null, null, current);

        internal static WaveStatus InProgress(string path, WaveMarker marker, CoordinatorIdentity current) =>
            new WaveStatus(path, WaveMarkerState.WaveInProgress, marker, null, current);

        internal static WaveStatus Unreadable(string path, string problem, CoordinatorIdentity current) =>
            new WaveStatus(path, WaveMarkerState.WaveInProgressDetailsUnreadable, null, problem, current);
    }
}
