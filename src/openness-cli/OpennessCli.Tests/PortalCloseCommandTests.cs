using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpennessCli;
using OpennessCli.Cli;
using OpennessCli.Model;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// 🔴 <b>The EXECUTION half of <c>portal-close</c> — the half that ends processes.</b>
///
/// <para><see cref="PortalCloseTests"/> covers the decision: which processes, and how. This covers what
/// happens to that decision afterwards — that <c>--yes</c> actually gates it, that a <c>Leave</c> never
/// reaches the code that can kill something, that a failed save is a full stop, and that the report
/// tells the three terminating outcomes apart.</para>
///
/// <para><b>Every assertion here is on an observable, not on an exit code.</b> An exit code proves how a
/// command ended, not what it did on the way — <see cref="PreConnectRefusalRoutingTests"/> is this
/// assembly's record of learning that, and the sentinel is the same shape: <c>FakeGateway</c> RECORDS
/// each plan it was asked to execute, so "nothing was terminated" is a positive observation.</para>
/// </summary>
public class PortalCloseCommandTests
{
    // Ages are relative to the real clock, because RunPortalClose reads DateTime.Now itself — the
    // injected `now` seam belongs to the planner, which PortalCloseTests drives directly.
    private static PortalProcessInfo Proc(
        int pid, string? projectPath = null, bool marked = false, bool visible = true, int ageMinutes = 60) =>
        new PortalProcessInfo(
            pid, projectPath, DateTime.Now.AddMinutes(-ageMinutes), HasUserInterface: true,
            MarkedByThisTool: marked, StartedAt: DateTime.Now.AddMinutes(-ageMinutes),
            LaunchedByThisTool: marked, OpennessVisible: visible);

    /// <summary>The mixed machine this command exists for: one in use, one orphan, one stray, one invisible.</summary>
    private static IReadOnlyList<PortalProcessInfo> AMixedMachine() => new[]
    {
        Proc(100, @"C:\proj\SomebodysWork.ap20"),
        Proc(101, marked: true),
        Proc(102),
        Proc(103, visible: false, ageMinutes: 90),
    };

    private static (int ExitCode, FakeGateway Gateway, string StdOut, string StdErr) RunCli(
        IReadOnlyList<PortalProcessInfo>? processes, params string[] args)
    {
        var gateway = new FakeGateway { PortalProcesses = processes };
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exit = Program.Run(args, () => gateway);
            return (exit, gateway, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    // ---- 1. --yes gates the whole thing -----------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE ONE THAT MATTERS. Without <c>--yes</c>, NOTHING is executed.</b>
    ///
    /// <para>Asserted on the seam, not on the exit code: <c>ClosedPlans</c> is the list of plans the
    /// gateway was asked to carry out, and a refusal returning 10 after three processes had already been
    /// killed would look identical on the exit code alone.</para>
    /// </summary>
    [Fact]
    public void Without_yes_NOTHING_is_executed()
    {
        var (exit, gateway, stdout, stderr) = RunCli(AMixedMachine(), "portal-close");

        Assert.Empty(gateway.ClosedPlans);

        // Stated separately: reaching Connect would risk the first-connect dialog and, on an empty
        // machine, LAUNCH a Portal — the opposite of a pileup cleanup.
        Assert.Equal(0, gateway.ConnectCalls);
        Assert.Equal(0, gateway.OpenProjectCalls);

        Assert.Equal(ExitCodes.NotConfirmed, exit);

        // The plan is still printed IN FULL, with a reason for every process including the ones being
        // left alone — that is what the operator is being asked to confirm.
        Assert.Contains("PORTAL-CLOSE PLAN", stdout, StringComparison.Ordinal);
        Assert.Contains("3 of 4 Portal process(es) selected", stdout, StringComparison.Ordinal);
        Assert.Contains("reason:", stdout, StringComparison.Ordinal);
        Assert.Contains("IN USE", stdout, StringComparison.Ordinal);

        // And the refusal claims exactly what it can. The process list WAS read; saying "Portal was not
        // contacted", as the other unconfirmed writes do, would be false here.
        Assert.Contains("Nothing was attached to, saved or terminated", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("Portal was not contacted", stderr, StringComparison.Ordinal);
    }

    /// <summary>
    /// *** THE CONTROL FOR THE INSTRUMENT. *** Without it, an empty <c>ClosedPlans</c> is equally what a
    /// parse failure, a gateway that was never constructed, or a test that never ran would produce.
    /// </summary>
    [Fact]
    public void With_yes_the_selected_plans_ARE_executed_so_the_sentinel_means_something()
    {
        var (exit, gateway, _, _) = RunCli(AMixedMachine(), "portal-close", "--yes");

        Assert.Equal(new[] { 101, 102, 103 }, gateway.ClosedPlans.Select(p => p.Process.Pid).ToArray());
        Assert.Equal(ExitCodes.Success, exit);
    }

    // ---- 2. the negative control ------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL, AND IT IS RUN.</b> Most tests here assert that something IS closed; a
    /// command that closed everything would satisfy them all. A machine holding only a person's open
    /// project and a Portal that is still starting must terminate NOTHING — with <c>--yes</c> given, so
    /// the confirm gate is not what is doing the work.
    /// </summary>
    [Fact]
    public void NEGATIVE_CONTROL_an_in_use_portal_and_a_young_invisible_one_terminate_NOTHING()
    {
        var machine = new[]
        {
            Proc(100, @"C:\proj\SomebodysWork.ap20"),
            Proc(101, visible: false, ageMinutes: 1),
        };

        var (exit, gateway, stdout, _) = RunCli(machine, "portal-close", "--yes");

        Assert.Empty(gateway.ClosedPlans);
        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("0 of 2 Portal process(es) selected", stdout, StringComparison.Ordinal);
        Assert.Contains("2 left alone", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("TERMINATED", stdout, StringComparison.Ordinal);
    }

    /// <summary>
    /// A <c>Leave</c> never reaches the gateway even on a run where other plans do. The outcome is built
    /// in <c>Program</c>, so the code that can kill a process is not even asked about a process that must
    /// not be killed — and <c>FakeGateway</c> throws if one arrives anyway.
    /// </summary>
    [Fact]
    public void A_Leave_never_reaches_the_gateway_even_when_other_plans_do()
    {
        var machine = new[] { Proc(100, @"C:\proj\SomebodysWork.ap20"), Proc(101, marked: true) };

        var (_, gateway, stdout, _) = RunCli(machine, "portal-close", "--yes");

        Assert.Equal(new[] { 101 }, gateway.ClosedPlans.Select(p => p.Process.Pid).ToArray());
        Assert.Contains("LEFT ALONE", stdout, StringComparison.Ordinal);
    }

    /// <summary>Naming an in-use Portal by pid is the only route to it, and it is SAVED first.</summary>
    [Fact]
    public void A_pid_named_in_use_portal_is_saved_then_terminated()
    {
        var (exit, gateway, stdout, _) = RunCli(
            new[] { Proc(100, @"C:\proj\SomebodysWork.ap20") }, "portal-close", "--pid", "100", "--yes");

        var executed = Assert.Single(gateway.ClosedPlans);
        Assert.Equal(PortalCloseMethod.SaveThenTerminate, executed.Method);
        Assert.Contains("SAVED then TERMINATED", stdout, StringComparison.Ordinal);
        Assert.Equal(ExitCodes.Success, exit);
    }

    /// <summary>The attach bound is the caller's, not a hidden constant — an unapproved binary hangs silently.</summary>
    [Fact]
    public void The_attach_timeout_is_plumbed_through_from_timeout_connect()
    {
        var (_, gateway, _, _) = RunCli(
            new[] { Proc(101, marked: true) }, "portal-close", "--yes", "--timeout-connect", "42");

        Assert.Equal(TimeSpan.FromSeconds(42), gateway.LastCloseAttachTimeout);
    }

    // ---- 3. a failed save is a full stop ----------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A FAILED SAVE IS NEVER FOLLOWED BY A TERMINATE — asserted on the terminate itself, which is
    /// never invoked.</b>
    ///
    /// <para>This is why the sequencing was pulled out of the gateway. Checking only that the OUTCOME
    /// says <c>SaveFailedNotTerminated</c> would be satisfied by code that killed the process and then
    /// labelled it honestly; the property that matters is that the kill never happened.</para>
    /// </summary>
    [Fact]
    public void A_failed_save_is_NOT_followed_by_a_terminate()
    {
        var terminateCalls = 0;

        var outcome = PortalCloseSequence.Execute(
            SaveThenTerminatePlan(),
            () => new PortalSaveAttempt(PortalSaveOutcome.Failed, "the save FAILED - Project.Save() threw."),
            (kind, detail) =>
            {
                terminateCalls++;
                return PortalCloseOutcome.For(SaveThenTerminatePlan(), kind, detail);
            });

        Assert.Equal(0, terminateCalls);
        Assert.Equal(PortalCloseOutcomeKind.SaveFailedNotTerminated, outcome.Kind);
        Assert.Contains("FAILED", outcome.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// The control for the one above: a save that SUCCEEDS is followed by exactly one terminate. Without
    /// it, "the terminate was not called" is also what a broken <c>Execute</c> that never terminates
    /// anything would produce.
    /// </summary>
    [Fact]
    public void A_successful_save_IS_followed_by_exactly_one_terminate()
    {
        var terminateCalls = 0;

        var outcome = PortalCloseSequence.Execute(
            SaveThenTerminatePlan(),
            () => new PortalSaveAttempt(PortalSaveOutcome.Saved, "saved: SomebodysWork."),
            (kind, detail) =>
            {
                terminateCalls++;
                return PortalCloseOutcome.For(SaveThenTerminatePlan(), kind, detail);
            });

        Assert.Equal(1, terminateCalls);
        Assert.Equal(PortalCloseOutcomeKind.SavedThenTerminated, outcome.Kind);
    }

    /// <summary>
    /// The two no-save methods never invoke the save at all. Attaching to a stray empty Portal to ask it
    /// to save nothing would be this command taking the first-connect-dialog risk for no reason.
    /// </summary>
    [Theory]
    [InlineData(PortalCloseMethod.TerminateEmpty, PortalCloseOutcomeKind.TerminatedNothingToSave)]
    [InlineData(PortalCloseMethod.TerminateUnsaveable, PortalCloseOutcomeKind.TerminatedWithoutSaving)]
    public void A_terminate_only_method_never_attempts_a_save(
        PortalCloseMethod method, PortalCloseOutcomeKind expected)
    {
        var saveCalls = 0;
        var plan = PlanFor(Proc(101, marked: true), PortalProcessClass.StrayEmpty, method);

        var outcome = PortalCloseSequence.Execute(
            plan,
            () =>
            {
                saveCalls++;
                return new PortalSaveAttempt(PortalSaveOutcome.Saved, "should never happen");
            },
            (kind, detail) => PortalCloseOutcome.For(plan, kind, detail));

        Assert.Equal(0, saveCalls);
        Assert.Equal(expected, outcome.Kind);
    }

    /// <summary>A <c>Leave</c> arriving at the executor is a loud throw, never a quiet no-op.</summary>
    [Fact]
    public void PortalCloseSequence_REFUSES_a_Leave_plan()
    {
        var plan = PlanFor(Proc(100, @"C:\proj\Thing.ap20"), PortalProcessClass.InUse, PortalCloseMethod.Leave);

        var ex = Assert.Throws<ArgumentException>(() => PortalCloseSequence.Execute(
            plan,
            () => throw new InvalidOperationException("must not be reached"),
            (_, _) => throw new InvalidOperationException("must not be reached")));

        Assert.Contains("Leave", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// End to end: a failed save is reported as such, warned about loudly, and gates the exit code. The
    /// process is still running, which is a thing the operator has to go and deal with.
    /// </summary>
    [Fact]
    public void A_failed_save_is_reported_warned_about_and_exits_CommandError()
    {
        var gateway = new FakeGateway { PortalProcesses = new[] { Proc(100, @"C:\proj\SomebodysWork.ap20") } };
        var originalOut = Console.Out;
        var stdout = new StringWriter();
        int exit;
        try
        {
            Console.SetOut(stdout);
            Console.SetError(new StringWriter());

            // Configured by pid, so the plan is the real one and only its execution is bent.
            gateway.CloseOutcomes[100] = new PortalCloseOutcome(
                100, PortalProcessClass.InUse, PortalCloseMethod.SaveThenTerminate,
                PortalCloseOutcomeKind.SaveFailedNotTerminated, @"C:\proj\SomebodysWork.ap20",
                "the save FAILED - EngineeringTargetInvocationException: the project is locked.");

            exit = Program.Run(new[] { "portal-close", "--pid", "100", "--yes" }, () => gateway);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(Console.Error);
        }

        var text = stdout.ToString();
        Assert.Contains("SAVE FAILED - NOT TERMINATED (still running)", text, StringComparison.Ordinal);
        Assert.Contains("LEFT RUNNING", text, StringComparison.Ordinal);
        Assert.Contains("Terminating one would have destroyed the work", text, StringComparison.Ordinal);
        Assert.Equal(ExitCodes.CommandError, exit);
    }

    // ---- 4. the three outcomes must be tellable apart ---------------------------------------------

    /// <summary>
    /// 🔴 <b>SAVED / nothing-to-save / TERMINATED WITHOUT SAVING / left alone all render differently, and
    /// the unsaveable one is never folded in with the others.</b>
    ///
    /// <para>That third case is the branch the owner's <i>"save where you CAN"</i> carves out. It is not a
    /// defect — but a report printing one word for all three would tell the reader the ruling was honoured
    /// in a case where it could not be.</para>
    /// </summary>
    [Fact]
    public void The_four_outcomes_render_distinguishably()
    {
        var plans = MixedPlans();
        var outcomes = new[]
        {
            PortalCloseOutcome.LeftAlone(plans[0]),
            PortalCloseOutcome.For(plans[1], PortalCloseOutcomeKind.SavedThenTerminated, "saved: Thing."),
            PortalCloseOutcome.For(plans[2], PortalCloseOutcomeKind.TerminatedNothingToSave, "nothing was open."),
            PortalCloseOutcome.For(plans[3], PortalCloseOutcomeKind.TerminatedWithoutSaving, "Openness cannot see it."),
        };

        var text = OutputFormatter.FormatPortalCloseTable(plans, outcomes);

        Assert.Contains("LEFT ALONE", text, StringComparison.Ordinal);
        Assert.Contains("SAVED then TERMINATED", text, StringComparison.Ordinal);
        Assert.Contains("TERMINATED (nothing to save)", text, StringComparison.Ordinal);
        Assert.Contains("TERMINATED WITHOUT SAVING", text, StringComparison.Ordinal);

        // The counts are separate too, not one "3 closed".
        Assert.Contains(
            "RESULT: 1 SAVED then terminated, 1 terminated with nothing to save, 1 TERMINATED WITHOUT SAVING, 1 left alone.",
            text,
            StringComparison.Ordinal);

        // And the unsaveable one earns a footer naming its pid, so it cannot be skimmed past.
        Assert.Contains("TERMINATED WITHOUT BEING SAVED (pid(s) 103)", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>"Nothing was open" and "the API would not say" are different facts, and the executed report
    /// used to print "(none)" for both.</b>
    ///
    /// <para>Found by reading this command's own output rather than by a test — the same shape as the
    /// <c>AcquiredPrecedesStart</c> defect: <c>PortalProcessInfo</c> carefully models the absence, and a
    /// layer above re-consumes it as a measurement. On an Openness-invisible process, whether a project is
    /// open is exactly what nobody knows — and that process is the one being terminated WITHOUT a save.</para>
    /// </summary>
    [Fact]
    public void An_openness_invisible_process_never_reports_that_no_project_was_open()
    {
        var plans = MixedPlans();
        var invisible = plans[3];
        var outcomes = new[]
        {
            PortalCloseOutcome.For(invisible, PortalCloseOutcomeKind.TerminatedWithoutSaving, "terminated."),
        };

        var text = OutputFormatter.FormatPortalCloseTable(new[] { invisible }, outcomes);

        Assert.Contains("project: (unknown - not visible to Openness)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("project: (none)", text, StringComparison.Ordinal);
    }

    /// <summary>The control: a VISIBLE empty process genuinely has nothing open, and says so.</summary>
    [Fact]
    public void A_visible_empty_process_does_report_that_nothing_was_open()
    {
        var plans = MixedPlans();
        var orphan = plans[2];
        var outcomes = new[]
        {
            PortalCloseOutcome.For(orphan, PortalCloseOutcomeKind.TerminatedNothingToSave, "terminated."),
        };

        Assert.Contains(
            "project: (none)",
            OutputFormatter.FormatPortalCloseTable(new[] { orphan }, outcomes),
            StringComparison.Ordinal);
    }

    /// <summary>The same four survive the JSON shape as distinct machine-readable values.</summary>
    [Fact]
    public void The_four_outcomes_are_distinct_in_json_too()
    {
        var plans = MixedPlans();
        var outcomes = new[]
        {
            PortalCloseOutcome.LeftAlone(plans[0]),
            PortalCloseOutcome.For(plans[1], PortalCloseOutcomeKind.SavedThenTerminated, "saved."),
            PortalCloseOutcome.For(plans[2], PortalCloseOutcomeKind.TerminatedNothingToSave, "empty."),
            PortalCloseOutcome.For(plans[3], PortalCloseOutcomeKind.TerminatedWithoutSaving, "invisible."),
        };

        using var doc = JsonDocument.Parse(OutputFormatter.FormatPortalCloseJson(plans, outcomes));
        var kinds = doc.RootElement.GetProperty("outcomes").EnumerateArray()
            .Select(o => o.GetProperty("outcome").GetString())
            .ToArray();

        Assert.Equal(
            new[] { "LeftAlone", "SavedThenTerminated", "TerminatedNothingToSave", "TerminatedWithoutSaving" },
            kinds);
        Assert.True(doc.RootElement.GetProperty("executed").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("anyFailed").GetBoolean());
    }

    // ---- 5. the denominator, on every run ---------------------------------------------------------

    /// <summary>
    /// The planner's denominator is printed on the dry run AND on the executed run. "3 closed" cannot be
    /// told from "3 of 9 closed", and only the second says what was looked at.
    /// </summary>
    [Fact]
    public void The_denominator_is_printed_on_both_the_dry_run_and_the_executed_run()
    {
        var (_, _, dryStdout, _) = RunCli(AMixedMachine(), "portal-close");
        var (_, _, runStdout, _) = RunCli(AMixedMachine(), "portal-close", "--yes");

        Assert.Contains("SELECTED: 3 of 4 Portal process(es) selected", dryStdout, StringComparison.Ordinal);
        Assert.Contains("SELECTED: 3 of 4 Portal process(es) selected", runStdout, StringComparison.Ordinal);
    }

    /// <summary>
    /// An empty machine says NOTHING EXAMINED — and exits 0, because unlike a compile gate, "there are no
    /// stray Portal processes" is this command's desired end state rather than an unanswered question.
    /// </summary>
    [Fact]
    public void No_portal_processes_at_all_reports_NOTHING_EXAMINED_and_succeeds()
    {
        var (exit, gateway, stdout, _) = RunCli(Array.Empty<PortalProcessInfo>(), "portal-close", "--yes");

        Assert.Empty(gateway.ClosedPlans);
        Assert.Contains("NOTHING EXAMINED", stdout, StringComparison.Ordinal);
        Assert.Contains("RESULT: nothing was examined, so nothing was closed.", stdout, StringComparison.Ordinal);
        Assert.Equal(ExitCodes.Success, exit);
    }

    // ---- 6. pid identity: the kill is checked against the process it was planned for ---------------

    private static readonly string[] PortalNames = { "Siemens.Automation.Portal", "TIAPortal" };

    [Fact]
    public void An_unchanged_pid_is_not_a_mismatch()
    {
        var planned = Proc(100, @"C:\proj\Thing.ap20");

        Assert.Null(PortalCloseIdentity.Mismatch(
            PortalNames, "Siemens.Automation.Portal", planned.StartedAt, planned));
    }

    /// <summary>
    /// 🔴 <b>A REUSED PID.</b> The planned Portal exited and the operating system handed its number to
    /// something else. This command runs by definition on machines where Portal processes are dying, so
    /// this is not hypothetical — and killing by a stale pid is how a janitor takes out something nobody
    /// asked it about.
    /// </summary>
    [Fact]
    public void A_pid_that_now_names_a_different_program_is_a_mismatch()
    {
        var planned = Proc(100, @"C:\proj\Thing.ap20");

        var mismatch = PortalCloseIdentity.Mismatch(PortalNames, "notepad", planned.StartedAt, planned);

        Assert.NotNull(mismatch);
        Assert.Contains("REUSED", mismatch!, StringComparison.Ordinal);
    }

    /// <summary>A pid recycled onto ANOTHER Portal is the case a name check alone cannot catch.</summary>
    [Fact]
    public void A_pid_that_now_names_a_DIFFERENT_portal_is_a_mismatch()
    {
        var planned = Proc(100, @"C:\proj\Thing.ap20", ageMinutes: 90);

        var mismatch = PortalCloseIdentity.Mismatch(
            PortalNames, "Siemens.Automation.Portal", DateTime.Now.AddMinutes(-1), planned);

        Assert.NotNull(mismatch);
        Assert.Contains("DIFFERENT PROCESS", mismatch!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_process_whose_name_cannot_be_read_is_a_mismatch()
    {
        var planned = Proc(100, @"C:\proj\Thing.ap20");

        Assert.NotNull(PortalCloseIdentity.Mismatch(PortalNames, null, planned.StartedAt, planned));
    }

    /// <summary>
    /// The plan recorded no start time (the enumeration could not read one). The name check still has to
    /// pass, and that is as far as the evidence goes — stated rather than silently upgraded to a match.
    /// </summary>
    [Fact]
    public void A_planned_process_with_no_recorded_start_time_falls_back_to_the_name_check()
    {
        var planned = new PortalProcessInfo(
            100, null, default(DateTime), HasUserInterface: false, MarkedByThisTool: false,
            StartedAt: null, LaunchedByThisTool: false, OpennessVisible: false);

        Assert.Null(PortalCloseIdentity.Mismatch(PortalNames, "Siemens.Automation.Portal", null, planned));
        Assert.NotNull(PortalCloseIdentity.Mismatch(PortalNames, "notepad", null, planned));
    }

    // ---- 7. the arguments -------------------------------------------------------------------------

    [Fact]
    public void Pid_is_repeatable()
    {
        var parsed = Assert.IsType<ParseResult.PortalCloseSuccess>(
            ArgumentParser.Parse(new[] { "portal-close", "--pid", "100", "--pid", "204", "--yes" }));

        Assert.Equal(new[] { 100, 204 }, parsed.Options.Pids.ToArray());
        Assert.True(parsed.Options.Confirm);
    }

    /// <summary>
    /// A non-positive pid names no process. Refused by name rather than landing silently in the
    /// "matched nothing" case, which reads exactly like a thorough sweep that found nothing to do.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void A_non_positive_pid_is_refused(string pid)
    {
        var failure = Assert.IsType<ParseResult.Failure>(
            ArgumentParser.Parse(new[] { "portal-close", "--pid", pid }));

        Assert.Contains("'--pid' requires a positive integer", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_positional_argument_is_refused_because_this_command_targets_no_project()
    {
        var failure = Assert.IsType<ParseResult.Failure>(
            ArgumentParser.Parse(new[] { "portal-close", @"C:\proj\My.ap20", "--yes" }));

        Assert.Contains("takes no <project>", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Portal_close_is_listed_in_the_unknown_subcommand_help()
    {
        var failure = Assert.IsType<ParseResult.Failure>(ArgumentParser.Parse(new[] { "portal-clsoe" }));

        Assert.Contains("portal-close", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>The dry run emits parseable JSON on stdout, with the prose refusal on stderr.</summary>
    [Fact]
    public void The_dry_run_json_carries_the_plan_and_says_it_was_not_executed()
    {
        var (exit, _, stdout, stderr) = RunCli(AMixedMachine(), "portal-close", "--json");

        using var doc = JsonDocument.Parse(stdout);
        Assert.False(doc.RootElement.GetProperty("executed").GetBoolean());
        Assert.Equal(4, doc.RootElement.GetProperty("plans").GetArrayLength());
        Assert.Contains("3 of 4", doc.RootElement.GetProperty("summary").GetString()!, StringComparison.Ordinal);

        Assert.Equal(ExitCodes.NotConfirmed, exit);
        Assert.Contains("Re-run with --yes", stderr, StringComparison.Ordinal);
    }

    // ---- helpers -----------------------------------------------------------------------------------

    private static PortalClosePlan PlanFor(
        PortalProcessInfo process, PortalProcessClass klass, PortalCloseMethod method) =>
        new PortalClosePlan(process, klass, method, "test plan");

    private static PortalClosePlan SaveThenTerminatePlan() =>
        PlanFor(Proc(100, @"C:\proj\Thing.ap20"), PortalProcessClass.InUse, PortalCloseMethod.SaveThenTerminate);

    /// <summary>One plan of each method, in the order the outcome tests expect them.</summary>
    private static IReadOnlyList<PortalClosePlan> MixedPlans() => new[]
    {
        PlanFor(Proc(100, @"C:\proj\SomebodysWork.ap20"), PortalProcessClass.InUse, PortalCloseMethod.Leave),
        PlanFor(Proc(101, @"C:\proj\Thing.ap20"), PortalProcessClass.InUse, PortalCloseMethod.SaveThenTerminate),
        PlanFor(Proc(102, marked: true), PortalProcessClass.SelfLaunchedOrphan, PortalCloseMethod.TerminateEmpty),
        PlanFor(
            Proc(103, visible: false, ageMinutes: 90),
            PortalProcessClass.OpennessInvisible,
            PortalCloseMethod.TerminateUnsaveable),
    };
}
