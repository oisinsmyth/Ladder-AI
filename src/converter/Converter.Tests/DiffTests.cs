using System.Text.Json;
using Converter.Diff;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `converter diff` (S7 entry requirement): before/after IR of one block → which networks changed,
/// with the rest provably identical in IR. Same test shape as DigestTests: build the model, serialize
/// with the real serializer (empty sidecars — parse doesn't cross-check sidecar counts; that's a
/// to-xml/FlgNetBuilder concern), diff from real temp files (DiffRunner reads by path).
///
/// The load-bearing case is Diff_VolatileSidecarOnly_IsIdentical: a re-export that only churns UIds
/// must read as identical, which falls out of comparing the sidecar-free readable form.
/// </summary>
public class DiffTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string WriteIr(IrBlock block, string compileUnitBase = "100")
    {
        var baseId = int.Parse(compileUnitBase);
        var sidecars = block.Networks
            .Select((n, idx) => new NetworkSidecar(
                n.Number, (baseId + idx).ToString(),
                Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();

        var path = Path.Combine(Path.GetTempPath(), $"diff-test-{Guid.NewGuid():N}.ir");
        File.WriteAllText(path, IrSerializer.SerializeBlock(block, sidecars));
        _tempFiles.Add(path);
        return path;
    }

    // A sidecar-less .ir (readable form only, no SIDECAR section) — what a freshly-authored or
    // validation-corpus block looks like; diff must handle it (2026-07-18 gen-block-modify-fix finding).
    private string WriteIrSidecarless(IrBlock block)
    {
        var sidecars = block.Networks
            .Select(n => new NetworkSidecar(n.Number, n.Number.ToString(), Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();
        var sidecarless = IrSerializer.SerializeBlock(block, sidecars).Split("\nSIDECAR\n", 2)[0] + "\n";
        var path = Path.Combine(Path.GetTempPath(), $"diff-test-{Guid.NewGuid():N}.ir");
        File.WriteAllText(path, sidecarless);
        _tempFiles.Add(path);
        return path;
    }

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
            }
        }
    }

    private static IrNetwork Coil(int number, string title, string coilTag, string condition) =>
        new(number, title, new[] { new CoilAssignment(coilTag, new Expr.TagRef(condition)) });

    private static IrBlock Block(string name, IReadOnlyList<IrNetwork> networks,
        string? title = null, IReadOnlyList<DbMember>? statics = null) =>
        new("0", "FB", name, 1, "LAD", null, networks, StaticMembers: statics, Title: title);

    [Fact]
    public void Diff_SidecarlessInputs_Work()
    {
        var block = Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB") });
        var report = DiffRunner.Run(WriteIrSidecarless(block), WriteIrSidecarless(block), Array.Empty<int>());

        Assert.False(report.HasAnyChange);
        Assert.Equal(2, report.IdenticalCount);
    }

    [Fact]
    public void Diff_SidecarlessVsSidecarful_ComparesReadableForm()
    {
        // A sidecar-less as-built vs a sidecar-carrying re-export of the same logic reads as identical.
        var block = Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA") });
        var report = DiffRunner.Run(WriteIrSidecarless(block), WriteIr(block), Array.Empty<int>());

        Assert.False(report.HasAnyChange);
    }

    [Fact]
    public void Diff_SameBlockTwice_AllIdentical()
    {
        var block = Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB") });
        var report = DiffRunner.Run(WriteIr(block), WriteIr(block), Array.Empty<int>());

        Assert.False(report.HasAnyChange);
        Assert.False(report.BlockNameMismatch);
        Assert.Equal(2, report.IdenticalCount);
        Assert.All(report.Networks, n => Assert.Equal(NetworkChangeKind.Identical, n.Kind));
    }

    // The reason no separate Normalizer is needed: identical readable body, different sidecar UIds
    // (the shape a TIA re-export produces) reads as fully identical.
    [Fact]
    public void Diff_VolatileSidecarOnly_IsIdentical()
    {
        var block = Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB") });
        var report = DiffRunner.Run(
            WriteIr(block, compileUnitBase: "100"),
            WriteIr(block, compileUnitBase: "900"),
            Array.Empty<int>());

        Assert.False(report.HasAnyChange);
        Assert.Equal(2, report.IdenticalCount);
    }

    [Fact]
    public void Diff_OneRungChanged_OnlyThatNetworkChanged()
    {
        var oldBlock = Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB") });
        var newBlock = Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA_CHANGED"), Coil(2, "N2", "OutB", "InB") });

        var report = DiffRunner.Run(WriteIr(oldBlock), WriteIr(newBlock), Array.Empty<int>());

        Assert.True(report.HasAnyChange);
        Assert.Equal(1, report.ChangedCount);
        var n1 = report.Networks.Single(n => n.Number == 1);
        Assert.Equal(NetworkChangeKind.Changed, n1.Kind);
        Assert.Contains("InA", n1.BeforeText);
        Assert.Contains("InA_CHANGED", n1.AfterText);
        Assert.Equal(NetworkChangeKind.Identical, report.Networks.Single(n => n.Number == 2).Kind);
    }

    [Fact]
    public void Diff_AddedAndRemovedNetwork()
    {
        var oneNet = Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA") });
        var twoNets = Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB") });

        var added = DiffRunner.Run(WriteIr(oneNet), WriteIr(twoNets), Array.Empty<int>());
        Assert.Equal(1, added.AddedCount);
        Assert.Equal(NetworkChangeKind.Added, added.Networks.Single(n => n.Number == 2).Kind);

        var removed = DiffRunner.Run(WriteIr(twoNets), WriteIr(oneNet), Array.Empty<int>());
        Assert.Equal(1, removed.RemovedCount);
        Assert.Equal(NetworkChangeKind.Removed, removed.Networks.Single(n => n.Number == 2).Kind);
    }

    [Fact]
    public void Diff_OnlyAssertion_ConfinedChangePasses_OutOfScopeChangeViolates()
    {
        var oldBlock = Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB") });
        var newBlock = Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA_CHANGED"), Coil(2, "N2", "OutB", "InB") });

        // Change confined to the declared network -> no violation.
        var confined = DiffRunner.Run(WriteIr(oldBlock), WriteIr(newBlock), new[] { 1 });
        Assert.False(confined.HasInvarianceViolation);

        // Same change, but the declared set doesn't include the network that actually changed.
        var violation = DiffRunner.Run(WriteIr(oldBlock), WriteIr(newBlock), new[] { 2 });
        Assert.True(violation.HasInvarianceViolation);
        Assert.Equal(1, Assert.Single(violation.InvarianceViolations).Number);
    }

    [Fact]
    public void Diff_HeaderTitleAndInterfaceChanges()
    {
        var nets = new[] { Coil(1, "N1", "OutA", "InA") };
        var baseBlock = Block("FB_X", nets, title: "Original title");
        var titled = Block("FB_X", nets, title: "New title");
        var withMember = Block("FB_X", nets, title: "Original title",
            statics: new[] { new DbMember("NewStatic", "Bool", Retain: false, StartValue: null) });

        var titleDiff = DiffRunner.Run(WriteIr(baseBlock), WriteIr(titled), Array.Empty<int>());
        Assert.True(titleDiff.Header.TitleChanged);
        Assert.Equal("Original title", titleDiff.Header.TitleBefore);
        Assert.Equal("New title", titleDiff.Header.TitleAfter);
        Assert.False(titleDiff.Header.InterfaceChanged);
        Assert.True(titleDiff.HasAnyChange);

        var interfaceDiff = DiffRunner.Run(WriteIr(baseBlock), WriteIr(withMember), Array.Empty<int>());
        Assert.True(interfaceDiff.Header.InterfaceChanged);
        Assert.False(interfaceDiff.Header.TitleChanged);
    }

    [Fact]
    public void Diff_BlockNameMismatch_IsFlaggedNotFatal()
    {
        var oldBlock = Block("FB_OldName", new[] { Coil(1, "N1", "OutA", "InA") });
        var newBlock = Block("FB_NewName", new[] { Coil(1, "N1", "OutA", "InA") });

        var report = DiffRunner.Run(WriteIr(oldBlock), WriteIr(newBlock), Array.Empty<int>());
        Assert.True(report.BlockNameMismatch);
        Assert.Equal("FB_OldName", report.OtherBlockName);
        Assert.Equal("FB_NewName", report.BlockName);
    }

    [Fact]
    public void FormatText_And_Json_Render()
    {
        var oldBlock = Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB") });
        var newBlock = Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA_CHANGED"), Coil(2, "N2", "OutB", "InB") });
        var report = DiffRunner.Run(WriteIr(oldBlock), WriteIr(newBlock), new[] { 2 });

        var text = DiffOutputFormatter.FormatText(report);
        Assert.Contains("SUMMARY: 2 network(s): 1 changed", text);
        Assert.Contains("CHANGED network 1", text);
        Assert.Contains("INVARIANCE VIOLATION", text);

        var json = DiffOutputFormatter.FormatJson(report);
        using var doc = JsonDocument.Parse(json); // asserts valid JSON
        Assert.Equal(1, doc.RootElement.GetProperty("summary").GetProperty("changed").GetInt32());
        Assert.True(doc.RootElement.GetProperty("hasInvarianceViolation").GetBoolean());
    }
}
