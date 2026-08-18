using Converter.CrossCheck;
using Converter.SignalInventory;
using Converter.Trace;

namespace Converter.UndrivenScan;

// FI-39 check 4: for each INSTANCE of an FB, which of the interface members the FB reads actually
// receive a value — driven / disarmed / defaulted / undriven, computed.
//
// The real new value over the project-wide reference graph is PER-INSTANCE resolution. That graph
// canonicalizes to (FB, member) and pools across every instance, which is correct for its own question
// and wrong for this one: if one instance drives a member and another does not, the pooled view shows
// the member alive and the gap disappears. A dropped rotation-sensor bypass on one instance of a shared
// VSD block is exactly that shape (`docs/evidence/PlantAutoControl-bench-grading.md`, REQ-012).
public static class UndrivenScanRunner
{
    public static UndrivenScanReport Run(
        string projectDir,
        string fbName,
        IReadOnlyList<string> instanceFilter,
        IReadOnlyList<string> callerFiles,
        bool includeHints = false)
    {
        var graph = ProjectUsageGraph.Build(projectDir, callerFiles);
        var inventory = SignalInventory.SignalInventory.Build(projectDir);

        // FI-44 - resolve --fb against the corpus BEFORE scanning. Without this the two "found
        // nothing" cases below fell through to an empty row set and reported success.
        if (!graph.BlockNames.Contains(fbName))
        {
            return new UndrivenScanReport(projectDir, inventory.FilesScanned, fbName, callerFiles,
                Array.Empty<MemberDrive>(), inventory.Warnings.Concat(graph.Warnings).ToList(),
                ScanScope.UnknownBlock);
        }

        // FI-50. An instance is an instance DB OR a multi-instance static inside another FB. Only
        // counting the first meant that under C-132 — where the house style is one STATIC UDT and
        // FBs are placed as multi-instances — this scan examined nothing and said so quietly on a
        // whole corpus. Both forms carry per-instance state and both can be left undriven.
        var allInstances = graph.InstanceToFb
            .Concat(graph.MultiInstanceToFb)
            .Where(kv => string.Equals(kv.Value, fbName, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .ToList();

        if (allInstances.Count == 0)
        {
            return new UndrivenScanReport(projectDir, inventory.FilesScanned, fbName, callerFiles,
                Array.Empty<MemberDrive>(), inventory.Warnings.Concat(graph.Warnings).ToList(),
                ScanScope.NoInstances);
        }

        var instances = allInstances
            .Where(i => instanceFilter.Count == 0 || instanceFilter.Contains(i, StringComparer.Ordinal))
            .OrderBy(i => i, StringComparer.Ordinal)
            .ToList();

        // FI-44 again, one level in. `--instance` naming nothing that exists produced zero rows and
        // EXIT 0 — a filter typo read exactly like a clean sweep of every instance.
        if (instances.Count == 0)
        {
            return new UndrivenScanReport(projectDir, inventory.FilesScanned, fbName, callerFiles,
                Array.Empty<MemberDrive>(), inventory.Warnings.Concat(graph.Warnings).ToList(),
                ScanScope.NoInstancesMatchedFilter);
        }

        // In scope: members the FB READS (inputs — "undriven" means something), PLUS members nothing
        // touches at all. That second group matters and is easy to miss: a declared input the FB never
        // reads AND no caller writes is inert on both sides, which is exactly the documented
        // dropped-bypass shape (the FB offers a rotation-sensor input; nothing wires it, nothing consumes
        // it). Filtering to "members the FB reads" alone would have excluded the very defect this check
        // exists to catch. Members the FB WRITES are out of scope — a status nobody consumes is the
        // project-wide reference graph's question, not this one.
        var scopedMembers = graph.InstanceMemberPaths
            .Concat(graph.MultiInstanceMemberPaths)
            .Where(p => instances.Contains(p.Item1, StringComparer.Ordinal))
            .Select(p => p.Item2)
            .Distinct(StringComparer.Ordinal)
            .Where(suffix => !IsWrittenByFb(graph, fbName, suffix))
            .Where(suffix => IsReadByFb(graph, fbName, suffix) || IsUntouchedByFb(graph, fbName, suffix))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        // 🔴 FI-44, THE THIRD SHAPE — 2026-08-18. The block exists, it HAS instances, and the scope
        // filter above still emptied the set: every interface member is one the FB itself writes, so
        // there is no caller-driven input left to resolve. That is a perfectly ordinary state for a
        // block that only PUBLISHES — and it produced `0 member/instance pair(s)` and EXIT 0, which is
        // indistinguishable from a thorough scan that found nothing wrong.
        //
        // Measured on a live corpus: TWO OF THE THREE LARGEST BLOCKS reported exactly that, and both
        // were read as passes. The two earlier guards (unknown block, no instances) were written
        // against the ways a scan could examine nothing THAT WERE KNOWN THEN; this is the way that was
        // not, and the lesson is that the count of rows is the thing to key on, not the reasons.
        if (scopedMembers.Count == 0)
        {
            return new UndrivenScanReport(projectDir, inventory.FilesScanned, fbName, callerFiles,
                Array.Empty<MemberDrive>(), inventory.Warnings.Concat(graph.Warnings).ToList(),
                ScanScope.NoMembersInScope);
        }

        var startValues = inventory.Leaves.ToLookup(l => l.Path, StringComparer.Ordinal);

        var rows = new List<MemberDrive>();
        foreach (var instance in instances)
        {
            foreach (var member in scopedMembers)
            {
                rows.Add(Classify(graph, inventory, startValues, fbName, instance, member, includeHints));
            }
        }

        return new UndrivenScanReport(projectDir, inventory.FilesScanned, fbName, callerFiles, rows,
            inventory.Warnings.Concat(graph.Warnings).ToList());
    }

    // 🔴 ALL THREE OF THESE RESOLVE THROUGH `UsagesReaching`, NOT THROUGH A VERBATIM KEY LOOKUP
    // (2026-08-18). A block that writes a whole struct — `MOVE(IN := DB_Param.Recipe[3]) => Selected` —
    // writes every member of it, under a usage key that names no member. Looking `Selected.SRID` up
    // verbatim finds nothing, so the FB's own output members survived the scope filter below and were
    // then reported UNDRIVEN to their caller: the caller is not supposed to drive them at all.
    private static bool IsReadByFb(ProjectUsageGraph graph, string fbName, string suffix) =>
        graph.UsagesReaching(suffix).Readers.Any(r => string.Equals(r.Block, fbName, StringComparison.Ordinal));

    // A member the FB WRITES is an output it reports, not an input the caller is meant to drive — even
    // when the FB also reads it back (an internal latch exposed on the interface). Reporting those as
    // "undriven (default FALSE)" from the caller's side was ~2/3 of this scan's output and actively
    // misleading: a reader can mistake an FB output's default for a missing wire.
    private static bool IsWrittenByFb(ProjectUsageGraph graph, string fbName, string suffix) =>
        graph.UsagesReaching(suffix).Writers.Any(w => string.Equals(w.Block, fbName, StringComparison.Ordinal));

    // Declared on the interface but neither read nor written by the FB itself.
    private static bool IsUntouchedByFb(ProjectUsageGraph graph, string fbName, string suffix)
    {
        var (writers, readers) = graph.UsagesReaching(suffix);
        return !readers.Concat(writers).Any(s => string.Equals(s.Block, fbName, StringComparison.Ordinal));
    }

    private static MemberDrive Classify(
        ProjectUsageGraph graph,
        SignalInventory.SignalInventory inventory,
        ILookup<string, SignalLeaf> startValues,
        string fbName,
        string instance,
        string member,
        bool includeHints)
    {
        // FI-50. An instance DB is addressed by its own name from outside (`iDB_X.IO.Step`), but a
        // MULTI-INSTANCE is addressed from inside its owner by its bare static name
        // (`ValveWater.IO.Step`) — there is no instance root in the text at all. So the lookup uses
        // the local form, then restricts to the owning block: two FBs that both happen to declare a
        // `ValveWater` would otherwise pool each other's writers and each mask the other's gap.
        var isMulti = graph.MultiInstanceOrigin.TryGetValue(instance, out var origin);

        // 🔴 A MULTI-INSTANCE MEMBER IS ADDRESSED TWO WAYS, AND ONLY ONE OF THEM WAS EVER LOOKED UP
        // (2026-08-18). From INSIDE the owning FB it is bare and local — `ValveDrain.IO.InHand`. From
        // ANY OTHER BLOCK it is absolute and rooted on the owner's instance DB —
        // `iDB_SiloVessel_SiloW.ValveDrain.IO.InHand`. This method resolved the local form only, so
        // every write from an orchestrator, a command decoder or a startup block was invisible.
        //
        // MEASURED: six members reported UNDRIVEN on all sixteen placements of one valve FB — 96 false
        // reports — while `cross-check`, reading the SAME graph, listed the writers of each absolute
        // path plainly. Two tools contradicting each other over one corpus is what made it findable,
        // and a check wrong in one direction 60% of the time cannot be trusted in the other.
        //
        // The owner restriction applies to the LOCAL form only, and must: a bare `ValveDrain.IO.InHand`
        // could belong to any FB that happens to declare a `ValveDrain`, so pooling those would let one
        // block's wiring mask another's gap. The ABSOLUTE form names one placement and needs no such
        // guard — that is exactly what makes it absolute.
        var lookups = new List<(string Path, string Root, string? RestrictToBlock)>();
        if (isMulti)
        {
            lookups.Add(($"{origin.LocalRoot}.{member}", origin.LocalRoot, origin.OwnerFb));
            lookups.Add(($"{instance}.{member}", instance, null));
        }
        else
        {
            lookups.Add(($"{instance}.{member}", instance, null));
        }

        var path = isMulti ? $"{origin.LocalRoot}.{member}" : $"{instance}.{member}";

        var writers = new List<ProjectUsageGraph.UsageSite>();
        foreach (var (candidate, root, restrictTo) in lookups)
        {
            // The instance root is the FLOOR, never an ancestor that drives: `CALL FB(iDB, ...)`
            // records a write at the bare iDB path, and admitting it would mark every member driven.
            var found = graph.UsagesReaching(candidate, notAbove: root).Writers;
            writers.AddRange(restrictTo is null
                ? found
                : found.Where(w => string.Equals(w.Block, restrictTo, StringComparison.Ordinal)));
        }

        // A declaration-site placement (`FB_X/Member`) has no real root, so its absolute form is not a
        // path any logic writes; the local lookup above is the only one that can resolve. Nothing to do
        // here beyond noting that the union is over candidates, not over guesses.
        var writerNames = writers
            .Select(w => $"{w.Block} N{w.Network}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        if (writers.Count > 0)
        {
            var allDisarmed = writers.All(w => DisarmAnalysis.IsProvablyFalse(w.Guard));
            return new MemberDrive(instance, member,
                allDisarmed ? DriveState.Disarmed : DriveState.Driven,
                null, writerNames, Array.Empty<string>());
        }

        var startValue = startValues[path].FirstOrDefault()?.StartValue;
        var state = IsUntouchedByFb(graph, fbName, member)
            ? DriveState.DeadInterfaceMember
            : startValue is null ? DriveState.Undriven : DriveState.UndrivenDefault;

        return new MemberDrive(instance, member, state, startValue, writerNames,
            includeHints ? NameJoinHints(graph, inventory, member) : Array.Empty<string>());
    }

    // A LABELLED HINT, never part of the exit condition. Token matching is a heuristic — a member called
    // `RotationSensor` and a field signal called `...RotSen` match only under a fuzzy rule — so it is
    // offered as colour for a human and kept out of anything mechanical.
    private static IReadOnlyList<string> NameJoinHints(
        ProjectUsageGraph graph,
        SignalInventory.SignalInventory inventory,
        string member)
    {
        var tokens = Tokens(member.Split('.').Last());
        if (tokens.Count < 2)
        {
            // A single-token member name (`Stop`, `InHand`) cannot be matched discriminatingly — one
            // shared word is coincidence, not a lead. Offer nothing rather than noise.
            return Array.Empty<string>();
        }

        return inventory.Leaves
            .Where(l => l.Origin is SignalOrigin.GlobalDb or SignalOrigin.TagTable)
            .Where(l => !IsReferencedAnywhere(graph, l.Path))
            .Where(l => SharedTokenCount(tokens, Tokens(l.Leaf)) >= 2)
            .Select(l => l.Path)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .Take(3)
            .ToList();
    }

    // Fuzzy-but-stated rule: CamelCase chunks plus a 3-letter abbreviation of each long chunk, so
    // `RotationSensor` can reach `RotSen`. Requiring TWO shared tokens is what keeps that catch while
    // discarding coincidence — a rule matching on one token alone pulls in every name sharing a common
    // word like `Hand` or `Time`, which buries the signal it exists to surface.
    private static IReadOnlyList<string> Tokens(string leaf)
    {
        var parts = System.Text.RegularExpressions.Regex
            .Matches(leaf, "[A-Z][a-z]*")
            .Select(m => m.Value)
            .Where(p => p.Length >= 3)
            .ToList();

        var tokens = new List<string>();
        foreach (var part in parts)
        {
            tokens.Add(part);
            if (part.Length >= 5)
            {
                tokens.Add(part[..3]); // Rotation -> Rot, Sensor -> Sen
            }
        }

        return tokens.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Two names share a token when either side's token is a prefix of the other's — so `Rot` matches
    // `Rotation`, without `Rot` matching an unrelated word that merely contains those letters.
    private static int SharedTokenCount(IReadOnlyList<string> a, IReadOnlyList<string> b) =>
        a.Count(t => b.Any(o =>
            o.StartsWith(t, StringComparison.OrdinalIgnoreCase)
            || t.StartsWith(o, StringComparison.OrdinalIgnoreCase)));

    private static bool IsReferencedAnywhere(ProjectUsageGraph graph, string path) =>
        graph.Usages.TryGetValue(path, out var usage)
        && (usage.Readers.Count > 0 || usage.Writers.Count > 0);
}
