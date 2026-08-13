namespace Harness.Wire;

/// <summary>
/// The timing constants this client budgets against.
///
/// <para><b>NOTHING IS CHOSEN HERE.</b> Every value below is transcribed from the specification's
/// §12a, which is the single place a timing constant is picked and the only place one may be revised.
/// A figure carrying neither an <c>[M]</c> (measured on the rig) nor a <c>[D]</c> (derived there) is
/// stale and predates 2026-08-13. If one of these looks wrong, the fix is in §12a, not here.</para>
///
/// <para><b>Why the p99 and not the median.</b> A wave issues round trips in the hundreds, so "one in
/// a hundred" is not a rare event within a single wave — it is several per wave. A budget keyed on the
/// median is a budget that is wrong several times per wave, and its failure mode is a MISSED ASSERTION
/// REPORTED AS A PASS.</para>
/// </summary>
public static class WireTiming
{
    /// <summary>Worst of the measured median band. <b>Expected durations only</b> — never a budget. [D, §12a]</summary>
    public const int RttTypicalMs = 78;

    /// <summary>
    /// <b>Every budget, every cap, every width uses this one.</b> [M, §12a]
    ///
    /// <para><b>Raised 173 → 201 ms on 2026-08-13.</b> The 173 was derived from a 1..16-register sweep
    /// and applied to full-width slots; re-measured at 123 registers over four runs the per-run p99s
    /// were 157 / 201 / 168 / 185, two of them above 173. This is the WORST OBSERVED run p99, not the
    /// weighted central estimate of 183 — a budget keyed on an uncertain tail must fail toward the
    /// pessimistic side, because setting it too high costs a few slots of width and setting it too low
    /// costs a MISSED ASSERTION REPORTED AS A PASS.</para>
    ///
    /// <para><b>It is the weakest constant here and §12a says so:</b> a 1-in-100 statistic from four
    /// runs, sampling-limited. No figure derived from it should be quoted to three significant figures,
    /// and what would firm it up is more SESSIONS, not more widths.</para>
    ///
    /// <para><b>The width provenance was withdrawn.</b> A within-width p99 ranged 136→562 ms in one
    /// session — an order of magnitude more than the 173→201 move — so "width widens the tail" is NOT
    /// established and nothing here caps slot width on timing grounds.</para>
    /// </summary>
    public const int RttP99Ms = 201;

    /// <summary>
    /// Marginal cost of one register, measured at 123 on writes; no trend at all on reads. [M, §12a]
    ///
    /// <para><b>It is negligible and it is NOT zero</b>, and the difference is the whole finding of
    /// 2026-08-13. A full-width 123-register write costs ~4.9 ms more than a one-register write, ~6% of
    /// a round trip — against 100% for a second round trip. So "round trips cost, registers do not"
    /// survives by a factor of about sixteen, and the bare phrase "marginal cost per register is zero"
    /// may no longer be said. <b>This constant exists to be quoted, never to be budgeted from:</b> no
    /// poll budget in this assembly is derived from slot width, and a test asserts that.</para>
    /// </summary>
    public const double MarginalCostPerRegisterMs = 0.040;

    /// <summary>
    /// The single 2,216 ms sample in 2,000. <b>Timeouts only, never throughput</b> — it must not be added
    /// to <see cref="RttP99Ms"/> and no budget includes it. [M, §12a]
    /// </summary>
    public const int RttMaxObservedMs = 2216;

    /// <summary>Scan period under load — the row to budget from, because the harness always has traffic on it. [D, §12a]</summary>
    public const double ScanPeriodMs = 23.33;

    /// <summary>
    /// The client's per-request timeout: <c>RTT_max x ~1.35</c>. [D, §12a derivation 4]
    ///
    /// <para>A per-request timeout under 2,216 ms converts a measured, ordinary tail event into a
    /// transport failure — at ~1 in 2,000 requests, which is ~1 in 22 waves, arriving as an intermittent
    /// unattributable red. There is no throughput cost to a generous timeout: it only ever elapses on a
    /// request that has already failed.</para>
    /// </summary>
    public const int PerRequestTimeoutMs = 3000;

    /// <summary>
    /// X-B's wall-clock backstop, computed rather than guessed. [D, §12a derivation 4]
    ///
    /// <para><c>(declared scans x scan period) + (expected round trips x RTT_p99) + one RTT_max
    /// allowance.</c> <b>Without the last term a healthy test reports TIMED-OUT roughly every 22nd
    /// wave</b> — and a spurious TIMED-OUT is worse than a spurious FAILED, because it is believed.</para>
    ///
    /// <para><b>Every term is BOUND-shaped and none of them may move to the p90 (§12a's F-5 kind rule).</b>
    /// The middle term keeps <see cref="RttP99Ms"/>: substituting <c>RTT_p90</c> would halve the allowance
    /// while 10% of round trips exceed that figure BY CONSTRUCTION, so on a test issuing 19 round trips
    /// roughly two would be expected to blow the per-trip assumption every run. The last term keeps
    /// <see cref="RttMaxObservedMs"/> and <b>is not padding</b>.</para>
    ///
    /// <para><b>The scan term takes a <see cref="ScanBudget"/> and the run's
    /// <see cref="RuntimeCompression"/>, never a bare count</b> — the declaration is in scans AT THE
    /// AUTHOR'S <c>comp</c>, and the same behaviour occupies a different number of scans at the factor the
    /// wave actually runs. A duration declared at <c>comp = 10</c> and consumed as though it were
    /// <c>comp = 1</c> is ten times too short, and the symptom is a TIMED-OUT on a healthy test. There is
    /// deliberately no overload that takes an <c>int</c>: the hole is closed by being unexpressible rather
    /// than by a check that a later edit could drop.</para>
    /// </summary>
    public static int BackstopMs(ScanBudget declared, RuntimeCompression runtime, int expectedRoundTrips)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(runtime);

        if (expectedRoundTrips < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedRoundTrips), expectedRoundTrips, "a round-trip count cannot be negative.");

        return (int)Math.Ceiling(declared.BoundAt(runtime) * ScanPeriodMs)
             + (expectedRoundTrips * RttP99Ms)
             + RttMaxObservedMs;
    }

    /// <summary>
    /// Round trips one wave index costs:
    /// <c>K x ceil(Wv / 123) + P x ceil(K / R) + 1</c> — K slots, <c>Wv</c> vector registers per slot,
    /// P poll rounds, <c>R = floor(125 / Wr)</c> whole slots per read, plus the one-transaction commit.
    /// [D, §12a derivation 2]
    ///
    /// <para><b>The read term was <c>P x K</c> until F-1 was adopted on 2026-08-13</b> — one FC03 per
    /// slot. It is now <c>P x ceil(K/R)</c>, and since <c>R = floor(125 / Wr)</c>, <b>slot width has
    /// entered the round-trip count for the first time, in the denominator</b>. Registers still do not
    /// cost on the wire (~0.040 ms each); what they now do is decide how many slots share a read. Width
    /// is free per-register and expensive per-R-step.</para>
    ///
    /// <para><b>The WRITE term is untouched, and that is deliberate.</b> F-1 is a read-side change.
    /// Vector data is still written across as many transactions as it takes while nothing is running,
    /// and the commit is still ONE transaction — that is where A1's coherence guarantee lives, and
    /// nothing here is an invitation to batch writes differently.</para>
    /// </summary>
    public static int RoundTripsPerIndex(int slots, int vectorRegistersPerSlot, int resultRegistersPerSlot, int pollRounds)
    {
        if (slots < 0 || vectorRegistersPerSlot < 0 || pollRounds < 0)
            throw new ArgumentOutOfRangeException(nameof(slots), "a count cannot be negative.");

        if (resultRegistersPerSlot < 1)
            throw new ArgumentOutOfRangeException(nameof(resultRegistersPerSlot), resultRegistersPerSlot, "a slot that publishes nothing cannot be judged, so it is not a slot the map allocates.");

        var writes = vectorRegistersPerSlot == 0
            ? 0
            : (vectorRegistersPerSlot + Harness.Map.ModbusLimits.MaxWriteRegisters - 1) / Harness.Map.ModbusLimits.MaxWriteRegisters;

        var slotsPerRead = Math.Max(1, Harness.Map.ModbusLimits.MaxReadRegisters / resultRegistersPerSlot);
        var reads = pollRounds * ((slots + slotsPerRead - 1) / slotsPerRead);

        return (slots * writes) + reads + 1;
    }

    /// <summary>
    /// O11's cap: <c>K_max(S) = R x floor(S x scan / RTT_p99)</c>, the widest tensor the poll budget
    /// sustains for a SAMPLED assertion whose true window is <paramref name="sMinScans"/> scans.
    /// [D, §12a derivation 2]
    ///
    /// <para><b>F-1's factor lands here undiluted.</b> Elsewhere it is diluted — writes and the commit
    /// are untouched and <c>ceil(K/R)</c> is a step — but the cap is straight-line proportional to R. At
    /// S = 10 scans a 20-register slot admits SIX slots where a padded 123-register one admits ONE, and
    /// that row is the point of the change: the cap that was binding at 1 was an artefact of the map,
    /// not of the link.</para>
    ///
    /// <para><b>REPORTED, NEVER ENFORCED, and D36 is why.</b> The arithmetic needs no rig, but its free
    /// variable <c>S_min</c> — whether real submission sets declare sampled windows at all, and how
    /// short — has never been observed. A formula whose free variable has never been measured is a
    /// prediction, which is the exact thing that deferral exists to prevent. <b>Latched assertions have
    /// no <c>S</c> at all and this cap does not bind on them</b>, which is most of why F-3 argues for
    /// making latching mandatory.</para>
    /// </summary>
    public static int MaxTensorWidth(int slotsPerRead, int sMinScans)
    {
        if (slotsPerRead < 1)
            throw new ArgumentOutOfRangeException(nameof(slotsPerRead), slotsPerRead, "a read covers at least one whole slot.");

        if (sMinScans < 1)
            throw new ArgumentOutOfRangeException(nameof(sMinScans), sMinScans, "an observation window of less than one scan is below the floor at any rate; there is no width that observes it.");

        return slotsPerRead * (int)Math.Floor(sMinScans * ScanPeriodMs / RttP99Ms);
    }

    /// <summary>Observability floor in scans at the p99 for a K-slot tensor: a slot is polled every <c>K x RTT</c>. [D, §12a derivation 1]</summary>
    /// <remarks>
    /// 8.6 scans at K=1 (was 7.4 at <c>RTT_p99</c> = 173), so a SAMPLED level must persist for 9 scans to
    /// be caught — and for <c>9 x K</c> in a K-slot tensor. A latched observation has no such window.
    /// </remarks>
    public static double ObservabilityFloorScans(int slots)
    {
        if (slots < 1)
            throw new ArgumentOutOfRangeException(nameof(slots), slots, "a tensor of no slots is not polled at all.");

        return slots * RttP99Ms / ScanPeriodMs;
    }
}
