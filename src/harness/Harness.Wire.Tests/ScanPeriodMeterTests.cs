using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// 🔴 <b>The scan period stops being only a compiled constant.</b>
///
/// <para><c>WireTiming.ScanPeriodMs</c> is one program's number — 24.931 ms, measured 2026-08-18 — and
/// thirty-odd sites convert scans to milliseconds with it. Its own doc says it is a property of the
/// PROGRAM and must be re-measured whenever the program changes materially, and nothing enforced that.
/// Phase 4 makes a lane something the tool MAKES, so a new lane is a new program inheriting the old
/// figure silently.</para>
/// </summary>
public class ScanPeriodMeterTests
{
    private const uint Stamp = 0x622F3EB7;

    private static readonly DateTimeOffset T0 = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A synthetic series at a known rate: one observation per second, advancing by <paramref name="perSecond"/>.</summary>
    private static ScanPeriodMeter Series(double msPerScan, int seconds, string precondition = "a wave under poll load")
    {
        var meter = new ScanPeriodMeter(Stamp, precondition);
        var perSecond = 1000.0 / msPerScan;

        for (var s = 0; s <= seconds; s++)
            meter.Observe(new ScanCount((uint)Math.Round(s * perSecond)), T0.AddSeconds(s));

        return meter;
    }

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL, AND IT COMES FIRST.</b> A program scanning at the rate the constant was
    /// calibrated against must produce NO disagreement. A check that fires on the very program it was
    /// measured from is off by definition, and would be switched off within a day.
    /// </summary>
    [Fact]
    public void A_program_at_the_calibrated_rate_agrees_and_says_nothing()
    {
        var result = Series(WireTiming.ScanPeriodMs, seconds: 60).Result();

        Assert.NotNull(result);
        Assert.False(result!.DisagreesWithConstant);
        Assert.Equal(WireTiming.ScanPeriodMs, result.MillisecondsPerScan, precision: 1);
        Assert.DoesNotContain("DISAGREES", result.ToString());
    }

    /// <summary>A program scanning at half the rate is named, with both numbers and the direction.</summary>
    [Fact]
    public void A_program_at_HALF_the_rate_disagrees_and_shows_both_numbers()
    {
        var result = Series(WireTiming.ScanPeriodMs * 2, seconds: 60).Result();

        Assert.NotNull(result);
        Assert.True(result!.DisagreesWithConstant);
        Assert.True(result.DeltaFraction > 0.9, $"expected roughly +100%, got {result.DeltaFraction:P}");

        var text = result.ToString();
        Assert.Contains("DISAGREES", text);
        Assert.Contains("compiled constant 24.931 ms", text);
    }

    /// <summary>
    /// 🔴 <b>A window too short is NOT MEASURED — never a value.</b> Two adjacent instants are not a rate,
    /// and a plausible-looking number taken over 300 ms is worse than no number: it would be believed.
    /// </summary>
    [Fact]
    public void A_window_below_the_floor_yields_NO_measurement_and_says_why()
    {
        var meter = new ScanPeriodMeter(Stamp, "a wave under poll load");
        meter.Observe(new ScanCount(0), T0);
        meter.Observe(new ScanCount(40), T0.AddMilliseconds(900));

        Assert.Null(meter.Result());
        Assert.Contains("NOT MEASURED", meter.NotMeasuredBecause());
        Assert.Contains("not a rate", meter.NotMeasuredBecause());
    }

    /// <summary>Too few scans, over a long enough window, is equally not a rate — and says the other reason.</summary>
    [Fact]
    public void Too_few_scans_yields_NO_measurement_and_says_the_OTHER_reason()
    {
        var meter = new ScanPeriodMeter(Stamp, "a wave under poll load");
        meter.Observe(new ScanCount(0), T0);
        meter.Observe(new ScanCount(5), T0.AddSeconds(30));

        Assert.Null(meter.Result());
        Assert.Contains("advanced 5 scan(s)", meter.NotMeasuredBecause());
    }

    /// <summary>
    /// 🔴 <b>A counter that never advances is a STOPPED program, and is reported as one</b> — not folded
    /// into a period, which would silently report an enormous scan time for a PLC that is not scanning.
    /// </summary>
    [Fact]
    public void A_counter_that_never_advances_is_named_as_stopped_not_as_a_slow_scan()
    {
        var meter = new ScanPeriodMeter(Stamp, "a wave under poll load");
        for (var s = 0; s <= 60; s++)
            meter.Observe(new ScanCount(7), T0.AddSeconds(s));

        Assert.Null(meter.Result());
        Assert.Contains("STOPPED or STALLED", meter.NotMeasuredBecause());
    }

    /// <summary>Nothing observed at all is its own reason — distinct from every other absence.</summary>
    [Fact]
    public void No_observations_at_all_says_the_counter_was_never_sampled()
    {
        var meter = new ScanPeriodMeter(Stamp, "a wave under poll load");

        Assert.Null(meter.Result());
        Assert.Contains("never sampled", meter.NotMeasuredBecause());
    }

    /// <summary>
    /// The counter is a 32-bit unsigned that wraps, and a wrap is a legitimate advance. Measuring across
    /// one must not produce a negative or absurd rate — <c>ScanCount.Since</c> is wrap-safe and this pins
    /// that the meter actually relies on it.
    /// </summary>
    [Fact]
    public void A_counter_wrap_is_an_advance_not_a_catastrophe()
    {
        var meter = new ScanPeriodMeter(Stamp, "a wave under poll load");
        meter.Observe(new ScanCount(uint.MaxValue - 100), T0);
        meter.Observe(new ScanCount(300), T0.AddSeconds(10));   // wrapped: 401 scans

        var result = meter.Result();

        Assert.NotNull(result);
        Assert.Equal(401, result!.Scans);
        Assert.True(result.MillisecondsPerScan > 0);
    }

    /// <summary>An implausible jump is ignored rather than trusted — it is corruption, not a scan rate.</summary>
    [Fact]
    public void An_implausible_jump_is_ignored_rather_than_recorded()
    {
        var meter = new ScanPeriodMeter(Stamp, "a wave under poll load");
        meter.Observe(new ScanCount(0), T0);
        meter.Observe(new ScanCount(400), T0.AddSeconds(10));
        // Beyond ScanCount.PlausibleAdvanceCeiling (int.MaxValue) measured FROM 400 — 0x8000_0000 is not,
        // which is why the first draft of this test passed for the wrong reason.
        meter.Observe(new ScanCount(0xC000_0000), T0.AddSeconds(11));

        var result = meter.Result();

        Assert.NotNull(result);
        Assert.Equal(400, result!.Scans);   // the garbage sample did not become the endpoint
    }

    /// <summary>
    /// 🔴 <b>The subject travels with the number.</b> The scan period belongs to a PROGRAM, and this
    /// repository has twice written one program's figure down as a rig fact — once wrong by an order of
    /// magnitude. A measurement whose subject is not attached is that failure, pre-packaged.
    /// </summary>
    [Fact]
    public void The_build_stamp_and_the_precondition_travel_with_the_measurement()
    {
        var result = Series(WireTiming.ScanPeriodMs, seconds: 60, precondition: "idle, no vectors commanded").Result();

        Assert.Equal(Stamp, result!.BuildStamp);
        Assert.Equal("idle, no vectors commanded", result.Precondition);
        Assert.Contains("16#622F3EB7", result.ToString());
    }

    /// <summary>A measurement with no stated condition is refused at construction, not reported blank.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_meter_with_no_stated_condition_is_refused(string precondition)
    {
        Assert.Throws<ArgumentException>(() => new ScanPeriodMeter(Stamp, precondition));
    }

    /// <summary>The denominator is on the rendering, always — a rate over 20 scans is not a rate over 12,000.</summary>
    [Fact]
    public void The_rendering_always_carries_its_denominator_and_window()
    {
        var text = Series(WireTiming.ScanPeriodMs, seconds: 60).Result()!.ToString();

        Assert.Contains("scan(s)", text);
        Assert.Contains(" s,", text);
        Assert.Contains("build stamp", text);
    }
}
