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
    private static readonly Regex DbHeading = new(@"^###\s+`([A-Za-z_][A-Za-z0-9_]*)`", RegexOptions.Compiled);

    private static readonly Regex TableRow = new(@"^\|(.+)\|", RegexOptions.Compiled);

    public static SignalSweepReport Run(string projectDir, string specsDir, string? registerPath, string? unclaimedPath)
    {
        var warnings = new List<string>();
        var inventory = SignalInventory.SignalInventory.Build(projectDir);

        // Denominator: every global-DB leaf and tag-table tag in the corpus. Exact and cheap.
        var swept = inventory.Leaves
            .Where(l => l.Origin is SignalOrigin.GlobalDb or SignalOrigin.TagTable)
            .Select(l => l.Path)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (swept.Count == 0)
        {
            throw new SignalSweepFormatException(
                $"sweep: no global-DB or tag-table signals found under '{projectDir}' — nothing to sweep. " +
                "Refusing to report full coverage of an empty denominator.");
        }

        var claimed = CollectClaimed(specsDir, registerPath, warnings);
        var (disposed, tableRead) = unclaimedPath is null
            ? (new HashSet<string>(StringComparer.Ordinal), false)
            : CollectDisposed(unclaimedPath);

        if (unclaimedPath is not null && !tableRead)
        {
            throw new SignalSweepFormatException(
                $"sweep: no '### `Db`' disposition tables found in '{unclaimedPath}'. " +
                "Refusing to report signals as unaccounted when the disposition leg was not read.");
        }

        var signals = swept
            .Select(p => new SweptSignal(p, p.Split('.')[0], Classify(p, claimed, disposed)))
            .ToList();

        var byDb = signals
            .GroupBy(s => s.Root, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new DbBreakdown(g.Key, g.Count(),
                g.Count(s => s.Accounting == Accounting.ClaimedBySpec),
                g.Count(s => s.Accounting == Accounting.Disposed),
                g.Count(s => s.Accounting == Accounting.Unaccounted)))
            .ToList();

        warnings.AddRange(inventory.Warnings);
        return new SignalSweepReport(projectDir, inventory.FilesScanned, signals, byDb, tableRead, warnings);
    }

    private static Accounting Classify(string path, HashSet<string> claimed, HashSet<string> disposed)
    {
        if (claimed.Contains(path))
        {
            return Accounting.ClaimedBySpec;
        }

        return disposed.Contains(path) ? Accounting.Disposed : Accounting.Unaccounted;
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
    private static (HashSet<string>, bool) CollectDisposed(string unclaimedPath)
    {
        var disposed = new HashSet<string>(StringComparer.Ordinal);
        string? db = null;
        var sawTable = false;

        foreach (var line in File.ReadLines(unclaimedPath))
        {
            var heading = DbHeading.Match(line);
            if (heading.Success)
            {
                db = heading.Groups[1].Value;
                continue;
            }

            if (db is null || !TableRow.IsMatch(line))
            {
                continue;
            }

            // Only the members cell (the first) names signals; the disposition cell names instances and
            // question ids, which are not signals.
            var cells = line.Trim('|').Split('|');
            foreach (var token in RelationArtifactParsers.EvidenceTokens(cells[0]))
            {
                sawTable = true;

                // Bare leaf under a DB heading -> qualify. An already-qualified token is taken as-is.
                disposed.Add(token.Contains('.') ? token : $"{db}.{token}");

                // `Test : Array[0..75]` style entries name an array root whose leaves the inventory
                // expands individually; record the root so its leaves match too.
                disposed.Add($"{db}.{token.Split(' ')[0]}");
            }
        }

        return (disposed, sawTable);
    }
}
