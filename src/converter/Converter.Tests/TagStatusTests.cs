using Converter.Ir;
using Converter.SimaticMl;
using Converter.TagStatus;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-24 (2026-07-18): `converter tagstatus` — mechanical exists/proposed classification of tag
/// names against a project export, reusing ProjectIndex + AccessNode.FromDottedPath (the same
/// primitive preflight uses, so the two never disagree). Builds a real on-disk project dir the way
/// the command runs.
/// </summary>
public class TagStatusTests : IDisposable
{
    private readonly string _projectDir;

    public TagStatusTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), $"tagstatus-proj-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_projectDir);

        WriteProjectFile("DB_Input.ir", DbIrSerializer.Serialize(
            new DbSource("0", "DB_Input", 1, InstanceOfName: null, Comment: "Inputs.",
                Members: new[] { new DbMember("Cycle_Start", "Bool", Retain: false, StartValue: null) })));
        WriteProjectFile("Tags.ir", TagTableIrSerializer.Serialize(new PlcTagTableSource("0", "Tags", new[]
        {
            new PlcTagSource("1", "DI3_SYS_CycleStart", "Bool", "%I0.0", true, true, true, null),
            new PlcTagSource("2", "Clock_0.5Hz", "Bool", "%M0.0", true, true, true, null),
        })));
    }

    private void WriteProjectFile(string fileName, string content) =>
        File.WriteAllText(Path.Combine(_projectDir, fileName), content);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_projectDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Run_ClassifiesDbMemberAndBareTagAsExists_MadeUpAsProposed()
    {
        var report = TagStatusRunner.Run(
            new[] { "DB_Input.Cycle_Start", "DI3_SYS_CycleStart", "DB_Input", "MadeUpTag" }, _projectDir);

        var byName = report.Entries.ToDictionary(e => e.Name);
        Assert.True(byName["DB_Input.Cycle_Start"].Exists);          // DB.member -> classified by its root
        Assert.Equal("DB_Input", byName["DB_Input.Cycle_Start"].Root);
        Assert.True(byName["DI3_SYS_CycleStart"].Exists);            // bare tag-table tag
        Assert.True(byName["DB_Input"].Exists);                      // the DB name itself
        Assert.False(byName["MadeUpTag"].Exists);                    // not in the export
        Assert.True(report.HasProposed);
    }

    // Locks the whole-name-first logic: a bare tag-table tag whose own name contains a dot
    // (Clock_0.5Hz) must resolve as EXISTS, not be mis-split into a "Clock_0" root that misses.
    [Fact]
    public void Run_DottedTagTableName_ResolvesWholeName_NotMisSplitRoot()
    {
        var report = TagStatusRunner.Run(new[] { "Clock_0.5Hz" }, _projectDir);

        var entry = Assert.Single(report.Entries);
        Assert.True(entry.Exists);
        Assert.False(report.HasProposed);
    }

    [Fact]
    public void Run_AllExisting_HasProposedFalse()
    {
        var report = TagStatusRunner.Run(new[] { "DB_Input", "DI3_SYS_CycleStart" }, _projectDir);

        Assert.False(report.HasProposed);
        Assert.All(report.Entries, e => Assert.True(e.Exists));
    }

    [Fact]
    public void FormatText_And_Json_RenderStatuses()
    {
        var report = TagStatusRunner.Run(new[] { "DB_Input.Cycle_Start", "MadeUpTag" }, _projectDir);

        var text = TagStatusOutputFormatter.FormatText(report);
        Assert.Contains("DB_Input.Cycle_Start -> EXISTS (root: DB_Input)", text);
        Assert.Contains("MadeUpTag -> PROPOSED", text);
        Assert.Contains("SUMMARY: 2 name(s), 1 proposed", text);

        var json = TagStatusOutputFormatter.FormatJson(report);
        using var doc = System.Text.Json.JsonDocument.Parse(json); // asserts valid JSON
        Assert.Equal(2, doc.RootElement.GetProperty("names").GetArrayLength());
    }
}
