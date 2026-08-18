using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// 🔴 <b>THE INERT EXPECTATION IS DECLARED PER SIGNAL, AND AN UNDECLARED ONE REFUSES.</b>
///
/// <para>*** THE FIXTURE TRAP THIS FILE IS WRITTEN AGAINST. *** Every pre-existing fixture in this
/// repository rests at ZERO, so none of them can tell "declared 0" from "defaulted 0" — which is exactly
/// the hole the hardcoded <c>ToDictionary(i =&gt; i, _ =&gt; (ushort)0)</c> lived in. <b>The bindings
/// below therefore rest at non-zero values wherever the property under test allows one, and one of them
/// rests at a NEGATIVE SENTINEL</b>, because the measured deliverable does.</para>
/// </summary>
public class InertRestPlanTests
{
    private const string Basis = "measured on the rig";

    private static SlotBinding Binding(params MirroredSignal[] results) =>
        new("S0", MirroredSignal.Ints("DB_X.Setpoint"), "DB_X.Start", results);

    // -------------------------------------------------------------------------------------------------
    // The ruling: absent is a refusal, not a zero
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void A_SIGNAL_WITH_NO_DECLARED_RESTING_VALUE_REFUSES_AND_NAMES_ITSELF()
    {
        // The defect in one line: this used to produce `{ 0: 0 }` and run.
        var plan = InertRestPlan.For(Binding(MirroredSignal.Int("DB_X.Verdict")), RegisterWordOrder.HighWordFirst);

        Assert.False(plan.Planned);
        Assert.Null(plan.Declaration);
        Assert.Contains(plan.Refusals, r => r.Contains("DB_X.Verdict", StringComparison.Ordinal));
        Assert.Contains(plan.Refusals, r => r.Contains("THIS IS A REFUSAL AND NOT A ZERO", StringComparison.Ordinal));
    }

    [Fact]
    public void THE_REFUSAL_STATES_THE_SILENT_HALF_because_the_loud_half_is_the_one_people_assume()
    {
        // A reader who only knows that a hardcoded 0 "refuses a -1 sentinel" concludes the default is
        // merely inconvenient. The disqualifying half is that where 0 IS a verdict, the assumed 0 makes the
        // check PASS over a stale result — and a refusal that does not say so invites the wrong fix.
        var plan = InertRestPlan.For(Binding(MirroredSignal.Int("DB_X.Verdict")), RegisterWordOrder.HighWordFirst);

        Assert.Contains(plan.Refusals, r => r.Contains("measured PASS verdict", StringComparison.Ordinal));
    }

    // -------------------------------------------------------------------------------------------------
    // The values that are NOT zero — the whole reason this exists
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void A_NEGATIVE_SENTINEL_IS_A_DECLARABLE_RESTING_VALUE_and_lands_as_the_register_actually_reads()
    {
        // -1 meaning "no test has been performed", where 0 is a measured PASS. This is the case that makes
        // the old default WRONG rather than merely unstated, and it is the fixture no existing test had.
        var plan = InertRestPlan.For(
            Binding(new MirroredSignal("DB_X.Verdict", MirrorValueType.Int, Rest: InertRest.At("-1", "sentinel: no test performed; 0 is a PASS"))),
            RegisterWordOrder.HighWordFirst);

        Assert.True(plan.Planned, string.Join(" | ", plan.Refusals));
        Assert.Equal(65535, plan.Require().ExpectedResults[0]);
        Assert.Equal(1, plan.DeclaredCount);
        Assert.Empty(plan.Require().Defaulted);
    }

    [Fact]
    public void A_SIGNAL_THAT_IS_HONESTLY_TRUE_AT_REST_IS_DECLARABLE()
    {
        // Alarm bits that are legitimately raised after a restart. Under the old default this slot could
        // not pass its inert gate at all, and the block was what got blamed.
        var plan = InertRestPlan.For(
            Binding(new MirroredSignal("DB_X.Alarm", MirrorValueType.Bool, Rest: InertRest.At("true", "raised after a restart until acknowledged"))),
            RegisterWordOrder.HighWordFirst);

        Assert.True(plan.Planned, string.Join(" | ", plan.Refusals));
        Assert.Equal(1, plan.Require().ExpectedResults[0]);
    }

    [Fact]
    public void A_WIDE_ELEMENTS_RESTING_VALUE_COVERS_BOTH_ITS_REGISTERS_under_the_measured_word_order()
    {
        // The premise the old hardcoded declaration was right about and supplied itself: inert is declared
        // over every RESULT REGISTER, including the second half of a wide element. A per-signal declaration
        // has to expand, or those halves go back to being unclaimed.
        var plan = InertRestPlan.For(
            Binding(new MirroredSignal("DB_X.Elapsed", MirrorValueType.Time, Rest: InertRest.At("70000", "the block holds its elapsed timer at the configured floor"))),
            RegisterWordOrder.HighWordFirst);

        Assert.True(plan.Planned, string.Join(" | ", plan.Refusals));
        Assert.Equal(2, plan.Require().ExpectedResults.Count);
        Assert.Equal(70000u >> 16, plan.Require().ExpectedResults[0]);
        Assert.Equal(70000u & 0xFFFF, plan.Require().ExpectedResults[1]);
    }

    [Fact]
    public void THE_WORD_ORDER_REACHES_THE_RESTING_VALUE_and_the_two_orders_do_not_agree()
    {
        // A resting value written under one order and read under the other would cancel out on our own
        // loopback and disagree only against the device — the shape of self-agreement this project
        // distrusts. So the order is a parameter here exactly as it is on the stimulus side.
        var signal = new MirroredSignal("DB_X.Elapsed", MirrorValueType.Time, Rest: InertRest.At("70000", Basis));

        var high = InertRestPlan.For(Binding(signal), RegisterWordOrder.HighWordFirst).Require().ExpectedResults;
        var low = InertRestPlan.For(Binding(signal), RegisterWordOrder.LowWordFirst).Require().ExpectedResults;

        Assert.NotEqual(high[0], low[0]);
        Assert.Equal(high[0], low[1]);
    }

    [Fact]
    public void A_RESTING_VALUE_THAT_DOES_NOT_FIT_ITS_ELEMENT_IS_REFUSED_BY_THE_SAME_RULE_THE_STIMULUS_SIDE_USES()
    {
        // One range rule, not two. A second numeric path beside MirrorValueFit is how two derivations of
        // one rule come to disagree — three instances of exactly that are recorded in this codebase.
        var plan = InertRestPlan.For(
            Binding(new MirroredSignal("DB_X.Count", MirrorValueType.Int, Rest: InertRest.At("70000", Basis))),
            RegisterWordOrder.HighWordFirst);

        Assert.False(plan.Planned);
        Assert.Contains(plan.Refusals, r => r.Contains("RESTING value", StringComparison.Ordinal) && r.Contains("does not fit", StringComparison.Ordinal));
    }

    [Fact]
    public void AN_ENCODING_DECLARED_ON_THE_SIGNAL_APPLIES_TO_ITS_RESTING_VALUE_TOO()
    {
        // A symbolic resting state — "the phase register rests at IDLE" — falls out of reusing the stimulus
        // path rather than being a feature anybody had to add.
        var encoding = new ValueEncoding(
            new Dictionary<string, long>(StringComparer.Ordinal) { ["IDLE"] = 7 },
            NumericFallback.Refuse, null, "the coordinator's phase table");

        var plan = InertRestPlan.For(
            Binding(new MirroredSignal("DB_X.Phase", MirrorValueType.Int, Encoding: encoding,
                Rest: InertRest.At("IDLE", "the phase register rests at IDLE between scenarios"))),
            RegisterWordOrder.HighWordFirst);

        Assert.True(plan.Planned, string.Join(" | ", plan.Refusals));
        Assert.Equal(7, plan.Require().ExpectedResults[0]);
    }

    // -------------------------------------------------------------------------------------------------
    // EXCLUDED — a positive claim, and it must cost a reason
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void AN_EXCLUDED_SIGNAL_IS_GATED_BY_NOTHING_AND_THE_REASON_TRAVELS_WITH_IT()
    {
        var plan = InertRestPlan.For(
            Binding(new MirroredSignal("DB_X.Pulse", MirrorValueType.Bool,
                Rest: InertRest.Excluded("one-scan pulse: reads 1 in about one sample of five, so any single-sample expectation is a coin toss"))),
            RegisterWordOrder.HighWordFirst);

        Assert.True(plan.Planned, string.Join(" | ", plan.Refusals));
        Assert.Empty(plan.Require().ExpectedResults);
        Assert.Contains("one-scan pulse", plan.Require().Excluded[0], StringComparison.Ordinal);
        Assert.Equal(1, plan.ExcludedCount);
    }

    [Fact]
    public void AN_EXCLUSION_WITH_NO_REASON_IS_REFUSED_at_the_factory_and_at_the_consuming_end()
    {
        // Both routes, because a record can be constructed directly and a wire format deserializes into
        // one — so the factory is not the only way in.
        Assert.Throws<ArgumentException>(() => InertRest.Excluded("   "));

        var plan = InertRestPlan.For(
            Binding(new MirroredSignal("DB_X.Pulse", MirrorValueType.Bool, Rest: new InertRest(InertRestKind.Excluded, null, "  "))),
            RegisterWordOrder.HighWordFirst);

        Assert.False(plan.Planned);
        Assert.Contains(plan.Refusals, r => r.Contains("EXCLUDED resting state with no reason", StringComparison.Ordinal));
    }

    [Fact]
    public void A_DECLARATION_THAT_IS_BOTH_A_VALUE_AND_AN_EXCLUSION_IS_REFUSED_rather_than_resolved()
    {
        var plan = InertRestPlan.For(
            Binding(new MirroredSignal("DB_X.Pulse", MirrorValueType.Bool, Rest: new InertRest(InertRestKind.Excluded, "1", Basis))),
            RegisterWordOrder.HighWordFirst);

        Assert.False(plan.Planned);
        Assert.Contains(plan.Refusals, r => r.Contains("two different claims", StringComparison.Ordinal));
    }

    [Fact]
    public void A_VALUE_CLAIM_WITH_NO_VALUE_IS_REFUSED_because_an_absent_value_is_not_a_zero_one()
    {
        var plan = InertRestPlan.For(
            Binding(new MirroredSignal("DB_X.Verdict", MirrorValueType.Int, Rest: new InertRest(InertRestKind.Value, null, Basis))),
            RegisterWordOrder.HighWordFirst);

        Assert.False(plan.Planned);
        Assert.Contains(plan.Refusals, r => r.Contains("declares a resting VALUE and gives none", StringComparison.Ordinal));
    }

    // -------------------------------------------------------------------------------------------------
    // The migration escape, and what it costs
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void THE_ASSUME_ZERO_CLAIM_FILLS_ONLY_THE_HOLES_AND_MARKS_THEM_DEFAULTED()
    {
        // *** THE FIXTURE RESTS AT -1, DELIBERATELY. *** With every signal resting at zero this test could
        // not tell a declared expectation from a defaulted one — which is the hole the whole defect lived
        // in, reproduced inside its own regression test.
        var binding = Binding(
            new MirroredSignal("DB_X.Verdict", MirrorValueType.Int, Rest: InertRest.At("-1", "sentinel: no test performed")),
            MirroredSignal.Int("DB_X.Legacy")) with
        {
            AssumedZeroRest = true,
            AssumedZeroRestBasis = "carried from the deployed binding; DB_X.Legacy is being declared this week",
        };

        var plan = InertRestPlan.For(binding, RegisterWordOrder.HighWordFirst);

        Assert.True(plan.Planned, string.Join(" | ", plan.Refusals));
        Assert.Equal(1, plan.DeclaredCount);
        Assert.Equal(1, plan.DefaultedCount);

        // The declared one is NOT swept into the default, and the defaulted one is visible as a default.
        Assert.Equal(65535, plan.Require().ExpectedResults[0]);
        Assert.Equal(0, plan.Require().ExpectedResults[1]);
        Assert.Equal(new[] { 1 }, plan.Require().Defaulted.OrderBy(r => r).ToArray());
        Assert.Contains(plan.Registers, r => r.Provenance == InertRestProvenance.Defaulted && r.Detail.Contains("NOBODY DECLARED THIS", StringComparison.Ordinal));
    }

    [Fact]
    public void THE_ASSUME_ZERO_CLAIM_WITHOUT_A_BASIS_IS_REFUSED_even_though_it_would_have_worked()
    {
        var binding = Binding(MirroredSignal.Int("DB_X.Legacy")) with { AssumedZeroRest = true };

        var plan = InertRestPlan.For(binding, RegisterWordOrder.HighWordFirst);

        Assert.False(plan.Planned);
        Assert.Contains(plan.Refusals, r => r.Contains("gives no basis", StringComparison.Ordinal));
    }

    [Fact]
    public void AN_ASSUME_ZERO_CLAIM_THAT_COVERS_NOTHING_IS_REPORTED_AS_COVERING_NOTHING()
    {
        // An empty classification is a slot waiting to be misused: left in place, the day a declaration is
        // removed the hole is filled in silence instead of refusing.
        var binding = Binding(
            new MirroredSignal("DB_X.Verdict", MirrorValueType.Int, Rest: InertRest.At("-1", "sentinel"))) with
        {
            AssumedZeroRest = true,
            AssumedZeroRestBasis = "left over from the migration",
        };

        var plan = InertRestPlan.For(binding, RegisterWordOrder.HighWordFirst);

        Assert.True(plan.Planned, string.Join(" | ", plan.Refusals));
        Assert.Equal(0, plan.DefaultedCount);
        Assert.Contains(plan.Notes, n => n.Contains("covered NOTHING", StringComparison.Ordinal));
    }

    // -------------------------------------------------------------------------------------------------
    // The latch band: DERIVED from the rungs this harness emits, never declared
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void A_PHASE_ARMED_LATCH_REGISTER_IS_DERIVED_TO_REST_CLEARED()
    {
        var plan = InertRestPlan.For(
            Binding(new MirroredSignal("DB_X.Trip", MirrorValueType.Bool, Transient: true, RearmsEachIndex: true,
                Rest: InertRest.At("false", "the trip coil is ANDed with the start condition"))),
            RegisterWordOrder.HighWordFirst);

        Assert.True(plan.Planned, string.Join(" | ", plan.Refusals));

        var latch = plan.Registers.Single(r => r.Signal.Contains("(latch)", StringComparison.Ordinal));
        Assert.Equal(InertRestProvenance.Derived, latch.Provenance);
        Assert.Equal(0, latch.Expected);
        Assert.Contains("RCOIL := NOT <start bool>", latch.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_ONCE_PER_WAVE_LATCH_REGISTER_IS_DERIVED_AS_NOT_GATED_because_it_has_no_per_index_rest()
    {
        // *** THE HARDCODED ZERO WAS WRONG HERE TOO, AND NOBODY HAD NOTICED. *** An unconditional SCOIL is
        // 0 before its first firing and 1 for the rest of the wave, so asserting 0 at every index refuses
        // every index after the one where the signal legitimately fired.
        var plan = InertRestPlan.For(
            Binding(new MirroredSignal("DB_X.Trip", MirrorValueType.Bool, Transient: true,
                Rest: InertRest.At("false", "the trip coil is ANDed with the start condition"))),
            RegisterWordOrder.HighWordFirst);

        Assert.True(plan.Planned, string.Join(" | ", plan.Refusals));

        var latch = plan.Registers.Single(r => r.Signal.Contains("(latch)", StringComparison.Ordinal));
        Assert.Equal(InertRestProvenance.Excluded, latch.Provenance);
        Assert.False(latch.Gated);
        Assert.Contains("once-per-WAVE latch", latch.Detail, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------------------
    // The denominator, and the over-fire converse
    // -------------------------------------------------------------------------------------------------

    [Fact]
    public void EVERY_REGISTER_OF_THE_BAND_IS_ACCOUNTED_FOR_AND_THE_SUMMARY_PRINTS_THE_DENOMINATOR()
    {
        var binding = Binding(
            new MirroredSignal("DB_X.Verdict", MirrorValueType.Int, Rest: InertRest.At("-1", "sentinel")),
            new MirroredSignal("DB_X.Elapsed", MirrorValueType.Time, Rest: InertRest.At("70000", "floor")),
            new MirroredSignal("DB_X.Pulse", MirrorValueType.Bool, Transient: true, RearmsEachIndex: true,
                Rest: InertRest.Excluded("one-scan pulse")));

        var plan = InertRestPlan.For(binding, RegisterWordOrder.HighWordFirst);

        // 1 (Int) + 2 (Time) + 1 (Bool) values, plus 1 latch register.
        Assert.Equal(binding.ResultRegistersNeeded, plan.Registers.Count);
        Assert.Equal(Enumerable.Range(0, binding.ResultRegistersNeeded), plan.Registers.Select(r => r.Register));

        Assert.Equal(3, plan.DeclaredCount);
        Assert.Equal(1, plan.ExcludedCount);
        Assert.Equal(1, plan.DerivedCount);
        Assert.Equal(0, plan.DefaultedCount);

        Assert.Contains("5 result register(s)", plan.Summary(), StringComparison.Ordinal);
        Assert.Contains("3 DECLARED", plan.Summary(), StringComparison.Ordinal);
    }

    [Fact]
    public void THE_UNAFFECTED_CASE_a_binding_that_declares_everything_is_planned_and_refuses_nothing()
    {
        // Asserted as deliberately as the refusals. A gate that fires on submissions it has nothing to say
        // about is noise, and noise gets switched off — after which the cases it WAS right about go through
        // unchecked.
        var plan = InertRestPlan.For(
            Binding(
                new MirroredSignal("DB_X.Verdict", MirrorValueType.Int, Rest: InertRest.At("-1", "sentinel")),
                new MirroredSignal("DB_X.Alarm", MirrorValueType.Bool, Rest: InertRest.At("true", "raised after a restart"))),
            RegisterWordOrder.HighWordFirst);

        Assert.True(plan.Planned);
        Assert.Empty(plan.Refusals);
        Assert.Empty(plan.Notes);
        Assert.Empty(plan.Require().Defaulted);
        Assert.Empty(plan.Require().Excluded);
    }
}
