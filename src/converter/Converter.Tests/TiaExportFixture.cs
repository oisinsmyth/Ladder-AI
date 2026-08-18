using System.Xml.Linq;

namespace Converter.Tests;

/// <summary>
/// Saves a converter-built SimaticML document to disk <b>as TIA would have written it</b> — that is,
/// carrying the <c>&lt;DocumentInfo&gt;</c> block TIA's exporter emits on every Openness export and the
/// converter's own writers never emit.
///
/// <para>🔴 <b>WHY THE FIXTURES HAD TO LEARN THIS.</b> Every drift-check fixture in this suite built
/// its "export" side by calling a converter writer and saving the result — which is exactly the state
/// that made <c>drift-check</c> report <c>0 drifted / 101 match</c> four times on a live job while
/// comparing the converter against its own <c>to-xml</c> output. Once that state gates
/// (<c>DriftCheckReport.ComparedNothingFromTia</c>), a fixture standing in for a TIA export has to
/// declare itself one, and a fixture that does NOT is the negative case rather than the norm.</para>
///
/// <para>The Normalizer already ignores DocumentInfo — it must, since real exports carry it and
/// converter output does not, and the two are compared for equivalence every day — so adding it
/// changes provenance and nothing else.</para>
/// </summary>
public static class TiaExportFixture
{
    /// <summary>Saves <paramref name="document"/> with TIA's provenance block injected.</summary>
    public static void SaveAsTiaExport(this XDocument document, string path)
    {
        var copy = new XDocument(document);
        copy.Root!.AddFirst(new XElement("DocumentInfo",
            new XElement("Created", "2026-01-01T00:00:00.0000000Z"),
            new XElement("ExportSetting", "WithDefaults")));
        copy.Save(path);
    }
}
