using System;
using System.Collections.Generic;
using System.Linq;
using OpennessCli.Model;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// 🔴 <b>Deciding what to close, without touching anything.</b>
///
/// <para>Same split as <c>portal-status</c>: the decision is pure and fully tested here with hand-built
/// lists; the Siemens-touching half (attach, save, close, terminate) is integration-only. That split is
/// what lets a DESTRUCTIVE command have every branch of its judgement exercised offline.</para>
///
/// <para>Owner ruling, 2026-08-23: <b>"save where you can, then close"</b>. These tests pin both halves —
/// that a saveable process IS saved first, and that an unsaveable one is terminated and SAID SO rather
/// than being quietly folded in with the graceful closes.</para>
/// </summary>
public class PortalCloseTests
{
    private static readonly DateTime Now = new DateTime(2026, 8, 23, 12, 0, 0);

    private static PortalProcessInfo Proc(
        int pid, string? projectPath = null, bool marked = false, bool visible = true, int ageMinutes = 60) =>
        new PortalProcessInfo(
            pid, projectPath, new DateTime(2026, 8, 23, 11, 0, 0), HasUserInterface: true,
            MarkedByThisTool: marked, StartedAt: Now.AddMinutes(-ageMinutes),
            LaunchedByThisTool: marked, OpennessVisible: visible);

    private static IReadOnlyList<PortalClosePlan> Plan(IEnumerable<PortalProcessInfo> processes, params int[] pids) =>
        PortalClosePlanner.Plan(processes.ToList(), pids, Now);

    private static PortalClosePlan Only(IEnumerable<PortalProcessInfo> processes, params int[] pids) =>
        Assert.Single(Plan(processes, pids));

    // ---- the default sweep -----------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A Portal with a project open is NEVER selected by a sweep.</b> It may be a person's session or
    /// another agent's, and closing it costs someone their afternoon even when the save succeeds.
    /// </summary>
    [Fact]
    public void An_IN_USE_portal_is_never_selected_by_a_sweep()
    {
        var plan = Only(new[] { Proc(100, @"C:\proj\Thing.ap20") });

        Assert.Equal(PortalCloseMethod.Leave, plan.Method);
        Assert.Contains("IN USE", plan.Reason);
        Assert.Contains("--pid", plan.Reason);
    }

    /// <summary>But it IS closable when named — and then it is saved first, which is the owner's ruling.</summary>
    [Fact]
    public void An_IN_USE_portal_named_by_pid_is_SAVED_then_closed()
    {
        var plan = Only(new[] { Proc(100, @"C:\proj\Thing.ap20") }, 100);

        Assert.Equal(PortalCloseMethod.SaveThenClose, plan.Method);
        Assert.Contains("SAVED", plan.Reason);
    }

    [Fact]
    public void A_self_launched_orphan_is_closed_with_nothing_to_save()
    {
        var plan = Only(new[] { Proc(101, marked: true) });

        Assert.Equal(PortalCloseMethod.CloseEmpty, plan.Method);
        Assert.Contains("nothing to save", plan.Reason);
    }

    /// <summary>A stray empty is closable too — and the reason says out loud that it may be someone's window.</summary>
    [Fact]
    public void A_stray_empty_is_closed_but_the_reason_warns_it_may_be_a_persons_window()
    {
        var plan = Only(new[] { Proc(102) });

        Assert.Equal(PortalCloseMethod.CloseEmpty, plan.Method);
        Assert.Contains("will be noticed", plan.Reason);
    }

    // ---- the Openness-invisible case, which is why this command exists ---------------------------

    /// <summary>
    /// 🔴 <b>An old invisible process is TERMINATED, and the plan says it cannot be saved.</b> This is the
    /// branch the ruling's "where you can" carves out: there is nothing to attach to, so there is nothing
    /// to ask to save. It must never be reported as if it were a graceful close.
    /// </summary>
    [Fact]
    public void An_OLD_openness_invisible_process_is_terminated_and_named_as_unsaveable()
    {
        var plan = Only(new[] { Proc(103, visible: false, ageMinutes: 90) });

        Assert.Equal(PortalCloseMethod.KillUnsaveable, plan.Method);
        Assert.Contains("CANNOT BE SAVED", plan.Reason);
        Assert.Contains("terminated", plan.Reason);
    }

    /// <summary>
    /// 🔴 <b>A YOUNG invisible process is left alone — "still starting" is a documented cause of
    /// invisibility.</b> Without this floor, the command would create the pileup it exists to clear.
    /// </summary>
    [Fact]
    public void A_YOUNG_openness_invisible_process_is_left_alone()
    {
        var plan = Only(new[] { Proc(104, visible: false, ageMinutes: 1) });

        Assert.Equal(PortalCloseMethod.Leave, plan.Method);
        Assert.Contains("STILL STARTING", plan.Reason);
    }

    /// <summary>Naming it overrides the floor — the operator can see the screen and the tool cannot.</summary>
    [Fact]
    public void A_young_invisible_process_named_by_pid_is_terminated_anyway()
    {
        var plan = Only(new[] { Proc(104, visible: false, ageMinutes: 1) }, 104);

        Assert.Equal(PortalCloseMethod.KillUnsaveable, plan.Method);
    }

    /// <summary>
    /// 🔴 <b>An invisible process whose age cannot be read is LEFT, not killed.</b> Unknown age cannot rule
    /// out "still starting", and the failure directions are not symmetric: leaving one costs a stale
    /// process, killing one costs somebody's unsaved work.
    /// </summary>
    [Fact]
    public void An_invisible_process_of_UNKNOWN_age_is_left_alone()
    {
        var unknown = new PortalProcessInfo(
            105, null, default(DateTime), HasUserInterface: false, MarkedByThisTool: false,
            StartedAt: null, LaunchedByThisTool: false, OpennessVisible: false);

        var plan = Only(new[] { unknown });

        Assert.Equal(PortalCloseMethod.Leave, plan.Method);
        Assert.Contains("age is unknown", plan.Reason);
    }

    // ---- the denominator -------------------------------------------------------------------------

    /// <summary>
    /// The summary states what was LOOKED AT, not only what was selected — on every run, including the one
    /// that selects nothing. "3 closed" cannot be told from "3 of 9".
    /// </summary>
    [Fact]
    public void The_summary_states_the_denominator_and_flags_the_unsaveable()
    {
        var plans = Plan(new[]
        {
            Proc(100, @"C:\proj\Thing.ap20"),          // in use, left
            Proc(101, marked: true),                    // orphan, closed
            Proc(102),                                  // stray, closed
            Proc(103, visible: false, ageMinutes: 90),  // invisible, terminated
        });

        var summary = PortalClosePlanner.Summarise(plans);

        Assert.Contains("3 of 4 Portal process(es) selected", summary);
        Assert.Contains("1 CANNOT be saved", summary);
        Assert.Contains("TERMINATED", summary);
    }

    /// <summary>An empty machine is NOTHING EXAMINED, not a clean sweep. Empty is not clean (FI-44).</summary>
    [Fact]
    public void No_portal_processes_at_all_is_NOTHING_EXAMINED()
    {
        Assert.Contains("NOTHING EXAMINED", PortalClosePlanner.Summarise(Array.Empty<PortalClosePlan>()));
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL.</b> Most tests above assert something is closed; a planner that selected
    /// everything would satisfy them. A machine holding only a person's open project and a Portal that is
    /// still starting must yield NO targets at all.
    /// </summary>
    [Fact]
    public void A_machine_with_nothing_safe_to_close_selects_NOTHING()
    {
        var plans = Plan(new[]
        {
            Proc(100, @"C:\proj\SomebodysWork.ap20"),
            Proc(101, visible: false, ageMinutes: 1),
        });

        Assert.All(plans, p => Assert.Equal(PortalCloseMethod.Leave, p.Method));
        Assert.Contains("0 of 2", PortalClosePlanner.Summarise(plans));
    }

    /// <summary>Naming a pid that is not running selects nothing and invents nothing.</summary>
    [Fact]
    public void Naming_a_pid_that_is_not_running_changes_nothing()
    {
        var plans = Plan(new[] { Proc(100, @"C:\proj\Thing.ap20") }, 999);

        Assert.All(plans, p => Assert.Equal(PortalCloseMethod.Leave, p.Method));
    }
}
