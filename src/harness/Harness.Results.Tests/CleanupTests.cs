using System.Reflection;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// <b>DB-7's cleanup stage — and its four rules are refusals here, not guidance.</b>
///
/// <para>Nothing in the design ever removes anything, and three things accumulate. The danger of fixing
/// that is obvious and is what the rules are made of: <i>deleting something a running test depends on.</i>
/// </para>
/// </summary>
public class CleanupTests
{
    private static CleanupCandidate Candidate(
        string name = "iDB_Test_1",
        RemovalKind kind = RemovalKind.InstanceDb,
        string why = "the test it was created for completed at wave 12",
        string authority = "coordinator, cleanup cadence") =>
        new(name, kind, why, authority);

    private static readonly IReadOnlyCollection<string> Drained = Array.Empty<string>();

    // ---------------------------------------------------------------------------------------------
    // Rule 1 — it runs when tests are DRAINED, and the drain state is COMPUTED
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void An_UNKNOWN_drain_state_is_not_a_drained_one()
    {
        var report = Cleanup.Plan(new[] { Candidate() }, testsInFlight: null, ReferenceGraph.Empty);

        Assert.Equal(CleanupOutcome.DrainStateUnknown, report.Outcome);
        Assert.Empty(report.Removals);
        Assert.Contains("Unknown is not drained", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_test_still_in_flight_stops_the_batch_WHATEVER_the_graph_says()
    {
        var report = Cleanup.Plan(new[] { Candidate() }, new[] { "V-7" }, ReferenceGraph.Empty);

        Assert.Equal(CleanupOutcome.TestsNotDrained, report.Outcome);
        Assert.Empty(report.Removals);
        Assert.Contains("V-7", report.Dispositions.Single().Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_drain_state_is_a_SET_and_not_a_bool_the_caller_asserts()
    {
        // A `bool drained` parameter would be the caller supplying the verdict — the defect this project
        // keeps finding, most recently in observability. The signature is the guard.
        var plan = typeof(Cleanup).GetMethod(nameof(Cleanup.Plan))!;

        Assert.DoesNotContain(plan.GetParameters(), p => p.ParameterType == typeof(bool));
    }

    // ---------------------------------------------------------------------------------------------
    // Rule 2 — eligibility is GRAPH-PROVEN, never count-based
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void With_NO_GRAPH_eligibility_is_provable_for_nothing()
    {
        var report = Cleanup.Plan(new[] { Candidate() }, Drained, graph: null);

        Assert.Equal(CleanupOutcome.GraphNotAvailable, report.Outcome);
        Assert.Empty(report.Removals);
    }

    [Fact]
    public void A_block_UNREFERENCED_BY_VECTORS_but_CALLED_BY_ANOTHER_BLOCK_is_retained()
    {
        // DB-7 names this case explicitly, and it is the one an age- or completion-based rule gets wrong.
        var report = Cleanup.Plan(
            new[] { Candidate("FC_Helper", RemovalKind.Block) },
            Drained,
            ReferenceGraph.Of(("FC_Helper", new[] { "FC_ControlMain" })));

        var disposition = report.Dispositions.Single();

        Assert.Equal(CleanupEligibility.Referenced, disposition.Eligibility);
        Assert.Contains("FC_ControlMain", disposition.Detail, StringComparison.Ordinal);
        Assert.Empty(report.Removals);
    }

    [Fact]
    public void NOTHING_IN_THIS_FILE_OFFERS_AN_AGE_A_COUNT_OR_A_LAST_USED_TIME()
    {
        // The rule is only as good as the absence of an easier alternative sitting next to it. This is the
        // "did not run" case for rule 2: every other test proves the graph is consulted, and none of them
        // would notice a LastUsed property being added beside it.
        var types = new[] { typeof(CleanupCandidate), typeof(CleanupDisposition), typeof(CleanupReport), typeof(ReferenceGraph), typeof(Cleanup) };

        var forbidden = types
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(m => m.Name)
            .Where(n => n.Contains("Age", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("LastUsed", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("Days", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("Older", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(forbidden);
    }

    // ---------------------------------------------------------------------------------------------
    // Rule 3 — every removal is recorded: what, why, on whose authority
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_candidate_with_NO_AUTHORITY_is_not_removed()
    {
        var report = Cleanup.Plan(new[] { Candidate(authority: "  ") }, Drained, ReferenceGraph.Empty);

        Assert.Equal(CleanupEligibility.NoAuthorityRecorded, report.Dispositions.Single().Eligibility);
        Assert.Empty(report.Removals);
    }

    [Fact]
    public void A_candidate_with_NO_REASON_is_not_removed_either()
    {
        var report = Cleanup.Plan(new[] { Candidate(why: "") }, Drained, ReferenceGraph.Empty);

        Assert.Equal(CleanupEligibility.NoAuthorityRecorded, report.Dispositions.Single().Eligibility);
    }

    [Fact]
    public void A_candidate_that_does_not_say_WHAT_KIND_it_is_is_not_removed()
    {
        var report = Cleanup.Plan(new[] { Candidate(kind: RemovalKind.Unstated) }, Drained, ReferenceGraph.Empty);

        Assert.Equal(CleanupEligibility.KindNotStated, report.Dispositions.Single().Eligibility);
    }

    [Fact]
    public void The_record_carries_WHAT_WHY_and_ON_WHOSE_AUTHORITY_for_every_removal()
    {
        var report = Cleanup.Plan(new[] { Candidate() }, Drained, ReferenceGraph.Empty);
        var rendered = report.Render();

        Assert.Equal(CleanupOutcome.Planned, report.Outcome);
        Assert.Contains("REMOVE InstanceDb 'iDB_Test_1'", rendered, StringComparison.Ordinal);
        Assert.Contains("why: the test it was created for completed at wave 12", rendered, StringComparison.Ordinal);
        Assert.Contains("authority: coordinator, cleanup cadence", rendered, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Rule 4 — deletions are ordinary changes and count against the budget
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_TWENTY_FIRST_eligible_removal_is_DEFERRED_BY_NAME_and_never_dropped()
    {
        var candidates = Enumerable.Range(1, 25).Select(i => Candidate($"iDB_Test_{i}")).ToArray();

        var report = Cleanup.Plan(candidates, Drained, ReferenceGraph.Empty);

        Assert.Equal(Cleanup.ChangeBudget, report.Removals.Count);
        Assert.Equal(5, report.Deferred.Count);
        Assert.Equal(25, report.Dispositions.Count);
        Assert.Contains("iDB_Test_21", report.Deferred[0].Candidate.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void A_removal_needs_NO_DISRUPTIVE_BOUNDARY_because_deleting_a_block_is_RUN_class()
    {
        var report = Cleanup.Plan(new[] { Candidate() }, Drained, ReferenceGraph.Empty);

        Assert.Contains("RUN-class", report.Removals.Single().Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Empty is not clean; and the zero values
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_cleanup_that_LOOKED_AT_NOTHING_is_not_a_cleanup_that_found_nothing_to_do()
    {
        var report = Cleanup.Plan(Array.Empty<CleanupCandidate>(), Drained, ReferenceGraph.Empty);

        Assert.Equal(CleanupOutcome.NothingExamined, report.Outcome);
        Assert.NotEqual(CleanupOutcome.Planned, report.Outcome);
    }

    [Fact]
    public void Every_enum_here_has_an_UNUSABLE_zero_value()
    {
        Assert.Equal(RemovalKind.Unstated, default(RemovalKind));
        Assert.Equal(CleanupEligibility.Unstated, default(CleanupEligibility));
        Assert.Equal(CleanupOutcome.Unstated, default(CleanupOutcome));
    }
}
