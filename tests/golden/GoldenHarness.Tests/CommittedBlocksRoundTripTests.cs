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
/// Covers every `ir/&lt;project&gt;/` block that has a matching `simatic-ml/&lt;project&gt;/` export —
/// and, since 2026-08-05, NAMES the ones that don't (see <see cref="KnownMissingExports"/>).
/// </summary>
public class CommittedBlocksRoundTripTests
{
    private static (string IrDir, string XmlDir)[] CoveredProjects()
    {
        var repo = ToolPaths.RepoRoot();
        return new[]
        {
            (Path.Combine(repo, "ir", "reference"), Path.Combine(repo, "simatic-ml", "reference")),
            (Path.Combine(repo, "ir", "test-project001"), Path.Combine(repo, "simatic-ml", "test-project001")),
        };
    }

    /// <summary>
    /// `BLOCK` .ir files knowingly committed with no paired `simatic-ml/` export, so this suite cannot
    /// check them. Until 2026-08-05 such a block was dropped by a bare `if (File.Exists(...))` that
    /// yielded nothing — a *silent* exclusion, indistinguishable in the run output from a block that
    /// was never committed at all (audit F-20). Naming them here keeps the gap visible without turning
    /// the suite red over a corpus decision the owner has not made yet (audit F-19: whether these get
    /// exported, or the corpus grows differently, is open).
    ///
    /// This list is asserted in both directions:
    ///   - a NEW unpaired block that is not listed fails <see cref="EveryReadableOnlyBlock_IsEitherCovered_OrAKnownGap"/>;
    ///   - an entry that HAS gained an export fails <see cref="KnownMissingExport_IsStillMissing"/>,
    ///     so a closed gap cannot linger here pretending to still be one.
    /// </summary>
    private static readonly string[] KnownMissingExports =
    {
        // Generated 2026-07-xx and never taken through a live TIA export; its UDT and instance DB are
        // missing for the same reason, but those are excluded earlier as non-BLOCK content.
        "FB_HopperBlockageMonitor",
    };

    /// <summary>
    /// Every committed readable-only `BLOCK` .ir, paired with whether its export exists. One case per
    /// block either way, so the run output lists the whole population rather than only the checkable
    /// part of it.
    /// </summary>
    private static IEnumerable<(string Name, string IrDir, string XmlDir, bool HasExport)> ReadableOnlyBlockCandidates()
    {
        var repo = ToolPaths.RepoRoot();

        foreach (var (irDir, xmlDir) in CoveredProjects())
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

                yield return (name, irDir, xmlDir, File.Exists(Path.Combine(xmlDir, name + ".xml")));
            }
        }
    }

    public static IEnumerable<object[]> ReadableOnlyBlocks() =>
        ReadableOnlyBlockCandidates()
            .Where(c => c.HasExport)
            .Select(c => new object[] { c.Name, c.IrDir, c.XmlDir });

    public static IEnumerable<object[]> ReadableOnlyBlocksWithoutExport() =>
        ReadableOnlyBlockCandidates()
            .Where(c => !c.HasExport)
            .Select(c => new object[] { c.Name, c.XmlDir });

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

    /// <summary>
    /// The loud half of the exclusion (audit F-20, 2026-08-05). A readable-only block with no export is
    /// unverifiable by this suite, which is a real hole — so it gets its own named, listed test case
    /// instead of vanishing from the run. Passing here asserts only "we know about this one"; it does
    /// NOT assert the block round-trips.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadableOnlyBlocksWithoutExport))]
    public void EveryReadableOnlyBlock_IsEitherCovered_OrAKnownGap(string block, string xmlDir)
    {
        Assert.True(KnownMissingExports.Contains(block),
            $"'{block}' is committed readable-only but has no export at '{Path.Combine(xmlDir, block + ".xml")}', " +
            "so nothing in this suite checks that it synthesizes correctly. Export it and commit the .xml, or — if " +
            "it is deliberately out of corpus — add it to CommittedBlocksRoundTripTests.KnownMissingExports with " +
            "the reason. Silently leaving it uncovered is the one option this test exists to remove.");
    }

    /// <summary>
    /// Staleness guard for the allowlist above: once a gap is closed, the entry must go, or the list
    /// starts documenting a state of the world that is no longer true.
    /// </summary>
    [Theory]
    [MemberData(nameof(KnownMissingExportNames))]
    public void KnownMissingExport_IsStillMissing(string block)
    {
        var covered = ReadableOnlyBlockCandidates().Where(c => c.Name == block).ToList();

        Assert.True(covered.Count > 0,
            $"'{block}' is listed in KnownMissingExports but is no longer a committed readable-only BLOCK .ir " +
            "in a covered project. Remove the entry.");

        Assert.All(covered, c => Assert.False(c.HasExport,
            $"'{block}' is listed in KnownMissingExports but its export now exists at " +
            $"'{Path.Combine(c.XmlDir, block + ".xml")}'. Remove the entry — it is covered by the round-trip " +
            "theory now."));
    }

    public static IEnumerable<object[]> KnownMissingExportNames() =>
        KnownMissingExports.Select(name => new object[] { name });
}
