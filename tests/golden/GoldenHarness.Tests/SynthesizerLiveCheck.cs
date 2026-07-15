namespace GoldenHarness;

/// <summary>
/// Sidecar synthesis (2026-07-15) — the live TIA proof. Builds a genuinely new, never-existed-in-
/// TIA scratch block at runtime, converts via `converter to-xml --synthesize` (no real SIDECAR
/// section anywhere in the input), imports and compiles against the real reference project, then
/// deletes the scratch block regardless of outcome.
///
/// The readable network body is `ir/reference/PerimeterSafetyAlarms.ir`'s own real Network 1
/// (S1 item 7 Phase A — a 3-way OR-merge of negated contacts plus five plain single-contact
/// assignments, some negated) — deliberately reused rather than the input-mapping/output-mapping
/// pattern excerpts first tried here: those reference real JOB9002 tags (`DiscreteInputs.OSCIsoFB`, `DI28`,
/// ...) that were never imported into SampleProject, which produced 104 real "tag not defined"
/// compile errors — a genuine finding, but about the live check's own tag choice, not the
/// synthesizer. PerimeterSafetyAlarms' own tags (`EquipmentStatus.SafetyZone1`,
/// `AlarmWords.AlarmWord1.%X0`, ...) are already part of SampleProject's committed reference
/// corpus, so referencing them here is safe and, being both an OR-merge and several plain-contact
/// shapes in one real source, gives richer coverage than either pattern excerpt alone.
///
/// This is the only tier of the sidecar-synthesis test suite that actually proves the
/// Part-ordering/signal-flow invariant `SidecarSynthesizer`'s own doc comment describes — an
/// in-memory round trip (`Converter.Tests/SidecarSynthesizerFidelityTests.cs`) walks by wire
/// lookup, never by UId order, so it can't catch a real Import()-time ordering rejection the way
/// this can. Same "manual/live, not CI" reasoning as <see cref="ReferenceProjectRoundTrip"/> —
/// needs a live Portal session, deliberately not an always-running [Fact]; call
/// <see cref="Run"/> by hand (or from a throwaway test with a [Fact] attribute added temporarily)
/// whenever the synthesizer changes.
///
/// Project path/device/group path reused verbatim from <see cref="ReferenceProjectRoundTrip"/> —
/// same reference project, same station.
/// </summary>
public static class SynthesizerLiveCheck
{
    private const string ProjectPath = @"C:\Users\User\Desktop\AI Ladder Project\SampleProject\SampleProject.ap20";
    private const string Device = "S7-1200 station_1";
    private const string GroupPath = "S7-1200 station_1/PLC1 6ES7 214-1AG40-0XB0";
    private const string BlockName = "SynthesizerProbe";
    private const int BlockNumber = 900; // clearly out of the reference corpus's own numbering range

    public static RoundTripReport Run(string workDir)
    {
        Directory.CreateDirectory(workDir);

        // Real, already-proven-in-SampleProject tag references (see class doc comment for why
        // this replaced the first attempt, which used real-but-wrong-project tags). Readable text
        // only — no SIDECAR section anywhere in this string, which is the entire point.
        var networkText =
            "NETWORK 1 \"Perimeter Safety Alarm Bit Mapping\"\n" +
            "  COIL AlarmWords.AlarmWord1.%X0 := NOT EquipmentStatus.SafetyZone1 OR NOT EquipmentStatus.SafetyZone2 OR NOT EquipmentStatus.SafetyZone3\n" +
            "  COIL AlarmWords.AlarmWord1.%X1 := NOT EquipmentStatus.SafetyZone1\n" +
            "  COIL AlarmWords.AlarmWord1.%X2 := NOT EquipmentStatus.SafetyZone2\n" +
            "  COIL AlarmWords.AlarmWord1.%X3 := NOT EquipmentStatus.SafetyZone3\n" +
            "  COIL AlarmWords.AlarmWord1.%X4 := NOT EquipmentStatus.SafetyGate1\n" +
            "  COIL AlarmWords.AlarmWord1.%X5 := EquipmentStatus.SafetyPullCord1\n" +
            "  COIL AlarmWords.AlarmWord1.%X6 := EquipmentStatus.FireDamper1\n" +
            "  COIL AlarmWords.AlarmWord1.%X7 := EquipmentStatus.FireDamper2\n";

        var blockText =
            $"BLOCK FC {BlockName}\nROOTID 0\nNUMBER {BlockNumber}\nLANGUAGE LAD\n\n" +
            "INTERFACE\n  INPUT\n  OUTPUT\n  CONSTANT\n\n" +
            networkText;

        var irPath = Path.Combine(workDir, $"{BlockName}.ir");
        File.WriteAllText(irPath, blockText);

        try
        {
            var toXml = ProcessRunner.Run(ToolPaths.ConverterExe, "to-xml", irPath, "--synthesize");
            if (toXml.ExitCode != 0)
            {
                return RoundTripReport.Failed("to-xml --synthesize", toXml);
            }

            var xmlPath = Path.ChangeExtension(irPath, ".xml");

            var runner = new RoundTripRunner();

            var import = runner.Import(ProjectPath, GroupPath, xmlPath);
            if (import.ExitCode != 0)
            {
                return RoundTripReport.Failed("import", import);
            }

            var compile = runner.Compile(ProjectPath, Device, BlockName);
            if (compile.ExitCode != 0 && GetErrorCount(compile.StdOut) != 0)
            {
                return RoundTripReport.Failed("compile", compile);
            }

            return RoundTripReport.Passed(irPath, xmlPath);
        }
        finally
        {
            // Clean up regardless of outcome — this is a clearly-scratch, always-disposable probe
            // block, same discipline as the established PlantAutoControl-in-SampleProject cleanup.
            ProcessRunner.Run(ToolPaths.OpennessCliExe, "delete", ProjectPath, "--block", BlockName, "--device", Device, "--yes");
        }
    }

    private static int GetErrorCount(string stdOut)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(stdOut);
            return document.RootElement.GetProperty("errors").GetInt32();
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return -1;
        }
    }
}
