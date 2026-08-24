using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Converter.Tests;
using Xunit;

namespace Ladder.Converter.Tests;

/// <summary>
/// 🔴 <b>One of the two tests in this suite that run real PROCESSES, and the reason the lease is allowed
/// to call itself a lock.</b>
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
/// <para>🔴 <b>2026-08-24 — AND FOR ITS FIRST LIFE IT PROVED LESS THAN IT SAID.</b> The overlap
/// assertion below, the one that makes the sentence above true, <b>could not fail</b>: it compared a
/// timestamp taken before <c>Process.Start</c> against one taken in the DRAIN LOOP, which runs after
/// every racer has been launched, so <c>lastStart &lt; firstExit</c> held by construction. Serialising
/// the launches left all three tests GREEN. The comment said "a race that did not race proves nothing"
/// and the code did not implement it. Fixed by moving the machinery into <see cref="ProcessRace"/>,
/// where the timestamps are the OPERATING SYSTEM's and the same mutation reddens all three — the
/// measurement is recorded there, on the type that carries them.</para>
///
/// <para>The launching, timing and overlap machinery is shared with <c>ClaimProcessRaceTests</c> rather
/// than copied: it was wrong in two places at once, one copy got fixed, and the other went on running
/// the broken form. What stays here is the argument vector and how a winner is recognised.</para>
/// </summary>
[Collection(TestCollections.ProcessRace)]
public sealed class LeaseProcessRaceTests : IDisposable
{
    private const int Racers = 4;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "lease-race-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
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

    private string[] LeaseFiles() => Directory.GetFiles(_root, "*.lease");

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
        const int attemptsPerRound = 4;

        // Every attempt — including one discarded for not overlapping — takes one fresh resource and so
        // leaves exactly one lease file behind. Counting attempts rather than rounds keeps the file
        // count an assertion about the store rather than about the retry loop.
        var attemptsRun = 0;

        for (var round = 0; round < rounds; round++)
        {
            // The resource and the holder names carry the round AND the attempt. Without that they
            // repeat, lease files accumulate across rounds in one store, and the "exactly one file names
            // the winner" check below matches an EARLIER round's file — a failure that looks exactly
            // like a broken lock. Cost an investigation once already.
            string TargetFor(int attempt) => $"rig:10.10.10.{(round * attemptsPerRound) + attempt}";

            var raced = ProcessRace.UntilTheyOverlap(
                (attempt, i) => AcquireArgs(TargetFor(attempt), $"agent-{round}-{attempt}-{i}"),
                racersPerRound,
                attemptsPerRound);

            attemptsRun += raced.Attempt + 1;

            var runs = raced.Runs;
            var target = TargetFor(raced.Attempt);

            // Every racer must have reached the store: the lease contract is 0 = acquired,
            // 1 = refused, 2 = unusable input, and a field of 2s would make "exactly one won" fail (or,
            // in a variant that only counted winners, pass) for a reason that is not about the lock.
            Assert.Equal(racersPerRound, runs.Count);
            Assert.All(runs, r => Assert.True(r.ExitCode is 0 or 1,
                $"a racer exited {r.ExitCode}, which is neither ACQUIRED (0) nor REFUSED (1) — it never reached the "
                + $"store, so it raced nothing.\nstdout:\n{r.Stdout}\nstderr:\n{r.Stderr}"));

            var winners = runs.Where(r => r.ExitCode == 0).ToList();
            var losers = runs.Where(r => r.ExitCode == 1).ToList();

            Assert.True(winners.Count == 1,
                $"round {round}: exactly one process must win {target}, but {winners.Count} did. Exit codes were: "
                + string.Join(", ", runs.Select(r => r.ExitCode))
                + "\nstdout of each winner:\n" + string.Join("\n---\n", winners.Select(r => r.Stdout)));

            Assert.Equal(racersPerRound - 1, losers.Count);

            // Every refusal must name the SAME holder, and it must be the one that actually won. Two
            // losers naming two different winners would mean the store had briefly held two leases.
            var winningHolder = Enumerable.Range(0, racersPerRound)
                .Select(i => $"agent-{round}-{raced.Attempt}-{i}")
                .Single(holder => winners[0].Stdout.Contains($"holder  {holder}"));

            foreach (var loser in losers)
            {
                Assert.Contains(winningHolder, loser.Stderr);
            }

            // One slot per resource and one resource per attempt, so the store holds exactly as many
            // lease files as attempts run — not merely "at least one naming the winner", which would
            // also be true of a store that wrote a file per racer.
            var files = LeaseFiles();
            Assert.Equal(attemptsRun, files.Length);
            Assert.Single(files, f => File.ReadAllText(f).Contains(winningHolder));
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
        var raced = ProcessRace.UntilTheyOverlap(
            (attempt, i) => AcquireArgs($"rig:10.10.10.{(attempt * Racers) + i}", $"agent-{attempt}-{i}"),
            Racers);

        Assert.All(raced.Runs, r => Assert.Equal(0, r.ExitCode));

        // And all four grants were recorded, distinctly: four exit-0s over a store holding one file
        // would be four holders told they own the same rig. (Four per attempt — a discarded attempt
        // took four resources of its own.)
        Assert.Equal(Racers * (raced.Attempt + 1), LeaseFiles().Length);
    }

    /// <summary>
    /// The lock survives the losers: after the race, the store holds exactly one lease for the contested
    /// resource and it belongs to the process that reported winning. A store left with two files, or
    /// none, would still have produced one exit-0 above.
    /// </summary>
    [Fact]
    public void After_the_race_the_store_holds_exactly_one_lease()
    {
        var raced = ProcessRace.UntilTheyOverlap(
            (attempt, i) => AcquireArgs($"rig:10.10.10.{200 + attempt}", $"agent-{attempt}-{i}"),
            Racers);

        // One file per contested resource, and one resource per attempt — a discarded attempt leaves
        // its own.
        var files = LeaseFiles();
        Assert.Equal(raced.Attempt + 1, files.Length);

        var winner = raced.Runs.Single(r => r.ExitCode == 0);
        var winningHolder = Enumerable.Range(0, Racers)
            .Select(i => $"agent-{raced.Attempt}-{i}")
            .Single(holder => winner.Stdout.Contains($"holder  {holder}"));

        Assert.Single(files, f => File.ReadAllText(f).Contains(winningHolder));
    }
}
