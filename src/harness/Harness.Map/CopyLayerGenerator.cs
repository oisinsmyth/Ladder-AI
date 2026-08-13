using System.Text;
using System.Text.RegularExpressions;

namespace Harness.Map;

/// <summary>Naming and numbering the generator needs and must not invent.</summary>
/// <param name="BlockName">Name of the generated FC.</param>
/// <param name="BlockNumber">
/// Its block number. Required, with no default: hard rule 3 forbids inventing block numbers, and X-J
/// reserves a range for harness objects that the caller — not this generator — allocates from.
/// </param>
/// <param name="TagTableName">Name of the generated PLC tag table.</param>
/// <param name="TagPrefix">Prefix for every mirror tag, so harness names never collide with plant ones.</param>
public sealed record CopyLayerNaming(
    string BlockName = "FC_HarnessCopyLayer",
    int BlockNumber = 0,
    string TagTableName = "HarnessMirror",
    string TagPrefix = "HX_");

/// <summary>
/// Generates the minimal copy layer: vector in, start bool, results out, free-running scan counter.
///
/// <para><b>Minimal means minimal, and the list of absences is the deliverable.</b> No packing (one
/// register carries one Int-width value; anything wider is packing and is deferred), no multi-slot, no
/// claims, no coverage, no deferred queue, no cleanup, no time compression, no latched transients, no
/// event scan-stamps, no rich result package, no executed-start-bool echo, no version register. All of
/// that is width, none of it is proven, and building it now is how phase 2 stops being cheap.</para>
///
/// <para><b>Why the mirror is reached through a tag table rather than absolute addresses in the
/// rungs.</b> A PLC tag table maps a symbolic name onto a <c>%M</c> address (<c>ir/SPEC.md</c>'s
/// TAGTABLE grammar), which puts every address in ONE place that <see cref="RetentionCheck"/> can
/// audit against the retentive window. Spread through rungs, the same addresses would be auditable only
/// by re-parsing every network.</para>
///
/// <para><b>The block is called every scan, including throughout inert</b> (D37) — this generator emits
/// no gate on the call, and that is not an omission. Resets are held asserted during inert and a reset
/// is processed BY the block; a block that is not called never processes its reset and holds whatever
/// its statics contained, which is the opposite of inert.</para>
/// </summary>
public static class CopyLayerGenerator
{
    private static readonly Regex SafeIdentifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// The types this generator can actually render — <b>the whole supported set, in one place.</b>
    ///
    /// <para>Everything else is a refusal naming the signal. It is a set rather than a <c>default:</c>
    /// arm because a <c>default:</c> that falls through to Int is the defect that reached the rig.</para>
    /// </summary>
    private static readonly IReadOnlySet<MirrorValueType> Renderable =
        new HashSet<MirrorValueType> { MirrorValueType.Bool, MirrorValueType.Int };

    /// <summary>
    /// The order types are emitted in — <b>Bool before Int, and that is the IR's rule, not a preference.</b>
    ///
    /// <para><c>ir/SPEC.md</c>: statements within one network are grouped by kind in a fixed order, and
    /// <c>COIL</c> comes before <c>MOVE</c>. This generator emits one network per type rather than one
    /// ordered network, so the rule cannot be broken — but the networks themselves are emitted in the
    /// same order, so merging them later would still be legal.</para>
    /// </summary>
    private static readonly MirrorValueType[] RenderOrder = { MirrorValueType.Bool, MirrorValueType.Int };

    /// <summary>
    /// The mirror tag for one register, <b>typed and addressed together</b> — the two things the
    /// hard-coded <c>"Int"</c> got wrong at once.
    ///
    /// <para>A Bool takes <b>bit 0 of its own register</b>: the register is not shared, because packing
    /// is an explicit non-goal and because the client's result index is the register offset. Bit 0 is
    /// chosen so a client reading the register as a 16-bit word tests <c>value &amp; 1</c> — the same
    /// convention <see cref="MirrorGeometry.BitAddressOf"/> already applies to the start bools, so
    /// there is ONE bit-order question in this system and not two.</para>
    /// </summary>
    private static MirrorTag MirrorTagFor(string name, MirrorValueType type, MirrorGeometry geometry, int register, string comment) =>
        type switch
        {
            MirrorValueType.Bool => new MirrorTag(name, "Bool", geometry.BitAddressOf(register, 0),
                geometry.ByteAddressOf(register), comment + " Bool, bit 0 of this register."),

            MirrorValueType.Int => new MirrorTag(name, "Int", geometry.WordAddressOf(register),
                geometry.ByteAddressOf(register), comment),

            // Unreachable: the refusal pass above rejects anything outside Renderable before this runs.
            // It throws rather than defaulting because a default here is the original defect.
            _ => throw new InvalidOperationException(
                $"mirror tag '{name}' has type {type}, which reached rendering. The type refusal should have stopped this; "
                + "defaulting to Int is what put an unmirrorable copy layer on a controller."),
        };

    /// <summary>Generate the copy layer for a single-slot map, or report every reason it cannot be.</summary>
    /// <param name="stamp">
    /// The build stamp to publish into the version register (§9, build-plan item 2.6). Required, with
    /// no default: a version register carrying a defaulted value confirms nothing, and the failure it
    /// exists to catch — an aborted or half-applied download — is exactly the one where a plausible
    /// value is worse than none. Derive it with <see cref="BuildStamp.Of"/>.
    /// </param>
    public static CopyLayerResult Generate(RegisterMap map, SlotBinding binding, CopyLayerNaming naming, BuildStamp stamp) =>
        Generate(map, new[] { binding }, naming, stamp);

    /// <summary>
    /// Generate the copy layer for a wave set of any width — one binding per slot, in map order.
    ///
    /// <para><b>Phase 3 lifts the one-slot refusal that phase 2 carried.</b> The layer is still minimal
    /// in every other respect; what it gains is that per-slot networks repeat, and that each slot
    /// publishes a START ECHO (X-E).</para>
    /// </summary>
    public static CopyLayerResult Generate(RegisterMap map, IReadOnlyList<SlotBinding> bindings, CopyLayerNaming naming, BuildStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(naming);

        var refusals = new List<string>();

        if (stamp.Value == 0)
            refusals.Add("the build stamp is zero. Unwritten bit memory reads as zero, so a zero stamp confirms against a CPU that never ran the copy layer — the exact half-applied download the version register exists to catch (spec section 9).");

        // EVERY slot in the map must be bound. A map slot with no binding is a region the client can
        // address and nothing maintains: its results would read as an unbroken run of zeros
        // indistinguishable from a real result, and its start bool would drive nothing at all.
        if (bindings.Count != map.Slots.Count)
            refusals.Add($"the map holds {map.Slots.Count} slot(s) and {bindings.Count} binding(s) were supplied. Every slot must be bound: an unbound slot is a mirror region nothing maintains, and its zeros are indistinguishable from a result.");

        var bound = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in bindings)
        {
            if (b.SlotId is not null && !bound.Add(b.SlotId))
                refusals.Add($"slot '{b.SlotId}' is bound twice. Two bindings for one slot means one of them silently wins.");
        }

        if (naming.BlockNumber <= 0)
            refusals.Add("block number must be supplied and positive. Harness block numbers come from the reserved range the caller allocates from, never from this generator (hard rule 3).");

        foreach (var (name, what) in new[] { (naming.BlockName, "block name"), (naming.TagTableName, "tag table name") })
        {
            if (!SafeIdentifier.IsMatch(name ?? string.Empty))
                refusals.Add($"{what} '{name}' is not a plain identifier.");
        }

        if (!SafeIdentifier.IsMatch((naming.TagPrefix ?? string.Empty) + "x"))
            refusals.Add($"tag prefix '{naming.TagPrefix}' would not form a plain identifier.");

        foreach (var binding in bindings)
        {
            var slot = map.Slot(binding.SlotId ?? string.Empty);
            if (slot is null)
                refusals.Add($"binding names slot '{binding.SlotId}', which is not in the map.");

            if (!SafeIdentifier.IsMatch(binding.SlotId ?? string.Empty))
                refusals.Add($"slot id '{binding.SlotId}' is not a plain identifier, and it is part of every generated tag name.");

            var targets = binding.VectorTargets ?? Array.Empty<MirroredSignal>();
            var sources = binding.ResultSources ?? Array.Empty<MirroredSignal>();

            if (sources.Count == 0)
                refusals.Add($"binding for slot '{binding.SlotId}' wires no result sources. A slot that publishes nothing produces a result region of zeros indistinguishable from a real one.");

            if (slot is not null && targets.Count > slot.Vector.Length)
                refusals.Add($"binding wires {targets.Count} vector targets but slot '{binding.SlotId}' was allocated {slot.Vector.Length} vector registers.");

            if (slot is not null && sources.Count > slot.Result.Length)
                refusals.Add($"binding wires {sources.Count} result sources but slot '{binding.SlotId}' was allocated {slot.Result.Length} result registers.");

            foreach (var tag in targets.Concat(sources).Select(s => s?.Tag).Append(binding.StartCondition))
            {
                if (tag is not null && string.IsNullOrWhiteSpace(tag))
                    refusals.Add($"a bound tag name is blank on slot '{binding.SlotId}'.");
            }

            // *** THE TYPE IS REFUSED BY NAME, NEVER DEFAULTED. *** A hard-coded "Int" is what put an
            // unmirrorable copy layer on a controller: every result rendered as a plain MOVE, and TIA
            // answered "Data type Bool is not permitted here." A signal whose type nobody stated must
            // stop the generator here rather than reach a rung that cannot be compiled.
            var typed = targets.Select(s => (Signal: s, Where: "vector target"))
                .Concat(sources.Select(s => (Signal: s, Where: "result source")))
                .ToArray();

            foreach (var (signal, where) in typed)
            {
                if (signal is null)
                {
                    refusals.Add($"a {where} on slot '{binding.SlotId}' is null.");
                    continue;
                }

                if (!Renderable.Contains(signal.Type))
                {
                    refusals.Add(
                        $"{where} '{signal.Tag}' on slot '{binding.SlotId}' has type {signal.Type}, which this generator cannot mirror. "
                        + $"Supported: {string.Join(", ", Renderable.Select(t => t.ToString()))}. "
                        + (signal.Type == MirrorValueType.Unstated
                            ? "*** UNSTATED IS A REFUSAL AND NOT A DEFAULT. *** Assuming Int here is exactly the defect this refusal exists to prevent: it renders a plain MOVE, which TIA rejects for a Bool with \"Data type Bool is not permitted here\" — after a full import."
                            : "Widening this means deciding the TAG TYPE and the RUNG SHAPE together and verifying both against TIA, not adding an enum member."));
                }
            }
        }

        if (refusals.Count > 0)
            return new CopyLayerResult(null, Array.Empty<HarnessObject>(), refusals);

        var geometry = map.Geometry;
        var prefix = naming.TagPrefix;

        var tags = new List<MirrorTag>
        {
            new($"{prefix}ProgramVersion", "DWord", geometry.DoubleWordAddressOf(map.Version.Register),
                geometry.ByteAddressOf(map.Version.Register),
                "Build stamp of the downloaded IR set. Present only if this code is running."),

            new($"{prefix}ScanCount", "DInt", geometry.DoubleWordAddressOf(map.ScanCounter.Register),
                geometry.ByteAddressOf(map.ScanCounter.Register),
                "Free-running scan counter. Wraps; scan stamps are differences from the start edge."),
        };

        // Ordered by the MAP, not by the caller's list: slot ordinals decide addresses, so generating in
        // binding order would let a reordered list produce differently-numbered networks for one map.
        var ordered = map.Slots
            .Select(s => (Slot: s, Binding: bindings.Single(b => b.SlotId == s.SlotId)))
            .ToArray();

        foreach (var (allocation, binding) in ordered)
        {
            if (binding.StartCondition is not null)
            {
                tags.Add(new($"{prefix}{binding.SlotId}_Start", "Bool",
                    geometry.BitAddressOf(allocation.StartBoolRegister, allocation.StartBitInRegister),
                    geometry.ByteAddressOf(allocation.StartBoolRegister),
                    "Start bool. Its rising edge is the test's T=0."));

                tags.Add(new($"{prefix}{binding.SlotId}_Ran", "Bool",
                    geometry.BitAddressOf(map.StartEcho.Register + (allocation.StartBoolRegister - map.StartBools.Register), allocation.StartBitInRegister),
                    geometry.ByteAddressOf(map.StartEcho.Register + (allocation.StartBoolRegister - map.StartBools.Register)),
                    "Latched: the block's own start condition was seen high. Cleared by the client at inert."));
            }

            var vectorTargets = binding.VectorTargets ?? Array.Empty<MirroredSignal>();

            for (var i = 0; i < vectorTargets.Count; i++)
            {
                var register = allocation.Vector.Register + i;
                tags.Add(MirrorTagFor($"{prefix}{binding.SlotId}_V{i:000}", vectorTargets[i].Type, geometry, register,
                    $"Vector register {i}."));
            }

            for (var i = 0; i < binding.ResultSources.Count; i++)
            {
                var register = allocation.Result.Register + i;
                tags.Add(MirrorTagFor($"{prefix}{binding.SlotId}_R{i:000}", binding.ResultSources[i].Type, geometry, register,
                    $"Result register {i}."));
            }
        }

        var networks = new List<CopyLayerNetwork>();
        var number = 1;

        networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.Version,
            "Program version",
            new[] { new CopyLayerCopy(stamp.Literal, $"{prefix}ProgramVersion", MirrorValueType.Int) }));

        networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.ScanCounter,
            "Free-running scan counter",
            new[] { new CopyLayerCopy($"{prefix}ScanCount", $"{prefix}ScanCount", MirrorValueType.Int) }));

        foreach (var (_, binding) in ordered)
        {
            var targets = binding.VectorTargets ?? Array.Empty<MirroredSignal>();

            // *** ONE NETWORK PER TYPE, and the register index stays the LIST index. *** Splitting by
            // type is what keeps the IR's kind-ordering rule (COIL before MOVE) unreachable rather than
            // merely satisfied; keying the register off the position in the binding's list is what keeps
            // the client's `IndexOf(signal)` arithmetic true across the split.
            foreach (var type in RenderOrder)
            {
                var ofType = targets
                    .Select((t, i) => (Signal: t, Index: i))
                    .Where(x => x.Signal.Type == type)
                    .Select(x => new CopyLayerCopy($"{prefix}{binding.SlotId}_V{x.Index:000}", x.Signal.Tag, type))
                    .ToArray();

                if (ofType.Length > 0)
                {
                    networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.VectorIn,
                        $"Vector in - slot {binding.SlotId} - {type}", ofType));
                }
            }

            if (binding.StartCondition is not null)
            {
                networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.StartBool,
                    $"Start bool - slot {binding.SlotId}",
                    new[] { new CopyLayerCopy($"{prefix}{binding.SlotId}_Start", binding.StartCondition, MirrorValueType.Bool) }));

                // X-E. The echo reads the FAR side of the coil above — the block's OWN start condition,
                // which is what the program actually ran on. Reading back the mirror bit instead would
                // only report what the client wrote, which is the plan, and the plan is not evidence.
                //
                // LATCHED, because a poll gap is 8.6 scans at the p99 and a short test can start and
                // finish between two polls. A level echo would then read low at both, and the log would
                // say the slot never ran — which is exactly the false evidence X-E exists to kill.
                networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.StartEcho,
                    $"Start echo - slot {binding.SlotId}",
                    new[] { new CopyLayerCopy($"{prefix}{binding.SlotId}_Ran", binding.StartCondition, MirrorValueType.Bool) }));
            }

            foreach (var type in RenderOrder)
            {
                var ofType = binding.ResultSources
                    .Select((s, i) => (Signal: s, Index: i))
                    .Where(x => x.Signal.Type == type)
                    .Select(x => new CopyLayerCopy(x.Signal.Tag, $"{prefix}{binding.SlotId}_R{x.Index:000}", type))
                    .ToArray();

                if (ofType.Length > 0)
                {
                    networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.ResultsOut,
                        $"Results out - slot {binding.SlotId} - {type}", ofType));
                }
            }
        }

        var plan = new CopyLayerPlan(map, ordered.Select(o => o.Binding).ToArray(), stamp, tags, networks,
            ordered.Where(o => o.Binding.StartCondition is null).Select(o => o.Binding.SlotId).ToArray());

        var objects = new[]
        {
            new HarnessObject(naming.TagTableName, HarnessObjectKind.TagTable, WriteTagTable(naming.TagTableName, tags)),
            new HarnessObject(naming.BlockName, HarnessObjectKind.Block, WriteBlock(naming, networks)),
        };

        return new CopyLayerResult(plan, objects, Array.Empty<string>());
    }

    /// <summary>
    /// Renders the mirror tag table as IR.
    ///
    /// <para>Only the tags the copy layer actually references are declared. The slot's allocated
    /// region may be wider — it is fixed-size, sized to the widest slot in the wave set — but the
    /// client addresses the mirror by REGISTER NUMBER, not by tag, so declaring the unbound remainder
    /// would create symbols nothing reads and nothing writes.</para>
    /// </summary>
    private static string WriteTagTable(string name, IReadOnlyList<MirrorTag> tags)
    {
        var ir = new StringBuilder();
        ir.Append($"TAGTABLE {name}\n");
        ir.Append("  ROOTID 0\n");
        ir.Append("  TAGS\n");

        // Tag ids are round-trip metadata. Real exports step them in threes from 1; the values carry no
        // meaning beyond being distinct, and TIA reassigns them on import.
        var id = 1;
        foreach (var tag in tags)
        {
            ir.Append($"    {tag.Name} {id:X} : {tag.DataType} @ {tag.Address} ACCESSIBLE VISIBLE WRITABLE COMMENT \"{Escape(tag.Comment)}\"\n");
            id += 3;
        }

        return ir.ToString();
    }

    /// <summary>
    /// Renders the copy-layer FC as IR.
    ///
    /// <para><b>One operation per network, deliberately.</b> The IR parser requires statements within a
    /// network to be grouped by kind in a fixed order, and that order mirrors real rung-execution order
    /// — get it wrong and the consumer silently reads the producer's PREVIOUS-scan value with no error
    /// anywhere. One kind per network makes the ordering rule unreachable rather than merely satisfied.</para>
    ///
    /// <para>The empty INTERFACE sections are not padding: a real TIA export of a parameterless FC
    /// carries <c>Input</c>, <c>Output</c> and <c>Constant</c> sections, and emitting them is what makes
    /// this text byte-identical to its own <c>to-xml</c> / <c>to-ir</c> round trip.</para>
    /// </summary>
    private static string WriteBlock(CopyLayerNaming naming, IReadOnlyList<CopyLayerNetwork> networks)
    {
        var ir = new StringBuilder();
        ir.Append($"BLOCK FC {naming.BlockName}\n");
        ir.Append("ROOTID 0\n");
        ir.Append($"NUMBER {naming.BlockNumber}\n");
        ir.Append("LANGUAGE LAD\n");
        ir.Append("TITLE \"Harness copy layer\"\n");
        ir.Append('\n');
        ir.Append("INTERFACE\n");
        ir.Append("  INPUT\n");
        ir.Append("  OUTPUT\n");
        ir.Append("  CONSTANT\n");

        foreach (var network in networks)
        {
            ir.Append('\n');
            ir.Append($"NETWORK {network.Number} \"{Escape(network.Title)}\"\n");

            switch (network.Kind)
            {
                case CopyLayerNetworkKind.ScanCounter:
                    var counter = network.Moves[0].From;
                    ir.Append($"  ADD(EN := TRUE, IN1 := {counter}, IN2 := 1) => {counter}\n");
                    break;

                case CopyLayerNetworkKind.StartBool:
                    var start = network.Moves[0];
                    ir.Append($"  COIL {start.To} := {start.From}\n");
                    break;

                case CopyLayerNetworkKind.StartEcho:
                    var echo = network.Moves[0];
                    ir.Append($"  SCOIL {echo.From} := {echo.To}\n");
                    break;

                default:
                    // *** THE SHAPE IS DECIDED BY THE TYPE, AND A BOOL DOES NOT MOVE. *** TIA answers a
                    // `MOVE` with a Bool operand "Data type Bool is not permitted here" — measured, on a
                    // live import, after the whole convert/import cycle. A Bool is copied by a coil, which
                    // is what a real TIA export of a Bool-copying block does (see FC_Outputs in the
                    // committed export corpus, and the test that reads it).
                    foreach (var copy in network.Moves)
                    {
                        ir.Append(copy.Type switch
                        {
                            MirrorValueType.Bool => $"  COIL {copy.To} := {copy.From}\n",
                            MirrorValueType.Int => $"  MOVE(EN := TRUE, IN := {copy.From}) => {copy.To}\n",
                            _ => throw new InvalidOperationException(
                                $"copy {copy.From} -> {copy.To} has type {copy.Type}, which reached rendering. "
                                + "The type refusal should have stopped this; rendering it as a MOVE is the original defect."),
                        });
                    }

                    break;
            }
        }

        return ir.ToString();
    }

    /// <summary>The IR quoted-string escape set, in the order that keeps the backslash rule sound.</summary>
    private static string Escape(string text) => text
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n");
}
