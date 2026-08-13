using System.Security.Cryptography;
using System.Text;

namespace Harness.Map;

/// <summary>
/// The 32-bit build stamp the copy layer publishes into the version register (§9).
///
/// <para><b>What it is for.</b> The CPU will not state its own identity, so the PROGRAM publishes it:
/// the coordinator computes a hash of what it is about to download and GENERATES A LITERAL into the
/// copy layer. That constant lives IN THE CODE, so it can only be present if that code is running, and
/// post-download verification becomes ONE REGISTER READ — no upload, no Portal session, sub-second.</para>
///
/// <para><b>Why the copy layer is not one of its own inputs.</b> The stamp is rendered INTO the copy
/// layer, so hashing the copy layer would be circular. The copy layer is a pure function of the map,
/// the binding and the naming, so those go in instead and the derivation is total without being
/// self-referential.</para>
///
/// <para><b>Excision is an explicit input, and it has to be</b> (spec, gap 4). The map hash deliberately
/// does NOT change when a slot is excised — that is what keeps every client mirror valid — but the
/// build stamp MUST, because the stamp names the IR set actually downloaded and excision changes it.
/// Two properties that look contradictory and are not, so both are asserted by test.</para>
///
/// <para><b>Zero is not a stamp.</b> Unwritten bit memory reads as zero, so a stamp of zero would
/// "confirm" against a CPU that never ran the copy layer at all. The derivation walks the digest until
/// it finds a non-zero word rather than papering over it with an arbitrary substitute.</para>
/// </summary>
public readonly record struct BuildStamp(uint Value)
{
    /// <summary>The IR literal form the copy layer emits — eight hex digits, 32 bits.</summary>
    public string Literal => $"16#{Value:X8}";

    /// <summary>The two holding registers it occupies, high word first. See <c>RegisterWordOrder</c>.</summary>
    public ushort HighWord => (ushort)(Value >> 16);

    /// <summary>The low half of the stamp.</summary>
    public ushort LowWord => (ushort)(Value & 0xFFFF);

    public override string ToString() => Literal;

    /// <summary>
    /// Derive the stamp for one download: the map, the binding and the naming that determine the copy
    /// layer, the excision set, and every other object in the download.
    /// </summary>
    public static BuildStamp Of(
        RegisterMap map,
        SlotBinding binding,
        CopyLayerNaming naming,
        IEnumerable<HarnessObject>? programUnderTest = null) =>
        Of(map, new[] { binding }, naming, programUnderTest);

    /// <summary>The same derivation over a wave set of any width.</summary>
    public static BuildStamp Of(
        RegisterMap map,
        IReadOnlyList<SlotBinding> bindings,
        CopyLayerNaming naming,
        IEnumerable<HarnessObject>? programUnderTest = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(naming);

        var canonical = new StringBuilder();
        canonical.Append("harness-build/1\n");
        canonical.Append($"map={map.MapHash}\n");

        // Excision, named separately BECAUSE the map hash omits it on purpose.
        foreach (var slot in map.Slots.Where(s => s.Excised))
            canonical.Append($"excised={slot.SlotId}\n");

        canonical.Append($"block={naming.BlockName}/{naming.BlockNumber} table={naming.TagTableName} prefix={naming.TagPrefix}\n");

        foreach (var binding in bindings)
        {
            canonical.Append($"slot={binding.SlotId} start={binding.StartCondition ?? "<none>"}\n");

            // The TYPE is part of the canonical form. Retyping a signal changes both the mirror tag and
            // the rung shape, so two bindings differing only in a type are two different programs — and
            // a stamp that could not tell them apart would confirm the wrong one as running.
            foreach (var target in binding.VectorTargets ?? Array.Empty<MirroredSignal>())
                canonical.Append($"v={target.Tag}:{target.Type}\n");

            foreach (var source in binding.ResultSources ?? Array.Empty<MirroredSignal>())
                canonical.Append($"r={source.Tag}:{source.Type}\n");
        }

        foreach (var obj in (programUnderTest ?? Array.Empty<HarnessObject>()).OrderBy(o => o.Name, StringComparer.Ordinal))
            canonical.Append($"obj={obj.Kind}:{obj.Name}\n{obj.Ir}\n");

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));

        for (var offset = 0; offset + 4 <= digest.Length; offset += 4)
        {
            var word = (uint)((digest[offset] << 24) | (digest[offset + 1] << 16) | (digest[offset + 2] << 8) | digest[offset + 3]);
            if (word != 0)
                return new BuildStamp(word);
        }

        // Thirty-two consecutive zero bytes out of SHA-256. Not reachable in practice, and a throw is the
        // honest treatment: silently substituting a constant would make one build stamp mean two things.
        throw new InvalidOperationException("the digest yielded no non-zero 32-bit word; a zero build stamp cannot be told from bit memory that was never written.");
    }
}
