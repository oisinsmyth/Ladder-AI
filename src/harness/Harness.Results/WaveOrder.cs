namespace Harness.Results;

/// <summary>
/// One binding slot's sequencing declaration, as <see cref="WaveOrder"/> needs it.
/// </summary>
/// <param name="SlotId">The binding's own slot id — the map key and the tag fragment.</param>
/// <param name="Groups">
/// The specification slot ids this slot answers to, <b>in the order the binding declared them</b>. Never
/// empty: a slot always answers to at least its own id.
/// </param>
/// <param name="OrderStated">
/// Whether the coordinator positively claimed that <paramref name="Groups"/>' order is the order they
/// run in. <b>Ignored when there is only one group</b> — one group needs no order, and demanding a claim
/// there would fire the gate on every ordinary binding, which is how a gate becomes noise.
/// </param>
/// <param name="BoundarySpanning">
/// 🔴 <b>Groups that do NOT run inline with the others because a DOWNLOAD BOUNDARY happens inside
/// them.</b>
///
/// <para>*** THIS IS A PROPERTY OF THE VECTORS AND IT IS IN THEIR OWN FIELDS, NOT A RULING HANDED
/// DOWN. *** The hopper set's <c>STARTUP</c> vectors say <i>"RIDES AN ALREADY-SCHEDULED DISRUPTIVE
/// BOUNDARY (X-F)… The boundary stops and restarts the CPU"</i> and <i>"THE HARNESS IS DISCONNECTED
/// ACROSS THE DOWNLOAD (D18) and nobody is polling while the first scan happens"</i>. <b>They require a
/// CPU restart to occur in the middle of them.</b></para>
///
/// <para><b>Folding such a group into the inline sequence is a confident wrong answer, not a limitation.</b>
/// Run inline, a restart-spanning vector executes with no restart — and every group scheduled after it
/// would run against a freshly cleared accumulator and a reconnected harness that the plain sequence never
/// accounted for. It reads as a clean pass. <i>Same class as the multi-writer defect: the rungs all ran,
/// the packages all arrived, and the experiment was not the one anybody asked for.</i></para>
///
/// <para>Its POSITION inside <see cref="Groups"/> is deliberately not read — it takes no inline ordinal at
/// all — so listing it anywhere is equivalent, and a test pins that.</para>
/// </param>
public sealed record WaveSlotGroups(
    string SlotId,
    IReadOnlyList<string> Groups,
    bool OrderStated,
    IReadOnlyList<string>? BoundarySpanning = null);

/// <summary>
/// Where one vector sits in its slot's merged run.
/// </summary>
/// <param name="VectorId">The vector.</param>
/// <param name="SlotId">The BINDING slot it resolved to — not the id it cited.</param>
/// <param name="Group">The specification slot id it cited.</param>
/// <param name="GroupRank">The group's position in the stated order. 0 for a single-group slot.</param>
/// <param name="Ordinal">
/// 🔴 <b>Its position in the merged sequence for that slot — the number the wave indexes
/// <c>Results[…]</c> by.</b> This is what the submission does not carry: each group restarts
/// <c>index</c> at 0, so <c>index</c> alone is not a position in anything.
/// </param>
public sealed record WaveOrdinal(string VectorId, string SlotId, string Group, int GroupRank, int Ordinal);

/// <summary>
/// The merged order, or every reason there is not one.
/// </summary>
/// <param name="Ordinals">
/// One entry per vector that resolved. <b>Empty when <see cref="Refusals"/> is non-empty</b> — a partial
/// order is not a weaker order, it is a wrong one, and the vectors it does contain would still index a
/// tensor built from all of them.
/// </param>
/// <param name="Refusals">Every reason the merge cannot be made, each naming what to state.</param>
/// <param name="Detail">The whole report, ready to print, on the clean path as well as the refused one.</param>
/// <param name="BoundarySpanning">
/// Vectors that cite a BOUNDARY-SPANNING group and therefore cannot be scheduled in this wave at all.
/// <b>Carried separately from <see cref="Refusals"/> because the two have different remedies</b>: an
/// unstated order is fixed by stating one, and this is fixed by running the group around the download
/// boundary it needs — which is not a thing this loop can do.
/// </param>
public sealed record WaveOrderReport(
    IReadOnlyList<WaveOrdinal> Ordinals,
    IReadOnlyList<string> Refusals,
    string Detail,
    IReadOnlyList<string>? BoundarySpanning = null)
{
    /// <summary>Vector ids that cannot run inline. Never null.</summary>
    public IReadOnlyList<string> BoundarySpanningVectors => BoundarySpanning ?? Array.Empty<string>();

    /// <summary>True when every vector has a position. <b>Not "no refusals" alone</b> — see the note on empty.</summary>
    public bool Ordered => Refusals.Count == 0 && BoundarySpanningVectors.Count == 0;

    /// <summary>Vector id → its position in its slot's merged run.</summary>
    public IReadOnlyDictionary<string, int> OrdinalOf =>
        Ordinals.ToDictionary(o => o.VectorId, o => o.Ordinal, StringComparer.Ordinal);

    /// <summary>Vector id → the BINDING slot id it resolved to.</summary>
    public IReadOnlyDictionary<string, string> SlotOf =>
        Ordinals.ToDictionary(o => o.VectorId, o => o.SlotId, StringComparer.Ordinal);
}

/// <summary>
/// 🔴 <b>THE TWO-LEVEL ORDINAL — <c>(group, index)</c> — WITHOUT WHICH A MANY-TO-ONE SLOT MAP PRODUCES
/// 21 CONFIDENTLY WRONG PACKAGES OUT OF 27.</b>
///
/// <para><b>The problem is a consequence of the fix above it, not of the vectors.</b> Once six
/// specification slot ids resolve to ONE mirror slot — which four independent authorities say they must —
/// their vectors merge into one tensor. And <i>the submission carries no total order</i>: MEASURED, each
/// group restarts <c>index</c> at 0, so the deliverable's 27 vectors carry six distinct index values
/// between them. The run reads <c>distribution.Results[ordinal]</c>, so a naive merge gives <b>six vectors
/// all reading <c>Results[0]</c>, and 21 of 27 packages carrying another vector's run</b> — with no error
/// raised anywhere. That is the same confident-wrong-answer class as the multi-writer defect, arriving one
/// layer up.</para>
///
/// <para>*** SO THE SLOT ID STOPS BEING AN ADDRESSING KEY AND BECOMES A SEQUENCING KEY: *** the MAJOR
/// half of a two-level ordinal, with the vector's own <c>index</c> as the minor half.</para>
///
/// <para>🔴 <b>AND THE GROUP ORDER IS NOT THIS CODE'S TO CHOOSE. IT REFUSES BY NAME WHEN UNSTATED, AND
/// DOES NOT FALL BACK TO DOCUMENT ORDER.</b> Document order is the obvious candidate and has the merit of
/// being a property of an artifact rather than a preference — <b>and it is still a guess wearing a
/// mechanism's clothes.</b> It is also not a free choice on the live deliverable: one group is a CPU
/// stop/restart across a disruptive download boundary with retentive memory preserved, so where it sits
/// decides what every group after it starts from. When the coordinator states an order, the DATA supplies
/// it and nothing in this file changes; that is the test of whether this is a mechanism or a decision.</para>
///
/// <para><b>Two more refusals, and both are about the merge rather than about taste:</b> a group whose
/// indices are not exactly <c>0..n-1</c> would leave a HOLE in the merged tensor (every later vector then
/// reads its neighbour's run), and a repeated index inside one group would give two vectors one position.
/// Neither can be repaired by choosing, so neither is.</para>
///
/// <para><b>What this deliberately does NOT do:</b> it does not report a vector citing an unbound slot.
/// That is <see cref="SlotJoin"/>'s, it is one-sided there for a stated reason, and two checks reporting
/// one fault is how a reader comes to believe there are two.</para>
/// </summary>
public static class WaveOrder
{
    /// <summary>
    /// Merge every vector into a position within its slot's run.
    /// </summary>
    /// <param name="vectors">Each vector's id, the slot id it CITED, and its own index within that group.</param>
    /// <param name="slots">The binding slots, each with the groups it serves and whether their order is stated.</param>
    public static WaveOrderReport Of(
        IEnumerable<(string VectorId, string Slot, int Index)> vectors,
        IReadOnlyList<WaveSlotGroups> slots)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentNullException.ThrowIfNull(slots);

        var refusals = new List<string>();
        var rows = vectors.ToArray();

        // group id -> (binding slot, rank within that slot's stated order). Built first so a duplicate
        // group id across two slots is caught before anything is ordered by it.
        var rank = new Dictionary<string, (string SlotId, int Rank)>(StringComparer.Ordinal);

        // Groups that take NO inline ordinal. Kept as its own set rather than as a flag on the rank,
        // because a boundary-spanning group has no rank — giving it one is exactly the mis-scheduling
        // being prevented.
        var spanning = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var slot in slots)
        {
            var declaredSpanning = slot.BoundarySpanning ?? Array.Empty<string>();
            var groups = (slot.Groups ?? Array.Empty<string>()).ToArray();

            foreach (var named in declaredSpanning.Where(g => !groups.Contains(g, StringComparer.Ordinal)))
            {
                refusals.Add(
                    $"slot '{slot.SlotId}' declares '{named}' boundary-spanning, and does not serve it. A group excluded from a "
                    + "sequence it was never in reads as a handled case and handles nothing.");
            }

            foreach (var named in declaredSpanning.Where(g => groups.Contains(g, StringComparer.Ordinal)))
                spanning[named] = slot.SlotId;

            // *** THE INLINE SEQUENCE IS WHAT IS LEFT. *** A boundary-spanning group takes no position, so
            // its place in the list is not read and moving it changes nothing.
            var inline = groups.Where(g => !spanning.ContainsKey(g)).ToArray();

            if (groups.Length == 0)
            {
                refusals.Add($"slot '{slot.SlotId}' answers to no specification id at all, so no vector can address it.");
                continue;
            }

            if (inline.Length > 1 && !slot.OrderStated)
            {
                refusals.Add(
                    $"slot '{slot.SlotId}' serves {inline.Length} inline specification slot id(s) — {string.Join(", ", inline.Select(g => $"'{g}'"))} — "
                    + "and THE ORDER THEY RUN IN IS NOT STATED. *** THIS IS NOT DEFAULTED TO THE ORDER THEY ARE LISTED IN, AND NOT TO "
                    + "DOCUMENT ORDER. *** The submission carries no total order of its own: every group restarts `index` at 0, so `index` "
                    + "alone is not a position in anything, and a merge without a major key gives one vector per group all reading "
                    + "Results[0] while the rest carry another vector's run — reported confidently, with no error. The groups are also not "
                    + "interchangeable: a group that stops and restarts the CPU changes what every group after it begins from. Declare "
                    + "`servesRunInOrder: true` to claim that the `serves` array's order IS the running order, or reorder `serves` until it is.");
                continue;
            }

            for (var i = 0; i < inline.Length; i++)
            {
                if (rank.TryGetValue(inline[i], out var already))
                {
                    refusals.Add(
                        $"specification slot id '{inline[i]}' is served by both '{already.SlotId}' and '{slot.SlotId}'. A vector citing it "
                        + "would address two mirror regions, and which one it reached would be decided by the order the bindings happen to be listed in.");
                    continue;
                }

                rank[inline[i]] = (slot.SlotId, i);
            }
        }

        // *** A VECTOR CITING A BOUNDARY-SPANNING GROUP IS BOUND AND UNSCHEDULABLE, WHICH ARE DIFFERENT
        // FACTS. *** It must not fall out as "unknown slot" (SlotJoin would blame the wrong thing) and it
        // must not receive an inline ordinal (that is the mis-scheduling itself). So it is named.
        var unschedulable = rows
            .Where(v => spanning.ContainsKey(v.Slot ?? string.Empty))
            .OrderBy(v => v.VectorId, StringComparer.Ordinal)
            .ToArray();

        // Every vector that resolved to an INLINE group, grouped by the binding slot it lands in. A vector
        // citing an unbound id is silently left out here on purpose: SlotJoin owns that refusal.
        var resolved = rows
            .Where(v => rank.ContainsKey(v.Slot ?? string.Empty))
            .Select(v => new ResolvedRow(v.VectorId, v.Slot!, v.Index, rank[v.Slot!].SlotId, rank[v.Slot!].Rank))
            .ToArray();

        var ordinals = new List<WaveOrdinal>();

        foreach (var bySlot in resolved.GroupBy(v => v.BindingSlot, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var offset = 0;

            foreach (var byGroup in bySlot.GroupBy(v => v.Rank).OrderBy(g => g.Key))
            {
                var members = byGroup.OrderBy(v => v.Index).ToArray();
                var groupId = byGroup.First().Group;

                // *** THE INDICES MUST BE EXACTLY 0..n-1. *** A gap leaves a hole in the merged tensor and
                // every later vector reads its neighbour's run; a repeat gives two vectors one position.
                // Both are silent, and neither has a repair this code could choose.
                var seen = members.Select(m => m.Index).ToArray();
                var expected = Enumerable.Range(0, members.Length).ToArray();

                if (!seen.SequenceEqual(expected))
                {
                    refusals.Add(
                        $"group '{groupId}' on slot '{bySlot.Key}' carries {members.Length} vector(s) with indices "
                        + $"[{string.Join(", ", seen)}], which is not 0..{members.Length - 1}. A gap leaves a HOLE in the merged run and "
                        + "every vector after it reads its neighbour's result; a repeat gives two vectors one position. Neither is "
                        + "recoverable by choosing, so neither is chosen.");
                    continue;
                }

                foreach (var member in members)
                    ordinals.Add(new WaveOrdinal(member.VectorId, bySlot.Key, groupId, byGroup.Key, offset + member.Index));

                offset += members.Length;
            }
        }

        if (unschedulable.Length > 0)
        {
            var byGroup = unschedulable
                .GroupBy(v => v.Slot ?? string.Empty, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToArray();

            refusals.Add(
                $"{unschedulable.Length} vector(s) cite {byGroup.Length} BOUNDARY-SPANNING group(s), which cannot run inline with the rest "
                + "of this wave. *** THE VECTORS THEMSELVES SAY WHY: the boundary STOPS AND RESTARTS THE CPU, and the harness is "
                + "DISCONNECTED ACROSS THE DOWNLOAD, so nobody is polling while the first scan happens. *** Executing them in an ordinary "
                + "inline sequence would run a restart-spanning scenario WITHOUT the restart, and would run every group after it against a "
                + "freshly cleared accumulator and a reconnected harness — a confident wrong answer that reads as a clean pass. This loop "
                + "runs ONE inline sequence and cannot schedule a download boundary inside a wave, so these are refused BY NAME rather than "
                + "folded in: "
                + string.Join(" | ", byGroup.Select(g => $"'{g.Key}' ({g.Count()} vector(s): {string.Join(", ", g.Select(v => v.VectorId))})"))
                + ". Submit them as their own run, sequenced around the boundary by whoever owns the deployment.");
        }

        if (refusals.Count > 0)
        {
            // Emptied on purpose. A partial order is not a weaker order — the ordinals it DOES contain
            // would still index a tensor built from every vector, which is the wrong-answer case.
            return new WaveOrderReport(
                Array.Empty<WaveOrdinal>(), refusals,
                $"NOT ORDERED: {refusals.Count} reason(s). No vector was given a position, because a partial order still indexes a whole tensor.",
                unschedulable.Select(v => v.VectorId).ToArray());
        }

        var slotCount = ordinals.Select(o => o.SlotId).Distinct(StringComparer.Ordinal).Count();
        var groupCount = ordinals.Select(o => o.Group).Distinct(StringComparer.Ordinal).Count();

        return new WaveOrderReport(ordinals, Array.Empty<string>(),
            $"ORDERED: {ordinals.Count} vector(s) over {groupCount} group(s) merged into {slotCount} slot(s) by (group, index). "
            + (groupCount > slotCount
                ? "The group is the MAJOR key, and its rank comes from the binding's stated order."
                : "One group per slot, so the ordinal is the vector's own index."));
    }

    /// <summary>One vector after its cited group has been resolved to a binding slot and a rank.</summary>
    private sealed record ResolvedRow(string VectorId, string Group, int Index, string BindingSlot, int Rank);
}
