using Harness.Loop;
using Harness.Map;
using Harness.Results;
using Harness.Skeleton;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE OBSERVATION PATH JOINS ON THE NAME A VECTOR CITES — AND UNTIL 2026-08-17 IT JOINED ON THE
/// BLOCK'S TAG, SO A WAVE COULD RUN PERFECTLY AND READ NOTHING.</b>
///
/// <para><b>Why this file exists rather than a case in <c>LoopRunTests</c>:</b> every fixture in that
/// file declares <c>SpecName == Tag</c> — deliberately, and it says so — <b>so the two keys are
/// indistinguishable there and no test in it could ever have failed on this.</b> That is the same shape
/// as the finding it is guarding: on the real deliverable the ONE signal that resolved was the one whose
/// spec name happened to equal its tag. <i>A fixture in which two keys coincide is a fixture that cannot
/// tell them apart.</i></para>
///
/// <para><b>The shape reproduced here is JOB9004's first live wave, 2026-08-17:</b> a binding that states a
/// specification name for every result source, and vectors that cite those names. The wave ran, cost
/// 7,912 round trips, and returned <c>observed: "&lt;never read&gt;"</c> for all four declared
/// assertions while the published mirror feed recorded the whole result band being read.</para>
/// </summary>
public class SpecNameObservationTests
{
    // The specification's vocabulary. NOT the block's — that is the entire point, and it is the shape of
    // every real binding: `SPEC.FaultAlarm` is the alarm id an instance carries, `iDB_Widget.IO.Fault` is
    // what the block calls the member.
    private const string CountSpecName = "SPEC.RampCount";
    private const string DoneSpecName = "SPEC.RampDone";

    private const string ClauseId = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    /// <summary>
    /// The binding under test: identical to the skeleton's except that the two result sources carry a
    /// spec name that DIFFERS from the tag. Nothing else moves — same tags, same types, same order — so
    /// any difference in outcome is attributable to the join and to nothing else.
    /// </summary>
    private static SlotBinding Binding(string slotId = "S0") => new(
        slotId,
        MirroredSignal.Ints(TrivialBlock.StepTag, TrivialBlock.LimitTag),
        TrivialBlock.StartTag,
        new[]
        {
            new MirroredSignal(TrivialBlock.CountTag, MirrorValueType.Int, SpecName: CountSpecName,
                Rest: InertRest.At("0", "the ramp block's network 1 holds the count at 0 while the start command is off")),
            new MirroredSignal(TrivialBlock.DoneTag, MirrorValueType.Int, SpecName: DoneSpecName,
                Rest: InertRest.At("0", "the ramp block's network 1 holds the done flag at 0 while the start command is off")),
        });

    private static AssertionEnumeration Enumeration(IReadOnlyDictionary<string, string>? bounds, string observedSignal) =>
        AssertionEnumeration.Of(
            new[] { ClauseId },
            new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When },
            "agent-c",
            new Dictionary<string, string>(StringComparer.Ordinal) { [AssertionIdValue] = AssertionText },
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [AssertionIdValue] = new HashSet<string>(StringComparer.Ordinal) { observedSignal },
            },
            bounds);

    private static SubmissionVector Vector(
        string observedSignal = CountSpecName,
        string completionSignal = DoneSpecName,
        string expected = "10",
        int limit = 10) =>
        new("V-1", "S0", 0, new AgentIdentity("agent-b"),
            new Basis(ClauseId, AssertionIdValue),
            new Dictionary<string, string> { [TrivialBlock.StepTag] = "5", [TrivialBlock.LimitTag] = limit.ToString() },
            TrivialBlock.StartTag,
            new[] { new ObservabilityDeclaration(observedSignal, SignalNature.PersistentState, InstrumentationMode.Sampled, 20, expected) },
            AssertionForm.When,
            new SettlingDeclaration("count unchanged across 3 consecutive scans", new[] { observedSignal }, 3),
            MaxDurationScans: 20,
            CompletionValue: 1,
            Array.Empty<BlacklistEntry>(),
            CompressionFactor: 1,
            AssertedBehaviours: new[] { "ramp-to-limit" },
            CompletionSignal: completionSignal,
            Kills: "a ramp that overshoots the limit by one step",
            BoundsUsed: new Dictionary<string, string>(StringComparer.Ordinal) { ["ramp_limit"] = limit.ToString() });

    private static LoopRequest Request(SubmissionVector? vector = null, TrivialBlockDefect defect = TrivialBlockDefect.None)
    {
        var v = vector ?? Vector();
        var observed = v.Expectations[0].Signal;

        return new LoopRequest(
            new[] { v },
            Enumeration(v.BoundsUsed, observed),
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true, declaredBy: "agent-m"),
            new AgentIdentity("agent-a"),
            ConflictGraph.Empty,
            LoopRunTests.Geometry(),
            new[] { new SlotRequest("S0", 2, 2) },
            new[] { Binding("S0") },
            new CopyLayerNaming(BlockNumber: 900),
            TrivialBlock.Generate(3000, blockNumber: 901, defect),
            RuntimeCompression: RuntimeCompression.Uncompressed,
            CompressionInputs: null,
            Deployment: new DeploymentDeclaration("loop-test-import", Array.Empty<S7ObjectDeclaration>()),
            TagMapReach: TagMapReach.Of(Array.Empty<S7Reach>()),

            // 2.7's join, stated under the SPECIFICATION's names, because that is what a vector cites and
            // what the gate is handed.
            SignalStorage: SignalStorageMap.Of(new[]
            {
                (CountSpecName, new SignalStorage("DemoUnit", TrivialBlock.CountTag)),
                (DoneSpecName, new SignalStorage("DemoUnit", TrivialBlock.DoneTag)),
            }),
            UnknownFields: Array.Empty<string>(),
            AnnotationFields: Array.Empty<string>(),

            // No submission document, so no derivable field was hand-authored. Gate 0c's claim, stated.
            Derivation: DerivationEvidence.NoDocument,

            // Gate 1b's flat ceiling — no scenario clock on these vectors. See LoopRunTests.Request.
            MaxIndexScans: 200,

            // Gate 5c: the coordinator who declared the binding, a fourth party. See LoopRunTests.Request.
            MapAuthor: new AgentIdentity("agent-k"));
    }

    // ---------------------------------------------------------------------------------------------
    // THE MEASURED DEFECT, END TO END
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_VECTOR_CITING_THE_SPEC_NAME_IS_ACTUALLY_OBSERVED_and_the_run_reaches_a_verdict_about_the_block()
    {
        // *** THE MEASURED SHAPE. *** Before the fix this ran to completion and came back with
        // observed "<never read>" on every assertion, because ResultRegisterOf matched on Tag.
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());
        var result = LoopRun.Execute(Request(), gateway);

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        var package = Assert.Single(result.Packages);

        // The three facts that separate "read it" from "ran past it", asserted separately because a
        // verdict alone would also be produced by a lucky default.
        Assert.Equal(CountSpecName, package.Assertions[0].Signal);
        Assert.Equal("10", package.Assertions[0].Observed);
        Assert.Equal(AssertionState.Held, package.Assertions[0].State);

        Assert.Equal(ResultVerdict.Pass, package.Verdict);
        Assert.True(package.ConclusiveAboutTheBlock);
    }

    [Fact]
    public void AND_IT_STILL_DISAGREES_WHEN_THE_BLOCK_IS_WRONG_so_the_join_did_not_buy_a_verdict_by_agreeing_with_everything()
    {
        // The positive control's other half. A lookup that resolved to the WRONG register would also
        // produce a decoded value and a confident verdict — this one has to track the defect.
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());
        var result = LoopRun.Execute(Request(defect: TrivialBlockDefect.OffByOneAtTheLimit), gateway);

        var package = Assert.Single(result.Packages);
        Assert.Equal("15", package.Assertions[0].Observed);
        Assert.Equal(ResultVerdict.Fail, package.Verdict);
    }

    [Fact]
    public void THE_COMPLETION_SIGNAL_JOINS_ON_THE_SPEC_NAME_TOO_and_the_wave_finishes_on_the_register_it_named()
    {
        // The second half of the same defect, and the more dangerous one: an unjoined completion name used
        // to fall back to result register 0. Here register 0 is the COUNT and register 1 is DONE, so a
        // fallback would have watched the count — and completion value 1 is a value the count passes on its
        // way up, so the wave would have stopped early and read a half-finished ramp. It does not.
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());
        var result = LoopRun.Execute(Request(), gateway);

        var package = Assert.Single(result.Packages);
        Assert.Equal(SlotOutcome.Completed, package.RunOutcome);
        Assert.Equal("10", package.Assertions[0].Observed);
    }

    // ---------------------------------------------------------------------------------------------
    // AND A NAME THAT JOINS TO NOTHING IS REFUSED BEFORE ANYTHING IS SPENT
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AN_EXPECTATION_NAMING_A_SIGNAL_THE_BINDING_DOES_NOT_CARRY_STOPS_THE_RUN_WITH_NOTHING_DEPLOYED()
    {
        // The remaining half after the lookup is fixed: a citation matching NEITHER key. It cannot be
        // observed by anything, so it is refused where SlotJoin is refused — above the device boundary.
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());
        var result = LoopRun.Execute(Request(Vector(observedSignal: "SPEC.NoSuchSignal")), gateway);

        Assert.Equal(LoopOutcome.NotBound, result.Outcome);

        // *** THE OBSERVABLE CONSEQUENCE, NOT THE OUTCOME CODE. *** A disconnected refusal can produce the
        // right enum and still have spent a download.
        Assert.Equal(0, gateway.Deployments);
        Assert.Equal(0, gateway.Opens);
        Assert.Empty(result.Packages);

        Assert.Contains("SPEC.NoSuchSignal", result.Detail, StringComparison.Ordinal);

        // Both sides named, so the reader can tell which of the two documents to fix.
        Assert.Contains(CountSpecName, result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_COMPLETION_SIGNAL_NAMING_NOTHING_IS_REFUSED_AND_THE_REFUSAL_NAMES_THE_REGISTER_ZERO_HAZARD()
    {
        // *** THIS IS EXACTLY WHAT THE LIVE WAVE DID. *** `SPEC.Scenario_Done` matched neither the tag nor
        // the spec name (`SPEC.ScenarioDone`) — a near-miss that looks right — the poll watched result
        // register 0 — the stimulus model's PHASE code — and a 41-second scenario was declared complete
        // after EIGHT SCANS.
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());
        var result = LoopRun.Execute(Request(Vector(completionSignal: "SPEC.Scenario_Done")), gateway);

        Assert.Equal(LoopOutcome.NotBound, result.Outcome);
        Assert.Equal(0, gateway.Deployments);
        Assert.Contains("SPEC.Scenario_Done", result.Detail, StringComparison.Ordinal);
        Assert.Contains("register 0", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void THE_UNAFFECTED_CASE_a_binding_that_states_no_spec_name_still_joins_on_its_tag()
    {
        // Asserted as deliberately as the refusals. `JoinKey` falls back to the tag where no spec name was
        // stated, so a binding written the older way must keep working — a gate that fires on ordinary
        // submissions is noise, and noise gets switched off.
        var binding = new SlotBinding(
            "S0",
            MirroredSignal.Ints(TrivialBlock.StepTag, TrivialBlock.LimitTag),
            TrivialBlock.StartTag,
            new[]
            {
                new MirroredSignal(TrivialBlock.CountTag, MirrorValueType.Int),
                new MirroredSignal(TrivialBlock.DoneTag, MirrorValueType.Int),
            });

        Assert.Equal(0, binding.ResultRegisterOf(TrivialBlock.CountTag));
        Assert.Equal(1, binding.ResultRegisterOf(TrivialBlock.DoneTag));
        Assert.NotNull(binding.ResultSignal(TrivialBlock.CountTag));
    }
}
