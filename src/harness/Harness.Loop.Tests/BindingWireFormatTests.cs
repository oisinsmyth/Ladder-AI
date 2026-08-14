using Harness.Gate;
using Harness.Map;
using Harness.Run;

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
        "resultSources": [{ "tag": "Alarm", "type": "Bool", "specName": "SPEC.Alarm" }]
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
    public void AN_ABSENT_SERVES_LEAVES_THE_SLOT_ANSWERING_TO_ITS_OWN_ID()
    {
        // The unaffected case: every binding written before these fields existed must behave exactly as
        // it did, and an empty `serves` is not a slot that answers to nothing.
        var document = BindingDocument.Read("""
        {
          "blockNumber": 9001,
          "baseByte": 1000,
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
