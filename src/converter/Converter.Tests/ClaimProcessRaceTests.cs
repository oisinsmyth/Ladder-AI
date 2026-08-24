using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 <b>FI-65 standing requirement 8, the half of it that can actually be closed here.</b>
///
/// <para><c>docs/evidence/fi-65-claims-build.md</c> §4.8 asks, before claims are wired into the coding
/// skills, for "an end-to-end test that two concurrent AGENT RUNS against one project cannot both take
/// the same resource". This file runs two — eight, twelve times over — concurrent <b>PROCESSES</b> of
/// one CLI verb. Those are not the same claim, and the difference is stated here so nobody can read a
/// green suite as the requirement discharged:</para>
///
/// <list type="bullet">
/// <item><b>CLOSED: the primitive.</b> Real OS processes, separate address spaces, separate handle
/// tables, contending for one claim slot in one shared store through the real `converter claim`
/// command line — argument parsing, corpus build, store resolution, `File.Move(overwrite: false)` and
/// the exit-code contract included. Exactly one wins, every loser is told who beat it, and the store is
/// left holding exactly one file. That is the concurrency guarantee the registry rests on, and until
/// this file existed it was recorded as "BUILT, NEVER RUN WITH TWO AGENTS CONTENDING".</item>
/// <item><b>NOT CLOSED: the workflow.</b> Nothing here dispatches an agent. No skill calls the claim
/// tool yet (§5.6, "Adoption"), so nothing in this repo demonstrates that two <i>generation runs</i>
/// take claims at all — and a registry nobody consults is not made safe by being race-free. An agent
/// run also does far more than one verb: it holds a claim across a long stage, releases it, retries
/// after a crash, and can simply forget to ask. None of that is exercised below.</item>
/// </list>
///
/// <para><b>So requirement 8 stays open on the adoption half.</b> What may be struck from it is the
/// words "cannot both take the same resource" for a single contended acquire; what may not is
/// "two concurrent agent runs". A future reader marking the whole requirement satisfied on the strength
/// of this file would be recording a workflow test that was never written.</para>
///
/// <para>The launching, timing and overlap machinery lives in <see cref="ProcessRace"/>, shared with
/// <c>LeaseProcessRaceTests</c>; the caveats stated there (it exercises the BUILT BINARY, and the FI-24
/// process narrowing is about the converter assembly, not this one) apply here. What stays in this file
/// is the argument vector and how a winner is recognised.</para>
/// </summary>
[Collection(TestCollections.ProcessRace)]
public sealed class ClaimProcessRaceTests : IDisposable
{
    private const int Racers = 4;

    /// <summary>
    /// 🔴 <b>A REAL CORPUS, AND NOT AS SCENERY.</b> <c>claim</c> validates against the project before it
    /// touches the store, and <c>ClaimValidator.Reject</c> answers <c>NothingExamined</c> — exit 2 — for
    /// an empty one. Point this at a bare temp directory and every racer refuses for the same reason,
    /// "exactly one won" is false in the harmless direction, and the test proves nothing while looking
    /// busy. The exit-code assertions below are what keep that from ever being mistaken for a pass.
    /// </summary>
    private readonly string _projectDir = ClaimsTestCorpus.Create();

    private readonly string _claimsRoot = ClaimsTestCorpus.CreateClaimsRoot();

    public void Dispose() => ClaimsTestCorpus.Delete(_projectDir, _claimsRoot);

    /// <summary>
    /// <b>Every racer must have reached the store.</b> Exit 2 is <c>Invalid</c> or <c>NothingExamined</c>
    /// — an unusable argument, or a corpus that indexed nothing — and a field of 2s would make "exactly
    /// one won" fail for a reason that has nothing to do with the lock, or (worse, in a future variant
    /// that only counted winners) pass for one. The claim contract is 0 = acquired, 1 = held by another,
    /// 2 = nothing decided; only the first two are answers about the race.
    /// </summary>
    private static void AssertEveryRacerGotARealAnswer(List<ProcessRun> runs, int expected)
    {
        Assert.Equal(expected, runs.Count);
        Assert.All(runs, r => Assert.True(r.ExitCode is 0 or 1,
            $"a racer exited {r.ExitCode}, which is neither ACQUIRED (0) nor HELD-BY-ANOTHER (1) — it never reached "
            + $"the store, so it raced nothing.\nstdout:\n{r.Stdout}\nstderr:\n{r.Stderr}"));
    }

    private string[] ClaimArgs(string value, string agent) => new[]
    {
        "claim",
        "--project", _projectDir,
        "--claims", _claimsRoot,
        "--agent", agent,
        "--kind", "block-number",
        "--value", value,
        "--purpose", "process race",
    };

    /// <summary>
    /// The store the CLI actually resolved: <c>ClaimStore</c> appends the project slug to the root, so
    /// reading the root itself would find nothing and find it silently. Asserted to exist rather than
    /// enumerated blindly.
    /// </summary>
    private string StoreDir()
    {
        var dir = Path.Combine(_claimsRoot, Path.GetFileName(_projectDir.TrimEnd(Path.DirectorySeparatorChar)));
        Assert.True(Directory.Exists(dir),
            $"no claim store at '{dir}' — the racers wrote nothing anywhere this test knows how to look, "
            + "which is not the same as writing nothing.");
        return dir;
    }

    private string[] ClaimFiles() => Directory.GetFiles(StoreDir(), "*.claim", SearchOption.TopDirectoryOnly);

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>Processes race one block number and exactly one wins — over several rounds, on purpose.</b>
    ///
    /// <para><b>Why rounds rather than a single race.</b> The single-round form of the sibling lease test
    /// PASSED against a deliberately broken store whose acquire was a check-then-write instead of an
    /// atomic move. That is not a near miss: the window between "does the file exist" and "write it" is
    /// tens of microseconds, while process start-up jitter spreads the racers over milliseconds, so a
    /// single round almost never lands two callers inside it. A negative control that cannot redden is
    /// not a control.</para>
    ///
    /// <para>More racers and repeated rounds narrow that gap by raising the number of contended pairs.
    /// <b>It narrows it, and does not close it</b> — this test is fully deterministic for a CORRECT store
    /// (exactly one winner, always) and probabilistic only in how reliably it would catch a non-atomic
    /// one. Measured against a build whose acquire was replaced by a check-then-write, it reddened in
    /// both attempts, reporting two winners of one number in round 0; that measurement is what licenses
    /// the claim, not the reasoning above it. The rounds stay anyway: they cost about a second, and the
    /// margin they buy is the difference between catching that mutation reliably and catching it
    /// sometimes.</para>
    /// </summary>
    [Fact]
    public void PROCESSES_race_one_block_number_and_exactly_ONE_wins()
    {
        const int rounds = 12;
        const int racersPerRound = 8;
        const int attemptsPerRound = 4;

        // Every attempt — including one discarded for not overlapping — claims one fresh value and so
        // leaves exactly one file behind. Counting attempts rather than rounds is what keeps the file
        // count below an assertion about the store instead of an assertion about the retry loop.
        var attemptsRun = 0;

        for (var round = 0; round < rounds; round++)
        {
            // Both the VALUE and the agent names carry the round AND the attempt. Without that they
            // repeat: claim files accumulate across rounds in one store, and the "exactly one file names
            // the winner" check below matches an EARLIER round's file — a failure that looks exactly like
            // a broken lock. Cost an investigation once already, on the lease side.
            //
            // FB7100+ is free in ClaimsTestCorpus (which holds FC3, FB50, DB20, DB21) and is outside the
            // reserved harness band, so the acquire is decided by the store and by nothing else.
            string ValueFor(int attempt) => $"FB{7100 + (round * attemptsPerRound) + attempt}";

            var raced = ProcessRace.UntilTheyOverlap(
                (attempt, i) => ClaimArgs(ValueFor(attempt), $"agent-{round}-{attempt}-{i}"),
                racersPerRound,
                attemptsPerRound);

            attemptsRun += raced.Attempt + 1;

            var runs = raced.Runs;
            var value = ValueFor(raced.Attempt);

            AssertEveryRacerGotARealAnswer(runs, racersPerRound);

            var winners = runs.Where(r => r.ExitCode == 0).ToList();
            var losers = runs.Where(r => r.ExitCode == 1).ToList();

            Assert.True(winners.Count == 1,
                $"round {round}: exactly one process must win {value}, but {winners.Count} did. Exit codes were: "
                + string.Join(", ", runs.Select(r => r.ExitCode))
                + "\nstdout of each winner:\n" + string.Join("\n---\n", winners.Select(r => r.Stdout)));

            Assert.Equal(racersPerRound - 1, losers.Count);

            // Every refusal must name the SAME agent, and it must be the one that actually won. Two
            // losers naming two different holders would mean the store had briefly held two claims.
            var winningAgent = Enumerable.Range(0, racersPerRound)
                .Select(i => $"agent-{round}-{raced.Attempt}-{i}")
                .Single(agent => winners[0].Stdout.Contains($"agent   {agent}"));

            foreach (var loser in losers)
            {
                Assert.Contains($"holder  {winningAgent}", loser.Stderr);
            }

            // The denominator: one slot per value and one value per attempt, so the store must hold
            // exactly as many claim files as attempts run — not merely "at least one for this round",
            // which would also be true of a store that wrote a file per racer.
            var files = ClaimFiles();
            Assert.Equal(attemptsRun, files.Length);

            var thisRound = files.Select(File.ReadAllText).Where(t => t.Contains($"value {value}\n")).ToList();
            Assert.Single(thisRound);
            Assert.Contains($"agent {winningAgent}\n", thisRound[0]);
        }
    }

    /// <summary>
    /// <b>The negative control.</b> A fence that refuses everything passes every test that only checks
    /// for a refusal — so four processes taking four DIFFERENT block numbers must all succeed. Without
    /// this, a claim store with a `return HeldByAnother` at the top of TryAcquire would look perfect
    /// above.
    /// </summary>
    [Fact]
    public void Four_PROCESSES_taking_four_DIFFERENT_block_numbers_all_win()
    {
        var raced = ProcessRace.UntilTheyOverlap((attempt, i) => ClaimArgs($"FB{7200 + (attempt * Racers) + i}", $"agent-{attempt}-{i}"), Racers);

        AssertEveryRacerGotARealAnswer(raced.Runs, Racers);

        Assert.All(raced.Runs, r => Assert.Equal(0, r.ExitCode));

        // And all four grants were recorded, distinctly: four exit-0s over a store holding one file
        // would be four agents told they own the same slot. (Four per attempt, discarded attempts
        // included — they claimed four values of their own.)
        Assert.Equal(Racers * (raced.Attempt + 1), ClaimFiles().Length);
    }

    /// <summary>
    /// The lock survives the losers: after the race, the store holds exactly one claim and it belongs to
    /// the process that reported winning. A store left with two files, or none, would still have produced
    /// one exit-0 above.
    /// </summary>
    [Fact]
    public void After_the_race_the_store_holds_exactly_one_claim()
    {
        var raced = ProcessRace.UntilTheyOverlap((attempt, i) => ClaimArgs($"FB{7300 + attempt}", $"agent-{attempt}-{i}"), Racers);
        var value = $"FB{7300 + raced.Attempt}";

        AssertEveryRacerGotARealAnswer(raced.Runs, Racers);

        // One file per contested value, and one value per attempt — a discarded attempt leaves its own.
        var files = ClaimFiles();
        Assert.Equal(raced.Attempt + 1, files.Length);

        var forThisValue = files.Select(File.ReadAllText).Where(t => t.Contains($"value {value}\n")).ToList();
        Assert.Single(forThisValue);

        var winner = raced.Runs.Single(r => r.ExitCode == 0);
        var winningAgent = Enumerable.Range(0, Racers)
            .Select(i => $"agent-{raced.Attempt}-{i}")
            .Single(agent => winner.Stdout.Contains($"agent   {agent}"));

        Assert.Contains($"agent {winningAgent}\n", forThisValue[0]);
    }
}
