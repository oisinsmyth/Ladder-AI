using Xunit;
using Xunit.Abstractions;

namespace GoldenHarness;

/// <summary>
/// Offline synthesis-parity regression (docs/notes/synthesis-parity-plan.md). Runs the whole
/// reference corpus through <see cref="SynthesisParityRunner"/> — no Portal, both sides committed —
/// writes the living matrix to `tests/golden/synthesis-parity-matrix.md`, and fails only if a block
/// in <see cref="KnownGreen"/> regresses. As each synthesis gap closes, move its block into
/// KnownGreen so it becomes a hard guard; blocks not yet in the set are known gaps, tracked in the
/// matrix without turning CI red.
/// </summary>
public class SynthesisParityTests
{
    private readonly ITestOutputHelper _output;

    public SynthesisParityTests(ITestOutputHelper output) => _output = output;

    /// <summary>Blocks proven to reach synthesis parity. Grow this as Stage-1 gaps close.</summary>
    private static readonly HashSet<string> KnownGreen = new(StringComparer.Ordinal)
    {
        // Reach parity as of Stage 0 (2026-07-18). The four DBs synthesize trivially (no FlgNet);
        // the five code blocks exercise contacts/coils/OR-merge/negation, comparisons, TON, and the
        // box family. Grow this as Stage-1 gaps close (SUB/DIV, WordAnd, TONR, timer-instance-scope,
        // standalone-Not) — see docs/notes/converter-synthesis-gaps.md.
        "CommsProcessData",
        "AlarmWords",
        "EquipmentStatus",
        // NodeStatusAlarms was here until 2026-08-13 — moved to KnownIncompleteAnswerKey below. It is
        // NOT a synthesis gap (which is what everything outside KnownGreen otherwise means), and
        // mislabelling it as one is precisely the flattening this project has just been bitten by.
        "PerimeterSafetyAlarms",
        "DB_Timers",
        "ThresholdAlarms",
        "FBTimers",
        "ScaleValue",
        // 2026-07-18: the box family (MUL/CONVERT/SUB/DIV/ABS/SWAP) + Mul-to-Mul ENO chaining +
        // registry-typed ABS/SWAP.
        "SignalConditioning",
        // 2026-07-18: TONR/TOF synthesis + wired CALL, once the Normalizer learned Call/Instance/
        // OpenCon UIds are volatile too.
        "TimingAndCalls",
        // 2026-07-18 (Gap G2): a same-network timer .Q read wires directly from the TON's Q port
        // (a TimerOutputStep), matching how TIA exports it.
        "TimerSample",
        // 2026-07-18 (Gap H): a standalone Not part `NOT (X)` is now distinct from a negated contact
        // `NOT X` in the readable grammar, so synthesis rebuilds the right LAD element.
        "BooleanExtras",
        // 2026-07-18: the box family's last five — WAND/CALC/T_SUB/T_CONV/MOVE_BLK_VARIANT — closing
        // the corpus. Complete synthesis: all 14 reference blocks derive a sidecar matching their export.
        "DataHandling",
        // 2026-07-19: the SPLIT grammar — contact fan-out is now derivable (hand-authored split/merge pairs).
        "HandAuthorSplitsMerges",
    };

    /// <summary>
    /// Blocks that fail parity because their committed EXPORT is not a faithful TIA artifact — the
    /// answer key is wrong, not the synthesis. Deliberately not expressed as "absent from KnownGreen":
    /// that bucket means "a synthesis gap we have not closed yet", and a block filed under the wrong
    /// reason is a block nobody goes back to.
    ///
    /// Asserted in both directions — an entry that starts passing turns the suite red so it gets moved
    /// into <see cref="KnownGreen"/>, where it becomes a hard guard again.
    /// </summary>
    internal static readonly Dictionary<string, string> KnownIncompleteAnswerKey = new(StringComparer.Ordinal)
    {
        ["NodeStatusAlarms"] = "committed export is a trimmed 2026-07-10 seed artifact carrying no <Interface>; sole difference is that element (measured with `converter compare`). Clears on a fresh export — see CommittedBlocksRoundTripTests.KnownIncompleteAnswerKeys.",
    };

    [Fact]
    public void ReferenceCorpus_SynthesisMatchesRealExport()
    {
        var repoRoot = ToolPaths.RepoRoot();
        var irDir = Path.Combine(repoRoot, "ir", "reference");
        var xmlDir = Path.Combine(repoRoot, "simatic-ml", "reference");
        var workDir = Path.Combine(Path.GetTempPath(), "synthesis-parity");

        var results = SynthesisParityRunner.RunAll(irDir, xmlDir, workDir);
        var matrix = SynthesisParityRunner.RenderMatrix(results);

        File.WriteAllText(Path.Combine(repoRoot, "tests", "golden", "synthesis-parity-matrix.md"), matrix);
        _output.WriteLine(matrix);

        var regressions = results
            .Where(r => KnownGreen.Contains(r.Block) && !r.Pass)
            .Select(r => $"{r.Block}: [{r.Stage}] {r.Detail}")
            .ToList();

        Assert.True(regressions.Count == 0,
            "Synthesis-parity regressions in KnownGreen blocks:\n" + string.Join("\n", regressions) + "\n\n" + matrix);

        // Staleness, the other direction: a block filed as "the answer key is incomplete" that now
        // passes has had its export refreshed, and must be promoted into KnownGreen — otherwise the
        // set quietly becomes a list of excuses nobody rechecks.
        var recovered = results
            .Where(r => KnownIncompleteAnswerKey.ContainsKey(r.Block) && r.Pass)
            .Select(r => r.Block)
            .ToList();

        Assert.True(recovered.Count == 0,
            $"[{string.Join(", ", recovered)}] reach parity now but are still listed in " +
            "KnownIncompleteAnswerKey. Move them into KnownGreen so they become hard guards again.\n\n" + matrix);
    }
}
