using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>The ceiling that actually binds, and which nothing checked until 2026-08-22.</b>
///
/// <para><see cref="MapAllocator"/> refused a map larger than <see cref="MirrorGeometry.AvailableRegisters"/>
/// — BIT MEMORY, 3,596 registers at base <c>%M1000</c> — and never compared it against the width
/// <c>MB_HOLD_REG</c> declares, which on the rig is <b>576</b>. One lane uses about 165, so the gap has
/// never bitten. A batch of four would overflow it.</para>
///
/// <para><b>Why that matters more than the arithmetic suggests.</b> Registers past the declared area
/// exist in <c>%M</c> and cannot be read over Modbus at all — the server refuses them. So the overflow
/// would not appear as a map that does not fit. It would appear as a wave whose reads start failing
/// partway through the band: a device fault, apparently, sending a reader to the controller instead of
/// to the area pointer.</para>
/// </summary>
public class DeclaredAreaCeilingTests
{
    /// <summary>The rig's real numbers: base %M1000, and MB_HOLD_REG declaring 576 registers.</summary>
    private static MirrorGeometry Rig(int declaredRegisters = 576) =>
        MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 1000, declaredRegisters: declaredRegisters);

    private static MapResult Allocate(MirrorGeometry geometry, int slots, int vector, int result) =>
        MapAllocator.Allocate(new WaveSetRequest(
            geometry,
            Enumerable.Range(0, slots).Select(i => new SlotRequest($"S{i}", vector, result)).ToArray()));

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The case the batch would have hit: comfortably inside bit memory, well past what the wire can
    /// reach.
    /// </summary>
    [Fact]
    public void A_map_inside_BIT_MEMORY_but_past_the_DECLARED_area_is_refused()
    {
        // 8 slots x (40 vector + 40 result) = 640 registers plus overhead: under 3,596, over 576.
        var result = Allocate(Rig(), slots: 8, vector: 40, result: 40);

        Assert.False(result.Allocated);
        Assert.Contains(result.Refusals, r => r.Contains("MB_HOLD_REG declares only 576"));

        // It must NOT have been caught by the bit-memory check, or this test is measuring the old one.
        Assert.DoesNotContain(result.Refusals, r => r.Contains("addressable from %M1000"));
    }

    /// <summary>
    /// The refusal names both numbers and the overshoot, in the house style — a reader has to be able to
    /// size the problem without re-deriving it.
    /// </summary>
    [Fact]
    public void The_refusal_names_the_need_the_limit_and_the_overshoot()
    {
        var result = Allocate(Rig(), slots: 8, vector: 40, result: 40);

        var refusal = Assert.Single(result.Refusals, r => r.Contains("MB_HOLD_REG"));
        Assert.Contains("the map needs 646 registers", refusal);
        Assert.Contains("over by 70", refusal);

        // And it says what to DO, because "widen the pointer" and "run fewer slots" are different repairs
        // and the reader is the one who knows which is available.
        Assert.Contains("Widen the area pointer", refusal);
    }

    /// <summary>
    /// 🔴 <b>The negative control.</b> A ceiling that refuses everything passes every test above. One
    /// lane's real shape must still allocate.
    /// </summary>
    [Fact]
    public void A_single_lane_still_fits_inside_576()
    {
        var result = Allocate(Rig(), slots: 1, vector: 63, result: 95);

        Assert.True(result.Allocated, string.Join(" | ", result.Refusals));
    }

    /// <summary>
    /// Both ceilings are live. A map past bit memory is still refused for THAT reason, with its own
    /// number — the new check did not replace the old one.
    /// </summary>
    [Fact]
    public void The_BIT_MEMORY_ceiling_still_applies_on_its_own_terms()
    {
        // Declared area deliberately larger than memory allows would itself be refused, so this uses a
        // geometry whose declared area is the memory maximum and then overflows memory.
        var wide = MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 1000, declaredRegisters: 3596);
        var result = Allocate(wide, slots: 40, vector: 60, result: 60);

        Assert.False(result.Allocated);
        Assert.Contains(result.Refusals, r => r.Contains("addressable from %M1000"));
    }

    /// <summary>
    /// A declared width of zero is not "no limit". It is an area nothing can be read from, and an
    /// unstated one arrives here as exactly that.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void An_empty_or_negative_declared_area_is_refused_by_the_geometry_itself(int declared)
    {
        var geometry = MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 1000, declaredRegisters: declared);

        Assert.Contains(geometry.Refusals, r => r.Contains("declared Modbus area"));
    }

    /// <summary>
    /// The declared area is a window onto <c>%M</c>, so it cannot extend past the memory it looks at. A
    /// pointer that does is a program-side mistake, and the symptom would be reads that succeed on the
    /// wire while addressing memory the CPU does not have.
    /// </summary>
    [Fact]
    public void A_declared_area_running_past_bit_memory_is_refused()
    {
        var geometry = MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 8000, declaredRegisters: 500);

        Assert.Contains(geometry.Refusals, r => r.Contains("past the CPU's 8192 bytes"));
    }

    /// <summary>
    /// 🔴 <b>The declared width is a build-stamp input, and until 2026-08-22 it was not — while a
    /// committed note claimed it was.</b>
    ///
    /// <para><c>total-plant-run-feasibility.md</c> stated the width "is a map-hash input so the build
    /// stamp moves". It was not in the canonical form at all, so widening or narrowing
    /// <c>MB_HOLD_REG</c> changed what the wire could reach while every stamp stayed identical: a client
    /// and a controller could disagree about the size of the window and still match.</para>
    /// </summary>
    [Fact]
    public void Two_maps_differing_ONLY_in_the_declared_width_hash_differently()
    {
        var narrow = Allocate(Rig(576), slots: 1, vector: 3, result: 2).Require();
        var wide = Allocate(Rig(1200), slots: 1, vector: 3, result: 2).Require();

        // Same layout in every other respect — so the hash difference can only be the declared width.
        Assert.Equal(narrow.ResultBlock.Register, wide.ResultBlock.Register);
        Assert.Equal(narrow.ResultBlock.Length, wide.ResultBlock.Length);

        Assert.NotEqual(narrow.MapHash, wide.MapHash);
    }
}
