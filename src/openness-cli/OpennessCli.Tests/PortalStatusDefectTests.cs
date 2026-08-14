using System;
using System.Collections.Generic;
using System.Linq;
using OpennessCli.Cli;
using OpennessCli.Model;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// The four defects found on 2026-08-13, after the owner observed at the machine that the
/// experiment-1.7 throws kill the Portal process the download was attached to — and the rig lane
/// measured it BY PID, because a count alone cannot see it (2→1 reads as "an idle instance closed"
/// unless you hold the PIDs).
///
/// <b>None of them was found by this suite, and all four are reporting failures rather than guard
/// failures — which is exactly why nothing complained.</b> Our tooling silently relaunches Portal, so
/// a crash-and-relaunch looked like an ordinary run from every surface we had.
///
/// Each test below has a partner asserting the <b>"did not run"</b> case: seven such gaps have now
/// been found across the lanes, and every one is the same shape — a check whose non-firing nobody
/// tested.
/// </summary>
public class PortalStatusDefectTests
{
    private static PortalProcessInfo Process(
        int pid,
        string? project = null,
        bool marked = false,
        DateTime? started = null,
        DateTime? acquired = null,
        bool launchedByUs = false,
        bool opennessVisible = true) =>
        new(pid, project, acquired ?? new DateTime(2026, 8, 13, 15, 38, 9), true, marked,
            started, launchedByUs, opennessVisible);

    // ---- defect 2: it disagreed with the operating system -----------------------------------

    /// <summary>
    /// `portal-status` reported `PROCESSES: 1` while the OS showed two. It is the read-only
    /// diagnostic this project reaches for when Portal misbehaves, and a process Openness cannot see
    /// is <b>precisely the case you reach for it in</b> — one that has just died, is still starting,
    /// or has lost its Openness endpoint.
    /// </summary>
    [Fact]
    public void AProcessTheOsShowsAndOpennessDoesNot_IsCountedAndClassifiedAsSuch()
    {
        var report = PortalStatusClassifier.Classify(new[]
        {
            Process(11228, project: @"C:\p\Thing.ap20"),
            Process(16972, opennessVisible: false),
        });

        Assert.Equal(2, report.Total);
        Assert.Equal(1, report.OpennessInvisibleCount);
        Assert.Equal(
            PortalProcessClass.OpennessInvisible,
            report.Processes.Single(p => p.Process.Pid == 16972).Class);
    }

    /// <summary>
    /// An OS-only process must NOT be read as "empty". Its null project is an absence of information,
    /// not a fact about the process — classifying it as a stray would invent the very thing the API
    /// declined to tell us.
    /// </summary>
    [Fact]
    public void AnOpennessInvisibleProcess_IsNotMistakenForAnEmptyOne()
    {
        var report = PortalStatusClassifier.Classify(new[] { Process(16972, opennessVisible: false) });

        Assert.Equal(0, report.StrayEmptyCount);
        Assert.Contains("OPERATING SYSTEM shows", report.Note, StringComparison.Ordinal);
    }

    // THE "DID NOT RUN" CASE. When every process IS visible, none of the above may fire — otherwise
    // the new class would quietly reclassify ordinary runs and the warning would become noise.
    [Fact]
    public void WhenEveryProcessIsVisible_NothingIsReportedAsOsOnly()
    {
        var report = PortalStatusClassifier.Classify(new[]
        {
            Process(11228, project: @"C:\p\Thing.ap20"),
            Process(16972),
        });

        Assert.Equal(0, report.OpennessInvisibleCount);
        Assert.DoesNotContain("OPERATING SYSTEM shows", report.Note, StringComparison.Ordinal);
    }

    // ---- defect 3: a process cannot be acquired before it exists ----------------------------

    /// <summary>
    /// Measured: PID 16972 reported `ACQUIRED 14:47:51` and started at `15:38:09` — fifty minutes
    /// before its own existence. The value is not this process's age and had been presented as one.
    /// A plausible wrong timestamp is worse than a missing one, so the impossibility is COMPUTED.
    /// </summary>
    [Fact]
    public void AnAcquiredTimeEarlierThanTheProcessStart_IsFlaggedAsImpossible()
    {
        var p = Process(
            16972,
            started: new DateTime(2026, 8, 13, 15, 38, 9),
            acquired: new DateTime(2026, 8, 13, 14, 47, 51));

        Assert.True(p.AcquiredPrecedesStart);

        var report = PortalStatusClassifier.Classify(new[] { p });
        Assert.Contains("EARLIER THAN THEIR OWN START TIME", report.Note, StringComparison.Ordinal);
        Assert.Contains("! BEFORE START", OutputFormatter.FormatPortalStatusTable(report), StringComparison.Ordinal);
    }

    // THE "DID NOT RUN" CASE, twice over: an ordinary timestamp must not be flagged, and neither
    // must one we cannot check. A flag that fires on unknown data is a flag nobody reads.
    [Fact]
    public void AnOrdinaryAcquiredTime_IsNotFlagged()
    {
        var p = Process(
            16972,
            started: new DateTime(2026, 8, 13, 15, 38, 9),
            acquired: new DateTime(2026, 8, 13, 15, 38, 12));

        Assert.False(p.AcquiredPrecedesStart);
        Assert.DoesNotContain("! BEFORE START", OutputFormatter.FormatPortalStatusTable(
            PortalStatusClassifier.Classify(new[] { p })), StringComparison.Ordinal);
    }

    /// <summary>
    /// *** THE FALSE ALARM, AND THE CASE NO FIXTURE EVER BUILT (found 2026-08-14). ***
    ///
    /// An Openness-INVISIBLE process reports no acquired time at all - the gateway constructs it with
    /// `Acquired: default`, deliberately, "rather than filled with a plausible-looking value". The
    /// impossibility check then read that default as a MEASUREMENT, so it was true for EVERY OS-only
    /// process with a readable start time, and the report announced an impossible ACQUIRED time about
    /// a process whose ACQUIRED column reads "(not visible to Openness)".
    ///
    /// <para>It fired on both real runs of this command on 2026-08-14. It was never caught because
    /// the Process() helper here DEFAULTS `acquired` to a real timestamp, so no fixture had ever
    /// reproduced the shape the gateway actually emits - the "did not run" case above covers a
    /// missing START time and there was no equivalent for a missing ACQUIRED time.</para>
    ///
    /// <para><i>A false alarm in a diagnostic is the same class of error as a false green:</i> it
    /// teaches its reader to discount the one time it is right.</para>
    /// </summary>
    [Fact]
    public void AnOpennessInvisibleProcess_IsNotFlagged_BecauseItReportsNoAcquiredTimeAtAll()
    {
        // Exactly what OpennessGateway builds for an OS-only process: no project, no acquired time,
        // a readable OS start time.
        var p = new PortalProcessInfo(
            12028, ProjectPath: null, Acquired: default, HasUserInterface: false, MarkedByThisTool: false,
            StartedAt: new DateTime(2026, 8, 13, 19, 20, 28), LaunchedByThisTool: false, OpennessVisible: false);

        Assert.False(p.AcquiredPrecedesStart);

        var report = PortalStatusClassifier.Classify(new[] { p });
        Assert.DoesNotContain("EARLIER THAN THEIR OWN START TIME", report.Note, StringComparison.Ordinal);
        Assert.DoesNotContain("! BEFORE START", OutputFormatter.FormatPortalStatusTable(report), StringComparison.Ordinal);
    }

    /// <summary>
    /// The real-world pair that produced the false alarm, together: a healthy in-use session whose
    /// ACQUIRED is LATER than its start (correct ordering, must not flag) beside an OS-only process
    /// with no acquired time (nothing to say, must not flag). This is the exact shape of both
    /// `portal-status` runs on 2026-08-14, and the report must now be silent on the subject.
    /// </summary>
    [Fact]
    public void TheMeasuredPair_ProducesNoImpossibleTimestampClaim()
    {
        var inUse = Process(
            5252,
            project: @"C:\p\Thing.ap20",
            started: new DateTime(2026, 8, 13, 19, 19, 27),
            acquired: new DateTime(2026, 8, 14, 2, 49, 19));

        var osOnly = new PortalProcessInfo(
            12028, null, default, false, false,
            new DateTime(2026, 8, 13, 19, 20, 28), false, OpennessVisible: false);

        var report = PortalStatusClassifier.Classify(new[] { inUse, osOnly });

        Assert.DoesNotContain("EARLIER THAN THEIR OWN START TIME", report.Note, StringComparison.Ordinal);

        // *** THE CONTROL. *** The OS-only finding itself must SURVIVE - the fix silences one claim,
        // not the whole note. A flag removed by deleting its subject is not a fix.
        Assert.Contains("OPERATING SYSTEM shows", report.Note, StringComparison.Ordinal);
        Assert.Equal(1, report.OpennessInvisibleCount);
    }

    [Fact]
    public void WhenTheStartTimeCouldNotBeRead_NothingIsClaimedAboutTheAcquiredTime()
    {
        var p = Process(16972, started: null, acquired: new DateTime(2026, 8, 13, 14, 47, 51));

        Assert.False(p.AcquiredPrecedesStart);

        var table = OutputFormatter.FormatPortalStatusTable(PortalStatusClassifier.Classify(new[] { p }));
        Assert.Contains("(unreadable)", table, StringComparison.Ordinal);
        Assert.DoesNotContain("! BEFORE START", table, StringComparison.Ordinal);
    }

    // ---- defect 1: the relaunch nothing recorded --------------------------------------------

    /// <summary>
    /// The reuse registry is erased the moment a project opens (Mark on launch, Unmark on open), so
    /// <c>MarkedByThisTool</c> is <b>structurally incapable</b> of being true for a process in use —
    /// which is exactly when a reader wants to know "did we launch this one?". The report-only launch
    /// history answers it without touching the guard.
    /// </summary>
    [Fact]
    public void AnInUseProcessWeLaunched_IsReportedAsLaunchedByUs()
    {
        var report = PortalStatusClassifier.Classify(new[]
        {
            Process(16972, project: @"C:\p\Thing.ap20", marked: false, launchedByUs: true),
        });

        // Still in-use — the CLASS is unchanged, and deliberately so: this is reporting, not a
        // reclassification that could feed a decision.
        Assert.Equal(PortalProcessClass.InUse, report.Processes.Single().Class);
        Assert.Contains("by us", OutputFormatter.FormatPortalStatusTable(report), StringComparison.Ordinal);
    }

    // THE "DID NOT RUN" CASE: a process we did not launch must say so, or the column means nothing.
    [Fact]
    public void AProcessWeDidNotLaunch_SaysSo()
    {
        var report = PortalStatusClassifier.Classify(new[]
        {
            Process(11228, project: @"C:\p\Thing.ap20", launchedByUs: false),
        });

        Assert.Contains("not by us", OutputFormatter.FormatPortalStatusTable(report), StringComparison.Ordinal);
    }

    /// <summary>
    /// *** THE DIRECTION THE REGISTRY DEFECT FAILS IN — the question that mattered most. ***
    ///
    /// The reuse guard is <c>!hasProject &amp;&amp; IsMarkedAsLaunchedByThisTool(pid)</c>. An EMPTY
    /// registry makes the second conjunct false, so a discovered empty process is never reused and a
    /// fresh instance is launched instead: <b>safe, and wasteful</b>. The reporting was broken; the
    /// guard was not. This test pins the direction so a future "fix" to the registry cannot quietly
    /// turn a fail-safe into a fail-open.
    /// </summary>
    [Fact]
    public void AnEmptyRegistry_MakesTheReuseGuardRefuse_NotPermit()
    {
        // A PID that cannot plausibly be marked: nothing has ever written it.
        Assert.False(LaunchedInstanceRegistry.IsMarkedAsLaunchedByThisTool(int.MaxValue - 7));
    }

    /// <summary>
    /// *** THE ASSERTION THAT WOULD HAVE CAUGHT THE SILENT RELAUNCH. ***
    ///
    /// The exact life cycle that erased the evidence: launch (marked), then open a project into it
    /// (unmarked). Afterwards the reuse registry says nothing — correctly, it only ever tracks empty
    /// orphans — and until now nothing else said anything either, so a Portal that had crashed and
    /// been silently relaunched was indistinguishable from one that had been there all along.
    ///
    /// Uses this test process's own PID because <c>Load()</c> prunes dead ones; the entry self-cleans
    /// once the runner exits, and it can never affect the reuse guard, which only ever asks about
    /// PIDs returned by <c>TiaPortal.GetProcesses()</c>.
    /// </summary>
    [Fact]
    public void AfterAProjectOpens_TheReuseRegistryForgets_AndTheLaunchHistoryDoesNot()
    {
        var pid = System.Diagnostics.Process.GetCurrentProcess().Id;

        LaunchedInstanceRegistry.MarkLaunched(pid);
        Assert.True(LaunchedInstanceRegistry.IsMarkedAsLaunchedByThisTool(pid));

        // What OpenProject does the moment a project is successfully opened into the instance.
        LaunchedInstanceRegistry.Unmark(pid);

        Assert.False(LaunchedInstanceRegistry.IsMarkedAsLaunchedByThisTool(pid));
        Assert.True(LaunchedInstanceRegistry.WasLaunchedByThisTool(pid));
    }

    /// <summary>
    /// THE "DID NOT RUN" CASE: a PID nothing ever launched must be false in BOTH records. A history
    /// that answered "by us" for everything would be worse than the blank it replaces.
    /// </summary>
    [Fact]
    public void APidWeNeverLaunched_IsInNeitherRecord()
    {
        const int never = int.MaxValue - 7;

        Assert.False(LaunchedInstanceRegistry.IsMarkedAsLaunchedByThisTool(never));
        Assert.False(LaunchedInstanceRegistry.WasLaunchedByThisTool(never));
    }

    /// <summary>
    /// And the separation itself: the report-only history must not be readable by the guard. If these
    /// two were ever merged, satisfying the reporting need would edit a safety decision.
    /// </summary>
    [Fact]
    public void TheReportOnlyHistory_IsNotTheReuseGuard()
    {
        var guard = typeof(LaunchedInstanceRegistry).GetMethod(nameof(LaunchedInstanceRegistry.IsMarkedAsLaunchedByThisTool));
        var report = typeof(LaunchedInstanceRegistry).GetMethod(nameof(LaunchedInstanceRegistry.WasLaunchedByThisTool));

        Assert.NotNull(guard);
        Assert.NotNull(report);
        Assert.NotSame(guard, report);
    }
}
