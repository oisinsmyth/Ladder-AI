using Converter.Ir;
using Converter.Review;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// S4 Phase 1 (2026-07-15): mechanical convention-review rule checks (docs/06-lad-conventions.md).
/// True-positive/true-negative coverage for each of the 8 Phase-1 rules — 5 discriminating
/// (C-003, C-005, C-201, C-301/C-501, C-406) and 3 defense-in-depth/vacuous (C-102, C-401,
/// C-404) — built directly against <see cref="Rules"/>, no file I/O, matching this suite's
/// existing "construct the model directly" style (see NetworkTitleCommentTests.cs).
/// </summary>
public class ReviewRulesTests
{
    private static IrBlock MakeBlock(
        string kind,
        string name,
        IReadOnlyList<IrNetwork> networks,
        string? comment = null,
        IReadOnlyList<DbMember>? staticMembers = null,
        IReadOnlyList<DbMember>? tempMembers = null) =>
        new("0", kind, name, 1, "LAD", comment, networks, staticMembers, tempMembers);

    // ---- C-003: block/DB naming prefix ----

    [Fact]
    public void CheckC003BlockPrefix_FbWithoutPrefix_Flags()
    {
        var block = MakeBlock("FB", "MotorDOL", Array.Empty<IrNetwork>());

        var finding = Assert.Single(Rules.CheckC003BlockPrefix(block));
        Assert.Equal("C-003", finding.RuleId);
        Assert.Equal(FindingSeverity.Warn, finding.Severity);
    }

    [Fact]
    public void CheckC003BlockPrefix_FbWithPrefix_Clean()
    {
        var block = MakeBlock("FB", "FB_MotorDOL", Array.Empty<IrNetwork>());

        Assert.Empty(Rules.CheckC003BlockPrefix(block));
    }

    [Fact]
    public void CheckC003BlockPrefix_FcWithoutPrefix_Flags()
    {
        var block = MakeBlock("FC", "PlantAutoControl", Array.Empty<IrNetwork>());

        Assert.Single(Rules.CheckC003BlockPrefix(block));
    }

    [Fact]
    public void CheckC003BlockPrefix_ObHasNoRequiredPrefix_Clean()
    {
        var block = MakeBlock("OB", "Main", Array.Empty<IrNetwork>());

        Assert.Empty(Rules.CheckC003BlockPrefix(block));
    }

    [Fact]
    public void CheckC003DbPrefix_PlainDbWithoutPrefix_Flags()
    {
        var db = new DbSource("0", "AlarmData", 1, InstanceOfName: null, Comment: null, Members: Array.Empty<DbMember>());

        var finding = Assert.Single(Rules.CheckC003DbPrefix(db));
        Assert.Equal(FindingSeverity.Warn, finding.Severity);
    }

    [Fact]
    public void CheckC003DbPrefix_PlainDbWithPrefix_Clean()
    {
        var db = new DbSource("0", "DB_AlarmData", 1, InstanceOfName: null, Comment: null, Members: Array.Empty<DbMember>());

        Assert.Empty(Rules.CheckC003DbPrefix(db));
    }

    [Fact]
    public void CheckC003DbPrefix_InstanceDbWithoutIPrefix_Flags()
    {
        var db = new DbSource("0", "MotorDOL_1", 1, InstanceOfName: "FB_MotorDOL", Comment: null, Members: Array.Empty<DbMember>());

        Assert.Single(Rules.CheckC003DbPrefix(db));
    }

    [Fact]
    public void CheckC003DbPrefix_InstanceDbWithIPrefix_Clean()
    {
        var db = new DbSource("0", "iDB_MotorDOL_1", 1, InstanceOfName: "FB_MotorDOL", Comment: null, Members: Array.Empty<DbMember>());

        Assert.Empty(Rules.CheckC003DbPrefix(db));
    }

    // ---- C-005: letters/digits/underscore only, per dot-separated path component ----

    [Fact]
    public void CheckC005Charset_ValidNames_Clean()
    {
        var network = new IrNetwork(1, "T", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor_A2")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC005Charset(block));
    }

    [Fact]
    public void CheckC005Charset_HyphenInName_Flags()
    {
        var network = new IrNetwork(1, "T", new[] { new CoilAssignment("Output-1", new Expr.TagRef("Sensor1")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var finding = Assert.Single(Rules.CheckC005Charset(block));
        Assert.Equal("C-005", finding.RuleId);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Equal(1, finding.NetworkNumber);
    }

    [Fact]
    public void CheckC005Charset_SliceAddressComponent_SkippedNotFlagged()
    {
        // .%X3 is addressing syntax (an alarm-word bit slice), not a user-chosen name - must not
        // be charset-checked at all, even though '%' itself would fail IsValidIdentifier.
        var network = new IrNetwork(1, "T", new[] { new CoilAssignment("AlarmWord.%X3", new Expr.TagRef("Cond1")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC005Charset(block));
    }

    [Fact]
    public void CheckC005Charset_ArrayIndexSuffix_StrippedBeforeCheck()
    {
        // Recipe[3], unstripped, would fail on '[' / ']' - proves the bracket-suffix strip runs.
        var network = new IrNetwork(1, "T", new[] { new CoilAssignment("Output1", new Expr.TagRef("Recipe[3]")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC005Charset(block));
    }

    [Fact]
    public void CheckC005Charset_LiteralOperand_ExcludedFromCheck()
    {
        // T#100MS contains '#', would fail the charset check if TagReferences ever yielded an
        // Expr.Literal's own value - it must not.
        var network = new IrNetwork(1, "T", Array.Empty<CoilAssignment>(),
            Timers: new[] { new TimerBinding("Timer1", new Expr.TagRef("StartCond"), new Expr.Literal("T#100MS")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC005Charset(block));
    }

    [Fact]
    public void CheckC005CharsetDbMembers_ValidNames_Clean()
    {
        var members = new[] { new DbMember("MotorSpeed", "Real", false, null) };

        Assert.Empty(Rules.CheckC005CharsetDbMembers("DB_Test", members));
    }

    [Fact]
    public void CheckC005CharsetDbMembers_InvalidName_Flags()
    {
        var members = new[] { new DbMember("Motor-Speed", "Real", false, null) };

        var finding = Assert.Single(Rules.CheckC005CharsetDbMembers("DB_Test", members));
        Assert.Equal(FindingSeverity.Error, finding.Severity);
    }

    [Fact]
    public void CheckC005CharsetDbMembers_NestedInvalidName_FlagsViaRecursion()
    {
        var nested = new[] { new DbMember("Bad Name", "Bool", false, null) };
        var members = new[] { new DbMember("Group1", "Struct", false, null, NestedMembers: nested) };

        var finding = Assert.Single(Rules.CheckC005CharsetDbMembers("DB_Test", members));
        Assert.Contains("Bad Name", finding.Description);
    }

    // ---- C-201: every block has a header comment; every non-empty network has a title ----

    [Fact]
    public void CheckC201HeaderComment_Null_Flags()
    {
        Assert.Single(Rules.CheckC201HeaderComment("FB_Test", null));
    }

    [Fact]
    public void CheckC201HeaderComment_Empty_Flags()
    {
        Assert.Single(Rules.CheckC201HeaderComment("FB_Test", string.Empty));
    }

    [Fact]
    public void CheckC201HeaderComment_Present_Clean()
    {
        Assert.Empty(Rules.CheckC201HeaderComment("FB_Test", "Does a thing."));
    }

    [Fact]
    public void CheckC201NetworkTitles_MissingTitleWithRealLogic_Flags()
    {
        var network = new IrNetwork(1, string.Empty, new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var finding = Assert.Single(Rules.CheckC201NetworkTitles(block));
        Assert.Equal(1, finding.NetworkNumber);
    }

    [Fact]
    public void CheckC201NetworkTitles_EmptyNetworkNoTitle_NotFlagged()
    {
        // Mirrors the corpus's own trailing-[empty]-network pattern (TimerSample Network 4) - a
        // network with zero Parts has nothing to title.
        var network = new IrNetwork(4, string.Empty, Array.Empty<CoilAssignment>());
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC201NetworkTitles(block));
    }

    [Fact]
    public void CheckC201NetworkTitles_Titled_Clean()
    {
        var network = new IrNetwork(1, "Motor start/stop", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC201NetworkTitles(block));
    }

    // ---- C-301 / C-501: symbolic addressing only, except alarm-word / comms / data-handling ----

    [Fact]
    public void CheckC301_NoSliceAccess_Clean()
    {
        var network = new IrNetwork(1, "Normal logic", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC301AbsoluteAddressing(block));
    }

    [Fact]
    public void CheckC301_SingleSliceBitTitled_SatisfiesAlarmException_Clean()
    {
        var network = new IrNetwork(1, "High temperature alarm", new[] { new CoilAssignment("AlarmWord.%X3", new Expr.TagRef("Cond1")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC301AbsoluteAddressing(block));
    }

    [Fact]
    public void CheckC301_SingleSliceBitUntitled_FlagsBothC301AndC501()
    {
        var network = new IrNetwork(1, string.Empty, new[] { new CoilAssignment("AlarmWord.%X3", new Expr.TagRef("Cond1")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var findings = Rules.CheckC301AbsoluteAddressing(block).ToList();

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.RuleId == "C-301" && f.Severity == FindingSeverity.Error);
        Assert.Contains(findings, f => f.RuleId == "C-501" && f.Severity == FindingSeverity.Warn);
    }

    [Fact]
    public void CheckC301_MultipleSliceBitsEvenTitled_FlagsBothRules()
    {
        var network = new IrNetwork(1, "Alarm word bits", new[]
        {
            new CoilAssignment("AlarmWord.%X3", new Expr.TagRef("Cond1")),
            new CoilAssignment("AlarmWord.%X4", new Expr.TagRef("Cond2")),
        });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var findings = Rules.CheckC301AbsoluteAddressing(block).ToList();

        Assert.Equal(2, findings.Count);
    }

    [Fact]
    public void CheckC301_DataHandlingSelfIdentifiedBlock_ExemptsSliceAccess_Clean()
    {
        // The exact scoping bug the S4 plan-agent review caught before implementation: C-301
        // names three exceptions (alarm words/C-501, comms mapping, data-handling per C-105), not
        // just the alarm-word one - a data-handling block's own untitled, multi-bit slice access
        // must NOT be flagged (this is the DataHandling.ir shape).
        var network = new IrNetwork(1, string.Empty, new[]
        {
            new CoilAssignment("PackedWord.%X0", new Expr.TagRef("Cond1")),
            new CoilAssignment("PackedWord.%X1", new Expr.TagRef("Cond2")),
        });
        var block = MakeBlock("FB", "FB_DataHandling", new[] { network }, comment: "Data-handling block per C-105, indexed access throughout.");

        Assert.Empty(Rules.CheckC301AbsoluteAddressing(block));
    }

    [Fact]
    public void CheckC301_CommsSelfIdentifiedBlock_ExemptsSliceAccess_Clean()
    {
        var network = new IrNetwork(1, string.Empty, new[] { new CoilAssignment("MappedWord.%X0", new Expr.TagRef("Cond1")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network }, comment: "Handles Modbus communication mapping.");

        Assert.Empty(Rules.CheckC301AbsoluteAddressing(block));
    }

    // ---- C-406: TON is the only permitted timer instruction (declaration + usage forms) ----

    [Fact]
    public void CheckC406TimerUsage_Tonr_Flags()
    {
        var network = new IrNetwork(1, "T", Array.Empty<CoilAssignment>(),
            Timers: new[] { new TimerBinding("Timer1", new Expr.TagRef("Start"), new Expr.Literal("T#5S"), TimerKind.Tonr, new Expr.TagRef("Reset1")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var finding = Assert.Single(Rules.CheckC406TimerUsage(block));
        Assert.Equal(FindingSeverity.Error, finding.Severity);
    }

    [Fact]
    public void CheckC406TimerUsage_Tof_Flags()
    {
        var network = new IrNetwork(1, "T", Array.Empty<CoilAssignment>(),
            Timers: new[] { new TimerBinding("Timer1", new Expr.TagRef("Start"), new Expr.Literal("T#5S"), TimerKind.Tof) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Single(Rules.CheckC406TimerUsage(block));
    }

    [Fact]
    public void CheckC406TimerUsage_Ton_Clean()
    {
        var network = new IrNetwork(1, "T", Array.Empty<CoilAssignment>(),
            Timers: new[] { new TimerBinding("Timer1", new Expr.TagRef("Start"), new Expr.Literal("T#5S")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC406TimerUsage(block));
    }

    [Fact]
    public void CheckC406TimerDeclarations_TonrTime_Flags()
    {
        var members = new[] { new DbMember("Timer1", "TONR_TIME", false, null) };

        var finding = Assert.Single(Rules.CheckC406TimerDeclarations("FB_Test", members));
        Assert.Equal(FindingSeverity.Error, finding.Severity);
    }

    [Fact]
    public void CheckC406TimerDeclarations_TofTime_Flags()
    {
        var members = new[] { new DbMember("Timer1", "TOF_TIME", false, null) };

        Assert.Single(Rules.CheckC406TimerDeclarations("FB_Test", members));
    }

    [Fact]
    public void CheckC406TimerDeclarations_TonTime_Clean()
    {
        var members = new[] { new DbMember("Timer1", "TON_TIME", false, null) };

        Assert.Empty(Rules.CheckC406TimerDeclarations("FB_Test", members));
    }

    [Fact]
    public void CheckC406TimerDeclarations_NestedTonrTime_FlagsViaRecursion()
    {
        var nested = new[] { new DbMember("InnerTimer", "TONR_TIME", false, null) };
        var members = new[] { new DbMember("Group1", "Struct", false, null, NestedMembers: nested) };

        Assert.Single(Rules.CheckC406TimerDeclarations("FB_Test", members));
    }

    // ---- C-102/C-401/C-404: defense-in-depth, structurally vacuous against the current IR model ----

    [Fact]
    public void CheckC102NoJumps_AlwaysEmpty()
    {
        Assert.Empty(Rules.CheckC102NoJumps(MakeBlock("FB", "FB_Test", Array.Empty<IrNetwork>())));
    }

    [Fact]
    public void CheckC401NoCounters_AlwaysEmpty()
    {
        Assert.Empty(Rules.CheckC401NoCounters(MakeBlock("FB", "FB_Test", Array.Empty<IrNetwork>())));
    }

    [Fact]
    public void CheckC404NoBuiltInEdgeInstructions_AlwaysEmpty()
    {
        Assert.Empty(Rules.CheckC404NoBuiltInEdgeInstructions(MakeBlock("FB", "FB_Test", Array.Empty<IrNetwork>())));
    }

    // ---- C-408: a timer's ET is never compared to produce a boolean trigger ----

    [Fact]
    public void CheckC408_EtComparedToConstant_Flags()
    {
        var network = new IrNetwork(1, "Homemade timer", new[]
        {
            new CoilAssignment("Trigger", new Expr.Compare(">", new Expr.TagRef("MyTimer.ET"), new Expr.Literal("T#5S"))),
        });

        var finding = Assert.Single(Rules.CheckC408EtComparison(MakeBlock("FB", "FB_Test", new[] { network })));
        Assert.Equal("C-408", finding.RuleId);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Contains("MyTimer.ET", finding.Description);
    }

    // The owner-confirmed scope call: ET compared against a *non-constant* is a finding too, not
    // only against a literal constant.
    [Fact]
    public void CheckC408_EtComparedToVariable_Flags()
    {
        var network = new IrNetwork(1, "Homemade timer", new[]
        {
            new CoilAssignment("Trigger", new Expr.Compare(">=", new Expr.TagRef("MyTimer.ET"), new Expr.TagRef("Threshold"))),
        });

        Assert.Single(Rules.CheckC408EtComparison(MakeBlock("FB", "FB_Test", new[] { network })));
    }

    // Recursion: a Compare nested inside And/Or is still reached.
    [Fact]
    public void CheckC408_EtComparisonNestedInsideAnd_Flags()
    {
        var network = new IrNetwork(1, "Gated homemade timer", new[]
        {
            new CoilAssignment("Trigger", new Expr.And(new Expr[]
            {
                new Expr.TagRef("Enable"),
                new Expr.Compare(">", new Expr.TagRef("MyTimer.ET"), new Expr.Literal("T#5S")),
            })),
        });

        Assert.Single(Rules.CheckC408EtComparison(MakeBlock("FB", "FB_Test", new[] { network })));
    }

    // Permitted form: reading ET as a *value* (MOVE to a named variable) carries no comparison.
    [Fact]
    public void CheckC408_EtMovedToNamedVariable_Clean()
    {
        var network = new IrNetwork(1, "Display elapsed", Array.Empty<CoilAssignment>(),
            Moves: new[] { new MoveStatement(new Expr.TagRef("Enable"), new Expr.TagRef("MyTimer.ET"), "HMI_Elapsed") });

        Assert.Empty(Rules.CheckC408EtComparison(MakeBlock("FB", "FB_Test", new[] { network })));
    }

    [Fact]
    public void CheckC408_ComparisonWithoutEt_Clean()
    {
        var network = new IrNetwork(1, "Level check", new[]
        {
            new CoilAssignment("HighLevel", new Expr.Compare(">", new Expr.TagRef("Tank_Level"), new Expr.Literal("100"))),
        });

        Assert.Empty(Rules.CheckC408EtComparison(MakeBlock("FB", "FB_Test", new[] { network })));
    }

    // ---- C-001: member/variable names are short PascalCase, underscore-free ----

    [Fact]
    public void CheckC001_UnderscoreMember_Flags()
    {
        var finding = Assert.Single(Rules.CheckC001MemberNames("DB_Input", new[]
        {
            new DbMember("Cycle_Start", "Bool", Retain: false, StartValue: null),
        }));
        Assert.Equal("C-001", finding.RuleId);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Contains("Cycle_Start", finding.Description);
    }

    [Fact]
    public void CheckC001_CamelCaseMember_Flags()
    {
        Assert.Single(Rules.CheckC001MemberNames("FB_X", new[]
        {
            new DbMember("runFlag", "Bool", Retain: false, StartValue: null),
        }));
    }

    [Fact]
    public void CheckC001_PascalCaseMembers_Clean()
    {
        Assert.Empty(Rules.CheckC001MemberNames("FB_X", new[]
        {
            new DbMember("CycleStart", "Bool", Retain: false, StartValue: null),
            new DbMember("FltHigh", "Bool", Retain: false, StartValue: null),
            new DbMember("Q", "Bool", Retain: false, StartValue: null),
            new DbMember("Motor2Run", "Bool", Retain: false, StartValue: null),
        }));
    }

    // All-caps system leaves (a timer's PT/ET/Q) are uppercase + alphanumeric, so they pass.
    [Fact]
    public void CheckC001_AllCapsLeaf_Clean()
    {
        Assert.Empty(Rules.CheckC001MemberNames("iDB_X", new[]
        {
            new DbMember("PT", "Time", Retain: false, StartValue: null),
            new DbMember("ET", "Time", Retain: false, StartValue: null),
        }));
    }

    // Recursion: a nested struct member with a bad name is reached.
    [Fact]
    public void CheckC001_NestedBadMember_FlaggedViaRecursion()
    {
        var parent = new DbMember("Settings", "Struct", Retain: false, StartValue: null, NestedMembers: new[]
        {
            new DbMember("Bad_Name", "Bool", Retain: false, StartValue: null),
        });
        var finding = Assert.Single(Rules.CheckC001MemberNames("DB_X", new[] { parent }));
        Assert.Contains("Bad_Name", finding.Description);
    }
}
