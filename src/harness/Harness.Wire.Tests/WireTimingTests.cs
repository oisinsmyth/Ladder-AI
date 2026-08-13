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
        Assert.Equal(201, WireTiming.RttP99Ms);
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

    // ---------------------------------------------------------------------------------------------
    // Width is not a poll budget, and the arithmetic has to say so mechanically
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Widening_every_slot_leaves_the_round_trip_cost_of_an_index_unchanged()
    {
        // Registers appear nowhere in the cost model except inside a ceil that is 1 for every legal slot.
        // Marginal cost per register is ~0.040 ms — negligible and NOT zero — but nothing derives a poll
        // budget from it, and a future edit that reintroduced register-thrift fails here.
        var narrow = WireTiming.RoundTripsPerIndex(slots: 4, vectorRegistersPerSlot: 1, pollRounds: 2);
        var wide = WireTiming.RoundTripsPerIndex(slots: 4, vectorRegistersPerSlot: 123, pollRounds: 2);

        Assert.Equal(narrow, wide);
    }

    [Fact]
    public void Adding_one_slot_raises_the_cost_of_an_index_by_the_poll_rounds_plus_its_own_write()
    {
        var four = WireTiming.RoundTripsPerIndex(4, 10, 2);
        var five = WireTiming.RoundTripsPerIndex(5, 10, 2);

        Assert.Equal(3, five - four);   // one write + two poll reads
        Assert.Equal(13, four);         // 4 x (1 + 2) + 1, exactly section 12a derivation 2
    }

    [Fact]
    public void A_slot_past_the_FC16_limit_costs_a_second_write_and_that_is_the_only_way_width_shows_up()
    {
        Assert.Equal(WireTiming.RoundTripsPerIndex(1, 123, 1), WireTiming.RoundTripsPerIndex(1, 1, 1));
        Assert.Equal(WireTiming.RoundTripsPerIndex(1, 123, 1) + 1, WireTiming.RoundTripsPerIndex(1, 124, 1));
    }

    [Fact]
    public void The_marginal_cost_of_a_register_is_recorded_as_negligible_and_NOT_zero()
    {
        // The bare phrase "marginal cost per register is zero" may no longer be said: it is ~0.040 ms,
        // ~6% of a round trip at full width, against 100% for a second round trip.
        Assert.Equal(0.040, WireTiming.MarginalCostPerRegisterMs);

        var fullWidth = WireTiming.MarginalCostPerRegisterMs * Harness.Map.ModbusLimits.MaxWriteRegisters;
        Assert.InRange(fullWidth / WireTiming.RttTypicalMs, 0.04, 0.08);
    }

    [Fact]
    public void The_observability_floor_scales_with_the_number_of_slots_not_with_their_width()
    {
        // 8.6 scans at K=1 (was 7.4 at RTT_p99 = 173), so a sampled level must persist for 9 scans — and
        // for 9 x K in a K-slot tensor. This is the number a two-slot wave has to respect.
        Assert.Equal(201 / 23.33, WireTiming.ObservabilityFloorScans(1), 3);
        Assert.Equal(2 * WireTiming.ObservabilityFloorScans(1), WireTiming.ObservabilityFloorScans(2), 6);
        Assert.Throws<ArgumentOutOfRangeException>(() => WireTiming.ObservabilityFloorScans(0));
    }

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
