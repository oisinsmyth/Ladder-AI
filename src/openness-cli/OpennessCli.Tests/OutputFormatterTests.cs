using System.Collections.Generic;
using System.Text.Json;
using OpennessCli.Cli;
using OpennessCli.Model;
using Xunit;

namespace OpennessCli.Tests;

public class OutputFormatterTests
{
    private static readonly IReadOnlyList<BlockInfo> SampleBlocks = new[]
    {
        new BlockInfo("Main", BlockType.OB, 1, "LAD", IsSafety: false, Path: "PLC_1/Program blocks", IsConsistent: true),
        new BlockInfo("FB_ConveyorControl", BlockType.FB, 1, "LAD", IsSafety: false, Path: "PLC_1/Program blocks", IsConsistent: true),
        new BlockInfo("FB_EStopChain", BlockType.FB, 2, "F_LAD", IsSafety: true, Path: "PLC_1/Program blocks/Safety", IsConsistent: true),
    };

    [Fact]
    public void FormatTable_EmptyList_ReturnsPlaceholder()
    {
        var table = OutputFormatter.FormatTable(System.Array.Empty<BlockInfo>());
        Assert.Equal("(no blocks found)", table);
    }

    [Fact]
    public void FormatTable_IncludesHeaderAndEveryBlockName()
    {
        var table = OutputFormatter.FormatTable(SampleBlocks);

        Assert.Contains("TYPE", table);
        Assert.Contains("NAME", table);
        Assert.Contains("SAFETY", table);
        Assert.Contains("Main", table);
        Assert.Contains("FB_ConveyorControl", table);
        Assert.Contains("FB_EStopChain", table);
    }

    [Fact]
    public void FormatTable_FlagsSafetyBlocksInOutput_ButNeverExposesTheirContents()
    {
        var table = OutputFormatter.FormatTable(SampleBlocks);

        // The safety block's name/number/language/path are legitimate metadata already
        // captured before formatting — the formatter's job is just to flag it, not to
        // add or omit anything beyond what EnumerateBlocks already decided was safe to expose.
        Assert.Contains("SAFETY", table);
        var lines = table.Split('\n');
        var safetyLine = System.Array.Find(lines, l => l.Contains("FB_EStopChain"));
        Assert.NotNull(safetyLine);
        Assert.Contains("SAFETY", safetyLine);
    }

    [Fact]
    public void FormatJson_RoundTripsAllFields()
    {
        var json = OutputFormatter.FormatJson(SampleBlocks);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(3, root.GetArrayLength());

        var safetyBlock = root[2];
        Assert.Equal("FB_EStopChain", safetyBlock.GetProperty("name").GetString());
        Assert.Equal("FB", safetyBlock.GetProperty("type").GetString());
        Assert.Equal(2, safetyBlock.GetProperty("number").GetInt32());
        Assert.Equal("F_LAD", safetyBlock.GetProperty("language").GetString());
        Assert.True(safetyBlock.GetProperty("safety").GetBoolean());
        Assert.Equal("PLC_1/Program blocks/Safety", safetyBlock.GetProperty("path").GetString());
    }

    [Fact]
    public void FormatJson_EmptyList_ReturnsEmptyArray()
    {
        var json = OutputFormatter.FormatJson(System.Array.Empty<BlockInfo>());
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    private static readonly CompileResult CleanCompile = new(
        CompileState.Success,
        ErrorCount: 0,
        WarningCount: 0,
        Messages: new[] { new CompileMessage(CompileState.Success, "Compiling finished (errors: 0; warnings: 0)", "PLC_1") });

    private static readonly CompileResult FailedCompile = new(
        CompileState.Error,
        ErrorCount: 1,
        WarningCount: 2,
        Messages: new[]
        {
            new CompileMessage(CompileState.Error, "Tag 'Foo' does not exist", "PlantAutoControl/Network 3"),
            new CompileMessage(CompileState.Warning, "Unused temp variable", "PlantAutoControl/Network 1"),
        });

    [Fact]
    public void FormatCompileTable_ShowsStateAndCounts()
    {
        var table = OutputFormatter.FormatCompileTable(CleanCompile);

        Assert.Contains("STATE: Success", table);
        Assert.Contains("ERRORS: 0", table);
        Assert.Contains("WARNINGS: 0", table);
    }

    [Fact]
    public void FormatCompileTable_ListsEachMessageWithPathAndDescription()
    {
        var table = OutputFormatter.FormatCompileTable(FailedCompile);

        Assert.Contains("PlantAutoControl/Network 3", table);
        Assert.Contains("Tag 'Foo' does not exist", table);
        Assert.Contains("PlantAutoControl/Network 1", table);
        Assert.Contains("Unused temp variable", table);
    }

    [Fact]
    public void FormatCompileJson_RoundTripsStateErrorsWarningsAndMessages()
    {
        var json = OutputFormatter.FormatCompileJson(FailedCompile);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("Error", root.GetProperty("state").GetString());
        Assert.Equal(1, root.GetProperty("errors").GetInt32());
        Assert.Equal(2, root.GetProperty("warnings").GetInt32());

        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("Tag 'Foo' does not exist", messages[0].GetProperty("description").GetString());
        Assert.Equal("PlantAutoControl/Network 3", messages[0].GetProperty("path").GetString());
    }

    private static readonly SanityCheckResult HealthyResult = new(
        TotalBlocks: 42,
        InconsistentBlocks: System.Array.Empty<BlockConsistencyIssue>(),
        DeviceCompiles: new[] { new DeviceCompileSummary("S7-1200 G2 station_2/JOB9002_PLC", CleanCompile) },
        TotalTypes: 7,
        InconsistentTypes: System.Array.Empty<TypeConsistencyIssue>(),
        DuplicateNumbers: System.Array.Empty<DuplicateBlockNumber>());

    private static readonly SanityCheckResult UnhealthyResult = new(
        TotalBlocks: 42,
        InconsistentBlocks: new[]
        {
            new BlockConsistencyIssue("ControlMain", "S7-1200 G2 station_2/JOB9002_PLC/Control", "LAD"),
        },
        DeviceCompiles: new[] { new DeviceCompileSummary("S7-1200 G2 station_2/JOB9002_PLC", CleanCompile) },
        TotalTypes: 7,
        InconsistentTypes: System.Array.Empty<TypeConsistencyIssue>(),
        DuplicateNumbers: System.Array.Empty<DuplicateBlockNumber>());

    // FI-62: the exact live shape — every block consistent, every device compile clean, and one
    // UDT inconsistent. This MUST be unhealthy; before the fix it reported HEALTHY and the first
    // symptom was TIA refusing to export the type much later.
    private static readonly SanityCheckResult InconsistentTypeOnlyResult = new(
        TotalBlocks: 52,
        InconsistentBlocks: System.Array.Empty<BlockConsistencyIssue>(),
        DeviceCompiles: new[] { new DeviceCompileSummary("S7-1200 station_1/PLC_1", CleanCompile) },
        TotalTypes: 7,
        InconsistentTypes: new[] { new TypeConsistencyIssue("UDT_Drum", "S7-1200 station_1/PLC_1") },
        DuplicateNumbers: System.Array.Empty<DuplicateBlockNumber>());

    [Fact]
    public void SanityCheckResult_Unhealthy_WhenOnlyATypeIsInconsistent()
    {
        Assert.False(InconsistentTypeOnlyResult.IsHealthy);
    }

    [Fact]
    public void SanityCheckTable_AlwaysReportsTypes_SoZeroIsVisiblyChecked()
    {
        var text = OutputFormatter.FormatSanityCheckTable(HealthyResult);

        Assert.Contains("TYPES: 7  INCONSISTENT: 0", text);
        Assert.Contains("OVERALL: HEALTHY", text);
    }

    [Fact]
    public void SanityCheckTable_NamesTheInconsistentTypeAndHowToClearIt()
    {
        var text = OutputFormatter.FormatSanityCheckTable(InconsistentTypeOnlyResult);

        Assert.Contains("OVERALL: ISSUES FOUND", text);
        Assert.Contains("TYPES: 7  INCONSISTENT: 1", text);
        Assert.Contains("UDT_Drum", text);
        Assert.Contains("--type", text);
    }

    // FI-66. Re-reading is not a fixpoint. A block that NOTHING has compiled stays inconsistent
    // across every subsequent read — measured at three consecutive reads on a live job, while the
    // block never appeared in any import list. The output used to imply the opposite by naming no
    // remedy beside the block list, so an agent following the documented loop could re-read for ever.
    // The device compile below does not clear them; that is FI-52's finding.
    [Fact]
    public void SanityCheckTable_SaysRereadingWillNotClearInconsistentBlocks()
    {
        var text = OutputFormatter.FormatSanityCheckTable(UnhealthyResult);

        Assert.Contains("ControlMain", text);
        // The two things a reader must not have to infer: that re-running is futile, and what does work.
        Assert.Contains("does NOT clear these", text);
        Assert.Contains("--block", text);
    }

    [Fact]
    public void SanityCheckJson_CarriesTypeCounts()
    {
        var json = OutputFormatter.FormatSanityCheckJson(InconsistentTypeOnlyResult);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.False(root.GetProperty("healthy").GetBoolean());
        Assert.Equal(7, root.GetProperty("totalTypes").GetInt32());
        Assert.Equal(1, root.GetProperty("inconsistentTypes").GetArrayLength());
        Assert.Equal("UDT_Drum", root.GetProperty("inconsistentTypes")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void SanityCheckResult_Healthy_WhenNoIssuesAndAllCompilesSucceed()
    {
        Assert.True(HealthyResult.IsHealthy);
    }

    [Fact]
    public void SanityCheckResult_Unhealthy_WhenAnyBlockInconsistent()
    {
        Assert.False(UnhealthyResult.IsHealthy);
    }

    [Fact]
    public void SanityCheckResult_Unhealthy_WhenAnyDeviceCompileFails()
    {
        var result = new SanityCheckResult(
            TotalBlocks: 1,
            InconsistentBlocks: System.Array.Empty<BlockConsistencyIssue>(),
            DeviceCompiles: new[] { new DeviceCompileSummary("device", FailedCompile) },
            TotalTypes: 0,
            InconsistentTypes: System.Array.Empty<TypeConsistencyIssue>(),
            DuplicateNumbers: System.Array.Empty<DuplicateBlockNumber>());

        Assert.False(result.IsHealthy);
    }

    [Fact]
    public void FormatSanityCheckTable_Healthy_SaysHealthy()
    {
        var table = OutputFormatter.FormatSanityCheckTable(HealthyResult);

        Assert.Contains("OVERALL: HEALTHY", table);
        Assert.Contains("BLOCKS: 42", table);
        Assert.Contains("INCONSISTENT: 0", table);
    }

    [Fact]
    public void FormatSanityCheckTable_Unhealthy_ListsInconsistentBlocksAndDeviceCompiles()
    {
        var table = OutputFormatter.FormatSanityCheckTable(UnhealthyResult);

        Assert.Contains("OVERALL: ISSUES FOUND", table);
        Assert.Contains("ControlMain", table);
        Assert.Contains("S7-1200 G2 station_2/JOB9002_PLC/Control", table);
        Assert.Contains("S7-1200 G2 station_2/JOB9002_PLC: Success", table);
    }

    [Fact]
    public void FormatSanityCheckJson_RoundTripsHealthAndDetail()
    {
        var json = OutputFormatter.FormatSanityCheckJson(UnhealthyResult);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("healthy").GetBoolean());
        Assert.Equal(42, root.GetProperty("totalBlocks").GetInt32());

        var inconsistent = root.GetProperty("inconsistentBlocks");
        Assert.Equal(1, inconsistent.GetArrayLength());
        Assert.Equal("ControlMain", inconsistent[0].GetProperty("name").GetString());

        var deviceCompiles = root.GetProperty("deviceCompiles");
        Assert.Equal(1, deviceCompiles.GetArrayLength());
        Assert.Equal("Success", deviceCompiles[0].GetProperty("state").GetString());
    }
}
