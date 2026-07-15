using Converter.Ir;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// S6 unlock (2026-07-15): patterns/ committed content gets the same round-trip guarantee as
/// ir/reference/ - these are real, sanitized excerpts (network-only, no BLOCK/SIDECAR wrapper) or
/// whole blocks, and nothing here should silently drift from what actually parses/reserializes
/// cleanly. Walks up from the test's own output directory to find the repo root (rather than a
/// fixed "../../../.." depth) so this doesn't break on a build-configuration or TFM change.
/// </summary>
public class PatternExampleTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "patterns")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root (no 'patterns' directory found above test output).");
    }

    [Theory]
    [InlineData("chained-permissive-enable/examples/network-2-link-conveyor.ir")]
    [InlineData("chained-permissive-enable/examples/network-7-metal-collection-conveyor.ir")]
    [InlineData("chained-permissive-enable/examples/network-8-overband-magnet.ir")]
    [InlineData("chained-permissive-enable/examples/network-12-drum-separator-chainhead.ir")]
    [InlineData("motor-dol/examples/calling-network.ir")]
    public void NetworkOnlyExample_RoundTripsLosslessly(string relativePath)
    {
        var path = Path.Combine(RepoRoot(), "patterns", relativePath);
        var text = File.ReadAllText(path);

        var network = IrParser.ParseNetworkOnly(text);
        var reserialized = IrSerializer.SerializeNetworkOnly(network);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void MotorDolBlock_RoundTripsLosslessly()
    {
        var path = Path.Combine(RepoRoot(), "patterns", "motor-dol", "MotorStarter.ir");
        var text = File.ReadAllText(path);

        var (block, sidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(block, sidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void MotorDolInstanceDb_RoundTripsLosslessly()
    {
        var path = Path.Combine(RepoRoot(), "patterns", "motor-dol", "examples", "MotorStarterInst8.ir");
        var text = File.ReadAllText(path);

        var db = DbIrParser.ParseDb(text);
        var reserialized = DbIrSerializer.Serialize(db);

        Assert.Equal(text, reserialized);
    }
}
