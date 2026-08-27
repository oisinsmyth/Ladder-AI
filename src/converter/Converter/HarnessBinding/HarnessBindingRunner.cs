using Converter.CrossCheck;
using Converter.Ir;
using Converter.ServedArea;
using Converter.SignalInventory;

namespace Converter.HarnessBinding;

/// <summary>
/// THE MECHANICALLY-DERIVABLE HALF OF A HARNESS BINDING DOCUMENT, EMITTED INSTEAD OF TRANSCRIBED.
///
/// <para>Why it exists (2026-08-27): the binding document is hand-typed — the committed
/// <c>gen/test-project001/hopper-blockage-alarm/harness-binding.json</c> is 297 lines for ONE slot of
/// 20 signals — and a transcription is the one step in this pipeline with no mechanical check behind
/// it. <c>signal-set</c> landed the same day and states a block's signals; this turns that statement
/// into the document the harness actually loads, and stops at the exact line where the corpus stops
/// knowing.</para>
///
/// <para><b>IT IS A SCAFFOLD, NOT AN AUTHOR.</b> Four fields in that document are claims about the
/// PLANT or about a SPECIFICATION — <c>specName</c> (a spec↔code name translation), <c>encoding</c> (a
/// transcription of prose tables, carrying a required citation), <c>inertRest</c> (what the plant reads
/// at rest) and <c>startCondition</c> (where <b>null is the positive claim "no start gate"</b>, not an
/// absence). None of them is emitted, none of them is defaulted, and each is named in
/// <c>unresolvedHoles</c> — a key gate 0b REFUSES, so an unfinished scaffold cannot reach a run.</para>
///
/// <para><b>DELIBERATELY NOT A SECOND ANALYSIS.</b> The direction, the writer/reader sites and the
/// served window are read from <c>ProjectUsageGraph</c>, <c>SignalInventory</c> and
/// <c>ServedAreaRunner</c> — the same producers <c>signal-set</c>, <c>candidate-scan</c> and
/// <c>served-area</c> answer from. A second graph walk would give the project two answers to "does
/// this block write this member" that could disagree.</para>
/// </summary>
public static class HarnessBindingRunner
{
    // IR type -> the mirror's own vocabulary, and its width in HOLDING REGISTERS. Not a guess: the
    // table is `Harness.Map.MirrorValueType` and `MirrorElements`, where a Bool occupies a whole
    // register (packing is an explicit non-goal, so the client's index arithmetic stays true) and a
    // Time is 32 bits across TWO registers. Anything absent from this table is REFUSED by name rather
    // than mapped to the nearest thing — the hard-coded "Int" that reached a controller and came back
    // `Data type Bool is not permitted here` is the reason that table exists at all.
    private static readonly IReadOnlyDictionary<string, (string Mirror, int Width)> MirrorTypes =
        new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bool"] = ("Bool", 1),
            ["Int"] = ("Int", 1),
            ["Time"] = ("Time", 2),
        };

    public static HarnessBindingReport Run(
        string projectDir,
        string stimulusBlock,
        IReadOnlyList<string> observedBlocks,
        IReadOnlyList<string> scopes,
        string slotId,
        string? stimulusInstance)
    {
        ArgumentNullException.ThrowIfNull(observedBlocks);
        ArgumentNullException.ThrowIfNull(scopes);

        var graph = ProjectUsageGraph.Build(projectDir);
        var inventory = SignalInventory.SignalInventory.Build(projectDir);
        var warnings = inventory.Warnings.Concat(graph.Warnings).ToList();
        var refusals = new List<BindingRefusal>();

        // Resolve every named block against the corpus BEFORE deriving anything. A scaffold that
        // answers a question about a block which is not there emits a document with fewer rows, and
        // "fewer rows" is indistinguishable from "that block has fewer signals" once it is on disk.
        foreach (var name in new[] { stimulusBlock }.Concat(observedBlocks))
        {
            if (!graph.BlockNames.Contains(name))
            {
                refusals.Add(new BindingRefusal(name,
                    $"no block named '{name}' is in this corpus ({inventory.FilesScanned} file(s) scanned). "
                    + "A typo, a block not written yet, or the wrong --project. RESOLVED BY: the caller, "
                    + "by naming a block `converter signal-set --project " + projectDir + "` can find."));
            }
        }

        if (refusals.Count > 0)
        {
            return Empty(projectDir, inventory.FilesScanned, stimulusBlock, stimulusInstance ?? string.Empty,
                observedBlocks, scopes, slotId, refusals, warnings, BindingScope.UnknownBlock);
        }

        // The instance the harness addresses. Binding tags are ABSOLUTE on a placement
        // (`iDB_HopperBlockageStim.Stim.Profile`), never bare, so a block with no placement has no
        // address to bind and a block with two has no ONE address — and picking either would be a
        // claim about which physical unit is on the rig.
        var placements = PlacementsOf(graph, stimulusBlock);
        var instance = stimulusInstance;

        if (instance is null)
        {
            if (placements.Count == 0)
            {
                refusals.Add(new BindingRefusal(stimulusBlock,
                    $"'{stimulusBlock}' has no instance DB and no multi-instance placement in this corpus, so "
                    + "its members have no absolute address for a binding to name. A binding tag is always "
                    + "`<placement>.<member>`. RESOLVED BY: the engineer, who creates the instance (hard rule 3 "
                    + "— this tool will not invent one), or the caller with --instance <iDB> once it exists."));
            }
            else if (placements.Count > 1)
            {
                refusals.Add(new BindingRefusal(stimulusBlock,
                    $"'{stimulusBlock}' has {placements.Count} placements — {string.Join(", ", placements)} — and "
                    + "this binding can address exactly one. WHICH ONE IS ON THE RIG IS A FACT ABOUT THE PLANT, "
                    + "not about the corpus, and choosing here would decide it silently. RESOLVED BY: the caller, "
                    + "with --instance <iDB>."));
            }
            else
            {
                instance = placements[0];
            }
        }
        else if (!placements.Contains(instance, StringComparer.Ordinal))
        {
            refusals.Add(new BindingRefusal(instance,
                $"--instance '{instance}' is not a placement of '{stimulusBlock}' in this corpus. "
                + $"Its placements are: {(placements.Count == 0 ? "(none)" : string.Join(", ", placements))}. "
                + "RESOLVED BY: the caller."));
        }

        if (refusals.Count > 0)
        {
            return Empty(projectDir, inventory.FilesScanned, stimulusBlock, instance ?? string.Empty,
                observedBlocks, scopes, slotId, refusals, warnings, BindingScope.Derived);
        }

        var targets = new List<DerivedSignal>();
        var sources = new List<DerivedSignal>();
        var excluded = new List<ExcludedSignal>();

        Collect(graph, inventory, stimulusBlock, instance!, BindingRole.Stimulus, scopes,
            targets, sources, excluded, refusals);

        foreach (var observed in observedBlocks)
        {
            var observedPlacements = PlacementsOf(graph, observed);

            if (observedPlacements.Count != 1)
            {
                refusals.Add(new BindingRefusal(observed,
                    $"'{observed}' has {observedPlacements.Count} placement(s) "
                    + $"({(observedPlacements.Count == 0 ? "none" : string.Join(", ", observedPlacements))}), and an "
                    + "observation still needs ONE absolute address to read from. RESOLVED BY: the engineer (create "
                    + "the instance) or the caller (observe the placement, not the type)."));
                continue;
            }

            Collect(graph, inventory, observed, observedPlacements[0], BindingRole.Observed, scopes,
                targets, sources, excluded, refusals);
        }

        // The served window, from the SAME producer `converter served-area` answers from — so a
        // binding and the tool that checks it cannot disagree about the area they describe. NOT
        // DERIVED is carried through as null, never as a number: exit 2 there is never a pass.
        var served = ServedAreaRunner.Run(new[] { projectDir });

        var scope = targets.Count == 0 && sources.Count == 0
            ? (scopes.Count > 0 ? BindingScope.NothingInScope : BindingScope.Derived)
            : BindingScope.Derived;

        var holes = Holes(slotId, targets, sources, served);

        return new HarnessBindingReport(
            projectDir, inventory.FilesScanned, stimulusBlock, instance!, observedBlocks, scopes, slotId,
            Order(targets), Order(sources), excluded, refusals, holes, warnings,
            served.Derived ? served.BaseByte : null,
            served.Derived ? served.Registers : null,
            served.Denominator,
            scope);
    }

    private static IReadOnlyList<DerivedSignal> Order(List<DerivedSignal> rows) =>
        rows.OrderBy(r => r.Tag, StringComparer.Ordinal).ToList();

    // Every placement of a block, in BOTH forms — an instance DB of its own and a multi-instance
    // static inside another FB (FI-50). Under C-132 a whole corpus can consist of nothing but the
    // second, so resolving only the first reports "no placement" for a block that has one.
    private static List<string> PlacementsOf(ProjectUsageGraph graph, string block) =>
        graph.InstanceToFb.Concat(graph.MultiInstanceToFb)
            .Where(kv => string.Equals(kv.Value, block, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

    private static void Collect(
        ProjectUsageGraph graph,
        SignalInventory.SignalInventory inventory,
        string block,
        string instance,
        BindingRole role,
        IReadOnlyList<string> scopes,
        List<DerivedSignal> targets,
        List<DerivedSignal> sources,
        List<ExcludedSignal> excluded,
        List<BindingRefusal> refusals)
    {
        var prefix = block + ".";

        foreach (var leaf in inventory.Leaves
                     .Where(l => l.Origin == SignalOrigin.FbInterface)
                     .Where(l => l.Path.StartsWith(prefix, StringComparison.Ordinal))
                     .OrderBy(l => l.Path, StringComparer.Ordinal))
        {
            var member = leaf.Path[prefix.Length..];
            var tag = instance + "." + member;

            // Matched against the INSTANCE-QUALIFIED TAG, not the bare member, because that is what
            // the binding carries and because the same member name means different things on
            // different blocks — `--scope IO.` would take one block's public face and another's by
            // accident. One rule, no fallback: `--scope iDB_X.IO.` says exactly what it says.
            if (scopes.Count > 0 && !scopes.Any(s => tag.StartsWith(s, StringComparison.Ordinal)))
            {
                excluded.Add(new ExcludedSignal(tag, leaf.Type, "-", ExclusionReason.OutOfScope));
                continue;
            }

            var (writers, readers) = InterfaceUsages(graph, block, member, instance);
            var writes = writers.Any(w => string.Equals(w.Block, block, StringComparison.Ordinal));
            var reads = readers.Any(r => string.Equals(r.Block, block, StringComparison.Ordinal));

            var direction = (writes, reads) switch
            {
                (true, true) => "both",
                (true, false) => "written",
                (false, true) => "read",
                _ => "unused",
            };

            if (direction == "unused")
            {
                excluded.Add(new ExcludedSignal(tag, leaf.Type, direction, ExclusionReason.Unused));
                continue;
            }

            // 🔴 THE PARTITION, AND ITS ONE HARD RULE. `read` on the STIMULUS head is the only shape
            // that becomes something the harness DRIVES. Everything else is observed, because
            // observing is non-destructive and driving is not:
            //
            //  - `both` is NEVER a vector target, whatever the role. The block writes the member
            //    itself, so a harness write contends with its own coil — and `writers` below is the
            //    evidence. It is offered as an OBSERVATION instead, which costs nothing.
            //  - a `read` member of an OBSERVED block is driven by the STIMULUS MODEL, not by the
            //    harness. Driving it would have the harness testing its own arithmetic.
            var kind = (role, direction) switch
            {
                (BindingRole.Stimulus, "read") => "target",
                (BindingRole.Stimulus, "both") => "source",
                (BindingRole.Stimulus, "written") => "source",
                (BindingRole.Observed, "read") => "excluded-input",
                _ => "source",
            };

            if (kind == "excluded-input")
            {
                excluded.Add(new ExcludedSignal(tag, leaf.Type, direction, ExclusionReason.ObservedInput));
                continue;
            }

            if (!MirrorTypes.TryGetValue(leaf.Type, out var mirror))
            {
                refusals.Add(new BindingRefusal(tag,
                    $"IR type '{leaf.Type}' has no mirror element, so this signal cannot be given a register "
                    + $"width. The mirror carries {string.Join(", ", MirrorTypes.Keys)} and nothing else. "
                    + "NAMING THE NEAREST TYPE IS THE DEFECT THIS REFUSES: a hard-coded `Int` reached a "
                    + "controller and TIA answered `Data type Bool is not permitted here`, after a full import. "
                    + "RESOLVED BY: widening Harness.Map.MirrorElements with a measurement behind it, or by the "
                    + "caller excluding this member with --scope."));
                continue;
            }

            var (latchedBy, evidence) = Latch(block, writers);

            var signal = new DerivedSignal(tag, mirror.Mirror, leaf.Type, mirror.Width, latchedBy, evidence,
                Sites(writers), Sites(readers));

            if (kind == "target")
            {
                // Recorded on the row it did NOT become, so the exclusion is visible beside the
                // inclusion rather than only in a total.
                targets.Add(signal);
            }
            else
            {
                if (role == BindingRole.Stimulus && direction == "both")
                {
                    excluded.Add(new ExcludedSignal(tag, leaf.Type, direction, ExclusionReason.ReadWriteContention));
                }

                sources.Add(signal);
            }
        }
    }

    // 🔴 `latchedBy` IS DERIVABLE, AND THE COMMITTED ARTIFACT RECORDS A HUMAN DERIVING IT BY HAND:
    // "the latch provenance was in the IR all along ... a whole-corpus search finds no other writer".
    // That search is this method. Two conditions, both required:
    //
    //   (1) a SET or RESET coil somewhere in the writer set — a plain COIL is not a latch however it
    //       is sealed, and `IO.HopperBlockedAlarm` is exactly that case: it reads itself in its own
    //       rung and the committed binding correctly claims no latch for it;
    //   (2) EVERY writer in ONE block — the field takes a BLOCK NAME because a name is provenance
    //       checkable against the deployed object set, and two blocks latching one member is not a
    //       provenance, it is a defect for cross-check to report.
    //
    // Absent is itself the claim "this binding claims no latch", so it is never written speculatively.
    private static (string? LatchedBy, string Evidence) Latch(
        string block,
        IReadOnlyList<ProjectUsageGraph.UsageSite> writers)
    {
        if (writers.Count == 0)
        {
            return (null, "no writer in this corpus");
        }

        var blocks = writers.Select(w => w.Block).Distinct(StringComparer.Ordinal).OrderBy(b => b, StringComparer.Ordinal).ToList();
        var latching = writers.Where(w => w.Kind is CoilKind.Set or CoilKind.Reset).ToList();

        if (latching.Count == 0)
        {
            return (null, $"written by {string.Join(", ", Sites(writers))}, all plain coils — not a latch");
        }

        if (blocks.Count > 1)
        {
            return (null,
                $"SET/RESET coils in {blocks.Count} blocks ({string.Join(", ", blocks)}), so no ONE block is the "
                + "provenance. NOT DERIVED — this is a cross-check finding, not a binding decision.");
        }

        return (blocks[0],
            $"{string.Join(", ", latching.Select(w => $"{w.Block} N{w.Network} {w.Kind.ToString()!.ToUpperInvariant()}COIL"))}"
            + "; no other writer in this corpus");
    }

    // An interface member is addressed TWO ways and both have to be resolved — the repair
    // undriven-scan needed on 2026-08-18, where looking up one form only made 136 of 228 rows false.
    // From INSIDE the block the member is bare and local; from every other block it is absolute on
    // the placement. The placement is the FLOOR on the ancestor walk: `CALL FB(iDB, ...)` records a
    // write at the bare instance path, which is an ancestor of every member in it.
    private static (IReadOnlyList<ProjectUsageGraph.UsageSite> Writers, IReadOnlyList<ProjectUsageGraph.UsageSite> Readers)
        InterfaceUsages(ProjectUsageGraph graph, string block, string member, string instance)
    {
        var writers = new List<ProjectUsageGraph.UsageSite>();
        var readers = new List<ProjectUsageGraph.UsageSite>();

        var local = graph.UsagesReaching(member);
        writers.AddRange(local.Writers.Where(w => string.Equals(w.Block, block, StringComparison.Ordinal)));
        readers.AddRange(local.Readers.Where(r => string.Equals(r.Block, block, StringComparison.Ordinal)));

        var absolute = graph.UsagesReaching(instance + "." + member, notAbove: instance);
        writers.AddRange(absolute.Writers);
        readers.AddRange(absolute.Readers);

        return (writers, readers);
    }

    private static IReadOnlyList<string> Sites(IEnumerable<ProjectUsageGraph.UsageSite> sites) =>
        sites.Select(s => $"{s.Block} N{s.Network}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

    // THE FOUR CLAIMS, PLUS THE DOCUMENT-LEVEL FACTS NO CORPUS STATES. Each names what is missing and
    // WHO resolves it — a hole that only says "missing" moves the work without saying where.
    private static IReadOnlyList<BindingHole> Holes(
        string slotId,
        IReadOnlyList<DerivedSignal> targets,
        IReadOnlyList<DerivedSignal> sources,
        ServedAreaReport served)
    {
        var holes = new List<BindingHole>();
        var all = targets.Concat(sources).Select(s => s.Tag).ToList();

        holes.Add(new BindingHole("specName", all,
            "the specification's own name for each of these signals",
            "the coordinator. It is a spec<->code TRANSLATION and the corpus contains only one of the "
            + "two sides. Absent, gate 5 reports NOT CHECKED and nothing joins what a vector cites to "
            + "what the copy layer carries."));

        // Only the DRIVEN rows, and only the Int ones. A Time is a duration written as itself and a
        // Bool is true or false — neither has a symbol table to need. An Int is the one width a
        // specification names by SYMBOL, and one arriving with no encoder reaches the range check AS
        // TEXT and is refused by name, which is what a missing encoder looks like when it is behaving
        // well.
        var encodable = targets.Where(s => s.MirrorType == "Int").Select(s => s.Tag).ToList();

        if (encodable.Count > 0)
        {
            holes.Add(new BindingHole("encoding", encodable,
                "whether the cited values for these are SYMBOLS needing a table, or numbers written as "
                + "themselves",
                "the coordinator, transcribing the specification's own table and CITING the line "
                + "(`source`, e.g. `md:132-143`). Without it an encoded value is indistinguishable from a "
                + "number somebody invented. If the cited text IS the value, leave `encoding` absent — "
                + "that is the positive claim `no table`."));
        }

        holes.Add(new BindingHole("inertRest", sources.Select(s => s.Tag).ToList(),
            "what each of these reads when the plant is at rest",
            "the coordinator. It is a claim about the PLANT, and a hard-coded zero is wrong in both "
            + "directions. `excluded: true` with a `basis` is the other admissible answer; the slot-level "
            + "`assumedZeroRest` escape needs `assumedZeroRestBasis` beside it."));

        holes.Add(new BindingHole("startCondition", new[] { $"slots[{slotId}]" },
            "the member the harness drives to start one index, if there is one",
            "the coordinator. 🔴 ABSENT IS NOT AN ABSENCE HERE — a null `startCondition` is the POSITIVE "
            + "claim `this slot has no start gate` (D37). The corpus cannot tell a block that has no start "
            + "bool from one whose start bool nobody has named, so neither answer is emitted."));

        holes.Add(new BindingHole("blockName + blockNumber + tagTableName + tagPrefix",
            new[] { "binding" },
            "the identity of the COPY LAYER this binding will generate, which does not exist yet",
            "the coordinator. `blockNumber` must come from `converter claim --kind block-number "
            + "--allocate --floor 9000` (the harness band), never from a number chosen here. A blank "
            + "`blockNumber` is refused by CopyLayerGenerator rather than defaulted to 0."));

        holes.Add(new BindingHole("declaredBy", new[] { "binding" },
            "who authored this binding",
            "the coordinator, and it must NOT be the block's author: gate 5c exists because a party who "
            + "decides both what the block does and what can be observed of it is the correlated reading "
            + "this pipeline breaks. A tool cannot sign for a person."));

        if (!served.Derived)
        {
            holes.Add(new BindingHole("declaredRegisters + baseByte", new[] { "binding" },
                "the served Modbus window — NOT DERIVED from this corpus",
                $"the engineer. {served.Denominator}. `converter served-area` reads the width off the "
                + "MB_SERVER call AND its sidecar and refuses on disagreement; exit 2 is never a pass, so "
                + "no number is written here."));
        }

        // 🔴 THE ORDER OF THE TWO ARRAYS IS ITSELF A DECISION, because array position IS register
        // position. The committed binding's own `_resultSourceOrder` note records what happens when it
        // is treated as cosmetic: four signals sat in a different order from the deployed layer, the
        // document was internally consistent, and the next deploy would simply have moved them — which
        // is exactly when a client written against the old map goes quietly wrong.
        holes.Add(new BindingHole("registerOrder", new[] { $"slots[{slotId}].vectorTargets", $"slots[{slotId}].resultSources" },
            "confirmation that the scaffold's ordinal-by-tag order is the order to deploy in",
            "the coordinator. A corpus states which members exist, never which register each belongs in. "
            + "If a layer is already deployed, transcribe ITS order — a re-order moves signals to different "
            + "registers with nothing to flag it."));

        holes.Add(new BindingHole("retentiveBytes", new[] { "binding" },
            "the program's retentive %M extent",
            "the engineer. It is a property of the PROGRAM, not of the CPU, and it is an input to the map "
            + "hash and therefore to the build stamp. Nothing in an IR corpus states it."));

        return holes;
    }

    private static HarnessBindingReport Empty(
        string projectDir,
        int filesScanned,
        string stimulusBlock,
        string instance,
        IReadOnlyList<string> observed,
        IReadOnlyList<string> scopes,
        string slotId,
        IReadOnlyList<BindingRefusal> refusals,
        IReadOnlyList<string> warnings,
        BindingScope scope) =>
        new(projectDir, filesScanned, stimulusBlock, instance, observed, scopes, slotId,
            Array.Empty<DerivedSignal>(), Array.Empty<DerivedSignal>(), Array.Empty<ExcludedSignal>(),
            refusals, Array.Empty<BindingHole>(), warnings, null, null,
            "NOT CONSULTED — the scaffold refused before it reached the served area.", scope);
}
