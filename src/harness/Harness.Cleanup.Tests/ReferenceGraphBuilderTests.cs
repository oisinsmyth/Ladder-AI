namespace Harness.Cleanup.Tests;

/// <summary>
/// <b>DB-7 rule 2's evidence.</b> "Nothing references this" is only as good as the edges that were
/// looked for, so every source is tested for the edge it contributes AND the builder is tested for
/// refusing a document that would contribute none.
/// </summary>
public class ReferenceGraphBuilderTests
{
    private static readonly IrObject Fb = new("FB_X", IrObjectKind.Block, "FB", 9001, null, "FB_X.ir");
    private static readonly IrObject Idb = new("iDB_X", IrObjectKind.InstanceDb, null, 9001, "FB_X", "iDB_X.ir");
    private static readonly IrObject Main = new("Main", IrObjectKind.Block, "OB", 1, null, "Main.ir");

    private static readonly IReadOnlyList<(string, string)> NoArtifacts = Array.Empty<(string, string)>();

    private static string Json(string siblingRefs, string extra = "") =>
        $"{{\"siblingRefs\":{siblingRefs}{extra}}}";

    [Fact]
    public void A_CALL_is_a_reference()
    {
        var result = ReferenceGraphBuilder.Build(
            Json("[{\"block\":\"Main\",\"calls\":[\"FB_X\"],\"instanceDbRoots\":[]}]"),
            new[] { Fb, Main },
            NoArtifacts);

        Assert.Equal(1, result.CallEdges);
        Assert.Contains("called by Main", result.Graph.Referrers("FB_X"));
    }

    [Fact]
    public void An_INSTANCE_DB_NAMED_BY_A_BLOCK_is_a_reference_even_though_it_is_not_a_call()
    {
        var result = ReferenceGraphBuilder.Build(
            Json("[{\"block\":\"Main\",\"calls\":[],\"instanceDbRoots\":[\"iDB_X\"]}]"),
            new[] { Idb, Main },
            NoArtifacts);

        Assert.Single(result.Graph.Referrers("iDB_X"));
    }

    [Fact]
    public void An_FB_IS_REFERENCED_BY_ITS_OWN_INSTANCE_DB_so_cleanup_CONVERGES_rather_than_inverting_the_order()
    {
        // cross-check emits no such edge. Without it an FB whose iDB still exists reads as orphaned, and
        // the plan would propose deleting the FB out from under a live instance DB — which TIA refuses.
        // With it, the iDB goes in one batch and the FB becomes eligible in the next.
        var result = ReferenceGraphBuilder.Build(
            Json("[]"),
            new[] { Fb, Idb },
            NoArtifacts);

        Assert.Equal(1, result.InstanceOfEdges);
        Assert.Contains("instantiated by iDB_X", result.Graph.Referrers("FB_X"));
        Assert.Empty(result.Graph.Referrers("iDB_X"));
    }

    [Fact]
    public void A_TEST_ARTIFACT_naming_an_object_is_a_reference()
    {
        var result = ReferenceGraphBuilder.Build(
            Json("[]"),
            new[] { Fb },
            new[] { ("vectors.json", "{\"model\":\"FB_X\"}") });

        Assert.Equal(1, result.VectorEdges);
        Assert.Contains("named in admitted test artifact vectors.json", result.Graph.Referrers("FB_X"));
    }

    [Fact]
    public void The_test_artifact_match_is_WHOLE_WORD_so_a_longer_name_is_not_a_hit()
    {
        // FB_X must not be matched by FB_XRay. Over-referencing is the safe direction, but a rule that
        // matched every prefix would retain everything and the tool would be a no-op that looks careful.
        var result = ReferenceGraphBuilder.Build(
            Json("[]"),
            new[] { Fb },
            new[] { ("vectors.json", "{\"model\":\"FB_XRay\"}") });

        Assert.Equal(0, result.VectorEdges);
        Assert.Empty(result.Graph.Referrers("FB_X"));
    }

    [Fact]
    public void A_WRITER_and_a_READER_of_a_member_both_reference_the_object_that_owns_it()
    {
        var db = new IrObject("DB_A", IrObjectKind.GlobalDb, null, 9500, null, "DB_A.ir");
        var reader = new IrObject("FC_R", IrObjectKind.Block, "FC", 2, null, "FC_R.ir");

        var result = ReferenceGraphBuilder.Build(
            Json("[]", ",\"soleWriters\":[{\"path\":\"DB_A.Flag.%X0\",\"writer\":{\"block\":\"Main\"},\"readers\":[{\"block\":\"FC_R\"}]}]"),
            new[] { db, Main, reader },
            NoArtifacts);

        Assert.Equal(2, result.Graph.Referrers("DB_A").Count);
    }

    [Theory]
    [InlineData("DB_A.Flag.%X0", "DB_A")]
    [InlineData("DB_Vessel[0].Sensor[1].Reading", "DB_Vessel")]
    [InlineData("BareName", "BareName")]
    public void A_path_roots_on_its_first_segment_with_subscripts_stripped(string path, string root)
        => Assert.Equal(root, ReferenceGraphBuilder.PathRoot(path));

    [Fact]
    public void A_SELF_EDGE_IS_DROPPED_AND_REPORTED_BY_NAME()
    {
        // An FB's STATIC members are paths rooted at the FB's own name, so every FB with statics
        // "references" itself. Left in, nothing in any corpus could ever be eligible and this tool would
        // return a clean-looking zero forever. It is the only removal-direction rule in the builder, so
        // it is never silent.
        var result = ReferenceGraphBuilder.Build(
            Json("[]", ",\"soleWriters\":[{\"path\":\"FB_X.Timer.PT\",\"writer\":{\"block\":\"FB_X\"},\"readers\":[]}]"),
            new[] { Fb },
            NoArtifacts);

        Assert.Empty(result.Graph.Referrers("FB_X"));
        Assert.Contains("FB_X", result.SelfEdgesDropped);
    }

    [Fact]
    public void A_SELF_EDGE_DROP_does_not_take_a_REAL_referrer_with_it()
    {
        // The over-firing direction of the same rule: dropping FB_X's self-edge must leave Main's edge
        // alone. A subtraction that removed a real referrer would propose deleting something in use.
        var result = ReferenceGraphBuilder.Build(
            Json("[{\"block\":\"Main\",\"calls\":[\"FB_X\"],\"instanceDbRoots\":[]}]",
                 ",\"soleWriters\":[{\"path\":\"FB_X.Timer.PT\",\"writer\":{\"block\":\"FB_X\"},\"readers\":[]}]"),
            new[] { Fb, Main },
            NoArtifacts);

        Assert.Contains("called by Main", result.Graph.Referrers("FB_X"));
        Assert.Contains("FB_X", result.SelfEdgesDropped);
    }

    [Fact]
    public void An_edge_to_a_name_OUTSIDE_the_corpus_is_dropped()
    {
        var result = ReferenceGraphBuilder.Build(
            Json("[{\"block\":\"Main\",\"calls\":[\"FB_Elsewhere\"],\"instanceDbRoots\":[]}]"),
            new[] { Main },
            NoArtifacts);

        Assert.Empty(result.Graph.Referrers("FB_Elsewhere"));
    }

    [Fact]
    public void A_DOCUMENT_WITH_NO_SIBLING_REFS_IS_REFUSED_rather_than_read_as_a_graph_with_no_calls()
    {
        // The sharpest form of "deleting an input produces a WRONG conclusion, not a smaller one": with
        // no call graph, every called block reads as unreferenced and the plan would confidently propose
        // deleting the whole program.
        var ex = Assert.Throws<CleanupInputException>(() =>
            ReferenceGraphBuilder.Build("{\"multiWriters\":[]}", new[] { Fb }, NoArtifacts));

        Assert.Contains("siblingRefs", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MALFORMED_JSON_is_refused_and_named()
    {
        var ex = Assert.Throws<CleanupInputException>(() =>
            ReferenceGraphBuilder.Build("{not json", new[] { Fb }, NoArtifacts));

        Assert.Contains("not valid JSON", ex.Message, StringComparison.Ordinal);
    }
}
