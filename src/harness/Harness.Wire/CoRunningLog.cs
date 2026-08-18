namespace Harness.Wire;

/// <summary>What the commanded set and the executed set said about one wave index.</summary>
public enum CoRunningOutcome
{
    /// <summary>Every commanded slot ran, and no slot ran that was not commanded.</summary>
    Agrees,

    /// <summary>A slot was commanded and its block was never seen to run. The test did not happen.</summary>
    CommandedButDidNotRun,

    /// <summary>
    /// A slot ran that was NOT commanded at this index. Under D26a rule 2 a null slot's bool is simply
    /// not raised, so a block that ran anyway is driven by something other than this harness.
    /// </summary>
    RanButWasNotCommanded,

    /// <summary>
    /// 🔴 <b>NO COMMIT HAPPENED AT THIS INDEX AT ALL — the inert phase refused, so no start bool was
    /// ever raised.</b>
    ///
    /// <para><b>Deliberately NOT <see cref="CommandedButDidNotRun"/>, and that distinction is the whole
    /// reason this member exists.</b> The wave used to record the index's PLANNED slot set as
    /// <c>Commanded</c> on the inert-failure path, where <c>InertPhase.Commit</c> is never called. The
    /// echo had just been cleared by the same inert phase, so the log necessarily reported <i>"the slot
    /// was commanded and the echo says its block never saw its start condition (X-E)"</i> — <b>a
    /// fabricated accusation against a block that was never asked to do anything</b>, and it is exactly
    /// the false evidence X-E was built to eliminate, arriving through X-E's own channel. Measured on
    /// JOB9004's vessel wave 2026-08-18: the run's final control frame read
    /// <c>StartBools = 0x0000</c> — nothing had been commanded — while the package reported
    /// <c>CommandedButDidNotRun</c>, and the investigation it triggered went looking for a start-bit
    /// write race in the client that does not exist.</para>
    ///
    /// <para><b>It is also not <see cref="Agrees"/>.</b> Commanded and executed are both empty here, so
    /// they trivially agree — and reporting a wave that stopped dead as "everything agreed" is the empty
    /// -is-not-clean shape this repository keeps paying for.</para>
    /// </summary>
    NotCommitted,
}

/// <summary>
/// One index of the co-running log: which slots were asked to run, and which were observed to.
/// </summary>
/// <param name="Commanded">
/// 🔴 <b>Slots whose start bool the COMMIT ACTUALLY RAISED — never the slots an index planned to run.</b>
/// On an index where <c>InertPhase.Commit</c> was not reached this is EMPTY, and <paramref name="Planned"/>
/// carries what would have run. See <see cref="CoRunningOutcome.NotCommitted"/>.
/// </param>
/// <param name="Executed">
/// Slots whose OWN start condition the copy layer latched — measured on the device, not derived here.
/// </param>
/// <param name="Committed">
/// Whether the commit transaction happened at all. <c>false</c> makes an empty
/// <paramref name="Commanded"/> readable as "nothing was asked to run" rather than as "a wave set of no
/// slots", which are different facts.
/// </param>
/// <param name="Planned">
/// The slots that WOULD have been commanded. Kept so a refused index still says which tests it cost;
/// it is never mistaken for evidence because nothing derives an outcome from it.
/// </param>
public sealed record CoRunningIndex(
    int WaveIndex,
    IReadOnlyList<int> Commanded,
    IReadOnlyList<int> Executed,
    bool Committed = true,
    IReadOnlyList<int>? Planned = null)
{
    /// <summary>What the index set out to run. Equal to <see cref="Commanded"/> on a committed index.</summary>
    public IReadOnlyList<int> PlannedSlots => Planned ?? Commanded;

    /// <summary>Commanded and not executed.</summary>
    public IReadOnlyList<int> Missing => Commanded.Except(Executed).OrderBy(i => i).ToArray();

    /// <summary>Executed and not commanded.</summary>
    public IReadOnlyList<int> Unexpected => Executed.Except(Commanded).OrderBy(i => i).ToArray();

    /// <summary>
    /// <b>The two measured disagreements first, then the "no commit happened" state.</b>
    ///
    /// <para><see cref="CoRunningOutcome.RanButWasNotCommanded"/> deliberately still wins over
    /// <see cref="CoRunningOutcome.NotCommitted"/>: an echo latched at an index where the client raised
    /// nothing means something OTHER than this harness is driving that block, which is more alarming on a
    /// refused index than on a committed one, not less. Swallowing it under "nothing was commanded" would
    /// hide the one finding this log exists to make.</para>
    /// </summary>
    public CoRunningOutcome Outcome =>
        Missing.Count > 0 ? CoRunningOutcome.CommandedButDidNotRun
        : Unexpected.Count > 0 ? CoRunningOutcome.RanButWasNotCommanded
        : Committed ? CoRunningOutcome.Agrees
        : CoRunningOutcome.NotCommitted;

    /// <summary>Which other slots shared this index with <paramref name="slotIndex"/>, as MEASURED.</summary>
    public IReadOnlyList<int> CoRunnersOf(int slotIndex) =>
        Executed.Where(i => i != slotIndex).OrderBy(i => i).ToArray();
}

/// <summary>
/// X-E's co-running log — <b>built from executed start bools, never from the plan.</b>
///
/// <para><b>Why the distinction is not pedantry.</b> D21 has results carry a slice of the wave timeline
/// so that interference the conflict graph could not predict is diagnosable: when agent A opens a valve,
/// the model raises a level and agent B's level-alarm test fails, B's own code is innocent and B would
/// otherwise hunt a phantom. That only works if the slice is TRUE. Generated from the PLANNED slot set
/// it names slots that never executed — the spec's own worked example has an excised slot's neighbours
/// reading "ran alongside S3 and S4" when S3 and S4 were never downloaded — and §7a's cause-4
/// attribution then points at a phantom, <b>which is worse than having no log at all</b>.</para>
///
/// <para><b>So the log is a MEASUREMENT.</b> The copy layer latches each block's own start condition into
/// an echo register; the client clears the latches at inert and reads them back with the poll it was
/// making anyway. What was intended to run and what ran are two facts, and this keeps both.</para>
///
/// <para><b>It also happens to be the guard against the one inference left in the map.</b> The <c>%M</c>
/// byte carrying a register's bit is inferred, not measured (<c>MirrorGeometry.BitAddressOf</c>). If that
/// inference is wrong, a commanded slot's block never sees its start condition — and this log reports
/// exactly that, as <see cref="CoRunningOutcome.CommandedButDidNotRun"/>, rather than leaving a test to
/// time out for an unexplained reason. That was not why the echo was specified; it falls out of it.</para>
/// </summary>
public sealed class CoRunningLog
{
    private readonly List<CoRunningIndex> _indices = new();

    /// <summary>Every index recorded, in wave order.</summary>
    public IReadOnlyList<CoRunningIndex> Indices => _indices;

    /// <summary>True when every index agreed. There is no partial credit.</summary>
    public bool Agrees => _indices.Count > 0 && _indices.All(i => i.Outcome == CoRunningOutcome.Agrees);

    /// <summary>
    /// Record one index <b>from the set the commit actually raised</b> and an observed control snapshot.
    ///
    /// <para>🔴 <b>Only reachable after <c>InertPhase.Commit</c> has returned.</b> Handing this the slots
    /// an index PLANNED, on a path where the commit did not happen, is the defect
    /// <see cref="CoRunningOutcome.NotCommitted"/> documents — use <see cref="RecordNotCommitted"/> there.</para>
    /// </summary>
    public CoRunningIndex Record(int waveIndex, IReadOnlyList<int> commanded, ControlSnapshot observed, int slotCount)
    {
        ArgumentNullException.ThrowIfNull(commanded);
        ArgumentNullException.ThrowIfNull(observed);

        var executed = Enumerable.Range(0, slotCount).Where(observed.Executed).ToArray();
        var entry = new CoRunningIndex(waveIndex, commanded.OrderBy(i => i).ToArray(), executed);
        _indices.Add(entry);
        return entry;
    }

    /// <summary>
    /// 🔴 <b>Record an index at which NO COMMIT HAPPENED — the inert phase refused before any start bool
    /// was raised.</b>
    ///
    /// <para>The commanded set is EMPTY because that is what the device says: on such an index the last
    /// write to the start-bool word was <c>InertPhase.Establish</c>'s <c>LowerAllStartBools()</c>, and the
    /// echo was cleared by the same phase a round trip later. <paramref name="planned"/> is kept so the
    /// index still says which tests it cost, but nothing derives an outcome from it — a plan is not
    /// evidence, which is the sentence this whole class opens with.</para>
    /// </summary>
    /// <param name="observed">
    /// The control snapshot anyway, because the echo is still worth reading here: a latch set at an index
    /// where nothing was commanded is <see cref="CoRunningOutcome.RanButWasNotCommanded"/> and is real.
    /// </param>
    public CoRunningIndex RecordNotCommitted(int waveIndex, IReadOnlyList<int> planned, ControlSnapshot observed, int slotCount)
    {
        ArgumentNullException.ThrowIfNull(planned);
        ArgumentNullException.ThrowIfNull(observed);

        var executed = Enumerable.Range(0, slotCount).Where(observed.Executed).ToArray();

        var entry = new CoRunningIndex(waveIndex, Array.Empty<int>(), executed,
            Committed: false, Planned: planned.OrderBy(i => i).ToArray());

        _indices.Add(entry);
        return entry;
    }

    /// <summary>The slice one slot's results carry: the indices it ran at, and who ran alongside it.</summary>
    public IReadOnlyList<(int WaveIndex, IReadOnlyList<int> CoRunners)> SliceFor(int slotIndex) =>
        _indices
            .Where(i => i.Executed.Contains(slotIndex))
            .Select(i => (i.WaveIndex, i.CoRunnersOf(slotIndex)))
            .ToArray();

    /// <summary>Every index whose commanded and executed sets disagreed, with the reason.</summary>
    public IReadOnlyList<string> Discrepancies => _indices
        .Where(i => i.Outcome != CoRunningOutcome.Agrees)
        .Select(i => i.Outcome switch
        {
            CoRunningOutcome.CommandedButDidNotRun =>
                $"index {i.WaveIndex}: slot(s) {string.Join(", ", i.Missing)} were commanded and never ran. The test did not happen — this is not a failing test, and reading it as one would send an agent editing correct logic.",

            CoRunningOutcome.RanButWasNotCommanded =>
                $"index {i.WaveIndex}: slot(s) {string.Join(", ", i.Unexpected)} ran without being commanded. Something other than this harness is driving that block's start condition.",

            // *** NOT AN X-E DISAGREEMENT, AND IT SAYS SO IN ITS OWN TEXT. *** Nothing was asked to run,
            // so nothing failing to run says anything whatsoever about any block. The cause is upstream —
            // the inert phase's own report names it — and this line exists to send the reader there
            // rather than at the logic.
            _ =>
                $"index {i.WaveIndex}: NO START BOOL WAS RAISED — the inert phase refused before the commit, so slot(s) "
                + $"{string.Join(", ", i.PlannedSlots)} were PLANNED and never commanded. This is NOT the X-E disagreement above it: "
                + "nothing was asked to run. Read the inert phase's own outcome for the cause; nothing here is evidence about a block.",
        })
        .ToArray();
}
