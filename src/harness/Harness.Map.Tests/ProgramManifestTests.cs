using Harness.Map;
using Xunit;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE BUILD STAMP NOW RECORDS WHAT IT WAS COMPUTED OVER, BECAUSE A GREEN WAVE COULD NOT BE
/// RE-RUN.</b>
///
/// <para>*** MEASURED 2026-08-21. *** A wave had run end to end on a controller and produced result
/// packages. A later attempt to re-run it was refused by the verifying gateway on a build-stamp
/// mismatch — and <b>nothing recorded which program set the successful run's stamp had been computed
/// over</b>. Two candidate sets were tried; they produced two different stamps and neither was the
/// device's. A hash cannot be inverted, so the run was unreproducible the moment its command line was
/// gone. The result package, the artifact meant to OUTLIVE the run, had kept the outcome and not the
/// input.</para>
///
/// <para>The property that matters is not "a manifest exists" but that it <b>describes exactly the set
/// that was hashed</b> — including telling a DIFFERENT set from a CHANGED one, which is the distinction
/// that would have identified the mismatch in minutes.</para>
/// </summary>
public class ProgramManifestTests
{
    private static readonly CopyLayerNaming Naming =
        new(BlockName: "FC_TheCopyLayer", BlockNumber: 900, TagTableName: "TheMirrorTable");

    private static RegisterMap OneSlot() =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000),
            new[] { new SlotRequest("S0", 3, 2) })).Require();

    private static SlotBinding Binding() => new(
        "S0",
        MirroredSignal.Ints("DB_Unit.Setpoint", "DB_Unit.Mode"),
        "DB_Unit.StartCmd",
        MirroredSignal.Ints("DB_Unit.Actual", "DB_Unit.State"));

    private static HarnessObject Block(string name, string ir) => new(name, HarnessObjectKind.Block, ir);

    private static (BuildStamp Stamp, ProgramManifest Manifest) Derive(params HarnessObject[] program)
    {
        var stamp = BuildStamp.Derive(OneSlot(), new[] { Binding() }, Naming, program, out _, out var manifest);
        return (stamp, manifest);
    }

    // ---------------------------------------------------------------------------------------------
    // the manifest describes what was hashed
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_MANIFEST_CARRIES_THE_STAMP_IT_PRODUCED()
    {
        // The pair travels together or neither is much use: a stamp with no manifest cannot be
        // reproduced, and a manifest with no stamp cannot be matched to a device.
        var (stamp, manifest) = Derive(Block("FB_Real", "BLOCK FB_Real\n"));

        Assert.Equal(stamp.Value, manifest.Stamp);
    }

    [Fact]
    public void AND_NAMES_EVERY_OBJECT_IT_HASHED()
    {
        var (_, manifest) = Derive(
            Block("FB_Real", "BLOCK FB_Real\n"),
            Block("FC_AlsoReal", "BLOCK FC_AlsoReal\n"));

        Assert.Equal(2, manifest.Objects.Count);
        Assert.Contains(manifest.Objects, o => o.Name == "FB_Real");
        Assert.Contains(manifest.Objects, o => o.Name == "FC_AlsoReal");
        Assert.All(manifest.Objects, o => Assert.Equal(HarnessObjectKind.Block.ToString(), o.Kind));
        Assert.False(manifest.HashedNothing);
    }

    // ---------------------------------------------------------------------------------------------
    // 🔴 the distinction that would have solved the real failure in minutes
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_CHANGED_OBJECT_CHANGES_THE_STAMP_AND_THE_MANIFEST_SHOWS_WHERE()
    {
        // *** THE LOAD-BEARING PROPERTY. *** The same object NAMES with different CONTENT is exactly the
        // case a name-only manifest cannot distinguish from the right set - and it is one of the two
        // explanations for a stamp that no longer matches. The per-object hash is what separates them.
        var (before, first) = Derive(Block("FB_Real", "BLOCK FB_Real\nNETWORK 1 \"a\"\n"));
        var (after, second) = Derive(Block("FB_Real", "BLOCK FB_Real\nNETWORK 1 \"CHANGED\"\n"));

        Assert.NotEqual(before.Value, after.Value);

        // Same name, same count - and the hash says the content moved.
        Assert.Equal(first.Objects.Single().Name, second.Objects.Single().Name);
        Assert.NotEqual(first.Objects.Single().Sha256, second.Objects.Single().Sha256);
    }

    [Fact]
    public void AND_A_DIFFERENT_SET_IS_VISIBLE_AS_A_DIFFERENT_SET()
    {
        // The other explanation for a mismatched stamp: not a changed file, a different list. Both were
        // live candidates on the real failure and the manifest tells them apart at a glance.
        var (_, one) = Derive(Block("FB_Real", "BLOCK FB_Real\n"));
        var (_, two) = Derive(Block("FB_Real", "BLOCK FB_Real\n"), Block("FB_Extra", "BLOCK FB_Extra\n"));

        Assert.Single(one.Objects);
        Assert.Equal(2, two.Objects.Count);
        Assert.Equal(one.Objects.Single().Sha256, two.Objects.Single(o => o.Name == "FB_Real").Sha256);
    }

    // ---------------------------------------------------------------------------------------------
    // the absences, each its own state
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void NO_PROGRAM_UNDER_TEST_HASHES_NOTHING_AND_SAYS_SO()
    {
        // *** EMPTY IS A REAL CLAIM HERE, AND IT IS NOT THE SAME AS ABSENT. *** A run declaring no
        // program under test hashed no objects; a run whose manifest was never recorded is a different
        // thing entirely, and the consumer renders the second as null rather than as this.
        var (_, manifest) = Derive();

        Assert.True(manifest.HashedNothing);
        Assert.Empty(manifest.Objects);
    }

    [Fact]
    public void THE_HARNESS_OWN_OBJECTS_ARE_EXCLUDED_BY_NAME_AND_NOT_SILENTLY()
    {
        // A caller who hands over a whole IR directory has no way to know it also handed over the copy
        // layer. An exclusion nobody can see is indistinguishable from an object never supplied - so the
        // manifest carries the refusals beside the things it did hash.
        var (_, manifest) = Derive(
            Block("FB_Real", "BLOCK FB_Real\n"),
            Block("FC_TheCopyLayer", "BLOCK FC_TheCopyLayer\n"));

        Assert.Single(manifest.Objects);
        Assert.Equal("FB_Real", manifest.Objects.Single().Name);
        Assert.Contains(manifest.ExcludedAsSelfReferential, e => e.Contains("FC_TheCopyLayer", StringComparison.Ordinal));
    }
}
