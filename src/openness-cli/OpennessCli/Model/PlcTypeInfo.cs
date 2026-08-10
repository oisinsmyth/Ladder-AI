namespace OpennessCli.Model;

// A PLC data type (UDT) has no Number/Language/Safety concept — confirmed real, 2026-07-14: PlcType
// exposes no ProgrammingLanguage at all (which is why ExportType carries no safety refusal). Same
// minimal shape and same reasoning as TagTableInfo, rather than retrofitting BlockInfo with fields
// that would be meaningless here.
//
// Added for FI-70's bulk export. Types were reachable inside the gateway (sanity-check counts them
// since FI-62) but were never on the interface, so no caller could ask what they were — and a bulk
// export that silently omitted every UDT would be exactly the kind of incomplete dump FI-70 exists
// to stop being mistaken for a complete one.
public sealed record PlcTypeInfo(string Name, string Path);
