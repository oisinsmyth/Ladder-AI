using System;
using System.Collections.Generic;
using System.Linq;

namespace OpennessCli.Model;

/// <summary>
/// A plain, Siemens-free snapshot of one running <c>Siemens.Automation.Portal.exe</c> process, as
/// read from <c>TiaPortal.GetProcesses()</c> WITHOUT ever attaching to or launching anything
/// (docs/notes/openness-api-surface-v20.md: <c>Id</c>/<c>ProjectPath</c>/<c>Mode</c>/
/// <c>AcquisitionTime</c> are all readable off <c>TiaPortalProcess</c> directly). Deliberately a
/// POCO with no Siemens types so the classifier over it is unit-testable — the COM-touching
/// enumeration that produces it lives in <see cref="OpennessCli.Openness.OpennessGateway"/>.
/// </summary>
/// <param name="Pid">The real OS process ID — also the key into <c>LaunchedInstanceRegistry</c>.</param>
/// <param name="ProjectPath">The project open in this process, or <c>null</c> when the process is empty.</param>
/// <param name="Acquired">The process's <c>AcquisitionTime</c> — an age signal for pileup vs. fresh.</param>
/// <param name="HasUserInterface">Whether the process runs <c>WithUserInterface</c> (a human window) or headless.</param>
/// <param name="MarkedByThisTool">
/// True only if <c>LaunchedInstanceRegistry</c> still marks this PID as one this tool launched and
/// never finished opening a project into — i.e. an empty self-launch orphan, not just "anything we
/// ever launched" (once a project opens into it, it is Unmarked).
/// </param>
/// <param name="StartedAt">
/// The OS process start time (<c>Process.StartTime</c>), or null when the process could not be read
/// — e.g. it is gone, or it belongs to another user. **This, not <paramref name="Acquired"/>, is the
/// age signal.**
/// </param>
/// <param name="LaunchedByThisTool">
/// Whether this tool launched this process, from the report-only launch history —
/// <b>regardless of whether a project was later opened into it</b>. Distinct from
/// <paramref name="MarkedByThisTool"/>, which is erased as soon as a project opens and therefore can
/// never be true for a process in use. Report-only: no decision branches on it.
/// </param>
/// <param name="OpennessVisible">
/// False for a process the OPERATING SYSTEM shows and <c>TiaPortal.GetProcesses()</c> does not.
/// Measured 2026-08-13: `portal-status` reported `PROCESSES: 1` while the OS showed two — the
/// read-only diagnostic this project reaches for when Portal misbehaves **disagreed with the
/// operating system, silently**, because it only ever enumerated what Openness would admit to.
/// </param>
/// <param name="AttachedSessionCount">
/// How many Openness sessions are attached to this process right now, or <c>null</c> when the
/// question was NOT ANSWERED — an Openness-invisible process, or a getter that threw.
///
/// <para><b>Measured 2026-08-23, not assumed.</b> <c>TiaPortalProcess.AttachedSessions</c> sat marked
/// <i>"unexplored"</i> in <c>docs/notes/openness-api-surface-v20.md</c> for weeks while
/// <c>portal-close</c> swept empty Portals blind. Probed against a live V20: an empty Portal nobody
/// was attached to returned <b>0 items</b>; while a second process held an <c>Attach()</c>ed handle
/// to it, a <b>DIFFERENT</b> process reading the same property returned <b>1 item</b> naming the
/// holder's own pid and exe path; after <c>Dispose()</c>, 0 again. Hard-killing the holder also
/// returned it to 0 — <b>a dead holder leaves no stale session</b>, so a guard built on this cannot
/// be jammed permanently by a crash.</para>
///
/// <para>The element is <c>Siemens.Engineering.TiaPortalSession</c>, and the fields that carry the
/// answer are <c>ProcessId</c> and <c>ProcessPath</c> — the ATTACHING CLIENT's, not the Portal's.
/// <c>IsActive</c> read <c>False</c> throughout a live attach, so it means something other than
/// "somebody is attached" and nothing here reads it.</para>
///
/// <para>🔴 <b><c>null</c> IS NOT ZERO.</b> Zero is a measurement — nobody is attached. Null is the
/// absence of one, and this project has already shipped one defect from reading a modelled absence
/// as a measurement (see <see cref="AcquiredPrecedesStart"/>). Nothing may treat them alike.</para>
/// </param>
/// <param name="AttachedSessionHolders">
/// Who holds those sessions — a pre-formatted <c>"pid 19536 openness-cli.exe"</c> list, or
/// <c>null</c> when there are none or it could not be read. A string rather than a collection so this
/// record keeps value equality. It exists so that a refusal names a pid the reader can go and look
/// at, rather than one they can only override blindly.
/// </param>
public sealed record PortalProcessInfo(
    int Pid,
    string? ProjectPath,
    DateTime Acquired,
    bool HasUserInterface,
    bool MarkedByThisTool,
    DateTime? StartedAt = null,
    bool LaunchedByThisTool = false,
    bool OpennessVisible = true,
    int? AttachedSessionCount = null,
    string? AttachedSessionHolders = null)
{
    /// <summary>
    /// True only when an attached session was POSITIVELY MEASURED. An unread count (<c>null</c>) is
    /// false here on purpose: answering "attached" for every process this tool cannot interrogate
    /// would make them all permanently unsweepable, which is the pileup <c>portal-close</c> exists to
    /// clear. The blindness stays where it was rather than being converted into a guard.
    /// </summary>
    public bool HasAttachedSession => AttachedSessionCount > 0;

    /// <summary>
    /// *** A PROCESS CANNOT BE ACQUIRED BEFORE IT EXISTS. ***
    ///
    /// Measured 2026-08-13: `portal-status` dated PID 16972 `ACQUIRED 14:47:51` when that process
    /// started at `15:38:09` — fifty minutes earlier than its own existence. Whatever
    /// <c>TiaPortalProcess.AcquisitionTime</c> is, it is demonstrably NOT this process's age, and it
    /// had been presented as one ("an age signal for pileup vs. fresh").
    ///
    /// <b>Why this is worse than a missing value:</b> it is plausible. A wrong timestamp that looks
    /// right invites exactly the reasoning-from-sequence this project has spent the day trying not to
    /// do. So the impossibility is COMPUTED and shown, rather than the value being quietly trusted or
    /// quietly dropped.
    ///
    /// What <c>AcquisitionTime</c> actually means is NOT established here and is not guessed at —
    /// it is reported as the raw API value it is, and flagged when it contradicts the OS.
    ///
    /// 🔴 <b>AND IT FIRED ON EVERY OPENNESS-INVISIBLE PROCESS, WHICH REPORT NO ACQUIRED TIME AT ALL
    /// (fixed 2026-08-14).</b> Those are built with <c>Acquired: default</c> — deliberately, and the
    /// gateway says so at the site: <i>"Everything the Openness API would have told us is unknown by
    /// construction here, and is left at its default rather than filled with a plausible-looking
    /// value."</i> That was exactly right. <b>This comparison then read the default as a
    /// measurement</b>, so <c>0001-01-01 &lt; any real start time</c> was true for every one of them,
    /// and the report announced *"an ACQUIRED time EARLIER THAN THEIR OWN START TIME"* about a
    /// process whose ACQUIRED column, three characters away, read <c>(not visible to Openness)</c>.
    ///
    /// <para><b>The honest representation was re-consumed as data one layer down</b> — the absence
    /// was correctly modelled and then incorrectly read. It is not a date-blind comparison; the
    /// comparison is exact. It was asked about a value that does not exist.</para>
    ///
    /// <para><i>A false alarm in a diagnostic is the same class of error as a false green:</i> it
    /// teaches its reader to discount the one time it is right. It fired on the ONLY two runs of this
    /// command tonight, both times about a process it had nothing to say about.</para>
    ///
    /// <para>The guard is on <see cref="OpennessVisible"/> — the property that MEANS "an acquired
    /// time was actually read" — plus the value's own absence, so a visible process that somehow
    /// reports <see cref="DateTime.MinValue"/> is not flagged either. Neither alone is enough: the
    /// flag answers only for processes that reported something.</para>
    /// </summary>
    public bool AcquiredPrecedesStart =>
        OpennessVisible &&
        Acquired != default &&
        StartedAt is DateTime started &&
        Acquired < started;
}

/// <summary>
/// How a running Portal process relates to this tool. Purely a read-only classification — nothing
/// here kills or reuses anything (killing stays FI-07/Parked).
/// </summary>
public enum PortalProcessClass
{
    /// <summary>Has a project open — in active use, never a cleanup candidate.</summary>
    InUse,

    /// <summary>Empty and marked in the registry — an orphan this tool launched and will reuse/replace itself; self-healing, safe to leave.</summary>
    SelfLaunchedOrphan,

    /// <summary>Empty and NOT marked — a human's own window, a first-connect-dialog wait, or stale pileup. Never this tool's to close automatically.</summary>
    StrayEmpty,

    /// <summary>
    /// *** THE OS SHOWS IT AND OPENNESS DOES NOT. *** Nothing can be said about its project, its
    /// mode or its age from the Openness API, because the API does not admit it exists. Its own
    /// class because the alternative — omitting it, which is what happened until 2026-08-13 — makes
    /// this command disagree with the operating system without saying so.
    /// </summary>
    OpennessInvisible,
}

public sealed record ClassifiedPortalProcess(PortalProcessInfo Process, PortalProcessClass Class);

/// <summary>
/// The classified enumeration plus a short human note inferring likely cause (pileup vs. a
/// first-connect-dialog wait) from the counts. Read-only diagnostic — carries no exit-gate verdict.
/// </summary>
public sealed record PortalStatusReport(
    IReadOnlyList<ClassifiedPortalProcess> Processes,
    string Note)
{
    public int Total => Processes.Count;

    public int InUseCount => Processes.Count(p => p.Class == PortalProcessClass.InUse);

    public int SelfLaunchedOrphanCount => Processes.Count(p => p.Class == PortalProcessClass.SelfLaunchedOrphan);

    public int StrayEmptyCount => Processes.Count(p => p.Class == PortalProcessClass.StrayEmpty);

    /// <summary>Processes the OS shows and Openness does not — see <see cref="PortalProcessClass.OpennessInvisible"/>.</summary>
    public int OpennessInvisibleCount => Processes.Count(p => p.Class == PortalProcessClass.OpennessInvisible);
}
