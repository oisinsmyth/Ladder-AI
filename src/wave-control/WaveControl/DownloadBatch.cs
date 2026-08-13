using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// Whether loading a batch requires the CPU to be stopped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THE ZERO VALUE IS <see cref="Undetermined"/>, AND IT IS THE ONLY VALUE THIS LIBRARY EVER
    /// PRODUCES. *** Measured 2026-08-13, and it is why no cost model here assumes either answer:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     A download of TWO changed blocks loaded into a RUNNING CPU with NO stop/start cycle at all —
    ///     <c>StopModules</c> was never answered and <c>StartModules</c> was never raised [M].
    ///   </description></item>
    ///   <item><description>
    ///     An earlier download of NINETEEN objects DID require the stop [M].
    ///   </description></item>
    /// </list>
    /// <para>
    /// *** SO THE STOP IS A FUNCTION OF WHAT CHANGED, NOT OF DOWNLOADING — AND NOT OF OBJECT COUNT
    /// EITHER, since counting is the obvious wrong lesson to draw from those two runs. *** Batch cost
    /// is therefore NOT uniform per object and not uniform per batch. What determines it has not been
    /// measured, so this library REFUSES TO PREDICT IT: every batch it plans reports
    /// <see cref="Undetermined"/>.
    /// </para>
    /// <para>
    /// <see cref="Required"/> and <see cref="NotRequired"/> exist for the determination made AT
    /// DOWNLOAD TIME, from the configurations the download actually raises (D32) — whether
    /// <c>StopModules</c> appeared at all. Nothing PC-side can answer it in advance, and the component
    /// that would consume the answer is 4.2's escalation ladder, which is blocked on A6/G4 (what a
    /// throw from the download delegate leaves the CPU in, unmeasured, failure mode a half-loaded CPU).
    /// </para>
    /// </remarks>
    public enum CpuStopRequirement
    {
        /// <summary>
        /// Not determinable before the download. The only value this library produces.
        /// </summary>
        Undetermined = 0,

        /// <summary>The download raised a configuration that entails stopping the CPU. Set at download time only.</summary>
        Required = 1,

        /// <summary>The download completed with the CPU left running. Set at download time only.</summary>
        NotRequired = 2,
    }

    /// <summary>
    /// One dependency-closed batch of at most twenty changed objects (DB-4) — the unit that lands in
    /// one wave-boundary download.
    /// </summary>
    public sealed class DownloadBatch
    {
        internal DownloadBatch(int index, IReadOnlyList<ChangedObject> objects)
        {
            Index = index;
            Objects = objects;
        }

        /// <summary>Zero-based position in the plan. Batches are downloaded in this order.</summary>
        public int Index { get; }

        /// <summary>The objects in the batch, ordered by name for a stable plan and a stable log.</summary>
        public IReadOnlyList<ChangedObject> Objects { get; }

        /// <summary>How many objects — the figure DB-4's limit is expressed in.</summary>
        public int Count => Objects.Count;

        /// <summary>The object names.</summary>
        public IReadOnlyList<string> ObjectNames => Objects.Select(o => o.Name).ToArray();

        /// <summary>
        /// ALWAYS <see cref="CpuStopRequirement.Undetermined"/> for a planned batch. See that type: the
        /// stop is a function of what changed, the determining property is unmeasured, and this library
        /// will not guess in either direction.
        /// </summary>
        public CpuStopRequirement CpuStop => CpuStopRequirement.Undetermined;

        /// <summary>
        /// TRUE when any object in the batch resets data values (DB-1's RUN (Init) row). It does NOT
        /// change the queue — it still flows through a wave boundary — but every result obtained before
        /// it is invalid (DB-2's validity stamp).
        /// </summary>
        public bool ResetsData => Objects.Any(o => o.ResetsData);

        /// <summary>One line for the log.</summary>
        public string ToLogLine() =>
            "BATCH " + Index + " (" + Count + " object(s), CPU stop " + CpuStop +
            (ResetsData ? ", RESETS DATA — invalidates earlier results" : string.Empty) + "): " +
            string.Join(", ", ObjectNames.ToArray());

        /// <inheritdoc />
        public override string ToString() => ToLogLine();
    }
}
