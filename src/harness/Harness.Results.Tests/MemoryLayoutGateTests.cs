using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// Gate 11 (contract §4.5). <b>The invariant was measured TRUE and enforced by nothing</b>, so every
/// test here is about a way it could stop being true without anything noticing.
/// </summary>
public class MemoryLayoutGateTests
{
    private const string ClauseId = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);
    private const string Import = "import-2026-08-13-a";

    private static SubmissionVector Vector() =>
        new("V-1", "S0", 0, new AgentIdentity("agent-b"),
            new Basis(ClauseId, AssertionIdValue),
            new Dictionary<string, string> { ["Demo_Step"] = "5" },
            "Demo_Start",
            new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 0, "10") },
            AssertionForm.When,
            new SettlingDeclaration("count unchanged across 3 scans", new[] { "Demo_Count" }),
            MaxDurationScans: 20,
            CompletionValue: 1,
            new[] { new BlacklistEntry("FC_Other", "shares the plant model instance") },
            1,
            new[] { "ramp-to-limit" },
            "Demo_Done",
            "a ramp that overshoots by one step");

    private static GateResult Gate(DeploymentDeclaration? deployment, TagMapReach? reach = null) =>
        SubmissionGate.Check(
            new[] { Vector() },
            AssertionEnumeration.Of(new[] { ClauseId }, new[] { AssertionIdValue },
                new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When }, "agent-c",
                new Dictionary<string, string>(StringComparer.Ordinal) { [AssertionIdValue] = AssertionText },
                new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                {
                    [AssertionIdValue] = new HashSet<string>(StringComparer.Ordinal) { "Demo_Count" },
                }),
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true),
            new AgentIdentity("agent-a"),
            MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })),
            floorScans: 9,
            runtimeCompression: 1,
            ConflictGraph.Empty,
            compressionInputs: null,
            deployment: deployment,
            tagMapReach: reach)
        .Gates.Single(g => g.Gate.StartsWith("11 memory layout", StringComparison.Ordinal));

    private static S7ObjectDeclaration Row(
        string area = "DB_HarnessMarker", int db = 100, string obj = "DB_HarnessMarker",
        DeclaredLayout layout = DeclaredLayout.Standard, string? stamp = Import) =>
        new(area, db, obj, layout, stamp);

    // ---- ABSENT versus EMPTY ----------------------------------------------------------------------

    /// <summary>
    /// <b>Absent is a different statement from <c>s7Objects: []</c>.</b> One is "nobody said" and the
    /// other is the positive claim that no classic-S7comm path reaches a data block.
    /// </summary>
    [Fact]
    public void An_ABSENT_deployment_is_NOT_CHECKED_and_says_so_is_not_an_empty_list()
    {
        var gate = Gate(deployment: null);

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains("ABSENT IS NOT `s7Objects: []`", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_EMPTY_s7Objects_with_a_tag_map_that_reaches_nothing_is_the_normal_mirror_only_state()
    {
        var gate = Gate(new DeploymentDeclaration(Import, Array.Empty<S7ObjectDeclaration>()), TagMapReach.Of(Array.Empty<S7Reach>()));

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
        Assert.Contains("normal mirror-only state", gate.Detail, StringComparison.Ordinal);
    }

    // ---- THE SET-DIFFERENCE, which is the point of the gate ---------------------------------------

    /// <summary>
    /// *** THE MECHANISM BY WHICH THE INVARIANT WOULD BE BROKEN. *** <c>S7Transport</c> reaches any DB
    /// through a hand-written tag map, and the write fence is scoped on an AREA NAME drawn from that same
    /// map — so it verifies the caller's claimed area matches the tag's, never what KIND of object it is.
    /// </summary>
    [Fact]
    public void A_PAIR_THE_TAG_MAP_CAN_REACH_AND_NOBODY_DECLARED_IS_REFUSED()
    {
        var gate = Gate(
            new DeploymentDeclaration(Import, Array.Empty<S7ObjectDeclaration>()),
            TagMapReach.Of(new[] { new S7Reach("DB_Interface", 10) }));

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("DB_Interface (DB10)", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("has no layout claim", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void And_the_SAME_pair_declared_as_a_harness_object_at_Standard_passes()
    {
        var gate = Gate(
            new DeploymentDeclaration(Import, new[] { Row("DB_Interface", 10, "DB_HarnessInterface") }),
            TagMapReach.Of(new[] { new S7Reach("DB_Interface", 10) }));

        Assert.True(gate.Passed);
    }

    /// <summary>
    /// <b>No tag map is NOT CHECKED, never a pass.</b> An unreadable or absent map compared against an
    /// empty reachable set would say "the map reaches nothing", which is the opposite claim.
    /// </summary>
    [Fact]
    public void With_NO_TAG_MAP_the_set_difference_could_not_be_made_and_the_gate_says_so()
    {
        var gate = Gate(new DeploymentDeclaration(Import, new[] { Row() }), TagMapReach.None);

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains("THE SET-DIFFERENCE IS THE POINT OF THIS GATE", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// A NOT CHECKED gate still reports what it DID check, so a layout defect is not hidden behind the
    /// missing half.
    /// </summary>
    [Fact]
    public void A_NOT_CHECKED_gate_still_reports_the_findings_it_could_make()
    {
        var gate = Gate(
            new DeploymentDeclaration(Import, new[] { Row(layout: DeclaredLayout.Optimized) }),
            TagMapReach.None);

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains("declares Optimized", gate.Detail, StringComparison.Ordinal);
    }

    // ---- THE INVARIANT ITSELF ---------------------------------------------------------------------

    [Fact]
    public void A_ROW_NAMING_A_DELIVERABLE_BLOCK_IS_THE_INVARIANT_BROKEN()
    {
        var gate = Gate(
            new DeploymentDeclaration(Import, new[] { Row("DB_Plant", 20, "FC_ShipsToSite") }),
            TagMapReach.Of(new[] { new S7Reach("DB_Plant", 20) }, new[] { "FC_ShipsToSite" }));

        Assert.False(gate.Passed);
        Assert.Contains("THAT IS THE INVARIANT BROKEN, NOT A FINDING", gate.Detail, StringComparison.Ordinal);
    }

    // ---- LAYOUT ------------------------------------------------------------------------------------

    [Fact]
    public void An_OPTIMIZED_layout_is_refused_and_says_the_object_is_ABSENT_rather_than_wrong()
    {
        var gate = Gate(
            new DeploymentDeclaration(Import, new[] { Row(layout: DeclaredLayout.Optimized) }),
            TagMapReach.Of(new[] { new S7Reach("DB_HarnessMarker", 100) }));

        Assert.False(gate.Passed);
        Assert.Contains("simply ABSENT", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("wiring problem", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_UNSTATED_layout_is_refused_because_TIA_resolves_no_opinion_to_Optimized()
    {
        var gate = Gate(
            new DeploymentDeclaration(Import, new[] { Row(layout: DeclaredLayout.Unstated) }),
            TagMapReach.Of(new[] { new S7Reach("DB_HarnessMarker", 100) }));

        Assert.False(gate.Passed);
        Assert.Contains("RESOLVES NO OPINION TO OPTIMIZED", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void NotApplicable_is_accepted_and_needs_no_stamp_because_there_is_no_layout_to_re_assert()
    {
        var gate = Gate(
            new DeploymentDeclaration(Import, new[] { Row("M_Mirror", 0, "HarnessMirror", DeclaredLayout.NotApplicable, stamp: null) }),
            TagMapReach.Of(new[] { new S7Reach("M_Mirror", 0) }));

        Assert.True(gate.Passed);
    }

    // ---- THE STAMP, and why it is not a boolean ----------------------------------------------------

    /// <summary>
    /// *** THE TWO FACTS A BOOLEAN COULD NOT SEPARATE. *** "Nobody re-asserted it" and "re-asserted after
    /// the wrong import" are different, and both fall out of comparing a stamp.
    /// </summary>
    [Fact]
    public void NOBODY_RE_ASSERTED_IT_and_RE_ASSERTED_AFTER_THE_WRONG_IMPORT_are_DIFFERENT_findings()
    {
        var never = Gate(
            new DeploymentDeclaration(Import, new[] { Row(stamp: null) }),
            TagMapReach.Of(new[] { new S7Reach("DB_HarnessMarker", 100) }));

        var stale = Gate(
            new DeploymentDeclaration(Import, new[] { Row(stamp: "import-2026-08-12-z") }),
            TagMapReach.Of(new[] { new S7Reach("DB_HarnessMarker", 100) }));

        Assert.False(never.Passed);
        Assert.False(stale.Passed);

        Assert.Contains("NOBODY RE-ASSERTED THE LAYOUT", never.Detail, StringComparison.Ordinal);
        Assert.Contains("RE-ASSERTED AFTER THE WRONG IMPORT", stale.Detail, StringComparison.Ordinal);
        Assert.NotEqual(never.Detail, stale.Detail);
    }

    [Fact]
    public void Rows_with_NO_IMPORT_STAMP_at_all_are_NOT_CHECKED_because_nothing_dates_the_claims()
    {
        var gate = Gate(
            new DeploymentDeclaration(ImportStamp: null, new[] { Row() }),
            TagMapReach.Of(new[] { new S7Reach("DB_HarnessMarker", 100) }));

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains("DATED against it", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_naming_no_harness_object_is_the_claim_not_being_made()
    {
        var gate = Gate(
            new DeploymentDeclaration(Import, new[] { Row(obj: "") }),
            TagMapReach.Of(new[] { new S7Reach("DB_HarnessMarker", 100) }));

        Assert.False(gate.Passed);
        Assert.Contains("names no harness object", gate.Detail, StringComparison.Ordinal);
    }
}
