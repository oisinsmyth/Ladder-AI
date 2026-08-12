using System.Collections.Generic;
using System.Linq;

namespace Ladder.Download
{
    /// <summary>
    /// A whole <c>DownloadResult</c> in dependency-free form: the top-level state and counts, plus
    /// the message tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ABSENCE IS MODELLED EXPLICITLY, by <see cref="Absent"/>. An aborted download produces no
    /// <c>DownloadResult</c> at all, and "no result" is a materially different thing from "a result
    /// that reported nothing" — the first says the question was never answered, the second says it
    /// was answered with nothing. Collapsing them is exactly the mistake spec §9c's third design
    /// rule forbids (undetermined must never become a negative), so they are two distinct inputs
    /// producing two distinct outputs, not a null that callers may or may not remember to check.
    /// </para>
    /// <para>
    /// <see cref="State"/> is carried because the caller may want to report it. It takes NO part in
    /// the transfer verdict — see <see cref="DownloadFeedback.Verdict"/>.
    /// </para>
    /// </remarks>
    public sealed class DownloadResultSummary
    {
        /// <summary>
        /// The download produced no result object at all — it aborted, threw, or was never reached.
        /// </summary>
        public static readonly DownloadResultSummary Absent = new DownloadResultSummary();

        private DownloadResultSummary()
        {
            IsPresent = false;
            Messages = new List<DownloadMessageNode>();
        }

        public DownloadResultSummary(
            string? state,
            int errorCount,
            int warningCount,
            IEnumerable<DownloadMessageNode>? messages)
        {
            IsPresent = true;
            State = state;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            Messages = messages == null
                ? new List<DownloadMessageNode>()
                : messages.Where(m => m != null).ToList();
        }

        /// <summary>False when the download produced no result object at all.</summary>
        public bool IsPresent { get; }

        /// <summary>The result's own state string. NOT evidence of transfer, in either direction.</summary>
        public string? State { get; }

        public int ErrorCount { get; }

        public int WarningCount { get; }

        /// <summary>The message tree's roots, nesting intact.</summary>
        public IReadOnlyList<DownloadMessageNode> Messages { get; }
    }
}
