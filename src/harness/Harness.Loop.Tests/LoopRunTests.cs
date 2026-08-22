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

    internal static MirrorGeometry Geometry() => MirrorGeometry.ForCpu1214C(256, MirrorBase, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - MirrorBase) / 2);

    // A REAL assertion ID, COMPUTED rather than invented. Gate 3g recomputes every ID from its own
    // normalised text and refuses a mismatch, which is what lets the stamping step (assertion-
    // enumeration.md 3.4) have no independence from the block or vector author. A hand-written six-hex
    // literal cannot be made to pass that: finding a text hashing to chosen digits is a preimage problem.
    private const string ClauseId = "REQ-014";

    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";

    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    private static AssertionEnumeration Enumeration(
        AssertionForm form = AssertionForm.When,
        string enumerator = "agent-c",
        IReadOnlyDictionary<string, string>? bounds = null) =>
        AssertionEnumeration.Of(
            new[] { ClauseId },
            new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = form },
            enumerator,
            new Dictionary<string, string>(StringComparer.Ordinal) { [AssertionIdValue] = AssertionText },
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                // AMB-14: every signal a citation of this assertion depends on. One here, and the
                // vector observes it.
                [AssertionIdValue] = new HashSet<string>(StringComparer.Ordinal) { TrivialBlock.CountTag },
            },
            bounds);

    private static SubmissionVector Vector(
        int step = 5, int limit = 10, string expected = "10", int settlingScans = 3,
        AssertionForm form = AssertionForm.When, string author = "agent-b",
        InstrumentationMode mode = InstrumentationMode.Sampled, int window = 20, string? kills = "a ramp that overshoots the limit by one step",
        string? completionSignal = null, int completionValue = 1) =>
        new("V-1", "S0", 0, new AgentIdentity(author),
            new Basis(ClauseId, AssertionIdValue),
            new Dictionary<string, string> { [TrivialBlock.StepTag] = step.ToString(), [TrivialBlock.LimitTag] = limit.ToString() },
            TrivialBlock.StartTag,
            new[] { new ObservabilityDeclaration(TrivialBlock.CountTag, SignalNature.PersistentState, mode, window, expected) },
            form,
            new SettlingDeclaration("count unchanged across 3 consecutive scans",
                new[] { TrivialBlock.CountTag, TrivialBlock.DoneTag }, settlingScans),
            MaxDurationScans: 20,
            CompletionValue: completionValue,
            Array.Empty<BlacklistEntry>(),
            CompressionFactor: 1,
            AssertedBehaviours: new[] { "ramp-to-limit" },
            CompletionSignal: completionSignal ?? TrivialBlock.DoneTag,
            Kills: kills,
            // AMB-19: the limit IS the specified bound this vector was written against, so it is declared
            // from the same variable rather than restated. Request() then derives the enumeration's table
            // from this, which keeps every loop fixture CURRENT by construction — deliberately, because
            // these tests are about the loop. Staleness itself is tested in BoundsCurrencyTests and in
            // SubmissionGateTests, where the table and the vector are set independently.
            BoundsUsed: new Dictionary<string, string>(StringComparer.Ordinal) { ["ramp_limit"] = limit.ToString() });

    /// <summary>
    /// The 2.7 join for these fixtures — <b>and this is the DID-NOT-RUN control.</b>
    ///
    /// <para>A join that refuses everything passes every test that only checks refusals, and that shape
    /// has been caught here repeatedly. So the default fixture DECLARES a complete join: both result
    /// signals are real PLC tags in the skeleton block's own tag table, with the tag table as their
    /// owner. Gates 8 and 8c must therefore actually RUN — not be NOT CHECKED — and the tests that
    /// assert a full green depend on that.</para>
    /// </summary>
    internal static SignalStorageMap Storage() => SignalStorageMap.Of(new[]
    {
        (TrivialBlock.CountTag, new SignalStorage("DemoUnit", TrivialBlock.CountTag)),
        (TrivialBlock.DoneTag, new SignalStorage("DemoUnit", TrivialBlock.DoneTag)),
    });

    /// <summary>
    /// 🔴 <b>THE RE-EXPRESSED VECTORS MUST REACH THE DEVICE, NOT JUST THE REPORT.</b>
    ///
    /// <para><b>Measured on the rig, 2026-08-22.</b> <c>Generate</c> re-expressed the scenario
    /// coordinates by reassigning its own <c>request</c> parameter — a LOCAL — while <c>Execute</c> built
    /// the wave from the request IT still held. The gate log said "18 coordinates re-expressed at comp 4"
    /// and the device received all 18 UNSCALED. The block then ran its timers four times faster against a
    /// full-length scenario: index 0 hit its backstop <b>with 5 of 5 assertions holding</b>, the next
    /// index could not establish inert because the plant was still mid-run, and the third never ran.</para>
    ///
    /// <para><b>Why the existing tests all passed.</b> They checked that <c>ScenarioScale.Apply</c>
    /// COMPUTES the right values, and that the gate REPORTS them. Neither followed the number to the
    /// wire. This one does: the ramp stops at whatever limit the device actually received, so the
    /// expectation below holds <b>only if the scaled value was written</b> — an unscaled 40 makes the
    /// count overshoot and the assertion fail.</para>
    /// </summary>
    [Fact]
    public void THE_SCALED_COORDINATE_REACHES_THE_DEVICE_and_not_merely_the_report()
    {
        // The ceilings are the ones the compressed-run test below already establishes as admissible; the
        // ONLY thing this test changes is that a coordinate is declared as scaling. Declared 40, so at
        // comp 2 the device must receive 20 — and the block ramps to whatever it actually got.
        var request = Request(
            vector: Vector(step: 5, limit: 40, expected: "20"),
            compression: new RuntimeCompression(2),
            compressionInputs: new BlockCompressionInputs(
                PlantMs: 2_000, BudgetMs: 1_000,
                Presets: new[] { new TimerPreset("Dwell", 2_000, PresetSource.Data) },
                ModelCompStable: 100, NegligibleFraction: null))
            with
        { ScenarioTimeInputs = new[] { TrivialBlock.LimitTag } };

        var map = LoopRun.Generate(request).Map!;
        var (result, gateway) = Run(request);

        Assert.Equal(LoopOutcome.Ran, result.Outcome);

        // The value ON THE DEVICE, read out of the simulated PLC's own memory rather than out of any
        // report this run produced. A report is exactly what was right last time while the device was wrong.
        var binding = request.Bindings[0];
        var target = binding.VectorTargets.ToList().FindIndex(t => t.JoinKey == TrivialBlock.LimitTag);
        var register = map.VectorBlock.Register + binding.VectorRegisterOffsets[target];

        // %MW is BIG-endian and BitConverter is little-endian on x86, so the naive read returns 0x1400
        // for a stored 20. Combined explicitly rather than reversed in place, because a byte order got
        // settled on this project by reading the DEVICE and is not something to re-guess in a test.
        var at = Geometry().BaseByte + register * 2;
        var written = (ushort)((gateway.Plc.Memory[at] << 8) | gateway.Plc.Memory[at + 1]);

        Assert.Equal(20, written);
    }

    internal static LoopRequest Request(
        SubmissionVector? vector = null,
        AssertionEnumeration? enumeration = null,
        TrivialBlockDefect defect = TrivialBlockDefect.None,
        ConflictGraph? conflicts = null,
        FidelityDeclaration? fidelity = null,
        RuntimeCompression? compression = null,
        BlockCompressionInputs? compressionInputs = null) =>
        new(new[] { vector ?? Vector() },
            // The bounds table is derived from the vector's own declaration, so these fixtures are
            // bounds-CURRENT whatever `limit` a test picks. See the note on Vector's BoundsUsed.
            enumeration ?? Enumeration(bounds: (vector ?? Vector()).BoundsUsed),
            fidelity ?? FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true, declaredBy: "agent-m"),
            new AgentIdentity("agent-a"),
            conflicts ?? ConflictGraph.Empty,
            Geometry(),
            new[] { new SlotRequest("S0", 2, 2) },
            new[] { TrivialBlock.Binding("S0") },
            new CopyLayerNaming(BlockNumber: 900),
            TrivialBlock.Generate(ProgramBase, blockNumber: 901, defect),
            RuntimeCompression: compression ?? RuntimeCompression.Uncompressed,
            CompressionInputs: compressionInputs,

            // Contract 4.5, and it is TRUE of this loop rather than convenient: the mirror is %MW bit
            // memory (spec 6, MirrorGeometry), the copy layer generates no data block, and no tag map is
            // in play - so no classic-S7comm path reaches one. Declared as the positive claim it is, and
            // compared against a reachable set that really is empty.
            Deployment: new DeploymentDeclaration("loop-test-import", Array.Empty<S7ObjectDeclaration>()),
            TagMapReach: TagMapReach.Of(Array.Empty<S7Reach>()),

            // The 2.7 join, complete - see Storage(). This is what lets gates 8 and 8c RUN rather than
            // report NOT CHECKED, which is the control the whole change needs.
            SignalStorage: Storage(),

            // 🔴 *** THE CLAIM IS THE CALLER'S TO MAKE, AND HERE IT IS TRUE. *** This fixture composes a
            // request from TYPED objects, so there is genuinely no channel by which an unknown field could
            // arrive and the empty set is COMPUTED rather than assumed. It has to be said out loud because
            // LoopRun used to say it for everybody - including LoopCli, which PARSES two documents and for
            // which it is false. Omitting these now yields NOT CHECKED on gate 0b, which is the loud
            // failure the default was chosen to produce.
            UnknownFields: Array.Empty<string>(),
            AnnotationFields: Array.Empty<string>(),

            // Same claim, same reason, for gate 0c: there is no submission document here, so no derivable
            // field was hand-authored. Said out loud rather than defaulted, because the caller for which
            // it is FALSE - LoopCli, which parses one - is the caller that spends rig time.
            Derivation: DerivationEvidence.NoDocument,

            // Gate 1b. These vectors are ramp-to-limit: they finish when a count reaches a limit, so they
            // have no scenario clock and `ScenarioEndInput` would have nothing to point at. A flat ceiling
            // is the only bound available to that shape, and it is the coordinator's to declare. 200 scans
            // against the fixture's MaxDuration of 20 — bounded with room, so a fixture change that trips
            // this gate is a real change rather than a fixture running along an edge.
            MaxIndexScans: 200,

            // Gate 10c: EMPTY is the positive claim that this stimulus has no time-valued scenario data,
            // and it is TRUE here rather than convenient — these vectors are ramp-to-limit, completion is a
            // count reaching a limit, and there is no clock whose scale could disagree with the block's.
            // Omitting it would be NOT CHECKED, which is the honest answer for a stimulus that HAS one.
            ScenarioTimeInputs: Array.Empty<string>());

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
        Assert.Equal(AssertionIdValue, package.Assertions[0].AssertionId);
        Assert.Equal("15", package.Assertions[0].Observed);
    }

    [Fact]
    public void The_defect_changes_the_BUILD_STAMP_so_the_two_runs_are_different_downloads()
    {
        var clean = Run().Result;
        var buggy = Run(Request(defect: TrivialBlockDefect.OffByOneAtTheLimit)).Result;

        Assert.NotEqual(clean.Packages[0].Stamp.ProgramVersion, buggy.Packages[0].Stamp.ProgramVersion);
    }

    [Fact]
    public void A_COMPLETION_VALUE_OTHER_THAN_1_IS_HONOURED_because_the_contract_states_none()
    {
        // *** FOUND BY MUTATION: HARDCODING 1 LEFT THE SUITE GREEN. *** The loop carried the completion
        // value as data and nothing exercised a value other than 1, so the field and the literal were
        // indistinguishable - a guard nothing could make fire, for the fifth time in this project.
        //
        // Contract section 2 names a completion SIGNAL and never says what value on it means finished.
        // Here the signal is the COUNT and the value is the limit itself: the ramp is complete when the
        // count reads 10, and nothing about "1" is involved.
        var (result, _) = Run(Request(vector: Vector(completionSignal: TrivialBlock.CountTag, completionValue: 10)));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        var package = Assert.Single(result.Packages);
        Assert.Equal(ResultVerdict.Pass, package.Verdict);
        Assert.Equal("10", package.Assertions[0].Observed);
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
        // 🔴 *** RE-TARGETED, AND THE PROPERTY IS KEPT. *** This used to put the PROGRAM UNDER TEST inside
        // the retentive window and assert NotAssertable. 0.1b is `every HARNESS object is asserted
        // non-retentive`, and the program under test is neither generated nor the harness's to constrain —
        // measured the moment `--program` could name the real corpus: 159 findings over 45 objects, every
        // one a property of correct plant code, including a member that is retentive ON PURPOSE so it
        // survives the CPU restart the boundary-spanning vectors ride.
        //
        // What survives is the ORDER: the assertion runs, over a non-empty denominator, BEFORE the device.
        var (result, gateway) = Run();

        Assert.True(result.Retention!.Passed);
        Assert.True(result.Retention.ObjectsExamined > 0);
        Assert.True(result.Retention.AddressesExamined > 0);
        Assert.Equal(1, gateway.Deployments);
    }

    [Fact]
    public void The_0_1b_FAILING_direction_is_UNREACHABLE_from_this_loop_and_that_is_asserted_rather_than_assumed()
    {
        // *** A GUARD WIRED IN THAT NOTHING CAN REACH IS NOT THE SAME AS A GUARD THAT IS WRONG, AND IT IS
        // NOT THE SAME AS ONE THAT IS RIGHT EITHER. *** With 0.1b scoped to the GENERATED objects, its two
        // failing conditions — an address below the retentive window, an address past the top of bit
        // memory — are functions of the same two numbers the ALLOCATOR refuses on first. So the loop
        // cannot produce NotAssertable from generated objects, and a test asserting it would be pinning a
        // state nothing constructs.
        //
        // That is recorded as an ORDERING fact instead: the earlier gate fires, by name. If a later change
        // makes 0.1b reachable through this loop, this test goes red and demands the real one be written.
        var request = Request() with { Geometry = MirrorGeometry.ForCpu1214C(retentiveBytes: MirrorBase + 2, baseByte: MirrorBase, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - MirrorBase) / 2) };
        var gateway = new SimulatedGateway(Geometry());

        var result = LoopRun.Execute(request, gateway);

        Assert.Equal(LoopOutcome.NotDerivable, result.Outcome);
        Assert.Contains("INSIDE the retentive M window", result.Detail, StringComparison.Ordinal);
        Assert.Equal(0, gateway.Deployments);

        // The refusal itself is tested directly, against inputs the allocator would never let through:
        // see RetentionCheckTests, which drives RetentionCheck rather than the loop.
        Assert.Null(result.Retention);
    }

    [Fact]
    public void A_RETENTIVE_PROGRAM_UNDER_TEST_DOES_NOT_FAIL_0_1b_and_its_retain_is_reported_instead()
    {
        // The converse of the re-targeting above, and the case the deliverable actually is: the plant
        // program sits inside the retentive window and declares retentive members, and the harness runs.
        var request = Request() with { ProgramUnderTest = TrivialBlock.Generate(100, 901) };

        var result = LoopRun.Execute(request, new SimulatedGateway(Geometry()));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.True(result.Retention!.Passed);
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
        Assert.Equal(SettlingState.NotSettled, package.Settling.State);
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
        Assert.Equal(SettlingState.NotEstablished, package.Settling.State);
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

    // ---------------------------------------------------------------------------------------------
    // 5. X-D against X-B — the loop is where the two could disagree
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>The vector's declared <c>comp</c> reaches the WAVE's backstop, not only the gate.</b>
    ///
    /// <para>The gate re-checks every window at the factor the run will use; until this was wired, the wave
    /// computed its backstops from the raw scan counts as though every vector were declared at
    /// <c>comp = 1</c>. A vector declaring 20 scans at <c>comp = 10</c> would then be bounded ten times too
    /// tightly and report TIMED-OUT on a healthy test — the one verdict the design built to be
    /// unambiguous, and the one that is believed.</para>
    /// </summary>
    [Fact]
    public void THE_DECLARED_COMPRESSION_REACHES_THE_BACKSTOP_AND_NOT_ONLY_THE_GATE()
    {
        static string TimeoutDetail(int declaredComp)
        {
            // A limit far enough away that nothing completes before the backstop, and an injected clock so
            // no test waits. Both runs are otherwise identical.
            var vector = Vector(step: 1, limit: 30_000) with { CompressionFactor = declaredComp };
            var elapsed = 0L;

            var result = LoopRun.Execute(Request(vector: vector), new SimulatedGateway(Geometry()), () => elapsed += 400);

            Assert.Equal(LoopOutcome.Ran, result.Outcome);
            var run = result.Wave!.For(0).Results[0];
            Assert.Equal(SlotOutcome.TimedOut, run.Outcome);
            return run.Detail;
        }

        var atOne = TimeoutDetail(1);
        var atThree = TimeoutDetail(3);

        // 20 scans declared at comp=3, run at comp=1, is SIXTY scans of real time — and the backstop says so.
        // 4, not 3: one write + one result read + one CONTROL read + the commit. The control read joined
        // WireTiming.RoundTripsPerIndex on 2026-08-22 — Observe always issued it and the formula never
        // counted it, leaving the backstop short in the spurious-TIMED-OUT direction.
        Assert.Contains($"backstop of {WireTiming.BackstopMs(new ScanBudget(20, 1), RuntimeCompression.Uncompressed, 4)} ms", atOne, StringComparison.Ordinal);
        Assert.Contains($"backstop of {WireTiming.BackstopMs(new ScanBudget(20, 3), RuntimeCompression.Uncompressed, 4)} ms", atThree, StringComparison.Ordinal);
        Assert.NotEqual(atOne, atThree);
    }

    /// <summary>
    /// <b>The wave's own compression — the seam where the gate and the runner could hold different
    /// numbers.</b>
    ///
    /// <para>Until a compressed run was reachable at all, passing <c>Uncompressed</c> to the wave instead
    /// of the request's factor was INDISTINGUISHABLE from passing it correctly: every admissible run was at
    /// comp=1, so the argument could have been a literal and no test would have known. That is the class of
    /// defect this component has found seven times, and it is closed by making the compressed path
    /// reachable rather than by asserting harder about the uncompressed one.</para>
    /// </summary>
    [Fact]
    public void THE_RUNTIME_COMPRESSION_REACHES_THE_WAVE_and_a_literal_ONE_there_would_be_INVISIBLE_without_this()
    {
        static string TimeoutDetail(RuntimeCompression compression)
        {
            var elapsed = 0L;
            var request = Request(
                vector: Vector(step: 1, limit: 30_000),
                compression: compression,
                // comp_min here is 2000 / 1000 = 2x, so the ceilings have to leave room for it. Under the
                // RULED ABSOLUTE 500 ms timer floor (2026-08-18) a 2-second DATA preset caps at 4.0x — the
                // preset was 500 ms while the floor was the scan-derived 116.7 ms, which capped at 4.3x.
                // The headroom is what this test needs; the ceiling itself is pinned in TimeCompressionTests.
                // No negligibleFraction: the ratio-distortion bound is the ruled 10x literal-headroom
                // companion now, and there are no LITERAL presets here for it to bind on anyway.
                compressionInputs: new BlockCompressionInputs(
                    PlantMs: 2_000, BudgetMs: 1_000,
                    Presets: new[] { new TimerPreset("Dwell", 2_000, PresetSource.Data) },
                    ModelCompStable: 100, NegligibleFraction: null));

            var result = LoopRun.Execute(request, new SimulatedGateway(Geometry()), () => elapsed += 400);

            Assert.Equal(LoopOutcome.Ran, result.Outcome);
            return result.Wave!.For(0).Results[0].Detail;
        }

        // Same vector, same declaration of 20 scans at comp=1: run at comp=2 the test really does take ten
        // scans, and the bound follows it.
        Assert.Contains($"backstop of {WireTiming.BackstopMs(new ScanBudget(20, 1), RuntimeCompression.Uncompressed, 4)} ms",
            TimeoutDetail(RuntimeCompression.Uncompressed), StringComparison.Ordinal);

        Assert.Contains($"backstop of {WireTiming.BackstopMs(new ScanBudget(20, 1), new RuntimeCompression(2), 4)} ms",
            TimeoutDetail(new RuntimeCompression(2)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_compressed_run_WITH_the_block_level_ceilings_supplied_is_admissible_and_reaches_the_device()
    {
        // comp_min is 2000 / 1000 = 2x and the run is at 2x, so admissibility turns on the ceilings being
        // at least that. Under the RULED ABSOLUTE 500 ms timer floor (2026-08-18) a 2-second DATA preset
        // caps at 4.0x; a 500 ms preset — what this test carried while the floor was the scan-derived
        // 116.7 ms — now caps at exactly 1.0x and would be refused before the device. That refusal is
        // correct arithmetic under the new rule, not a regression, so the FIXTURE moves and the assertion
        // does not.
        var (result, gateway) = Run(Request(
            compression: new RuntimeCompression(2),
            compressionInputs: new BlockCompressionInputs(
                PlantMs: 2_000, BudgetMs: 1_000,
                Presets: new[] { new TimerPreset("Dwell", 2_000, PresetSource.Data) },
                ModelCompStable: 100, NegligibleFraction: null)));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.Equal(1, gateway.Deployments);
    }

    [Fact]
    public void A_COMPRESSED_RUN_IS_REFUSED_BEFORE_THE_DEVICE_because_three_of_X_Ds_ceilings_are_unstated()
    {
        // Fail-closed, and it is a real consequence rather than a formality: nothing may run compressed
        // until a submission can state the block's presets and the model's comp_stable. The refusal is at
        // the GATE, so nothing is deployed and no wave is spent.
        var (result, gateway) = Run(Request(compression: new RuntimeCompression(4)));

        Assert.Equal(LoopOutcome.NotAdmissible, result.Outcome);
        Assert.Contains(result.Gate!.NotChecked, g => g.Gate.StartsWith("10b time compression", StringComparison.Ordinal));
        Assert.Equal(0, gateway.Deployments);
        Assert.Equal(0, gateway.Opens);
    }

    [Fact]
    public void The_summary_names_the_OUTCOME_first_and_the_verdict_spread_after()
    {
        Assert.StartsWith("RAN —", Run().Result.Summary(), StringComparison.Ordinal);
        Assert.Contains("1 PASS", Run().Result.Summary(), StringComparison.Ordinal);
        Assert.StartsWith("NOTADMISSIBLE —", Run(Request(vector: Vector(author: "agent-a"))).Result.Summary(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_VALUE_TOO_WIDE_FOR_ITS_ELEMENT_STOPS_THE_LOOP_BEFORE_ANYTHING_IS_DEPLOYED()
    {
        // *** THE MEASURED CASE. *** 81 duration values in the deliverable vector set exceed 65 535 ms.
        // Through a single-register mapping 75 000 arrives as 9 464: no error, no timeout, just every
        // boundary firing early and a confident FAIL against a block that did nothing wrong.
        var wide = Vector(limit: 10) with
        {
            Inputs = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [TrivialBlock.StepTag] = "5",
                [TrivialBlock.LimitTag] = "75000",
            },
        };

        var (result, gateway) = Run(Request(vector: wide));

        Assert.Equal(LoopOutcome.NotRepresentable, result.Outcome);

        // *** BEFORE THE DEVICE. *** Refusing after deployment would mean the CPU was loaded with a
        // program that could not carry its own stimulus.
        Assert.Equal(0, gateway.Deployments);
        Assert.Equal(0, gateway.Opens);

        Assert.Contains("9464", result.Detail, StringComparison.Ordinal);
        Assert.Contains("REFUSAL RATHER THAN A TRUNCATION", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void And_the_SAME_value_runs_when_the_element_is_wide_enough_to_carry_it()
    {
        // The pair matters: the refusal has to be about the WIDTH, not about the number being large.
        Assert.True(MirrorValueFit.Check(TrivialBlock.LimitTag, MirrorValueType.Time, "75000").Fits);
        Assert.False(MirrorValueFit.Check(TrivialBlock.LimitTag, MirrorValueType.Int, "75000").Fits);
    }

    [Fact]
    public void A_COMPLETION_SIGNAL_WIDER_THAN_ONE_REGISTER_IS_REFUSED_because_it_is_INEXPRESSIBLE()
    {
        // ⚠️ WireVector compares ONE register against a ushort, so a 32-bit completion signal cannot be
        // expressed at all. Comparing anyway would test its high half and report TIMED-OUT forever on a
        // block that finished — a spurious TIMED-OUT being worse than a spurious FAIL, because it is
        // believed. Until completion carries a width like every other mirrored element, this refuses.
        var binding = new SlotBinding(
            "S0",
            MirroredSignal.Ints(TrivialBlock.StepTag, TrivialBlock.LimitTag),
            TrivialBlock.StartTag,
            new[]
            {
                new MirroredSignal(TrivialBlock.CountTag, MirrorValueType.Int, SpecName: TrivialBlock.CountTag),
                new MirroredSignal(TrivialBlock.DoneTag, MirrorValueType.Time, SpecName: TrivialBlock.DoneTag),
            });

        var request = Request() with { Bindings = new[] { binding }, Slots = new[] { new SlotRequest("S0", 2, 4) } };

        var (result, gateway) = Run(request);

        Assert.Equal(LoopOutcome.NotRepresentable, result.Outcome);
        Assert.Equal(0, gateway.Deployments);
        Assert.Contains("INEXPRESSIBLE, NOT MIS-EXPRESSED", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_OBSERVABILITY_FLOOR_IS_MONOTONIC_IN_SLOT_COUNT_so_OVER_declaring_is_the_SAFE_error()
    {
        // 🔴 *** A CORRECTION, PINNED SO IT CANNOT BE MIS-REMEMBERED AGAIN. *** It has been said that
        // over-declaring the slot count gives a floor that is too PERMISSIVE. It is the opposite:
        // readsPerCycle is monotonically non-decreasing in slot count and the floor is proportional to it,
        // so OVER-declaring gives a floor that is too HIGH — the conservative error. *** THE PERMISSIVE
        // DIRECTION IS UNDER-DECLARING *** — which is what naming the slots you are submitting rather than
        // the wave set they run in produces.
        var geometry = MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2);

        double previous = 0;

        foreach (var slots in new[] { 1, 2, 3, 6, 12, 24, 48 })
        {
            var map = MapAllocator.Allocate(new WaveSetRequest(
                geometry,
                Enumerable.Range(0, slots).Select(i => new SlotRequest($"S{i}", 2, 8)).ToArray())).Require();

            var floor = WireTiming.ObservabilityFloorScans(map.ReadPlan(Enumerable.Range(0, map.Slots.Count)).Count);

            Assert.True(floor >= previous,
                $"the floor fell from {previous} to {floor} going to {slots} slot(s). If it can fall, over-declaring the "
                + "slot count becomes the PERMISSIVE error and the correction recorded here is wrong.");

            previous = floor;
        }

        // And the measured consequence the wave set actually turned on: 6 slots x 8 result registers is 48,
        // still inside ONE Modbus read of 125, so the floor is UNCHANGED between 1 slot and 6. The floor is
        // quantised on ROUND TRIPS, not on slot count — which is why the byte-identical gate output across
        // that change was correct rather than suspicious.
        double FloorFor(int slots)
        {
            var map = MapAllocator.Allocate(new WaveSetRequest(
                geometry,
                Enumerable.Range(0, slots).Select(i => new SlotRequest($"S{i}", 2, 8)).ToArray())).Require();

            return WireTiming.ObservabilityFloorScans(map.ReadPlan(Enumerable.Range(0, map.Slots.Count)).Count);
        }

        Assert.Equal(FloorFor(1), FloorFor(6));
    }

    [Fact]
    public void THERE_IS_ONE_32_BIT_WORD_ORDER_IN_THIS_SYSTEM_and_it_is_now_MEASURED()
    {
        // A `Time` result is one %MD on the PLC and TWO holding registers on the wire, so decoding it
        // means choosing which half is the low word — the SAME choice the version register and the scan
        // counter already make. Two independent defaults would mean two things to calibrate and a way for
        // them to disagree, so the loop's default is asserted equal to the client's.
        //
        // ✅ *** AND THE CHOICE IS NOW MEASURED: HighWordFirst. *** 2026-08-13 against a known pattern
        // (16#00001111) and 2026-08-14 by decoding the deployed build stamp at registers 0-1 — both with
        // DISTINGUISHABLE HALVES, which is what makes a reading identify the order rather than merely
        // agree with itself. A Time read under the wrong order would be out by 65 536 ms and read as a
        // plausible timing bug; that hazard is why it was worth measuring, and it is now closed.
        var loopDefault = Request().WordOrder;

        // Read off MirrorClient's own constructor rather than restated, so the two cannot drift apart
        // without this failing.
        var clientDefault = typeof(MirrorClient).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Where(p => p.ParameterType == typeof(RegisterWordOrder) && p.HasDefaultValue)
            .Select(p => (RegisterWordOrder)p.DefaultValue!)
            .Distinct()
            .ToArray();

        var declared = Assert.Single(clientDefault);
        Assert.Equal(declared, loopDefault);

        // Writing under one order and reading under the other CANCELS OUT on our own loopback — which is
        // why agreeing with ourselves proves nothing here and only the device can settle it.
        const uint pattern = 0xA93F2C71;
        foreach (var order in Enum.GetValues<RegisterWordOrder>())
        {
            var words = RegisterWords.From32(pattern, order);
            Assert.Equal(pattern, RegisterWords.To32(words[0], words[1], order));
        }

        // Under the OTHER order the same pair reads as a different number entirely — the failure mode.
        var high = RegisterWords.From32(pattern, RegisterWordOrder.HighWordFirst);
        Assert.NotEqual(pattern, RegisterWords.To32(high[0], high[1], RegisterWordOrder.LowWordFirst));
    }
}
