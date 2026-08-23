using System.Text.Json;
using Harness.Loop;
using Harness.Run;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b><c>OwedOnTheDevice[0]</c> IS A HAND-MAINTAINED CONSTANT ASSERTING A FACT ABOUT HISTORY, AND GIT
/// PROVES IT CANNOT SELF-CORRECT.</b>
///
/// <para><b>The defect class, named.</b> It is the same one <c>F-3-authority</c> was: prose asserting a
/// system state, unable to move when the state moves. Commit <c>1b4d433</c> (<i>"three stale caveats
/// corrected"</i>) rewrote items 4 and 5 of this same array and left items 1 and 2 byte-identical —
/// a list that needs a person to notice is a list that goes stale twice.</para>
///
/// <para><b>And nothing emits it.</b> <c>git grep OwedOnTheDevice</c> finds the declaration, four
/// <c>.md</c> files, two XML-doc cross-references and one test asserting the strings are PRESENT. Its own
/// doc comment justified living on the result <i>"because a caveat somebody has to go and look up is a
/// caveat nobody reads"</i> — and it lives only in a source file and a test that pins it there.</para>
///
/// <para>🔴 <b>THE STRUCTURAL FIX IS TO REPLACE THE HISTORY CLAIM WITH AN OBSERVATION.</b> The loop is
/// HANDED its gateway, so which gateway deployed a run is knowable at construction time, per run, and
/// travels on the result — exactly the shape <c>WithFormAuthority</c> gave F-3. What no constant can
/// observe is whether the gateway has EVER run; that belongs to the tracked record, and the array now
/// points at it and dates its reading instead of asserting it flat.</para>
///
/// <para><b>Every positive below is paired with a negative</b>, because a caveat hardcoded to say
/// "deployed" would pass the happy-path test alone.</para>
/// </summary>
public class DeviceGatewayCaveatTests
{
    private const string CaveatId = "device-gateway";

    private static LoopCaveat Caveat(LoopResult result) =>
        Assert.Single(result.Caveats, c => c.Id == CaveatId);

    private static LoopResult Ran(SimulatedGateway? gateway = null) =>
        LoopRun.Execute(LoopRunTests.Request(), gateway ?? new SimulatedGateway(LoopRunTests.Geometry()));

    // -------------------------------------------------------------------------------------------------
    // 1. THE OBSERVATION — which gateway deployed THIS run, on THIS result
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE ACCEPTANCE TEST.</b> A run that reached the device boundary reports the gateway it was
    /// handed, by type, together with what that gateway said it did. <c>SimulatedGateway</c> is not a
    /// download and the result must name it rather than leave a reader to assume.
    /// </summary>
    [Fact]
    public void A_RUN_REPORTS_THE_GATEWAY_THAT_DEPLOYED_IT()
    {
        var detail = Caveat(Ran()).Detail;

        Assert.Contains(nameof(SimulatedGateway), detail, StringComparison.Ordinal);
        Assert.Contains("LOADED", detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The negative control that a hardcoded "deployed" would fail.</b> A gateway configured to refuse
    /// never loads anything, and the caveat has to say so — a constant reporting a successful deployment
    /// would pass the test above and be wrong here.
    /// </summary>
    [Fact]
    public void AND_A_GATEWAY_THAT_REFUSED_IS_REPORTED_AS_NOT_ATTEMPTED()
    {
        var refusing = new SimulatedGateway(LoopRunTests.Geometry()) { Refuse = true };
        var detail = Caveat(Ran(refusing)).Detail;

        Assert.Contains("NOT ATTEMPTED", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("LOADED", detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The second control: before the boundary, it claims nothing.</b> A run that stops above step 5 —
    /// the generate-and-stop path, and every pre-deployment refusal — must carry a caveat that says no
    /// deployment was attempted, not one that names a gateway it never called.
    /// </summary>
    [Fact]
    public void AND_BEFORE_THE_DEVICE_BOUNDARY_IT_NAMES_NO_GATEWAY_AT_ALL()
    {
        var caveats = LoopRun.Generate(LoopRunTests.Request(), stopWhenInadmissible: false).Caveats;
        var detail = Assert.Single(caveats, c => c.Id == CaveatId).Detail;

        Assert.Contains("NO DEPLOYMENT HAS BEEN ATTEMPTED", detail, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(SimulatedGateway), detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>It reaches the artifact.</b> The caveats array is what a reviewer reads weeks later, and the
    /// whole argument for deriving this rather than writing it down is that the derived value travels.
    /// </summary>
    [Fact]
    public void THE_CAVEAT_REACHES_THE_RUN_ARTIFACT()
    {
        var caveats = JsonDocument.Parse(LoopCli.Render(Ran())).RootElement.GetProperty("caveats");

        var emitted = Assert.Single(caveats.EnumerateArray()
            .Where(c => c.GetProperty("id").GetString() == CaveatId));

        Assert.Contains(nameof(SimulatedGateway), emitted.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------------------
    // 2. THE REGISTER STOPS ASSERTING WHAT IT CANNOT OBSERVE
    // -------------------------------------------------------------------------------------------------

    private static string Item(string marker) =>
        Assert.Single(LoopResult.OwedOnTheDevice, o => o.Contains(marker, StringComparison.Ordinal));

    /// <summary>
    /// 🔴 <b>The sentence that could not move.</b> Item 1 asserted, as a bare undated constant, that no
    /// step of the gateway <i>"HAS EVER BEEN EXECUTED"</i>. That is a claim about history, and no constant
    /// in this file can observe history — which is exactly how items 1 and 2 survived a commit that
    /// corrected items 4 and 5. Asserted against by name so re-introducing it fails here.
    /// </summary>
    [Fact]
    public void ITEM_1_NO_LONGER_ASSERTS_A_HISTORY_IT_CANNOT_OBSERVE()
    {
        Assert.DoesNotContain("NO STEP OF IT HAS EVER BEEN EXECUTED", Item("DEPLOYMENT ITSELF"), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>And it points at the two things that CAN answer the question</b> — the per-run caveat for this
    /// run, and the tracked record for every run. A pointer is what a claim becomes when the claim cannot
    /// be checked from where it is written.
    /// </summary>
    [Fact]
    public void ITEM_1_POINTS_AT_THE_DERIVED_CAVEAT_AND_AT_THE_TRACKED_RECORD()
    {
        var item = Item("DEPLOYMENT ITSELF");

        Assert.Contains(CaveatId, item, StringComparison.Ordinal);
        Assert.Contains("docs/notes/test-log.tsv", item, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE POINTER RESOLVES.</b> An item naming a caveat id that nothing emits is the same defect one
    /// level down, so the id is looked up in a real run rather than trusted. Rename the caveat and this
    /// goes red.
    /// </summary>
    [Fact]
    public void AND_THE_CAVEAT_ID_ITEM_1_NAMES_IS_ONE_A_RUN_ACTUALLY_EMITS()
    {
        Assert.Contains(CaveatId, Item("DEPLOYMENT ITSELF"), StringComparison.Ordinal);
        Assert.Equal(CaveatId, Caveat(Ran()).Id);
    }

    /// <summary>
    /// 🔴 <b>ITEM 2 MUST NOT BE DISCHARGED BECAUSE ITEM 1 LOOKS DISCHARGED.</b>
    ///
    /// <para>They sit next to each other and they are not the same question. The 2026-08-14 load manifest
    /// is recorded as first-hand from <c>DownloadResultAdapter</c> — the LIVE Openness object, reachable
    /// only in-process — and a gateway that shells out to a net48 binary cannot be in that process. So that
    /// event is evidence FOR the probe and AGAINST this item, and the item now says so where the next
    /// reader will meet it.</para>
    /// </summary>
    [Fact]
    public void ITEM_2_WARNS_AGAINST_BEING_DISCHARGED_ALONGSIDE_ITEM_1()
    {
        var item = Item("THE LOAD MANIFEST");

        Assert.Contains("DownloadResultAdapter", item, StringComparison.Ordinal);
        Assert.Contains("separate PROCESS", item, StringComparison.Ordinal);
        Assert.Contains("DO NOT DISCHARGE THIS BECAUSE ITEM 1", item, StringComparison.Ordinal);
    }
}
