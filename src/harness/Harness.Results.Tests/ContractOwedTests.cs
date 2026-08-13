using Harness.Gate;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// The four things the contract specified and the code did not do. The contract lane recorded each as a
/// known hole rather than papering over it; these are the closures, and each test is written against the
/// SYMPTOM the hole produced rather than against the fix.
/// </summary>
public class ContractOwedTests
{
    private const string ClauseId = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    private static AssertionEnumeration Enumeration() =>
        AssertionEnumeration.Of(new[] { ClauseId }, new[] { AssertionIdValue },
            new Dictionary<string, AssertionForm> { [AssertionIdValue] = AssertionForm.When },
            "agent-c",
            new Dictionary<string, string>(StringComparer.Ordinal) { [AssertionIdValue] = AssertionText },
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [AssertionIdValue] = new HashSet<string>(StringComparer.Ordinal) { "Demo_Count" },
            });

    private static SubmissionVector Vector(int? completionValue = 1, int comp = 1) =>
        new("V-1", "S0", 0, new AgentIdentity("agent-b"),
            new Basis(ClauseId, AssertionIdValue),
            new Dictionary<string, string> { ["Demo_Step"] = "5" },
            "Demo_Start",
            new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 40, "10") },
            AssertionForm.When,
            new SettlingDeclaration("count unchanged across 3 scans", new[] { "Demo_Count" }),
            MaxDurationScans: 20,
            CompletionValue: completionValue,
            new[] { new BlacklistEntry("FC_Other", "shares the plant model instance") },
            comp,
            new[] { "ramp-to-limit" },
            "Demo_Done",
            "a ramp that overshoots by one step");

    private static SubmissionReport Check(
        SubmissionVector? vector = null,
        int runtimeCompression = 1,
        BlockCompressionInputs? inputs = null) =>
        SubmissionGate.Check(
            new[] { vector ?? Vector() },
            Enumeration(),
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true),
            new AgentIdentity("agent-a"),
            MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })),
            floorScans: 9,
            runtimeCompression,
            ConflictGraph.Empty,
            inputs,

            // Contract 4.5, and TRUE of these fixtures rather than convenient: the mirror is %MW bit
            // memory, nothing here generates a data block, and the reachable set really is empty.
            new DeploymentDeclaration("fixture-import", Array.Empty<S7ObjectDeclaration>()),
            TagMapReach.Of(Array.Empty<S7Reach>()));

    private static GateResult Gate(SubmissionReport report, string startsWith) =>
        report.Gates.Single(g => g.Gate.StartsWith(startsWith, StringComparison.Ordinal));

    // ---------------------------------------------------------------------------------------------
    // 1 — THE COMPLETION VALUE. A live silent-wrong-answer defect, not a tightening.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>SlotRun</c> compares a result register against the completion value and reports
    /// <c>TIMED-OUT</c> otherwise. <b>With a default of 1, a block signalling completion with a state
    /// number was compared against a value nobody stated, and a HEALTHY block read as never having
    /// finished</b> — the author told the block is wrong when what is wrong is that nobody said what
    /// finished looks like.
    /// </summary>
    [Fact]
    public void A_VECTOR_WITH_NO_COMPLETION_VALUE_IS_REFUSED_RATHER_THAN_DEFAULTED_TO_ONE()
    {
        var report = Check(Vector(completionValue: null));

        var schema = Gate(report, "1 schema");
        Assert.False(schema.Passed);
        Assert.Contains("declares no completion VALUE", schema.Detail, StringComparison.Ordinal);
        Assert.Contains("state number", schema.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// A state number is the case the field exists for. It must be ADMITTED, or the fix would have
    /// replaced a wrong answer with a refusal of the right one.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(65535)]
    public void A_STATED_completion_value_anywhere_in_range_is_admitted(int value)
    {
        Assert.True(Gate(Check(Vector(completionValue: value)), "1 schema").Passed);
    }

    /// <summary>
    /// The wave casts it with <c>unchecked((ushort))</c>, so out of range does not fail — it silently
    /// becomes a different number, and the vector waits for a value nobody wrote.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(70000)]
    public void A_completion_value_outside_the_register_range_is_refused_because_the_cast_is_unchecked(int value)
    {
        var schema = Gate(Check(Vector(completionValue: value)), "1 schema");

        Assert.False(schema.Passed);
        Assert.Contains("UNCHECKED", schema.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void The_schema_report_says_NOT_STATED_rather_than_printing_a_number_nobody_gave()
    {
        Assert.Contains("<NOT STATED>", Gate(Check(Vector(completionValue: null)), "1 schema").Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 2 + 3 — the compression inputs reach the gate, and the ceiling keys on the RUNTIME factor
    // ---------------------------------------------------------------------------------------------

    private static BlockCompressionInputs Inputs(double? plantMs = 1000, double? budgetMs = 1000, double? compStable = null) =>
        new(plantMs, budgetMs, Array.Empty<TimerPreset>(), compStable, NegligibleFraction: 0.01);

    /// <summary>
    /// *** THE HOLE, AS A TEST. *** A wave at <c>runtimeCompression = 8</c> whose budget gives
    /// <c>comp_min = 1</c> was told nothing was being scaled, so the missing <c>comp_stable</c> did not
    /// bind and the submission passed — <b>while the model was being driven at 8x</b>.
    /// </summary>
    [Fact]
    public void A_WAVE_AT_COMP_8_WITH_COMP_MIN_1_AND_NO_COMP_STABLE_IS_REFUSED()
    {
        var report = Check(Vector(comp: 8), runtimeCompression: 8, inputs: Inputs(compStable: null));

        var gate = Gate(report, "10b time compression");
        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("comp_stable", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("the wave runs at comp=8", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// The mutation, as a test: the SAME submission with <c>comp_stable</c> declared is admitted. If the
    /// refusal above fired for any other reason, this would fail too.
    /// </summary>
    [Fact]
    public void And_the_same_wave_WITH_comp_stable_declared_clears_the_ceiling()
    {
        var gate = Gate(Check(Vector(comp: 8), runtimeCompression: 8, inputs: Inputs(compStable: 100)), "10b time compression");

        Assert.True(gate.Passed);
    }

    /// <summary>
    /// <c>comp_min</c> above 1 still binds when the wave runs at 1 — taking the MAXIMUM of the two can
    /// only tighten, and a plan needing 240x is asking a model to run at 240x whatever the wave is set to.
    /// </summary>
    [Fact]
    public void A_high_comp_min_still_binds_even_when_the_wave_runs_at_one()
    {
        var plan = TimeCompression.Plan(
            new CompressionRequest(240_000, 1_000,
                new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 40, "10") },
                DeclaredCompression: 1, SlotsPerPollCycle: 1, Presets: Array.Empty<TimerPreset>(),
                ModelCompStable: null, NegligibleFraction: 0.01, RuntimeCompression: 1),
            floorScans: 9);

        Assert.Equal(CompressionOutcome.NotComputable, plan.Outcome);
        Assert.Contains(plan.NotDeclared, b => b.Kind == CompressionBoundKind.Model);
    }

    [Fact]
    public void At_comp_one_on_both_axes_the_model_ceiling_cannot_bind_and_says_so()
    {
        var plan = TimeCompression.Plan(
            new CompressionRequest(1_000, 1_000,
                new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 40, "10") },
                DeclaredCompression: 1, SlotsPerPollCycle: 1, Presets: Array.Empty<TimerPreset>(),
                ModelCompStable: null, NegligibleFraction: 0.01, RuntimeCompression: 1),
            floorScans: 9);

        Assert.True(plan.Runnable);
        Assert.Contains(plan.Bounds, b => b.Kind == CompressionBoundKind.Model && b.Detail.Contains("nothing is being scaled", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------
    // 4 — plantMs / budgetMs nullable: a DIAGNOSTIC correction, not a hole
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>Both directions fail closed, so nothing was ever admitted wrongly.</b> What was wrong is the
    /// DIAGNOSIS: an omitted pair arrived as <c>0</c>, <c>Plan</c> threw, and the CLI reported the whole
    /// document unreadable — <c>NOTHING EXAMINED</c>, exit 2 — which sends the reader looking at the
    /// document instead of at the missing field.
    /// </summary>
    [Theory]
    [InlineData(null, 1000.0)]
    [InlineData(1000.0, null)]
    [InlineData(null, null)]
    public void An_incomplete_compression_input_is_NOT_CHECKED_and_NAMES_THE_FIELD(double? plantMs, double? budgetMs)
    {
        var report = Check(Vector(comp: 8), runtimeCompression: 8, inputs: Inputs(plantMs, budgetMs, compStable: 100));

        var gate = Gate(report, "10b time compression");
        Assert.Equal(GateStatus.NotChecked, gate.Status);
        Assert.Contains(plantMs is null ? "plantMs" : "budgetMs", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("The rest of the submission is fine", gate.Detail, StringComparison.Ordinal);

        // Still NOT ADMISSIBLE. The correction is which SENTENCE the reader gets, never whether it passes.
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);
    }

    [Fact]
    public void A_zero_budget_is_missing_rather_than_a_number_because_dividing_by_it_reports_infinity()
    {
        Assert.Contains(Inputs(1000, 0).Missing, m => m.Contains("budgetMs", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------
    // The document half — the fields exist and reach the gate
    // ---------------------------------------------------------------------------------------------

    private const string WithCompression = """
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 8,
      "slotsInWaveSet": 1,
      "resultRegistersPerSlot": 20,
      "computedConflicts": [],
      "model": { "id": "M_Ramp", "represents": ["ramp-to-limit"], "validatedAgainstPlantData": true, "compStable": 100 },
      "blockCompression": {
        "plantMs": 240000, "budgetMs": 30000, "negligibleFraction": 0.01,
        "presets": [{ "name": "T_Dwell", "presetMs": 60000, "source": "Data" }]
      },
      "enumeration": { "clauses": ["REQ-014"], "assertions": ["REQ-014:ffcc38"],
                       "forms": { "REQ-014:ffcc38": "When" }, "enumerator": "agent-c",
                       "normalisedTexts": { "REQ-014:ffcc38": "WHEN the step is applied THEN the count reaches the limit" },
                       "requiredObservations": { "REQ-014:ffcc38": ["Demo_Count"] } },
      "map": { "providedFor": { "Demo_Count": ["Latched"] } },
      "vectors": [{
        "id": "V-1", "slot": "S0", "index": 0, "author": "agent-b",
        "clause": "REQ-014", "assertion": "REQ-014:ffcc38",
        "startBool": "Demo_Start",
        "expectations": [{ "signal": "Demo_Count", "nature": "PersistentState", "mode": "Latched", "windowScans": 400, "expected": "10" }],
        "settlingCondition": "count unchanged across 3 scans", "settlingSignals": ["Demo_Count"],
        "maxDurationScans": 400,
        "blacklist": [{ "block": "FC_Other", "reason": "shares the plant model" }],
        "assertionForm": "When",
        "completionValue": 1,
        "compressionFactor": 8,
        "assertedBehaviours": ["ramp-to-limit"],
        "completionSignal": "Demo_Done",
        "kills": "a ramp that overshoots by one step"
      }]
    }
    """;

    private static (int Exit, string Output) Run(string json)
    {
        var writer = new StringWriter();
        var exit = GateCli.Run(new[] { "check", "sub.json" }, writer, _ => json);
        return (exit, writer.ToString());
    }

    /// <summary>
    /// <c>SubmissionGate.Check</c> has always taken <c>BlockCompressionInputs</c> and <b><c>Evaluate</c>
    /// never passed it</b>, so gate 10b reported NOT CHECKED from the CLI even for a submission that could
    /// have answered it.
    /// </summary>
    [Fact]
    public void THE_BLOCK_COMPRESSION_INPUTS_REACH_THE_GATE_THROUGH_THE_DOCUMENT()
    {
        var (_, output) = Run(WithCompression);

        Assert.Contains("] 10b time compression", output, StringComparison.Ordinal);
        Assert.DoesNotContain("[NOT CHECKED] 10b time compression", output, StringComparison.Ordinal);
        Assert.Contains("comp_min", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_WITHOUT_THEM_THE_SAME_COMPRESSED_DOCUMENT_REPORTS_NOT_CHECKED()
    {
        var without = WithCompression
            .Replace(", \"compStable\": 100", string.Empty, StringComparison.Ordinal)
            .Replace("\"blockCompression\"", "\"blockCompressionRemoved\"", StringComparison.Ordinal);

        var (exit, output) = Run(without);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("[NOT CHECKED] 10b time compression", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A preset that does not say DATA or LITERAL is refused, never guessed: the two push in OPPOSITE
    /// directions, so there is no fail-safe assumption available.
    /// </summary>
    [Fact]
    public void A_PRESET_WITH_NO_SOURCE_IS_REFUSED_BECAUSE_THE_TWO_ANSWERS_PUSH_OPPOSITE_WAYS()
    {
        var unstated = WithCompression.Replace("\"source\": \"Data\"", "\"source\": \"Unstated\"", StringComparison.Ordinal);

        var (exit, output) = Run(unstated);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("OPPOSITE directions", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// An omitted <c>plantMs</c> used to make the whole document unreadable. It must now be exit 1 with
    /// the field named, never exit 2.
    /// </summary>
    [Fact]
    public void AN_OMITTED_PLANT_MS_EXITS_ONE_NAMING_THE_FIELD_AND_NEVER_EXITS_TWO()
    {
        var incomplete = WithCompression.Replace("\"plantMs\": 240000, ", string.Empty, StringComparison.Ordinal);

        var (exit, output) = Run(incomplete);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.NotEqual(GateExit.NothingExamined, exit);
        Assert.Contains("plantMs", output, StringComparison.Ordinal);
        Assert.DoesNotContain("NOTHING EXAMINED", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_DOCUMENT_WITH_NO_COMPLETION_VALUE_IS_REFUSED_THROUGH_THE_CLI()
    {
        var missing = WithCompression.Replace("\"completionValue\": 1,", string.Empty, StringComparison.Ordinal);

        var (exit, output) = Run(missing);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("declares no completion VALUE", output, StringComparison.Ordinal);
    }
}
