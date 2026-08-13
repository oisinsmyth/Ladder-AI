using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>Why one restored entry was not put back into the queues.</summary>
    public sealed class RehydrationRejection
    {
        internal RehydrationRejection(PersistedQueueEntry entry, AdmissionDecision decision, string detail)
        {
            Entry = entry;
            Decision = decision;
            Detail = detail;
        }

        /// <summary>The entry as it was read off disk.</summary>
        public PersistedQueueEntry Entry { get; }

        /// <summary>The re-admission decision, carrying every finding.</summary>
        public AdmissionDecision Decision { get; }

        /// <summary>What went wrong, in a sentence.</summary>
        public string Detail { get; }

        /// <inheritdoc />
        public override string ToString() =>
            "REHYDRATION REJECTED " + Entry.Submission.Id + ": " + Detail +
            (Decision.Findings.Count == 0
                ? string.Empty
                : " [" + string.Join("; ", Decision.Findings.Select(f => f.ToString()).ToArray()) + "]");
    }

    /// <summary>
    /// The result of putting a persisted queue state back into live queues, RE-GATED.
    /// </summary>
    public sealed class QueueRehydration
    {
        internal QueueRehydration(
            QueueRestoreState state,
            WaveQueues? queues,
            IReadOnlyList<QueuedSubmission> readmitted,
            IReadOnlyList<RehydrationRejection> rejected,
            string summary)
        {
            State = state;
            Queues = queues;
            Readmitted = readmitted;
            Rejected = rejected;
            Summary = summary;
        }

        /// <summary>The state of the read this rehydration was built from.</summary>
        public QueueRestoreState State { get; }

        /// <summary>
        /// The live queues — NULL unless <see cref="State"/> is <see cref="QueueRestoreState.Restored"/>.
        /// Null rather than empty on purpose: an unreadable or absent state must not hand back
        /// something a caller can use without noticing which of the three it got.
        /// </summary>
        public WaveQueues? Queues { get; }

        /// <summary>The entries that passed the gate again and are back in the queues.</summary>
        public IReadOnlyList<QueuedSubmission> Readmitted { get; }

        /// <summary>The entries that did NOT, each with the findings that refused it.</summary>
        public IReadOnlyList<RehydrationRejection> Rejected { get; }

        /// <summary>A sentence describing the outcome.</summary>
        public string Summary { get; }

        /// <summary>TRUE only when every restored entry was re-admitted.</summary>
        public bool Complete => State == QueueRestoreState.Restored && Rejected.Count == 0;

        /// <summary>One line for the log, plus every rejection.</summary>
        public string Describe()
        {
            var head = "REHYDRATE (" + State + "): " + Readmitted.Count + " re-admitted, " +
                       Rejected.Count + " rejected :: " + Summary;

            return Rejected.Count == 0
                ? head
                : head + Environment.NewLine +
                  string.Join(Environment.NewLine, Rejected.Select(r => "  - " + r).ToArray());
        }

        /// <inheritdoc />
        public override string ToString() => Describe();
    }

    /// <summary>
    /// Puts a persisted queue state back into live queues, RE-RUNNING THE ADMISSION GATE against the
    /// content's CURRENT hash.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THE RELOAD IS THE ONE PLACE A GATED ITEM CAN QUIETLY BECOME AN UNGATED ONE, WHICH IS WHY
    /// THIS TYPE EXISTS RATHER THAN A `queues.LoadFrom(store)` METHOD. *** An entry was admitted on
    /// evidence about a particular state of the content. While the coordinator was dead, that content
    /// may have moved on — an agent edits its block, the file changes, and the persisted "preflight
    /// passed, compiled clean" now describes something that no longer exists. Restoring it verbatim
    /// would put work into a wave whose evidence is stale, and the whole hash comparison in
    /// <see cref="AdmissionController"/> would have been defeated by the simple act of restarting.
    /// </para>
    /// <para>
    /// So every entry is rebuilt with the CURRENT hash of its content, and passed back through
    /// <see cref="AdmissionController.Admit"/> with the evidence that was persisted alongside it. The
    /// gate is not re-implemented here; it is re-run. An entry whose content moved on refuses as
    /// <see cref="AdmissionFindingKind.EvidenceStale"/>, exactly as it would have on first submission.
    /// </para>
    /// <para>
    /// *** A HASH THE CALLER CANNOT SUPPLY IS A REFUSAL, NOT A PASS. *** The hash delegate returns null
    /// when it cannot determine the current hash — the artifact is gone, the tool failed, the path
    /// moved. That produces an object with no hash, which the gate already refuses. Nothing here falls
    /// back to the persisted hash: doing so would answer "has this changed?" with the value being
    /// checked.
    /// </para>
    /// <para>
    /// *** THE QUEUE IS RE-DERIVED AND CROSS-CHECKED, NOT TRUSTED. *** The persisted entry records
    /// which queue it was in; re-admission computes one from the change classes. They must agree. A
    /// disagreement means the classification changed under the coordinator, and it is a REJECTION with
    /// both values named — a restored entry that quietly moved from the deferred queue to the run
    /// queue would put a STOP-class change through a wave boundary.
    /// </para>
    /// <para>
    /// WHAT IS PRESERVED rather than recomputed: the arrival order, the enqueue time, the number of
    /// wave boundaries already waited, and the REASON — which for an excised entry is D32 step 5's
    /// record, and replacing it with a fresh admission summary would erase the only account of why
    /// that submission is where it is.
    /// </para>
    /// </remarks>
    public static class QueueRehydrator
    {
        /// <summary>
        /// Re-gate a restore result and rebuild the live queues.
        /// </summary>
        /// <param name="restored">What <see cref="WaveQueueStore.Read"/> found.</param>
        /// <param name="currentHashOf">
        /// The CURRENT content hash of an object, by name — `converter ir-hash` or equivalent. Return
        /// null when it cannot be determined; that is a refusal, never a pass.
        /// </param>
        public static QueueRehydration Rehydrate(QueueRestoreResult restored, Func<string, string?> currentHashOf)
        {
            if (restored == null)
            {
                throw new ArgumentNullException(nameof(restored));
            }

            if (currentHashOf == null)
            {
                throw new ArgumentNullException(
                    nameof(currentHashOf),
                    "Rehydration needs a way to ask what the content's hash is NOW. Without it the only " +
                    "available answer is the persisted hash, and checking a value against itself is not " +
                    "a check.");
            }

            if (restored.State != QueueRestoreState.Restored)
            {
                return new QueueRehydration(
                    restored.State,
                    null,
                    new QueuedSubmission[0],
                    new RehydrationRejection[0],
                    restored.State == QueueRestoreState.NoStateFileFound
                        ? "No state file, so there is nothing to re-gate — and NOTHING TO RESUME EITHER. " +
                          "This is not an empty queue: it is expected on a first start and is lost " +
                          "admitted work on any other. Declare which through " +
                          "WaveQueueStore.AcceptNoPersistedState."
                        : "The state file could not be read (" + restored.Problem + "), so nothing was " +
                          "restored. A queue that cannot be read is not a queue that is empty.");
            }

            var queues = new WaveQueues();
            var readmitted = new List<QueuedSubmission>();
            var rejected = new List<RehydrationRejection>();

            foreach (var entry in restored.Entries.OrderBy(e => e.Sequence))
            {
                var rebuilt = new Submission(
                    entry.Submission.Id,
                    entry.Submission.Agent,
                    entry.Submission.Objects.Select(o => new ChangedObject(
                        o.Name,
                        o.Kind,
                        o.ChangeClass,
                        o.DependsOn,
                        currentHashOf(o.Name))));

                var decision = AdmissionController.Admit(rebuilt, entry.Evidence);

                if (!decision.Admitted)
                {
                    rejected.Add(new RehydrationRejection(
                        entry,
                        decision,
                        "It no longer passes the admission gate. It was admitted once, but the gate is " +
                        "re-run against the content's CURRENT hash, and restoring it regardless would " +
                        "put work into a wave on evidence about something that no longer exists."));
                    continue;
                }

                var queueProblem = CheckQueue(entry, decision.Queue);
                if (queueProblem != null)
                {
                    rejected.Add(new RehydrationRejection(entry, decision, queueProblem));
                    continue;
                }

                readmitted.Add(queues.Restore(
                    decision,
                    entry.Queue,
                    entry.Reason,
                    entry.EnqueuedUtc,
                    entry.Sequence,
                    entry.WaveBoundariesWaited,
                    entry.Excised));
            }

            var summary = rejected.Count == 0
                ? readmitted.Count + " entry/ies re-admitted against their current content hashes; the " +
                  "queues are as they were, including arrival order, wait counts and excision reasons."
                : rejected.Count + " of " + restored.Entries.Count + " restored entry/ies FAILED the " +
                  "re-gate and are NOT in the queues. Their agents must re-submit; this is reported " +
                  "rather than silently dropped, which is the whole point of persisting them.";

            return new QueueRehydration(QueueRestoreState.Restored, queues, readmitted, rejected, summary);
        }

        /// <summary>
        /// The queue cross-check, and *** IT IS ASYMMETRIC ON PURPOSE — AN EQUALITY HERE IS WRONG. ***
        /// </summary>
        /// <remarks>
        /// <para>
        /// Re-admission derives a queue from the change classes. For most entries that must equal the
        /// persisted queue. But D32 step 5 EXCISES a RUN-class submission INTO the deferred queue, and
        /// its change classes never said so — routing it again correctly answers "run queue" for
        /// something that legitimately belongs in the deferred one. An equality check rejects every
        /// excised entry on reload, which is how this was found.
        /// </para>
        /// <para>
        /// So the two directions are not the same risk and are not treated the same way:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///     Persisted DEFERRED, now routes RUN — permitted ONLY when the entry is flagged excised.
        ///     Worst case it waits for a boundary it did not have to wait for.
        ///   </description></item>
        ///   <item><description>
        ///     *** Persisted RUN, now routes DEFERRED — ALWAYS A REJECTION. *** That is a STOP-class
        ///     change about to go through a wave boundary and stop the CPU mid-testing.
        ///   </description></item>
        /// </list>
        /// </remarks>
        private static string? CheckQueue(PersistedQueueEntry entry, DownloadQueue routed)
        {
            if (entry.Excised && entry.Queue != DownloadQueue.DeferredQueue)
            {
                return "It is flagged as excised (D32 step 5) but was persisted in the " + entry.Queue +
                       ". An excision moves a submission INTO the deferred queue; an excised entry " +
                       "anywhere else is a state nothing in this design produces.";
            }

            if (routed == entry.Queue)
            {
                return null;
            }

            if (entry.Queue == DownloadQueue.DeferredQueue &&
                routed == DownloadQueue.RunQueue &&
                entry.Excised)
            {
                // The excision case. Its change classes say RUN and it sits in the deferred queue by
                // decision, which is exactly what an excision is.
                return null;
            }

            return "It was persisted in the " + entry.Queue + " and now routes to the " + routed +
                   (entry.Excised ? " (excised)" : string.Empty) +
                   ". The classification changed under the coordinator" +
                   (entry.Queue == DownloadQueue.RunQueue
                       ? ", and this is the dangerous direction: restoring it would put a STOP-class " +
                         "change through a wave boundary and stop the CPU mid-testing."
                       : ", and only an EXCISED entry may sit in the deferred queue while routing to " +
                         "the run queue.");
        }
    }
}
