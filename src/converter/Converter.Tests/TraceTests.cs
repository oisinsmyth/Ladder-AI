using System.Text.Json;
using Converter.Ir;
using Converter.SimaticMl;
using Converter.Trace;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-25 (2026-07-20): `converter trace` — the review-functional forward-pass verdict tracer. v1 hops:
/// output-path (1), interface-chain (2), number-constraint (4). Builds a temp corpus + binding file the
/// way the command runs, and asserts the per-hop candidate verdicts.
/// </summary>
public class TraceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _bindingPath;

    public TraceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"trace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        // DB_Settings: Good matches spec, Bad mismatches, NoVal has no start value.
        WriteDb("DB_Settings.ir", new DbSource("0", "DB_Settings", 1, InstanceOfName: null, Comment: null, Members: new[]
        {
            new DbMember("Good", "Real", Retain: false, StartValue: "10.0"),
            new DbMember("Bad", "Real", Retain: false, StartValue: "5.0"),
            new DbMember("NoVal", "Real", Retain: false, StartValue: null),
        }));

        // FB_Map writes Out_Written and Iface_Written; nothing writes Out_Dead / Iface_Dead.
        WriteBlock("FB_Map.ir", new IrBlock("0", "FB", "FB_Map", 1, "LAD", null, new[]
        {
            new IrNetwork(1, "Maps", new[]
            {
                new CoilAssignment("Out_Written", new Expr.TagRef("X")),
                new CoilAssignment("Iface_Written", new Expr.TagRef("Y")),
            }),
        }));

        _bindingPath = Path.Combine(_dir, "binding.json");
        File.WriteAllText(_bindingPath, Binding);
    }

    private const string Binding = """
    {
      "bindings": [
        { "req": "R1", "out_tag": "Out_Written" },
        { "req": "R2", "out_tag": "Out_Dead" },
        { "req": "R3", "iface_member": "Iface_Written" },
        { "req": "R4", "iface_member": "Iface_Dead" },
        { "req": "R5", "number": { "member": "DB_Settings.Good", "expected": "10.0" } },
        { "req": "R6", "number": { "member": "DB_Settings.Bad", "expected": "10.0" } },
        { "req": "R7", "number": { "member": "DB_Settings.NoVal", "expected": "10.0" } },
        { "req": "R8", "number": { "member": "DB_Settings.Missing", "expected": "10.0" } }
      ]
    }
    """;

    private void WriteDb(string file, DbSource db) =>
        File.WriteAllText(Path.Combine(_dir, file), DbIrSerializer.Serialize(db));

    private void WriteBlock(string file, IrBlock block)
    {
        var sidecars = block.Networks
            .Select(n => new NetworkSidecar(n.Number, n.Number.ToString(), Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();
        File.WriteAllText(Path.Combine(_dir, file), IrSerializer.SerializeBlock(block, sidecars));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static Verdict VerdictOf(TraceReport report, string req) =>
        report.Requirements.Single(r => r.Req == req).Hops.Single().Verdict;

    [Fact]
    public void Run_EachV1Hop_ClassifiesCorrectly()
    {
        var report = TraceRunner.Run(_bindingPath, _dir);

        Assert.Equal(Verdict.Ok, VerdictOf(report, "R1"));            // out_tag written
        Assert.Equal(Verdict.Unimplemented, VerdictOf(report, "R2")); // out_tag no writer
        Assert.Equal(Verdict.Ok, VerdictOf(report, "R3"));            // iface_member written
        Assert.Equal(Verdict.BrokenChain, VerdictOf(report, "R4"));   // iface_member no writer
        Assert.Equal(Verdict.Ok, VerdictOf(report, "R5"));            // start value matches
        Assert.Equal(Verdict.Contradicted, VerdictOf(report, "R6"));  // start value mismatches
        Assert.Equal(Verdict.Partial, VerdictOf(report, "R7"));       // no start value
        Assert.Equal(Verdict.Partial, VerdictOf(report, "R8"));       // member not found
    }

    [Fact]
    public void Run_OkOutputPath_CarriesWriterEvidence()
    {
        var report = TraceRunner.Run(_bindingPath, _dir);
        var hop = report.Requirements.Single(r => r.Req == "R1").Hops.Single();
        Assert.Contains("FB_Map N1", hop.Evidence);
    }

    [Fact]
    public void BindingFile_Load_ParsesSnakeCaseFields()
    {
        var file = BindingFile.Load(_bindingPath);
        var r1 = file.Bindings.Single(b => b.Req == "R1");
        Assert.Equal("Out_Written", r1.OutTag); // snake_case out_tag bound to OutTag
        var r5 = file.Bindings.Single(b => b.Req == "R5");
        Assert.Equal("DB_Settings.Good", r5.Number!.Member);
    }

    [Fact]
    public void FormatText_And_Json_Render()
    {
        var report = TraceRunner.Run(_bindingPath, _dir);

        var text = TraceOutputFormatter.FormatText(report);
        Assert.Contains("[UNIMPLEMENTED] output-path: Out_Dead", text);
        Assert.Contains("[CONTRADICTED] number-constraint", text);
        Assert.Contains("not an adjudicated pass", text);

        var json = TraceOutputFormatter.FormatJson(report);
        using var doc = JsonDocument.Parse(json); // asserts valid JSON
        Assert.Equal(8, doc.RootElement.GetProperty("requirements").GetArrayLength());
    }
}
