using System.Text.Json;
using Harness.Device;

namespace Harness.Batch;

/// <summary>Whether the two derivations of reachability agree — and if not, exactly how.</summary>
/// <param name="Outcome">See <see cref="ParityOutcome"/>. Only <c>Disagreed</c> gates.</param>
/// <param name="Detail">What was compared, or why it could not be.</param>
public sealed record ReachabilityParityResult(ParityOutcome Outcome, string Detail);

public enum ParityOutcome
{
    /// <summary>Both derivations were run and answered the same. The only reassuring outcome.</summary>
    Agreed = 1,

    /// <summary>🔴 They answered differently. One of them is wrong and nothing here can say which.</summary>
    Disagreed = 2,

    /// <summary>
    /// The second derivation could not be run or could not be read. <b>Reported, never a refusal</b> — the
    /// converter is not always on the path, and a batch that stopped because it could not double-check
    /// would be a check refusing ordinary work, which is how checks get switched off.
    /// </summary>
    NotCompared = 3,
}

/// <summary>
/// 🔴 <b>THE PARITY CHECK FOR A RULE THAT IS NECESSARILY DERIVED TWICE.</b>
///
/// <para>CLAUDE.md: <i>"when you write a SECOND derivation of an existing rule, the pinning test belongs
/// in the same commit as the copy"</i> — <c>GateParityTests</c> exists because two derivations of the gate
/// once disagreed on twelve inputs, and a batch planner then re-derived slot width and was short by the
/// latch registers. <c>Harness.Batch.Reachability</c> is a second derivation of the converter's
/// <c>ProjectUsageGraph.ReachableFromAnOb</c>, and it had no such test.</para>
///
/// <para><b>Why the usual remedy — delete one copy — is unavailable here.</b>
/// <c>src/harness/Directory.Build.props</c> keeps the harness dependency-free ON PURPOSE: no
/// Siemens.Engineering, no Portal, and changing the harness never triggers the TIA
/// <c>(Path, FileHash)</c> re-approval cycle. Referencing the converter would give away a property this
/// project deliberately paid for. So the two derivations must both exist, and the strongest available
/// answer is not one implementation but a RUNTIME comparison over the corpus actually in front of them —
/// which is a better test than any fixture, because it runs on real input every time a batch is planned.</para>
///
/// <para><b>A disagreement is a refusal.</b> Not a warning: the whole point of two derivations is that
/// when they differ, at least one is wrong, and neither of them knows which. Continuing would mean
/// picking a winner by accident of which code path the caller happened to be on.</para>
/// </summary>
public static class ReachabilityParity
{
    public static ReachabilityParityResult Check(
        ReachabilityReport mine, string converterExe, string unionIrDirectory,
        IProcessRunner runner, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(mine);
        ArgumentNullException.ThrowIfNull(runner);

        var result = runner.Run(
            converterExe,
            new[] { "cross-check", "--project", unionIrDirectory, "--json" },
            timeout ?? TimeSpan.FromMinutes(2));

        if (!result.Started)
            return new ReachabilityParityResult(ParityOutcome.NotCompared, "the converter did not start: " + result.Detail);

        if (result.TimedOut)
            return new ReachabilityParityResult(ParityOutcome.NotCompared, "the converter timed out.");

        return CheckAgainst(mine, result.StandardOutput);
    }

    /// <summary>
    /// The same comparison against a report somebody has ALREADY fetched. <see cref="UnionPreflight"/>
    /// runs <c>cross-check</c> over the same union for its own reasons, so the two share one subprocess
    /// rather than each paying for an identical walk of the same corpus.
    /// </summary>
    public static ReachabilityParityResult CheckAgainst(ReachabilityReport mine, string crossCheckJson)
    {
        ArgumentNullException.ThrowIfNull(mine);

        JsonElement reachability;
        try
        {
            using var doc = JsonDocument.Parse(crossCheckJson);
            if (!doc.RootElement.TryGetProperty("reachability", out var node) || node.ValueKind == JsonValueKind.Null)
            {
                return new ReachabilityParityResult(ParityOutcome.NotCompared,
                    "this converter build emits no `reachability` section, so there is only one derivation to consult. "
                    + "The text-walk verdict stands alone and is the weaker of the two.");
            }

            reachability = node.Clone();
        }
        catch (JsonException)
        {
            return new ReachabilityParityResult(ParityOutcome.NotCompared,
                "the converter's output was not readable JSON, so the second derivation could not be consulted.");
        }

        var theirsKnown = reachability.TryGetProperty("known", out var k) && k.GetBoolean();

        // 🔴 COMPARED FIRST, because everything below is meaningless if the two disagree about whether the
        // question is answerable at all. "No OB, so UNKNOWN" and "checked, all reachable" produce the same
        // empty unreachable list and mean opposite things.
        if (theirsKnown != mine.Verified)
        {
            return new ReachabilityParityResult(ParityOutcome.Disagreed,
                $"the two derivations disagree about whether reachability is DECIDABLE at all: the text walk says "
                + $"{(mine.Verified ? "decidable" : "UNKNOWN (no OB)")} and the converter says "
                + $"{(theirsKnown ? "decidable" : "UNKNOWN (no OB)")}. That is a disagreement about the ROOTS, so "
                + "one of them is not seeing an OB the other is.");
        }

        if (!theirsKnown)
        {
            return new ReachabilityParityResult(ParityOutcome.Agreed,
                "both derivations agree the corpus has no OB, so reachability is UNKNOWN — not \"all reachable\".");
        }

        var theirs = Names(reachability, "unreachable");
        var ours = mine.Unreachable.ToHashSet(StringComparer.Ordinal);

        var onlyTheirs = theirs.Except(ours, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var onlyOurs = ours.Except(theirs, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        if (onlyTheirs.Length > 0 || onlyOurs.Length > 0)
        {
            return new ReachabilityParityResult(ParityOutcome.Disagreed,
                "the two derivations of reachability DISAGREE, so at least one is wrong and neither knows which. "
                + (onlyTheirs.Length > 0 ? $"The converter alone calls these unreachable: {string.Join(", ", onlyTheirs)}. " : string.Empty)
                + (onlyOurs.Length > 0 ? $"The text walk alone calls these unreachable: {string.Join(", ", onlyOurs)}. " : string.Empty)
                + "The converter parses IR properly and the text walk is a regex, so the converter is the more likely "
                + "to be right — but this refuses rather than choosing, because a rule that resolves its own "
                + "contradictions silently is how the twelve-input gate disagreement survived.");
        }

        var blocks = reachability.TryGetProperty("codeBlocks", out var cb) ? cb.GetArrayLength() : 0;
        return new ReachabilityParityResult(ParityOutcome.Agreed,
            $"both derivations agree over {blocks} code block(s): {ours.Count} unreachable.");
    }

    private static HashSet<string> Names(JsonElement node, string property) =>
        node.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
}
