using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// Build-plan item 0.1b — the non-retentive assertion, checked from the IR with no device involved.
///
/// <para>Most of what follows is a NEGATIVE test. That is the point: the check's whole job is to
/// refuse, so a suite that only ever fed it clean objects would be a suite that could not fail. Each
/// test below constructs the specific broken thing the rule exists for.</para>
/// </summary>
public class RetentionCheckTests
{
    private static readonly MirrorGeometry Rig = MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000);

    private static HarnessObject Db(string ir) => new("DB_Harness", HarnessObjectKind.DataBlock, ir);

    private static HarnessObject CleanTable(string address = "%MW4000") => new(
        "HarnessMirror", HarnessObjectKind.TagTable,
        $"TAGTABLE HarnessMirror\n  ROOTID 0\n  TAGS\n    HX_A 1 : Int @ {address} ACCESSIBLE VISIBLE WRITABLE\n");

    // ---------------------------------------------------------------------------------------------
    // The generator's own output must pass
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_generated_copy_layer_passes()
    {
        var map = MapAllocator.Allocate(new WaveSetRequest(Rig, new[] { new SlotRequest("S0", 2, 2) })).Require();
        var result = CopyLayerGenerator.Generate(map,
            new SlotBinding("S0", new[] { "DB_Unit.Setpoint" }, "DB_Unit.StartCmd", new[] { "DB_Unit.Actual" }),
            new CopyLayerNaming(BlockNumber: 900));

        var verdict = RetentionCheck.Check(result.Objects, Rig);

        Assert.True(verdict.Passed, verdict.Summary());
        Assert.Equal(2, verdict.ObjectsExamined);
    }

    // ---------------------------------------------------------------------------------------------
    // Retain itself, and the all-or-nothing trap the build plan names
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_retentive_member_of_a_standard_access_DB_refuses_and_names_the_WHOLE_object()
    {
        var verdict = RetentionCheck.Check(new[]
        {
            Db("DB DB_Harness\n  ROOTID 0\n  NUMBER 900\n  MEMORYLAYOUT Standard\n  MEMBERS\n    Counter : Int RETAIN\n"),
        }, Rig);

        Assert.False(verdict.Passed);
        var finding = Assert.Single(verdict.Findings);

        // The message must not read as "one member is retentive". On a standard-access block retain is
        // all-or-nothing, and a reader who fixes the member believing the rest was fine has the wrong model.
        Assert.Contains("WHOLE object", finding.Detail, StringComparison.Ordinal);
        Assert.Contains("all-or-nothing", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_retentive_static_on_a_code_block_refuses()
    {
        var verdict = RetentionCheck.Check(new[]
        {
            new HarnessObject("FB_Harness", HarnessObjectKind.Block,
                "BLOCK FB FB_Harness\nROOTID 0\nNUMBER 901\nLANGUAGE LAD\n\nINTERFACE\n  STATIC\n    Elapsed : DInt RETAIN\n"),
        }, Rig);

        Assert.False(verdict.Passed);
        Assert.Single(verdict.Findings);
    }

    [Fact]
    public void The_word_retain_inside_comment_prose_is_not_a_finding()
    {
        // Without this the check would fire on its own documentation, and the first fix anyone applied
        // would be to weaken the scan.
        var verdict = RetentionCheck.Check(new[]
        {
            Db("DB DB_Harness\n  ROOTID 0\n  NUMBER 900\n  MEMORYLAYOUT Standard\n  MEMBERS\n    Counter : Int COMMENT \"Deliberately does not RETAIN across a download.\"\n"),
        }, Rig);

        Assert.True(verdict.Passed, verdict.Summary());
    }

    [Fact]
    public void A_member_named_something_containing_retain_is_not_a_finding()
    {
        var verdict = RetentionCheck.Check(new[]
        {
            Db("DB DB_Harness\n  ROOTID 0\n  NUMBER 900\n  MEMORYLAYOUT Standard\n  MEMBERS\n    RETAINED_Count : Int\n    NoRETAIN : Bool\n"),
        }, Rig);

        Assert.True(verdict.Passed, verdict.Summary());
    }

    // ---------------------------------------------------------------------------------------------
    // Layout: undetermined is not clean
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_DB_with_no_MEMORYLAYOUT_line_is_refused_because_absence_means_no_opinion()
    {
        var verdict = RetentionCheck.Check(new[]
        {
            Db("DB DB_Harness\n  ROOTID 0\n  NUMBER 900\n  MEMBERS\n    Counter : Int\n"),
        }, Rig);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, f => f.Detail.Contains("no opinion", StringComparison.Ordinal));
    }

    [Fact]
    public void An_optimized_DB_is_refused_because_it_is_absent_on_the_wire_not_merely_awkward()
    {
        var verdict = RetentionCheck.Check(new[]
        {
            Db("DB DB_Harness\n  ROOTID 0\n  NUMBER 900\n  MEMORYLAYOUT Optimized\n  MEMBERS\n    Counter : Int\n"),
        }, Rig);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, f => f.Detail.Contains("ABSENT", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------
    // The mirror: for %M the address IS the assertion
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_mirror_tag_inside_the_retentive_window_is_refused()
    {
        var verdict = RetentionCheck.Check(new[] { CleanTable("%MW128") }, Rig);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, f => f.Detail.Contains("retentive M window", StringComparison.Ordinal));
    }

    [Fact]
    public void A_mirror_tag_exactly_at_the_top_of_the_retentive_window_is_allowed()
    {
        // The boundary is the first NON-retentive byte, so MB256 with 256 retentive bytes is fine and
        // MB255 is not. Stated as a test because an off-by-one here is silent.
        Assert.True(RetentionCheck.Check(new[] { CleanTable("%MW256") }, Rig).Passed);
        Assert.False(RetentionCheck.Check(new[] { CleanTable("%MW254") }, Rig).Passed);
    }

    [Fact]
    public void A_tag_outside_bit_memory_is_refused()
    {
        var verdict = RetentionCheck.Check(new[] { CleanTable("%IW64") }, Rig);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, f => f.Detail.Contains("not a bit-memory address", StringComparison.Ordinal));
    }

    [Fact]
    public void A_tag_running_past_the_top_of_bit_memory_is_refused()
    {
        var verdict = RetentionCheck.Check(new[] { CleanTable("%MD8190") }, Rig);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, f => f.Detail.Contains("runs past", StringComparison.Ordinal));
    }

    [Fact]
    public void A_bit_address_in_the_mirror_is_read_as_its_byte()
    {
        Assert.True(RetentionCheck.Check(new[] { CleanTable("%M4005.0") }, Rig).Passed);
        Assert.False(RetentionCheck.Check(new[] { CleanTable("%M5.0") }, Rig).Passed);
    }

    // ---------------------------------------------------------------------------------------------
    // Empty is not clean, and unreadable is not a skip
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Checking_nothing_is_not_a_pass()
    {
        var verdict = RetentionCheck.Check(Array.Empty<HarnessObject>(), Rig);

        Assert.False(verdict.Passed);
        Assert.Equal(0, verdict.ObjectsExamined);
        Assert.Contains("nothing examined", verdict.Summary(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_tag_table_declaring_no_tags_is_refused()
    {
        var verdict = RetentionCheck.Check(new[]
        {
            new HarnessObject("HarnessMirror", HarnessObjectKind.TagTable, "TAGTABLE HarnessMirror\n  ROOTID 0\n  TAGS\n"),
        }, Rig);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, f => f.Detail.Contains("no tags", StringComparison.Ordinal));
    }

    [Fact]
    public void An_object_the_check_cannot_classify_is_refused_never_skipped()
    {
        var verdict = RetentionCheck.Check(new[]
        {
            new HarnessObject("Mystery", HarnessObjectKind.DataBlock, "SOMETHING ELSE ENTIRELY\n  stuff\n"),
        }, Rig);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, f => f.Detail.Contains("could not classify", StringComparison.Ordinal));
    }

    [Fact]
    public void An_object_whose_IR_disagrees_with_its_declared_kind_is_refused()
    {
        var verdict = RetentionCheck.Check(new[]
        {
            new HarnessObject("Confused", HarnessObjectKind.DataBlock, "TAGTABLE Confused\n  ROOTID 0\n  TAGS\n    A 1 : Int @ %MW4000\n"),
        }, Rig);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, f => f.Detail.Contains("declared as", StringComparison.Ordinal));
    }

    [Fact]
    public void A_bad_mirror_geometry_refuses_even_when_every_object_is_clean()
    {
        // The geometry is itself a harness object for this purpose: a mirror placed inside the retentive
        // window makes every tag in it retentive, however clean each tag line reads.
        var inside = MirrorGeometry.ForCpu1214C(retentiveBytes: 4096, baseByte: 1000);
        var verdict = RetentionCheck.Check(new[] { CleanTable("%MW4000") }, inside);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, f => f.Object == "<mirror geometry>");
    }

    [Fact]
    public void Passed_requires_both_something_examined_and_no_findings()
    {
        Assert.False(new RetentionVerdict(0, Array.Empty<RetentionFinding>()).Passed);
        Assert.False(new RetentionVerdict(3, new[] { new RetentionFinding("x", "y") }).Passed);
        Assert.True(new RetentionVerdict(3, Array.Empty<RetentionFinding>()).Passed);
    }
}
