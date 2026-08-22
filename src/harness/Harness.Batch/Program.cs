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
Func<IReadOnlyList<HarnessObject>, DeploymentOutcome>? deploy = null;

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
    deploy = objects => gateway.Deploy(objects, default);
}

return BatchCli.Run(args, Console.Out, File.ReadAllText, File.WriteAllText, runner, deploy);

string? ValueOf(string flag)
{
    var index = Array.IndexOf(args, flag);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
