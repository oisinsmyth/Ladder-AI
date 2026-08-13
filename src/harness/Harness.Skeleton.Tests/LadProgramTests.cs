using Harness.Skeleton;

namespace Harness.Skeleton.Tests;

/// <summary>
/// The interpreter, and above all its REFUSALS.
///
/// <para>An interpreter that silently ignored a statement it did not understand would make the whole
/// phase-2 demonstration worthless in the most convincing possible way: the loop would run, the result
/// would be green, and it would be green because a rung was not executed. Every test below that expects
/// an <see cref="UnsupportedIrException"/> is guarding that.</para>
/// </summary>
public class LadProgramTests
{
    private const string Table =
        "TAGTABLE T\n  ROOTID 0\n  TAGS\n"
        + "    Flag 1 : Bool @ %M100.3 ACCESSIBLE VISIBLE WRITABLE\n"
        + "    A 4 : Int @ %MW102 ACCESSIBLE VISIBLE WRITABLE\n"
        + "    B 7 : Int @ %MW104 ACCESSIBLE VISIBLE WRITABLE\n"
        + "    Big A : DInt @ %MD106 ACCESSIBLE VISIBLE WRITABLE\n";

    private static LadProgram Program(params string[] networkLines)
    {
        var body = "BLOCK FC X\nROOTID 0\nNUMBER 1\nLANGUAGE LAD\n\nINTERFACE\n  INPUT\n\nNETWORK 1 \"n\"\n"
                   + string.Join("\n", networkLines.Select(l => "  " + l)) + "\n";

        return new LadProgram().WithTagTable(Table).WithBlock(body);
    }

    private static byte[] Run(LadProgram program, int scans = 1)
    {
        var memory = new byte[512];
        for (var i = 0; i < scans; i++)
            program.Scan(memory);

        return memory;
    }

    // ---------------------------------------------------------------------------------------------
    // Memory, big-endian as S7 lays it out
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void An_Int_is_two_big_endian_bytes_and_a_DInt_is_four()
    {
        var memory = Run(Program(
            "MOVE(EN := TRUE, IN := 258) => A",
            "MOVE(EN := TRUE, IN := 16#01020304) => Big"));

        Assert.Equal(new byte[] { 0x01, 0x02 }, memory[102..104]);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, memory[106..110]);
    }

    [Fact]
    public void An_Int_reads_back_signed()
    {
        var memory = Run(Program("MOVE(EN := TRUE, IN := -3) => A"));
        var program = Program("MOVE(EN := A < 0, IN := 1) => B");
        program.Scan(memory);

        Assert.Equal(1, LadProgram.Read(memory, program.Tags["B"]));
    }

    [Fact]
    public void A_bit_tag_addresses_one_bit_of_one_byte()
    {
        var memory = Run(Program("COIL Flag := TRUE"));

        Assert.Equal(0b0000_1000, memory[100]);
    }

    // ---------------------------------------------------------------------------------------------
    // Execution order
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Statements_within_a_network_run_in_listed_order()
    {
        // ir/SPEC.md: within one kind's contiguous run, statements execute in LISTED order, and real
        // working content depends on it — a MOVE that resets a counter listed before the ADD that
        // increments it nets to 1 on a scan where both fire, not 0.
        var memory = Run(Program(
            "MOVE(EN := TRUE, IN := 0) => A",
            "MOVE(EN := TRUE, IN := 5) => A"));

        Assert.Equal(5, LadProgram.Read(memory, Program().Tags["A"]));
    }

    [Fact]
    public void Blocks_run_in_the_order_they_were_loaded()
    {
        var first = "BLOCK FC F\nROOTID 0\nNUMBER 1\nLANGUAGE LAD\n\nNETWORK 1 \"n\"\n  MOVE(EN := TRUE, IN := 1) => A\n";
        var second = "BLOCK FC S\nROOTID 0\nNUMBER 2\nLANGUAGE LAD\n\nNETWORK 1 \"n\"\n  MOVE(EN := TRUE, IN := 2) => A\n";

        var program = new LadProgram().WithTagTable(Table).WithBlock(first).WithBlock(second);
        var memory = Run(program);

        Assert.Equal(2, LadProgram.Read(memory, program.Tags["A"]));
    }

    // ---------------------------------------------------------------------------------------------
    // Expressions
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("A = B", 5, 5, 1)]
    [InlineData("A = B", 5, 6, 0)]
    [InlineData("A <> B", 5, 6, 1)]
    [InlineData("A >= B", 5, 5, 1)]
    [InlineData("A <= B", 6, 5, 0)]
    [InlineData("A > B", 6, 5, 1)]
    [InlineData("A < B", 6, 5, 0)]
    public void Every_comparison_the_generators_can_emit_is_implemented(string expression, int a, int b, int expected)
    {
        var program = Program(
            $"MOVE(EN := TRUE, IN := {a}) => A",
            $"MOVE(EN := TRUE, IN := {b}) => B",
            $"COIL Flag := {expression}");

        var memory = Run(program);

        Assert.Equal(expected, LadProgram.Read(memory, program.Tags["Flag"]));
    }

    [Fact]
    public void AND_binds_tighter_than_OR_as_the_IR_spec_states()
    {
        // ir/SPEC.md: "A AND B OR C reads unambiguously as (A AND B) OR C".
        var program = Program(
            "MOVE(EN := TRUE, IN := 1) => A",
            "COIL Flag := Flag AND Flag OR A = 1");

        Assert.Equal(1, LadProgram.Read(Run(program), program.Tags["Flag"]));
    }

    [Fact]
    public void A_parenthesised_NOT_and_a_negated_contact_evaluate_the_same()
    {
        var negated = Program("COIL Flag := NOT Flag");
        var standalone = Program("COIL Flag := NOT (Flag)");

        Assert.Equal(LadProgram.Read(Run(negated), negated.Tags["Flag"]),
                     LadProgram.Read(Run(standalone), standalone.Tags["Flag"]));
    }

    // ---------------------------------------------------------------------------------------------
    // Refusals — the reason this class is safe to build a demonstration on
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_statement_kind_it_does_not_implement_is_a_throw_never_a_skipped_line()
    {
        var error = Assert.Throws<UnsupportedIrException>(() =>
            Program("TON(IN := Flag, PT := T#5S) => Flag"));

        Assert.Contains("not implemented by this interpreter", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_undeclared_tag_is_a_throw_rather_than_a_write_to_nowhere()
    {
        Assert.Throws<UnsupportedIrException>(() => Program("MOVE(EN := TRUE, IN := 1) => Missing"));
        Assert.Throws<UnsupportedIrException>(() => Program("MOVE(EN := Missing, IN := 1) => A"));
        Assert.Throws<UnsupportedIrException>(() => Program("MOVE(EN := TRUE, IN := Missing) => A"));
    }

    [Fact]
    public void A_word_tag_used_as_a_contact_is_a_throw()
    {
        var error = Assert.Throws<UnsupportedIrException>(() => Program("COIL Flag := A"));

        Assert.Contains("a contact reads a bit", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_coil_onto_a_word_tag_is_a_throw()
    {
        var error = Assert.Throws<UnsupportedIrException>(() => Program("COIL A := Flag"));

        Assert.Contains("a coil writes a bit", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_address_outside_bit_memory_is_a_throw()
    {
        var error = Assert.Throws<UnsupportedIrException>(() =>
            new LadProgram().WithTagTable("TAGTABLE T\n  ROOTID 0\n  TAGS\n    In 1 : Bool @ %I0.0 ACCESSIBLE\n"));

        Assert.Contains("models %M and nothing else", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tag_declared_twice_is_a_throw_rather_than_the_second_one_quietly_winning()
    {
        Assert.Throws<UnsupportedIrException>(() =>
            new LadProgram().WithTagTable(Table).WithTagTable(Table));
    }

    [Fact]
    public void An_unbalanced_or_malformed_expression_is_a_throw()
    {
        Assert.Throws<UnsupportedIrException>(() => Program("COIL Flag := (Flag"));
        Assert.Throws<UnsupportedIrException>(() => Program("COIL Flag := Flag AND"));
        Assert.Throws<UnsupportedIrException>(() => Program("COIL Flag := AND Flag"));
    }

    [Fact]
    public void Every_line_of_the_generated_program_is_parsed_and_none_is_silently_dropped()
    {
        // The counting matters: an interpreter that skipped a line it did not recognise would still run.
        var copyLayerAndBlock = SkeletonRig.Build();

        var statements = new LadProgram();
        foreach (var table in copyLayerAndBlock.HarnessObjects.Concat(copyLayerAndBlock.ProgramObjects)
                     .Where(o => o.Kind == Harness.Map.HarnessObjectKind.TagTable))
            statements.WithTagTable(table.Ir);

        foreach (var block in copyLayerAndBlock.HarnessObjects.Concat(copyLayerAndBlock.ProgramObjects)
                     .Where(o => o.Kind == Harness.Map.HarnessObjectKind.Block))
            statements.WithBlock(block.Ir);

        // Copy layer: version, scan counter, 2 vector moves, 1 coil, 2 result moves = 7.
        // Block under test: 2 clearing moves, 1 add, 1 done move = 4.
        Assert.Equal(11, statements.StatementCount);
    }
}
