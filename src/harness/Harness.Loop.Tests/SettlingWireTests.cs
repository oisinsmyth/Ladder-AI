using Harness.Gate;
using Harness.Results;
using Harness.Run;
using Harness.Skeleton;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b><c>settlingUnchangedForScans</c> — the field the checked type had and the WIRE FORMAT DID NOT.</b>
///
/// <para>*** EVERY SETTLING CLAIM THIS PROJECT HAS MADE THROUGH A DOCUMENT WAS VACUOUS. ***
/// <c>SettlingDeclaration.UnchangedForScans</c> is the one settling form the runner can EVALUATE, and
/// <c>GateCli.ToSubmissionVector</c> constructed every declaration with <c>0</c> — the value that means
/// <i>"prose the runner cannot check"</i>. <c>LoopRun.Settling</c> returns on that first line, so every
/// vector in every submission came back <see cref="SettlingState.NotEstablished"/>: not wrong, not
/// refused, simply never evaluated. Fourth instance of <i>the domain model gained a field and the wire
/// format did not</i>, and the same shape as the flag whose refusal no caller could reach.</para>
///
/// <para><b>These tests run the whole chain — JSON document → <c>LoopCli.Compose</c> → <c>LoopRun</c> →
/// result package — against the LAD interpreter</b>, because the defect lived strictly between the
/// document and the checked type, which is a region no assertion about the typed objects can enter. The
/// existing settling tests build a <c>SettlingDeclaration</c> directly and were green throughout.</para>
/// </summary>
public class SettlingWireTests
{
    private const int MirrorBase = 4000;
    private const int ProgramBase = 3000;

    private const string ClauseId = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    /// <summary>A submission written against the skeleton block, with the settling field as a PARAMETER.</summary>
    private static string Submission(string settlingField) => $$"""
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 1,
      // Gate 1b's flat ceiling — these vectors have no scenario clock. See GateParityTests.
      "maxIndexScans": 200,
      "resultRegistersPerSlot": 2,
      "conflictEdges": [],
      "deployment": { "noS7Transport": true },
      "model": {
        "id": "M_Ramp", "represents": ["ramp-to-limit"], "doesNotRepresent": ["overshoot"],
        "validatedAgainstPlantData": true, "declaredBy": "agent-m"
      },
      "enumeration": {
        "clauses": ["{{ClauseId}}"],
        "assertions": ["{{AssertionIdValue}}"],
        "forms": { "{{AssertionIdValue}}": "When" },
        "enumerator": "agent-c",
        "normalisedTexts": { "{{AssertionIdValue}}": "{{AssertionText}}" },
        "requiredObservations": { "{{AssertionIdValue}}": ["{{TrivialBlock.CountTag}}"] },
        "bounds": { "ramp_limit": "10" }
      },
      "map": {
        "harnessOnly": ["{{TrivialBlock.CountTag}}", "{{TrivialBlock.DoneTag}}"],
        "providedFor": { "{{TrivialBlock.CountTag}}": ["Sampled"] }
      },
      "vectors": [{
        "id": "V-1", "slot": "S0", "index": 0, "author": "agent-b",
        "clause": "{{ClauseId}}", "assertion": "{{AssertionIdValue}}",
        "inputs": { "{{TrivialBlock.StepTag}}": "5", "{{TrivialBlock.LimitTag}}": "10" },
        "startBool": "{{TrivialBlock.StartTag}}",
        "assertionForm": "When",
        "expectations": [
          { "signal": "{{TrivialBlock.CountTag}}", "nature": "PersistentState", "mode": "Sampled", "windowScans": 20, "expected": "10" }
        ],
        "settlingCondition": "count unchanged across 3 consecutive scans",
        "settlingSignals": ["{{TrivialBlock.CountTag}}", "{{TrivialBlock.DoneTag}}"],
        {{settlingField}}
        "maxDurationScans": 20,
        "completionSignal": "{{TrivialBlock.DoneTag}}",
        "completionValue": 1,
        "compressionFactor": 1,
        "assertedBehaviours": ["ramp-to-limit"],
        "blacklist": [],
        "kills": "a ramp that overshoots the limit by one step",
        "boundsUsed": { "ramp_limit": "10" }
      }]
    }
    """;

    private static readonly string Binding = $$"""
    {
      "blockName": "FC_HarnessCopyLayer",
      "blockNumber": 900,
      "baseByte": {{MirrorBase}},
      "declaredRegisters": 576,
      "slots": [{
        "slotId": "S0",
        "startCondition": "{{TrivialBlock.StartTag}}",
        "vectorTargets": [
          { "tag": "{{TrivialBlock.StepTag}}", "specName": "{{TrivialBlock.StepTag}}", "type": "Int" },
          { "tag": "{{TrivialBlock.LimitTag}}", "specName": "{{TrivialBlock.LimitTag}}", "type": "Int" }
        ],
        "resultSources": [
          { "tag": "{{TrivialBlock.CountTag}}", "specName": "{{TrivialBlock.CountTag}}", "type": "Int",
            "inertRest": { "value": "0", "basis": "network 1 holds the count at 0 while the start command is off" } },
          { "tag": "{{TrivialBlock.DoneTag}}", "specName": "{{TrivialBlock.DoneTag}}", "type": "Int",
            "inertRest": { "value": "0", "basis": "network 1 holds the done flag at 0 while the start command is off" } }
        ]
      }]
    }
    """;

    private static ResultPackage RunFromWire(string settlingField)
    {
        var request = LoopCli.Compose(
            SubmissionDocument.Read(DerivedFixture.WithDerivation(Submission(settlingField), DerivedFixture.ArtifactPath, Binding)),
            BindingDocument.Read(Binding),
            TrivialBlock.Generate(ProgramBase, blockNumber: 901, TrivialBlockDefect.None),
            // The fixture is a DERIVED submission, so gate 0c can attribute its map, deployment and
            // conflict edges rather than refusing them as hand-authored. The reader exists only to serve
            // the artifact those records name: gate 0c re-hashes it, and an artifact it cannot read is
            // NOT CHECKED rather than a pass.
            readFile: DerivedFixture.ReaderFor(Binding));

        var result = LoopRun.Execute(request, new SimulatedGateway(Harness.Map.MirrorGeometry.ForCpu1214C(256, MirrorBase, declaredRegisters: (Harness.Map.MirrorGeometry.Cpu1214CBitMemoryBytes - MirrorBase) / 2)));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        return Assert.Single(result.Packages);
    }

    [Fact]
    public void A_DECLARED_SCAN_COUNT_REACHES_THE_RUNNER_AND_THE_VALUE_SETTLES()
    {
        // 🔴 *** BEFORE THE WIRE FIELD EXISTED THIS RETURNED NotEstablished, AND SO DID EVERY OTHER
        // VECTOR IN EVERY SUBMISSION. *** The runner's first line is `Settling is not
        // { UnchangedForScans: > 0 }`, and the document had no way to make it greater than zero.
        var package = RunFromWire("\"settlingUnchangedForScans\": 3,");

        Assert.Equal(SettlingState.Settled, package.Settling.State);
        Assert.Equal(ResultVerdict.Pass, package.Verdict);
    }

    [Fact]
    public void AN_OMITTED_SCAN_COUNT_IS_STILL_NotEstablished_because_prose_is_not_something_the_runner_can_check()
    {
        // The converse, and it is what stops the test above from passing against a field that is simply
        // hard-coded to something non-zero: zero remains legal, meaningful, and NOT a pass. Gate 6 still
        // requires a settling CONDITION, so this vector is admissible — it just cannot be evaluated.
        var package = RunFromWire(string.Empty);

        Assert.Equal(SettlingState.NotEstablished, package.Settling.State);
        Assert.Equal(ResultVerdict.Unsettled, package.Verdict);
    }

    [Fact]
    public void THE_DECLARED_NUMBER_IS_THE_NUMBER_CARRIED_rather_than_merely_non_zero()
    {
        // A field threaded as `x > 0 ? 3 : 0` would satisfy both tests above. This reads the value back
        // off the composed request, which is the artifact the runner is handed.
        foreach (var scans in new[] { 1, 3, 42 })
        {
            var request = LoopCli.Compose(
                SubmissionDocument.Read(DerivedFixture.WithDerivation(
                    Submission($"\"settlingUnchangedForScans\": {scans},"), DerivedFixture.ArtifactPath, Binding)),
                BindingDocument.Read(Binding),
                Array.Empty<Harness.Map.HarnessObject>(),
                readFile: DerivedFixture.ReaderFor(Binding));

            Assert.Equal(scans, Assert.Single(request.Vectors).Settling!.UnchangedForScans);
        }
    }

    [Fact]
    public void A_BLOCK_THAT_SAYS_DONE_AND_KEEPS_RUNNING_IS_CAUGHT_THROUGH_THE_WIRE_PATH_TOO()
    {
        // *** THE POINT OF THE FIELD, AND THE REASON ITS ABSENCE MATTERED. *** Phase 2's defective build
        // raises Done at 10 and goes on ramping to 15, so a snapshot is a CONFIDENTLY WRONG answer rather
        // than a missed one. With no wire field the settling check could never fire on any real
        // submission — the defect would have been read as a Pass or an ordinary Fail on the number.
        var request = LoopCli.Compose(
            SubmissionDocument.Read(DerivedFixture.WithDerivation(
                Submission("\"settlingUnchangedForScans\": 3,"), DerivedFixture.ArtifactPath, Binding)),
            BindingDocument.Read(Binding),
            TrivialBlock.Generate(ProgramBase, blockNumber: 901, TrivialBlockDefect.DoneWhileStillRunning),
            readFile: DerivedFixture.ReaderFor(Binding));

        var result = LoopRun.Execute(request, new SimulatedGateway(Harness.Map.MirrorGeometry.ForCpu1214C(256, MirrorBase, declaredRegisters: (Harness.Map.MirrorGeometry.Cpu1214CBitMemoryBytes - MirrorBase) / 2)));
        var package = Assert.Single(result.Packages);

        Assert.Equal(SettlingState.NotSettled, package.Settling.State);
        Assert.Equal(ResultVerdict.Unsettled, package.Verdict);
        Assert.False(package.ConclusiveAboutTheBlock);
    }
}
