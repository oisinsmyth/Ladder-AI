using Harness.CmdInject;

namespace Harness.CmdInject.Tests;

/// <summary>
/// The acknowledgement model: the full 2×2 keyed on the count, <b>including the incoherent cell</b>, and
/// the proof that the result is never what decides.
/// </summary>
public class AckModelTests
{
    private const ushort Sent = 10;
    private const ushort PriorCount = 100;

    private static AckClassification Classify(ushort ackSeq, ushort ackCount, string result = "0") =>
        AckModel.Classify(Sent, PriorCount, new AckObservation(ackSeq, ackCount, result));

    [Fact]
    public void SeqMatchesAndCountAdvanced_IsAcknowledged()
    {
        var c = Classify(ackSeq: Sent, ackCount: PriorCount + 1);
        Assert.Equal(AckOutcome.Acknowledged, c.Outcome);
        Assert.True(c.SeqMatches);
        Assert.True(c.CountAdvanced);
    }

    [Fact]
    public void SeqDiffersAndCountUnchanged_IsPending()
    {
        var c = Classify(ackSeq: 9, ackCount: PriorCount);
        Assert.Equal(AckOutcome.Pending, c.Outcome);
    }

    [Fact]
    public void SeqDiffersAndCountAdvanced_IsSuperseded()
    {
        var c = Classify(ackSeq: 9, ackCount: PriorCount + 1);
        Assert.Equal(AckOutcome.Superseded, c.Outcome);
    }

    [Fact]
    public void SeqMatchesButCountUnchanged_IsIncoherent()
    {
        // *** THE INCOHERENT CELL. *** Our sequence is echoed, but the processed-count did not move: the two
        // observations disagree, so this is its own outcome, never a success with a caveat.
        var c = Classify(ackSeq: Sent, ackCount: PriorCount);
        Assert.Equal(AckOutcome.Incoherent, c.Outcome);
        Assert.True(c.SeqMatches);
        Assert.False(c.CountAdvanced);
    }

    [Fact]
    public void TheResultIsCarriedVerbatimAndNeverDecides()
    {
        // Same two facts, different result strings — the outcome must be identical. The result is reported,
        // never branched on.
        var acknowledgedA = Classify(Sent, PriorCount + 1, result: "0");
        var acknowledgedB = Classify(Sent, PriorCount + 1, result: "any-code-at-all");

        Assert.Equal(acknowledgedA.Outcome, acknowledgedB.Outcome);
        Assert.Equal("0", acknowledgedA.Result);
        Assert.Equal("any-code-at-all", acknowledgedB.Result);
    }

    [Fact]
    public void EveryCellOfTheTwoByTwoHasExactlyOneOutcome()
    {
        // Exhaustive: the four (seqMatches, countAdvanced) combinations map to four distinct outcomes and
        // there is no fifth.
        var outcomes = new[]
        {
            Classify(Sent, PriorCount + 1).Outcome, // T,T
            Classify(9, PriorCount).Outcome,        // F,F
            Classify(9, PriorCount + 1).Outcome,    // F,T
            Classify(Sent, PriorCount).Outcome,     // T,F
        };

        Assert.Equal(4, outcomes.Distinct().Count());
    }
}
