using Harness.Map;
using Harness.Results;
using Harness.Wire;

namespace Harness.Results.Tests;

/// <summary>
/// DB-8's stimulus check — the element with teeth.
///
/// <para><b>Every test here supplies a HEALTHY-LOOKING content set</b>, because that is the point: a
/// frozen mirror is perfectly self-consistent and nothing in the data distinguishes it from a live run.
/// The check never looks at content at all, so these tests vary only the liveness evidence.</para>
/// </summary>
public class StimulusCheckTests
{
    private static readonly BuildStamp Build = new(0xA93F2C71);

    private static StimulusEvidence Healthy(
        bool commanded = true,
        bool executed = true,
        long scanAdvance = 40,
        int roundTrips = 8,
        ManifestPresence manifest = ManifestPresence.Loaded,
        VersionReport? version = null) =>
        new(commanded, executed, scanAdvance, roundTrips, manifest,
            version ?? new VersionReport(VersionOutcome.Confirmed, Build.Value, Build.Value, 3, 1, "confirmed"));

    private static StimulusReport Check(StimulusEvidence? evidence, int roundTrips = 8) =>
        StimulusCheck.Check(evidence, StimulusExpectation.AtLeastOneScanPerRoundTrip(roundTrips), Build);

    [Fact]
    public void A_live_run_is_confirmed()
    {
        var report = Check(Healthy());

        Assert.True(report.Confirmed, report.Detail);
        Assert.Contains("advanced 40 scan(s) across 8 round trip(s)", report.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // THE FROZEN MIRROR — the case the whole element exists for
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_counter_that_never_moved_is_a_FROZEN_MIRROR_and_not_a_pass()
    {
        var report = Check(Healthy(scanAdvance: 0));

        Assert.Equal(StimulusOutcome.CounterFrozen, report.Outcome);
        Assert.Contains("perfectly self-consistent", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void MOVED_IS_NOT_ADVANCED_BY_THE_EXPECTED_AMOUNT()
    {
        // The distinction the coordinator named: a counter that ticked once across a run that issued
        // forty round trips has "moved", and a check keyed on movement would confirm it.
        var report = Check(Healthy(scanAdvance: 1, roundTrips: 40), roundTrips: 40);

        Assert.Equal(StimulusOutcome.CounterAdvancedTooLittle, report.Outcome);
        Assert.Contains("MOVED is not ADVANCED BY THE EXPECTED AMOUNT", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_floor_SCALES_WITH_THE_WORK_DONE_rather_than_being_a_constant()
    {
        // Twenty scans is plenty for a short run and not enough for a long one. A fixed threshold would
        // pass the second, which is the run with more opportunity to have gone wrong.
        Assert.True(Check(Healthy(scanAdvance: 20, roundTrips: 8), roundTrips: 8).Confirmed);
        Assert.Equal(StimulusOutcome.CounterAdvancedTooLittle,
            Check(Healthy(scanAdvance: 20, roundTrips: 60), roundTrips: 60).Outcome);
    }

    // ---------------------------------------------------------------------------------------------
    // The four different kinds of "it did not run"
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_slot_never_commanded_is_NeverRan_and_says_nothing_about_the_block()
    {
        var report = Check(Healthy(commanded: false));

        Assert.Equal(StimulusOutcome.NeverRan, report.Outcome);
        Assert.Contains("not a failing test", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_slot_commanded_whose_block_never_ran_is_a_DIFFERENT_fact_from_never_commanded()
    {
        // Both are "the test did not happen" and they call for different actions: one is the wave's plan,
        // the other is the copy layer or the start-bool address. Collapsing them would leave an agent to
        // guess which.
        var report = Check(Healthy(executed: false));

        Assert.Equal(StimulusOutcome.CommandedButDidNotRun, report.Outcome);
        Assert.Contains("editing correct logic", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_object_absent_from_the_load_manifest_separates_WRONG_from_NEVER_LOADED()
    {
        var report = Check(Healthy(manifest: ManifestPresence.Absent));

        Assert.Equal(StimulusOutcome.NotLoaded, report.Outcome);
    }

    [Fact]
    public void A_manifest_that_was_not_available_is_not_the_same_as_one_that_said_LOADED()
    {
        // Section 9c forbids inferring transfer from absence. NotAvailable does not block confirmation —
        // the other evidence stands — but it is recorded rather than silently read as Loaded, and it
        // earns a caveat on the stamp.
        var report = Check(Healthy(manifest: ManifestPresence.NotAvailable));

        Assert.True(report.Confirmed, report.Detail);
        Assert.Contains("not positively evidenced", report.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The build actually running
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_version_register_that_did_not_confirm_makes_the_run_unreadable()
    {
        var stale = new VersionReport(VersionOutcome.Stale, 0x0BADF00D, Build.Value, 3, 1, "a different build");

        Assert.Equal(StimulusOutcome.WrongBuildRunning, Check(Healthy(version: stale)).Outcome);
    }

    [Fact]
    public void A_version_check_that_confirmed_a_DIFFERENT_QUESTION_is_not_a_confirmation()
    {
        // The one shape a CONFIRMED report can still be wrong in: it says "the program is what I asked
        // about", and this asks whether it asked about the right thing. Nothing else in the package would
        // have noticed.
        var confirmedElsewhere = new VersionReport(VersionOutcome.Confirmed, 0x11112222, 0x11112222, 3, 1, "confirmed");

        var report = Check(Healthy(version: confirmedElsewhere));

        Assert.Equal(StimulusOutcome.WrongBuildRunning, report.Outcome);
        Assert.Contains("confirmation of the wrong question", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void No_evidence_at_all_is_its_own_outcome_and_never_a_pass()
    {
        var report = Check(null);

        Assert.Equal(StimulusOutcome.NotChecked, report.Outcome);
        Assert.False(report.Confirmed);
        Assert.Contains("reads exactly like a healthy run", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void No_expectation_is_also_NotChecked_rather_than_a_check_against_zero()
    {
        Assert.Equal(StimulusOutcome.NotChecked, StimulusCheck.Check(Healthy(), null, Build).Outcome);
    }

    [Fact]
    public void An_expectation_derived_from_no_round_trips_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StimulusExpectation.AtLeastOneScanPerRoundTrip(0));
    }

    [Fact]
    public void The_check_looks_at_NO_RESULT_CONTENT_at_all()
    {
        // Stated as a property of the type: StimulusEvidence carries no register, no value and no
        // assertion. Liveness established from content is liveness established from the thing whose
        // trustworthiness is in question.
        var carried = typeof(StimulusEvidence).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(new[] { "Commanded", "Executed", "Manifest", "RoundTrips", "ScanAdvance", "Version" }, carried);
    }
}
