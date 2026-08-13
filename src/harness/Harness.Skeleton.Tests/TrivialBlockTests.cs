using Harness.Map;
using Harness.Skeleton;

namespace Harness.Skeleton.Tests;

/// <summary>
/// Build-plan item 2.3 — the one trivial block under test and the one trivial model.
///
/// <para>The IR asserted here is not decoration: it was round-tripped through <c>converter to-xml</c> /
/// <c>to-ir --no-sidecar</c> and came back byte-identical, and the defective variant converts to a
/// different SimaticML <c>Part</c> (<c>Le</c> rather than <c>Lt</c>). So the two builds are two real
/// artifacts, not two branches of a C# flag.</para>
/// </summary>
public class TrivialBlockTests
{
    private static string Block(TrivialBlockDefect defect = TrivialBlockDefect.None) =>
        TrivialBlock.Generate(3000, 901, defect).Single(o => o.Kind == HarnessObjectKind.Block).Ir;

    private static string Table() =>
        TrivialBlock.Generate(3000, 901).Single(o => o.Kind == HarnessObjectKind.TagTable).Ir;

    [Fact]
    public void The_block_is_the_IR_the_converter_round_trips()
    {
        Assert.Equal(
            """
            BLOCK FC FC_DemoRamp
            ROOTID 0
            NUMBER 901
            LANGUAGE LAD
            TITLE "Ramp a count to a limit"

            INTERFACE
              INPUT
              OUTPUT
              CONSTANT

            NETWORK 1 "Hold the count and the done flag at zero while the start command is off"
              MOVE(EN := NOT Demo_Start, IN := 0) => Demo_Count
              MOVE(EN := NOT Demo_Start, IN := 0) => Demo_Done

            NETWORK 2 "Add one step to the count each scan until it reaches the limit"
              ADD(EN := Demo_Start AND Demo_Count < Demo_Limit, IN1 := Demo_Count, IN2 := Demo_Step) => Demo_Count

            NETWORK 3 "Report done once the count has reached the limit"
              MOVE(EN := Demo_Start AND Demo_Count >= Demo_Limit, IN := 1) => Demo_Done

            """.ReplaceLineEndings("\n"),
            Block());
    }

    [Fact]
    public void The_tag_table_is_the_IR_the_converter_round_trips()
    {
        Assert.Equal(
            """
            TAGTABLE DemoUnit
              ROOTID 0
              TAGS
                Demo_Start 1 : Bool @ %M3000.0 ACCESSIBLE VISIBLE WRITABLE COMMENT "Start command. The block runs only while this is on."
                Demo_Step 4 : Int @ %MW3002 ACCESSIBLE VISIBLE WRITABLE COMMENT "Amount added to the count each scan."
                Demo_Limit 7 : Int @ %MW3004 ACCESSIBLE VISIBLE WRITABLE COMMENT "Count at or above which the ramp stops."
                Demo_Count A : Int @ %MW3006 ACCESSIBLE VISIBLE WRITABLE COMMENT "Accumulated count."
                Demo_Done D : Int @ %MW3008 ACCESSIBLE VISIBLE WRITABLE COMMENT "1 once the count has reached the limit."

            """.ReplaceLineEndings("\n"),
            Table());
    }

    [Fact]
    public void The_defect_changes_one_comparison_and_nothing_else()
    {
        Assert.Contains("Demo_Count < Demo_Limit", Block(), StringComparison.Ordinal);
        Assert.Contains("Demo_Count <= Demo_Limit", Block(TrivialBlockDefect.OffByOneAtTheLimit), StringComparison.Ordinal);
    }

    [Fact]
    public void A_block_number_or_a_base_byte_must_be_supplied_rather_than_defaulted()
    {
        // Hard rule 3 for the number; D36's discipline for the address — a deferred measurement must not
        // acquire a default that could be mistaken for one.
        Assert.Throws<ArgumentOutOfRangeException>(() => TrivialBlock.Generate(3000, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrivialBlock.Generate(3001, 901));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrivialBlock.Generate(-2, 901));
    }

    [Fact]
    public void The_binding_wires_the_blocks_own_start_condition_and_not_a_test_only_input()
    {
        // D37/DB-5: a test-only input is scaffolding inside the block under test, and what ships would
        // then not be what was tested. Demo_Start is what makes the block run at all.
        var binding = TrivialBlock.Binding();

        Assert.Equal(TrivialBlock.StartTag, binding.StartCondition);
        Assert.Contains(TrivialBlock.StartTag, Block(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The model
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(5, 10, 10, 2)]     // lands exactly on the limit — the case the defect gets wrong
    [InlineData(3, 10, 12, 4)]     // overshoots either way — the case the defect gets right
    [InlineData(1, 1, 1, 1)]
    [InlineData(7, 0, 0, 0)]       // already at rest
    public void The_model_predicts_the_settled_count_and_the_scans_it_takes(int step, int limit, int count, int scans)
    {
        var prediction = TrivialBlockModel.Predict(step, limit);

        Assert.Equal(count, prediction.Count);
        Assert.Equal(scans, prediction.Scans);
        Assert.Equal(1, prediction.Done);
    }

    [Fact]
    public void A_vector_with_no_settled_outcome_is_refused_rather_than_predicted()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrivialBlockModel.Predict(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrivialBlockModel.Predict(-1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrivialBlockModel.Predict(5, -1));
    }

    [Fact]
    public void A_slot_that_published_too_few_registers_is_judged_FAILED_not_passed_over()
    {
        var verdict = TrivialBlockModel.Judge(5, 10, new ushort[] { 10 });

        Assert.False(verdict.Held);
        Assert.Contains("An unread register is not a passing one", verdict.Detail, StringComparison.Ordinal);
    }
}
