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
/// <para>🔴 <b>ONE CALL SITE HAS NOT BEEN MOVED ONTO THIS YET, AND IT IS NAMED HERE RATHER THAN LEFT TO
/// BE FOUND: <c>Harness.Batch.BatchPlanner.Plan</c>.</b> It still open-codes the identical union at its
/// own lines around 205–212 — the file was held by concurrent work when this was extracted, so the swap
/// (<c>DeclaredReservations.AppliedTo(geometry, documents.Select(d =&gt; d.Binding))</c>, replacing both the
/// <c>SelectMany/Select/Distinct</c> and the <c>if (reserved.Length &gt; 0)</c> guard) is outstanding. The
/// three helper tests in <c>ReservedRegionRunPathTests</c> pin this type's semantics to exactly what that
/// code does today, so the swap is behaviour-preserving; until it happens <b>this rule has two
/// derivations, which is the thing this repository refuses.</b></para>
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
