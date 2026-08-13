using Harness.Loop;
using Harness.Map;
using Harness.Results;
using Harness.Skeleton;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// X-I rule 2's run-loop half: <b>the loop refuses to continue past a model that did not pass.</b>
///
/// <para>Two properties are tested with equal deliberateness, and the second is the one that gets
/// forgotten: the STOP fires when a model fails, and <b>a sequence with no failure is UNTOUCHED</b>.
/// A correctly-built gate asked in the wrong order refuses submissions that never depended on it, and
/// <i>a gate firing outside its scope is noise, and noise gets switched off.</i></para>
///
/// <para><b>The stop path is driven BOTH ways</b> (Y7b): once through the real
/// <see cref="LoopRun.Execute"/> with the genuinely defective block, and once through an injected
/// runner. If nothing could produce a failed model wave set, the stop path would be unreachable and its
/// test would prove nothing.</para>
/// </summary>
public class WaveSetSequenceTests
{
    private static WaveSetSubmission Set(string id, WaveSetRole role, LoopRequest request, params string[] dependsOn) =>
        new(id, role, request, dependsOn);

    private static SequenceResult Run(IReadOnlyList<WaveSetSubmission> sets, Func<WaveSetSubmission, LoopResult>? run = null) =>
        WaveSetSequence.Execute(sets, new SimulatedGateway(LoopRunTests.Geometry()), null, run);

    private static WaveSetResult Of(SequenceResult result, string id) =>
        result.Sets.Single(s => s.Id == id);

    // ---------------------------------------------------------------------------------------------
    // THE STOP, through the REAL loop and a REAL defective block
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// *** THE WHOLE POINT. *** The model wave set runs the deliberately defective block and comes back
    /// FAIL. The consumer must never be handed to the loop — its results would otherwise be produced and
    /// believed after the model they rest on failed.
    /// </summary>
    [Fact]
    public void A_CONSUMER_WHOSE_MODEL_FAILED_IS_NEVER_HANDED_TO_THE_LOOP()
    {
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());
        var handed = new List<string>();

        var result = WaveSetSequence.Execute(
            new[]
            {
                Set("M", WaveSetRole.Model, LoopRunTests.Request(defect: TrivialBlockDefect.OffByOneAtTheLimit)),
                Set("C", WaveSetRole.Consumer, LoopRunTests.Request(), "M"),
            },
            gateway,
            run: set =>
            {
                handed.Add(set.Id);
                return LoopRun.Execute(set.Request, gateway);
            });

        Assert.Equal(WaveSetOutcome.RanAndFailed, Of(result, "M").Outcome);
        Assert.Equal(WaveSetOutcome.NotRunBecauseAModelDidNotPass, Of(result, "C").Outcome);

        // *** NOT "the consumer failed" — the consumer NEVER RAN. ***
        Assert.Equal(new[] { "M" }, handed);
        Assert.Null(Of(result, "C").Result);
        Assert.True(result.Stopped);
    }

    /// <summary>
    /// The stop happens BEFORE the loop is called, so nothing reaches the device. A stop that ran the
    /// wave and discarded the answer would still have stopped the CPU and still have written to the
    /// controller.
    /// </summary>
    [Fact]
    public void The_stop_happens_before_ANY_deployment_is_attempted()
    {
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());

        WaveSetSequence.Execute(
            new[]
            {
                Set("M", WaveSetRole.Model, LoopRunTests.Request(defect: TrivialBlockDefect.OffByOneAtTheLimit)),
                Set("C", WaveSetRole.Consumer, LoopRunTests.Request(), "M"),
            },
            gateway,
            run: set => LoopRun.Execute(set.Request, gateway));

        // One deployment: the model's. The consumer's was never attempted.
        Assert.Equal(1, gateway.Deployments);
    }

    [Fact]
    public void The_stopped_set_NAMES_the_model_and_says_why_rather_than_being_dropped()
    {
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());

        var result = WaveSetSequence.Execute(
            new[]
            {
                Set("M", WaveSetRole.Model, LoopRunTests.Request(defect: TrivialBlockDefect.OffByOneAtTheLimit)),
                Set("C", WaveSetRole.Consumer, LoopRunTests.Request(), "M"),
            },
            gateway,
            run: set => LoopRun.Execute(set.Request, gateway));

        var stopped = Of(result, "C");
        Assert.Contains("'M'", stopped.Detail, StringComparison.Ordinal);
        Assert.Contains("ran and FAILED", stopped.Detail, StringComparison.Ordinal);
        Assert.Contains("wrong answer that looks like a result", stopped.Detail, StringComparison.Ordinal);

        // The set is IN the list. Dropping it would make "we stopped" indistinguishable from "there was
        // nothing to run".
        Assert.Equal(2, result.Sets.Count);
        Assert.Single(result.NotRun);
    }

    // ---------------------------------------------------------------------------------------------
    // THE UNAFFECTED CASE — tested as deliberately as the stop
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>A sequence with no failed model must be untouched.</b> A gate firing outside its scope is
    /// noise, and noise gets switched off.
    /// </summary>
    [Fact]
    public void A_SEQUENCE_WITH_NO_FAILURE_RUNS_EVERY_SET_AND_IS_UNTOUCHED()
    {
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());
        var handed = new List<string>();

        var result = WaveSetSequence.Execute(
            new[]
            {
                Set("M", WaveSetRole.Model, LoopRunTests.Request()),
                Set("C", WaveSetRole.Consumer, LoopRunTests.Request(), "M"),
            },
            gateway,
            run: set =>
            {
                handed.Add(set.Id);
                return LoopRun.Execute(set.Request, gateway);
            });

        Assert.Equal(new[] { "M", "C" }, handed);
        Assert.Equal(WaveSetOutcome.Ran, Of(result, "M").Outcome);
        Assert.Equal(WaveSetOutcome.Ran, Of(result, "C").Outcome);
        Assert.False(result.Stopped);
        Assert.Empty(result.NotRun);
        Assert.Contains("UNAFFECTED case", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_model_only_sequence_is_untouched_because_nothing_depends_on_anything()
    {
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());

        var result = WaveSetSequence.Execute(
            new[] { Set("M", WaveSetRole.Model, LoopRunTests.Request(defect: TrivialBlockDefect.OffByOneAtTheLimit)) },
            gateway,
            run: set => LoopRun.Execute(set.Request, gateway));

        // The model failed and NOTHING was stopped, because nothing rested on it. The gate must not fire
        // where it has no subject.
        Assert.Equal(WaveSetOutcome.RanAndFailed, Of(result, "M").Outcome);
        Assert.False(result.Stopped);
    }

    [Fact]
    public void A_consumer_of_a_DIFFERENT_model_is_untouched_when_an_unrelated_model_fails()
    {
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());

        var result = Run(
            new[]
            {
                Set("M_bad", WaveSetRole.Model, LoopRunTests.Request()),
                Set("M_good", WaveSetRole.Model, LoopRunTests.Request()),
                Set("C", WaveSetRole.Consumer, LoopRunTests.Request(), "M_good"),
            },
            run: set => set.Id == "M_bad" ? Failed() : Ran());

        Assert.Equal(WaveSetOutcome.RanAndFailed, Of(result, "M_bad").Outcome);
        Assert.Equal(WaveSetOutcome.Ran, Of(result, "C").Outcome);
        Assert.False(result.Stopped);
    }

    // ---------------------------------------------------------------------------------------------
    // THE STOP, DRIVEN DIRECTLY (Y7b) — and the three not-passing states kept apart
    // ---------------------------------------------------------------------------------------------

    private static LoopResult Ran() =>
        LoopRun.Execute(LoopRunTests.Request(), new SimulatedGateway(LoopRunTests.Geometry()));

    private static LoopResult Failed() =>
        LoopRun.Execute(LoopRunTests.Request(defect: TrivialBlockDefect.OffByOneAtTheLimit), new SimulatedGateway(LoopRunTests.Geometry()));

    private static LoopResult NeverDeployed() =>
        LoopRun.Execute(LoopRunTests.Request(), new RefusingDeviceGateway());

    /// <summary>
    /// *** A MODEL THAT NEVER RAN IS NOT A MODEL THAT PASSED. *** X-I rule 2 needs a PASSING result, and
    /// "no failures" is also what a run that learned nothing looks like.
    /// </summary>
    [Fact]
    public void A_MODEL_THAT_NEVER_DEPLOYED_ALSO_STOPS_THE_CONSUMER()
    {
        var result = Run(
            new[]
            {
                Set("M", WaveSetRole.Model, LoopRunTests.Request()),
                Set("C", WaveSetRole.Consumer, LoopRunTests.Request(), "M"),
            },
            run: set => set.Id == "M" ? NeverDeployed() : Ran());

        Assert.Equal(WaveSetOutcome.RanAndLearnedNothing, Of(result, "M").Outcome);
        Assert.Equal(WaveSetOutcome.NotRunBecauseAModelDidNotPass, Of(result, "C").Outcome);
    }

    [Fact]
    public void FAILED_and_LEARNED_NOTHING_are_reported_as_DIFFERENT_facts()
    {
        var failed = Run(new[] { Set("M", WaveSetRole.Model, LoopRunTests.Request()) }, _ => Failed());
        var nothing = Run(new[] { Set("M", WaveSetRole.Model, LoopRunTests.Request()) }, _ => NeverDeployed());

        Assert.NotEqual(Of(failed, "M").Outcome, Of(nothing, "M").Outcome);
        Assert.Contains("RAN AND FAILED", Of(failed, "M").Detail, StringComparison.Ordinal);
        Assert.Contains("NOT the same as passing", Of(nothing, "M").Detail, StringComparison.Ordinal);
    }

    /// <summary>A stop CASCADES: a consumer of a stopped consumer is not run either, and says why.</summary>
    [Fact]
    public void The_stop_cascades_and_the_second_hop_says_the_first_was_itself_not_run()
    {
        var result = Run(
            new[]
            {
                Set("M", WaveSetRole.Model, LoopRunTests.Request()),
                Set("C1", WaveSetRole.Consumer, LoopRunTests.Request(), "M"),
                Set("C2", WaveSetRole.Consumer, LoopRunTests.Request(), "C1"),
            },
            run: set => set.Id == "M" ? Failed() : Ran());

        Assert.Equal(WaveSetOutcome.NotRunBecauseAModelDidNotPass, Of(result, "C1").Outcome);
        Assert.Equal(WaveSetOutcome.NotRunBecauseAModelDidNotPass, Of(result, "C2").Outcome);
        Assert.Contains("was itself not run", Of(result, "C2").Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // SHAPE — refused before anything runs
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_CONSUMER_DEPENDING_ON_NOTHING_IS_REFUSED_BECAUSE_THE_ORDERING_WOULD_BE_VACUOUS()
    {
        var ran = 0;
        var result = Run(new[] { Set("C", WaveSetRole.Consumer, LoopRunTests.Request()) }, _ => { ran++; return Ran(); });

        Assert.All(result.Sets, s => Assert.Equal(WaveSetOutcome.Refused, s.Outcome));
        Assert.Equal(0, ran);
        Assert.Contains("cannot be stopped by anything", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_SET_WITH_NO_ROLE_IS_REFUSED_RATHER_THAN_TREATED_AS_EITHER()
    {
        var result = Run(new[] { Set("X", WaveSetRole.Unstated, LoopRunTests.Request()) }, _ => Ran());

        Assert.Contains("declares no role", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_DEPENDENCY_THAT_COMES_LATER_IS_REFUSED_BECAUSE_THE_MODEL_WOULD_RUN_AFTER_THE_CONSUMER()
    {
        var result = Run(
            new[]
            {
                Set("C", WaveSetRole.Consumer, LoopRunTests.Request(), "M"),
                Set("M", WaveSetRole.Model, LoopRunTests.Request()),
            },
            _ => Ran());

        Assert.Contains("comes LATER in the sequence", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_DEPENDENCY_ON_A_SET_THAT_IS_NOT_HERE_IS_REFUSED()
    {
        var result = Run(new[] { Set("C", WaveSetRole.Consumer, LoopRunTests.Request(), "M_absent") }, _ => Ran());

        Assert.Contains("A dependency on nothing cannot stop anything", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_EMPTY_SEQUENCE_HAS_NOT_SHOWN_THAT_THE_STOP_WORKS_AND_SAYS_SO()
    {
        var result = Run(Array.Empty<WaveSetSubmission>(), _ => Ran());

        Assert.Empty(result.Sets);
        Assert.Contains("Empty is not clean", result.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// The run loop's identity, for a <c>StopOnFailedWaveSetGate</c> declaration to name. Pinned so that
    /// a declaration citing it cannot silently come to be about a different loop.
    /// </summary>
    [Fact]
    public void The_run_loop_version_is_stable_and_nameable()
    {
        Assert.Equal("Harness.Loop.WaveSetSequence/1", WaveSetSequence.RunLoopVersion);
    }
}
