using System.Text;
using System.Text.Json;

namespace Converter.ReachableState;

/// <summary>
/// One record set, two renderers — the house style.
///
/// <para>The JSON half is what a driver feeds into <c>new TestSlot(id, agent, reachableState,
/// reachableStateProvenance, …)</c>. Its central property is the one <c>ConflictGraphOutputFormatter</c>
/// exists for too: *** A CLOSURE NOBODY COMPUTED OMITS ITS KEY; IT IS NEVER WRITTEN AS `[]`. ***</para>
/// </summary>
public static class ReachableStateOutputFormatter
{
    public static string FormatJson(ReachableStateReport report)
    {
        // *** THE ONE DECISION THIS FILE EXISTS FOR, AT BOTH LEVELS. ***
        //
        // `reachableState: []` is read downstream as the positive claim "computed, and it reaches
        // nothing" — a slot carrying it is admitted as independent. A closure that could NOT be
        // computed must therefore omit the key, and NOT write null either: a lenient deserializer
        // rounds null to the empty list and restores the false claim one layer down.
        //
        // `provenance` is omitted with it. That is the load-bearing half: WaveSetAdmission raises
        // ColouringDefect.ReachableStateNotComputed on an EMPTY PROVENANCE, so a block emitted
        // without one is refused per-slot rather than treated as independent of everything.
        object payload = report.Computed
            ? new
            {
                blocks = report.Blocks.Select(BlockPayload),
                derivation = Derivation(report),
            }
            : new
            {
                notComputed = report.NotComputedReason,
                derivation = Derivation(report),
            };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object BlockPayload(BlockReachableState block) => block.Computed
        ? new
        {
            block = block.Block,
            reachableState = block.ReachableState,
            provenance = block.Provenance,
            derivation = new
            {
                callClosure = block.CallClosure,
                aliasCanonicalisations = block.AliasCanonicalisations,
            },
        }
        : (object)new
        {
            block = block.Block,
            notComputed = block.NotComputedReason,
            derivation = new
            {
                callClosure = block.CallClosure,
                unresolvedCalls = block.UnresolvedCalls,
            },
        };

    // Ours, and it carries what a reader needs to CHECK the closure instead of trusting it: how many
    // blocks it walked, and every alias rewrite it applied. *A narrowing nobody can see becomes a
    // place to hide* — the alias rewrite is exactly what makes a caller's `iDB_X.IO.Step` intersect
    // the FB's own `IO.Step`, so its count is printed on every run, including when it is zero.
    private static object Derivation(ReachableStateReport report) => new
    {
        computed = report.Computed,
        corpus = report.CorpusDescription,
        corpusHash = report.CorpusHash,
        blocksExamined = report.Blocks.Count,
        blocksComputed = report.Blocks.Count(b => b.Computed),
        blocksNotComputed = report.NotComputedBlocks.Count,
        aliasRewrites = report.Blocks.Sum(b => b.AliasCanonicalisations.Count),
        warnings = report.Warnings,
    };

    public static string FormatText(ReachableStateReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("REACHABLE STATE (D9) — the transitive closure, through each block's CALL tree,");
        sb.AppendLine("of every storage location it touches. COMPUTED from the IR, never declared.");
        sb.AppendLine();
        sb.AppendLine($"CORPUS: {report.CorpusDescription}");
        sb.AppendLine($"HASH:   {report.CorpusHash}");

        foreach (var warning in report.Warnings)
        {
            sb.AppendLine($"  WARNING: {warning}");
        }

        if (!report.Computed)
        {
            sb.AppendLine();
            sb.AppendLine("NOT COMPUTED — no closure is emitted and no slot may be built from this run.");
            sb.AppendLine($"  {report.NotComputedReason}");
            return sb.ToString();
        }

        sb.AppendLine($"PROVENANCE: {report.Provenance}");
        sb.AppendLine();

        foreach (var block in report.Blocks)
        {
            if (!block.Computed)
            {
                sb.AppendLine($"{block.Block}: NOT COMPUTED — the key is WITHHELD, so a slot built from it has no");
                sb.AppendLine("  provenance and admission refuses it rather than treating it as independent.");
                sb.AppendLine($"  {block.NotComputedReason}");
                sb.AppendLine();
                continue;
            }

            sb.AppendLine($"{block.Block}: {block.ReachableState.Count} storage location(s) over " +
                          $"{block.CallClosure.Count} block(s) in the call closure");
            foreach (var path in block.ReachableState)
            {
                sb.AppendLine($"    {path}");
            }

            if (block.CallClosure.Count > 1)
            {
                sb.AppendLine($"  CALL CLOSURE: {string.Join(", ", block.CallClosure)}");
            }

            // Printed even at zero. A rewriter that is silent when it rewrote nothing and one that has
            // stopped working produce identical output.
            sb.AppendLine($"  ALIAS REWRITES: {block.AliasCanonicalisations.Count}");
            foreach (var alias in block.AliasCanonicalisations)
            {
                sb.AppendLine($"    {alias}");
            }

            sb.AppendLine();
        }

        sb.AppendLine($"SUMMARY: {report.Blocks.Count(b => b.Computed)} computed, " +
                      $"{report.NotComputedBlocks.Count} NOT COMPUTED (key withheld).");
        return sb.ToString();
    }
}
