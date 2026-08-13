using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DownloadProbe;
using Xunit;

using ProbeProgram = DownloadProbe.Program;

namespace OpennessCli.Tests;

/// <summary>
/// THE DEVICE-DOWNLOAD FENCE (2026-08-13). `download-probe` is the one binary in this repository
/// that calls <c>DownloadProvider.Download</c>, and this is what decides which project it may open.
///
/// *** IT REPLACED A FILE-NAME SUFFIX, WHICH WAS WRONG IN BOTH DIRECTIONS. *** The old guard accepted
/// any project whose file name ended <c>" scratch.ap20"</c>: it BLOCKED the repository's own scratch
/// projects (<c>GenProject1.ap20</c>, <c>SampleProject.ap20</c>) and it did NOT STOP a real site
/// project, because a suffix is a convention and anything can be renamed into one. This machine
/// carries about nineteen real site <c>.ap20</c> projects beside the scratch ones.
///
/// *** WHAT THESE TESTS ARE FOR, AND WHY THEY ARE SHAPED LIKE THIS. *** "A guard that was written,
/// tested around, and never actually executed" is this project's most reliable failure mode, so the
/// central test here does not inspect the fence — it hands <c>Run</c> a session stub that APPENDS TO
/// A SENTINEL FILE and then asserts the sentinel does not exist. And the instrument is CONTROLLED:
/// a permitted case asserts the sentinel IS written. Without that half, "no sentinel" is also what a
/// broken stub or a test that never ran looks like.
/// </summary>
public class DownloadProbeAllowlistTests
{
    // ---- the fixture: a repository with a known allowlist, and a project file to point at --------

    private static string NewTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "probe-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    // ---- THE SENTINEL: the fence refuses BEFORE anything runs, and the test can prove it ---------

    /// <summary>
    /// ADR-0011's shape, in C#. The session stub is the probe's ONLY route to Portal — <c>Run</c>
    /// calls nothing else that could attach — so "the sentinel was not written" and "Portal was not
    /// contacted" are the same statement, observed directly rather than inferred from reading the
    /// code.
    /// </summary>
    private static (int Exit, bool SentinelWritten, string Stderr, int LogFiles) RunWithSentinel(
        string projectPath, string binaryDirectory)
    {
        var logDir = NewTempDir();

        // Deliberately NOT inside logDir: the log directory's file count is itself an assertion
        // (a refusal must leave no artifact at all), and a sentinel sitting in it would be one.
        var sentinel = Path.Combine(NewTempDir(), "portal-was-contacted.sentinel");
        var stderr = new StringWriter();

        var exit = ProbeProgram.Run(
            new[] { projectPath, "--options", "SoftwareOnlyChanges", "--log-dir", logDir },
            new StringWriter(),
            stderr,
            (_, log) =>
            {
                File.AppendAllText(sentinel, "the session ran\n");
                log.Line("(stub session)");
                return new ProbeOutcome(ProbeExitCodes.Completed, "stub");
            },
            binaryDirectory);

        return (exit, File.Exists(sentinel), stderr.ToString(), Directory.GetFiles(logDir).Length);
    }

    [Fact]
    public void ARefusedProject_NeverReachesTheSession_AndLeavesNoArtifact()
    {
        using var repo = ProbeFenceRepo.Permitting();

        // A sibling project, in the same folder, that nobody allowlisted. This is the mistyped-path
        // case the fence exists for.
        var other = Path.Combine(Path.GetDirectoryName(repo.ProjectPath)!, "NotAllowlisted.ap20");
        File.WriteAllText(other, "(another project)");

        var (exit, sentinel, stderr, logFiles) = RunWithSentinel(other, repo.BinaryDirectory);

        Assert.Equal(ProbeExitCodes.RefusedByPath, exit);
        Assert.False(sentinel, "the session ran — the fence did not refuse before Portal could be contacted.");
        Assert.Contains("REFUSED", stderr, StringComparison.Ordinal);
        Assert.Contains("not on the allowlist", stderr, StringComparison.Ordinal);
        Assert.Contains("Portal was NOT contacted", stderr, StringComparison.Ordinal);

        // Not even an artifact: the refusal happens before the log file is opened.
        Assert.Equal(0, logFiles);
    }

    /// <summary>
    /// *** THE CONTROLLED POSITIVE CASE, AND IT IS WHAT MAKES THE ONE ABOVE EVIDENCE. *** Without it,
    /// "the sentinel is absent" is also what a broken stub, a wrong path, or a test that never ran
    /// looks like — the same "empty is not clean" trap this project keeps meeting.
    /// </summary>
    [Fact]
    public void AnAllowlistedProject_DoesReachTheSession()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var (exit, sentinel, _, logFiles) = RunWithSentinel(repo.ProjectPath, repo.BinaryDirectory);

        Assert.Equal(ProbeExitCodes.Completed, exit);
        Assert.True(sentinel, "the permitted case did not reach the session, so the refusal test proves nothing.");
        Assert.Equal(1, logFiles);
    }

    /// <summary>
    /// The permitted run says WHICH ENTRY vouched for it, in the artifact. A fence that only speaks
    /// when it refuses leaves a successful run unable to say what permitted it — and this log is the
    /// record somebody reads weeks later.
    /// </summary>
    [Fact]
    public void APermittedRun_RecordsTheEntryThatVouchedForIt_InTheLog()
    {
        using var repo = ProbeFenceRepo.Permitting();
        var logDir = NewTempDir();

        ProbeProgram.Run(
            new[] { repo.ProjectPath, "--options", "SoftwareOnlyChanges", "--log-dir", logDir },
            new StringWriter(),
            new StringWriter(),
            (_, _) => new ProbeOutcome(ProbeExitCodes.Completed, "stub"),
            repo.BinaryDirectory);

        var text = File.ReadAllText(Assert.Single(Directory.GetFiles(logDir)));
        Assert.Contains("SCRATCH FENCE: PERMITTED", text, StringComparison.Ordinal);
        Assert.Contains(ScratchProjectGuard.RepoPrefix + "Sandbox/Sandbox.ap20", text, StringComparison.Ordinal);
        Assert.Contains(repo.AllowlistPath, text, StringComparison.Ordinal);
    }

    // ---- the old suffix convention is gone, in both directions -----------------------------------

    /// <summary>
    /// THE CASE THE OLD GUARD BLOCKED. A project the repository legitimately owns and deliberately
    /// did not rename is now permitted — by being named, which is the only thing that permits
    /// anything here.
    /// </summary>
    [Fact]
    public void AProjectWithoutTheOldSuffix_IsPermitted_WhenItIsAllowlisted()
    {
        using var repo = ProbeFenceRepo.Permitting("GenProject1.ap20");

        var decision = ScratchProjectGuard.Evaluate(repo.ProjectPath, repo.BinaryDirectory);

        Assert.True(decision.Permitted, decision.Text);
        Assert.DoesNotContain("scratch.ap20", decision.MatchedEntry!.Raw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// THE CASE THE OLD GUARD LET THROUGH. A project can be renamed into any convention; it cannot
    /// rename itself onto an allowlist. The name here is deliberately the old magic one.
    /// </summary>
    [Fact]
    public void AProjectRenamedIntoTheOldConvention_IsStillRefused()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var renamed = Path.Combine(Path.GetDirectoryName(repo.ProjectPath)!, "Anything scratch.ap20");
        File.WriteAllText(renamed, "(a project that merely LOOKS like the scratch copy)");

        var decision = ScratchProjectGuard.Evaluate(renamed, repo.BinaryDirectory);

        Assert.False(decision.Permitted);
        Assert.Contains("not on the allowlist", decision.Text, StringComparison.Ordinal);
    }

    // ---- every "empty is not clean" case is a refusal, never a pass -------------------------------

    [Fact]
    public void NoAllowlistAnywhere_IsARefusal_BecauseTheFenceVerifiedNothing()
    {
        using var repo = ProbeFenceRepo.Create(entries: null);

        // Both sources named explicitly, and neither exists — the case that must not read as
        // "nothing objected, therefore fine".
        var decision = ScratchProjectGuard.Decide(
            repo.ProjectPath,
            new[] { repo.AllowlistPath, Path.Combine(repo.Root, "nowhere", "machine.allowlist") },
            repo.Root);

        Assert.False(decision.Permitted);
        Assert.Contains("verified NOTHING", decision.Text, StringComparison.Ordinal);
        Assert.Contains(repo.AllowlistPath, decision.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAllowlistOfCommentsOnly_IsARefusal()
    {
        using var repo = ProbeFenceRepo.Create(new[] { "# nothing here", string.Empty, "   " });

        var decision = ScratchProjectGuard.Decide(repo.ProjectPath, new[] { repo.AllowlistPath }, repo.Root);

        Assert.False(decision.Permitted);
        Assert.Contains("empty", decision.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ABareRelativeEntry_IsARefusal_NotASilentSkip()
    {
        using var repo = ProbeFenceRepo.Create(new[] { @"Sandbox\Sandbox.ap20" });

        var decision = ScratchProjectGuard.Decide(repo.ProjectPath, new[] { repo.AllowlistPath }, repo.Root);

        Assert.False(decision.Permitted);
        Assert.Contains("bare relative path", decision.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ARepoRelativeEntry_WithNoRepositoryRoot_IsARefusal()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var decision = ScratchProjectGuard.Decide(
            repo.ProjectPath, new[] { repo.AllowlistPath }, repoRoot: null, binaryDirectory: @"C:\somewhere\else");

        Assert.False(decision.Permitted);
        Assert.Contains("repository root", decision.Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ADR-0011 decision 4, which cost a live confirm-loop run: a bare project NAME resolves to the
    /// project FOLDER, which exists. Fail-closed worked and the fence printed the wrong reason — and
    /// *** a guard that explains itself wrongly is how somebody concludes it is broken and goes
    /// looking for a way around it. *** The failure mode is social, not technical.
    /// </summary>
    [Fact]
    public void ADirectory_IsRefused_AndSaysItIsADirectory()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var decision = ScratchProjectGuard.Evaluate(Path.GetDirectoryName(repo.ProjectPath), repo.BinaryDirectory);

        Assert.False(decision.Permitted);
        Assert.Contains("it is a DIRECTORY", decision.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatIsNotAProject_IsRefused_AndSaysWhy()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var decision = ScratchProjectGuard.Evaluate(repo.AllowlistPath, repo.BinaryDirectory);

        Assert.False(decision.Permitted);
        Assert.Contains("not a .apNN project file", decision.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AProjectThatDoesNotExist_IsRefused_AndSaysSo()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var decision = ScratchProjectGuard.Evaluate(
            Path.Combine(repo.Root, "Sandbox", "Vanished.ap20"), repo.BinaryDirectory);

        Assert.False(decision.Permitted);
        Assert.Contains("no such file exists", decision.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoProjectAtAll_IsARefusal(string? path)
    {
        using var repo = ProbeFenceRepo.Permitting();

        Assert.False(ScratchProjectGuard.Evaluate(path, repo.BinaryDirectory).Permitted);
    }

    // ---- the comparison is on the RESOLVED path --------------------------------------------------

    /// <summary>
    /// ADR-0011 requirement 5. A different casing and a <c>..</c> that arrives at the same file are
    /// the same project; a string comparison would call them different and the fence would refuse a
    /// legitimate target — which is how a fence gets edited by someone who thinks it is broken.
    /// </summary>
    [Fact]
    public void ThePathIsCanonicalised_SoDotDotAndCasingResolveToTheSameProject()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var viaDotDot = Path.Combine(
            Path.GetDirectoryName(repo.ProjectPath)!, "..", "Sandbox", "Sandbox.ap20");

        Assert.True(ScratchProjectGuard.Evaluate(viaDotDot, repo.BinaryDirectory).Permitted);
        Assert.True(ScratchProjectGuard.Evaluate(repo.ProjectPath.ToUpperInvariant(), repo.BinaryDirectory).Permitted);
    }

    /// <summary>
    /// The converse, and the one that matters: a <c>..</c> cannot walk OUT of an allowlisted folder
    /// into a project that is not on the list.
    /// </summary>
    [Fact]
    public void ADotDotPath_CannotWalkOntoAProjectThatIsNotAllowlisted()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var outsider = Path.Combine(repo.Root, "Elsewhere.ap20");
        File.WriteAllText(outsider, "(not allowlisted)");

        var viaDotDot = Path.Combine(Path.GetDirectoryName(repo.ProjectPath)!, "..", "Elsewhere.ap20");

        Assert.False(ScratchProjectGuard.Evaluate(viaDotDot, repo.BinaryDirectory).Permitted);
    }

    // ---- NO OVERRIDE: not a flag, not an argument, not an environment variable --------------------

    /// <summary>
    /// *** THE CLAIM IN THE ALLOWLIST FILE — "there is no override" — CHECKED RATHER THAN STATED. ***
    /// Every spelling somebody would reach for is an unknown option, which is a USAGE ERROR: refused
    /// loudly, never accepted as a no-op. The <c>download-plan</c> shape.
    /// </summary>
    [Theory]
    [InlineData("--allowlist")]
    [InlineData("--allow-any-project")]
    [InlineData("--no-fence")]
    [InlineData("--force")]
    [InlineData("--yes")]
    [InlineData("--is-scratch-project")]
    public void NoFlagCanNameADifferentAllowlistOrSkipTheFence(string flag)
    {
        using var repo = ProbeFenceRepo.Permitting();

        var parsed = ProbeArgumentParser.Parse(
            new[] { repo.ProjectPath, "--options", "Software", flag, "anything" }, null, Path.GetTempPath());

        var failure = Assert.IsType<ProbeParseResult.Failure>(parsed);
        Assert.Contains("Unknown option", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// *** AND NO ENVIRONMENT VARIABLE EITHER — WITH THE INSTRUMENT CONTROLLED. *** The fence's whole
    /// source is searched for an environment read, and the same search is then run against a file
    /// that demonstrably HAS one (<c>ProbeArguments.cs</c> reads
    /// <c>LADDER_PROBE_LOG_DIR</c>). Without that control, "no match" would also be what a wrong
    /// path, a renamed file or a typo in the search term looks like — which is the same
    /// "empty is not clean" trap the fence itself is written against.
    /// </summary>
    [Fact]
    public void NoEnvironmentVariableCanNameADifferentAllowlist()
    {
        var fence = ReadRepoSource(@"src\openness-cli\DownloadProbe\ScratchProjectGuard.cs");
        var control = ReadRepoSource(@"src\openness-cli\DownloadProbe\ProbeArguments.cs");

        // The control: the search term really does find an environment read when there is one.
        Assert.Contains("GetEnvironmentVariable", control, StringComparison.Ordinal);

        Assert.DoesNotContain("GetEnvironmentVariable", fence, StringComparison.Ordinal);
        Assert.DoesNotContain("LADDER_", fence, StringComparison.Ordinal);
    }

    /// <summary>
    /// The one environment-derived value the fence does use is <c>CommonApplicationData</c> — a
    /// Windows special folder, not a variable a caller sets meaningfully — and it names a FIXED file
    /// under it. Pinned so that "machine-local" cannot quietly become "wherever this points today".
    /// </summary>
    [Fact]
    public void TheMachineAllowlistIsAFixedPath_UnderProgramData()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Ladder-AI",
            "download-probe.allowlist");

        Assert.Equal(expected, ScratchProjectGuard.MachineAllowlistPath);
    }

    // ---- the committed allowlist is real, and it names the repository's own scratch project -------

    /// <summary>
    /// The fence is only as good as the file behind it, so the committed file is read as data rather
    /// than assumed: it must exist, parse, and permit the S6 sandbox project.
    ///
    /// *** AND IT MUST NOT NAME A LIVE-RUN PROJECT. *** Nothing from <c>Live Runs/</c> may enter this
    /// repository in its own vocabulary (CLAUDE.md, docs/13) — a project whose path may not be
    /// committed belongs in the machine-local allowlist, which is outside the working tree precisely
    /// so no ignore rule, `git add -f` or repo-walking tool can publish it.
    /// </summary>
    [Fact]
    public void TheCommittedAllowlist_NamesTheSandboxProject_AndNoLiveRun()
    {
        var repoRoot = ScratchProjectGuard.FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var path = Path.Combine(repoRoot!, ScratchProjectGuard.RepoRelativeAllowlist);
        Assert.True(File.Exists(path), $"the committed allowlist is missing: {path}");

        var entries = File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(entries);
        Assert.Contains(entries, e => e.EndsWith("GenProject1/GenProject1.ap20", StringComparison.OrdinalIgnoreCase));

        foreach (var entry in entries)
        {
            Assert.DoesNotContain("Live Runs", entry, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadRepoSource(string relativePath)
    {
        var repoRoot = ScratchProjectGuard.FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var path = Path.Combine(repoRoot!, relativePath);
        Assert.True(File.Exists(path), $"source file not found: {path}");
        return File.ReadAllText(path);
    }
}
