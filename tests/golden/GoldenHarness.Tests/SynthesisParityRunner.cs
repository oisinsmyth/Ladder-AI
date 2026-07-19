using System.Text;
using System.Xml.Linq;

namespace GoldenHarness;

/// <summary>
/// The synthesis-parity oracle (docs/notes/synthesis-parity-plan.md, 2026-07-18). For each block
/// that ships both a committed readable IR (`ir/reference/&lt;b&gt;.ir`, readable + real SIDECAR) and a
/// committed real export (`simatic-ml/reference/&lt;b&gt;.xml`): strip the sidecar, re-*derive* one via
/// `converter to-xml --synthesize`, and check the derived SimaticML is semantically equivalent to the
/// real export (<see cref="Normalizer"/>). The real export is the answer key — no hand-written spec,
/// and no live Portal (both sides are committed), so this runs in CI unlike
/// <see cref="ReferenceProjectRoundTrip"/> / <see cref="SynthesizerLiveCheck"/>.
///
/// A PASS means synthesis reached parity with the read side for that block's constructs. A FAIL at
/// the `synthesize` stage carries the synthesizer's own per-network unsupported-construct message
/// (`SidecarSynthesizer` `UnsupportedSynthesisConstructException`), which self-identifies the gap; a
/// FAIL at `compare` means it synthesized but the derived graph/types diverge from the real export.
/// </summary>
public sealed record ParityResult(string Block, bool Pass, string Stage, string Detail);

public static class SynthesisParityRunner
{
    /// <summary>
    /// The reference corpus (dependency order irrelevant here — each block is independent). Same set
    /// as <see cref="ReferenceProjectRoundTrip"/>; DBs/UDTs are included — `--synthesize` is a no-op
    /// for a block with no FlgNet, so they act as trivial-pass controls.
    /// </summary>
    public static readonly string[] Blocks =
    {
        "CommsProcessData",
        "AlarmWords",
        "EquipmentStatus",
        "NodeStatusAlarms",
        "PerimeterSafetyAlarms",
        "DB_Timers",
        "TimerSample",
        "ThresholdAlarms",
        "SignalConditioning",
        "DataHandling",
        "BooleanExtras",
        "FBTimers",
        "ScaleValue",
        "TimingAndCalls",
        // Hand-authored split/merge fixture (2026-07-19): the SPLIT grammar makes contact fan-out
        // derivable — five split networks + five split-free equivalents, all round-tripping exactly.
        "HandAuthorSplitsMerges",
    };

    /// <summary>
    /// Everything before the lone `SIDECAR` delimiter line — the readable IR a fresh synthesis must
    /// reconstruct a sidecar for. (The sidecar is appended once per file, ir/SPEC.md §Sidecar.)
    /// </summary>
    public static string StripSidecar(string irText)
    {
        var builder = new StringBuilder();
        foreach (var line in irText.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Trim() == "SIDECAR")
            {
                break;
            }

            builder.Append(line).Append('\n');
        }

        return builder.ToString();
    }

    public static ParityResult Run(string block, string irDir, string xmlDir, string workDir)
    {
        var irPath = Path.Combine(irDir, block + ".ir");
        var answerKeyPath = Path.Combine(xmlDir, block + ".xml");
        if (!File.Exists(irPath))
        {
            return new ParityResult(block, false, "setup", $"missing IR {irPath}");
        }

        if (!File.Exists(answerKeyPath))
        {
            return new ParityResult(block, false, "setup", $"missing answer key {answerKeyPath}");
        }

        var readablePath = Path.Combine(workDir, block + ".ir");
        File.WriteAllText(readablePath, StripSidecar(File.ReadAllText(irPath)));

        // --project points at the readable IR dir so wired CALLs can resolve callee interfaces.
        var toXml = ProcessRunner.Run(ToolPaths.ConverterExe, "to-xml", readablePath, "--synthesize", "--project", irDir);
        if (toXml.ExitCode != 0)
        {
            return new ParityResult(block, false, "synthesize", FirstMeaningfulLine(toXml.StdErr, toXml.StdOut));
        }

        var synthPath = Path.ChangeExtension(readablePath, ".xml");
        XDocument real;
        XDocument synth;
        try
        {
            real = XDocument.Load(answerKeyPath);
            synth = XDocument.Load(synthPath);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException)
        {
            return new ParityResult(block, false, "load", ex.Message);
        }

        var equal = Normalizer.AreSemanticallyEquivalent(real, synth);
        if (!equal)
        {
            // Leave the two normalized forms next to the synth XML so the divergence is one diff
            // away — a `compare` failure is always either a real synthesis gap or a Normalizer
            // under-strip, and telling them apart means seeing exactly what differs.
            File.WriteAllText(Path.Combine(workDir, block + ".real.norm.xml"), Normalizer.Strip(real.Root!).ToString());
            File.WriteAllText(Path.Combine(workDir, block + ".synth.norm.xml"), Normalizer.Strip(synth.Root!).ToString());
        }

        return new ParityResult(block, equal, equal ? "ok" : "compare",
            equal ? string.Empty : "derived SimaticML not semantically equivalent to the real export");
    }

    public static IReadOnlyList<ParityResult> RunAll(string irDir, string xmlDir, string workDir)
    {
        Directory.CreateDirectory(workDir);
        return Blocks.Select(b => Run(b, irDir, xmlDir, workDir)).ToList();
    }

    /// <summary>Renders the results as a Markdown matrix (committed as the living gap catalogue).</summary>
    public static string RenderMatrix(IReadOnlyList<ParityResult> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Synthesis-parity matrix");
        builder.AppendLine();
        builder.AppendLine($"Generated by `SynthesisParityTests` over the reference corpus. {results.Count(r => r.Pass)}/{results.Count} blocks reach parity.");
        builder.AppendLine();
        builder.AppendLine("| Block | Parity | Stage | Blocker |");
        builder.AppendLine("|---|---|---|---|");
        foreach (var r in results)
        {
            var blocker = r.Detail.Replace("|", "\\|").Replace("\n", " ").Trim();
            builder.AppendLine($"| {r.Block} | {(r.Pass ? "PASS" : "FAIL")} | {r.Stage} | {blocker} |");
        }

        return builder.ToString();
    }

    private static string FirstMeaningfulLine(string stdErr, string stdOut)
    {
        var text = string.IsNullOrWhiteSpace(stdErr) ? stdOut : stdErr;
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith("Usage:", StringComparison.Ordinal))
            {
                return trimmed;
            }
        }

        return text.Trim();
    }
}
