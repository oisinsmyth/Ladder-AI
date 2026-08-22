using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace Ladder.Converter.Tests;

/// <summary>
/// 🔴 <b>The only test in this suite that runs real PROCESSES, and the reason the lease is allowed to
/// call itself a lock.</b>
///
/// <para>Everything else about the lease is decided in-process: <c>LeaseStoreTests</c> is
/// single-threaded, and the one racing test the converter had before this
/// (<c>ClaimConcurrencyTests</c>) uses <c>Parallel.For</c> over stores inside ONE process. That is a
/// different experiment. A <c>Parallel.For</c> shares an address space, a garbage collector and a file
/// handle table; two OS processes contending for one directory entry share none of them, and the
/// primitive under test — <c>File.Move(overwrite: false)</c> — is an OS-level guarantee that in-process
/// concurrency never actually exercises.</para>
///
/// <para>The component this replaces is recorded as <i>"BUILT, NEVER RUN WITH TWO AGENTS
/// CONTENDING"</i>. This is that run.</para>
///
/// <para><b>Two caveats stated rather than hidden.</b> First, this exercises the BUILT BINARY, so it
/// reports on whatever was last compiled — Release by preference, since that is what the skills invoke.
/// Second, <c>System.Diagnostics.Process</c> appears here, in the TEST assembly; the FI-24 narrowing
/// pinned by <c>ConverterProcessInvariantTests</c> is about the converter assembly itself, which starts
/// no child processes.</para>
/// </summary>
public sealed class LeaseProcessRaceTests : IDisposable
{
    private const int Racers = 4;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "lease-race-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    /// <summary>
    /// The built CLI. <b>Release first — that is what the skills invoke</b> — and an absent binary is a
    /// FAILURE, not a reason to skip: a race test that quietly does not run is the "empty is not clean"
    /// failure in its purest form.
    /// </summary>
    private static string ConverterExe()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            foreach (var configuration in new[] { "Release", "Debug" })
            {
                var candidate = Path.Combine(
                    directory.FullName, "src", "converter", "Converter", "bin", configuration, "net8.0", "converter.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "converter.exe was not found by walking up from " + AppContext.BaseDirectory + ". "
            + "*** THIS IS A FAILURE, NOT A REASON TO SKIP *** — build it with: "
            + "dotnet build -c Release src/converter/converter.sln");
    }

    private sealed record Run(int ExitCode, string Stdout, string Stderr, DateTime StartedUtc, DateTime ExitedUtc);

    /// <summary>
    /// Launch every racer, THEN wait for any of them. Starting each in turn costs a millisecond or two
    /// while a converter start-up costs tens, so by the time the first reaches its acquire the rest are
    /// already running — and <see cref="AssertTheyActuallyOverlapped"/> checks that rather than trusting it.
    /// </summary>
    private List<Run> Race(Func<int, string[]> argumentsFor, int racers = Racers)
    {
        var exe = ConverterExe();
        var processes = new List<(Process Process, DateTime Started)>();

        for (var i = 0; i < racers; i++)
        {
            var info = new ProcessStartInfo(exe)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var argument in argumentsFor(i))
            {
                info.ArgumentList.Add(argument);
            }

            var started = DateTime.UtcNow;
            processes.Add((Process.Start(info)!, started));
        }

        var runs = new List<Run>();
        foreach (var (process, started) in processes)
        {
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            runs.Add(new Run(process.ExitCode, stdout, stderr, started, DateTime.UtcNow));
            process.Dispose();
        }

        return runs;
    }

    /// <summary>
    /// <b>A race that did not race proves nothing.</b> If the racers ran one after another, "exactly one
    /// won" would be true of a lock that does not work at all — so the overlap is asserted, and its
    /// absence FAILS rather than passing quietly.
    /// </summary>
    private static void AssertTheyActuallyOverlapped(List<Run> runs)
    {
        var lastStart = runs.Max(r => r.StartedUtc);
        var firstExit = runs.Min(r => r.ExitedUtc);

        Assert.True(lastStart < firstExit,
            $"the racers did not overlap: the last one started at {lastStart:O} but the first had already exited at "
            + $"{firstExit:O}. They ran in sequence, so nothing was raced and 'exactly one won' would be true even of "
            + "a lock that does nothing.");
    }

    private string[] AcquireArgs(string target, string holder) => new[]
    {
        "lease", "acquire",
        "--resource", target,
        "--leases", _root,
        "--holder", holder,
        "--pid", Environment.ProcessId.ToString(),
        "--ttl", "10",
        "--purpose", "process race",
    };

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>Processes race one resource and exactly one wins — over several rounds, on purpose.</b>
    ///
    /// <para><b>Why rounds rather than a single race.</b> The first version of this test ran four
    /// processes once, and it PASSED against a deliberately broken store whose acquire was a
    /// check-then-write instead of an atomic move. That is not a near miss: the window between "does the
    /// file exist" and "write it" is tens of microseconds, while process start-up jitter spreads the
    /// racers over milliseconds, so a single round almost never lands two callers inside it. A negative
    /// control that cannot redden is not a control.</para>
    ///
    /// <para>More racers and repeated rounds narrow that gap by raising the number of contended pairs.
    /// <b>It narrows it, and does not close it</b> — this test is fully deterministic for a CORRECT
    /// store (exactly one winner, always) and probabilistic only in how reliably it would catch a
    /// non-atomic one. Measured against the broken build, it reddens; that measurement is what licenses
    /// the claim, not the reasoning above it.</para>
    /// </summary>
    [Fact]
    public void PROCESSES_race_one_resource_and_exactly_ONE_wins()
    {
        const int rounds = 12;
        const int racersPerRound = 8;

        for (var round = 0; round < rounds; round++)
        {
            // Holder names carry the round. Without that they repeat, lease files accumulate across
            // rounds in one store, and the "exactly one file names the winner" check below matches an
            // EARLIER round's file — a failure that looks exactly like a broken lock. Cost an
            // investigation once already.
            var target = $"rig:10.10.10.{round}";
            var runs = Race(i => AcquireArgs(target, $"agent-{round}-{i}"), racersPerRound);

            AssertTheyActuallyOverlapped(runs);

            var winners = runs.Where(r => r.ExitCode == 0).ToList();
            var losers = runs.Where(r => r.ExitCode == 1).ToList();

            Assert.True(winners.Count == 1,
                $"round {round}: exactly one process must win, but {winners.Count} did. Exit codes were: "
                + string.Join(", ", runs.Select(r => r.ExitCode))
                + "\nstdout of each winner:\n" + string.Join("\n---\n", winners.Select(r => r.Stdout)));

            Assert.Equal(racersPerRound - 1, losers.Count);

            // Every refusal must name the SAME holder, and it must be the one that actually won. Two
            // losers naming two different winners would mean the store had briefly held two leases.
            var winningHolder = Enumerable.Range(0, racersPerRound)
                .Select(i => $"agent-{round}-{i}")
                .Single(holder => winners[0].Stdout.Contains($"holder  {holder}"));

            foreach (var loser in losers)
            {
                Assert.Contains(winningHolder, loser.Stderr);
            }

            // One slot per target, so the round's own file is the only thing that may have appeared.
            Assert.Single(Directory.GetFiles(_root, "*.lease"), f => File.ReadAllText(f).Contains(winningHolder));
        }
    }

    /// <summary>
    /// <b>The negative control.</b> A fence that refuses everything passes every test that only checks
    /// for a refusal — so four processes taking four DIFFERENT resources must all succeed. Without this,
    /// a lease store with a `return HeldByAnother` at the top would look perfect above.
    /// </summary>
    [Fact]
    public void Four_PROCESSES_taking_four_DIFFERENT_resources_all_win()
    {
        var runs = Race(i => AcquireArgs($"rig:10.10.10.{i}", $"agent-{i}"));

        AssertTheyActuallyOverlapped(runs);

        Assert.All(runs, r => Assert.Equal(0, r.ExitCode));
    }

    /// <summary>
    /// The lock survives the losers: after the race, the store holds exactly one lease and it belongs to
    /// the process that reported winning. A store left with two files, or none, would still have
    /// produced one exit-0 above.
    /// </summary>
    [Fact]
    public void After_the_race_the_store_holds_exactly_one_lease()
    {
        var runs = Race(i => AcquireArgs("rig:10.10.10.11", $"agent-{i}"));
        AssertTheyActuallyOverlapped(runs);

        var leaseFiles = Directory.GetFiles(_root, "*.lease");
        Assert.Single(leaseFiles);

        var winner = runs.Single(r => r.ExitCode == 0);
        var recorded = File.ReadAllText(leaseFiles[0]);
        var winningHolder = Enumerable.Range(0, Racers)
            .Select(i => $"agent-{i}")
            .Single(holder => winner.Stdout.Contains($"holder  {holder}"));

        Assert.Contains(winningHolder, recorded);
    }
}
