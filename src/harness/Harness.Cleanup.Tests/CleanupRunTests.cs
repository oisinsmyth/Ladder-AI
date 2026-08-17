namespace Harness.Cleanup.Tests;

/// <summary>
/// The entry point end to end, over real files.
///
/// <para><b>The two directions are tested as deliberately as each other.</b> A gate that refuses every
/// ordinary run is noise, and noise gets switched off — after which the cases it was right about go
/// through unchecked. So for every refusal here there is a run that MUST succeed and MUST find
/// something, and the "nothing is eligible" verdict is never allowed to be the only outcome this suite
/// can produce.</para>
/// </summary>
public class CleanupRunTests
{
    private static CleanupOptions Options(
        TempCorpus corpus,
        string? crossCheck = null,
        string? drain = null,
        string authority = "lane-c, DB-7 cadence",
        IReadOnlyList<string>? artifacts = null,
        string? claims = null,
        IReadOnlyList<string>? models = null) =>
        new(corpus.Ir, crossCheck, artifacts ?? new[] { corpus.File_("vectors.json", "{}") },
            drain, authority, claims, models ?? Array.Empty<string>());

    // ---------------------------------------------------------------------------------------------
    // The positive direction — the tool must be able to find something, or every zero it prints is
    // unfalsifiable
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void An_ORPHANED_HARNESS_BLOCK_IS_FOUND_ELIGIBLE_with_the_command_to_remove_it()
    {
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Live", 9001).Fc("FC_Orphan", 9098).Fc("FC_Deliverable", 2);
        var xc = corpus.CrossCheck("xc.json", "[{\"block\":\"Main\",\"calls\":[\"FB_Live\"],\"instanceDbRoots\":[]}]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport()));

        Assert.Equal(CleanupExit.Planned, outcome.Exit);
        Assert.Contains("REMOVE Block 'FC_Orphan'", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("authority: lane-c, DB-7 cadence", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("openness-cli delete <project> --block FC_Orphan --yes", outcome.Text, StringComparison.Ordinal);

        // ...and it did NOT propose the one thing that is called, nor the deliverable outside the range.
        Assert.DoesNotContain("REMOVE Block 'FB_Live'", outcome.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("FC_Deliverable", outcome.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_BLOCK_IN_NO_TEST_AT_ALL_IS_STILL_RETAINED_WHEN_ANOTHER_BLOCK_CALLS_IT()
    {
        // DB-7 names this case outright, and it is the one an age- or completion-based rule gets wrong.
        using var corpus = new TempCorpus();
        corpus.Fb("FB_NoVectorMentionsMe", 9010);
        var xc = corpus.CrossCheck("xc.json", "[{\"block\":\"Main\",\"calls\":[\"FB_NoVectorMentionsMe\"],\"instanceDbRoots\":[]}]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport()));

        Assert.Equal(CleanupExit.Planned, outcome.Exit);
        Assert.Contains("Referenced", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("<- called by Main", outcome.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("REMOVE", outcome.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_FB_whose_INSTANCE_DB_still_exists_is_RETAINED_and_the_iDB_goes_FIRST()
    {
        // Cleanup converges across batches, in the only order TIA will accept.
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Orphan", 9099).InstanceDb("iDB_Orphan", 9099, "FB_Orphan");
        var xc = corpus.CrossCheck("xc.json", "[]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport()));

        Assert.Contains("REMOVE InstanceDb 'iDB_Orphan'", outcome.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("REMOVE Block 'FB_Orphan'", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("<- instantiated by iDB_Orphan", outcome.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_DECLARED_MODEL_is_recorded_as_a_Model_and_not_as_a_Block()
    {
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Stim", 9002);
        var xc = corpus.CrossCheck("xc.json", "[]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport(), models: new[] { "FB_Stim" }));

        Assert.Contains("REMOVE Model 'FB_Stim'", outcome.Text, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The refusals, each routed through the library rather than shadowed by an argument check
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void With_NO_DRAIN_REPORT_the_LIBRARY_refuses_and_the_shim_reports_ITS_verdict()
    {
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Orphan", 9001);
        var xc = corpus.CrossCheck("xc.json", "[]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, drain: null));

        Assert.Equal(CleanupExit.Refused, outcome.Exit);
        Assert.Contains("DRAINSTATEUNKNOWN", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("Unknown is not drained", outcome.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("REMOVE", outcome.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void With_NO_CROSS_CHECK_eligibility_is_provable_for_nothing()
    {
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Orphan", 9001);

        var outcome = CleanupRun.Execute(Options(corpus, crossCheck: null, drain: corpus.DrainedReport()));

        Assert.Equal(CleanupExit.Refused, outcome.Exit);
        Assert.Contains("GRAPHNOTAVAILABLE", outcome.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("REMOVE", outcome.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_TEST_STILL_IN_FLIGHT_stops_the_batch_WHATEVER_the_graph_says()
    {
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Orphan", 9001);
        var xc = corpus.CrossCheck("xc.json", "[]");
        var busy = corpus.File_("drain.txt", "format=1\ncomputed-by=x\ncomputed-at=t\nin-flight=1\ntest=V-7\nend\n");

        var outcome = CleanupRun.Execute(Options(corpus, xc, busy));

        Assert.Equal(CleanupExit.TestsNotDrained, outcome.Exit);
        Assert.Contains("V-7", outcome.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("REMOVE", outcome.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void With_NO_TEST_ARTIFACT_the_run_is_refused_because_every_model_would_read_as_unreferenced()
    {
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Orphan", 9001);
        var xc = corpus.CrossCheck("xc.json", "[]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport(), artifacts: Array.Empty<string>()));

        Assert.Equal(CleanupExit.Refused, outcome.Exit);
        Assert.Contains("not a smaller answer, it is a wrong one", outcome.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_input_that_was_NAMED_but_does_not_exist_is_a_different_fact_from_one_not_named()
    {
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Orphan", 9001);

        var outcome = CleanupRun.Execute(Options(corpus, Path.Combine(corpus.Root, "absent.json"), corpus.DrainedReport()));

        Assert.Equal(CleanupExit.Refused, outcome.Exit);
        Assert.Contains("wrong path rather than an omission", outcome.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_MODEL_declaration_that_lands_on_NOTHING_is_a_hard_error()
    {
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Stim", 9002);
        var xc = corpus.CrossCheck("xc.json", "[]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport(), models: new[] { "FB_Typo" }));

        Assert.Equal(CleanupExit.Usage, outcome.Exit);
        Assert.Contains("FB_Typo", outcome.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void With_NO_AUTHORITY_nothing_is_removed_even_when_the_graph_is_clean()
    {
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Orphan", 9001);
        var xc = corpus.CrossCheck("xc.json", "[]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport(), authority: "  "));

        Assert.Equal(CleanupExit.Planned, outcome.Exit);
        Assert.Contains("NoAuthorityRecorded", outcome.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("REMOVE", outcome.Text, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Empty is not clean
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_corpus_with_NOTHING_HARNESS_OWNED_exits_NON_ZERO_and_prints_its_denominator()
    {
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Deliverable", 7).Fc("FC_Deliverable", 2);
        var xc = corpus.CrossCheck("xc.json", "[]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport()));

        Assert.Equal(CleanupExit.NothingExamined, outcome.Exit);
        Assert.Contains("NOTHING EXAMINED - this is not a pass", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("objects examined                          2", outcome.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_EMPTY_DIRECTORY_exits_NON_ZERO_too()
    {
        using var corpus = new TempCorpus();
        var xc = corpus.CrossCheck("xc.json", "[]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport()));

        Assert.Equal(CleanupExit.NothingExamined, outcome.Exit);
    }

    [Fact]
    public void The_DENOMINATOR_is_printed_on_EVERY_planned_run_not_only_on_interesting_ones()
    {
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Orphan", 9001).Fc("FC_Deliverable", 2);
        var xc = corpus.CrossCheck("xc.json", "[]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport()));

        Assert.Contains("objects examined                          2", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("harness-owned", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("outside the reserved range", outcome.Text, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Scope gaps and the kinds DB-7 does not name
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_TYPE_and_a_TAGTABLE_are_reported_as_UNADDRESSABLE_rather_than_silently_omitted()
    {
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Live", 9001).Type("UDT_HarnessThing").TagTable("HarnessMirror", "HarnessMirror");
        var xc = corpus.CrossCheck("xc.json", "[{\"block\":\"Main\",\"calls\":[\"FB_Live\"],\"instanceDbRoots\":[]}]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport()));

        Assert.Contains("UNADDRESSABLE BY X-J'S OWNERSHIP RULE   2", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("UDT_HarnessThing", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("HarnessMirror", outcome.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_harness_owned_GLOBAL_DB_is_NONE_OF_DB_7s_THREE_KINDS_and_is_refused_by_that_route()
    {
        // Fail-closed. Filing it as a Block would have the stage removing a class of object DB-7's own
        // rule never sanctioned.
        using var corpus = new TempCorpus();
        corpus.GlobalDb("DB_HarnessScratch", 9500);
        var xc = corpus.CrossCheck("xc.json", "[]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport()));

        Assert.Contains("KindNotStated", outcome.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("REMOVE", outcome.Text, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // §16.12c — the claim disposition
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void With_NO_CLAIMS_DOCUMENT_the_disposition_is_NOT_CHECKED_and_never_no_claim_held()
    {
        using var corpus = new TempCorpus();
        corpus.Fc("FC_Orphan", 9098);
        var xc = corpus.CrossCheck("xc.json", "[]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport()));

        Assert.Contains("[NotChecked]", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("this run did not look", outcome.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_HELD_CLAIM_produces_the_release_command_AFTER_the_delete_and_is_never_run_here()
    {
        using var corpus = new TempCorpus();
        corpus.Fc("FC_Orphan", 9098);
        var xc = corpus.CrossCheck("xc.json", "[]");
        var claims = corpus.File_("claims.json",
            "{\"store\":\"C:/ProgramData/Ladder-AI/claims/p\",\"claims\":[{\"kind\":\"block-number\",\"value\":\"FC9098\",\"agent\":\"lane-a\",\"purpose\":\"the orphan\",\"created\":\"t\"}]}");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport(), claims: claims));

        Assert.Contains("[Held]", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("--release --agent lane-a --kind block-number --value FC9098", outcome.Text, StringComparison.Ordinal);

        var deleteAt = outcome.Text.IndexOf("openness-cli delete", StringComparison.Ordinal);
        var releaseAt = outcome.Text.IndexOf("--release --agent", StringComparison.Ordinal);
        Assert.True(deleteAt >= 0 && deleteAt < releaseAt,
            "the delete must be emitted BEFORE the release: a release issued while the block still exists "
            + "hands the number to the next allocator.");
    }

    [Fact]
    public void An_EMPTY_STORE_reports_NO_CLAIM_HELD_and_says_what_it_cannot_distinguish()
    {
        using var corpus = new TempCorpus();
        corpus.Fc("FC_Orphan", 9098);
        var xc = corpus.CrossCheck("xc.json", "[]");
        var claims = corpus.File_("claims.json", "{\"store\":\"C:/ProgramData/Ladder-AI/claims/p\",\"claims\":[]}");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport(), claims: claims));

        Assert.Contains("[NoClaimHeld]", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("allocated BEFORE the registry existed", outcome.Text, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The budget, DB-7 rule 4
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_TWENTY_FIRST_eligible_removal_is_DEFERRED_BY_NAME_and_never_dropped()
    {
        using var corpus = new TempCorpus();
        for (var i = 1; i <= 25; i++) corpus.Fc($"FC_Orphan{i:D2}", 9100 + i);
        var xc = corpus.CrossCheck("xc.json", "[]");

        var outcome = CleanupRun.Execute(Options(corpus, xc, corpus.DrainedReport()));

        Assert.Contains("20 removal(s), 5 deferred", outcome.Text, StringComparison.Ordinal);
        Assert.Contains("DeferredToNextBatch", outcome.Text, StringComparison.Ordinal);
    }
}
