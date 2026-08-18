using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// 🔴 <b>THE RETENTION RULE — and the property that matters is that what it DROPS is reported, never
/// silent.</b>
///
/// <para>A wave has already cost 7,912 round trips, so unbounded history is not an option. The rule is
/// therefore a compromise, and a compromise whose losses are invisible is indistinguishable from a
/// complete record. Every test below is about a denominator.</para>
/// </summary>
public class ObservationSeriesTests
{
    private static ushort[] Band(params int[] values) => values.Select(v => (ushort)v).ToArray();

    [Fact]
    public void CONSECUTIVE_DUPLICATE_FRAMES_ARE_COLLAPSED_and_the_poll_count_still_reports_them_all()
    {
        var recorder = new ObservationRecorder();

        for (var poll = 1; poll <= 20; poll++)
            recorder.Record(new ScanCount((uint)(100 + poll)), poll, Band(7, 0));

        var series = recorder.Build();

        // *** THE COLLAPSE IS WHAT MAKES THE COMMON CASE CHEAP, AND THE DENOMINATOR IS WHAT MAKES IT
        // HONEST. *** One distinct value, but twenty polls really happened and the series says so.
        Assert.Equal(20, series.PollsObserved);
        Assert.Equal(1, series.DistinctFrames);
        Assert.False(series.Truncated);

        // Two frames: the one distinct value, and the LAST poll — whose SCAN is different and is the
        // completion instant every earlier version of this code used.
        Assert.Equal(2, series.Frames.Count);
        Assert.Equal(101u, series.Frames[0].Scan.Raw);
        Assert.Equal(120u, series.Final!.Scan.Raw);
    }

    [Fact]
    public void A_BAND_THAT_NEVER_REPEATS_KEEPS_EVERY_CHANGE_UP_TO_THE_CAP()
    {
        var recorder = new ObservationRecorder();

        for (var poll = 1; poll <= 10; poll++)
            recorder.Record(new ScanCount((uint)poll), poll, Band(poll, 0));

        var series = recorder.Build();

        Assert.Equal(10, series.PollsObserved);
        Assert.Equal(10, series.DistinctFrames);
        Assert.Equal(10, series.Frames.Count);
        Assert.False(series.Truncated);
        Assert.Equal(Band(1, 0), series.Frames[0].Registers);
        Assert.Equal(Band(10, 0), series.Final!.Registers);
    }

    /// <summary>
    /// 🔴 <b>THE CAP IS EXERCISED THROUGH THE REAL CONSTANT, not through an injected one.</b>
    ///
    /// <para>A cap passed in by the test would prove the arithmetic and say nothing about the value the
    /// two production poll loops actually run under — and the whole reason the cap is a constant rather
    /// than a parameter is that two call sites with two caps is how they come to disagree.</para>
    /// </summary>
    [Fact]
    public void PASSING_THE_CAP_IS_REPORTED_AS_TRUNCATION_and_the_last_frame_survives_it()
    {
        var recorder = new ObservationRecorder();
        var polls = ObservationSeries.DefaultCap * 3;

        for (var poll = 1; poll <= polls; poll++)
            recorder.Record(new ScanCount((uint)poll), poll, Band(poll, 0));

        var series = recorder.Build();

        Assert.True(series.Truncated);
        Assert.Equal(polls, series.PollsObserved);
        Assert.Equal(polls, series.DistinctFrames);

        // *** THE ARITHMETIC IS THE FINDING: retained is far below distinct, and both numbers are
        // printed, so nobody can read this as a whole record. *** The bound is unchanged from the prefix
        // rule this replaced — same memory, different frames.
        Assert.True(series.Frames.Count <= ObservationSeries.DefaultCap + 1);
        Assert.True(series.Frames.Count < series.DistinctFrames);

        // The completion instant is present even though the cap bit long before it.
        Assert.Equal((uint)polls, series.Final!.Scan.Raw);
        Assert.Equal(Band(polls, 0), series.Final.Registers);

        Assert.Contains("TRUNCATED", series.Describe(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE RETAINED SET IS A UNIFORM SAMPLE OF THE WHOLE INDEX, NOT A PREFIX OF IT (D1).</b>
    ///
    /// <para>The prefix rule this replaces kept the first 64 distinct frames and dropped every later one,
    /// which on the measured slot bought <b>a fixed ~42–46 seconds of any index</b> — the change rate held
    /// at 1.39–1.52 /s across three very different scenarios, so the frames retained had nothing to do with
    /// the scenario's length and everything to do with its first three quarters of a minute.</para>
    ///
    /// <para>This asserts the property that makes that impossible: the retained frames are evenly spaced
    /// over the entire distinct sequence, and the LAST one before the always-kept completing frame is in
    /// the final <see cref="ObservationSeries.Stride"/> of the index rather than in its first tenth.</para>
    /// </summary>
    [Fact]
    public void THE_RETAINED_FRAMES_ARE_SPREAD_ACROSS_THE_WHOLE_INDEX_never_a_prefix_of_it()
    {
        var recorder = new ObservationRecorder();
        const int distinct = 1000;

        for (var poll = 1; poll <= distinct; poll++)
            recorder.Record(new ScanCount((uint)poll), poll, Band(poll, 0));

        var series = recorder.Build();

        Assert.True(series.Truncated);
        Assert.True(series.Stride > 1);

        // The scan IS the distinct index in this fixture, so the retained scans are the sample.
        var scans = series.Frames.Select(f => (int)f.Scan.Raw).ToArray();

        // The first change is always retained, and the completing frame always is.
        Assert.Equal(1, scans[0]);
        Assert.Equal(distinct, scans[^1]);

        // *** THE SPREAD, STATED AS A GAP BOUND. *** No two consecutive retained frames are more than one
        // stride apart, so no contiguous run of `stride` changes anywhere in the index is unrepresented —
        // which is precisely the structural exclusion the prefix rule guaranteed for the index's tail.
        for (var i = 1; i < scans.Length; i++)
            Assert.True(scans[i] - scans[i - 1] <= series.Stride, $"gap {scans[i - 1]}..{scans[i]} exceeds stride {series.Stride}");

        // And the sample really does reach the end: the last SAMPLED frame (not the appended completing
        // one) is within a stride of the final change. Under the prefix rule it was frame 64 of 1000.
        Assert.True(scans[^2] >= distinct - series.Stride);
    }

    /// <summary>
    /// 🔴 <b>THE MEASURED D1 INDEX, REBUILT: A 369-SECOND INDEX WHOSE OBSERVATION WINDOW OPENS AT
    /// 313.8 s — AND THE PREFIX RULE RETAINED NOT ONE FRAME INSIDE IT.</b>
    ///
    /// <para><b>Published numbers, vector 003 of the measured wave:</b> 369.4 s, 515 distinct changes,
    /// 1.39 /s, window opening at 313.8 s. 64 frames bought 45.9 s. The retained series and the window
    /// <b>missed each other by ~268 seconds</b>, and five signals were folded over the resulting
    /// head-and-tail frames anyway — three of them coming back <c>Disagreed</c>, <b>65 of 65</b>.</para>
    ///
    /// <para>⚠️ <b>THE FIXTURE TRAP IS CLOSED FIRST, and it has to be:</b> the assertions below are
    /// evidence only if the window genuinely lies outside what the OLD rule would have kept. The window
    /// opens well past the cap AND closes before completion — the second half matters because the
    /// completing frame is always retained, so a window still open at the end would have been covered by
    /// accident and this test would prove nothing.</para>
    /// </summary>
    [Fact]
    public void THE_MEASURED_LONG_INDEX_RETAINS_FRAMES_INSIDE_A_WINDOW_THAT_OPENS_LONG_AFTER_THE_CAP()
    {
        // 515 distinct changes; the arm register rises at change 438 (313.8 s of 369.4 s at 1.39 /s) and
        // falls again at 506, because a well-built model returns the block to inert BEFORE it reports
        // completion.
        const int distinct = 515;
        const int windowOpens = 438;
        const int windowCloses = 506;

        // *** THE TRAP, CLOSED. *** The window is entirely outside the first `DefaultCap` changes and is
        // shut again by the completing frame, so the prefix rule's head AND its always-kept tail are both
        // out of window. Retained-in-window under that rule: zero.
        Assert.True(windowOpens > ObservationSeries.DefaultCap);
        Assert.True(windowCloses <= distinct);

        var recorder = new ObservationRecorder();

        for (var change = 1; change <= distinct; change++)
        {
            var armed = change >= windowOpens && change < windowCloses ? 1 : 0;
            recorder.Record(new ScanCount((uint)change), change, Band(change, armed));
        }

        var series = recorder.Build();

        Assert.True(series.Truncated);
        Assert.Equal(distinct, series.DistinctFrames);

        // *** THE FINDING. *** Register 1 is the arm. Under the prefix rule this count was 0.
        var inWindow = series.Frames.Count(f => f.Registers[1] == 1);
        Assert.True(inWindow > 0, "not one retained frame was taken inside the observation window — this is the D1 defect");

        // Not one lucky frame either: the window spans 68 of 515 changes, so a uniform sample must land in
        // it several times over. Asserting a floor rather than an exact count keeps this a statement about
        // the SPREAD and not about the current cap arithmetic.
        Assert.True(inWindow >= 3, $"only {inWindow} retained frame(s) fell inside the window");

        // The completing frame is still there, and it is still OUT of window — the instant every version of
        // this code before the series used, and the one that made the false verdicts look plausible.
        Assert.Equal((uint)distinct, series.Final!.Scan.Raw);
        Assert.Equal(0, series.Final.Registers[1]);
    }

    /// <summary>
    /// ⚠️ <b><c>seriesTruncated: true</c> WAS ON EVERY ONE OF THE ROWS THAT ACCUSED A PRODUCTION BLOCK AND
    /// TOLD NOBODY ANYTHING USEFUL.</b> The flag says THAT frames were dropped; the reader's question is
    /// WHICH ONES. <see cref="ObservationSeries.Coverage"/> answers that, on every series.
    /// </summary>
    [Fact]
    public void THE_SERIES_STATES_WHICH_PART_OF_THE_INDEX_IT_REPRESENTS_not_merely_that_it_was_truncated()
    {
        var recorder = new ObservationRecorder();

        for (var poll = 1; poll <= ObservationSeries.DefaultCap * 8; poll++)
            recorder.Record(new ScanCount((uint)poll), poll, Band(poll, 0));

        var truncated = recorder.Build();

        Assert.True(truncated.Stride > 1);
        Assert.Contains("WHOLE INDEX, SAMPLED 1 IN", truncated.Coverage, StringComparison.Ordinal);
        Assert.Contains($"1 IN {truncated.Stride}", truncated.Coverage, StringComparison.Ordinal);

        // What was given up is RESOLUTION, and the coverage line says so in those words rather than
        // leaving a reader to infer the range was clipped.
        Assert.Contains("RESOLUTION, NOT A PART OF THE INDEX", truncated.Coverage, StringComparison.Ordinal);
        Assert.Contains(truncated.Coverage, truncated.Describe(), StringComparison.Ordinal);

        // *** AND IT PRINTS ON A CLEAN SERIES TOO. *** A coverage line that appears only on bad news
        // teaches its reader that its absence means the question was not asked.
        var whole = new ObservationRecorder();
        whole.Record(new ScanCount(1), 1, Band(1));

        var complete = whole.Build();
        Assert.Equal(1, complete.Stride);
        Assert.Contains("WHOLE INDEX, EVERY CHANGE", complete.Coverage, StringComparison.Ordinal);
        Assert.Contains(complete.Coverage, complete.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void AN_UNTRUNCATED_SERIES_SAYS_SO_TOO_so_the_absence_of_a_warning_is_not_the_absence_of_a_report()
    {
        var recorder = new ObservationRecorder();
        recorder.Record(new ScanCount(5), 1, Band(1));

        var series = recorder.Build();

        // The denominator line prints on EVERY series, not only on the bad ones — a report that appears
        // only on bad news teaches its reader that its absence means it was not run.
        Assert.DoesNotContain("TRUNCATED", series.Describe(), StringComparison.Ordinal);
        Assert.Contains("1 poll round(s)", series.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void NOTHING_OBSERVED_IS_A_POSITIVE_VALUE_and_is_distinguishable_from_a_band_that_never_changed()
    {
        Assert.False(ObservationSeries.Empty.Any);
        Assert.Equal(0, ObservationSeries.Empty.PollsObserved);
        Assert.Null(ObservationSeries.Empty.Final);
        Assert.Contains("NOTHING OBSERVED", ObservationSeries.Empty.Describe(), StringComparison.Ordinal);

        // A band that never changed still ran a poll, and that is a different fact. Empty is not clean.
        var recorder = new ObservationRecorder();
        recorder.Record(new ScanCount(1), 1, Band(0, 0, 0));

        Assert.True(recorder.Build().Any);
        Assert.Equal(1, recorder.Build().PollsObserved);
    }

    [Fact]
    public void A_BAND_THAT_CHANGES_AND_CHANGES_BACK_KEEPS_BOTH_TRANSITIONS()
    {
        // The collapse is CONSECUTIVE-duplicate, not distinct-value: a signal that goes up and comes back
        // down is exactly the shape the whole series exists for, and de-duplicating by VALUE would erase
        // the return.
        var recorder = new ObservationRecorder();

        recorder.Record(new ScanCount(1), 1, Band(0));
        recorder.Record(new ScanCount(2), 2, Band(1));
        recorder.Record(new ScanCount(3), 3, Band(1));
        recorder.Record(new ScanCount(4), 4, Band(0));

        var series = recorder.Build();

        Assert.Equal(3, series.DistinctFrames);
        Assert.Equal(new ushort[] { 0, 1, 0 }, series.Frames.Select(f => f.Registers[0]).ToArray());
    }

    [Fact]
    public void A_CAP_OF_ZERO_IS_REFUSED_because_a_series_that_retains_nothing_is_not_a_series()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ObservationRecorder(0));
    }
}
