namespace Converter.SimaticMl;

// A PLC tag's own shape is genuinely simpler and different from a DB/UDT member — confirmed real,
// 2026-07-14 (grounding PlantAutoControl's own dependency closure, "Default tag table",
// station_2/JOB9002_PLC): DataTypeName/ExternalAccessible/ExternalVisible/ExternalWritable/
// LogicalAddress/Name are all plain child elements directly under <AttributeList> (not wrapped in
// <BooleanAttribute Name="..."> the way a DB/UDT member's own booleans are), and there's no
// Retain/SetPoint/StartValue/nested-structure concept at all — a tag is a flat, physical-address-
// backed scalar. LogicalAddress (e.g. "%IW64") is structural (tied to hardware wiring), not
// identifying content, so it's never sanitized — same category as a UId or array index.
public sealed record PlcTagSource(
    string RootUId,
    string Name,
    string DataTypeName,
    string LogicalAddress,
    bool ExternalAccessible,
    bool ExternalVisible,
    bool ExternalWritable,
    string? Comment);

public sealed record PlcTagTableSource(
    string RootUId,
    string Name,
    IReadOnlyList<PlcTagSource> Tags);
