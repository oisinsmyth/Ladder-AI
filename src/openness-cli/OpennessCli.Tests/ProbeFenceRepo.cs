using System;
using System.Collections.Generic;
using System.IO;

using DownloadProbe;

namespace OpennessCli.Tests;

/// <summary>
/// A throwaway repository — a <c>.git</c> marker, a <c>tools/</c> allowlist and a project file — for
/// exercising `download-probe`'s device-download fence.
///
/// IT HAS TO BE REAL FILES. Since 2026-08-13 the fence compares RESOLVED PATHS against an allowlist
/// and requires the project to be an existing <c>.apNN</c> leaf, so an invented string like
/// <c>C:\work\Thing scratch.ap20</c> can no longer stand in for a project — it is refused before any
/// of the behaviour under test is reached. Every test that drives <c>Program.Run</c> past the fence
/// needs one of these.
///
/// The scratch project the rig actually uses is a copy of a live engineering job, and its name may not
/// enter this repository (CLAUDE.md, "Live runs": use anything, commit nothing) — which is why the
/// fixture invents its own project rather than naming a real one.
/// </summary>
internal sealed class ProbeFenceRepo : IDisposable
{
    private ProbeFenceRepo(string root, string projectPath, string allowlistPath)
    {
        Root = root;
        ProjectPath = projectPath;
        AllowlistPath = allowlistPath;

        // The binary lives where a build output would: several levels down, inside the repo.
        BinaryDirectory = Path.Combine(root, "src", "openness-cli", "DownloadProbe", "bin", "Release", "net48");
        Directory.CreateDirectory(BinaryDirectory);
    }

    internal string Root { get; }

    /// <summary>The project file. Allowlisted iff the repo was created by <see cref="Permitting"/>.</summary>
    internal string ProjectPath { get; }

    internal string AllowlistPath { get; }

    /// <summary>Stands in for the directory <c>download-probe.exe</c> was built into.</summary>
    internal string BinaryDirectory { get; }

    /// <param name="entries">
    /// Allowlist lines exactly as written. Null means "write no allowlist file at all", which is a
    /// different case from an empty one and must refuse for a different reason.
    /// </param>
    internal static ProbeFenceRepo Create(IEnumerable<string>? entries, string projectFileName = "Sandbox.ap20")
    {
        var root = Path.Combine(Path.GetTempPath(), "probe-fence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        // A git WORKTREE's .git is a FILE, not a directory, and the fence must find both. A file is
        // used here deliberately: it is the case a naive Directory.Exists check would miss.
        File.WriteAllText(Path.Combine(root, ".git"), "gitdir: somewhere-else");

        var projectDir = Path.Combine(root, "Sandbox");
        Directory.CreateDirectory(projectDir);
        var projectPath = Path.Combine(projectDir, projectFileName);
        File.WriteAllText(projectPath, "(a stand-in for a TIA project file)");

        var tools = Path.Combine(root, "tools");
        Directory.CreateDirectory(tools);
        var allowlist = Path.Combine(tools, ScratchProjectGuard.AllowlistFileName);
        if (entries is not null)
        {
            File.WriteAllLines(allowlist, entries);
        }

        return new ProbeFenceRepo(root, projectPath, allowlist);
    }

    /// <summary>An allowlist naming this fixture's own project, and nothing else.</summary>
    internal static ProbeFenceRepo Permitting(string projectFileName = "Sandbox.ap20") =>
        Create(
            new[]
            {
                "# a comment, ignored",
                string.Empty,
                ScratchProjectGuard.RepoPrefix + "Sandbox/" + projectFileName,
            },
            projectFileName);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A leaked temp directory is not a test failure.
        }
    }
}
