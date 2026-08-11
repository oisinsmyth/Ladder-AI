using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpennessCli.Cli;
using OpennessCli.Model;
using Xunit;

namespace OpennessCli.Tests;

// The bulk half of import. `import` takes N files as ONE kind, in the order given, and stops at the
// first failure — fine for the two or three files a change touches, useless for putting a whole
// program back, which is a mixed set in a dependency order nobody can supply from filenames.
//
// What these tests pin is not "it imports" (that needs a Portal): it is that nothing can go missing
// QUIETLY. A file that cannot be classified, or that would silently overwrite another because they
// share a basename, is a REJECTION with a reason — never a skip — because the failure mode of a
// restore is a project that comes back looking whole.
public class ImportAllTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "import-all-tests-" + Guid.NewGuid().ToString("N"));

    public ImportAllTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // A temp directory that outlives the test run is not a test failure.
        }
    }

    private string WriteXml(string name, string rootElement)
    {
        var path = Path.Combine(_dir, name + ".xml");
        File.WriteAllText(path,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
            "<Document>\r\n" +
            "  <Engineering version=\"V20\" />\r\n" +
            $"  <{rootElement} ID=\"0\">\r\n" +
            $"  </{rootElement}>\r\n" +
            "</Document>\r\n");
        return path;
    }

    [Fact]
    public void Classifies_EachFile_FromItsRootElement()
    {
        WriteXml("UDT_Valve", "SW.Types.PlcStruct");
        WriteXml("Tags_Sample", "SW.Tags.PlcTagTable");
        WriteXml("FB_Valve", "SW.Blocks.FB");

        var plan = ImportAllPlanner.Build(new[] { _dir });

        Assert.Empty(plan.Rejections);
        Assert.Equal(ImportAllPlanner.ItemKind.Type, plan.Files.Single(f => f.Name == "UDT_Valve").Kind);
        Assert.Equal(ImportAllPlanner.ItemKind.TagTable, plan.Files.Single(f => f.Name == "Tags_Sample").Kind);
        Assert.Equal(ImportAllPlanner.ItemKind.Block, plan.Files.Single(f => f.Name == "FB_Valve").Kind);
    }

    // Tag tables before types before blocks: the dependency across those three is one-directional
    // over the whole corpus (a block may use a UDT; a UDT never uses a block), so it is worth
    // ordering rather than discovering by retry.
    [Fact]
    public void Orders_TagTables_ThenTypes_ThenBlocks()
    {
        WriteXml("FB_Valve", "SW.Blocks.FB");
        WriteXml("UDT_Valve", "SW.Types.PlcStruct");
        WriteXml("Tags_Sample", "SW.Tags.PlcTagTable");

        var kinds = ImportAllPlanner.Build(new[] { _dir }).Files.Select(f => f.Kind).ToList();

        Assert.Equal(
            new[] { ImportAllPlanner.ItemKind.TagTable, ImportAllPlanner.ItemKind.Type, ImportAllPlanner.ItemKind.Block },
            kinds);
    }

    // The one intra-phase dependency that is certain rather than probable: an instance DB cannot
    // resolve until the FB it instantiates exists.
    [Fact]
    public void Orders_InstanceDbs_AfterTheFbsTheyInstantiate()
    {
        WriteXml("iDB_Valve_Inlet", "SW.Blocks.InstanceDB");
        WriteXml("FB_Valve", "SW.Blocks.FB");

        var names = ImportAllPlanner.Build(new[] { _dir }).Files.Select(f => f.Name).ToList();

        Assert.Equal(new[] { "FB_Valve", "iDB_Valve_Inlet" }, names);
    }

    [Fact]
    public void UnclassifiableFile_IsRejectedWithAReason_NotSkipped()
    {
        var path = WriteXml("Screen_Main", "HmiScreen");

        var plan = ImportAllPlanner.Build(new[] { _dir });

        Assert.Empty(plan.Files);
        var rejection = Assert.Single(plan.Rejections);
        Assert.Equal(path, rejection.Path);
        Assert.Contains("HmiScreen", rejection.Reason);
        Assert.False(plan.IsUsable);
    }

    [Fact]
    public void UnreadableFile_IsRejected_RatherThanCrashingTheRun()
    {
        var path = Path.Combine(_dir, "Truncated.xml");
        File.WriteAllText(path, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<Document>\r\n  <SW.Blocks.FB");

        var plan = ImportAllPlanner.Build(new[] { _dir });

        Assert.Empty(plan.Files);
        Assert.Contains("could not be read as SimaticML", Assert.Single(plan.Rejections).Reason);
    }

    // Two files with the same basename would both import under the same name, so the later would
    // overwrite the earlier and WHICH ONE SURVIVED would depend on argument order. That is the exact
    // shape of a restore that looks complete and is not.
    [Fact]
    public void DuplicateBasename_IsRejected_RatherThanSilentlyOverwriting()
    {
        var first = WriteXml("FB_Valve", "SW.Blocks.FB");
        var otherDir = Path.Combine(_dir, "stale");
        Directory.CreateDirectory(otherDir);
        var second = Path.Combine(otherDir, "FB_Valve.xml");
        File.Copy(first, second);

        var plan = ImportAllPlanner.Build(new[] { first, second });

        Assert.Single(plan.Files);
        var rejection = Assert.Single(plan.Rejections);
        Assert.Equal(second, rejection.Path);
        Assert.Contains("already supplied by", rejection.Reason);
    }

    [Fact]
    public void MissingPath_IsRejected()
    {
        var plan = ImportAllPlanner.Build(new[] { Path.Combine(_dir, "not-there.xml") });

        Assert.Contains("no such file or directory", Assert.Single(plan.Rejections).Reason);
    }

    // Non-recursive on purpose: an export directory sits beside its own superseded variants often
    // enough that recursion would quietly import a stale copy of a block over a current one.
    [Fact]
    public void DirectoryExpansion_DoesNotRecurse()
    {
        WriteXml("FB_Valve", "SW.Blocks.FB");
        var sub = Path.Combine(_dir, "older-export");
        Directory.CreateDirectory(sub);
        File.Copy(Path.Combine(_dir, "FB_Valve.xml"), Path.Combine(sub, "FB_Stale.xml"));

        var plan = ImportAllPlanner.Build(new[] { _dir });

        Assert.Equal(new[] { "FB_Valve" }, plan.Files.Select(f => f.Name));
    }

    [Fact]
    public void EmptyDirectory_IsRejected_NotReportedAsANoOpSuccess()
    {
        var plan = ImportAllPlanner.Build(new[] { _dir });

        Assert.Contains("no .xml files", Assert.Single(plan.Rejections).Reason);
    }

    // A file that was never attempted has thrown nothing, so "nothing failed" is true and useless.
    // Completeness is about what is in the project, not about what errored.
    [Fact]
    public void Result_IsIncomplete_WhenAFileWasRejectedEvenThoughNothingFailed()
    {
        var result = new ImportAllResult("PLC_1", new[]
        {
            new ImportAllEntry("a.xml", "A", "Block", ImportAllOutcome.Imported, 1, null),
            new ImportAllEntry("b.xml", "B", "?", ImportAllOutcome.Rejected, 0, "unclassifiable"),
        }, Passes: 1);

        Assert.Equal(0, result.FailedCount);
        Assert.False(result.IsComplete);
        Assert.Contains("INCOMPLETE", OutputFormatter.FormatImportAllTable(result));
    }

    [Fact]
    public void Report_NamesEveryFileThatDidNotGoIn()
    {
        var result = new ImportAllResult("PLC_1", new[]
        {
            new ImportAllEntry("a.xml", "A", "Block", ImportAllOutcome.Imported, 1, null),
            new ImportAllEntry("b.xml", "B", "Block", ImportAllOutcome.Failed, 3, "EngineeringTargetInvocationException: Data type \"UDT_X\" is unknown"),
            new ImportAllEntry("c.xml", "C", "?", ImportAllOutcome.Rejected, 0, "unclassifiable"),
        }, Passes: 3);

        var text = OutputFormatter.FormatImportAllTable(result);

        Assert.Contains("FAILED:   B", text);
        Assert.Contains("UDT_X", text);
        Assert.Contains("REJECTED: c.xml", text);
        Assert.Contains("1 imported, 1 failed, 1 rejected", text);
    }

    // A file that only imported on a later pass is the visible evidence that the fixpoint did work
    // the ordering could not — worth reporting, and distinct from a failure.
    [Fact]
    public void Report_DistinguishesARetriedSuccessFromAFailure()
    {
        var result = new ImportAllResult("PLC_1", new[]
        {
            new ImportAllEntry("a.xml", "A", "Type", ImportAllOutcome.Imported, 2, null),
        }, Passes: 2);

        var text = OutputFormatter.FormatImportAllTable(result);

        Assert.Contains("RETRIED:  A  (imported on pass 2)", text);
        Assert.Contains("COMPLETE", text);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public void Json_CarriesCompletenessAndPerFileOutcomes()
    {
        var result = new ImportAllResult("PLC_1", new[]
        {
            new ImportAllEntry("a.xml", "A", "Block", ImportAllOutcome.Imported, 1, null),
            new ImportAllEntry("b.xml", "B", "?", ImportAllOutcome.Rejected, 0, "unclassifiable"),
        }, Passes: 1);

        using var doc = JsonDocument.Parse(OutputFormatter.FormatImportAllJson(result));
        var root = doc.RootElement;

        Assert.False(root.GetProperty("complete").GetBoolean());
        Assert.Equal(1, root.GetProperty("imported").GetInt32());
        Assert.Equal(1, root.GetProperty("rejected").GetInt32());
        Assert.Equal(2, root.GetProperty("entries").GetArrayLength());
    }

    // Compiled-and-wrong and never-examined are different failures, and the second is the one that
    // looks like success (FI-52). A single "failed" count would let it hide in the first.
    [Fact]
    public void CompileAll_CountsErrorsAndInconsistencySeparately()
    {
        var result = new CompileAllResult(new[]
        {
            new CompileAllEntry("A", "Block", CompileState.Warning, 0, 4, StillInconsistent: false, null),
            new CompileAllEntry("B", "Block", CompileState.Success, 0, 0, StillInconsistent: true, null),
        }, Passes: 1);

        Assert.Equal(0, result.WithErrorsCount);
        Assert.Equal(1, result.StillInconsistentCount);
        Assert.False(result.IsClean);

        var text = OutputFormatter.FormatCompileAllTable(result);
        Assert.Contains("UNCLEARED: B", text);
        Assert.Contains("NOT CLEAN", text);
    }

    // A project with pre-existing hardware warnings returns a non-Success state on a perfectly clean
    // block, so the verdict keys on ErrorCount and never on State.
    [Fact]
    public void CompileAll_IsClean_WhenTheOnlyNonSuccessStateIsWarnings()
    {
        var result = new CompileAllResult(new[]
        {
            new CompileAllEntry("A", "Type", CompileState.Warning, 0, 4, StillInconsistent: false, null),
        }, Passes: 1);

        Assert.True(result.IsClean);
        Assert.Contains("CLEAN", OutputFormatter.FormatCompileAllTable(result));
    }

    [Fact]
    public void CompileAll_Parse_TakesDeviceAndJson_AndRefusesAStrayArgument()
    {
        var ok = Assert.IsType<ParseResult.CompileAllSuccess>(
            ArgumentParser.Parse(new[] { "compile-all", @"C:\p.ap20", "--device", "PLC_1", "--json" }));
        Assert.Equal("PLC_1", ok.Options.Device);
        Assert.True(ok.Options.Json);

        Assert.IsType<ParseResult.Failure>(
            ArgumentParser.Parse(new[] { "compile-all", @"C:\p.ap20", "extra" }));
    }

    [Fact]
    public void Parse_RequiresGroupAndAtLeastOnePath()
    {
        Assert.IsType<ParseResult.Failure>(
            ArgumentParser.Parse(new[] { "import-all", @"C:\p.ap20", _dir }));

        Assert.IsType<ParseResult.Failure>(
            ArgumentParser.Parse(new[] { "import-all", @"C:\p.ap20", "--group", "PLC_1" }));

        var ok = Assert.IsType<ParseResult.ImportAllSuccess>(
            ArgumentParser.Parse(new[] { "import-all", @"C:\p.ap20", "--group", "PLC_1", _dir }));
        Assert.Equal("PLC_1", ok.Options.GroupPath);
        Assert.Single(ok.Options.Paths);
        Assert.False(ok.Options.DryRun);
    }

    [Fact]
    public void Parse_AcceptsDryRunAndJson()
    {
        var ok = Assert.IsType<ParseResult.ImportAllSuccess>(
            ArgumentParser.Parse(new[] { "import-all", @"C:\p.ap20", "--group", "PLC_1", "--dry-run", "--json", _dir }));

        Assert.True(ok.Options.DryRun);
        Assert.True(ok.Options.Json);
    }

    [Fact]
    public void Plan_Report_ListsTheImportOrderItWouldUse()
    {
        WriteXml("FB_Valve", "SW.Blocks.FB");
        WriteXml("UDT_Valve", "SW.Types.PlcStruct");

        var text = OutputFormatter.FormatImportAllPlanTable(ImportAllPlanner.Build(new[] { _dir }), "PLC_1");

        Assert.True(text.IndexOf("UDT_Valve", StringComparison.Ordinal) < text.IndexOf("FB_Valve", StringComparison.Ordinal));
        Assert.Contains("2 file(s) would be imported, 0 rejected", text);
    }
}
