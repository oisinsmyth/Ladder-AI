namespace Harness.Wire;

/// <summary>
/// What "inert" means for one slot at one wave index, declared rather than inferred.
/// </summary>
/// <param name="ExpectedResults">
/// Result register index → the value it must read once the start condition is established. This is
/// D33's FIRST check, and it must be declared: a check with no expectation passes over anything.
/// </param>
/// <param name="QuiescenceScans">
/// Scans that must elapse between the two observations of D33's SECOND check. At least one, because
/// two reads within one scan cannot distinguish a settled value from a changing one.
/// </param>
/// <remarks>
/// <b>There is deliberately no vector here.</b> D33 consequence 1 makes inert "computed per boundary
/// from tensor[k+1]'s vectors" — the values that establish the start state ARE the next test's values,
/// written while nothing is running. Carrying a second copy on this record would create two places that
/// could disagree about one thing, and the disagreement would present as a test that started from a
/// state nobody declared.
/// </remarks>
public sealed record InertDeclaration(
    IReadOnlyDictionary<int, ushort> ExpectedResults,
    int QuiescenceScans = 1);

/// <summary>One slot's participation in an inert phase: which slot, what values, and what inert means for it.</summary>
public sealed record SlotInert(int SlotIndex, ushort[] Vector, InertDeclaration Declaration);

/// <summary>Why inert was or was not established. Never a bool on its own.</summary>
public enum InertOutcome
{
    /// <summary>Both checks held.</summary>
    Established,

    /// <summary>Check one: the start conditions are not what the declaration says they must be.</summary>
    StartConditionsWrong,

    /// <summary>Check two: the values are right and still moving. A model integrating toward a value is not AT it.</summary>
    NotQuiescent,

    /// <summary>The scan counter did not advance far enough to make either check meaningful.</summary>
    ScanCounterStalled,
}

/// <summary>
/// The outcome of establishing inert, kept SEPARATE from the test's own results.
///
/// <para>D33: "OUTPUTS ARE NOT RECORDED DURING INERT. Establishing a start state moves outputs;
/// recording that would put spurious activity in front of every single test." These observations exist
/// to justify the verdict and are not part of what the test observed.</para>
/// </summary>
public sealed record InertReport(
    InertOutcome Outcome,
    long ScanAtVerify,
    ushort[] FirstObservation,
    ushort[] SecondObservation,
    string Detail)
{
    public bool Established => Outcome == InertOutcome.Established;
}

/// <summary>
/// D33's inert phase and D37's commit, with the two checks and the ordering rule mechanised.
///
/// <para><b>Inert is the START STATE OF THE NEXT TEST, not "everything idle."</b> The next test's start
/// conditions are established, no dynamics are triggered, resets are held asserted as a LEVEL for the
/// whole period, latches are released before the first scan of the test, and nothing is recorded.</para>
///
/// <para><b>Two checks, and the second is the one that earns its place.</b> D33 consequence 3: "a model
/// sitting at the right value while still integrating toward another is not inert, and only the second
/// check catches it." So the value being right once is not the test — it must be right, and then still
/// be the same value a declared number of scans later.</para>
///
/// <para><b>The reset is the start bool held LOW, and it is a level.</b> In the minimal copy layer the
/// block under test clears its own state while its start condition is off, so lowering the start bool
/// IS holding the reset asserted. D37 rules out the tempting alternative — gating the CALL so the block
/// does not run during inert — because a block that is not called never processes its reset and holds
/// whatever its statics contained, which is the opposite of inert.</para>
///
/// <para><b>The commit happens on a LATER SCAN, never the same one</b> (D37), and that is enforced
/// against the observed scan counter rather than assumed from the round-trip time. Releasing a reset and
/// starting in one scan makes the outcome depend on rung order inside the block, which is not something
/// a test should be sensitive to.</para>
/// </summary>
public static class InertPhase
{
    /// <summary>Establish inert for one slot and verify it, both checks.</summary>
    /// <param name="maxPolls">
    /// Bound on scan-counter reads while waiting, so a stopped PLC ends the phase rather than hanging it.
    /// </param>
    /// <param name="vector">
    /// The next test's values — written HERE, while nothing is running, because they are what establishes
    /// its start condition.
    /// </param>
    public static InertReport Establish(MirrorClient client, int slotIndex, ushort[] vector, InertDeclaration declaration, int maxPolls = 200) =>
        Establish(client, new[] { new SlotInert(slotIndex, vector, declaration) }, maxPolls);

    /// <summary>
    /// Establish inert for a WHOLE TENSOR at once, and verify every active slot.
    ///
    /// <para><b>One inert phase covers every test in the next tensor, and D33 consequence 2 says why that
    /// is even well-defined:</b> the tests in a tensor are conflict-free, so their start conditions
    /// CANNOT CONTRADICT. Two tests needing contradictory start states are, by that fact alone, in
    /// different tensors — so conflict-freedom is not only about interference during a test, it is what
    /// makes a shared inert phase constructible at all.</para>
    ///
    /// <para><b>Slots absent from <paramref name="active"/> are NULL at this index</b> (D26a rule 2) —
    /// either their tensor is shorter, or they have already exited. No new encoding: their start bool is
    /// simply not raised, D33's inert holds, and their values are don't-care. They are not verified,
    /// because there is nothing they are being asked to be at.</para>
    /// </summary>
    public static InertReport Establish(MirrorClient client, IReadOnlyList<SlotInert> active, int maxPolls = 200)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(active);

        if (active.Count == 0)
            throw new ArgumentException("an inert phase with no active slot verifies nothing. An index at which every slot is null should not have been run.", nameof(active));

        foreach (var slot in active)
        {
            if (slot.Declaration.QuiescenceScans < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(active), slot.Declaration.QuiescenceScans,
                    "the quiescence check needs at least one scan between its two observations; two reads inside one scan cannot tell a settled value from a changing one.");
            }
        }

        // The reset, as a LEVEL held for the whole inert period — not an edge and not a pulse. ONE write,
        // covering every slot: a null slot's bool goes low here too, which is the whole of D26a rule 2.
        client.LowerAllStartBools();

        // D33: latches are released, and the release must COMPLETE before the first scan of the test.
        // The echo is a latch, so it is cleared here rather than after the commit.
        client.ClearStartEcho();

        // The values that establish the NEXT test's start condition (D33 consequence 1).
        foreach (var slot in active)
            client.WriteVector(slot.SlotIndex, slot.Vector);

        var quiescence = active.Max(s => s.Declaration.QuiescenceScans);

        // Let the program act on them before asking whether it has.
        var start = client.ReadControl().ScanCounter;
        if (!WaitScans(client, start, quiescence, maxPolls, out _))
            return Stalled(quiescence);

        // CHECK ONE — start conditions established, on every active slot.
        var first = active.ToDictionary(s => s.SlotIndex, s => client.ReadResults(s.SlotIndex));

        var wrong = active.SelectMany(s => s.Declaration.ExpectedResults
            .Where(e => e.Key >= first[s.SlotIndex].Length || first[s.SlotIndex][e.Key] != e.Value)
            .Select(e => e.Key < first[s.SlotIndex].Length
                ? $"slot {s.SlotIndex} R{e.Key:000} reads {first[s.SlotIndex][e.Key]}, declared {e.Value}"
                : $"slot {s.SlotIndex} R{e.Key:000} was declared but the slot has only {first[s.SlotIndex].Length} result register(s)"))
            .ToArray();

        if (wrong.Length > 0)
        {
            return new InertReport(InertOutcome.StartConditionsWrong, start, Flatten(active, first), Array.Empty<ushort>(),
                "the next test's start conditions are not established: " + string.Join("; ", wrong));
        }

        // CHECK TWO — dynamics quiescent. The values are right; are they STILL right, and unchanged?
        var afterFirst = client.ReadControl().ScanCounter;
        if (!WaitScans(client, afterFirst, quiescence, maxPolls, out var scanAtSecondRead))
            return Stalled(quiescence);

        var second = active.ToDictionary(s => s.SlotIndex, s => client.ReadResults(s.SlotIndex));

        var moving = active.SelectMany(s =>
            Enumerable.Range(0, Math.Min(first[s.SlotIndex].Length, second[s.SlotIndex].Length))
                .Where(i => first[s.SlotIndex][i] != second[s.SlotIndex][i])
                .Select(i => $"slot {s.SlotIndex} R{i:000} moved {first[s.SlotIndex][i]} -> {second[s.SlotIndex][i]}"))
            .ToArray();

        if (moving.Length > 0)
        {
            return new InertReport(InertOutcome.NotQuiescent, scanAtSecondRead, Flatten(active, first), Flatten(active, second),
                $"the start values are right and still moving over {quiescence} scan(s): " + string.Join("; ", moving)
                + ". A model at the right value while still integrating toward another is not inert.");
        }

        // The scan the verify COMPLETED at, read AFTER the second observation rather than before it.
        // Taken before, "a later scan than the verify" would be satisfied by the round trip that FETCHED
        // the second observation — the commit could then land in the same scan the verify observed, which
        // is precisely the race D37 states the rule against.
        var scanAtVerify = client.ReadControl().ScanCounter;

        return new InertReport(InertOutcome.Established, scanAtVerify, Flatten(active, first), Flatten(active, second),
            $"start conditions established and unchanged over {quiescence} scan(s) on {active.Count} slot(s).");
    }

    /// <summary>Observations in slot order, so a single-slot caller sees exactly its own registers.</summary>
    private static ushort[] Flatten(IReadOnlyList<SlotInert> active, IReadOnlyDictionary<int, ushort[]> observations) =>
        active.OrderBy(s => s.SlotIndex).SelectMany(s => observations[s.SlotIndex]).ToArray();

    /// <summary>
    /// D37's commit: raise the start bools on a LATER scan than the verify, in ONE transaction. Returns
    /// the scan counter at the commit — the tests' T=0.
    /// </summary>
    public static long Commit(MirrorClient client, InertReport verified, IEnumerable<int> slotIndices, int maxPolls = 200)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(verified);

        if (!verified.Established)
        {
            throw new WireException(
                $"refusing to raise the start bools: inert was not established ({verified.Outcome}). {verified.Detail} D34 alternates inert/test unconditionally, but the inert phase is what CONTAINS a bad state — starting from one that was never verified is what that alternation exists to prevent.");
        }

        // A LATER scan than the verify, never the same one — observed, not inferred from the round-trip
        // time. Releasing a reset and starting in one scan makes the outcome depend on rung order inside
        // the block under test.
        if (!WaitScans(client, verified.ScanAtVerify, 1, maxPolls, out var scanAtCommit))
        {
            throw new WireException(
                $"the scan counter did not advance past the inert verify within {maxPolls} poll(s), so the start bools cannot be raised on a later scan than the verify (D37). The PLC may be stopped.");
        }

        client.Commit(slotIndices);
        return scanAtCommit;
    }

    private static InertReport Stalled(int scans) => new(
        InertOutcome.ScanCounterStalled, 0, Array.Empty<ushort>(), Array.Empty<ushort>(),
        $"the scan counter did not advance {scans} scan(s), so neither inert check could be made. Empty is not clean: this is not an inert state, it is an unobserved one.");

    private static bool WaitScans(MirrorClient client, long from, int scans, int maxPolls, out long reached)
    {
        reached = from;

        for (var poll = 0; poll < maxPolls; poll++)
        {
            reached = client.ReadControl().ScanCounter;
            if (reached - from >= scans)
                return true;
        }

        return false;
    }
}
