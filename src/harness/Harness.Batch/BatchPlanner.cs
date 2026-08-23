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
    ServedAreaFact? ServedArea = null)
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
        IReadOnlyList<Lane> lanes, Func<string, string> readFile, ServedAreaFact? servedArea = null)
    {
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(readFile);

        // "Nobody asked" is a state and it is carried, not defaulted away. A result whose ServedArea is
        // silently null reads, on every surface that prints it, exactly like one where the check ran.
        var served = servedArea ?? ServedAreaFact.NotAsked;

        var refusals = new List<string>();

        // Empty is not clean. A batch of nothing plans perfectly, deploys perfectly and tests nothing —
        // and it is the outcome most likely to be believed, because every other number reads as valid.
        if (lanes.Count == 0)
        {
            return new BatchPlanResult(0, Array.Empty<string>(),
                new[] { "NOTHING BATCHED: the queue is empty. That is not a clean batch, it is a batch of nothing — and a deployment built from it would test nothing while reporting a success." },
                null, null, Array.Empty<string>(), null, served);
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
            return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, Array.Empty<string>(), null, served);

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
            return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, programPaths, null, served);

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
                return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, programPaths, null, served);
            }

            declared = served.Registers;
        }

        if (refusals.Count > 0)
            return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, programPaths, null, served);

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
        var reserved = DeclaredReservations.Of(documents.Select(d => d.Binding));

        geometry = DeclaredReservations.AppliedTo(geometry, documents.Select(d => d.Binding));

        var map = MapAllocator.Allocate(new WaveSetRequest(geometry, slots));

        if (!map.Allocated)
        {
            // Refused, never truncated. Dropping the lanes that do not fit would produce a batch that
            // runs and a set of lanes that silently did not.
            refusals.AddRange(map.Refusals.Select(r => "the merged map does not fit: " + r));
            return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, programPaths, null, served);
        }

        var merged = MergeBindings(documents.Select(d => d.Binding).ToList(), reserved);

        return new BatchPlanResult(
            lanes.Count,
            lanes.Select(l => l.Name).ToArray(),
            Array.Empty<string>(),
            merged,
            map.Map,
            programPaths,
            reachability,
            served);
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
        IReadOnlyList<ReservedRegion> reserved)
    {
        var first = documents[0];
        var merged = new BindingDocument
        {
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

        foreach (var refusal in result.Refusals)
            sb.Append("  REFUSED  ").Append(refusal).Append('\n');

        return sb.ToString();
    }
}
