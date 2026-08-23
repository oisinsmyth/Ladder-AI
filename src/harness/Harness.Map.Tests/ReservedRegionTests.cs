using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>The neighbour check — the defect measured live on the rig on 2026-08-23.</b>
///
/// <para>A generated mirror and a hand-authored "virtual panel" both claimed registers 256–323 of one
/// <c>%M</c> area. <b>53 panel tags collided bit for bit</b>, including the panel's master enable and a
/// safety-healthy substitution, both of which the copy layer overwrote every scan from unrelated fault
/// flags. The panel's own comment said it was inert until a PC enabled it; in the deployed program that
/// was false. It was safe only because the rig cannot actuate.</para>
///
/// <para><b>Two checks ran and both passed, and neither was broken.</b> <see cref="MapAllocator"/>
/// bounds the mirror against the declared area — the WHOLE area — and <see cref="RegisterMap"/> proves
/// the mirror's regions disjoint <i>from each other</i>. Each examines something real that is not the
/// thing at risk: a <b>CLOSED</b> check, never empty, never silent, and healthiest-looking exactly when
/// it is wrong. A denominator does not catch that; only moving the question somewhere it is answerable
/// does. The mirror's extent is computed — ~165 registers a lane — so it grew past a band nobody had
/// written down on the day a batch went from one lane to two.</para>
///
/// <para>🔴 <b>WHAT THIS CHECK CANNOT POSSIBLY SEE, stated plainly because the last one's blind spot was
/// never written down either:</b></para>
/// <list type="number">
/// <item><b>A neighbour nobody declared.</b> The reservation list is somebody's knowledge of the
/// neighbourhood, and this check is exactly as complete as that knowledge — no more. The virtual panel
/// would still have collided if no one had written it down here. <b>The fix for an undeclared
/// neighbour is to declare it, never to strengthen this check</b>, and no arrangement of this code can
/// change that.</item>
/// <item><b>Whether a declared band is where the declarer said it was.</b> Nothing here reads the
/// deployed program; a reservation naming the wrong registers protects the wrong registers, and looks
/// identical to one naming the right ones.</item>
/// <item><b>Anything outside the mirror's own regions.</b> It compares reservations against what the
/// MAP occupies. Another writer reaching into the mirror from the PLC side — a rung that happens to
/// write <c>%MW1512</c> — is invisible here and always will be; that is <c>RetentionCheck</c>'s side of
/// the wall at best, and in general it is not a static question at all.</item>
/// <item><b>Collisions outside the declared Modbus area.</b> A band past
/// <see cref="MirrorGeometry.DeclaredRegisters"/> is kept clear by the declared-area ceiling, which is a
/// different live check. This one would report it satisfied, truthfully and for the wrong reason.</item>
/// </list>
/// </summary>
public class ReservedRegionTests
{
    /// <summary>The rig's real numbers: base %M1000, MB_HOLD_REG declaring 576 registers.</summary>
    private static MirrorGeometry Rig(int declaredRegisters = 576) =>
        MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 1000, declaredRegisters: declaredRegisters);

    private static MapResult Allocate(MirrorGeometry geometry, int slots, int vector, int result) =>
        MapAllocator.Allocate(new WaveSetRequest(
            geometry,
            Enumerable.Range(0, slots).Select(i => new SlotRequest($"S{i}", vector, result)).ToArray()));

    /// <summary>
    /// The shape that was actually deployed: two lanes, 324 registers of mirror.
    ///
    /// <para>Control is 6 (version 2, scan counter 2, start bools 1, echo 1), vectors are 2 x 63 = 126,
    /// results are 2 x 96 = 192. Total 324, running to register 323 — which is where it met the panel.</para>
    /// </summary>
    private static MapResult TwoLanes(MirrorGeometry geometry) =>
        Allocate(geometry, slots: 2, vector: 63, result: 96);

    /// <summary>The panel band as it really sits: registers 256..323, 68 registers.</summary>
    private const string PanelOwner = "virtual panel command band";

    private static ReservedRegion Panel(int register = 256) => new(register, 68, PanelOwner);

    // ---------------------------------------------------------------------------------------------
    // Today's case, both directions.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>The real case.</b> Two lanes, 324 registers, a 576-register area, and the panel at 256.
    /// Inside bit memory, inside the declared width, disjoint from itself — and refused.
    /// </summary>
    [Fact]
    public void The_two_lane_mirror_that_hit_the_panel_is_refused()
    {
        var result = TwoLanes(Rig().Reserving(Panel()));

        Assert.False(result.Allocated);

        var refusal = Assert.Single(result.Refusals);
        Assert.Contains("[256..323]", refusal);
        Assert.Contains(PanelOwner, refusal);
        Assert.Contains("results region", refusal);

        // And it was NOT caught by either of the checks that were already there — if it were, this test
        // is measuring the old behaviour and the new guard could be deleted without it noticing.
        Assert.DoesNotContain("MB_HOLD_REG declares only", refusal);
        Assert.DoesNotContain("addressable from", refusal);
    }

    /// <summary>
    /// 🔴 <b>The same mirror, the layout actually being deployed: a 1024-register area with the band
    /// moved to 704. It must allocate.</b>
    /// </summary>
    [Fact]
    public void The_same_mirror_against_a_band_at_704_in_a_1024_register_area_allocates()
    {
        var result = TwoLanes(Rig(1024).Reserving(Panel(704)));

        Assert.True(result.Allocated, string.Join(" | ", result.Refusals));
        Assert.Equal(324, result.Require().TotalRegisters);
    }

    /// <summary>
    /// The refusal names BOTH SIDES and the OVERLAP, which is the whole difference between this refusal
    /// and "does not fit". Nobody knew the two shared an area; a message that names only the mirror
    /// sends the reader back to the mirror.
    /// </summary>
    [Fact]
    public void The_refusal_names_the_mirror_region_the_owner_and_the_shared_registers()
    {
        var refusal = Assert.Single(TwoLanes(Rig().Reserving(Panel())).Refusals);

        // The mirror's side, by name and extent.
        Assert.Contains("the mirror's results region [132..323]", refusal);

        // The owner's side, by name and extent.
        Assert.Contains($"'{PanelOwner}' [256..323]", refusal);

        // The registers they share, computed rather than restated.
        Assert.Contains("enters reserved registers [256..323]", refusal);

        // And what to do, because shrinking the wave set and moving the mirror are different repairs.
        Assert.Contains("Shrink the wave set", refusal);
    }

    /// <summary>
    /// <b>The other direction on the same input: correct the one offending field and it allocates.</b>
    /// The mirror, the geometry and the reservation's owner are unchanged — only the band's start moves
    /// past the mirror's last register.
    /// </summary>
    [Fact]
    public void Moving_the_band_one_register_past_the_mirror_allocates_the_same_map()
    {
        var refused = TwoLanes(Rig().Reserving(Panel(323)));
        Assert.False(refused.Allocated);

        var allowed = TwoLanes(Rig().Reserving(Panel(324)));
        Assert.True(allowed.Allocated, string.Join(" | ", allowed.Refusals));

        // 323 is the mirror's last register and 324 is the first free one — a one-register overlap is
        // still a refusal, so the boundary is not off by one in the permissive direction.
        Assert.Contains("[323]", Assert.Single(refused.Refusals));
    }

    /// <summary>
    /// Symmetric case: the band stays put and the WAVE SET shrinks to fit under it. One lane's real
    /// shape ends at register 165, well clear of 256.
    /// </summary>
    [Fact]
    public void Shrinking_the_wave_set_under_the_band_allocates()
    {
        var geometry = Rig().Reserving(Panel());

        Assert.False(TwoLanes(geometry).Allocated);

        var oneLane = Allocate(geometry, slots: 1, vector: 63, result: 95);
        Assert.True(oneLane.Allocated, string.Join(" | ", oneLane.Refusals));
        Assert.True(oneLane.Require().TotalRegisters < 256);
    }

    // ---------------------------------------------------------------------------------------------
    // 🔴 The negative control. A guard that refuses ordinary work is a guard that gets deleted.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL, RUN AND NOT MERELY WRITTEN.</b> A map with reservations it does not
    /// touch must allocate <b>exactly</b> as the same map with none declared — same regions, same slot
    /// addresses, and the same map hash, so no build stamp moves because somebody documented a
    /// neighbour.
    /// </summary>
    [Fact]
    public void Reservations_the_mirror_does_not_touch_change_nothing_at_all()
    {
        var bare = TwoLanes(Rig(1024)).Require();

        var reserved = TwoLanes(Rig(1024)
            .Reserving(Panel(704))
            .Reserving(new ReservedRegion(900, 40, "legacy alarm mirror"))
            .Reserving(new ReservedRegion(1000, 24, "spare, held for the second panel"))).Require();

        Assert.Equal(bare.TotalRegisters, reserved.TotalRegisters);
        Assert.Equal(bare.Regions.Select(r => $"{r.Name}{r.Range}"), reserved.Regions.Select(r => $"{r.Name}{r.Range}"));
        Assert.Equal(bare.Slots.Select(s => $"{s.SlotId}{s.Vector}{s.Result}"), reserved.Slots.Select(s => $"{s.SlotId}{s.Vector}{s.Result}"));

        // 🔴 The stamp does not move. A reservation constrains whether a map may exist; it changes
        // nothing about the layout, the deployed bytes or the wire, so hashing it in would invalidate
        // every existing submission for no fact on the device.
        Assert.Equal(bare.MapHash, reserved.MapHash);
    }

    /// <summary>
    /// A band abutting the mirror's last register on either side is NOT an overlap. Off-by-one in the
    /// strict direction would refuse perfectly good layouts, and this is the check that catches it.
    /// </summary>
    [Theory]
    [InlineData(324)]  // starts one past the mirror's end
    [InlineData(400)]
    public void A_band_abutting_or_above_the_mirror_is_not_an_intrusion(int register)
    {
        Assert.True(TwoLanes(Rig().Reserving(new ReservedRegion(register, 68, PanelOwner))).Allocated);
    }

    /// <summary>
    /// Nothing about the ordinary path changed: a wave set with no reservations at all is the state
    /// every existing caller is in, and it must behave exactly as it did before this file existed.
    /// </summary>
    [Fact]
    public void A_geometry_with_no_reservations_allocates_as_it_always_did()
    {
        var result = TwoLanes(Rig());

        Assert.True(result.Allocated, string.Join(" | ", result.Refusals));
        Assert.Empty(result.Require().Geometry.ReservedRegions);
    }

    // ---------------------------------------------------------------------------------------------
    // Which mirror region was hit, and every one of them can be.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The refusal names the region that was hit, not "the mirror". Landing in the RESULTS corrupts an
    /// observation; landing in the CONTROL region corrupts a commit — different problems, and the
    /// reader should not have to work out which from a register number.
    /// </summary>
    [Theory]
    [InlineData(0, "version")]
    [InlineData(2, "scan counter")]
    [InlineData(4, "start bools")]
    [InlineData(5, "start echo")]
    [InlineData(6, "vectors")]
    [InlineData(132, "results")]
    public void Each_mirror_region_is_named_when_it_is_the_one_hit(int register, string expectedRegion)
    {
        var refusals = TwoLanes(Rig().Reserving(new ReservedRegion(register, 1, PanelOwner))).Refusals;

        Assert.Contains(refusals, r => r.Contains($"the mirror's {expectedRegion} region"));
    }

    /// <summary>
    /// A band spanning several mirror regions reports EACH of them. One refusal naming only the first
    /// would understate how much of the band is contested, and the repair depends on the whole picture.
    /// </summary>
    [Fact]
    public void A_band_across_several_regions_reports_every_region_it_crosses()
    {
        var refusals = TwoLanes(Rig().Reserving(new ReservedRegion(0, 200, PanelOwner))).Refusals;

        Assert.Equal(6, refusals.Count);
        foreach (var region in new[] { "version", "scan counter", "start bools", "start echo", "vectors", "results" })
            Assert.Contains(refusals, r => r.Contains($"the mirror's {region} region"));
    }

    /// <summary>Two owners both in the mirror's way are both named — the reader needs to talk to both.</summary>
    [Fact]
    public void Two_intruded_owners_are_both_named()
    {
        var refusals = TwoLanes(Rig()
            .Reserving(Panel())
            .Reserving(new ReservedRegion(140, 8, "legacy alarm mirror"))).Refusals;

        Assert.Equal(2, refusals.Count);
        Assert.Contains(refusals, r => r.Contains(PanelOwner));
        Assert.Contains(refusals, r => r.Contains("legacy alarm mirror"));
    }

    // ---------------------------------------------------------------------------------------------
    // Empty is not clean: a reservation that cannot be read stops the check, it does not leave it.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A zero-length reservation is refused, not skipped. Skipping it is the exact failure this guard
    /// exists to prevent, one level up: a caller who declared a neighbour and got no protection.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void A_reservation_of_no_registers_is_refused(int length)
    {
        var result = TwoLanes(Rig().Reserving(new ReservedRegion(256, length, PanelOwner)));

        Assert.False(result.Allocated);
        Assert.Contains(result.Refusals, r => r.Contains("A reservation of nothing protects nothing"));

        // The other direction: give it a real length and the check runs — and finds the collision.
        var real = TwoLanes(Rig().Reserving(new ReservedRegion(256, 68, PanelOwner)));
        Assert.False(real.Allocated);
        Assert.Contains(real.Refusals, r => r.Contains("enters reserved registers"));
    }

    /// <summary>
    /// A reservation with no owner is refused. The label is the point of the check: a refusal that
    /// cannot say whose space was hit sends the reader to the mirror, which is the one place the
    /// problem is not.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_reservation_with_no_owner_is_refused(string owner)
    {
        var result = TwoLanes(Rig().Reserving(new ReservedRegion(704, 68, owner)));

        Assert.False(result.Allocated);
        Assert.Contains(result.Refusals, r => r.Contains("declares no owner"));

        // Same region, same everything, owner supplied: it allocates. Note the band at 704 does not
        // touch this mirror, so the ONLY thing under test is the label.
        Assert.True(TwoLanes(Rig().Reserving(new ReservedRegion(704, 68, PanelOwner))).Allocated);
    }

    /// <summary>
    /// A negative start is refused naming the UNITS, because the likely cause is a %M byte address
    /// typed into a register field — the mistake that would leave a band declared and the wrong
    /// registers protected.
    /// </summary>
    [Fact]
    public void A_negative_start_is_refused_and_names_the_units()
    {
        var result = TwoLanes(Rig().Reserving(new ReservedRegion(-8, 68, PanelOwner)));

        Assert.False(result.Allocated);

        var refusal = Assert.Single(result.Refusals, r => r.Contains("starts at register -8"));
        Assert.Contains("HOLDING REGISTERS from the area base", refusal);
        Assert.Contains("ReservingBytes", refusal);
    }

    /// <summary>
    /// 🔴 <b>A malformed reservation stops the comparison; it is not dropped from it.</b> If it were
    /// dropped, a geometry carrying one good band and one unreadable one would report the good band
    /// satisfied — a pass over a picture the caller does not have.
    /// </summary>
    [Fact]
    public void One_unreadable_reservation_stops_the_whole_comparison()
    {
        var geometry = Rig()
            .Reserving(Panel())                                  // genuinely in the way
            .Reserving(new ReservedRegion(400, 0, "spare band")); // unreadable

        var intrusions = geometry.Intrusions(TwoLanes(Rig()).Require().Regions);

        Assert.All(intrusions, i => Assert.Contains("the mirror was NOT checked", i));
        Assert.DoesNotContain(intrusions, i => i.Contains("enters reserved registers"));
    }

    /// <summary>
    /// 🔴 <b>Reservations checked against no usable mirror region is a refusal carrying its own
    /// denominator</b> — every reservation would otherwise report satisfied, which is FI-44's pass over
    /// nothing in miniature.
    /// </summary>
    [Fact]
    public void Reservations_checked_against_an_empty_mirror_refuse_rather_than_pass()
    {
        var geometry = Rig().Reserving(Panel());

        var overNothing = geometry.Intrusions(Array.Empty<(string, RegisterRange)>());
        Assert.Contains("Nothing was compared", Assert.Single(overNothing));

        // And the subtler one: regions PRESENT but none of them occupying a register.
        var overEmptyRegions = geometry.Intrusions(new (string, RegisterRange)[]
        {
            ("vectors", new RegisterRange(6, 0)),
            ("results", new RegisterRange(6, 0)),
        });
        var refusal = Assert.Single(overEmptyRegions);
        Assert.Contains("presented 2 region(s), NONE of which occupies a register", refusal);

        // The other direction: one region that does occupy a register, and the check runs.
        var real = geometry.Intrusions(new (string, RegisterRange)[] { ("results", new RegisterRange(200, 100)) });
        Assert.Contains("enters reserved registers", Assert.Single(real));
    }

    /// <summary>
    /// A geometry with NO reservations answers "no intrusions" over an empty mirror, and that is not the
    /// same silence as above — there is nothing to be wrong about. Asserted so the denominator refusal
    /// cannot be widened into ordinary callers by accident.
    /// </summary>
    [Fact]
    public void No_reservations_over_no_regions_is_simply_empty()
    {
        Assert.Empty(Rig().Intrusions(Array.Empty<(string, RegisterRange)>()));
    }

    /// <summary>
    /// <c>Reserving()</c> with nothing to reserve throws. At the call site it reads as "the neighbours
    /// are declared" and delivers no protection — the guard's own failure mode, and the reason a
    /// caller with nothing to declare simply does not call it.
    /// </summary>
    [Fact]
    public void Reserving_nothing_throws_rather_than_returning_an_unprotected_geometry()
    {
        var thrown = Assert.Throws<ArgumentException>(() => Rig().Reserving());
        Assert.Contains("protects nothing", thrown.Message);

        Assert.Throws<ArgumentException>(() => Rig().Reserving(Array.Empty<ReservedRegion>()));
    }

    /// <summary>Two owners claiming one register is the same defect one level up, and is refused.</summary>
    [Fact]
    public void Two_reservations_claiming_the_same_registers_are_refused()
    {
        var geometry = Rig(1024)
            .Reserving(Panel(704))
            .Reserving(new ReservedRegion(750, 40, "legacy alarm mirror"));

        var refusal = Assert.Single(geometry.Refusals, r => r.Contains("both claim registers"));
        Assert.Contains($"'{PanelOwner}' [704..771]", refusal);
        Assert.Contains("'legacy alarm mirror' [750..789]", refusal);
        Assert.Contains("both claim registers [750..771]", refusal);

        Assert.False(TwoLanes(geometry).Allocated);

        // Corrected: move the second band clear of the first and the pair is accepted — and neither is
        // in the mirror's way, so the map allocates.
        var fixedUp = Rig(1024)
            .Reserving(Panel(704))
            .Reserving(new ReservedRegion(772, 40, "legacy alarm mirror"));

        Assert.Empty(fixedUp.Refusals);
        Assert.True(TwoLanes(fixedUp).Allocated);
    }

    // ---------------------------------------------------------------------------------------------
    // Derived, not authored: %M byte spans become registers here rather than in someone's head.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The panel band is known by its <c>%M</c> addresses. Registers 256..323 at base %M1000 are bytes
    /// 1512..1647, and deriving that here is what stops a band being declared real, off by a few
    /// registers, and protecting the wrong ones.
    /// </summary>
    [Fact]
    public void A_byte_span_derives_the_register_band_it_covers()
    {
        var geometry = Rig().ReservingBytes(firstByte: 1512, byteLength: 136, owner: PanelOwner);

        var region = Assert.Single(geometry.ReservedRegions);
        Assert.Equal(256, region.Register);
        Assert.Equal(68, region.Length);
        Assert.Equal(PanelOwner, region.Owner);

        // And it finds the same collision the hand-written band did.
        Assert.Contains("[256..323]", Assert.Single(TwoLanes(geometry).Refusals));
    }

    /// <summary>
    /// A span that starts or ends mid-register claims the WHOLE register at each end. The mirror writes
    /// whole <c>%MW</c> words, so rounding inward would hand back exactly the byte that gets overwritten.
    /// </summary>
    [Fact]
    public void A_byte_span_rounds_OUTWARD_to_whole_registers()
    {
        // Byte 1513 is the low half of register 256; byte 1514 is the high half of register 257.
        var region = Assert.Single(Rig().ReservingBytes(firstByte: 1513, byteLength: 2, owner: PanelOwner).ReservedRegions);

        Assert.Equal(256, region.Register);
        Assert.Equal(2, region.Length);
    }

    /// <summary>
    /// A byte span below the mirror base derives a negative register and is refused naming the units,
    /// rather than being clamped to zero — where it would silently reserve the version register.
    /// </summary>
    [Fact]
    public void A_byte_span_below_the_mirror_base_is_refused_not_clamped()
    {
        var geometry = Rig().ReservingBytes(firstByte: 900, byteLength: 16, owner: "retentive bookkeeping");

        var region = Assert.Single(geometry.ReservedRegions);
        Assert.True(region.Register < 0);
        Assert.Contains(geometry.Refusals, r => r.Contains("HOLDING REGISTERS from the area base"));
    }

    /// <summary>
    /// A byte span of nothing derives a reservation of nothing and is refused as one — it is not rounded
    /// up into a single register that would look like a real band.
    /// </summary>
    [Fact]
    public void A_byte_span_of_no_bytes_stays_a_reservation_of_no_registers()
    {
        var geometry = Rig().ReservingBytes(firstByte: 1512, byteLength: 0, owner: PanelOwner);

        Assert.Equal(0, Assert.Single(geometry.ReservedRegions).Length);
        Assert.Contains(geometry.Refusals, r => r.Contains("A reservation of nothing protects nothing"));
    }

    // ---------------------------------------------------------------------------------------------
    // The seam with the checks that were already there.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A band beyond the declared width is NOT this check's business, and is not silently ignored
    /// either: the declared-area ceiling is what keeps the mirror out of it, and that check is live.
    /// Asserted so nobody later reads the clean result as "the band was cleared".
    /// </summary>
    [Fact]
    public void A_band_past_the_declared_width_is_held_off_by_the_declared_ceiling_instead()
    {
        // Declared 576; the band sits at 700, which the mirror could only reach by first overrunning
        // the declared area.
        var geometry = Rig().Reserving(new ReservedRegion(700, 68, PanelOwner));

        // A mirror small enough to fit the declared area cannot reach it: no intrusion, and correctly so.
        Assert.True(TwoLanes(geometry).Allocated);

        // A mirror large enough to reach it is refused BY THE DECLARED CEILING, before this check would
        // have had anything to say.
        var oversized = Allocate(geometry, slots: 8, vector: 40, result: 40);
        Assert.False(oversized.Allocated);
        Assert.Contains(oversized.Refusals, r => r.Contains("MB_HOLD_REG declares only 576"));
    }

    /// <summary>
    /// The reservation check runs against the map's OWN region list, so the two cannot drift. If a
    /// region is ever added to the layout, this fails until the list here is updated — which is the
    /// point: a new region silently exempt from the neighbour check is how the next one of these
    /// happens.
    /// </summary>
    [Fact]
    public void Every_region_the_map_declares_is_one_the_neighbour_check_covers()
    {
        var map = TwoLanes(Rig()).Require();

        Assert.Equal(
            new[] { "version", "scan counter", "start bools", "start echo", "vectors", "results" },
            map.Regions.Select(r => r.Name));

        // Each of them, individually, is refusable — proved by walking the map's own list rather than a
        // list written out here.
        foreach (var (name, range) in map.Regions)
        {
            var refusals = TwoLanes(Rig().Reserving(new ReservedRegion(range.Register, 1, PanelOwner))).Refusals;
            Assert.Contains(refusals, r => r.Contains($"the mirror's {name} region"));
        }
    }
}
