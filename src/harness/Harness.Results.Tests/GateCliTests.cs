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
      "enumeration": { "clauses": ["REQ-014"], "assertions": ["REQ-014:ffcc38"],
                       "forms": { "REQ-014:ffcc38": "When" }, "enumerator": "agent-c",
                       "normalisedTexts": { "REQ-014:ffcc38": "WHEN the step is applied THEN the count reaches the limit" },
                       "requiredObservations": { "REQ-014:ffcc38": ["Demo_Count"] } },
      "map": { "providedFor": { "Demo_Count": ["Latched"] } },
      "vectors": [{
        "id": "V-1", "slot": "S0", "index": 0, "author": "agent-b",
        "clause": "REQ-014", "assertion": "REQ-014:ffcc38",
        "startBool": "Demo_Start",
        "expectations": [{ "signal": "Demo_Count", "nature": "PersistentState", "mode": "Latched", "windowScans": 0, "expected": "10" }],
        "settlingCondition": "count unchanged across 3 scans", "settlingSignals": ["Demo_Count"],
        "maxDurationScans": 20,
        "blacklist": [{ "block": "FC_Other", "reason": "shares the plant model" }],
        "assertionForm": "When",
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
            .Replace("\"REQ-014:ffcc38\": \"When\"", "\"REQ-014:ffcc38\": \"Never\"", StringComparison.Ordinal)
            .Replace("\"assertionForm\": \"When\"", "\"assertionForm\": \"Never\"", StringComparison.Ordinal);

        var (exit, output) = Run(never);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("SampledCannotAnswerANeverAssertion", output, StringComparison.Ordinal);

        // The same vector as a WHEN is admissible, so the refusal is about the FORM and nothing else.
        // The same vector as a WHEN - in the vector AND in the enumeration, since they must agree - is
        // admissible, so the refusal is about the FORM and nothing else.
        Assert.Equal(GateExit.AdmissibleSubjectToJudgement,
            Run(never
                .Replace("\"assertionForm\": \"Never\"", "\"assertionForm\": \"When\"", StringComparison.Ordinal)
                .Replace("\"REQ-014:ffcc38\": \"Never\"", "\"REQ-014:ffcc38\": \"When\"", StringComparison.Ordinal)).Exit);
    }

    // ---------------------------------------------------------------------------------------------
    // The two fields, END TO END through the document — three lanes hit this independently
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_DOCUMENT_CARRYING_forms_AND_enumerator_MAKES_3d_AND_3e_GENUINELY_CHECKED()
    {
        // The checks were built and the DOCUMENT could not carry what they needed, so from the CLI the
        // independence gate always reported NotChecked and the form cross-check could never fire.
        var (_, output) = Run(Good);

        Assert.Contains("[CHECKED   ] 3d enumerator independence", output, StringComparison.Ordinal);
        Assert.Contains("[CHECKED   ] 3e assertion form authority", output, StringComparison.Ordinal);
    }

    [Fact]
    public void And_WITHOUT_them_the_same_document_reports_NOT_CHECKED_rather_than_passing()
    {
        // The flat projection: clause and assertion IDs, and nothing else. THREE gates cannot run, and
        // NOT CHECKED fails closed for every one of them.
        var flat = """
        {
          "blockAuthor": "agent-a",
          "runtimeCompression": 1,
          "slotsInWaveSet": 1,
          "resultRegistersPerSlot": 20,
          "computedConflicts": [],
          "model": { "id": "M_Ramp", "represents": ["ramp-to-limit"], "validatedAgainstPlantData": true },
          "enumeration": { "clauses": ["REQ-014"], "assertions": ["REQ-014:ffcc38"] },
          "map": { "providedFor": { "Demo_Count": ["Latched"] } },
          "vectors": [{
            "id": "V-1", "slot": "S0", "index": 0, "author": "agent-b",
            "clause": "REQ-014", "assertion": "REQ-014:ffcc38",
            "startBool": "Demo_Start",
            "expectations": [{ "signal": "Demo_Count", "nature": "PersistentState", "mode": "Latched", "windowScans": 0, "expected": "10" }],
            "settlingCondition": "count unchanged across 3 scans", "settlingSignals": ["Demo_Count"],
            "maxDurationScans": 20,
            "blacklist": [{ "block": "FC_Other", "reason": "shares the plant model" }],
            "assertionForm": "When",
            "completionValue": 1,
            "compressionFactor": 1,
            "assertedBehaviours": ["ramp-to-limit"],
            "completionSignal": "Demo_Done",
            "kills": "a ramp that overshoots by one step"
          }]
        }
        """;

        var (exit, output) = Run(flat);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("[NOT CHECKED] 3d enumerator independence", output, StringComparison.Ordinal);
        Assert.Contains("[NOT CHECKED] 3e assertion form authority", output, StringComparison.Ordinal);

        // *** AND 3g, WHICH IS THE ONE THAT KEEPS THE STAMPER UNTRUSTED. *** Without normalised text no
        // ID can be recomputed, so a hand-written hex string is indistinguishable from a computed one.
        Assert.Contains("[NOT CHECKED] 3g assertion IDs recompute", output, StringComparison.Ordinal);
        Assert.Contains("TAKEN ON TRUST", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// *** AN ID THAT DOES NOT RECOMPUTE IS REFUSED THROUGH THE DOCUMENT. ***
    ///
    /// <para>This is the whole reason §3.4 can let the stamper be anybody: a wrong hash is caught. Here
    /// the text is edited and the ID left alone — the exact "preserved an ID whose text changed" defect
    /// the stamper refuses to commit, arriving from the other direction.</para>
    /// </summary>
    [Fact]
    public void AN_ID_THAT_DOES_NOT_RECOMPUTE_FROM_ITS_OWN_TEXT_IS_REFUSED()
    {
        var edited = Good.Replace(
            "WHEN the step is applied THEN the count reaches the limit",
            "WHEN the step is applied THEN the count reaches the limit within the window",
            StringComparison.Ordinal);

        var (exit, output) = Run(edited);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("DOES NOT RECOMPUTE", output, StringComparison.Ordinal);
        Assert.Contains("STALE", output, StringComparison.Ordinal);
    }

    /// <summary>A citation in the display-ordinal form is rejected by SHAPE, not by lookup (§3.3).</summary>
    [Fact]
    public void A_CITATION_IN_THE_DISPLAY_ORDINAL_FORM_IS_REJECTED_BY_SHAPE()
    {
        var ordinal = Good.Replace("\"assertion\": \"REQ-014:ffcc38\"", "\"assertion\": \"REQ-014.A2\"", StringComparison.Ordinal);

        var (exit, output) = Run(ordinal);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("DISPLAY ORDINAL", output, StringComparison.Ordinal);
        Assert.Contains("POSITIONAL", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_ENUMERATOR_WHO_IS_THE_BLOCKS_AUTHOR_IS_REFUSED_THROUGH_THE_DOCUMENT()
    {
        var (exit, output) = Run(Good.Replace("\"enumerator\": \"agent-c\"", "\"enumerator\": \"agent-a\"", StringComparison.Ordinal));

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("[REFUSED   ] 3d enumerator independence", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_ONE_WHO_IS_A_VECTORS_AUTHOR_IS_TOO()
    {
        // The gate compares against BlockAuthor AND every vector's author, normalised.
        var (exit, output) = Run(Good.Replace("\"enumerator\": \"agent-c\"", "\"enumerator\": \"AGENT-B \"", StringComparison.Ordinal));

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("THIRD party to both authors", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_VECTOR_THAT_OMITS_ITS_FORM_IS_REFUSED_AND_THIS_IS_ITS_OWN_CASE()
    {
        // *** THE NOT-DECLARED CASE, TESTED SEPARATELY FROM THE MIS-DECLARED ONE. *** The field used to
        // default to When on the reasoning that WHEN is checked hardest; the form decides whether a
        // SAMPLED observation is admissible, so an author who omitted it was handed the permissive path.
        // The zero value is Unstated and it fails the same comparison a wrong form fails.
        var omitted = Good.Replace("\"assertionForm\": \"When\",", string.Empty, StringComparison.Ordinal);

        var (exit, output) = Run(omitted);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("declares no assertion form", output, StringComparison.Ordinal);
        Assert.Contains("A DROPPED FORM FAILS THE SAME COMPARISON AS A WRONG ONE", output, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_COMPLETION_VALUE_REACHES_THE_GATE_FROM_THE_DOCUMENT_and_the_report_states_it()
    {
        // *** ALSO FOUND BY MUTATION. *** The CLI carried the field and nothing in the report mentioned
        // it, so passing a literal 1 instead of the document's value left every test green. Contract
        // section 2 defines no completion VALUE, so the gate now says what it read - which is both the
        // honest treatment of an undefined field and the thing that makes the pass-through checkable.
        var (_, output) = Run(Good.Replace("\"completionSignal\": \"Demo_Done\"", "\"completionSignal\": \"Demo_Done\", \"completionValue\": 7", StringComparison.Ordinal));

        Assert.Contains("Demo_Done reads 7", output, StringComparison.Ordinal);
        Assert.Contains("states no VALUE, so this is reported rather than assumed", output, StringComparison.Ordinal);
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

    // ---------------------------------------------------------------------------------------------
    // THE PASS-THROUGHS — the class of hole this component has now found seven times
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_EXPECTED_VALUE_REACHES_THE_GATE_FROM_THE_DOCUMENT()
    {
        // The field existed on the checked type and the DOCUMENT had no way to state one, so every
        // CLI-supplied expectation arrived with a null predicate and nothing noticed. Dropping the
        // pass-through again turns the good fixture red, which is the property that was missing.
        var withoutPredicate = Good.Replace(", \"expected\": \"10\"", string.Empty, StringComparison.Ordinal);
        var (exit, output) = Run(withoutPredicate);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("declares no expected value", output, StringComparison.Ordinal);

        // And with it, the same document is admissible — so the pass-through is load-bearing for a PASS
        // and not only for a refusal.
        Assert.Equal(GateExit.AdmissibleSubjectToJudgement, Run(Good).Exit);
    }

    [Fact]
    public void A_CONFLICT_EDGES_ENTRY_REACHES_THE_GATE_AND_ITS_SIGNAL_IS_NAMED_IN_THE_REPORT()
    {
        var withEdge = Good.Replace(
            "\"computedConflicts\": [],",
            """
            "conflictEdges": [{ "blockA": "FC_PumpA", "blockB": "FC_PumpB", "provenance": "MultiWriter",
                                "signal": "Pump_Run", "class": "Deliverable" }],
            """,
            StringComparison.Ordinal);

        var (exit, output) = Run(withEdge);

        Assert.Equal(GateExit.AdmissibleSubjectToJudgement, exit);
        Assert.Contains("Pump_Run", output, StringComparison.Ordinal);
        Assert.Contains("MULTI-WRITER FINDING(S) ON DELIVERABLE SIGNALS", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_BARE_CONFLICT_LIST_IS_NOT_ADMISSIBLE_because_its_multi_writer_report_would_mean_nothing()
    {
        var bareList = Good.Replace("\"computedConflicts\": [],", "\"computedConflicts\": [\"FC_Other\"],", StringComparison.Ordinal);

        var (exit, output) = Run(bareList);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("8c multi-writer provenance (X-G)", output, StringComparison.Ordinal);
        Assert.Contains("nothing to do with multi-writers", output, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_RUNTIME_COMPRESSION_REACHES_BOTH_COMPRESSION_GATES()
    {
        var compressed = Good.Replace("\"runtimeCompression\": 1", "\"runtimeCompression\": 4", StringComparison.Ordinal);

        var (exit, output) = Run(compressed);

        // 10b fails closed above comp=1: three of X-D's four ceilings are properties of the block and the
        // model, and contract section 2 gives an author nowhere to state them.
        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("10b time compression", output, StringComparison.Ordinal);
        Assert.Contains("OFTEN BINDS FIRST", output, StringComparison.Ordinal);

        // And the timer ceiling that binds first is quoted at its MEASURED value, not X-D's original.
        Assert.Contains("4.3x", output, StringComparison.Ordinal);
    }
}
