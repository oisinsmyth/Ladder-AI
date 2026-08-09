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

        // Interface-UDT aliasing corpus (FI-22 follow-up). FB_Seq owns a Static UDT member `IO` with
        // two leaves: `IO.Foo` (written FB-internally, read externally via the iDB) and `IO.Ghost`
        // (written FB-internally, read NOWHERE). iDB_Seq is its instance DB; FC_Orch is the caller.
        WriteDb("iDB_Seq.ir", new DbSource("0", "iDB_Seq", 20, InstanceOfName: "FB_Seq", Comment: null, Members: new[]
        {
            new DbMember("IO", "\"UDT_SeqIO\"", Retain: true, StartValue: null, SetPoint: true, NestedMembers: new[]
            {
                new DbMember("Foo", "Bool", Retain: false, StartValue: null),
                new DbMember("Ghost", "Bool", Retain: false, StartValue: null),
            }),
        }));

        // FB-internal writes use the BARE suffix; block name == the instantiated FB (FB_Seq).
        WriteBlock("FB_Seq.ir", new IrBlock("0", "FB", "FB_Seq", 3, "LAD", null, new[]
        {
            new IrNetwork(1, "Drive interface members", new[]
            {
                new CoilAssignment("IO.Foo", new Expr.TagRef("Enable")),
                new CoilAssignment("IO.Ghost", new Expr.TagRef("Enable")),
            }),
        }));

        // Orchestrator reads Foo through the iDB-qualified alias; never references Ghost.
        WriteBlock("FC_Orch.ir", new IrBlock("0", "FC", "FC_Orch", 4, "LAD", null, new[]
        {
            new IrNetwork(1, "Consume via iDB", new[]
            {
                new CoilAssignment("Scratch", new Expr.TagRef("iDB_Seq.IO.Foo")),
            }),
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
    public void InterfaceMember_WrittenInternally_ReadViaIdb_IsNotDead()
    {
        var report = CrossCheckRunner.Run(_dir);
        var byPath = report.DeadMembers.ToDictionary(d => d.Path);

        // IO.Foo: written in FB_Seq (bare), read in FC_Orch (iDB-qualified) — aliasing correlates the
        // two, so it is NOT dead despite each alias form looking half-dead on its own.
        Assert.False(byPath.ContainsKey("FB_Seq.IO.Foo"));
    }

    [Fact]
    public void InterfaceMember_WrittenInternally_NeverRead_IsDead()
    {
        var report = CrossCheckRunner.Run(_dir);
        var byPath = report.DeadMembers.ToDictionary(d => d.Path);

        // IO.Ghost: written FB-internally, read by nobody anywhere (no internal read, no iDB read).
        Assert.True(byPath.ContainsKey("FB_Seq.IO.Ghost"));
        var ghost = byPath["FB_Seq.IO.Ghost"];
        Assert.Equal(DeadMemberScope.InterfaceMember, ghost.Scope);
        Assert.Empty(ghost.Readers);                 // written-but-never-consumed
        Assert.NotEmpty(ghost.Writers);
        Assert.Contains(ghost.Writers, w => w.Block == "FB_Seq");
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
        Assert.Contains("DEAD MEMBERS", text);
        Assert.Contains("DB_Buf.Dead [global-db]", text);
        Assert.Contains("SUMMARY:", text);

        var json = CrossCheckOutputFormatter.FormatJson(report);
        using var doc = JsonDocument.Parse(json); // asserts valid JSON
        Assert.True(doc.RootElement.TryGetProperty("deadMembers", out _));
    }

    // FI-53 (2026-08-07). An `Array[0..n] of "UDT"` member is inventoried as ONE leaf, because the
    // walk does not expand a UDT sitting behind an array. Every real reference goes through an
    // element AND a member (`DB_Arr.Slot[0].Value`), so the exact-string lookup this used to do
    // matched nothing and the member reported "unused (no writer, no reader)".
    //
    // Measured on a live project before the fix: the weighing-interface array and the per-silo
    // parameter array both read as dead against 16 and 24 real readers. Acting on that advice would
    // have deleted the plant's entire weighing path — which makes this the more dangerous half of
    // the array-subscript problem. FI-51 fixed expressing a subscript; this is the reference graph
    // failing to credit one.
    [Fact]
    public void ArrayOfUdtMember_CreditsReadsThroughItsElements()
    {
        WriteDb("DB_Arr.ir", new DbSource("0", "DB_Arr", 30, InstanceOfName: null, Comment: null, Members: new[]
        {
            new DbMember("Slot", "Array[0..3] of \"UDT_Slot\"", Retain: false, StartValue: null),
            new DbMember("Plain", "Bool", Retain: false, StartValue: null),
        }));

        WriteBlock("FC_Arr.ir", new IrBlock("0", "FC", "FC_Arr", 40, "LAD", null, new[]
        {
            new IrNetwork(1, "Reads through two different elements", new[]
            {
                new CoilAssignment("DB_Arr.Plain", new Expr.TagRef("DB_Arr.Slot[0].Value")),
                new CoilAssignment("DB_Ctrl.Shared", new Expr.TagRef("DB_Arr.Slot[2].Value")),
            }),
        }));

        var byPath = CrossCheckRunner.Run(_dir).DeadMembers.ToDictionary(m => m.Path, StringComparer.Ordinal);

        // Read through elements, never written -> reported as consumed-but-never-written, NOT as
        // "no reader". The readers must actually be listed, and deduplicated to one per network.
        Assert.True(byPath.ContainsKey("DB_Arr.Slot"));
        Assert.Equal(new[] { "FC_Arr" }, byPath["DB_Arr.Slot"].Readers.Select(r => r.Block).ToArray());
        Assert.Empty(byPath["DB_Arr.Slot"].Writers);

        // The guard that matters: pooling must not make everything look alive. A sibling that
        // genuinely nothing touches is still reported dead.
        Assert.True(byPath.ContainsKey("DB_Arr.Plain"));
        Assert.Empty(byPath["DB_Arr.Plain"].Readers);
    }

    // Prefix matching must respect component boundaries: a member named `Slot` must not absorb
    // usages of a differently-named sibling that merely starts with the same letters.
    [Fact]
    public void PooledMatching_DoesNotAbsorbASimilarlyNamedSibling()
    {
        WriteDb("DB_Pre.ir", new DbSource("0", "DB_Pre", 31, InstanceOfName: null, Comment: null, Members: new[]
        {
            new DbMember("Slot", "Array[0..1] of \"UDT_Slot\"", Retain: false, StartValue: null),
            new DbMember("SlotCount", "Int", Retain: false, StartValue: null),
        }));

        WriteBlock("FC_Pre.ir", new IrBlock("0", "FC", "FC_Pre", 41, "LAD", null, new[]
        {
            new IrNetwork(1, "Touches only the count", new[]
            {
                new CoilAssignment("DB_Ctrl.Shared", new Expr.TagRef("DB_Pre.SlotCount")),
            }),
        }));

        var byPath = CrossCheckRunner.Run(_dir).DeadMembers.ToDictionary(m => m.Path, StringComparer.Ordinal);

        // `SlotCount` is read; `Slot` is not, and must not inherit `SlotCount`'s reader.
        Assert.True(byPath.ContainsKey("DB_Pre.Slot"));
        Assert.Empty(byPath["DB_Pre.Slot"].Readers);
    }

    // FI-67. `multiWriters` lists only paths written by MORE THAN ONE site, so the sole-writer set —
    // its exact complement — was never emitted, and the writer graph could not be asked the one
    // question a back-out must ask: which members lose their ONLY writer if this is deleted?
    //
    // That is not academic. Deleting the sole writer of a RETENTIVE member leaves it frozen with
    // nothing able to clear it; on one live job that included a resource reservation whose surviving
    // reader gates every grant, so removing the writer with the bit standing would have made a shared
    // machine ungrantable permanently. Three hand-written passes over that deletion each added one
    // more item and still missed a whole class of four — which is why this is derived, not listed.
    [Fact]
    public void SoleWriters_AreExactlyTheComplementOfMultiWriters()
    {
        var report = CrossCheckRunner.Run(_dir);

        var sole = report.SoleWriters.Select(s => s.Path).ToHashSet(StringComparer.Ordinal);
        var multi = report.MultiWriters.Select(m => m.Path).ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(report.SoleWriters);
        Assert.Empty(sole.Intersect(multi));                                   // disjoint
        Assert.All(report.MultiWriters, m => Assert.True(m.Writers.Count >= 2));
    }

    // Readers travel with the fact because they are what separates a hazard from dead data: a member
    // whose readers all disappear with the same feature is inert, one with a surviving reader is
    // live. Both answers come off the one graph, so answering only the first would leave the caller
    // to re-derive the second by hand — which is how that class of four was missed.
    [Fact]
    public void SoleWriters_CarryTheirReaders_AndAnEmptyListIsARealAnswer()
    {
        var report = CrossCheckRunner.Run(_dir);

        Assert.All(report.SoleWriters, s => Assert.NotNull(s.Readers));

        // DB_Buf.Dead is written once and read nowhere — written-once-read-never is a useful answer
        // (dead data), not a missing one.
        var dead = report.SoleWriters.SingleOrDefault(s => s.Path == "DB_Buf.Dead");
        Assert.NotNull(dead);
        Assert.Empty(dead!.Readers);
    }

    // Output contract: JSON only. Most members have exactly one writer, so this is the largest table
    // in the report — printing it in the human view would drown the four tables a reader scans. It
    // exists to be queried, and a query wants JSON.
    [Fact]
    public void SoleWriters_AreInJsonOnly_NotInTheHumanTable()
    {
        var report = CrossCheckRunner.Run(_dir);

        var root = System.Text.Json.JsonDocument.Parse(CrossCheckOutputFormatter.FormatJson(report)).RootElement;
        Assert.True(root.TryGetProperty("soleWriters", out var arr));
        Assert.Equal(report.SoleWriters.Count, arr.GetArrayLength());
        Assert.True(arr[0].TryGetProperty("writer", out var w));
        Assert.True(w.TryGetProperty("block", out _));
        Assert.True(arr[0].TryGetProperty("readers", out _));

        Assert.DoesNotContain("soleWriters", CrossCheckOutputFormatter.FormatText(report));
    }
}
