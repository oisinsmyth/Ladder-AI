using System.Collections.Generic;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// FI-61. The rule under test: TIA approves an Openness caller by (Path, FileHash), so a REBUILD at
/// the same path revokes approval and Openness then refuses the caller SILENTLY — no dialog, no
/// exception, no log, just a connect that blocks until the caller's own timeout expires.
///
/// These cover the decision only. The registry read is a deliberately thin, untested shim whose
/// every failure path returns <see cref="OpennessWhitelist.Verdict.Unknown"/>, because this check is
/// advisory: a wrong "not approved" must never stop a Portal command from being attempted.
/// </summary>
public class OpennessWhitelistTests
{
    private const string Exe = @"C:\repo\src\openness-cli\OpennessCli\bin\Debug\net48\openness-cli.exe";
    private const string Other = @"C:\repo\.claude\worktrees\wt\src\openness-cli\OpennessCli\bin\Debug\net48\openness-cli.exe";

    private static OpennessWhitelist.Entry E(string path, string hash) => new(path, hash);

    [Fact]
    public void MatchingPathAndHash_IsApproved()
    {
        var result = OpennessWhitelist.Evaluate(new[] { E(Exe, "AAA=") }, Exe, "AAA=");

        Assert.Equal(OpennessWhitelist.Verdict.Approved, result.Verdict);
        Assert.Null(OpennessWhitelist.DescribeIfNotApproved(result, Exe));
    }

    // The exact failure of 2026-08-08: the path has been approved many times over, but the binary
    // was rebuilt so none of those hashes is this file's.
    [Fact]
    public void SamePathButNoHashMatches_IsStaleHash_AndCountsThePriorApprovals()
    {
        var entries = new List<OpennessWhitelist.Entry>
        {
            E(Exe, "OLD1="),
            E(Exe, "OLD2="),
            E(Exe, "OLD3="),
        };

        var result = OpennessWhitelist.Evaluate(entries, Exe, "FRESHLY-BUILT=");

        Assert.Equal(OpennessWhitelist.Verdict.StaleHash, result.Verdict);
        Assert.Equal(3, result.EntriesForPath);

        var warning = OpennessWhitelist.DescribeIfNotApproved(result, Exe);
        Assert.NotNull(warning);
        Assert.Contains("REBUILT", warning);
        // The operationally important half, corrected 2026-08-10: a new build needs a PERSON to
        // approve it. With someone at the machine that costs seconds; unattended there is nobody to
        // accept and the attach sits until the timeout — which is what actually stalls an agent.
        Assert.Contains("PERSON", warning);
        Assert.Contains("UNATTENDED", warning);
    }

    // Both warnings fire at the exact moment someone needs to know the manual step is avoidable, so
    // both must name the way out. Asserted rather than left to prose: a message that stops mentioning
    // the escape hatch reads as "there is nothing to do but fetch a human", which is no longer true.
    [Theory]
    [InlineData(OpennessWhitelist.Verdict.StaleHash)]
    [InlineData(OpennessWhitelist.Verdict.PathNotListed)]
    public void EveryWarning_NamesTheOneTimeSetupThatRemovesTheManualStep(OpennessWhitelist.Verdict verdict)
    {
        var warning = OpennessWhitelist.DescribeIfNotApproved(new OpennessWhitelist.Result(verdict, 1), Exe);

        Assert.NotNull(warning);
        Assert.Contains("openness-approve-setup.ps1", warning);
    }

    // A worktree build is a different application to Openness even though it is the same source.
    [Fact]
    public void PathNeverApproved_IsPathNotListed()
    {
        var result = OpennessWhitelist.Evaluate(new[] { E(Exe, "AAA=") }, Other, "AAA=");

        Assert.Equal(OpennessWhitelist.Verdict.PathNotListed, result.Verdict);
        Assert.Equal(0, result.EntriesForPath);
        Assert.Contains("no TIA Openness approval entry names this path", OpennessWhitelist.DescribeIfNotApproved(result, Other));
    }

    // Identical hash at a different path must NOT approve: copying an approved build elsewhere is
    // exactly the case Openness treats as a new application.
    [Fact]
    public void SameHashAtDifferentPath_DoesNotApprove()
    {
        var result = OpennessWhitelist.Evaluate(new[] { E(Other, "SAME=") }, Exe, "SAME=");

        Assert.Equal(OpennessWhitelist.Verdict.PathNotListed, result.Verdict);
    }

    [Fact]
    public void EmptyWhitelist_IsPathNotListed()
    {
        var result = OpennessWhitelist.Evaluate(new OpennessWhitelist.Entry[0], Exe, "AAA=");

        Assert.Equal(OpennessWhitelist.Verdict.PathNotListed, result.Verdict);
    }

    // Windows paths are case-insensitive; the registry may carry a different casing than the
    // running assembly's Location reports.
    [Fact]
    public void PathComparison_IsCaseInsensitive()
    {
        var result = OpennessWhitelist.Evaluate(new[] { E(Exe.ToUpperInvariant(), "AAA=") }, Exe, "AAA=");

        Assert.Equal(OpennessWhitelist.Verdict.Approved, result.Verdict);
    }

    // Base64 IS case-sensitive — two hashes differing only in case are genuinely different files,
    // and treating them as equal would report an unapproved binary as approved.
    [Fact]
    public void HashComparison_IsCaseSensitive()
    {
        var result = OpennessWhitelist.Evaluate(new[] { E(Exe, "aaa=") }, Exe, "AAA=");

        Assert.Equal(OpennessWhitelist.Verdict.StaleHash, result.Verdict);
    }

    [Fact]
    public void UnknownVerdict_NeverWarns()
    {
        var result = new OpennessWhitelist.Result(OpennessWhitelist.Verdict.Unknown, 0);

        Assert.Null(OpennessWhitelist.DescribeIfNotApproved(result, Exe));
    }
}
