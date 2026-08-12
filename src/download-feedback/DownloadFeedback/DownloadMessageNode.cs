using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Download
{
    /// <summary>
    /// One node of a download's message tree, in a form that carries no Siemens dependency.
    /// </summary>
    /// <remarks>
    /// This mirrors what a Siemens <c>DownloadResultMessage</c> exposes — text, state, error and
    /// warning counts, timestamp, children — and nothing more. Keeping it a plain DTO is what lets
    /// the parser be exercised against recorded logs with no Portal, no device and no net48
    /// constraint, and it is why the fifteen probe logs are a usable fixture at all.
    ///
    /// NESTING IS PRESERVED DELIBERATELY (spec §9c item 5). The hardware download nests its real
    /// content one level deeper than the software download does, under a bare
    /// <c>Hardware configuration</c> group node, so a parser that flattens on the way in cannot
    /// later tell a group header from a load message.
    /// </remarks>
    public sealed class DownloadMessageNode
    {
        public DownloadMessageNode(
            string text,
            string? state = null,
            int errorCount = 0,
            int warningCount = 0,
            DateTimeOffset? timestamp = null,
            IEnumerable<DownloadMessageNode>? children = null)
        {
            Text = text ?? string.Empty;
            State = state;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            Timestamp = timestamp;
            Children = children == null
                ? EmptyChildren
                : children.Where(c => c != null).ToList();
        }

        private static readonly IReadOnlyList<DownloadMessageNode> EmptyChildren =
            new List<DownloadMessageNode>();

        /// <summary>The message text, exactly as TIA produced it. Never normalised.</summary>
        public string Text { get; }

        /// <summary>
        /// The node's own state, as a string (<c>Success</c>, <c>Information</c>, <c>Warning</c>, …).
        /// A string rather than an enum on purpose: a state name this codebase has not seen must
        /// survive the trip rather than being coerced into one it has.
        /// </summary>
        public string? State { get; }

        public int ErrorCount { get; }

        public int WarningCount { get; }

        public DateTimeOffset? Timestamp { get; }

        public IReadOnlyList<DownloadMessageNode> Children { get; }

        public override string ToString() => Text;
    }
}
