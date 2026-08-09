using System.Collections.Generic;
using System.Linq;

namespace OpennessCli.Model;

public sealed record BlockConsistencyIssue(string Name, string Path, string Language);

/// <summary>
/// FI-62 (2026-08-09). A PLC DATA TYPE IS NOT A BLOCK, AND THE GATE USED TO MISS IT ENTIRELY.
///
/// Measured on a live job: `sanity-check` reported `OVERALL: HEALTHY`, `BLOCKS: 52`,
/// `INCONSISTENT: 0` and a device compile of `Success (errors=0, warnings=0)` — and TIA then
/// refused `export --type UDT_Drum` with *"Inconsistent blocks and PLC data types (UDT) cannot be
/// exported."* The refusal came from TIA, not from us (<c>ExportType</c> has no consistency guard
/// of its own; it just calls <c>type.Export(...)</c>).
///
/// Two things hid it. The check enumerated <c>PlcBlock</c> only, so a UDT was never examined. And
/// the device compile stayed green because nothing in that corpus instantiated the UDT — an
/// uninstantiated type has nothing to make a compile fail. So a freshly-imported type could sit
/// inconsistent behind a completely clean gate, and the first symptom was an export refusal much
/// later.
///
/// Same family as FI-52, one layer up: a check reporting a clean result over something it had not
/// examined. Hard rule 4 names `sanity-check` as THE gate, so a blind spot in it is a blind spot in
/// the gate.
/// </summary>
public sealed record TypeConsistencyIssue(string Name, string Path);

public sealed record DeviceCompileSummary(string DevicePath, CompileResult Compile);

public sealed record SanityCheckResult(
    int TotalBlocks,
    IReadOnlyList<BlockConsistencyIssue> InconsistentBlocks,
    IReadOnlyList<DeviceCompileSummary> DeviceCompiles,
    int TotalTypes,
    IReadOnlyList<TypeConsistencyIssue> InconsistentTypes)
{
    // Types count toward health exactly as blocks do — the whole point of FI-62 is that an
    // inconsistent type must not pass a gate that calls itself HEALTHY.
    public bool IsHealthy =>
        InconsistentBlocks.Count == 0
        && InconsistentTypes.Count == 0
        && DeviceCompiles.All(d => d.Compile.State == CompileState.Success);
}
