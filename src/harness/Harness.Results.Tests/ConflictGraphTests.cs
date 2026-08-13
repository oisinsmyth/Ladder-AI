using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// <b>X-G — the packer can mask a genuine multi-writer defect.</b>
///
/// <para>Two blocks that both write the same coil are a conflict, so DB-13 puts them in different tensors
/// and both tests pass. <b>The scheduler has silently repaired a defect that will ship.</b> The fix is
/// reporting, and the thing worth testing is what an EMPTY report is allowed to mean.</para>
/// </summary>
public class ConflictGraphTests
{
    private static ConflictEdge MultiWriter(SignalClass @class = SignalClass.Deliverable) =>
        new("FC_PumpA", "FC_PumpB", ConflictProvenance.MultiWriter, "Pump_Run", @class);

    [Fact]
    public void A_MULTI_WRITER_ON_A_DELIVERABLE_SIGNAL_IS_A_FINDING()
    {
        var graph = new ConflictGraph(new[] { MultiWriter() });

        var finding = Assert.Single(graph.MultiWriterFindings);

        Assert.Equal("Pump_Run", finding.Signal);
        Assert.Contains("both blocks run in the same scan cycle", graph.Render(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_multi_writer_on_HARNESS_INSTRUMENTATION_is_not_one()
    {
        // A conflict on a signal that exists only because the harness instrumented it (D13) is an artefact
        // of testing, not a defect that ships.
        var graph = new ConflictGraph(new[] { MultiWriter(SignalClass.HarnessInstrumentation) });

        Assert.Empty(graph.MultiWriterFindings);
        Assert.True(graph.ProvenanceComplete);
    }

    [Fact]
    public void A_SHARED_MODEL_edge_on_a_deliverable_signal_is_not_a_multi_writer_finding_either()
    {
        var graph = new ConflictGraph(new[]
        {
            new ConflictEdge("FC_UnitA", "FC_UnitB", ConflictProvenance.SharedModel, "Tank_Level", SignalClass.Deliverable),
        });

        Assert.Empty(graph.MultiWriterFindings);
    }

    [Fact]
    public void THE_REPORT_IS_EMITTED_ON_CLEAN_GRAPHS_TOO()
    {
        // The same lesson as F-6's collapse report: a line that appears only on bad news teaches its reader
        // that absence means "not run".
        Assert.Contains("0 multi-writer edges on deliverable signals", ConflictGraph.Empty.Render(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // *** THE HALF WITH TEETH: an unprovenanced graph is NOT a clean one ***
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AN_EDGE_WITH_NO_PROVENANCE_MAKES_THE_WHOLE_GRAPH_UNREPORTABLE()
    {
        var graph = new ConflictGraph(new[]
        {
            MultiWriter(),
            new ConflictEdge("FC_X", "FC_Y", ConflictProvenance.Unstated, "?", SignalClass.Unstated),
        });

        Assert.False(graph.ProvenanceComplete);
        Assert.Contains("NOT CHECKED", graph.Render(), StringComparison.Ordinal);
        Assert.Contains("nothing to do with multi-writers", graph.Render(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_SIGNAL_CLASS_THAT_IS_UNSTATED_IS_ALSO_INCOMPLETE_and_not_only_the_provenance()
    {
        // Both axes decide whether an edge is a finding, so a blank on either is a blank on the answer.
        var graph = new ConflictGraph(new[]
        {
            new ConflictEdge("FC_A", "FC_B", ConflictProvenance.MultiWriter, "Sig", SignalClass.Unstated),
        });

        Assert.False(graph.ProvenanceComplete);
        Assert.Empty(graph.MultiWriterFindings);
    }

    [Fact]
    public void THE_FLAT_BLOCK_LIST_THIS_DESIGN_USED_BEFORE_X_G_NO_LONGER_READS_AS_CLEAN()
    {
        // This is the case the gate exists for. Before X-G the conflict input was a set of block names, and
        // a multi-writer report over it would have been empty — for a reason that has nothing to do with
        // multi-writers.
        var graph = ConflictGraph.WithoutProvenance(new[] { "FC_Other" });

        Assert.False(graph.ProvenanceComplete);
        Assert.Empty(graph.MultiWriterFindings);
        Assert.Contains("NOT CHECKED", graph.Render(), StringComparison.Ordinal);

        // And the packer still gets everything it had.
        Assert.Contains("FC_Other", graph.BlocksForPacking);
    }

    [Fact]
    public void An_EMPTY_graph_and_a_NULL_one_are_different_statements()
    {
        // "The graph ran and found no conflicts" versus "no graph was supplied". The first is a result.
        Assert.True(ConflictGraph.Empty.ProvenanceComplete);
        Assert.Empty(ConflictGraph.Empty.BlocksForPacking);
    }

    [Fact]
    public void BlocksForPacking_gives_the_packer_exactly_what_it_consumed_before()
    {
        var graph = new ConflictGraph(new[] { MultiWriter() });

        Assert.Equal(new[] { "FC_PumpA", "FC_PumpB" }, graph.BlocksForPacking.OrderBy(b => b, StringComparer.Ordinal));
    }

    [Fact]
    public void Both_enums_have_an_UNUSABLE_zero_value()
    {
        Assert.Equal(ConflictProvenance.Unstated, default(ConflictProvenance));
        Assert.Equal(SignalClass.Unstated, default(SignalClass));
    }
}
