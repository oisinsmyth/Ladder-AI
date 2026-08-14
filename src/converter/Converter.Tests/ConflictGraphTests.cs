using System.Text.Json;
using Converter.ConflictGraph;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 2026-08-14. `converter conflict-graph` — the SUBMISSION-SCOPED emission
/// `Harness.Results.SubmissionGate` gates 8 and 8c actually consume.
///
/// <para><b>Why it is not `cross-check` with a filter.</b> `cross-check` emits whole-project fact
/// tables keyed on a storage path; the gate consumes EDGES between BLOCKS carrying a provenance and a
/// signal class. An instruction to bridge the two was withdrawn as wrong, and the lane that received
/// it correctly supplied nothing rather than reshaping one into the other.</para>
///
/// <para>*** THE PROPERTY MOST OF THIS FILE IS ABOUT: AN ABSENT GRAPH AND AN EMPTY ONE ARE DIFFERENT
/// DOCUMENTS. *** The consumer reads a missing <c>conflictEdges</c> key as NOT CHECKED and gates, and
/// reads <c>conflictEdges: []</c> as the positive claim that the graph ran and found nothing. Two
/// authors have refused to write that claim unearned; these tests make it impossible for the emitter
/// to write it accidentally.</para>
/// </summary>
public class ConflictGraphTests : IDisposable
{
    private readonly string _dir;

    public ConflictGraphTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"conflictgraph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        // Two PLANT blocks writing one global tag: a genuine cross-block multi-writer that ships.
        WriteBlock("FC_PlantA.ir", Writer("FC", "FC_PlantA", 10, "SharedPlantTag"));
        WriteBlock("FC_PlantB.ir", Writer("FC", "FC_PlantB", 11, "SharedPlantTag"));

        // Two HARNESS blocks (reserved band) writing one global tag: a conflict that is an artefact
        // of testing, not a defect in the deliverable.
        WriteBlock("FC_Harn1.ir", Writer("FC", "FC_Harn1", 9001, "SharedHarnessTag"));
        WriteBlock("FC_Harn2.ir", Writer("FC", "FC_Harn2", 9002, "SharedHarnessTag"));

        // Two OBs: an OB's number is fixed by its event class, so neither can be classified — the
        // signal class is Unstated and the consumer must report NOT CHECKED.
        WriteBlock("OB_X.ir", Writer("OB", "OB_X", 1, "SharedUnknowableTag"));
        WriteBlock("OB_Y.ir", Writer("OB", "OB_Y", 100, "SharedUnknowableTag"));

        // The false-finding shape: two FBs each with their own `IO.Step`. Nothing shared.
        WriteBlock("FB_One.ir", Local("FB_One", 20));
        WriteBlock("FB_Two.ir", Local("FB_Two", 21));
    }

    private static IrBlock Writer(string kind, string name, int number, string tag) =>
        new("0", kind, name, number, "LAD", null, new[]
        {
            new IrNetwork(1, "Write", new[] { new CoilAssignment(tag, new Expr.TagRef("Enable")) }),
        });

    private static IrBlock Local(string name, int number) =>
        new("0", "FB", name, number, "LAD", null, new[]
        {
            new IrNetwork(1, "Own member, twice", new[]
            {
                new CoilAssignment("IO.Step", new Expr.TagRef("Enable")),
                new CoilAssignment("IO.Step", new Expr.TagRef("Enable")),
            }),
        },
            StaticMembers: new[]
            {
                new DbMember("IO", $"\"UDT_{name}\"", Retain: true, StartValue: null, SetPoint: true, NestedMembers: new[]
                {
                    new DbMember("Step", "Int", Retain: false, StartValue: null),
                }),
            });

    private void WriteBlock(string file, IrBlock block)
    {
        var sidecars = block.Networks
            .Select(n => new NetworkSidecar(n.Number, n.Number.ToString(), Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();
        File.WriteAllText(Path.Combine(_dir, file), IrSerializer.SerializeBlock(block, sidecars));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private ConflictGraphReport Run(params string[] signals) =>
        ConflictGraphRunner.Run(_dir, signals, allowUnresolved: false);

    // ---- The shape the gate consumes -------------------------------------------------------------

    [Fact]
    public void AGenuineCrossBlockMultiWriter_BecomesOneProvenancedEdge()
    {
        var report = Run("SharedPlantTag");

        Assert.True(report.Computed);
        var edge = Assert.Single(report.Edges);
        Assert.Equal("FC_PlantA", edge.BlockA);
        Assert.Equal("FC_PlantB", edge.BlockB);
        Assert.Equal(EdgeProvenance.MultiWriter, edge.Provenance);
        Assert.Equal("SharedPlantTag", edge.Signal);
        Assert.Equal(EdgeSignalClass.Deliverable, edge.Class);
    }

    // *** THE FALSE-FINDING CLASS CANNOT BECOME AN EDGE. *** This is the join between the aliasing fix
    // and this emitter: a block-local storage has all its writers inside one block, and one block is
    // not a conflict. Had the emitter been built on the unfixed grouping, `IO.Step` would have put a
    // fictitious FB_One<->FB_Two edge into a submission's conflict graph and separated two slots that
    // never conflicted.
    [Fact]
    public void ABlockLocalPathProducesNoEdge_EvenThoughTwoFbsSpellItTheSame()
    {
        var report = ConflictGraphRunner.Run(_dir, new[] { "FB_One.IO.Step", "FB_Two.IO.Step" }, allowUnresolved: false);

        Assert.True(report.Computed);
        Assert.Empty(report.Edges);
        Assert.All(report.Signals, s => Assert.Equal(SignalResolution.Resolved, s.Resolution));
    }

    // And the unqualified name is REFUSED rather than resolved to one of them — the same discipline
    // one layer up.
    [Fact]
    public void AnAmbiguousLeafName_IsRefusedNotGuessed()
    {
        var report = Run("IO.Step");

        Assert.False(report.Computed);
        var fact = Assert.Single(report.Ambiguous);
        Assert.Equal(
            new[] { "FB_One.IO.Step", "FB_Two.IO.Step" },
            fact.Candidates.OrderBy(c => c, StringComparer.Ordinal));
    }

    // ---- Signal class, derived and never declared ------------------------------------------------

    [Fact]
    public void EveryWriterInTheReservedBand_IsHarnessInstrumentation()
    {
        var edge = Assert.Single(Run("SharedHarnessTag").Edges);
        Assert.Equal(EdgeSignalClass.HarnessInstrumentation, edge.Class);
    }

    // *** AN UNCLASSIFIABLE SIGNAL YIELDS `Unstated`, WHICH FAILS THE GATE CLOSED. *** The refusal is
    // carried across rather than resolved into a guess: an OB's number is fixed by its event class, so
    // neither writer can be placed, and the consumer will report NOT CHECKED.
    [Fact]
    public void WritersThatCannotBeClassified_YieldUnstated_NotAGuess()
    {
        var report = Run("SharedUnknowableTag");

        var edge = Assert.Single(report.Edges);
        Assert.Equal(EdgeSignalClass.Unstated, edge.Class);
        Assert.Single(report.WithoutRecordedProvenance);
    }

    // ---- Absent vs empty: the refusal semantics --------------------------------------------------

    // A signal that resolves and simply has no second writer. The graph RAN, so `conflictEdges: []` is
    // an EARNED claim and the key must be present.
    [Fact]
    public void GraphRanAndFoundNothing_EmitsAnEmptyArray_WhichIsThePositiveClaim()
    {
        var report = ConflictGraphRunner.Run(_dir, new[] { "FB_One.IO.Step" }, allowUnresolved: false);
        Assert.True(report.Computed);
        Assert.Empty(report.Edges);

        using var doc = JsonDocument.Parse(ConflictGraphOutputFormatter.FormatJson(report));
        Assert.True(doc.RootElement.TryGetProperty("conflictEdges", out var edges));
        Assert.Equal(JsonValueKind.Array, edges.ValueKind);
        Assert.Equal(0, edges.GetArrayLength());
    }

    // *** THE KEY IS OMITTED, NOT WRITTEN AS `[]` AND NOT WRITTEN AS `null`. *** A null would let a
    // lenient deserializer round it to the empty list and restore the false claim one layer down —
    // the "tidy representation re-created the failure one layer down" shape, designed out.
    [Fact]
    public void GraphDidNotRun_OmitsTheKeyEntirely()
    {
        var report = Run("NoSuchSignalAnywhere");
        Assert.False(report.Computed);

        using var doc = JsonDocument.Parse(ConflictGraphOutputFormatter.FormatJson(report));
        Assert.False(doc.RootElement.TryGetProperty("conflictEdges", out _));
        Assert.True(doc.RootElement.TryGetProperty("notComputed", out _));
    }

    [Fact]
    public void NoSignalsSupplied_IsNotComputed_BecauseNothingWasExamined()
    {
        var report = ConflictGraphRunner.Run(_dir, Array.Empty<string>(), allowUnresolved: false);

        Assert.False(report.Computed);
        Assert.Contains("nothing was examined", report.NotComputedReason);
    }

    // A file that did not parse can hold the SECOND writer that makes a signal a conflict, so a graph
    // built over it cannot honestly say "no conflicts".
    [Fact]
    public void APartialCorpus_WithholdsTheGraphRatherThanClaimingItIsClean()
    {
        File.WriteAllText(Path.Combine(_dir, "broken.ir"), "BLOCK FC FC_Broken\nnot IR at all\n");

        var report = Run("SharedPlantTag");

        Assert.False(report.Computed);
        Assert.NotEmpty(report.Warnings);
        Assert.Contains("PARTIAL", report.NotComputedReason);

        using var doc = JsonDocument.Parse(ConflictGraphOutputFormatter.FormatJson(report));
        Assert.False(doc.RootElement.TryGetProperty("conflictEdges", out _));
    }

    // The named escape exists, and it does NOT become the default — the unresolved signals are still
    // reported in full beside the edges it does emit.
    [Fact]
    public void AllowUnresolved_EmitsWhatResolved_AndStillReportsTheGap()
    {
        var report = ConflictGraphRunner.Run(_dir, new[] { "SharedPlantTag", "NoSuchSignal" }, allowUnresolved: true);

        Assert.True(report.Computed);
        Assert.Single(report.Edges);
        Assert.Single(report.Unresolved);
    }

    // ---- What is deliberately NOT emitted --------------------------------------------------------

    // *** `computedConflicts` MUST NEVER BE EMITTED. *** A bare block name there becomes an edge with
    // `Unstated` provenance, and the consumer's ProvenanceComplete is ALL-or-nothing — so emitting the
    // weaker field beside the stronger one would turn gate 8c to NOT CHECKED for the whole submission.
    // Gate 8's packing set is derived from the edges, so it is fully served without it.
    [Fact]
    public void ComputedConflictsIsNeverEmitted()
    {
        using var doc = JsonDocument.Parse(ConflictGraphOutputFormatter.FormatJson(Run("SharedPlantTag")));

        Assert.False(doc.RootElement.TryGetProperty("computedConflicts", out _));
    }

    // Only MultiWriter. A CallGraph edge is about no signal, so it could only carry an Unstated class,
    // and one of those disables gate 8c for everything — an added edge would silently switch off the
    // report it was added beside.
    [Fact]
    public void OnlyMultiWriterProvenanceIsEverEmitted()
    {
        var report = ConflictGraphRunner.Run(
            _dir,
            new[] { "SharedPlantTag", "SharedHarnessTag", "SharedUnknowableTag" },
            allowUnresolved: true);

        Assert.NotEmpty(report.Edges);
        Assert.All(report.Edges, e => Assert.Equal(EdgeProvenance.MultiWriter, e.Provenance));
    }

    // The emitted edge object carries exactly the five fields ConflictEdgeDocument deserializes, with
    // `class` spelled as the consumer spells it. Read off the CONSUMER, never off what we happen to
    // emit — a fixture authored by reading our own output is a mirror, not a fixture.
    [Fact]
    public void AnEmittedEdgeCarriesTheConsumersOwnFieldNames()
    {
        using var doc = JsonDocument.Parse(ConflictGraphOutputFormatter.FormatJson(Run("SharedPlantTag")));
        var edge = doc.RootElement.GetProperty("conflictEdges")[0];

        foreach (var field in new[] { "blockA", "blockB", "provenance", "signal", "class" })
        {
            Assert.True(edge.TryGetProperty(field, out _), $"emitted edge is missing '{field}'");
        }

        Assert.Equal("MultiWriter", edge.GetProperty("provenance").GetString());
        Assert.Equal("Deliverable", edge.GetProperty("class").GetString());
    }

    // ---- Reading the submission's signal set ------------------------------------------------------

    [Fact]
    public void SignalsFromSubmission_TakesInputsKeysAndExpectationSignals()
    {
        var path = Path.Combine(_dir, "submission.json");
        File.WriteAllText(path, """
        {
          "vectors": [
            { "inputs": { "Stim_A": "1", "Stim_B": "2" },
              "expectations": [ { "signal": "SharedPlantTag" } ] }
          ]
        }
        """);

        var signals = ConflictGraphRunner.SignalsFromSubmission(path);

        Assert.Equal(new[] { "SharedPlantTag", "Stim_A", "Stim_B" }, signals.OrderBy(s => s, StringComparer.Ordinal));
    }

    // Tolerant by design: this reads ANOTHER LANE'S evolving schema, so a document with no vectors is
    // an empty signal set rather than a throw — and an empty set is NOT COMPUTED, not a clean pass.
    [Fact]
    public void SignalsFromSubmission_ADocumentWithNoVectors_IsEmptyNotAThrow()
    {
        var path = Path.Combine(_dir, "empty.json");
        File.WriteAllText(path, "{ \"model\": {} }");

        Assert.Empty(ConflictGraphRunner.SignalsFromSubmission(path));
    }
}
