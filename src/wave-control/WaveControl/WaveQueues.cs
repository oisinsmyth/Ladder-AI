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
            DownloadQueue queue,
            string reason,
            DateTimeOffset enqueuedUtc,
            int sequence)
        {
            Submission = submission;
            Queue = queue;
            Reason = reason;
            EnqueuedUtc = enqueuedUtc;
            Sequence = sequence;
        }

        /// <summary>The submission.</summary>
        public Submission Submission { get; }

        /// <summary>Which queue it is in now.</summary>
        public DownloadQueue Queue { get; }

        /// <summary>Why it is in that queue — the routing argument, or the excision reason.</summary>
        public string Reason { get; }

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
    /// NOT PERSISTED. These queues live in the coordinator's process. X-C's marker is what survives a
    /// crash, and it records the wave, not the queues — so a coordinator that dies loses the deferred
    /// queue and its submitting agents must re-submit. That is a named gap, not an oversight: nothing
    /// in the spec asks for queue persistence, and inventing a format for it here would put a second
    /// unreviewed durability mechanism next to the one X-C specified.
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
                decision.Queue,
                decision.Summary,
                enqueuedUtc ?? DateTimeOffset.UtcNow,
                _sequence++);

            _entries.Add(entry);
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
                DownloadQueue.DeferredQueue,
                "EXCISED from the wave set (D32 step 5): " + stated,
                existing.EnqueuedUtc,
                existing.Sequence)
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
