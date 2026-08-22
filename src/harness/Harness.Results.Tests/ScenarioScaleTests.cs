using Harness.Results;
using Harness.Wire;
using Xunit;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>Scaling the block without the scenario is not a slower run — it is a DIFFERENT TEST.</b>
///
/// <para>The stimulus model plays its scenario against an IEC timer read in real milliseconds, so
/// compressing the block's presets alone leaves the run exactly as long as before <b>and moves every
/// event the vector placed relative to a window</b>. The verdicts still appear. They are about a test
/// nobody designed.</para>
///
/// <para>Nearly walked into: the obvious-looking fix — scale the model's TICK — turned out to compress
/// nothing at all, because the scenario clock is elapsed time and not a tick count. Worse, the tick sets
/// the dither period from which the model's own stability ceiling is computed, so shortening it removes
/// the term that makes that ceiling calculable.</para>
/// </summary>
public class ScenarioScaleTests
{
    private static SubmissionVector Vector(string id, params (string Name, string Value)[] inputs) =>
        SubmissionGateTests.Vector(id: id) with
        {
            Inputs = inputs.ToDictionary(i => i.Name, i => i.Value, StringComparer.Ordinal),
        };

    private static readonly string[] Coordinates = { "Stim.ArmAt", "Stim.EndAt" };

    [Fact]
    public void Every_declared_coordinate_is_divided_by_the_factor_and_the_report_names_each_one()
    {
        var result = ScenarioScale.Apply(
            new[] { Vector("V-1", ("Stim.ArmAt", "303000"), ("Stim.EndAt", "358000"), ("Stim.WeightKg", "1900")) },
            new RuntimeCompression(4),
            Coordinates);

        Assert.True(result.Ok, result.Report);

        var vector = Assert.Single(result.Vectors);
        Assert.Equal("75750", vector.Inputs["Stim.ArmAt"]);
        Assert.Equal("89500", vector.Inputs["Stim.EndAt"]);

        // *** ONLY THE DECLARED ONES. *** A weight is not a time; scaling everything numeric would be a
        // different and much worse bug than scaling nothing.
        Assert.Equal("1900", vector.Inputs["Stim.WeightKg"]);

        Assert.Contains("358000 -> 89500", result.Report, StringComparison.Ordinal);
    }

    /// <summary>
    /// The vectors keep declaring PLANT time; only what reaches the device is compressed. That split is
    /// what lets gate 1b bound a backstop against its own scenario's end in the units the author wrote.
    /// </summary>
    [Fact]
    public void The_ORIGINAL_vector_is_left_alone_so_the_gates_still_read_what_the_author_declared()
    {
        var original = Vector("V-1", ("Stim.EndAt", "358000"));

        var result = ScenarioScale.Apply(new[] { original }, new RuntimeCompression(4), new[] { "Stim.EndAt" });

        Assert.Equal("358000", original.Inputs["Stim.EndAt"]);
        Assert.Equal("89500", Assert.Single(result.Vectors).Inputs["Stim.EndAt"]);
    }

    [Fact]
    public void At_factor_ONE_nothing_is_re_expressed_and_the_report_SAYS_so_rather_than_being_silent()
    {
        var result = ScenarioScale.Apply(
            new[] { Vector("V-1", ("Stim.EndAt", "358000")) }, RuntimeCompression.Uncompressed, Coordinates);

        Assert.True(result.Ok);
        Assert.Empty(result.Scaled);
        Assert.Equal("358000", Assert.Single(result.Vectors).Inputs["Stim.EndAt"]);
        Assert.Contains("no scenario coordinate is re-expressed", result.Report, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>The failure this whole class exists to prevent</b>, and it is worse than either extreme: the
    /// wave takes exactly as long as before AND the events have moved.
    /// </summary>
    [Fact]
    public void Compressing_with_NO_declaration_is_refused_rather_than_scaling_nothing()
    {
        var result = ScenarioScale.Apply(
            new[] { Vector("V-1", ("Stim.EndAt", "358000")) }, new RuntimeCompression(4), declaredInputs: null);

        Assert.False(result.Ok);
        Assert.Contains("COMPRESSING THE BLOCK WITHOUT THE SCENARIO", string.Join(" ", result.Refusals), StringComparison.Ordinal);

        // And the vectors come back UNTOUCHED, so a caller that ignores the refusal cannot end up with a
        // half-scaled set.
        Assert.Equal("358000", Assert.Single(result.Vectors).Inputs["Stim.EndAt"]);
    }

    [Fact]
    public void An_EMPTY_declaration_is_the_positive_claim_that_this_stimulus_has_no_clock()
    {
        var result = ScenarioScale.Apply(
            new[] { Vector("V-1", ("Demo_Step", "5")) }, new RuntimeCompression(4), Array.Empty<string>());

        // Distinct from omitted, which is NOT CHECKED. A ramp-to-limit stimulus finishes when a count
        // reaches a limit and genuinely has no clock to scale.
        Assert.True(result.Ok, result.Report);
        Assert.Contains("positive claim", result.Report, StringComparison.Ordinal);
    }

    /// <summary>
    /// A vector missing one declared coordinate would run a scenario at MIXED scales — some events
    /// compressed, some not — which is invisible in every artifact and worse than either extreme.
    /// </summary>
    [Fact]
    public void A_vector_missing_ONE_declared_coordinate_is_refused_not_partially_scaled()
    {
        var result = ScenarioScale.Apply(
            new[]
            {
                Vector("V-1", ("Stim.ArmAt", "4000"), ("Stim.EndAt", "40000")),
                Vector("V-2", ("Stim.EndAt", "60000")),
            },
            new RuntimeCompression(4),
            Coordinates);

        Assert.False(result.Ok);
        Assert.Contains("mixed scales", string.Join(" ", result.Refusals), StringComparison.Ordinal);
    }

    /// <summary>
    /// Rounding would move an event without saying so, and these are the coordinates the assertions are
    /// placed against — so a value that does not divide is a refusal, not a nearest-integer.
    /// </summary>
    [Fact]
    public void A_coordinate_that_does_not_divide_is_REFUSED_rather_than_rounded()
    {
        var result = ScenarioScale.Apply(
            new[] { Vector("V-1", ("Stim.EndAt", "358001")) }, new RuntimeCompression(4), new[] { "Stim.EndAt" });

        Assert.False(result.Ok);
        Assert.Contains("does not divide by 4", string.Join(" ", result.Refusals), StringComparison.Ordinal);
    }

    [Fact]
    public void A_declared_name_matching_NOTHING_is_called_out_once_rather_than_once_per_vector()
    {
        var result = ScenarioScale.Apply(
            new[]
            {
                Vector("V-1", ("Stim.EndAt", "40000")),
                Vector("V-2", ("Stim.EndAt", "60000")),
            },
            new RuntimeCompression(4),
            new[] { "Stim.EndAt", "Stim.Typo" });

        Assert.False(result.Ok);

        // The typo explains every per-vector refusal it causes, so it is stated once on its own terms.
        Assert.Contains(result.Refusals, r => r.Contains("no vector in this submission has an input by that name", StringComparison.Ordinal));
    }

    [Fact]
    public void A_non_numeric_coordinate_is_refused_by_name()
    {
        var result = ScenarioScale.Apply(
            new[] { Vector("V-1", ("Stim.EndAt", "T#6M")) }, new RuntimeCompression(4), new[] { "Stim.EndAt" });

        Assert.False(result.Ok);
        Assert.Contains("not a whole number of milliseconds", string.Join(" ", result.Refusals), StringComparison.Ordinal);
    }
}
