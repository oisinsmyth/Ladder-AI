using Harness.Gate;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// The CLI — <b>because a C# method is not a check an author can run.</b>
///
/// <para>`Admissibility.Check` existed and was correct, and no submission could be certified admissible
/// from a command line: reaching it meant writing a program. These tests drive <c>GateCli.Run</c>
/// directly rather than a process, which is why the decision half lives outside <c>Program</c>.</para>
/// </summary>
public class GateCliTests
{
    private const string Good = """
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 1,
      "resultRegistersPerSlot": 20,
      "computedConflicts": [],
      "model": { "id": "M_Ramp", "represents": ["ramp-to-limit"], "validatedAgainstPlantData": true },
      "enumeration": { "clauses": ["REQ-014"], "assertions": ["REQ-014:3f9a1c"] },
      "map": { "providedFor": { "Demo_Count": ["Latched"] } },
      "vectors": [{
        "id": "V-1", "slot": "S0", "index": 0, "author": "agent-b",
        "clause": "REQ-014", "assertion": "REQ-014:3f9a1c",
        "startBool": "Demo_Start",
        "expectations": [{ "signal": "Demo_Count", "nature": "PersistentState", "mode": "Latched", "windowScans": 0 }],
        "settlingCondition": "count unchanged across 3 scans", "settlingSignals": ["Demo_Count"],
        "maxDurationScans": 20,
        "blacklist": [{ "block": "FC_Other", "reason": "shares the plant model" }],
        "compressionFactor": 1,
        "assertedBehaviours": ["ramp-to-limit"],
        "completionSignal": "Demo_Done",
        "kills": "a ramp that overshoots by one step"
      }]
    }
    """;

    private static (int Exit, string Output) Run(string json, string[]? args = null)
    {
        var writer = new StringWriter();
        var exit = GateCli.Run(args ?? new[] { "check", "sub.json" }, writer, _ => json);
        return (exit, writer.ToString());
    }

    [Fact]
    public void A_complete_submission_exits_zero_and_says_ADMISSIBLE_SUBJECT_TO_JUDGEMENT()
    {
        var (exit, output) = Run(Good);

        Assert.Equal(GateExit.AdmissibleSubjectToJudgement, exit);
        Assert.Contains("VERDICT: ADMISSIBLE-SUBJECT-TO-JUDGEMENT", output, StringComparison.Ordinal);
        Assert.Contains("no plain ADMISSIBLE", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_report_names_the_VERIFIER_for_every_gate_so_a_check_cannot_be_claimed_without_one()
    {
        var (_, output) = Run(Good);

        Assert.Contains("(by ObservabilityCheck)", output, StringComparison.Ordinal);
        Assert.Contains("(by AgentIdentity)", output, StringComparison.Ordinal);
        Assert.Contains("(by Admissibility)", output, StringComparison.Ordinal);
        Assert.Contains("(by none, ever)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_refused_gate_exits_one()
    {
        var (exit, output) = Run(Good.Replace("\"author\": \"agent-b\"", "\"author\": \"AGENT-A \"", StringComparison.Ordinal));

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("VERDICT: NOT ADMISSIBLE", output, StringComparison.Ordinal);
        Assert.Contains("[REFUSED   ] 2 authorship", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_gate_that_could_not_run_exits_one_and_prints_the_BUILD_LIST()
    {
        // The conflict graph is absent, so the blacklist gate is NOT CHECKED. That is not a pass, and
        // the report says what is missing rather than what happened.
        var (exit, output) = Run(Good.Replace("\"computedConflicts\": [],", string.Empty, StringComparison.Ordinal));

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("[NOT CHECKED] 8 blacklist", output, StringComparison.Ordinal);
        Assert.Contains("NOT CHECKED — what is missing (this is the build list)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_EMPTY_SUBMISSION_EXITS_TWO_AND_NEVER_ZERO()
    {
        // The purest form of a gate that passed without looking at anything. Its own exit code.
        var (exit, output) = Run("""{ "vectors": [] }""");

        Assert.Equal(GateExit.NothingExamined, exit);
        Assert.Contains("VERDICT: NOTHING EXAMINED", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_document_exits_two_rather_than_being_treated_as_admissible()
    {
        var (exit, output) = Run("{ this is not json");

        Assert.Equal(GateExit.NothingExamined, exit);
        Assert.Contains("NOTHING EXAMINED", output, StringComparison.Ordinal);
        Assert.Contains("Empty is not clean", output, StringComparison.Ordinal);
    }

    [Fact]
    public void No_arguments_prints_usage_and_exits_two()
    {
        var (exit, output) = Run(Good, Array.Empty<string>());

        Assert.Equal(GateExit.NothingExamined, exit);
        Assert.Contains("usage: harness-gate check", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_instrumentation_mode_in_the_document_is_NOTHING_EXAMINED_not_a_silent_default()
    {
        var (exit, output) = Run(Good.Replace("[\"Latched\"]", "[\"Sticky\"]", StringComparison.Ordinal));

        Assert.Equal(GateExit.NothingExamined, exit);
        Assert.Contains("is not an instrumentation mode", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_sampled_NEVER_assertion_is_refused_by_the_command()
    {
        // F-3, ruled: sampled is admissible for PersistentState only, and never for a NEVER assertion.
        // The document says which form it cites; omitting the field gets `When`, so an author who means
        // NEVER must say so - and saying so is what makes the refusal reachable.
        var never = Good
            .Replace("\"mode\": \"Latched\", \"windowScans\": 0", "\"mode\": \"Sampled\", \"windowScans\": 100", StringComparison.Ordinal)
            .Replace("[\"Latched\"]", "[\"Sampled\"]", StringComparison.Ordinal)
            .Replace("\"compressionFactor\": 1", "\"assertionForm\": \"Never\", \"compressionFactor\": 1", StringComparison.Ordinal);

        var (exit, output) = Run(never);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("SampledCannotAnswerANeverAssertion", output, StringComparison.Ordinal);

        // The same vector as a WHEN is admissible, so the refusal is about the FORM and nothing else.
        Assert.Equal(GateExit.AdmissibleSubjectToJudgement,
            Run(never.Replace("\"assertionForm\": \"Never\", ", string.Empty, StringComparison.Ordinal)).Exit);
    }

    [Fact]
    public void The_report_carries_the_ESCALATIONS_the_submission_may_turn_on()
    {
        var (_, output) = Run(Good);

        Assert.Contains("section 9.1", output, StringComparison.Ordinal);
        Assert.Contains("section 9.4", output, StringComparison.Ordinal);
        Assert.Contains("what MAKES two agents different is undefined", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_floor_is_COMPUTED_FROM_THE_WAVE_SET_and_a_wider_one_refuses_a_window_a_narrow_one_admits()
    {
        // The same vector, the same declared window, two wave-set widths. The floor is a property of the
        // set, not of the vector, and the CLI derives it rather than taking it.
        var sampled = Good
            .Replace("\"mode\": \"Latched\", \"windowScans\": 0", "\"mode\": \"Sampled\", \"windowScans\": 12", StringComparison.Ordinal)
            .Replace("[\"Latched\"]", "[\"Sampled\"]", StringComparison.Ordinal);

        Assert.Equal(GateExit.AdmissibleSubjectToJudgement, Run(sampled).Exit);

        var wide = sampled
            .Replace("\"slotsInWaveSet\": 1", "\"slotsInWaveSet\": 12", StringComparison.Ordinal)
            .Replace("\"resultRegistersPerSlot\": 20", "\"resultRegistersPerSlot\": 125", StringComparison.Ordinal);

        Assert.Equal(GateExit.NotAdmissible, Run(wide).Exit);
    }
}
