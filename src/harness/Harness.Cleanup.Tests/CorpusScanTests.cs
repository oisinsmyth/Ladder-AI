namespace Harness.Cleanup.Tests;

/// <summary>The corpus walk, and the identity rules it must not get wrong.</summary>
public class CorpusScanTests
{
    [Fact]
    public void An_objects_identity_comes_from_ITS_OWN_CONTENT_and_not_from_its_filename()
    {
        // drift-check was repaired for exactly this on 2026-08-14: TIA's name for the default tag table
        // contains SPACES and the .ir filename does not, and pairing on the filename made one object fail
        // to pair and then counted it twice.
        using var corpus = new TempCorpus();
        corpus.TagTable("Default tag table", "DefaultTagTable");

        var scan = CorpusScan.Read(corpus.Ir);

        Assert.Equal("Default tag table", scan.Objects.Single().Name);
    }

    [Theory]
    [InlineData("BLOCK FB FB_X\nNUMBER 9001\n", "FB_X", IrObjectKind.Block, 9001)]
    [InlineData("BLOCK FC FC_X\nNUMBER 12\n", "FC_X", IrObjectKind.Block, 12)]
    [InlineData("BLOCK OB Main\nNUMBER 1\n", "Main", IrObjectKind.Block, 1)]
    [InlineData("DB DB_X\n  NUMBER 5\n  MEMBERS\n", "DB_X", IrObjectKind.GlobalDb, 5)]
    [InlineData("TYPE UDT_X\n  MEMBERS\n", "UDT_X", IrObjectKind.Type, null)]
    [InlineData("TAGTABLE HarnessMirror\n  TAGS\n", "HarnessMirror", IrObjectKind.TagTable, null)]
    public void The_four_header_forms_all_parse(string text, string name, IrObjectKind kind, int? number)
    {
        var parsed = CorpusScan.ParseHeader(text, "x.ir");

        Assert.NotNull(parsed);
        Assert.Equal(name, parsed!.Name);
        Assert.Equal(kind, parsed.Kind);
        Assert.Equal(number, parsed.Number);
    }

    [Fact]
    public void A_DB_carrying_INSTANCEOF_is_an_INSTANCE_DB_and_names_its_FB()
    {
        var parsed = CorpusScan.ParseHeader("DB iDB_X\n  NUMBER 9002\n  INSTANCEOF FB_X\n", "x.ir");

        Assert.Equal(IrObjectKind.InstanceDb, parsed!.Kind);
        Assert.Equal("FB_X", parsed.InstanceOf);
    }

    [Fact]
    public void A_file_that_declares_no_object_is_UNREADABLE_and_not_simply_absent()
    {
        // A parse that could not run is the same absence one level in — and drift-check exited 0 over a
        // whole corpus of unparseable .ir until 2026-08-14 for that conflation.
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Real", 9001);
        File.WriteAllText(Path.Combine(corpus.Ir, "junk.ir"), "this is not an IR header\n");

        var scan = CorpusScan.Read(corpus.Ir);

        Assert.Equal(2, scan.FilesSeen);
        Assert.Single(scan.Objects);
        Assert.Single(scan.Unreadable);
    }

    [Fact]
    public void The_reserved_range_is_INCLUSIVE_at_both_ends_and_excludes_its_neighbours()
    {
        Assert.False(New(8999).IsHarnessOwned);
        Assert.True(New(9000).IsHarnessOwned);
        Assert.True(New(9999).IsHarnessOwned);
        Assert.False(New(10000).IsHarnessOwned);

        static IrObject New(int n) => new("X", IrObjectKind.Block, "FB", n, null, "x.ir");
    }

    [Fact]
    public void A_TYPE_and_a_TAGTABLE_are_UNADDRESSABLE_by_the_number_rule_in_BOTH_directions()
    {
        // The finding the first real run produced, pinned. They are not harness-owned and they are not
        // provably NOT harness-owned: X-J's rule cannot reach them, because they have no number space.
        var type = new IrObject("UDT_X", IrObjectKind.Type, null, null, null, "x.ir");
        var table = new IrObject("HarnessMirror", IrObjectKind.TagTable, null, null, null, "x.ir");

        Assert.False(type.IsHarnessOwned);
        Assert.True(type.IsUnaddressableByNumber);
        Assert.False(table.IsHarnessOwned);
        Assert.True(table.IsUnaddressableByNumber);

        // ...and a numbered object is NOT unaddressable, or the flag would be true of everything.
        Assert.False(new IrObject("FB_X", IrObjectKind.Block, "FB", 9001, null, "x.ir").IsUnaddressableByNumber);
    }
}
