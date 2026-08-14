using Harness.Loop;
using Harness.Map;
using Harness.Results;
using Harness.Skeleton;
using Harness.Wire;
using Xunit;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE MANY-TO-ONE SLOT MAP AND THE JOIN KEY, DRIVEN THROUGH THE WHOLE LOOP.</b>
///
/// <para>Two defects meet here and both are invisible to any check that stops at the objects:</para>
/// <list type="number">
/// <item><b>The writer joined on the block TAG while every vector keys its inputs by the SPEC name.</b>
/// <c>MirrorValueFit</c> was corrected to the spec name; <c>LoopRun.ToWireVector</c> was not, so on a
/// binding where the two differ the width gate checked values that were then never written and every
/// stimulus register stayed at ZERO. The block ran a scenario nobody asked for and the packages arrived
/// looking ordinary.</item>
/// <item><b>Six specification ids serving one mirror slot merge into one tensor, and the submission
/// carries no total order</b> — each group restarts <c>index</c> at 0. Without a major key, one vector
/// per group reads <c>Results[0]</c> and the rest carry another vector's run.</item>
/// </list>
///
/// <para><b>Every test here asserts an OBSERVED VALUE rather than an outcome code.</b> Both defects
/// produce a run that completes; what they change is which number comes back.</para>
/// </summary>
public class ManyToOneSlotTests
{
    private const int MirrorBase = 4000;
    private const int ProgramBase = 3000;

    private const string ClauseId = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    // The SPECIFICATION's names for the two stimulus inputs — deliberately different from the block's
    // tag names, which is the whole point. On the deliverable all ten differ.
    private const string SpecStep = "SPEC.Step";
    private const string SpecLimit = "SPEC.Limit";

    private static MirrorGeometry Geometry() => MirrorGeometry.ForCpu1214C(256, MirrorBase);

    private static AssertionEnumeration Enumeration(IReadOnlyDictionary<string, string>? bounds = null) =>
        AssertionEnumeration.Of(
            new[] { ClauseId },
            new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When },
            "agent-c",
            new Dictionary<string, string>(StringComparer.Ordinal) { [AssertionIdValue] = AssertionText },
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [AssertionIdValue] = new HashSet<string>(StringComparer.Ordinal) { TrivialBlock.CountTag },
            },
            bounds);

    /// <summary>
    /// A vector citing its inputs by the SPEC names.
    ///
    /// <para><b>The vectors in one wave differ by STEP, never by LIMIT.</b> The limit is the SPECIFIED
    /// bound the enumeration's table records, and the bounds-currency gate refuses a submission whose
    /// vectors disagree with it — correctly. Varying the step keeps every vector bounds-current while
    /// still giving each a DISTINGUISHABLE observed count, which is what a package reading another
    /// vector's run has to be caught by.</para>
    /// </summary>
    private static SubmissionVector Vector(string id, string slot, int index, int step, bool citeByTag = false)
    {
        const int limit = 20;

        // The ramp adds `step` while the count is below the limit, so it stops at the first multiple of
        // step at or above 20. Computed rather than tabulated, so the fixture cannot drift from the block.
        var expected = ((limit + step - 1) / step) * step;

        return
            new SubmissionVector(id, slot, index, new AgentIdentity("agent-b"),
            new Basis(ClauseId, AssertionIdValue),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [citeByTag ? TrivialBlock.StepTag : SpecStep] = step.ToString(),
                [citeByTag ? TrivialBlock.LimitTag : SpecLimit] = limit.ToString(),
            },
            TrivialBlock.StartTag,
            new[] { new ObservabilityDeclaration(TrivialBlock.CountTag, SignalNature.PersistentState, InstrumentationMode.Sampled, 20, expected.ToString()) },
            AssertionForm.When,
            new SettlingDeclaration("count unchanged across 3 consecutive scans",
                new[] { TrivialBlock.CountTag, TrivialBlock.DoneTag }, 3),
            MaxDurationScans: 20,
            CompletionValue: 1,
            Array.Empty<BlacklistEntry>(),
            CompressionFactor: 1,
            AssertedBehaviours: new[] { "ramp-to-limit" },
            CompletionSignal: TrivialBlock.DoneTag,
            Kills: "a ramp that overshoots the limit by one step",
            BoundsUsed: new Dictionary<string, string>(StringComparer.Ordinal) { ["ramp_limit"] = limit.ToString() });
    }

    /// <summary>The skeleton block's binding, with SPEC names on the stimulus inputs and an optional many-to-one map.</summary>
    private static SlotBinding Binding(
        string slotId = "S0",
        bool specNames = true,
        string[]? serves = null,
        bool orderStated = false,
        string[]? spanning = null) =>
        new(slotId,
            new[]
            {
                new MirroredSignal(TrivialBlock.StepTag, MirrorValueType.Int, specNames ? SpecStep : null),
                new MirroredSignal(TrivialBlock.LimitTag, MirrorValueType.Int, specNames ? SpecLimit : null),
            },
            TrivialBlock.StartTag,
            new[]
            {
                new MirroredSignal(TrivialBlock.CountTag, MirrorValueType.Int, SpecName: TrivialBlock.CountTag),
                new MirroredSignal(TrivialBlock.DoneTag, MirrorValueType.Int, SpecName: TrivialBlock.DoneTag),
            })
        {
            Serves = serves ?? Array.Empty<string>(),
            ServesRunInOrder = orderStated,
            BoundarySpanning = spanning ?? Array.Empty<string>(),
        };

    private static LoopRequest Request(IReadOnlyList<SubmissionVector> vectors, SlotBinding binding) =>
        new(vectors,
            Enumeration(vectors[0].BoundsUsed),
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true, declaredBy: "agent-m"),
            new AgentIdentity("agent-a"),
            ConflictGraph.Empty,
            Geometry(),
            new[] { new SlotRequest(binding.SlotId, 2, 2) },
            new[] { binding },
            new CopyLayerNaming(BlockNumber: 900),
            TrivialBlock.Generate(ProgramBase, blockNumber: 901, TrivialBlockDefect.None),
            RuntimeCompression: RuntimeCompression.Uncompressed,
            Deployment: new DeploymentDeclaration("loop-test-import", Array.Empty<S7ObjectDeclaration>()),
            TagMapReach: TagMapReach.Of(Array.Empty<S7Reach>()),
            SignalStorage: SignalStorageMap.Of(new[]
            {
                (TrivialBlock.CountTag, new SignalStorage("DemoUnit", TrivialBlock.CountTag)),
                (TrivialBlock.DoneTag, new SignalStorage("DemoUnit", TrivialBlock.DoneTag)),
            }),
            UnknownFields: Array.Empty<string>(),
            AnnotationFields: Array.Empty<string>());

    private static LoopResult Run(LoopRequest request) =>
        LoopRun.Execute(request, new SimulatedGateway(Geometry()));

    // ---------------------------------------------------------------------------------------------
    // THE JOIN KEY — the value must actually REACH the block
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AVectorKeyedByTheSPECNAME_ACTUALLY_DRIVES_THE_BLOCK()
    {
        // *** THE MUTATION THIS EXISTS FOR: *** revert ToWireVector to look its inputs up by
        // `target.Tag` and nothing matches, every stimulus register stays at zero, the ramp never runs,
        // and Count comes back 0 rather than 20. The run still completes, which is why the assertion is
        // on the OBSERVED VALUE and not on the outcome.
        var result = Run(Request(new[] { Vector("V-1", "S0", 0, step: 5) }, Binding()));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);

        var package = Assert.Single(result.Packages);
        Assert.Equal("20", package.Assertions[0].Observed);
        Assert.Equal(ResultVerdict.Pass, package.Verdict);
    }

    [Fact]
    public void AVectorKeyedByTheBLOCKTAG_IsREFUSED_ratherThanQuietlyUnwritten()
    {
        // The other direction. With spec names STATED, the tag is not also tried — and the input then
        // reaches no target, which is the orphan refusal. Before that refusal existed this ran to
        // completion against a stimulus of zeros.
        var result = Run(Request(new[] { Vector("V-1", "S0", 0, step: 5, citeByTag: true) }, Binding()));

        Assert.Equal(LoopOutcome.NotRepresentable, result.Outcome);
        Assert.Contains("NO BOUND TARGET CONSUMES IT", result.Detail);
    }

    [Fact]
    public void WithNoSpecNameStated_TheTagIsStillTheKey()
    {
        // The unaffected case: every binding written before `specName` existed keys on the tag, and must
        // keep working exactly as it did.
        var result = Run(Request(
            new[] { Vector("V-1", "S0", 0, step: 5, citeByTag: true) },
            Binding(specNames: false)));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.Equal("20", Assert.Single(result.Packages).Assertions[0].Observed);
    }

    // ---------------------------------------------------------------------------------------------
    // THE MANY-TO-ONE MAP
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SixIdsServingOneSlot_ALLRESOLVE_AndEachPackageCarriesITSOWNRun()
    {
        // Two groups, each restarting `index` at 0 — the deliverable's exact shape. The limits differ per
        // vector, so a package reading the wrong run reads the wrong NUMBER.
        //
        // *** WITHOUT THE ORDINAL, G-0 AND H-0 BOTH READ Results[0] AND H-0 REPORTS 20. ***
        var vectors = new[]
        {
            Vector("G-0", "GROUP-A", 0, step: 5),
            Vector("G-1", "GROUP-A", 1, step: 6),
            Vector("H-0", "GROUP-B", 0, step: 3),
        };

        var result = Run(Request(vectors, Binding(serves: new[] { "GROUP-A", "GROUP-B" }, orderStated: true)));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.Equal(3, result.Packages.Count);

        Assert.Equal("20", result.Packages.Single(p => p.VectorId == "G-0").Assertions[0].Observed);
        Assert.Equal("24", result.Packages.Single(p => p.VectorId == "G-1").Assertions[0].Observed);
        Assert.Equal("21", result.Packages.Single(p => p.VectorId == "H-0").Assertions[0].Observed);

        // *** AND THE MERGED ORDER IS OBSERVABLE IN THE VERDICTS, WHICH IS A SECOND, INDEPENDENT WITNESS
        // TO IT. *** Settling can only be established for a slot's LAST index — earlier ones have had an
        // inert phase move the program on — so exactly one package settles, and it is the last in the
        // MERGED order. GROUP-B is second in the stated order, so that is H-0. Were the groups merged the
        // other way round, or not merged at all, a different vector would be the settled one.
        Assert.Equal(ResultVerdict.Pass, result.Packages.Single(p => p.VectorId == "H-0").Verdict);
        Assert.Equal(ResultVerdict.Unsettled, result.Packages.Single(p => p.VectorId == "G-0").Verdict);
        Assert.Equal(ResultVerdict.Unsettled, result.Packages.Single(p => p.VectorId == "G-1").Verdict);
    }

    [Fact]
    public void ABindingThatDoesNOTNameTheIds_STILLREFUSES()
    {
        // *** MUTATE EVERYTHING, BOTH DIRECTIONS. *** The many-to-one map must not become a way for any
        // slot id to resolve to any slot. A binding that serves nothing answers only to its own id.
        var result = Run(Request(
            new[] { Vector("G-0", "GROUP-A", 0, step: 5) },
            Binding()));

        Assert.Equal(LoopOutcome.NotBound, result.Outcome);
        Assert.Contains("that no binding declares", result.Detail);
        Assert.Contains("'GROUP-A'", result.Detail);
    }

    [Fact]
    public void WithServesStated_TheSlotsOWNIdStopsBeingCitable()
    {
        // A vector citing the internal key has no group rank, and there is no correct rank to give it.
        var result = Run(Request(
            new[] { Vector("V-1", "S0", 0, step: 5) },
            Binding(serves: new[] { "GROUP-A", "GROUP-B" }, orderStated: true)));

        Assert.Equal(LoopOutcome.NotBound, result.Outcome);
        Assert.Contains("'S0'", result.Detail);
    }

    [Fact]
    public void SeveralGroupsWithNoStatedOrder_STOPBEFORETHEDEVICE()
    {
        var gateway = new SimulatedGateway(Geometry());

        var result = LoopRun.Execute(
            Request(
                new[] { Vector("G-0", "GROUP-A", 0, step: 5), Vector("H-0", "GROUP-B", 0, step: 3) },
                Binding(serves: new[] { "GROUP-A", "GROUP-B" }, orderStated: false)),
            gateway);

        Assert.Equal(LoopOutcome.NotOrdered, result.Outcome);
        Assert.Contains("THE ORDER THEY RUN IN IS NOT STATED", result.Detail);

        // *** NOTHING WAS SPENT. *** The refusal sits before the device boundary, so no deployment was
        // attempted and no transport was opened.
        Assert.Equal(0, gateway.Deployments);
        Assert.Equal(0, gateway.Opens);
    }

    // ---------------------------------------------------------------------------------------------
    // THE BOUNDARY-SPANNING GROUP
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ABoundarySpanningGroupsVectors_AreREFUSED_andNothingIsDeployed()
    {
        var gateway = new SimulatedGateway(Geometry());

        var result = LoopRun.Execute(
            Request(
                new[] { Vector("G-0", "GROUP-A", 0, step: 5), Vector("S-0", "GROUP-STARTUP", 0, step: 3) },
                Binding(serves: new[] { "GROUP-A", "GROUP-STARTUP" }, orderStated: true,
                        spanning: new[] { "GROUP-STARTUP" })),
            gateway);

        // Its own outcome, because the REMEDY differs from an unstated order: this one is fixed by
        // running the group around the download boundary its vectors declare.
        Assert.Equal(LoopOutcome.NotSchedulable, result.Outcome);
        Assert.Contains("BOUNDARY-SPANNING", result.Detail);
        Assert.Contains("S-0", result.Detail);
        Assert.Equal(0, gateway.Deployments);
    }

    [Fact]
    public void ABoundarySpanningGroupNOBODYCITES_LeavesAnOrdinaryWaveALONE()
    {
        // *** THE UNAFFECTED CASE. *** A gate that refuses ordinary submissions is removed within a
        // week, by someone who is right to.
        var result = Run(Request(
            new[] { Vector("G-0", "GROUP-A", 0, step: 5), Vector("G-1", "GROUP-A", 1, step: 6) },
            Binding(serves: new[] { "GROUP-A", "GROUP-STARTUP" }, orderStated: true,
                    spanning: new[] { "GROUP-STARTUP" })));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.Equal("20", result.Packages.Single(p => p.VectorId == "G-0").Assertions[0].Observed);
        Assert.Equal("24", result.Packages.Single(p => p.VectorId == "G-1").Assertions[0].Observed);
    }

    [Fact]
    public void ABoundarySpanningGroupsPOSITIONInServes_IsNotRead()
    {
        // Listed FIRST rather than last, with the same two ordinary vectors: the ordinals must not move.
        // If its position were read, GROUP-A would start one place later and G-0 would read G-1's run.
        var result = Run(Request(
            new[] { Vector("G-0", "GROUP-A", 0, step: 5), Vector("G-1", "GROUP-A", 1, step: 6) },
            Binding(serves: new[] { "GROUP-STARTUP", "GROUP-A" }, orderStated: true,
                    spanning: new[] { "GROUP-STARTUP" })));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.Equal("20", result.Packages.Single(p => p.VectorId == "G-0").Assertions[0].Observed);
        Assert.Equal("24", result.Packages.Single(p => p.VectorId == "G-1").Assertions[0].Observed);
    }
}
