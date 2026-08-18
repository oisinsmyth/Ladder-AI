using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.DriftCheck;

// Pairs each `ir/<proj>/*.ir` with its counterpart in the exports dir, rebuilds the SimaticML from the
// .ir in-memory via the shared `Program.BuildXmlFromIrText` (the same code path `to-xml` uses — no
// second implementation to drift), and Normalizer-compares it to the export. Decoupled detector: no
// knowledge of which drift is "known/tolerated" (that lives in the golden-test layer, which excludes
// the answer-key blocks) — this just reports what diverges.
public static class DriftCheckRunner
{
    // complete: the caller declares exportsDir is the whole picture (FI-70 — a fresh controller dump
    // rather than a possibly-lagging committed corpus), which is what makes an ABSENCE a finding
    // rather than an ordinary state. It never changes what is compared.
    public static DriftCheckReport Run(string projectDir, string exportsDir, bool complete = false)
    {
        // Registries resolve callee interfaces / operand types for synthesizing any readable-only block
        // in the project (a stored-sidecar block ignores them). Built once from the whole export.
        var callees = Program.BuildCalleeRegistry(Array.Empty<string>(), projectDir);
        var tagTypes = Program.BuildTagTypeRegistry(Array.Empty<string>(), projectDir);

        var entries = new List<DriftEntry>();

        // PAIRING BY DECLARED OBJECT IDENTITY, NOT BY FILENAME (2026-08-14). Found against the live
        // controller, which is why no offline probe reached it: TIA's own name for the default tag table
        // contains SPACES ("Default tag table") and the .ir filename does not (DefaultTagTable.ir) — yet
        // BOTH documents declare the real name in their own content. Pairing on the filename made the
        // object fail to pair and then counted it TWICE, once in each direction:
        //     EXPORT-ONLY: Default tag table   (no paired .ir in project dir)
        //     SKIPPED:     DefaultTagTable     (no paired .xml in exports dir)
        // with nothing anywhere suggesting they might be the same object. Under --complete an
        // EXPORT-ONLY row means "this is in the controller and no .ir describes it" — a serious finding,
        // and that one was FALSE, while the real question (does the tag table match?) was never asked.
        // A spurious finding AND a silently skipped comparison, out of one naming mismatch.
        var irs = Index(
            Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly),
            IrObjectName, entries, "project dir");
        var xmls = Index(
            Directory.EnumerateFiles(exportsDir, "*.xml", SearchOption.TopDirectoryOnly),
            XmlObjectName, entries, "exports dir");

        foreach (var identity in irs.Keys.Concat(xmls.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal))
        {
            var haveIr = irs.TryGetValue(identity, out var irPath);
            var haveXml = xmls.TryGetValue(identity, out var xmlPath);
            var name = Path.GetFileNameWithoutExtension(haveIr ? irPath! : xmlPath!);

            if (!haveXml)
            {
                entries.Add(new DriftEntry(name, irPath, null, DriftStatus.Skipped, "no paired export"));
                continue;
            }

            if (!haveIr)
            {
                entries.Add(new DriftEntry(name, null, xmlPath, DriftStatus.ExportOnly, "no paired .ir in project dir"));
                continue;
            }

            XDocument built;
            try
            {
                var irText = File.ReadAllText(irPath!);
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

            XDocument committed;
            try
            {
                committed = XDocument.Load(xmlPath!);
            }
            catch (System.Xml.XmlException ex)
            {
                // Same class as an unbuildable .ir and reported the same way: a comparison that could
                // not be made. It was previously an unhandled throw that ended the whole walk.
                entries.Add(new DriftEntry(name, irPath, xmlPath, DriftStatus.Error,
                    $"export is not parseable XML: {ex.Message}"));
                continue;
            }

            // WHOSE DOCUMENT IS ON THE OTHER SIDE OF THIS COMPARISON. `<DocumentInfo>` is written by
            // TIA's own exporter on every Openness export and by nothing else — the converter's
            // SimaticML writers never emit it. So its ABSENCE means the file could have been produced
            // by `to-xml`, which writes BESIDE ITS INPUT by default (FI-72) and therefore replaces a
            // real export sitting in the same directory. See DriftCheckReport.TiaExportCount.
            var fromTia = committed.Root?.Elements()
                .Any(e => string.Equals(e.Name.LocalName, "DocumentInfo", StringComparison.Ordinal)) == true;

            var equivalent = Normalizer.AreSemanticallyEquivalent(committed, built);
            entries.Add(new DriftEntry(
                name, irPath, xmlPath,
                equivalent ? DriftStatus.Match : DriftStatus.Drifted,
                equivalent ? null : "semantic divergence between .ir and committed export",
                fromTia));
        }

        return new DriftCheckReport(entries, complete, projectDir, exportsDir);
    }

    // Files keyed by the identity DECLARED IN THE FILE, falling back to the basename when the file does
    // not declare one. Two files claiming one identity is a PAIRING FAILURE — a third thing, and not an
    // absence in either direction: nothing can be compared, and saying "missing" would be a guess about
    // which of them was meant.
    private static Dictionary<string, string> Index(
        IEnumerable<string> paths, Func<string, string?> declaredName, List<DriftEntry> entries, string where)
    {
        var byIdentity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.OrderBy(p => p, StringComparer.Ordinal))
        {
            var identity = Normalise(declaredName(path) ?? Path.GetFileNameWithoutExtension(path));
            if (byIdentity.TryGetValue(identity, out var first))
            {
                entries.Add(new DriftEntry(
                    Path.GetFileNameWithoutExtension(path), null, null, DriftStatus.PairingFailure,
                    $"two files in the {where} claim the identity '{identity}': "
                    + $"{Path.GetFileName(first)} and {Path.GetFileName(path)} - neither can be paired"));
                continue;
            }

            byIdentity[identity] = path;
        }

        return byIdentity;
    }

    // Whitespace is removed because it is exactly what differs between TIA's "Default tag table" and the
    // filename DefaultTagTable. Nothing else is folded away: this is a filename-vs-declared-name
    // reconciliation, not a fuzzy match, and a comparator that learns to equate more representations
    // starts passing things.
    private static string Normalise(string name) =>
        new(name.Where(c => !char.IsWhiteSpace(c)).ToArray());

    // The .ir's own declared name: `BLOCK <Kind> <Name>` / `DB <Name>` / `TYPE <Name>` / `TAGTABLE <Name>`.
    // The rest of the line IS the name (it may contain spaces — that is the whole point).
    private static string? IrObjectName(string path)
    {
        try
        {
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith("BLOCK ", StringComparison.Ordinal))
                {
                    var parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                    return parts.Length == 3 ? parts[2].Trim() : null;
                }

                foreach (var prefix in new[] { "TAGTABLE ", "TYPE ", "DB " })
                {
                    if (line.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return line[prefix.Length..].Trim();
                    }
                }

                return null;   // first meaningful line is none of the four - fall back to the basename
            }
        }
        catch (IOException)
        {
        }

        return null;
    }

    // The export's own declared name: the OUTERMOST SW.* object's AttributeList/Name. Outermost matters —
    // a tag table's own Name sits beside a nested SW.Tags.PlcTag for every tag it contains, each with a
    // Name of its own.
    private static string? XmlObjectName(string path)
    {
        try
        {
            var document = XDocument.Load(path);
            var obj = document.Root?.Elements()
                .FirstOrDefault(e => e.Name.LocalName.StartsWith("SW.", StringComparison.Ordinal));
            return obj?.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "AttributeList")?
                .Elements()
                .FirstOrDefault(e => e.Name.LocalName == "Name")?
                .Value.Trim();
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException)
        {
            return null;   // unparseable: paired by basename, and the comparison itself reports the error
        }
    }
}
