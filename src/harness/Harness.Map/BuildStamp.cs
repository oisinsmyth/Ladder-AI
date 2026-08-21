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
        IEnumerable<HarnessObject>? programUnderTest = null) =>
        Derive(map, bindings, naming, programUnderTest, out _);

    /// <summary>
    /// The derivation, REPORTING which supplied objects it refused to hash as the harness's own output.
    ///
    /// <para>The exclusion is not an optimisation and it is not silent. A caller that hands this method a
    /// whole IR directory has no way to know it just handed over the copy layer as well, and an exclusion
    /// nobody can see is indistinguishable from an object that was never supplied.</para>
    /// </summary>
    public static BuildStamp Derive(
        RegisterMap map,
        IReadOnlyList<SlotBinding> bindings,
        CopyLayerNaming naming,
        IEnumerable<HarnessObject>? programUnderTest,
        out IReadOnlyList<string> excludedAsSelfReferential) =>
        Derive(map, bindings, naming, programUnderTest, out excludedAsSelfReferential, out _);

    /// <summary>
    /// 🔴 <b>THE DERIVATION, ALSO REPORTING THE PROGRAM MANIFEST IT HASHED.</b>
    ///
    /// <para>*** A RUN THAT CANNOT BE REPRODUCED CANNOT BE RE-VERIFIED, AND THAT WAS MEASURED. *** A wave
    /// ran green on a controller; a later attempt to re-run it was refused on a build-stamp mismatch, and
    /// <b>nothing recorded which program set the successful run's stamp had been computed over</b>. The
    /// stamp is a hash — two different sets give two different stamps and neither can be inverted — so the
    /// earlier run was unreproducible the moment its command line was gone.</para>
    ///
    /// <para><b>The manifest is built in the SAME loop that feeds the hash</b>, deliberately. A second
    /// walk applying the same ordering and the same self-referential exclusion would be a second opinion
    /// about what was hashed, and the whole value of the manifest is that it cannot disagree with the
    /// stamp beside it.</para>
    /// </summary>
    public static BuildStamp Derive(
        RegisterMap map,
        IReadOnlyList<SlotBinding> bindings,
        CopyLayerNaming naming,
        IEnumerable<HarnessObject>? programUnderTest,
        out IReadOnlyList<string> excludedAsSelfReferential,
        out ProgramManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(naming);

        var hashed = new List<ProgramManifestEntry>();

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

            // 🔴 *** THE LATCH SHAPE IS PART OF THE PROGRAM, SO IT IS PART OF THE STAMP. *** A signal's
            // Transient/RearmsEachIndex/ArmedBy decide the RUNGS the copy layer emits — an unconditional
            // SCOIL, a phase-armed SCOIL+RCOIL, or nothing. Adding an arm window changes the emitted IR
            // and changes NO WIDTH, so without this the map hash would not move either and two different
            // programs would carry one stamp: the version register would confirm a build that is not the
            // one running, which is the single thing it exists to prevent.
            //
            // APPENDED, NEVER INSERTED. A signal that shapes no latch emits exactly the line it always
            // did, so every stamp already computed for a plain binding is unchanged — including the one
            // in the currently-deployed copy layer.
            //
            // 🔴 *** THE ARM LINE IS A QUALIFIER ON THE LATCH LINE, AND UNTIL 2026-08-20 IT WAS NOT
            // WRITTEN THAT WAY (D2's second consumer, missed). *** The sentence above — "ArmedBy decides
            // the RUNGS the copy layer emits" — is TRUE ONLY OF A TRANSIENT SIGNAL. `ArmedBy` acquired a
            // SECOND consumer on 2026-08-18, `SlotBinding.ArmRegisterOf`, which narrows the FRAMES a
            // Sampled expectation is judged over by reading the arm tag's own mirrored register. That
            // consumer EMITS NO RUNG AND NO REGISTER: the generator reaches `ArmWindow` in exactly two
            // places (the latch tag's comment and the latch rung's arm term) and both are inside a walk
            // of `LatchRegisterOffsets`, which is keyed on `Transient`. So declaring `armedBy` on a
            // NON-transient signal moved this stamp while the emitted artifact stayed BYTE-IDENTICAL —
            // measured by diffing the generated IR, where the only difference was the stamp constant.
            //
            // That is wrong on the stamp's own terms. *** THE STAMP MEANS "WHAT IS EXECUTING". *** An
            // evaluation window is a property of how the CLIENT JUDGES the frames it polls; it is not in
            // the download and cannot be read out of it, so hashing it makes the version register refuse
            // a controller that is running precisely the program the coordinator built — the same
            // false-`Stale` failure the self-reference exclusion below was added to remove.
            //
            // Gated on `Transient`, not on `PhaseArmed`, deliberately: the `arm=` line QUALIFIES the
            // `latch=` line, so the canonical form can never carry an arm without the latch it modifies.
            // That is one case wider than strictly necessary — a `Transient` signal that does not re-arm
            // takes no arm term in its rung — and wider is the safe direction here, because
            // over-hashing separates two builds that are the same while under-hashing conflates two that
            // are not, and only the second can confirm the wrong program.
            foreach (var source in binding.ResultSources ?? Array.Empty<MirroredSignal>())
            {
                canonical.Append($"r={source.Tag}:{source.Type}");

                if (source.Transient)
                    canonical.Append(source.RearmsEachIndex ? " latch=phase-armed" : " latch=unconditional");

                if (source.Transient && source.ArmWindowStated)
                    canonical.Append($" arm={source.ArmWindow}");

                canonical.Append('\n');
            }
        }

        // 🔴 *** THE HARNESS'S OWN GENERATED OBJECTS ARE EXCLUDED, AND THAT IS THE INVARIANT THIS CLASS
        // OPENS BY STATING: "the copy layer is not one of its own inputs ... hashing the copy layer would
        // be circular". *** It honoured that for its OWN arguments, and `--program <ir-dir>` re-introduced
        // the circularity FROM THE SIDE, because the copy-layer block and the mirror tag table are FILES
        // IN THAT DIRECTORY and the loader below quite correctly picks them up.
        //
        // MEASURED 2026-08-17, and the symptom is the worst-shaped one available: generate -> promote the
        // generated layer into `ir/` -> regenerate to verify the device moved the stamp 16#F52ECEAD ->
        // 16#4ED5E68D over an input set that was otherwise byte-identical (43 tracked files, none dirty,
        // no code change in between — the promoting commit's own recorded command reproduces the OLD stamp
        // only from the PRE-promotion tree). So `VersionCheck` classified a CORRECTLY DEPLOYED program as
        // `Stale`, whose text says the download "aborted, was refused, or never reached it" — sending a
        // person to re-download a device that was already right. *** EVERY PROMOTION INVALIDATED THE STAMP
        // IT HAD JUST WRITTEN, so the version register could never confirm anything after the first time.
        //
        // Excluding them costs nothing: both objects are a pure function of the map, the bindings and the
        // naming, and all three are already hashed above — which is the same argument the class opens with.
        // The two names come FROM `naming`, never from a hardcoded list, so a renamed copy layer stays
        // excluded and an unrelated block never is.
        var selfReferential = new List<string>();

        foreach (var obj in (programUnderTest ?? Array.Empty<HarnessObject>()).OrderBy(o => o.Name, StringComparer.Ordinal))
        {
            if (string.Equals(obj.Name, naming.BlockName, StringComparison.Ordinal) ||
                string.Equals(obj.Name, naming.TagTableName, StringComparison.Ordinal))
            {
                selfReferential.Add($"{obj.Kind}:{obj.Name}");
                continue;
            }

            canonical.Append($"obj={obj.Kind}:{obj.Name}\n{obj.Ir}\n");

            // Recorded HERE, in the loop that feeds the hash, so the manifest cannot describe a different
            // set from the one that was stamped.
            hashed.Add(new ProgramManifestEntry(
                obj.Kind.ToString(),
                obj.Name,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(obj.Ir))).ToLowerInvariant()));
        }

        excludedAsSelfReferential = selfReferential;

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));

        for (var offset = 0; offset + 4 <= digest.Length; offset += 4)
        {
            var word = (uint)((digest[offset] << 24) | (digest[offset + 1] << 16) | (digest[offset + 2] << 8) | digest[offset + 3]);
            if (word != 0)
            {
                var stamp = new BuildStamp(word);
                manifest = new ProgramManifest(hashed, selfReferential, stamp.Value);
                return stamp;
            }
        }

        // Thirty-two consecutive zero bytes out of SHA-256. Not reachable in practice, and a throw is the
        // honest treatment: silently substituting a constant would make one build stamp mean two things.
        throw new InvalidOperationException("the digest yielded no non-zero 32-bit word; a zero build stamp cannot be told from bit memory that was never written.");
    }
}

/// <summary>One object the build stamp was computed over.</summary>
/// <param name="Kind">The object's kind, exactly as it appears in the canonical form.</param>
/// <param name="Name">Its name, exactly as it appears in the canonical form.</param>
/// <param name="Sha256">
/// A hash of the object's IR TEXT. <b>The name alone is not enough</b>: the defect this exists for is a
/// stamp that no longer matches, and "the same twelve names" is equally true of twelve files that have
/// since been edited. The hash is what distinguishes a different SET from a changed one.
/// </param>
public sealed record ProgramManifestEntry(string Kind, string Name, string Sha256);

/// <summary>
/// 🔴 <b>WHAT THE BUILD STAMP WAS COMPUTED OVER — recorded so a run can be reproduced.</b>
///
/// <para>*** MEASURED 2026-08-21: A WAVE THAT RAN GREEN COULD NOT BE RE-RUN. *** The verifying gateway
/// refused a later attempt on a stamp mismatch, and nothing recorded which program set the successful
/// run's stamp had come from. Two candidate sets were tried and produced two different stamps, neither
/// the device's. A hash cannot be inverted, so the earlier run became unreproducible the moment its
/// command line was lost — and the result package, which is the artifact meant to outlive the run, kept
/// the outcome and not the input.</para>
///
/// <para><b>This is the input side of the same claim the stamp makes.</b> The stamp says <i>a program
/// hashing to this is executing</i>; the manifest says <i>and here is what was hashed</i>. Neither is
/// much use alone once the shell history is gone.</para>
/// </summary>
/// <param name="Objects">Every object hashed, in the order the canonical form appends them.</param>
/// <param name="ExcludedAsSelfReferential">
/// The harness's own generated objects, refused BY NAME rather than dropped. A caller who passes a whole
/// IR directory has no way to know it also handed over the copy layer, and an exclusion nobody can see is
/// indistinguishable from an object that was never supplied.
/// </param>
/// <param name="Stamp">The stamp these objects produced, carried beside them so the pair travels together.</param>
public sealed record ProgramManifest(
    IReadOnlyList<ProgramManifestEntry> Objects,
    IReadOnlyList<string> ExcludedAsSelfReferential,
    uint Stamp)
{
    /// <summary>
    /// <b>An empty manifest is a real state and says so.</b> A run declaring no program under test
    /// hashes no objects; that is different from a run whose manifest was never recorded, and the two
    /// must not render the same.
    /// </summary>
    public bool HashedNothing => Objects.Count == 0;
}
