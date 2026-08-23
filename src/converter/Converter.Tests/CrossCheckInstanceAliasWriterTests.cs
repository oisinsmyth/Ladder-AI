using Converter.CrossCheck;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 2026-08-21. <b>`cross-check` PRINTED A VERDICT ITS OWN DATA CONTRADICTED.</b>
///
/// <para>A block-local row carried the annotation <i>"block-local to FB_X — not a cross-block
/// conflict; also addressable as iDB_X.member"</i> — <b>on the same line, and the alias it named was
/// being written by another block.</b> In <c>ir/test-project001</c> four rows read that way, every
/// one of them with <c>OB100</c> writing the alias. The sole-writer table had the same blindness on
/// EIGHT rows, which is the more dangerous half: it answers <i>"what loses its only writer if I
/// delete this?"</i>, and a member also written from an OB through its instance DB is not
/// sole-written at all.</para>
///
/// <para><b>The key spaces are still NOT pooled, and that is deliberate.</b>
/// <c>ProjectUsageGraph.QualifiedPath</c> explains why: an FB with two instance DBs has one internal
/// write landing in both, so merging would invent a conflict — the exact bug the 2026-08-14 grouping
/// was written to kill. What changed is that the outside writers are now CARRIED on the group, so the
/// report states a fact instead of asserting a verdict.</para>
///
/// <para>*** BOTH DIRECTIONS, like its sibling file. *** A fix that drops the annotation everywhere
/// would pass a one-sided test and destroy the thing the annotation exists for, so every assertion
/// that the label is GONE is paired with one that it SURVIVES where it is true.</para>
/// </summary>
public class CrossCheckInstanceAliasWriterTests : IDisposable
{
    private readonly string _dir;

    public CrossCheckInstanceAliasWriterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"crosscheck-aliaswriter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        // FB_Seq: writes IO.Step TWICE internally (a block-local multi-writer) and IO.Latch ONCE
        // internally (a block-local SOLE writer). Both rows are about to be contradicted from outside.
        WriteBlock("FB_Seq.ir", Fb("FB_Seq", 1, "\"UDT_SeqIO\""));

        // FB_Quiet: the CONTROL. Identical shape, and nothing outside ever touches its instance DB, so
        // its rows must keep saying "not a cross-block conflict".
        WriteBlock("FB_Quiet.ir", Fb("FB_Quiet", 2, "\"UDT_QuietIO\""));

        WriteDb("iDB_Seq.ir", Idb("iDB_Seq", 10, "FB_Seq", "\"UDT_SeqIO\""));
        WriteDb("iDB_Quiet.ir", Idb("iDB_Quiet", 11, "FB_Quiet", "\"UDT_QuietIO\""));

        // OB_Init: the startup-reset shape from the real corpus — it clears the sequencer's state
        // through the INSTANCE DB, spelling the very same storage a different way.
        WriteBlock("OB_Init.ir", new IrBlock("0", "OB", "OB_Init", 100, "LAD", null, new[]
        {
            new IrNetwork(1, "Startup reset", new[]
            {
                new CoilAssignment("iDB_Seq.IO.Step", new Expr.TagRef("AlwaysTrue")),
                new CoilAssignment("iDB_Seq.IO.Latch", new Expr.TagRef("AlwaysTrue")),
            }),
        }));
    }

    // ---- THE DEFECT ------------------------------------------------------------------------------

    [Fact]
    public void BlockLocalMultiWriter_WrittenFromOutsideThroughItsInstanceDb_CarriesTheOutsideWriters()
    {
        var report = CrossCheckRunner.Run(_dir);
        var row = Row(report, "FB_Seq.IO.Step");

        Assert.Equal("FB_Seq", row.Owner);
        Assert.Contains("iDB_Seq.IO.Step", row.InstanceAliases);
        Assert.NotEmpty(row.AliasWriters);
        Assert.Contains("OB_Init", row.AliasWriters.Select(w => w.Block));
    }

    [Fact]
    public void BlockLocalMultiWriter_WrittenFromOutside_NoLongerClaimsItIsNotACrossBlockConflict()
    {
        var text = CrossCheckOutputFormatter.FormatText(CrossCheckRunner.Run(_dir));
        var line = LineFor(text, "FB_Seq.IO.Step");

        Assert.DoesNotContain("not a cross-block conflict", line);
        Assert.Contains("ALSO WRITTEN FROM OUTSIDE FB_Seq", line);
        Assert.Contains("OB_Init N1", line);
    }

    // 🔴 THE MORE DANGEROUS HALF. One internal writer plus an outside alias writer gives
    // Writers.Count == 1, so the row appears ONLY in the sole-writer table and never in the
    // multi-writer one. Without the alias writers a back-out is told the safe thing about a member
    // that is not safe.
    [Fact]
    public void SoleWriter_WrittenFromOutsideThroughItsInstanceDb_IsNotActuallySoleWritten()
    {
        var report = CrossCheckRunner.Run(_dir);
        var row = report.SoleWriters.Single(s => s.Path == "FB_Seq.IO.Latch");

        Assert.Equal("FB_Seq", row.Writer.Block);
        Assert.NotEmpty(row.AliasWriters);
        Assert.Contains("OB_Init", row.AliasWriters.Select(w => w.Block));
    }

    // ---- THE OTHER DIRECTION: the annotation must SURVIVE where it is true ------------------------

    [Fact]
    public void BlockLocalMultiWriter_WithNoOutsideWriter_KeepsTheAnnotation()
    {
        var report = CrossCheckRunner.Run(_dir);
        var row = Row(report, "FB_Quiet.IO.Step");

        Assert.Equal("FB_Quiet", row.Owner);
        Assert.Empty(row.AliasWriters);

        var line = LineFor(CrossCheckOutputFormatter.FormatText(report), "FB_Quiet.IO.Step");
        Assert.Contains("not a cross-block conflict", line);
        Assert.DoesNotContain("ALSO WRITTEN FROM OUTSIDE", line);
    }

    [Fact]
    public void SoleWriter_WithNoOutsideWriter_CarriesNoAliasWriters()
    {
        var report = CrossCheckRunner.Run(_dir);
        var row = report.SoleWriters.Single(s => s.Path == "FB_Quiet.IO.Latch");

        Assert.Empty(row.AliasWriters);
    }

    // The FB's own internal writes must never count as "outside", or every ordinary FB becomes a
    // self-conflict and the signal is worthless.
    [Fact]
    public void TheOwnersOwnWrites_AreNeverCountedAsOutsideWriters()
    {
        var report = CrossCheckRunner.Run(_dir);
        var row = Row(report, "FB_Seq.IO.Step");

        Assert.DoesNotContain("FB_Seq", row.AliasWriters.Select(w => w.Block));
    }

    // *** THE KEY SPACES ARE STILL SEPARATE. *** This is the property the 2026-08-14 grouping bought
    // and this change must not spend: the internal path and the iDB path are NOT merged into one
    // group, they are cross-referenced. If they were merged, the FB's own 2 writes plus OB_Init's
    // would pool and FB_Seq would appear among the writers of the iDB-keyed row.
    [Fact]
    public void TheInternalAndInstanceDbPaths_AreStillSeparateGroups_NotPooled()
    {
        var report = CrossCheckRunner.Run(_dir);
        var internalRow = Row(report, "FB_Seq.IO.Step");

        // Every writer listed on the block-local row is the owner itself — the outside writer lives in
        // AliasWriters, deliberately not in Writers.
        Assert.All(internalRow.Writers, w => Assert.Equal("FB_Seq", w.Block));
    }

    // ---- fixture ---------------------------------------------------------------------------------

    private static MultiWriterFact Row(CrossCheckReport report, string path) =>
        report.MultiWriters.Single(m => m.Path == path);

    private static string LineFor(string text, string path) =>
        text.Split('\n').Single(l => l.TrimStart().StartsWith(path + ":", StringComparison.Ordinal));

    private static IrBlock Fb(string name, int number, string ioType) =>
        new("0", "FB", name, number, "LAD", null, new[]
        {
            new IrNetwork(1, "Step transitions", new[]
            {
                new CoilAssignment("IO.Step", new Expr.TagRef("Enable")),
                new CoilAssignment("IO.Step", new Expr.TagRef("Enable")),
            }),
            new IrNetwork(2, "Latch", new[]
            {
                new CoilAssignment("IO.Latch", new Expr.TagRef("Enable")),
            }),
        },
            StaticMembers: new[]
            {
                new DbMember("IO", ioType, Retain: true, StartValue: null, SetPoint: true, NestedMembers: new[]
                {
                    new DbMember("Step", "Int", Retain: false, StartValue: null),
                    new DbMember("Latch", "Bool", Retain: false, StartValue: null),
                }),
            });

    private static DbSource Idb(string name, int number, string fb, string ioType) =>
        new("0", name, number, InstanceOfName: fb, Comment: null, Members: new[]
        {
            new DbMember("IO", ioType, Retain: true, StartValue: null, SetPoint: true, NestedMembers: new[]
            {
                new DbMember("Step", "Int", Retain: false, StartValue: null),
                new DbMember("Latch", "Bool", Retain: false, StartValue: null),
            }),
        });

    private void WriteDb(string file, DbSource db) =>
        File.WriteAllText(Path.Combine(_dir, file), DbIrSerializer.Serialize(db));

    private void WriteBlock(string file, IrBlock block)
    {
        var sidecars = block.Networks
            .Select(n => new NetworkSidecar(n.Number, n.Number.ToString(), Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();
        File.WriteAllText(Path.Combine(_dir, file), IrSerializer.SerializeBlock(block, sidecars));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
