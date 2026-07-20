using System.Collections.Generic;
using System.Linq;
using OpennessCli.Model;

namespace OpennessCli.Openness;

/// <summary>
/// Pure classification of a Portal-process snapshot against the self-launch registry — no Siemens
/// types, no COM, so fully unit-testable with hand-built lists (unlike the enumeration that feeds
/// it). Backs `openness-cli portal-status`, the read-only, safe half of the parked FI-07 janitor:
/// it diagnoses stale-process pileup precisely instead of leaving it to `tasklist` + human
/// judgment, and never kills, attaches to, or launches anything.
/// </summary>
public static class PortalStatusClassifier
{
    public static PortalStatusReport Classify(IReadOnlyList<PortalProcessInfo> processes)
    {
        var classified = processes
            .Select(p => new ClassifiedPortalProcess(p, ClassifyOne(p)))
            .ToList();

        return new PortalStatusReport(classified, BuildNote(classified));
    }

    // A process with a project open is in use, full stop. An empty one is fair-game-to-reuse only
    // if the registry positively marks it as this tool's own orphan; anything else empty is a stray
    // — a human's window, a first-connect-dialog wait, or stale pileup — never this tool's to touch.
    private static PortalProcessClass ClassifyOne(PortalProcessInfo p)
    {
        if (!string.IsNullOrEmpty(p.ProjectPath))
        {
            return PortalProcessClass.InUse;
        }

        return p.MarkedByThisTool
            ? PortalProcessClass.SelfLaunchedOrphan
            : PortalProcessClass.StrayEmpty;
    }

    // Infers the likely cause from counts, so the human reading it knows whether there's anything to
    // do. Orphans are self-healing (say so, don't alarm); a single stray is usually benign (a
    // first-connect-dialog wait or a human window); several strays match the documented pileup
    // symptom (the correlate of "second instance won't connect", CLAUDE.md environment notes).
    private static string BuildNote(IReadOnlyList<ClassifiedPortalProcess> processes)
    {
        if (processes.Count == 0)
        {
            return "No TIA Portal processes are running.";
        }

        var orphans = processes.Count(p => p.Class == PortalProcessClass.SelfLaunchedOrphan);
        var strays = processes.Count(p => p.Class == PortalProcessClass.StrayEmpty);

        var parts = new List<string>();

        if (orphans > 0)
        {
            parts.Add(
                $"{orphans} self-launched orphan(s) left by an interrupted run — this tool recognises " +
                "and reuses/replaces them itself, so no action is needed.");
        }

        if (strays == 0)
        {
            if (orphans == 0)
            {
                parts.Add("No empty Portal instances — nothing to clean up.");
            }
        }
        else if (strays == 1)
        {
            parts.Add(
                "1 stray empty Portal instance — often a Portal still waiting on the first-connect " +
                "approval dialog, or a human's own empty window; confirm before closing it.");
        }
        else
        {
            parts.Add(
                $"{strays} stray empty Portal instances — consistent with stale-process pileup (the known " +
                "correlate of \"second instance won't connect\"); closing the idle ones may help.");
        }

        return string.Join(" ", parts);
    }
}
