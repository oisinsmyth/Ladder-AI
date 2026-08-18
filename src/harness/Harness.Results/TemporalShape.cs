namespace Harness.Results;

/// <summary>
/// 🔴 <b>WHAT AN EXPECTATION CLAIMS ABOUT ITS SIGNAL <i>IN TIME</i> — the question nothing in this system
/// could ask until 2026-08-18.</b>
///
/// <para><b>The measured problem.</b> The harness samples repeatedly across the observation window and
/// folds the series three ways: all agree ⇒ <c>Held</c>, none agree ⇒ <c>Disagreed</c>, mixed ⇒
/// <c>Inconclusive</c>. That refusal is correct. But <b>four different authors reach it meaning four
/// different things</b>, and nothing let them say which: <i>true throughout the phase</i> (then 51-of-65
/// is a FAILURE), <i>true at some point</i> (then 51-of-65 is a PASS), <i>true at one defined instant</i>
/// (needs the instant defined), <i>becomes true and stays</i> (a pass iff the 51 are the TAIL). On a live
/// wave, 2 of 3 packages and 4 of 14 assertion rows came back <c>Inconclusive</c> for exactly this reason,
/// with <c>conclusiveAboutTheBlock: 0</c>. At plant scale that is most of a campaign's results.</para>
///
/// <para>🔴 <b>WIDEN WHAT CAN BE SAID. NEVER WIDEN WHAT PASSES.</b> A rule of the form <i>"if most
/// observations agree, call it a pass"</i> converts an honest refusal into a fabricated green — the exact
/// failure this project exists to prevent. <b>Every shape below is a claim the author had to type</b>, and
/// <see cref="Unstated"/> — the default, and what every vector written before this field existed carries —
/// behaves <b>exactly as it did before</b>, down to the text.</para>
///
/// <para><b>NOT <c>InstrumentationMode</c>, and not <c>SignalNature</c>.</b> An instrumentation mode is
/// <i>how the copy layer watches</i> and is DERIVED from what was generated; a signal's nature is <i>what
/// the signal is like</i> and is discovered. A shape is <b>a claim about the specification</b>, made by the
/// vector author, and it can be wrong. See <see cref="BecomesAndHolds"/> for why that one is deliberately
/// not called <c>Latched</c>.</para>
///
/// <para>Design note: <c>docs/notes/observation-window-shapes.md</c>.</para>
/// </summary>
public enum TemporalShape
{
    /// <summary>
    /// 🔴 <b>NOBODY SAID WHAT SHAPE THIS EXPECTATION HAS — and it is the ZERO VALUE, following
    /// <c>AssertionForm.Unstated</c>, <c>MapProvenance.Unstated</c> and <c>WindowState.Unknown</c>.</b>
    ///
    /// <para><b>It is deliberately UNUSABLE.</b> A shape decides whether a mixed series is a pass, a
    /// failure or a refusal, so a field that silently defaulted to any of the five real shapes would hand
    /// every author who omitted it that shape's reading. Making the default unusable means <b>a DROPPED
    /// shape fails the same comparison as a WRONG one</b>.</para>
    ///
    /// <para><b>And it is the BACKWARD-COMPATIBILITY GUARANTEE, not merely a safe default.</b> Every
    /// vector written before this field existed carries it, including vectors already run against a real
    /// job. A mixed series under this shape is <c>Inconclusive</c> with the same text it produced on the
    /// day — a silent change of verdict here would rewrite the meaning of results already recorded.</para>
    /// </summary>
    Unstated = 0,

    /// <summary>
    /// <b>UNIVERSAL: expected to hold at EVERY instant of the phase.</b> Commanded states, held outputs,
    /// a mode that must persist.
    ///
    /// <para>A mixed series is a <b>DISAGREEMENT</b> — but only where an arm window was declared, because
    /// only then is the disagreeing frame known to be inside the phase rather than in the model's inert
    /// tail. Without a window it stays <c>Inconclusive</c> and names <c>armedBy</c> as the repair.</para>
    ///
    /// <para>A <c>Held</c> here means <i>no counterexample in N samples</i>, which is all a sampled
    /// universal claim can ever mean. The denominators are on the row.</para>
    /// </summary>
    Throughout,

    /// <summary>
    /// <b>EXISTENTIAL: expected to be true at SOME instant of the phase.</b> Events, acknowledgements, a
    /// step that must be entered — anything whose exit is not part of the claim.
    ///
    /// <para>A mixed series is a <b>PASS</b>: the signal was seen. Failure needs the expected value to be
    /// absent from the WHOLE series, tail included — a strictly larger search than the phase, which is why
    /// this shape needs no window in either direction and is <b>exactly as strong an accusation as
    /// today's</b>.</para>
    ///
    /// <para>⚠️ <b>THE TRAP: an existential claim on a RAMPING NUMERIC is discharged by a transient
    /// pass-through.</b> A counter climbing to the wrong value passes THROUGH the right one. This shape is
    /// for EVENT-shaped signals; on a value that ramps use <see cref="AtEnd"/> or <see cref="Throughout"/>.
    /// The outcome reports the number of distinct observed values so a pass-through is visible in the row.
    /// This is also why the fold still refuses to DERIVE a shape from <c>AssertionForm.When</c>.</para>
    /// </summary>
    AtSomePoint,

    /// <summary>
    /// <b>ORDERED: expected to BECOME true and then STAY true for the rest of the phase.</b> Latching
    /// alarms, fault flags, sequence-complete bits — anything with a rising edge that must not fall back.
    ///
    /// <para><b>This is the shape that reads <c>51 of 65</c> correctly</b>: a pass if the agreeing frames
    /// are the TAIL, a defect if they are the HEAD. It is the only shape that reads the ORDER of the
    /// frames, and the only one whose verdict changes when the series is reversed — which is what makes it
    /// a distinct shape rather than a tuning of the two above. Strictly stronger than
    /// <see cref="AtSomePoint"/>, strictly weaker than <see cref="Throughout"/>.</para>
    ///
    /// <para>🔴 <b>IT IS NOT <c>InstrumentationMode.Latched</c> AND THE NAME IS DELIBERATE.</b> That is an
    /// INSTRUMENT — a sticky bit the copy layer emits, derived from what was generated, immune to a poll
    /// gap. This is a CLAIM, answered from the poll series and fully subject to the observability floor.
    /// A signal may be instrumented <c>Latched</c> and asserted <see cref="BecomesAndHolds"/>; then the
    /// latch answers and this shape is never consulted. <b>The word "Latched" is spent.</b></para>
    /// </summary>
    BecomesAndHolds,

    /// <summary>
    /// <b>ONE DEFINED INSTANT: the LAST CONSIDERED FRAME</b> — end-of-window where an arm window was
    /// declared, the completion instant where it was not. Post-conditions: <i>the block returns to
    /// inert</i>, <i>the sequence leaves the valve closed</i>, a terminal state number.
    ///
    /// <para>There is no <c>Inconclusive</c> branch, because there is no SET to disagree with itself.</para>
    ///
    /// <para>🔴 <b>THIS IS THE SHAPE WITH THE SHARP EDGE — IT IS THE ORIGINAL DEFECT WITH A DECLARATION
    /// ATTACHED.</b> The behaviour before 2026-08-17 <i>was</i> this shape, applied to every expectation,
    /// chosen by nobody, and it produced a confident FAIL against a block proven correct on the device.
    /// What makes it admissible now is that <b>the author typed it and the row names the instant and its
    /// window state</b>. With NO window declared, that instant is the last frame before completion — i.e.
    /// the INERT TAIL for exactly the class of well-built model this system requires — and the outcome
    /// says so in those words.</para>
    /// </summary>
    AtEnd,

    /// <summary>
    /// <b>THE FORBIDDEN VALUE: this value must NEVER be observed in the phase.</b> The expectation's
    /// <c>Expected</c> field names what must not appear. <c>NEVER</c>-form assertions, interlocks,
    /// prohibitions.
    ///
    /// <para><b>Not redundant with <see cref="Throughout"/>.</b> For a Bool, <i>never true</i> and
    /// <i>always false</i> coincide. <b>For anything else they do not</b> — <i>never state 7</i> is not
    /// <i>always state 3</i> — and the predicate model here is string equality with no inequality
    /// operator, so without this shape a NEVER-form assertion over a non-Bool has no expressible
    /// expectation at all. <c>AssertionForm.Never</c> is already first-class here, and a live wave recorded
    /// four NEVER-form assertions among the nine it could not cover.</para>
    ///
    /// <para>⚠️ <b>A pass here is produced by SEEING NOTHING, which is also what a poll gap produces.</b>
    /// That is the standing weakness of every NEVER assertion under sampling and it is bounded by the
    /// observability floor, not by this shape. An OCCURRENCE, by contrast, is positive evidence — so a
    /// disagreement is safe wherever the occurrence is known to be inside the phase.</para>
    ///
    /// <para><b>The row's <c>Observed</c> text is written as a SENTENCE on this shape alone</b>, because
    /// <c>expected: "true" … Held</c> about a signal that was never true is actively misleading to a
    /// skimming reader. The <c>Expected</c> field keeps the author's declared value so the row still joins
    /// to the vector. In the accounting, <c>FramesAgreed</c> counts OCCURRENCES OF THE FORBIDDEN VALUE —
    /// <see cref="ObservationWindow.Describe"/> says so on every row of this shape.</para>
    /// </summary>
    AtNoPoint,
}
