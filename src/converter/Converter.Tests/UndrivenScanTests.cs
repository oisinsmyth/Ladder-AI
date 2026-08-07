using Converter.Ir;
using Converter.SimaticMl;
using Converter.UndrivenScan;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-39 check 4 (2026-08-05): `converter undriven-scan` — per INSTANCE, which interface members
/// actually receive a value.
///
/// The real new value over the project-wide reference graph is per-instance resolution: that graph
/// canonicalizes to (FB, member) and pools across every instance, which is right for its own question and
/// wrong for this one — if one instance drives a member and another does not, the pooled view shows the
/// member alive and the gap disappears. A dropped bypass on one instance of a shared block is exactly
/// that shape (`docs/evidence/PlantAutoControl-bench-grading.md`, REQ-012).
/// </summary>
public class UndrivenScanTests : IDisposable
{
    private readonly string _dir;

    public UndrivenScanTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"undriven-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        // The FB reads IO.Cmd, and declares IO.Optional which it never touches.
        WriteBlock("FB_M.ir", "FB_M",
            statics: new[]
            {
                new DbMember("IO", "Struct", false, null, NestedMembers: new[]
                {
                    new DbMember("Cmd", "Bool", Retain: false, StartValue: null),
                    new DbMember("Optional", "Bool", Retain: false, StartValue: null),
                }),
            },
            networks: new[]
            {
                new IrNetwork(1, "Uses Cmd", new[]
                {
                    new CoilAssignment("IO.Run", new Expr.TagRef("IO.Cmd")),
                }),
            });

        // Two instances of the same FB — the pooling trap.
        WriteInstanceDb("iDB_A.ir", "iDB_A", "FB_M");
        WriteInstanceDb("iDB_B.ir", "iDB_B", "FB_M");
    }

    private void WriteBlock(string fileName, string name, DbMember[] statics, IrNetwork[] networks) =>
        File.WriteAllText(
            Path.Combine(_dir, fileName),
            IrSerializer.SerializeBlockReadable(
                new IrBlock("0", "FB", name, 1, "LAD", null, networks, StaticMembers: statics)));

    private void WriteInstanceDb(string fileName, string name, string fb) =>
        File.WriteAllText(Path.Combine(_dir, fileName), DbIrSerializer.Serialize(
            new DbSource("0", name, 2, InstanceOfName: fb, Comment: null, Members: new[]
            {
                new DbMember("IO", "Struct", false, null, NestedMembers: new[]
                {
                    new DbMember("Cmd", "Bool", Retain: false, StartValue: null),
                    new DbMember("Optional", "Bool", Retain: false, StartValue: null),
                }),
            })));

    // A caller that drives iDB_A.IO.Cmd but leaves iDB_B's unwired.
    private string WriteCaller()
    {
        var path = Path.Combine(_dir, "caller_FC.ir");
        File.WriteAllText(path, IrSerializer.SerializeBlockReadable(
            new IrBlock("0", "FC", "FC_Caller", 9, "LAD", null, new[]
            {
                new IrNetwork(6, "Drives A only", new[]
                {
                    new CoilAssignment("iDB_A.IO.Cmd", new Expr.TagRef("Src")),
                }),
            })));
        return path;
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

    // The regression that makes this check worth building: a driven instance must not mask an undriven one.
    [Fact]
    public void OneInstanceDriven_DoesNotMaskTheUndrivenSibling()
    {
        var report = UndrivenScanRunner.Run(_dir, "FB_M", Array.Empty<string>(), new[] { WriteCaller() });

        var a = Assert.Single(report.Members, m => m.Instance == "iDB_A" && m.Member == "IO.Cmd");
        var b = Assert.Single(report.Members, m => m.Instance == "iDB_B" && m.Member == "IO.Cmd");

        Assert.Equal(DriveState.Driven, a.State);
        Assert.Contains("FC_Caller N6", a.Writers);
        Assert.Equal(DriveState.Undriven, b.State);
        Assert.True(report.HasFindings);
    }

    // A caller file outside --project still contributes writers (the batch-merge idiom ProjectIndex uses).
    [Fact]
    public void CallerOutsideProject_ContributesWriters()
    {
        var withoutCaller = UndrivenScanRunner.Run(_dir, "FB_M", Array.Empty<string>(), Array.Empty<string>());
        var withCaller = UndrivenScanRunner.Run(_dir, "FB_M", Array.Empty<string>(), new[] { WriteCaller() });

        Assert.Equal(DriveState.Undriven,
            Assert.Single(withoutCaller.Members, m => m.Instance == "iDB_A" && m.Member == "IO.Cmd").State);
        Assert.Equal(DriveState.Driven,
            Assert.Single(withCaller.Members, m => m.Instance == "iDB_A" && m.Member == "IO.Cmd").State);
    }

    // The REQ-012 shape: declared on the interface, no writer, and the FB never reads it either.
    [Fact]
    public void MemberNeitherWrittenNorReadByTheFb_IsDeadInterface_AndDoesNotGate()
    {
        var report = UndrivenScanRunner.Run(_dir, "FB_M", new[] { "iDB_A" }, new[] { WriteCaller() });

        var optional = Assert.Single(report.Members, m => m.Member == "IO.Optional");
        Assert.Equal(DriveState.DeadInterfaceMember, optional.State);

        // Every gating member is driven here, so a dead-interface fact alone must not set HasFindings —
        // a reusable block's unused optional inputs are normal, and gating on them would bury real findings.
        Assert.False(report.HasFindings);
    }

    [Fact]
    public void InstanceFilter_NarrowsToTheNamedInstance()
    {
        var report = UndrivenScanRunner.Run(_dir, "FB_M", new[] { "iDB_B" }, new[] { WriteCaller() });

        Assert.All(report.Members, m => Assert.Equal("iDB_B", m.Instance));
    }

    [Fact]
    public void Report_StatesItsDenominator_AndJsonIsValid()
    {
        var report = UndrivenScanRunner.Run(_dir, "FB_M", Array.Empty<string>(), Array.Empty<string>());

        Assert.Equal(3, report.FilesScanned); // FB_M.ir + the two instance DBs
        Assert.Contains("3 file(s) scanned", UndrivenScanOutputFormatter.FormatText(report));

        var json = UndrivenScanOutputFormatter.FormatJson(report);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("FB_M", doc.RootElement.GetProperty("fb").GetString());
    }

    // Hints are opt-in: a heuristic that fires on every undriven member buries the findings it sits beside.
    [Fact]
    public void NameJoinHints_AreOptIn()
    {
        var without = UndrivenScanRunner.Run(_dir, "FB_M", Array.Empty<string>(), Array.Empty<string>());

        Assert.All(without.Members, m => Assert.Empty(m.NameJoinHints));
    }

    // FI-44 — "empty is not clean". Before this, scanning a block that had never been written
    // produced zero rows, zero findings and EXIT 0: an FB that did not exist passed the
    // drive-state check. A check whose failure mode is silent success is worse than no check,
    // because it is believed.
    [Fact]
    public void UnknownBlock_IsNotAPass_ItIsAnUnansweredQuestion()
    {
        var report = UndrivenScanRunner.Run(_dir, "FB_NeverWritten", Array.Empty<string>(), Array.Empty<string>());

        Assert.Equal(ScanScope.UnknownBlock, report.Scope);
        Assert.True(report.ExaminedNothing);
        Assert.Empty(report.Members);

        // Kept distinct from HasFindings: a finding is a defect in the plant, this is a defect in
        // the question, and they call for different actions.
        Assert.False(report.HasFindings);
    }

    // The other way to examine nothing, and it needs different words because it needs a different
    // fix: the block is real, but per-instance resolution over zero instances has resolved nothing.
    [Fact]
    public void BlockWithNoInstances_IsAlsoNotAPass()
    {
        WriteBlock("FB_Uninstantiated.ir", "FB_Uninstantiated",
            statics: new[] { new DbMember("IO", "Struct", false, null, NestedMembers: new[]
            {
                new DbMember("Cmd", "Bool", Retain: false, StartValue: null),
            }) },
            networks: Array.Empty<IrNetwork>());

        var report = UndrivenScanRunner.Run(_dir, "FB_Uninstantiated", Array.Empty<string>(), Array.Empty<string>());

        Assert.Equal(ScanScope.NoInstances, report.Scope);
        Assert.True(report.ExaminedNothing);
    }

    // Guard the true positive: fixing the false clean must not weaken the real check.
    [Fact]
    public void RealBlockWithInstances_StillScansAndStillReportsFindings()
    {
        var report = UndrivenScanRunner.Run(_dir, "FB_M", Array.Empty<string>(), Array.Empty<string>());

        Assert.Equal(ScanScope.Scanned, report.Scope);
        Assert.False(report.ExaminedNothing);
        Assert.NotEmpty(report.Members);
    }

    // FI-50 (2026-08-07). A MULTI-INSTANCE — an FB placed as a STATIC member of another FB rather
    // than given its own instance DB — was invisible here, because instances were read only off DB
    // sources carrying an InstanceOf.
    //
    // Why that is not a corner case: C-132 makes the single-STATIC-UDT interface the house style, so
    // a corpus can consist of nothing but multi-instances. Found on a live job where EVERY FB
    // reported "no instances" and the scan exited without examining one member of one block.
    private void WriteOwnerWithTwoPlacements() =>
        WriteBlock("FB_Owner.ir", "FB_Owner",
            statics: new[]
            {
                // Two placements of the same FB. One is wired, one is not — the pooling trap again,
                // and the reason a per-instance answer is the point.
                new DbMember("Left", "\"FB_M\"", Retain: false, StartValue: null),
                new DbMember("Right", "\"FB_M\"", Retain: false, StartValue: null),
                // An IEC timer static is instruction state, not interface: nobody "drives" its Q.
                new DbMember("Dwell", "TON_TIME", Retain: false, StartValue: null),
            },
            networks: new[]
            {
                new IrNetwork(4, "Drives the left placement only", new[]
                {
                    new CoilAssignment("Left.IO.Cmd", new Expr.TagRef("Src")),
                }),
            });

    [Fact]
    public void MultiInstanceStatic_IsAnInstance_AndResolvesPerPlacement()
    {
        WriteOwnerWithTwoPlacements();

        var report = UndrivenScanRunner.Run(_dir, "FB_M", Array.Empty<string>(), Array.Empty<string>());

        var left = Assert.Single(report.Members,
            m => m.Instance == "FB_Owner/Left" && m.Member == "IO.Cmd");
        var right = Assert.Single(report.Members,
            m => m.Instance == "FB_Owner/Right" && m.Member == "IO.Cmd");

        Assert.Equal(DriveState.Driven, left.State);
        Assert.Contains("FB_Owner N4", left.Writers);
        Assert.Equal(DriveState.Undriven, right.State);
    }

    // The owning FB has no instance DB of its own here, so the placement is identified by its
    // DECLARATION SITE. That form says plainly it is a class and not a placement — and reporting it
    // is still far better than reporting nothing, which is the FI-44 failure wearing a new hat.
    [Fact]
    public void MultiInstanceOwner_WithNoInstanceDb_StillExaminesTheInterface()
    {
        WriteOwnerWithTwoPlacements();

        var report = UndrivenScanRunner.Run(_dir, "FB_M", Array.Empty<string>(), Array.Empty<string>());

        Assert.Equal(ScanScope.Scanned, report.Scope);
        Assert.False(report.ExaminedNothing);
        Assert.All(
            report.Members.Where(m => m.Instance.StartsWith("FB_Owner/", StringComparison.Ordinal)),
            m => Assert.Contains('/', m.Instance));
    }

    // An IEC timer's Q and ET are written by the timer instruction, not by any caller, so counting
    // them as members nobody drives is a false positive — and a loud one, since every dwell in a
    // sequencer carries one.
    [Fact]
    public void IecTimerStatics_AreNotReportedAsUndrivenMembers()
    {
        WriteOwnerWithTwoPlacements();

        var report = UndrivenScanRunner.Run(_dir, "FB_Owner", Array.Empty<string>(), Array.Empty<string>());

        Assert.DoesNotContain(report.Members, m => m.Member.StartsWith("Dwell.", StringComparison.Ordinal));
    }

    // A placement is not a leaf of its owner: FB_Owner's own scan must not report `Left.IO.Cmd` as
    // one of FB_Owner's members. It belongs to FB_M's scan, where it is judged against FB_M's
    // interface.
    [Fact]
    public void NestedPlacement_IsNotAlsoALeafOfItsOwner()
    {
        WriteOwnerWithTwoPlacements();

        var report = UndrivenScanRunner.Run(_dir, "FB_Owner", Array.Empty<string>(), Array.Empty<string>());

        Assert.DoesNotContain(report.Members, m => m.Member.StartsWith("Left.", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Members, m => m.Member.StartsWith("Right.", StringComparison.Ordinal));
    }
}
