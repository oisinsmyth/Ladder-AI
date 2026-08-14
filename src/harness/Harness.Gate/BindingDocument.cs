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
            {
                Collect(signal.UnknownFields, $"{path}.vectorTargets[{j}]", found);

                // The encoding is a nested object and gate 0b has to reach INSIDE it: a misspelt `source`
                // there would silently drop the citation the encoding refuses without, and a misspelt
                // `whenNumeric` would silently leave the safe `Refuse` in place — which looks like working
                // strictness and is actually a dropped field.
                Collect(signal.Encoding?.UnknownFields, $"{path}.vectorTargets[{j}].encoding", found);
            }

            foreach (var (signal, j) in (slot.ResultSources ?? new List<MirroredSignalDocument>()).Select((r, k) => (r, k)))
            {
                Collect(signal.UnknownFields, $"{path}.resultSources[{j}]", found);
                Collect(signal.Encoding?.UnknownFields, $"{path}.resultSources[{j}].encoding", found);
            }
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

    /// <summary>
    /// 🔴 <b>The specification slot ids this ONE slot serves — the wire half of the many-to-one map.</b>
    ///
    /// <para>Absent or empty means the slot answers to its own <see cref="SlotId"/> and nothing else,
    /// which is every binding written before this existed. When it is stated, the slot's own id stops
    /// being citable — see <c>Harness.Map.SlotBinding.Serves</c>, which carries the reasoning and the four
    /// authorities behind it.</para>
    ///
    /// <para><b>The domain model has had no way to express this at all</b>, so a set of vectors written
    /// against six phase ids of one physical slot could not be run: <c>SlotJoin</c> reported
    /// <c>27 vector(s) name 6 slot(s) that no binding declares</c>, correctly, with no route past it that
    /// was not an invention.</para>
    /// </summary>
    public List<string>? Serves { get; set; }

    /// <summary>
    /// 🔴 <b>A POSITIVE CLAIM THAT <see cref="Serves"/>' ORDER IS THE ORDER THEY RUN IN.</b>
    ///
    /// <para><b>Its absence on a multi-group slot is a REFUSAL, not a fallback to the listed order.</b>
    /// The submission carries no total order of its own — measured, every group restarts <c>index</c> at 0
    /// — so the merge needs a major key, and taking one from the array's incidental order would be a
    /// default. <i>A default here is a guess wearing a mechanism's clothes.</i></para>
    ///
    /// <para>Ignored on a single-group slot, deliberately: a gate that fires on submissions it has nothing
    /// to say about is noise, and noise gets switched off.</para>
    /// </summary>
    public bool ServesRunInOrder { get; set; }

    /// <summary>
    /// 🔴 <b>Served groups that do NOT run inline, because a DOWNLOAD BOUNDARY happens inside them.</b>
    ///
    /// <para>A group listed here takes no position in the inline sequence, so where it appears in
    /// <see cref="Serves"/> is not read. A run carrying its vectors is refused BY NAME — see
    /// <c>LoopOutcome.NotSchedulable</c> — because a restart-spanning scenario executed without the
    /// restart reads as a clean pass, and so does every group scheduled after it.</para>
    ///
    /// <para>Naming a group that is not served is itself a refusal: an exclusion from a sequence the group
    /// was never in reads as a handled case and handles nothing.</para>
    /// </summary>
    public List<string>? BoundarySpanning { get; set; }

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
    /// <para><b>The generator now BUILDS it</b> (2026-08-14): the latch becomes
    /// <c>SCOIL &lt;latch&gt; := &lt;slot start bool&gt; AND [&lt;armedBy&gt;] AND &lt;signal&gt;</c> with
    /// <c>RCOIL &lt;latch&gt; := NOT &lt;slot start bool&gt;</c>, so every vector index re-arms it. It
    /// used to be a REFUSAL — that refusal survives for the one case still inexpressible, a slot whose
    /// <c>startCondition</c> is null and which therefore has no per-index level to arm on.</para>
    /// </summary>
    public bool RearmsEachIndex { get; set; }

    /// <summary>
    /// 🔴 <b>THE TAG THAT OPENS THE OBSERVATION WINDOW INSIDE ONE INDEX.</b>
    ///
    /// <para><see cref="RearmsEachIndex"/> re-arms the latch per index, off the slot's start bool. This
    /// narrows it further within an index, and it is what the coordinator's capability request asked for
    /// in those words: <i>"THE LATCHES MUST BE ARMED BY THE STIMULUS MODEL'S PHASE FLAG (HBA_Stim.Armed),
    /// not free-running from T=0"</i>. Without it a run that legitimately begins with a clear-down fills
    /// the latch from the clear-down and reads violated whatever the block did.</para>
    ///
    /// <para><b>Absent is a claim of NO in-index window</b>, which leaves the whole index armed — correct
    /// for a signal whose every firing inside an index matters. It is <b>refused on a signal with no
    /// generated latch</b>, since an arm with nothing to arm is a caller believing they have a
    /// phase-armed observation when they have a sampled one.</para>
    /// </summary>
    public string? ArmedBy { get; set; }

    /// <summary>
    /// 🔴 <b>HOW A CITED VALUE BECOMES THE INTEGER THIS MEMBER HOLDS — the field whose absence made 81 of
    /// the deliverable's values unwritable.</b>
    ///
    /// <para>All 27 conformance vectors write symbolic decade names (<c>"RAISE_UNINTERRUPTED"</c>,
    /// <c>"CLEARDOWN"</c>) into members the IR declares <c>Int</c>. The tables that resolve them are stated
    /// in the coordinator's prose, so supplying one is a TRANSCRIPTION — <b>and the schema had nowhere to
    /// put it</b>, so the values reached the range check as text and were refused by name.</para>
    ///
    /// <para><b>Two entries may share a <c>specName</c> and carry DIFFERENT encodings over the same cited
    /// value.</b> That is the whole of the two-tag form: one set-B field, two IR members, read twice under
    /// two stated rules. Nothing special-cases it.</para>
    ///
    /// <para>Absent means the cited text IS the value, which is right for every plain duration.</para>
    /// </summary>
    public ValueEncodingDocument? Encoding { get; set; }

    /// <summary>Unknown keys at signal level — where a misspelt <c>specName</c> would otherwise vanish.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>
/// One value encoding, on the wire. See <c>Harness.Map.ValueEncoding</c> for the semantics; this type is
/// only its JSON shape.
/// </summary>
public sealed class ValueEncodingDocument
{
    /// <summary>Cited text → the integer written. Compared ordinally, after trimming.</summary>
    public Dictionary<string, long>? Values { get; set; }

    /// <summary>What to do with a cited integer the table does not name. <c>Refuse</c> is the zero value.</summary>
    public NumericFallback WhenNumeric { get; set; } = NumericFallback.Refuse;

    /// <summary>The fixed integer for <c>Literal</c>. Refused when absent there, and refused when present anywhere else.</summary>
    public long? NumericLiteral { get; set; }

    /// <summary>
    /// 🔴 <b>The prose line this table transcribes. REQUIRED — an encoding without one refuses.</b>
    ///
    /// <para>Held as a mapped field rather than as an <c>_</c>-prefixed annotation on purpose: an
    /// annotation is not read by anything, so a citation kept there is a convention somebody eventually
    /// drops. Here, dropping it makes the encoding fail loudly instead — which is the difference between
    /// a transcription and a number somebody invented.</para>
    /// </summary>
    public string? Source { get; set; }

    /// <summary>Unknown keys inside an encoding, folded into gate 0b like every other level.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }

    /// <summary>The domain form.</summary>
    public ValueEncoding ToEncoding() =>
        new(Values ?? new Dictionary<string, long>(StringComparer.Ordinal), WhenNumeric, NumericLiteral, Source);
}
