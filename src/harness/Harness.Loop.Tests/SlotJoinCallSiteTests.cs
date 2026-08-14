using Harness.Results;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE SLOT JOIN'S CALL SITE — and the assertion that matters is <c>Deployments == 0</c>, not the
/// outcome value.</b>
///
/// <para><see cref="SlotJoin"/> is a decision procedure, unit-tested where it lives. What it could not
/// establish from there is the only property it exists for: <b>that the refusal happens BEFORE the
/// download</b>. <i>A disconnected check can still produce a non-<c>Ran</c> outcome by accident</i> — the
/// old behaviour did exactly that, reaching step 7 and throwing — so a test keyed on the outcome alone
/// would have passed against the defect.</para>
///
/// <para><b>Measured 2026-08-14, and the exception was never the defect — the POSITION was.</b> A vector
/// naming an unbound slot passed map derivation, the submission gate, the width check, copy-layer
/// generation and the 0.1b retention assertion; the program was <b>DEPLOYED</b>; the version register was
/// <b>CONFIRMED</b>; and only then did <c>SlotIndexOf</c> throw
/// <c>ArgumentException: no slot 'X' in this map</c> — with the gateway recording
/// <c>Deployments=1, Opens=1</c> already spent.</para>
/// </summary>
public class SlotJoinCallSiteTests
{
    private static LoopRequest UnboundSlot(string slot = "SLOT-HBA-RAISE")
    {
        var request = LoopRunTests.Request();
        return request with { Vectors = new[] { request.Vectors[0] with { Slot = slot } } };
    }

    [Fact]
    public void A_VECTOR_NAMING_AN_UNBOUND_SLOT_IS_REFUSED_BEFORE_ANYTHING_IS_DEPLOYED()
    {
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());

        var result = LoopRun.Execute(UnboundSlot(), gateway);

        Assert.Equal(LoopOutcome.NotBound, result.Outcome);

        // *** THE TWO THAT MATTER. *** They are the observable consequence of the check being in the
        // right PLACE; the outcome above only says it fired.
        Assert.Equal(0, gateway.Deployments);
        Assert.Equal(0, gateway.Opens);

        Assert.Empty(result.Packages);
    }

    [Fact]
    public void THE_REFUSAL_NAMES_BOTH_SIDES_because_the_reader_must_decide_which_document_is_wrong()
    {
        var result = LoopRun.Execute(UnboundSlot(), new SimulatedGateway(LoopRunTests.Geometry()));

        Assert.Contains("SLOT-HBA-RAISE", result.Detail, StringComparison.Ordinal);
        Assert.Contains("V-1", result.Detail, StringComparison.Ordinal);

        // The BOUND side too: half the picture cannot settle which of two documents is stale.
        Assert.Contains("'S0'", result.Detail, StringComparison.Ordinal);
        Assert.Contains("Nothing was generated and nothing was deployed", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_MATCHING_SLOT_IS_UNTOUCHED_and_still_deploys_and_runs()
    {
        // *** THE UNAFFECTED CASE, ASSERTED AS DELIBERATELY AS THE REFUSAL. *** A join that refuses
        // everything passes every test that only looks for a refusal, and a gate firing outside its scope
        // is noise — after which the cases it was right about go through unchecked.
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());

        var result = LoopRun.Execute(LoopRunTests.Request(), gateway);

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.Equal(1, gateway.Deployments);
        Assert.NotEmpty(result.Packages);
    }

    [Fact]
    public void A_BOUND_SLOT_WITH_NO_VECTORS_IS_NOT_A_REFUSAL_because_the_check_is_ONE_SIDED()
    {
        // The converse direction of the scope: an EXCISED or deliberately inert slot (D32) is a path the
        // design provides, and refusing it here would refuse a legitimate wave set. Two bound slots, one
        // vector — the second slot carries nothing and the run still proceeds.
        var request = LoopRunTests.Request();

        var extended = request with
        {
            Slots = request.Slots.Append(new Harness.Map.SlotRequest("S1", 2, 2)).ToArray(),
            Bindings = request.Bindings.Append(Harness.Skeleton.TrivialBlock.Binding("S1")).ToArray(),
        };

        var result = LoopRun.Execute(extended, new SimulatedGateway(LoopRunTests.Geometry()));

        Assert.NotEqual(LoopOutcome.NotBound, result.Outcome);
    }

    [Fact]
    public void THE_MATCH_IS_ORDINAL_because_a_slot_id_is_a_key_and_not_a_word()
    {
        // A case difference is a DIFFERENT key. Silently accepting it here would make the loop's join
        // laxer than the one SlotJoin's own tests pin, which is the divergence this call site exists to
        // avoid having two of.
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());

        Assert.Equal(LoopOutcome.NotBound, LoopRun.Execute(UnboundSlot("s0"), gateway).Outcome);
        Assert.Equal(0, gateway.Deployments);
    }
}
