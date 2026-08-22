using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// W3 — applying a compression factor to a block's presets, which is the half of time compression that
/// was never built.
///
/// <para>🔴 <b>The planner has been complete for a long time and nothing applied it.</b>
/// <c>TimeCompression.Plan</c> computes <c>comp_min</c> and all four ceilings, gates 10a/10b check them,
/// and <c>ScanBudget</c> re-expresses every declared duration at the factor — while <b>no code anywhere
/// changed a preset on the device.</b> The factor therefore governed only how long the harness was
/// willing to WAIT, and raising it against an unscaled plant produces a spurious TIMED-OUT on a healthy
/// test.</para>
/// </summary>
public class CompressedPresetsTests
{
    private static TimerPreset Data(string name, double ms) => new(name, ms, PresetSource.Data);
    private static TimerPreset Literal(string name, double ms) => new(name, ms, PresetSource.Literal);

    [Fact]
    public void A_data_preset_scales_by_the_factor_and_says_the_arithmetic()
    {
        var table = CompressedPresets.For(new[] { Data("AvgWindow", 2_000) }, factor: 4);

        var preset = Assert.Single(table.Presets);
        Assert.Equal(PresetScaleOutcome.Scaled, preset.Outcome);
        Assert.Equal(500, preset.ScaledMs);
        Assert.True(table.Usable);
        Assert.Contains("2000 ms / 4 = 500 ms", preset.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// The worked case already recorded in <c>CLAUDE.md</c>: a 2-second shortest preset against the ruled
    /// 500 ms absolute floor gives exactly <b>4.0×</b>, and one step further is refused.
    /// </summary>
    [Fact]
    public void The_shortest_preset_sets_the_ceiling_and_one_step_past_it_is_REFUSED()
    {
        Assert.True(CompressedPresets.For(new[] { Data("AvgWindow", 2_000) }, factor: 4).Usable);

        var tooFar = CompressedPresets.For(new[] { Data("AvgWindow", 2_000) }, factor: 5);

        Assert.False(tooFar.Usable);
        var preset = Assert.Single(tooFar.Refused);
        Assert.Equal(PresetScaleOutcome.BelowFloor, preset.Outcome);

        // *** REFUSED, NOT CLAMPED. *** Holding one preset at the floor while its neighbours scale changes
        // the RATIO between them, which is a different plant and not a faster one.
        Assert.Contains("REFUSED RATHER THAN CLAMPED", preset.Detail, StringComparison.Ordinal);

        // And it says the highest factor this preset does admit, so the fix is arithmetic rather than a hunt.
        Assert.Contains("highest factor this preset admits is 4", preset.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A refused preset must never carry its nominal value forward.</b> A deploy reading a number out
    /// of a refused row would apply the UNCOMPRESSED preset while every other row was scaled — the
    /// partial application that makes the plant incoherent.
    /// </summary>
    [Fact]
    public void A_preset_that_could_not_be_scaled_has_NO_value_rather_than_its_original_one()
    {
        var table = CompressedPresets.For(new[] { Data("Short", 1_000), Literal("Debounce", 500) }, factor: 4);

        Assert.All(table.Presets.Where(p => !p.Deployable), p => Assert.True(double.IsNaN(p.ScaledMs)));
        Assert.Empty(table.Deployable.Where(p => double.IsNaN(p.ScaledMs)));
    }

    [Fact]
    public void A_LITERAL_is_never_scaled_and_is_reported_rather_than_silently_dropped()
    {
        // 50 ms literal against a 1000 ms compressed behaviour: 20x headroom, so the ratio bound is clear
        // and this test is about the literal being REPORTED rather than about the bound.
        var table = CompressedPresets.For(new[] { Data("Window", 4_000), Literal("Debounce", 50) }, factor: 4);

        var literal = Assert.Single(table.Literals);
        Assert.Equal("Debounce", literal.Name);
        Assert.True(double.IsNaN(literal.ScaledMs));

        // A literal does not by itself make the table unusable — but a table that omitted it would read as
        // a complete account of the block's timing, and is not.
        Assert.True(table.Usable);
        Assert.Contains("1 literal", table.Summary, StringComparison.Ordinal);
        Assert.Contains("clear of the 10x headroom", table.RatioDetail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>X-D's ratio-distortion bound.</b> A literal does not scale, so compressing the behaviour
    /// around it changes the PROPORTION between them — past a point the block is running a different
    /// design, not a faster one.
    /// </summary>
    [Fact]
    public void Compressing_too_close_to_an_unscaled_LITERAL_is_refused_by_the_ratio_bound()
    {
        // 4000 / 4 = 1000 ms of compressed behaviour against a 500 ms literal. The ruled companion needs
        // 10x, i.e. 5000 ms, so this is refused even though every preset cleared the timer floor.
        var table = CompressedPresets.For(new[] { Data("Window", 4_000), Literal("Debounce", 500) }, factor: 4);

        Assert.Empty(table.Refused);          // no preset broke the FLOOR ...
        Assert.True(table.RatioDistorted);    // ... and the table is still not usable
        Assert.False(table.Usable);
        Assert.Contains("RATIO REFUSED", table.RatioDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void With_no_literal_declared_the_ratio_bound_says_it_did_not_bind_rather_than_staying_silent()
    {
        var table = CompressedPresets.For(new[] { Data("Window", 4_000) }, factor: 4);

        Assert.False(table.RatioDistorted);

        // *** AND IT SAYS WHAT THAT REALLY MEANS. *** "No literal was declared" is a statement about the
        // declaration, not about the block: an undeclared literal in the call tree is one nothing can see.
        Assert.Contains("statement about the DECLARATION", table.RatioDetail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>The trap a real call tree springs.</b> Blocks carry presets that are already under the floor
    /// at 1× — debounces, one-shots — so a blanket scale over everything refuses immediately. This outcome
    /// is kept SEPARATE from <see cref="PresetScaleOutcome.BelowFloor"/> because the fixes differ:
    /// lowering the factor cures that one and can never cure this one.
    /// </summary>
    [Fact]
    public void A_preset_already_under_the_floor_at_comp_ONE_says_that_lowering_the_factor_cannot_help()
    {
        var table = CompressedPresets.For(new[] { Data("Debounce", 200) }, factor: 1);

        var preset = Assert.Single(table.Presets);
        Assert.Equal(PresetScaleOutcome.AlreadyBelowFloor, preset.Outcome);
        Assert.Contains("LOWERING THE FACTOR CANNOT FIX THIS", preset.Detail, StringComparison.Ordinal);
        Assert.False(table.Usable);
    }

    [Fact]
    public void An_UNSTATED_source_is_treated_as_unscalable_because_the_two_answers_push_opposite_ways()
    {
        var table = CompressedPresets.For(new[] { new TimerPreset("Mystery", 4_000, PresetSource.Unstated) }, factor: 2);

        var preset = Assert.Single(table.Presets);
        Assert.NotEqual(PresetScaleOutcome.Scaled, preset.Outcome);
        Assert.Contains("no fail-safe guess", preset.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// Factor 1 is not a no-op to skip. It produces a table saying every preset keeps its nominal value,
    /// which makes "the deploy applied no compression" a positive claim rather than an absence.
    /// </summary>
    [Fact]
    public void Factor_ONE_still_produces_a_table_and_it_is_usable()
    {
        var table = CompressedPresets.For(new[] { Data("Window", 2_000) }, factor: 1);

        Assert.True(table.Usable);
        Assert.Equal(2_000, Assert.Single(table.Deployable).ScaledMs);
        Assert.Contains("EXAMINED: 1 declared preset(s) at comp 1", table.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_declaration_is_not_usable_above_factor_one()
    {
        // Empty is not clean: a table over no presets would deploy an UNCOMPRESSED program while the
        // wave's backstop re-expressed at the factor, and the symptom is a spurious TIMED-OUT.
        Assert.False(CompressedPresets.For(Array.Empty<TimerPreset>(), factor: 4).Usable);

        // The denominator is printed even then — that line is what distinguishes "scaled nothing" from
        // "had nothing to scale".
        Assert.Contains("EXAMINED: 0 declared preset(s)", CompressedPresets.For(Array.Empty<TimerPreset>(), factor: 4).Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_factor_below_one_is_refused_because_it_would_make_the_plant_slower()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CompressedPresets.For(new[] { Data("W", 2_000) }, factor: 0));
    }

    [Fact]
    public void The_floor_comes_from_TimeCompression_and_is_not_restated_here()
    {
        // If the ruled floor ever moves, this table moves with it rather than carrying a second copy of
        // the constant — the failure mode CLAUDE.md records twice for scan period.
        Assert.Equal(TimeCompression.EffectiveTimerFloorMs, CompressedPresets.For(new[] { Data("W", 2_000) }, 2).FloorMs);
    }
}
