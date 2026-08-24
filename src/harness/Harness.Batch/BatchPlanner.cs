using System.Text;
using System.Text.Json;
using Harness.Gate;
using Harness.Map;
using Harness.Run;

namespace Harness.Batch;

/// <summary>What planning a batch produced, or every reason it produced nothing.</summary>
/// <param name="LanesQueued">
/// <b>The denominator, always printed.</b> Lanes batched alone cannot be told from lanes batched out of
/// many, and a report that states only what it included is the shape this project keeps being caught by.
/// </param>
public sealed record BatchPlanResult(
    int LanesQueued,
    IReadOnlyList<string> LanesBatched,
    IReadOnlyList<string> Refusals,
    string? MergedBindingJson,
    RegisterMap? Map,
    IReadOnlyList<string> ProgramPaths,

    /// <summary>Whether every deployed code block is in the scan - or that it could not be determined.</summary>
    ReachabilityReport? Reachability = null,

    /// <summary>
    /// What the PROGRAM says the Modbus server serves, against which the authored <c>declaredRegisters</c>
    /// was checked — or the reason nothing was derived. Never null on a returned result: "not asked" is
    /// itself a state, and one the report has to print.
    /// </summary>
    ServedAreaFact? ServedArea = null,

    /// <summary>
    /// 🔴 What the PROGRAM says already occupies the served area — or which of the several nothings it
    /// was. Never null on a returned result, for <see cref="ServedArea"/>'s reason.
    /// </summary>
    NeighbourFact? Neighbours = null,

    /// <summary>
    /// The derived set measured against the declared one: what was used, what was excluded as the
    /// mirror's own, and which declarations nothing corroborated.
    /// </summary>
    NeighbourReconciliation? NeighbourReconciliation = null,

    /// <summary>
    /// 🔴 <b>WHO THE MERGED BINDING ENDED UP DECLARING, AND — WHEN IT DECLARES NOBODY — WHY.</b>
    ///
    /// <para>Null only where no merge happened (a refused plan). Otherwise it is stated on every plan,
    /// pass or drop, for the reason the neighbour line above is: <b>an authority line that appears only
    /// when something went wrong teaches a reader that its absence means "attributed"</b>, and the
    /// absence would in fact mean nobody printed it.</para>
    ///
    /// <para>The unattributed text is the SAME account gate 5c gives when it reads <c>NOT CHECKED</c> on
    /// a batched submission, so the trail works from either end: from the gate, which names
    /// <see cref="BatchPlanner"/>'s merge; and from here, at the moment the field is dropped, hours
    /// before anybody runs a wave. See <c>SharedDeclarer</c>.</para>
    /// </summary>
    string? MergedAuthority = null)
{
    public bool Planned => Refusals.Count == 0 && MergedBindingJson is not null;
}

/// <summary>
/// Merges queued lanes into ONE deployment.
///
/// <para><b>Only the bindings are merged.</b> Each lane keeps its own submission and runs it against the
/// shared map afterwards — which works because the wave already treats a slot with no vector at an index
/// as inert, the same mechanism that covers an excised slot. Merging submissions would mean merging
/// every document-level field the gate evaluates, and a batch has no business changing any lane's
/// verdict.</para>
/// </summary>
public static class BatchPlanner
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,

        // camelCase because every hand-written binding in this repo is camelCase, and the merged one is
        // a binding like any other — something a person will read next to the lanes it came from, and
        // something harness-run reads with the same reader. BindingDocument.Read is case-insensitive so
        // PascalCase would have worked and looked wrong, which is the kind of difference that gets
        // explained away rather than noticed.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        // A merged document should not sprout fifty nulls the lanes never wrote.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static BatchPlanResult Plan(
        IReadOnlyList<Lane> lanes,
        Func<string, string> readFile,
        ServedAreaFact? servedArea = null,
        NeighbourFact? neighbours = null)
    {
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(readFile);

        // "Nobody asked" is a state and it is carried, not defaulted away. A result whose ServedArea is
        // silently null reads, on every surface that prints it, exactly like one where the check ran.
        var served = servedArea ?? ServedAreaFact.NotAsked;

        // Same rule, same reason (workbench Y1). NotAsked is the state every plan was in before the
        // derivation existed; it does not gate, and it does not print as a green either.
        var neighbourFact = neighbours ?? NeighbourFact.NotAsked;

        var refusals = new List<string>();

        // Empty is not clean. A batch of nothing plans perfectly, deploys perfectly and tests nothing —
        // and it is the outcome most likely to be believed, because every other number reads as valid.
        if (lanes.Count == 0)
        {
            return new BatchPlanResult(0, Array.Empty<string>(),
                new[] { "NOTHING BATCHED: the queue is empty. That is not a clean batch, it is a batch of nothing — and a deployment built from it would test nothing while reporting a success." },
                null, null, Array.Empty<string>(), null, served, neighbourFact);
        }

        var documents = new List<(Lane Lane, BindingDocument Binding)>();
        foreach (var lane in lanes)
        {
            try
            {
                var text = readFile(lane.BindingPath);
                var binding = BindingDocument.Read(text);
                documents.Add((lane, binding));
            }
            catch (Exception e) when (e is IOException or JsonException or InvalidDataException)
            {
                refusals.Add($"lane '{lane.Name}': its binding at {lane.BindingPath} could not be read — {e.Message}");
            }
        }

        if (refusals.Count > 0)
            return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, Array.Empty<string>(), null, served, neighbourFact);

        // ---- The geometry every lane has to agree on. -------------------------------------------
        //
        // These are properties of ONE deployed harness, not of a lane, so two lanes disagreeing is not
        // something to reconcile by picking one — it is a submission-level mistake, and choosing
        // silently would deploy a mirror at an address half the lanes do not expect.
        AgreeOn(documents, refusals, "baseByte", b => b.BaseByte);
        AgreeOn(documents, refusals, "retentiveBytes", b => b.RetentiveBytes);
        AgreeOn(documents, refusals, "declaredRegisters", b => b.DeclaredRegisters);
        AgreeOn(documents, refusals, "blockName", b => b.BlockName);
        AgreeOn(documents, refusals, "blockNumber", b => b.BlockNumber);
        AgreeOn(documents, refusals, "tagTableName", b => b.TagTableName);
        AgreeOn(documents, refusals, "tagPrefix", b => b.TagPrefix);

        // ---- Slot ids, which become mirror addresses. -------------------------------------------
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (lane, binding) in documents)
        {
            foreach (var slot in binding.Slots ?? new List<SlotBindingDocument>())
            {
                var id = slot.SlotId ?? string.Empty;
                if (id.Length == 0)
                {
                    refusals.Add($"lane '{lane.Name}' has a slot with no id. Slot ids decide addresses and name results; an unnamed slot cannot be reported on.");
                    continue;
                }

                if (owners.TryGetValue(id, out var other))
                {
                    refusals.Add(
                        $"slot id '{id}' is claimed by BOTH lane '{other}' and lane '{lane.Name}'. Ordinals decide mirror addresses and "
                        + "results are looked up by slot id, so two slots of one name in a batch would write to different registers and "
                        + "read back as each other. Rename one — this is exactly the collision a batch exists to surface.");
                    continue;
                }

                owners[id] = lane.Name;
            }
        }

        // ---- The union program, whose CONSTRUCTION is the first union check. --------------------
        var programPaths = new List<string>();
        var byBasename = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lane in lanes)
        {
            foreach (var path in lane.ProgramPaths)
            {
                programPaths.Add(path);

                foreach (var file in FilesUnder(path))
                {
                    var name = Path.GetFileName(file);
                    if (byBasename.TryGetValue(name, out var other) && !string.Equals(other, file, StringComparison.OrdinalIgnoreCase))
                    {
                        refusals.Add(
                            $"'{name}' is contributed by two lanes ({other} and {file}). A union corpus cannot hold two objects of one "
                            + "name: TIA's import matches by name, so the second would REPLACE the first rather than join it. This is the "
                            + "collision class the union check exists to find, and it is found here rather than in the controller.");
                    }
                    else
                    {
                        byBasename[name] = file;
                    }
                }
            }
        }

        // ---- DID EVERY REQUESTED PATH ACTUALLY CONTRIBUTE ANYTHING? ------------------------------
        //
        // 🔴 A path naming nothing used to resolve to an empty sequence and vanish. The union was then
        // short, and the SAME short set feeds the build stamp, the reachability check and the drift
        // check — so one typo'd --program weakened three things at once, in the same direction, with no
        // line anywhere. Refused rather than warned: the stamp means "what is executing", and a stamp
        // computed over a set that is missing a block is not a weaker claim, it is a false one.
        refusals.AddRange(ProgramFiles.Refusals(ProgramFiles.Resolve(programPaths)));

        // ---- IS EVERY DEPLOYED BLOCK ACTUALLY IN THE SCAN? ---------------------------------------
        var reachability = Reachability.Of(programPaths);
        refusals.AddRange(reachability.Refusals);

        if (refusals.Count > 0)
            return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, programPaths, null, served, neighbourFact);

        // ---- Does the merged map fit what Modbus can reach? -------------------------------------
        var first = documents[0].Binding;
        var slots = documents
            .SelectMany(d => (d.Binding.Slots ?? new List<SlotBindingDocument>())
                .Select(s => new SlotRequest(s.SlotId!, Math.Max(1, VectorRegistersOf(s)), Math.Max(1, ResultRegistersOf(s)))))
            .ToArray();

        // =========================================================================================
        // 🔴 IS THE DECLARED WIDTH TRUE OF THE PROGRAM? (workbench Y2, 2026-08-23)
        // =========================================================================================
        //
        // `declaredRegisters` was AUTHORED BY HAND, per lane, and required. It reaches
        // MirrorGeometry, MapAllocator, RegisterMap.MapHash's canonical form (`declared=`) and
        // therefore the build stamp — so the stamp DOES hash a declared width, and the gap is
        // narrower than it looks. What nothing checked is whether the number is TRUE OF THE PROGRAM.
        //
        // The truth lives in the comms block, in two lines that can silently disagree: the readable
        // `MB_HOLD_REG := P#M1000.0 WORD 37` and the sidecar constant backing it. `converter
        // served-area` reads BOTH and refuses when they differ; here the result is compared against
        // what the lanes declared.
        //
        // *** THE DERIVED VALUE NEVER SILENTLY REPLACES AN AUTHORED ONE. *** Where both exist and
        // differ, this REFUSES naming both numbers and both sources. Substituting the derived one
        // would make the authored field decorative and hide a real disagreement between the binding
        // and the block; substituting the authored one would defeat the check outright. The authored
        // field becomes OPTIONAL-AND-CHECKED — absent, the derived number stands; present, it must
        // agree.
        //
        // 🔴 WHAT THIS CANNOT SEE: WHETHER THE BLOCK IT READ IS THE BLOCK ON THE CONTROLLER. The
        // derivation reads the staged corpus, never the CPU. The mirror's widening to 1024 registers
        // was established by probing the device from both sides, and nothing here substitutes for
        // that. A corpus stale with respect to the rig derives a confident, agreed, WRONG number and
        // is indistinguishable from a fresh one. The claim bought is strictly smaller and is printed
        // as such: A BINDING CAN NO LONGER DISAGREE WITH THE PROGRAM THAT WAS STAGED.
        //
        // A producer REFUSAL gates — that is a defect in the thing about to be deployed. Being unable
        // to CONSULT the producer does not: the same trade ReachabilityParity already makes, said out
        // loud on the report rather than swallowed.
        refusals.AddRange(served.Refusals.Select(r =>
            "the served area could not be derived from the program corpus, and a width nothing corroborates is exactly "
            + "what this check exists to stop: " + r));

        var authored = first.DeclaredRegisters;
        var declaringLanes = string.Join(", ", documents
            .Where(d => d.Binding.DeclaredRegisters is not null)
            .Select(d => $"'{d.Lane.Name}' ({d.Lane.BindingPath})"));

        if (served.Derived && authored is int stated && stated != served.Registers)
        {
            refusals.Add(
                $"the batch declares `declaredRegisters` {stated} but the program serves {served.Registers}. "
                + $"The {stated} is AUTHORED, in the binding(s) of {declaringLanes}; the {served.Registers} is "
                + $"DERIVED — {served.Denominator}. Neither is substituted for the other: a map allocated against a "
                + "width the MB_SERVER call does not serve fits on paper and faults on the wire, and a map allocated "
                + "against a corpus the binding disagrees with hides which of the two is stale. Reconcile them.");
        }

        if (served.Derived && first.BaseByte is int statedBase && statedBase != served.BaseByte)
        {
            refusals.Add(
                $"the batch declares `baseByte` {statedBase} but the program serves its holding registers from "
                + $"%M{served.BaseByte} — {served.Denominator}. The mirror would be written at one address and served "
                + "from another, and every register a client read would be off by the difference.");
        }

        // Optional-and-checked. Absent, the derived number stands — it is the only one in the
        // transaction that came from the program. Absent AND underived is still a refusal: the
        // derivation is a check on the authored value, never a licence to stop stating one when
        // nobody looked.
        if (authored is not int declared)
        {
            if (!served.Derived)
            {
                refusals.Add("no lane states `declaredRegisters`, so the merged map cannot be checked against what a Modbus client can reach. It is required per lane."
                    + " Nor could it be derived from the program: " + served.Denominator);
                return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, programPaths, null, served, neighbourFact);
            }

            declared = served.Registers;
        }

        if (refusals.Count > 0)
            return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, programPaths, null, served, neighbourFact);

        // The 1000 fallback is unchanged and deliberately kept: a derived base of 0 means NOT DERIVED,
        // and letting that stand in for an unstated one would move the whole mirror to %M0.
        var geometry = MirrorGeometry.ForCpu1214C(
            first.RetentiveBytes ?? 256, first.BaseByte ?? (served.Derived ? served.BaseByte : 1000), declared);

        // 🔴 THE NEIGHBOURS, IF THE LANE DECLARED ANY — and this is the wiring that makes the guard bite.
        //
        // Measured 2026-08-23: the merged mirror grew from one lane to two and walked into a hand-authored
        // virtual panel's command band, 53 tags colliding bit-for-bit including its master enable. The
        // allocator bounds the mirror against the DECLARED AREA and the map proves its regions disjoint
        // from EACH OTHER; neither knows a neighbour exists. Without this line ReservedRegion is a guard
        // nothing can trigger.
        //
        // Malformed reservations are refused BY THE GEOMETRY rather than dropped here: a lane that
        // declared a neighbour believes part of the area is off limits, and silently ignoring a
        // typo'd one would restore exactly the silence this closes.
        // 🔴 UNIONED ACROSS LANES, NOT TAKEN FROM THE FIRST. The area is SHARED, so a neighbour any lane
        // knows about is a neighbour of the merged map — and reading only `first` would silently drop one
        // that only a later lane declared, which is the same class of silence this whole guard closes.
        //
        // Identical declarations collapse; two lanes declaring OVERLAPPING-BUT-DIFFERENT regions do not,
        // and the geometry refuses them. That is correct rather than awkward: it means two lanes disagree
        // about what else lives in the area, and guessing which is right would be inventing the answer.
        // 🔴 ONE DERIVATION, 2026-08-23. This block used to open-code the union that
        // `DeclaredReservations` now owns — and while it did, `LoopCli.Compose` built its geometry with
        // no reservations at all, so the guard bound on the batch path and was INERT on every path that
        // went through Compose: `harness-run`, `Harness.Verify`, and `harness-batch --merged`, the deploy
        // path itself. The merged binding carried the reservation and the consumer dropped it.
        //
        // The rule is not the three lines it looks like. Absent `register`/`length` map to -1/0 SO THE
        // REGION REFUSES ITSELF, never to 0/1, which would read as a real band nobody typed; identical
        // declarations collapse while overlapping-but-different ones do not; and an empty set must never
        // reach `Reserving`, which throws. Each of those is invisible at a call site, which is exactly
        // why a second copy of them was a defect waiting rather than a duplication to tidy.
        var declaredRegions = DeclaredReservations.Of(documents.Select(d => d.Binding));

        // =========================================================================================
        // 🔴 AND THE NEIGHBOURS NOBODY DECLARED — DERIVED FROM THE PROGRAM (workbench Y1, 2026-08-23)
        // =========================================================================================
        //
        // Everything above this line is somebody's knowledge of the neighbourhood. `ReservedRegion`'s own
        // documentation admits the reach: "it can only ever see neighbours somebody wrote down", and the
        // panel that overwrote 53 tags would have collided just the same had the field been left blank —
        // WHICH IT WAS. `converter neighbours` reads every %M tag and every P#M area pointer in the
        // corpus and says who declares each one; this consumes that.
        //
        // The naming comes off the merged binding, and it is what the mirror's OWN objects are excluded
        // on — a CLOSED SET of two names from CopyLayerNaming, the shape BuildStamp.cs:192-199 already
        // uses. On the committed reference corpus that is not a nicety: all 26 %M claims inside
        // %M1000..%M1073 belong to the mirror's own tag table, and left in, every map would refuse
        // against its own tags.
        var namingDefaults = new CopyLayerNaming();
        var naming = new CopyLayerNaming(
            first.BlockName ?? namingDefaults.BlockName,
            first.BlockNumber ?? namingDefaults.BlockNumber,
            first.TagTableName ?? namingDefaults.TagTableName,
            first.TagPrefix ?? namingDefaults.TagPrefix);

        var reconciliation = NeighbourReconciler.Of(neighbourFact, geometry, naming, declaredRegions);

        // 🔴 *** A REFUSAL INPUT THAT WENT MISSING REFUSES. W5's FALLBACK SHAPE IS NOT COPIED HERE. ***
        //
        // `UnionPreflight` treats "could not consult the converter" as a REPORT, and for a reachability
        // report that is right. A neighbour list is not a report: it is the only thing standing between a
        // COMPUTED mirror extent and somebody else's registers, and a run that proceeds without one is
        // exactly the state that overwrote 53 tags on a running controller. So every way the derivation
        // can fail to arrive gates here — with ONE named escape, which is counted.
        if (neighbourFact.Gates)
        {
            refusals.Add(
                "THE NEIGHBOUR LIST COULD NOT BE DERIVED FROM THE PROGRAM CORPUS, and a mirror allocated against a "
                + "neighbour list nothing derived is the state that overwrote 53 tags on a running controller: "
                + neighbourFact.Why
                + " This is REFUSED rather than reported, because unlike a reachability finding it is an input to a "
                + "refusal: falling back to 'whatever the bindings declared' would restore the silence exactly. "
                + "Fix the corpus, or state the escape `--neighbours declared-only` — WHICH IS COUNTED.");

            foreach (var refusal in neighbourFact.Refusals)
                refusals.Add("the neighbour producer refused a construct in the corpus: " + refusal);

            return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, programPaths,
                null, served, neighbourFact, reconciliation);
        }

        // 🔴 DERIVED REGIONS ARE UNIONED WITH DECLARED ONES AND BOTH REACH THE MERGED BINDING. A derived
        // neighbour applied only to THIS geometry would be enforced once, at plan time, and thrown away —
        // and `run --merged <file>` re-allocates from the document, so it would allocate with no knowledge
        // of the occupant at all. That is the failure `MergeBindings` already carries a paragraph about,
        // and a derived reservation is no more durable than a declared one unless it is written down.
        //
        // The reconciler has already removed the parts a declaration covers, so the two sets cannot
        // overlap each other and the geometry's "two owners cannot hold the same registers" refusal keeps
        // meaning what it says: two AUTHORS disagree.
        var reserved = declaredRegions.Concat(reconciliation.Derived).ToArray();

        // The empty case must not reach Reserving, which throws on one — see DeclaredReservations, whose
        // whole reason for existing is that this rule is invisible at a call site.
        if (reserved.Length > 0)
            geometry = geometry.Reserving(reserved);

        var map = MapAllocator.Allocate(new WaveSetRequest(geometry, slots));

        if (!map.Allocated)
        {
            // Refused, never truncated. Dropping the lanes that do not fit would produce a batch that
            // runs and a set of lanes that silently did not.
            refusals.AddRange(map.Refusals.Select(r => "the merged map does not fit: " + r));
            return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, programPaths,
                null, served, neighbourFact, reconciliation);
        }

        // Resolved HERE rather than inside MergeBindings, so the reason a merge drops the field can be
        // carried onto the result and printed. Computing it in the serializer would leave the only
        // account of it inside a string nobody sees until a gate reads NOT CHECKED days later.
        var authority = SharedDeclarer(documents);

        var merged = MergeBindings(documents.Select(d => d.Binding).ToList(), reserved, authority.Name);

        return new BatchPlanResult(
            lanes.Count,
            lanes.Select(l => l.Name).ToArray(),
            Array.Empty<string>(),
            merged,
            map.Map,
            programPaths,
            reachability,
            served,
            neighbourFact,
            reconciliation,
            authority.Line);
    }

    /// <summary>
    /// The merged binding: lane 0's geometry and naming — every lane having been made to agree on both —
    /// with every lane's slots concatenated in queue order.
    /// </summary>
    /// <summary>
    /// 🔴 <b><paramref name="reserved"/> is passed in rather than re-derived, so the merged document
    /// states the reservations THE MAP WAS ACTUALLY ALLOCATED AGAINST</b> — not a second union that
    /// could disagree with the first.
    ///
    /// <para><b>Found 2026-08-23, on the deployment that exists because of the collision.</b> This method
    /// copied seven scalars and the slots and dropped <c>ReservedRegions</c> BY OMISSION — twenty lines
    /// after the comment explaining that silently dropping a neighbour "is the same class of silence this
    /// whole guard closes." The guard still bit at plan time, because the union reaches
    /// <c>geometry.Reserving</c> before allocation; what was lost is the DURABLE ARTIFACT. The merged
    /// binding is what <c>run --merged</c> consumes, what the copy layer is generated from, and what the
    /// next reader opens — and it no longer knew the neighbour existed.</para>
    ///
    /// <para>⚠️ <b>The failure that makes this more than tidiness:</b> <c>run --merged &lt;file&gt;</c> against
    /// a merged binding written by an earlier <c>plan</c> would allocate with NO neighbour knowledge at
    /// all. The reservation would have been enforced once, at plan time, and then thrown away — which is
    /// indistinguishable from never having declared one.</para>
    ///
    /// <para>Union semantics are the geometry's, unchanged: identical declarations collapse, and two
    /// lanes declaring overlapping-but-different regions are refused upstream rather than reconciled
    /// here. An empty union writes <c>null</c>, not <c>[]</c>, because <b>"no lane declared a neighbour"
    /// and "the area is otherwise empty" are different facts</b> and the field's own documentation turns
    /// on that distinction.</para>
    /// </summary>
    private static string MergeBindings(
        IReadOnlyList<BindingDocument> documents,
        IReadOnlyList<ReservedRegion> reserved,
        string? declaredBy)
    {
        var first = documents[0];
        var merged = new BindingDocument
        {
            DeclaredBy = declaredBy,
            BlockName = first.BlockName,
            BlockNumber = first.BlockNumber,
            TagTableName = first.TagTableName,
            TagPrefix = first.TagPrefix,
            BaseByte = first.BaseByte,
            RetentiveBytes = first.RetentiveBytes,
            DeclaredRegisters = first.DeclaredRegisters,
            ReservedRegions = reserved.Count == 0
                ? null
                : reserved
                    .Select(r => new ReservedRegionDocument
                    {
                        Register = r.Register,
                        Length = r.Length,
                        Owner = r.Owner,
                    })
                    .ToList(),
            Slots = documents.SelectMany(d => d.Slots ?? new List<SlotBindingDocument>()).ToList(),
        };

        return JsonSerializer.Serialize(merged, Json);
    }

    /// <summary>
    /// 🔴 <b>THE MERGED DOCUMENT'S <c>declaredBy</c> — CARRIED WHEN IT IS LOSSLESS, DROPPED WHEN IT IS NOT,
    /// AND NEVER COLLAPSED TO ONE NAME.</b>
    ///
    /// <para>Every other document-level field above goes through <see cref="AgreeOn"/>, which REFUSES
    /// disagreement. That is right for the geometry — <c>baseByte</c> is a property of one deployed
    /// harness, so two answers is a mistake. <b>It is wrong here.</b> An author is not a property of the
    /// deployment; different lanes legitimately have different coordinators, and refusing that would
    /// refuse a configuration nothing else in this system objects to. <c>AgreeOn</c> is therefore
    /// deliberately not used, and this is not an oversight.</para>
    ///
    /// <para>🔴 <b>THE OPTION RULED OUT ON EVIDENCE, NOT ON TASTE: the merged document declaring its own
    /// author (the batcher).</b> Gate 5c refuses a map declared by the block's author or a vector's. If
    /// the batch stamps its own name over lane B's, and lane B's coordinator is also lane B's block
    /// author, <b>the gate compares the batcher against the block author, finds them different, and
    /// PASSES</b> — a real conflict laundered into a green by the merge step. That is fail-OPEN, and it is
    /// the one outcome this whole gate exists to prevent.</para>
    ///
    /// <para><b>What is done instead.</b> When every contributing lane names the SAME declarer, collapsing
    /// to that one name loses nothing and it is carried — this is the ordinary case, one coordinator
    /// running a batch of their own lanes, and it keeps gate 5c a live verdict on batched runs rather than
    /// a permanent NOT CHECKED that would get switched off. When the lanes name DIFFERENT declarers, the
    /// merged document declares NOBODY: gate 5c then reads NOT CHECKED and says so by name, which is
    /// fail-closed and audible. <b>A batch is not silently weakened; it is loudly unattributed.</b></para>
    ///
    /// <para>🔴 <b>THE PROPERLY CORRECT ANSWER IS A SET, AND IT IS RULED NOT BUILT — 2026-08-24, owner,
    /// recorded at <c>docs/notes/owner-questions.md</c> D2. THAT IS A DECISION, NOT AN OVERSIGHT, AND
    /// THIS PARAGRAPH IS WHERE A READER WHO ARRIVES FROM GATE 5c IS MEANT TO LAND.</b>
    /// A merged map derived from two coordinators' bindings has TWO authorities, and the fail-closed
    /// comparison is "refuse if ANY contributing declarer is the block's author or a vector's". Nothing
    /// less than a set can express that: a joined string like <c>"agent-x; agent-y"</c> is worse than
    /// nothing, because <c>AgentIdentity.SameAs</c> would compare the whole joined literal and match
    /// neither party — a conflict rendered invisible by the formatting. Carrying a set means a PLURAL WIRE
    /// FIELD on <see cref="BindingDocument"/>, which every reader and gate 0b's unknown-field refusal see,
    /// and a plural <c>MapAuthor</c> on the domain map — a schema change across ~29 documents for a case
    /// no batch has yet produced. <b>The revisit trigger is the first real multi-coordinator batch, which
    /// is also the first moment the right semantics are knowable; until then the mixed-declarer batch is
    /// NOT CHECKED rather than guessed at, which is fail-CLOSED.</b></para>
    ///
    /// <para>⚠️ <b>THE NULL HAS TWO CAUSES AND THEY ARE NOT THE SAME REPAIR</b> — a message naming only
    /// one misleads on the other. <b>(1) Disagreement:</b> the lanes name different coordinators.
    /// <b>(2) Partial silence:</b> the lanes that spoke agree, but at least one lane said nothing — a
    /// batch attributed to the lanes that happened to say is not an attributed batch, the same rule as a
    /// partially-attributed enumeration set at gate 3d. A distinct-count alone would miss (2) entirely.
    /// There is a THIRD state, and it is deliberately not described as a merge effect: when NO lane
    /// states a declarer the merged document is unattributed for the ordinary reason a single binding is,
    /// and saying "your lanes disagreed" there would send the reader hunting for a disagreement that does
    /// not exist.</para>
    /// </summary>
    /// <returns>
    /// The name to carry — null when the merge cannot attribute — and, always, the line the plan prints.
    /// Both come from ONE evaluation on purpose: a second method recomputing the predicate to explain it
    /// is a message that drifts away from the behaviour it describes.
    /// </returns>
    private static (string? Name, string Line) SharedDeclarer(IReadOnlyList<(Lane Lane, BindingDocument Binding)> documents)
    {
        var named = documents
            .Where(d => !string.IsNullOrWhiteSpace(d.Binding.DeclaredBy))
            .ToArray();

        // Compared with the identity's own normalisation — trim, then case-insensitively — and not
        // ordinally, so `"agent-a"` and `"Agent-A "` are one voice here exactly as AgentIdentity.SameAs
        // makes them one party at the gate.
        //
        // *** IT IS DELIBERATELY NOT FORM-AWARE, AND THE ASYMMETRY IS SAFE IN THIS DIRECTION.  *** Since
        // 2026-08-24 `AgentIdentity` distinguishes a ROLE label from an INSTANCE label
        // `<session-id>/<agent-type>` and refuses to compare across them (docs/notes/
        // test-environment-contract.md §1.1). This collapse asks a DIFFERENT question — did every lane
        // name the same coordinator — and two lanes naming one coordinator in two forms are two distinct
        // strings here, so the merged document declares NOBODY and gate 5c reads NOT CHECKED. That is the
        // same fail-closed outcome the gate would reach itself, arrived at one step earlier; the failure
        // this must never have is a collapse that hides a conflict, and a stricter comparison cannot.
        var distinct = named
            .Select(d => d.Binding.DeclaredBy!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var silent = documents.Where(d => string.IsNullOrWhiteSpace(d.Binding.DeclaredBy)).ToArray();

        if (distinct.Length == 1 && silent.Length == 0)
        {
            return (documents[0].Binding.DeclaredBy,
                $"declared by '{distinct[0]}' — all {documents.Count} lane(s) named the same coordinator, so collapsing to one name lost nothing "
                + "and gate 5c stays a live verdict on this batch.");
        }

        // The third state, and it is NOT a merge effect: nobody was dropped, because nobody was named.
        if (distinct.Length == 0)
        {
            return (null,
                $"UNATTRIBUTED — no lane states `declaredBy` ({documents.Count} lane(s), none of them naming a coordinator). "
                + "That is an unattributed BINDING and not a merge effect: nothing was dropped here, because nothing was offered. "
                + "Gate 5c reads NOT CHECKED, and it closes the moment any lane's binding names its author.");
        }

        var because = distinct.Length > 1
            ? "the lanes name DIFFERENT coordinators (" + string.Join("; ", named
                .GroupBy(d => d.Binding.DeclaredBy!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => $"'{g.Key}' from {string.Join(", ", g.Select(d => "'" + d.Lane.Name + "'"))}")) + ")"
            : $"the lanes that spoke all named '{distinct[0]}'";

        var alsoSilent = silent.Length == 0
            ? string.Empty
            : $", and {silent.Length} lane(s) state no `declaredBy` at all ({string.Join(", ", silent.Select(d => "'" + d.Lane.Name + "'"))}) — "
                + "a batch attributed to the lanes that happened to say is not an attributed batch";

        return (null,
            $"UNATTRIBUTED — {because}{alsoSilent}. The merged document therefore declares NOBODY and gate 5c reads NOT CHECKED. "
            + "*** THAT IS DELIBERATE AND IT IS NOT REPAIRABLE BY EDITING THIS MERGE. *** Stamping one lane's name over the others is fail-OPEN "
            + "(if the dropped party is also the block's author, 5c compares the survivor, finds a difference and PASSES a real conflict), and the "
            + "plural `declaredBy` that would carry both authorities is RULED NOT BUILT — owner, 2026-08-24, docs/notes/owner-questions.md D2 — because "
            + "the correct answer is a SET and a joined string would compare as one literal matching neither party. "
            + "Re-plan under a single coordinator, or run the lane on its own binding.");
    }

    private static void AgreeOn<T>(
        IReadOnlyList<(Lane Lane, BindingDocument Binding)> documents,
        List<string> refusals,
        string field,
        Func<BindingDocument, T> read)
    {
        var distinct = documents
            .Select(d => (d.Lane.Name, Value: read(d.Binding)))
            .GroupBy(x => x.Value)
            .ToList();

        if (distinct.Count <= 1)
            return;

        var rendered = string.Join("; ", distinct.Select(g =>
            $"{Describe(g.Key)} from {string.Join(", ", g.Select(x => "'" + x.Name + "'"))}"));

        refusals.Add(
            $"the lanes disagree about `{field}`: {rendered}. That is a property of ONE deployed harness, not of a lane, "
            + "so it cannot be reconciled by choosing — picking one would deploy a mirror that half the lanes do not expect.");
    }

    private static string Describe<T>(T value) => value is null ? "<unstated>" : value.ToString() ?? "<unstated>";

    private static int VectorRegistersOf(SlotBindingDocument slot) =>
        (slot.VectorTargets ?? new List<MirroredSignalDocument>()).Sum(RegistersFor);

    /// <summary>
    /// 🔴 <b>VALUE registers PLUS LATCH registers, and the latch term is what this was missing.</b>
    ///
    /// <para>Measured on the rig 2026-08-22: this reported the vessel lane at <b>154</b> registers while
    /// the real derivation needed <b>165</b> — the difference being exactly its 11 transient result
    /// signals, each of which carries a latch register appended after the values. The planner therefore
    /// UNDER-counted, and a capacity gate that under-counts is worse than no gate: it says "fits" and
    /// hands the refusal to the step after it.</para>
    ///
    /// <para>The authority is <c>SlotBinding.ResultRegistersNeeded</c>, which says so in as many words —
    /// <i>"not the signal count, and not the value width alone — a transient signal also carries a LATCH
    /// register, so the budget moves when a latch is added."</i> This is the same sum over the JSON
    /// documents, and <c>BatchPlannerWidthTests</c> pins the two against each other so the copy cannot
    /// drift in silence.</para>
    /// </summary>
    private static int ResultRegistersOf(SlotBindingDocument slot)
    {
        var sources = slot.ResultSources ?? new List<MirroredSignalDocument>();
        return sources.Sum(RegistersFor) + sources.Count(s => s.Transient);
    }

    /// <summary>
    /// Width per signal, from the element table: <c>Bool</c> and <c>Int</c> take one register, <c>Time</c>
    /// takes two. Read from the type rather than assumed, because a Time counted as one register is a map
    /// that is short by a word per timer and overlaps the next slot.
    /// </summary>
    private static int RegistersFor(MirroredSignalDocument signal) =>
        signal.Type == MirrorValueType.Time ? 2 : 1;

    /// <summary>
    /// 🔴 <b>Delegates to <see cref="ProgramFiles"/> — this used to be a second copy of that rule.</b> Kept
    /// as a named method only because the call sites read better with it; it must never grow a behaviour of
    /// its own again. <c>ProgramFilesParityTests</c> holds the two together.
    /// </summary>
    private static IEnumerable<string> FilesUnder(string path) => ProgramFiles.Under(new[] { path });

    /// <summary>The report, with its denominators.</summary>
    public static string Describe(BatchPlanResult result)
    {
        var sb = new StringBuilder();
        sb.Append(result.Planned ? "BATCH PLANNED" : "BATCH REFUSED").Append('\n');
        sb.Append($"  lanes queued  {result.LanesQueued}\n");
        sb.Append($"  lanes batched {result.LanesBatched.Count}");
        if (result.LanesBatched.Count > 0)
            sb.Append("  (").Append(string.Join(", ", result.LanesBatched)).Append(')');
        sb.Append('\n');

        if (result.Map is { } map)
        {
            sb.Append($"  map           {map.ResultBlock.End} register(s) over {map.Slots.Count} slot(s), "
                + $"declared area {map.Geometry.DeclaredRegisters}\n");
            sb.Append($"  stamp input   base %M{map.Geometry.BaseByte}, map hash {map.MapHash[..12]}…\n");
        }

        // Printed on EVERY plan, pass or refuse. "Could not check" is a RESULT, and an unstated one reads
        // as "checked and fine" — which is exactly how an uncalled slot FC reached a controller.
        if (result.Reachability is { } reach)
            sb.Append("  scan      ").Append(reach.Summary).Append('\n');

        // 🔴 THE WIDTH'S PROVENANCE, ON EVERY PLAN. Either it was read off the block that serves it —
        // both homes of the number, named by line — or it was DECLARED and nothing corroborated it. The
        // second is the state the pipeline was in until 2026-08-23, and it must not print as a green.
        if (result.ServedArea is { } served)
        {
            sb.Append("  width     ").Append(served.Denominator).Append('\n');

            // The claim, bounded where the reader meets it. A derived width says the binding agrees with
            // the program that was STAGED; it says nothing about the CPU, which is a different question
            // answered only by probing the device.
            if (served.Derived)
            {
                sb.Append("            ^ read from the program CORPUS, not the controller — it cannot see whether the\n");
                sb.Append("              block it read is the block running on the CPU. Agreement with the STAGED program\n");
                sb.Append("              is the whole of the claim.\n");
            }
        }

        // 🔴 WHO ELSE IS IN THE AREA, ON EVERY PLAN — INCLUDING THE ZERO, WHICH IS THE ONE THAT MATTERS.
        // A neighbour line that appears only when something was found teaches a reader that its absence
        // means "clean", and the absence would in fact mean "nobody looked". That is the exact reading
        // that let a mirror grow into a virtual panel's command band.
        if (result.NeighbourReconciliation is { } neighbours)
        {
            sb.Append("  area      ").Append(neighbours.Denominator).Append('\n');

            foreach (var report in neighbours.Reports)
                sb.Append("            REPORTED  ").Append(report).Append('\n');

            // Printed under EVERY outcome, and deliberately loudest under the derived one — that is the
            // run whose reader is likeliest to widen it into "the area is free".
            sb.Append("            ^ WHAT THIS CANNOT SEE: an occupant reaching %M WITHOUT DECLARING a tag or an\n");
            sb.Append("              area pointer (indirect or pointer-computed access); anything outside the corpus it\n");
            sb.Append("              was handed; and whether that corpus is the program on the controller — it reads\n");
            sb.Append("              files, not the CPU. A zero means 'nothing in the corpus DECLARED a claim'.\n");
        }

        // 🔴 WHO THE MERGED BINDING DECLARES, ON EVERY PLAN THAT PRODUCED ONE — INCLUDING THE ATTRIBUTED
        // CASE. Printed unconditionally for the neighbour line's reason: a line that shows up only when
        // the field was dropped trains a reader to read its absence as "attributed", and the absence in
        // fact means nobody printed it. The unattributed text is the account gate 5c will give days
        // later, said here at the moment the field is actually dropped.
        if (result.MergedAuthority is { } authority)
            sb.Append("  authority ").Append(authority).Append('\n');

        foreach (var refusal in result.Refusals)
            sb.Append("  REFUSED  ").Append(refusal).Append('\n');

        return sb.ToString();
    }
}
