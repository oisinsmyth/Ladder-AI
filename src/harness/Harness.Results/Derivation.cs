namespace Harness.Results;

/// <summary>
/// 🔴 <b>THE FIELDS A SUBMISSION MUST NOT HAND-AUTHOR — because a tool already knows them.</b>
///
/// <para>*** A FIELD THAT CAN BE DERIVED MUST NEVER BE AUTHORED. *** A hand-typed derivable field is
/// only an opportunity to disagree with reality, and a gate that checks it is checking the
/// TRANSCRIPTION rather than the truth. Every name below is produced by something that already exists —
/// the copy-layer generator, <c>converter reachable-state</c>, the device gateway, the timing
/// constants — and until now nothing composed them into the document the gate reads, so an agent
/// retyped them and the gates graded the typing.</para>
///
/// <para><b>What is NOT here is the point of the list.</b> The vectors, the enumeration, the fidelity
/// declaration and the author's own identity stay authored, and deriving any of them would be
/// circular: an expectation derived from the block under test makes every test pass.</para>
///
/// <para><b>Names are the SUBMISSION'S OWN JSON PROPERTY NAMES</b>, not prettier labels. The refusal
/// has to name the thing the author would have to go and delete, and a translated name is one more
/// place the two documents can drift apart.</para>
/// </summary>
public static class DerivableField
{
    /// <summary>The observability map. Produced by the copy-layer generator from the coordinator's binding.</summary>
    public const string Map = "map";

    /// <summary>
    /// Signal → controller storage. Produced by <c>converter reachable-state</c>.
    ///
    /// <para><b>A NESTED key, and named as one.</b> It lives under <c>map</c> rather than at the top
    /// level, and it has a DIFFERENT producer from the rest of the map — the copy-layer generator knows
    /// how a signal is watched, only the reference graph knows where it lives. Naming it separately is
    /// what lets each half be attributed to the tool that actually produced it; folding it into
    /// <c>map</c> would credit the generator with a fact it does not have.</para>
    /// </summary>
    public const string Storage = "map.storage";

    /// <summary>Conflict edges with provenance. Produced by the reachable-state set intersection.</summary>
    public const string ConflictEdges = "conflictEdges";

    /// <summary>Bare conflict names. Same producer, weaker form; derivable for exactly the same reason.</summary>
    public const string ComputedConflicts = "computedConflicts";

    /// <summary>Block-level compression ceilings and timer presets. Produced from the block's own IR and the measured timing.</summary>
    public const string BlockCompression = "blockCompression";

    /// <summary>Import stamp and S7 objects. Produced by the device gateway that performed the deployment.</summary>
    public const string Deployment = "deployment";

    /// <summary>Path to the S7 tag map. Produced by the deployment, not chosen by the vector author.</summary>
    public const string TagMapPath = "tagMapPath";

    /// <summary>The runtime compression factor. Produced by the time-compression arithmetic.</summary>
    public const string RuntimeCompression = "runtimeCompression";

    /// <summary>
    /// Every derivable field, in the order a refusal should list them.
    ///
    /// <para><b>Deliberately NOT including <c>slotsInWaveSet</c> and <c>resultRegistersPerSlot</c>.</b>
    /// Both are wave-set geometry the coordinator does know, so both are candidates — but they are not
    /// produced by any of the composers this gate can name today, and adding a field to this list before
    /// something produces it turns the gate into one that refuses submissions nobody can yet fix.</para>
    /// </summary>
    public static IReadOnlyList<string> All { get; } = new[]
    {
        Map, Storage, ConflictEdges, ComputedConflicts, BlockCompression, Deployment, TagMapPath, RuntimeCompression,
    };

    /// <summary>True when <paramref name="field"/> is one this gate governs.</summary>
    public static bool IsDerivable(string field) => All.Contains(field, StringComparer.Ordinal);
}

/// <summary>
/// The tools allowed to be named as the producer of a derived field.
///
/// <para><b>An unrecognised producer is a refusal naming it, never a shrug.</b> Without the closed set,
/// <c>"producer": "me"</c> satisfies the gate perfectly and the whole mechanism becomes a formality —
/// the author would be attesting to their own transcription in a field designed to prevent exactly
/// that.</para>
/// </summary>
public static class DerivationProducer
{
    public const string CopyLayerGenerator = "CopyLayerGenerator";
    public const string ReachableState = "converter reachable-state";
    public const string SlotConflictDerivation = "SlotConflictDerivation";
    public const string DeviceGateway = "Harness.Device";
    public const string TimeCompression = "TimeCompression";
    public const string S7TagMap = "S7TagMap";

    /// <summary>Every producer this gate will accept.</summary>
    public static IReadOnlySet<string> Known { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        CopyLayerGenerator, ReachableState, SlotConflictDerivation, DeviceGateway, TimeCompression, S7TagMap,
    };

    /// <summary>
    /// 🔴 <b>WHAT EACH PRODUCER'S ARTIFACT MUST TURN OUT TO BE.</b>
    ///
    /// <para>*** MEASURED ON A REAL JOB, TWICE, AND BOTH WOULD HAVE BEEN STAMPED. *** The artifact that
    /// should have produced the conflict edges was a <c>notComputed</c> report — it resolved none of the
    /// submission's signals and said so — while the submission declared the edges anyway. The deployment
    /// artifact read <c>outcome: NotDeployed</c>, its own detail stating the device was not running the
    /// staged build. Both exist, both are readable, both hash. <b>An artifact being PRESENT is not the
    /// artifact being an ANSWER.</b></para>
    /// </summary>
    public static ArtifactKind KindFor(string producer) => producer switch
    {
        CopyLayerGenerator => ArtifactKind.Binding,
        ReachableState => ArtifactKind.ReachableState,
        SlotConflictDerivation => ArtifactKind.ConflictGraph,
        DeviceGateway => ArtifactKind.DeployResult,
        S7TagMap => ArtifactKind.TagMap,
        TimeCompression => ArtifactKind.None,
        _ => ArtifactKind.None,
    };
}

/// <summary>
/// The one hash both sides use.
///
/// <para><b>It hashes the artifact AS THE READER RETURNS IT, not the bytes on disk</b>, and that is a
/// deliberate limit rather than an oversight: the gate is given a text reader and nothing else, and
/// introducing a second reader here would be a second opinion that can drift from the one the deriver
/// used — the failure this whole mechanism exists to remove. The consequence is that a change visible
/// only below the text layer (a BOM appearing, an encoding change that round-trips) is not detected.
/// Every change to the artifact's actual CONTENT is.</para>
/// </summary>
public static class DerivationHash
{
    /// <summary>Lowercase hex SHA-256 of the UTF-8 encoding of <paramref name="content"/>.</summary>
    public static string Of(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Hex(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
    }

    /// <summary>
    /// Lowercase hex SHA-256 of the artifact's ACTUAL BYTES — the strong form.
    ///
    /// <para><b>Preferred wherever a byte reader is available</b>, because it closes the gap
    /// <see cref="Of"/> documents: a change visible only below the text layer (a BOM appearing, an
    /// encoding that round-trips) leaves the text hash identical. Which form was used is recorded on
    /// the record itself, so the weaker one is never invisible.</para>
    /// </summary>
    public static string OfBytes(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Hex(System.Security.Cryptography.SHA256.HashData(content));
    }

    private static string Hex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();
}

/// <summary>
/// 🔴 <b>HOW HARD THE CLAIM BEHIND A DERIVED FIELD ACTUALLY IS.</b>
///
/// <para>*** "DERIVED" WAS ONE UNDIFFERENTIATED WORD, AND IT COVERED TWO VERY DIFFERENT THINGS. *** A
/// field whose value was RECOMPUTED and matched is evidence; a field merely pointed at a file is a
/// citation. Both are better than a typed number, and they are not the same, so the gate counts them
/// separately and says so.</para>
/// </summary>
public enum DerivationVerification
{
    /// <summary>
    /// <b>The value was recomputed from the artifact and matched.</b> The strong form — a mismatch
    /// would have been a refusal, so a record carrying this is a checked claim.
    /// </summary>
    Computed = 0,

    /// <summary>
    /// <b>Settled by a stated rule rather than by recomputation</b>, because nothing computes it. The
    /// rule is named in the refusal path, so it is auditable — see <c>runtimeCompression</c>, where
    /// "1 with no declared bounds" is trivially true and "&gt; 1 with no bounds" is refused.
    /// </summary>
    ByRule = 1,

    /// <summary>
    /// <b>Cited to an artifact of the right kind, but not recomputed.</b> Nothing in-harness produces
    /// the value. Still meaningfully stronger than a typed field — the artifact must exist, be the
    /// right kind, not be a failure report, and still hash — but it is the weakest of the three and is
    /// reported as its own count rather than folded in with the others.
    /// </summary>
    Attributed = 2,
}

/// <summary>What an artifact must turn out to BE, before its hash is worth recording.</summary>
public enum ArtifactKind
{
    /// <summary>No artifact at all — the value is settled by rule.</summary>
    None = 0,

    /// <summary>The coordinator's binding document.</summary>
    Binding = 1,

    /// <summary><c>converter reachable-state</c> output.</summary>
    ReachableState = 2,

    /// <summary>A conflict-graph document.</summary>
    ConflictGraph = 3,

    /// <summary>A serialized loop result, carrying the deployment's own outcome.</summary>
    DeployResult = 4,

    /// <summary>An S7 tag map.</summary>
    TagMap = 5,
}

/// <summary>
/// One derived field's provenance: what produced it, out of which artifact, and whether that artifact
/// still hashes to what the derivation recorded.
/// </summary>
/// <param name="Field">The submission field this attests to. One of <see cref="DerivableField.All"/>.</param>
/// <param name="Producer">The tool that produced it. One of <see cref="DerivationProducer.Known"/>.</param>
/// <param name="Artifact">Path to the artifact it was derived from, as the deriver saw it.</param>
/// <param name="DeclaredSha256">The artifact's hash AT DERIVATION TIME, recorded by the deriver.</param>
/// <param name="ObservedSha256">
/// The artifact's hash AS THE GATE FOUND IT, or <c>null</c> when the artifact could not be read.
///
/// <para><b>The gate does not read files.</b> Everything in <c>Harness.Results</c> is pure arithmetic
/// over typed inputs, and the caller that has a file reader does the reading — the same division that
/// keeps every refusal path in this assembly testable with no filesystem in the room. <c>null</c> is
/// therefore "the caller could not read it", which is NOT CHECKED, and never "it matched".</para>
/// </param>
/// <param name="Verified">
/// How hard the claim is — recomputed, settled by rule, or merely cited. <b>Reported as three separate
/// counts</b> rather than folded into one word; see <see cref="DerivationVerification"/>.
/// </param>
/// <param name="HashedOverBytes">
/// True when the hash was taken over the artifact's actual bytes, false when over its text as read.
/// <b>Recorded rather than assumed, so the weaker form is never invisible</b> — a text hash cannot see
/// a change below the text layer, and a reader has to be able to tell which check they got.
/// </param>
public sealed record DerivationRecord(
    string Field,
    string Producer,
    string Artifact,
    string DeclaredSha256,
    string? ObservedSha256,
    DerivationVerification Verified = DerivationVerification.Attributed,
    bool HashedOverBytes = false)
{
    /// <summary>True when the gate's caller managed to read the artifact at all.</summary>
    public bool ArtifactWasRead => ObservedSha256 is not null;

    /// <summary>
    /// True only when the artifact was read AND still hashes to what the deriver recorded.
    ///
    /// <para><b>An unread artifact is false here and must not be treated as a refusal</b> — the two are
    /// separated at the call site, because "the artifact changed under us" and "we could not look" are
    /// different findings and only the first is the submission's fault.</para>
    /// </summary>
    public bool HashMatches =>
        ObservedSha256 is not null
        && string.Equals(DeclaredSha256, ObservedSha256, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// What the derivation gate reasons over: the provenance records, and which derivable fields the
/// submission actually carried.
///
/// <para><b>Both halves are needed and neither implies the other.</b> The records alone cannot detect a
/// hand-authored field — that is a field PRESENT with no record — and the present-set alone cannot
/// detect a stale one. A gate given only records would pass the exact submission this exists to
/// refuse.</para>
///
/// <para><b>There is deliberately no <c>Empty</c> or <c>None</c> singleton.</b> An absent evidence object
/// means nobody supplied it, which is NOT CHECKED; an evidence object carrying no records is the
/// positive claim that nothing was derived. Offering a shared "none" value is how the first becomes the
/// second by accident — the distinction this project keeps having to relearn. <see cref="NoDocument"/>
/// is the one blessed shortcut and it is named for the claim it makes, not for its emptiness.</para>
/// </summary>
/// <param name="Records">One per field the deriver produced.</param>
/// <param name="DerivableFieldsPresent">
/// The subset of <see cref="DerivableField.All"/> the submission document actually carried, decided by
/// the document reader — key presence, not value truthiness, because a field that is present and empty
/// was still authored by somebody.
/// </param>
public sealed record DerivationEvidence(
    IReadOnlyList<DerivationRecord> Records,
    IReadOnlyList<string> DerivableFieldsPresent)
{
    /// <summary>
    /// 🔴 <b>THE CLAIM OF A CALLER THAT HAS NO SUBMISSION DOCUMENT AT ALL</b> — a request composed from
    /// typed objects, which is how every in-process test and every programmatic caller builds one.
    ///
    /// <para><b>It is a positive claim and it must be made deliberately, which is why there is no
    /// default.</b> With no document there are no authored document fields, so the gate's question is
    /// vacuously satisfied — but the callee may not assert that on the caller's behalf. This codebase
    /// already paid for that lesson once: the loop passed an empty unknown-field set "because a typed
    /// caller cannot have unknown fields", which was true of a hand-built request and FALSE of the CLI
    /// that parses documents, and gate 0b passed unconditionally under the loop while the standalone gate
    /// refused the same submission by name.</para>
    ///
    /// <para><b>Named for the claim, not for its emptiness.</b> A caller that parses a document and
    /// reaches for this is visibly saying something false, which is exactly what a grep for it should
    /// surface.</para>
    /// </summary>
    public static DerivationEvidence NoDocument { get; } =
        new(Array.Empty<DerivationRecord>(), Array.Empty<string>());
}
