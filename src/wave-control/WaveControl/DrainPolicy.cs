using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>D24's four answers. The zero value is not one of the three that authorise anything.</summary>
    public enum DrainOutcome
    {
        /// <summary>
        /// Nothing was evaluated. The zero value, so a default-initialised or dropped outcome never
        /// reads as an authorisation to stop the CPU.
        /// </summary>
        NotEvaluated = 0,

        /// <summary>
        /// The deferred queue is empty and testing can proceed. The ordinary state, and reported as its
        /// own answer rather than as "do not drain": "there is nothing waiting" and "there is something
        /// waiting but tests are still progressing" are different facts about the run.
        /// </summary>
        NothingToDrain = 1,

        /// <summary>
        /// Something is deferred, but at least one admitted submission can still make progress — and
        /// this decision NAMES IT. D24 drains when no test can make progress, so a witness that can is
        /// the whole reason not to.
        /// </summary>
        DoNotDrain = 2,

        /// <summary>
        /// *** NO TEST CAN MAKE PROGRESS AND THERE IS NOTHING DEFERRED TO DRAIN. *** A drain would fix
        /// nothing because the deferred queue is empty, so this is a wave-blocking condition that needs
        /// a different treatment — the excise-and-continue path (D32 step 5, D34's residual) or a
        /// person. Reported separately because it is the state most easily mistaken for
        /// <see cref="NothingToDrain"/>: both have an empty deferred queue, and only one of them is
        /// healthy.
        /// </summary>
        StalledWithNothingToDrain = 3,

        /// <summary>
        /// D24's condition is met: the deferred queue is non-empty and no test can make progress. This
        /// is the only outcome that authorises R8's disruptive mode.
        /// </summary>
        Drain = 4,
    }

    /// <summary>Why one admitted submission cannot make progress.</summary>
    public enum ProgressBlockKind
    {
        /// <summary>Not a block.</summary>
        None = 0,

        /// <summary>
        /// It depends on an object that is sitting in the DEFERRED queue, so it cannot be tested until
        /// the drain lands. This is D24's "the deferred queue holds other dependencies too, so in
        /// practice the two conditions converge", made concrete.
        /// </summary>
        BlockedOnADeferredObject = 1,

        /// <summary>
        /// It depends on an object that is in NO queue and not on the device. A drain will not fix this
        /// one, and conflating it with the case above would send a fix at the queue when the fault is in
        /// the change set or the baseline.
        /// </summary>
        DependencyPresentNowhere = 2,
    }

    /// <summary>One reason a submission cannot progress, naming the object and the dependency.</summary>
    public sealed class ProgressBlock
    {
        internal ProgressBlock(ProgressBlockKind kind, string submissionId, string objectName, string dependencyName, string detail)
        {
            Kind = kind;
            SubmissionId = submissionId;
            ObjectName = objectName;
            DependencyName = dependencyName;
            Detail = detail;
        }

        /// <summary>Which kind of block.</summary>
        public ProgressBlockKind Kind { get; }

        /// <summary>The submission that cannot progress.</summary>
        public string SubmissionId { get; }

        /// <summary>The object in it that is blocked.</summary>
        public string ObjectName { get; }

        /// <summary>What it is blocked on.</summary>
        public string DependencyName { get; }

        /// <summary>What is wrong.</summary>
        public string Detail { get; }

        /// <inheritdoc />
        public override string ToString() =>
            Kind + " [" + SubmissionId + "/" + ObjectName + " -> " + DependencyName + "]: " + Detail;
    }

    /// <summary>
    /// D24's decision, with everything R8 requires to be logged about a deliberate CPU stop: what was in
    /// the queue, and why.
    /// </summary>
    public sealed class DrainDecision
    {
        private DrainDecision(
            DrainOutcome outcome,
            QueuedSubmission? witness,
            IReadOnlyList<ProgressBlock> blocks,
            IReadOnlyList<QueuedSubmission> deferredContents,
            int runQueueDepth,
            string reason)
        {
            Outcome = outcome;
            Witness = witness;
            Blocks = blocks;
            DeferredContents = deferredContents;
            RunQueueDepth = runQueueDepth;
            Reason = reason;
        }

        /// <summary>The answer.</summary>
        public DrainOutcome Outcome { get; }

        /// <summary>
        /// For <see cref="DrainOutcome.DoNotDrain"/> ONLY: the submission that CAN make progress. Never
        /// null for that outcome, and never non-null for any other — the whole point is that a decision
        /// not to drain rests on positive evidence of progress rather than on nothing having been found.
        /// </summary>
        public QueuedSubmission? Witness { get; }

        /// <summary>Every reason every blocked submission cannot progress.</summary>
        public IReadOnlyList<ProgressBlock> Blocks { get; }

        /// <summary>What is sitting in the deferred queue — R8's "what was in the queue".</summary>
        public IReadOnlyList<QueuedSubmission> DeferredContents { get; }

        /// <summary>How many submissions were in the run queue when the decision was made.</summary>
        public int RunQueueDepth { get; }

        /// <summary>The reason, in a sentence.</summary>
        public string Reason { get; }

        /// <summary>
        /// TRUE only for <see cref="DrainOutcome.Drain"/>. R8's sanctioned exception is permitted ONLY
        /// when the deferred queue is being drained, and this is the flag that says so.
        /// </summary>
        public bool DisruptiveModeAuthorised => Outcome == DrainOutcome.Drain;

        /// <summary>
        /// The log line. For a drain it carries R8's required record — a deliberate CPU stop, what was
        /// in the queue, and why.
        /// </summary>
        public string ToLogLine()
        {
            var contents = DeferredContents.Count == 0
                ? "<deferred queue empty>"
                : string.Join(", ", DeferredContents
                    .Select(d => d.Submission.Id + "(" + d.Objects.Count + " obj, waited " + d.WaveBoundariesWaited + ")")
                    .ToArray());

            switch (Outcome)
            {
                case DrainOutcome.Drain:
                    return "DRAIN AUTHORISED — DELIBERATE CPU STOP (D24/R8). Deferred queue: " + contents +
                           ". Run queue depth " + RunQueueDepth + ", " + Blocks.Count +
                           " progress block(s). :: " + Reason;

                case DrainOutcome.DoNotDrain:
                    return "DO NOT DRAIN — progress is possible: " + (Witness == null ? "<none>" : Witness.Submission.Id) +
                           " can run. Deferred queue: " + contents + ". :: " + Reason;

                case DrainOutcome.StalledWithNothingToDrain:
                    return "STALLED WITH NOTHING TO DRAIN — no test can make progress and the deferred " +
                           "queue is empty, so a drain would change nothing. " + Blocks.Count +
                           " progress block(s). :: " + Reason;

                case DrainOutcome.NothingToDrain:
                    return "NOTHING TO DRAIN — the deferred queue is empty and testing is progressing. :: " + Reason;

                default:
                    return "DRAIN NOT EVALUATED :: " + Reason;
            }
        }

        /// <summary>The log line plus every block, one per line.</summary>
        public string Describe()
        {
            if (Blocks.Count == 0)
            {
                return ToLogLine();
            }

            return ToLogLine() + Environment.NewLine +
                   string.Join(Environment.NewLine, Blocks.Select(b => "  - " + b).ToArray());
        }

        /// <inheritdoc />
        public override string ToString() => ToLogLine();

        internal static DrainDecision NothingToDrain(int runQueueDepth, string reason) =>
            new DrainDecision(DrainOutcome.NothingToDrain, null, new ProgressBlock[0], new QueuedSubmission[0], runQueueDepth, reason);

        internal static DrainDecision Stalled(IReadOnlyList<ProgressBlock> blocks, int runQueueDepth, string reason) =>
            new DrainDecision(DrainOutcome.StalledWithNothingToDrain, null, blocks, new QueuedSubmission[0], runQueueDepth, reason);

        internal static DrainDecision DoNotDrain(
            QueuedSubmission witness,
            IReadOnlyList<ProgressBlock> blocks,
            IReadOnlyList<QueuedSubmission> deferredContents,
            int runQueueDepth,
            string reason)
        {
            if (witness == null)
            {
                throw new InvalidOperationException(
                    "A decision not to drain must NAME the submission that can make progress. D24's " +
                    "condition is 'no test can make progress', so declining to drain is a claim that one " +
                    "can — and a claim with no witness is an absence of findings reported as a result.");
            }

            return new DrainDecision(DrainOutcome.DoNotDrain, witness, blocks, deferredContents, runQueueDepth, reason);
        }

        internal static DrainDecision Drain(
            IReadOnlyList<ProgressBlock> blocks,
            IReadOnlyList<QueuedSubmission> deferredContents,
            int runQueueDepth,
            string reason)
        {
            if (deferredContents.Count == 0)
            {
                throw new InvalidOperationException(
                    "A drain must have something to drain. Authorising a deliberate CPU stop over an " +
                    "empty queue spends the disruption for no change.");
            }

            return new DrainDecision(DrainOutcome.Drain, null, blocks, deferredContents, runQueueDepth, reason);
        }
    }

    /// <summary>
    /// D24 — *** THE DEFERRED QUEUE DRAINS WHEN NO TEST CAN MAKE PROGRESS. *** This computes that
    /// condition, and it is the only thing that authorises R8's disruptive mode.
    /// </summary>
    /// <remarks>
    /// <para>
    /// D24 chose that condition over "all queued tests complete" because the deferred queue holds other
    /// dependencies too, so in practice the two converge. *** THAT SENTENCE IS THE IMPLEMENTATION, NOT
    /// AN ASIDE: *** a submission is blocked not only when it is itself deferred, but when something it
    /// DEPENDS ON is deferred. So "can this submission make progress?" is a question about the
    /// dependency graph across both queues and the deployed baseline, not about the run queue alone.
    /// </para>
    /// <para>
    /// *** DECLINING TO DRAIN REQUIRES A WITNESS. *** A submission that can run is named in the
    /// decision, and <see cref="DrainDecision.DoNotDrain"/> cannot be built without one. The failure
    /// this prevents is the project's most repeated: an absence of findings read as a positive result.
    /// A policy that returned "do not drain" because it found no reason to would be exactly that, and
    /// would look identical to one that never examined anything.
    /// </para>
    /// <para>
    /// *** WHAT AUTHORISATION MEANS HERE, PRECISELY. *** This decides that the disruptive boundary is
    /// DUE. It does not perform a download, does not choose a download option, and says nothing about
    /// what happens if that download raises an unhandled configuration — that is D32's escalation
    /// ladder, which is NOT built and is blocked on assumption A6/G4: what a throw from the download
    /// delegate leaves the CPU in is UNMEASURED, and its failure mode is a half-loaded CPU. Nothing in
    /// this file assumes an answer to it.
    /// </para>
    /// </remarks>
    public static class DrainPolicy
    {
        /// <summary>Decide whether the deferred queue should be drained.</summary>
        /// <param name="queues">The two queues.</param>
        /// <param name="deployed">What is already on the device, so a dependency can be satisfied without a download.</param>
        public static DrainDecision Decide(WaveQueues queues, DeployedProgram deployed)
        {
            if (queues == null)
            {
                throw new ArgumentNullException(nameof(queues));
            }

            if (deployed == null)
            {
                throw new ArgumentNullException(
                    nameof(deployed),
                    "The drain decision needs a deployed-program baseline: without one, every dependency " +
                    "looks unsatisfied, every submission looks blocked, and the policy would authorise a " +
                    "CPU stop on the strength of not knowing anything.");
            }

            var runQueue = queues.RunQueue;
            var deferred = queues.DeferredQueue;

            var deferredObjects = new HashSet<string>(
                deferred.SelectMany(d => d.Objects).Select(o => o.Name).Where(n => n.Length > 0),
                ChangedObject.NameComparer);

            var runQueueObjects = new HashSet<string>(
                runQueue.SelectMany(r => r.Objects).Select(o => o.Name).Where(n => n.Length > 0),
                ChangedObject.NameComparer);

            var blocks = new List<ProgressBlock>();
            QueuedSubmission? witness = null;

            foreach (var candidate in runQueue)
            {
                var candidateBlocks = BlocksFor(candidate, deferredObjects, runQueueObjects, deployed);
                blocks.AddRange(candidateBlocks);

                if (candidateBlocks.Count == 0 && witness == null)
                {
                    witness = candidate;
                }
            }

            var canProgress = witness != null;

            if (deferred.Count == 0)
            {
                if (canProgress)
                {
                    return DrainDecision.NothingToDrain(
                        runQueue.Count,
                        "The deferred queue is empty and " + runQueue.Count + " submission(s) are queued, " +
                        "at least one of which can run.");
                }

                return DrainDecision.Stalled(
                    blocks,
                    runQueue.Count,
                    runQueue.Count == 0
                        ? "Nothing is queued at all: no test can make progress because there are no tests. " +
                          "A drain would change nothing — the deferred queue is empty too. This is an idle " +
                          "coordinator or a submission pipeline that has stopped feeding it, and it is " +
                          "reported rather than read as a healthy quiet period."
                        : runQueue.Count + " submission(s) are queued and none can make progress, but the " +
                          "deferred queue is empty so a drain would change nothing. The blocks below name " +
                          "dependencies that exist nowhere; the treatment is D32 step 5's excision or a " +
                          "person, not a disruptive download.");
            }

            if (canProgress)
            {
                return DrainDecision.DoNotDrain(
                    witness!,
                    blocks,
                    deferred,
                    runQueue.Count,
                    "D24 drains when NO test can make progress. '" + witness!.Submission.Id +
                    "' can: every object it depends on is deployed or in the run queue. The " +
                    deferred.Count + " deferred submission(s) wait for a boundary where nothing can run.");
            }

            return DrainDecision.Drain(
                blocks,
                deferred,
                runQueue.Count,
                "No test can make progress and " + deferred.Count + " submission(s) are deferred, so D24's " +
                "condition is met and the disruptive boundary is due (R8's mode is permitted ONLY here). " +
                "The download itself, and what to do if it raises an unhandled configuration, is D32's " +
                "ladder and is not decided by this policy.");
        }

        private static List<ProgressBlock> BlocksFor(
            QueuedSubmission candidate,
            HashSet<string> deferredObjects,
            HashSet<string> runQueueObjects,
            DeployedProgram deployed)
        {
            var blocks = new List<ProgressBlock>();

            foreach (var o in candidate.Objects)
            {
                foreach (var dependency in o.DependsOn)
                {
                    if (deferredObjects.Contains(dependency))
                    {
                        blocks.Add(new ProgressBlock(
                            ProgressBlockKind.BlockedOnADeferredObject,
                            candidate.Submission.Id,
                            o.Name,
                            dependency,
                            "'" + dependency + "' is in the deferred queue, so it will not be on the " +
                            "device until the drain lands. Testing '" + o.Name + "' before then would " +
                            "test against a program that does not contain what it depends on."));
                        continue;
                    }

                    if (runQueueObjects.Contains(dependency) || deployed.Contains(dependency))
                    {
                        continue;
                    }

                    blocks.Add(new ProgressBlock(
                        ProgressBlockKind.DependencyPresentNowhere,
                        candidate.Submission.Id,
                        o.Name,
                        dependency,
                        "'" + dependency + "' is in neither queue and not in the deployed baseline (" +
                        deployed + "). A drain will NOT fix this: the change set or the baseline is wrong."));
                }
            }

            return blocks;
        }
    }
}
