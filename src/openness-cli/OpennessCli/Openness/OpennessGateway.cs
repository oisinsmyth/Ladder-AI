using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpennessCli.Model;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using ModelBlockType = OpennessCli.Model.BlockType;
using ModelCompileState = OpennessCli.Model.CompileState;

namespace OpennessCli.Openness;

/// <summary>
/// The only file in this project that references Siemens.Engineering types
/// (docs/05-architecture.md: openness-cli is the sole TIA touchpoint).
///
/// Block-group traversal shape and the ProgrammingLanguage enum members were confirmed
/// by reflecting on the installed V20 Siemens.Engineering.dll (PlcSoftware.BlockGroup ->
/// PlcBlockGroup.Blocks/.Groups, recursively; DeviceItem.GetService&lt;SoftwareContainer&gt;()
/// -> .Software as PlcSoftware). End-to-end behavior against a live Portal session with a
/// real project is still unverified until run against the reference project.
///
/// Every block read is metadata-only: Name, Number, ProgrammingLanguage. Nothing here ever
/// opens a block (no interface/network access) — that holds for safety and non-safety
/// blocks alike, because `list` only lists.
/// </summary>
public sealed class OpennessGateway : IOpennessGateway
{
    private TiaPortal? _tiaPortal;
    private Project? _project;

    public void Connect(TimeSpan timeout)
    {
        RunWithTimeout(
            () =>
            {
                var processes = TiaPortal.GetProcesses();
                _tiaPortal = processes.Count > 0
                    ? processes[0].Attach()
                    : new TiaPortal(TiaPortalMode.WithUserInterface);
            },
            timeout,
            () => new ConnectTimeoutException(timeout));
    }

    public void OpenProject(string projectIdentifier, TimeSpan timeout)
    {
        if (_tiaPortal is null)
        {
            throw new InvalidOperationException($"{nameof(Connect)} must be called before {nameof(OpenProject)}.");
        }

        RunWithTimeout(
            () =>
            {
                // The identifier can be a project name already open in the attached Portal
                // instance (common: engineer already has Portal + project open) or a path to
                // a .apNN file to open fresh. Never re-open something already open.
                _project = FindAlreadyOpenProject(_tiaPortal.Projects, projectIdentifier)
                    ?? _tiaPortal.Projects.Open(new FileInfo(projectIdentifier));
            },
            timeout,
            () => new ProjectOpenTimeoutException(timeout));
    }

    private static Project? FindAlreadyOpenProject(ProjectComposition projects, string identifier)
    {
        foreach (Project project in projects)
        {
            if (string.Equals(project.Name, identifier, StringComparison.OrdinalIgnoreCase))
            {
                return project;
            }

            if (project.Path is not null && string.Equals(project.Path.FullName, identifier, StringComparison.OrdinalIgnoreCase))
            {
                return project;
            }
        }

        return null;
    }

    public IReadOnlyList<BlockInfo> EnumerateBlocks()
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(EnumerateBlocks)}.");
        }

        var results = new List<BlockInfo>();

        foreach (Device device in _project.Devices)
        {
            foreach (DeviceItem item in device.DeviceItems)
            {
                WalkDeviceItem(item, device.Name, results);
            }
        }

        return results;
    }

    private static void WalkDeviceItem(DeviceItem item, string parentPath, List<BlockInfo> results)
    {
        var path = $"{parentPath}/{item.Name}";

        var softwareContainer = item.GetService<SoftwareContainer>();
        if (softwareContainer?.Software is PlcSoftware plcSoftware)
        {
            WalkBlockGroup(plcSoftware.BlockGroup, path, results);
        }

        foreach (DeviceItem child in item.DeviceItems)
        {
            WalkDeviceItem(child, path, results);
        }
    }

    private static void WalkBlockGroup(PlcBlockGroup group, string groupPath, List<BlockInfo> results)
    {
        foreach (PlcBlock block in group.Blocks)
        {
            results.Add(ToBlockInfo(block, groupPath));
        }

        foreach (PlcBlockUserGroup subGroup in group.Groups)
        {
            WalkBlockGroup(subGroup, $"{groupPath}/{subGroup.Name}", results);
        }
    }

    private static BlockInfo ToBlockInfo(PlcBlock block, string groupPath)
    {
        var language = block.ProgrammingLanguage.ToString();
        var isSafety = SafetyClassifier.IsSafety(language);

        // IsConsistent is skipped for safety blocks — hard rule 2 says never read a safety
        // block's properties beyond identification, and consistency state isn't needed for
        // anything on the safety path (which stops at flagging, full stop).
        var isConsistent = isSafety || block.IsConsistent;

        return new BlockInfo(
            Name: block.Name,
            Type: ClassifyBlockType(block),
            Number: block.Number,
            Language: language,
            IsSafety: isSafety,
            Path: groupPath,
            IsConsistent: isConsistent);
    }

    private static ModelBlockType ClassifyBlockType(PlcBlock block) => block switch
    {
        OB => ModelBlockType.OB,
        FB => ModelBlockType.FB,
        FC => ModelBlockType.FC,
        DataBlock => ModelBlockType.DB,
        _ => throw new UnrecognizedBlockTypeException(block.GetType().FullName ?? block.GetType().Name),
    };

    public void ExportBlock(string blockName, string? deviceFilter, string outPath)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(ExportBlock)}.");
        }

        var matches = FindMatchingBlocks(_project, blockName).ToList();
        if (deviceFilter is not null)
        {
            matches = matches.Where(m => m.Path.IndexOf(deviceFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        if (matches.Count == 0)
        {
            throw new BlockNotFoundException(blockName);
        }

        if (matches.Count > 1)
        {
            throw new AmbiguousBlockException(blockName, matches.Select(m => m.Path));
        }

        var block = matches[0].Block;
        var language = block.ProgrammingLanguage.ToString();
        if (SafetyClassifier.IsSafety(language))
        {
            throw new SafetyContentRefusedException(blockName, language);
        }

        if (File.Exists(outPath))
        {
            File.Delete(outPath);
        }

        block.Export(new FileInfo(outPath), Siemens.Engineering.ExportOptions.WithDefaults);

        if (!File.Exists(outPath))
        {
            // Spike-discovered quirk (docs/notes/openness-quirks.md): Export() can return
            // without producing a file, no exception thrown. One retry before giving up.
            block.Export(new FileInfo(outPath), Siemens.Engineering.ExportOptions.WithDefaults);
            if (!File.Exists(outPath))
            {
                throw new ExportProducedNoFileException(outPath);
            }
        }
    }

    public IReadOnlyList<BlockInfo> ImportBlocks(string groupPath, IReadOnlyList<string> files)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(ImportBlocks)}.");
        }

        var group = FindGroup(_project, groupPath);
        var imported = new List<BlockInfo>();

        foreach (var file in files)
        {
            var results = group.Blocks.Import(new FileInfo(file), Siemens.Engineering.ImportOptions.Override);
            foreach (var item in results)
            {
                if (item is not PlcBlock block)
                {
                    throw new InvalidOperationException(
                        $"Import() of '{file}' returned an unexpected object type: {item?.GetType().FullName ?? "null"}.");
                }

                var language = block.ProgrammingLanguage.ToString();
                if (SafetyClassifier.IsSafety(language))
                {
                    // Defense in depth: nothing upstream of this pipeline should ever produce
                    // safety-language IR, but verify rather than assume (belt-and-braces).
                    throw new SafetyContentRefusedException(block.Name, language);
                }

                imported.Add(ToBlockInfo(block, groupPath));
            }
        }

        return imported;
    }

    public CompileResult Compile(string? deviceFilter)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(Compile)}.");
        }

        var candidates = FindPlcDeviceItems(_project).ToList();
        if (deviceFilter is not null)
        {
            candidates = candidates.Where(c => c.Path.IndexOf(deviceFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        if (candidates.Count != 1)
        {
            throw new DeviceNotFoundException(deviceFilter);
        }

        return CompileDeviceItem(candidates[0].Item, candidates[0].Path);
    }

    private static CompileResult CompileDeviceItem(DeviceItem deviceItem, string path)
    {
        var compilable = deviceItem.GetService<ICompilable>();
        if (compilable is null)
        {
            var softwareContainer = deviceItem.GetService<SoftwareContainer>();
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                compilable = plcSoftware.GetService<ICompilable>();
            }
        }

        if (compilable is null)
        {
            throw new InvalidOperationException($"Could not obtain a compilable service for device '{path}'.");
        }

        return RunCompile(compilable);
    }

    public CompileResult CompileBlock(string blockName, string? deviceFilter)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(CompileBlock)}.");
        }

        var matches = FindMatchingBlocks(_project, blockName).ToList();
        if (deviceFilter is not null)
        {
            matches = matches.Where(m => m.Path.IndexOf(deviceFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        if (matches.Count == 0)
        {
            throw new BlockNotFoundException(blockName);
        }

        if (matches.Count > 1)
        {
            throw new AmbiguousBlockException(blockName, matches.Select(m => m.Path));
        }

        var block = matches[0].Block;
        var language = block.ProgrammingLanguage.ToString();
        if (SafetyClassifier.IsSafety(language))
        {
            throw new SafetyContentRefusedException(blockName, language);
        }

        // PlcBlock implements IEngineeringServiceProvider like DeviceItem/PlcSoftware do, and
        // this GetService<ICompilable>() call is confirmed live, 2026-07-11, to actually return
        // a working per-block compiler — not documented anywhere, found by reflecting on the
        // installed DLL then testing live rather than assuming device-level compile was the only
        // granularity available (see docs/notes/openness-quirks.md).
        var compilable = block.GetService<ICompilable>()
            ?? throw new InvalidOperationException(
                $"Block '{blockName}' does not expose an ICompilable service — expected one to be available (confirmed live on other blocks, see docs/notes/openness-quirks.md).");

        return RunCompile(compilable);
    }

    private static CompileResult RunCompile(ICompilable compilable)
    {
        var result = compilable.Compile();
        var messages = new List<CompileMessage>();
        foreach (CompilerResultMessage message in result.Messages)
        {
            messages.Add(new CompileMessage(MapCompileState(message.State), message.Description, message.Path));
        }

        return new CompileResult(MapCompileState(result.State), result.ErrorCount, result.WarningCount, messages);
    }

    public SanityCheckResult RunSanityCheck()
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(RunSanityCheck)}.");
        }

        var blocks = EnumerateBlocks();
        var inconsistentBlocks = blocks
            .Where(b => !b.IsConsistent)
            .Select(b => new BlockConsistencyIssue(b.Name, b.Path, b.Language))
            .ToList();

        var deviceCompiles = new List<DeviceCompileSummary>();
        foreach (var (item, path) in FindPlcDeviceItems(_project))
        {
            deviceCompiles.Add(new DeviceCompileSummary(path, CompileDeviceItem(item, path)));
        }

        return new SanityCheckResult(blocks.Count, inconsistentBlocks, deviceCompiles);
    }

    private static ModelCompileState MapCompileState(CompilerResultState state) => state switch
    {
        CompilerResultState.Success => ModelCompileState.Success,
        CompilerResultState.Information => ModelCompileState.Information,
        CompilerResultState.Warning => ModelCompileState.Warning,
        CompilerResultState.Error => ModelCompileState.Error,
        _ => throw new InvalidOperationException($"Unrecognized CompilerResultState: {state}."),
    };

    private static IEnumerable<(PlcBlock Block, string Path)> FindMatchingBlocks(Project project, string blockName)
    {
        foreach (Device device in project.Devices)
        {
            foreach (DeviceItem item in device.DeviceItems)
            {
                foreach (var match in FindBlocksInDeviceItem(item, device.Name, blockName))
                {
                    yield return match;
                }
            }
        }
    }

    private static IEnumerable<(PlcBlock Block, string Path)> FindBlocksInDeviceItem(DeviceItem item, string parentPath, string blockName)
    {
        var path = $"{parentPath}/{item.Name}";

        var softwareContainer = item.GetService<SoftwareContainer>();
        if (softwareContainer?.Software is PlcSoftware plcSoftware)
        {
            foreach (var match in FindBlocksInGroup(plcSoftware.BlockGroup, path, blockName))
            {
                yield return match;
            }
        }

        foreach (DeviceItem child in item.DeviceItems)
        {
            foreach (var match in FindBlocksInDeviceItem(child, path, blockName))
            {
                yield return match;
            }
        }
    }

    private static IEnumerable<(PlcBlock Block, string Path)> FindBlocksInGroup(PlcBlockGroup group, string groupPath, string blockName)
    {
        foreach (PlcBlock block in group.Blocks)
        {
            if (block.Name == blockName)
            {
                yield return (block, groupPath);
            }
        }

        foreach (PlcBlockUserGroup subGroup in group.Groups)
        {
            foreach (var match in FindBlocksInGroup(subGroup, $"{groupPath}/{subGroup.Name}", blockName))
            {
                yield return match;
            }
        }
    }

    private static IEnumerable<(DeviceItem Item, string Path)> FindPlcDeviceItems(Project project)
    {
        foreach (Device device in project.Devices)
        {
            foreach (DeviceItem item in device.DeviceItems)
            {
                foreach (var match in FindPlcDeviceItemsRecursive(item, device.Name))
                {
                    yield return match;
                }
            }
        }
    }

    private static IEnumerable<(DeviceItem Item, string Path)> FindPlcDeviceItemsRecursive(DeviceItem item, string parentPath)
    {
        var path = $"{parentPath}/{item.Name}";

        var softwareContainer = item.GetService<SoftwareContainer>();
        if (softwareContainer?.Software is PlcSoftware)
        {
            yield return (item, path);
        }

        foreach (DeviceItem child in item.DeviceItems)
        {
            foreach (var match in FindPlcDeviceItemsRecursive(child, path))
            {
                yield return match;
            }
        }
    }

    /// <summary>
    /// Resolves a "list"-style Path (e.g. "S7-1200 G2 station_2/JOB9002_PLC/Control") to the
    /// PlcBlockGroup it names: greedily descends matching DeviceItem names first, then treats
    /// the remaining segments as nested PlcBlockGroup names once a PLC software container is
    /// found. There's no fixed boundary between the device-item part and the group-name part
    /// in the path string, so this is the only reliable way to resolve it generically.
    /// </summary>
    private static PlcBlockGroup FindGroup(Project project, string groupPath)
    {
        var segments = groupPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new InvalidOperationException("Empty --group path.");
        }

        var device = project.Devices.Cast<Device>().FirstOrDefault(d => d.Name == segments[0])
            ?? throw new DeviceNotFoundException(segments[0]);

        var i = 1;
        DeviceItem? currentItem = null;
        IEnumerable<DeviceItem> currentLevel = device.DeviceItems.Cast<DeviceItem>();

        while (i < segments.Length)
        {
            var next = currentLevel.FirstOrDefault(it => it.Name == segments[i]);
            if (next is null)
            {
                break;
            }

            currentItem = next;
            currentLevel = next.DeviceItems.Cast<DeviceItem>();
            i++;
        }

        if (currentItem is null)
        {
            throw new InvalidOperationException($"No device item found under '{groupPath}'.");
        }

        var softwareContainer = currentItem.GetService<SoftwareContainer>();
        if (softwareContainer?.Software is not PlcSoftware plcSoftware)
        {
            throw new InvalidOperationException($"'{string.Join("/", segments, 0, i)}' is not a PLC software container.");
        }

        PlcBlockGroup group = plcSoftware.BlockGroup;
        for (; i < segments.Length; i++)
        {
            group = group.Groups.Cast<PlcBlockUserGroup>().FirstOrDefault(g => g.Name == segments[i])
                ?? throw new InvalidOperationException($"Block group '{segments[i]}' not found under '{string.Join("/", segments, 0, i)}'.");
        }

        return group;
    }

    private static void RunWithTimeout(Action action, TimeSpan timeout, Func<Exception> timeoutException)
    {
        var task = Task.Run(action);
        if (!task.Wait(timeout))
        {
            throw timeoutException();
        }

        if (task.IsFaulted)
        {
            throw task.Exception!.GetBaseException();
        }
    }

    public void Dispose()
    {
        _tiaPortal?.Dispose();
    }
}

public sealed class UnrecognizedBlockTypeException : Exception
{
    public UnrecognizedBlockTypeException(string typeName)
        : base(
            $"Unrecognized PLC block CLR type '{typeName}'. Expected OB/FB/FC or a DataBlock subtype " +
            "(GlobalDB/InstanceDB/ArrayDB). Refusing to classify silently — see design philosophy #10.")
    {
    }
}
