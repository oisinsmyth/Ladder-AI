using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE WIDTH GATE JOINED ON THE BLOCK'S TAG WHILE EVERY VECTOR KEYS ITS INPUTS BY THE
/// SPECIFICATION'S NAME — SO ON THE ONLY VECTOR SET IT WAS EVER WRITTEN FOR, IT EXAMINED NOTHING.</b>
///
/// <para><see cref="MirrorValueFit"/>'s own doc comment cites <i>81 duration values exceeding 65 535 ms
/// in the deliverable vector set</i> as the reason it exists. <b>Measured 2026-08-14: not one of them
/// was reachable.</b> An input keyed <c>HBA_Stim.P1Ms</c> against a target tagged
/// <c>iDB_HopperBlockageStim.Stim.P1</c> produced <b>0 refusals</b>; the identical value keyed by the
/// tag produced <b>1</b>.</para>
///
/// <para><b>Third instance of one cause.</b> The observability map keyed on the tag and resolved 1 of
/// 17 signals. This keyed on the tag and resolved 0 of 10. <i>A green that examined nothing is
/// indistinguishable from a green that examined everything.</i></para>
/// </summary>
public class MirrorInputJoinTests
{
    /// <summary>The deliverable's shape: block member path as the tag, set-B name as the spec name.</summary>
    private static MirroredSignal Target(string tag, string? spec, MirrorValueType type = MirrorValueType.Int) =>
        new(tag, type, SpecName: spec);

    private static Dictionary<string, string> Inputs(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    [Fact]
    public void AN_INPUT_KEYED_BY_THE_SPEC_NAME_IS_CHECKED_which_it_was_not()
    {
        // 75 000 ms in a single register is the exact value MirrorValueFit says it exists to refuse: it
        // arrives as 9 464, every boundary keyed on it fires early, and the run returns a confident FAIL
        // against a block that did nothing wrong.
        var refusals = MirrorValueFit.CheckAll(
            new[] { Target("iDB_HopperBlockageStim.Stim.P1", "HBA_Stim.P1Ms") },
            Inputs(("HBA_Stim.P1Ms", "75000")));

        var refusal = Assert.Single(refusals);
        Assert.Contains("HBA_Stim.P1Ms", refusal, StringComparison.Ordinal);
        Assert.Contains("9464", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void A_STATED_SPEC_NAME_WINS_EXCLUSIVELY_and_the_TAG_stops_being_a_key()
    {
        // Trying the tag as well would re-introduce exactly the silent identity the specName field exists
        // to remove. So an input keyed by the tag, when a spec name IS stated, does not match — and it is
        // reported as an ORPHAN rather than accepted or ignored.
        var refusals = MirrorValueFit.CheckAll(
            new[] { Target("iDB_HopperBlockageStim.Stim.P1", "HBA_Stim.P1Ms") },
            Inputs(("iDB_HopperBlockageStim.Stim.P1", "75000")));

        var refusal = Assert.Single(refusals);
        Assert.Contains("NO BOUND TARGET CONSUMES IT", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_UNSTATED_SPEC_NAME_FALLS_BACK_TO_THE_TAG_because_there_is_no_other_key()
    {
        // The skeleton block's shape, and the reason the fallback exists: where the binding stated no spec
        // name the tag is the only key there is. That weaker join is already reported as such on the
        // observation side (TagsWithNoSpecName).
        var refusals = MirrorValueFit.CheckAll(
            new[] { Target("Demo_Step", spec: null) },
            Inputs(("Demo_Step", "75000")));

        Assert.Single(refusals);
        Assert.Contains("Demo_Step", refusals[0], StringComparison.Ordinal);
    }

    [Fact]
    public void AN_INPUT_NO_TARGET_CONSUMES_IS_REFUSED_and_names_what_IS_bound()
    {
        // *** THE DELIVERABLE'S OWN CASE. *** All 27 conformance vectors supply `HBA_Stim.ResetAtMs`, and
        // the coordinator's binding deliberately leaves it unbound (one set-B field maps to TWO IR members,
        // harness-binding.md:57). The absence is an honest record; the run proceeding without it was not.
        var refusals = MirrorValueFit.CheckAll(
            new[] { Target("iDB_HopperBlockageStim.Stim.Profile", "HBA_Stim.Profile") },
            Inputs(("HBA_Stim.Profile", "10"), ("HBA_Stim.ResetAtMs", "90000")));

        var refusal = Assert.Single(refusals);
        Assert.Contains("'HBA_Stim.ResetAtMs'", refusal, StringComparison.Ordinal);
        Assert.Contains("the register stays at zero", refusal, StringComparison.Ordinal);

        // It names what IS bound, so the reader can see which of the two documents to change.
        Assert.Contains("HBA_Stim.Profile", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void A_BINDING_THAT_WIRES_NO_TARGETS_AT_ALL_SAYS_SO_rather_than_printing_an_empty_list()
    {
        var refusals = MirrorValueFit.CheckAll(Array.Empty<MirroredSignal>(), Inputs(("HBA_Stim.Profile", "10")));

        Assert.Contains("wires NO vector targets at all", Assert.Single(refusals), StringComparison.Ordinal);
    }

    [Fact]
    public void A_TARGET_THE_VECTOR_SAYS_NOTHING_ABOUT_IS_NOT_A_FINDING()
    {
        // The unaffected direction, asserted as deliberately as the refused one. Leaving an input undriven
        // is a legitimate choice and belongs to the inert declaration, not to the width rule. A gate that
        // fired here would refuse every ordinary partial vector, and would be switched off within a week.
        var refusals = MirrorValueFit.CheckAll(
            new[]
            {
                Target("iDB_HopperBlockageStim.Stim.Profile", "HBA_Stim.Profile"),
                Target("iDB_HopperBlockageStim.Stim.P1", "HBA_Stim.P1Ms", MirrorValueType.Time),
            },
            Inputs(("HBA_Stim.Profile", "10")));

        Assert.Empty(refusals);
    }

    [Fact]
    public void A_WIDE_VALUE_IN_A_WIDE_ELEMENT_IS_NOT_A_FINDING_either()
    {
        // The set-B durations are declared Time, which is 32-bit, so the gate must stay silent on them.
        // Without this the previous test could pass against a checker that refused everything.
        var refusals = MirrorValueFit.CheckAll(
            new[] { Target("iDB_HopperBlockageStim.Stim.P1", "HBA_Stim.P1Ms", MirrorValueType.Time) },
            Inputs(("HBA_Stim.P1Ms", "120000")));

        Assert.Empty(refusals);
    }
}
