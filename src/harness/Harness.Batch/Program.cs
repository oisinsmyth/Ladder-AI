using Harness.Run;
using System.Text.Json;
using Harness.Batch;
using Harness.Device;
using Harness.Loop;
using Harness.Map;

// The composition root, and it decides NOTHING except how to build the two things that touch the
// outside world. Every decision lives in BatchCli and BatchRunPlan so it is testable without a process
// — a decision only reachable through a process is a decision nobody tests.

var staging = ValueOf("--staging") ?? Path.GetTempPath();
var runner = new ProcessRunner(staging);

// 🔴 THE DEPLOYMENT GATEWAY IS BUILT ONLY WHEN --deploy-config NAMES ONE, AND ITS ABSENCE IS A REFUSAL
// RATHER THAN A DEFAULT. DeviceGatewayOptions carries ten required values — the Portal project, the
// group path, the three binaries, the download target — and not one of them has a safe guess. A default
// here would be this file inventing where to write a program and which controller to download it to.
Func<IReadOnlyList<string>, DeploymentOutcome>? deploy = null;

var configPath = ValueOf("--deploy-config");
if (configPath is not null)
{
    if (!File.Exists(configPath))
    {
        Console.Error.WriteLine($"--deploy-config not found: {configPath}");
        return BatchExit.Unusable;
    }

    DeviceGatewayOptions options;
    try
    {
        options = JsonSerializer.Deserialize<DeviceGatewayOptions>(
            File.ReadAllText(configPath),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            })
            ?? throw new JsonException("the document is null.");
    }
    catch (Exception error) when (error is JsonException or IOException)
    {
        Console.Error.WriteLine($"--deploy-config at {configPath} could not be read: {error.Message}");
        return BatchExit.Unusable;
    }

    var gateway = new OpennessDeviceGateway(options, runner);

    // 🔴 GENERATION HAPPENS HERE, IN-PROCESS, AND ITS OBJECTS GO STRAIGHT TO THE GATEWAY.
    //
    // The plan also has a Generate STEP that shells out to `harness-run --generate-only --emit`, and
    // that stays: it writes the copy layer where a person can read it, which is worth having. But the
    // deployment cannot be fed from those files — `--emit` writes `<name>.ir` and nothing else, while
    // the gateway routes imports on HarnessObjectKind, so loading them back would mean INFERRING
    // TagTable-from-Block out of file content. Generated in-process, each object still carries the Kind
    // its generator gave it.
    var mergedPath = ValueOf("--merged");
    var submissionPath = ValueOf("--deploy-submission");

    deploy = programPaths =>
    {
        if (mergedPath is null || submissionPath is null)
        {
            return new DeploymentOutcome(false, false, new HashSet<string>(),
                "--merged and --deploy-submission are both required to generate the copy layer that would be deployed. "
                + "Nothing was written and no download was attempted.");
        }

        LoopRequest request;
        try
        {
            // 🔴 THE PROGRAM UNDER TEST, LOADED FROM THE SAME PATHS THE WAVES USE.
            //
            // This passed Array.Empty<HarnessObject>(). The build stamp covers the program under test, so
            // the device was stamped over ZERO objects while every wave computes over the real ones —
            // measured on the rig as device 16#CBE1D692 against staged 16#679E7923, with the download
            // having in fact succeeded. The wave then refuses with a version mismatch whose text says the
            // device is not running this build, which is true and points at entirely the wrong cause.
            //
            // ProgramUnderTest.Load is the loader harness-run itself uses, so the two cannot disagree
            // about what a directory of .ir files contains.
            var program = ProgramUnderTest.Load(
                programPaths,
                File.ReadAllText,
                path => Directory.Exists(path)
                    ? Directory.GetFiles(path, "*.ir").OrderBy(p => p, StringComparer.Ordinal).ToArray()
                    : new[] { path });

            request = LoopCli.Compose(
                Harness.Gate.SubmissionDocument.Read(File.ReadAllText(submissionPath)),
                Harness.Gate.BindingDocument.Read(File.ReadAllText(mergedPath)),
                program,
                File.ReadAllText,
                File.ReadAllBytes);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException)
        {
            return new DeploymentOutcome(false, false, new HashSet<string>(),
                "the merged binding or the submission could not be composed, so nothing was generated and nothing was "
                + $"written: {error.Message}");
        }

        var generation = LoopRun.Generate(request, stopWhenInadmissible: false);
        if (generation.CopyLayer?.Objects is not { Count: > 0 } objects)
        {
            return new DeploymentOutcome(false, false, new HashSet<string>(),
                "generation produced no objects, so there was nothing to deploy. " + (generation.Detail));
        }

        return gateway.Deploy(objects, generation.Stamp);
    };
}

return BatchCli.Run(args, Console.Out, File.ReadAllText, File.WriteAllText, runner, deploy);

string? ValueOf(string flag)
{
    var index = Array.IndexOf(args, flag);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
