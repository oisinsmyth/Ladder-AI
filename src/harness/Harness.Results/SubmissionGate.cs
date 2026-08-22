using System.Text.RegularExpressions;
using Harness.Wire;

namespace Harness.Results;

/// <summary>
/// The three outcomes per gate, and they may never be collapsed into two.
/// </summary>
public enum GateStatus
{
    /// <summary>A verifier ran against this submission's actual content. A result.</summary>
    Checked,

    /// <summary>No verifier can exist for this; an independent reader decided. A result, LABELLED.</summary>
    Judgement,

    /// <summary>
    /// No verifier exists yet, or the input it needs is absent. <b>NOT A RESULT</b> — and it fails
    /// closed, with no flag that relaxes it.
    /// </summary>
    NotChecked,
}

/// <summary>
/// *** WHY A GATE COULD NOT RUN — AND THESE ARE FOUR DIFFERENT FACTS. ***
///
/// <para>A flat <c>NOT CHECKED</c> label flattens them, and <b>a count of unexplained NOT CHECKEDs is
/// exactly the shape that reads as a pass to a tired reader</b> — the failure this component exists to
/// avoid. One of these is not a gap at all; one can never be closed off the rig; two are build-list
/// items with different owners.</para>
/// </summary>
public enum NotCheckedReason
{
    /// <summary>
    /// Nobody said. <b>Unusable, and a defect in the GATE rather than in the submission</b> — a
    /// <c>NOT CHECKED</c> that does not say which kind it is has put an unexplained entry on the list
    /// this enum exists to empty. Asserted against by test.
    /// </summary>
    Unstated = 0,

    /// <summary>
    /// <b>Requires a controller, a download or a wave.</b> It cannot be answered offline by anybody and
    /// no artifact substitutes for it. Nothing to build; it needs the rig session.
    /// </summary>
    RequiresTheDevice = 1,

    // ---------------------------------------------------------------------------------------------
    // 🔴 2 IS DELIBERATELY VACANT. It was `IndependentAuthorityByDesign`, RETIRED 2026-08-14 BY
    // MEASUREMENT, and the number is skipped rather than reused so the retirement stays visible in the
    // source rather than only in a commit message.
    //
    // What it claimed: the input must come from an authority OTHER than the submitter, so the gate reads
    // NOT CHECKED for ever and that is correct rather than outstanding.
    //
    // *** BOTH ITS MEMBERS WERE MEASURED AND BOTH RUN. *** Gate 5 (observability map) was reclassified
    // first: driven with a loadable coordinator binding it BECOMES CHECKED and returns a verdict. That
    // left gate 4b's no-fidelity-at-all branch as the only path producing it, and the same measurement
    // was applied — a fidelity declaration that NAMES ITS DECLARER makes 4b return `Checked` and pass.
    // Neither was a design property; both were an artifact nobody had produced yet.
    //
    // *** THE CATEGORY IS RETIRED RATHER THAN LEFT EMPTY, AND THAT IS THE POINT. *** An empty
    // classification is a slot waiting to be misused: the next gate that is merely INCONVENIENT to check
    // gets filed under it, and "by design" is unfalsifiable once nobody remembers it was measured empty.
    //
    // *** AND THE CLAIM THAT SURVIVES IS STRONGER THAN EITHER TRIAGE: NO `NOT CHECKED` VERDICT IN THIS
    // SYSTEM IS A PROPERTY OF THE DESIGN. EVERY ONE NAMES AN ARTIFACT THAT COULD EXIST — OR THE RIG. ***
    // A REFUSAL can still be permanent and correct (a self-declared map is refused for ever, and should
    // be); what is never permanent is a gate that could not RUN. The two were being conflated, and that
    // conflation is what let a transcription job be recorded as a law of nature.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The authority exists, or could, and nobody has produced it yet. <b>A build-list item with an
    /// owner</b> — closable without a rig and without breaking anybody's independence.
    /// </summary>
    AwaitingAnArtifactThatCouldExist = 3,

    /// <summary>
    /// <b>The harness itself cannot yet compute it.</b> Not the submission's fault and not anybody
    /// else's artifact — this lane's own build list.
    /// </summary>
    HarnessCapabilityMissing = 4,
}

/// <summary>One gate's outcome, with the verifier that produced it named.</summary>
/// <param name="Reason">
/// Why it could not run. <b>Meaningful only when <see cref="Status"/> is
/// <see cref="GateStatus.NotChecked"/></b>, and required there — see <see cref="NotCheckedReason"/>.
/// </param>
public sealed record GateResult(
    string Gate,
    GateStatus Status,
    bool Passed,
    string Verifier,
    string Detail,
    NotCheckedReason Reason = NotCheckedReason.Unstated)
{
    /// <summary>
    /// The only way a <c>NOT CHECKED</c> result should be built: <b>the reason is a required
    /// parameter</b>, so a gate cannot join the list without saying which kind it is.
    /// </summary>
    public static GateResult CouldNotRun(string gate, NotCheckedReason reason, string verifier, string detail) =>
        new(gate, GateStatus.NotChecked, false, verifier, detail, reason);

    /// <summary>True when this gate's silence is a build-list item rather than a design property.</summary>
    public bool IsClosableOffline =>
        Status == GateStatus.NotChecked
        && Reason is NotCheckedReason.AwaitingAnArtifactThatCouldExist or NotCheckedReason.HarnessCapabilityMissing;
}

/// <summary>The submission's verdict. <b>There is deliberately no plain "ADMISSIBLE".</b></summary>
public enum SubmissionVerdict
{
    /// <summary>Every mechanical gate ran and passed. Judgement gates remain, and they are named.</summary>
    AdmissibleSubjectToJudgement,

    /// <summary>A gate ran and refused, or a gate could not be run at all.</summary>
    NotAdmissible,

    /// <summary>There was nothing to gate. Empty is not clean.</summary>
    NothingExamined,
}

/// <summary>The whole report for one submission.</summary>
public sealed record SubmissionReport(IReadOnlyList<GateResult> Gates, int VectorsExamined)
{
    public SubmissionVerdict Verdict =>
        VectorsExamined == 0 || Gates.Count == 0 ? SubmissionVerdict.NothingExamined
        : Gates.Any(g => g.Status == GateStatus.NotChecked) ? SubmissionVerdict.NotAdmissible
        : Gates.Any(g => g.Status == GateStatus.Checked && !g.Passed) ? SubmissionVerdict.NotAdmissible
        : SubmissionVerdict.AdmissibleSubjectToJudgement;

    /// <summary>Gates that could not be run. This is the harness lane's build list.</summary>
    public IReadOnlyList<GateResult> NotChecked => Gates.Where(g => g.Status == GateStatus.NotChecked).ToArray();

    /// <summary>Gates that ran and refused.</summary>
    public IReadOnlyList<GateResult> Refused => Gates.Where(g => g.Status == GateStatus.Checked && !g.Passed).ToArray();

    /// <summary>Gates nothing can ever verify, listed so they are not mistaken for verified ones.</summary>
    public IReadOnlyList<GateResult> Judgements => Gates.Where(g => g.Status == GateStatus.Judgement).ToArray();
}

/// <summary>
/// Contract §10's enforcement surface, in the order a submission meets it — <b>as runnable checks.</b>
///
/// <para><b>A gate nobody can run is not a gate, and saying it passed is worse than saying nothing.</b>
/// Every gate below reports which of the three outcomes it reached and which verifier produced it, so a
/// report cannot claim a check that did not happen.</para>
/// </summary>
public static class SubmissionGate
{
    private static readonly Regex AbsoluteAddress = new(@"^%[A-Za-z]{0,2}\d+(\.\d+)?$", RegexOptions.Compiled);

    /// <summary>
    /// A REAL, computed observability report over a trivially supportable expectation.
    ///
    /// <para>The per-gate delegations below reuse <c>Admissibility.Check</c>, which composes every gate;
    /// each call needs a value for the gates it is not asking about. This is deliberately a report
    /// PRODUCED BY the checker rather than a fabricated "supported" — there is no way to fabricate one,
    /// which is the property that made observability a computation in the first place.</para>
    /// </summary>
    private static readonly ObservabilityReport Passing = ObservabilityCheck.Evaluate(
        new[] { new ObservabilityDeclaration("<gate-local>", SignalNature.PersistentState, InstrumentationMode.Latched, 0) },
        AssertionForm.When,
        MirrorObservability.Of(("<gate-local>", new[] { InstrumentationMode.Latched })),
        floorScans: 1, declaredCompression: 1, runtimeCompression: 1);

    /// <summary>Run every gate over one submission.</summary>
    /// <param name="conflicts">
    /// The computed disjointness graph (D9), <b>with X-G's provenance on every edge</b>. <b>Null means the
    /// graph was not available</b>, which makes the blacklist gate NOT CHECKED rather than passed — a
    /// blacklist compared against an absent graph is a blacklist nobody checked. A graph built by
    /// <see cref="ConflictGraph.WithoutProvenance"/> is available but unprovenanced, which is a different
    /// and equally reportable state.
    /// </param>
    /// <param name="enumerations">
    /// 🔴 <b>THE ENUMERATIONS THIS SUBMISSION CITES INTO — PLURAL SINCE 2026-08-18.</b> A single
    /// <c>AssertionEnumeration</c> converts implicitly to a set of one and behaves exactly as it did
    /// before, which is what keeps every caller and every submission written before subjects existed on
    /// the path it was written for. See <see cref="AssertionEnumerationSet"/> for why a citation must
    /// resolve to (subject, clause, assertion) and why the assertion hash cannot break the tie.
    /// </param>
    public static SubmissionReport Check(
        IReadOnlyList<SubmissionVector> vectors,
        AssertionEnumerationSet enumerations,
        FidelityDeclaration? fidelity,
        AgentIdentity blockAuthor,
        MirrorObservability map,
        double floorScans,
        int runtimeCompression,
        ConflictGraph? conflicts,
        BlockCompressionInputs? compressionInputs = null,
        DeploymentDeclaration? deployment = null,
        TagMapReach? tagMapReach = null,

        /// <summary>
        /// Contract §2.7's join: signal → controller storage. <b>Null is the same as empty</b> — nobody
        /// declared it — and both leave gates 8 and 8c NOT CHECKED, because a conflict graph is a
        /// statement about storage and nothing said where these signals live.
        /// </summary>
        SignalStorageMap? storage = null,

        /// <summary>
        /// <b>True when the document CARRIED a <c>conflictEdges</c> key that was explicitly null.</b>
        /// Distinct from omitted: <c>[]</c> is the earned claim that the graph ran and found nothing,
        /// omitted is the honest NOT CHECKED, and an explicit null is neither — see gate 8.
        /// </summary>
        bool conflictEdgesExplicitlyNull = false,

        /// <summary>
        /// Fields the document carried that this schema does not know. <b>Named and refused</b> — a
        /// silently-ignored field is worse than a rejected one, because it reads as accepted.
        /// </summary>
        IReadOnlyList<string>? unknownFields = null,
        IReadOnlyList<string>? annotationFields = null,

        /// <summary>
        /// The deriver's provenance block, and which derivable fields the document actually carried.
        /// <b>Null is NOT CHECKED, never a pass</b> — a caller that supplies nothing has not established
        /// that the fields a tool already knows were produced rather than typed.
        /// </summary>
        DerivationEvidence? derivation = null,

        /// <summary>
        /// The input path whose value is the scenario's END, in plant milliseconds — the thing every
        /// vector's <c>MaxDuration</c> is bounded against. <b>Null leaves gate 1b NOT CHECKED</b>: the
        /// harness cannot know which of a stimulus model's inputs carries that, and guessing it is how a
        /// backstop ends up bounded by nothing.
        /// </summary>
        string? scenarioEndInput = null,

        /// <summary>
        /// A flat ceiling, in scans, on any one vector's <c>MaxDuration</c> — the bound for vectors with
        /// <b>no scenario clock at all</b> (a ramp-to-limit test has no end time, so there is nothing
        /// tighter available to it). Applies to every vector when declared, and never replaces
        /// <paramref name="scenarioEndInput"/>'s tighter per-vector bound.
        /// </summary>
        int? maxIndexScans = null,

        /// <summary>
        /// Which of the vectors' inputs are SCENARIO COORDINATES — plant times that must be re-expressed
        /// at the wave's factor. Null above comp 1 leaves gate 10c NOT CHECKED; empty is the positive
        /// claim that this stimulus has no time-valued scenario data.
        /// </summary>
        IReadOnlyList<string>? scenarioTimeInputs = null)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentNullException.ThrowIfNull(enumerations);
        ArgumentNullException.ThrowIfNull(map);

        var gates = new List<GateResult>();

        if (vectors.Count == 0)
        {
            gates.Add(new GateResult("0 submission", GateStatus.Checked, false, nameof(SubmissionGate),
                "the submission contains no vectors. Empty is not clean: a submission with nothing in it cannot be admitted, and reporting it as admissible would be the purest form of a gate that passed without examining anything."));
            return new SubmissionReport(gates, 0);
        }

        gates.Add(UnknownFields(unknownFields, annotationFields));
        gates.Add(DerivedFields(derivation));
        gates.Add(Schema(vectors));
        gates.Add(Backstop(vectors, scenarioEndInput, maxIndexScans));
        gates.Add(ScenarioScaleGate(vectors, runtimeCompression, scenarioTimeInputs));
        gates.Add(Authorship(vectors, blockAuthor));
        gates.Add(SubjectResolution(vectors, enumerations));
        gates.Add(BasisGate(vectors, enumerations));
        gates.Add(CitationShape(vectors));
        gates.Add(IdsRecompute(enumerations));
        gates.Add(RequiredObservations(vectors, enumerations));
        gates.Add(EnumeratorIndependence(vectors, enumerations, blockAuthor));
        gates.Add(AssertionFormAuthority(vectors, enumerations));
        gates.Add(BoundsCurrency(vectors, enumerations));
        gates.Add(new GateResult("3c basis — faithful reading of the clause", GateStatus.Judgement, true, "none, ever",
            "whether the cited assertion is a faithful reading of the clause is what the independent author is for. It is recorded, never verified."));
        gates.Add(Fidelity(vectors, fidelity));
        gates.Add(FidelityAuthority(vectors, fidelity, blockAuthor));
        gates.Add(Observability(vectors, enumerations, map, floorScans, runtimeCompression));
        gates.Add(Settling(vectors));
        gates.Add(new GateResult("6b settling — does the condition imply the value is final", GateStatus.Judgement, true, "none, ever",
            "whether the declared settling condition really implies finality is judgement, informed by the model's fidelity declaration."));
        gates.Add(StartBool(vectors));
        gates.Add(SignalStorageJoin(vectors, storage));
        gates.Add(Blacklist(vectors, conflicts, storage, conflictEdgesExplicitlyNull));
        gates.Add(new GateResult("8b blacklist — over-broad?", GateStatus.Judgement, true, "density, reported not gated",
            $"blacklist density is {vectors.Sum(v => v.Blacklist.Count)} entr(ies) across {vectors.Count} vector(s). Over-blacklisting is measurable and not preventable."));
        gates.Add(MultiWriterProvenance(conflicts, storage));
        gates.Add(LivenessPreconditions(vectors, map));
        gates.Add(CompressionCeiling(vectors, floorScans, runtimeCompression));
        gates.Add(CompressionBoundsNotInTheSubmission(vectors, runtimeCompression, floorScans, compressionInputs));
        gates.Add(MemoryLayout(deployment, tagMapReach));

        return new SubmissionReport(gates, vectors.Count);
    }

    // -------------------------------------------------------------------------------------------------
    // 1 — schema
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A FIELD THIS SCHEMA DOES NOT KNOW IS A REFUSAL NAMING IT — NEVER A SILENT DROP.</b>
    ///
    /// <para>*** A SILENTLY-IGNORED FIELD IS WORSE THAN A REJECTED ONE, BECAUSE IT READS AS ACCEPTED. ***
    /// The contract and this code have diverged before: the contract specified three instrumentation
    /// fields the code never implemented, so an author writing to the contract emitted three fields that
    /// vanished — and got a green built on declarations nobody read.</para>
    ///
    /// <para><b>This closes a divergence in the direction that cannot lie, whichever document is stale.</b>
    /// The refusal does not decide who is right; it makes the disagreement impossible to miss.</para>
    /// </summary>
    private static GateResult UnknownFields(IReadOnlyList<string>? unknown, IReadOnlyList<string>? annotations)
    {
        const string name = "0b unknown fields";

        // *** THE ANNOTATION COUNT IS REPORTED ON EVERY RUN, INCLUDING ZERO. *** The narrowing is what
        // keeps this gate usable, and a narrowing nobody can see is one that quietly becomes a place to
        // hide. A reader who wonders where 82 fields went finds the answer in the gate's own line.
        var annotated = annotations is null
            ? string.Empty
            : $" {annotations.Count} annotation(s) (leading '_') were EXCLUDED by name, not examined: the contract specifies no '_'-prefixed field, so one can never be the contract/code divergence this gate exists for.";

        if (unknown is null)
        {
            return GateResult.CouldNotRun(name, NotCheckedReason.HarnessCapabilityMissing, "the document reader's extension data",
                "nobody supplied the document's unknown-field set, so a field this schema does not know would have been dropped in "
                + "silence. That is not a pass: a dropped field reads as an accepted one." + annotated);
        }

        if (unknown.Count == 0)
        {
            return new GateResult(name, GateStatus.Checked, true, nameof(SubmissionGate),
                "every field in the submission and the binding is one their schemas read. Nothing was silently ignored." + annotated);
        }

        return new GateResult(name, GateStatus.Checked, false, nameof(SubmissionGate),
            $"{unknown.Count} field(s) are not read by these schemas: {string.Join(", ", unknown)}. "
            + "*** THIS IS A REFUSAL AND NOT A WARNING, BECAUSE A SILENTLY-IGNORED FIELD READS AS AN ACCEPTED ONE. *** Either the "
            + "contract has moved ahead of the code or the document is stale; this gate does not decide which, it makes the "
            + "disagreement impossible to miss. Remove the field, or implement it."
            + " (An intentional COMMENT belongs under a leading '_', which is excluded by name.)" + annotated);
    }

    // -------------------------------------------------------------------------------------------------
    // 0c — derived fields
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A FIELD A TOOL ALREADY KNOWS, TYPED BY HAND, IS A REFUSAL NAMING IT.</b>
    ///
    /// <para>*** THE GATES WERE GRADING A TRANSCRIPTION. *** A dozen sub-documents — the map, storage,
    /// the conflict edges, the deployment stamps, the compression ceilings — are each produced by
    /// something that already exists, and nothing composed them into the document this gate reads. So an
    /// agent retyped them, and every downstream gate then checked the retyping faithfully. A gate that
    /// verifies a hand-copied number verifies the copying.</para>
    ///
    /// <para><b>Precedent, in this codebase, on exactly one field:</b> <c>--binding</c> supplies the
    /// observability map from the coordinator instead of from the vector author, and gate 5 moves from
    /// NOT CHECKED to a real verdict because of it. This is that same move, made general.</para>
    ///
    /// <para><b>Why a present-and-underived field is refused rather than warned:</b> a warning on the
    /// path that reaches the rig is read once and skimmed thereafter. The refusal names the field, which
    /// is the thing the author has to go and delete.</para>
    ///
    /// <para><b>Why ZERO derivable fields present is a PASS, and not the empty-is-not-clean refusal the
    /// rest of this system applies.</b> This gate asks one question — <i>was anything here hand-authored
    /// that a tool already knows?</i> — and with no such field present the honest answer is no. The
    /// ABSENCE of those fields is a real finding, but it belongs to the gates that consume them: omit the
    /// deployment and gate 11 reports NOT CHECKED, omit the graph and gate 8 does, and a single NOT
    /// CHECKED already makes the whole submission NOT ADMISSIBLE. <b>So omission is not an escape route
    /// from this gate — it is a worse outcome by a different door</b>, and duplicating the complaint here
    /// would make 0c a gate that refuses ordinary submissions, which is how a gate gets switched off. The
    /// denominator is printed on every run so the scope of the claim is never in doubt.</para>
    /// </summary>
    private static GateResult DerivedFields(DerivationEvidence? evidence)
    {
        const string name = "0c derived fields";

        if (evidence is null)
        {
            return GateResult.CouldNotRun(name, NotCheckedReason.HarnessCapabilityMissing, "the deriver's provenance block",
                "nobody supplied the derivation evidence, so a field a tool already knows could have been typed by hand and "
                + "graded as though it were measured. That is not a pass: the gates downstream would then be checking the "
                + "transcription rather than the truth.");
        }

        var present = evidence.DerivableFieldsPresent;
        var records = evidence.Records;

        // *** PRINTED ON EVERY RUN, INCLUDING ZERO. *** Every other number this gate can report is a
        // reason a check did NOT happen; this one is the denominator, and a claim without it is the
        // shape of green this project keeps having to retract.
        //
        // 🔴 THE THREE VERIFICATION CLASSES ARE REPORTED SEPARATELY, because "derived" was one word
        // covering two very different claims: a value RECOMPUTED and matched is evidence, a value merely
        // cited to a file is a citation. A single count lets the weaker one hide inside the stronger.
        var denominator =
            $" DENOMINATOR: {present.Count} derivable field(s) present, {records.Count} with a derivation record "
            + $"({records.Count(r => r.Verified == DerivationVerification.Computed)} recomputed and matched, "
            + $"{records.Count(r => r.Verified == DerivationVerification.ByRule)} settled by rule, "
            + $"{records.Count(r => r.Verified == DerivationVerification.Attributed)} cited only), "
            + $"{records.Count(r => r.ArtifactWasRead)} re-hashed against the artifact on disk "
            + $"({records.Count(r => r.HashedOverBytes)} over bytes).";

        if (present.Count == 0)
        {
            return new GateResult(name, GateStatus.Checked, true, nameof(SubmissionGate),
                "no derivable field is present, so nothing here was hand-authored. Their ABSENCE is not this gate's "
                + "question and is not being waved through: the gates that consume them (8 conflicts, 10b compression, "
                + "11 memory layout) report NOT CHECKED without them, and one NOT CHECKED already makes the submission "
                + "NOT ADMISSIBLE." + denominator);
        }

        var authored = present
            .Where(f => !records.Any(r => string.Equals(r.Field, f, StringComparison.Ordinal)))
            .ToArray();

        var unknownProducer = records
            .Where(r => !DerivationProducer.Known.Contains(r.Producer))
            .ToArray();

        var attestsToNothing = records
            .Where(r => !present.Contains(r.Field, StringComparer.Ordinal))
            .ToArray();

        var stale = records.Where(r => r.ArtifactWasRead && !r.HashMatches).ToArray();

        // 🔴 *** A BY-RULE RECORD HAS NO ARTIFACT, AND THAT IS NOT AN UNREAD ONE. *** It is settled by a
        // stated rule precisely because nothing produces a file for it, so demanding one would put the
        // gate permanently at NOT CHECKED for a field that is as verified as it can be. A record
        // claiming ByRule while NAMING an artifact is a different thing and is refused below.
        var unread = records
            .Where(r => r.Verified != DerivationVerification.ByRule)
            .Where(r => !r.ArtifactWasRead)
            .ToArray();

        var ruleWithArtifact = records
            .Where(r => r.Verified == DerivationVerification.ByRule && !string.IsNullOrWhiteSpace(r.Artifact))
            .ToArray();

        var problems = new List<string>();

        if (authored.Length > 0)
        {
            problems.Add(
                $"{authored.Length} field(s) were AUTHORED, not derived: {string.Join(", ", authored)}. "
                + "Each is produced by a tool that already exists; derive it and let the provenance say so.");
        }

        if (unknownProducer.Length > 0)
        {
            problems.Add(
                $"{unknownProducer.Length} record(s) name a producer this gate does not know: "
                + string.Join(", ", unknownProducer.Select(r => $"{r.Field} <- '{r.Producer}'"))
                + ". An open producer set would let the author attest to their own transcription.");
        }

        if (attestsToNothing.Length > 0)
        {
            problems.Add(
                $"{attestsToNothing.Length} record(s) attest to a field the document does not carry: "
                + string.Join(", ", attestsToNothing.Select(r => r.Field))
                + ". The deriver produced it and the submission does not have it, so one of the two lost data.");
        }

        if (stale.Length > 0)
        {
            problems.Add(
                $"{stale.Length} field(s) were derived from an artifact that has CHANGED since: "
                + string.Join(", ", stale.Select(r => $"{r.Field} <- {r.Artifact}"))
                + ". The derivation is stale; re-derive rather than re-stamp.");
        }

        if (ruleWithArtifact.Length > 0)
        {
            problems.Add(
                $"{ruleWithArtifact.Length} record(s) claim to be settled BY RULE while naming an artifact: "
                + string.Join(", ", ruleWithArtifact.Select(r => $"{r.Field} <- {r.Artifact}"))
                + ". By-rule means no file produces this value, so naming one claims a provenance that does not exist "
                + "and exempts itself from the hash check at the same time.");
        }

        if (problems.Count > 0)
        {
            return new GateResult(name, GateStatus.Checked, false, nameof(SubmissionGate),
                string.Join(" ", problems) + denominator);
        }

        if (unread.Length > 0)
        {
            return GateResult.CouldNotRun(name, NotCheckedReason.AwaitingAnArtifactThatCouldExist, nameof(SubmissionGate),
                $"{unread.Length} field(s) carry a derivation this gate could not verify, because the artifact could not be "
                + $"read: {string.Join(", ", unread.Select(r => $"{r.Field} <- {r.Artifact}"))}. An unreadable artifact is "
                + "not a matching one." + denominator);
        }

        return new GateResult(name, GateStatus.Checked, true, nameof(SubmissionGate),
            "every derivable field the submission carries was produced by a named tool, and every artifact still hashes to "
            + "what the derivation recorded." + denominator);
    }

    /// <summary>
    /// Multiplicative headroom over the scenario's own declared length. <b>Measured, not chosen:</b>
    /// across the three vectors of the one wave that has run to completion on a controller, actual scans
    /// over scenario scans were <c>1.25</c>, <c>1.16</c> and <c>1.008</c>. 1.5 clears the worst of those
    /// with room and is nowhere near the ~3.07x the same submission declared.
    /// </summary>
    private const double BackstopMargin = 1.5;

    /// <summary>
    /// Flat allowance on top, because the overhead a scenario pays before its own clock starts — arming,
    /// the inert phase, one poll round of detection lag — <b>does not scale with the scenario</b>. On the
    /// measured wave it was ~400 scans, and it is the whole reason the ratio was 1.25 on the 40-second
    /// vector and 1.008 on the six-minute one. A purely multiplicative bound would be far too tight on a
    /// short scenario and far too loose on a long one.
    /// </summary>
    private const int BackstopFixedOverheadScans = 500;

    /// <summary>
    /// <b>The backstop a vector declares must be bounded by the scenario that vector itself describes.</b>
    ///
    /// <para>🔴 <b>Nothing bounded it before 2026-08-22.</b> <c>MaxDurationScans</c> was authored freely and
    /// the only check was <c>&gt;= 1</c>. It is linear in the deadline
    /// (<see cref="WireTiming.BackstopMs"/>), and <c>WaveRun</c> takes the MAXIMUM across the tensor — so
    /// one generous number set the deadline for every slot at that index. Measured on a real submission:
    /// 6,168 + 8,568 + 44,328 scans declared against 2,007 + 2,793 + 14,468 actually used, a flat ~3.07x
    /// hedge, and <b>~24.7 minutes of wall clock if the slot never completes.</b></para>
    ///
    /// <para><b>Both directions are refused, and the low one is the more dangerous.</b> A backstop BELOW
    /// the scenario's own end cannot be satisfied by a healthy test that runs to that end — it is a
    /// spurious TIMED-OUT by construction, and this file's own note is that a spurious TIMED-OUT is worse
    /// than a spurious FAILED because it is believed.</para>
    ///
    /// <para><b>NOT CHECKED without <c>scenarioEndInput</c>, deliberately.</b> The harness cannot know
    /// which of a stimulus model's inputs carries the scenario's end — that is a property of the model,
    /// not of the wire — so it has to be named. A submission that does not name it leaves this gate NOT
    /// CHECKED, which makes the submission NOT ADMISSIBLE, and that is the intended pressure: the
    /// alternative is a vacuous pass over a number nobody bounded.</para>
    /// </summary>
    /// <summary>
    /// 🔴 <b>Does the SCENARIO scale with the block?</b>
    ///
    /// <para>Compressing a block's timer presets makes its windows run <c>n</c> times faster. The stimulus
    /// model plays its scenario against an IEC timer read in REAL milliseconds, so unless its coordinates
    /// are re-expressed too, <b>the run takes exactly as long as before and every event the vector placed
    /// relative to a window lands somewhere else.</b> The verdicts still appear, and they are about a test
    /// nobody designed — which is why this gates rather than warns.</para>
    ///
    /// <para><b>At comp 1 it is a real computed pass</b>, on the same reasoning gate 10b uses: nothing is
    /// scaled, so nothing can disagree about scale. <b>Omitted above comp 1 is NOT CHECKED</b>, and an
    /// EMPTY list is the positive claim that this stimulus has no time-valued scenario data — the
    /// ramp-to-limit case, whose completion is a count reaching a limit and has no clock at all.</para>
    /// </summary>
    private static GateResult ScenarioScaleGate(IReadOnlyList<SubmissionVector> vectors, int runtimeCompression, IReadOnlyList<string>? scenarioTimeInputs)
    {
        const string name = "10c scenario scale — does the scenario shrink WITH the block";

        if (runtimeCompression <= 1)
        {
            return new GateResult(name, GateStatus.Checked, true, nameof(ScenarioScale),
                $"this wave runs at comp={runtimeCompression}, so no scenario coordinate is re-expressed and none can disagree with the "
                + "block's scale. Computed from the submission, not assumed: at comp 1 the block's presets are unscaled too.");
        }

        if (scenarioTimeInputs is null)
        {
            return GateResult.CouldNotRun(name, NotCheckedReason.AwaitingAnArtifactThatCouldExist, "the submission's scenarioTimeInputs",
                $"this wave runs at comp={runtimeCompression}, so the block IS being compressed, and nothing says which of the vectors' "
                + "inputs are scenario coordinates. Which they are cannot be inferred — a stimulus model's inputs mix times, selectors and "
                + "levels, and its own tick is time-valued and must NOT scale. Declare `scenarioTimeInputs`, or declare it EMPTY to claim "
                + "positively that this stimulus has no time-valued scenario data.");
        }

        // The SAME implementation the loop runs, deliberately. A gate that computed this a second way is
        // how a green here stops being evidence about what the loop will actually write.
        var result = ScenarioScale.Apply(vectors, new RuntimeCompression(runtimeCompression), scenarioTimeInputs);

        return result.Ok
            ? new GateResult(name, GateStatus.Checked, true, nameof(ScenarioScale), result.Report)
            : new GateResult(name, GateStatus.Checked, false, nameof(ScenarioScale), result.Report);
    }

    private static GateResult Backstop(IReadOnlyList<SubmissionVector> vectors, string? scenarioEndInput, int? maxIndexScans)
    {
        const string name = "1b backstop bound — the scenario the vector itself declares";

        if (string.IsNullOrWhiteSpace(scenarioEndInput) && maxIndexScans is null)
        {
            return GateResult.CouldNotRun(name, NotCheckedReason.AwaitingAnArtifactThatCouldExist, "the submission's scenarioEndInput / maxIndexScans",
                "nothing bounds MaxDuration. Declare `scenarioEndInput` (the input path whose value is the scenario's end, in plant "
                + "milliseconds) for the tight per-vector bound, or `maxIndexScans` for a flat ceiling where the vectors have no scenario "
                + "clock at all — a ramp-to-limit test genuinely has no end time, and a ceiling is the only bound available to it. "
                + "It is NOT CHECKED rather than passed because an unbounded backstop is exactly the thing that costs ~24.7 minutes on a wedged slot.");
        }

        var problems = new List<string>();
        var bounded = 0;
        var byCeilingOnly = 0;

        foreach (var v in vectors)
        {
            var label = $"{v.Id} (slot {v.Slot}, index {v.Index})";

            // The flat ceiling applies to EVERY vector when it is declared, scenario clock or not. It is
            // the weaker of the two bounds and never replaces the tighter one — it catches the vector the
            // tighter bound cannot see.
            if (maxIndexScans is int ceiling && v.MaxDurationScans > ceiling)
            {
                var over = (int)((v.MaxDurationScans - ceiling) * WireTiming.ScanPeriodMs / 1000);
                problems.Add($"{label}: MaxDuration is {v.MaxDurationScans} scans against the declared ceiling of {ceiling}. "
                    + $"That is {over} s of wall clock this index would burn before reporting TIMED-OUT, on every index that wedges.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(scenarioEndInput))
            {
                // Bounded by the ceiling and nothing tighter. Counted separately: it is a real bound, and
                // it is NOT the same claim as "checked against the scenario this vector describes".
                byCeilingOnly++;
                bounded++;
                continue;
            }

            if (!v.Inputs.TryGetValue(scenarioEndInput, out var raw))
            {
                if (maxIndexScans is not null)
                {
                    byCeilingOnly++;
                    bounded++;
                    continue;
                }

                problems.Add($"{label}: declares no input `{scenarioEndInput}`, so its scenario has no stated end and its "
                    + $"MaxDuration of {v.MaxDurationScans} scans is bounded by nothing.");
                continue;
            }

            if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var endMs) || endMs < 1)
            {
                problems.Add($"{label}: `{scenarioEndInput}` reads \"{raw}\", which is not a positive whole number of milliseconds.");
                continue;
            }

            var scenarioScans = (int)Math.Ceiling(endMs / WireTiming.ScanPeriodMs);
            var allowed = (int)Math.Ceiling(scenarioScans * BackstopMargin) + BackstopFixedOverheadScans;

            if (v.MaxDurationScans < scenarioScans)
            {
                problems.Add($"{label}: MaxDuration is {v.MaxDurationScans} scans but the scenario runs to {endMs} ms, which is "
                    + $"{scenarioScans} scans at the measured {WireTiming.ScanPeriodMs} ms period. A backstop shorter than the scenario "
                    + "fires on a HEALTHY test, and a spurious TIMED-OUT is believed in a way a spurious FAILED is not.");
                continue;
            }

            if (v.MaxDurationScans > allowed)
            {
                var wasted = (int)((v.MaxDurationScans - allowed) * WireTiming.ScanPeriodMs / 1000);
                problems.Add($"{label}: MaxDuration is {v.MaxDurationScans} scans against a scenario of {scenarioScans} scans ({endMs} ms) — "
                    + $"{v.MaxDurationScans / (double)scenarioScans:0.00}x. The bound is {allowed} scans "
                    + $"(x{BackstopMargin} plus {BackstopFixedOverheadScans} scans of arming and inert overhead, which does not scale with the scenario). "
                    + $"The excess is {wasted} s of wall clock this index would burn before reporting TIMED-OUT, paid on every index that wedges.");
                continue;
            }

            bounded++;
        }

        // The denominator, on every run: every other number here is a reason a vector was NOT bounded.
        // *** THE TWO BOUNDS ARE COUNTED SEPARATELY AND DELIBERATELY. *** "Bounded by a flat ceiling" is a
        // materially weaker claim than "bounded by the scenario this vector itself describes", and
        // collapsing them into one number would let a submission of entirely ceiling-bounded vectors read
        // exactly like one where every backstop was checked against its own scenario.
        var denominator = $"BOUNDED: {bounded} of {vectors.Count} vector(s)"
            + (string.IsNullOrWhiteSpace(scenarioEndInput) ? "" : $", checked against `{scenarioEndInput}`")
            + (byCeilingOnly > 0 ? $" — of which {byCeilingOnly} by the flat ceiling of {maxIndexScans} scan(s) ONLY, with no scenario clock to check against" : "")
            + ".";

        return problems.Count == 0
            ? new GateResult(name, GateStatus.Checked, true, nameof(SubmissionGate),
                $"{denominator} Every backstop sits at or above its scenario's own end and no more than "
                + $"x{BackstopMargin} + {BackstopFixedOverheadScans} scans above it.")
            : new GateResult(name, GateStatus.Checked, false, nameof(SubmissionGate),
                denominator + " " + string.Join(" ", problems));
    }

    private static GateResult Schema(IReadOnlyList<SubmissionVector> vectors)
    {
        var problems = new List<string>();

        foreach (var v in vectors)
        {
            var label = string.IsNullOrWhiteSpace(v.Id) ? "<unnamed vector>" : v.Id;

            if (string.IsNullOrWhiteSpace(v.Id)) problems.Add($"{label}: Id is empty.");
            if (string.IsNullOrWhiteSpace(v.Slot)) problems.Add($"{label}: Slot is empty.");
            if (v.Index < 0) problems.Add($"{label}: Index is negative.");
            if (string.IsNullOrWhiteSpace(v.StartBool)) problems.Add($"{label}: StartBool is empty.");
            if (string.IsNullOrWhiteSpace(v.CompletionSignal)) problems.Add($"{label}: CompletionSignal is empty.");
            if (v.CompressionFactor < 1) problems.Add($"{label}: CompressionFactor is {v.CompressionFactor}. Scan counts are meaningless without the comp they were stated at.");

            // MaxDuration is not a formality: X-B makes it the per-test timeout and DB-13 needs it for
            // wave length, so a vector without one can neither be packed nor bounded.
            if (v.MaxDurationScans < 1)
                problems.Add($"{label}: MaxDuration is {v.MaxDurationScans} scans. X-B makes it the per-test timeout and DB-13 needs it for wave length — a vector without one can neither be packed nor bounded.");

            if (v.Expectations.Count == 0)
                problems.Add($"{label}: no Expectations. A vector that asserts nothing cannot fail, so its pass says nothing.");

            // *** THE PREDICATE. *** Contract section 2 lists one and ObservabilityDeclaration.Expected
            // carries it, and until now NOTHING CHECKED IT — an expectation with no predicate reached the
            // result package, where a null becomes the string "<no predicate>" and is compared against the
            // observed value. That yields a FAILED on a vector that should have been REFUSED: the author is
            // told the block is wrong when what is wrong is that nobody said what right looks like.
            foreach (var e in v.Expectations.Where(e => string.IsNullOrWhiteSpace(e.Expected)))
            {
                problems.Add($"{label}: expectation '{e.Signal}' declares no expected value. An expectation with nothing to compare against cannot fail, so its pass says nothing — and it does not become an error, it becomes a spurious disagreement against a placeholder.");
            }

            // *** THE COMPLETION VALUE. *** SlotRun compares a result register against it and reports
            // TIMED-OUT otherwise, so a default of 1 was a LIVE SILENT-WRONG-ANSWER defect: a block
            // signalling completion with a state number was compared against a value nobody stated, and a
            // HEALTHY block came back as never having finished. The author is then told the block is
            // wrong when what is wrong is that nobody said what "finished" looks like — the same shape as
            // the missing predicate, one field over.
            if (v.CompletionValue is null)
            {
                problems.Add($"{label}: declares no completion VALUE for '{v.CompletionSignal}'. It used to default to 1, which is the harness's convention and not the block's: SlotRun compares a result register against this and reports TIMED-OUT otherwise, so a block that signals completion with a state number reads as never having finished. There is no fail-safe default, so it is required.");
            }
            else if (v.CompletionValue is < 0 or > 65535)
            {
                // The wave casts it with `unchecked((ushort))`, so anything outside the range silently
                // becomes a different number — and the vector would then wait for a value it never named.
                problems.Add($"{label}: completion value {v.CompletionValue} is outside 0..65535. The wave casts it to a holding register with an UNCHECKED conversion, so it would silently become a different value and the vector would wait for a number nobody wrote.");
            }

            // Kills is in the code and absent from contract section 2's format. Section 10 requires
            // mutation testing and this is the only mechanism for it that exists, so it is required here
            // and the discrepancy is raised rather than silently resolved.
            if (string.IsNullOrWhiteSpace(v.Kills))
                problems.Add($"{label}: no Kills. A vector that no credible wrong implementation would fail only measures uptime. (Contract section 2 omits this field; section 10 requires mutation testing and this is the only mechanism for it — raised as a discrepancy, not resolved.)");
        }

        // The completion condition is REPORTED even on a pass, because contract section 2 defines no
        // completion VALUE at all - it names a signal and stops. A field the contract does not define is
        // one an author cannot check against anything, so the gate says what it read rather than leaving
        // the reader to assume the harness's convention of 1.
        var completions = string.Join(", ", vectors
            .Select(v => $"{v.CompletionSignal} reads {v.CompletionValue?.ToString() ?? "<NOT STATED>"}")
            .Distinct(StringComparer.Ordinal));

        // The completion condition is printed on BOTH branches. A report that appears only on good news
        // teaches a reader that its absence means everything was fine — and this is the field whose
        // silent default made a healthy block read as never having finished.
        var completionNote = $" Completion condition(s): {completions} — contract section 2 names a completion SIGNAL and states no VALUE, so this is reported rather than assumed.";

        return new GateResult("1 schema", GateStatus.Checked, problems.Count == 0, nameof(SubmissionGate),
            (problems.Count == 0
                ? $"{vectors.Count} vector(s), every contract section 2 field present and typed."
                : string.Join(" | ", problems))
            + completionNote);
    }

    // -------------------------------------------------------------------------------------------------
    // 2 — authorship (D6)
    // -------------------------------------------------------------------------------------------------

    private static GateResult Authorship(IReadOnlyList<SubmissionVector> vectors, AgentIdentity blockAuthor)
    {
        var problems = new List<string>();

        if (!blockAuthor.IsRecorded)
            problems.Add("the block's author is not recorded, so D6's independence cannot be established. Unknown is not independent.");

        foreach (var v in vectors)
        {
            if (!v.Author.IsRecorded)
                problems.Add($"{v.Id}: the vector's author is not recorded.");
            else if (blockAuthor.IsRecorded && v.Author.SameAs(blockAuthor))
                problems.Add($"{v.Id}: '{v.Author}' wrote both the block and the vector. That is a correlated check and it is a refusal, not a warning.");
        }

        return new GateResult("2 authorship (D6)", GateStatus.Checked, problems.Count == 0, nameof(AgentIdentity),
            problems.Count == 0
                ? $"vector author(s) differ from the block author '{blockAuthor}' under a normalised comparison. NOTE: what MAKES two agents different is undefined — see AgentIdentity."
                : string.Join(" | ", problems));
    }

    // -------------------------------------------------------------------------------------------------
    // 3j — SUBJECT RESOLUTION: which enumeration does this citation mean?
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A CITATION MUST RESOLVE TO (SUBJECT, CLAUSE, ASSERTION) — NOT TO (CLAUSE, ASSERTION).</b>
    ///
    /// <para><b>The measured problem, 2026-08-18.</b> A campaign gained a second enumeration with a
    /// different subject, and <b>four clause IDs appear in both files</b>, each numbering its assertions
    /// from A1. Until this gate existed a submission could hold one enumeration and a citation was checked
    /// with two <c>Contains</c> calls against two flat sets, so the only way to submit a two-subject
    /// campaign was to MERGE them — after which a citation to a shared clause is answered by whichever
    /// entry survived the merge, the denominator reported is the union of two denominators and therefore
    /// neither, and <b>the dangling-citation check passes having examined the wrong set.</b></para>
    ///
    /// <para>🔴 <b>AND THE ASSERTION ID CANNOT BE THE TIE-BREAKER — MEASURED, NOT ASSUMED.</b> An ID is
    /// <c>clause + ":" + hash(normalised text)</c> with no subject term, so two subjects sharing a clause
    /// AND a sentence mint the same ID; the live vessel enumeration records that this has already happened.
    /// A resolver falling back on the hash would be right until the first shared sentence. <b>Matching is at
    /// clause level and the hash is never consulted to disambiguate.</b></para>
    ///
    /// <para><b>A set of one cannot be ambiguous</b>, so every submission written before subjects existed
    /// passes here without qualifying anything, and an unqualified citation still means the file that was
    /// there first. What is refused is a clause declared by TWO subjects with nothing saying which.</para>
    /// </summary>
    private static GateResult SubjectResolution(IReadOnlyList<SubmissionVector> vectors, AssertionEnumerationSet enumerations)
    {
        const string name = "3j subject resolution";

        if (enumerations.IsEmpty)
        {
            return new GateResult(name, GateStatus.Checked, false, nameof(AssertionEnumerationSet),
                $"NOTHING EXAMINED — the submission supplies no assertion enumeration at all, so none of these {vectors.Count} citation(s) "
                + "was resolved to anything. An empty set answers every citation the same way while reading exactly like a resolution that ran (FI-44).");
        }

        var problems = new List<string>();

        // A subject declared twice is malformed before any citation is considered: a citation naming it
        // would resolve to whichever came first, which is the pick this gate exists to prevent, wearing a
        // QUALIFIED citation's clothes so nobody would think to look.
        if (enumerations.DuplicateSubjects.Count > 0)
        {
            problems.Add($"{enumerations.DuplicateSubjects.Count} subject(s) are declared by more than one enumeration: "
                + string.Join(", ", enumerations.DuplicateSubjects)
                + ". Two enumerations under one subject name is two answers to one question, and a citation naming it would resolve to whichever was listed first.");
        }

        foreach (var v in vectors)
        {
            var resolution = enumerations.Resolve(v.Basis);
            if (resolution.IsFailure)
                problems.Add($"{v.Id}: {resolution.State} — {resolution.Detail}");
        }

        var shared = enumerations.SharedClauses();

        // *** THE PASS STATES ITS OWN DENOMINATOR AND ITS OWN SCOPE. *** "every citation resolved" is also
        // true of a submission whose subjects share no clause at all, where this gate had nothing to
        // separate; saying which case it was is the difference between a check and a formality.
        var scope = enumerations.IsSingle
            ? "The submission carries ONE enumeration, so nothing could be ambiguous and no citation needed qualifying — this is the shape every submission had before subjects existed, and it behaves exactly as it did."
            : shared.Count == 0
                ? $"The submission carries {enumerations.Enumerations.Count} subjects ({string.Join(", ", enumerations.Subjects)}) that share NO clause, so every unqualified citation had exactly one home. Nothing here was contested."
                : $"The submission carries {enumerations.Enumerations.Count} subjects ({string.Join(", ", enumerations.Subjects)}) sharing {shared.Count} clause(s) — {string.Join(", ", shared)} — and every citation into those resolved to exactly one subject.";

        return new GateResult(name, GateStatus.Checked, problems.Count == 0, nameof(AssertionEnumerationSet),
            problems.Count == 0
                ? $"all {vectors.Count} citation(s) resolve to exactly one subject. {scope} Denominators: {enumerations.DenominatorText()}."
                : string.Join(" | ", problems));
    }

    /// <summary>
    /// Each vector paired with the enumeration its citation resolved to.
    ///
    /// <para><b>Resolved ONCE and shared, rather than re-derived per gate.</b> Six gates below need to know
    /// which denominator a vector cites into, and six independent derivations of one answer is how the loop
    /// and the CLI came to disagree about twelve gate inputs — the defect <c>GateInputs</c> exists to
    /// prevent, one component in.</para>
    /// </summary>
    private static (SubmissionVector Vector, AssertionEnumeration? Enumeration)[] Resolved(
        IReadOnlyList<SubmissionVector> vectors, AssertionEnumerationSet enumerations) =>
        vectors.Select(v => (v, enumerations.Resolve(v.Basis).Enumeration)).ToArray();

    /// <summary>
    /// The line a per-vector gate adds when some of its subjects could not be placed.
    ///
    /// <para>🔴 <b>AN UNRESOLVED VECTOR IS SKIPPED AND SAID SO — NEVER SKIPPED SILENTLY.</b> A gate that
    /// quietly dropped the vectors it could not place would report a pass over the remainder, and where
    /// EVERY citation is ambiguous it would examine nothing and pass. Empty is not clean.</para>
    /// </summary>
    private static string? UnresolvedNote((SubmissionVector Vector, AssertionEnumeration? Enumeration)[] resolved)
    {
        var unresolved = resolved.Where(r => r.Enumeration is null && r.Vector.Basis is not null).ToArray();

        return unresolved.Length == 0
            ? null
            : $"{unresolved.Length} of {resolved.Length} vector(s) were NOT examined by this gate because their citation resolves to no single subject "
              + $"({string.Join(", ", unresolved.Select(r => r.Vector.Id))}) — see gate 3j. A check that could not be made on some of its subjects has not passed on them.";
    }

    // -------------------------------------------------------------------------------------------------
    // 3 — basis, 4 — fidelity, 6 — settling: delegated to Admissibility, which already computes them
    // -------------------------------------------------------------------------------------------------

    private static GateResult BasisGate(IReadOnlyList<SubmissionVector> vectors, AssertionEnumerationSet enumerations)
    {
        var problems = vectors
            .SelectMany(v => Admissibility.Check(v.Basis, enumerations, FidelityDeclaration.Of("<n/a>", new[] { "x" }, null, true),
                    Array.Empty<string>(), new SettlingDeclaration("n/a", Array.Empty<string>()), v.CompletionSignal,
                    new AgentIdentity("a"), new AgentIdentity("b"), Passing)
                .Refusals
                .Where(r => r.Reason is RefusalReason.BasisNotCited or RefusalReason.ClauseNotEnumerated
                                     or RefusalReason.AssertionNotEnumerated or RefusalReason.EnumerationEmpty
                                     or RefusalReason.AmbiguousSubject or RefusalReason.SubjectNotEnumerated)
                .Select(r => $"{v.Id}: {r.Reason} — {r.Detail}"))
            .ToArray();

        // *** THE DENOMINATOR IS STATED PER SUBJECT AND NEVER SUMMED. *** Summed over two subjects the
        // total is the coverage denominator of neither, and a reader taking a coverage figure from this
        // line would be quoting a number that does not exist.
        return new GateResult("3 basis — clause AND assertion", GateStatus.Checked, problems.Length == 0, nameof(Admissibility),
            problems.Length == 0 ? $"every citation resolves into {enumerations.DenominatorText()}." : string.Join(" | ", problems));
    }

    private static GateResult Fidelity(IReadOnlyList<SubmissionVector> vectors, FidelityDeclaration? fidelity)
    {
        var problems = vectors
            .SelectMany(v => Admissibility.Check(new Basis("c", "a"), AssertionEnumeration.Of(new[] { "c" }, new[] { "a" }),
                    fidelity, v.AssertedBehaviours, new SettlingDeclaration("n/a", Array.Empty<string>()), v.CompletionSignal,
                    new AgentIdentity("a"), new AgentIdentity("b"), Passing)
                .Refusals
                .Where(r => r.Reason is RefusalReason.FidelityExceeded or RefusalReason.FidelityUnusable or RefusalReason.NothingExamined)
                .Select(r => $"{v.Id}: {r.Reason} — {r.Detail}"))
            .ToArray();

        return new GateResult("4 fidelity (M4)", GateStatus.Checked, problems.Length == 0, nameof(Admissibility),
            problems.Length == 0 ? $"every asserted behaviour is in model '{fidelity?.ModelId}'s Represents set." : string.Join(" | ", problems));
    }

    /// <summary>
    /// *** GATE 5's DEFECT, ONE ARTIFACT OVER: WHO SUPPLIED THE FIDELITY LIST? ***
    ///
    /// <para>M4 lets a vector assert only behaviours the model claims to represent — so <b>the Represents
    /// set is what LICENSES the vector's assertions</b>. If the vector's own author wrote that set, the
    /// licence is self-issued and gate 4 compares a claim against its own author's other claim. Measured
    /// 2026-08-14 on a live submission: a model BLOCK existed, a model DECLARATION did not, and the model
    /// id occurred <b>only inside the vector file</b>.</para>
    ///
    /// <para><b>Third instance of this family.</b> The enumeration (3d) was the first; the observability
    /// map was the second, where <c>GateCli</c> read the map out of the submission the vector author
    /// wrote and the CLI was weaker than the loop <i>in exactly the place the CLI decides whether to
    /// proceed</i>. Fixed the same way: name an authority, compare it, and report NOT CHECKED when there
    /// is none.</para>
    /// </summary>
    private static GateResult FidelityAuthority(IReadOnlyList<SubmissionVector> vectors, FidelityDeclaration? fidelity, AgentIdentity blockAuthor)
    {
        const string name = "4b fidelity authority (M3/M4)";

        if (fidelity is null)
        {
            // 🔴 *** RECLASSIFIED 2026-08-14, BY THE SAME MEASUREMENT THAT MOVED GATE 5, AND IT EMPTIED
            // THE CATEGORY. *** This was IndependentAuthorityByDesign — "the Represents set must come
            // from an authority the vector author does not control, so it reads NOT CHECKED for ever".
            // Measured: hand this gate a fidelity declaration that NAMES ITS DECLARER and it returns
            // `Checked` and passes. So the remedy is an artifact, and an artifact that could exist is a
            // build-list item — not a design property.
            //
            // With gate 5 already moved, this was the LAST path producing that reason, and the enum
            // member is now retired rather than left empty. See NotCheckedReason's vacant slot 2.
            return GateResult.CouldNotRun(name, NotCheckedReason.AwaitingAnArtifactThatCouldExist, "a model declaration from an authority the vector author does not control",
                "no fidelity declaration was supplied at all, so there is nothing to attribute. Gate 4 refuses on the absence itself; this reports the separate fact that WHO wrote the Represents set was never established either. "
                + "*** THAT IS A MISSING ARTIFACT, NOT A DESIGN PROPERTY: *** measured 2026-08-14, this gate RUNS and returns a verdict as soon as a declaration naming its declarer is supplied.");
        }

        if (!fidelity.DeclaredBy.IsRecorded)
        {
            return GateResult.CouldNotRun(name, NotCheckedReason.AwaitingAnArtifactThatCouldExist, "the model declaration's own identity field",
                $"model '{fidelity.ModelId}' does not record who declared it, so the list that LICENSES every asserted behaviour (M4) cannot be shown independent of the party it licenses. "
                + "That is the shape found live: a model BLOCK existed, a model DECLARATION did not, and the model id occurred only inside the vector file. Unknown is not independent.");
        }

        var problems = new List<string>();

        foreach (var v in vectors.Where(v => v.Author.IsRecorded && fidelity.DeclaredBy.SameAs(v.Author)))
        {
            problems.Add($"{v.Id}: '{fidelity.DeclaredBy}' both wrote this vector and declared what model '{fidelity.ModelId}' represents. "
                + "*** THE FIDELITY LIST IS SUPPLIED BY THE PARTY WHOSE VECTORS IT LICENSES *** — M4 permits an assertion only if the model claims that behaviour, so an author who writes both permits their own assertions.");
        }

        // The block author is a DIFFERENT conflict and gets its own sentence: a model declared by the
        // block's author describes what the implementation is believed to do, which is the correlated
        // reading this pipeline exists to break.
        if (blockAuthor.IsRecorded && fidelity.DeclaredBy.SameAs(blockAuthor))
        {
            problems.Add($"'{fidelity.DeclaredBy}' both wrote the block and declared what model '{fidelity.ModelId}' represents. The model then describes what the implementation is believed to do, and a vector admitted against it is agreeing with the block by construction.");
        }

        return new GateResult(name, GateStatus.Checked, problems.Count == 0, nameof(AgentIdentity),
            problems.Count == 0
                ? $"model '{fidelity.ModelId}' was declared by '{fidelity.DeclaredBy}', who is neither the block's author nor any vector's — so the Represents set licensing these assertions came from a third party."
                : string.Join(" | ", problems));
    }

    private static GateResult Settling(IReadOnlyList<SubmissionVector> vectors)
    {
        var problems = vectors
            .SelectMany(v => Admissibility.Check(new Basis("c", "a"), AssertionEnumeration.Of(new[] { "c" }, new[] { "a" }),
                    FidelityDeclaration.Of("<n/a>", new[] { "x" }, null, true), Array.Empty<string>(),
                    v.Settling, v.CompletionSignal, new AgentIdentity("a"), new AgentIdentity("b"), Passing)
                .Refusals
                .Where(r => r.Reason is RefusalReason.SettlingNotDeclared or RefusalReason.SettlingIsTheCompletionFlag)
                .Select(r => $"{v.Id}: {r.Reason} — {r.Detail}"))
            .ToArray();

        return new GateResult("6 settling", GateStatus.Checked, problems.Length == 0, nameof(Admissibility),
            problems.Length == 0 ? "every vector declares a settling condition that is not the completion flag alone." : string.Join(" | ", problems));
    }

    /// <summary>
    /// The enumeration is the coverage DENOMINATOR, so who produced it decides whether citing into it
    /// buys anything.
    ///
    /// <para><b>If the block's author performed the decomposition, D6's independence is lost at the
    /// denominator</b> - the same reading that produced the block produced the set of things anyone may
    /// assert about it, and the vector author's citation stops being a second reading. An UNRECORDED
    /// enumerator is NOT CHECKED, never a pass: unknown is not independent.</para>
    /// </summary>
    /// <remarks>
    /// <b>EVERY enumeration in the set must clear this, not the one that happens to be first.</b> A
    /// submission whose vessel denominator was written by the vessel block's author is exactly as
    /// correlated as one whose only denominator was — and it would sit behind a valve enumeration with an
    /// impeccable third-party enumerator. An UNRECORDED enumerator on ANY of them is NOT CHECKED for the
    /// whole gate: a partially-attributed set is not an attributed one.
    /// </remarks>
    private static GateResult EnumeratorIndependence(IReadOnlyList<SubmissionVector> vectors, AssertionEnumerationSet enumerations, AgentIdentity blockAuthor)
    {
        var unattributed = enumerations.Enumerations.Where(e => !e.Enumerator.IsRecorded).ToArray();

        if (enumerations.IsEmpty || unattributed.Length > 0)
        {
            var which = enumerations.IsEmpty
                ? "no enumeration was supplied"
                : enumerations.IsSingle
                    ? "the enumeration does not record who produced it"
                    : $"{unattributed.Length} of {enumerations.Enumerations.Count} enumerations record no enumerator ({string.Join(", ", unattributed.Select(e => AssertionEnumerationSet.DisplaySubject(e.Subject)))})";

            return GateResult.CouldNotRun("3d enumerator independence", NotCheckedReason.AwaitingAnArtifactThatCouldExist, "the enumeration's own identity field",
                $"{which}, so it cannot be shown independent of the block's author. If the block's author decomposed the requirement, D6's independence is lost AT THE DENOMINATOR and citing into it buys nothing. Unknown is not independent.");
        }

        var problems = new List<string>();

        foreach (var enumeration in enumerations.Enumerations)
        {
            var where = enumerations.IsSingle ? string.Empty : $"[{AssertionEnumerationSet.DisplaySubject(enumeration.Subject)}] ";

            if (blockAuthor.IsRecorded && enumeration.Enumerator.SameAs(blockAuthor))
                problems.Add($"{where}'{enumeration.Enumerator}' both wrote the block and enumerated its assertions. The denominator is then the block author's own reading of the requirement, and a vector citing into it is agreeing with the block by construction.");

            foreach (var v in vectors.Where(v => v.Author.IsRecorded && enumeration.Enumerator.SameAs(v.Author)))
                problems.Add($"{v.Id}: {where}'{enumeration.Enumerator}' both enumerated the assertions and wrote this vector. The enumeration is meant to be a THIRD party to both authors.");
        }

        var producers = enumerations.Enumerations
            .Select(e => e.Enumerator.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new GateResult("3d enumerator independence", GateStatus.Checked, problems.Count == 0, nameof(AgentIdentity),
            problems.Count == 0
                ? enumerations.IsSingle
                    ? $"the enumeration was produced by '{enumerations.Enumerations[0].Enumerator}', who is neither the block's author nor any vector's."
                    : $"all {enumerations.Enumerations.Count} enumerations were produced by {string.Join(", ", producers.Select(p => $"'{p}'"))}, none of whom is the block's author or any vector's."
                : string.Join(" | ", problems));
    }

    // -------------------------------------------------------------------------------------------------
    // 3f — the citation's SHAPE, and 3g — the IDs recompute (assertion-enumeration.md §3.3, §3.4)
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// *** A `Basis` IN THE DISPLAY-ORDINAL FORM IS REJECTED, MECHANICALLY, BY SHAPE (§3.3). ***
    ///
    /// <para>Listings show <c>REQ-014.A2</c> because <c>REQ-014:3f9a1c</c> is unreadable aloud —
    /// <b>which is exactly why the ordinal is the one people would type.</b> It is positional, so a
    /// citation in it silently comes to name a different assertion the moment one is inserted above it.
    /// The rejection is by shape rather than by lookup because a lookup miss reads as "you cited
    /// something that does not exist", and this is a different error with a different fix.</para>
    /// </summary>
    private static GateResult CitationShape(IReadOnlyList<SubmissionVector> vectors)
    {
        var problems = new List<string>();

        foreach (var v in vectors)
        {
            if (v.Basis is null || string.IsNullOrWhiteSpace(v.Basis.AssertionId))
                continue;

            if (AssertionId.IsDisplayOrdinalForm(v.Basis.AssertionId))
            {
                problems.Add($"{v.Id}: cites '{v.Basis.AssertionId}', which is the DISPLAY ORDINAL. It is display only and is never citable (§3.3): the ordinal is POSITIONAL, so inserting one assertion above it silently makes this citation name a different one. Cite the content-derived ID — <clause>:<six lowercase hex>.");
            }
        }

        return new GateResult("3f citation shape", GateStatus.Checked, problems.Count == 0, nameof(AssertionId),
            problems.Count == 0
                ? "no vector cites a display ordinal. Citations are content-derived, so none of them is positional."
                : string.Join(" | ", problems));
    }

    /// <summary>
    /// *** THE RECOMPUTATION THAT MAKES THE STAMPER SAFE TO BE ANYBODY (§3.4). ***
    ///
    /// <para>The enumerator issues <c>normalised_text:</c> and no <c>id:</c> — it is denied <c>Bash</c>
    /// on purpose, and an ID is a SHA-256, so the agent responsible for the denominator cannot produce
    /// the identifiers it is cited by. A separate stamping step writes them in. <b>That step needs no
    /// independence from the block author or the vector author for exactly one reason: this gate
    /// recomputes every ID from the text and refuses a mismatch.</b> A wrong hash is caught, and a right
    /// one is what anybody would have produced.</para>
    ///
    /// <para><b>So if this stops running, the stamper becomes trusted, and it was never designed to
    /// be.</b> An enumeration carrying no normalised text is therefore NOT CHECKED — not a pass. The
    /// projection that omits the texts is exactly the one a compromised stamper would emit.</para>
    /// </summary>
    /// <remarks>
    /// <b>Run over EVERY enumeration in the set, each against its OWN clause set.</b> An ID is scoped to
    /// its clause and now, transitively, to its subject: recomputing a vessel assertion against the union
    /// of both clause sets would accept an ID scoped to the wrong file, which is the merge this gate table
    /// stopped relying on. One enumeration carrying no normalised text takes the WHOLE gate to NOT CHECKED
    /// — a partially-verifiable set is not a verified one, and the unverified half is exactly where a
    /// wrong ID would sit.
    /// </remarks>
    private static GateResult IdsRecompute(AssertionEnumerationSet enumerations)
    {
        const string name = "3g assertion IDs recompute";

        var untexted = enumerations.Enumerations.Where(e => e.CarriesNoNormalisedText).ToArray();

        if (enumerations.IsEmpty || untexted.Length > 0)
        {
            var which = enumerations.IsEmpty
                ? "no enumeration was supplied, so nothing"
                : enumerations.IsSingle
                    ? "the enumeration carries no normalised text, so no ID"
                    : $"{untexted.Length} of {enumerations.Enumerations.Count} enumerations carry no normalised text ({string.Join(", ", untexted.Select(e => AssertionEnumerationSet.DisplaySubject(e.Subject)))}), so their IDs";

            return GateResult.CouldNotRun(name, NotCheckedReason.AwaitingAnArtifactThatCouldExist, "normalised_text in the enumeration projection",
                $"{which} could be recomputed and the STAMPER'S OUTPUT WAS TAKEN ON TRUST. "
                + "§3.4 permits a stamper with no independence from the block or vector author ONLY because this recomputation happens; without it "
                + "a hand-written or altered hex string is indistinguishable from a computed one. Emit `normalisedTexts` alongside `assertions`.");
        }

        var problems = new List<string>();

        foreach (var enumeration in enumerations.Enumerations)
            RecomputeOne(enumeration, enumerations.IsSingle, problems);

        var total = enumerations.Enumerations.Sum(e => e.Assertions.Count);

        return new GateResult(name, GateStatus.Checked, problems.Count == 0, nameof(AssertionId),
            problems.Count == 0
                ? enumerations.IsSingle
                    ? $"all {total} assertion ID(s) recompute from their own normalised text. The stamper is verified rather than trusted."
                    : $"all {total} assertion ID(s) across {enumerations.Enumerations.Count} subjects recompute from their own normalised text, each against ITS OWN clause set. The stamper is verified rather than trusted."
                : string.Join(" | ", problems));
    }

    private static void RecomputeOne(AssertionEnumeration enumeration, bool single, List<string> problems)
    {
        var where = single ? string.Empty : $"[{AssertionEnumerationSet.DisplaySubject(enumeration.Subject)}] ";

        foreach (var (id, text) in enumeration.NormalisedTexts!.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (!enumeration.Assertions.Contains(id))
            {
                problems.Add($"{where}'{id}' has a normalised text but is not in the assertion set. The projection disagrees with itself, and there is no way to tell which half is right.");
                continue;
            }

            if (!AssertionId.TryParse(id, out var clauseId, out _))
            {
                problems.Add($"{where}'{id}' is not an assertion ID (<clause>:<six lowercase hex>), so nothing can be recomputed for it.");
                continue;
            }

            if (!enumeration.Clauses.Contains(clauseId))
            {
                problems.Add($"{where}'{id}' names clause '{clauseId}', which is not in the enumeration's clause set. An ID is scoped to its clause, so this one is scoped to nothing.");
                continue;
            }

            if (AssertionId.Normalise(text) != text)
            {
                problems.Add($"{where}'{id}': the supplied text is not itself normalised (§3.1). Normalising it here would change the ID being checked, so the check is refused rather than made to pass.");
                continue;
            }

            var recomputed = AssertionId.Compute(clauseId, text);
            if (!string.Equals(recomputed, id, StringComparison.Ordinal))
            {
                problems.Add($"{where}'{id}' DOES NOT RECOMPUTE — its own normalised text hashes to '{recomputed}'. Either the text was edited without re-stamping (every citation to '{id}' is then STALE, §3.2) or the ID was not computed from this text at all.");
            }
        }

        var uncovered = enumeration.Assertions.Where(a => enumeration.NormalisedTextOf(a) is null).ToArray();
        if (uncovered.Length > 0)
        {
            problems.Add($"{where}{uncovered.Length} assertion(s) carry no normalised text and were NOT recomputed: {string.Join(", ", uncovered.OrderBy(a => a, StringComparer.Ordinal).Take(5))}"
                + (uncovered.Length > 5 ? ", …" : string.Empty)
                + ". A partially-verifiable enumeration is not a verified one — the unverified ones are exactly where a wrong ID would sit.");
        }
    }

    /// <summary>
    /// *** AMB-14: EVERY SIGNAL A CITATION DEPENDS ON, NOT JUST ONE. ***
    ///
    /// <para>The mechanical check was <i>"the response signal appears in <c>Expectations</c>"</i>, and a
    /// response signal was a SINGLE STRING. <b>A simultaneity claim has two signals, and both must be
    /// observed for the citation to mean anything.</b> A vector could cite a relational assertion, expect
    /// on one output alone, never observe the other, and pass — <b>while nothing about the RELATION was
    /// tested</b>. That is a check reporting on a claim it did not examine, which is the shape this whole
    /// gate table exists to refuse.</para>
    ///
    /// <para><b>An enumeration that declares none is NOT CHECKED, not a pass</b> — same treatment as the
    /// forms map and the normalised texts, and for the same reason: against a projection that carries
    /// nothing, the hole is exactly as open as it was.</para>
    ///
    /// <para><b>This is a FIELD, and it must stay one.</b> No hashed assertion text in this format
    /// contains a signal name or a numeric bound, which is why four re-issues cost zero re-hashes when an
    /// output was renamed. Closing AMB-14 by editing an assertion's text to name both signals would give
    /// that property away to fix something that lives beside the sentence.</para>
    /// </summary>
    private static GateResult RequiredObservations(IReadOnlyList<SubmissionVector> vectors, AssertionEnumerationSet enumerations)
    {
        const string name = "3h required observations (AMB-14)";

        var resolved = Resolved(vectors, enumerations);

        // *** THE CONDITION IS ASKED OF THE ENUMERATIONS THESE VECTORS ACTUALLY CITE INTO. *** Asking it of
        // the whole set would let a fully-declared valve enumeration carry an undeclared vessel one through;
        // asking it of a merge would compare a citation against a table assembled from both.
        var consulted = resolved.Where(r => r.Enumeration is not null).Select(r => r.Enumeration!).Distinct().ToArray();
        var undeclared = consulted.Where(e => e.CarriesNoRequiredObservations).ToArray();

        if (enumerations.IsEmpty || consulted.Length == 0 || undeclared.Length > 0)
        {
            var which = enumerations.IsEmpty || consulted.Length == 0
                ? "no enumeration this submission cites into could be identified, so nothing"
                : enumerations.IsSingle
                    ? "the enumeration declares no required observations, so no citation"
                    : $"{undeclared.Length} of the {consulted.Length} enumerations these vectors cite into declare no required observations ({string.Join(", ", undeclared.Select(e => AssertionEnumerationSet.DisplaySubject(e.Subject)))}), so their citations";

            return GateResult.CouldNotRun(name, NotCheckedReason.AwaitingAnArtifactThatCouldExist, "response_signal + also_requires_observation_of in the enumeration",
                $"{which} was checked against the signals it depends on. "
                + "A RELATIONAL assertion is the case that matters: cite one, expect on one of its two outputs, never observe the other, and the relation is untested while everything reports green.");
        }

        var problems = new List<string>();

        foreach (var (v, enumeration) in resolved)
        {
            if (v.Basis is null || string.IsNullOrWhiteSpace(v.Basis.AssertionId) || enumeration is null)
                continue;

            var required = enumeration.RequiredObservationsOf(v.Basis.AssertionId);

            if (required is null)
            {
                problems.Add($"{v.Id}: the enumeration declares required observations but none for '{v.Basis.AssertionId}', so this citation was checked against nothing. A partially-declared enumeration is not a permissive one.");
                continue;
            }

            if (required.Count == 0)
            {
                problems.Add($"{v.Id}: '{v.Basis.AssertionId}' declares an EMPTY set of required observations. An assertion whose response nobody named cannot have a vector shown to observe it — empty is not clean.");
                continue;
            }

            var observed = v.Expectations.Select(e => e.Signal).ToHashSet(StringComparer.Ordinal);
            var missing = required.Where(s => !observed.Contains(s)).ToArray();

            if (missing.Length > 0)
            {
                problems.Add($"{v.Id}: cites '{v.Basis.AssertionId}', which depends on {string.Join(", ", required.OrderBy(s => s, StringComparer.Ordinal))}, and declares no expectation on {string.Join(", ", missing.OrderBy(s => s, StringComparer.Ordinal))}. "
                    + (required.Count > 1
                        ? "*** THIS IS AMB-14's CASE. *** The assertion relates these signals, so observing one of them tests neither the other nor the relation, and a pass here would say nothing about what was cited."
                        : "The assertion's response is that signal; a vector that never observes it cannot have tested the assertion it cites."));
            }
        }

        var unresolved = UnresolvedNote(resolved);
        if (unresolved is not null)
            problems.Add(unresolved);

        return new GateResult(name, GateStatus.Checked, problems.Count == 0, nameof(AssertionEnumeration),
            problems.Count == 0
                ? $"every citation observes EVERY signal its assertion depends on, across {vectors.Count(v => v.Basis is not null)} cited vector(s)."
                : string.Join(" | ", problems));
    }

    // -------------------------------------------------------------------------------------------------
    // 3i — bounds currency (AMB-19)
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>Does this vector still test the number the specification currently states?</b>
    ///
    /// <para>*** THE HOLE THIS CLOSES IS MADE ENTIRELY OUT OF CORRECT DECISIONS. *** No hashed assertion
    /// text contains a numeric bound — that is what makes a re-issue cost zero re-hashes, and it is the
    /// property gate 3g and gate 3h both lean on. The enumeration therefore says, rightly, that retuning
    /// the bounds table re-hashes nothing. Put those together and <b>a retune changes the truth
    /// conditions of every assertion referring to the table while moving ZERO IDs</b> — 22 of 27 on the
    /// hopper enumeration. A vector written against the old number goes on passing: no dangling citation
    /// for gate 3 to catch, no ID mismatch for gate 3g to catch, and no <c>Stale</c> verdict, because
    /// staleness keys on assertion IDs and not on bound values.</para>
    ///
    /// <para>🔴 <b>A MISMATCH REFUSES THE SUBMISSION AND IS REPORTED AS STALE, NEVER AS A FAILURE OF THE
    /// BLOCK.</b> The vector may have been perfectly correct when written; what changed is underneath it.
    /// Reporting that as a disagreement would send an agent to edit correct logic — the identical defect
    /// to the missing-predicate case, where a vector's own omission surfaced as the block being wrong.
    /// <see cref="ResultPackage.Verdict"/> carries the same distinction through to the result.</para>
    ///
    /// <para><b>NOT CHECKED means a comparison could not be made</b>, and it fails closed: no table at
    /// all, or a vector that says nothing at all about bounds. The second is the hole itself rather than a
    /// formality — <b>a vector that never records which number it was written against can never be found
    /// stale by anything</b>, so it is precisely the vector that survives a retune in silence.</para>
    ///
    /// <para>🔴 <b>BUT SILENCE AND <c>boundsUsed: {}</c> ARE DIFFERENT CLAIMS, AND UNTIL 2026-08-17 THIS
    /// GATE TREATED THEM AS ONE.</b> Measured on a real submission: two vectors cited assertions that
    /// genuinely carry no bound, recorded that truthfully as an empty map, and were refused — so <b>the
    /// only way to clear the refusal was to invent a bound</b>, which is exactly the fabrication this gate
    /// exists to prevent. <i>A gate that can only be satisfied by making something up is inverted.</i> The
    /// empty claim is now CHECKED — <b>and verified rather than taken</b>: it passes only where the
    /// enumeration confirms the cited assertion depends on no bound, is NOT CHECKED where the enumeration
    /// cannot say, and is a <b>refusal</b> where the enumeration names a bound the vector claims does not
    /// apply.</para>
    /// </summary>
    /// <remarks>
    /// 🔴 <b>THE TABLE IS THE ONE THE VECTOR'S OWN SUBJECT PUBLISHES — NEVER A MERGE OF ALL OF THEM.</b>
    /// Bound names are BARE (<c>persistence_threshold</c>), carrying no subject and no clause, so two
    /// subject files' tables collide by name wherever they share one. Measured on the live campaign:
    /// <b>9 bound names appear in both enumerations.</b> Their values agree today — which is luck, not a
    /// property — and a merge would compare a vector against whichever entry survived the moment one
    /// subject retuned. That is AMB-19 re-opened across a file boundary, and it would move no assertion ID.
    /// </remarks>
    private static GateResult BoundsCurrency(IReadOnlyList<SubmissionVector> vectors, AssertionEnumerationSet enumerations)
    {
        const string name = "3i bounds currency (AMB-19)";
        const string verifier = nameof(BoundsCurrencyCheck) + ", against the enumeration's bounds table";

        var resolved = Resolved(vectors, enumerations);
        var consulted = resolved.Where(r => r.Enumeration is not null).Select(r => r.Enumeration!).Distinct().ToArray();
        var boundless = consulted.Where(e => e.CarriesNoBounds).ToArray();

        if (enumerations.IsEmpty || consulted.Length == 0 || boundless.Length > 0)
        {
            var which = enumerations.IsEmpty || consulted.Length == 0
                ? $"no enumeration this submission cites into could be identified, so the number each of these {vectors.Count} vector(s) was written against"
                : enumerations.IsSingle
                    ? $"the enumeration supplied no `bounds` table, so the number each of these {vectors.Count} vector(s) was written against"
                    : $"{boundless.Length} of the {consulted.Length} enumerations these vectors cite into supply no `bounds` table ({string.Join(", ", boundless.Select(e => AssertionEnumerationSet.DisplaySubject(e.Subject)))}), so the numbers their vectors were written against";

            return GateResult.CouldNotRun(name, NotCheckedReason.AwaitingAnArtifactThatCouldExist, verifier,
                $"{which} was compared against nothing. "
                + "*** AN ABSENT TABLE IS NOT AN AGREEING ONE. *** This is AMB-19's channel wide open: a bound can be retuned, every assertion referring to it changes what it is true of, "
                + "no assertion ID moves, and every other gate here stays green. Supply the enumeration's `bounds:` table as `enumeration.bounds`.");
        }

        // *** THE EXPECTATION COMES FROM THE ENUMERATION AND NEVER FROM THE VECTOR. *** A vector claiming
        // "no bound applies to me" checked against its own claim is not checked at all; the enumeration is
        // the third party, and where it does not answer the finding says NOT CHECKED rather than passing.
        var findings = resolved
            .Where(r => r.Enumeration is not null)
            .Select(r => BoundsCurrencyCheck.Evaluate(
                r.Vector.Id,
                r.Vector.BoundsUsed,
                r.Enumeration!.Bounds,
                r.Enumeration.BoundsExpectationFor(r.Vector.Basis?.AssertionId)))
            .ToArray();

        // An unresolved citation has no third party to be compared against, so this gate did not examine it
        // — and a gate that quietly dropped its unexaminable subjects would pass over the remainder.
        var unresolvedBounds = UnresolvedNote(resolved);
        if (unresolvedBounds is not null)
        {
            return GateResult.CouldNotRun(name, NotCheckedReason.AwaitingAnArtifactThatCouldExist, verifier,
                unresolvedBounds + " A bound can only be compared against the table the vector's own SUBJECT publishes, and until the citation resolves there is no such table.");
        }

        var refused = findings.Where(f => f.PremiseOutOfDate).ToArray();
        var undeclared = findings.Where(f => f.State == BoundsCurrencyState.NotDeclared).ToArray();
        var unverifiable = findings.Where(f => f.State == BoundsCurrencyState.NoBoundsClaimUnverified).ToArray();
        var noBounds = findings.Where(f => f.State == BoundsCurrencyState.NoBoundsCited).ToArray();

        // The NOT-CHECKED group is reported FIRST and keeps its own status even when refusable vectors
        // were also found, because the two are different facts and the weaker one must not be dressed in
        // the stronger one's status: "we compared and refused" would hide "and these others we could not
        // compare at all". Both refuse the submission, so nothing is admitted either way — only the report
        // differs, and the report is the build list.
        if (undeclared.Length > 0 || unverifiable.Length > 0)
        {
            var parts = new List<string>();

            if (undeclared.Length > 0)
            {
                parts.Add(
                    $"{undeclared.Length} of {vectors.Count} vector(s) say NOTHING AT ALL about bounds, so nothing could establish whether they still test the specified number: "
                    + string.Join(" | ", undeclared.Select(f => f.VectorId))
                    + ". *** A VECTOR THAT RECORDS NO BOUND CANNOT BE FOUND STALE BY ANYTHING *** — it survives a retune with every mechanical check green, which is AMB-19 exactly. "
                    + "Declare `boundsUsed` on each vector: bound name to the value it was written against. If the cited assertion genuinely has no bound, declare it EMPTY — that is a different claim and it is checkable.");
            }

            if (unverifiable.Length > 0)
            {
                parts.Add(
                    $"{unverifiable.Length} of {vectors.Count} vector(s) POSITIVELY CLAIM they were written against no bound, and the enumeration cannot confirm it: "
                    + string.Join(" | ", unverifiable.Select(f => f.Detail))
                    + " *** THE REPAIR IS TO THE ENUMERATION, NOT TO THESE VECTORS. *** Supply `enumeration.assertionBounds` — assertion ID to the bound names that assertion depends on, with an EMPTY list "
                    + "meaning 'this one depends on none'. DO NOT invent a bound on the vector to clear this: that is the fabrication this gate exists to prevent, and a gate satisfiable only by making something up is inverted.");
            }

            if (refused.Length > 0)
            {
                parts.Add("AND, SEPARATELY, THESE WERE COMPARED AND DISAGREE — STALE, NOT FAILED, and not a defect in the block: "
                    + string.Join(" | ", refused.Select(f => f.Detail)));
            }

            return GateResult.CouldNotRun(name, NotCheckedReason.AwaitingAnArtifactThatCouldExist, verifier, string.Join(" ", parts));
        }

        if (refused.Length > 0)
        {
            return new GateResult(name, GateStatus.Checked, false, verifier,
                string.Join(" | ", refused.Select(f => f.Detail))
                + " *** THE BLOCK IS NOT ACCUSED OF ANYTHING HERE. Do NOT edit the block on the strength of this finding. ***");
        }

        var agreed = findings.SelectMany(f => f.Agreed).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var compared = findings.Count(f => f.State == BoundsCurrencyState.Current);

        // The pass states its own denominator, and it states the two kinds of pass separately. A run in
        // which EVERY vector legitimately cites no bound compares no value at all — `agreed` is empty —
        // and a summary that printed "every one matches the current table ()" would be an invariance claim
        // over an empty remainder. Same shape as drift-check's COMPARED: line.
        return new GateResult(name, GateStatus.Checked, true, verifier,
            $"all {vectors.Count} vector(s) state which bound they were written against. "
            + (compared > 0
                ? $"{compared} declared a value and every one matches the enumeration's current table ({string.Join("; ", agreed)}). "
                : "NO VECTOR HERE DECLARED A VALUE, so no value was compared — the pass rests entirely on the verified no-bound claims below. ")
            + (noBounds.Length > 0
                ? $"{noBounds.Length} positively state they were written against NO bound, and the enumeration confirms each cited assertion depends on none ({string.Join("; ", noBounds.Select(f => $"{f.VectorId} cites {f.CitedAssertion}"))}) — VERIFIED against the enumeration, not taken from the vector. "
                : string.Empty)
            + "A retune of any specified value would now refuse this submission rather than silently changing what it tests.");
    }

    /// <summary>
    /// <b>Where F-3 gets its authority.</b>
    ///
    /// <para>F-3 refuses a SAMPLED observation of a NEVER assertion, and the form was declared by the
    /// VECTOR - so the ruling was enforced against what an author claimed, and an author who cited a
    /// NEVER and declared WHEN took the permissive path. An enumeration that carries the form lets the
    /// two be compared.</para>
    ///
    /// <para><b>An enumeration carrying no forms is NOT CHECKED, not a pass.</b> That is the flat
    /// projection, and against it the hole is exactly as open as it was - reporting it as verified would
    /// be the failure this whole gate table exists to prevent.</para>
    /// </summary>
    private static GateResult AssertionFormAuthority(IReadOnlyList<SubmissionVector> vectors, AssertionEnumerationSet enumerations)
    {
        var resolved = Resolved(vectors, enumerations);
        var consulted = resolved.Where(r => r.Enumeration is not null).Select(r => r.Enumeration!).Distinct().ToArray();
        var formless = consulted.Where(e => e.CarriesNoForms).ToArray();

        if (enumerations.IsEmpty || consulted.Length == 0 || formless.Length > 0)
        {
            var which = enumerations.IsEmpty || consulted.Length == 0
                ? "no enumeration this submission cites into could be identified"
                : enumerations.IsSingle
                    ? "the enumeration is the flat projection (clause and assertion IDs only) and carries no canonical form"
                    : $"{formless.Length} of the {consulted.Length} enumerations these vectors cite into are the flat projection and carry no canonical form ({string.Join(", ", formless.Select(e => AssertionEnumerationSet.DisplaySubject(e.Subject)))})";

            return GateResult.CouldNotRun("3e assertion form authority", NotCheckedReason.AwaitingAnArtifactThatCouldExist, "per-assertion form in the enumeration",
                $"{which}, so a vector's declared form was compared against nothing. F-3's refusal of a SAMPLED NEVER is therefore enforced against WHAT THE VECTOR CLAIMS: cite a NEVER, declare WHEN, take the permissive path.");
        }

        var problems = new List<string>();

        foreach (var (v, enumeration) in resolved)
        {
            if (v.Basis is null || enumeration is null)
                continue;

            var declared = v.Form;
            var enumerated = enumeration.FormOf(v.Basis.AssertionId);

            if (enumerated is null)
            {
                problems.Add($"{v.Id}: the enumeration carries forms but none for '{v.Basis.AssertionId}', so this citation's form could not be checked. A partially-formed enumeration is not a permissive one.");
            }
            else if (enumerated == AssertionForm.Unstated)
            {
                problems.Add($"{v.Id}: the enumeration lists '{v.Basis.AssertionId}' with an UNSTATED form. The form is the enumeration's to state, and a blank there is not a WHEN - it is a decomposition that has not been finished. F-3 cannot be enforced against it.");
            }
            else if (declared == AssertionForm.Unstated)
            {
                problems.Add($"{v.Id}: declares no assertion form. *** A DROPPED FORM FAILS THE SAME COMPARISON AS A WRONG ONE, DELIBERATELY *** - the form decides whether a SAMPLED observation is admissible (F-3), so a field that defaulted to WHEN would hand every author who omitted it the permissive path. The enumeration says '{v.Basis.AssertionId}' is {enumerated}; declare it.");
            }
            else if (enumerated != declared)
                problems.Add($"{v.Id}: declares form {declared} and the enumeration says '{v.Basis.AssertionId}' is {enumerated}. The assertion's form is the ENUMERATION's to state; a vector that disagrees with it is asserting something other than what it cites - and if the disagreement is Never-declared-as-When it is F-3's refusal being walked around.");
        }

        var unresolvedForms = UnresolvedNote(resolved);
        if (unresolvedForms is not null)
            problems.Add(unresolvedForms);

        return new GateResult("3e assertion form authority", GateStatus.Checked, problems.Count == 0, nameof(AssertionEnumeration),
            problems.Count == 0
                ? "every citation's declared form matches the enumeration's, so F-3 is enforced against what the assertion IS rather than what the vector claims."
                : string.Join(" | ", problems));
    }

    // -------------------------------------------------------------------------------------------------
    // 5 — observability, COMPUTED
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The latch admission — <b>rendered for every report that carried a latch claim, passing or not.</b>
    ///
    /// <para>Its own method so there is ONE place it can be omitted from, and so the "could any input
    /// reach a branch that omits it?" question has a single answer: no. It is appended unconditionally by
    /// the only caller, and when nothing claims a latch it says THAT rather than nothing — an empty string
    /// here would be indistinguishable from the defect it replaces.</para>
    /// </summary>
    private static string LatchAdmission(MirrorObservability map) =>
        map.LatchProvenance.Count == 0
            ? " No signal claims a latch: the copy layer emits none for these signals, and none was declared."
            : " *** LATCH CLAIMS ADMITTED ON PROVENANCE, NOT ON VERIFICATION: *** "
              + string.Join("; ", map.LatchProvenance.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => $"{e.Key} latched by {e.Value}"))
              + ". Verify those blocks are in the deployment - THIS GATE TAKES THE NAME, NOT THE FACT.";

    private static GateResult Observability(IReadOnlyList<SubmissionVector> vectors, AssertionEnumerationSet enumerations, MirrorObservability map, double floorScans, int runtimeCompression)
    {
        // 🔴 *** THE MAP'S AUTHORITY IS CHECKED BEFORE THE MAP IS USED. *** This gate compares what a
        // vector asks to observe against what the copy layer PROVIDES — so a map supplied by the vector
        // author is the author vouching for the artifact they are being checked against. Measured: the
        // standalone CLI took its map from the submission while the loop took the same map from the
        // coordinator's bindings, which made the CLI WEAKER than the loop in exactly the place the CLI
        // decides whether to proceed — and it is consulted FIRST, so a weaker gate there is worse than an
        // absent one. It refuses to be the deciding voice rather than being quietly permissive.
        if (map.Provenance != MapProvenance.Bindings)
        {
            // 🔴 *** RECLASSIFIED 2026-08-14, BY MEASUREMENT RATHER THAN BY ARGUMENT. *** This was
            // IndependentAuthorityByDesign — "it reads NOT CHECKED for ever, and that is correct". The
            // REFUSAL of a self-declared map is indeed permanent and correct; the NOT CHECKED is not.
            // Driven with `--binding` pointed at a real, loadable coordinator binding, this gate BECOMES
            // CHECKED and returns a verdict. So the remedy is an artifact, and an artifact that could
            // exist is a build-list item, not a design property.
            //
            // What the measurement actually found, and it is the useful half: the HBA coordinator binding
            // EXISTS — as PROSE (`harness-binding.md`). `--binding` needs it as DATA. A document that
            // exists and cannot be loaded is not the same as one nobody wrote, and calling it "by design"
            // would have retired a transcription job as a law of nature.
            return GateResult.CouldNotRun("5 observability", NotCheckedReason.AwaitingAnArtifactThatCouldExist, "a map derived from the coordinator's bindings",
                map.Provenance == MapProvenance.SelfDeclared
                    ? "the observability map came out of the SUBMISSION — the vector author declaring what the copy layer provides, "
                      + "which is the author vouching for the artifact this gate exists to check them against. *** A SELF-DECLARED MAP "
                      + "CANNOT BE THE DECIDING VOICE, *** and this gate is consulted before anything is spent, so being quietly "
                      + "permissive here is worse than not running. Supply the coordinator's bindings (`--binding`), which is the "
                      + "source the loop itself uses. *** THAT IS A USAGE FACT, NOT A DESIGN PROPERTY: *** measured 2026-08-14, this "
                      + "gate runs and returns a verdict the moment a loadable binding is passed. The one for this block exists as "
                      + "PROSE and not as data, so what is owed is a transcription, not a decision."
                    : "nothing said where the observability map came from. That is NOT CHECKED rather than trusted: the whole value of "
                      + "this gate is that the map is a THIRD-PARTY statement of what the copy layer provides, and an unattributed map "
                      + "is indistinguishable from one the vector author wrote.");
        }

        // 🔴 *** A TAG WITH NO STATED SPECIFICATION NAME IS A JOIN NOBODY MADE, AND IT IS NOT CHECKED. ***
        // The harness assumed the spec's signal name WAS the block's tag name; measured, that assumption
        // held for exactly one signal in seventeen, and the one it held for was the only name collision.
        // A tag entered into the map under its own name would be matched by accident wherever the two
        // happen to coincide and missed silently everywhere else - which is the assumption, re-introduced
        // as a default. So an unjoined tag is named here instead.
        if (map.TagsWithNoSpecName.Count > 0)
        {
            return GateResult.CouldNotRun("5 observability", NotCheckedReason.AwaitingAnArtifactThatCouldExist, "a specName on every bound signal",
                $"{map.TagsWithNoSpecName.Count} bound signal(s) state no specification name, so nothing could join what a vector CITES to what "
                + $"the copy layer CARRIES: {string.Join(", ", map.TagsWithNoSpecName)}. "
                + "*** ABSENT DOES NOT MEAN 'THE SAME AS THE TAG'. *** That silent identity is the assumption this field exists to remove, and "
                + "re-introducing it as a default would remove nothing - it matches by accident where the names coincide and misses everywhere "
                + "else. If a signal's two names genuinely are the same, STATE that they are.");
        }

        var problems = new List<string>();
        var resolved = Resolved(vectors, enumerations);

        foreach (var (v, enumeration) in resolved)
        {
            // A vector whose declared comp is not a comp is refused HERE as well as at the schema gate,
            // and the check is then run at 1 so the rest of the vector is still evaluated. Skipping it
            // would report the observability gate as passed on a vector it never looked at.
            var declared = v.CompressionFactor;
            if (declared < 1)
            {
                problems.Add($"{v.Id}: CompressionFactor is {declared}, so the window arithmetic has no basis. Scan counts are meaningless without the comp they were stated at; the check below was run at comp=1 to evaluate the rest.");
                declared = 1;
            }

            // *** THE FORM COMES FROM THE ENUMERATION WHERE THE ENUMERATION HAS ONE. *** F-3's refusal
            // keys on the assertion's form, so evaluating it against the VECTOR's declaration would
            // enforce the ruling against what an author claimed. The mismatch itself is refused by the
            // form-authority gate; this makes the observability verdict right even so.
            //
            // *** AND IT IS THE FORM FROM THE SUBJECT THIS VECTOR CITES INTO. *** Reading it from a merge
            // would let one subject's form decide another subject's observability verdict wherever the two
            // share a clause — F-3 enforced against the wrong file.
            var form = (v.Basis is not null && enumeration is not null ? enumeration.FormOf(v.Basis.AssertionId) : null) ?? v.Form;
            var report = ObservabilityCheck.Evaluate(v.Expectations, form, map, floorScans, declared, runtimeCompression);
            problems.AddRange(report.Refusals.Select(r => $"{v.Id}/{r.Signal}: {r.Outcome} — {r.Detail}"));
        }

        // Every vector IS evaluated above — an unresolved citation falls back to the vector's own declared
        // form, which is weaker, so the fact is reported rather than left to be inferred from gate 3j.
        var unresolvedObs = UnresolvedNote(resolved);
        if (unresolvedObs is not null)
        {
            problems.Add(unresolvedObs.Replace("were NOT examined by this gate because", "were evaluated against THEIR OWN declared form rather than the enumeration's, because", StringComparison.Ordinal)
                + " F-3 is then enforced against what the vector claims, which is the permissive path this gate exists to close.");
        }

        return new GateResult("5 observability", GateStatus.Checked, problems.Count == 0, nameof(ObservabilityCheck),
            (problems.Count == 0
                ? $"every expectation is supportable by the map, against a floor of {floorScans:0.0} scan(s) at comp={runtimeCompression}."
                : string.Join(" | ", problems))

            // 🔴 *** THE ADMISSION IS ATTACHED TO THE CLAIM, NOT TO THE OUTCOME, AND THAT IS A DEFECT FIX.
            // *** It used to render only when problems.Count == 0 - so on a report that refused for ANY
            // other reason, latch claims were admitted on an unverified caller-supplied block name and
            // NOTHING SAID SO. Measured: four such claims went through exactly that way.
            //
            // The whole justification for latchedBy being a BLOCK NAME rather than `latched: true` is that
            // the name is checkable and the report says it was taken on trust. AN ADMISSION THAT APPEARS
            // ONLY WHEN EVERYTHING PASSED IS MISSING FROM EVERY REPORT ANYONE READS CLOSELY - a refusing
            // report is precisely the one that gets read.
            + LatchAdmission(map));
    }

    // -------------------------------------------------------------------------------------------------
    // 7 — start bool
    // -------------------------------------------------------------------------------------------------

    private static GateResult StartBool(IReadOnlyList<SubmissionVector> vectors)
    {
        var problems = new List<string>();

        foreach (var group in vectors.GroupBy(v => v.Slot, StringComparer.Ordinal))
        {
            var names = group.Select(v => v.StartBool).Distinct(StringComparer.Ordinal).ToArray();
            if (names.Length > 1)
                problems.Add($"slot '{group.Key}' names {names.Length} different start bools ({string.Join(", ", names)}). Exactly one per slot: the commit raises one bit per slot.");
        }

        foreach (var v in vectors.Where(v => AbsoluteAddress.IsMatch(v.StartBool?.Trim() ?? string.Empty)))
        {
            problems.Add($"{v.Id}: StartBool '{v.StartBool}' is a bit POSITION, not a name. Bind by NAME — the bit order within the start-bool register is [I], not [M], and the simulator and BitAddressOf agree FROM THE SAME PREMISE, so their agreement is worth nothing.");
        }

        return new GateResult("7 start bool (submission half)", GateStatus.Checked, problems.Count == 0, nameof(SubmissionGate),
            problems.Count == 0
                ? "exactly one start bool per slot, every one bound by name. The LATER-SCAN rule is enforced at run time by InertPhase against the observed counter, and is not checkable here."
                : string.Join(" | ", problems));
    }

    // -------------------------------------------------------------------------------------------------
    // 8 — blacklist
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The reason gates 8/8c cannot run, or null when the join is complete.
    ///
    /// <para><b>Three states, and the middle one is a CLAIM rather than a silence.</b> In <c>storage</c>
    /// it is resolvable. In <c>harnessOnly</c> no edge is possible, and <b>that is a computed fact</b>.
    /// In neither, NOT CHECKED. In both, refused. Without the third state a legitimately mirror-only
    /// signal is indistinguishable from a typo for ever — which is precisely what the converter's own
    /// unresolved reason says: <i>"this may be a mirror-only signal, or the name may be wrong"</i>, two
    /// entirely different repairs behind one silence.</para>
    /// </summary>
    private static string? StorageJoinIncomplete(IReadOnlyList<SubmissionVector> vectors, SignalStorageMap? storage)
    {
        if (storage?.Ambiguities.Count > 0)
        {
            return "the declared join is AMBIGUOUS and is refused rather than resolved to one candidate: "
                 + string.Join(" | ", storage.Ambiguities.Select(a => $"'{a.Signal}' -> " + string.Join(", ", a.Candidates.Select(c => c.ToString()))))
                 + ". Instance aliases of one storage are collapsed first, so this means GENUINELY DIFFERENT STORAGE - and picking a "
                 + "candidate is the aliasing that manufactured fictional multi-writers, not the fix.";
        }

        if (storage is null || storage.IsEmpty)
        {
            return "no `map.storage` / `map.harnessOnly` was declared (contract 2.7), so no submission signal could be joined to a "
                 + "controller location. `map.providedFor` says HOW a signal is watched and never WHERE it is. *** MEASURED ON A LIVE "
                 + "SUBMISSION: 1 OF 17 SIGNALS RESOLVED, AND THAT ONE ONLY BECAUSE ITS SPEC NAME AND BLOCK TAG HAPPEN TO BE THE SAME "
                 + "STRING. *** Declare each signal's storage, or declare it `harnessOnly` - which is a POSITIVE claim that it occupies "
                 + "no PLC storage, not an omission.";
        }

        var signals = vectors.SelectMany(v => v.Expectations).Select(e => e.Signal).Distinct(StringComparer.Ordinal).ToArray();

        var contradictions = signals.Where(sig => storage.Resolve(sig) == StorageJoin.Contradiction).ToArray();
        if (contradictions.Length > 0)
        {
            return $"{contradictions.Length} signal(s) are declared in BOTH `storage` and `harnessOnly`: {string.Join(", ", contradictions)}. "
                 + "A signal cannot both occupy storage and occupy none, and choosing which declaration to believe would be the gate "
                 + "deciding what the author meant.";
        }

        var unstated = signals.Where(sig => storage.Resolve(sig) == StorageJoin.NotStated).ToArray();
        if (unstated.Length > 0)
        {
            return $"{unstated.Length} of {signals.Length} signal(s) are in NEITHER `storage` nor `harnessOnly`: {string.Join(", ", unstated)}. "
                 + "*** NOBODY STATED THE JOIN, AND THAT IS NOT THE SAME AS 'IT OCCUPIES NO STORAGE'. *** If these are mirror-side logical "
                 + "names, say so with `harnessOnly` - that turns a NOT CHECKED into a fact.";
        }

        return null;
    }

    /// <summary>
    /// 2.7 as its own gate, so the join's state is REPORTED rather than only felt as two NOT CHECKEDs.
    /// </summary>
    private static GateResult SignalStorageJoin(IReadOnlyList<SubmissionVector> vectors, SignalStorageMap? storage)
    {
        const string name = "8s signal storage join (2.7)";

        if (StorageJoinIncomplete(vectors, storage) is { } gap)
        {
            var refusal = storage is not null && (storage.Ambiguities.Count > 0 || vectors.SelectMany(v => v.Expectations)
                .Any(e => storage.Resolve(e.Signal) == StorageJoin.Contradiction));

            // An ambiguity or a contradiction was COMPARED and found wrong; an absent join was not
            // compared at all. Two different statuses, because they call for different repairs.
            return refusal
                ? new GateResult(name, GateStatus.Checked, false, "map.storage / map.harnessOnly", gap)
                : GateResult.CouldNotRun(name, NotCheckedReason.AwaitingAnArtifactThatCouldExist,
                    "map.storage / map.harnessOnly", gap);
        }

        var signals = vectors.SelectMany(v => v.Expectations).Select(e => e.Signal).Distinct(StringComparer.Ordinal).ToArray();
        var resolvable = signals.Count(sig => storage!.Resolve(sig) == StorageJoin.InStorage);
        var mirrorOnly = signals.Length - resolvable;

        return new GateResult(name, GateStatus.Checked, true, nameof(SignalStorageMap),
            $"every one of {signals.Length} signal(s) is joined: {resolvable} to controller storage, {mirrorOnly} declared `harnessOnly`. "
            + "*** `harnessOnly` IS A POSITIVE CLAIM AND NOT AN OMISSION: *** those signals occupy no PLC storage, so no conflict edge is "
            + "possible for them and that is a COMPUTED FACT rather than a silence. Nothing here matched a signal by the shape of its name - "
            + "the declared join REPLACES suffix matching, which is what manufactured two of the four cross-block multi-writer findings this "
            + "project has ever recorded.");
    }

    private static GateResult Blacklist(
        IReadOnlyList<SubmissionVector> vectors, ConflictGraph? conflicts, SignalStorageMap? storage, bool edgesExplicitlyNull)
    {
        // *** null IS NOT THE SAME AS OMITTED, AND THIS IS WHERE THAT BITES. *** `[]` is the EARNED
        // positive claim that the graph ran over a whole corpus and found nothing; OMITTING the key is the
        // weaker and TRUE statement that it did not run. An explicit null is neither - and a lenient
        // deserializer turns it back into an empty collection one layer down, restoring the false claim
        // AFTER the refusal was correctly made. So it is refused by name rather than normalised.
        if (edgesExplicitlyNull)
        {
            return new GateResult("8 blacklist", GateStatus.Checked, false, nameof(SubmissionGate),
                "`conflictEdges` is present and NULL. *** THAT IS NEITHER OF THE TWO THINGS IT COULD MEAN. *** `[]` is the earned claim "
                + "that the graph RAN over a whole corpus and found nothing; omitting the key is the honest 'it did not run'. A null is a "
                + "third thing that a lenient deserializer will quietly turn into an empty list one layer down, restoring the very claim "
                + "the omission was refusing to make. Omit the key, or carry edges.");
        }

        // 2.7. Gate 8's packing set derives from the edges, and an edge is a statement about STORAGE.
        if (StorageJoinIncomplete(vectors, storage) is { } gap)
        {
            return GateResult.CouldNotRun("8 blacklist", NotCheckedReason.AwaitingAnArtifactThatCouldExist, "map.storage / map.harnessOnly (2.7)",
                "the blacklist could not be checked against a computed graph, because the graph is a statement about STORAGE and " + gap);
        }

        var computedConflicts = conflicts?.BlocksForPacking;

        if (computedConflicts is null)
        {
            return GateResult.CouldNotRun("8 blacklist", NotCheckedReason.AwaitingAnArtifactThatCouldExist, "cross-check conflict graph",
                "no computed disjointness graph was supplied, so the add-only property was compared against nothing. A blacklist checked against an absent graph is a blacklist nobody checked, and reporting it as passed is exactly the failure this gate exists to prevent.");
        }

        var problems = new List<string>();

        foreach (var v in vectors)
        {
            foreach (var entry in v.Blacklist)
            {
                if (string.IsNullOrWhiteSpace(entry.Block))
                    problems.Add($"{v.Id}: a blacklist entry names no block.");

                if (string.IsNullOrWhiteSpace(entry.Reason))
                    problems.Add($"{v.Id}: blacklist entry '{entry.Block}' carries no reason. The failure mode here is defensive over-blacklisting, concurrency collapsing toward serial, and nobody noticing BECAUSE IT STILL WORKS — a recorded reason is what makes that visible.");
            }
        }

        // ADD-ONLY IS A PROPERTY OF THE TYPE, NOT OF THIS CHECK: BlacklistEntry carries no negation, no
        // "allow" and no override, so an agent cannot express a removal. What is verified here is that
        // the effective set is a SUPERSET of the computed one — which it is by construction, and is
        // asserted rather than assumed.
        var declared = vectors.SelectMany(v => v.Blacklist.Select(b => b.Block)).ToHashSet(StringComparer.Ordinal);
        var effective = new HashSet<string>(computedConflicts, StringComparer.Ordinal);
        effective.UnionWith(declared);

        if (!computedConflicts.IsSubsetOf(effective))
            problems.Add("the effective exclusion set does not contain every computed conflict. That should be impossible — the blacklist can only add — so this is a defect in the gate, not in the submission.");

        return new GateResult("8 blacklist", GateStatus.Checked, problems.Count == 0, nameof(SubmissionGate),
            problems.Count == 0
                ? $"{declared.Count} declared exclusion(s) on top of {computedConflicts.Count} computed conflict(s); every entry carries a reason, and the type carries no way to remove one. NOTE: the blacklist names BLOCKS while admission colours SLOTS, so naming a block excludes every slot testing it."
                : string.Join(" | ", problems));
    }

    // -------------------------------------------------------------------------------------------------
    // 8c — X-G: the packer can mask a genuine multi-writer defect
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>X-G, and the thing worth getting right is what an EMPTY report means.</b>
    ///
    /// <para>Two blocks that both write the same coil are a conflict, so DB-13 puts them in different
    /// tensors and both tests pass — <b>the scheduler has silently repaired a defect that will ship.</b>
    /// The fact is already computed by <c>converter cross-check</c> (C-308) and this design consumed it
    /// only as a graph edge. Reporting it is the fix X-G asks for.</para>
    ///
    /// <para><b>It reports on clean graphs too</b>, for the same reason F-6's collapse report prints its
    /// no-collapse line: a report that appears only on bad news teaches its reader that absence means
    /// "not run". And <b>a graph with unrecorded provenance is NOT CHECKED, never a clean bill</b> — that
    /// is the one state in which "0 multi-writer findings" would be true of the report and say nothing
    /// about the program.</para>
    ///
    /// <para><b>A finding does not refuse the submission, and that is a decision.</b> The defect is in the
    /// DELIVERABLE — two blocks writing one signal in one scan cycle — not in the vectors, and X-G's own
    /// treatment says "reported as FINDINGS as well as being used for packing". Refusing here would make a
    /// vector author responsible for a program defect they cannot fix. <b>Whether it should instead be a
    /// hard refusal is an owner question</b>, and it is recorded rather than decided in the code.</para>
    /// </summary>
    private static GateResult MultiWriterProvenance(ConflictGraph? conflicts, SignalStorageMap? storage)
    {
        // A multi-writer is two blocks writing THE SAME STORAGE, so with no join there is nothing the
        // question could even be asked about.
        if (storage is null || storage.IsEmpty)
        {
            return GateResult.CouldNotRun("8c multi-writer provenance (X-G)", NotCheckedReason.AwaitingAnArtifactThatCouldExist, "map.storage / map.harnessOnly (2.7)",
                "no multi-writer fact could be reported: a multi-writer is two blocks writing the same STORAGE, and no `map.storage` / "
                + "`map.harnessOnly` join was declared (2.7). Measured on a live submission: 1 of 17 signals resolved.");
        }

        if (conflicts is null)
        {
            return GateResult.CouldNotRun("8c multi-writer provenance (X-G)", NotCheckedReason.AwaitingAnArtifactThatCouldExist, "cross-check conflict graph with provenance",
                "no conflict graph was supplied, so no multi-writer fact could be reported. An absent graph and a graph with no multi-writers produce the same empty report, which is why this is NOT CHECKED rather than a pass.");
        }

        if (!conflicts.ProvenanceComplete)
        {
            return GateResult.CouldNotRun("8c multi-writer provenance (X-G)", NotCheckedReason.AwaitingAnArtifactThatCouldExist, "provenance on every conflict edge",
                conflicts.Render()
                + " Edges without provenance are the state this gate exists for: the packer will still separate the blocks, both tests will still pass, and nothing will have said that a multi-writer on a deliverable signal is what is being separated.");
        }

        return new GateResult("8c multi-writer provenance (X-G)", GateStatus.Checked, true, nameof(ConflictGraph),
            conflicts.Render()
            + (conflicts.MultiWriterFindings.Count > 0
                ? " REPORTED, NOT REFUSED: the defect is in the deliverable rather than in this submission, and X-G's treatment is to report. Whether it should refuse is an open owner question."
                : string.Empty));
    }

    // -------------------------------------------------------------------------------------------------
    // 10 — X-D: time compression, and the half of it a submission can answer
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>X-D's assertion ceiling, per vector — and it catches a case gate 5 structurally cannot.</b>
    ///
    /// <para>Gate 5 exempts LATCHED and STAMPED expectations from the observability floor, correctly: a
    /// latch holds until cleared at test start and cannot fall in a poll gap. <b>X-D does not exempt them
    /// from the scan-period ceiling.</b> An event compressed below one scan of real time does not happen
    /// long enough to be latched either — <c>comp_max = T_event / scan_period</c> — so a latched
    /// declaration that clears gate 5 at every compression can still be void at the one being run.</para>
    ///
    /// <para><b>For SAMPLED expectations this is deliberately the same inequality as gate 5</b>, computed
    /// from the other side: <c>window.At(comp) &gt;= floor</c> is <c>comp &lt;= window.PlantScans /
    /// floor</c>. They cannot disagree because they are one division in <c>ScanBudget</c>, and a test
    /// sweeps the range asserting the boundary is the same number. What this adds is the CEILING as a
    /// number the author can act on, rather than only the verdict that one particular factor failed.</para>
    /// </summary>
    private static GateResult CompressionCeiling(IReadOnlyList<SubmissionVector> vectors, double floorScans, int runtimeCompression)
    {
        var problems = new List<string>();
        var ceilings = new List<string>();

        foreach (var v in vectors)
        {
            var declared = Math.Max(1, v.CompressionFactor);
            var perVector = new List<(string Signal, double Ceiling)>();

            foreach (var e in v.Expectations.Where(e => e.WindowScans >= 1))
            {
                var window = new Harness.Wire.ScanBudget(e.WindowScans, declared);
                perVector.Add((e.Signal, e.Mode == InstrumentationMode.Sampled
                    ? TimeCompression.SampledCeiling(window, floorScans)
                    : TimeCompression.LatchedCeiling(window)));
            }

            if (perVector.Count == 0)
            {
                ceilings.Add($"{v.Id}: no window declared on any expectation, so no assertion ceiling could be computed for it");
                continue;
            }

            var binding = perVector.MinBy(c => c.Ceiling);
            ceilings.Add($"{v.Id}: comp_max(assertion) = {binding.Ceiling:0.##}x, bound by '{binding.Signal}'");

            if (runtimeCompression > binding.Ceiling)
            {
                problems.Add($"{v.Id}: this wave runs at comp={runtimeCompression} and the vector's own assertion ceiling is {binding.Ceiling:0.##}x, bound by '{binding.Signal}'. "
                    + "X-D: compression shortens the REAL-TIME separation of the events being observed, so past this factor the assertion is not merely hard to catch — it is never sampled, and every check still reports green. USE comp_min, NOT comp_max.");
            }
        }

        return new GateResult("10a time compression — assertion ceiling (X-D)", GateStatus.Checked, problems.Count == 0, nameof(TimeCompression),
            problems.Count == 0
                ? $"this wave runs at comp={runtimeCompression}, under every vector's assertion ceiling. {string.Join("; ", ceilings)}. NOTE: the LATCHED ceiling is checked here and NOT by gate 5 — latching is exempt from the observability floor, never from the scan-period term."
                : string.Join(" | ", problems));
    }

    /// <summary>
    /// <b>X-D's other three ceilings, which a submission does not carry — and the treatment depends on
    /// whether anything is actually being compressed.</b>
    ///
    /// <para>The timer bound (<c>PT / floor</c>, which X-D says <i>often binds first</i>), the model's
    /// declared <c>comp_stable</c>, and the ratio-distortion bound on unscaled literals are properties of
    /// the BLOCK and the MODEL, not of the vectors. Contract §2 gives an author nowhere to state them.</para>
    ///
    /// <para><b>So: at comp = 1 this is a real pass</b> — nothing is scaled, and none of the three can bind.
    /// That is computed from the submission, not assumed. <b>Above comp = 1 it is NOT CHECKED and fails
    /// closed</b>, because the plan is then compressing a block whose shortest preset nobody stated. Under
    /// the RULED ABSOLUTE 500 ms timer floor (2026-08-18) a 2-second preset caps at 4.0x, not the 10x X-D
    /// originally assumed — and a 500 ms preset caps at exactly 1.0x, i.e. cannot be compressed at all. So
    /// this is exactly the range where a submission would otherwise sail through.</para>
    /// </summary>
    private static GateResult CompressionBoundsNotInTheSubmission(
        IReadOnlyList<SubmissionVector> vectors, int runtimeCompression, double floorScans, BlockCompressionInputs? inputs)
    {
        var declaredFactors = vectors.Select(v => Math.Max(1, v.CompressionFactor)).ToArray();

        // *** IT KEYS ON THE RUNTIME FACTOR ALONE, AND THAT IS A DECISION. *** A vector's DECLARED comp is
        // the unit its scan counts are stated in; it says nothing about how fast the model will be driven.
        // Only the RUNTIME factor scales presets, distorts the ratio of an unscaled literal, and asks a
        // model to behave at a rate. A vector declaring comp=10 while the wave runs at 1 is running a model
        // in real time and none of these three ceilings can bind on it.
        if (runtimeCompression <= 1)
        {
            return new GateResult("10b time compression — timer / model / ratio ceilings (X-D)", GateStatus.Checked, true, nameof(TimeCompression),
                $"nothing is compressed at run time — this wave runs at comp={runtimeCompression}, whatever the vectors' declared factor(s) of {string.Join(", ", declaredFactors.Distinct().OrderBy(f => f))} — so X-D's timer, model-stability and ratio-distortion ceilings cannot bind. "
                + $"That is computed from the submission, not assumed: at comp=1 a DATA preset is unscaled, an unscaled LITERAL keeps its proportion, and the model is not being asked to run at a factor. "
                + $"For reference, the timer floor is the RULED ABSOLUTE {TimeCompression.AbsoluteTimerFloorMs:0.#} ms (2026-08-18), which subsumes the scan-derived k x scan = {TimeCompression.TimerScanMultiple} x {Harness.Wire.WireTiming.ScanPeriodMs:0.###} = {TimeCompression.TimerFloorMs:0.#} ms — so a 2-second preset would cap compression at {2000.0 / TimeCompression.EffectiveTimerFloorMs:0.0}x, not the 10x X-D originally assumed.");
        }

        if (inputs is null)
        {
            return GateResult.CouldNotRun("10b time compression — timer / model / ratio ceilings (X-D)", NotCheckedReason.AwaitingAnArtifactThatCouldExist, "TimeCompression.Plan, via BlockCompressionInputs",
                $"this wave runs at comp={runtimeCompression} with declared factor(s) {string.Join(", ", declaredFactors.Distinct().OrderBy(f => f))}, so compression IS being applied — and three of X-D's four ceilings were compared against nothing. "
                + $"No timer presets were supplied (the term X-D says OFTEN BINDS FIRST: PT / floor, where the floor is the RULED ABSOLUTE {TimeCompression.EffectiveTimerFloorMs:0.#} ms, so a 2-second preset caps at {2000.0 / TimeCompression.EffectiveTimerFloorMs:0.0}x) and no model comp_stable. "
                + "*** A negligible-fraction threshold is NO LONGER ONE OF THE INPUTS (superseded 2026-08-18): *** the ratio-distortion bound is now the ruled "
                + $"{TimeCompression.LiteralHeadroomMultiple:0.#}x literal-headroom companion, so an unscaled literal needs no declared fraction. "
                + "Supply the presets as `blockCompression` (with `model.compStable`). An unknown ceiling is not a high one.");
        }

        // *** AN INCOMPLETE OBJECT IS NOT CHECKED, NAMING THE FIELD — never a throw. *** PlantMs and
        // BudgetMs used to be non-nullable, so an omitted pair arrived as 0, Plan threw, and the CLI
        // reported the whole DOCUMENT unreadable (NOTHING EXAMINED). Both fail closed; only the diagnosis
        // was wrong, and it sent the reader looking at the wrong thing.
        if (inputs.Missing.Count > 0)
        {
            return GateResult.CouldNotRun("10b time compression — timer / model / ratio ceilings (X-D)", NotCheckedReason.AwaitingAnArtifactThatCouldExist, "TimeCompression.Plan, via BlockCompressionInputs",
                $"this wave runs at comp={runtimeCompression}, so compression IS being applied, and the block compression inputs are incomplete: "
                + string.Join(" | ", inputs.Missing)
                + ". The rest of the submission is fine; these fields are what is missing.");
        }

        var plan = TimeCompression.Plan(
            new CompressionRequest(inputs.PlantMs!.Value, inputs.BudgetMs!.Value,
                vectors.SelectMany(v => v.Expectations).ToArray(),
                declaredFactors.Max(),

                // 🔴 *** THIS RTT_p99 IS NOT A BOUND, AND IT IS CORRECT ONLY BY CANCELLATION. ***
                //
                // F-5's rule keys the constant on the QUANTITY'S KIND — durations take the p90, bounds
                // take the p99 — and this is NEITHER. It is the algebraic INVERSE of
                // `WireTiming.ObservabilityFloorScans`, which is `slots * RttP99Ms / ScanPeriodMs`;
                // multiplying back by ScanPeriodMs and dividing by RttP99Ms recovers `slots`, the
                // reads-per-cycle the caller already had. The two constants cancel.
                //
                // *** THE RULE THAT DOES REACH IT IS NOT WRITTEN ANYWHERE ELSE, SO IT IS WRITTEN HERE:
                // USE THE SAME CONSTANT THE FLOOR WAS COMPUTED WITH. *** Not the p99 because a ceiling is
                // bound-shaped — that reasoning is about the FLOOR, not about this inversion. If
                // ObservabilityFloorScans is ever re-keyed, this must move WITH it; and "correcting" it
                // to the duration-shaped RttTypicalMs would break the cancellation and silently return a
                // reads-per-cycle roughly 2.6x too large, which widens every assertion ceiling derived
                // from it.
                //
                // The honest fix is for the floor to expose its own inverse so there is nothing to keep
                // in step. Recorded rather than done here: that is a Harness.Wire change, and this site
                // is the one that would go wrong meanwhile.
                Math.Max(1, (int)Math.Round(floorScans * Harness.Wire.WireTiming.ScanPeriodMs / Harness.Wire.WireTiming.RttP99Ms)),
                inputs.Presets, inputs.ModelCompStable, inputs.NegligibleFraction,
                // *** THE RUNTIME FACTOR REACHES THE ARITHMETIC. *** Without it, Plan's own branches keyed
                // on comp_min and a wave at comp=8 with comp_min=1 was told nothing was being scaled.
                runtimeCompression),
            floorScans);

        if (!plan.Runnable)
        {
            return new GateResult("10b time compression — timer / model / ratio ceilings (X-D)", GateStatus.Checked, false, nameof(TimeCompression),
                plan.Render());
        }

        if (runtimeCompression > plan.CompMax)
        {
            return new GateResult("10b time compression — timer / model / ratio ceilings (X-D)", GateStatus.Checked, false, nameof(TimeCompression),
                $"this wave runs at comp={runtimeCompression} and comp_max is {plan.CompMax:0.##}x, bound by {plan.BindingBound}. " + plan.Render());
        }

        return new GateResult("10b time compression — timer / model / ratio ceilings (X-D)", GateStatus.Checked, true, nameof(TimeCompression),
            plan.Render()
            + (runtimeCompression > plan.CompMin
                ? $" *** THIS WAVE RUNS AT comp={runtimeCompression}, ABOVE comp_min. *** It clears the ceiling, and X-D's rule is to take the LEAST compression that meets the budget and bank the remainder as margin — compression is a fidelity risk, and running above comp_min spends that margin for nothing."
                : string.Empty));
    }

    // -------------------------------------------------------------------------------------------------
    // 11 — memory layout (contract §4.5). THE INVARIANT WAS TRUE AND ENFORCED BY NOTHING.
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// *** EVERY <c>(area, dbNumber)</c> A TAG MAP CAN REACH MUST BE A DECLARED HARNESS OBJECT. ***
    ///
    /// <para><b>The invariant was measured TRUE and enforced by nothing.</b> <c>S7Transport</c> reaches
    /// any DB through a HAND-WRITTEN JSON tag map, and the write fence cannot catch it because the fence
    /// is scoped on an AREA NAME drawn from that same map — so it verifies that the caller's claimed area
    /// matches the tag's, never what KIND of object the area is. Nothing read a deliverable block's data
    /// only because no tag map in the repository pointed at one.</para>
    ///
    /// <para>So the enforceable point is the TAG MAP, and the check is a set-difference against an
    /// artifact the vector author did not write for this purpose.</para>
    ///
    /// <para><b>Four ways to fail, and they are four different facts:</b> a reachable pair nobody
    /// declared; a row naming a DELIVERABLE block (the invariant broken); a layout that is
    /// <c>Optimized</c> or unstated (the object is ABSENT on the wire and every read fails at the first
    /// DATA transfer, which presents as wiring); and a <c>layoutSetAfterImport</c> that is missing or
    /// names an OLDER import — the layout reverts at EVERY import, so a stale stamp says it was
    /// re-asserted and has reverted since.</para>
    /// </summary>
    private static GateResult MemoryLayout(DeploymentDeclaration? deployment, TagMapReach? reach)
    {
        const string name = "11 memory layout (§4.5)";

        if (deployment is null)
        {
            return GateResult.CouldNotRun(name, NotCheckedReason.RequiresTheDevice, "deployment declaration + the tag map's reach",
                "no `deployment` was declared, so nothing was compared. It is a property of the DOWNLOAD and resubmitting the vector cannot supply it — "
                + "and ABSENT IS NOT `s7Objects: []`, which is the positive claim that no classic-S7comm path reaches a data block.");
        }

        var rows = deployment.S7Objects ?? Array.Empty<S7ObjectDeclaration>();

        // *** "THERE IS NO S7 TRANSPORT" IS NOW SAYABLE, AND IT IS A DIFFERENT CLAIM FROM "THE MAP REACHES
        // NO DB". *** Without it, a Modbus-only deployment had to invent a tag map it does not use or sit
        // permanently NOT CHECKED — and a gate nobody can satisfy stops being read.
        if (deployment.NoS7Transport)
        {
            if (rows.Count > 0)
            {
                return new GateResult(name, GateStatus.Checked, false, nameof(DeploymentDeclaration),
                    $"`noS7Transport` is declared AND {rows.Count} s7Object(s) are enumerated. Those contradict: a deployment cannot both "
                    + "have no classic-S7comm path and list the objects one reaches. One of the two is stale, and guessing which would be "
                    + "the gate deciding what the declaration meant.");
            }

            return new GateResult(name, GateStatus.Checked, true, nameof(DeploymentDeclaration),
                "`noS7Transport` is declared: this deployment carries NO classic-S7comm client at all, so there is no reachable set to "
                + "difference against a deliverable and §4.5's optimized-block hazard cannot arise. *** THIS IS A POSITIVE CLAIM, NOT A "
                + "SKIP: *** it is refused if any s7Object is also declared, and it says something different from `s7Objects: []`, which "
                + "asserts that a classic-S7comm path exists and reaches no data block.");
        }

        var problems = new List<string>();

        // The stamp dates every layout claim, so a declaration with rows and no stamp cannot be checked
        // at all — the comparison that distinguishes "nobody re-asserted it" from "re-asserted after the
        // wrong import" has nothing to compare against.
        if (rows.Count > 0 && string.IsNullOrWhiteSpace(deployment.ImportStamp))
        {
            return GateResult.CouldNotRun(name, NotCheckedReason.RequiresTheDevice, "deployment.importStamp",
                $"{rows.Count} s7Object(s) are declared and no `importStamp` is. Every layout claim is DATED against it, so without one a `layoutSetAfterImport` cannot be told from a stale one — "
                + "and a stale one means the layout was re-asserted against a previous import and has reverted since.");
        }

        foreach (var row in rows)
        {
            var label = $"{row.Area} (DB{row.DbNumber})";

            if (string.IsNullOrWhiteSpace(row.HarnessObject))
            {
                problems.Add($"{label}: names no harness object. §4.5's invariant is that every reachable object IS one, so a blank here is the claim not being made rather than being made about nothing.");
            }
            else if (reach is not null && reach.DeliverableObjects.Contains(row.HarnessObject))
            {
                problems.Add($"{label}: names '{row.HarnessObject}', WHICH SHIPS. *** THAT IS THE INVARIANT BROKEN, NOT A FINDING *** — the harness touches harness-generated objects only, and a classic-S7comm path into a deliverable block means what ships is not what was tested.");
            }

            switch (row.Layout)
            {
                case DeclaredLayout.Standard:
                case DeclaredLayout.NotApplicable:
                    break;
                case DeclaredLayout.Optimized:
                    problems.Add($"{label}: declares Optimized. Classic S7comm cannot see an optimized block AT ALL — it is not an error, the object is simply ABSENT, and the read fails at the first DATA transfer rather than at connect, which presents as a wiring problem.");
                    break;
                default:
                    problems.Add($"{label}: declares no layout. Absence means \"no opinion\", never a default, and TIA RESOLVES NO OPINION TO OPTIMIZED. Undetermined is not clean.");
                    break;
            }

            // NotApplicable objects have no layout to re-assert — a %M region or a tag table — so a stamp
            // is neither required nor meaningful there.
            if (row.Layout == DeclaredLayout.NotApplicable)
                continue;

            if (string.IsNullOrWhiteSpace(row.LayoutSetAfterImport))
            {
                problems.Add($"{label}: carries no `layoutSetAfterImport`. NOBODY RE-ASSERTED THE LAYOUT — and the layout reverts at EVERY import, silently, with `drift-check` structurally blind to it in both directions.");
            }
            else if (!string.Equals(row.LayoutSetAfterImport, deployment.ImportStamp, StringComparison.Ordinal))
            {
                problems.Add($"{label}: was re-asserted after import '{row.LayoutSetAfterImport}' and the current import is '{deployment.ImportStamp}'. RE-ASSERTED AFTER THE WRONG IMPORT — a different fact from nobody having done it, and the reason this field is a stamp rather than a boolean. It has reverted since.");
            }
        }

        if (reach?.Reachable is null)
        {
            var declared = rows.Count == 0
                ? "The declaration claims `s7Objects: []` — no classic-S7comm path reaches a data block — and that claim was NOT compared against a tag map."
                : $"The {rows.Count} declared row(s) were checked for layout and stamp, and NOT compared against what a tag map can actually reach.";

            return GateResult.CouldNotRun(name, NotCheckedReason.AwaitingAnArtifactThatCouldExist, "the tag map's reachable (area, dbNumber) set",
                declared + " *** THE SET-DIFFERENCE IS THE POINT OF THIS GATE *** — S7Transport reaches any DB through a hand-written map, and the write fence is scoped on an area name from that same map, so it cannot see what KIND of object an area is. "
                + (problems.Count > 0 ? "Findings on what WAS checked: " + string.Join(" | ", problems) : string.Empty));
        }

        var declaredReach = rows.Select(r => r.Reach).ToHashSet();
        var undeclared = reach.Reachable.Where(r => !declaredReach.Contains(r)).ToArray();

        if (undeclared.Length > 0)
        {
            problems.Add($"the tag map can reach {string.Join(", ", undeclared.Select(r => r.ToString()).OrderBy(s => s, StringComparer.Ordinal))}, which `s7Objects` does not declare. "
                + "An undeclared reachable object has no layout claim, so nothing says it is Standard — and an optimized one is ABSENT on the wire rather than wrong on it.");
        }

        return new GateResult(name, GateStatus.Checked, problems.Count == 0, nameof(TagMapReach),
            problems.Count == 0
                ? (rows.Count == 0
                    ? $"the tag map reaches {reach.Reachable.Count} object(s) and `s7Objects` is empty — consistent only because the map reaches nothing addressable, which is the normal mirror-only state (the mirror is %MW bit memory, not a DB)."
                    : $"every one of the {reach.Reachable.Count} (area, dbNumber) pair(s) the tag map can reach is declared as a harness object, at Standard or NotApplicable, re-asserted after import '{deployment.ImportStamp}'.")
                : string.Join(" | ", problems));
    }

    // -------------------------------------------------------------------------------------------------
    // 9 — liveness, the half that IS separable to submission time
    // -------------------------------------------------------------------------------------------------

    private static GateResult LivenessPreconditions(IReadOnlyList<SubmissionVector> vectors, MirrorObservability map)
    {
        // The liveness CHECK is post-run and cannot be brought forward — nothing has happened yet. What
        // CAN be brought forward is whether liveness could be established at all, and a vector whose
        // liveness would be unanswerable is refused before a wave is spent on it rather than after.
        var problems = new List<string>();

        foreach (var v in vectors)
        {
            if (string.IsNullOrWhiteSpace(v.StartBool))
                problems.Add($"{v.Id}: no start bool, so neither 'was it commanded' nor 'did it run' could be answered after the wave. The result would be unreadable rather than failing.");

            if (map.IsEmpty)
                problems.Add($"{v.Id}: the map declares nothing, so no signal this vector reads could be shown to have been published.");
        }

        return new GateResult("9 liveness preconditions (submission half)", GateStatus.Checked, problems.Count == 0, nameof(SubmissionGate),
            problems.Count == 0
                ? "every vector could have its liveness established after the run: it has a start bool, and the map publishes something. The stimulus check itself is post-run (StimulusCheck) and is not a submission-time gate."
                : string.Join(" | ", problems));
    }
}
