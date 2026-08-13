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

/// <param name="Scope">
/// WHICH COMPILE RAN — and it is recorded because for a year the answer was not what this line
/// implied. Until 2026-08-13 this compile was the PLC <c>DeviceItem</c>'s, whose entire message tree
/// is `Hardware configuration`; it never looked at the program, which is why it could report
/// `Success (errors=0, warnings=0)` over blocks that do not compile. It is now the <c>Device</c>
/// (station) scope — hardware AND program. Naming the scope in the report is the cheap half of the
/// fix: the expensive half was that nobody could tell from the output which question had been asked.
/// </param>
public sealed record DeviceCompileSummary(string DevicePath, CompileResult Compile, string Scope);

/// <param name="DuplicateNumbers">
/// Blocks sharing one number on one device (2026-08-13). Deliberately a REQUIRED positional
/// parameter with no default: a default would let a future construction site omit it and get a
/// result that silently claims nothing was found, which is the exact shape of the hole this closes.
/// </param>
public sealed record SanityCheckResult(
    int TotalBlocks,
    IReadOnlyList<BlockConsistencyIssue> InconsistentBlocks,
    IReadOnlyList<DeviceCompileSummary> DeviceCompiles,
    int TotalTypes,
    IReadOnlyList<TypeConsistencyIssue> InconsistentTypes,
    IReadOnlyList<DuplicateBlockNumber> DuplicateNumbers)
{
    // Types count toward health exactly as blocks do — the whole point of FI-62 is that an
    // inconsistent type must not pass a gate that calls itself HEALTHY.
    //
    // And so do duplicate block numbers (2026-08-13). They are the sharpest case yet for why this
    // property must not be a synonym for "everything is consistent": the measured project WAS fully
    // consistent and DID compile clean while holding two blocks at FC 910. Consistency and
    // compilation are the two questions this check used to ask, and both of them answer "fine" on a
    // project that has been silently broken.
    // THE COMPILE VERDICT KEYS ON ERRORS, NEVER ON State (2026-08-13) — the rule `compile` and
    // `compile-all` already follow, arrived at here the moment the compile above became a real
    // program compile. It used to read `State == CompileState.Success`, which was survivable only
    // because the hardware-only compile it ran had nothing to warn about. The station scope reports
    // the project's genuine warnings — a hardware-interrupt OB with no trigger, IO that does not
    // exist in the configured hardware — so on State this check would call a perfectly good project
    // unhealthy, on every run, for reasons no action of ours can clear.
    //
    // Fail-closed on the count itself, same as `compile`: the larger of the compiler's own
    // ErrorCount and the number of Error messages in its tree, because the two demonstrably disagree.
    public bool IsHealthy =>
        InconsistentBlocks.Count == 0
        && InconsistentTypes.Count == 0
        && DuplicateNumbers.Count == 0
        && DeviceCompiles.All(d => EffectiveErrors(d.Compile) == 0);

    internal static int EffectiveErrors(CompileResult result) =>
        System.Math.Max(result.ErrorCount, result.Messages.Count(m => m.State == CompileState.Error));
}
