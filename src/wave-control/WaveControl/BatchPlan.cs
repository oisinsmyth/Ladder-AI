using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>The outcome of a batching attempt. The zero value is the one that is not a plan.</summary>
    public enum BatchPlanOutcome
    {
        /// <summary>
        /// *** NOTHING WAS BATCHED, BECAUSE THERE WAS NOTHING TO BATCH. *** Its own outcome, never a
        /// plan with zero batches reported as a success. An empty change set means no download is
        /// needed, which is a legitimate state — and it is also exactly what a broken change tracker
        /// produces, which is why the two must not share a verdict (FI-44; `compile-all` claimed a pass
        /// after examining nothing).
        /// </summary>
        NothingToBatch = 0,

        /// <summary>The change set cannot be batched. Every reason is listed.</summary>
        Refused = 1,

        /// <summary>A plan exists: batches that partition the change set, each closed and within the limit.</summary>
        Planned = 2,
    }

    /// <summary>Why a change set could not be batched.</summary>
    public enum BatchRefusalReason
    {
        /// <summary>Not a refusal.</summary>
        None = 0,

        /// <summary>An object in the change set has no name.</summary>
        UnnamedObject = 1,

        /// <summary>Two objects in the change set share a name.</summary>
        DuplicateObjectName = 2,

        /// <summary>
        /// A dependency is neither in the change set nor on the device, so no batch can ever satisfy it.
        /// </summary>
        UnresolvedDependency = 3,

        /// <summary>
        /// *** A GROUP OF OBJECTS THAT MUST LAND TOGETHER IS LARGER THAN THE LIMIT. *** Not a packing
        /// failure — an unpackable set. Because batches partition the change set, closure forces every
        /// weakly-connected component of the dependency graph into ONE batch, so a component of
        /// twenty-one objects cannot be downloaded consistently at all. The shape that produces it is
        /// DB-1's blast radius: a modified UDT is RUN (Init) on EVERY DB built on it, so one UDT with
        /// twenty dependent DBs is a twenty-one-object component from a single edit.
        /// </summary>
        DependencyGroupExceedsObjectLimit = 4,

        /// <summary>
        /// A STOP-class or unclassified object was handed to the WAVE-BOUNDARY planner. STOP-class
        /// changes do not flow through a wave boundary (D23); they accumulate for the drain. This
        /// refusal is the backstop for a routing bug that would otherwise stop the CPU mid-wave.
        /// </summary>
        StopClassObjectInAWaveBoundaryBatch = 5,

        /// <summary>
        /// The independent closure check (<see cref="DependencyClosure"/>) rejected the packer's own
        /// output. Never expected; it is a refusal rather than a warning because a plan whose closure
        /// cannot be verified is exactly as unusable as one known to be open.
        /// </summary>
        PlanFailedItsOwnClosureCheck = 6,
    }

    /// <summary>One reason a change set could not be batched, naming the objects involved.</summary>
    public sealed class BatchRefusal
    {
        internal BatchRefusal(BatchRefusalReason reason, IReadOnlyList<string> objectNames, string detail)
        {
            Reason = reason;
            ObjectNames = objectNames;
            Detail = detail;
        }

        /// <summary>Which reason.</summary>
        public BatchRefusalReason Reason { get; }

        /// <summary>The objects involved.</summary>
        public IReadOnlyList<string> ObjectNames { get; }

        /// <summary>What is wrong, and what would fix it.</summary>
        public string Detail { get; }

        /// <inheritdoc />
        public override string ToString() =>
            Reason + " [" + string.Join(", ", ObjectNames.ToArray()) + "]: " + Detail;
    }

    /// <summary>
    /// The result of planning a wave-boundary download: an ordered set of dependency-closed batches, a
    /// refusal with reasons, or nothing to batch.
    /// </summary>
    public sealed class BatchPlan
    {
        private BatchPlan(
            BatchPlanOutcome outcome,
            IReadOnlyList<DownloadBatch> batches,
            IReadOnlyList<BatchRefusal> refusals,
            int maxObjectsPerBatch,
            int objectsExamined,
            string summary)
        {
            Outcome = outcome;
            Batches = batches;
            Refusals = refusals;
            MaxObjectsPerBatch = maxObjectsPerBatch;
            ObjectsExamined = objectsExamined;
            Summary = summary;
        }

        /// <summary>Planned, refused, or nothing to batch.</summary>
        public BatchPlanOutcome Outcome { get; }

        /// <summary>The batches, in download order. Empty unless <see cref="Outcome"/> is Planned.</summary>
        public IReadOnlyList<DownloadBatch> Batches { get; }

        /// <summary>Every reason for a refusal.</summary>
        public IReadOnlyList<BatchRefusal> Refusals { get; }

        /// <summary>The limit applied — twenty, per DB-4, unless the caller said otherwise.</summary>
        public int MaxObjectsPerBatch { get; }

        /// <summary>
        /// How many changed objects were examined. Reported alongside every outcome so that "nothing
        /// went wrong" can always be distinguished from "nothing was looked at".
        /// </summary>
        public int ObjectsExamined { get; }

        /// <summary>A sentence describing the outcome.</summary>
        public string Summary { get; }

        /// <summary>TRUE only for <see cref="BatchPlanOutcome.Planned"/>.</summary>
        public bool Usable => Outcome == BatchPlanOutcome.Planned;

        /// <summary>How many objects the plan covers.</summary>
        public int PlannedObjectCount => Batches.Sum(b => b.Count);

        /// <summary>One line for the log.</summary>
        public string ToLogLine()
        {
            switch (Outcome)
            {
                case BatchPlanOutcome.Planned:
                    return "BATCH PLAN: " + Batches.Count + " batch(es), " + PlannedObjectCount +
                           " object(s), limit " + MaxObjectsPerBatch + " :: " + Summary;

                case BatchPlanOutcome.Refused:
                    return "BATCH PLAN REFUSED: " + Refusals.Count + " reason(s) over " +
                           ObjectsExamined + " object(s) :: " + Summary;

                default:
                    return "NOTHING TO BATCH: " + Summary;
            }
        }

        /// <summary>The log line plus every batch or every refusal, one per line.</summary>
        public string Describe()
        {
            var lines = new List<string> { ToLogLine() };
            lines.AddRange(Batches.Select(b => "  " + b.ToLogLine()));
            lines.AddRange(Refusals.Select(r => "  - " + r));
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        /// <inheritdoc />
        public override string ToString() => ToLogLine();

        internal static BatchPlan NothingToBatch(int maxObjectsPerBatch, string summary) =>
            new BatchPlan(
                BatchPlanOutcome.NothingToBatch,
                new DownloadBatch[0],
                new BatchRefusal[0],
                maxObjectsPerBatch,
                objectsExamined: 0,
                summary: summary);

        internal static BatchPlan Refuse(
            IReadOnlyList<BatchRefusal> refusals,
            int maxObjectsPerBatch,
            int objectsExamined,
            string summary)
        {
            if (refusals.Count == 0)
            {
                throw new InvalidOperationException(
                    "A batch refusal must carry at least one reason, or the plan reports a failure that " +
                    "names nothing to fix.");
            }

            return new BatchPlan(BatchPlanOutcome.Refused, new DownloadBatch[0], refusals, maxObjectsPerBatch, objectsExamined, summary);
        }

        internal static BatchPlan Planned(
            IReadOnlyList<DownloadBatch> batches,
            int maxObjectsPerBatch,
            int objectsExamined,
            string summary)
        {
            if (batches.Count == 0)
            {
                throw new InvalidOperationException(
                    "A plan with no batches is not a plan. An empty change set is reported as " +
                    "NothingToBatch, which is a different outcome on purpose.");
            }

            return new BatchPlan(BatchPlanOutcome.Planned, batches, new BatchRefusal[0], maxObjectsPerBatch, objectsExamined, summary);
        }
    }
}
