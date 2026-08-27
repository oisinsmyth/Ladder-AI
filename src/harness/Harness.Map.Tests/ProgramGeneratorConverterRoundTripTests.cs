using System.Diagnostics;
using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE GENERATED OB AND INSTANCE DB, THROUGH THE REAL CONVERTER — the second outside authority.</b>
///
/// <para><see cref="InstanceDbAgainstTheCommittedCorpusTests"/> shows the generators reproduce committed
/// files byte for byte. This file asks a different party a different question: <b>does the IR they emit
/// PARSE, and does it convert to SimaticML?</b> A generated block the converter refuses is worth nothing
/// however well it reads, and the component's own tests cannot fail that way — they compare the generator's
/// output against what its author expected the generator to emit.</para>
///
/// <para><b>Neither this nor the corpus file is the compile gate</b> (hard rule 4). <c>to-xml</c> proves the
/// document is well-formed to the converter; only TIA's own import proves it is acceptable to TIA.</para>
/// </summary>
public class ProgramGeneratorConverterRoundTripTests
{
    /// <summary>
    /// The converter binary, Release first (what the skills invoke), Debug second.
    /// <b>Absent is a FAILURE, not a skip</b> — an optional check is one that stops running.
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
                + ". *** THIS IS A FAILURE, NOT A REASON TO SKIP. *** Build it with: "
                + "dotnet build -c Release src/converter/converter.sln (free and safe at any time — the converter never "
                + "touches Portal). Without it the only authority left in this component's loop is the component itself.");
        }
    }

    private static (int Exit, string Output) RunConverter(params string[] args)
    {
        // Redirected, never piped — and safe here: the converter is a pure in-process file transformer with
        // no child process, so it carries none of openness-cli's inherited-handle hazard.
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

    private const string Fb = """
        BLOCK FB FB_Widget
        ROOTID 0
        NUMBER 9100
        LANGUAGE LAD
        TITLE "Widget"

        INTERFACE
          INPUT
          OUTPUT
          STATIC
            Cmd : "UDT_WidgetIO" RETAIN SETPOINT COMMENT "Caller interface."
              Start : Bool
              DwellTime : Time
            Running : Bool
            DwellTimer : TON_TIME VERSION 1.0 SETPOINT
              PT : Time
              ET : Time
              IN : Bool
              Q : Bool
          CONSTANT

        NETWORK 1 "Run"
          COIL Running := Cmd.Start
        """;

    private static ProgramGenerationResult Generated() =>
        ProgramGenerator.Generate(
            new ProgramDeclaration(
                new CyclicObDeclaration(
                    new CyclicObNaming("OB_HarnessCycle", 1, "ProgramCycle", "Harness Program Sweep"),
                    new[]
                    {
                        new ObCall("FB_Widget", "iDB_Widget", "Run The Widget",
                                   "Ahead of the copy layer so the mirror publishes THIS scan's outputs."),
                        new ObCall("FC_HarnessCopyLayer", null, "Harness Mirror Copy Layer"),
                    }),
                new[]
                {
                    new InstanceDbDeclaration(
                        new InstanceDbNaming("iDB_Widget", 9200, "FB_Widget", "One widget instance. Synthetic test material."),
                        InstanceDbMemberSource.ProjectedFromFb,
                        new[] { new InstanceDbPreset("Cmd.DwellTime", "T#12S") }),
                }),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["FB_Widget"] = Fb },
            copyLayerBlock: "FC_HarnessCopyLayer");

    /// <summary>
    /// 🔴 <b>BOTH GENERATED OBJECTS CONVERT — the OB with its two calls and its network comment, and the
    /// instance DB with a nested UDT member, a system-type timer instance and a declared preset.</b>
    /// </summary>
    [Fact]
    public void THE_GENERATED_OB_AND_INSTANCE_DB_CONVERT_TO_SIMATICML()
    {
        var result = Generated();
        Assert.False(result.Refused);

        var work = Directory.CreateTempSubdirectory("programgen-roundtrip-");
        try
        {
            foreach (var obj in result.Objects)
            {
                var irPath = Path.Combine(work.FullName, obj.Name + ".ir");
                File.WriteAllText(irPath, obj.Ir);

                var (exit, output) = RunConverter("to-xml", irPath);

                Assert.True(exit == 0, $"converter to-xml refused the generated {obj.Kind} '{obj.Name}' (exit {exit}): {output}");
                Assert.True(File.Exists(Path.Combine(work.FullName, obj.Name + ".xml")),
                    $"converter to-xml reported success for '{obj.Name}' and wrote no XML.");
            }
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }
}
