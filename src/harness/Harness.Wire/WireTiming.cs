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
    /// Worst of the measured p99 band. <b>Every budget, every cap, every width, every timeout uses this
    /// one.</b> [D, §12a]
    /// </summary>
    public const int RttP99Ms = 173;

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
    /// </summary>
    public static int BackstopMs(int declaredScans, int expectedRoundTrips)
    {
        if (declaredScans < 0)
            throw new ArgumentOutOfRangeException(nameof(declaredScans), declaredScans, "a duration in scans cannot be negative.");
        if (expectedRoundTrips < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedRoundTrips), expectedRoundTrips, "a round-trip count cannot be negative.");

        return (int)Math.Ceiling(declaredScans * ScanPeriodMs)
             + (expectedRoundTrips * RttP99Ms)
             + RttMaxObservedMs;
    }
}
