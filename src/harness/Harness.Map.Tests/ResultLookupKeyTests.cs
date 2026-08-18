using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE RESULT BAND HAS TWO LOOKUPS AND THEY USE DIFFERENT KEYS ON PURPOSE.</b>
///
/// <para><see cref="SlotBinding.ResultRegisterOf"/> and <see cref="SlotBinding.ResultSignal"/> answer the
/// question <i>"a vector cited this name — which register is it?"</i>, so they join on
/// <see cref="MirroredSignal.JoinKey"/>. <see cref="SlotBinding.ResultSignalByTag"/> answers the
/// generator's question <i>"I am about to emit this TAG into a rung — what is it?"</i>.</para>
///
/// <para><b>Until 2026-08-17 the first pair joined on the TAG</b>, which is the third independent
/// derivation of this join to be found wrong — <c>MirrorValueFit</c> and <c>LoopRun.ToWireVector</c> were
/// both corrected on 2026-08-14. The consequence measured on JOB9004's first live wave: the stimulus reached the
/// block and <b>every observation resolved to no register at all</b>.</para>
///
/// <para><b>Every fixture here states a spec name that DIFFERS from the tag</b>, because the two keys are
/// indistinguishable wherever they coincide — and on the real deliverable the only signal that resolved
/// was the one where they did.</para>
/// </summary>
public class ResultLookupKeyTests
{
    private static readonly CopyLayerNaming Naming = new(BlockNumber: 900);
    private static readonly BuildStamp Stamp = new(0xA93F2C71);

    private const string Tag = "iDB_Widget.IO.Fault";
    private const string Spec = "SPEC.FaultAlarm";

    private static SlotBinding Binding(params MirroredSignal[] sources) => new(
        "S0",
        MirroredSignal.Ints("DB_Unit.Setpoint"),
        "DB_Unit.StartCmd",
        sources);

    // ---------------------------------------------------------------------------------------------
    // The cited-name lookup
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_REGISTER_LOOKUP_JOINS_ON_THE_SPEC_NAME_AND_THE_TAG_IS_NOT_ALSO_TRIED()
    {
        var binding = Binding(
            new MirroredSignal("DB_Unit.Elapsed", MirrorValueType.Time, SpecName: "SPEC.Elapsed"),
            new MirroredSignal(Tag, MirrorValueType.Bool, SpecName: Spec));

        // The offset is the running REGISTER sum, so the Bool after a Time sits at 2, not 1.
        Assert.Equal(2, binding.ResultRegisterOf(Spec));

        // *** THE TAG IS NOT AN ALTERNATIVE KEY. *** Accepting both would make `specName` decorative and
        // let two vocabularies address one signal, which is how a rename stops being detectable.
        Assert.Equal(-1, binding.ResultRegisterOf(Tag));

        // -1, never 0 — 0 is a real offset and a caller that cannot tell them apart reads the first
        // register for every signal it does not have.
        Assert.Equal(-1, binding.ResultRegisterOf("SPEC.NotHere"));
    }

    [Fact]
    public void THE_TYPE_LOOKUP_JOINS_ON_THE_SAME_KEY_because_the_two_are_read_together_on_every_observation()
    {
        // A pair that joined on different keys would return a register for one name and a TYPE for
        // another — which decodes a real register with the wrong element and yields a plausible value.
        var binding = Binding(new MirroredSignal(Tag, MirrorValueType.Bool, SpecName: Spec));

        Assert.Equal(MirrorValueType.Bool, binding.ResultSignal(Spec)!.Type);
        Assert.Null(binding.ResultSignal(Tag));
    }

    [Fact]
    public void A_BINDING_THAT_STATES_NO_SPEC_NAME_STILL_RESOLVES_BY_TAG_because_JoinKey_falls_back()
    {
        // The unaffected case, asserted as deliberately as the changed one.
        var binding = Binding(new MirroredSignal(Tag, MirrorValueType.Bool));

        Assert.Equal(0, binding.ResultRegisterOf(Tag));
        Assert.NotNull(binding.ResultSignal(Tag));
    }

    // ---------------------------------------------------------------------------------------------
    // The generator's lookup — the other key, and it must stay the other key
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_GENERATORS_LOOKUP_IS_BY_TAG_because_its_key_is_emitted_into_LAD_as_a_rung_source()
    {
        var binding = Binding(new MirroredSignal(Tag, MirrorValueType.Bool, SpecName: Spec));

        Assert.Same(binding.ResultSources[0], binding.ResultSignalByTag(Tag));
        Assert.Null(binding.ResultSignalByTag(Spec));

        // The latch band is keyed the same way, which is WHY the generator needs a by-tag lookup at all.
        var latched = Binding(new MirroredSignal(Tag, MirrorValueType.Bool, SpecName: Spec, Transient: true));
        Assert.True(latched.LatchRegisterOffsets.ContainsKey(Tag));
    }

    [Fact]
    public void A_PHASE_ARMED_LATCH_ON_A_SPEC_NAMED_SIGNAL_STILL_EMITS_THE_TAG_and_stays_phase_armed()
    {
        // 🔴 *** THE REGRESSION THIS PAIR OF LOOKUPS EXISTS TO PREVENT. *** If the generator's latch pass
        // were moved onto the cited name, `signal` would come back NULL for every spec-named signal —
        // `is not { PhaseArmed: true }` would then be TRUE, and a re-arming latch would silently be emitted
        // in its ONE-SHOT form. That compiles, deploys, reads plausibly, and makes index 2 read index 1's
        // firing. It is the exact failure the phase-armed throw further down was written for, arrived at
        // from the other side.
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000),
            new[] { new SlotRequest("S0", 1, 4) })).Require();

        var result = CopyLayerGenerator.Generate(
            map,
            Binding(new MirroredSignal(Tag, MirrorValueType.Bool, SpecName: Spec,
                Transient: true, RearmsEachIndex: true, ArmedBy: "iDB_Model.Phase.Armed")),
            Naming, Stamp);

        Assert.True(result.Generated);
        var ir = result.Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;

        // The rung reads the BLOCK'S TAG. A spec name here would be a coil reading a symbol no PLC tag
        // table declares.
        Assert.Contains(
            $"SCOIL HX_S0_L001 := HX_S0_Start AND iDB_Model.Phase.Armed AND {Tag}",
            ir, StringComparison.Ordinal);
        Assert.Contains("RCOIL HX_S0_L001 := NOT HX_S0_Start", ir, StringComparison.Ordinal);

        // *** AND THE ONE-SHOT FORM IS ABSENT. *** The assertion that separates "phase-armed" from
        // "happens to have a latch".
        Assert.DoesNotContain($"SCOIL HX_S0_L001 := {Tag}", ir, StringComparison.Ordinal);

        // The tag table's latch comment names the TAG too, since that is what a person reads in TIA.
        var table = result.Objects.Single(o => o.Kind == HarnessObjectKind.TagTable).Ir;
        Assert.Contains($"LATCH for '{Tag}', PHASE-ARMED", table, StringComparison.Ordinal);
    }
}
