using System;
using System.Collections.Generic;
using OpennessCli.Model;

namespace OpennessCli.Openness;

public interface IOpennessGateway : IDisposable
{
    void Connect(TimeSpan timeout);

    void OpenProject(string projectIdentifier, TimeSpan timeout);

    IReadOnlyList<BlockInfo> EnumerateBlocks();

    /// <summary>
    /// Exports the named block to <paramref name="outPath"/>. Refuses (throws
    /// <see cref="SafetyContentRefusedException"/>) before calling Export() at all if the block
    /// classifies as safety. <paramref name="deviceFilter"/> disambiguates when the same block
    /// name/number exists under more than one device (common — see docs/notes/stage-gates.md).
    /// </summary>
    void ExportBlock(string blockName, string? deviceFilter, string outPath);

    /// <summary>
    /// Imports <paramref name="files"/> into the block group at <paramref name="groupPath"/>
    /// (format: "&lt;device&gt;/&lt;group&gt;/.../&lt;group&gt;", matching the Path shown by
    /// `list`). No generic "import wherever it goes" exists on the Siemens side, so an explicit
    /// target is required here too.
    /// </summary>
    IReadOnlyList<BlockInfo> ImportBlocks(string groupPath, IReadOnlyList<string> files);

    /// <summary>Compiles the PLC software under <paramref name="deviceFilter"/> (or the project's only PLC device, if unambiguous).</summary>
    CompileResult Compile(string? deviceFilter);

    /// <summary>
    /// Compiles a single named block via its own <c>ICompilable</c> service — distinct from,
    /// and NOT equivalent to, whole-device <see cref="Compile"/>. Confirmed live, 2026-07-10
    /// (docs/notes/openness-quirks.md): a block freshly re-imported via Openness's Import()
    /// gets flagged IsConsistent=false, and device-level Compile() reports Success without ever
    /// clearing that flag — this is what does. <paramref name="deviceFilter"/> disambiguates the
    /// same way as <see cref="ExportBlock"/>. Refuses safety content, same as export/import.
    /// </summary>
    CompileResult CompileBlock(string blockName, string? deviceFilter);

    /// <summary>
    /// Read-only health check: enumerates every block's consistency flag (no export attempt
    /// needed — cheaper and more precise than probing via Export()) and compiles every PLC
    /// device found in the project. Exists to answer "is this project's Openness state OK"
    /// directly, rather than discovering it as a side effect of some other command failing.
    /// </summary>
    SanityCheckResult RunSanityCheck();
}

public sealed class ConnectTimeoutException : Exception
{
    public ConnectTimeoutException(TimeSpan timeout)
        : base(
            $"TIA Portal did not respond to attach/launch within {timeout.TotalMinutes:0} minute(s). " +
            "This is usually the first-connect approval dialog waiting inside TIA Portal — check Portal, " +
            "accept the dialog if it's there, then re-run. Not retrying automatically (single-session rule).")
    {
    }
}

public sealed class ProjectOpenTimeoutException : Exception
{
    public ProjectOpenTimeoutException(TimeSpan timeout)
        : base(
            $"Opening the project did not complete within {timeout.TotalMinutes:0} minute(s). Large TIA " +
            "projects can genuinely take several minutes — this can be normal. Do not kill TIA Portal; " +
            "pass --timeout-open with a larger value if this project is known to be large, then re-run.")
    {
    }
}

/// <summary>Thrown when export/import would touch content that classifies as safety. Structural refusal — Goal 11, never retrofitted.</summary>
public sealed class SafetyContentRefusedException : Exception
{
    public SafetyContentRefusedException(string blockName, string language)
        : base($"Refusing: '{blockName}' classifies as safety content (ProgrammingLanguage={language}). This pipeline never touches safety blocks (CLAUDE.md hard rule 2).")
    {
    }
}

public sealed class BlockNotFoundException : Exception
{
    public BlockNotFoundException(string blockName)
        : base($"No block named '{blockName}' found in the project.")
    {
    }
}

public sealed class AmbiguousBlockException : Exception
{
    public AmbiguousBlockException(string blockName, IEnumerable<string> paths)
        : base($"Block '{blockName}' exists in more than one place: {string.Join(", ", paths)}. Pass --device to disambiguate.")
    {
    }
}

/// <summary>The spike-discovered quirk (docs/notes/openness-quirks.md): Export() can return without producing a file. Thrown after one retry.</summary>
public sealed class ExportProducedNoFileException : Exception
{
    public ExportProducedNoFileException(string outPath)
        : base($"Export() returned but no file appeared at '{outPath}', even after one retry. See docs/notes/openness-quirks.md.")
    {
    }
}

public sealed class DeviceNotFoundException : Exception
{
    public DeviceNotFoundException(string? deviceFilter)
        : base(deviceFilter is null
            ? "No PLC device found in the project (or more than one — pass --device to disambiguate)."
            : $"No PLC device matching '{deviceFilter}' found.")
    {
    }
}
