using System.Xml.Linq;
using Xunit;

namespace GoldenHarness;

/// <summary>
/// Phase-4 own-sidecar oracle (ADR-0006 / ADR-0005). The blocks that fan-out made derivable — MotorStarter
/// and the three test-project001 FBs — are committed readable-only, guarded against a FROZEN answer key:
/// `to-xml` of each block's own stored sidecar at drop time, captured under `tests/golden/answer-keys/`.
///
/// Why not the `simatic-ml/` export (like <see cref="CommittedBlocksRoundTripTests"/>): those exports have
/// drifted from the committed `.ir` (fixes never re-exported — e.g. FB_ShredderSequencer N3, FB_PusherControl
/// missing a whole network), and MotorStarter has no export at all. The frozen own-sidecar key is
/// staleness-immune and needs no live TIA — it proves synthesis reproduces the block's own correct wiring,
/// which is exactly the ADR-0005 condition for storing a block readable-only.
/// </summary>
public class FrozenAnswerKeyRoundTripTests
{
    // The blocks dropped to readable-only, each guarded against its frozen own-sidecar key. MotorStarter and
    // FB_MotorFwdRevSystem are NOT here yet — the oracle proved they are not byte-exact (a constant-typing
    // gap: a MOVE `IN`/box input literal is typed by magnitude, `Int`, where the real export types it to the
    // UDInt destination — plus a residual wiring diff on FB_MotorFwdRevSystem). They keep their stored
    // sidecars until that is fixed; add them here when it is.
    public static IEnumerable<object[]> Blocks()
    {
        var repo = ToolPaths.RepoRoot();
        yield return new object[] { "FB_ShredderSequencer", Path.Combine(repo, "ir", "test-project001", "FB_ShredderSequencer.ir") };
        yield return new object[] { "FB_PusherControl", Path.Combine(repo, "ir", "test-project001", "FB_PusherControl.ir") };
    }

    [Theory]
    [MemberData(nameof(Blocks))]
    public void ReadableOnly_SynthesizesEquivalentToFrozenAnswerKey(string block, string irPath)
    {
        var repo = ToolPaths.RepoRoot();
        var answerKeyPath = Path.Combine(repo, "tests", "golden", "answer-keys", block + ".xml");
        Assert.True(File.Exists(answerKeyPath), $"missing frozen answer key {answerKeyPath}");

        var irDir = Path.GetDirectoryName(irPath)!;
        var workDir = Path.Combine(Path.GetTempPath(), "frozen-answer-key");
        Directory.CreateDirectory(workDir);

        // Strip any stored sidecar and synthesize — the exact derive path `to-xml` uses on a readable-only
        // block. (Works whether or not the committed `.ir` still carries a sidecar, so it is the same guard
        // before and after the drop.)
        var readablePath = Path.Combine(workDir, block + ".ir");
        File.WriteAllText(readablePath, SynthesisParityRunner.StripSidecar(File.ReadAllText(irPath)));

        var toXml = ProcessRunner.Run(ToolPaths.ConverterExe, "to-xml", readablePath, "--synthesize", "--project", irDir);
        Assert.True(toXml.ExitCode == 0, $"{block}: to-xml --synthesize failed — {toXml.StdErr}{toXml.StdOut}");

        var synthPath = Path.ChangeExtension(readablePath, ".xml");
        var real = XDocument.Load(answerKeyPath);
        var synth = XDocument.Load(synthPath);

        if (!Normalizer.AreSemanticallyEquivalent(real, synth))
        {
            File.WriteAllText(Path.Combine(workDir, block + ".real.norm.xml"), Normalizer.Strip(real.Root!).ToString());
            File.WriteAllText(Path.Combine(workDir, block + ".synth.norm.xml"), Normalizer.Strip(synth.Root!).ToString());
            Assert.Fail($"{block}: synthesis does not reproduce its own stored wiring (frozen answer key) — " +
                "unsafe to store readable-only. Normalized forms written next to the synth XML.");
        }
    }
}
