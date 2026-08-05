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
///
/// Array-of-UDT fix (FI-45 item 1, 2026-08-05): the walk stripped no array subscript and never
/// continued into the element type, so on any project whose per-instance data model is an array of
/// UDT — the ordinary way to express N identical vessels — every per-instance binding read as an
/// invented member. The gate fired at correct code, which is how an anti-laundering check gets
/// trained out of use. Fixtures below are invented; the real defect was found on live data that
/// never enters this repo.
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

        // The N-identical-vessels shape (FI-45 item 1): a DB member that is an array of UDT, whose
        // element type itself contains a further array of UDT, so a nested indexed walk is testable.
        WriteProjectFile("UDT_Sensor.ir", TypeIrSerializer.Serialize(
            new PlcTypeSource("0", "UDT_Sensor", null, new[]
            {
                new DbMember("Reading", "Real", Retain: false, StartValue: null),
            })));
        WriteProjectFile("UDT_Vessel.ir", TypeIrSerializer.Serialize(
            new PlcTypeSource("0", "UDT_Vessel", null, new[]
            {
                new DbMember("MaxNet", "Real", Retain: false, StartValue: null),
                new DbMember("Sensor", "Array[0..1] of \"UDT_Sensor\"", Retain: false, StartValue: null),
            })));
        WriteProjectFile("DB_Params.ir", DbIrSerializer.Serialize(
            new DbSource("0", "DB_Params", 3, InstanceOfName: null, Comment: "Per-vessel parameters.",
                Members: new[]
                {
                    new DbMember("Vessel", "Array[0..3] of \"UDT_Vessel\"", Retain: false, StartValue: null),
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

    // FI-45 item 1, the case that matters: an array of UDT is how N identical vessels are expressed,
    // so if this reports MEMBER-NOT-FOUND the gate accuses every per-instance binding on the project
    // of being invented — correct code failing the anti-laundering check.
    [Fact]
    public void Run_MemberThroughArrayOfUdt_IsExists_NotAFalseAccusationOfInvention()
    {
        var report = TagStatusRunner.Run(new[] { "DB_Params.Vessel[0].MaxNet" }, _projectDir);

        var entry = Assert.Single(report.Entries);
        Assert.Equal(TagStatusKind.Exists, entry.Status);
        Assert.False(report.HasBlocking);
    }

    // The walk is recursive, not one-subscript-deep: an array of UDT inside an array of UDT.
    [Fact]
    public void Run_NestedArrayOfUdtPath_ResolvesAtEveryDepth()
    {
        var report = TagStatusRunner.Run(new[] { "DB_Params.Vessel[2].Sensor[1].Reading" }, _projectDir);

        Assert.Equal(TagStatusKind.Exists, Assert.Single(report.Entries).Status);
        Assert.False(report.HasBlocking);
    }

    // An index past the declared bounds is a real defect nobody used to get told about. It gets its
    // own status because the member IS real — "member not found" would send the engineer hunting the
    // wrong thing.
    [Fact]
    public void Run_SubscriptOutsideDeclaredBounds_IsIndexOutOfRange_AndBlocks()
    {
        var report = TagStatusRunner.Run(
            new[] { "DB_Params.Vessel[7].MaxNet", "DB_Params.Vessel[0].Sensor[5].Reading" }, _projectDir);

        var byName = report.Entries.ToDictionary(e => e.Name);
        var outer = byName["DB_Params.Vessel[7].MaxNet"];
        Assert.Equal(TagStatusKind.IndexOutOfRange, outer.Status);
        Assert.True(outer.IsBlocking);
        Assert.Contains("Vessel[7]", outer.Detail);
        Assert.Contains("0..3", outer.Detail);

        // Bounds are checked at every depth, not just the first subscript.
        Assert.Equal(TagStatusKind.IndexOutOfRange, byName["DB_Params.Vessel[0].Sensor[5].Reading"].Status);
        Assert.True(report.HasBlocking);
    }

    // The deliberate reading of the unindexed form: a type-level question ("does every Vessel carry
    // MaxNet?"), which is how a spec or binding table names a member common to all instances. It must
    // not be MEMBER-NOT-FOUND — that would call something real invented.
    [Fact]
    public void Run_UnindexedPathThroughArrayOfUdt_IsExists_ReadAsTypeLevelQuestion()
    {
        var report = TagStatusRunner.Run(new[] { "DB_Params.Vessel.MaxNet" }, _projectDir);

        Assert.Equal(TagStatusKind.Exists, Assert.Single(report.Entries).Status);
        Assert.False(report.HasBlocking);
    }

    // The false positive must not be fixed by weakening the true one: an invented member inside the
    // array's element type still fails, at every depth.
    [Fact]
    public void Run_InventedMemberInsideArrayElementType_IsStillMemberNotFound()
    {
        var report = TagStatusRunner.Run(
            new[] { "DB_Params.Vessel[0].TotallyInvented", "DB_Params.Vessel[0].Sensor[1].AlsoInvented" },
            _projectDir);

        Assert.All(report.Entries, e => Assert.Equal(TagStatusKind.MemberNotFound, e.Status));
        Assert.True(report.HasBlocking);
    }

    // Guards the same true positive one level below an elementary type: a Bool has no members, so a
    // further component is invented, not merely unverifiable.
    [Fact]
    public void Run_MemberBelowAnElementaryType_IsStillMemberNotFound()
    {
        var report = TagStatusRunner.Run(new[] { "DB_Input.Cycle_Start.Nope" }, _projectDir);

        var entry = Assert.Single(report.Entries);
        Assert.Equal(TagStatusKind.MemberNotFound, entry.Status);
        Assert.True(entry.IsBlocking);
    }

    // A subscript the tool cannot evaluate (a symbolic/variable index) is never guessed at — the
    // bounds check only ever speaks when it is provably right, or the crying-wolf failure returns.
    [Fact]
    public void Run_SymbolicSubscript_IsNotFlagged()
    {
        var report = TagStatusRunner.Run(new[] { "DB_Params.Vessel[#i].MaxNet" }, _projectDir);

        Assert.Equal(TagStatusKind.Exists, Assert.Single(report.Entries).Status);
        Assert.False(report.HasBlocking);
    }

    [Fact]
    public void FormatText_And_Json_RenderIndexOutOfRange()
    {
        var report = TagStatusRunner.Run(new[] { "DB_Params.Vessel[7].MaxNet" }, _projectDir);

        var text = TagStatusOutputFormatter.FormatText(report);
        Assert.Contains("DB_Params.Vessel[7].MaxNet -> INDEX-OUT-OF-RANGE (root: DB_Params)", text);
        Assert.Contains("SUMMARY: 1 name(s), 0 proposed, 0 member-not-found, 0 member-unchecked, 1 index-out-of-range", text);

        var json = TagStatusOutputFormatter.FormatJson(report);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var entry = doc.RootElement.GetProperty("names")[0];
        Assert.Equal("INDEX-OUT-OF-RANGE", entry.GetProperty("status").GetString());
        Assert.True(entry.GetProperty("blocking").GetBoolean());
        Assert.Contains("0..3", entry.GetProperty("detail").GetString());
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
