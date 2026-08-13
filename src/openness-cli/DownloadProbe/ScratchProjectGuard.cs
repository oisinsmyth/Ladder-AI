using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DownloadProbe;

/// <summary>
/// The refusal that makes this tool incapable of downloading to a project nobody named.
///
/// *** IT IS AN ALLOWLIST OF RESOLVED PATHS, NOT A FILE-NAME CONVENTION (2026-08-13). *** It used to
/// be a suffix match on the file name — <c>" scratch.ap20"</c> — and that was wrong in both
/// directions, measured:
///
///   * IT BLOCKED THE LEGITIMATE CASE. The S6 sandbox project is deliberately called
///     <c>GenProject1.ap20</c> on disk (CLAUDE.md, "Codename note": renaming a live Openness-managed
///     project folder was judged not worth the risk for a naming-only change), and
///     <c>SampleProject.ap20</c> is the other allowlisted scratch project. Neither could ever be the
///     target of a download, however deliberately somebody chose it.
///   * IT DID NOT STOP THE DANGEROUS CASE. A file-name suffix is a CONVENTION, and anything can be
///     renamed into one. This engineering PC carries about NINETEEN real site <c>.ap20</c>
///     projects, and a fence any of them could satisfy by rename is not protecting them.
///
/// The pattern is ADR-0011's, deliberately, because its properties were argued for and measured
/// there: ALLOWLIST NEVER DENYLIST (a denylist would have to be complete, and stops being complete
/// the next time a job folder arrives); entries absolute or <c>repo:</c>-prefixed so a committed file
/// is portable across worktrees; the check runs BEFORE anything else so Portal is never contacted on
/// a refusal; junctions DETECTED AND REFUSED rather than half-resolved; and NO OVERRIDE — no flag, no
/// environment variable, no argument that names a different allowlist.
///
/// *** TWO FILES, AND THE SECOND ONE IS NOT AN ESCAPE HATCH — IT IS THE DATA BOUNDARY. *** The old
/// guard's own comment gave the real reason it was a suffix rather than a list of paths, and it still
/// holds: <b>a live engineering job's path may not be written into this repository</b> (CLAUDE.md,
/// "Live runs": use anything, commit nothing). The rig's scratch project is a copy of a live job, so
/// its path cannot go in <see cref="RepoRelativeAllowlist"/>. It therefore goes in a MACHINE-LOCAL
/// file at a FIXED path outside the repo (<see cref="MachineAllowlistPath"/>) — which is still a
/// decision somebody makes once, in a file, not a flag anyone can pass. Both files are read and
/// neither is nameable from the command line.
/// </summary>
internal static class ScratchProjectGuard
{
    /// <summary>The allowlist's file name, identical in both locations.</summary>
    internal const string AllowlistFileName = "download-probe.allowlist";

    /// <summary>Where the committed allowlist lives, relative to the repository root.</summary>
    internal const string RepoRelativeAllowlist = @"tools\" + AllowlistFileName;

    /// <summary>ADR-0011's portable entry form: resolved against the repository root.</summary>
    internal const string RepoPrefix = "repo:";

    /// <summary>
    /// The machine-local allowlist, for a scratch project whose PATH MAY NOT BE COMMITTED — which on
    /// this machine is the rig's project, a copy of a live engineering job. A fixed path: there is no
    /// flag, no environment variable and no argument that changes it, so it is a file the owner
    /// writes once and not something a run can point somewhere else.
    /// </summary>
    internal static string MachineAllowlistPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Ladder-AI",
        AllowlistFileName);

    /// <summary>
    /// The live entry point: locate the allowlists from where this executable actually sits, and
    /// decide. Every input is discovered, none is supplied by the caller — see the class remarks.
    /// </summary>
    internal static GuardDecision Evaluate(string? projectPath) =>
        Evaluate(projectPath, AppDomain.CurrentDomain.BaseDirectory);

    /// <summary>
    /// The same decision with the starting directory injected, so the whole fence — including
    /// repository-root discovery and the "no allowlist anywhere" refusal — is testable without
    /// relying on where the test runner happens to live.
    /// </summary>
    internal static GuardDecision Evaluate(string? projectPath, string? binaryDirectory)
    {
        var repoRoot = FindRepoRoot(binaryDirectory);

        var sources = new List<string>();
        if (repoRoot is not null)
        {
            sources.Add(Path.Combine(repoRoot, RepoRelativeAllowlist));
        }

        sources.Add(MachineAllowlistPath);

        return Decide(projectPath, sources, repoRoot, binaryDirectory);
    }

    /// <summary>
    /// Walks up from <paramref name="start"/> looking for a <c>.git</c> entry. A git WORKTREE's
    /// <c>.git</c> is a FILE rather than a directory, and <see cref="File.Exists"/> plus
    /// <see cref="Directory.Exists"/> covers both — a worktree build therefore resolves
    /// <c>repo:</c> entries against its own checkout, which is the property ADR-0011 wanted.
    /// </summary>
    internal static string? FindRepoRoot(string? start)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            return null;
        }

        string? probe;
        try
        {
            probe = Path.GetFullPath(start!);
        }
        catch (Exception)
        {
            return null;
        }

        while (!string.IsNullOrEmpty(probe))
        {
            var marker = Path.Combine(probe!, ".git");
            if (Directory.Exists(marker) || File.Exists(marker))
            {
                return probe;
            }

            var parent = Path.GetDirectoryName(probe!);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, probe, StringComparison.Ordinal))
            {
                return null;
            }

            probe = parent;
        }

        return null;
    }

    /// <summary>
    /// THE FENCE. Reads every allowlist that exists, resolves every entry, and compares the resolved
    /// project path against them.
    ///
    /// EVERY FAILURE HERE IS A REFUSAL, NEVER A SKIP (ADR-0011 requirement 4, FI-44 "empty is not
    /// clean"): no allowlist file, no entries, a malformed entry, an unresolvable path, a junction,
    /// an 8.3 short name, a project that is a directory. If the fence cannot positively identify the
    /// target, it has verified nothing — and an unverified target is not a permitted one.
    /// </summary>
    internal static GuardDecision Decide(
        string? projectPath,
        IReadOnlyList<string> allowlistPaths,
        string? repoRoot,
        string? binaryDirectory = null)
    {
        var present = allowlistPaths.Where(File.Exists).ToList();
        if (present.Count == 0)
        {
            return GuardDecision.Deny(
                projectPath,
                new[]
                {
                    "No allowlist exists, so this fence has verified NOTHING — which is a refusal, not a pass.",
                    "Looked for:",
                }
                .Concat(allowlistPaths.Select(p => "    " + p))
                .Concat(RepoRootNote(repoRoot, binaryDirectory))
                .Concat(HowToPermit(repoRoot))
                .ToList());
        }

        var entries = new List<AllowlistEntry>();
        foreach (var file in present)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch (Exception ex)
            {
                return GuardDecision.Deny(
                    projectPath,
                    new[]
                    {
                        $"The allowlist '{file}' could not be read: {ex.GetType().Name}: {ex.Message}",
                        "An allowlist that cannot be read permits nothing.",
                    });
            }

            foreach (var line in lines)
            {
                var raw = line.Trim();
                if (raw.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var value = raw;
                if (raw.StartsWith(RepoPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (repoRoot is null)
                    {
                        return GuardDecision.Deny(
                            projectPath,
                            new[]
                            {
                                $"Allowlist entry '{raw}' in '{file}' is repo-relative, and the repository root",
                                "could not be located from this executable, so it cannot be resolved.",
                            }.Concat(RepoRootNote(repoRoot, binaryDirectory)).ToList());
                    }

                    value = Path.Combine(repoRoot, raw.Substring(RepoPrefix.Length).Trim());
                }

                if (!Path.IsPathRooted(value))
                {
                    return GuardDecision.Deny(
                        projectPath,
                        new[]
                        {
                            $"Allowlist entry '{raw}' in '{file}' is a bare relative path.",
                            "It would resolve differently depending on the working directory, which is exactly the",
                            $"ambiguity this fence refuses. Use an absolute path, or the '{RepoPrefix}' prefix.",
                        });
                }

                var resolved = Canonicalise(value, out var problem);
                if (resolved is null)
                {
                    return GuardDecision.Deny(
                        projectPath,
                        new[] { $"Allowlist entry '{raw}' in '{file}' could not be canonicalised: {problem}" });
                }

                entries.Add(new AllowlistEntry(raw, resolved, file));
            }
        }

        if (entries.Count == 0)
        {
            return GuardDecision.Deny(
                projectPath,
                new[] { "Every allowlist found is empty (comments and blank lines only):" }
                    .Concat(present.Select(p => "    " + p))
                    .Concat(new[] { "An empty allowlist permits nothing. This is a refusal by construction." })
                    .Concat(HowToPermit(repoRoot))
                    .ToList());
        }

        // An entry naming a project that is not on this machine simply never matches. That fails
        // CLOSED, which is the safe direction, so it is not itself an error.
        var permitted = entries.Select(e => $"    {e.Raw}  ->  {e.Resolved}   [{e.Source}]").ToList();

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return GuardDecision.Deny(projectPath, new[] { "No project path was given." }, permitted);
        }

        // *** IT MUST BE A FILE, AND THE EXTENSION IS CHECKED. *** ADR-0011 decision 4, measured on
        // the confirm loop: a bare project name RESOLVES TO A DIRECTORY (the project folder exists),
        // so an existence check alone passes and the fence then refuses with the wrong reason. A
        // guard that explains itself wrongly is how somebody concludes it is broken and goes looking
        // for a way around it.
        var project = projectPath!;
        var isProjectFile = File.Exists(project) && IsProjectExtension(project);
        if (!isProjectFile)
        {
            var what = Directory.Exists(project) ? "it is a DIRECTORY, not a project file"
                : File.Exists(project) ? "it exists but is not a .apNN project file"
                : "no such file exists";

            return GuardDecision.Deny(
                projectPath,
                new[]
                {
                    $"The project given is not a project file: {what}.",
                    "This fence needs the PATH TO THE .apNN FILE, not a project name and not the project folder:",
                    "a name cannot be resolved to a canonical path, so the target cannot be positively identified.",
                },
                permitted);
        }

        var projectResolved = Canonicalise(project, out var projectProblem);
        if (projectResolved is null)
        {
            return GuardDecision.Deny(
                projectPath,
                new[] { $"The project path could not be canonicalised: {projectProblem}" },
                permitted);
        }

        // Junctions and symlinks: .NET Framework 4.8 has no ResolveLinkTarget, so a reparse point
        // cannot be resolved reliably from here. DETECTED AND REFUSED rather than half-resolved —
        // "I could not canonicalise this" is an honest refusal, and a comparison that silently
        // ignored a junction would be a fence with a documented way through it (ADR-0011 decision 3).
        var reparse = FindReparsePoint(projectResolved);
        if (reparse is not null)
        {
            return GuardDecision.Deny(
                projectPath,
                new[]
                {
                    "The project is reached through a junction or symlink:",
                    "    " + reparse,
                    "That path cannot be canonicalised on this toolchain, and this fence will not compare a path",
                    "it could not canonicalise. Supply the real path, or allowlist the real path.",
                },
                permitted);
        }

        foreach (var entry in entries)
        {
            if (string.Equals(projectResolved, entry.Resolved, StringComparison.OrdinalIgnoreCase))
            {
                return GuardDecision.Permit(
                    projectResolved,
                    entry,
                    new[]
                    {
                        "SCRATCH FENCE: PERMITTED.",
                        $"  project resolves to : {projectResolved}",
                        $"  allowlist entry     : {entry.Raw}",
                        $"  from                : {entry.Source}",
                    });
            }
        }

        return GuardDecision.Deny(
            projectPath,
            new[]
            {
                "This project is not on the allowlist.",
                $"  as given    : {projectPath}",
                $"  resolves to : {projectResolved}",
            },
            permitted);
    }

    private static IReadOnlyList<string> RepoRootNote(string? repoRoot, string? binaryDirectory) =>
        repoRoot is null
            ? new[]
            {
                $"The repository root could not be located from '{binaryDirectory ?? "(unknown)"}' (no .git found",
                "walking upward), so the committed allowlist could not be looked for at all.",
            }
            : new[] { $"Repository root: {repoRoot}" };

    private static IReadOnlyList<string> HowToPermit(string? repoRoot)
    {
        var lines = new List<string>
        {
            string.Empty,
            "TO PERMIT A PROJECT, add its path to one of those files, one per line:",
            $"  {RepoPrefix}<path>   resolved against the repository root — use this for a project INSIDE the repo,",
            "                so the entry stays correct in every checkout and worktree.",
            "  <absolute>    a fully-qualified path.",
            string.Empty,
            "A project whose PATH MAY NOT BE COMMITTED — a copy of a live engineering job, which is what the",
            "rig's scratch project is — goes in the MACHINE-LOCAL file instead, never in the repository one",
            "(CLAUDE.md, \"Live runs\": use anything, commit nothing):",
            $"  {MachineAllowlistPath}",
            string.Empty,
            "*** THERE IS NO OVERRIDE. *** No flag, no environment variable, no argument names a different",
            "allowlist. Adding a project is a decision, made once, with the restore point named.",
        };

        if (repoRoot is null)
        {
            lines.Add("(The repository allowlist was not reachable on this run — only the machine-local one.)");
        }

        return lines;
    }

    private static bool IsProjectExtension(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension is null || extension.Length < 4 || !extension.StartsWith(".ap", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return extension.Substring(3).All(char.IsDigit);
    }

    /// <summary>
    /// Compare the RESOLVED path, never the string as typed, so a relative path, a different casing
    /// or a <c>..</c> cannot walk around the fence (ADR-0011 requirement 5). 8.3 short names are
    /// refused for the same reason junctions are: <see cref="Path.GetFullPath"/> does not expand
    /// them, so the result would be canonical-looking and wrong.
    /// </summary>
    private static string? Canonicalise(string path, out string? problem)
    {
        problem = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            problem = "the path is empty";
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path.Trim());
        }
        catch (Exception ex)
        {
            problem = $"{ex.GetType().Name}: {ex.Message}";
            return null;
        }

        full = full.TrimEnd('\\', '/');

        foreach (var component in full.Split('\\'))
        {
            if (component.Contains("~") && component.Any(char.IsDigit))
            {
                problem = $"'{component}' looks like an 8.3 short name, which cannot be canonicalised from here — supply the long path";
                return null;
            }
        }

        return full;
    }

    /// <summary>The path or the first ancestor of it that is a reparse point, or null.</summary>
    private static string? FindReparsePoint(string fullPath)
    {
        var probe = fullPath;
        while (!string.IsNullOrEmpty(probe))
        {
            try
            {
                if (File.Exists(probe) || Directory.Exists(probe))
                {
                    var attributes = File.GetAttributes(probe);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return probe;
                    }
                }
            }
            catch (Exception)
            {
                // Unreadable attributes are not evidence of a reparse point, and the path comparison
                // below is the thing that decides. Keep walking.
            }

            var parent = Path.GetDirectoryName(probe);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, probe, StringComparison.Ordinal))
            {
                return null;
            }

            probe = parent!;
        }

        return null;
    }
}

/// <summary>One allowlist line, as written and as resolved, with the file it came from.</summary>
internal sealed class AllowlistEntry
{
    internal AllowlistEntry(string raw, string resolved, string source)
    {
        Raw = raw;
        Resolved = resolved;
        Source = source;
    }

    internal string Raw { get; }

    internal string Resolved { get; }

    internal string Source { get; }
}

/// <summary>
/// The fence's answer. A refusal carries its whole report — the reason, what was looked for, and
/// every permitted entry — because a guard that explains itself wrongly, or not at all, is how
/// somebody concludes it is broken and goes looking for a way around it.
/// </summary>
internal sealed class GuardDecision
{
    private GuardDecision(bool permitted, IReadOnlyList<string> lines, AllowlistEntry? matched, string? resolvedProjectPath)
    {
        Permitted = permitted;
        Lines = lines;
        MatchedEntry = matched;
        ResolvedProjectPath = resolvedProjectPath;
    }

    internal bool Permitted { get; }

    internal AllowlistEntry? MatchedEntry { get; }

    internal string? ResolvedProjectPath { get; }

    internal IReadOnlyList<string> Lines { get; }

    internal string Text => string.Join(Environment.NewLine, Lines);

    internal static GuardDecision Permit(string resolved, AllowlistEntry entry, IReadOnlyList<string> lines) =>
        new(true, lines, entry, resolved);

    internal static GuardDecision Deny(
        string? projectPath, IReadOnlyList<string> reason, IReadOnlyList<string>? permitted = null)
    {
        var lines = new List<string>
        {
            $"REFUSED: '{projectPath}' is not an allowlisted scratch project.",
            string.Empty,
        };

        lines.AddRange(reason);

        if (permitted is { Count: > 0 })
        {
            lines.Add(string.Empty);
            lines.Add("Permitted:");
            lines.AddRange(permitted);
        }

        lines.Add(string.Empty);
        lines.Add("download-probe performs a REAL DEVICE DOWNLOAD. It is deliberately incapable of doing so");
        lines.Add("against a project nobody named — this machine carries private engineering projects beside the");
        lines.Add("scratch ones, and a fence a rename could satisfy would not be protecting them.");
        lines.Add("Portal was NOT contacted, no project was opened, and nothing was written.");

        return new GuardDecision(false, lines, matched: null, resolvedProjectPath: null);
    }
}
