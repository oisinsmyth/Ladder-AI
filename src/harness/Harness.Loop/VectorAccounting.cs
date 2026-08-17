using Harness.Wire;

namespace Harness.Loop;

/// <summary>What became of one SUBMITTED vector. Every one of them gets exactly one of these.</summary>
public enum VectorDisposition
{
    /// <summary>The wave reached this vector's index on its slot, and a result package exists for it.</summary>
    Ran,

    /// <summary>
    /// 🔴 <b>The wave ran and this slot's run ended BEFORE this vector's index.</b> The vector was never
    /// attempted; nothing about it, or about the block, was tested.
    ///
    /// <para>A slot exiting early is legitimate behaviour (D26a rule 3 lets a slot leave on its own
    /// tensor length, and an inert phase that cannot be established stops the wave outright). <b>What is
    /// not legitimate is a report that quietly renarrows its denominator to what it managed to
    /// produce</b>, which is the shape most likely to be read as a complete run.</para>
    /// </summary>
    SlotExitedFirst,

    /// <summary>
    /// The loop stopped before any wave existed — not admissible, not ordered, not deployed, not
    /// confirmed, and the rest. <b>Every submitted vector is in this state, not none of them</b>: a run
    /// that tested nothing must account for everything it did not test.
    /// </summary>
    WaveDidNotRun,

    /// <summary>
    /// The wave ran and produced NO results at all for this vector's slot.
    ///
    /// <para><b>Kept apart from <see cref="SlotExitedFirst"/> deliberately.</b> "The slot ran and stopped
    /// at index 4" and "the slot has no distribution at all" are different facts and send a reader to
    /// different places. <see cref="WaveRun"/> distributes every tensor it was given, including the ones
    /// a stopped wave never reached, so this is a backstop rather than a routine state — <b>and it is
    /// tested directly rather than left to be reached by accident</b>, because a classification nothing
    /// can produce is a slot waiting to be misused.</para>
    /// </summary>
    NotDistributed,
}

/// <summary>One submitted vector and what became of it.</summary>
/// <param name="WaveIndex">Its ordinal in its slot's MERGED run — not its own <c>index</c> field, which restarts at 0 per group.</param>
public sealed record VectorAccount(
    string VectorId,
    string CitedSlotId,
    int SlotIndex,
    int WaveIndex,
    VectorDisposition Disposition,
    string Detail)
{
    public bool Attempted => Disposition == VectorDisposition.Ran;
}

/// <summary>
/// One slot that stopped short of its own tensor — <b>named, with the index it reached and the cost in
/// vectors.</b>
/// </summary>
/// <param name="LastOutcome">The <see cref="SlotOutcome"/> recorded at the last index it did reach.</param>
/// <param name="LastDetail">That index's own explanation, which is the nearest thing to a CAUSE this level has.</param>
public sealed record SlotExit(
    int SlotIndex,
    int IndicesRun,
    int TensorLength,
    int LastIndex,
    string LastOutcome,
    string LastDetail,
    int VectorsNeverAttempted);

/// <summary>
/// 🔴 <b>The run's denominator: every SUBMITTED vector, with a disposition and a reason.</b>
///
/// <para><b>The defect this closes, measured on the first wave that has ever run.</b> The run reported:
/// <c>the wave ran to 22 index(es) over 1 slot(s)</c>, then two packages, then
/// <c>0 of 2 package(s) say anything about the block at all</c>. <b>Nothing anywhere stated what became
/// of vectors 3 to 22.</b> The summary's denominator was THE NUMBER OF PACKAGES PRODUCED, not the number
/// of vectors SUBMITTED — so a run that reported on 2 of 22 read as a complete one, and the 20 that were
/// never attempted left no trace in the console or in the JSON.</para>
///
/// <para><b>So the denominator is the submitted count, always, and it is computed in ONE place that both
/// the packages and the report read.</b> <see cref="LoopRun"/> builds a package if and only if this says
/// <see cref="VectorDisposition.Ran"/>, which is what stops the two from drifting into disagreement —
/// the previous arrangement had the package loop silently <c>continue</c> on a missing run, and no other
/// code knew it had happened.</para>
/// </summary>
/// <param name="IndicesPlanned">The wave's length: the longest tensor. What the wave SET OUT to run.</param>
/// <param name="IndicesRun">
/// The most indices any slot actually recorded. <b>Not the same number as
/// <paramref name="IndicesPlanned"/> once a wave stops early</b>, and reporting only the first is how
/// "ran to 22 index(es)" came to describe a run that reached two.
/// </param>
public sealed record RunAccount(
    IReadOnlyList<VectorAccount> Vectors,
    IReadOnlyList<SlotExit> SlotExits,
    int IndicesPlanned,
    int IndicesRun)
{
    /// <summary>The denominator. <b>Every summary line in this system reads against this</b>, never against the packages.</summary>
    public int Submitted => Vectors.Count;

    public int Ran => Vectors.Count(v => v.Disposition == VectorDisposition.Ran);

    /// <summary>Submitted and never attempted, whatever the reason. <c>Submitted - Ran</c> by construction.</summary>
    public int NeverAttempted => Submitted - Ran;

    public IReadOnlyList<VectorAccount> By(VectorDisposition disposition) =>
        Vectors.Where(v => v.Disposition == disposition).ToArray();

    /// <summary>An empty account, for the paths that have not yet read a submission at all.</summary>
    public static RunAccount Nothing { get; } =
        new(Array.Empty<VectorAccount>(), Array.Empty<SlotExit>(), 0, 0);
}

/// <summary>
/// Computes <see cref="RunAccount"/>. <b>Pure, and separate from the loop</b>, so the case that matters —
/// a slot that stopped short — can be constructed directly rather than reached by arranging for a wave to
/// go wrong.
/// </summary>
public static class VectorAccounting
{
    /// <summary>One submitted vector, as this accounting needs it.</summary>
    /// <param name="WaveIndex">The MERGED ordinal, from <c>WaveOrder</c>.</param>
    public readonly record struct SubmittedVector(string VectorId, string CitedSlotId, int SlotIndex, int WaveIndex);

    /// <summary>
    /// 🔴 <b>The wave never ran: EVERY submitted vector is accounted for as never attempted.</b>
    ///
    /// <para>Not "no vectors" and not an empty list. A loop that stopped at the gate, at the order, at
    /// deployment or at the version register has told the reader nothing about any of them, and the
    /// honest denominator for that run is still the full submitted set.</para>
    /// </summary>
    public static RunAccount NothingAttempted(
        IEnumerable<SubmittedVector> vectors, LoopOutcome outcome, string detail)
    {
        ArgumentNullException.ThrowIfNull(vectors);

        var rows = vectors
            .Select(v => new VectorAccount(
                v.VectorId, v.CitedSlotId, v.SlotIndex, v.WaveIndex,
                VectorDisposition.WaveDidNotRun,
                $"NEVER ATTEMPTED - the loop stopped at {outcome} before any wave ran, so this vector was not submitted to "
                + $"the device at all. Nothing here is evidence about the block. {detail}"))
            .ToArray();

        return new RunAccount(rows, Array.Empty<SlotExit>(), 0, 0);
    }

    /// <summary>
    /// The wave ran: each vector either has a result at its index on its slot, or it does not and this
    /// says which index the slot stopped at.
    /// </summary>
    /// <param name="indicesRunBySlot">
    /// Slot index -> how many indices that slot actually recorded, and what the last one said. <b>Slots
    /// absent from this map produced no distribution at all</b>, which is
    /// <see cref="VectorDisposition.NotDistributed"/> and not a short run.
    /// </param>
    /// <param name="indicesPlanned">The wave's length - the longest tensor.</param>
    public static RunAccount Of(
        IReadOnlyList<SubmittedVector> vectors,
        IReadOnlyDictionary<int, SlotProgress> indicesRunBySlot,
        int indicesPlanned)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentNullException.ThrowIfNull(indicesRunBySlot);

        // The tensor length of a slot IS the number of submitted vectors that resolve to it: the wave
        // groups the submission by slot and runs one index per vector. Derived rather than passed in, so
        // it cannot disagree with the denominator two lines below.
        var tensorLengths = vectors
            .GroupBy(v => v.SlotIndex)
            .ToDictionary(g => g.Key, g => g.Count());

        var rows = new List<VectorAccount>();

        foreach (var vector in vectors)
        {
            if (!indicesRunBySlot.TryGetValue(vector.SlotIndex, out var progress))
            {
                rows.Add(new VectorAccount(
                    vector.VectorId, vector.CitedSlotId, vector.SlotIndex, vector.WaveIndex,
                    VectorDisposition.NotDistributed,
                    $"NEVER ATTEMPTED - the wave produced no results at all for slot {vector.SlotIndex}. This is not a slot "
                    + "that ran and stopped: no distribution for it exists, so not even the index it reached is known."));

                continue;
            }

            if (vector.WaveIndex < progress.IndicesRun)
            {
                rows.Add(new VectorAccount(
                    vector.VectorId, vector.CitedSlotId, vector.SlotIndex, vector.WaveIndex,
                    VectorDisposition.Ran,
                    $"RAN at index {vector.WaveIndex} of slot {vector.SlotIndex}. Read its result package - a package existing "
                    + "is not a statement that anything held."));

                continue;
            }

            rows.Add(new VectorAccount(
                vector.VectorId, vector.CitedSlotId, vector.SlotIndex, vector.WaveIndex,
                VectorDisposition.SlotExitedFirst,
                $"NEVER ATTEMPTED - slot {vector.SlotIndex} ran {progress.IndicesRun} of its "
                + $"{tensorLengths[vector.SlotIndex]} index(es) and stopped at index {progress.IndicesRun - 1}, which recorded "
                + $"{progress.LastOutcome}. This vector sits at index {vector.WaveIndex} and was never submitted to the "
                + $"device. Nothing here is evidence about the block. The stopping index said: {progress.LastDetail}"));
        }

        var exits = tensorLengths
            .Where(t => !indicesRunBySlot.TryGetValue(t.Key, out var p) || p.IndicesRun < t.Value)
            .OrderBy(t => t.Key)
            .Select(t =>
            {
                var progress = indicesRunBySlot.TryGetValue(t.Key, out var p)
                    ? p
                    : new SlotProgress(0, "<no distribution>", "the wave produced no distribution for this slot at all.");

                return new SlotExit(
                    t.Key,
                    progress.IndicesRun,
                    t.Value,
                    progress.IndicesRun - 1,
                    progress.LastOutcome,
                    progress.LastDetail,
                    t.Value - progress.IndicesRun);
            })
            .ToArray();

        var indicesRun = indicesRunBySlot.Count == 0 ? 0 : indicesRunBySlot.Values.Max(p => p.IndicesRun);

        return new RunAccount(rows, exits, indicesPlanned, indicesRun);
    }

    /// <summary>How far one slot actually got, and what the last index it reached said.</summary>
    public readonly record struct SlotProgress(int IndicesRun, string LastOutcome, string LastDetail);

    /// <summary>
    /// A wave's distributions as <see cref="SlotProgress"/>.
    ///
    /// <para><b>A distribution with no results is <c>IndicesRun = 0</c> and says so</b> rather than being
    /// dropped, because a slot that was distributed empty and a slot that was never distributed are
    /// different facts.</para>
    /// </summary>
    public static IReadOnlyDictionary<int, SlotProgress> ProgressOf(IReadOnlyList<SlotDistribution> distributions)
    {
        ArgumentNullException.ThrowIfNull(distributions);

        return distributions.ToDictionary(
            d => d.SlotIndex,
            d => d.Results.Count == 0
                ? new SlotProgress(0, "<nothing recorded>", "the slot was distributed with no results at all.")
                : new SlotProgress(
                    d.Results.Count,
                    d.Results[^1].Outcome.ToString(),
                    d.Results[^1].Detail));
    }
}
