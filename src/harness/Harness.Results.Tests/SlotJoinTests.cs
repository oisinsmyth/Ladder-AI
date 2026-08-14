using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// The submission/binding slot join — see <see cref="SlotJoin"/> for the measurement that put it here
/// rather than in the run.
/// </summary>
public class SlotJoinTests
{
    private static (string, string) V(string id, string slot) => (id, slot);

    [Fact]
    public void EVERY_UNBOUND_SLOT_IS_NAMED_with_the_vectors_citing_it_AND_the_slots_that_ARE_bound()
    {
        // The hopper deliverable's exact shape, scaled down: the binding declares one slot and the
        // vectors cite others.
        var report = SlotJoin.Check(
            new[]
            {
                V("VB-HBA-001", "SLOT-HBA-RAISE"),
                V("VB-HBA-002", "SLOT-HBA-RAISE"),
                V("VB-HBA-011", "SLOT-HBA-LATCH"),
            },
            new[] { "SLOT-HBA-ALL" });

        Assert.True(report.Any);
        Assert.Equal(3, report.Vectors);
        Assert.Equal(2, report.Slots);

        // BOTH sides, because a reader has to decide which of two documents is wrong.
        Assert.Contains("SLOT-HBA-RAISE", report.Detail, StringComparison.Ordinal);
        Assert.Contains("SLOT-HBA-LATCH", report.Detail, StringComparison.Ordinal);
        Assert.Contains("'SLOT-HBA-ALL'", report.Detail, StringComparison.Ordinal);
        Assert.Contains("VB-HBA-001", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void MORE_THAN_FOUR_VECTORS_ON_ONE_SLOT_ARE_ELIDED_but_the_COUNT_is_not()
    {
        var report = SlotJoin.Check(
            Enumerable.Range(1, 6).Select(i => V($"V-{i}", "MISSING")),
            new[] { "S0" });

        Assert.Equal(6, report.Vectors);
        Assert.Contains("6 vector(s)", report.Detail, StringComparison.Ordinal);
        Assert.Contains(", ...", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_BINDING_WITH_NO_SLOTS_AT_ALL_SAYS_SO_rather_than_printing_an_empty_list()
    {
        // "Bound: " followed by nothing reads as a formatting bug and sends the reader looking in the
        // wrong file. Empty is not clean here either.
        var report = SlotJoin.Check(new[] { V("V-1", "S0") }, Array.Empty<string>());

        Assert.True(report.Any);
        Assert.Contains("the binding declares no slots at all", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_BOUND_SLOT_WITH_NO_VECTORS_IS_NOT_A_FINDING_because_that_is_an_EXCISION()
    {
        // The unaffected case, asserted as deliberately as the refused one. D32 allows a slot that is
        // allocated and will never run; a gate firing on it would be noise, and noise gets switched off.
        var report = SlotJoin.Check(new[] { V("V-1", "S0") }, new[] { "S0", "S1" });

        Assert.False(report.Any);
        Assert.Equal(0, report.Vectors);
        Assert.Contains("2 bound slot(s)", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_MATCH_IS_ORDINAL_because_a_slot_id_is_a_key_and_not_a_word()
    {
        var report = SlotJoin.Check(new[] { V("V-1", "slot-hba-all") }, new[] { "SLOT-HBA-ALL" });

        Assert.True(report.Any);
    }
}
