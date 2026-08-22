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
    private static readonly MirrorGeometry Rig = MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2);

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
            new SlotBinding("S0", MirroredSignal.Ints("DB_Unit.Setpoint"), "DB_Unit.StartCmd", MirroredSignal.Ints("DB_Unit.Actual")),
            new CopyLayerNaming(BlockNumber: 900), new BuildStamp(0xA93F2C71));

        var verdict = RetentionCheck.Check(result.Objects, Rig);

        Assert.True(verdict.Passed, verdict.Summary());
        Assert.Equal(2, verdict.ObjectsExamined);

        // And the ADDRESS rule actually ran over it. Object count alone would have said the same thing
        // about a set in which no address was ever looked at.
        Assert.True(verdict.AddressesExamined > 0, verdict.Summary());
    }

    [Fact]
    public void A_BOOL_RESULT_TAG_IS_EXAMINED_and_not_quietly_skipped_for_wearing_a_bit_address()
    {
        // A Bool result declares `%M<byte>.<bit>` where an Int declares `%MW<byte>`. If the audit's
        // address pattern did not match the dotted form, a whole class of mirror tag would sit OUTSIDE
        // the retentive-window check while the verdict still read PASSED — a green produced by looking at
        // less, which is the failure mode this check's own denominator exists to expose.
        var map = MapAllocator.Allocate(new WaveSetRequest(Rig, new[] { new SlotRequest("S0", 0, 2) })).Require();

        var ints = CopyLayerGenerator.Generate(map,
            new SlotBinding("S0", Array.Empty<MirroredSignal>(), null, MirroredSignal.Ints("DB_Unit.Actual", "DB_Unit.State")),
            new CopyLayerNaming(BlockNumber: 900), new BuildStamp(0xA93F2C71));

        var bools = CopyLayerGenerator.Generate(map,
            new SlotBinding("S0", Array.Empty<MirroredSignal>(), null, MirroredSignal.Bools("DB_Unit.Alarm", "DB_Unit.StopReq")),
            new CopyLayerNaming(BlockNumber: 900), new BuildStamp(0xA93F2C71));

        var intVerdict = RetentionCheck.Check(ints.Objects, Rig);
        var boolVerdict = RetentionCheck.Check(bools.Objects, Rig);

        Assert.True(boolVerdict.Passed, boolVerdict.Summary());

        // The same number of addresses, whatever the type. Fewer would mean the Bools went unexamined.
        Assert.Equal(intVerdict.AddressesExamined, boolVerdict.AddressesExamined);
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
        // The tag table is here because BOTH rules must have a subject for a pass to mean anything —
        // a DB-only set examines no address and is refused on that count alone (see the vacuity tests).
        var verdict = RetentionCheck.Check(new[]
        {
            Db("DB DB_Harness\n  ROOTID 0\n  NUMBER 900\n  MEMORYLAYOUT Standard\n  MEMBERS\n    Counter : Int COMMENT \"Deliberately does not RETAIN across a download.\"\n"),
            CleanTable(),
        }, Rig);

        Assert.True(verdict.Passed, verdict.Summary());
    }

    [Fact]
    public void A_member_named_something_containing_retain_is_not_a_finding()
    {
        var verdict = RetentionCheck.Check(new[]
        {
            Db("DB DB_Harness\n  ROOTID 0\n  NUMBER 900\n  MEMORYLAYOUT Standard\n  MEMBERS\n    RETAINED_Count : Int\n    NoRETAIN : Bool\n"),
            CleanTable(),
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
        var inside = MirrorGeometry.ForCpu1214C(retentiveBytes: 4096, baseByte: 1000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 1000) / 2);
        var verdict = RetentionCheck.Check(new[] { CleanTable("%MW4000") }, inside);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, f => f.Object == "<mirror geometry>");
    }

    [Fact]
    public void Passed_requires_something_examined_by_BOTH_rules_and_no_findings()
    {
        Assert.False(new RetentionVerdict(0, 0, Array.Empty<RetentionFinding>()).Passed);
        Assert.False(new RetentionVerdict(3, 0, Array.Empty<RetentionFinding>()).Passed);
        Assert.False(new RetentionVerdict(0, 3, Array.Empty<RetentionFinding>()).Passed);
        Assert.False(new RetentionVerdict(3, 3, new[] { new RetentionFinding("x", "y") }).Passed);
        Assert.True(new RetentionVerdict(3, 3, Array.Empty<RetentionFinding>()).Passed);
    }

    // ---------------------------------------------------------------------------------------------
    // 0.1b IS TWO RULES, AND THE ADDRESS RULE MUST NOT BE SATISFIABLE VACUOUSLY
    //
    // Each of the three below passed the check before this lane. Every one of them reads as clean in
    // every field: the attribute rule ran, found nothing, and the address rule never executed.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void An_object_set_carrying_no_address_at_all_is_refused_even_though_the_attribute_rule_is_satisfied()
    {
        // The whole object set is attribute-clean: standard layout, no RETAIN, classifiable, non-empty.
        // What it has no trace of is a MIRROR — so the rule that covers %M never had a subject.
        var verdict = RetentionCheck.Check(new[]
        {
            Db("DB DB_Harness\n  ROOTID 0\n  NUMBER 900\n  MEMORYLAYOUT Standard\n  MEMBERS\n    Counter : Int\n"),
            new HarnessObject("FC_Harness", HarnessObjectKind.Block,
                "BLOCK FC FC_Harness\nROOTID 0\nNUMBER 901\nLANGUAGE LAD\n\nINTERFACE\n  INPUT\n"),
        }, Rig);

        Assert.False(verdict.Passed);
        Assert.Empty(verdict.Findings);          // nothing is WRONG — that is exactly the trap
        Assert.Equal(2, verdict.ObjectsExamined);
        Assert.Equal(0, verdict.AddressesExamined);
        Assert.Contains("NO ADDRESS was", verdict.Summary(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_declared_tag_carrying_no_readable_address_is_a_finding_not_a_line_the_scan_moves_past()
    {
        // Six declarations, one readable address. The old scan examined that one and reported a pass;
        // the mirror could have sat anywhere.
        var table = new HarnessObject("HarnessMirror", HarnessObjectKind.TagTable,
            "TAGTABLE HarnessMirror\n  ROOTID 0\n  TAGS\n"
            + "    HX_A 1 : Int @ %MW4000 ACCESSIBLE VISIBLE WRITABLE\n"
            + "    HX_B 4 : Int\n"
            + "    HX_C 7 : Int\n");

        var verdict = RetentionCheck.Check(new[] { table }, Rig);

        Assert.False(verdict.Passed);
        Assert.Equal(1, verdict.AddressesExamined);
        Assert.Equal(2, verdict.Findings.Count);
        Assert.All(verdict.Findings, f => Assert.Contains("carries no '@ <address>'", f.Detail, StringComparison.Ordinal));
    }

    [Fact]
    public void An_absolute_address_written_into_a_rung_is_checked_like_any_other()
    {
        // Not reachable from the current generator, which is symbolic throughout — and that is why it
        // needs a test: the rule must hold because it is enforced, not because nobody has written one yet.
        var verdict = RetentionCheck.Check(new[]
        {
            new HarnessObject("FC_Harness", HarnessObjectKind.Block,
                "BLOCK FC FC_Harness\nROOTID 0\nNUMBER 901\nLANGUAGE LAD\n\nINTERFACE\n  INPUT\n\nNETWORK 1 \"Start\"\n  COIL DB_Unit.StartCmd := %M4.0\n"),
        }, Rig);

        Assert.False(verdict.Passed);
        Assert.Equal(1, verdict.AddressesExamined);
        Assert.Contains(verdict.Findings, f => f.Detail.Contains("retentive M window", StringComparison.Ordinal));
    }

    [Fact]
    public void An_absolute_address_in_a_rung_that_is_correctly_placed_counts_as_examined_and_passes()
    {
        var verdict = RetentionCheck.Check(new[]
        {
            new HarnessObject("FC_Harness", HarnessObjectKind.Block,
                "BLOCK FC FC_Harness\nROOTID 0\nNUMBER 901\nLANGUAGE LAD\n\nINTERFACE\n  INPUT\n\nNETWORK 1 \"Start\"\n  COIL DB_Unit.StartCmd := %M4005.0\n"),
        }, Rig);

        Assert.True(verdict.Passed, verdict.Summary());
        Assert.Equal(1, verdict.AddressesExamined);
    }
}
