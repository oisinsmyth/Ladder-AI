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
