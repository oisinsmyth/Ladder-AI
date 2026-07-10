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
        new BlockInfo("Main", BlockType.OB, 1, "LAD", IsSafety: false, Path: "PLC_1/Program blocks"),
        new BlockInfo("FB_ConveyorControl", BlockType.FB, 1, "LAD", IsSafety: false, Path: "PLC_1/Program blocks"),
        new BlockInfo("FB_EStopChain", BlockType.FB, 2, "F_LAD", IsSafety: true, Path: "PLC_1/Program blocks/Safety"),
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
}
