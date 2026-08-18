using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// The minimal copy-layer generator, build-plan item 2.2.
///
/// The IR text asserted here is not decoration: it was round-tripped through
/// <c>converter to-xml</c> / <c>to-ir --no-sidecar</c> and came back byte-identical, so these
/// expectations are a form the real toolchain accepts, not a form that merely looks plausible.
/// </summary>
public class CopyLayerGeneratorTests
{
    private static readonly CopyLayerNaming Naming = new(BlockNumber: 900);

    /// <summary>
    /// A fixed stamp, so the golden IR below is stable. Real callers derive it with
    /// <see cref="BuildStamp.Of"/>; this is the spec's own worked literal (section 9).
    /// </summary>
    private static readonly BuildStamp Stamp = new(0xA93F2C71);

    private static RegisterMap OneSlot(int vector = 3, int result = 2) =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000),
            new[] { new SlotRequest("S0", vector, result) })).Require();

    private static SlotBinding Binding(
        string? start = "DB_Unit.StartCmd",
        IReadOnlyList<MirroredSignal>? targets = null,
        IReadOnlyList<MirroredSignal>? sources = null) => new(
        "S0",
        targets ?? MirroredSignal.Ints("DB_Unit.Setpoint", "DB_Unit.Mode"),
        start,
        sources ?? MirroredSignal.Ints("DB_Unit.Actual", "DB_Unit.State"));

    private static CopyLayerResult Generate(RegisterMap? map = null, SlotBinding? binding = null) =>
        CopyLayerGenerator.Generate(map ?? OneSlot(), binding ?? Binding(), Naming, Stamp);

    // ---------------------------------------------------------------------------------------------
    // What it generates
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Generates_the_six_networks_the_minimal_layer_is_defined_as()
    {
        var plan = Generate().Require();

        Assert.Equal(
            new[]
            {
                CopyLayerNetworkKind.Version,
                CopyLayerNetworkKind.ScanCounter,
                CopyLayerNetworkKind.VectorIn,
                CopyLayerNetworkKind.StartBool,
                CopyLayerNetworkKind.StartEcho,
                CopyLayerNetworkKind.ResultsOut,
            },
            plan.Networks.Select(n => n.Kind));

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, plan.Networks.Select(n => n.Number));
    }

    [Fact]
    public void Emits_exactly_one_tag_table_and_one_block()
    {
        var result = Generate();

        Assert.Equal(2, result.Objects.Count);
        Assert.Single(result.Objects, o => o.Kind == HarnessObjectKind.TagTable);
        Assert.Single(result.Objects, o => o.Kind == HarnessObjectKind.Block);
    }

    [Fact]
    public void Mirror_tags_carry_the_addresses_the_map_allocated()
    {
        var map = OneSlot();
        var plan = Generate(map).Require();
        var geometry = map.Geometry;
        var slot = map.Slots[0];

        Assert.Equal(geometry.DoubleWordAddressOf(map.ScanCounter.Register),
            plan.Tags.Single(t => t.Name == "HX_ScanCount").Address);

        Assert.Equal(geometry.BitAddressOf(slot.StartBoolRegister, slot.StartBitInRegister),
            plan.Tags.Single(t => t.Name == "HX_S0_Start").Address);

        Assert.Equal(geometry.WordAddressOf(slot.Vector.Register),
            plan.Tags.Single(t => t.Name == "HX_S0_V000").Address);

        Assert.Equal(geometry.WordAddressOf(slot.Result.Register + 1),
            plan.Tags.Single(t => t.Name == "HX_S0_R001").Address);
    }

    [Fact]
    public void Declares_only_the_registers_it_wires_not_the_whole_fixed_size_region()
    {
        // The slot is allocated 8 vector registers; the binding wires 2. The client addresses the mirror
        // by register number, so the other 6 would be symbols nothing reads and nothing writes.
        var plan = Generate(OneSlot(vector: 8, result: 8)).Require();

        Assert.Equal(2, plan.Tags.Count(t => t.Name.Contains("_V0", StringComparison.Ordinal)));
        Assert.Equal(2, plan.Tags.Count(t => t.Name.Contains("_R0", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_generated_block_is_this_exact_IR_text()
    {
        var block = Generate().Objects.Single(o => o.Kind == HarnessObjectKind.Block);

        Assert.Equal(
            """
            BLOCK FC FC_HarnessCopyLayer
            ROOTID 0
            NUMBER 900
            LANGUAGE LAD
            TITLE "Harness copy layer"

            INTERFACE
              INPUT
              OUTPUT
              CONSTANT

            NETWORK 1 "Program version"
              MOVE(EN := TRUE, IN := 16#A93F2C71) => HX_ProgramVersion

            NETWORK 2 "Free-running scan counter"
              ADD(EN := TRUE, IN1 := HX_ScanCount, IN2 := 1) => HX_ScanCount

            NETWORK 3 "Vector in - slot S0 - Int"
              MOVE(EN := TRUE, IN := HX_S0_V000) => DB_Unit.Setpoint
              MOVE(EN := TRUE, IN := HX_S0_V001) => DB_Unit.Mode

            NETWORK 4 "Start bool - slot S0"
              COIL DB_Unit.StartCmd := HX_S0_Start

            NETWORK 5 "Start echo - slot S0"
              SCOIL HX_S0_Ran := DB_Unit.StartCmd

            NETWORK 6 "Results out - slot S0 - Int"
              MOVE(EN := TRUE, IN := DB_Unit.Actual) => HX_S0_R000
              MOVE(EN := TRUE, IN := DB_Unit.State) => HX_S0_R001

            """.ReplaceLineEndings("\n"),
            block.Ir);
    }

    [Fact]
    public void The_generated_tag_table_is_this_exact_IR_text()
    {
        var table = Generate().Objects.Single(o => o.Kind == HarnessObjectKind.TagTable);

        Assert.Equal(
            """
            TAGTABLE HarnessMirror
              ROOTID 0
              TAGS
                HX_ProgramVersion 1 : DWord @ %MD4000 ACCESSIBLE VISIBLE WRITABLE COMMENT "Build stamp of the downloaded IR set. Present only if this code is running."
                HX_ScanCount 4 : DInt @ %MD4004 ACCESSIBLE VISIBLE WRITABLE COMMENT "Free-running scan counter. Wraps; scan stamps are differences from the start edge."
                HX_S0_Start 7 : Bool @ %M4009.0 ACCESSIBLE VISIBLE WRITABLE COMMENT "Start bool. Its rising edge is the test's T=0."
                HX_S0_Ran A : Bool @ %M4011.0 ACCESSIBLE VISIBLE WRITABLE COMMENT "Latched: the block's own start condition was seen high. Cleared by the client at inert."
                HX_S0_V000 D : Int @ %MW4012 ACCESSIBLE VISIBLE WRITABLE COMMENT "Vector register 0."
                HX_S0_V001 10 : Int @ %MW4014 ACCESSIBLE VISIBLE WRITABLE COMMENT "Vector register 1."
                HX_S0_R000 13 : Int @ %MW4018 ACCESSIBLE VISIBLE WRITABLE COMMENT "Result register 0."
                HX_S0_R001 16 : Int @ %MW4020 ACCESSIBLE VISIBLE WRITABLE COMMENT "Result register 1."

            """.ReplaceLineEndings("\n"),
            table.Ir);
    }

    [Fact]
    public void One_statement_kind_per_network_so_the_IR_ordering_rule_is_unreachable()
    {
        // The parser requires statements grouped by kind in a fixed order, and that order mirrors real
        // rung-execution order: get it wrong and the consumer silently reads the producer's PREVIOUS-scan
        // value with no error anywhere. One kind per network makes the rule unreachable, not merely met.
        var block = Generate().Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;

        foreach (var network in block.Split("NETWORK ").Skip(1))
        {
            var kinds = network.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith('"') && !l.Contains('"'))
                .Select(l => l.Split('(')[0].Split(' ')[0])
                .Distinct()
                .ToArray();

            Assert.Single(kinds);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // D37: the absence of a start gate is a CLAIM, not a blank
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_binding_with_no_start_condition_omits_the_network_and_records_the_claim()
    {
        var plan = Generate(binding: Binding(start: null)).Require();

        Assert.True(plan.NoStartGateAsserted);
        Assert.DoesNotContain(plan.Networks, n => n.Kind == CopyLayerNetworkKind.StartBool);
        Assert.DoesNotContain(plan.Tags, t => t.Name.EndsWith("_Start", StringComparison.Ordinal));
        Assert.Equal(new[] { 1, 2, 3, 4 }, plan.Networks.Select(n => n.Number));

        // No start gate means no echo either: there is no start condition to latch, and a slot that
        // published a "ran" bit driven by nothing would be exactly the false evidence X-E kills.
        Assert.DoesNotContain(plan.Networks, n => n.Kind == CopyLayerNetworkKind.StartEcho);
        Assert.DoesNotContain(plan.Tags, t => t.Name.EndsWith("_Ran", StringComparison.Ordinal));
    }

    [Fact]
    public void A_binding_with_a_start_condition_does_not_assert_the_absence()
    {
        Assert.False(Generate().Require().NoStartGateAsserted);
    }

    [Fact]
    public void A_pure_observation_binding_writes_nothing_and_is_still_generated()
    {
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            new[] { new SlotRequest("S0", 0, 2) })).Require();

        var plan = CopyLayerGenerator.Generate(map,
            new SlotBinding("S0", Array.Empty<MirroredSignal>(), "DB_Unit.StartCmd", MirroredSignal.Ints("DB_Unit.Actual")),
            Naming, Stamp).Require();

        Assert.DoesNotContain(plan.Networks, n => n.Kind == CopyLayerNetworkKind.VectorIn);
        Assert.Contains(plan.Networks, n => n.Kind == CopyLayerNetworkKind.ResultsOut);
    }

    // ---------------------------------------------------------------------------------------------
    // 3.1: more than one slot
    // ---------------------------------------------------------------------------------------------

    private static RegisterMap TwoSlots() => MapAllocator.Allocate(new WaveSetRequest(
        MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000),
        new[] { new SlotRequest("S0", 2, 2), new SlotRequest("S1", 2, 2) })).Require();

    private static CopyLayerResult TwoSlotLayer() => CopyLayerGenerator.Generate(
        TwoSlots(),
        new[]
        {
            new SlotBinding("S0", MirroredSignal.Ints("DB_A.Setpoint", "DB_A.Mode"), "DB_A.StartCmd", MirroredSignal.Ints("DB_A.Actual", "DB_A.State")),
            new SlotBinding("S1", MirroredSignal.Ints("DB_B.Level", "DB_B.Trip"), "DB_B.StartCmd", MirroredSignal.Ints("DB_B.Peak", "DB_B.Alarm")),
        },
        Naming, Stamp);

    [Fact]
    public void The_two_slot_block_is_this_exact_IR_text()
    {
        var block = TwoSlotLayer().Objects.Single(o => o.Kind == HarnessObjectKind.Block);

        Assert.Equal(
            """
            BLOCK FC FC_HarnessCopyLayer
            ROOTID 0
            NUMBER 900
            LANGUAGE LAD
            TITLE "Harness copy layer"

            INTERFACE
              INPUT
              OUTPUT
              CONSTANT

            NETWORK 1 "Program version"
              MOVE(EN := TRUE, IN := 16#A93F2C71) => HX_ProgramVersion

            NETWORK 2 "Free-running scan counter"
              ADD(EN := TRUE, IN1 := HX_ScanCount, IN2 := 1) => HX_ScanCount

            NETWORK 3 "Vector in - slot S0 - Int"
              MOVE(EN := TRUE, IN := HX_S0_V000) => DB_A.Setpoint
              MOVE(EN := TRUE, IN := HX_S0_V001) => DB_A.Mode

            NETWORK 4 "Start bool - slot S0"
              COIL DB_A.StartCmd := HX_S0_Start

            NETWORK 5 "Start echo - slot S0"
              SCOIL HX_S0_Ran := DB_A.StartCmd

            NETWORK 6 "Results out - slot S0 - Int"
              MOVE(EN := TRUE, IN := DB_A.Actual) => HX_S0_R000
              MOVE(EN := TRUE, IN := DB_A.State) => HX_S0_R001

            NETWORK 7 "Vector in - slot S1 - Int"
              MOVE(EN := TRUE, IN := HX_S1_V000) => DB_B.Level
              MOVE(EN := TRUE, IN := HX_S1_V001) => DB_B.Trip

            NETWORK 8 "Start bool - slot S1"
              COIL DB_B.StartCmd := HX_S1_Start

            NETWORK 9 "Start echo - slot S1"
              SCOIL HX_S1_Ran := DB_B.StartCmd

            NETWORK 10 "Results out - slot S1 - Int"
              MOVE(EN := TRUE, IN := DB_B.Peak) => HX_S1_R000
              MOVE(EN := TRUE, IN := DB_B.Alarm) => HX_S1_R001

            """.ReplaceLineEndings("\n"),
            block.Ir);
    }

    [Fact]
    public void The_two_slot_tag_table_is_the_IR_the_converter_round_trips()
    {
        var table = TwoSlotLayer().Objects.Single(o => o.Kind == HarnessObjectKind.TagTable);

        Assert.Equal(
            """
            TAGTABLE HarnessMirror
              ROOTID 0
              TAGS
                HX_ProgramVersion 1 : DWord @ %MD4000 ACCESSIBLE VISIBLE WRITABLE COMMENT "Build stamp of the downloaded IR set. Present only if this code is running."
                HX_ScanCount 4 : DInt @ %MD4004 ACCESSIBLE VISIBLE WRITABLE COMMENT "Free-running scan counter. Wraps; scan stamps are differences from the start edge."
                HX_S0_Start 7 : Bool @ %M4009.0 ACCESSIBLE VISIBLE WRITABLE COMMENT "Start bool. Its rising edge is the test's T=0."
                HX_S0_Ran A : Bool @ %M4011.0 ACCESSIBLE VISIBLE WRITABLE COMMENT "Latched: the block's own start condition was seen high. Cleared by the client at inert."
                HX_S0_V000 D : Int @ %MW4012 ACCESSIBLE VISIBLE WRITABLE COMMENT "Vector register 0."
                HX_S0_V001 10 : Int @ %MW4014 ACCESSIBLE VISIBLE WRITABLE COMMENT "Vector register 1."
                HX_S0_R000 13 : Int @ %MW4020 ACCESSIBLE VISIBLE WRITABLE COMMENT "Result register 0."
                HX_S0_R001 16 : Int @ %MW4022 ACCESSIBLE VISIBLE WRITABLE COMMENT "Result register 1."
                HX_S1_Start 19 : Bool @ %M4009.1 ACCESSIBLE VISIBLE WRITABLE COMMENT "Start bool. Its rising edge is the test's T=0."
                HX_S1_Ran 1C : Bool @ %M4011.1 ACCESSIBLE VISIBLE WRITABLE COMMENT "Latched: the block's own start condition was seen high. Cleared by the client at inert."
                HX_S1_V000 1F : Int @ %MW4016 ACCESSIBLE VISIBLE WRITABLE COMMENT "Vector register 0."
                HX_S1_V001 22 : Int @ %MW4018 ACCESSIBLE VISIBLE WRITABLE COMMENT "Vector register 1."
                HX_S1_R000 25 : Int @ %MW4024 ACCESSIBLE VISIBLE WRITABLE COMMENT "Result register 0."
                HX_S1_R001 28 : Int @ %MW4026 ACCESSIBLE VISIBLE WRITABLE COMMENT "Result register 1."

            """.ReplaceLineEndings("\n"),
            table.Ir);
    }

    [Fact]
    public void The_two_slots_start_bits_are_different_bits_of_the_same_register()
    {
        // At two slots a wrong %M byte in BitAddressOf makes both commanded blocks fail to see their start
        // condition — a detectable non-event that the co-running log names — rather than one slot starting
        // another's test. That only becomes possible at nine slots, where bit 0 and bit 8 would swap.
        var plan = TwoSlotLayer().Require();

        Assert.Equal("%M4009.0", plan.Tags.Single(t => t.Name == "HX_S0_Start").Address);
        Assert.Equal("%M4009.1", plan.Tags.Single(t => t.Name == "HX_S1_Start").Address);
    }

    [Fact]
    public void Networks_are_generated_in_MAP_order_not_in_the_callers_binding_order()
    {
        // Slot ordinals decide addresses, so generating in binding order would let a reordered list
        // produce differently-numbered networks for one map — and the map hash would not move.
        var map = TwoSlots();
        var s0 = new SlotBinding("S0", MirroredSignal.Ints("A.v"), "A.s", MirroredSignal.Ints("A.r"));
        var s1 = new SlotBinding("S1", MirroredSignal.Ints("B.v"), "B.s", MirroredSignal.Ints("B.r"));

        var forwards = CopyLayerGenerator.Generate(map, new[] { s0, s1 }, Naming, Stamp).Objects;
        var backwards = CopyLayerGenerator.Generate(map, new[] { s1, s0 }, Naming, Stamp).Objects;

        Assert.Equal(forwards.Single(o => o.Kind == HarnessObjectKind.Block).Ir,
            backwards.Single(o => o.Kind == HarnessObjectKind.Block).Ir);
    }

    [Fact]
    public void Two_bindings_for_one_slot_are_refused()
    {
        var map = TwoSlots();
        var duplicate = new SlotBinding("S0", MirroredSignal.Ints("A.v"), "A.s", MirroredSignal.Ints("A.r"));

        var result = CopyLayerGenerator.Generate(map, new[] { duplicate, duplicate }, Naming, Stamp);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("bound twice", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------
    // 2.6: the version register
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_build_stamp_is_a_literal_in_the_code_not_a_value_written_from_outside()
    {
        // Section 9: the constant lives IN THE CODE, so it can only be present if that code is running.
        // A value the client wrote would confirm nothing at all about what the CPU is executing.
        var block = Generate().Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;

        Assert.Contains("MOVE(EN := TRUE, IN := 16#A93F2C71) => HX_ProgramVersion", block, StringComparison.Ordinal);
    }

    [Fact]
    public void The_version_register_is_a_DWord_across_the_maps_two_reserved_registers()
    {
        var map = OneSlot();
        var plan = Generate(map).Require();
        var tag = plan.Tags.Single(t => t.Name == "HX_ProgramVersion");

        Assert.Equal("DWord", tag.DataType);
        Assert.Equal(map.Geometry.DoubleWordAddressOf(map.Version.Register), tag.Address);
        Assert.Equal(2, map.Version.Length);
    }

    [Fact]
    public void A_zero_build_stamp_is_refused_because_unwritten_bit_memory_reads_as_zero()
    {
        var result = CopyLayerGenerator.Generate(OneSlot(), Binding(), Naming, default);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("build stamp is zero", StringComparison.Ordinal));
    }

    [Fact]
    public void Excising_a_slot_changes_the_build_stamp_while_leaving_the_map_hash_alone()
    {
        // Two properties that look contradictory and are not. The map hash must NOT move — that is what
        // keeps every client mirror valid across an excision (DB-6). The build stamp MUST, because it
        // names the IR set actually downloaded and excision changes it: "excision is a REGENERATION
        // STEP, not 'drop a file from the download list'". Computed at planning time, a stale stamp
        // makes every post-download check fail and look like a failed download.
        var map = OneSlot();
        var excised = map.WithSlotExcised("S0");

        Assert.Equal(map.MapHash, excised.MapHash);
        Assert.NotEqual(
            BuildStamp.Of(map, Binding(), Naming).Value,
            BuildStamp.Of(excised, Binding(), Naming).Value);
    }

    [Fact]
    public void The_build_stamp_moves_with_the_program_under_test_and_with_the_binding()
    {
        var map = OneSlot();
        var baseline = BuildStamp.Of(map, Binding(), Naming);

        Assert.NotEqual(baseline.Value, BuildStamp.Of(map, Binding(start: "DB_Unit.Other"), Naming).Value);
        Assert.NotEqual(baseline.Value, BuildStamp.Of(map, Binding(), Naming, new[]
        {
            new HarnessObject("FC_UnderTest", HarnessObjectKind.Block, "BLOCK FC FC_UnderTest\n"),
        }).Value);
    }

    [Fact]
    public void The_build_stamp_is_never_zero()
    {
        // Not a probabilistic hope: the derivation walks the digest for a non-zero word and throws if it
        // finds none, because a zero stamp cannot be told from bit memory that was never written.
        for (var i = 0; i < 64; i++)
        {
            var stamp = BuildStamp.Of(OneSlot(), Binding(start: $"DB_Unit.Start{i}"), Naming);
            Assert.NotEqual(0u, stamp.Value);
        }
    }

    [Fact]
    public void The_stamps_two_halves_reassemble_into_the_stamp()
    {
        var stamp = new BuildStamp(0xA93F2C71);

        Assert.Equal((ushort)0xA93F, stamp.HighWord);
        Assert.Equal((ushort)0x2C71, stamp.LowWord);
        Assert.Equal("16#A93F2C71", stamp.Literal);
    }

    // ---------------------------------------------------------------------------------------------
    // Refusals
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_map_slot_with_no_binding_is_refused()
    {
        // Phase 3 lifts phase 2's one-slot refusal, but not the requirement that every slot be bound: an
        // unbound slot is a mirror region nothing maintains, and its zeros are indistinguishable from a
        // result.
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            new[] { new SlotRequest("S0", 2, 2), new SlotRequest("S1", 2, 2) })).Require();

        var result = CopyLayerGenerator.Generate(map, Binding(), Naming, Stamp);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("Every slot must be bound", StringComparison.Ordinal));
    }

    [Fact]
    public void A_binding_naming_a_slot_the_map_does_not_hold_is_refused()
    {
        var result = CopyLayerGenerator.Generate(OneSlot(),
            new SlotBinding("S9", MirroredSignal.Ints("A"), null, MirroredSignal.Ints("B")), Naming, Stamp);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("not in the map", StringComparison.Ordinal));
    }

    [Fact]
    public void A_binding_wider_than_its_allocated_region_is_refused()
    {
        var narrow = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            new[] { new SlotRequest("S0", 1, 1) })).Require();

        var result = CopyLayerGenerator.Generate(narrow, Binding(), Naming, Stamp);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("vector target(s) needing", StringComparison.Ordinal));
        Assert.Contains(result.Refusals, r => r.Contains("result source(s) needing", StringComparison.Ordinal));
    }

    [Fact]
    public void A_binding_publishing_nothing_is_refused()
    {
        var result = CopyLayerGenerator.Generate(OneSlot(),
            new SlotBinding("S0", MirroredSignal.Ints("A"), null, Array.Empty<MirroredSignal>()), Naming, Stamp);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("publishes nothing", StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_block_number_is_refused_rather_than_defaulted()
    {
        // Hard rule 3: block numbers are not invented here. X-J reserves a range and the caller allocates.
        var result = CopyLayerGenerator.Generate(OneSlot(), Binding(), new CopyLayerNaming(), Stamp);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("block number", StringComparison.Ordinal));
    }

    [Fact]
    public void A_slot_id_that_is_not_an_identifier_STILL_YIELDS_ONLY_IDENTIFIER_TAG_NAMES()
    {
        // *** THE PROPERTY IS KEPT; THE MECHANISM MOVED (2026-08-14). *** This used to be a REFUSAL, and
        // the refusal was wrong in one direction: the slot id is also the cross-reference key a vector's
        // `slot` field must equal, and the deliverable's vectors cite `SLOT-HBA-RAISE`. Refusing every id
        // a tag name cannot hold made the coordinator's own single-slot binding ungeneratable — measured
        // on `SLOT-HBA-ALL`. The id is now kept and TRANSLITERATED for the tag fragment (SlotTagToken),
        // so what this test was actually protecting — no illegal character ever reaches a tag name — is
        // asserted directly rather than through a refusal that also blocked legitimate ids.
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            new[] { new SlotRequest("S 0", 2, 2) })).Require();

        var result = CopyLayerGenerator.Generate(map,
            new SlotBinding("S 0", MirroredSignal.Ints("A"), null, MirroredSignal.Ints("B")), Naming, Stamp);

        Assert.True(result.Generated, string.Join(" | ", result.Refusals));

        var plan = result.Require();

        Assert.Equal("S 0", plan.Bindings[0].SlotId);
        Assert.All(plan.Tags, t => Assert.True(SlotTagToken.IsPlainIdentifier(t.Name), $"tag '{t.Name}' is not a plain identifier"));
        Assert.Contains(plan.SlotTokens, d => d.SlotId == "S 0" && d.Token == "S_0" && d.Transliterated);
    }

    [Fact]
    public void A_refused_generation_yields_no_plan_no_objects_and_throws_on_Require()
    {
        var result = CopyLayerGenerator.Generate(OneSlot(), Binding(), new CopyLayerNaming(), Stamp);

        Assert.Null(result.Plan);
        Assert.Empty(result.Objects);
        Assert.Throws<InvalidOperationException>(() => result.Require());
    }

    // ---------------------------------------------------------------------------------------------
    // Bool mirroring — the defect a live TIA import found, and the refusal that replaces the default
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_BOOL_IS_COPIED_BY_A_COIL_AND_DECLARED_AT_A_BIT_ADDRESS()
    {
        // *** THE MEASURED DEFECT. *** Every mirror tag was hard-coded "Int" and every result rendered as
        // a plain MOVE. TIA answered "Data type Bool is not permitted here." on the first live import —
        // and every signal both conformance vector sets observe is a Bool, so the copy layer could not
        // mirror one asserted signal.
        var result = Generate(binding: Binding(sources: MirroredSignal.Bools("DB_Unit.Alarm", "DB_Unit.StopReq")));
        var plan = result.Require();

        var tag = plan.Tags.Single(t => t.Name == "HX_S0_R000");
        Assert.Equal("Bool", tag.DataType);
        Assert.Matches(@"^%M\d+\.\d+$", tag.Address);

        var ir = result.Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;
        Assert.Contains("COIL HX_S0_R000 := DB_Unit.Alarm", ir, StringComparison.Ordinal);
        Assert.DoesNotContain("MOVE(EN := TRUE, IN := DB_Unit.Alarm)", ir, StringComparison.Ordinal);
    }

    [Fact]
    public void A_BOOL_TAKES_BIT_ZERO_OF_ITS_OWN_REGISTER_so_the_register_index_stays_the_list_index()
    {
        // One register carries one value whatever its width. Packing is an explicit non-goal, and the
        // client's result index IS the offset into the result region — so a Bool sharing a register with
        // its neighbour would silently break every `IndexOf(signal)` in the loop.
        var map = OneSlot(result: 3);
        var plan = Generate(map, Binding(sources: new[]
        {
            MirroredSignal.Bool("DB_Unit.Alarm"),
            MirroredSignal.Int("DB_Unit.Actual"),
            MirroredSignal.Bool("DB_Unit.StopReq"),
        })).Require();

        var geometry = map.Geometry;
        var slot = map.Slots[0];

        Assert.Equal(geometry.BitAddressOf(slot.Result.Register + 0, 0), plan.Tags.Single(t => t.Name == "HX_S0_R000").Address);
        Assert.Equal(geometry.WordAddressOf(slot.Result.Register + 1), plan.Tags.Single(t => t.Name == "HX_S0_R001").Address);
        Assert.Equal(geometry.BitAddressOf(slot.Result.Register + 2, 0), plan.Tags.Single(t => t.Name == "HX_S0_R002").Address);

        // Bit 0, the same convention the start bools already use — so there is ONE bit-order question in
        // this system for the rig to settle, not two.
        Assert.Equal(geometry.BitAddressOf(slot.StartBoolRegister, slot.StartBitInRegister)[..^1] + "0",
            geometry.BitAddressOf(slot.StartBoolRegister, 0));
    }

    [Fact]
    public void A_MIXED_SLOT_EMITS_ONE_NETWORK_PER_TYPE_with_the_COIL_network_first()
    {
        // ir/SPEC.md groups statements within a network by kind in a fixed order and puts COIL before
        // MOVE. One network per type makes that rule unreachable rather than merely satisfied — and the
        // networks are still emitted in the legal order, so merging them later would remain legal.
        var plan = Generate(OneSlot(result: 3), Binding(sources: new[]
        {
            MirroredSignal.Bool("DB_Unit.Alarm"),
            MirroredSignal.Int("DB_Unit.Actual"),
            MirroredSignal.Bool("DB_Unit.StopReq"),
        })).Require();

        var results = plan.Networks.Where(n => n.Kind == CopyLayerNetworkKind.ResultsOut).ToArray();

        Assert.Equal(2, results.Length);
        Assert.EndsWith("- Bool", results[0].Title, StringComparison.Ordinal);
        Assert.EndsWith("- Int", results[1].Title, StringComparison.Ordinal);
        Assert.True(results[0].Number < results[1].Number);

        // The register index survives the split: R000 and R002 are the two Bools, R001 the Int.
        Assert.Equal(new[] { "HX_S0_R000", "HX_S0_R002" }, results[0].Moves.Select(m => m.To));
        Assert.Equal(new[] { "HX_S0_R001" }, results[1].Moves.Select(m => m.To));
    }

    [Fact]
    public void A_BOOL_VECTOR_TARGET_IS_DRIVEN_BY_A_COIL_TOO()
    {
        var result = Generate(binding: Binding(targets: new[]
        {
            MirroredSignal.Int("DB_Unit.Setpoint"),
            MirroredSignal.Bool("DB_Unit.Enable"),
        }));

        var ir = result.Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;

        Assert.Contains("MOVE(EN := TRUE, IN := HX_S0_V000) => DB_Unit.Setpoint", ir, StringComparison.Ordinal);
        Assert.Contains("COIL DB_Unit.Enable := HX_S0_V001", ir, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_UNSTATED_TYPE_IS_REFUSED_BY_NAME_AND_NEVER_TREATED_AS_INT()
    {
        // *** THE DEFECT WAS A SILENT DEFAULT, SO THE FIX IS A REFUSAL. *** An Unstated signal that
        // rendered as Int would be indistinguishable from a signal somebody deliberately typed Int, which
        // is exactly how the original hard-coding survived every test in this file.
        var result = Generate(binding: Binding(sources: new[] { new MirroredSignal("DB_Unit.Alarm", MirrorValueType.Unstated) }));

        Assert.False(result.Generated);
        Assert.Empty(result.Objects);

        var refusal = Assert.Single(result.Refusals);
        Assert.Contains("DB_Unit.Alarm", refusal, StringComparison.Ordinal);
        Assert.Contains("Unstated", refusal, StringComparison.Ordinal);
        Assert.Contains("UNSTATED IS A REFUSAL AND NOT A DEFAULT", refusal, StringComparison.Ordinal);

        // The refusal names what IS supported, so the reader has a route rather than only a wall.
        Assert.Contains("Bool", refusal, StringComparison.Ordinal);
        Assert.Contains("Int", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void A_TYPE_OUTSIDE_THE_SUPPORTED_SET_IS_REFUSED_BY_NAME_TOO()
    {
        // The next type will be found the same way this one was unless it refuses. `(MirrorValueType)99`
        // stands for whatever gets added to the enum without being taught to render — the case a
        // `default:` arm falling through to Int would silently absorb.
        var result = Generate(binding: Binding(targets: new[] { new MirroredSignal("DB_Unit.Setpoint", (MirrorValueType)99) }));

        Assert.False(result.Generated);

        var refusal = Assert.Single(result.Refusals);
        Assert.Contains("DB_Unit.Setpoint", refusal, StringComparison.Ordinal);
        Assert.Contains("vector target", refusal, StringComparison.Ordinal);
        Assert.Contains("cannot mirror", refusal, StringComparison.Ordinal);
        Assert.Contains("a ROW in MirrorElements", refusal, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Time — the second half of the same defect, and the first element wider than one register
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_TIME_IS_32_BIT_so_it_is_addressed_as_MD_and_OCCUPIES_TWO_REGISTERS()
    {
        // *** FORCED, NOT A NICETY. *** Scenarios run to 120 000 ms and a holding register is 16 bits, so
        // a scenario time cannot be an Int at all — it would wrap at 32 767 and read as a plausible number.
        var map = OneSlot(result: 3);
        var plan = Generate(map, Binding(sources: MirroredSignal.Times("DB_Unit.Elapsed"))).Require();

        var tag = plan.Tags.Single(t => t.Name == "HX_S0_R000");
        Assert.Equal("Time", tag.DataType);
        Assert.Equal(map.Geometry.DoubleWordAddressOf(map.Slots[0].Result.Register), tag.Address);
        Assert.Equal(2, MirrorElements.Require(MirrorValueType.Time).Registers);
    }

    [Fact]
    public void THE_TAG_AFTER_A_TIME_IS_R002_because_the_suffix_is_the_REGISTER_not_the_list_position()
    {
        // The two were the same number only while every element was one register wide. The GAP is the
        // point: R001 is the Time's second half, not a register nobody wired.
        var plan = Generate(OneSlot(result: 4), Binding(sources: new[]
        {
            MirroredSignal.Time("DB_Unit.Elapsed"),
            MirroredSignal.Bool("DB_Unit.Alarm"),
            MirroredSignal.Int("DB_Unit.Actual"),
        })).Require();

        var names = plan.Tags
            .Where(t => t.Name.StartsWith("HX_S0_R0", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToArray();

        Assert.Equal(new[] { "HX_S0_R000", "HX_S0_R002", "HX_S0_R003" }, names);
        Assert.DoesNotContain("HX_S0_R001", names);
    }

    [Fact]
    public void THE_REGISTER_OFFSETS_ARE_A_RUNNING_SUM_OF_WIDTHS_not_the_list_index()
    {
        // Everything downstream that turns a signal into an address comes from here. A reader still using
        // IndexOf would return the Time's SECOND HALF as the next signal's value — silently, plausibly.
        var binding = Binding(sources: new[]
        {
            MirroredSignal.Time("DB_Unit.Elapsed"),
            MirroredSignal.Bool("DB_Unit.Alarm"),
            MirroredSignal.Time("DB_Unit.Remaining"),
        });

        Assert.Equal(new[] { 0, 2, 3 }, binding.ResultRegisterOffsets);
        Assert.Equal(5, binding.ResultRegistersNeeded);

        Assert.Equal(3, binding.ResultRegisterOf("DB_Unit.Remaining"));

        // -1 rather than 0 for a signal this binding does not carry: 0 is a real offset, and a caller that
        // cannot tell them apart reads the first register for everything it does not have.
        Assert.Equal(-1, binding.ResultRegisterOf("DB_Unit.NotHere"));
    }

    [Fact]
    public void A_SLOT_IS_SIZED_IN_REGISTERS_NOT_SIGNALS_so_a_Time_that_does_not_fit_is_refused()
    {
        // Two signals, three registers needed. The old check compared a COUNT against a register
        // allocation, which was true only while every element was one register wide.
        var result = CopyLayerGenerator.Generate(
            OneSlot(result: 2),
            Binding(sources: new[] { MirroredSignal.Time("DB_Unit.Elapsed"), MirroredSignal.Int("DB_Unit.Actual") }),
            Naming, Stamp);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("needing 3 register(s)", StringComparison.Ordinal));
        Assert.Contains(result.Refusals, r => r.Contains("A 32-bit element occupies two", StringComparison.Ordinal));
    }

    [Fact]
    public void A_TIME_MIRRORS_WITH_A_MOVE_because_a_real_TIA_block_moves_Time_members()
    {
        // FB_HopperBlockageMonitor NETWORK 3 in the committed corpus does `MOVE(IN := ZeroTime) =>
        // AccumulatedElapsed`, both Time, and TIA accepted it. A Time is word-ish: it moves, it does not
        // coil. (A bare Time LITERAL is a different matter and that block's own comment records TIA
        // rejecting one — the copy layer moves members, never literals.)
        var ir = Generate(OneSlot(result: 3), Binding(sources: MirroredSignal.Times("DB_Unit.Elapsed")))
            .Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;

        Assert.Contains("MOVE(EN := TRUE, IN := DB_Unit.Elapsed) => HX_S0_R000", ir, StringComparison.Ordinal);
        Assert.DoesNotContain("COIL HX_S0_R000", ir, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_TIME_TAG_SAYS_ITS_WORD_ORDER_IS_UNCALIBRATED_on_the_tag_itself()
    {
        // ⚠️ A Time is two registers on the wire, so reading it back inherits the SAME uncalibrated
        // 32-bit transform as the version register and the scan counter. Read under the wrong order it is
        // out by 65 536 ms and looks like a plausible timing bug. The caveat rides on the tag comment
        // because that is what a person reads in TIA, where no doc comment reaches.
        var plan = Generate(OneSlot(result: 3), Binding(sources: MirroredSignal.Times("DB_Unit.Elapsed"))).Require();

        var comment = plan.Tags.Single(t => t.Name == "HX_S0_R000").Comment;

        Assert.Contains("registers 9 and 10", comment, StringComparison.Ordinal);
        Assert.Contains("UNCALIBRATED", comment, StringComparison.Ordinal);
    }

    [Fact]
    public void EVERY_SUPPORTED_TYPE_HAS_A_SHAPE_AN_ADDRESS_FORM_AND_A_WIDTH_from_ONE_table()
    {
        // *** Bool AND Time WERE NOT TWO BUGS. *** Both came from the same hard-coded "Int", and the fix
        // is one table rather than two special cases — so the third type is a ROW, and a type with no row
        // refuses. This test is the thing that fails if somebody adds an enum member and no row.
        foreach (var type in Enum.GetValues<MirrorValueType>().Where(t => t != MirrorValueType.Unstated))
        {
            var element = MirrorElements.For(type);

            Assert.True(element is not null,
                $"MirrorValueType.{type} has no row in MirrorElements. Add the row — data type, address form "
                + "(which is where the width comes from) and rung shape — or the generator will refuse every "
                + "signal of that type, which is the correct behaviour but not the intended one.");

            Assert.NotEmpty(element!.IrDataType);
            Assert.True(element.Registers is 1 or 2);
        }
    }

    [Fact]
    public void RETYPING_A_SIGNAL_CHANGES_THE_BUILD_STAMP_because_it_is_a_different_program()
    {
        // The stamp confirms what is RUNNING. A Bool and an Int of the same name are different tags at
        // different addresses driven by different rungs, so a stamp that could not tell them apart would
        // confirm the wrong program as loaded.
        var map = OneSlot();
        var asInt = BuildStamp.Of(map, Binding(sources: MirroredSignal.Ints("DB_Unit.Alarm")), Naming);
        var asBool = BuildStamp.Of(map, Binding(sources: MirroredSignal.Bools("DB_Unit.Alarm")), Naming);

        Assert.NotEqual(asInt.Value, asBool.Value);
    }

    // ---------------------------------------------------------------------------------------------
    // THE PER-SIGNAL LATCH — the milestone's last blocker, and the alternative was a trap
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_TRANSIENT_SIGNAL_GETS_A_STICKY_BIT_the_client_reads_and_clears()
    {
        // *** THE ALTERNATIVE WAS DOWNGRADING THE DECLARATION TO Sampled, WHICH LOOKS LIKE PROGRESS AND IS
        // NOT: *** it converts a strong assertion into one that can silently miss. A sampled assertion
        // landing in a poll gap is a silent wrong answer, not an error - and a poll IS one round trip, so
        // no polling rate recovers a one-scan event.
        var plan = Generate(OneSlot(result: 4), Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool, Transient: true),
        })).Require();

        var latch = plan.Tags.Single(t => t.Name.Contains("_L0", StringComparison.Ordinal));

        Assert.Equal("Bool", latch.DataType);
        Assert.Contains("Sticky", latch.Comment, StringComparison.Ordinal);
        Assert.Contains("cleared by the CLIENT", latch.Comment, StringComparison.Ordinal);

        var ir = Generate(OneSlot(result: 4), Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool, Transient: true),
        })).Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;

        // SCOIL, never COIL: a set coil is what makes it sticky, and it is the same shape the start echo
        // already uses for the same reason.
        Assert.Contains("SCOIL HX_S0_L001 := DB_Unit.Pulse", ir, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // THE PHASE-ARMED LATCH — built 2026-08-14, replacing the refusal that stood in for it
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE PROPERTY THE OLD REFUSAL WAS PROTECTING, NOW PROTECTED BY THE CAPABILITY.</b>
    ///
    /// <para>The old test asserted that a re-arming signal is REFUSED rather than given a one-shot latch.
    /// The property underneath it was never the refusal — it was <i>a re-arming signal must never receive
    /// the unconditional latch</i>, because index 2 would read identical to index 1 and a signal that
    /// never fired again would read as one that did. That property is asserted here directly, against a
    /// layer that is actually generated.</para>
    /// </summary>
    [Fact]
    public void A_SIGNAL_THAT_MUST_RE_ARM_EACH_INDEX_GETS_A_PHASE_ARMED_LATCH_NEVER_A_ONE_SHOT_ONE()
    {
        var result = Generate(OneSlot(result: 4), Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool,
                Transient: true, RearmsEachIndex: true, ArmedBy: "DB_Unit.Armed"),
        }));

        Assert.True(result.Generated);
        Assert.Empty(result.Refusals);

        var ir = result.Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;

        // The SET is guarded by the per-index window AND the in-index arm, with the signal LAST.
        Assert.Contains(
            "SCOIL HX_S0_L001 := HX_S0_Start AND DB_Unit.Armed AND DB_Unit.Pulse",
            ir, StringComparison.Ordinal);

        // The RESET is on a LEVEL, so a harness restart re-clears rather than stranding index 1's
        // evidence where index 2's reader will find it.
        Assert.Contains("RCOIL HX_S0_L001 := NOT HX_S0_Start", ir, StringComparison.Ordinal);

        // *** AND THE ONE-SHOT FORM IS ABSENT. *** This is the assertion the whole capability exists for:
        // the unconditional SCOIL is what silently deletes any finding that turns on a signal FALLING.
        Assert.DoesNotContain("SCOIL HX_S0_L001 := DB_Unit.Pulse", ir, StringComparison.Ordinal);

        // Reset AFTER set, so reset dominates if the two ever overlapped.
        Assert.True(
            ir.IndexOf("SCOIL HX_S0_L001", StringComparison.Ordinal) < ir.IndexOf("RCOIL HX_S0_L001", StringComparison.Ordinal),
            "the RCOIL must follow the SCOIL: clearing evidence is the loud failure direction and setting it falsely is the quiet one.");
    }

    /// <summary>
    /// The arm window is OPTIONAL, and its absence is a claim of no in-index window rather than a
    /// missing field. The per-index re-arm still holds, because that comes from the start bool.
    /// </summary>
    [Fact]
    public void WITHOUT_AN_ARM_WINDOW_THE_WHOLE_INDEX_IS_ARMED_and_the_start_bool_still_re_arms_it()
    {
        var ir = Generate(OneSlot(result: 4), Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool, Transient: true, RearmsEachIndex: true),
        })).Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;

        Assert.Contains("SCOIL HX_S0_L001 := HX_S0_Start AND DB_Unit.Pulse", ir, StringComparison.Ordinal);
        Assert.Contains("RCOIL HX_S0_L001 := NOT HX_S0_Start", ir, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE ARM BAND COSTS NO REGISTER, AND THAT IS A DESIGN CLAIM WORTH PINNING.</b>
    ///
    /// <para>The per-index window is the slot's START BOOL, which the mirror already carries and the
    /// client already drives low before every index. A second client-written arm band would have been a
    /// register, a <c>MirrorClient</c> verb and a wave step, all duplicating that. If somebody later adds
    /// an arm register, this goes red and they have to say why the existing seam was not enough.</para>
    /// </summary>
    [Fact]
    public void PHASE_ARMING_COSTS_THE_SAME_REGISTERS_AS_THE_UNCONDITIONAL_LATCH()
    {
        var plain = Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool, Transient: true),
        });

        var armed = Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool,
                Transient: true, RearmsEachIndex: true, ArmedBy: "DB_Unit.Armed"),
        });

        Assert.Equal(plain.ResultRegistersNeeded, armed.ResultRegistersNeeded);
        Assert.Equal(plain.LatchRegistersNeeded, armed.LatchRegistersNeeded);
        Assert.Equal(plain.LatchRegisterOffsets["DB_Unit.Pulse"], armed.LatchRegisterOffsets["DB_Unit.Pulse"]);
    }

    /// <summary>
    /// 🔴 <b>THE REFUSAL SURVIVES, FOR THE CASE THAT IS GENUINELY STILL INEXPRESSIBLE.</b>
    ///
    /// <para>A phase-armed latch is armed and re-cleared by the slot's start bool. D37 makes a null
    /// <c>StartCondition</c> a positive CLAIM — "this block is purely reactive and has no start gate" —
    /// so such a slot has no per-index level at all, and the only latch available is the unconditional
    /// one. <b>An unreachable refusal is worse than no refusal</b>, so this is the test that says it is
    /// still reachable.</para>
    /// </summary>
    [Fact]
    public void A_RE_ARMING_SIGNAL_ON_A_SLOT_WITH_NO_START_CONDITION_IS_STILL_REFUSED_BY_NAME()
    {
        var result = Generate(OneSlot(result: 4), Binding(start: null, sources: new[]
        {
            new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool, Transient: true, RearmsEachIndex: true),
        }));

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("CANNOT EXPRESS IT ON THIS SLOT", StringComparison.Ordinal));

        // It names the signal, the slot, and BOTH routes that work.
        Assert.Contains(result.Refusals, r => r.Contains("DB_Unit.Pulse", StringComparison.Ordinal));
        Assert.Contains(result.Refusals, r => r.Contains("D37", StringComparison.Ordinal));
        Assert.Contains(result.Refusals, r => r.Contains("bind this slot's real start condition", StringComparison.Ordinal));
        Assert.Contains(result.Refusals, r => r.Contains("LatchedBy", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>An arm window that reaches NEITHER consumer arms nothing</b>, so it is refused rather than
    /// accepted and ignored — the "plausible artifact" failure, one field over from the one that made
    /// this work necessary.
    ///
    /// <para><b>The arm tag here is deliberately NOT a result source of the slot</b>, which is what makes
    /// every case below reach neither consumer: no generated latch, and nothing
    /// <c>SlotBinding.ArmRegisterOf</c> could resolve. The converse — the arm tag IS mirrored — is
    /// <see cref="AN_ARM_WINDOW_ON_A_MIRRORED_LEVEL_IS_ADMITTED_AND_GENERATES_NO_LATCH"/>, and without
    /// that pair this test would pass just as well against a generator that refuses every arm window.</para>
    /// </summary>
    [Theory]
    [InlineData(false, false, null, "it is mirrored Sampled")]
    [InlineData(true, false, null, "it is mirrored Sampled")]
    [InlineData(false, false, "FB_Other", "lives INSIDE that block")]
    public void AN_ARM_WINDOW_ON_A_SIGNAL_WITH_NO_GENERATED_LATCH_IS_REFUSED(
        bool transient, bool rearms, string? latchedBy, string expected)
    {
        var result = Generate(OneSlot(result: 4), Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool,
                LatchedBy: latchedBy, Transient: transient, RearmsEachIndex: rearms, ArmedBy: "DB_Unit.Armed"),
        }));

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("ARMS NOTHING", StringComparison.Ordinal));
        Assert.Contains(result.Refusals, r => r.Contains(expected, StringComparison.Ordinal));

        // It names BOTH routes that work, not just the latch one — the whole defect in the old refusal
        // was that it knew of one consumer and there are two.
        Assert.Contains(result.Refusals, r => r.Contains("RESULT SOURCE OF THIS SLOT", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>D2: AN ARM WINDOW ON A PLAIN <c>Sampled</c> LEVEL IS ADMITTED, AND IT GENERATES NO LATCH.</b>
    ///
    /// <para><b>The refusal it replaces was written when <c>armedBy</c> had exactly one consumer</b> — the
    /// generated latch's arm term, which genuinely needs a momentary signal. It has had a second since
    /// 2026-08-17: <c>SlotBinding.ArmRegisterOf</c>, which arms the EVALUATION of a <c>Sampled</c> series
    /// by reading the arm tag's own mirrored register, and needs no latch and no transience at all.</para>
    ///
    /// <para><b>Measured cost of the stale premise:</b> six arm windows on the live binding had to be
    /// declared <c>transient</c> to be accepted, five of them on LEVELS, generating six latch registers
    /// nothing would ever read — band 95 → 101, mirror 164 → 170, and a redeploy to carry it.</para>
    ///
    /// <para><b>The latch-register assertion is the load-bearing half.</b> Admitting the binding while
    /// still emitting a latch would pass an admission-only test and would save not one register, which is
    /// the entire point of the change.</para>
    /// </summary>
    [Fact]
    public void AN_ARM_WINDOW_ON_A_MIRRORED_LEVEL_IS_ADMITTED_AND_GENERATES_NO_LATCH()
    {
        var binding = Binding(sources: new[]
        {
            // A LEVEL. Not transient, not re-arming, no LatchedBy — the exact shape the old rule refused.
            new MirroredSignal("DB_Unit.Response", MirrorValueType.Bool, ArmedBy: "DB_Unit.Armed"),

            // The arm tag, mirrored in the same result band. This is what ArmRegisterOf resolves against.
            new MirroredSignal("DB_Unit.Armed", MirrorValueType.Bool),
        });

        var result = Generate(OneSlot(result: 4), binding);

        Assert.True(result.Generated, string.Join(" | ", result.Refusals));

        // *** NO LATCH REGISTER WAS BOUGHT. *** The saving is the whole reason for the change.
        Assert.Empty(binding.LatchRegisterOffsets);
        Assert.DoesNotContain(result.Plan!.Networks, n => n.Kind == CopyLayerNetworkKind.ResultLatch);

        // And the window is genuinely readable: the observer resolves it to a register of this band.
        Assert.True(binding.ArmRegisterOf("DB_Unit.Response") >= 0);
    }

    /// <summary>
    /// <b>THE HALF OF THE OLD RULE THAT WAS NEVER STALE:</b> an arm tag this slot does not publish still
    /// arms nothing observable, whatever the signal declares. Without this the narrowed condition would
    /// admit a window that reads UNKNOWN at every frame and changes no verdict.
    /// </summary>
    [Fact]
    public void AN_ARM_TAG_THE_SLOT_DOES_NOT_PUBLISH_IS_STILL_REFUSED()
    {
        var result = Generate(OneSlot(result: 4), Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Response", MirrorValueType.Bool, ArmedBy: "DB_Unit.Armed"),
            new MirroredSignal("DB_Unit.Other", MirrorValueType.Bool),
        }));

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("ARMS NOTHING", StringComparison.Ordinal));
    }

    /// <summary>
    /// A blank arm window is a caller error on ANY result source, not only on a phase-armed one — the check
    /// used to sit inside the phase-armed loop, which was right while a latch was the only consumer.
    /// </summary>
    [Fact]
    public void A_BLANK_ARM_WINDOW_IS_REFUSED_ON_A_PLAIN_SAMPLED_SIGNAL_TOO()
    {
        var result = Generate(OneSlot(result: 4), Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Response", MirrorValueType.Bool, ArmedBy: "   "),
        }));

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("BLANK arm window", StringComparison.Ordinal));
    }

    /// <summary>A blank arm window is a caller who meant to name one, and is not read as an absent one.</summary>
    [Fact]
    public void A_BLANK_ARM_WINDOW_IS_REFUSED_rather_than_read_as_an_absent_one()
    {
        var result = Generate(OneSlot(result: 4), Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool,
                Transient: true, RearmsEachIndex: true, ArmedBy: "   "),
        }));

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("BLANK arm window", StringComparison.Ordinal));
    }

    /// <summary>
    /// The latch's provenance says WHICH FORM it is, because a reader of the plan cannot tell them apart
    /// from the register alone — and the two are cleared by different parties.
    /// </summary>
    [Fact]
    public void THE_PROVENANCE_DISTINGUISHES_THE_TWO_GENERATED_FORMS()
    {
        var unconditional = new MirroredSignal("Pulse", MirrorValueType.Bool, Transient: true);
        var armed = new MirroredSignal("Pulse", MirrorValueType.Bool,
            Transient: true, RearmsEachIndex: true, ArmedBy: "DB_Unit.Armed");

        Assert.Equal(LatchSource.Generated, unconditional.LatchSource);
        Assert.Equal(LatchSource.Generated, armed.LatchSource);

        Assert.False(unconditional.PhaseArmed);
        Assert.True(armed.PhaseArmed);

        Assert.DoesNotContain("PHASE-ARMED", unconditional.LatchProvenance!, StringComparison.Ordinal);
        Assert.Contains("PHASE-ARMED", armed.LatchProvenance!, StringComparison.Ordinal);
        Assert.Contains("DB_Unit.Armed", armed.LatchProvenance!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE LATCH SHAPE MOVES THE BUILD STAMP, AND IT MOVES NO WIDTH — so nothing else would have.</b>
    ///
    /// <para>Adding an arm window changes the emitted rungs and changes no register count, so the map
    /// hash does not move. Without the stamp carrying it, two different programs would share one stamp
    /// and the version register would confirm a build that is not the one running.</para>
    /// </summary>
    [Fact]
    public void ARMING_A_LATCH_CHANGES_THE_BUILD_STAMP_because_it_is_a_different_program()
    {
        var map = OneSlot(result: 4);

        var unconditional = Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool, Transient: true),
        });

        var rearming = unconditional with
        {
            ResultSources = new[]
            {
                new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool, Transient: true, RearmsEachIndex: true),
            },
        };

        var withWindow = unconditional with
        {
            ResultSources = new[]
            {
                new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool,
                    Transient: true, RearmsEachIndex: true, ArmedBy: "DB_Unit.Armed"),
            },
        };

        var stamps = new[] { unconditional, rearming, withWindow }
            .Select(b => BuildStamp.Of(map, b, Naming).Value)
            .ToArray();

        Assert.Equal(3, stamps.Distinct().Count());

        // The widths are identical — which is exactly why the map hash cannot carry this.
        Assert.Equal(map.MapHash, map.MapHash);
        Assert.Equal(unconditional.ResultRegistersNeeded, withWindow.ResultRegistersNeeded);
    }

    /// <summary>
    /// <b>APPENDED, NEVER INSERTED.</b> A signal that shapes no latch must hash exactly as it always did,
    /// so every stamp already computed for a plain binding — including the one in the deployed copy layer
    /// — is unchanged by this work.
    /// </summary>
    [Fact]
    public void A_BINDING_WITH_NO_LATCH_HASHES_TO_THE_SAME_STAMP_AS_BEFORE()
    {
        // 🔴 THIS CONSTANT WAS NOT READ OUT OF THIS CODE. It was computed by re-implementing the
        // PRE-CHANGE canonical form (git HEAD's BuildStamp.Of and RegisterMap.MapHash) in a separate
        // language and hashing it there — so it is an authority outside the assembly, not a value the
        // implementation agreed with itself about. If this goes red, a stamp already published to a
        // controller has silently changed meaning.
        Assert.Equal(0x055BE3AEu, BuildStamp.Of(OneSlot(), Binding(), Naming).Value);
    }

    /// <summary>
    /// 🔴 *** THE DID-NOT-RUN TEST FOR THE REFUSAL ITSELF. ***
    ///
    /// <para><b>A refusal that fires on everything passes every test that only checks refusals</b>, and
    /// this component has been caught by that shape twice. So: an ordinary transient — one asserted once
    /// per WAVE, which the unconditional latch expresses correctly — must STILL GET ITS LATCH.</para>
    /// </summary>
    [Fact]
    public void THE_DID_NOT_RUN_TEST_an_ordinary_transient_STILL_GETS_its_unconditional_latch()
    {
        var result = Generate(OneSlot(result: 4), Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool, Transient: true),
        }));

        Assert.True(result.Generated);
        Assert.Empty(result.Refusals);

        var ir = result.Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;
        Assert.Contains("SCOIL HX_S0_L001 := DB_Unit.Pulse", ir, StringComparison.Ordinal);
    }

    /// <summary>
    /// The refusal is scoped to the LATCH, not to the signal. A re-arming signal that is not transient
    /// has no generated latch to be wrong, so there is nothing to refuse — and refusing it anyway would
    /// be the gate firing outside its subject.
    /// </summary>
    [Fact]
    public void A_NON_TRANSIENT_SIGNAL_DECLARING_RE_ARM_IS_NOT_REFUSED_BECAUSE_NO_LATCH_IS_EMITTED()
    {
        var result = Generate(OneSlot(result: 4), Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Actual", MirrorValueType.Int, RearmsEachIndex: true),
        }));

        Assert.True(result.Generated);
        Assert.Empty(result.Refusals);
    }

    /// <summary>
    /// A HAND-AUTHORED latch is the route the refusal names, so it must actually be open: a re-arming
    /// signal latched by the block under test is admitted on provenance, with no generated latch involved.
    /// </summary>
    [Fact]
    public void THE_NAMED_ROUTE_IS_OPEN_a_hand_authored_latch_on_a_re_arming_signal_is_admitted()
    {
        var signal = new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool,
            LatchedBy: "FB_UnitUnderTest", RearmsEachIndex: true);

        var result = Generate(OneSlot(result: 4), Binding(sources: new[] { signal }));

        Assert.True(result.Generated);
        Assert.Empty(result.Refusals);
        Assert.Equal(LatchSource.HandAuthored, signal.LatchSource);
    }

    [Fact]
    public void THE_DID_NOT_RUN_TEST_a_signal_that_needs_no_latch_does_not_acquire_one()
    {
        // 🔴 *** DO NOT LATCH EVERYTHING. *** A latch on a signal that does not need one costs a register
        // and hides nothing - and the converse trap is worth carrying: A BLOCK LATCHING ITS OWN OUTPUT IS
        // A VALUE UNDER TEST, NOT INSTRUMENTATION. Latching it again in the copy layer would mean the
        // harness observing its own latch rather than the block's.
        var result = Generate(binding: Binding(sources: MirroredSignal.Bools("DB_Unit.Alarm")));
        var plan = result.Require();

        Assert.Empty(plan.Tags.Where(t => t.Name.Contains("_L0", StringComparison.Ordinal)));
        Assert.Empty(plan.Networks.Where(n => n.Kind == CopyLayerNetworkKind.ResultLatch));

        var ir = result.Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;
        Assert.DoesNotContain("SCOIL HX_S0_L", ir, StringComparison.Ordinal);

        // And the register budget is unchanged by a signal that needs no latch.
        Assert.Equal(0, Binding(sources: MirroredSignal.Bools("DB_Unit.Alarm")).LatchRegistersNeeded);
    }

    [Fact]
    public void THE_LATCH_MODE_IS_DERIVED_not_declared_so_its_source_is_Generated()
    {
        // The mode must stay DERIVED. A generated latch makes Latched a COMPUTED fact - readable out of
        // the emitted IR - where a hand-authored one can only be believed, and the two report differently.
        var generated = new MirroredSignal("Pulse", MirrorValueType.Bool, Transient: true);
        var handAuthored = new MirroredSignal("Violation", MirrorValueType.Bool, LatchedBy: "FB_HarnessViolationLatch");
        var neither = new MirroredSignal("Alarm", MirrorValueType.Bool);

        Assert.Equal(LatchSource.Generated, generated.LatchSource);
        Assert.Equal(LatchSource.HandAuthored, handAuthored.LatchSource);
        Assert.Equal(LatchSource.None, neither.LatchSource);

        Assert.Contains("derived", generated.LatchProvenance!, StringComparison.Ordinal);
        Assert.Equal("FB_HarnessViolationLatch", handAuthored.LatchProvenance);
        Assert.Null(neither.LatchProvenance);
    }

    [Fact]
    public void THE_REGISTER_BUDGET_MOVES_and_the_latches_sit_AFTER_the_values()
    {
        // *** COMPUTE THE WIDTH FROM THE SIGNAL SET; DO NOT ASSUME THERE IS ROOM. *** The mirror has gone
        // 8 -> 31 -> 35 already. Latches live in their own band after the values so that ADDING ONE MOVES
        // NO VALUE OFFSET - interleaving them would shift every later value the moment a latch appeared,
        // and the client's arithmetic is the value offsets.
        var binding = Binding(sources: new[]
        {
            new MirroredSignal("DB_Unit.Elapsed", MirrorValueType.Time),
            new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool, Transient: true),
            new MirroredSignal("DB_Unit.Alarm", MirrorValueType.Bool),
            new MirroredSignal("DB_Unit.Blip", MirrorValueType.Bool, Transient: true),
        });

        // Values: Time(2) + Bool(1) + Bool(1) + Bool(1) = 5. Latches: two transients = 2. Total 7.
        Assert.Equal(new[] { 0, 2, 3, 4 }, binding.ResultRegisterOffsets);
        Assert.Equal(2, binding.LatchRegistersNeeded);
        Assert.Equal(7, binding.ResultRegistersNeeded);

        // The latch band starts where the values end, and the value offsets are untouched by it.
        Assert.Equal(5, binding.LatchRegisterOffsets["DB_Unit.Pulse"]);
        Assert.Equal(6, binding.LatchRegisterOffsets["DB_Unit.Blip"]);
    }

    [Fact]
    public void AND_A_SLOT_TOO_NARROW_FOR_ITS_LATCHES_IS_REFUSED_rather_than_silently_overrunning()
    {
        // The budget is checked in REGISTERS, and the latch band is part of it - so "there is room for the
        // values" is no longer the question.
        var result = CopyLayerGenerator.Generate(
            OneSlot(result: 1),
            Binding(sources: new[] { new MirroredSignal("DB_Unit.Pulse", MirrorValueType.Bool, Transient: true) }),
            Naming, Stamp);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("needing 2 register(s)", StringComparison.Ordinal));
    }
}
