using System;
using System.Collections.Generic;
using OpennessCli.Model;

namespace OpennessCli.Openness;

public interface IOpennessGateway : IDisposable
{
    /// <param name="preferProjectIdentifier">Optional hint: attach to a running Portal that already
    /// has this project open, rather than whichever process happens to be first. Readable without
    /// attaching, so it costs nothing; falls back to the first process when absent or unmatched.
    /// Narrows WHICH process is attached, never whether one is.</param>
    void Connect(TimeSpan timeout, string? preferProjectIdentifier = null);

    /// <summary>
    /// Read-only Portal-process diagnostic for `portal-status`: enumerates every running
    /// <c>Siemens.Automation.Portal.exe</c> via <c>TiaPortal.GetProcesses()</c> and maps each to a
    /// Siemens-free <see cref="PortalProcessInfo"/>, stamping <c>MarkedByThisTool</c> from
    /// <c>LaunchedInstanceRegistry</c>. Deliberately does NOT go through <see cref="Connect"/> —
    /// it never calls <c>Attach()</c>, <c>new TiaPortal(...)</c>, or <c>Projects.Open()</c>, so it
    /// can't trigger the first-connect dialog or add to the very pileup it is meant to diagnose.
    /// <c>Id</c>/<c>ProjectPath</c>/<c>Mode</c>/<c>AcquisitionTime</c> are all readable off
    /// <c>TiaPortalProcess</c> without attaching (docs/notes/openness-api-surface-v20.md).
    /// </summary>
    IReadOnlyList<PortalProcessInfo> EnumeratePortalProcesses();

    void OpenProject(string projectIdentifier, TimeSpan timeout);

    IReadOnlyList<BlockInfo> EnumerateBlocks();

    /// <summary>
    /// Enumerates every PLC tag table in the project — a distinct object type from blocks/DBs/UDTs
    /// (<c>PlcSoftware.TagTableGroup</c>, confirmed real 2026-07-14 while grounding `PlantAutoControl`'s
    /// own dependency closure: some of its referenced tags are bare, single-component Access
    /// references that never appear in <see cref="EnumerateBlocks"/>'s own output — a genuine
    /// PLC tag, not a DB member). No Number/Language/Safety concept exists for a tag table at all.
    /// </summary>
    IReadOnlyList<TagTableInfo> EnumerateTagTables();

    /// <summary>
    /// Read-only walk of every HMI device in the project. Exists because the rest of this gateway
    /// is PLC-only by construction — every other walker filters
    /// <c>SoftwareContainer.Software is PlcSoftware</c>, so an HMI device was previously invisible
    /// to this tool rather than merely unsupported.
    ///
    /// The two HMI families are handled differently because the API forces it
    /// (docs/notes/openness-hmi-api-survey.md): Unified exposes a full typed screen-item tree and
    /// per-property dynamizations but has NO screen export, so reading the live model is the only
    /// way to observe a screen's contents at all; classic exposes no screen contents whatsoever, so
    /// its screens enumerate by name only.
    ///
    /// Strictly read-only: no Create, no SetAttribute, no import, no compile. Nothing here writes.
    /// </summary>
    /// <param name="screenFilter">When null, screens are summarised (name/size/item count) without
    /// reading their items — reading every item on every screen is slow enough to matter. When set,
    /// items and dynamizations are read for screens whose name matches (ordinal, case-insensitive),
    /// or for all screens when it is "*".</param>
    /// <param name="maxItems">Cap on items read per screen, so a pathological screen cannot stall a
    /// survey run. Truncation is visible: <see cref="Model.HmiScreenInfo.ItemCount"/> is the true
    /// count, which the caller compares against the number of items actually returned.</param>
    IReadOnlyList<HmiDeviceInfo> EnumerateHmi(string? screenFilter, int maxItems);

    /// <summary>
    /// The metamodel behind <see cref="EnumerateHmi"/>: for each screen-item type observed, every
    /// attribute the API declares, with its access mode and create-relevance, plus the list of types
    /// the ScreenItems composition will accept.
    ///
    /// This exists because WinCC Unified has **no screen export** — there is no XML or JSON document
    /// of a screen to read anywhere, in the API, the project folder, or the compiled runtime image.
    /// What replaces it is that Openness describes itself: <c>GetAttributeInfos()</c> and
    /// <c>GetCreationInfos()</c> are the authoritative schema, and unlike a sample document they
    /// state which attributes are Mandatory at creation rather than leaving it to be inferred.
    ///
    /// Read-only, like the rest of this walker — reporting that an attribute is writable is not
    /// writing to it.
    /// </summary>
    IReadOnlyList<HmiSchemaReport> EnumerateHmiSchema(string screenFilter, int maxItems);

    /// <summary>
    /// Creates one screen (and optionally a few static items on it), runs <c>Validate()</c>, and
    /// saves. **The only writing method in the HMI half of this interface** — kept separate from the
    /// walker on purpose so that reading can never become writing by accident.
    ///
    /// Refuses on an existing screen name: this creates, never overwrites. Refuses on an ambiguous
    /// device rather than guessing, because writing to the wrong panel is not undone by re-reading.
    /// </summary>
    HmiCreateScreenResult CreateHmiScreen(string screenName, long width, long height, IReadOnlyList<string> itemTypes);

    /// <summary>
    /// Modifies an existing screen: sets attributes on the screen or its items, and creates event
    /// handlers. Separate from <see cref="CreateHmiScreen"/> because the risk is different —
    /// creating touches nothing anyone depends on; editing changes something that already works.
    ///
    /// Attribute values are coerced using the target's own <c>GetAttributeInfos</c> (enum parse or
    /// <c>Convert.ChangeType</c>), and a read-only attribute is refused rather than silently
    /// ignored. Targets and event names that do not exist are hard errors: a skipped edit and a
    /// successful one look identical in the output otherwise.
    /// </summary>
    /// <summary>
    /// Compiles the HMI device. <see cref="CompileResult"/> is the same shape the PLC path returns,
    /// so the diagnostics render identically. Exists because <c>Compile</c> resolves through
    /// PLC-only device discovery and cannot target an HMI at all.
    /// </summary>
    CompileResult CompileHmi(string? deviceFilter);

    HmiEditScreenResult EditHmiScreen(
        string screenName,
        IReadOnlyList<(string Target, string Attribute, string Value)> sets,
        IReadOnlyList<(string Target, string EventType, string? Script)> events,
        IReadOnlyList<(string Target, string Property, string Tag)> binds,
        // Screen-scoped deletes — items, bindings and event handlers hang off a SCREEN, not off
        // HmiSoftware, so `hmi-delete` (which resolves device-level compositions) cannot reach them.
        IReadOnlyList<(string What, string Target, string? Detail)> deletes,
        // Item types to add to the existing screen. Each is attempted independently and a failure is
        // REPORTED rather than aborting the rest — the breadth sweep needs a per-type verdict.
        IReadOnlyList<string> addItems,
        // Non-tag dynamization kinds (Script/Flashing/Expression/ResourceList/TagParameter), each
        // attempted independently so one refusal does not hide the others.
        IReadOnlyList<(string Target, string Property, string Kind)> bindKinds,
        // Mapping-table maintenance, applied BEFORE the creates below so a re-run lands on a known
        // state: Openness has no transaction and the entries composition has no upsert.
        IReadOnlyList<(string Target, string Property)> mapClears,
        // Mapping-table entry specs — "<EntryType>[;<Attr>=<Value>]..." — driving
        // TagDynamization -> ValueConverter -> MappingTable -> Entries. This is the second route to
        // flashing (openness-hmi-write-api.md §4m/§4n), reached through the one dynamization kind
        // that is not gated on the target property's type.
        IReadOnlyList<(string Target, string Property, string EntrySpec)> maps);

    /// <summary>
    /// Creates an HMI tag (and its table if absent) to serve as a dynamization bind target.
    /// Minimal by design — name, table, data type — since an internal tag is enough to test whether
    /// a binding resolves.
    /// </summary>
    string CreateHmiTag(string tagName, string tableName, string dataType);

    /// <summary>
    /// Metamodel-driven create/delete/census over any composition on <c>HmiSoftware</c>, resolved by
    /// name at runtime. One trio instead of a subcommand per kind — there are 80 creatable kinds and
    /// 184 deletable types, and the API describes itself well enough that hand-writing wrappers
    /// would be transcription rather than engineering.
    ///
    /// <see cref="DeleteHmiObject"/> enforces the probe-artifact prefix in code unless explicitly
    /// overridden: it is the only destructive path here and it runs unattended.
    /// </summary>
    string CreateHmiObject(string kind, string name, string? parent);

    string DeleteHmiObject(string kind, string name, bool allowAnyName);

    IReadOnlyList<HmiObjectInfo> InventoryHmi(string? kindFilter);

    /// <summary>
    /// Sets attributes on any object in any composition — the counterpart to
    /// <see cref="CreateHmiObject"/>, since most objects are useless bare. <c>texts</c> takes the
    /// MultilingualText path (alarm texts and the like), which can only write into a language the
    /// project already has.
    /// </summary>
    IReadOnlyList<string> SetHmiObjectAttributes(
        string kind,
        string name,
        IReadOnlyList<(string Attribute, string Value)> sets,
        IReadOnlyList<(string Attribute, string Value)> texts);

    /// <summary>
    /// Exports the named block to <paramref name="outPath"/>. Refuses (throws
    /// <see cref="SafetyContentRefusedException"/>) before calling Export() at all if the block
    /// classifies as safety. <paramref name="deviceFilter"/> disambiguates when the same block
    /// name/number exists under more than one device (common — see docs/notes/stage-gates.md).
    /// </summary>
    void ExportBlock(string blockName, string? deviceFilter, string outPath);

    /// <summary>
    /// Exports the named PLC data type (UDT) to <paramref name="outPath"/>. Same
    /// <paramref name="deviceFilter"/> disambiguation as <see cref="ExportBlock"/>. No safety
    /// refusal here — confirmed real, 2026-07-14: <c>PlcType</c> has no <c>ProgrammingLanguage</c>
    /// property at all (reflected on the installed DLL), so there's nothing for the F-prefix
    /// safety classifier to check; a UDT is a plain data-type declaration, never executable logic.
    /// </summary>
    void ExportType(string typeName, string? deviceFilter, string outPath);

    /// <summary>
    /// Exports the named PLC tag table to <paramref name="outPath"/>. Same
    /// <paramref name="deviceFilter"/> disambiguation as <see cref="ExportBlock"/>. No safety
    /// refusal — a tag table has no <c>ProgrammingLanguage</c> either, same reasoning as
    /// <see cref="ExportType"/>.
    /// </summary>
    void ExportTagTable(string tagTableName, string? deviceFilter, string outPath);

    /// <summary>
    /// Imports <paramref name="files"/> into the block group at <paramref name="groupPath"/>
    /// (format: "&lt;device&gt;/&lt;group&gt;/.../&lt;group&gt;", matching the Path shown by
    /// `list`). No generic "import wherever it goes" exists on the Siemens side, so an explicit
    /// target is required here too.
    /// </summary>
    IReadOnlyList<BlockInfo> ImportBlocks(string groupPath, IReadOnlyList<string> files);

    /// <summary>
    /// Imports <paramref name="files"/> as PLC data types (UDTs) into the type group at
    /// <paramref name="groupPath"/> — same path format as <see cref="ImportBlocks"/>, but a
    /// distinct composition tree (<c>PlcTypeGroup.Types</c>, not <c>PlcBlockGroup.Blocks</c>).
    /// Returns imported type names, not <see cref="BlockInfo"/> — a UDT has no Number/
    /// ProgrammingLanguage to report.
    /// </summary>
    IReadOnlyList<string> ImportTypes(string groupPath, IReadOnlyList<string> files);

    /// <summary>
    /// Creates a new instance DB named <paramref name="dbName"/> in the block group at
    /// <paramref name="groupPath"/>, backing an instance of the FB named
    /// <paramref name="instanceOfName"/> (<c>PlcBlockComposition.CreateInstanceDB</c>, confirmed
    /// real 2026-07-14). Always auto-numbered — never invents a literal DB number (CLAUDE.md hard
    /// rule 3). For an FB imported standalone with no calling context, this supplies the storage
    /// its own multi-instance Static members (e.g. TON_TIME timers) need to compile.
    /// </summary>
    BlockInfo CreateInstanceDb(string groupPath, string dbName, string instanceOfName);

    /// <summary>
    /// Imports <paramref name="files"/> as PLC tag tables into the tag-table group at
    /// <paramref name="groupPath"/> — same path format as <see cref="ImportBlocks"/>, but a
    /// distinct composition tree (<c>PlcTagTableGroup.TagTables</c>). Returns imported tag-table
    /// names, same reasoning as <see cref="ImportTypes"/>.
    /// </summary>
    IReadOnlyList<string> ImportTagTables(string groupPath, IReadOnlyList<string> files);

    /// <summary>Compiles the PLC software under <paramref name="deviceFilter"/> (or the project's only PLC device, if unambiguous).</summary>
    CompileResult Compile(string? deviceFilter);

    /// <summary>
    /// Resolves the named block exactly like <see cref="ExportBlock"/>/<see cref="CompileBlock"/>
    /// (same <paramref name="deviceFilter"/> disambiguation, same safety refusal), then — only
    /// when <paramref name="confirm"/> is <c>true</c> — deletes it via <c>PlcBlock.Delete()</c>.
    /// Always returns the matched block's own info, confirmed or not, so a caller can show what
    /// was (or would be) deleted either way. This is the first genuinely irreversible operation
    /// this gateway exposes (no undo, and this tool has no way to recreate a deleted block) —
    /// <paramref name="confirm"/><c>=false</c> is a dry-run preview, deliberately not deleting
    /// anything, unlike every other command here.
    /// </summary>
    BlockInfo DeleteBlock(string blockName, string? deviceFilter, bool confirm);

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
    /// Compiles a single named PLC data type (UDT) via its own <c>ICompilable</c> service —
    /// mirrors <see cref="CompileBlock"/>, minus the safety check (no <c>ProgrammingLanguage</c>
    /// on <c>PlcType</c> — see <see cref="ExportType"/>'s own doc comment).
    /// </summary>
    CompileResult CompileType(string typeName, string? deviceFilter);

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
            "accept the dialog if it's there, then re-run. Not retrying automatically, to avoid piling up " +
            "redundant Portal processes while the state is unclear.")
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

public sealed class TypeNotFoundException : Exception
{
    public TypeNotFoundException(string typeName)
        : base($"No PLC data type (UDT) named '{typeName}' found in the project.")
    {
    }
}

public sealed class AmbiguousTypeException : Exception
{
    public AmbiguousTypeException(string typeName, IEnumerable<string> paths)
        : base($"Type '{typeName}' exists in more than one place: {string.Join(", ", paths)}. Pass --device to disambiguate.")
    {
    }
}

public sealed class TagTableNotFoundException : Exception
{
    public TagTableNotFoundException(string tagTableName)
        : base($"No PLC tag table named '{tagTableName}' found in the project.")
    {
    }
}

public sealed class AmbiguousTagTableException : Exception
{
    public AmbiguousTagTableException(string tagTableName, IEnumerable<string> paths)
        : base($"Tag table '{tagTableName}' exists in more than one place: {string.Join(", ", paths)}. Pass --device to disambiguate.")
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

/// <summary>
/// Thrown when a <c>--group &lt;device&gt;/&lt;path&gt;</c> argument names a group that isn't there —
/// the part of the path *after* the device name, which <see cref="DeviceNotFoundException"/> already
/// covers. Added 2026-08-05 (audit F-09): these sites threw a bare <c>InvalidOperationException</c>,
/// so the commonest user mistake this tool has reported as exit 5 (an internal fault) rather than 7.
///
/// The messages point at <c>openness-cli list</c> deliberately. A <c>--group</c> path must match that
/// command's own Path column verbatim — a device item's real name can itself contain spaces and an
/// embedded article number as one literal string — and a shortened guess is precisely how this error
/// gets produced, so naming the cure in the message is most of the value.
/// </summary>
public sealed class GroupNotFoundException : Exception
{
    private GroupNotFoundException(string message)
        : base(message)
    {
    }

    public static GroupNotFoundException EmptyPath() =>
        new("Empty --group path. Pass --group <device>/<path>, copied verbatim from `openness-cli list`'s Path column.");

    public static GroupNotFoundException NoDeviceItem(string groupPath) =>
        new($"No device item found under '{groupPath}'. Copy the Path column from `openness-cli list` verbatim — " +
            "a device item's real name can contain spaces and an article number as one literal string, and a " +
            "shortened guess fails here.");

    public static GroupNotFoundException GroupMissing(string kind, string groupName, string parentPath) =>
        new($"{kind} group '{groupName}' not found under '{parentPath}'. Check it against `openness-cli list`'s Path column.");
}

/// <summary>
/// Thrown when a <c>--group</c> path resolves to a device item that carries no PLC software — an HMI
/// station, a rack, or a module rather than the PLC itself. Same user-error class as
/// <see cref="GroupNotFoundException"/>, kept separate because the correction is different: not a typo
/// in the path, but the wrong device item named along it.
/// </summary>
public sealed class HmiUnknownKindException : Exception
{
    public HmiUnknownKindException(string kind, IReadOnlyList<string> available)
        : base($"'{kind}' is not a composition on HmiSoftware." +
               (available.Count > 0 ? $" Available kinds: {string.Join(", ", available)}." : string.Empty))
    {
    }
}

public sealed class HmiKindNotCreatableException : Exception
{
    public HmiKindNotCreatableException(string kind)
        : base($"The '{kind}' composition has no string-argument Create — objects of this kind cannot be created through Openness (script modules and text lists are import-only, for example).")
    {
    }
}

public sealed class HmiKindNotDeletableException : Exception
{
    public HmiKindNotDeletableException(string kind, string typeName)
        : base($"'{typeName}' (in {kind}) declares no Delete() — this kind cannot be deleted through Openness. System tags, system text lists and the audit trail are like this by design.")
    {
    }
}

public sealed class HmiObjectAlreadyExistsException : Exception
{
    public HmiObjectAlreadyExistsException(string kind, string name)
        : base($"A {kind} object named '{name}' already exists. This command creates and never overwrites — choose a different name, or delete the existing one first.")
    {
    }
}

public sealed class HmiObjectNotFoundException : Exception
{
    public HmiObjectNotFoundException(string kind, string name)
        : base($"No {kind} object named '{name}' was found. List what exists with `openness-cli hmi-inventory <project> --kind {kind}`.")
    {
    }
}

/// <summary>
/// The one place the data boundary is enforced in CODE rather than procedurally. Everything else in
/// this tool is general-purpose and relies on a recorded scope; deletion runs unattended, so a
/// mistyped name must not be able to remove a real screen, tag or alarm.
/// </summary>
public sealed class HmiRefusedToDeleteRealObjectException : Exception
{
    public HmiRefusedToDeleteRealObjectException(string name, string requiredPrefix)
        : base($"Refusing to delete '{name}': this tool only deletes its own probe artifacts, whose names start with '{requiredPrefix}'. " +
               "Pass --allow-any-name to override, which is never correct for unattended work and must be a deliberate, supervised choice.")
    {
    }
}

/// <summary>
/// A nested <c>--set</c> target named a step that does not resolve. Distinct from
/// <see cref="HmiScreenItemNotFoundException"/>, which is about the FIRST segment: this one means the
/// item was found and the path then went somewhere that is not there — a dynamization that was never
/// created, a property that is null, or an index past the end of a composition.
/// </summary>
public sealed class HmiTargetPathNotResolvableException : Exception
{
    public HmiTargetPathNotResolvableException(string path, string segment, string reason)
        : base($"Cannot resolve '{segment}' in target path '{path}': {reason} " +
               "A nested target steps through dynamizations and engineering objects, e.g. " +
               "'HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0]'.")
    {
    }
}

/// <summary>
/// The mapping-table route was asked for on something that has no mapping table. Only a
/// <c>TagDynamization</c> carries a <c>ValueConverter</c>, and only a value converter carries a
/// mapping table — so this is the error for "bind it first" and for "that binding is the wrong kind".
/// </summary>
public sealed class HmiMappingTableNotAvailableException : Exception
{
    public HmiMappingTableNotAvailableException(string typeName, string propertyName, string reason)
        : base($"No mapping table is reachable on {typeName}.{propertyName}: {reason}. " +
               "A mapping table hangs off a TagDynamization's ValueConverter, so bind the property to a tag first (--bind).")
    {
    }
}

public sealed class HmiUnknownMappingEntryTypeException : Exception
{
    public HmiUnknownMappingEntryTypeException(string entryType)
        : base($"'{entryType}' is not a mapping-table entry type. Use Simple, Range, Bitmask or Base " +
               "(or their MappingTableEntry* CLR names), or 'bits:SingleBit'/'bits:MultiBit' for the " +
               "non-generic Create(BitDynamizationType) overload, which creates a whole set of bitmask entries at once.")
    {
    }
}

public sealed class HmiDynamizationsNotSupportedException : Exception
{
    public HmiDynamizationsNotSupportedException(string typeName)
        : base($"'{typeName}' exposes no Dynamizations composition, so no property on it can be bound to a tag.")
    {
    }
}

public sealed class HmiTagAlreadyExistsException : Exception
{
    public HmiTagAlreadyExistsException(string name)
        : base($"An HMI tag named '{name}' already exists. This command creates tags and never modifies an existing one — choose a different name.")
    {
    }
}

public sealed class HmiScreenNotFoundException : Exception
{
    public HmiScreenNotFoundException(string name)
        : base($"No screen named '{name}' was found on the HMI device (screens inside screen groups were searched too). This command edits existing screens and never creates one — check the name with `openness-cli hmi <project>`.")
    {
    }
}

public sealed class HmiScreenItemNotFoundException : Exception
{
    public HmiScreenItemNotFoundException(string itemName, string screenName)
        : base($"Screen '{screenName}' has no item named '{itemName}'. Use 'Screen' to target the screen itself, or list the item names with `openness-cli hmi <project> --screen {screenName}`.")
    {
    }
}

public sealed class HmiUnknownAttributeException : Exception
{
    public HmiUnknownAttributeException(string attribute, string typeName)
        : base($"'{typeName}' declares no attribute named '{attribute}'. List the real ones, with their access modes, using `openness-cli hmi <project> --schema`.")
    {
    }
}

public sealed class HmiAttributeNotWritableException : Exception
{
    public HmiAttributeNotWritableException(string attribute, string typeName, string accessMode)
        : base($"'{typeName}.{attribute}' has access mode {accessMode} and cannot be set. Read-only sub-parts (Font, Padding, InputBehavior, ToolTipText) are configured by reaching into the object they return, not by assigning to them.")
    {
    }
}

public sealed class HmiEventsNotSupportedException : Exception
{
    public HmiEventsNotSupportedException(string typeName)
        : base($"'{typeName}' exposes no EventHandlers composition, so no event can be attached to it.")
    {
    }
}

public sealed class HmiUnknownEventTypeException : Exception
{
    public HmiUnknownEventTypeException(string eventType, string typeName, IReadOnlyList<string> valid)
        : base($"'{typeName}' has no event '{eventType}'. Valid events for it: {string.Join(", ", valid)}. Note the vocabulary is touch-first — there is no 'Click'; use 'Tapped'.")
    {
    }
}

public sealed class HmiScreenAlreadyExistsException : Exception
{
    public HmiScreenAlreadyExistsException(string name)
        : base($"A screen named '{name}' already exists. This command creates screens and never overwrites one — choose a different name, or delete the existing screen in TIA Portal first.")
    {
    }
}

public sealed class HmiUnknownScreenItemTypeException : Exception
{
    public HmiUnknownScreenItemTypeException(string typeName)
        : base($"'{typeName}' is not a concrete screen-item type in the installed Openness assembly. Run `openness-cli hmi <project> --schema` to list the types this device can actually create.")
    {
    }
}

public sealed class NoUnifiedHmiDeviceException : Exception
{
    public NoUnifiedHmiDeviceException()
        : base("No WinCC Unified HMI device found in this project. Screen creation is Unified-only — classic HMI exposes no screen-item model at all, so there is nothing to create into.")
    {
    }
}

public sealed class AmbiguousHmiDeviceException : Exception
{
    public AmbiguousHmiDeviceException(IReadOnlyList<string> paths)
        : base($"More than one WinCC Unified HMI device found ({string.Join(", ", paths)}). Refusing to guess which one to write to.")
    {
    }
}

public sealed class NotAPlcSoftwareContainerException : Exception
{
    public NotAPlcSoftwareContainerException(string path)
        : base($"'{path}' is not a PLC software container.")
    {
    }
}
