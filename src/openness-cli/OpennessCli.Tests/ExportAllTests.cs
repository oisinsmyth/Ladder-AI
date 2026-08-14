using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OpennessCli.Cli;
using OpennessCli.Model;
using Xunit;

namespace OpennessCli.Tests;

// FI-70. `drift-check` compares ir/*.ir against simatic-ml/*.xml and NEITHER SIDE IS THE CONTROLLER,
// so a file edited on disk and never imported — or a block changed in TIA and never exported — was
// invisible to every automated check. `export-all` produces the thing to compare against.
//
// What these tests pin is not "it exports": it is that the report CANNOT let a partial dump read as
// a whole one. The directory is handed to `converter drift-check --complete`, which treats a missing
// file as "this block is not in the controller" — so every hole has to be named, and a hole with a
// perfectly good reason (a safety refusal) still has to be named, or a correct refusal becomes a
// false finding about the controller.
public class ExportAllTests
{
    private const string OutDir = @"C:\dump";

    private static BlockInfo Block(string name, bool safety = false) =>
        new(name, BlockType.FB, 1, safety ? "F_LAD" : "LAD", IsSafety: safety, Path: "PLC_1/Program blocks", IsConsistent: true);

    private static ExportAllPlanner.Plan Plan(
        IEnumerable<BlockInfo>? blocks = null,
        IEnumerable<PlcTypeInfo>? types = null,
        IEnumerable<TagTableInfo>? tagTables = null,
        bool includeTagTables = false) =>
        ExportAllPlanner.Build(
            (blocks ?? Array.Empty<BlockInfo>()).ToList(),
            (types ?? Array.Empty<PlcTypeInfo>()).ToList(),
            (tagTables ?? Array.Empty<TagTableInfo>()).ToList(),
            OutDir,
            includeTagTables);

    [Fact]
    public void Plan_CoversBlocksAndTypes_AndTagTablesOnlyWhenAskedFor()
    {
        var withoutTables = Plan(
            blocks: new[] { Block("FB_Valve") },
            types: new[] { new PlcTypeInfo("UDT_Motor", "PLC_1") },
            tagTables: new[] { new TagTableInfo("Default tag table", "PLC_1") });

        Assert.Equal(2, withoutTables.Items.Count);
        Assert.DoesNotContain(withoutTables.Items, i => i.Kind == ExportAllPlanner.ItemKind.TagTable);

        var withTables = Plan(
            blocks: new[] { Block("FB_Valve") },
            types: new[] { new PlcTypeInfo("UDT_Motor", "PLC_1") },
            tagTables: new[] { new TagTableInfo("Default tag table", "PLC_1") },
            includeTagTables: true);

        Assert.Equal(3, withTables.Items.Count);
    }

    // Types were reachable inside the gateway since FI-62 but were never on the interface. A bulk
    // export that silently omitted every UDT is exactly the partial dump this feature exists to stop
    // being mistaken for a complete one.
    [Fact]
    public void Plan_ExportsTypes_NotJustBlocks()
    {
        var plan = Plan(types: new[] { new PlcTypeInfo("UDT_Motor", "PLC_1"), new PlcTypeInfo("UDT_Valve", "PLC_1") });

        Assert.Equal(2, plan.ToExport.Count());
        Assert.All(plan.ToExport, i => Assert.Equal(ExportAllPlanner.ItemKind.Type, i.Kind));
    }

    [Fact]
    public void Plan_RefusesSafetyContent_AndNamesItRatherThanOmittingIt()
    {
        var plan = Plan(blocks: new[] { Block("FB_Pump"), Block("FB_EStopChain", safety: true) });

        // Both are in the plan. The safety block is REFUSED, not absent — an absent entry would be
        // read downstream as "this block is not in the controller".
        Assert.Equal(2, plan.Items.Count);
        var refused = Assert.Single(plan.Refused);
        Assert.Equal("FB_EStopChain", refused.Name);
        Assert.Contains("safety", refused.RefusedReason);
        Assert.Null(refused.OutPath);
    }

    // drift-check pairs by BASENAME. Two objects of different kinds sharing a name would both write
    // "<name>.xml": one silently overwrites the other and the comparison then runs against the wrong
    // object, yielding a false MATCH or a false DRIFT with nothing to show which.
    [Fact]
    public void Plan_RefusesABasenameCollisionRatherThanSilentlyOverwriting()
    {
        var plan = Plan(
            blocks: new[] { Block("Motor") },
            types: new[] { new PlcTypeInfo("Motor", "PLC_1") });

        Assert.Single(plan.ToExport);
        var refused = Assert.Single(plan.Refused);
        Assert.Equal(ExportAllPlanner.ItemKind.Type, refused.Kind);
        Assert.Contains("collides", refused.RefusedReason);
        Assert.Contains("basename", refused.RefusedReason);
    }

    [Fact]
    public void Plan_WritesEveryExportIntoTheOutDirAsDotXml()
    {
        var plan = Plan(blocks: new[] { Block("FB_Valve") });

        Assert.Equal(System.IO.Path.Combine(OutDir, "FB_Valve.xml"), plan.ToExport.Single().OutPath);
    }

    private static ExportAllResult Result(params ExportAllEntry[] entries) => new(OutDir, entries);

    private static ExportAllResult ResultWithTagTables(params ExportAllEntry[] entries) =>
        new(OutDir, entries, TagTablesIncluded: true);

    private static ExportAllEntry Entry(string name, ExportAllOutcome outcome, string? detail = null) =>
        new(name, "Block", "PLC_1/Program blocks", outcome == ExportAllOutcome.Refused ? null : @"C:\dump\x.xml", outcome, detail);

    // The property the exit code and the report both turn on: a refusal is legitimate AND still
    // means the directory is not the whole project.
    [Fact]
    public void Result_IsNotComplete_WhenAnythingWasRefused_EvenThoughTheRefusalWasCorrect()
    {
        Assert.True(Result(Entry("A", ExportAllOutcome.Exported)).IsComplete);
        Assert.False(Result(
            Entry("A", ExportAllOutcome.Exported),
            Entry("F", ExportAllOutcome.Refused, "classifies as safety content")).IsComplete);
        Assert.False(Result(
            Entry("A", ExportAllOutcome.Exported),
            Entry("B", ExportAllOutcome.Failed, "boom")).IsComplete);
    }

    [Fact]
    public void Table_StatesTheConclusionInWords_NotJustCounts()
    {
        var complete = OutputFormatter.FormatExportAllTable(Result(Entry("A", ExportAllOutcome.Exported)));
        Assert.Contains("SUMMARY: 1 exported, 0 refused, 0 failed", complete);
        Assert.Contains("COMPLETE:", complete);
        Assert.Contains("--complete", complete); // names the command it has earned the right to feed

        var partial = OutputFormatter.FormatExportAllTable(Result(
            Entry("A", ExportAllOutcome.Exported),
            Entry("F_Chain", ExportAllOutcome.Refused, "classifies as safety content")));

        Assert.Contains("REFUSED: F_Chain", partial);
        Assert.Contains("INCOMPLETE:", partial);
        // The consequence has to be stated, not left to be inferred from a count.
        Assert.Contains("not in the controller", partial);
        Assert.DoesNotContain("COMPLETE: every block", partial);
    }

    // ---- the ADVICE, which was narrower than its wording (2026-08-14) --------------------------

    /// <summary>
    /// *** A COMPLETE DUMP WITHOUT --tagtables MUST NOT RECOMMEND --complete. ***
    ///
    /// The old report said "COMPLETE: every block and type in the project was exported" - accurate,
    /// and appropriately scoped - and then recommended `drift-check --complete`, WHOSE ENTIRE MEANING
    /// IS "treat this directory as everything". A reader followed it literally and got two spurious
    /// SKIPPED rows for tag tables that were never exported: findings about the dump, presented as
    /// findings about the controller.
    ///
    /// <para>ADVICE IS A CLAIM. The recommendation dropped the scope the sentence above it had been
    /// careful to state, at exactly the moment the reader was deciding what to do next.</para>
    /// </summary>
    [Fact]
    public void Table_WithoutTagTables_NamesTheOmission_AndWithholdsTheCompleteRecommendation()
    {
        var text = OutputFormatter.FormatExportAllTable(Result(Entry("A", ExportAllOutcome.Exported)));

        // The accurate half is kept. It was never the problem.
        Assert.Contains("COMPLETE: every block and type in the project was exported.", text);

        Assert.Contains("TAG TABLES ARE NOT IN THIS DIRECTORY", text);
        Assert.Contains("must NOT be handed to drift-check --complete", text);
        Assert.Contains("Re-run with --tagtables", text);

        // And the recommendation itself is gone - naming the hazard while still printing the command
        // underneath it would be a warning, and a warning is not a gate.
        Assert.DoesNotContain("Safe to compare against", text);
    }

    /// <summary>
    /// *** THE CONTROL. *** With --tagtables the directory really is the whole project, and the
    /// recommendation must come back - otherwise this is a report that never recommends anything,
    /// which passes the test above for the wrong reason and helps nobody.
    /// </summary>
    [Fact]
    public void Table_WithTagTables_DoesRecommendComplete()
    {
        var text = OutputFormatter.FormatExportAllTable(ResultWithTagTables(Entry("A", ExportAllOutcome.Exported)));

        Assert.Contains("every block, type and tag table", text);
        Assert.Contains("Safe to compare against", text);
        Assert.Contains("--complete", text);
        Assert.DoesNotContain("TAG TABLES ARE NOT IN THIS DIRECTORY", text);
    }

    /// <summary>
    /// The machine consumer gets the same distinction as the human one. `complete` answers "was
    /// everything ATTEMPTED produced?" and is silent about tag tables; `safeForDriftCheckComplete`
    /// answers the question a caller reaching for `--complete` is actually asking.
    /// </summary>
    [Fact]
    public void Json_SeparatesCompleteFromSafeForDriftCheckComplete()
    {
        var without = OutputFormatter.FormatExportAllJson(Result(Entry("A", ExportAllOutcome.Exported)));
        Assert.Contains("\"complete\": true", without);
        Assert.Contains("\"tagTablesIncluded\": false", without);
        Assert.Contains("\"safeForDriftCheckComplete\": false", without);

        var with = OutputFormatter.FormatExportAllJson(ResultWithTagTables(Entry("A", ExportAllOutcome.Exported)));
        Assert.Contains("\"safeForDriftCheckComplete\": true", with);
    }

    [Fact]
    public void Table_LeadsWithFailures_SoTheyAreNotBuriedUnderNinetySuccesses()
    {
        var text = OutputFormatter.FormatExportAllTable(Result(
            Entry("A", ExportAllOutcome.Exported),
            Entry("B", ExportAllOutcome.Refused, "safety"),
            Entry("C", ExportAllOutcome.Failed, "ExportProducedNoFileException: nothing appeared")));

        var lines = text.Split('\n');
        var failedAt = Array.FindIndex(lines, l => l.StartsWith("FAILED:"));
        var refusedAt = Array.FindIndex(lines, l => l.StartsWith("REFUSED:"));
        var summaryAt = Array.FindIndex(lines, l => l.StartsWith("SUMMARY:"));

        Assert.True(failedAt >= 0 && failedAt < refusedAt && refusedAt < summaryAt);
        Assert.Contains("ExportProducedNoFileException", text);
    }

    [Fact]
    public void Json_CarriesCompletenessAndEveryEntry()
    {
        var json = OutputFormatter.FormatExportAllJson(Result(
            Entry("A", ExportAllOutcome.Exported),
            Entry("F_Chain", ExportAllOutcome.Refused, "classifies as safety content")));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("complete").GetBoolean());
        Assert.Equal(1, root.GetProperty("exported").GetInt32());
        Assert.Equal(1, root.GetProperty("refused").GetInt32());
        Assert.Equal(2, root.GetProperty("entries").GetArrayLength());
        Assert.Equal("Refused", root.GetProperty("entries")[1].GetProperty("outcome").GetString());
    }

    [Fact]
    public void Parse_RequiresProjectAndOutDir_AndDefaultsTagTablesOff()
    {
        var ok = ArgumentParser.Parse(new[] { "export-all", @"C:\proj\p.ap20", "--out", @"C:\dump" });
        var success = Assert.IsType<ParseResult.ExportAllSuccess>(ok);
        Assert.Equal(@"C:\dump", success.Options.OutDir);
        Assert.False(success.Options.IncludeTagTables);
        Assert.False(success.Options.Json);

        Assert.IsType<ParseResult.Failure>(
            ArgumentParser.Parse(new[] { "export-all", @"C:\proj\p.ap20" }));
        Assert.IsType<ParseResult.Failure>(
            ArgumentParser.Parse(new[] { "export-all", "--out", @"C:\dump" }));
    }

    [Fact]
    public void Parse_AcceptsTagTablesDeviceAndJson()
    {
        var ok = ArgumentParser.Parse(new[]
        {
            "export-all", @"C:\proj\p.ap20", "--out", @"C:\dump", "--tagtables", "--device", "PLC_1", "--json",
        });

        var success = Assert.IsType<ParseResult.ExportAllSuccess>(ok);
        Assert.True(success.Options.IncludeTagTables);
        Assert.True(success.Options.Json);
        Assert.Equal("PLC_1", success.Options.Device);
    }
}
