using System;

namespace Ladder.Wave
{
    /// <summary>
    /// A marker operation that could not be carried out. NOTE THE THINGS THAT ARE NOT THIS: finding
    /// a marker on startup is not an exception, and neither is an unreadable one — both are states
    /// reported by <see cref="WaveMarkerStore.Read"/>.
    /// </summary>
    public class WaveMarkerException : Exception
    {
        /// <summary>Creates the exception.</summary>
        public WaveMarkerException(string message) : base(message)
        {
        }

        /// <summary>Creates the exception with an inner cause.</summary>
        public WaveMarkerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Thrown by <see cref="WaveMarkerStore.BeginWave"/> when a marker is already present.
    /// </summary>
    /// <remarks>
    /// Beginning a wave over a marker somebody else left would erase the only evidence that a
    /// previous wave never completed — and X-C item 3 requires those results to be DISCARDED, which
    /// is impossible once you no longer know they exist. The recovery is explicit and named:
    /// <see cref="WaveMarkerStore.DiscardInterruptedWave"/>.
    /// </remarks>
    public sealed class WaveAlreadyInProgressException : WaveMarkerException
    {
        internal WaveAlreadyInProgressException(WaveStatus status)
            : base("A wave-in-progress marker is already present at '" + status.MarkerPath + "'. " +
                   status.Describe() +
                   " Acknowledge it with DiscardInterruptedWave before beginning a new wave.")
        {
            Status = status;
        }

        /// <summary>What the existing marker said.</summary>
        public WaveStatus Status { get; }
    }
}
