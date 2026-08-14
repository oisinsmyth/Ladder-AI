using Converter.Claims;
using Converter.Ir;
using Converter.Review;
using Converter.SimaticMl;
using Ladder.Wave;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 X-J's MISSING HALF (2026-08-14). The spec's treatment reads <i>"a NUMBER RANGE IS RESERVED for
/// harness-generated objects <b>and the claim tool refuses allocations inside it</b>"</i>. The range
/// existed — <see cref="HarnessNumberRange.Declared"/>, 9000–9999 per space for FB/FC/DB with OB
/// excluded — and *** `claim --allocate` HAD NO KNOWLEDGE OF IT AND WOULD ALLOCATE INSIDE IT WITHOUT
/// COMMENT. ***
///
/// <para><b>Why it matters, measured rather than feared:</b> TIA accepted an import declaring
/// <c>FC 910</c> while another block already held 910 and created two blocks at that number, with
/// import, per-block compile, device compile and <c>sanity-check</c> all green. On the hammer campaign
/// several agents allocate concurrently against one shared claims dir, and the band is what stops a
/// harness object colliding with a deliverable.</para>
///
/// <para>*** BOTH DIRECTIONS, THROUGHOUT. *** A fence that refuses everything passes every test that
/// only checks refusals, so every refusal below is paired with a legitimate case that must still
/// succeed — above all <b>a harness object taking 9000</b>.</para>
/// </summary>
public class ClaimReservedBandTests : IDisposable
{
    private readonly string _corpusDir = ClaimsTestCorpus.Create();
    private readonly string _claimsRoot = ClaimsTestCorpus.CreateClaimsRoot();

    public void Dispose() => ClaimsTestCorpus.Delete(_corpusDir, _claimsRoot);

    private ClaimOutcome Allocate(string type, int floor = 0, string agent = "a1") =>
        ClaimsRunner.Allocate(
            ClaimCorpus.Build(_corpusDir), new ClaimStore(_claimsRoot, _corpusDir), _corpusDir,
            ClaimKind.BlockNumber, type, floor, null, agent, "test");

    private ClaimOutcome Acquire(string value, string agent = "a1") =>
        ClaimsRunner.Acquire(
            ClaimCorpus.Build(_corpusDir), new ClaimStore(_claimsRoot, _corpusDir), _corpusDir,
            ClaimKind.BlockNumber, value, agent, "test");

    private static int NumberOf(ClaimOutcome outcome) =>
        ClaimValidator.BlockNumberParts(outcome.Claim!.Value).Number;

    // Fills a whole number space's band so exhaustion can be reached without 1000 real blocks: every
    // band number is CLAIMED by another agent, which is one of the two ways a candidate is unusable.
    private void ClaimWholeBand(string type, string byAgent)
    {
        var store = new ClaimStore(_claimsRoot, _corpusDir);
        var corpus = ClaimCorpus.Build(_corpusDir);
        for (var n = ReservedBand.Declared.FirstNumber; n <= ReservedBand.Declared.LastNumber; n++)
        {
            ClaimsRunner.Acquire(corpus, store, _corpusDir, ClaimKind.BlockNumber, $"{type}{n}", byAgent, "fill");
        }
    }

    // ---- The band is declared ONCE ---------------------------------------------------------------

    // *** TWO DECLARATIONS OF ONE BAND IS HOW THEY DIVERGE. *** The converter must not restate the
    // numbers; it must read the ruling's own object. This test fails the moment somebody reintroduces
    // a local constant that happens to agree today.
    [Fact]
    public void TheBandIsReadFromTheOneDeclaration_NotRestated()
    {
        Assert.Same(HarnessNumberRange.Declared().GetType(), ReservedBand.Declared.GetType());
        Assert.Equal(HarnessNumberRange.Declared().FirstNumber, ReservedBand.Declared.FirstNumber);
        Assert.Equal(HarnessNumberRange.Declared().LastNumber, ReservedBand.Declared.LastNumber);

        // HarnessScope (the review-side consumer) reads the same object rather than its own copy.
        Assert.Equal(ReservedBand.Declared.FirstNumber, HarnessScope.ReservedBandLow);
        Assert.Equal(ReservedBand.Declared.LastNumber, HarnessScope.ReservedBandHigh);
    }

    [Fact]
    public void TheDeclarationCoversFbFcDb_AndExcludesOb()
    {
        Assert.True(ReservedBand.Declared.CoversSpace("FB"));
        Assert.True(ReservedBand.Declared.CoversSpace("FC"));
        Assert.True(ReservedBand.Declared.CoversSpace("DB"));
        Assert.False(ReservedBand.Declared.CoversSpace("OB"));
    }

    // ---- Prevention: a plain --allocate CANNOT return a band number -------------------------------

    [Theory]
    [InlineData("FB")]
    [InlineData("FC")]
    [InlineData("DB")]
    public void PlainAllocate_NeverReturnsABandNumber(string type)
    {
        var outcome = Allocate(type);

        Assert.True(outcome.Ok, outcome.Reason);
        Assert.Equal(BandPosition.Outside, ReservedBand.PositionOf(type, NumberOf(outcome)));
    }

    // *** THE ONE THAT PROVES IT IS "CANNOT" AND NOT "PREFERS NOT TO". *** With the floor set just
    // below the band, the search would walk straight into it. It must step OVER the band, not stop at
    // it and not enter it — and it must land on the first number PAST the band, not merely on
    // something outside.
    [Fact]
    public void AllocateWithAFloorJustBelowTheBand_StepsOverIt()
    {
        var justBelow = ReservedBand.Declared.FirstNumber - 1;
        var store = new ClaimStore(_claimsRoot, _corpusDir);
        var corpus = ClaimCorpus.Build(_corpusDir);

        // Take the one number below the band so the search is forced to continue into it.
        Assert.True(ClaimsRunner.Acquire(corpus, store, _corpusDir, ClaimKind.BlockNumber, $"FC{justBelow}", "other", "fill").Ok);

        var outcome = Allocate("FC", floor: justBelow, agent: "a1");

        Assert.True(outcome.Ok, outcome.Reason);
        Assert.Equal(ReservedBand.Declared.LastNumber + 1, NumberOf(outcome));
    }

    // A floor ABOVE the band is left alone — the skip must not displace numbers nobody asked about.
    [Fact]
    public void AllocateWithAFloorAboveTheBand_IsUnaffected()
    {
        var above = ReservedBand.Declared.LastNumber + 1;

        var outcome = Allocate("FC", floor: above);

        Assert.True(outcome.Ok, outcome.Reason);
        Assert.Equal(above, NumberOf(outcome));
    }

    // ---- The band is REACHABLE, which is the direction a refuse-everything fence would fail --------

    // *** A HARNESS OBJECT LEGITIMATELY TAKING 9000 MUST STILL SUCCEED. ***
    [Fact]
    public void AllocateWithTheBandFloor_TakesTheFirstBandNumber()
    {
        var outcome = Allocate("FC", floor: ReservedBand.Declared.AllocationFloor);

        Assert.True(outcome.Ok, outcome.Reason);
        Assert.Equal(ReservedBand.Declared.FirstNumber, NumberOf(outcome));
        Assert.Equal(BandPosition.Inside, ReservedBand.PositionOf("FC", NumberOf(outcome)));
    }

    // And an explicit in-band value — the form HarnessNumberRange.ClaimArgumentsFor renders — succeeds.
    // Refusing it would leave the harness ledger unable to record its own allocations, which is the
    // collision the band exists to prevent, reintroduced by the fence.
    [Fact]
    public void AnExplicitInBandValue_IsAcceptedAndSaysSo()
    {
        var outcome = Acquire($"FC{ReservedBand.Declared.FirstNumber}");

        Assert.True(outcome.Ok, outcome.Reason);
        Assert.Contains("INSIDE the reserved harness band", outcome.Reason);
        Assert.Contains("Accepted, not verified", outcome.Reason);
    }

    // The converse: an ordinary claim must NOT be decorated with the band note, or the note becomes
    // noise on every line and stops being read.
    [Fact]
    public void AnOrdinaryClaim_CarriesNoBandNote()
    {
        var outcome = Acquire("FC120");

        Assert.True(outcome.Ok, outcome.Reason);
        Assert.DoesNotContain("reserved harness band", outcome.Reason);
    }

    // ---- Exhaustion: a refusal, never a quiet step outside ----------------------------------------

    // *** SILENTLY ALLOCATING OUTSIDE THE BAND IS THE FAILURE MODE. *** A search that ran past 9999
    // would hand a harness generator a deliverable number with every downstream check still green.
    [Fact]
    public void AnExhaustedBand_RefusesByNameAndDoesNotEscapeTheBand()
    {
        ClaimWholeBand("FC", byAgent: "other");

        var outcome = Allocate("FC", floor: ReservedBand.Declared.AllocationFloor, agent: "a1");

        Assert.False(outcome.Ok);
        Assert.Equal(ClaimResult.BandExhausted, outcome.Result);
        Assert.Null(outcome.Claim);
        Assert.Contains("RESERVED HARNESS BAND IS EXHAUSTED", outcome.Reason);
        Assert.Contains("did NOT continue past its end", outcome.Reason);
    }

    // Exhaustion is its OWN result, not HeldByAnother — the two demand opposite responses, and
    // "someone got there first" invites the retry at a higher floor that must never happen here.
    [Fact]
    public void BandExhaustion_IsADistinctResultFromLosingARace()
    {
        ClaimWholeBand("FB", byAgent: "other");

        var exhausted = Allocate("FB", floor: ReservedBand.Declared.AllocationFloor, agent: "a1");
        Assert.Equal(ClaimResult.BandExhausted, exhausted.Result);

        // A DELIVERABLE allocation over the same filled band is unaffected: the band is not its pool.
        var deliverable = Allocate("FB", agent: "a1");
        Assert.True(deliverable.Ok, deliverable.Reason);
        Assert.Equal(BandPosition.Outside, ReservedBand.PositionOf("FB", NumberOf(deliverable)));
    }

    // ---- The OB carve-out, in both directions -----------------------------------------------------

    // *** OB IS EXCLUDED AND THE RULE BREAKS ON ITS OWN FIRST EXAMPLE WITHOUT IT. *** An OB's number is
    // fixed by its event class, and the spec names OB80 as a harness object. Applying the band to OBs
    // would emit a false finding on a correct project — and the first false finding is what gets a
    // check switched off.
    [Fact]
    public void ObAllocation_IsNotFencedAtAll_BecauseTheBandCoversNoObNumber()
    {
        var outcome = Allocate("OB", floor: ReservedBand.Declared.FirstNumber);

        Assert.True(outcome.Ok, outcome.Reason);
        // It lands exactly where a plain search would — no skip, no confinement, no exhaustion rule.
        Assert.Equal(ReservedBand.Declared.FirstNumber, NumberOf(outcome));
        Assert.Equal(BandPosition.SpaceNotCovered, ReservedBand.PositionOf("OB", NumberOf(outcome)));
    }

    [Fact]
    public void AnObClaimInsideTheBandsNumbers_CarriesNoBandNote()
    {
        var outcome = Acquire($"OB{ReservedBand.Declared.FirstNumber}");

        Assert.True(outcome.Ok, outcome.Reason);
        Assert.DoesNotContain("reserved harness band", outcome.Reason);
    }

    // ---- Other kinds are untouched ----------------------------------------------------------------

    // The band is about BLOCK NUMBERS. A gate that fires on cases outside its scope is noise, and
    // noise gets switched off — after which the cases it was right about go through unchecked.
    [Fact]
    public void ANonBlockNumberAllocation_IsUnaffected()
    {
        var outcome = ClaimsRunner.Allocate(
            ClaimCorpus.Build(_corpusDir), new ClaimStore(_claimsRoot, _corpusDir), _corpusDir,
            ClaimKind.BlockNetwork, null, 0, ClaimsTestCorpus.SharedBlock, "a1", "test");

        Assert.True(outcome.Ok, outcome.Reason);
        Assert.Equal($"{ClaimsTestCorpus.SharedBlock}:8", outcome.Claim!.Value);
        Assert.DoesNotContain("reserved harness band", outcome.Reason);
    }

    // ---- The corpus is still honoured inside the band ---------------------------------------------

    // A band allocation still skips numbers the project already uses — the band changes WHERE the
    // search looks, never whether "free" means free.
    [Fact]
    public void ABandAllocation_StillSkipsANumberTheCorpusAlreadyUses()
    {
        var first = ReservedBand.Declared.FirstNumber;
        File.WriteAllText(
            Path.Combine(_corpusDir, "FC_Harness.ir"),
            IrSerializer.SerializeBlockReadable(new IrBlock(
                "0", "FC", "FC_Harness", first, "LAD", "already holds the first band number",
                new[] { new IrNetwork(1, "Only", Array.Empty<CoilAssignment>()) })));

        var outcome = Allocate("FC", floor: ReservedBand.Declared.AllocationFloor);

        Assert.True(outcome.Ok, outcome.Reason);
        Assert.Equal(first + 1, NumberOf(outcome));
    }
}
