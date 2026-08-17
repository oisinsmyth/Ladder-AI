namespace Converter.ConflictGraph;

/// <summary>
/// *** THESE NAMES ARE NOT OURS. *** They mirror `Harness.Results.ConflictProvenance` exactly,
/// because the emitted JSON is deserialized straight into that enum by `SubmissionDocument`. Matching
/// the consumer's shape is the whole discipline here: a shape the harness would have to be changed to
/// accept is a shape that does not work.
/// </summary>
public enum EdgeProvenance
{
    /// <summary>
    /// Nothing recorded why. <b>The zero value, and unusable</b> — a graph containing one edge with
    /// it makes gate 8c NOT CHECKED for the WHOLE submission. This emitter never writes it.
    /// </summary>
    Unstated = 0,

    /// <summary>Both blocks WRITE the same signal. <b>The only kind this tool can derive.</b></summary>
    MultiWriter,

    /// <summary>Both tests instance the same model. A property of the submission, not of the program.</summary>
    SharedModel,

    /// <summary>One block calls the other. Derivable here, and deliberately NOT emitted — see ConflictGraphRunner.</summary>
    CallGraph,

    /// <summary>The submission's own blacklist said so. Add-only, and never this tool's to state.</summary>
    DeclaredByAuthor,
}

/// <summary>Whether the signal an edge is about SHIPS. Mirrors `Harness.Results.SignalClass`.</summary>
public enum EdgeSignalClass
{
    /// <summary>
    /// Nothing said. <b>The zero value, and it fails the gate closed</b> — which is exactly what an
    /// unclassifiable signal should do. Emitted deliberately rather than guessed at.
    /// </summary>
    Unstated = 0,

    /// <summary>Part of the deliverable program. A multi-writer here SHIPS.</summary>
    Deliverable,

    /// <summary>Exists only because the harness instrumented it. A conflict here is an artefact of testing.</summary>
    HarnessInstrumentation,
}

/// <summary>One conflict edge, in the exact shape `ConflictEdgeDocument` deserializes.</summary>
public sealed record ConflictEdgeFact(
    string BlockA,
    string BlockB,
    EdgeProvenance Provenance,
    string Signal,
    EdgeSignalClass Class,
    // Ours, not the harness's — extra properties are ignored by the consumer's deserializer, and this
    // is what lets a reader check the derivation instead of taking the edge on trust.
    string StoragePath,
    string Derivation);

/// <summary>How one supplied signal name resolved against the project's storage paths.</summary>
public enum SignalResolution
{
    /// <summary>Exactly one storage location. The only state that can produce an edge.</summary>
    Resolved,

    /// <summary>
    /// No storage path matched. <b>Reported, never dropped</b> — "no conflict found for X" and "X was
    /// never looked for" are different facts, and an emitter that silently drops the second is
    /// claiming the first.
    /// </summary>
    Unresolved,

    /// <summary>
    /// *** MORE THAN ONE DISTINCT STORAGE MATCHED, SO IT IS REFUSED. *** Resolving a name to one of
    /// several candidates is precisely the aliasing that made `cross-check` invent multi-writers;
    /// doing it here would put the same fiction into a submission's conflict graph. Instance aliases
    /// of ONE storage are collapsed first, so this fires only on genuinely different storage.
    /// </summary>
    Ambiguous,

    /// <summary>
    /// *** THE SUBMISSION DECLARED IT `harnessOnly`: A POSITIVE CLAIM THAT IT OCCUPIES NO PLC STORAGE. ***
    /// No conflict edge is possible for it, and that is a COMPUTED FACT rather than a gap — so it does
    /// NOT count against the scope, and a submission consisting entirely of mirror-side names is
    /// legitimately computable.
    ///
    /// <para>This state exists because the tooling names an ambiguity it cannot settle: an unresolved
    /// signal means <i>"this may be a mirror-only signal, or the name may be wrong"</i> — two entirely
    /// different repairs behind one silence. The claim is the author's to make.</para>
    /// </summary>
    HarnessOnly,

    /// <summary>
    /// The declaration contradicts itself — the signal is in BOTH <c>storage</c> and
    /// <c>harnessOnly</c>. <b>Refused, and the named escape does not cover it</b>:
    /// <c>--allow-unresolved</c> means "accept that some names were not looked at", never "accept a
    /// document that answers one question twice, differently".
    /// </summary>
    Refused,
}

/// <param name="Join">
/// WHICH JOIN CARRIED THE NAME. Reported on every line, resolved or not — <b>a resolution that cannot
/// be explained is what let a tool read 70 of 70 signals as unresolved while the field that joined them
/// sat in the same document, unread.</b>
/// </param>
public sealed record SignalResolutionFact(
    string Signal,
    SignalResolution Resolution,
    IReadOnlyList<string> Candidates,
    string Reason,
    SignalJoinKind Join = SignalJoinKind.ProjectPathMatch);

/// <summary>
/// The emission, and *** ITS CENTRAL PROPERTY IS THAT AN ABSENT GRAPH AND AN EMPTY ONE ARE DIFFERENT
/// DOCUMENTS. ***
///
/// <para>The consumer treats a missing <c>conflictEdges</c> key as NOT CHECKED and gates; it treats
/// <c>conflictEdges: []</c> as <b>the positive claim that the graph ran and found nothing</b>. Two
/// authors have already refused to write that claim unearned, so this type must keep the refusal
/// expressible: when <see cref="Computed"/> is false the formatter OMITS the key entirely rather than
/// writing an empty array.</para>
/// </summary>
public sealed record ConflictGraphReport(
    bool Computed,
    string NotComputedReason,
    IReadOnlyList<ConflictEdgeFact> Edges,
    IReadOnlyList<SignalResolutionFact> Signals,
    IReadOnlyList<string> Warnings,
    string CorpusDescription)
{
    public IReadOnlyList<SignalResolutionFact> Unresolved =>
        Signals.Where(s => s.Resolution == SignalResolution.Unresolved).ToList();

    public IReadOnlyList<SignalResolutionFact> Ambiguous =>
        Signals.Where(s => s.Resolution == SignalResolution.Ambiguous).ToList();

    /// <summary>
    /// Signals the submission positively claimed occupy no PLC storage. <b>Counted separately from both
    /// the resolved and the unresolved</b> — folding them into either would turn a computed fact into
    /// either an edge search that happened or a gap that did not exist.
    /// </summary>
    public IReadOnlyList<SignalResolutionFact> HarnessOnly =>
        Signals.Where(s => s.Resolution == SignalResolution.HarnessOnly).ToList();

    /// <summary>Signals whose declaration contradicts itself. Never escapable by <c>--allow-unresolved</c>.</summary>
    public IReadOnlyList<SignalResolutionFact> RefusedSignals =>
        Signals.Where(s => s.Resolution == SignalResolution.Refused).ToList();

    /// <summary>
    /// Signals nothing looked at — the count an empty edge list would otherwise hide.
    /// <see cref="SignalResolution.HarnessOnly"/> is NOT here: it was looked at, and the answer was
    /// "no edge is possible".
    /// </summary>
    public IReadOnlyList<SignalResolutionFact> Unjudged =>
        Signals.Where(s => s.Resolution is not (SignalResolution.Resolved or SignalResolution.HarnessOnly)).ToList();

    /// <summary>
    /// Edges the consumer's own `ProvenanceRecorded` will reject. Surfaced HERE as well, because at the
    /// gate it appears as a flat NOT CHECKED for the whole submission with nothing naming the cause —
    /// and the operator who can fix it is the one running this command.
    /// </summary>
    public IReadOnlyList<ConflictEdgeFact> WithoutRecordedProvenance =>
        Edges.Where(e => e.Provenance == EdgeProvenance.Unstated || e.Class == EdgeSignalClass.Unstated).ToList();
}
