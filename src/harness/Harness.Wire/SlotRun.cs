using System.Diagnostics;

namespace Harness.Wire;

/// <summary>
/// One vector, as the wire needs it: what to write, what inert looks like first, how completion is
/// recognised, and how long it may take.
/// </summary>
/// <param name="Values">The vector's register values, in slot-register order.</param>
/// <param name="Inert">The inert phase that must be established and verified BEFORE this vector runs (D33).</param>
/// <param name="CompletionRegister">
/// Result register index the client polls. Phase 2 recognises completion from ONE register reaching ONE
/// value, because that is what the walking skeleton needs and nothing about a richer completion protocol
/// is proven yet.
/// </param>
/// <param name="CompletionValue">The value that register reads when the test is over.</param>
/// <param name="DeclaredScans">
/// The vector's declared maximum duration IN SCANS — the unit the observability floor argues in (X-B).
/// It is an input to the wall-clock backstop, never the backstop itself.
/// </param>
public sealed record WireVector(
    ushort[] Values,
    InertDeclaration Inert,
    int CompletionRegister,
    ushort CompletionValue,
    int DeclaredScans);

/// <summary>How one vector ended. TIMED-OUT is deliberately not FAILED.</summary>
public enum SlotOutcome
{
    /// <summary>The completion register reached its value. The results are the results.</summary>
    Completed,

    /// <summary>Inert could not be established, so the test never started. Not a test failure.</summary>
    NotInert,

    /// <summary>
    /// The backstop elapsed. X-B invented this outcome precisely so "the condition never occurred" would
    /// be distinguishable from a real failure — a spurious TIMED-OUT is worse than a spurious FAILED,
    /// because it is believed.
    /// </summary>
    TimedOut,
}

/// <summary>What one vector produced, and what it cost.</summary>
/// <param name="Results">The slot's result registers, read once completion was recognised.</param>
/// <param name="StartScan">The scan counter at the commit — the test's T=0 (D37).</param>
/// <param name="EndScan">The scan counter at the observation that recognised completion.</param>
/// <param name="Inert">The inert phase's own report, kept separate: outputs are not recorded during inert.</param>
/// <param name="RoundTrips">Round trips the whole sequence cost. The unit that was measured to matter.</param>
public sealed record SlotRunResult(
    SlotOutcome Outcome,
    ushort[] Results,
    long StartScan,
    long EndScan,
    int PollRounds,
    int RoundTrips,
    InertReport Inert,
    string Detail)
{
    /// <summary>Scans from T=0 to the observation that recognised completion.</summary>
    public long ElapsedScans => EndScan - StartScan;
}

/// <summary>
/// Build-plan item 2.4: write vector → raise start bool → poll → read results, with 2.5's inert phase
/// in front of it.
///
/// <para><b>The shape is §1.3's loop for one slot at one index:</b> establish inert for THIS index,
/// VERIFY it (both checks), raise the start bools on a later scan, observe until the test completes.
/// Unconditionally, pass or fail (D34) — a failing test never diverts the sequence, because the inert
/// phase is what contains a bad state.</para>
///
/// <para><b>A POLL IS ONE ROUND TRIP</b> (§12a derivation 1). There is no separate "poll rate" to tune:
/// the period is what the link gives, and at K=1 that is one round trip per slot per poll round. This
/// runner therefore counts poll ROUNDS and round trips, and never sleeps.</para>
///
/// <para><b>The backstop is computed, not guessed</b> (§12a derivation 4). Without the RTT_max term a
/// healthy test reports TIMED-OUT roughly every 22nd wave.</para>
/// </summary>
public static class SlotRun
{
    /// <summary>Run one vector against one slot.</summary>
    /// <param name="nowMs">Monotonic milliseconds. Injected so the backstop is testable with no clock skew and no waiting.</param>
    public static SlotRunResult Run(MirrorClient client, int slotIndex, WireVector vector, Func<long>? nowMs = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(vector);

        var roundTripsBefore = client.RoundTrips;
        var stopwatch = Stopwatch.StartNew();
        nowMs ??= () => stopwatch.ElapsedMilliseconds;

        var inert = InertPhase.Establish(client, slotIndex, vector.Values, vector.Inert);
        if (!inert.Established)
        {
            return new SlotRunResult(SlotOutcome.NotInert, Array.Empty<ushort>(), 0, 0, 0,
                client.RoundTrips - roundTripsBefore, inert,
                "the test never started, which is not a test failure: " + inert.Detail);
        }

        var startScan = InertPhase.Commit(client, inert, new[] { slotIndex });

        // Two round trips per poll round: the control region (scan counter, and the version check rides
        // on it) and the slot's own result region. That is exactly RegisterMap.PollRoundTrips at K=1.
        var backstop = WireTiming.BackstopMs(vector.DeclaredScans, expectedRoundTrips: 2);
        var deadline = nowMs() + backstop;

        var polls = 0;
        while (true)
        {
            var control = client.ReadControl();
            var results = client.ReadResults(slotIndex);
            polls++;

            if (vector.CompletionRegister < results.Length && results[vector.CompletionRegister] == vector.CompletionValue)
            {
                return new SlotRunResult(SlotOutcome.Completed, results, startScan, control.ScanCounter, polls,
                    client.RoundTrips - roundTripsBefore, inert,
                    $"completion register R{vector.CompletionRegister:000} reached {vector.CompletionValue} after {control.ScanCounter - startScan} scan(s) and {polls} poll round(s).");
            }

            if (nowMs() >= deadline)
            {
                return new SlotRunResult(SlotOutcome.TimedOut, results, startScan, control.ScanCounter, polls,
                    client.RoundTrips - roundTripsBefore, inert,
                    $"the backstop of {backstop} ms elapsed with R{vector.CompletionRegister:000} reading "
                    + (vector.CompletionRegister < results.Length ? results[vector.CompletionRegister].ToString() : "<outside the slot>")
                    + $" rather than {vector.CompletionValue}. TIMED-OUT is not FAILED: the condition may simply never have occurred.");
            }
        }
    }
}
