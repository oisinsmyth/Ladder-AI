using Harness.Gate;
using Harness.Map;
using Harness.Results;
using Harness.Run;
using Harness.Skeleton;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b><c>quiescenceScans</c> — the inert check's WAIT, which the checked type had and NO DOCUMENT
/// COULD STATE.</b>
///
/// <para><c>InertDeclaration.QuiescenceScans</c> has existed since the inert phase did, and
/// <c>LoopRun</c> called <c>InertRestPlan.For(binding, wordOrder)</c> and never passed the third
/// argument — so it sat at the parameter default, <b>1</b>, for every slot in every submission. <b>The one
/// knob designed for "this model takes N scans to settle" was unreachable from any binding.</b></para>
///
/// <para><b>IT COST A WAVE.</b> After a download the check wrote the next vector and sampled ONE SCAN
/// (~24 ms) later, while the model's settle after a contents step is <b>~9 seconds</b>. It caught the
/// block mid-integration and refused — <c>NotQuiescent</c>, which reads as a defect in the program.
/// Deterministic rather than flaky: the first run after any download fails and the second passes, because
/// by then the model is already at rest.</para>
///
/// <para><b>These tests run the whole chain — JSON binding → <c>LoopCli.Compose</c> → the plan the wave
/// is built from</b>, because the defect lived strictly between the document and the checked type, which
/// is a region no assertion about the typed objects can enter. That is the same shape as the settling
/// scan count, the flag whose refusal no caller could reach, and three others recorded in
/// <c>BindingDocument</c>.</para>
/// </summary>
public class QuiescenceWireTests
{
    private const int MirrorBase = 4000;
    private const int ProgramBase = 3000;

    private const string ClauseId = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    private static readonly string Submission = $$"""
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
        "settlingSignals": ["{{TrivialBlock.CountTag}}"],
        "settlingUnchangedForScans": 3,
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

    /// <summary>The binding, with the quiescence declaration as a PARAMETER — including "absent".</summary>
    private static string Binding(string quiescenceField) => $$"""
    {
      "blockName": "FC_HarnessCopyLayer",
      "blockNumber": 900,
      "baseByte": {{MirrorBase}},
      "slots": [{
        "slotId": "S0",
        "startCondition": "{{TrivialBlock.StartTag}}",
        {{quiescenceField}}
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

    private static LoopRequest Compose(string quiescenceField) =>
        LoopCli.Compose(
            SubmissionDocument.Read(DerivedFixture.WithDerivation(Submission, DerivedFixture.ArtifactPath, Submission)),
            BindingDocument.Read(Binding(quiescenceField)),
            TrivialBlock.Generate(ProgramBase, blockNumber: 901),

            // A DERIVED fixture, so gate 0c attributes the map/deployment/edges instead of refusing them.
            // The artifact is the submission text itself here, because the BINDING varies per test while
            // the hash recorded in the records must match whatever the reader returns.
            readFile: DerivedFixture.ReaderFor(Submission));

    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_DECLARED_NUMBER_REACHES_THE_INERT_DECLARATION_THE_WAVE_IS_BUILT_FROM()
    {
        // 🔴 *** BEFORE THE WIRE FIELD EXISTED THIS WAS 1 FOR EVERY SLOT IN EVERY SUBMISSION, and the
        // binding had no way to make it anything else. It is read back off the plan rather than off the
        // request, because the plan is what the wave's inert phase is actually handed.
        foreach (var scans in new[] { 1, 3, 42 })
        {
            var binding = Assert.Single(Compose($"\"quiescenceScans\": {scans},").Bindings);

            Assert.Equal(scans, binding.QuiescenceScans);
            Assert.Equal(scans, InertRestPlan.For(binding, RegisterWordOrder.HighWordFirst).Require().QuiescenceScans);
        }
    }

    [Fact]
    public void AN_OMITTED_DECLARATION_IS_THE_FLOOR_OF_ONE_rather_than_a_blank()
    {
        // The converse, and it is what stops the test above from passing against a field hard-coded to
        // something non-zero. One is the floor for a stated reason: two reads inside one scan cannot tell a
        // settled value from a changing one.
        var binding = Assert.Single(Compose(string.Empty).Bindings);

        Assert.Equal(1, binding.QuiescenceScans);
        Assert.Equal(1, InertRestPlan.For(binding, RegisterWordOrder.HighWordFirst).Require().QuiescenceScans);
    }

    [Fact]
    public void A_WINDOW_THE_POLL_BUDGET_CANNOT_OBSERVE_STOPS_THE_RUN_BEFORE_ANYTHING_IS_DEPLOYED()
    {
        // ⚠️ The wait is declared in SCANS and paid in POLLS, and the worst case is one scan observed per
        // poll. Left to run, this reports ScanCounterStalled — "The PLC may be stopped" — for a number
        // written in a binding document, which is a refusal wearing a diagnosis.
        var result = LoopRun.Execute(
            Compose($"\"quiescenceScans\": {InertPhase.PollBudget + 1},"),
            new SimulatedGateway(MirrorGeometry.ForCpu1214C(256, MirrorBase)));

        Assert.Equal(LoopOutcome.RestNotDeclared, result.Outcome);
        Assert.Empty(result.Packages);
        Assert.Contains($"{InertPhase.PollBudget + 1}", result.Detail, StringComparison.Ordinal);
        Assert.Contains($"{InertPhase.PollBudget} poll(s)", result.Detail, StringComparison.Ordinal);
        Assert.Contains("nothing was deployed", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_WAIT_IS_REPORTED_ON_THE_RUN_THAT_SUCCEEDS_because_a_silent_default_looked_like_a_stated_one()
    {
        var result = LoopRun.Execute(
            Compose("\"quiescenceScans\": 4,"),
            new SimulatedGateway(MirrorGeometry.ForCpu1214C(256, MirrorBase)));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.Contains("QUIESCENCE: 4 scan(s)", result.InertRest!.Summary(), StringComparison.Ordinal);
    }

    [Fact]
    public void THE_UNAFFECTED_CASE_a_declared_wait_within_the_budget_still_runs_the_wave()
    {
        // Asserted deliberately: a gate that refuses working submissions on the day it lands is removed
        // within a week, by somebody who is right to. The boundary VALUE is the one worth pinning — a gate
        // that fires one short gets widened by hand, and the widening is never as careful as the original.
        var plan = InertRestPlan.For(
            Assert.Single(Compose($"\"quiescenceScans\": {InertPhase.PollBudget},").Bindings),
            RegisterWordOrder.HighWordFirst);

        Assert.True(plan.Planned);
        Assert.Empty(plan.Refusals);
    }
}
