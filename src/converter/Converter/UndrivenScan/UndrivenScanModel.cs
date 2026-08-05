namespace Converter.UndrivenScan;

// Whether an instance's interface input actually receives a value, computed per instance.
public enum DriveState
{
    Driven,           // at least one armed writer
    Disarmed,         // writers exist but every one is provably-false-guarded — built, switched off
    UndrivenDefault,  // no writer, but the instance DB carries a start value: that constant IS the behaviour
    Undriven,         // no writer, no start value
    DeadInterfaceMember, // declared on the interface, no writer AND the FB never reads it — inert on both
                         // sides. The shape of the documented dropped-bypass defect: the FB offers a
                         // rotation-sensor input, the caller wires nothing to it, and nothing consumes it.
}

// One interface member of one instance. Per-instance is the whole point: pooling across every instance
// of a shared FB — which the project-wide reference graph does deliberately, for its own question —
// hides the case where one instance drives a member and another does not, and that is exactly the shape
// of the documented dropped-bypass defect.
public sealed record MemberDrive(
    string Instance,
    string Member,
    DriveState State,
    string? StartValue,
    IReadOnlyList<string> Writers,
    IReadOnlyList<string> NameJoinHints);

public sealed record UndrivenScanReport(
    string ProjectDir,
    int FilesScanned,
    string FbName,
    IReadOnlyList<string> Callers,
    IReadOnlyList<MemberDrive> Members,
    IReadOnlyList<string> Warnings)
{
    // Exit-bearing on hard facts only: the FB READS this member and nothing writes it, so it consumes an
    // unset value — or every writer is disarmed, which is the same thing with extra steps.
    //
    // DeadInterfaceMember deliberately does NOT gate. A reusable library block legitimately exposes
    // optional inputs a given instance doesn't use, so "declared, untouched" is a FACT (reported, and
    // queryable against a spec that required the capability) rather than a defect on its own. Gating on
    // it would emit findings by the hundred on a rich FB and train readers to ignore the output — the
    // failure mode this tooling exists to avoid.
    public bool HasFindings =>
        Members.Any(m => m.State is DriveState.Undriven or DriveState.Disarmed);
}
