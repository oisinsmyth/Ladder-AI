using Converter.Ir;
using Converter.Review;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// S4 Phase 1 (2026-07-15): ReviewRunner's own orchestration layer - BLOCK vs. DB dispatch, the
/// never-silently-absent rule-status invariant, and the --ignore-errors abort-vs-record branch
/// (the mid-session ask that added it: a batch review shouldn't abort entirely just because one
/// file fails to parse). Rules.cs's own check logic is covered directly in ReviewRulesTests.cs;
/// these tests are about the file-dispatch/status-assembly layer on top, which genuinely needs
/// real files on disk (ReviewRunner.ReviewFiles reads by path, unlike the in-memory Rules.* API).
/// </summary>
public class ReviewRunnerTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string WriteTempIrFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"review-test-{Guid.NewGuid():N}.ir");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    // ReviewRunner's OWN list, not a copy of it (2026-08-18). This was a hand-maintained duplicate
    // that had drifted to 12 of the 18 registered rules, so every "one status per rule, on every
    // content kind" assertion below was silently checking a subset — a rule could be registered and
    // never wired into the DB/TYPE/TAGTABLE branches and still pass.
    private static readonly string[] AllRuleIds = ReviewRunner.AllRuleIds;

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Best-effort cleanup only - not the point of the test.
            }
        }
    }

    [Fact]
    public void ReviewFiles_BlockKindFile_DispatchesToReviewBlockAndReportsEveryRule()
    {
        // Deliberately unprefixed name/no header comment so C-003/C-201 both have something real
        // to find - proves findings actually flow end-to-end through the dispatch/assembly layer,
        // not just that Rules.cs itself works in isolation.
        var block = new IrBlock("0", "FB", "MotorDOL", 1, "LAD", null, new[]
        {
            new IrNetwork(1, "Motor start/stop", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var path = WriteTempIrFile(IrSerializer.SerializeBlock(block, new[] { sidecar }));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        Assert.Equal("MotorDOL", file.BlockName);
        Assert.Null(file.FileError);
        Assert.Contains(file.Findings, f => f.RuleId == "C-003");
        Assert.Contains(file.Findings, f => f.RuleId == "C-201");

        foreach (var ruleId in AllRuleIds)
        {
            Assert.Contains(file.RuleStatuses, s => s.RuleId == ruleId);
        }

        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-102" && s.Status == RuleCheckStatus.CheckedVacuous);
        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-401" && s.Status == RuleCheckStatus.CheckedVacuous);
        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-404" && s.Status == RuleCheckStatus.CheckedVacuous);
    }

    // C-410 end to end: a self-restarting timer must reach the report through the file-dispatch
    // layer and GATE (exit 1), not merely be findable by calling Rules directly.
    [Fact]
    public void ReviewFiles_SelfRestartingTimerBlock_C410CheckedAndGates()
    {
        var block = new IrBlock("0", "FB", "FB_Tick", 1, "LAD", "Produces a one-second tick.", new[]
        {
            new IrNetwork(1, "One-second tick", Array.Empty<CoilAssignment>(), Timers: new[]
            {
                new TimerBinding("CycleTimer", new Expr.Not(new Expr.TagRef("CycleTimer.Q")), new Expr.Literal("T#1S")),
            }),
        }, StaticMembers: new[] { new DbMember("CycleTimer", "TON_TIME", Retain: false, StartValue: null) });
        var path = WriteTempIrFile(IrSerializer.SerializeBlockReadable(block));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-410" && s.Status == RuleCheckStatus.Checked);
        var finding = Assert.Single(file.Findings, f => f.RuleId == "C-410");
        Assert.Contains("SELF-RESTART (TOTAL)", finding.Description);
        Assert.Equal(ReviewOutcome.Findings, ReviewOutcome.ExitCode(report, allowUnchecked: false));
    }

    // The repaired form of the same block — Q written to a named Bool, IN gated on that Bool —
    // through the same dispatch layer. This is the corpus-wide claim: running the rule over an
    // already-repaired project must report nothing.
    [Fact]
    public void ReviewFiles_RepairedTickBlock_C410CheckedAndClean()
    {
        var block = new IrBlock("0", "FB", "FB_Tick", 1, "LAD", "Produces a one-second tick.", new[]
        {
            new IrNetwork(1, "One-second tick", new[]
            {
                new CoilAssignment("TickElapsed", new Expr.TagRef("CycleTimer.Q")),
            }, Timers: new[]
            {
                new TimerBinding("CycleTimer", new Expr.Not(new Expr.TagRef("TickElapsed")), new Expr.Literal("T#1S")),
            }),
        }, StaticMembers: new[]
        {
            new DbMember("TickElapsed", "Bool", Retain: false, StartValue: null),
            new DbMember("CycleTimer", "TON_TIME", Retain: false, StartValue: null),
        });
        var path = WriteTempIrFile(IrSerializer.SerializeBlockReadable(block));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-410" && s.Status == RuleCheckStatus.Checked);
        Assert.DoesNotContain(file.Findings, f => f.RuleId == "C-410");
    }

    [Fact]
    public void ReviewFiles_DbKindFile_DispatchesToReviewDbWithNetworkRulesNotApplicable()
    {
        var db = new DbSource("0", "AlarmData", 1, InstanceOfName: null, Comment: "Holds alarm state.", Members: Array.Empty<DbMember>());
        var path = WriteTempIrFile(DbIrSerializer.Serialize(db));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        Assert.Equal("AlarmData", file.BlockName);
        Assert.Null(file.FileError);
        Assert.Contains(file.Findings, f => f.RuleId == "C-003"); // "AlarmData" lacks the DB_ prefix.

        foreach (var ruleId in AllRuleIds)
        {
            Assert.Contains(file.RuleStatuses, s => s.RuleId == ruleId);
        }

        foreach (var networkOnlyRule in new[] { "C-301", "C-501", "C-102", "C-401", "C-404" })
        {
            Assert.Contains(file.RuleStatuses, s => s.RuleId == networkOnlyRule && s.Status == RuleCheckStatus.NotApplicable);
        }
    }

    [Fact]
    public void ReviewFiles_MalformedFile_IgnoreErrorsFalse_ThrowsWithPathInMessage()
    {
        var path = WriteTempIrFile("BLOCK GARBAGE\n");

        var ex = Assert.Throws<ReviewFileException>(() => ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false));

        Assert.Contains(path, ex.Message);
    }

    [Fact]
    public void ReviewFiles_MalformedFile_IgnoreErrorsTrue_RecordsFileErrorAndContinuesBatch()
    {
        var badPath = WriteTempIrFile("BLOCK GARBAGE\n");

        var block = new IrBlock("0", "FB", "FB_Good", 1, "LAD", "A header comment.", new[]
        {
            new IrNetwork(1, "Motor start/stop", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var goodPath = WriteTempIrFile(IrSerializer.SerializeBlock(block, new[] { sidecar }));

        var report = ReviewRunner.ReviewFiles(new[] { badPath, goodPath }, ignoreErrors: true);

        Assert.Equal(2, report.Files.Count);

        var badFile = report.Files.Single(f => f.FilePath == badPath);
        Assert.NotNull(badFile.FileError);
        Assert.Empty(badFile.Findings);
        Assert.Empty(badFile.RuleStatuses);

        var goodFile = report.Files.Single(f => f.FilePath == goodPath);
        Assert.Null(goodFile.FileError);
        Assert.Equal("FB_Good", goodFile.BlockName);
    }

    // An untitled single-slice alarm-word coil trips C-301 (absolute addressing) AND, because it
    // fails the alarm-word exception, C-501 - one check (CheckC301AbsoluteAddressing) emits both
    // (see ReviewRulesTests.CheckC301_SingleSliceBitUntitled_FlagsBothC301AndC501). The C-301
    // status count must reflect only the C-301 findings actually printed under it, not the
    // co-emitted C-501 one - the F-1 reporter bug (count read "2" while one C-301 finding printed).
    [Fact]
    public void ReviewFiles_C301CoEmitsC501_C301CountMatchesOnlyItsOwnPrintedFindings()
    {
        var block = new IrBlock("0", "FB", "FB_Alarms", 1, "LAD", null, new[]
        {
            new IrNetwork(1, string.Empty, new[] { new CoilAssignment("AlarmWord.%X3", new Expr.TagRef("Cond1")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var path = WriteTempIrFile(IrSerializer.SerializeBlock(block, new[] { sidecar }));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        var c301Printed = file.Findings.Count(f => f.RuleId == "C-301");
        var c501Printed = file.Findings.Count(f => f.RuleId == "C-501");
        Assert.Equal(1, c301Printed);
        Assert.Equal(1, c501Printed);

        var c301Status = file.RuleStatuses.Single(s => s.RuleId == "C-301");
        var c501Status = file.RuleStatuses.Single(s => s.RuleId == "C-501");
        Assert.Equal(c301Printed, c301Status.FindingCount); // was 2 before the F-1 fix (counted the C-501 too)
        Assert.Equal(c501Printed, c501Status.FindingCount);
    }

    // General invariant, the real future-proofing: a Checked status's FindingCount must always equal
    // the number of findings printed under that rule ID. Catches any check co-emitting another
    // rule's findings (only C-301 -> C-501 today) inflating the wrong count.
    [Fact]
    public void ReviewFiles_EveryCheckedStatusCountEqualsItsOwnPrintedFindings()
    {
        var block = new IrBlock("0", "FB", "FB_Alarms", 1, "LAD", null, new[]
        {
            new IrNetwork(1, string.Empty, new[] { new CoilAssignment("AlarmWord.%X3", new Expr.TagRef("Cond1")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var path = WriteTempIrFile(IrSerializer.SerializeBlock(block, new[] { sidecar }));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        foreach (var status in file.RuleStatuses.Where(s => s.Status == RuleCheckStatus.Checked))
        {
            var printed = file.Findings.Count(f => f.RuleId == status.RuleId);
            Assert.Equal(printed, status.FindingCount);
        }
    }

    // A committed block .ir can be sidecar-less (e.g. a hand-authored OB); review its logic anyway,
    // the same branch preflight uses. Before this, review threw "Expected a 'SIDECAR' section" and
    // the block was silently unreviewable.
    [Fact]
    public void ReviewFiles_SidecarLessBlock_ReviewsInsteadOfErroring()
    {
        var block = new IrBlock("0", "OB", "OB_Startup", 100, "LAD", "Startup reset.", new[]
        {
            new IrNetwork(1, "Reset a state bit", new[] { new CoilAssignment("SomeState", new Expr.TagRef("Trigger")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var full = IrSerializer.SerializeBlock(block, new[] { sidecar });

        var cut = full.IndexOf("\nSIDECAR", StringComparison.Ordinal);
        Assert.True(cut > 0, "fixture setup: serialized block should contain a SIDECAR section to strip");
        var sidecarLess = full[..cut] + "\n";
        Assert.False(IrParser.HasSidecarSection(sidecarLess)); // the fixture really is sidecar-less

        var path = WriteTempIrFile(sidecarLess);

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        Assert.Null(file.FileError);                 // was non-null ("COULD NOT REVIEW") before the fix
        Assert.Equal("OB_Startup", file.BlockName);
        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-408" && s.Status == RuleCheckStatus.Checked); // rules ran
    }

    // A UDT (TYPE file) is a NAMED, COMMENTED member container. C-001/C-003/C-005/C-201/C-406 all
    // have subjects in one; the rest genuinely don't. This test used to assert "only C-001, rest
    // NotApplicable" — the same collapse the tag-table branch had, and four of those seventeen
    // "not applicable" rules were implementable all along.
    [Fact]
    public void ReviewFiles_TypeKindFile_ChecksTheFiveRulesAUdtHasSubjectsFor()
    {
        var udt = new PlcTypeSource("0", "UDT_Test", "A type.", new[]
        {
            new DbMember("Good", "Bool", Retain: false, StartValue: null),
            new DbMember("Bad_Name", "Bool", Retain: false, StartValue: null),
        });
        var path = WriteTempIrFile(TypeIrSerializer.Serialize(udt));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        Assert.Equal("UDT_Test", file.BlockName);
        Assert.Contains(file.Findings, f => f.RuleId == "C-001" && f.Description.Contains("Bad_Name"));
        foreach (var id in new[] { "C-001", "C-003", "C-005", "C-201", "C-406" })
        {
            Assert.Contains(file.RuleStatuses, s => s.RuleId == id && s.Status == RuleCheckStatus.Checked);
        }

        // This UDT is correctly prefixed and commented, so the two newly-reachable rules that could
        // fire here find nothing — a conforming fixture, paired with the violating one below.
        Assert.DoesNotContain(file.Findings, f => f.RuleId == "C-003");
        Assert.DoesNotContain(file.Findings, f => f.RuleId == "C-201");

        foreach (var id in AllRuleIds.Where(id => id is not ("C-001" or "C-003" or "C-005" or "C-201" or "C-406")))
        {
            Assert.Contains(file.RuleStatuses, s => s.RuleId == id && s.Status == RuleCheckStatus.NotApplicable);
        }
    }

    // The violating counterpart: an unprefixed, uncommented UDT. Before this change both defects
    // were invisible — C-003 and C-201 both reported "TYPE rule support: only C-001 … in Phase 1".
    [Fact]
    public void ReviewFiles_TypeKindFile_UnprefixedUncommented_FlagsC003AndC201()
    {
        var udt = new PlcTypeSource("0", "MotorIOSet", Comment: null, Members: new[]
        {
            new DbMember("Run", "Bool", Retain: false, StartValue: null),
        });
        var path = WriteTempIrFile(TypeIrSerializer.Serialize(udt));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        Assert.Contains(file.Findings, f => f.RuleId == "C-003" && f.Description.Contains("UDT_"));
        Assert.Contains(file.Findings, f => f.RuleId == "C-201");
    }

    // The invariant that makes the three-way split auditable rather than a matter of trust: every
    // rule gets EXACTLY ONE status per file, for every content kind — and no two NotApplicable
    // reasons on one file are the same blanket sentence. A single reason repeated 18 times is
    // precisely what the tag-table and TYPE branches used to emit.
    [Theory]
    [InlineData("tagtable")]
    [InlineData("type")]
    [InlineData("db")]
    public void ReviewFiles_EveryContentKind_OneStatusPerRule_AndNotApplicableReasonsAreRuleSpecific(string kind)
    {
        var content = kind switch
        {
            "tagtable" => TagTableIrSerializer.Serialize(new PlcTagTableSource("0", "Tags", new[]
            {
                new PlcTagSource("1", "DI3_SYS_Start", "Bool", "%I0.0", true, true, true, null),
            })),
            "type" => TypeIrSerializer.Serialize(new PlcTypeSource("0", "UDT_Test", "A type.", new[]
            {
                new DbMember("Run", "Bool", Retain: false, StartValue: null),
            })),
            _ => DbIrSerializer.Serialize(new DbSource("0", "DB_Test", 1, InstanceOfName: null, Comment: "A DB.", Members: Array.Empty<DbMember>())),
        };
        var path = WriteTempIrFile(content);

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        foreach (var id in AllRuleIds)
        {
            Assert.Single(file.RuleStatuses, s => s.RuleId == id);
        }

        var notApplicableReasons = file.RuleStatuses
            .Where(s => s.Status == RuleCheckStatus.NotApplicable)
            .Select(s => s.Reason)
            .ToList();
        Assert.All(notApplicableReasons, r => Assert.False(string.IsNullOrWhiteSpace(r)));
        Assert.True(
            notApplicableReasons.Distinct(StringComparer.Ordinal).Count() >= notApplicableReasons.Count / 2,
            $"{kind}: NotApplicable reasons are near-identical boilerplate ({notApplicableReasons.Count} entries, "
            + $"{notApplicableReasons.Distinct(StringComparer.Ordinal).Count()} distinct) — a blanket per-file phrase is what hid the tag-table gap");
    }

    // C-118 is cross-file: without a --project index the enclosing interface UDT can't be resolved.
    // On a block that DOES use a Step register the rule therefore had a subject and was NOT judged —
    // Skipped, which gates (exit 2). It used to report NotApplicable, indistinguishable from the
    // genuine "this block has no stepped sequence" case below.
    [Fact]
    public void ReviewFiles_StepBlockNoProjectIndex_C118SkippedAndGates()
    {
        var path = WriteTempIrFile(SerializeSequencerBlock());

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-118" && s.Status == RuleCheckStatus.Skipped);
        Assert.Equal(ReviewOutcome.Incomplete, ReviewOutcome.ExitCode(report, allowUnchecked: false));
    }

    // The other half of that split, and the reason it is safe: a block with no Step register at all
    // is genuinely NotApplicable with or without an index — it does not gate, so an ordinary review
    // of ordinary blocks still exits on findings alone.
    [Fact]
    public void ReviewFiles_NonStepBlockNoProjectIndex_C118NotApplicableAndDoesNotGate()
    {
        var block = new IrBlock("0", "FB", "FB_Plain", 1, "LAD", "A plain block.", new[]
        {
            new IrNetwork(1, "Motor start/stop", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var path = WriteTempIrFile(IrSerializer.SerializeBlock(block, new[] { sidecar }));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        foreach (var id in new[] { "C-118", "C-122", "C-125" })
        {
            Assert.Contains(file.RuleStatuses, s => s.RuleId == id && s.Status == RuleCheckStatus.NotApplicable);
        }

        Assert.Empty(ReviewOutcome.UncheckedRules(report));
        Assert.Equal(ReviewOutcome.Clean, ReviewOutcome.ExitCode(report, allowUnchecked: false));
    }

    // With a --project index resolving the interface UDT (Step : Int inside it), C-118 runs Checked
    // and finds nothing — the real FB_ShredderSequencer / UDT_ShredderSequencerIO shape is clean.
    [Fact]
    public void ReviewFiles_StepBlockWithProjectIndex_C118CheckedAndClean()
    {
        var path = WriteTempIrFile(SerializeSequencerBlock());
        var index = TagTypeRegistry.FromSources(
            Array.Empty<DbSource>(),
            new[] { new PlcTypeSource("0", "UDT_SeqIO", null, new[] { new DbMember("Step", "Int", false, null) }) },
            Array.Empty<PlcTagSource>());

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false, index);
        var file = Assert.Single(report.Files);

        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-118" && s.Status == RuleCheckStatus.Checked);
        Assert.DoesNotContain(file.Findings, f => f.RuleId == "C-118");
    }

    // A block whose Step register is referenced through a UDT-typed interface member `IO.Step`.
    private string SerializeSequencerBlock()
    {
        var ioMember = new DbMember("IO", "\"UDT_SeqIO\"", Retain: true, StartValue: null, SetPoint: true,
            NestedMembers: new[] { new DbMember("Step", "Int", false, null) });
        var block = new IrBlock("0", "FB", "FB_Seq", 1, "LAD", "A stepped sequence.", new[]
        {
            new IrNetwork(1, "In cycle", new[]
            {
                new CoilAssignment("InCycle", new Expr.Compare("<>", new Expr.TagRef("IO.Step"), new Expr.Literal("0"))),
            }),
        }, StaticMembers: new[] { ioMember });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        return IrSerializer.SerializeBlock(block, new[] { sidecar });
    }

    // Physical-IO tags keep their underscores by design — that is C-001's own physical-IO FORMAT,
    // not an exemption from checking it. This test used to assert the whole table was NotApplicable
    // (2026-07 wording: "no member-naming check applies"); it now asserts the same conforming tag is
    // CHECKED and clean, which is the claim that was actually wanted. The violating counterparts
    // live in ReviewTagTableRulesTests.
    [Fact]
    public void ReviewFiles_TagTable_ConformingPhysicalIoTag_C001CheckedAndClean()
    {
        var tags = new PlcTagTableSource("0", "Tags", new[]
        {
            new PlcTagSource("1", "DI3_SYS_Start", "Bool", "%I0.0", true, true, true, null),
        });
        var path = WriteTempIrFile(TagTableIrSerializer.Serialize(tags));

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        Assert.Equal("Tags", file.BlockName);
        Assert.DoesNotContain(file.Findings, f => f.RuleId == "C-001");
        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-001" && s.Status == RuleCheckStatus.Checked);
    }
}
