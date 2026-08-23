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
    /// was never committed at all (audit F-20). Naming them here keeps the gap visible.
    ///
    /// <para><b>AUDIT F-19 IS CLOSED, 2026-08-23, AND THE DECISION IS "DO NOT EXPORT THESE SEVEN".</b>
    /// F-19 had left it open whether the harness blocks get exported or the corpus grows differently.
    /// The settling argument is <c>FC_HarnessCopyLayer</c>: it is REGENERATED from the harness binding
    /// on every deployment, so committing an export of it would freeze one binding's output as the
    /// answer key and guarantee **perpetual drift in the very baseline this suite exists to keep
    /// honest**. The other six are harness scaffolding in the reserved 9000-9999 block-number band
    /// (`CLAUDE.md`), and this corpus is reviewer-skill validation data for blocks a reviewer reads as
    /// part of a delivery. Each entry below carries its OWN reason, because they are not the same
    /// block seven times — one is generated, one simulates the plant, one observes it, and four are
    /// tooling test material for four different tooling questions.</para>
    ///
    /// <para>*** SAY WHAT THIS COSTS: IT CONVERTS A RED INTO A DOCUMENTED HOLE. *** Seven readable-only
    /// blocks are now permanently unverified by this suite, and naming them does not verify them. What
    /// it buys is that the hole is enumerated and asserted in both directions rather than being a
    /// silent `File.Exists` false — and that a NEW unexported block still turns the suite red, which is
    /// the property the naming exists to preserve.</para>
    ///
    /// <para>🔴 A qualification carried deliberately rather than smoothed away (2026-08-23 IR read):
    /// "not project content" is NOT the same as "harmless in the project" — see the
    /// <c>FB_HopperBlockageStim</c> entry. And do NOT restate the four Hx blocks' own claim that they
    /// are "never called by the plant program": <c>Main.ir</c> networks 9-12 DO call them from OB1. The
    /// defensible claim is ISOLATION — they reference no PLC tag, no global DB, no UDT and no other
    /// block — not a claim about not being called.</para>
    ///
    /// <para>Not listed here and not an omission: <c>FB_Comms_ModbusServer</c> (FB 9000) is also
    /// unexported, but it carries a stored `SIDECAR` and is excluded from this population one method
    /// down, by the rule that a sidecar-carrying block is allowed to be non-derivable. It is not a gap
    /// this list is meant to hold. <c>HarnessMirror</c> (a tag table), <c>UDT_HopperBlockageStim</c>
    /// and the harness instance DBs are likewise out of scope — this list is `BLOCK` documents only.</para>
    ///
    /// <para>*** WAS A `string[]` UNTIL 2026-08-23, AND THE COMMENT BELOW ALREADY ASKED FOR SOMETHING
    /// IT COULD NOT HOLD *** — <see cref="EveryReadableOnlyBlock_IsEitherCovered_OrAKnownGap"/> told
    /// you to add a block "with the reason" and the type had nowhere to put one. Same lesson, and the
    /// same shape, as the drift baseline's move off a flat `string[]`
    /// (<see cref="ExportDriftDetectorTests"/>): a list of tolerated names that cannot say WHY a name
    /// is on it stops being a decision and becomes furniture. Filling it with seven names under one
    /// shared reason would have reproduced the defect in a dictionary.</para>
    ///
    /// This list is asserted in both directions:
    ///   - a NEW unpaired block that is not listed fails <see cref="EveryReadableOnlyBlock_IsEitherCovered_OrAKnownGap"/>;
    ///   - an entry that HAS gained an export fails <see cref="KnownMissingExport_IsStillMissing"/>,
    ///     so a closed gap cannot linger here pretending to still be one.
    /// </summary>
    private static readonly Dictionary<string, string> KnownMissingExports = new(StringComparer.Ordinal)
    {
        // It was EMPTY from 2026-08-13, and empty was the GOOD state: the single prior entry,
        // `FB_HopperBlockageMonitor`, gained its export in `c49f5e9` and
        // `KnownMissingExport_IsStillMissing` is what noticed. What refilled it is not that gap
        // reopening — it is the test-environment harness landing in `ir/test-project001/` between
        // 2026-08-13 and 2026-08-14 and never being ruled on.

        // ---- GENERATED, AND THAT IS THE WHOLE ARGUMENT --------------------------------------------
        // FC 9001. Emitted by `src/harness/Harness.Map/CopyLayerGenerator.cs` from the harness binding
        // — `Harness.Run/LoopCli.cs:722` passes `binding.BlockName ?? "FC_HarnessCopyLayer"`, and the
        // generator's network titles ("Program version", "Free-running scan counter",
        // "Vector in - slot {SlotId} - {Type}", ...) match the committed IR. It is pure plumbing: every
        // network is a MOVE or a COIL between an `HX_*` mirror tag and an instance-DB member, and it
        // decides nothing.
        //
        // 🔴 THE DRIFT IS DEMONSTRABLE, NOT PREDICTED. TWO committed bindings target this same block
        // name and number and would generate DIFFERENT bodies:
        // `gen/test-project001/hopper-blockage-alarm/harness-binding.json` (slot HBA) and
        // `gen/test-project001/hx-corpus/harness-binding.json` (blockNumber 9001, slots HXE/HXD/HXS/HXL).
        // The committed .ir carries only the HBA slot — so it is already one binding's output frozen,
        // and already wrong for the other binding in the same repo. An export of it would be an answer
        // key that is stale the next time anyone deploys.
        ["FC_HarnessCopyLayer"] = "FC 9001, GENERATED per deployment by Harness.Map/CopyLayerGenerator from the harness binding; two committed bindings target the same block and emit different bodies, so a committed export would be a permanently-drifting answer key.",

        // ---- HAND-AUTHORED SCAFFOLDING, ONE ROLE EACH ---------------------------------------------
        // FB 9002 (NOT the 9010+ corpus band). Hand-authored, `45e972c`; no harness source emits it.
        // The plant SIMULATOR for the hopper-blockage runs: it synthesises a commanded timeline of
        // three sensor signals from a Profile code and four durations. It makes no claim about the
        // plant — the logic is a run latch, one TON, a decode of the profile's tens digit into one of
        // six timeline shapes, and phase flags; a sensor state falls out of `Shape = 3`, not out of
        // when a hopper actually blocks. It publishes its OWN commanded threshold belief rather than
        // reading the monitor's, deliberately, so that a reset placed at the crossing stays a stimulus
        // decision. The plant claim lives in `FB_HopperBlockageMonitor`, which IS exported.
        //
        // 🔴 AND IT IS THE ONE OF THE SEVEN THAT CAN OVERRIDE THE FIELD. It writes `DB_Input.Test[..]`
        // and `DB_Controls.FaultReset`, gated on `Running`, and `Main` calls it at network 1 AHEAD of
        // the input map at network 2 — so while a run is under way it IS the field for three real DI
        // lines. That is the test-injection facility the exported input map already provides, so it
        // does not make the block project content; it does mean a delivery review would want to
        // confirm the block is ABSENT from the delivered program. "Not project content" is not the same
        // as "harmless in the project", and this list should not be read as saying it is.
        ["FB_HopperBlockageStim"] = "FB 9002, hand-authored (45e972c) plant SIMULATOR: a commanded timeline of three sensor signals from a Profile code and four durations, publishing its own threshold belief so the stimulus stays a stimulus decision. Test material, not equipment logic — but it writes DB_Input.Test[..]/DB_Controls.FaultReset ahead of the input map, so it must be absent from a delivery, not merely unreviewed.",

        // FB 9003. Hand-authored, `92fa300`; the harness source names it as external in prose
        // (`Harness.Map/CopyLayer.cs:252`, `CopyLayerGenerator.cs:210` both say "the hand-authored
        // FB_HarnessViolationLatch"). A read-only OBSERVER: it latches four "this never happens"
        // states so a violation lasting one scan survives to be read — its own reason being that a
        // sampler looking a hundred times still looks between scans, and a miss reports as a pass.
        // It reads two instance DBs and writes only its own six statics; no coil of its leaves the
        // instance DB, so it cannot influence plant behaviour. Coupled to the requirement register for
        // MAINTENANCE (change what the register forbids and this must change), which is a reason to
        // keep it visible, not a reason to call it deliverable logic.
        ["FB_HarnessViolationLatch"] = "FB 9003, hand-authored (92fa300) read-only OBSERVER: latches four forbidden states so a one-scan violation survives sampling. Writes only its own statics — nothing downstream reads its output. Tracks the requirement register for maintenance, but carries no plant claim.",

        // ---- THE Hx CORPUS: FOUR TOOLING QUESTIONS, NOT FOUR COPIES OF ONE BLOCK ------------------
        // All four hand-authored together, `b3f6736`, 2026-08-14; grep across `src/harness` for their
        // names returns nothing, so no generator knows they exist. `gen/test-project001/hx-corpus/
        // requirements.md` states the provenance and the status outright: authored manually as
        // artificial test material, and "no clause here is a plant requirement". Their isolation is
        // the load-bearing property — each references no PLC tag, no global DB, no UDT and no other
        // block; every signal is a member of its own instance DB. (They ARE called, from Main networks
        // 9-12; the block comments' "never called by the plant program" is not the claim to rely on.)
        ["FB_HxBoolEcho"] = "FB 9010, hand-authored (b3f6736) Hx corpus — the ZERO-STATE case: two combinational coils, no memory, so a run needs no clear-down or arming window. The complement output exists so a stuck-low result register cannot read as correct in both states.",
        ["FB_HxDwellTimer"] = "FB 9011, hand-authored (b3f6736) Hx corpus — the TIMING and 32-BIT-WIDTH case: the only corpus block carrying Time values and a timer instance. The preset echo pins the mirror's word order, so a word swap shows as a wrong value instead of a plausible duration.",
        ["FB_HxIntStep"] = "FB 9012, hand-authored (b3f6736) Hx corpus — the 16-BIT ARITHMETIC and DISJOINT MULTI-WRITER case: StepSum is written by exactly two statements with provably disjoint enables, which is the shape `converter reachable-state` exists to adjudicate.",
        ["FB_HxSealLatch"] = "FB 9013, hand-authored (b3f6736) Hx corpus — the MEMORY / DOMINANCE case: two self-holding seals differing only in clear-dominant vs set-dominant. The pair discriminates wrong dominance, which a single latch cannot report.",
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
            .Where(c => !c.HasExport && !KnownMissingExports.ContainsKey(c.Name))
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

        foreach (var block in KnownMissingExports.Keys)
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
