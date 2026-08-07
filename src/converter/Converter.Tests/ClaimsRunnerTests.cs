using Converter.Claims;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-50 component 1 — allocation, and what `claims --check` gates on. The gating boundary is the
/// substance here: a fulfilled allocation claim (the agent wrote the block, so the resource now
/// exists) is the NORMAL end state, and a check that failed on it would cry wolf on every success.
/// </summary>
public class ClaimsRunnerTests : IDisposable
{
    private readonly string _projectDir = ClaimsTestCorpus.Create();
    private readonly string _claimsRoot = ClaimsTestCorpus.CreateClaimsRoot();
    private readonly ClaimCorpus _corpus;
    private readonly ClaimStore _store;

    public ClaimsRunnerTests()
    {
        _corpus = ClaimCorpus.Build(_projectDir);
        _store = new ClaimStore(_claimsRoot, _projectDir);
    }

    private ClaimOutcome Acquire(ClaimKind kind, string value, string agent) =>
        ClaimsRunner.Acquire(_corpus, _store, _projectDir, kind, value, agent, null);

    private ClaimsReport Check(DateTime? now = null) =>
        ClaimsRunner.Check(_corpus, _store, _projectDir, now ?? new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Allocate_SkipsNumbersUsedInTheCorpus()
    {
        var outcome = ClaimsRunner.Allocate(_corpus, _store, _projectDir, ClaimKind.BlockNumber, "FB", 50, null, "A", null);

        // FB50 is FB_Existing, so the floor of 50 must land on 51.
        Assert.True(outcome.Ok);
        Assert.Equal("FB51", outcome.Claim!.Value);
    }

    [Fact]
    public void Allocate_BlockNetwork_TakesTheSlotAfterTheLast()
    {
        var outcome = ClaimsRunner.Allocate(_corpus, _store, _projectDir, ClaimKind.BlockNetwork, null, 1, ClaimsTestCorpus.SharedBlock, "A", null);

        Assert.Equal(ClaimsTestCorpus.SharedBlock + ":8", outcome.Claim!.Value);
    }

    [Fact]
    public void Allocate_AlarmBit_SkipsTheDrivenBit()
    {
        var outcome = ClaimsRunner.Allocate(_corpus, _store, _projectDir, ClaimKind.AlarmBit, null, 1, ClaimsTestCorpus.AlarmWord, "A", null);

        // Bit 0 is driven by the shared FC, so the first free bit is 1.
        Assert.Equal(ClaimsTestCorpus.AlarmWord + ".%X1", outcome.Claim!.Value);
    }

    [Fact]
    public void Allocate_WithoutTheRequiredScope_IsInvalid()
    {
        var outcome = ClaimsRunner.Allocate(_corpus, _store, _projectDir, ClaimKind.BlockNetwork, null, 1, null, "A", null);

        Assert.Equal(ClaimResult.Invalid, outcome.Result);
        Assert.Contains("--in", outcome.Reason);
    }

    [Fact]
    public void Allocate_IsUndefinedForKindsWithNoOrdering()
    {
        var outcome = ClaimsRunner.Allocate(_corpus, _store, _projectDir, ClaimKind.BlockEdit, null, 1, null, "A", null);

        Assert.Equal(ClaimResult.Invalid, outcome.Result);
    }

    [Fact]
    public void Check_CleanWhenEveryClaimStillHolds()
    {
        Acquire(ClaimKind.BlockNumber, "FB77", "A");
        Acquire(ClaimKind.BlockNetwork, ClaimsTestCorpus.SharedBlock + ":8", "A");

        var report = Check();

        Assert.False(report.HasFindings);
        Assert.Equal(2, report.Claims.Count);
    }

    [Fact]
    public void Check_TreatsAFulfilledAllocationAsSuccessNotConflict()
    {
        // FB50 exists in the corpus. A claim on it therefore looks "already used" — which, for an
        // allocation claim, is what a completed piece of work looks like from the outside.
        _store.TryAcquire(_projectDir, ClaimKind.BlockNumber, "FB50", "A", null);

        var report = Check();

        Assert.False(report.HasFindings);
        Assert.Single(report.Fulfilled);
        Assert.Empty(report.Conflicts);
    }

    [Fact]
    public void Check_FlagsAnExclusiveClaimOnAVanishedBlock()
    {
        _store.TryAcquire(_projectDir, ClaimKind.BlockEdit, "FC_Deleted", "A", null);

        var report = Check();

        Assert.True(report.HasFindings);
        Assert.Single(report.Conflicts);
    }

    [Fact]
    public void Check_FindsTheCrossKindConflictTheFilesystemCannot()
    {
        // Two DIFFERENT values, so both acquisitions legitimately succeed — the store has no way to
        // see that they describe the same block. This is the conflict class --check exists for.
        Acquire(ClaimKind.BlockEdit, ClaimsTestCorpus.SharedBlock, "A");
        Acquire(ClaimKind.BlockNetwork, ClaimsTestCorpus.SharedBlock + ":8", "B");

        var report = Check();

        Assert.True(report.HasFindings);
        Assert.Contains(report.Conflicts, c => c.Detail.Contains("exclusive block-edit"));
    }

    [Fact]
    public void Check_AllowsOneAgentToHoldBothClaimsOnItsOwnBlock()
    {
        Acquire(ClaimKind.BlockEdit, ClaimsTestCorpus.SharedBlock, "A");
        Acquire(ClaimKind.BlockNetwork, ClaimsTestCorpus.SharedBlock + ":8", "A");

        Assert.False(Check().HasFindings);
    }

    [Fact]
    public void Check_ReportsStaleClaimsWithoutGatingOnThem()
    {
        Acquire(ClaimKind.BlockNumber, "FB77", "A");

        var report = ClaimsRunner.Check(_corpus, _store, _projectDir, DateTime.UtcNow.AddDays(3));

        Assert.Single(report.Stale);
        Assert.False(report.HasFindings);
    }

    [Fact]
    public void Check_WarnsWhenTheClaimsDirectoryDoesNotExist()
    {
        var report = Check();

        Assert.Empty(report.Claims);
        Assert.Contains(report.Warnings, w => w.Contains("0 claims examined"));
    }

    [Fact]
    public void Check_WarnsWhenAClaimRecordsADifferentProject()
    {
        _store.TryAcquire("ir/some-other-project", ClaimKind.BlockNumber, "FB77", "A", null);

        Assert.Contains(Check().Warnings, w => w.Contains("some-other-project"));
    }

    public void Dispose() => ClaimsTestCorpus.Delete(_projectDir, _claimsRoot);
}
