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
/// while an agent was mid-run, and every subsequent attach hung for its full timeout.
///
/// CORRECTED 2026-08-10, AND THE MECHANISM IS NOT FULLY SETTLED — read both observations, because
/// each is measured and they do not sit comfortably together:
///
///   2026-08-08, UNATTENDED. Rebuilt binary, attaches hung for their whole timeout (60 s, then
///   15 min). A `PrintWindow` capture of every Portal window showed NO dialog anywhere, and both
///   main windows were visible and enabled. Nobody was at the machine.
///
///   2026-08-10, OWNER PRESENT. Rebuilt Release binary; the owner approved it; the connect
///   completed in ~8 s and a whitelist entry appeared for the new hash. The previously-hanging
///   Debug binary then connected in ~7 s as well.
///
/// WHAT IS SAFE TO CONCLUDE, and all this class acts on: a build TIA has not seen before needs a
/// PERSON to approve it. With someone at the machine that costs seconds. UNATTENDED THERE IS NOBODY
/// TO ACCEPT, and the attach sits until the caller's timeout expires — which is the practical rule
/// that matters for an agent, and it is why rebuilding mid-run stalls one.
///
/// WHAT IS NOT SETTLED: why no dialog was visible on 08-08. It may never have been raised (the
/// concurrent HMI session on another project was hammering Portal at the time and is an independently
/// recorded cause of intermittent attach wedges), or it may be raised in a way that capture missed.
/// DO NOT write either explanation up as fact without new evidence — an earlier version of this
/// comment asserted "refused silently, no dialog, ever" and that overstated what was measured.
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
                    $"WARNING: this build has never connected to TIA Openness — it was REBUILT since its last approval.\r\n" +
                    $"  {executablePath}\r\n" +
                    $"  TIA records approved callers by (Path, FileHash). {result.EntriesForPath} earlier build(s) of this exact\r\n" +
                    $"  path are approved; none matches this file's hash.\r\n" +
                    $"  A NEW BUILD NEEDS A PERSON TO APPROVE IT AT THE MACHINE. Approved live 2026-08-10: the connect\r\n" +
                    $"  completed in ~8 s once the owner accepted it. UNATTENDED, THERE IS NOBODY TO ACCEPT, and the\r\n" +
                    $"  connect will sit until --timeout-connect expires — which is why rebuilding mid-run stalls an agent.\r\n" +
                    $"  If you are running unattended: use an already-approved build. If someone is at the machine: proceed.";

            case Verdict.PathNotListed:
                return
                    $"WARNING: no TIA Openness approval entry names this path — this build has never connected.\r\n" +
                    $"  {executablePath}\r\n" +
                    $"  Approval does not carry across build locations, so a worktree or copied build is a new application\r\n" +
                    $"  to Openness. IT NEEDS A PERSON TO APPROVE IT AT THE MACHINE; unattended, the connect will sit\r\n" +
                    $"  until --timeout-connect expires.";

            default:
                return null;
        }
    }
}
