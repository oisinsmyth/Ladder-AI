using Harness.Map;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE INERT EXPECTATION'S CALL SITE — and the assertion that matters is
/// <c>Deployments == 0</c>, not the outcome value.</b>
///
/// <para><b>What was there.</b> <c>LoopRun.ToWireVector</c> built the declaration as
/// <c>Enumerable.Range(0, binding.ResultRegistersNeeded).ToDictionary(i =&gt; i, _ =&gt; (ushort)0)</c> —
/// every result register asserted to rest at zero, hardcoded — under a type whose own summary reads
/// <i>"D33's FIRST check, and it must be declared: a check with no expectation passes over anything."</i>
/// <b>That is not a declaration; it is an assumption wearing one's clothes.</b></para>
///
/// <para><b>Measured consequence, on real hardware.</b> One slot passed its inert gate only because its
/// published signals happen to rest at zero. A second could not pass it at all: a <c>-1</c> sentinel where
/// <c>0</c> is a measured PASS, a commanded input resting at whatever the pending index declares, alarm
/// bits honestly true after a restart, and a one-scan pulse that reads 1 in about one sample of five.</para>
///
/// <para><b>THE FIXTURES HERE REST AT NON-ZERO VALUES ON PURPOSE.</b> Every pre-existing fixture in this
/// repository rests at zero, so none of them can tell a declared expectation from the hardcoded one — the
/// trap that hid this for the life of the feature.</para>
/// </summary>
public class InertRestCallSiteTests
{
    /// <summary>The ordinary request, with one result signal's declaration REMOVED — the shape every binding had.</summary>
    private static LoopRequest Undeclared()
    {
        var request = LoopRunTests.Request();
        var binding = request.Bindings[0];

        return request with
        {
            Bindings = new[]
            {
                binding with
                {
                    ResultSources = new[] { binding.ResultSources[0], binding.ResultSources[1] with { Rest = null } },
                },
            },
        };
    }

    // -------------------------------------------------------------------------------------------------
    // The refusal, and WHERE it happens
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void AN_UNDECLARED_RESTING_VALUE_IS_REFUSED_BEFORE_ANYTHING_IS_DEPLOYED()
    {
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());

        var result = LoopRun.Execute(Undeclared(), gateway);

        Assert.Equal(LoopOutcome.RestNotDeclared, result.Outcome);

        // *** THE TWO THAT MATTER. *** The outcome only says the check fired; these say it fired in the
        // right PLACE. A check that refused after the download would cost a deployment every time.
        Assert.Equal(0, gateway.Deployments);
        Assert.Equal(0, gateway.Opens);

        Assert.Empty(result.Packages);
    }

    [Fact]
    public void THE_REFUSAL_NAMES_THE_SIGNAL_AND_THE_REMEDY()
    {
        var result = LoopRun.Execute(Undeclared(), new SimulatedGateway(LoopRunTests.Geometry()));

        Assert.Contains(Harness.Skeleton.TrivialBlock.DoneTag, result.Detail, StringComparison.Ordinal);
        Assert.Contains("THIS IS A REFUSAL AND NOT A ZERO", result.Detail, StringComparison.Ordinal);
        Assert.Contains("inertRest", result.Detail, StringComparison.Ordinal);
        Assert.Contains("assumedZeroRest", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void EVERY_SUBMITTED_VECTOR_IS_STILL_ACCOUNTED_FOR_ON_THIS_PATH()
    {
        // A stop before the wave is the run most likely to be read as "no results yet" rather than as
        // "every vector was never attempted", and an empty list says the first.
        var request = Undeclared();

        var result = LoopRun.Execute(request, new SimulatedGateway(LoopRunTests.Geometry()));

        Assert.Equal(request.Vectors.Count, result.Account.Submitted);
        Assert.Equal(request.Vectors.Count, result.Account.NeverAttempted);
    }

    // -------------------------------------------------------------------------------------------------
    // The migration escape: it works, it is per slot, and it is VISIBLE
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void THE_ASSUME_ZERO_CLAIM_LETS_THE_UNDECLARED_BINDING_RUN()
    {
        // The reason the escape exists: the ruling lands on a slot that is deployed and running against the
        // old behaviour, and a fail-closed gate that refuses working submissions on the day it lands is
        // removed within a week by someone who is right to.
        var undeclared = Undeclared();

        var migrated = undeclared with
        {
            Bindings = new[]
            {
                undeclared.Bindings[0] with
                {
                    AssumedZeroRest = true,
                    AssumedZeroRestBasis = "carried from the deployed binding; the signal is being declared this week",
                },
            },
        };

        var gateway = new SimulatedGateway(LoopRunTests.Geometry());
        var result = LoopRun.Execute(migrated, gateway);

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.Equal(1, gateway.Deployments);
    }

    [Fact]
    public void AND_THE_RUN_SAYS_IT_DEFAULTED_rather_than_letting_it_read_like_a_declaration()
    {
        var undeclared = Undeclared();

        var migrated = undeclared with
        {
            Bindings = new[]
            {
                undeclared.Bindings[0] with
                {
                    AssumedZeroRest = true,
                    AssumedZeroRestBasis = "carried from the deployed binding",
                },
            },
        };

        var result = LoopRun.Execute(migrated, new SimulatedGateway(LoopRunTests.Geometry()));

        // On the headline line, where a reader of RESULTS meets it — not in a design note.
        Assert.Contains("1 DEFAULTED", result.Detail, StringComparison.Ordinal);

        // And structurally, so a consumer can act on it rather than parse prose.
        Assert.NotNull(result.InertRest);
        Assert.Equal(1, result.InertRest!.Plans.Sum(p => p.DefaultedCount));
        Assert.Equal(1, result.InertRest!.Plans.Sum(p => p.DeclaredCount));
    }

    [Fact]
    public void THE_ESCAPE_WITHOUT_A_BASIS_IS_REFUSED_so_it_is_not_a_free_switch()
    {
        var undeclared = Undeclared();

        var noBasis = undeclared with
        {
            Bindings = new[] { undeclared.Bindings[0] with { AssumedZeroRest = true } },
        };

        var gateway = new SimulatedGateway(LoopRunTests.Geometry());
        var result = LoopRun.Execute(noBasis, gateway);

        Assert.Equal(LoopOutcome.RestNotDeclared, result.Outcome);
        Assert.Contains("gives no basis", result.Detail, StringComparison.Ordinal);
        Assert.Equal(0, gateway.Deployments);
    }

    // -------------------------------------------------------------------------------------------------
    // The over-fire converse — a gate that fires outside its scope is noise, and noise gets switched off
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void THE_UNAFFECTED_CASE_a_fully_declared_binding_deploys_and_runs()
    {
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());

        var result = LoopRun.Execute(LoopRunTests.Request(), gateway);

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.Equal(1, gateway.Deployments);
        Assert.NotEmpty(result.Packages);

        Assert.NotNull(result.InertRest);
        Assert.Equal(0, result.InertRest!.Plans.Sum(p => p.DefaultedCount));
    }

    [Fact]
    public void A_SLOT_RESTING_AT_A_NEGATIVE_SENTINEL_RUNS_WHEN_THE_PROGRAM_AGREES_and_is_refused_when_it_does_not()
    {
        // *** THE CASE THE OLD HARDCODED ZERO COULD NOT EXPRESS, DRIVEN THROUGH THE WHOLE LOOP. *** The
        // block under test genuinely holds its outputs at 0, so declaring a -1 rest must FAIL the inert
        // phase — which is what makes the run below evidence that the declared value reaches the wire at
        // all, rather than being carried and ignored.
        var request = LoopRunTests.Request();
        var binding = request.Bindings[0];

        var sentinel = request with
        {
            Bindings = new[]
            {
                binding with
                {
                    ResultSources = new[]
                    {
                        binding.ResultSources[0] with { Rest = InertRest.At("-1", "sentinel: no test performed") },
                        binding.ResultSources[1],
                    },
                },
            },
        };

        var gateway = new SimulatedGateway(LoopRunTests.Geometry());
        var result = LoopRun.Execute(sentinel, gateway);

        // It DEPLOYED — the declaration is well-formed, so the refusal above the device does not fire —
        // and then the inert phase disagreed with the device, which is the check doing its job.
        Assert.Equal(1, gateway.Deployments);
        Assert.Equal(LoopOutcome.Ran, result.Outcome);

        // NOT INERT — the declared -1 reached the wire and disagreed with a program that genuinely holds
        // its output at 0. Under the old hardcoded zero this run would have started happily, which is the
        // whole point: the declaration is consumed, not carried.
        Assert.All(result.Packages, p => Assert.Equal(SlotOutcome.NotInert, p.RunOutcome));
        Assert.All(result.Packages, p => Assert.False(p.ConclusiveAboutTheBlock));
    }
}
