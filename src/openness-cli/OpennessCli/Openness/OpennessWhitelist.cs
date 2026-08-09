using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace OpennessCli.Openness;

/// <summary>
/// FI-61 (2026-08-08). TIA APPROVES OPENNESS CALLERS BY (PATH, FILEHASH) — SO A REBUILD REVOKES
/// APPROVAL, AND THE REFUSAL IS COMPLETELY SILENT.
///
/// The whitelist lives at:
///     HKLM\SOFTWARE\Siemens\Automation\Openness\&lt;version&gt;\Whitelist\&lt;exe name&gt;\Entry (N)
///       Path         = C:\...\bin\Debug\net48\openness-cli.exe
///       DateModified = 2026/07/10 08:40:17.964
///       FileHash     = PY8nw9ndT2J/qsmq1G289KwAEM10YkvRPjcY1j5MlTE=   (base64 SHA-256)
///
/// One entry is added per approved build, so the key accumulates: this machine had 84 entries for
/// one Debug path. Rebuild the executable and its hash no longer matches ANY of them — Openness
/// then refuses to hand out a Portal connection and simply never responds. `Attach()` blocks until
/// the caller's own timeout expires. There is NO dialog, NO exception and NO log entry.
///
/// That failure is indistinguishable, from the outside, from a wedged Portal — which is exactly how
/// it burned an hour on 2026-08-08: a `dotnet test` on the openness-cli solution rebuilt the binary
/// while an agent was mid-run, and every subsequent attach hung for its full timeout. Both the CLI's
/// own error text and `docs/notes/openness-quirks.md` blamed the first-connect approval dialog, and
/// a direct `PrintWindow` capture of every Portal window proved no dialog existed anywhere.
///
/// THIS CHECK IS ADVISORY AND MUST STAY THAT WAY. It warns and lets the connect proceed; it never
/// blocks. A false negative here (a whitelist layout this code does not understand, a registry view
/// it cannot read, a Siemens change) would otherwise refuse every Portal command on a machine where
/// everything actually works — far worse than the hang it prevents. Reporting a wrong answer loudly
/// is cheap; refusing correct work is not.
/// </summary>
public static class OpennessWhitelist
{
    private const string WhitelistRoot = @"SOFTWARE\Siemens\Automation\Openness";

    public enum Verdict
    {
        /// <summary>An entry matches both this path and this file's current hash.</summary>
        Approved,

        /// <summary>The path is whitelisted, but no entry carries this file's hash — i.e. REBUILT.</summary>
        StaleHash,

        /// <summary>No entry names this path at all — never approved from this location.</summary>
        PathNotListed,

        /// <summary>The whitelist could not be read. Says nothing either way; never treated as a failure.</summary>
        Unknown,
    }

    public readonly struct Entry
    {
        public Entry(string path, string fileHash)
        {
            Path = path;
            FileHash = fileHash;
        }

        public string Path { get; }

        public string FileHash { get; }
    }

    public sealed class Result
    {
        public Result(Verdict verdict, int entriesForPath)
        {
            Verdict = verdict;
            EntriesForPath = entriesForPath;
        }

        public Verdict Verdict { get; }

        /// <summary>How many entries name this path — the count of previously approved builds.</summary>
        public int EntriesForPath { get; }
    }

    /// <summary>
    /// The decision itself, with no registry or filesystem access, so it can be tested directly.
    /// Paths compare case-insensitively (Windows); hashes compare exactly (base64 is case-sensitive).
    /// </summary>
    public static Result Evaluate(IEnumerable<Entry> entries, string executablePath, string executableHash)
    {
        var forPath = entries
            .Where(e => !string.IsNullOrEmpty(e.Path)
                        && string.Equals(e.Path, executablePath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (forPath.Count == 0)
        {
            return new Result(Verdict.PathNotListed, 0);
        }

        var matched = forPath.Any(e => string.Equals(e.FileHash, executableHash, StringComparison.Ordinal));

        return new Result(matched ? Verdict.Approved : Verdict.StaleHash, forPath.Count);
    }

    /// <summary>
    /// Checks the running executable. Any failure returns <see cref="Verdict.Unknown"/> — this is a
    /// diagnostic aid, and it must never become a new way for the tool to refuse to run.
    /// </summary>
    public static Result CheckRunningExecutable()
    {
        try
        {
            var exePath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
            if (exePath is null || exePath.Length == 0 || !File.Exists(exePath))
            {
                return new Result(Verdict.Unknown, 0);
            }

            var hash = ComputeBase64Sha256(exePath);
            var entries = ReadEntries(Path.GetFileName(exePath));

            return entries is null ? new Result(Verdict.Unknown, 0) : Evaluate(entries, exePath, hash);
        }
        catch
        {
            return new Result(Verdict.Unknown, 0);
        }
    }

    public static string ComputeBase64Sha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        return Convert.ToBase64String(sha.ComputeHash(stream));
    }

    /// <summary>
    /// Every Openness version key is scanned, not just the one matching the resolved TIA install:
    /// several majors coexist on an engineering PC (17.0 and 20.0 here), and an entry under any of
    /// them is still an approval. Returns null when the root cannot be read at all.
    /// </summary>
    private static List<Entry>? ReadEntries(string exeName)
    {
        // Registry64 explicitly: a 32-bit process would otherwise be redirected to WOW6432Node and
        // find nothing, which would report every approved binary as unapproved.
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var root = baseKey.OpenSubKey(WhitelistRoot);
        if (root is null)
        {
            return null;
        }

        var entries = new List<Entry>();

        foreach (var versionName in root.GetSubKeyNames())
        {
            using var appKey = root.OpenSubKey($@"{versionName}\Whitelist\{exeName}");
            if (appKey is null)
            {
                continue;
            }

            foreach (var entryName in appKey.GetSubKeyNames())
            {
                using var entryKey = appKey.OpenSubKey(entryName);
                if (entryKey is null)
                {
                    continue;
                }

                var path = entryKey.GetValue("Path") as string;
                var fileHash = entryKey.GetValue("FileHash") as string;
                if (path is not null && fileHash is not null)
                {
                    entries.Add(new Entry(path, fileHash));
                }
            }
        }

        return entries;
    }

    /// <summary>
    /// The warning text. Separate from the check so it can be asserted in tests without a registry.
    /// </summary>
    public static string? DescribeIfNotApproved(Result result, string executablePath)
    {
        switch (result.Verdict)
        {
            case Verdict.StaleHash:
                return
                    $"WARNING: this executable is NOT approved for TIA Openness — it has been REBUILT since it was last approved.\r\n" +
                    $"  {executablePath}\r\n" +
                    $"  TIA whitelists callers by (Path, FileHash). {result.EntriesForPath} earlier build(s) of this exact path are\r\n" +
                    $"  approved, but none of them matches this file's current hash.\r\n" +
                    $"  EXPECT THE CONNECT BELOW TO HANG UNTIL --timeout-connect EXPIRES, WITH NO DIALOG AND NO ERROR.\r\n" +
                    $"  Openness refuses an unapproved caller silently; it does not prompt. Use a build that is already\r\n" +
                    $"  approved, or have the machine owner approve this one.";

            case Verdict.PathNotListed:
                return
                    $"WARNING: this executable is NOT approved for TIA Openness — no whitelist entry names this path.\r\n" +
                    $"  {executablePath}\r\n" +
                    $"  Approval does not carry across build locations, so a worktree or freshly-copied build is a\r\n" +
                    $"  different application as far as Openness is concerned.\r\n" +
                    $"  EXPECT THE CONNECT BELOW TO HANG UNTIL --timeout-connect EXPIRES, WITH NO DIALOG AND NO ERROR.";

            default:
                return null;
        }
    }
}
