using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ladder.Download
{
    /// <summary>
    /// Turns a download's message tree into <see cref="DownloadFeedback"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE VERDICT IS KEYED ON THE LOAD MANIFEST AND ON NOTHING ELSE. Two specific readings are
    /// excluded by construction, because the probe this replaces did both at once:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// It is never inferred from the ABSENCE of an up-to-date phrase. Absence of a denial is not
    /// evidence of an act. The manifest is the evidence, and on the run that prompted this there
    /// were ninety-nine positive load messages sitting unread while the verdict was derived from a
    /// phrase that was not there.
    /// </description></item>
    /// <item><description>
    /// It is never read off <c>state=Success</c>. The first run that ever returned Success
    /// transferred nothing at all. <see cref="DownloadResultSummary.State"/> is reported and never
    /// consulted here — grep this file for it and you will find it only in the report, not in the
    /// decision.
    /// </description></item>
    /// </list>
    /// <para>
    /// Fixing one of those and leaving the other would have left the defect in place, so both are
    /// excluded, and the tests assert each independently.
    /// </para>
    /// </remarks>
    public static class DownloadFeedbackParser
    {
        /// <summary>
        /// Parse a download result. Pass <see cref="DownloadResultSummary.Absent"/> — or null — when
        /// the download produced no result at all; that yields
        /// <see cref="TransferVerdict.Undetermined"/>, never a negative.
        /// </summary>
        public static DownloadFeedback Parse(DownloadResultSummary? result)
        {
            var summary = result ?? DownloadResultSummary.Absent;

            var all = new List<ClassifiedMessage>();
            var order = 0;
            foreach (var root in summary.Messages)
            {
                Walk(root, new List<string>(), all, ref order);
            }

            var loadedObjects = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new List<string>();
            var loadedObjectMessageCount = 0;

            var nonObjectLoads = new List<ClassifiedMessage>();
            var runState = new List<RunStateTransition>();
            var upToDate = new List<ClassifiedMessage>();
            var unrecognised = new List<ClassifiedMessage>();

            foreach (var message in all)
            {
                switch (message.Kind)
                {
                    case DownloadMessageKind.ObjectLoad:
                        loadedObjectMessageCount++;
                        var name = message.Subject ?? message.Text;
                        if (seen.Add(name))
                        {
                            loadedObjects.Add(name);
                        }
                        else if (!duplicates.Contains(name))
                        {
                            duplicates.Add(name);
                        }

                        break;

                    case DownloadMessageKind.NonObjectLoad:
                        nonObjectLoads.Add(message);
                        break;

                    case DownloadMessageKind.CpuStopped:
                        runState.Add(new RunStateTransition(
                            RunStateEvent.Stopped, message.Subject, message.Node.Timestamp, message.Order, message.Text));
                        break;

                    case DownloadMessageKind.CpuStarted:
                        runState.Add(new RunStateTransition(
                            RunStateEvent.Started, message.Subject, message.Node.Timestamp, message.Order, message.Text));
                        break;

                    case DownloadMessageKind.UpToDate:
                        upToDate.Add(message);
                        break;

                    case DownloadMessageKind.Unrecognised:
                        unrecognised.Add(message);
                        break;

                    case DownloadMessageKind.Container:
                    default:
                        break;
                }
            }

            var transferredItems = loadedObjects.Count + nonObjectLoads.Count;
            var anomalies = new List<string>();

            TransferVerdict verdict;
            string reason;

            if (!summary.IsPresent)
            {
                verdict = TransferVerdict.Undetermined;
                reason = "No DownloadResult exists — the download aborted or threw before producing one. " +
                         "Nothing is known about what reached the device; read the device to find out.";
            }
            else if (transferredItems > 0)
            {
                verdict = TransferVerdict.Transferred;
                reason = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} item(s) were reported loaded BY NAME ({1} program object(s) + {2} non-object item(s)). " +
                    "Positive evidence, from the load manifest.",
                    transferredItems, loadedObjects.Count, nonObjectLoads.Count);
            }
            else if (upToDate.Count > 0)
            {
                verdict = TransferVerdict.NothingTransferred;
                reason = "TIA stated positively that it did not load, because the target was up-to-date, " +
                         "and named nothing loaded. Success with nothing transferred.";
            }
            else
            {
                verdict = TransferVerdict.Undetermined;
                reason = "The result names nothing loaded AND does not say it skipped the transfer. " +
                         "That is not a negative: the result state says nothing about transfer in either direction.";
            }

            if (upToDate.Count > 0 && transferredItems > 0)
            {
                anomalies.Add(
                    "CONFLICTING EVIDENCE: the result carries an up-to-date statement AND " +
                    transferredItems.ToString(CultureInfo.InvariantCulture) +
                    " load message(s). The verdict follows the load messages, because a named load is " +
                    "the stronger evidence, but this combination has never been observed and should be read.");
            }

            if (duplicates.Count > 0)
            {
                anomalies.Add(
                    "REPEATED OBJECT NAME(S) in the load manifest: " + string.Join(", ", duplicates.ToArray()) +
                    ". The manifest is a set, so the count is of distinct names; " +
                    loadedObjectMessageCount.ToString(CultureInfo.InvariantCulture) + " message(s) produced " +
                    loadedObjects.Count.ToString(CultureInfo.InvariantCulture) + " name(s).");
            }

            if (unrecognised.Count > 0)
            {
                anomalies.Add(
                    unrecognised.Count.ToString(CultureInfo.InvariantCulture) +
                    " message(s) of a shape this parser does not recognise. They are carried in full and " +
                    "counted as nothing — a new vocabulary is the expected way this parser goes out of date.");
            }

            if (summary.IsPresent && all.Count == 0)
            {
                anomalies.Add("The result exists but carries NO messages at all. Empty is not clean.");
            }

            return new DownloadFeedback(
                summary,
                all,
                loadedObjects,
                loadedObjectMessageCount,
                duplicates,
                nonObjectLoads,
                runState,
                upToDate,
                unrecognised,
                verdict,
                reason,
                anomalies);
        }

        private static void Walk(
            DownloadMessageNode node,
            List<string> path,
            List<ClassifiedMessage> into,
            ref int order)
        {
            string? subject;
            var kind = MessageClassifier.Classify(node, out subject);
            into.Add(new ClassifiedMessage(node, kind, subject, order++, path));

            if (node.Children.Count == 0)
            {
                return;
            }

            path.Add(node.Text);
            foreach (var child in node.Children)
            {
                Walk(child, path, into, ref order);
            }

            path.RemoveAt(path.Count - 1);
        }
    }
}
