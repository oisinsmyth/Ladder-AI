namespace Harness.Results;

/// <summary>
/// 🔴 <b>WHETHER AN OBSERVATION WAS TAKEN INSIDE THE WINDOW ITS OWN BINDING DECLARED FOR IT.</b>
///
/// <para><b>Nothing in this system asked that question until 2026-08-17, and the answer on the measured
/// run was no.</b> The model publishes its arm flag in the same 23-register read the harness makes every
/// poll; the snapshot every assertion was evaluated against has it reading <b>0</b>. An assertion read
/// outside its own window rendered as a plain <c>Disagreed</c>, which is the false-accusation direction.</para>
/// </summary>
public enum WindowState
{
    /// <summary>
    /// 🔴 <b>NOBODY COULD SAY — and it is the ZERO VALUE, following <c>AssertionForm.Unstated</c>.</b>
    ///
    /// <para>Either the signal's binding declared no arm window, or it declared one whose tag this slot
    /// does not publish. <b>It is NOT "the window was open"</b>: a default that silently meant that would
    /// hand every under-declared signal the permissive reading, which is how the harness came to treat one
    /// arbitrary instant as authoritative in the first place.</para>
    /// </summary>
    Unknown = 0,

    /// <summary>The arm signal read TRUE when this frame was taken. The window was open.</summary>
    InWindow,

    /// <summary>The arm signal read FALSE when this frame was taken. <b>Whatever this frame says is not evidence about the assertion.</b></summary>
    OutOfWindow,
}

/// <summary>Where an observation came from. Two genuinely different instruments, never collapsed.</summary>
public enum ObservationSource
{
    /// <summary>
    /// A sticky bit in the mirror, set by the copy layer inside the armed window and cleared when the slot
    /// is not running an index. <b>The only instrument immune to the tail</b>: it says "this happened at
    /// some point inside the window", which is exactly what survives a late sample.
    /// </summary>
    Latch,

    /// <summary>The retained poll series — every frame this index produced, not just the completing one.</summary>
    Series,
}

/// <summary>
/// 🔴 <b>WHEN AN ASSERTION WAS OBSERVED, AND OUT OF HOW MANY OBSERVATIONS. The H-4 half: a verdict that
/// cannot say when it was taken is a verdict that cannot be argued with.</b>
///
/// <para><b>Every count here is a denominator.</b> "Agreed" alone is the number that looks like evidence;
/// "agreed at 214 of 388 frames considered, out of 402 read, out of 1,978 polls" is the number that can be
/// checked. The measured failure was a package reporting one disagreement as conclusive without anything
/// anywhere recording that the reading was taken 25.2 seconds after the window closed.</para>
/// </summary>
/// <param name="Source">Latch or series. A latch answer needs none of the frame counts and says so by leaving them zero.</param>
/// <param name="FramesRead">Frames in which this signal decoded to a value at all.</param>
/// <param name="FramesConsidered">Frames the verdict was actually taken over — the in-window ones where a window was declared, otherwise all readable ones.</param>
/// <param name="FramesAgreed">Of those, how many matched the expectation.</param>
/// <param name="FramesOutOfWindow">Readable frames excluded because the arm signal read false. <b>Reported, never silently dropped.</b></param>
/// <param name="WindowWasDeclared">Whether the signal's binding named an arm window this slot actually publishes. False means the window state is UNKNOWN, not open.</param>
/// <param name="WindowAtFinalFrame">
/// The window state at the COMPLETING frame — the instant every earlier version of this code used, and
/// reported on every outcome whether or not it decided anything. This is the field that would have made
/// the measured defect visible on the day: <c>OutOfWindow</c> against a confident FAIL.
/// </param>
/// <param name="FirstScan">Scan counter at the first retained frame.</param>
/// <param name="LastScan">Scan counter at the last retained frame — the completion instant.</param>
/// <param name="DecidingScan">Scan counter at the frame the verdict was taken from. For a latch, the frame the latch was read at.</param>
/// <param name="PollsObserved">Poll rounds that read this slot at this index. The outermost denominator.</param>
/// <param name="DistinctFrames">Times the result band's value changed. Compare with <paramref name="RetainedFrames"/>.</param>
/// <param name="RetainedFrames">Frames actually kept. Fewer than <paramref name="DistinctFrames"/> means the cap bit.</param>
/// <param name="SeriesTruncated">Whether later distinct frames were dropped. <b>A truncated series is not a record of the index.</b></param>
public sealed record ObservationWindow(
    ObservationSource Source,
    int FramesRead,
    int FramesConsidered,
    int FramesAgreed,
    int FramesOutOfWindow,
    bool WindowWasDeclared,
    WindowState WindowAtFinalFrame,
    long FirstScan,
    long LastScan,
    long DecidingScan,
    int PollsObserved,
    int DistinctFrames,
    int RetainedFrames,
    bool SeriesTruncated)
{
    /// <summary>The one-line accounting, printed on every outcome rather than only the interesting ones.</summary>
    public string Describe() =>
        Source == ObservationSource.Latch
            ? $"read from the LATCH register at scan {DecidingScan} (a latch reports whether the condition occurred at any point inside the armed window, so it survives a model that recovers to inert before it signals completion)."
            : $"read from the POLL SERIES: {FramesAgreed} of {FramesConsidered} considered frame(s) agreed, "
              + $"{FramesRead} frame(s) decoded, {FramesOutOfWindow} excluded as OUT OF WINDOW, "
              + $"deciding scan {DecidingScan} in [{FirstScan}, {LastScan}]. "
              + $"{PollsObserved} poll round(s), {DistinctFrames} distinct band value(s), {RetainedFrames} retained"
              + (SeriesTruncated ? " — *** SERIES TRUNCATED, so this is not a record of the whole index. ***" : ".")
              + (WindowWasDeclared
                  ? $" Window at the completing frame: {WindowAtFinalFrame}."
                  : " NO ARM WINDOW WAS DECLARED for this signal, so no frame could be excluded and the window state is UNKNOWN rather than open.");
}

/// <summary>One frame of the series, decoded for one signal.</summary>
/// <param name="Scan">The scan counter read in the same poll round.</param>
/// <param name="PollRound">Which poll round produced it.</param>
/// <param name="Value">The decoded value, or <b>null when this signal could not be decoded from this frame</b> — never a zero.</param>
/// <param name="Window">Whether the arm signal was true when this frame was taken.</param>
public sealed record ObservedFrame(long Scan, int PollRound, string? Value, WindowState Window);

/// <summary>
/// 🔴 <b>TURNS A SERIES OF OBSERVATIONS INTO ONE OUTCOME — and its whole design constraint is that it must
/// never produce a FALSE ACCUSATION.</b>
///
/// <para><b>The measured defect (JOB9004, 2026-08-17).</b> The harness kept one observation per index: the
/// poll that recognised completion. A well-built stimulus model returns the block to inert BEFORE it
/// raises its completion flag — that is required, and it is what stops a wave dying after one vector — so
/// the one instant the harness looked at was, by construction, the one instant at which every commanded
/// member is inert. An assertion expecting a commanded state to be TRUE read false and the package reported FAIL
/// against a block proven correct from an earlier frame on the device.</para>
///
/// <para><b>THE FOLD, AND WHY IT IS THIS ONE.</b> The window narrows the set first, then a three-way
/// verdict is taken over what is left:</para>
/// <list type="bullet">
/// <item><b>Every considered frame agreed</b> → <see cref="AssertionState.Held"/>. Decisive.</item>
/// <item><b>No considered frame agreed</b> → <see cref="AssertionState.Disagreed"/>. Decisive, and it is
/// the only shape that may accuse: the block never presented the expected value at ANY instant this
/// harness looked.</item>
/// <item><b>Some did and some did not</b> → <see cref="AssertionState.Inconclusive"/>. <b>Never a
/// Disagreed.</b> The signal took both values inside the considered set and nothing declared which instant
/// is the one that matters, so the honest answer names both facts and the repair.</item>
/// </list>
///
/// <para>⚠️ <b>THE THIRD CASE IS DELIBERATELY WEAKER THAN THE OLD BEHAVIOUR, AND THAT IS THE POINT.</b>
/// Before this, a mixed series was resolved by taking whichever instant the slot happened to finish at —
/// which produced a decisive verdict, and produced the WRONG one. <i>A confident answer obtained by
/// ignoring the disagreeing evidence is worse than an honest refusal to answer</i>, and the refusal names
/// two cheap repairs: declare an <c>armedBy</c> for the signal so the window is in-band, or declare the
/// expectation <c>Latched</c> so the observation survives the tail.</para>
///
/// <para><b>It does not consult <c>AssertionForm</c>, and that is a decision rather than an omission.</b>
/// The tempting rule is <i>mixed + WHEN ⇒ Held, because a WHEN passes on having SEEN the response</i>.
/// Applied to the skeleton's own off-by-one build, whose count ramps THROUGH the expected value on its way
/// to the wrong one, that rule reports HELD for a block the harness exists to catch — and it would pass
/// today only because the poll rate happens to miss the intermediate value, which is a check fitted to the
/// implementation it is checking. <b>The form also arrives from the VECTOR and is <c>Unstated</c> on the
/// deliverable</b>, so keying a verdict on it would key it on a field nobody filled in.</para>
/// </summary>
public static class SeriesEvaluation
{
    /// <summary>
    /// The observed text of an expectation that had a window declared and was never observed inside it.
    /// <b>A distinct token, because "read at the wrong time" and "never read" send a reader to different
    /// documents.</b>
    /// </summary>
    public const string OnlyOutsideTheWindow = "<observed only OUTSIDE the declared window>";

    /// <summary>The observed text of a <c>Latched</c> expectation on a signal for which no latch exists.</summary>
    public const string NoLatch = "<no latch register: a Latched expectation cannot be answered from the value register>";

    /// <summary>Evaluate one expectation over one index's decoded frames.</summary>
    /// <param name="expected">The expectation's predicate, as declared. Never null here — the schema gate refuses an expectation with nothing to compare against.</param>
    /// <param name="frames">Every retained frame, decoded for this signal, in poll order.</param>
    /// <param name="series">The series' own accounting, so the outcome carries its denominators.</param>
    public static AssertionOutcome Evaluate(
        string assertionId,
        string signal,
        string expected,
        IReadOnlyList<ObservedFrame> frames,
        SeriesAccounting series)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(series);

        var readable = frames.Where(f => f.Value is not null).ToArray();

        if (readable.Length == 0)
        {
            // Nothing decoded from any frame. Identical to the pre-series behaviour and it means the same
            // thing: the signal was never read, which is neither a pass nor a failure.
            return AssertionOutcome.Compare(assertionId, signal, expected, null);
        }

        var windowDeclared = readable.Any(f => f.Window != WindowState.Unknown);
        var inWindow = readable.Where(f => f.Window == WindowState.InWindow).ToArray();
        var outOfWindow = readable.Count(f => f.Window == WindowState.OutOfWindow);
        var finalWindow = frames.Count > 0 ? frames[^1].Window : WindowState.Unknown;

        var firstScan = readable[0].Scan;
        var lastScan = readable[^1].Scan;

        ObservationWindow Accounting(ObservationSource source, int considered, int agreed, long deciding) =>
            new(source, readable.Length, considered, agreed, outOfWindow, windowDeclared, finalWindow,
                firstScan, lastScan, deciding,
                series.PollsObserved, series.DistinctFrames, series.RetainedFrames, series.Truncated);

        // *** A DECLARED WINDOW THAT NEVER OPENED IN ANY RETAINED FRAME IS NOT A DISAGREEMENT. *** Every
        // reading we hold is one the binding itself says is outside the window, so none of them is evidence
        // about the assertion — and rendering that as Disagreed is exactly the false accusation.
        if (windowDeclared && inWindow.Length == 0)
        {
            return new AssertionOutcome(assertionId, signal, expected, OnlyOutsideTheWindow, AssertionState.NotObserved)
            {
                Window = Accounting(ObservationSource.Series, 0, 0, lastScan),
                Detail =
                    $"every one of the {readable.Length} readable frame(s) was taken while this signal's declared arm window was CLOSED, "
                    + "so not one of them says anything about the assertion. This is NOT a disagreement and must not be actioned against the block. "
                    + "Either the window never opened during the index, or the frames that fell inside it were not retained.",
            };
        }

        var considered = windowDeclared ? inWindow : readable;
        var agreed = considered.Where(f => string.Equals(f.Value, expected, StringComparison.Ordinal)).ToArray();

        if (agreed.Length == considered.Length)
        {
            return new AssertionOutcome(assertionId, signal, expected, expected, AssertionState.Held)
            {
                Window = Accounting(ObservationSource.Series, considered.Length, agreed.Length, considered[^1].Scan),
                Detail = $"agreed at every one of the {considered.Length} considered frame(s).",
            };
        }

        if (agreed.Length == 0)
        {
            // The only shape that may accuse: the expected value was not present at ANY instant this
            // harness looked inside the window.
            return new AssertionOutcome(assertionId, signal, expected, considered[^1].Value!, AssertionState.Disagreed)
            {
                Window = Accounting(ObservationSource.Series, considered.Length, 0, considered[^1].Scan),
                Detail =
                    $"the expected value was not observed at ANY of the {considered.Length} considered frame(s), "
                    + $"spanning scans {considered[0].Scan} to {considered[^1].Scan}.",
            };
        }

        var firstAgreeing = agreed[0];

        return new AssertionOutcome(assertionId, signal, expected,
            $"{expected} at {agreed.Length} of {considered.Length} observation(s)", AssertionState.Inconclusive)
        {
            Window = Accounting(ObservationSource.Series, considered.Length, agreed.Length, firstAgreeing.Scan),
            Detail =
                $"the signal took the expected value at {agreed.Length} of {considered.Length} considered frame(s) "
                + $"(first at scan {firstAgreeing.Scan}) and a different value at the other {considered.Length - agreed.Length}, "
                + $"including scan {considered[^1].Scan}, the last one observed. "
                + "*** THIS IS NOT A DISAGREEMENT AND MUST NOT BE ACTIONED AGAINST THE BLOCK. *** Nothing declares which instant "
                + "discharges this assertion, and picking one is how a model's deliberate return to inert before it signals "
                + "completion was read as a defect. To make it decidable: declare `armedBy` on this signal in the binding so the "
                + "window is published in-band, or declare the expectation `Latched` so a transient occurrence survives the tail.",
        };
    }

    /// <summary>Evaluate a <c>Latched</c> expectation from its latch — the mode that is immune to the tail.</summary>
    /// <param name="source">
    /// Which kind of latch answered. <b>Reported rather than collapsed</b>: a GENERATED latch is a computed
    /// fact about the artifact this harness emits and can be read out of the generated IR, where a
    /// HAND-AUTHORED one is a claim about a block the harness did not write and cannot inspect.
    /// </param>
    public static AssertionOutcome FromLatch(
        string assertionId, string signal, string expected, string? latchValue, long scan,
        SeriesAccounting series, Harness.Map.LatchSource source)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (latchValue is null)
            return AssertionOutcome.Compare(assertionId, signal, expected, null);

        var agreed = string.Equals(latchValue, expected, StringComparison.Ordinal);

        var window = new ObservationWindow(
            ObservationSource.Latch, 1, 1, agreed ? 1 : 0,
            0, WindowWasDeclared: true, WindowState.InWindow, scan, scan, scan,
            series.PollsObserved, series.DistinctFrames, series.RetainedFrames, series.Truncated);

        return new AssertionOutcome(assertionId, signal, expected, latchValue,
            agreed ? AssertionState.Held : AssertionState.Disagreed)
        {
            Window = window,
            Detail = source == Harness.Map.LatchSource.Generated
                ? "read from the signal's own GENERATED latch register, in the copy layer's latch band. The latch is set inside the "
                  + "armed window and cleared only when the slot stops running an index, so it reports whether the condition occurred "
                  + "AT ANY POINT in the window rather than whether it happened to be true at the instant the harness last looked. "
                  + "This latch is in the IR this harness emitted and can be read out of it."
                : "read from the signal's VALUE register, because the binding declares the latching is done by a block under test "
                  + "rather than by the copy layer — there is no separate latch register for a hand-authored latch. "
                  + "⚠️ THIS IS TAKEN ON TRUST: the harness did not emit that latch and cannot inspect the named block, so a binding "
                  + "naming a block that does not in fact latch this signal would reproduce the late-sample defect here with nothing "
                  + "to say so. Check the named block against the deployed object set.",
        };
    }

    /// <summary>
    /// The refusal for a <c>Latched</c> expectation on a signal that has no latch. <b>It names the signal,
    /// and it never falls back to the value register.</b>
    ///
    /// <para>That fallback is not hypothetical: it is what the code did, for the whole life of the
    /// feature — <c>e.Mode</c> was never read by the code that performs the observation, so <c>Latched</c>
    /// and <c>Sampled</c> were the same thing at the wire. Reinstating it "just when there is no latch"
    /// would recreate exactly that, in the one case where the author has said the value register cannot
    /// answer.</para>
    /// </summary>
    public static AssertionOutcome NoLatchFor(string assertionId, string signal, string expected, SeriesAccounting series)
    {
        ArgumentNullException.ThrowIfNull(series);

        return new AssertionOutcome(assertionId, signal, expected, NoLatch, AssertionState.NotObserved)
        {
            Window = new ObservationWindow(
                ObservationSource.Latch, 0, 0, 0, 0, WindowWasDeclared: false, WindowState.Unknown, 0, 0, 0,
                series.PollsObserved, series.DistinctFrames, series.RetainedFrames, series.Truncated),
            Detail =
                $"'{signal}' is declared `Latched` and the binding claims NO latch for it of either kind — neither a generated one "
                + "(the signal is not `transient`) nor a hand-authored one (no `latchedBy` block is named) — so there is nothing to read. "
                + "*** THE VALUE REGISTER IS NOT A SUBSTITUTE AND IS DELIBERATELY NOT READ: *** an author who declares `Latched` is "
                + "saying the value register cannot answer, and answering from it anyway is how `Latched` became a synonym for "
                + "`Sampled`. The repair is in the BINDING, not the vector: mark the signal `transient` (and `rearmsEachIndex` for a "
                + "per-index window) so the copy layer generates the latch. Gate 5 refuses this before a device is touched — reaching "
                + "it here means the binding changed after admission, or the gate was bypassed.",
        };
    }
}

/// <summary>
/// The series' own denominators, carried onto every outcome derived from it.
///
/// <para>A separate small record rather than a reference to the series itself: the outcome lives in a
/// result package that may outlive the wave, and holding the frames through it would keep every retained
/// frame of every index alive for the life of the report.</para>
/// </summary>
public sealed record SeriesAccounting(int PollsObserved, int DistinctFrames, int RetainedFrames, bool Truncated)
{
    /// <summary>Nothing was observed. <b>A positive value, so a consumer cannot mistake it for an omission.</b></summary>
    public static SeriesAccounting Nothing { get; } = new(0, 0, 0, false);
}
