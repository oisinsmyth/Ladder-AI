using Harness.Batch;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>A batch is N lanes sharing ONE deployment — and the download time it saves is NOT the reason
/// it exists.</b>
///
/// <para>FI-65 #4 states the reason: <i>"two blocks that each compiled clean in isolation can still
/// conflict (C-308 multi-writer, dead wiring, IO boundary), and only a union check sees it."</i> A batch
/// is the only place that check is possible at all. So most of these tests are about COLLISIONS the
/// merge is supposed to surface, not about the merge succeeding.</para>
/// </summary>
public class BatchPlannerTests
{
    private const string Geometry =
        "\"blockName\": \"FC_HarnessCopyLayer\", \"blockNumber\": 9001, \"tagTableName\": \"HarnessMirror\", "
        + "\"tagPrefix\": \"HX_\", \"baseByte\": 1000, \"retentiveBytes\": 256, \"declaredRegisters\": 576";

    private static string Binding(string slotId, string? geometry = null, int resultSignals = 2) =>
        "{ " + (geometry ?? Geometry) + ", \"slots\": [{ \"slotId\": \"" + slotId + "\", "
        + "\"startCondition\": \"Demo_Start\", "
        + "\"vectorTargets\": [{ \"tag\": \"Demo_In\", \"specName\": \"In\", \"type\": \"Int\" }], "
        + "\"resultSources\": ["
        + string.Join(",", Enumerable.Range(0, resultSignals).Select(i =>
            $"{{ \"tag\": \"Demo_Out{i}\", \"specName\": \"Out{i}\", \"type\": \"Int\" }}"))
        + "] }] }";

    private static Lane LaneNamed(string name, string bindingKey, params string[] programs) =>
        new(name, bindingKey, name + ".submission.json", programs.Length == 0 ? new[] { name + "/ir" } : programs);

    /// <summary>Reads bindings out of a dictionary rather than off disk — the planner takes its reader.</summary>
    private static Func<string, string> Reader(Dictionary<string, string> bindings) =>
        path => bindings.TryGetValue(path, out var text)
            ? text
            : throw new FileNotFoundException($"no fixture binding at {path}");

    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Two_lanes_with_distinct_slots_merge_into_ONE_binding()
    {
        var bindings = new Dictionary<string, string>
        {
            ["a.json"] = Binding("Valve_S0"),
            ["b.json"] = Binding("Vessel_S0"),
        };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json"), LaneNamed("vessel", "b.json") },
            Reader(bindings));

        Assert.True(result.Planned, string.Join(" | ", result.Refusals));
        Assert.Equal(2, result.LanesQueued);
        Assert.Equal(new[] { "valve", "vessel" }, result.LanesBatched);

        // One map covering both lanes' slots — which is what makes it one deployment.
        Assert.Equal(2, result.Map!.Slots.Count);
        Assert.Contains("Valve_S0", result.MergedBindingJson);
        Assert.Contains("Vessel_S0", result.MergedBindingJson);
    }

    /// <summary>
    /// 🔴 <b>The collision that exists today.</b> Both harness lanes declare the same slot ids, and
    /// ordinals decide mirror addresses while results are looked up by slot id — so two slots of one
    /// name would write to different registers and read back as each other.
    /// </summary>
    [Fact]
    public void Two_lanes_claiming_ONE_slot_id_is_refused_and_names_BOTH()
    {
        var bindings = new Dictionary<string, string>
        {
            ["a.json"] = Binding("S0"),
            ["b.json"] = Binding("S0"),
        };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json"), LaneNamed("vessel", "b.json") },
            Reader(bindings));

        Assert.False(result.Planned);
        var refusal = Assert.Single(result.Refusals, r => r.Contains("slot id 'S0'"));
        Assert.Contains("'valve'", refusal);
        Assert.Contains("'vessel'", refusal);
    }

    /// <summary>
    /// Geometry is a property of ONE deployed harness. Two lanes disagreeing is not something to
    /// reconcile by picking one — choosing silently would deploy a mirror at an address half the lanes
    /// do not expect.
    /// </summary>
    [Theory]
    [InlineData("\"baseByte\": 2000", "baseByte")]
    [InlineData("\"declaredRegisters\": 300", "declaredRegisters")]
    [InlineData("\"blockNumber\": 9002", "blockNumber")]
    [InlineData("\"tagPrefix\": \"HY_\"", "tagPrefix")]
    public void Lanes_that_disagree_about_the_shared_geometry_are_refused(string override_, string field)
    {
        // Replace one field in the second lane's geometry.
        var key = override_.Split(':')[0];
        var altered = string.Join(", ", Geometry.Split(", ").Select(part => part.StartsWith(key, StringComparison.Ordinal) ? override_ : part));

        var bindings = new Dictionary<string, string>
        {
            ["a.json"] = Binding("Valve_S0"),
            ["b.json"] = Binding("Vessel_S0", altered),
        };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json"), LaneNamed("vessel", "b.json") },
            Reader(bindings));

        Assert.False(result.Planned);
        Assert.Contains(result.Refusals, r => r.Contains($"disagree about `{field}`"));
    }

    /// <summary>
    /// 🔴 <b>Empty is not clean.</b> A batch of nothing plans perfectly, deploys perfectly and tests
    /// nothing — and every other number in the report reads as valid, which is what makes it believable.
    /// </summary>
    [Fact]
    public void An_EMPTY_queue_is_refused_and_does_not_report_a_plan()
    {
        var result = BatchPlanner.Plan(Array.Empty<Lane>(), _ => throw new InvalidOperationException("nothing should be read"));

        Assert.False(result.Planned);
        Assert.Equal(0, result.LanesQueued);
        Assert.Contains(result.Refusals, r => r.Contains("NOTHING BATCHED"));
    }

    /// <summary>
    /// The merged map is checked against what Modbus can reach, and a batch that does not fit is REFUSED
    /// rather than truncated. Dropping the lanes that do not fit would produce a batch that runs and a
    /// set of lanes that silently did not.
    /// </summary>
    [Fact]
    public void A_merged_map_past_the_declared_area_is_refused_rather_than_truncated()
    {
        // Every lane inside 576 alone; together, well past it. 100 result signals each x 8 lanes.
        var bindings = new Dictionary<string, string>();
        var lanes = new List<Lane>();
        for (var i = 0; i < 8; i++)
        {
            bindings[$"{i}.json"] = Binding($"S{i}", resultSignals: 100);
            lanes.Add(LaneNamed($"lane{i}", $"{i}.json"));
        }

        var result = BatchPlanner.Plan(lanes, Reader(bindings));

        Assert.False(result.Planned);
        Assert.Contains(result.Refusals, r => r.Contains("the merged map does not fit"));
        Assert.Contains(result.Refusals, r => r.Contains("MB_HOLD_REG declares only 576"));

        // And nothing was quietly included.
        Assert.Empty(result.LanesBatched);
        Assert.Equal(8, result.LanesQueued);
    }

    /// <summary>
    /// 🔴 <b>The negative control.</b> A planner that refuses everything passes every refusal test above.
    /// Eight lanes that DO fit must plan.
    /// </summary>
    [Fact]
    public void Eight_lanes_that_fit_are_batched()
    {
        var bindings = new Dictionary<string, string>();
        var lanes = new List<Lane>();
        for (var i = 0; i < 8; i++)
        {
            bindings[$"{i}.json"] = Binding($"S{i}", resultSignals: 20);
            lanes.Add(LaneNamed($"lane{i}", $"{i}.json"));
        }

        var result = BatchPlanner.Plan(lanes, Reader(bindings));

        Assert.True(result.Planned, string.Join(" | ", result.Refusals));
        Assert.Equal(8, result.LanesBatched.Count);
    }

    /// <summary>
    /// A lane whose binding cannot be read is a refusal naming the lane and the path — not a batch of the
    /// lanes that happened to parse.
    /// </summary>
    [Fact]
    public void An_unreadable_binding_refuses_the_whole_batch_and_names_the_lane()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding("Valve_S0") };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json"), LaneNamed("vessel", "missing.json") },
            Reader(bindings));

        Assert.False(result.Planned);
        Assert.Contains(result.Refusals, r => r.Contains("lane 'vessel'") && r.Contains("missing.json"));
        Assert.Empty(result.LanesBatched);
    }

    /// <summary>
    /// The report states its denominator on every run. Lanes batched alone cannot be told from lanes
    /// batched out of many.
    /// </summary>
    [Fact]
    public void The_report_always_states_how_many_lanes_were_QUEUED_not_only_how_many_were_batched()
    {
        var bindings = new Dictionary<string, string>
        {
            ["a.json"] = Binding("Valve_S0"),
            ["b.json"] = Binding("Vessel_S0"),
        };

        var text = BatchPlanner.Describe(BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json"), LaneNamed("vessel", "b.json") },
            Reader(bindings)));

        Assert.Contains("lanes queued  2", text);
        Assert.Contains("lanes batched 2", text);
    }

    /// <summary>
    /// 🔴 <b>The merged binding has to be READABLE BY THE THING THAT CONSUMES IT.</b>
    ///
    /// <para>Serialising a document and asserting on the text proves the text; it does not prove
    /// <c>harness-run</c> can load it. This round-trips the merge through the very reader the loop uses,
    /// and checks both lanes' slots survive — the failure it excludes is a merged file that looks
    /// perfectly well-formed and loses a lane on the way back in.</para>
    /// </summary>
    [Fact]
    public void The_merged_binding_round_trips_through_the_reader_the_LOOP_uses()
    {
        var bindings = new Dictionary<string, string>
        {
            ["a.json"] = Binding("Valve_S0"),
            ["b.json"] = Binding("Vessel_S0"),
        };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json"), LaneNamed("vessel", "b.json") },
            Reader(bindings));

        var reloaded = Harness.Gate.BindingDocument.Read(result.MergedBindingJson!);

        Assert.Equal(2, reloaded.Slots!.Count);
        Assert.Equal(new[] { "Valve_S0", "Vessel_S0" }, reloaded.Slots.Select(s => s.SlotId).ToArray());

        // And the shared geometry survived, or the merged deployment would sit somewhere no lane expects.
        Assert.Equal(1000, reloaded.BaseByte);
        Assert.Equal(576, reloaded.DeclaredRegisters);
        Assert.Equal(9001, reloaded.BlockNumber);

        // Each slot keeps its own signals — a merge that flattened them would produce a map of the right
        // SIZE bound to the wrong tags, which nothing downstream could detect.
        Assert.All(reloaded.Slots, s => Assert.NotEmpty(s.ResultSources!));
    }

    /// <summary>
    /// camelCase, because every hand-written binding in this repo is. The reader is case-insensitive so
    /// PascalCase would work and look wrong — the kind of difference that gets explained away.
    /// </summary>
    [Fact]
    public void The_merged_binding_is_written_in_the_same_case_as_every_other_binding()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding("Valve_S0") };

        var result = BatchPlanner.Plan(new[] { LaneNamed("valve", "a.json") }, Reader(bindings));

        Assert.Contains("\"slotId\"", result.MergedBindingJson);
        Assert.DoesNotContain("\"SlotId\"", result.MergedBindingJson);
    }

    /// <summary>
    /// A <c>Time</c> spans two registers. Counting it as one would make the map short by a word per timer
    /// and overlap the next slot — silently, and only on the wire.
    /// </summary>
    [Fact]
    public void A_Time_signal_is_counted_as_TWO_registers()
    {
        var oneInt = Binding("S0", resultSignals: 1);
        var oneTime = oneInt.Replace("\"type\": \"Int\" }]", "\"type\": \"Time\" }]");

        var ints = BatchPlanner.Plan(new[] { LaneNamed("a", "x") }, _ => oneInt);
        var times = BatchPlanner.Plan(new[] { LaneNamed("a", "x") }, _ => oneTime);

        Assert.True(ints.Planned && times.Planned);
        Assert.True(times.Map!.ResultBlock.End > ints.Map!.ResultBlock.End,
            "a Time result must widen the map relative to an Int one.");
    }
}
