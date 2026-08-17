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

    /// <summary>
    /// The enumeration says nothing about which bounds an assertion depends on. <b>Only consulted for the
    /// EMPTY claim</b>, so every test about a DECLARED bound passes this and is unaffected by it.
    /// </summary>
    private static readonly AssertionBoundsExpectation NoRelation =
        AssertionBoundsExpectation.NotStated("this fixture states no per-assertion bounds relation");

    /// <summary>The enumeration positively states that the cited assertion depends on the named bounds — none, if none are named.</summary>
    private static AssertionBoundsExpectation Says(string assertionId, params string[] bounds) =>
        AssertionBoundsExpectation.Stated(assertionId, bounds.ToHashSet(StringComparer.Ordinal));

    [Fact]
    public void Matching_values_are_CURRENT_and_the_finding_names_what_it_compared()
    {
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "T#60S")), Table, NoRelation);

        Assert.Equal(BoundsCurrencyState.Current, result.State);
        Assert.True(result.Checked);
        Assert.False(result.PremiseOutOfDate);
        Assert.Equal(new[] { "persistence_threshold = T#60S" }, result.Agreed);
    }

    [Fact]
    public void A_RETUNED_value_is_STALE_and_the_finding_carries_BOTH_numbers()
    {
        var retuned = new Dictionary<string, string>(StringComparer.Ordinal) { ["persistence_threshold"] = "T#90S" };

        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "T#60S")), retuned, NoRelation);

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

        var detail = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "T#60S")), retuned, NoRelation).Detail;

        Assert.Contains("STALE, NOT FAILED", detail, StringComparison.Ordinal);
        Assert.Contains("NOT A DEFECT IN THE BLOCK", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_vector_that_SAYS_NOTHING_AT_ALL_is_NOT_DECLARED_rather_than_current()
    {
        // The hole itself: silence. *** THIS TEST USED TO LOOP OVER { null, Used() } AND ASSERT THE SAME
        // STATE FOR BOTH *** — it pinned the conflation as the contract. The property it was really
        // guarding is kept and is asserted here and in the empty-claim tests below: NEITHER may read as
        // agreement. What changed is that they are no longer the same NON-agreement.
        var result = BoundsCurrencyCheck.Evaluate("V-1", null, Table, NoRelation);

        Assert.Equal(BoundsCurrencyState.NotDeclared, result.State);
        Assert.False(result.Checked);

        // NotDeclared is deliberately NOT PremiseOutOfDate: it is unknown-currency, not known-stale,
        // and conflating them would report a vector as testing the wrong number when nobody knows.
        Assert.False(result.PremiseOutOfDate);
    }

    [Fact]
    public void SILENCE_AND_THE_EMPTY_CLAIM_ARE_DIFFERENT_STATES_and_that_is_the_whole_fix()
    {
        // Two calls differing ONLY in null versus empty, against an enumeration that confirms the cited
        // assertion is unbounded. If a future edit collapses them again — the obvious `is null or Count ==
        // 0` — this is the test that goes red, and it goes red on the difference rather than on a message.
        var silent = BoundsCurrencyCheck.Evaluate("V-1", null, Table, Says("REQ-1.a"));
        var claimed = BoundsCurrencyCheck.Evaluate("V-1", Used(), Table, Says("REQ-1.a"));

        Assert.Equal(BoundsCurrencyState.NotDeclared, silent.State);
        Assert.Equal(BoundsCurrencyState.NoBoundsCited, claimed.State);
        Assert.NotEqual(silent.State, claimed.State);

        // And they land on opposite sides of the only question a caller asks.
        Assert.False(silent.Checked);
        Assert.True(claimed.Checked);
    }

    // ---------------------------------------------------------------------------------------------
    // `boundsUsed: {}` — THE POSITIVE CLAIM, AND ITS THREE ANSWERS
    //
    // Measured 2026-08-17: two vectors on a real submission cited assertions that genuinely carry no
    // bound, recorded that truthfully, and were refused — leaving INVENTING A BOUND as the only way to
    // pass. A gate satisfiable only by making something up is inverted.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void An_EMPTY_claim_the_enumeration_CONFIRMS_is_CHECKED_and_PASSES()
    {
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(), Table, Says("REQ-1.a"));

        Assert.Equal(BoundsCurrencyState.NoBoundsCited, result.State);
        Assert.True(result.Checked);
        Assert.False(result.PremiseOutOfDate);

        // A pass that does not name its authority is a claim taken on trust wearing a verdict's clothes.
        Assert.Equal("REQ-1.a", result.CitedAssertion);
        Assert.Contains("VERIFIED against the enumeration, not taken from the vector", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_EMPTY_claim_NOBODY_CAN_VERIFY_is_NOT_CHECKED_and_names_the_reason_and_the_REPAIR()
    {
        // The enumeration states no per-assertion relation, so the claim is unverifiable. It must not
        // pass — a declaration is a transferred responsibility, not a verification — and it must not send
        // the author off to invent a number either.
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(), Table,
            AssertionBoundsExpectation.NotStated("the enumeration states no per-assertion bounds relation at all"));

        Assert.Equal(BoundsCurrencyState.NoBoundsClaimUnverified, result.State);
        Assert.False(result.Checked);
        Assert.False(result.PremiseOutOfDate);

        Assert.Contains("no per-assertion bounds relation at all", result.Detail, StringComparison.Ordinal);
        Assert.Contains("THE REPAIR IS TO THE ENUMERATION, NEVER TO THE VECTOR", result.Detail, StringComparison.Ordinal);
        Assert.Contains("DO NOT add a bound to the vector", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_EMPTY_claim_the_enumeration_CONTRADICTS_is_REFUSED_and_it_is_the_serious_one()
    {
        // *** THE CHECK THAT MAKES THIS A FIX AND NOT A HOLE. *** The vector asserts that no number
        // applies; the enumeration says its assertion depends on one. That is a vector written against a
        // bound nobody looked at.
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(), Table, Says("REQ-1.a", "persistence_threshold"));

        Assert.Equal(BoundsCurrencyState.BoundsOmitted, result.State);
        Assert.False(result.Checked);

        // Refused as a VECTOR defect — Stale, never Fail. The block is not accused of anything.
        Assert.True(result.PremiseOutOfDate);

        var disagreement = Assert.Single(result.Disagreements);
        Assert.Equal("persistence_threshold", disagreement.Bound);
        Assert.Equal("<none declared>", disagreement.DeclaredValue);
        Assert.Equal("T#60S", disagreement.SpecifiedValue);
        Assert.Contains("THE BLOCK IS NOT ACCUSED OF ANYTHING HERE", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_EMPTY_claim_against_NO_TABLE_stays_NO_TABLE_because_empty_is_not_clean()
    {
        // The route by which this fix could have become a hole: declare nothing on both sides and sail
        // through. An enumeration with no bounds table is not one whose assertions have no bounds.
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(), null, Says("REQ-1.a"));

        Assert.Equal(BoundsCurrencyState.NoTable, result.State);
        Assert.False(result.Checked);
    }

    [Fact]
    public void An_UNVERIFIABLE_expectation_must_carry_a_REASON_or_it_cannot_be_constructed()
    {
        // "Could not be checked" without "because" is a dead end for whoever has to repair it, so the
        // absence is not constructible without one.
        Assert.Throws<ArgumentException>(() => AssertionBoundsExpectation.NotStated("  "));
    }

    [Fact]
    public void An_assertion_MISSING_from_the_relation_is_an_ABSENCE_not_an_assertion_with_no_bounds()
    {
        // Absent-versus-empty at the second level. The relation exists and simply does not mention this
        // assertion; reading that as "so it has none" would let an incomplete relation license every
        // empty claim in the submission.
        var enumeration = AssertionEnumeration.Of(
            new[] { "REQ-1" },
            new[] { "REQ-1.a", "REQ-1.b" },
            bounds: Table,
            assertionBounds: new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["REQ-1.a"] = new HashSet<string>(StringComparer.Ordinal),
            });

        var mentioned = BoundsCurrencyCheck.Evaluate("V-1", Used(), Table, enumeration.BoundsExpectationFor("REQ-1.a"));
        var missing = BoundsCurrencyCheck.Evaluate("V-2", Used(), Table, enumeration.BoundsExpectationFor("REQ-1.b"));

        Assert.Equal(BoundsCurrencyState.NoBoundsCited, mentioned.State);
        Assert.Equal(BoundsCurrencyState.NoBoundsClaimUnverified, missing.State);
        Assert.Contains("an assertion missing from the relation is an ABSENCE", missing.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_vector_that_cites_NO_assertion_cannot_have_its_empty_claim_verified()
    {
        var enumeration = AssertionEnumeration.Of(
            new[] { "REQ-1" },
            new[] { "REQ-1.a" },
            bounds: Table,
            assertionBounds: new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["REQ-1.a"] = new HashSet<string>(StringComparer.Ordinal),
            });

        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(), Table, enumeration.BoundsExpectationFor(null));

        Assert.Equal(BoundsCurrencyState.NoBoundsClaimUnverified, result.State);
        Assert.False(result.Checked);
    }

    [Fact]
    public void The_three_PREMISE_refusals_give_THREE_DIFFERENT_repairs_and_never_the_wrong_one()
    {
        // A headline that misnames the repair sends the reader to the wrong document with confidence.
        // "Written against a bound the specification no longer states" is simply FALSE of a vector that
        // declared no bound at all.
        var retuned = new Dictionary<string, string>(StringComparer.Ordinal) { ["persistence_threshold"] = "T#90S" };

        var stale = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "T#60S")), retuned, NoRelation);
        var unknown = BoundsCurrencyCheck.Evaluate("V-1", Used(("settle_time", "T#3S")), Table, NoRelation);
        var omitted = BoundsCurrencyCheck.Evaluate("V-1", Used(), Table, Says("REQ-1.a", "persistence_threshold"));

        Assert.All(new[] { stale, unknown, omitted }, f => Assert.True(f.PremiseOutOfDate));

        Assert.Contains("NO LONGER STATES", stale.PremiseHeadline, StringComparison.Ordinal);
        Assert.Contains("DOES NOT CONTAIN", unknown.PremiseHeadline, StringComparison.Ordinal);
        Assert.Contains("written against a bound nobody looked at", omitted.PremiseHeadline, StringComparison.Ordinal);

        Assert.Equal(3, new[] { stale.PremiseHeadline, unknown.PremiseHeadline, omitted.PremiseHeadline }.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void An_ABSENT_TABLE_is_NO_TABLE_even_when_the_vector_declared_a_bound()
    {
        // Order matters: reporting NotDeclared here would name the wrong repair. The vector did its part.
        foreach (var table in new IReadOnlyDictionary<string, string>?[] { null, Used() })
        {
            var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "T#60S")), table, NoRelation);

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
            Table,
            NoRelation);

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
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("Persistence_Threshold", "T#60S")), Table, NoRelation);

        Assert.Equal(BoundsCurrencyState.Current, result.State);
    }

    [Fact]
    public void A_bound_VALUE_is_matched_with_CASE_PRESERVED_because_laxity_there_would_PASS()
    {
        // The direction this check may never move in. Case-folding the value is a small convenience that
        // buys the ability to call two different strings equal, and the whole gate is about not doing that.
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "t#60s")), Table, NoRelation);

        Assert.Equal(BoundsCurrencyState.Stale, result.State);
    }

    [Fact]
    public void Values_are_normalised_by_the_SAME_rule_the_assertion_IDs_use_and_by_nothing_else()
    {
        // Trim, collapse internal whitespace, strip one trailing '.' or ';'. Reused rather than
        // re-invented, so there is one normalisation in this codebase and not two that drift.
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "  T#60S.  ")), Table, NoRelation);

        Assert.Equal(BoundsCurrencyState.Current, result.State);
    }

    [Fact]
    public void EQUIVALENT_DURATIONS_ARE_REPORTED_AS_DIFFERENT_and_that_is_deliberate()
    {
        // T#60S and T#1M are the same interval. Teaching the comparator to know that would be teaching it
        // to equate things, which is how a comparator starts passing — and the cost of the strictness is
        // one human reading two values that the report prints side by side.
        var result = BoundsCurrencyCheck.Evaluate("V-1", Used(("persistence_threshold", "T#1M")), Table, NoRelation);

        Assert.Equal(BoundsCurrencyState.Stale, result.State);
        Assert.Contains("T#1M", result.Detail, StringComparison.Ordinal);
        Assert.Contains("T#60S", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unnamed_vector_is_LABELLED_rather_than_producing_a_blank_finding()
    {
        var result = BoundsCurrencyCheck.Evaluate("  ", null, Table, NoRelation);

        Assert.Equal("<unnamed vector>", result.VectorId);
    }
}
