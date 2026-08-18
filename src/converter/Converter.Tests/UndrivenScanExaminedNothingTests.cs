using Converter.Ir;
using Converter.SimaticMl;
using Converter.UndrivenScan;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 FI-44, THE THIRD AND FOURTH SHAPES — 2026-08-18.
///
/// <para>The first two ways <c>undriven-scan</c> could examine nothing and exit 0 were closed by
/// name: an unknown <c>--fb</c>, and an FB nothing instantiates. This is the way that was not
/// thought of, and it was found on a live corpus where <b>TWO OF THE THREE LARGEST BLOCKS</b>
/// reported <c>0 member/instance pair(s), 0 undriven</c> and <c>exit 0</c>: the block EXISTS, it HAS
/// an instance DB, and every one of its interface members is one the FB itself writes — so the
/// caller-driven scope is empty and the scan resolves nothing.</para>
///
/// <para>That is a perfectly ordinary state for a block that only publishes. It is not a pass, and it
/// is not a finding either: it is a statement that the question does not apply. The lesson recorded
/// in <c>UndrivenScanReport.ExaminedNothing</c> is that the ROW COUNT is the fact and the scope enum
/// is only a catalogue of causes — so the gate keys on both, and a fifth shape cannot slip through by
/// not having been enumerated.</para>
/// </summary>
public class UndrivenScanExaminedNothingTests : IDisposable
{
    private readonly string _dir;

    public UndrivenScanExaminedNothingTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"undriven-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    private static DbMember Struct(string name, params string[] leaves) =>
        new(name, "Struct", false, null,
            NestedMembers: leaves.Select(l => new DbMember(l, "Bool", Retain: false, StartValue: null)).ToArray());

    private void WriteBlock(string name, IrNetwork[] networks, DbMember[] statics) =>
        File.WriteAllText(Path.Combine(_dir, name + ".ir"), IrSerializer.SerializeBlockReadable(
            new IrBlock("0", name.StartsWith("FC", StringComparison.Ordinal) ? "FC" : "FB",
                name, 1, "LAD", null, networks, StaticMembers: statics)));

    private void WriteInstanceDb(string name, string fb, DbMember[] members) =>
        File.WriteAllText(Path.Combine(_dir, name + ".ir"), DbIrSerializer.Serialize(
            new DbSource("0", name, 2, InstanceOfName: fb, Comment: null, Members: members)));

    /// <summary>A block that only PUBLISHES: it writes every member of its own interface.</summary>
    private void BuildPublishOnlyBlock()
    {
        var iface = new[] { Struct("IO", "StateCode", "Healthy") };

        WriteBlock("FB_Reporter", new[]
        {
            new IrNetwork(1, "publish", new[]
            {
                new CoilAssignment("IO.StateCode", new Expr.TagRef("DB_Plant.Mode")),
                new CoilAssignment("IO.Healthy", new Expr.TagRef("DB_Plant.Alive")),
            }),
        }, iface);

        WriteInstanceDb("iDB_Reporter_One", "FB_Reporter", iface);
    }

    [Fact]
    public void BlockWithInstancesButNoCallerDrivenMember_ExaminedNothing()
    {
        BuildPublishOnlyBlock();

        var report = UndrivenScanRunner.Run(_dir, "FB_Reporter", Array.Empty<string>(), Array.Empty<string>());

        Assert.Empty(report.Members);
        Assert.False(report.HasFindings);          // no defect was found...
        Assert.True(report.ExaminedNothing);       // ...because nothing was looked at
        Assert.Equal(ScanScope.NoMembersInScope, report.Scope);

        var text = UndrivenScanOutputFormatter.FormatText(report);
        Assert.Contains("NOTHING EXAMINED - this is not a pass", text);
    }

    [Fact]
    public void InstanceFilterMatchingNothing_ExaminedNothing()
    {
        BuildDrivenBlock();

        var report = UndrivenScanRunner.Run(
            _dir, "FB_Gate", new[] { "iDB_TypoNotHere" }, Array.Empty<string>());

        Assert.Empty(report.Members);
        Assert.True(report.ExaminedNothing);
        Assert.Equal(ScanScope.NoInstancesMatchedFilter, report.Scope);
    }

    /// <summary>A block whose interface a caller really does drive — the ordinary scannable case.</summary>
    private void BuildDrivenBlock()
    {
        var iface = new[] { Struct("IO", "Cmd", "Out") };

        WriteBlock("FB_Gate", new[]
        {
            new IrNetwork(1, "act", new[] { new CoilAssignment("IO.Out", new Expr.TagRef("IO.Cmd")) }),
        }, iface);

        WriteInstanceDb("iDB_Gate_One", "FB_Gate", iface);
    }

    /// <summary>
    /// THE POSITIVE CONTROL, and it is not optional: a gate that refuses everything passes every test
    /// that only checks for refusals. A real scan over a real instance still reports rows and still
    /// exits on its findings, not on its scope.
    /// </summary>
    [Fact]
    public void ARealScanIsStillARealScan()
    {
        BuildDrivenBlock();

        var report = UndrivenScanRunner.Run(_dir, "FB_Gate", Array.Empty<string>(), Array.Empty<string>());

        Assert.NotEmpty(report.Members);
        Assert.False(report.ExaminedNothing);
        Assert.Equal(ScanScope.Scanned, report.Scope);
        Assert.True(report.HasFindings); // IO.Cmd is read by the FB and driven by nobody
    }

    [Fact]
    public void UnknownBlockAndNoInstancesStillReportTheirOwnReasons()
    {
        BuildDrivenBlock();

        var unknown = UndrivenScanRunner.Run(_dir, "FB_NotHere", Array.Empty<string>(), Array.Empty<string>());
        Assert.True(unknown.ExaminedNothing);
        Assert.Equal(ScanScope.UnknownBlock, unknown.Scope);

        // A block with no instance DB and no multi-instance placement anywhere.
        WriteBlock("FB_Orphan", new[]
        {
            new IrNetwork(1, "act", new[] { new CoilAssignment("IO.Out", new Expr.TagRef("IO.Cmd")) }),
        }, new[] { Struct("IO", "Cmd", "Out") });

        var orphan = UndrivenScanRunner.Run(_dir, "FB_Orphan", Array.Empty<string>(), Array.Empty<string>());
        Assert.True(orphan.ExaminedNothing);
    }

    /// <summary>
    /// THE ROW-COUNT CLAUSE ON ITS OWN. Every shape above also sets a scope value, so a gate reading
    /// only the enum would pass all of them — and that is exactly how the third shape survived two
    /// rounds of "empty is not clean" work. This asserts the other half directly: a report claiming
    /// Scope.Scanned over zero rows is still nothing examined, whatever produced it.
    /// </summary>
    [Fact]
    public void ZeroRowsUnderAScannedScopeIsStillNothingExamined()
    {
        var report = new UndrivenScanReport(
            "anywhere", 0, "FB_X", Array.Empty<string>(),
            Array.Empty<MemberDrive>(), Array.Empty<string>(), ScanScope.Scanned);

        Assert.True(report.ExaminedNothing);
    }

    /// <summary>The JSON carried neither the scope nor the flag, so a consumer could not tell either.</summary>
    [Fact]
    public void JsonCarriesScopeAndExaminedNothing()
    {
        BuildPublishOnlyBlock();

        var json = UndrivenScanOutputFormatter.FormatJson(
            UndrivenScanRunner.Run(_dir, "FB_Reporter", Array.Empty<string>(), Array.Empty<string>()));

        Assert.Contains("\"scope\": \"NoMembersInScope\"", json);
        Assert.Contains("\"examinedNothing\": true", json);
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
