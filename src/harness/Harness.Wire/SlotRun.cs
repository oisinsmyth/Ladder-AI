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
/// <param name="Duration">
/// The vector's declared maximum duration IN SCANS, <b>with the compression factor it was declared at</b>
/// (X-B). It is an input to the wall-clock backstop, never the backstop itself — and it is a
/// <see cref="ScanBudget"/> rather than an <c>int</c> because a scan count consumed at the wrong <c>comp</c>
/// produces a spurious TIMED-OUT on a healthy test.
/// </param>
/// <param name="Settling">
/// 🔴 <b>THE DWELL TO TAKE AT THIS INDEX'S CLOSE, WHILE ITS STATE IS STILL THE CURRENT ONE.</b>
///
/// <para>*** BEFORE THIS EXISTED, ONLY THE LAST VECTOR ON A SLOT COULD EVER SETTLE. *** Settling was
/// evaluated while building the result packages, after the WHOLE wave had finished, by re-reading the
/// device — and by then the next index's inert phase had moved the program on, so every non-final
/// vector was <c>NotEstablished</c> by construction. Measured on a real wave: three vectors on one
/// slot, all assertions held, and the two that were not last came back UNSETTLED for that reason
/// alone.</para>
///
/// <para>Null means the caller declared no settling for this vector, which stays
/// <c>NotEstablished</c> downstream — never "settled".</para>
/// </param>
public sealed record WireVector(
    ushort[] Values,
    InertDeclaration Inert,
    int CompletionRegister,
    ushort CompletionValue,
    ScanBudget Duration,
    SettlingProbe? Settling = null);

/// <summary>
/// What to hold still, and for how long, to decide whether an observed value was FINAL.
/// </summary>
/// <param name="Registers">
/// The result registers the declared settling signals resolve to, <b>already widened</b> — a Time
/// occupies two registers and comparing only the first would miss a value moving in its other half.
/// Resolution happens once, at the caller, using the one join key in this system.
/// </param>
/// <param name="UnchangedForScans">Scans the band must hold still. Must be positive; a zero dwell settles anything.</param>
public sealed record SettlingProbe(IReadOnlyList<SettlingRegister> Registers, int UnchangedForScans);

/// <summary>
/// One probed register, <b>carrying the SIGNAL NAME it came from</b>.
///
/// <para>🔴 <b>The name is not decoration.</b> A report that says only <c>R003 moved 5 -> 7</c> makes
/// attributing a wave of unsettled verdicts a forensic exercise — which this codebase records having
/// done once already. The declaration is written in signal names, so the finding has to come back in
/// them.</para>
/// </summary>
public sealed record SettlingRegister(string Signal, int Register);

/// <summary>
/// The dwell, taken. <b>Reported whatever it found</b> — the point is that the sample EXISTS for every
/// index, not that it was favourable.
/// </summary>
/// <param name="Taken">False when the dwell could not be performed; the reason is in <paramref name="Detail"/>.</param>
/// <param name="Unchanged">True only when every probed register read the same value after the dwell as at the close.</param>
/// <param name="Detail">Which register moved and from what to what, or why no sample was taken.</param>
/// <param name="ScansWaited">Scans actually waited, so a dwell cut short by the poll bound is visible rather than assumed.</param>
public sealed record SettlingSample(bool Taken, bool Unchanged, string Detail, long ScansWaited);

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
/// <param name="Results">
/// The slot's result registers, read once completion was recognised.
///
/// <para>⚠️ <b>THIS IS ONE INSTANT, AND FOR A MODEL WITH A TAIL RECOVERY IT IS THE INERT ONE.</b> It is
/// kept because it is what settling compares against and what the completion detail describes — but an
/// expectation must be evaluated against <see cref="Observations"/>, not against this. See
/// <see cref="ObservationSeries"/> for the measured defect.</para>
/// </param>
/// <param name="StartScan">The scan counter at the commit — the test's T=0 (D37).</param>
/// <param name="EndScan">The scan counter at the observation that recognised completion.</param>
/// <param name="Inert">The inert phase's own report, kept separate: outputs are not recorded during inert.</param>
/// <param name="RoundTrips">Round trips the whole sequence cost. The unit that was measured to matter.</param>
/// <param name="Observations">
/// 🔴 <b>EVERY OBSERVATION THIS INDEX PRODUCED, not just the completing one — and it is REQUIRED, with no
/// default.</b>
///
/// <para>A default of <see cref="ObservationSeries.Empty"/> here would let a construction site forget the
/// series and produce a result that reads as "nothing was observed" while a poll loop had run thousands
/// of rounds. Every site states it; the ones that genuinely observed nothing state
/// <see cref="ObservationSeries.Empty"/> and mean it.</para>
/// </param>
public sealed record SlotRunResult(
    SlotOutcome Outcome,
    ushort[] Results,
    ScanCount StartScan,
    ScanCount EndScan,
    int PollRounds,
    int RoundTrips,
    InertReport Inert,
    string Detail,
    ObservationSeries Observations,

    /// <summary>
    /// The settling dwell taken at this index's close, or null when none was asked for.
    ///
    /// <para><b>Null is "nobody asked", not "it settled".</b> The evaluator turns a null into
    /// <c>NotEstablished</c> with that reason, which is the same treatment every other missing input
    /// gets in this system.</para>
    /// </summary>
    SettlingSample? Settling = null)
{
    /// <summary>Scans from T=0 to the observation that recognised completion.</summary>
    /// <summary>Scans from T=0, taken MODULARLY so the count is right across the counter's wrap.</summary>
    public long ElapsedScans => EndScan.Since(StartScan);
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
    /// <param name="compression">
    /// <b>The factor this run actually uses</b>, which is not necessarily the one the vector's scan counts
    /// were declared at. Required rather than defaulted: a default of 1 here would silently re-introduce
    /// exactly the mismatch <see cref="ScanBudget"/> exists to close.
    /// </param>
    /// <param name="nowMs">Monotonic milliseconds. Injected so the backstop is testable with no clock skew and no waiting.</param>
    public static SlotRunResult Run(MirrorClient client, RuntimeCompression compression, int slotIndex, WireVector vector, Func<long>? nowMs = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(compression);
        ArgumentNullException.ThrowIfNull(vector);

        var roundTripsBefore = client.RoundTrips;
        var stopwatch = Stopwatch.StartNew();
        nowMs ??= () => stopwatch.ElapsedMilliseconds;

        var inert = InertPhase.Establish(client, slotIndex, vector.Values, vector.Inert);
        if (!inert.Established)
        {
            return new SlotRunResult(SlotOutcome.NotInert, Array.Empty<ushort>(), default, default, 0,
                client.RoundTrips - roundTripsBefore, inert,
                "the test never started, which is not a test failure: " + inert.Detail,
                // Genuinely nothing: no poll round ran. Stated rather than defaulted.
                ObservationSeries.Empty);
        }

        var startScan = InertPhase.Commit(client, inert, new[] { slotIndex });

        // Two round trips per poll round: the control region (scan counter, and the version check rides
        // on it) and the slot's own result region. That is exactly RegisterMap.PollRoundTrips at K=1.
        var backstop = WireTiming.BackstopMs(vector.Duration, compression, expectedRoundTrips: 2);
        var deadline = nowMs() + backstop;

        var polls = 0;

        // 🔴 *** EVERY POLL IS RECORDED, NOT ONLY THE ONE THAT ENDS THE LOOP. *** This loop used to
        // reassign `results` and return whichever round happened to recognise completion — which, for any
        // model that recovers to inert before announcing it has finished, is guaranteed to be the one
        // instant at which nothing is commanded. See ObservationSeries.
        var recorder = new ObservationRecorder();

        while (true)
        {
            var control = client.ReadControl();
            var results = client.ReadResults(slotIndex);
            polls++;
            recorder.Record(control.ScanCounter, polls, results);

            if (vector.CompletionRegister < results.Length && results[vector.CompletionRegister] == vector.CompletionValue)
            {
                // 🔴 *** THE DWELL HAPPENS HERE, AND NOWHERE ELSE WILL DO. *** This is the last moment at
                // which the program is still in the state this vector left it: the caller has not advanced
                // to the next index, so its inert phase has not yet moved anything. Taking the sample after
                // the wave - which is where settling used to be decided - is why only the LAST vector on a
                // slot could ever establish it.
                var settling = Dwell(client, slotIndex, vector.Settling, results, control.ScanCounter);

                return new SlotRunResult(SlotOutcome.Completed, results, startScan, control.ScanCounter, polls,
                    client.RoundTrips - roundTripsBefore, inert,
                    $"completion register R{vector.CompletionRegister:000} reached {vector.CompletionValue} after {control.ScanCounter.Since(startScan)} scan(s) and {polls} poll round(s). "
                    + recorder.Build().Describe()
                    + (settling is null ? string.Empty : " " + settling.Detail),
                    recorder.Build(),
                    settling);
            }

            // *** NO DWELL ON A TIMED-OUT INDEX, DELIBERATELY. *** Settling asks whether an observed value
            // was FINAL, and a run that never reached its completion condition has no observed value to
            // ask about. Dwelling here would produce a confident "unchanged" over a program still mid-test.
            if (nowMs() >= deadline)
            {
                return new SlotRunResult(SlotOutcome.TimedOut, results, startScan, control.ScanCounter, polls,
                    client.RoundTrips - roundTripsBefore, inert,
                    $"the backstop of {backstop} ms elapsed with R{vector.CompletionRegister:000} reading "
                    + (vector.CompletionRegister < results.Length ? results[vector.CompletionRegister].ToString() : "<outside the slot>")
                    + $" rather than {vector.CompletionValue}. TIMED-OUT is not FAILED: the condition may simply never have occurred. "
                    + recorder.Build().Describe(),
                    recorder.Build());
            }
        }
    }

    /// <summary>
    /// Hold for the declared scans, then re-read the slot's band and report whether the probed registers
    /// moved.
    /// </summary>
    /// <remarks>
    /// <para><b>Every road that is not a comparison returns <c>Taken: false</c> WITH ITS REASON.</b> A
    /// sample that could not be taken must never arrive downstream looking like one that was taken and
    /// found nothing — that is the shape of green this project keeps having to retract.</para>
    ///
    /// <para><b>The poll bound is the same 200 the previous implementation used</b>, kept so the change
    /// is a MOVE rather than a move plus a quiet re-tuning. Exhausting it is <c>Taken: false</c>, not a
    /// pass: it means the dwell never completed.</para>
    /// </remarks>
    internal static SettlingSample? Dwell(
        MirrorClient client, int slotIndex, SettlingProbe? probe, ushort[] atClose, ScanCount from)
    {
        if (probe is null)
            return null;

        if (probe.UnchangedForScans <= 0)
        {
            return new SettlingSample(false, false,
                $"the settling dwell was declared as {probe.UnchangedForScans} scan(s), and a zero-length dwell is "
                + "satisfied by any program whatsoever. NOT settled.", 0);
        }

        if (probe.Registers.Count == 0)
        {
            return new SettlingSample(false, false,
                "the settling probe names no register, so there is nothing to hold still. EMPTY IS NOT CLEAN: a "
                + "comparison over zero registers is satisfied by anything. NOT settled.", 0);
        }

        var outOfBand = probe.Registers.Where(r => r.Register < 0 || r.Register >= atClose.Length).ToArray();
        if (outOfBand.Length > 0)
        {
            return new SettlingSample(false, false,
                $"the settling probe reaches R{string.Join(", R", outOfBand.Select(r => r.Register.ToString("000")))} and the slot's "
                + $"band holds {atClose.Length} register(s), so the comparison could not be made against what was observed.", 0);
        }

        for (var poll = 0; poll < 200; poll++)
        {
            var waited = client.ReadControl().ScanCounter.Since(from);
            if (waited < probe.UnchangedForScans)
                continue;

            var now = client.ReadResults(slotIndex);

            // Named by SIGNAL as well as register: the declaration is written in signal names, so the
            // finding has to come back in them.
            var moved = probe.Registers
                .Where(r => r.Register < now.Length && now[r.Register] != atClose[r.Register])
                .Select(r => $"R{r.Register:000} ('{r.Signal}') moved {atClose[r.Register]} -> {now[r.Register]}")
                .ToArray();

            return moved.Length == 0
                ? new SettlingSample(true, true,
                    $"settled: every one of the {probe.Registers.Count} probed register(s) held its value across "
                    + $"{waited} scan(s) after the close.", waited)
                : new SettlingSample(true, false,
                    $"NOT settled: {moved.Length} probed register(s) moved in the {waited} scan(s) after the close - "
                    + string.Join(", ", moved) + ". The observed value was not final.", waited);
        }

        return new SettlingSample(false, false,
            $"the dwell of {probe.UnchangedForScans} scan(s) had not elapsed after 200 poll round(s), so no comparison "
            + "was made. That is NOT settled - it is a dwell that never completed.", 0);
    }
}
