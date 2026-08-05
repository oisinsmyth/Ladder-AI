using Converter.RelationReconcile;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-39 checks 2 + 3 (2026-08-05): `converter relation-reconcile` — reconcile the relation-id sets
/// across the spec artifacts, and check that each `verified-cross-block` precondition cites something
/// actually written.
///
/// Check 2 is a regression guard rather than a catch (the fixture's legs already reconcile); what it
/// converts is three HAND-ASSERTED counts inside the artifact into computed ones. Check 3 closes a
/// demonstrated loophole: the precondition class specified citation FORM, not probative force, so a
/// citation pointing at a bare declaration satisfied the rule while proving nothing.
/// </summary>
public class RelationReconcileTests : IDisposable
{
    private readonly string _dir;
    private readonly string _specs;

    public RelationReconcileTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"relrec-{Guid.NewGuid():N}");
        _specs = Path.Combine(_dir, "equipment-specs");
        Directory.CreateDirectory(_specs);
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

    private void WriteSpec(string instance, params string[] ids) =>
        File.WriteAllText(Path.Combine(_specs, instance + ".md"),
            "# spec\n" + string.Join("\n", ids.Select(id => $"- **{id}** something [ref]")) + "\n");

    private string WriteLedger(params (string Instance, string Id, string Disposition, string Evidence, string Precondition)[] rows)
    {
        var path = Path.Combine(_dir, "code-structure.md");
        var sb = new System.Text.StringBuilder("# D2\n");
        foreach (var group in rows.GroupBy(r => r.Instance))
        {
            sb.Append($"\n## Ledger — `{group.Key}` (title)\n\n");
            sb.Append("| relation | disposition | evidence | precondition class |\n|---|---|---|---|\n");
            foreach (var r in group)
            {
                sb.Append($"| {r.Id} | {r.Disposition} | {r.Evidence} | {r.Precondition} |\n");
            }
        }

        sb.Append("\n## **STOPPED — no render produced.**\n");
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    // Same ledger, but with a real D3 render appended instead of the stop heading.
    private string WriteLedgerWithRender(
        (string Instance, string Id, string Disposition, string Evidence, string Precondition)[] rows,
        string renderBody)
    {
        var path = Path.Combine(_dir, "code-structure.md");
        var sb = new System.Text.StringBuilder("# D2\n");
        foreach (var group in rows.GroupBy(r => r.Instance))
        {
            sb.Append($"\n## Ledger — `{group.Key}` (title)\n\n");
            sb.Append("| relation | disposition | evidence | precondition class |\n|---|---|---|---|\n");
            foreach (var r in group)
            {
                sb.Append($"| {r.Id} | {r.Disposition} | {r.Evidence} | {r.Precondition} |\n");
            }
        }

        sb.Append("\n## D3 — Render\n\n").Append(renderBody);
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private string WriteRegister(params (string Instance, string Id)[] rows)
    {
        var path = Path.Combine(_dir, "requirements.md");
        var sb = new System.Text.StringBuilder("# register\n");
        foreach (var group in rows.GroupBy(r => r.Instance))
        {
            sb.Append($"\n### `{group.Key}` — title (class)\n\n");
            sb.Append("| REQ | Rel | Text | Class | Provenance | Open |\n|---|---|---|---|---|---|\n");
            var n = 1;
            foreach (var r in group)
            {
                sb.Append($"| REQ-{n++:000} | {r.Id} | text | control | ref | — |\n");
            }
        }

        File.WriteAllText(path, sb.ToString());
        return path;
    }

    [Fact]
    public void AllLegsReconcile_NoDifferences_AndStoppedRenderIsAbsentNotZero()
    {
        WriteSpec("InstA", "C1", "P1");
        var ledger = WriteLedger(("InstA", "C1", "render", "x", "—"), ("InstA", "P1", "render", "y", "—"));
        var register = WriteRegister(("InstA", "C1"), ("InstA", "P1"));

        var report = RelationReconcileRunner.Run(_specs, ledger, register, projectDir: null);

        Assert.All(report.Differences, d => Assert.Empty(d.MissingInTo));
        var render = Assert.Single(report.Legs, l => l.Kind == LegKind.Render);
        Assert.False(render.Present); // ABSENT — never "0 relations, reconciles trivially"
        Assert.False(report.HasFindings);
    }

    [Fact]
    public void RelationMissingFromLedger_IsReportedWithItsInstanceQualifier()
    {
        WriteSpec("InstA", "C1", "C2");
        var ledger = WriteLedger(("InstA", "C1", "render", "x", "—"));
        var register = WriteRegister(("InstA", "C1"), ("InstA", "C2"));

        var report = RelationReconcileRunner.Run(_specs, ledger, register, projectDir: null);

        var diff = Assert.Single(report.Differences,
            d => d.From == LegKind.Specs && d.To == LegKind.Ledger && d.MissingInTo.Count > 0);
        Assert.Equal(new RelationKey("InstA", "C2"), Assert.Single(diff.MissingInTo));
        Assert.True(report.HasFindings);
    }

    // The vacuity regression: a bare union of ids across instances would let one instance's C1 satisfy
    // another's, and the check would pass while relations were genuinely missing.
    [Fact]
    public void TwoInstancesSameId_DoNotCancelEachOther()
    {
        WriteSpec("InstA", "C1");
        WriteSpec("InstB", "C1");
        var ledger = WriteLedger(("InstA", "C1", "render", "x", "—"));   // InstB.C1 absent
        var register = WriteRegister(("InstA", "C1"), ("InstB", "C1"));

        var report = RelationReconcileRunner.Run(_specs, ledger, register, projectDir: null);

        var diff = Assert.Single(report.Differences,
            d => d.From == LegKind.Specs && d.To == LegKind.Ledger && d.MissingInTo.Count > 0);
        Assert.Equal(new RelationKey("InstB", "C1"), Assert.Single(diff.MissingInTo));
    }

    // A leg that parses nothing must be a hard error. A silent all-green after format drift would make
    // this check worse than not having it.
    [Fact]
    public void LedgerWithNoRelationTable_IsAHardError_NotACleanReconcile()
    {
        WriteSpec("InstA", "C1");
        var empty = Path.Combine(_dir, "empty.md");
        File.WriteAllText(empty, "# nothing to see\n");
        var register = WriteRegister(("InstA", "C1"));

        var ex = Assert.Throws<RelationReconcileFormatException>(
            () => RelationReconcileRunner.Run(_specs, empty, register, projectDir: null));
        Assert.Contains("Refusing to report a clean reconcile", ex.Message);
    }

    [Fact]
    public void SpecsWithNoBullets_IsAHardError()
    {
        File.WriteAllText(Path.Combine(_specs, "InstA.md"), "# no relation bullets here\n");
        var ledger = WriteLedger(("InstA", "C1", "render", "x", "—"));
        var register = WriteRegister(("InstA", "C1"));

        Assert.Throws<RelationReconcileFormatException>(
            () => RelationReconcileRunner.Run(_specs, ledger, register, projectDir: null));
    }

    // Measured format facts: an em-dash precondition is empty, and `**rebind**` is bold in the artifact.
    [Fact]
    public void EmDashPrecondition_IsEmpty_AndBoldDispositionParses()
    {
        WriteSpec("InstA", "C1", "C2");
        var ledger = WriteLedger(
            ("InstA", "C1", "**rebind**", "x", "—"),
            ("InstA", "C2", "in-FB", "y", "verified-in-block"));
        var register = WriteRegister(("InstA", "C1"), ("InstA", "C2"));

        var report = RelationReconcileRunner.Run(_specs, ledger, register, projectDir: null);

        // Neither row is verified-cross-block, so no citation is attempted — the em-dash must not be
        // mistaken for a precondition value.
        Assert.Empty(report.Citations);
        Assert.False(report.HasFindings);
    }

    // ---- FI-44 item 3: a leg that matched nothing must not read as clean ------------------------
    //
    // The demonstrated shape: the SKILL's own D3 example wrote `instance: iDB_MotorDOL_Conv07` while
    // every other leg is keyed on the SPEC INSTANCE, so following the documentation produced a render
    // leg that could not intersect anything. The comparison happened and examined nothing.

    [Fact]
    public void RenderKeyedToTheInstanceDbName_MatchesNothing_AndSaysSo()
    {
        WriteSpec("Conveyor07", "C1", "C2");
        var ledger = WriteLedgerWithRender(
            new[] { ("Conveyor07", "C1", "rendered", "`DQ_11`", "—"), ("Conveyor07", "C2", "rendered", "`DI_23`", "—") },
            "NETWORK \"Conveyor-07 Automatic Control\"        instance: iDB_MotorDOL_Conv07\n" +
            "  .RunningFB := DI_23   [C2]\n  DQ_11 := .Run   [C1]\n");
        var register = WriteRegister(("Conveyor07", "C1"), ("Conveyor07", "C2"));

        var report = RelationReconcileRunner.Run(_specs, ledger, register, projectDir: null);

        var uncompared = Assert.Single(report.UncomparedLegs);
        Assert.Equal(LegKind.Render, uncompared.Kind);
        Assert.Equal(2, uncompared.Count);
        Assert.True(report.HasFindings);
        Assert.Contains("compared against nothing",
            RelationReconcileOutputFormatter.FormatText(report), StringComparison.Ordinal);
    }

    // The deliberate exception, guarded: ABSENT is a parse fact about an artifact that is not there and
    // must keep NOT gating, even now that a present-but-matching-nothing leg does.
    [Fact]
    public void AbsentRender_IsNotAMatchedNothingLeg_AndStillDoesNotGate()
    {
        WriteSpec("InstA", "C1", "P1");
        var ledger = WriteLedger(("InstA", "C1", "rendered", "x", "—"), ("InstA", "P1", "rendered", "y", "—"));
        var register = WriteRegister(("InstA", "C1"), ("InstA", "P1"));

        var report = RelationReconcileRunner.Run(_specs, ledger, register, projectDir: null);

        Assert.False(Assert.Single(report.Legs, l => l.Kind == LegKind.Render).Present);
        Assert.Empty(report.UncomparedLegs);   // absent is not "matched nothing"
        Assert.False(report.HasFindings);
    }

    // A render that agrees on ONE relation and disagrees on the rest is a difference, not a
    // matched-nothing: the leg was genuinely compared. Keeping these apart is the whole point.
    [Fact]
    public void RenderSharingOneKey_IsADifference_NotAMatchedNothingLeg()
    {
        WriteSpec("InstA", "C1", "C2");
        var ledger = WriteLedgerWithRender(
            new[] { ("InstA", "C1", "rendered", "x", "—"), ("InstA", "C2", "rendered", "y", "—") },
            "NETWORK \"A\" instance: `InstA`\n  .Coil := Tag   [C1]\n");
        var register = WriteRegister(("InstA", "C1"), ("InstA", "C2"));

        var report = RelationReconcileRunner.Run(_specs, ledger, register, projectDir: null);

        Assert.Empty(report.UncomparedLegs);
        var diff = Assert.Single(report.Differences,
            d => d.To == LegKind.Render && d.From == LegKind.Ledger && d.MissingInTo.Count > 0);
        Assert.Equal(new RelationKey("InstA", "C2"), Assert.Single(diff.MissingInTo));
        Assert.True(report.HasFindings);
    }

    // ---- FI-45 item 3: partition the ledger by disposition -------------------------------------

    // Five of the seven documented dispositions mean "this relation does NOT become a D3 term". Each
    // one used to make the check exit 1 on a correct artifact; `render-BLOCKED` was undeclarable.
    [Theory]
    [InlineData("in-FB")]
    [InlineData("discharged")]
    [InlineData("render-BLOCKED")]
    [InlineData("render-stopped")]
    [InlineData("out-of-scope-obligation")]
    public void NonRenderBoundDisposition_IsAccountedFor_NotADifference(string disposition)
    {
        WriteSpec("InstA", "C1", "C2");
        var ledger = WriteLedgerWithRender(
            new[] { ("InstA", "C1", "rendered", "x", "—"), ("InstA", "C2", disposition, "y", "—") },
            "NETWORK \"A\" instance: `InstA`\n  .Coil := Tag   [C1]\n");
        var register = WriteRegister(("InstA", "C1"), ("InstA", "C2"));

        var report = RelationReconcileRunner.Run(_specs, ledger, register, projectDir: null);

        Assert.All(report.Differences, d => Assert.Empty(d.MissingInTo));
        Assert.False(report.HasFindings);
        var group = Assert.Single(report.LedgerDispositions, g => g.Class == DispositionClass.AccountedFor);
        Assert.Equal(1, group.Count);
    }

    // The true positive the partition must not weaken: a `rendered` row absent from the render.
    [Fact]
    public void RenderBoundRelationMissingFromTheRender_StillFails()
    {
        WriteSpec("InstA", "C1", "C2", "C3");
        var ledger = WriteLedgerWithRender(
            new[]
            {
                ("InstA", "C1", "rendered", "x", "—"),
                ("InstA", "C2", "render-BLOCKED", "Q-C04", "—"),
                ("InstA", "C3", "rendered", "z", "—"),
            },
            "NETWORK \"A\" instance: `InstA`\n  .Coil := Tag   [C1]\n");
        var register = WriteRegister(("InstA", "C1"), ("InstA", "C2"), ("InstA", "C3"));

        var report = RelationReconcileRunner.Run(_specs, ledger, register, projectDir: null);

        var diff = Assert.Single(report.Differences,
            d => d.From == LegKind.Ledger && d.To == LegKind.Render && d.MissingInTo.Count > 0);
        Assert.Equal(new RelationKey("InstA", "C3"), Assert.Single(diff.MissingInTo));   // C2 is accounted for
        Assert.True(report.HasFindings);
    }

    // Out of the render, nothing is filtered: a term tagged with a relation no artifact declares is
    // still the C-606 finding it always was.
    [Fact]
    public void RenderTermForAnUndeclaredRelation_IsStillADifference()
    {
        WriteSpec("InstA", "C1");
        var ledger = WriteLedgerWithRender(
            new[] { ("InstA", "C1", "rendered", "x", "—") },
            "NETWORK \"A\" instance: `InstA`\n  .Coil := Tag   [C1]\n  .Other := Tag2   [C9]\n");
        var register = WriteRegister(("InstA", "C1"));

        var report = RelationReconcileRunner.Run(_specs, ledger, register, projectDir: null);

        Assert.Empty(report.UncomparedLegs);   // it shares C1 — it was compared
        var diff = Assert.Single(report.Differences,
            d => d.From == LegKind.Render && d.To == LegKind.Specs && d.MissingInTo.Count > 0);
        Assert.Equal(new RelationKey("InstA", "C9"), Assert.Single(diff.MissingInTo));
    }

    // An invented disposition must not be the cheap way out of the render obligation.
    [Fact]
    public void UnrecognizedDisposition_FailsClosed_AndIsWarnedAbout()
    {
        WriteSpec("InstA", "C1", "C2");
        var ledger = WriteLedgerWithRender(
            new[] { ("InstA", "C1", "rendered", "x", "—"), ("InstA", "C2", "interface-only, driver absent", "y", "—") },
            "NETWORK \"A\" instance: `InstA`\n  .Coil := Tag   [C1]\n");
        var register = WriteRegister(("InstA", "C1"), ("InstA", "C2"));

        var report = RelationReconcileRunner.Run(_specs, ledger, register, projectDir: null);

        Assert.Contains(report.Warnings, w => w.Contains("outside the documented vocabulary", StringComparison.Ordinal));
        var diff = Assert.Single(report.Differences,
            d => d.From == LegKind.Ledger && d.To == LegKind.Render && d.MissingInTo.Count > 0);
        Assert.Equal(new RelationKey("InstA", "C2"), Assert.Single(diff.MissingInTo));
    }

    // Disposition cells are compound and decorated in the real corpus — every string below is taken
    // from a committed `code-structure.md` or from the SKILL's own D2 table.
    [Theory]
    [InlineData("rendered", DispositionClass.RenderBound, "rendered")]
    [InlineData("render", DispositionClass.RenderBound, "rendered")]           // artifact spelling
    [InlineData("**rebind**", DispositionClass.RenderBound, "rebind")]
    [InlineData("rebind → render", DispositionClass.RenderBound, "rebind")]
    [InlineData("in-FB + render(driver)", DispositionClass.RenderBound, "rendered")]  // a driver term IS owed
    [InlineData("in-FB", DispositionClass.AccountedFor, "in-FB")]
    [InlineData("in-FB + driver-BLOCKED", DispositionClass.AccountedFor, "in-FB")]
    [InlineData("discharged (S5)", DispositionClass.AccountedFor, "discharged")]
    [InlineData("render-BLOCKED", DispositionClass.AccountedFor, "render-BLOCKED")]
    [InlineData("render-BLOCKED `[contested]`", DispositionClass.AccountedFor, "render-BLOCKED")]
    [InlineData("render-stopped", DispositionClass.AccountedFor, "render-stopped")]
    [InlineData("out-of-scope-obligation", DispositionClass.AccountedFor, "out-of-scope-obligation")]
    [InlineData("Satisfied", DispositionClass.Unrecognized, "Satisfied")]
    [InlineData("", DispositionClass.Unrecognized, "(blank)")]
    public void Classify_ReadsTheRealDispositionCells(string cell, DispositionClass expected, string label)
    {
        var verdict = RelationDispositions.Classify(cell);

        Assert.Equal(expected, verdict.Class);
        Assert.Equal(label, verdict.Label);
        // Unrecognized is held to the render obligation — an invented word is not an escape hatch.
        Assert.Equal(expected != DispositionClass.AccountedFor, verdict.IsRenderBound);
    }

    [Fact]
    public void RenderTermTags_WhenPresent_AreParsedPerInstance()
    {
        WriteSpec("InstA", "C1");
        var ledgerPath = Path.Combine(_dir, "code-structure.md");
        File.WriteAllText(ledgerPath,
            "# D2\n\n## Ledger — `InstA` (t)\n\n| relation | disposition | evidence | precondition class |\n|---|---|---|---|\n" +
            "| C1 | render | x | — |\n\n# D3\n\nNETWORK \"A\" instance: `InstA`\n  .Coil := Tag   [C1]\n");
        var register = WriteRegister(("InstA", "C1"));

        var report = RelationReconcileRunner.Run(_specs, ledgerPath, register, projectDir: null);

        var render = Assert.Single(report.Legs, l => l.Kind == LegKind.Render);
        Assert.True(render.Present);
        Assert.Contains(new RelationKey("InstA", "C1"), render.Keys);
    }
}
