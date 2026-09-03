using System.Text;
using Harness.Results;
using Harness.Wire;

namespace Harness.Gate;

/// <summary>Exit codes. <b>Read the verdict, not the exit code</b> — but a caller that only has the exit code must not be misled.</summary>
public static class GateExit
{
    /// <summary>Every mechanical gate ran and passed. Judgement gates remain and are named in the report.</summary>
    public const int AdmissibleSubjectToJudgement = 0;

    /// <summary>A gate ran and refused, or a gate could not be run at all.</summary>
    public const int NotAdmissible = 1;

    /// <summary>
    /// <b>NOTHING EXAMINED.</b> No vectors, an unreadable document, or a missing file. Deliberately its
    /// own code and never 0: an empty submission that exits 0 is the purest form of a gate that passed
    /// without looking at anything (FI-44).
    /// </summary>
    public const int NothingExamined = 2;
}

/// <summary>
/// 🔴 <b>Everything <see cref="SubmissionGate.Check"/> needs that is derived from the DOCUMENTS — in ONE
/// place, so the two callers cannot answer the same question differently.</b>
///
/// <para>*** THE LOOP'S GATE WAS NOT THE GATE, IN BOTH DIRECTIONS. *** <c>harness-gate</c> derived these
/// twelve values from the submission and the binding; <c>harness-run</c> re-derived four of them, passed
/// <c>null</c> for four more and passed a HARD-CODED empty set for the last two. Measured on the
/// deliverable set: the loop reported <b>gates 8, 8s, 8c, 10b and 11 as NOT CHECKED where the standalone
/// gate ran them</b>, and gate <b>0b passed unconditionally</b> because the loop asserted, on the
/// document's behalf, that no unknown field could arrive — true of a caller composing TYPED objects and
/// false of one PARSING A DOCUMENT, which is exactly what <c>LoopCli</c> does.</para>
///
/// <para><b>A submission could pass the loop's gate and fail the real one, and the loop is what spends
/// rig time.</b> Two independently-maintained derivations of one input set is how that happens, so there
/// is now one — <i>called rather than copied</i>, the same argument that made <c>LoopRun.Generate</c> the
/// code <c>LoopRun.Execute</c> runs rather than a second copy of it.</para>
///
/// <para><b>What is deliberately NOT here:</b> the observability map and the observability floor. Both
/// are legitimately different questions for the two callers — the loop derives its floor from the
/// ALLOCATED map's read plan, the CLI from the submission's declared wave-set width — and folding them in
/// would replace a real difference with a false agreement.</para>
/// </summary>
public sealed record GateInputs(
    IReadOnlyList<SubmissionVector> Vectors,

    /// <summary>
    /// 🔴 <b>PLURAL SINCE 2026-08-18 — the enumerations, not the enumeration.</b> A single
    /// <c>AssertionEnumeration</c> converts implicitly to a set of one, so the type change is invisible to
    /// every caller and every single-subject submission behaves exactly as it did. It is here rather than
    /// derived twice for the same reason everything else in this record is: two derivations of one input
    /// is how the loop's gate and the CLI's came to disagree about twelve of them.
    /// </summary>
    AssertionEnumerationSet Enumeration,
    FidelityDeclaration? Fidelity,
    AgentIdentity BlockAuthor,
    ConflictGraph? Conflicts,
    BlockCompressionInputs? CompressionInputs,
    DeploymentDeclaration? Deployment,
    TagMapReach? TagMapReach,
    SignalStorageMap? Storage,
    bool ConflictEdgesExplicitlyNull,
    IReadOnlyList<string> UnknownFields,
    IReadOnlyList<string> AnnotationFields,
    int RuntimeCompression,

    /// <summary>
    /// Gate 0c's input: the deriver's provenance block, plus which derivable fields the document
    /// carried. <b>It is in this record rather than derived per-caller for the same reason everything
    /// else here is</b> — two derivations of one input set is how the loop's gate and the CLI's came to
    /// disagree about twelve of them, and this one governs whether the other twelve were produced or
    /// typed.
    /// </summary>
    DerivationEvidence? Derivation = null,

    /// <summary>
    /// The document's <c>scenarioEndInput</c> — which input carries the scenario's end, in plant
    /// milliseconds. Gate 1b bounds every vector's <c>MaxDuration</c> against it; null leaves that gate
    /// NOT CHECKED.
    /// </summary>
    string? ScenarioEndInput = null,

    /// <summary>
    /// The document's <c>maxIndexScans</c> — gate 1b's flat ceiling, for vectors with no scenario clock.
    /// </summary>
    int? MaxIndexScans = null,

    /// <summary>
    /// The document's <c>scenarioTimeInputs</c> — which inputs are scenario coordinates that scale with
    /// the factor. Null above comp 1 leaves gate 10c NOT CHECKED.
    /// </summary>
    IReadOnlyList<string>? ScenarioTimeInputs = null);

/// <summary>
/// The runnable gate. <c>harness-gate check &lt;submission.json&gt;</c>.
///
/// <para><b>Split from <c>Program</c> so it is testable</b> — the CLI's own behaviour (what it refuses,
/// what it exits) is a decision like any other, and a decision only reachable through a process is a
/// decision nobody tests.</para>
/// </summary>
public static class GateCli
{
    public static int Run(IReadOnlyList<string> args, TextWriter output, Func<string, string> readFile, Func<string, byte[]>? readBytes = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(readFile);

        if (args.Count < 2 || !string.Equals(args[0], "check", StringComparison.Ordinal))
        {
            output.WriteLine("usage: harness-gate check <submission.json>");
            output.WriteLine();
            output.WriteLine("Runs the phase 5.1 admissibility gates from docs/notes/test-environment-contract.md");
            output.WriteLine("over a submission document. Read-only: no socket, no Portal, no file written.");
            output.WriteLine();
            output.WriteLine($"exit {GateExit.AdmissibleSubjectToJudgement} = ADMISSIBLE-SUBJECT-TO-JUDGEMENT   (there is deliberately no plain ADMISSIBLE)");
            output.WriteLine($"exit {GateExit.NotAdmissible} = NOT ADMISSIBLE                     (a gate refused, or a gate could not run)");
            output.WriteLine($"exit {GateExit.NothingExamined} = NOTHING EXAMINED                   (no vectors, or the document could not be read)");
            return GateExit.NothingExamined;
        }

        // 🔴 *** --binding IS WHAT MAKES THIS CLI AS STRONG AS THE LOOP. *** Without it the observability
        // map comes out of the submission — the vector author declaring what the copy layer provides — and
        // gate 5 reports NOT CHECKED rather than passing. Measured: the CLI took its map from the
        // submission while the loop took it from the coordinator's bindings, so the standalone tool was
        // WEAKER than the loop in exactly the place it decides whether to proceed, and it is consulted FIRST.
        var bindingIndex = args.ToList().IndexOf("--binding");
        var bindingPath = bindingIndex >= 0 && bindingIndex + 1 < args.Count ? args[bindingIndex + 1] : null;

        if (bindingIndex >= 0 && bindingPath is null)
        {
            output.WriteLine("NOTHING EXAMINED — `--binding` was given with no path after it.");
            output.WriteLine("It names the COORDINATOR's binding document, which is what gate 5 needs an authority from. A flag with no value is not one.");
            return GateExit.NothingExamined;
        }

        // *** THE BINDING IS READ IN ITS OWN try, NAMING ITS OWN FILE. *** Measured 2026-08-14: pointing
        // --binding at the real coordinator binding (which is MARKDOWN) produced
        // "could not read '<the submission>'" — the submission was fine, and the message sent the reader
        // to the wrong artifact entirely. One catch over two reads cannot say which one failed, and the
        // one it names is the one it did not.
        BindingDocument? binding = null;
        if (bindingPath is not null)
        {
            try
            {
                binding = BindingDocument.Read(readFile(bindingPath));
            }
            catch (Exception ex)
            {
                output.WriteLine($"NOTHING EXAMINED — could not read the BINDING '{bindingPath}': {ex.GetType().Name}: {ex.Message}");
                output.WriteLine("*** THE SUBMISSION IS NOT WHAT FAILED HERE. *** `--binding` takes a MACHINE-READABLE binding document (JSON).");
                output.WriteLine("A coordinator binding written as prose is a real document and still not a loadable one — gate 5 needs the slot bindings as data.");
                return GateExit.NothingExamined;
            }
        }

        SubmissionDocument document;
        SubmissionReport report;
        try
        {
            document = SubmissionDocument.Read(readFile(args[1]));

            // Evaluate is INSIDE the try: a document that parses as JSON and then names a mode nothing
            // implements is still a document that could not be read, and it must reach the same
            // NOTHING EXAMINED outcome rather than escaping as an unhandled throw.
            report = Evaluate(document, readFile, binding, readBytes);
        }
        catch (Exception ex)
        {
            output.WriteLine($"NOTHING EXAMINED — could not read the SUBMISSION '{args[1]}': {ex.GetType().Name}: {ex.Message}");
            output.WriteLine("An unreadable submission is not an admissible one. Empty is not clean.");
            return GateExit.NothingExamined;
        }

        Write(report, document, output);

        return report.Verdict switch
        {
            SubmissionVerdict.AdmissibleSubjectToJudgement => GateExit.AdmissibleSubjectToJudgement,
            SubmissionVerdict.NotAdmissible => GateExit.NotAdmissible,
            _ => GateExit.NothingExamined,
        };
    }

    /// <summary>Turn the document into the checked types and run every gate.</summary>
    /// <param name="binding">
    /// The coordinator's bindings. <b>Supplying them is what gives gate 5 an authority other than the
    /// vector author</b>; without them the map is self-declared and gate 5 refuses to be the deciding voice.
    /// </param>
    public static SubmissionReport Evaluate(SubmissionDocument document, Func<string, string>? readFile = null, BindingDocument? binding = null, Func<string, byte[]>? readBytes = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        // *** ONE DERIVATION, SHARED WITH THE LOOP. *** See GateInputs: everything below that is not the
        // observability map or the floor comes from here, so a value this CLI checks and a value the loop
        // checks are the same value rather than two derivations that agree today.
        var inputs = InputsOf(document, binding, readFile, readBytes);

        var map = new MirrorObservability(
            (document.Map?.ProvidedFor ?? new Dictionary<string, List<string>>())
            .ToDictionary(
                e => e.Key,
                e => (IReadOnlySet<InstrumentationMode>)e.Value
                    .Select(m => Enum.TryParse<InstrumentationMode>(m, ignoreCase: true, out var parsed)
                        ? parsed
                        : throw new InvalidDataException($"'{m}' is not an instrumentation mode. Expected one of: {string.Join(", ", Enum.GetNames<InstrumentationMode>())}."))
                    .ToHashSet(),
                StringComparer.Ordinal))
        {
            // *** THE SUBMISSION'S OWN MAP, AND IT IS LABELLED AS SUCH. *** Gate 5 then reports NOT
            // CHECKED rather than passing, because a map the vector author wrote cannot adjudicate the
            // vector author. `harness-run --binding` supplies the coordinator's, which is what the loop uses.
            Provenance = MapProvenance.SelfDeclared,
        };

        // The coordinator's bindings, derived exactly as LoopRun derives them — same source, same
        // authority. This is the "make the CLI no weaker" half; the branch above is the "refuse to be the
        // deciding voice" half, and both are needed because the CLI must remain usable without a binding.
        if (binding is not null)
        {
            // Keyed on the SPECIFICATION's names and with the modes DERIVED - see FromBindings. Keying on
            // the tag is the assumption that failed 16 times in 17 in one run.
            map = MirrorObservability.FromBindings(
                (binding.Slots ?? new List<SlotBindingDocument>())
                    .SelectMany(sl => sl.ResultSources ?? new List<MirroredSignalDocument>())
                    .Select(ToMirroredSignal),

                // 🔴 WHO wrote it, alongside WHERE it came from — gate 5c. Provenance fences out the
                // vector author structurally and is silent about the block's, so a binding declared by
                // the block's author would reach the gate above as fully trusted.
                new AgentIdentity(binding.DeclaredBy ?? string.Empty));
        }

        // *** THE FLOOR IS COMPUTED FROM SECTION 12a, NEVER CARRIED HERE. *** It scales with the number
        // of slots sharing the poll and with slots-per-read, so it is a property of the WAVE SET.
        var slotsPerRead = Math.Max(1, ModbusLimitsProxy.MaxReadRegisters / Math.Max(1, document.ResultRegistersPerSlot));
        var readsPerCycle = (int)Math.Ceiling(Math.Max(1, document.SlotsInWaveSet) / (double)slotsPerRead);
        var floor = WireTiming.ObservabilityFloorScans(readsPerCycle);

        return SubmissionGate.Check(
            inputs.Vectors,
            inputs.Enumeration,
            inputs.Fidelity,
            inputs.BlockAuthor,
            map,
            floor,
            inputs.RuntimeCompression,
            inputs.Conflicts,
            inputs.CompressionInputs,
            inputs.Deployment,
            inputs.TagMapReach,
            inputs.Storage,
            inputs.ConflictEdgesExplicitlyNull,
            inputs.UnknownFields,
            inputs.AnnotationFields,
            inputs.Derivation,
            inputs.ScenarioEndInput,
            inputs.MaxIndexScans,
            inputs.ScenarioTimeInputs);
    }

    /// <summary>
    /// 🔴 <b>Derive every document-sourced gate input, once.</b> See <see cref="GateInputs"/> for why this
    /// exists and for what is deliberately left out of it.
    /// </summary>
    /// <param name="readFile">
    /// Used only to read the tag map named by <c>tagMapPath</c>. <b>Absent means gate 11's set-difference
    /// could not be made</b> and the gate reports NOT CHECKED — never an empty reachable set, which is the
    /// opposite claim.
    /// </param>
    public static GateInputs InputsOf(SubmissionDocument document, BindingDocument? binding = null, Func<string, string>? readFile = null, Func<string, byte[]>? readBytes = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var extra = ExtraFields(document, binding);

        return new GateInputs(
            (document.Vectors ?? new List<VectorDocument>()).Select(ToSubmissionVector).ToArray(),
            ToEnumerationSet(document),
            ToFidelity(document),
            new AgentIdentity(document.BlockAuthor ?? string.Empty),
            ToConflicts(document),
            ToCompressionInputs(document),
            ToDeploymentDeclaration(document),
            ToTagMapReach(document, readFile),
            ToSignalStorage(document),
            document.ConflictEdgesExplicitlyNull,
            extra.Unknown,
            extra.Annotations,
            Math.Max(1, document.RuntimeCompression),
            ToDerivationEvidence(document, readFile, readBytes),
            document.ScenarioEndInput,
            document.MaxIndexScans,
            document.ScenarioTimeInputs);
    }

    /// <summary>
    /// Gate 0c's evidence: the provenance records the document carries, each re-hashed against the
    /// artifact it names, plus the set of derivable fields the document actually carried.
    ///
    /// <para><b>An unreadable artifact yields a null observed hash, not a missing record</b> — the gate
    /// then reports NOT CHECKED for that field. Dropping the record instead would make an unreadable
    /// artifact look like a field nobody derived, i.e. turn "could not look" into "hand-authored", which
    /// blames the submission for the harness's problem.</para>
    ///
    /// <para>🔴 <b>NEVER NULL FOR A DOCUMENT, INCLUDING ONE WITH NO <c>derivation</c> KEY.</b> Null is
    /// reserved for a caller with no document channel at all — a hand-built typed request — where NOT
    /// CHECKED is the honest answer. A PARSED document always yields evidence, because the absence of the
    /// key is itself the finding: no records plus no derivable fields is a clean scoped pass, and no
    /// records plus derivable fields present is precisely the hand-authored submission this gate exists
    /// to refuse. Returning null for both would collapse those two into one NOT CHECKED and lose the
    /// refusal.</para>
    /// </summary>
    public static DerivationEvidence ToDerivationEvidence(
        SubmissionDocument document,
        Func<string, string>? readFile,
        Func<string, byte[]>? readBytes = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var records = new List<DerivationRecord>();

        foreach (var d in document.Derivation ?? new List<DerivationDocument>())
        {
            var artifact = d.Artifact ?? string.Empty;
            string? observed = null;

            if (!string.IsNullOrWhiteSpace(artifact))
            {
                try
                {
                    // 🔴 *** RE-HASH THE WAY THE RECORD SAYS IT WAS HASHED, OR NOT AT ALL. *** A record
                    // stamped over BYTES compared against a hash taken over TEXT will never match, so
                    // mixing the two would report every derived field as STALE - a gate that refuses
                    // everything, for a reason having nothing to do with the submission. Where the record
                    // claims bytes and no byte reader was supplied, the honest answer is that this gate
                    // could not verify it, which is NOT CHECKED rather than a failure.
                    observed = d.HashedOverBytes
                        ? readBytes is null ? null : DerivationHash.OfBytes(readBytes(artifact))
                        : readFile is null ? null : DerivationHash.Of(readFile(artifact));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
                {
                    // Unreadable is not unchanged. Left null so the gate says it could not verify.
                    observed = null;
                }
            }

            records.Add(new DerivationRecord(
                d.Field ?? string.Empty,
                d.Producer ?? string.Empty,
                artifact,
                d.ArtifactSha256 ?? string.Empty,
                observed,
                d.Verified,
                d.HashedOverBytes));
        }

        return new DerivationEvidence(records, document.DerivableFieldsPresent());
    }

    /// <summary>
    /// Every field neither schema mapped, from BOTH documents, split into genuine unknowns and deliberate
    /// annotations.
    ///
    /// <para><b>The binding half was missing entirely</b>, and it is the document that carries the
    /// instrumentation — a misspelt <c>specName</c> there reproduces the run where 1 of 17 signals
    /// resolved and gate 5, the interface check and the conflict graph all failed together. 0b would have
    /// said nothing.</para>
    ///
    /// <para><b>When no binding is supplied, the binding's unknown set is not empty — it is absent</b>,
    /// and nothing here pretends otherwise: an unsupplied document contributes no paths and no claim.</para>
    /// </summary>
    private static (IReadOnlyList<string> Unknown, IReadOnlyList<string> Annotations) ExtraFields(
        SubmissionDocument document, BindingDocument? binding)
    {
        var submission = document.AllExtraFieldPaths();

        if (binding is null)
            return submission;

        var fromBinding = binding.AllExtraFieldPaths();

        return (submission.Unknown.Concat(fromBinding.Unknown).ToArray(),
                submission.Annotations.Concat(fromBinding.Annotations).ToArray());
    }

    /// <summary>
    /// Contract 2.7's join, off the document. <b>Null only when NEITHER key is present</b> — an empty
    /// `storage` beside a populated `harnessOnly` is a real declaration and must not collapse to "nobody
    /// said".
    /// </summary>
    public static SignalStorageMap? ToSignalStorage(SubmissionDocument document)
    {
        var storage = document.Map?.Storage;
        var harnessOnly = document.Map?.HarnessOnly;

        if (storage is null && harnessOnly is null)
            return null;

        return SignalStorageMap.Of(
            (storage ?? new Dictionary<string, StorageDocument>())
                .Select(e => (e.Key, new SignalStorage(e.Value?.Owner, e.Value?.Path ?? string.Empty))),
            harnessOnly);
    }

    /// <summary>
    /// One bound signal, off the document. <b>Nothing is defaulted</b> — an absent spec name stays absent.
    ///
    /// <para><c>Transient</c>, <c>RearmsEachIndex</c> and <c>ArmedBy</c> are threaded here because the
    /// first two were NOT, and the consequence was that both capabilities were unreachable from a
    /// binding: the domain model had the fields, the generator read them, and nothing could set them.
    /// <b>A field nobody can set is a field that does not exist</b>, however well it is implemented
    /// downstream. <c>ArmedBy</c> is threaded in the same commit that gives it a meaning, so it never
    /// spends a day in that state.</para>
    /// </summary>
    public static Harness.Map.MirroredSignal ToMirroredSignal(MirroredSignalDocument row) =>
        new(row.Tag ?? string.Empty, row.Type, row.SpecName, row.LatchedBy, row.Transient, row.RearmsEachIndex, row.ArmedBy,
            // *** THREADED IN THE SAME COMMIT THAT GIVES IT A MEANING. *** `Transient` and
            // `RearmsEachIndex` each spent time as fields the generator read and nothing could set — a
            // field nobody can set is a field that does not exist, however well it is implemented
            // downstream, and that is the third instance of it this file records.
            row.Encoding?.ToEncoding(),

            // *** THREADED IN THE SAME COMMIT THAT GIVES IT A MEANING, for the fourth time in this file's
            // history. *** The resting value was supplied by a hardcoded zero inside the wave builder, so
            // the capability was not merely unreachable from a binding — nobody could see it existed.
            row.InertRest?.ToRest());

    /// <summary>The enumeration projection, off the document. Public so the runner composes it the same way.</summary>
    /// <summary>
    /// 🔴 <b>EVERY ENUMERATION THE SUBMISSION CITES INTO, AS A SET.</b>
    ///
    /// <para><b><c>enumeration</c> and <c>enumerations</c> are mutually exclusive and supplying both
    /// THROWS.</b> Two answers to one question cannot be reconciled here: preferring either one silently
    /// discards a denominator somebody wrote down, and merging them is the union that belongs to no
    /// subject. Both CLIs wrap this read and report NOTHING EXAMINED naming the contradiction, which is
    /// the same treatment an unparseable document gets and for the same reason.</para>
    ///
    /// <para><b>Neither present is an EMPTY set, not an empty enumeration.</b> Gate 3j then reports
    /// NOTHING EXAMINED rather than gate 3 reporting an empty denominator — different facts, and the
    /// first is the one that is true.</para>
    /// </summary>
    public static AssertionEnumerationSet ToEnumerationSet(SubmissionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var plural = document.Enumerations ?? new List<EnumerationDocument>();

        if (document.Enumeration is not null && plural.Count > 0)
        {
            throw new InvalidDataException(
                $"the submission carries BOTH `enumeration` (singular) and `enumerations` ({plural.Count} of them). "
                + "*** THAT IS TWO ANSWERS TO ONE QUESTION AND NEITHER CAN BE PREFERRED HERE. *** Choosing one would silently "
                + "discard a coverage denominator somebody wrote down, and merging them would produce a denominator that belongs "
                + "to no subject — the exact defect subjects exist to remove. Use `enumeration` for a single-subject campaign, or "
                + "`enumerations` with a `subject` on each; never both.");
        }

        if (document.Enumeration is null && plural.Count == 0)
            return AssertionEnumerationSet.Empty;

        var documents = document.Enumeration is not null ? new List<EnumerationDocument> { document.Enumeration } : plural;

        return AssertionEnumerationSet.Of(documents.Select(ToEnumeration));
    }

    /// <summary>One enumeration document as the checked type. <b>Nulls are absences and stay absences</b> — see the field docs on each.</summary>
    public static AssertionEnumeration ToEnumeration(SubmissionDocument document) =>
        ToEnumeration(document.Enumeration);

    private static AssertionEnumeration ToEnumeration(EnumerationDocument? enumeration) =>
        AssertionEnumeration.Of(
            enumeration?.Clauses ?? Enumerable.Empty<string>(),
            enumeration?.Assertions ?? Enumerable.Empty<string>(),
            enumeration?.Forms,
            enumeration?.Enumerator ?? string.Empty,
            enumeration?.NormalisedTexts,
            enumeration?.RequiredObservations?.ToDictionary(
                e => e.Key,
                e => (IReadOnlySet<string>)e.Value.ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal),
            enumeration?.Bounds,
            // *** THE EMPTY LIST SURVIVES THE PROJECTION. *** `bounds: []` for an assertion is the
            // positive claim "this one depends on none" and is the only thing that lets a vector's
            // `boundsUsed: {}` pass; an assertion absent from the map stays absent, and its claim stays
            // NOT CHECKED. Collapsing empty to absent here would silently re-close the honest exit.
            enumeration?.AssertionBounds?.ToDictionary(
                e => e.Key,
                e => (IReadOnlySet<string>)e.Value.ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal),
            enumeration?.Subject ?? string.Empty,
            // *** THE MALFORMED CLAIM SURVIVES THE PROJECTION TOO, AS A REJECTION AND NOT AS A NULL. ***
            // `BoundsAbsenceClaim.Of` turns `claimed: true` with no `by` or no `because` into a REJECTED
            // claim, which is not a claim — the guard stays shut — but which carries the reason, so the
            // author is told why theirs did not count instead of watching it disappear. Collapsing it to
            // None here would restore the silence FI-99 exists to remove.
            enumeration?.BoundsAbsence is null
                ? BoundsAbsenceClaim.None
                : BoundsAbsenceClaim.Of(
                    enumeration.BoundsAbsence.Claimed,
                    enumeration.BoundsAbsence.By,
                    enumeration.BoundsAbsence.Because));

    /// <summary>The model's fidelity declaration, off the document.</summary>
    public static FidelityDeclaration? ToFidelity(SubmissionDocument document) =>
        document.Model is null
            ? null
            : FidelityDeclaration.Of(document.Model.Id ?? string.Empty,
                document.Model.Represents ?? Enumerable.Empty<string>(),
                document.Model.DoesNotRepresent,
                document.Model.ValidatedAgainstPlantData,
                document.Model.DeclaredBy ?? string.Empty);

    public static DeploymentDeclaration? ToDeploymentDeclaration(SubmissionDocument document) =>
        document.Deployment is null
            ? null
            : new DeploymentDeclaration(
                document.Deployment.ImportStamp,
                (document.Deployment.S7Objects ?? new List<S7ObjectDocument>())
                    .Select(o => new S7ObjectDeclaration(
                        o.Area ?? string.Empty, o.DbNumber, o.HarnessObject ?? string.Empty, o.Layout, o.LayoutSetAfterImport))
                    .ToArray(),
                document.Deployment.NoS7Transport);

    /// <summary>
    /// The reachable set, <b>COMPUTED FROM THE TAG MAP</b> with the same reader the transport uses.
    ///
    /// <para>A map that cannot be read yields <c>null</c>, so gate 11 reports NOT CHECKED rather than
    /// comparing against an empty set — which would say "the map reaches nothing", the opposite claim.</para>
    /// </summary>
    private static TagMapReach ToTagMapReach(SubmissionDocument document, Func<string, string>? readFile)
    {
        var deliverables = document.Deployment?.DeliverableObjects ?? new List<string>();

        if (string.IsNullOrWhiteSpace(document.TagMapPath) || readFile is null)
            return TagMapReach.None with { DeliverableObjects = deliverables.ToHashSet(StringComparer.Ordinal) };

        try
        {
            var map = Harness.S7.S7TagMap.FromJson(readFile(document.TagMapPath!));
            return TagMapReach.Of(map.Tags.Select(t => new S7Reach(t.Area, t.DbNumber)), deliverables);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or Harness.S7.S7ConfigurationException or System.Text.Json.JsonException)
        {
            // An unreadable map is NOT an empty one. Gate 11 then says the set-difference could not be
            // made, which is the honest answer and is not a pass.
            return TagMapReach.None with { DeliverableObjects = deliverables.ToHashSet(StringComparer.Ordinal) };
        }
    }

    /// <summary>
    /// X-D's block-level ceilings, off the document.
    ///
    /// <para><b><c>SubmissionGate.Check</c> has always taken these and <c>Evaluate</c> never passed
    /// them</b>, so gate 10b reported NOT CHECKED from the CLI even for a submission that could have
    /// answered it — the same shape as <c>forms</c> and <c>enumerator</c> before them. <c>compStable</c>
    /// comes from the MODEL because it is the model author's number.</para>
    ///
    /// <para>Null only when the document says nothing at all. An object present but incomplete is passed
    /// through so the gate can name the missing field, rather than being flattened to "absent".</para>
    /// </summary>
    /// <summary>
    /// The document's compression inputs. <b>Public so <c>harness-gate compress</c> reads them through
    /// exactly this path</b> — two readers for one section is how the gate and the tool that feeds it end
    /// up disagreeing, which has already happened once in this file's history with the derive reader.
    /// </summary>
    public static BlockCompressionInputs? ToCompressionInputs(SubmissionDocument document)
    {
        if (document.BlockCompression is null && document.Model?.CompStable is null)
            return null;

        var block = document.BlockCompression;

        return new BlockCompressionInputs(
            block?.PlantMs,
            block?.BudgetMs,
            (block?.Presets ?? new List<TimerPresetDocument>())
                .Select(p => new TimerPreset(p.Name ?? string.Empty, p.PresetMs, p.Source))
                .ToArray(),
            document.Model?.CompStable,
            block?.NegligibleFraction);
    }

    /// <summary>
    /// Both conflict inputs, combined — <b>and neither is allowed to launder the other</b>.
    ///
    /// <para>A bare block name in <c>computedConflicts</c> becomes an edge with <c>Unstated</c> provenance,
    /// so a document mixing the two gets the honest answer: the packing set is complete, and X-G's report
    /// is NOT CHECKED because part of the graph never said why. Null on BOTH means no graph at all, which
    /// is a third state again.</para>
    /// </summary>
    public static ConflictGraph? ToConflicts(SubmissionDocument document)
    {
        if (document.ComputedConflicts is null && document.ConflictEdges is null)
            return null;

        var edges = new List<ConflictEdge>();

        foreach (var e in document.ConflictEdges ?? new List<ConflictEdgeDocument>())
        {
            edges.Add(new ConflictEdge(
                e.BlockA ?? string.Empty, e.BlockB ?? string.Empty, e.Provenance,
                e.Signal ?? string.Empty, e.Class));
        }

        edges.AddRange(ConflictGraph.WithoutProvenance(document.ComputedConflicts ?? new List<string>()).Edges);

        return new ConflictGraph(edges);
    }

    public static SubmissionVector ToSubmissionVector(VectorDocument v) => new(
        v.Id ?? string.Empty,
        v.Slot ?? string.Empty,
        v.Index,
        new AgentIdentity(v.Author ?? string.Empty),
        string.IsNullOrWhiteSpace(v.Clause) && string.IsNullOrWhiteSpace(v.Assertion)
            ? null
            : new Basis(v.Clause ?? string.Empty, v.Assertion ?? string.Empty, v.Subject),
        v.Inputs ?? new Dictionary<string, string>(),
        v.StartBool ?? string.Empty,
        (v.Expectations ?? new List<ExpectationDocument>())
            // *** THE SHAPE IS CARRIED THROUGH, NOT DROPPED HERE. *** It is the last hop of the field's
            // sequencing: mapped by the schema so gate 0b stops refusing it, carried here so the runner
            // has something to pass to the evaluator. A field the parser knows and the projection drops
            // is the silently-ignored field gate 0b exists to prevent, one layer in.
            .Select(e => new ObservabilityDeclaration(e.Signal ?? string.Empty, e.Nature, e.Mode, e.WindowScans, e.Expected, e.TemporalShape))
            .ToArray(),
        v.AssertionForm,
        string.IsNullOrWhiteSpace(v.SettlingCondition)
            ? null
            // *** UnchangedForScans IS READ FROM THE DOCUMENT AND NOT DEFAULTED. *** It used to be
            // constructed as 0 here with no wire field behind it, so LoopRun.Settling returned
            // NotEstablished on its first line for every vector in every submission — a settling claim
            // that could not be evaluated, reported as one that was not established.
            : new SettlingDeclaration(v.SettlingCondition, v.SettlingSignals ?? new List<string>(), v.SettlingUnchangedForScans),
        v.MaxDurationScans,
        v.CompletionValue,
        (v.Blacklist ?? new List<BlacklistDocument>())
            .Select(b => new BlacklistEntry(b.Block ?? string.Empty, b.Reason ?? string.Empty))
            .ToArray(),
        v.CompressionFactor,
        (IReadOnlyCollection<string>?)v.AssertedBehaviours ?? Array.Empty<string>(),
        v.CompletionSignal ?? string.Empty,
        v.Kills,
        v.BoundsUsed);

    /// <summary>The report shape the 5.1 skill's Step 5 specifies.</summary>
    public static void Write(SubmissionReport report, SubmissionDocument document, TextWriter output)
    {
        var verdict = report.Verdict switch
        {
            SubmissionVerdict.AdmissibleSubjectToJudgement => "ADMISSIBLE-SUBJECT-TO-JUDGEMENT",
            SubmissionVerdict.NotAdmissible => "NOT ADMISSIBLE",
            _ => "NOTHING EXAMINED",
        };

        output.WriteLine($"VERDICT: {verdict}");
        output.WriteLine($"  {report.VectorsExamined} vector(s) examined, {report.Gates.Count} gate(s) run.");
        output.WriteLine("  There is deliberately no plain ADMISSIBLE: judgement gates can never be verified.");
        output.WriteLine();

        // 🔴 *** THE COVERAGE BLOCK, ON EVERY RUN AND ABOVE THE GATE LIST. ***
        //
        // A submission can be admissible, pass every gate, run on the rig and buy NOTHING — three vectors
        // citing two assertions is two units of coverage and three waves. Measured: five rig events over
        // five days, and the set of assertions ever asserted against a block did not change once. Not one
        // gate was wrong about that; there was simply no number, so nobody added it up.
        //
        // It is printed BEFORE the gates because it is the only line here that says what the submission is
        // WORTH rather than whether it is allowed, and a reader who stops at VERDICT should still meet it.
        foreach (var line in report.Coverage.Lines())
            output.WriteLine(line);

        output.WriteLine();

        // *** AN UNASKED QUESTION AND AN ANSWERED ONE MUST NOT PRINT ALIKE. ***
        //
        // Found live: a Never expectation was flipped to Sampled in a scratch copy AND THE GATE OUTPUT
        // DID NOT MOVE, because gate 5 is NOT CHECKED whenever the map is self-declared — so F-3's
        // refusal is masked, and the flipped vector would slip through today and be refused the moment
        // real bindings arrive. That is the authority fix working as designed, and it makes the CURRENT
        // report weaker than it looks. Anybody reading "gate 5 did not complain" is reading a question
        // nobody asked.
        //
        // So a NOT CHECKED line is marked in the margin as well as in the label, and the summary counts
        // them before the gate list rather than after it.
        if (report.NotChecked.Count > 0)
        {
            output.WriteLine($"*** {report.NotChecked.Count} GATE(S) WERE NOT CHECKED. A GATE THAT DID NOT RUN DID NOT PASS. ***");
            output.WriteLine("    Nothing below marked NOT CHECKED is evidence about this submission — its silence is an");
            output.WriteLine("    UNASKED QUESTION, and a defect it would have caught is still there. They are listed again");
            output.WriteLine("    at the end with what each one needs.");
            output.WriteLine();
        }

        output.WriteLine("GATES");
        foreach (var gate in report.Gates)
        {
            var status = gate.Status switch
            {
                GateStatus.Checked => gate.Passed ? "CHECKED   " : "REFUSED   ",
                GateStatus.Judgement => "JUDGEMENT ",
                _ => "NOT CHECKED",
            };

            // The margin marker is deliberately loud and deliberately NOT on the passing lines: a reader
            // skimming for trouble scans the left edge, and NOT CHECKED has to be trouble there.
            var margin = gate.Status switch
            {
                GateStatus.NotChecked => "!!",
                GateStatus.Checked when !gate.Passed => ">>",
                _ => "  ",
            };

            output.WriteLine($"{margin}[{status}] {gate.Gate}  (by {gate.Verifier})");
            output.WriteLine($"      {gate.Detail}");
        }

        if (report.NotChecked.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("!! NOT CHECKED — AND THESE ARE FOUR DIFFERENT FACTS, NOT ONE LIST");
            output.WriteLine("   A flat count of NOT CHECKEDs is exactly the shape that reads as a pass to a tired reader.");
            output.WriteLine("   One group below is not a gap at all; one can never be closed off the rig; two are build-list");
            output.WriteLine("   items with different owners. Read the group, not the number.");

            // Grouped in the order a reader should act on them: what is already correct, what needs the
            // rig session, then the two that are somebody's build list.
            // 🔴 THE "BY DESIGN — NOT A GAP" GROUP IS GONE, RETIRED 2026-08-14 BY MEASUREMENT. Both its
            // members (gate 5's map authority, gate 4b's absent fidelity) were driven and BOTH RUN once
            // the artifact they name exists. *** NO NOT CHECKED IN THIS REPORT IS A PROPERTY OF THE
            // DESIGN: every one names an artifact that could exist, or the rig. *** A group heading that
            // told a reader otherwise was the most comfortable line in the output and the least true.
            WriteNotCheckedGroup(report, output, NotCheckedReason.RequiresTheDevice,
                "REQUIRES THE DEVICE. A controller, a download or a wave — no artifact substitutes for it,",
                "and nothing offline can close it. These are the rig session's, and there is nothing to build.",
                null);

            WriteNotCheckedGroup(report, output, NotCheckedReason.AwaitingAnArtifactThatCouldExist,
                "AWAITING AN ARTIFACT THAT COULD EXIST. Closable without a rig and without breaking anybody's",
                "independence — the authority exists or could, and nobody has produced it yet. THIS IS THE BUILD LIST.",
                null);

            WriteNotCheckedGroup(report, output, NotCheckedReason.HarnessCapabilityMissing,
                "THE HARNESS CANNOT YET COMPUTE IT. Not the submission's fault and not anybody else's artifact:",
                "this one is the harness lane's own build list.",
                null);

            // *** A GATE THAT DID NOT SAY WHICH KIND IT IS. *** Its own defect, reported here rather than
            // silently joining one of the groups above — an unexplained NOT CHECKED is the entry this
            // whole grouping exists to make impossible.
            WriteNotCheckedGroup(report, output, NotCheckedReason.Unstated,
                "*** UNCLASSIFIED — THIS IS A DEFECT IN THE GATE, NOT IN THE SUBMISSION. ***",
                "It did not say whether it needs the rig, an independent authority, or an artifact nobody has",
                "written. Fix the gate: build it with GateResult.CouldNotRun, which requires the reason.");
        }

        WriteJudgements(report, output);
    }

    /// <summary>
    /// One reason-group of NOT CHECKED gates, or nothing when the group is empty.
    ///
    /// <para><b>An empty group prints nothing rather than "0"</b>: the point of the grouping is to stop a
    /// reader counting, and a row of zeroes invites exactly that.</para>
    /// </summary>
    private static void WriteNotCheckedGroup(
        SubmissionReport report, TextWriter output, NotCheckedReason reason, string line1, string? line2, string? line3)
    {
        var gates = report.NotChecked.Where(g => g.Reason == reason).ToArray();
        if (gates.Length == 0)
            return;

        output.WriteLine();
        output.WriteLine($"   [{gates.Length}] {line1}");
        if (line2 is not null) output.WriteLine("       " + line2);
        if (line3 is not null) output.WriteLine("       " + line3);

        foreach (var gate in gates)
            output.WriteLine($"     - {gate.Gate}: needs {gate.Verifier}");
    }

    private static void WriteJudgements(SubmissionReport report, TextWriter output)
    {
        if (report.Judgements.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("JUDGEMENT — recorded, never verified");
            foreach (var gate in report.Judgements)
                output.WriteLine($"  - {gate.Gate}");
        }

        output.WriteLine();
        output.WriteLine("ESCALATIONS — open contract questions this submission may turn on");
        output.WriteLine("  section 9.1  a vector refused at observability has no route back to testable.");
        output.WriteLine("               Do NOT fix it by adding a status output to the block: that collides with D13,");
        output.WriteLine("               and whether an author may change a block's interface purely to make it");
        output.WriteLine("               testable is OPEN WITH THE OWNER.");
        output.WriteLine("  section 9.4  a STAMPED assertion turning on a difference of 1 or 2 scans sits inside an");
        output.WriteLine("               unspecified off-by-one (the copy layer runs BEFORE the block).");
        output.WriteLine("  D6           what MAKES two agents different is undefined. The comparison here is");
        output.WriteLine("               normalised, which closes the keystroke variants and nothing deeper.");
    }
}

/// <summary>The one protocol constant this CLI needs, named rather than restated.</summary>
internal static class ModbusLimitsProxy
{
    public const int MaxReadRegisters = Harness.Map.ModbusLimits.MaxReadRegisters;
}
