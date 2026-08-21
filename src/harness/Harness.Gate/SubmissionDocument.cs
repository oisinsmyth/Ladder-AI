using System.Text.Json;
using System.Text.Json.Serialization;
using Harness.Results;

namespace Harness.Gate;

/// <summary>
/// The JSON a submission is written as, and the reader that turns it into the checked types.
///
/// <para><b>Every field is nullable on the way in, deliberately.</b> A missing field must reach the
/// schema gate as MISSING rather than as a default — a zero <c>MaxDuration</c> that arrived because
/// nobody wrote one and a zero that somebody typed are the same value, and only the gate should decide
/// what to do about it. Deserialising into non-nullable fields would silently supply the answer.</para>
/// </summary>
public sealed class SubmissionDocument
{
    public string? BlockAuthor { get; set; }

    /// <summary>
    /// The runtime compression factor.
    ///
    /// <para><b>Key presence is tracked</b> (see <see cref="RuntimeCompressionKeyPresent"/>) because this
    /// is a derivable field with a DEFAULT: absent and <c>1</c> deserialise to the same value, so gate 0c
    /// could not otherwise tell a field nobody wrote from one somebody typed. Same device the
    /// <c>conflictEdges</c> setter already uses, for the same reason.</para>
    /// </summary>
    public int RuntimeCompression
    {
        get => _runtimeCompression;
        set
        {
            _runtimeCompression = value;
            RuntimeCompressionKeyPresent = true;
        }
    }

    private int _runtimeCompression = 1;

    /// <summary>Whether the document CARRIED a <c>runtimeCompression</c> key, as opposed to defaulting to 1.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RuntimeCompressionKeyPresent { get; private set; }

    /// <summary>Slots in the wave set. Feeds §12a derivation 1's floor, which scales with tensor width.</summary>
    public int SlotsInWaveSet { get; set; } = 1;

    /// <summary>Result-region width in registers, for the slots-per-read arithmetic the floor uses.</summary>
    public int ResultRegistersPerSlot { get; set; } = 1;

    public ModelDocument? Model { get; set; }

    /// <summary>
    /// The single enumeration — <b>the shape every submission had before 2026-08-18, and still the right
    /// one for a single-subject campaign.</b>
    ///
    /// <para><b>Mutually exclusive with <see cref="Enumerations"/>.</b> Supplying both is two answers to
    /// one question and is refused by name; see <see cref="EnumerationConflict"/>.</para>
    /// </summary>
    public EnumerationDocument? Enumeration { get; set; }

    /// <summary>
    /// 🔴 <b>THE ENUMERATIONS A MULTI-SUBJECT CAMPAIGN CITES INTO — one per subject.</b>
    ///
    /// <para><b>Until this existed a submission could hold exactly one</b>, so a campaign with a valve
    /// enumeration and a vessel enumeration had one option: merge them. After a merge a citation to a
    /// clause both files declare is answered by whichever entry survived, and the denominator gate 3
    /// reports is the union of two denominators and therefore neither.</para>
    ///
    /// <para><b>Each element MUST declare its <c>subject</c> when there is more than one</b> — an unnamed
    /// subject in a set of two is a denominator nobody can cite into deliberately. Resolution, ambiguity
    /// and the reason the assertion hash cannot break the tie all live in
    /// <see cref="AssertionEnumerationSet"/>.</para>
    /// </summary>
    public List<EnumerationDocument>? Enumerations { get; set; }

    public MapDocument? Map { get; set; }

    /// <summary>
    /// Blocks the reference graph says conflict with this submission's targets.
    ///
    /// <b>Absent means the graph was not available</b>, and the blacklist gate then reports NOT CHECKED.
    /// An empty ARRAY is a different statement — "the graph ran and found no conflicts" — and is
    /// honoured as such.
    ///
    /// <para><b>A bare name records no PROVENANCE</b>, so a non-empty list here makes X-G's multi-writer
    /// gate NOT CHECKED. Supply <see cref="ConflictEdges"/> instead to say WHY two blocks conflict; the two
    /// are combined, so this field remains usable for the edges whose provenance genuinely is unknown.</para>
    /// </summary>
    public List<string>? ComputedConflicts { get; set; }

    /// <summary>
    /// Conflict edges WITH X-G's provenance: why the two blocks conflict, on which signal, and whether that
    /// signal is part of the deliverable.
    ///
    /// <para><b>This is what turns "0 multi-writer findings" from a sentence into a fact.</b> Without
    /// provenance the report is empty for a reason that has nothing to do with multi-writers, and X-G's
    /// whole point is that the packer separating two writers of one deliverable coil is right for testing
    /// and wrong to do silently.</para>
    /// </summary>
    public List<ConflictEdgeDocument>? ConflictEdges
    {
        get => _conflictEdges;
        set
        {
            _conflictEdges = value;
            ConflictEdgesKeyPresent = true;
        }
    }

    private List<ConflictEdgeDocument>? _conflictEdges;

    /// <summary>
    /// Whether the document CARRIED a <c>conflictEdges</c> key at all — set by the setter, which
    /// System.Text.Json calls even for an explicit <c>null</c>.
    ///
    /// <para>*** OMITTED, `[]` AND `null` ARE THREE DIFFERENT THINGS AND ONLY TWO OF THEM ARE LEGAL. ***
    /// <c>[]</c> is the EARNED positive claim that the graph ran over a whole corpus and found nothing.
    /// Omitting the key is the weaker and TRUE statement that it did not run — which is the right answer
    /// when a file in the corpus could not be parsed, because <b>the file that failed to parse may hold
    /// the second writer that makes a signal a conflict</b>. An explicit null is neither, and it is the
    /// dangerous one: <b>a lenient deserializer turns it back into an empty collection one layer down,
    /// restoring the false claim after the refusal was correctly made.</b> Gate 8 refuses it by name.</para>
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ConflictEdgesKeyPresent { get; private set; }

    /// <summary>True for the one illegal form: the key is there and its value is null.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ConflictEdgesExplicitlyNull => ConflictEdgesKeyPresent && _conflictEdges is null;

    /// <summary>
    /// X-D's three BLOCK-level ceilings — the inputs contract §2 gives an author nowhere to state.
    ///
    /// <para><b>The checks were built and the document could not carry what they needed</b>, so gate 10b
    /// reported NOT CHECKED even for a submission that could have answered it. That is the same shape as
    /// <c>forms</c> and <c>enumerator</c> before them: a gate whose refusal names a remedy nobody can
    /// supply is a dead end wearing the costume of a build list.</para>
    /// </summary>
    public BlockCompressionDocument? BlockCompression { get; set; }

    /// <summary>
    /// Contract §4.5 — a property of the DOWNLOAD, never of a vector.
    ///
    /// <para><b>Absent is NOT <c>s7Objects: []</c>.</b> The empty list is a POSITIVE CLAIM — no
    /// classic-S7comm path reaches a data block, the normal mirror-only state — and is checked as one.
    /// Absent is "nobody said", and gate 11 is NOT CHECKED.</para>
    /// </summary>
    public DeploymentDocument? Deployment { get; set; }

    /// <summary>
    /// Path to the S7 tag map the run will use. <b>Gate 11's set-difference is computed from it.</b>
    ///
    /// <para>It is a PATH and not a declared reachable set, deliberately: a set the submission stated
    /// would be the author vouching for the very artifact the gate exists to check them against. Absent
    /// means no map was available and the gate reports NOT CHECKED — an empty reachable set would be the
    /// opposite claim.</para>
    /// </summary>
    public string? TagMapPath { get; set; }

    public List<VectorDocument>? Vectors { get; set; }

    /// <summary>
    /// 🔴 <b>THE DERIVER'S PROVENANCE BLOCK — one entry per field a tool produced.</b>
    ///
    /// <para><b>Deliberately not an <c>_</c>-prefixed annotation.</b> Gate 0b excludes those BY NAME and
    /// never reads them, and this is the one piece of metadata that has to be read: gate 0c decides
    /// whether a derivable field was produced or typed, and it cannot do that from something the reader
    /// is contracted to skip.</para>
    /// </summary>
    public List<DerivationDocument>? Derivation { get; set; }

    /// <summary>
    /// Which of <see cref="Harness.Results.DerivableField.All"/> this document actually carried.
    ///
    /// <para><b>KEY PRESENCE, NOT VALUE TRUTHINESS.</b> A field that is present and empty was still
    /// written by somebody, and it is the writing this gate is about. <c>conflictEdges</c> and
    /// <c>runtimeCompression</c> therefore use their key-presence flags rather than their values — the
    /// first because an explicit null is a distinct and illegal third state, the second because it has a
    /// default that is indistinguishable from an author typing it.</para>
    /// </summary>
    public IReadOnlyList<string> DerivableFieldsPresent()
    {
        var present = new List<string>();

        if (Map is not null) present.Add(Harness.Results.DerivableField.Map);
        if (Map?.Storage is not null) present.Add(Harness.Results.DerivableField.Storage);
        if (ConflictEdgesKeyPresent) present.Add(Harness.Results.DerivableField.ConflictEdges);
        if (ComputedConflicts is not null) present.Add(Harness.Results.DerivableField.ComputedConflicts);
        if (BlockCompression is not null) present.Add(Harness.Results.DerivableField.BlockCompression);
        if (Deployment is not null) present.Add(Harness.Results.DerivableField.Deployment);
        if (!string.IsNullOrWhiteSpace(TagMapPath)) present.Add(Harness.Results.DerivableField.TagMapPath);
        if (RuntimeCompressionKeyPresent) present.Add(Harness.Results.DerivableField.RuntimeCompression);

        return present;
    }

    /// <summary>
    /// 🔴 <b>FIELDS THIS SCHEMA DOES NOT KNOW, CAPTURED RATHER THAN DISCARDED.</b>
    ///
    /// <para>*** A SILENTLY-IGNORED FIELD IS WORSE THAN A REJECTED ONE, BECAUSE IT READS AS ACCEPTED. ***
    /// The contract and this code diverged: §2.8 specifies <c>modes</c>, <c>modeSource</c> and
    /// <c>instrumentedBy</c>, and the code implements <c>specName</c> and <c>latchedBy</c> only. An author
    /// writing to the contract today emits three fields that vanish — and their submission looks admitted.</para>
    ///
    /// <para><b>This closes the divergence in the direction that cannot lie, whichever document is
    /// stale:</b> the gate names every unknown field and refuses. A reader then learns which of the two
    /// is out of date instead of getting a green built on a field nobody read.</para>
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }

    /// <summary>
    /// The unknown-field marker for a DELIBERATE ANNOTATION.
    ///
    /// <para>JSON has no comments, and this project's submissions carry a great deal of reasoning that
    /// belongs beside the data — the deliverable wave set has <b>82</b> of them. A leading underscore is
    /// the conventional marker for exactly that, and it is <b>self-identifying</b>: the author had to
    /// type it.</para>
    /// </summary>
    public const string AnnotationPrefix = "_";

    /// <summary>
    /// 🔴 <b>UNKNOWN fields — the ones gate 0b refuses — with annotations EXCLUDED and COUNTED.</b>
    ///
    /// <para>*** THE RATIONALE WAS RIGHT AND THE SCOPE WAS WRONG. *** Gate 0b exists to catch a
    /// CONTRACT/CODE DIVERGENCE: a field the contract SPECIFIES that this code does not read, which
    /// vanishes silently and leaves a green built on declarations nobody looked at. <b>An
    /// <c>_</c>-prefixed key can never be that field</b>, because the contract specifies no
    /// <c>_</c>-prefixed field and never will — the prefix means "this is not data".</para>
    ///
    /// <para><b>And a gate that refuses every ordinary submission is a gate that gets switched off</b> —
    /// which is worse than no gate, because it still appears in the list looking like a check. It refused
    /// the deliverable 82 times over its own commentary while the divergence it was built for
    /// (<c>modes</c>, <c>modeSource</c>, <c>instrumentedBy</c>) would have been items 83, 84 and 85 in
    /// the same message.</para>
    ///
    /// <para><b>The narrowing does not create a smuggling route, and that is checked rather than
    /// asserted.</b> A real divergence arrives under the name the CONTRACT gives it — nobody writes
    /// <c>_modes</c> and expects it read — so renaming a field into an annotation is not a mistake
    /// anybody makes by accident, and doing it deliberately would be forging a comment. The count is
    /// reported on every run so the convention stays visible instead of becoming a place to hide.</para>
    /// </summary>
    public IReadOnlyList<string> UnknownFieldPaths() => AllExtraFieldPaths().Unknown;

    /// <summary>Annotation paths, reported so the convention is visible rather than merely tolerated.</summary>
    public IReadOnlyList<string> AnnotationPaths() => AllExtraFieldPaths().Annotations;

    /// <summary>Every field the schema did not map, split into genuine unknowns and deliberate annotations.</summary>
    public (IReadOnlyList<string> Unknown, IReadOnlyList<string> Annotations) AllExtraFieldPaths()
    {
        var found = new List<string>();
        Collect(UnknownFields, string.Empty, found);
        Collect(Map?.UnknownFields, "map", found);
        Collect(Enumeration?.UnknownFields, "enumeration", found);

        // The provenance block gets the same treatment as every other sub-document. It is the one whose
        // whole job is to be READ, so a key dropped in silence here is a derivation nobody checked.
        foreach (var (record, index) in (Derivation ?? new List<DerivationDocument>()).Select((d, i) => (d, i)))
            Collect(record.UnknownFields, $"derivation[{index}]", found);

        // *** EVERY ELEMENT, NOT JUST THE FIRST. *** A typo in the second subject's enumeration is exactly
        // as silently dropped as one in the first, and it is the one nobody would go looking for.
        foreach (var (enumeration, index) in (Enumerations ?? new List<EnumerationDocument>()).Select((e, i) => (e, i)))
            Collect(enumeration.UnknownFields, $"enumerations[{index}]", found);

        Collect(Model?.UnknownFields, "model", found);
        Collect(Deployment?.UnknownFields, "deployment", found);
        Collect(BlockCompression?.UnknownFields, "blockCompression", found);

        foreach (var (vector, index) in (Vectors ?? new List<VectorDocument>()).Select((v, i) => (v, i)))
        {
            Collect(vector.UnknownFields, $"vectors[{index}]", found);

            foreach (var (expectation, j) in (vector.Expectations ?? new List<ExpectationDocument>()).Select((e, k) => (e, k)))
                Collect(expectation.UnknownFields, $"vectors[{index}].expectations[{j}]", found);
        }

        return Split(found);

        static void Collect(Dictionary<string, object?>? unknown, string prefix, List<string> into)
        {
            foreach (var key in (unknown ?? new Dictionary<string, object?>()).Keys)
                into.Add(prefix.Length == 0 ? key : prefix + "." + key);
        }
    }

    /// <summary>
    /// Split paths into unknowns and annotations. <b>The LEAF name decides</b> — a path is
    /// <c>vectors[3]._why</c>, and only the last segment is the key the author wrote.
    /// </summary>
    public static (IReadOnlyList<string> Unknown, IReadOnlyList<string> Annotations) Split(IEnumerable<string> paths)
    {
        var unknown = new List<string>();
        var annotations = new List<string>();

        foreach (var path in paths)
        {
            var dot = path.LastIndexOf('.');
            var leaf = dot >= 0 ? path[(dot + 1)..] : path;

            if (leaf.StartsWith(AnnotationPrefix, StringComparison.Ordinal))
                annotations.Add(path);
            else
                unknown.Add(path);
        }

        return (unknown, annotations);
    }

    public static SubmissionDocument Read(string json) =>
        JsonSerializer.Deserialize<SubmissionDocument>(json, Options)
        ?? throw new InvalidDataException("the submission document is empty.");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Serialise a submission back to the wire form.
    ///
    /// <para>🔴 <b>THE COUNTERPART TO <see cref="Read"/>, AND IT LIVES HERE FOR THE SAME REASON.</b> The
    /// deriver first carried its own reader options, which silently lacked the enum converter this
    /// document needs — so a perfectly good submission threw on parse and was passed through untouched.
    /// One document, one reader, one writer: a second set of options is a second opinion about the
    /// format, and it fails in the direction that looks like nothing happening.</para>
    ///
    /// <para><b>camelCase on the way out</b>, matching the contract and every hand-authored submission.
    /// Reading is case-insensitive, so this is a compatibility choice rather than a requirement.</para>
    /// </summary>
    public static string Write(SubmissionDocument document) =>
        JsonSerializer.Serialize(document, WriteOptions);

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}

public sealed class ModelDocument
{
    /// <summary>Fields this schema does not know. Named and refused by the gate — never silently dropped.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }

    public string? Id { get; set; }
    public List<string>? Represents { get; set; }
    public List<string>? DoesNotRepresent { get; set; }
    public bool ValidatedAgainstPlantData { get; set; }

    /// <summary>
    /// The model's declared <c>comp_stable</c> (M3/M4) — the factor its author states it behaves at.
    ///
    /// <para><b>It lives on the MODEL because it is the model author's number</b>, and it is nullable
    /// because an undeclared stability ceiling is not an infinite one. When compression is being applied
    /// and this is absent, X-D's model bound is <c>NOT DECLARED</c> and the plan is not runnable.</para>
    /// </summary>
    public double? CompStable { get; set; }

    /// <summary>
    /// Who declared this model's <c>Represents</c> set.
    ///
    /// <para><b>That list LICENSES every asserted behaviour (M4)</b>, so one written by the vector's own
    /// author is self-issued. Absent means gate 4b is NOT CHECKED — never a pass.</para>
    /// </summary>
    public string? DeclaredBy { get; set; }
}

/// <summary>
/// The block-level compression inputs. <b>Every one is nullable</b>, so an incomplete object reaches the
/// gate as INCOMPLETE.
/// </summary>
/// <remarks>
/// <c>PlantMs</c> and <c>BudgetMs</c> being nullable is a DIAGNOSTIC correction rather than a hole being
/// closed: as non-nullable doubles an omitted pair arrived as <c>0</c>, <c>TimeCompression.MinimumFor</c>
/// threw, and <c>GateCli</c> caught it as an unreadable document — <b>exit 2, NOTHING EXAMINED</b>. Both
/// fail closed, so nothing was ever admitted wrongly; what was wrong is that the operator was told the
/// document could not be read when the document was fine and one field was missing. Now it is
/// <b>exit 1, NOT CHECKED</b>, which names the field.
/// </remarks>
public sealed class BlockCompressionDocument
{
    /// <summary>How long the behaviour under test takes in the plant, for <c>comp_min</c>.</summary>
    public double? PlantMs { get; set; }

    /// <summary>How long the wave may spend on it.</summary>
    public double? BudgetMs { get; set; }

    /// <summary>The block's dwell presets, each declared DATA or LITERAL. <b>Unstated is refused, never guessed</b>.</summary>
    public List<TimerPresetDocument>? Presets { get; set; }

    /// <summary>
    /// 🔴 <b>SUPERSEDED 2026-08-18 AND NO LONGER CONSULTED.</b> It was the ratio-distortion threshold, and
    /// the specification works an example without ever saying where "negligible" ends — so it had no
    /// default and its absence made that bound <c>NOT DECLARED</c>, which refused the plan.
    ///
    /// <para>The ruling replaced it with <c>TimeCompression.AbsoluteTimerFloorMs</c> (no timer preset
    /// compressed below ~500 ms) plus the ruled <c>TimeCompression.LiteralHeadroomMultiple</c>, so the
    /// bound is computable from the presets alone. <b>The field is still PARSED so that submissions
    /// written before the ruling are not refused for carrying it</b>, and <c>TimeCompression.Plan</c>
    /// appends to its own detail that the declared value was not used — a stated input that quietly
    /// governs nothing being worse than an absent one. <b>Omit it in new submissions.</b></para>
    /// </summary>
    public double? NegligibleFraction { get; set; }

    /// <summary>
    /// Unknown keys here were outside gate 0b too — and this object carries X-D's ceilings, where a
    /// dropped field silently becomes an uncomputed bound rather than a refused one.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>Contract §4.5's deployment object.</summary>
public sealed class DeploymentDocument
{
    /// <summary>Fields this schema does not know. Named and refused by the gate — never silently dropped.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }

    /// <summary>Identifies the import the copy layer was generated by. Required whenever <c>S7Objects</c> is non-empty.</summary>
    public string? ImportStamp { get; set; }

    /// <summary>
    /// EVERY object any classic-S7comm path may reach. <b>Null and empty are different claims</b>: null
    /// is nobody said, <c>[]</c> is "no classic-S7comm path reaches a data block".
    /// </summary>
    public List<S7ObjectDocument>? S7Objects { get; set; }

    /// <summary>
    /// Names of blocks that SHIP, so a row naming one can be refused.
    ///
    /// <para><b>A caller-supplied list, and its weakness is named rather than hidden:</b> an author who
    /// omits a deliverable's name buys silence on that one row. It is here because the alternative is no
    /// check at all, and the reachability half of gate 11 does not depend on it — a reachable object that
    /// is not declared fails regardless of what anybody called deliverable.</para>
    /// </summary>
    public List<string>? DeliverableObjects { get; set; }

    /// <summary>
    /// <b>"There is no classic-S7comm transport in this deployment at all."</b> A positive claim, distinct
    /// from <c>s7Objects: []</c> (which asserts a path exists and reaches no data block) and from absent
    /// (which is "nobody said"). Declaring it alongside any <c>s7Objects</c> row is a contradiction and is
    /// refused.
    /// </summary>
    public bool NoS7Transport { get; set; }
}

/// <summary>One <c>s7Objects</c> row. <b><c>Layout</c> absent parses as <c>Unstated</c>, which is refused.</b></summary>
public sealed class S7ObjectDocument
{
    public string? Area { get; set; }

    public int DbNumber { get; set; }

    /// <summary>The harness-generated object this is. A row naming a DELIVERABLE block is a refusal.</summary>
    public string? HarnessObject { get; set; }

    public DeclaredLayout Layout { get; set; } = DeclaredLayout.Unstated;

    /// <summary>The <c>importStamp</c> that <c>--set Standard --yes</c> followed. <b>A stamp, not a boolean.</b></summary>
    public string? LayoutSetAfterImport { get; set; }
}

/// <summary>One dwell preset. <b><c>Source</c> absent parses as <c>Unstated</c>, which is refused.</b></summary>
/// <remarks>
/// The two real answers push in OPPOSITE directions — a DATA preset lowers the timer ceiling, a LITERAL
/// one lowers the ratio-distortion ceiling — so there is no fail-safe guess available.
/// </remarks>
public sealed class TimerPresetDocument
{
    public string? Name { get; set; }

    public double PresetMs { get; set; }

    public PresetSource Source { get; set; } = PresetSource.Unstated;
}

/// <summary>
/// The enumeration the gate consumes.
///
/// <para><b>The flat projection is legal and is not free.</b> Clauses plus assertions alone is what the
/// enumeration skill calls its projection, and against it two gates report NOT CHECKED: the assertion's
/// canonical FORM cannot be compared with what the vector declared (F-3's authority), and the
/// ENUMERATOR's independence cannot be established. Supplying Forms and Enumerator is what turns those
/// into checks.</para>
/// </summary>
public sealed class EnumerationDocument
{
    /// <summary>Fields this schema does not know. Named and refused by the gate — never silently dropped.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }

    /// <summary>
    /// 🔴 <b>WHAT THIS ENUMERATION IS THE DENOMINATOR *FOR* — e.g. the enumeration file's own
    /// <c>subject:</c> block, reduced to one word.</b>
    ///
    /// <para><b>Optional, and absent is correct for a single-subject campaign</b> — every enumeration
    /// written before 2026-08-18 is an unnamed single subject and stays valid unchanged. It becomes
    /// load-bearing the moment a submission carries two: a citation says which subject it means, and a
    /// clause declared by both with nothing saying which is a REFUSAL naming both, never a pick.</para>
    /// </summary>
    public string? Subject { get; set; }

    public List<string>? Clauses { get; set; }
    public List<string>? Assertions { get; set; }

    /// <summary>Assertion ID to its canonical form. Absent means the projection, and the form gate is NOT CHECKED.</summary>
    public Dictionary<string, AssertionForm>? Forms { get; set; }

    /// <summary>Who performed the decomposition. Absent means independence cannot be shown, which is NOT CHECKED.</summary>
    public string? Enumerator { get; set; }

    /// <summary>
    /// Assertion ID to the <c>normalised_text</c> it was computed from (§3.4).
    ///
    /// <para><b>Absent means the stamper's output is taken on trust, and gate 3g says so.</b> §3.4 lets a
    /// stamper have no independence from the block or the vector author for one reason only — the gate
    /// recomputes every ID from this text and refuses a mismatch. Omitting it removes that, and the
    /// omission is exactly what a compromised stamper would emit.</para>
    /// </summary>
    public Dictionary<string, string>? NormalisedTexts { get; set; }

    /// <summary>
    /// Assertion ID to <b>every signal a citation of it depends on</b> — the enumeration's
    /// <c>response_signal:</c> and its <c>also_requires_observation_of:</c>, merged.
    ///
    /// <para><b>AMB-14.</b> It was a single string, and a simultaneity claim has two signals: a vector
    /// citing one could expect on one output alone, never observe the other, and pass with the relation
    /// untested. A LIST, not a second scalar, because the next relational assertion may name three.</para>
    ///
    /// <para>Absent means gate 3h is NOT CHECKED — not a pass.</para>
    /// </summary>
    public Dictionary<string, List<string>>? RequiredObservations { get; set; }

    /// <summary>
    /// The enumeration's <c>bounds:</c> table — bound name to the SPECIFIED value, e.g.
    /// <c>{ "persistence_threshold": "T#60S" }</c>.
    ///
    /// <para><b>AMB-19.</b> Clauses refer to this table BY NAME so that no number enters a hashed
    /// assertion text — which is what makes a re-issue cost zero re-hashes, and which also means
    /// <b>retuning a value here changes what many assertions are true of while moving no assertion ID at
    /// all</b>. Nothing downstream would notice: no citation dangles, no ID mismatches, and staleness
    /// keys on assertion IDs rather than on bound values.</para>
    ///
    /// <para><b>Carry the BARE VALUE, not the enumeration's provenance prose.</b> The YAML writes
    /// <c>"T#60S  (Q-HBA-01, owner) — the SPECIFIED value"</c>; this field wants <c>"T#60S"</c>. A
    /// transcribed comparison is the same trust <c>normalisedTexts</c> already rests on, and a
    /// mis-transcription shows up as a loud STALE naming both strings rather than as a silent pass.</para>
    ///
    /// <para>Absent means gate 3i is NOT CHECKED — never a pass.</para>
    /// </summary>
    public Dictionary<string, string>? Bounds { get; set; }

    /// <summary>
    /// Assertion ID to <b>which of the bounds above that assertion depends on</b> — the relation that
    /// makes a vector's <c>boundsUsed: {}</c> a checkable claim instead of an accepted one.
    ///
    /// <para>🔴 <b>WITHOUT IT, A HONEST VECTOR HAD NO EXIT.</b> An assertion about a gating condition, an
    /// ordering or a state names no time and no threshold, so its vector truthfully records no bound —
    /// and gate 3i refused that, leaving <i>inventing a bound</i> as the only way through. Measured on a
    /// real submission, on two vectors, 2026-08-17.</para>
    ///
    /// <para><b>An EMPTY list for an assertion is the positive statement "this one depends on none"</b>
    /// and is what lets the empty claim pass. <b>An assertion simply MISSING from this map is an
    /// absence</b> and leaves the claim NOT CHECKED — the same absent-versus-empty discipline
    /// <c>reachable-state</c> uses. Absent altogether means every empty claim is NOT CHECKED.</para>
    ///
    /// <para><b>It belongs to the ENUMERATION</b> — the third party — for the same reason the enumeration
    /// itself does: a claim verified against something its own author wrote is not verified.</para>
    /// </summary>
    public Dictionary<string, List<string>>? AssertionBounds { get; set; }
}

public sealed class MapDocument
{
    /// <summary>Fields this schema does not know. Named and refused by the gate — never silently dropped.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }

    /// <summary>signal → instrumentation modes the copy layer provides for it. <b>HOW it is watched.</b></summary>
    public Dictionary<string, List<string>>? ProvidedFor { get; set; }

    /// <summary>
    /// signal → <b>WHERE it lives on the controller</b> (contract 2.7).
    ///
    /// <para>*** GATES 8 AND 8c COULD NOT BE FED FROM A REAL SUBMISSION AT ALL — NOT MERELY UNSUPPLIED,
    /// BUT INEXPRESSIBLE. *** A conflict graph is a statement about STORAGE: two blocks conflict because
    /// they write the same location. <c>providedFor</c> carries observability modes only — it says how a
    /// signal is watched and never where it is. Measured on a live submission: <b>1 of 17 signals
    /// resolved, and that one only because its spec name and block tag happen to be the same string.</b></para>
    /// </summary>
    public Dictionary<string, StorageDocument>? Storage { get; set; }

    /// <summary>
    /// Signals that occupy <b>NO PLC storage at all</b> — a POSITIVE CLAIM, not an omission.
    ///
    /// <para><b>The third state, and it is the one that makes the other two mean anything.</b> The
    /// tooling names an ambiguity it cannot settle: an unresolved signal reports <i>"this may be a
    /// mirror-only signal, or the name may be wrong"</i> — two entirely different repairs behind one
    /// silence. <b>The claim is the author's to make, and making it turns a NOT CHECKED into a fact:</b>
    /// no conflict edge is possible for these, computed rather than assumed.</para>
    /// </summary>
    public List<string>? HarnessOnly { get; set; }
}

/// <summary>
/// One <c>map.storage</c> entry. <b>Two keys, deliberately not one dotted string.</b>
/// </summary>
/// <remarks>
/// The contract's words: <i>an emitted string is not a schema.</i> A consumer handed <c>A.B.C</c> cannot
/// tell whether <c>A</c> is an owning block or a DB without parsing — and a parse is a lookup, which is
/// the thing this field exists to remove.
/// </remarks>
public sealed class StorageDocument
{
    /// <summary>
    /// The block or tag table that DECLARES the root. <b>Omitted for a global path</b> — a DB member, a
    /// PLC tag, an <c>iDB_…</c> member or a physical address is already unique.
    /// </summary>
    public string? Owner { get; set; }

    /// <summary>The path within that owner, or the global path verbatim.</summary>
    public string? Path { get; set; }
}

/// <summary>
/// One derived field's provenance, as written by <c>harness-gate derive</c>.
///
/// <para><b>The author does not write these and cannot usefully forge one:</b> the producer must be a
/// name gate 0c knows, and the hash must still match the artifact on disk. Typing a plausible-looking
/// entry by hand for a field you also typed by hand fails on the hash, which is the point.</para>
/// </summary>
public sealed class DerivationDocument
{
    /// <summary>Fields this schema does not know. Named and refused by gate 0b — never silently dropped.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }

    /// <summary>The submission field this attests to, by its own JSON property name.</summary>
    public string? Field { get; set; }

    /// <summary>The tool that produced it. Must be one gate 0c recognises.</summary>
    public string? Producer { get; set; }

    /// <summary>Path to the artifact it was derived from, as the deriver saw it.</summary>
    public string? Artifact { get; set; }

    /// <summary>The artifact's SHA-256 at derivation time. Re-hashed by the gate; a mismatch is a stale derivation.</summary>
    public string? ArtifactSha256 { get; set; }

    /// <summary>
    /// How hard the claim is: <c>Computed</c>, <c>ByRule</c> or <c>Attributed</c>.
    ///
    /// <para><b>Defaults to <c>Attributed</c>, the weakest</b>, so a record written before this field
    /// existed — or by anything that omits it — claims the least rather than the most.</para>
    /// </summary>
    public Harness.Results.DerivationVerification Verified { get; set; } = Harness.Results.DerivationVerification.Attributed;

    /// <summary>
    /// Whether the hash was taken over the artifact's BYTES rather than its text as read.
    ///
    /// <para><b>Defaults to false for the same reason:</b> the text hash is the weaker check, and an
    /// omitted field must not silently claim the stronger one.</para>
    /// </summary>
    public bool HashedOverBytes { get; set; }
}

public sealed class VectorDocument
{
    /// <summary>Fields this schema does not know. Named and refused by the gate — never silently dropped.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }

    public string? Id { get; set; }
    public string? Slot { get; set; }
    public int Index { get; set; }
    public string? Author { get; set; }
    public string? Clause { get; set; }
    public string? Assertion { get; set; }

    /// <summary>
    /// 🔴 <b>WHICH ENUMERATION THIS CITATION MEANS.</b> Optional, and unqualified stays legal for ever —
    /// what is refused is a clause declared by TWO subjects with nothing saying which.
    ///
    /// <para><b>Every citation written before 2026-08-18 is unqualified and means the subject that existed
    /// then.</b> A set of one cannot be ambiguous, so those vectors resolve exactly as they always did and
    /// are never retargeted.</para>
    /// </summary>
    public string? Subject { get; set; }
    public Dictionary<string, string>? Inputs { get; set; }
    public string? StartBool { get; set; }
    public List<ExpectationDocument>? Expectations { get; set; }

    /// <summary>
    /// The cited assertion's canonical form.
    ///
    /// <para><b>It does NOT default to <c>When</c> any more.</b> It used to, on the reasoning that WHEN
    /// is the shape checked hardest — but the form decides whether a SAMPLED observation is admissible
    /// (F-3), so a document that omitted the field was handed the permissive path. The zero value is
    /// <see cref="AssertionForm.Unstated"/>, which <b>fails the same comparison a WRONG form fails</b>.
    /// The enumeration is where an author gets the right answer.</para>
    /// </summary>
    public AssertionForm AssertionForm { get; set; } = AssertionForm.Unstated;

    /// <summary>
    /// The value on the completion signal that means "finished".
    ///
    /// <para>*** THE DEFAULT OF 1 IS GONE, AND IT WAS A LIVE DEFECT. *** <c>SlotRun</c> compares a result
    /// register against this and reports <c>TIMED-OUT</c> otherwise, so a block signalling completion with
    /// a STATE NUMBER was compared against a value nobody stated — and a <b>healthy block read as never
    /// having finished</b>. 1 is the harness's convention, not the block's, and a field that supplies the
    /// answer nobody gave is the missing-predicate defect one field over.</para>
    ///
    /// <para>Absent is a REFUSAL at the schema gate, and so is anything outside 0..65535 — the wave casts
    /// it to a holding register with an unchecked conversion.</para>
    /// </summary>
    public int? CompletionValue { get; set; }
    public string? SettlingCondition { get; set; }
    public List<string>? SettlingSignals { get; set; }

    /// <summary>
    /// 🔴 <b>The one settling form the runner can EVALUATE: the observed value unchanged across this many
    /// consecutive scans.</b>
    ///
    /// <para>*** THE FIELD EXISTED ON THE CHECKED TYPE AND HAD NO WIRE REPRESENTATION, SO EVERY VECTOR IN
    /// EVERY SUBMISSION RETURNED <c>SettlingState.NotEstablished</c>. *** <c>SettlingDeclaration</c> has
    /// carried <c>UnchangedForScans</c> since the runner could check it, and <c>ToSubmissionVector</c>
    /// constructed every declaration with <c>0</c> — the value that means <i>"prose the runner cannot
    /// check"</i>. <c>LoopRun.Settling</c> returns on that first line, so <b>every settling claim this
    /// project has made through a document was vacuous</b>: not wrong, not refused, simply never evaluated.
    /// Third instance of <i>the domain model gained a field and the wire format did not</i>.</para>
    ///
    /// <para><b>Zero remains a legal, meaningful value and is NOT refused</b> — it says the declared
    /// condition is prose, which is the honest state for a condition no structural form captures. Gate 6
    /// still requires a <c>settlingCondition</c>, and whether the condition implies finality is gate 6b's
    /// judgement either way. What zero must never be is a value nobody chose.</para>
    /// </summary>
    public int SettlingUnchangedForScans { get; set; }
    public int MaxDurationScans { get; set; }
    public List<BlacklistDocument>? Blacklist { get; set; }
    public int CompressionFactor { get; set; } = 1;
    public List<string>? AssertedBehaviours { get; set; }
    public string? CompletionSignal { get; set; }
    public string? Kills { get; set; }

    /// <summary>
    /// <b>Which specified bound this vector was written against</b> — bound name to the value used, e.g.
    /// <c>{ "persistence_threshold": "T#60S" }</c>. Compared against <c>enumeration.bounds</c> by gate 3i.
    ///
    /// <para><b>Absent is NOT CHECKED, and it is the AMB-19 hole itself rather than a formality.</b> A
    /// vector that never records the number it was written against cannot be found stale by anything —
    /// the bound is retuned, the vector goes on testing the old value, no ID moves, nothing dangles and
    /// every gate stays green. The field is where a vector makes itself falsifiable on that axis.</para>
    /// </summary>
    public Dictionary<string, string>? BoundsUsed { get; set; }
}

public sealed class ExpectationDocument
{
    /// <summary>Fields this schema does not know. Named and refused by the gate — never silently dropped.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }

    public string? Signal { get; set; }
    public SignalNature Nature { get; set; }
    public InstrumentationMode Mode { get; set; }
    public int WindowScans { get; set; }

    /// <summary>
    /// The value this expectation asserts — contract §2's <c>predicate</c>.
    ///
    /// <para><b>The field existed on the checked type and there was no way to supply one</b>, which is the
    /// same shape as every other hole this component has found: the case a field exists for gets tested,
    /// and the case where it was ignored does not. Absent is a REFUSAL at the schema gate, never a
    /// default — an expectation with nothing to compare against cannot fail.</para>
    /// </summary>
    public string? Expected { get; set; }

    /// <summary>
    /// 🔴 <b>WHAT THIS EXPECTATION CLAIMS ABOUT ITS SIGNAL *IN TIME* — the question nothing in the vector
    /// format could ask until 2026-08-18.</b>
    ///
    /// <para><b>Why the field had to be mapped here before any author could write it.</b> Gate 0b refuses
    /// a field this schema does not know, by name — <i>"a silently-ignored field is worse than a rejected
    /// one, because it reads as accepted"</i> — so a vector carrying <c>temporalShape</c> was not ignored,
    /// it was REFUSED, and the whole submission with it. Mapping it is step 2 of the sequencing in
    /// <c>docs/notes/observation-window-shapes.md §7</c>; step 3 is the runner passing it through.</para>
    ///
    /// <para><b>Absent parses as <see cref="TemporalShape.Unstated"/>, which reproduces the previous fold
    /// exactly, down to the text.</b> Accepted values: <c>throughout</c>, <c>atSomePoint</c>,
    /// <c>becomesAndHolds</c>, <c>atEnd</c>, <c>atNoPoint</c>.</para>
    ///
    /// <para>⚠️ <b>A MISSPELT SHAPE IS A PARSE REFUSAL, NOT A SILENT <c>Unstated</c></b> — the same
    /// treatment <c>nature</c> and <c>mode</c> have had all along. The design note's §7 reads "unrecognised
    /// ⇒ Unstated", which is right about the EVALUATOR and would be wrong here: coercing a typo to the
    /// default is precisely the quiet convenience its own next paragraph forbids, where <i>"a dropped field
    /// must fail the same comparison a wrong one does"</i>. Dropped and misspelt must not part company at
    /// the parser.</para>
    /// </summary>
    public TemporalShape TemporalShape { get; set; } = TemporalShape.Unstated;
}

/// <summary>One conflict edge with X-G's provenance. Every field is required; the enums' zero values are unusable.</summary>
public sealed class ConflictEdgeDocument
{
    public string? BlockA { get; set; }
    public string? BlockB { get; set; }

    /// <summary>Why they conflict. <b>Absent parses as <c>Unstated</c></b>, which makes the X-G gate NOT CHECKED.</summary>
    public ConflictProvenance Provenance { get; set; } = ConflictProvenance.Unstated;

    public string? Signal { get; set; }

    /// <summary>Whether the signal ships. <b>Absent parses as <c>Unstated</c></b>, which makes the X-G gate NOT CHECKED.</summary>
    public SignalClass Class { get; set; } = SignalClass.Unstated;
}

public sealed class BlacklistDocument
{
    public string? Block { get; set; }
    public string? Reason { get; set; }
}
