using Converter.RelationReconcile;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-35 checks 2 + 3 (2026-08-05): `converter relation-reconcile` — reconcile the relation-id sets
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
