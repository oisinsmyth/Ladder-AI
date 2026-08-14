using System.Collections.Generic;
using System.Linq;

namespace OpennessCli.Model;

// FI-70. The outcome of a bulk export, kept as a record set with its own renderers (the
// SanityCheckResult pattern) rather than printed as it goes — the summary has to be able to state
// what was NOT produced, and a running commentary cannot.
public enum ExportAllOutcome
{
    Exported,
    Refused, // planned, then deliberately not exported — safety content, or a basename collision
    Failed,  // attempted and threw, or Export() returned without writing a file
}

public sealed record ExportAllEntry(
    string Name,
    string Kind,
    string Path,
    string? OutPath,
    ExportAllOutcome Outcome,
    string? Detail);

/// <param name="TagTablesIncluded">
/// Whether <c>--tagtables</c> was given. 🔴 <b>Load-bearing for the ADVICE this report prints, and
/// missing until 2026-08-14.</b> The summary said <c>COMPLETE: every block and type in the project
/// was exported</c> — accurate, and appropriately scoped — and then recommended
/// <c>drift-check --complete</c>, <b>whose entire meaning is "treat this directory as everything"</b>.
/// A reader followed that literally and got two spurious <c>SKIPPED</c> rows for tag tables that were
/// never exported: findings about the dump, presented as findings about the controller.
///
/// <para><i>Advice is a claim.</i> The recommendation dropped the scope the sentence above it had
/// been careful to state, at exactly the moment the reader was deciding what to do next — so the
/// flag is carried here and the recommendation is conditional on it.</para>
/// </param>
public sealed record ExportAllResult(
    string OutDir, IReadOnlyList<ExportAllEntry> Entries, bool TagTablesIncluded = false)
{
    public int ExportedCount => Entries.Count(e => e.Outcome == ExportAllOutcome.Exported);

    public int RefusedCount => Entries.Count(e => e.Outcome == ExportAllOutcome.Refused);

    public int FailedCount => Entries.Count(e => e.Outcome == ExportAllOutcome.Failed);

    // A dump with a hole in it is not a dump. Both non-exported outcomes fail the command, because
    // the whole point of this directory is to be handed to a completeness check that reads a missing
    // file as "this block is not in the controller" — so a partial dump silently becomes a set of
    // false findings about the controller. Refused is legitimate and still fails: the operator must
    // know the dump is incomplete before comparing against it, even when the reason is correct.
    public bool IsComplete => FailedCount == 0 && RefusedCount == 0;
}
