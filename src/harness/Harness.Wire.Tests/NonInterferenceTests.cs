using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// The differential itself, driven by a stub wave rather than by a program.
///
/// <para><b>This is where it is made to FAIL.</b> Driven only by the real skeleton, the check would be
/// exercised on a system that behaves — and a check nobody has seen refuse is a check nobody knows
/// refuses. The <c>run</c> delegate exists for exactly this.</para>
/// </summary>
public class NonInterferenceTests
{
    private static SlotTensor Tensor(int slot, int length = 1) => new(slot,
        Enumerable.Range(0, length).Select(_ => new WireVector(
            Array.Empty<ushort>(),
            new InertDeclaration(new Dictionary<int, ushort>()),
            0, 1, new ScanBudget(1, 1))).ToArray());

    private static SlotRunResult Result(SlotOutcome outcome, params ushort[] results) =>
        new(outcome, results, new ScanCount(0), new ScanCount(0), 1, 1,
            new InertReport(InertOutcome.Established, new ScanCount(0), Array.Empty<ushort>(), Array.Empty<ushort>(), "stub"), "stub",
            // This fixture is about NON-INTERFERENCE, not about observation; one frame states what these
            // results are without pretending to a series. See ObservationSeries.OfSingleFrame.
            ObservationSeries.OfSingleFrame(results, new ScanCount(0)));

    /// <summary>A wave whose per-slot results are dictated by the caller, keyed on the set of slots run.</summary>
    private static Func<IReadOnlyList<SlotTensor>, WaveResult> Waves(
        Func<IReadOnlyList<int>, int, IReadOnlyList<SlotRunResult>> resultsFor,
        CoRunningLog? log = null) =>
        tensors =>
        {
            var slots = tensors.Select(t => t.SlotIndex).ToArray();
            var distributions = tensors
                .Select(t => new SlotDistribution(t.SlotIndex, t.Length - 1, resultsFor(slots, t.SlotIndex),
                    Array.Empty<(int, IReadOnlyList<int>)>()))
                .ToArray();

            return new WaveResult(tensors.Max(t => t.Length), distributions, log ?? Agreeing(tensors), 0);
        };

    private static CoRunningLog Agreeing(IReadOnlyList<SlotTensor> tensors)
    {
        var log = new CoRunningLog();
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            Enumerable.Range(0, 2).Select(i => new SlotRequest($"S{i}", 1, 1)).ToArray())).Require();

        var commanded = tensors.Select(t => t.SlotIndex).ToArray();
        var word = new ushort[map.StartEcho.Length];
        foreach (var slot in commanded)
            word[0] |= (ushort)(1 << slot);

        log.Record(0, commanded, new ControlSnapshot(1, default, word, word), map.Slots.Count);
        return log;
    }

    [Fact]
    public void A_pair_whose_concurrent_results_match_its_solo_results_is_indistinguishable()
    {
        var report = NonInterference.Compare(new[] { Tensor(0), Tensor(1) },
            Waves((_, slot) => new[] { Result(SlotOutcome.Completed, (ushort)(10 + slot)) }));

        Assert.True(report.Indistinguishable, report.Summary());
        Assert.Equal(2, report.Comparisons);
    }

    [Fact]
    public void A_slot_whose_value_moves_when_the_other_runs_is_a_finding()
    {
        var report = NonInterference.Compare(new[] { Tensor(0), Tensor(1) },
            Waves((slots, slot) => new[]
            {
                Result(SlotOutcome.Completed, slot == 0 && slots.Count > 1 ? (ushort)99 : (ushort)10),
            }));

        Assert.False(report.Indistinguishable);
        var finding = Assert.Single(report.Findings);
        Assert.Equal(0, finding.SlotIndex);
        Assert.Contains("did not change between those two runs", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_slot_whose_OUTCOME_changes_is_a_finding_even_when_the_registers_match()
    {
        // A slot that completed alone and timed out alongside is interference too, and comparing only the
        // register values would miss it — the registers of a timed-out slot can be anything, including
        // exactly what a completed one published.
        var report = NonInterference.Compare(new[] { Tensor(0), Tensor(1) },
            Waves((slots, slot) => new[]
            {
                Result(slot == 0 && slots.Count > 1 ? SlotOutcome.TimedOut : SlotOutcome.Completed, 10),
            }));

        Assert.False(report.Indistinguishable);
        Assert.Contains(report.Findings, f => f.Detail.Contains("ended Completed alone", StringComparison.Ordinal));
    }

    [Fact]
    public void A_slot_that_produced_a_DIFFERENT_NUMBER_of_results_is_a_finding()
    {
        var report = NonInterference.Compare(new[] { Tensor(0, 2), Tensor(1, 2) },
            Waves((slots, slot) => slot == 0 && slots.Count > 1
                ? new[] { Result(SlotOutcome.Completed, 10) }
                : new[] { Result(SlotOutcome.Completed, 10), Result(SlotOutcome.Completed, 10) }));

        Assert.False(report.Indistinguishable);
        Assert.Contains(report.Findings, f => f.Detail.Contains("result(s) alone and", StringComparison.Ordinal));
    }

    [Fact]
    public void A_differential_that_compared_NOTHING_is_not_a_pass()
    {
        // The solo runs produced no results, so there was nothing for the concurrent ones to be
        // indistinguishable FROM. Every other field reads clean, which is why the count gates.
        var report = NonInterference.Compare(new[] { Tensor(0), Tensor(1) },
            Waves((_, _) => Array.Empty<SlotRunResult>()));

        Assert.False(report.Indistinguishable);
        Assert.Equal(0, report.Comparisons);
        Assert.Contains("NOT COMPARED", report.Summary(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_co_running_discrepancy_gates_the_verdict_rather_than_being_reported_beside_it()
    {
        // If a slot was commanded and never executed, its "solo" results are the previous state and
        // comparing them proves nothing. A differential over runs that did not happen is worthless.
        var broken = new CoRunningLog();
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            new[] { new SlotRequest("S0", 1, 1), new SlotRequest("S1", 1, 1) })).Require();
        broken.Record(0, new[] { 0, 1 }, new ControlSnapshot(1, default, new ushort[] { 0b11 }, new ushort[] { 0b01 }), 2);

        var report = NonInterference.Compare(new[] { Tensor(0), Tensor(1) },
            Waves((_, slot) => new[] { Result(SlotOutcome.Completed, (ushort)(10 + slot)) }, broken));

        Assert.False(report.Indistinguishable);
        Assert.Empty(report.Findings);
        Assert.NotEmpty(report.Discrepancies);
    }

    [Fact]
    public void A_differential_needs_at_least_two_slots()
    {
        Assert.Throws<ArgumentException>(() =>
            NonInterference.Compare(new[] { Tensor(0) }, Waves((_, _) => Array.Empty<SlotRunResult>())));
    }
}
