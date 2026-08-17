namespace Harness.Results;

/// <summary>What kind of thing accumulates. DB-7's three, and the zero value is not one of them.</summary>
public enum RemovalKind
{
    /// <summary>Nothing said. <b>Unusable</b>, so a candidate nobody classified cannot be removed as whichever member came first.</summary>
    Unstated = 0,

    /// <summary>A per-TEST instance DB (D10 — one per test, not one per agent). The fastest-growing of the three.</summary>
    InstanceDb,

    /// <summary>A model no vector still references.</summary>
    Model,

    /// <summary>A block not in any admitted test.</summary>
    Block,
}

/// <summary>Why one candidate was or was not removable. <b>Every one of these is a different fact.</b></summary>
public enum CleanupEligibility
{
    /// <summary>Unusable zero value.</summary>
    Unstated = 0,

    /// <summary>The reference graph proves nothing references it, tests are drained, and an authority is recorded.</summary>
    Eligible,

    /// <summary>Something references it. <b>Computed from the graph</b>, never inferred from age or from a test having finished.</summary>
    Referenced,

    /// <summary>No authority was recorded for the removal, so it could not be recorded as DB-7 requires.</summary>
    NoAuthorityRecorded,

    /// <summary>The candidate did not say what kind of object it is.</summary>
    KindNotStated,

    /// <summary>It is eligible and did not fit in this batch's change budget. <b>Deferred, never dropped.</b></summary>
    DeferredToNextBatch,
}

/// <summary>Something cleanup might remove.</summary>
/// <param name="Authority">
/// <b>On whose authority.</b> DB-7 requires every removal to record what, why and on whose authority —
/// otherwise this becomes the "a block silently disappeared" failure, which is the shape of the
/// folder-download footgun (R5) forbidden elsewhere. An empty authority is a refusal, not a default.
/// </param>
public sealed record CleanupCandidate(string Name, RemovalKind Kind, string Why, string Authority);

/// <summary>
/// <b>The computed reference graph</b> — <c>converter cross-check</c>'s output, as the only admissible
/// evidence of eligibility.
///
/// <para>DB-7: <i>eligibility is GRAPH-PROVEN, NEVER COUNT-BASED.</i> "Nothing references this" is a
/// computed fact, not an assumption from age or from a test having finished — <b>a block can be
/// unreferenced by vectors and still called by another block.</b> So this graph carries every referrer,
/// whatever kind it is, and a candidate is eligible only when the set is empty.</para>
/// </summary>
public sealed record ReferenceGraph(IReadOnlyDictionary<string, IReadOnlySet<string>> ReferencedBy)
{
    public IReadOnlySet<string> Referrers(string name) =>
        ReferencedBy.TryGetValue(name, out var refs) ? refs : new HashSet<string>(StringComparer.Ordinal);

    public static ReferenceGraph Of(params (string Name, string[] Referrers)[] entries) =>
        new(entries.ToDictionary(
            e => e.Name,
            e => (IReadOnlySet<string>)e.Referrers.ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal));

    /// <summary>Nothing references anything. A real state, and distinct from "no graph was computed" (a null).</summary>
    public static ReferenceGraph Empty { get; } = new(new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal));
}

/// <summary>One candidate's disposition, with the evidence.</summary>
public sealed record CleanupDisposition(CleanupCandidate Candidate, CleanupEligibility Eligibility, string Detail)
{
    public bool Removable => Eligibility == CleanupEligibility.Eligible;
}

/// <summary>Whether a cleanup batch could be planned at all.</summary>
public enum CleanupOutcome
{
    /// <summary>Unusable zero value.</summary>
    Unstated = 0,

    /// <summary>A batch was computed. It may still be empty — see <see cref="CleanupReport.Removals"/>.</summary>
    Planned,

    /// <summary>Tests are still in flight, so nothing may be removed. <b>Deleting something a running test depends on is the obvious way to make this dangerous.</b></summary>
    TestsNotDrained,

    /// <summary>No reference graph was supplied, so eligibility could be proven for nothing.</summary>
    GraphNotAvailable,

    /// <summary>Nothing established whether tests are drained. <b>Unknown is not drained.</b></summary>
    DrainStateUnknown,

    /// <summary>There were no candidates. Empty is not clean: a plan over nothing is not a successful cleanup.</summary>
    NothingExamined,
}

/// <summary>The batch, and everything it did not do.</summary>
public sealed record CleanupReport(
    CleanupOutcome Outcome,
    IReadOnlyList<CleanupDisposition> Dispositions,
    string Detail)
{
    /// <summary>What this batch would remove.</summary>
    public IReadOnlyList<CleanupDisposition> Removals => Dispositions.Where(d => d.Removable).ToArray();

    /// <summary>Eligible and held back by the change budget. <b>Named, so a partial batch cannot read as a complete one.</b></summary>
    public IReadOnlyList<CleanupDisposition> Deferred =>
        Dispositions.Where(d => d.Eligibility == CleanupEligibility.DeferredToNextBatch).ToArray();

    /// <summary>
    /// DB-7's record — <b>what, why, and on whose authority</b>, one line per removal, on every batch
    /// including empty ones.
    /// </summary>
    public string Render() =>
        $"{Outcome.ToString().ToUpperInvariant()} - {Removals.Count} removal(s), {Deferred.Count} deferred, "
        + $"{Dispositions.Count - Removals.Count - Deferred.Count} retained. {Detail}"
        + (Removals.Count == 0
            ? string.Empty
            : Environment.NewLine + string.Join(Environment.NewLine, Removals.Select(r =>
                $"  REMOVE {r.Candidate.Kind} '{r.Candidate.Name}' - why: {r.Candidate.Why} - authority: {r.Candidate.Authority} - evidence: {r.Detail}")));
}

/// <summary>
/// <b>Build-plan 6.7 — DB-7's cleanup stage.</b>
///
/// <para><i>Nothing in the design ever removes anything</i>, and three things accumulate: per-test instance
/// DBs, models, and blocks no longer under test. Left alone they consume scan time (M6), object count, and
/// eventually the change budget.</para>
///
/// <para><b>DB-7's four rules, each of which is a refusal here rather than guidance:</b></para>
/// <list type="number">
/// <item><b>It runs when tests are DRAINED</b> — the same condition as the deferred queue (D24). Deleting
/// something a running test depends on is the obvious way to make this dangerous. <b>The drain state is
/// computed from the in-flight set, never asserted by a caller</b>: a <c>bool drained</c> parameter would
/// be the caller supplying the verdict, which is the defect this project keeps finding.</item>
/// <item><b>Eligibility is GRAPH-PROVEN, NEVER COUNT-BASED.</b> There is no member on any type in this
/// file named for an age, a count, or a last-used time — a reflection test pins that, because the rule is
/// only as good as the absence of an easier alternative sitting next to it.</item>
/// <item><b>Every removal is recorded</b> — what, why and on whose authority. A candidate with no recorded
/// authority is not removed.</item>
/// <item><b>Deletions are ordinary changes</b>: they route through DB-1 and count against the change
/// budget — and, usefully, deleting an FB/FC/DB/UDT is RUN-class, so <b>cleanup needs no disruptive
/// boundary of its own</b>. What does not fit in a batch is DEFERRED and named, never dropped.</item>
/// </list>
/// </summary>
public static class Cleanup
{
    /// <summary>
    /// DB-1's batching budget. Deletions are ordinary changes and count against it.
    ///
    /// <para><b>This is the change budget, NOT the drain's.</b> The owner ruled on 2026-08-13 that DB-4's
    /// twenty does not apply to the DRAIN; it applies to what may be batched into one download, which is
    /// exactly what a cleanup batch is.</para>
    /// </summary>
    public const int ChangeBudget = 20;

    /// <summary>Plan one cleanup batch.</summary>
    /// <param name="testsInFlight">
    /// Tests that have not yet drained. <b>Null means nothing established the drain state</b>, which is
    /// <see cref="CleanupOutcome.DrainStateUnknown"/> and not a licence to proceed. An EMPTY collection is
    /// a different statement — the drain ran and there is nothing outstanding — and is honoured as such.
    /// </param>
    /// <param name="graph">
    /// The computed reference graph. <b>Null means it was not available</b>, and eligibility is then
    /// provable for nothing.
    /// </param>
    public static CleanupReport Plan(
        IReadOnlyList<CleanupCandidate> candidates,
        IReadOnlyCollection<string>? testsInFlight,
        ReferenceGraph? graph)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (testsInFlight is null)
        {
            return new CleanupReport(CleanupOutcome.DrainStateUnknown, Array.Empty<CleanupDisposition>(),
                "nothing established whether tests are drained, so no removal may be planned. Unknown is not drained: DB-7's first rule exists because deleting something a running test depends on is the obvious way to make this dangerous.");
        }

        if (graph is null)
        {
            return new CleanupReport(CleanupOutcome.GraphNotAvailable, Array.Empty<CleanupDisposition>(),
                "no reference graph was supplied, so 'nothing references this' could be PROVEN for nothing. DB-7 makes eligibility graph-proven and never count-based, and a batch computed without the graph would be exactly the count-based one that rule forbids.");
        }

        if (candidates.Count == 0)
        {
            return new CleanupReport(CleanupOutcome.NothingExamined, Array.Empty<CleanupDisposition>(),
                "there were no candidates, so nothing was examined. Empty is not clean: a cleanup that looked at nothing is not a cleanup that found nothing to do.");
        }

        if (testsInFlight.Count > 0)
        {
            return new CleanupReport(CleanupOutcome.TestsNotDrained,
                candidates.Select(c => new CleanupDisposition(c, CleanupEligibility.Referenced,
                    $"{testsInFlight.Count} test(s) still in flight ({string.Join(", ", testsInFlight)}), so nothing is removable whatever the graph says.")).ToArray(),
                $"{testsInFlight.Count} test(s) have not drained. Cleanup runs on the same condition as the deferred queue (D24), and that condition is not met.");
        }

        var dispositions = new List<CleanupDisposition>();
        var budgetUsed = 0;

        foreach (var candidate in candidates)
        {
            if (candidate.Kind == RemovalKind.Unstated)
            {
                dispositions.Add(new CleanupDisposition(candidate, CleanupEligibility.KindNotStated,
                    $"'{candidate.Name}' does not say what kind of object it is, so what removing it would cost - scan time, object count, an instance DB a test still owns - is unknown."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(candidate.Authority) || string.IsNullOrWhiteSpace(candidate.Why))
            {
                dispositions.Add(new CleanupDisposition(candidate, CleanupEligibility.NoAuthorityRecorded,
                    $"'{candidate.Name}' records {(string.IsNullOrWhiteSpace(candidate.Authority) ? "no authority" : "no reason")}. DB-7 requires what, why AND on whose authority; a removal that cannot be recorded is the 'a block silently disappeared' failure this rule was written against."));
                continue;
            }

            var referrers = graph.Referrers(candidate.Name);
            if (referrers.Count > 0)
            {
                dispositions.Add(new CleanupDisposition(candidate, CleanupEligibility.Referenced,
                    $"'{candidate.Name}' is referenced by {string.Join(", ", referrers.OrderBy(r => r, StringComparer.Ordinal))}. A block can be unreferenced by VECTORS and still called by another BLOCK, which is why this is read from the graph and not from whether a test finished."));
                continue;
            }

            if (budgetUsed >= ChangeBudget)
            {
                dispositions.Add(new CleanupDisposition(candidate, CleanupEligibility.DeferredToNextBatch,
                    $"'{candidate.Name}' is eligible and does not fit: deletions are ordinary changes and count against DB-1's batching budget of {ChangeBudget}. It is deferred by name rather than dropped."));
                continue;
            }

            budgetUsed++;
            dispositions.Add(new CleanupDisposition(candidate, CleanupEligibility.Eligible,
                $"the reference graph shows nothing references '{candidate.Name}'. Deleting an FB/FC/DB/UDT is RUN-class, so this batch needs no disruptive boundary of its own."));
        }

        return new CleanupReport(CleanupOutcome.Planned, dispositions,
            $"tests are drained and the graph was available. {budgetUsed} of DB-1's {ChangeBudget}-change budget used.");
    }
}
