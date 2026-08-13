namespace Harness.Device;

/// <summary>
/// The gateway's own write fence: <b>an ALLOWLIST OF RESOLVED PATHS, not a file-name convention.</b>
///
/// <para>It used to be a suffix match on <c>" scratch.ap20"</c>, mirroring <c>download-probe</c>'s guard
/// as that guard then stood. <b>The openness-cli lane replaced its guard with an allowlist on
/// 2026-08-13 and measured the suffix wrong in both directions</b>, and both arguments apply verbatim
/// here because this gateway also writes:</para>
/// <list type="bullet">
/// <item>It BLOCKED THE LEGITIMATE CASE — the S6 sandbox project is deliberately <c>GenProject1.ap20</c>
/// on disk, so it could never be a target however deliberately it was chosen.</item>
/// <item>It DID NOT STOP THE DANGEROUS CASE — a file-name suffix is a CONVENTION, and any of the ~19
/// private engineering projects on this machine could satisfy it by rename.</item>
/// </list>
///
/// <para>ADR-0011's pattern, deliberately: allowlist never denylist; entries absolute or
/// <c>repo:</c>-prefixed so the committed file is portable across worktrees; checked BEFORE anything
/// runs; and <b>no override</b> — no flag, no environment variable, no argument naming a different
/// allowlist. <b>No allowlist found anywhere is a REFUSAL</b>, never a pass: an absent fence must not
/// read as an open one.</para>
///
/// <para><b>Two files, and the second is the data boundary rather than an escape hatch.</b> A live
/// live job's path may not be written into this repository (CLAUDE.md, "Live runs": use anything,
/// commit nothing), and the rig's project is a copy of one — so its path goes in a machine-local file at
/// a fixed path outside the repo. Both are read; neither is nameable from a command line.</para>
///
/// <para><b>Deliberately a SECOND implementation of the same fence, not a shared one.</b> This assembly
/// is net8.0 and <c>download-probe</c> is net48, so they cannot share code. The constants are pinned
/// against that binary's own source by <c>ScratchFenceTests</c>, so a divergence is a red test rather
/// than a gateway that imports into a project the probe would then refuse to download to.</para>
/// </summary>
public static class ScratchAllowlist
{
    /// <summary>The allowlist's file name, identical in both locations.</summary>
    public const string AllowlistFileName = "download-probe.allowlist";

    /// <summary>Where the committed allowlist lives, relative to the repository root.</summary>
    public const string RepoRelativeAllowlist = @"tools\" + AllowlistFileName;

    /// <summary>ADR-0011's portable entry form: resolved against the repository root.</summary>
    public const string RepoPrefix = "repo:";

    /// <summary>The machine-local allowlist. A fixed path — no flag and no environment variable moves it.</summary>
    public static string MachineAllowlistPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Ladder-AI",
        AllowlistFileName);

    /// <summary>Whether this project may be written to, and why not when it may not.</summary>
    public sealed record Decision(bool Allowed, string Reason, IReadOnlyList<string> Consulted, int EntriesRead);

    /// <summary>Decide, locating the allowlists from where this assembly sits.</summary>
    public static Decision Evaluate(string? projectPath) => Evaluate(projectPath, AppContext.BaseDirectory);

    /// <summary>The same decision with the starting directory injected, so the whole fence is testable.</summary>
    public static Decision Evaluate(string? projectPath, string? startDirectory)
    {
        var consulted = new List<string>();

        if (string.IsNullOrWhiteSpace(projectPath))
            return new Decision(false, "no project path was given. This fence answers 'no' whenever it cannot answer 'yes'.", consulted, 0);

        string target;
        try
        {
            target = Path.GetFullPath(projectPath!.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new Decision(false, $"'{projectPath}' is not a usable path ({ex.GetType().Name}), so it is not an allowlisted one.", consulted, 0);
        }

        var repoRoot = FindRepositoryRoot(startDirectory);
        var entries = new List<string>();

        if (repoRoot is not null)
        {
            var repoList = Path.Combine(repoRoot, RepoRelativeAllowlist.Replace('\\', Path.DirectorySeparatorChar));
            consulted.Add(repoList);
            entries.AddRange(ReadEntries(repoList, repoRoot));
        }

        consulted.Add(MachineAllowlistPath);
        entries.AddRange(ReadEntries(MachineAllowlistPath, repoRoot));

        if (entries.Count == 0)
        {
            return new Decision(false,
                "NO ALLOWLIST ENTRY WAS FOUND ANYWHERE, so nothing is allowlisted and this is a refusal — an absent fence must never read as an open one. "
                + $"Consulted: {string.Join(" ; ", consulted)}. " + HowToGrant,
                consulted, 0);
        }

        var allowed = entries.Any(e => string.Equals(e, target, StringComparison.OrdinalIgnoreCase));

        return new Decision(allowed,
            allowed
                ? $"'{target}' is named in an allowlist ({entries.Count} entr(ies) read)."
                : $"'{target}' IS NOT AN ALLOWLISTED PROJECT. This gateway imports, compiles and downloads; adding a project is a decision made once in a file, never a flag. "
                  + $"{entries.Count} entr(ies) read from: {string.Join(" ; ", consulted)}. " + HowToGrant,
            consulted, entries.Count);
    }

    /// <summary>
    /// The refusal's own remedy, naming the exact path and format.
    ///
    /// <para><b>An agent must never write the machine-local file.</b> Doing so would be granting its own
    /// permission to download to a live-job copy — the working agreement's "supply the mechanism, let the
    /// owner run it". So this is a sentence, and the file is the owner's to create.</para>
    /// </summary>
    public static string HowToGrant =>
        "TO GRANT ONE: a path that may be committed goes in " + RepoRelativeAllowlist
        + " (one per line; absolute, or 'repo:<path-from-repo-root>'). "
        + "A path that may NOT be committed — the rig's project is a copy of a live engineering job, and CLAUDE.md's live-run boundary is RETENTION, not access — goes in "
        + MachineAllowlistPath + " instead, one ABSOLUTE path per line. "
        + "*** THAT FILE IS THE OWNER'S TO WRITE AND MUST NEVER BE CREATED BY AN AGENT: writing it is granting a download target, not configuring a tool. ***";

    /// <summary>Absolute paths, one per entry. Blank lines and <c>#</c> comments are skipped.</summary>
    private static IEnumerable<string> ReadEntries(string listPath, string? repoRoot)
    {
        string[] lines;
        try
        {
            if (!File.Exists(listPath))
                yield break;

            lines = File.ReadAllLines(listPath);
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            string candidate;
            if (line.StartsWith(RepoPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (repoRoot is null)
                    continue;

                candidate = Path.Combine(repoRoot, line[RepoPrefix.Length..].Trim().Replace('\\', Path.DirectorySeparatorChar));
            }
            else if (Path.IsPathRooted(line))
            {
                candidate = line;
            }
            else
            {
                // A bare relative path is REFUSED rather than guessed at (ADR-0011 requirement 4):
                // relative to what is exactly the question, and every wrong answer names a real file.
                continue;
            }

            string resolved;
            try
            {
                resolved = Path.GetFullPath(candidate);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            yield return resolved;
        }
    }

    private static string? FindRepositoryRoot(string? start)
    {
        var dir = string.IsNullOrWhiteSpace(start) ? null : new DirectoryInfo(start!);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }
}
