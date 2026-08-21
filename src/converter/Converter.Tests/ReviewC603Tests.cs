using Converter.Ir;
using Converter.Review;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// C-603 — "step membership is enumerated, not ranged" (docs/06, Simplicity &amp; readability).
///
/// WHY THIS RULE IS IN THE RUNNER AT ALL (2026-08-21): a C-603 breach sat in FB_ShredderSequencer
/// for weeks while `converter review` reported `0 finding(s)` on the block every single time. It was
/// eventually found by a human-directed read, and cost two fix dispatches and two review dispatches.
/// A rule the runner can see costs an exit code; a rule it cannot costs a review round.
///
/// EVERY TEST HERE IS TWO-DIRECTIONAL ON PURPOSE. A guard nobody has watched REFUSE anything is
/// decoration, and a guard nobody has watched PASS anything is a rule the next author will delete.
/// The discriminating case is deliberately the real one: FB_ShredderSequencer network 14 before the
/// fix carried THREE ranged coils under ONE comment that stated the range intent for exactly one of
/// them. Any exemption keyed on "the network has a comment" passes all three. This one fails two and
/// passes one, which is the whole reason the exemption is keyed on the SUBJECT'S NAME.
/// </summary>
public class ReviewC603Tests : IDisposable
{
    private readonly List<string> _tempFiles = new();

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

    private string WriteTempIrFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"c603-test-{Guid.NewGuid():N}.ir");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "patterns")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root (no 'patterns' directory found above test output).");
    }

    private static Expr Cmp(string op, string tag, string literal) =>
        new Expr.Compare(op, new Expr.TagRef(tag), new Expr.Literal(literal));

    private static Expr And(params Expr[] operands) => new Expr.And(operands);

    private static Expr Or(params Expr[] operands) => new Expr.Or(operands);

    private static IrBlock OneNetworkBlock(string title, string? comment, params CoilAssignment[] coils) =>
        new("0", "FB", "FB_Seq", 1, "LAD", "A stepped sequence.", new[]
        {
            new IrNetwork(1, title, coils, Comment: comment),
        });

    private FileReviewResult Review(IrBlock block)
    {
        var path = WriteTempIrFile(IrSerializer.SerializeBlockReadable(block));
        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        return Assert.Single(report.Files);
    }

    // ---------------------------------------------------------------------------------------
    // Direction 1: the rule REFUSES.
    // ---------------------------------------------------------------------------------------

    // The bare case: an ordered-range step predicate, no network comment at all, nothing anywhere
    // that could have stated a range intent.
    [Fact]
    public void RangedStepPredicate_NoNetworkComment_IsAFinding()
    {
        var file = Review(OneNetworkBlock(
            "Cycle outputs",
            comment: null,
            new CoilAssignment("IO.RunDischargeConv", Cmp(">=", "IO.Step", "20"))));

        var finding = Assert.Single(file.Findings, f => f.RuleId == "C-603");
        Assert.Equal(FindingSeverity.Warn, finding.Severity);
        Assert.Contains("IO.RunDischargeConv", finding.Description);
        Assert.Contains("IO.Step >= 20", finding.Description);
        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-603" && s.Status == RuleCheckStatus.Checked && s.FindingCount == 1);
    }

    // *** THE EXEMPTION IS NOT "HAS A COMMENT". *** A long, careful, genuinely informative network
    // comment that never mentions this coil has not stated a range intent for this coil.
    [Fact]
    public void RangedStepPredicate_CommentThatNeverNamesTheCoil_IsStillAFinding()
    {
        var file = Review(OneNetworkBlock(
            "Cycle outputs",
            comment: "The cycle runs the machine up in stages, and each stage hands over to the next only once the "
                + "equipment it started has confirmed. Stop handling is two-tier so that losing a downstream drive "
                + "halts the cycle without taking hand-jog away from the operator.",
            new CoilAssignment("IO.RunDischargeConv", Cmp(">=", "IO.Step", "20"))));

        Assert.Single(file.Findings, f => f.RuleId == "C-603");
    }

    // A two-sided span is ONE decision and reports as one finding, quoting both bounds - not two
    // findings the reader has to work out are the same rung.
    [Fact]
    public void TwoSidedSpan_ReportsOnceAndQuotesBothBounds()
    {
        var file = Review(OneNetworkBlock(
            "Pusher commands",
            comment: null,
            new CoilAssignment("IO.PusherParkCmd", And(Cmp(">=", "IO.Step", "20"), Cmp("<=", "IO.Step", "40")))));

        var finding = Assert.Single(file.Findings, f => f.RuleId == "C-603");
        Assert.Contains("IO.Step >= 20", finding.Description);
        Assert.Contains("IO.Step <= 40", finding.Description);
    }

    // All four ordered operators are the rule's subject, in either operand order.
    [Theory]
    [InlineData(">=")]
    [InlineData("<=")]
    [InlineData(">")]
    [InlineData("<")]
    public void EveryOrderedOperator_IsAFinding(string op)
    {
        var file = Review(OneNetworkBlock(
            "Cycle outputs",
            comment: null,
            new CoilAssignment("IO.SomeCmd", Cmp(op, "IO.Step", "20"))));

        Assert.Single(file.Findings, f => f.RuleId == "C-603");
    }

    [Fact]
    public void RangeWrittenWithTheStepOnTheRight_IsAFinding()
    {
        var block = OneNetworkBlock(
            "Cycle outputs",
            comment: null,
            new CoilAssignment("IO.SomeCmd", new Expr.Compare("<=", new Expr.Literal("20"), new Expr.TagRef("IO.Step"))));

        Assert.Single(Review(block).Findings, f => f.RuleId == "C-603");
    }

    // *** THE ONE PLACE THE NAME ANCHOR IS REFUSED. *** "Step" is not distinctive prose in a
    // sequencer - every network comment in the block contains the word - so a range guarding a write
    // to the Step register can never be exempted by naming its own target. It is a finding even here,
    // where the comment is saturated with the word.
    [Fact]
    public void RangeGuardingAWriteToStepItself_IsAFindingEvenWhenTheCommentIsAllAboutSteps()
    {
        var block = new IrBlock("0", "FB", "FB_Seq", 1, "LAD", "A stepped sequence.", new[]
        {
            new IrNetwork(1, "Transitions", Array.Empty<CoilAssignment>(), Moves: new[]
            {
                new MoveStatement(And(Cmp(">=", "IO.Step", "20"), new Expr.TagRef("DischargeConfirmed")), new Expr.Literal("30"), "IO.Step"),
            }, Comment: "The step advances out of the start ramp once the discharge conveyor confirms; the step it "
                + "advances to is the reverse-run step, and the step numbering leaves room for a step to be added "
                + "between any two of these steps later."),
        });

        var finding = Assert.Single(Review(block).Findings, f => f.RuleId == "C-603");
        Assert.Contains("IO.Step", finding.Description);
    }

    // ---------------------------------------------------------------------------------------
    // Direction 2: the rule PASSES.
    // ---------------------------------------------------------------------------------------

    // Enumeration is the shape the rule asks for, and it needs no comment to be allowed.
    [Fact]
    public void EnumeratedMembership_IsNotAFinding()
    {
        var file = Review(OneNetworkBlock(
            "Cycle outputs",
            comment: null,
            new CoilAssignment("IO.RunDischargeConv", Or(
                Cmp("=", "IO.Step", "20"),
                Cmp("=", "IO.Step", "30"),
                Cmp("=", "IO.Step", "40"),
                Cmp("=", "IO.Step", "50")))));

        Assert.DoesNotContain(file.Findings, f => f.RuleId == "C-603");
        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-603" && s.Status == RuleCheckStatus.Checked && s.FindingCount == 0);
    }

    // C-603's own text: "`Step <> 0` and `Step = n` are always fine (C-119 fixes idle = 0)." Neither
    // is ordered, so neither can silently absorb a step inserted between two existing numbers.
    [Fact]
    public void EqualityAndInequality_AreNeverFindings()
    {
        var file = Review(OneNetworkBlock(
            "Cycle status",
            comment: null,
            new CoilAssignment("IO.InCycle", Cmp("<>", "IO.Step", "0")),
            new CoilAssignment("IO.MotorReverseCmd", Cmp("=", "IO.Step", "30"))));

        Assert.DoesNotContain(file.Findings, f => f.RuleId == "C-603");
    }

    // The exemption, working: the comment names the coil, so this is where a reader goes to find the
    // intent, and the tool stops and hands the sentence itself to the reviewer.
    [Fact]
    public void RangedStepPredicate_CommentNamingTheCoil_IsNotAFinding()
    {
        var file = Review(OneNetworkBlock(
            "Pusher commands",
            comment: "PusherParkCmd holds through the machine-start steps 20-40 as a deliberate range - any step "
                + "inserted inside the start ramp still wants the pusher home - long enough for the pusher's warned "
                + "repark to fire even when the discharge confirm is quick.",
            new CoilAssignment("IO.PusherParkCmd", And(Cmp(">=", "IO.Step", "20"), Cmp("<=", "IO.Step", "40")))));

        Assert.DoesNotContain(file.Findings, f => f.RuleId == "C-603");
        Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-603" && s.Status == RuleCheckStatus.Checked);
    }

    // The name anchor is word-boundaried, and a hyphen is part of the word: a comment about
    // `Running`, `runs` or `reverse-run` has not named a coil called `Run`. All three appear here,
    // and none of them exempts.
    [Fact]
    public void CommentContainingTheNameOnlyAsASubstring_DoesNotExempt()
    {
        var file = Review(OneNetworkBlock(
            "Cycle outputs",
            comment: "The machine is Running from the reverse-run step onward, and the conveyor runs with it.",
            new CoilAssignment("IO.Run", Cmp(">=", "IO.Step", "20"))));

        Assert.Single(file.Findings, f => f.RuleId == "C-603");
    }

    // ---------------------------------------------------------------------------------------
    // EMPTY IS NOT CLEAN.
    // ---------------------------------------------------------------------------------------

    // No Step register anywhere: NotApplicable, with its own reason. A zero here means "nothing to
    // read", and must not be reported the same way as "read and clean".
    [Fact]
    public void BlockWithNoStepRegister_IsNotApplicableNotChecked()
    {
        var file = Review(OneNetworkBlock(
            "Motor start",
            comment: null,
            new CoilAssignment("IO.RunCmd", new Expr.TagRef("IO.StartPressed"))));

        var status = Assert.Single(file.RuleStatuses, s => s.RuleId == "C-603");
        Assert.Equal(RuleCheckStatus.NotApplicable, status.Status);
        Assert.Contains("no Step register", status.Reason);
    }

    // A ranged step predicate that is not any write's guarding condition (here, a CALL input
    // argument) cannot be attributed to a subject, so the comment cannot be asked about it by name.
    // *** THAT IS SKIPPED, AND SKIPPED IS EXIT 2. *** It is emphatically not a silent pass.
    [Fact]
    public void RangeOutsideAnyWriteGuard_IsSkippedAndGatesAsIncomplete()
    {
        var block = new IrBlock("0", "FB", "FB_Seq", 1, "LAD", "A stepped sequence.", new[]
        {
            new IrNetwork(1, "Call the downstream conveyor", Array.Empty<CoilAssignment>(), Calls: new[]
            {
                new CallStatement("FB_Conveyor", "ConveyorInst", new Expr.TagRef("IO.SystemHealthy"), new CallArgument[]
                {
                    new CallArgument.InputArg("RunRequest", Cmp(">=", "IO.Step", "20")),
                }),
            }),
        }, StaticMembers: new[] { new DbMember("ConveyorInst", "FB_Conveyor", Retain: false, StartValue: null) });

        var path = WriteTempIrFile(IrSerializer.SerializeBlockReadable(block));
        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        var status = Assert.Single(file.RuleStatuses, s => s.RuleId == "C-603");
        Assert.Equal(RuleCheckStatus.Skipped, status.Status);
        Assert.Contains("IO.Step >= 20", status.Reason);
        Assert.Equal(ReviewOutcome.Incomplete, ReviewOutcome.ExitCode(report, allowUnchecked: false));
    }

    // ---------------------------------------------------------------------------------------
    // The measured case. Both halves of it.
    // ---------------------------------------------------------------------------------------

    // FB_ShredderSequencer networks 14 and 15 AS THEY STOOD BEFORE the 2026-08-21 fix (commit
    // 27bc689), comment text verbatim. Three ranged coils; the comment states the range intent for
    // PusherParkCmd and for nothing else; network 15 had no comment at all.
    //
    // *** THIS IS THE TEST THE WHOLE DESIGN IS FOR. *** Two of network 14's three ranges are findings
    // and the third is not, under one shared comment - which is exactly what a per-network exemption
    // cannot express and what `converter review` reported as `0 finding(s)` for weeks.
    [Fact]
    public void ShredderSequencerAsItStoodBeforeTheFix_FlagsTheUnstatedRangesAndPassesTheStatedOne()
    {
        const string network14Comment =
            "MotorStartArm asserts only through PreStart: one operator start press = one motor arming (FC_ControlMain "
            + "hands it to the motor FB's self-clearing RecentStart - REQ-062). PusherParkCmd holds through the "
            + "machine-start steps 20-40 (range intent, C-603: any step inserted inside the start ramp still wants the "
            + "pusher home) - long enough for the pusher's warned repark to fire even when the discharge confirm is "
            + "quick (REQ-045); from Running onward the pusher's own cycle-entry repark takes over. Pusher stop "
            + "handling is two-tier (REQ-012/013 + owner amendment 2026-07-16): PusherModeForceOff (stop "
            + "button/unhealthy/step-60, mirroring the step-60 overcurrent force) drives mode 0 - parks the pusher and "
            + "blocks jog - while PusherCycleInhibitCmd mirrors the full StopCmd so downstream loss still stops cycle "
            + "behavior without taking hand-jog away (REQ-040).";

        var block = new IrBlock("0", "FB", "FB_ShredderSequencer", 1, "LAD", "Shredder cycle sequencer.", new[]
        {
            new IrNetwork(14, "Motor And Pusher Mode Commands", new[]
            {
                new CoilAssignment("IO.MotorReverseCmd", Cmp("=", "IO.Step", "30")),
                new CoilAssignment("IO.MotorAutoStartCmd", And(Cmp(">=", "IO.Step", "30"), Cmp("<=", "IO.Step", "50"))),
                new CoilAssignment("IO.MotorPreStartDoneCmd", Cmp(">=", "IO.Step", "20")),
                new CoilAssignment("IO.MotorStartArm", Cmp("=", "IO.Step", "10")),
                new CoilAssignment("IO.PusherParkCmd", And(Cmp(">=", "IO.Step", "20"), Cmp("<=", "IO.Step", "40"))),
            }, Comment: network14Comment),
            new IrNetwork(15, "Output Commands", new[]
            {
                new CoilAssignment("IO.RunDischargeConv", Cmp(">=", "IO.Step", "20")),
                new CoilAssignment("IO.InCycle", Cmp("<>", "IO.Step", "0")),
            }),
        });

        var findings = Review(block).Findings.Where(f => f.RuleId == "C-603").ToList();

        Assert.Equal(3, findings.Count);
        Assert.Contains(findings, f => f.Description.Contains("IO.MotorAutoStartCmd"));
        Assert.Contains(findings, f => f.Description.Contains("IO.MotorPreStartDoneCmd"));
        Assert.Contains(findings, f => f.Description.Contains("IO.RunDischargeConv"));

        // The legitimately-retained range, under the same comment as two of the three findings.
        Assert.DoesNotContain(findings, f => f.Description.Contains("PusherParkCmd"));
    }

    // The other half of the same claim, against the file itself rather than a reconstruction: the
    // repaired, committed FB_ShredderSequencer must come back CHECKED and clean - one legitimate
    // range retained, and the rule says so rather than saying nothing.
    [Fact]
    public void CommittedShredderSequencer_IsCheckedAndCleanUnderC603()
    {
        var path = Path.Combine(RepoRoot(), "ir", "test-project001", "FB_ShredderSequencer.ir");
        Assert.True(File.Exists(path), $"Expected the worked C-603 example at {path}");

        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
        var file = Assert.Single(report.Files);

        var status = Assert.Single(file.RuleStatuses, s => s.RuleId == "C-603");
        Assert.Equal(RuleCheckStatus.Checked, status.Status);
        Assert.Equal(0, status.FindingCount);
        Assert.DoesNotContain(file.Findings, f => f.RuleId == "C-603");
    }
}
