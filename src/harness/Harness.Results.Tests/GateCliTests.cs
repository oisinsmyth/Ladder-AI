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
    /// <summary>
    /// <b><c>internal</c> so the gate-0c tests can reuse the ONE known-admissible submission.</b> A second
    /// fixture written to be admissible is a second thing that can quietly stop being admissible, and
    /// then those tests pass while measuring nothing.
    /// </summary>
    internal const string Good = """
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 1,
      "resultRegistersPerSlot": 20,
      // Gate 1b: where the scenario ends, so MaxDuration is bounded by something. 400 ms is 17 scans, so
      // the 20-scan maxDuration clears 1b's floor and sits far under its ceiling.
      "scenarioEndInput": "Demo_EndAt",
      "computedConflicts": [],
      "model": { "id": "M_Ramp", "represents": ["ramp-to-limit"], "validatedAgainstPlantData": true, "declaredBy": "agent-m" },
      "enumeration": { "clauses": ["REQ-014"], "assertions": ["REQ-014:ffcc38"],
                       "forms": { "REQ-014:ffcc38": "When" }, "enumerator": "agent-c",
                       "normalisedTexts": { "REQ-014:ffcc38": "WHEN the step is applied THEN the count reaches the limit" },
                       "requiredObservations": { "REQ-014:ffcc38": ["Demo_Count"] },
                       "bounds": { "ramp_limit": "10" } },
      // *** Sampled, NOT Latched, AND THAT IS A FINDING RATHER THAN A FIXTURE TWEAK. *** The minimal copy
      // layer emits result-register MOVEs and no per-signal latch, so `Latched` on a RESULT was never
      // available — and this fixture claimed it for as long as the CLI took the map from the submission.
      // With --binding the map is derived from what the copy layer actually provides, and the claim fails.
      "map": { "providedFor": { "Demo_Count": ["Sampled"] },
                // 2.7: WHERE the signal lives. providedFor says only HOW it is watched.
                "storage": { "Demo_Count": { "owner": "DemoUnit", "path": "Demo_Count" } } },
      "tagMapPath": "tags.json",
      "deployment": {
        "importStamp": "import-A",
        "s7Objects": [{ "area": "DB_HarnessMarker", "dbNumber": 100, "harnessObject": "DB_HarnessMarker",
                        "layout": "Standard", "layoutSetAfterImport": "import-A" }]
      },
      "vectors": [{
        "id": "V-1", "slot": "S0", "index": 0, "author": "agent-b",
        "clause": "REQ-014", "assertion": "REQ-014:ffcc38",
        "startBool": "Demo_Start",
        "inputs": { "Demo_EndAt": "400" },
        "expectations": [{ "signal": "Demo_Count", "nature": "PersistentState", "mode": "Sampled", "windowScans": 20, "expected": "10" }],
        "settlingCondition": "count unchanged across 3 scans", "settlingSignals": ["Demo_Count"],
        "maxDurationScans": 20,
        "blacklist": [{ "block": "FC_Other", "reason": "shares the plant model" }],
        "assertionForm": "When",
        "completionValue": 1,
        "compressionFactor": 1,
        "assertedBehaviours": ["ramp-to-limit"],
        "completionSignal": "Demo_Done",
        "kills": "a ramp that overshoots by one step",
        "boundsUsed": { "ramp_limit": "10" }
      }]
    }
    """;

    /// <summary>
    /// The tag map gate 11's set-difference is computed FROM. A real one, read by the same reader the
    /// transport uses — a declared reachable set would be the author vouching for the artifact the gate
    /// exists to check them against.
    /// </summary>
    internal const string TagMap = """
    { "tags": [ { "name": "Marker_Build", "area": "DB_HarnessMarker", "db": 100, "byte": 0, "type": "DInt" } ] }
    """;

    /// <summary>
    /// The coordinator's bindings — <b>what makes this CLI as strong as the loop.</b>
    ///
    /// <para>🔴 Gate 5 compares what a vector asks to observe against what the copy layer PROVIDES. Taken
    /// from the submission, that map is the vector author vouching for the artifact the gate exists to
    /// check them against — and this CLI is consulted FIRST, so being quietly permissive there is worse
    /// than not running. Every test below therefore passes <c>--binding</c>; the one that does not is the
    /// test of the refusal.</para>
    /// </summary>
    internal const string Binding = """
    {
      "slots": [{
        "slotId": "S0",
        "vectorTargets": [{ "tag": "Demo_Step", "type": "Int" }],
        "startCondition": "Demo_Start",
        "resultSources": [{ "tag": "Demo_Count", "type": "Int", "specName": "Demo_Count" }]
      }]
    }
    """;

    /// <param name="derive">
    /// Stamp provenance first, so the fixture is a DERIVED submission rather than an authored one.
    /// <b>Default true, because that is now what an admissible submission looks like</b> — gate 0c refuses
    /// a derivable field carrying no record, and these tests are about the OTHER gates. The tests that
    /// exercise 0c itself pass <c>false</c> and assert the refusal by name.
    /// </param>
    private static (int Exit, string Output) Run(string json, string[]? args = null, bool derive = true)
    {
        var writer = new StringWriter();
        var submission = derive ? DerivedFixture.WithDerivation(json, "binding.json", Binding) : json;

        var exit = GateCli.Run(args ?? new[] { "check", "sub.json", "--binding", "binding.json" }, writer,
            path => path switch
            {
                "tags.json" => TagMap,
                "binding.json" => Binding,
                _ => submission,
            });
        return (exit, writer.ToString());
    }

    [Fact]
    public void WITHOUT_A_BINDING_THE_CLI_REFUSES_TO_BE_THE_DECIDING_VOICE_ON_GATE_5()
    {
        // *** THE AUTHORITY GAP, MEASURED AND CLOSED. *** GateCli took its map from the submission while
        // LoopRun took the same map from the coordinator's bindings, so the standalone tool was WEAKER
        // than the loop in exactly the place the tool decides whether to proceed. It is also why nothing
        // mechanical could see a stale binding row.
        var writer = new StringWriter();
        var exit = GateCli.Run(new[] { "check", "sub.json" }, writer,
            path => string.Equals(path, "tags.json", StringComparison.Ordinal) ? TagMap : Good);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("SELF-DECLARED MAP", writer.ToString(), StringComparison.Ordinal);
        Assert.Contains("NOT CHECKED", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void And_WITH_the_binding_the_same_submission_is_admissible()
    {
        // The did-not-run half: a fence that refuses everything passes every test that only checks
        // refusals. Supplying the coordinator's bindings must actually restore the pass.
        Assert.Equal(GateExit.AdmissibleSubjectToJudgement, Run(Good).Exit);
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

        // *** AND IT PRINTS WHICH KIND OF NOT CHECKED, NOT JUST HOW MANY. *** A flat count is the shape
        // that reads as a pass to a tired reader; the blacklist gate's silence is a build-list item, and
        // the report has to say so rather than filing it beside gate 11, which needs a controller.
        Assert.Contains("FOUR DIFFERENT FACTS", output, StringComparison.Ordinal);
        Assert.Contains("AWAITING AN ARTIFACT THAT COULD EXIST", output, StringComparison.Ordinal);
        Assert.Contains("- 8 blacklist: needs", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The three groups a reader must never conflate, printed distinctly on one report: <b>by design</b>
    /// (not a gap), <b>requires the device</b> (no artifact closes it), and <b>awaiting an artifact</b>
    /// (the build list). And an UNCLASSIFIED group must not appear at all — that one is a defect in a
    /// gate rather than in the submission.
    /// </summary>
    [Fact]
    public void THE_NOT_CHECKED_GROUPS_ARE_PRINTED_SEPARATELY_AND_NONE_IS_UNCLASSIFIED()
    {
        // The flat projection with no deployment: gate 5 (by design), gate 11 (device), and several
        // enumeration-fed gates (build list) all report NOT CHECKED at once.
        var (_, output) = Run(Good
            .Replace("\"computedConflicts\": [],", string.Empty, StringComparison.Ordinal)
            .Replace("\"deployment\"", "\"deploymentRemoved\"", StringComparison.Ordinal));

        // This fixture supplies specNames, so gate 5 RUNS here — its by-design refusal is pinned
        // directly in NotCheckedTriageTests rather than asserted through a fixture that does not trip it.
        Assert.Contains("REQUIRES THE DEVICE", output, StringComparison.Ordinal);
        Assert.Contains("AWAITING AN ARTIFACT THAT COULD EXIST", output, StringComparison.Ordinal);

        // The two groups are SEPARATE sections, not one list: gate 11 needs a controller and the
        // blacklist needs an artifact, and a reader must not be able to file them together.
        var device = output.IndexOf("REQUIRES THE DEVICE", StringComparison.Ordinal);
        var awaiting = output.IndexOf("AWAITING AN ARTIFACT THAT COULD EXIST", StringComparison.Ordinal);
        Assert.True(device < awaiting, "the device-bound group must print before the build list: it is the one nothing offline can close.");
        Assert.Contains("11 memory layout", output[device..awaiting], StringComparison.Ordinal);
        Assert.DoesNotContain("8 blacklist", output[device..awaiting], StringComparison.Ordinal);

        // *** THE ONE THAT MUST NEVER PRINT. *** It only appears when a gate failed to say which kind of
        // NOT CHECKED it is, which is the entry the whole grouping exists to make impossible.
        Assert.DoesNotContain("UNCLASSIFIED", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// *** AN UNASKED QUESTION AND AN ANSWERED ONE MUST NOT PRINT ALIKE. ***
    ///
    /// <para>Found live: a <c>Never</c> expectation was flipped to <c>Sampled</c> in a scratch copy and
    /// <b>the gate output did not move</b>, because gate 5 is NOT CHECKED whenever the map is
    /// self-declared — so F-3's refusal is masked and the vector would slip through today, to be refused
    /// the moment real bindings arrive. Reading "gate 5 did not complain" was reading a question nobody
    /// asked.</para>
    /// </summary>
    [Fact]
    public void A_NOT_CHECKED_GATE_IS_MARKED_IN_THE_MARGIN_AND_COUNTED_BEFORE_THE_LIST()
    {
        var (_, output) = Run(Good.Replace("\"computedConflicts\": [],", string.Empty, StringComparison.Ordinal));

        // Counted BEFORE the gate list, so a reader meets it before the lines it qualifies.
        var banner = output.IndexOf("GATE(S) WERE NOT CHECKED", StringComparison.Ordinal);
        var gates = output.IndexOf("GATES", StringComparison.Ordinal);
        Assert.True(banner >= 0 && banner < gates, "the NOT CHECKED count must precede the gate list, not follow it.");

        Assert.Contains("A GATE THAT DID NOT RUN DID NOT PASS", output, StringComparison.Ordinal);

        // Marked in the LEFT MARGIN, which is what a reader skimming for trouble scans — and NOT marked
        // on the passing lines, or the marker means nothing.
        Assert.Contains("!![NOT CHECKED] 8 blacklist", output, StringComparison.Ordinal);
        Assert.Contains("  [CHECKED   ] 1 schema", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_REFUSED_GATE_IS_MARKED_DIFFERENTLY_FROM_ONE_THAT_NEVER_RAN()
    {
        var refused = Run(Good.Replace("\"kills\": \"a ramp that overshoots by one step\"", "\"kills\": \"\"", StringComparison.Ordinal)).Output;

        Assert.Contains(">>[REFUSED   ] 1 schema", refused, StringComparison.Ordinal);
        Assert.DoesNotContain("!![REFUSED", refused, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gate 4b, through the document: the model declaration must name an authority the vector author
    /// does not control, or the list that licenses every asserted behaviour is self-issued.
    /// </summary>
    [Fact]
    public void A_MODEL_DECLARED_BY_THE_VECTORS_OWN_AUTHOR_IS_REFUSED()
    {
        var selfIssued = Good.Replace("\"declaredBy\": \"agent-m\"", "\"declaredBy\": \"agent-b\"", StringComparison.Ordinal);

        var (exit, output) = Run(selfIssued);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("SUPPLIED BY THE PARTY WHOSE VECTORS IT LICENSES", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_MODEL_WITH_NO_DECLARER_IS_NOT_CHECKED_RATHER_THAN_PASSING()
    {
        var anonymous = Good.Replace(", \"declaredBy\": \"agent-m\"", string.Empty, StringComparison.Ordinal);

        var (exit, output) = Run(anonymous);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("!![NOT CHECKED] 4b fidelity authority", output, StringComparison.Ordinal);
        Assert.Contains("only inside the vector file", output, StringComparison.Ordinal);
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
        var (exit, output) = Run(Good.Replace("[\"Sampled\"]", "[\"Sticky\"]", StringComparison.Ordinal));

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
            .Replace("\"windowScans\": 20", "\"windowScans\": 100", StringComparison.Ordinal)
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
          "model": { "id": "M_Ramp", "represents": ["ramp-to-limit"], "validatedAgainstPlantData": true, "declaredBy": "agent-m" },
          "enumeration": { "clauses": ["REQ-014"], "assertions": ["REQ-014:ffcc38"] },
          "map": { "providedFor": { "Demo_Count": ["Latched"] } },
          "tagMapPath": "tags.json",
          "deployment": {
            "importStamp": "import-A",
            "s7Objects": [{ "area": "DB_HarnessMarker", "dbNumber": 100, "harnessObject": "DB_HarnessMarker",
                            "layout": "Standard", "layoutSetAfterImport": "import-A" }]
          },
          "vectors": [{
            "id": "V-1", "slot": "S0", "index": 0, "author": "agent-b",
            "clause": "REQ-014", "assertion": "REQ-014:ffcc38",
            "startBool": "Demo_Start",
        "inputs": { "Demo_EndAt": "400" },
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
            .Replace("\"windowScans\": 20", "\"windowScans\": 12", StringComparison.Ordinal);

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
    public void BOTH_HALVES_OF_AMB_19_REACH_THE_GATE_FROM_THE_DOCUMENT()
    {
        // The table and the vector's declaration are two separate pass-throughs, and dropping EITHER has
        // to turn the good fixture red — a check wired on one side only is a check whose other side is
        // decoration. Tested by removal rather than by inspection, so the wiring cannot rot silently.
        var noTable = Good.Replace("\"bounds\": { \"ramp_limit\": \"10\" }", "\"boundsRemoved\": {}", StringComparison.Ordinal);
        Assert.NotEqual(Good, noTable);
        Assert.Equal(GateExit.NotAdmissible, Run(noTable).Exit);
        Assert.Contains("AN ABSENT TABLE IS NOT AN AGREEING ONE", Run(noTable).Output, StringComparison.Ordinal);

        var noDeclaration = Good.Replace("\"boundsUsed\": { \"ramp_limit\": \"10\" }", "\"boundsUsedRemoved\": {}", StringComparison.Ordinal);
        Assert.NotEqual(Good, noDeclaration);
        Assert.Equal(GateExit.NotAdmissible, Run(noDeclaration).Exit);
        Assert.Contains("CANNOT BE FOUND STALE BY ANYTHING", Run(noDeclaration).Output, StringComparison.Ordinal);

        // And the retune itself, end to end through the CLI: the ONLY edit is the table's value, so
        // nothing else in the document can account for the refusal.
        var retuned = Good.Replace("\"bounds\": { \"ramp_limit\": \"10\" }", "\"bounds\": { \"ramp_limit\": \"30\" }", StringComparison.Ordinal);
        var (exit, output) = Run(retuned);

        Assert.Equal(GateExit.NotAdmissible, exit);
        Assert.Contains("STALE, NOT FAILED", output, StringComparison.Ordinal);
        Assert.Contains("Do NOT edit the block", output, StringComparison.Ordinal);

        Assert.Equal(GateExit.AdmissibleSubjectToJudgement, Run(Good).Exit);
    }

    [Fact]
    public void THE_EMPTY_BOUNDS_CLAIM_AND_ITS_ENUMERATION_RELATION_BOTH_REACH_THE_GATE_FROM_THE_DOCUMENT()
    {
        // *** A FIELD NOBODY CAN SET IS A FIELD THAT DOES NOT EXIST *** — this repo has recorded that
        // three times — so the new relation is tested through the DOCUMENT, by removal, not by reading
        // the projection code. All three states are driven from JSON alone.
        var claimsNone = Good.Replace("\"boundsUsed\": { \"ramp_limit\": \"10\" }", "\"boundsUsed\": {}", StringComparison.Ordinal);
        Assert.NotEqual(Good, claimsNone);

        // (1) The claim with NO relation to check it against: NOT CHECKED, and the repair named is the
        //     enumeration's — never "add a bound to the vector", which is the fabrication being avoided.
        var unverifiable = Run(claimsNone);
        Assert.Equal(GateExit.NotAdmissible, unverifiable.Exit);
        Assert.Contains("THE REPAIR IS TO THE ENUMERATION, NOT TO THESE VECTORS", unverifiable.Output, StringComparison.Ordinal);
        Assert.Contains("DO NOT invent a bound", unverifiable.Output, StringComparison.Ordinal);

        // (2) The enumeration confirms the assertion depends on none — an EMPTY LIST, which must survive
        //     the projection as an answer rather than collapsing into an absence.
        var confirmed = claimsNone.Replace(
            "\"bounds\": { \"ramp_limit\": \"10\" } },",
            "\"bounds\": { \"ramp_limit\": \"10\" }, \"assertionBounds\": { \"REQ-014:ffcc38\": [] } },",
            StringComparison.Ordinal);
        Assert.NotEqual(claimsNone, confirmed);
        Assert.Equal(GateExit.AdmissibleSubjectToJudgement, Run(confirmed).Exit);
        Assert.Contains("VERIFIED against the enumeration, not taken from the vector", Run(confirmed).Output, StringComparison.Ordinal);

        // (3) The enumeration NAMES a bound the vector claims does not apply: refused, and the block is
        //     explicitly not accused. The only edit between (2) and (3) is the relation's contents.
        var contradicted = claimsNone.Replace(
            "\"bounds\": { \"ramp_limit\": \"10\" } },",
            "\"bounds\": { \"ramp_limit\": \"10\" }, \"assertionBounds\": { \"REQ-014:ffcc38\": [\"ramp_limit\"] } },",
            StringComparison.Ordinal);
        Assert.NotEqual(claimsNone, contradicted);

        var refused = Run(contradicted);
        Assert.Equal(GateExit.NotAdmissible, refused.Exit);
        Assert.Contains("ramp_limit", refused.Output, StringComparison.Ordinal);
        Assert.Contains("THE BLOCK IS NOT ACCUSED OF ANYTHING HERE", refused.Output, StringComparison.Ordinal);

        // And the untouched document still passes: the new field is optional and its absence changes
        // nothing for a submission that declares its bounds.
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

        // And the timer ceiling that binds first is quoted at the RULED ABSOLUTE floor (2026-08-18) rather
        // than X-D's original 10x — and rather than the scan-derived k x scan it now subsumes. On a
        // 2-second preset that is 2000 / 500 = 4.0x. Both halves are asserted: the number, and the fact
        // that the text names WHICH floor produced it, because a ceiling quoted without its provenance is
        // exactly what gets re-derived wrongly next time.
        Assert.Contains("4.0x", output, StringComparison.Ordinal);
        Assert.Contains("RULED ABSOLUTE 500 ms", output, StringComparison.Ordinal);
    }
}
