using Harness.Loop;
using Harness.Map;
using Harness.Results;
using Harness.Skeleton;
using Harness.Wire;
using Xunit;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>EVERY VECTOR ON A SLOT CAN ESTABLISH SETTLING — NOT ONLY THE LAST ONE.</b>
///
/// <para>*** MEASURED ON A REAL WAVE, 2026-08-20, AND THE CAUSE WAS THE HARNESS RATHER THAN THE BLOCK.
/// *** Three vectors ran on one slot. Every assertion held — 1/1, 1/1, 4/4 — and the stimulus was
/// Confirmed on all three. The vector at the slot's LAST index came back <c>Pass</c>; the other two came
/// back <c>Unsettled</c>. Nothing about the block differed between them.</para>
///
/// <para><b>The reason was where the sample was taken.</b> Settling was decided while building the
/// result packages — after the WHOLE wave had finished — by re-reading the device. By then the next
/// index's inert phase had moved the program on, so <c>LoopRun.Settling</c> refused any index that was
/// not the slot's last, and refused it correctly <i>given where it was called</i>. <b>Two thirds of that
/// wave's vectors were unanswerable by construction.</b></para>
///
/// <para><b>The dwell now happens at each index's own close</b>, inside <c>WaveRun</c>, before the caller
/// advances. This file is where that shows: <b>the first test below fails against the old code</b>, which
/// is the only reason it is worth having.</para>
/// </summary>
public class SettlingEveryIndexTests
{
    private const int MirrorBase = 4000;
    private const int ProgramBase = 3000;

    private const string ClauseId = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    // ---------------------------------------------------------------------------------------------
    // the load-bearing one
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THREE_VECTORS_ON_ONE_SLOT_ALL_SETTLE_not_only_the_last()
    {
        var result = Run(Vector("V-0", 0, step: 5), Vector("V-1", 1, step: 6), Vector("V-2", 2, step: 7));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.Equal(3, result.Packages.Count);

        // *** THE ASSERTION THAT FAILS AGAINST THE OLD CODE. *** It used to be exactly one Settled - the
        // last index - and two NotEstablished.
        Assert.All(result.Packages, p => Assert.Equal(SettlingState.Settled, p.Settling.State));
        Assert.All(result.Packages, p => Assert.Equal(ResultVerdict.Pass, p.Verdict));
    }

    [Fact]
    public void AND_EACH_PACKAGE_SETTLES_ON_ITS_OWN_INDEX_rather_than_inheriting_the_last_ones()
    {
        // A settled verdict copied from a neighbour would satisfy the test above. Each vector ramps by a
        // different step and therefore rests at a different count, so a package that settled against
        // another index's values would be reporting the wrong number here.
        var result = Run(Vector("V-0", 0, step: 5), Vector("V-1", 1, step: 6), Vector("V-2", 2, step: 7));

        Assert.Equal("20", result.Packages.Single(p => p.VectorId == "V-0").Assertions[0].Observed);
        Assert.Equal("24", result.Packages.Single(p => p.VectorId == "V-1").Assertions[0].Observed);
        Assert.Equal("21", result.Packages.Single(p => p.VectorId == "V-2").Assertions[0].Observed);
    }

    // ---------------------------------------------------------------------------------------------
    // the converse — the move must not have disarmed the check
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_BLOCK_THAT_KEEPS_MOVING_IS_STILL_CAUGHT_AT_EVERY_INDEX()
    {
        // *** THE DIRECTION THAT MATTERS MOST. *** Making every index settle is worthless if it made every
        // index settle REGARDLESS. The defective block raises Done and goes on ramping, so the declared
        // settling signals are still moving after the close - at every index, not just the last.
        var result = Run(TrivialBlockDefect.DoneWhileStillRunning,
            Vector("V-0", 0, step: 5), Vector("V-1", 1, step: 6));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.All(result.Packages, p => Assert.Equal(SettlingState.NotSettled, p.Settling.State));
    }

    [Fact]
    public void AND_THE_FAILURE_NAMES_THE_SIGNAL_AND_THE_REGISTER_AT_EVERY_INDEX()
    {
        // The declaration is written in signal names, so the finding has to come back in them - on the
        // earlier indices too, which is where a wave of unsettled verdicts previously said nothing at all.
        var result = Run(TrivialBlockDefect.DoneWhileStillRunning,
            Vector("V-0", 0, step: 5), Vector("V-1", 1, step: 6));

        Assert.All(result.Packages, p =>
        {
            Assert.Contains(TrivialBlock.CountTag, p.Settling.Detail, StringComparison.Ordinal);
            Assert.Contains("moved", p.Settling.Detail, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void A_VECTOR_DECLARING_NO_SCAN_COUNT_IS_STILL_NotEstablished()
    {
        // The pre-existing refusals survive the move: a settling condition the runner cannot check is not
        // rescued by taking the sample earlier.
        var result = Run(Vector("V-0", 0, step: 5, settlingScans: 0));

        Assert.Equal(SettlingState.NotEstablished, Assert.Single(result.Packages).Settling.State);
    }

    // ---------------------------------------------------------------------------------------------
    // fixtures — one slot, several indices
    // ---------------------------------------------------------------------------------------------

    private static LoopResult Run(params SubmissionVector[] vectors) =>
        Run(TrivialBlockDefect.None, vectors);

    private static LoopResult Run(TrivialBlockDefect defect, params SubmissionVector[] vectors) =>
        LoopRun.Execute(Request(vectors, defect), new SimulatedGateway(Geometry()));

    private static MirrorGeometry Geometry() => MirrorGeometry.ForCpu1214C(256, MirrorBase);

    private static SubmissionVector Vector(string id, int index, int step, int settlingScans = 3)
    {
        const int limit = 20;

        // The ramp adds `step` while the count is below the limit, so it stops at the first multiple of
        // step at or above 20. Computed rather than tabulated, so the fixture cannot drift from the block.
        var expected = ((limit + step - 1) / step) * step;

        return new SubmissionVector(id, "S0", index, new AgentIdentity("agent-b"),
            new Basis(ClauseId, AssertionIdValue),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [TrivialBlock.StepTag] = step.ToString(),
                [TrivialBlock.LimitTag] = limit.ToString(),
            },
            TrivialBlock.StartTag,
            new[] { new ObservabilityDeclaration(TrivialBlock.CountTag, SignalNature.PersistentState, InstrumentationMode.Sampled, 20, expected.ToString()) },
            AssertionForm.When,
            new SettlingDeclaration("count and done unchanged across 3 consecutive scans",
                new[] { TrivialBlock.CountTag, TrivialBlock.DoneTag }, settlingScans),
            MaxDurationScans: 20,
            CompletionValue: 1,
            Array.Empty<BlacklistEntry>(),
            CompressionFactor: 1,
            AssertedBehaviours: new[] { "ramp-to-limit" },
            CompletionSignal: TrivialBlock.DoneTag,
            Kills: "a ramp that overshoots the limit by one step",
            BoundsUsed: new Dictionary<string, string>(StringComparer.Ordinal) { ["ramp_limit"] = limit.ToString() });
    }

    private static SlotBinding Binding() =>
        new("S0",
            new[]
            {
                new MirroredSignal(TrivialBlock.StepTag, MirrorValueType.Int, SpecName: TrivialBlock.StepTag),
                new MirroredSignal(TrivialBlock.LimitTag, MirrorValueType.Int, SpecName: TrivialBlock.LimitTag),
            },
            TrivialBlock.StartTag,
            new[]
            {
                new MirroredSignal(TrivialBlock.CountTag, MirrorValueType.Int, SpecName: TrivialBlock.CountTag,
                    Rest: InertRest.At("0", "the ramp block's network 1 holds the count at 0 while the start command is off")),
                new MirroredSignal(TrivialBlock.DoneTag, MirrorValueType.Int, SpecName: TrivialBlock.DoneTag,
                    Rest: InertRest.At("0", "the ramp block's network 1 holds the done flag at 0 while the start command is off")),
            });

    private static LoopRequest Request(IReadOnlyList<SubmissionVector> vectors, TrivialBlockDefect defect) =>
        new(vectors,
            AssertionEnumeration.Of(new[] { ClauseId }, new[] { AssertionIdValue },
                new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When },
                "agent-c",
                new Dictionary<string, string>(StringComparer.Ordinal) { [AssertionIdValue] = AssertionText },
                new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                {
                    [AssertionIdValue] = new HashSet<string>(StringComparer.Ordinal) { TrivialBlock.CountTag },
                },
                vectors[0].BoundsUsed),
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true, declaredBy: "agent-m"),
            new AgentIdentity("agent-a"),
            ConflictGraph.Empty,
            Geometry(),
            new[] { new SlotRequest("S0", 2, 2) },
            new[] { Binding() },
            new CopyLayerNaming(BlockNumber: 900),
            TrivialBlock.Generate(ProgramBase, blockNumber: 901, defect),
            RuntimeCompression: RuntimeCompression.Uncompressed,
            Deployment: new DeploymentDeclaration("loop-test-import", Array.Empty<S7ObjectDeclaration>()),
            TagMapReach: TagMapReach.Of(Array.Empty<S7Reach>()),
            SignalStorage: SignalStorageMap.Of(new[]
            {
                (TrivialBlock.CountTag, new SignalStorage("DemoUnit", TrivialBlock.CountTag)),
                (TrivialBlock.DoneTag, new SignalStorage("DemoUnit", TrivialBlock.DoneTag)),
            }),
            UnknownFields: Array.Empty<string>(),
            AnnotationFields: Array.Empty<string>(),

            // No submission document, so no derivable field was hand-authored. Gate 0c's claim, stated.
            Derivation: DerivationEvidence.NoDocument,

            // Gate 1b's flat ceiling — no scenario clock on these vectors. See LoopRunTests.Request.
            MaxIndexScans: 200);
}
