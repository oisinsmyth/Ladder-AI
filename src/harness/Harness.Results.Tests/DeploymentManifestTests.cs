using Harness.Gate;
using Harness.Results;
using Xunit;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>THE <c>deployment</c> ARM — the object manifest gate 5b differences a latch block against,
/// REBUILT FROM THE DOWNLOAD instead of believed.</b>
///
/// <para>Before this, <c>deployment</c> fell to <c>Recompute.Check</c>'s default arm: cited, never
/// compared, stamped <c>Attributed</c>. Gate 5b then looked a hand-authored latch block up in a set the
/// submission's own author wrote, and <c>DeploymentDocument.DeliverableObjects</c> admitted it in its own
/// words — <i>"an author who omits a deliverable's name buys silence on that one row."</i></para>
///
/// <para><b>Both directions everywhere.</b> Every refusal below is paired with the same shape, corrected,
/// passing — an arm that refused everything would satisfy half this file on its own.</para>
///
/// <para>⚠️ <b>*** WHAT A GREEN IN THIS FILE IS WORTH, AND IT IS LESS THAN IT LOOKS. ***</b> The four
/// probe logs read here are real recorded runs, and their own README states the limit:
/// <b>"Object names are invented; the message shape is verbatim."</b> So these tests prove the PARSE and
/// the COMPARE — that a manifest is recovered from the shape TIA really emits, and that the asymmetry
/// fires in the right direction. <b>They are not a verdict on any real material, and nobody may cite them
/// as one.</b> There is no surviving probe artifact from either recorded download on this rig: both
/// manifests exist in prose only, in commit notes and an annotation string. This arm has therefore NEVER
/// been exercised against a real deployment.</para>
///
/// <para>🔴 <b>*** WHAT THE NEXT DOWNLOAD MUST CAPTURE, SO THE INSTRUCTION EXISTS BEFORE IT IS NEEDED.
/// ***</b></para>
/// <list type="number">
/// <item><description><b>Run the probe with <c>--json</c> and REDIRECT STDOUT TO A DURABLE FILE.</b> That
/// one file is the whole artifact: <c>--json</c> puts the machine-readable report on stdout and sends the
/// verbatim log to the file and to stderr, and the report EMBEDS the log rather than pointing at it — so
/// the capture carries the <c>==== download-probe</c> banner and the <c>TRANSFER VERDICT</c> line the
/// artifact-KIND check needs AND the first-class <c>loadManifest</c> this arm prefers. Redirect with
/// <c>&gt;</c> or <c>Out-File</c>, never a pipe.</description></item>
/// <item><description><b>Pass <c>--log-dir</c> pointing somewhere that survives.</b> It defaults to
/// <c>%LADDER_PROBE_LOG_DIR%</c> or <c>%TEMP%\download-probe</c>, and a temp directory is where evidence
/// goes to be tidied away — which is exactly how the two recorded downloads came to leave nothing behind.
/// </description></item>
/// <item><description><b>Record WHICH PROGRAM was on the device and HOW it was loaded</b> — the
/// <c>--options</c> used, and the import the submission's <c>importStamp</c> names. Nothing in the two
/// documents joins them, so that pairing has to be written down by the person who was there or it is
/// gone.</description></item>
/// </list>
/// <para>Then point the deriver at it: <c>harness-gate derive --artifact deployment=&lt;that file&gt;</c>.
/// A capture that is missing the banner or the verdict line is refused by the KIND check before this arm
/// is reached, which is a different message and a different fix.</para>
///
/// <para>⚠️ <b>*** AND THE TRAP THAT WOULD MAKE THE FIRST REAL RUN REFUSE CORRECT WORK. ***</b> A captured
/// manifest is only comparable against a submission describing <b>THE SAME PROGRAM</b>, loaded the same
/// way. One recorded download on this rig put a DIFFERENT program on the device; and a differential run
/// (<c>SoftwareOnlyChanges</c>) loads only what changed, so its manifest is a legitimate SUBSET of what is
/// on the controller. Compared across either boundary a correctly-authored manifest over-claims and this
/// arm refuses it. Nothing in the two documents joins them — an <c>importStamp</c> names an import, a
/// probe log names a project path — so the pairing is the operator's act, and the refusal text leads with
/// that rather than letting a mispairing read as a finding about the submission.</para>
/// </summary>
public class DeploymentManifestTests
{
    // ---------------------------------------------------------------------------------------------
    // the asymmetry — CompareMap's, deliberately, for the same reason
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_MANIFEST_MATCHING_WHAT_THE_DOWNLOAD_LOADED_IS_MATCHED_and_says_what_it_compared()
    {
        // The differential run loaded exactly one object. A submission naming exactly that one agrees
        // with the download completely — no over-claim and nothing left over.
        var result = Check(Declaring("DB_Data01"), Fixture("differential-one-object.txt"));

        Assert.Equal(Recompute.Outcome.Matched, result.Outcome);

        // *** THE DENOMINATOR, ASSERTED. *** `no over-claims found` is true of a real comparison and of a
        // comparison against nothing, and only one of those is a result.
        Assert.Contains("all 1 object(s)", result.Detail, StringComparison.Ordinal);
        Assert.Contains("read off 1 loaded object(s)", result.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("incomplete, not wrong", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_OBJECT_THE_DOWNLOAD_DID_NOT_LOAD_IS_REFUSED_AND_NAMED()
    {
        // 🔴 *** THE DANGEROUS DIRECTION. *** Gate 5b reports a hand-authored latching block "present in
        // the deployment" on the strength of this list. A name in it that nothing loaded is 5b handing
        // back a reassurance nobody earned.
        var result = Check(Declaring("DB_Data01", "DB_NeverLoaded"), Fixture("differential-one-object.txt"));

        Assert.Equal(Recompute.Outcome.Differs, result.Outcome);
        Assert.Contains("DB_NeverLoaded", result.Detail, StringComparison.Ordinal);
        Assert.Contains("1 of its 2 object(s)", result.Detail, StringComparison.Ordinal);

        // The refusal argues rather than merely walls: the FIRST thing it tells the reader is that a
        // mispaired artifact produces this exact finding against correct work.
        Assert.Contains("CHECK THE PAIRING", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void BUT_A_DOWNLOAD_THAT_LOADED_MORE_THAN_THE_SUBMISSION_LISTS_IS_INCOMPLETE_RATHER_THAN_WRONG()
    {
        // 🔴 *** THE ASYMMETRY, AND IT IS CompareMap's PRECEDENT RATHER THAN A NEW JUDGEMENT. *** There,
        // measured on a real submission, a binding provided ~70 signals the map did not list, and
        // refusing that would have refused every honest submission in the job. Same shape here: a full
        // download loads 99 objects and a submission declares the two it cares about. Nothing can rely on
        // an object it does not name, so the omission cannot produce a wrong result — it is REPORTED.
        var result = Check(Declaring("DB_Data01", "FB_Block01"), Fixture("full-ninety-nine-objects.txt"));

        Assert.Equal(Recompute.Outcome.Matched, result.Outcome);
        Assert.Contains("all 2 object(s)", result.Detail, StringComparison.Ordinal);
        Assert.Contains("read off 99 loaded object(s)", result.Detail, StringComparison.Ordinal);
        Assert.Contains("also loaded 97 object(s) the deployment does not name", result.Detail, StringComparison.Ordinal);
        Assert.Contains("incomplete, not wrong", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_CASE_ONLY_MISS_IS_STILL_A_REFUSAL_AND_IS_NAMED_AS_ONE()
    {
        // §4.5's object names are compared ORDINALLY throughout, and gate 5b already refuses a case-only
        // miss while SAYING it is one. Matching the two would be a silent widening of §4.5 hidden in a
        // recomputation.
        var result = Check(Declaring("db_data01"), Fixture("differential-one-object.txt"));

        Assert.Equal(Recompute.Outcome.Differs, result.Outcome);
        Assert.Contains("differing only in case", result.Detail, StringComparison.Ordinal);
        Assert.Contains("DB_Data01", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_THE_SAME_NAME_SPELLED_THE_WAY_TIA_SPELLED_IT_MATCHES()
    {
        var result = Check(Declaring("DB_Data01"), Fixture("differential-one-object.txt"));

        Assert.Equal(Recompute.Outcome.Matched, result.Outcome);
    }

    // ---------------------------------------------------------------------------------------------
    // deliverableObjects is half the union, and it has to carry its own weight
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AN_OBJECT_DECLARED_ONLY_IN_deliverableObjects_IS_COMPARED_TOO()
    {
        // The manifest gate 5b builds is `s7Objects[].harnessObject` UNION `deliverableObjects`. An arm
        // that recomputed only the s7 half would leave the deliverable half exactly as self-reported as
        // it was — and the deliverable half is the one 5b's doc comment names as the weak one.
        var result = Check(Deployment(s7: new[] { "DB_Data01" }, deliverables: new[] { "FB_NotInThisDownload" }),
            Fixture("differential-one-object.txt"));

        Assert.Equal(Recompute.Outcome.Differs, result.Outcome);
        Assert.Contains("FB_NotInThisDownload", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_A_deliverableObjects_ENTRY_THE_DOWNLOAD_DID_LOAD_PASSES()
    {
        var result = Check(Deployment(s7: new[] { "DB_Data01" }, deliverables: new[] { "FB_Block01" }),
            Fixture("full-ninety-nine-objects.txt"));

        Assert.Equal(Recompute.Outcome.Matched, result.Outcome);
        Assert.Contains("all 2 object(s)", result.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // EMPTY IS NOT CLEAN — and neither is UNDETERMINED
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AN_ABORTED_RUN_WITH_NO_DOWNLOAD_RESULT_IS_NOT_COMPARABLE_AND_IS_NEVER_A_REFUSAL()
    {
        // *** UNDETERMINED IS NOT NothingTransferred, AND THE WHOLE download-feedback COMPONENT EXISTS
        // PARTLY TO KEEP THOSE APART. *** No result object ever existed, so nobody looked. Refusing here
        // would blame the submission for an abort, and passing would be worse.
        var result = Check(Declaring("DB_Data01"), Fixture("aborted-no-download-result.txt"));

        Assert.Equal(Recompute.Outcome.NotComparable, result.Outcome);
        Assert.Contains("no `==== DOWNLOAD RESULT` section", result.Detail, StringComparison.Ordinal);
        Assert.Contains("cited, not checked", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_RUN_THAT_TRANSFERRED_NOTHING_COMPARES_AGAINST_AN_EMPTY_MANIFEST_WHICH_IS_NOT_A_FINDING()
    {
        // 🔴 *** GATE 5b ALREADY RULED THIS EXACT CASE AND THE RULING IS FOLLOWED RATHER THAN RE-TAKEN. ***
        // Its words: treating "absent from an empty list" as a refusal "would report the same finding for
        // a submission that named a real loaded block as for one that named a fiction. Both are
        // unverified; only one is wrong."
        var result = Check(Declaring("DB_Data01"), Fixture("up-to-date-nothing-transferred.txt"));

        Assert.Equal(Recompute.Outcome.NotComparable, result.Outcome);
        Assert.Contains("names NO loaded program object", result.Detail, StringComparison.Ordinal);
        Assert.Contains("1 declared object(s) were compared against an empty set", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_LOOP_RESULT_JSON_CARRIES_NO_MANIFEST_SO_THE_FIELD_STAYS_CITED()
    {
        // `outcome: Ran` says a deployment happened and names not one object. It passes the KIND check
        // and must not then be treated as a manifest of zero objects.
        var result = Check(Declaring("DB_Data01"), """{ "outcome": "Ran" }""");

        Assert.Equal(Recompute.Outcome.NotComparable, result.Outcome);
        Assert.Contains("carries no load manifest", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_SUBMISSION_WHOSE_DEPLOYMENT_NAMES_NO_OBJECT_HAS_NOTHING_TO_COMPARE()
    {
        // `noS7Transport` and `s7Objects: []` are claims about the classic-S7comm WIRE. Neither says
        // anything was loaded, so neither can be checked against a load manifest.
        var result = Check("""{ "deployment": { "noS7Transport": true } }""", Fixture("full-ninety-nine-objects.txt"));

        Assert.Equal(Recompute.Outcome.NotComparable, result.Outcome);
        Assert.Contains("names no object at all", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_A_SUBMISSION_WITH_NO_deployment_AT_ALL_IS_NOT_COMPARABLE_EITHER()
    {
        var result = Check("""{ "blockAuthor": "agent-a" }""", Fixture("full-ninety-nine-objects.txt"));

        Assert.Equal(Recompute.Outcome.NotComparable, result.Outcome);
        Assert.Contains("declares no deployment", result.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // FIRST-HAND FIRST, and the verdict records which authority it used
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_PROBES_OWN_loadManifest_IS_PREFERRED_OVER_ITS_RENDERED_LOG()
    {
        // 🔴 *** THE TWO AUTHORITIES ARE NOT EQUALLY STRONG, SO THE CHOICE BETWEEN THEM IS PINNED. ***
        // `loadManifest` is built by DownloadResultAdapter off the live Openness DownloadResult; the log
        // is that same result after the probe's renderer has been at it, and ProbeLogReader's own docs
        // warn "anything the renderer drops is gone before this reader sees it". This payload carries
        // BOTH, naming DIFFERENT objects, so which one was used is decidable rather than assumed.
        var result = Check(Declaring("DB_FirstHand"), ProbeJson(firstHand: "DB_FirstHand", loggedObject: "DB_Data01"));

        Assert.Equal(Recompute.Outcome.Matched, result.Outcome);

        // And it SAYS which authority it read, because a result that does not invites the stronger reading.
        Assert.Contains("via DownloadResultAdapter", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_THE_RENDERED_LOG_IN_THE_SAME_PAYLOAD_IS_NOT_WHAT_WAS_READ()
    {
        // The other half of the pair. If the log had been preferred this would pass, so the two tests
        // together decide it rather than describing it.
        var result = Check(Declaring("DB_Data01"), ProbeJson(firstHand: "DB_FirstHand", loggedObject: "DB_Data01"));

        Assert.Equal(Recompute.Outcome.Differs, result.Outcome);
        Assert.Contains("DB_Data01", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void WITH_NO_loadManifest_THE_EMBEDDED_LOG_IS_READ_AND_THE_WEAKER_SOURCE_IS_NAMED()
    {
        // An older probe build emits no `loadManifest`. Losing the manifest silently would be worse than
        // reading a rendering — so the rendering is read, and it is never allowed to look first-hand.
        var result = Check(Declaring("DB_Data01"), ProbeJson(firstHand: null, loggedObject: "DB_Data01"));

        Assert.Equal(Recompute.Outcome.Matched, result.Outcome);
        Assert.Contains("via ProbeLogReader", result.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("via DownloadResultAdapter", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_loadManifest_MARKED_UNAVAILABLE_IS_NOBODY_LOOKED_AND_NOT_NOTHING_LOADED()
    {
        // *** THE PROBE EMITS EVERY LIST AS null IN THAT CASE PRECISELY SO THE TWO CANNOT BE CONFUSED. ***
        // Reading it as an empty set here would undo that at the first consumer, and turn an abort into a
        // refusal of a manifest that may be perfectly correct.
        var result = Check(Declaring("DB_Data01"), """
            { "loadManifest": { "available": false, "source": "DownloadResultAdapter" } }
            """);

        Assert.Equal(Recompute.Outcome.NotComparable, result.Outcome);
        Assert.NotEqual(Recompute.Outcome.Differs, result.Outcome);
        Assert.Contains("carries no load manifest", result.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // REACHABLE FROM A COMMAND SOMEBODY CAN RUN
    // ---------------------------------------------------------------------------------------------
    //
    // *** THE QUESTION IS "COULD ANY INPUT REACH THIS LINE", NOT "IS THIS LINE CALLED". ***
    // WaveSetAdmission.cs:468-475 records what the other answer costs: a private check was deleted and
    // the suite stayed green because nothing could reach it. Every test above calls `Recompute.Check`
    // directly; these two prove the same arm is reached by `harness-gate derive` from a file an operator
    // is actually holding, past the artifact-KIND check that guards it.

    [Fact]
    public void THE_ARM_IS_REACHED_BY_harness_gate_derive_AND_STAMPS_deployment_COMPUTED()
    {
        var (exit, output, _) = DeriveComputationTests.Derive(
            submission: SubmissionDeclaring("DB_Data01"),
            deploy: ProbeLogAround(Fixture("differential-one-object.txt")));

        Assert.True(exit == DeriveExit.Derived, output);

        // *** THE WHOLE POINT, IN ONE ASSERTION. *** Before this arm existed the same run printed
        // ATTRIBUTED here - cited to a file, never compared with it.
        Assert.Contains("COMPUTED   " + DerivableField.Deployment, output, StringComparison.Ordinal);

        // And the operator is told WHAT was compared and BY WHICH AUTHORITY, on the pass and not only on
        // the refusal. `COMPUTED` alone is true of a comparison against 99 objects and of one against a
        // single row.
        Assert.Contains("COMPARED   " + DerivableField.Deployment, output, StringComparison.Ordinal);
        Assert.Contains("read off 1 loaded object(s) via ProbeLogReader", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_AN_OVER_CLAIMING_MANIFEST_REFUSES_THE_WHOLE_DERIVATION_WITH_NOTHING_WRITTEN()
    {
        // The committed fixture submission declares `DB_HarnessMarker`, which this download never loaded.
        // Nothing is written: a submission stamped with a partial provenance would pass gate 0c for the
        // fields it happened to cover.
        var (exit, output, written) = DeriveComputationTests.Derive(
            deploy: ProbeLogAround(Fixture("differential-one-object.txt")));

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("MISMATCH   " + DerivableField.Deployment, output, StringComparison.Ordinal);
        Assert.Contains("DB_HarnessMarker", output, StringComparison.Ordinal);
        Assert.Empty(written);
    }

    [Fact]
    public void AND_THE_KIND_CHECK_STILL_GUARDS_THE_ARM_a_log_that_moved_nothing_never_reaches_it()
    {
        // Layering, pinned. A NOTHINGTRANSFERRED log is refused by the artifact-KIND check BEFORE any
        // recomputation, so the arm's own "empty manifest" branch is defence in depth rather than the
        // only thing standing there. Reported as a bad SOURCE, never as a mismatch: they are different
        // mistakes with different fixes.
        var (exit, output, _) = DeriveComputationTests.Derive(
            submission: SubmissionDeclaring("DB_Data01"),
            deploy: ProbeLogAround(Fixture("up-to-date-nothing-transferred.txt"), verdict: "NOTHINGTRANSFERRED"));

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("BAD SOURCE " + DerivableField.Deployment, output, StringComparison.Ordinal);
        Assert.DoesNotContain("MISMATCH   " + DerivableField.Deployment, output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_CAPTURED_probe_json_IS_ONE_FILE_THAT_CLEARS_THE_KIND_CHECK_AND_FEEDS_THE_FIRST_HAND_ARM()
    {
        // 🔴 *** THIS IS THE ARTIFACT A FUTURE DOWNLOAD MUST CAPTURE, PINNED END TO END. *** `--json` puts
        // the machine-readable report on stdout with the verbatim log EMBEDDED, so one redirected file
        // carries the `==== download-probe` banner and `TRANSFER VERDICT` line the artifact-KIND check
        // keys on AND the first-class `loadManifest` this arm prefers. Two files would be two artifacts,
        // and only one of them can be hashed onto the derivation record.
        var (exit, output, written) = DeriveComputationTests.Derive(
            submission: SubmissionDeclaring("DB_FirstHand"),
            deploy: ProbeJson(firstHand: "DB_FirstHand", loggedObject: "DB_SomethingTheRendererKept"));

        Assert.True(exit == DeriveExit.Derived, output);
        Assert.Contains("COMPUTED   " + DerivableField.Deployment, output, StringComparison.Ordinal);
        Assert.NotEmpty(written);

        // 🔴 THE FIRST-HAND AUTHORITY, NAMED IN THE RUN'S OWN OUTPUT. A capture whose manifest came off
        // the live DownloadResult and one re-derived from a rendering are not the same evidence, and a
        // report that does not say which invites the stronger reading.
        Assert.Contains("COMPARED   " + DerivableField.Deployment, output, StringComparison.Ordinal);
        Assert.Contains("via DownloadResultAdapter", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_THE_SAME_CAPTURE_REFUSES_A_SUBMISSION_NAMING_WHAT_THE_LIVE_RESULT_DID_NOT_LOAD()
    {
        // The pair. `DB_SomethingTheRendererKept` is in the embedded LOG and NOT in the first-hand
        // manifest, so a run that read the rendering instead would pass this — which is the whole reason
        // the two are given different names here.
        var (exit, output, written) = DeriveComputationTests.Derive(
            submission: SubmissionDeclaring("DB_SomethingTheRendererKept"),
            deploy: ProbeJson(firstHand: "DB_FirstHand", loggedObject: "DB_SomethingTheRendererKept"));

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("MISMATCH   " + DerivableField.Deployment, output, StringComparison.Ordinal);
        Assert.Empty(written);
    }

    [Fact]
    public void A_probe_json_CAPTURE_SAYING_NOTHING_MOVED_IS_REFUSED_even_though_Transferred_is_a_substring()
    {
        // *** THE SUBSTRING TRAP AGAIN, ON THE NEW SURFACE. *** The serialized enum values are
        // `Transferred`, `NothingTransferred` and `Undetermined`, and the first is a substring of the
        // second — so a Contains check on the JSON key would read "nothing was transferred" as success,
        // exactly as it would on the rendered line. The log form was already pinned against this; the
        // report form is a second place the same mistake fits.
        var (exit, output, _) = DeriveComputationTests.Derive(
            submission: SubmissionDeclaring("DB_FirstHand"),
            deploy: ProbeJson(firstHand: "DB_FirstHand", loggedObject: "DB_FirstHand", transferVerdict: "NothingTransferred"));

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("BAD SOURCE " + DerivableField.Deployment, output, StringComparison.Ordinal);
        Assert.Contains("NothingTransferred", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_A_probe_json_CAPTURE_WITH_A_NULL_transferVerdict_IS_REFUSED_because_this_fails_closed()
    {
        // The probe writes null when no transfer classification exists at all — an abort, a throw, a run
        // that never reached the download. "We cannot say" must never be admitted as "it happened".
        var (exit, output, _) = DeriveComputationTests.Derive(
            submission: SubmissionDeclaring("DB_FirstHand"),
            deploy: ProbeJson(firstHand: "DB_FirstHand", loggedObject: "DB_FirstHand", transferVerdict: null));

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("ARTIFACT REPORTS FAILURE", output, StringComparison.Ordinal);
        Assert.Contains("no transfer classification exists", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_SERIALIZED_LOOP_RESULT_IS_STILL_JUDGED_ON_ITS_OWN_outcome_and_not_on_transferVerdict()
    {
        // Trying JSON before the banner reordered this method; the loop-result shape it has always
        // accepted must keep working, and the field it is judged on must not have quietly changed.
        var (exit, output, _) = DeriveComputationTests.Derive(deploy: """{ "outcome": "Ran" }""");

        Assert.True(exit == DeriveExit.Derived, output);
        Assert.Contains("ATTRIBUTED " + DerivableField.Deployment, output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // fixtures
    // ---------------------------------------------------------------------------------------------

    private static Recompute.Result Check(string submissionJson, string artifact) =>
        Recompute.Check(DerivableField.Deployment, SubmissionDocument.Read(submissionJson), artifact);

    /// <summary>A bare submission whose deployment names exactly <paramref name="s7Objects"/>.</summary>
    private static string Declaring(params string[] s7Objects) => Deployment(s7Objects, System.Array.Empty<string>());

    private static string Deployment(IReadOnlyList<string> s7, IReadOnlyList<string> deliverables)
    {
        var rows = s7.Select((o, i) =>
            $$"""{ "area": "{{o}}", "dbNumber": {{100 + i}}, "harnessObject": "{{o}}", "layout": "Standard", "layoutSetAfterImport": "import-A" }""");

        var names = deliverables.Select(d => $"\"{d}\"");

        return $$"""
        {
          "deployment": {
            "importStamp": "import-A",
            "s7Objects": [ {{string.Join(", ", rows)}} ],
            "deliverableObjects": [ {{string.Join(", ", names)}} ]
          }
        }
        """;
    }

    /// <summary>The committed submission fixture, with its deployment's one object renamed.</summary>
    private static string SubmissionDeclaring(string harnessObject) =>
        GateCliTests.Good.Replace("DB_HarnessMarker", harnessObject, StringComparison.Ordinal);

    /// <summary>
    /// One of the four RECORDED probe logs, linked in from the download-feedback suite.
    ///
    /// <para><b>Real runs, invented object names, verbatim message shape</b> — see this class's header for
    /// what that does and does not license.</para>
    /// </summary>
    private static string Fixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

        // A missing fixture must FAIL rather than yield an empty string that quietly compares against
        // nothing — the exact shape of pass this file exists to refuse.
        Assert.True(File.Exists(path), $"the recorded probe log '{name}' is not beside the test assembly at {path}.");

        return File.ReadAllText(path);
    }

    /// <summary>
    /// A recorded <c>DOWNLOAD RESULT</c> section inside the envelope a whole probe log carries.
    ///
    /// <para><b>The envelope is SYNTHETIC and the body is not.</b> The committed fixtures are excerpts
    /// starting at <c>==== DOWNLOAD RESULT</c>, and the artifact-kind check keys on the probe's banner and
    /// its <c>TRANSFER VERDICT</c> line — so reaching the arm from the CLI needs both. What is being
    /// proved end-to-end is the ROUTE; the manifest parsed out of it is the real recorded shape.</para>
    /// </summary>
    private static string ProbeLogAround(string downloadResult, string verdict = "TRANSFERRED") =>
        "==== download-probe ============================================================\n"
        + "project        : somewhere/Scratch.ap20\n"
        + $"TRANSFER VERDICT : {verdict}\n"
        + downloadResult;

    /// <summary>
    /// <c>download-probe --json</c> stdout, carrying the first-hand manifest, the rendered log, or both.
    /// </summary>
    // 🔴 THE DEFAULT WAS "Transferred", A TOKEN NO REAL PROBE HAS EVER EMITTED (corrected 2026-08-28).
    // download-probe serializes TransferVerdictKind — SoftwareLoaded / NothingTransferred /
    // Undetermined — and "Transferred" belongs to the Openness library's enum, which the probe maps
    // ONTO SoftwareLoaded before writing. So every test built on this default was exercising the
    // check against a value the artifact under test cannot produce, and the check's own comparison
    // was wrong in exactly the same way. THE FIXTURE RATIFIED THE DEFECT: a real download reported
    // SoftwareLoaded, gate 0c refused it as "ARTIFACT REPORTS FAILURE", and the suite stayed green
    // throughout. A fixture that invents its subject's vocabulary cannot catch the subject getting
    // that vocabulary wrong.
    private static string ProbeJson(string? firstHand, string loggedObject, string? transferVerdict = "SoftwareLoaded")
    {
        var manifest = firstHand is null
            ? string.Empty
            : $$"""
              "loadManifest": { "available": true, "source": "DownloadResultAdapter",
                                "loadedObjects": [ "{{firstHand}}" ], "verdict": "Transferred",
                                "verdictReason": "one object load message.", "unrecognisedMessageCount": 0 },
            """;

        // *** THE BANNER AND THE VERDICT LINE ARE IN THE EMBEDDED LOG, WHICH IS WHY ONE CAPTURED FILE
        // SATISFIES BOTH CHECKS. *** `download-probe --json` embeds its verbatim log rather than pointing
        // at it (Program.BuildJson: "a JSON report that only pointed at a file would be a summary of the
        // one thing that must never be summarised"), and that log opens with `==== download-probe` and
        // carries `DownloadFeedback.ToReport()`'s `TRANSFER VERDICT` line. So the artifact-KIND check
        // finds what it keys on inside the JSON, and this arm then reads the FIRST-HAND `loadManifest`
        // rather than the rendering the kind check happened to look at.
        var log = string.Join(",\n    ", new[]
        {
            "\"==== download-probe ====\"",
            "\"TRANSFER VERDICT : TRANSFERRED\"",
            "\"==== DOWNLOAD RESULT ====\"",
            "\"state         : Success\"",
            "\"errors        : 0\"",
            "\"warnings      : 0\"",
            "\"messages (recursive, verbatim):\"",
            "\"  - state=Success errors=0 warnings=0\"",
            "\"    text : PLC_1\"",
            "\"    - state=Success errors=0 warnings=0\"",
            $"\"      text : '{loggedObject}' was loaded successfully.\"",
        });

        // `null`, never an omitted key: the probe always emits `transferVerdict` and writes null when no
        // transfer classification exists. Dropping the key would be a different artifact shape entirely.
        var verdict = transferVerdict is null ? "null" : $"\"{transferVerdict}\"";

        return $$"""
        {
          "tool": "download-probe",
          "transferVerdict": {{verdict}},
        {{manifest}}
          "log": [
            {{log}}
          ]
        }
        """;
    }
}
