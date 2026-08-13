using System.Xml.Linq;
using Xunit;

namespace GoldenHarness;

/// <summary>
/// <c>&lt;MemoryLayout&gt;</c> compared RAW against real TIA exports — again deliberately not through
/// <see cref="Normalizer"/>, and for a sharper reason than the Interface case: *** THE NORMALIZER'S
/// LAYOUT COMPARISON IS OPTED OUT ON EVERY COMPARISON THIS HARNESS MAKES. ***
///
/// <para><c>Normalizer.AreSemanticallyEquivalent</c> compares the element only when BOTH documents
/// declare one ("a document that declares none is stating no opinion"), which is the right rule for a
/// committed corpus that predates the emit side. The consequence, MEASURED 2026-08-13: <b>0 of the 26
/// committed <c>.ir</c> files declare a layout</b>, while 27 of 38 committed exports do — so every
/// <c>AreSemanticallyEquivalent</c> call reachable from this suite runs with
/// <c>compareMemoryLayout: false</c>. <c>converter compare</c> says so out loud on any document
/// derived from the corpus: <c>MEMORYLAYOUT: Optimized -&gt; (none declared) (NOT compared)</c>.</para>
///
/// <para>That matters because the layout is not cosmetic. A block's layout decides whether classic
/// S7comm can see it at all (an Optimized block is ABSENT on the wire, not an error), and it is not
/// durable: <c>openness-cli block-layout --set Standard</c> is reverted to Optimized by the next
/// import, because the imported XML states no opinion and TIA applies the S7-1200 default. That is a
/// silent revert behind a green check — exactly what an unarmed assertion cannot catch.</para>
///
/// <para>Direction: real export → <c>to-ir</c> → <c>to-xml</c> → read the layout back RAW, which is
/// the one direction that can be asserted today, since the corpus <c>.ir</c> is silent. The expected
/// value is read out of TIA's own file; nothing our code produces contributes to it, so a converter
/// that dropped the attribute on BOTH the read and the write path still goes red here.</para>
/// </summary>
public class MemoryLayoutFidelityTests
{
    /// <summary>
    /// Every committed export. Includes the four early trimmed artifacts that carry no
    /// <c>&lt;DocumentInfo&gt;</c> (AlarmWords, CommsProcessData, EquipmentStatus, NodeStatusAlarms) —
    /// they declare no layout either, and "declares none, still declares none afterwards" is a real
    /// assertion: inventing a layout where TIA stated none is the same defect pointing the other way.
    /// </summary>
    private static IEnumerable<(string Name, string ExportPath, string IrDir)> Exports()
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

    public static IEnumerable<object[]> AllExports() =>
        Exports().Select(e => new object[] { e.Name, e.ExportPath, e.IrDir });

    /// <summary>Every declared layout value, in document order. Raw — no ignore list, no opt-in.</summary>
    internal static IReadOnlyList<string> DeclaredLayouts(XDocument doc) =>
        doc.Descendants()
            .Where(e => e.Name.LocalName == "MemoryLayout")
            .Select(e => e.Value.Trim())
            .ToList();

    /// <summary>
    /// export → <c>to-ir</c> → <c>to-xml</c>, into a scratch dir. <c>--out</c> is not optional: the
    /// default is beside-the-input, which would write over the committed corpus (FI-72).
    /// Every failure is a FAILURE, never a skip — a round trip that produced no file must not read as
    /// a block whose layout was preserved.
    /// </summary>
    private static XDocument RoundTrip(string name, string exportPath, string irDir)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "memory-layout-fidelity", name);
        if (Directory.Exists(workDir))
        {
            Directory.Delete(workDir, recursive: true);
        }

        var scratchIr = Path.Combine(workDir, "ir");
        var scratchXml = Path.Combine(workDir, "xml");
        Directory.CreateDirectory(scratchIr);
        Directory.CreateDirectory(scratchXml);

        var toIr = ProcessRunner.Run(ToolPaths.ConverterExe, "to-ir", exportPath, "--out", scratchIr);
        Assert.True(toIr.ExitCode == 0, $"{name}: to-ir failed (exit {toIr.ExitCode}) — {toIr.StdErr}{toIr.StdOut}");

        var irPath = Path.Combine(scratchIr, name + ".ir");
        Assert.True(File.Exists(irPath),
            $"{name}: to-ir reported success but wrote no {irPath}. Empty is not clean.");

        // No --synthesize: `to-ir` wrote a real sidecar, and the converter refuses to derive over one.
        // The sidecar path is the right one for this question anyway — MemoryLayout is a block-level
        // attribute, not network wiring, so it must survive whether or not the sidecar is re-derived.
        var toXml = ProcessRunner.Run(
            ToolPaths.ConverterExe, "to-xml", irPath, "--project", irDir, "--out", scratchXml);
        Assert.True(toXml.ExitCode == 0, $"{name}: to-xml failed (exit {toXml.ExitCode}) — {toXml.StdErr}{toXml.StdOut}");

        var xmlPath = Path.Combine(scratchXml, name + ".xml");
        Assert.True(File.Exists(xmlPath),
            $"{name}: to-xml reported success but wrote no {xmlPath}. Empty is not clean.");

        return XDocument.Load(xmlPath);
    }

    [Theory]
    [MemberData(nameof(AllExports))]
    public void RealExport_MemoryLayout_SurvivesTheIrRoundTrip(string block, string exportPath, string irDir)
    {
        var expected = DeclaredLayouts(XDocument.Load(exportPath));
        var actual = DeclaredLayouts(RoundTrip(block, exportPath, irDir));

        Assert.True(expected.SequenceEqual(actual, StringComparer.Ordinal),
            $"{block}: MemoryLayout did not survive export -> to-ir -> to-xml." +
            $"{Environment.NewLine}  TIA's export declares: [{string.Join(", ", expected)}]" +
            $"{Environment.NewLine}  ours declares:         [{string.Join(", ", actual)}]" +
            Environment.NewLine +
            "Dropping it is not benign: the import then states no opinion, TIA applies the S7-1200 " +
            "default (Optimized), and a Standard block becomes invisible to classic S7comm with every " +
            "Normalizer-based check still green.");
    }

    /// <summary>
    /// The did-not-run case, in both of its forms. An empty export population makes the Theory above
    /// vacuous; a population where NOTHING declares a layout makes every one of its cases the trivial
    /// "declared none, still none" pass, which looks identical in the run output.
    /// </summary>
    [Fact]
    public void TheCorpus_ActuallyExercisesTheLayoutAssertion()
    {
        var exports = Exports().ToList();
        Assert.True(exports.Count > 0, "no committed exports found — empty is not clean");

        var declaring = exports
            .Where(e => DeclaredLayouts(XDocument.Load(e.ExportPath)).Count > 0)
            .Select(e => e.Name)
            .ToList();

        Assert.True(declaring.Count > 0,
            $"none of the {exports.Count} committed exports declares a <MemoryLayout>, so every case of " +
            "RealExport_MemoryLayout_SurvivesTheIrRoundTrip is the trivial both-absent pass and the suite " +
            "is asserting nothing about layout at all.");
    }

    /// <summary>
    /// The value must be CARRIED, not assumed. Every committed export declares <c>Optimized</c> and
    /// none declares <c>Standard</c>, so the round-trip theory above cannot on its own distinguish
    /// "preserves what TIA said" from "always writes Optimized" — and Standard is the value that
    /// matters, because it is the one a purpose-built, wire-readable DB needs.
    ///
    /// <para>WHAT THIS COSTS: the input is a real TIA export with exactly one attribute VALUE changed,
    /// so it is no longer a verbatim TIA artifact. That is the whole of the synthesis — the structure,
    /// the members and the element's position are TIA's, and <c>Standard</c> is one of only two values
    /// <c>Siemens.Engineering.SW.Blocks.MemoryLayout</c> defines. It is not a substitute for a real
    /// Standard export in the corpus, which remains the honest way to close this and needs a Portal
    /// session (<c>block-layout --set Standard --yes</c>, then export).</para>
    ///
    /// <para>Run against both writers, because they are separate code paths:
    /// <c>DbSourceWriter</c> for a data block and <c>BlockSourceWriter</c> for a code block.</para>
    /// </summary>
    [Theory]
    [InlineData("SW.Blocks.GlobalDB")]
    [InlineData("SW.Blocks.FC")]
    public void MemoryLayoutValue_IsCarriedThrough_NotHardcoded(string rootElement)
    {
        var candidate = Exports().FirstOrDefault(e =>
        {
            var doc = XDocument.Load(e.ExportPath);
            return doc.Root?.Elements().Any(x => x.Name.LocalName == rootElement) == true
                && DeclaredLayouts(doc) is [var only] && only == "Optimized";
        });

        Assert.True(candidate.Name is not null,
            $"no committed export has a <{rootElement}> declaring exactly one MemoryLayout of 'Optimized', " +
            "so this test has no fixture and is measuring nothing. Empty is not clean.");

        var mutatedDir = Path.Combine(Path.GetTempPath(), "memory-layout-fidelity", "mutated");
        Directory.CreateDirectory(mutatedDir);
        var mutatedPath = Path.Combine(mutatedDir, candidate.Name + ".xml");

        var mutated = XDocument.Load(candidate.ExportPath);
        var element = mutated.Descendants().Single(e => e.Name.LocalName == "MemoryLayout");
        element.Value = "Standard";
        mutated.Save(mutatedPath);

        var actual = DeclaredLayouts(RoundTrip(candidate.Name, mutatedPath, candidate.IrDir));

        Assert.True(actual.SequenceEqual(new[] { "Standard" }, StringComparer.Ordinal),
            $"{candidate.Name} (<{rootElement}>): the round trip was given 'Standard' and returned " +
            $"[{string.Join(", ", actual)}]. The layout is not being carried through — it is being " +
            "assumed, which is indistinguishable from correct on a corpus where every export says " +
            "Optimized.");
    }

    /// <summary>
    /// The gap this file exists to cover, recorded as a number so it cannot drift unnoticed. While it
    /// is 0, no <see cref="Normalizer.AreSemanticallyEquivalent"/> call reachable from this suite —
    /// the parity oracle, the committed round-trip, the frozen answer keys, <c>drift-check</c> —
    /// compares a layout at all, and the Theory above is the only thing in the harness that does.
    ///
    /// <para>Going red here is GOOD NEWS: it means committed IR has been re-derived from its export
    /// and now declares a layout, so the Normalizer's comparison has armed itself for those blocks.
    /// Raise the baseline and say which blocks. (Editing <c>ir/</c> is the lad-coder sub-agent's job,
    /// CLAUDE.md hard rule 8 — this test only measures.)</para>
    /// </summary>
    [Fact]
    public void NoCommittedIr_DeclaresALayout_SoTheNormalizerComparisonIsDisarmed()
    {
        const int ArmedBaseline = 0;

        var repo = ToolPaths.RepoRoot();
        var irFiles = new[] { "reference", "test-project001" }
            .Select(p => Path.Combine(repo, "ir", p))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.ir"))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(irFiles.Count > 0, "no committed .ir found — empty is not clean");

        var armed = irFiles
            .Where(f => File.ReadAllLines(f).Any(l => l.TrimStart().StartsWith("MEMORYLAYOUT ", StringComparison.Ordinal)))
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        Assert.True(armed.Count == ArmedBaseline,
            $"{armed.Count} of {irFiles.Count} committed .ir files declare a MEMORYLAYOUT, baseline is " +
            $"{ArmedBaseline}: [{string.Join(", ", armed)}]. If this went UP, those blocks were re-derived " +
            "from their exports and AreSemanticallyEquivalent now compares layout for them — raise the " +
            "baseline. If it went DOWN below 0 something is very wrong.");
    }
}
