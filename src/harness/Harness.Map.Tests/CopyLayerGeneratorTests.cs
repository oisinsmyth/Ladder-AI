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
        CopyLayerGenerator.Generate(map ?? OneSlot(), binding ?? Binding(), Naming);

    // ---------------------------------------------------------------------------------------------
    // What it generates
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Generates_the_four_networks_the_minimal_layer_is_defined_as()
    {
        var plan = Generate().Require();

        Assert.Equal(
            new[]
            {
                CopyLayerNetworkKind.ScanCounter,
                CopyLayerNetworkKind.VectorIn,
                CopyLayerNetworkKind.StartBool,
                CopyLayerNetworkKind.ResultsOut,
            },
            plan.Networks.Select(n => n.Kind));

        Assert.Equal(new[] { 1, 2, 3, 4 }, plan.Networks.Select(n => n.Number));
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

        Assert.Equal(2, plan.Tags.Count(t => t.Name.Contains("_V", StringComparison.Ordinal)));
        Assert.Equal(2, plan.Tags.Count(t => t.Name.Contains("_R", StringComparison.Ordinal)));
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

            NETWORK 1 "Free-running scan counter"
              ADD(EN := TRUE, IN1 := HX_ScanCount, IN2 := 1) => HX_ScanCount

            NETWORK 2 "Vector in - slot S0"
              MOVE(EN := TRUE, IN := HX_S0_V000) => DB_Unit.Setpoint
              MOVE(EN := TRUE, IN := HX_S0_V001) => DB_Unit.Mode

            NETWORK 3 "Start bool - slot S0"
              COIL DB_Unit.StartCmd := HX_S0_Start

            NETWORK 4 "Results out - slot S0"
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
                HX_ScanCount 1 : DInt @ %MD4000 ACCESSIBLE VISIBLE WRITABLE COMMENT "Free-running scan counter. Wraps; scan stamps are differences from the start edge."
                HX_S0_Start 4 : Bool @ %M4005.0 ACCESSIBLE VISIBLE WRITABLE COMMENT "Start bool. Its rising edge is the test's T=0."
                HX_S0_V000 7 : Int @ %MW4006 ACCESSIBLE VISIBLE WRITABLE COMMENT "Vector register 0."
                HX_S0_V001 A : Int @ %MW4008 ACCESSIBLE VISIBLE WRITABLE COMMENT "Vector register 1."
                HX_S0_R000 D : Int @ %MW4012 ACCESSIBLE VISIBLE WRITABLE COMMENT "Result register 0."
                HX_S0_R001 10 : Int @ %MW4014 ACCESSIBLE VISIBLE WRITABLE COMMENT "Result register 1."

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
        Assert.Equal(new[] { 1, 2, 3 }, plan.Networks.Select(n => n.Number));
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
            Naming).Require();

        Assert.DoesNotContain(plan.Networks, n => n.Kind == CopyLayerNetworkKind.VectorIn);
        Assert.Contains(plan.Networks, n => n.Kind == CopyLayerNetworkKind.ResultsOut);
    }

    // ---------------------------------------------------------------------------------------------
    // Refusals
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_two_slot_map_is_refused_because_multi_slot_is_phase_3()
    {
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            new[] { new SlotRequest("S0", 2, 2), new SlotRequest("S1", 2, 2) })).Require();

        var result = CopyLayerGenerator.Generate(map, Binding(), Naming);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("phase 3", StringComparison.Ordinal));
    }

    [Fact]
    public void A_binding_naming_a_slot_the_map_does_not_hold_is_refused()
    {
        var result = CopyLayerGenerator.Generate(OneSlot(),
            new SlotBinding("S9", new[] { "A" }, null, new[] { "B" }), Naming);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("not in the map", StringComparison.Ordinal));
    }

    [Fact]
    public void A_binding_wider_than_its_allocated_region_is_refused()
    {
        var narrow = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            new[] { new SlotRequest("S0", 1, 1) })).Require();

        var result = CopyLayerGenerator.Generate(narrow, Binding(), Naming);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("vector targets", StringComparison.Ordinal));
        Assert.Contains(result.Refusals, r => r.Contains("result sources", StringComparison.Ordinal));
    }

    [Fact]
    public void A_binding_publishing_nothing_is_refused()
    {
        var result = CopyLayerGenerator.Generate(OneSlot(),
            new SlotBinding("S0", new[] { "A" }, null, Array.Empty<string>()), Naming);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("publishes nothing", StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_block_number_is_refused_rather_than_defaulted()
    {
        // Hard rule 3: block numbers are not invented here. X-J reserves a range and the caller allocates.
        var result = CopyLayerGenerator.Generate(OneSlot(), Binding(), new CopyLayerNaming());

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
            new SlotBinding("S 0", new[] { "A" }, null, new[] { "B" }), Naming);

        Assert.False(result.Generated);
        Assert.Contains(result.Refusals, r => r.Contains("plain identifier", StringComparison.Ordinal));
    }

    [Fact]
    public void A_refused_generation_yields_no_plan_no_objects_and_throws_on_Require()
    {
        var result = CopyLayerGenerator.Generate(OneSlot(), Binding(), new CopyLayerNaming());

        Assert.Null(result.Plan);
        Assert.Empty(result.Objects);
        Assert.Throws<InvalidOperationException>(() => result.Require());
    }
}
