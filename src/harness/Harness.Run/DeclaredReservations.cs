using Harness.Gate;
using Harness.Map;

namespace Harness.Run;

/// <summary>
/// 🔴 <b>The ONE reading of a binding's <c>reservedRegions</c> into a <see cref="MirrorGeometry"/>.</b>
///
/// <para><b>Why it is a type rather than four lines at each call site.</b> The rule it carries is small
/// and every part of it is load-bearing in a way that is invisible to a reader of the four lines:
/// <c>register</c> absent maps to <c>-1</c> and <c>length</c> absent to <c>0</c> <i>so that the region
/// refuses itself</i>; identical declarations collapse and different ones do not; and an EMPTY set must
/// not reach <see cref="MirrorGeometry.Reserving(ReservedRegion[])"/> at all, which throws on one. A
/// second derivation of that would agree with the first on the day it was written and is the shape this
/// repository refuses.</para>
///
/// <para><b>The union is over a SEQUENCE of documents, because that is the batch planner's shape and it
/// is a superset of this CLI's.</b> <c>harness-run</c> has one binding; <c>harness-batch</c> merges every
/// lane's, and the area they share is one area — so a neighbour ANY lane declared is a neighbour of the
/// merged map, and reading only the first would silently drop one that a later lane knew about. Taking
/// the sequence here means the single-document caller is the degenerate case of the multi-document one
/// rather than a second treatment of it.</para>
///
/// <para>⚠️ <b>MALFORMED IS CARRIED THROUGH, NEVER FILTERED OUT.</b> Nothing here inspects
/// <see cref="ReservedRegion.Refusals"/>. A binding that declared a neighbour believes part of the area
/// is off limits, and dropping a typo'd declaration would hand that caller no protection while every
/// report stayed green — the same shape as the defect the guard exists for, one level up. The geometry
/// refuses it, loudly, by name.</para>
///
/// <para><b>What this cannot see</b> (<see cref="MirrorGeometry.ReservedRegions"/>): a neighbour nobody
/// wrote down. The collision this whole mechanism comes from would have happened just the same had the
/// field been left blank. An empty list is the ABSENCE of a claim, not the claim that the area is
/// otherwise empty, and the remedy for an undeclared neighbour is to declare it.</para>
///
/// <para>✅ <b>ONE DERIVATION, since <c>182b3f9</c>.</b> This paragraph used to name
/// <c>Harness.Batch.BatchPlanner.Plan</c> as an outstanding second copy — it was held by concurrent work
/// when this type was extracted, and has since been swapped onto <see cref="Of"/> and
/// <see cref="AppliedTo(MirrorGeometry, IEnumerable{BindingDocument})"/>. The three helper tests in
/// <c>ReservedRegionRunPathTests</c> pinned this type's semantics to exactly what that code did, and the
/// harness suite held at the same count across the swap, which is the evidence a behaviour-preserving
/// change owes.</para>
///
/// <para>⚠️ <b>Keep it that way, and the reason is measured rather than stylistic.</b> While the second
/// copy existed, <c>LoopCli.Compose</c> applied no reservations at all — so the guard bound on the batch
/// path and was <b>inert on every path through Compose</b>: <c>harness-run</c>, <c>Harness.Verify</c>, and
/// <c>harness-batch --merged</c>, the deploy path itself, which read a merged binding that carried the
/// reservation and dropped it. The rule is not the three lines it looks like: absent
/// <c>register</c>/<c>length</c> map to <c>-1</c>/<c>0</c> <b>so the region refuses itself</b> rather than
/// to <c>0</c>/<c>1</c>, which would read as a real band nobody typed; identical declarations collapse
/// while overlapping-but-different ones do not; and an empty set must never reach <c>Reserving</c>, which
/// throws. Each is invisible at a call site, which is why a copy of them was a defect waiting rather than
/// a duplication to tidy.</para>
/// </summary>
public static class DeclaredReservations
{
    /// <summary>
    /// Every reservation the given bindings declare, as domain regions — unioned, with identical
    /// declarations collapsed.
    ///
    /// <para>Two documents declaring OVERLAPPING-BUT-DIFFERENT regions do not collapse, and the geometry
    /// refuses the pair. That is correct rather than awkward: it means two authors disagree about what
    /// else lives in the area, and guessing which is right would be inventing the answer.</para>
    /// </summary>
    public static IReadOnlyList<ReservedRegion> Of(IEnumerable<BindingDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        return documents
            .SelectMany(d => d.ReservedRegions ?? new List<ReservedRegionDocument>())

            // 🔴 THE DEFAULTS ARE CHOSEN TO BE REFUSED, NOT TO BE PLAUSIBLE. An absent `register` becomes
            // -1 and an absent `length` becomes 0, both of which ReservedRegion.Refusals rejects by name.
            // Defaulting them to 0 and 1 would turn a misspelt key into a one-register band at the base of
            // the area that somebody would then read as a real declaration.
            .Select(r => new ReservedRegion(r.Register ?? -1, r.Length ?? 0, r.Owner ?? string.Empty))
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// <paramref name="geometry"/> with everything the bindings declare applied to it — or
    /// <paramref name="geometry"/> unchanged when they declare nothing.
    ///
    /// <para><b>The empty case does not call <see cref="MirrorGeometry.Reserving(ReservedRegion[])"/>,</b>
    /// which throws on an empty list precisely because such a call reads as "the neighbours are declared"
    /// and delivers nothing. Owning that branch here is why neither CLI has to remember it.</para>
    /// </summary>
    public static MirrorGeometry AppliedTo(MirrorGeometry geometry, IEnumerable<BindingDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var reserved = Of(documents);

        return reserved.Count == 0 ? geometry : geometry.Reserving(reserved.ToArray());
    }

    /// <summary>The single-binding case — <c>harness-run</c>'s, and nothing more than a sequence of one.</summary>
    public static MirrorGeometry AppliedTo(MirrorGeometry geometry, BindingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return AppliedTo(geometry, new[] { document });
    }
}
