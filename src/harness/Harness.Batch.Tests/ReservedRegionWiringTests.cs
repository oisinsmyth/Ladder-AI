using Harness.Batch;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>The guard is only a guard once a planner feeds it.</b>
///
/// <para><c>ReservedRegion</c> and <c>MirrorGeometry.Reserving</c> landed complete and fully tested and
/// <b>nothing constructed a geometry with a reservation</b>, so the check was live and dormant — it
/// could not have caught the next occurrence of the very defect it was written for. These tests are
/// about the wiring, not the check: they prove a lane's declaration reaches allocation.</para>
///
/// <para>The measured incident, for the numbers below: a merged mirror grew from one lane to two,
/// reached register 323, and walked into a hand-authored virtual panel's command band at 256 — 53 tags
/// colliding bit-for-bit, including the panel's master enable.</para>
/// </summary>
public class ReservedRegionWiringTests
{
    private const string Geometry =
        "\"blockName\": \"FC_HarnessCopyLayer\", \"blockNumber\": 9001, \"tagTableName\": \"HarnessMirror\", "
        + "\"tagPrefix\": \"HX_\", \"baseByte\": 1000, \"retentiveBytes\": 256";

    /// <summary>
    /// A lane sized to the REAL incident: 79 vector + 80 result signals ⇒ a padded slot width of 159, so
    /// two merged slots reach <c>6 + 2×159 = 324</c> registers — the exact extent the deployed two-lane
    /// mirror had when it walked into the panel at 256.
    ///
    /// <para>⚠️ Two drafts of this fixture were wrong, in opposite directions, and both are worth
    /// recording. <b>30 + 60</b> gave a width of 90, so two slots reached only 186 and never touched the
    /// band — both "should refuse" tests passed with <b>nothing to refuse</b>. <b>29 + 130</b> reached the
    /// right extent but put 130 registers in one slot's result region, which the map refuses outright:
    /// a result region must fit ONE FC03 read (125 registers) and a read must never straddle two slots
    /// (X-A). The split has to be even-ish for the arithmetic to be reachable at all.</para>
    ///
    /// <para>The lesson worth keeping: I guessed at the second failure instead of reading the refusal,
    /// which the map states plainly. Sizing a fixture to the measurement — and reading what the tool
    /// says when it declines — is what makes these tests about the guard rather than about arithmetic.</para>
    /// </summary>
    private static string Binding(string slotId, int declaredRegisters, string? reserved = null) =>
        "{ " + Geometry + ", \"declaredRegisters\": " + declaredRegisters
        + (reserved is null ? string.Empty : ", \"reservedRegions\": " + reserved)
        + ", \"slots\": [{ \"slotId\": \"" + slotId + "\", \"startCondition\": \"Go\", "
        + "\"vectorTargets\": ["
        + string.Join(",", Enumerable.Range(0, 79).Select(i => $"{{ \"tag\": \"In{i}\", \"specName\": \"In{i}\", \"type\": \"Int\" }}"))
        + "], \"resultSources\": ["
        + string.Join(",", Enumerable.Range(0, 80).Select(i => $"{{ \"tag\": \"Out{i}\", \"specName\": \"Out{i}\", \"type\": \"Int\" }}"))
        + "] }] }";

    private const string PanelAt256 = "[{ \"register\": 256, \"length\": 320, \"owner\": \"virtual panel\" }]";
    private const string PanelAt704 = "[{ \"register\": 704, \"length\": 320, \"owner\": \"virtual panel\" }]";

    private string _root = string.Empty;

    private Lane LaneNamed(string name, string key)
    {
        if (_root.Length == 0)
        {
            _root = Path.Combine(Path.GetTempPath(), "reserved-wiring-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "FC_Fixture.ir"), "BLOCK FC FC_Fixture\nEND_BLOCK\n");
        }

        return new Lane(name, key, name + ".submission.json", new[] { _root });
    }

    private static Func<string, string> Reader(Dictionary<string, string> bindings) =>
        path => bindings.TryGetValue(path, out var text) ? text : throw new FileNotFoundException(path);

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>Two lanes reaching into a declared band are REFUSED at plan time, naming the owner.</b> This
    /// is the incident, reproduced through the planner rather than through the geometry directly.
    /// </summary>
    [Fact]
    public void A_merged_mirror_that_reaches_a_declared_band_is_refused_and_names_the_owner()
    {
        var bindings = new Dictionary<string, string>
        {
            ["a.json"] = Binding("Valve_S0", 576, PanelAt256),
            ["b.json"] = Binding("Vessel_S0", 576, PanelAt256),
        };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json"), LaneNamed("vessel", "b.json") }, Reader(bindings));

        Assert.False(result.Planned);
        Assert.Contains(result.Refusals, r => r.Contains("virtual panel", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL, and it is the layout actually being deployed.</b> The same two lanes
    /// against the same panel moved to 704 in a 1024-register area plan cleanly. Without this, a planner
    /// that refused any reservation at all would pass the test above.
    /// </summary>
    [Fact]
    public void The_same_two_lanes_against_the_band_at_704_plan_cleanly()
    {
        var bindings = new Dictionary<string, string>
        {
            ["a.json"] = Binding("Valve_S0", 1024, PanelAt704),
            ["b.json"] = Binding("Vessel_S0", 1024, PanelAt704),
        };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json"), LaneNamed("vessel", "b.json") }, Reader(bindings));

        Assert.True(result.Planned, string.Join(" | ", result.Refusals));
    }

    /// <summary>A lane declaring no neighbours behaves exactly as before — the field is additive.</summary>
    [Fact]
    public void A_lane_declaring_no_reservations_plans_as_it_always_did()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding("Valve_S0", 576) };

        var result = BatchPlanner.Plan(new[] { LaneNamed("valve", "a.json") }, Reader(bindings));
        Assert.True(result.Planned, string.Join(" | ", result.Refusals));
    }

    /// <summary>
    /// 🔴 <b>A neighbour only ONE lane declares still applies to the merged map.</b> Reading the first
    /// lane's list would drop it — the same silence the guard exists to close, reintroduced one level up.
    /// </summary>
    [Fact]
    public void A_neighbour_declared_by_only_the_SECOND_lane_still_binds_the_merged_map()
    {
        var bindings = new Dictionary<string, string>
        {
            ["a.json"] = Binding("Valve_S0", 576),                 // declares nothing
            ["b.json"] = Binding("Vessel_S0", 576, PanelAt256),    // declares the panel
        };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json"), LaneNamed("vessel", "b.json") }, Reader(bindings));

        Assert.False(result.Planned);
        Assert.Contains(result.Refusals, r => r.Contains("virtual panel", StringComparison.Ordinal));
    }

    /// <summary>
    /// Both lanes declaring the SAME neighbour is the ordinary case and must not read as two owners
    /// fighting over one band — identical declarations collapse.
    /// </summary>
    [Fact]
    public void Both_lanes_declaring_the_same_neighbour_is_not_a_conflict()
    {
        var bindings = new Dictionary<string, string>
        {
            ["a.json"] = Binding("Valve_S0", 1024, PanelAt704),
            ["b.json"] = Binding("Vessel_S0", 1024, PanelAt704),
        };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json"), LaneNamed("vessel", "b.json") }, Reader(bindings));

        Assert.True(result.Planned, string.Join(" | ", result.Refusals));
        Assert.DoesNotContain(result.Refusals, r => r.Contains("both claim", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>Two lanes disagreeing about what else lives in the area is refused, not reconciled.</b>
    /// Guessing which is right would be inventing the area map.
    /// </summary>
    [Fact]
    public void Two_lanes_declaring_OVERLAPPING_but_different_neighbours_are_refused()
    {
        var bindings = new Dictionary<string, string>
        {
            ["a.json"] = Binding("Valve_S0", 1024, "[{ \"register\": 704, \"length\": 320, \"owner\": \"panel\" }]"),
            ["b.json"] = Binding("Vessel_S0", 1024, "[{ \"register\": 750, \"length\": 40, \"owner\": \"something else\" }]"),
        };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json"), LaneNamed("vessel", "b.json") }, Reader(bindings));

        Assert.False(result.Planned);
    }

    /// <summary>
    /// A malformed reservation is REFUSED, never dropped. A lane that declared a neighbour believes part
    /// of the area is off limits; ignoring a typo'd declaration restores exactly the silence being closed.
    /// </summary>
    [Fact]
    public void A_reservation_with_no_owner_is_refused_rather_than_ignored()
    {
        var bindings = new Dictionary<string, string>
        {
            ["a.json"] = Binding("Valve_S0", 1024, "[{ \"register\": 704, \"length\": 320 }]"),
        };

        Assert.False(BatchPlanner.Plan(new[] { LaneNamed("valve", "a.json") }, Reader(bindings)).Planned);
    }
}
