using System;

namespace Ladder.Download
{
    public enum RunStateEvent
    {
        Stopped,
        Started,
    }

    /// <summary>
    /// A CPU stop or start, as TIA reported it during the download.
    /// </summary>
    /// <remarks>
    /// THESE ARE TIA'S WORDS ABOUT WHAT THE DOWNLOAD DID, NOT A READING OF THE CPU'S MODE. Nothing
    /// in this library asks a controller anything. An empty transition list therefore means "the
    /// download said nothing about run state" — it does NOT mean the CPU kept running, and the
    /// caller must not read it that way. See <see cref="DownloadFeedback.RunStateDisclosed"/>.
    /// </remarks>
    public sealed class RunStateTransition
    {
        public RunStateTransition(RunStateEvent transition, string? device, DateTimeOffset? timestamp, int order, string text)
        {
            Transition = transition;
            Device = device;
            Timestamp = timestamp;
            Order = order;
            Text = text;
        }

        public RunStateEvent Transition { get; }

        public string? Device { get; }

        public DateTimeOffset? Timestamp { get; }

        /// <summary>Pre-order position in the message tree — the ordering the spec asks be kept.</summary>
        public int Order { get; }

        public string Text { get; }

        public override string ToString() => Text;
    }
}
