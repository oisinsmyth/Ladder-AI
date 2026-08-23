using System.Text.Json;
using Harness.Device;

namespace Harness.Batch;

/// <summary>One thing the union check found. <b>A fact, not a verdict</b> — see the class remark.</summary>
public sealed record UnionFinding(string Kind, string Detail);

/// <summary>
/// What the union pre-flight saw.
/// </summary>
/// <param name="Ran">
/// Whether the check happened at all. <b>False is not clean</b> — the report says which of the several
/// nothings it was, and never lets an unperformed check read like a passed one.
/// </param>
/// <param name="Detail">What ran, or why nothing did.</param>
/// <param name="ObjectsExamined">The denominator. Printed on every run, including a clean one.</param>
/// <param name="Findings">Facts, in the order the tools reported them.</param>
/// <param name="CrossCheckJson">
/// The raw report, kept so the reachability parity check can reuse it rather than paying for a second
/// identical subprocess over the same corpus.
/// </param>
public sealed record UnionPreflightResult(
    bool Ran,
    string Detail,
    int ObjectsExamined,
    IReadOnlyList<UnionFinding> Findings,
    string? CrossCheckJson)
{
    /// <summary>The line that prints whether or not anything was found. <b>Never omitted.</b></summary>
    public string Summary => Ran
        ? $"union pre-flight: {ObjectsExamined} object(s) in the union, {Findings.Count} finding(s), 0 gating"
        : $"UNION PRE-FLIGHT NOT PERFORMED — {Detail}";
}

/// <summary>
/// 🔴 <b>THE UNION CHECK, ACTUALLY RUN, AND ACTUALLY OVER THE UNION.</b>
///
/// <para>Two defects, in one place, both found by reading <c>BatchCli</c> rather than any document.</para>
///
/// <para><b>1. It was PRINTED, not run.</b> <c>harness-batch plan</c> emitted the commands and asked the
/// operator to type them. A check somebody must remember to run is the shape hard rule 3 was in before
/// <c>preflight</c> — FI-36/FI-39 exist for exactly this, <i>"the mechanical floor: checks that survive an
/// agent choosing not to look."</i></para>
///
/// <para>🔴 <b>2. It was not a union check.</b> It looped over the lanes' paths and printed ONE
/// <c>cross-check</c> PER PATH — N per-directory checks, which under FI-65's reading (b) is precisely the
/// <i>filter</i> and not the gate. <b>The sentence printed directly above it made the union argument
/// correctly and the command then contradicted it:</b> <i>"two blocks that each compiled clean in
/// isolation can still conflict, and only a union check sees it."</i></para>
///
/// <para><b>That is a closed check, and a clean example of the class.</b> It examines something real —
/// each lane's own directory — so it is never empty and never silent. <i>What could it not possibly
/// see?</i> <b>A conflict that exists only BETWEEN two lanes</b> — the only kind a union check exists to
/// find, and the one its own comment names as the reason it exists.</para>
///
/// <para>⚠️ <b>REPORT-ONLY, DELIBERATELY, AND THIS IS NOT TIMIDITY.</b> <c>cross-check</c> emits
/// <b>facts, not verdicts</b>, and exits 0 by design; deciding which facts should gate is a judgement
/// that needs the measurement (X1) behind it. A refusal set chosen wide gets the gate switched off — and
/// a switched-off gate still appears in the list, which is worse than no gate. The refusal set is a
/// separate, later, evidence-led act.</para>
/// </summary>
public static class UnionPreflight
{
    /// <summary>
    /// Runs <c>cross-check</c> over the whole union and reports its facts. The JSON comes back on the
    /// result so <see cref="ReachabilityParity"/> can reuse it — one subprocess, two consumers.
    /// </summary>
    public static UnionPreflightResult Run(
        string converterExe, string unionIrDirectory, IProcessRunner runner, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(runner);

        var result = runner.Run(
            converterExe,
            new[] { "cross-check", "--project", unionIrDirectory, "--json" },
            timeout ?? TimeSpan.FromMinutes(2));

        if (!result.Started)
            return NotRun("the converter did not start: " + result.Detail);

        if (result.TimedOut)
            return NotRun("the converter timed out, so nothing across the lanes was compared.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(result.StandardOutput);
        }
        catch (JsonException)
        {
            return NotRun("the converter's output was not readable JSON, so nothing across the lanes was compared.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var findings = new List<UnionFinding>();

            // 🔴 THE CROSS-LANE ONES FIRST, because they are the only kind this check exists for. A
            // multi-writer within one lane would have been found by that lane's own preflight.
            Collect(root, "multiWriters", "MULTI-WRITER", findings);
            Collect(root, "deadMembers", "DEAD-MEMBER", findings);
            Collect(root, "ioBoundary", "IO-BOUNDARY", findings);

            var objects = root.TryGetProperty("reachability", out var reach)
                          && reach.ValueKind == JsonValueKind.Object
                          && reach.TryGetProperty("codeBlocks", out var blocks)
                ? blocks.GetArrayLength()
                : 0;

            // Zero objects is NOT a clean union — it is a union that contains nothing, which is the
            // absence FI-44 exists for. Said as such rather than reported as "0 findings".
            return objects == 0
                ? NotRun("the union contains NO code blocks, so there was nothing to compare across lanes. "
                       + "NOTHING EXAMINED is not the same as nothing found.")
                : new UnionPreflightResult(true, "cross-check ran over the whole union.", objects, findings,
                    result.StandardOutput);
        }
    }

    private static void Collect(JsonElement root, string property, string kind, List<UnionFinding> into)
    {
        if (!root.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in array.EnumerateArray())
        {
            var path = item.TryGetProperty("path", out var p) ? p.GetString() : null;
            var block = item.TryGetProperty("block", out var b) ? b.GetString() : null;
            into.Add(new UnionFinding(kind, path ?? block ?? item.ToString()));
        }
    }

    private static UnionPreflightResult NotRun(string why) =>
        new(false, why, 0, Array.Empty<UnionFinding>(), null);
}
