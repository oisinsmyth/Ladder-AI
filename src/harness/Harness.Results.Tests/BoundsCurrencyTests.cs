using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// AMB-19's comparison, on its own — <b>the one axis the assertion-ID scheme deliberately does not
/// cover.</b>
///
/// <para>Every ruling that built the hole is correct in isolation: no number belongs in a hashed
/// assertion text, and retuning the bounds table therefore re-hashes nothing. The consequence is that a
/// retune changes what many assertions are TRUE OF while moving no ID — so nothing that keys on IDs can
/// see it, and everything in this harness keys on IDs.</para>
/// </summary>
public class BoundsCurrencyTests
{
    private static readonly IReadOnlyDictionary<string, string> Table =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["persistence_threshold"] = "T#60S",
            ["clear_debounce_filter_time"] = "T#2S",
        };

    private static IReadOnlyDictionary<string, string> Used(params (string Name, string Value)[] entries) =>
        entries.ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);

    [Fact]
    public void Matching_values_are_CURRENT_and_the_finding_names_what_it_compared()
    {
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "T#60S")), Table);

        Assert.Equal(BoundsCurrencyState.Current, result.State);
        Assert.True(result.Checked);
        Assert.False(result.PremiseOutOfDate);
        Assert.Equal(new[] { "persistence_threshold = T#60S" }, result.Agreed);
    }

    [Fact]
    public void A_RETUNED_value_is_STALE_and_the_finding_carries_BOTH_numbers()
    {
        var retuned = new Dictionary<string, string>(StringComparer.Ordinal) { ["persistence_threshold"] = "T#90S" };

        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "T#60S")), retuned);

        Assert.Equal(BoundsCurrencyState.Stale, result.State);
        Assert.True(result.PremiseOutOfDate);

        // A finding that says only THAT something differs sends the reader back to two documents. Both
        // values, in the finding, is what makes it actionable in one read.
        var disagreement = Assert.Single(result.Disagreements);
        Assert.Equal("persistence_threshold", disagreement.Bound);
        Assert.Equal("T#60S", disagreement.DeclaredValue);
        Assert.Equal("T#90S", disagreement.SpecifiedValue);
    }

    [Fact]
    public void A_STALE_finding_says_it_is_NOT_a_defect_in_the_block()
    {
        var retuned = new Dictionary<string, string>(StringComparer.Ordinal) { ["persistence_threshold"] = "T#90S" };

        var detail = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "T#60S")), retuned).Detail;

        Assert.Contains("STALE, NOT FAILED", detail, StringComparison.Ordinal);
        Assert.Contains("NOT A DEFECT IN THE BLOCK", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_vector_that_declares_NOTHING_is_NOT_DECLARED_rather_than_current()
    {
        // The hole itself. Both null and empty are the same claim — the vector said nothing — and
        // neither may read as agreement.
        foreach (var declared in new IReadOnlyDictionary<string, string>?[] { null, Used() })
        {
            var result = BoundsCurrencyCheck.Evaluate("V-1", declared, Table);

            Assert.Equal(BoundsCurrencyState.NotDeclared, result.State);
            Assert.False(result.Checked);

            // NotDeclared is deliberately NOT PremiseOutOfDate: it is unknown-currency, not known-stale,
            // and conflating them would report a vector as testing the wrong number when nobody knows.
            Assert.False(result.PremiseOutOfDate);
        }
    }

    [Fact]
    public void An_ABSENT_TABLE_is_NO_TABLE_even_when_the_vector_declared_a_bound()
    {
        // Order matters: reporting NotDeclared here would name the wrong repair. The vector did its part.
        foreach (var table in new IReadOnlyDictionary<string, string>?[] { null, Used() })
        {
            var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "T#60S")), table);

            Assert.Equal(BoundsCurrencyState.NoTable, result.State);
            Assert.False(result.Checked);
        }
    }

    [Fact]
    public void A_bound_the_table_never_had_is_UNKNOWN_and_UNKNOWN_OUTRANKS_STALE()
    {
        // A broken reference has to be repaired before a question about its value can even be asked, so
        // a submission carrying both reports the reference problem rather than the value one.
        var result = BoundsCurrencyCheck.Evaluate(
            "V-1",
            Used(("persistence_threshold", "T#1S"), ("settle_time", "T#3S")),
            Table);

        Assert.Equal(BoundsCurrencyState.Unknown, result.State);
        Assert.True(result.PremiseOutOfDate);
        Assert.Equal(2, result.Disagreements.Count);

        var unknown = result.Disagreements.Single(d => d.Bound == "settle_time");
        Assert.Null(unknown.SpecifiedValue);
        Assert.Contains("has no such bound", unknown.ToString(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The asymmetry: lax where laxity fails CLOSED, strict where laxity would fail OPEN
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_bound_NAME_is_matched_case_insensitively_because_getting_it_wrong_only_refuses()
    {
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("Persistence_Threshold", "T#60S")), Table);

        Assert.Equal(BoundsCurrencyState.Current, result.State);
    }

    [Fact]
    public void A_bound_VALUE_is_matched_with_CASE_PRESERVED_because_laxity_there_would_PASS()
    {
        // The direction this check may never move in. Case-folding the value is a small convenience that
        // buys the ability to call two different strings equal, and the whole gate is about not doing that.
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "t#60s")), Table);

        Assert.Equal(BoundsCurrencyState.Stale, result.State);
    }

    [Fact]
    public void Values_are_normalised_by_the_SAME_rule_the_assertion_IDs_use_and_by_nothing_else()
    {
        // Trim, collapse internal whitespace, strip one trailing '.' or ';'. Reused rather than
        // re-invented, so there is one normalisation in this codebase and not two that drift.
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "  T#60S.  ")), Table);

        Assert.Equal(BoundsCurrencyState.Current, result.State);
    }

    [Fact]
    public void EQUIVALENT_DURATIONS_ARE_REPORTED_AS_DIFFERENT_and_that_is_deliberate()
    {
        // T#60S and T#1M are the same interval. Teaching the comparator to know that would be teaching it
        // to equate things, which is how a comparator starts passing — and the cost of the strictness is
        // one human reading two values that the report prints side by side.
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "T#1M")), Table);

        Assert.Equal(BoundsCurrencyState.Stale, result.State);
        Assert.Contains("T#1M", result.Detail, StringComparison.Ordinal);
        Assert.Contains("T#60S", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unnamed_vector_is_LABELLED_rather_than_producing_a_blank_finding()
    {
        var result = BoundsCurrencyCheck.Evaluate("  ", null, Table);

        Assert.Equal("<unnamed vector>", result.VectorId);
    }
}
