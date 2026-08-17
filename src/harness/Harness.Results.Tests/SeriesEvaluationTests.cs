using Harness.Map;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>THE FOLD, IN ISOLATION — and the property it must never lose is that it CANNOT PRODUCE A FALSE
/// ACCUSATION.</b>
///
/// <para>The end-to-end shape is proved in <c>Harness.Loop.Tests.ObservationWindowTests</c> against a
/// block that actually runs. These are the fold's own boundaries, which an end-to-end fixture reaches
/// only by luck.</para>
/// </summary>
public class SeriesEvaluationTests
{
    private const string Id = "a1b2c3";
    private const string Signal = "SPEC.Response";

    private static readonly SeriesAccounting Series = new(120, 6, 6, false);

    private static ObservedFrame Frame(long scan, string? value, WindowState window = WindowState.Unknown) =>
        new(scan, (int)scan, value, window);

    private static AssertionOutcome Evaluate(string expected, params ObservedFrame[] frames) =>
        SeriesEvaluation.Evaluate(Id, Signal, expected, frames, Series);

    // -------------------------------------------------------------------------------------------------
    // The three-way fold
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void EVERY_CONSIDERED_FRAME_AGREEING_IS_HELD()
    {
        var outcome = Evaluate("true", Frame(1, "true"), Frame(2, "true"), Frame(3, "true"));

        Assert.Equal(AssertionState.Held, outcome.State);
        Assert.True(outcome.SaysSomethingAboutTheBlock);
        Assert.Equal(3, outcome.Window!.FramesAgreed);
        Assert.Equal(3, outcome.Window.FramesConsidered);
    }

    [Fact]
    public void NO_CONSIDERED_FRAME_AGREEING_IS_A_DISAGREEMENT_and_that_is_the_only_shape_that_may_accuse()
    {
        var outcome = Evaluate("true", Frame(1, "false"), Frame(2, "false"));

        Assert.Equal(AssertionState.Disagreed, outcome.State);
        Assert.True(outcome.SaysSomethingAboutTheBlock);
        Assert.Equal("false", outcome.Observed);
        Assert.Equal(0, outcome.Window!.FramesAgreed);
    }

    /// <summary>
    /// 🔴 <b>THE MEASURED DEFECT'S SHAPE: agreed early, disagreed at the end. It is NOT a disagreement.</b>
    /// </summary>
    [Fact]
    public void SOME_AGREEING_AND_SOME_NOT_IS_INCONCLUSIVE_NEVER_DISAGREED()
    {
        var outcome = Evaluate("true", Frame(1, "true"), Frame(2, "true"), Frame(3, "false"));

        Assert.Equal(AssertionState.Inconclusive, outcome.State);
        Assert.NotEqual(AssertionState.Disagreed, outcome.State);
        Assert.False(outcome.SaysSomethingAboutTheBlock);

        Assert.Equal(2, outcome.Window!.FramesAgreed);
        Assert.Equal(3, outcome.Window.FramesConsidered);

        // The observed text cannot be skimmed as a bare value, and the detail names both repairs.
        Assert.Contains("2 of 3", outcome.Observed, StringComparison.Ordinal);
        Assert.Contains("MUST NOT BE ACTIONED AGAINST THE BLOCK", outcome.Detail!, StringComparison.Ordinal);
        Assert.Contains("armedBy", outcome.Detail!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The ORDER of the disagreement does not change the answer</b> — asserted because the tempting
    /// shortcut is "trust the last frame unless an earlier one disagrees", which is the old behaviour
    /// wearing a series.
    /// </summary>
    [Fact]
    public void A_MIXED_SERIES_WHOSE_LAST_FRAME_AGREES_IS_ALSO_INCONCLUSIVE()
    {
        var outcome = Evaluate("true", Frame(1, "false"), Frame(2, "true"));

        Assert.Equal(AssertionState.Inconclusive, outcome.State);
    }

    // -------------------------------------------------------------------------------------------------
    // The window
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void OUT_OF_WINDOW_FRAMES_ARE_EXCLUDED_AND_COUNTED_not_silently_dropped()
    {
        var outcome = Evaluate("true",
            Frame(1, "false", WindowState.OutOfWindow),
            Frame(2, "true", WindowState.InWindow),
            Frame(3, "true", WindowState.InWindow),
            Frame(4, "false", WindowState.OutOfWindow));

        Assert.Equal(AssertionState.Held, outcome.State);
        Assert.Equal(2, outcome.Window!.FramesConsidered);
        Assert.Equal(2, outcome.Window.FramesOutOfWindow);
        Assert.Equal(4, outcome.Window.FramesRead);
        Assert.True(outcome.Window.WindowWasDeclared);
        Assert.Equal(WindowState.OutOfWindow, outcome.Window.WindowAtFinalFrame);
    }

    [Fact]
    public void THE_WINDOW_STILL_ACCUSES_WHEN_EVERY_IN_WINDOW_FRAME_DISAGREES()
    {
        // The over-fire converse of the test above: narrowing to the window must not make everything
        // unfalsifiable. Same out-of-window frames, opposite in-window content, opposite verdict.
        var outcome = Evaluate("true",
            Frame(1, "true", WindowState.OutOfWindow),
            Frame(2, "false", WindowState.InWindow),
            Frame(3, "false", WindowState.InWindow),
            Frame(4, "false", WindowState.OutOfWindow));

        Assert.Equal(AssertionState.Disagreed, outcome.State);
        Assert.Equal(2, outcome.Window!.FramesConsidered);
        Assert.Equal(0, outcome.Window.FramesAgreed);
    }

    /// <summary>
    /// 🔴 <b>A DECLARED WINDOW THAT NEVER OPENED IN ANY RETAINED FRAME IS NOT A DISAGREEMENT.</b>
    ///
    /// <para>This is the JOB9004 snapshot in miniature: <c>Stim.Armed</c> read 0 in the one frame the
    /// harness kept. Every reading held is one the binding itself says is outside the window, so none of
    /// them is evidence — and rendering that as <c>Disagreed</c> is exactly the false accusation.</para>
    /// </summary>
    [Fact]
    public void A_WINDOW_THAT_NEVER_OPENED_IS_NOT_OBSERVED_RATHER_THAN_DISAGREED()
    {
        var outcome = Evaluate("true",
            Frame(1, "false", WindowState.OutOfWindow),
            Frame(2, "false", WindowState.OutOfWindow));

        Assert.Equal(AssertionState.NotObserved, outcome.State);
        Assert.NotEqual(AssertionState.Disagreed, outcome.State);
        Assert.Equal(SeriesEvaluation.OnlyOutsideTheWindow, outcome.Observed);
        Assert.Equal(0, outcome.Window!.FramesConsidered);
        Assert.Equal(2, outcome.Window.FramesOutOfWindow);
    }

    [Fact]
    public void UNKNOWN_WINDOW_STATE_EXCLUDES_NOTHING_and_reports_that_no_window_was_declared()
    {
        // Unknown is NOT "open" and it is NOT "closed" — it means nothing could classify the frame, so
        // nothing is excluded and the report says the window state is unknown.
        var outcome = Evaluate("true", Frame(1, "true"), Frame(2, "true"));

        Assert.Equal(AssertionState.Held, outcome.State);
        Assert.False(outcome.Window!.WindowWasDeclared);
        Assert.Equal(0, outcome.Window.FramesOutOfWindow);
        Assert.Contains("NO ARM WINDOW WAS DECLARED", outcome.Window.Describe(), StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------------------
    // Nothing read
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void A_SERIES_IN_WHICH_NOTHING_DECODED_IS_NEVER_READ()
    {
        var outcome = Evaluate("true", Frame(1, null), Frame(2, null));

        Assert.Equal(AssertionState.NotObserved, outcome.State);
        Assert.Equal("<never read>", outcome.Observed);
    }

    [Fact]
    public void AN_EMPTY_SERIES_IS_NEVER_READ_rather_than_vacuously_held()
    {
        var outcome = Evaluate("true");

        Assert.Equal(AssertionState.NotObserved, outcome.State);
        Assert.Equal("<never read>", outcome.Observed);
    }

    [Fact]
    public void UNDECODABLE_FRAMES_ARE_SKIPPED_and_the_readable_ones_still_decide()
    {
        var outcome = Evaluate("true", Frame(1, null), Frame(2, "true"), Frame(3, null));

        Assert.Equal(AssertionState.Held, outcome.State);
        Assert.Equal(1, outcome.Window!.FramesRead);
    }

    // -------------------------------------------------------------------------------------------------
    // The latch
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void A_GENERATED_LATCH_IS_READ_AS_THE_ARTIFACT_IT_IS()
    {
        var outcome = SeriesEvaluation.FromLatch(Id, Signal, "true", "true", 900, Series, LatchSource.Generated);

        Assert.Equal(AssertionState.Held, outcome.State);
        Assert.Equal(ObservationSource.Latch, outcome.Window!.Source);
        Assert.Equal(900, outcome.Window.DecidingScan);
        Assert.Contains("GENERATED latch register", outcome.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_LATCH_THAT_DISAGREES_STILL_DISAGREES()
    {
        var outcome = SeriesEvaluation.FromLatch(Id, Signal, "true", "false", 900, Series, LatchSource.Generated);

        Assert.Equal(AssertionState.Disagreed, outcome.State);
        Assert.Equal("false", outcome.Observed);
    }

    /// <summary>
    /// <b>A hand-authored latch is admitted and its limit is PRINTED WHERE A READER OF RESULTS MEETS IT</b>
    /// — the harness did not emit that latch and cannot inspect the named block, so this one is a
    /// transferred responsibility rather than a verification.
    /// </summary>
    [Fact]
    public void A_HAND_AUTHORED_LATCH_IS_ADMITTED_AND_SAYS_IT_IS_TAKEN_ON_TRUST()
    {
        var outcome = SeriesEvaluation.FromLatch(Id, Signal, "true", "true", 900, Series, LatchSource.HandAuthored);

        Assert.Equal(AssertionState.Held, outcome.State);
        Assert.Contains("TAKEN ON TRUST", outcome.Detail!, StringComparison.Ordinal);
        Assert.Contains("VALUE register", outcome.Detail!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>NO LATCH IS A REFUSAL NAMING THE SIGNAL — never a fallback to the value register.</b>
    ///
    /// <para>That fallback is not hypothetical: it is what the code did for the whole life of the feature,
    /// because <c>e.Mode</c> was never read by the code that performs the observation. Reinstating it "just
    /// when there is no latch" would recreate it in the one case where the author has said outright that
    /// the value register cannot answer.</para>
    /// </summary>
    [Fact]
    public void NO_LATCH_IS_A_REFUSAL_THAT_NAMES_THE_SIGNAL()
    {
        var outcome = SeriesEvaluation.NoLatchFor(Id, Signal, "true", Series);

        Assert.Equal(AssertionState.NotObserved, outcome.State);
        Assert.False(outcome.SaysSomethingAboutTheBlock);
        Assert.Equal(SeriesEvaluation.NoLatch, outcome.Observed);
        Assert.Contains(Signal, outcome.Detail!, StringComparison.Ordinal);
        Assert.Contains("NOT A SUBSTITUTE", outcome.Detail!, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------------------
    // The denominators travel with the outcome
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void THE_SERIES_ACCOUNTING_TRAVELS_ONTO_EVERY_OUTCOME_including_the_truncation_flag()
    {
        var truncated = new SeriesAccounting(2000, 900, 65, true);

        var outcome = SeriesEvaluation.Evaluate(Id, Signal, "true", new[] { Frame(1, "true") }, truncated);

        Assert.Equal(2000, outcome.Window!.PollsObserved);
        Assert.Equal(900, outcome.Window.DistinctFrames);
        Assert.Equal(65, outcome.Window.RetainedFrames);
        Assert.True(outcome.Window.SeriesTruncated);
        Assert.Contains("SERIES TRUNCATED", outcome.Window.Describe(), StringComparison.Ordinal);
    }
}
