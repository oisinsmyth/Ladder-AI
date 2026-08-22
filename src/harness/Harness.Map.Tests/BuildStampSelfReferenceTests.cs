using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// The build stamp must not be a function of the copy layer it is written into.
///
/// <para><b>Why this file exists, measured 2026-08-17 against the bench rig.</b> <c>BuildStamp</c>'s own
/// summary states the invariant — <i>"the copy layer is not one of its own inputs ... hashing the copy
/// layer would be circular"</i> — and the class honoured it for its OWN arguments. Then
/// <c>harness-run --program &lt;ir-dir&gt;</c> re-introduced the circularity from the side: the copy-layer
/// block and the mirror tag table are FILES IN THAT DIRECTORY, and the program loader quite correctly
/// picked them up as objects under test.</para>
///
/// <para><b>The symptom was the worst-shaped one available.</b> Generate → promote the generated layer
/// into <c>ir/</c> → regenerate in order to verify the device moved the stamp from <c>16#F52ECEAD</c> to
/// <c>16#4ED5E68D</c> over an input set that was otherwise byte-identical — 43 tracked files, none dirty,
/// no intervening code change, and the promoting commit's own recorded command reproducing the deployed
/// stamp only from the PRE-promotion tree. <c>VersionCheck</c> therefore classified a CORRECTLY DEPLOYED
/// program as <c>Stale</c>, whose text reads "the download aborted, was refused, or never reached it" —
/// which sends a person to re-download a device that is already right. <b>Every promotion invalidated the
/// stamp it had just written, so the version register could never confirm anything after the first
/// time.</b></para>
///
/// <para><b>Both directions are pinned below</b>, because an exclusion that swallowed too much would be
/// the same defect wearing the other sign: a stamp that stops moving when a real block changes would
/// confirm a build that is not running, which is the one thing §9 exists to prevent.</para>
/// </summary>
public class BuildStampSelfReferenceTests
{
    private const string CopyLayerBlock = "FC_TheCopyLayer";
    private const string MirrorTable = "TheMirrorTable";

    private static readonly CopyLayerNaming Naming =
        new(BlockName: CopyLayerBlock, BlockNumber: 900, TagTableName: MirrorTable);

    private static RegisterMap OneSlot() =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2),
            new[] { new SlotRequest("S0", 3, 2) })).Require();

    private static SlotBinding Binding() => new(
        "S0",
        MirroredSignal.Ints("DB_Unit.Setpoint", "DB_Unit.Mode"),
        "DB_Unit.StartCmd",
        MirroredSignal.Ints("DB_Unit.Actual", "DB_Unit.State"));

    private static HarnessObject Block(string name, string ir) =>
        new(name, HarnessObjectKind.Block, ir);

    /// <summary>The plant blocks — the part of the program set that is genuinely under test.</summary>
    private static HarnessObject[] Plant() =>
    [
        Block("FB_Real", "BLOCK FB_Real\nNETWORK 1 \"a\"\n  COIL X := Y\n"),
        Block("FC_AlsoReal", "BLOCK FC_AlsoReal\nNETWORK 1 \"b\"\n  COIL P := Q\n"),
    ];

    private static uint StampOver(IEnumerable<HarnessObject> program) =>
        BuildStamp.Of(OneSlot(), new[] { Binding() }, Naming, program).Value;

    // ---------------------------------------------------------------------------------------------
    // The defect itself
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// THE REGRESSION TEST. Two program sets differing ONLY in the harness's own generated objects — which
    /// is exactly what promotion does to <c>ir/</c> — must stamp identically. Reverting the exclusion in
    /// <c>BuildStamp.Derive</c> reddens this.
    /// </summary>
    [Fact]
    public void Promoting_the_generated_layer_does_not_move_the_stamp()
    {
        var beforePromotion = Plant()
            .Append(Block(CopyLayerBlock, "BLOCK FC_TheCopyLayer\nNETWORK 1 \"Program version\"\n  MOVE(EN := TRUE, IN := 16#11111111) => HX_ProgramVersion\n"))
            .Append(new HarnessObject(MirrorTable, HarnessObjectKind.TagTable, "TAGTABLE TheMirrorTable\n  HX_A 1 : Bool @ %M4000.0\n"));

        var afterPromotion = Plant()
            .Append(Block(CopyLayerBlock, "BLOCK FC_TheCopyLayer\nNETWORK 1 \"Program version\"\n  MOVE(EN := TRUE, IN := 16#22222222) => HX_ProgramVersion\nNETWORK 2 \"Result latches\"\n  SCOIL HX_L := HX_Start\n"))
            .Append(new HarnessObject(MirrorTable, HarnessObjectKind.TagTable, "TAGTABLE TheMirrorTable\n  HX_A 1 : Bool @ %M4000.0\n  HX_L 2 : Bool @ %M4001.0\n"));

        Assert.Equal(StampOver(beforePromotion), StampOver(afterPromotion));
    }

    /// <summary>
    /// The same claim stated as the thing that actually broke: a program set carrying the harness's own
    /// objects stamps the same as one that never had them, because they contribute nothing either way.
    /// </summary>
    [Fact]
    public void The_harness_objects_contribute_nothing_whether_present_or_absent()
    {
        var withHarness = Plant()
            .Append(Block(CopyLayerBlock, "BLOCK FC_TheCopyLayer\n<anything at all>\n"))
            .Append(new HarnessObject(MirrorTable, HarnessObjectKind.TagTable, "TAGTABLE TheMirrorTable\n<anything at all>\n"));

        Assert.Equal(StampOver(Plant()), StampOver(withHarness));
    }

    // ---------------------------------------------------------------------------------------------
    // The converse — an exclusion that ate too much would be the same defect with the other sign
    // ---------------------------------------------------------------------------------------------

    /// <summary>A change to a REAL block must still move the stamp. Widening the exclusion reddens this.</summary>
    [Fact]
    public void A_change_to_a_block_under_test_still_moves_the_stamp()
    {
        var edited = new[]
        {
            Block("FB_Real", "BLOCK FB_Real\nNETWORK 1 \"a\"\n  COIL X := NOT Y\n"),
            Block("FC_AlsoReal", "BLOCK FC_AlsoReal\nNETWORK 1 \"b\"\n  COIL P := Q\n"),
        };

        Assert.NotEqual(StampOver(Plant()), StampOver(edited));
    }

    /// <summary>
    /// A block that merely SHARES A KIND with the copy layer is not excluded. The exclusion is keyed on the
    /// name the naming declares, so "it is an FC" must not be enough — otherwise every FC in the plant
    /// would silently leave the stamp.
    /// </summary>
    [Fact]
    public void Exclusion_is_by_declared_name_not_by_kind()
    {
        var edited = new[]
        {
            Block("FB_Real", "BLOCK FB_Real\nNETWORK 1 \"a\"\n  COIL X := Y\n"),
            Block("FC_AlsoReal", "BLOCK FC_AlsoReal\nNETWORK 1 \"b\"\n  COIL P := CHANGED\n"),
        };

        Assert.NotEqual(StampOver(Plant()), StampOver(edited));
    }

    // ---------------------------------------------------------------------------------------------
    // The exclusion is reported, because one nobody can see is indistinguishable from an absent object
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Derive_names_what_it_excluded()
    {
        var program = Plant()
            .Append(Block(CopyLayerBlock, "BLOCK FC_TheCopyLayer\n"))
            .Append(new HarnessObject(MirrorTable, HarnessObjectKind.TagTable, "TAGTABLE TheMirrorTable\n"));

        BuildStamp.Derive(OneSlot(), new[] { Binding() }, Naming, program, out var excluded);

        Assert.Equal(
            new[] { $"{HarnessObjectKind.Block}:{CopyLayerBlock}", $"{HarnessObjectKind.TagTable}:{MirrorTable}" },
            excluded.OrderBy(e => e, StringComparer.Ordinal));
    }

    /// <summary>
    /// EMPTY IS NOT CLEAN, in its local form: over a program set holding neither harness object the
    /// reported exclusion list must be EMPTY rather than absent, so a caller can tell "nothing to exclude"
    /// from "the exclusion never ran".
    /// </summary>
    [Fact]
    public void Nothing_to_exclude_reports_an_empty_list_not_a_null()
    {
        BuildStamp.Derive(OneSlot(), new[] { Binding() }, Naming, Plant(), out var excluded);

        Assert.NotNull(excluded);
        Assert.Empty(excluded);
    }
}
