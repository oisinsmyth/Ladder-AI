namespace Harness.Wire;

/// <summary>One slot's solo result set beside its concurrent one, at one wave index.</summary>
public sealed record InterferenceFinding(int SlotIndex, int WaveIndex, ushort[] Solo, ushort[] Concurrent, string Detail);

/// <summary>The differential's verdict.</summary>
/// <param name="Findings">Every place a slot's concurrent result differed from its solo result.</param>
/// <param name="SoloOutcomes">What each slot's solo run produced, so a pair that was broken in BOTH runs is visible.</param>
public sealed record InterferenceReport(
    IReadOnlyList<InterferenceFinding> Findings,
    IReadOnlyDictionary<int, IReadOnlyList<SlotOutcome>> SoloOutcomes,
    IReadOnlyDictionary<int, IReadOnlyList<SlotOutcome>> ConcurrentOutcomes,
    IReadOnlyList<string> Discrepancies,
    int Comparisons)
{
    /// <summary>
    /// True only when something was actually compared AND nothing differed AND the co-running log agreed.
    ///
    /// <para>The comparison count is not decoration. A differential that compared nothing — because a
    /// solo run produced no results to compare against — is not evidence of non-interference; it is the
    /// absence of evidence, and it reads identically in every other field.</para>
    /// </summary>
    public bool Indistinguishable => Comparisons > 0 && Findings.Count == 0 && Discrepancies.Count == 0;

    public string Summary() => Indistinguishable
        ? $"INDISTINGUISHABLE: {Comparisons} result set(s) compared, no divergence."
        : Comparisons == 0
            ? "NOT COMPARED: no solo result was available to compare a concurrent one against. Empty is not clean."
            : $"INTERFERENCE: {Findings.Count} divergence(s) across {Comparisons} comparison(s).";
}

/// <summary>
/// Build-plan item 3.2 — <b>demonstrate non-interference, and demonstrate the DETECTION of it.</b>
///
/// <para><b>The mechanism is a differential, and it is the exit criterion's own phrasing:</b> "two slots
/// produce results indistinguishable from their SOLO runs". Each slot is run alone — every other slot
/// null at every index, which under D26a rule 2 costs no new mechanism at all — and then all slots are
/// run together. A slot whose concurrent results differ from its solo results was affected by something
/// that was not present when it ran alone.</para>
///
/// <para><b>WHAT THIS DOES NOT PROVE, AND THE PLAN NAMES IT AS THE WAY THIS GATE IS USUALLY FAILED.</b>
/// A green from a pair that shares nothing measures nothing about interference: two blocks with disjoint
/// tags cannot interfere however the harness behaves. So a green here is only meaningful alongside a
/// RED from a pair constructed to interfere — and the pair has to interfere <b>only when both slots are
/// active</b>, because every block is CALLED every scan (D37) and a coupling that bites while the other
/// slot is null would break the solo run too, which is a different and much easier fault to see.</para>
///
/// <para><b>The co-running log is folded in</b> because a differential over runs that did not actually
/// happen is worthless: if a slot was commanded and never executed, its "solo" results are the previous
/// state and comparing them proves nothing. Discrepancies gate the verdict rather than being reported
/// beside it.</para>
///
/// <para><b>What it deliberately does not do:</b> it does not decide WHY two slots interfere, does not
/// build a conflict graph, and does not colour one. DB-13's admission is phase 6 and needs the reference
/// graph, not the wire.</para>
/// </summary>
public static class NonInterference
{
    /// <summary>Run each slot solo, then all together, and compare.</summary>
    /// <param name="run">
    /// Runs one wave over the given tensors and returns the result. Injected rather than calling
    /// <see cref="WaveRun"/> directly so the differential can be exercised against a wave that is made to
    /// misbehave — a check that can only be driven by a well-behaved system is a check nobody has seen fail.
    /// </param>
    public static InterferenceReport Compare(IReadOnlyList<SlotTensor> tensors, Func<IReadOnlyList<SlotTensor>, WaveResult> run)
    {
        ArgumentNullException.ThrowIfNull(tensors);
        ArgumentNullException.ThrowIfNull(run);

        if (tensors.Count < 2)
            throw new ArgumentException("a non-interference differential needs at least two slots; with one there is nothing for it to be indistinguishable from.", nameof(tensors));

        var solo = new Dictionary<int, IReadOnlyList<SlotRunResult>>();
        var soloOutcomes = new Dictionary<int, IReadOnlyList<SlotOutcome>>();
        var discrepancies = new List<string>();

        foreach (var tensor in tensors)
        {
            var wave = run(new[] { tensor });
            var results = wave.For(tensor.SlotIndex).Results;

            solo[tensor.SlotIndex] = results;
            soloOutcomes[tensor.SlotIndex] = results.Select(r => r.Outcome).ToArray();
            discrepancies.AddRange(wave.Log.Discrepancies.Select(d => $"solo run of slot {tensor.SlotIndex}: {d}"));
        }

        var concurrent = run(tensors);
        discrepancies.AddRange(concurrent.Log.Discrepancies.Select(d => $"concurrent run: {d}"));

        var findings = new List<InterferenceFinding>();
        var comparisons = 0;

        foreach (var tensor in tensors)
        {
            var soloResults = solo[tensor.SlotIndex];
            var togetherResults = concurrent.For(tensor.SlotIndex).Results;

            for (var index = 0; index < Math.Min(soloResults.Count, togetherResults.Count); index++)
            {
                comparisons++;

                var a = soloResults[index];
                var b = togetherResults[index];

                if (a.Outcome != b.Outcome)
                {
                    findings.Add(new InterferenceFinding(tensor.SlotIndex, index, a.Results, b.Results,
                        $"slot {tensor.SlotIndex} index {index} ended {a.Outcome} alone and {b.Outcome} alongside the rest of the wave set."));
                    continue;
                }

                if (!a.Results.SequenceEqual(b.Results))
                {
                    findings.Add(new InterferenceFinding(tensor.SlotIndex, index, a.Results, b.Results,
                        $"slot {tensor.SlotIndex} index {index} published [{string.Join(", ", a.Results)}] alone and [{string.Join(", ", b.Results)}] alongside the rest of the wave set. The block's own logic did not change between those two runs."));
                }
            }

            if (soloResults.Count != togetherResults.Count)
            {
                findings.Add(new InterferenceFinding(tensor.SlotIndex, -1, Array.Empty<ushort>(), Array.Empty<ushort>(),
                    $"slot {tensor.SlotIndex} produced {soloResults.Count} result(s) alone and {togetherResults.Count} alongside the rest of the wave set."));
            }
        }

        return new InterferenceReport(
            findings,
            soloOutcomes,
            tensors.ToDictionary(t => t.SlotIndex,
                t => (IReadOnlyList<SlotOutcome>)concurrent.For(t.SlotIndex).Results.Select(r => r.Outcome).ToArray()),
            discrepancies,
            comparisons);
    }
}
