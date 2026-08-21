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
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
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
public sealed record DerivationRecord(
    string Field,
    string Producer,
    string Artifact,
    string DeclaredSha256,
    string? ObservedSha256)
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
