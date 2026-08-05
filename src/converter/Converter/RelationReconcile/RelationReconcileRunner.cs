using Converter.CrossCheck;
using Converter.Trace;

namespace Converter.RelationReconcile;

// FI-35 checks 2 + 3 in one subcommand: reconcile the relation-id sets across the spec artifacts, and
// check that each `verified-cross-block` precondition actually cites something written.
//
// Check 2 buys a regression guard, not a catch: the fixture's three legs already reconcile. What it
// converts is three HAND-ASSERTED counts inside the artifact into computed ones — the "computed rather
// than asserted" principle this tooling exists for.
//
// Check 3 closes a demonstrated loophole: `verified-cross-block` specified citation FORM, not probative
// force, so a citation pointing at a bare declaration satisfied the rule while proving nothing.
public static class RelationReconcileRunner
{
    public static RelationReconcileReport Run(
        string specsDir,
        string ledgerPath,
        string registerPath,
        string? projectDir)
    {
        var warnings = new List<string>();

        var specs = RelationArtifactParsers.ParseSpecs(specsDir);
        var (ledger, ledgerRows) = RelationArtifactParsers.ParseLedger(ledgerPath);
        var register = RelationArtifactParsers.ParseRegister(registerPath);
        var render = RelationArtifactParsers.ParseRender(ledgerPath);

        if (!render.Present)
        {
            warnings.Add(RelationArtifactParsers.LooksStopped(ledgerPath)
                ? "render: ABSENT — the ledger records a stopped D3. A stopped rung is a legitimate state, not a drift."
                : "render: ABSENT — no D3 term tags found, and no STOPPED marker either. Verify this is intended.");
        }

        var present = new[] { specs, ledger, register, render }.Where(l => l.Present).ToList();
        var differences = new List<PairDifference>();
        foreach (var from in present)
        {
            foreach (var to in present.Where(l => l.Kind != from.Kind))
            {
                var missing = from.Keys.Distinct().Except(to.Keys).OrderBy(k => k.ToString(), StringComparer.Ordinal).ToList();
                differences.Add(new PairDifference(from.Kind, to.Kind, missing));
            }
        }

        var (citations, filesScanned) = projectDir is null
            ? (Array.Empty<CitationRow>() as IReadOnlyList<CitationRow>, 0)
            : CheckCitations(ledgerRows, projectDir);

        if (projectDir is null)
        {
            warnings.Add("citations: skipped — pass --project <ir-dir> to check that `verified-cross-block` preconditions cite something written.");
        }

        return new RelationReconcileReport(
            new[] { specs, ledger, register, render }, differences, citations, projectDir, filesScanned, warnings);
    }

    private static (IReadOnlyList<CitationRow>, int) CheckCitations(
        IReadOnlyList<(RelationKey Key, string Disposition, string Evidence, string Precondition)> rows,
        string projectDir)
    {
        var graph = ProjectUsageGraph.Build(projectDir);
        var inventory = SignalInventory.SignalInventory.Build(projectDir);
        var known = inventory.Leaves.Select(l => l.Path).ToHashSet(StringComparer.Ordinal);

        var results = new List<CitationRow>();
        foreach (var row in rows.Where(r =>
                     r.Precondition.Contains("verified-cross-block", StringComparison.OrdinalIgnoreCase)))
        {
            var tokens = RelationArtifactParsers.EvidenceTokens(row.Evidence)
                .Select(t => Classify(t, graph, known))
                .ToList();

            results.Add(new CitationRow(row.Key, row.Evidence, tokens));
        }

        return (results, inventory.FilesScanned);
    }

    private static CitationToken Classify(string token, ProjectUsageGraph graph, HashSet<string> known)
    {
        if (graph.Usages.TryGetValue(token, out var usage) && usage.Writers.Count > 0)
        {
            // Built-but-switched-off is a distinct fact from unwritten, and the difference matters to a
            // precondition that claims something is established elsewhere.
            var allDisarmed = usage.Writers.All(w => DisarmAnalysis.IsProvablyFalse(w.Guard));
            return new CitationToken(token,
                allDisarmed ? TokenResolution.DisarmedWriters : TokenResolution.WrittenMember,
                usage.Writers.Count);
        }

        return known.Contains(token)
            ? new CitationToken(token, TokenResolution.DeclarationOnly, 0)
            : new CitationToken(token, TokenResolution.Unresolved, 0);
    }
}
