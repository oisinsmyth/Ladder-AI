namespace OpennessCli.Model;

public enum CompileState
{
    Success,
    Information,
    Warning,
    Error,
}

public sealed record CompileMessage(CompileState State, string Description, string Path);

/// <param name="ConsistentAfterCompile">
/// The item's own <c>IsConsistent</c> flag, RE-READ from a freshly re-resolved block/type AFTER the
/// compile and the save. <c>null</c> means the question was not asked (a whole-device or HMI compile,
/// where there is no single item to ask about) or could not be answered (the re-resolve found no
/// single match).
///
/// It exists because **a clean per-block compile does not imply the block is exportable** — measured
/// 2026-08-12: a per-block compile reported *"Block was successfully compiled"* with
/// <c>ErrorCount=0</c>, and the block was still flagged <c>IsConsistent=false</c> because a block it
/// referenced did not exist. TIA then refused to export it (*"Inconsistent blocks and PLC data types
/// (UDT) cannot be exported"*), and `sanity-check` was the only thing that caught it. This is the
/// converse of FI-52: FI-52 is *a device compile leaves OTHER blocks inconsistent*; this is *a
/// per-block compile leaves ITS OWN block inconsistent*.
///
/// Read back rather than assumed, on the same principle as <c>block-layout --set</c>: the failure
/// mode is an operation that reports success and did not take, and only reading the state afterwards
/// tells the two apart.
/// </param>
public sealed record CompileResult(
    CompileState State,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<CompileMessage> Messages,
    bool? ConsistentAfterCompile = null);
