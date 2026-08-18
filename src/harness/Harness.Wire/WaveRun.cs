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

/// <summary>What a whole wave produced.</summary>
/// <param name="Length">Indices run — MAX tensor length across the slots (D26a), never a colouring decision.</param>
public sealed record WaveResult(
    int Length,
    IReadOnlyList<SlotDistribution> Distributions,
    CoRunningLog Log,
    int RoundTrips)
{
    public SlotDistribution For(int slotIndex) => Distributions.Single(d => d.SlotIndex == slotIndex);
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
    public static WaveResult Run(
        MirrorClient client,
        RuntimeCompression compression,
        IReadOnlyList<SlotTensor> tensors,
        Func<long>? nowMs = null,
        Action<SlotDistribution>? onSlotComplete = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(compression);
        ArgumentNullException.ThrowIfNull(tensors);

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

        for (var index = 0; index < length; index++)
        {
            // Active = the slots that still have a vector at THIS index. The rest are null: not
            // commanded, not verified, values don't-care.
            var active = tensors.Where(t => index < t.Length).ToArray();

            var inert = InertPhase.Establish(client,
                active.Select(t => new SlotInert(t.SlotIndex, t.Vectors[index].Values, t.Vectors[index].Inert)).ToArray());

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
                collected[slotIndex].Add(result);

            // 🔴 *** THE ECHO IS READ HERE, AFTER THE OBSERVATION AND BEFORE THE NEXT INDEX'S INERT PHASE
            // CLEARS IT. *** That ordering is the whole handshake: the latch is set by the copy layer in
            // the same scan the start bit is copied, survives every poll gap (a short test can start and
            // finish between two polls), is read once here, and is released by the NEXT
            // `InertPhase.Establish` — never after a commit. Clearing it on the far side of a commit would
            // wipe a latch that had just been set and would be indistinguishable, in every artifact this
            // system produces, from a block that never started.
            log.Record(index, commanded, client.ReadControl(), client.Map.Slots.Count);

            // D26a rule 3 — a slot exits when its OWN tensor is done, and its results go out THEN.
            foreach (var tensor in tensors.Where(t => t.Length == index + 1).OrderBy(t => t.SlotIndex))
            {
                var distribution = new SlotDistribution(tensor.SlotIndex, index,
                    collected[tensor.SlotIndex], log.SliceFor(tensor.SlotIndex));

                distributions.Add(distribution);
                onSlotComplete?.Invoke(distribution);
            }
        }

        // Any slot the loop never distributed — because inert failed and the wave stopped — still gets
        // its results, marked at the index it reached. Silence would read as "no results yet".
        foreach (var tensor in tensors.Where(t => distributions.All(d => d.SlotIndex != t.SlotIndex)).OrderBy(t => t.SlotIndex))
        {
            distributions.Add(new SlotDistribution(tensor.SlotIndex, collected[tensor.SlotIndex].Count - 1,
                collected[tensor.SlotIndex], log.SliceFor(tensor.SlotIndex)));
        }

        return new WaveResult(length, distributions, log, client.RoundTrips - roundTripsBefore);
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

        return done.OrderBy(d => d.SlotIndex).ToArray();
    }
}
