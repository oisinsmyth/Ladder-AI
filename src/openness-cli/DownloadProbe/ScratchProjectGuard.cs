using System;
using System.IO;

namespace DownloadProbe;

/// <summary>
/// The refusal that makes this tool incapable of downloading to the real project.
///
/// It is a suffix match on the FILE NAME, not an allowlist of full paths, for two reasons. A full
/// path would have to name a live engineering job inside this repository, which the data boundary
/// forbids (CLAUDE.md, "Live runs": use anything, commit nothing). And an allowlist keyed on a path
/// silently stops protecting anything the moment the folder is copied or renamed, whereas the
/// scratch copy's defining property — that its name ends in " scratch" — travels with it.
///
/// It is checked BEFORE Portal is contacted, and that ordering is the property under test: the
/// refusal must be reachable with no Portal session in existence, so that a wrong path cannot even
/// open the real project read-only, let alone hold its file lock.
/// </summary>
internal static class ScratchProjectGuard
{
    /// <summary>
    /// The only thing this tool will open. The leading space is deliberate: it makes
    /// "…\Anything scratch.ap20" pass and "…\myscratch.ap20" fail, so a project that merely happens
    /// to contain the word cannot be mistaken for the scratch copy.
    /// </summary>
    internal const string RequiredSuffix = " scratch.ap20";

    internal static bool IsScratchProject(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return false;
        }

        // Compare the file name, never the whole string: a directory called "… scratch.ap20" further
        // up the path must not vouch for a different file below it.
        string fileName;
        try
        {
            fileName = Path.GetFileName(projectPath!.Trim());
        }
        catch (ArgumentException)
        {
            // An unusable path is not the scratch project. Refuse rather than throw — this guard's
            // answer is always "no" when it cannot say "yes".
            return false;
        }

        return fileName.EndsWith(RequiredSuffix, StringComparison.OrdinalIgnoreCase);
    }

    internal static string DescribeRefusal(string? projectPath) =>
        $"REFUSED: '{projectPath}' is not the scratch project." + Environment.NewLine +
        $"download-probe opens only a project whose file name ends in '{RequiredSuffix}' " +
        "(case-insensitive). This tool performs a real device download; it is deliberately " +
        "incapable of doing so against the real project." + Environment.NewLine +
        "Portal was NOT contacted, no project was opened, and nothing was written.";
}
