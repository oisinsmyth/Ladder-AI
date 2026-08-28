using System.IO;
using DownloadProbe;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// THE <c>root:</c> ENTRY FORM (2026-08-28), added on the owner's instruction that the download fence
/// must stop demanding a per-project edit. A <c>root:</c> entry names a DIRECTORY and permits every
/// <c>.apNN</c> at or under it, so a new project in an already-named area needs no config change.
///
/// <para>It is ADDITIVE by design: an entry without the prefix still means exactly the one file it
/// always meant. The allowlist's own header requires that — redefining what an existing entry means
/// is an owner decision with a review behind it, and widening a fence is not the moment to also
/// change what its existing lines say. <see cref="EntryFormsCoexist_AndAPlainEntryStillMeansOneFile"/>
/// is that guarantee.</para>
///
/// <para>🔴 <b>THE SIBLING-DIRECTORY TEST IS THE POINT OF THIS FILE.</b> The whole risk this fence
/// exists for is that this machine carries about nineteen private engineering projects in folders BESIDE
/// the scratch ones. A root implemented as a bare <c>StartsWith</c> would make a root of
/// <c>…\Sand</c> permit <c>…\Sandbox\x.ap20</c> — a different directory whose name merely begins the
/// same way — which is precisely the neighbouring-folder case. The separator is the entire check.</para>
/// </summary>
public class DownloadProbeRootEntryTests
{
    [Fact]
    public void ARootEntry_PermitsAProjectBeneathIt_WithNoEntryNamingThatProject()
    {
        using var repo = ProbeFenceRepo.Create(new[] { "root:repo:Sandbox" });

        var decision = ScratchProjectGuard.Decide(repo.ProjectPath, new[] { repo.AllowlistPath }, repo.Root);

        Assert.True(decision.Permitted);
        Assert.Contains("UNDER a permitted root", decision.Text, System.StringComparison.Ordinal);
    }

    // 🔴 The hole a naive prefix comparison leaves. `Sand` is not `Sandbox`, and on this machine the
    // difference between a directory and its similarly-named neighbour is the difference between the
    // bench rig and a site's plant.
    [Fact]
    public void ARootThatIsOnlyANAMEPREFIX_OfTheProjectsDirectory_DoesNotMatch()
    {
        using var repo = ProbeFenceRepo.Create(new[] { "root:repo:Sand" });

        var decision = ScratchProjectGuard.Decide(repo.ProjectPath, new[] { repo.AllowlistPath }, repo.Root);

        Assert.False(decision.Permitted);
        Assert.Contains("not on the allowlist", decision.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ARootElsewhere_DoesNotPermitThisProject()
    {
        using var repo = ProbeFenceRepo.Create(new[] { "root:repo:SomewhereElse" });

        var decision = ScratchProjectGuard.Decide(repo.ProjectPath, new[] { repo.AllowlistPath }, repo.Root);

        Assert.False(decision.Permitted);
    }

    // A root that names nothing would either permit everything or nothing, and neither is a claim
    // anybody made. Refusing is the only honest reading — consistent with every other malformed-entry
    // case in this fence, which refuses rather than skipping the line.
    [Fact]
    public void ABareRootPrefixWithNoDirectory_IsARefusal()
    {
        using var repo = ProbeFenceRepo.Create(new[] { "root:" });

        var decision = ScratchProjectGuard.Decide(repo.ProjectPath, new[] { repo.AllowlistPath }, repo.Root);

        Assert.False(decision.Permitted);
        Assert.Contains("no directory after it", decision.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public void AnAbsoluteRoot_Works_TheSameAsARepoRelativeOne()
    {
        using var repo = ProbeFenceRepo.Create(null);
        var projectDirectory = Path.GetDirectoryName(repo.ProjectPath)!;
        File.WriteAllLines(repo.AllowlistPath, new[] { "root:" + projectDirectory });

        var decision = ScratchProjectGuard.Decide(repo.ProjectPath, new[] { repo.AllowlistPath }, repo.Root);

        Assert.True(decision.Permitted);
    }

    // THE ADDITIVE GUARANTEE. Both forms in one file: the root permits its subtree, and the plain
    // entry still permits exactly one file and nothing near it.
    [Fact]
    public void EntryFormsCoexist_AndAPlainEntryStillMeansOneFile()
    {
        using var repo = ProbeFenceRepo.Create(new[]
        {
            "root:repo:SomewhereElse",
            "repo:Sandbox\\Sandbox.ap20",
        });

        Assert.True(ScratchProjectGuard.Decide(repo.ProjectPath, new[] { repo.AllowlistPath }, repo.Root).Permitted);

        // A sibling file inside the SAME directory is not permitted by that plain entry — the exact
        // form did not quietly become a directory claim when the root form arrived beside it.
        var sibling = Path.Combine(Path.GetDirectoryName(repo.ProjectPath)!, "Neighbour.ap20");
        File.WriteAllText(sibling, "(a different project)");

        Assert.False(ScratchProjectGuard.Decide(sibling, new[] { repo.AllowlistPath }, repo.Root).Permitted);
    }

    // A root does not turn the fence into a wildcard: a project OUTSIDE every named root is still
    // refused, which is the property protecting the private projects in sibling folders.
    [Fact]
    public void AProjectOutsideEveryRoot_IsStillRefused()
    {
        using var repo = ProbeFenceRepo.Create(new[] { "root:repo:Sandbox" });

        var outside = Path.Combine(repo.Root, "NotSandbox");
        Directory.CreateDirectory(outside);
        var project = Path.Combine(outside, "Other.ap20");
        File.WriteAllText(project, "(a project nobody named)");

        var decision = ScratchProjectGuard.Decide(project, new[] { repo.AllowlistPath }, repo.Root);

        Assert.False(decision.Permitted);
    }
}
