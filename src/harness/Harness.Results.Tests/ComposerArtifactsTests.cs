using Harness.Results;
using Xunit;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>THE READERS THAT REFUSE TO TURN A "COULD NOT COMPUTE" REPORT INTO AN EMPTY ANSWER.</b>
///
/// <para>*** MEASURED ON A REAL JOB. *** The artifact that should have produced the conflict edges was a
/// <c>notComputed</c> report — it resolved none of the submission's signals and said so — while the
/// submission declared the edges anyway. Read as "no conflicts", that document is indistinguishable
/// from a clean program.</para>
///
/// <para><b>Every case is paired with its opposite</b>, because a reader that refused everything would
/// satisfy each refusal test on its own.</para>
/// </summary>
public class ComposerArtifactsTests
{
    // ---------------------------------------------------------------------------------------------
    // reachable state
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_REACHABLE_STATE_DOCUMENT_WITH_CONTENT_READS()
    {
        var read = ComposerArtifacts.ReadReachableState(
            """{ "blocks": [ { "block": "FB_A", "reachableState": [ "DB.X", "DB.Y" ] } ] }""");

        Assert.Equal(ComposerReadOutcome.Read, read.Outcome);
        Assert.Equal(new[] { "DB.X", "DB.Y" }, read.Entries["FB_A"]);
    }

    [Fact]
    public void AND_A_WHOLE_REPORT_REFUSAL_IS_NOT_COMPUTED_RATHER_THAN_EMPTY()
    {
        // The producer writes `notComputed` INSTEAD OF `blocks`, so a reader that looked for content
        // first would find none and call it an answer.
        var read = ComposerArtifacts.ReadReachableState(
            """{ "notComputed": "68 of 68 signals could not be resolved to exactly one storage path." }""");

        Assert.Equal(ComposerReadOutcome.NotComputed, read.Outcome);
        Assert.Contains("could not be resolved", read.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_A_SINGLE_BLOCK_WITHHELD_BY_NAME_MAKES_THE_WHOLE_READ_NOT_COMPUTED()
    {
        // 🔴 Returning the blocks that DID compute would be an answer over a smaller set, and nothing
        // downstream could tell it from a whole one.
        var read = ComposerArtifacts.ReadReachableState(
            """
            { "blocks": [ { "block": "FB_A", "reachableState": [ "DB.X" ] },
                          { "block": "FB_B", "notComputed": "its call tree left the corpus" } ] }
            """);

        Assert.Equal(ComposerReadOutcome.NotComputed, read.Outcome);
        Assert.Contains("FB_B", read.Detail, StringComparison.Ordinal);
        Assert.Empty(read.Entries);
    }

    [Fact]
    public void AN_EMPTY_CLOSURE_IS_NOT_COMPUTED_because_the_producer_withholds_rather_than_emitting_zero()
    {
        var read = ComposerArtifacts.ReadReachableState("""{ "blocks": [] }""");

        Assert.Equal(ComposerReadOutcome.NotComputed, read.Outcome);
    }

    // ---------------------------------------------------------------------------------------------
    // conflict graph — where empty IS an answer
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AN_EMPTY_CONFLICT_GRAPH_IS_AN_ANSWER_and_this_is_the_asymmetry_with_the_closure()
    {
        // *** THE TWO KINDS GENUINELY DIFFER. *** `[]` here is the EARNED claim "the graph ran over the
        // corpus and found nothing" — the same meaning the submission's own empty conflictEdges carries.
        // Refusing it would refuse every clean project.
        var read = ComposerArtifacts.ReadConflictGraph("""{ "edges": [] }""");

        Assert.Equal(ComposerReadOutcome.Read, read.Outcome);
        Assert.Empty(read.Entries);
    }

    [Fact]
    public void AND_A_CONFLICT_GRAPH_THAT_COULD_NOT_BE_COMPUTED_IS_STILL_REFUSED()
    {
        var read = ComposerArtifacts.ReadConflictGraph(
            """{ "notComputed": "the signals could not be resolved to storage paths." }""");

        Assert.Equal(ComposerReadOutcome.NotComputed, read.Outcome);
    }

    [Fact]
    public void THE_conflictEdges_SPELLING_IS_READ_because_it_is_what_the_live_producer_emits()
    {
        // 🔴 *** FOUND BY A DRY RUN OVER A REAL JOB, AND IT WAS A FALSE REFUSAL. *** This shape - a fully
        // computed graph, 36 signals examined and 36 resolved - was reported as "neither an answer nor a
        // refusal". A valid artifact called unusable is as damaging as an invalid one accepted.
        var read = ComposerArtifacts.ReadConflictGraph(
            """{ "conflictEdges": [], "derivation": { "computed": true, "signalsResolved": 36 } }""");

        Assert.Equal(ComposerReadOutcome.Read, read.Outcome);
    }

    [Fact]
    public void AND_THE_DOCUMENTS_OWN_computed_FALSE_OUTRANKS_ANY_INFERENCE_FROM_ITS_SHAPE()
    {
        // The document states outright whether it was computed. Guessing from the shape when it says so
        // is how a report of failure gets read as a clean answer.
        var read = ComposerArtifacts.ReadConflictGraph(
            """{ "conflictEdges": [], "derivation": { "computed": false, "signalsUnresolved": 68 } }""");

        Assert.Equal(ComposerReadOutcome.NotComputed, read.Outcome);
        Assert.Contains("68", read.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_CONFLICT_GRAPH_WITH_EDGES_READS_THEM()
    {
        var read = ComposerArtifacts.ReadConflictGraph(
            """{ "edges": [ { "blockA": "FB_A", "blockB": "FB_B" } ] }""");

        Assert.Equal(ComposerReadOutcome.Read, read.Outcome);
        Assert.Equal(new[] { "FB_B" }, read.Entries["FB_A"]);
    }

    // ---------------------------------------------------------------------------------------------
    // not this kind at all
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_DOCUMENT_OF_THE_WRONG_SHAPE_IS_NOT_THIS_KIND_RATHER_THAN_EMPTY()
    {
        // The drift guard: a format that moves surfaces LOUDLY as NotThisKind, never as a clean read of
        // nothing. This reader is a second copy of a wave-control format and that is the accepted cost.
        var read = ComposerArtifacts.ReadReachableState("""{ "somethingElse": true }""");

        Assert.Equal(ComposerReadOutcome.NotThisKind, read.Outcome);
    }

    [Fact]
    public void AND_SO_IS_TEXT_THAT_IS_NOT_JSON()
    {
        Assert.Equal(ComposerReadOutcome.NotThisKind, ComposerArtifacts.ReadReachableState("not json at all").Outcome);
    }
}
