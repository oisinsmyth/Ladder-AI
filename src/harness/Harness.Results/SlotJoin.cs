namespace Harness.Results;

/// <summary>
/// Vectors whose <c>slot</c> names a slot nobody bound, and the refusal that names them.
/// </summary>
/// <param name="Vectors">How many vectors are unbound in total.</param>
/// <param name="Slots">How many DISTINCT slot ids they name.</param>
/// <param name="Detail">
/// The whole refusal, ready to print. <b>It names both sides</b> — every unbound id with the vectors
/// citing it, and every id that IS bound — because the reader has to decide which of two documents is
/// wrong and cannot do that from one half.
/// </param>
public sealed record SlotJoinReport(int Vectors, int Slots, string Detail)
{
    /// <summary>True when at least one vector names a slot no binding declares.</summary>
    public bool Any => Slots > 0;

    /// <summary>Nothing unbound. <b>A real answer, not an absence</b> — the join was made and it held.</summary>
    public static SlotJoinReport Clean(int boundSlots) =>
        new(0, 0, $"every vector names one of the {boundSlots} bound slot(s).");
}

/// <summary>
/// 🔴 <b>THE SLOT ID IS THE ONLY JOIN BETWEEN A SUBMISSION AND A BINDING, AND NOTHING COMPARED THEM
/// UNTIL THE WAVE WAS ALREADY RUNNING.</b>
///
/// <para>The vector author writes <c>slot</c>; the coordinator writes <c>slotId</c>; <b>by design
/// neither reads the other's document</b>, which is what makes the two an independent pair of
/// authorities. The cost of that independence is that a typo, a rename or an unresolved slot partition
/// shows up nowhere until something tries to use both.</para>
///
/// <para><b>MEASURED 2026-08-14, and the exception is not the defect — the POSITION is.</b> A vector
/// naming an unbound slot passed map derivation, the submission gate, the width check, copy-layer
/// generation and the 0.1b retention assertion; the program was <b>DEPLOYED</b>; the version register
/// was <b>CONFIRMED</b>; and only then did the run throw
/// <c>ArgumentException: no slot 'SLOT-NOT-BOUND' in this map</c>, with the gateway recording one
/// deployment and one open transport. <i>A cross-reference failure that costs a download is a check in
/// the wrong place, not a rare case.</i></para>
///
/// <para><b>This WAS the live shape of the hopper deliverable, and it is not any more.</b> The committed
/// binding declared one slot, <c>SLOT-HBA-ALL</c>, while all 27 conformance vectors cited six other ids,
/// and this reported <c>27 vector(s) name 6 slot(s) that no binding declares</c> — correctly, and with no
/// route past it that was not an invention. <b>The route is now
/// <c>SlotBinding.Serves</c>:</b> one slot answers to several specification ids, so the bound set passed
/// in is the CITABLE ids rather than the map's internal slot ids. <i>The check is unchanged; what changed
/// is what it is given.</i></para>
///
/// <para>⚠️ <b>Do not re-read that as "the join is satisfied by declaring it".</b> The ids a binding
/// serves are still the coordinator's statement about which vectors address which mirror region, and a
/// vector citing an id nobody serves is refused exactly as before — including the slot's OWN id once
/// <c>serves</c> is stated, because such a vector has no position in the merged run.</para>
///
/// <para><b>IT IS DELIBERATELY ONE-SIDED.</b> A vector naming a slot nobody bound cannot run. A BOUND
/// slot with no vectors is a different fact — a slot held inert, or one whose vectors were excised
/// (D32) — and refusing it here would refuse a path the design provides. A gate that fires outside its
/// scope is noise, and noise gets switched off.</para>
/// </summary>
public static class SlotJoin
{
    /// <summary>
    /// Check every vector's slot against the bound set.
    /// </summary>
    /// <param name="vectors">Each vector's own id and the slot it names.</param>
    /// <param name="boundSlotIds">The slot ids the bindings declare, in map order.</param>
    public static SlotJoinReport Check(
        IEnumerable<(string VectorId, string Slot)> vectors,
        IReadOnlyList<string> boundSlotIds)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentNullException.ThrowIfNull(boundSlotIds);

        var bound = new HashSet<string>(boundSlotIds, StringComparer.Ordinal);

        var unbound = vectors
            .Where(v => !bound.Contains(v.Slot ?? string.Empty))
            .GroupBy(v => v.Slot ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        if (unbound.Length == 0)
            return SlotJoinReport.Clean(boundSlotIds.Count);

        var named = string.Join(" | ", unbound.Select(g =>
            $"'{g.Key}' ({g.Count()} vector(s): {string.Join(", ", g.Take(4).Select(v => v.VectorId))}{(g.Count() > 4 ? ", ..." : string.Empty)})"));

        return new SlotJoinReport(
            unbound.Sum(g => g.Count()),
            unbound.Length,
            $"{unbound.Sum(g => g.Count())} vector(s) name {unbound.Length} slot(s) that no binding declares. "
            + "THE SLOT ID IS THE ONLY JOIN BETWEEN THE SUBMISSION AND THE BINDING, and the two documents are written by "
            + "parties who deliberately do not read each other's. Unbound: " + named
            + ". Bound: " + (boundSlotIds.Count == 0 ? "NOTHING - the binding declares no slots at all" : string.Join(", ", boundSlotIds.Select(s => $"'{s}'")))
            + ". *** THIS IS A BINDING/SUBMISSION DISAGREEMENT, NOT A FAULT IN EITHER FILE'S SYNTAX. *** Either the binding "
            + "is short of slots the vectors were written against, or the vectors cite ids the coordinator did not use.");
    }
}
