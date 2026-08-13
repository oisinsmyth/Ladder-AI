using Harness.Loop;
using Harness.Map;
using Harness.Results;
using Harness.Skeleton;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// *** PHASE 5's GATE: A BLOCK GOES FROM REQUEST TO TESTED WITHOUT A HUMAN IN THE INNER LOOP, AND THE
/// RESULT PACKAGE TELLS THE AUTHORING AGENT SOMETHING IT COULD ACT ON. ***
///
/// <para>Every piece in this loop is the real one — the map, the gate, the copy layer, the retention
/// assertion, the client, the wave, the package — except the CPU, which is a simulator that EXECUTES
/// THE GENERATED IR rather than re-implementing what it means. What remains to be shown on hardware is
/// on <see cref="LoopResult.OwedOnTheDevice"/>, and it is a deliverable rather than an apology.</para>
/// </summary>
public class LoopRunTests
{
    private const int MirrorBase = 4000;
    private const int ProgramBase = 3000;

    private static MirrorGeometry Geometry() => MirrorGeometry.ForCpu1214C(256, MirrorBase);

    private static AssertionEnumeration Enumeration(AssertionForm form = AssertionForm.When, string enumerator = "agent-c") =>
        AssertionEnumeration.Of(
            new[] { "REQ-014" },
            new[] { "REQ-014:3f9a1c" },
            new Dictionary<string, AssertionForm> { ["REQ-014:3f9a1c"] = form },
            enumerator);

    private static SubmissionVector Vector(
        int step = 5, int limit = 10, string expected = "10", int settlingScans = 3,
        AssertionForm form = AssertionForm.When, string author = "agent-b",
        InstrumentationMode mode = InstrumentationMode.Sampled, int window = 20, string? kills = "a ramp that overshoots the limit by one step") =>
        new("V-1", "S0", 0, new AgentIdentity(author),
            new Basis("REQ-014", "REQ-014:3f9a1c"),
            new Dictionary<string, string> { [TrivialBlock.StepTag] = step.ToString(), [TrivialBlock.LimitTag] = limit.ToString() },
            TrivialBlock.StartTag,
            new[] { new ObservabilityDeclaration(TrivialBlock.CountTag, SignalNature.PersistentState, mode, window, expected) },
            form,
            new SettlingDeclaration("count unchanged across 3 consecutive scans", new[] { TrivialBlock.CountTag }, settlingScans),
            MaxDurationScans: 20,
            Array.Empty<BlacklistEntry>(),
            CompressionFactor: 1,
            new[] { "ramp-to-limit" },
            TrivialBlock.DoneTag,
            kills);

    private static LoopRequest Request(
        SubmissionVector? vector = null,
        AssertionEnumeration? enumeration = null,
        TrivialBlockDefect defect = TrivialBlockDefect.None,
        IReadOnlySet<string>? conflicts = null,
        FidelityDeclaration? fidelity = null) =>
        new(new[] { vector ?? Vector() },
            enumeration ?? Enumeration(),
            fidelity ?? FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true),
            new AgentIdentity("agent-a"),
            conflicts ?? new HashSet<string>(),
            Geometry(),
            new[] { new SlotRequest("S0", 2, 2) },
            new[] { TrivialBlock.Binding("S0") },
            new CopyLayerNaming(BlockNumber: 900),
            TrivialBlock.Generate(ProgramBase, blockNumber: 901, defect),
            RuntimeCompression: 1);

    private static (LoopResult Result, SimulatedGateway Gateway) Run(LoopRequest? request = null, SimulatedGateway? gateway = null)
    {
        var g = gateway ?? new SimulatedGateway(Geometry());
        return (LoopRun.Execute(request ?? Request(), g), g);
    }

    // ---------------------------------------------------------------------------------------------
    // THE GATE ITSELF
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void GREEN_a_correct_block_goes_from_SUBMISSION_to_a_PASS_with_no_human_in_the_loop()
    {
        var (result, _) = Run();

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        var package = Assert.Single(result.Packages);
        Assert.Equal(ResultVerdict.Pass, package.Verdict);
        Assert.True(result.LearnedAnything);
    }

    [Fact]
    public void RED_the_deliberately_defective_block_comes_back_FAIL_and_says_WHAT_TO_DO()
    {
        // The phase-2 defect, driven through the whole loop: one operator changed in the IR, which
        // converts to a different SimaticML Part. The package does not merely say FAIL - it says fix the
        // block against the SPECIFICATION, and names the clause and the assertion it came from.
        var (result, _) = Run(Request(defect: TrivialBlockDefect.OffByOneAtTheLimit));

        var package = Assert.Single(result.Packages);
        Assert.Equal(ResultVerdict.Fail, package.Verdict);
        Assert.True(package.ConclusiveAboutTheBlock);
        Assert.Contains("NOT AGAINST THE VECTOR", package.WhatToDoNext, StringComparison.Ordinal);
        Assert.Equal("REQ-014:3f9a1c", package.Assertions[0].AssertionId);
        Assert.Equal("15", package.Assertions[0].Observed);
    }

    [Fact]
    public void The_defect_changes_the_BUILD_STAMP_so_the_two_runs_are_different_downloads()
    {
        var clean = Run().Result;
        var buggy = Run(Request(defect: TrivialBlockDefect.OffByOneAtTheLimit)).Result;

        Assert.NotEqual(clean.Packages[0].Stamp.ProgramVersion, buggy.Packages[0].Stamp.ProgramVersion);
    }

    // ---------------------------------------------------------------------------------------------
    // 1. THE LOOP CANNOT REPORT A RESULT IT DID NOT ESTABLISH
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_FROZEN_MIRROR_YIELDS_STALE_AND_THE_LOOP_CANNOT_UPGRADE_IT()
    {
        // The registers agree with the expectation in every position, because nothing ever changed them.
        // The wave "ran"; DB-8's precedence puts liveness before content and the loop honours it rather
        // than deciding for itself that a completed wave is a result.
        var gateway = new SimulatedGateway(Geometry(), scansPerTransaction: 0);
        var (result, _) = Run(gateway: gateway);

        Assert.NotEqual(LoopOutcome.Ran, result.Outcome);
        Assert.Empty(result.Packages);
        Assert.False(result.LearnedAnything);
    }

    [Fact]
    public void The_loop_exposes_NO_verdict_of_its_own()
    {
        // DB-8 owns verdicts. A loop that computed one would be a second opinion on the one question the
        // design has already answered carefully, and the two would drift.
        var members = typeof(LoopResult).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain(members, m => m.Contains("Pass", StringComparison.OrdinalIgnoreCase)
                                         || m.Contains("Fail", StringComparison.OrdinalIgnoreCase)
                                         || m.Contains("Verdict", StringComparison.OrdinalIgnoreCase)
                                         || m.Contains("Success", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LearnedAnything_is_NOT_the_absence_of_failures()
    {
        // A run of six Stale packages has no failures and has learned nothing. A caller reaching for
        // "no Fail" would count it a success - the same trap ConclusiveAboutTheBlock closes one level down.
        var (result, _) = Run(gateway: new SimulatedGateway(Geometry(), scansPerTransaction: 0));

        Assert.DoesNotContain(result.Packages, p => p.Verdict == ResultVerdict.Fail);
        Assert.False(result.LearnedAnything);
    }

    [Fact]
    public void A_caller_cannot_read_an_empty_package_list_as_a_clean_one()
    {
        var (result, _) = Run(gateway: new SimulatedGateway(Geometry()) { Refuse = true });

        var error = Assert.Throws<InvalidOperationException>(() => result.RequireRan());
        Assert.Contains("did not run", error.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 2. THE GATE RUNS BEFORE THE WAVE IS SPENT
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AN_INADMISSIBLE_SUBMISSION_NEVER_REACHES_THE_DEVICE_AT_ALL()
    {
        // The property, asserted on the gateway's own call counts: not "the deployment failed", but
        // nothing was ever handed to it.
        var (result, gateway) = Run(Request(vector: Vector(author: "agent-a")));

        Assert.Equal(LoopOutcome.NotAdmissible, result.Outcome);
        Assert.Equal(0, gateway.Deployments);
        Assert.Equal(0, gateway.Opens);
        Assert.Empty(result.Packages);
        Assert.Contains("nothing was deployed", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_gate_that_COULD_NOT_RUN_also_stops_the_loop_before_the_device()
    {
        // NOT CHECKED fails closed all the way up: an absent conflict graph makes the blacklist gate
        // unrunnable, and an unrunnable gate is not a passed one.
        var request = Request() with { ComputedConflicts = null };
        var gateway = new SimulatedGateway(Geometry());

        var result = LoopRun.Execute(request, gateway);

        Assert.Equal(LoopOutcome.NotAdmissible, result.Outcome);
        Assert.Equal(0, gateway.Deployments);
        Assert.NotEmpty(result.Gate!.NotChecked);
    }

    [Fact]
    public void An_unobservable_vector_is_refused_before_the_wave_rather_than_after_it()
    {
        // A window below the floor. Nothing about this needs a device to discover, and discovering it
        // after a download would have cost a wave to learn something arithmetic could say for free.
        var (result, gateway) = Run(Request(vector: Vector(window: 2)));

        Assert.Equal(LoopOutcome.NotAdmissible, result.Outcome);
        Assert.Equal(0, gateway.Deployments);
        Assert.Contains(result.Gate!.Refused, g => g.Gate.StartsWith("5 observability", StringComparison.Ordinal));
    }

    [Fact]
    public void The_0_1b_assertion_runs_before_the_device_too()
    {
        // A program whose own tags sit inside the retentive window. Generated, asserted, refused - and
        // no deployment attempted.
        var request = Request() with { ProgramUnderTest = TrivialBlock.Generate(100, 901) };
        var gateway = new SimulatedGateway(Geometry());

        var result = LoopRun.Execute(request, gateway);

        Assert.Equal(LoopOutcome.NotAssertable, result.Outcome);
        Assert.Equal(0, gateway.Deployments);
        Assert.False(result.Retention!.Passed);
    }

    [Fact]
    public void A_map_that_cannot_be_derived_stops_before_the_gate_and_says_the_gate_did_not_run()
    {
        var request = Request() with { Slots = Array.Empty<SlotRequest>() };

        var result = LoopRun.Execute(request, new SimulatedGateway(Geometry()));

        Assert.Equal(LoopOutcome.NotDerivable, result.Outcome);
        Assert.Null(result.Gate);
        Assert.Contains("the gate could not run", result.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The device boundary
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_DEFAULT_gateway_REFUSES_rather_than_pretending()
    {
        var result = LoopRun.Execute(Request(), new RefusingDeviceGateway());

        Assert.Equal(LoopOutcome.NotDeployed, result.Outcome);
        Assert.Empty(result.Packages);
        Assert.Contains("NOT a failed download", result.Deployment!.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_deployment_that_loaded_LESS_than_it_was_given_stops_the_loop()
    {
        var gateway = new SimulatedGateway(Geometry());
        gateway.OmitFromManifest.Add("FC_DemoRamp");

        var result = LoopRun.Execute(Request(), gateway);

        Assert.Equal(LoopOutcome.NotDeployed, result.Outcome);
        Assert.Equal(0, gateway.Opens);
    }

    [Fact]
    public void A_device_running_a_DIFFERENT_BUILD_stops_the_loop_before_the_wave()
    {
        var gateway = new SimulatedGateway(Geometry()) { PublishVersionInstead = 0x0BADF00D };

        var result = LoopRun.Execute(Request(), gateway);

        Assert.Equal(LoopOutcome.NotConfirmed, result.Outcome);
        Assert.Empty(result.Packages);
        Assert.False(result.Version!.Confirmed);
    }

    // ---------------------------------------------------------------------------------------------
    // Settling — lane 5's ambiguity, closed for the form a declaration can state
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_BLOCK_THAT_SAYS_DONE_AND_KEEPS_RUNNING_COMES_BACK_UNSETTLED_RATHER_THAN_WRONG()
    {
        // *** THE CONTRACT'S SHARPEST WARNING, REPRODUCED THROUGH THE WHOLE LOOP. *** The block latches
        // its done flag and goes on changing the value it was asked about, so any single observation is a
        // snapshot of something still moving - and a harness that compared that snapshot and believed it
        // would return a CONFIDENTLY WRONG verdict rather than a missed one. The settling check compares
        // WHAT WAS RECORDED against what is still true some scans later, so it is caught and the result
        // says NOTHING WAS LEGITIMATELY READ rather than reporting a number.
        //
        // The two existing builds both SETTLE, so neither could make this guard fire. That is why the
        // third defect exists: a guard nothing can exercise is a guard nobody knows works.
        var (result, _) = Run(Request(defect: TrivialBlockDefect.DoneWhileStillRunning, vector: Vector(expected: "10")));

        var package = Assert.Single(result.Packages);
        Assert.Equal(SettlingState.NotSettled, package.Settling);
        Assert.Equal(ResultVerdict.Unsettled, package.Verdict);
        Assert.False(package.ConclusiveAboutTheBlock);
        Assert.Contains("NOTHING WAS LEGITIMATELY READ", package.WhatToDoNext, StringComparison.Ordinal);
    }

    [Fact]
    public void A_settling_condition_the_runner_cannot_EVALUATE_is_NotEstablished_never_Settled()
    {
        // Prose the runner cannot check is not a failure and is not a pass: it is a value that was never
        // established, and the package refuses on it rather than reading it as final.
        var (result, _) = Run(Request(vector: Vector(settlingScans: 0)));

        var package = Assert.Single(result.Packages);
        Assert.Equal(SettlingState.NotEstablished, package.Settling);
        Assert.Equal(ResultVerdict.Unsettled, package.Verdict);
    }

    // ---------------------------------------------------------------------------------------------
    // 3. THE CAVEATS ARE CARRIED, ON EVERY RUN
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Every_run_carries_the_known_gaps_including_the_ones_that_went_well()
    {
        // A report that appears only on bad news teaches a reader that its absence means it was not run -
        // the same lesson as F-6's collapse report printing its no-collapse line.
        foreach (var result in new[] { Run().Result, Run(Request(vector: Vector(author: "agent-a"))).Result })
        {
            Assert.Contains(result.Caveats, c => c.Id == "F-3-authority");
            Assert.Contains(result.Caveats, c => c.Id == "DB-8-saw-nothing");
        }
    }

    [Fact]
    public void THE_COLLAPSE_REPORT_IS_EMITTED_HERE_BECAUSE_NOTHING_ELSE_EMITS_IT()
    {
        var (result, _) = Run();

        var seam = Assert.Single(result.Caveats, c => c.Id == "F-6-collapse-seam");
        Assert.Contains("SLOT-WIDTH REPORT (not a gate", seam.Detail, StringComparison.Ordinal);
        Assert.Contains("nothing else emits it", seam.Detail, StringComparison.Ordinal);

        // And it is still a report here: nothing in the loop consults it.
        Assert.NotNull(result.SizeReport);
        Assert.Equal(LoopOutcome.Ran, result.Outcome);
    }

    [Fact]
    public void The_DB_8_caveat_states_HOW_MANY_sampled_expectations_this_submission_has()
    {
        // So its absence can never mean "not applicable" - it says 0 of N rather than not appearing.
        var sampled = Assert.Single(Run().Result.Caveats, c => c.Id == "DB-8-saw-nothing");
        Assert.Contains("declares 1 sampled expectation(s) of 1", sampled.Detail, StringComparison.Ordinal);

        var latched = Assert.Single(Run(Request(vector: Vector(mode: InstrumentationMode.Latched))).Result.Caveats,
            c => c.Id == "DB-8-saw-nothing");
        Assert.Contains("declares 0 sampled expectation(s) of 1", latched.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 4. What is owed on the device — a deliverable
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void What_is_owed_on_the_device_is_carried_on_the_result_type_itself()
    {
        Assert.NotEmpty(LoopResult.OwedOnTheDevice);
        Assert.Contains(LoopResult.OwedOnTheDevice, o => o.Contains("DEPLOYMENT ITSELF", StringComparison.Ordinal));
        Assert.Contains(LoopResult.OwedOnTheDevice, o => o.Contains("F-1's PREMISE", StringComparison.Ordinal));
        Assert.Contains(LoopResult.OwedOnTheDevice, o => o.Contains("BIT ORDER", StringComparison.Ordinal));
        Assert.Contains(LoopResult.OwedOnTheDevice, o => o.Contains("A5", StringComparison.Ordinal));
    }

    [Fact]
    public void The_summary_names_the_OUTCOME_first_and_the_verdict_spread_after()
    {
        Assert.StartsWith("RAN —", Run().Result.Summary(), StringComparison.Ordinal);
        Assert.Contains("1 PASS", Run().Result.Summary(), StringComparison.Ordinal);
        Assert.StartsWith("NOTADMISSIBLE —", Run(Request(vector: Vector(author: "agent-a"))).Result.Summary(), StringComparison.Ordinal);
    }
}
