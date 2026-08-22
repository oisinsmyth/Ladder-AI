using Harness.Loop;
using Harness.Map;
using Harness.Results;
using Harness.Skeleton;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE HARNESS KEPT EXACTLY ONE OBSERVATION PER INDEX — THE POLL THAT RECOGNISED COMPLETION — AND
/// THAT INSTANT IS, BY CONSTRUCTION, THE ONE INSTANT AT WHICH A WELL-BUILT MODEL HAS RETURNED THE BLOCK TO
/// INERT.</b>
///
/// <para><b>Measured on JOB9004's first live wave, 2026-08-17.</b> An assertion expecting a commanded state to be TRUE was sampled ~600 ms
/// after the scenario ended and read false — <i>a value impossible for ANY block at that instant</i> — and
/// the package reported <c>FAIL</c> against a block proven correct from an earlier frame on the device.
/// Its <c>conclusiveAboutTheBlock: 1</c> should have been <c>0</c>.</para>
///
/// <para><b>WHY NONE OF THE 1,770 EXISTING TESTS COULD FAIL ON IT, which is the reason this file exists
/// rather than a case in <c>LoopRunTests</c>.</b> Every fixture in this repository uses a block that HOLDS
/// its result at its final value once it raises completion — <c>TrivialBlock</c> and <c>PeakBlock</c> both
/// do. So for every one of them the completing frame IS the answer, the series and the snapshot agree, and
/// <i>a harness that kept only the snapshot was indistinguishable from one that kept everything.</i> That
/// is the same trap that hid the spec-name join defect one day earlier: <b>a fixture in which two things
/// coincide cannot tell them apart.</b></para>
///
/// <para><see cref="TailRecoveryBlock"/> is the fixture that can: its response is presented through a
/// window, WITHDRAWN, and only then is completion reported — so <b>the last poll and the in-window poll
/// disagree</b>, which is the whole of the defect.</para>
/// </summary>
public class ObservationWindowTests
{
    private const int MirrorBase = 4000;
    private const int ProgramBase = 3200;

    // The scenario, in ticks of the block's own counter. Arm opens LATER than the response rises and closes
    // WITH it, and completion comes well after both — so the series contains three genuinely different
    // kinds of frame: agreeing-but-out-of-window, agreeing-and-in-window, and disagreeing-and-out-of-window.
    private const int ArmAt = 10;
    private const int HoldUntil = 40;
    private const int EndAt = 60;

    private const string ClauseId = "REQ-021";
    private const string AssertionText = "WHEN the scenario is inside its window THEN the response is presented";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    private const string ResponseSpec = "SPEC.Response";
    private const string ArmedSpec = "SPEC.Armed";
    private const string DoneSpec = "SPEC.Done";

    private static MirrorGeometry Geometry() => MirrorGeometry.ForCpu1214C(256, MirrorBase, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - MirrorBase) / 2);

    private static AssertionEnumeration Enumeration() =>
        AssertionEnumeration.Of(
            new[] { ClauseId },
            new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When },
            "agent-c",
            new Dictionary<string, string>(StringComparer.Ordinal) { [AssertionIdValue] = AssertionText },
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [AssertionIdValue] = new HashSet<string>(StringComparer.Ordinal) { ResponseSpec },
            },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["window_hold_ticks"] = HoldUntil.ToString() });

    private static SubmissionVector Vector(
        InstrumentationMode mode = InstrumentationMode.Sampled,
        string expected = "true",
        string signal = ResponseSpec,
        TemporalShape temporalShape = TemporalShape.Unstated) =>
        new("V-T1", "S0", 0, new AgentIdentity("agent-b"),
            new Basis(ClauseId, AssertionIdValue),
            new Dictionary<string, string>
            {
                [TailRecoveryBlock.ArmTag] = ArmAt.ToString(),
                [TailRecoveryBlock.HoldTag] = HoldUntil.ToString(),
                [TailRecoveryBlock.EndTag] = EndAt.ToString(),
            },
            TailRecoveryBlock.StartTag,
            new[] { new ObservabilityDeclaration(signal, SignalNature.PersistentState, mode, 20, expected, temporalShape) },
            AssertionForm.When,

            // 🔴 *** SETTLING IS DECLARED, AND IT WILL PASS — WHICH IS THE MEASURED SHAPE, NOT A
            // CONVENIENCE. *** An inert tail is PERFECTLY SETTLED: the live package that reported FAIL
            // against a correct block carried `Settled`, which is exactly why settling could neither
            // detect this defect nor serve as a better answer for it. A fixture that reached its verdict
            // through `Unsettled` would be testing the wrong guard and would go green if the fix were
            // removed.
            new SettlingDeclaration("the result band is unchanged across 3 consecutive scans",
                new[] { ResponseSpec, DoneSpec }, 3),
            MaxDurationScans: 400,
            CompletionValue: 1,
            Array.Empty<BlacklistEntry>(),
            CompressionFactor: 1,
            AssertedBehaviours: new[] { "response-through-the-window" },
            CompletionSignal: DoneSpec,
            Kills: "a block that never presents the response inside the window",
            BoundsUsed: new Dictionary<string, string>(StringComparer.Ordinal) { ["window_hold_ticks"] = HoldUntil.ToString() });

    private static LoopRequest Request(
        SubmissionVector? vector = null,
        TailRecoveryShape shape = TailRecoveryShape.ResponsePresentedThroughTheWindow,
        bool latchTheResponse = false,
        bool declareTheArmWindow = false)
    {
        var binding = TailRecoveryBlock.Binding("S0", latchTheResponse, declareTheArmWindow);

        return new LoopRequest(
            new[] { vector ?? Vector() },
            Enumeration(),
            FidelityDeclaration.Of("M_Tail", new[] { "response-through-the-window" }, new[] { "the tail recovery itself" }, true, declaredBy: "agent-m"),
            new AgentIdentity("agent-a"),
            ConflictGraph.Empty,
            Geometry(),
            new[] { new SlotRequest("S0", binding.VectorRegistersNeeded, binding.ResultRegistersNeeded) },
            new[] { binding },
            new CopyLayerNaming(BlockNumber: 920),
            TailRecoveryBlock.Generate(ProgramBase, blockNumber: 921, shape),
            RuntimeCompression: RuntimeCompression.Uncompressed,
            CompressionInputs: null,
            Deployment: new DeploymentDeclaration("loop-test-import", Array.Empty<S7ObjectDeclaration>()),
            TagMapReach: TagMapReach.Of(Array.Empty<S7Reach>()),
            SignalStorage: SignalStorageMap.Of(new[]
            {
                (ResponseSpec, new SignalStorage("DemoTail", TailRecoveryBlock.ResponseTag)),
                (ArmedSpec, new SignalStorage("DemoTail", TailRecoveryBlock.ArmedTag)),
                (DoneSpec, new SignalStorage("DemoTail", TailRecoveryBlock.DoneTag)),
            }),
            UnknownFields: Array.Empty<string>(),
            AnnotationFields: Array.Empty<string>(),

            // No submission document, so no derivable field was hand-authored. Gate 0c's claim, stated.
            Derivation: DerivationEvidence.NoDocument,

            // Gate 1b's flat ceiling: these vectors are ramp-to-limit and have no scenario clock, so
            // there is nothing tighter to bound MaxDuration against. See LoopRunTests.Request.
            //
            // 800 rather than that file's 200 because THIS fixture declares MaxDurationScans: 400 — these
            // tests are about observation WINDOWS and need a long one. The ceiling is a per-submission
            // decision for exactly this reason, and 200 would refuse the fixture correctly.
            MaxIndexScans: 800);
    }

    /// <summary>One scan per transaction, so the poll rate resolves the window rather than stepping over it.</summary>
    private static SimulatedGateway Gateway() => new(Geometry(), scansPerTransaction: 1);

    private static ResultPackage Run(LoopRequest request, out LoopResult result)
    {
        result = LoopRun.Execute(request, Gateway());

        // The refusals are in the message, because "NotAdmissible" alone sends a reader to guess which of
        // twenty-five gates fired.
        Assert.True(result.Outcome == LoopOutcome.Ran,
            $"{result.Outcome}: {result.Detail} || "
            + string.Join(" | ", result.Gate?.Refused.Select(r => $"{r.Reason}: {r.Detail}") ?? Array.Empty<string>()));

        return Assert.Single(result.Packages);
    }

    // -------------------------------------------------------------------------------------------------
    // THE FIXTURE ITSELF — asserted before anything is concluded from it
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE FIXTURE TRAP, CLOSED EXPLICITLY: the last frame and the in-window frames MUST disagree.</b>
    ///
    /// <para>Everything below is evidence about the observation path only if this holds. A series whose
    /// frames all agree — which is what every pre-existing fixture produces — would make every fold in this
    /// file return the same answer, and the whole file would pass while proving nothing.</para>
    /// </summary>
    [Fact]
    public void THE_FIXTURE_PRODUCES_A_SERIES_WHOSE_LAST_FRAME_DISAGREES_WITH_ITS_IN_WINDOW_FRAMES()
    {
        var package = Run(Request(), out var result);
        var run = result.Wave!.For(package.SlotIndex).Results[package.WaveIndex];
        var binding = TailRecoveryBlock.Binding("S0");

        var responseRegister = binding.ResultRegisterOf(ResponseSpec);
        var armedRegister = binding.ResultRegisterOf(ArmedSpec);

        static bool Bit(ResultFrame f, int register) => (f.Registers[register] & 1) == 1;

        // The denominators first, because a series of one frame would satisfy several of the assertions
        // below by accident.
        Assert.True(run.Observations.PollsObserved > 10, $"only {run.Observations.PollsObserved} poll round(s) — too few to resolve the window.");
        Assert.True(run.Observations.Frames.Count >= 3, $"only {run.Observations.Frames.Count} retained frame(s).");

        var inWindow = run.Observations.Frames.Where(f => Bit(f, armedRegister)).ToArray();
        Assert.NotEmpty(inWindow);

        // *** THE TRAP, ASSERTED: every in-window frame has the response UP, and the final frame has it
        // DOWN. *** These two facts are what make the file's conclusions attributable to the fold rather
        // than to a fixture in which every frame says the same thing.
        Assert.All(inWindow, f => Assert.True(Bit(f, responseRegister), $"in-window frame at scan {f.Scan} has the response down."));
        Assert.False(Bit(run.Observations.Final!, responseRegister), "the FINAL frame has the response UP — this fixture has no tail recovery and proves nothing.");

        // And the completion snapshot the old code used is that same final frame, so the old behaviour is
        // reproducible from this fixture rather than merely asserted about.
        Assert.Equal(run.Results, run.Observations.Final!.Registers);
    }

    // -------------------------------------------------------------------------------------------------
    // H-1 — THE MEASURED FALSE ACCUSATION
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE MEASURED SHAPE. Before this change: <c>Disagreed</c> and a package verdict of <c>Fail</c>
    /// against a block that did exactly what the specification asks.</b>
    ///
    /// <para>With no arm window declared for the signal, the harness has no basis for excluding any frame —
    /// so it does not exclude any, and reports what it actually saw: the response was present at some
    /// observed instants and absent at others, and nothing says which instant discharges the assertion.
    /// <b>That is <see cref="AssertionState.Inconclusive"/>, and it is deliberately weaker than the answer
    /// this used to give</b>, because the answer it used to give was wrong.</para>
    /// </summary>
    [Fact]
    public void A_RESPONSE_WITHDRAWN_BEFORE_COMPLETION_IS_NO_LONGER_REPORTED_AS_A_DISAGREEMENT()
    {
        var package = Run(Request(), out _);
        var assertion = Assert.Single(package.Assertions);

        Assert.Equal(AssertionState.Inconclusive, assertion.State);
        Assert.NotEqual(AssertionState.Disagreed, assertion.State);

        Assert.Equal(ResultVerdict.Inconclusive, package.Verdict);

        // *** THE COUNT THE ANALYSIS SAID SHOULD HAVE BEEN ZERO. ***
        Assert.False(package.ConclusiveAboutTheBlock);
        Assert.False(assertion.SaysSomethingAboutTheBlock);

        // It reports BOTH facts rather than picking one: the response WAS seen, at some frames and not
        // others, and the reader is told which repair makes it decidable.
        var window = Assert.IsType<ObservationWindow>(assertion.Window);
        Assert.Equal(ObservationSource.Series, window.Source);
        Assert.True(window.FramesAgreed > 0, "no frame agreed, so this is the wrong shape entirely.");
        Assert.True(window.FramesAgreed < window.FramesConsidered, "every frame agreed, so the fixture has no tail.");
        Assert.False(window.WindowWasDeclared);

        Assert.Contains("MUST NOT BE ACTIONED AGAINST THE BLOCK", package.WhatToDoNext, StringComparison.Ordinal);
        Assert.Contains("armedBy", package.WhatToDoNext, StringComparison.Ordinal);
        Assert.Contains("Latched", package.WhatToDoNext, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------------------
    // H-4 — THE WINDOW, AND THE OVER-FIRE CONVERSE
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>The window makes it decisive again, in the PASSING direction.</b> With the arm window declared,
    /// the out-of-window frames stop counting, every remaining frame agrees, and the assertion HOLDS.
    /// </summary>
    [Fact]
    public void WITH_THE_WINDOW_DECLARED_A_CORRECT_IN_WINDOW_OBSERVATION_STILL_PASSES()
    {
        var package = Run(Request(declareTheArmWindow: true), out _);
        var assertion = Assert.Single(package.Assertions);

        Assert.Equal(AssertionState.Held, assertion.State);
        Assert.Equal(ResultVerdict.Pass, package.Verdict);
        Assert.True(package.ConclusiveAboutTheBlock);

        var window = Assert.IsType<ObservationWindow>(assertion.Window);
        Assert.True(window.WindowWasDeclared);
        Assert.Equal(window.FramesConsidered, window.FramesAgreed);

        // *** THE FRAMES THAT WERE EXCLUDED ARE COUNTED, NEVER SILENTLY DROPPED. *** They include the
        // completing frame, which is why the old code got the opposite answer from the same run.
        Assert.True(window.FramesOutOfWindow > 0, "nothing was excluded, so the window did no work and this test proves nothing.");
        Assert.Equal(WindowState.OutOfWindow, window.WindowAtFinalFrame);
    }

    /// <summary>
    /// 🔴 <b>THE OVER-FIRE CONVERSE, AND IT IS THE ASSERTION THAT KEEPS THE WHOLE CHANGE HONEST.</b>
    ///
    /// <para>A fold that answered <c>Inconclusive</c> whenever the frames differ, or <c>Held</c> whenever
    /// the expected value appeared once, would pass every test above and would <b>stop detecting defects
    /// altogether</b>. Here the response is withdrawn BEFORE the window opens: over the whole series the two
    /// shapes are indistinguishable — both present the response at some frames and not others — and they
    /// differ only in the IN-WINDOW frames. <b>A window check reading the wrong register, or no register,
    /// could not tell them apart.</b></para>
    /// </summary>
    [Fact]
    public void AND_IT_STILL_ACCUSES_WHEN_THE_RESPONSE_IS_ABSENT_THROUGHOUT_THE_WINDOW()
    {
        var package = Run(
            Request(shape: TailRecoveryShape.ResponseWithdrawnBeforeTheWindowOpens, declareTheArmWindow: true),
            out _);

        var assertion = Assert.Single(package.Assertions);

        Assert.Equal(AssertionState.Disagreed, assertion.State);
        Assert.Equal(ResultVerdict.Fail, package.Verdict);
        Assert.True(package.ConclusiveAboutTheBlock);

        var window = Assert.IsType<ObservationWindow>(assertion.Window);
        Assert.Equal(0, window.FramesAgreed);
        Assert.True(window.FramesConsidered > 0, "nothing was considered, so this accusation rests on no observation at all.");
    }

    /// <summary>
    /// <b>And WITHOUT the window, that same defective build is only <c>Inconclusive</c>.</b>
    ///
    /// <para>Stated as its own test because it is the cost of the change and it should be visible rather
    /// than discovered: <b>an under-declared binding buys a weaker verdict.</b> The response is up for
    /// ticks 1..9 in this build too, so the series is mixed either way — and only the declared window
    /// separates "wrong" from "we cannot tell". That is the incentive the design intends, and it is far
    /// cheaper than the alternative, which was a confident FAIL taken at an instant nobody checked.</para>
    /// </summary>
    [Fact]
    public void THE_COST_OF_NOT_DECLARING_THE_WINDOW_IS_A_WEAKER_VERDICT_ON_THE_SAME_DEFECT()
    {
        var package = Run(Request(shape: TailRecoveryShape.ResponseWithdrawnBeforeTheWindowOpens), out _);
        var assertion = Assert.Single(package.Assertions);

        Assert.Equal(AssertionState.Inconclusive, assertion.State);
        Assert.Equal(ResultVerdict.Inconclusive, package.Verdict);
        Assert.False(package.ConclusiveAboutTheBlock);
    }

    // -------------------------------------------------------------------------------------------------
    // H-2 — THE LATCH, WHICH NEEDED NO PLC CHANGE AND NO RE-DOWNLOAD
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A <c>Latched</c> EXPECTATION NOW READS ITS LATCH REGISTER — and it answers the question the
    /// value register cannot.</b>
    ///
    /// <para>The latch is set inside the armed window and cleared only when the slot stops running an
    /// index, so it is STILL STANDING at completion while the value register has long since gone inert.
    /// <b>Same run, same registers, same instant, opposite answer</b> — which is exactly why this half
    /// needed no PLC change and no re-download: the latch registers were already in the band the client
    /// reads every poll.</para>
    /// </summary>
    [Fact]
    public void A_LATCHED_EXPECTATION_READS_THE_LATCH_AND_ANSWERS_WHAT_THE_VALUE_REGISTER_CANNOT()
    {
        var package = Run(
            Request(vector: Vector(mode: InstrumentationMode.Latched), latchTheResponse: true),
            out var result);

        var assertion = Assert.Single(package.Assertions);

        Assert.Equal(AssertionState.Held, assertion.State);
        Assert.Equal("true", assertion.Observed);
        Assert.Equal(ResultVerdict.Pass, package.Verdict);

        var window = Assert.IsType<ObservationWindow>(assertion.Window);
        Assert.Equal(ObservationSource.Latch, window.Source);

        // *** AND THE VALUE REGISTER AT THAT SAME INSTANT SAYS THE OPPOSITE. *** Asserted from the run
        // rather than assumed, because "the latch was read" and "the value register happened to agree" are
        // indistinguishable unless the two are shown to differ.
        var run = result.Wave!.For(package.SlotIndex).Results[package.WaveIndex];
        var binding = TailRecoveryBlock.Binding("S0", latchTheResponse: true);

        var valueRegister = binding.ResultRegisterOf(ResponseSpec);
        var latchRegister = binding.LatchRegisterOf(ResponseSpec);

        Assert.NotEqual(valueRegister, latchRegister);
        Assert.Equal(0, run.Results[valueRegister] & 1);
        Assert.Equal(1, run.Results[latchRegister] & 1);
    }

    /// <summary>
    /// <b>The latch's own converse: it must go the other way on a build that never presented the response
    /// inside the window.</b> A latch that is set whatever happens is a green that examined nothing.
    /// </summary>
    [Fact]
    public void AND_THE_LATCH_IS_CLEAR_WHEN_THE_RESPONSE_NEVER_APPEARED_INSIDE_THE_WINDOW()
    {
        var package = Run(
            Request(
                vector: Vector(mode: InstrumentationMode.Latched),
                shape: TailRecoveryShape.ResponseWithdrawnBeforeTheWindowOpens,
                declareTheArmWindow: true),
            out _);

        var assertion = Assert.Single(package.Assertions);

        Assert.Equal(AssertionState.Disagreed, assertion.State);
        Assert.Equal("false", assertion.Observed);
        Assert.Equal(ResultVerdict.Fail, package.Verdict);
    }

    // -------------------------------------------------------------------------------------------------
    // THE UNAFFECTED CASE, ASSERTED AS DELIBERATELY AS THE REST
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>A block that holds its result at completion is completely untouched by all of this.</b>
    ///
    /// <para>Asserted here rather than left to the 162 pre-existing loop tests, because the point is the
    /// contrast: the same fold, over a series whose frames all agree, returns exactly what the single
    /// snapshot returned. <i>A gate that fires on ordinary submissions is noise, and noise gets switched
    /// off.</i></para>
    /// </summary>
    [Fact]
    public void A_BLOCK_THAT_HOLDS_ITS_RESULT_AT_COMPLETION_IS_UNAFFECTED()
    {
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());
        var result = LoopRun.Execute(LoopRunTests.Request(), gateway);

        var package = Assert.Single(result.Packages);

        Assert.Equal(AssertionState.Held, package.Assertions[0].State);
        Assert.Equal(ResultVerdict.Pass, package.Verdict);
        Assert.True(package.ConclusiveAboutTheBlock);

        var window = Assert.IsType<ObservationWindow>(package.Assertions[0].Window);
        Assert.Equal(window.FramesConsidered, window.FramesAgreed);
    }

    // -------------------------------------------------------------------------------------------------
    // THE TEMPORAL SHAPE, PASSED THROUGH BY THE RUNNER — the observation-window model's last hop
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE RUNNER PASSES THE DECLARED SHAPE TO THE EVALUATOR, AND THIS IS WHAT PROVES IT.</b>
    ///
    /// <para><b>The evaluator has understood shapes since 2026-08-18 and the runner did not pass one</b> —
    /// <c>SeriesEvaluation.Evaluate</c>'s shape parameter is trailing and optional, so the omission compiled,
    /// ran, and reported <c>Unstated</c> for every expectation in every wave. The whole model was reachable
    /// only by a caller that did not exist. Measured on the wave of 2026-08-18: three vectors, all
    /// Inconclusive, <c>conclusiveAboutTheBlock: 0</c>, with 65 frames observed on two signals.</para>
    ///
    /// <para><b>Why one series and three shapes rather than one assertion.</b> A test asserting that a
    /// declared shape "changes the verdict" passes for any change at all, including a change that broke the
    /// fold. Here <b>one identical run yields three DIFFERENT states</b> — the unstated refusal, an
    /// existential pass, and a single-instant disagreement — so the argument must be both consulted and
    /// consulted correctly. Delete <c>e.Shape</c> from <c>LoopRun.Assertions</c> and all three collapse to
    /// <c>Inconclusive</c>.</para>
    /// </summary>
    [Fact]
    public void ONE_SERIES_UNDER_THREE_DECLARED_SHAPES_YIELDS_THREE_DIFFERENT_STATES()
    {
        // The did-not-run control, first: with no shape stated this series is the measured Inconclusive,
        // and if it were anything else every comparison below would be against the wrong baseline.
        var unstated = Run(Request(Vector()), out _);
        Assert.Equal(AssertionState.Inconclusive, Assert.Single(unstated.Assertions).State);

        // EXISTENTIAL: the response WAS present at some observed instant, and its later withdrawal is not
        // part of the claim. A pass — and one the author had to type to get.
        var atSomePoint = Run(Request(Vector(temporalShape: TemporalShape.AtSomePoint)), out _);
        Assert.Equal(AssertionState.Held, Assert.Single(atSomePoint.Assertions).State);

        // ONE DEFINED INSTANT: the last considered frame, which in this fixture is the withdrawn tail.
        // A disagreement — the OPPOSITE verdict from the same frames, which is what makes this pair
        // evidence about the shape rather than about the run.
        var atEnd = Run(Request(Vector(temporalShape: TemporalShape.AtEnd)), out _);
        Assert.Equal(AssertionState.Disagreed, Assert.Single(atEnd.Assertions).State);

        Assert.Equal(3, new[]
        {
            Assert.Single(unstated.Assertions).State,
            Assert.Single(atSomePoint.Assertions).State,
            Assert.Single(atEnd.Assertions).State,
        }.Distinct().Count());
    }

    /// <summary>
    /// 🔴 <b>THE ACCUSATION RULE SURVIVES THE INTEGRATION: a sharp shape still refuses to accuse without a
    /// declared arm window.</b>
    ///
    /// <para><c>Throughout</c> on this mixed series is the case the design note calls out — <i>"a shape may
    /// accuse only on the strength of a frame known to be inside the phase"</i>. With no <c>armedBy</c> the
    /// disagreeing frame may be the model's own inert tail, so the verdict stays <c>Inconclusive</c> and
    /// names the repair. <b>Widening what can be SAID must never widen what PASSES — or what FAILS.</b></para>
    /// </summary>
    [Fact]
    public void A_SHARP_SHAPE_STILL_REFUSES_TO_ACCUSE_WITH_NO_DECLARED_WINDOW()
    {
        var package = Run(Request(Vector(temporalShape: TemporalShape.Throughout)), out _);
        var assertion = Assert.Single(package.Assertions);

        Assert.Equal(AssertionState.Inconclusive, assertion.State);
        Assert.False(assertion.SaysSomethingAboutTheBlock);
        Assert.Contains("armedBy", package.WhatToDoNext, StringComparison.Ordinal);

        // And the converse in the same breath, so this is not simply "Throughout never accuses": with the
        // window declared the out-of-window tail stops counting, every remaining frame agrees, and the same
        // shape over the same block HOLDS.
        var windowed = Run(Request(Vector(temporalShape: TemporalShape.Throughout), declareTheArmWindow: true), out _);

        Assert.Equal(AssertionState.Held, Assert.Single(windowed.Assertions).State);
        Assert.True(windowed.ConclusiveAboutTheBlock);
    }
}
