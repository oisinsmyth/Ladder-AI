namespace Harness.Results;

/// <summary>
/// <b>Why two things conflict.</b> X-G's provenance — the field whose absence is the whole gap.
/// </summary>
public enum ConflictProvenance
{
    /// <summary>
    /// Nothing recorded why. <b>The zero value, and unusable.</b> An edge with no provenance is
    /// indistinguishable from a multi-writer edge, which is the one kind that must be reported — so a
    /// graph containing one cannot support the X-G report and says so.
    /// </summary>
    Unstated = 0,

    /// <summary>
    /// <b>Both blocks WRITE the same signal.</b> <c>converter cross-check</c> already computes this fact
    /// (C-308) and this design has been consuming it only as a graph edge.
    /// </summary>
    MultiWriter,

    /// <summary>Both tests instance the same model, so running them together would couple them (D28).</summary>
    SharedModel,

    /// <summary>One block calls the other, so testing them concurrently tests neither in isolation (X-E).</summary>
    CallGraph,

    /// <summary>The submission's own blacklist said so (D22). Add-only, and never removable.</summary>
    DeclaredByAuthor,
}

/// <summary>
/// Whether the signal an edge is about SHIPS. <b>The distinction X-G turns on.</b>
/// </summary>
public enum SignalClass
{
    /// <summary>Nothing said. <b>The zero value, and unusable</b> — see <see cref="ConflictProvenance.Unstated"/>.</summary>
    Unstated = 0,

    /// <summary>The signal is part of the deliverable program. A multi-writer here SHIPS.</summary>
    Deliverable,

    /// <summary>The signal exists only because the harness instrumented it (D13). A conflict here is an artefact of testing.</summary>
    HarnessInstrumentation,
}

/// <summary>One conflict edge, with its provenance and the signal it is about.</summary>
public sealed record ConflictEdge(string BlockA, string BlockB, ConflictProvenance Provenance, string Signal, SignalClass Class)
{
    /// <summary>
    /// <b>X-G's finding:</b> two blocks writing the same DELIVERABLE signal. The packer will put them in
    /// different tensors and both tests will pass — <i>and in the deliverable both blocks run in the same
    /// scan cycle.</i>
    /// </summary>
    public bool IsMultiWriterOnADeliverable =>
        Provenance == ConflictProvenance.MultiWriter && Class == SignalClass.Deliverable;

    public bool ProvenanceRecorded =>
        Provenance != ConflictProvenance.Unstated && Class != SignalClass.Unstated;

    public override string ToString() => $"{BlockA} <-> {BlockB} on '{Signal}' ({Provenance}, {Class})";
}

/// <summary>
/// The computed disjointness graph, <b>with X-G's provenance attached to every edge</b>.
///
/// <para><b>The gap this closes, stated as X-G states it:</b> two blocks that both write the same coil are
/// a conflict, so DB-13 puts them in different tensors and both tests pass. <b>THE SCHEDULER HAS SILENTLY
/// REPAIRED A DEFECT THAT WILL SHIP.</b> Separating them is the right thing to do for testing and the wrong
/// thing to do silently — so the edges that arise from multi-writer on a deliverable signal are reported as
/// FINDINGS as well as being used for packing.</para>
///
/// <para><b>The fail-closed half is the one worth having.</b> A report of "no multi-writer findings" reads
/// identically whether the graph looked and found none or was never told what its edges were FOR. So
/// <see cref="ProvenanceComplete"/> is what the gate keys on, and a graph built from a flat list of block
/// names — everything this design consumed before X-G — reports NOT CHECKED rather than a clean bill.</para>
/// </summary>
public sealed record ConflictGraph(IReadOnlyList<ConflictEdge> Edges)
{
    /// <summary>The blocks the packer must keep apart. This is all the design consumed before X-G.</summary>
    public IReadOnlySet<string> BlocksForPacking =>
        Edges.SelectMany(e => new[] { e.BlockA, e.BlockB })
             .Where(b => !string.IsNullOrWhiteSpace(b))
             .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// <b>Every edge whose provenance is recorded.</b> An empty findings list is only meaningful when this
    /// is true of the whole graph.
    /// </summary>
    public bool ProvenanceComplete => Edges.All(e => e.ProvenanceRecorded);

    /// <summary>The edges X-G says must be reported as findings.</summary>
    public IReadOnlyList<ConflictEdge> MultiWriterFindings =>
        Edges.Where(e => e.IsMultiWriterOnADeliverable).ToArray();

    /// <summary>The edges whose provenance nobody recorded — named, so "none found" cannot mean "never looked".</summary>
    public IReadOnlyList<ConflictEdge> WithoutRecordedProvenance =>
        Edges.Where(e => !e.ProvenanceRecorded).ToArray();

    /// <summary>
    /// The graph ran and found no conflicts. <b>A different statement from "no graph was supplied"</b>,
    /// which is a null and reports NOT CHECKED.
    /// </summary>
    public static ConflictGraph Empty { get; } = new(Array.Empty<ConflictEdge>());

    /// <summary>
    /// A graph built from the FLAT LIST of block names this design consumed before X-G.
    ///
    /// <para><b>It is deliberately not silently upgraded.</b> A bare name says two blocks conflict and
    /// nothing about why, so every edge it produces carries <see cref="ConflictProvenance.Unstated"/> and
    /// the X-G gate reports NOT CHECKED. That is the honest reading: the multi-writer report over such a
    /// graph would be empty for the same reason a report over a graph with no multi-writers is empty, and
    /// those two must never look alike.</para>
    /// </summary>
    public static ConflictGraph WithoutProvenance(IEnumerable<string> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        return new ConflictGraph(blocks
            .Select(b => new ConflictEdge(b, "<this submission's targets>", ConflictProvenance.Unstated, "<unrecorded>", SignalClass.Unstated))
            .ToArray());
    }

    /// <summary>The X-G report, emitted on clean graphs too — an absent line reads as a check that passed.</summary>
    public string Render()
    {
        if (!ProvenanceComplete)
        {
            return $"NOT CHECKED — {WithoutRecordedProvenance.Count} of {Edges.Count} edge(s) record no provenance, "
                 + "so a multi-writer report over this graph would be empty for a reason that has nothing to do with multi-writers.";
        }

        var findings = MultiWriterFindings;

        return findings.Count == 0
            ? $"{Edges.Count} edge(s), every provenance recorded, 0 multi-writer edges on deliverable signals."
            : $"{Edges.Count} edge(s), every provenance recorded. *** {findings.Count} MULTI-WRITER FINDING(S) ON DELIVERABLE SIGNALS *** — "
              + string.Join(" | ", findings.Select(f => f.ToString()))
              + ". The packer WILL separate these and both tests may then pass, while in the deliverable both blocks run in the same scan cycle. "
              + "This is a finding about the PROGRAM, not about the submission.";
    }
}
