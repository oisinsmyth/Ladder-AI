using Harness.Gate;
using Harness.Map;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>GATE 5b — M-19's SECOND LIMB. "IT TAKES THE NAME, NOT THE FACT."</b>
///
/// <para>A hand-authored latch is admitted on PROVENANCE: <i>this signal is latched by block X</i>.
/// Nothing joined X to what was loaded, so <b>a submission could be ADMISSIBLE while naming latching
/// blocks that are not in the deployment</b> — and that was the deliverable's own state when the hole was
/// found. Every test here is about that join: it must fire, it must be scoped to the half that is
/// actually taken on trust, and it must not pass by examining nothing.</para>
/// </summary>
public class LatchDeploymentGateTests
{
    private const string GateName = "5b latch block in the deployment";
    private const string ClauseId = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);
    private const string Import = "import-2026-08-21-a";

    private static SubmissionVector Vector() =>
        new("V-1", "S0", 0, new AgentIdentity("agent-b"),
            new Basis(ClauseId, AssertionIdValue),
            new Dictionary<string, string> { ["Demo_Step"] = "5" },
            "Demo_Start",
            new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 0, "10") },
            AssertionForm.When,
            new SettlingDeclaration("count unchanged across 3 scans", new[] { "Demo_Count" }),
            MaxDurationScans: 20,
            CompletionValue: 1,
            new[] { new BlacklistEntry("FC_Other", "shares the plant model instance") },
            1,
            new[] { "ramp-to-limit" },
            "Demo_Done",
            "a ramp that overshoots by one step");

    /// <summary>The map, built the way the coordinator's binding builds it — modes DERIVED, never declared.</summary>
    private static MirrorObservability Map(params MirroredSignal[] signals) =>
        MirrorObservability.FromBindings(signals);

    private static MirroredSignal HandAuthored(string spec, string block) =>
        new(spec, MirrorValueType.Bool, SpecName: spec, LatchedBy: block);

    private static MirroredSignal Generated(string spec) =>
        new(spec, MirrorValueType.Bool, SpecName: spec, Transient: true);

    private static DeploymentDeclaration Deployment(params string[] harnessObjects) =>
        new(Import, harnessObjects.Select((o, i) => new S7ObjectDeclaration($"DB_{o}", 100 + i, o, DeclaredLayout.Standard, Import)).ToArray());

    private static TagMapReach Deliverables(params string[] names) =>
        TagMapReach.None with { DeliverableObjects = names.ToHashSet(StringComparer.Ordinal) };

    private static GateResult Gate(MirrorObservability map, DeploymentDeclaration? deployment, TagMapReach? reach = null) =>
        Report(map, deployment, reach).Gates.Single(g => g.Gate.StartsWith(GateName, StringComparison.Ordinal));

    private static SubmissionReport Report(MirrorObservability map, DeploymentDeclaration? deployment, TagMapReach? reach = null) =>
        SubmissionGate.Check(
            new[] { Vector() },
            AssertionEnumeration.Of(new[] { ClauseId }, new[] { AssertionIdValue },
                new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When }, "agent-c",
                new Dictionary<string, string>(StringComparer.Ordinal) { [AssertionIdValue] = AssertionText },
                new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                {
                    [AssertionIdValue] = new HashSet<string>(StringComparer.Ordinal) { "Demo_Count" },
                }),
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true, declaredBy: "agent-m"),
            new AgentIdentity("agent-a"),
            map,
            floorScans: 9,
            runtimeCompression: 1,
            ConflictGraph.Empty,
            compressionInputs: null,
            deployment: deployment,
            tagMapReach: reach);

    // ---- THE REFUSAL, AND THE CONTROL THAT MUST STILL WORK -----------------------------------------

    /// <summary>
    /// <b>The measured defect, reproduced.</b> The map names a latching block; the deployment enumerates
    /// its objects and that block is not among them.
    /// </summary>
    [Fact]
    public void THE_GATE_FIRES_when_the_deployment_does_not_name_the_LATCHING_BLOCK()
    {
        var gate = Gate(
            Map(HandAuthored("Demo_Count", "FB_HarnessViolationLatch")),
            Deployment("DB_HarnessMirror"),
            Deliverables("FC_ControlMain"));

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("FB_HarnessViolationLatch", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("WHICH THE DEPLOYMENT DOES NOT NAME", gate.Detail, StringComparison.Ordinal);

        // The denominator: what was compared, not only what was found.
        Assert.Contains("2 object(s) named by the deployment", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE OTHER DIRECTION. A fence that refuses everything passes every test that only checks
    /// refusals.</b> Same map, same gate — the deployment now names the block, as a DELIVERABLE.
    /// </summary>
    [Fact]
    public void AND_IT_PASSES_when_the_deployment_names_it_among_the_DELIVERABLE_objects()
    {
        var gate = Gate(
            Map(HandAuthored("Demo_Count", "FB_HarnessViolationLatch")),
            Deployment("DB_HarnessMirror"),
            Deliverables("FC_ControlMain", "FB_HarnessViolationLatch"));

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
        Assert.Contains("every hand-authored latch claim names a block the deployment declares", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// The second half of the manifest. A latch living in a harness-generated object is named by an
    /// <c>s7Objects</c> row, and that must satisfy the gate too — otherwise the check reads only one of
    /// the two lists the deployment document carries.
    /// </summary>
    [Fact]
    public void AND_IT_PASSES_when_the_deployment_names_it_as_an_s7Objects_HARNESS_OBJECT()
    {
        var gate = Gate(
            Map(HandAuthored("Demo_Count", "FB_HarnessViolationLatch")),
            Deployment("DB_HarnessMirror", "FB_HarnessViolationLatch"),
            Deliverables("FC_ControlMain"));

        Assert.True(gate.Passed);
    }

    /// <summary>
    /// A pass here is a NARROW claim and says so out loud. Closing "is it loaded" does not close "does it
    /// latch", and a reader who takes the pass for the second one has been misled by the gate.
    /// </summary>
    [Fact]
    public void A_PASS_STILL_SAYS_THE_LATCH_ITSELF_IS_TAKEN_ON_TRUST()
    {
        var gate = Gate(
            Map(HandAuthored("Demo_Count", "FB_HarnessViolationLatch")),
            Deployment("FB_HarnessViolationLatch"));

        Assert.True(gate.Passed);
        Assert.Contains("THIS CLOSES 'IS IT LOADED', NOT 'DOES IT LATCH'", gate.Detail, StringComparison.Ordinal);
    }

    // ---- NOT CHECKED, WHICH IS NEVER A PASS --------------------------------------------------------

    /// <summary>
    /// <b>No deployment document is NOT CHECKED.</b> The name is then believed exactly as
    /// <c>latched: true</c> would have been — the shape <c>latchedBy</c> exists to avoid.
    /// </summary>
    [Fact]
    public void WITH_NO_DEPLOYMENT_DOCUMENT_the_gate_is_NOT_CHECKED_and_the_submission_is_NOT_ADMISSIBLE()
    {
        var report = Report(Map(HandAuthored("Demo_Count", "FB_HarnessViolationLatch")), deployment: null);
        var gate = report.Gates.Single(g => g.Gate.StartsWith(GateName, StringComparison.Ordinal));

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);

        // Device-bound, like gate 11: what is on the controller is a property of the DOWNLOAD, and no
        // offline artifact substitutes for it. Pinned so it cannot drift onto the offline build list.
        Assert.Equal(NotCheckedReason.RequiresTheDevice, gate.Reason);
        Assert.False(gate.IsClosableOffline);

        // And it reports the denominator even here, so the reader knows how many names went unverified.
        Assert.Contains("1 HAND-AUTHORED", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>EMPTY IS NOT CLEAN, and it is not a refusal either.</b> A deployment that enumerates no object
    /// cannot confirm or deny a name — reporting "absent from an empty list" as a refusal would give the
    /// same answer for a real loaded block as for a fiction.
    /// </summary>
    [Fact]
    public void A_DEPLOYMENT_THAT_NAMES_NO_OBJECT_AT_ALL_IS_NOT_CHECKED_not_a_refusal()
    {
        var gate = Gate(
            Map(HandAuthored("Demo_Count", "FB_HarnessViolationLatch")),
            new DeploymentDeclaration(null, Array.Empty<S7ObjectDeclaration>(), NoS7Transport: true));

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Equal(NotCheckedReason.RequiresTheDevice, gate.Reason);
        Assert.Contains("AN EMPTY MANIFEST IS NOT AN EMPTY DEPLOYMENT", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("FB_HarnessViolationLatch", gate.Detail, StringComparison.Ordinal);
    }

    // ---- THE SCOPE, IN BOTH DIRECTIONS -------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE GENERATED HALF IS OUT OF SCOPE, AND THIS IS THE TEST THAT SAYS SO.</b>
    ///
    /// <para>A generated latch is derived from the signal being declared transient and the copy layer
    /// emits the rung itself — the <c>SCOIL</c>/<c>RCOIL</c> pair in
    /// <c>ir/test-project001/FC_HarnessCopyLayer.ir</c> NETWORK 8. It names no block, so there is nothing
    /// to look up. <b>Widening this gate to cover it would demand a manifest entry for something already
    /// in the artifact</b> — a refusal nobody can satisfy, which is how a gate gets switched off.</para>
    /// </summary>
    [Fact]
    public void A_GENERATED_LATCH_NAMES_NO_BLOCK_AND_IS_NOT_LOOKED_UP()
    {
        var gate = Gate(Map(Generated("Demo_Count")), Deployment("DB_HarnessMirror"));

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
        Assert.Contains("no latch claim in this map names a block", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("1 GENERATED", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The negative control on the scoping itself.</b> Identical signal, identical deployment — only
    /// the latch's SOURCE changes. If this passed too, the scoping test above would be measuring a gate
    /// that never refuses anything rather than one that refuses the right half.
    /// </summary>
    [Fact]
    public void AND_THE_SAME_SIGNAL_DECLARED_HAND_AUTHORED_INSTEAD_IS_REFUSED()
    {
        var gate = Gate(Map(HandAuthored("Demo_Count", "FB_DemoLatch")), Deployment("DB_HarnessMirror"));

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("FB_DemoLatch", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// A mixed map: one of each. <b>The hand-authored one is refused and the generated one is not
    /// mentioned as a problem</b>, and the denominator prints both — the scoping is visible in the report
    /// rather than inferable from its silence.
    /// </summary>
    [Fact]
    public void A_MIXED_MAP_REFUSES_ONLY_THE_HAND_AUTHORED_HALF_and_prints_both_counts()
    {
        var gate = Gate(
            Map(Generated("Demo_Done"), HandAuthored("Demo_Count", "FB_DemoLatch")),
            Deployment("DB_HarnessMirror"));

        Assert.False(gate.Passed);
        Assert.Contains("2 latch claim(s) in the map — 1 HAND-AUTHORED", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("1 GENERATED", gate.Detail, StringComparison.Ordinal);

        // Only ONE signal is named as a problem, and it is the hand-authored one.
        Assert.Contains("Demo_Count: latched by 'FB_DemoLatch'", gate.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Demo_Done: latched by", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The discriminator cannot drift from the text it discriminates.</b> Gate 5b splits the two halves
    /// on a rendered string, because <c>MirrorObservability.LatchProvenance</c> is string-to-string and
    /// <c>LatchSource</c> does not survive the trip. This pins the predicate against the renderer for all
    /// three sources — if someone rewords the generated prose without moving the constant, this reddens
    /// rather than silently reclassifying every generated latch as a block name.
    /// </summary>
    [Fact]
    public void THE_GENERATED_PROVENANCE_PREDICATE_AGREES_WITH_LatchSource_FOR_ALL_THREE_SOURCES()
    {
        var generated = new MirroredSignal("A", MirrorValueType.Bool, SpecName: "A", Transient: true);
        var armed = new MirroredSignal("B", MirrorValueType.Bool, SpecName: "B", Transient: true, RearmsEachIndex: true, ArmedBy: "DB_Unit.Armed");
        var handAuthored = new MirroredSignal("C", MirrorValueType.Bool, SpecName: "C", LatchedBy: "FB_DemoLatch");

        Assert.Equal(LatchSource.Generated, generated.LatchSource);
        Assert.True(MirroredSignal.IsGeneratedProvenance(generated.LatchProvenance));

        Assert.Equal(LatchSource.Generated, armed.LatchSource);
        Assert.True(MirroredSignal.IsGeneratedProvenance(armed.LatchProvenance));

        Assert.Equal(LatchSource.HandAuthored, handAuthored.LatchSource);
        Assert.False(MirroredSignal.IsGeneratedProvenance(handAuthored.LatchProvenance));
    }

    // ---- A CASE-ONLY MISS IS A REFUSAL THAT ARGUES -------------------------------------------------

    /// <summary>
    /// §4.5's object names are compared ordinally throughout, so a case-only difference is a refusal —
    /// but it is named as one, because a refusal the author cannot explain is one they route around.
    /// </summary>
    [Fact]
    public void A_CASE_ONLY_DIFFERENCE_IS_REFUSED_and_the_near_match_is_named()
    {
        var gate = Gate(
            Map(HandAuthored("Demo_Count", "fb_demolatch")),
            Deployment("FB_DemoLatch"));

        Assert.False(gate.Passed);
        Assert.Contains("differs only in case", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("FB_DemoLatch", gate.Detail, StringComparison.Ordinal);
    }

    // ---- REACHABLE FROM A DOCUMENT, NOT ONLY FROM A UNIT TEST --------------------------------------

    private const string Binding = """
    {
      "blockName": "FC_HarnessCopyLayer",
      "blockNumber": 9001,
      "tagTableName": "HarnessMirror",
      "tagPrefix": "HX_",
      "baseByte": 1000,
      "declaredRegisters": 576,
      "slots": [
        {
          "slotId": "S0",
          "startCondition": null,
          "vectorTargets": [ { "tag": "iDB_Demo.Step", "specName": "Demo_Step", "type": "Int" } ],
          "resultSources": [
            { "tag": "iDB_Demo.Count", "specName": "Demo_Count", "type": "Int", "latchedBy": "FC_DemoLatch" },
            { "tag": "iDB_Demo.Done",  "specName": "Demo_Done",  "type": "Bool" }
          ]
        }
      ]
    }
    """;

    private static string Submission(string deliverableObjects) => $$"""
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 1,
      "resultRegistersPerSlot": 20,
      "model": { "id": "M_Ramp", "declaredBy": "agent-m", "represents": ["ramp-to-limit"], "validatedAgainstPlantData": true },
      "enumeration": {
        "clauses": ["REQ-014"],
        "assertions": ["REQ-014:ffcc38"],
        "forms": { "REQ-014:ffcc38": "When" },
        "enumerator": "agent-c",
        "normalisedTexts": { "REQ-014:ffcc38": "WHEN the step is applied THEN the count reaches the limit" },
        "requiredObservations": { "REQ-014:ffcc38": ["Demo_Count"] }
      },
      "deployment": {
        "importStamp": "import-2026-08-21-a",
        "deliverableObjects": [{{deliverableObjects}}]
      },
      "vectors": [{
        "id": "V-1", "slot": "S0", "index": 0, "author": "agent-b",
        "clause": "REQ-014", "assertion": "REQ-014:ffcc38",
        "startBool": "Demo_Start",
        "inputs": { "Demo_Step": "5" },
        "expectations": [{ "signal": "Demo_Count", "nature": "PersistentState", "mode": "Latched", "windowScans": 0, "expected": "10" }],
        "settlingCondition": "count unchanged across 3 scans", "settlingSignals": ["Demo_Count"],
        "maxDurationScans": 20,
        "blacklist": [{ "block": "FC_Other", "reason": "shares the plant model" }],
        "assertionForm": "When",
        "completionValue": 1,
        "compressionFactor": 1,
        "assertedBehaviours": ["ramp-to-limit"],
        "completionSignal": "Demo_Done",
        "kills": "a ramp that overshoots by one step"
      }]
    }
    """;

    private static GateResult ThroughTheCli(string deliverableObjects) =>
        GateCli.Evaluate(SubmissionDocument.Read(Submission(deliverableObjects)), null, BindingDocument.Read(Binding))
            .Gates.Single(g => g.Gate.StartsWith(GateName, StringComparison.Ordinal));

    /// <summary>
    /// 🔴 <b>COULD ANY INPUT REACH THIS LINE?</b> <c>WaveSetAdmission</c> records what happens when the
    /// answer is no: a private loop's check was deleted and the whole suite stayed green, because nothing
    /// could reach it. So the refusal is driven once more from a real submission plus a real coordinator
    /// binding, through <c>GateCli</c> — the path a submission actually takes.
    /// </summary>
    [Fact]
    public void THE_REFUSAL_IS_REACHABLE_FROM_A_DOCUMENT_AND_A_BINDING()
    {
        var gate = ThroughTheCli("\"FC_ControlMain\"");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("FC_DemoLatch", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("WHICH THE DEPLOYMENT DOES NOT NAME", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>The document path's other direction: declare the block and the same submission clears 5b.</summary>
    [Fact]
    public void AND_THE_SAME_DOCUMENT_PASSES_ONCE_THE_DEPLOYMENT_DECLARES_THE_BLOCK()
    {
        var gate = ThroughTheCli("\"FC_ControlMain\", \"FC_DemoLatch\"");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
    }

    /// <summary>
    /// Gate 5's own admission line must point at its closure, or the report tells a reader to go and check
    /// something by hand that a gate now checks.
    /// </summary>
    [Fact]
    public void GATE_5s_ADMISSION_POINTS_AT_5b_while_still_saying_what_it_does_NOT_verify()
    {
        var five = Report(Map(HandAuthored("Demo_Count", "FB_DemoLatch")), Deployment("FB_DemoLatch"))
            .Gates.Single(g => g.Gate.StartsWith("5 observability", StringComparison.Ordinal));

        Assert.Contains("THIS GATE TAKES THE NAME, NOT THE FACT", five.Detail, StringComparison.Ordinal);
        Assert.Contains("NOW GATE 5b", five.Detail, StringComparison.Ordinal);
    }
}
