using System.Text.Json;
using Converter.Ir;
using Converter.IrHash;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-17 (2026-07-20): `converter ir-hash` — a stable content hash of a block's readable IR, the key
/// an explanation sidecar (docs/notes/explanation-sidecars.md) is derived-from / validated against.
/// Same test shape as DigestTests: build the model in memory, serialize with the real serializers,
/// hash from a real temp file (IrHashRunner reads by path).
/// </summary>
public class IrHashTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string WriteTempIrFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"irhash-test-{Guid.NewGuid():N}.ir");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Best-effort cleanup only - not the point of the test.
            }
        }
    }

    private static IrBlock BlockWithNetworkLogic(string coilTarget) => new(
        "0", "FB", "FB_Demo", 7, "LAD", "Header comment.", new[]
        {
            new IrNetwork(1, "Run seal-in", new[]
            {
                new CoilAssignment(coilTarget, new Expr.TagRef("Clock_0.5Hz")),
            }),
        },
        StaticMembers: new[] { new DbMember("RunTimer", "TON_TIME", Retain: false, StartValue: null) },
        Title: "Demo block");

    private static string SerializeWithEmptySidecars(IrBlock block)
    {
        var sidecars = block.Networks
            .Select(n => new NetworkSidecar(n.Number, n.Number.ToString(), Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();
        return IrSerializer.SerializeBlock(block, sidecars);
    }

    [Fact]
    public void IrHash_SameBlockSerializedTwice_ProducesSameHash()
    {
        var pathA = WriteTempIrFile(SerializeWithEmptySidecars(BlockWithNetworkLogic("Motor_Out")));
        var pathB = WriteTempIrFile(SerializeWithEmptySidecars(BlockWithNetworkLogic("Motor_Out")));

        var report = IrHashRunner.Run(new[] { pathA, pathB });

        Assert.All(report.Entries, e => Assert.False(e.IsError, e.Error));
        Assert.Equal(report.Entries[0].Hash, report.Entries[1].Hash);
        Assert.False(report.HasErrors);
    }

    [Fact]
    public void IrHash_BlocksDifferingInNetworkLogic_ProduceDifferentHashes()
    {
        var pathA = WriteTempIrFile(SerializeWithEmptySidecars(BlockWithNetworkLogic("Motor_Out")));
        var pathB = WriteTempIrFile(SerializeWithEmptySidecars(BlockWithNetworkLogic("Pump_Out")));

        var report = IrHashRunner.Run(new[] { pathA, pathB });

        Assert.NotEqual(report.Entries[0].Hash, report.Entries[1].Hash);
    }

    [Fact]
    public void IrHash_SidecarPresentVsReadableOnly_ProducesSameHash()
    {
        var block = BlockWithNetworkLogic("Motor_Out");

        // With SIDECAR section vs the readable-only form (no SIDECAR) — parsed back via the same
        // prefix-dispatch the runner uses, both must hash identically: the hash is over the readable
        // form, immune to SIDECAR presence.
        var withSidecar = SerializeWithEmptySidecars(block);
        var readableOnly = IrSerializer.SerializeBlockReadable(block);
        Assert.Contains("SIDECAR", withSidecar);
        Assert.DoesNotContain("SIDECAR", readableOnly);

        var pathWith = WriteTempIrFile(withSidecar);
        var pathReadable = WriteTempIrFile(readableOnly);

        var report = IrHashRunner.Run(new[] { pathWith, pathReadable });

        Assert.All(report.Entries, e => Assert.False(e.IsError, e.Error));
        Assert.Equal(report.Entries[0].Hash, report.Entries[1].Hash);
    }

    [Fact]
    public void IrHash_JsonOutput_ParsesAndContainsHash()
    {
        var path = WriteTempIrFile(SerializeWithEmptySidecars(BlockWithNetworkLogic("Motor_Out")));

        var report = IrHashRunner.Run(new[] { path });
        var json = IrHashOutputFormatter.FormatJson(report);

        using var doc = JsonDocument.Parse(json);
        var hashes = doc.RootElement.GetProperty("hashes");
        Assert.Equal(1, hashes.GetArrayLength());
        var hash = hashes[0].GetProperty("hash").GetString();
        Assert.False(string.IsNullOrEmpty(hash));
        Assert.Equal(report.Entries[0].Hash, hash);
    }

    [Fact]
    public void IrHash_MissingFile_IsErrorAndSignalsNonZeroExit()
    {
        var report = IrHashRunner.Run(new[] { Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.ir") });

        Assert.True(report.HasErrors);
        Assert.True(report.Entries[0].IsError);
        Assert.Null(report.Entries[0].Hash);
    }
}
