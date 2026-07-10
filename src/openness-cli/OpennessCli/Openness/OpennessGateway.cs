using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OpennessCli.Model;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using ModelBlockType = OpennessCli.Model.BlockType;

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

        return new BlockInfo(
            Name: block.Name,
            Type: ClassifyBlockType(block),
            Number: block.Number,
            Language: language,
            IsSafety: isSafety,
            Path: groupPath);
    }

    private static ModelBlockType ClassifyBlockType(PlcBlock block) => block switch
    {
        OB => ModelBlockType.OB,
        FB => ModelBlockType.FB,
        FC => ModelBlockType.FC,
        DataBlock => ModelBlockType.DB,
        _ => throw new UnrecognizedBlockTypeException(block.GetType().FullName ?? block.GetType().Name),
    };

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
