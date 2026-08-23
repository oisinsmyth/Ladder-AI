using Harness.Gate;
using Harness.Map;
using Harness.Run;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b><c>harness-run</c> AND A DECLARED NEIGHBOUR — the guard that bound on one path and was inert on
/// the other.</b>
///
/// <para><see cref="ReservedRegion"/> exists because of a measured, bit-for-bit collision on a running
/// controller: a generated mirror and a hand-authored virtual panel both claimed registers 256–323 of one
/// <c>%M</c> area and <b>53 panel tags were overwritten every scan</b>, the panel's master enable among
/// them. <c>BatchPlanner</c> was wired to <see cref="MirrorGeometry.Reserving(ReservedRegion[])"/> the
/// same day; <c>harness-run</c> was not, and it is the path a person is most likely to drive by hand.</para>
///
/// <para><b>The reproduction these tests are built from, measured before the fix:</b> a binding declaring
/// the panel band, run through <c>harness-run --generate-only</c>, produced a <b>318-register mirror
/// straight through the reserved band, exit 4, clean</b> — no refusal, no warning, no mention of the
/// neighbour anywhere in the report. <c>reservedRegions</c> parsed, validated and was then dropped on the
/// floor.</para>
///
/// <para>⚠️ <b>What none of this can see, and the fix must not be read as closing it.</b> The check can
/// only ever see a neighbour <i>somebody wrote down</i>. The panel would have collided just the same had
/// <c>reservedRegions</c> been left blank — see <see cref="MirrorGeometry.ReservedRegions"/>, whose empty
/// default "is the ABSENCE of a claim, not the claim 'the area is otherwise empty'". The remedy for an
/// undeclared neighbour is to declare it, never to strengthen this check.</para>
/// </summary>
public class ReservedRegionRunPathTests
{
    /// <summary>The band the real collision happened in: registers 256–323 of the declared area.</summary>
    private const int PanelRegister = 256;
    private const int PanelLength = 68;
    private const string PanelOwner = "virtual panel command band";

    /// <summary>
    /// A band inside the declared area that the mirror does NOT reach — the negative control.
    /// <b>A guard that refuses everything is worse than no guard</b>, so the same map against a band out of
    /// its way has to plan exactly as it did before this existed.
    /// </summary>
    private const int OutOfReachRegister = 400;

    /// <summary>
    /// Enough slots that the mirror grows PAST register 256, which is the whole shape of the incident: the
    /// extent is computed, so it walked into a band nobody had written down as a batch went from one lane
    /// to two. 100 slots: control 18 registers, vectors 18–217, results 218–317.
    /// </summary>
    private const int Slots = 100;

    /// <summary>
    /// The submission is deliberately EMPTY OF VECTORS, so the gate refuses it and
    /// <c>--generate-only</c> exits 4 — which is precisely the "exit 4, clean" the reproduction measured.
    /// The reservation check lives at map derivation, ABOVE the gate, so the two are independent and this
    /// fixture keeps the gate's verdict constant across every case below.
    /// </summary>
    private const string Submission = """
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 100,
      "maxIndexScans": 200,
      "resultRegistersPerSlot": 1,
      "computedConflicts": [],
      "model": { "id": "M", "represents": ["ramp"], "validatedAgainstPlantData": true },
      "enumeration": { "clauses": ["REQ-1"], "assertions": ["REQ-1:aaaaaa"] },
      "map": { "providedFor": { "Demo_Count": ["Sampled"] } },
      "vectors": []
    }
    """;

    /// <summary>
    /// A binding over <see cref="Slots"/> slots, with whatever <c>reservedRegions</c> the case needs —
    /// <b>including none at all</b>, which is the shape every binding written before the field existed had.
    /// </summary>
    private static string Binding(string? reservedRegionsJson = null, int declaredRegisters = 576)
    {
        var slots = string.Join(",\n", Enumerable.Range(0, Slots).Select(i => $$"""
            { "slotId": "S{{i:D3}}",
              "vectorTargets": [{ "tag": "Stim_Total_{{i:D3}}", "type": "Time" }],
              "startCondition": "Stim_Start_{{i:D3}}",
              "resultSources": [{ "tag": "Alarm_{{i:D3}}", "type": "Bool",
                                  "inertRest": { "value": "false", "basis": "the alarm coil is ANDed with the start condition, so it is off at inert" } }] }
            """));

        var reserved = reservedRegionsJson is null
            ? string.Empty
            : $"\"reservedRegions\": {reservedRegionsJson},\n";

        return $$"""
        {
          "blockNumber": 9001,
          "baseByte": 1000,
          "declaredRegisters": {{declaredRegisters}},
          {{reserved}}"slots": [
        {{slots}}
          ]
        }
        """;
    }

    private static string Band(int register, int length, string owner) =>
        $$"""[{ "register": {{register}}, "length": {{length}}, "owner": "{{owner}}" }]""";

    private static (int Exit, string Output) Run(string bindingJson, params string[] extraArgs)
    {
        var writer = new StringWriter();

        var args = new[] { "--submission", "sub.json", "--binding", "binding.json", "--no-program-under-test" }
            .Concat(extraArgs)
            .ToArray();

        var exit = LoopCli.Run(
            args,
            writer,
            path => path switch
            {
                "sub.json" => Submission,
                "binding.json" => bindingJson,
                _ => throw new FileNotFoundException(path),
            },
            (_, _) => { },
            env: _ => null,
            expandProgramPath: path => new[] { path });

        return (exit, writer.ToString());
    }

    // ---------------------------------------------------------------------------------------------
    // THE REPRODUCTION
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void GENERATE_ONLY_REFUSES_A_MIRROR_THAT_REACHES_A_DECLARED_NEIGHBOUR_and_names_the_owner()
    {
        var (exit, output) = Run(Binding(Band(PanelRegister, PanelLength, PanelOwner)), "--generate-only");

        // 🔴 BEFORE THE FIX THIS WAS 4 — the IR was produced, over the panel's registers, and the report
        // never mentioned that a neighbour had been declared at all.
        Assert.Equal(LoopExit.DidNotRun, exit);
        Assert.Contains("NOT GENERATED: NotDerivable", output, StringComparison.Ordinal);

        // *** THE REFUSAL NAMES THE OWNER. *** ReservedRegion: "a refusal that cannot say WHOSE space was
        // hit sends the reader looking in the wrong place" — to the mirror, which is the one place the
        // problem is not.
        Assert.Contains(PanelOwner, output, StringComparison.Ordinal);
        Assert.Contains("enters reserved registers", output, StringComparison.Ordinal);

        // Refused, never truncated: nothing was emitted over the neighbour's band.
        Assert.DoesNotContain("GENERATED —", output, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_DEPLOYING_RUN_REFUSES_THE_SAME_WAY_because_the_guard_is_not_a_generate_only_feature()
    {
        // Same binding, no --generate-only. The reservation is applied in Compose, so it binds on every
        // path this CLI has rather than on the one the reproduction happened to use.
        var (exit, output) = Run(Binding(Band(PanelRegister, PanelLength, PanelOwner)));

        Assert.Equal(LoopExit.DidNotRun, exit);
        Assert.Contains(PanelOwner, output, StringComparison.Ordinal);
        Assert.Contains("enters reserved registers", output, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_REFUSAL_TEXT_IS_THE_GEOMETRY_S_OWN_so_the_two_paths_cannot_come_to_disagree()
    {
        // The sentence a reader acts on is produced by MirrorGeometry.Intrusions and by nothing else —
        // harness-run does not compose a second wording of its own. Pinned against the geometry directly
        // so that a divergence here fails rather than being noticed by a person comparing two reports.
        var geometry = MirrorGeometry
            .ForCpu1214C(retentiveBytes: 256, baseByte: 1000, declaredRegisters: 576)
            .Reserving(new ReservedRegion(PanelRegister, PanelLength, PanelOwner));

        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 1000, declaredRegisters: 576),
            Enumerable.Range(0, Slots).Select(i => new SlotRequest($"S{i:D3}", 2, 1)).ToArray())).Require();

        var expected = Assert.Single(geometry.Intrusions(map.Regions));

        var (_, output) = Run(Binding(Band(PanelRegister, PanelLength, PanelOwner)), "--generate-only");

        Assert.Contains(expected, output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // NEGATIVE CONTROLS — a guard that refuses everything is worse than none
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_BAND_THE_MIRROR_DOES_NOT_REACH_PLANS_CLEANLY_and_is_reported_as_CLEAR_not_omitted()
    {
        var (exit, output) = Run(Binding(Band(OutOfReachRegister, PanelLength, PanelOwner)), "--generate-only");

        // Exit 4 is the fixture's own gate verdict — the submission carries no vectors — and it is what
        // the reproduction measured. What matters is that the MAP DERIVED and the IR came out: *** a guard
        // that refuses everything is worse than no guard. ***
        Assert.Equal(LoopExit.GeneratedNotAdmissible, exit);
        Assert.Contains("GENERATED —", output, StringComparison.Ordinal);
        Assert.DoesNotContain("enters reserved registers", output, StringComparison.Ordinal);

        // 🔴 AND THE PASS IS STATED. The defect's whole shape was a report that said nothing either way, so
        // "checked and clear" was indistinguishable from "never looked". Silence is not the clean outcome.
        Assert.Contains("NEIGHBOURS  : 1 reserved region(s) declared", output, StringComparison.Ordinal);
        Assert.Contains($"CLEAR OF  '{PanelOwner}' [400..467]", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_RUN_THAT_DECLARES_NO_NEIGHBOUR_SAYS_SO_rather_than_reading_as_an_area_proved_empty()
    {
        var (_, output) = Run(Binding(), "--generate-only");

        Assert.Contains("NEIGHBOURS  : 0 reserved region(s) declared", output, StringComparison.Ordinal);

        // ⚠️ THE HONEST HEADLINE, IN THE PRODUCT AND NOT ONLY IN A COMMENT: this check sees exactly what
        // somebody wrote down. The panel would have collided just the same with the field left blank.
        Assert.Contains("NOT THE SAME AS 'THE AREA IS OTHERWISE EMPTY'", output, StringComparison.Ordinal);
        Assert.Contains("this check cannot find them for you", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_BINDING_WITH_NO_RESERVED_REGIONS_BEHAVES_EXACTLY_AS_IT_DID_and_hashes_identically()
    {
        var without = Run(Binding(), "--generate-only");
        var outOfReach = Run(Binding(Band(OutOfReachRegister, PanelLength, PanelOwner)), "--generate-only");

        Assert.Equal(LoopExit.GeneratedNotAdmissible, without.Exit);
        Assert.Contains("GENERATED —", without.Output, StringComparison.Ordinal);

        // 🔴 *** THE FIELD IS ADDITIVE AND THE MAP HASH PROVES IT. *** A reservation changes nothing about
        // the layout, the deployed bytes or the wire — it can only decide whether a map is allowed to
        // exist — so it is deliberately NOT a MapHash input. If applying it here had moved the hash, every
        // existing build stamp would have moved for no fact on the device.
        Assert.Equal(MapHashOf(without.Output), MapHashOf(outOfReach.Output));
    }

    private static string MapHashOf(string output) =>
        output.Split('\n').Single(l => l.StartsWith("MAP HASH", StringComparison.Ordinal)).Trim();

    // ---------------------------------------------------------------------------------------------
    // MALFORMED IS REFUSED BY THE GEOMETRY, NEVER DROPPED
    // ---------------------------------------------------------------------------------------------

    [Theory]
    // A reservation of nothing. Silently ignoring it leaves a caller who declared protection with none.
    [InlineData("""[{ "register": 256, "length": 0, "owner": "virtual panel command band" }]""", "protects nothing")]
    // %M byte addresses typed where registers were meant — the likeliest way to get this wrong.
    [InlineData("""[{ "register": -8, "length": 68, "owner": "virtual panel command band" }]""", "HOLDING REGISTERS from the area base")]
    // No owner. The label is the point of the check, so a blank is refused rather than defaulted.
    [InlineData("""[{ "register": 256, "length": 68, "owner": "" }]""", "declares no owner")]
    // Every field absent — the shape a misspelt key leaves behind.
    [InlineData("""[{ }]""", "declares no owner")]
    public void A_MALFORMED_RESERVATION_IS_REFUSED_BY_THE_GEOMETRY_rather_than_dropped(string json, string expected)
    {
        var (exit, output) = Run(Binding(json), "--generate-only");

        // *** THE SAME SHAPE AS THE DEFECT THIS GUARD EXISTS FOR, ONE LEVEL UP. *** A typo'd reservation
        // that is quietly skipped restores exactly the silence this closes: the binding says part of the
        // area is off limits and nothing enforces it.
        Assert.Equal(LoopExit.DidNotRun, exit);
        Assert.Contains(expected, output, StringComparison.Ordinal);
        Assert.DoesNotContain("GENERATED —", output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // THE EXTRACTION, PINNED — one derivation, two callers
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>The union is over a SEQUENCE of bindings, which is the batch planner's shape and a superset of
    /// this CLI's.</b> harness-run passes one document; harness-batch passes every lane's. Pinned here so
    /// the helper cannot quietly become single-document-only and force the batch path back onto a second
    /// copy of the same four lines.
    /// </summary>
    [Fact]
    public void THE_HELPER_UNIONS_ACROSS_DOCUMENTS_AND_COLLAPSES_IDENTICAL_DECLARATIONS()
    {
        var a = BindingDocument.Read(Binding(Band(PanelRegister, PanelLength, PanelOwner)));
        var b = BindingDocument.Read(Binding(Band(PanelRegister, PanelLength, PanelOwner)));
        var c = BindingDocument.Read(Binding(Band(900, 40, "legacy alarm mirror")));

        var union = DeclaredReservations.Of(new[] { a, b, c });

        // Identical declarations collapse; a region only a LATER document declares still survives, because
        // the area is shared and reading only the first would drop a neighbour somebody knows about.
        Assert.Equal(2, union.Count);
        Assert.Contains(union, r => r.Owner == PanelOwner && r.Register == PanelRegister && r.Length == PanelLength);
        Assert.Contains(union, r => r.Owner == "legacy alarm mirror");
    }

    [Fact]
    public void THE_HELPER_MAPS_ABSENT_FIELDS_ONTO_VALUES_THE_REGION_ITSELF_REFUSES()
    {
        // 🔴 Absent register/length/owner become -1 / 0 / "" — values ReservedRegion.Refusals rejects by
        // name — rather than 0 / 1 / "reserved", which would read as a real band nobody typed.
        var region = Assert.Single(DeclaredReservations.Of(new[] { BindingDocument.Read(Binding("""[{ }]""")) }));

        Assert.Equal(-1, region.Register);
        Assert.Equal(0, region.Length);
        Assert.Equal(string.Empty, region.Owner);
        Assert.NotEmpty(region.Refusals);
    }

    [Fact]
    public void AN_EMPTY_UNION_LEAVES_THE_GEOMETRY_UNTOUCHED_rather_than_calling_Reserving_with_nothing()
    {
        // MirrorGeometry.Reserving THROWS on an empty list, because at a call site it reads as "the
        // neighbours are declared" and protects nothing. The helper owns that rule so neither caller has
        // to remember it — and a geometry with no reservations is a legitimate state, not a degraded one.
        var bare = MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 1000, declaredRegisters: 576);

        var applied = DeclaredReservations.AppliedTo(bare, new[] { BindingDocument.Read(Binding()) });

        Assert.Empty(applied.ReservedRegions);
        Assert.Equal(bare, applied);
    }
}
