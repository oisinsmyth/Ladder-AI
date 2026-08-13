using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE WIDTH RULE, AND IT EXISTS BECAUSE TRUNCATION IS THE QUIET HAZARD.</b>
///
/// <para><b>Measured on the deliverable vector set: 81 duration values exceed 65 535 ms</b> — ten
/// distinct figures between 70 000 and 120 000. A single-register mapping does not error on those. It
/// wraps: <c>75 000 ms</c> arrives as <c>9 464 ms</c>, every scenario boundary keyed on it fires early,
/// and the run returns <b>a plausible-looking FAIL against a block that did nothing wrong.</b></para>
///
/// <para>*** THE TWO 32-BIT HAZARDS ARE NOT SYMMETRIC. *** A swapped WORD ORDER announces itself — 75 s
/// reads as about 7 days, the scenario never reaches its boundary, the run TIMES OUT. A truncated WIDTH
/// passes quietly with a reasonable-looking number. <b>So the width failure is the one made loud</b>, and
/// the order hazard is left to the rig's calibration step, which is where a loud failure can be read.</para>
/// </summary>
public class MirrorValueFitTests
{
    // ---------------------------------------------------------------------------------------------
    // The measured case
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SEVENTY_FIVE_THOUSAND_MS_IN_AN_INT_IS_REFUSED_AND_THE_REFUSAL_NAMES_WHAT_IT_WOULD_HAVE_BECOME()
    {
        var result = MirrorValueFit.Check("Stim_TotalMs", MirrorValueType.Int, "75000");

        Assert.False(result.Fits);

        // 75 000 mod 65 536 = 9 464. The refusal SAYS SO, because the whole hazard is that 9 464 is an
        // entirely plausible dwell and nobody reading a result would question it.
        Assert.Contains("9464", result.Refusal!, StringComparison.Ordinal);
        Assert.Contains("Stim_TotalMs", result.Refusal!, StringComparison.Ordinal);
        Assert.Contains("REFUSAL AND NEVER A MODULO", result.Refusal!, StringComparison.Ordinal);

        // And it names the remedy rather than only the wall.
        Assert.Contains("declare it Time", result.Refusal!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(70000)]
    [InlineData(75000)]
    [InlineData(90000)]
    [InlineData(120000)]
    public void EVERY_DURATION_ABOVE_THE_SINGLE_REGISTER_CEILING_IS_REFUSED_AS_AN_INT_AND_FITS_AS_A_TIME(int ms)
    {
        // The ten distinct figures in the vector set live between 70 000 and 120 000; these are the ends
        // and two in between. The point of the pair is that the 32-bit path is LOAD-BEARING for them —
        // not that the refusal is nice to have.
        Assert.False(MirrorValueFit.Check("Stim_TotalMs", MirrorValueType.Int, ms.ToString()).Fits);
        Assert.True(MirrorValueFit.Check("Stim_TotalMs", MirrorValueType.Time, ms.ToString()).Fits);
    }

    [Fact]
    public void THE_CEILING_IS_THE_ELEMENTS_OWN_RANGE_not_a_number_restated_here()
    {
        // The bound comes from the element's ADDRESS FORM, which is also where its width comes from — so a
        // new type inherits one consistent limit rather than picking up a second table that could disagree.
        var element = MirrorElements.Require(MirrorValueType.Int);

        Assert.True(MirrorValueFit.Check("x", MirrorValueType.Int, element.Maximum.ToString()).Fits);
        Assert.False(MirrorValueFit.Check("x", MirrorValueType.Int, (element.Maximum + 1).ToString()).Fits);

        Assert.True(MirrorValueFit.Check("x", MirrorValueType.Int, element.Minimum.ToString()).Fits);
        Assert.False(MirrorValueFit.Check("x", MirrorValueType.Int, (element.Minimum - 1).ToString()).Fits);
    }

    [Fact]
    public void A_TIME_HAS_ITS_OWN_CEILING_and_it_is_not_infinite()
    {
        var element = MirrorElements.Require(MirrorValueType.Time);

        Assert.True(MirrorValueFit.Check("x", MirrorValueType.Time, element.Maximum.ToString()).Fits);
        Assert.False(MirrorValueFit.Check("x", MirrorValueType.Time, (element.Maximum + 1).ToString()).Fits);

        // Nothing wider exists, so the refusal says that rather than offering a remedy that does not exist.
        var refusal = MirrorValueFit.Check("x", MirrorValueType.Time, (element.Maximum + 1).ToString()).Refusal!;
        Assert.Contains("no wider element", refusal, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The other ways a value fails to be a value
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_BOOL_TAKES_true_false_or_1_0_and_refuses_anything_that_would_have_to_be_GUESSED()
    {
        foreach (var yes in new[] { "true", "True", "1" })
            Assert.Equal(1, MirrorValueFit.Check("Flag", MirrorValueType.Bool, yes).Value);

        foreach (var no in new[] { "false", "FALSE", "0" })
            Assert.Equal(0, MirrorValueFit.Check("Flag", MirrorValueType.Bool, no).Value);

        var refused = MirrorValueFit.Check("Flag", MirrorValueType.Bool, "yes");
        Assert.False(refused.Fits);
        Assert.Contains("not a boolean", refused.Refusal!, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_ABSENT_VALUE_IS_REFUSED_rather_than_defaulted_to_zero()
    {
        // Zero is a value the block could legitimately be driven with, so writing one on the author's
        // behalf would be inventing the stimulus — the same shape as the missing-predicate defect.
        var result = MirrorValueFit.Check("Stim_Step", MirrorValueType.Int, "   ");

        Assert.False(result.Fits);
        Assert.Contains("An absent value is not a zero one", result.Refusal!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_VALUE_THAT_IS_NOT_A_NUMBER_IS_REFUSED_rather_than_best_efforted()
    {
        var result = MirrorValueFit.Check("Stim_Step", MirrorValueType.Int, "about 5");

        Assert.False(result.Fits);
        Assert.Contains("not an integer", result.Refusal!, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_UNSUPPORTED_TYPE_CANNOT_BE_RANGE_CHECKED_and_says_so()
    {
        var result = MirrorValueFit.Check("Stim_Step", MirrorValueType.Unstated, "5");

        Assert.False(result.Fits);
        Assert.Contains("cannot mirror", result.Refusal!, StringComparison.Ordinal);
        Assert.Contains("no width", result.Refusal!, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Over a whole binding
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void CheckAll_names_every_offender_and_leaves_UNDRIVEN_inputs_alone()
    {
        var signals = new[]
        {
            MirroredSignal.Int("Stim_Step"),
            MirroredSignal.Int("Stim_TotalMs"),
            MirroredSignal.Time("Stim_WindowMs"),
            MirroredSignal.Int("Stim_Untouched"),
        };

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Stim_Step"] = "5",
            ["Stim_TotalMs"] = "75000",
            ["Stim_WindowMs"] = "120000",
            // Stim_Untouched is deliberately absent: leaving an input undriven is a legitimate choice and
            // belongs to the inert declaration, not to the width rule.
        };

        var refusals = MirrorValueFit.CheckAll(signals, values);

        var only = Assert.Single(refusals);
        Assert.Contains("Stim_TotalMs", only, StringComparison.Ordinal);
        Assert.DoesNotContain("Stim_WindowMs", only, StringComparison.Ordinal);
        Assert.DoesNotContain("Stim_Untouched", only, StringComparison.Ordinal);
    }
}
