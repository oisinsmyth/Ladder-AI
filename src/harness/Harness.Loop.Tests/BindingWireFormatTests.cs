using Harness.Gate;
using Harness.Map;
using Harness.Run;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>FOLLOW EVERY NEW BINDING PROPERTY TO THE WIRE — because "the domain model gained a field and the
/// wire format did not" is the defect this repository has now hit FOUR times.</b>
///
/// <para>Its shape never varies: the capability is implemented, the generator reads it, tests of the
/// DOMAIN objects pass — and <b>nothing can set it from the only artifact a coordinator writes</b>.
/// <c>MirroredSignal.Transient</c> lived that way; so did <c>RearmsEachIndex</c>; so did a refusal flag
/// with no wire representation, which made the system LOOK like it had a guard. <i>A field nobody can set
/// is a field that does not exist, however well it is implemented downstream.</i></para>
///
/// <para><b>These tests parse JSON and assert on the DOMAIN object</b> — the region between the file and
/// the parser, which no assertion about the objects can enter.</para>
/// </summary>
public class BindingWireFormatTests
{
    private const string BindingJson = """
    {
      "blockNumber": 9001,
      "baseByte": 1000,
      "declaredRegisters": 576,
      "slots": [{
        "slotId": "INTERNAL-KEY",
        "serves": ["G-ONE", "G-TWO", "G-BOUNDARY"],
        "servesRunInOrder": true,
        "boundarySpanning": ["G-BOUNDARY"],
        "startCondition": "Stim_Start",
        "vectorTargets": [
          {
            "tag": "DB.Mode",
            "specName": "SPEC.Both",
            "type": "Int",
            "encoding": {
              "source": "md:123-130",
              "whenNumeric": "Literal",
              "numericLiteral": 1,
              "values": { "-1": 0, "HOLD": 2 }
            }
          },
          {
            "tag": "DB.At",
            "specName": "SPEC.Both",
            "type": "Time",
            "encoding": {
              "source": "md:123-130",
              "whenNumeric": "Passthrough",
              "values": { "-1": 0, "HOLD": 0 }
            }
          }
        ],
        "resultSources": [
          { "tag": "Alarm", "type": "Bool", "specName": "SPEC.Alarm",
            "inertRest": { "value": "true", "basis": "raised after a restart until acknowledged" } },
          { "tag": "Verdict", "type": "Int", "specName": "SPEC.Verdict",
            "inertRest": { "value": "-1", "basis": "sentinel: no test performed; 0 is a measured PASS" } },
          { "tag": "Pulse", "type": "Bool", "specName": "SPEC.Pulse",
            "inertRest": { "excluded": true, "basis": "one-scan pulse: reads 1 in about one sample of five" } }
        ]
      }]
    }
    """;

    private static SlotBinding Slot()
    {
        var submission = SubmissionDocument.Read("""
        {
          "blockAuthor": "agent-a",
          "runtimeCompression": 1,
          "slotsInWaveSet": 1,
          "resultRegistersPerSlot": 4,
          "enumeration": { "clauses": ["REQ-1"], "assertions": ["REQ-1:aaaaaa"] },
          "vectors": []
        }
        """);

        var request = LoopCli.Compose(submission, BindingDocument.Read(BindingJson), Array.Empty<HarnessObject>());

        return Assert.Single(request.Bindings);
    }

    [Fact]
    public void SERVES_reaches_the_domain_in_the_order_the_document_wrote_it()
    {
        // The order is load-bearing — it is the major key of the two-level ordinal — so a reader that
        // sorted or de-duplicated would silently re-sequence the wave.
        Assert.Equal(new[] { "G-ONE", "G-TWO", "G-BOUNDARY" }, Slot().Serves);
        Assert.Equal(new[] { "G-ONE", "G-TWO", "G-BOUNDARY" }, Slot().CitableSlotIds);
    }

    [Fact]
    public void SERVES_RUN_IN_ORDER_reaches_the_domain()
    {
        // Without this the wave refuses. A claim that cannot be made from the file is a refusal nobody
        // can clear.
        Assert.True(Slot().ServesRunInOrder);
    }

    [Fact]
    public void BOUNDARY_SPANNING_reaches_the_domain()
    {
        // *** THE FLAG WHOSE JOB IS TO CAUSE A REFUSAL. *** An unreachable refusal is worse than no
        // refusal, because the system looks like it has a guard.
        Assert.Equal(new[] { "G-BOUNDARY" }, Slot().BoundarySpanning);
    }

    [Fact]
    public void THE_ENCODING_reaches_the_domain_ON_BOTH_HALVES_OF_A_SHARED_SPEC_NAME()
    {
        var slot = Slot();

        Assert.Equal(2, slot.VectorTargets.Count);
        Assert.All(slot.VectorTargets, t => Assert.Equal("SPEC.Both", t.JoinKey));

        var mode = slot.VectorTargets[0];
        var at = slot.VectorTargets[1];

        Assert.NotNull(mode.Encoding);
        Assert.NotNull(at.Encoding);

        // The two READ THE SAME CITED VALUE UNDER DIFFERENT RULES, which is the whole of the two-tag
        // form. Asserted through the encoder rather than on the fields, so a reader that bound the
        // fields but dropped the mode would still be caught.
        Assert.Equal(1, mode.Encoding!.Encode("SPEC.Both", "90000").Value);
        Assert.Equal(90000, at.Encoding!.Encode("SPEC.Both", "90000").Value);

        Assert.Equal(2, mode.Encoding.Encode("SPEC.Both", "HOLD").Value);
        Assert.Equal(0, at.Encoding.Encode("SPEC.Both", "HOLD").Value);
    }

    [Fact]
    public void THE_ENCODINGS_SOURCE_reaches_the_domain_so_a_dropped_citation_FAILS_rather_than_passing()
    {
        Assert.Equal("md:123-130", Slot().VectorTargets[0].Encoding!.Source);
    }

    [Fact]
    public void A_MISSPELT_KEY_INSIDE_AN_ENCODING_IS_VISIBLE_TO_GATE_0b()
    {
        // The encoding is a NESTED object, and gate 0b has to reach inside it: a misspelt `source` would
        // silently drop the citation the encoding refuses without, and a misspelt `whenNumeric` would
        // silently leave the safe `Refuse` in place — which looks like working strictness and is
        // actually a dropped field.
        var document = BindingDocument.Read(BindingJson.Replace("\"whenNumeric\": \"Literal\"", "\"whenNumberic\": \"Literal\""));

        var (unknown, _) = document.AllExtraFieldPaths();

        Assert.Contains(unknown, p => p.EndsWith(".encoding.whenNumberic", StringComparison.Ordinal));
    }

    [Fact]
    public void THE_INERT_RESTING_VALUE_reaches_the_domain_AND_IT_IS_NOT_ZERO()
    {
        // 🔴 *** FIFTH INSTANCE OF THIS FILE'S SHAPE, AND THE WORST OF THEM. *** The resting value was not
        // merely unsettable from a binding — it was SUPPLIED, by a hardcoded
        // `Range(0, ResultRegistersNeeded).ToDictionary(i => i, _ => (ushort)0)` in the wave builder. So the
        // capability did not look missing; it looked implemented, and every run was gated on an assumption
        // no document had made.
        //
        // *** THE FIXTURE RESTS AT NON-ZERO AND AT A NEGATIVE SENTINEL DELIBERATELY. *** With everything at
        // zero this test would pass identically against the old hardcoded declaration.
        var slot = Slot();

        Assert.Equal("true", slot.ResultSources[0].Rest!.Value);
        Assert.Equal(InertRestKind.Value, slot.ResultSources[0].Rest!.Kind);
        Assert.Equal("-1", slot.ResultSources[1].Rest!.Value);
        Assert.Contains("measured PASS", slot.ResultSources[1].Rest!.Basis, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_EXCLUDED_RESTING_STATE_reaches_the_domain_WITH_ITS_REASON()
    {
        var pulse = Slot().ResultSources[2];

        Assert.True(pulse.Rest!.IsExcluded);
        Assert.Contains("one-scan pulse", pulse.Rest!.Basis, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_DECLARED_RESTS_REACH_THE_PLAN_AS_THE_REGISTERS_ACTUALLY_READ()
    {
        // Through the parser AND through the computation, because the field arriving in the domain object
        // says nothing about whether anything consumes it — which is the other half of this file's defect
        // class.
        var plan = InertRestPlan.For(Slot(), RegisterWordOrder.HighWordFirst);

        Assert.True(plan.Planned, string.Join(" | ", plan.Refusals));
        Assert.Equal(1, plan.Require().ExpectedResults[0]);       // Bool true
        Assert.Equal(65535, plan.Require().ExpectedResults[1]);   // Int -1
        Assert.True(plan.Require().Excluded.ContainsKey(2));      // the pulse
    }

    [Fact]
    public void A_MISSPELT_KEY_INSIDE_AN_INERT_REST_IS_VISIBLE_TO_GATE_0b()
    {
        // A misspelt `excluded` leaves a signal claiming a resting VALUE of null; a misspelt `basis`
        // silently removes the only thing a reader can check an exclusion by. Both are dropped in silence
        // without this.
        var document = BindingDocument.Read(BindingJson.Replace("\"excluded\": true", "\"exlcuded\": true"));

        var (unknown, _) = document.AllExtraFieldPaths();

        Assert.Contains(unknown, p => p.EndsWith(".inertRest.exlcuded", StringComparison.Ordinal));
    }

    [Fact]
    public void THE_SLOT_LEVEL_ASSUME_ZERO_CLAIM_reaches_the_domain_WITH_ITS_BASIS()
    {
        var document = BindingDocument.Read("""
        {
          "blockNumber": 9001,
          "baseByte": 1000,
          "declaredRegisters": 576,
          "slots": [{
            "slotId": "S0",
            "startCondition": "Stim_Start",
            "assumedZeroRest": true,
            "assumedZeroRestBasis": "carried from the deployed binding while its signals are declared",
            "vectorTargets": [{ "tag": "DB.X", "type": "Int" }],
            "resultSources": [{ "tag": "Alarm", "type": "Bool" }]
          }]
        }
        """);

        var submission = SubmissionDocument.Read("""
        {
          "blockAuthor": "agent-a", "runtimeCompression": 1, "slotsInWaveSet": 1,
          "resultRegistersPerSlot": 4,
          "enumeration": { "clauses": ["REQ-1"], "assertions": ["REQ-1:aaaaaa"] },
          "vectors": []
        }
        """);

        var slot = Assert.Single(LoopCli.Compose(submission, document, Array.Empty<HarnessObject>()).Bindings);

        Assert.True(slot.AssumedZeroRest);
        Assert.Contains("deployed binding", slot.AssumedZeroRestBasis!, StringComparison.Ordinal);

        // And it does what it claims: the undeclared alarm is DEFAULTED, visibly, rather than refusing.
        var plan = InertRestPlan.For(slot, RegisterWordOrder.HighWordFirst);
        Assert.True(plan.Planned, string.Join(" | ", plan.Refusals));
        Assert.Equal(1, plan.DefaultedCount);
    }

    [Fact]
    public void AN_ABSENT_SERVES_LEAVES_THE_SLOT_ANSWERING_TO_ITS_OWN_ID()
    {
        // The unaffected case: every binding written before these fields existed must behave exactly as
        // it did, and an empty `serves` is not a slot that answers to nothing.
        var document = BindingDocument.Read("""
        {
          "blockNumber": 9001,
          "baseByte": 1000,
          "declaredRegisters": 576,
          "slots": [{
            "slotId": "S0",
            "startCondition": "Stim_Start",
            "vectorTargets": [{ "tag": "DB.X", "type": "Int" }],
            "resultSources": [{ "tag": "Alarm", "type": "Bool" }]
          }]
        }
        """);

        var submission = SubmissionDocument.Read("""
        {
          "blockAuthor": "agent-a", "runtimeCompression": 1, "slotsInWaveSet": 1,
          "resultRegistersPerSlot": 4,
          "enumeration": { "clauses": ["REQ-1"], "assertions": ["REQ-1:aaaaaa"] },
          "vectors": []
        }
        """);

        var slot = Assert.Single(LoopCli.Compose(submission, document, Array.Empty<HarnessObject>()).Bindings);

        Assert.Empty(slot.Serves);
        Assert.False(slot.ServesRunInOrder);
        Assert.Empty(slot.BoundarySpanning);
        Assert.Equal(new[] { "S0" }, slot.CitableSlotIds);
        Assert.Null(slot.VectorTargets[0].Encoding);
    }
}
