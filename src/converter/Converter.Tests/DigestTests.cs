using System.Text.Json;
using Converter.Digest;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-15 (2026-07-16): `converter digest` — a deterministic structural summary of .ir content.
/// Same test shape as ReviewRunnerTests: build the model in memory, serialize with the real
/// serializers, digest from a real temp file (DigestBuilder reads by path).
/// </summary>
public class DigestTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string WriteTempIrFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"digest-test-{Guid.NewGuid():N}.ir");
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

    private static IrBlock BlockWithCallAndTimer() => new(
        "0", "FB", "FB_Demo", 7, "LAD", "Header comment.", new[]
        {
            new IrNetwork(1, "Run seal-in", new[]
            {
                new CoilAssignment("DB_Controls.Motor_Run", new Expr.TagRef("Clock_0.5Hz")),
                new CoilAssignment("Motor_Out", new Expr.TagRef("DB_Controls.Motor_Run")),
            }),
            new IrNetwork(2, "Starter call", Array.Empty<CoilAssignment>(),
                Timers: new[] { new TimerBinding("RunTimer", new Expr.TagRef("Start_PB"), new Expr.TagRef("DB_Settings.RunDelay")) },
                Calls: new[] { new CallStatement("MotorStarter", "MotorStarterInst1", new Expr.TagRef("Enable"), Array.Empty<CallArgument>()) }),
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
    public void DigestFiles_Block_ReportsKindInterfaceCallsNetworksAndRoots()
    {
        var path = WriteTempIrFile(SerializeWithEmptySidecars(BlockWithCallAndTimer()));

        var report = DigestBuilder.DigestFiles(new[] { path }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        Assert.Null(file.FileError);
        Assert.Equal("FB", file.Kind);
        Assert.Equal("FB_Demo", file.Name);
        Assert.Equal(7, file.Number);
        Assert.Equal("Demo block", file.Title);

        var staticSection = Assert.Single(file.Sections, s => s.Section == "STATIC");
        Assert.Equal(new[] { "RunTimer : TON_TIME" }, staticSection.Members);

        var call = Assert.Single(file.Calls);
        Assert.Equal("MotorStarter", call.BlockName);
        Assert.Equal(1, call.CallCount);
        Assert.Equal(new[] { "MotorStarterInst1" }, call.Instances);

        Assert.Equal(2, file.Networks.Count);
        Assert.Equal("coil:2", file.Networks[0].Statements);
        Assert.Equal("timer:1, call:1", file.Networks[1].Statements);
        Assert.Equal("Run seal-in", file.Networks[0].Title);
    }

    [Fact]
    public void DigestFiles_TagRoots_AreDedupedSortedAndLiteralDotNamesStayAtomic()
    {
        var path = WriteTempIrFile(SerializeWithEmptySidecars(BlockWithCallAndTimer()));

        var report = DigestBuilder.DigestFiles(new[] { path }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        // Clock_0.5Hz must stay one root (the literal-dot system tag), DB_Controls must appear
        // once despite two references, and call instance/timer instance/plain tags all count.
        Assert.Equal(
            new[] { "Clock_0.5Hz", "DB_Controls", "DB_Settings", "Enable", "MotorStarterInst1", "Motor_Out", "RunTimer", "Start_PB" },
            file.TagRoots);
    }

    [Fact]
    public void DigestFiles_SidecarlessBlockText_IsDigestedViaTheSynthesisParser()
    {
        var full = SerializeWithEmptySidecars(BlockWithCallAndTimer());
        var sidecarIndex = full.Replace("\r\n", "\n").IndexOf("\nSIDECAR", StringComparison.Ordinal);
        Assert.True(sidecarIndex > 0, "expected serialized block text to contain a SIDECAR section");
        var withoutSidecar = full.Replace("\r\n", "\n")[..sidecarIndex] + "\n";
        var path = WriteTempIrFile(withoutSidecar);

        var report = DigestBuilder.DigestFiles(new[] { path }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        Assert.Null(file.FileError);
        Assert.Equal("FB_Demo", file.Name);
        Assert.Equal(2, file.Networks.Count);
    }

    [Fact]
    public void DigestFiles_GlobalAndInstanceDb_ReportKindNumberAndNestedMemberCounts()
    {
        var globalDb = new DbSource("0", "DB_Controls", 12, InstanceOfName: null, Comment: null, Members: new[]
        {
            new DbMember("Motor_Run", "Bool", Retain: false, StartValue: null),
            new DbMember("Setpoints", "Struct", Retain: false, StartValue: null, NestedMembers: new[]
            {
                new DbMember("High", "Int", Retain: false, StartValue: null),
                new DbMember("Low", "Int", Retain: false, StartValue: null),
            }),
        });
        var instanceDb = new DbSource("0", "iDB_MotorStarter", 6, InstanceOfName: "MotorStarter", Comment: null, Members: Array.Empty<DbMember>());

        var globalPath = WriteTempIrFile(DbIrSerializer.Serialize(globalDb));
        var instancePath = WriteTempIrFile(DbIrSerializer.Serialize(instanceDb));

        var report = DigestBuilder.DigestFiles(new[] { globalPath, instancePath }, ignoreErrors: false);

        Assert.Equal(2, report.Files.Count);
        var global = report.Files[0];
        Assert.Equal("GlobalDB", global.Kind);
        Assert.Equal(12, global.Number);
        var members = Assert.Single(global.Sections, s => s.Section == "MEMBERS");
        Assert.Equal(new[] { "Motor_Run : Bool", "Setpoints : Struct (2 nested)" }, members.Members);

        var instance = report.Files[1];
        Assert.Equal("InstanceDB", instance.Kind);
        Assert.Equal("MotorStarter", instance.InstanceOfName);
    }

    [Fact]
    public void DigestFiles_TagTable_ReportsTagsWithTypeAndAddress()
    {
        var table = new PlcTagTableSource("0", "MinimalTags", new[]
        {
            new PlcTagSource("1", "Start_PB", "Bool", "%I0.0", true, true, true, null),
            new PlcTagSource("2", "Motor_Out", "Bool", "%Q0.1", true, true, true, null),
        });
        var path = WriteTempIrFile(TagTableIrSerializer.Serialize(table));

        var report = DigestBuilder.DigestFiles(new[] { path }, ignoreErrors: false);

        var file = Assert.Single(report.Files);
        Assert.Equal("TAGTABLE", file.Kind);
        Assert.Equal("MinimalTags", file.Name);
        var tags = Assert.Single(file.Sections);
        Assert.Equal("TAGS", tags.Section);
        Assert.Equal(new[] { "Start_PB : Bool @ %I0.0", "Motor_Out : Bool @ %Q0.1" }, tags.Members);
    }

    [Fact]
    public void DigestFiles_BadFile_ThrowsWithoutIgnoreErrorsAndRecordsWithIt()
    {
        var badPath = WriteTempIrFile("not ir content at all");

        Assert.Throws<DigestFileException>(() => DigestBuilder.DigestFiles(new[] { badPath }, ignoreErrors: false));

        var report = DigestBuilder.DigestFiles(new[] { badPath }, ignoreErrors: true);
        var file = Assert.Single(report.Files);
        Assert.NotNull(file.FileError);
    }

    [Fact]
    public void FormatText_ContainsTheStructuralLines()
    {
        var path = WriteTempIrFile(SerializeWithEmptySidecars(BlockWithCallAndTimer()));
        var report = DigestBuilder.DigestFiles(new[] { path }, ignoreErrors: false);

        var text = DigestOutputFormatter.FormatText(report);

        Assert.Contains("KIND: FB  NAME: FB_Demo  NUMBER: 7", text);
        Assert.Contains("MotorStarter x1 (instances: MotorStarterInst1)", text);
        Assert.Contains("NETWORKS (2)", text);
        Assert.Contains("1 \"Run seal-in\" coil:2", text);
        Assert.Contains("TAG ROOTS (8):", text);
        Assert.Contains("SUMMARY: 1 file(s)", text);
    }

    [Fact]
    public void FormatJson_IsValidJsonMirroringTheReport()
    {
        var path = WriteTempIrFile(SerializeWithEmptySidecars(BlockWithCallAndTimer()));
        var report = DigestBuilder.DigestFiles(new[] { path }, ignoreErrors: false);

        var json = DigestOutputFormatter.FormatJson(report);

        using var doc = JsonDocument.Parse(json);
        var file = doc.RootElement.GetProperty("files")[0];
        Assert.Equal("FB_Demo", file.GetProperty("name").GetString());
        Assert.Equal(2, file.GetProperty("networks").GetArrayLength());
        Assert.Equal(8, file.GetProperty("tagRoots").GetArrayLength());
    }
}
