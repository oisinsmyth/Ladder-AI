using System.Diagnostics;
using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE SECOND OUTSIDE AUTHORITY: the real converter, run as a process, over the real generated IR.</b>
///
/// <para>Two tests in this project were named <c>..._is_the_IR_the_converter_round_trips</c> and <b>neither
/// of them ran the converter</b> — the round trip had been done once, by hand, and the name kept the claim
/// alive long after anything checked it. They are now named for what they are (golden text), and this is
/// the check that actually makes the claim.</para>
///
/// <para><b>WHAT THIS PROVES AND, MORE IMPORTANTLY, WHAT IT DOES NOT.</b> It proves the generated IR is
/// GRAMMATICAL — that the converter parses it, emits SimaticML, and reads that back to the same text.
/// *** IT PROVES NOTHING ABOUT TYPES. *** Measured directly on the artifact TIA rejected: with
/// <c>--project ir/test-project001</c>, so member types WERE resolved, <c>converter to-xml</c> returned
/// exit 0 and <c>converter preflight</c> reported one unrelated finding. <b>The converter converts a
/// Bool into a MOVE without complaint; only TIA refuses it.</b> So this file is a grammar check that can
/// disagree, sitting alongside <see cref="CopyLayerAgainstRealTiaExportTests"/>, which is the type/shape
/// check — and neither replaces the compile gate.</para>
/// </summary>
public class CopyLayerConverterRoundTripTests
{
    private static readonly CopyLayerNaming Naming = new(BlockNumber: 900);
    private static readonly BuildStamp Stamp = new(0xA93F2C71);

    /// <summary>
    /// The converter binary, Release first (what the skills invoke), Debug second.
    ///
    /// <para><b>Absent is a FAILURE, not a skip.</b> An optional check is one that stops running, and the
    /// defect this file exists for got through because nothing outside the component could fail.</para>
    /// </summary>
    private static string ConverterExe
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir is not null)
            {
                foreach (var configuration in new[] { "Release", "Debug" })
                {
                    var candidate = Path.Combine(dir.FullName, "src", "converter", "Converter", "bin", configuration, "net8.0", "converter.exe");
                    if (File.Exists(candidate))
                        return candidate;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException(
                "converter.exe was not found by walking up from " + AppContext.BaseDirectory
                + " for src/converter/Converter/bin/{Release,Debug}/net8.0/converter.exe. *** THIS IS A FAILURE, NOT A REASON TO SKIP. *** "
                + "Build it with: dotnet build -c Release src/converter/converter.sln  (free and safe at any time — the converter "
                + "never touches Portal). Without it, the only authority left in this component's loop is the component itself.");
        }
    }

    private static (int Exit, string Output) RunConverter(params string[] args)
    {
        // Redirected, never piped — and safe here: the converter is a pure in-process file transformer
        // with no child process, so it carries none of openness-cli's inherited-handle hazard.
        var info = new ProcessStartInfo(ConverterExe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in args)
            info.ArgumentList.Add(arg);

        using var process = Process.Start(info) ?? throw new InvalidOperationException("converter.exe did not start.");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output);
    }

    private static CopyLayerResult Mixed()
    {
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000),
            new[] { new SlotRequest("S0", 2, 3) })).Require();

        var binding = new SlotBinding(
            "S0",
            new[] { MirroredSignal.Int("DB_Unit.Setpoint"), MirroredSignal.Bool("DB_Unit.Enable") },
            "DB_Unit.StartCmd",
            new[] { MirroredSignal.Bool("DB_Unit.Alarm"), MirroredSignal.Int("DB_Unit.Actual"), MirroredSignal.Bool("DB_Unit.StopReq") });

        return CopyLayerGenerator.Generate(map, binding, Naming, Stamp);
    }

    [Fact]
    public void A_MIXED_BOOL_AND_INT_COPY_LAYER_ROUND_TRIPS_THROUGH_THE_REAL_CONVERTER()
    {
        var generated = Mixed().Objects;
        var block = generated.Single(o => o.Kind == HarnessObjectKind.Block);
        var table = generated.Single(o => o.Kind == HarnessObjectKind.TagTable);

        var work = Directory.CreateTempSubdirectory("copylayer-roundtrip-");
        try
        {
            var irPath = Path.Combine(work.FullName, "FC_HarnessCopyLayer.ir");

            // *** BOTH OBJECTS, AND THAT IS A FINDING RATHER THAN SETUP. *** The block alone does NOT
            // convert: the version network writes a base-prefixed literal (16#A93F2C71) and the converter
            // refuses to type it by magnitude, because `HX_ProgramVersion`'s DWord declaration lives in
            // the TAG TABLE. So the copy layer is two objects that must travel together, and a caller
            // handing the converter only the block gets exit 1. Nothing inside this component could have
            // told us that — the generator emits both and was perfectly happy.
            // .ir is LF, deliberately — the repo's own convention for this extension.
            File.WriteAllText(irPath, block.Ir.ReplaceLineEndings("\n"));
            File.WriteAllText(Path.Combine(work.FullName, "HarnessMirror.ir"), table.Ir.ReplaceLineEndings("\n"));

            // --project so the mirror tags resolve from the table beside it; --allow-blind-types because
            // the copy layer legitimately references blocks outside its own file (that is its whole job),
            // which is the flag's stated case.
            var (toXml, xmlOutput) = RunConverter("to-xml", irPath, "--project", work.FullName, "--allow-blind-types", "--out", work.FullName);
            Assert.True(toXml == 0, $"converter to-xml refused the generated copy layer (exit {toXml}):{Environment.NewLine}{xmlOutput}");

            var xmlPath = Path.Combine(work.FullName, "FC_HarnessCopyLayer.xml");
            Assert.True(File.Exists(xmlPath), "converter to-xml exited 0 and wrote no XML. Empty is not clean.");

            // The COIL survives as a Coil part in the SimaticML, which is the half a grammar-only check
            // would otherwise leave unexamined.
            var xml = File.ReadAllText(xmlPath);
            Assert.Contains("\"Coil\"", xml, StringComparison.Ordinal);
            Assert.Contains("\"Move\"", xml, StringComparison.Ordinal);

            // And back again. Byte-identical is the claim the old test names made and never checked.
            var readBack = Path.Combine(work.FullName, "back");
            Directory.CreateDirectory(readBack);

            // --project again, and for the same reason: --no-sidecar VERIFIES the sidecar is derivable,
            // and deriving it means typing that same hex literal from the tag table.
            var (toIr, irOutput) = RunConverter("to-ir", xmlPath, "--project", work.FullName, "--no-sidecar", "--out", readBack);
            Assert.True(toIr == 0, $"converter to-ir refused its own XML (exit {toIr}):{Environment.NewLine}{irOutput}");

            var returned = File.ReadAllText(Path.Combine(readBack, "FC_HarnessCopyLayer.ir"));
            Assert.Equal(block.Ir.ReplaceLineEndings("\n"), returned.ReplaceLineEndings("\n"));
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    [Fact]
    public void THE_CONVERTER_IS_AN_AUTHORITY_ON_GRAMMAR_AND_NOT_ON_TYPES_so_this_file_is_not_the_whole_check()
    {
        // *** MEASURED, AND RECORDED HERE SO NOBODY MISTAKES A GREEN ABOVE FOR A TYPE CHECK. *** The exact
        // shape TIA rejected — a Bool moved with a plain MOVE — converts cleanly. If this test ever fails
        // because the converter STARTED refusing it, that is good news and this test should be deleted in
        // favour of relying on it.
        var work = Directory.CreateTempSubdirectory("copylayer-typeblind-");
        try
        {
            var irPath = Path.Combine(work.FullName, "FC_TypeBlind.ir");

            File.WriteAllText(irPath, string.Join("\n", new[]
            {
                "BLOCK FC FC_TypeBlind",
                "ROOTID 0",
                "NUMBER 901",
                "LANGUAGE LAD",
                "TITLE \"A Bool moved with MOVE - the shape TIA refuses\"",
                string.Empty,
                "INTERFACE",
                "  INPUT",
                "  OUTPUT",
                "  CONSTANT",
                string.Empty,
                "NETWORK 1 \"Bool into an Int register\"",
                "  MOVE(EN := TRUE, IN := DB_Unit.Alarm) => HX_S0_R000",
                string.Empty,
            }));

            var (exit, output) = RunConverter("to-xml", irPath, "--allow-blind-types", "--out", work.FullName);

            Assert.True(exit == 0,
                "the converter refused `MOVE(IN := <Bool>)`. That would be a WELCOME change — it would mean the type "
                + "error is catchable before TIA — but it contradicts what this test records, so update the claim rather "
                + $"than silencing it. Exit {exit}:{Environment.NewLine}{output}");
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }
}
