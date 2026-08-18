using Harness.Results;
using Harness.Wire;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>D1: A BOUNDED RETENTION THAT DROPPED THE PART OF THE INDEX UNDER TEST, AND A FOLD THAT
/// CONVICTED FROM WHAT WAS LEFT.</b>
///
/// <para><b>Measured on vector 003 of the wave of 2026-08-18</b>, from published numbers: a 369.4-second
/// index, 515 distinct band changes at 1.39 /s, and an observation window opening at 313.8 s. The retention
/// rule kept the FIRST 64 distinct frames plus the final one, which on that slot bought 45.9 seconds of any
/// index — <b>the retained series and the window missed each other by ~268 seconds.</b> The two signals
/// that declared an arm window returned the honest <c>NotObserved</c>. The five that did not were folded
/// over the same 65 head-and-tail frames and returned verdicts: one <c>Held</c> earned entirely outside the
/// phase, and <b>three <c>Disagreed</c> — accusations against a site's block built from frames taken
/// more than four minutes before the stimulus meant to make them true.</b></para>
///
/// <para><b>TWO INDEPENDENT DEFECTS, AND THIS FILE HOLDS BOTH HALVES.</b> The retention was wrong (fixed in
/// <see cref="ObservationRecorder"/>: a uniform sample of the whole index rather than a prefix of it), AND
/// the fold was willing to convict from an ABSENCE over a series whose coverage of the phase nobody could
/// state. Either fix alone leaves a live route to a false accusation, so both are asserted here — with the
/// PREFIX COUNTERFACTUAL computed from the same frames, because a test that cannot reproduce the old
/// verdict is not evidence that the new one is different.</para>
/// </summary>
public class TruncatedSeriesVerdictTests
{
    private const string Id = "d1d1d1";
    private const string Signal = "SPEC.EngageCommand";

    // Vector 003's published shape. The arm rises at change 438 (313.8 s of 369.4 s at 1.39 /s) and falls
    // again before completion, because a well-built stimulus model returns the block to inert BEFORE it
    // raises its completion flag — which is what puts the completing frame outside the window too.
    private const int DistinctChanges = 515;
    private const int WindowOpens = 438;
    private const int WindowCloses = 506;

    /// <summary>
    /// Register 0 is the signal under test, register 1 is its arm, <b>register 2 is what makes the band
    /// change 515 times.</b>
    ///
    /// <para>That third register is not padding. The recorder collapses CONSECUTIVE DUPLICATES, so a band
    /// holding only the signal and its arm changes twice in the whole index and never reaches the cap — the
    /// measured slot's 23-register band carries phase codes and counters beside the signals, which is why
    /// 515 changes were recorded across 369 seconds. <b>Without it this fixture would not truncate, and
    /// every test below would pass against the defect.</b></para>
    /// </summary>
    private static ObservationSeries MeasuredIndex()
    {
        var recorder = new ObservationRecorder();

        for (var change = 1; change <= DistinctChanges; change++)
        {
            // *** THE BLOCK DOES EXACTLY WHAT THE SPECIFICATION ASKS: *** it presents the commanded state
            // for the whole armed phase, and withdraws it when the phase ends. Every frame outside the
            // window reads false, and every frame outside the window is correct to read false.
            var armed = change >= WindowOpens && change < WindowCloses;
            recorder.Record(new ScanCount((uint)change), change, Frame(change, armed));
        }

        return recorder.Build();
    }

    private static ushort[] Frame(int change, bool armed) =>
        new[] { armed ? (ushort)1 : (ushort)0, armed ? (ushort)1 : (ushort)0, (ushort)change };

    /// <summary>Decode one series the way <c>LoopRun</c> does: value from register 0, window from register 1.</summary>
    private static ObservedFrame[] Decode(ObservationSeries series, bool declareWindow) =>
        series.Frames.Select(f => new ObservedFrame(
            f.Scan.Raw,
            f.PollRound,
            ((f.Registers[0] & 1) == 1).ToString().ToLowerInvariant(),
            declareWindow
                ? (f.Registers[1] & 1) == 1 ? WindowState.InWindow : WindowState.OutOfWindow
                : WindowState.Unknown)).ToArray();

    private static SeriesAccounting Accounting(ObservationSeries series) =>
        new(series.PollsObserved, series.DistinctFrames, series.Frames.Count, series.Truncated)
        {
            Stride = series.Stride,
        };

    /// <summary>
    /// 🔴 <b>THE FIXTURE TRAP, CLOSED FIRST — everything below is evidence only if this holds.</b>
    ///
    /// <para>The old rule is reproduced from the same frames: first <see cref="ObservationSeries.DefaultCap"/>
    /// distinct frames plus the final one. <b>Not one of them is inside the window</b>, and the fold over
    /// them convicts. If that were not so, the tests below would pass against a harness that had changed
    /// nothing.</para>
    /// </summary>
    [Fact]
    public void THE_PREFIX_RULE_RETAINED_NOTHING_INSIDE_THE_WINDOW_AND_CONVICTED_ANYWAY()
    {
        // The prefix rule, restated: the first `DefaultCap` distinct changes plus the final one. Built
        // from the same index shape rather than from the new series, so it is the OLD rule and not a
        // filtered version of the new one.
        var prefix = Enumerable.Range(1, ObservationSeries.DefaultCap)
            .Append(DistinctChanges)
            .Select(change =>
            {
                var armed = change >= WindowOpens && change < WindowCloses;
                return new ObservedFrame(change, change, armed.ToString().ToLowerInvariant(), WindowState.Unknown);
            })
            .ToArray();

        Assert.Equal(ObservationSeries.DefaultCap + 1, prefix.Length);

        // *** ZERO FRAMES INSIDE THE PHASE. *** Every one of them reads false, and every one of them is
        // correct to: the phase had not started, or had already ended.
        Assert.All(prefix, f => Assert.Equal("false", f.Value));

        // And this is the verdict that was published: a decisive accusation, 65 of 65, against a block
        // that presented the commanded state for the whole of its armed phase.
        var asItWas = SeriesEvaluation.Evaluate(Id, Signal, "true", prefix,
            new SeriesAccounting(2000, DistinctChanges, prefix.Length, Truncated: false));

        Assert.Equal(AssertionState.Disagreed, asItWas.State);
        Assert.True(asItWas.SaysSomethingAboutTheBlock);
    }

    /// <summary>
    /// 🔴 <b>THE REGRESSION: A LONG INDEX WHOSE ARM WINDOW OPENS AFTER THE CAP NO LONGER YIELDS A VERDICT
    /// FROM PRE-WINDOW FRAMES.</b>
    ///
    /// <para>With no arm window declared — <b>which is the state the five accused signals were in</b> — the
    /// retained sample now spans the phase, so the series is MIXED and the fold has no basis for choosing an
    /// instant. That is <see cref="AssertionState.Inconclusive"/>: weaker than the answer it used to give,
    /// and the answer it used to give was wrong.</para>
    /// </summary>
    [Fact]
    public void WITH_NO_WINDOW_DECLARED_THE_LONG_INDEX_IS_NO_LONGER_AN_ACCUSATION()
    {
        var series = MeasuredIndex();
        var frames = Decode(series, declareWindow: false);

        var outcome = SeriesEvaluation.Evaluate(Id, Signal, "true", frames, Accounting(series));

        Assert.NotEqual(AssertionState.Disagreed, outcome.State);
        Assert.Equal(AssertionState.Inconclusive, outcome.State);
        Assert.False(outcome.SaysSomethingAboutTheBlock);

        // The verdict was taken over frames that include the phase — the property the retention change
        // bought, asserted on the fold's own accounting rather than on the recorder's.
        Assert.True(outcome.Window!.FramesAgreed > 0);
        Assert.True(outcome.Window.SeriesTruncated);
        Assert.True(outcome.Window.RetentionStride > 1);
    }

    /// <summary>
    /// <b>AND WITH THE WINDOW DECLARED IT IS DECISIVE AGAIN, IN THE PASSING DIRECTION.</b> The out-of-window
    /// frames stop counting, every remaining frame agrees, and the block is credited with what it actually
    /// did. This is the half that keeps the change from being "answer less".
    /// </summary>
    [Fact]
    public void WITH_THE_WINDOW_DECLARED_THE_SAME_INDEX_HOLDS()
    {
        var series = MeasuredIndex();
        var frames = Decode(series, declareWindow: true);

        var outcome = SeriesEvaluation.Evaluate(Id, Signal, "true", frames, Accounting(series));

        Assert.Equal(AssertionState.Held, outcome.State);
        Assert.True(outcome.SaysSomethingAboutTheBlock);
        Assert.True(outcome.Window!.FramesConsidered > 0);
        Assert.Equal(outcome.Window.FramesConsidered, outcome.Window.FramesAgreed);
        Assert.True(outcome.Window.FramesOutOfWindow > 0);
    }

    // -------------------------------------------------------------------------------------------------
    // The truncation-accusation rule, in isolation from the retention that makes it rare
    // -------------------------------------------------------------------------------------------------

    private static ObservedFrame[] AllDisagreeing(bool declareWindow, bool windowEverOpen = false) =>
        Enumerable.Range(1, 40)
            .Select(i => new ObservedFrame(i, i, "false",
                declareWindow
                    ? windowEverOpen && i > 30 ? WindowState.InWindow : WindowState.OutOfWindow
                    : WindowState.Unknown))
            .ToArray();

    /// <summary>
    /// 🔴 <b>THE RULING: AN ARGUMENT FROM ABSENCE MAY NOT BE MADE OVER A SAMPLE WHOSE COVERAGE OF THE PHASE
    /// NOBODY CAN STATE.</b>
    ///
    /// <para>This branch is the only verdict in the fold that accuses from what was NOT seen. Every other
    /// accusation names a frame that WAS observed, and a frame that exists cannot be an artifact of dropping
    /// frames — an absence can be, and on the measured run it was.</para>
    /// </summary>
    [Fact]
    public void A_TRUNCATED_SERIES_WITH_NO_DECLARED_WINDOW_MAY_NOT_ACCUSE_FROM_AN_ABSENCE()
    {
        var truncated = new SeriesAccounting(2000, 900, 34, Truncated: true) { Stride = 32 };

        var outcome = SeriesEvaluation.Evaluate(Id, Signal, "true", AllDisagreeing(declareWindow: false), truncated);

        Assert.Equal(AssertionState.NotObserved, outcome.State);
        Assert.False(outcome.SaysSomethingAboutTheBlock);
        Assert.Equal(SeriesEvaluation.PhaseNotCovered, outcome.Observed);

        // It names the repair, and the repair is one field.
        Assert.Contains("MUST NOT BE ACTIONED AGAINST THE BLOCK", outcome.Detail!, StringComparison.Ordinal);
        Assert.Contains("armedBy", outcome.Detail!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE OVER-FIRE CONVERSE, AND IT IS WHAT KEEPS THE RULING HONEST.</b>
    ///
    /// <para>A rule that withheld every absence-accusation would pass the test above and would stop the
    /// harness detecting anything. An UNTRUNCATED series holds EVERY distinct change, so its absence is
    /// real and it accuses exactly as it did before — same frames, same expectation, opposite verdict, and
    /// the only difference is the truncation flag.</para>
    /// </summary>
    [Fact]
    public void AN_UNTRUNCATED_SERIES_STILL_ACCUSES_FROM_THE_SAME_ABSENCE()
    {
        var whole = new SeriesAccounting(2000, 40, 40, Truncated: false);

        var outcome = SeriesEvaluation.Evaluate(Id, Signal, "true", AllDisagreeing(declareWindow: false), whole);

        Assert.Equal(AssertionState.Disagreed, outcome.State);
        Assert.True(outcome.SaysSomethingAboutTheBlock);
    }

    /// <summary>
    /// <b>AND A TRUNCATED SERIES ACCUSES TOO WHERE THE PHASE IS DEMONSTRABLY IN IT.</b> The rule is keyed on
    /// coverage of the phase, not on truncation: with an arm window declared and retained frames taken while
    /// it was open, the absence is over the right part of the index and the verdict stands.
    /// </summary>
    [Fact]
    public void A_TRUNCATED_SERIES_WHOSE_RETAINED_FRAMES_COVER_THE_WINDOW_STILL_ACCUSES()
    {
        var truncated = new SeriesAccounting(2000, 900, 34, Truncated: true) { Stride = 32 };

        var outcome = SeriesEvaluation.Evaluate(Id, Signal, "true",
            AllDisagreeing(declareWindow: true, windowEverOpen: true), truncated);

        Assert.Equal(AssertionState.Disagreed, outcome.State);
        Assert.True(outcome.SaysSomethingAboutTheBlock);
        Assert.True(outcome.Window!.FramesOutOfWindow > 0);
    }

    /// <summary>
    /// <b>A declared window that never opened in ANY retained frame keeps its own, older refusal</b> — a
    /// different token from the truncation one, because "read at the wrong time" and "we cannot show we
    /// looked at the right part of the index" send a reader to different repairs.
    /// </summary>
    [Fact]
    public void A_DECLARED_WINDOW_THAT_NEVER_OPENED_KEEPS_ITS_OWN_DISTINCT_REFUSAL()
    {
        var truncated = new SeriesAccounting(2000, 900, 34, Truncated: true) { Stride = 32 };

        var outcome = SeriesEvaluation.Evaluate(Id, Signal, "true",
            AllDisagreeing(declareWindow: true), truncated);

        Assert.Equal(AssertionState.NotObserved, outcome.State);
        Assert.Equal(SeriesEvaluation.OnlyOutsideTheWindow, outcome.Observed);
        Assert.NotEqual(SeriesEvaluation.PhaseNotCovered, outcome.Observed);
    }

    /// <summary>
    /// ⚠️ <b><c>seriesTruncated: true</c> WAS ALREADY ON EVERY ONE OF THE FALSE-ACCUSATION ROWS.</b> It says
    /// only THAT frames were dropped. The stride says WHICH PART OF THE INDEX the verdict was taken over,
    /// and it travels onto the outcome so a reader meets it beside the verdict rather than in a retention doc.
    /// </summary>
    [Fact]
    public void THE_COVERAGE_TRAVELS_ONTO_THE_OUTCOME_not_merely_the_truncation_flag()
    {
        var truncated = new SeriesAccounting(2000, 900, 34, Truncated: true) { Stride = 32 };

        var outcome = SeriesEvaluation.Evaluate(Id, Signal, "true",
            new[] { new ObservedFrame(1, 1, "true", WindowState.Unknown) }, truncated);

        Assert.Equal(32, outcome.Window!.RetentionStride);
        Assert.Contains("UNIFORM SAMPLE at 1 in 32", outcome.Window.Describe(), StringComparison.Ordinal);
        Assert.Contains("spanning the whole index", outcome.Window.Describe(), StringComparison.Ordinal);
    }
}
