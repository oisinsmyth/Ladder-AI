using Harness.Gate;
using Harness.Loop;
using Harness.Map;
using Harness.Results;
using Harness.Run;
using Harness.Wire;

namespace Harness.Verify.Tests;

/// <summary>
/// A submission, a binding and a program that <see cref="LoopCli.Compose"/> and
/// <see cref="LoopRun.Generate"/> accept, so the tests below compose a REAL map and a REAL build stamp
/// through the same calls the tool makes.
///
/// <para><b>The baseline is deliberately ADMISSIBLE and deliberately composes.</b> A fixture that failed
/// to compose would satisfy every "the tool refused" assertion while measuring nothing — two runs that
/// both stop before the device agree perfectly.</para>
/// </summary>
internal static class Fixtures
{
    private const string ClauseId = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    internal static string SubmissionJson => $$"""
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 1,
      "resultRegistersPerSlot": 2,
      "conflictEdges": [],
      "deployment": { "noS7Transport": true },
      "model": {
        "id": "M_Ramp",
        "represents": ["ramp-to-limit"],
        "doesNotRepresent": ["overshoot"],
        "validatedAgainstPlantData": true,
        "declaredBy": "agent-m",
        "compStable": 10.0
      },
      "enumeration": {
        "clauses": ["{{ClauseId}}"],
        "assertions": ["{{AssertionIdValue}}"],
        "forms": { "{{AssertionIdValue}}": "When" },
        "enumerator": "agent-c",
        "normalisedTexts": { "{{AssertionIdValue}}": "{{AssertionText}}" },
        "requiredObservations": { "{{AssertionIdValue}}": ["Count"] },
        "bounds": { "ramp_limit": "10" }
      },
      "map": {
        "harnessOnly": ["Count", "Done"],
        "providedFor": { "Count": ["Sampled"] }
      },
      "vectors": [{
        "id": "V-1",
        "slot": "S0",
        "index": 0,
        "author": "agent-b",
        "clause": "{{ClauseId}}",
        "assertion": "{{AssertionIdValue}}",
        "inputs": { "Demo_Step": "5" },
        "startBool": "Demo_Start",
        "assertionForm": "When",
        "expectations": [
          { "signal": "Count", "nature": "PersistentState", "mode": "Sampled", "windowScans": 20, "expected": "10" }
        ],
        "settlingCondition": "count unchanged across 3 consecutive scans",
        "settlingSignals": ["Count", "Done"],
        "settlingUnchangedForScans": 3,
        "maxDurationScans": 20,
        "completionSignal": "Done",
        "completionValue": 1,
        "compressionFactor": 1,
        "assertedBehaviours": ["ramp-to-limit"],
        "blacklist": [],
        "kills": "a ramp that overshoots the limit by one step",
        "boundsUsed": { "ramp_limit": "10" }
      }]
    }
    """;

    internal static string BindingJson => """
    {
      "blockName": "FC_HarnessCopyLayer",
      "blockNumber": 9001,
      "baseByte": 1000,
      "declaredRegisters": 576,
      "slots": [{
        "slotId": "S0",
        "startCondition": "Demo_Start",
        "vectorTargets": [{ "tag": "Demo_Step", "specName": "Demo_Step", "type": "Int" }],
        "resultSources": [
          { "tag": "Demo_Count", "specName": "Count", "type": "Int" },
          { "tag": "Demo_Done", "specName": "Done", "type": "Bool" }
        ]
      }]
    }
    """;

    internal const string ProgramIr = "BLOCK FC FC_DemoRamp\n  NUMBER 901\n";

    internal const string SubmissionPath = "submission.json";
    internal const string BindingPath = "binding.json";
    internal const string ProgramPath = "FC_DemoRamp.ir";

    /// <summary>The tool's own file reader, in memory.</summary>
    internal static Func<string, string> Files => path => path switch
    {
        SubmissionPath => SubmissionJson,
        BindingPath => BindingJson,
        ProgramPath => ProgramIr,
        _ => throw new FileNotFoundException($"the fixture has no file '{path}'.", path),
    };

    internal static IReadOnlyList<string> Expand(string path) => new[] { path };

    /// <summary>
    /// The map and stamp the tool will compute for the fixture above — derived through the SAME calls, so
    /// a test's expectation cannot drift from what the tool does.
    /// </summary>
    internal static (RegisterMap Map, BuildStamp Stamp) Composed()
    {
        var request = LoopCli.Compose(
            SubmissionDocument.Read(SubmissionJson),
            BindingDocument.Read(BindingJson),
            new[] { new HarnessObject("FC_DemoRamp", HarnessObjectKind.Block, ProgramIr) });

        var generation = LoopRun.Generate(request, stopWhenInadmissible: false);

        Assert.True(generation.Generated, $"the fixture must compose; it stopped at {generation.Stopped}: {generation.Detail}");
        return (generation.Map!, generation.Stamp);
    }

    internal static VerifyOptions Options(string allowlistPath, string address = "10.0.0.1") =>
        new(SubmissionPath, BindingPath, new[] { ProgramPath }, address, 503, 1, allowlistPath);
}
