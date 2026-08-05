using Converter.Ir;
using Converter.SimaticMl;
using Converter.TagStatus;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-24 (2026-07-18): `converter tagstatus` — mechanical classification of tag names against a
/// project export, reusing ProjectIndex + AccessNode.FromDottedPath (the same primitive preflight
/// uses, so the two never disagree). Builds a real on-disk project dir the way the command runs.
///
/// Member-blindness fix (2026-08-05): classification used to stop at the DB root, so
/// `DB_Input.CompletelyMadeUp` reported EXISTS — the anti-laundering gate for hard rule 3 blessed
/// invented members, and `gen-block-new` gates its run on that result. Member resolution now reuses
/// TagTypeRegistry, with MEMBER-UNCHECKED for namespaces the export genuinely cannot enumerate so
/// the fix cannot manufacture false gaps in the other direction.
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

        // An instance-DB stub with no member tree — what `create-instance-db` leaves behind before a
        // re-export. Its members live in the FB, so they are not enumerable from here.
        WriteProjectFile("iDB_Stub.ir", DbIrSerializer.Serialize(
            new DbSource("0", "iDB_Stub", 2, InstanceOfName: "FB_Motor", Comment: null,
                Members: Array.Empty<DbMember>())));
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
    public void Run_ClassifiesRealDbMemberAndBareTagAsExists_MadeUpRootAsProposed()
    {
        var report = TagStatusRunner.Run(
            new[] { "DB_Input.Cycle_Start", "DI3_SYS_CycleStart", "DB_Input", "MadeUpTag" }, _projectDir);

        var byName = report.Entries.ToDictionary(e => e.Name);
        Assert.Equal(TagStatusKind.Exists, byName["DB_Input.Cycle_Start"].Status);   // member really is there
        Assert.Equal("DB_Input", byName["DB_Input.Cycle_Start"].Root);
        Assert.Equal(TagStatusKind.Exists, byName["DI3_SYS_CycleStart"].Status);     // bare tag-table tag
        Assert.Equal(TagStatusKind.Exists, byName["DB_Input"].Status);               // the DB name itself
        Assert.Equal(TagStatusKind.Proposed, byName["MadeUpTag"].Status);            // root not in the export
        Assert.True(report.HasBlocking);
    }

    // The bug this fix exists for: an invented member on a REAL DB used to classify EXISTS on the
    // strength of its root, so the hard-rule-3 gate passed it.
    [Fact]
    public void Run_InventedMemberOnRealDb_IsMemberNotFound_AndBlocks()
    {
        var report = TagStatusRunner.Run(
            new[] { "DB_Input.CompletelyMadeUpMember", "DB_Input.Cycle_Start" }, _projectDir);

        var byName = report.Entries.ToDictionary(e => e.Name);
        Assert.Equal(TagStatusKind.MemberNotFound, byName["DB_Input.CompletelyMadeUpMember"].Status);
        Assert.True(byName["DB_Input.CompletelyMadeUpMember"].IsBlocking);
        Assert.Equal(TagStatusKind.Exists, byName["DB_Input.Cycle_Start"].Status);
        Assert.True(report.HasBlocking);
    }

    // The guard against fixing one false answer with another: a DB whose member tree the export does
    // not carry cannot be checked, so its members are neither blessed nor failed.
    [Fact]
    public void Run_MemberOfStubInstanceDb_IsMemberUnchecked_AndDoesNotBlock()
    {
        var report = TagStatusRunner.Run(new[] { "iDB_Stub.IO.Run" }, _projectDir);

        var entry = Assert.Single(report.Entries);
        Assert.Equal(TagStatusKind.MemberUnchecked, entry.Status);
        Assert.False(entry.IsBlocking);
        Assert.False(report.HasBlocking);
    }

    // --roots-only restores the pre-fix behaviour for the Design stage, which classifies at root
    // level because it designs *against* gaps rather than coding against them.
    [Fact]
    public void Run_RootsOnly_ClassifiesInventedMemberAsExists()
    {
        var report = TagStatusRunner.Run(
            new[] { "DB_Input.CompletelyMadeUpMember" }, _projectDir, rootsOnly: true);

        Assert.Equal(TagStatusKind.Exists, Assert.Single(report.Entries).Status);
        Assert.False(report.HasBlocking);
    }

    // Locks the whole-name-first logic: a bare tag-table tag whose own name contains a dot
    // (Clock_0.5Hz) must resolve as EXISTS, not be mis-split into a "Clock_0" root that misses.
    [Fact]
    public void Run_DottedTagTableName_ResolvesWholeName_NotMisSplitRoot()
    {
        var report = TagStatusRunner.Run(new[] { "Clock_0.5Hz" }, _projectDir);

        var entry = Assert.Single(report.Entries);
        Assert.Equal(TagStatusKind.Exists, entry.Status);
        Assert.False(report.HasBlocking);
    }

    [Fact]
    public void Run_AllExisting_HasBlockingFalse()
    {
        var report = TagStatusRunner.Run(new[] { "DB_Input", "DI3_SYS_CycleStart" }, _projectDir);

        Assert.False(report.HasBlocking);
        Assert.All(report.Entries, e => Assert.True(e.Exists));
    }

    [Fact]
    public void FormatText_And_Json_RenderStatuses()
    {
        var report = TagStatusRunner.Run(
            new[] { "DB_Input.Cycle_Start", "MadeUpTag", "DB_Input.CompletelyMadeUpMember" }, _projectDir);

        var text = TagStatusOutputFormatter.FormatText(report);
        Assert.Contains("DB_Input.Cycle_Start -> EXISTS (root: DB_Input)", text);
        Assert.Contains("MadeUpTag -> PROPOSED", text);
        Assert.Contains("DB_Input.CompletelyMadeUpMember -> MEMBER-NOT-FOUND (root: DB_Input)", text);
        Assert.Contains("SUMMARY: 3 name(s), 1 proposed, 1 member-not-found, 0 member-unchecked", text);

        var json = TagStatusOutputFormatter.FormatJson(report);
        using var doc = System.Text.Json.JsonDocument.Parse(json); // asserts valid JSON
        var names = doc.RootElement.GetProperty("names");
        Assert.Equal(3, names.GetArrayLength());
        Assert.Equal("MEMBER-NOT-FOUND", names[2].GetProperty("status").GetString());
        Assert.True(names[2].GetProperty("blocking").GetBoolean());
    }
}
