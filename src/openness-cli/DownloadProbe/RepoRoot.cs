using System;
using System.IO;

namespace DownloadProbe;

/// <summary>
/// Locate the repository root by walking up from a directory looking for a <c>.git</c> entry.
///
/// <para>Extracted from <c>ScratchProjectGuard</c> when the project fence was removed (ADR-0013,
/// 2026-08-28). It is kept because it is a general utility that merely happened to live in the
/// fence — it resolves nothing about downloads and gates nothing — and deleting it with the fence
/// would have taken working code that several fixtures use to find their own corpus.</para>
/// </summary>
internal static class RepoRoot
{
    /// <summary>
    /// Walks up from <paramref name="start"/> looking for a <c>.git</c> entry, returning null if
    /// none is found.
    ///
    /// <para>A git WORKTREE's <c>.git</c> is a FILE rather than a directory, and both are checked —
    /// a worktree build therefore resolves against its own checkout. That property was load-bearing
    /// for the fence and is retained deliberately: a naive <c>Directory.Exists</c> silently fails in
    /// every worktree, which is where a good deal of this project's work happens.</para>
    /// </summary>
    internal static string? Find(string? start)
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
}
