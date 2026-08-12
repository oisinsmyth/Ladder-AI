using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Ladder.Download
{
    /// <summary>
    /// Recovers a <see cref="DownloadResultSummary"/> from the text of a <c>download-probe</c> log.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TWO REASONS THIS EXISTS, and neither is "the live path".
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// It makes fifteen recorded downloads a usable test fixture. Those runs cost a live rig, a CPU
    /// stop and a restart each; being able to re-derive a verdict from them with no device is the
    /// whole reason this component could be built and proved before the next download.
    /// </description></item>
    /// <item><description>
    /// Forensics. A log that already exists can be re-read by the fixed parser and the old verdict
    /// checked against the new one.
    /// </description></item>
    /// </list>
    /// <para>
    /// The live path is <see cref="DownloadResultAdapter"/>, straight off the Openness object. Never
    /// route a live download through a log file: the log is the probe's RENDERING of the result, and
    /// anything the renderer drops is gone before this reader sees it.
    /// </para>
    /// <para>
    /// A log with no <c>==== DOWNLOAD RESULT</c> section yields
    /// <see cref="DownloadResultSummary.Absent"/> — the aborted-run case, which must read as
    /// undetermined and not as nothing-transferred.
    /// </para>
    /// </remarks>
    public static class ProbeLogReader
    {
        private static readonly Regex SectionHeader = new Regex(@"^====\s", RegexOptions.CultureInvariant);

        private static readonly Regex ResultHeader =
            new Regex(@"^====\s+DOWNLOAD RESULT\b", RegexOptions.CultureInvariant);

        private static readonly Regex ScalarLine =
            new Regex(@"^(?<key>state|errors|warnings)\s*:\s*(?<value>.*?)\s*$", RegexOptions.CultureInvariant);

        private static readonly Regex MessagesHeader =
            new Regex(@"^messages\b.*:\s*$", RegexOptions.CultureInvariant);

        private static readonly Regex NodeLine = new Regex(
            @"^(?<indent>\s*)-\s+state=(?<state>\S+)\s+errors=(?<errors>\d+)\s+warnings=(?<warnings>\d+)(?:\s+at=(?<at>\S+))?\s*$",
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

        private static readonly Regex TextLine =
            new Regex(@"^\s*text\s*:\s?(?<text>.*?)\s*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

        public static DownloadResultSummary Read(string? logText)
        {
            if (string.IsNullOrEmpty(logText))
            {
                return DownloadResultSummary.Absent;
            }

            var lines = logText!.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            var start = -1;
            for (var i = 0; i < lines.Length; i++)
            {
                if (ResultHeader.IsMatch(lines[i]))
                {
                    start = i + 1;
                    break;
                }
            }

            if (start < 0)
            {
                return DownloadResultSummary.Absent;
            }

            string? state = null;
            var errors = 0;
            var warnings = 0;
            var messagesAt = -1;

            var end = lines.Length;
            for (var i = start; i < lines.Length; i++)
            {
                if (SectionHeader.IsMatch(lines[i]))
                {
                    end = i;
                    break;
                }

                if (messagesAt < 0)
                {
                    if (MessagesHeader.IsMatch(lines[i]))
                    {
                        messagesAt = i + 1;
                        continue;
                    }

                    var scalar = ScalarLine.Match(lines[i]);
                    if (scalar.Success)
                    {
                        var value = scalar.Groups["value"].Value;
                        switch (scalar.Groups["key"].Value)
                        {
                            case "state":
                                state = value.Length == 0 ? null : value;
                                break;
                            case "errors":
                                errors = ParseInt(value);
                                break;
                            case "warnings":
                                warnings = ParseInt(value);
                                break;
                        }
                    }
                }
            }

            var roots = messagesAt < 0
                ? new List<DownloadMessageNode>()
                : ReadMessages(lines, messagesAt, end);

            return new DownloadResultSummary(state, errors, warnings, roots);
        }

        private static int ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }

        private static IReadOnlyList<DownloadMessageNode> ReadMessages(string[] lines, int from, int to)
        {
            // Mutable builders, because a node's children are only known once the next line at a
            // shallower indent arrives.
            var roots = new List<Builder>();
            var stack = new List<Builder>();

            for (var i = from; i < to; i++)
            {
                var node = NodeLine.Match(lines[i]);
                if (!node.Success)
                {
                    continue;
                }

                var indent = node.Groups["indent"].Value.Length;

                var text = string.Empty;
                if (i + 1 < to)
                {
                    var textMatch = TextLine.Match(lines[i + 1]);
                    if (textMatch.Success)
                    {
                        text = textMatch.Groups["text"].Value;
                        i++;
                    }
                }

                var builder = new Builder
                {
                    Indent = indent,
                    Text = text,
                    State = node.Groups["state"].Value,
                    Errors = ParseInt(node.Groups["errors"].Value),
                    Warnings = ParseInt(node.Groups["warnings"].Value),
                    Timestamp = ParseTimestamp(node.Groups["at"].Success ? node.Groups["at"].Value : null),
                };

                while (stack.Count > 0 && stack[stack.Count - 1].Indent >= indent)
                {
                    stack.RemoveAt(stack.Count - 1);
                }

                if (stack.Count == 0)
                {
                    roots.Add(builder);
                }
                else
                {
                    stack[stack.Count - 1].Children.Add(builder);
                }

                stack.Add(builder);
            }

            var result = new List<DownloadMessageNode>();
            foreach (var root in roots)
            {
                result.Add(root.Build());
            }

            return result;
        }

        private static DateTimeOffset? ParseTimestamp(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            DateTimeOffset parsed;
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out parsed)
                ? parsed
                : (DateTimeOffset?)null;
        }

        private sealed class Builder
        {
            public int Indent;
            public string Text = string.Empty;
            public string? State;
            public int Errors;
            public int Warnings;
            public DateTimeOffset? Timestamp;
            public readonly List<Builder> Children = new List<Builder>();

            public DownloadMessageNode Build()
            {
                var kids = new List<DownloadMessageNode>();
                foreach (var child in Children)
                {
                    kids.Add(child.Build());
                }

                return new DownloadMessageNode(Text, State, Errors, Warnings, Timestamp, kids);
            }
        }
    }
}
