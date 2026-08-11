using System.Collections.Generic;
using System.Linq;

namespace OpennessCli.Model;

// Same shape as ExportAllResult, for the same reason: the summary has to be able to state what did
// NOT go in, and a running commentary printed as it goes cannot — a file that failed on pass 1 and
// succeeded on pass 3 must not read as a failure, and one that failed on every pass must not scroll
// away above 100 successes.
public enum ImportAllOutcome
{
    Imported,
    Failed,   // attempted on every pass and still throwing when the passes stopped making progress
    Rejected, // never attempted: unreadable, unclassifiable, or a duplicate name in the input set
}

/// <param name="Pass">Which pass imported it. &gt;1 means it depended on something imported later in the order.</param>
public sealed record ImportAllEntry(
    string Path,
    string Name,
    string Kind,
    ImportAllOutcome Outcome,
    int Pass,
    string? Detail);

public sealed record ImportAllResult(string GroupPath, IReadOnlyList<ImportAllEntry> Entries, int Passes)
{
    public int ImportedCount => Entries.Count(e => e.Outcome == ImportAllOutcome.Imported);

    public int FailedCount => Entries.Count(e => e.Outcome == ImportAllOutcome.Failed);

    public int RejectedCount => Entries.Count(e => e.Outcome == ImportAllOutcome.Rejected);

    /// <summary>
    /// Everything supplied went in. Deliberately NOT "nothing threw": a rejected file never reached
    /// Portal and so never threw, and it is exactly the case where the project comes back looking
    /// whole while a block is missing from it.
    /// </summary>
    public bool IsComplete => FailedCount == 0 && RejectedCount == 0;
}
