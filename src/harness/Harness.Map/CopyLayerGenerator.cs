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

    /// <summary>Generate the copy layer for a single-slot map, or report every reason it cannot be.</summary>
    /// <param name="stamp">
    /// The build stamp to publish into the version register (§9, build-plan item 2.6). Required, with
    /// no default: a version register carrying a defaulted value confirms nothing, and the failure it
    /// exists to catch — an aborted or half-applied download — is exactly the one where a plausible
    /// value is worse than none. Derive it with <see cref="BuildStamp.Of"/>.
    /// </param>
    public static CopyLayerResult Generate(RegisterMap map, SlotBinding binding, CopyLayerNaming naming, BuildStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(naming);

        var refusals = new List<string>();

        if (stamp.Value == 0)
            refusals.Add("the build stamp is zero. Unwritten bit memory reads as zero, so a zero stamp confirms against a CPU that never ran the copy layer — the exact half-applied download the version register exists to catch (spec section 9).");

        // Phase 2 is one slot, one block, one vector, end to end — narrow and complete rather than broad
        // and partial. A second slot is phase 3, and it exists to retire A5 (two slots do not interfere),
        // which nothing has yet measured. Generating for two now would look like it worked.
        if (map.Slots.Count != 1)
            refusals.Add($"the minimal copy layer generates for exactly ONE slot; this map has {map.Slots.Count}. Multi-slot is phase 3 and it retires assumption A5, which is unmeasured.");

        var slot = map.Slot(binding.SlotId);
        if (slot is null)
            refusals.Add($"binding names slot '{binding.SlotId}', which is not in the map.");

        if (naming.BlockNumber <= 0)
            refusals.Add("block number must be supplied and positive. Harness block numbers come from the reserved range the caller allocates from, never from this generator (hard rule 3).");

        foreach (var (name, what) in new[] { (naming.BlockName, "block name"), (naming.TagTableName, "tag table name") })
        {
            if (!SafeIdentifier.IsMatch(name ?? string.Empty))
                refusals.Add($"{what} '{name}' is not a plain identifier.");
        }

        if (!SafeIdentifier.IsMatch((naming.TagPrefix ?? string.Empty) + "x"))
            refusals.Add($"tag prefix '{naming.TagPrefix}' would not form a plain identifier.");

        if (!SafeIdentifier.IsMatch(binding.SlotId ?? string.Empty))
            refusals.Add($"slot id '{binding.SlotId}' is not a plain identifier, and it is part of every generated tag name.");

        var vectorTargets = binding.VectorTargets ?? Array.Empty<string>();
        var resultSources = binding.ResultSources ?? Array.Empty<string>();

        if (resultSources.Count == 0)
            refusals.Add("binding wires no result sources. A slot that publishes nothing produces a result region of zeros indistinguishable from a real one.");

        if (slot is not null && vectorTargets.Count > slot.Vector.Length)
            refusals.Add($"binding wires {vectorTargets.Count} vector targets but slot '{binding.SlotId}' was allocated {slot.Vector.Length} vector registers.");

        if (slot is not null && resultSources.Count > slot.Result.Length)
            refusals.Add($"binding wires {resultSources.Count} result sources but slot '{binding.SlotId}' was allocated {slot.Result.Length} result registers.");

        foreach (var tag in vectorTargets.Concat(resultSources).Append(binding.StartCondition))
        {
            if (tag is not null && string.IsNullOrWhiteSpace(tag))
                refusals.Add("a bound tag name is blank.");
        }

        if (refusals.Count > 0)
            return new CopyLayerResult(null, Array.Empty<HarnessObject>(), refusals);

        var allocation = slot!;
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

        if (binding.StartCondition is not null)
        {
            tags.Add(new($"{prefix}{binding.SlotId}_Start", "Bool",
                geometry.BitAddressOf(allocation.StartBoolRegister, allocation.StartBitInRegister),
                geometry.ByteAddressOf(allocation.StartBoolRegister),
                "Start bool. Its rising edge is the test's T=0."));
        }

        for (var i = 0; i < vectorTargets.Count; i++)
        {
            var register = allocation.Vector.Register + i;
            tags.Add(new($"{prefix}{binding.SlotId}_V{i:000}", "Int", geometry.WordAddressOf(register),
                geometry.ByteAddressOf(register), $"Vector register {i}."));
        }

        for (var i = 0; i < resultSources.Count; i++)
        {
            var register = allocation.Result.Register + i;
            tags.Add(new($"{prefix}{binding.SlotId}_R{i:000}", "Int", geometry.WordAddressOf(register),
                geometry.ByteAddressOf(register), $"Result register {i}."));
        }

        var networks = new List<CopyLayerNetwork>();
        var number = 1;

        networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.Version,
            "Program version",
            new[] { (stamp.Literal, $"{prefix}ProgramVersion") }));

        networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.ScanCounter,
            "Free-running scan counter",
            new[] { ($"{prefix}ScanCount", $"{prefix}ScanCount") }));

        if (vectorTargets.Count > 0)
        {
            networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.VectorIn,
                $"Vector in - slot {binding.SlotId}",
                vectorTargets.Select((t, i) => ($"{prefix}{binding.SlotId}_V{i:000}", t)).ToArray()));
        }

        if (binding.StartCondition is not null)
        {
            networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.StartBool,
                $"Start bool - slot {binding.SlotId}",
                new[] { ($"{prefix}{binding.SlotId}_Start", binding.StartCondition) }));
        }

        networks.Add(new CopyLayerNetwork(number, CopyLayerNetworkKind.ResultsOut,
            $"Results out - slot {binding.SlotId}",
            resultSources.Select((s, i) => (s, $"{prefix}{binding.SlotId}_R{i:000}")).ToArray()));

        var plan = new CopyLayerPlan(map, binding, stamp, tags, networks, binding.StartCondition is null);

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
                    var (bit, condition) = network.Moves[0];
                    ir.Append($"  COIL {condition} := {bit}\n");
                    break;

                default:
                    foreach (var (from, to) in network.Moves)
                        ir.Append($"  MOVE(EN := TRUE, IN := {from}) => {to}\n");
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
