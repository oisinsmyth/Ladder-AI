using Ladder.Wave;

namespace Converter.Claims;

/// <summary>Where a block number sits relative to the X-J reserved harness band.</summary>
public enum BandPosition
{
    /// <summary>The band does not cover this number space at all — OB, per the carve-out.</summary>
    SpaceNotCovered,

    /// <summary>Outside the band, in a space the band covers. Deliverable territory.</summary>
    Outside,

    /// <summary>Inside the band, in a space the band covers. Harness territory.</summary>
    Inside,
}

/// <summary>
/// X-J's ENFORCING HALF: the reserved band, applied to `converter claim`.
///
/// <para>The spec's treatment reads <i>"a NUMBER RANGE IS RESERVED for harness-generated objects
/// <b>and the claim tool refuses allocations inside it</b>"</i>. The range existed
/// (<see cref="HarnessNumberRange"/>, declared 9000–9999 per space for FB/FC/DB, OB excluded);
/// *** THE CLAIM TOOL KNEW NOTHING ABOUT IT AND WOULD ALLOCATE INSIDE IT WITHOUT COMMENT. ***
/// This is the half that was missing.</para>
///
/// <para>🔴 <b>THE BAND IS NOT DECLARED HERE.</b> It is read from
/// <see cref="HarnessNumberRange.Declared"/> — the one place the ruling names, overturnable at the one
/// line the ruling says. A second copy agrees until the day it does not, and this repo had briefly
/// grown one.</para>
///
/// <para><b>WHY, MEASURED RATHER THAN FEARED:</b> TIA accepted an import declaring <c>FC 910</c> while
/// another block already held 910 and created two blocks at that number — import exit 0, per-block
/// compile exit 0, device compile <c>Success</c>, <c>sanity-check OVERALL: HEALTHY</c>. The
/// hard-rule-4 gate passed green over a duplicate.</para>
///
/// <para>*** WHAT IS ENFORCED, AND IT NEEDS NO JUDGEMENT ABOUT WHO IS ASKING: ***</para>
/// <list type="number">
/// <item><b>A plain <c>--allocate</c> CANNOT return a band number.</b> The band is REMOVED from the
/// candidate set — not deprioritised, not preferred against. This is X-J's named mechanism, and it is
/// unconditional because it does not need to know anything about the caller.</item>
/// <item><b>A band allocation is CONFINED to the band.</b> When <c>--floor</c> puts the search inside
/// the reserved range (<see cref="HarnessNumberRange.AllocationFloor"/> is what feeds it), the search
/// stops at the band's last number and <b>refuses naming exhaustion</b>. *** WALKING PAST 9999 INTO
/// UNRESERVED SPACE IS THE FAILURE MODE *** — it hands a harness generator a deliverable number while
/// every check stays green, which is the `FC 910` collision arriving by a different road.</item>
/// </list>
///
/// <para>*** WHAT IS NOT ENFORCED, AND WHY A FLAG WOULD HAVE BEEN WORSE THAN THE GAP. *** An explicit
/// <c>--value</c> inside the band is ACCEPTED, recorded and REPORTED as a band claim rather than
/// refused. Two reasons, both measured rather than assumed:</para>
/// <list type="bullet">
/// <item><b>Nothing at claim time can derive harness-ness.</b> A block-number claim is an ALLOCATION:
/// the block does not exist yet — <see cref="ClaimValidator"/> refuses the claim outright if it does.
/// So there is no artifact, and <see cref="Review.HarnessScope"/>'s derivation, which reads a block's
/// number out of its own IR, has nothing to read. A <c>--harness</c> switch would close the gap in
/// appearance only: it is a caller assertion, and a caller assertion is forgotten exactly when it
/// matters.</item>
/// <item><b>Refusing it would break X-J's own interoperation point.</b>
/// <see cref="HarnessNumberRange.ClaimArgumentsFor"/> renders <c>claim … --value FC9001</c> — the
/// harness ledger allocates within the band and then records that number here. Refusing explicit
/// in-band values would make the harness unable to register its own allocations, which is the
/// collision the band exists to prevent, reintroduced by the fence.</item>
/// </list>
///
/// <para>⚠️ *** AND THE VERIFICATION HALF IS NOT IMPLEMENTABLE FROM THIS DERIVATION — STATED HERE SO
/// THE NEXT READER DOES NOT ADD IT AS AN OBVIOUS OMISSION. *** The tempting closure is "once the block
/// exists, classify it and check the claim against it". It cannot work:
/// <see cref="Review.HarnessScope"/> decides harness-ness <b>by reading this same band</b>, so the
/// comparison reduces to <c>band(n) == band(n)</c> — a tautology that could not fire on any input, in
/// either direction. *** A CHECK THAT SHARES ITS SUBJECT'S BLIND SPOT IS NOT A CHECK ***, and a guard
/// that is correct, wired in and unfalsifiable in place is one of this project's named failure modes.
/// Closing it needs an authority that classifies a block by something OTHER than its number — the
/// harness ledger's own record of what it generated would be one — and no such authority is reachable
/// from the converter today.</para>
///
/// <para><b>Prevention where prevention is possible; the gap named where it is not.</b> An honest gap
/// beats a bypass, and a reported in-band claim is attributable where a silent one is not.</para>
/// </summary>
public static class ReservedBand
{
    /// <summary>The one declaration, consumed — never restated.</summary>
    public static HarnessNumberRange Declared { get; } = HarnessNumberRange.Declared();

    public static BandPosition PositionOf(string numberSpace, int number)
    {
        if (!Declared.CoversSpace(numberSpace))
        {
            return BandPosition.SpaceNotCovered;
        }

        return Declared.ContainsNumber(number) ? BandPosition.Inside : BandPosition.Outside;
    }

    /// <summary>
    /// TRUE when a search starting at <paramref name="floor"/> is a BAND allocation — i.e. the caller
    /// aimed the search into the reserved range.
    ///
    /// <para>Deliberately a property of the RANGE REQUESTED, never a boolean about the caller's
    /// identity: a floor is checkable against the declaration, where <i>"I am the harness"</i> is
    /// checkable against nothing. It also means a deliverable agent cannot reach the band by accident
    /// — only by naming a floor inside a range this tool will then say, in the outcome text, is
    /// reserved and by whom.</para>
    /// </summary>
    public static bool IsBandAllocation(string numberSpace, int floor) =>
        Declared.CoversSpace(numberSpace) && Declared.ContainsNumber(floor);

    /// <summary>The one-line description used in every refusal, so a reason is always attributable.</summary>
    public static string Describe() => Declared.Describe();
}
