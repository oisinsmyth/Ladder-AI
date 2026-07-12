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

        var (tiaInstallOverride, timeoutConnectSeconds, timeoutOpenSeconds) = CommonOptions(parseResult);

        try
        {
            // Fail fast with a clear message before touching Portal at all.
            TiaInstallLocator.Resolve(tiaInstallOverride);
        }
        catch (TiaInstallNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.EnvironmentError;
        }

        using IOpennessGateway gateway = new OpennessGateway();
        try
        {
            gateway.Connect(TimeSpan.FromSeconds(timeoutConnectSeconds));

            switch (parseResult)
            {
                case ParseResult.ListSuccess list:
                    return RunList(gateway, list.Options, timeoutOpenSeconds);
                case ParseResult.ExportSuccess export:
                    return RunExport(gateway, export.Options, timeoutOpenSeconds);
                case ParseResult.ImportSuccess import:
                    return RunImport(gateway, import.Options, timeoutOpenSeconds);
                case ParseResult.CompileSuccess compile:
                    return RunCompile(gateway, compile.Options, timeoutOpenSeconds);
                case ParseResult.DeleteSuccess delete:
                    return RunDelete(gateway, delete.Options, timeoutOpenSeconds);
                case ParseResult.SanityCheckSuccess sanityCheck:
                    return RunSanityCheck(gateway, sanityCheck.Options, timeoutOpenSeconds);
                default:
                    throw new InvalidOperationException($"Unhandled parse result: {parseResult.GetType().Name}");
            }
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
        catch (SafetyContentRefusedException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.SafetyRefused;
        }
        catch (Exception ex) when (ex is BlockNotFoundException or AmbiguousBlockException or DeviceNotFoundException or ExportProducedNoFileException)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.CommandError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"openness-cli {args[0]} failed: {DescribeWithInnerExceptions(ex)}");
            return ExitCodes.UnexpectedError;
        }
    }

    private static string DescribeWithInnerExceptions(Exception ex)
    {
        var messages = new List<string>();
        for (var current = ex; current is not null; current = current.InnerException)
        {
            messages.Add($"{current.GetType().Name}: {current.Message}");
        }

        return string.Join(" ---> ", messages);
    }

    private static int RunList(IOpennessGateway gateway, ListOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var blocks = gateway.EnumerateBlocks();
        Console.WriteLine(options.Json ? OutputFormatter.FormatJson(blocks) : OutputFormatter.FormatTable(blocks));
        return ExitCodes.Success;
    }

    private static int RunExport(IOpennessGateway gateway, ExportCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        gateway.ExportBlock(options.BlockName, options.Device, options.OutPath);
        Console.WriteLine($"Exported '{options.BlockName}' -> {options.OutPath}");
        return ExitCodes.Success;
    }

    private static int RunImport(IOpennessGateway gateway, ImportCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var imported = gateway.ImportBlocks(options.GroupPath, options.Files);
        Console.WriteLine(OutputFormatter.FormatTable(imported));
        return ExitCodes.Success;
    }

    private static int RunCompile(IOpennessGateway gateway, CompileCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var result = options.Block is null
            ? gateway.Compile(options.Device)
            : gateway.CompileBlock(options.Block, options.Device);
        Console.WriteLine(options.Json ? OutputFormatter.FormatCompileJson(result) : OutputFormatter.FormatCompileTable(result));
        return result.State == Model.CompileState.Success ? ExitCodes.Success : ExitCodes.CompileFailed;
    }

    private static int RunDelete(IOpennessGateway gateway, DeleteCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var info = gateway.DeleteBlock(options.BlockName, options.Device, options.Confirm);
        if (!options.Confirm)
        {
            Console.WriteLine($"Would delete '{info.Name}' ({info.Path}) — pass --yes to confirm.");
            return ExitCodes.NotConfirmed;
        }

        Console.WriteLine($"Deleted '{info.Name}' ({info.Path}).");
        return ExitCodes.Success;
    }

    private static int RunSanityCheck(IOpennessGateway gateway, ListOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var result = gateway.RunSanityCheck();
        Console.WriteLine(options.Json ? OutputFormatter.FormatSanityCheckJson(result) : OutputFormatter.FormatSanityCheckTable(result));
        return result.IsHealthy ? ExitCodes.Success : ExitCodes.SanityCheckFailed;
    }

    private static (string? TiaInstallOverride, int TimeoutConnectSeconds, int TimeoutOpenSeconds) CommonOptions(ParseResult result) => result switch
    {
        ParseResult.ListSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.ExportSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.ImportSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.CompileSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.DeleteSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.SanityCheckSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        _ => throw new InvalidOperationException($"Unhandled parse result: {result.GetType().Name}"),
    };
}

internal static class ExitCodes
{
    public const int Success = 0;
    public const int UsageError = 1;
    public const int EnvironmentError = 2;
    public const int ConnectTimeout = 3;
    public const int ProjectOpenTimeout = 4;
    public const int UnexpectedError = 5;
    public const int SafetyRefused = 6;
    public const int CommandError = 7;
    public const int CompileFailed = 8;
    public const int SanityCheckFailed = 9;
    public const int NotConfirmed = 10;
}
