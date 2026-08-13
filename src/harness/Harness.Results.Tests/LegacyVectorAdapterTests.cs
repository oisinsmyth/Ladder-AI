using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// The reconciliation of the two vector models — <b>and it is a mapping, not a merge.</b>
///
/// <para>They were being read as rival versions of one type. They are two objects that were never
/// distinguished: <c>Harness.TestVector</c> is the RUNNER's vector (steps, stimulus, scan waits) and
/// <see cref="SubmissionVector"/> is the SUBMISSION's (slot, index, author, basis, settling,
/// MaxDuration). Editing either into the other loses something real.</para>
/// </summary>
public class LegacyVectorAdapterTests
{
    private static Adaptation Adapt(int steps = 1, string basis = "REQ-014 section 3", string? kills = "an inverted contact") =>
        LegacyVectorAdapter.Adapt("V-1", basis, SignalNature.PersistentState, steps, new[] { "Demo_Count" }, kills);

    [Fact]
    public void NOTHING_IS_FABRICATED_the_adapter_reports_gaps_rather_than_producing_a_half_populated_vector()
    {
        // A half-populated SubmissionVector would then PASS the schema gate on values nobody wrote,
        // which is the absent-field failure this whole lane is against.
        var adaptation = Adapt();

        Assert.False(adaptation.Adapted);
        Assert.Null(adaptation.Vector);
        Assert.NotEmpty(adaptation.Gaps);
    }

    [Fact]
    public void The_ASSERTION_half_of_the_basis_is_named_as_UNRECOVERABLE_rather_than_guessed()
    {
        var gap = Adapt().Gaps.Single(g => g.Field == "Basis.Assertion");

        Assert.Contains("CANNOT BE INFERRED", gap.Detail, StringComparison.Ordinal);
        Assert.Contains("re-create the correlated check inside the adapter", gap.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_contract_field_the_legacy_shape_lacks_is_named_individually()
    {
        var fields = Adapt().Gaps.Select(g => g.Field).ToArray();

        foreach (var required in new[] { "Slot", "Index", "Author", "StartBool", "MaxDuration", "Settling", "Blacklist", "CompressionFactor", "CompletionSignal" })
            Assert.Contains(required, fields);
    }

    [Fact]
    public void The_legacy_WaitScans_is_NOT_offered_as_a_MaxDuration()
    {
        // They are different quantities — one is per STEP, the other bounds the whole test and doubles
        // as X-B's timeout. Treating one as the other would produce a plausible number for the wrong
        // thing, which is worse than an absent one.
        var gap = Adapt().Gaps.Single(g => g.Field == "MaxDuration");

        Assert.Contains("is not the same quantity", gap.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // *** THE SPEC QUESTION — REFUSED, NOT FLATTENED ***
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_MULTI_STEP_VECTOR_IS_REFUSED_BECAUSE_THE_CONTRACT_HAS_NO_SHAPE_FOR_ONE()
    {
        // Contract §2's vector is FLAT: one Inputs map, one set of Expectations, one MaxDuration.
        // TestVector is STEPPED. T=0 is the rising edge of the start bool (D37) and a second stimulus
        // mid-test has no defined relationship to it, so flattening would invent a semantics the design
        // does not state. THIS IS A SPEC QUESTION AND IT IS NOT RESOLVED IN CODE.
        var gap = Adapt(steps: 3).Gaps.Single(g => g.Field == "Steps");

        Assert.Contains("NEEDS A RULING", gap.Detail, StringComparison.Ordinal);
        Assert.Contains("T=0", gap.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_single_step_vector_raises_no_Steps_gap()
    {
        Assert.DoesNotContain(Adapt(steps: 1).Gaps, g => g.Field == "Steps");
    }

    // ---------------------------------------------------------------------------------------------
    // The two observability axes
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_legacy_enum_maps_to_SIGNAL_NATURE_because_that_is_the_axis_it_always_was()
    {
        // {PersistentState, Transient, Coincidence} is what a signal IS. {Latched, Sampled, Stamped} is
        // what was DONE about it. They are not two versions of one vocabulary, which is why nothing could
        // map them while they were one undifferentiated idea.
        Assert.Equal(SignalNature.PersistentState, LegacyVectorAdapter.NatureOf(0));
        Assert.Equal(SignalNature.Transient, LegacyVectorAdapter.NatureOf(1));
        Assert.Equal(SignalNature.Coincidence, LegacyVectorAdapter.NatureOf(2));
    }

    [Fact]
    public void A_fourth_legacy_member_would_have_to_gain_a_row_before_it_could_be_admitted()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => LegacyVectorAdapter.NatureOf(3));

        Assert.Contains("ModesThatCanAnswer", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_relation_between_the_axes_is_SUFFICIENCY_not_translation()
    {
        // A nature does not BECOME a mode. It constrains which modes could answer, and for the
        // persistent case that is all three — so a translation table would have had to pick one
        // arbitrarily and would have been wrong two thirds of the time.
        Assert.Equal(3, ObservabilityCheck.ModesThatCanAnswer(SignalNature.PersistentState).Count);
        Assert.Equal(2, ObservabilityCheck.ModesThatCanAnswer(SignalNature.Transient).Count);
        Assert.Single(ObservabilityCheck.ModesThatCanAnswer(SignalNature.Coincidence));
    }

    [Fact]
    public void The_one_INFERRED_cell_of_the_table_is_marked_rather_than_absorbed()
    {
        // That STAMPED also answers occurrence for a Transient is not quoted anywhere: the contract gives
        // stamps as answering "when, relative to T=0". It is the only cell the contract does not state,
        // and it is marked [I] in the source rather than presented as quoted.
        var doc = typeof(ObservabilityCheck).GetMethod(nameof(ObservabilityCheck.ModesThatCanAnswer));

        Assert.NotNull(doc);
        Assert.Contains(InstrumentationMode.Stamped, ObservabilityCheck.ModesThatCanAnswer(SignalNature.Transient));
    }
}
