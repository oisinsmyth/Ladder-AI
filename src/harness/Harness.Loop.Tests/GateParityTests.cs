using Harness.Gate;
using Harness.Map;
using Harness.Results;
using Harness.Run;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE INVARIANT: <c>harness-run</c> AND <c>harness-gate</c> MUST AGREE GATE FOR GATE ON THE SAME
/// SUBMISSION.</b>
///
/// <para>*** THE LOOP'S GATE WAS NOT THE GATE, AND IT WAS WRONG IN BOTH DIRECTIONS. *** Measured
/// 2026-08-14 on the deliverable wave set. <c>LoopCli.Compose</c> re-derived four gate inputs, passed
/// <c>null</c> for four more — <c>signalStorage</c>, <c>tagMapReach</c>, <c>compressionInputs</c>, and the
/// explicit-null <c>conflictEdges</c> flag — and hard-coded the last two, so <b>gates 8, 8s, 8c, 10b and
/// 11 went NOT CHECKED under the loop while the standalone gate RAN them, and gate 0b passed
/// unconditionally</b> because the loop asserted, on the document's behalf, that no unknown field could
/// arrive. That is true of a caller composing typed objects and false of one PARSING TWO DOCUMENTS, which
/// is exactly what the CLI does.</para>
///
/// <para><b>The dangerous direction is the second one.</b> A submission could pass the loop's gate and
/// fail the real one — and the loop is the path that spends rig time, so the weaker check is the one
/// guarding the expensive door.</para>
///
/// <para><b>These tests are the deliverable, not the two patches.</b> The fix is structural — one
/// <see cref="GateInputs"/> derivation both callers use — and this is what stops it drifting apart again.
/// Every mutation below is asserted in BOTH directions: the gate that now runs is shown to REFUSE when it
/// should, and the unaffected submission is shown to still pass. <i>A change that makes everything NOT
/// CHECKED passes every test that only looks for NOT CHECKED.</i></para>
/// </summary>
public class GateParityTests
{
    // A real clause and a real assertion text, so the ID RECOMPUTES (gate 3g). A hand-written six-hex
    // literal cannot be made to pass that: finding a text that hashes to chosen digits is a preimage
    // problem.
    private const string ClauseId = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    /// <summary>
    /// A submission every mechanical gate can RUN and pass. <b>The did-not-run control.</b>
    ///
    /// <para>A fixture that refuses everything would satisfy every assertion below while proving nothing:
    /// two CLIs that both report NOT CHECKED for everything agree perfectly. So the baseline is
    /// ADMISSIBLE-SUBJECT-TO-JUDGEMENT, asserted as such, and each mutation moves exactly one gate.</para>
    /// </summary>
    internal static string SubmissionJson() => Submission();

    internal static string BindingJson() => Binding();

    private static string Submission(
        string? extraTopLevelField = null,
        string conflictEdges = "\"conflictEdges\": [],",
        string storage = "\"harnessOnly\": [\"Count\", \"Done\"],",
        string? blockCompression = null,
        string? tagMapPath = null,
        string deployment = "\"deployment\": { \"noS7Transport\": true },",
        int runtimeCompression = 1,
        int settlingScans = 3) => $$"""
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": {{runtimeCompression}},
      "slotsInWaveSet": 1,
      "resultRegistersPerSlot": 2,
      {{extraTopLevelField ?? string.Empty}}
      {{conflictEdges}}
      {{blockCompression ?? string.Empty}}
      {{(tagMapPath is null ? string.Empty : $"\"tagMapPath\": \"{tagMapPath}\",")}}
      {{deployment}}
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
        {{storage}}
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
        "settlingUnchangedForScans": {{settlingScans}},
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

    private static string Binding(string? extraField = null) => $$"""
    {
      {{extraField ?? string.Empty}}
      "blockName": "FC_HarnessCopyLayer",
      "blockNumber": 9001,
      "baseByte": 1000,
      "slots": [{
        "slotId": "S0",
        "startCondition": "Demo_Start",
        "vectorTargets": [{ "tag": "Demo_Step", "specName": "Demo_Step", "type": "Int" }],
        "resultSources": [
          { "tag": "Demo_Count", "specName": "Count", "type": "Int",
            "inertRest": { "value": "0", "basis": "the demo ramp holds its count at 0 while the start command is off" } },
          { "tag": "Demo_Done", "specName": "Done", "type": "Bool",
            "inertRest": { "value": "false", "basis": "the demo ramp holds its done flag off while the start command is off" } }
        ]
      }]
    }
    """;

    /// <summary>One block under test, so the stamp is over something and the two sides see the same set.</summary>
    private static readonly HarnessObject[] Program =
    {
        new("FC_DemoRamp", HarnessObjectKind.Block, "BLOCK FC FC_DemoRamp\n  NUMBER 901\n"),
    };

    /// <summary>
    /// Run BOTH gates over one pair of documents and return them side by side.
    ///
    /// <para>The loop's half is <c>LoopRun.Generate(stopWhenInadmissible: false)</c>, which is the same
    /// code <c>LoopRun.Execute</c> runs — so this compares the gate that actually guards a deployment, not
    /// a re-implementation of it.</para>
    /// </summary>
    private static (SubmissionReport Standalone, SubmissionReport Loop) Both(
        string submissionJson, string bindingJson, Func<string, string>? readFile = null)
    {
        var submission = SubmissionDocument.Read(submissionJson);
        var binding = BindingDocument.Read(bindingJson);

        var standalone = GateCli.Evaluate(submission, readFile, binding);

        var generation = LoopRun.Generate(
            LoopCli.Compose(submission, binding, Program, readFile), stopWhenInadmissible: false);

        Assert.NotNull(generation.Gate);
        return (standalone, generation.Gate!);
    }

    /// <summary>Gate label to (status, passed) — the comparison the invariant is stated in.</summary>
    private static Dictionary<string, string> Verdicts(SubmissionReport report) =>
        report.Gates.ToDictionary(g => g.Gate, g => $"{g.Status}/{(g.Passed ? "passed" : "refused")}", StringComparer.Ordinal);

    /// <summary>
    /// The invariant itself: same gates, same verdicts. <b>Reported as the per-gate difference</b>, because
    /// a count agrees with a wrong derivation as readily as with a right one.
    /// </summary>
    private static void AssertAgree(SubmissionReport standalone, SubmissionReport loop)
    {
        var a = Verdicts(standalone);
        var b = Verdicts(loop);

        // Denominator first: a comparison of two empty sets is not agreement.
        Assert.True(a.Count > 20, $"the standalone gate ran only {a.Count} gate(s); this comparison would be near-vacuous.");

        var differences = a.Keys.Union(b.Keys, StringComparer.Ordinal)
            .Where(k => !string.Equals(a.GetValueOrDefault(k, "<absent>"), b.GetValueOrDefault(k, "<absent>"), StringComparison.Ordinal))
            .Select(k => $"{k}: harness-gate={a.GetValueOrDefault(k, "<absent>")} harness-run={b.GetValueOrDefault(k, "<absent>")}")
            .ToArray();

        Assert.True(differences.Length == 0,
            "*** THE LOOP'S GATE IS NOT THE GATE. *** A submission can pass one and fail the other, and the loop is the path "
            + "that spends rig time:" + Environment.NewLine + string.Join(Environment.NewLine, differences));
    }

    // ---------------------------------------------------------------------------------------------
    // THE MULTI-SUBJECT SHAPE — parity PROVEN for it, not inherited from the single-subject fixture
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE SUBJECT GATE AGREES ON BOTH SIDES — INCLUDING WHEN IT REFUSES.</b>
    ///
    /// <para><b>This is the test that made a Gate-only fix impossible</b>, and it is here so the reason
    /// stays checkable rather than remembered. A subject resolution performed in <c>GateCli</c> alone would
    /// have appeared as <c>&lt;absent&gt;</c> on the loop's side of <see cref="AssertAgree"/> — the loop's
    /// gate weaker than the standalone one, in exactly the place the standalone one is consulted first, on
    /// the path that spends rig time. It lives in <c>SubmissionGate</c>, which both callers run.</para>
    ///
    /// <para><b>Asserted in both directions.</b> The refusing case alone would be satisfied by two gates
    /// that both broke; the resolving case alone would be satisfied by two that both stopped checking.</para>
    /// </summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("\"subject\": \"TANK\",", true)]
    public void THE_TWO_SUBJECT_SHAPE_AGREES_GATE_FOR_GATE_ON_BOTH_SIDES(string? vectorSubject, bool expectResolved)
    {
        var (standalone, loop) = Both(TwoSubjectSubmission(vectorSubject), Binding());

        AssertAgree(standalone, loop);

        // And the gate that moved is NAMED, on the loop's side — "they agree" is also true when both are
        // absent, which is the failure mode this whole file exists for.
        var gate = Assert.Single(loop.Gates, g => g.Gate == "3j subject resolution");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.Equal(expectResolved, gate.Passed);

        if (!expectResolved)
        {
            Assert.Contains("PUMP", gate.Detail, StringComparison.Ordinal);
            Assert.Contains("TANK", gate.Detail, StringComparison.Ordinal);
        }
    }

    private const string SharedClause = "SHARED-7";
    private const string PumpText = "WHEN the interlock is broken THEN the drive command is dropped";
    private const string TankText = "WHEN the interlock is broken THEN the fill valve is closed";
    private static readonly string PumpId = AssertionId.Compute(SharedClause, PumpText);
    private static readonly string TankId = AssertionId.Compute(SharedClause, TankText);

    /// <summary>
    /// The single-subject fixture with its enumeration replaced by TWO that share a clause. Everything else
    /// is byte-identical, so any gate that moves has moved for the subject and nothing else.
    /// </summary>
    private static string TwoSubjectSubmission(string? vectorSubject)
    {
        var json = Submission();

        // *** SPLICED BY INDEX, AND EVERY ANCHOR IS ASSERTED. *** A `.Replace` whose needle does not match
        // returns the string unchanged, so a fixture built that way degrades SILENTLY into the
        // single-subject one — and a two-subject test that quietly tests one subject is precisely the
        // examined-nothing green this file exists to catch. It caught itself here, on the first run.
        var start = json.IndexOf("\"enumeration\":", StringComparison.Ordinal);
        var end = json.IndexOf("\"map\":", StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start, "the fixture's enumeration block moved; this splice would have produced the SINGLE-subject submission.");

        var twoSubjects = $$"""
            "enumerations": [
                { "subject": "PUMP", "clauses": ["{{SharedClause}}"], "assertions": ["{{PumpId}}"],
                  "forms": { "{{PumpId}}": "When" }, "enumerator": "agent-c",
                  "normalisedTexts": { "{{PumpId}}": "{{PumpText}}" },
                  "requiredObservations": { "{{PumpId}}": ["Count"] }, "bounds": { "ramp_limit": "10" } },
                { "subject": "TANK", "clauses": ["{{SharedClause}}"], "assertions": ["{{TankId}}"],
                  "forms": { "{{TankId}}": "When" }, "enumerator": "agent-c",
                  "normalisedTexts": { "{{TankId}}": "{{TankText}}" },
                  "requiredObservations": { "{{TankId}}": ["Count"] }, "bounds": { "ramp_limit": "10" } }
              ],

            """;

        var spliced = json[..start] + twoSubjects + json[end..];

        var citation = $"\"clause\": \"{ClauseId}\",";
        var assertion = $"\"assertion\": \"{AssertionIdValue}\",";
        Assert.Contains(citation, spliced, StringComparison.Ordinal);
        Assert.Contains(assertion, spliced, StringComparison.Ordinal);

        return spliced
            .Replace(citation, $"\"clause\": \"{SharedClause}\",", StringComparison.Ordinal)
            .Replace(assertion, $"\"assertion\": \"{TankId}\", {vectorSubject ?? string.Empty}", StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // THE COMPARATOR'S OWN CONTROLS — because it is otherwise unfalsifiable in place
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("signal storage")]
    [InlineData("tag map reach")]
    [InlineData("unknown fields")]
    [InlineData("compression inputs")]
    public void THE_COMPARISON_DETECTS_A_DIVERGENCE_and_names_the_gate_that_moved(string dropped)
    {
        // 🔴 *** FOUND BY MUTATION, IN THIS FILE'S OWN CODE. *** Replacing `Verdicts(loop)` with
        // `Verdicts(standalone)` inside AssertAgree — so it compares one side with ITSELF — left all 95
        // tests GREEN. Every mutation test above also asserts its gate directly, and those assertions are
        // all on the loop's side, so the comparison could stop comparing and nothing would notice. It was
        // correct, wired in, and unfalsifiable in place.
        //
        // So it is exercised DIRECTLY, against a request the CLI can no longer produce: each of these
        // drops one input that Compose now carries, which is exactly the defect this whole change fixed.
        // The converse — a correct pair produces NO finding — is the baseline test below, and neither
        // subsumes the other.
        // *** comp = 2, AND THAT IS NOT DECORATION. *** At comp=1 gate 10b returns "nothing is compressed
        // at run time, so X-D's ceilings cannot bind" WITHOUT READING the block inputs at all — so
        // dropping CompressionInputs changed no verdict and this control passed while measuring nothing.
        // Found by running it. A compressed wave is the only condition under which that input is load-bearing.
        var submission = SubmissionDocument.Read(Submission(
            blockCompression: "\"blockCompression\": { \"plantMs\": 60000, \"budgetMs\": 6000, \"negligibleFraction\": 0.05, \"presets\": [{ \"name\": \"dwell\", \"presetMs\": 500, \"source\": \"Data\" }] },",
            tagMapPath: "map.json",
            deployment: "\"deployment\": { \"importStamp\": \"loop-test\", \"s7Objects\": [] },",
            runtimeCompression: 2));

        var binding = BindingDocument.Read(Binding());
        Func<string, string> readFile = _ => """{ "tags": [ { "name": "Probe", "area": "DB", "db": 42, "byte": 0, "type": "Int" } ] }""";

        var standalone = GateCli.Evaluate(submission, readFile, binding);
        var whole = LoopCli.Compose(submission, binding, Program, readFile);

        var crippled = dropped switch
        {
            "signal storage" => whole with { SignalStorage = null },
            "tag map reach" => whole with { TagMapReach = null },
            "unknown fields" => whole with { UnknownFields = null },
            "compression inputs" => whole with { CompressionInputs = null },
            _ => throw new ArgumentOutOfRangeException(nameof(dropped)),
        };

        var loop = LoopRun.Generate(crippled, stopWhenInadmissible: false).Gate;
        Assert.NotNull(loop);

        var caught = Record.Exception(() => AssertAgree(standalone, loop!));

        Assert.True(caught is not null,
            $"AssertAgree did not notice that the loop's request had lost its {dropped}. *** A COMPARATOR THAT CANNOT FAIL IS "
            + "DOCUMENTATION WEARING A CHECK'S CLOTHES. ***");

        Assert.Contains("THE LOOP'S GATE IS NOT THE GATE", caught!.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // THE UNAFFECTED CASE — asserted as deliberately as any refusal
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_FULLY_DECLARED_SUBMISSION_IS_ADMISSIBLE_ON_BOTH_and_that_is_the_did_not_run_control()
    {
        var (standalone, loop) = Both(Submission(), Binding());

        // *** IF THIS FIXTURE WERE REFUSED, EVERY OTHER TEST HERE WOULD PASS WHILE MEASURING NOTHING. ***
        Assert.Equal(SubmissionVerdict.AdmissibleSubjectToJudgement, standalone.Verdict);
        Assert.Equal(SubmissionVerdict.AdmissibleSubjectToJudgement, loop.Verdict);

        AssertAgree(standalone, loop);
    }

    [Fact]
    public void GATES_8_8s_AND_8c_RUN_UNDER_THE_LOOP_when_the_2_7_join_is_declared()
    {
        // These are the three the loop could never run: Compose never passed SignalStorage, so a
        // submission declaring `map.harnessOnly` got NOT CHECKED from the loop and a CHECKED pass from
        // harness-gate. Named individually rather than left to the set comparison, because "they agree"
        // is also true when both are NOT CHECKED.
        var (_, loop) = Both(Submission(), Binding());

        foreach (var gate in new[] { "8s signal storage join (2.7)", "8 blacklist", "8c multi-writer provenance (X-G)" })
        {
            var result = Assert.Single(loop.Gates, g => g.Gate == gate);
            Assert.Equal(GateStatus.Checked, result.Status);
            Assert.True(result.Passed, $"{gate}: {result.Detail}");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // MUTATIONS — each moves ONE gate, and it must move on BOTH sides
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void REMOVING_THE_2_7_JOIN_TAKES_GATES_8_8s_AND_8c_TO_NOT_CHECKED_ON_BOTH()
    {
        // The mutation that proves the wiring is live: with the join gone the loop must LOSE the three
        // gates it just ran, exactly as the standalone tool does. A fix that simply reported NOT CHECKED
        // everywhere would pass the previous test's negation and fail this one's counterpart above.
        var (standalone, loop) = Both(Submission(storage: string.Empty), Binding());

        Assert.Equal(SubmissionVerdict.NotAdmissible, standalone.Verdict);
        Assert.Equal(SubmissionVerdict.NotAdmissible, loop.Verdict);

        foreach (var gate in new[] { "8s signal storage join (2.7)", "8 blacklist", "8c multi-writer provenance (X-G)" })
            Assert.Equal(GateStatus.NotChecked, Assert.Single(loop.Gates, g => g.Gate == gate).Status);

        AssertAgree(standalone, loop);
    }

    [Fact]
    public void A_SIGNAL_DECLARED_BOTH_WAYS_IS_REFUSED_ON_BOTH_rather_than_merely_unchecked()
    {
        // 8s has three outcomes, not two, and only this one is a REFUSAL: a signal cannot both occupy
        // storage and occupy none. Without it, "the gate runs now" would be evidenced only by passes.
        var (standalone, loop) = Both(
            Submission(storage: "\"harnessOnly\": [\"Count\", \"Done\"], \"storage\": { \"Count\": { \"owner\": \"DemoUnit\", \"path\": \"Demo_Count\" } },"),
            Binding());

        var gate = Assert.Single(loop.Gates, g => g.Gate == "8s signal storage join (2.7)");
        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("declared in BOTH", gate.Detail, StringComparison.Ordinal);

        AssertAgree(standalone, loop);
    }

    [Fact]
    public void AN_UNKNOWN_FIELD_IN_THE_SUBMISSION_REFUSES_GATE_0b_ON_BOTH()
    {
        // 🔴 The loop passed `unknownFields: Array.Empty<string>()` unconditionally, so gate 0b was a
        // guaranteed pass there — the contract/code divergence it exists to catch was invisible to the
        // only path that deploys.
        var (standalone, loop) = Both(Submission(extraTopLevelField: "\"modeSource\": \"contract-2.8\","), Binding());

        var gate = Assert.Single(loop.Gates, g => g.Gate == "0b unknown fields");
        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("modeSource", gate.Detail, StringComparison.Ordinal);

        AssertAgree(standalone, loop);
    }

    [Fact]
    public void AN_UNKNOWN_FIELD_IN_THE_BINDING_REFUSES_GATE_0b_ON_BOTH()
    {
        // The binding is the document that carries the INSTRUMENTATION, and a misspelt `specName` there
        // reproduces the run where 1 of 17 signals resolved. Both documents feed this gate or neither does.
        var (standalone, loop) = Both(Submission(), Binding(extraField: "\"tagPrefixx\": \"HX_\","));

        var gate = Assert.Single(loop.Gates, g => g.Gate == "0b unknown fields");
        Assert.False(gate.Passed);
        Assert.Contains("tagPrefixx", gate.Detail, StringComparison.Ordinal);

        AssertAgree(standalone, loop);
    }

    [Fact]
    public void AN_ANNOTATION_IS_EXCLUDED_BY_NAME_ON_BOTH_and_the_count_is_reported()
    {
        // The converse of the two above, and it is what stops gate 0b decaying into a gate that refuses
        // every ordinary submission: an `_`-prefixed key is excluded, COUNTED, and never a refusal.
        var (standalone, loop) = Both(Submission(extraTopLevelField: "\"_why\": \"a deliberate note\","), Binding());

        var gate = Assert.Single(loop.Gates, g => g.Gate == "0b unknown fields");
        Assert.True(gate.Passed, gate.Detail);
        Assert.Contains("1 annotation(s)", gate.Detail, StringComparison.Ordinal);

        AssertAgree(standalone, loop);
    }

    [Fact]
    public void AN_EXPLICIT_NULL_conflictEdges_REFUSES_GATE_8_ON_BOTH()
    {
        // Omitted, `[]` and `null` are three different things and only two are legal. The loop hard-coded
        // `conflictEdgesExplicitlyNull: false`, so the third was UNREACHABLE from the deploying path.
        var (standalone, loop) = Both(Submission(conflictEdges: "\"conflictEdges\": null,"), Binding());

        var gate = Assert.Single(loop.Gates, g => g.Gate == "8 blacklist");
        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("present and NULL", gate.Detail, StringComparison.Ordinal);

        AssertAgree(standalone, loop);
    }

    [Fact]
    public void THE_BLOCK_LEVEL_COMPRESSION_CEILINGS_REACH_GATE_10b_ON_BOTH()
    {
        // Compose passed `CompressionInputs: null`, so X-D's timer/model/ratio ceilings were computed from
        // nothing under the loop even for a submission that answered them.
        // At comp=1 this gate short-circuits without reading the inputs, so the wave is compressed here:
        // otherwise "the ceilings reach the gate" would be true of a gate that never looked at them.
        var (standalone, loop) = Both(
            Submission(blockCompression: "\"blockCompression\": { \"plantMs\": 60000, \"budgetMs\": 6000, \"negligibleFraction\": 0.05, \"presets\": [{ \"name\": \"dwell\", \"presetMs\": 500, \"source\": \"Data\" }] },", runtimeCompression: 2),
            Binding());

        // *** AND IT REFUSES, WHICH IS THE CORRECT OUTCOME AND IS NOT SOFTENED. *** With the block inputs
        // actually read, X-D's ceilings bind: comp_min = 10 (60 000 ms of plant behaviour into a 6 000 ms
        // budget) against a comp_max of 2.32 from the 500 ms DATA preset. The loop could not previously
        // reach that conclusion at all.
        var gate = Assert.Single(loop.Gates, g => g.Gate.StartsWith("10b", StringComparison.Ordinal));
        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("comp_max", gate.Detail, StringComparison.Ordinal);

        // And the SAME submission with the block inputs removed must NOT reach the same verdict — the
        // control that stops this test passing against a gate that ignores them.
        var (_, without) = Both(Submission(runtimeCompression: 2), Binding());
        Assert.NotEqual(Verdicts(loop)[gate.Gate], Verdicts(without)[gate.Gate]);

        AssertAgree(standalone, loop);
    }

    [Fact]
    public void THE_TAG_MAP_REACHES_GATE_11_ON_BOTH()
    {
        // `TagMapReach: null` under the loop meant gate 11's SET-DIFFERENCE — the point of the gate — was
        // never made there. Here the map reaches DB42 and `s7Objects: []` claims nothing is reachable, so
        // the difference is non-empty and the gate must REFUSE. `noS7Transport` would short-circuit before
        // the comparison, which is why this variant does not use it.
        const string tagMap = """
        { "tags": [ { "name": "Probe", "area": "DB", "db": 42, "byte": 0, "type": "Int" } ] }
        """;

        const string emptyS7 = "\"deployment\": { \"importStamp\": \"loop-test\", \"s7Objects\": [] },";

        var (standalone, loop) = Both(
            Submission(tagMapPath: "map.json", deployment: emptyS7),
            Binding(),
            path => path == "map.json" ? tagMap : throw new FileNotFoundException(path));

        AssertAgree(standalone, loop);

        var gate = Assert.Single(loop.Gates, g => g.Gate.StartsWith("11 memory layout", StringComparison.Ordinal));
        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("DB42", gate.Detail.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);

        // The CONVERSE, without which the assertion above would also hold for a gate that refuses
        // everything: the same declaration with NO tag map is NOT CHECKED rather than refused, and the
        // two sides still agree on that.
        var (withoutStandalone, withoutLoop) = Both(Submission(deployment: emptyS7), Binding());
        AssertAgree(withoutStandalone, withoutLoop);

        Assert.Equal(GateStatus.NotChecked,
            Assert.Single(withoutLoop.Gates, g => g.Gate.StartsWith("11 memory layout", StringComparison.Ordinal)).Status);
    }

    // ---------------------------------------------------------------------------------------------
    // THE DELIVERABLE SET — a corpus check, because the input we would have to invent is on disk
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_DELIVERABLE_SETS_GATE_INPUTS_ARE_THE_SAME_ON_BOTH_SIDES()
    {
        // *** THE SUBMISSION THAT WILL ACTUALLY BE RUN. *** A fixture agrees with itself; this is the
        // artifact, with its 116 annotations, its partial 2.7 join and its absent deployment declaration.
        //
        // This half compares the INPUTS rather than the gate verdicts, and it runs today whatever else is
        // true of the pair — see the sibling test for why the verdict comparison currently cannot.
        var (submission, binding) = Deliverable();

        var inputs = GateCli.InputsOf(submission, binding, File.ReadAllText);
        var request = LoopCli.Compose(submission, binding, Program, File.ReadAllText);

        Assert.Equal(inputs.UnknownFields, request.UnknownFields);
        Assert.Equal(inputs.AnnotationFields, request.AnnotationFields);
        Assert.Equal(inputs.ConflictEdgesExplicitlyNull, request.ConflictEdgesExplicitlyNull);
        Assert.Equal(inputs.RuntimeCompression, request.Compression.Factor);
        Assert.Equal(inputs.Vectors.Count, request.Vectors.Count);
        Assert.Equal(inputs.Storage is null, request.SignalStorage is null);
        Assert.Equal(inputs.Deployment is null, request.Deployment is null);
        Assert.Equal(inputs.TagMapReach is null, request.TagMapReach is null);
        Assert.Equal(inputs.CompressionInputs is null, request.CompressionInputs is null);
        Assert.Equal(inputs.Fidelity is null, request.Fidelity is null);
        Assert.Equal(inputs.Conflicts is null, request.ComputedConflicts is null);

        // *** AND IT IS NOT VACUOUS. *** These are the values the loop used to discard: the real documents
        // carry 27 vectors, a non-empty annotation set and a declared 2.7 join, so every equality above is
        // comparing something. Without this the test would pass just as well against two nulls.
        Assert.Equal(27, request.Vectors.Count);
        Assert.NotEmpty(request.AnnotationFields!);
        Assert.NotNull(request.SignalStorage);
    }

    [Fact]
    public void THE_27_VECTOR_DELIVERABLE_SET_GETS_THE_SAME_ANSWER_FROM_BOTH_CLIS()
    {
        var (submission, binding) = Deliverable();

        var standalone = GateCli.Evaluate(submission, File.ReadAllText, binding);
        var generation = LoopRun.Generate(
            LoopCli.Compose(submission, binding, Program, File.ReadAllText), stopWhenInadmissible: false);

        // The standalone side must have examined something, whatever the loop did.
        Assert.True(standalone.Gates.Count > 20, $"harness-gate ran only {standalone.Gates.Count} gate(s) over the deliverable set.");

        if (generation.Gate is not null)
        {
            AssertAgree(standalone, generation.Gate);
            return;
        }

        // 🔴 *** THE LOOP CANNOT REACH ITS GATE ON THIS PAIR, AND THERE IS EXACTLY ONE PERMITTED REASON. ***
        // The 27 vectors name six slots (SLOT-HBA-RAISE, -CLEAR, -RESET, -STARTUP, -LATCH, -PAIR) and the
        // committed coordinator binding declares one, SLOT-HBA-ALL — its own `_unbound.slotPartition`
        // records the partition as UNDETERMINED by the prose. The slot join stops the run above the gate,
        // which is correct and is somebody else's artifact to close.
        //
        // *** THIS IS NOT A SKIP. *** Any other stop fails here by name, the standalone side is still
        // required to have examined 20+ gates above, and the moment the binding names the vectors' slots
        // the comparison turns itself on. Written so that CLOSING the gap does not turn this test red — a
        // test that punishes its own fix is how a defect acquires tenure.
        Assert.Equal(LoopOutcome.NotBound, generation.Stopped);
        Assert.Contains("SLOT-HBA-ALL", generation.Detail, StringComparison.Ordinal);
    }

    private static (SubmissionDocument Submission, BindingDocument Binding) Deliverable()
    {
        var dir = Corpus();

        return (SubmissionDocument.Read(File.ReadAllText(Path.Combine(dir, "conformance-vectors-b.json"))),
                BindingDocument.Read(File.ReadAllText(Path.Combine(dir, "harness-binding.json"))));
    }

    private static string Corpus()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "gen", "test-project001", "hopper-blockage-alarm");
            if (File.Exists(Path.Combine(candidate, "conformance-vectors-b.json")))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "the deliverable wave set was not found by walking up from " + AppContext.BaseDirectory
            + ". *** THIS IS A FAILURE, NOT A SKIP: *** the parity invariant is worth most against the real submission, and a "
            + "test that quietly stops running against it is the corpus check nobody notices has gone.");
    }
}
