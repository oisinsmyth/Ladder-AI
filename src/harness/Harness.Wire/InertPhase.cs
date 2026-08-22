using Harness.Map;

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
/// <param name="ExcludedResults">
/// 🔴 <b>Registers this declaration positively claims have NO meaningful resting value, index → why.</b>
///
/// <para>*** THIS IS NOT THE SAME AS BEING ABSENT FROM <paramref name="ExpectedResults"/>, AND THE
/// DIFFERENCE IS THE POINT. *** Absent used to mean "not checked", silently, which is the sentence this
/// type opens with running backwards. A register is now either expected, or excluded WITH A REASON, or the
/// declaration does not cover the band and <see cref="InertOutcome.RestNotDeclared"/> refuses.</para>
///
/// <para><b>An excluded register is skipped by BOTH checks, and the second is the one that is easy to
/// forget.</b> A one-scan pulse reads 1 in about one sample of five: it fails check one at random, and
/// when it does pass check one it then moves between the two observations and fails check two. Excluding
/// it from the value check alone would leave it failing the quiescence check for the same reason.</para>
/// </param>
/// <param name="DefaultedResults">
/// 🔴 <b>Registers whose expectation NOBODY STATED — filled in by a named migration claim rather than
/// declared.</b>
///
/// <para><b>Carried so that a default is never indistinguishable from a declaration.</b> When check one
/// fires on one of these, the failure says so in its own text: the disagreement may be the block, and it
/// may equally be an expectation somebody assumed. That distinction is unavailable to a reader who is
/// handed a bare "R003 reads 65535, declared 0".</para>
/// </param>
/// <remarks>
/// <b>There is deliberately no vector here.</b> D33 consequence 1 makes inert "computed per boundary
/// from tensor[k+1]'s vectors" — the values that establish the start state ARE the next test's values,
/// written while nothing is running. Carrying a second copy on this record would create two places that
/// could disagree about one thing, and the disagreement would present as a test that started from a
/// state nobody declared.
/// </remarks>
/// <summary>
/// <b>Hold the inert phase until the block's own clock is at a known point, instead of guessing where
/// it is.</b>
///
/// <para>🔴 <b>What this exists to remove, measured.</b> A block's movement windows are TUMBLING: the
/// reference re-captures at every expiry, and when a window arms depends on how the previous index left
/// the plant. A vector that does not know the arm instant has to place its stimulus late enough to land
/// inside a live window under BOTH the earliest and latest possible arm — and on the one wave that has
/// run end to end, <b>half of the dominant index was that hedge rather than behaviour under test</b>.
/// Its author wrote it down: <i>"THIS IS WHY THE INDEX IS SIX MINUTES: robustness to an unknown warm-up
/// state, not generosity."</i></para>
///
/// <para><b>It is a measurement replacing a guess, so it makes the test stronger and not merely
/// faster.</b> The failure it removes is worse than the time it costs: a step landing outside a live
/// window yields a verdict that never becomes conclusive, <i>"which reads as a clean pass having tested
/// nothing."</i></para>
/// </summary>
/// <param name="Register">The result register to watch, resolved from the signal by the map.</param>
/// <param name="Signal">The signal's name, carried for the report — a register number alone is unreadable.</param>
/// <param name="Threshold">
/// Required by <see cref="PhaseTrigger.Below"/> and <see cref="PhaseTrigger.AtOrAbove"/>, and refused
/// with <see cref="PhaseTrigger.Decreases"/>, which compares against the previous sample and not a number.
/// </param>
/// <param name="GuardRegister">
/// 🔴 <b>Optional, and REQUIRED IN PRACTICE FOR ANY CLOCK THAT IS NOT FREE-RUNNING.</b> A register that
/// must read non-zero for a sample to count at all.
///
/// <para><b>The false positive it closes, found on a real block.</b> An arm-gated timer reads <b>0 while
/// disarmed</b> — and 0 is exactly what "the window just restarted" looks like. So
/// <see cref="PhaseTrigger.Below"/> on such a clock is satisfied on the first poll, every time,
/// <i>reporting that a window just began when no window is running at all.</i> <see cref="PhaseTrigger.Decreases"/>
/// is worse: the arm→disarm edge takes the value from mid-ramp back to 0, which reads as a wrap.</para>
///
/// <para><b>While the guard is low, samples are DISCARDED rather than merely unmatched</b> — including
/// the remembered previous value. Keeping it would let a comparison straddle a disarm, which is the
/// spurious-wrap case above.</para>
/// </param>
/// <param name="GuardSignal">The guard's name, for the report.</param>
/// <param name="Width">
/// 🔴 <b>REGISTERS THE SIGNAL OCCUPIES — 1 for a Bool or Int, 2 for a Time, AND GETTING THIS WRONG IS
/// SILENT.</b>
///
/// <para>The map is HIGH-WORD-FIRST, and <c>ResultRegisterOf</c> returns a signal's FIRST register. So
/// for a <c>Time</c> the first register is the HIGH word, and any elapsed value under 65.536 s leaves it
/// at <b>0 permanently</b> while every millisecond lands in the second. A one-register
/// <see cref="PhaseTrigger.Below"/> on such a signal is therefore satisfied on the FIRST POLL, ALWAYS —
/// it reports a freshly-restarted window on every wave, including waves where no window is running.</para>
///
/// <para>This is not hypothetical: the same high-word blindness is already recorded against a mirrored
/// timer elsewhere in this system, where a declaration written against the high word alone could not see
/// the signal move at all. So the value is assembled across the full width before anything is compared.</para>
/// </param>
public sealed record PhaseCondition(
    int Register,
    string Signal,
    PhaseTrigger Trigger,
    uint? Threshold = null,
    int? GuardRegister = null,
    string? GuardSignal = null,
    int Width = 1,
    RegisterWordOrder WordOrder = RegisterWordOrder.HighWordFirst)
{
    public int Register { get; } = Register >= 0
        ? Register
        : throw new ArgumentOutOfRangeException(nameof(Register), Register, "a phase condition watches a register in the slot's own result band.");

    public PhaseTrigger Trigger { get; } = Trigger != PhaseTrigger.Unstated
        ? Trigger
        : throw new ArgumentOutOfRangeException(nameof(Trigger), "a phase condition with no trigger states nothing. Unstated is deliberately not a usable value.");

    public int Width { get; } = Width is 1 or 2
        ? Width
        : throw new ArgumentOutOfRangeException(nameof(Width), Width, "a mirrored element occupies one register (Bool, Int) or two (Time). Anything else is not a width this map produces.");

    /// <summary>The signal's value at this poll, assembled across its full width. Never one word of two.</summary>
    public uint ValueIn(ushort[] registers) => Width == 2
        ? RegisterWords.To32(registers[Register], registers[Register + 1], WordOrder)
        : registers[Register];

    /// <summary>True when the whole signal, not just its first register, lies inside the slot's band.</summary>
    public bool FitsWithin(int bandLength) => Register + Width <= bandLength;

    public uint? Threshold { get; } = Trigger switch
    {
        PhaseTrigger.Decreases when Threshold is not null =>
            throw new ArgumentException("Decreases compares against the PREVIOUS sample, not against a number. A threshold here would be silently ignored.", nameof(Threshold)),
        PhaseTrigger.Below or PhaseTrigger.AtOrAbove when Threshold is null =>
            throw new ArgumentException($"{Trigger} needs a threshold to compare against.", nameof(Threshold)),
        _ => Threshold,
    };

    /// <summary>
    /// Whether this poll satisfies the condition. <paramref name="hasPrevious"/> is false on the first
    /// poll, which <see cref="PhaseTrigger.Decreases"/> can never satisfy.
    /// </summary>
    public bool IsMet(uint value, bool hasPrevious, uint previous) => Trigger switch
    {
        PhaseTrigger.Decreases => hasPrevious && value < previous,
        PhaseTrigger.Below => value < Threshold!.Value,
        PhaseTrigger.AtOrAbove => value >= Threshold!.Value,
        _ => false,
    };

    public override string ToString()
    {
        var core = Trigger == PhaseTrigger.Decreases
            ? $"{Signal} (R{Register:000}) decreases"
            : $"{Signal} (R{Register:000}) {Trigger} {Threshold}";

        return GuardRegister is { } guard
            ? $"{core}, while {GuardSignal} (R{guard:000}) is set"
            : core;
    }
}

public sealed record InertDeclaration(
    IReadOnlyDictionary<int, ushort> ExpectedResults,
    int QuiescenceScans = 1,
    IReadOnlyDictionary<int, string>? ExcludedResults = null,
    IReadOnlySet<int>? DefaultedResults = null,

    /// <summary>
    /// Optional: hold the inert phase until the block's own clock is at a known point before committing.
    /// <b>Absent means the vector accepts an unknown phase</b> — which is legitimate for a test with no
    /// windowed behaviour, and expensive for one with it.
    /// </summary>
    PhaseCondition? Phase = null)
{
    /// <summary>Excluded registers with their stated reasons. Never null — an absent map is an empty one.</summary>
    public IReadOnlyDictionary<int, string> Excluded =>
        ExcludedResults ?? new Dictionary<int, string>();

    /// <summary>Registers whose expectation was defaulted rather than declared. Never null.</summary>
    public IReadOnlySet<int> Defaulted =>
        DefaultedResults ?? new HashSet<int>();

    /// <summary>
    /// True when this declaration says SOMETHING about the register — a value or an exclusion.
    /// <b>Silence is not one of the answers</b>, which is what <see cref="InertOutcome.RestNotDeclared"/>
    /// is for.
    /// </summary>
    public bool Covers(int register) => ExpectedResults.ContainsKey(register) || Excluded.ContainsKey(register);
}

/// <summary>One slot's participation in an inert phase: which slot, what values, and what inert means for it.</summary>
public sealed record SlotInert(int SlotIndex, ushort[] Vector, InertDeclaration Declaration);

/// <summary>Why inert was or was not established. Never a bool on its own.</summary>
public enum InertOutcome
{
    /// <summary>Both checks held.</summary>
    Established,

    /// <summary>Check one: the start conditions are not what the declaration says they must be.</summary>
    StartConditionsWrong,

    /// <summary>
    /// 🔴 <b>A register in the slot's result band is neither expected nor excluded — the declaration does
    /// not cover what is about to be checked.</b>
    ///
    /// <para><b>Deliberately NOT <see cref="Established"/>, and deliberately not
    /// <see cref="StartConditionsWrong"/>.</b> The values may be perfect; what is missing is anybody's
    /// statement of what they should be. This is the outcome that stops <i>"a check with no expectation
    /// passes over anything"</i> from being the thing this class does — <b>an unchecked register looks
    /// exactly like a quiet one, and empty is not clean.</b></para>
    /// </summary>
    RestNotDeclared,

    /// <summary>Check two: the values are right and still moving. A model integrating toward a value is not AT it.</summary>
    NotQuiescent,

    /// <summary>The scan counter did not advance far enough to make either check meaningful.</summary>
    ScanCounterStalled,

    /// <summary>
    /// The scan counter moved BACKWARDS by more than a wrap explains — a CPU restart, a reload, or a
    /// different program. <b>Deliberately not <see cref="ScanCounterStalled"/>:</b> the counter did move,
    /// and reading a backwards step as a stall sends a reader to the wrong place. Reading it as an
    /// ADVANCE would be worse still, because the modular difference makes it a very large positive.
    /// </summary>
    ScanCounterWentBackwards,

    /// <summary>
    /// A <see cref="PhaseCondition"/> was declared and did not occur inside the poll budget.
    ///
    /// <para><b>Deliberately NOT <see cref="Established"/> with a note.</b> The whole point of declaring
    /// a phase is that the vector's stimulus timing is written against it; committing anyway would run
    /// the test from the unknown phase the declaration exists to eliminate, and the result would look
    /// exactly like one taken from the right phase. It is also not
    /// <see cref="ScanCounterStalled"/> — the counter may be advancing perfectly well while the watched
    /// quantity simply never wraps.</para>
    /// </summary>
    PhaseNotReached,

    /// <summary>
    /// The link went away before the inert phase could conclude. <b>Not a statement about the device's
    /// state</b> — the phase was never completed, so nothing here says whether the slot was quiescent.
    /// Distinct from every value above, each of which is a MEASUREMENT that came back wrong.
    /// </summary>
    LinkLost,
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
    ScanCount ScanAtVerify,
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
    /// <summary>
    /// 🔴 <b>THE POLLS EITHER WAIT MAY SPEND, AS A NAMED CONSTANT RATHER THAN A LITERAL AT THREE CALL
    /// SITES.</b>
    ///
    /// <para><b>It is the denominator <c>InertRestPlan</c> checks a declared quiescence against.</b> The
    /// wait is declared in SCANS and paid in POLLS, and the worst case is one scan observed per poll — so
    /// a quiescence larger than this cannot be observed within the budget under every scan-time /
    /// round-trip ratio. A literal on both sides is how the plan's refusal and the loop's actual bound
    /// would come to be different numbers, which is a shape this codebase records four times.</para>
    ///
    /// <para><b>200 is unchanged from the literal it replaces</b>, so nothing about today's runs moves.
    /// Note what it buys and what it does not: at the measured 63–106 ms round trip it is 12.6–21 s of
    /// wall clock, so a wait near 9.5 s has only about a quarter of the budget to spare in the worst case.
    /// Raising it is a decision about how long a wave may sit on a wedged rig, and is deliberately taken
    /// deliberately.</para>
    /// </summary>
    public const int PollBudget = 200;

    /// <summary>Establish inert for one slot and verify it, both checks.</summary>
    /// <param name="maxPolls">
    /// Bound on scan-counter reads while waiting, so a stopped PLC ends the phase rather than hanging it.
    /// </param>
    /// <param name="vector">
    /// The next test's values — written HERE, while nothing is running, because they are what establishes
    /// its start condition.
    /// </param>
    public static InertReport Establish(MirrorClient client, int slotIndex, ushort[] vector, InertDeclaration declaration, int maxPolls = PollBudget) =>
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
    public static InertReport Establish(MirrorClient client, IReadOnlyList<SlotInert> active, int maxPolls = PollBudget)
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

            // 🔴 *** A WINDOW THIS BUDGET CANNOT OBSERVE IS REFUSED BY NAME, NOT LEFT TO STALL. *** The
            // wait is declared in SCANS and paid in POLLS, and the worst case is one scan observed per
            // poll — so beyond the budget the loop below runs out and returns ScanCounterStalled, whose
            // text says "The PLC may be stopped". That sends a reader to the controller for a number
            // somebody wrote in a binding document. `InertRestPlan` refuses this before anything is
            // deployed; this is the backstop for a direct caller, and it names BOTH numbers.
            if (slot.Declaration.QuiescenceScans > maxPolls)
            {
                throw new ArgumentOutOfRangeException(nameof(active), slot.Declaration.QuiescenceScans,
                    $"slot {slot.SlotIndex} declares a quiescence of {slot.Declaration.QuiescenceScans} scan(s) and the poll budget is {maxPolls} poll(s). "
                    + "One poll observes at least one scan and never more than the link supplies, so this window cannot be observed within the budget. "
                    + "Left to run it reports ScanCounterStalled, which blames the CPU for a declared number.");
            }
        }

        // -----------------------------------------------------------------------------------------
        // 🔴 THE PER-INDEX WRITE ORDER. Four writes, in this order, and each one is on the side of the
        // commit it is on for a stated reason. `InertPhaseTests` pins the order; do not reorder them.
        //
        //   1. LOWER the start bools      — here, because it is the reset LEVEL and everything below
        //                                   must be observed with the block under test held reset.
        //   2. CLEAR the echo latches     — here, and NEVER after the commit. See below.
        //   3. WRITE the vector           — here, because the next test's values ARE its start condition
        //                                   (D33 consequence 1) and they must be in place before the
        //                                   quiescence window that decides whether it is settled.
        //   4. RAISE the start bools      — `Commit`, on a LATER scan than the verify (D37).
        //
        // 🔴 *** WHY 2 IS BEFORE 4 AND CAN NEVER MOVE AFTER IT. *** The echo is a SET coil on the
        // block's own start condition and NOTHING IN THE PROGRAM CLEARS IT — this client is the only
        // thing that ever does. Clearing it after the commit would wipe a latch the commit had just set,
        // and the wave would then read "the block never saw its start condition" for a block that ran
        // perfectly. That failure is invisible in every artifact this system produces: it is
        // byte-identical to a start bit that never reached the controller.
        //
        // *** AND IT IS A RACE, NOT A CONSTANT ERROR, WHICH IS WHY IT WOULD LOOK INTERMITTENT. *** The
        // controller scans in ~2-25 ms and a Modbus round trip to the rig measures 63-106 ms, so the
        // client is 3-40x slower than the thing it is handshaking with. The latch is the only mechanism
        // bridging that gap; on the wrong side of the commit it destroys it for as long as the copy layer
        // takes to re-set the bit, which is a window nothing here can bound.
        //
        // Writes 1 and 2 are separate transactions ON PURPOSE and in this order: the copy layer's SET
        // coil re-fires for as long as the start condition is high, so clearing the echo while the start
        // bools were still raised would clear a latch the very next scan re-sets. Write 1's response has
        // returned before write 2 is issued — one round trip is many scans — so the reset is established
        // on the device before the release is asked for.
        // -----------------------------------------------------------------------------------------

        // 1. The reset, as a LEVEL held for the whole inert period — not an edge and not a pulse. ONE
        // write, covering every slot: a null slot's bool goes low here too, which is the whole of D26a
        // rule 2.
        client.LowerAllStartBools();

        // 2. D33: latches are released, and the release must COMPLETE before the first scan of the test.
        // The echo is a latch, so it is cleared here rather than after the commit.
        client.ClearStartEcho();

        // 3. The values that establish the NEXT test's start condition (D33 consequence 1).
        foreach (var slot in active)
            client.WriteVector(slot.SlotIndex, slot.Vector);

        var quiescence = active.Max(s => s.Declaration.QuiescenceScans);

        // Let the program act on them before asking whether it has.
        var start = client.ReadControl().ScanCounter;
        if (!WaitScans(client, start, quiescence, maxPolls, out _, out var backwards))
            return backwards ? WentBackwards() : Stalled(quiescence);

        // CHECK ONE — start conditions established, on every active slot. ONE read per group of whole
        // slots (X-A as amended by F-1): the inert phase observes every active slot twice, so reading
        // them one at a time would cost 2K round trips per index where 2 x ceil(K/R) will do.
        var first = client.ReadResults(active.Select(s => s.SlotIndex));

        // *** A DECLARATION ABOUT A DIFFERENT BAND, FIRST. *** An entry naming a register the slot does not
        // have says the declaration was written against something other than this slot, and that is a more
        // specific finding than the coverage one below — which such a declaration would also trip.
        var outOfBand = active.SelectMany(s =>
            s.Declaration.ExpectedResults.Keys.Concat(s.Declaration.Excluded.Keys)
                .Where(r => r >= first[s.SlotIndex].Length)
                .Select(r => $"slot {s.SlotIndex} R{r:000} was declared but the slot has only {first[s.SlotIndex].Length} result register(s)"))
            .ToArray();

        if (outOfBand.Length > 0)
        {
            return new InertReport(InertOutcome.StartConditionsWrong, start, Flatten(active, first), Array.Empty<ushort>(),
                "the next test's start conditions are not established: " + string.Join("; ", outOfBand));
        }

        // *** COVERAGE, AGAINST THE BAND THAT WAS ACTUALLY READ. *** This is the check that makes the
        // sentence at the top of this file true. Without it a declaration mentioning one register of
        // twenty-three passes over the other twenty-two and reports Established — and there is no outward
        // difference between a register that was quiet and one nobody looked at.
        var undeclared = active.SelectMany(s =>
            Enumerable.Range(0, first[s.SlotIndex].Length)
                .Where(r => !s.Declaration.Covers(r))
                .Select(r => $"slot {s.SlotIndex} R{r:000}"))
            .ToArray();

        if (undeclared.Length > 0)
        {
            return new InertReport(InertOutcome.RestNotDeclared, start, Flatten(active, first), Array.Empty<ushort>(),
                $"{undeclared.Length} result register(s) are neither expected nor excluded, so inert was NOT established: "
                + string.Join(", ", undeclared)
                + ". *** A CHECK WITH NO EXPECTATION PASSES OVER ANYTHING. *** An undeclared register cannot be told from a "
                + "quiet one, so this refuses rather than reporting a start state it did not examine. Declare each signal's "
                + "resting value, or EXCLUDE it with a reason.");
        }

        var wrong = active.SelectMany(s => s.Declaration.ExpectedResults
            .Where(e => first[s.SlotIndex][e.Key] != e.Value)
            .Select(e => $"slot {s.SlotIndex} R{e.Key:000} reads {first[s.SlotIndex][e.Key]}, declared {e.Value}"
                // *** A DEFAULTED EXPECTATION IS NAMED AS ONE, IN THE FAILURE ITSELF. *** Handed a bare
                // "reads 65535, declared 0" a reader investigates the block. Told the 0 was assumed rather
                // than stated, they can weigh the other half — and the other half is where this defect was.
                + (s.Declaration.Defaulted.Contains(e.Key)
                    ? " — BUT NOBODY DECLARED THIS REGISTER'S RESTING VALUE: the 0 was DEFAULTED under the slot's assumedZeroRest "
                      + "claim, so the disagreement may be the expectation rather than the program"
                    : string.Empty)))
            .ToArray();

        if (wrong.Length > 0)
        {
            return new InertReport(InertOutcome.StartConditionsWrong, start, Flatten(active, first), Array.Empty<ushort>(),
                "the next test's start conditions are not established: " + string.Join("; ", wrong));
        }

        // CHECK TWO — dynamics quiescent. The values are right; are they STILL right, and unchanged?
        var afterFirst = client.ReadControl().ScanCounter;
        if (!WaitScans(client, afterFirst, quiescence, maxPolls, out var scanAtSecondRead, out var backwardsAgain))
            return backwardsAgain ? WentBackwards() : Stalled(quiescence);

        var second = client.ReadResults(active.Select(s => s.SlotIndex));

        // *** AN EXCLUDED REGISTER IS SKIPPED BY THIS CHECK TOO, AND THAT IS NOT AN OVERSIGHT WORTH
        // "TIGHTENING". *** The motivating exclusion is a ONE-SCAN PULSE: it fails check one at random, and
        // on the runs where it happens to pass check one it then moves between the two observations and
        // fails HERE for the same reason. Excluding it from the value check alone would leave the coin toss
        // in place one check further down — and it would get blamed on the block, which is the whole shape
        // of the defect this declaration exists to remove.
        var moving = active.SelectMany(s =>
            Enumerable.Range(0, Math.Min(first[s.SlotIndex].Length, second[s.SlotIndex].Length))
                .Where(i => !s.Declaration.Excluded.ContainsKey(i))
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

        // ---- PHASE ALIGNMENT, HERE AND NOWHERE EARLIER -----------------------------------------------
        //
        // After both inert checks, so the phase is observed on a state already verified as the next
        // test's start conditions; and immediately before returning, so `Commit` follows as closely as
        // the link allows. Every scan between observing the phase and raising the start bool is a scan of
        // the window that the vector will not get, so this is the last possible moment.
        var phased = active.Where(s => s.Declaration.Phase is not null).ToArray();

        if (phased.Length == 0)
        {
            return new InertReport(InertOutcome.Established, scanAtVerify, Flatten(active, first), Flatten(active, second),
                $"start conditions established and unchanged over {quiescence} scan(s) on {active.Count} slot(s). "
                + Denominator(active)
                // Said on the passing run: an absent phase declaration is a CHOICE with a cost, and one
                // that is invisible in the artifact is one nobody revisits when a wave takes six minutes.
                + " NO PHASE CONDITION DECLARED: the test starts from wherever the block's own clocks happen to be, "
                + "so any vector whose timing depends on a window must be written to survive every arm instant.");
        }

        if (!AwaitPhase(client, phased, maxPolls, scanAtVerify, out var phaseDetail, out var scanAtPhase))
        {
            return new InertReport(InertOutcome.PhaseNotReached, scanAtPhase, Flatten(active, first), Flatten(active, second),
                phaseDetail);
        }

        return new InertReport(InertOutcome.Established, scanAtPhase, Flatten(active, first), Flatten(active, second),
            $"start conditions established and unchanged over {quiescence} scan(s) on {active.Count} slot(s). "
            + Denominator(active) + " " + phaseDetail);
    }

    /// <summary>
    /// Poll until every declared phase condition has been observed, or the budget runs out.
    ///
    /// <para><b>Each slot latches the moment ITS condition is first met</b>, and the loop ends when all
    /// have. ⚠️ <b>The SPREAD between the first and last is reported and NOT gated</b>, and it matters:
    /// where two slots latch scans apart, only the LAST one is genuinely fresh at the commit and the
    /// earlier one has already burned that many scans of its window. Gating it would be a new judgement
    /// about how much staleness is tolerable, which nothing here is in a position to make — but a spread
    /// nobody printed is a spread nobody knows about.</para>
    /// </summary>
    private static bool AwaitPhase(
        MirrorClient client,
        IReadOnlyList<SlotInert> phased,
        int maxPolls,
        ScanCount from,
        out string detail,
        out ScanCount reached)
    {
        var previous = new Dictionary<int, uint>();
        var metAt = new Dictionary<int, long>();

        // Slots whose guard was low on the LAST poll. Kept so the timeout can say "the clock never
        // armed" rather than "the phase never occurred" — different problems, different places to look.
        var guardLow = new HashSet<int>();
        var polls = 0;
        reached = from;

        while (polls < maxPolls)
        {
            var control = client.ReadControl();
            reached = control.ScanCounter;
            polls++;

            var observed = client.ReadResults(phased.Select(s => s.SlotIndex));

            // Whether the guard was low is a property of THIS poll, not of the run so far.
            guardLow.Clear();

            foreach (var slot in phased)
            {
                if (metAt.ContainsKey(slot.SlotIndex))
                    continue;

                var phase = slot.Declaration.Phase!;
                var values = observed[slot.SlotIndex];

                // A register outside the slot's own band is a REFUSAL, not a wait that never ends. The
                // budget would eventually expire and report "the phase never occurred", which blames the
                // block for what is a declaration error.
                // The WHOLE signal has to fit, not just its first register — a Time straddling the end of
                // the band would read its high word and index past the array for its low one.
                if (!phase.FitsWithin(values.Length))
                {
                    detail = $"slot {slot.SlotIndex} declares its phase on {phase}, which occupies {phase.Width} register(s) from "
                        + $"R{phase.Register:000}, but that slot publishes only {values.Length}. The signal is outside the band, so the "
                        + "condition could never be observed — a declaration that does not fit the map, not a block that never reached its phase.";
                    return false;
                }

                // ---- THE GUARD, BEFORE THE SAMPLE IS EVEN REMEMBERED --------------------------------
                if (phase.GuardRegister is { } guardRegister)
                {
                    if (guardRegister >= values.Length)
                    {
                        detail = $"slot {slot.SlotIndex} declares its phase guard on '{phase.GuardSignal}' (R{guardRegister:000}) but that slot "
                            + $"publishes only {values.Length} result register(s). A declaration that does not fit the map, not a block that "
                            + "never reached its phase.";
                        return false;
                    }

                    if (values[guardRegister] == 0)
                    {
                        // *** DISCARDED, NOT MERELY UNMATCHED. *** A remembered value from before a disarm
                        // would let the next comparison straddle it — and the arm→disarm edge takes the
                        // clock from mid-ramp back to 0, which reads as a wrap. That is the spurious phase
                        // this guard exists to prevent, so the memory goes too.
                        previous.Remove(slot.SlotIndex);
                        guardLow.Add(slot.SlotIndex);
                        continue;
                    }
                }

                // Assembled across the FULL width. One word of a two-word Time is the silent always-true.
                var value = phase.ValueIn(values);
                var had = previous.TryGetValue(slot.SlotIndex, out var last);
                previous[slot.SlotIndex] = value;

                if (phase.IsMet(value, had, last))
                    metAt[slot.SlotIndex] = control.ScanCounter.Since(from);
            }

            if (metAt.Count == phased.Count)
            {
                var spread = metAt.Values.Max() - metAt.Values.Min();

                detail = $"PHASE ESTABLISHED for {metAt.Count} slot(s) after {polls} poll round(s): "
                    + string.Join("; ", phased.Select(s => $"slot {s.SlotIndex} {s.Declaration.Phase} at +{metAt[s.SlotIndex]} scan(s)"))
                    + ".";

                if (spread > 0)
                {
                    detail += $" ⚠️ SPREAD {spread} scan(s) between the first and last slot to reach its phase — only the LAST is fresh at the commit, "
                        + "and the others have already spent that many scans of their window. Reported, not gated.";
                }

                return true;
            }
        }

        var waiting = phased.Where(s => !metAt.ContainsKey(s.SlotIndex))
            .Select(s => $"slot {s.SlotIndex} {s.Declaration.Phase}"
                + (guardLow.Contains(s.SlotIndex)
                    ? " — ITS GUARD IS LOW: the clock is not running at all, so this is not a window that failed to wrap, it is a window that never armed"
                    : previous.TryGetValue(s.SlotIndex, out var v) ? $" (last read {v})" : " (never read)"));

        detail = $"the declared phase did not occur within the poll budget of {maxPolls} round(s): still waiting on "
            + string.Join("; ", waiting)
            + ". NOT ESTABLISHED rather than committed anyway: the vector's stimulus timing is written against this phase, "
            + "so starting from an unknown one produces a result indistinguishable from a correct run.";

        return false;
    }

    /// <summary>
    /// 🔴 <b>WHAT THE CHECK ACTUALLY EXAMINED, ON THE PASSING RUN AS WELL AS THE FAILING ONE.</b>
    ///
    /// <para>An <c>Established</c> with no denominator is the shape this project has been bitten by
    /// repeatedly: a green that examined nothing reads exactly like a green that examined everything. So
    /// the pass states its own scope — how many registers were gated, how many of those expectations
    /// nobody actually stated, and how many registers were deliberately not looked at.</para>
    /// </summary>
    private static string Denominator(IReadOnlyList<SlotInert> active)
    {
        var gated = active.Sum(s => s.Declaration.ExpectedResults.Count);
        var excluded = active.Sum(s => s.Declaration.Excluded.Count);
        var defaulted = active.Sum(s => s.Declaration.Defaulted.Count);

        return $"INERT REST: {gated} of {gated + excluded} result register(s) gated"
            + (defaulted > 0
                ? $", OF WHICH {defaulted} DEFAULTED — nobody declared those and they were assumed to rest at 0"
                : string.Empty)
            + (excluded > 0
                ? $"; {excluded} EXCLUDED by declaration, checked by NOBODY."
                : "; 0 EXCLUDED.");
    }

    /// <summary>Observations in slot order, so a single-slot caller sees exactly its own registers.</summary>
    private static ushort[] Flatten(IReadOnlyList<SlotInert> active, IReadOnlyDictionary<int, ushort[]> observations) =>
        active.OrderBy(s => s.SlotIndex).SelectMany(s => observations[s.SlotIndex]).ToArray();

    /// <summary>
    /// D37's commit: raise the start bools on a LATER scan than the verify, in ONE transaction. Returns
    /// the scan counter at the commit — the tests' T=0.
    /// </summary>
    public static ScanCount Commit(MirrorClient client, InertReport verified, IEnumerable<int> slotIndices, int maxPolls = PollBudget)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(verified);

        if (!verified.Established)
        {
            throw new WireException(
                $"refusing to raise the start bools: inert was not established ({verified.Outcome}). {verified.Detail} D34 alternates inert/test unconditionally, but the inert phase is what CONTAINS a bad state — starting from one that was never verified is what that alternation exists to prevent.");
        }

        // 4. A LATER scan than the verify, never the same one — observed, not inferred from the
        // round-trip time. Releasing a reset and starting in one scan makes the outcome depend on rung
        // order inside the block under test.
        //
        // 🔴 *** THIS IS THE LAST WRITE OF THE INDEX. Nothing after it may clear the echo latch *** — see
        // the write-order block in `Establish`. The next release is the NEXT index's inert phase.
        if (!WaitScans(client, verified.ScanAtVerify, 1, maxPolls, out var scanAtCommit, out _))
        {
            throw new WireException(
                $"the scan counter did not advance past the inert verify within {maxPolls} poll(s), so the start bools cannot be raised on a later scan than the verify (D37). The PLC may be stopped.");
        }

        client.Commit(slotIndices);
        return scanAtCommit;
    }

    /// <summary>
    /// The counter moved BACKWARDS by more than a wrap explains. Its own outcome, because "it stalled"
    /// and "it restarted" send a reader to two different places — and because the modular difference
    /// would otherwise have made this look like an enormous ADVANCE, which is the dangerous direction.
    /// </summary>
    private static InertReport WentBackwards() => new(
        InertOutcome.ScanCounterWentBackwards, default, Array.Empty<ushort>(), Array.Empty<ushort>(),
        "the scan counter went BACKWARDS by more than the counter's wrap explains, so no inert check could be made. "
        + "A wrap is absorbed (the difference is modular); this is not one. The realistic causes are a CPU restart, a reload, or a different program running — "
        + "and the version register is the other half of that question.");

    private static InertReport Stalled(int scans) => new(
        InertOutcome.ScanCounterStalled, default, Array.Empty<ushort>(), Array.Empty<ushort>(),
        $"the scan counter did not advance {scans} scan(s), so neither inert check could be made. Empty is not clean: this is not an inert state, it is an unobserved one.");

    /// <summary>
    /// Poll until the counter has advanced <paramref name="scans"/>.
    ///
    /// <para><b>It asks TWO questions, not one.</b> The difference is modular, so it can never be
    /// negative — a counter that went BACKWARDS therefore reads as a very large forward number and would
    /// satisfy any threshold. <see cref="ScanCount.IsPlausibleAdvanceFrom"/> is what separates an advance
    /// from a discontinuity, and without it this loop would report a restarted CPU as instantly quiescent.</para>
    /// </summary>
    private static bool WaitScans(MirrorClient client, ScanCount from, int scans, int maxPolls, out ScanCount reached, out bool wentBackwards)
    {
        reached = from;
        wentBackwards = false;

        for (var poll = 0; poll < maxPolls; poll++)
        {
            reached = client.ReadControl().ScanCounter;

            if (!reached.IsPlausibleAdvanceFrom(from))
            {
                wentBackwards = true;
                return false;
            }

            if (reached.Since(from) >= scans)
                return true;
        }

        return false;
    }
}
