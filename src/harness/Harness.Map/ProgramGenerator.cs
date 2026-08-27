namespace Harness.Map;

/// <summary>The cyclic OB, declared as data. See <see cref="CyclicObGenerator"/> — the ORDER is the content.</summary>
public sealed record CyclicObDeclaration(CyclicObNaming Naming, IReadOnlyList<ObCall> Calls);

/// <summary>
/// 🔴 <b>THE PROGRAM-LEVEL HALF OF A LANE'S TEST SIDE, DECLARED AS DATA — the artifacts that belong to the
/// PROGRAM rather than to one slot.</b>
///
/// <para><see cref="LaneDeclaration"/> covers what is per-slot: the slot FC and the stimulus shell. These
/// two are not. A cyclic OB is one per program however many slots it drives, and an instance DB belongs to
/// the FB it instantiates, which may be a slot's or may be the comms block every slot shares.</para>
///
/// <para><b>Every field is optional and ABSENT MEANS NOT GENERATED, never a guess</b> — the same rule
/// <see cref="LaneDeclaration"/> follows, reported the same way, on a positive line rather than by
/// silence.</para>
/// </summary>
public sealed record ProgramDeclaration(
    CyclicObDeclaration? CyclicOb = null,
    IReadOnlyList<InstanceDbDeclaration>? InstanceDbs = null)
{
    /// <summary>True when this declaration asks for nothing at all — which is a legitimate program.</summary>
    public bool Empty => CyclicOb is null && (InstanceDbs is null || InstanceDbs.Count == 0);
}

/// <summary>
/// Everything <see cref="ProgramGenerator"/> produced for one request. <b><see cref="Refusals"/> non-empty
/// means NOTHING may be used from this result</b> — the rule <see cref="CopyLayerResult"/> and
/// <see cref="LaneGenerationResult"/> both follow.
/// </summary>
public sealed record ProgramGenerationResult(
    CyclicObResult? CyclicOb,
    IReadOnlyList<InstanceDbResult> InstanceDbs,
    IReadOnlyList<string> Obligations,
    IReadOnlyList<string> NotDeclared,
    IReadOnlyList<string> Refusals)
{
    /// <summary>Nothing declared. An empty result is legitimate and is not a refusal.</summary>
    public static readonly ProgramGenerationResult Nothing = new(
        null, Array.Empty<InstanceDbResult>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    public bool Refused => Refusals.Count > 0;

    /// <summary>
    /// The deployable generated objects — the OB and the instance DBs. <b>All of them EXECUTE or are LOADED
    /// on the controller</b>, so all of them belong in the build stamp; unlike the copy layer, none embeds
    /// the stamp, so hashing them is not circular.
    /// </summary>
    public IReadOnlyList<HarnessObject> Objects
    {
        get
        {
            var objects = new List<HarnessObject>();
            if (CyclicOb is not null)
                objects.Add(new HarnessObject(CyclicOb.BlockName, HarnessObjectKind.Block, CyclicOb.Ir));

            foreach (var db in InstanceDbs)
                objects.Add(new HarnessObject(db.DbName, HarnessObjectKind.DataBlock, db.Ir));

            return objects;
        }
    }

    /// <summary>
    /// The counts, on the passing path as well as the refusing one. <b>Both halves are printed</b>: a bare
    /// "3 generated" is true of a program whose other five objects nobody looked at.
    /// </summary>
    public string Summary() =>
        $"{(CyclicOb is null ? "no cyclic OB" : "1 cyclic OB")} and {InstanceDbs.Count} instance DB(s) GENERATED; "
        + $"{NotDeclared.Count} program object(s) NOT generated because nothing declared them — those remain AUTHORED.";
}

/// <summary>
/// 🔴 <b>THE PRODUCTION CALL SITE FOR <see cref="InstanceDbGenerator"/> AND <see cref="CyclicObGenerator"/>,
/// AND THE PLACE THE ORDERING OBLIGATIONS STOP BEING SENTENCES.</b>
///
/// <para><see cref="SlotFcGenerator"/> emits, with every block it generates, the sentence <i>"'X' MUST be
/// called from the cyclic OB, ahead of the copy layer"</i>. That sentence exists because a slot FC once sat
/// in a controller called by nothing: every vector timed out, the start echo reported "commanded, observed
/// to run" throughout because both halves of it live in the copy layer, and it cost a wave and three hours.
/// Until this class existed the sentence was the whole mechanism. <b>Here the cyclic OB is generated in the
/// same pass, so the obligation becomes a CHECK against the call list</b> — the OB is refused if it does not
/// call a generated slot FC, and refused again if it calls it after the copy layer.</para>
///
/// <para>🔴 <b>WHAT THIS CLASS STILL DOES NOT DECIDE: THE ORDER.</b> It checks the two orderings its inputs
/// already determine and takes every other position from the declaration verbatim. LAD executes in network
/// order and what must run before what is a claim about the program, not a fact about its parts.</para>
///
/// <para><b>Contract copied from <see cref="LaneGenerator"/> unchanged</b>, including its one rule of its
/// own: a declaration is honoured WHOLE or refused NAMED, and a refusal anywhere voids the whole result.</para>
///
/// <para>🔴 <b>WHAT THESE OBJECTS ARE DELIBERATELY NOT PUT THROUGH: the 0.1b non-retentive assertion.</b>
/// <see cref="RetentionCheck"/> runs over the COPY LAYER's objects, and that is where it stops on purpose.
/// An instance DB's retention is its FB's — <c>FB_HopperBlockageStim</c> declares
/// <c>PreBoundaryDone : Bool RETAIN</c> deliberately, because that member is what lets the model know it is
/// running after the CPU restart it exists to test, and the boundary-spanning vectors depend on it. A 0.1b
/// applied to a projected instance DB would refuse the deliverable for containing the thing the deliverable
/// needs, which is the finding <c>LoopRun</c> already records one artifact over. <b>It is a projection of a
/// block's own storage, not harness instrumentation</b>, and the rule is about the latter.</para>
/// </summary>
public static class ProgramGenerator
{
    /// <param name="declaration">What the program declares. Null or empty produces <see cref="ProgramGenerationResult.Nothing"/>.</param>
    /// <param name="fbIrByName">
    /// 🔴 <b>THE FB SOURCES, BY NAME — required, and a missing one is a REFUSAL rather than a name written
    /// into an <c>INSTANCEOF</c>.</b> An instance DB is a projection of an interface; a projection of an
    /// interface nobody supplied is a block asserted to mirror something never looked at.
    /// </param>
    /// <param name="lane">
    /// What the lane generated, or null. Every generated slot FC becomes a presence requirement and an
    /// ordering requirement on the OB — <b>derived from the lane, never typed</b>.
    /// </param>
    /// <param name="copyLayerBlock">
    /// The copy layer's block name. It is the <c>Later</c> half of every ordering requirement and must
    /// itself be called: it is the block that publishes what the scan produced.
    /// </param>
    /// <param name="instancePathsUsedElsewhere">
    /// Instance paths named by calls this generator does not emit — a slot FC's two calls, above all. A
    /// generated instance DB named by NOTHING, here or in the OB, is a data block loaded into a controller
    /// and never written.
    /// </param>
    public static ProgramGenerationResult Generate(
        ProgramDeclaration? declaration,
        IReadOnlyDictionary<string, string>? fbIrByName = null,
        LaneGenerationResult? lane = null,
        string? copyLayerBlock = null,
        IReadOnlyList<string>? instancePathsUsedElsewhere = null)
    {
        if (declaration is null || declaration.Empty)
            return ProgramGenerationResult.Nothing;

        var sources = fbIrByName ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var refusals = new List<string>();
        var obligations = new List<string>();
        var notDeclared = new List<string>();
        var instanceDbs = new List<InstanceDbResult>();

        // ---- the instance DBs -----------------------------------------------------------------------
        var declaredDbs = declaration.InstanceDbs ?? Array.Empty<InstanceDbDeclaration>();
        if (declaredDbs.Count == 0)
        {
            notDeclared.Add("no instance DB was declared, so none was generated. Every instance DB this program needs is "
                          + "AUTHORED.");
        }

        var byDbName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byDbNumber = new Dictionary<int, string>();
        var leftToTiaByFb = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var db in declaredDbs)
        {
            if (db is null)
            {
                refusals.Add("a null instance-DB declaration was supplied.");
                continue;
            }

            if (!sources.TryGetValue(db.Naming.FbName, out var fbIr))
            {
                refusals.Add(
                    $"instance DB '{db.Naming.DbName}' declares INSTANCEOF '{db.Naming.FbName}', and that FB's IR was not "
                    + "supplied. An instance DB is a projection of an interface: with no interface in hand the generator "
                    + "would be emitting a block asserted to mirror something nobody looked at. Supply the FB in the "
                    + "program set.");
                continue;
            }

            InstanceDbResult generated;
            try
            {
                generated = InstanceDbGenerator.Generate(db, fbIr);
            }
            catch (ArgumentException error)
            {
                refusals.Add($"instance DB '{db.Naming.DbName}' could not be generated: {error.Message}");
                continue;
            }

            // TIA matches an import by NAME: a second file with the same name replaces the first, and both
            // sit on disk with nothing saying which survived.
            if (byDbName.TryGetValue(generated.DbName, out var nameOwner))
            {
                refusals.Add(
                    $"'{generated.DbName}' is declared twice (as an instance of '{nameOwner}' and of '{generated.FbName}'). "
                    + "TIA matches an import by name, so the second would silently replace the first.");
                continue;
            }

            if (byDbNumber.TryGetValue(db.Naming.DbNumber, out var numberOwner))
            {
                refusals.Add(
                    $"'{generated.DbName}' and '{numberOwner}' both declare DB number {db.Naming.DbNumber}. A number holds one "
                    + "block; claim a second with `converter claim --allocate --kind block-number --type DB --floor 9000`.");
                continue;
            }

            // 🔴 THE SILENT COLLISION THIS WHOLE DESIGN IS BUILT AROUND. `leftToTia` means TIA fills the
            // instance from the FB — INCLUDING the FB's own start values. One instance of an FB may take
            // them; a SECOND cannot, because a start value that identifies an instance (a listening port, a
            // connection ID) is then the same in both, and the failure is not a compile error. It is a
            // connection that never establishes, on a rig, hours later.
            if (generated.Members == InstanceDbMemberSource.LeftToTia)
            {
                if (leftToTiaByFb.TryGetValue(generated.FbName, out var first))
                {
                    refusals.Add(
                        $"'{generated.DbName}' and '{first}' are both instances of '{generated.FbName}' declared with "
                        + "`leftToTia` members, so TIA fills BOTH from the FB's own start values and both start identically. "
                        + $"'{generated.FbName}' declares {generated.InheritedStartValues.Count} start value(s): "
                        + $"{string.Join(", ", generated.InheritedStartValues)}. If any of those identifies the instance — a "
                        + "listening port, a connection ID — the two collide, and not at compile time. Declare at most one "
                        + "instance this way and give the others `projectedFromFb` members with explicit presets.");
                    continue;
                }

                leftToTiaByFb[generated.FbName] = generated.DbName;

                obligations.Add(
                    $"'{generated.DbName}' has an EMPTY members section by declaration: TIA projects it from "
                    + $"'{generated.FbName}' at compile, and it therefore INHERITS that FB's "
                    + $"{generated.InheritedStartValues.Count} start value(s)"
                    + (generated.InheritedStartValues.Count == 0
                        ? ". The FB declares none, so nothing is inherited — an earned zero, read off the FB's own text."
                        : $": {string.Join(", ", generated.InheritedStartValues)}. Those are this instance's presets and "
                          + "nothing here declared them."));
            }

            byDbName[generated.DbName] = generated.FbName;
            byDbNumber[db.Naming.DbNumber] = generated.DbName;
            instanceDbs.Add(generated);
        }

        // ---- the cyclic OB --------------------------------------------------------------------------
        CyclicObResult? cyclicOb = null;

        if (declaration.CyclicOb is null)
        {
            notDeclared.Add("no cyclic OB was declared, so none was generated. The OB that calls this program is AUTHORED — "
                          + "and it is the one block whose content is the ORDER, so nothing here has checked that the "
                          + "generated blocks are called at all.");
        }
        else
        {
            var presence = new List<ObCallPresence>();
            var order = new List<ObCallOrder>();

            foreach (var slot in lane?.Slots ?? Array.Empty<LaneSlotGeneration>())
            {
                if (slot.SlotFc is not { } slotFc)
                    continue;

                presence.Add(new ObCallPresence(slotFc.BlockName, slotFc.CallSiteObligation));

                if (copyLayerBlock is { Length: > 0 })
                {
                    order.Add(new ObCallOrder(slotFc.BlockName, copyLayerBlock, slotFc.CallSiteObligation));
                }
            }

            if (copyLayerBlock is { Length: > 0 })
            {
                presence.Add(new ObCallPresence(
                    copyLayerBlock,
                    "The copy layer is what publishes the scan's observations into the mirror the client reads. An OB that "
                    + "does not call it deploys, loads and reports healthy while the mirror never moves — and the vectors "
                    + "then fail against a block that did nothing wrong."));
            }

            try
            {
                cyclicOb = CyclicObGenerator.Generate(
                    declaration.CyclicOb.Naming, declaration.CyclicOb.Calls, presence, order);
            }
            catch (ArgumentException error)
            {
                refusals.Add($"the cyclic OB '{declaration.CyclicOb.Naming.BlockName}' could not be generated: {error.Message}");
            }
        }

        // ---- every generated instance DB must be instantiated by SOMETHING --------------------------
        //
        // 🔴 ONLY WHEN THE OB WAS GENERATED HERE, AND THAT GATE IS THE POINT RATHER THAN A CONCESSION. The
        // check needs the WHOLE call list to say "nothing names this". With the OB authored, the call list
        // is in a file this generator has not seen, so a refusal would be a claim made against nothing — the
        // exact empty-is-not-clean failure, run in the refusing direction. The absent OB is already reported
        // on its own NotDeclared line, which says in words that nothing here checked the calls.
        if (refusals.Count == 0 && instanceDbs.Count > 0 && cyclicOb is not null)
        {
            var used = new HashSet<string>(instancePathsUsedElsewhere ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var call in declaration.CyclicOb?.Calls ?? Array.Empty<ObCall>())
            {
                if (call.InstancePath is { Length: > 0 } path)
                    used.Add(path.Split('.')[0]);
            }

            foreach (var db in instanceDbs.Where(db => !used.Contains(db.DbName)))
            {
                // 🔴 THE ORPHAN, ONE ARTIFACT OVER. A generated instance DB that no call names is imported,
                // loaded, counted in the manifest and never written — and every value read out of it is the
                // zero it was loaded with, which reads exactly like a block that ran and did nothing.
                refusals.Add(
                    $"instance DB '{db.DbName}' is generated and nothing instantiates it: no call in the cyclic OB names it, "
                    + "and it was not declared as an instance anywhere else in this lane. It would be imported, loaded, "
                    + "counted in the manifest and never written, and every value read out of it would be the zero it was "
                    + "loaded with — which reads exactly like a block that ran and did nothing.");
            }
        }

        // 🔴 A REFUSED REQUEST YIELDS NOTHING USABLE, INCLUDING THE PARTS THAT WORKED — the rule
        // LaneGenerator states and for the same reason: two thirds of a program is the orphan with the
        // paperwork filed.
        return refusals.Count > 0
            ? new ProgramGenerationResult(null, Array.Empty<InstanceDbResult>(), Array.Empty<string>(), Array.Empty<string>(), refusals)
            : new ProgramGenerationResult(cyclicOb, instanceDbs, obligations, notDeclared, refusals);
    }
}
