using System.Collections.Generic;

namespace OpennessCli.Model;

/// <summary>
/// Which HMI API a device's software is reached through. The two are disjoint object models
/// sharing nothing but the <c>SoftwareContainer</c> they hang off — see
/// docs/notes/openness-hmi-api-survey.md. The distinction is not cosmetic: it decides whether
/// screen contents are readable at all.
/// </summary>
public enum HmiFamily
{
    /// <summary>WinCC Unified (<c>Siemens.Engineering.HmiUnified.HmiSoftware</c>) — screens carry a
    /// full typed item tree, so <see cref="HmiScreenInfo.Items"/> can be populated.</summary>
    Unified,

    /// <summary>Classic Comfort/Advanced/Professional (<c>Siemens.Engineering.Hmi.HmiTarget</c>) —
    /// <c>Screen</c> exposes no items at all, so screens enumerate by name only and
    /// <see cref="HmiScreenInfo.Items"/> is always empty. That is the API's shape, not a gap in
    /// this walker.</summary>
    Classic,
}

public sealed record HmiDeviceInfo(
    string Path,
    string SoftwareName,
    HmiFamily Family,
    int ScreenCount,
    int ScreenGroupCount,
    int TagCount,
    int DiscreteAlarmCount,
    int AnalogAlarmCount,
    int AlarmClassCount,
    int ScriptCount,
    IReadOnlyList<HmiScreenInfo> Screens);

public sealed record HmiScreenInfo(
    string Name,
    int? ScreenNumber,
    long? Width,
    long? Height,
    int ItemCount,
    IReadOnlyList<HmiScreenItemInfo> Items,
    // Screens carry their own handlers (Loaded/Unloaded/Tapped/ContextTapped), on a different
    // composition from their items'. Omitting them made a created screen-level event unverifiable.
    IReadOnlyList<HmiEventInfo> Events);

/// <summary>
/// One object on a screen. <see cref="ItemType"/> is the CLR type name (e.g. <c>HmiIOField</c>) —
/// the same unambiguous-type-over-inferred-kind choice <c>ClassifyBlockType</c> makes PLC-side.
/// Geometry is read through generic attribute access rather than by casting to each of the ~50
/// concrete item types, so an item type this tool has never seen still reports its position.
/// </summary>
public sealed record HmiScreenItemInfo(
    string Name,
    string ItemType,
    long? Left,
    long? Top,
    long? Width,
    long? Height,
    IReadOnlyList<HmiDynamizationInfo> Dynamizations,
    IReadOnlyList<HmiEventInfo> Events);

/// <summary>
/// One event handler on a screen or screen item. Reading these closes the walker's one genuinely
/// misleading gap: a button reported with no dynamizations is not unbound, its behaviour simply
/// lives in <c>EventHandlers</c>, which is a different composition entirely.
///
/// The event vocabulary is touch-first — there is no "Click". Interactive items expose
/// <c>Tapped</c>/<c>ContextTapped</c>/<c>KeyDown</c>/<c>KeyUp</c> (buttons add <c>Down</c>/<c>Up</c>),
/// screens expose <c>Loaded</c>/<c>Unloaded</c>, and controls expose <c>Initialized</c>/
/// <c>CommandFired</c>. Each event type is an enum member of a per-item-type enum, not a string.
/// </summary>
public sealed record HmiEventInfo(
    string EventType,
    bool HasScript,
    string? ScriptPreview);

/// <summary>
/// A single property-to-source binding. This is the whole reason the walker exists: on Unified
/// there is no screen export, so a dynamization is only observable by reading the live object.
/// <see cref="Tag"/>/<see cref="PlcTag"/> are populated for tag dynamizations only; the other
/// kinds (Script, Flashing, ResourceList, Expression, TagParameter) report <see cref="Kind"/> and
/// leave both null.
/// </summary>
public sealed record HmiDynamizationInfo(
    string PropertyName,
    string Kind,
    string? Tag,
    string? PlcTag,
    // The value-converter half of a TagDynamization, rendered as one line: ConditionType, the
    // formula if one is selected, and every mapping-table entry with its value, alternate value and
    // flashing state. Null when the dynamization is not a tag one, or carries no converter
    // configuration at all — a screen full of plain bindings must not grow a column of empty
    // mapping summaries. This is the only way to observe a mapping table from outside the writing
    // process: Unified has no screen export, so read-back IS the evidence.
    string? Mapping = null);

/// <summary>
/// One writable/readable property of a screen-item type, as the API itself describes it.
///
/// This is the answer to "where is the schema" for WinCC Unified. There is no screen XML to read —
/// Unified has no screen export at all (docs/notes/openness-hmi-api-survey.md §4) — but Openness is
/// self-describing: <c>IEngineeringObject.GetAttributeInfos()</c> reports every attribute with its
/// access mode and, crucially, its <see cref="CreateRelevance"/>. That is strictly better than a
/// sample XML file, because it distinguishes what you MAY set from what you MUST set at creation
/// time, which no example document can tell you.
/// </summary>
public sealed record HmiAttributeSchema(
    string Name,
    string AccessMode,
    string CreateRelevance,
    string? SupportedType,
    string? SampleValue);

/// <summary>
/// The schema of one screen-item CLR type (e.g. <c>HmiIOField</c>), reported once per type rather
/// than once per instance — twenty buttons on a screen have one schema between them.
/// </summary>
public sealed record HmiTypeSchema(
    string TypeName,
    IReadOnlyList<string> Compositions,
    IReadOnlyList<HmiAttributeSchema> Attributes);

/// <summary>
/// What `--schema` produces: the item types this device could create, and the full attribute schema
/// of each type actually observed on the walked screens.
/// </summary>
public sealed record HmiSchemaReport(
    string DevicePath,
    IReadOnlyList<string> CreatableScreenItemTypes,
    IReadOnlyList<HmiTypeSchema> ItemSchemas);

/// <summary>
/// One message from <c>UIBase.Validate()</c>. Reported per property, and warnings are kept
/// separately from errors because the API distinguishes them — this is the closest thing the HMI
/// side has to the PLC compile gate, and flattening the two would throw away exactly the
/// information that makes it a gate rather than a hint.
/// </summary>
public sealed record HmiValidationMessage(
    string PropertyName,
    string Severity,
    string Message);

/// <summary>
/// Result of the screen-creation probe: what was created, and what <c>Validate()</c> said about it.
/// <see cref="Saved"/> records whether the project was actually written — a created-but-unsaved
/// screen lives only in the Portal process's memory and vanishes with it (the trap
/// <c>SaveProject</c>'s own comment records for the PLC side).
/// </summary>
public sealed record HmiCreateScreenResult(
    string DevicePath,
    string ScreenName,
    long Width,
    long Height,
    IReadOnlyList<string> CreatedItems,
    IReadOnlyList<HmiValidationMessage> Validation,
    bool Saved);

/// <summary>
/// One object in an HMI composition, as reported by `hmi-inventory`. Deliberately minimal — this is
/// a census, not a description; `hmi --screen` and `--schema` are for detail.
/// </summary>
public sealed record HmiObjectInfo(string Kind, string Name, string TypeName);

/// <summary>
/// Result of editing an existing screen. <see cref="Applied"/> records each change in the form it was
/// actually made, which is not always the form it was asked for — an attribute declared as an enum
/// or a number takes a converted value, and saying so makes a silent coercion visible.
/// </summary>
public sealed record HmiEditScreenResult(
    string DevicePath,
    string ScreenName,
    IReadOnlyList<string> Applied,
    IReadOnlyList<HmiValidationMessage> Validation,
    bool Saved);

/// <summary>
/// A project-library type. <paramref name="ClrTypeName"/> is the point of the whole command:
/// faceplates are library types (`Hmi.Faceplate.FaceplateLibraryType`), and whether a *Unified*
/// faceplate surfaces as that class or as a plain `LibraryType` is the load-bearing unknown in
/// `docs/notes/openness-hmi-faceplate-library.md`. Reporting the CLR name of every type answers it
/// by observation instead of by inference.
/// </summary>
public sealed record LibraryTypeInfo(
    string FolderPath,
    string Name,
    string ClrTypeName,
    string? Namespace,
    string Status,
    IReadOnlyList<string> ExportFormats,
    IReadOnlyList<LibraryVersionInfo> Versions);

public sealed record LibraryVersionInfo(
    string VersionNumber,
    string State,
    string ClrTypeName,
    bool IsDefault,
    int InstanceCount);

public sealed record MasterCopyInfo(string FolderPath, string Name, IReadOnlyList<string> ContentTypes);

public sealed record LibraryInventory(
    IReadOnlyList<LibraryTypeInfo> Types,
    IReadOnlyList<MasterCopyInfo> MasterCopies);
