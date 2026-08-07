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
    IReadOnlyList<HmiScreenItemInfo> Items);

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
    IReadOnlyList<HmiDynamizationInfo> Dynamizations);

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
    string? PlcTag);

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
