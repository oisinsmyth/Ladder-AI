using System;
using System.Diagnostics;
using System.IO;
using DownloadProbe;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// PATH-FORM ATTACKS ON THE DEVICE-DOWNLOAD FENCE (hammer campaign, 2026-08-14).
///
/// <para><see cref="DownloadProbeAllowlistTests"/> covers the cases the fence was designed against:
/// casing, <c>..</c>, a directory, a bare name, a missing/empty/relative allowlist. It does NOT
/// cover the forms the fence's own documentation CLAIMS to handle — <b>junctions detected and
/// refused, 8.3 short names refused</b> — nor the ones nobody had asked about: a trailing separator,
/// forward slashes, an un-expanded <c>%VAR%</c>, a hard link, a <c>\\?\</c> device path.</para>
///
/// <para><b>An allowlist is only as good as its notion of sameness</b>, and the two directions are
/// not equally bad. A form that should be REFUSED and is PERMITTED is the failure this fence exists
/// to prevent; a form that should be PERMITTED and is refused is a lesser finding that must still be
/// recorded, because a fence people believe is broken is a fence people edit.</para>
///
/// <para>Every case here runs against <see cref="ProbeFenceRepo"/> and a stub session, so Portal is
/// never contacted and no download is ever possible. The permit-direction cases assert on
/// <see cref="ScratchProjectGuard"/> directly rather than through the binary, for the same reason.</para>
/// </summary>
public class DownloadProbeFencePathFormsTests
{
    // ---- refuse-direction: forms that must NOT reach the session ---------------------------------

    /// <summary>
    /// *** THE CLAIM THE FENCE MAKES AND NOTHING TESTED. *** ScratchProjectGuard's remarks say
    /// junctions are "DETECTED AND REFUSED rather than half-resolved", because .NET Framework 4.8 has
    /// no ResolveLinkTarget. A junction to the allowlisted folder is the sharpest case: the target
    /// really IS the allowlisted project, so a half-resolution would look correct.
    /// </summary>
    [Fact]
    public void AJunctionToTheAllowlistedFolder_IsRefused_NotHalfResolved()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var link = Path.Combine(repo.Root, "viaJunction");
        if (!TryCreateJunction(link, Path.GetDirectoryName(repo.ProjectPath)!))
        {
            Assert.Fail("SETUP: could not create a junction, so this claim was NOT tested. " +
                        "An untested fence claim is the finding, not a skip.");
        }

        try
        {
            var viaLink = Path.Combine(link, Path.GetFileName(repo.ProjectPath));
            var decision = ScratchProjectGuard.Evaluate(viaLink, repo.BinaryDirectory);

            Assert.False(decision.Permitted, "a junction reached the allowlisted project through an unresolved path");
            Assert.Contains("junction or symlink", decision.Text, StringComparison.Ordinal);
        }
        finally
        {
            // A recursive Directory.Delete cannot remove a junction on net48; the fixture's Dispose
            // would then throw and mask whatever this test actually found.
            RemoveJunction(link);
        }
    }

    /// <summary>
    /// The converse, and the one that would actually be exploited: a junction whose target is NOT
    /// allowlisted, sitting where an allowlisted path would be. Same refusal, different reason to
    /// care — here a half-resolution would permit a project nobody named.
    /// </summary>
    [Fact]
    public void AJunctionAwayFromTheAllowlistedFolder_IsRefused()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var elsewhere = Path.Combine(repo.Root, "Elsewhere");
        Directory.CreateDirectory(elsewhere);
        File.WriteAllText(Path.Combine(elsewhere, "Sandbox.ap20"), "(not the allowlisted project)");

        // The junction is NAMED like the allowlisted folder but points somewhere else.
        var link = Path.Combine(repo.Root, "SandboxLink");
        if (!TryCreateJunction(link, elsewhere))
        {
            Assert.Fail("SETUP: could not create a junction, so this claim was NOT tested.");
        }

        try
        {
            var decision = ScratchProjectGuard.Evaluate(
                Path.Combine(link, "Sandbox.ap20"), repo.BinaryDirectory);

            Assert.False(decision.Permitted);
            Assert.Contains("junction or symlink", decision.Text, StringComparison.Ordinal);
        }
        finally
        {
            RemoveJunction(link);
        }
    }

    /// <summary>
    /// A HARD LINK is a second name for the same bytes and carries no reparse point, so the fence
    /// cannot see it at all. It must still refuse — because the fence keys on the PATH, and the
    /// second name is not on the allowlist. Recorded so the limit is explicit: a hard link created
    /// AT an allowlisted path would be permitted, and nothing here detects that.
    /// </summary>
    [Fact]
    public void AHardLinkToTheAllowlistedProject_IsRefused_BecauseTheFenceKeysOnThePath()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var twin = Path.Combine(Path.GetDirectoryName(repo.ProjectPath)!, "Twin.ap20");
        if (!TryCreateHardLink(twin, repo.ProjectPath))
        {
            Assert.Fail("SETUP: could not create a hard link, so this claim was NOT tested.");
        }

        Assert.False(ScratchProjectGuard.Evaluate(twin, repo.BinaryDirectory).Permitted);
    }

    /// <summary>An un-expanded environment variable is not a path. GetFullPath does not expand it.</summary>
    [Fact]
    public void AnUnexpandedEnvironmentVariable_IsRefused()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var decision = ScratchProjectGuard.Evaluate(
            @"%TEMP%\Sandbox\Sandbox.ap20", repo.BinaryDirectory);

        Assert.False(decision.Permitted);
    }

    /// <summary>
    /// A trailing separator makes the leaf a directory reference. It must not be silently trimmed
    /// into a permit — the fence requires an existing .apNN LEAF, and this is not one.
    /// </summary>
    [Fact]
    public void ATrailingSeparator_IsRefused()
    {
        using var repo = ProbeFenceRepo.Permitting();

        Assert.False(ScratchProjectGuard.Evaluate(repo.ProjectPath + @"\", repo.BinaryDirectory).Permitted);
    }

    /// <summary>
    /// A component that merely LOOKS like an 8.3 short name is refused, even though it is a perfectly
    /// ordinary long directory name. That is the fail-closed direction and it is the right one — but
    /// it is a real false refusal and is pinned here so it is a known cost rather than a surprise.
    /// </summary>
    [Fact]
    public void ADirectoryWhoseRealNameContainsTilde1_IsRefusedAsAnEightDotThreeName()
    {
        using var repo = ProbeFenceRepo.Create(new[] { ScratchProjectGuard.RepoPrefix + @"Sand~1/Sandbox.ap20" });

        var dir = Path.Combine(repo.Root, "Sand~1");
        Directory.CreateDirectory(dir);
        var project = Path.Combine(dir, "Sandbox.ap20");
        File.WriteAllText(project, "(a project in a folder whose real name contains ~1)");

        var decision = ScratchProjectGuard.Evaluate(project, repo.BinaryDirectory);

        Assert.False(decision.Permitted);
        Assert.Contains("8.3 short name", decision.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A <c>\\?\</c> device path names the same file and bypasses Win32 path normalisation. It must
    /// not permit: the allowlist entry resolves without the prefix, so the strings differ and the
    /// fence fails closed. Pinned because the failure would be silent in the other direction.
    /// </summary>
    [Fact]
    public void ADeviceNamespacePath_IsRefused_NotSilentlyEquated()
    {
        using var repo = ProbeFenceRepo.Permitting();

        Assert.False(ScratchProjectGuard.Evaluate(@"\\?\" + repo.ProjectPath, repo.BinaryDirectory).Permitted);
    }

    // ---- permit-direction: forms that name the same file and should still work --------------------

    /// <summary>Forward slashes are the same path on Windows; GetFullPath normalises them.</summary>
    [Fact]
    public void ForwardSlashes_NameTheSameProject()
    {
        using var repo = ProbeFenceRepo.Permitting();

        Assert.True(ScratchProjectGuard.Evaluate(repo.ProjectPath.Replace('\\', '/'), repo.BinaryDirectory).Permitted);
    }

    /// <summary>A <c>.</c> segment is a no-op that GetFullPath collapses.</summary>
    [Fact]
    public void ADotSegment_NamesTheSameProject()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var viaDot = Path.Combine(
            Path.GetDirectoryName(repo.ProjectPath)!, ".", Path.GetFileName(repo.ProjectPath));

        Assert.True(ScratchProjectGuard.Evaluate(viaDot, repo.BinaryDirectory).Permitted);
    }

    /// <summary>
    /// Trailing whitespace. Windows strips it from a path, so this names the allowlisted project and
    /// a person would read it as the same argument. Whichever way the fence answers, it must be
    /// PINNED: an inconsistency between the existence check (which sees the file) and the extension
    /// check (which sees ".ap20 ") is exactly the kind of seam that reads as "the fence is broken".
    /// </summary>
    [Fact]
    public void TrailingWhitespace_IsRefused_BecauseTheExtensionCheckSeesTheSpace()
    {
        using var repo = ProbeFenceRepo.Permitting();

        var decision = ScratchProjectGuard.Evaluate(repo.ProjectPath + " ", repo.BinaryDirectory);

        Assert.False(decision.Permitted);
        Assert.Contains("not a .apNN project file", decision.Text, StringComparison.Ordinal);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static bool TryCreateJunction(string link, string target) =>
        RunCmd($"mklink /J \"{link}\" \"{target}\"") && Directory.Exists(link);

    // ---- the "repo:" prefix: CURRENT behaviour, pinned, deliberately not changed ------------------

    /// <summary>
    /// 🔴 <b>A <c>repo:</c> entry that is itself ROOTED escapes the repository root entirely</b>,
    /// because <see cref="Path.Combine"/> discards its first argument when the second is rooted. So
    /// <c>repo:C:\Somewhere\Job.ap20</c> permits a project nowhere near the repository, while
    /// reading — to anyone reviewing the allowlist — as though it were inside it.
    ///
    /// <para><b>This is NOT a caller-controlled bypass.</b> Whoever can write this line can equally
    /// write the bare absolute path, which the format supports outright. It is an ALLOWLIST-AUTHORING
    /// hazard, and the reason it is pinned rather than fixed is that redefining what an existing
    /// entry MEANS is an owner decision with a review behind it.</para>
    ///
    /// <para><b>The reason it matters is the divergence, not this case.</b> PowerShell's
    /// <c>Join-Path</c> does NOT discard, so <c>confirm-roundtrip.ps1</c> reads the same line as
    /// <c>C:\&lt;repo&gt;\C:\Somewhere\Job.ap20</c> — a nonsense path that never matches. One format,
    /// two fences, two meanings. Both fail closed today because nothing sits at either location:
    /// that is luck, and this test is what turns the luck into a recorded fact.</para>
    /// </summary>
    [Fact]
    public void ARepoPrefixedEntryThatIsItselfRooted_EscapesTheRepositoryRoot()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        try
        {
            var outside = Path.Combine(outsideDir, "Job.ap20");
            File.WriteAllText(outside, "(a project OUTSIDE the repository, named by a repo: entry)");

            using var repo = ProbeFenceRepo.Create(new[] { ScratchProjectGuard.RepoPrefix + outside });

            var decision = ScratchProjectGuard.Decide(outside, new[] { repo.AllowlistPath }, repo.Root);

            Assert.True(
                decision.Permitted,
                "CURRENT behaviour is that a rooted repo: entry escapes the repo root. If this now " +
                "REFUSES, the semantics were changed — which may well be right, but it is a decision " +
                "that needs the allowlist comments and both fences updated together, not a silent fix.");
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    /// <summary>
    /// The likeliest typo in the format, and the sharper half of the divergence: a LEADING
    /// BACKSLASH. <c>Path.Combine</c> yields <c>\GenProject1\…</c>, which
    /// <see cref="Path.GetFullPath"/> then resolves against the CURRENT DRIVE ROOT — not the
    /// repository. PowerShell's <c>Join-Path</c> yields the repo-relative path the author meant.
    /// Pinned so the disagreement cannot quietly change on either side.
    /// </summary>
    [Fact]
    public void ARepoPrefixedEntryWithALeadingSeparator_ResolvesAgainstTheDriveRoot_NotTheRepo()
    {
        using var repo = ProbeFenceRepo.Create(new[] { ScratchProjectGuard.RepoPrefix + @"\Sandbox\Sandbox.ap20" });

        // The fixture's real project is <root>\Sandbox\Sandbox.ap20. If the entry resolved against
        // the repo root — as its author intended and as the PowerShell fence does — this would be
        // permitted. It is not, because it resolved against the drive root instead.
        var decision = ScratchProjectGuard.Decide(repo.ProjectPath, new[] { repo.AllowlistPath }, repo.Root);

        Assert.False(
            decision.Permitted,
            "CURRENT behaviour is that a leading separator after repo: escapes to the drive root. " +
            "If this now PERMITS, the two fences have been brought into agreement — good, but update " +
            "both allowlist files' comments in the same change.");
        Assert.Contains(@"\Sandbox\Sandbox.ap20", decision.Text, StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveJunction(string link)
    {
        if (Directory.Exists(link))
        {
            RunCmd($"rmdir \"{link}\"");
        }
    }

    private static bool TryCreateHardLink(string link, string target) =>
        RunCmd($"mklink /H \"{link}\" \"{target}\"") && File.Exists(link);

    private static bool RunCmd(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });

            process!.WaitForExit(20_000);
            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
