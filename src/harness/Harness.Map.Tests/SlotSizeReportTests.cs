using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// F-6's collapse report — <b>a number that exists when somebody goes looking, and nothing else.</b>
///
/// <para>F-6 (whether wave-set admission should group by slot size) is DEFERRED by the owner, because
/// doing nothing only costs when widths are heterogeneous and <b>nothing has measured what real slot
/// widths look like</b>. So this reports the cost and refuses nothing; the tests below hold both halves
/// of that, and the second half is the one that would rot.</para>
/// </summary>
public class SlotSizeReportTests
{
    private static MirrorGeometry Rig() => MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2);

    private static MapResult Allocate(params (string Id, int Result)[] slots) =>
        MapAllocator.Allocate(new WaveSetRequest(Rig(),
            slots.Select(s => new SlotRequest(s.Id, 2, s.Result)).ToArray()));

    // ---------------------------------------------------------------------------------------------
    // The trigger, and what it prints
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ONE_WIDE_SLOT_COLLAPSES_R_AND_THE_REPORT_SAYS_BY_HOW_MUCH()
    {
        // The spec's own worked example: a single 123-register slot alongside twenty 20-register ones
        // takes R from 6 to 1 and sextuples the read cost of every one of them.
        var result = Allocate(
            new[] { ("S0", 123) }
                .Concat(Enumerable.Range(1, 20).Select(i => ($"S{i}", 20)))
                .ToArray());

        var report = result.SizeReport!;

        Assert.True(report.Collapsed);
        Assert.Equal("S0", report.WidestSlotId);
        Assert.Equal(123, report.WidestResultRegisters);
        Assert.Equal(1, report.SlotsPerRead);
        Assert.Equal(20, report.MostCommonRequestedWidth);
        Assert.Equal(20, report.SlotsAtMostCommonWidth);
        Assert.Equal(6, report.SlotsPerReadAtMostCommonWidth);
        Assert.Equal(6, report.ReadCostMultiple);
        Assert.Equal(21, report.TotalSlots);
    }

    [Fact]
    public void The_report_SAYS_IT_IS_A_REPORT_because_a_number_in_a_green_chain_gets_skimmed()
    {
        var text = Allocate(("S0", 123), ("S1", 20), ("S2", 20)).SizeReport!.Describe();

        Assert.StartsWith("SLOT-WIDTH REPORT (not a gate", text, StringComparison.Ordinal);
        Assert.Contains("nothing refuses on it", text, StringComparison.Ordinal);
        Assert.Contains("F-6", text, StringComparison.Ordinal);
        Assert.Contains("DEFERRED", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_HOMOGENEOUS_wave_set_reports_NO_COLLAPSE_rather_than_saying_nothing()
    {
        // Doing nothing only costs when widths are HETEROGENEOUS, so the uniform case is the normal one
        // and it still gets a line — a report that only appears on bad news teaches a reader that its
        // absence means it was not run.
        var report = Allocate(("S0", 20), ("S1", 20), ("S2", 20)).SizeReport!;

        Assert.False(report.Collapsed);
        Assert.Equal(6, report.SlotsPerRead);
        Assert.Equal(6, report.SlotsPerReadAtMostCommonWidth);
        Assert.Contains("No width collapse", report.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_WIDER_MAJORITY_is_not_a_collapse_because_nobody_is_paying_for_a_neighbour()
    {
        // Widths {20, 123, 123}: the wide ones ARE the majority, so R = 1 is what the set asked for and
        // no slot is carrying a cost it did not choose.
        var report = Allocate(("S0", 20), ("S1", 123), ("S2", 123)).SizeReport!;

        Assert.False(report.Collapsed);
        Assert.Equal(123, report.MostCommonRequestedWidth);
    }

    [Fact]
    public void On_a_tie_the_NARROWEST_width_is_taken_so_the_report_never_flatters_the_current_layout()
    {
        // Widths {12, 20, 123}, one each. Taking the narrowest makes the collapse look LARGEST, and the
        // count is reported alongside so a reader can see it is 1 of 3 rather than a majority.
        var report = Allocate(("S0", 12), ("S1", 20), ("S2", 123)).SizeReport!;

        Assert.Equal(12, report.MostCommonRequestedWidth);
        Assert.Equal(1, report.SlotsAtMostCommonWidth);
        Assert.Equal(10, report.SlotsPerReadAtMostCommonWidth);
        Assert.Contains("1 of 3 slot(s) asked for 12", report.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void It_is_derived_from_what_the_SET_ASKED_FOR_not_from_the_flattened_map()
    {
        // After allocation every slot IS 123 registers wide, so the original widths exist nowhere else.
        // Computing this from the map would report "no collapse" on the exact case it exists for.
        var result = Allocate(("S0", 123), ("S1", 20), ("S2", 20));

        Assert.Equal(123, result.Require().ResultRegistersPerSlot);
        Assert.Equal(20, result.SizeReport!.MostCommonRequestedWidth);
    }

    // ---------------------------------------------------------------------------------------------
    // IT IS NOT A GATE, AND THIS IS THE HALF THAT WOULD ROT
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_COLLAPSED_WAVE_SET_STILL_ALLOCATES_and_nothing_refuses_on_the_report()
    {
        var result = Allocate(("S0", 123), ("S1", 20), ("S2", 20));

        Assert.True(result.SizeReport!.Collapsed);
        Assert.True(result.Allocated);
        Assert.Empty(result.Refusals);
        Assert.NotNull(result.Require());
    }

    [Fact]
    public void The_map_itself_carries_no_size_report_so_no_downstream_check_can_consult_it()
    {
        // Kept off RegisterMap deliberately: anything on the map is an input to the map hash question,
        // to the copy layer and to every client. A deferred POLICY must not become a value the design
        // silently starts depending on.
        Assert.DoesNotContain(typeof(RegisterMap).GetProperties(), p => p.PropertyType == typeof(SlotSizeReport));
    }

    [Fact]
    public void A_REFUSED_wave_set_reports_no_widths_because_there_were_none_to_read()
    {
        var refused = MapAllocator.Allocate(new WaveSetRequest(Rig(), Array.Empty<SlotRequest>()));

        Assert.False(refused.Allocated);
        Assert.Null(refused.SizeReport);
    }

    [Fact]
    public void Nothing_in_the_allocator_groups_reorders_or_sizes_anything()
    {
        // The ruling's operative half. Slot ORDER is the caller's and decides addresses; a set given
        // wide-first must allocate wide-first, or the report would have become a scheduler.
        var wideFirst = Allocate(("S0", 123), ("S1", 20)).Require();
        var narrowFirst = Allocate(("S0", 20), ("S1", 123)).Require();

        Assert.Equal("S0", wideFirst.Slots[0].SlotId);
        Assert.Equal("S0", narrowFirst.Slots[0].SlotId);
        Assert.Equal(123, wideFirst.ResultRegistersPerSlot);
        Assert.Equal(123, narrowFirst.ResultRegistersPerSlot);
    }
}
