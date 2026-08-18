using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// Build-plan item 2.5 — D33's inert phase and its TWO checks, and D37's later-scan commit.
///
/// <para>Most of what follows is a negative test, because both checks exist to refuse. A suite that only
/// fed this an already-inert program would be a suite that could not fail.</para>
/// </summary>
public class InertPhaseTests
{
    private static readonly BuildStamp Stamp = MirrorClientTests.Stamp;

    private static readonly ushort[] Vector = { 5, 10 };

    private static InertDeclaration Declaration(int quiescenceScans = 1) => new(
        new Dictionary<int, ushort> { [0] = 0, [1] = 0 },
        quiescenceScans);

    private static (MirrorClient Client, RecordingTransport Wire) Wired()
    {
        var map = MirrorClientTests.Map();
        var wire = new RecordingTransport(map, Stamp);
        return (new MirrorClient(map, wire, Stamp), wire);
    }

    // ---------------------------------------------------------------------------------------------
    // The happy path, and what it must do on the way
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Establishing_inert_lowers_the_start_bools_first_then_writes_the_vector()
    {
        // The reset is a LEVEL held for the whole inert period (D33), and the vector is what establishes
        // the NEXT test's start condition. Writing the vector while the block is still running would be
        // stimulating a test nobody started.
        var (client, wire) = Wired();

        var report = InertPhase.Establish(client, 0, Vector, Declaration());

        Assert.True(report.Established, report.Detail);

        var writes = wire.Log.Where(t => t.IsWrite).ToArray();
        Assert.Equal(3, writes.Length);
        Assert.Equal(0, writes[0].Values[0]);                     // start bools cleared - the reset LEVEL
        Assert.Equal(0, writes[1].Values[0]);                     // echo latches released (D33)
        Assert.Equal(new ushort[] { 5, 10 }, writes[2].Values);   // then the vector
    }

    [Fact]
    public void The_commit_raises_the_start_bools_on_a_LATER_scan_than_the_verify()
    {
        var (client, wire) = Wired();
        var report = InertPhase.Establish(client, 0, Vector, Declaration());

        var t0 = InertPhase.Commit(client, report, new[] { 0 });

        Assert.True(t0.Since(report.ScanAtVerify) > 0,
            $"T=0 was scan {t0} and the verify was scan {report.ScanAtVerify}. D37: releasing a reset and starting in one scan makes the outcome depend on rung order inside the block.");
        Assert.Equal(1, wire.StartBoolRegisters[0]);
    }

    // ---------------------------------------------------------------------------------------------
    // Check one: start conditions established
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_result_that_is_not_at_its_declared_start_value_refuses()
    {
        var (client, wire) = Wired();
        wire.SetResult(0, 1, 7);

        var report = InertPhase.Establish(client, 0, Vector, Declaration());

        Assert.Equal(InertOutcome.StartConditionsWrong, report.Outcome);
        Assert.Contains("R001 reads 7, declared 0", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_declaration_naming_a_register_the_slot_does_not_have_refuses_rather_than_passing_over_it()
    {
        var (client, _) = Wired();
        var declaration = new InertDeclaration(new Dictionary<int, ushort> { [9] = 0 });

        var report = InertPhase.Establish(client, 0, Vector, declaration);

        Assert.Equal(InertOutcome.StartConditionsWrong, report.Outcome);
        Assert.Contains("only 2 result register(s)", report.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Check two: dynamics quiescent — the one that earns its place
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_value_that_is_right_and_still_moving_is_not_inert()
    {
        // D33 consequence 3: "a model sitting at the right value while still integrating toward another
        // is not inert, and only the second check catches it." Here R000 reads its declared 0 at the
        // first observation and has moved by the second — check one passes, check two must not.
        var (client, wire) = Wired();
        var reads = 0;
        wire.OnTransaction = t =>
        {
            reads++;
            if (reads > 6) t.SetResult(0, 0, 3);
        };

        var report = InertPhase.Establish(client, 0, Vector, Declaration());

        Assert.Equal(InertOutcome.NotQuiescent, report.Outcome);
        Assert.Contains("R000 moved 0 -> 3", report.Detail, StringComparison.Ordinal);
        Assert.Contains("still integrating", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_quiescence_window_of_less_than_one_scan_is_refused_at_the_declaration()
    {
        var (client, _) = Wired();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InertPhase.Establish(client, 0, Vector, Declaration(quiescenceScans: 0)));
    }

    // ---------------------------------------------------------------------------------------------
    // A stalled scan counter is unobserved, not clean
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_stopped_scan_counter_makes_inert_unobservable_rather_than_established()
    {
        var (client, wire) = Wired();
        wire.ScansPerTransaction = 0;

        var report = InertPhase.Establish(client, 0, Vector, Declaration(), maxPolls: 5);

        Assert.Equal(InertOutcome.ScanCounterStalled, report.Outcome);
        Assert.Contains("Empty is not clean", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_commit_refuses_when_inert_was_never_established()
    {
        var (client, wire) = Wired();
        wire.SetResult(0, 0, 99);
        var report = InertPhase.Establish(client, 0, Vector, Declaration());

        var error = Assert.Throws<WireException>(() => InertPhase.Commit(client, report, new[] { 0 }));

        Assert.Contains("inert was not established", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_commit_refuses_when_the_scan_counter_will_not_advance_past_the_verify()
    {
        var (client, wire) = Wired();
        var report = InertPhase.Establish(client, 0, Vector, Declaration());
        wire.ScansPerTransaction = 0;

        var error = Assert.Throws<WireException>(() => InertPhase.Commit(client, report, new[] { 0 }, maxPolls: 4));

        Assert.Contains("later scan than the verify", error.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // D33: outputs are not recorded during inert
    // ---------------------------------------------------------------------------------------------

    // ---------------------------------------------------------------------------------------------
    // The declaration must COVER the band — "a check with no expectation passes over anything"
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A declaration that is silent about a register in the band. <b>The whole defect in one fixture:</b>
    /// before this, silence meant "not checked", and an unchecked register was indistinguishable from a
    /// quiet one.
    /// </summary>
    private static InertDeclaration OnlyR000 => new(new Dictionary<int, ushort> { [0] = 0 });

    [Fact]
    public void A_REGISTER_THE_DECLARATION_IS_SILENT_ABOUT_REFUSES_RATHER_THAN_BEING_PASSED_OVER()
    {
        var (client, _) = Wired();

        var report = InertPhase.Establish(client, 0, Vector, OnlyR000);

        Assert.Equal(InertOutcome.RestNotDeclared, report.Outcome);
        Assert.Contains("R001", report.Detail, StringComparison.Ordinal);
        Assert.Contains("A CHECK WITH NO EXPECTATION PASSES OVER ANYTHING", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_UNDECLARED_REGISTER_IS_ITS_OWN_OUTCOME_and_not_StartConditionsWrong()
    {
        // The values may be perfect; what is missing is anybody's statement of what they should be. Sending
        // a reader to look at the program for a fault in the DOCUMENT is the wrong place.
        var (client, wire) = Wired();
        wire.SetResult(0, 1, 0);

        Assert.Equal(InertOutcome.RestNotDeclared, InertPhase.Establish(client, 0, Vector, OnlyR000).Outcome);
    }

    // ---------------------------------------------------------------------------------------------
    // EXCLUDED — skipped by BOTH checks, and the second is the one that is easy to forget
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AN_EXCLUDED_REGISTER_IS_NOT_GATED_ON_ITS_VALUE()
    {
        var (client, wire) = Wired();
        wire.SetResult(0, 1, 7);

        var report = InertPhase.Establish(client, 0, Vector, new InertDeclaration(
            new Dictionary<int, ushort> { [0] = 0 },
            ExcludedResults: new Dictionary<int, string> { [1] = "one-scan pulse" }));

        Assert.True(report.Established, report.Detail);
    }

    [Fact]
    public void AN_EXCLUDED_REGISTER_IS_NOT_GATED_ON_QUIESCENCE_EITHER_which_is_the_case_that_motivates_it()
    {
        // 🔴 *** THE ONE-SCAN PULSE. *** It reads 1 in about one sample of five, so on the runs where it
        // happens to pass the value check it then MOVES between the two observations and fails the
        // quiescence check for exactly the same reason. Excluding it from the value check alone would leave
        // the coin toss in place one check further down — and the coin toss would be blamed on the block.
        var (client, wire) = Wired();
        var reads = 0;
        wire.OnTransaction = t =>
        {
            reads++;
            if (reads > 6) t.SetResult(0, 1, 1);
        };

        var report = InertPhase.Establish(client, 0, Vector, new InertDeclaration(
            new Dictionary<int, ushort> { [0] = 0 },
            ExcludedResults: new Dictionary<int, string> { [1] = "one-scan pulse: true for a single scan" }));

        Assert.True(report.Established, report.Detail);
    }

    [Fact]
    public void A_MOVING_REGISTER_THAT_IS_NOT_EXCLUDED_STILL_FAILS_QUIESCENCE()
    {
        // The converse of the test above, so that "excluded is skipped" cannot be satisfied by a quiescence
        // check that stopped working. Same fixture, same movement, no exclusion.
        var (client, wire) = Wired();
        var reads = 0;
        wire.OnTransaction = t =>
        {
            reads++;
            if (reads > 6) t.SetResult(0, 1, 1);
        };

        var report = InertPhase.Establish(client, 0, Vector, Declaration());

        Assert.Equal(InertOutcome.NotQuiescent, report.Outcome);
        Assert.Contains("R001 moved 0 -> 1", report.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The over-fire converse: a slot resting at NON-ZERO declared values must still pass
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_SLOT_RESTING_AT_ITS_DECLARED_NON_ZERO_VALUES_IS_ESTABLISHED()
    {
        // *** THE FIXTURE TRAP, HANDLED HEAD-ON. *** Every other fixture in this file rests at zero, so
        // none of them can tell a declared expectation from the old hardcoded one. This slot rests at a
        // -1 sentinel and a raised alarm — the two shapes measured on real hardware — and must pass.
        var (client, wire) = Wired();
        wire.SetResult(0, 0, 65535);
        wire.SetResult(0, 1, 1);

        var report = InertPhase.Establish(client, 0, Vector, new InertDeclaration(
            new Dictionary<int, ushort> { [0] = 65535, [1] = 1 }));

        Assert.True(report.Established, report.Detail);
    }

    [Fact]
    public void A_SLOT_THAT_IS_GENUINELY_DIRTY_IS_STILL_REFUSED_when_the_declared_rest_is_non_zero()
    {
        // And the other half of the converse: declaring a non-zero rest must not become a way of declaring
        // "anything goes". A register at 0 where -1 was declared is a fault, and 0 is the value the old
        // implementation would have accepted here.
        var (client, wire) = Wired();
        wire.SetResult(0, 0, 0);
        wire.SetResult(0, 1, 1);

        var report = InertPhase.Establish(client, 0, Vector, new InertDeclaration(
            new Dictionary<int, ushort> { [0] = 65535, [1] = 1 }));

        Assert.Equal(InertOutcome.StartConditionsWrong, report.Outcome);
        Assert.Contains("R000 reads 0, declared 65535", report.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // A defaulted expectation must never read like a declared one
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_FAILURE_ON_A_DEFAULTED_EXPECTATION_SAYS_THAT_NOBODY_DECLARED_IT()
    {
        // Handed a bare "reads 7, declared 0" a reader investigates the block. Told the 0 was assumed
        // rather than stated, they can weigh the other half — and the other half is where this defect was.
        var (client, wire) = Wired();
        wire.SetResult(0, 1, 7);

        var report = InertPhase.Establish(client, 0, Vector, new InertDeclaration(
            new Dictionary<int, ushort> { [0] = 0, [1] = 0 },
            DefaultedResults: new HashSet<int> { 1 }));

        Assert.Equal(InertOutcome.StartConditionsWrong, report.Outcome);
        Assert.Contains("NOBODY DECLARED THIS REGISTER'S RESTING VALUE", report.Detail, StringComparison.Ordinal);
        Assert.Contains("may be the expectation rather than the program", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_FAILURE_ON_A_DECLARED_EXPECTATION_CARRIES_NO_SUCH_CAVEAT()
    {
        // The converse, so the caveat cannot decay into boilerplate printed on everything.
        var (client, wire) = Wired();
        wire.SetResult(0, 1, 7);

        var report = InertPhase.Establish(client, 0, Vector, Declaration());

        Assert.Equal(InertOutcome.StartConditionsWrong, report.Outcome);
        Assert.DoesNotContain("NOBODY DECLARED", report.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The denominator, on the PASSING run
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_PASS_STATES_WHAT_IT_EXAMINED_because_a_green_that_examined_nothing_reads_the_same()
    {
        var (client, wire) = Wired();
        wire.SetResult(0, 1, 7);

        var report = InertPhase.Establish(client, 0, Vector, new InertDeclaration(
            new Dictionary<int, ushort> { [0] = 0 },
            ExcludedResults: new Dictionary<int, string> { [1] = "one-scan pulse" }));

        Assert.True(report.Established, report.Detail);
        Assert.Contains("INERT REST: 1 of 2 result register(s) gated", report.Detail, StringComparison.Ordinal);
        Assert.Contains("1 EXCLUDED by declaration, checked by NOBODY", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_PASS_NAMES_ITS_DEFAULTS_TOO()
    {
        var (client, _) = Wired();

        var report = InertPhase.Establish(client, 0, Vector, new InertDeclaration(
            new Dictionary<int, ushort> { [0] = 0, [1] = 0 },
            DefaultedResults: new HashSet<int> { 1 }));

        Assert.True(report.Established, report.Detail);
        Assert.Contains("OF WHICH 1 DEFAULTED", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_inert_observations_are_reported_separately_from_the_tests_results()
    {
        // "Establishing a start state moves outputs; recording that would put spurious activity in front
        // of every single test." The observations exist to justify the verdict, not to be results.
        var (client, _) = Wired();

        var report = InertPhase.Establish(client, 0, Vector, Declaration());

        Assert.IsType<InertReport>(report);
        Assert.Equal(2, report.FirstObservation.Length);
        Assert.Equal(2, report.SecondObservation.Length);
    }
}
