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

    /// <summary>
    /// TRUE when nothing was compiled at all. Named, because the two zeros this record can hold —
    /// <i>examined everything and found nothing wrong</i> and <i>examined nothing</i> — are the same
    /// numbers and opposite facts, and only one of them is a pass.
    /// </summary>
    public bool NothingExamined => CompiledCount == 0;

    /// <summary>
    /// 🔴 <b>GUARDED ON <see cref="NothingExamined"/> (2026-08-14).</b> It used to read
    /// <c>WithErrorsCount == 0 &amp;&amp; StillInconsistentCount == 0</c>, which is
    /// <b>vacuously true when nothing was examined</b> — <c>0 == 0 &amp;&amp; 0 == 0</c> — so an
    /// empty work set produced the same value as a fully-verified clean project.
    ///
    /// <para>The TEXT formatter never showed it: it prints NOTHING EXAMINED <i>instead of</i> the
    /// CLEAN line when <c>CompiledCount == 0</c>, with the rule written in a comment beside it
    /// (<i>"Nothing was examined must never render as everything passed"</i>).
    /// <b><c>FormatCompileAllJson</c> had no such guard and emitted <c>"clean": true</c> on exactly
    /// that run</b> — measured live on GenProject1, where an all-consistent project returned
    /// <c>{"clean": true, "compiled": 0, "entries": []}</c> alongside the text run's
    /// <c>NOTHING EXAMINED</c>. The exit code (14) was the only thing separating them, and a
    /// consumer branching on the field named <c>clean</c> is not an unreasonable consumer.</para>
    ///
    /// <para>This is the case the command exists to catch, aimed at itself: an item that compiled
    /// WITH ERRORS is still flagged consistent, so <b>the run right after a failed one has an empty
    /// work set</b> — the moment this value is most likely to be read and least entitled to be
    /// believed. Fixing it in the model rather than in the JSON formatter is deliberate: a guard
    /// that has to be repeated at every render site is one a future render site will not get.</para>
    /// </summary>
    public bool IsClean => !NothingExamined && WithErrorsCount == 0 && StillInconsistentCount == 0;
}
