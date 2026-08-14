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
        string? title = null, IReadOnlyList<DbMember>? statics = null, string? comment = null) =>
        new("0", "FB", name, 1, "LAD", comment, networks, StaticMembers: statics, Title: title);

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

    // --- the invariance gate and the header (tooling-hammer campaign, 2026-08-14) ----------------
    //
    // MEASURED BEFORE: retyping an interface member Bool -> Int, with NO network touched, printed
    //     HEADER changed: interface
    //     INVARIANCE OK: all changes confined to --only {1}
    // two lines apart, the second contradicting the first, and exited 0. This is the S7 modification
    // gate ("the diff must show every changed network and prove the rest identical"), and a retyped
    // member is precisely the change that compiles, imports and misbehaves on the controller. Ruled a
    // violation; --allow-header makes the intent expressible rather than leaving the default a false
    // assurance.

    private static IReadOnlyList<DbMember> Statics(params (string Name, string Type)[] members) =>
        members.Select(m => new DbMember(m.Name, m.Type, Retain: false, StartValue: null)).ToArray();

    [Fact]
    public void OnlyGate_InterfaceMemberRetyped_WithNoNetworkTouched_Violates()
    {
        var nets = new[] { Coil(1, "N1", "OutA", "InA") };
        var before = WriteIr(Block("FB_X", nets, statics: Statics(("A", "Bool"))));
        var after = WriteIr(Block("FB_X", nets, statics: Statics(("A", "Int"))));

        var report = DiffRunner.Run(before, after, new[] { 1 });

        Assert.Empty(report.InvarianceViolations);      // no NETWORK moved - that was the whole trap
        Assert.True(report.Header.InterfaceChanged);
        Assert.True(report.HasUnclaimedHeaderChange);
        Assert.True(report.HasInvarianceViolation);
        Assert.Contains("HEADER changed", DiffOutputFormatter.FormatText(report));
        Assert.DoesNotContain("INVARIANCE OK", DiffOutputFormatter.FormatText(report));
    }

    [Fact]
    public void OnlyGate_InterfaceMemberAdded_Violates()
    {
        var nets = new[] { Coil(1, "N1", "OutA", "InA") };
        var before = WriteIr(Block("FB_X", nets, statics: Statics(("A", "Bool"))));
        var after = WriteIr(Block("FB_X", nets, statics: Statics(("A", "Bool"), ("B", "Bool"))));

        Assert.True(DiffRunner.Run(before, after, new[] { 1 }).HasInvarianceViolation);
    }

    [Fact]
    public void OnlyGate_BlockRenamed_Violates_NotMerelyWarns()
    {
        var nets = new[] { Coil(1, "N1", "OutA", "InA") };
        var report = DiffRunner.Run(WriteIr(Block("FB_X", nets)), WriteIr(Block("FB_Y", nets)), new[] { 1 });

        Assert.True(report.BlockNameMismatch);
        Assert.True(report.HasInvarianceViolation);
        Assert.Contains("renamed", DiffOutputFormatter.FormatText(report));
    }

    // The named escape. gen-block-modify-purpose changes interfaces on purpose and says so.
    [Fact]
    public void OnlyGate_HeaderChangeDeclared_IsAllowed()
    {
        var nets = new[] { Coil(1, "N1", "OutA", "InA") };
        var before = WriteIr(Block("FB_X", nets, statics: Statics(("A", "Bool"))));
        var after = WriteIr(Block("FB_X", nets, statics: Statics(("A", "Int"))));

        var report = DiffRunner.Run(before, after, new[] { 1 }, allowHeaderChange: true);

        Assert.True(report.Header.InterfaceChanged);
        Assert.False(report.HasUnclaimedHeaderChange);
        Assert.False(report.HasInvarianceViolation);
        Assert.Contains("declared via --allow-header", DiffOutputFormatter.FormatText(report));
    }

    // The unaffected case, tested as deliberately as the refused one: an ORDINARY scoped fix - one
    // network changed, header untouched - must stay a clean pass, or the gate is noise and gets
    // switched off. And the verdict now says WHAT IT EXAMINED rather than only that it passed.
    [Fact]
    public void OnlyGate_OrdinaryScopedFix_IsUntouched()
    {
        var statics = Statics(("A", "Bool"));
        var before = WriteIr(Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB") }, statics: statics));
        var after = WriteIr(Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA_FIXED"), Coil(2, "N2", "OutB", "InB") }, statics: statics));

        var report = DiffRunner.Run(before, after, new[] { 1 });

        Assert.False(report.HasUnclaimedHeaderChange);
        Assert.False(report.HasInvarianceViolation);
        Assert.Contains("header unchanged", DiffOutputFormatter.FormatText(report));
    }

    // Without --only there is no declared change set, so nothing can be "outside" it. A plain diff
    // must never gate on a header change - that would fire on every re-export comparison.
    [Fact]
    public void NoOnlySet_HeaderChangeDoesNotGate()
    {
        var nets = new[] { Coil(1, "N1", "OutA", "InA") };
        var before = WriteIr(Block("FB_X", nets, statics: Statics(("A", "Bool"))));
        var after = WriteIr(Block("FB_X", nets, statics: Statics(("A", "Int"))));

        var report = DiffRunner.Run(before, after, Array.Empty<int>());

        Assert.True(report.Header.InterfaceChanged);
        Assert.False(report.HasInvarianceViolation);
    }

    // --- the comment-only carve-out (2026-08-14, ruled on the gate's FIRST CONTACT with real work) ---
    //
    // A fix-wave run widened a Modbus area and, as instructed, repaired two stale block comments in
    // the same file — one said the area covered "8 words" when it covered 35. The gate refused,
    // correctly by its own rules, and the author correctly declined --allow-header, because that flag
    // is gen-block-modify-purpose's and reaching for it means ROUTE, not DECLARE. Which left a
    // DOCUMENTATION-ONLY repair with no clean path under either modify skill — while the comments were
    // already false, so leaving them was not a neutral option.
    //
    // --only asks one question: did anything change outside the named networks that could ALTER WHAT
    // THE PLC DOES? An interface change answers yes. A block comment cannot. The report already knew
    // the difference (commentChanged: true, interfaceChanged: false); the verdict threw it away.

    private const string StaleComment = "Covers 8 words of MB_HOLD_REG.";
    private const string FixedComment = "Covers 35 words of MB_HOLD_REG.";

    [Fact]
    public void OnlyGate_CommentOnlyRepair_DoesNotGate()
    {
        var statics = Statics(("A", "Bool"));
        var nets = new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB") };
        var before = WriteIr(Block("FB_X", nets, statics: statics, comment: StaleComment));
        var after = WriteIr(Block("FB_X", nets, statics: statics, comment: FixedComment));

        var report = DiffRunner.Run(before, after, new[] { 1 });

        Assert.True(report.Header.CommentChanged);
        Assert.True(report.Header.CommentOnlyChange);
        Assert.False(report.Header.BehaviourBearingChange);
        Assert.False(report.HasUnclaimedHeaderChange);
        Assert.False(report.HasInvarianceViolation);
    }

    // Non-gating is NOT invisible. A comment repair is exactly how a stale comment gets fixed, and
    // equally where a correct one gets silently discarded — so it is reported on its own line.
    [Fact]
    public void OnlyGate_CommentOnlyRepair_IsStillReportedOnItsOwnLine()
    {
        var statics = Statics(("A", "Bool"));
        var nets = new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB") };
        var report = DiffRunner.Run(
            WriteIr(Block("FB_X", nets, statics: statics, comment: StaleComment)),
            WriteIr(Block("FB_X", nets, statics: statics, comment: FixedComment)),
            new[] { 1 });

        Assert.True(report.HasNonGatingCommentChange);

        var text = DiffOutputFormatter.FormatText(report);
        Assert.Contains("HEADER COMMENT CHANGED (does not gate)", text);
        Assert.Contains("INVARIANCE OK", text);
        Assert.Contains("block comment differs", text);
    }

    // 🔴 THE MUTATION THE RULING DEMANDS, IN THE DIRECTION THAT MATTERS. A relaxation that lets an
    // interface change through UNDER COVER OF A COMMENT EDIT is worse than the gap it fixes — so the
    // two changed together must still gate, and the reason must name the INTERFACE, not the comment.
    [Fact]
    public void OnlyGate_InterfaceChangeAlongsideAnIdenticalCommentChange_StillGates()
    {
        var nets = new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB") };
        var report = DiffRunner.Run(
            WriteIr(Block("FB_X", nets, statics: Statics(("A", "Bool")), comment: StaleComment)),
            WriteIr(Block("FB_X", nets, statics: Statics(("A", "Int")), comment: FixedComment)),
            new[] { 1 });

        Assert.True(report.Header.CommentChanged);          // the same comment edit as the case above
        Assert.True(report.Header.InterfaceChanged);
        Assert.False(report.Header.CommentOnlyChange);
        Assert.True(report.HasUnclaimedHeaderChange);
        Assert.True(report.HasInvarianceViolation);
        Assert.False(report.HasNonGatingCommentChange);     // it is not the non-gating case

        var text = DiffOutputFormatter.FormatText(report);
        Assert.Contains("an INTERFACE member changed", text);
        Assert.DoesNotContain("HEADER COMMENT CHANGED (does not gate)", text);
    }

    // The other two behaviour-bearing fields must not drift into the carve-out either. A TITLE is
    // identity, and a rename creates a DUPLICATE block on import rather than updating one.
    [Fact]
    public void OnlyGate_TitleChangeAlongsideACommentChange_StillGates()
    {
        var nets = new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB") };
        var report = DiffRunner.Run(
            WriteIr(Block("FB_X", nets, title: "Before", comment: StaleComment)),
            WriteIr(Block("FB_X", nets, title: "After", comment: FixedComment)),
            new[] { 1 });

        Assert.True(report.HasUnclaimedHeaderChange);
        Assert.Contains("the block TITLE changed", DiffOutputFormatter.FormatText(report));
    }

    [Fact]
    public void OnlyGate_RenameAlongsideACommentChange_StillGates()
    {
        var nets = new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB") };
        var report = DiffRunner.Run(
            WriteIr(Block("FB_X", nets, comment: StaleComment)),
            WriteIr(Block("FB_Y", nets, comment: FixedComment)),
            new[] { 1 });

        Assert.True(report.HasUnclaimedHeaderChange);
        Assert.Contains("renamed", DiffOutputFormatter.FormatText(report));
    }

    // --- the empty remainder ------------------------------------------------------------------
    //
    // "All changes confined to --only {1}" over a block with ONE network proves nothing: the
    // remainder is empty. Measured on FB_Comms_ModbusServer, which has exactly one network — `--only
    // 1` reported OK with identical: 0. Same shape as drift-check's COMPARED: <n>.

    [Fact]
    public void OnlyGate_SingleNetworkBlock_SaysTheRemainderIsEmpty()
    {
        var nets = new[] { Coil(1, "N1", "OutA", "InA") };
        var report = DiffRunner.Run(
            WriteIr(Block("FB_X", nets)),
            WriteIr(Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA_FIXED") })),
            new[] { 1 });

        Assert.Equal(0, report.IdenticalCount);
        Assert.False(report.HasInvarianceViolation);   // still not a FAILURE - the claim is just empty

        var text = DiffOutputFormatter.FormatText(report);
        Assert.Contains("UNCHANGED REMAINDER: 0 network(s)", text);
        Assert.Contains("NOTHING WAS PROVEN", text);
    }

    [Fact]
    public void OnlyGate_MultiNetworkBlock_StatesTheRemainderItProved()
    {
        var nets = new[] { Coil(1, "N1", "OutA", "InA"), Coil(2, "N2", "OutB", "InB"), Coil(3, "N3", "OutC", "InC") };
        var report = DiffRunner.Run(
            WriteIr(Block("FB_X", nets)),
            WriteIr(Block("FB_X", new[] { Coil(1, "N1", "OutA", "InA_FIXED"), nets[1], nets[2] })),
            new[] { 1 });

        Assert.Equal(2, report.IdenticalCount);

        var text = DiffOutputFormatter.FormatText(report);
        Assert.Contains("UNCHANGED REMAINDER: 2 network(s) proven identical", text);
        Assert.DoesNotContain("NOTHING WAS PROVEN", text);
    }
}
