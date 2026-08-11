using System.Collections.Generic;
using System.Linq;

namespace OpennessCli.Model;

// The gate's missing bulk half. FI-52 established that a whole-device compile does NOT clear the
// inconsistent flag a freshly-imported block carries, so the only thing that does is a per-block (or
// per-type) compile — which, after a restore, means ninety-odd of them. One CLI invocation each
// means one Portal attach each; this exists so it is one session.
//
// Errors and inconsistency are tracked SEPARATELY on purpose. They are different failures: a block
// can compile with errors (it was examined and it is wrong) or stay inconsistent (it was not
// examined at all, or the compile did not take). Collapsing them into one "failed" count would let
// the second hide inside the first.
public sealed record CompileAllEntry(
    string Name,
    string Kind,
    CompileState State,
    int ErrorCount,
    int WarningCount,
    bool StillInconsistent,
    string? Detail);

public sealed record CompileAllResult(IReadOnlyList<CompileAllEntry> Entries, int Passes)
{
    public int CompiledCount => Entries.Count;

    /// <summary>
    /// Counted on <c>ErrorCount</c>, never on <c>State</c>. A project with pre-existing hardware
    /// warnings returns a non-Success state on a perfectly clean block, so keying the verdict on the
    /// state would call every block on such a project a failure.
    /// </summary>
    public int WithErrorsCount => Entries.Count(e => e.ErrorCount > 0);

    public int StillInconsistentCount => Entries.Count(e => e.StillInconsistent);

    public bool IsClean => WithErrorsCount == 0 && StillInconsistentCount == 0;
}
