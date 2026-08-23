using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpennessCli.Model;

namespace OpennessCli.Openness;

/// <summary>
/// What actually HAPPENED to one process — as distinct from <see cref="PortalCloseMethod"/>, which is
/// what was going to happen to it.
///
/// <para>🔴 <b>The three terminating outcomes are kept apart on purpose, and must never be collapsed
/// into "closed".</b> The owner's ruling is <i>"save where you can, then close"</i>; a report that
/// prints one word for all three tells the reader the ruling was honoured when in one of the three
/// cases it could not be. That third case is not a defect — it is the branch the ruling's <i>"where
/// you can"</i> carves out — but it is only honest if it is visible.</para>
/// </summary>
public enum PortalCloseOutcomeKind
{
    /// <summary>Not a target. Nothing was attached to, saved, or terminated.</summary>
    LeftAlone = 0,

    /// <summary>The project was saved, and then the process was terminated. The ruling's happy path.</summary>
    SavedThenTerminated = 1,

    /// <summary>Terminated. Nothing was open in it, so no work existed to lose.</summary>
    TerminatedNothingToSave = 2,

    /// <summary>
    /// 🔴 <b>Terminated WITHOUT a save.</b> Openness could not see the process, so there was nothing to
    /// attach to and nothing to ask. Anything unsaved in it is gone.
    /// </summary>
    TerminatedWithoutSaving = 3,

    /// <summary>
    /// 🔴 <b>The save was attempted and did not succeed, so the process was LEFT RUNNING.</b>
    ///
    /// <para>This is the one rule that overrides the command's whole purpose. If we could have saved and
    /// did not manage to, terminating destroys exactly the work the ruling exists to protect — so a
    /// failed save is a full stop, not a step that gets retried past. The stale process survives, which
    /// is the cheap failure; the alternative is the expensive one.</para>
    /// </summary>
    SaveFailedNotTerminated = 4,

    /// <summary>The terminate itself failed, or the process had not exited afterwards. Still running.</summary>
    TerminateFailed = 5,

    /// <summary>
    /// 🔴 <b>The pid no longer names the process the plan was made about, so nothing was terminated.</b>
    /// Pids are reused, and this command runs by definition on a machine where Portal processes are
    /// dying. Killing by a stale pid is how a janitor takes out something it was never asked about.
    /// </summary>
    IdentityChangedNotTerminated = 6,
}

/// <summary>One executed <see cref="PortalClosePlan"/>: what was planned, what happened, and why.</summary>
/// <param name="Detail">
/// Prose, and it ACCUMULATES: a save that succeeded and a terminate that then failed both appear here,
/// because "saved but still running" is a different thing for the reader to do than either half alone.
/// </param>
public sealed record PortalCloseOutcome(
    int Pid,
    PortalProcessClass Class,
    PortalCloseMethod Planned,
    PortalCloseOutcomeKind Kind,
    string? ProjectPath,
    string Detail)
{
    /// <summary>
    /// The outcome for a plan that was never executed. Built HERE rather than by the gateway, so that a
    /// <c>Leave</c> never reaches the code that can terminate something — see
    /// <c>OpennessGateway.ClosePortalProcess</c>, which refuses one outright.
    /// </summary>
    public static PortalCloseOutcome LeftAlone(PortalClosePlan plan) =>
        For(plan, PortalCloseOutcomeKind.LeftAlone, plan.Reason);

    public static PortalCloseOutcome For(PortalClosePlan plan, PortalCloseOutcomeKind kind, string detail) =>
        new PortalCloseOutcome(
            plan.Process.Pid, plan.Class, plan.Method, kind, plan.Process.ProjectPath, detail);

    /// <summary>Whether this outcome is one the operator has to go and do something about.</summary>
    public bool IsFailure =>
        Kind is PortalCloseOutcomeKind.SaveFailedNotTerminated
            or PortalCloseOutcomeKind.TerminateFailed
            or PortalCloseOutcomeKind.IdentityChangedNotTerminated;
}

/// <summary>How an attempt to save one process's open project(s) came out.</summary>
public enum PortalSaveOutcome
{
    /// <summary>Every open project was saved.</summary>
    Saved = 0,

    /// <summary>Nothing was open, so there was nothing to save. Not a failure.</summary>
    NothingOpen = 1,

    /// <summary>
    /// 🔴 <b>The save did not happen.</b> Whatever the cause — Openness no longer lists the process, the
    /// attach timed out, <c>Project.Save()</c> threw — the work is still only in memory.
    /// </summary>
    Failed = 2,
}

/// <param name="Detail">Prose for the report. Always populated, including on success.</param>
public sealed record PortalSaveAttempt(PortalSaveOutcome Outcome, string Detail);

/// <summary>
/// 🔴 <b>The order the two destructive steps happen in, and the one case where the second must not
/// happen at all.</b>
///
/// <para>Pure, with the save and the terminate injected, for a specific reason: <b>"a failed save is
/// never followed by a terminate" is the rule that overrides this whole command's purpose, and it was
/// otherwise provable only by reading the gateway.</b> With the seam here, a test supplies a save that
/// fails and a terminate that RECORDS, and asserts the terminate was never invoked — the thing that
/// must not happen is observed, not inferred from an outcome value the same code chose.</para>
///
/// <para>The gateway keeps the mechanics (attach, <c>Project.Save()</c>, <c>Kill()</c>) and this keeps
/// the judgement — the same split <see cref="PortalClosePlanner"/> already makes one level up.</para>
/// </summary>
public static class PortalCloseSequence
{
    /// <param name="plan">A non-<see cref="PortalCloseMethod.Leave"/> plan.</param>
    /// <param name="save">
    /// Attach-and-save. <b>Invoked for <see cref="PortalCloseMethod.SaveThenTerminate"/> and nothing
    /// else</b> — the other two methods are for processes where no save is possible or needed, and
    /// attaching to one would be this command taking a risk it was told not to take.
    /// </param>
    /// <param name="terminate">
    /// Kill the process and report it, given the outcome kind to use on success and the detail so far.
    /// <b>Not invoked at all when a save was attempted and failed.</b>
    /// </param>
    public static PortalCloseOutcome Execute(
        PortalClosePlan plan,
        Func<PortalSaveAttempt> save,
        Func<PortalCloseOutcomeKind, string, PortalCloseOutcome> terminate)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (save == null) throw new ArgumentNullException(nameof(save));
        if (terminate == null) throw new ArgumentNullException(nameof(terminate));

        // Defence in depth. Program builds the LeftAlone outcome itself and never routes a Leave here,
        // so this can only fire on a bug — and the answer is a loud throw, never a quiet no-op, because
        // a Leave reaching the code that can kill things is the one bug in this command that costs
        // somebody their afternoon.
        if (plan.Method == PortalCloseMethod.Leave)
        {
            throw new ArgumentException(
                $"Refusing: the plan for pid {plan.Process.Pid} is Leave, and this only executes " +
                "terminations. A Leave must never reach the code that can kill a process.",
                nameof(plan));
        }

        if (plan.Method != PortalCloseMethod.SaveThenTerminate)
        {
            return plan.Method == PortalCloseMethod.TerminateUnsaveable
                ? terminate(
                    PortalCloseOutcomeKind.TerminatedWithoutSaving,
                    "Openness cannot see this process, so no save was possible and none was attempted.")
                : terminate(
                    PortalCloseOutcomeKind.TerminatedNothingToSave,
                    "nothing was open in this process, so no save was needed.");
        }

        var attempt = save();

        // 🔴 FULL STOP, NOT A STEP TO PUSH PAST. If we could have saved and did not manage to,
        // terminating destroys exactly the work the owner's ruling exists to protect. The stale process
        // survives — that is the cheap failure, and it is the one this takes.
        if (attempt.Outcome == PortalSaveOutcome.Failed)
        {
            return PortalCloseOutcome.For(plan, PortalCloseOutcomeKind.SaveFailedNotTerminated, attempt.Detail);
        }

        return attempt.Outcome == PortalSaveOutcome.NothingOpen
            ? terminate(PortalCloseOutcomeKind.TerminatedNothingToSave, attempt.Detail)
            : terminate(PortalCloseOutcomeKind.SavedThenTerminated, attempt.Detail);
    }
}

/// <summary>
/// 🔴 <b>Is this pid still the process the plan was made about?</b>
///
/// <para>Pure, so it is testable without a process table. Split out of the gateway because it is real
/// judgement and not plumbing: <b>pids are reused</b>, the window between the enumeration that produced
/// a plan and the kill that executes it is unbounded, and this command exists precisely for machines
/// where Portal processes are appearing and dying. The same evidence pairing the lease store uses —
/// identity is holder + pid + PROCESS START TIME, because pids are reused.</para>
///
/// <para>Fails CLOSED in every direction: a name that cannot be read, a name that is not Portal's, and
/// a start time that disagrees all mean "do not terminate". The cost of a false refusal is one stale
/// Portal; the cost of a false kill is somebody else's process.</para>
/// </summary>
public static class PortalCloseIdentity
{
    /// <summary>
    /// Clock granularity between the enumeration's read of <c>Process.StartTime</c> and this one. They
    /// are the same underlying value, so this is slack against formatting/rounding, not a tolerance for
    /// a genuinely different process — a reused pid is separated from its predecessor by the
    /// predecessor's whole lifetime, never by a second.
    /// </summary>
    public const double StartTimeToleranceSeconds = 1.0;

    /// <returns>Null when the pid still names the planned process; otherwise the reason it does not.</returns>
    public static string? Mismatch(
        IReadOnlyCollection<string> portalProcessNames,
        string? actualProcessName,
        DateTime? actualStart,
        PortalProcessInfo planned)
    {
        if (portalProcessNames == null) throw new ArgumentNullException(nameof(portalProcessNames));
        if (planned == null) throw new ArgumentNullException(nameof(planned));

        if (string.IsNullOrEmpty(actualProcessName))
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "pid {0}: its own process name could not be read, so it cannot be confirmed as the Portal the " +
                "plan was made about. NOT terminated.",
                planned.Pid);
        }

        if (!portalProcessNames.Contains(actualProcessName!, StringComparer.OrdinalIgnoreCase))
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "pid {0} is now '{1}', which is not a TIA Portal process - the planned process has exited and " +
                "its pid has been REUSED. NOT terminated.",
                planned.Pid, actualProcessName);
        }

        // No recorded start time means the enumeration could not read one either. The planner already
        // refuses to sweep such a process (only an explicit --pid reaches here), and the name check
        // above still had to pass, so this is as far as the evidence goes - said out loud rather than
        // silently treated as a match.
        if (planned.StartedAt is not DateTime plannedStart)
        {
            return null;
        }

        if (actualStart is not DateTime started)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "pid {0}: the plan recorded a start time of {1:yyyy-MM-dd HH:mm:ss} and the process will not " +
                "report one now, so the two cannot be compared. NOT terminated.",
                planned.Pid, plannedStart);
        }

        if (Math.Abs((started - plannedStart).TotalSeconds) > StartTimeToleranceSeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "pid {0} now reports start time {1:yyyy-MM-dd HH:mm:ss}, but the plan was made about a process " +
                "started {2:yyyy-MM-dd HH:mm:ss} - same pid, DIFFERENT PROCESS. NOT terminated.",
                planned.Pid, started, plannedStart);
        }

        return null;
    }
}

/// <summary>
/// The executed run's summary line. Sibling of <see cref="PortalClosePlanner.Summarise"/>, which states
/// what was GOING to happen; this states what did.
/// </summary>
public static class PortalCloseReport
{
    /// <summary>
    /// One line per outcome kind that occurred, with its count, and never a rolled-up "closed" total.
    ///
    /// <para>🔴 <b>The unsaveable terminations are named separately even when everything went to plan.</b>
    /// That is the whole reporting requirement: a run that terminated three processes, one of which
    /// could not be saved, is not the same event as a run that saved all three, and the difference is
    /// invisible in any count that adds them together.</para>
    /// </summary>
    public static string SummariseOutcomes(IReadOnlyList<PortalCloseOutcome> outcomes)
    {
        if (outcomes == null) throw new ArgumentNullException(nameof(outcomes));

        if (outcomes.Count == 0)
        {
            return "RESULT: nothing was examined, so nothing was closed.";
        }

        var parts = new List<string>();
        void Count(PortalCloseOutcomeKind kind, string singular)
        {
            var n = outcomes.Count(o => o.Kind == kind);
            if (n > 0)
            {
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1}", n, singular));
            }
        }

        Count(PortalCloseOutcomeKind.SavedThenTerminated, "SAVED then terminated");
        Count(PortalCloseOutcomeKind.TerminatedNothingToSave, "terminated with nothing to save");
        Count(PortalCloseOutcomeKind.TerminatedWithoutSaving, "TERMINATED WITHOUT SAVING");
        Count(PortalCloseOutcomeKind.LeftAlone, "left alone");
        Count(PortalCloseOutcomeKind.SaveFailedNotTerminated, "SAVE FAILED and so NOT terminated");
        Count(PortalCloseOutcomeKind.TerminateFailed, "TERMINATE FAILED");
        Count(PortalCloseOutcomeKind.IdentityChangedNotTerminated, "not terminated (pid no longer names that process)");

        return "RESULT: " + string.Join(", ", parts) + ".";
    }

    /// <summary>
    /// The loud footer, printed only when there is something to be loud about. Two separate warnings
    /// because they send the reader to different places: one says work was destroyed, the other says
    /// work SURVIVED and a process is still running.
    /// </summary>
    public static IReadOnlyList<string> Warnings(IReadOnlyList<PortalCloseOutcome> outcomes)
    {
        if (outcomes == null) throw new ArgumentNullException(nameof(outcomes));

        var warnings = new List<string>();

        var unsaved = outcomes.Where(o => o.Kind == PortalCloseOutcomeKind.TerminatedWithoutSaving).ToList();
        if (unsaved.Count > 0)
        {
            warnings.Add(string.Format(
                CultureInfo.InvariantCulture,
                "*** {0} process(es) were TERMINATED WITHOUT BEING SAVED (pid(s) {1}). Openness could not see " +
                "them, so they could not be asked to save. Anything unsaved in them is gone. ***",
                unsaved.Count,
                string.Join(", ", unsaved.Select(o => o.Pid.ToString(CultureInfo.InvariantCulture)))));
        }

        var saveFailed = outcomes.Where(o => o.Kind == PortalCloseOutcomeKind.SaveFailedNotTerminated).ToList();
        if (saveFailed.Count > 0)
        {
            warnings.Add(string.Format(
                CultureInfo.InvariantCulture,
                "*** {0} process(es) COULD NOT BE SAVED and were therefore LEFT RUNNING (pid(s) {1}). " +
                "Terminating one would have destroyed the work the save was for. They are still there; deal " +
                "with them in TIA Portal, or re-run once the save succeeds. ***",
                saveFailed.Count,
                string.Join(", ", saveFailed.Select(o => o.Pid.ToString(CultureInfo.InvariantCulture)))));
        }

        var stillRunning = outcomes
            .Where(o => o.Kind is PortalCloseOutcomeKind.TerminateFailed
                or PortalCloseOutcomeKind.IdentityChangedNotTerminated)
            .ToList();
        if (stillRunning.Count > 0)
        {
            warnings.Add(string.Format(
                CultureInfo.InvariantCulture,
                "*** {0} process(es) were selected and NOT terminated (pid(s) {1}). See the DETAIL column. ***",
                stillRunning.Count,
                string.Join(", ", stillRunning.Select(o => o.Pid.ToString(CultureInfo.InvariantCulture)))));
        }

        return warnings;
    }

    public static bool AnyFailed(IReadOnlyList<PortalCloseOutcome> outcomes)
    {
        if (outcomes == null) throw new ArgumentNullException(nameof(outcomes));
        return outcomes.Any(o => o.IsFailure);
    }
}
