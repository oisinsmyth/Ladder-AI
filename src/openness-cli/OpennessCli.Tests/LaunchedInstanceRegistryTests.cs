using System.Diagnostics;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

// LaunchedInstanceRegistry is pure file/JSON bookkeeping keyed by OS process ID — no COM
// dependency, unlike the rest of OpennessGateway, so genuinely unit-testable. Added 2026-07-14
// alongside the fix it backs (docs/notes/concurrent-portal-test-plan.md T4.1's orphan-accumulation
// finding, closed the same day rather than left as a follow-up).
//
// Uses the current test process's own PID as a real, reliably-alive PID (Load() prunes anything
// whose process has exited, so a fake/made-up PID would never round-trip as "marked" in the first
// place — the tests need a genuinely live one). Each test unmarks its own PID in a finally block
// so tests don't leak state into the real shared registry file between runs.
public class LaunchedInstanceRegistryTests
{
    private static int CurrentPid => Process.GetCurrentProcess().Id;

    [Fact]
    public void MarkLaunched_ThenChecked_ReportsTrue()
    {
        var pid = CurrentPid;
        LaunchedInstanceRegistry.MarkLaunched(pid);
        try
        {
            Assert.True(LaunchedInstanceRegistry.IsMarkedAsLaunchedByThisTool(pid));
        }
        finally
        {
            LaunchedInstanceRegistry.Unmark(pid);
        }
    }

    [Fact]
    public void Unmark_RemovesIt_NoLongerReportsTrue()
    {
        var pid = CurrentPid;
        LaunchedInstanceRegistry.MarkLaunched(pid);
        LaunchedInstanceRegistry.Unmark(pid);

        Assert.False(LaunchedInstanceRegistry.IsMarkedAsLaunchedByThisTool(pid));
    }

    [Fact]
    public void IsMarkedAsLaunchedByThisTool_NeverMarked_ReportsFalse()
    {
        // A real, currently-running PID that was simply never marked — must never be mistaken for
        // one this tool launched. This is the exact property that keeps a human's own Portal
        // window safe: absence from the registry always means "leave it alone."
        var pid = CurrentPid;
        LaunchedInstanceRegistry.Unmark(pid); // ensure a clean slate regardless of test order

        Assert.False(LaunchedInstanceRegistry.IsMarkedAsLaunchedByThisTool(pid));
    }

    [Fact]
    public void IsMarkedAsLaunchedByThisTool_MarkedPidWhoseProcessHasSinceExited_IsPrunedAndReportsFalse()
    {
        // A PID that was marked, then its process fully exited (e.g. a human closed it, or it
        // crashed) without ever being unmarked — must not be recognized as "still mine" forever;
        // Load() prunes anything no longer running.
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        var deadPid = process.Id;
        process.WaitForExit();

        LaunchedInstanceRegistry.MarkLaunched(deadPid);

        Assert.False(LaunchedInstanceRegistry.IsMarkedAsLaunchedByThisTool(deadPid));
    }
}
