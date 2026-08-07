using System.Collections.Concurrent;
using Converter.Claims;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-48 component 1 — the property the entire design rests on.
///
/// Acquisition is `FileMode.CreateNew`, i.e. the filesystem is the mutex. If that guarantee does not
/// hold, every other test in the claim suite still passes and the tool silently grants the same
/// resource to two agents — which is precisely the failure it exists to prevent, restored by the
/// thing meant to remove it. So the race is tested directly rather than reasoned about.
/// </summary>
public class ClaimConcurrencyTests : IDisposable
{
    private readonly string _projectDir = ClaimsTestCorpus.Create();
    private readonly string _claimsRoot = ClaimsTestCorpus.CreateClaimsRoot();

    [Fact]
    public void ContestedValue_ExactlyOneAgentWins()
    {
        const int agents = 24;
        var corpus = ClaimCorpus.Build(_projectDir);
        var outcomes = new ConcurrentBag<ClaimOutcome>();

        Parallel.For(0, agents, i =>
        {
            // A store per agent, as in real use: each agent is its own process with its own object
            // graph, sharing only the directory.
            var store = new ClaimStore(_claimsRoot, _projectDir);
            outcomes.Add(ClaimsRunner.Acquire(corpus, store, _projectDir, ClaimKind.BlockNumber, "FB77", $"agent{i}", null));
        });

        var winners = outcomes.Where(o => o.Ok).ToList();
        Assert.Single(winners);
        Assert.Equal(agents - 1, outcomes.Count(o => o.Result == ClaimResult.HeldByAnother));

        // Every loser must be told the SAME holder, and it must be the actual winner — a refusal that
        // names the wrong agent sends the loser to argue with an innocent party.
        var winner = winners[0].Claim!.Agent;
        Assert.All(outcomes.Where(o => !o.Ok), o => Assert.Equal(winner, o.Holder!.Agent));
    }

    [Fact]
    public void ParallelAllocation_HandsOutDistinctValues()
    {
        const int agents = 12;
        var corpus = ClaimCorpus.Build(_projectDir);
        var outcomes = new ConcurrentBag<ClaimOutcome>();

        Parallel.For(0, agents, i =>
        {
            var store = new ClaimStore(_claimsRoot, _projectDir);
            outcomes.Add(ClaimsRunner.Allocate(corpus, store, _projectDir, ClaimKind.BlockNumber, "FB", 1, null, $"agent{i}", null));
        });

        var values = outcomes.Where(o => o.Ok).Select(o => o.Claim!.Value).ToList();
        Assert.Equal(agents, values.Count);
        Assert.Equal(agents, values.Distinct().Count());

        // FB50 is taken in the corpus, so allocation must have stepped over it even under contention.
        Assert.DoesNotContain("FB50", values);
    }

    [Fact]
    public void ReclaimingYourOwnValue_IsIdempotent()
    {
        var corpus = ClaimCorpus.Build(_projectDir);
        var store = new ClaimStore(_claimsRoot, _projectDir);

        var first = ClaimsRunner.Acquire(corpus, store, _projectDir, ClaimKind.BlockNumber, "FB60", "A", "first");
        var second = ClaimsRunner.Acquire(corpus, store, _projectDir, ClaimKind.BlockNumber, "FB60", "A", "retry");

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        // The original claim survives — a retry must not silently rewrite the purpose or timestamp of
        // a claim another step may already have reported.
        Assert.Equal("first", store.Find(ClaimKind.BlockNumber, "FB60")!.Purpose);
    }

    public void Dispose() => ClaimsTestCorpus.Delete(_projectDir, _claimsRoot);
}
