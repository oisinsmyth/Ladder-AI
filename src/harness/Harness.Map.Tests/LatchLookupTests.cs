using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b><c>LatchRegisterOffsets</c> HAD NO CONSUMER OUTSIDE <c>CopyLayerGenerator</c> — the code that
/// EMITS the latch rungs. Nothing on the observe side ever asked for it.</b>
///
/// <para>So the phase-armed latches that were argued for, sized (+1 register each), generated, deployed
/// and read off the wire every poll were <b>unconsumed</b>, and <c>Latched</c> and <c>Sampled</c> were the
/// same thing at the wire: one read of the value register at completion. <see cref="SlotBinding.LatchRegisterOf"/>
/// and <see cref="SlotBinding.ArmRegisterOf"/> are the observe-side lookups that were missing, and these
/// tests are about the two things such a lookup gets wrong: <b>the KEY</b> (a vector cites the
/// specification's name; the latch table is keyed on the IR tag) and <b>the ABSENCE</b> (-1 must not be
/// mistakable for register 0, and must never be papered over with a fallback).</para>
/// </summary>
public class LatchLookupTests
{
    private const string Tag = "iDB_Unit.IO.OpenValve";
    private const string Spec = "DrainValve.Command";
    private const string ArmTag = "iDB_Stim.Stim.Armed";
    private const string ArmSpec = "VLV_Stim.Armed";

    /// <summary>
    /// The JOB9004 valve slot in miniature: a phase-armed transient whose arm signal is itself published in
    /// the same result band, beside two ordinary signals — and every spec name DIFFERS from its tag, which
    /// is the shape a fixture needs to be able to tell the two keys apart at all.
    /// </summary>
    private static SlotBinding Binding() => new(
        "VLV",
        MirroredSignal.Ints("iDB_Stim.Stim.Profile"),
        "iDB_Stim.Stim.Start",
        new[]
        {
            new MirroredSignal(ArmTag, MirrorValueType.Bool, SpecName: ArmSpec),
            new MirroredSignal("iDB_Unit.IO.Telemetry", MirrorValueType.Int, SpecName: "Valve.Telemetry"),
            new MirroredSignal(Tag, MirrorValueType.Bool, SpecName: Spec,
                Transient: true, RearmsEachIndex: true, ArmedBy: ArmTag),
        });

    [Fact]
    public void THE_LATCH_RESOLVES_FROM_THE_NAME_A_VECTOR_CITES_not_from_the_tag()
    {
        var binding = Binding();

        // Three value registers (0,1,2), then the latch band starts at 3.
        Assert.Equal(2, binding.ResultRegisterOf(Spec));
        Assert.Equal(3, binding.LatchRegisterOf(Spec));

        // *** AND THEY ARE DIFFERENT REGISTERS. *** The whole defect was reading the first where the
        // author asked for the second.
        Assert.NotEqual(binding.ResultRegisterOf(Spec), binding.LatchRegisterOf(Spec));
    }

    [Fact]
    public void THE_IR_TAG_IS_NOT_A_KEY_HERE_because_a_vector_never_cites_one()
    {
        // The mirror of ResultLookupKeyTests' rule, on the latch side. Asking by tag is the generator's
        // question and it has its own method for it (ResultSignalByTag); this one answers the vector's.
        Assert.Equal(-1, Binding().LatchRegisterOf(Tag));
    }

    [Fact]
    public void A_SIGNAL_WITH_NO_LATCH_ANSWERS_MINUS_ONE_AND_NOT_ITS_VALUE_REGISTER()
    {
        var binding = Binding();

        // *** THE FORBIDDEN FALLBACK, PINNED. *** `Valve.Telemetry` is a real, carried signal at register
        // 1; it simply has no latch. Returning 1 here would answer a `Latched` expectation from the value
        // register — which is precisely how `Latched` became a synonym for `Sampled`.
        Assert.Equal(1, binding.ResultRegisterOf("Valve.Telemetry"));
        Assert.Equal(-1, binding.LatchRegisterOf("Valve.Telemetry"));

        // A name the binding does not carry at all answers the same way, and -1 rather than 0 because 0 is
        // a real offset.
        Assert.Equal(-1, binding.LatchRegisterOf("SPEC.NotHere"));
    }

    [Fact]
    public void THE_ARM_REGISTER_IS_THE_ONE_CARRYING_THE_SIGNALS_OWN_DECLARED_ARM_TAG()
    {
        var binding = Binding();

        // The arm tag is published in the band at register 0, and the lookup finds it FROM the transient
        // signal's own ArmedBy — resolving the cited name to the signal, then the signal's arm tag to a
        // register.
        Assert.Equal(0, binding.ArmRegisterOf(Spec));
        Assert.Equal(0, binding.ResultRegisterOf(ArmSpec));
    }

    [Fact]
    public void A_SIGNAL_THAT_DECLARED_NO_ARM_WINDOW_GETS_NO_ARM_REGISTER_even_when_a_sibling_declares_one()
    {
        // 🔴 *** THE INFERENCE THAT IS DELIBERATELY NOT MADE. *** Several signals in a binding commonly
        // name the same arm tag, and it is tempting to treat that tag as the SLOT's window and apply it to
        // signals that declared none. That would be an inference about a signal from its neighbours, and
        // an inferred window is how an observation acquires a confidence it was never given.
        Assert.Equal(-1, Binding().ArmRegisterOf("Valve.Telemetry"));
    }

    [Fact]
    public void AN_ARM_TAG_THE_SLOT_DOES_NOT_PUBLISH_IS_UNREADABLE_and_says_so_with_minus_one()
    {
        // A binding may legitimately arm a latch from a tag it never mirrors: the latch still works on the
        // controller and the PC simply cannot see the window. That is a limit, not a failure, and it must
        // read as UNKNOWN rather than as an open window.
        var binding = new SlotBinding(
            "VLV",
            Array.Empty<MirroredSignal>(),
            "iDB_Stim.Stim.Start",
            new[]
            {
                new MirroredSignal(Tag, MirrorValueType.Bool, SpecName: Spec,
                    Transient: true, RearmsEachIndex: true, ArmedBy: "iDB_Stim.Stim.ArmedButNotMirrored"),
            });

        // One value register at 0, so the latch band starts at 1 — the latch is fine; only the WINDOW is
        // unreadable, and the two facts are answered separately.
        Assert.Equal(0, binding.ResultRegisterOf(Spec));
        Assert.Equal(1, binding.LatchRegisterOf(Spec));
        Assert.Equal(-1, binding.ArmRegisterOf(Spec));
    }

    [Fact]
    public void THE_LATCH_BAND_SITS_AFTER_EVERY_VALUE_INCLUDING_A_WIDE_ONE()
    {
        // A Time occupies two registers, so a latch offset computed from the LIST INDEX rather than the
        // running width would land inside the values. Pinned here because the latch lookup is a new
        // consumer of that arithmetic.
        var binding = new SlotBinding(
            "S0",
            Array.Empty<MirroredSignal>(),
            "Start",
            new[]
            {
                new MirroredSignal("Unit.Elapsed", MirrorValueType.Time, SpecName: "SPEC.Elapsed"),
                new MirroredSignal("Unit.Pulse", MirrorValueType.Bool, SpecName: "SPEC.Pulse", Transient: true),
            });

        Assert.Equal(0, binding.ResultRegisterOf("SPEC.Elapsed"));
        Assert.Equal(2, binding.ResultRegisterOf("SPEC.Pulse"));
        Assert.Equal(3, binding.LatchRegisterOf("SPEC.Pulse"));
        Assert.Equal(4, binding.ResultRegistersNeeded);
    }
}
