using System.Text.Json;
using Converter.CrossCheck;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-22 (2026-07-20): `converter cross-check` — whole-project reader/writer-graph FACTS (never
/// verdicts). Builds a small on-disk corpus exercising each fact table: dead global-DB members (both
/// directions + fully unused), a multi-writer path, a physical-IO reference, and a sibling reference.
/// </summary>
public class CrossCheckTests : IDisposable
{
    private readonly string _dir;

    public CrossCheckTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"crosscheck-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        // Global buffer DB: Dead (written, never read), Orphan (read, never written), Live (both),
        // Unused (neither).
        WriteDb("DB_Buf.ir", new DbSource("0", "DB_Buf", 1, InstanceOfName: null, Comment: null, Members: new[]
        {
            new DbMember("Dead", "Bool", Retain: false, StartValue: null),
            new DbMember("Orphan", "Bool", Retain: false, StartValue: null),
            new DbMember("Live", "Bool", Retain: false, StartValue: null),
            new DbMember("Unused", "Bool", Retain: false, StartValue: null),
        }));

        // FB_A: writes DB_Buf.Dead (from a physical input), reads DB_Buf.Orphan into DB_Buf.Live, then
        // writes DB_Ctrl.Shared twice (coil + a CALL output => a multi-writer), CALLing FB_B via iDB_X.
        WriteBlock("FB_A.ir", new IrBlock("0", "FB", "FB_A", 1, "LAD", null, new[]
        {
            new IrNetwork(1, "Map + gate", new[]
            {
                new CoilAssignment("DB_Buf.Dead", new Expr.TagRef("DI3_SYS_Start")),
                new CoilAssignment("DB_Buf.Live", new Expr.TagRef("DB_Buf.Orphan")),
            }),
            new IrNetwork(2, "Shared write 1", new[]
            {
                new CoilAssignment("DB_Ctrl.Shared", new Expr.TagRef("DB_Buf.Live")),
            }),
            new IrNetwork(3, "Call sibling", Array.Empty<CoilAssignment>(),
                Calls: new[]
                {
                    new CallStatement("FB_B", "iDB_X", new Expr.TagRef("Enable"), new CallArgument[]
                    {
                        new CallArgument.OutputArg("Out", "DB_Ctrl.Shared"),
                    }),
                }),
        }));

        WriteBlock("FB_B.ir", new IrBlock("0", "FB", "FB_B", 2, "LAD", null, new[]
        {
            new IrNetwork(1, "Trivial", new[] { new CoilAssignment("DB_Buf.Live", new Expr.TagRef("Enable")) }),
        }));
    }

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

    [Fact]
    public void DeadMembers_BothDirectionsAndFullyUnused()
    {
        var report = CrossCheckRunner.Run(_dir);
        var byPath = report.DeadMembers.ToDictionary(d => d.Path);

        Assert.True(byPath.ContainsKey("DB_Buf.Dead"));   // written, never read
        Assert.Empty(byPath["DB_Buf.Dead"].Readers);
        Assert.NotEmpty(byPath["DB_Buf.Dead"].Writers);

        Assert.True(byPath.ContainsKey("DB_Buf.Orphan")); // read, never written
        Assert.Empty(byPath["DB_Buf.Orphan"].Writers);
        Assert.NotEmpty(byPath["DB_Buf.Orphan"].Readers);

        Assert.True(byPath.ContainsKey("DB_Buf.Unused"));  // neither

        Assert.False(byPath.ContainsKey("DB_Buf.Live"));   // both — not dead
    }

    [Fact]
    public void MultiWriter_SharedPathListsAllWriters()
    {
        var report = CrossCheckRunner.Run(_dir);
        var shared = Assert.Single(report.MultiWriters, m => m.Path == "DB_Ctrl.Shared");
        Assert.True(shared.Writers.Count >= 2); // the coil in N2 and the CALL output in N3
    }

    [Fact]
    public void IoBoundary_SurfacesPhysicalIoReference()
    {
        var report = CrossCheckRunner.Run(_dir);
        Assert.Contains(report.IoBoundary, io => io.Block == "FB_A" && io.Path == "DI3_SYS_Start" && io.Direction == "read");
    }

    [Fact]
    public void SiblingRefs_SurfaceCallAndInstanceDbRoot()
    {
        var report = CrossCheckRunner.Run(_dir);
        var fbA = Assert.Single(report.SiblingRefs, s => s.Block == "FB_A");
        Assert.Contains("FB_B", fbA.Calls);
        Assert.Contains("iDB_X", fbA.InstanceDbRoots);
    }

    [Fact]
    public void FormatText_And_Json_Render()
    {
        var report = CrossCheckRunner.Run(_dir);

        var text = CrossCheckOutputFormatter.FormatText(report);
        Assert.Contains("DEAD GLOBAL-DB MEMBERS", text);
        Assert.Contains("DB_Buf.Dead", text);
        Assert.Contains("SUMMARY:", text);

        var json = CrossCheckOutputFormatter.FormatJson(report);
        using var doc = JsonDocument.Parse(json); // asserts valid JSON
        Assert.True(doc.RootElement.TryGetProperty("deadMembers", out _));
    }
}
