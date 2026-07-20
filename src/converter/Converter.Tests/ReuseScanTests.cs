using System.Text.Json;
using Converter.Ir;
using Converter.ReuseScan;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-29 (2026-07-20): `converter reuse-scan` — a digest-backed corpus query surfacing candidate
/// blocks that reference a tag and/or implement a statement kind, for the reuse-first pass. Builds a
/// real on-disk project dir the way the command runs (ReuseScanRunner reads a directory of .ir).
/// </summary>
public class ReuseScanTests : IDisposable
{
    private readonly string _projectDir;

    public ReuseScanTests()
    {
        _projectDir = Path.Combine(Path.GetTempPath(), $"reusescan-proj-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_projectDir);

        // FB_Alpha: network 1 drives DQ5_DIS_Run (coil); network 2 is a timeout timer.
        WriteBlock("FB_Alpha.ir", new IrBlock(
            "0", "FB", "FB_Alpha", 1, "LAD", null, new[]
            {
                new IrNetwork(1, "Discharge run", new[] { new CoilAssignment("DQ5_DIS_Run", new Expr.TagRef("Enable")) }),
                new IrNetwork(2, "Discharge timeout", Array.Empty<CoilAssignment>(),
                    Timers: new[] { new TimerBinding("T1", new Expr.TagRef("Start"), new Expr.TagRef("DB_Settings.Delay")) }),
            }));

        // FB_Beta: one coil network, no timer, unrelated tags.
        WriteBlock("FB_Beta.ir", new IrBlock(
            "0", "FB", "FB_Beta", 2, "LAD", null, new[]
            {
                new IrNetwork(1, "Other", new[] { new CoilAssignment("Other_Out", new Expr.TagRef("Other_In")) }),
            }));
    }

    private void WriteBlock(string fileName, IrBlock block)
    {
        var sidecars = block.Networks
            .Select(n => new NetworkSidecar(n.Number, n.Number.ToString(), Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();
        File.WriteAllText(Path.Combine(_projectDir, fileName), IrSerializer.SerializeBlock(block, sidecars));
    }

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
    public void Run_ByTag_MatchesOnlyTheBlockReferencingIt()
    {
        var report = ReuseScanRunner.Run(_projectDir, new[] { "DQ5_DIS_Run" }, Array.Empty<string>());

        var match = Assert.Single(report.Matches);
        Assert.Equal("FB_Alpha", match.BlockName);
        Assert.Contains("DQ5_DIS_Run", match.MatchedTags);
        Assert.True(report.HasMatches);
    }

    [Fact]
    public void Run_ByKind_MatchesTheNetworkWithThatStatement()
    {
        var report = ReuseScanRunner.Run(_projectDir, Array.Empty<string>(), new[] { "timer" });

        var match = Assert.Single(report.Matches);
        Assert.Equal("FB_Alpha", match.BlockName);
        var network = Assert.Single(match.MatchedNetworks);
        Assert.Equal(2, network.Number);
        Assert.Contains("timer", network.MatchedKinds);
    }

    [Fact]
    public void Run_TagAndKind_RequiresBothGroups()
    {
        // Both satisfied by FB_Alpha (references the tag AND has a timer network).
        var both = ReuseScanRunner.Run(_projectDir, new[] { "DQ5_DIS_Run" }, new[] { "timer" });
        Assert.Single(both.Matches);
        Assert.Equal("FB_Alpha", both.Matches[0].BlockName);

        // Tag present (FB_Beta? no) — a tag that exists but on a block with no timer must NOT match a
        // tag+kind query: Other_Out is on FB_Beta which has no timer.
        var mismatched = ReuseScanRunner.Run(_projectDir, new[] { "Other_Out" }, new[] { "timer" });
        Assert.Empty(mismatched.Matches);
        Assert.False(mismatched.HasMatches);
    }

    [Fact]
    public void Run_NoMatch_HasMatchesFalse()
    {
        var report = ReuseScanRunner.Run(_projectDir, new[] { "NoSuchTag" }, Array.Empty<string>());

        Assert.Empty(report.Matches);
        Assert.False(report.HasMatches);
    }

    [Fact]
    public void FormatText_And_Json_RenderMatches()
    {
        var report = ReuseScanRunner.Run(_projectDir, new[] { "DQ5_DIS_Run" }, new[] { "timer" });

        var text = ReuseScanOutputFormatter.FormatText(report);
        Assert.Contains("MATCH: FB_Alpha (FB)", text);
        Assert.Contains("tags: DQ5_DIS_Run", text);
        Assert.Contains("network 2 \"Discharge timeout\" — timer", text);
        Assert.Contains("SUMMARY: 1 block(s) matched", text);

        var json = ReuseScanOutputFormatter.FormatJson(report);
        using var doc = JsonDocument.Parse(json); // asserts valid JSON
        Assert.Equal(1, doc.RootElement.GetProperty("matches").GetArrayLength());
    }
}
