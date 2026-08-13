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
    public void Widening_the_VECTOR_region_leaves_the_round_trip_cost_of_an_index_unchanged()
    {
        // Reads key on the RESULT region, so vector width still appears nowhere except inside a ceil
        // that is 1 for every legal slot. Marginal cost per register is ~0.040 ms — negligible and NOT
        // zero — and a future edit that reintroduced register-thrift on the write side fails here.
        var narrow = WireTiming.RoundTripsPerIndex(slots: 4, vectorRegistersPerSlot: 1, resultRegistersPerSlot: 20, pollRounds: 2);
        var wide = WireTiming.RoundTripsPerIndex(slots: 4, vectorRegistersPerSlot: 123, resultRegistersPerSlot: 20, pollRounds: 2);

        Assert.Equal(narrow, wide);
    }

    [Fact]
    public void Widening_the_RESULT_region_CAN_raise_the_cost_and_before_F1_that_was_false()
    {
        // *** THE ONE CLAIM F-1 FALSIFIES, INVERTED RATHER THAN DELETED. *** Until 2026-08-13 the read
        // term was P x K and no width could touch it. It is now P x ceil(K / R) with R = floor(125 / Wr),
        // so slot width has entered the round-trip count IN THE DENOMINATOR. Padding a slot is no longer
        // free: widening it far enough to drop R costs a WHOLE round trip per read per slot-group.
        var sized = WireTiming.RoundTripsPerIndex(slots: 6, vectorRegistersPerSlot: 4, resultRegistersPerSlot: 20, pollRounds: 2);
        var padded = WireTiming.RoundTripsPerIndex(slots: 6, vectorRegistersPerSlot: 4, resultRegistersPerSlot: 123, pollRounds: 2);

        Assert.Equal(6 * 1 + 2 * 1 + 1, sized);      // R = 6, so all six slots in ONE read per round
        Assert.Equal(6 * 1 + 2 * 6 + 1, padded);     // R = 1, so six reads per round
        Assert.True(padded > sized);
    }

    [Fact]
    public void Adding_one_slot_raises_the_cost_by_its_own_write_and_only_sometimes_by_a_read()
    {
        // The read term is a STEP, not a slope: a slot added inside an existing group rides along free,
        // and the one that crosses an R boundary pays a whole round trip. That is why F-1's factor is
        // diluted in wave duration and undiluted only in O11's cap.
        var five = WireTiming.RoundTripsPerIndex(5, 10, 25, 2);   // R = 5: one read per round
        var six = WireTiming.RoundTripsPerIndex(6, 10, 25, 2);    // R = 5: two reads per round

        Assert.Equal(5 + 2 + 1, five);
        Assert.Equal(6 + 4 + 1, six);

        var four = WireTiming.RoundTripsPerIndex(4, 10, 25, 2);
        Assert.Equal(1, five - four);                             // rides along inside the group
        Assert.Equal(3, six - five);                              // crosses the boundary
    }

    [Fact]
    public void A_slot_past_the_FC16_limit_costs_a_second_write()
    {
        Assert.Equal(WireTiming.RoundTripsPerIndex(1, 123, 20, 1), WireTiming.RoundTripsPerIndex(1, 1, 20, 1));
        Assert.Equal(WireTiming.RoundTripsPerIndex(1, 123, 20, 1) + 1, WireTiming.RoundTripsPerIndex(1, 124, 20, 1));
    }

    [Fact]
    public void The_write_term_is_untouched_by_F1_which_is_where_A1s_guarantee_lives()
    {
        // F-1 is a READ-side change. Nothing about it batches writes differently, and A1's "no tear
        // observed" was measured at 16 registers per FC16 with one request never split — evidence that
        // does not stretch merely because reads got cheaper.
        foreach (var resultWidth in new[] { 1, 12, 20, 25, 62, 123 })
        {
            var writes = WireTiming.RoundTripsPerIndex(4, 200, resultWidth, pollRounds: 0) - 1;
            Assert.Equal(8, writes);   // 4 slots x ceil(200/123) = 8, at every read width
        }
    }

    // ---------------------------------------------------------------------------------------------
    // O11's cap — where F-1's factor lands undiluted
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 10, 1)]      // W = 123, the badly-sized map
    [InlineData(5, 10, 5)]      // W = 25
    [InlineData(6, 10, 6)]      // W = 20 — SIX slots where a padded one admits ONE
    [InlineData(10, 10, 10)]    // W = 12
    [InlineData(1, 100, 11)]
    [InlineData(6, 100, 66)]
    public void The_cap_is_R_times_the_old_one(int slotsPerRead, int sMinScans, int expected)
    {
        Assert.Equal(expected, WireTiming.MaxTensorWidth(slotsPerRead, sMinScans));
    }

    [Fact]
    public void The_cap_is_proportional_to_slots_per_read_at_every_window()
    {
        foreach (var window in new[] { 10, 20, 50, 100, 200 })
        {
            var one = WireTiming.MaxTensorWidth(1, window);
            Assert.Equal(6 * one, WireTiming.MaxTensorWidth(6, window));
        }
    }

    [Fact]
    public void An_unobservable_window_is_refused_rather_than_capped_at_zero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WireTiming.MaxTensorWidth(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => WireTiming.MaxTensorWidth(0, 10));
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
