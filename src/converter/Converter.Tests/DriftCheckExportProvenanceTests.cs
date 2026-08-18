using Converter.DriftCheck;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 2026-08-18 — <b>THE FOURTH WAY <c>drift-check</c> COULD REPORT A CLEAN RUN HAVING PROVED
/// NOTHING, AND THE ONLY ONE THAT SHOWS A FULL DENOMINATOR WHILE DOING IT.</b>
///
/// <para>The three already closed all surface as <c>COMPARED: 0</c>. This one surfaces as
/// <c>COMPARED: 101</c> with a hundred green MATCH lines, because the directory passed as
/// <c>--exports</c> held <b>the converter's own <c>to-xml</c> output</b> rather than TIA exports:
/// <c>to-xml</c> writes BESIDE ITS INPUT by default (FI-72), so running it over an <c>ir/</c>
/// directory replaces every real export sitting there, and every comparison afterwards is the
/// converter against itself. MATCH becomes a tautology.</para>
///
/// <para>MEASURED on a live job: 0 of 101 files carried TIA's <c>&lt;DocumentInfo&gt;</c>, and
/// <c>0 drifted / 101 match</c> had been recorded FOUR TIMES as evidence the corpus was in sync with
/// the controller.</para>
///
/// <para>THE GATE IS "NONE", NOT "ALL", and the second test here is what pins that: a real corpus
/// legitimately carries the odd document without provenance (<c>simatic-ml/reference/</c> has four out
/// of fifteen), and failing those would be the gate firing outside its own question.</para>
/// </summary>
public class DriftCheckExportProvenanceTests : IDisposable
{
    private readonly List<string> _dirs = new();

    private string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"drift-prov-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        return dir;
    }

    private static DbSource Db(string name) =>
        new("0", name, 1, InstanceOfName: null, Comment: null,
            Members: new[] { new DbMember("A", "Bool", Retain: false, StartValue: null) });

    private static void WriteIr(string dir, string name) =>
        File.WriteAllText(Path.Combine(dir, name + ".ir"), DbIrSerializer.Serialize(Db(name)));

    /// <summary>An export as TIA writes one — carrying its provenance block.</summary>
    private static void WriteTiaExport(string dir, string name) =>
        DbSourceWriter.Write(Db(name)).SaveAsTiaExport(Path.Combine(dir, name + ".xml"));

    /// <summary>An "export" as `to-xml` writes one — the converter's own output, no provenance.</summary>
    private static void WriteConverterOutput(string dir, string name) =>
        DbSourceWriter.Write(Db(name)).Save(Path.Combine(dir, name + ".xml"));

    [Fact]
    public void EveryExportIsConverterOutput_IsNotAPass()
    {
        var project = NewDir();
        var exports = NewDir();
        foreach (var name in new[] { "DB_One", "DB_Two", "DB_Three" })
        {
            WriteIr(project, name);
            WriteConverterOutput(exports, name);
        }

        var report = DriftCheckRunner.Run(project, exports);

        // Every pair MATCHES — of course it does, it is the same writer on both sides. That is the
        // whole point: the old contract called this a clean run.
        Assert.Equal(3, report.ComparedCount);
        Assert.Equal(3, report.Entries.Count(e => e.Status == DriftStatus.Match));
        Assert.False(report.ExaminedNothing); // the denominator is FULL, which is what made it credible

        Assert.Equal(0, report.TiaExportCount);
        Assert.Equal(3, report.NonTiaExportCount);
        Assert.True(report.ComparedNothingFromTia);
        Assert.True(report.HasDrift);

        var text = DriftCheckOutputFormatter.FormatText(report);
        Assert.Contains("PROVENANCE: 0 of 3", text);
        Assert.Contains("NO TIA EXPORT WAS COMPARED", text);
    }

    [Fact]
    public void OneExportWithoutProvenanceAmongRealOnes_ReportsButDoesNotGate()
    {
        var project = NewDir();
        var exports = NewDir();
        WriteIr(project, "DB_Real");
        WriteTiaExport(exports, "DB_Real");
        WriteIr(project, "DB_HandMade");
        WriteConverterOutput(exports, "DB_HandMade");

        var report = DriftCheckRunner.Run(project, exports);

        Assert.Equal(1, report.TiaExportCount);
        Assert.Equal(1, report.NonTiaExportCount);
        Assert.False(report.ComparedNothingFromTia);
        Assert.False(report.HasDrift);

        // Reported, not gated: the count is on every run so a directory drifting towards converter
        // output is visible long before it becomes total.
        Assert.Contains("PROVENANCE: 1 of 2", DriftCheckOutputFormatter.FormatText(report));
        Assert.DoesNotContain("NO TIA EXPORT WAS COMPARED", DriftCheckOutputFormatter.FormatText(report));
    }

    [Fact]
    public void AllExportsFromTia_IsAnOrdinaryCleanRun()
    {
        var project = NewDir();
        var exports = NewDir();
        foreach (var name in new[] { "DB_One", "DB_Two" })
        {
            WriteIr(project, name);
            WriteTiaExport(exports, name);
        }

        var report = DriftCheckRunner.Run(project, exports);

        Assert.Equal(2, report.TiaExportCount);
        Assert.False(report.ComparedNothingFromTia);
        Assert.False(report.HasDrift);
        Assert.Contains("PROVENANCE: 2 of 2", DriftCheckOutputFormatter.FormatText(report));
    }

    /// <summary>
    /// Provenance is per-entry and reaches the JSON, so a consumer can tell "nothing drifted" from
    /// "nothing on the other side came from TIA" without parsing prose.
    /// </summary>
    [Fact]
    public void JsonCarriesTheProvenanceDenominator()
    {
        var project = NewDir();
        var exports = NewDir();
        WriteIr(project, "DB_One");
        WriteConverterOutput(exports, "DB_One");

        var json = DriftCheckOutputFormatter.FormatJson(DriftCheckRunner.Run(project, exports));

        Assert.Contains("\"tiaExportCount\": 0", json);
        Assert.Contains("\"comparedNothingFromTia\": true", json);
        Assert.Contains("\"fromTia\": false", json);
    }

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
