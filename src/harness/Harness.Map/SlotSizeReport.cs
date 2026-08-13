namespace Harness.Map;

/// <summary>
/// What the wave set's slot widths cost it — <b>a REPORT, never a gate.</b>
///
/// <para><b>Why this exists at all.</b> Slots are fixed-size within a wave set, sized to the largest
/// member (X-A). Before F-1 that cost nothing. After it, <c>R = floor(125 / slot_size)</c> decides how
/// many whole slots share one FC03 read, so <b>ONE WIDE SLOT COLLAPSES R FOR EVERY SLOT IN THE SET</b> —
/// a single 123-register slot admitted alongside twenty 20-register ones takes R from 6 to 1 and
/// sextuples the read cost of every one of them.</para>
///
/// <para><b>F-6 — whether admission should therefore group by slot size — IS DEFERRED (owner, 2026-08-13),
/// and this is what was built instead.</b> The reasoning is worth keeping because it is why nothing here
/// groups, reorders or sizes anything:</para>
/// <list type="bullet">
/// <item>Doing nothing only costs when widths are HETEROGENEOUS, and <b>there is no measurement of what
/// real slot widths look like</b> — no real block has been through the pipeline yet. That is 5.2.</item>
/// <item>Grouping by size is a SCHEDULING policy and can be added later <b>without touching the map</b>.</item>
/// <item>Size CLASSES change the map — slot addressing stops being <c>base + index x W</c> — <b>and
/// F-1's whole-slot read rule would have to hold across classes, which is the thing A1 and F-1 were
/// measured against.</b> Not a change to make on an unmeasured guess.</item>
/// </list>
///
/// <para><b>SO THIS IS A NUMBER THAT EXISTS WHEN SOMEBODY GOES LOOKING, AND NOTHING ELSE.</b> It does
/// not refuse, does not warn inside a green chain, and is not consulted by any check —
/// <c>MapResult.Allocated</c> is unaffected by it. This project's own rule is that a warning inside a
/// green chain gets skimmed; the point is that when the first real blocks arrive, the F-6 decision is
/// made on data rather than on the argument that produced this deferral.</para>
/// </summary>
/// <param name="WidestSlotId">The slot that set the wave set's width.</param>
/// <param name="WidestResultRegisters">Its result width — the width every slot is then allocated.</param>
/// <param name="SlotsPerRead">R as actually allocated.</param>
/// <param name="MostCommonRequestedWidth">
/// The modal requested result width; on a tie, the narrowest. Reported with its count rather than
/// called a "majority", so a reader can see whether it is one.
/// </param>
/// <param name="SlotsAtMostCommonWidth">How many slots asked for that width.</param>
/// <param name="SlotsPerReadAtMostCommonWidth">What R would have been had every slot been that wide.</param>
public sealed record SlotSizeReport(
    string WidestSlotId,
    int WidestResultRegisters,
    int SlotsPerRead,
    int MostCommonRequestedWidth,
    int SlotsAtMostCommonWidth,
    int SlotsPerReadAtMostCommonWidth,
    int TotalSlots)
{
    /// <summary>True when the widest slot cost the rest of the set read bandwidth they would otherwise have had.</summary>
    public bool Collapsed => SlotsPerReadAtMostCommonWidth > SlotsPerRead;

    /// <summary>How many times more reads a poll round costs than it would at the common width.</summary>
    public double ReadCostMultiple => SlotsPerRead == 0
        ? 0
        : (double)SlotsPerReadAtMostCommonWidth / SlotsPerRead;

    /// <summary>
    /// The report line. <b>It says outright that it is a report</b>, because a number in a green chain
    /// that does not say what it is gets read as a warning and then skimmed.
    /// </summary>
    public string Describe() => Collapsed
        ? $"SLOT-WIDTH REPORT (not a gate, nothing refuses on it): slot '{WidestSlotId}' asks for "
          + $"{WidestResultRegisters} result register(s), and slots are fixed-size across a wave set — so every one "
          + $"of the {TotalSlots} slots is allocated that width and R = {SlotsPerRead} whole slot(s) per FC03 read. "
          + $"{SlotsAtMostCommonWidth} of {TotalSlots} slot(s) asked for {MostCommonRequestedWidth} register(s), at "
          + $"which R would have been {SlotsPerReadAtMostCommonWidth} — so the read half of a poll round costs "
          + $"{ReadCostMultiple:0.#}x what it would in a set of that width. F-6 (whether admission should group by "
          + $"slot size) is DEFERRED pending a measurement of what real slot widths look like; this number exists so "
          + $"that decision is made on data."
        : $"SLOT-WIDTH REPORT (not a gate): {TotalSlots} slot(s) at {WidestResultRegisters} result register(s), "
          + $"R = {SlotsPerRead} whole slot(s) per FC03 read. No width collapse — no slot is paying for a wider "
          + $"neighbour.";

    /// <summary>Derive the report from what the wave set ASKED for, before fixed-sizing flattened it.</summary>
    internal static SlotSizeReport For(IReadOnlyList<SlotRequest> slots)
    {
        var widest = slots.OrderByDescending(s => s.ResultRegisters).ThenBy(s => s.SlotId, StringComparer.Ordinal).First();

        // Modal requested width; on a tie the narrowest, because that is the reading that makes the
        // collapse look LARGEST — and a report that flatters the current layout is a report nobody needs.
        var mostCommon = slots
            .GroupBy(s => s.ResultRegisters)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First();

        return new SlotSizeReport(
            widest.SlotId,
            widest.ResultRegisters,
            SlotsPerReadAt(widest.ResultRegisters),
            mostCommon.Key,
            mostCommon.Count(),
            SlotsPerReadAt(mostCommon.Key),
            slots.Count);
    }

    private static int SlotsPerReadAt(int width) => Math.Max(1, ModbusLimits.MaxReadRegisters / Math.Max(1, width));
}
