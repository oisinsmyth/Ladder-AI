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

    private static SlotBinding Binding(string? start = "DB_Unit.StartCmd") => new(
        "S0",
        new[] { "DB_Unit.Setpoint", "DB_Unit.Mode" },
        start,
        new[] { "DB_Unit.Actual", "DB_Unit.State" });

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
    public void The_generated_block_is_the_IR_the_converter_round_trips()
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

            NETWORK 3 "Vector in - slot S0"
              MOVE(EN := TRUE, IN := HX_S0_V000) => DB_Unit.Setpoint
              MOVE(EN := TRUE, IN := HX_S0_V001) => DB_Unit.Mode

            NETWORK 4 "Start bool - slot S0"
              COIL DB_Unit.StartCmd := HX_S0_Start

            NETWORK 5 "Start echo - slot S0"
              SCOIL HX_S0_Ran := DB_Unit.StartCmd

            NETWORK 6 "Results out - slot S0"
              MOVE(EN := TRUE, IN := DB_Unit.Actual) => HX_S0_R000
              MOVE(EN := TRUE, IN := DB_Unit.State) => HX_S0_R001

            """.ReplaceLineEndings("\n"),
            block.Ir);
    }

    [Fact]
    public void The_generated_tag_table_is_the_IR_the_converter_round_trips()
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
            new SlotBinding("S0", Array.Empty<string>(), "DB_Unit.StartCmd", new[] { "DB_Unit.Actual" }),
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
            new SlotBinding("S0", new[] { "DB_A.Setpoint", "DB_A.Mode" }, "DB_A.StartCmd", new[] { "DB_A.Actual", "DB_A.State" }),
            new SlotBinding("S1", new[] { "DB_B.Level", "DB_B.Trip" }, "DB_B.StartCmd", new[] { "DB_B.Peak", "DB_B.Alarm" }),
        },
        Naming, Stamp);

    [Fact]
    public void The_two_slot_block_is_the_IR_the_converter_round_trips()
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

            NETWORK 3 "Vector in - slot S0"
              MOVE(EN := TRUE, IN := HX_S0_V000) => DB_A.Setpoint
              MOVE(EN := TRUE, IN := HX_S0_V001) => DB_A.Mode

            NETWORK 4 "Start bool - slot S0"
              COIL DB_A.StartCmd := HX_S0_Start

            NETWORK 5 "Start echo - slot S0"
              SCOIL HX_S0_Ran := DB_A.StartCmd

            NETWORK 6 "Results out - slot S0"
              MOVE(EN := TRUE, IN := DB_A.Actual) => HX_S0_R000
              MOVE(EN := TRUE, IN := DB_A.State) => HX_S0_R001

            NETWORK 7 "Vector in - slot S1"
              MOVE(EN := TRUE, IN := HX_S1_V000) => DB_B.Level
              MOVE(EN := TRUE, IN := HX_S1_V001) => DB_B.Trip

            NETWORK 8 "Start bool - slot S1"
              COIL DB_B.StartCmd := HX_S1_Start

            NETWORK 9 "Start echo - slot S1"
              SCOIL HX_S1_Ran := DB_B.StartCmd

            NETWORK 10 "Results out - slot S1"
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
        var s0 = new SlotBinding("S0", new[] { "A.v" }, "A.s", new[] { "A.r" });
        var s1 = new SlotBinding("S1", new[] { "B.v" }, "B.s", new[] { "B.r" });

        var forwards = CopyLayerGenerator.Generate(map, new[] { s0, s1 }, Naming, Stamp).Objects;
        var backwards = CopyLayerGenerator.Generate(map, new[] { s1, s0 }, Naming, Stamp).Objects;

        Assert.Equal(forwards.Single(o => o.Kind == HarnessObjectKind.Block).Ir,
            backwards.Single(o => o.Kind == HarnessObjectKind.Block).Ir);
    }

    [Fact]
    public void Two_bindings_for_one_slot_are_refused()
    {
        var map = TwoSlots();
        var duplicate = new SlotBinding("S0", new[] { "A.v" }, "A.s", new[] { "A.r" });

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
            new SlotBinding("S9", new[] { "A" }, null, new[] { "B" }), Naming, Stamp);

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
        Assert.Contains(result.Refusals, r => r.Contains("vector targets", StringComparison.Ordinal));
        Assert.Contains(result.Refusals, r => r.Contains("result sources", StringComparison.Ordinal));
    }

    [Fact]
    public void A_binding_publishing_nothing_is_refused()
    {
        var result = CopyLayerGenerator.Generate(OneSlot(),
            new SlotBinding("S0", new[] { "A" }, null, Array.Empty<string>()), Naming, Stamp);

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
    public void A_slot_id_that_is_not_an_identifier_is_refused_because_it_becomes_a_tag_name()
    {
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            new[] { new SlotRequest("S 0", 2, 2) })).Require();

        var result = CopyLayerGenerator.Generate(map,
            new SlotBinding("S 0", new[] { "A" }, null, new[] { "B" }), Naming, Stamp);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("plain identifier", StringComparison.Ordinal));
    }

    [Fact]
    public void A_refused_generation_yields_no_plan_no_objects_and_throws_on_Require()
    {
        var result = CopyLayerGenerator.Generate(OneSlot(), Binding(), new CopyLayerNaming(), Stamp);

        Assert.Null(result.Plan);
        Assert.Empty(result.Objects);
        Assert.Throws<InvalidOperationException>(() => result.Require());
    }
}
