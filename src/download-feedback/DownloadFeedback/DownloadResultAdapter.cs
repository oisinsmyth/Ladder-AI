using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Download
{
    /// <summary>
    /// Builds a <see cref="DownloadResultSummary"/> out of any object graph, by projection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the seam that keeps this library free of <c>Siemens.Engineering</c>. The caller —
    /// which is the only place that can legally hold a <c>DownloadResult</c> — supplies six small
    /// functions and gets back a dependency-free tree. In <c>openness-cli</c> that wiring is a dozen
    /// lines:
    /// </para>
    /// <code>
    /// var summary = DownloadResultAdapter.Adapt&lt;DownloadResultMessage&gt;(
    ///     result.State.ToString(), result.ErrorCount, result.WarningCount, result.Messages,
    ///     m =&gt; m.Message,
    ///     m =&gt; m.State.ToString(),
    ///     m =&gt; (int)m.ErrorCount,
    ///     m =&gt; (int)m.WarningCount,
    ///     m =&gt; m.DateTime,
    ///     m =&gt; m.Messages);
    /// var feedback = DownloadFeedbackParser.Parse(summary);
    /// </code>
    /// <para>
    /// The exact member names on the Siemens side are deliberately NOT baked in here: the only
    /// evidence available while this was built is the probe's own rendering of that tree, and
    /// guessing an API surface from a log is how a component ships broken. The delegates are where
    /// that guess belongs — in the caller, where it fails to compile if it is wrong.
    /// </para>
    /// <para>
    /// DEPTH IS BOUNDED at <see cref="MaxDepth"/>. A malformed or cyclic graph must not take the
    /// coordinator down with a stack overflow, which is not a catchable failure; overrunning the
    /// bound truncates and says so in the node text, so the truncation surfaces as an unrecognised
    /// message rather than as silence.
    /// </para>
    /// </remarks>
    public static class DownloadResultAdapter
    {
        public const int MaxDepth = 64;

        internal const string TruncationMarker =
            "[download-feedback] message tree truncated: nesting exceeded MaxDepth";

        public static DownloadResultSummary Adapt<TMessage>(
            string? state,
            int errorCount,
            int warningCount,
            IEnumerable<TMessage>? messages,
            Func<TMessage, string?> text,
            Func<TMessage, string?>? nodeState = null,
            Func<TMessage, int>? nodeErrorCount = null,
            Func<TMessage, int>? nodeWarningCount = null,
            Func<TMessage, DateTimeOffset?>? timestamp = null,
            Func<TMessage, IEnumerable<TMessage>?>? children = null)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var roots = messages == null
                ? new List<DownloadMessageNode>()
                : messages
                    .Where(m => m != null)
                    .Select(m => Build(m, text, nodeState, nodeErrorCount, nodeWarningCount, timestamp, children, 0))
                    .ToList();

            return new DownloadResultSummary(state, errorCount, warningCount, roots);
        }

        private static DownloadMessageNode Build<TMessage>(
            TMessage message,
            Func<TMessage, string?> text,
            Func<TMessage, string?>? nodeState,
            Func<TMessage, int>? nodeErrorCount,
            Func<TMessage, int>? nodeWarningCount,
            Func<TMessage, DateTimeOffset?>? timestamp,
            Func<TMessage, IEnumerable<TMessage>?>? children,
            int depth)
        {
            var childNodes = new List<DownloadMessageNode>();

            if (children != null)
            {
                if (depth >= MaxDepth)
                {
                    childNodes.Add(new DownloadMessageNode(TruncationMarker));
                }
                else
                {
                    var kids = children(message);
                    if (kids != null)
                    {
                        foreach (var kid in kids)
                        {
                            if (kid == null)
                            {
                                continue;
                            }

                            childNodes.Add(Build(
                                kid, text, nodeState, nodeErrorCount, nodeWarningCount, timestamp, children, depth + 1));
                        }
                    }
                }
            }

            return new DownloadMessageNode(
                text(message) ?? string.Empty,
                nodeState == null ? null : nodeState(message),
                nodeErrorCount == null ? 0 : nodeErrorCount(message),
                nodeWarningCount == null ? 0 : nodeWarningCount(message),
                timestamp == null ? (DateTimeOffset?)null : timestamp(message),
                childNodes);
        }
    }
}
