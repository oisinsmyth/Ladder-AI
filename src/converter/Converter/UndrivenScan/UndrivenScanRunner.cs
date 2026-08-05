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

        // Every instance DB of this FB, unless the caller narrowed it.
        var instances = graph.InstanceToFb
            .Where(kv => string.Equals(kv.Value, fbName, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .Where(i => instanceFilter.Count == 0 || instanceFilter.Contains(i, StringComparer.Ordinal))
            .OrderBy(i => i, StringComparer.Ordinal)
            .ToList();

        // In scope: members the FB READS (inputs — "undriven" means something), PLUS members nothing
        // touches at all. That second group matters and is easy to miss: a declared input the FB never
        // reads AND no caller writes is inert on both sides, which is exactly the documented
        // dropped-bypass shape (the FB offers a rotation-sensor input; nothing wires it, nothing consumes
        // it). Filtering to "members the FB reads" alone would have excluded the very defect this check
        // exists to catch. Members the FB WRITES are out of scope — a status nobody consumes is the
        // project-wide reference graph's question, not this one.
        var scopedMembers = graph.InstanceMemberPaths
            .Where(p => instances.Contains(p.InstanceDb, StringComparer.Ordinal))
            .Select(p => p.Suffix)
            .Distinct(StringComparer.Ordinal)
            .Where(suffix => !IsWrittenByFb(graph, fbName, suffix))
            .Where(suffix => IsReadByFb(graph, fbName, suffix) || IsUntouchedByFb(graph, fbName, suffix))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

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

    private static bool IsReadByFb(ProjectUsageGraph graph, string fbName, string suffix) =>
        graph.Usages.TryGetValue(suffix, out var usage)
        && usage.Readers.Any(r => string.Equals(r.Block, fbName, StringComparison.Ordinal));

    // A member the FB WRITES is an output it reports, not an input the caller is meant to drive — even
    // when the FB also reads it back (an internal latch exposed on the interface). Reporting those as
    // "undriven (default FALSE)" from the caller's side was ~2/3 of this scan's output and actively
    // misleading: a reader can mistake an FB output's default for a missing wire.
    private static bool IsWrittenByFb(ProjectUsageGraph graph, string fbName, string suffix) =>
        graph.Usages.TryGetValue(suffix, out var usage)
        && usage.Writers.Any(w => string.Equals(w.Block, fbName, StringComparison.Ordinal));

    // Declared on the interface but neither read nor written by the FB itself.
    private static bool IsUntouchedByFb(ProjectUsageGraph graph, string fbName, string suffix) =>
        !graph.Usages.TryGetValue(suffix, out var usage)
        || !usage.Readers.Concat(usage.Writers).Any(s => string.Equals(s.Block, fbName, StringComparison.Ordinal));

    private static MemberDrive Classify(
        ProjectUsageGraph graph,
        SignalInventory.SignalInventory inventory,
        ILookup<string, SignalLeaf> startValues,
        string fbName,
        string instance,
        string member,
        bool includeHints)
    {
        var path = $"{instance}.{member}";
        var writers = graph.Usages.TryGetValue(path, out var usage)
            ? usage.Writers
            : new List<ProjectUsageGraph.UsageSite>();

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
