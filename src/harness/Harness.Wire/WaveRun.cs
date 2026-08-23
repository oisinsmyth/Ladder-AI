using System.Diagnostics;

namespace Harness.Wire;

/// <summary>
/// One slot's submission: a COLUMN of vectors that differ only in values (D26a).
///
/// <para>"Same methodology" means same register layout and same observability declaration — values only.
/// That is the load-bearing half of D26a rule 1 and it is why the map can be frozen: a slot's layout is
/// constant across every index, so each index only rewrites values into a fixed shape.</para>
/// </summary>
public sealed record SlotTensor(int SlotIndex, IReadOnlyList<WireVector> Vectors)
{
    public int Length => Vectors.Count;
}

/// <summary>One slot's results, distributed when THAT slot finished — not at wave end.</summary>
/// <param name="CompletedAtIndex">
/// The wave index at which the slot's own tensor ran out. D26a rule 3: feedback latency is bounded by a
/// slot's OWN tensor length, not the wave's.
/// </param>
/// <param name="CoRunning">Who actually ran alongside it, per index, measured (X-E).</param>
public sealed record SlotDistribution(
    int SlotIndex,
    int CompletedAtIndex,
    IReadOnlyList<SlotRunResult> Results,
    IReadOnlyList<(int WaveIndex, IReadOnlyList<int> CoRunners)> CoRunning);

/// <summary>
/// When to stop re-arming a slot that keeps timing out. <b>The cost this bounds is not hypothetical:</b>
/// nothing in the wave ever stopped attempting a slot, so a wedged one was re-armed at every remaining
/// index and paid a FULL BACKSTOP each time. Measured against one real submission's declared budgets —
/// 6,168 + 8,568 + 44,328 scans — a slot that never completes costs <b>~24.7 minutes and produces no
/// evidence at all.</b>
///
/// <para><b>Why a count and not "stop at the first one".</b> A single TIMED-OUT is genuinely ambiguous:
/// X-B invented that outcome precisely so "the condition never occurred" stays distinguishable from a
/// failure, and one index can legitimately not reach its condition while the next does. Two in a row is
/// a slot that is not answering.</para>
///
/// <para>🔴 <b>ABANDONING IS NOT FAILING, AND IT MUST NOT READ AS COVERAGE.</b> The indices not run are
/// reported through the EXISTING <c>NEVER ATTEMPTED</c> path — <c>VectorDisposition.SlotExitedFirst</c> —
/// which already states the index reached, the outcome that stopped it, and that nothing there is
/// evidence about the block. No result is fabricated for a vector that was never submitted.</para>
/// </summary>
/// <param name="ConsecutiveIndices">
/// How many consecutive TIMED-OUT indices end the slot. <b><c>null</c> means never abandon</b> — stated,
/// not defaulted, so "keep going forever" is a choice someone made rather than a field left blank.
/// </param>
public sealed record TimeoutAbandon(int? ConsecutiveIndices)
{
    /// <summary>Never stop re-arming. The behaviour before 2026-08-22, kept expressible so a test can ask for it.</summary>
    public static readonly TimeoutAbandon Never = new((int?)null);

    /// <summary>Stop after <paramref name="indices"/> consecutive TIMED-OUT indices.</summary>
    public static TimeoutAbandon AfterConsecutive(int indices) => indices >= 1
        ? new TimeoutAbandon(indices)
        : throw new ArgumentOutOfRangeException(nameof(indices), indices, "abandoning after fewer than one timed-out index would stop a slot that has not yet timed out once. Use TimeoutAbandon.Never to disable.");

    /// <summary>Two consecutive timeouts — the policy argued for above.</summary>
    public static readonly TimeoutAbandon Default = AfterConsecutive(2);

    /// <summary>Whether a run of <paramref name="consecutiveTimeouts"/> has reached this policy's limit.</summary>
    public bool Reached(int consecutiveTimeouts) =>
        ConsecutiveIndices is int limit && consecutiveTimeouts >= limit;

    public override string ToString() =>
        ConsecutiveIndices is int n ? $"abandon after {n} consecutive TIMED-OUT index(es)" : "never abandon";
}

/// <summary>
/// 🔴 <b>Retrying the INERT PHASE at the first index, because a download leaves the plant moving.</b>
///
/// <para><b>What this replaces.</b> The batch runner slept for a fixed 15 s after a download and then
/// started the wave anyway. Measured 2026-08-22: the wave still refused <c>NotQuiescent</c> — the vessel
/// model's R013/R014 were still alternating — and a re-run minutes later passed 3 of 3. A blind wait is
/// a guess at a duration nobody has measured, and it is wrong in both directions: too short and the
/// wave refuses, too long and every deploy pays for the worst case.</para>
///
/// <para><b>The inert phase IS the readiness test.</b> It takes two observations a scan apart and
/// compares them, which is precisely "has this slot's result band stopped moving". So the fix is to ASK
/// AGAIN rather than to wait blind — and the number of attempts it took is then a MEASUREMENT of the
/// settling time, which is the thing nobody has.</para>
///
/// <para>⚠️ <b>FIRST INDEX ONLY, and that is not an optimisation.</b> A slot that is not quiescent at
/// index 3 has been disturbed by something the wave itself did, and retrying there would paper over a
/// real finding — the model is not returning to rest between indices, which is exactly what D33 exists
/// to catch. Only the first index has an excuse, and only because a download just happened.</para>
///
/// <para><b>The caller licenses it.</b> The wave does not decide to be lenient; whoever knows a
/// download just occurred passes this. Default is <see cref="None"/>.</para>
/// </summary>
/// <param name="Attempts">Total inert attempts at index 0, including the first. 1 means no retry.</param>
public sealed record InertSettle(int Attempts, TimeSpan Between)
{
    /// <summary>No retry. The behaviour everywhere that has not just downloaded.</summary>
    public static readonly InertSettle None = new(1, TimeSpan.Zero);

    public static InertSettle Retry(int attempts, TimeSpan between) => attempts >= 1
        ? new InertSettle(attempts, between)
        : throw new ArgumentOutOfRangeException(nameof(attempts), attempts, "an inert phase is attempted at least once.");

    public override string ToString() =>
        Attempts <= 1 ? "no inert retry" : $"up to {Attempts} inert attempts at index 0, {Between.TotalSeconds:0.#}s apart";
}

/// <summary>
/// 🔴 <b>What the post-download transient actually cost — the measurement, not the prose about it.</b>
///
/// <para><b>Why this is a record and not two fields on <see cref="WaveResult"/>.</b> The pair only ever
/// means anything together: an attempt count with no elapsed time, or an elapsed time with no attempt
/// count, is a half-answer that a reader has to guess at. One nullable object has exactly two states —
/// measured, or not licensed — and no way to be in a third.</para>
///
/// <para>🔴 <b><c>Attempts == 1</c> and <c>Waited == 0</c> is a REAL MEASUREMENT, and it is the one this
/// type exists for.</b> It says the plant was quiescent the first time it was asked, immediately after a
/// download. Before this existed the only signal was a prose string set <i>only</i> when the retry loop
/// had to work, so "settled instantly" and "nobody ever asked" were the same observation — a null — and
/// the run that finally measured the transient at zero was indistinguishable from the run that never
/// measured it at all. <b>Absent is not empty</b> (FI-44), one level in from where that rule is usually
/// applied: the check was not empty, it was silent about its success.</para>
/// </summary>
/// <param name="Attempts">
/// Inert attempts the first index needed. <b>At least 1</b> — the phase is always attempted once, so a
/// zero here would describe something that did not happen.
/// </param>
/// <param name="Waited">
/// Wall time spent waiting BETWEEN attempts, so <c>Attempts == 1</c> implies <c>Zero</c>. This is the
/// settling time, and it is deliberately not the duration of the attempts themselves — those cost
/// round trips whether the plant is quiescent or not.
/// </param>
/// <param name="Quiescent">
/// Whether the plant ever went quiet. <b>False is still a measurement</b> and must not be read as a
/// missing one: it says the transient outlasted the licence, which is a finding about the plant.
/// </param>
public sealed record InertSettleMeasurement(int Attempts, TimeSpan Waited, bool Quiescent)
{
    public override string ToString() =>
        Quiescent
            ? $"quiescent after {Attempts} inert attempt(s), {Waited.TotalSeconds:0.#}s waited"
            : $"STILL not quiescent after {Attempts} inert attempt(s), {Waited.TotalSeconds:0.#}s waited";
}

/// <summary>
/// 🔴 <b>Why a wave stopped before its last index, when something stopped it.</b>
///
/// <para><b>Absent means it ran to the end.</b> Not "probably fine" — the wave either reached its
/// length or it says here why it did not.</para>
/// </summary>
/// <param name="AtIndex">The index that was in flight. Everything BEFORE it completed and is reported.</param>
/// <param name="IndicesCompleted">
/// How many indices finished. <b>The denominator</b>: a package that does not say how much of the wave
/// happened cannot be told apart from a whole one.
/// </param>
public sealed record WaveInterruption(int AtIndex, int IndicesCompleted, int IndicesNeverAttempted, string Detail)
{
    public override string ToString() =>
        $"THE WAVE DID NOT FINISH: it stopped at index {AtIndex}. {IndicesCompleted} index(es) completed and are reported; "
        + $"{IndicesNeverAttempted} were never attempted. {Detail}";
}

/// <summary>What a whole wave produced.</summary>
/// <param name="Length">Indices run — MAX tensor length across the slots (D26a), never a colouring decision.</param>
/// <param name="Interruption">
/// 🔴 <b>Null when the wave ran to its end.</b> Before this existed, a dropped link threw out of
/// <c>Run</c>, out of <c>LoopRun.Execute</c>, and out of <c>harness-run</c> before it ever wrote a
/// package — so a wave that had completed most of its indices produced NOTHING, and the only record was
/// a stack trace in a terminal.
/// </param>
public sealed record WaveResult(
    int Length,
    IReadOnlyList<SlotDistribution> Distributions,
    CoRunningLog Log,
    int RoundTrips,
    WaveInterruption? Interruption = null,

    /// <summary>
    /// Prose about the settling, written ONLY when the retry loop had to work more than once. Kept
    /// because callers render it, but it is <b>not</b> the measurement — see <see cref="InertSettle"/>
    /// below, which is null in exactly one situation instead of two.
    /// </summary>
    string? SettleReport = null,

    /// <summary>
    /// 🔴 <b>The measurement, and its ONLY absent meaning is "no retry was licensed".</b> Present
    /// whenever the caller asked for one — including <c>Attempts == 1, Waited == 0</c>, which is the
    /// plant having been quiescent immediately and is the result this field was added to make visible.
    /// <see cref="SettleReport"/> stays null in that case and always did, which is the defect.
    /// </summary>
    InertSettleMeasurement? InertSettle = null)
{
    public SlotDistribution For(int slotIndex) => Distributions.Single(d => d.SlotIndex == slotIndex);

    /// <summary>Stated positively so a caller has to handle it rather than notice a null.</summary>
    public bool RanToCompletion => Interruption is null;
}

/// <summary>
/// Build-plan items 3.4 and 3.5 — the wave: inert, tensor[0], inert, tensor[1], … with slots entering
/// and leaving on their own tensor lengths.
///
/// <para><b>Wave length is MAX TENSOR LENGTH</b> (D26a). Nothing this runner does can change it, and
/// DB-13's colouring does not set it either — what the conflict graph decides is which slots may SHARE a
/// wave set, which costs an extra wave rather than a longer one.</para>
///
/// <para><b>A slot with no vector at index i is NULL, and null means INERT</b> (D26a rule 2). There is no
/// new encoding: its start bool is simply not raised at that index, D33's inert holds, and its values are
/// don't-care. The same mechanism covers an EXCISED slot — allocated and null at every index — which is
/// why excision needs no re-derivation of the map.</para>
///
/// <para><b>A slot exits when its OWN tensor is done</b> (D26a rule 3), and its results are distributed
/// at that moment rather than batched to wave end. A 3-vector slot in a 6-index wave has its results
/// after index 2.</para>
/// </summary>
public static class WaveRun
{
    /// <summary>Run one wave over a set of slot tensors.</summary>
    /// <param name="onSlotComplete">
    /// Called the moment a slot's tensor runs out, before the wave continues. This is D26a rule 3's whole
    /// point: a caller that only reads the returned <see cref="WaveResult"/> has re-batched the feedback
    /// to wave end, which is the latency the rule exists to remove.
    /// </param>
    /// <param name="compression">
    /// <b>The factor this wave actually runs at.</b> It comes before the tensors because it governs how
    /// every scan count in them is read: a maximum duration declared at one <c>comp</c> and consumed at
    /// another is a backstop that fires on a healthy test. Required, never defaulted — see
    /// <see cref="ScanBudget"/>.
    /// </param>
    /// <param name="abandon">
    /// When to stop re-arming a slot that keeps timing out. <b>Omitted means
    /// <see cref="TimeoutAbandon.Default"/>, which DOES abandon</b> — pass <see cref="TimeoutAbandon.Never"/>
    /// for the pre-2026-08-22 behaviour of retrying a wedged slot at every remaining index. The permissive
    /// option is the one you have to ask for, because it is the one that costs ~24.7 minutes for no
    /// evidence.
    /// </param>
    public static WaveResult Run(
        MirrorClient client,
        RuntimeCompression compression,
        IReadOnlyList<SlotTensor> tensors,
        Func<long>? nowMs = null,
        Action<SlotDistribution>? onSlotComplete = null,
        TimeoutAbandon? abandon = null,
        InertSettle? inertSettle = null,
        Action<TimeSpan>? pauseFor = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(compression);
        ArgumentNullException.ThrowIfNull(tensors);

        abandon ??= TimeoutAbandon.Default;
        var settle = inertSettle ?? InertSettle.None;
        var pause = pauseFor ?? Thread.Sleep;
        string? settleReport = null;
        InertSettleMeasurement? settleMeasurement = null;

        if (tensors.Count == 0)
            throw new ArgumentException("a wave over no slots runs nothing. Empty is not clean.", nameof(tensors));

        if (tensors.Any(t => t.Length == 0))
            throw new ArgumentException("a slot submitted an empty tensor. A slot with nothing to run should not be in the wave set at all — it is not the same thing as a slot that is null at some index.", nameof(tensors));

        if (tensors.Select(t => t.SlotIndex).Distinct().Count() != tensors.Count)
            throw new ArgumentException("two tensors name the same slot.", nameof(tensors));

        var roundTripsBefore = client.RoundTrips;
        var stopwatch = Stopwatch.StartNew();
        nowMs ??= () => stopwatch.ElapsedMilliseconds;

        var log = new CoRunningLog();
        var collected = tensors.ToDictionary(t => t.SlotIndex, _ => new List<SlotRunResult>());
        var distributions = new List<SlotDistribution>();

        var length = tensors.Max(t => t.Length);

        // Per slot: how many indices IN A ROW have ended TIMED-OUT, and which slots have been given up
        // on. A slot in `abandoned` is not active at any later index and its remaining vectors are never
        // submitted — reported as NEVER ATTEMPTED, never as a result.
        var consecutiveTimeouts = tensors.ToDictionary(t => t.SlotIndex, _ => 0);
        var abandoned = new HashSet<int>();

        // 🔴 Set when the link dies mid-wave. Everything already in `collected` is still distributed
        // below — that is the whole point of catching it here rather than letting it reach the CLI,
        // where it escaped before `Write(result)` and threw away every completed index.
        WaveInterruption? interruption = null;
        var indicesCompleted = 0;

        for (var index = 0; index < length; index++)
        {
            // Active = the slots that still have a vector at THIS index. The rest are null: not
            // commanded, not verified, values don't-care.
            var active = tensors.Where(t => index < t.Length && !abandoned.Contains(t.SlotIndex)).ToArray();

            // Every slot that had indices left has been abandoned. Continuing would run inert phases
            // against nothing, which is the "empty is not clean" shape: a wave that examined nothing
            // must not spend the remaining indices looking busy.
            if (active.Length == 0)
                break;

            InertReport? inertForCatch = null;

            try
            {
                var slotInerts = active
                    .Select(t => new SlotInert(t.SlotIndex, t.Vectors[index].Values, t.Vectors[index].Inert))
                    .ToArray();

                var inert = InertPhase.Establish(client, slotInerts);

                // Retried ONLY at index 0, and only when the caller said a download just happened. Every
                // attempt is counted so the settling time is reported rather than assumed — that count is
                // the measurement the fixed 15 s wait never produced.
                var inertAttempts = 1;
                if (index == 0 && settle.Attempts > 1)
                {
                    while (!inert.Established && inertAttempts < settle.Attempts)
                    {
                        if (settle.Between > TimeSpan.Zero)
                            pause(settle.Between);

                        inertAttempts++;
                        inert = InertPhase.Establish(client, slotInerts);
                    }

                    // 🔴 SET UNCONDITIONALLY, INSIDE THE "RETRY WAS LICENSED" BRANCH — that placement IS
                    // the fix. The prose below is written only when the loop had to work, so on the run
                    // where the plant is quiescent immediately it stays null and the measurement is lost.
                    // Those are the two runs a reader most needs to tell apart: "settled instantly" and
                    // "nobody asked" reported identically, and the second is what everyone assumed.
                    //
                    // Waited is (attempts - 1) intervals: the wait happens BETWEEN attempts, so a single
                    // attempt waited for nothing. Same arithmetic the prose uses, so the two cannot drift.
                    settleMeasurement = new InertSettleMeasurement(
                        inertAttempts,
                        TimeSpan.FromTicks(settle.Between.Ticks * (inertAttempts - 1)),
                        inert.Established);

                    if (inertAttempts > 1)
                    {
                        settleReport = inert.Established
                            ? $"the first index needed {inertAttempts} inert attempt(s) over "
                              + $"{(inertAttempts - 1) * settle.Between.TotalSeconds:0.#}s before the plant was quiescent — "
                              + "the post-download transient, MEASURED rather than waited out."
                            : $"the first index made {inertAttempts} inert attempt(s) over "
                              + $"{(inertAttempts - 1) * settle.Between.TotalSeconds:0.#}s and the plant was STILL not quiescent. "
                              + "That is no longer a startup transient — something is moving that the declaration does not expect.";
                    }
                }

                // Captured for the catch below, which cannot see into this scope. If the link dies AFTER
                // this point the real inert report is reported with the lost index; if it dies during
                // Establish, the catch synthesises one that says the phase never concluded — rather than
                // one that looks like a measurement.
                inertForCatch = inert;

                // 🔴 *** PLANNED, NOT COMMANDED — AND THE NAME IS THE FIX. ***
                //
                // This variable was called `commanded` and was handed to the co-running log on BOTH paths
                // below, including the one where `InertPhase.Commit` is never called. `Establish` has just
                // written LOW start bools and CLEARED the echo, so on the refusal path the log compared a
                // fabricated commanded set against a freshly-zeroed echo and could only ever report X-E's
                // `CommandedButDidNotRun`: *"the slot was commanded and the echo says its block never saw its
                // start condition"*.
                //
                // Measured on JOB9004's vessel wave, 2026-08-18: the run's own last control frame read
                // StartBools = 0x0000 — the commit provably had not happened — while the result package
                // accused the block of not starting, and the investigation that produced went hunting a
                // start-bit write race in this client that does not exist. A plan is not evidence; the whole
                // of X-E is that sentence, and this is where the log was quietly breaking it.
                //
                // It becomes `commanded` at exactly one point in this method: after `Commit` returns.
                var planned = active.Select(t => t.SlotIndex).ToArray();

                if (!inert.Established)
                {
                    foreach (var t in active)
                    {
                        collected[t.SlotIndex].Add(new SlotRunResult(SlotOutcome.NotInert, Array.Empty<ushort>(), default, default, 0, 0, inert,
                            "the test never started, which is not a test failure: " + inert.Detail,
                            // No poll round ran at all. Stated, not defaulted.
                            ObservationSeries.Empty));
                    }

                    // D34 alternates inert/test unconditionally, but an inert phase that could not be
                    // ESTABLISHED is O6's residual — a wave-blocking condition rather than a test failure —
                    // and continuing would run every later index from a state nobody verified.
                    //
                    // The echo is still READ here: a latch set at an index where this client raised nothing is
                    // `RanButWasNotCommanded`, which is real and more alarming here than anywhere else.
                    log.RecordNotCommitted(index, planned, client.ReadControlUnverified(), client.Map.Slots.Count);
                    break;
                }

                // ---- THE COMMIT. Everything above ran with the start bools LOW; everything below runs with
                // them HIGH, and only from here is `planned` also `commanded`.
                var commanded = planned;
                var startScan = InertPhase.Commit(client, inert, commanded);

                var perIndex = Observe(client, compression, active, index, startScan, inert, nowMs);
                foreach (var (slotIndex, result) in perIndex)
                {
                    collected[slotIndex].Add(result);

                    // CONSECUTIVE, so anything that is not a timeout resets the run. A slot that times out,
                    // then completes, then times out has not stopped answering — it has produced two
                    // different answers, and only an unbroken run is evidence that it has stopped.
                    consecutiveTimeouts[slotIndex] = result.Outcome == SlotOutcome.TimedOut
                        ? consecutiveTimeouts[slotIndex] + 1
                        : 0;
                }

                // 🔴 *** THE ECHO IS READ HERE, AFTER THE OBSERVATION AND BEFORE THE NEXT INDEX'S INERT PHASE
                // CLEARS IT. *** That ordering is the whole handshake: the latch is set by the copy layer in
                // the same scan the start bit is copied, survives every poll gap (a short test can start and
                // finish between two polls), is read once here, and is released by the NEXT
                // `InertPhase.Establish` — never after a commit. Clearing it on the far side of a commit would
                // wipe a latch that had just been set and would be indistinguishable, in every artifact this
                // system produces, from a block that never started.
                log.Record(index, commanded, client.ReadControl(), client.Map.Slots.Count);

                // ---- ABANDONMENT. After the co-running log, so the abandoned slot's slice includes the index
                // that stopped it; before the exit loop, so it leaves by this path rather than that one.
                foreach (var tensor in active.OrderBy(t => t.SlotIndex))
                {
                    // A slot on its last index is finishing anyway — the exit loop below owns it, and calling
                    // it "abandoned" would claim vectors were skipped when there were none left.
                    if (index + 1 >= tensor.Length || !abandon.Reached(consecutiveTimeouts[tensor.SlotIndex]))
                        continue;

                    abandoned.Add(tensor.SlotIndex);

                    // Said on the LAST RESULT, because that is what VectorDisposition.SlotExitedFirst quotes
                    // back for every index that never ran. Without it the package reports the vectors as
                    // NEVER ATTEMPTED and says only that the slot "stopped" — true, and silent about why.
                    var last = collected[tensor.SlotIndex][^1];
                    collected[tensor.SlotIndex][^1] = last with
                    {
                        Detail = last.Detail
                            + $" *** SLOT ABANDONED: {consecutiveTimeouts[tensor.SlotIndex]} consecutive index(es) ended TIMED-OUT and the "
                            + $"declared policy is to {abandon}. Its remaining {tensor.Length - (index + 1)} index(es) were NOT submitted "
                            + "to the device. A slot that has not answered twice running is not answering, and re-arming it costs a full "
                            + "backstop per index for no evidence. *** THIS IS NOT A STATEMENT THAT THOSE VECTORS WOULD HAVE FAILED — "
                            + "nothing was learned about them, which is why they are reported as NEVER ATTEMPTED rather than as results.",
                    };

                    var abandonedDistribution = new SlotDistribution(tensor.SlotIndex, index,
                        collected[tensor.SlotIndex], log.SliceFor(tensor.SlotIndex));

                    distributions.Add(abandonedDistribution);
                    onSlotComplete?.Invoke(abandonedDistribution);
                }

                // D26a rule 3 — a slot exits when its OWN tensor is done, and its results go out THEN.
                // Abandoned slots are excluded: they were distributed above, at the index that stopped them.
                foreach (var tensor in tensors.Where(t => t.Length == index + 1 && !abandoned.Contains(t.SlotIndex)).OrderBy(t => t.SlotIndex))
                {
                    var distribution = new SlotDistribution(tensor.SlotIndex, index,
                        collected[tensor.SlotIndex], log.SliceFor(tensor.SlotIndex));

                    distributions.Add(distribution);
                    onSlotComplete?.Invoke(distribution);
                }
            }
            catch (WireLinkLostException lost)
            {
                // 🔴 *** THE WAVE STOPS, AND EVERYTHING ALREADY OBSERVED SURVIVES. ***
                //
                // Before this existed the exception left Run, left LoopRun.Execute, and reached the top
                // of harness-run — which never got as far as writing a package. A wave that had completed
                // most of its indices produced NOTHING, and the only record was a stack trace.
                //
                // Only WireLinkLostException is caught. Catching Exception here would swallow every
                // programming error in the observation path and file it as network weather, which is a
                // worse defect than the one being fixed.
                foreach (var t in active)
                {
                    // A slot that already has a result at THIS index keeps it: the link can die after
                    // Observe returned, on the control read. Adding a second result for the same index
                    // would put the tensor and the collected list out of step for every index after it.
                    if (collected[t.SlotIndex].Count > index)
                        continue;

                    collected[t.SlotIndex].Add(new SlotRunResult(
                        SlotOutcome.LinkLost, Array.Empty<ushort>(), default, default, 0, 0,
                        inertForCatch ?? new InertReport(InertOutcome.LinkLost, default,
                            Array.Empty<ushort>(), Array.Empty<ushort>(),
                            "the link went away before the inert phase concluded, so nothing was established or refuted about the slot's rest state."),
                        "NOTHING WAS LEARNED ABOUT THIS VECTOR: " + lost.Message,
                        ObservationSeries.Empty));
                }

                interruption = new WaveInterruption(index, indicesCompleted, Math.Max(0, length - index - 1), lost.Message);
                break;
            }

            indicesCompleted++;
        }

        // Any slot the loop never distributed — because inert failed and the wave stopped — still gets
        // its results, marked at the index it reached. Silence would read as "no results yet".
        foreach (var tensor in tensors.Where(t => distributions.All(d => d.SlotIndex != t.SlotIndex)).OrderBy(t => t.SlotIndex))
        {
            distributions.Add(new SlotDistribution(tensor.SlotIndex, collected[tensor.SlotIndex].Count - 1,
                collected[tensor.SlotIndex], log.SliceFor(tensor.SlotIndex)));
        }

        return new WaveResult(length, distributions, log, client.RoundTrips - roundTripsBefore, interruption, settleReport, settleMeasurement);
    }

    /// <summary>
    /// Poll every active slot until each has completed or the longest backstop has elapsed.
    ///
    /// <para><b>One FC03 per GROUP of whole slots per poll round, plus one control read</b> — exactly
    /// <c>RegisterMap.PollRoundTrips</c>, and exactly §12a's cost model with F-1's <c>P x ceil(K/R)</c>
    /// read term. A slot that has completed is never the REASON for a read, but it may ride along inside
    /// a group that was happening anyway, which costs nothing: the transaction is the unit, not the
    /// register.</para>
    /// </summary>

    private static IReadOnlyList<(int SlotIndex, SlotRunResult Result)> Observe(
        MirrorClient client,
        RuntimeCompression compression,
        IReadOnlyList<SlotTensor> active,
        int index,
        ScanCount startScan,
        InertReport inert,
        Func<long> nowMs)
    {
        var outstanding = active.ToDictionary(t => t.SlotIndex, t => t.Vectors[index]);
        var done = new List<(int SlotIndex, SlotRunResult Result)>();
        var polls = 0;

        // 🔴 *** ONE RECORDER PER SLOT, BECAUSE THE SERIES IS A PROPERTY OF THE SLOT AND NOT OF THE WAVE.
        // *** Slots leave the poll loop at different rounds, so a shared recorder would give an early
        // finisher the later slots' frames — and the frames are the evidence. See ObservationSeries for
        // the defect this closes: this loop kept exactly the round that recognised completion, which for
        // any model that recovers to inert BEFORE announcing completion is the one guaranteed-inert
        // instant in the whole index.
        var recorders = active.ToDictionary(t => t.SlotIndex, _ => new ObservationRecorder());

        // The backstop is per index and takes the LONGEST duration in the tensor, because the index costs
        // its longest member. *** LONGEST IS MEASURED AFTER RE-EXPRESSION AT THIS WAVE'S comp, NOT ON THE
        // RAW COUNTS *** — two vectors in one tensor may declare their scans at different factors, and 20
        // scans at comp=10 is ten times the behaviour of 20 scans at comp=1. Comparing the bare integers
        // picks the wrong vector and under-sizes the bound, which is a spurious TIMED-OUT on a healthy test.
        // Round trips are counted as (slots + 1) per poll round: registers appear nowhere in it, which is
        // the point — nothing here is derived from slot width.
        var backstop = WireTiming.BackstopMs(
            outstanding.Values.MaxBy(v => v.Duration.At(compression))!.Duration,
            compression,
            expectedRoundTrips: WireTiming.RoundTripsPerIndex(
                active.Count,
                client.Map.VectorRegistersPerSlot,
                client.Map.ResultRegistersPerSlot,
                pollRounds: 1));

        var deadline = nowMs() + backstop;

        // slot -> what it looked like when IT completed. The dwell below compares against this, not
        // against a band re-read after every slot has finished.
        var closes = new Dictionary<int, (SettlingProbe? Probe, ushort[] AtClose, ScanCount At)>();

        while (outstanding.Count > 0)
        {
            var control = client.ReadControl();
            polls++;

            // One read per group of whole slots — the map decides the grouping, and there is no register
            // in the request. A completed slot inside a group is read too and simply ignored.
            var observed = client.ReadResults(outstanding.Keys);

            foreach (var slotIndex in outstanding.Keys.ToArray())
            {
                var vector = outstanding[slotIndex];
                var results = observed[slotIndex];
                var recorder = recorders[slotIndex];

                // Recorded BEFORE the completion test, so the completing frame is in the series rather
                // than only in `Results`. A series missing its own last frame would make the new
                // behaviour incomparable with the old.
                recorder.Record(control.ScanCounter, polls, results);

                if (vector.CompletionRegister < results.Length && results[vector.CompletionRegister] == vector.CompletionValue)
                {
                    var series = recorder.Build();

                    // Remembered so the settling dwell below can compare against THIS slot's values at
                    // ITS close, rather than against whatever the band reads once every slot has finished.
                    closes[slotIndex] = (vector.Settling, results, control.ScanCounter);

                    done.Add((slotIndex, new SlotRunResult(SlotOutcome.Completed, results, startScan, control.ScanCounter,
                        polls, 0, inert,
                        $"completion register R{vector.CompletionRegister:000} reached {vector.CompletionValue} after {control.ScanCounter.Since(startScan)} scan(s) and {polls} poll round(s). "
                        + series.Describe(),
                        series)));

                    outstanding.Remove(slotIndex);
                }
                else if (nowMs() >= deadline)
                {
                    var series = recorder.Build();

                    done.Add((slotIndex, new SlotRunResult(SlotOutcome.TimedOut, results, startScan, control.ScanCounter,
                        polls, 0, inert,
                        $"the backstop of {backstop} ms elapsed with R{vector.CompletionRegister:000} reading "
                        + (vector.CompletionRegister < results.Length ? results[vector.CompletionRegister].ToString() : "<outside the slot>")
                        + $" rather than {vector.CompletionValue}. TIMED-OUT is not FAILED: the condition may simply never have occurred. "
                        + series.Describe(),
                        series)));

                    outstanding.Remove(slotIndex);
                }
            }
        }

        // -------------------------------------------------------------------------------------------
        // 🔴 THE SETTLING DWELL — HERE, AND NOWHERE LATER
        // -------------------------------------------------------------------------------------------
        //
        // *** BEFORE THIS, ONLY THE LAST VECTOR ON A SLOT COULD EVER SETTLE. *** Settling was decided
        // while building the result packages, after the WHOLE WAVE, by re-reading the device — and by then
        // the next index's inert phase had moved the program on, so `LoopRun.Settling` had to refuse any
        // index that was not the slot's last. Measured on a real wave: three vectors on one slot, every
        // assertion held, and the two that were not last came back UNSETTLED for that reason alone.
        //
        // This point is still INSIDE the index: every slot has reached its completion condition, and the
        // caller has not started the next index's inert phase. It is the last moment at which the state
        // is the one these vectors produced.
        //
        // ⚠️ *** WHAT THIS MEASURES, STATED EXACTLY: *** the probed registers held the values they had at
        // THIS SLOT'S OWN CLOSE, across the dwell ending here. A slot that finished early therefore gets
        // MORE quiet time than the dwell it asked for, not less — and a value that moved and moved back
        // within it reads as unchanged. Both are properties of a two-point comparison and are why the
        // report says which registers were compared and over how many scans.
        var settled = new List<(int SlotIndex, SlotRunResult Result)>();

        foreach (var (slotIndex, result) in done)
        {
            if (result.Outcome != SlotOutcome.Completed
                || !closes.TryGetValue(slotIndex, out var close)
                || close.Probe is null)
            {
                settled.Add((slotIndex, result));
                continue;
            }

            var sample = SlotRun.Dwell(client, slotIndex, close.Probe, close.AtClose, close.At);
            settled.Add((slotIndex, result with
            {
                Settling = sample,
                Detail = sample is null ? result.Detail : result.Detail + " " + sample.Detail,
            }));
        }

        return settled.OrderBy(d => d.SlotIndex).ToArray();
    }
}
