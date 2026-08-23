using System;
using System.Collections.Generic;
using System.Linq;
using OpennessCli.Model;

namespace OpennessCli.Openness;

/// <summary>
/// How a process would be closed.
///
/// <para>🔴 <b>EVERY CLOSE IS A TERMINATE, AND THAT IS NOT A DESIGN CHOICE — IT IS WHAT OPENNESS
/// OFFERS.</b> <c>TiaPortal.Dispose()</c> on a handle obtained by <c>Attach()</c> releases only THIS
/// tool's reference; it does not close the process or touch what is open in it
/// (<c>OpennessGateway.DisposeAllExcept</c> relies on exactly that, and this project has attached to
/// human sessions across many live tests without ever closing one). Disposing self-launched instances is
/// the only case where it ends a process. <b>So for anything this tool did not launch, "close" means
/// terminating the OS process, and the ONLY variable is whether a save happened first.</b></para>
///
/// <para>An earlier draft of this enum modelled a "graceful close through Openness" for empty instances.
/// There is no such operation. Recorded rather than quietly corrected, because the wrong model would have
/// produced a command that reported closing something it had merely let go of.</para>
/// </summary>
public enum PortalCloseMethod
{
    /// <summary>Not a target. <see cref="PortalClosePlan.Reason"/> says why.</summary>
    Leave = 0,

    /// <summary>
    /// Attach, <b>SAVE the open project</b>, release, then terminate. The owner's ruling — <i>"save where
    /// you can, then close"</i> — and this is the branch where you can.
    /// </summary>
    SaveThenTerminate = 1,

    /// <summary>Terminate. Openness can see it and nothing is open, so there is nothing to save.</summary>
    TerminateEmpty = 2,

    /// <summary>
    /// 🔴 <b>Terminate WITHOUT saving — Openness cannot see it, so it cannot be asked.</b> This is the
    /// branch the ruling's <i>"where you can"</i> carves out, and it is reported by name on every run
    /// rather than folded in with the ones that were saved.
    /// </summary>
    TerminateUnsaveable = 3,
}

/// <summary>What will happen to one process, and why.</summary>
public sealed record PortalClosePlan(
    PortalProcessInfo Process,
    PortalProcessClass Class,
    PortalCloseMethod Method,
    string Reason);

/// <summary>
/// 🔴 <b>Which Portal processes to close, decided WITHOUT touching any of them.</b>
///
/// <para>Pure classification over a snapshot — no Siemens types, no COM — so every branch is testable
/// with hand-built lists, exactly like <see cref="PortalStatusClassifier"/>, which it builds on. The
/// enumeration that feeds it is the untestable part and stays in the gateway.</para>
///
/// <para><b>Why this exists.</b> Stale Portal pileup is a recorded cause of "the second instance won't
/// connect", and on 2026-08-22 two Openness-invisible processes forced a rig run to use the
/// <c>--attest-portal-unjudgeable</c> escape — a human vouching in prose for something no tool could
/// decide. Closing them was out of scope until the owner ruled on 2026-08-23: <b>"save where you can,
/// then close"</b>.</para>
///
/// <para>🔴 <b>THE DANGEROUS TARGET IS THE ONE WITH A PROJECT OPEN, and it is never in the default
/// set.</b> An in-use Portal may be a person's session or another agent's; closing it costs someone
/// their afternoon even when the save succeeds. It is closable, but only when named by <c>--pid</c>, so
/// that no sweep can ever take one by accident.</para>
/// </summary>
public static class PortalClosePlanner
{
    /// <summary>
    /// An Openness-invisible process younger than this is left alone. <b>"Still starting" is a documented
    /// cause of invisibility</b> — as is "has just died" — and killing a Portal that is three seconds into
    /// launching would be this command creating the pileup it exists to clear.
    /// </summary>
    public const int DefaultMinimumAgeMinutes = 5;

    /// <param name="processes">The snapshot, from the gateway's two-source enumeration.</param>
    /// <param name="explicitPids">
    /// Processes named directly. <b>The only route to closing an in-use Portal</b>, and the only route
    /// past the minimum-age guard.
    /// </param>
    /// <param name="now">Injected so the age guard is testable without waiting.</param>
    /// <param name="minimumAgeMinutes">See <see cref="DefaultMinimumAgeMinutes"/>.</param>
    public static IReadOnlyList<PortalClosePlan> Plan(
        IReadOnlyList<PortalProcessInfo> processes,
        IReadOnlyCollection<int> explicitPids,
        DateTime now,
        int minimumAgeMinutes = DefaultMinimumAgeMinutes)
    {
        if (processes == null) throw new ArgumentNullException(nameof(processes));
        if (explicitPids == null) throw new ArgumentNullException(nameof(explicitPids));

        var classified = PortalStatusClassifier.Classify(processes);

        return classified.Processes
            .Select(c => PlanOne(c.Process, c.Class, explicitPids.Contains(c.Process.Pid), now, minimumAgeMinutes))
            .ToList();
    }

    private static PortalClosePlan PlanOne(
        PortalProcessInfo process, PortalProcessClass klass, bool named, DateTime now, int minimumAgeMinutes)
    {
        PortalClosePlan Plan(PortalCloseMethod method, string reason) =>
            new PortalClosePlan(process, klass, method, reason);

        switch (klass)
        {
            case PortalProcessClass.InUse:
                // 🔴 NAMED OR NOT AT ALL. A sweep must never take a session somebody is working in.
                return named
                    ? Plan(PortalCloseMethod.SaveThenTerminate,
                        "named explicitly by --pid, and it has a project open: it will be SAVED and then closed. " +
                        "A sweep would never have selected it.")
                    : Plan(PortalCloseMethod.Leave,
                        "it has a project open, so it is IN USE - possibly a person's session or another agent's. " +
                        "Name it with --pid if you really mean it; no sweep will ever select it.");

            case PortalProcessClass.SelfLaunchedOrphan:
                return Plan(PortalCloseMethod.TerminateEmpty,
                    "an empty instance this tool launched and did not clean up. Nothing is open, so there is " +
                    "nothing to save.");

            case PortalProcessClass.StrayEmpty:
                return Plan(PortalCloseMethod.TerminateEmpty,
                    "an empty instance this tool did not launch - a human's window, a first-connect-dialog wait, " +
                    "or stale pileup. Nothing is open, so there is nothing to save; but note it may be somebody's " +
                    "window and closing it will be noticed.");

            case PortalProcessClass.OpennessInvisible:
                // 🔴 The whole reason this command exists, and the branch where "save first" is impossible:
                // Openness cannot see the process, so there is nothing to attach to and nothing to ask.
                if (named)
                {
                    return Plan(PortalCloseMethod.TerminateUnsaveable,
                        "named explicitly by --pid. Openness cannot see it, so it CANNOT BE SAVED - it will be " +
                        "terminated. Anything unsaved in it is lost.");
                }

                var age = Age(process, now);
                if (age == null)
                {
                    return Plan(PortalCloseMethod.Leave,
                        "Openness cannot see it AND its start time could not be read, so its age is unknown and the " +
                        "\"still starting\" case cannot be ruled out. Name it with --pid to terminate it anyway.");
                }

                if (age.Value.TotalMinutes < minimumAgeMinutes)
                {
                    return Plan(PortalCloseMethod.Leave,
                        string.Format(
                            "Openness cannot see it, but it is only {0:0.#} minute(s) old and STILL STARTING is a " +
                            "documented cause of invisibility. Left alone below the {1}-minute floor; name it with " +
                            "--pid to override.",
                            age.Value.TotalMinutes, minimumAgeMinutes));
                }

                return Plan(PortalCloseMethod.TerminateUnsaveable,
                    string.Format(
                        "Openness cannot see it and it is {0:0} minute(s) old, so it is not merely starting. It " +
                        "CANNOT BE SAVED - there is nothing to attach to - so it will be terminated.",
                        age.Value.TotalMinutes));

            default:
                return Plan(PortalCloseMethod.Leave, "unclassified, so left alone.");
        }
    }

    private static TimeSpan? Age(PortalProcessInfo process, DateTime now) =>
        process.StartedAt.HasValue && process.StartedAt.Value <= now
            ? now - process.StartedAt.Value
            : (TimeSpan?)null;

    /// <summary>
    /// The denominator line, printed on <b>every</b> run including the one that closes nothing. "3 closed"
    /// cannot be told from "3 of 9 closed", and only the second says what was looked at.
    /// </summary>
    public static string Summarise(IReadOnlyList<PortalClosePlan> plans)
    {
        if (plans == null) throw new ArgumentNullException(nameof(plans));

        if (plans.Count == 0)
        {
            return "NOTHING EXAMINED - no TIA Portal processes are running.";
        }

        var targets = plans.Count(p => p.Method != PortalCloseMethod.Leave);
        var unsaveable = plans.Count(p => p.Method == PortalCloseMethod.TerminateUnsaveable);
        var saved = plans.Count(p => p.Method == PortalCloseMethod.SaveThenTerminate);

        return string.Format("{0} of {1} Portal process(es) selected", targets, plans.Count) +
               (saved > 0 ? string.Format("; {0} will be SAVED first", saved) : string.Empty) +
               (unsaveable > 0
                   ? string.Format("; {0} CANNOT be saved (Openness cannot see them) and will be TERMINATED", unsaveable)
                   : string.Empty) +
               ".";
    }
}
