using Harness.Results;
using Xunit;

namespace Harness.Results.Tests;

/// <summary>
/// The two-level ordinal, and the two things it refuses.
///
/// <para><b>The unaffected cases are tested as deliberately as the refused ones.</b> A gate that fires
/// outside its scope is noise, and noise gets switched off — after which the cases it was right about go
/// through unchecked. So: a single-group slot needs no order and must not be asked for one, and a
/// boundary-spanning group nobody cites must not refuse a wave it has nothing to do with.</para>
/// </summary>
public class WaveOrderTests
{
    private static WaveSlotGroups Slot(string id, bool ordered, string[]? spanning = null, params string[] groups) =>
        new(id, groups.Length == 0 ? new[] { id } : groups, ordered, spanning);

    // ---- THE ORDINARY CASE, WHICH MUST NOT ACQUIRE A NEW REQUIREMENT -----------------------------

    [Fact]
    public void OneGroupPerSlot_NeedsNoStatedOrder_AndTheOrdinalIsTheVectorsOwnIndex()
    {
        var report = WaveOrder.Of(
            new[] { ("V-0", "S0", 0), ("V-1", "S0", 1), ("V-2", "S0", 2) },
            new[] { Slot("S0", ordered: false) });

        Assert.True(report.Ordered);
        Assert.Equal(0, report.OrdinalOf["V-0"]);
        Assert.Equal(1, report.OrdinalOf["V-1"]);
        Assert.Equal(2, report.OrdinalOf["V-2"]);
    }

    [Fact]
    public void TwoSlotsEachWithOneGroup_AreOrderedIndependently()
    {
        var report = WaveOrder.Of(
            new[] { ("A-0", "S0", 0), ("B-0", "S1", 0), ("B-1", "S1", 1) },
            new[] { Slot("S0", ordered: false), Slot("S1", ordered: false) });

        Assert.True(report.Ordered);
        Assert.Equal(0, report.OrdinalOf["A-0"]);
        Assert.Equal(0, report.OrdinalOf["B-0"]);
        Assert.Equal(1, report.OrdinalOf["B-1"]);
    }

    // ---- THE MANY-TO-ONE MAP ---------------------------------------------------------------------

    [Fact]
    public void ManyToOne_WithAStatedOrder_MergesByGroupThenIndex()
    {
        // Two groups, each restarting `index` at 0 — which is the shape MEASURED on the deliverable and
        // the whole reason the ordinal exists. Without a major key, G-0 and H-0 both read Results[0].
        var report = WaveOrder.Of(
            new[] { ("G-0", "G", 0), ("G-1", "G", 1), ("H-0", "H", 0), ("H-1", "H", 1) },
            new[] { Slot("S0", ordered: true, spanning: null, "G", "H") });

        Assert.True(report.Ordered);
        Assert.Equal(0, report.OrdinalOf["G-0"]);
        Assert.Equal(1, report.OrdinalOf["G-1"]);
        Assert.Equal(2, report.OrdinalOf["H-0"]);
        Assert.Equal(3, report.OrdinalOf["H-1"]);

        // Every vector resolves to the ONE binding slot, which is the point of the map.
        Assert.All(report.Ordinals, o => Assert.Equal("S0", o.SlotId));
    }

    [Fact]
    public void TheStatedOrderIsTheServesORDER_NotTheAlphabet()
    {
        var report = WaveOrder.Of(
            new[] { ("G-0", "G", 0), ("H-0", "H", 0) },
            new[] { Slot("S0", ordered: true, spanning: null, "H", "G") });

        Assert.True(report.Ordered);
        Assert.Equal(0, report.OrdinalOf["H-0"]);
        Assert.Equal(1, report.OrdinalOf["G-0"]);
    }

    [Fact]
    public void ManyToOne_WithNoStatedOrder_IsRefusedByName()
    {
        var report = WaveOrder.Of(
            new[] { ("G-0", "G", 0), ("H-0", "H", 0) },
            new[] { Slot("S0", ordered: false, spanning: null, "G", "H") });

        Assert.False(report.Ordered);
        Assert.Contains(report.Refusals, r => r.Contains("THE ORDER THEY RUN IN IS NOT STATED"));

        // *** AND IT DOES NOT FALL BACK. *** A partial order still indexes a whole tensor, so no vector
        // gets a position at all.
        Assert.Empty(report.Ordinals);
    }

    // ---- THE BOUNDARY-SPANNING GROUP -------------------------------------------------------------

    [Fact]
    public void ABoundarySpanningGroupWhoseVectorsAreSubmitted_IsRefusedByName()
    {
        var report = WaveOrder.Of(
            new[] { ("G-0", "G", 0), ("S-0", "STARTUP", 0), ("S-1", "STARTUP", 1) },
            new[] { Slot("S0", ordered: true, spanning: new[] { "STARTUP" }, "G", "STARTUP") });

        Assert.False(report.Ordered);
        Assert.Equal(new[] { "S-0", "S-1" }, report.BoundarySpanningVectors);
        Assert.Contains(report.Refusals, r => r.Contains("BOUNDARY-SPANNING") && r.Contains("'STARTUP'"));

        // It names the vectors, because the reader's next act is to submit them as their own run.
        Assert.Contains(report.Refusals, r => r.Contains("S-0") && r.Contains("S-1"));
    }

    [Fact]
    public void ABoundarySpanningGroupNOBODYCITES_RefusesNothing()
    {
        // *** THE UNAFFECTED CASE, AND IT MATTERS AS MUCH AS THE REFUSED ONE. *** A wave that does not
        // use the route is untouched by the route's precondition.
        var report = WaveOrder.Of(
            new[] { ("G-0", "G", 0), ("G-1", "G", 1) },
            new[] { Slot("S0", ordered: true, spanning: new[] { "STARTUP" }, "G", "STARTUP") });

        Assert.True(report.Ordered);
        Assert.Empty(report.BoundarySpanningVectors);
        Assert.Equal(new[] { 0, 1 }, new[] { report.OrdinalOf["G-0"], report.OrdinalOf["G-1"] });
    }

    [Fact]
    public void ABoundarySpanningGroupTakesNoInlinePosition_SoItsPlaceInServesIsNotRead()
    {
        // Listed FIRST and listed LAST must give the ordinary groups the same ordinals. If its position
        // were read, `G` would start at 1 in one of these.
        var first = WaveOrder.Of(
            new[] { ("G-0", "G", 0), ("H-0", "H", 0) },
            new[] { Slot("S0", ordered: true, spanning: new[] { "STARTUP" }, "STARTUP", "G", "H") });

        var last = WaveOrder.Of(
            new[] { ("G-0", "G", 0), ("H-0", "H", 0) },
            new[] { Slot("S0", ordered: true, spanning: new[] { "STARTUP" }, "G", "H", "STARTUP") });

        Assert.True(first.Ordered);
        Assert.True(last.Ordered);
        Assert.Equal(first.OrdinalOf["G-0"], last.OrdinalOf["G-0"]);
        Assert.Equal(first.OrdinalOf["H-0"], last.OrdinalOf["H-0"]);
        Assert.Equal(0, last.OrdinalOf["G-0"]);
        Assert.Equal(1, last.OrdinalOf["H-0"]);
    }

    [Fact]
    public void ExcludingAGroupTheSlotDoesNotServe_IsRefused()
    {
        // An exclusion from a sequence the group was never in reads as a handled case and handles
        // nothing — the wrong-pointer defect, in a field.
        var report = WaveOrder.Of(
            new[] { ("G-0", "G", 0) },
            new[] { Slot("S0", ordered: true, spanning: new[] { "TYPO" }, "G") });

        Assert.False(report.Ordered);
        Assert.Contains(report.Refusals, r => r.Contains("'TYPO'") && r.Contains("does not serve it"));
    }

    [Fact]
    public void AllGroupsBoundarySpanning_LeavesNoInlineOrderToState()
    {
        // One inline group remains after exclusion, so no order claim is demanded — and the spanning
        // vectors are still refused.
        var report = WaveOrder.Of(
            new[] { ("G-0", "G", 0), ("S-0", "STARTUP", 0) },
            new[] { Slot("S0", ordered: false, spanning: new[] { "STARTUP" }, "G", "STARTUP") });

        Assert.False(report.Ordered);
        Assert.DoesNotContain(report.Refusals, r => r.Contains("THE ORDER THEY RUN IN IS NOT STATED"));
        Assert.Single(report.BoundarySpanningVectors);
    }

    // ---- THE MERGE'S OWN INTEGRITY ---------------------------------------------------------------

    [Fact]
    public void AGapInAGroupsIndices_IsRefused()
    {
        // A hole in the merged tensor makes every later vector read its neighbour's run.
        var report = WaveOrder.Of(
            new[] { ("G-0", "G", 0), ("G-3", "G", 3) },
            new[] { Slot("S0", ordered: false) with { Groups = new[] { "G" } } });

        Assert.False(report.Ordered);
        Assert.Contains(report.Refusals, r => r.Contains("is not 0..1"));
    }

    [Fact]
    public void ARepeatedIndexInAGroup_IsRefused()
    {
        var report = WaveOrder.Of(
            new[] { ("G-a", "G", 0), ("G-b", "G", 0) },
            new[] { Slot("S0", ordered: false) with { Groups = new[] { "G" } } });

        Assert.False(report.Ordered);
        Assert.Contains(report.Refusals, r => r.Contains("is not 0..1"));
    }

    [Fact]
    public void OneGroupServedByTwoSlots_IsRefused()
    {
        // Which mirror region it reached would be decided by the order the bindings happen to be listed
        // in, which is not a decision anything should make.
        var report = WaveOrder.Of(
            new[] { ("G-0", "G", 0) },
            new[]
            {
                Slot("S0", ordered: false) with { Groups = new[] { "G" } },
                Slot("S1", ordered: false) with { Groups = new[] { "G" } },
            });

        Assert.False(report.Ordered);
        Assert.Contains(report.Refusals, r => r.Contains("is served by both"));
    }

    [Fact]
    public void AVectorCitingAnUnknownGroup_IsNotThisChecksBusiness()
    {
        // SlotJoin owns that refusal and says it better. Two checks reporting one fault is how a reader
        // comes to believe there are two.
        var report = WaveOrder.Of(
            new[] { ("G-0", "G", 0), ("X-0", "NOPE", 0) },
            new[] { Slot("S0", ordered: false) with { Groups = new[] { "G" } } });

        Assert.True(report.Ordered);
        Assert.DoesNotContain("X-0", report.OrdinalOf.Keys);
    }

    [Fact]
    public void TheDetailIsWrittenOnTheCleanPathToo()
    {
        var report = WaveOrder.Of(
            new[] { ("G-0", "G", 0) },
            new[] { Slot("S0", ordered: false) with { Groups = new[] { "G" } } });

        Assert.StartsWith("ORDERED:", report.Detail);
    }
}
