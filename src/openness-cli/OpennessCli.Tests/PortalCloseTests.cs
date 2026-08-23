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
        int pid, string? projectPath = null, bool marked = false, bool visible = true, int ageMinutes = 60,
        int? attached = null, string? holders = null) =>
        new PortalProcessInfo(
            pid, projectPath, new DateTime(2026, 8, 23, 11, 0, 0), HasUserInterface: true,
            MarkedByThisTool: marked, StartedAt: Now.AddMinutes(-ageMinutes),
            LaunchedByThisTool: marked, OpennessVisible: visible,
            AttachedSessionCount: attached, AttachedSessionHolders: holders);

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

        Assert.Equal(PortalCloseMethod.SaveThenTerminate, plan.Method);
        Assert.Contains("SAVED", plan.Reason);
    }

    [Fact]
    public void A_self_launched_orphan_is_closed_with_nothing_to_save()
    {
        var plan = Only(new[] { Proc(101, marked: true) });

        Assert.Equal(PortalCloseMethod.TerminateEmpty, plan.Method);
        Assert.Contains("nothing to save", plan.Reason);
    }

    /// <summary>A stray empty is closable too — and the reason says out loud that it may be someone's window.</summary>
    [Fact]
    public void A_stray_empty_is_closed_but_the_reason_warns_it_may_be_a_persons_window()
    {
        var plan = Only(new[] { Proc(102) });

        Assert.Equal(PortalCloseMethod.TerminateEmpty, plan.Method);
        Assert.Contains("will be noticed", plan.Reason);
    }

    // ---- somebody is ATTACHED to the empty one (measured 2026-08-23) -----------------------------
    //
    // Until this section existed, "an agent is working in an empty Portal right now" and "abandoned
    // pileup" were the SAME INPUT to this planner, and the sweep took both. The distinguishing fact was
    // measured, not assumed: TiaPortalProcess.AttachedSessions, read from a DIFFERENT process than the
    // one holding the handle, reported the holder's own pid and exe path while it held and 0 items
    // before and after (docs/notes/openness-api-surface-v20.md).

    /// <summary>
    /// 🔴 <b>THE DEFECT THIS SECTION EXISTS FOR.</b> An empty Portal somebody is attached to is a live
    /// session, not pileup, and a sweep must not take it — the same NAMED-OR-NOT-AT-ALL protection an
    /// in-use Portal already had. The old planner returned <c>TerminateEmpty</c> here.
    /// </summary>
    [Fact]
    public void An_ATTACHED_stray_empty_is_NOT_selected_by_a_sweep()
    {
        var plan = Only(new[] { Proc(110, attached: 1, holders: "pid 19536 openness-cli.exe") });

        Assert.Equal(PortalCloseMethod.Leave, plan.Method);
        Assert.Contains("ATTACHED", plan.Reason);
        Assert.Contains("--pid", plan.Reason);
    }

    /// <summary>The reason names WHO holds it — a pid the reader can go and look at is the difference
    /// between a refusal they can act on and one they can only override blindly.</summary>
    [Fact]
    public void The_reason_names_the_process_holding_the_session()
    {
        var plan = Only(new[] { Proc(110, attached: 1, holders: "pid 19536 openness-cli.exe") });

        Assert.Contains("pid 19536", plan.Reason);
    }

    /// <summary>Named by --pid it is still closable, exactly like an in-use one: the guard blocks the
    /// SWEEP, it does not remove the operator's ability to act deliberately.</summary>
    [Fact]
    public void An_attached_stray_empty_named_by_pid_is_still_closed()
    {
        var plan = Only(new[] { Proc(110, attached: 1, holders: "pid 19536 openness-cli.exe") }, 110);

        Assert.Equal(PortalCloseMethod.TerminateEmpty, plan.Method);
        Assert.Contains("ATTACHED", plan.Reason);
    }

    /// <summary>
    /// A self-launched orphan with a live session is another agent's openness-cli mid-run — the registry
    /// mark is machine-wide, not per-invocation, so "this tool launched it" does not mean "nobody is using
    /// it". Same guard, and it is a strict widening of the ask: the defect report named only the stray.
    /// </summary>
    [Fact]
    public void An_ATTACHED_self_launched_orphan_is_NOT_selected_by_a_sweep()
    {
        var plan = Only(new[] { Proc(111, marked: true, attached: 1, holders: "pid 2752 openness-cli.exe") });

        Assert.Equal(PortalCloseMethod.Leave, plan.Method);
        Assert.Contains("ATTACHED", plan.Reason);
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL FOR THE NEW GUARD, and the reason it is a guard rather than a blanket.</b>
    /// A genuinely abandoned empty Portal — measured, zero sessions — is STILL SWEPT. A guard that protects
    /// everything protects nothing, and this command's whole purpose is to clear pileup.
    /// </summary>
    [Fact]
    public void An_UNATTACHED_empty_portal_is_STILL_SWEPT()
    {
        Assert.Equal(PortalCloseMethod.TerminateEmpty, Only(new[] { Proc(112, attached: 0) }).Method);
        Assert.Equal(PortalCloseMethod.TerminateEmpty, Only(new[] { Proc(113, marked: true, attached: 0) }).Method);
    }

    /// <summary>
    /// 🔴 <b>WHAT THE NEW CHECK STILL CANNOT SEE.</b> An Openness-invisible process is not in
    /// <c>GetProcesses()</c> at all, so it can never report an attached session — <c>null</c>, meaning NOT
    /// READ, never "measured zero". It is swept exactly as blindly as before, and that is recorded here so
    /// the gap is a pinned fact rather than a thing somebody assumes the guard covers.
    /// </summary>
    [Fact]
    public void An_openness_invisible_process_can_never_report_attachment_and_is_swept_as_blindly_as_before()
    {
        var invisible = Proc(114, visible: false, ageMinutes: 90);

        Assert.Null(invisible.AttachedSessionCount);
        Assert.False(invisible.HasAttachedSession);
        Assert.Equal(PortalCloseMethod.TerminateUnsaveable, Only(new[] { invisible }).Method);
    }

    /// <summary>
    /// An UNREADABLE session count is not a protection. <c>null</c> means the question was not answered,
    /// and answering it "attached" would make every process this tool cannot interrogate permanently
    /// unsweepable — the pileup this command exists to clear. The blindness is left exactly where it was.
    /// </summary>
    [Fact]
    public void An_unread_session_count_leaves_the_old_behaviour_untouched()
    {
        Assert.Equal(PortalCloseMethod.TerminateEmpty, Only(new[] { Proc(115, attached: null) }).Method);
    }

    /// <summary>The denominator counts the attached one as looked-at-and-left, not as absent.</summary>
    [Fact]
    public void An_attached_empty_portal_is_in_the_denominator_but_not_the_numerator()
    {
        var plans = Plan(new[] { Proc(116, attached: 1, holders: "pid 1 x.exe"), Proc(117, attached: 0) });

        Assert.Contains("1 of 2 Portal process(es) selected", PortalClosePlanner.Summarise(plans));
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

        Assert.Equal(PortalCloseMethod.TerminateUnsaveable, plan.Method);
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

        Assert.Equal(PortalCloseMethod.TerminateUnsaveable, plan.Method);
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
