using Harness.Gate;
using Harness.Results;
using Xunit;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>GATE 0c — a derivable field that was TYPED rather than PRODUCED is a refusal naming it.</b>
///
/// <para>*** THE PROBLEM THIS CLOSES. *** A dozen sub-documents of a submission — the map, storage, the
/// conflict edges, the deployment stamps, the compression ceilings — are each produced by a tool that
/// already exists, and nothing composed them into the document the gate reads. So an agent retyped them
/// and every downstream gate graded the retyping faithfully.</para>
///
/// <para><b>Every case below is asserted in BOTH directions</b>, following this suite's own discipline: a
/// change that makes everything NOT CHECKED passes every test that only looks for NOT CHECKED. So each
/// refusal is paired with the same submission passing once the defect is removed.</para>
/// </summary>
public class DerivedFieldsGateTests
{
    private const string Gate0c = "0c derived fields";

    // ---------------------------------------------------------------------------------------------
    // the control, and the refusal it is a control for
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_DERIVED_SUBMISSION_PASSES_0c_AND_IS_ADMISSIBLE()
    {
        var (exit, output, gate) = Run(derive: true);

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed, gate.Detail);
        Assert.Equal(GateExit.AdmissibleSubjectToJudgement, exit);
        Assert.Contains("ADMISSIBLE-SUBJECT-TO-JUDGEMENT", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_THE_SAME_SUBMISSION_HAND_AUTHORED_IS_REFUSED_WITH_EVERY_FIELD_NAMED()
    {
        var (exit, _, gate) = Run(derive: false);

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("AUTHORED, not derived", gate.Detail, StringComparison.Ordinal);
        Assert.Equal(GateExit.NotAdmissible, exit);

        // *** NAMED, NOT COUNTED. *** "3 fields were authored" tells an author nothing about what to go
        // and fix; the whole value of the refusal is that it names the key to delete.
        Assert.Contains(DerivableField.Map, gate.Detail, StringComparison.Ordinal);
        Assert.Contains(DerivableField.Deployment, gate.Detail, StringComparison.Ordinal);
        Assert.Contains(DerivableField.RuntimeCompression, gate.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // the ways a provenance record can be wrong
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AN_ARTIFACT_THAT_HAS_CHANGED_SINCE_DERIVATION_IS_REFUSED_AS_STALE()
    {
        var gate = GateOf(Evaluate(Stamped(), readArtifact: "this is not what was hashed"));

        Assert.False(gate.Passed);
        Assert.Contains("CHANGED since", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("re-derive rather than re-stamp", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_UNREADABLE_ARTIFACT_IS_NOT_CHECKED_AND_NEVER_A_PASS()
    {
        // *** "COULD NOT LOOK" IS NOT "IT MATCHED". *** This is the distinction the whole system keeps
        // having to relearn: an empty or absent check reported as a pass is the failure mode, so an
        // artifact the gate cannot open leaves the field unverified and the submission NOT ADMISSIBLE.
        var gate = GateOf(Evaluate(Stamped(), readArtifact: null));

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("not a matching one", gate.Detail, StringComparison.Ordinal);
        Assert.Equal(NotCheckedReason.AwaitingAnArtifactThatCouldExist, gate.Reason);
    }

    [Fact]
    public void A_PRODUCER_THE_GATE_DOES_NOT_KNOW_IS_REFUSED_BY_NAME()
    {
        // Without a closed producer set, `"producer": "me"` satisfies the gate perfectly and the author is
        // attesting to their own transcription in the field designed to prevent exactly that.
        var evidence = new DerivationEvidence(
            new[] { Record(DerivableField.Map, producer: "me") },
            new[] { DerivableField.Map });

        var gate = GateOf(SubmissionGate.Check(
            Vectors(), Enumeration(), null, new AgentIdentity("agent-a"), Map(), 9, 1, null,
            derivation: evidence));

        Assert.False(gate.Passed);
        Assert.Contains("does not know", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("'me'", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_RECORD_ATTESTING_TO_A_FIELD_THE_DOCUMENT_DOES_NOT_CARRY_IS_REFUSED()
    {
        // The deriver produced it and the submission does not have it: one of the two lost data, and
        // neither answer is "fine".
        var evidence = new DerivationEvidence(
            new[] { Record(DerivableField.Map), Record(DerivableField.Deployment) },
            new[] { DerivableField.Map });

        var gate = GateOf(SubmissionGate.Check(
            Vectors(), Enumeration(), null, new AgentIdentity("agent-a"), Map(), 9, 1, null,
            derivation: evidence));

        Assert.False(gate.Passed);
        Assert.Contains("does not carry", gate.Detail, StringComparison.Ordinal);
        Assert.Contains(DerivableField.Deployment, gate.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // the denominator, and the two kinds of nothing
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_DENOMINATOR_IS_PRINTED_ON_EVERY_RUN_INCLUDING_THE_PASSING_ONE()
    {
        var (_, _, gate) = Run(derive: true);

        // Every other number this gate reports is a reason something did NOT happen. This one says how
        // many were in scope, and a claim without it is the shape of green this project keeps retracting.
        Assert.Contains("DENOMINATOR:", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("re-hashed against the artifact on disk", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void NOBODY_SUPPLYING_THE_EVIDENCE_IS_NOT_CHECKED_RATHER_THAN_A_PASS()
    {
        var gate = GateOf(SubmissionGate.Check(
            Vectors(), Enumeration(), null, new AgentIdentity("agent-a"), Map(), 9, 1, null,
            derivation: null));

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Equal(NotCheckedReason.HarnessCapabilityMissing, gate.Reason);
    }

    [Fact]
    public void A_SUBMISSION_CARRYING_NO_DERIVABLE_FIELD_PASSES_0c_AND_SAYS_WHERE_THE_ABSENCE_IS_JUDGED()
    {
        // 🔴 *** THE ONE PLACE THIS GATE DOES NOT APPLY EMPTY-IS-NOT-CLEAN, AND WHY. *** 0c asks whether
        // anything here was hand-authored. With no derivable field present the honest answer is no. The
        // ABSENCE of those fields is a real finding and belongs to the gates that consume them — see the
        // companion test, which shows omission is a WORSE outcome by a different door, not an escape.
        var gate = GateOf(SubmissionGate.Check(
            Vectors(), Enumeration(), null, new AgentIdentity("agent-a"), Map(), 9, 1, null,
            derivation: new DerivationEvidence(Array.Empty<DerivationRecord>(), Array.Empty<string>())));

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
        Assert.Contains("no derivable field is present", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("NOT ADMISSIBLE", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_OMITTING_THEM_IS_STILL_NOT_ADMISSIBLE_so_0c_IS_NOT_ESCAPABLE_BY_DELETION()
    {
        // The claim the previous test's message makes, verified rather than asserted in prose: dropping
        // the derivable fields to dodge 0c leaves the gates that consume them unable to run, and a single
        // NOT CHECKED already makes the submission NOT ADMISSIBLE.
        var report = SubmissionGate.Check(
            Vectors(), Enumeration(), null, new AgentIdentity("agent-a"), Map(), 9, 1, null,
            derivation: new DerivationEvidence(Array.Empty<DerivationRecord>(), Array.Empty<string>()));

        Assert.True(GateOf(report).Passed);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);
    }

    // ---------------------------------------------------------------------------------------------
    // fixtures
    // ---------------------------------------------------------------------------------------------

    private static (int Exit, string Output, GateResult Gate) Run(bool derive)
    {
        var submission = derive
            ? DerivedFixture.WithDerivation(GateCliTests.Good, "binding.json", GateCliTests.Binding)
            : GateCliTests.Good;

        var writer = new StringWriter();
        var exit = GateCli.Run(new[] { "check", "sub.json", "--binding", "binding.json" }, writer,
            path => path switch
            {
                "tags.json" => GateCliTests.TagMap,
                "binding.json" => GateCliTests.Binding,
                _ => submission,
            });

        var report = GateCli.Evaluate(
            SubmissionDocument.Read(submission),
            path => path switch
            {
                "tags.json" => GateCliTests.TagMap,
                "binding.json" => GateCliTests.Binding,
                _ => submission,
            },
            BindingDocument.Read(GateCliTests.Binding));

        return (exit, writer.ToString(), GateOf(report));
    }

    /// <summary>The known-admissible submission, stamped by the PRODUCTION deriver rather than by hand.</summary>
    private static string Stamped() =>
        DerivedFixture.WithDerivation(GateCliTests.Good, "binding.json", GateCliTests.Binding);

    private static SubmissionReport Evaluate(string submission, string? readArtifact) =>
        GateCli.Evaluate(
            SubmissionDocument.Read(submission),
            path => path switch
            {
                "tags.json" => GateCliTests.TagMap,
                "binding.json" => readArtifact ?? throw new IOException("the artifact is gone."),
                _ => submission,
            },
            BindingDocument.Read(GateCliTests.Binding));

    private static GateResult GateOf(SubmissionReport report) =>
        Assert.Single(report.Gates, g => string.Equals(g.Gate, Gate0c, StringComparison.Ordinal));

    private static DerivationRecord Record(string field, string? producer = null, string? observed = "hash") =>
        new(field, producer ?? DeriveCli.DefaultProducerFor(field), "artifact.json", "hash", observed);

    private static SubmissionVector[] Vectors() => new[] { SubmissionGateTests.Vector() };

    private static AssertionEnumerationSet Enumeration() => SubmissionGateTests.EnumerationForDerivationTests();

    private static MirrorObservability Map() =>
        MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })) with { Provenance = MapProvenance.Bindings };
}
