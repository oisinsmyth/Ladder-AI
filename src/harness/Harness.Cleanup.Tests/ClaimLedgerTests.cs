namespace Harness.Cleanup.Tests;

/// <summary>Spec §16.12c's half: what a removal would strand, and what "nothing" is allowed to mean.</summary>
public class ClaimLedgerTests
{
    private const string Store = "C:\\\\ProgramData\\\\Ladder-AI\\\\claims\\\\test-project001";

    private static string Json(string claims) =>
        $"{{\"project\":\"ir/p\",\"store\":\"{Store}\",\"claims\":{claims},\"conflicts\":[],\"fulfilled\":[],\"stale\":[]}}";

    [Fact]
    public void The_STORE_PATH_is_read_from_the_document_and_kept()
    {
        // Passing …\claims\<project> where the ROOT was wanted yields a second empty store with the same
        // name that grants every claim, and two parties making that mistake agree perfectly. The document
        // reports the path the converter actually resolved — the artifact, not anyone's argument.
        var ledger = ClaimLedger.Parse(Json("[]"), "claims.json");

        Assert.Contains("test-project001", ledger.StorePath, StringComparison.Ordinal);
    }

    [Fact]
    public void A_document_with_no_STORE_field_is_refused()
    {
        var ex = Assert.Throws<CleanupInputException>(() =>
            ClaimLedger.Parse("{\"claims\":[]}", "claims.json"));

        Assert.Contains("store", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_document_with_no_CLAIMS_array_is_refused_rather_than_read_as_an_empty_store()
    {
        var ex = Assert.Throws<CleanupInputException>(() =>
            ClaimLedger.Parse("{\"store\":\"x\"}", "claims.json"));

        Assert.Contains("claims", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_claim_on_the_TYPE_PREFIXED_block_number_is_found()
    {
        var ledger = ClaimLedger.Parse(
            Json("[{\"kind\":\"block-number\",\"value\":\"FB9001\",\"agent\":\"lane-a\",\"purpose\":\"a new FB\",\"created\":\"2026-08-17T00:00:00Z\"}]"),
            "claims.json");

        var held = ledger.For(new IrObject("FB_X", IrObjectKind.Block, "FB", 9001, null, "FB_X.ir"));

        Assert.Equal("lane-a", Assert.Single(held).Agent);
    }

    [Fact]
    public void A_claim_on_the_OBJECT_NAME_is_found_too_because_that_is_how_block_edit_is_spelled()
    {
        var ledger = ClaimLedger.Parse(
            Json("[{\"kind\":\"block-edit\",\"value\":\"FB_X\",\"agent\":\"lane-b\",\"purpose\":\"editing\",\"created\":\"t\"}]"),
            "claims.json");

        Assert.Single(ledger.For(new IrObject("FB_X", IrObjectKind.Block, "FB", 9001, null, "FB_X.ir")));
    }

    [Fact]
    public void An_UNRELATED_claim_is_not_matched()
    {
        // The over-firing direction. Matching is deliberately broad, but a rule that matched everything
        // would emit a release command for somebody else's reservation.
        var ledger = ClaimLedger.Parse(
            Json("[{\"kind\":\"block-number\",\"value\":\"FB9500\",\"agent\":\"lane-a\",\"purpose\":\"p\",\"created\":\"t\"}]"),
            "claims.json");

        Assert.Empty(ledger.For(new IrObject("FB_X", IrObjectKind.Block, "FB", 9001, null, "FB_X.ir")));
    }

    [Fact]
    public void An_EMPTY_STORE_yields_no_claims_which_is_a_DIFFERENT_fact_from_no_store_supplied()
    {
        // ClaimDisposition keeps them apart; this pins that the empty case really is reachable and really
        // is empty, so the CLI's NotChecked cannot quietly become the same value.
        Assert.Empty(ClaimLedger.Parse(Json("[]"), "claims.json").Claims);
        Assert.NotEqual(ClaimDisposition.NoClaimHeld, ClaimDisposition.NotChecked);
        Assert.Equal(ClaimDisposition.Unstated, default(ClaimDisposition));
    }
}
