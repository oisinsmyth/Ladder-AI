using System;
using OpennessCli.Cli;
using OpennessCli.Openness;

namespace OpennessCli;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var parseResult = ArgumentParser.Parse(args);
        if (parseResult is ParseResult.Failure failure)
        {
            Console.Error.WriteLine(failure.Message);
            return ExitCodes.UsageError;
        }

        var options = ((ParseResult.Success)parseResult).Options;

        try
        {
            // Fail fast with a clear message before touching Portal at all.
            TiaInstallLocator.Resolve(options.TiaInstallOverride);
        }
        catch (TiaInstallNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.EnvironmentError;
        }

        using IOpennessGateway gateway = new OpennessGateway();
        try
        {
            gateway.Connect(TimeSpan.FromSeconds(options.TimeoutConnectSeconds));
            gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(options.TimeoutOpenSeconds));
            var blocks = gateway.EnumerateBlocks();

            Console.WriteLine(options.Json ? OutputFormatter.FormatJson(blocks) : OutputFormatter.FormatTable(blocks));
            return ExitCodes.Success;
        }
        catch (ConnectTimeoutException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.ConnectTimeout;
        }
        catch (ProjectOpenTimeoutException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.ProjectOpenTimeout;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"openness-cli list failed: {ex.Message}");
            return ExitCodes.UnexpectedError;
        }
    }
}

internal static class ExitCodes
{
    public const int Success = 0;
    public const int UsageError = 1;
    public const int EnvironmentError = 2;
    public const int ConnectTimeout = 3;
    public const int ProjectOpenTimeout = 4;
    public const int UnexpectedError = 5;
}
