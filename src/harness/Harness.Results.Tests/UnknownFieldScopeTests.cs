using Harness.Gate;
using Harness.Map;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// Gate 0b's SCOPE, and the binding document it did not inspect.
///
/// <para><b>The rationale was right and the scope was wrong.</b> 0b exists to catch a contract/code
/// divergence — a field the contract SPECIFIES that the code does not read, which vanishes silently and
/// leaves a green built on declarations nobody looked at. It refused the deliverable 82 times over its
/// own commentary, and <i>a gate that refuses every ordinary submission is a gate that gets switched
/// off — which is worse than no gate, because it still appears in the list looking like a check</i>.</para>
///
/// <para><b>So the narrowing is tested from both sides.</b> Narrowing a gate until it passes the
/// deliverable is one step from narrowing it until it passes everything, and the tests that matter here
/// are the ones proving it still refuses.</para>
/// </summary>
public class UnknownFieldScopeTests
{
    // ---------------------------------------------------------------------------------------------
    // THE SPLIT
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("_artifact")]
    [InlineData("vectors[3]._whyThisBound")]
    [InlineData("enumeration._provenance")]
    [InlineData("binding.slots[0].resultSources[2]._note")]
    public void An_UNDERSCORE_PREFIXED_key_is_an_ANNOTATION_and_is_not_refused(string path)
    {
        var (unknown, annotations) = SubmissionDocument.Split(new[] { path });

        Assert.Empty(unknown);
        Assert.Single(annotations);
    }

    /// <summary>
    /// *** THE THREE FIELDS 0b WAS BUILT FOR. *** Contract §2.8 specifies them and the code implements
    /// <c>specName</c>/<c>latchedBy</c> only — so an author writing to the contract emits three fields
    /// that vanish. They must still be refused, and the narrowing must not have moved them.
    /// </summary>
    [Theory]
    [InlineData("modes")]
    [InlineData("modeSource")]
    [InlineData("instrumentedBy")]
    [InlineData("vectors[0].expectations[1].modes")]
    public void THE_DIVERGENCE_THIS_GATE_EXISTS_FOR_IS_STILL_REFUSED(string path)
    {
        var (unknown, annotations) = SubmissionDocument.Split(new[] { path });

        Assert.Single(unknown);
        Assert.Empty(annotations);
    }

    /// <summary>
    /// <b>The LEAF decides, not the path.</b> A path is <c>vectors[3]._why</c>, and only the last segment
    /// is the key the author actually wrote — a parent under an annotation-shaped prefix is still a real
    /// field.
    /// </summary>
    [Fact]
    public void The_LEAF_name_decides_so_a_real_field_under_an_annotated_parent_is_still_refused()
    {
        var (unknown, annotations) = SubmissionDocument.Split(new[] { "_wrapper.modes" });

        Assert.Equal(new[] { "_wrapper.modes" }, unknown);
        Assert.Empty(annotations);
    }

    [Fact]
    public void An_underscore_INSIDE_a_name_is_not_an_annotation()
    {
        // `mode_source` is a misspelling of a real field, not a comment. Only a LEADING underscore is
        // the marker, because the marker has to be one an author cannot type by accident.
        var (unknown, _) = SubmissionDocument.Split(new[] { "mode_source" });

        Assert.Single(unknown);
    }

    // ---------------------------------------------------------------------------------------------
    // THROUGH THE DOCUMENT
    // ---------------------------------------------------------------------------------------------

    private const string Annotated = """
    {
      "_artifact": "the deliverable wave set",
      "_whyThisShape": "commentary that belongs beside the data, because JSON has no comments",
      "blockAuthor": "agent-a",
      "model": { "id": "M", "represents": ["r"], "_modelNote": "why this model" },
      "enumeration": { "clauses": ["REQ-1"], "assertions": ["REQ-1:aaaaaa"], "_source": "issue 4" },
      "vectors": [{ "id": "V-1", "_whyThisVector": "the isolating defect" }]
    }
    """;

    [Fact]
    public void EVERY_annotation_in_a_document_is_reported_and_NONE_is_refused()
    {
        var (unknown, annotations) = SubmissionDocument.Read(Annotated).AllExtraFieldPaths();

        Assert.Empty(unknown);
        Assert.Equal(5, annotations.Count);
        Assert.Contains("_artifact", annotations);
        Assert.Contains("model._modelNote", annotations);
        Assert.Contains("enumeration._source", annotations);
        Assert.Contains("vectors[0]._whyThisVector", annotations);
    }

    [Fact]
    public void A_REAL_unknown_field_beside_the_annotations_is_still_found()
    {
        var withDivergence = Annotated.Replace("\"blockAuthor\": \"agent-a\",", "\"blockAuthor\": \"agent-a\", \"modeSource\": \"derived\",", StringComparison.Ordinal);

        var (unknown, annotations) = SubmissionDocument.Read(withDivergence).AllExtraFieldPaths();

        Assert.Equal(new[] { "modeSource" }, unknown);
        Assert.Equal(5, annotations.Count);
    }

    // ---------------------------------------------------------------------------------------------
    // THE BINDING DOCUMENT — outside the gate entirely until now
    // ---------------------------------------------------------------------------------------------

    private const string Binding = """
    {
      "blockName": "FC_HarnessCopyLayer",
      "blockNumber": 9000,
      "baseByte": 1000,
      "declaredRegisters": 576,
      "slots": [{
        "slotId": "S0",
        "startCondition": "HX_Start",
        "resultSources": [
          { "tag": "DB_Unit.Alarm", "type": "Bool", "specName": "HopperBlockedAlarm", "transient": true },
          { "tag": "DB_Unit.Count", "type": "Int", "spec_name": "Count" }
        ]
      }]
    }
    """;

    /// <summary>
    /// 🔴 <b>A MISSPELT <c>specName</c> IS THE FAILURE THIS GATE IS FOR, AND THE BINDING WAS OUTSIDE
    /// IT.</b>
    ///
    /// <para>The document had no extension data at all, so every unknown key was dropped in silence — on
    /// the half that carries the instrumentation. <c>specName</c>'s own history is the argument: while
    /// the translation lived only in prose, gate 5, the interface check and the conflict graph all failed
    /// in one run with 1 of 17 signals resolving.</para>
    /// </summary>
    [Fact]
    public void A_MISSPELT_specName_IN_THE_BINDING_IS_NOW_FOUND()
    {
        var (unknown, _) = BindingDocument.Read(Binding).AllExtraFieldPaths();

        Assert.Equal(new[] { "binding.slots[0].resultSources[1].spec_name" }, unknown);
    }

    /// <summary>
    /// <b>CASE IS NOT THE VECTOR, and that is deliberate rather than a gap.</b> Both readers set
    /// <c>PropertyNameCaseInsensitive</c>, so <c>specname</c> binds to <c>SpecName</c> and is READ, not
    /// dropped. 0b's subject is a field that VANISHES; a field that binds has not vanished.
    ///
    /// <para>Recorded as a test because the first draft of the test above used <c>specname</c> and passed
    /// for the wrong reason — <i>the check was right and the instrument was wrong</i>.</para>
    /// </summary>
    [Fact]
    public void A_CASE_VARIANT_binds_and_is_therefore_NOT_an_unknown_field()
    {
        var json = Binding.Replace("\"spec_name\"", "\"specname\"", StringComparison.Ordinal);
        var binding = BindingDocument.Read(json);

        Assert.Empty(binding.AllExtraFieldPaths().Unknown);
        Assert.Equal("Count", binding.Slots![0].ResultSources![1].SpecName);
    }

    [Fact]
    public void An_annotation_in_the_binding_is_excluded_the_same_way()
    {
        var annotated = Binding.Replace("\"blockName\":", "\"_whyThisBinding\": \"the coordinator's half\", \"blockName\":", StringComparison.Ordinal);

        var (_, annotations) = BindingDocument.Read(annotated).AllExtraFieldPaths();

        Assert.Contains("binding._whyThisBinding", annotations);
    }

    // ---------------------------------------------------------------------------------------------
    // transient / rearmsEachIndex — the wire representation that did not exist
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// *** THIRD INSTANCE: THE DOMAIN MODEL GAINED THE FIELD AND THE WIRE FORMAT DID NOT. ***
    /// <c>MirroredSignal.Transient</c> existed, the generator read it, and <c>ToMirroredSignal</c> never
    /// passed it — so the capability was unreachable from the only artifact a coordinator writes.
    /// </summary>
    [Fact]
    public void transient_REACHES_THE_DOMAIN_MODEL_FROM_A_BINDING()
    {
        var binding = BindingDocument.Read(Binding);
        var signal = GateCli.ToMirroredSignal(binding.Slots![0].ResultSources![0]);

        Assert.True(signal.Transient);
        Assert.True(signal.LatchClaimed);
    }

    [Fact]
    public void An_UNSTATED_transient_is_the_ABSENCE_of_a_claim_and_claims_no_latch()
    {
        var binding = BindingDocument.Read(Binding);
        var signal = GateCli.ToMirroredSignal(binding.Slots![0].ResultSources![1]);

        Assert.False(signal.Transient);
        Assert.False(signal.LatchClaimed);
    }

    /// <summary>
    /// <b>The SAME hole, checked because the second and third flags travel the same path.</b> A field
    /// nobody can set is a field that does not exist, however well it is implemented downstream.
    /// </summary>
    [Fact]
    public void rearmsEachIndex_AND_armedBy_REACH_THE_DOMAIN_MODEL_TOO()
    {
        var signal = GateCli.ToMirroredSignal(BindingDocument.Read(ReArming).Slots![0].ResultSources![0]);

        Assert.True(signal.RearmsEachIndex);
        Assert.True(signal.PhaseArmed);
        Assert.Equal("DB_Unit.Armed", signal.ArmWindow);
    }

    /// <summary>
    /// 🔴 <b>AND THE CAPABILITY IS REACHABLE FROM A BINDING — asserted on the EMITTED IR, not on the
    /// flag.</b>
    ///
    /// <para>This test used to assert the generator's REFUSAL, because <c>rearmsEachIndex</c>'s only
    /// implementation was one. The property it was protecting was never the refusal — it was that a
    /// re-arming signal must never receive the unconditional latch, since that is what silently deletes
    /// every finding turning on a signal FALLING. That property is asserted here against the text the
    /// generator produces, all the way from a JSON document.</para>
    /// </summary>
    [Fact]
    public void And_the_PHASE_ARMED_LATCH_is_reachable_from_a_binding()
    {
        var layer = GenerateFrom(ReArming, startCondition: "HX_Start");

        Assert.True(layer.Generated);
        Assert.Empty(layer.Refusals);

        var ir = layer.Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;

        Assert.Contains("SCOIL HX_S0_L002 := HX_S0_Start AND DB_Unit.Armed AND DB_Unit.Alarm", ir, StringComparison.Ordinal);
        Assert.Contains("RCOIL HX_S0_L002 := NOT HX_S0_Start", ir, StringComparison.Ordinal);
        Assert.DoesNotContain("SCOIL HX_S0_L002 := DB_Unit.Alarm", ir, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The refusal is still reachable from a binding too</b>, for the case that remains inexpressible:
    /// a slot claiming no start gate (D37) has no per-index level to arm on. <i>An unreachable refusal is
    /// worse than no refusal — the system looks like it has a guard.</i>
    /// </summary>
    [Fact]
    public void And_the_REFUSAL_is_still_reachable_from_a_binding_when_the_slot_has_no_start_gate()
    {
        var layer = GenerateFrom(ReArming, startCondition: null);

        Assert.False(layer.Generated);
        Assert.Contains(layer.Refusals, r => r.Contains("CANNOT EXPRESS IT ON THIS SLOT", StringComparison.Ordinal));
    }

    /// <summary>The same binding, with the two latch-shaping flags the capability turns on.</summary>
    private static readonly string ReArming = Binding.Replace(
        "\"transient\": true",
        "\"transient\": true, \"rearmsEachIndex\": true, \"armedBy\": \"DB_Unit.Armed\"",
        StringComparison.Ordinal);

    private static CopyLayerResult GenerateFrom(string bindingJson, string? startCondition)
    {
        var sources = BindingDocument.Read(bindingJson).Slots![0].ResultSources!.Select(GateCli.ToMirroredSignal).ToArray();

        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 1000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 1000) / 2),
            new[] { new SlotRequest("S0", 2, 3) }));

        return CopyLayerGenerator.Generate(
            map.Map!,
            new[] { new SlotBinding("S0", new[] { new MirroredSignal("HX_In", MirrorValueType.Int) }, startCondition, sources) },
            new CopyLayerNaming(BlockNumber: 9000),
            new BuildStamp(1));
    }
}
