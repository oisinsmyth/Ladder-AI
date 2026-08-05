using System.Text.RegularExpressions;
using Converter.RelationReconcile;
using Converter.SignalInventory;

namespace Converter.SignalSweep;

// FI-39 check 5: the project-level residual sweep, computed.
//
// Its value is EXACTNESS, not a catch — the study grades it Medium and it has no oracle. What it
// replaces is an artifact's own self-reported approximations ("Named members swept: ~190",
// "out-of-scope-instance: ~80") with computed integers and an exact residue. An approximate denominator
// cannot support a completeness claim; an exact one can.
public static class SignalSweepRunner
{
    // `### `DiscreteInputs` (DB 15)` — the qualification source. The disposition tables list BARE leaf
    // names, so without the enclosing heading every leaf silently fails to match the inventory's
    // fully-qualified paths.
    //
    // Any backticked heading, not just an identifier-shaped one: a real PLC tag table is routinely
    // called "Default tag table", and an identifier-only pattern made that container undispositionable
    // by construction. A heading that names no container in the export is warned about below, so
    // widening the match cannot turn a mismatch into a silent pass.
    private static readonly Regex ContainerHeading = new(@"^###\s+`([^`]+)`", RegexOptions.Compiled);

    private static readonly Regex TableRow = new(@"^\|(.+)\|", RegexOptions.Compiled);

    public static SignalSweepReport Run(string projectDir, string specsDir, string? registerPath, string? unclaimedPath)
    {
        var warnings = new List<string>();
        var inventory = SignalInventory.SignalInventory.Build(projectDir);

        // Denominator: every global-DB leaf and tag-table tag in the corpus. Exact and cheap.
        // Kept as LEAVES rather than paths: a tag-table tag's path is bare, so its container (the
        // qualifier a disposition heading supplies) is only knowable from the leaf.
        var swept = inventory.Leaves
            .Where(l => l.Origin is SignalOrigin.GlobalDb or SignalOrigin.TagTable)
            .GroupBy(l => l.Path, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(l => l.Path, StringComparer.Ordinal)
            .ToList();

        if (swept.Count == 0)
        {
            throw new SignalSweepFormatException(
                $"sweep: no global-DB or tag-table signals found under '{projectDir}' — nothing to sweep. " +
                "Refusing to report full coverage of an empty denominator.");
        }

        var claimed = CollectClaimed(specsDir, registerPath, warnings);
        var (disposed, headings) = unclaimedPath is null
            ? (new HashSet<string>(StringComparer.Ordinal), new List<string>())
            : CollectDisposed(unclaimedPath);
        var tableRead = headings.Count > 0;

        if (unclaimedPath is not null && !tableRead)
        {
            throw new SignalSweepFormatException(
                $"sweep: no '### `Db`' disposition tables found in '{unclaimedPath}'. " +
                "Refusing to report signals as unaccounted when the disposition leg was not read.");
        }

        var containers = swept.Select(l => l.ContainerName).ToHashSet(StringComparer.Ordinal);
        foreach (var heading in headings.Where(h => !containers.Contains(h)).Distinct(StringComparer.Ordinal))
        {
            // A heading naming no container qualifies its rows into nothing, and every signal beneath it
            // then reads as unaccounted for a reason the report otherwise never states. Said out loud,
            // the mismatch is a five-second fix; unsaid, it is exactly the silent noise FI-45 is about.
            warnings.Add(
                $"sweep: disposition heading '{heading}' names no global DB or tag table in the export — " +
                "its rows qualify against nothing, so the signals under it will read as unaccounted.");
        }

        var signals = swept
            .Select(l =>
            {
                var kind = l.Origin == SignalOrigin.TagTable ? SignalContainerKind.TagTable : SignalContainerKind.Db;

                // The FI-45 item 2 fix. A disposition row is qualified `<heading>.<leaf>`; a DB member's
                // own path already IS that, but a tag-table tag's path is bare (deliberately — see
                // SignalLeaf). So qualify the tag here, on the sweep side only, using its real table
                // name. Strict equality both sides: a bare-name match against ANY heading would let a
                // same-named DB member disposition a tag that nobody has actually accounted for.
                var dispositionKey = kind == SignalContainerKind.TagTable
                    ? $"{l.ContainerName}.{l.Leaf}"
                    : l.Path;

                return new SweptSignal(l.Path, l.ContainerName, kind, Classify(l.Path, dispositionKey, claimed, disposed));
            })
            .ToList();

        var byContainer = signals
            .GroupBy(s => (s.Container, s.Kind))
            .OrderBy(g => g.Key.Kind)
            .ThenBy(g => g.Key.Container, StringComparer.Ordinal)
            .Select(g => new ContainerBreakdown(g.Key.Container, g.Key.Kind, g.Count(),
                g.Count(s => s.Accounting == Accounting.ClaimedBySpec),
                g.Count(s => s.Accounting == Accounting.Disposed),
                g.Count(s => s.Accounting == Accounting.Unaccounted)))
            .ToList();

        warnings.AddRange(inventory.Warnings);
        return new SignalSweepReport(projectDir, inventory.FilesScanned, signals, byContainer, tableRead, warnings);
    }

    // `path` is what a spec writes (bare for a tag, qualified for a DB member); `dispositionKey` is what
    // a disposition table's heading qualifies it to. They differ only for a tag-table tag.
    private static Accounting Classify(string path, string dispositionKey, HashSet<string> claimed, HashSet<string> disposed)
    {
        if (claimed.Contains(path) || claimed.Contains(dispositionKey))
        {
            return Accounting.ClaimedBySpec;
        }

        return disposed.Contains(path) || disposed.Contains(dispositionKey)
            ? Accounting.Disposed
            : Accounting.Unaccounted;
    }

    // "Mentioned in a spec" is the honest containment test — deliberately coarse. A spec that NAMES a tag
    // without binding it is a different problem, and one this check must not pretend to detect.
    private static HashSet<string> CollectClaimed(string specsDir, string? registerPath, List<string> warnings)
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var files = Directory.EnumerateFiles(specsDir, "*.md", SearchOption.TopDirectoryOnly).ToList();

        if (files.Count == 0)
        {
            throw new SignalSweepFormatException($"sweep: no .md specs under '{specsDir}'.");
        }

        if (registerPath is not null)
        {
            files.Add(registerPath);
        }

        foreach (var token in files.SelectMany(f => RelationArtifactParsers.EvidenceTokens(File.ReadAllText(f))))
        {
            claimed.Add(token);
        }

        if (claimed.Count == 0)
        {
            warnings.Add($"sweep: parsed 0 backticked identifiers from {files.Count} spec file(s) — every signal will read as unaccounted. Verify the spec format.");
        }

        return claimed;
    }

    // The per-DB disposition tables are the only machine-readable part of the residual artifact; the
    // `### Q-Cnn` narrative findings stay narrative and are deliberately not parsed.
    // Returns the qualified disposition keys plus every heading that actually carried parsed rows — the
    // latter so a heading naming no real container can be reported instead of silently qualifying
    // nothing.
    private static (HashSet<string>, List<string>) CollectDisposed(string unclaimedPath)
    {
        var disposed = new HashSet<string>(StringComparer.Ordinal);
        var headingsWithRows = new List<string>();
        string? container = null;

        foreach (var line in File.ReadLines(unclaimedPath))
        {
            var heading = ContainerHeading.Match(line);
            if (heading.Success)
            {
                container = heading.Groups[1].Value.Trim();
                continue;
            }

            if (container is null || !TableRow.IsMatch(line))
            {
                continue;
            }

            // Only the members cell (the first) names signals; the disposition cell names instances and
            // question ids, which are not signals.
            var cells = line.Trim('|').Split('|');
            foreach (var token in RelationArtifactParsers.EvidenceTokens(cells[0]))
            {
                if (!headingsWithRows.Contains(container, StringComparer.Ordinal))
                {
                    headingsWithRows.Add(container);
                }

                // Bare leaf under a container heading -> qualify. An already-qualified token is taken
                // as-is. A tag-table tag is bare by nature, so the heading is the ONLY qualifier it can
                // ever get — which is why the sweep side qualifies the tag to match (FI-45 item 2).
                disposed.Add(token.Contains('.') ? token : $"{container}.{token}");

                // `Test : Array[0..75]` style entries name an array root whose leaves the inventory
                // expands individually; record the root so its leaves match too.
                disposed.Add($"{container}.{token.Split(' ')[0]}");
            }
        }

        return (disposed, headingsWithRows);
    }
}
