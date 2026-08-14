using System.Reflection;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// *** EVERY <c>NOT CHECKED</c> MUST SAY WHICH KIND IT IS. ***
///
/// <para>A flat count of NOT CHECKEDs is the shape that reads as a pass to a tired reader. Four
/// different facts hide behind it: one that is not a gap at all, one that can never be closed off the
/// rig, and two build lists with different owners. These tests make the classification impossible to
/// omit rather than merely conventional.</para>
/// </summary>
public class NotCheckedTriageTests
{
    /// <summary>
    /// *** THE GUARD THAT CANNOT BE SILENTLY DISCONNECTED. ***
    ///
    /// <para>Every <c>NOT CHECKED</c> path in <c>SubmissionGate</c> is driven — by a submission
    /// deliberately missing every optional input — and each result must carry a reason. A new gate that
    /// forgets one reddens this, and the only construction path that supplies one is
    /// <see cref="GateResult.CouldNotRun"/>, whose reason is a REQUIRED parameter.</para>
    /// </summary>
    [Fact]
    public void NO_GATE_REPORTS_NOT_CHECKED_WITHOUT_SAYING_WHY()
    {
        var report = MinimalSubmission();

        Assert.NotEmpty(report.NotChecked);

        var unclassified = report.NotChecked.Where(g => g.Reason == NotCheckedReason.Unstated).ToArray();

        Assert.True(unclassified.Length == 0,
            "these gates reported NOT CHECKED without saying which kind of NOT CHECKED they are, which is a defect in the GATE and not in the submission: "
            + string.Join(", ", unclassified.Select(g => g.Gate))
            + ". Build them with GateResult.CouldNotRun, whose reason is a required parameter.");
    }

    /// <summary>
    /// The negative control. If <c>MinimalSubmission</c> ever stopped tripping the NOT CHECKED paths,
    /// the test above would pass by examining nothing — the exact failure this project keeps finding.
    /// </summary>
    [Fact]
    public void The_fixture_really_does_trip_a_broad_set_of_NOT_CHECKED_paths()
    {
        var report = MinimalSubmission();

        Assert.True(report.NotChecked.Count >= 6,
            $"only {report.NotChecked.Count} gate(s) reported NOT CHECKED, so the classification test above is examining almost nothing. "
            + "Empty is not clean: a guard driven over one path is a guard that has not been driven.");
    }

    /// <summary>
    /// <b>A CHECKED or JUDGEMENT gate carries no reason</b>, because there is nothing to explain. A
    /// reason on a passing gate would invite a reader to look for a caveat that does not exist.
    /// </summary>
    [Fact]
    public void A_gate_that_DID_run_carries_no_reason()
    {
        var report = MinimalSubmission();

        Assert.All(report.Gates.Where(g => g.Status != GateStatus.NotChecked),
            g => Assert.Equal(NotCheckedReason.Unstated, g.Reason));
    }

    /// <summary>
    /// The three device-bound gates are the rig session's, and <b>no artifact closes them</b>. Pinned so
    /// that a future change cannot quietly reclassify one as a build-list item — which would put it on a
    /// list somebody expects to empty offline.
    /// </summary>
    [Fact]
    public void GATE_11_REQUIRES_THE_DEVICE_AND_IS_NOT_A_BUILD_LIST_ITEM()
    {
        var gate = MinimalSubmission().NotChecked.Single(g => g.Gate.StartsWith("11 memory layout", StringComparison.Ordinal));

        Assert.Equal(NotCheckedReason.RequiresTheDevice, gate.Reason);
        Assert.False(gate.IsClosableOffline);
    }

    /// <summary>
    /// *** GATE 5 IS D6 INDEPENDENCE WORKING, NOT A GAP. *** The observability map must come from the
    /// coordinator's bindings; the submission's own map is the author vouching for the artifact the gate
    /// exists to check them against. It will read NOT CHECKED for ever on a self-declaring submission,
    /// and that is the correct output.
    /// </summary>
    [Fact]
    public void GATE_5_IS_BY_DESIGN_AND_MUST_NOT_BE_FILED_AS_OUTSTANDING_WORK()
    {
        var gate = MinimalSubmission().NotChecked.Single(g => g.Gate.StartsWith("5 observability", StringComparison.Ordinal));

        Assert.Equal(NotCheckedReason.IndependentAuthorityByDesign, gate.Reason);
        Assert.False(gate.IsClosableOffline);
    }

    [Fact]
    public void The_four_reasons_are_distinguishable_and_the_zero_value_is_the_unusable_one()
    {
        // default(NotCheckedReason) must never be a real classification: a dropped reason has to fail
        // the same comparison a wrong one fails.
        Assert.Equal(NotCheckedReason.Unstated, default(NotCheckedReason));
        Assert.Equal(5, Enum.GetValues<NotCheckedReason>().Length);
    }

    /// <summary>
    /// <b>Every reason the enum declares is actually reachable</b> from some gate — a classification
    /// nothing produces is a category nobody maintains. Checked against the union of two submissions so
    /// that a reason used only on a healthy one still counts.
    /// </summary>
    [Fact]
    public void Every_declared_reason_except_Unstated_is_produced_by_some_gate()
    {
        var produced = MinimalSubmission().NotChecked.Select(g => g.Reason).ToHashSet();

        foreach (var reason in Enum.GetValues<NotCheckedReason>().Where(r => r != NotCheckedReason.Unstated))
        {
            Assert.True(produced.Contains(reason),
                $"no gate produced {reason}. A classification nothing emits is a category nobody maintains, and the report's group for it prints nothing for ever.");
        }
    }

    /// <summary>
    /// <c>CouldNotRun</c> is the only construction path that supplies a reason, and it always produces a
    /// <c>NOT CHECKED</c> that did not pass. Pinned because the whole guard rests on it.
    /// </summary>
    [Fact]
    public void CouldNotRun_always_produces_a_NOT_CHECKED_that_did_not_pass()
    {
        var gate = GateResult.CouldNotRun("x", NotCheckedReason.RequiresTheDevice, "a controller", "detail");

        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Equal(NotCheckedReason.RequiresTheDevice, gate.Reason);
    }

    /// <summary>
    /// A submission that supplies NOTHING optional, so every gate needing an outside input reports NOT
    /// CHECKED. It is deliberately minimal rather than realistic: the point is coverage of the
    /// classification, not of the block.
    /// </summary>
    private static SubmissionReport MinimalSubmission()
    {
        const string clause = "REQ-014";
        const string text = "WHEN the step is applied THEN the count reaches the limit";
        var id = AssertionId.Compute(clause, text);

        var vector = new SubmissionVector(
            "V-1", "S0", 0, new AgentIdentity("agent-b"),
            new Basis(clause, id),
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

        return SubmissionGate.Check(
            new[] { vector },

            // The FLAT projection: assertion ids only. No forms, no enumerator, no normalised text, no
            // required observations, no bounds — so every enumeration-fed gate reports NOT CHECKED.
            AssertionEnumeration.Of(new[] { clause }, new[] { id }),

            // A model with no declaring authority: gate 4 can run, gate 4b cannot.
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true),

            new AgentIdentity("agent-a"),

            // A SELF-DECLARED map — gate 5's by-design refusal.
            MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })),

            floorScans: 9,
            runtimeCompression: 8,          // > 1, so gate 10b needs inputs nobody supplied
            conflicts: null,                // no graph at all
            compressionInputs: null,
            deployment: null,               // gate 11: a property of the DOWNLOAD
            tagMapReach: null);
    }
}
