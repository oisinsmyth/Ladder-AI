using System.Text.Json;
using System.Text.Json.Serialization;
using Harness.Map;

namespace Harness.Gate;

/// <summary>
/// The binding document — <b>the coordinator's half, and it is not in the submission on purpose.</b>
///
/// <para>Per D13 instrumentation is a property of the COPY LAYER, not of the block under test, and the
/// vector author is fenced from it. So which signal sits in which register, and what TYPE it is, is
/// stated here rather than by whoever wrote the vectors.</para>
/// </summary>
public sealed class BindingDocument
{
    public List<SlotBindingDocument>? Slots { get; set; }

    /// <summary>Copy-layer block name. Defaults to the generator's own.</summary>
    public string? BlockName { get; set; }

    /// <summary>Block number. <b>No default that works</b> — the generator refuses zero, because harness block numbers come from a reserved range the caller allocates from (hard rule 3).</summary>
    public int? BlockNumber { get; set; }

    public string? TagTableName { get; set; }
    public string? TagPrefix { get; set; }

    /// <summary>First <c>%M</c> byte of the mirror. The rig's is 1000.</summary>
    public int? BaseByte { get; set; }

    /// <summary>Retentive <c>%M</c> extent, for the 0.1b non-retentive assertion.</summary>
    public int? RetentiveBytes { get; set; }

    /// <summary>
    /// 🔴 <b>THE BINDING DOCUMENT WAS OUTSIDE GATE 0b, AND IT IS THE DOCUMENT WHERE A DROPPED FIELD HAS
    /// ALREADY COST A RUN.</b>
    ///
    /// <para>0b refuses a field the submission schema does not read, because a silently-ignored field
    /// reads as an accepted one. <b>This document had no extension data at all</b>, so every unknown key
    /// in it was dropped in silence — and this is the half that carries the INSTRUMENTATION.
    /// <c>specName</c>'s own history is the argument: while the translation lived only in prose, gate 5,
    /// the static interface check and the conflict graph all failed in one run with 1 of 17 signals
    /// resolving. A misspelt <c>specname</c> here would reproduce that exactly, and 0b would have said
    /// nothing.</para>
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }

    /// <summary>Every field the binding schema did not map, split as the submission's are.</summary>
    public (IReadOnlyList<string> Unknown, IReadOnlyList<string> Annotations) AllExtraFieldPaths()
    {
        var found = new List<string>();
        Collect(UnknownFields, "binding", found);

        foreach (var (slot, index) in (Slots ?? new List<SlotBindingDocument>()).Select((s, i) => (s, i)))
        {
            var path = $"binding.slots[{index}]";
            Collect(slot.UnknownFields, path, found);

            foreach (var (signal, j) in (slot.VectorTargets ?? new List<MirroredSignalDocument>()).Select((v, k) => (v, k)))
                Collect(signal.UnknownFields, $"{path}.vectorTargets[{j}]", found);

            foreach (var (signal, j) in (slot.ResultSources ?? new List<MirroredSignalDocument>()).Select((r, k) => (r, k)))
                Collect(signal.UnknownFields, $"{path}.resultSources[{j}]", found);
        }

        return SubmissionDocument.Split(found);

        static void Collect(Dictionary<string, object?>? unknown, string prefix, List<string> into)
        {
            foreach (var key in (unknown ?? new Dictionary<string, object?>()).Keys)
                into.Add(prefix + "." + key);
        }
    }

    public static BindingDocument Read(string json) =>
        JsonSerializer.Deserialize<BindingDocument>(json, Options)
        ?? throw new InvalidDataException("the binding document is empty.");

    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };
}

/// <summary>One slot's binding.</summary>
public sealed class SlotBindingDocument
{
    public string? SlotId { get; set; }
    public List<MirroredSignalDocument>? VectorTargets { get; set; }

    /// <summary><b>Null is a CLAIM</b> — "this block has no start gate" (D37) — never a blank.</summary>
    public string? StartCondition { get; set; }

    public List<MirroredSignalDocument>? ResultSources { get; set; }

    /// <summary>Unknown keys at slot level. Folded into gate 0b — see <see cref="BindingDocument.UnknownFields"/>.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>One mirrored signal. <b><c>Type</c> absent parses as <c>Unstated</c>, which the generator refuses by name.</b></summary>
public sealed class MirroredSignalDocument
{
    public string? Tag { get; set; }

    /// <summary>
    /// The element type. <b>There is deliberately no default:</b> a hard-coded <c>Int</c> is what put an
    /// unmirrorable copy layer on a controller, and <c>Unstated</c> refuses naming the signal.
    /// </summary>
    public MirrorValueType Type { get; set; } = MirrorValueType.Unstated;

    /// <summary>
    /// 🔴 <b>THE SPECIFICATION'S NAME FOR THIS SIGNAL — the join the harness never had.</b>
    ///
    /// <para><c>tag</c> is what the BLOCK calls it; this is what the SPECIFICATION calls it, and a vector
    /// cites the second. *** MEASURED: WHEREVER THEY DIFFER, EVERY MECHANICAL PATH FAILED *** — gate 5,
    /// the static interface check and the conflict graph, all in one run, with 1 of 17 signals resolving
    /// and that one being the only name collision. The translation had lived only in a prose table, and
    /// prose is what no gate reads.</para>
    ///
    /// <para><b>Absent is NOT "the same as tag".</b> That identity is the assumption being removed. Absent
    /// means the binding said nothing, gate 5 reports NOT CHECKED and names the tag, and a genuinely
    /// identical pair is STATED as identical.</para>
    /// </summary>
    public string? SpecName { get; set; }

    /// <summary>
    /// <b>The block that latches this signal, when one does.</b> Absent means this binding claims no latch.
    ///
    /// <para>*** FOUR TRUE LATCHES WERE REFUSED IN ONE RUN BECAUSE THIS COULD NOT BE SAID. *** The
    /// copy-layer generator emits no per-signal latch, so <c>Sampled</c> is all it can derive; a
    /// hand-authored latch block on the device is real and was undeclarable.</para>
    ///
    /// <para>🔴 <b>It names a BLOCK, not a mode.</b> <c>latched: true</c> would be a caller asserting the
    /// answer, and a caller assertion is forgotten exactly when it matters. A block name is PROVENANCE —
    /// checkable against the deployed object set, and printed in the gate's own report.</para>
    /// </summary>
    public string? LatchedBy { get; set; }

    /// <summary>
    /// 🔴 <b>THE SIGNAL IS A TRANSIENT AND THE COPY LAYER MUST LATCH IT — and until now this could not be
    /// SAID FROM A BINDING AT ALL.</b>
    ///
    /// <para>*** THIRD INSTANCE OF THE SAME SHAPE: THE DOMAIN MODEL GAINED THE FIELD AND THE WIRE FORMAT
    /// DID NOT. *** <c>MirroredSignal.Transient</c> exists, the generator reads it, and
    /// <c>ToMirroredSignal</c> never passed it — so the capability was unreachable from the only artifact
    /// a coordinator writes. A field nobody can set is a field that does not exist, however well it is
    /// implemented downstream.</para>
    ///
    /// <para><b>False is not a claim that the signal is persistent</b> — it is the absence of a claim that
    /// it is transient, and the generator's own note says the default's failure mode is a REFUSAL: no
    /// latch is emitted and gate 5 refuses a <c>Latched</c> expectation, loudly, before anything is
    /// spent.</para>
    /// </summary>
    public bool Transient { get; set; }

    /// <summary>
    /// The signal fires ONCE PER VECTOR INDEX, and each firing must be distinguishable from the last.
    ///
    /// <para>⚠️ <b>ITS OMISSION FAILS SILENTLY WHERE <see cref="Transient"/>'s FAILS LOUDLY, AND THE TWO
    /// SIT SIDE BY SIDE.</b> Forgetting <c>transient</c> yields no latch and gate 5 refuses. Forgetting
    /// THIS yields the one-shot latch, which <b>compiles, deploys and reads plausibly</b> — index 2's
    /// latch is already high from index 1, so a signal that never fired again reads as one that did.
    /// <i>Two flags whose omissions fail in opposite directions is a trap that looks like symmetry:
    /// anyone reasoning by analogy from the loud one will trust the silent one exactly as far, and be
    /// wrong.</i></para>
    ///
    /// <para>The generator REFUSES BY NAME when this is set, rather than emitting the latch it can emit —
    /// the copy layer cannot express a per-index arm band. It is on the wire so that the refusal is
    /// reachable from a binding: <b>a capability gap that can be declared is one a caller meets at the
    /// gate, and one that cannot is one they meet on the rig</b>, where the symptom is predicted findings
    /// quietly absent.</para>
    /// </summary>
    public bool RearmsEachIndex { get; set; }

    /// <summary>Unknown keys at signal level — where a misspelt <c>specName</c> would otherwise vanish.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}
