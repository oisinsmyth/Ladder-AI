using System.Text.Json;

namespace GoldenHarness;

/// <summary>
/// Orchestrates one block through the full Layer-1 round trip (docs/08-testing-strategy.md):
/// export -> to-ir -> to-xml -> import -> compile -> re-export. Each stage is a separate method
/// so both the automated NormalizerTests (which never touch a live Portal) and a manual
/// one-off run against a real project (S1 walking-skeleton plan step 4) can reuse the same code.
/// </summary>
public sealed class RoundTripRunner
{
    public ProcessResult Export(string project, string blockName, string? device, string outPath)
    {
        var args = new List<string> { "export", project, "--block", blockName, "--out", outPath };
        if (device is not null)
        {
            args.Add("--device");
            args.Add(device);
        }

        return ProcessRunner.Run(ToolPaths.OpennessCliExe, args.ToArray());
    }

    public ProcessResult ToIr(string file) => ProcessRunner.Run(ToolPaths.ConverterExe, "to-ir", file);

    public ProcessResult ToXml(string file) => ProcessRunner.Run(ToolPaths.ConverterExe, "to-xml", file);

    public ProcessResult Import(string project, string groupPath, params string[] files)
    {
        var args = new List<string> { "import", project, "--group", groupPath };
        args.AddRange(files);
        return ProcessRunner.Run(ToolPaths.OpennessCliExe, args.ToArray());
    }

    public ProcessResult Compile(string project, string? device, string? blockName = null)
    {
        var args = new List<string> { "compile", project, "--json" };
        if (device is not null)
        {
            args.Add("--device");
            args.Add(device);
        }

        if (blockName is not null)
        {
            args.Add("--block");
            args.Add(blockName);
        }

        return ProcessRunner.Run(ToolPaths.OpennessCliExe, args.ToArray());
    }

    /// <summary>
    /// Runs every stage in sequence against a real project/block. Stops at the first failing
    /// stage. This is what the manual live proof (S1 walking-skeleton plan step 4) calls
    /// directly — it is deliberately not wired into an xUnit [Fact] here, since it needs a live
    /// Portal session and a real project and is run once, by hand, with results recorded in
    /// docs/notes/stage-gates.md rather than asserted in CI.
    /// </summary>
    public RoundTripReport RunFull(string project, string blockName, string? device, string groupPath, string workDir)
    {
        Directory.CreateDirectory(workDir);
        var exportedPath = Path.Combine(workDir, $"{blockName}.xml");
        var reExportedPath = Path.Combine(workDir, $"{blockName}.reexported.xml");

        var export = Export(project, blockName, device, exportedPath);
        if (export.ExitCode != 0)
        {
            return RoundTripReport.Failed("export", export);
        }

        var toIr = ToIr(exportedPath);
        if (toIr.ExitCode != 0)
        {
            return RoundTripReport.Failed("to-ir", toIr);
        }

        var irPath = Path.ChangeExtension(exportedPath, ".ir");

        var toXml = ToXml(irPath);
        if (toXml.ExitCode != 0)
        {
            return RoundTripReport.Failed("to-xml", toXml);
        }

        var regeneratedXmlPath = Path.ChangeExtension(irPath, ".xml");

        var import = Import(project, groupPath, regeneratedXmlPath);
        if (import.ExitCode != 0)
        {
            return RoundTripReport.Failed("import", import);
        }

        // Block-level, not device-level: confirmed real, 2026-07-10 (docs/notes/openness-quirks.md)
        // — device-level compile reports Success without ever clearing a freshly-imported block's
        // IsConsistent flag, so the re-export below would refuse ("Inconsistent blocks... cannot
        // be exported") even on genuinely correct content. Block-level compile is what actually
        // clears it.
        // openness-cli compile's own exit code is non-zero for any State != Success, including a
        // benign hardware-config Warning with errors=0 — stricter than this project's own
        // established "0 errors is clean, warnings are expected" bar (e.g. stage-gates.md
        // repeatedly accepts "STATE: Warning, ERRORS: 0" as a clean compile). Parse the JSON body
        // and gate on the actual error count rather than the raw exit code.
        var compile = Compile(project, device, blockName);
        if (compile.ExitCode != 0 && GetErrorCount(compile.StdOut) != 0)
        {
            return RoundTripReport.Failed("compile", compile);
        }

        var reExport = Export(project, blockName, device, reExportedPath);
        if (reExport.ExitCode != 0)
        {
            return RoundTripReport.Failed("re-export", reExport);
        }

        return RoundTripReport.Passed(exportedPath, reExportedPath);
    }

    /// <summary>
    /// Reads the `errors` field from `compile --json` output. Returns -1 (never equal to a real
    /// error count, so callers' `!= 0` checks still fail closed) if the output isn't the expected
    /// shape — deliberately fail-safe rather than silently treating an unparseable result (e.g. a
    /// connection failure that never reached compile at all) as "0 errors, clean."
    /// </summary>
    private static int GetErrorCount(string stdOut)
    {
        try
        {
            using var document = JsonDocument.Parse(stdOut);
            return document.RootElement.GetProperty("errors").GetInt32();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return -1;
        }
    }
}

public sealed record RoundTripReport(bool Success, string? FailedStage, ProcessResult? FailureResult, string? OriginalExportPath, string? ReExportPath)
{
    public static RoundTripReport Failed(string stage, ProcessResult result) => new(false, stage, result, null, null);

    public static RoundTripReport Passed(string originalExportPath, string reExportPath) => new(true, null, null, originalExportPath, reExportPath);
}
