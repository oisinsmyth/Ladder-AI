using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// One racer's run: its exit code, what it said, and — crucially — <b>the OPERATING SYSTEM's</b> start
/// and exit times, <see cref="Process.StartTime"/> and <see cref="Process.ExitTime"/>, never a
/// <c>DateTime.UtcNow</c> taken around the call.
///
/// <para>🔴 <b>That distinction is the difference between a guard and a decoration, and it was measured,
/// not reasoned. It is the reason this file exists.</b> Both process-race tests originally timed each
/// racer the same wrong way: <c>StartedUtc</c> just before <c>Process.Start</c>, and <c>ExitedUtc</c>
/// when the DRAIN LOOP reached that process. The drain runs only after every racer has been launched, so
/// the earliest "exit" it can record is later than the last launch — <c>lastStart &lt; firstExit</c> is
/// then true BY CONSTRUCTION. Mutation-tested by adding a <c>WaitForExit()</c> to the launch loop,
/// running the racers strictly one after another: <b>every test still passed.</b> The overlap assertion
/// could not fail, so it was not asserting anything — inside the tests this repo cites as its strongest
/// concurrency evidence, a check that ran, passed, and examined nothing.</para>
///
/// <para>With the OS timestamps the same mutation reddens them, and that measurement — not the paragraph
/// above it — is what licenses the claim that these processes actually contended.</para>
/// </summary>
internal sealed record ProcessRun(int ExitCode, string Stdout, string Stderr, DateTime StartedUtc, DateTime ExitedUtc);

/// <summary>The attempt that actually overlapped, and its index — callers key their values off it.</summary>
internal sealed record RacedAttempt(List<ProcessRun> Runs, int Attempt);

/// <summary>
/// The shared harness behind <c>LeaseProcessRaceTests</c> and <c>ClaimProcessRaceTests</c> — the only
/// tests in this suite that run real PROCESSES, and the reason the lease and the claim registry are
/// allowed to call themselves locks.
///
/// <para><b>Shared rather than copied, deliberately.</b> The two files raced identical harnesses for two
/// different verbs, and when the overlap guard turned out to be unfalsifiable it was unfalsifiable
/// TWICE — one file was fixed and the other went on running the broken form. A discriminator that exists
/// in two copies is a discriminator that will disagree with itself; what differs between the two tests
/// is the argument vector and how a winner is recognised, and that is all that stays in them.</para>
///
/// <para><b>What this does not change:</b> these tests exercise the BUILT BINARY, so they report on
/// whatever was last compiled — Release by preference, since that is what the skills invoke. And
/// <c>System.Diagnostics.Process</c> appears here, in the TEST assembly; the FI-24 narrowing pinned by
/// <c>ConverterProcessInvariantTests</c> is about the converter assembly itself, which starts no child
/// processes.</para>
/// </summary>
internal static class ProcessRace
{
    /// <summary>
    /// The built CLI. <b>Release first — that is what the skills invoke</b> — and an absent binary is a
    /// FAILURE, not a reason to skip: a race test that quietly does not run is the "empty is not clean"
    /// failure in its purest form.
    /// </summary>
    public static string ConverterExe()
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

    /// <summary>
    /// Launch every racer, THEN wait for any of them — and launch them CONCURRENTLY, so the field is not
    /// spread out by the cost of starting it. By the time the first racer reaches its acquire the rest
    /// are already running; <see cref="AssertTheyActuallyOverlapped"/> checks that rather than trusting
    /// it, and <see cref="UntilTheyOverlap"/> re-races when the machine was too busy for it to be true.
    /// </summary>
    public static List<ProcessRun> Race(Func<int, string[]> argumentsFor, int racers)
    {
        var exe = ConverterExe();
        var processes = new Process[racers];

        // Launched from a THREAD POOL rather than one after another. `Process.Start` costs a few
        // milliseconds unloaded and tens of them while the rest of the suite is running in parallel;
        // serialised, eight of those spread the field over half a second while a racer lives about two
        // hundred milliseconds, and the last one starts after the first has finished. Measured exactly
        // that way in a full-suite run, as a REAL failure of the fixed overlap assertion. Starting them
        // concurrently compresses the launch window, which is the fix — the assertion only reports it.
        Parallel.For(0, racers, i =>
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

            processes[i] = Process.Start(info)!;
        });

        var runs = new List<ProcessRun>();
        foreach (var process in processes)
        {
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // The OS's own start and exit times, read while the handle is still open. See ProcessRun:
            // timing this loop instead would make the overlap assertion unfalsifiable.
            runs.Add(new ProcessRun(
                process.ExitCode, stdout, stderr,
                process.StartTime.ToUniversalTime(),
                process.ExitTime.ToUniversalTime()));
            process.Dispose();
        }

        return runs;
    }

    public static bool Overlapped(List<ProcessRun> runs) =>
        runs.Max(r => r.StartedUtc) < runs.Min(r => r.ExitedUtc);

    /// <summary>
    /// <b>A race that did not race proves nothing.</b> If the racers ran one after another, "exactly one
    /// won" would be true of a lock that does not work at all — so the overlap is asserted, and its
    /// absence FAILS rather than passing quietly.
    /// </summary>
    public static void AssertTheyActuallyOverlapped(List<ProcessRun> runs)
    {
        var lastStart = runs.Max(r => r.StartedUtc);
        var firstExit = runs.Min(r => r.ExitedUtc);

        Assert.True(lastStart < firstExit,
            $"the racers did not overlap: the last one started at {lastStart:O} but the first had already exited at "
            + $"{firstExit:O}. They ran in sequence, so nothing was raced and 'exactly one won' would be true even of "
            + "a lock that does nothing.");
    }

    /// <summary>
    /// Race, and if the launches did not actually overlap, race again on FRESH resources — up to
    /// <paramref name="attempts"/> times, after which the test FAILS with the overlap diagnostic.
    ///
    /// <para><b>Why a retry is not a weakening.</b> The property under test is what the store does when
    /// two processes are inside it at once; an attempt where the OS did not put them there measured
    /// nothing, and neither passing nor failing on it says anything about the lock. The retry discards
    /// that attempt, it does not forgive it — <b>if no attempt in the budget ever overlaps, the test is
    /// red</b>, which is exactly what should happen on a machine where these processes cannot be made to
    /// contend, because there the whole test is measuring nothing.</para>
    ///
    /// <para><b>Fresh resources per attempt, always</b> — which is why the caller is handed the attempt
    /// index and builds the argument vector from it. Re-racing the SAME resource would find it already
    /// taken by the discarded attempt, every racer would lose, and the round would fail as a broken lock.
    /// Each discarded attempt also leaves its own file in the store, so a caller counting files counts
    /// ATTEMPTS, not rounds.</para>
    /// </summary>
    public static RacedAttempt UntilTheyOverlap(Func<int, int, string[]> argumentsFor, int racers, int attempts = 4)
    {
        List<ProcessRun>? last = null;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var runs = Race(i => argumentsFor(attempt, i), racers);
            if (Overlapped(runs))
            {
                return new RacedAttempt(runs, attempt);
            }

            last = runs;
        }

        AssertTheyActuallyOverlapped(last!);
        throw new InvalidOperationException(
            $"unreachable: {attempts} attempts failed to overlap and AssertTheyActuallyOverlapped did not fail.");
    }
}
