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
/// <param name="SeriesTruncated">
/// Whether the retention cap bit, so the index is represented by a SAMPLE rather than by every change —
/// see <see cref="RetentionStride"/> for the resolution. <b>It does NOT mean the end of the index was
/// dropped</b> (that was the prefix rule, replaced under D1), and on its own it never told a reader which
/// part of the index a verdict was taken over: it was true on all three of the rows that accused a
/// site's block on 2026-08-17.
/// </param>
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
    /// <summary>
    /// 🔴 <b>THE TEMPORAL SHAPE THE AUTHOR DECLARED for this expectation — i.e. WHICH QUESTION the fold
    /// below was asked.</b>
    ///
    /// <para><b>An init-only property with a default rather than a positional member</b>, deliberately:
    /// every existing construction site keeps compiling and keeps producing
    /// <see cref="TemporalShape.Unstated"/>, which is today's behaviour exactly.</para>
    ///
    /// <para><b>Carried onto the outcome because a verdict that cannot say what question it answered
    /// cannot be argued with</b> — the same reason every count here is a denominator. A <c>Held</c> under
    /// <see cref="TemporalShape.AtSomePoint"/> and a <c>Held</c> under
    /// <see cref="TemporalShape.Throughout"/> are different claims about the block, and a reader who
    /// cannot tell them apart will over-read the weaker one.</para>
    /// </summary>
    public TemporalShape Shape { get; init; } = TemporalShape.Unstated;

    /// <summary>
    /// 🔴 <b>WHICH PART OF THE INDEX THE RETAINED FRAMES REPRESENT: one frame per this many distinct band
    /// changes. THE FIELD <see cref="SeriesTruncated"/> SHOULD HAVE BEEN ALL ALONG.</b>
    ///
    /// <para><c>seriesTruncated: true</c> was on every row of the measured package, including the three
    /// that accused a site's block from frames taken four minutes before the stimulus — because it says
    /// only THAT frames were dropped, and the reader's actual question is WHICH ONES. 1 is "every change";
    /// higher is "a uniform sample at this resolution, spanning the whole index".</para>
    /// </summary>
    public int RetentionStride { get; init; } = 1;

    /// <summary>The one-line accounting, printed on every outcome rather than only the interesting ones.</summary>
    public string Describe() =>
        Source == ObservationSource.Latch
            ? $"read from the LATCH register at scan {DecidingScan} (a latch reports whether the condition occurred at any point inside the armed window, so it survives a model that recovers to inert before it signals completion)."
            : $"read from the POLL SERIES: {FramesAgreed} of {FramesConsidered} considered frame(s) agreed, "
              + $"{FramesRead} frame(s) decoded, {FramesOutOfWindow} excluded as OUT OF WINDOW, "
              + $"deciding scan {DecidingScan} in [{FirstScan}, {LastScan}]. "
              + $"{PollsObserved} poll round(s), {DistinctFrames} distinct band value(s), {RetainedFrames} retained"
              + (SeriesTruncated
                  ? $" — *** SERIES TRUNCATED: the retained frames are a UNIFORM SAMPLE at 1 in {RetentionStride}, "
                    + "spanning the whole index from the first change to the last. What was lost is time RESOLUTION, "
                    + "not a part of the index. ***"
                  : ".")
              + (WindowWasDeclared
                  ? $" Window at the completing frame: {WindowAtFinalFrame}."
                  : " NO ARM WINDOW WAS DECLARED for this signal, so no frame could be excluded and the window state is UNKNOWN rather than open.")
              // *** THE QUESTION THAT WAS ASKED, PRINTED BESIDE THE ANSWER — and nothing at all when the
              // shape is Unstated, because that is the pre-existing behaviour and this line must not
              // change one character of it.
              + (Shape == TemporalShape.Unstated
                  ? string.Empty
                  : $" Declared temporal shape: {Shape}."
                    + (Shape == TemporalShape.AtNoPoint
                        ? " NOTE: on this shape the agreement count is the number of OCCURRENCES OF THE FORBIDDEN VALUE, not agreements."
                        : string.Empty));
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
/// <para><b>The measured defect (JOB9004's first live wave, 2026-08-17).</b> The harness kept one observation per index: the
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
/// <para>🔴 <b>AND SINCE 2026-08-18 THE FOLD ASKS WHICH QUESTION IT IS ANSWERING — <see cref="TemporalShape"/>.</b>
/// The third case above is right and it is also, at plant scale, MOST OF THE RESULTS: four different
/// authors reach it meaning four different things (<i>throughout</i>, <i>at some point</i>, <i>at one
/// instant</i>, <i>becomes and stays</i>), and a "51 of 65" is a FAILURE under the first and a PASS under
/// the second. The shape lets the author say which, so the fold can judge against a stated claim instead
/// of refusing. <b>What is widened is what can be SAID, never what PASSES:</b> an expectation whose shape
/// was never stated is <c>Unstated</c> and comes back exactly as it did before, text included — and the
/// two sharpest shapes may only ACCUSE where an arm window is declared, because otherwise the disagreeing
/// frame may be the model's own required return to inert. Design note:
/// <c>docs/notes/observation-window-shapes.md</c>.</para>
///
/// <para>🔴 <b>AND SINCE 2026-08-18 IT ASKS WHETHER IT MAY CONVICT AT ALL — the truncation-accusation
/// rule (D1).</b> The retained series is bounded, so on a long index it is a SAMPLE. Every accusation this
/// fold makes except one names a frame that was actually observed, and a frame that exists cannot be an
/// artifact of dropping frames. <b>The exception is the total-absence branch</b>, which accuses from what
/// was NOT seen — and that is precisely what a bounded retention can manufacture. Measured: on a
/// 369-second index whose window opened at 313.8 s, the old prefix retention kept nothing inside the phase
/// and three signals came back <c>Disagreed</c>, <b>65 of 65</b>, from frames taken more than four minutes
/// before the stimulus. <b>So an absence may accuse only where the phase under test is demonstrably in
/// what was retained</b> — an arm window declared AND open at some retained frame — otherwise the verdict
/// is <see cref="AssertionState.NotObserved"/> with <see cref="SeriesEvaluation.PhaseNotCovered"/>. An
/// UNTRUNCATED series holds every change, so its absence is real and it accuses exactly as before.</para>
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

    /// <summary>
    /// 🔴 <b>THE OBSERVED TEXT OF AN ACCUSATION WITHHELD BECAUSE THE RETAINED SERIES IS A SAMPLE AND
    /// NOTHING SAYS THE PHASE UNDER TEST IS IN IT.</b> Its own token, because "we looked and it was never
    /// there" and "we cannot show we looked at the right part of the index" send a reader to different
    /// repairs.
    /// </summary>
    public const string PhaseNotCovered =
        "<the retained series is a sample of the index and nothing shows it covers the phase under test>";

    /// <summary>
    /// The observed text of an expectation declared <see cref="TemporalShape.AtNoPoint"/> whose forbidden
    /// value never appeared. <b>A sentence rather than a bare value</b>, because on that shape alone a row
    /// reading <c>expected: "true" … Held</c> would be describing a signal that was never true.
    /// </summary>
    public static string ForbiddenValueAbsent(string expected) =>
        $"<'{expected}' was never observed — which is what this expectation requires>";

    /// <summary>Evaluate one expectation over one index's decoded frames.</summary>
    /// <param name="expected">The expectation's predicate, as declared. Never null here — the schema gate refuses an expectation with nothing to compare against.</param>
    /// <param name="frames">Every retained frame, decoded for this signal, in poll order.</param>
    /// <param name="series">The series' own accounting, so the outcome carries its denominators.</param>
    /// <param name="shape">
    /// 🔴 <b>WHAT THE AUTHOR CLAIMED ABOUT THE SIGNAL IN TIME. <see cref="TemporalShape.Unstated"/> — the
    /// default, and what every vector written before the field existed carries — reproduces the previous
    /// behaviour exactly, including the text.</b>
    ///
    /// <para><b>The parameter is TRAILING AND OPTIONAL on purpose.</b> The runner's call site lives in
    /// another component and is owned by another track; leaving it untouched must be a no-op, and it is.
    /// Passing the expectation's declared shape is a one-argument change there, and nothing else.</para>
    /// </param>
    public static AssertionOutcome Evaluate(
        string assertionId,
        string signal,
        string expected,
        IReadOnlyList<ObservedFrame> frames,
        SeriesAccounting series,
        TemporalShape shape = TemporalShape.Unstated)
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
                series.PollsObserved, series.DistinctFrames, series.RetainedFrames, series.Truncated)
            {
                Shape = shape,
                RetentionStride = series.Stride,
            };

        // 🔴 *** IS THE PHASE UNDER TEST DEMONSTRABLY IN WHAT WE KEPT? *** It is exactly when the binding
        // declared an arm window AND at least one retained frame was taken while that window was open.
        // Anything else is UNKNOWN coverage — and unknown is not "covered", by the same rule that makes
        // WindowState.Unknown not "open". This is the term the accusation rule below turns on.
        var phaseRepresented = windowDeclared && inWindow.Length > 0;

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

        // *** THE FORBIDDEN-VALUE SHAPE INVERTS THE WHOLE FOLD, so it is answered before any of the
        // branches below. *** For it, `agreed` counts OCCURRENCES OF WHAT MUST NOT HAPPEN, and every
        // verdict below would read exactly backwards.
        if (shape == TemporalShape.AtNoPoint)
            return ForbiddenValue(assertionId, signal, expected, considered, agreed, windowDeclared, Accounting);

        if (agreed.Length == considered.Length)
        {
            return new AssertionOutcome(assertionId, signal, expected, expected, AssertionState.Held)
            {
                Window = Accounting(ObservationSource.Series, considered.Length, agreed.Length, considered[^1].Scan),
                Detail = $"agreed at every one of the {considered.Length} considered frame(s)."
                    + WholesaleAgreementClause(shape, considered),
            };
        }

        if (agreed.Length == 0)
        {
            // 🔴 *** THE TRUNCATION-ACCUSATION RULE (D1, 2026-08-18). AN ARGUMENT FROM ABSENCE MAY NOT BE
            // MADE OVER A SAMPLE WHOSE COVERAGE OF THE PHASE IS UNKNOWN. ***
            //
            // This branch is the ONLY verdict in the whole fold that accuses from what was NOT seen; every
            // other accusation names a frame that WAS observed (a counterexample under Throughout, a
            // fall-back under BecomesAndHolds, an occurrence under AtNoPoint, the declared instant under
            // AtEnd), and a frame that exists cannot be an artifact of dropping frames. Absence can be,
            // and on the measured run it was: three Disagreed rows, 65 of 65 considered, every one of
            // those frames taken more than four minutes before the stimulus.
            //
            // Uniform decimation makes this rare rather than routine — the retained frames now span the
            // whole index — but "spread across the index" is not "inside the phase", and the residual
            // case is exactly the one that bit: a window narrower than the sample's resolution. So the
            // rule fails closed, and it is deliberately keyed on `phaseRepresented` rather than on
            // truncation alone: an untruncated series holds EVERY change, so its absence is real.
            //
            // *** THE REPAIR IS ONE FIELD, AND SINCE D2 IT COSTS NOTHING: *** declare `armedBy` on the
            // signal. It no longer requires `transient`, generates no latch and consumes no register when
            // the arm tag is already mirrored in the slot's band.
            if (series.Truncated && !phaseRepresented)
            {
                return new AssertionOutcome(assertionId, signal, expected, PhaseNotCovered, AssertionState.NotObserved)
                {
                    Window = Accounting(ObservationSource.Series, considered.Length, 0, considered[^1].Scan),
                    Detail =
                        $"the expected value was not observed at any of the {considered.Length} considered frame(s) — "
                        + $"BUT THOSE FRAMES ARE A SAMPLE. The band changed {series.DistinctFrames} time(s) and "
                        + $"{series.RetainedFrames} frame(s) were retained, one per {series.Stride} change(s), and "
                        + (windowDeclared
                            ? "although an arm window IS declared for this signal, no retained frame was taken while it was open."
                            : "NO ARM WINDOW IS DECLARED for this signal, so nothing here can say whether the phase under test is "
                              + "represented among the frames that were kept.")
                        + " *** THIS IS NOT A DISAGREEMENT AND MUST NOT BE ACTIONED AGAINST THE BLOCK. *** An accusation from ABSENCE "
                        + "requires that the absence be over the right part of the index, and that cannot be shown here. "
                        + "This exact shape — a long index, a sampled series, and no way to say the window was in it — is how three "
                        + "accusations were built against a site's block on 2026-08-17 from frames taken minutes before the "
                        + "stimulus. THE REPAIR: declare `armedBy` on this signal in the binding so the window travels in-band; it "
                        + "needs no latch and no `transient`, and the verdict then becomes decidable in either direction. Or declare "
                        + "the expectation `Latched`, so an occurrence survives sampling entirely.",
                };
            }

            // The only shape that may accuse: the expected value was not present at ANY instant this
            // harness looked inside the window. *** EVERY SHAPE AGREES ON THIS ONE. *** A universal claim
            // never held, an existential one never occurred, a becomes-and-holds never rose and a
            // single-instant claim disagrees at its instant — so this branch is shape-independent, and
            // the shape widens nothing here.
            return new AssertionOutcome(assertionId, signal, expected, considered[^1].Value!, AssertionState.Disagreed)
            {
                Window = Accounting(ObservationSource.Series, considered.Length, 0, considered[^1].Scan),
                Detail =
                    $"the expected value was not observed at ANY of the {considered.Length} considered frame(s), "
                    + $"spanning scans {considered[0].Scan} to {considered[^1].Scan}."
                    + TotalAbsenceClause(shape),
            };
        }

        // ------------------------------------------------------------------------------------------
        // MIXED. Some frames agreed and some did not — the case that used to have exactly one answer,
        // and the whole reason this parameter exists. What follows judges it against the shape the
        // author DECLARED; where nothing was declared it is untouched, byte for byte.
        // ------------------------------------------------------------------------------------------
        var firstAgreeing = agreed[0];

        return shape switch
        {
            TemporalShape.Throughout =>
                Universal(assertionId, signal, expected, considered, agreed, windowDeclared, Accounting),

            TemporalShape.AtSomePoint =>
                Existential(assertionId, signal, expected, considered, agreed, Accounting),

            TemporalShape.BecomesAndHolds =>
                Rising(assertionId, signal, expected, considered, agreed, windowDeclared, Accounting),

            TemporalShape.AtEnd =>
                Terminal(assertionId, signal, expected, considered, agreed, windowDeclared, Accounting),

            // 🔴 *** UNSTATED: UNCHANGED, DELIBERATELY, AND THIS IS THE BACKWARD-COMPATIBILITY
            // GUARANTEE. *** Every vector written before the shape field existed lands here, including
            // vectors already run against a real job. Same verdict, same observed text, same detail —
            // a silent change here would rewrite the meaning of results already recorded.
            _ => new AssertionOutcome(assertionId, signal, expected,
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
            },
        };
    }

    /// <summary>How the accounting is built, so the shape handlers below need not restate the denominators.</summary>
    private delegate ObservationWindow Accountant(ObservationSource source, int considered, int agreed, long deciding);

    /// <summary>
    /// <b><see cref="TemporalShape.Throughout"/>, mixed.</b> A universal claim contradicted by an observed
    /// frame.
    ///
    /// <para>🔴 <b>THE WINDOW IS WHAT DECIDES WHETHER THIS MAY ACCUSE, and that is the accusation rule
    /// rather than caution.</b> With an arm window declared, the disagreeing frame is KNOWN to be inside
    /// the phase, so the signal demonstrably did not hold throughout it — positive evidence, and the
    /// verdict is a disagreement. Without one, that frame may be a TAIL frame, i.e. the model's own
    /// required return to inert, and accusing on it is the exact false accusation of 2026-08-17 wearing a
    /// declaration. <b>Declaring the shape is necessary and not sufficient; the binding still has to
    /// publish the window.</b></para>
    /// </summary>
    private static AssertionOutcome Universal(
        string assertionId, string signal, string expected,
        ObservedFrame[] considered, ObservedFrame[] agreed, bool windowDeclared, Accountant accounting)
    {
        var offending = considered.First(f => !string.Equals(f.Value, expected, StringComparison.Ordinal));

        if (windowDeclared)
        {
            return new AssertionOutcome(assertionId, signal, expected, offending.Value!, AssertionState.Disagreed)
            {
                Window = accounting(ObservationSource.Series, considered.Length, agreed.Length, offending.Scan),
                Detail =
                    $"declared THROUGHOUT, and it was not. The signal held at {agreed.Length} of {considered.Length} considered "
                    + $"frame(s) but read '{offending.Value}' at scan {offending.Scan}, INSIDE the declared arm window. "
                    + "A universal claim is refuted by one counterexample, and this one is known to be inside the phase rather than "
                    + "in the model's inert tail, because the arm signal read TRUE in the same register read. "
                    + "This IS a statement about the block.",
            };
        }

        return new AssertionOutcome(assertionId, signal, expected,
            $"{expected} at {agreed.Length} of {considered.Length} observation(s)", AssertionState.Inconclusive)
        {
            Window = accounting(ObservationSource.Series, considered.Length, agreed.Length, agreed[0].Scan),
            Detail =
                $"declared THROUGHOUT, and the signal read '{offending.Value}' at scan {offending.Scan} — but NO ARM WINDOW WAS "
                + "DECLARED for it, so nothing says whether that frame is inside the phase or in the model's tail. "
                + "*** THIS IS NOT A DISAGREEMENT AND MUST NOT BE ACTIONED AGAINST THE BLOCK. *** A model is REQUIRED to return the "
                + "block to inert before it signals completion, so the frames after the phase read exactly like a signal that dropped. "
                + "The shape is stated and the interval is not: declare `armedBy` on this signal in the binding and this becomes "
                + "decidable — as a PASS or a FAIL, on the evidence.",
        };
    }

    /// <summary>
    /// <b><see cref="TemporalShape.AtSomePoint"/>, mixed: HELD.</b> The author asserted an occurrence and
    /// the occurrence was observed; the frames that disagree are outside the claim, not against it.
    ///
    /// <para>⚠️ <b>The distinct-value count is printed because of the RAMP.</b> An existential claim on a
    /// value that ramps is discharged by a pass-through — a counter climbing to the wrong value passes
    /// through the right one — so a reader must be able to see, in the row, that the signal took several
    /// values rather than two. <b>Reported, never gated:</b> a ramp is legitimate evidence for an
    /// event-shaped author and a trap for a value-shaped one, and the evaluator cannot tell which from
    /// here.</para>
    /// </summary>
    private static AssertionOutcome Existential(
        string assertionId, string signal, string expected,
        ObservedFrame[] considered, ObservedFrame[] agreed, Accountant accounting)
    {
        var distinct = considered.Select(f => f.Value).Distinct(StringComparer.Ordinal).Count();

        return new AssertionOutcome(assertionId, signal, expected,
            $"{expected} at {agreed.Length} of {considered.Length} observation(s)", AssertionState.Held)
        {
            Window = accounting(ObservationSource.Series, considered.Length, agreed.Length, agreed[0].Scan),
            Detail =
                $"declared AT SOME POINT, and it was: the signal took the expected value at {agreed.Length} of "
                + $"{considered.Length} considered frame(s), first at scan {agreed[0].Scan} and last at scan {agreed[^1].Scan}. "
                + "The frames that read otherwise are outside this claim rather than against it — an existential expectation says "
                + "nothing about when the signal stopped. "
                + (distinct > 2
                    ? $"⚠️ The signal took {distinct} DISTINCT values across the considered frames. If this is a value that RAMPS, "
                      + "an existential expectation is discharged by a pass-through on the way to the wrong value — check that this "
                      + "signal is event-shaped, or restate the expectation as `throughout` or `atEnd`."
                    : $"The signal took {distinct} distinct value(s) across the considered frames."),
        };
    }

    /// <summary>
    /// <b><see cref="TemporalShape.BecomesAndHolds"/>, mixed.</b> The one shape that reads the ORDER of the
    /// frames: a pass iff the agreeing frames are a non-empty SUFFIX of the considered set.
    ///
    /// <para><b>This is the shape that reads a "51 of 65" correctly</b> — a pass when the 51 are the tail, a
    /// defect when they are the head. Reverse the series and the verdict changes, which is precisely what
    /// makes it a distinct shape rather than a tuning of the other two.</para>
    ///
    /// <para><b>A fall-back may accuse only with a declared window</b>, by the same rule as
    /// <see cref="Universal"/>: without one, the frames where the signal went false again may be the
    /// model's own recovery.</para>
    /// </summary>
    private static AssertionOutcome Rising(
        string assertionId, string signal, string expected,
        ObservedFrame[] considered, ObservedFrame[] agreed, bool windowDeclared, Accountant accounting)
    {
        var rose = Array.FindIndex(considered, f => string.Equals(f.Value, expected, StringComparison.Ordinal));
        var heldToTheEnd = considered.Skip(rose)
            .All(f => string.Equals(f.Value, expected, StringComparison.Ordinal));

        if (heldToTheEnd)
        {
            return new AssertionOutcome(assertionId, signal, expected,
                $"{expected} from scan {considered[rose].Scan} onward, at {agreed.Length} of {considered.Length} observation(s)",
                AssertionState.Held)
            {
                Window = accounting(ObservationSource.Series, considered.Length, agreed.Length, considered[rose].Scan),
                Detail =
                    $"declared BECOMES AND HOLDS, and it did: the signal read otherwise for the first {rose} considered frame(s), "
                    + $"took the expected value at scan {considered[rose].Scan}, and held it at every one of the "
                    + $"{agreed.Length} frame(s) from there to scan {considered[^1].Scan}. The agreeing frames are the TAIL of the "
                    + "considered set, which is what this shape asserts. Note that the rising edge itself falls between two polls: "
                    + "what is observed is that the signal was NOT at the expected value and later WAS, not the instant it changed.",
            };
        }

        var fellBack = considered.Skip(rose)
            .First(f => !string.Equals(f.Value, expected, StringComparison.Ordinal));

        if (windowDeclared)
        {
            return new AssertionOutcome(assertionId, signal, expected, fellBack.Value!, AssertionState.Disagreed)
            {
                Window = accounting(ObservationSource.Series, considered.Length, agreed.Length, fellBack.Scan),
                Detail =
                    $"declared BECOMES AND HOLDS, and it did not hold. The signal took the expected value at scan "
                    + $"{considered[rose].Scan} and then read '{fellBack.Value}' again at scan {fellBack.Scan}, INSIDE the declared "
                    + "arm window — so the fall-back is known to be inside the phase rather than in the model's inert tail. "
                    + "This IS a statement about the block.",
            };
        }

        return new AssertionOutcome(assertionId, signal, expected,
            $"{expected} at {agreed.Length} of {considered.Length} observation(s), then not",
            AssertionState.Inconclusive)
        {
            Window = accounting(ObservationSource.Series, considered.Length, agreed.Length, considered[rose].Scan),
            Detail =
                $"declared BECOMES AND HOLDS. The signal rose at scan {considered[rose].Scan} and read '{fellBack.Value}' again at "
                + $"scan {fellBack.Scan} — but NO ARM WINDOW WAS DECLARED for it, so nothing says whether that fall-back is inside "
                + "the phase or is the model's own required return to inert. "
                + "*** THIS IS NOT A DISAGREEMENT AND MUST NOT BE ACTIONED AGAINST THE BLOCK. *** Declare `armedBy` on this signal in "
                + "the binding and this becomes decidable on the evidence.",
        };
    }

    /// <summary>
    /// <b><see cref="TemporalShape.AtEnd"/>, mixed.</b> One frame decides, and the row names it.
    ///
    /// <para>🔴 <b>THIS IS THE PRE-2026-08-17 BEHAVIOUR WITH A DECLARATION ATTACHED, AND THE DIFFERENCE IS
    /// THE WHOLE ARGUMENT FOR ADMITTING IT.</b> The old code took the completing frame for EVERY
    /// expectation, chosen by nobody and recorded nowhere, and produced a confident FAIL against a block
    /// proven correct on the device. Here the author asked for a terminal instant, and the outcome names
    /// which scan it was and what the arm window read there — a verdict taken at an instant the reader can
    /// see is a different object from one taken at an instant nobody knew was being used.</para>
    ///
    /// <para><b>With a window declared the instant is the last IN-WINDOW frame</b> (the considered set is
    /// already narrowed), i.e. the end of the phase. <b>Without one it is the last frame before
    /// completion</b> — the INERT TAIL for exactly the class of well-built model this system requires —
    /// and the detail says so in those words, because an author who meant "at the end of the active phase"
    /// needs `armedBy` first.</para>
    /// </summary>
    private static AssertionOutcome Terminal(
        string assertionId, string signal, string expected,
        ObservedFrame[] considered, ObservedFrame[] agreed, bool windowDeclared, Accountant accounting)
    {
        var last = considered[^1];
        var held = string.Equals(last.Value, expected, StringComparison.Ordinal);

        var whereTheInstantCameFrom = windowDeclared
            ? "the last frame taken while the declared arm window was still OPEN, i.e. the end of the phase."
            : "the last frame observed before completion. *** NO ARM WINDOW WAS DECLARED, so this instant is the model's TAIL: *** "
              + "a well-built stimulus model returns the block to inert BEFORE it raises its completion flag, so a commanded state is "
              + "expected to read inert here. If you meant 'at the end of the ACTIVE phase', declare `armedBy` on this signal in the "
              + "binding first.";

        return new AssertionOutcome(assertionId, signal, expected, last.Value!,
            held ? AssertionState.Held : AssertionState.Disagreed)
        {
            Window = accounting(ObservationSource.Series, considered.Length, agreed.Length, last.Scan),
            Detail =
                $"declared AT END, so exactly one frame decides: scan {last.Scan}, which read '{last.Value}'. That instant is "
                + whereTheInstantCameFrom
                + $" (For context, and NOT part of this verdict: the signal agreed at {agreed.Length} of {considered.Length} "
                + "considered frame(s).)",
        };
    }

    /// <summary>
    /// <b><see cref="TemporalShape.AtNoPoint"/> — the whole fold, inverted.</b> The expectation's value is
    /// the FORBIDDEN one, so an "agreeing" frame is an occurrence of what must not happen.
    ///
    /// <para><b>An occurrence is positive evidence and may accuse; an absence is not and cannot.</b> A pass
    /// here is produced by seeing nothing, which is also what a poll gap produces — the standing weakness of
    /// every NEVER assertion under sampling, bounded by the observability floor rather than by this shape,
    /// and printed on the row so a reader meets it where the verdict is.</para>
    /// </summary>
    private static AssertionOutcome ForbiddenValue(
        string assertionId, string signal, string expected,
        ObservedFrame[] considered, ObservedFrame[] occurrences, bool windowDeclared, Accountant accounting)
    {
        if (occurrences.Length == 0)
        {
            var window = accounting(ObservationSource.Series, considered.Length, 0, considered[^1].Scan);

            return new AssertionOutcome(assertionId, signal, expected, ForbiddenValueAbsent(expected), AssertionState.Held)
            {
                Window = window,
                Detail =
                    $"declared AT NO POINT: '{expected}' is the FORBIDDEN value, and it was not observed at any of the "
                    + $"{considered.Length} considered frame(s), spanning scans {considered[0].Scan} to {considered[^1].Scan}. "
                    + "⚠️ A pass on this shape is produced by SEEING NOTHING, which is also what a poll gap produces — it is only as "
                    + "strong as the observability floor makes it, and it is not proof the value never appeared between polls."
                    // *** THE SAME WEAKNESS THE TRUNCATION-ACCUSATION RULE GATES, POINTING THE OTHER WAY. ***
                    // It is not gated here because this is a PASS, and withholding a pass does not protect a
                    // block from a false accusation — but a pass from absence over a SAMPLE is weaker again,
                    // and a reader must meet that where the verdict is rather than in a retention doc.
                    + (window.SeriesTruncated
                        ? $" ⚠️ AND THE SERIES IS A SAMPLE: one frame per {window.RetentionStride} distinct band change(s). "
                          + "An occurrence standing for fewer changes than that can fall between two retained frames, so this pass is "
                          + "weaker than an untruncated one by exactly that resolution."
                        : string.Empty),
            };
        }

        if (windowDeclared)
        {
            return new AssertionOutcome(assertionId, signal, expected,
                $"'{expected}' OBSERVED at {occurrences.Length} of {considered.Length} observation(s)",
                AssertionState.Disagreed)
            {
                Window = accounting(ObservationSource.Series, considered.Length, occurrences.Length, occurrences[0].Scan),
                Detail =
                    $"declared AT NO POINT, and it happened: the FORBIDDEN value '{expected}' was observed at scan "
                    + $"{occurrences[0].Scan}, INSIDE the declared arm window, and at {occurrences.Length} of "
                    + $"{considered.Length} considered frame(s) in all. An occurrence is positive evidence and this one is known to be "
                    + "inside the phase. This IS a statement about the block.",
            };
        }

        return new AssertionOutcome(assertionId, signal, expected,
            $"'{expected}' OBSERVED at {occurrences.Length} of {considered.Length} observation(s)",
            AssertionState.Inconclusive)
        {
            Window = accounting(ObservationSource.Series, considered.Length, occurrences.Length, occurrences[0].Scan),
            Detail =
                $"declared AT NO POINT, and the FORBIDDEN value '{expected}' WAS observed, first at scan {occurrences[0].Scan} — but "
                + "NO ARM WINDOW WAS DECLARED for this signal, so nothing says whether that occurrence is inside the phase or is a "
                + "transient of the model's own reset. "
                + "*** THIS IS NOT A DISAGREEMENT AND MUST NOT BE ACTIONED AGAINST THE BLOCK. *** Declare `armedBy` on this signal in "
                + "the binding and this becomes decidable on the evidence.",
        };
    }

    /// <summary>
    /// What the shape adds when EVERY considered frame agreed. <b>Empty for
    /// <see cref="TemporalShape.Unstated"/>, so the pre-existing text is unchanged character for
    /// character.</b>
    /// </summary>
    private static string WholesaleAgreementClause(TemporalShape shape, ObservedFrame[] considered) => shape switch
    {
        TemporalShape.Throughout =>
            " Declared THROUGHOUT, and no counterexample was seen — which is all a sampled universal claim can ever mean. "
            + "It is not proof the signal never dropped between two polls.",

        TemporalShape.AtSomePoint =>
            " Declared AT SOME POINT, and it is discharged many times over: the expected value was present at every considered frame.",

        TemporalShape.BecomesAndHolds =>
            $" Declared BECOMES AND HOLDS. The signal was ALREADY at the expected value at the first considered frame (scan "
            + $"{considered[0].Scan}), so NO RISING EDGE WAS OBSERVED — the edge, if any, fell before the first retained frame or "
            + "before the window opened. The 'holds' half is satisfied; the 'becomes' half was not witnessed here.",

        TemporalShape.AtEnd =>
            $" Declared AT END; the deciding instant is scan {considered[^1].Scan}, and it agreed along with every other considered "
            + "frame, so the choice of instant is not load-bearing in this result.",

        _ => string.Empty,
    };

    /// <summary>
    /// What the shape adds when NO considered frame agreed. <b>Empty for
    /// <see cref="TemporalShape.Unstated"/>.</b> Every shape reaches the same verdict here, so these
    /// clauses only say what was refuted.
    /// </summary>
    private static string TotalAbsenceClause(TemporalShape shape) => shape switch
    {
        TemporalShape.Throughout => " Declared THROUGHOUT: it did not hold at any observed instant, let alone all of them.",
        TemporalShape.AtSomePoint =>
            " Declared AT SOME POINT: the occurrence was never seen. This accusation is exactly as strong as it was before shapes "
            + "existed — an event smaller than the poll gap is invisible at any polling rate, which is what the observability floor "
            + "bounds.",
        TemporalShape.BecomesAndHolds => " Declared BECOMES AND HOLDS: the signal never became the expected value at all.",
        TemporalShape.AtEnd => " Declared AT END: the deciding instant disagrees, as does every other considered frame.",
        _ => string.Empty,
    };

    /// <summary>Evaluate a <c>Latched</c> expectation from its latch — the mode that is immune to the tail.</summary>
    /// <param name="source">
    /// Which kind of latch answered. <b>Reported rather than collapsed</b>: a GENERATED latch is a computed
    /// fact about the artifact this harness emits and can be read out of the generated IR, where a
    /// HAND-AUTHORED one is a claim about a block the harness did not write and cannot inspect.
    /// </param>
    /// <param name="shape">
    /// 🔴 <b>THE SHAPE THE AUTHOR DECLARED, AND UNTIL 2026-09-02 THIS METHOD DID NOT TAKE IT.</b>
    ///
    /// <para>It took no shape, compared <c>latchValue == expected</c>, and built its
    /// <see cref="ObservationWindow"/> without setting <see cref="ObservationWindow.Shape"/> — so every
    /// <c>Latched</c> row printed <c>Unstated</c> in the result file no matter what its vector said. On
    /// <see cref="TemporalShape.AtNoPoint"/> the declared value is the FORBIDDEN one, so that comparison
    /// was <b>exactly inverted</b>: a latch reading the forbidden value was reported Held, and a latch
    /// that never saw it was reported Disagreed.</para>
    ///
    /// <para><b>MEASURED, not theorised.</b> A wave on 2026-09-02 reported two <c>Latched</c> +
    /// <c>atNoPoint</c> rows as failures whose latches had read exactly what the assertion required, and
    /// one of them was the wave's strongest positive result — a seal-in correctly breaking. The blind
    /// reviewer who found it put it as "the vector is right, the block is right, the evaluator is
    /// wrong".</para>
    ///
    /// <para><b>Only <see cref="TemporalShape.AtNoPoint"/> changes behaviour.</b> Every other shape,
    /// including <see cref="TemporalShape.Unstated"/>, keeps the equality fold it has always had — the
    /// inversion is the one place the series path also treats as answered before every other branch
    /// (<c>ForbiddenValue</c>), and widening beyond it would silently re-judge rows nobody has reported a
    /// defect on.</para>
    /// </param>
    public static AssertionOutcome FromLatch(
        string assertionId, string signal, string expected, string? latchValue, long scan,
        SeriesAccounting series, Harness.Map.LatchSource source,
        TemporalShape shape = TemporalShape.Unstated)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (latchValue is null)
            return AssertionOutcome.Compare(assertionId, signal, expected, null);

        // *** THE FORBIDDEN-VALUE SHAPE INVERTS THE FOLD, exactly as it does on the series path. *** For
        // AtNoPoint `expected` names what must NOT happen, so a latch that MATCHES it is an occurrence of
        // the forbidden value and the assertion is Disagreed; a latch that does not match saw nothing and
        // the assertion Held.
        var matchesDeclared = string.Equals(latchValue, expected, StringComparison.Ordinal);
        var agreed = shape == TemporalShape.AtNoPoint ? !matchesDeclared : matchesDeclared;

        var window = new ObservationWindow(
            ObservationSource.Latch, 1, 1, agreed ? 1 : 0,
            0, WindowWasDeclared: true, WindowState.InWindow, scan, scan, scan,
            series.PollsObserved, series.DistinctFrames, series.RetainedFrames, series.Truncated)
        {
            // Reported as well as obeyed: a row whose shape was dropped read `Unstated` in the result
            // file, which is how the defect stayed invisible to everyone reading the output.
            Shape = shape,
        };

        return new AssertionOutcome(assertionId, signal, expected, latchValue,
            agreed ? AssertionState.Held : AssertionState.Disagreed)
        {
            Window = window,
            Detail = shape == TemporalShape.AtNoPoint
                ? (agreed
                    ? $"declared AT NO POINT and read from a latch: '{expected}' is the FORBIDDEN value and the latch did not "
                      + "hold it, so the condition did not occur inside the armed window. ⚠️ A pass on this shape is produced by "
                      + "SEEING NOTHING — here, by one latch bit standing for the whole window. It is only as strong as the claim "
                      + "that this latch is set by everything that would constitute an occurrence."
                    : $"declared AT NO POINT and read from a latch: '{expected}' is the FORBIDDEN value and the latch HELD it, so "
                      + "the condition occurred at some point inside the armed window. An occurrence is positive evidence. This IS "
                      + "a statement about the block.")
                : source == Harness.Map.LatchSource.Generated
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
    /// <summary>
    /// 🔴 <b>ONE RETAINED FRAME PER THIS MANY DISTINCT BAND CHANGES — <c>ObservationSeries.Stride</c>,
    /// carried through so the fold can say WHICH PART OF THE INDEX it judged.</b>
    ///
    /// <para>1 means every change was kept. Higher means the frames are a uniform sample spanning the whole
    /// index at that resolution — which is a far weaker loss than the prefix rule that preceded it, and the
    /// reason a truncated series is no longer simply "not a record of the index".</para>
    ///
    /// <para>Init-only with a default, so every hand-built fixture keeps compiling and keeps stating 1.</para>
    /// </summary>
    public int Stride { get; init; } = 1;

    /// <summary>Nothing was observed. <b>A positive value, so a consumer cannot mistake it for an omission.</b></summary>
    public static SeriesAccounting Nothing { get; } = new(0, 0, 0, false);
}
