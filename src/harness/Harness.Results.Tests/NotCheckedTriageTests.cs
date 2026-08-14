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
    /// *** GATE 5 WAS FILED "BY DESIGN" AND THE MEASUREMENT SAYS OTHERWISE. ***
    ///
    /// <para>The old classification reasoned that the observability map must come from an authority other
    /// than the submitter, so the gate reads NOT CHECKED for ever. <b>The REFUSAL of a self-declared map
    /// is indeed permanent and correct. The NOT CHECKED is not.</b> Driven with a loadable coordinator
    /// binding the gate RUNS and returns a verdict — measured 2026-08-14 against the live 27-vector
    /// submission, where supplying one turned it from NOT CHECKED into <c>REFUSED</c>, naming every
    /// signal the binding did not carry.</para>
    ///
    /// <para><b>So the remedy is an artifact, and an artifact that could exist is a build-list item.</b>
    /// What the measurement actually found is the useful half: the coordinator binding for this block
    /// EXISTS — as PROSE. A document that exists and cannot be loaded is not the same as one nobody
    /// wrote, and "by design" would have retired a transcription job as a law of nature.</para>
    /// </summary>
    [Fact]
    public void GATE_5_IS_AWAITING_AN_ARTIFACT_AND_THAT_WAS_ESTABLISHED_BY_RUNNING_IT()
    {
        var gate = MinimalSubmission().NotChecked.Single(g => g.Gate.StartsWith("5 observability", StringComparison.Ordinal));

        Assert.Equal(NotCheckedReason.AwaitingAnArtifactThatCouldExist, gate.Reason);
        Assert.True(gate.IsClosableOffline);
    }

    /// <summary>
    /// The other half of the same measurement: <b>supplying a binding makes gate 5 RUN.</b> Without this,
    /// the reclassification above rests on the classification constant rather than on the behaviour, and
    /// a gate that could not run whatever you passed it would still satisfy it.
    /// </summary>
    [Fact]
    public void AND_SUPPLYING_A_BINDING_SOURCED_MAP_MAKES_GATE_5_ACTUALLY_RUN()
    {
        var fromBindings = MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched }))
            with { Provenance = MapProvenance.Bindings };

        var gate = MinimalSubmission(map: fromBindings).Gates
            .Single(g => g.Gate.StartsWith("5 observability", StringComparison.Ordinal));

        Assert.NotEqual(GateStatus.NotChecked, gate.Status);
    }

    [Fact]
    public void The_reasons_are_distinguishable_and_the_zero_value_is_the_unusable_one()
    {
        // default(NotCheckedReason) must never be a real classification: a dropped reason has to fail
        // the same comparison a wrong one fails.
        Assert.Equal(NotCheckedReason.Unstated, default(NotCheckedReason));
        Assert.Equal(4, Enum.GetValues<NotCheckedReason>().Length);
    }

    /// <summary>
    /// *** THE CATEGORY IS RETIRED, AND THIS IS WHAT KEEPS IT RETIRED. ***
    ///
    /// <para><c>IndependentAuthorityByDesign</c> claimed some gates can never run because their input
    /// must come from an authority the submitter does not control. <b>Both of its members were measured
    /// and both run.</b> Gate 5 runs given a loadable coordinator binding; gate 4b runs given a fidelity
    /// declaration that names its declarer.</para>
    ///
    /// <para><b>An empty classification is a slot waiting to be misused</b> — the next gate that is
    /// merely INCONVENIENT to check gets filed under it, and "by design" is unfalsifiable once nobody
    /// remembers it was measured empty. So the member is gone, its number is left vacant, and this test
    /// makes re-adding one a deliberate act rather than a convenience.</para>
    /// </summary>
    [Fact]
    public void NO_REASON_CLAIMS_A_GATE_CAN_NEVER_RUN_BY_DESIGN()
    {
        var names = Enum.GetNames<NotCheckedReason>();

        Assert.DoesNotContain(names, n => n.Contains("ByDesign", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Never", StringComparison.OrdinalIgnoreCase));

        // The vacant number is not reused. A new member landing on 2 would silently inherit the slot the
        // retirement emptied — which is precisely the misuse the retirement was for.
        Assert.DoesNotContain(Enum.GetValues<NotCheckedReason>(), r => (int)r == 2);
    }

    /// <summary>
    /// The measurement that emptied the category, kept as a test so the reclassification rests on
    /// BEHAVIOUR rather than on the constant. A gate that could not run whatever you passed it would
    /// fail this.
    /// </summary>
    [Fact]
    public void GATE_4b_RUNS_THE_MOMENT_A_FIDELITY_DECLARATION_NAMES_ITS_DECLARER()
    {
        var declared = FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true)
            with { DeclaredBy = new AgentIdentity("agent-coordinator") };

        var gate = MinimalSubmission(fidelity: declared).Gates
            .Single(g => g.Gate.StartsWith("4b fidelity authority", StringComparison.Ordinal));

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed);
    }

    /// <summary>
    /// And the absence is now a BUILD-LIST item rather than a design property — the reclassification
    /// itself, pinned.
    /// </summary>
    [Fact]
    public void AND_AN_ABSENT_FIDELITY_DECLARATION_IS_NOW_A_BUILD_LIST_ITEM()
    {
        var gate = MinimalSubmission(omitFidelity: true).NotChecked
            .Single(g => g.Gate.StartsWith("4b fidelity authority", StringComparison.Ordinal));

        Assert.Equal(NotCheckedReason.AwaitingAnArtifactThatCouldExist, gate.Reason);
        Assert.True(gate.IsClosableOffline);
    }

    /// <summary>
    /// <b>Every reason the enum declares is actually reachable</b> from some gate — a classification
    /// nothing produces is a category nobody maintains. Checked against the union of two submissions so
    /// that a reason used only on a healthy one still counts.
    /// </summary>
    [Fact]
    public void Every_declared_reason_except_Unstated_is_produced_by_some_gate()
    {
        // *** THE UNION, WHICH IS WHAT THE COMMENT ABOVE ALWAYS SAID AND THE CODE DID NOT DO. *** It read
        // one submission, and passed only because gate 5 was then filed IndependentAuthorityByDesign.
        // Reclassifying gate 5 on the measurement left that category produced by exactly one path — 4b's
        // no-fidelity-at-all branch — which the default fixture does not drive. A one-submission union is
        // how a category comes to look maintained by a gate that no longer emits it.
        var produced = MinimalSubmission().NotChecked
            .Concat(MinimalSubmission(omitFidelity: true).NotChecked)
            .Select(g => g.Reason)
            .ToHashSet();

        foreach (var reason in Enum.GetValues<NotCheckedReason>().Where(r => r != NotCheckedReason.Unstated))
        {
            Assert.True(produced.Contains(reason),
                $"no gate produced {reason}. A classification nothing emits is a category nobody maintains, and the report's group for it prints nothing for ever.");
        }
    }

    /// <summary>
    /// <b>The category it guarded is now retired</b>, so what it pins is the property that replaced it:
    /// every reason the enum still declares names an artifact, or the rig.
    /// </summary>
    [Fact]
    public void EVERY_SURVIVING_REASON_NAMES_AN_ARTIFACT_OR_THE_RIG()
    {
        var produced = MinimalSubmission().NotChecked
            .Concat(MinimalSubmission(omitFidelity: true).NotChecked)
            .ToArray();

        Assert.NotEmpty(produced);

        Assert.All(produced, g => Assert.True(
            g.Reason is NotCheckedReason.RequiresTheDevice
                     or NotCheckedReason.AwaitingAnArtifactThatCouldExist
                     or NotCheckedReason.HarnessCapabilityMissing,
            $"{g.Gate} reported {g.Reason}. Since 2026-08-14 there is no reason meaning 'this can never run', "
            + "and a new one would have to be added deliberately — see NotCheckedReason's vacant slot 2."));
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

    private static SubmissionReport MinimalSubmission(MirrorObservability? map = null, FidelityDeclaration? fidelity = null, bool omitFidelity = false)
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
            omitFidelity ? null : fidelity ?? FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true),

            new AgentIdentity("agent-a"),

            // A SELF-DECLARED map by default — the vector author vouching for the artifact gate 5
            // exists to check them against.
            map ?? MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })),

            floorScans: 9,
            runtimeCompression: 8,          // > 1, so gate 10b needs inputs nobody supplied
            conflicts: null,                // no graph at all
            compressionInputs: null,
            deployment: null,               // gate 11: a property of the DOWNLOAD
            tagMapReach: null);
    }
}
