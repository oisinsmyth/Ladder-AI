using Converter.CrossCheck;
using Converter.Trace;

namespace Converter.RelationReconcile;

// FI-39 checks 2 + 3 in one subcommand: reconcile the relation-id sets across the spec artifacts, and
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

        // FI-45 item 3: partition the ledger by disposition. Five of the seven documented dispositions
        // mean "this relation does NOT become a D3 term", so requiring the render leg to carry an
        // identical key set made any such row a failure and `render-BLOCKED` undeclarable.
        var (renderBound, dispositionGroups) = PartitionByDisposition(ledgerRows, warnings);

        var differences = new List<PairDifference>();
        foreach (var from in present)
        {
            foreach (var to in present.Where(l => l.Kind != from.Kind))
            {
                // Into the render, only the render-bound subset is owed. Out of the render, everything
                // still is: a term tagged with a relation no artifact declares remains a finding.
                var fromKeys = from.Keys.Distinct();
                if (to.Kind == LegKind.Render)
                {
                    fromKeys = fromKeys.Where(renderBound.Contains);
                }

                var missing = fromKeys.Except(to.Keys).OrderBy(k => k.ToString(), StringComparer.Ordinal).ToList();
                differences.Add(new PairDifference(from.Kind, to.Kind, missing));
            }
        }

        // FI-44 item 3, and it is computed on the RAW keys deliberately — before the partition above
        // narrows anything. A leg whose keys intersect no other leg's was compared against nothing, and
        // the partition must not become a second way for that to pass quietly.
        var uncompared = FindUncomparedLegs(present);

        var (citations, filesScanned) = projectDir is null
            ? (Array.Empty<CitationRow>() as IReadOnlyList<CitationRow>, 0)
            : CheckCitations(ledgerRows, projectDir);

        if (projectDir is null)
        {
            warnings.Add("citations: skipped — pass --project <ir-dir> to check that `verified-cross-block` preconditions cite something written.");
        }

        return new RelationReconcileReport(
            new[] { specs, ledger, register, render }, differences, citations, projectDir, filesScanned, warnings,
            dispositionGroups, uncompared);
    }

    // The ledger is the only artifact that states a disposition, so it alone decides which relations are
    // owed a D3 term. A relation is render-bound if ANY of its rows is: a row that renders is not
    // cancelled by a sibling row that doesn't.
    private static (HashSet<RelationKey> RenderBound, IReadOnlyList<DispositionGroup> Groups) PartitionByDisposition(
        IReadOnlyList<(RelationKey Key, string Disposition, string Evidence, string Precondition)> rows,
        List<string> warnings)
    {
        var renderBound = new HashSet<RelationKey>();
        var groups = new Dictionary<string, (DispositionClass Class, int Count)>(StringComparer.Ordinal);
        var unrecognized = new List<string>();

        foreach (var row in rows)
        {
            var verdict = RelationDispositions.Classify(row.Disposition);
            if (verdict.IsRenderBound)
            {
                renderBound.Add(row.Key);
            }

            var count = groups.TryGetValue(verdict.Label, out var existing) ? existing.Count : 0;
            groups[verdict.Label] = (verdict.Class, count + 1);

            if (verdict.Class == DispositionClass.Unrecognized && !unrecognized.Contains(verdict.Label))
            {
                unrecognized.Add(verdict.Label);
            }
        }

        if (unrecognized.Count > 0)
        {
            // Fail closed and say so: the row still owes a D3 term, and the vocabulary gap is visible
            // rather than absorbed. Inventing a disposition is a documented failure mode of this rung.
            warnings.Add(
                $"ledger: {unrecognized.Count} disposition(s) outside the documented vocabulary — " +
                string.Join(", ", unrecognized.Take(5).Select(u => $"'{u}'")) +
                (unrecognized.Count > 5 ? ", …" : string.Empty) +
                ". Treated as RENDER-BOUND (fail closed). Use the D2 vocabulary in gen-code-structure, " +
                "or raise the gap as a friction report.");
        }

        var ordered = groups
            .Select(g => new DispositionGroup(g.Key, g.Value.Class, g.Value.Count))
            .OrderBy(g => g.Class)
            .ThenByDescending(g => g.Count)
            .ThenBy(g => g.Disposition, StringComparer.Ordinal)
            .ToList();

        return (renderBound, ordered);
    }

    // A present leg sharing no key at all with any other present leg. Absence is a parse fact (zero
    // keys, reported ABSENT, never gates); this is a comparison fact — the leg is there, it parsed, and
    // it met nothing it could agree or disagree with.
    private static IReadOnlyList<UncomparedLeg> FindUncomparedLegs(IReadOnlyList<Leg> present)
    {
        var uncompared = new List<UncomparedLeg>();
        foreach (var leg in present)
        {
            var keys = leg.Keys.Distinct().ToList();
            if (keys.Count == 0)
            {
                continue;
            }

            var others = present.Where(l => l.Kind != leg.Kind).SelectMany(l => l.Keys).ToHashSet();
            if (others.Count == 0 || keys.Any(others.Contains))
            {
                continue;
            }

            uncompared.Add(new UncomparedLeg(leg.Kind, keys.Count,
                keys.OrderBy(k => k.ToString(), StringComparer.Ordinal).Take(5).ToList()));
        }

        return uncompared;
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
