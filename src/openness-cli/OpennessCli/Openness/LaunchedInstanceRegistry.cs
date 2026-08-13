using System.Diagnostics;
using System.Text.Json;

namespace OpennessCli.Openness;

/// <summary>
/// Persists the OS process IDs of Portal instances this tool has itself launched via
/// <c>new TiaPortal(...)</c>, across separate <c>openness-cli</c> invocations — each invocation is
/// its own short-lived process with no memory of earlier ones, so recognizing "did I launch this"
/// requires something durable. Backed by <see cref="TiaPortalProcess.Id"/> (the real OS process
/// ID — confirmed real, 2026-07-14, missed entirely by this project's original 2026-07-10 API
/// survey, which recorded only <c>Attach()</c>/<c>Dispose()</c> on that type).
///
/// Exists specifically to close a gap the 2026-07-14 concurrent-Portal stability audit found
/// (<c>docs/notes/concurrent-portal-test-plan.md</c> T4.1): a client killed mid-launch leaves
/// behind a Portal process with nothing open, and — because <see cref="OpennessGateway"/> can't
/// tell "my own orphan from an earlier interrupted run" from "a human's own empty window" — it
/// used to leave that orphan untouched forever, only ever launching yet another fresh instance.
/// A PID recorded here is proof this tool created that exact process; nothing else ever gets
/// marked, so a human's own manually-launched window is never at risk of misidentification.
/// </summary>
internal static class LaunchedInstanceRegistry
{
    private static readonly string RegistryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "openness-cli",
        "launched-instances.json");

    /// <summary>
    /// THE SECOND, REPORT-ONLY RECORD. Every PID this tool has ever launched and that is still
    /// alive — and, unlike the reuse registry above, <b>it is never unmarked</b>.
    ///
    /// 🔴 **WHY IT EXISTS (2026-08-13).** `launched-instances.json` was observed as `[]` before and
    /// after repeated launches, and `portal-status` therefore reported a freshly-launched instance
    /// as plain `in-use`. That is not a recording bug — it is the design: <see cref="MarkLaunched"/>
    /// writes on launch and <see cref="Unmark"/> deletes on successful open, so the record is
    /// **erased at exactly the moment it becomes interesting to a reader.** The reuse registry only
    /// ever tracks *empty orphans*, so `MarkedByThisTool` is structurally incapable of being true for
    /// a process with a project open — and a relaunch after a Portal crash was invisible, because
    /// nothing durable said "we launched this one".
    ///
    /// **Kept strictly separate from the reuse guard, on purpose.** The obvious fix — stop unmarking
    /// — would make <see cref="IsMarkedAsLaunchedByThisTool"/> return true for processes it currently
    /// says nothing about, i.e. it would edit a safety guard to satisfy a reporting need. This record
    /// is written by the same call, read by nothing but `portal-status`, and **consulted by no
    /// decision**. Nothing here can make the reuse guard fail open, because the guard never reads it.
    /// </summary>
    private static readonly string LaunchLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "openness-cli",
        "launched-history.json");

    // Called immediately after `new TiaPortal(...)` returns — before anything else can go wrong
    // (a slow/refused Projects.Open(), a client kill) — so the mark is durable even if this
    // process never gets to call Unmark itself.
    public static void MarkLaunched(int processId)
    {
        var pids = Load();
        pids.Add(processId);
        Save(pids);

        // The report-only half. Deliberately a second write rather than a flag on the first: the
        // reuse registry's contents are load-bearing for a safety decision and this is not.
        var history = LoadHistory();
        history.Add(processId);
        SaveHistory(history);
    }

    /// <summary>
    /// Whether this tool launched this exact process, <b>regardless of whether a project was later
    /// opened into it</b>. Report-only: no decision in this codebase branches on it, and the reuse
    /// guard (<see cref="IsMarkedAsLaunchedByThisTool"/>) deliberately does not consult it.
    /// </summary>
    public static bool WasLaunchedByThisTool(int processId) => LoadHistory().Contains(processId);

    private static HashSet<int> LoadHistory() => LoadPruned(LaunchLogPath);

    private static void SaveHistory(HashSet<int> pids) => SaveTo(LaunchLogPath, pids);

    // Called once this same invocation successfully opens its own target into the process it just
    // launched — the process is no longer empty, so there's nothing left to reuse/orphan-detect
    // about it; keeping it in the registry would just be stale bookkeeping.
    public static void Unmark(int processId)
    {
        var pids = Load();
        if (pids.Remove(processId))
        {
            Save(pids);
        }
    }

    // True only if this exact PID was previously marked by MarkLaunched and never unmarked — the
    // one condition under which OpenProject() is willing to treat a discovered empty process as
    // fair game to reuse. Never guesses; a PID absent from this registry is always left alone.
    public static bool IsMarkedAsLaunchedByThisTool(int processId) => Load().Contains(processId);

    // Loads the registry, pruning any PID whose OS process no longer exists at all (fully exited
    // since it was marked — e.g. a human closed it, or it crashed) — keeps the file from growing
    // unbounded with references to long-gone processes. Best-effort throughout: a missing,
    // corrupt, or unreadable registry file is treated as empty rather than a hard failure — this
    // mechanism is a durability nicety for cleaning up orphans, not something any real operation
    // should ever fail over.
    private static HashSet<int> Load() => LoadPruned(RegistryPath);

    private static HashSet<int> LoadPruned(string path)
    {
        List<int>? stored;
        try
        {
            if (!File.Exists(path))
            {
                return new HashSet<int>();
            }

            stored = JsonSerializer.Deserialize<List<int>>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new HashSet<int>();
        }

        var alive = new HashSet<int>();
        foreach (var pid in stored ?? new List<int>())
        {
            try
            {
                Process.GetProcessById(pid).Dispose();
                alive.Add(pid);
            }
            catch (ArgumentException)
            {
                // Process no longer exists — pruned, not carried forward.
            }
        }

        return alive;
    }

    private static void Save(HashSet<int> pids) => SaveTo(RegistryPath, pids);

    private static void SaveTo(string path, HashSet<int> pids)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(pids));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort — see Load()'s own doc comment. Losing this write just means the next
            // invocation won't recognize this particular orphan; it falls back to today's safe
            // (if wasteful) behavior of launching yet another fresh instance instead.
        }
    }
}
