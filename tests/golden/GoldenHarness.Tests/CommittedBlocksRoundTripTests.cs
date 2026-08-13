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
        // EMPTY, 2026-08-13 — and empty is the GOOD state here, not a broken enumeration. The single
        // entry was `FB_HopperBlockageMonitor` ("generated 2026-07-xx and never taken through a live
        // TIA export"); commit `c49f5e9` added the three hopper-blockage exports, so it and its UDT and
        // instance DB are all covered now. `KnownMissingExport_IsStillMissing` is what noticed, exactly
        // as it was built to.
    };

    /// <summary>
    /// Blocks whose committed export is NOT A FAITHFUL TIA ARTIFACT, so the divergence is in the answer
    /// key rather than in our synthesis. A THIRD category, distinct from both of the ones this project
    /// already had — it is neither "a fix landed in the .ir and was never re-exported" (the D-7 deferred
    /// class) nor "drift nobody had noticed" — and keeping it separate matters, because a flat list of
    /// tolerated names is exactly what let two real UDT drifts sit invisible (see
    /// <see cref="BlockInterfaceFidelityTests"/>).
    ///
    /// Asserted in BOTH directions: an entry here must STILL diverge, so a fresh export silently
    /// closing the gap turns the suite red and tells you to delist it.
    /// </summary>
    internal static readonly Dictionary<string, string> KnownIncompleteAnswerKeys = new(StringComparer.Ordinal)
    {
        // Surfaced 2026-08-13 by converter `ae62f76` (an Interface is compared, not discarded).
        // NodeStatusAlarms is one of four 2026-07-10 seed artifacts committed with their TIA scaffolding
        // trimmed — they carry no <DocumentInfo> either. On this one, an FC, the <Interface> went with
        // the trim: the export has NO Interface element at all, while any regenerated FC emits the
        // standard boilerplate (a real TIA FC export carries one — see simatic-ml/reference/ScaleValue.xml).
        // MEASURED: `converter compare` localises exactly ONE difference, ELEMENT-ADDED at
        // /Document/SW.Blocks.FC/AttributeList/Interface. Nothing about the logic diverges.
        // OUR OUTPUT IS RIGHT AND THE ANSWER KEY IS INCOMPLETE. To clear: re-export the block from TIA.
        ["NodeStatusAlarms"] = "committed export is a trimmed 2026-07-10 seed artifact with no <Interface>; the sole difference is that element, and it is an ADDITION of TIA's own defaults.",
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

    [Theory]
    [MemberData(nameof(ReadableOnlyBlocks))]
    public void ReadableOnlyBlock_SynthesizesEquivalentToItsExport(string block, string irDir, string xmlDir)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "committed-roundtrip");
        Directory.CreateDirectory(workDir);

        var result = SynthesisParityRunner.Run(block, irDir, xmlDir, workDir);

        if (KnownIncompleteAnswerKeys.TryGetValue(block, out var reason))
        {
            Assert.False(result.Pass,
                $"{block} is listed in KnownIncompleteAnswerKeys ({reason}) but now round-trips equivalent " +
                "to its export. Remove the entry — the answer key has been refreshed and the block is " +
                "guarded by the assertion below now.");
            return;
        }

        Assert.True(result.Pass,
            $"{block} is committed readable-only but does not round-trip equivalent to its export — " +
            $"[{result.Stage}] {result.Detail}. Either restore its stored sidecar (converter to-ir --no-sidecar " +
            "would refuse) or close the synthesis gap that makes it diverge. If instead the EXPORT is at " +
            "fault (not a faithful TIA artifact), that is KnownIncompleteAnswerKeys — and say which, " +
            "because a flat list of tolerated names is what let two real UDT drifts sit invisible.");
    }

    /// <summary>
    /// The loud half of the exclusion (audit F-20, 2026-08-05). A readable-only block with no export is
    /// unverifiable by this suite, which is a real hole — so it is named rather than vanishing from the
    /// run. Passing asserts only "every uncovered block is one we know about"; it does NOT assert that
    /// any block round-trips.
    ///
    /// <para>*** WAS A [Theory] UNTIL 2026-08-13, AND THAT WAS A DEFECT. *** Its <c>MemberData</c>
    /// enumerates only the blocks WITH a gap, so the moment the last gap closed — the good state, which
    /// `c49f5e9` produced — xUnit failed the whole theory with "No data found". A test that cannot
    /// express success is a test that punishes the fix. It is a <c>[Fact]</c> over the whole population
    /// now: vacuously true when there are no gaps, and the "empty is not clean" concern is put where it
    /// actually belongs — on the OVERALL enumeration, which must never be empty, because that would
    /// mean the corpus walk itself broke.</para>
    /// </summary>
    [Fact]
    public void EveryReadableOnlyBlock_IsEitherCovered_OrAKnownGap()
    {
        var candidates = ReadableOnlyBlockCandidates().ToList();
        Assert.True(candidates.Count > 0,
            "no committed readable-only BLOCK .ir found at all — the corpus walk is broken, and an empty " +
            "population would let every assertion below pass vacuously. Empty is not clean.");

        var uncoveredAndUnnamed = candidates
            .Where(c => !c.HasExport && !KnownMissingExports.Contains(c.Name))
            .Select(c => $"{c.Name} (expected {Path.Combine(c.XmlDir, c.Name + ".xml")})")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        Assert.True(uncoveredAndUnnamed.Count == 0,
            "committed readable-only block(s) with no export, so nothing in this suite checks that they " +
            $"synthesize correctly:\n  {string.Join("\n  ", uncoveredAndUnnamed)}\n" +
            "Export them and commit the .xml, or — if deliberately out of corpus — add them to " +
            "CommittedBlocksRoundTripTests.KnownMissingExports with the reason. Silently leaving one " +
            "uncovered is the option this test exists to remove.");
    }

    /// <summary>
    /// Staleness guard for the allowlist above: once a gap is closed, the entry must go, or the list
    /// starts documenting a state of the world that is no longer true.
    ///
    /// <para>*** A [Fact], NOT A [Theory], AND THAT IS THE POINT. *** A theory over a KNOWN-PROBLEM LIST
    /// fails with "No data found" the moment the list empties — which is the state you are working
    /// towards. It punished its own fix here on 2026-08-13 when `c49f5e9` closed the last gap. Sibling
    /// of the same defect in <see cref="EveryReadableOnlyBlock_IsEitherCovered_OrAKnownGap"/>; both are
    /// Facts now. (A theory over a CORPUS enumeration is different and stays a theory — an empty corpus
    /// really is broken.)</para>
    /// </summary>
    [Fact]
    public void KnownMissingExport_IsStillMissing()
    {
        var candidates = ReadableOnlyBlockCandidates().ToList();
        var stale = new List<string>();

        foreach (var block in KnownMissingExports)
        {
            var covered = candidates.Where(c => c.Name == block).ToList();
            if (covered.Count == 0)
            {
                stale.Add($"{block} — no longer a committed readable-only BLOCK .ir in a covered project");
                continue;
            }

            foreach (var c in covered.Where(c => c.HasExport))
            {
                stale.Add($"{block} — its export now exists at {Path.Combine(c.XmlDir, block + ".xml")}");
            }
        }

        Assert.True(stale.Count == 0,
            "KnownMissingExports entr(ies) no longer describe the world:" + Environment.NewLine + "  " +
            string.Join(Environment.NewLine + "  ", stale) + Environment.NewLine +
            "Remove them — they are covered by the round-trip theory now.");
    }
}
