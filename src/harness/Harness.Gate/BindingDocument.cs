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
    /// <summary>
    /// 🔴 <b>WHO WROTE THIS BINDING — and until now the document carried no author at ANY of its levels.</b>
    ///
    /// <para>The observability map gate 5 adjudicates against is DERIVED FROM THIS DOCUMENT
    /// (<c>MirrorObservability.FromBindings</c>), so this document decides <b>what can be seen of the
    /// block</b>. Gate 5 fences the vector author out of it STRUCTURALLY — by provenance, refusing a map
    /// that came out of the submission — and that fence says nothing whatever about the BLOCK's author.
    /// <b>A block author who also writes this file decides both what the block does and what anyone is
    /// able to observe of it</b>, which is the correlated reading the whole pipeline exists to break, and
    /// no code compared the two parties because there was no second operand to compare.</para>
    ///
    /// <para><b>Same shape and same name as <c>FidelityDeclaration.DeclaredBy</c></b>, which is the same
    /// defect one artifact over: the fidelity list licenses what a vector may ASSERT, this document
    /// decides what a vector may OBSERVE, and both are worthless as checks if the party being checked
    /// wrote them. See gate <c>5c map authority</c>.</para>
    ///
    /// <para><b>Absent is NOT CHECKED, never a pass</b> — <i>unknown is not independent</i>. It is a
    /// mapped field rather than an <c>_</c>-annotation for <c>ValueEncodingDocument.Source</c>'s reason:
    /// an annotation is read by nothing, so a convention kept there is one somebody eventually drops.</para>
    /// </summary>
    public string? DeclaredBy { get; set; }

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
    /// 🔴 <b>The width <c>MB_HOLD_REG</c> declares, in registers — the reachable ceiling.</b>
    ///
    /// <para><b>Required, and deliberately not defaulted</b>, unlike <see cref="BaseByte"/> and
    /// <see cref="RetentiveBytes"/> above. Those two have obvious rig values and a wrong guess shows up
    /// immediately as addresses that do not match; this one governs whether a map FITS, and a default
    /// would be a number this code invented silently deciding that question. It is the same rule
    /// <c>harness-mirror-read</c>'s <c>--declared-registers</c> already enforces: <i>"defaulting it would
    /// let a run conclude against a width nobody stated."</i></para>
    ///
    /// <para>It is the <c>n</c> in the comms block's area pointer, <c>P#M&lt;base&gt;.0 WORD n</c>. ⚠️ <b>Do
    /// not memorise a figure for it.</b> A number written here would be a fact about one deployment
    /// inside a schema comment, which is how this repository has twice recorded one program's scan
    /// period as a rig fact — read the comms block.</para>
    /// </summary>
    public int? DeclaredRegisters { get; set; }

    /// <summary>
    /// 🔴 <b>Runs of the declared area that something ELSE in the deployed program already owns.</b>
    ///
    /// <para><b>Why this field exists, measured on a bench rig 2026-08-23.</b> A generated mirror and a
    /// hand-authored virtual panel both claimed registers 256–323 of one <c>%M</c> area. 53 tags collided
    /// bit-for-bit, including the panel's master enable and a safety-healthy substitution, both
    /// overwritten every scan by the copy layer. Nothing caught it: the mirror is bounded against the
    /// DECLARED AREA and proved disjoint from ITSELF, and neither check knows a neighbour exists. The
    /// mirror's extent is computed — about 165 registers per lane — so it grew into a reservation nobody
    /// had encoded, as the batch went from one lane to two.</para>
    ///
    /// <para><b>Empty means no neighbour was declared, NOT that the area is otherwise empty.</b> That
    /// distinction is the whole honesty of the field: the guard can only see what somebody wrote down,
    /// and the panel would have collided just the same if this had been left blank. Until something
    /// DERIVES the neighbour list from the deployed program, this is a place to put the knowledge rather
    /// than a way to obtain it.</para>
    ///
    /// <para>Registers, numbered from the area base — the same numbering as <see cref="DeclaredRegisters"/>,
    /// never <c>%M</c> byte addresses. Each needs an owner label, because a refusal that cannot say whose
    /// space was hit sends the reader to the mirror, which is the one place the problem is not.</para>
    /// </summary>
    public List<ReservedRegionDocument>? ReservedRegions { get; set; }

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
                Collect(signal.InertRest?.UnknownFields, $"{path}.vectorTargets[{j}].inertRest", found);

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

                // The resting state is the newest nested object and it is the one where a dropped key is
                // worst: a misspelt `excluded` leaves a signal claiming a resting VALUE of null, and a
                // misspelt `basis` silently removes the only thing a reader can check an exclusion by.
                Collect(signal.InertRest?.UnknownFields, $"{path}.resultSources[{j}].inertRest", found);
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

/// <summary>
/// One neighbour's claim on the declared area. See <see cref="BindingDocument.ReservedRegions"/> for why
/// this exists and for the limit on what it can prove.
/// </summary>
public sealed class ReservedRegionDocument
{
    /// <summary>First register, numbered from the area base. Never a <c>%M</c> byte address.</summary>
    public int? Register { get; set; }

    /// <summary>How many registers. A reservation of nothing protects nothing and is refused.</summary>
    public int? Length { get; set; }

    /// <summary>
    /// Who owns it, in words. <b>Required</b>: a refusal naming only a register range sends the reader to
    /// the mirror, which is the one place the problem is not.
    /// </summary>
    public string? Owner { get; set; }
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

    /// <summary>
    /// 🔴 <b>THE NAMED MIGRATION ESCAPE: this slot's UNDECLARED resting values may be assumed to be zero.</b>
    ///
    /// <para><b>It exists because the refusal it escapes lands on a slot that is DEPLOYED AND RUNNING</b>
    /// against the old hardcoded-zero behaviour. A fail-closed gate that refuses working submissions on the
    /// day it lands gets removed within a week, by someone who is right to — so the migration is one stated
    /// line here rather than a revert.</para>
    ///
    /// <para><b>It is not free.</b> <see cref="AssumedZeroRestBasis"/> is required; it is per SLOT, so it
    /// cannot be set project-wide by accident; every register it covers is counted and listed as
    /// <c>DEFAULTED</c> in the run's inert-rest report; and any inert failure on such a register says in
    /// its own text that nobody declared it — <b>so the reader is told the expectation may be the wrong
    /// half of the disagreement.</b></para>
    ///
    /// <para>A signal that declares its own <c>inertRest</c> is unaffected: the escape only ever fills a
    /// hole.</para>
    /// </summary>
    public bool AssumedZeroRest { get; set; }

    /// <summary>Why this slot's undeclared resting values may be assumed zero. <b>Required when <see cref="AssumedZeroRest"/> is set</b>, even if it currently covers nothing.</summary>
    public string? AssumedZeroRestBasis { get; set; }

    /// <summary>
    /// 🔴 <b>SCANS THE INERT CHECK MUST LET PASS BEFORE IT DECIDES THIS SLOT IS QUIESCENT — the wire field
    /// that did not exist, for the one knob designed to state <i>"this model takes N scans to settle"</i>.</b>
    ///
    /// <para><c>InertDeclaration.QuiescenceScans</c> has existed since the inert phase did, and the loop
    /// called <c>InertRestPlan.For(binding, wordOrder)</c> without the third argument — so it was
    /// permanently <b>1</b> and no document could say otherwise. <b>It cost a wave:</b> after a download
    /// the check wrote the next vector and sampled one scan (~24 ms) later, against a model whose settle
    /// after a contents step is ~9 seconds, and refused the block for being mid-integration.</para>
    ///
    /// <para><b>Absent means 1</b> — the floor, not a blank: two reads inside one scan cannot tell a
    /// settled value from a changing one. Below 1, or above what the poll budget can observe, is a refusal
    /// naming both numbers (<c>Harness.Wire.InertRestPlan</c>). <b>It does not enter the build stamp:</b>
    /// it changes how long the client waits before it looks and changes nothing that is downloaded.</para>
    /// </summary>
    public int? QuiescenceScans { get; set; }

    /// <summary>
    /// Hold the inert phase until this slot's block is at a known point in its own cycle. The name of a
    /// signal this slot already publishes as a result source; absent means no phase condition, and the
    /// test starts from wherever the block's clocks happen to be.
    ///
    /// <para>Where a block's windows are TUMBLING and the arm instant depends on how the previous index
    /// left the plant, a vector that cannot know the phase has to be written to survive every arm
    /// instant — and on the one wave that has run end to end, that hedge was about half of the dominant
    /// index. See <c>Harness.Map.SlotBinding.PhaseSignal</c>.</para>
    /// </summary>
    public string? PhaseSignal { get; set; }

    /// <summary>
    /// How <see cref="PhaseSignal"/> is recognised as restarted: <c>Decreases</c>, <c>Below</c> or
    /// <c>AtOrAbove</c>. <b>Required whenever <see cref="PhaseSignal"/> is set, and deliberately without a
    /// default</b> — <c>Decreases</c> waits for a wrap and costs up to a full window period while
    /// <c>Below</c> can be satisfied immediately, so choosing one on the author's behalf changes both what
    /// is proven and what the wave costs.
    /// </summary>
    public string? PhaseTrigger { get; set; }

    /// <summary>
    /// The comparison value for <c>Below</c> and <c>AtOrAbove</c>; refused with <c>Decreases</c>, which
    /// compares against the previous sample rather than a number.
    /// </summary>
    public uint? PhaseThreshold { get; set; }

    /// <summary>
    /// A signal that must be SET for a phase sample to count. <b>Required in practice for any clock that
    /// is not free-running:</b> an arm-gated timer reads 0 while disarmed, which is indistinguishable
    /// from "the window just restarted" — so without a guard the phase is confidently wrong rather than
    /// absent. Null means the clock free-runs, which is a claim about the block.
    /// </summary>
    public string? PhaseGuardSignal { get; set; }

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

    /// <summary>
    /// 🔴 <b>WHAT THIS SIGNAL READS WHEN NOTHING IS RUNNING — and until now the harness assumed ZERO for
    /// every published signal in the system.</b>
    ///
    /// <para>*** THE HARDCODED ZERO LIVED IN THE WAVE BUILDER, WHERE NO COORDINATOR COULD SEE IT: ***
    /// <c>Range(0, ResultRegistersNeeded).ToDictionary(i =&gt; i, _ =&gt; (ushort)0)</c>, feeding a type
    /// whose own summary says the expectation <i>must be declared</i>. <b>Fourth instance of the shape this
    /// file already records three times:</b> a capability documented as declared, supplied by the code
    /// instead, with no field a document could state it in.</para>
    ///
    /// <para><b>Absent is a REFUSAL, not a zero</b> — see <c>Harness.Wire.InertRestPlan</c>, which states
    /// the ruling and why the two alternatives were rejected. The migration for a slot that cannot be
    /// re-declared yet is <see cref="SlotBindingDocument.AssumedZeroRest"/>.</para>
    /// </summary>
    public InertRestDocument? InertRest { get; set; }

    /// <summary>Unknown keys at signal level — where a misspelt <c>specName</c> would otherwise vanish.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>
/// One signal's declared resting state, on the wire. <b>Exactly one of <see cref="Value"/> and
/// <see cref="Excluded"/></b>; see <c>Harness.Map.InertRest</c> for the semantics.
/// </summary>
public sealed class InertRestDocument
{
    /// <summary>
    /// The resting value, as text. <b>Signed</b> — a sentinel of <c>-1</c> is a real case and the register
    /// it lands in is unsigned on the wire. It is read through exactly the path a stimulus value is, so the
    /// signal's own <c>encoding</c> applies and a <c>Bool</c> takes <c>true</c>/<c>false</c>.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// 🔴 <b>This signal has NO meaningful resting value and inert is NOT gated on it — a positive claim.</b>
    ///
    /// <para>The motivating case is a ONE-SCAN PULSE, which reads 1 in about one sample of five: any
    /// single-sample expectation on it is a coin toss, and the coin toss gets blamed on the block.
    /// <b><see cref="Basis"/> is required here</b>, because an exclusion is the one declaration no machine
    /// will ever check.</para>
    /// </summary>
    public bool Excluded { get; set; }

    /// <summary>Why. <b>Required for <see cref="Excluded"/>, optional for a value</b> — a value is falsified by the device on every run; an exclusion is falsified by nobody.</summary>
    public string? Basis { get; set; }

    /// <summary>Unknown keys inside a resting-state declaration, folded into gate 0b like every other level.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }

    /// <summary>
    /// The domain form. <b>An empty object is not a declaration</b> — it states neither a value nor an
    /// exclusion, and is carried through as a malformed <c>Value</c> claim so the refusal names the signal
    /// rather than this class throwing without one.
    /// </summary>
    public InertRest ToRest() =>
        Excluded
            ? new InertRest(InertRestKind.Excluded, Value, (Basis ?? string.Empty).Trim())
            : new InertRest(InertRestKind.Value, Value, (Basis ?? string.Empty).Trim());
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
