using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.DriftCheck;

// Pairs each `ir/<proj>/*.ir` with `simatic-ml/<proj>/<name>.xml` (identical basename), rebuilds the
// SimaticML from the .ir in-memory via the shared `Program.BuildXmlFromIrText` (the same code path
// `to-xml` uses — no second implementation to drift), and Normalizer-compares it to the committed
// export. Decoupled detector: no knowledge of which drift is "known/tolerated" (that lives in the
// golden-test layer, which excludes the answer-key blocks) — this just reports what diverges.
public static class DriftCheckRunner
{
    public static DriftCheckReport Run(string projectDir, string exportsDir)
    {
        // Registries resolve callee interfaces / operand types for synthesizing any readable-only block
        // in the project (a stored-sidecar block ignores them). Built once from the whole export.
        var callees = Program.BuildCalleeRegistry(Array.Empty<string>(), projectDir);
        var tagTypes = Program.BuildTagTypeRegistry(Array.Empty<string>(), projectDir);

        var entries = new List<DriftEntry>();
        var irFiles = Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal);

        foreach (var irPath in irFiles)
        {
            var name = Path.GetFileNameWithoutExtension(irPath);
            var xmlPath = Path.Combine(exportsDir, name + ".xml");

            if (!File.Exists(xmlPath))
            {
                entries.Add(new DriftEntry(name, irPath, null, DriftStatus.Skipped, "no paired .xml in exports dir"));
                continue;
            }

            XDocument built;
            try
            {
                var irText = File.ReadAllText(irPath);
                // Serialize-then-reparse so both sides are disk-parsed XDocuments: BuildXmlFromIrText
                // yields an in-memory tree whose whitespace/text node shape differs from a file-loaded
                // one, and XNode.DeepEquals (inside the Normalizer) is sensitive to that (the
                // SynthesizeReadableVerified caveat). Comparing two parsed trees avoids a spurious mismatch.
                built = XDocument.Parse(Program.BuildXmlFromIrText(irText, synthesize: false, callees, tagTypes).ToString());
            }
            catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException
                                           or NonReducibleNetworkException or IrFormatException
                                           or UnsupportedSynthesisConstructException)
            {
                entries.Add(new DriftEntry(name, irPath, xmlPath, DriftStatus.Error,
                    $"could not build XML from .ir: {ex.GetType().Name}: {ex.Message}"));
                continue;
            }

            var committed = XDocument.Load(xmlPath);
            var equivalent = Normalizer.AreSemanticallyEquivalent(committed, built);
            entries.Add(new DriftEntry(
                name, irPath, xmlPath,
                equivalent ? DriftStatus.Match : DriftStatus.Drifted,
                equivalent ? null : "semantic divergence between .ir and committed export"));
        }

        return new DriftCheckReport(entries);
    }
}
