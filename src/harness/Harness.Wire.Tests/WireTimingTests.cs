using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// The constants, and the two decisions that are NOT defaults.
///
/// <para>Nothing here re-derives anything: §12a is the only place a timing constant is chosen, and these
/// tests exist so that a change to one of them has to be made deliberately, against the reason.</para>
/// </summary>
public class WireTimingTests
{
    [Fact]
    public void Budgets_are_keyed_on_the_p99_not_the_median()
    {
        // A wave issues round trips in the hundreds, so "one in a hundred" is several per wave. Keyed on
        // the median, a wave set admits three to five times more slots than the link can observe, and the
        // failure is a MISSED ASSERTION REPORTED AS A PASS.
        Assert.Equal(173, WireTiming.RttP99Ms);
        Assert.Equal(78, WireTiming.RttTypicalMs);
        Assert.True(WireTiming.RttP99Ms > WireTiming.RttTypicalMs);
    }

    [Fact]
    public void The_backstop_includes_the_outlier_allowance()
    {
        // §12a derivation 4: without the RTT_max term a healthy test reports TIMED-OUT roughly once every
        // 22 waves, and a spurious TIMED-OUT is worse than a spurious FAILED because it is believed.
        var backstop = WireTiming.BackstopMs(declaredScans: 100, expectedRoundTrips: 2);
        var withoutOutlier = (int)Math.Ceiling(100 * WireTiming.ScanPeriodMs) + (2 * WireTiming.RttP99Ms);

        Assert.Equal(withoutOutlier + WireTiming.RttMaxObservedMs, backstop);
        Assert.True(backstop - withoutOutlier >= 2216);
    }

    [Fact]
    public void The_per_request_timeout_is_above_the_worst_round_trip_ever_measured()
    {
        // Below it, a measured ordinary tail event becomes a transport failure at ~1 request in 2,000 —
        // an intermittent unattributable red.
        Assert.True(WireTiming.PerRequestTimeoutMs > WireTiming.RttMaxObservedMs);
    }

    [Fact]
    public void The_default_policy_turns_NModbuss_retries_OFF()
    {
        // The library defaults this to 3. A retried write is a duplicated vector application; a retried
        // start-bool commit is a SECOND T=0.
        Assert.Equal(0, ModbusPolicy.Default.Retries);
        Assert.Empty(ModbusPolicy.Default.Refusals);
    }

    [Fact]
    public void A_policy_with_retries_on_is_refused_and_says_why()
    {
        var policy = ModbusPolicy.Default with { Retries = 3 };

        Assert.Contains(policy.Refusals, r => r.Contains("SECOND T=0", StringComparison.Ordinal));
    }

    [Fact]
    public void A_policy_with_a_timeout_under_the_measured_outlier_is_refused()
    {
        var policy = ModbusPolicy.Default with { ReadTimeoutMs = 1000, WriteTimeoutMs = 1000 };

        Assert.Equal(2, policy.Refusals.Count);
        Assert.All(policy.Refusals, r => Assert.Contains("2216 ms round trip", r, StringComparison.Ordinal));
    }

    [Fact]
    public void A_negative_duration_is_refused_rather_than_producing_a_shorter_backstop()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WireTiming.BackstopMs(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => WireTiming.BackstopMs(1, -2));
    }

    // ---------------------------------------------------------------------------------------------
    // The word order, which is configurable BECAUSE it is uncalibrated
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_two_word_orders_are_inverses_of_each_other()
    {
        const uint value = 0xA93F2C71;

        var high = RegisterWords.From32(value, RegisterWordOrder.HighWordFirst);
        var low = RegisterWords.From32(value, RegisterWordOrder.LowWordFirst);

        Assert.Equal(new ushort[] { 0xA93F, 0x2C71 }, high);
        Assert.Equal(new ushort[] { 0x2C71, 0xA93F }, low);
        Assert.Equal(value, RegisterWords.To32(high[0], high[1], RegisterWordOrder.HighWordFirst));
        Assert.Equal(value, RegisterWords.To32(low[0], low[1], RegisterWordOrder.LowWordFirst));
        Assert.Equal(RegisterWords.Swapped(value), RegisterWords.To32(high[0], high[1], RegisterWordOrder.LowWordFirst));
    }
}
