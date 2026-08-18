using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>THE OBSERVATION-WINDOW MODEL — and the property under test is NOT "the shapes work". It is that
/// WHAT CAN BE SAID widened and WHAT PASSES did not.</b>
///
/// <para>Three invariants carry that, and they are asserted OVER THE WHOLE SHAPE SET rather than one
/// shape at a time, so that a sixth shape added later cannot quietly opt out of them:</para>
/// <list type="number">
/// <item><b><c>Unstated</c> is byte-identical to the pre-shape behaviour</b> — same verdict, same observed
/// text, same detail. Vectors already run against a real job carry it.</item>
/// <item><b>No shape turns a total absence of agreement into a pass</b> (with <c>AtNoPoint</c>'s inversion
/// asserted as its own converse, not exempted).</item>
/// <item><b>No shape accuses on a mixed series with no declared arm window</b>, except the one that
/// decides at a single NAMED instant — and that exception is asserted with its reason.</item>
/// </list>
///
/// <para>Every fixture name here is invented. Design note:
/// <c>docs/notes/observation-window-shapes.md</c>.</para>
/// </summary>
public class TemporalShapeTests
{
    private const string Id = "9f3ac1";
    private const string Signal = "SPEC.HoldCommand";

    private static readonly SeriesAccounting Series = new(120, 6, 6, false);

    private static readonly TemporalShape[] EveryShape =
        Enum.GetValues<TemporalShape>();

    private static readonly TemporalShape[] EveryStatedShape =
        Enum.GetValues<TemporalShape>().Where(s => s != TemporalShape.Unstated).ToArray();

    private static ObservedFrame Frame(long scan, string? value, WindowState window = WindowState.Unknown) =>
        new(scan, (int)scan, value, window);

    private static ObservedFrame In(long scan, string? value) => Frame(scan, value, WindowState.InWindow);

    private static AssertionOutcome Evaluate(TemporalShape shape, string expected, params ObservedFrame[] frames) =>
        SeriesEvaluation.Evaluate(Id, Signal, expected, frames, Series, shape);

    // =================================================================================================
    // INVARIANT 1 — the default is the old behaviour, and it is asserted on the TEXT, not just the state
    // =================================================================================================

    /// <summary>
    /// 🔴 <b>The one test that would fail if this whole change had rewritten the meaning of results
    /// already recorded against a real job.</b> The strings are quoted in full deliberately: a state
    /// comparison alone would pass while the sentence a human reads changed underneath them.
    /// </summary>
    [Fact]
    public void UNSTATED_IS_BYTE_IDENTICAL_TO_THE_PRE_SHAPE_BEHAVIOUR_on_a_mixed_series()
    {
        var frames = new[] { Frame(10, "true"), Frame(20, "true"), Frame(30, "false") };

        // The call the runner makes today — no shape argument at all.
        var untouched = SeriesEvaluation.Evaluate(Id, Signal, "true", frames, Series);
        var explicitly = SeriesEvaluation.Evaluate(Id, Signal, "true", frames, Series, TemporalShape.Unstated);

        Assert.Equal(AssertionState.Inconclusive, untouched.State);
        Assert.Equal(untouched.State, explicitly.State);
        Assert.Equal(untouched.Observed, explicitly.Observed);
        Assert.Equal(untouched.Detail, explicitly.Detail);

        Assert.Equal("true at 2 of 3 observation(s)", untouched.Observed);
        Assert.Equal(
            "the signal took the expected value at 2 of 3 considered frame(s) (first at scan 10) and a different value at the "
            + "other 1, including scan 30, the last one observed. *** THIS IS NOT A DISAGREEMENT AND MUST NOT BE ACTIONED AGAINST "
            + "THE BLOCK. *** Nothing declares which instant discharges this assertion, and picking one is how a model's deliberate "
            + "return to inert before it signals completion was read as a defect. To make it decidable: declare `armedBy` on this "
            + "signal in the binding so the window is published in-band, or declare the expectation `Latched` so a transient "
            + "occurrence survives the tail.",
            untouched.Detail);
    }

    /// <summary>
    /// The measured live shape — <b>agreed at 29 of 41 with no arm window</b> — still comes back
    /// <c>Inconclusive</c> when nobody states a shape. <b>This is the case the whole change exists to
    /// make decidable, and it must stay undecided until somebody says what they meant.</b>
    /// </summary>
    [Fact]
    public void THE_MEASURED_MIXED_SERIES_STAYS_INCONCLUSIVE_WHILE_NO_SHAPE_IS_STATED()
    {
        var frames = Enumerable.Range(0, 41)
            .Select(i => Frame(1000 + i, i < 29 ? "true" : "false"))
            .ToArray();

        var outcome = SeriesEvaluation.Evaluate(Id, Signal, "true", frames, Series);

        Assert.Equal(AssertionState.Inconclusive, outcome.State);
        Assert.False(outcome.SaysSomethingAboutTheBlock);
        Assert.Contains("29 of 41", outcome.Observed, StringComparison.Ordinal);
    }

    [Fact]
    public void UNSTATED_IS_THE_ZERO_VALUE_so_a_dropped_field_cannot_become_a_convenient_one()
    {
        Assert.Equal(TemporalShape.Unstated, default(TemporalShape));
        Assert.Equal(0, (int)TemporalShape.Unstated);
    }

    // =================================================================================================
    // INVARIANT 2 — nothing turns "no agreement anywhere" into a pass
    // =================================================================================================

    [Fact]
    public void NO_SHAPE_TURNS_A_TOTAL_ABSENCE_OF_AGREEMENT_INTO_A_PASS()
    {
        foreach (var shape in EveryShape)
        {
            var outcome = Evaluate(shape, "true", In(10, "false"), In(20, "false"), In(30, "false"));

            if (shape == TemporalShape.AtNoPoint)
            {
                // The converse, asserted rather than exempted: on this shape the DECLARED value is the
                // FORBIDDEN one, so "never observed" is the pass — and it is a pass about absence, which
                // the row must say out loud.
                Assert.Equal(AssertionState.Held, outcome.State);
                Assert.Contains("SEEING NOTHING", outcome.Detail!, StringComparison.Ordinal);
                continue;
            }

            Assert.Equal(AssertionState.Disagreed, outcome.State);
        }
    }

    [Fact]
    public void NO_SHAPE_MAKES_A_SERIES_THAT_WAS_NEVER_READ_SAY_ANYTHING()
    {
        foreach (var shape in EveryShape)
        {
            // Empty is not clean — including for AtNoPoint, where "we saw no forbidden value" is exactly
            // what an unread register also looks like.
            Assert.Equal(AssertionState.NotObserved, Evaluate(shape, "true").State);
            Assert.Equal(AssertionState.NotObserved, Evaluate(shape, "true", Frame(10, null), Frame(20, null)).State);
        }
    }

    /// <summary>
    /// 🔴 <b>A declared window that never opened outranks every shape.</b> Each retained frame is one the
    /// binding itself says is outside the window, so none of them is evidence — and a shape must not be a
    /// route around that.
    /// </summary>
    [Fact]
    public void A_WINDOW_THAT_NEVER_OPENED_OUTRANKS_EVERY_SHAPE()
    {
        foreach (var shape in EveryShape)
        {
            var outcome = Evaluate(shape, "true",
                Frame(10, "true", WindowState.OutOfWindow),
                Frame(20, "false", WindowState.OutOfWindow));

            Assert.Equal(AssertionState.NotObserved, outcome.State);
            Assert.Equal(SeriesEvaluation.OnlyOutsideTheWindow, outcome.Observed);
            Assert.False(outcome.SaysSomethingAboutTheBlock);
        }
    }

    // =================================================================================================
    // INVARIANT 3 — the accusation rule: no accusing on a mixed series with no declared window
    // =================================================================================================

    [Fact]
    public void NO_SHAPE_ACCUSES_ON_A_MIXED_SERIES_WITH_NO_DECLARED_WINDOW_except_the_single_instant_one()
    {
        foreach (var shape in EveryStatedShape)
        {
            // Agreed early, disagreed late — with nothing to say whether the late frames are the phase or
            // the model's required return to inert.
            var outcome = Evaluate(shape, "true", Frame(10, "true"), Frame(20, "true"), Frame(30, "false"));

            if (shape == TemporalShape.AtEnd)
            {
                // The one exception, and it is not a loophole: there is no SET here to disagree with
                // itself. Exactly one frame decides, the row names its scan, and the row states that with
                // no window that instant is the model's tail.
                Assert.Equal(AssertionState.Disagreed, outcome.State);
                Assert.Contains("scan 30", outcome.Detail!, StringComparison.Ordinal);
                Assert.Contains("NO ARM WINDOW WAS DECLARED", outcome.Detail!, StringComparison.Ordinal);
                Assert.Contains("TAIL", outcome.Detail!, StringComparison.Ordinal);
                continue;
            }

            Assert.NotEqual(AssertionState.Disagreed, outcome.State);
        }
    }

    /// <summary>
    /// The converse of the rule: <b>with the window declared, the sharp shapes DO accuse</b> — otherwise
    /// the model is unfalsifiable and the widening bought nothing.
    /// </summary>
    [Fact]
    public void WITH_A_DECLARED_WINDOW_THE_SHARP_SHAPES_DO_ACCUSE()
    {
        Assert.Equal(AssertionState.Disagreed,
            Evaluate(TemporalShape.Throughout, "true", In(10, "true"), In(20, "false")).State);

        Assert.Equal(AssertionState.Disagreed,
            Evaluate(TemporalShape.BecomesAndHolds, "true", In(10, "false"), In(20, "true"), In(30, "false")).State);

        Assert.Equal(AssertionState.Disagreed,
            Evaluate(TemporalShape.AtNoPoint, "true", In(10, "false"), In(20, "true")).State);
    }

    // =================================================================================================
    // Throughout
    // =================================================================================================

    [Fact]
    public void THROUGHOUT_HOLDS_WHEN_EVERY_CONSIDERED_FRAME_AGREES_and_says_what_that_can_mean()
    {
        var outcome = Evaluate(TemporalShape.Throughout, "true", In(10, "true"), In(20, "true"));

        Assert.Equal(AssertionState.Held, outcome.State);
        Assert.True(outcome.SaysSomethingAboutTheBlock);
        Assert.Contains("no counterexample was seen", outcome.Detail!, StringComparison.Ordinal);
        Assert.Contains("not proof the signal never dropped between two polls", outcome.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void THROUGHOUT_WITH_A_WINDOW_DISAGREES_AND_NAMES_THE_OFFENDING_FRAME()
    {
        var outcome = Evaluate(TemporalShape.Throughout, "true",
            In(10, "true"), In(20, "true"), In(30, "false"), In(40, "true"));

        Assert.Equal(AssertionState.Disagreed, outcome.State);
        Assert.True(outcome.SaysSomethingAboutTheBlock);
        Assert.Equal("false", outcome.Observed);
        Assert.Equal(30, outcome.Window!.DecidingScan);
        Assert.Contains("INSIDE the declared arm window", outcome.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void THROUGHOUT_WITHOUT_A_WINDOW_IS_INCONCLUSIVE_AND_NAMES_THE_REPAIR()
    {
        var outcome = Evaluate(TemporalShape.Throughout, "true", Frame(10, "true"), Frame(20, "false"));

        Assert.Equal(AssertionState.Inconclusive, outcome.State);
        Assert.False(outcome.SaysSomethingAboutTheBlock);
        Assert.Contains("MUST NOT BE ACTIONED AGAINST THE BLOCK", outcome.Detail!, StringComparison.Ordinal);
        Assert.Contains("armedBy", outcome.Detail!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Out-of-window frames are excluded before the shape is applied</b> — so a drop that happens in
    /// the tail cannot make a <c>Throughout</c> accuse, even though a window WAS declared.
    /// </summary>
    [Fact]
    public void THROUGHOUT_DOES_NOT_ACCUSE_ON_A_DROP_THAT_HAPPENS_OUT_OF_WINDOW()
    {
        var outcome = Evaluate(TemporalShape.Throughout, "true",
            In(10, "true"), In(20, "true"), Frame(30, "false", WindowState.OutOfWindow));

        Assert.Equal(AssertionState.Held, outcome.State);
        Assert.Equal(2, outcome.Window!.FramesConsidered);
        Assert.Equal(1, outcome.Window.FramesOutOfWindow);
    }

    // =================================================================================================
    // AtSomePoint
    // =================================================================================================

    [Fact]
    public void AT_SOME_POINT_HOLDS_ON_A_MIXED_SERIES_which_is_the_whole_point_of_the_shape()
    {
        var outcome = Evaluate(TemporalShape.AtSomePoint, "true",
            Frame(10, "true"), Frame(20, "true"), Frame(30, "false"));

        Assert.Equal(AssertionState.Held, outcome.State);
        Assert.True(outcome.SaysSomethingAboutTheBlock);
        Assert.Contains("2 of 3", outcome.Observed, StringComparison.Ordinal);
        Assert.Contains("outside this claim rather than against it", outcome.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void AT_SOME_POINT_IGNORES_THE_ORDER_where_becomes_and_holds_does_not()
    {
        Assert.Equal(AssertionState.Held,
            Evaluate(TemporalShape.AtSomePoint, "true", Frame(10, "true"), Frame(20, "false")).State);

        Assert.Equal(AssertionState.Held,
            Evaluate(TemporalShape.AtSomePoint, "true", Frame(10, "false"), Frame(20, "true")).State);
    }

    /// <summary>
    /// ⚠️ <b>The ramp trap is REPORTED where a reader meets the verdict</b> — an existential claim on a
    /// value that ramps is discharged by a pass-through on the way to the wrong value.
    /// </summary>
    [Fact]
    public void AT_SOME_POINT_REPORTS_A_RAMP_rather_than_gating_on_it()
    {
        var ramp = Evaluate(TemporalShape.AtSomePoint, "12",
            Frame(10, "10"), Frame(20, "11"), Frame(30, "12"), Frame(40, "13"), Frame(50, "14"));

        Assert.Equal(AssertionState.Held, ramp.State);
        Assert.Contains("5 DISTINCT values", ramp.Detail!, StringComparison.Ordinal);
        Assert.Contains("pass-through", ramp.Detail!, StringComparison.Ordinal);

        // A two-valued signal gets the count without the warning: a gate that fires outside its scope is
        // noise, and noise gets switched off.
        var boolean = Evaluate(TemporalShape.AtSomePoint, "true", Frame(10, "true"), Frame(20, "false"));
        Assert.DoesNotContain("pass-through", boolean.Detail!, StringComparison.Ordinal);
    }

    // =================================================================================================
    // BecomesAndHolds — the only shape that reads the ORDER
    // =================================================================================================

    /// <summary>
    /// 🔴 <b>THE SAME MULTISET OF OBSERVATIONS, TWO ORDERS, TWO VERDICTS.</b> This is what makes the shape
    /// a distinct question rather than a tuning of the other two — and it is the shape that reads a
    /// "51 of 65" correctly: a pass when the agreeing frames are the TAIL, a defect when they are the HEAD.
    /// </summary>
    [Fact]
    public void BECOMES_AND_HOLDS_IS_THE_ONLY_SHAPE_WHOSE_VERDICT_CHANGES_WHEN_THE_SERIES_IS_REVERSED()
    {
        var tail = Evaluate(TemporalShape.BecomesAndHolds, "true",
            In(10, "false"), In(20, "false"), In(30, "true"), In(40, "true"), In(50, "true"));

        var head = Evaluate(TemporalShape.BecomesAndHolds, "true",
            In(10, "true"), In(20, "true"), In(30, "true"), In(40, "false"), In(50, "false"));

        Assert.Equal(AssertionState.Held, tail.State);
        Assert.Equal(AssertionState.Disagreed, head.State);

        // Same agreement count either way — so nothing that only counts could tell these apart.
        Assert.Equal(3, tail.Window!.FramesAgreed);
        Assert.Equal(3, head.Window!.FramesAgreed);

        Assert.Equal(30, tail.Window.DecidingScan);   // the rising edge
        Assert.Equal(40, head.Window.DecidingScan);   // the fall-back
    }

    [Fact]
    public void BECOMES_AND_HOLDS_WITHOUT_A_WINDOW_IS_INCONCLUSIVE_ON_A_FALL_BACK()
    {
        var outcome = Evaluate(TemporalShape.BecomesAndHolds, "true",
            Frame(10, "false"), Frame(20, "true"), Frame(30, "false"));

        Assert.Equal(AssertionState.Inconclusive, outcome.State);
        Assert.Contains("MUST NOT BE ACTIONED AGAINST THE BLOCK", outcome.Detail!, StringComparison.Ordinal);
        Assert.Contains("armedBy", outcome.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void BECOMES_AND_HOLDS_HOLDS_WITHOUT_A_WINDOW_WHEN_THE_AGREEING_FRAMES_ARE_THE_TAIL()
    {
        // No window needed to PASS: extra tail frames could only have refuted it, and they agree.
        var outcome = Evaluate(TemporalShape.BecomesAndHolds, "true",
            Frame(10, "false"), Frame(20, "true"), Frame(30, "true"));

        Assert.Equal(AssertionState.Held, outcome.State);
        Assert.Contains("from scan 20 onward", outcome.Observed, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Already true at the first frame: a pass on the "holds" half, with the un-witnessed "becomes"
    /// half stated rather than implied.</b> The rising edge may have fallen before the first retained
    /// frame, and a row that quietly claimed to have seen it would be inventing evidence.
    /// </summary>
    [Fact]
    public void BECOMES_AND_HOLDS_SAYS_SO_WHEN_NO_RISING_EDGE_WAS_WITNESSED()
    {
        var outcome = Evaluate(TemporalShape.BecomesAndHolds, "true", In(10, "true"), In(20, "true"));

        Assert.Equal(AssertionState.Held, outcome.State);
        Assert.Contains("NO RISING EDGE WAS OBSERVED", outcome.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void BECOMES_AND_HOLDS_DISAGREES_WHEN_IT_NEVER_BECAME()
    {
        var outcome = Evaluate(TemporalShape.BecomesAndHolds, "true", In(10, "false"), In(20, "false"));

        Assert.Equal(AssertionState.Disagreed, outcome.State);
        Assert.Contains("never became", outcome.Detail!, StringComparison.Ordinal);
    }

    // =================================================================================================
    // AtEnd
    // =================================================================================================

    [Fact]
    public void AT_END_DECIDES_AT_THE_LAST_CONSIDERED_FRAME_AND_NAMES_IT()
    {
        var held = Evaluate(TemporalShape.AtEnd, "false", Frame(10, "true"), Frame(20, "true"), Frame(30, "false"));

        Assert.Equal(AssertionState.Held, held.State);
        Assert.Equal("false", held.Observed);
        Assert.Equal(30, held.Window!.DecidingScan);
        Assert.Contains("exactly one frame decides: scan 30", held.Detail!, StringComparison.Ordinal);

        // ...and the agreement count is reported as CONTEXT, explicitly not as the thing that decided.
        Assert.Contains("NOT part of this verdict", held.Detail!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>With a window declared, the instant is the last IN-WINDOW frame, not the last frame overall.</b>
    /// The distinction is the entire 2026-08-17 defect: the last frame overall is, by construction, the one
    /// instant at which a well-built model has already returned the block to inert.
    /// </summary>
    [Fact]
    public void AT_END_TAKES_THE_END_OF_THE_WINDOW_NOT_THE_END_OF_THE_SERIES()
    {
        // Deliberately mixed inside the window, so the single-instant handler actually runs: the naive
        // "last frame" is scan 30 and disagrees, the correct end-of-phase frame is scan 20 and agrees.
        var outcome = Evaluate(TemporalShape.AtEnd, "true",
            In(10, "false"), In(20, "true"), Frame(30, "false", WindowState.OutOfWindow));

        Assert.Equal(AssertionState.Held, outcome.State);
        Assert.Equal(20, outcome.Window!.DecidingScan);
        Assert.Contains("the end of the phase", outcome.Detail!, StringComparison.Ordinal);

        // The out-of-window frame is still COUNTED and reported — excluded, never silently dropped.
        Assert.Equal(1, outcome.Window.FramesOutOfWindow);
        Assert.Equal(WindowState.OutOfWindow, outcome.Window.WindowAtFinalFrame);
    }

    // =================================================================================================
    // AtNoPoint — the inverted fold
    // =================================================================================================

    [Fact]
    public void AT_NO_POINT_HOLDS_WHEN_THE_FORBIDDEN_VALUE_NEVER_APPEARS_and_the_row_reads_as_a_sentence()
    {
        var outcome = Evaluate(TemporalShape.AtNoPoint, "true", In(10, "false"), In(20, "false"));

        Assert.Equal(AssertionState.Held, outcome.State);

        // *** THE RENDERING HAZARD, PINNED. *** `expected: "true" ... Held` about a signal that was never
        // true would be actively misleading to a skimming reader, so on this shape the observed text is a
        // sentence. The Expected field keeps the author's declared value so the row still joins.
        Assert.Equal("true", outcome.Expected);
        Assert.Equal(SeriesEvaluation.ForbiddenValueAbsent("true"), outcome.Observed);
        Assert.Contains("was never observed", outcome.Observed, StringComparison.Ordinal);
    }

    [Fact]
    public void AT_NO_POINT_DISAGREES_ON_AN_OCCURRENCE_INSIDE_THE_WINDOW()
    {
        var outcome = Evaluate(TemporalShape.AtNoPoint, "true", In(10, "false"), In(20, "true"), In(30, "false"));

        Assert.Equal(AssertionState.Disagreed, outcome.State);
        Assert.True(outcome.SaysSomethingAboutTheBlock);
        Assert.Equal(20, outcome.Window!.DecidingScan);
        Assert.Contains("FORBIDDEN value", outcome.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void AT_NO_POINT_WITHOUT_A_WINDOW_IS_INCONCLUSIVE_because_the_occurrence_may_be_a_reset_transient()
    {
        var outcome = Evaluate(TemporalShape.AtNoPoint, "true", Frame(10, "false"), Frame(20, "true"));

        Assert.Equal(AssertionState.Inconclusive, outcome.State);
        Assert.False(outcome.SaysSomethingAboutTheBlock);
        Assert.Contains("armedBy", outcome.Detail!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The accounting field means something different on this shape, and the row says so</b> rather
    /// than leaving a reader to assume <c>framesAgreed</c> counts agreements.
    /// </summary>
    [Fact]
    public void AT_NO_POINT_DECLARES_THAT_ITS_AGREEMENT_COUNT_IS_AN_OCCURRENCE_COUNT()
    {
        var outcome = Evaluate(TemporalShape.AtNoPoint, "true", In(10, "true"), In(20, "false"));

        Assert.Equal(1, outcome.Window!.FramesAgreed);
        Assert.Contains("OCCURRENCES OF THE FORBIDDEN VALUE", outcome.Window.Describe(), StringComparison.Ordinal);
    }

    // =================================================================================================
    // The shape travels with the outcome
    // =================================================================================================

    [Fact]
    public void THE_DECLARED_SHAPE_IS_CARRIED_ONTO_THE_OUTCOME_so_a_verdict_can_say_which_question_it_answered()
    {
        foreach (var shape in EveryShape)
        {
            var outcome = Evaluate(shape, "true", In(10, "true"), In(20, "true"));

            Assert.Equal(shape, outcome.Window!.Shape);

            if (shape == TemporalShape.Unstated)
            {
                // Unchanged text, again — Describe() is read by humans and by the JSON.
                Assert.DoesNotContain("Declared temporal shape", outcome.Window.Describe(), StringComparison.Ordinal);
                continue;
            }

            Assert.Contains($"Declared temporal shape: {shape}", outcome.Window.Describe(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AN_EXPECTATION_DECLARES_NO_SHAPE_BY_DEFAULT()
    {
        var declaration = new ObservabilityDeclaration("SPEC.Response", SignalNature.PersistentState,
            InstrumentationMode.Sampled, 875, "true");

        Assert.Equal(TemporalShape.Unstated, declaration.Shape);

        var stated = declaration with { Shape = TemporalShape.BecomesAndHolds };
        Assert.Equal(TemporalShape.BecomesAndHolds, stated.Shape);
        Assert.Equal(TemporalShape.Unstated, declaration.Shape);
    }

    // =================================================================================================
    // The verdict a package computes from these rows
    // =================================================================================================

    /// <summary>
    /// <b>The point of the whole change, at package level:</b> a wave that returned <c>Inconclusive</c> and
    /// nothing else becomes a wave that says something — <i>once somebody states what they meant</i>, and
    /// not before.
    /// </summary>
    [Fact]
    public void A_STATED_SHAPE_IS_WHAT_TURNS_AN_INCONCLUSIVE_ROW_INTO_A_CONCLUSIVE_ONE()
    {
        var frames = new[] { In(10, "false"), In(20, "true"), In(30, "true"), In(40, "true") };

        var unstated = SeriesEvaluation.Evaluate(Id, Signal, "true", frames, Series);
        Assert.Equal(AssertionState.Inconclusive, unstated.State);
        Assert.False(unstated.SaysSomethingAboutTheBlock);

        var asExistential = Evaluate(TemporalShape.AtSomePoint, "true", frames);
        var asLatching = Evaluate(TemporalShape.BecomesAndHolds, "true", frames);
        var asUniversal = Evaluate(TemporalShape.Throughout, "true", frames);

        Assert.Equal(AssertionState.Held, asExistential.State);
        Assert.Equal(AssertionState.Held, asLatching.State);
        Assert.Equal(AssertionState.Disagreed, asUniversal.State);

        // Three different, defensible answers to three different questions about ONE series. That is the
        // widening: the evidence did not change, the claim did.
        Assert.True(asExistential.SaysSomethingAboutTheBlock);
        Assert.True(asLatching.SaysSomethingAboutTheBlock);
        Assert.True(asUniversal.SaysSomethingAboutTheBlock);
    }
}
