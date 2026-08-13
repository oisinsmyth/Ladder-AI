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
}

/// <summary>
/// One index of the co-running log: which slots were asked to run, and which were observed to.
/// </summary>
/// <param name="Commanded">Slots whose start bool the commit raised.</param>
/// <param name="Executed">
/// Slots whose OWN start condition the copy layer latched — measured on the device, not derived here.
/// </param>
public sealed record CoRunningIndex(int WaveIndex, IReadOnlyList<int> Commanded, IReadOnlyList<int> Executed)
{
    /// <summary>Commanded and not executed.</summary>
    public IReadOnlyList<int> Missing => Commanded.Except(Executed).OrderBy(i => i).ToArray();

    /// <summary>Executed and not commanded.</summary>
    public IReadOnlyList<int> Unexpected => Executed.Except(Commanded).OrderBy(i => i).ToArray();

    public CoRunningOutcome Outcome =>
        Missing.Count > 0 ? CoRunningOutcome.CommandedButDidNotRun
        : Unexpected.Count > 0 ? CoRunningOutcome.RanButWasNotCommanded
        : CoRunningOutcome.Agrees;

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

    /// <summary>Record one index from the commit set and an observed control snapshot.</summary>
    public CoRunningIndex Record(int waveIndex, IReadOnlyList<int> commanded, ControlSnapshot observed, int slotCount)
    {
        ArgumentNullException.ThrowIfNull(commanded);
        ArgumentNullException.ThrowIfNull(observed);

        var executed = Enumerable.Range(0, slotCount).Where(observed.Executed).ToArray();
        var entry = new CoRunningIndex(waveIndex, commanded.OrderBy(i => i).ToArray(), executed);
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
        .Select(i => i.Outcome == CoRunningOutcome.CommandedButDidNotRun
            ? $"index {i.WaveIndex}: slot(s) {string.Join(", ", i.Missing)} were commanded and never ran. The test did not happen — this is not a failing test, and reading it as one would send an agent editing correct logic."
            : $"index {i.WaveIndex}: slot(s) {string.Join(", ", i.Unexpected)} ran without being commanded. Something other than this harness is driving that block's start condition.")
        .ToArray();
}
