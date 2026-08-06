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

    // C-501 was amended by owner ruling on 2026-08-06: the unit is the alarm WORD, not the bit.
    // The three tests that used to live here encoded the superseded form — and one of them asserted
    // that several bits of one word was a violation, which is now the REQUIRED shape and is what
    // patterns/motor-dol NETWORK 14 has always done. They are replaced, not adjusted.

    [Fact]
    public void CheckC301_WholeAlarmWordInOneCommentedNetwork_Clean()
    {
        // The proven site shape: patterns/motor-dol NETWORK 14 writes three bits of one word.
        var network = new IrNetwork(1, "Alarm", new[]
        {
            new CoilAssignment("IO.Alarm.%X0", new Expr.TagRef("IO.FTR")),
            new CoilAssignment("IO.Alarm.%X1", new Expr.TagRef("IO.FTS")),
            new CoilAssignment("IO.Alarm.%X2", new Expr.TagRef("IO.FaultFB")),
        }, Comment: "%X0 = FTR = \"Pump 1 — fail to run — check starter\"\n%X1 = FTS = \"Pump 1 — fail to stop\"\n%X2 = FaultFB = \"Pump 1 — starter fault\"");
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC301AbsoluteAddressing(block));
    }

    [Fact]
    public void CheckC301_NegatedNamedCause_IsStillASingleNamedCause_Clean()
    {
        var network = new IrNetwork(1, "Alarm", new[]
        {
            new CoilAssignment("IO.Alarm.%X0", new Expr.Not(new Expr.TagRef("IO.Healthy"))),
        }, Comment: "%X0 = NOT Healthy = \"Pump 1 — unhealthy\"");
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC301AbsoluteAddressing(block));
    }

    [Fact]
    public void CheckC301_NoBitMapComment_FlagsBothRules()
    {
        // Condition 3. Titled but uncommented — the shape the SUPERSEDED rule accepted.
        var network = new IrNetwork(1, "High temperature alarm", new[] { new CoilAssignment("AlarmWord.%X3", new Expr.TagRef("Cond1")) });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var findings = Rules.CheckC301AbsoluteAddressing(block).ToList();

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.RuleId == "C-301" && f.Severity == FindingSeverity.Error);
        Assert.Contains(findings, f => f.RuleId == "C-501" && f.Severity == FindingSeverity.Warn);
    }

    [Fact]
    public void CheckC301_BitMapOmitsOneBit_FlagsBothRules()
    {
        // Condition 3, the case that matters: a comment exists, so a reader assumes it is documented.
        var network = new IrNetwork(1, "Alarm", new[]
        {
            new CoilAssignment("IO.Alarm.%X0", new Expr.TagRef("IO.FTR")),
            new CoilAssignment("IO.Alarm.%X1", new Expr.TagRef("IO.FTS")),
        }, Comment: "%X0 = FTR = \"Pump 1 — fail to run\"");
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var findings = Rules.CheckC301AbsoluteAddressing(block).ToList();

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.Description.Contains("%X1"));
    }

    [Fact]
    public void CheckC301_BitsOfTwoDifferentWordsInOneNetwork_FlagsBothRules()
    {
        // Condition 1a — a network's subject is the one word it changes.
        var network = new IrNetwork(1, "Alarms", new[]
        {
            new CoilAssignment("IO.Alarm0.%X0", new Expr.TagRef("IO.FTR")),
            new CoilAssignment("IO.Alarm1.%X0", new Expr.TagRef("IO.FTS")),
        }, Comment: "%X0 = FTR / FTS");
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var findings = Rules.CheckC301AbsoluteAddressing(block).ToList();

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.Description.Contains("different words"));
    }

    [Fact]
    public void CheckC301_OneWordSplitAcrossTwoNetworks_FlagsEachNetwork()
    {
        // Condition 1b — all of a word's bits belong in ONE network. Each offending network is
        // reported, so the reader sees both halves of the split rather than one arbitrary end.
        var first = new IrNetwork(1, "Alarm part 1", new[]
        {
            new CoilAssignment("IO.Alarm.%X0", new Expr.TagRef("IO.FTR")),
        }, Comment: "%X0 = FTR");
        var second = new IrNetwork(2, "Alarm part 2", new[]
        {
            new CoilAssignment("IO.Alarm.%X1", new Expr.TagRef("IO.FTS")),
        }, Comment: "%X1 = FTS");
        var block = MakeBlock("FB", "FB_Test", new[] { first, second });

        var findings = Rules.CheckC301AbsoluteAddressing(block).ToList();

        Assert.Equal(4, findings.Count);
        Assert.Contains(findings, f => f.NetworkNumber ==1 && f.Description.Contains("also written by network(s) 2"));
        Assert.Contains(findings, f => f.NetworkNumber ==2 && f.Description.Contains("also written by network(s) 1"));
    }

    [Fact]
    public void CheckC301_CauseAndedWithNegatedSuppressor_Clean()
    {
        // Owner ruling 2026-08-06: the suppressor term is permitted. C-504 puts filtering in the
        // alarm-write network and nowhere else, so a suppressed bit is NECESSARILY this shape —
        // rejecting it made the two rules jointly forbid the only correct implementation.
        var network = new IrNetwork(1, "Alarm", new[]
        {
            new CoilAssignment("IO.Alarm.%X0", new Expr.And(new Expr[]
            {
                new Expr.TagRef("IO.FTO"),
                new Expr.Not(new Expr.TagRef("IO.SuppFTO")),
            })),
        }, Comment: "%X0 = FTO = \"Valve — failed to open\"");
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC301AbsoluteAddressing(block));
    }

    [Fact]
    public void CheckC301_TwoUnnegatedCausesAnded_StillFlagged()
    {
        // The boundary the ruling did NOT move: two un-negated tags give the bit two plausible
        // subjects, so a reader cannot tell which one it is named for. That is the anonymous logic
        // condition 2 exists to catch, and permitting suppressors must not let it back in.
        var network = new IrNetwork(1, "Alarm", new[]
        {
            new CoilAssignment("IO.Alarm.%X0", new Expr.And(new Expr[]
            {
                new Expr.TagRef("A"),
                new Expr.TagRef("B"),
            })),
        }, Comment: "%X0 = A AND B");
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var findings = Rules.CheckC301AbsoluteAddressing(block).ToList();

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.Description.Contains("inline expression"));
    }

    [Fact]
    public void CheckC301_BitDrivenByInlineExpression_FlagsBothRules()
    {
        // Condition 2 — the relaxation to word-sized networks is only safe because every bit reads
        // as a named cause; an inline expression is what makes %X0 need decoding again. An OR of
        // causes is the canonical case: the bit is true for more than one reason and the network
        // says nothing about which, so C-130's condensed bit is what this should have read instead.
        var network = new IrNetwork(1, "Alarm", new[]
        {
            new CoilAssignment("IO.Alarm.%X0", new Expr.Or(new Expr[] { new Expr.TagRef("A"), new Expr.TagRef("B") })),
        }, Comment: "%X0 = A OR B");
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var findings = Rules.CheckC301AbsoluteAddressing(block).ToList();

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.Description.Contains("inline expression"));
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

    // TIA-informative system params (e.g. an OB's Initial_Call startup-info input) are exempt -
    // they're TIA-provided and not renameable, even with an underscore.
    [Fact]
    public void CheckC001_InformativeSystemParam_Exempt()
    {
        Assert.Empty(Rules.CheckC001MemberNames("Main", new[]
        {
            new DbMember("Initial_Call", "Bool", Retain: false, StartValue: null, Informative: true),
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

    // ---- C-103: Set/Reset pairing within a block (candidate, per-file) ----

    // True positive: a Set (SCOIL) with no matching Reset in the same block is flagged (Warn).
    [Fact]
    public void CheckC103_SetWithoutReset_Flags()
    {
        var network = new IrNetwork(1, "Latch fault", new[]
        {
            new CoilAssignment("FaultLatch", new Expr.TagRef("Trip"), CoilKind.Set),
        });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var finding = Assert.Single(Rules.CheckC103SetResetPairing(block));
        Assert.Equal("C-103", finding.RuleId);
        Assert.Equal(FindingSeverity.Warn, finding.Severity);
        Assert.Equal(1, finding.NetworkNumber);
        Assert.Contains("FaultLatch", finding.Description);
    }

    // True positive: a Reset (RCOIL) with no matching Set in the same block is flagged too.
    [Fact]
    public void CheckC103_ResetWithoutSet_Flags()
    {
        var network = new IrNetwork(1, "Clear fault", new[]
        {
            new CoilAssignment("FaultLatch", new Expr.TagRef("Ack"), CoilKind.Reset),
        });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var finding = Assert.Single(Rules.CheckC103SetResetPairing(block));
        Assert.Contains("no matching Set", finding.Description);
    }

    // True negative: a Set and its matching Reset in the same block (different networks) are clean.
    [Fact]
    public void CheckC103_PairedSetAndReset_Clean()
    {
        var setNet = new IrNetwork(1, "Latch", new[]
        {
            new CoilAssignment("FaultLatch", new Expr.TagRef("Trip"), CoilKind.Set),
        });
        var resetNet = new IrNetwork(2, "Unlatch", new[]
        {
            new CoilAssignment("FaultLatch", new Expr.TagRef("Ack"), CoilKind.Reset),
        });
        var block = MakeBlock("FB", "FB_Test", new[] { setNet, resetNet });

        Assert.Empty(Rules.CheckC103SetResetPairing(block));
    }

    // True negative: plain (Assign) coils are not Set/Reset and never trip C-103.
    [Fact]
    public void CheckC103_PlainCoilsOnly_Clean()
    {
        var network = new IrNetwork(1, "Outputs", new[]
        {
            new CoilAssignment("Motor", new Expr.TagRef("RunCmd")),
            new CoilAssignment("Lamp", new Expr.TagRef("Fault")),
        });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC103SetResetPairing(block));
    }

    // ---- C-121: step transition is a plain MOVE to Step guarded by inline `Step = <from>` ----

    // True positive (case b): a MOVE to Step with no inline `Step = <from>` guard in its EN.
    [Fact]
    public void CheckC121_MoveToStepWithoutGuard_Flags()
    {
        var network = new IrNetwork(1, "Bad transition", Array.Empty<CoilAssignment>(),
            Moves: new[] { new MoveStatement(new Expr.TagRef("StartCond"), new Expr.Literal("10"), "IO.Step") });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var finding = Assert.Single(Rules.CheckC121StepTransition(block));
        Assert.Equal("C-121", finding.RuleId);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Contains("no inline", finding.Description);
    }

    // True positive (case a): a Step register written by a coil, not a MOVE, is a hard defect.
    [Fact]
    public void CheckC121_CoilWritesStep_Flags()
    {
        var network = new IrNetwork(1, "Wrong: coil to Step", new[]
        {
            new CoilAssignment("Step", new Expr.TagRef("SomeCond")),
        });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        var finding = Assert.Single(Rules.CheckC121StepTransition(block));
        Assert.Equal("C-121", finding.RuleId);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Contains("not a MOVE", finding.Description);
    }

    // True negative: a properly-guarded MOVE-to-Step transition (the real sequencer shape:
    // `MOVE(EN := ... AND IO.Step = 10, IN := 20) => IO.Step`) is clean.
    [Fact]
    public void CheckC121_GuardedMoveToStep_Clean()
    {
        var en = new Expr.And(new Expr[]
        {
            new Expr.TagRef("PreStartTimer.Q"),
            new Expr.Compare("=", new Expr.TagRef("IO.Step"), new Expr.Literal("10")),
        });
        var network = new IrNetwork(1, "Step 10 transition", Array.Empty<CoilAssignment>(),
            Moves: new[] { new MoveStatement(en, new Expr.Literal("20"), "IO.Step") });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC121StepTransition(block));
    }

    // True negative: MOVEs to non-Step destinations (e.g. a ReversalCount reset) are ignored.
    [Fact]
    public void CheckC121_MoveToNonStepDest_Clean()
    {
        var network = new IrNetwork(1, "Reset counter", Array.Empty<CoilAssignment>(),
            Moves: new[] { new MoveStatement(new Expr.TagRef("FaultReset"), new Expr.Literal("0"), "ReversalCount") });
        var block = MakeBlock("FB", "FB_Test", new[] { network });

        Assert.Empty(Rules.CheckC121StepTransition(block));
    }

    // ---- C-118: the phase register is one `Step : Int` inside the block's interface UDT (cross-file) ----

    // A block whose Step logic writes/reads `IO.Step`, where interface member `IO` is typed as a UDT
    // the resolver knows, carrying a single `Step : Int`. Mirrors FB_ShredderSequencer's real shape.
    private static IrBlock MakeSequencerBlock(string udtTypeName)
    {
        var ioMember = new DbMember("IO", $"\"{udtTypeName}\"", Retain: true, StartValue: null, SetPoint: true);
        var en = new Expr.Compare("=", new Expr.TagRef("IO.Step"), new Expr.Literal("0"));
        var network = new IrNetwork(6, "Step 0 (Idle) - Transition", Array.Empty<CoilAssignment>(),
            Moves: new[] { new MoveStatement(en, new Expr.Literal("10"), "IO.Step") });
        return new IrBlock("0", "FB", "FB_Sequencer", 1, "LAD", "A stepped sequence.", new[] { network },
            StaticMembers: new[] { ioMember });
    }

    private static TagTypeRegistry IndexWithUdt(string udtName, params DbMember[] members) =>
        TagTypeRegistry.FromSources(
            Array.Empty<DbSource>(),
            new[] { new PlcTypeSource("0", udtName, null, members) },
            Array.Empty<PlcTagSource>());

    // True negative: `Step : Int` inside the resolvable interface UDT is clean (no false positive) —
    // this is the exact FB_ShredderSequencer / UDT_ShredderSequencerIO shape.
    [Fact]
    public void CheckC118_StepIntInInterfaceUdt_Clean()
    {
        var block = MakeSequencerBlock("UDT_SeqIO");
        var index = IndexWithUdt("UDT_SeqIO",
            new DbMember("CycleStart", "Bool", false, null),
            new DbMember("Step", "Int", false, null));

        Assert.Empty(Rules.CheckC118StepInterfaceUdt(block, index));
    }

    // True positive: a Step register referenced as a bare block-local `Step` (a private Static),
    // not through the interface UDT.
    [Fact]
    public void CheckC118_BareBlockLocalStep_Flags()
    {
        var en = new Expr.Compare("=", new Expr.TagRef("Step"), new Expr.Literal("0"));
        var network = new IrNetwork(1, "Bad: bare Step", Array.Empty<CoilAssignment>(),
            Moves: new[] { new MoveStatement(en, new Expr.Literal("10"), "Step") });
        var block = new IrBlock("0", "FB", "FB_Bad", 1, "LAD", "c", new[] { network },
            StaticMembers: new[] { new DbMember("Step", "Int", false, null) });
        var index = IndexWithUdt("UDT_SeqIO", new DbMember("Step", "Int", false, null));

        var finding = Assert.Single(Rules.CheckC118StepInterfaceUdt(block, index));
        Assert.Equal("C-118", finding.RuleId);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Contains("bare", finding.Description);
    }

    // True positive: `Step` lives in the interface UDT but is not `Int`.
    [Fact]
    public void CheckC118_StepNonIntInUdt_Flags()
    {
        var block = MakeSequencerBlock("UDT_SeqIO");
        var index = IndexWithUdt("UDT_SeqIO", new DbMember("Step", "DInt", false, null));

        var finding = Assert.Single(Rules.CheckC118StepInterfaceUdt(block, index));
        Assert.Equal("C-118", finding.RuleId);
        Assert.Contains("not `Int`", finding.Description);
    }

    // True positive: the phase register lives in a Controls/Settings DB, not the interface UDT.
    [Fact]
    public void CheckC118_StepInControlsDb_Flags()
    {
        var en = new Expr.Compare("=", new Expr.TagRef("DB_Controls.Step"), new Expr.Literal("0"));
        var network = new IrNetwork(1, "Bad: Step in DB", Array.Empty<CoilAssignment>(),
            Moves: new[] { new MoveStatement(en, new Expr.Literal("10"), "DB_Controls.Step") });
        var block = new IrBlock("0", "FB", "FB_Bad", 1, "LAD", "c", new[] { network });
        var index = TagTypeRegistry.FromSources(
            new[] { new DbSource("0", "DB_Controls", 1, InstanceOfName: null, Comment: null, Members: new[] { new DbMember("Step", "Int", false, null) }) },
            Array.Empty<PlcTypeSource>(),
            Array.Empty<PlcTagSource>());

        var finding = Assert.Single(Rules.CheckC118StepInterfaceUdt(block, index));
        Assert.Contains("DB_Controls", finding.Description);
    }

    // True positive: more than one `Step` member in the interface UDT.
    [Fact]
    public void CheckC118_MultipleStepMembers_Flags()
    {
        var block = MakeSequencerBlock("UDT_SeqIO");
        var index = IndexWithUdt("UDT_SeqIO",
            new DbMember("Step", "Int", false, null),
            new DbMember("Step", "Int", false, null));

        Assert.Contains(Rules.CheckC118StepInterfaceUdt(block, index), f => f.Description.Contains("declares 2"));
    }

    // True negative: a block with no Step logic at all yields nothing (rule has nothing to place).
    [Fact]
    public void CheckC118_NoStepLogic_Clean()
    {
        var network = new IrNetwork(1, "Plain", new[] { new CoilAssignment("Motor", new Expr.TagRef("RunCmd")) });
        var block = MakeBlock("FB", "FB_Plain", new[] { network });
        var index = IndexWithUdt("UDT_SeqIO", new DbMember("Step", "Int", false, null));

        Assert.Empty(Rules.CheckC118StepInterfaceUdt(block, index));
    }

    // ---- C-119: idle/home is always step 0 (single-file step-number census) ----

    // A stepper whose step set is exactly the transitions used, with the given IN literals. Each
    // transition MOVE is `MOVE(EN := Step = <from>, IN := <to>) => IO.Step` — so the union of the
    // MOVE-IN literals and the EN `Step = <from>` literals is the block's step-number set.
    private static IrBlock MakeStepperWithTransitions(params (int From, int To)[] transitions)
    {
        var moves = transitions
            .Select(t => new MoveStatement(
                new Expr.Compare("=", new Expr.TagRef("IO.Step"), new Expr.Literal(t.From.ToString())),
                new Expr.Literal(t.To.ToString()),
                "IO.Step"))
            .ToArray();
        var network = new IrNetwork(1, "Transitions", Array.Empty<CoilAssignment>(), Moves: moves);
        return MakeBlock("FB", "FB_Seq", new[] { network });
    }

    // True positive: a stepped sequence with no step 0 (steps 10, 20, 30) is flagged.
    [Fact]
    public void CheckC119_NoStepZero_Flags()
    {
        var block = MakeStepperWithTransitions((10, 20), (20, 30));

        var finding = Assert.Single(Rules.CheckC119IdleIsStepZero(block));
        Assert.Equal("C-119", finding.RuleId);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Contains("no step 0", finding.Description);
    }

    // True negative: a stepped sequence that includes step 0 is clean.
    [Fact]
    public void CheckC119_HasStepZero_Clean()
    {
        var block = MakeStepperWithTransitions((0, 10), (10, 20));

        Assert.Empty(Rules.CheckC119IdleIsStepZero(block));
    }

    // True negative: a block with no step logic at all yields nothing (nothing to place).
    [Fact]
    public void CheckC119_NoStepLogic_Clean()
    {
        var network = new IrNetwork(1, "Plain", new[] { new CoilAssignment("Motor", new Expr.TagRef("RunCmd")) });
        var block = MakeBlock("FB", "FB_Plain", new[] { network });

        Assert.Empty(Rules.CheckC119IdleIsStepZero(block));
    }

    // ---- C-120: steps ascend in multiples of 10 (single-file) ----

    // True positive: step 15 (not a multiple of 10) is flagged.
    [Fact]
    public void CheckC120_StepNotMultipleOfTen_Flags()
    {
        var block = MakeStepperWithTransitions((0, 10), (10, 15));

        var finding = Assert.Single(Rules.CheckC120StepsMultipleOfTen(block));
        Assert.Equal("C-120", finding.RuleId);
        Assert.Equal(FindingSeverity.Warn, finding.Severity);
        Assert.Contains("15", finding.Description);
    }

    // True negative: all steps multiples of 10 → clean.
    [Fact]
    public void CheckC120_AllMultiplesOfTen_Clean()
    {
        var block = MakeStepperWithTransitions((0, 10), (10, 20), (20, 30));

        Assert.Empty(Rules.CheckC120StepsMultipleOfTen(block));
    }

    // ---- C-122: step-dwell timer shape (cross-file for the PT-home part) ----

    // A step-dwell timer network: one TON whose IN is `Step = <step>` (subject + specific-step
    // gate), instanced at `instancePath`, PT from `pt`.
    private static IrBlock MakeDwellTimerBlock(string udtTypeName, string instancePath, Expr pt, Expr? inGate = null)
    {
        var gate = inGate ?? new Expr.Compare("=", new Expr.TagRef("IO.Step"), new Expr.Literal("10"));
        var network = new IrNetwork(1, "Step 10 dwell", Array.Empty<CoilAssignment>(),
            Timers: new[] { new TimerBinding(instancePath, gate, pt) });
        var ioMember = new DbMember("IO", $"\"{udtTypeName}\"", Retain: true, StartValue: null, SetPoint: true);
        return new IrBlock("0", "FB", "FB_Seq", 1, "LAD", "A stepped sequence.", new[] { network },
            StaticMembers: new[] { ioMember, new DbMember(instancePath.Split('.')[0], "TON_TIME", false, null) });
    }

    // The interface UDT carrying both Step:Int and a settings member DwellTime:Time (the C-122
    // per-instance PT home).
    private static TagTypeRegistry DwellIndex(string udtName = "UDT_SeqIO") =>
        IndexWithUdt(udtName,
            new DbMember("Step", "Int", false, null),
            new DbMember("DwellTime", "Time", false, null));

    // True negative: a well-formed dwell timer — IN gated `Step = 10`, multi-instance in Static,
    // PT from a UDT settings member (IO.DwellTime) — is clean (no false positive).
    [Fact]
    public void CheckC122_WellFormedDwellTimer_Clean()
    {
        var block = MakeDwellTimerBlock("UDT_SeqIO", "DwellTimer", new Expr.TagRef("IO.DwellTime"));

        Assert.Empty(Rules.CheckC122DwellTimerShape(block, DwellIndex()));
    }

    // True negative: a bare block-local converted setpoint PT (a Static DInt member, the
    // MUL+CONVERT ms-idiom) is NOT flagged — this is FB_ShredderSequencer's real shape.
    [Fact]
    public void CheckC122_LocalConvertedSetpointPt_Clean()
    {
        var block = MakeDwellTimerBlock("UDT_SeqIO", "DwellTimer", new Expr.TagRef("DwellTimeMS"));

        Assert.Empty(Rules.CheckC122DwellTimerShape(block, DwellIndex()));
    }

    // True negative: a timer with no Step relationship in its IN is not a C-122 subject — skipped
    // entirely, no findings (mirrors UpstreamEnableTimer, fed by another timer's Q).
    [Fact]
    public void CheckC122_NonDwellTimerInStepper_Skipped()
    {
        var block = MakeDwellTimerBlock("UDT_SeqIO", "DerivedTimer", new Expr.TagRef("DB_Timers.Foo"),
            inGate: new Expr.TagRef("InfeedRunning"));

        Assert.Empty(Rules.CheckC122DwellTimerShape(block, DwellIndex()));
    }

    // True positive (a): a step-dwell timer whose IN is Step-gated but only by a NON-equality
    // comparison (`Step >= 30`) — can't self-reset per step.
    [Fact]
    public void CheckC122_InGateNotEquality_Flags()
    {
        var block = MakeDwellTimerBlock("UDT_SeqIO", "DwellTimer", new Expr.TagRef("IO.DwellTime"),
            inGate: new Expr.Compare(">=", new Expr.TagRef("IO.Step"), new Expr.Literal("30")));

        var finding = Assert.Single(Rules.CheckC122DwellTimerShape(block, DwellIndex()));
        Assert.Equal("C-122", finding.RuleId);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Contains("equality gate", finding.Description);
    }

    // True positive (b): a step-dwell timer instanced in DB_Timers rather than the block's Static.
    [Fact]
    public void CheckC122_InstancedInDbTimers_Flags()
    {
        var block = MakeDwellTimerBlock("UDT_SeqIO", "DB_Timers.DwellTimer", new Expr.TagRef("IO.DwellTime"));

        var finding = Assert.Single(Rules.CheckC122DwellTimerShape(block, DwellIndex()));
        Assert.Equal("C-122", finding.RuleId);
        Assert.Contains("DB_Timers", finding.Description);
    }

    // True positive (c): PT sourced directly from a DB, not a UDT settings member.
    [Fact]
    public void CheckC122_PtFromDb_Flags()
    {
        var block = MakeDwellTimerBlock("UDT_SeqIO", "DwellTimer", new Expr.TagRef("DB_Settings.DwellTime"));
        var index = TagTypeRegistry.FromSources(
            new[] { new DbSource("0", "DB_Settings", 1, InstanceOfName: null, Comment: null, Members: new[] { new DbMember("DwellTime", "Time", false, null) }) },
            new[] { new PlcTypeSource("0", "UDT_SeqIO", null, new[] { new DbMember("Step", "Int", false, null) }) },
            Array.Empty<PlcTagSource>());

        var finding = Assert.Single(Rules.CheckC122DwellTimerShape(block, index));
        Assert.Equal("C-122", finding.RuleId);
        Assert.Contains("DB_Settings", finding.Description);
    }

    // Cross-file gating: reviewed without a --project index, C-122 is recorded NotApplicable (its
    // PT-home part can't resolve the interface UDT) — never silently absent. Goes through
    // ReviewRunner, the only place the NotApplicable status is produced.
    [Fact]
    public void ReviewFiles_NoProjectIndex_C122NotApplicable()
    {
        var block = new IrBlock("0", "FB", "FB_Seq", 1, "LAD", "c", new[]
        {
            new IrNetwork(1, "T", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var path = Path.Combine(Path.GetTempPath(), $"c122-na-{Guid.NewGuid():N}.ir");
        File.WriteAllText(path, IrSerializer.SerializeBlock(block, new[] { sidecar }));
        try
        {
            var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);

            var file = Assert.Single(report.Files);
            Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-122" && s.Status == RuleCheckStatus.NotApplicable);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- C-125: a C-122 dwell-timeout timer's fault bit lives in the interface UDT (cross-file) ----

    // A step-10 dwell-timeout network mirroring FB_PusherControl N8: a Step-gated TON
    // (EndTravelTimer, IN := IO.Step = 10) and a self-latch fault coil written to `faultCoilTag`,
    // cleared by `NOT IO.FaultReset` — the exact timeout-fault shape C-125 keys off.
    private static IrBlock MakeTimeoutFaultBlock(string faultCoilTag, string udtTypeName = "UDT_SeqIO")
    {
        var timerGate = new Expr.Compare("=", new Expr.TagRef("IO.Step"), new Expr.Literal("10"));
        var faultCond = new Expr.And(new Expr[]
        {
            new Expr.Or(new Expr[]
            {
                new Expr.TagRef("EndTravelTimer.Q"),
                new Expr.And(new Expr[]
                {
                    new Expr.TagRef(faultCoilTag),
                    new Expr.Not(new Expr.TagRef("IO.FaultReset")),
                }),
            }),
            new Expr.TagRef("IO.Fitted"),
        });
        var network = new IrNetwork(8, "Step 10 - Timer And Timeout Fault",
            new[] { new CoilAssignment(faultCoilTag, faultCond) },
            Timers: new[] { new TimerBinding("EndTravelTimer", timerGate, new Expr.TagRef("IO.EndTravelTimeout")) });
        var ioMember = new DbMember("IO", $"\"{udtTypeName}\"", Retain: true, StartValue: null, SetPoint: true);
        return new IrBlock("0", "FB", "FB_Seq", 1, "LAD", "A stepped sequence.", new[] { network },
            StaticMembers: new[] { ioMember, new DbMember("EndTravelTimer", "TON_TIME", false, null) });
    }

    // True negative: the timeout-fault bit is a member of the resolvable interface UDT (IO.<...>Fault)
    // — the real FB_PusherControl / FB_ShredderSequencer shape. Clean, no false positive.
    [Fact]
    public void CheckC125_FaultInInterfaceUdt_Clean()
    {
        var block = MakeTimeoutFaultBlock("IO.EndTravelTimeoutFault");
        var index = IndexWithUdt("UDT_SeqIO",
            new DbMember("Step", "Int", false, null),
            new DbMember("EndTravelTimeoutFault", "Bool", false, null));

        Assert.Empty(Rules.CheckC125TimeoutFaultInInterfaceUdt(block, index));
    }

    // True positive: the SAME timeout-fault shape but the fault bit is a bare private Static
    // (not in the interface UDT) → C-125 warn.
    [Fact]
    public void CheckC125_FaultInBarePrivateStatic_Flags()
    {
        var block = MakeTimeoutFaultBlock("EndTravelTimeoutFault");
        var index = IndexWithUdt("UDT_SeqIO", new DbMember("Step", "Int", false, null));

        var finding = Assert.Single(Rules.CheckC125TimeoutFaultInInterfaceUdt(block, index));
        Assert.Equal("C-125", finding.RuleId);
        Assert.Equal(FindingSeverity.Warn, finding.Severity);
        Assert.Equal(8, finding.NetworkNumber);
        Assert.Contains("EndTravelTimeoutFault", finding.Description);
    }

    // True positive: the timeout-fault bit lives in a DB, not the interface UDT → C-125 warn.
    [Fact]
    public void CheckC125_FaultInDb_Flags()
    {
        var block = MakeTimeoutFaultBlock("DB_Alarms.EndTravelTimeoutFault");
        var index = TagTypeRegistry.FromSources(
            new[] { new DbSource("0", "DB_Alarms", 1, InstanceOfName: null, Comment: null, Members: new[] { new DbMember("EndTravelTimeoutFault", "Bool", false, null) }) },
            new[] { new PlcTypeSource("0", "UDT_SeqIO", null, new[] { new DbMember("Step", "Int", false, null) }) },
            Array.Empty<PlcTagSource>());

        var finding = Assert.Single(Rules.CheckC125TimeoutFaultInInterfaceUdt(block, index));
        Assert.Equal("C-125", finding.RuleId);
        Assert.Contains("DB_Alarms", finding.Description);
    }

    // True negative (the PressureHold false-positive guard): a HOLD bit reads a Step-gated timer's
    // .Q but its name does NOT end in "Fault" and it is cleared by another timer's Q, not FaultReset
    // — so it is NOT a timeout-fault subject and is never flagged, even as a bare private Static.
    [Fact]
    public void CheckC125_HoldBitNotFault_NotFlagged()
    {
        var timerGate = new Expr.And(new Expr[]
        {
            new Expr.Compare("=", new Expr.TagRef("IO.Step"), new Expr.Literal("10")),
            new Expr.TagRef("IO.HighPressure"),
        });
        var holdCond = new Expr.Or(new Expr[]
        {
            new Expr.TagRef("PressureConfirmTimer.Q"),
            new Expr.And(new Expr[]
            {
                new Expr.TagRef("PressureHold"),
                new Expr.Not(new Expr.TagRef("PressureClearTimer.Q")),
            }),
        });
        var network = new IrNetwork(5, "Pressure Hold",
            new[] { new CoilAssignment("PressureHold", holdCond) },
            Timers: new[] { new TimerBinding("PressureConfirmTimer", timerGate, new Expr.TagRef("IO.ConfirmTime")) });
        var ioMember = new DbMember("IO", "\"UDT_SeqIO\"", Retain: true, StartValue: null, SetPoint: true);
        var block = new IrBlock("0", "FB", "FB_Seq", 1, "LAD", "A stepped sequence.", new[] { network },
            StaticMembers: new[] { ioMember, new DbMember("PressureConfirmTimer", "TON_TIME", false, null), new DbMember("PressureHold", "Bool", false, null) });
        var index = IndexWithUdt("UDT_SeqIO", new DbMember("Step", "Int", false, null));

        Assert.Empty(Rules.CheckC125TimeoutFaultInInterfaceUdt(block, index));
    }

    // True negative: a block with no Step-gated (C-122-subject) timer has no dwell-timeout to place
    // — yields nothing regardless of any Fault-named coils.
    [Fact]
    public void CheckC125_NoDwellTimer_Clean()
    {
        var faultCond = new Expr.Or(new Expr[]
        {
            new Expr.TagRef("SomeTrip"),
            new Expr.And(new Expr[] { new Expr.TagRef("SomeFault"), new Expr.Not(new Expr.TagRef("IO.FaultReset")) }),
        });
        var network = new IrNetwork(1, "Plain fault latch", new[] { new CoilAssignment("SomeFault", faultCond) });
        var ioMember = new DbMember("IO", "\"UDT_SeqIO\"", Retain: true, StartValue: null, SetPoint: true);
        var block = new IrBlock("0", "FB", "FB_Seq", 1, "LAD", "c", new[] { network }, StaticMembers: new[] { ioMember });
        var index = IndexWithUdt("UDT_SeqIO", new DbMember("Step", "Int", false, null));

        Assert.Empty(Rules.CheckC125TimeoutFaultInInterfaceUdt(block, index));
    }

    // Cross-file gating: reviewed without a --project index, C-125 is recorded NotApplicable (its
    // fault-bit-home check can't resolve the interface UDT) — never silently absent. Goes through
    // ReviewRunner, the only place the NotApplicable status is produced.
    [Fact]
    public void ReviewFiles_NoProjectIndex_C125NotApplicable()
    {
        var block = new IrBlock("0", "FB", "FB_Seq", 1, "LAD", "c", new[]
        {
            new IrNetwork(1, "T", new[] { new CoilAssignment("Output1", new Expr.TagRef("Sensor1")) }),
        });
        var sidecar = new NetworkSidecar(1, "3", Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>());
        var path = Path.Combine(Path.GetTempPath(), $"c125-na-{Guid.NewGuid():N}.ir");
        File.WriteAllText(path, IrSerializer.SerializeBlock(block, new[] { sidecar }));
        try
        {
            var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);

            var file = Assert.Single(report.Files);
            Assert.Contains(file.RuleStatuses, s => s.RuleId == "C-125" && s.Status == RuleCheckStatus.NotApplicable);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
