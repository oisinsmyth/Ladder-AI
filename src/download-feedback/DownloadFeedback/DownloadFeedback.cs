using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ladder.Download
{
    /// <summary>
    /// Everything a download's result says, in the terms the coordinator needs: the load manifest,
    /// the run-state transitions, the non-object loads, the up-to-date signal, per-node counts with
    /// their nesting, whatever was not recognised, and one three-valued transfer verdict.
    /// </summary>
    public sealed class DownloadFeedback
    {
        internal DownloadFeedback(
            DownloadResultSummary result,
            IReadOnlyList<ClassifiedMessage> allMessages,
            IReadOnlyList<string> loadedObjects,
            int loadedObjectMessageCount,
            IReadOnlyList<string> duplicateLoadedObjects,
            IReadOnlyList<ClassifiedMessage> nonObjectLoads,
            IReadOnlyList<RunStateTransition> runStateTransitions,
            IReadOnlyList<ClassifiedMessage> upToDateSignals,
            IReadOnlyList<ClassifiedMessage> unrecognisedMessages,
            TransferVerdict verdict,
            string verdictReason,
            IReadOnlyList<string> anomalies)
        {
            Result = result;
            AllMessages = allMessages;
            LoadedObjects = loadedObjects;
            LoadedObjectMessageCount = loadedObjectMessageCount;
            DuplicateLoadedObjects = duplicateLoadedObjects;
            NonObjectLoads = nonObjectLoads;
            RunStateTransitions = runStateTransitions;
            UpToDateSignals = upToDateSignals;
            UnrecognisedMessages = unrecognisedMessages;
            Verdict = verdict;
            VerdictReason = verdictReason;
            Anomalies = anomalies;
        }

        /// <summary>The result as supplied, message tree and nesting intact.</summary>
        public DownloadResultSummary Result { get; }

        /// <summary>False when the download produced no result object at all.</summary>
        public bool ResultPresent => Result.IsPresent;

        /// <summary>
        /// Every message, pre-order, each with its kind and its path. Nothing is filtered out of
        /// this list — it is the audit trail behind every count below.
        /// </summary>
        public IReadOnlyList<ClassifiedMessage> AllMessages { get; }

        // ---- 1. THE LOAD MANIFEST -------------------------------------------------------------

        /// <summary>
        /// The set of program objects TIA reported loading, in first-seen order. THE positive
        /// evidence of transfer, and the set that gets compared against the coordinator's predicted
        /// change set.
        /// </summary>
        public IReadOnlyList<string> LoadedObjects { get; }

        /// <summary>Size of the manifest. 1 on a differential run, 99 on a full one — measured.</summary>
        public int LoadedObjectCount => LoadedObjects.Count;

        /// <summary>
        /// How many object-load MESSAGES there were, which is the manifest size unless a name
        /// repeated. Kept separate so a repeat is visible rather than silently deduplicated.
        /// </summary>
        public int LoadedObjectMessageCount { get; }

        public IReadOnlyList<string> DuplicateLoadedObjects { get; }

        // ---- 3. NON-OBJECT LOADS --------------------------------------------------------------

        /// <summary>
        /// Hardware / connection / routing configuration loads — three different wordings on one
        /// measured run, which is why they are recognised by shape and not by a list of names.
        /// </summary>
        public IReadOnlyList<ClassifiedMessage> NonObjectLoads { get; }

        public int NonObjectLoadCount => NonObjectLoads.Count;

        public IReadOnlyList<string> NonObjectLoadSubjects =>
            NonObjectLoads.Select(m => m.Subject ?? m.Text).ToList();

        /// <summary>
        /// Everything reported loaded by name, objects and non-objects together. This is what the
        /// verdict keys on.
        /// </summary>
        public int TransferredItemCount => LoadedObjectCount + NonObjectLoadCount;

        // ---- 2. RUN-STATE TRANSITIONS ---------------------------------------------------------

        /// <summary>Stops and starts, in the order the tree gave them.</summary>
        public IReadOnlyList<RunStateTransition> RunStateTransitions { get; }

        /// <summary>
        /// Whether the download said ANYTHING about run state. False means undisclosed, which is a
        /// gap in the evidence and not a reassurance that the CPU kept running.
        /// </summary>
        public bool RunStateDisclosed => RunStateTransitions.Count > 0;

        /// <summary>
        /// The last transition reported, or null when none was. Null is "not disclosed", never
        /// "still running".
        /// </summary>
        public RunStateEvent? FinalRunStateEvent =>
            RunStateTransitions.Count == 0 ? (RunStateEvent?)null : RunStateTransitions[RunStateTransitions.Count - 1].Transition;

        // ---- 4. THE UP-TO-DATE SIGNAL ---------------------------------------------------------

        public IReadOnlyList<ClassifiedMessage> UpToDateSignals { get; }

        public bool UpToDateSignalPresent => UpToDateSignals.Count > 0;

        // ---- SURFACING WHAT WE DO NOT KNOW ----------------------------------------------------

        /// <summary>
        /// Messages whose shape this parser does not recognise. Carried in full, never dropped:
        /// every download option measured so far produced a vocabulary nobody predicted, so this
        /// list being non-empty is the signal that the parser needs extending — and it can only be
        /// that signal if it is reported.
        /// </summary>
        public IReadOnlyList<ClassifiedMessage> UnrecognisedMessages { get; }

        public int UnrecognisedMessageCount => UnrecognisedMessages.Count;

        // ---- 5. PER-NODE STATE AND COUNTS -----------------------------------------------------

        /// <summary>Sum of every node's own error count.</summary>
        public int TotalNodeErrorCount => AllMessages.Sum(m => m.Node.ErrorCount);

        public int TotalNodeWarningCount => AllMessages.Sum(m => m.Node.WarningCount);

        public IReadOnlyList<ClassifiedMessage> MessagesWithErrors =>
            AllMessages.Where(m => m.Node.ErrorCount > 0).ToList();

        public IReadOnlyList<ClassifiedMessage> MessagesWithWarnings =>
            AllMessages.Where(m => m.Node.WarningCount > 0).ToList();

        /// <summary>Message counts by the node state string TIA gave, e.g. Success / Information.</summary>
        public IReadOnlyDictionary<string, int> MessageCountsByState =>
            AllMessages
                .GroupBy(m => m.Node.State ?? "(none)")
                .ToDictionary(g => g.Key, g => g.Count());

        // ---- THE VERDICT ----------------------------------------------------------------------

        public TransferVerdict Verdict { get; }

        /// <summary>The evidence the verdict rests on, in one sentence, for the log.</summary>
        public string VerdictReason { get; }

        /// <summary>
        /// Things that do not fit: conflicting evidence, repeated names, a present-but-empty result,
        /// unrecognised shapes. Never fatal on their own, always reported.
        /// </summary>
        public IReadOnlyList<string> Anomalies { get; }

        /// <summary>A human-readable summary, for a probe log or an agent's result.</summary>
        public string ToReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("TRANSFER VERDICT : " + Verdict.ToString().ToUpperInvariant());
            sb.AppendLine("  basis          : " + VerdictReason);
            sb.AppendLine("  load manifest  : " + LoadedObjectCount.ToString(CultureInfo.InvariantCulture) + " object(s)");
            if (LoadedObjectCount > 0)
            {
                foreach (var name in LoadedObjects)
                {
                    sb.AppendLine("      - " + name);
                }
            }

            sb.AppendLine("  non-object     : " + NonObjectLoadCount.ToString(CultureInfo.InvariantCulture) + " item(s)");
            foreach (var item in NonObjectLoads)
            {
                sb.AppendLine("      - " + item.Text);
            }

            sb.AppendLine("  run state      : " + (RunStateDisclosed
                ? string.Join(" -> ", RunStateTransitions.Select(t => t.Transition.ToString()).ToArray())
                : "NOT DISCLOSED — the download said nothing about it. That is a gap, not a reassurance."));

            sb.AppendLine("  up-to-date     : " + (UpToDateSignalPresent ? "stated" : "not stated"));
            sb.AppendLine("  result state   : " + (ResultPresent
                ? (Result.State ?? "(none)") + " errors=" + Result.ErrorCount.ToString(CultureInfo.InvariantCulture) +
                  " warnings=" + Result.WarningCount.ToString(CultureInfo.InvariantCulture) +
                  "  (reported only — takes no part in the verdict)"
                : "NO RESULT OBJECT EXISTS"));

            sb.AppendLine("  unrecognised   : " + UnrecognisedMessageCount.ToString(CultureInfo.InvariantCulture) + " message(s)");
            foreach (var item in UnrecognisedMessages)
            {
                sb.AppendLine("      ! " + item.Text);
            }

            foreach (var anomaly in Anomalies)
            {
                sb.AppendLine("  ANOMALY        : " + anomaly);
            }

            return sb.ToString();
        }
    }
}
