using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpennessCli.Model;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Screen;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.UI.Base;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.ScreenGroup;
using Siemens.Engineering.HmiUnified.UI.Screens;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;
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

    // Set true only when Connect() itself had to launch a brand new instance because zero Portal
    // processes existed at all — this exact instance is known, for certain, to have been created
    // by this tool, this run, not a human's own manually-launched window, so its own "empty" state
    // can be trusted as fair game without consulting LaunchedInstanceRegistry at all.
    private bool _connectLaunchedFreshInstance;

    // The OS process ID of whatever this invocation itself just launched via `new TiaPortal(...)`
    // (Connect()'s own fresh launch, or OpenProject()'s own fallback launch) — recorded in
    // LaunchedInstanceRegistry the moment it's created, removed once a project is successfully
    // opened into it. Null whenever this invocation attached to something that already existed.
    private int? _pendingLaunchPid;

    public void Connect(TimeSpan timeout, string? preferProjectIdentifier = null)
    {
        RunWithTimeout(
            () =>
            {
                var processes = TiaPortal.GetProcesses();
                if (processes.Count > 0)
                {
                    _tiaPortal = ChooseProcessToAttach(processes, preferProjectIdentifier).Attach();
                    _connectLaunchedFreshInstance = false;
                }
                else
                {
                    var portal = new TiaPortal(TiaPortalMode.WithUserInterface);
                    _tiaPortal = portal;
                    _connectLaunchedFreshInstance = true;

                    // Marked immediately, before anything else can go wrong (a slow/refused
                    // Projects.Open(), this client getting killed) — confirmed real, 2026-07-14
                    // (concurrent-Portal stability audit, docs/notes/concurrent-portal-test-plan.md
                    // T4.1): without this, a client killed mid-launch left a permanently orphaned
                    // process behind that nothing would ever recognize or reuse again. See
                    // LaunchedInstanceRegistry's own doc comment.
                    _pendingLaunchPid = portal.GetCurrentProcess().Id;
                    LaunchedInstanceRegistry.MarkLaunched(_pendingLaunchPid.Value);
                }
            },
            timeout,
            () => new ConnectTimeoutException(timeout));
    }

    /// <summary>
    /// Picks which running Portal to attach to, preferring one that already has the requested
    /// project open.
    ///
    /// Connect() used to take <c>processes[0]</c> unconditionally. That is fine with one Portal and
    /// actively harmful with several: attaching to a wedged or busy instance hangs until the connect
    /// timeout even when a perfectly healthy instance with the target project already open is
    /// sitting next to it in the list. Observed live 2026-08-07 — two runs against the same project
    /// succeeded, then a third hung for the full 15-minute timeout with five Portal processes
    /// running, which is the "second instance sometimes won't connect under process pileup" symptom
    /// CLAUDE.md records.
    ///
    /// <c>TiaPortalProcess.ProjectPath</c> is readable WITHOUT attaching
    /// (docs/notes/openness-api-surface-v20.md, which flagged exactly this as an unused
    /// simplification), so the choice costs nothing and touches nothing. Falls back to the old
    /// behaviour when there is no hint or no match — this narrows which process is attached, and
    /// never changes whether one is.
    /// </summary>
    private static TiaPortalProcess ChooseProcessToAttach(IList<TiaPortalProcess> processes, string? preferProjectIdentifier)
    {
        if (preferProjectIdentifier is not { Length: > 0 } wanted || string.IsNullOrWhiteSpace(wanted))
        {
            return processes[0];
        }

        foreach (TiaPortalProcess process in processes)
        {
            string? projectPath;
            try
            {
                projectPath = process.ProjectPath?.FullName;
            }
            catch (Exception)
            {
                // A process that will not describe itself is exactly one not to attach to blindly.
                continue;
            }

            if (projectPath is not null && ProjectIdentifierMatches(projectPath, wanted))
            {
                return process;
            }
        }

        return processes[0];
    }

    // Mirrors FindAlreadyOpenProject's own matching: the identifier is either a full .apNN path or a
    // bare project name, so compare both the whole path and its file-name stem.
    private static bool ProjectIdentifierMatches(string projectPath, string identifier)
    {
        if (string.Equals(projectPath, identifier, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var stem = Path.GetFileNameWithoutExtension(projectPath);
        return string.Equals(stem, identifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(stem, Path.GetFileNameWithoutExtension(identifier), StringComparison.OrdinalIgnoreCase);
    }

    // Read-only Portal-process enumeration for `portal-status`. Independent of Connect()/_tiaPortal
    // on purpose (see IOpennessGateway's own doc comment): it only calls the static
    // TiaPortal.GetProcesses() and reads each process's own properties — Id/ProjectPath/Mode/
    // AcquisitionTime are all readable WITHOUT Attach() (docs/notes/openness-api-surface-v20.md,
    // confirmed 2026-07-14), so nothing here attaches, launches, opens, or closes anything. All
    // Siemens types stay quarantined in this method; only the POCO leaves it.
    public IReadOnlyList<PortalProcessInfo> EnumeratePortalProcesses()
    {
        var results = new List<PortalProcessInfo>();
        foreach (TiaPortalProcess process in TiaPortal.GetProcesses())
        {
            var projectPath = process.ProjectPath?.FullName;
            results.Add(new PortalProcessInfo(
                process.Id,
                string.IsNullOrEmpty(projectPath) ? null : projectPath,
                process.AcquisitionTime,
                process.Mode == TiaPortalMode.WithUserInterface,
                LaunchedInstanceRegistry.IsMarkedAsLaunchedByThisTool(process.Id)));
        }

        return results;
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
                // The identifier can be a project name already open in some Portal instance
                // (common: engineer already has Portal + project open) or a path to a .apNN file
                // to open fresh. Never re-open something already open. Never Save()/Close() a
                // project this tool didn't open itself — confirmed real, 2026-07-13: this is
                // expected once a human can be running Portal manually, on their own project, at
                // the same time as this tool (the project owner's own "you control one instance,
                // I control another" ask). Openness only allows one project open per TiaPortal
                // session (confirmed real, 2026-07-10: Projects.Open() throws "Another project is
                // already open" otherwise), so a process with something else already open is
                // simply unusable for us, not an error to work around by closing what's there.
                //
                // If Connect() had to launch a brand new instance (zero Portal processes existed
                // at all), there is nothing else to search: this exact instance is known, for
                // certain, to be both empty and created by this tool in this exact call — open
                // directly into it and skip the search entirely.
                if (_connectLaunchedFreshInstance)
                {
                    _project = _tiaPortal.Projects.Open(new FileInfo(projectIdentifier));
                    UnmarkPendingLaunchOnSuccess();
                    return;
                }

                // Full search across every currently running process (not just the one Connect()
                // originally attached to) — confirmed necessary, 2026-07-14, from a real failure:
                // an earlier version of this search picked whichever process it checked first
                // that had *nothing* open, without first confirming no *other* running process
                // already held the target project's own exclusive file lock — Projects.Open()
                // then failed outright with TIA's own "already opened by user... on computer..."
                // lock error, because a sibling process genuinely had it open under a name/path
                // this code hadn't looked at yet. Search every candidate for an exact already-open
                // match before falling back to a fresh instance.
                _tiaPortal.Dispose();
                var candidates = new List<(TiaPortal Portal, int ProcessId)>();
                foreach (TiaPortalProcess candidateProcess in TiaPortal.GetProcesses())
                {
                    try
                    {
                        candidates.Add((candidateProcess.Attach(), candidateProcess.Id));
                    }
                    catch (Exception)
                    {
                        // A listed process that can't be attached to (e.g. genuinely dead/
                        // unresponsive) is simply not a candidate — skip it, don't abort the
                        // whole search over one bad entry.
                    }
                }

                foreach (var (candidate, _) in candidates)
                {
                    var alreadyOpen = FindAlreadyOpenProject(candidate.Projects, projectIdentifier);
                    if (alreadyOpen is not null)
                    {
                        _tiaPortal = candidate;
                        _project = alreadyOpen;
                        DisposeAllExcept(candidates, candidate);
                        return;
                    }
                }

                // An empty process discovered here is fair game ONLY if LaunchedInstanceRegistry
                // positively identifies it as one this tool itself launched in an earlier,
                // apparently-interrupted invocation (its own client killed before it could open a
                // project and unmark itself) — never for a process this tool has no record of
                // creating, which might just as easily be a human's own freshly-launched, still-
                // empty window. Confirmed real, 2026-07-14 (concurrent-Portal stability audit,
                // docs/notes/concurrent-portal-test-plan.md T1.1 found the human-window case; T4.1
                // found the orphan-accumulation cost of closing it the conservative way). This
                // closes the orphan gap without reopening the human-window one: the registry only
                // ever contains PIDs this tool marked itself, nothing is ever guessed.
                foreach (var (candidate, processId) in candidates)
                {
                    if (!candidate.Projects.Cast<Project>().Any() && LaunchedInstanceRegistry.IsMarkedAsLaunchedByThisTool(processId))
                    {
                        _tiaPortal = candidate;
                        _project = candidate.Projects.Open(new FileInfo(projectIdentifier));
                        LaunchedInstanceRegistry.Unmark(processId);
                        DisposeAllExcept(candidates, candidate);
                        return;
                    }
                }

                // Nothing already running is usable, and nothing empty is positively identified as
                // this tool's own — get a dedicated fresh instance, marked the same way Connect()
                // marks its own fresh launch, so a future invocation can recognize this one too if
                // this client gets killed before it finishes.
                DisposeAllExcept(candidates, null);
                var portal = new TiaPortal(TiaPortalMode.WithUserInterface);
                _tiaPortal = portal;
                _pendingLaunchPid = portal.GetCurrentProcess().Id;
                LaunchedInstanceRegistry.MarkLaunched(_pendingLaunchPid.Value);
                _project = portal.Projects.Open(new FileInfo(projectIdentifier));
                UnmarkPendingLaunchOnSuccess();
            },
            timeout,
            () => new ProjectOpenTimeoutException(timeout));
    }

    private void UnmarkPendingLaunchOnSuccess()
    {
        if (_pendingLaunchPid is int pid)
        {
            LaunchedInstanceRegistry.Unmark(pid);
            _pendingLaunchPid = null;
        }
    }

    // Attach() alone never opens/closes/saves anything, so disposing an attached (not
    // self-created) handle just releases this tool's own reference — doesn't touch the process or
    // whatever's open in it. Confirmed safe by this project's own established pattern of
    // attaching to an already-running human session across many live tests without ever closing
    // it.
    private static void DisposeAllExcept(IEnumerable<(TiaPortal Portal, int ProcessId)> candidates, TiaPortal? keep)
    {
        foreach (var (candidate, _) in candidates)
        {
            if (!ReferenceEquals(candidate, keep))
            {
                candidate.Dispose();
            }
        }
    }

    private static Project? FindAlreadyOpenProject(ProjectComposition projects, string identifier)
    {
        foreach (Project project in projects)
        {
            if (string.Equals(project.Name, identifier, StringComparison.OrdinalIgnoreCase))
            {
                return project;
            }

            if (project.Path is not null && PathsMatch(project.Path.FullName, identifier))
            {
                return project;
            }
        }

        return null;
    }

    // Plain string comparison spuriously misses an already-open project when the caller's own
    // identifier uses a different slash direction than Project.Path.FullName's own native
    // backslash format (e.g. "C:/foo/bar.ap20" vs "C:\foo\bar.ap20") — confirmed real,
    // 2026-07-14: this caused FindAlreadyOpenProject to conclude a project wasn't already open
    // when it genuinely was, triggering a spurious extra Portal instance launch. Path.GetFullPath
    // canonicalizes both sides (slash direction, relative segments) before comparing.
    // Internal (not private) so OpennessCli.Tests can exercise this directly — pure string/path
    // logic, no COM dependency, unlike the rest of this class. Confirmed real, 2026-07-14
    // (concurrent-Portal stability audit, docs/notes/concurrent-portal-test-plan.md T5.1): this had
    // zero test coverage despite being exactly the code a real bug (the slash-direction mismatch
    // documented above) was found in.
    internal static bool PathsMatch(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            // identifier isn't a well-formed path at all (e.g. it's meant to match by Name only,
            // already checked above) — not a match, not an error.
            return false;
        }
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

    // Mirrors EnumerateBlocks/WalkDeviceItem/WalkBlockGroup exactly, walking
    // PlcSoftware.TagTableGroup/PlcTagTableGroup.TagTables/.Groups instead of
    // .BlockGroup/PlcBlockGroup.Blocks/.Groups — confirmed real, 2026-07-14 (reflecting on the
    // installed DLL): the same recursive group shape, for PLC tag tables instead of blocks.
    public IReadOnlyList<TagTableInfo> EnumerateTagTables()
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(EnumerateTagTables)}.");
        }

        var results = new List<TagTableInfo>();

        foreach (Device device in _project.Devices)
        {
            foreach (DeviceItem item in device.DeviceItems)
            {
                WalkDeviceItemForTagTables(item, device.Name, results);
            }
        }

        return results;
    }

    private static void WalkDeviceItemForTagTables(DeviceItem item, string parentPath, List<TagTableInfo> results)
    {
        var path = $"{parentPath}/{item.Name}";

        var softwareContainer = item.GetService<SoftwareContainer>();
        if (softwareContainer?.Software is PlcSoftware plcSoftware)
        {
            WalkTagTableGroup(plcSoftware.TagTableGroup, path, results);
        }

        foreach (DeviceItem child in item.DeviceItems)
        {
            WalkDeviceItemForTagTables(child, path, results);
        }
    }

    public IReadOnlyList<HmiDeviceInfo> EnumerateHmi(string? screenFilter, int maxItems)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(EnumerateHmi)}.");
        }

        var results = new List<HmiDeviceInfo>();

        foreach (Device device in _project.Devices)
        {
            foreach (DeviceItem item in device.DeviceItems)
            {
                WalkDeviceItemForHmi(item, device.Name, screenFilter, maxItems, results);
            }
        }

        return results;
    }

    /// <summary>
    /// The ONLY method in the HMI half of this gateway that writes. Everything else is read-only by
    /// construction; this one is deliberately separate, separately named, and gated on an explicit
    /// confirmation at the CLI layer, so "walk the HMI" can never turn into "modify the HMI" by
    /// accident.
    ///
    /// Creates a screen, optionally a few static items on it, runs <c>Validate()</c>, and saves.
    /// Refuses if a screen of that name already exists — this creates, it never overwrites, and it
    /// never touches an existing screen.
    /// </summary>
    public HmiCreateScreenResult CreateHmiScreen(string screenName, long width, long height, IReadOnlyList<string> itemTypes)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(CreateHmiScreen)}.");
        }

        var (software, devicePath) = FindSingleUnifiedSoftware();

        // Create, never overwrite. The read walker already proved name lookup works; this is the
        // guard that keeps a "test screen" from silently replacing a real one on a name collision.
        if (software.Screens.Find(screenName) is not null)
        {
            throw new HmiScreenAlreadyExistsException(screenName);
        }

        HmiScreen screen = software.Screens.Create(screenName);
        screen.Width = (uint)width;
        screen.Height = (uint)height;

        var created = new List<string>();
        var index = 1;
        foreach (var itemType in itemTypes)
        {
            var itemName = $"{itemType}_{index++}";
            CreateScreenItem(screen, itemType, itemName);
            created.Add($"{itemType} {itemName}");
        }

        var validation = ReadValidation(screen);

        SaveProject();

        return new HmiCreateScreenResult(devicePath, screenName, width, height, created, validation, true);
    }

    /// <summary>
    /// Modifies an EXISTING screen: sets attributes, and creates event handlers. The second write
    /// path, and deliberately separate from creation — creating a screen touches nothing anyone
    /// depends on, whereas editing one changes something that already works.
    /// </summary>
    public HmiEditScreenResult EditHmiScreen(
        string screenName,
        IReadOnlyList<(string Target, string Attribute, string Value)> sets,
        IReadOnlyList<(string Target, string EventType, string? Script)> events,
        IReadOnlyList<(string Target, string Property, string Tag)> binds,
        IReadOnlyList<(string What, string Target, string? Detail)> deletes,
        IReadOnlyList<string> addItems,
        IReadOnlyList<(string Target, string Property, string Kind)> bindKinds)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(EditHmiScreen)}.");
        }

        var (software, devicePath) = FindSingleUnifiedSoftware();
        var screen = FindScreenAnywhere(software, screenName) ?? throw new HmiScreenNotFoundException(screenName);

        var applied = new List<string>();

        foreach (var (target, attribute, value) in sets)
        {
            var subject = ResolveTarget(screen, target);
            var coerced = SetAttributeCoerced(subject, attribute, value);
            applied.Add($"set {target}.{attribute} = {coerced}");
        }

        // Syntax findings join the validation list rather than living only in the applied-change
        // text, so the command can FAIL on them. Measured 2026-08-08: a deliberately unparseable
        // handler was accepted and the command still exited 0, because it gated on Validate() alone
        // — and Validate() says nothing about scripts. A check whose result cannot fail the command
        // is decoration.
        var syntaxFindings = new List<HmiValidationMessage>();
        foreach (var (target, eventType, script) in events)
        {
            var subject = ResolveTarget(screen, target);
            var created = CreateEventHandler(subject, eventType, script, syntaxFindings);
            applied.Add(created);
        }

        foreach (var (target, property, tag) in binds)
        {
            var subject = ResolveTarget(screen, target);
            applied.Add(CreateTagBinding(subject, property, tag));
        }

        // Non-tag dynamization kinds, each attempted independently so one refusal does not hide the
        // other four — the P3 question is which of the six kinds can be created at all.
        foreach (var (target, property, kindName) in bindKinds)
        {
            try
            {
                var subject = ResolveTarget(screen, target);
                applied.Add(CreateDynamizationOfKind(subject, property, kindName));
            }
            catch (Exception ex)
            {
                var root = ex.GetBaseException();
                applied.Add($"dynamization {kindName} on {target}.{property} -> REFUSED ({root.GetType().Name}: {root.Message.Split('\n')[0].Trim()})");
            }
        }

        // Adding items to an EXISTING screen, one attempt per type, each failure caught and
        // reported rather than aborting the rest. That tolerance is the point: the breadth sweep
        // asks "which of the 56 types can actually be created", and an all-or-nothing command
        // answers it one type at a time at one Portal round trip each.
        foreach (var itemType in addItems)
        {
            var itemName = $"{itemType}_P2";
            try
            {
                CreateScreenItem(screen, itemType, itemName);
                applied.Add($"add-item {itemType} -> OK");
            }
            catch (Exception ex)
            {
                var root = ex.GetBaseException();
                applied.Add($"add-item {itemType} -> REFUSED ({root.GetType().Name}: {root.Message.Split('\n')[0].Trim()})");
            }
        }

        // Screen-scoped deletes. These cannot go through `hmi-delete`, which resolves compositions
        // on HmiSoftware — items, bindings and handlers hang off a SCREEN, not off the device.
        foreach (var (what, target, detail) in deletes)
        {
            applied.Add(DeleteScreenScoped(screen, what, target, detail));
        }

        var validation = ReadValidation(screen).Concat(syntaxFindings).ToList();
        SaveProject();

        return new HmiEditScreenResult(devicePath, screenName, applied, validation, true);
    }

    /// <summary>
    /// Binds a property to a tag by creating a <c>TagDynamization</c> on it. This is the PLC↔HMI
    /// coupling — see <c>openness-hmi-write-api.md</c> §3.
    ///
    /// The composition is keyed by PROPERTY NAME and <c>PropertyName</c> is get-only, so re-binding
    /// is delete-then-create rather than assignment. <c>Tag</c> is a plain string with no
    /// existence check of any kind at assign time, which is exactly what makes §4d's
    /// unchecked-reference problem real.
    /// </summary>
    /// <summary>
    /// Creates a dynamization of ANY kind on a property. `TagDynamization` gets the tag assigned;
    /// the other five kinds are created bare, because the point of P3 is to learn whether they can
    /// be created at all and what they demand afterwards.
    /// </summary>
    private static string CreateDynamizationOfKind(IEngineeringObject subject, string propertyName, string kindName)
    {
        var dynamizations = subject.GetType().GetProperty("Dynamizations")?.GetValue(subject)
            ?? throw new HmiDynamizationsNotSupportedException(subject.GetType().Name);

        var kindType = typeof(TagDynamization).Assembly.GetTypes()
            .FirstOrDefault(t => t.Name.Equals(kindName, StringComparison.OrdinalIgnoreCase) && typeof(DynamizationBase).IsAssignableFrom(t) && !t.IsAbstract)
            ?? throw new HmiUnknownKindException(kindName, new List<string> { "TagDynamization", "ScriptDynamization", "FlashingDynamization", "ExpressionDynamization", "ResourceListDynamization", "TagParameterDynamization" });

        var find = dynamizations.GetType().GetMethods()
            .FirstOrDefault(m => m.Name == "Find" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
        var existing = find?.Invoke(dynamizations, new object[] { propertyName });
        if (existing is not null)
        {
            existing.GetType().GetMethod("Delete", Type.EmptyTypes)?.Invoke(existing, null);
        }

        var create = dynamizations.GetType().GetMethods()
            .FirstOrDefault(m => m.Name == "Create" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
            ?? throw new HmiDynamizationsNotSupportedException(subject.GetType().Name);

        var created = create.MakeGenericMethod(kindType).Invoke(dynamizations, new object[] { propertyName })
            ?? throw new InvalidOperationException($"Creating a {kindName} on '{propertyName}' returned null.");

        var actualKind = TryRead(() => ((DynamizationBase)created).DynamizationType.ToString()) ?? "?";
        return $"dynamization {kindName} created on {subject.GetType().Name}.{propertyName} [DynamizationType={actualKind}]";
    }

    private static string CreateTagBinding(IEngineeringObject subject, string propertyName, string tagName)
    {
        var dynamizations = subject.GetType().GetProperty("Dynamizations")?.GetValue(subject)
            ?? throw new HmiDynamizationsNotSupportedException(subject.GetType().Name);

        // Replace rather than stack: a property can carry only one dynamization, and Find is keyed
        // by the property name.
        var find = dynamizations.GetType().GetMethods()
            .FirstOrDefault(m => m.Name == "Find" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
        var existing = find?.Invoke(dynamizations, new object[] { propertyName });
        var replaced = existing is not null;
        if (existing is not null)
        {
            existing.GetType().GetMethod("Delete", Type.EmptyTypes)?.Invoke(existing, null);
        }

        var create = dynamizations.GetType().GetMethods()
            .FirstOrDefault(m => m.Name == "Create" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
            ?? throw new HmiDynamizationsNotSupportedException(subject.GetType().Name);

        var dynamization = create.MakeGenericMethod(typeof(TagDynamization)).Invoke(dynamizations, new object[] { propertyName })
            ?? throw new InvalidOperationException($"Creating a TagDynamization on '{propertyName}' returned null.");

        var typed = (TagDynamization)dynamization;
        typed.Tag = tagName;

        // PlcTag/Address/DataType are GET-ONLY read-backs derived from the tag itself. Reading them
        // straight back is the only available evidence that the name resolved to anything — empty
        // means unresolved, and nothing else will say so.
        var plcTag = TryRead(() => typed.PlcTag);
        var dataType = TryRead(() => typed.DataType);
        var resolved = string.IsNullOrEmpty(plcTag) && string.IsNullOrEmpty(dataType)
            ? "UNRESOLVED (PlcTag and DataType both empty — the tag name may not exist)"
            : $"resolved: plcTag='{plcTag}' dataType='{dataType}'";

        return $"bind {(replaced ? "replaced" : "created")} {subject.GetType().Name}.{propertyName} <- tag '{tagName}' [{resolved}]";
    }

    /// <summary>
    /// Deletes something that lives ON a screen: an item, a tag binding, or an event handler. Each
    /// re-reads afterwards and says whether the thing actually went away — Openness has no
    /// transaction, so a `Delete()` that did not throw is not proof it took effect.
    /// </summary>
    private static string DeleteScreenScoped(HmiScreen screen, string what, string target, string? detail)
    {
        switch (what)
        {
            case "item":
            {
                var item = FindScreenItem(screen, target) ?? throw new HmiScreenItemNotFoundException(target, TryRead(() => screen.Name) ?? string.Empty);
                item.Delete();
                var gone = FindScreenItem(screen, target) is null;
                return gone
                    ? $"deleted item '{target}'; confirmed absent on re-read"
                    : $"DELETE CALLED on item '{target}' but it is STILL PRESENT";
            }

            case "bind":
            {
                var subject = ResolveTarget(screen, target);
                var property = detail ?? throw new InvalidOperationException("--delete-bind needs <Target>.<Property>.");
                var dynamizations = subject.GetType().GetProperty("Dynamizations")?.GetValue(subject)
                    ?? throw new HmiDynamizationsNotSupportedException(subject.GetType().Name);
                var find = dynamizations.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "Find" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
                var existing = find?.Invoke(dynamizations, new object[] { property })
                    ?? throw new HmiObjectNotFoundException("Dynamizations", $"{target}.{property}");
                existing.GetType().GetMethod("Delete", Type.EmptyTypes)?.Invoke(existing, null);
                var stillThere = find?.Invoke(dynamizations, new object[] { property }) is not null;
                return stillThere
                    ? $"DELETE CALLED on binding {target}.{property} but it is STILL PRESENT"
                    : $"deleted binding {target}.{property}; confirmed absent on re-read";
            }

            case "event":
            {
                var subject = ResolveTarget(screen, target);
                var eventTypeName = detail ?? throw new InvalidOperationException("--delete-event needs <Target>:<EventType>.");
                var composition = subject.GetType().GetProperty("EventHandlers")?.GetValue(subject)
                    ?? throw new HmiEventsNotSupportedException(subject.GetType().Name);
                var findEv = composition.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "Find" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsEnum)
                    ?? throw new HmiEventsNotSupportedException(subject.GetType().Name);
                var enumType = findEv.GetParameters()[0].ParameterType;
                var names = Enum.GetNames(enumType);
                var match = names.FirstOrDefault(n => string.Equals(n, eventTypeName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new HmiUnknownEventTypeException(eventTypeName, subject.GetType().Name, names);
                object parsed = Enum.Parse(enumType, match);
                var handler = findEv.Invoke(composition, new object[] { parsed })
                    ?? throw new HmiObjectNotFoundException("EventHandlers", $"{target}:{eventTypeName}");
                handler.GetType().GetMethod("Delete", Type.EmptyTypes)?.Invoke(handler, null);
                var survives = findEv.Invoke(composition, new object[] { parsed }) is not null;
                return survives
                    ? $"DELETE CALLED on event {target}:{match} but it is STILL PRESENT"
                    : $"deleted event {target}:{match}; confirmed absent on re-read";
            }

            default:
                throw new InvalidOperationException($"Unknown delete kind '{what}'.");
        }
    }

    private static HmiScreenItemBase? FindScreenItem(HmiScreen screen, string name)
    {
        foreach (HmiScreenItemBase item in screen.ScreenItems)
        {
            if (string.Equals(TryRead(() => item.Name), name, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    // "Screen" addresses the screen itself; anything else is an item name on it. Unknown names are a
    // hard error rather than a no-op, because a silently-skipped edit is indistinguishable from a
    // successful one in the output.
    private static IEngineeringObject ResolveTarget(HmiScreen screen, string target)
    {
        if (string.Equals(target, "Screen", StringComparison.OrdinalIgnoreCase))
        {
            return screen;
        }

        foreach (HmiScreenItemBase item in screen.ScreenItems)
        {
            if (string.Equals(TryRead(() => item.Name), target, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        throw new HmiScreenItemNotFoundException(target, TryRead(() => screen.Name) ?? string.Empty);
    }

    // SetAttribute takes an object, and the API is strict about the runtime type: a UInt32 property
    // will not accept a string or an Int32. The declared type comes from the object's own
    // GetAttributeInfos, so the coercion is driven by the API's own schema rather than guesswork.
    private static string SetAttributeCoerced(IEngineeringObject subject, string attribute, string value)
    {
        var info = subject.GetAttributeInfos().FirstOrDefault(i => string.Equals(i.Name, attribute, StringComparison.OrdinalIgnoreCase));
        if (info is null)
        {
            throw new HmiUnknownAttributeException(attribute, subject.GetType().Name);
        }

        if (info.AccessMode is EngineeringAttributeAccessMode.Read or EngineeringAttributeAccessMode.None)
        {
            throw new HmiAttributeNotWritableException(attribute, subject.GetType().Name, info.AccessMode.ToString());
        }

        var targetType = info.SupportedTypes?.FirstOrDefault();
        object converted = targetType is null
            ? value
            : targetType.IsEnum
                ? Enum.Parse(targetType, value, ignoreCase: true)
                : Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);

        subject.SetAttribute(info.Name, converted);
        return $"{converted} ({converted.GetType().Name})";
    }

    // Each item type has its own EventHandlers composition whose Create() takes that type's own
    // event enum — Create(HmiButtonEventType) and so on. Bound at runtime for the same reason
    // CreateScreenItem is: there are ~40 of them and a hand-written switch would rot.
    private static string CreateEventHandler(IEngineeringObject subject, string eventTypeName, string? script, List<HmiValidationMessage> syntaxFindings)
    {
        var composition = subject.GetType().GetProperty("EventHandlers")?.GetValue(subject)
            ?? throw new HmiEventsNotSupportedException(subject.GetType().Name);

        var create = composition.GetType().GetMethods()
            .FirstOrDefault(m => m.Name == "Create" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsEnum)
            ?? throw new HmiEventsNotSupportedException(subject.GetType().Name);

        // net48 has no non-generic Enum.TryParse(Type, ...), and matching against the declared names
        // first means the error can list what IS valid rather than just saying "no".
        var enumType = create.GetParameters()[0].ParameterType;
        var names = Enum.GetNames(enumType);
        var match = names.FirstOrDefault(n => string.Equals(n, eventTypeName, StringComparison.OrdinalIgnoreCase))
            ?? throw new HmiUnknownEventTypeException(eventTypeName, subject.GetType().Name, names);

        object parsed = Enum.Parse(enumType, match);

        // Find-then-Create, so the command is IDEMPOTENT: re-running it to change a script must
        // update the existing handler, not stack a second one on the same event. Openness offers
        // Find(eventType) on the same composition, which is exactly the lookup needed.
        var find = composition.GetType().GetMethods()
            .FirstOrDefault(m => m.Name == "Find" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == enumType);

        var existing = find?.Invoke(composition, new object[] { parsed });
        var reused = existing is not null;

        var handler = existing
            ?? create.Invoke(composition, new object[] { parsed })
            ?? throw new InvalidOperationException($"Creating the '{eventTypeName}' handler returned null.");

        var verb = reused ? "updated" : "created";
        if (script is null)
        {
            return $"event {verb} {subject.GetType().Name}.{eventTypeName} (no script)";
        }

        var scriptObject = handler.GetType().GetProperty("Script")?.GetValue(handler)
            ?? throw new InvalidOperationException($"The '{eventTypeName}' handler exposes no Script to set.");
        scriptObject.GetType().GetProperty("ScriptCode")?.SetValue(scriptObject, script);

        // SyntaxCheck() is on IHmiScript and had never been called by this project. Calling it here
        // is the point: a script body is otherwise accepted verbatim with nothing checking it, which
        // is the HMI equivalent of importing LAD without compiling.
        var syntax = RunSyntaxCheck(scriptObject);
        if (syntax.StartsWith("SYNTAX ERRORS", StringComparison.Ordinal) || syntax.StartsWith("SyntaxCheck threw", StringComparison.Ordinal))
        {
            syntaxFindings.Add(new HmiValidationMessage($"{subject.GetType().Name}.{eventTypeName}", "Error", syntax));
        }

        return $"event {verb} {subject.GetType().Name}.{eventTypeName} ({script.Length} chars, {syntax})";
    }

    // Returns a short human-readable verdict rather than throwing: a syntax problem should be
    // reported alongside everything else that happened, not abort the whole edit halfway through.
    private static string RunSyntaxCheck(object scriptObject)
    {
        try
        {
            var method = scriptObject.GetType().GetMethod("SyntaxCheck", Type.EmptyTypes);
            if (method is null)
            {
                return "SyntaxCheck unavailable";
            }

            if (method.Invoke(scriptObject, null) is not Siemens.Engineering.HmiUnified.Common.HmiValidationResult result)
            {
                return "SyntaxCheck returned nothing";
            }

            var errors = (result.Errors ?? Enumerable.Empty<string>()).ToList();
            var warnings = (result.Warnings ?? Enumerable.Empty<string>()).ToList();
            if (errors.Count == 0 && warnings.Count == 0)
            {
                return "syntax OK";
            }

            var parts = new List<string>();
            if (errors.Count > 0)
            {
                parts.Add("SYNTAX ERRORS: " + string.Join("; ", errors));
            }

            if (warnings.Count > 0)
            {
                parts.Add("warnings: " + string.Join("; ", warnings));
            }

            return string.Join(" | ", parts);
        }
        catch (Exception ex)
        {
            return $"SyntaxCheck threw {ex.GetType().Name}";
        }
    }

    private static HmiScreen? FindScreenAnywhere(HmiSoftware software, string screenName)
    {
        var direct = software.Screens.Find(screenName);
        if (direct is not null)
        {
            return direct;
        }

        // Screens.Find is root-only for the same reason Screens is (survey §7) — a screen inside a
        // group is invisible to it, so the group tree has to be walked too.
        foreach (HmiScreenGroup group in software.ScreenGroups)
        {
            var found = FindScreenInGroup(group, screenName);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static HmiScreen? FindScreenInGroup(HmiScreenGroup group, string screenName)
    {
        var direct = group.Screens.Find(screenName);
        if (direct is not null)
        {
            return direct;
        }

        foreach (HmiScreenGroup child in group.Groups)
        {
            var found = FindScreenInGroup(child, screenName);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    // Create<T> is generic over the item type, but the caller names types as strings, so the type
    // argument has to be bound at runtime. Resolved against the assembly that actually defines the
    // Unified UI types rather than a hand-maintained switch — the device reports 56 creatable types
    // and enumerating them here by hand would rot.
    private static void CreateScreenItem(HmiScreen screen, string itemTypeName, string itemName)
    {
        var composition = screen.ScreenItems;
        var type = typeof(HmiScreenItemBase).Assembly
            .GetTypes()
            .FirstOrDefault(t => t.Name.Equals(itemTypeName, StringComparison.OrdinalIgnoreCase)
                && typeof(HmiScreenItemBase).IsAssignableFrom(t)
                && !t.IsAbstract);

        if (type is null)
        {
            throw new HmiUnknownScreenItemTypeException(itemTypeName);
        }

        var create = composition.GetType()
            .GetMethods()
            .FirstOrDefault(m => m.Name == "Create" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);

        if (create is null)
        {
            throw new InvalidOperationException("HmiScreenItemBaseComposition.Create<T>(string) was not found on the installed Openness assembly.");
        }

        create.MakeGenericMethod(type).Invoke(composition, new object[] { itemName });
    }

    private static IReadOnlyList<HmiValidationMessage> ReadValidation(HmiScreen screen)
    {
        var messages = new List<HmiValidationMessage>();
        try
        {
            // Fully qualified: Siemens's own HmiValidationResult and this project's
            // HmiValidationMessage are one letter apart in intent and would read ambiguously here.
            foreach (Siemens.Engineering.HmiUnified.Common.HmiValidationResult result in screen.Validate())
            {
                var property = TryRead(() => result.PropertyName) ?? string.Empty;
                foreach (var error in result.Errors ?? Enumerable.Empty<string>())
                {
                    messages.Add(new HmiValidationMessage(property, "Error", error));
                }

                foreach (var warning in result.Warnings ?? Enumerable.Empty<string>())
                {
                    messages.Add(new HmiValidationMessage(property, "Warning", warning));
                }
            }
        }
        catch (Exception ex)
        {
            // Validate() has never been called by this project before. If it throws, that is itself
            // the finding — surface it as a message rather than losing it or failing the create.
            messages.Add(new HmiValidationMessage(string.Empty, "ValidateThrew", ex.GetType().Name + ": " + ex.Message));
        }

        return messages;
    }

    private (HmiSoftware Software, string Path) FindSingleUnifiedSoftware()
    {
        var found = new List<(HmiSoftware Software, string Path)>();
        foreach (Device device in _project!.Devices)
        {
            foreach (DeviceItem item in device.DeviceItems)
            {
                CollectUnifiedSoftware(item, device.Name, found);
            }
        }

        return found.Count switch
        {
            0 => throw new NoUnifiedHmiDeviceException(),
            1 => found[0],
            // Writing to the wrong panel is not recoverable by re-reading, so an ambiguous target is
            // a hard error rather than a first-match guess.
            _ => throw new AmbiguousHmiDeviceException(found.Select(f => f.Path).ToList()),
        };
    }

    private static void CollectUnifiedSoftware(DeviceItem item, string parentPath, List<(HmiSoftware, string)> found)
    {
        var path = $"{parentPath}/{item.Name}";
        if (item.GetService<SoftwareContainer>()?.Software is HmiSoftware unified)
        {
            found.Add((unified, path));
        }

        foreach (DeviceItem child in item.DeviceItems)
        {
            CollectUnifiedSoftware(child, path, found);
        }
    }

    /// <summary>
    /// Creates an HMI tag (and its tag table if absent). Exists to give the dynamization probe a
    /// bind target that this session invented, rather than binding to a real project tag.
    ///
    /// Deliberately minimal: name, table, data type. A tag intended to reach a PLC would also need
    /// `Connection` and `PlcTag` set, which is out of scope here — an internal tag is enough to test
    /// whether a binding resolves.
    /// </summary>
    public string CreateHmiTag(string tagName, string tableName, string dataType)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(CreateHmiTag)}.");
        }

        var (software, devicePath) = FindSingleUnifiedSoftware();

        var table = software.TagTables.Find(tableName);
        var createdTable = table is null;
        table ??= software.TagTables.Create(tableName);

        if (software.Tags.Find(tagName) is not null)
        {
            throw new HmiTagAlreadyExistsException(tagName);
        }

        var tag = software.Tags.Create(tagName, tableName);

        // Data-type assignment is attempted, not required. Measured 2026-08-08: setting
        // HmiDataType on a freshly created tag throws
        //   "Set is not allowed for disabled fields"
        // — writability here is CONTEXTUAL, depending on other properties (connection/tag type), and
        // GetAttributeInfos' static AccessMode does not predict it. Failing the whole create over an
        // optional refinement would be wrong, so the outcome is reported instead.
        var typeNote = TrySetDataType(tag, dataType);

        SaveProject();

        var readBack = TryRead(() => tag.HmiDataType) ?? "(unreadable)";
        return $"created tag '{tagName}' in table '{tableName}'{(createdTable ? " (table created)" : "")} on {devicePath}; " +
               $"requested type '{dataType}' -> {typeNote}; HmiDataType now '{readBack}'";
    }

    private static string TrySetDataType(Siemens.Engineering.HmiUnified.HmiTags.HmiTag tag, string dataType)
    {
        try
        {
            tag.HmiDataType = dataType;
            return "HmiDataType set";
        }
        catch (Exception ex)
        {
            var first = ex.GetBaseException().Message.Split('\n')[0].Trim();
            return $"HmiDataType REFUSED ({first})";
        }
    }

    /// <summary>
    /// The prefix every object this tool creates must carry, and the ONLY prefix it will delete by
    /// default. The data-boundary restriction is procedural everywhere else in this tool; for the
    /// destructive path it is enforced in code, because this programme runs unattended and a
    /// mistyped name must not be able to delete one of the 48 real screens or 233 real tags.
    /// </summary>
    public const string ProbeArtifactPrefix = "ZZ_AI_";

    /// <summary>
    /// Metamodel-driven create: resolves a composition on <c>HmiSoftware</c> BY NAME at runtime and
    /// invokes its <c>Create</c>. One method instead of a subcommand per kind — there are 80
    /// creatable composition kinds, and the API is self-describing enough that hand-writing 80
    /// wrappers would be transcription, not engineering.
    /// </summary>
    /// <param name="parent">Second Create argument where the composition takes one (e.g. a tag's
    /// table name). Ignored by single-argument Creates.</param>
    public string CreateHmiObject(string kind, string name, string? parent)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(CreateHmiObject)}.");
        }

        var (software, devicePath) = FindSingleUnifiedSoftware();
        var composition = ResolveComposition(software, kind);

        if (FindInComposition(composition, name) is not null)
        {
            throw new HmiObjectAlreadyExistsException(kind, name);
        }

        // Prefer the overload whose argument count matches what the caller supplied, so `--in`
        // selects Tags.Create(name, table) over Tags.Create(name) without special-casing tags.
        var creates = composition.GetType().GetMethods()
            .Where(m => m.Name == "Create" && !m.IsGenericMethodDefinition)
            .Where(m => m.GetParameters().All(p => p.ParameterType == typeof(string)))
            .OrderByDescending(m => m.GetParameters().Length)
            .ToList();

        if (creates.Count == 0)
        {
            throw new HmiKindNotCreatableException(kind);
        }

        var wanted = parent is null ? 1 : 2;
        var create = creates.FirstOrDefault(m => m.GetParameters().Length == wanted) ?? creates[0];
        var args = create.GetParameters().Length == 2 ? new object[] { name, parent ?? string.Empty } : new object[] { name };

        var created = create.Invoke(composition, args)
            ?? throw new InvalidOperationException($"{kind}.Create returned null for '{name}'.");

        SaveProject();
        return $"created {kind} '{name}'{(parent is not null ? $" in '{parent}'" : string.Empty)} on {devicePath} [{created.GetType().Name}]";
    }

    /// <summary>
    /// Metamodel-driven delete. Enumerates the composition and matches on <c>Name</c> rather than
    /// using <c>Find</c>, because Find's signature varies across compositions (some key on an enum,
    /// some on a string) while every composition is enumerable and every deletable object has a
    /// Name.
    /// </summary>
    public string DeleteHmiObject(string kind, string name, bool allowAnyName)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(DeleteHmiObject)}.");
        }

        if (!allowAnyName && !name.StartsWith(ProbeArtifactPrefix, StringComparison.Ordinal))
        {
            throw new HmiRefusedToDeleteRealObjectException(name, ProbeArtifactPrefix);
        }

        var (software, devicePath) = FindSingleUnifiedSoftware();
        var composition = ResolveComposition(software, kind);

        var target = FindInComposition(composition, name)
            ?? throw new HmiObjectNotFoundException(kind, name);

        var delete = target.GetType().GetMethod("Delete", Type.EmptyTypes)
            ?? throw new HmiKindNotDeletableException(kind, target.GetType().Name);

        var typeName = target.GetType().Name;
        delete.Invoke(target, null);
        SaveProject();

        // Prove it by re-reading rather than trusting the call: Openness has no transaction, so
        // "did not throw" is not the same as "is gone".
        var stillThere = FindInComposition(ResolveComposition(software, kind), name) is not null;
        return stillThere
            ? $"DELETE CALLED but '{name}' is STILL PRESENT in {kind} — the object did not go away"
            : $"deleted {kind} '{name}' [{typeName}] on {devicePath}; confirmed absent on re-read";
    }

    /// <summary>
    /// Sets attributes on any object in any composition — the counterpart to
    /// <see cref="CreateHmiObject"/>, for the objects that are useless bare (an alarm needs a
    /// trigger, a log needs a path). <paramref name="texts"/> goes through the MultilingualText
    /// path, which is the one this survey flagged as awkward: `Items` has no `Create`, so a language
    /// must already exist in the project.
    /// </summary>
    public IReadOnlyList<string> SetHmiObjectAttributes(
        string kind,
        string name,
        IReadOnlyList<(string Attribute, string Value)> sets,
        IReadOnlyList<(string Attribute, string Value)> texts)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(SetHmiObjectAttributes)}.");
        }

        var (software, _) = FindSingleUnifiedSoftware();
        var composition = ResolveComposition(software, kind);
        var target = FindInComposition(composition, name) ?? throw new HmiObjectNotFoundException(kind, name);

        var applied = new List<string>();

        foreach (var (attribute, value) in sets)
        {
            try
            {
                applied.Add("set " + SetAttributeCoerced((IEngineeringObject)target, attribute, value) + $"  -> {attribute}");
            }
            catch (Exception ex)
            {
                var root = ex.GetBaseException();
                applied.Add($"set {attribute} -> REFUSED ({root.GetType().Name}: {root.Message.Split('\n')[0].Trim()})");
            }
        }

        foreach (var (attribute, value) in texts)
        {
            applied.Add(SetMultilingualText(target, attribute, value));
        }

        SaveProject();
        return applied;
    }

    /// <summary>
    /// Writes a language-specific string into a <c>MultilingualText</c> property. The awkward part,
    /// per the survey: the property is get-only, <c>Items</c> has no <c>Create</c>, and runtime
    /// languages cannot be added — so the text can only go into a language the project already has.
    /// Reports which language it used, since that is the fact worth knowing.
    /// </summary>
    private string SetMultilingualText(object target, string attribute, string value)
    {
        try
        {
            var mlt = target.GetType().GetProperty(attribute)?.GetValue(target);
            if (mlt is null)
            {
                return $"text {attribute} -> REFUSED (no such property, or it returned null)";
            }

            if (mlt is not MultilingualText multilingual)
            {
                return $"text {attribute} -> REFUSED (property is {mlt.GetType().Name}, not MultilingualText)";
            }

            // Items has no Create, so the language must already exist. Take the first item rather
            // than guessing a culture: which languages a project has is a project fact, not ours.
            foreach (MultilingualTextItem item in multilingual.Items)
            {
                var culture = TryRead(() => item.Language?.Culture?.Name) ?? "?";
                item.Text = value;
                return $"text {attribute} -> set in language '{culture}'";
            }

            return $"text {attribute} -> REFUSED (MultilingualText has no language items; Items has no Create and runtime languages cannot be added)";
        }
        catch (Exception ex)
        {
            var root = ex.GetBaseException();
            return $"text {attribute} -> REFUSED ({root.GetType().Name}: {root.Message.Split('\n')[0].Trim()})";
        }
    }

    public IReadOnlyList<HmiObjectInfo> InventoryHmi(string? kindFilter)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(InventoryHmi)}.");
        }

        var (software, _) = FindSingleUnifiedSoftware();
        var results = new List<HmiObjectInfo>();

        foreach (var property in software.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (!property.PropertyType.Name.EndsWith("Composition", StringComparison.Ordinal))
            {
                continue;
            }

            if (kindFilter is not null && !string.Equals(property.Name, kindFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryReadObject(() => property.GetValue(software)) is not System.Collections.IEnumerable items)
            {
                continue;
            }

            try
            {
                foreach (var item in items)
                {
                    if (item is null)
                    {
                        continue;
                    }

                    var itemName = TryRead(() => item.GetType().GetProperty("Name")?.GetValue(item) as string) ?? "(unnamed)";
                    results.Add(new HmiObjectInfo(property.Name, itemName, item.GetType().Name));
                }
            }
            catch (Exception)
            {
                // A composition that refuses to enumerate is reported by its absence, not by an
                // exception that would lose the other 19.
            }
        }

        return results;
    }

    // Resolves e.g. "Tags" / "DiscreteAlarms" / "Screens" to the composition object on HmiSoftware.
    private static object ResolveComposition(HmiSoftware software, string kind)
    {
        var property = software.GetType()
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(p => string.Equals(p.Name, kind, StringComparison.OrdinalIgnoreCase)
                && p.PropertyType.Name.EndsWith("Composition", StringComparison.Ordinal));

        if (property is null)
        {
            var available = software.GetType()
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.PropertyType.Name.EndsWith("Composition", StringComparison.Ordinal))
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal);
            throw new HmiUnknownKindException(kind, available.ToList());
        }

        return property.GetValue(software)
            ?? throw new HmiUnknownKindException(kind, new List<string>());
    }

    // Screens are the one kind where enumeration is not enough — Screens is root-only and a screen
    // may live in a group (survey §7) — so that case delegates to the recursive finder.
    private object? FindInComposition(object composition, string name)
    {
        if (composition is HmiScreenComposition screens)
        {
            var software = FindSingleUnifiedSoftware().Software;
            return FindScreenAnywhere(software, name);
        }

        if (composition is not System.Collections.IEnumerable items)
        {
            return null;
        }

        try
        {
            foreach (var item in items)
            {
                if (item is null)
                {
                    continue;
                }

                var itemName = TryRead(() => item.GetType().GetProperty("Name")?.GetValue(item) as string);
                if (string.Equals(itemName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    public IReadOnlyList<HmiSchemaReport> EnumerateHmiSchema(string screenFilter, int maxItems)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(EnumerateHmiSchema)}.");
        }

        var results = new List<HmiSchemaReport>();

        foreach (Device device in _project.Devices)
        {
            foreach (DeviceItem item in device.DeviceItems)
            {
                WalkDeviceItemForHmiSchema(item, device.Name, screenFilter, maxItems, results);
            }
        }

        return results;
    }

    private static void WalkDeviceItemForHmiSchema(
        DeviceItem item,
        string parentPath,
        string screenFilter,
        int maxItems,
        List<HmiSchemaReport> results)
    {
        var path = $"{parentPath}/{item.Name}";

        if (item.GetService<SoftwareContainer>()?.Software is HmiSoftware unified)
        {
            results.Add(ReadUnifiedSchema(unified, path, screenFilter, maxItems));
        }

        foreach (DeviceItem child in item.DeviceItems)
        {
            WalkDeviceItemForHmiSchema(child, path, screenFilter, maxItems, results);
        }
    }

    // Schema is a property of the TYPE, so each distinct CLR type is described once no matter how
    // many instances carry it. Classic is absent here on purpose: it exposes no screen items, so
    // there is no item schema to report (docs/notes/openness-hmi-api-survey.md §3).
    private static HmiSchemaReport ReadUnifiedSchema(HmiSoftware software, string path, string screenFilter, int maxItems)
    {
        var creatable = new List<string>();
        var schemas = new Dictionary<string, HmiTypeSchema>(StringComparer.Ordinal);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var screens = new List<HmiScreen>();
        foreach (HmiScreen screen in software.Screens)
        {
            if (seen.Add(TryRead(() => screen.Name) ?? string.Empty))
            {
                screens.Add(screen);
            }
        }

        foreach (HmiScreenGroup group in software.ScreenGroups)
        {
            CollectGroupScreens(group, seen, screens);
        }

        foreach (var screen in screens)
        {
            var screenName = TryRead(() => screen.Name) ?? string.Empty;
            if (!WantsDetail(screenName, screenFilter))
            {
                continue;
            }

            // The creation metamodel: what types this composition will accept. This is the piece no
            // sample document could give you — it is the API stating its own contract.
            if (creatable.Count == 0)
            {
                creatable.AddRange(ReadCreatableTypes(screen, "ScreenItems"));
            }

            if (!schemas.ContainsKey(nameof(HmiScreen)))
            {
                schemas[nameof(HmiScreen)] = ReadTypeSchema(screen, nameof(HmiScreen));
            }

            var read = 0;
            foreach (HmiScreenItemBase item in screen.ScreenItems)
            {
                if (read++ >= maxItems)
                {
                    break;
                }

                var typeName = item.GetType().Name;
                if (!schemas.ContainsKey(typeName))
                {
                    schemas[typeName] = ReadTypeSchema(item, typeName);
                }
            }
        }

        return new HmiSchemaReport(path, creatable, schemas.Values.OrderBy(s => s.TypeName, StringComparer.Ordinal).ToList());
    }

    private static void CollectGroupScreens(HmiScreenGroup group, HashSet<string> seen, List<HmiScreen> results)
    {
        foreach (HmiScreen screen in group.Screens)
        {
            if (seen.Add(TryRead(() => screen.Name) ?? string.Empty))
            {
                results.Add(screen);
            }
        }

        foreach (HmiScreenGroup child in group.Groups)
        {
            CollectGroupScreens(child, seen, results);
        }
    }

    private static IReadOnlyList<string> ReadCreatableTypes(IEngineeringObject owner, string compositionName)
    {
        try
        {
            return owner.GetCreationInfos(compositionName)
                .Select(info => info.Type?.Name ?? "(unknown)")
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    private static HmiTypeSchema ReadTypeSchema(IEngineeringObject subject, string typeName)
    {
        var compositions = new List<string>();
        try
        {
            compositions.AddRange(subject.GetCompositionInfos().Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal));
        }
        catch (Exception)
        {
            // A type that will not describe its child compositions still has attributes worth having.
        }

        var attributes = new List<HmiAttributeSchema>();
        try
        {
            foreach (var info in subject.GetAttributeInfos().OrderBy(i => i.Name, StringComparer.Ordinal))
            {
                attributes.Add(new HmiAttributeSchema(
                    info.Name,
                    TryRead(() => info.AccessMode.ToString()) ?? "?",
                    TryRead(() => info.CreateRelevance.ToString()) ?? "?",
                    TryRead(() => info.SupportedTypes?.FirstOrDefault()?.Name),
                    // A sample value makes an attribute name legible in a way a type name alone does
                    // not — "HorizontalAlignment : HmiHorizontalAlignment" says far less than "= Left".
                    DescribeValue(subject, info.Name)));
            }
        }
        catch (Exception)
        {
            // Same reasoning as everywhere else in this walker: partial beats nothing.
        }

        return new HmiTypeSchema(typeName, compositions, attributes);
    }

    private static string? DescribeValue(IEngineeringObject subject, string attributeName)
    {
        try
        {
            var value = subject.GetAttribute(attributeName);
            return value switch
            {
                null => null,
                AttributeValueUnsupported => "(unsupported)",
                var v => v.ToString(),
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Same recursive DeviceItem descent as WalkDeviceItem/WalkDeviceItemForTagTables, but matching
    // the two HMI software types instead of PlcSoftware. A device is only ever one of the three, so
    // the branches are exclusive and a PLC device simply falls through as before.
    private static void WalkDeviceItemForHmi(
        DeviceItem item,
        string parentPath,
        string? screenFilter,
        int maxItems,
        List<HmiDeviceInfo> results)
    {
        var path = $"{parentPath}/{item.Name}";

        var softwareContainer = item.GetService<SoftwareContainer>();
        switch (softwareContainer?.Software)
        {
            case HmiSoftware unified:
                results.Add(ReadUnifiedDevice(unified, path, screenFilter, maxItems));
                break;
            case HmiTarget classic:
                results.Add(ReadClassicDevice(classic, path));
                break;
        }

        foreach (DeviceItem child in item.DeviceItems)
        {
            WalkDeviceItemForHmi(child, path, screenFilter, maxItems, results);
        }
    }

    private static HmiDeviceInfo ReadUnifiedDevice(HmiSoftware software, string path, string? screenFilter, int maxItems)
    {
        // Screens are reachable two ways: HmiSoftware.Screens, and recursively via HmiScreenGroups.
        // What is NOT established is whether HmiSoftware.Screens is root-only (so the group walk adds
        // screens) or already a flat view of all of them (so the group walk would double-count).
        // Rather than bet on one reading, collect both and deduplicate by name — correct under
        // either, and it cannot silently under-report the folder-organised case, which is the
        // failure that would actually mislead. Screen names are unique per Unified device.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var screens = new List<HmiScreenInfo>();

        foreach (HmiScreen screen in software.Screens)
        {
            AddUnifiedScreen(screen, screenFilter, maxItems, seen, screens);
        }

        foreach (HmiScreenGroup group in software.ScreenGroups)
        {
            WalkUnifiedScreenGroup(group, screenFilter, maxItems, seen, screens);
        }

        return new HmiDeviceInfo(
            path,
            software.Name,
            HmiFamily.Unified,
            screens.Count,
            CountOrZero(() => software.ScreenGroups.Count),
            CountOrZero(() => software.Tags.Count),
            CountOrZero(() => software.DiscreteAlarms.Count),
            CountOrZero(() => software.AnalogAlarms.Count),
            CountOrZero(() => software.AlarmClasses.Count),
            CountOrZero(() => software.Scripts.Count),
            screens);
    }

    private static void WalkUnifiedScreenGroup(
        HmiScreenGroup group,
        string? screenFilter,
        int maxItems,
        HashSet<string> seen,
        List<HmiScreenInfo> results)
    {
        foreach (HmiScreen screen in group.Screens)
        {
            AddUnifiedScreen(screen, screenFilter, maxItems, seen, results);
        }

        foreach (HmiScreenGroup child in group.Groups)
        {
            WalkUnifiedScreenGroup(child, screenFilter, maxItems, seen, results);
        }
    }

    private static void AddUnifiedScreen(
        HmiScreen screen,
        string? screenFilter,
        int maxItems,
        HashSet<string> seen,
        List<HmiScreenInfo> results)
    {
        var name = TryRead(() => screen.Name) ?? string.Empty;
        if (!seen.Add(name))
        {
            return;
        }

        results.Add(ReadUnifiedScreen(screen, screenFilter, maxItems));
    }

    private static HmiScreenInfo ReadUnifiedScreen(HmiScreen screen, string? screenFilter, int maxItems)
    {
        var name = screen.Name;
        var width = ReadLongAttribute(screen, "Width");
        var height = ReadLongAttribute(screen, "Height");
        int? number = ReadLongAttribute(screen, "ScreenNumber") is { } n ? (int)n : null;

        // Count is cheap (composition metadata); reading each item's properties is not. Only pay
        // that cost for screens the caller actually asked to see inside.
        var itemCount = CountOrZero(() => screen.ScreenItems.Count);
        if (!WantsDetail(name, screenFilter))
        {
            return new HmiScreenInfo(name, number, width, height, itemCount, Array.Empty<HmiScreenItemInfo>(), ReadEvents(screen));
        }

        var items = new List<HmiScreenItemInfo>();
        foreach (HmiScreenItemBase item in screen.ScreenItems)
        {
            if (items.Count >= maxItems)
            {
                break;
            }

            items.Add(ReadUnifiedScreenItem(item));
        }

        return new HmiScreenInfo(name, number, width, height, itemCount, items, ReadEvents(screen));
    }

    private static HmiScreenItemInfo ReadUnifiedScreenItem(HmiScreenItemBase item)
    {
        // The CLR type is the unambiguous answer to "what is this object" — the same reasoning
        // ClassifyBlockType uses PLC-side. Geometry goes through generic attribute access instead of
        // a cast per concrete type: there are ~50 of them (widgets, shapes, controls), they do not
        // share one geometry base, and an item type added by a future TIA version still reports.
        var dynamizations = new List<HmiDynamizationInfo>();
        try
        {
            foreach (DynamizationBase dynamization in item.Dynamizations)
            {
                dynamizations.Add(ReadDynamization(dynamization));
            }
        }
        catch (Exception)
        {
            // An item that refuses to enumerate its dynamizations still belongs in the listing —
            // reporting it with none is honest and visibly different from reporting nothing at all.
        }

        // Name comes off the typed property first — it is declared on HmiScreenItemBase, so it is
        // always there — and only falls back to generic attribute access. The reverse order would
        // silently yield an empty name if this TIA version does not expose "Name" as an attribute.
        return new HmiScreenItemInfo(
            TryRead(() => item.Name) ?? ReadStringAttribute(item, "Name") ?? string.Empty,
            item.GetType().Name,
            ReadLongAttribute(item, "Left"),
            ReadLongAttribute(item, "Top"),
            ReadLongAttribute(item, "Width"),
            ReadLongAttribute(item, "Height"),
            dynamizations,
            ReadEvents(item));
    }

    // EventHandlers is declared on each CONCRETE item type with its own composition and its own
    // event enum (HmiButtonEventHandlerComposition/HmiButtonEventType, and so on for ~40 types) —
    // there is no shared base to cast to, so this reads it reflectively. Until this existed, a
    // button reported "no dynamizations" and that read as "not bound" when it meant "not looked at".
    private static IReadOnlyList<HmiEventInfo> ReadEvents(object owner)
    {
        var events = new List<HmiEventInfo>();
        try
        {
            if (owner.GetType().GetProperty("EventHandlers")?.GetValue(owner) is not System.Collections.IEnumerable handlers)
            {
                return events;
            }

            foreach (var handler in handlers)
            {
                if (handler is null)
                {
                    continue;
                }

                var type = handler.GetType();
                var eventType = TryRead(() => type.GetProperty("EventType")?.GetValue(handler)?.ToString()) ?? "?";
                var script = TryReadObject(() => type.GetProperty("Script")?.GetValue(handler));
                var code = script is null
                    ? null
                    : TryRead(() => script.GetType().GetProperty("ScriptCode")?.GetValue(script) as string);

                events.Add(new HmiEventInfo(
                    eventType,
                    !string.IsNullOrWhiteSpace(code),
                    Preview(code)));
            }
        }
        catch (Exception)
        {
            // Same defensive posture as the rest of the walker: an item that will not describe its
            // events still belongs in the listing.
        }

        return events;
    }

    // Script bodies can be long and are not the point of a structural listing; the first line is
    // enough to tell "there is real code here" from "an empty handler was created and never filled".
    private static string? Preview(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var firstLine = code!.Split('\n')[0].Trim();
        return firstLine.Length > 80 ? firstLine.Substring(0, 77) + "..." : firstLine;
    }

    private static object? TryReadObject(Func<object?> read)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static HmiDynamizationInfo ReadDynamization(DynamizationBase dynamization)
    {
        string? tag = null;
        string? plcTag = null;
        if (dynamization is TagDynamization tagDynamization)
        {
            tag = TryRead(() => tagDynamization.Tag);
            plcTag = TryRead(() => tagDynamization.PlcTag);
        }

        var propertyName = TryRead(() => dynamization.PropertyName) ?? string.Empty;
        var kind = TryRead(() => dynamization.DynamizationType.ToString()) ?? dynamization.GetType().Name;
        return new HmiDynamizationInfo(propertyName, kind, tag, plcTag);
    }

    // Classic exposes no screen contents at all: Siemens.Engineering.Hmi.Screen.Screen has no
    // ScreenItems property, and ScreenComposition has no Create — screens arrive only by SimaticML
    // import or master-copy/library copy. So this reports names and nothing more, which is the API's
    // ceiling rather than this walker's.
    private static HmiDeviceInfo ReadClassicDevice(HmiTarget target, string path)
    {
        var screens = new List<HmiScreenInfo>();
        WalkClassicScreenFolder(target.ScreenFolder, screens);

        return new HmiDeviceInfo(
            path,
            target.Name,
            HmiFamily.Classic,
            screens.Count,
            0,
            0,
            0,
            0,
            0,
            0,
            screens);
    }

    private static void WalkClassicScreenFolder(ScreenFolder folder, List<HmiScreenInfo> results)
    {
        foreach (Screen screen in folder.Screens)
        {
            // Classic: no items, and no event model either — Screen exposes neither.
            results.Add(new HmiScreenInfo(screen.Name, null, null, null, 0, Array.Empty<HmiScreenItemInfo>(), Array.Empty<HmiEventInfo>()));
        }

        foreach (ScreenUserFolder child in folder.Folders)
        {
            WalkClassicScreenFolder(child, results);
        }
    }

    private static bool WantsDetail(string screenName, string? screenFilter) =>
        screenFilter is not null
        && (screenFilter == "*" || string.Equals(screenName, screenFilter, StringComparison.OrdinalIgnoreCase));

    // Every read below is defensive on purpose. These are the first calls this project has ever made
    // into the HMI half of the API and they are reflection-derived, not runtime-proven — one
    // property that throws on one item type must not lose the other 47 screens.
    private static int CountOrZero(Func<int> read)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static string? TryRead(Func<string?> read)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ReadStringAttribute(IEngineeringObject item, string attribute)
    {
        try
        {
            return item.GetAttribute(attribute) as string;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static long? ReadLongAttribute(IEngineeringObject item, string attribute)
    {
        try
        {
            return item.GetAttribute(attribute) switch
            {
                null => null,
                var value => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture),
            };
        }
        catch (Exception)
        {
            // Not every item type has every geometry attribute — a TouchArea and a Rectangle do not
            // agree on what they expose. Absent is a fact, not an error.
            return null;
        }
    }

    private static void WalkTagTableGroup(PlcTagTableGroup group, string groupPath, List<TagTableInfo> results)
    {
        foreach (PlcTagTable tagTable in group.TagTables)
        {
            results.Add(new TagTableInfo(tagTable.Name, groupPath));
        }

        foreach (PlcTagTableUserGroup subGroup in group.Groups)
        {
            WalkTagTableGroup(subGroup, $"{groupPath}/{subGroup.Name}", results);
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

    // No safety refusal here — confirmed real, 2026-07-14 (reflecting on the installed DLL):
    // PlcType has no ProgrammingLanguage property at all, so there's nothing for the F-prefix
    // classifier to check. A UDT is a plain data-type declaration, never executable logic.
    public void ExportType(string typeName, string? deviceFilter, string outPath)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(ExportType)}.");
        }

        var matches = FindMatchingTypes(_project, typeName).ToList();
        if (deviceFilter is not null)
        {
            matches = matches.Where(m => m.Path.IndexOf(deviceFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        if (matches.Count == 0)
        {
            throw new TypeNotFoundException(typeName);
        }

        if (matches.Count > 1)
        {
            throw new AmbiguousTypeException(typeName, matches.Select(m => m.Path));
        }

        var type = matches[0].Type;

        if (File.Exists(outPath))
        {
            File.Delete(outPath);
        }

        type.Export(new FileInfo(outPath), Siemens.Engineering.ExportOptions.WithDefaults);

        if (!File.Exists(outPath))
        {
            // Same quirk as ExportBlock — one retry before giving up.
            type.Export(new FileInfo(outPath), Siemens.Engineering.ExportOptions.WithDefaults);
            if (!File.Exists(outPath))
            {
                throw new ExportProducedNoFileException(outPath);
            }
        }
    }

    // No safety refusal — a tag table has no ProgrammingLanguage either, same reasoning as
    // ExportType.
    public void ExportTagTable(string tagTableName, string? deviceFilter, string outPath)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(ExportTagTable)}.");
        }

        var matches = FindMatchingTagTables(_project, tagTableName).ToList();
        if (deviceFilter is not null)
        {
            matches = matches.Where(m => m.Path.IndexOf(deviceFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        if (matches.Count == 0)
        {
            throw new TagTableNotFoundException(tagTableName);
        }

        if (matches.Count > 1)
        {
            throw new AmbiguousTagTableException(tagTableName, matches.Select(m => m.Path));
        }

        var tagTable = matches[0].TagTable;

        if (File.Exists(outPath))
        {
            File.Delete(outPath);
        }

        tagTable.Export(new FileInfo(outPath), Siemens.Engineering.ExportOptions.WithDefaults);

        if (!File.Exists(outPath))
        {
            // Same quirk as ExportBlock/ExportType — one retry before giving up.
            tagTable.Export(new FileInfo(outPath), Siemens.Engineering.ExportOptions.WithDefaults);
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

        try
        {
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
        }
        finally
        {
            // Confirmed real, 2026-07-14: Import() only mutates the in-memory project model —
            // nothing here ever called Project.Save(), so every prior "live-verified" import was
            // only as durable as whichever Portal process happened to stay alive afterward.
            // Killing that process (or the machine restarting) silently discarded it, no error.
            // try/finally so whatever succeeded before a later file's failure still persists.
            SaveProject();
        }

        return imported;
    }

    // PlcBlockComposition.CreateInstanceDB(name, isAutoNumbered, number, instanceOfName) — confirmed
    // real, 2026-07-14 (Siemens.Engineering.xml doc comments, TIA Portal V20 PublicAPI). Grounded
    // for a genuine round-trip gap, not logic generation: an FB imported standalone (no calling
    // context) has nowhere for its own multi-instance Static members (e.g. TON_TIME timers) to
    // resolve their storage, surfacing as "Missing instance DB" on compile. Creating the instance
    // DB invents nothing — no tag, address, or DB number (always auto-numbered, TIA's own choice,
    // never a literal passed in here) — it only gives an already-existing, already-imported block
    // the scaffolding TIA itself would create automatically had the block been placed via a CALL
    // in the UI. Same category as importing a real dependency DB, not S6+ logic generation.
    public BlockInfo CreateInstanceDb(string groupPath, string dbName, string instanceOfName)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(CreateInstanceDb)}.");
        }

        var group = FindGroup(_project, groupPath);

        try
        {
            var db = group.Blocks.CreateInstanceDB(dbName, isAutoNumbered: true, 0, instanceOfName);
            return ToBlockInfo(db, groupPath);
        }
        finally
        {
            SaveProject();
        }
    }

    // Returns imported type names, not BlockInfo — a PlcType has no Number/ProgrammingLanguage to
    // report (confirmed real, 2026-07-14), so BlockInfo's own shape doesn't fit; kept minimal
    // rather than retrofitting BlockInfo with fields that would be meaningless for a UDT.
    public IReadOnlyList<string> ImportTypes(string groupPath, IReadOnlyList<string> files)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(ImportTypes)}.");
        }

        var group = FindTypeGroup(_project, groupPath);
        var imported = new List<string>();

        try
        {
            foreach (var file in files)
            {
                var results = group.Types.Import(new FileInfo(file), Siemens.Engineering.ImportOptions.Override);
                foreach (var item in results)
                {
                    if (item is not PlcType type)
                    {
                        throw new InvalidOperationException(
                            $"Import() of '{file}' returned an unexpected object type: {item?.GetType().FullName ?? "null"}.");
                    }

                    imported.Add(type.Name);
                }
            }
        }
        finally
        {
            SaveProject();
        }

        return imported;
    }

    // Returns imported tag-table names, not BlockInfo — same reasoning as ImportTypes: a
    // PlcTagTable has no Number/ProgrammingLanguage either.
    public IReadOnlyList<string> ImportTagTables(string groupPath, IReadOnlyList<string> files)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(ImportTagTables)}.");
        }

        var group = FindTagTableGroup(_project, groupPath);
        var imported = new List<string>();

        try
        {
            foreach (var file in files)
            {
                var results = group.TagTables.Import(new FileInfo(file), Siemens.Engineering.ImportOptions.Override);
                foreach (var item in results)
                {
                    if (item is not PlcTagTable tagTable)
                    {
                        throw new InvalidOperationException(
                            $"Import() of '{file}' returned an unexpected object type: {item?.GetType().FullName ?? "null"}.");
                    }

                    imported.Add(tagTable.Name);
                }
            }
        }
        finally
        {
            SaveProject();
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

        var result = CompileDeviceItem(candidates[0].Item, candidates[0].Path);
        SaveProject();
        return result;
    }

    /// <summary>
    /// Compiles the HMI device. Separate from <see cref="Compile"/> because that one resolves its
    /// target through <c>FindPlcDeviceItems</c> and so cannot see an HMI device at all — the same
    /// PLC-only-by-construction blind spot the read walker had.
    ///
    /// The point of this is the open question in <c>openness-hmi-write-api.md</c> §4c: `Validate()`
    /// is measurably shallow, so is a device compile the real gate instead? Whatever this reports is
    /// the answer, including "it reports nothing".
    /// </summary>
    public CompileResult CompileHmi(string? deviceFilter)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(CompileHmi)}.");
        }

        var candidates = new List<(DeviceItem Item, string Path)>();
        foreach (Device device in _project.Devices)
        {
            foreach (DeviceItem item in device.DeviceItems)
            {
                CollectHmiDeviceItems(item, device.Name, candidates);
            }
        }

        if (deviceFilter is not null)
        {
            candidates = candidates.Where(c => c.Path.IndexOf(deviceFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        if (candidates.Count != 1)
        {
            throw new DeviceNotFoundException(deviceFilter);
        }

        var result = CompileHmiTarget(candidates[0].Item, candidates[0].Path);
        SaveProject();
        return result;
    }

    /// <summary>
    /// Finds the compilable for an HMI device by walking OUTWARD from the software-bearing item.
    ///
    /// Measured 2026-08-08: <c>GetService&lt;ICompilable&gt;()</c> returns **null** on the device item
    /// that actually carries the <c>HmiSoftware</c> — and `HmiSoftware` itself is not an
    /// <c>IEngineeringServiceProvider</c> at all, so it cannot supply one either. The PLC assumption
    /// baked into <c>CompileDeviceItem</c> (item first, its software second) simply does not hold
    /// here. So this climbs the ancestor chain to the station and finally tries the
    /// <c>Device</c> itself, reporting exactly what it tried when nothing is compilable.
    /// </summary>
    private static CompileResult CompileHmiTarget(DeviceItem softwareItem, string path)
    {
        var tried = new List<string>();

        for (IEngineeringObject? current = softwareItem; current is not null; current = TryReadObject(() => current.Parent) as IEngineeringObject)
        {
            var label = current.GetType().Name + (current is DeviceItem di ? $" '{TryRead(() => di.Name)}'" : string.Empty);
            tried.Add(label);

            if (current is not IEngineeringServiceProvider provider)
            {
                continue;
            }

            var compilable = TryReadObject(() => provider.GetService<ICompilable>()) as ICompilable;
            if (compilable is not null)
            {
                return RunCompile(compilable);
            }
        }

        throw new InvalidOperationException(
            $"No ICompilable service anywhere in the device tree for HMI '{path}'. Tried, outward from the software-bearing item: {string.Join(" -> ", tried)}. " +
            "Note HmiSoftware is not an IEngineeringServiceProvider, so it can never supply one.");
    }

    private static void CollectHmiDeviceItems(DeviceItem item, string parentPath, List<(DeviceItem, string)> found)
    {
        var path = $"{parentPath}/{item.Name}";
        var software = item.GetService<SoftwareContainer>()?.Software;
        if (software is HmiSoftware || software is HmiTarget)
        {
            found.Add((item, path));
        }

        foreach (DeviceItem child in item.DeviceItems)
        {
            CollectHmiDeviceItems(child, path, found);
        }
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
        // this GetService<ICompilable>() call is confirmed live, 2026-07-10, to actually return
        // a working per-block compiler — not documented anywhere, found by reflecting on the
        // installed DLL then testing live rather than assuming device-level compile was the only
        // granularity available (see docs/notes/openness-quirks.md).
        var compilable = block.GetService<ICompilable>()
            ?? throw new InvalidOperationException(
                $"Block '{blockName}' does not expose an ICompilable service — expected one to be available (confirmed live on other blocks, see docs/notes/openness-quirks.md).");

        var result = RunCompile(compilable);
        SaveProject();
        return result;
    }

    // Mirrors CompileBlock exactly, minus the safety check (no ProgrammingLanguage on PlcType —
    // see ExportType's own doc comment). Whether PlcType.GetService<ICompilable>() actually
    // returns a working compiler the way PlcBlock's does is unconfirmed until live-verified —
    // structurally plausible (PlcType implements IEngineeringServiceProvider too), not assumed.
    public CompileResult CompileType(string typeName, string? deviceFilter)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(CompileType)}.");
        }

        var matches = FindMatchingTypes(_project, typeName).ToList();
        if (deviceFilter is not null)
        {
            matches = matches.Where(m => m.Path.IndexOf(deviceFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        if (matches.Count == 0)
        {
            throw new TypeNotFoundException(typeName);
        }

        if (matches.Count > 1)
        {
            throw new AmbiguousTypeException(typeName, matches.Select(m => m.Path));
        }

        var type = matches[0].Type;
        var compilable = type.GetService<ICompilable>()
            ?? throw new InvalidOperationException(
                $"Type '{typeName}' does not expose an ICompilable service.");

        var result = RunCompile(compilable);
        SaveProject();
        return result;
    }

    // Confirmed real via Siemens's own Siemens.Engineering.xml doc comments (2026-07-13):
    // PlcBlock.Delete() — "Deletes this instance.", a plain no-argument instance method on the
    // exact same PlcBlock type Export/CompileBlock already resolve. BlockInfo is captured before
    // Delete() runs (nothing left to read from a deleted COM object afterward) and always
    // returned, confirmed or not — this is the first irreversible operation this gateway exposes,
    // so `confirm=false` deliberately resolves and reports without touching anything, mirroring
    // the dry-run precedent this project already applies elsewhere to destructive actions.
    public BlockInfo DeleteBlock(string blockName, string? deviceFilter, bool confirm)
    {
        if (_project is null)
        {
            throw new InvalidOperationException($"{nameof(OpenProject)} must be called before {nameof(DeleteBlock)}.");
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

        var (block, path) = matches[0];
        var language = block.ProgrammingLanguage.ToString();
        if (SafetyClassifier.IsSafety(language))
        {
            throw new SafetyContentRefusedException(blockName, language);
        }

        var info = ToBlockInfo(block, path);

        if (confirm)
        {
            block.Delete();
            SaveProject();
        }

        return info;
    }

    private static CompileResult RunCompile(ICompilable compilable)
    {
        var result = compilable.Compile();
        var messages = new List<CompileMessage>();
        foreach (CompilerResultMessage message in result.Messages)
        {
            CollectMessages(message, messages);
        }

        return new CompileResult(MapCompileState(result.State), result.ErrorCount, result.WarningCount, messages);
    }

    // CompilerResultMessage.Messages is a nested tree, not a flat list — confirmed by reflecting
    // on the installed DLL, 2026-07-10: the top-level message is often just a rollup
    // ("Compiling finished (errors: N; warnings: 0)") with empty Description, and the actual
    // per-error text lives in child Messages, arbitrarily deep. Missing this meant every compile
    // failure reported a count with no way to see why — recurse and keep every node so nothing is
    // silently dropped.
    private static void CollectMessages(CompilerResultMessage message, List<CompileMessage> results)
    {
        results.Add(new CompileMessage(MapCompileState(message.State), message.Description, message.Path));
        foreach (CompilerResultMessage child in message.Messages)
        {
            CollectMessages(child, results);
        }
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

        // sanity-check's own device compiles are read-as-diagnostic in intent, but Compile() has
        // the same real IsConsistent-flipping side effect here as everywhere else it's called —
        // save once at the end so that side effect doesn't silently evaporate either.
        SaveProject();

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

    // Mirrors FindMatchingBlocks/FindBlocksInDeviceItem/FindBlocksInGroup exactly, walking
    // PlcSoftware.TypeGroup/PlcTypeGroup.Types/.Groups instead of .BlockGroup/PlcBlockGroup.
    // Blocks/.Groups — confirmed real, 2026-07-14 (reflecting on the installed DLL): the same
    // recursive group shape, just for PLC data types (UDTs) instead of blocks.
    private static IEnumerable<(PlcType Type, string Path)> FindMatchingTypes(Project project, string typeName)
    {
        foreach (Device device in project.Devices)
        {
            foreach (DeviceItem item in device.DeviceItems)
            {
                foreach (var match in FindTypesInDeviceItem(item, device.Name, typeName))
                {
                    yield return match;
                }
            }
        }
    }

    private static IEnumerable<(PlcType Type, string Path)> FindTypesInDeviceItem(DeviceItem item, string parentPath, string typeName)
    {
        var path = $"{parentPath}/{item.Name}";

        var softwareContainer = item.GetService<SoftwareContainer>();
        if (softwareContainer?.Software is PlcSoftware plcSoftware)
        {
            foreach (var match in FindTypesInGroup(plcSoftware.TypeGroup, path, typeName))
            {
                yield return match;
            }
        }

        foreach (DeviceItem child in item.DeviceItems)
        {
            foreach (var match in FindTypesInDeviceItem(child, path, typeName))
            {
                yield return match;
            }
        }
    }

    private static IEnumerable<(PlcType Type, string Path)> FindTypesInGroup(PlcTypeGroup group, string groupPath, string typeName)
    {
        foreach (PlcType type in group.Types)
        {
            if (type.Name == typeName)
            {
                yield return (type, groupPath);
            }
        }

        foreach (PlcTypeUserGroup subGroup in group.Groups)
        {
            foreach (var match in FindTypesInGroup(subGroup, $"{groupPath}/{subGroup.Name}", typeName))
            {
                yield return match;
            }
        }
    }

    // Mirrors FindMatchingTypes/FindTypesInDeviceItem/FindTypesInGroup exactly, walking
    // PlcSoftware.TagTableGroup/PlcTagTableGroup.TagTables/.Groups instead of
    // .TypeGroup/PlcTypeGroup.Types/.Groups — confirmed real, 2026-07-14 (reflecting on the
    // installed DLL): the same recursive group shape, for PLC tag tables instead of UDTs.
    private static IEnumerable<(PlcTagTable TagTable, string Path)> FindMatchingTagTables(Project project, string tagTableName)
    {
        foreach (Device device in project.Devices)
        {
            foreach (DeviceItem item in device.DeviceItems)
            {
                foreach (var match in FindTagTablesInDeviceItem(item, device.Name, tagTableName))
                {
                    yield return match;
                }
            }
        }
    }

    private static IEnumerable<(PlcTagTable TagTable, string Path)> FindTagTablesInDeviceItem(DeviceItem item, string parentPath, string tagTableName)
    {
        var path = $"{parentPath}/{item.Name}";

        var softwareContainer = item.GetService<SoftwareContainer>();
        if (softwareContainer?.Software is PlcSoftware plcSoftware)
        {
            foreach (var match in FindTagTablesInGroup(plcSoftware.TagTableGroup, path, tagTableName))
            {
                yield return match;
            }
        }

        foreach (DeviceItem child in item.DeviceItems)
        {
            foreach (var match in FindTagTablesInDeviceItem(child, path, tagTableName))
            {
                yield return match;
            }
        }
    }

    private static IEnumerable<(PlcTagTable TagTable, string Path)> FindTagTablesInGroup(PlcTagTableGroup group, string groupPath, string tagTableName)
    {
        foreach (PlcTagTable tagTable in group.TagTables)
        {
            if (tagTable.Name == tagTableName)
            {
                yield return (tagTable, groupPath);
            }
        }

        foreach (PlcTagTableUserGroup subGroup in group.Groups)
        {
            foreach (var match in FindTagTablesInGroup(subGroup, $"{groupPath}/{subGroup.Name}", tagTableName))
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
    /// The shared front half of <see cref="FindGroup"/>, <see cref="FindTypeGroup"/> and
    /// <see cref="FindTagTableGroup"/> — three ~50-line functions that were verbatim copies of each
    /// other apart from their last five lines (deduplicated 2026-08-05).
    ///
    /// Resolves a "list"-style Path (e.g. "S7-1200 G2 station_2/JOB9002_PLC/Control") as far as the
    /// PlcSoftware it names, and reports how many leading segments that consumed. There's no fixed
    /// boundary between the device-item part of the path and the group-name part, so the walk is
    /// greedy: descend matching DeviceItem names for as long as they match, then hand the remaining
    /// segments back to the caller, which knows which composition tree they name. Only that tail
    /// differs — PlcBlockGroup, PlcTypeGroup and PlcTagTableGroup share no base type carrying a
    /// `Groups` collection, so folding the tail in too would cost more parameterisation than it saves.
    /// </summary>
    private static (PlcSoftware Software, string[] Segments, int Index) ResolvePlcSoftware(Project project, string groupPath)
    {
        var segments = groupPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw GroupNotFoundException.EmptyPath();
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
            throw GroupNotFoundException.NoDeviceItem(groupPath);
        }

        var softwareContainer = currentItem.GetService<SoftwareContainer>();
        if (softwareContainer?.Software is not PlcSoftware plcSoftware)
        {
            throw new NotAPlcSoftwareContainerException(string.Join("/", segments, 0, i));
        }

        return (plcSoftware, segments, i);
    }

    private static PlcBlockGroup FindGroup(Project project, string groupPath)
    {
        var (plcSoftware, segments, i) = ResolvePlcSoftware(project, groupPath);

        PlcBlockGroup group = plcSoftware.BlockGroup;
        for (; i < segments.Length; i++)
        {
            group = group.Groups.Cast<PlcBlockUserGroup>().FirstOrDefault(g => g.Name == segments[i])
                ?? throw GroupNotFoundException.GroupMissing("Block", segments[i], string.Join("/", segments, 0, i));
        }

        return group;
    }

    // PlcSoftware.TypeGroup/PlcTypeGroup is a separate composition tree, not a view onto
    // .BlockGroup/PlcBlockGroup (confirmed real, 2026-07-14: PlcTypeGroup has no relation to
    // PlcBlockGroup beyond both hanging off the same PlcSoftware).
    private static PlcTypeGroup FindTypeGroup(Project project, string groupPath)
    {
        var (plcSoftware, segments, i) = ResolvePlcSoftware(project, groupPath);

        PlcTypeGroup group = plcSoftware.TypeGroup;
        for (; i < segments.Length; i++)
        {
            group = group.Groups.Cast<PlcTypeUserGroup>().FirstOrDefault(g => g.Name == segments[i])
                ?? throw GroupNotFoundException.GroupMissing("Type", segments[i], string.Join("/", segments, 0, i));
        }

        return group;
    }

    private static PlcTagTableGroup FindTagTableGroup(Project project, string groupPath)
    {
        var (plcSoftware, segments, i) = ResolvePlcSoftware(project, groupPath);

        PlcTagTableGroup group = plcSoftware.TagTableGroup;
        for (; i < segments.Length; i++)
        {
            group = group.Groups.Cast<PlcTagTableUserGroup>().FirstOrDefault(g => g.Name == segments[i])
                ?? throw GroupNotFoundException.GroupMissing("Tag table", segments[i], string.Join("/", segments, 0, i));
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

    // Confirmed real, 2026-07-14: no code path in this gateway ever called Project.Save() before
    // this fix — Import()/Delete()/Compile() only mutate the in-memory project model. Whatever
    // Portal process ends up holding that in-memory state is the only thing keeping it alive;
    // closing that process (a taskkill, a crash, the machine restarting) silently discards it with
    // no error, no warning. Caught live: a UDT imported and confirmed compiling earlier in the same
    // session had vanished from disk the moment the Portal process holding it was closed. Every
    // state-mutating gateway method now calls this once it's done.
    private void SaveProject()
    {
        _project!.Save();
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
