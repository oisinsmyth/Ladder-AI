using Xunit;

namespace GoldenHarness;

/// <summary>
/// Derive-always safety net (ADR-0005, 2026-07-19): every committed *readable-only* code block (one
/// stored with no SIDECAR) must synthesize to a form semantically equivalent to its real export. This
/// guards against the exact hazard the migration surfaced — a block that synthesises *successfully* but
/// diverges (e.g. array-index locals, Gap D) being committed sidecar-less, so that a later `to-xml`
/// silently derives the wrong logic. A sidecar-carrying block is skipped: it keeps its faithful stored
/// sidecar precisely because it can't yet be safely derived.
///
/// Covers every `ir/&lt;project&gt;/` block that has a matching `simatic-ml/&lt;project&gt;/` export.
/// </summary>
public class CommittedBlocksRoundTripTests
{
    public static IEnumerable<object[]> ReadableOnlyBlocks()
    {
        var repo = ToolPaths.RepoRoot();
        var pairs = new[]
        {
            (Path.Combine(repo, "ir", "reference"), Path.Combine(repo, "simatic-ml", "reference")),
            (Path.Combine(repo, "ir", "test-project001"), Path.Combine(repo, "simatic-ml", "test-project001")),
        };

        foreach (var (irDir, xmlDir) in pairs)
        {
            if (!Directory.Exists(irDir))
            {
                continue;
            }

            foreach (var irPath in Directory.EnumerateFiles(irDir, "*.ir"))
            {
                var text = File.ReadAllText(irPath);
                if (!text.StartsWith("BLOCK ", StringComparison.Ordinal))
                {
                    continue; // DBs/UDTs/tag-tables have no sidecar concept
                }

                if (text.Contains("\nSIDECAR", StringComparison.Ordinal))
                {
                    continue; // sidecar-carrying: allowed to be non-derivable
                }

                var name = Path.GetFileNameWithoutExtension(irPath);

                // A block with a committed frozen answer key is guarded by FrozenAnswerKeyRoundTripTests
                // instead — its `simatic-ml/` export has drifted from the committed `.ir` (fixes never
                // re-exported), so the export-based check here would fail on correct synthesis.
                if (File.Exists(Path.Combine(repo, "tests", "golden", "answer-keys", name + ".xml")))
                {
                    continue;
                }

                if (File.Exists(Path.Combine(xmlDir, name + ".xml")))
                {
                    yield return new object[] { name, irDir, xmlDir };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(ReadableOnlyBlocks))]
    public void ReadableOnlyBlock_SynthesizesEquivalentToItsExport(string block, string irDir, string xmlDir)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "committed-roundtrip");
        Directory.CreateDirectory(workDir);

        var result = SynthesisParityRunner.Run(block, irDir, xmlDir, workDir);

        Assert.True(result.Pass,
            $"{block} is committed readable-only but does not round-trip equivalent to its export — " +
            $"[{result.Stage}] {result.Detail}. Either restore its stored sidecar (converter to-ir --no-sidecar " +
            "would refuse) or close the synthesis gap that makes it diverge.");
    }
}
