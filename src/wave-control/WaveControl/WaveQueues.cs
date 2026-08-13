using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>One admitted submission sitting in one of D23's two queues.</summary>
    public sealed class QueuedSubmission
    {
        internal QueuedSubmission(
            Submission submission,
            IReadOnlyList<AdmissionEvidence> evidence,
            DownloadQueue queue,
            string reason,
            DateTimeOffset enqueuedUtc,
            int sequence,
            bool excised)
        {
            Submission = submission;
            Evidence = evidence;
            Queue = queue;
            Reason = reason;
            EnqueuedUtc = enqueuedUtc;
            Sequence = sequence;
            Excised = excised;
        }

        /// <summary>The submission.</summary>
        public Submission Submission { get; }

        /// <summary>
        /// The admission evidence this entry was admitted on. It travels WITH the entry, including
        /// across a save and reload, because an entry restored without it would be an item that was
        /// gated once and silently reloaded as an item nobody gated.
        /// </summary>
        public IReadOnlyList<AdmissionEvidence> Evidence { get; }

        /// <summary>Which queue it is in now.</summary>
        public DownloadQueue Queue { get; }

        /// <summary>Why it is in that queue — the routing argument, or the excision reason.</summary>
        public string Reason { get; }

        /// <summary>
        /// TRUE when this entry is in the deferred queue because it was EXCISED (D32 step 5), not
        /// because its change class put it there.
        /// </summary>
        /// <remarks>
        /// *** THIS FLAG IS WHY THE QUEUE CROSS-CHECK ON RELOAD HAS TO BE ASYMMETRIC. *** An excised
        /// entry is RUN-class sitting in the DEFERRED queue by decision, so re-deriving its queue from
        /// its change classes gives the "wrong" answer legitimately. A boolean carries that, where
        /// sniffing the reason text for the word EXCISED would be a check one edit away from silently
        /// passing everything. See <see cref="QueueRehydrator"/>.
        /// </remarks>
        public bool Excised { get; }

        /// <summary>When it entered the queues.</summary>
        public DateTimeOffset EnqueuedUtc { get; }

        /// <summary>
        /// Arrival order. Ordering by this rather than by timestamp keeps the queues deterministic when
        /// two submissions arrive inside one clock tick, which is otherwise a source of runs that
        /// cannot be compared.
        /// </summary>
        public int Sequence { get; }

        /// <summary>
        /// How many wave boundaries have passed since it was enqueued. Visibility only — nothing acts on
        /// it. See <see cref="WaveQueues.NoteWaveBoundary"/> for why it is recorded and why no threshold
        /// exists.
        /// </summary>
        public int WaveBoundariesWaited { get; internal set; }

        /// <summary>The objects it carries.</summary>
        public IReadOnlyList<ChangedObject> Objects => Submission.Objects;

        /// <inheritdoc />
        public override string ToString() =>
            Submission.Id + " in " + Queue + " (" + Objects.Count + " object(s), " +
            WaveBoundariesWaited + " wave boundary/ies waited): " + Reason;
    }

    /// <summary>
    /// D23's TWO QUEUES, held together because the interesting operations are the ones that MOVE
    /// something between them: an excision (D32 step 5) and a drain (D24).
    /// </summary>
    /// <remarks>
    /// <para>
    /// RUN-class submissions flow through normal wave boundaries. STOP-class submissions do not flow at
    /// all — they accumulate here until <see cref="DrainPolicy"/> says no test can make progress, and
    /// only then is the disruptive boundary spent (D24, R8).
    /// </para>
    /// <para>
    /// *** DRAINING REQUIRES A DECISION OBJECT, NOT A FLAG. *** <see cref="Drain"/> takes a
    /// <see cref="DrainDecision"/> and refuses one that does not say drain. R8 is explicit that the
    /// disruptive mode must be "a SEPARATE, EXPLICITLY DECLARED DISRUPTIVE MODE — never a flag on the
    /// wave-boundary path, and never a fallback reached by retry", and permitted "ONLY when the
    /// deferred queue is being drained". A boolean argument would satisfy the letter of that and none
    /// of the intent; a decision that had to be computed, and that carries its own reasoning into the
    /// log, satisfies both.
    /// </para>
    /// <para>
    /// PERSISTED SEPARATELY, BY <see cref="WaveQueueStore"/>. This type holds the queues in memory and
    /// knows nothing about files; the store saves and reloads them, and <see cref="QueueRehydrator"/>
    /// re-runs the admission gate on everything it reads back. The separation is deliberate — the
    /// queue rules and the durability rules fail in different ways and are worth testing apart.
    /// </para>
    /// </remarks>
    public sealed class WaveQueues
    {
        private readonly List<QueuedSubmission> _entries = new List<QueuedSubmission>();
        private int _sequence;

        /// <summary>Submissions waiting to go out at a normal wave boundary.</summary>
        public IReadOnlyList<QueuedSubmission> RunQueue =>
            _entries.Where(e => e.Queue == DownloadQueue.RunQueue).OrderBy(e => e.Sequence).ToArray();

        /// <summary>Submissions accumulated for the next drain (D24).</summary>
        public IReadOnlyList<QueuedSubmission> DeferredQueue =>
            _entries.Where(e => e.Queue == DownloadQueue.DeferredQueue).OrderBy(e => e.Sequence).ToArray();

        /// <summary>Everything in either queue.</summary>
        public IReadOnlyList<QueuedSubmission> All => _entries.OrderBy(e => e.Sequence).ToArray();

        /// <summary>
        /// Enqueue an ADMITTED submission into the queue its admission decision chose.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The decision is not an admission, or a submission with that id is already queued. Both are
        /// caller defects rather than states to report: a refused submission that reached a queue means
        /// the gate was bypassed, and two live entries for one id make every later lookup ambiguous.
        /// </exception>
        public QueuedSubmission Enqueue(AdmissionDecision decision, DateTimeOffset? enqueuedUtc = null)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            if (!decision.Admitted)
            {
                throw new InvalidOperationException(
                    "Only an ADMITTED submission may be queued; this one is " + decision.Outcome +
                    ". Queuing anything else would put work into a wave that never passed loop 2, which " +
                    "is the single thing admission control exists to prevent (§1.2). Decision: " +
                    decision.ToLogLine());
            }

            if (_entries.Any(e => string.Equals(e.Submission.Id, decision.Submission.Id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "A submission with id '" + decision.Submission.Id + "' is already queued. Two live " +
                    "entries for one id make every excision, completion and drain-log line ambiguous.");
            }

            var entry = new QueuedSubmission(
                decision.Submission,
                decision.Evidence,
                decision.Queue,
                decision.Summary,
                enqueuedUtc ?? DateTimeOffset.UtcNow,
                _sequence++,
                excised: false);

            _entries.Add(entry);
            return entry;
        }

        /// <summary>
        /// RESTORE an entry read back from <see cref="WaveQueueStore"/>, preserving the facts a
        /// re-admission cannot recompute: when it arrived, its position in the arrival order, how many
        /// wave boundaries it has already waited, and the REASON — which for an excised entry is D32
        /// step 5's record and is the one thing that must not be replaced by a fresh admission summary.
        /// </summary>
        /// <remarks>
        /// Internal on purpose: the ONLY route to it is <see cref="QueueRehydration"/>, which re-runs
        /// the admission gate against the content's CURRENT hash first. A public restore would be a
        /// door into the queues that bypasses loop 2, which is the one thing admission control exists
        /// to prevent.
        /// </remarks>
        internal QueuedSubmission Restore(
            AdmissionDecision decision,
            DownloadQueue queue,
            string reason,
            DateTimeOffset enqueuedUtc,
            int sequence,
            int waveBoundariesWaited,
            bool excised)
        {
            if (!decision.Admitted)
            {
                throw new InvalidOperationException(
                    "Only an ADMITTED decision may be restored; this one is " + decision.Outcome + ".");
            }

            var entry = new QueuedSubmission(
                decision.Submission,
                decision.Evidence,
                queue,
                reason,
                enqueuedUtc,
                sequence,
                excised)
            {
                WaveBoundariesWaited = waveBoundariesWaited,
            };

            _entries.Add(entry);
            _sequence = Math.Max(_sequence, sequence + 1);
            return entry;
        }

        /// <summary>
        /// D32 step 5 — EXCISE a submission from the wave set and route it to the deferred queue, with
        /// the reason recorded. Used when a configuration is attributed to it, and by D34's residual
        /// (a block whose inert state cannot be established is a wave-blocking condition, treated the
        /// same way).
        /// </summary>
        /// <exception cref="InvalidOperationException">No such submission is in the run queue.</exception>
        /// <exception cref="ArgumentException">No reason was given.</exception>
        public QueuedSubmission Excise(string submissionId, string reason)
        {
            var stated = (reason ?? string.Empty).Trim();
            if (stated.Length == 0)
            {
                throw new ArgumentException(
                    "An excision must record WHY. D32 caveat 1 turns on exactly this distinction — 'no " +
                    "block is responsible' and 'the locator failed to find one' take the same branch and " +
                    "call for opposite fixes — and an unexplained excision is indistinguishable from both.",
                    nameof(reason));
            }

            var existing = _entries.FirstOrDefault(e =>
                e.Queue == DownloadQueue.RunQueue &&
                string.Equals(e.Submission.Id, submissionId, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                throw new InvalidOperationException(
                    "No submission with id '" + submissionId + "' is in the run queue, so there is " +
                    "nothing to excise. Excising nothing and reporting success would put an excision in " +
                    "the log that never happened.");
            }

            var moved = new QueuedSubmission(
                existing.Submission,
                existing.Evidence,
                DownloadQueue.DeferredQueue,
                "EXCISED from the wave set (D32 step 5): " + stated,
                existing.EnqueuedUtc,
                existing.Sequence,
                excised: true)
            {
                WaveBoundariesWaited = existing.WaveBoundariesWaited,
            };

            _entries.Remove(existing);
            _entries.Add(moved);
            return moved;
        }

        /// <summary>
        /// The submission's wave ran and its results were distributed; remove it from the run queue.
        /// </summary>
        /// <exception cref="InvalidOperationException">No such submission is in the run queue.</exception>
        public void Complete(string submissionId)
        {
            var existing = _entries.FirstOrDefault(e =>
                e.Queue == DownloadQueue.RunQueue &&
                string.Equals(e.Submission.Id, submissionId, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                throw new InvalidOperationException(
                    "No submission with id '" + submissionId + "' is in the run queue. Completing one " +
                    "that is not there would silently succeed while the real entry stayed queued forever.");
            }

            _entries.Remove(existing);
        }

        /// <summary>
        /// Record that a wave boundary passed. Increments the wait counter on every DEFERRED submission.
        /// </summary>
        /// <remarks>
        /// *** VISIBILITY ONLY — NOTHING IN THIS LIBRARY ACTS ON THE COUNT, AND THAT IS DELIBERATE. ***
        /// D24 drains when no test can make progress, and that condition alone permits a deferred
        /// submission to wait indefinitely while RUN-class work keeps arriving. §1.4 accepts the delay
        /// ("that delay is unavoidable in any design and is not treated as a problem") but says nothing
        /// about an unbounded one. Choosing a threshold would be adding a policy the spec does not have,
        /// so the count is recorded and reported, and the decision is left where it belongs.
        /// </remarks>
        public void NoteWaveBoundary()
        {
            foreach (var entry in _entries.Where(e => e.Queue == DownloadQueue.DeferredQueue))
            {
                entry.WaveBoundariesWaited++;
            }
        }

        /// <summary>
        /// Drain the deferred queue — the ONLY place R8's disruptive mode is permitted (D24).
        /// </summary>
        /// <param name="decision">
        /// A decision computed by <see cref="DrainPolicy"/> whose outcome is
        /// <see cref="DrainOutcome.Drain"/>. Anything else is refused.
        /// </param>
        /// <returns>The drained submissions, in arrival order. The deferred queue is then empty.</returns>
        /// <exception cref="InvalidOperationException">
        /// The decision does not authorise a drain, or the deferred queue is empty.
        /// </exception>
        public IReadOnlyList<QueuedSubmission> Drain(DrainDecision decision)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            if (decision.Outcome != DrainOutcome.Drain || !decision.DisruptiveModeAuthorised)
            {
                throw new InvalidOperationException(
                    "Draining requires a decision whose outcome is Drain; this one is " + decision.Outcome +
                    ". The drain is the only place R8 permits the disruptive mode, and R8 requires that " +
                    "mode to be explicitly declared rather than reached by a flag or a retry. Decision: " +
                    decision.ToLogLine());
            }

            var deferred = DeferredQueue;
            if (deferred.Count == 0)
            {
                throw new InvalidOperationException(
                    "The deferred queue is empty, so there is nothing to drain. A drain that moved " +
                    "nothing and reported success would spend a CPU stop for no change (FI-44: empty is " +
                    "not clean).");
            }

            foreach (var entry in deferred)
            {
                _entries.Remove(entry);
            }

            return deferred;
        }
    }
}
