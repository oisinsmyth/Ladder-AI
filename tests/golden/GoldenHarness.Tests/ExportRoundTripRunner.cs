using System.Xml.Linq;
using Xunit;

namespace GoldenHarness;

/// <summary>
/// real TIA export → <c>to-ir</c> → <c>to-xml</c>, into a scratch dir. The direction the raw
/// (Normalizer-free) fidelity suites use: TIA wrote the input, so the expected value in any assertion
/// downstream of this comes from TIA's own file and not from anything we produced — which is what
/// stops a defect that is consistent across our read and write paths from cancelling.
///
/// Every failure here is a FAILURE, never a skip. A round trip that produced no file must not read as
/// a block whose attributes all survived.
/// </summary>
public static class ExportRoundTripRunner
{
    public static XDocument Run(string scope, string name, string exportPath, string irDir)
    {
        var workDir = Path.Combine(Path.GetTempPath(), scope, name);
        if (Directory.Exists(workDir))
        {
            Directory.Delete(workDir, recursive: true);
        }

        var scratchIr = Path.Combine(workDir, "ir");
        var scratchXml = Path.Combine(workDir, "xml");
        Directory.CreateDirectory(scratchIr);
        Directory.CreateDirectory(scratchXml);

        // --out is not optional: the default is beside-the-input, which would write over the committed
        // corpus (FI-72).
        var toIr = ProcessRunner.Run(ToolPaths.ConverterExe, "to-ir", exportPath, "--out", scratchIr);
        Assert.True(toIr.ExitCode == 0, $"{name}: to-ir failed (exit {toIr.ExitCode}) — {toIr.StdErr}{toIr.StdOut}");

        var irPath = Path.Combine(scratchIr, name + ".ir");
        Assert.True(File.Exists(irPath),
            $"{name}: to-ir reported success but wrote no {irPath}. Empty is not clean.");

        // No --synthesize: `to-ir` wrote a real sidecar and the converter refuses to derive over one.
        // Block-level attributes are not network wiring — they must survive whether or not the sidecar
        // is re-derived.
        var toXml = ProcessRunner.Run(
            ToolPaths.ConverterExe, "to-xml", irPath, "--project", irDir, "--out", scratchXml);
        Assert.True(toXml.ExitCode == 0, $"{name}: to-xml failed (exit {toXml.ExitCode}) — {toXml.StdErr}{toXml.StdOut}");

        var xmlPath = Path.Combine(scratchXml, name + ".xml");
        Assert.True(File.Exists(xmlPath),
            $"{name}: to-xml reported success but wrote no {xmlPath}. Empty is not clean.");

        return XDocument.Load(xmlPath);
    }

    /// <summary>Every committed export, paired with the IR dir that resolves its type references.</summary>
    public static IEnumerable<(string Name, string ExportPath, string IrDir)> CommittedExports()
    {
        var repo = ToolPaths.RepoRoot();
        foreach (var project in new[] { "reference", "test-project001" })
        {
            var xmlDir = Path.Combine(repo, "simatic-ml", project);
            if (!Directory.Exists(xmlDir))
            {
                continue;
            }

            foreach (var xmlPath in Directory.EnumerateFiles(xmlDir, "*.xml").OrderBy(p => p, StringComparer.Ordinal))
            {
                yield return (Path.GetFileNameWithoutExtension(xmlPath), xmlPath, Path.Combine(repo, "ir", project));
            }
        }
    }
}
