using System.Text;
using System.Text.Json;
using Harness.Gate;
using Harness.Map;

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
    IReadOnlyList<string> ProgramPaths)
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

    public static BatchPlanResult Plan(IReadOnlyList<Lane> lanes, Func<string, string> readFile)
    {
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(readFile);

        var refusals = new List<string>();

        // Empty is not clean. A batch of nothing plans perfectly, deploys perfectly and tests nothing —
        // and it is the outcome most likely to be believed, because every other number reads as valid.
        if (lanes.Count == 0)
        {
            return new BatchPlanResult(0, Array.Empty<string>(),
                new[] { "NOTHING BATCHED: the queue is empty. That is not a clean batch, it is a batch of nothing — and a deployment built from it would test nothing while reporting a success." },
                null, null, Array.Empty<string>());
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
            return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, Array.Empty<string>());

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

        if (refusals.Count > 0)
            return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, programPaths);

        // ---- Does the merged map fit what Modbus can reach? -------------------------------------
        var first = documents[0].Binding;
        var slots = documents
            .SelectMany(d => (d.Binding.Slots ?? new List<SlotBindingDocument>())
                .Select(s => new SlotRequest(s.SlotId!, Math.Max(1, VectorRegistersOf(s)), Math.Max(1, ResultRegistersOf(s)))))
            .ToArray();

        if (first.DeclaredRegisters is not int declared)
        {
            refusals.Add("no lane states `declaredRegisters`, so the merged map cannot be checked against what a Modbus client can reach. It is required per lane.");
            return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, programPaths);
        }

        var geometry = MirrorGeometry.ForCpu1214C(first.RetentiveBytes ?? 256, first.BaseByte ?? 1000, declared);
        var map = MapAllocator.Allocate(new WaveSetRequest(geometry, slots));

        if (!map.Allocated)
        {
            // Refused, never truncated. Dropping the lanes that do not fit would produce a batch that
            // runs and a set of lanes that silently did not.
            refusals.AddRange(map.Refusals.Select(r => "the merged map does not fit: " + r));
            return new BatchPlanResult(lanes.Count, Array.Empty<string>(), refusals, null, null, programPaths);
        }

        var merged = MergeBindings(documents.Select(d => d.Binding).ToList());

        return new BatchPlanResult(
            lanes.Count,
            lanes.Select(l => l.Name).ToArray(),
            Array.Empty<string>(),
            merged,
            map.Map,
            programPaths);
    }

    /// <summary>
    /// The merged binding: lane 0's geometry and naming — every lane having been made to agree on both —
    /// with every lane's slots concatenated in queue order.
    /// </summary>
    private static string MergeBindings(IReadOnlyList<BindingDocument> documents)
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

    private static IEnumerable<string> FilesUnder(string path)
    {
        if (File.Exists(path))
            return new[] { path };

        return Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.ir", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();
    }

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

        foreach (var refusal in result.Refusals)
            sb.Append("  REFUSED  ").Append(refusal).Append('\n');

        return sb.ToString();
    }
}
