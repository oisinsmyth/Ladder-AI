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

    /// <summary>
    /// 🔴 <b>WHAT THE PROGRAM GENERATES RATHER THAN HAS TYPED — the artifacts that belong to the PROGRAM
    /// rather than to one slot.</b>
    ///
    /// <para><see cref="SlotBindingDocument.Generate"/> covers the per-slot half: the slot FC and the
    /// stimulus head's index shell. These are the other half — the cyclic OB (one per program however many
    /// slots it drives) and the instance DBs (each belonging to the FB it instantiates, which may be a
    /// slot's or the comms block every slot shares). Between them, four of the lane's hand-authored
    /// <c>.ir</c> artifacts stop being typed.</para>
    ///
    /// <para><b>ABSENT MEANS NOT GENERATED, and it is REPORTED as such rather than passed over</b> — the
    /// rule <see cref="SlotBindingDocument.Generate"/> already follows. A lane written before this field
    /// existed behaves exactly as it did, and its run says in words that its OB and its instance DBs are
    /// AUTHORED.</para>
    /// </summary>
    public ProgramGenerationDocument? GenerateProgram { get; set; }

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

            // 🔴 THE GENERATION DECLARATION, TO ITS FULL DEPTH — and here a dropped key fails in the
            // QUIETEST direction of any level in this document. A misspelt `slotFcNumber` or `stimHead`
            // does not produce a wrong block; it produces NO block, reported as "nothing was declared,
            // so this is authored" — which is a true sentence about a document that plainly asked for
            // generation. Gate 0b is the only thing that can tell those two apart.
            Collect(slot.Generate?.UnknownFields, $"{path}.generate", found);
            Collect(slot.Generate?.StimulusHead?.UnknownFields, $"{path}.generate.stimulusHead", found);
            Collect(slot.Generate?.BlockUnderTest?.UnknownFields, $"{path}.generate.blockUnderTest", found);
            Collect(slot.Generate?.StimHead?.UnknownFields, $"{path}.generate.stimHead", found);

            foreach (var (phase, j) in (slot.Generate?.StimHead?.Phases ?? new List<StimPhaseDocument>()).Select((p, k) => (p, k)))
                Collect(phase.UnknownFields, $"{path}.generate.stimHead.phases[{j}]", found);

            // The stimulus UDT, to its full depth. A misspelt `datatype` on a member the shell does not
            // reference does not produce an untyped member — the generator refuses that by name — but a
            // misspelt `startValue` or `comment` produces a member silently missing the one thing a person
            // was asserting about it, which nothing downstream can tell from a member nobody annotated.
            Collect(slot.Generate?.StimUdt?.UnknownFields, $"{path}.generate.stimUdt", found);

            foreach (var (member, j) in (slot.Generate?.StimUdt?.Members ?? new List<StimUdtMemberDocument>()).Select((m, k) => (m, k)))
                Collect(member.UnknownFields, $"{path}.generate.stimUdt.members[{j}]", found);
        }

        // 🔴 THE PROGRAM-LEVEL GENERATION DECLARATION, TO ITS FULL DEPTH, AND IT FAILS EVEN QUIETER THAN
        // THE SLOT'S. A misspelt `calls` produces no OB and is reported as "the OB is AUTHORED"; a misspelt
        // `presets` produces an instance DB whose commissioning defaults are simply absent, and absent start
        // values are ZEROS on the controller, which is a plausible number rather than an error. Gate 0b is
        // the only thing that can tell a declaration that asked for something from one that did not.
        Collect(GenerateProgram?.UnknownFields, "binding.generateProgram", found);
        Collect(GenerateProgram?.CyclicOb?.UnknownFields, "binding.generateProgram.cyclicOb", found);

        // 🔴 THE COMMS FB, WHERE A DROPPED KEY IS WORSE STILL. A misspelt `localPort` or `connectionId`
        // does not produce a wrong number, it produces a REFUSAL naming all three endpoint fields — loud,
        // and pointing at the two the author did type. But a misspelt `memoryLayout` silently omits the
        // line, which is the state this generator's absent-layout obligation exists to describe, and a
        // misspelt `networkComment` costs the block the only prose that says what it serves.
        Collect(GenerateProgram?.CommsFb?.UnknownFields, "binding.generateProgram.commsFb", found);

        foreach (var (call, index) in (GenerateProgram?.CyclicOb?.Calls ?? new List<ObCallDocument>()).Select((c, i) => (c, i)))
            Collect(call.UnknownFields, $"binding.generateProgram.cyclicOb.calls[{index}]", found);

        foreach (var (db, index) in (GenerateProgram?.InstanceDbs ?? new List<InstanceDbDocument>()).Select((d, i) => (d, i)))
        {
            var path = $"binding.generateProgram.instanceDbs[{index}]";
            Collect(db.UnknownFields, path, found);

            foreach (var (preset, j) in (db.Presets ?? new List<InstanceDbPresetDocument>()).Select((p, k) => (p, k)))
                Collect(preset.UnknownFields, $"{path}.presets[{j}]", found);
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

    /// <summary>
    /// 🔴 <b>WHAT THIS LANE'S TEST SIDE IS GENERATED FROM RATHER THAN TYPED — and until it existed, two
    /// working generators had no production caller at all.</b>
    ///
    /// <para>Standing a conformance lane up meant hand-authoring eight <c>.ir</c> artifacts, two of which —
    /// the slot FC and the stimulus head's 18-network index shell — are derived <b>exactly</b> from a
    /// handful of declared names and three declared lists. <c>Harness.Map.SlotFcGenerator</c> and
    /// <c>Harness.Map.StimShellGenerator</c> both existed, both were tested, and neither was reachable from
    /// <c>harness-run</c> or <c>harness-batch</c>: the work they remove was still being done by hand.</para>
    ///
    /// <para><b>ABSENT MEANS NOT GENERATED, and it is REPORTED as such rather than passed over.</b> A lane
    /// written before this field existed behaves exactly as it did, and its run says in words that its slot
    /// FC and its head are AUTHORED. The one thing that must never happen is the reverse reading — an
    /// authored object counted as generated — because the question the origin field answers is <i>how much
    /// of this lane is still hand-built</i>.</para>
    ///
    /// <para><b>It is DATA, deliberately, and it is kept small.</b> Moving hand-authoring out of IR and into
    /// a declarative document is the whole point; moving it into a LARGE document is not. Everything
    /// derivable is derived — the roles in the lane manifest, the call-site obligation, the shell's required
    /// statics and UDT members — and what is left is the handful of facts no code can know.</para>
    /// </summary>
    public LaneGenerationDocument? Generate { get; set; }

    /// <summary>Unknown keys at slot level. Folded into gate 0b — see <see cref="BindingDocument.UnknownFields"/>.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>
/// One slot's generation declaration. See <see cref="SlotBindingDocument.Generate"/>.
///
/// <para>🔴 <b>A PARTIAL slot-FC declaration is REFUSED, not partially honoured.</b> The FC's entire content
/// is its two CALLs in order, so a declaration naming the block and not the block under test describes no
/// block — <c>Harness.Map.LaneGenerator</c> names the missing half rather than filling it in.</para>
/// </summary>
public sealed class LaneGenerationDocument
{
    /// <summary>The generated slot FC's name.</summary>
    public string? SlotFcName { get; set; }

    /// <summary>
    /// Its block number. <b>Required alongside the name and never defaulted</b> — hard rule 3 forbids
    /// inventing one, and harness objects come from the reserved 9000–9999 range the caller allocates from
    /// (<c>converter claim --allocate --kind block-number --type FC --floor 9000</c>).
    /// </summary>
    public int? SlotFcNumber { get; set; }

    /// <summary>
    /// The block the slot calls FIRST. <b>That order is not a parameter anywhere</b>: the head commands the
    /// plant model for this scan and the block under test must see this scan's commands, so reversing them
    /// costs one scan of latency on every transition and shows up only as vectors timing out near their
    /// backstop. See <c>Harness.Map.SlotFcGenerator</c>.
    /// </summary>
    public CalledBlockDocument? StimulusHead { get; set; }

    /// <summary>The block the slot calls SECOND — the subject of the lane.</summary>
    public CalledBlockDocument? BlockUnderTest { get; set; }

    /// <summary>
    /// The head spec the index shell is derived from, or absent. <b>Independent of the three fields
    /// above</b>: a lane may generate its slot FC while its head stays wholly authored, and the run's report
    /// says which of the two happened.
    /// </summary>
    public StimHeadSpecDocument? StimHead { get; set; }

    /// <summary>
    /// 🔴 <b>The stimulus UDT, or absent. DEPENDENT on <see cref="StimHead"/>, and the only field here
    /// that is</b> — <c>Harness.Map.StimUdtGenerator</c> derives the member set and every type it can from
    /// the EMITTED RUNGS, so with no shell generated there is nothing to derive from and declaring one is
    /// refused rather than answered from the spec instead.
    ///
    /// <para>*** IT WAS UNREACHABLE FROM HERE FOR FIVE DAYS AND THE COMMIT THAT ADDED IT SAID OTHERWISE.
    /// *** <c>d61652c</c> claimed <i>"<c>LaneDeclaration.StimUdt</c> through <c>LaneGenerator</c>"</i> was
    /// wired to production; <c>LoopCli.ToLaneDeclaration</c> constructed a five-argument
    /// <c>LaneDeclaration</c> and this class had no <c>stimUdt</c> property at all, so a binding that
    /// declared one ran clean and reported the type AUTHORED. <b>A field nobody can set is a field that
    /// does not exist</b>, however well it is implemented downstream — the rule
    /// <see cref="MirroredSignalDocument.Transient"/> already records twice over.</para>
    /// </summary>
    public StimUdtDocument? StimUdt { get; set; }

    /// <summary>Unknown keys here. Folded into gate 0b — a misspelt key would silently mean "not generated".</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>
/// One slot's stimulus UDT, on the wire. See <c>Harness.Map.StimUdtGenerator</c> for what is DERIVED from
/// the emitted rungs and what is refused; this type is only its JSON shape.
///
/// <para>Naming is FLATTENED into this object rather than nested, matching <see cref="CyclicObDocument"/>
/// and <see cref="InstanceDbDocument"/>, which flatten theirs. The domain records nest, and a wire shape
/// that mirrored that nesting would be a second nesting convention inside one document.</para>
/// </summary>
public sealed class StimUdtDocument
{
    /// <summary>The generated UDT's name.</summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// The type's own comment. <b>Required</b>: this type is the vocabulary a model is commanded in, and a
    /// comment describing that is a description of the plant, which no generator originates.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// The declared half — every member the rungs do not prove, and every type, start value and comment the
    /// rungs cannot fix. <b>Absent or empty is legitimate and is not a claim that there are none</b>: the
    /// generator refuses BY NAME each member it cannot type, so an under-declared UDT fails loudly.
    /// </summary>
    public List<StimUdtMemberDocument>? Members { get; set; }

    /// <summary>Unknown keys, folded into gate 0b like every other level.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>One declared UDT member, on the wire. See <c>Harness.Map.StimUdtMember</c> for the three jobs it does.</summary>
public sealed class StimUdtMemberDocument
{
    /// <summary>The member's name, without the <c>Stim.</c> root.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// The IR datatype. <b>Required for a member the shell does not reference</b>; for one it does, it is
    /// CHECKED against the derived type rather than trusted — a declaration contradicting the rungs is a
    /// refusal, not an override.
    /// </summary>
    public string? Datatype { get; set; }

    /// <summary>The member's start value, verbatim as IR writes it. A start value is a preset and a preset is a claim about the plant.</summary>
    public string? StartValue { get; set; }

    /// <summary>The member's own comment. Optional; its absence is REPORTED rather than filled.</summary>
    public string? Comment { get; set; }

    /// <summary>Unknown keys, folded into gate 0b like every other level.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>One of the slot FC's two calls, on the wire.</summary>
public sealed class CalledBlockDocument
{
    /// <summary>The FB or FC being called.</summary>
    public string? Block { get; set; }

    /// <summary>
    /// Its instance DB, or a dotted multi-instance path. <b>Absent is a CLAIM that this is an FC</b>, which
    /// is stateless and has no instance — the emitted call then omits the leading positional argument.
    /// </summary>
    public string? Instance { get; set; }

    /// <summary>
    /// The network's title. <b>Required, and the generator will not invent one</b> (C-201): a generated
    /// "Network 1" passes review and tells a reader nothing.
    /// </summary>
    public string? NetworkTitle { get; set; }

    /// <summary>Unknown keys, folded into gate 0b like every other level.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>
/// A stimulus head declared, on the wire — the input <c>Harness.Map.StimShellGenerator</c> derives the
/// index shell from. See <c>Harness.Map.StimHeadSpec</c> for what each field means and for the refusals,
/// several of which are computed from the emitted rungs rather than from a list somebody maintains.
/// </summary>
public sealed class StimHeadSpecDocument
{
    /// <summary>The head's block name. Used to name the emitted fragment; never emitted into the rungs.</summary>
    public string? HeadName { get; set; }

    /// <summary>The block-under-test's fault-reset input, as a full member path. Owned by S9 and written by nothing else.</summary>
    public string? UutReset { get; set; }

    /// <summary>The index watchdog's preset, as a Time literal.</summary>
    public string? Watchdog { get; set; }

    /// <summary>The three cleardown dwells, as a Time literal.</summary>
    public string? Dwell { get; set; }

    /// <summary>Ordered. The one design decision that settles four networks at once.</summary>
    public List<StimPhaseDocument>? Phases { get; set; }

    /// <summary>
    /// Every latched cause the block can hold. 🔴 <b>The most dangerous field here</b>: a cause left out is
    /// one that every cleardown outcome reports as absent, forever, in green. Empty is permitted and is
    /// REPORTED, because "nothing to clear" and "nobody listed anything" must not look the same.
    /// </summary>
    public List<string>? Causes { get; set; }

    /// <summary>Every one-scan counting edge. Empty omits the counting network entirely.</summary>
    public List<string>? CycleEdges { get; set; }

    /// <summary>Every published bit the re-arm network must clear. Checked against what the shell actually SETS.</summary>
    public List<string>? OutcomeBits { get; set; }

    /// <summary>Unknown keys, folded into gate 0b like every other level.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>One phase of the index, on the wire.</summary>
public sealed class StimPhaseDocument
{
    /// <summary>The membership bit this phase drives (a static Bool).</summary>
    public string? Bit { get; set; }

    /// <summary>The boundary static holding the time this phase ends at.</summary>
    public string? End { get; set; }

    /// <summary>How long it lasts — a static or a UDT member, <b>never a literal</b>, or the index can only be re-timed by regenerating the block.</summary>
    public string? Duration { get; set; }

    /// <summary>
    /// <c>Disarm</c>, <c>Reset</c>, <c>Verify</c>, <c>Scenario</c> or <c>Settle</c>. <b>Only <c>Reset</c> is
    /// read by the generator</b> — it builds the reset pulse from the phases carrying it; the rest document
    /// intent. An unrecognised value is refused rather than treated as unstated: a typo'd <c>Reset</c> would
    /// silently produce a head that can never clear the block under test.
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>Unknown keys, folded into gate 0b like every other level.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>
/// The program-level generation declaration, on the wire. See <see cref="BindingDocument.GenerateProgram"/>
/// and <c>Harness.Map.ProgramGenerator</c>.
///
/// <para><b>It is DATA, deliberately, and it is kept small.</b> Moving hand-authoring out of IR and into a
/// declarative document is the whole point; moving it into a LARGE document is not. Everything derivable is
/// derived — every instance DB member and marker from the FB's interface, every ordering obligation from the
/// lane, the OB's system parameters from its event class — and what is left is the handful of facts no code
/// can know: the names, the numbers, the order, and the presets.</para>
/// </summary>
public sealed class ProgramGenerationDocument
{
    /// <summary>The cyclic OB. Absent means it is AUTHORED, which is reported.</summary>
    public CyclicObDocument? CyclicOb { get; set; }

    /// <summary>The instance DBs. Absent or empty means every one of them is AUTHORED, which is reported.</summary>
    public List<InstanceDbDocument>? InstanceDbs { get; set; }

    /// <summary>
    /// 🔴 <b>The Modbus server, or absent. One per PROGRAM</b> — it serves ONE window, and
    /// <c>converter served-area</c> refuses a corpus holding two <c>MB_SERVER</c> calls outright, because
    /// which one serves the harness mirror is then not derivable.
    ///
    /// <para>*** UNREACHABLE FROM HERE FOR FIVE DAYS, ALONGSIDE <see cref="LaneGenerationDocument.StimUdt"/>
    /// AND FOR THE SAME REASON. *** <c>LoopCli.ToProgramDeclaration</c> constructed a two-argument
    /// <c>ProgramDeclaration</c> and this class had no <c>commsFb</c> property, so a binding declaring one
    /// reported <c>no comms FB was declared, so none was generated</c> — <b>a true sentence about a document
    /// that plainly asked for generation</b>, which this file's gate-0b note already names as the quietest
    /// failure of any level here.</para>
    ///
    /// <para>The served WINDOW is deliberately not on the wire: it is <see cref="BindingDocument.BaseByte"/>
    /// and <see cref="BindingDocument.DeclaredRegisters"/>, handed to the generator as the map's own geometry
    /// so the block cannot serve an area the map was never allocated against.</para>
    /// </summary>
    public CommsFbDocument? CommsFb { get; set; }

    /// <summary>Unknown keys, folded into gate 0b like every other level.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>
/// The comms FB, on the wire. See <c>Harness.Map.CommsFbGenerator</c> for what is derived and what is
/// refused; this type is only its JSON shape, flattened as <see cref="CyclicObDocument"/> is.
///
/// <para><b>The connection SHAPE is not here.</b> <c>CommsConnectionShape</c> has exactly one member —
/// passive TCP accepting any client, whose four <c>TCON_IP_v4</c> values are read off the one committed
/// server block — so a wire field offering that single choice would set nothing and reach nothing. A second
/// shape needs its own export behind it, and the key arrives in the same commit that grounds it.</para>
/// </summary>
public sealed class CommsFbDocument
{
    /// <summary>The generated FB's name.</summary>
    public string? BlockName { get; set; }

    /// <summary>
    /// Its number. <b>Required and never defaulted</b> — hard rule 3 forbids inventing one, and harness
    /// objects come from the reserved 9000–9999 range the caller allocates from
    /// (<c>converter claim --allocate --kind block-number --type FB --floor 9000</c>).
    /// </summary>
    public int? BlockNumber { get; set; }

    /// <summary>The block's TITLE. Required; the generator will not invent one.</summary>
    public string? Title { get; set; }

    /// <summary>
    /// The block's own COMMENT. <b>Required.</b> It may write the served window as the placeholders
    /// <c>{base}</c> and <c>{registers}</c>, which the generator fills from the geometry — so the prose
    /// cannot fall behind the area pointer beside it. A number typed here instead is CHECKED against the
    /// geometry, not trusted.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// 🔴 The TCP port the server listens on. <b>Required.</b> 502 is the Modbus registered port and that is
    /// exactly why it is not a default: the port a rig listens on is a commissioning decision, it IDENTIFIES
    /// the instance, and a wrong one is not a compile error but a connection that never establishes.
    /// </summary>
    public int? LocalPort { get; set; }

    /// <summary>The <c>CONN_OUC</c> connection ID, verbatim as IR writes it (<c>16#0010</c>, or a decimal). Required; unique per connection on the CPU.</summary>
    public string? ConnectionId { get; set; }

    /// <summary>
    /// The <c>HW_ANY</c> hardware identifier of the PROFINET interface to listen on. <b>Required — it names
    /// a piece of hardware, which hard rule 3 forbids inventing outright</b>, so there is nothing to default
    /// it to even in principle. Read it off the device's own configuration, never from another project's block.
    /// </summary>
    public int? InterfaceId { get; set; }

    /// <summary>The one network's title. Required (C-201).</summary>
    public string? NetworkTitle { get; set; }

    /// <summary>The one network's comment. Required, and it takes the same two placeholders the block comment does.</summary>
    public string? NetworkComment { get; set; }

    /// <summary>
    /// 🔴 <b>The <c>MEMORYLAYOUT</c> line, or absent to omit it — and ABSENT IS THE DELIBERATE DEFAULT,
    /// not an oversight.</b>
    ///
    /// <para>The layout that actually runs is asserted after every import by
    /// <c>openness-cli block-layout --set Standard --yes</c>, because a TIA import silently reverts a block
    /// to <c>Optimized</c>. A line here is a SECOND, WEAKER statement of the same fact, free to disagree
    /// with the one that runs — so the generator originates none, and a caller who states one is told in an
    /// obligation that the import can revert it.</para>
    ///
    /// <para><b>Consequence, measured 2026-08-27:</b> a generated block compared against its own TIA
    /// re-export is <c>converter compare</c> exit 2 — <i>only one document declares a MemoryLayout</i> —
    /// which is the documented input pair for <c>--allow-silent-layout</c>, not a defect in the block. The
    /// generators say so in an obligation rather than leaving it to be remembered.</para>
    /// </summary>
    public string? MemoryLayout { get; set; }

    /// <summary>Unknown keys, folded into gate 0b like every other level.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>
/// The cyclic OB, on the wire. 🔴 <b><see cref="Calls"/> IS THE BLOCK</b> — see
/// <c>Harness.Map.CyclicObGenerator</c>: the order is the content, and the generator originates none of it.
/// </summary>
public sealed class CyclicObDocument
{
    /// <summary>The OB's name.</summary>
    public string? BlockName { get; set; }

    /// <summary>
    /// Its number. <b>Required and never defaulted</b>, and unlike every other harness object it does NOT
    /// come from the 9000–9999 band: an OB's number is fixed by its event class, which is why OBs are
    /// excluded from that band. The generator refuses a number inside it.
    /// </summary>
    public int? Number { get; set; }

    /// <summary>
    /// The event class. Only <c>ProgramCycle</c> is accepted — TIA requires an OB's system parameters to be
    /// present and informative, they differ by event class, and the committed corpus grounds one set.
    /// </summary>
    public string? SecondaryType { get; set; }

    /// <summary>The OB's title. Required; the generator will not invent one.</summary>
    public string? Title { get; set; }

    /// <summary>
    /// 🔴 <b>The OB's header COMMENT. Required — and until 2026-08-27 there was no such key, so NO BINDING
    /// COULD PRODUCE A C-201-COMPLIANT OB.</b>
    ///
    /// <para><c>converter preflight</c> refuses a generated OB with <c>[review:C-201] [Error] … has no
    /// header comment</c>. TIA imports and compiles it happily, so nothing on the device path caught it;
    /// the conventions gate did. <b>The 9000–9999 harness exemption does not apply</b> — an OB's number is
    /// fixed by its event class, so it is excluded from that band and <c>review:harness-scope</c> cannot
    /// classify it, which it says out loud rather than excusing.</para>
    ///
    /// <para>Say what this OB sweeps and in what order. In the one block whose entire content IS an order,
    /// that is the sentence worth writing.</para>
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>🔴 <b>IN THE ORDER THEY WILL EXECUTE.</b> LAD runs networks in order and nothing here reorders them.</summary>
    public List<ObCallDocument>? Calls { get; set; }

    /// <summary>Unknown keys, folded into gate 0b like every other level.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>One call the cyclic OB makes.</summary>
public sealed class ObCallDocument
{
    /// <summary>The FB or FC being called.</summary>
    public string? Block { get; set; }

    /// <summary>
    /// Its instance DB, or a dotted multi-instance path. <b>Absent is a CLAIM that this is an FC</b>, which
    /// has no instance; the emitted call then omits the leading positional argument.
    /// </summary>
    public string? Instance { get; set; }

    /// <summary>The network's title. Required (C-201); the generator will not invent one.</summary>
    public string? NetworkTitle { get; set; }

    /// <summary>
    /// Why the call sits where it sits. Optional — but in the one block whose entire content is an ORDER,
    /// this is where an ordering argument belongs.
    /// </summary>
    public string? NetworkComment { get; set; }

    /// <summary>Unknown keys, folded into gate 0b like every other level.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>
/// One instance DB, on the wire. 🔴 <b>Its STRUCTURE is a mechanical projection of the FB's interface and is
/// never declared here; its START VALUES are presets, and a preset is a claim about the plant.</b>
/// </summary>
public sealed class InstanceDbDocument
{
    /// <summary>The DB's name.</summary>
    public string? DbName { get; set; }

    /// <summary>
    /// Its number. <b>Required and never defaulted</b> — hard rule 3 forbids inventing one, and harness
    /// objects come from the reserved 9000–9999 range
    /// (<c>converter claim --allocate --kind block-number --type DB --floor 9000</c>).
    /// </summary>
    public int? DbNumber { get; set; }

    /// <summary>The FB this instantiates. Its IR must be in the program set — a name alone is not something to project from.</summary>
    public string? Fb { get; set; }

    /// <summary>
    /// The DB's own comment. <b>Required</b>: every DB in the committed corpus carries one, generated code is
    /// held to a stricter bar than site code, and an invented "Instance of FB_X" tells a reader nothing.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// 🔴 <b><c>projectedFromFb</c> or <c>leftToTia</c>, and there is NO DEFAULT</b> — the two differ in what
    /// happens to the FB's own start values. <c>projectedFromFb</c> copies the structure and drops every start
    /// value unless a preset declares it; <c>leftToTia</c> emits an empty MEMBERS section, so TIA fills the
    /// instance from the FB INCLUDING its start values, and a second instance declared that way inherits the
    /// same ones (which is refused).
    /// </summary>
    public string? Members { get; set; }

    /// <summary>The start values this instance carries, by member path. See <c>Harness.Map.InstanceDbPreset</c>.</summary>
    public List<InstanceDbPresetDocument>? Presets { get; set; }

    /// <summary>Unknown keys, folded into gate 0b like every other level.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? UnknownFields { get; set; }
}

/// <summary>
/// One declared start value. 🔴 <b>Exactly one of <see cref="Value"/> and <see cref="Cleared"/>; neither is a
/// declaration that has not decided, and both are opposite claims about what the instance starts at.</b>
/// </summary>
public sealed class InstanceDbPresetDocument
{
    /// <summary>Dotted member path from the top of the MEMBERS section (<c>Stim.ModelThreshold</c>, <c>Comms.RemoteAddress.ADDR[1]</c>).</summary>
    public string? Path { get; set; }

    /// <summary>The start value, verbatim as IR writes it (<c>T#60S</c>, <c>16#0010</c>, <c>503</c>).</summary>
    public string? Value { get; set; }

    /// <summary>
    /// The explicit "this instance deliberately starts at the type default". It exists so that DROPPING a
    /// start value the FB declares is also something somebody typed.
    /// </summary>
    public bool Cleared { get; set; }

    /// <summary>Unknown keys, folded into gate 0b like every other level.</summary>
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
