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

        // The cap, plus the always-retained last frame. *** THE ARITHMETIC IS THE FINDING: retained is
        // far below distinct, and both numbers are printed, so nobody can read this as a whole record. ***
        Assert.Equal(ObservationSeries.DefaultCap + 1, series.Frames.Count);
        Assert.True(series.Frames.Count < series.DistinctFrames);

        // The completion instant is present even though the cap bit long before it.
        Assert.Equal((uint)polls, series.Final!.Scan.Raw);
        Assert.Equal(Band(polls, 0), series.Final.Registers);

        Assert.Contains("TRUNCATED", series.Describe(), StringComparison.Ordinal);
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
