namespace Converter.SimaticMl;

// A PLC data type (UDT, source root element `SW.Types.PlcStruct`) — confirmed real, 2026-07-14
// (`TypeDOL`, `FB MotorDOL`'s own dependency). Simpler than DbSource: no Number (UDTs aren't
// numbered like blocks), no InstanceOfName (a type is never an instance of anything). Members
// reuse DbMember directly — a UDT's own member shape (Name/Datatype/BooleanAttributes/optional
// StartValue) is the same underlying concept as a DB's Static section member, just parsed via
// DbInterfaceMembers.ParseTypeMember/WriteTypeMember (no Remanence/Accessibility attribute on the
// <Member> tag itself — confirmed real, a genuine difference from DbSourceParser's own shape).
public sealed record PlcTypeSource(
    string RootUId,
    string Name,
    string? Comment,
    IReadOnlyList<DbMember> Members);
