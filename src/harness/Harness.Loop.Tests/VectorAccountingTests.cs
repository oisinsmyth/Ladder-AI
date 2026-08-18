using Harness.Loop;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE DENOMINATOR IS THE SUBMITTED VECTOR COUNT — NEVER THE NUMBER OF PACKAGES PRODUCED.</b>
///
/// <para><b>The defect these exist for, verbatim from the first wave that has ever run:</b>
/// <c>the wave ran to 22 index(es) over 1 slot(s), costing 890 round trip(s)</c>, then TWO packages,
/// then <c>0 of 2 package(s) say anything about the block at all</c>. <b>Nothing anywhere stated what
/// became of vectors 3 to 22.</b></para>
///
/// <para>A slot exiting early is legitimate; a report that silently renarrows its own denominator to
/// match what it managed to produce is not — and it is the shape most likely to be read as a complete
/// run, because every number in it is internally consistent.</para>
///
/// <para><b>This is tested here, as a pure function, rather than only through a wave</b>, because the
/// case that matters is a slot that stopped short, and arranging for a real wave to stop at a chosen
/// index is exactly the kind of setup cost that stops a case being tested at all. The end-to-end wiring
/// is proved separately in <see cref="RunDenominatorTests"/>.</para>
/// </summary>
public class VectorAccountingTests
{
    private static VectorAccounting.SubmittedVector V(int ordinal, int slot = 0) =>
        new($"V-{ordinal + 1:000}", $"S{slot}", slot, ordinal);

    private static SlotRunResult Result(SlotOutcome outcome, string detail) =>
        new(outcome, Array.Empty<ushort>(), default, default, 0, 0,
            new InertReport(InertOutcome.Established, default, Array.Empty<ushort>(), Array.Empty<ushort>(), "established"),
            detail,
            // These results carry no registers at all, so an empty series is the honest statement.
            ObservationSeries.Empty);

    private static SlotDistribution Distribution(int slot, params SlotRunResult[] results) =>
        new(slot, results.Length - 1, results,
            Array.Empty<(int WaveIndex, IReadOnlyList<int> CoRunners)>());

    // -------------------------------------------------------------------------------------------------
    // The complete run — tested as deliberately as the short one
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// *** THE UNAFFECTED CASE. *** A wave that ran every index reports every vector as Ran, no slot
    /// exits, and nothing that reads as a shortfall. A check that fires on healthy input is noise, and
    /// noise gets switched off.
    /// </summary>
    [Fact]
    public void EveryIndexRan_EveryVectorIsAccountedForAsRan()
    {
        var vectors = new[] { V(0), V(1), V(2) };

        var account = VectorAccounting.Of(
            vectors,
            VectorAccounting.ProgressOf(new[]
            {
                Distribution(0,
                    Result(SlotOutcome.Completed, "index 0 completed"),
                    Result(SlotOutcome.Completed, "index 1 completed"),
                    Result(SlotOutcome.Completed, "index 2 completed")),
            }),
            indicesPlanned: 3);

        Assert.Equal(3, account.Submitted);
        Assert.Equal(3, account.Ran);
        Assert.Equal(0, account.NeverAttempted);
        Assert.Empty(account.SlotExits);
        Assert.All(account.Vectors, v => Assert.Equal(VectorDisposition.Ran, v.Disposition));
        Assert.Equal(3, account.IndicesRun);
        Assert.Equal(3, account.IndicesPlanned);
    }

    // -------------------------------------------------------------------------------------------------
    // The measured shape: 22 submitted, 2 run
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>JOB9004'S FIRST LIVE WAVE, RECONSTRUCTED.</b> 22 vectors on one slot, the slot stopping after
    /// index 1. Every one of the 20 that were never attempted is a row with a reason, and the summary
    /// counts read against 22.
    /// </summary>
    [Fact]
    public void SlotStoppedAfterIndexOne_TheOtherTwentyAreAccountedForByName()
    {
        var vectors = Enumerable.Range(0, 22).Select(i => V(i)).ToArray();

        var account = VectorAccounting.Of(
            vectors,
            VectorAccounting.ProgressOf(new[]
            {
                Distribution(0,
                    Result(SlotOutcome.Completed, "completion register R001 reached 1"),
                    Result(SlotOutcome.NotInert, "the test never started, which is not a test failure: the scan counter did not advance")),
            }),
            indicesPlanned: 22);

        Assert.Equal(22, account.Submitted);
        Assert.Equal(2, account.Ran);
        Assert.Equal(20, account.NeverAttempted);

        // *** EVERY SUBMITTED VECTOR HAS EXACTLY ONE ROW. *** The count IS the property: a run that
        // accounts for 21 of 22 has the same defect in smaller print.
        Assert.Equal(22, account.Vectors.Count);
        Assert.Equal(vectors.Select(v => v.VectorId), account.Vectors.Select(a => a.VectorId));

        Assert.Equal(20, account.By(VectorDisposition.SlotExitedFirst).Count);

        var exit = Assert.Single(account.SlotExits);
        Assert.Equal(0, exit.SlotIndex);
        Assert.Equal(2, exit.IndicesRun);
        Assert.Equal(22, exit.TensorLength);
        Assert.Equal(1, exit.LastIndex);
        Assert.Equal(nameof(SlotOutcome.NotInert), exit.LastOutcome);
        Assert.Equal(20, exit.VectorsNeverAttempted);

        // The nearest thing to a CAUSE this level has: what the stopping index itself recorded. It is
        // carried rather than summarised, because the loop cannot know why the device did it.
        Assert.Contains("the scan counter did not advance", exit.LastDetail, StringComparison.Ordinal);
    }

    /// <summary>
    /// Each never-attempted row says WHICH index the slot stopped at and that this vector sits beyond it
    /// — <b>a disposition with no reason is a shorter way of saying nothing.</b>
    /// </summary>
    [Fact]
    public void ANeverAttemptedRow_NamesTheStoppingIndexAndRefusesToBeReadAsEvidence()
    {
        var account = VectorAccounting.Of(
            Enumerable.Range(0, 5).Select(i => V(i)).ToArray(),
            VectorAccounting.ProgressOf(new[]
            {
                Distribution(0,
                    Result(SlotOutcome.Completed, "ok"),
                    Result(SlotOutcome.TimedOut, "the backstop of 900 ms elapsed")),
            }),
            indicesPlanned: 5);

        var row = account.Vectors.Single(v => v.VectorId == "V-004");

        Assert.Equal(VectorDisposition.SlotExitedFirst, row.Disposition);
        Assert.False(row.Attempted);
        Assert.Contains("NEVER ATTEMPTED", row.Detail, StringComparison.Ordinal);
        Assert.Contains("stopped at index 1", row.Detail, StringComparison.Ordinal);
        Assert.Contains("index 3", row.Detail, StringComparison.Ordinal);
        Assert.Contains("Nothing here is evidence about the block", row.Detail, StringComparison.Ordinal);
        Assert.Contains("the backstop of 900 ms elapsed", row.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two slots, one of which finished — <b>the exit list names only the one that stopped short</b>, and
    /// the healthy slot's vectors are not swept up with it.
    /// </summary>
    [Fact]
    public void OneSlotShort_TheOtherSlotsVectorsAreUnaffected()
    {
        var vectors = new[] { V(0, 0), V(1, 0), V(2, 0), V(0, 1), V(1, 1) };

        var account = VectorAccounting.Of(
            vectors,
            VectorAccounting.ProgressOf(new[]
            {
                Distribution(0, Result(SlotOutcome.Completed, "ok")),
                Distribution(1, Result(SlotOutcome.Completed, "ok"), Result(SlotOutcome.Completed, "ok")),
            }),
            indicesPlanned: 3);

        Assert.Equal(5, account.Submitted);
        Assert.Equal(3, account.Ran);

        var exit = Assert.Single(account.SlotExits);
        Assert.Equal(0, exit.SlotIndex);
        Assert.Equal(2, exit.VectorsNeverAttempted);

        Assert.All(account.Vectors.Where(v => v.SlotIndex == 1), v => Assert.Equal(VectorDisposition.Ran, v.Disposition));
    }

    // -------------------------------------------------------------------------------------------------
    // The states that are not "a slot ran and stopped"
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A slot with NO distribution at all is its own disposition, and it is exercised directly.</b>
    ///
    /// <para><c>WaveRun</c> distributes every tensor it was given, so nothing in the loop can currently
    /// produce this. <b>That is precisely why it is constructed here rather than left to be reached by
    /// accident</b> — an empty classification is a slot waiting to be misused, and "the slot ran and
    /// stopped at index 4" and "no distribution for it exists" send a reader to different places.</para>
    /// </summary>
    [Fact]
    public void ASlotThatWasNeverDistributed_IsNotReportedAsAShortRun()
    {
        var account = VectorAccounting.Of(
            new[] { V(0, 0), V(1, 0) },
            VectorAccounting.ProgressOf(Array.Empty<SlotDistribution>()),
            indicesPlanned: 2);

        Assert.Equal(2, account.Submitted);
        Assert.Equal(0, account.Ran);
        Assert.All(account.Vectors, v => Assert.Equal(VectorDisposition.NotDistributed, v.Disposition));
        Assert.Contains("no results at all", account.Vectors[0].Detail, StringComparison.Ordinal);

        var exit = Assert.Single(account.SlotExits);
        Assert.Equal(0, exit.IndicesRun);
        Assert.Equal(2, exit.VectorsNeverAttempted);
    }

    /// <summary>
    /// A distribution carrying no results is <b>distributed-and-empty</b>, which is a different fact from
    /// never distributed — and neither of them is a slot that ran.
    /// </summary>
    [Fact]
    public void ADistributionWithNoResults_IsZeroIndicesRunRatherThanAbsent()
    {
        var progress = VectorAccounting.ProgressOf(new[]
        {
            new SlotDistribution(0, -1, Array.Empty<SlotRunResult>(), Array.Empty<(int, IReadOnlyList<int>)>()),
        });

        var account = VectorAccounting.Of(new[] { V(0), V(1) }, progress, indicesPlanned: 2);

        Assert.Equal(0, account.Ran);
        Assert.All(account.Vectors, v => Assert.Equal(VectorDisposition.SlotExitedFirst, v.Disposition));
        Assert.Equal(0, account.IndicesRun);
    }

    /// <summary>
    /// 🔴 <b>A run that stopped before any wave accounts for EVERY submitted vector, not none of them.</b>
    /// An empty list there says "no results yet"; the truth is "twenty-two were never attempted".
    /// </summary>
    [Fact]
    public void TheWaveNeverRan_EverySubmittedVectorIsStillAccountedFor()
    {
        var account = VectorAccounting.NothingAttempted(
            Enumerable.Range(0, 22).Select(i => V(i)),
            LoopOutcome.NotDeployed,
            "no deployment was attempted: this gateway was configured to refuse.");

        Assert.Equal(22, account.Submitted);
        Assert.Equal(0, account.Ran);
        Assert.Equal(22, account.NeverAttempted);
        Assert.All(account.Vectors, v => Assert.Equal(VectorDisposition.WaveDidNotRun, v.Disposition));
        Assert.Contains("NotDeployed", account.Vectors[0].Detail, StringComparison.Ordinal);
        Assert.Contains("this gateway was configured to refuse", account.Vectors[0].Detail, StringComparison.Ordinal);

        // No slot "exited": nothing entered. Reporting an exit here would invent a run that never began.
        Assert.Empty(account.SlotExits);
        Assert.Equal(0, account.IndicesRun);
    }

    /// <summary>
    /// <c>Submitted - Ran = NeverAttempted</c>, whatever the shape. <b>Asserted as an invariant rather
    /// than as three numbers that happen to agree in one fixture</b>: the whole defect was two counts
    /// that were each individually right and did not describe the same set.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(22, 2)]
    [InlineData(6, 0)]
    public void TheThreeCountsAlwaysReconcile(int submitted, int indicesRun)
    {
        var distributions = indicesRun == 0
            ? Array.Empty<SlotDistribution>()
            : new[] { Distribution(0, Enumerable.Range(0, indicesRun).Select(_ => Result(SlotOutcome.Completed, "ok")).ToArray()) };

        var account = VectorAccounting.Of(
            Enumerable.Range(0, submitted).Select(i => V(i)).ToArray(),
            VectorAccounting.ProgressOf(distributions),
            indicesPlanned: submitted);

        Assert.Equal(submitted, account.Submitted);
        Assert.Equal(indicesRun, account.Ran);
        Assert.Equal(submitted - indicesRun, account.NeverAttempted);
        Assert.Equal(account.Submitted, account.Vectors.Count);
    }
}
