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
/// claims, no coverage, no deferred queue, no cleanup, no time compression, no
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
    /// The order types are emitted in — <b>every COIL type before every MOVE type, and that is the IR's
    /// rule rather than a preference.</b>
    ///
    /// <para><c>ir/SPEC.md</c>: statements within one network are grouped by kind in a fixed order, and
    /// <c>COIL</c> comes before <c>MOVE</c>. This generator emits one network per TYPE rather than one
    /// ordered network, so the rule cannot be broken — but the networks are emitted in the legal order
    /// anyway, so merging them later would remain legal. <b>Derived from the element table's own shapes</b>,
    /// so a new row lands in the right place without anybody editing a second list that could disagree.</para>
    /// </summary>
    private static IReadOnlyList<MirrorElement> RenderOrder =>
        MirrorElements.All.OrderBy(e => e.Shape == MirrorCopyShape.Coil ? 0 : 1).ThenBy(e => e.Type).ToArray();

    /// <summary>
    /// The mirror tag for one register, <b>typed, addressed and SIZED together</b> — the three things the
    /// hard-coded <c>"Int"</c> got wrong at once.
    ///
    /// <para>Everything here is read out of <see cref="MirrorElements"/>. A Bool takes <b>bit 0 of its own
    /// register</b> — bit 0 so a client reading the register as a 16-bit word tests <c>value &amp; 1</c>,
    /// the same convention <see cref="MirrorGeometry.BitAddressOf"/> already applies to the start bools,
    /// which keeps ONE bit-order question in this system rather than two. A Time takes a <c>%MD</c> and
    /// therefore <b>two</b> registers — the same span the version register and the scan counter already
    /// occupy, and the same (now measured) word order.</para>
    /// </summary>
    private static MirrorTag MirrorTagFor(string name, MirrorValueType type, MirrorGeometry geometry, int register, string comment)
    {
        var element = MirrorElements.Require(type);

        var address = element.Form switch
        {
            MirrorAddressForm.Bit => geometry.BitAddressOf(register, 0),
            MirrorAddressForm.Word => geometry.WordAddressOf(register),
            MirrorAddressForm.DoubleWord => geometry.DoubleWordAddressOf(register),
            _ => throw new InvalidOperationException($"address form {element.Form} has no rendering."),
        };

        // Said on the TAG, because the register span is what a client has to get right and the word order
        // inside it is the thing a client can get wrong.
        //
        // 🔴 *** THE WORD "UNCALIBRATED" BELOW IS NOW STALE AND IS DELIBERATELY NOT CHANGED YET. *** The
        // order was measured HighWordFirst on 2026-08-13 and 2026-08-14 (see RegisterWordOrder). This
        // string is GENERATED IR TEXT, not a doc comment: editing it changes the copy layer's tag table
        // on disk, and a lane is running against a deployed build right now. It would NOT move the build
        // stamp — BuildStamp hashes the program under test, the map, the bindings and the naming, not the
        // generated text — so the change would be invisible to the verifying gateway while making the
        // committed IR differ from what is loaded. That is the quiet divergence this project keeps
        // getting bitten by, so it waits for a window with no run in flight.
        var note = element.Form switch
        {
            MirrorAddressForm.Bit => " Bool, bit 0 of this register.",
            MirrorAddressForm.DoubleWord =>
                $" {element.IrDataType}, 32-bit: registers {register} and {register + 1}, reassembled under the configurable word order, WHICH IS UNCALIBRATED.",
            _ => string.Empty,
        };

        return new MirrorTag(name, element.IrDataType, address, geometry.ByteAddressOf(register), comment + note);
    }

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

            // *** A SLOT ID IS A CROSS-REFERENCE KEY AND A TAG FRAGMENT, AND THOSE ALPHABETS DIFFER. ***
            // This used to demand a plain identifier outright, which made the deliverable's own binding
            // ungeneratable: the vectors say `SLOT-HBA-RAISE`, the binding must say the same string for the
            // two to join, and a hyphen cannot appear in a PLC tag name. Measured on the committed
            // single-slot binding, which returned `slot id 'SLOT-HBA-ALL' is not a plain identifier` and
            // emitted nothing. The id is now kept as the key and TRANSLITERATED for tag names, reported per
            // slot, with a collision refused below.
            if (!SlotTagToken.CanDerive(binding.SlotId))
            {
                refusals.Add(
                    $"slot id '{binding.SlotId}' carries no letter or digit, so no identifier can be derived from it for the tag "
                    + "names this generator emits. A slot id may contain any characters a vector's `slot` field can cite - they are "
                    + "transliterated for the tag fragment - but it must carry something to transliterate.");
            }

            var targets = binding.VectorTargets ?? Array.Empty<MirroredSignal>();
            var sources = binding.ResultSources ?? Array.Empty<MirroredSignal>();

            if (sources.Count == 0)
                refusals.Add($"binding for slot '{binding.SlotId}' wires no result sources. A slot that publishes nothing produces a result region of zeros indistinguishable from a real one.");

            // *** WIDTH, NOT COUNT. *** A Time occupies two registers, so a binding of three signals can
            // need four. Comparing a signal COUNT against a REGISTER allocation was true only while every
            // type was one register wide, which is exactly the assumption that broke.
            if (slot is not null && binding.VectorRegistersNeeded > slot.Vector.Length)
                refusals.Add($"binding wires {targets.Count} vector target(s) needing {binding.VectorRegistersNeeded} register(s), but slot '{binding.SlotId}' was allocated {slot.Vector.Length} vector register(s). A 32-bit element occupies two.");

            if (slot is not null && binding.ResultRegistersNeeded > slot.Result.Length)
                refusals.Add($"binding wires {sources.Count} result source(s) needing {binding.ResultRegistersNeeded} register(s), but slot '{binding.SlotId}' was allocated {slot.Result.Length} result register(s). A 32-bit element occupies two.");

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

            // *** THE PHASE-ARMED LATCH, AND THE ONE CASE THAT IS STILL INEXPRESSIBLE. ***
            //
            // A transient that re-arms each index gets
            //     SCOIL <latch> := <slot start bool> AND [<arm>] AND <signal>
            //     RCOIL <latch> := NOT <slot start bool>
            // which is the shape the hand-authored FB_HarnessViolationLatch already uses. THE PER-INDEX
            // WINDOW IS THE START BOOL: InertPhase.Establish lowers every start bool before EVERY index
            // and Commit raises it after the verify, so the mirror already carries a client-driven level
            // that goes low between indices. No arm REGISTER and no new client verb were needed — the
            // seam already ships, and building a second one would have cost a register to duplicate it.
            //
            // WHAT IS STILL REFUSED, BY NAME: a slot whose StartCondition is null. D37 makes that null a
            // CLAIM — "this block is purely reactive and is held inert by its input values alone" — and
            // such a slot has NO per-index level of any kind. There is nothing to arm on and nothing to
            // clear on, so the only latch expressible there is the unconditional one, which is exactly
            // the artifact whose symptom is predicted findings quietly absent on the rig.
            foreach (var signal in sources.Where(s => s is { PhaseArmed: true }))
            {
                if (binding.StartCondition is null)
                {
                    refusals.Add(
                        $"result source '{signal.Tag}' on slot '{binding.SlotId}' declares RearmsEachIndex, and THE COPY LAYER CANNOT EXPRESS IT ON THIS SLOT. "
                        + "A phase-armed latch is armed and re-cleared by the slot's START BOOL — the one level the client drives low before every index and high at the commit — "
                        + $"and slot '{binding.SlotId}' binds a NULL start condition, which under D37 is the positive claim that this block has no start gate at all. "
                        + "With no per-index level there is nothing to arm on and nothing to clear on, so the only latch available is the unconditional one: it would set on the first firing and stay set for the rest of the wave, "
                        + "index 2 would read identical to index 1, and a signal that never fired again would read as one that did. "
                        + $"THE TWO ROUTES THAT WORK: bind this slot's real start condition, or latch '{signal.Tag}' in the block under test and declare it with LatchedBy, which is admitted on provenance. "
                        + "Refusing here rather than emitting the latch that compiles is deliberate: the wrong one is only discoverable on the rig.");
                }

            }

            // *** A BLANK ArmedBy IS A CALLER WHO MEANT TO NAME A SIGNAL, ON ANY RESULT SOURCE. *** This
            // used to sit inside the PhaseArmed loop above, which was right while a latch was the only
            // consumer of an arm window; with the evaluation consumer below it is not, so a blank on a
            // non-latching source is now the same caller error and is refused in the same words.
            foreach (var signal in sources.Where(s => s is { ArmedBy: not null, ArmWindowStated: false }))
            {
                refusals.Add(
                    $"result source '{signal.Tag}' on slot '{binding.SlotId}' states a BLANK arm window. "
                    + "An empty ArmedBy is not the same as an absent one: absent claims no in-index window and leaves the whole index armed, while blank is a caller who meant to name a signal. "
                    + "Name the tag, or omit the field.");
            }

            // 🔴 *** AN ARM WINDOW THAT ARMS NOTHING IS REFUSED — AND SINCE 2026-08-18 THERE ARE TWO WAYS
            // TO ARM SOMETHING, NOT ONE (D2). ***
            //
            // The refusal used to read `ArmWindowStated && !PhaseArmed`, i.e. an arm window is only
            // meaningful on a Transient && RearmsEachIndex signal. THAT WAS TRUE WHEN IT WAS WRITTEN AND
            // STOPPED BEING TRUE ON 2026-08-17. `armedBy` then had exactly one consumer — the generated
            // latch's arm term, which genuinely needs a momentary signal and a latch to arm. It has had a
            // SECOND consumer since: `SlotBinding.ArmRegisterOf`, which arms the EVALUATION of a `Sampled`
            // series. That consumer reads the arm tag's own mirrored register out of the same FC03 the
            // harness already makes every poll, narrows the considered frames to the ones taken while the
            // window was open, and NEEDS NO LATCH, NO STICKY BIT AND NO TRANSIENCE. A level is not merely
            // acceptable to it — a level is the natural shape of a phase flag.
            //
            // *** WHAT THE STALE PREMISE COST, MEASURED ON THE LIVE BINDING: *** six arm windows had to be
            // declared `transient` to get past this refusal, FIVE OF THEM ON LEVELS. Each generated a latch
            // register nothing would ever read: band 95 -> 101, mirror 164 -> 170, which then forces a
            // width change in a PLC source file declared in two places where a mismatch is silent, a
            // re-import, a re-compile and a re-download. One narrowed condition removes all six.
            //
            // *** SO THE QUESTION IS "DOES THIS ARM ANYTHING", NOT "IS THERE A LATCH". *** It arms
            // something if it arms a generated latch (PhaseArmed) OR if the arm tag is itself a result
            // source of THIS slot, because that is precisely and only when ArmRegisterOf resolves it —
            // the same condition, read off the same collection, so the two cannot drift apart. An arm tag
            // the slot does not publish still arms nothing observable and is still refused, which is the
            // half of the old rule that was never stale.
            foreach (var signal in sources.Where(s => s is { ArmWindowStated: true, PhaseArmed: false }))
            {
                // BY TAG, matching SlotBinding.ArmRegisterOf exactly: that lookup compares ArmWindow
                // against each result source's Tag, never against its JoinKey. Comparing anything else
                // here would admit a binding whose window then reads as UNKNOWN at every frame.
                var armIsMirrored = sources.Any(s =>
                    s is not null && string.Equals(s.Tag, signal.ArmWindow, StringComparison.Ordinal));

                if (armIsMirrored)
                    continue;

                refusals.Add(
                    $"result source '{signal.Tag}' on slot '{binding.SlotId}' names an arm window ('{signal.ArmWindow}') that ARMS NOTHING. "
                    + "An arm window has exactly two consumers, and this signal reaches neither. "
                    + "(1) A GENERATED LATCH, which needs the signal declared BOTH Transient and RearmsEachIndex — "
                    + (string.IsNullOrWhiteSpace(signal.LatchedBy)
                        ? "this signal declares neither, so it is mirrored Sampled."
                        : $"this signal declares LatchedBy '{signal.LatchedBy}', and the arming of a hand-authored latch lives INSIDE that block — it is not this generator's to emit.")
                    + $" (2) THE EVALUATION WINDOW, which needs '{signal.ArmWindow}' to be a RESULT SOURCE OF THIS SLOT so the observer can read it out of the same result band — it is not one here, "
                    + "so every frame would come back with the window state UNKNOWN and the declaration would change no verdict. "
                    + $"THE TWO ROUTES THAT WORK: mirror '{signal.ArmWindow}' as a result source of this slot (no latch, no Transient, no extra register beyond the arm tag itself), "
                    + "or declare this signal Transient AND RearmsEachIndex so the copy layer emits a phase-armed latch. Or drop the arm window.");
            }

            foreach (var (signal, where) in typed)
            {
                if (signal is null)
                {
                    refusals.Add($"a {where} on slot '{binding.SlotId}' is null.");
                    continue;
                }

                if (MirrorElements.For(signal.Type) is null)
                {
                    refusals.Add(
                        $"{where} '{signal.Tag}' on slot '{binding.SlotId}' has type {signal.Type}, which this generator cannot mirror. "
                        + $"Supported: {MirrorElements.Supported}. "
                        + (signal.Type == MirrorValueType.Unstated
                            ? "*** UNSTATED IS A REFUSAL AND NOT A DEFAULT. *** Assuming Int here is exactly the defect this refusal exists to prevent: it renders a plain MOVE, which TIA rejects for a Bool with \"Data type Bool is not permitted here\" — after a full import."
                            : "Widening this is a ROW in MirrorElements - data type, address form (which is where the register width comes from) and rung shape together, verified against TIA. Never an enum member on its own."));
                }
            }
        }

        // *** THE TWO CROSS-SLOT CHECKS. *** Everything above is about ONE binding; a wave set of several
        // slots has two hazards that no per-binding check can see, and both were silent until 2026-08-14.
        refusals.AddRange(TokenCollisions(bindings));
        refusals.AddRange(MultipleWriters(bindings));

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

        // One derivation per slot, in map order, carried onto the plan so a reader of the RESULT meets it
        // rather than only a reader of this code. See SlotTagToken: the id is the vector's cross-reference
        // key, the token is what a PLC tag name can hold, and they are not the same alphabet.
        var tokens = ordered.Select(o => SlotTagToken.Derive(o.Binding.SlotId)).ToArray();

        foreach (var (allocation, binding) in ordered)
        {
            var token = SlotTagToken.For(binding.SlotId);

            if (binding.StartCondition is not null)
            {
                tags.Add(new($"{prefix}{token}_Start", "Bool",
                    geometry.BitAddressOf(allocation.StartBoolRegister, allocation.StartBitInRegister),
                    geometry.ByteAddressOf(allocation.StartBoolRegister),
                    "Start bool. Its rising edge is the test's T=0."));

                tags.Add(new($"{prefix}{token}_Ran", "Bool",
                    geometry.BitAddressOf(map.StartEcho.Register + (allocation.StartBoolRegister - map.StartBools.Register), allocation.StartBitInRegister),
                    geometry.ByteAddressOf(map.StartEcho.Register + (allocation.StartBoolRegister - map.StartBools.Register)),
                    "Latched: the block's own start condition was seen high. Cleared by the client at inert."));
            }

            // *** THE SUFFIX IS THE REGISTER OFFSET, NOT THE LIST POSITION. *** They were the same number
            // only while every element was one register wide. With a 32-bit element in the list, the tag
            // after a Time at R000 is R002 — and the gap is the point: it says, in the name, that R001 is
            // the Time's second half rather than a register nobody wired.
            var vectorTargets = binding.VectorTargets ?? Array.Empty<MirroredSignal>();
            var vectorOffsets = binding.VectorRegisterOffsets;

            for (var i = 0; i < vectorTargets.Count; i++)
            {
                var offset = vectorOffsets[i];
                tags.Add(MirrorTagFor($"{prefix}{token}_V{offset:000}", vectorTargets[i].Type, geometry,
                    allocation.Vector.Register + offset, $"Vector register {offset}."));
            }

            var resultOffsets = binding.ResultRegisterOffsets;

            for (var i = 0; i < binding.ResultSources.Count; i++)
            {
                var offset = resultOffsets[i];
                tags.Add(MirrorTagFor($"{prefix}{token}_R{offset:000}", binding.ResultSources[i].Type, geometry,
                    allocation.Result.Register + offset, $"Result register {offset}."));
            }

            // *** THE LATCH BAND, AFTER THE VALUES. *** A momentary signal is unobservable by sampling at
            // any rate - a poll IS one round trip - so the copy layer gives it a STICKY BIT the client
            // reads and clears, which is the shape Observability.Transient has always described. The
            // latches sit in their own band so that adding one moves no value offset.
            foreach (var (tag, offset) in binding.LatchRegisterOffsets.OrderBy(e => e.Value))
            {
                // BY TAG: LatchRegisterOffsets is keyed on the IR tag, because that key is emitted into
                // LAD below as the latch rung's own source expression. ResultSignal asks by the name a
                // VECTOR cites, which is a different key wherever a binding states a spec name.
                var signal = binding.ResultSignalByTag(tag);

                // *** THE COMMENT SAYS WHICH FORM THIS LATCH IS, because the tag table is what a person
                // reads in TIA and the two forms are indistinguishable from the address. *** Reading a
                // phase-armed latch as an unconditional one is how a client comes to clear a bit the copy
                // layer is already clearing, and reading it the other way is how index 1's firing is
                // attributed to index 2.
                tags.Add(MirrorTagFor($"{prefix}{token}_L{offset:000}", MirrorValueType.Bool, geometry,
                    allocation.Result.Register + offset,
                    signal is { PhaseArmed: true }
                        ? $"LATCH for '{tag}', PHASE-ARMED. Set only while this slot's start bool is high"
                          + (signal.ArmWindowStated ? $" AND '{signal.ArmWindow}' is true" : string.Empty)
                          + ", and RESET by the copy layer whenever the start bool is low - so every vector index re-arms it and index 2 cannot read index 1's firing. "
                          + "A momentary signal cannot be sampled at any rate, so this is the only way it is observable at all."
                        : $"LATCH for '{tag}'. Sticky: set by the copy layer when the signal is true, cleared by the CLIENT during inert. "
                          + "A momentary signal cannot be sampled at any rate, so this is the only way it is observable at all."));
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
            // The same derivation the tag table used. Computed from the id rather than carried across, so
            // the two cannot drift into naming different tags for one slot.
            var token = SlotTagToken.For(binding.SlotId);

            var targets = binding.VectorTargets ?? Array.Empty<MirroredSignal>();

            // *** ONE NETWORK PER TYPE, and the register index stays the LIST index. *** Splitting by
            // type is what keeps the IR's kind-ordering rule (COIL before MOVE) unreachable rather than
            // merely satisfied; keying the register off the position in the binding's list is what keeps
            // the client's `IndexOf(signal)` arithmetic true across the split.
            var targetOffsets = binding.VectorRegisterOffsets;

            foreach (var element in RenderOrder)
            {
                var ofType = targets
                    .Select((t, i) => (Signal: t, Offset: targetOffsets[i]))
                    .Where(x => x.Signal.Type == element.Type)
                    .Select(x => new CopyLayerCopy($"{prefix}{token}_V{x.Offset:000}", x.Signal.Tag, element.Type))
                    .ToArray();

                if (ofType.Length > 0)
                {
                    networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.VectorIn,
                        $"Vector in - slot {binding.SlotId} - {element.Type}", ofType));
                }
            }

            if (binding.StartCondition is not null)
            {
                networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.StartBool,
                    $"Start bool - slot {binding.SlotId}",
                    new[] { new CopyLayerCopy($"{prefix}{token}_Start", binding.StartCondition, MirrorValueType.Bool) }));

                // X-E. The echo reads the FAR side of the coil above — the block's OWN start condition,
                // which is what the program actually ran on. Reading back the mirror bit instead would
                // only report what the client wrote, which is the plan, and the plan is not evidence.
                //
                // LATCHED, because a poll gap is ~8.1 scans at the p99 and a short test can start and
                // finish between two polls. A level echo would then read low at both, and the log would
                // say the slot never ran — which is exactly the false evidence X-E exists to kill.
                networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.StartEcho,
                    $"Start echo - slot {binding.SlotId}",
                    new[] { new CopyLayerCopy($"{prefix}{token}_Ran", binding.StartCondition, MirrorValueType.Bool) }));
            }

            var sourceOffsets = binding.ResultRegisterOffsets;

            foreach (var element in RenderOrder)
            {
                var ofType = binding.ResultSources
                    .Select((s, i) => (Signal: s, Offset: sourceOffsets[i]))
                    .Where(x => x.Signal.Type == element.Type)
                    .Select(x => new CopyLayerCopy(x.Signal.Tag, $"{prefix}{token}_R{x.Offset:000}", element.Type))
                    .ToArray();

                if (ofType.Length > 0)
                {
                    networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.ResultsOut,
                        $"Results out - slot {binding.SlotId} - {element.Type}", ofType));
                }
            }

            // The latches, as SET coils - the same shape as the start echo (X-E), for the same reason: a
            // level coil would fall again the moment the signal did, and a poll gap is ~8.1 scans at the
            // p99, so a one-scan event lands between two polls and the log says it never happened.
            //
            // *** A PHASE-ARMED LATCH ADDS THE WINDOW AND THE RESET, AND BOTH COME FROM THE SLOT'S START
            // BOOL. *** The start bool is ANDed into the SET as well as inverted into the RESET, which is
            // not redundant: the arm window is a STATIC OF THE BLOCK UNDER TEST, and the harness must not
            // assume the block clears its own phase flag at inert. Holding the window closed at both ends
            // costs one contact and removes an assumption about the implementation being tested.
            var startBoolTag = binding.StartCondition is not null ? $"{prefix}{token}_Start" : null;

            var latchRungs = binding.LatchRegisterOffsets
                .OrderBy(e => e.Value)
                .Select(e =>
                {
                    // BY TAG, for the same reason as the tag-table pass above: `e.Key` IS the tag, and it
                    // goes on to be the latch rung's source expression.
                    var signal = binding.ResultSignalByTag(e.Key);
                    var latchTag = $"{prefix}{token}_L{e.Value:000}";

                    if (signal is not { PhaseArmed: true })
                        return new CopyLayerLatch(latchTag, e.Key, Array.Empty<string>(), ClearLevel: null);

                    // 🔴 *** A THROW, NEVER A FALLBACK TO THE UNCONDITIONAL FORM. *** Unreachable: the
                    // refusal above stops a phase-armed signal on a slot with no start condition. This
                    // said `|| startBoolTag is null -> unconditional` for one hour, and MUTATION TESTING
                    // MEASURED WHAT THAT COSTS: disconnecting the refusal with a one-token edit made the
                    // generator emit the unconditional latch for a re-arming signal, silently, and the
                    // CLI printed it as an ordinary copy layer. A guard whose bypass produces a PLAUSIBLE
                    // artifact is the exact shape this whole capability exists to remove — so the
                    // fallback is gone and disconnecting the refusal now fails loudly instead.
                    if (startBoolTag is null)
                    {
                        throw new InvalidOperationException(
                            $"signal '{e.Key}' on slot '{binding.SlotId}' is phase-armed and the slot has no start condition, so there is no "
                            + "per-index level to arm on or clear on. The refusal gate should have stopped this binding before rendering. "
                            + "Emitting the unconditional latch here instead would produce a latch that sets once and stays set for the whole "
                            + "wave — which compiles, deploys, reads plausibly, and silently deletes every finding that turns on the signal falling.");
                    }

                    var arms = new List<string> { startBoolTag };
                    if (signal.ArmWindow is { } window)
                        arms.Add(window);

                    return new CopyLayerLatch(latchTag, e.Key, arms, ClearLevel: startBoolTag);
                })
                .ToArray();

            if (latchRungs.Length > 0)
            {
                // Moves are DERIVED from the rungs rather than built beside them: two independently
                // populated views of one rung set is how a plan comes to describe something other than
                // what was emitted.
                networks.Add(new CopyLayerNetwork(number++, CopyLayerNetworkKind.ResultLatch,
                    $"Result latches - slot {binding.SlotId}",
                    latchRungs.Select(l => new CopyLayerCopy(l.LatchTag, l.Signal, MirrorValueType.Bool)).ToArray(),
                    latchRungs));
            }
        }

        // Result sources more than one slot observes. ADMITTED - the copy layer only reads them and each
        // slot writes its own register band - and reported anyway, because a scope that only speaks when
        // it exempts something cannot be told from one that has stopped running.
        var shared = ordered
            .SelectMany(o => (o.Binding.ResultSources ?? Array.Empty<MirroredSignal>())
                .Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Tag))
                .Select(s => (o.Binding.SlotId, s.Tag)))
            .GroupBy(x => x.Tag, StringComparer.Ordinal)
            .Where(g => g.Select(x => x.SlotId).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(g => $"{g.Key} (observed by {g.Select(x => x.SlotId).Distinct(StringComparer.Ordinal).Count()} slots: {string.Join(", ", g.Select(x => x.SlotId).Distinct(StringComparer.Ordinal))})")
            .ToArray();

        var plan = new CopyLayerPlan(map, ordered.Select(o => o.Binding).ToArray(), stamp, tags, networks,
            ordered.Where(o => o.Binding.StartCondition is null).Select(o => o.Binding.SlotId).ToArray())
        {
            SlotTokens = tokens,
            SharedObservations = shared,
        };

        var objects = new[]
        {
            new HarnessObject(naming.TagTableName, HarnessObjectKind.TagTable, WriteTagTable(naming.TagTableName, tags)),
            new HarnessObject(naming.BlockName, HarnessObjectKind.Block, WriteBlock(naming, networks)),
        };

        return new CopyLayerResult(plan, objects, Array.Empty<string>());
    }

    /// <summary>
    /// Two different slot ids that would produce the SAME tag fragment, named.
    ///
    /// <para><b>Refused rather than resolved.</b> Sharing a fragment means sharing every generated tag
    /// name — start bool, echo, every vector and result register — so the two slots would write each
    /// other's mirror region while the map, the hash and the register arithmetic all stayed valid. That is
    /// two agents aliased onto one register (DB-6), which the map's own disjointness check exists to make
    /// impossible and which it cannot see, because the aliasing would be in the NAMES rather than in the
    /// addresses.</para>
    /// </summary>
    private static IReadOnlyList<string> TokenCollisions(IReadOnlyList<SlotBinding> bindings)
    {
        var refusals = new List<string>();

        var groups = bindings
            .Where(b => SlotTagToken.CanDerive(b.SlotId))
            .Select(b => (b.SlotId, Token: SlotTagToken.For(b.SlotId!)))
            .GroupBy(x => x.Token, StringComparer.Ordinal)
            .Where(g => g.Select(x => x.SlotId).Distinct(StringComparer.Ordinal).Count() > 1);

        foreach (var group in groups)
        {
            refusals.Add(
                $"slot ids {string.Join(", ", group.Select(x => $"'{x.SlotId}'"))} all transliterate to the tag fragment "
                + $"'{group.Key}', so they would share EVERY generated tag name - start bool, start echo and every vector and "
                + "result register. Their mirror regions would alias each other while the map's address arithmetic and its "
                + "hash stayed perfectly valid, because the aliasing is in the NAMES and the disjointness check reads "
                + "ADDRESSES. Give them ids that differ in more than punctuation.");
        }

        return refusals;
    }

    /// <summary>
    /// 🔴 <b>TWO SLOTS THAT WRITE THE SAME TAG — AND UNTIL 2026-08-14 THE GENERATOR EMITTED BOTH AND SAID
    /// NOTHING.</b>
    ///
    /// <para><b>Measured, on two slots bound to one skeleton block.</b> The emitted layer carried
    /// <c>MOVE(EN := TRUE, IN := HX_S0_V000) =&gt; Demo_Step</c> in network 3 and
    /// <c>MOVE(EN := TRUE, IN := HX_S1_V000) =&gt; Demo_Step</c> in network 7, plus
    /// <c>COIL Demo_Start := HX_S0_Start</c> and <c>COIL Demo_Start := HX_S1_Start</c>. Rungs run in
    /// order, every scan, so <b>the LAST slot in the wave set silently owns the block and the earlier ones
    /// drive nothing.</b> The loop then ran, produced a package per slot, and reported
    /// <c>OUTCOME=Ran</c> — <i>a confident wrong answer, not an error.</i></para>
    ///
    /// <para><b>THE READ DIRECTION IS NOT THE SAME AND IS DELIBERATELY ADMITTED.</b> N slots may observe
    /// one result source: the copy layer only reads it, and each slot writes it into its own register
    /// band. That asymmetry is the whole point — the hopper set's six slots share their observations
    /// legitimately (<c>harness-binding.md:179-181</c>), and refusing that would refuse the deliverable
    /// for a hazard that only exists in the other direction.</para>
    ///
    /// <para><b>WHY A REFUSAL AND NOT A CLEVERER RENDERING.</b> A shared destination could be rendered
    /// correctly — one coil driven by the OR of the slots' start bools, and vector-in copies gated on
    /// each slot's own start bool. That changes WHEN the stimulus reaches the block (at the commit rather
    /// than during the data phase), which is an X-A property, and it is a design decision about the
    /// harness contract rather than an implementation choice inside it. The source prose reaches the same
    /// conclusion from the plant side: <i>"Making them genuinely concurrent would need six monitor
    /// instances, six stimulus instances and six input buffers. That is a project-shape decision, not a
    /// harness setting"</i> (<c>harness-binding.md:187-189</c>).</para>
    /// </summary>
    private static IReadOnlyList<string> MultipleWriters(IReadOnlyList<SlotBinding> bindings)
    {
        var refusals = new List<string>();

        // Every destination the emitted layer WRITES, with what it is and which slot claimed it. The start
        // condition is a destination too: `COIL <startCondition> := <slot start bool>`.
        var writes = bindings.SelectMany(b =>
            (b.VectorTargets ?? Array.Empty<MirroredSignal>())
                .Where(t => t is not null && !string.IsNullOrWhiteSpace(t.Tag))
                .Select(t => (Slot: b.SlotId ?? string.Empty, Tag: t.Tag, What: "a vector target"))
                .Concat(string.IsNullOrWhiteSpace(b.StartCondition)
                    ? Array.Empty<(string Slot, string Tag, string What)>()
                    : new (string Slot, string Tag, string What)[] { (b.SlotId ?? string.Empty, b.StartCondition!, "the start condition") }))
            .ToArray();

        var collisions = writes.GroupBy(w => w.Tag, StringComparer.Ordinal).Where(g => g.Count() > 1).ToArray();

        // Within ONE slot: a second copy overwrites the first inside a single scan. Its own refusal,
        // because the repair is different (bind the tag once) and so is the diagnosis.
        foreach (var group in collisions.Where(g => g.Select(w => w.Slot).Distinct(StringComparer.Ordinal).Count() == 1))
        {
            refusals.Add(
                $"tag '{group.Key}' is written {group.Count()} times by slot '{group.First().Slot}' alone ({string.Join(", ", group.Select(w => w.What))}). "
                + "One slot writes it twice, so the second copy overwrites the first within a single scan and the first is dead. Bind the tag once.");
        }

        // *** ACROSS SLOTS: ONE REFUSAL FOR THE WHOLE WAVE SET. *** The hopper binding collides on eleven
        // tags at once, and repeating the explanation eleven times buries it - a refusal nobody finishes
        // reading is a refusal that gets skimmed, which is the failure mode this project keeps recording
        // about warnings. The TAGS are enumerated; the reasoning is said once.
        var crossSlot = collisions
            .Where(g => g.Select(w => w.Slot).Distinct(StringComparer.Ordinal).Count() > 1)
            .ToArray();

        if (crossSlot.Length > 0)
        {
            var slots = crossSlot.SelectMany(g => g.Select(w => w.Slot)).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToArray();

            refusals.Add(
                $"{crossSlot.Length} tag(s) are WRITTEN by more than one slot in this wave set. "
                + "*** THE RUNGS EXECUTE IN ORDER, EVERY SCAN, SO THE LAST SLOT SILENTLY OWNS THE BLOCK AND THE OTHERS DRIVE NOTHING. *** "
                + "This does not error on the controller and it did not error here: the layer compiled, deployed, and the loop returned "
                + "a package per slot with every earlier slot's stimulus overwritten before the block ever saw it. "
                + "Sharing an OBSERVATION across slots is fine and is ADMITTED - the copy layer only READS a result source, and each slot "
                + "has its own register band. Sharing a STIMULUS is not. "
                + "THE TWO ROUTES: give each slot its own instance to drive (six monitor instances, six stimulus instances, six input "
                + "buffers - a project-shape decision, per harness-binding.md:187-189), or bind ONE slot per wave set and run the wave "
                + $"sets in sequence. Slots involved: {string.Join(", ", slots.Select(s => $"'{s}'"))}. Tags: "
                + string.Join("; ", crossSlot.Select(g =>
                    $"'{g.Key}' ({string.Join(", ", g.Select(w => $"slot '{w.Slot}' as {w.What}"))})")));
        }

        return refusals;
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

                case CopyLayerNetworkKind.ResultLatch:
                    // SCOIL, never COIL: a set coil is what makes the bit STICKY, and sticky is the whole
                    // point - the client reads it and clears it during inert.
                    foreach (var latch in network.LatchRungs)
                        ir.Append($"  SCOIL {latch.LatchTag} := {latch.SetExpression}\n");

                    // *** THE RESETS AFTER THE SETS, SO RESET DOMINATES. *** The two conditions are
                    // mutually exclusive by construction (the start bool cannot be both high and low), so
                    // the order is not load-bearing today - but if they ever were both true, clearing the
                    // evidence is the LOUD direction and setting it falsely is the quiet one, and that is
                    // the way round to fail.
                    //
                    // A LEVEL, never an edge. FB_HarnessViolationLatch network 6 states the reason from
                    // the other side: a latch cleared on the start EDGE is cleared again by a harness
                    // restart mid-run and the evidence goes with it.
                    foreach (var latch in network.LatchRungs.Where(l => l.PhaseArmed))
                        ir.Append($"  RCOIL {latch.LatchTag} := NOT {latch.ClearLevel}\n");

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
                        var shape = MirrorElements.Require(copy.Type).Shape;

                        ir.Append(shape switch
                        {
                            MirrorCopyShape.Coil => $"  COIL {copy.To} := {copy.From}\n",
                            MirrorCopyShape.Move => $"  MOVE(EN := TRUE, IN := {copy.From}) => {copy.To}\n",
                            _ => throw new InvalidOperationException($"copy shape {shape} has no rendering."),
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
