using System.Reflection;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// <b>The scan count and the <c>comp</c> it was stated at, and the bound that consumes them.</b>
///
/// <para>The hazard both halves exist for is one sentence: <i>a declaration that was sound when it was
/// written is void when it runs, and nobody edited the vector.</i> It shows up twice with opposite signs —
/// an observation window that crosses the floor as <c>comp</c> RISES, and a timeout that fires on a healthy
/// test as <c>comp</c> FALLS.</para>
/// </summary>
public class ScanBudgetTests
{
    // ---------------------------------------------------------------------------------------------
    // The zero values, which are the trap this project keeps finding
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_compression_factor_of_zero_is_REFUSED_rather_than_dividing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeCompression(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeCompression(-3));
    }

    [Fact]
    public void RuntimeCompression_is_a_REFERENCE_type_so_default_is_null_and_not_a_factor_of_zero()
    {
        // AssertionForm.When = 0 handed an omitted field the permissive form; this is the same shape. A
        // struct here would give default(RuntimeCompression).Factor == 0, which divides.
        Assert.False(typeof(RuntimeCompression).IsValueType);
        Assert.Null(default(RuntimeCompression));

        Assert.False(typeof(ScanBudget).IsValueType);
        Assert.Null(default(ScanBudget));
    }

    [Fact]
    public void A_scan_count_of_zero_or_a_missing_comp_is_REFUSED()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScanBudget(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScanBudget(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScanBudget(20, 0));
    }

    // ---------------------------------------------------------------------------------------------
    // The arithmetic, in both directions
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Twenty_scans_at_comp_one_is_TWO_scans_at_comp_ten()
    {
        // The contract's own worked example, and the reason the declaration carries its factor.
        Assert.Equal(2.0, new ScanBudget(20, 1).At(new RuntimeCompression(10)), 6);
    }

    [Fact]
    public void And_the_OTHER_direction_is_the_one_that_makes_a_HEALTHY_test_time_out()
    {
        // A duration declared at comp=10 takes TEN TIMES as many scans when the wave runs uncompressed.
        // Consumed as though it were the declared figure, the backstop is ten times too short.
        Assert.Equal(200.0, new ScanBudget(20, 10).At(RuntimeCompression.Uncompressed), 6);
    }

    [Fact]
    public void A_BOUND_rounds_UP_because_the_asymmetry_points_that_way()
    {
        // 7 scans at comp 1 run at comp 2 is 3.5 scans. A bound takes 4: a generous timeout costs nothing
        // (it only elapses on a request that has already failed) and a tight one produces a spurious
        // TIMED-OUT, which is believed.
        Assert.Equal(3.5, new ScanBudget(7, 1).At(new RuntimeCompression(2)), 6);
        Assert.Equal(4, new ScanBudget(7, 1).BoundAt(new RuntimeCompression(2)));
    }

    [Fact]
    public void PlantScans_is_the_declaration_at_comp_one()
    {
        Assert.Equal(200.0, new ScanBudget(20, 10).PlantScans, 6);
        Assert.Equal(200.0 * WireTiming.ScanPeriodMs, new ScanBudget(20, 10).PlantMs, 6);
    }

    // ---------------------------------------------------------------------------------------------
    // X-B's backstop
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_backstop_grows_when_the_wave_runs_SLOWER_than_the_declaration_was_written_for()
    {
        var declaredAtTen = new ScanBudget(20, 10);

        var atTen = WireTiming.BackstopMs(declaredAtTen, new RuntimeCompression(10), expectedRoundTrips: 2);
        var atOne = WireTiming.BackstopMs(declaredAtTen, RuntimeCompression.Uncompressed, expectedRoundTrips: 2);

        // The scan term is the only one that moves, and it moves by the factor.
        Assert.Equal((int)Math.Ceiling(20 * WireTiming.ScanPeriodMs), atTen - (2 * WireTiming.RttP99Ms) - WireTiming.RttMaxObservedMs);
        Assert.Equal((int)Math.Ceiling(200 * WireTiming.ScanPeriodMs), atOne - (2 * WireTiming.RttP99Ms) - WireTiming.RttMaxObservedMs);
        Assert.True(atOne > atTen);
    }

    [Fact]
    public void The_middle_term_keys_on_the_p99_and_NEVER_on_the_p90()
    {
        // §12a's F-5 kind rule: a backstop is BOUND-shaped. Substituting RTT_p90 (102.79 ms) would halve
        // the allowance while 10% of round trips exceed that figure BY CONSTRUCTION — on a test issuing 19
        // round trips, roughly two would blow the per-trip assumption every run.
        var backstop = WireTiming.BackstopMs(new ScanBudget(10, 1), RuntimeCompression.Uncompressed, expectedRoundTrips: 19);
        var wireTerm = backstop - (int)Math.Ceiling(10 * WireTiming.ScanPeriodMs) - WireTiming.RttMaxObservedMs;

        Assert.Equal(19 * 201, wireTerm);
        Assert.NotEqual(19 * 103, wireTerm);
    }

    [Fact]
    public void The_outlier_term_is_the_WHOLE_RTT_max_and_is_not_scaled_by_anything()
    {
        // Derivation 4: at §12a's worked wave, one wave in 22 contains a round trip over 2,216 ms. A
        // backstop computed without this term reports TIMED-OUT on a healthy test roughly that often.
        foreach (var comp in new[] { 1, 5, 50 })
        {
            var backstop = WireTiming.BackstopMs(new ScanBudget(50, 50), new RuntimeCompression(comp), expectedRoundTrips: 3);
            var withoutOutlier = (int)Math.Ceiling(new ScanBudget(50, 50).BoundAt(new RuntimeCompression(comp)) * WireTiming.ScanPeriodMs)
                               + (3 * WireTiming.RttP99Ms);

            Assert.Equal(WireTiming.RttMaxObservedMs, backstop - withoutOutlier);
        }
    }

    /// <summary>
    /// <b>The unexpressibility itself, pinned.</b>
    ///
    /// <para>This is the "did not run" case for the guard: every other test here proves the arithmetic is
    /// right when a <c>ScanBudget</c> is passed. None of them would notice an <c>int</c> overload being
    /// added back beside it, and that overload is the entire hole — a caller with a bare scan count would
    /// find a method that takes one.</para>
    /// </summary>
    [Fact]
    public void There_is_NO_overload_of_the_backstop_that_takes_a_BARE_SCAN_COUNT()
    {
        var overloads = typeof(WireTiming)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(WireTiming.BackstopMs))
            .ToArray();

        var overload = Assert.Single(overloads);

        Assert.Equal(typeof(ScanBudget), overload.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(RuntimeCompression), overload.GetParameters()[1].ParameterType);

        Assert.DoesNotContain(overloads, m => m.GetParameters().All(p => p.ParameterType == typeof(int)));
    }
}
