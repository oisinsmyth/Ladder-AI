using Harness.Gate;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// *** THE FOUR BUILD-LIST GATES, DRIVEN THROUGH THE DOCUMENT WITH THEIR INPUTS SUPPLIED. ***
///
/// <para>4b, 8s, 8 and 8c report NOT CHECKED on the live submission because it does not carry
/// <c>model.declaredBy</c>, a complete <c>map.storage</c>/<c>map.harnessOnly</c> join, or a conflict
/// graph. <b>The harness half of all four is built and wired</b> — and until this file existed that was
/// an assertion. Here each one is supplied its input, shown to RUN, and shown to FAIL, because
/// <i>a gate moved from NOT CHECKED to CHECKED-and-passing that cannot fail has moved from honest to
/// worthless.</i></para>
///
/// <para><b>And the whole point of the exercise is pinned by the last test:</b> closing all four does
/// NOT make the submission admissible. Gate 11 needs the device, no artifact substitutes, and if the
/// verdict ever flips without a download having happened that is the finding, not the milestone.</para>
/// </summary>
public class BuildListClosureTests
{
    private const string Clause = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(Clause, AssertionText);

    /// <summary>
    /// A submission carrying EVERY offline-closable input: the enumeration's four fields, the model's
    /// declaring authority, the storage join, and a conflict graph with provenance.
    /// </summary>
    private const string Complete = """
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 1,
      // Gate 1b: 400 ms is 17 scans, so the vector's 20-scan maxDuration clears the floor and sits well
      // under the ceiling. Without this the fixture stops being COMPLETE, which is what this file asserts.
      "scenarioEndInput": "Demo_EndAt",
      "resultRegistersPerSlot": 20,
      "conflictEdges": [
        { "blockA": "FC_Demo", "blockB": "FC_Other", "provenance": "SharedModel", "signal": "Demo_Count", "class": "HarnessInstrumentation" }
      ],
      "model": {
        "id": "M_Ramp",
        "declaredBy": "agent-m",
        "represents": ["ramp-to-limit"],
        "doesNotRepresent": ["overshoot"],
        "validatedAgainstPlantData": true
      },
      "enumeration": {
        "clauses": ["REQ-014"],
        "assertions": ["REQ-014:ffcc38"],
        "forms": { "REQ-014:ffcc38": "When" },
        "enumerator": "agent-c",
        "normalisedTexts": { "REQ-014:ffcc38": "WHEN the step is applied THEN the count reaches the limit" },
        "requiredObservations": { "REQ-014:ffcc38": ["Demo_Count"] },
        "bounds": { "limit": "10" }
      },
      "map": {
        "providedFor": { "Demo_Count": ["Latched"] },
        "harnessOnly": ["Demo_Count"]
      },
      "vectors": [{
        "id": "V-1", "slot": "S0", "index": 0, "author": "agent-b",
        "clause": "REQ-014", "assertion": "REQ-014:ffcc38",
        "startBool": "Demo_Start",
        "inputs": { "Demo_EndAt": "400" },
        "expectations": [{ "signal": "Demo_Count", "nature": "PersistentState", "mode": "Latched", "windowScans": 0, "expected": "10" }],
        "settlingCondition": "count unchanged across 3 scans", "settlingSignals": ["Demo_Count"],
        "maxDurationScans": 20,
        "blacklist": [{ "block": "FC_Other", "reason": "shares the plant model" }],
        "assertionForm": "When",
        "completionValue": 1,
        "compressionFactor": 1,
        "assertedBehaviours": ["ramp-to-limit"],
        "completionSignal": "Demo_Done",
        "kills": "a ramp that overshoots by one step",
        "boundsUsed": { "limit": "10" }
      }]
    }
    """;

    private static (int Exit, string Output) Run(string json)
    {
        var writer = new StringWriter();
        var exit = GateCli.Run(new[] { "check", "sub.json" }, writer, _ => json);
        return (exit, writer.ToString());
    }

    private static GateResult Gate(string json, string startsWith) =>
        GateCli.Evaluate(SubmissionDocument.Read(json)).Gates
            .Single(g => g.Gate.StartsWith(startsWith, StringComparison.Ordinal));

    /// <summary>
    /// The coordinator's binding — the authority gate 5 consults. <b>The one for the live block exists as
    /// PROSE and not as data</b>, which is exactly the transcription this shape shows to be sufficient.
    /// </summary>
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
          "vectorTargets": [
            { "tag": "iDB_Demo.Step", "specName": "Demo_Step", "type": "Int" }
          ],
          "resultSources": [
            { "tag": "iDB_Demo.Count", "specName": "Demo_Count", "type": "Int", "latchedBy": "FC_DemoLatch" },
            { "tag": "iDB_Demo.Done",  "specName": "Demo_Done",  "type": "Bool" }
          ]
        }
      ]
    }
    """;

    private static SubmissionReport EvaluateWithBinding(string json) =>
        GateCli.Evaluate(SubmissionDocument.Read(json), _ => json, BindingDocument.Read(Binding));

    // ---------------------------------------------------------------------------------------------
    // 4b — the model declaration's own identity field
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void GATE_4b_RUNS_ONCE_THE_MODEL_RECORDS_WHO_DECLARED_IT()
    {
        var gate = Gate(Complete, "4b fidelity authority");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
    }

    /// <summary>
    /// <b>The mutation.</b> M4 permits an assertion only if the model claims that behaviour, so an author
    /// who writes both the vectors and the Represents list permits their own assertions. Same name in both
    /// places must REFUSE, or 4b is a field being read and nothing being checked.
    /// </summary>
    [Fact]
    public void AND_IT_FAILS_WHEN_THE_VECTOR_AUTHOR_DECLARED_THE_MODEL_THAT_LICENSES_THEM()
    {
        var selfLicensed = Complete.Replace("\"declaredBy\": \"agent-m\"", "\"declaredBy\": \"agent-b\"", StringComparison.Ordinal);

        var gate = Gate(selfLicensed, "4b fidelity authority");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("permits their own assertions", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_IT_GOES_BACK_TO_NOT_CHECKED_WHEN_THE_FIELD_IS_REMOVED()
    {
        var anonymous = Complete.Replace("\"declaredBy\": \"agent-m\",", string.Empty, StringComparison.Ordinal);

        var gate = Gate(anonymous, "4b fidelity authority");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Equal(NotCheckedReason.AwaitingAnArtifactThatCouldExist, gate.Reason);
    }

    // ---------------------------------------------------------------------------------------------
    // 8s — the storage join (contract §2.7)
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void GATE_8s_RUNS_ONCE_EVERY_SIGNAL_IS_JOINED_OR_DECLARED_HARNESS_ONLY()
    {
        var gate = Gate(Complete, "8s signal storage join");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
    }

    /// <summary>
    /// <b>The mutation.</b> <c>harnessOnly</c> is a POSITIVE claim that a signal occupies no PLC storage.
    /// Declaring the same signal in both is a contradiction, and choosing which to believe would be the
    /// gate deciding what the author meant.
    /// </summary>
    [Fact]
    public void AND_IT_FAILS_ON_A_SIGNAL_DECLARED_BOTH_IN_STORAGE_AND_HARNESS_ONLY()
    {
        var contradictory = Complete.Replace(
            "\"harnessOnly\": [\"Demo_Count\"]",
            "\"harnessOnly\": [\"Demo_Count\"], \"storage\": { \"Demo_Count\": { \"owner\": \"FC_Demo\", \"path\": \"DB_Demo.Count\" } }",
            StringComparison.Ordinal);

        var gate = Gate(contradictory, "8s signal storage join");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("cannot both occupy storage and occupy none", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>An unjoined signal is NOT CHECKED, not a pass</b> — and this is the exact shape the live
    /// submission is in: 2 of 6 signals in neither list.
    /// </summary>
    [Fact]
    public void AND_A_SIGNAL_IN_NEITHER_LIST_RETURNS_IT_TO_NOT_CHECKED()
    {
        // The map must stay NON-EMPTY, or the branch under test is the "nothing was declared at all"
        // one — a different fact with a different repair, and asserting on it here would pass for the
        // wrong reason.
        var unjoined = Complete.Replace(
            "\"harnessOnly\": [\"Demo_Count\"]",
            "\"harnessOnly\": [\"Some_Other_Signal\"]",
            StringComparison.Ordinal);

        var gate = Gate(unjoined, "8s signal storage join");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains("NOBODY STATED THE JOIN", gate.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 8 and 8c — the blacklist and its provenance
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void GATES_8_AND_8c_RUN_ONCE_A_CONFLICT_GRAPH_WITH_PROVENANCE_IS_SUPPLIED()
    {
        Assert.Equal(GateStatus.Checked, Gate(Complete, "8 blacklist").Status);
        Assert.Equal(GateStatus.Checked, Gate(Complete, "8c multi-writer").Status);
    }

    /// <summary>
    /// <b>The mutation for 8 — and finding it took two attempts, which is the useful part.</b>
    ///
    /// <para>The obvious mutation is to empty the blacklist and expect the add-only property to refuse.
    /// <b>It cannot.</b> Gate 8 computes <c>effective = computed UNION declared</c> and then asks whether
    /// <c>computed</c> is a subset of it — which is true for every input, by construction. The gate says
    /// so in as many words (add-only is a property of the TYPE: <c>BlacklistEntry</c> carries no negation),
    /// so that assertion is documentation rather than a check.</para>
    ///
    /// <para>What gate 8 can actually refuse is an entry with <b>no recorded reason</b> — and that is the
    /// failure mode it names: defensive over-blacklisting, concurrency collapsing toward serial, and
    /// nobody noticing BECAUSE IT STILL WORKS.</para>
    /// </summary>
    [Fact]
    public void AND_GATE_8_FAILS_ON_A_BLACKLIST_ENTRY_THAT_RECORDS_NO_REASON()
    {
        var unreasoned = Complete.Replace(
            "{ \"block\": \"FC_Other\", \"reason\": \"shares the plant model\" }",
            "{ \"block\": \"FC_Other\" }",
            StringComparison.Ordinal);

        var gate = Gate(unreasoned, "8 blacklist");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("carries no reason", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// *** AND THE ADD-ONLY ASSERTION IS RECORDED AS UNFALSIFIABLE, RATHER THAN LEFT LOOKING LIKE A CHECK. ***
    /// Emptying the blacklist entirely leaves gate 8 PASSING. That is correct given the type, and it is
    /// pinned here so nobody later reads the superset line as evidence that a thin blacklist was caught.
    /// </summary>
    [Fact]
    public void The_add_only_superset_assertion_cannot_fail_and_that_is_stated_not_hidden()
    {
        var empty = Complete.Replace(
            "\"blacklist\": [{ \"block\": \"FC_Other\", \"reason\": \"shares the plant model\" }],",
            "\"blacklist\": [],",
            StringComparison.Ordinal);

        Assert.True(Gate(empty, "8 blacklist").Passed);
    }

    /// <summary>
    /// <b>The mutation for 8c.</b> An edge with no provenance records WHY nothing, and an unattributed
    /// graph is indistinguishable from one nobody computed.
    /// </summary>
    [Fact]
    public void AND_GATE_8c_RETURNS_TO_NOT_CHECKED_WHEN_AN_EDGE_STATES_NO_PROVENANCE()
    {
        var unattributed = Complete.Replace("\"provenance\": \"SharedModel\", ", string.Empty, StringComparison.Ordinal);

        var gate = Gate(unattributed, "8c multi-writer");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Equal(NotCheckedReason.AwaitingAnArtifactThatCouldExist, gate.Reason);
    }

    // ---------------------------------------------------------------------------------------------
    // 🔴 THE CONSTRAINT: closing them must not make the submission admissible
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// *** ALL FOUR CLOSED, AND THE VERDICT IS STILL NOT ADMISSIBLE. ***
    ///
    /// <para>Gate 11 requires the device: <c>deployment</c> is a property of the DOWNLOAD, no artifact
    /// substitutes for it, and resubmitting a vector cannot supply it. <b>If this ever flips to
    /// admissible without a download having happened, that is the finding</b> — it would mean an
    /// offline-closable input had been allowed to stand in for a device one.</para>
    /// </summary>
    [Fact]
    public void CLOSING_ALL_FOUR_LEAVES_THE_VERDICT_NOT_ADMISSIBLE_BECAUSE_GATE_11_NEEDS_THE_DEVICE()
    {
        var (exit, output) = Run(Complete);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("VERDICT: NOT ADMISSIBLE", output, StringComparison.Ordinal);

        var report = GateCli.Evaluate(SubmissionDocument.Read(Complete));

        // The four are gone from the NOT CHECKED list...
        foreach (var closed in new[] { "4b fidelity", "8s signal storage", "8 blacklist", "8c multi-writer" })
            Assert.DoesNotContain(report.NotChecked, g => g.Gate.StartsWith(closed, StringComparison.Ordinal));

        // ...and gate 11 is still there, still device-bound, still not closable offline.
        var eleven = report.NotChecked.Single(g => g.Gate.StartsWith("11 memory layout", StringComparison.Ordinal));
        Assert.Equal(NotCheckedReason.RequiresTheDevice, eleven.Reason);
        Assert.False(eleven.IsClosableOffline);

        // *** AND THE STRONGEST FORM: EVERYTHING OFFLINE-CLOSABLE CLOSED, INCLUDING GATE 5's BINDING. ***
        // The verdict is STILL NOT ADMISSIBLE. `deployment` is a property of the download; no artifact
        // substitutes for it and resubmitting a vector cannot supply it. If this ever flips without a
        // download having happened, an offline input has been allowed to stand in for a device one.
        Assert.Equal(SubmissionVerdict.NotAdmissible, EvaluateWithBinding(Complete).Verdict);
    }

    /// <summary>
    /// The negative control on the fixture itself. If <c>Complete</c> stopped supplying the inputs, every
    /// "it runs now" test above would pass by examining a gate that was NOT CHECKED for a different reason.
    /// </summary>
    [Fact]
    public void The_complete_fixture_really_does_close_the_four_and_nothing_else()
    {
        // *** EVERY OFFLINE-CLOSABLE INPUT SUPPLIED, INCLUDING THE COORDINATOR'S BINDING. *** Exactly one
        // NOT CHECKED must remain, and it must be the device one. A larger number means the fixture is
        // not what these tests believe it is; a smaller one would mean a device-bound gate had been
        // satisfied by an artifact, which is the finding rather than the milestone.
        var remaining = EvaluateWithBinding(Complete).NotChecked;

        var gate = Assert.Single(remaining);
        Assert.StartsWith("11 memory layout", gate.Gate, StringComparison.Ordinal);
        Assert.Equal(NotCheckedReason.RequiresTheDevice, gate.Reason);

        // And nothing that remains is closable offline — the build list is empty for this submission.
        Assert.DoesNotContain(remaining, g => g.IsClosableOffline);
    }
}
