using System.Text.RegularExpressions;

namespace Ladder.Download
{
    /// <summary>
    /// Recognises a single message's shape. Shape only — it makes no judgement about the download.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PATTERNS ARE ANCHORED AND ORDERED. Anchoring is what stops the hardware download's group
    /// header <c>Hardware configuration</c> from being read as its load message
    /// <c>Hardware configuration was loaded successfully.</c> — a substring match on
    /// "Hardware configuration" reads one as the other and reports a transfer that did not happen at
    /// that node.
    /// </para>
    /// <para>
    /// Both verbs are accepted wherever a load is recognised, because TIA demonstrably varies them:
    /// on the same hardware download, hardware and routing were "loaded" and connection was
    /// "downloaded".
    /// </para>
    /// <para>
    /// Matching is case-insensitive. That is not laxity about vocabulary: a change of WORDING must
    /// still fall through to <see cref="DownloadMessageKind.Unrecognised"/> and be surfaced, and it
    /// still does. Only capitalisation is forgiven.
    /// </para>
    /// </remarks>
    internal static class MessageClassifier
    {
        private const RegexOptions Opts =
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;

        // 'DB_Sample' was loaded successfully.
        private static readonly Regex ObjectLoad =
            new Regex(@"^'(?<name>[^']+)' was (?:loaded|downloaded) successfully\.$", Opts);

        // Hardware configuration was loaded successfully. / Connection configuration was downloaded successfully.
        private static readonly Regex NonObjectLoad =
            new Regex(@"^(?<subject>[^'].*?) was (?:loaded|downloaded) successfully\.$", Opts);

        // The software has not been loaded, because it is up-to-date.
        private static readonly Regex UpToDate =
            new Regex(@"^The (?<subject>.+?) has not been loaded, because it is up-to-date\.$", Opts);

        // PLC_1 stopped.
        private static readonly Regex CpuStopped =
            new Regex(@"^(?<device>\S.*?) stopped\.$", Opts);

        // PLC_1 started.
        private static readonly Regex CpuStarted =
            new Regex(@"^(?<device>\S.*?) started\.$", Opts);

        public static DownloadMessageKind Classify(DownloadMessageNode node, out string? subject)
        {
            subject = null;
            var text = (node.Text ?? string.Empty).Trim();

            if (text.Length == 0)
            {
                return node.Children.Count > 0 ? DownloadMessageKind.Container : DownloadMessageKind.Unrecognised;
            }

            var m = ObjectLoad.Match(text);
            if (m.Success)
            {
                subject = m.Groups["name"].Value;
                return DownloadMessageKind.ObjectLoad;
            }

            m = UpToDate.Match(text);
            if (m.Success)
            {
                subject = m.Groups["subject"].Value;
                return DownloadMessageKind.UpToDate;
            }

            m = NonObjectLoad.Match(text);
            if (m.Success)
            {
                subject = m.Groups["subject"].Value;
                return DownloadMessageKind.NonObjectLoad;
            }

            m = CpuStopped.Match(text);
            if (m.Success)
            {
                subject = m.Groups["device"].Value;
                return DownloadMessageKind.CpuStopped;
            }

            m = CpuStarted.Match(text);
            if (m.Success)
            {
                subject = m.Groups["device"].Value;
                return DownloadMessageKind.CpuStarted;
            }

            // A node that carries children and is not a sentence is a group header. Positive rule:
            // it needs BOTH the children and the absence of a terminating period, so an unfamiliar
            // sentence never disappears into this bucket by having happened to nest something.
            if (node.Children.Count > 0 && !text.EndsWith("."))
            {
                return DownloadMessageKind.Container;
            }

            return DownloadMessageKind.Unrecognised;
        }
    }
}
