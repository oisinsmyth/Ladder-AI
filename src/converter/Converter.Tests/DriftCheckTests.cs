using System.Text.Json;
using Converter.DriftCheck;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-26 (2026-07-20): `converter drift-check` — Normalizer-compares each committed .ir against its
/// paired simatic-ml .xml and flags divergence. Uses DB fixtures (deterministic, no synthesis): an
/// in-sync pair MATCHes, a pair whose .ir was changed after export DRIFTS, an unpaired .ir SKIPs.
/// </summary>
public class DriftCheckTests : IDisposable
{
    private readonly string _projectDir;
    private readonly string _exportsDir;

    public DriftCheckTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), $"drift-ir-{Guid.NewGuid():N}");
        _exportsDir = Path.Combine(Path.GetTempPath(), $"drift-xml-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_projectDir);
        Directory.CreateDirectory(_exportsDir);

        var v1 = Db("DB_Match", new DbMember("A", "Bool", Retain: false, StartValue: null));
        var v2 = Db("DB_Drift", new DbMember("A", "Bool", Retain: false, StartValue: null));
        var v2Changed = Db("DB_Drift",
            new DbMember("A", "Bool", Retain: false, StartValue: null),
            new DbMember("B", "Int", Retain: false, StartValue: null));

        // In sync: .ir and .xml both from v1.
        WriteIr("DB_Match.ir", v1);
        WriteXml("DB_Match.xml", v1);

        // Drifted: export is v2 (one member); the .ir gained a member (v2Changed) and was never re-exported.
        WriteXml("DB_Drift.xml", v2);
        WriteIr("DB_Drift.ir", v2Changed);

        // Unpaired: an .ir with no export (e.g. a block added post-export).
        WriteIr("DB_Lonely.ir", Db("DB_Lonely", new DbMember("A", "Bool", Retain: false, StartValue: null)));
    }

    private static DbSource Db(string name, params DbMember[] members) =>
        new("0", name, 1, InstanceOfName: null, Comment: null, Members: members);

    private void WriteIr(string fileName, DbSource db) =>
        File.WriteAllText(Path.Combine(_projectDir, fileName), DbIrSerializer.Serialize(db));

    private void WriteXml(string fileName, DbSource db) =>
        DbSourceWriter.Write(db).SaveAsTiaExport(Path.Combine(_exportsDir, fileName));

    public void Dispose()
    {
        foreach (var dir in new[] { _projectDir, _exportsDir })
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void Run_ClassifiesMatchDriftAndUnpaired()
    {
        var report = DriftCheckRunner.Run(_projectDir, _exportsDir);
        var byName = report.Entries.ToDictionary(e => e.Name);

        Assert.Equal(DriftStatus.Match, byName["DB_Match"].Status);
        Assert.Equal(DriftStatus.Drifted, byName["DB_Drift"].Status);
        Assert.Equal(DriftStatus.Skipped, byName["DB_Lonely"].Status);
        Assert.True(report.HasDrift);
    }

    // REWRITTEN 2026-08-14, and the old version is worth reading: it set up a project sharing NO name
    // with the exports dir and asserted `HasDrift == false`, with a comment that named the condition
    // outright — "Nothing is COMPARED here at all — and without --complete none of it fails". That is
    // the defect, asserted as the contract. The property it was protecting is real and is kept below:
    // a COMMITTED corpus may legitimately lag the IR in both directions, so unpaired files must not
    // fail without --complete. What does not follow is that a run comparing NOTHING is a pass.
    [Fact]
    public void Run_UnpairedFilesDoNotFail_WhenSomethingWasActuallyCompared()
    {
        // DB_Match pairs and matches; DB_Lonely has no export; DB_Drift's export... also pairs, so
        // remove it from the comparison by using a project that keeps only the first two.
        var project = Path.Combine(Path.GetTempPath(), $"drift-ir-lag-{Guid.NewGuid():N}");
        Directory.CreateDirectory(project);
        try
        {
            var match = Db("DB_Match", new DbMember("A", "Bool", Retain: false, StartValue: null));
            File.WriteAllText(Path.Combine(project, "DB_Match.ir"), DbIrSerializer.Serialize(match));
            File.WriteAllText(Path.Combine(project, "DB_Solo.ir"), DbIrSerializer.Serialize(
                Db("DB_Solo", new DbMember("X", "Bool", Retain: false, StartValue: null))));

            // _exportsDir carries DB_Match.xml (pairs) and DB_Drift.xml (export-only). Both absences
            // are ordinary states for a lagging corpus, and one real comparison happened.
            var report = DriftCheckRunner.Run(project, _exportsDir);

            Assert.Equal(1, report.ComparedCount);
            Assert.False(report.ExaminedNothing);
            Assert.Equal(DriftStatus.Skipped, report.Entries.Single(e => e.Name == "DB_Solo").Status);
            Assert.Equal(DriftStatus.ExportOnly, report.Entries.Single(e => e.Name == "DB_Drift").Status);
            Assert.False(report.HasDrift);
        }
        finally
        {
            Directory.Delete(project, recursive: true);
        }
    }

    // FI-70. The runner enumerates .ir files, so an export with no .ir beside it could not appear in
    // the report under ANY status — a block added by hand in TIA was invisible to the only tool that
    // compares the two sides. It is now always reported.
    [Fact]
    public void Run_ReportsAnExportThatHasNoIrBesideIt()
    {
        var lonelyExportProject = Path.Combine(Path.GetTempPath(), $"drift-ir-none-{Guid.NewGuid():N}");
        Directory.CreateDirectory(lonelyExportProject);
        try
        {
            var report = DriftCheckRunner.Run(lonelyExportProject, _exportsDir);

            Assert.Equal(2, report.Entries.Count(e => e.Status == DriftStatus.ExportOnly));
            var entry = report.Entries.Single(e => e.Name == "DB_Match");
            Assert.Equal(DriftStatus.ExportOnly, entry.Status);
            Assert.Null(entry.IrPath);
            Assert.NotNull(entry.XmlPath);
        }
        finally
        {
            Directory.Delete(lonelyExportProject, recursive: true);
        }
    }

    // The whole point of --complete: the same directory contents, the same comparison, a different
    // verdict — because the caller has declared that an absence MEANS something.
    [Fact]
    public void Complete_TurnsBothKindsOfAbsenceIntoAFailure()
    {
        var noDriftProject = Path.Combine(Path.GetTempPath(), $"drift-ir-abs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(noDriftProject);
        try
        {
            // One matching pair (so nothing DRIFTS), one .ir with no export, and DB_Drift.xml left
            // over in the exports dir with no .ir. Only the absences are in play.
            var match = Db("DB_Match", new DbMember("A", "Bool", Retain: false, StartValue: null));
            File.WriteAllText(Path.Combine(noDriftProject, "DB_Match.ir"), DbIrSerializer.Serialize(match));
            File.WriteAllText(
                Path.Combine(noDriftProject, "DB_Lonely.ir"),
                DbIrSerializer.Serialize(Db("DB_Lonely", new DbMember("A", "Bool", Retain: false, StartValue: null))));

            Assert.False(DriftCheckRunner.Run(noDriftProject, _exportsDir).HasDrift);
            Assert.True(DriftCheckRunner.Run(noDriftProject, _exportsDir, complete: true).HasDrift);
        }
        finally
        {
            Directory.Delete(noDriftProject, recursive: true);
        }
    }

    // A green run must not read as the stronger claim it did not make.
    [Fact]
    public void FormatText_SaysWhichQuestionItAnswered_WhenSomethingWasNotJudged()
    {
        var withoutComplete = DriftCheckOutputFormatter.FormatText(DriftCheckRunner.Run(_projectDir, _exportsDir));
        Assert.Contains("SCOPE: comparison only.", withoutComplete);
        Assert.Contains("were NOT judged", withoutComplete);
        Assert.Contains("--complete", withoutComplete);

        var withComplete = DriftCheckOutputFormatter.FormatText(
            DriftCheckRunner.Run(_projectDir, _exportsDir, complete: true));
        Assert.Contains("SCOPE: --complete", withComplete);
        Assert.Contains("are findings, not ordinary states", withComplete);
    }

    [Fact]
    public void FormatText_And_Json_RenderStatuses()
    {
        var report = DriftCheckRunner.Run(_projectDir, _exportsDir);

        var text = DriftCheckOutputFormatter.FormatText(report);
        Assert.Contains("DRIFTED: DB_Drift", text);
        Assert.Contains("SKIPPED: DB_Lonely", text);
        Assert.Contains("MATCH: DB_Match", text);
        Assert.Contains("SUMMARY: 1 drifted, 1 match, 1 skipped, 0 export-only, 0 error", text);

        var json = DriftCheckOutputFormatter.FormatJson(report);
        using var doc = JsonDocument.Parse(json); // asserts valid JSON
        Assert.True(doc.RootElement.GetProperty("hasDrift").GetBoolean());
        Assert.Equal(3, doc.RootElement.GetProperty("entries").GetArrayLength());
    }
}
