using Converter.CrossCheck;
using Converter.SignalInventory;

namespace Converter.SignalSet;

// One block's SIGNAL SET as a single machine-readable document: every interface member it declares and
// every external signal it references, each carrying the half of a harness binding that is mechanically
// derivable — type, RETAIN, start value, direction relative to the block, and who else drives or reads it.
//
// Why it exists (2026-08-27): the harness binding document is hand-typed — 297 lines for one slot of 20
// signals — and roughly half of every entry is a restatement of what the block's own IR already says.
// Nothing emitted that half, so it was transcribed, and a transcription is the one step in this pipeline
// with no mechanical check behind it.
//
// DELIBERATELY NOT A NEW ANALYSIS. The join it needs — the typed inventory against the project usage
// graph — is the one CandidateScanRunner already performs for its own question; this is the same join
// asked to enumerate rather than to filter. Adding a second graph walk would give the project two
// answers to "does this block write this member" that could disagree, which is the failure mode
// `cross-check` and `undriven-scan` contradicting each other over one corpus made findable.
//
// It states facts and never a verdict: it does not say a signal is bindable, safe to force, or missing.
public static class SignalSetRunner
{
    public static SignalSetReport Run(
        string projectDir,
        string blockName,
        string originFilter,
        string? typeFilter,
        string directionFilter)
    {
        var graph = ProjectUsageGraph.Build(projectDir);
        var inventory = SignalInventory.SignalInventory.Build(projectDir);
        var warnings = inventory.Warnings.Concat(graph.Warnings).ToList();

        // FI-44 - resolve --block against the corpus BEFORE building anything. An emitter that answers
        // a question about a block which is not there returns an empty document that reads exactly
        // like a block with no signals, and a generator downstream cannot tell the two apart.
        if (!graph.BlockNames.Contains(blockName))
        {
            return new SignalSetReport(projectDir, inventory.FilesScanned, blockName, originFilter,
                typeFilter, directionFilter, Array.Empty<SignalEntry>(), warnings,
                SignalSetScope.UnknownBlock);
        }

        // Every placement of this block, in both forms: an instance DB of its own, and a
        // multi-instance static inside another FB (FI-50). Under C-132 a whole corpus can consist of
        // nothing but the second, so resolving only the first would silently lose every external
        // writer of every member.
        var placements = graph.InstanceToFb
            .Concat(graph.MultiInstanceToFb)
            .Where(kv => string.Equals(kv.Value, blockName, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var unfiltered = DeclaredInterface(graph, inventory, blockName, placements)
            .Concat(ExternalReferences(graph, inventory, blockName))
            .ToList();

        // 🔴 THE OPAQUE SET IS TAKEN FROM THE UNFILTERED ENTRIES, BEFORE --origin/--type/--direction
        // (FI-88). An opaque member is one whose leaves are MISSING, so it is precisely the entry a
        // filter is most likely to drop — a `--type Bool` run over a block whose interface type could
        // not be opened would otherwise return a clean, confident, empty-ish document. A filter must
        // narrow what is REPORTED and never what is GATED on.
        var inSet = new HashSet<string>(unfiltered.Select(e => e.Path), StringComparer.Ordinal);
        var opaque = inventory.OpaqueLeaves
            .Where(o => inSet.Contains(o.Path))
            .ToList();

        var entries = unfiltered
            .Where(e => MatchesOrigin(e, originFilter))
            .Where(e => typeFilter is null
                        || string.Equals(e.Type, typeFilter, StringComparison.OrdinalIgnoreCase))
            .Where(e => MatchesDirection(e.Direction, directionFilter))
            .OrderBy(e => e.Origin == SignalDeclaration.Interface ? 0 : 1)
            .ThenBy(e => e.Path, StringComparer.Ordinal)
            .ToList();

        // The filters are the second door onto "examined nothing": --type Word on a block of Bools
        // returns an empty document that is indistinguishable from a block with no signals. Reported
        // as a scope fact, never as a clean result.
        return new SignalSetReport(projectDir, inventory.FilesScanned, blockName, originFilter,
            typeFilter, directionFilter, entries, warnings,
            entries.Count == 0 ? SignalSetScope.NoSignalsInScope : SignalSetScope.Scanned,
            opaque);
    }

    // The block's own declared interface, read off the inventory rather than off the interface
    // SECTIONS: under C-132 the members that matter sit under STATIC inside interface-UDT structs, and
    // a section filter answers the wrong question on the real corpus (see SignalInventory).
    private static IEnumerable<SignalEntry> DeclaredInterface(
        ProjectUsageGraph graph,
        SignalInventory.SignalInventory inventory,
        string blockName,
        IReadOnlyList<string> placements)
    {
        var prefix = blockName + ".";

        foreach (var leaf in inventory.Leaves
                     .Where(l => l.Origin == SignalOrigin.FbInterface)
                     .Where(l => l.Path.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var suffix = leaf.Path[prefix.Length..];
            var (writers, readers) = InterfaceUsages(graph, blockName, suffix, placements);

            yield return new SignalEntry(
                suffix,
                leaf.Path,
                leaf.Type,
                leaf.IsRetain,
                leaf.StartValue,
                DirectionFor(blockName, writers, readers),
                SignalDeclaration.Interface,
                Sites(writers),
                Sites(readers));
        }
    }

    // 🔴 AN INTERFACE MEMBER IS ADDRESSED TWO WAYS AND BOTH HAVE TO BE RESOLVED — the repair
    // `undriven-scan` needed on 2026-08-18, where looking up one form only made 136 of 228 rows false.
    // From INSIDE the block the member is BARE and local (`IO.Step`); from every other block it is
    // ABSOLUTE on the placement (`iDB_Rack_RackA.IO.Step`).
    //
    // The owner restriction applies to the BARE form and must: `_usages` is keyed verbatim, so three
    // FBs each declaring their own `IO.Step` land on one key — the false-multi-writer defect
    // `cross-check` shipped. The absolute form names one placement and needs no such guard; that is
    // what makes it absolute.
    //
    // The placement is the FLOOR on the ancestor walk. `CALL FB(iDB, ...)` records a write at the bare
    // instance path, which is an ancestor of every member in it; admitting it marks the whole
    // interface written.
    private static (List<ProjectUsageGraph.UsageSite> Writers, List<ProjectUsageGraph.UsageSite> Readers)
        InterfaceUsages(
            ProjectUsageGraph graph,
            string blockName,
            string suffix,
            IReadOnlyList<string> placements)
    {
        var writers = new List<ProjectUsageGraph.UsageSite>();
        var readers = new List<ProjectUsageGraph.UsageSite>();

        var local = graph.UsagesReaching(suffix);
        writers.AddRange(local.Writers.Where(w => string.Equals(w.Block, blockName, StringComparison.Ordinal)));
        readers.AddRange(local.Readers.Where(r => string.Equals(r.Block, blockName, StringComparison.Ordinal)));

        foreach (var placement in placements)
        {
            var absolute = graph.UsagesReaching(placement + "." + suffix, notAbove: placement);
            writers.AddRange(absolute.Writers);
            readers.AddRange(absolute.Readers);
        }

        return (writers, readers);
    }

    // Every signal the block references that it does not declare itself. `OwnerOf` is the test that
    // separates a global path from a block-local one — it cannot be answered from the name, since an
    // FB static and a tag-table tag are both bare single-component references, and it is answered
    // exactly by whether the referencing block declares that root.
    private static IEnumerable<SignalEntry> ExternalReferences(
        ProjectUsageGraph graph,
        SignalInventory.SignalInventory inventory,
        string blockName)
    {
        var declared = inventory.Leaves.ToLookup(l => l.Path, StringComparer.Ordinal);

        var paths = graph.Flat
            .Where(f => string.Equals(f.Block, blockName, StringComparison.Ordinal))
            .Select(f => ProjectUsageGraph.StripSubscripts(f.Path))
            .Where(p => graph.OwnerOf(blockName, p) is null)
            // A CALL names its own state store: `CALL FB_Rack(iDB_Rack_RackA, ...)` records a
            // reference to the bare instance path. That is a placement, not a signal, and listing it
            // would put a row in the binding document that has no value to read or write.
            .Where(p => !graph.InstanceToFb.ContainsKey(p) && !graph.MultiInstanceToFb.ContainsKey(p))
            .Distinct(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            // The instance/DB root is the floor here for the same reason it is on the interface half.
            var dot = TagPath.IndexOfSeparator(path);
            var root = dot > 0 ? path[..dot] : null;
            var (writers, readers) = graph.UsagesReaching(path, notAbove: root);

            var leaf = declared[path].FirstOrDefault();

            yield return new SignalEntry(
                path,
                path,
                leaf?.Type,
                leaf?.IsRetain ?? false,
                leaf?.StartValue,
                DirectionFor(blockName, writers, readers),
                OriginOf(leaf),
                Sites(writers),
                Sites(readers));
        }
    }

    // An instance DB's members are inventoried as FbInterface — they mirror the FB's interface 1:1 —
    // so a REFERENCED path landing on one is another placement's storage, addressed absolutely.
    private static SignalDeclaration OriginOf(SignalLeaf? leaf) => leaf?.Origin switch
    {
        SignalOrigin.GlobalDb => SignalDeclaration.GlobalDb,
        SignalOrigin.TagTable => SignalDeclaration.TagTable,
        SignalOrigin.FbInterface => SignalDeclaration.InstanceDb,
        _ => SignalDeclaration.Undeclared,
    };

    // Relative to the SUBJECT block, from the same site lists the writer/reader columns report — one
    // derivation, so the direction can never contradict the sites printed beside it.
    private static SignalDirection DirectionFor(
        string blockName,
        IEnumerable<ProjectUsageGraph.UsageSite> writers,
        IEnumerable<ProjectUsageGraph.UsageSite> readers)
    {
        var writes = writers.Any(w => string.Equals(w.Block, blockName, StringComparison.Ordinal));
        var reads = readers.Any(r => string.Equals(r.Block, blockName, StringComparison.Ordinal));

        return (writes, reads) switch
        {
            (true, true) => SignalDirection.Both,
            (true, false) => SignalDirection.Written,
            (false, true) => SignalDirection.Read,
            _ => SignalDirection.Unused,
        };
    }

    private static IReadOnlyList<string> Sites(IEnumerable<ProjectUsageGraph.UsageSite> sites) =>
        sites.Select(s => $"{s.Block} N{s.Network}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

    private static bool MatchesOrigin(SignalEntry entry, string filter) => filter switch
    {
        "interface" => entry.Origin == SignalDeclaration.Interface,
        "external" => entry.Origin != SignalDeclaration.Interface,
        _ => true,
    };

    private static bool MatchesDirection(SignalDirection direction, string filter) =>
        filter == "any" || string.Equals(direction.ToString(), filter, StringComparison.OrdinalIgnoreCase);
}
