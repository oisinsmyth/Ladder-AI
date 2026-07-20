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
        DbSourceWriter.Write(db).Save(Path.Combine(_exportsDir, fileName));

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

    [Fact]
    public void Run_AllInSync_HasDriftFalse()
    {
        // A project whose only member is the matching pair: no drift.
        var onlyMatchProject = Path.Combine(Path.GetTempPath(), $"drift-ir-clean-{Guid.NewGuid():N}");
        Directory.CreateDirectory(onlyMatchProject);
        try
        {
            var db = Db("DB_Solo", new DbMember("X", "Bool", Retain: false, StartValue: null));
            File.WriteAllText(Path.Combine(onlyMatchProject, "DB_Solo.ir"), DbIrSerializer.Serialize(db));

            var report = DriftCheckRunner.Run(onlyMatchProject, _exportsDir); // exportsDir has no DB_Solo.xml
            Assert.False(report.HasDrift);
            Assert.Equal(DriftStatus.Skipped, report.Entries.Single().Status);
        }
        finally
        {
            Directory.Delete(onlyMatchProject, recursive: true);
        }
    }

    [Fact]
    public void FormatText_And_Json_RenderStatuses()
    {
        var report = DriftCheckRunner.Run(_projectDir, _exportsDir);

        var text = DriftCheckOutputFormatter.FormatText(report);
        Assert.Contains("DRIFTED: DB_Drift", text);
        Assert.Contains("SKIPPED: DB_Lonely", text);
        Assert.Contains("MATCH: DB_Match", text);
        Assert.Contains("SUMMARY: 1 drifted, 1 match, 1 skipped, 0 error", text);

        var json = DriftCheckOutputFormatter.FormatJson(report);
        using var doc = JsonDocument.Parse(json); // asserts valid JSON
        Assert.True(doc.RootElement.GetProperty("hasDrift").GetBoolean());
        Assert.Equal(3, doc.RootElement.GetProperty("entries").GetArrayLength());
    }
}
