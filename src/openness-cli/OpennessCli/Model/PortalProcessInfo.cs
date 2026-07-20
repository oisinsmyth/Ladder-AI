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
public sealed record PortalProcessInfo(
    int Pid,
    string? ProjectPath,
    DateTime Acquired,
    bool HasUserInterface,
    bool MarkedByThisTool);

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
}
