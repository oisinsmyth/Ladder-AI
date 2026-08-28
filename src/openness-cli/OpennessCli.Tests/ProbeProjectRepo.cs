using System;
using System.IO;

namespace OpennessCli.Tests;

/// <summary>
/// A throwaway repository on disk with a stand-in project file, for tests that need
/// <c>download-probe</c> pointed at something plausible.
///
/// <para>Replaces <c>ProbeFenceRepo</c>, which also wrote an allowlist. The project fence was removed
/// on 2026-08-28 (ADR-0013), so there is no allowlist to write and nothing to be permitted BY —
/// hence the rename: a fixture called "fence" that no longer builds a fence would be the kind of
/// stale name that has already cost this project real time. The old <c>Permitting()</c> factory kept
/// its name for exactly one reason and it is gone with it: <b>every project is permitted now.</b></para>
/// </summary>
internal sealed class ProbeProjectRepo : IDisposable
{
    private ProbeProjectRepo(string root, string projectPath)
    {
        Root = root;
        ProjectPath = projectPath;

        // The binary lives where a build output would: several levels down, inside the repo.
        BinaryDirectory = Path.Combine(root, "src", "openness-cli", "DownloadProbe", "bin", "Release", "net48");
        Directory.CreateDirectory(BinaryDirectory);
    }

    internal string Root { get; }

    /// <summary>The stand-in project file. Nothing restricts it — see the class remarks.</summary>
    internal string ProjectPath { get; }

    /// <summary>Stands in for the directory <c>download-probe.exe</c> was built into.</summary>
    internal string BinaryDirectory { get; }

    internal static ProbeProjectRepo Create(string projectFileName = "Sandbox.ap20")
    {
        var root = Path.Combine(Path.GetTempPath(), "probe-project-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        // A git WORKTREE's .git is a FILE, not a directory. Kept as a file deliberately: it is the
        // case a naive Directory.Exists check misses, and RepoRoot.Find must handle both.
        File.WriteAllText(Path.Combine(root, ".git"), "gitdir: somewhere-else");

        var projectDir = Path.Combine(root, "Sandbox");
        Directory.CreateDirectory(projectDir);
        var projectPath = Path.Combine(projectDir, projectFileName);
        File.WriteAllText(projectPath, "(a stand-in for a TIA project file)");

        return new ProbeProjectRepo(root, projectPath);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception)
        {
            // A temp directory that will not delete is not a test failure.
        }
    }
}
