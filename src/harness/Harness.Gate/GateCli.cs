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
/// The runnable gate. <c>harness-gate check &lt;submission.json&gt;</c>.
///
/// <para><b>Split from <c>Program</c> so it is testable</b> — the CLI's own behaviour (what it refuses,
/// what it exits) is a decision like any other, and a decision only reachable through a process is a
/// decision nobody tests.</para>
/// </summary>
public static class GateCli
{
    public static int Run(IReadOnlyList<string> args, TextWriter output, Func<string, string> readFile)
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

        SubmissionDocument document;
        SubmissionReport report;
        try
        {
            document = SubmissionDocument.Read(readFile(args[1]));

            // 🔴 *** --binding IS WHAT MAKES THIS CLI AS STRONG AS THE LOOP. *** Without it the
            // observability map comes out of the submission — the vector author declaring what the copy
            // layer provides — and gate 5 reports NOT CHECKED rather than passing. Measured: the CLI took
            // its map from the submission while the loop took it from the coordinator's bindings, so the
            // standalone tool was WEAKER than the loop in exactly the place it decides whether to proceed,
            // and it is consulted FIRST.
            var bindingIndex = args.ToList().IndexOf("--binding");
            var binding = bindingIndex >= 0 && bindingIndex + 1 < args.Count
                ? BindingDocument.Read(readFile(args[bindingIndex + 1]))
                : null;

            // Evaluate is INSIDE the try: a document that parses as JSON and then names a mode nothing
            // implements is still a document that could not be read, and it must reach the same
            // NOTHING EXAMINED outcome rather than escaping as an unhandled throw.
            report = Evaluate(document, readFile, binding);
        }
        catch (Exception ex)
        {
            output.WriteLine($"NOTHING EXAMINED — could not read '{args[1]}': {ex.GetType().Name}: {ex.Message}");
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
    public static SubmissionReport Evaluate(SubmissionDocument document, Func<string, string>? readFile = null, BindingDocument? binding = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var vectors = (document.Vectors ?? new List<VectorDocument>()).Select(ToSubmissionVector).ToArray();

        var enumeration = ToEnumeration(document);

        var fidelity = ToFidelity(document);

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
            map = MirrorObservability.FromMinimalCopyLayer(
                (binding.Slots ?? new List<SlotBindingDocument>())
                    .SelectMany(sl => sl.ResultSources ?? new List<MirroredSignalDocument>())
                    .Select(r => r.Tag ?? string.Empty))
                with
                {
                    Provenance = MapProvenance.Bindings,
                };
        }

        // *** THE FLOOR IS COMPUTED FROM SECTION 12a, NEVER CARRIED HERE. *** It scales with the number
        // of slots sharing the poll and with slots-per-read, so it is a property of the WAVE SET.
        var slotsPerRead = Math.Max(1, ModbusLimitsProxy.MaxReadRegisters / Math.Max(1, document.ResultRegistersPerSlot));
        var readsPerCycle = (int)Math.Ceiling(Math.Max(1, document.SlotsInWaveSet) / (double)slotsPerRead);
        var floor = WireTiming.ObservabilityFloorScans(readsPerCycle);

        return SubmissionGate.Check(
            vectors,
            enumeration,
            fidelity,
            new AgentIdentity(document.BlockAuthor ?? string.Empty),
            map,
            floor,
            Math.Max(1, document.RuntimeCompression),
            ToConflicts(document),
            ToCompressionInputs(document),
            ToDeploymentDeclaration(document),
            ToTagMapReach(document, readFile));
    }

    /// <summary>The enumeration projection, off the document. Public so the runner composes it the same way.</summary>
    public static AssertionEnumeration ToEnumeration(SubmissionDocument document) =>
        AssertionEnumeration.Of(
            document.Enumeration?.Clauses ?? Enumerable.Empty<string>(),
            document.Enumeration?.Assertions ?? Enumerable.Empty<string>(),
            document.Enumeration?.Forms,
            document.Enumeration?.Enumerator ?? string.Empty,
            document.Enumeration?.NormalisedTexts,
            document.Enumeration?.RequiredObservations?.ToDictionary(
                e => e.Key,
                e => (IReadOnlySet<string>)e.Value.ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal),
            document.Enumeration?.Bounds);

    /// <summary>The model's fidelity declaration, off the document.</summary>
    public static FidelityDeclaration? ToFidelity(SubmissionDocument document) =>
        document.Model is null
            ? null
            : FidelityDeclaration.Of(document.Model.Id ?? string.Empty,
                document.Model.Represents ?? Enumerable.Empty<string>(),
                document.Model.DoesNotRepresent,
                document.Model.ValidatedAgainstPlantData);

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
    private static BlockCompressionInputs? ToCompressionInputs(SubmissionDocument document)
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
            : new Basis(v.Clause ?? string.Empty, v.Assertion ?? string.Empty),
        v.Inputs ?? new Dictionary<string, string>(),
        v.StartBool ?? string.Empty,
        (v.Expectations ?? new List<ExpectationDocument>())
            .Select(e => new ObservabilityDeclaration(e.Signal ?? string.Empty, e.Nature, e.Mode, e.WindowScans, e.Expected))
            .ToArray(),
        v.AssertionForm,
        string.IsNullOrWhiteSpace(v.SettlingCondition)
            ? null
            : new SettlingDeclaration(v.SettlingCondition, v.SettlingSignals ?? new List<string>()),
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

        output.WriteLine("GATES");
        foreach (var gate in report.Gates)
        {
            var status = gate.Status switch
            {
                GateStatus.Checked => gate.Passed ? "CHECKED   " : "REFUSED   ",
                GateStatus.Judgement => "JUDGEMENT ",
                _ => "NOT CHECKED",
            };

            output.WriteLine($"  [{status}] {gate.Gate}  (by {gate.Verifier})");
            output.WriteLine($"      {gate.Detail}");
        }

        if (report.NotChecked.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("NOT CHECKED — what is missing (this is the build list)");
            foreach (var gate in report.NotChecked)
                output.WriteLine($"  - {gate.Gate}: needs {gate.Verifier}");
        }

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
