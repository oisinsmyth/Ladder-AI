using Harness.Gate;
using Harness.Map;
using Harness.Run;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>FOLLOW THE GENERATION DECLARATION FROM THE BINDING FILE TO THE EMITTED IR — the fifth instance of
/// "the domain model gained a field and nothing could set it", except that here it was worse.</b>
///
/// <para><c>SlotFcGenerator</c> and <c>StimShellGenerator</c> were not merely unreachable from the wire:
/// they were unreachable from PRODUCTION ALTOGETHER. Both were called only from their own tests, so the two
/// objects whose entire content is derived from a handful of declared names were still hand-authored on
/// every lane, and standing a lane up meant typing eight <c>.ir</c> artifacts while the harness generated
/// two.</para>
///
/// <para><b>These tests cross the seams no assertion about the objects can enter</b> — the JSON to the
/// domain, the domain to <see cref="LoopRun.Generate"/>, and the generation to what
/// <c>harness-run --generate-only --emit</c> actually writes.</para>
/// </summary>
public class LaneGenerationReachesTheRunTests
{
    private const string Submission = """
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 1,
      "maxIndexScans": 200,
      "resultRegistersPerSlot": 4,
      "computedConflicts": [],
      "enumeration": { "clauses": ["REQ-1"], "assertions": ["REQ-1:aaaaaa"] },
      "vectors": []
    }
    """;

    /// <summary>
    /// A lane that declares NOTHING — the shape of every binding written before the field existed, and the
    /// case that must keep behaving exactly as it did.
    /// </summary>
    private const string BindingUndeclared = """
    {
      "declaredBy": "agent-k",
      "blockNumber": 9001,
      "baseByte": 1000,
      "declaredRegisters": 576,
      "slots": [{
        "slotId": "WIDGET",
        "vectorTargets": [{ "tag": "Stim_Total", "type": "Time" }],
        "startCondition": "Stim_Start",
        "resultSources": [{ "tag": "Alarm", "type": "Bool",
                            "inertRest": { "value": "false", "basis": "the coil is ANDed with the start condition" } }]
      }]
    }
    """;

    /// <summary>
    /// 🔴 <b>THE WHOLE DECLARATION SURFACE, AND ITS SIZE IS THE POINT.</b> Every head name here is
    /// INVENTED; see <c>Harness.Map.Tests.StimShellGeneratorTests</c> for why that matters and for what a
    /// green run of it does and does not establish.
    /// </summary>
    private const string BindingDeclared = """
    {
      "declaredBy": "agent-k",
      "blockNumber": 9001,
      "baseByte": 1000,
      "declaredRegisters": 576,
      "slots": [{
        "slotId": "WIDGET",
        "vectorTargets": [{ "tag": "Stim_Total", "type": "Time" }],
        "startCondition": "Stim_Start",
        "resultSources": [{ "tag": "Alarm", "type": "Bool",
                            "inertRest": { "value": "false", "basis": "the coil is ANDed with the start condition" } }],
        "generate": {
          "slotFcName": "FC_HarnessWidgetSlot",
          "slotFcNumber": 9010,
          "stimulusHead":   { "block": "FB_WidgetStim",      "instance": "iDB_WidgetStim",      "networkTitle": "Advance The Widget Stimulus Head" },
          "blockUnderTest": { "block": "FB_WidgetUnderTest", "instance": "iDB_WidgetUnderTest", "networkTitle": "Run The Widget Under Test" },
          "stimHead": {
            "headName": "FB_WidgetStim",
            "uutReset": "iDB_WidgetUnderTest.Cmd.FaultReset",
            "watchdog": "T#5M",
            "dwell": "T#200MS",
            "phases": [
              { "bit": "InHeadDisarm", "end": "HeadDisarmEnd", "duration": "DisarmDwell", "kind": "Disarm" },
              { "bit": "InHeadReset",  "end": "HeadResetEnd",  "duration": "ResetDwell",  "kind": "Reset" },
              { "bit": "InHeadVerify", "end": "HeadEnd",       "duration": "VerifyDwell", "kind": "Verify" },
              { "bit": "InScenario",   "end": "ScenEnd",       "duration": "Stim.EndAt",  "kind": "Scenario" },
              { "bit": "InTailDisarm", "end": "TailDisarmEnd", "duration": "DisarmDwell", "kind": "Disarm" },
              { "bit": "InTailReset",  "end": "TailResetEnd",  "duration": "ResetDwell",  "kind": "Reset" },
              { "bit": "InTailVerify", "end": "TailEnd",       "duration": "VerifyDwell", "kind": "Verify" }
            ],
            "causes": ["iDB_WidgetUnderTest.Status.JamLatched"],
            "cycleEdges": ["iDB_WidgetUnderTest.Status.StrokeComplete"],
            "outcomeBits": [
              "Stim.ArrivedDirty", "Stim.InertAtStart", "Stim.RecoverFailed",
              "Stim.InertAtEnd", "Stim.CycleCounted", "Stim.ScenarioDone"
            ]
          }
        }
      }]
    }
    """;

    private const string BlockIr = "BLOCK FC FC_DemoRamp\n  NUMBER 901\n  NETWORK 1 \"ramp\"\n";

    private static LoopRequest Compose(string binding, params HarnessObject[] program) =>
        LoopCli.Compose(
            SubmissionDocument.Read(Submission),
            BindingDocument.Read(binding),
            program);

    private static LoopGeneration Generate(string binding, params HarnessObject[] program) =>
        LoopRun.Generate(Compose(binding, program), stopWhenInadmissible: false);

    // ---------------------------------------------------------------------------------------------
    // THE WIRE
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_DECLARATION_REACHES_THE_DOMAIN_WHOLE()
    {
        var declaration = Assert.Single(Compose(BindingDeclared).LaneDeclarations!);

        Assert.Equal("WIDGET", declaration.SlotId);
        Assert.Equal("FC_HarnessWidgetSlot", declaration.SlotFc!.BlockName);
        Assert.Equal(9010, declaration.SlotFc.BlockNumber);
        Assert.Equal("FB_WidgetStim", declaration.StimulusHead!.BlockName);
        Assert.Equal("iDB_WidgetStim", declaration.StimulusHead.InstancePath);
        Assert.Equal("Advance The Widget Stimulus Head", declaration.StimulusHead.NetworkTitle);
        Assert.Equal("FB_WidgetUnderTest", declaration.BlockUnderTest!.BlockName);

        // The head spec, including the one field the generator actually READS.
        Assert.Equal("FB_WidgetStim", declaration.StimHead!.HeadName);
        Assert.Equal(7, declaration.StimHead.Phases.Count);
        Assert.Equal(2, declaration.StimHead.Phases.Count(p => p.Kind == StimPhaseKind.Reset));
    }

    [Fact]
    public void AN_UNDECLARED_SLOT_STILL_TRAVELS_because_omitting_it_would_hide_that_it_is_authored()
    {
        var declaration = Assert.Single(Compose(BindingUndeclared).LaneDeclarations!);

        Assert.Equal("WIDGET", declaration.SlotId);
        Assert.True(declaration.Empty);
    }

    [Theory]
    // A name with no number: hard rule 3 forbids inventing one, and the message must say where the number
    // comes from rather than only that it is missing.
    [InlineData("\"slotFcNumber\": 9010,", "", "slotFcNumber")]
    // A network title the generator will not invent (C-201).
    [InlineData("\"networkTitle\": \"Advance The Widget Stimulus Head\"", "\"networkTitle\": \"\"", "networkTitle")]
    // A typo'd phase kind. *** REFUSED RATHER THAN TREATED AS UNSTATED: *** Reset is the only kind the
    // shell reads, so a fallback would silently produce a head that can never clear the block under test.
    [InlineData("\"kind\": \"Reset\" }", "\"kind\": \"Rest\" }", "not one of Disarm, Reset")]
    public void A_HALF_STATED_DECLARATION_IS_REFUSED_BY_NAME_rather_than_silently_dropped(
        string find, string replace, string expected)
    {
        var mangled = BindingDeclared.Replace(find, replace, StringComparison.Ordinal);
        Assert.NotEqual(BindingDeclared, mangled);

        var error = Assert.Throws<InvalidDataException>(() => Compose(mangled));
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
        Assert.Contains("WIDGET", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GATE_0b_REACHES_INSIDE_THE_DECLARATION_because_a_dropped_key_here_reads_as_authored()
    {
        // 🔴 THE QUIETEST DROPPED FIELD IN THE DOCUMENT. A misspelt key here produces NO block, reported
        // as "nothing was declared, so this is authored" — a true sentence about a document that plainly
        // asked for generation. Gate 0b is the only thing that can tell the two apart.
        var document = BindingDocument.Read(
            BindingDeclared.Replace("\"slotFcNumber\":", "\"slotFcNumbr\":", StringComparison.Ordinal));

        var (unknown, _) = document.AllExtraFieldPaths();
        Assert.Contains(unknown, f => f.EndsWith("generate.slotFcNumbr", StringComparison.Ordinal));

        // And to full depth — the nested objects are where the head spec lives.
        var nested = BindingDocument.Read(
            BindingDeclared.Replace("\"uutReset\":", "\"uutRest\":", StringComparison.Ordinal));

        Assert.Contains(nested.AllExtraFieldPaths().Unknown, f => f.EndsWith("generate.stimHead.uutRest", StringComparison.Ordinal));

        var phase = BindingDocument.Read(
            BindingDeclared.Replace("\"bit\": \"InHeadDisarm\"", "\"bt\": \"InHeadDisarm\"", StringComparison.Ordinal));

        Assert.Contains(phase.AllExtraFieldPaths().Unknown, f => f.EndsWith("generate.stimHead.phases[0].bt", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------
    // THE RUN
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void GENERATE_EMITS_THE_SLOT_FC_ALONGSIDE_THE_COPY_LAYER()
    {
        var generation = Generate(BindingDeclared);

        Assert.True(generation.Generated);
        Assert.NotNull(generation.Lane);

        var fc = Assert.Single(generation.Lane!.Objects);
        Assert.Equal("FC_HarnessWidgetSlot", fc.Name);
        Assert.Contains("CALL FB_WidgetStim(iDB_WidgetStim, EN := TRUE)", fc.Ir, StringComparison.Ordinal);

        // The shell too — as a FRAGMENT, which is why it is not on Objects.
        var fragment = Assert.Single(generation.Lane.Fragments);
        Assert.Equal(18, fragment.Shell.Networks.Count);
        Assert.DoesNotContain(generation.Lane.Objects, o => o.Name.Contains("WidgetStim", StringComparison.Ordinal));
    }

    [Fact]
    public void AN_UNDECLARED_LANE_GENERATES_NOTHING_AND_THE_STAMP_IS_BYTE_FOR_BYTE_UNMOVED()
    {
        // 🔴 *** THE BACKWARD-COMPATIBILITY ASSERTION, AND IT IS ABOUT THE STAMP BECAUSE THE STAMP IS WHAT
        // A DEPLOYED CONTROLLER IS CONFIRMED AGAINST. *** Every lane currently deployed declares nothing.
        // If merely adding this seam moved their stamp, every one of them would report Stale — whose text
        // says the download "aborted, was refused, or never reached it" — and send somebody to re-download
        // a device that was already right.
        var program = new[] { new HarnessObject("FC_DemoRamp", HarnessObjectKind.Block, BlockIr) };

        var withSeam = Generate(BindingUndeclared, program);

        Assert.True(withSeam.Generated);
        Assert.Empty(withSeam.Lane!.Objects);
        Assert.Empty(withSeam.Lane.Fragments);

        // The same request composed with NO declarations at all — which is what a caller that predates the
        // field builds — must produce the identical stamp.
        var withoutSeam = LoopRun.Generate(
            Compose(BindingUndeclared, program) with { LaneDeclarations = null },
            stopWhenInadmissible: false);

        Assert.Equal(withoutSeam.Stamp.Value, withSeam.Stamp.Value);
        Assert.Equal(withoutSeam.Manifest!.Objects.Count, withSeam.Manifest!.Objects.Count);
    }

    [Fact]
    public void THE_GENERATED_SLOT_FC_IS_IN_THE_BUILD_STAMP_because_it_EXECUTES()
    {
        // 🔴 *** THE COPY LAYER IS THE ONE LEGITIMATE EXCLUSION AND THIS IS NOT IT. *** The copy layer
        // embeds the stamp, so hashing it would be circular. A slot FC embeds nothing and runs on the
        // controller, so leaving it out would let the declaration change the deployed program without
        // moving the stamp — two materially different programs carrying one stamp, which is the single
        // thing the version register exists to prevent.
        var declared = Generate(BindingDeclared);
        var undeclared = Generate(BindingUndeclared);

        Assert.Contains(declared.Manifest!.Objects, o => o.Name == "FC_HarnessWidgetSlot");
        Assert.DoesNotContain(undeclared.Manifest!.Objects, o => o.Name == "FC_HarnessWidgetSlot");
        Assert.NotEqual(undeclared.Stamp.Value, declared.Stamp.Value);

        // And the CONVERSE, without which the above would pass against a stamp that is simply unstable:
        // the same declaration twice is the same stamp.
        Assert.Equal(declared.Stamp.Value, Generate(BindingDeclared).Stamp.Value);
    }

    [Fact]
    public void A_PROMOTED_COPY_OF_A_GENERATED_OBJECT_IS_SUPERSEDED_AND_THE_SUBSTITUTION_IS_REPORTED()
    {
        // A caller who has promoted an earlier run's slot FC into the lane's ir/ hands it straight back in
        // through --program. Hashing both would put one object in the stamp twice; hashing the PROMOTED
        // one would stamp a file this run has just superseded. The generated version wins — and a swap
        // nobody is told about is the same defect one layer down, so it is a caveat.
        var stale = new HarnessObject("FC_HarnessWidgetSlot", HarnessObjectKind.Block, "BLOCK FC FC_HarnessWidgetSlot\n### stale\n");

        var generation = Generate(BindingDeclared, stale);

        Assert.Single(generation.Manifest!.Objects, o => o.Name == "FC_HarnessWidgetSlot");
        Assert.Contains(generation.Caveats, c => c.Id == "lane-generated-supersedes"
                                              && c.Detail.Contains("FC_HarnessWidgetSlot", StringComparison.Ordinal));

        // The stamp is over the GENERATED IR, so it equals the run that supplied no stale copy at all.
        Assert.Equal(Generate(BindingDeclared).Stamp.Value, generation.Stamp.Value);
    }

    [Fact]
    public void A_REFUSED_DECLARATION_STOPS_GENERATION_ENTIRELY_including_the_copy_layer()
    {
        // *** A COPY LAYER EMITTED BESIDE A REFUSED SLOT FC IS THE ORPHAN WITH THE PAPERWORK FILED. *** The
        // copy layer is the block that IS called; both halves of the start echo live inside it; the block
        // under test never runs and every vector times out reporting "commanded, observed to run".
        var broken = BindingDeclared.Replace(
            "\"blockUnderTest\": { \"block\": \"FB_WidgetUnderTest\", \"instance\": \"iDB_WidgetUnderTest\", \"networkTitle\": \"Run The Widget Under Test\" },",
            string.Empty, StringComparison.Ordinal);

        var generation = Generate(broken);

        Assert.False(generation.Generated);
        Assert.Equal(LoopOutcome.NotGeneratable, generation.Stopped);
        Assert.Null(generation.CopyLayer);
        Assert.Contains("only in part", generation.Lane!.Refusals.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void THE_OBLIGATIONS_REACH_THE_GENERATION_rather_than_being_left_in_the_generator()
    {
        var lane = Generate(BindingDeclared).Lane!;

        Assert.Contains(lane.Obligations, o =>
            o.Contains("FC_HarnessWidgetSlot", StringComparison.Ordinal)
            && o.Contains("MUST be called from the cyclic OB", StringComparison.Ordinal));

        Assert.Contains(lane.Obligations, o => o.Contains("THE DISARM LEVER", StringComparison.Ordinal));
    }

    /// <summary>
    /// Records what it was handed and then refuses, so <see cref="LoopRun.Execute"/> stops at the device
    /// boundary. <b>Deliberately not <c>SimulatedGateway</c></b>: that one parses every block with the LAD
    /// interpreter, and the question here is what the loop HANDS OVER, not what an interpreter makes of it.
    /// </summary>
    private sealed class RecordingGateway : IDeviceGateway
    {
        public List<HarnessObject> Handed { get; } = new();

        public DeploymentOutcome Deploy(IReadOnlyList<HarnessObject> objects, BuildStamp stamp)
        {
            Handed.AddRange(objects);
            return new DeploymentOutcome(false, false, new HashSet<string>(StringComparer.Ordinal),
                "this gateway records and refuses.");
        }

        public Harness.Wire.IRegisterTransport Open() => throw new NotSupportedException("nothing was deployed.");

        public void Dispose() { }
    }

    /// <summary>
    /// An ADMISSIBLE pair, because <see cref="LoopRun.Execute"/> stops at the gate and never reaches a
    /// gateway otherwise. Built from <see cref="GateParityTests"/>' fixtures by INJECTION rather than by
    /// hand, so it cannot drift from the pair the gate actually admits.
    /// </summary>
    private static LoopRequest Admissible(bool declaring)
    {
        var binding = GateParityTests.BindingJson();

        if (declaring)
        {
            binding = binding.Replace(
                "\"slotId\": \"S0\",",
                "\"slotId\": \"S0\", \"generate\": { \"slotFcName\": \"FC_HarnessWidgetSlot\", \"slotFcNumber\": 9010, "
                + "\"stimulusHead\": { \"block\": \"FB_WidgetStim\", \"instance\": \"iDB_WidgetStim\", \"networkTitle\": \"Advance The Widget Stimulus Head\" }, "
                + "\"blockUnderTest\": { \"block\": \"FB_WidgetUnderTest\", \"instance\": \"iDB_WidgetUnderTest\", \"networkTitle\": \"Run The Widget Under Test\" } },",
                StringComparison.Ordinal);
        }

        var submission = DerivedFixture.WithDerivation(
            GateParityTests.SubmissionJson(), DerivedFixture.ArtifactPath, GateParityTests.BindingJson());

        return LoopCli.Compose(
            SubmissionDocument.Read(submission),
            BindingDocument.Read(binding),
            new[] { new HarnessObject("FC_DemoRamp", HarnessObjectKind.Block, BlockIr) },
            DerivedFixture.ReaderFor(GateParityTests.BindingJson()));
    }

    [Fact]
    public void THE_GENERATED_SLOT_FC_IS_HANDED_TO_THE_GATEWAY_or_it_is_stamped_and_never_downloaded()
    {
        // 🔴 THE ORPHAN BY A DIFFERENT ROUTE. A slot FC that is generated, hashed into the stamp and then
        // NOT deployed leaves the copy layer running alone: its start echo reports "commanded, observed to
        // run" from both halves of itself and the block under test never executes. The deploy set and the
        // stamp input must be the same set, or the version register confirms a program other than the one
        // on the wire.
        var gateway = new RecordingGateway();

        LoopRun.Execute(Admissible(declaring: true), gateway);

        Assert.Contains(gateway.Handed, o => o.Name == "FC_HarnessWidgetSlot");
        Assert.Contains(gateway.Handed, o => o.Name == "FC_HarnessCopyLayer");
        Assert.Contains(gateway.Handed, o => o.Name == "FC_DemoRamp");

        // 🔴 THE NEGATIVE CONTROL. Without it the assertion above would pass against a gateway handed
        // everything in the process, and "the generated FC is deployed" would be worth nothing.
        var control = new RecordingGateway();
        LoopRun.Execute(Admissible(declaring: false), control);

        Assert.DoesNotContain(control.Handed, o => o.Name == "FC_HarnessWidgetSlot");
        Assert.Contains(control.Handed, o => o.Name == "FC_DemoRamp");
    }

    // ---------------------------------------------------------------------------------------------
    // THE CLI — what --generate-only PRINTS and what --emit WRITES
    // ---------------------------------------------------------------------------------------------

    private static (int Exit, string Output, List<string> Written) RunCli(string binding, params string[] extra)
    {
        var writer = new StringWriter();
        var written = new List<string>();

        var exit = LoopCli.Run(
            new[] { "--submission", "sub.json", "--binding", "binding.json", "--generate-only", "--no-program-under-test" }
                .Concat(extra).ToArray(),
            writer,
            path => path switch
            {
                "sub.json" => Submission,
                "binding.json" => binding,
                _ => throw new FileNotFoundException(path),
            },
            (path, _) => written.Add(path),
            null,
            _ => null,
            path => new[] { path });

        return (exit, writer.ToString(), written);
    }

    [Fact]
    public void GENERATE_ONLY_EMITS_THE_SLOT_FC_AS_A_DOT_IR_AND_THE_SHELL_INTO_A_SUBDIRECTORY()
    {
        var (_, output, written) = RunCli(BindingDeclared, "--emit", "out");

        Assert.Contains(Path.Combine("out", "FC_HarnessWidgetSlot.ir"), written);

        // 🔴 THE FRAGMENT IS NOT A `.ir` BESIDE THE BLOCKS. Every loader that turns an emit directory back
        // into a --program list globs `*.ir` non-recursively, and a fragment there would be picked up as a
        // block that cannot be imported.
        Assert.Contains(Path.Combine("out", "stim-shell", "FB_WidgetStim.networks.ir"), written);
        Assert.Contains(Path.Combine("out", "stim-shell", "FB_WidgetStim.requires.txt"), written);
        Assert.DoesNotContain(Path.Combine("out", "FB_WidgetStim.ir"), written);

        Assert.Contains("A FRAGMENT — networks only, NOT an importable block", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GENERATE_ONLY_PRINTS_THE_OBLIGATION_THAT_COST_A_WAVE()
    {
        var (_, output, _) = RunCli(BindingDeclared);

        Assert.Contains("OBLIGATIONS", output, StringComparison.Ordinal);
        Assert.Contains("'FC_HarnessWidgetSlot' MUST be called from the cyclic OB", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GENERATE_ONLY_SAYS_AUTHORED_FOR_A_LANE_THAT_DECLARES_NOTHING_and_still_generates_the_copy_layer()
    {
        // 🔴 *** THE NEGATIVE, AND THE ONE THAT MATTERS MOST. *** Every lane deployed today is in this
        // case. It must keep working, and a reader of its report must not be able to mistake it for a lane
        // whose test side is generated.
        var (exit, output, written) = RunCli(BindingUndeclared, "--emit", "out");

        // The IR was PRODUCED. The exit code here is the gate's — this fixture's vectors are not the
        // subject — and what matters is that generation itself was not stopped.
        Assert.NotEqual(LoopExit.DidNotRun, exit);
        Assert.Contains("GENERATED — ", output, StringComparison.Ordinal);

        Assert.Contains("AUTHORED   slot 'WIDGET': no slot FC was declared", output, StringComparison.Ordinal);
        Assert.Contains("AUTHORED   slot 'WIDGET': no stimulus head spec was declared", output, StringComparison.Ordinal);
        Assert.DoesNotContain("GENERATED  slot", output, StringComparison.Ordinal);

        // The copy layer and the mirror table are still emitted — this lane behaves exactly as it did.
        Assert.Contains(Path.Combine("out", "FC_HarnessCopyLayer.ir"), written);
        Assert.DoesNotContain(written, p => p.Contains("stim-shell", StringComparison.Ordinal));
    }

    [Fact]
    public void THE_LANE_SECTION_IS_PRINTED_ON_EVERY_RUN_including_the_one_with_nothing_to_say()
    {
        // A section that appears only when something was generated teaches a reader that its absence means
        // there was nothing left to generate — which is the opposite of the truth on every lane that exists.
        Assert.Contains("LANE        :", RunCli(BindingUndeclared).Output, StringComparison.Ordinal);
        Assert.Contains("LANE        :", RunCli(BindingDeclared).Output, StringComparison.Ordinal);
    }
}
