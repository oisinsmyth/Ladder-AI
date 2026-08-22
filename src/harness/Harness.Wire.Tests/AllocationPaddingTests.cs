using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// 🔴 <b>Allocation padding is excluded by CONSTRUCTION, and real signals never are.</b>
///
/// <para>Slots are fixed-size (X-A) so a client's bounds check can be <c>base + index x slot_size</c>.
/// In a multi-slot wave set that means a narrow lane is handed result registers its binding never
/// wired. Measured on the first two-lane batch, 2026-08-22: the valve lane refused
/// <c>RestNotDeclared</c> over exactly 72 registers — 95 allocated minus its own 23 — and no author
/// could have declared them, because they carry no signal.</para>
///
/// <para><b>This is not a weakening of the coverage rule.</b> That rule exists so a register carrying a
/// SIGNAL cannot go unexamined. Padding carries none. The tests below hold both halves: padding is
/// excluded, and nothing that could be a signal ever is.</para>
/// </summary>
public class AllocationPaddingTests
{
    private static SlotBinding Binding(int resultSignals) =>
        new("S0",
            MirroredSignal.Ints("In"),
            "Go",
            Enumerable.Range(0, resultSignals)
                .Select(i => new MirroredSignal($"Out{i}", MirrorValueType.Int, $"Out{i}",
                    Rest: InertRest.At("0", "the fixture rests at zero")))
                .ToArray());

    private static InertDeclaration Plan(int resultSignals, int? allocated) =>
        InertRestPlan.For(Binding(resultSignals), RegisterWordOrder.HighWordFirst,
            allocatedResultRegisters: allocated).Declaration
        ?? throw new InvalidOperationException("the plan refused, so there is no declaration to inspect.");


    // ---------------------------------------------------------------------------------------------

    /// <summary>The case measured on the rig: 23 wired, 95 allocated, the 72 between them padded.</summary>
    [Fact]
    public void The_registers_between_what_a_slot_WIRES_and_what_it_was_ALLOCATED_are_excluded()
    {
        var declaration = Plan(resultSignals: 23, allocated: 95);

        // Every register in the allocated band is now covered — which is what the inert phase checks.
        Assert.All(Enumerable.Range(0, 95), r => Assert.True(declaration.Covers(r), $"R{r:000} is not covered."));

        // And the padding is EXCLUDED rather than expected: nothing asserts a value for it.
        Assert.All(Enumerable.Range(23, 72), r => Assert.False(declaration.ExpectedResults!.ContainsKey(r)));
    }

    /// <summary>The exclusion says WHY, so a reader is not left to infer it from a silent gap.</summary>
    [Fact]
    public void The_padding_exclusion_states_its_reason()
    {
        var declaration = Plan(resultSignals: 23, allocated: 95);

        Assert.Contains("ALLOCATION PADDING", declaration.ExcludedResults![40]);
        Assert.Contains("wires 23 result register(s)", declaration.ExcludedResults![40]);
        Assert.Contains("allocated it 95", declaration.ExcludedResults![40]);
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL. A real signal is never padded away.</b> Every register the binding
    /// wires keeps its declared resting value, so the coverage rule still bites where it is meant to.
    /// </summary>
    [Fact]
    public void Every_WIRED_register_keeps_its_expectation_even_when_padding_is_added()
    {
        var withPadding = Plan(resultSignals: 23, allocated: 95);
        var without = Plan(resultSignals: 23, allocated: null);

        Assert.Equal(without.ExpectedResults!.Keys.OrderBy(k => k), withPadding.ExpectedResults!.Keys.OrderBy(k => k));
        Assert.Equal(23, withPadding.ExpectedResults!.Count);
    }

    /// <summary>A slot allocated exactly what it wires has no padding, and nothing is excluded.</summary>
    [Fact]
    public void An_exactly_sized_slot_pads_nothing()
    {
        var declaration = Plan(resultSignals: 23, allocated: 23);

        Assert.DoesNotContain(declaration.ExcludedResults!.Values, v => v.Contains("ALLOCATION PADDING"));
    }

    /// <summary>
    /// <b>An allocation BELOW what the binding wires is ignored, not trusted.</b> Honouring it would
    /// exclude real signals — the one direction this must never move in.
    /// </summary>
    [Fact]
    public void An_allocation_SMALLER_than_the_binding_excludes_nothing()
    {
        var declaration = Plan(resultSignals: 23, allocated: 5);

        Assert.Equal(23, declaration.ExpectedResults!.Count);
        Assert.DoesNotContain(declaration.ExcludedResults!.Values, v => v.Contains("ALLOCATION PADDING"));
    }

    /// <summary>
    /// <b>No allocation stated means no padding.</b> A caller that does not know the map does not get to
    /// silently narrow the band — the same rule the declared Modbus width follows.
    /// </summary>
    [Fact]
    public void An_UNSTATED_allocation_pads_nothing()
    {
        var declaration = Plan(resultSignals: 23, allocated: null);

        Assert.DoesNotContain(declaration.ExcludedResults!.Values, v => v.Contains("ALLOCATION PADDING"));
        Assert.False(declaration.Covers(40));
    }
}
