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

// FI-44 - why the scan produced no rows. A scan that examined nothing must not report success, and
// the two ways of examining nothing need different words because they need different actions.
public enum ScanScope
{
    Scanned,          // the FB exists and has instances; the rows below are a real result
    UnknownBlock,     // no block of this name in the corpus - a typo, or a block not written yet
    NoInstances,      // the block exists but nothing instantiates it - nothing to resolve per-instance

    // 🔴 2026-08-18. The block exists, it HAS instances, and NO INTERFACE MEMBER SURVIVED THE SCOPE
    // FILTER: every one of them is written by the FB itself, so there is no caller-driven input to
    // resolve. An ordinary state for a block that only publishes - and it exited 0 over zero rows,
    // which is what a thorough clean scan also looks like. Two of the three largest blocks on a live
    // corpus were in exactly this state and both were read as passes.
    NoMembersInScope,

    // --instance named nothing that exists. The filter typo, one level in from UnknownBlock, and the
    // same failure: zero rows, no findings, exit 0.
    NoInstancesMatchedFilter,
}

public sealed record UndrivenScanReport(
    string ProjectDir,
    int FilesScanned,
    string FbName,
    IReadOnlyList<string> Callers,
    IReadOnlyList<MemberDrive> Members,
    IReadOnlyList<string> Warnings,
    ScanScope Scope = ScanScope.Scanned)
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

    // FI-44 - "empty is not clean". Before this, an unknown --fb produced zero rows, zero findings
    // and EXIT 0: a block that had never been written passed the drive-state check. Verified with a
    // deliberately invented name. There is no reading of "scan a block that does not exist" that
    // ends in success, and the same holds for a block nothing instantiates - per-instance resolution
    // over zero instances has resolved nothing.
    //
    // Kept SEPARATE from HasFindings on purpose. A finding is a defect in the plant; this is a
    // defect in the question. They deserve different exit codes and different words, because the
    // action differs: fix the wiring, versus fix what you asked.
    //
    // 🔴 KEYED ON THE ROW COUNT AS WELL AS ON THE SCOPE, since 2026-08-18. The scope enum enumerates
    // the ways of examining nothing that someone has already thought of, and the third one arrived
    // three weeks after the first two were called complete. `Members.Count == 0` is the fact itself
    // rather than a catalogue of its causes, so a FOURTH shape gates on arrival instead of on being
    // noticed - and any new scope value that forgets to be listed here still cannot report a pass.
    public bool ExaminedNothing => Scope is not ScanScope.Scanned || Members.Count == 0;
}
