using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>A WAVE SET WIDER THAN ONE SLOT, AND THE TWO THINGS THAT MADE ONE INEXPRESSIBLE.</b>
///
/// <para><b>1. The slot id is a cross-reference key AND a tag fragment, and those alphabets differ.</b>
/// The deliverable's conformance vectors cite <c>SLOT-HBA-RAISE</c>; a PLC tag name cannot hold a
/// hyphen. Measured 2026-08-14 on the committed coordinator binding: <c>harness-run --generate-only</c>
/// returned <c>slot id 'SLOT-HBA-ALL' is not a plain identifier</c> and emitted nothing — <b>so this was
/// never a six-slot problem, it was a ONE-slot problem</b>, and no copy layer for those 27 vectors could
/// be generated at any width.</para>
///
/// <para><b>2. Several slots on one block WRITE the same tags, and the generator emitted every writer
/// and said nothing.</b> Measured the same day on two slots bound to one skeleton block: two
/// <c>MOVE … =&gt; Demo_Step</c> rungs and two <c>COIL Demo_Start := …</c> rungs, in one FC, executed in
/// order every scan. The last slot silently owns the block; the loop still ran and still produced a
/// package per slot. <b>Sharing an OBSERVATION is the opposite case and is admitted</b> — the copy layer
/// only reads a result source, and every slot has its own register band.</para>
/// </summary>
public class MultiSlotBindingTests
{
    private static readonly CopyLayerNaming Naming = new(BlockNumber: 900);
    private static readonly BuildStamp Stamp = new(0xA93F2C71);

    private static RegisterMap Map(params string[] slotIds) =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000),
            slotIds.Select(id => new SlotRequest(id, 3, 2)).ToArray())).Require();

    /// <summary>One slot's binding with its OWN stimulus, sharing whatever observations it is given.</summary>
    private static SlotBinding Lane(string slotId, string unit, IReadOnlyList<MirroredSignal>? sources = null) =>
        new(slotId,
            MirroredSignal.Ints($"{unit}.Setpoint", $"{unit}.Mode"),
            $"{unit}.StartCmd",
            sources ?? MirroredSignal.Ints("DB_Shared.Actual", "DB_Shared.State"));

    // ---------------------------------------------------------------------------------------------
    // 1. THE SLOT ID'S TWO ALPHABETS
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_HYPHENATED_SLOT_ID_IS_ADDRESSABLE_and_only_its_TAG_FRAGMENT_is_transliterated()
    {
        // The exact id in the committed coordinator binding. Before this it was a hard refusal and the
        // deliverable's copy layer could not be generated at all.
        var result = CopyLayerGenerator.Generate(Map("SLOT-HBA-ALL"), Lane("SLOT-HBA-ALL", "DB_A"), Naming, Stamp);

        Assert.True(result.Generated, string.Join(" | ", result.Refusals));

        var plan = result.Require();

        // The IDENTITY is untouched — a vector's `slot` field still joins to it.
        Assert.Equal("SLOT-HBA-ALL", plan.Bindings[0].SlotId);

        // The TAG FRAGMENT is a plain identifier, because a tag name cannot be anything else.
        Assert.Contains(plan.Tags, t => t.Name == "HX_SLOT_HBA_ALL_Start");
        Assert.DoesNotContain(plan.Tags, t => t.Name.Contains('-', StringComparison.Ordinal));
    }

    [Fact]
    public void The_derivation_is_reported_for_EVERY_slot_including_the_ones_it_did_not_change()
    {
        // A scope that only speaks when it changed something cannot be told from one that has stopped
        // running, so the unchanged slot is reported too.
        var plan = CopyLayerGenerator.Generate(
            Map("SLOT-HBA-ALL", "Plain"),
            new[] { Lane("SLOT-HBA-ALL", "DB_A"), Lane("Plain", "DB_B") },
            Naming, Stamp).Require();

        Assert.Equal(2, plan.SlotTokens.Count);
        Assert.Contains(plan.SlotTokens, d => d.SlotId == "SLOT-HBA-ALL" && d.Token == "SLOT_HBA_ALL" && d.Transliterated);
        Assert.Contains(plan.SlotTokens, d => d.SlotId == "Plain" && d.Token == "Plain" && !d.Transliterated);

        // And the summary says so on a run where nothing changed, rather than falling silent.
        Assert.Contains("0 transliterated", SlotTagToken.Describe(new[] { SlotTagToken.Derive("Plain") }), StringComparison.Ordinal);
    }

    [Fact]
    public void TWO_IDS_THAT_DIFFER_ONLY_IN_PUNCTUATION_ARE_REFUSED_because_they_would_ALIAS_each_others_registers()
    {
        // The map allocates them disjoint addresses and hashes cleanly; the aliasing would be in the
        // NAMES, which the map's own disjointness check cannot see.
        var result = CopyLayerGenerator.Generate(
            Map("SLOT-A", "SLOT_A"),
            new[] { Lane("SLOT-A", "DB_A"), Lane("SLOT_A", "DB_B") },
            Naming, Stamp);

        Assert.False(result.Generated);
        var refusal = Assert.Single(result.Refusals, r => r.Contains("transliterate", StringComparison.Ordinal));
        Assert.Contains("'SLOT-A'", refusal, StringComparison.Ordinal);
        Assert.Contains("'SLOT_A'", refusal, StringComparison.Ordinal);
        Assert.Contains("SLOT_A", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void A_SLOT_ID_OF_PUNCTUATION_ALONE_IS_REFUSED_rather_than_transliterated_to_underscores()
    {
        var result = CopyLayerGenerator.Generate(Map("---"), Lane("---", "DB_A"), Naming, Stamp);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("no letter or digit", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------
    // 2. SHARED OBSERVATION — ADMITTED, AND THE CASE THE DELIVERABLE NEEDS
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SIX_SLOTS_OBSERVING_THE_SAME_SIGNALS_GENERATE_and_each_keeps_ITS_OWN_RESULT_BAND()
    {
        var ids = new[] { "SLOT-HBA-RAISE", "SLOT-HBA-CLEAR", "SLOT-HBA-RESET", "SLOT-HBA-STARTUP", "SLOT-HBA-LATCH", "SLOT-HBA-PAIR" };
        var map = Map(ids);

        var result = CopyLayerGenerator.Generate(
            map, ids.Select((id, i) => Lane(id, $"DB_Unit{i}")).ToArray(), Naming, Stamp);

        Assert.True(result.Generated, string.Join(" | ", result.Refusals));
        var plan = result.Require();

        // Six result bands, disjoint, in map order — the property the register model must keep.
        var bands = map.Slots.Select(s => s.Result).ToArray();
        Assert.Equal(6, bands.Length);
        Assert.Equal(6, bands.Select(b => b.Register).Distinct().Count());
        Assert.All(bands, b => Assert.Equal(map.ResultRegistersPerSlot, b.Length));

        // Six sets of result tags, one per slot, none of them shared.
        foreach (var id in ids)
            Assert.Contains(plan.Tags, t => t.Name == $"HX_{SlotTagToken.For(id)}_R000");

        // And the sharing is REPORTED rather than silently absorbed.
        Assert.Contains(plan.SharedObservations, s => s.StartsWith("DB_Shared.Actual", StringComparison.Ordinal));
    }

    [Fact]
    public void The_shared_observation_report_is_EMPTY_and_PRESENT_when_no_signal_is_shared()
    {
        // The unaffected case, tested as deliberately as the shared one: a report that only ever appears
        // populated teaches its reader that an empty one means it did not run.
        var plan = CopyLayerGenerator.Generate(
            Map("A", "B"),
            new[]
            {
                Lane("A", "DB_A", MirroredSignal.Ints("DB_A.Actual", "DB_A.State")),
                Lane("B", "DB_B", MirroredSignal.Ints("DB_B.Actual", "DB_B.State")),
            },
            Naming, Stamp).Require();

        Assert.Empty(plan.SharedObservations);
        Assert.Equal(2, plan.SlotTokens.Count);
    }

    [Fact]
    public void LATCHES_STAY_IN_THEIR_OWN_BAND_AFTER_THE_VALUES_on_a_wide_wave_set()
    {
        // Adding a latch must move no VALUE offset — the client's arithmetic is the value offsets, and
        // this is the property the six-slot change had to preserve.
        var sources = new[]
        {
            new MirroredSignal("DB_Shared.Actual", MirrorValueType.Int, SpecName: "Actual"),
            new MirroredSignal("DB_Shared.Pulse", MirrorValueType.Bool, SpecName: "Pulse", Transient: true),
        };

        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            new[] { new SlotRequest("A", 3, 3), new SlotRequest("B", 3, 3) })).Require();

        var bindings = new[] { Lane("A", "DB_A", sources), Lane("B", "DB_B", sources) };
        var plan = CopyLayerGenerator.Generate(map, bindings, Naming, Stamp).Require();

        foreach (var binding in bindings)
        {
            // Values at 0 and 1; the latch band starts at 2, after them.
            Assert.Equal(new[] { 0, 1 }, binding.ResultRegisterOffsets);
            Assert.Equal(2, Assert.Single(binding.LatchRegisterOffsets).Value);
            Assert.Equal(3, binding.ResultRegistersNeeded);
        }

        Assert.Contains(plan.Tags, t => t.Name == "HX_A_L002");
        Assert.Contains(plan.Tags, t => t.Name == "HX_B_L002");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. SHARED STIMULUS — REFUSED, BY NAME
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TWO_SLOTS_WRITING_ONE_TAG_ARE_REFUSED_naming_the_tag_and_both_slots()
    {
        // The measured silent case: both slots bound to one block's stimulus and one block's start.
        var shared = new SlotBinding[]
        {
            Lane("A", "DB_Unit"),
            Lane("B", "DB_Unit"),
        };

        var result = CopyLayerGenerator.Generate(Map("A", "B"), shared, Naming, Stamp);

        Assert.False(result.Generated);

        var start = Assert.Single(result.Refusals, r => r.Contains("DB_Unit.StartCmd", StringComparison.Ordinal));
        Assert.Contains("slot 'A'", start, StringComparison.Ordinal);
        Assert.Contains("slot 'B'", start, StringComparison.Ordinal);
        Assert.Contains("the start condition", start, StringComparison.Ordinal);

        // The stimulus registers too, not only the start bool.
        Assert.Contains(result.Refusals, r => r.Contains("DB_Unit.Setpoint", StringComparison.Ordinal) && r.Contains("a vector target", StringComparison.Ordinal));

        // And it says what to do instead, in the two forms that actually work.
        Assert.Contains(result.Refusals, r => r.Contains("ONE slot per wave set", StringComparison.Ordinal));
    }

    [Fact]
    public void ONE_SLOT_WRITING_THE_SAME_TAG_TWICE_IS_REFUSED_too_and_reads_differently()
    {
        var result = CopyLayerGenerator.Generate(
            Map("A"),
            new SlotBinding("A", MirroredSignal.Ints("DB_A.Setpoint", "DB_A.Setpoint"), "DB_A.StartCmd",
                MirroredSignal.Ints("DB_A.Actual", "DB_A.State")),
            Naming, Stamp);

        Assert.False(result.Generated);
        var refusal = Assert.Single(result.Refusals, r => r.Contains("DB_A.Setpoint", StringComparison.Ordinal));
        Assert.Contains("One slot writes it twice", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void A_SLOT_MAY_STILL_OBSERVE_A_SIGNAL_ANOTHER_SLOT_DRIVES_because_a_read_is_not_a_write()
    {
        // The unaffected case for the multi-writer refusal: B watches the tag A commands. Nothing in the
        // emitted layer writes it twice, so the refusal must NOT fire — a gate that refuses cases outside
        // its scope is noise, and noise gets switched off.
        var result = CopyLayerGenerator.Generate(
            Map("A", "B"),
            new[]
            {
                Lane("A", "DB_A"),
                new SlotBinding("B", MirroredSignal.Ints("DB_B.Setpoint", "DB_B.Mode"), "DB_B.StartCmd",
                    MirroredSignal.Ints("DB_A.Setpoint", "DB_Shared.State")),
            },
            Naming, Stamp);

        Assert.True(result.Generated, string.Join(" | ", result.Refusals));
    }

    // ---------------------------------------------------------------------------------------------
    // 4. THE ONE-SLOT CASE, WHICH MUST BE UNTOUCHED
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_ONE_SLOT_BINDING_IS_UNTOUCHED_by_both_cross_slot_checks()
    {
        var result = CopyLayerGenerator.Generate(Map("S0"), Lane("S0", "DB_Unit"), Naming, Stamp);

        Assert.True(result.Generated, string.Join(" | ", result.Refusals));
        var plan = result.Require();

        Assert.Empty(plan.SharedObservations);
        Assert.Single(plan.SlotTokens);
        Assert.False(plan.SlotTokens[0].Transliterated);
    }
}
