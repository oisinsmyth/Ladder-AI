using Converter.CrossCheck;
using Converter.SignalInventory;

namespace Converter.CandidateScan;

// FI-39 check 1: given a requirement's target scope and the FB an instance uses, COMPUTE every signal
// that could satisfy it — the IO half from the project's signal inventory, the FB half from that block's
// own interface. "More than one candidate" becomes a computed fact instead of a judgement call.
//
// Why: two defects shipped because a requirement phrase ("not faulted", "running feedback") admitted
// more than one signal and the single reader who resolved it never noticed there was a choice — the
// spec stage and the review stage read the same words the same way (`.../PlantAutoControl-bench-autopsy.md`
// section 2-C). Nothing computed a candidate set, so nothing could flag the ambiguity.
//
// This tool reports what is in scope. It never says which one the requirement means — that is the
// engineer's call, and pretending otherwise would be inventing a verdict.
public static class CandidateScanRunner
{
    public static CandidateScanReport Run(
        string projectDir,
        string fbName,
        string? instance,
        IReadOnlyList<string> scopes,
        string? typeFilter,
        string direction,
        IReadOnlyList<string> phrases)
    {
        var inventory = SignalInventory.SignalInventory.Build(projectDir);
        var graph = ProjectUsageGraph.Build(projectDir);

        var io = CollectIoCandidates(inventory, graph, scopes, typeFilter);
        var fb = CollectFbCandidates(inventory, graph, fbName, direction, typeFilter);

        // Same-typed IO in the named scope vs FB members of matching direction — the transposition shape.
        var family = new FamilyFact(io.Count, fb.Count);

        // Advisory ONLY. Filtering a candidate set by name resemblance is precisely the reasoning that
        // produced the swapped-pairing defect ("Op" reads as operational, "Ready" as remote — each
        // self-evident, one of them wrong). Reporting the subset is useful; narrowing on it would launder
        // the same bias behind a computed-looking number, so the exit code keys off the unfiltered size.
        var phraseMatches = phrases.Count == 0
            ? Array.Empty<string>()
            : io.Select(c => c.Path)
                .Concat(fb.Select(c => c.Member))
                .Where(name => phrases.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

        var warnings = inventory.Warnings.Concat(graph.Warnings).ToList();

        return new CandidateScanReport(projectDir, inventory.FilesScanned, fbName, instance, scopes,
            typeFilter, direction, io, fb, family, phraseMatches, warnings);
    }

    // FI-44. --scope was a pure path PREFIX, which silently assumes DB-qualified signals
    // ("DiscreteInputs."). Under C-001 a physical-IO tag is <DI|DQ|AI|AQ><n>_<Equipment>_<Signal>,
    // so the equipment token sits in the MIDDLE and no prefix can address it. The result was not a
    // missing feature but a false clean: scoping to a piece of equipment returned zero candidates
    // and exit 0 on a genuinely contested binding, and the only way to get a finding was to name the
    // disputed signals - i.e. to already know the answer, which inverts the tool's purpose.
    //
    // Deliberately NOT generic segment matching: splitting every path on every separator would let
    // "--scope DB" match the whole corpus. This matches the C-001 equipment POSITION specifically,
    // and only on tag-table signals, so DB path behaviour is untouched.
    private static bool MatchesScope(SignalLeaf leaf, string scope)
    {
        if (leaf.Path.StartsWith(scope, StringComparison.Ordinal))
        {
            return true;
        }

        return leaf.Origin == SignalOrigin.TagTable
               && string.Equals(C001EquipmentToken(leaf.Path), scope, StringComparison.Ordinal);
    }

    // The <Equipment> field of a C-001 physical-IO tag, or null if the name is not in that form.
    private static string? C001EquipmentToken(string path)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            path, @"^(?:DI|DQ|AI|AQ)\d+_([A-Za-z0-9]+)_");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static List<IoCandidate> CollectIoCandidates(
        SignalInventory.SignalInventory inventory,
        ProjectUsageGraph graph,
        IReadOnlyList<string> scopes,
        string? typeFilter)
    {
        if (scopes.Count == 0)
        {
            return new List<IoCandidate>();
        }

        return inventory.Leaves
            .Where(l => l.Origin is SignalOrigin.GlobalDb or SignalOrigin.TagTable)
            .Where(l => scopes.Any(s => MatchesScope(l, s)))
            .Where(l => typeFilter is null || string.Equals(l.Type, typeFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => l.Path, StringComparer.Ordinal)
            .Select(l => new IoCandidate(l.Path, l.Type, ReadersOf(graph, l.Path)))
            .ToList();
    }

    private static List<FbCandidate> CollectFbCandidates(
        SignalInventory.SignalInventory inventory,
        ProjectUsageGraph graph,
        string fbName,
        string direction,
        string? typeFilter)
    {
        var prefix = fbName + ".";

        return inventory.Leaves
            .Where(l => l.Origin == SignalOrigin.FbInterface && l.Path.StartsWith(prefix, StringComparison.Ordinal))
            .Where(l => typeFilter is null || string.Equals(l.Type, typeFilter, StringComparison.OrdinalIgnoreCase))
            .Select(l =>
            {
                var suffix = l.Path[prefix.Length..];
                var role = RoleOf(graph, fbName, suffix);
                return new FbCandidate(suffix, l.Type, role, WriterSitesOf(graph, fbName, suffix));
            })
            .Where(c => MatchesDirection(c.Role, direction))
            .OrderBy(c => c.Member, StringComparer.Ordinal)
            .ToList();
    }

    // Status vs command is COMPUTED: does the FB write this member, or read it? The alternative —
    // trusting the interface section — is wrong on the real corpus, where a block's reportable status
    // members sit under STATIC while its INPUT/OUTPUT sections hold data-link words.
    private static MemberRole RoleOf(ProjectUsageGraph graph, string fbName, string suffix)
    {
        if (!graph.Usages.TryGetValue(suffix, out var usage))
        {
            return MemberRole.Unused;
        }

        var writes = usage.Writers.Any(w => string.Equals(w.Block, fbName, StringComparison.Ordinal));
        var reads = usage.Readers.Any(r => string.Equals(r.Block, fbName, StringComparison.Ordinal));

        return (writes, reads) switch
        {
            (true, true) => MemberRole.Both,
            (true, false) => MemberRole.Status,
            (false, true) => MemberRole.Command,
            _ => MemberRole.Unused,
        };
    }

    private static bool MatchesDirection(MemberRole role, string direction) => direction switch
    {
        "status" => role is MemberRole.Status or MemberRole.Both,
        "command" => role is MemberRole.Command or MemberRole.Both,
        _ => true,
    };

    private static IReadOnlyList<string> ReadersOf(ProjectUsageGraph graph, string path) =>
        graph.Usages.TryGetValue(path, out var usage)
            ? usage.Readers.Select(r => $"{r.Block} N{r.Network}")
                .Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList()
            : Array.Empty<string>();

    private static IReadOnlyList<string> WriterSitesOf(ProjectUsageGraph graph, string fbName, string suffix) =>
        graph.Usages.TryGetValue(suffix, out var usage)
            ? usage.Writers.Where(w => string.Equals(w.Block, fbName, StringComparison.Ordinal))
                .Select(w => $"{w.Block} N{w.Network}")
                .Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList()
            : Array.Empty<string>();
}
